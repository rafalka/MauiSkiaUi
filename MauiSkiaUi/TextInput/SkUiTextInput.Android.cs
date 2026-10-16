#if ANDROID
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using Android.Content;
using Android.Text;
using Android.Util;
using Android.Views;
using Android.Views.Autofill;
using Android.Views.InputMethods;
using Android.Widget;
using Java.Lang;
using Math = System.Math;
using String = Java.Lang.String;
using View = Android.Views.View;

namespace MauiSkiaUi;

/// <summary>
/// The text input of one surface on Android (<see cref="ISkUiTextInputProxy"/>): while a drawn field is focused, the surface's
/// container is a text editor (<c>onCheckIsTextEditor</c>) whose <c>InputConnection</c> is a <see cref="BaseInputConnection"/> over an
/// <see cref="IEditable"/> mirror of the field. Android implements the protocol's details on the mirror (composing spans, surrounding
/// text deletes, batch edits); after each batch the mirror's change reaches the field as one edit, and the field's changes from
/// keys and the app go back to the mirror and the input method (<c>updateSelection</c>, <c>restartInput</c>).
/// It is also the surface's autofill (D14): the drawn fields are virtual children of the container's autofill structure.
/// </summary>
internal sealed class SkUiAndroidTextInput(SkUiOverlayContainer container) : ISkUiTextInputProxy, ISkUiAutofillHost, IDisposable
{
    private readonly InputMethodManager? _imm = container.Context?.GetSystemService(Context.InputMethodService) as InputMethodManager;
    private SkUiInputConnection? _connection;

    /// <summary>The attached field.</summary>
    internal ISkUiTextInputClient? Client { get; private set; }

    /// <summary>Hardware keys type through key events (the container's <c>dispatchKeyEvent</c>), not the input method.</summary>
    public bool ReceivesCharacters => false;

    public void Attach(ISkUiTextInputClient client)
    {
        Client = client;
        client.InputAttached(this);
        _connection = null;
        _imm?.RestartInput(container);
        // Focus alone: an implicit request, which the input method may decline (a hardware keyboard attached).
        Show(client, ShowFlags.Implicit);
        // As a native field's focus: autofill offers what fits the form (asking for the structure the first time).
        if (OperatingSystem.IsAndroidVersionAtLeast(26) && AutofillManager is { IsEnabled: true } autofill)
            autofill.NotifyViewEntered(container, AutofillIdOf(client), ScreenBounds(client));
    }

    public void Detach(ISkUiTextInputClient client)
    {
        client.InputAttached(null);
        if (!ReferenceEquals(Client, client))
            return;
        Client = null;
        HideMagnifier(client);
        if (OperatingSystem.IsAndroidVersionAtLeast(26) && AutofillManager is { IsEnabled: true } autofill && _autofillIds.TryGetValue(client, out var id))
            autofill.NotifyViewExited(container, (int)id);
        _connection?.Disconnect();
        _connection = null;
        _imm?.RestartInput(container);
        // Focus moving on to another field keeps the keyboard.
        if (container.KeyboardFocus?.Focused is not ISkUiTextInputNode { WantsTextInput: true })
            _imm?.HideSoftInputFromWindow(container.WindowToken, HideSoftInputFlags.None);
    }

    /// <summary>The user asked for the keyboard (a tap on the field, a screen reader's activation): explicit, as <c>TextView</c> asks on a tap.</summary>
    public void ShowKeyboard(ISkUiTextInputClient client) => Show(client, 0);

    private void Show(ISkUiTextInputClient client, ShowFlags flags)
    {
        if (!ReferenceEquals(Client, client) || client.Options.IsReadOnly)
            return;
        // After the focus change has reached the window (showSoftInput needs the focused, attached view).
        container.Post(() =>
        {
            if (ReferenceEquals(Client, client))
                _imm?.ShowSoftInput(container, flags);
        });
    }

    public void HideKeyboard(ISkUiTextInputClient client)
    {
        if (ReferenceEquals(Client, client))
            _imm?.HideSoftInputFromWindow(container.WindowToken, HideSoftInputFlags.None);
    }

    public void StateChanged(ISkUiTextInputClient client, bool textChanged)
    {
        if (ReferenceEquals(Client, client))
            _connection?.Push(textChanged);
    }

