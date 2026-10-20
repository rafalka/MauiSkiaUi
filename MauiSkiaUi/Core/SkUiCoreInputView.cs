using System.Windows.Input;
using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// What the Core text fields share (the twin of <see cref="SkUiInputView"/>, on the same engine; <see cref="SkUiCoreEntry"/>,
/// <see cref="SkUiCoreEditor"/>): drawn text, caret, selection and placeholder, the look's chrome, and the platform's keyboard and
/// input methods through the surface's text input proxy. Fluent <c>Set*</c> setters and <see cref="System.ComponentModel.INotifyPropertyChanged"/>.
/// </summary>
/// <remarks>Core nodes have no enabled state: use <see cref="IsReadOnly"/> to stop editing (or hide the node).</remarks>
public abstract class SkUiCoreInputView : SkUiCoreNode, ISkUiFontElement, ISkUiFieldChromeElement, ISkUiTextFieldOwner, ISkUiKeyHandler, ISkUiTextInputNode, ISkUiWheelHandler
{
    private string? _text;
    private int _cursorPosition;
    private int _selectionLength;
    private bool _minimumHeightExplicit;
    private SkUiCoreNode? _leadingView;
    private SkUiCoreNode? _trailingView;

    /// <summary>The field engine shared with <see cref="SkUiInputView"/>.</summary>
    internal SkUiTextFieldModel Model { get; }

    private protected SkUiCoreInputView(bool multiline)
    {
        Model = new SkUiTextFieldModel(this, multiline);
        Focused += (_, _) => Model.SetFocused(true);
        Unfocused += (_, _) => Model.SetFocused(false);
    }

    #region Properties

    /// <summary>The text. Typing, the input method, paste and undo set it and raise <see cref="TextChanged"/>.</summary>
    public string? Text { get => _text; set => SetText(value); }

    /// <summary>Shown while the text is empty.</summary>
    public string? Placeholder { get => Model.Placeholder; set => SetPlaceholder(value); }

    /// <summary>The placeholder's color; <c>null</c> (default): the color scheme's muted color.</summary>
    public Color? PlaceholderColor { get => Model.PlaceholderColor; set => SetPlaceholderColor(value); }

    /// <summary>The text's color; <c>null</c> (default): the color scheme's foreground.</summary>
    public Color? TextColor { get => Model.TextColor; set => SetTextColor(value); }

    /// <summary>DIPs added after each character.</summary>
    public double CharacterSpacing { get => Model.CharacterSpacing; set => SetCharacterSpacing(value); }

    /// <summary>Upper or lower case: typed and pasted text is transformed.</summary>
    public TextTransform TextTransform { get => Model.Editor.Transform; set => SetTextTransform(value); }

    /// <summary>Font family (see <see cref="SkUiInputView.FontFamily"/>).</summary>
    public string? FontFamily { get => Model.FontFamily; set => SetFontFamily(value); }

    /// <summary>Font size in DIPs (default 16).</summary>
    public double FontSize { get => Model.EffectiveFontSize; set => SetFontSize(value); }

    /// <summary>Bold and italic.</summary>
    public FontAttributes FontAttributes { get => Model.FontAttributes; set => SetFontAttributes(value); }

    /// <summary>Whether the text follows the system text size. Default <c>true</c>.</summary>
    public bool FontAutoScalingEnabled { get => Model.FontAutoScalingEnabled; set => SetFontAutoScalingEnabled(value); }

    /// <summary>Horizontal text alignment (default Start, which follows the text's direction).</summary>
    public TextAlignment HorizontalTextAlignment { get => Model.HorizontalTextAlignment; set => SetHorizontalTextAlignment(value); }

    /// <summary>Vertical text alignment in the field (default Center for an entry, Start for an editor).</summary>
    public TextAlignment VerticalTextAlignment { get => Model.VerticalTextAlignment; set => SetVerticalTextAlignment(value); }

    /// <summary>
    /// The most characters (UTF-16 code units) the text may have (default <see cref="int.MaxValue"/>), counted on the whole text as
    /// shown, after <see cref="InputFormatters"/> (see <see cref="SkUiInputView.MaxLength"/>).
    /// </summary>
    public int MaxLength { get => Model.Editor.MaxLength; set => SetMaxLength(value); }

