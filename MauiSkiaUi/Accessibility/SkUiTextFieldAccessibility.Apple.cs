#if IOS || MACCATALYST
using CoreGraphics;
using Foundation;
using UIKit;

namespace MauiSkiaUi;

/// <summary>
/// A drawn editable text field as VoiceOver sees it: an element that is a <c>UITextInput</c>, as UIKit's text fields are, so
/// VoiceOver's rotor moves through its characters, words and lines, the caret follows what it reads, and it reads the selection.
/// While the field is focused the element forwards to the surface's hidden text input view (<see cref="SkUiTextInputView"/>, the
/// first responder: VoiceOver and the keyboard see one text and one selection); otherwise to the bridge's idle view, the same
/// implementation over the field without the keyboard (Flutter's text field semantics object works the same way). Selection
/// changes go through the semantics owner, as TalkBack's do, so the keyboard hears of them. Passwords are not elements of this
/// kind: their text is not exposed (VoiceOver reads the bullets of the value).
/// </summary>
internal sealed class SkUiTextFieldAccessibilityElement(NSObject container, SkUiAccessibilityBridge bridge, int id)
    : SkUiAccessibilityElement(container, bridge, id), IUITextInput
{
    /// <summary>An undocumented trait VoiceOver reads as "empty line" on a single tap (Flutter removes it from its text fields too).</summary>
    private const ulong EmptyLineTrait = 0x800000000000;

    private WeakReference<ISkUiTextInputClient>? _field;

    /// <summary>The field's input client (set by the bridge from the semantics node).</summary>
    internal ISkUiTextInputClient? Field
    {
        get => _field is { } reference && reference.TryGetTarget(out var client) ? client : null;
        set => _field = value is null ? null : new WeakReference<ISkUiTextInputClient>(value);
    }

    /// <summary>The view answering for the field now (the focused one's, else the idle one with the field set), or <c>null</c>.</summary>
    private SkUiTextInputView? Surrogate => Field is { } client && Bridge is { } bridge ? bridge.TextInputFor(client) : null;

    /// <summary>The focused field's text input view; <c>null</c> while the field is not focused (it is not edited then).</summary>
    private SkUiTextInputView? Editing => Field is { } client ? Bridge?.FocusedTextInput(client) : null;

    /// <summary>The text field's traits; while it is edited also the first responder's (VoiceOver reads "is editing").</summary>
    public override ulong AccessibilityTraits
    {
        get
        {
            var traits = base.AccessibilityTraits;
            if (Editing is { } view)
                traits |= (ulong)view.AccessibilityTraits;
            return traits & ~EmptyLineTrait;
        }
        set => base.AccessibilityTraits = value;
    }

    /// <summary>VoiceOver moved on: the idle view lets the field go.</summary>
    public override void AccessibilityElementDidLoseFocus() => Bridge?.ReleaseIdleTextInput();

    /// <summary>The view the geometry below is relative to (<c>UITextInput.textInputView</c>).</summary>
    // nil when the field is gone (the binding's type says otherwise).
    [Export("textInputView")]
    public UIView TextInputView => Surrogate!;

    #region UIKeyInput (edits only while the field is focused)

    public bool HasText => Surrogate?.HasText == true;

    public void InsertText(string text) => Editing?.InsertText(text);

    public void DeleteBackward() => Editing?.DeleteBackward();

    #endregion

    #region UITextInput: text and selection

    public string TextInRange(UITextRange range) => Surrogate?.TextInRange(range) ?? string.Empty;

    public void ReplaceText(UITextRange range, string text) => Editing?.ReplaceText(range, text);

    public UITextRange? SelectedTextRange
    {
        get => Surrogate?.SelectedTextRange;
        set
        {
            // VoiceOver moves the caret as it reads, or selects: the field's selection, as the screen reader's (the keyboard follows).
            if (value is SkUiTextRange range)
                Bridge?.SetSelection(Id, range.StartIndex, range.EndIndex);
        }
    }

    // nil without marked text (the binding's type says otherwise).
    public UITextRange MarkedTextRange => Surrogate?.MarkedTextRange!;

    public NSDictionary? MarkedTextStyle
    {
        get => Editing?.MarkedTextStyle;
        set
        {
            if (Editing is { } view)
                view.MarkedTextStyle = value;
        }
    }

    public void SetMarkedText(string? markedText, NSRange selectedRange) => Editing?.SetMarkedText(markedText, selectedRange);

    public void UnmarkText() => Editing?.UnmarkText();

    public UITextPosition BeginningOfDocument => new SkUiTextPosition(0);

    public UITextPosition EndOfDocument => Surrogate?.EndOfDocument ?? new SkUiTextPosition(0);

    // The keyboard's delegate is the first responder's; the element only reports it.
    public NSObject? WeakInputDelegate
    {
        get => Editing?.WeakInputDelegate;
        set { }
    }

    // nil when the field is gone (the binding's type says otherwise).
    public NSObject WeakTokenizer => Surrogate?.WeakTokenizer!;

    #endregion

    #region UITextInput: positions

    public UITextRange GetTextRange(UITextPosition fromPosition, UITextPosition toPosition) =>
        new SkUiTextRange(Index(fromPosition), Index(toPosition));

    // nil past the text's ends (the binding's type says otherwise).
    public UITextPosition GetPosition(UITextPosition fromPosition, nint offset) => Surrogate?.GetPosition(fromPosition, offset)!;

    public UITextPosition GetPosition(UITextPosition fromPosition, UITextLayoutDirection inDirection, nint offset) =>
        Surrogate?.GetPosition(fromPosition, inDirection, offset)!;

    public NSComparisonResult ComparePosition(UITextPosition first, UITextPosition second)
    {
        var (a, b) = (Index(first), Index(second));
        return a < b ? NSComparisonResult.Ascending : a > b ? NSComparisonResult.Descending : NSComparisonResult.Same;
    }

    public nint GetOffsetFromPosition(UITextPosition fromPosition, UITextPosition toPosition) => Index(toPosition) - Index(fromPosition);

    public UITextPosition GetPositionWithinRange(UITextRange range, UITextLayoutDirection direction) =>
        direction is UITextLayoutDirection.Right or UITextLayoutDirection.Down ? range.End : range.Start;

    public UITextRange GetCharacterRange(UITextPosition byExtendingPosition, UITextLayoutDirection direction) =>
        Surrogate?.GetCharacterRange(byExtendingPosition, direction)!;

    public NSWritingDirection GetBaseWritingDirection(UITextPosition forPosition, UITextStorageDirection direction) =>
        Surrogate?.GetBaseWritingDirection(forPosition, direction) ?? NSWritingDirection.Natural;

    public void SetBaseWritingDirectionforRange(NSWritingDirection writingDirection, UITextRange range)
    {
        // The field's direction follows its FlowDirection.
    }

    private static int Index(UITextPosition position) => position is SkUiTextPosition p ? p.Index : 0;

    #endregion

    #region UITextInput: geometry (in the surrogate's coordinates: textInputView)

    public CGRect GetFirstRectForRange(UITextRange range) => Surrogate?.GetFirstRectForRange(range) ?? CGRect.Empty;

    public CGRect GetCaretRectForPosition(UITextPosition? position) => Surrogate?.GetCaretRectForPosition(position) ?? CGRect.Empty;

    public UITextSelectionRect[] GetSelectionRects(UITextRange range) => Surrogate?.GetSelectionRects(range) ?? [];

    public UITextPosition GetClosestPositionToPoint(CGPoint point) => Surrogate?.GetClosestPositionToPoint(point) ?? new SkUiTextPosition(0);

    public UITextPosition GetClosestPositionToPoint(CGPoint point, UITextRange withinRange) =>
        Surrogate?.GetClosestPositionToPoint(point, withinRange) ?? new SkUiTextPosition(0);

    public UITextRange GetCharacterRangeAtPoint(CGPoint point) => Surrogate?.GetCharacterRangeAtPoint(point) ?? new SkUiTextRange(0, 0);

    #endregion
}
/// <summary>
/// The tokenizer of a drawn field's text input view: UIKit's string tokenizer (characters, words, sentences, paragraphs), with
/// lines from the field's layout. UIKit's line boundaries come from geometry it cannot read from a view that draws nothing
/// (a single-line entry's "line" was one word); VoiceOver's Lines rotor and the keyboard's line movement read these.
/// </summary>
internal sealed class SkUiTextInputTokenizer(SkUiTextInputView view) : UITextInputStringTokenizer(view)
{
    private ISkUiTextInputClient? Client => view.Client;