    public void OptionsChanged(ISkUiTextInputClient client)
    {
        if (!ReferenceEquals(Client, client))
            return;
        _connection = null;
        _imm?.RestartInput(container);
    }

    public void GeometryChanged(ISkUiTextInputClient client)
    {
        if (!ReferenceEquals(Client, client))
            return;
        _connection?.UpdateCursorAnchor(force: false);
        // The menu exists only on API 23+ (floating action modes; ShowEditMenu checks).
        if (OperatingSystem.IsAndroidVersionAtLeast(23))
            _menu?.Mode?.InvalidateContentRect();
    }

    private SkUiEditMenu? _menu;

    /// <summary>Android's floating text selection toolbar (cut, copy, paste, select all, in the system's language) over <paramref name="anchor"/>.</summary>
    public void ShowEditMenu(ISkUiTextInputClient client, Rect anchor, bool byTouch)
    {
        if (!ReferenceEquals(Client, client) || !OperatingSystem.IsAndroidVersionAtLeast(23))
            return;
        HideEditMenu(client);
        var menu = new SkUiEditMenu(client, anchor, container.Resources?.DisplayMetrics?.Density ?? 1);
        _menu = menu;
        menu.Mode = container.StartActionMode(menu, ActionModeType.Floating);
        if (menu.Mode is null)
            _menu = null;
    }

    public void HideEditMenu(ISkUiTextInputClient client)
    {
        var menu = _menu;
        _menu = null;
        menu?.Mode?.Finish();
    }

    private Magnifier? _magnifier;

    /// <summary>
    /// Android's magnifier (API 28+) over <paramref name="caret"/>, as a native field's while a handle moves. It copies the window,
    /// where the drawn surface (a texture view) is.
    /// </summary>
    public void ShowMagnifier(ISkUiTextInputClient client, Rect caret)
    {
        if (!ReferenceEquals(Client, client) || !OperatingSystem.IsAndroidVersionAtLeast(28))
            return;
        var density = container.Resources?.DisplayMetrics?.Density ?? 1;
        _magnifier ??= OperatingSystem.IsAndroidVersionAtLeast(29) ? new Magnifier.Builder(container).Build() : new Magnifier(container);
        _magnifier.Show((float)(caret.Center.X * density), (float)(caret.Center.Y * density));
        // The magnifier copies the window now; the moved caret is drawn by the surface's GL thread a frame or two later.
        if (!_magnifierRefreshPending)
        {
            _magnifierRefreshPending = true;
            container.PostDelayed(() =>
            {
                _magnifierRefreshPending = false;
                if (OperatingSystem.IsAndroidVersionAtLeast(28) && _magnifierShown)
                    _magnifier?.Update();
            }, MagnifierRefreshDelayMs);
        }
        _magnifierShown = true;
    }

    public void HideMagnifier(ISkUiTextInputClient client)
    {
        _magnifierShown = false;
        if (OperatingSystem.IsAndroidVersionAtLeast(28))
            _magnifier?.Dismiss();
    }

    // Two frames at 60 Hz: the surface has drawn the caret's new place.
    private const long MagnifierRefreshDelayMs = 34;
    private bool _magnifierShown;
    private bool _magnifierRefreshPending;

    #region Autofill (D14)

    private const string EditTextClass = "android.widget.EditText";

    // Android's hint names (View.AUTOFILL_HINT_*, androidx.autofill.HintConstants) for SkUiAutofillHints.
    private static readonly string[] UsernameHints = ["username"];
    private static readonly string[] PasswordHints = ["password"];
    private static readonly string[] NewPasswordHints = ["newPassword"];
    private static readonly string[] EmailHints = ["emailAddress"];
    private static readonly string[] PhoneHints = ["phoneNumber", "phone"];
    private static readonly string[] OneTimeCodeHints = ["smsOTPCode"];
    private static readonly string[] NameHints = ["personName", "name"];
    private static readonly string[] PostalAddressHints = ["postalAddress"];
    private static readonly string[] PostalCodeHints = ["postalCode"];
    private static readonly string[] CreditCardHints = ["creditCardNumber"];

    // Virtual ids of the fields autofill knows (stable while a field is on this surface), both ways.
    private readonly ConditionalWeakTable<ISkUiTextInputClient, object> _autofillIds = new();
    private readonly Dictionary<int, WeakReference<ISkUiTextInputClient>> _autofillFields = [];
    private readonly List<ISkUiTextInputClient> _formFields = [];
    private readonly int[] _location = new int[2];
    private int _lastAutofillId;