    /// <summary>Whether the text cannot be changed by the user (it can still be selected and copied).</summary>
    public bool IsReadOnly { get => Model.IsReadOnly; set => SetIsReadOnly(value); }

    /// <summary>The soft keyboard's layout.</summary>
    public Keyboard Keyboard { get => Model.Keyboard; set => SetKeyboard(value); }

    /// <summary>Whether spelling is checked (default <c>true</c>; off for passwords).</summary>
    public bool IsSpellCheckEnabled { get => Model.IsSpellCheckEnabled; set => SetIsSpellCheckEnabled(value); }

    /// <summary>Whether the keyboard suggests words (default <c>true</c>; off for passwords).</summary>
    public bool IsTextPredictionEnabled { get => Model.IsTextPredictionEnabled; set => SetIsTextPredictionEnabled(value); }

    /// <summary>Where the caret is (the selection's start); follows the user.</summary>
    public int CursorPosition { get => _cursorPosition; set => SetCursorPosition(value); }

    /// <summary>How many characters are selected from <see cref="CursorPosition"/>; follows the user.</summary>
    public int SelectionLength { get => _selectionLength; set => SetSelectionLength(value); }

    /// <summary>The caret's color; <c>null</c> (default): the look's.</summary>
    public Color? CaretColor { get => Model.CaretColor; set => SetCaretColor(value); }

    /// <summary>The color behind selected text; <c>null</c> (default): the look's.</summary>
    public Color? SelectionColor { get => Model.SelectionColor; set => SetSelectionColor(value); }

    /// <summary>What the field holds for password managers and autofill (<see cref="SkUiAutofillHints"/>, or a platform's own hint).</summary>
    /// <remarks>One hint: a value with white space throws (white space is reserved for a list of hints later).</remarks>
    public string? AutofillHint { get => Model.AutofillHint; set => SetAutofillHint(value); }

    /// <summary>
    /// Formatters that rewrite or refuse the user's edits, in order (<see cref="SkUiTextInputFormatter"/>; see
    /// <see cref="SkUiInputView.InputFormatters"/>).
    /// </summary>
    public IList<SkUiTextInputFormatter> InputFormatters => field ??= CreateInputFormatters();

    /// <summary>Whether text the app sets goes through <see cref="InputFormatters"/> too (default <c>false</c>; see <see cref="SkUiInputView.FormatsAssignedText"/>).</summary>
    public bool FormatsAssignedText { get => Model.FormatsAssignedText; set => SetFormatsAssignedText(value); }

    /// <summary>A node inside the field at its start side, before the text (see <see cref="SkUiInputView.LeadingView"/>).</summary>
    public SkUiCoreNode? LeadingView { get => _leadingView; set => SetLeadingView(value); }

    /// <summary>A node inside the field at its end side, after the text (see <see cref="SkUiInputView.TrailingView"/>).</summary>
    public SkUiCoreNode? TrailingView { get => _trailingView; set => SetTrailingView(value); }

    /// <summary>Whether the field's input is invalid: error chrome and message (see <see cref="SkUiInputView.HasError"/>).</summary>
    public bool HasError { get => Model.HasError; set => SetHasError(value); }

    /// <summary>The message under the field while <see cref="HasError"/> is set, in place of <see cref="HelperText"/> (see <see cref="SkUiInputView.ErrorText"/>).</summary>
    public string? ErrorText { get => Model.ErrorText; set => SetErrorText(value); }

    /// <summary>A message under the field (see <see cref="SkUiInputView.HelperText"/>).</summary>
    public string? HelperText { get => Model.HelperText; set => SetHelperText(value); }

    /// <summary>The error chrome's and message's color; <c>null</c> (default): the color scheme's (see <see cref="SkUiInputView.ErrorColor"/>).</summary>
    public Color? ErrorColor { get => Model.ErrorColor; set => SetErrorColor(value); }

    /// <summary>How the look draws this field's chrome; <see cref="SkUiTextFieldStyle.Default"/> (default): the look's (see <see cref="SkUiInputView.FieldStyle"/>).</summary>
    public SkUiTextFieldStyle FieldStyle { get => Model.FieldStyle; set => SetFieldStyle(value); }

    /// <summary>The space between the field's edges and its text in DIPs; <c>null</c> (default): the look's (see <see cref="SkUiInputView.TextPadding"/>).</summary>
    public Thickness? TextPadding { get => Model.Padding; set => SetTextPadding(value); }

