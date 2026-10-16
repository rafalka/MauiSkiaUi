#if IOS || MACCATALYST
using System.Runtime.InteropServices;
using CoreGraphics;
using Foundation;
using ObjCRuntime;
using UIKit;

namespace MauiSkiaUi;

/// <summary>
/// The text input of one surface on iOS and Mac Catalyst (<see cref="ISkUiTextInputProxy"/>): while a drawn field is focused, a
/// hidden <see cref="SkUiTextInputView"/> over the field's text is first responder. UIKit's keyboard, input methods (marked
/// text), autocorrect, dictation and autofill talk to it through <c>UITextInput</c>, which answers from the field's editor and
/// layout directly. The surface's container stays the drawn tree's key source: the view hands it the keys the field uses.
/// </summary>
internal sealed class SkUiAppleTextInput(SkUiOverlayContainer container) : ISkUiTextInputProxy
{
    private SkUiTextInputView? _view;
    private NSObject? _keyboardFrame;
    private NSObject? _keyboardHide;

    /// <summary>The keyboard moved or changed size: the surface's focus manager keeps the focused field above it.</summary>
    private void OnKeyboardFrame(object? sender, UIKeyboardEventArgs args)
    {
        if (container.Window is null || container.KeyboardFocus is not { } focus)
            return;
        var frame = container.ConvertRectFromView(args.FrameEnd, null);
        // Off the surface's bottom (hidden, or a hardware keyboard's bar below it): no keyboard over the surface.
        focus.SetKeyboardTop(frame.Top >= container.Bounds.Height || frame.Height <= 0 ? double.PositiveInfinity : frame.Top);
    }

    /// <summary>Typed characters arrive through <c>insertText</c>.</summary>
    public bool ReceivesCharacters => true;

    /// <summary>The view while a field is attached (tests, the container's responder checks).</summary>
    internal SkUiTextInputView? View => _view;

    public void Attach(ISkUiTextInputClient client)
    {
        _keyboardFrame ??= UIKeyboard.Notifications.ObserveWillChangeFrame(OnKeyboardFrame);
        _keyboardHide ??= UIKeyboard.Notifications.ObserveWillHide((_, _) => container.KeyboardFocus?.SetKeyboardTop(double.PositiveInfinity));
        var view = _view ??= new SkUiTextInputView(container);
        if (view.Superview is null)
            container.AddSubview(view);
        view.Client = client;
        view.UpdateFrame();
        client.InputAttached(this);
        if (view.IsFirstResponder)
        {
            // Focus moved from another field: the keyboard stays, with this field's options and text.
            view.ReloadInputViews();
            view.NotifyChanged(textChanged: true);
        }
        else
        {
            container.HandTextInput(view);
        }
    }

    public void Detach(ISkUiTextInputClient client)
    {
        client.InputAttached(null);
        if (_view is not { } view || !ReferenceEquals(view.Client, client))
            return;
        HideMagnifier(client);
        view.Client = null;
        // Another drawn node took focus: the container takes the keys back; focus left the surface: the keyboard goes.
        container.TakeTextInputBack(view, keepFocus: container.KeyboardFocus?.Focused is not null);
    }

    public void ShowKeyboard(ISkUiTextInputClient client)
    {
        if (_view is { } view && ReferenceEquals(view.Client, client) && !view.IsFirstResponder)
            container.HandTextInput(view);
    }

    public void HideKeyboard(ISkUiTextInputClient client)
    {
        // UIKit shows the keyboard while a text input is first responder: hiding it gives first responder back to the container.
        if (_view is { IsFirstResponder: true } view && ReferenceEquals(view.Client, client))
            container.TakeTextInputBack(view, keepFocus: true, hideOnly: true);
    }

    public void StateChanged(ISkUiTextInputClient client, bool textChanged)
    {
        if (_view is { } view && ReferenceEquals(view.Client, client))
            view.NotifyChanged(textChanged);
    }