    /// <summary>The window's autofill manager (API 26+), or <c>null</c>.</summary>
    [SupportedOSPlatform("android26.0")]
    private AutofillManager? AutofillManager =>
        field ??= container.Context?.GetSystemService(Java.Lang.Class.FromType(typeof(AutofillManager))) as AutofillManager;

    /// <summary>The virtual id of <paramref name="client"/> in the container's autofill structure; the field reports to this surface from now on.</summary>
    private int AutofillIdOf(ISkUiTextInputClient client)
    {
        if (_autofillIds.TryGetValue(client, out var known))
            return (int)known;
        var id = ++_lastAutofillId;
        _autofillIds.Add(client, id);
        _autofillFields[id] = new WeakReference<ISkUiTextInputClient>(client);
        client.AutofillHost?.Removed(client);
        client.AutofillHost = this;
        return id;
    }

    /// <summary>
    /// The container's <c>onProvideAutofillVirtualStructure</c>: the surface's drawn fields as <c>EditText</c>s (hints, value,
    /// input type, placeholder, bounds), after the container's native children (hosted views keep their own autofill). Without
    /// drawn fields, nothing: the container's children are described as usual.
    /// </summary>
    [SupportedOSPlatform("android26.0")]
    internal void ProvideAutofillStructure(ViewStructure structure, AutofillFlags flags)
    {
        container.KeyboardFocus?.CollectTextFields(_formFields);
        try
        {
            if (_formFields.Count == 0 || structure.AutofillId is not { } parent)
                return;
            var natives = 0;
            for (var index = 0; index < container.ChildCount; index++)
                if (container.GetChildAt(index) is { Visibility: ViewStates.Visible })
                    natives++;
            var next = structure.AddChildCount(natives + _formFields.Count);
            for (var index = 0; index < container.ChildCount; index++)
                if (container.GetChildAt(index) is { Visibility: ViewStates.Visible } child && structure.NewChild(next++) is { } node)
                    child.DispatchProvideAutofillStructure(node, flags);
            var density = container.Resources?.DisplayMetrics?.Density ?? 1;
            foreach (var field in _formFields)
                if (structure.NewChild(next++) is { } node)
                    Describe(node, parent, field, density);
        }
        finally
        {
            _formFields.Clear();
        }
    }

    [SupportedOSPlatform("android26.0")]
    private void Describe(ViewStructure node, AutofillId parent, ISkUiTextInputClient client, float density)
    {
        var options = client.Options;
        var editor = client.Editor;
        node.SetAutofillId(parent, AutofillIdOf(client));
        node.SetAutofillType(AutofillType.Text);
        if (HintsOf(options.AutofillHint) is { } hints)
            node.SetAutofillHints(hints);
        node.SetAutofillValue(AutofillValue.ForText(editor.Text));
        // Typed by the user, not from the app's resources (as a native field's).
        node.SetDataIsSensitive(true);
        node.SetClassName(EditTextClass);
        node.SetInputType((int)InputTypeOf(options));
        if (client.Placeholder is { Length: > 0 } placeholder)
            node.Hint = placeholder;
        if (!options.IsPassword)
            node.SetText(editor.Text, editor.SelectionStart, editor.SelectionEnd);
        node.SetVisibility(ViewStates.Visible);
        node.SetEnabled(true);
        node.SetFocusable(true);
        node.SetFocused(ReferenceEquals(Client, client));
        if (OperatingSystem.IsAndroidVersionAtLeast(28) && editor.MaxLength < int.MaxValue)
            node.SetMaxTextLength(editor.MaxLength);
        var bounds = client.GetFieldBounds();
        node.SetDimens((int)Math.Floor(bounds.X * density), (int)Math.Floor(bounds.Y * density), 0, 0,
            (int)Math.Ceiling(bounds.Width * density), (int)Math.Ceiling(bounds.Height * density));
    }