    /// <summary>The field's fill; <c>null</c> (default): its style's (see <see cref="SkUiInputView.FillColor"/>).</summary>
    public Color? FillColor { get => Model.Chrome.FillColor; set => SetFillColor(value); }

    /// <summary>The idle outline's or underline's color; <c>null</c> (default): the color scheme's muted color (see <see cref="SkUiInputView.BorderColor"/>).</summary>
    public Color? BorderColor { get => Model.Chrome.BorderColor; set => SetBorderColor(value); }

    /// <summary>The idle outline's or underline's width in DIPs; <c>null</c> (default): the style's (see <see cref="SkUiInputView.BorderWidth"/>).</summary>
    public double? BorderWidth { get => Model.Chrome.BorderWidth; set => SetBorderWidth(value); }

    /// <summary>The chrome's corner radii in DIPs; <c>null</c> (default): the style's (see <see cref="SkUiInputView.CornerRadii"/>).</summary>
    public CornerRadius? CornerRadii { get => Model.Chrome.CornerRadii; set => SetCornerRadii(value); }

    /// <summary>Raised when the text changes (by the user or the app), with the old and new text.</summary>
    public event EventHandler<TextChangedEventArgs>? TextChanged;

    #endregion

    #region Setters

    /// <summary>Sets <see cref="Text"/>.</summary>
    public SkUiCoreInputView SetText(string? value)
    {
        var old = _text;
        if (!SetProperty(ref _text, value, nameof(Text)))
            return this;
        var shown = Model.SetText(value);
        TextChanged?.Invoke(this, new TextChangedEventArgs(old, _text));
        // Cut or transformed: Text follows what the field shows.
        if (shown is not null)
            return SetText(shown);
        if (!_textEditing)
            OnTextAssigned();
        return this;
    }

    // The user's edit is setting Text (not the app).
    private bool _textEditing;

    /// <summary>The app set <see cref="Text"/> (not the user's edit), after the field took it.</summary>
    private protected virtual void OnTextAssigned()
    {
    }

    /// <summary>The user edited the text (typing, the input method, paste, undo), after <see cref="Text"/> followed.</summary>
    private protected virtual void OnTextEdited()
    {
    }

    /// <summary>Sets <see cref="Placeholder"/>.</summary>
    public SkUiCoreInputView SetPlaceholder(string? value) => Update(ref Model.Placeholder, value, nameof(Placeholder), measure: true);

    /// <summary>Sets <see cref="PlaceholderColor"/>.</summary>
    public SkUiCoreInputView SetPlaceholderColor(Color? value) => Update(ref Model.PlaceholderColor, value, nameof(PlaceholderColor));

    /// <summary>Sets <see cref="TextColor"/>.</summary>
    public SkUiCoreInputView SetTextColor(Color? value) => Update(ref Model.TextColor, value, nameof(TextColor));

    /// <summary>Sets <see cref="CharacterSpacing"/>.</summary>
    public SkUiCoreInputView SetCharacterSpacing(double value)
    {
        SkUiValidate.ThrowIfNotFinite(value, nameof(value));
        return Update(ref Model.CharacterSpacing, value, nameof(CharacterSpacing), measure: true);
    }

    /// <summary>Sets <see cref="TextTransform"/>.</summary>
    public SkUiCoreInputView SetTextTransform(TextTransform value)
    {
        ThrowIfUndefined(value);
        if (Model.Editor.Transform == value)
            return this;
        var shown = Model.SetTransform(value);
        OnPropertyChanged(nameof(TextTransform));
        return shown is null ? this : SetText(shown);
    }

    /// <summary>Sets <see cref="FontFamily"/>.</summary>
    public SkUiCoreInputView SetFontFamily(string? value) => Update(ref Model.FontFamily, value, nameof(FontFamily), measure: true);

    /// <summary>Sets <see cref="FontSize"/>.</summary>
    public SkUiCoreInputView SetFontSize(double value)
    {
        SkUiValidate.ThrowIfNotFinitePositive(value, nameof(value));
        return Update(ref Model.FontSize, value, nameof(FontSize), measure: true);
    }

    /// <summary>Sets <see cref="FontAttributes"/>.</summary>
    public SkUiCoreInputView SetFontAttributes(FontAttributes value) => Update(ref Model.FontAttributes, value, nameof(FontAttributes), measure: true);