    private static bool IsBackward(UITextDirection direction) =>
        (long)direction is (long)UITextStorageDirection.Backward or (long)UITextLayoutDirection.Left or (long)UITextLayoutDirection.Up;

    private static int Index(UITextPosition position) => position is SkUiTextPosition p ? p.Index : 0;

    public override UITextRange GetRangeEnclosingPosition(UITextPosition position, UITextGranularity granularity, UITextDirection direction)
    {
        if (granularity != UITextGranularity.Line || Client is not { } client)
            return base.GetRangeEnclosingPosition(position, granularity, direction);
        var index = Index(position);
        var line = client.GetLineAt(index);
        // Backward from a line's start: the line before it.
        if (IsBackward(direction) && index == line.Start && index > 0)
            line = client.GetLineAt(index - 1);
        return new SkUiTextRange(line.Start, line.End);
    }

    public override bool ProbeDirection(UITextPosition probePosition, UITextGranularity atBoundary, UITextDirection inDirection)
    {
        if (atBoundary != UITextGranularity.Line || Client is not { } client)
            return base.ProbeDirection(probePosition, atBoundary, inDirection);
        var index = Index(probePosition);
        var line = client.GetLineAt(index);
        return IsBackward(inDirection) ? index == line.Start : index == line.End;
    }

    // nil past the text's ends (the binding's type says otherwise).
    public override UITextPosition GetPosition(UITextPosition fromPosition, UITextGranularity toBoundary, UITextDirection inDirection)
    {
        if (toBoundary != UITextGranularity.Line || Client is not { } client)
            return base.GetPosition(fromPosition, toBoundary, inDirection);
        var index = Index(fromPosition);
        var length = client.Editor.Text.Length;
        var line = client.GetLineAt(index);
        if (IsBackward(inDirection))
            return index > line.Start ? new SkUiTextPosition(line.Start) : index > 0 ? new SkUiTextPosition(client.GetLineAt(index - 1).Start) : null!;
        // A paragraph's line ends before its break: the next line's end is past it.
        return index < line.End ? new SkUiTextPosition(line.End) : index < length ? new SkUiTextPosition(client.GetLineAt(index + 1).End) : null!;
    }

    public override bool ProbeDirectionWithinTextUnit(UITextPosition probePosition, UITextGranularity withinTextUnit, UITextDirection inDirection)
    {
        if (withinTextUnit != UITextGranularity.Line || Client is not { } client)
            return base.ProbeDirectionWithinTextUnit(probePosition, withinTextUnit, inDirection);
        var index = Index(probePosition);
        var line = client.GetLineAt(index);
        return IsBackward(inDirection) ? index > line.Start && index <= line.End : index >= line.Start && index < line.End;
    }
}
#endif