    /// <summary>Android's hints for a field's <c>AutofillHint</c>: <see cref="SkUiAutofillHints"/> mapped, any other string as it is.</summary>
    private static string[]? HintsOf(string? hint) => hint switch
    {
        null or "" => null,
        SkUiAutofillHints.Username => UsernameHints,
        SkUiAutofillHints.Password => PasswordHints,
        SkUiAutofillHints.NewPassword => NewPasswordHints,
        SkUiAutofillHints.EmailAddress => EmailHints,
        SkUiAutofillHints.PhoneNumber => PhoneHints,
        SkUiAutofillHints.OneTimeCode => OneTimeCodeHints,
        SkUiAutofillHints.Name => NameHints,
        SkUiAutofillHints.PostalAddress => PostalAddressHints,
        SkUiAutofillHints.PostalCode => PostalCodeHints,
        SkUiAutofillHints.CreditCardNumber => CreditCardHints,
        _ => [hint]
    };

    /// <summary>The container's <c>autofill(SparseArray)</c>: a service fills fields by virtual id, each as one undo step (formatted as a paste).</summary>
    [SupportedOSPlatform("android26.0")]
    internal void Autofill(SparseArray values)
    {
        for (var index = 0; index < values.Size(); index++)
            if (values.ValueAt(index) is AutofillValue { IsText: true } value && _autofillFields.TryGetValue(values.KeyAt(index), out var known)
                && known.TryGetTarget(out var client))
                client.SetTextFromAssistant(value.TextValue?.ToString() ?? string.Empty);
    }