    /// <summary>Sets <see cref="FontAutoScalingEnabled"/>.</summary>
    public SkUiCoreInputView SetFontAutoScalingEnabled(bool value) => Update(ref Model.FontAutoScalingEnabled, value, nameof(FontAutoScalingEnabled), measure: true);

    /// <summary>Sets <see cref="HorizontalTextAlignment"/>.</summary>
    public SkUiCoreInputView SetHorizontalTextAlignment(TextAlignment value)
    {
        ThrowIfUndefined(value);
        return Update(ref Model.HorizontalTextAlignment, value, nameof(HorizontalTextAlignment));
    }

    /// <summary>Sets <see cref="VerticalTextAlignment"/>.</summary>
    public SkUiCoreInputView SetVerticalTextAlignment(TextAlignment value)
    {
        ThrowIfUndefined(value);
        return Update(ref Model.VerticalTextAlignment, value, nameof(VerticalTextAlignment), measure: true);
    }

    /// <summary>Sets <see cref="MaxLength"/>: longer text is cut (a numeric entry's value is not).</summary>
    public SkUiCoreInputView SetMaxLength(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        if (Model.Editor.MaxLength == value)
            return this;
        var shown = Model.SetMaxLength(value);
        OnPropertyChanged(nameof(MaxLength));
        return shown is null ? this : SetText(shown);
    }

    /// <summary>Sets <see cref="IsReadOnly"/>.</summary>
    public SkUiCoreInputView SetIsReadOnly(bool value)
    {
        if (Model.IsReadOnly == value)
            return this;
        ApplyReadOnly(value);
        OnPropertyChanged(nameof(IsReadOnly));
        return this;
    }

    /// <summary><see cref="IsReadOnly"/> changed: the field model follows (a numeric entry adds its <c>IsEditable</c>).</summary>
    private protected virtual void ApplyReadOnly(bool value) => Model.SetReadOnly(value);

    /// <summary>Sets <see cref="Keyboard"/>.</summary>
    public SkUiCoreInputView SetKeyboard(Keyboard value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return UpdateOptions(ref Model.Keyboard, value, nameof(Keyboard));
    }

    /// <summary>Sets <see cref="IsSpellCheckEnabled"/>.</summary>
    public SkUiCoreInputView SetIsSpellCheckEnabled(bool value) => UpdateOptions(ref Model.IsSpellCheckEnabled, value, nameof(IsSpellCheckEnabled));

    /// <summary>Sets <see cref="IsTextPredictionEnabled"/>.</summary>
    public SkUiCoreInputView SetIsTextPredictionEnabled(bool value) => UpdateOptions(ref Model.IsTextPredictionEnabled, value, nameof(IsTextPredictionEnabled));

    /// <summary>Sets <see cref="CursorPosition"/>.</summary>
    public SkUiCoreInputView SetCursorPosition(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        if (SetProperty(ref _cursorPosition, value, nameof(CursorPosition)))
            Model.SetSelection(_cursorPosition, _selectionLength);
        return this;
    }

    /// <summary>Sets <see cref="SelectionLength"/>.</summary>
    public SkUiCoreInputView SetSelectionLength(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        if (SetProperty(ref _selectionLength, value, nameof(SelectionLength)))
            Model.SetSelection(_cursorPosition, _selectionLength);
        return this;
    }

    /// <summary>Sets <see cref="CaretColor"/>.</summary>
    public SkUiCoreInputView SetCaretColor(Color? value) => Update(ref Model.CaretColor, value, nameof(CaretColor));

    /// <summary>Sets <see cref="SelectionColor"/>.</summary>
    public SkUiCoreInputView SetSelectionColor(Color? value) => Update(ref Model.SelectionColor, value, nameof(SelectionColor));

    /// <summary>Sets <see cref="AutofillHint"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="value"/> has white space.</exception>
    public SkUiCoreInputView SetAutofillHint(string? value)
    {
        SkUiValidate.ThrowIfNotAutofillHint(value, nameof(value));
        return UpdateOptions(ref Model.AutofillHint, value, nameof(AutofillHint));
    }

    /// <summary>Replaces <see cref="InputFormatters"/> with <paramref name="formatters"/> (in order).</summary>
    public SkUiCoreInputView SetInputFormatters(params IEnumerable<SkUiTextInputFormatter> formatters)
    {
        ArgumentNullException.ThrowIfNull(formatters);
        var list = formatters.ToList();
        if (list.Contains(null!))
            throw new ArgumentException("A formatter is null.", nameof(formatters));
        var target = InputFormatters;
        target.Clear();
        foreach (var formatter in list)
            target.Add(formatter);
        return this;
    }