    public void OptionsChanged(ISkUiTextInputClient client)
    {
        if (_view is { } view && ReferenceEquals(view.Client, client) && view.IsFirstResponder)
            view.ReloadInputViews();
    }

    public void GeometryChanged(ISkUiTextInputClient client)
    {
        if (_view is { } view && ReferenceEquals(view.Client, client))
            view.UpdateFrame();
    }

    public void ShowEditMenu(ISkUiTextInputClient client, Rect anchor, bool byTouch)
    {
        if (_view is { } view && ReferenceEquals(view.Client, client))
            view.ShowEditMenu(anchor);
    }

    public void HideEditMenu(ISkUiTextInputClient client)
    {
        if (_view is { } view && ReferenceEquals(view.Client, client))
            view.HideEditMenu();
    }

    private NSObject? _loupe;

    /// <summary>
    /// UIKit's loupe (iOS 17+, <c>UITextLoupeSession</c>) over <paramref name="caret"/>, as native fields show while the caret or a
    /// selection end moves; nothing before iOS 17.
    /// </summary>
    public void ShowMagnifier(ISkUiTextInputClient client, Rect caret)
    {
        if (_view is not { } view || !ReferenceEquals(view.Client, client)
            || !(OperatingSystem.IsIOSVersionAtLeast(17) || OperatingSystem.IsMacCatalystVersionAtLeast(17)))
            return;
        // Surface DIPs are the container's coordinates, the loupe's interaction view.
        var point = new CGPoint(caret.Center.X, caret.Center.Y);
        var rect = new CGRect(caret.X, caret.Y, Math.Max(2, caret.Width), caret.Height);
        var loupe = _loupe as UITextLoupeSession ?? UITextLoupeSession.BeginLoupeSession(point, null, container);
        _loupe = loupe;
        loupe?.MoveToPoint(point, rect, tracksCaret: true);
    }

    public void HideMagnifier(ISkUiTextInputClient client)
    {
        if (_loupe is not { } loupe)
            return;
        _loupe = null;
        if (OperatingSystem.IsIOSVersionAtLeast(17) || OperatingSystem.IsMacCatalystVersionAtLeast(17))
            ((UITextLoupeSession)loupe).Invalidate();
    }

    /// <summary>The surface went away.</summary>
    public void Dispose()
    {
        _keyboardFrame?.Dispose();
        _keyboardFrame = null;
        _keyboardHide?.Dispose();
        _keyboardHide = null;
        _view?.RemoveFromSuperview();
        _view = null;
    }
}

/// <summary>A position in a drawn field's text (a UTF-16 index).</summary>
internal sealed class SkUiTextPosition(int index) : UITextPosition, INSCopying
{
    public int Index { get; } = Math.Max(0, index);

    public NSObject Copy(NSZone? zone) => new SkUiTextPosition(Index);
}

/// <summary>A range of a drawn field's text.</summary>
internal sealed class SkUiTextRange : UITextRange, INSCopying
{
    public SkUiTextRange(int start, int end)
    {
        StartIndex = Math.Max(0, Math.Min(start, end));
        EndIndex = Math.Max(0, Math.Max(start, end));
    }

    public int StartIndex { get; }
    public int EndIndex { get; }

    public override bool IsEmpty => StartIndex == EndIndex;
    public override UITextPosition Start => new SkUiTextPosition(StartIndex);
    public override UITextPosition End => new SkUiTextPosition(EndIndex);

    public NSObject Copy(NSZone? zone) => new SkUiTextRange(StartIndex, EndIndex);
}

/// <summary>A box of a selection (UIKit's selection rects: dictation, the edit menu's anchor).</summary>
internal sealed class SkUiTextSelectionRect(CGRect rect, bool rightToLeft, bool containsStart, bool containsEnd) : UITextSelectionRect
{
    public override CGRect Rect => rect;
    public override NSWritingDirection WritingDirection => rightToLeft ? NSWritingDirection.RightToLeft : NSWritingDirection.LeftToRight;
    public override bool ContainsStart => containsStart;
    public override bool ContainsEnd => containsEnd;
    public override bool IsVertical => false;
}