    /// <summary>A known field's text changed: autofill gets the value (which it saves on commit).</summary>
    public void TextChanged(ISkUiTextInputClient client)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(26) && AutofillManager is { IsEnabled: true } autofill && _autofillIds.TryGetValue(client, out var id))
            autofill.NotifyValueChanged(container, (int)id, AutofillValue.ForText(client.Editor.Text));
    }

    /// <summary>A known field left the surface: autofill sees it hidden (with a service that saves once a form's fields are gone, it offers to now).</summary>
    public void Removed(ISkUiTextInputClient client)
    {
        if (!_autofillIds.TryGetValue(client, out var id))
            return;
        _autofillIds.Remove(client);
        _autofillFields.Remove((int)id);
        if (ReferenceEquals(client.AutofillHost, this))
            client.AutofillHost = null;
        if (OperatingSystem.IsAndroidVersionAtLeast(27) && AutofillManager is { IsEnabled: true } autofill)
            autofill.NotifyViewVisibilityChanged(container, (int)id, false);
    }

    /// <summary>
    /// The container became visible or hidden (its page shown or left, the container attached or detached): the known fields with
    /// it, since autofill does not derive a virtual child's visibility from its parent's.
    /// </summary>
    internal void SurfaceVisibilityChanged(bool visible)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(27) || AutofillManager is not { IsEnabled: true } autofill)
            return;
        foreach (var (id, known) in _autofillFields)
            if (known.TryGetTarget(out _))
                autofill.NotifyViewVisibilityChanged(container, id, visible);
    }

    /// <summary>The surface goes: its fields are hidden to autofill and let go.</summary>
    public void Dispose()
    {
        SurfaceVisibilityChanged(false);
        foreach (var known in _autofillFields.Values)
            if (known.TryGetTarget(out var client) && ReferenceEquals(client.AutofillHost, this))
                client.AutofillHost = null;
        _autofillFields.Clear();
        _autofillIds.Clear();
    }

    /// <summary>A field's bounds on the screen, in pixels (where autofill anchors its suggestions).</summary>
    private Android.Graphics.Rect ScreenBounds(ISkUiTextInputClient client)
    {
        var bounds = client.GetFieldBounds();
        var density = container.Resources?.DisplayMetrics?.Density ?? 1;
        container.GetLocationOnScreen(_location);
        return new Android.Graphics.Rect(_location[0] + (int)Math.Floor(bounds.Left * density), _location[1] + (int)Math.Floor(bounds.Top * density),
            _location[0] + (int)Math.Ceiling(bounds.Right * density), _location[1] + (int)Math.Ceiling(bounds.Bottom * density));
    }

    #endregion

    /// <summary>The container's <c>onCreateInputConnection</c>: a connection to the attached field, or <c>null</c> without one.</summary>
    internal IInputConnection? CreateInputConnection(EditorInfo? info)
    {
        if (Client is not { } client || info is null)
            return null;
        Configure(info, client);
        _connection = new SkUiInputConnection(container, client, _imm);
        return _connection;
    }

    /// <summary>The keyboard's layout, return key and corrections from the field's options (MAUI's mapping, as its Android entry).</summary>
    private static void Configure(EditorInfo info, ISkUiTextInputClient client)
    {
        var options = client.Options;
        var editor = client.Editor;
        var type = InputTypeOf(options);
        info.InputType = type;
        var action = options.IsMultiline ? ImeAction.None : options.ReturnType switch
        {
            ReturnType.Go => ImeAction.Go,
            ReturnType.Next => ImeAction.Next,
            ReturnType.Search => ImeAction.Search,
            ReturnType.Send => ImeAction.Send,
            _ => ImeAction.Done
        };
        info.ImeOptions = (ImeFlags)action | ImeFlags.NoFullscreen | ImeFlags.NoExtractUi;
        if (options.IsMultiline)
            info.ImeOptions |= ImeFlags.NoEnterAction;
        if (options.IsPassword && OperatingSystem.IsAndroidVersionAtLeast(26))
            info.ImeOptions |= ImeFlags.NoPersonalizedLearning;
        info.InitialSelStart = editor.SelectionStart;
        info.InitialSelEnd = editor.SelectionEnd;
        info.InitialCapsMode = TextUtils.GetCapsMode(new String(editor.Text), editor.SelectionStart, (CapitalizationMode)(int)(type & InputTypes.MaskFlags));
        // Never a password's text (Android drops it for password input types too; this does not rely on that).
        if (OperatingSystem.IsAndroidVersionAtLeast(30) && !options.IsPassword)
            info.SetInitialSurroundingText(new String(editor.Text));
    }

    /// <summary>The input type of a field: the keyboard's (<see cref="Configure"/>) and what autofill services read the field as.</summary>
    private static InputTypes InputTypeOf(SkUiTextInputOptions options)
    {
        InputTypes type;
        if (options.IsNumber)
            type = InputTypes.ClassNumber | (options.IsDecimalNumber ? InputTypes.NumberFlagDecimal : 0)
                   | (options.IsSignedNumber ? InputTypes.NumberFlagSigned : 0) | (options.IsPassword ? InputTypes.NumberVariationPassword : 0);
        else if (options.IsNumeric)
            type = InputTypes.ClassNumber | InputTypes.NumberFlagDecimal | InputTypes.NumberFlagSigned
                   | (options.IsPassword ? InputTypes.NumberVariationPassword : 0);
        else if (options.IsTelephone)
            type = InputTypes.ClassPhone;
        else
        {
            type = InputTypes.ClassText;
            if (options.IsEmail)
                type |= InputTypes.TextVariationEmailAddress;
            else if (options.IsUrl)
                type |= InputTypes.TextVariationUri;
            if (options.IsPassword)
                type |= InputTypes.TextVariationPassword;
            if (options.IsMultiline)
                type |= InputTypes.TextFlagMultiLine;
            var flags = options.Flags;
            if (options.TextTransform == TextTransform.Uppercase || flags.HasFlag(KeyboardFlags.CapitalizeCharacter))
                type |= InputTypes.TextFlagCapCharacters;
            else if (flags.HasFlag(KeyboardFlags.CapitalizeWord))
                type |= InputTypes.TextFlagCapWords;
            else if (flags.HasFlag(KeyboardFlags.CapitalizeSentence) || options.IsChat || ReferenceEquals(options.Keyboard, Keyboard.Text)
                     || (ReferenceEquals(options.Keyboard, Keyboard.Default) && !options.IsPassword))
                type |= InputTypes.TextFlagCapSentences;
            if (options.IsSpellCheckEnabled && !options.IsPlain && !options.IsPassword)
                type |= InputTypes.TextFlagAutoCorrect;
            if (!options.IsTextPredictionEnabled || options.IsPlain || options.IsPassword)
                type |= InputTypes.TextFlagNoSuggestions;
        }
        return type;
    }
}