    /// <summary>Sets <see cref="FormatsAssignedText"/>.</summary>
    public SkUiCoreInputView SetFormatsAssignedText(bool value)
    {
        if (!SetProperty(ref Model.FormatsAssignedText, value, nameof(FormatsAssignedText)))
            return this;
        if (value)
            OnFormattersChanged();
        return this;
    }

    /// <summary>Sets <see cref="LeadingView"/>.</summary>
    public SkUiCoreInputView SetLeadingView(SkUiCoreNode? value) => SetAdornment(ref _leadingView, value, _trailingView, nameof(LeadingView));

    /// <summary>Sets <see cref="TrailingView"/>.</summary>
    public SkUiCoreInputView SetTrailingView(SkUiCoreNode? value) => SetAdornment(ref _trailingView, value, _leadingView, nameof(TrailingView));

    /// <summary>Sets <see cref="HasError"/>.</summary>
    public SkUiCoreInputView SetHasError(bool value) => UpdateMessage(ref Model.HasError, value, nameof(HasError));

    /// <summary>Sets <see cref="ErrorText"/>.</summary>
    public SkUiCoreInputView SetErrorText(string? value) => UpdateMessage(ref Model.ErrorText, value, nameof(ErrorText));

    /// <summary>Sets <see cref="HelperText"/>.</summary>
    public SkUiCoreInputView SetHelperText(string? value) => UpdateMessage(ref Model.HelperText, value, nameof(HelperText));

    /// <summary>Sets <see cref="ErrorColor"/>.</summary>
    public SkUiCoreInputView SetErrorColor(Color? value) => Update(ref Model.ErrorColor, value, nameof(ErrorColor));