/// <summary>
/// The hidden first responder of a focused drawn field: a transparent view over the field's text box implementing
/// <c>UITextInput</c> (and its traits) over the field's editor. It draws nothing and takes no touches; the drawn field draws
/// the text, caret, selection and marked text.
/// </summary>
[Adopts("UITextInputTraits")]
internal sealed class SkUiTextInputView : UIView, IUITextInput
{
    [DllImport("/usr/lib/libobjc.dylib")]
    private static extern void objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr argument);

    private static readonly IntPtr SelectionWillChange = Selector.GetHandle("selectionWillChange:");
    private static readonly IntPtr SelectionDidChange = Selector.GetHandle("selectionDidChange:");
    private static readonly IntPtr TextWillChange = Selector.GetHandle("textWillChange:");
    private static readonly IntPtr TextDidChange = Selector.GetHandle("textDidChange:");

    private readonly SkUiOverlayContainer _container;
    private readonly HashSet<UIKeyboardHidUsage> _handledKeys = [];
    private SkUiTextInputTokenizer? _tokenizer;

    public SkUiTextInputView(SkUiOverlayContainer container)
    {
        _container = container;
        BackgroundColor = UIColor.Clear;
        UserInteractionEnabled = false;
        // Transparent but not hidden: UIKit places the candidate bar, dictation and autofill from its frame.
        Alpha = 0.02f;
        IsAccessibilityElement = false;
        AccessibilityElementsHidden = true;
    }

    /// <summary>The attached field.</summary>
    internal ISkUiTextInputClient? Client { get; set; }

    private SkUiTextEditor? Editor => Client?.Editor;

    private int Length => Editor?.Text.Length ?? 0;

    /// <summary>Over the field's text box (container coordinates are surface DIPs).</summary>
    internal void UpdateFrame()
    {
        if (Client is not { } client)
            return;
        var bounds = client.GetTextBounds();
        Frame = new CGRect(bounds.X, bounds.Y, Math.Max(1, bounds.Width), Math.Max(1, bounds.Height));
    }

    private UIEditMenuInteraction? _menu;
    // UIKit holds the interaction's delegate weakly: without this reference it is collected and the menu ignores the target rect.
    private EditMenuDelegate? _menuDelegate;
    private CGRect _menuTarget;

    /// <summary>
    /// UIKit's edit menu at <paramref name="anchor"/> (surface DIPs): its items are this view's standard edit actions the field can
    /// run now (<see cref="CanPerform"/>), in the system's language.
    /// </summary>
    internal void ShowEditMenu(Rect anchor)
    {
        // UIKit places the menu by the target rect in the container's coordinates (surface DIPs), not this view's as documented:
        // in this view's own coordinates it landed off by this view's position, over the text (checked on iOS 26).
        _menuTarget = new CGRect(anchor.X, anchor.Y, anchor.Width, anchor.Height);
        if (OperatingSystem.IsIOSVersionAtLeast(16) || OperatingSystem.IsMacCatalystVersionAtLeast(16))
        {
            if (_menu is null)
            {
                _menuDelegate = new EditMenuDelegate(this);
                _menu = new UIEditMenuInteraction(_menuDelegate);
                AddInteraction(_menu);
            }
            var configuration = UIEditMenuConfiguration.Create(null, new CGPoint(_menuTarget.GetMidX(), _menuTarget.Top));
            // Above the text, as native fields show it (below when there is no room).
            configuration.PreferredArrowDirection = UIEditMenuArrowDirection.Down;
            _menu.PresentEditMenu(configuration);
            return;
        }
#pragma warning disable CA1422 // iOS 15: the menu controller
        UIMenuController.SharedMenuController.ShowMenu(this, ToView(anchor));
#pragma warning restore CA1422
    }

    internal void HideEditMenu()
    {
        if (OperatingSystem.IsIOSVersionAtLeast(16) || OperatingSystem.IsMacCatalystVersionAtLeast(16))
        {
            _menu?.DismissMenu();
            return;
        }
#pragma warning disable CA1422
        UIMenuController.SharedMenuController.HideMenu();
#pragma warning restore CA1422
    }

    /// <summary>Places the edit menu around the field's selection (rather than at the presenting point only).</summary>
    private sealed class EditMenuDelegate(SkUiTextInputView view) : UIEditMenuInteractionDelegate
    {
        public override CGRect GetTargetRect(UIEditMenuInteraction interaction, UIEditMenuConfiguration configuration) => view._menuTarget;
    }

    /// <summary>The field changed by keys or the app: UIKit's input (marked text, suggestions) follows.</summary>
    internal void NotifyChanged(bool textChanged)
    {
        if (WeakInputDelegate is not { } inputDelegate)
            return;
        var handle = inputDelegate.Handle.Handle;
        _notifying = true;
        try
        {
            if (textChanged)
            {
                objc_msgSend(handle, TextWillChange, Handle.Handle);
                objc_msgSend(handle, TextDidChange, Handle.Handle);
            }
            objc_msgSend(handle, SelectionWillChange, Handle.Handle);
            objc_msgSend(handle, SelectionDidChange, Handle.Handle);
        }
        finally
        {
            _notifying = false;
        }
    }

    // Set while UIKit hears of the field's own change: the keyboard answers selectionDidChange with a selection of its own (a
    // double-tapped word collapsed to its end), which must not override the field's.
    private bool _notifying;

    #region Responder

    public override bool CanBecomeFirstResponder => Client is not null;

    /// <summary>Set while the container hands first responder to this view (its own resignation is not a focus loss).</summary>
    internal bool Becoming { get; set; }

    public override bool ResignFirstResponder()
    {
        var resigned = base.ResignFirstResponder();
        // UIKit took first responder away (another native control, a dismissed keyboard on Mac): the drawn field loses focus,
        // unless the container is taking it back itself.
        if (resigned && Client is not null && !_container.IsTakingTextInputBack)
            _container.KeyboardFocus?.OnNativeFocusChanged(false);
        return resigned;
    }

    public override void PressesBegan(NSSet<UIPress> presses, UIPressesEvent evt)
    {
        // While an input method composes, every key is its own (candidate selection, commit with Return).
        if (Client is not { } client || client.Editor.HasComposition || _container.KeyboardFocus is not { } focus)
        {
            base.PressesBegan(presses, evt);
            return;
        }
        var unused = false;
        foreach (var press in presses)
        {
            if (press.Key is { } key && Map(key, out var character) is var mapped && mapped != SkUiKey.None
                && focus.KeyDown(mapped, SkUiAppleKeys.Modifiers(key), character: character))
                _handledKeys.Add(key.KeyCode);
            else
                unused = true;
        }
        if (unused)
            base.PressesBegan(presses, evt);
    }

    /// <summary>
    /// The keys the drawn field handles itself: navigation, deletion, Tab / Escape and shortcuts with Command or Control (as their
    /// letter). Typed characters and Return go to UIKit's input (<c>insertText</c>), where input methods see them.
    /// </summary>
    private static SkUiKey Map(UIKey key, out char character)
    {
        character = '\0';
        var flags = key.ModifierFlags;
        var command = (flags & (UIKeyModifierFlags.Command | UIKeyModifierFlags.Control)) != 0;
        var mapped = SkUiAppleKeys.Map(key);
        if (command && mapped == SkUiKey.None && key.CharactersIgnoringModifiers is { Length: 1 } letter)
        {
            character = letter[0];
            return SkUiKey.Character;
        }
        return mapped switch
        {
            SkUiKey.Space or SkUiKey.Enter when !command => SkUiKey.None,
            _ => mapped
        };
    }

    public override void PressesEnded(NSSet<UIPress> presses, UIPressesEvent evt)
    {
        if (!EndPresses(presses))
            base.PressesEnded(presses, evt);
    }

    public override void PressesCancelled(NSSet<UIPress> presses, UIPressesEvent evt)
    {
        if (!EndPresses(presses))
            base.PressesCancelled(presses, evt);
    }

    private bool EndPresses(NSSet<UIPress> presses)
    {
        var all = true;
        foreach (var press in presses)
            if (press.Key is not { } key || !_handledKeys.Remove(key.KeyCode))
                all = false;
        return all;
    }

    // The edit menu's actions (the system menu, the Mac's Edit menu): the field's.
    public override bool CanPerform(Selector action, NSObject? withSender)
    {
        if (Client is { } client && EditAction(action) is { } edit)
            return client.CanPerformEditAction(edit);
        return base.CanPerform(action, withSender);
    }

    private static SkUiTextEditAction? EditAction(Selector action) => action.Name switch
    {
        "cut:" => SkUiTextEditAction.Cut,
        "copy:" => SkUiTextEditAction.Copy,
        "paste:" => SkUiTextEditAction.Paste,
        "selectAll:" => SkUiTextEditAction.SelectAll,
        "delete:" => SkUiTextEditAction.Delete,
        _ => null
    };

    public override void Cut(NSObject? sender) => Client?.PerformEditAction(SkUiTextEditAction.Cut);
    public override void Copy(NSObject? sender) => Client?.PerformEditAction(SkUiTextEditAction.Copy);
    public override void Paste(NSObject? sender) => Client?.PerformEditAction(SkUiTextEditAction.Paste);
    public override void SelectAll(NSObject? sender) => Client?.PerformEditAction(SkUiTextEditAction.SelectAll);
    public override void Delete(NSObject? sender) => Client?.PerformEditAction(SkUiTextEditAction.Delete);

    #endregion

    #region UIKeyInput

    public bool HasText => Length > 0;

    public void InsertText(string text)
    {
        if (Client is not { } client)
            return;
        if (text == "\n" && !client.Options.IsMultiline)
        {
            Edit(client, string.Empty, static (client, _) => client.FinishComposing());
            client.PerformReturn();
            return;
        }
        Edit(client, text, static (client, text) => client.InsertText(text));
    }

    public void DeleteBackward()
    {
        if (Client is { } client)
            Edit(client, string.Empty, static (client, _) => client.DeleteBackward());
    }

    /// <summary>
    /// An edit from UIKit: when the field made it differently (a formatter, <c>MaxLength</c>, a transform, read-only), UIKit is told the
    /// text changed, as for the app's edits; it otherwise keeps the text it sent (suggestions and autocorrect on the wrong word).
    /// Not for marked text: telling UIKit mid-composition would end it. Committing it (<see cref="UnmarkText"/>) is an edit: the
    /// composition has ended, and the formatters may have rewritten what it committed.
    /// </summary>
    private void Edit(ISkUiTextInputClient client, string text, Action<ISkUiTextInputClient, string> edit)
    {
        var editor = client.Editor;
        editor.InputAdjusted = false;
        edit(client, text);
        if (!editor.InputAdjusted)
            return;
        editor.InputAdjusted = false;
        NotifyChanged(textChanged: true);
    }

    #endregion

    #region UITextInput: text and selection

    public string TextInRange(UITextRange range)
    {
        if (range is not SkUiTextRange r || Editor is not { } editor)
            return string.Empty;
        var start = Math.Min(r.StartIndex, editor.Text.Length);
        var end = Math.Min(r.EndIndex, editor.Text.Length);
        return editor.Text.Substring(start, end - start);
    }

    public void ReplaceText(UITextRange range, string text)
    {
        if (range is SkUiTextRange r && Client is { } client)
            Edit(client, text, (client, text) => client.ReplaceText(r.StartIndex, r.EndIndex, text));
    }

    public UITextRange? SelectedTextRange
    {
        get => Editor is { } editor ? new SkUiTextRange(editor.SelectionStart, editor.SelectionEnd) : null;
        set
        {
            if (value is SkUiTextRange r && !_notifying)
                Client?.SetSelection(r.StartIndex, r.EndIndex);
        }
    }

    // UIKit takes nil for no marked text (the binding's type says otherwise).
    public UITextRange MarkedTextRange =>
        Editor is { HasComposition: true } editor ? new SkUiTextRange(editor.CompositionStart, editor.CompositionEnd) : null!;

    public NSDictionary? MarkedTextStyle { get; set; }

    public void SetMarkedText(string? markedText, NSRange selectedRange)
    {
        if (Client is not { } client)
            return;
        var text = markedText ?? string.Empty;
        client.SetComposingText(text, (int)Math.Clamp(selectedRange.Location, 0, text.Length));
    }

    public void UnmarkText()
    {
        if (Client is { } client)
            Edit(client, string.Empty, static (client, _) => client.FinishComposing());
    }

    public UITextPosition BeginningOfDocument => new SkUiTextPosition(0);

    public UITextPosition EndOfDocument => new SkUiTextPosition(Length);

    public NSObject? WeakInputDelegate { get; set; }

    public NSObject WeakTokenizer => _tokenizer ??= new SkUiTextInputTokenizer(this);

    #endregion

    #region UITextInput: positions

    public UITextRange GetTextRange(UITextPosition fromPosition, UITextPosition toPosition) =>
        new SkUiTextRange(Index(fromPosition), Index(toPosition));

    // UIKit takes nil past the text's ends (the binding's type says otherwise).
    public UITextPosition GetPosition(UITextPosition fromPosition, nint offset)
    {
        var index = Index(fromPosition) + (int)offset;
        return index < 0 || index > Length ? null! : new SkUiTextPosition(index);
    }

    public UITextPosition GetPosition(UITextPosition fromPosition, UITextLayoutDirection inDirection, nint offset)
    {
        var index = Index(fromPosition);
        var moved = inDirection switch
        {
            UITextLayoutDirection.Left => index - (int)offset,
            UITextLayoutDirection.Right => index + (int)offset,
            // Lines: the field's visual lines (VoiceOver's Lines rotor; a single-line field's start and end).
            UITextLayoutDirection.Up => Client?.MoveByLines(index, -(int)offset) ?? 0,
            _ => Client?.MoveByLines(index, (int)offset) ?? Length
        };
        return new SkUiTextPosition(Math.Clamp(moved, 0, Length));
    }

    public NSComparisonResult ComparePosition(UITextPosition first, UITextPosition second)
    {
        var (a, b) = (Index(first), Index(second));
        return a < b ? NSComparisonResult.Ascending : a > b ? NSComparisonResult.Descending : NSComparisonResult.Same;
    }

    public nint GetOffsetFromPosition(UITextPosition fromPosition, UITextPosition toPosition) => Index(toPosition) - Index(fromPosition);

    public UITextPosition GetPositionWithinRange(UITextRange range, UITextLayoutDirection direction) =>
        direction is UITextLayoutDirection.Right or UITextLayoutDirection.Down ? range.End : range.Start;

    public UITextRange GetCharacterRange(UITextPosition byExtendingPosition, UITextLayoutDirection direction)
    {
        var index = Index(byExtendingPosition);
        return direction is UITextLayoutDirection.Left or UITextLayoutDirection.Up ? new SkUiTextRange(0, index) : new SkUiTextRange(index, Length);
    }

    public NSWritingDirection GetBaseWritingDirection(UITextPosition forPosition, UITextStorageDirection direction) =>
        Client?.IsRightToLeftAt(Index(forPosition)) == true ? NSWritingDirection.RightToLeft : NSWritingDirection.LeftToRight;

    public void SetBaseWritingDirectionforRange(NSWritingDirection writingDirection, UITextRange range)
    {
        // The field's direction follows its FlowDirection; UIKit's request is ignored.
    }

    private static int Index(UITextPosition position) => position is SkUiTextPosition p ? p.Index : 0;

    #endregion

    #region UITextInput: geometry

    /// <summary>Surface DIPs (the container's coordinates) in this view's coordinates.</summary>
    private CGRect ToView(Rect rect) => new(rect.X - Frame.X, rect.Y - Frame.Y, rect.Width, rect.Height);

    public CGRect GetFirstRectForRange(UITextRange range)
    {
        if (range is not SkUiTextRange r || Client is not { } client)
            return CGRect.Empty;
        var rects = client.GetRangeBounds(r.StartIndex, r.EndIndex);
        return rects.Count > 0 ? ToView(rects[0]) : GetCaretRectForPosition(r.Start);
    }

    public CGRect GetCaretRectForPosition(UITextPosition? position)
    {
        if (Client is not { } client)
            return CGRect.Empty;
        var caret = client.GetCaretBounds(position is null ? client.Editor.Caret : Index(position));
        return ToView(new Rect(caret.X, caret.Y, 2, caret.Height));
    }

    public UITextSelectionRect[] GetSelectionRects(UITextRange range)
    {
        if (range is not SkUiTextRange r || Client is not { } client)
            return [];
        var rects = client.GetRangeBounds(r.StartIndex, r.EndIndex);
        var result = new UITextSelectionRect[rects.Count];
        var rightToLeft = client.IsRightToLeftAt(r.StartIndex);
        for (var index = 0; index < rects.Count; index++)
            result[index] = new SkUiTextSelectionRect(ToView(rects[index]), rightToLeft, index == 0, index == rects.Count - 1);
        return result;
    }

    public UITextPosition GetClosestPositionToPoint(CGPoint point) =>
        new SkUiTextPosition(Client?.GetIndexAt(new Point(point.X + Frame.X, point.Y + Frame.Y)) ?? 0);

    public UITextPosition GetClosestPositionToPoint(CGPoint point, UITextRange withinRange)
    {
        var index = Index(GetClosestPositionToPoint(point));
        return withinRange is SkUiTextRange r ? new SkUiTextPosition(Math.Clamp(index, r.StartIndex, r.EndIndex)) : new SkUiTextPosition(index);
    }

    public UITextRange GetCharacterRangeAtPoint(CGPoint point)
    {
        var index = Index(GetClosestPositionToPoint(point));
        var text = Editor?.Text ?? string.Empty;
        var start = SkUiTextBoundaries.AlignToGrapheme(text, index);
        return new SkUiTextRange(start, SkUiTextBoundaries.NextGrapheme(text, start));
    }

    #endregion

    #region UITextInputTraits

    private SkUiTextInputOptions Options => Client?.Options ?? default;

    [Export("keyboardType")]
    public UIKeyboardType KeyboardType
    {
        get
        {
            var options = Options;
            // A numeric entry's: neither number pad has a minus key, so a value that can be negative gets numbers and punctuation.
            if (options.IsNumber)
                return options.IsSignedNumber ? UIKeyboardType.NumbersAndPunctuation
                    : options.IsDecimalNumber ? UIKeyboardType.DecimalPad : UIKeyboardType.NumberPad;
            if (options.IsNumeric) return UIKeyboardType.DecimalPad;
            if (options.IsTelephone) return UIKeyboardType.PhonePad;
            if (options.IsEmail) return UIKeyboardType.EmailAddress;
            if (options.IsUrl) return UIKeyboardType.Url;
            return options.IsPassword ? UIKeyboardType.AsciiCapable : UIKeyboardType.Default;
        }
    }

    [Export("returnKeyType")]
    public UIReturnKeyType ReturnKeyType => Options.IsMultiline ? UIReturnKeyType.Default : Options.ReturnType switch
    {
        ReturnType.Done => UIReturnKeyType.Done,
        ReturnType.Go => UIReturnKeyType.Go,
        ReturnType.Next => UIReturnKeyType.Next,
        ReturnType.Search => UIReturnKeyType.Search,
        ReturnType.Send => UIReturnKeyType.Send,
        _ => UIReturnKeyType.Default
    };

    [Export("autocapitalizationType")]
    public UITextAutocapitalizationType AutocapitalizationType
    {
        get
        {
            var options = Options;
            var flags = options.Flags;
            if (options.TextTransform == TextTransform.Uppercase || flags.HasFlag(KeyboardFlags.CapitalizeCharacter))
                return UITextAutocapitalizationType.AllCharacters;
            if (flags.HasFlag(KeyboardFlags.CapitalizeWord))
                return UITextAutocapitalizationType.Words;
            if (flags.HasFlag(KeyboardFlags.CapitalizeSentence) || options.IsChat || ReferenceEquals(options.Keyboard, Keyboard.Text))
                return UITextAutocapitalizationType.Sentences;
            if (options.IsPassword || options.IsEmail || options.IsUrl || options.IsPlain || options.IsNumeric || options.IsNumber || options.IsTelephone
                || options.TextTransform == TextTransform.Lowercase || flags.HasFlag(KeyboardFlags.CapitalizeNone))
                return UITextAutocapitalizationType.None;
            return UITextAutocapitalizationType.Sentences;
        }
    }

    [Export("autocorrectionType")]
    public UITextAutocorrectionType AutocorrectionType =>
        Options.IsTextPredictionEnabled && !Options.IsPlain ? UITextAutocorrectionType.Yes : UITextAutocorrectionType.No;

    [Export("spellCheckingType")]
    public UITextSpellCheckingType SpellCheckingType =>
        Options.IsSpellCheckEnabled && !Options.IsPlain ? UITextSpellCheckingType.Yes : UITextSpellCheckingType.No;

    [Export("isSecureTextEntry")]
    public bool IsSecureTextEntry => Options.IsPassword;

    [Export("textContentType")]
    // nil: no content type (the binding's type says otherwise).
    public NSString TextContentType => Options.AutofillHint switch
    {
        null => null!,
        SkUiAutofillHints.Username => UITextContentType.Username,
        SkUiAutofillHints.Password => UITextContentType.Password,
        SkUiAutofillHints.NewPassword => UITextContentType.NewPassword,
        SkUiAutofillHints.EmailAddress => UITextContentType.EmailAddress,
        SkUiAutofillHints.PhoneNumber => UITextContentType.TelephoneNumber,
        SkUiAutofillHints.OneTimeCode => UITextContentType.OneTimeCode,
        SkUiAutofillHints.Name => UITextContentType.Name,
        SkUiAutofillHints.PostalAddress => UITextContentType.FullStreetAddress,
        SkUiAutofillHints.PostalCode => UITextContentType.PostalCode,
        SkUiAutofillHints.CreditCardNumber => UITextContentType.CreditCardNumber,
        var other => new NSString(other)
    };

    [Export("smartQuotesType")]
    public UITextSmartQuotesType SmartQuotesType => Options.IsPlain || Options.IsPassword ? UITextSmartQuotesType.No : UITextSmartQuotesType.Default;

    [Export("smartDashesType")]
    public UITextSmartDashesType SmartDashesType => Options.IsPlain || Options.IsPassword ? UITextSmartDashesType.No : UITextSmartDashesType.Default;

    [Export("enablesReturnKeyAutomatically")]
    public bool EnablesReturnKeyAutomatically => false;

    #endregion
}
#endif