/// <summary>The floating selection toolbar of a drawn field: the actions the field can run now, around the selection.</summary>
internal sealed class SkUiEditMenu(ISkUiTextInputClient client, Rect anchor, float density) : ActionMode.Callback2
{
    private static readonly (SkUiTextEditAction Action, int Id, int Title)[] Items =
    [
        (SkUiTextEditAction.Cut, Android.Resource.Id.Cut, Android.Resource.String.Cut),
        (SkUiTextEditAction.Copy, Android.Resource.Id.Copy, Android.Resource.String.Copy),
        (SkUiTextEditAction.Paste, Android.Resource.Id.Paste, Android.Resource.String.Paste),
        (SkUiTextEditAction.SelectAll, Android.Resource.Id.SelectAll, Android.Resource.String.SelectAll)
    ];

    /// <summary>The running action mode.</summary>
    internal ActionMode? Mode { get; set; }

    public override bool OnCreateActionMode(ActionMode? mode, IMenu? menu)
    {
        if (menu is null)
            return false;
        var order = 0;
        foreach (var (action, id, title) in Items)
            if (client.CanPerformEditAction(action))
                menu.Add(0, id, order++, title)?.SetShowAsAction(ShowAsAction.Always);
        return menu.HasVisibleItems;
    }

    public override bool OnPrepareActionMode(ActionMode? mode, IMenu? menu) => false;

    public override bool OnActionItemClicked(ActionMode? mode, IMenuItem? item)
    {
        foreach (var (action, id, _) in Items)
            if (item?.ItemId == id)
            {
                client.PerformEditAction(action);
                mode?.Finish();
                return true;
            }
        return false;
    }

    public override void OnDestroyActionMode(ActionMode? mode) => Mode = null;

    public override void OnGetContentRect(ActionMode? mode, View? view, Android.Graphics.Rect? outRect) =>
        outRect?.Set((int)Math.Floor(anchor.Left * density), (int)Math.Floor(anchor.Top * density),
            (int)Math.Ceiling(Math.Max(anchor.Right, anchor.Left + 1) * density), (int)Math.Ceiling(Math.Max(anchor.Bottom, anchor.Top + 1) * density));
}

/// <summary>
/// The input method's connection to a focused drawn field: Android's <see cref="BaseInputConnection"/> edits an <see cref="IEditable"/>
/// mirror (full editor), every call runs in a batch, and when the outermost batch ends the mirror's text, selection and composing
/// region reach the field as one edit (a diff). What the field does differently (a max length, a transform, a read-only field)
/// goes back to the mirror and the input method.
/// </summary>
internal sealed class SkUiInputConnection : BaseInputConnection
{
    private readonly View _view;
    private readonly ISkUiTextInputClient _client;
    private readonly InputMethodManager? _imm;
    private readonly SpannableStringBuilder _editable;
    private int _batch;
    private bool _pushing;
    private bool _connected = true;
    private bool _monitorCursor;
    private ExtractedTextRequest? _extractRequest;
    private CursorAnchorInfo.Builder? _anchor;
    // Reused by each cursor update (it runs on every selection change and moved frame while the input method monitors it).
    private readonly int[] _location = new int[2];
    private Android.Graphics.Matrix? _matrix;
    // What the last update reported: an update that would report the same is skipped.
    private string? _anchorText;
    private (int, int, int, int, int, int, float) _anchorState;
    private Rect _anchorCaret;

    public SkUiInputConnection(View view, ISkUiTextInputClient client, InputMethodManager? imm) : base(view, true)
    {
        _view = view;
        _client = client;
        _imm = imm;
        _editable = new SpannableStringBuilder(client.Editor.Text);
        CopySelection();
    }

    public override IEditable? Editable => _editable;

    /// <summary>The field let go: later calls of the input method do nothing.</summary>
    internal void Disconnect() => _connected = false;

    #region Batches and the mirror

    public override bool BeginBatchEdit()
    {
        _batch++;
        return _connected;
    }

    public override bool EndBatchEdit()
    {
        if (_batch > 0 && --_batch == 0)
            Sync();
        return _batch > 0;
    }

    /// <summary>Runs an operation of <see cref="BaseInputConnection"/> on the mirror as one batch.</summary>
    private bool Batch(Func<bool> operation)
    {
        if (!_connected)
            return false;
        BeginBatchEdit();
        try
        {
            return operation();
        }
        finally
        {
            EndBatchEdit();
        }
    }