    /// <summary>Sets <see cref="FieldStyle"/>.</summary>
    public SkUiCoreInputView SetFieldStyle(SkUiTextFieldStyle value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value), value, null);
        return Update(ref Model.FieldStyle, value, nameof(FieldStyle));
    }

    /// <summary>Sets <see cref="TextPadding"/> (<c>null</c>: the look's).</summary>
    public SkUiCoreInputView SetTextPadding(Thickness? value)
    {
        SkUiValidate.ThrowIfNotThickness(value, nameof(value));
        return Update(ref Model.Padding, value, nameof(TextPadding), measure: true);
    }

    /// <summary>Sets <see cref="FillColor"/> (<c>null</c>: the style's).</summary>
    public SkUiCoreInputView SetFillColor(Color? value) => Update(ref Model.Chrome.FillColor, value, nameof(FillColor));

    /// <summary>Sets <see cref="BorderColor"/> (<c>null</c>: the color scheme's).</summary>
    public SkUiCoreInputView SetBorderColor(Color? value) => Update(ref Model.Chrome.BorderColor, value, nameof(BorderColor));

    /// <summary>Sets <see cref="BorderWidth"/> (<c>null</c>: the style's).</summary>
    public SkUiCoreInputView SetBorderWidth(double? value)
    {
        if (value is { } width)
            SkUiValidate.ThrowIfNegativeOrNotFinite(width, nameof(value));
        return Update(ref Model.Chrome.BorderWidth, value, nameof(BorderWidth));
    }

    /// <summary>Sets <see cref="CornerRadii"/> (<c>null</c>: the style's).</summary>
    public SkUiCoreInputView SetCornerRadii(CornerRadius? value)
    {
        if (value is { } radii)
            SkUiCornerRadii.Validate(radii, nameof(value));
        return Update(ref Model.Chrome.CornerRadii, value, nameof(CornerRadii));
    }

    /// <summary>Sets all four <see cref="CornerRadii"/> to <paramref name="value"/> DIPs.</summary>
    public SkUiCoreInputView SetCornerRadius(double value)
    {
        SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value));
        return SetCornerRadii(new CornerRadius(value));
    }

    private SkUiCoreInputView UpdateMessage<T>(ref T field, T value, string name)
    {
        if (SetProperty(ref field, value, name))
            Model.MessageChanged();
        return this;
    }

    private SkUiCoreInputView SetAdornment(ref SkUiCoreNode? slot, SkUiCoreNode? value, SkUiCoreNode? other, string name)
    {
        if (ReferenceEquals(slot, value))
            return this;
        if (value is not null)
        {
            if (ReferenceEquals(value, other))
                throw new InvalidOperationException("The leading and trailing views must be different nodes.");
            if (value.Parent is not null)
                throw new InvalidOperationException("Child already has a parent.");
            if (WouldCreateParentCycle(value))
                throw new InvalidOperationException("A view cannot create a parent cycle.");
        }
        // Visual tree index: the leading view comes first.
        var index = name == nameof(TrailingView) && other is not null ? 1 : 0;
        var previous = slot;
        if (previous is not null)
        {
            previous.AttachTo(null);
            SkUiDiagnostics.NotifyChildRemoved(this, previous, index);
        }
        slot = value;
        if (value is not null)
        {
            value.AttachTo(this);
            SkUiDiagnostics.NotifyChildAdded(this, value, index);
        }
        OnPropertyChanged(name);
        InvalidateRender(SkUiRenderDirty.Children);
        Model.TextChanged();
        return this;
    }

    private System.Collections.ObjectModel.ObservableCollection<SkUiTextInputFormatter> CreateInputFormatters()
    {
        var formatters = new System.Collections.ObjectModel.ObservableCollection<SkUiTextInputFormatter>();
        formatters.CollectionChanged += (_, _) => OnFormattersChanged();
        Model.Editor.Formatters = formatters;
        return formatters;
    }

    private void OnFormattersChanged()
    {
        if (Model.FormatsChanged() is { } shown)
            SetText(shown);
    }

    /// <inheritdoc />
    public override SkUiCoreNode SetMinimumHeight(double value)
    {
        _minimumHeightExplicit = true;
        return base.SetMinimumHeight(value);
    }

    private protected SkUiCoreInputView Update<T>(ref T field, T value, string name, bool measure = false)
    {
        if (!SetProperty(ref field, value, name))
            return this;
        if (measure)
            Model.TextChanged();
        else
            Model.PaintChanged();
        return this;
    }

    private protected SkUiCoreInputView UpdateOptions<T>(ref T field, T value, string name)
    {
        if (SetProperty(ref field, value, name))
            Model.OptionsChanged();
        return this;
    }

    private protected static void ThrowIfUndefined<T>(T value) where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value), value, null);
    }

    #endregion

    #region Node

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        Model.HasExplicitMinimumHeight = _minimumHeightExplicit;
        Model.LeadingSize = _leadingView is { IsVisible: true } leading ? leading.Measure(widthConstraint, heightConstraint) : Size.Zero;
        Model.TrailingSize = _trailingView is { IsVisible: true } trailing ? trailing.Measure(widthConstraint, heightConstraint) : Size.Zero;
        return Model.Measure(widthConstraint, heightConstraint);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        if (_leadingView is null && _trailingView is null)
            return;
        var (leading, trailing) = Model.AdornmentFrames(size.Width, size.Height);
        _leadingView?.Arrange(leading);
        _trailingView?.Arrange(trailing);
    }

    /// <inheritdoc />
    internal override IReadOnlyList<IVisualTreeElement> VisualChildren => (_leadingView, _trailingView) switch
    {
        (null, null) => [],
        ({ } leading, null) => [leading],
        (null, { } trailing) => [trailing],
        ({ } leading, { } trailing) => [leading, trailing]
    };

    /// <inheritdoc />
    internal override void AddRenderChildren(List<ISkUiRenderable> children)
    {
        if (_leadingView is not null)
            children.Add(_leadingView);
        if (_trailingView is not null)
            children.Add(_trailingView);
    }

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas) => Model.Draw(canvas, (float)Frame.Width, (float)Frame.Height);

    /// <inheritdoc />
    internal override void OnGetRenderProps(ref SkUiRenderProps props)
    {
        base.OnGetRenderProps(ref props);
        Model.GetRenderProps(ref props);
    }

    /// <inheritdoc />
    private protected override void ForgetOwnDrawable() => Model.ForgetCaret();

    /// <inheritdoc />
    internal override void CollectGestureRecognizers(List<SkUiGestureRecognizer> recognizers)
    {
        base.CollectGestureRecognizers(recognizers);
        if (!IsEnabled)
            return;
        recognizers.Add(Model.Gestures);
        if (Model.Steps is { HasButtons: true } steps)
            recognizers.Add(steps.Gestures);
    }

    /// <inheritdoc />
    protected override void OnIsEnabledChanged()
    {
        base.OnIsEnabledChanged();
        Model.IsEnabled = IsEnabled;
        if (!IsEnabled)
            Model.Steps?.Release();
        Model.PaintChanged();
        Model.SemanticsChanged();
    }

    /// <inheritdoc />
    internal override void OnPointerOverChanged(bool isOver)
    {
        Model.IsPointerOver = isOver;
        Model.PaintChanged();
    }

    /// <inheritdoc />
    internal override void OnEffectiveFlowDirectionChanged()
    {
        base.OnEffectiveFlowDirectionChanged();
        Model.TextChanged();
    }

    /// <inheritdoc />
    internal override void ReleaseDrawingResources()
    {
        base.ReleaseDrawingResources();
        Model.ReleaseDrawingResources();
    }

    /// <inheritdoc />
    protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
    {
        base.OnPopulateSemantics(info);
        Model.PopulateSemantics(info);
    }

    /// <inheritdoc />
    protected override bool OnSemanticsAction(SkUiSemanticsActions action) => Model.PerformSemanticsAction(action) || base.OnSemanticsAction(action);

    /// <inheritdoc />
    internal override bool OnSemanticsPartAction(int part, SkUiSemanticsActions action) => Model.PerformSemanticsPartAction(part, action);

    /// <inheritdoc />
    internal override bool TakesKeyboardFocus => true;

    /// <inheritdoc />
    internal override bool ShowsFocusRing => false;

    bool ISkUiKeyHandler.OnKeyDown(in SkUiKeyEvent key) => Model.OnKeyDown(key);

    ISkUiTextInputClient ISkUiTextInputNode.TextInputClient => Model;

    bool ISkUiTextInputNode.WantsTextInput => true;

    Point ISkUiWheelHandler.OnWheel(double deltaX, double deltaY) => Model.Wheel(deltaX, deltaY);

    bool ISkUiTextInputNode.CanTakeTextFocus => IsVisible && IsEnabled;

    bool ISkUiTextInputNode.HitsTextHandle(Point local) => Model.HandleAt(local) != 0;

    bool ISkUiTextInputNode.HitsOverflowMessage(Point local) => Model.HitsOverflowMessage(local);

    #endregion

    #region Owner

    ISkUiRenderable ISkUiTextFieldOwner.Node => this;

    Size ISkUiTextFieldOwner.FieldSize => Frame.Size;

    bool ISkUiTextFieldOwner.IsRightToLeft => IsRightToLeft;

    SkUiTextDirection ISkUiTextFieldOwner.TextDirection => InheritedDirection();

    bool ISkUiTextFieldOwner.IsFocused => IsFocused;

    void ISkUiTextFieldOwner.TextEdited(string text)
    {
        _textEditing = true;
        try
        {
            SetText(text);
        }
        finally
        {
            _textEditing = false;
        }
        OnTextEdited();
    }

    void ISkUiTextFieldOwner.SelectionEdited(int start, int length)
    {
        SetProperty(ref _cursorPosition, start, nameof(CursorPosition));
        SetProperty(ref _selectionLength, length, nameof(SelectionLength));
    }

    void ISkUiTextFieldOwner.Completed() => OnReturnPressed();

    /// <summary>The return key in a single-line field (an entry's <c>Completed</c>).</summary>
    private protected virtual void OnReturnPressed()
    {
    }

    bool ISkUiTextFieldOwner.RequestFocus() => Focus();

    void ISkUiTextFieldOwner.ReleaseFocus() => Unfocus();

    void ISkUiTextFieldOwner.InvalidateMeasure() => InvalidateMeasure();

    void ISkUiTextFieldOwner.InvalidatePaint() => InvalidatePaint();

    void ISkUiTextFieldOwner.InvalidateSemantics() => InvalidateSemantics();

    void ISkUiTextFieldOwner.InvalidateCaret() => InvalidateRender(SkUiRenderDirty.Props);

    bool ISkUiTextFieldOwner.AdornmentTakesPointer(Point local) =>
        SkUiTextFieldAdornments.TakesPointer(_leadingView, local) || SkUiTextFieldAdornments.TakesPointer(_trailingView, local);

    #endregion
}