    /// <summary>The mirror changed (a batch ended): the field takes the change as one edit, then the composing region and selection.</summary>
    private void Sync()
    {
        if (_pushing || !_connected)
            return;
        var editor = _client.Editor;
        var text = _editable.ToString() ?? string.Empty;
        var old = editor.Text;
        var selectionStart = Selection.GetSelectionStart(_editable);
        var selectionEnd = Selection.GetSelectionEnd(_editable);
        var composingStart = GetComposingSpanStart(_editable);
        var composingEnd = GetComposingSpanEnd(_editable);
        _client.BeginInputBatch();
        try
        {
            if (text != old)
            {
                var (start, end, inserted) = SkUiTextEditor.Diff(old, text);
                _client.ReplaceText(start, end, inserted);
            }
            if (composingStart >= 0 && composingEnd > composingStart)
                _client.SetComposingRegion(composingStart, composingEnd);
            else if (editor.HasComposition)
                _client.FinishComposing();
            if (selectionStart >= 0 && selectionEnd >= 0)
                _client.SetSelection(selectionStart, selectionEnd);
        }
        finally
        {
            _client.EndInputBatch();
        }
        // The field changed the edit (a max length, a transform, read-only): the mirror and the input method follow it.
        if (editor.Text != text)
            Push(textChanged: true);
        else if (editor.SelectionStart != Math.Min(selectionStart, selectionEnd) || editor.SelectionEnd != Math.Max(selectionStart, selectionEnd))
            Push(textChanged: false);
        else
            Report();
    }

    /// <summary>The field changed by keys or the app: the mirror follows, and the input method hears of it.</summary>
    internal void Push(bool textChanged)
    {
        if (!_connected)
            return;
        _pushing = true;
        try
        {
            var text = _client.Editor.Text;
            if (textChanged && _editable.ToString() != text)
            {
                RemoveComposingSpans(_editable);
                _editable.Replace(0, _editable.Length(), new String(text));
            }
            CopySelection();
        }
        finally
        {
            _pushing = false;
        }
        Report();
    }

    private void CopySelection()
    {
        var editor = _client.Editor;
        var length = _editable.Length();
        Selection.SetSelection(_editable, Math.Min(editor.Anchor, length), Math.Min(editor.Caret, length));
        if (editor.HasComposition && editor.CompositionEnd <= length)
        {
            _pushing = true;
            try
            {
                base.SetComposingRegion(editor.CompositionStart, editor.CompositionEnd);
            }
            finally
            {
                _pushing = false;
            }
        }
        else
        {
            RemoveComposingSpans(_editable);
        }
    }

    /// <summary>Tells the input method where the selection and composition are (and the extracted text and caret, when it asked).</summary>
    private void Report()
    {
        if (_imm is null || !_connected)
            return;
        var editor = _client.Editor;
        _imm.UpdateSelection(_view, editor.SelectionStart, editor.SelectionEnd,
            editor.HasComposition ? editor.CompositionStart : -1, editor.HasComposition ? editor.CompositionEnd : -1);
        if (_extractRequest is { } request)
            _imm.UpdateExtractedText(_view, request.Token, Extract());
        UpdateCursorAnchor(force: false);
    }

    #endregion

    #region Operations (each a batch on the mirror)

    public override bool CommitText(ICharSequence? text, int newCursorPosition) =>
        text is null ? false : Batch(() => base.CommitText(text, newCursorPosition));

    public override bool SetComposingText(ICharSequence? text, int newCursorPosition) =>
        text is null ? false : Batch(() => base.SetComposingText(text, newCursorPosition));

    public override bool SetComposingRegion(int start, int end) => Batch(() => base.SetComposingRegion(start, end));

    public override bool FinishComposingText() => Batch(base.FinishComposingText);

    public override bool DeleteSurroundingText(int beforeLength, int afterLength) => Batch(() => base.DeleteSurroundingText(beforeLength, afterLength));

    // Called by the system on API 24+ only.
    [SupportedOSPlatform("android24.0")]
    public override bool DeleteSurroundingTextInCodePoints(int beforeLength, int afterLength) =>
        Batch(() => base.DeleteSurroundingTextInCodePoints(beforeLength, afterLength));

    public override bool SetSelection(int start, int end) => Batch(() => base.SetSelection(start, end));

    public override bool CommitCorrection(CorrectionInfo? correctionInfo) => Batch(() => base.CommitCorrection(correctionInfo));

    /// <summary>The return key's action (Done, Go, Next, Search, Send): the field's return.</summary>
    public override bool PerformEditorAction(ImeAction actionCode)
    {
        if (!_connected)
            return false;
        _client.FinishComposing();
        _client.PerformReturn();
        return true;
    }

    public override bool PerformContextMenuAction(int id)
    {
        if (!_connected)
            return false;
        SkUiTextEditAction? action = id switch
        {
            Android.Resource.Id.SelectAll => SkUiTextEditAction.SelectAll,
            Android.Resource.Id.Cut => SkUiTextEditAction.Cut,
            Android.Resource.Id.Copy => SkUiTextEditAction.Copy,
            Android.Resource.Id.Paste => SkUiTextEditAction.Paste,
            _ => null
        };
        if (action is not { } value)
            return false;
        _client.PerformEditAction(value);
        return true;
    }

    public override ExtractedText? GetExtractedText(ExtractedTextRequest? request, GetTextFlags flags)
    {
        // Monitoring: the input method wants the text whenever it changes.
        _extractRequest = ((int)flags & 1) != 0 ? request : null;
        return request is null ? null : Extract();
    }

    private ExtractedText Extract()
    {
        var editor = _client.Editor;
        return new ExtractedText
        {
            StartOffset = 0,
            PartialStartOffset = -1,
            PartialEndOffset = -1,
            SelectionStart = editor.SelectionStart,
            SelectionEnd = editor.SelectionEnd,
            Text = new String(editor.Text),
            Flags = _client.Options.IsMultiline ? 0 : ExtractedTextFlags.SingleLine
        };
    }

    public override bool RequestCursorUpdates(int cursorUpdateMode)
    {
        if (!_connected)
            return false;
        _monitorCursor = (cursorUpdateMode & (int)CursorUpdate.Monitor) != 0;
        if ((cursorUpdateMode & (int)CursorUpdate.Immediate) != 0)
            UpdateCursorAnchor(force: true);
        return true;
    }

    [SupportedOSPlatform("android24.0")]
    public override void CloseConnection()
    {
        base.CloseConnection();
        _batch = 0;
    }

    #endregion

    /// <summary>The caret and composition in screen pixels for the input method's candidate window (when it monitors them).</summary>
    internal void UpdateCursorAnchor(bool force)
    {
        if (_imm is null || !_connected || (!force && !_monitorCursor))
            return;
        var editor = _client.Editor;
        var density = _view.Resources?.DisplayMetrics?.Density ?? 1f;
        _view.GetLocationOnScreen(_location);
        var caret = _client.GetCaretBounds(editor.Caret);
        // Exact: the same numbers are the same report (the text by reference: any edit makes a new string).
        var state = (editor.SelectionStart, editor.SelectionEnd, editor.CompositionStart, editor.CompositionEnd, _location[0], _location[1], density);
        if (!force && ReferenceEquals(_anchorText, editor.Text) && state == _anchorState && caret == _anchorCaret)
            return;
        (_anchorText, _anchorState, _anchorCaret) = (editor.Text, state, caret);
        var builder = _anchor ??= new CursorAnchorInfo.Builder();
        builder.Reset();
        builder.SetSelectionRange(editor.SelectionStart, editor.SelectionEnd);
        // Reset leaves no composing text.
        if (editor.HasComposition)
            builder.SetComposingText(editor.CompositionStart, new String(editor.Text.Substring(editor.CompositionStart, editor.CompositionEnd - editor.CompositionStart)));
        var matrix = _matrix ??= new Android.Graphics.Matrix();
        matrix.SetScale(density, density);
        matrix.PostTranslate(_location[0], _location[1]);
        builder.SetMatrix(matrix);
        builder.SetInsertionMarkerLocation((float)caret.X, (float)caret.Y, (float)caret.Bottom, (float)caret.Bottom,
            CursorAnchorFlags.HasVisibleRegion);
        _imm.UpdateCursorAnchorInfo(_view, builder.Build());
    }
}
/// <summary>Android: the window's <c>AutofillManager</c> commits or cancels its autofill session.</summary>
public static partial class SkUiAutofill
{
    static partial void FinishContext(bool save)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26)
            || Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.GetSystemService(Java.Lang.Class.FromType(typeof(AutofillManager))) is not AutofillManager autofill)
            return;
        if (save)
            autofill.Commit();
        else
            autofill.Cancel();
    }
}
#endif
