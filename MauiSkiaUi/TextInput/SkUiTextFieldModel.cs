using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>What the shared text field engine (<see cref="SkUiTextFieldModel"/>) needs from its control, on either layer.</summary>
internal interface ISkUiTextFieldOwner : ISkUiTransitionHost
{
    /// <summary>The drawn field node.</summary>
    ISkUiRenderable Node { get; }

    /// <summary>The field's arranged size.</summary>
    Size FieldSize { get; }

    /// <summary>Whether the field lays out right to left.</summary>
    bool IsRightToLeft { get; }

    /// <summary>The paragraph direction of the field's text.</summary>
    SkUiTextDirection TextDirection { get; }

    /// <summary>Whether the field has keyboard focus.</summary>
    bool IsFocused { get; }

    /// <summary>The user changed the text (typing, the input method, paste, undo): the control's <c>Text</c> follows.</summary>
    void TextEdited(string text);

    /// <summary>The selection or caret moved: the control's <c>CursorPosition</c> / <c>SelectionLength</c> follow.</summary>
    void SelectionEdited(int start, int length);

    /// <summary>The return key in a single-line field: <c>Completed</c> and <c>ReturnCommand</c>.</summary>
    void Completed();

    /// <summary>Takes keyboard focus (a tap on the field); returns whether it has it.</summary>
    bool RequestFocus();

    /// <summary>Gives keyboard focus up (Return on Apple platforms).</summary>
    void ReleaseFocus();

    void InvalidateMeasure();
    void InvalidatePaint();
    void InvalidateSemantics();

    /// <summary>The caret moved, showed or hid: the field's composite-time props are built again (nothing is recorded).</summary>
    void InvalidateCaret();

    /// <summary>
    /// Whether a press at <paramref name="local"/> (field coordinates) belongs to a leading or trailing view that handles pointers
    /// itself (a button): the field's own gestures leave it alone.
    /// </summary>
    bool AdornmentTakesPointer(Point local);
}

/// <summary>
/// The engine both drawn text fields share (<see cref="SkUiEntry"/>, <see cref="Core.SkUiCoreEntry"/>; later the editors): the
/// editing model (<see cref="SkUiTextEditor"/>) and its layout (<see cref="SkUiTextEditLayout"/>), the field drawn by the look with
/// the selection, text, placeholder and composition, the caret as a composite-time drawable that blinks without re-recording,
/// scrolling the text to keep the caret in view, keys, pointer selection, the clear button, the clipboard, screen readers, and
/// the input client the platform's text input talks to (<see cref="ISkUiTextInputClient"/>). The controls hold the properties
/// of their layer and copy the values here.
/// </summary>
internal sealed class SkUiTextFieldModel : ISkUiTextInputClient, ISkUiTextEditorOwner, ISkUiTextEditGeometry
{
    /// <summary>The font size of fields that set none (as SkiaUi's labels and pickers).</summary>
    internal const double DefaultFontSize = 16;

    // A caret this close to the text box's edge counts as inside it (scrolling keeps it in view).
    private const float ScrollMargin = 1;
    private static readonly SkUiTextInputOptions DefaultOptions = new(Keyboard.Default, ReturnType.Default, false, false, true, true, false,
        TextTransform.Default, null);

    // What assigned text is formatted against (D9).
    private static readonly SkUiTextEditValue EmptyValue = new(string.Empty, 0);

    private readonly ISkUiTextFieldOwner _owner;
    private readonly SkUiTextEditLayout _layout = new();
    // Made when the field first has a placeholder or a message (a field without either, as a sheet's cells, never makes them).
    private SkUiTextLayout? _placeholderLayout;
    private SkUiTextLayout? _messageLayout;
    private readonly SkUiTween _focus;
    private readonly List<SKRect> _rects = [];
    private readonly SkUiTextFieldGestures _gestures;
    private ISkUiTextInputProxy? _proxy;
    // Edits from the platform's input are not echoed back to it.
    private int _fromInput;
    private bool _layoutDirty = true;
    private string _display = string.Empty;
    // Password: where each shown bullet's grapheme starts in the text (and the text's length last).
    private int[]? _bulletStarts;
    // The text the bullet starts were built from: an edit makes them stale until the next layout (or a screen reader's query).
    private string? _bulletText;
    private float _scrollX;
    private float _scrollY;
    private bool _caretShown = true;
    private IDisposable? _blink;
    private SkUiDrawableSlot<CaretKey> _caret;
    private bool _clearPressed;
    // The field's own height (without its message) at the last measure: a node too short for both keeps the field at this height.
    private float _fieldHeight;

    private readonly record struct CaretKey(SKRect Rect, SKRect Clip, SKColor Color);

    public SkUiTextFieldModel(ISkUiTextFieldOwner owner, bool multiline = false)
    {
        _owner = owner;
        _focus = new SkUiTween(owner);
        Editor = new SkUiTextEditor(this) { IsMultiline = multiline };
        IsMultiline = multiline;
        VerticalTextAlignment = multiline ? TextAlignment.Start : TextAlignment.Center;
        _gestures = new SkUiTextFieldGestures(this);
    }

    /// <summary>The editing model: the text, selection, composition and undo.</summary>
    public SkUiTextEditor Editor { get; }

    /// <summary>The recognizer the field's pointer input goes through (the controls add it to their gestures).</summary>
    internal SkUiGestureRecognizer Gestures => _gestures;

    /// <summary>The layout of the shown text (tests).</summary>
    internal SkUiTextEditLayout Layout => _layout;

    #region State the controls copy in

    public readonly bool IsMultiline;
    public string? Placeholder;
    public Color? PlaceholderColor;
    public Color? TextColor;
    public Color? CaretColor;
    public Color? SelectionColor;
    public string? FontFamily;
    public double FontSize = DefaultFontSize;
    public FontAttributes FontAttributes;
    public bool FontAutoScalingEnabled = true;
    public double CharacterSpacing;
    public TextAlignment HorizontalTextAlignment = TextAlignment.Start;
    public TextAlignment VerticalTextAlignment;
    public Keyboard Keyboard = Keyboard.Default;
    public ReturnType ReturnType;
    public bool IsPassword;
    public bool IsSpellCheckEnabled = true;
    public bool IsTextPredictionEnabled = true;
    public ClearButtonVisibility ClearButtonVisibility;
    public string? AutofillHint;
    public bool IsEnabled = true;
    public bool IsPointerOver;
    public bool HasExplicitMinimumHeight;

    /// <summary>The field's rectangle in its node (a stepper's value between its buttons); <c>null</c> (default): the whole node.</summary>
    public SKRect? FieldArea;

    /// <summary>Whether the look draws the text field's chrome (default); a field drawn inside another control's chrome does not.</summary>
    public bool DrawsChrome = true;

    /// <summary>The look's padding around the text (default); a field in another control's chrome sets its own.</summary>
    public Thickness? Padding;

    /// <summary>A search bar: the look's search field with its icon at the start, and a clear button whenever there is text.</summary>
    public bool IsSearch;

    /// <summary>A search bar's icon color; <c>null</c>: the color scheme's muted color.</summary>
    public Color? SearchIconColor;

    /// <summary>The clear button's color (a search bar's <c>CancelButtonColor</c>); <c>null</c>: the color scheme's muted color.</summary>
    public Color? ClearButtonColor;

    /// <summary>The leading view's measured size (with its margin; <see cref="Size.Zero"/>: none or hidden).</summary>
    public Size LeadingSize;

    /// <summary>The trailing view's measured size (with its margin; <see cref="Size.Zero"/>: none or hidden).</summary>
    public Size TrailingSize;

    /// <summary>An editor's <c>AutoSize</c>: its measured height follows the text (otherwise typing does not measure it again).</summary>
    public bool AutoSize;

    /// <summary>The control's <c>FormatsAssignedText</c>: text the app sets goes through the input formatters too.</summary>
    public bool FormatsAssignedText;

    /// <summary>The control's <c>HasError</c>: the chrome and the message are drawn in the error color.</summary>
    public bool HasError;

    /// <summary>The control's <c>ErrorText</c>: the message under the field while <see cref="HasError"/> (in place of the helper text).</summary>
    public string? ErrorText;

    /// <summary>The control's <c>HelperText</c>: the message under the field (unless an error's text replaces it).</summary>
    public string? HelperText;

    /// <summary>The control's <c>ErrorColor</c>: the error chrome's and message's color; <c>null</c>: the color scheme's error color.</summary>
    public Color? ErrorColor;

    /// <summary>A numeric entry's − and + buttons, keys and wheel (D16); <c>null</c>: none.</summary>
    public SkUiTextFieldSteps? Steps;

    /// <summary>A numeric entry's keyboard (digits, a decimal separator, a sign), in place of <see cref="Keyboard"/>; none: <see cref="Keyboard"/>.</summary>
    public SkUiNumberKeyboard NumberKeyboard;

    /// <summary>A tap that focuses the field selects all its text instead of placing the caret (a numeric entry: typing replaces the value).</summary>
    public bool SelectsAllOnFocusTap;

    /// <summary>The control's <c>IsReadOnly</c> (the editor is also read-only while the text is not editable).</summary>
    public bool IsReadOnly { get; private set; }

    /// <summary>Whether the user may type (a numeric entry's <c>IsEditable</c>; its buttons and keys still step).</summary>
    public bool IsTextEditable { get; private set; } = true;

    /// <summary>The control's <c>IsReadOnly</c> or a numeric entry's <c>IsEditable</c> changed: the editor and the keyboard follow.</summary>
    public void SetReadOnly(bool isReadOnly, bool isTextEditable = true)
    {
        IsReadOnly = isReadOnly;
        IsTextEditable = isTextEditable;
        Editor.IsReadOnly = isReadOnly || !isTextEditable;
        OptionsChanged();
        PaintChanged();
    }

    /// <summary>
    /// The text the control holds: the app set it (not an edit of the user). The field shows it with an editor's line breaks as
    /// <c>\n</c> (<c>\r\n</c> and <c>\r</c>, as a Windows text box gives them, are one break each), cut to <c>MaxLength</c>,
    /// transformed (<c>TextTransform</c>) and, with <see cref="FormatsAssignedText"/>, formatted by the input formatters; returns
    /// that text when it differs, for the control's <c>Text</c> to follow (as MAUI's fields push the text they show back), else
    /// <c>null</c>. The control raises its own change first, then follows.
    /// </summary>
    public string? SetText(string? text)
    {
        var source = text ?? string.Empty;
        var lines = IsMultiline ? SkUiTextEditor.NormalizeLineBreaks(source) : source;
        var shown = SkUiTextTransform.Apply(Cut(lines), Editor.Transform);
        if (FormatsAssignedText && Editor.HasFormatters)
            shown = FormatAssigned(shown);
        if (shown != Editor.Text)
            Editor.SetText(shown);
        return shown == source ? null : shown;
    }

    /// <summary>
    /// The input formatters changed (or <see cref="FormatsAssignedText"/> was turned on): the text already in the field is
    /// formatted by them, as <c>TextTransform</c> transforms it when it changes (as if pasted into an empty field; the caret
    /// stays). Returns the new text for the control's <c>Text</c> to follow, or <c>null</c>.
    /// </summary>
    public string? FormatsChanged()
    {
        if (!Editor.HasFormatters)
            return null;
        var shown = FormatAssigned(Editor.Text);
        if (shown == Editor.Text)
            return null;
        Editor.SetText(shown, keepCaret: true);
        return shown;
    }

    // Text the app set, through the formatters as if it were pasted into an empty field (and still within MaxLength).
    private string FormatAssigned(string text) => Cut(Editor.Format(EmptyValue, new SkUiTextEditValue(text, text.Length)).Text);

    // Cut to MaxLength, never splitting a surrogate pair (as typed text is cut).
    private string Cut(string text)
    {
        if (Editor.MaxLength == int.MaxValue || text.Length <= Editor.MaxLength)
            return text;
        var length = Editor.MaxLength;
        if (length > 0 && char.IsLowSurrogate(text[length]))
            length--;
        return text[..length];
    }

    /// <summary>
    /// The control's <c>TextTransform</c> changed: the keyboard's options follow, and the text already in the field is transformed
    /// too (the caret and selection stay). Returns the transformed text for the control's <c>Text</c> to follow, or <c>null</c>.
    /// </summary>
    public string? SetTransform(TextTransform value)
    {
        Editor.Transform = value;
        OptionsChanged();
        var shown = SkUiTextTransform.Apply(Editor.Text, value);
        if (shown == Editor.Text)
            return null;
        Editor.SetText(shown, keepCaret: true);
        return shown;
    }

    /// <summary>The control's <c>CursorPosition</c> / <c>SelectionLength</c> were set: the selection follows.</summary>
    public void SetSelection(int cursorPosition, int selectionLength)
    {
        var start = Math.Clamp(cursorPosition, 0, Editor.Text.Length);
        var end = Math.Clamp(start + Math.Max(0, selectionLength), start, Editor.Text.Length);
        if (start == Editor.SelectionStart && end == Editor.SelectionEnd)
            return;
        Editor.Select(SkUiTextBoundaries.AlignToGrapheme(Editor.Text, start), SkUiTextBoundaries.AlignToGrapheme(Editor.Text, end));
    }

    /// <summary>A property the text's shape depends on changed (font, spacing, direction, password).</summary>
    public void TextChanged()
    {
        _layoutDirty = true;
        _placeholderLayout?.Invalidate();
        _messageLayout?.Invalidate();
        Steps?.InvalidateLayout();
        _owner.InvalidateMeasure();
        _owner.InvalidatePaint();
        _owner.InvalidateSemantics();
        _owner.InvalidateCaret();
    }

    /// <summary>
    /// What the message under the field depends on changed (<see cref="HasError"/>, <see cref="ErrorText"/>,
    /// <see cref="HelperText"/>): the field is measured and drawn again, and screen readers read the new message.
    /// </summary>
    public void MessageChanged()
    {
        // The layout keeps its lines until told: its key is the style and width, not the text.
        _messageLayout?.Invalidate();
        _owner.InvalidateMeasure();
        _owner.InvalidatePaint();
        _owner.InvalidateSemantics();
        _owner.InvalidateCaret();
    }

    /// <summary>A paint-only property changed (a color, alignment).</summary>
    public void PaintChanged()
    {
        _owner.InvalidatePaint();
        _owner.InvalidateCaret();
    }

    /// <summary>What screen readers read changed (a numeric entry's range: its increment and decrement actions).</summary>
    public void SemanticsChanged() => _owner.InvalidateSemantics();

    /// <summary>A property the platform's input reads changed (keyboard, return type, password, corrections): it restarts.</summary>
    public void OptionsChanged()
    {
        _proxy?.OptionsChanged(this);
        _owner.InvalidateSemantics();
    }

    #endregion

    #region Text and geometry

    private SkUiTextStyle TextStyle => new(SkUiTypefaces.Resolve(FontFamily, FontAttributes), SkUiFontScaling.ScaleFontSize(FontSize, FontAutoScalingEnabled),
        LineBreakMode.WordWrap, null, -1, -1, CharacterSpacing, _owner.TextDirection, SkUiTextRendering.Default, FontAttributes);

    /// <summary>A step button's text: one line in <paramref name="fontFamily"/> (else the field's) at the field's size.</summary>
    internal SkUiTextStyle ButtonTextStyle(string? fontFamily) => TextStyle with
    {
        Typeface = SkUiTypefaces.Resolve(fontFamily ?? FontFamily, FontAttributes), LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1
    };

    private SkUiTextStyle PlaceholderStyle => TextStyle with { LineBreakMode = IsMultiline ? LineBreakMode.WordWrap : LineBreakMode.TailTruncation, MaxLines = IsMultiline ? -1 : 1 };

    /// <summary>Whether the clear button shows: <see cref="ClearButtonVisibility.WhileEditing"/>, focused, with text, editable.</summary>
    internal bool ClearButtonShown => (IsSearch || (ClearButtonVisibility == ClearButtonVisibility.WhileEditing && _owner.IsFocused))
        && Editor.Text.Length > 0 && IsEnabled && !Editor.IsReadOnly;

    /// <summary>
    /// What sits across the field's width, as room taken from its start and its end side (mirrored in right-to-left): at the start
    /// a numeric entry's step buttons, the padding, a search bar's icon, the leading view and the space after it; at the end the
    /// step buttons, the padding, the trailing view, then the clear button (or the space before the trailing view). The text box
    /// takes what is left between them.
    /// </summary>
    private readonly record struct AdornmentRow(float StartButtons, float StartPadding, float SearchIcon, float Leading, float LeadingSpacing,
        float EndButtons, float EndPadding, float Trailing, float Clear, float TrailingSpacing)
    {
        /// <summary>The room before the text box, from the start side.</summary>
        public float Start => StartButtons + StartPadding + SearchIcon + Leading + LeadingSpacing;

        /// <summary>The room after the text box, from the end side.</summary>
        public float End => EndButtons + EndPadding + Trailing + Clear + TrailingSpacing;
    }

    private AdornmentRow Row
    {
        get
        {
            var look = SkUiLook.Current;
            var padding = Padding ?? look.TextFieldPadding;
            var rightToLeft = _owner.IsRightToLeft;
            var spacing = (float)look.TextFieldAdornmentSpacing;
            var leading = (float)LeadingSize.Width;
            var trailing = (float)TrailingSize.Width;
            var clear = ClearButtonShown ? (float)look.TextFieldClearButtonWidth : 0;
            var buttons = Steps?.Room ?? default;
            return new AdornmentRow(buttons.Start, (float)(rightToLeft ? padding.Right : padding.Left), IsSearch ? (float)look.SearchIconWidth : 0,
                leading, leading > 0 ? spacing : 0, buttons.End, (float)(rightToLeft ? padding.Left : padding.Right), trailing, clear,
                trailing > 0 && clear <= 0 ? spacing : 0);
        }
    }

    /// <summary>
    /// Where the text box sits in the field: the look's padding, with the adornments' room at the start and end sides (and a split,
    /// stacked numeric entry's bars above and below).
    /// </summary>
    private Thickness Inset
    {
        get
        {
            var padding = Padding ?? SkUiLook.Current.TextFieldPadding;
            var row = Row;
            var (top, bottom) = BarRoom;
            return _owner.IsRightToLeft
                ? new Thickness(row.End, padding.Top + top, row.Start, padding.Bottom + bottom)
                : new Thickness(row.Start, padding.Top + top, row.End, padding.Bottom + bottom);
        }
    }

    /// <summary>The heights a split, stacked numeric entry's bars take above and below the text (else none).</summary>
    private (float Top, float Bottom) BarRoom
    {
        get
        {
            if (Steps is not { } steps)
                return default;
            var room = steps.Room;
            return (room.Top, room.Bottom);
        }
    }

    /// <summary>The field's area without a split, stacked numeric entry's bars: where the row of adornments sits.</summary>
    private SKRect RowArea(SKRect area)
    {
        var (top, bottom) = BarRoom;
        if (top <= 0 && bottom <= 0)
            return area;
        var rowTop = Math.Min(area.Top + top, area.Bottom);
        return new SKRect(area.Left, rowTop, area.Right, Math.Max(rowTop, area.Bottom - bottom));
    }

    /// <summary>
    /// The − and + buttons' rectangles in a node of the given size (empty without buttons): beside the text at the start or end side
    /// (side by side − then + in reading order, or stacked + above −), split (− at the start, + at the end; stacked: + in a bar above
    /// the text, − in one below it). Mirrored in right-to-left.
    /// </summary>
    internal (SKRect Decrement, SKRect Increment) StepButtonBounds(float width, float height)
    {
        if (Steps is not { HasButtons: true } steps)
            return default;
        var area = Area(width, height);
        var room = steps.Room;
        var vertical = steps.Orientation == StackOrientation.Vertical;
        switch (steps.Placement)
        {
            case SkUiStepButtonPlacement.Split when vertical:
                return (new SKRect(area.Left, Math.Max(area.Top, area.Bottom - room.Bottom), area.Right, area.Bottom),
                    new SKRect(area.Left, area.Top, area.Right, Math.Min(area.Bottom, area.Top + room.Top)));
            case SkUiStepButtonPlacement.Split:
                return (FromStart(area, 0, room.Start, area.Top, area.Bottom), FromEnd(area, 0, room.End, area.Top, area.Bottom));
        }
        var start = steps.Placement == SkUiStepButtonPlacement.Start;
        var group = start ? FromStart(area, 0, room.Start, area.Top, area.Bottom) : FromEnd(area, 0, room.End, area.Top, area.Bottom);
        if (vertical)
        {
            var middle = (group.Top + group.Bottom) / 2;
            return (new SKRect(group.Left, middle, group.Right, group.Bottom), new SKRect(group.Left, group.Top, group.Right, middle));
        }
        // − before + in reading order: − on the left in left-to-right layouts, on the right in right-to-left.
        var center = (group.Left + group.Right) / 2;
        var left = new SKRect(group.Left, group.Top, center, group.Bottom);
        var right = new SKRect(center, group.Top, group.Right, group.Bottom);
        return _owner.IsRightToLeft ? (right, left) : (left, right);
    }

    /// <summary><see cref="StepButtonBounds(float, float)"/> at the field's arranged size.</summary>
    internal (SKRect Decrement, SKRect Increment) StepButtonBounds()
    {
        var (width, height) = Size;
        return StepButtonBounds(width, height);
    }

    /// <summary>
    /// The field's rectangle in a node of the given size: <see cref="FieldArea"/>, else the node above the message (the whole node
    /// without one). A node too short for the field and its message (a fixed height) keeps the field at its own height and the
    /// message hangs below the node (<see cref="GetOverflow"/>).
    /// </summary>
    private SKRect Area(float width, float height)
    {
        if (FieldArea is { } area)
            return area;
        var message = MessageHeight(width);
        return message <= 0 ? new SKRect(0, 0, width, height)
            : new SKRect(0, 0, width, Math.Min(height, Math.Max(height - message, _fieldHeight)));
    }

    /// <summary>
    /// The message under the field: <see cref="ErrorText"/> while <see cref="HasError"/> (when it has text, as Material's error
    /// text replaces the supporting text), else <see cref="HelperText"/>; <c>null</c>: none.
    /// </summary>
    internal string? Message => HasError && !string.IsNullOrEmpty(ErrorText) ? ErrorText : string.IsNullOrEmpty(HelperText) ? null : HelperText;

    /// <summary>The message's text: the field's font family at the look's message size, wrapped.</summary>
    private SkUiTextStyle MessageStyle => new(SkUiTypefaces.Resolve(FontFamily),
        SkUiFontScaling.ScaleFontSize(SkUiLook.Current.TextFieldMessageFontSize, FontAutoScalingEnabled), Direction: _owner.TextDirection);

    /// <summary>The message's padding in its band: the field's horizontal padding (start and end), the look's spacing above it.</summary>
    private Thickness MessagePadding
    {
        get
        {
            var look = SkUiLook.Current;
            var padding = Padding ?? look.TextFieldPadding;
            var (left, right) = _owner.IsRightToLeft ? (padding.Right, padding.Left) : (padding.Left, padding.Right);
            return new Thickness(left, look.TextFieldMessageSpacing, right, 0);
        }
    }

    /// <summary>The height of the message's band under the field (its spacing and wrapped lines) in a node <paramref name="width"/> DIPs wide; 0 without one.</summary>
    private float MessageHeight(float width) =>
        FieldArea is null && Message is { } message ? (float)MessageLayout.Measure(message, MessageStyle, MessagePadding, width).Height : 0;

    /// <summary>The stretch from <paramref name="from"/> to <paramref name="to"/> DIPs past the area's start side (its left, or its right in right-to-left).</summary>
    private SKRect FromStart(SKRect area, float from, float to, float top, float bottom) => _owner.IsRightToLeft
        ? new SKRect(area.Right - to, top, area.Right - from, bottom)
        : new SKRect(area.Left + from, top, area.Left + to, bottom);

    /// <summary>The stretch from <paramref name="from"/> to <paramref name="to"/> DIPs before the area's end side.</summary>
    private SKRect FromEnd(SKRect area, float from, float to, float top, float bottom) => FromStart(area, area.Width - to, area.Width - from, top, bottom);

    /// <summary>
    /// The rectangles to arrange the leading and trailing views in (node coordinates as a parent arranges a child: the views
    /// mirror themselves in right-to-left): their measured size, beside the text box, aligned with the text
    /// (<see cref="VerticalTextAlignment"/>) and kept inside the field.
    /// </summary>
    internal (Rect Leading, Rect Trailing) AdornmentFrames(double width, double height)
    {
        var area = RowArea(Area((float)width, (float)height));
        var row = Row;
        var (leadingTop, leadingBottom) = AdornmentBand(area, LeadingSize.Height);
        var (trailingTop, trailingBottom) = AdornmentBand(area, TrailingSize.Height);
        var leadingStart = row.StartButtons + row.StartPadding + row.SearchIcon;
        var leading = FromStart(area, leadingStart, leadingStart + row.Leading, leadingTop, leadingBottom);
        var trailingStart = row.EndButtons + row.EndPadding;
        var trailing = FromEnd(area, trailingStart, trailingStart + row.Trailing, trailingTop, trailingBottom);
        return (ToArranged(leading, width), ToArranged(trailing, width));
    }

    // A child's rectangle as its parent arranges it: in right-to-left, the child mirrors the rectangle it is given in the parent's width.
    private Rect ToArranged(SKRect rect, double width) =>
        new(_owner.IsRightToLeft ? width - rect.Right : rect.Left, rect.Top, rect.Width, rect.Height);

    /// <summary>Where an adornment <paramref name="height"/> DIPs tall sits: aligned with the text box as the text is, inside the area.</summary>
    private (float Top, float Bottom) AdornmentBand(SKRect area, double height)
    {
        var padding = Padding ?? SkUiLook.Current.TextFieldPadding;
        var size = (float)Math.Min(height, area.Height);
        var top = area.Top + (float)padding.Top;
        var bottom = Math.Max(top, area.Bottom - (float)padding.Bottom);
        var y = VerticalTextAlignment switch
        {
            TextAlignment.Start => top,
            TextAlignment.End => bottom - size,
            _ => top + (bottom - top - size) / 2
        };
        y = Math.Clamp(y, area.Top, Math.Max(area.Top, area.Bottom - size));
        return (y, y + size);
    }

    /// <summary>The text box in a node of the given size (node coordinates).</summary>
    private SKRect TextBox(float width, float height)
    {
        var area = Area(width, height);
        var inset = Inset;
        var left = area.Left + (float)inset.Left;
        var top = area.Top + (float)inset.Top;
        return new SKRect(left, top, Math.Max(left, area.Right - (float)inset.Right), Math.Max(top, area.Bottom - (float)inset.Bottom));
    }

    /// <summary>The clear button's room at the end side of the field, inside the trailing view.</summary>
    private SKRect ClearButtonBounds(float width, float height)
    {
        var area = RowArea(Area(width, height));
        var row = Row;
        var from = row.EndButtons + row.EndPadding + row.Trailing;
        return FromEnd(area, from, from + (float)SkUiLook.Current.TextFieldClearButtonWidth, area.Top, area.Bottom);
    }

    /// <summary>A search bar's icon room at the start side of the field, before the leading view.</summary>
    private SKRect SearchIconBounds(float width, float height)
    {
        var area = RowArea(Area(width, height));
        var row = Row;
        var from = row.StartButtons + row.StartPadding;
        return FromStart(area, from, from + (float)SkUiLook.Current.SearchIconWidth, area.Top, area.Bottom);
    }

    private SkUiTextLayout PlaceholderLayout => _placeholderLayout ??= new SkUiTextLayout(_owner);

    /// <summary>Whether the placeholder's and the message's layouts were made (tests: a field without either makes none).</summary>
    internal (bool Placeholder, bool Message) HasLayouts => (_placeholderLayout is not null, _messageLayout is not null);

    private SkUiTextLayout MessageLayout => _messageLayout ??= new SkUiTextLayout(_owner);

    /// <summary>The text as drawn: bullets for a password, spaces for line breaks in a single-line field.</summary>
    private string DisplayText(string text)
    {
        if (IsPassword)
        {
            var count = 0;
            for (var index = 0; index < text.Length; index = SkUiTextBoundaries.NextGrapheme(text, index))
                count++;
            var starts = new int[count + 1];
            var bullet = 0;
            for (var index = 0; index < text.Length; index = SkUiTextBoundaries.NextGrapheme(text, index))
                starts[bullet++] = index;
            starts[count] = text.Length;
            _bulletStarts = starts;
            _bulletText = text;
            return new string('•', count);
        }
        _bulletStarts = null;
        _bulletText = null;
        return !IsMultiline && text.AsSpan().IndexOfAny('\r', '\n') >= 0 ? text.Replace('\r', ' ').Replace('\n', ' ') : text;
    }

    /// <summary>A text index in the shown text (a bullet index for a password).</summary>
    private int ToDisplay(int index)
    {
        if (_bulletStarts is not { } starts)
            return index;
        var found = Array.BinarySearch(starts, index);
        return found >= 0 ? found : ~found - 1;
    }

    /// <summary>A shown-text index back in the text.</summary>
    private int ToSource(int index) => _bulletStarts is { } starts ? starts[Math.Clamp(index, 0, starts.Length - 1)] : index;

    /// <summary>Lays the text out for a text box <paramref name="boxWidth"/> wide (multiline text wraps at it).</summary>
    private void EnsureLayout(float boxWidth)
    {
        if (_layoutDirty || !ReferenceEquals(_layout.Text, _display) && _display != _layout.Text)
        {
            _display = DisplayText(Editor.Text);
            _layoutDirty = false;
        }
        _layout.Update(_display, TextStyle, IsMultiline ? boxWidth : double.PositiveInfinity);
        _layout.Arrange(HorizontalTextAlignment, Math.Max(0, boxWidth));
    }

    private float LineHeight
    {
        get
        {
            var font = SkUiTextResources.Font(TextStyle.Typeface, (float)TextStyle.FontSize, FontAttributes);
            return font.Spacing;
        }
    }

    /// <summary>
    /// The field's size: its text (or placeholder) with the padding and the adornments beside it (<see cref="LeadingSize"/> and
    /// <see cref="TrailingSize"/>, measured first), at least the look's touch height and the adornments' height.
    /// </summary>
    public Size Measure(double widthConstraint, double heightConstraint)
    {
        var inset = Inset;
        var available = Math.Max(0, widthConstraint - inset.HorizontalThickness);
        EnsureLayout(double.IsFinite(available) ? (float)available : float.PositiveInfinity);
        var width = (double)_layout.Width;
        var height = (double)Math.Max(_layout.Height, LineHeight);
        if (Editor.Text.Length == 0 && !string.IsNullOrEmpty(Placeholder))
        {
            var placeholder = PlaceholderLayout.Measure(Placeholder, PlaceholderStyle, default, available);
            width = Math.Max(width, placeholder.Width);
            height = Math.Max(height, placeholder.Height);
        }
        // Room for the caret at the end of the text.
        width += SkUiLook.Current.CaretWidth;
        var size = new Size(Math.Min(widthConstraint, width + inset.HorizontalThickness),
            Math.Max(height + inset.VerticalThickness, Math.Max(LeadingSize.Height, TrailingSize.Height)));
        if (!HasExplicitMinimumHeight)
            size.Height = Math.Max(size.Height, SkUiLook.Current.DefaultTextFieldMinimumHeight);
        _fieldHeight = (float)size.Height;
        if (FieldArea is not null || Message is not { } message)
            return size;
        // The message under the field, wrapped at the width the field may take: the field is at least as wide as the message's
        // longest line, so the lines drawn at the arranged width are never more than the lines measured here.
        var band = MessageLayout.Measure(message, MessageStyle, MessagePadding, widthConstraint);
        return new Size(Math.Min(widthConstraint, Math.Max(size.Width, band.Width)), size.Height + band.Height);
    }

    /// <summary>Where the text's box (its first line's top-left) is drawn in the field, scrolled.</summary>
    private SKPoint TextOrigin(SKRect box)
    {
        var top = box.Top;
        if (!IsMultiline || _layout.Height < box.Height)
        {
            var free = box.Height - _layout.Height;
            top += VerticalTextAlignment == TextAlignment.Center ? free / 2 : VerticalTextAlignment == TextAlignment.End ? free : 0;
        }
        return new SKPoint(box.Left - _scrollX, top - _scrollY);
    }

    /// <summary>Scrolls the text so the caret is inside the text box (single line: horizontally; multiline: vertically).</summary>
    private void KeepCaretInView(SKRect box)
    {
        if (_layout.Lines.Count == 0)
            return;
        var caret = _layout.CaretRect(ToDisplay(Editor.Caret), Editor.CaretUpstream);
        var caretWidth = (float)SkUiLook.Current.CaretWidth;
        if (!IsMultiline)
        {
            var line = _layout.Lines[0];
            var content = Math.Max(_layout.Width, MaxExtent(line)) + caretWidth;
            if (content <= box.Width)
            {
                _scrollX = 0;
                return;
            }
            // The aligned line's left edge in the box; the scroll keeps the caret between the box's edges.
            var x = caret.Left;
            var maxScroll = Math.Max(0, content - box.Width + Math.Min(0, line.Offset));
            if (x - _scrollX < ScrollMargin)
                _scrollX = x - ScrollMargin;
            else if (x + caretWidth - _scrollX > box.Width - ScrollMargin)
                _scrollX = x + caretWidth - box.Width + ScrollMargin;
            _scrollX = Math.Clamp(_scrollX, Math.Min(0, line.Offset), Math.Max(maxScroll, line.Offset + _layout.Width + caretWidth - box.Width));
            return;
        }
        if (_manualScroll)
        {
            _scrollY = Math.Clamp(_scrollY, 0, Math.Max(0, _layout.Height - box.Height));
            return;
        }
        if (caret.Top - _scrollY < 0)
            _scrollY = caret.Top;
        else if (caret.Bottom - _scrollY > box.Height)
            _scrollY = caret.Bottom - box.Height;
        _scrollY = Math.Clamp(_scrollY, 0, Math.Max(0, _layout.Height - box.Height));
    }

    private static float MaxExtent(SkUiTextEditLine line)
    {
        var max = line.Width;
        for (var unit = 0; unit < line.Length; unit++)
            max = Math.Max(max, line.Right[unit]);
        return max;
    }

    #endregion

    #region Drawing

    /// <summary>Draws the field: the look's chrome, then (clipped to the text box) the selection, the text or placeholder and the composition's underline, then the clear button.</summary>
    public void Draw(SKCanvas canvas, float width, float height)
    {
        var foreground = ToSkColor(TextColor ?? SkUiColors.DefaultForeground);
        if (IsSearch)
            SkUiLook.Current.DrawSearchField(canvas, new SkUiSearchFieldPaint
            {
                Bounds = Area(width, height),
                IconBounds = SearchIconBounds(width, height),
                Accent = ToSkColor(SkUiColors.Accent),
                Fill = ToSkColor(SkUiColors.Muted),
                IconColor = ToSkColor(SearchIconColor ?? SkUiColors.Muted),
                Focused = _focus.Value,
                IsPointerOver = IsPointerOver,
                IsEnabled = IsEnabled,
                IsRightToLeft = _owner.IsRightToLeft,
                HasError = HasError,
                Error = ToSkColor(ErrorColor ?? SkUiColors.Error)
            });
        else if (DrawsChrome)
            SkUiLook.Current.DrawTextField(canvas, new SkUiTextFieldPaint
            {
                Bounds = Area(width, height),
                Accent = ToSkColor(SkUiColors.Accent),
                Border = ToSkColor(SkUiColors.Muted),
                Foreground = foreground,
                Focused = _focus.Value,
                IsPointerOver = IsPointerOver,
                IsEnabled = IsEnabled,
                IsReadOnly = Editor.IsReadOnly,
                IsMultiline = IsMultiline,
                IsRightToLeft = _owner.IsRightToLeft,
                HasError = HasError,
                Error = ToSkColor(ErrorColor ?? SkUiColors.Error)
            });
        if (Steps is { HasButtons: true } steps)
        {
            var (decrement, increment) = StepButtonBounds(width, height);
            steps.Draw(canvas, Area(width, height), decrement, increment, ToSkColor(SkUiColors.Muted), foreground, IsEnabled, _owner.IsRightToLeft);
        }
        var box = TextBox(width, height);
        EnsureLayout(box.Width);
        KeepCaretInView(box);
        var origin = TextOrigin(box);
        canvas.Save();
        canvas.ClipRect(box);
        var paint = SkUiTextResources.TextPaint;
        if (_owner.IsFocused && Editor.HasSelection)
        {
            _rects.Clear();
            _layout.AddRangeRects(ToDisplay(Editor.SelectionStart), ToDisplay(Editor.SelectionEnd), _rects);
            paint.Color = ToSkColor(SelectionColor ?? SkUiLook.Current.DefaultSelectionColor);
            foreach (var rect in _rects)
                canvas.DrawRect(rect.Left + origin.X, rect.Top + origin.Y, rect.Width, rect.Height, paint);
        }
        if (Editor.Text.Length == 0)
        {
            if (!string.IsNullOrEmpty(Placeholder))
            {
                var color = ToSkColor(PlaceholderColor ?? SkUiColors.Muted);
                paint.Color = IsEnabled ? color : color.WithAlpha((byte)(color.Alpha * 0.5f));
                var inset = new Thickness(box.Left, origin.Y, width - box.Right, 0);
                PlaceholderLayout.Draw(canvas, Placeholder, PlaceholderStyle, inset, width, Math.Max(origin.Y + _layout.Height, height),
                    HorizontalTextAlignment, TextAlignment.Start, paint);
            }
        }
        else
        {
            paint.Color = IsEnabled ? foreground : foreground.WithAlpha((byte)(foreground.Alpha * 0.5f));
            _layout.Draw(canvas, origin.X, origin.Y, paint, box.Top - origin.Y, box.Bottom - origin.Y);
            if (Editor.HasComposition)
                DrawComposition(canvas, origin, paint);
        }
        canvas.Restore();
        if (HandlesShown)
            DrawHandles(canvas, width, height);
        else if (InsertionHandleShown)
            DrawInsertionHandle(canvas, width, height);
        if (ClearButtonShown)
            SkUiLook.Current.DrawTextFieldClearButton(canvas, new SkUiClearButtonPaint(ClearButtonBounds(width, height), ToSkColor(ClearButtonColor ?? SkUiColors.Muted),
                ToSkColor(SkUiColors.DefaultBackground), _clearPressed, IsEnabled));
        DrawMessage(canvas, width, height, paint);
    }

    /// <summary>The message under the field: the error color (<see cref="ErrorColor"/>) with an error (also for helper text), else the muted color; dimmed when disabled.</summary>
    private void DrawMessage(SKCanvas canvas, float width, float height, SKPaint paint)
    {
        if (FieldArea is not null || Message is not { } message)
            return;
        var color = ToSkColor(HasError ? ErrorColor ?? SkUiColors.Error : SkUiColors.Muted);
        paint.Color = IsEnabled ? color : color.WithAlpha((byte)(color.Alpha * 0.5f));
        var padding = MessagePadding;
        var top = Area(width, height).Bottom;
        MessageLayout.Draw(canvas, message, MessageStyle, new Thickness(padding.Left, top + padding.Top, padding.Right, 0), width, height,
            TextAlignment.Start, TextAlignment.Start, paint);
    }

    /// <summary>Whether the selection shows its handles: made by touch, while focused.</summary>
    private bool HandlesShown => _touchSelection && _owner.IsFocused && Editor.HasSelection && IsEnabled;

    /// <summary>Whether the caret shows its handle: after a tap, for <see cref="SkUiLook.TextInsertionHandleTimeout"/> or while dragged.</summary>
    private bool InsertionHandleShown => _insertionHandle && _owner.IsFocused && !Editor.HasSelection && IsEnabled && !Editor.IsReadOnly;

    /// <summary>
    /// What the field draws outside its bounds (the handles hang there, and a message that does not fit under a field of fixed
    /// height): positive amounts per side (<see cref="SkUiRenderProps.Overflow"/>); <c>null</c>: nothing. The node then stops
    /// clipping to its bounds (the field clips its text itself).
    /// </summary>
    internal SKRect? GetOverflow(float width, float height)
    {
        var bounds = new SKRect(0, 0, width, height);
        var look = SkUiLook.Current;
        if (HandlesShown)
        {
            var (start, end) = HandleEdges(width, height);
            bounds = SKRect.Union(look.GetTextSelectionHandleBounds(start, isStart: true), look.GetTextSelectionHandleBounds(end, isStart: false));
        }
        else if (InsertionHandleShown)
        {
            bounds = look.GetTextInsertionHandleBounds(CaretEdge(width, height));
        }
        if (FieldArea is null && MessageHeight(width) is > 0 and var message)
            bounds.Bottom = Math.Max(bounds.Bottom, Area(width, height).Bottom + message);
        var overflow = new SKRect(Math.Max(0, -bounds.Left), Math.Max(0, -bounds.Top), Math.Max(0, bounds.Right - width), Math.Max(0, bounds.Bottom - height));
        return overflow is { Left: <= 0, Top: <= 0, Right: <= 0, Bottom: <= 0 } ? null : overflow;
    }

    /// <summary>
    /// The field's composite-time props: the caret drawable and, while handles hang outside the field, room for them (the node
    /// then draws unclipped; the field clips its text itself).
    /// </summary>
    internal void GetRenderProps(ref SkUiRenderProps props)
    {
        props.Drawable = GetCaretDrawable(props.Width, props.Height);
        if (GetOverflow(props.Width, props.Height) is not { } overflow)
            return;
        props.ClipToBounds = false;
        var current = props.Overflow;
        props.Overflow = new SKRect(Math.Max(current.Left, overflow.Left), Math.Max(current.Top, overflow.Top),
            Math.Max(current.Right, overflow.Right), Math.Max(current.Bottom, overflow.Bottom));
    }

    /// <summary>The selection's edges in field coordinates: its start and end carets (the end kept on its line at a wrap).</summary>
    private (SKRect Start, SKRect End) HandleEdges(float width, float height)
    {
        var box = TextBox(width, height);
        EnsureLayout(box.Width);
        var origin = TextOrigin(box);
        var start = _layout.CaretRect(ToDisplay(Editor.SelectionStart));
        var end = _layout.CaretRect(ToDisplay(Editor.SelectionEnd), upstream: true);
        start.Offset(origin.X, origin.Y);
        end.Offset(origin.X, origin.Y);
        return (start, end);
    }

    /// <summary>The caret's middle in field coordinates.</summary>
    internal Point CaretPoint()
    {
        var (width, height) = Size;
        var caret = CaretEdge(width, height);
        return new Point(caret.MidX, caret.MidY);
    }

    /// <summary>The caret's rectangle in field coordinates (zero width).</summary>
    private SKRect CaretEdge(float width, float height)
    {
        var box = TextBox(width, height);
        EnsureLayout(box.Width);
        var origin = TextOrigin(box);
        var caret = _layout.CaretRect(ToDisplay(Editor.Caret), Editor.CaretUpstream);
        caret.Offset(origin.X, origin.Y);
        return caret;
    }

    // Whether an edge is in the visible text (a handle of a scrolled-out end is not drawn or grabbed).
    private bool InView(SKRect edge, float width, float height)
    {
        var box = TextBox(width, height);
        return edge.MidX >= box.Left - 1 && edge.MidX <= box.Right + 1 && edge.Bottom >= box.Top - 1 && edge.Top <= box.Bottom + 1;
    }

    private void DrawHandles(SKCanvas canvas, float width, float height)
    {
        var (start, end) = HandleEdges(width, height);
        var color = ToSkColor(CaretColor ?? SkUiLook.Current.DefaultCaretColor);
        if (InView(start, width, height))
            SkUiLook.Current.DrawTextSelectionHandle(canvas, new SkUiTextSelectionHandlePaint { Edge = start, IsStart = true, Color = color });
        if (InView(end, width, height))
            SkUiLook.Current.DrawTextSelectionHandle(canvas, new SkUiTextSelectionHandlePaint { Edge = end, IsStart = false, Color = color });
    }

    private void DrawInsertionHandle(SKCanvas canvas, float width, float height)
    {
        var caret = CaretEdge(width, height);
        if (InView(caret, width, height))
            SkUiLook.Current.DrawTextInsertionHandle(canvas,
                new SkUiTextInsertionHandlePaint { Caret = caret, Color = ToSkColor(CaretColor ?? SkUiLook.Current.DefaultCaretColor) });
    }

    /// <summary>
    /// The handle a touch at <paramref name="local"/> grabs: -1 (the selection's start), +1 (its end), <see cref="CaretHandle"/>
    /// (the insertion handle under the caret) or 0 (none).
    /// </summary>
    internal int HandleAt(Point local)
    {
        var (width, height) = Size;
        var look = SkUiLook.Current;
        var radius = look.TextSelectionHandleTouchRadius;
        if (HandlesShown)
        {
            var (start, end) = HandleEdges(width, height);
            var toStart = InView(start, width, height) ? Distance(look.GetTextSelectionHandleBounds(start, isStart: true), local) : double.MaxValue;
            var toEnd = InView(end, width, height) ? Distance(look.GetTextSelectionHandleBounds(end, isStart: false), local) : double.MaxValue;
            if (Math.Min(toStart, toEnd) > radius)
                return 0;
            return toStart <= toEnd ? -1 : 1;
        }
        if (!_owner.IsFocused || Editor.HasSelection || Editor.IsReadOnly || !IsEnabled)
            return 0;
        var caret = CaretEdge(width, height);
        if (!InView(caret, width, height))
            return 0;
        return InsertionHandleShown && Distance(look.GetTextInsertionHandleBounds(caret), local) <= radius ? CaretHandle : 0;
    }

    /// <summary>
    /// Whether a finger at <paramref name="local"/> that moves drags the caret itself, as on iOS (where the look shows no insertion
    /// handle): near the caret of a focused field. A tap there stays a tap.
    /// </summary>
    internal bool GrabsCaret(Point local)
    {
        if (SkUiLook.Current.ShowsTextInsertionHandle || !_owner.IsFocused || Editor.HasSelection || Editor.IsReadOnly || !IsEnabled)
            return false;
        var (width, height) = Size;
        var caret = CaretEdge(width, height);
        return InView(caret, width, height) && Distance(caret, local) <= SkUiLook.Current.TextSelectionHandleTouchRadius / 2;
    }

    /// <summary><see cref="HandleAt"/>'s value for the caret.</summary>
    internal const int CaretHandle = 2;

    // From a point to a rectangle (0 inside it).
    private static double Distance(SKRect rect, Point point)
    {
        var dx = point.X < rect.Left ? rect.Left - point.X : point.X > rect.Right ? point.X - rect.Right : 0;
        var dy = point.Y < rect.Top ? rect.Top - point.Y : point.Y > rect.Bottom ? point.Y - rect.Bottom : 0;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    // A handle drag: the selection's other end stays; the edge keeps its distance from the finger (the finger does not cover it).
    private int _handleAnchor;
    private int _draggedHandle;
    private Point _dragOffset;

    internal void BeginHandleDrag(int handle, Point local)
    {
        _draggedHandle = handle;
        var (width, height) = Size;
        SKRect edge;
        if (handle == CaretHandle)
        {
            edge = CaretEdge(width, height);
            _insertionHandleTimer?.Dispose();
            _insertionHandleTimer = null;
        }
        else
        {
            var (start, end) = HandleEdges(width, height);
            edge = handle < 0 ? start : end;
            _handleAnchor = handle < 0 ? Editor.SelectionEnd : Editor.SelectionStart;
        }
        _dragOffset = new Point(edge.MidX - local.X, edge.MidY - local.Y);
        HideEditMenu();
        ShowMagnifier();
    }

    internal void DragHandle(Point local)
    {
        var (index, upstream) = CaretAt(new Point(local.X + _dragOffset.X, local.Y + _dragOffset.Y));
        _settingTouchSelection = true;
        try
        {
            if (_draggedHandle == CaretHandle)
            {
                Editor.Select(index, index, upstream);
            }
            else
            {
                // Never empty: a handle dragged onto the other end keeps one grapheme selected.
                if (index == _handleAnchor)
                    index = index > 0 && index == Editor.Text.Length ? SkUiTextBoundaries.PreviousGrapheme(Editor.Text, index) : SkUiTextBoundaries.NextGrapheme(Editor.Text, index);
                Editor.Select(_handleAnchor, index, upstream);
            }
        }
        finally
        {
            _settingTouchSelection = false;
        }
        ShowMagnifier();
    }

    /// <summary>The finger lifted from a handle; <paramref name="moved"/>: it dragged (else it tapped the handle).</summary>
    internal void EndHandleDrag(bool moved)
    {
        HideMagnifier();
        if (_draggedHandle != CaretHandle)
        {
            ShowEditMenuAtSelection();
        }
        else if (!moved)
        {
            // A tap on the caret or its handle toggles the menu (paste), as native fields.
            if (_menuShown)
                HideEditMenu();
            else
                ShowEditMenuAtSelection();
        }
        if (_draggedHandle == CaretHandle && _insertionHandle)
            ShowInsertionHandle();
        _draggedHandle = 0;
        if (_owner.IsFocused)
            RestartBlink();
    }

    /// <summary>Shows the insertion handle (where the look has one) and starts its timeout.</summary>
    private void ShowInsertionHandle()
    {
        var look = SkUiLook.Current;
        if (!look.ShowsTextInsertionHandle || Editor.IsReadOnly)
            return;
        _insertionHandleTimer?.Dispose();
        _insertionHandleTimer = SkUiGestureSettings.Schedule(look.TextInsertionHandleTimeout, () =>
        {
            _insertionHandleTimer = null;
            if (_draggedHandle != CaretHandle)
                HideInsertionHandle();
        });
        if (_insertionHandle)
            return;
        _insertionHandle = true;
        HandlesChanged();
    }

    private void HideInsertionHandle()
    {
        _insertionHandleTimer?.Dispose();
        _insertionHandleTimer = null;
        if (!_insertionHandle)
            return;
        _insertionHandle = false;
        HandlesChanged();
    }

    // Handles drawn outside the field: the content and its overflow (render props) change.
    private void HandlesChanged()
    {
        _owner.InvalidatePaint();
        _owner.InvalidateCaret();
    }

    // The handle under the caret after a tap (Android's); its timer hides it.
    private bool _insertionHandle;
    private IDisposable? _insertionHandleTimer;

    private void ShowMagnifier()
    {
        if (_proxy is null)
            return;
        var index = _draggedHandle switch
        {
            < 0 => Editor.SelectionStart,
            > 0 and not CaretHandle => Editor.SelectionEnd,
            _ => Editor.Caret
        };
        _proxy.ShowMagnifier(this, GetCaretBounds(index));
        _magnifierShown = true;
    }

    private void HideMagnifier()
    {
        if (!_magnifierShown)
            return;
        _magnifierShown = false;
        _proxy?.HideMagnifier(this);
    }

    private bool _magnifierShown;

    #region Edit menu

    // The selection was made by touch (it shows handles, and its menu); set while touch input moves it.
    private bool _touchSelection;
    private bool _settingTouchSelection;
    private bool _menuShown;

    private void SelectByTouch(Action select)
    {
        HideInsertionHandle();
        _settingTouchSelection = true;
        try
        {
            select();
        }
        finally
        {
            _settingTouchSelection = false;
        }
        _touchSelection = true;
        _owner.InvalidatePaint();
    }

    /// <summary>The platform's edit menu at the selection (or the caret).</summary>
    private void ShowEditMenuAtSelection()
    {
        if (_proxy is null || !_owner.IsFocused)
            return;
        var rects = GetRangeBounds(Editor.SelectionStart, Editor.SelectionEnd);
        var anchor = rects.Count > 0 ? rects[0] : GetCaretBounds(Editor.Caret);
        for (var index = 1; index < rects.Count; index++)
            anchor = anchor.Union(rects[index]);
        ShowEditMenu(anchor, byTouch: true);
    }

    private void ShowEditMenu(Rect anchor, bool byTouch)
    {
        if (_proxy is null)
            return;
        _menuShown = true;
        _proxy.ShowEditMenu(this, anchor, byTouch);
    }

    private void HideEditMenu()
    {
        if (!_menuShown)
            return;
        _menuShown = false;
        _proxy?.HideEditMenu(this);
    }

    /// <summary>Whether the field asked for the edit menu and has not hidden it since (tests).</summary>
    internal bool IsEditMenuShown => _menuShown;

    /// <summary>A secondary (right) click: the caret moves there unless it is in the selection, and the edit menu opens at the point.</summary>
    internal void SecondaryClick(Point local)
    {
        Focus();
        var (index, upstream) = CaretAt(local);
        if (!Editor.HasSelection || index < Editor.SelectionStart || index > Editor.SelectionEnd)
            Editor.Select(index, index, upstream);
        var field = GetFieldBounds();
        ShowEditMenu(new Rect(field.X + local.X, field.Y + local.Y, 0, 0), byTouch: false);
    }

    #endregion

    /// <summary>The input method's marked text: underlined in the text color, as native fields mark it.</summary>
    private void DrawComposition(SKCanvas canvas, SKPoint origin, SKPaint paint)
    {
        _rects.Clear();
        _layout.AddRangeRects(ToDisplay(Editor.CompositionStart), ToDisplay(Editor.CompositionEnd), _rects);
        var thickness = Math.Max(1, (float)(TextStyle.FontSize / 14));
        foreach (var rect in _rects)
            canvas.DrawRect(rect.Left + origin.X, rect.Bottom + origin.Y - thickness - 1, rect.Width, thickness, paint);
    }

    /// <summary>
    /// The caret drawable while it shows (focused, editable, no selection, in its blink's on phase): a bar at the caret, clipped
    /// to the text box. Composite-time: moving or blinking it records nothing.
    /// </summary>
    public SkUiRenderDrawable? GetCaretDrawable(float width, float height)
    {
        if (!_owner.IsFocused || !IsEnabled || Editor.IsReadOnly || Editor.HasSelection || !_caretShown)
            return null;
        var box = TextBox(width, height);
        EnsureLayout(box.Width);
        KeepCaretInView(box);
        var origin = TextOrigin(box);
        var look = SkUiLook.Current;
        var caretWidth = (float)look.CaretWidth;
        var caret = _layout.CaretRect(ToDisplay(Editor.Caret), Editor.CaretUpstream);
        // Centered on the caret position, kept inside the box at its edges.
        var left = caret.Left + origin.X - caretWidth / 2;
        left = Math.Clamp(left, box.Left, Math.Max(box.Left, box.Right - caretWidth));
        var rect = new SKRect(left, caret.Top + origin.Y, left + caretWidth, caret.Bottom + origin.Y);
        var color = ToSkColor(CaretColor ?? look.DefaultCaretColor);
        var clip = box;
        clip.Inflate(caretWidth, 0);
        return _caret.Get(new CaretKey(rect, clip, color), (rect, clip, color), static state => new SkUiCaretDrawable(state.rect, state.clip, state.color));
    }

    /// <summary>The field's drawing resources go (it left its surface): text blobs, the caret drawable (owned by the compositor).</summary>
    public void ReleaseDrawingResources()
    {
        _layout.Release();
        _placeholderLayout?.Release();
        _messageLayout?.Release();
        Steps?.ReleaseDrawingResources();
        _layoutDirty = true;
        _caret.Forget();
        // The field left its surface: that surface's autofill lets it go.
        if (AutofillHost is { } autofill)
        {
            AutofillHost = null;
            autofill.Removed(this);
        }
    }

    /// <summary>Forgets the committed caret drawable (the node was reset; the compositor disposes it).</summary>
    public void ForgetCaret() => _caret.Forget();

    private static SKColor ToSkColor(Color color) => SkUiToggleDrawing.ToSkColor(color);

    #endregion

    #region Focus and caret blink

    /// <summary>The field gained or lost keyboard focus.</summary>
    public void SetFocused(bool focused)
    {
        if (Math.Abs(_focus.Target - (focused ? 1f : 0f)) > 0)
            _focus.AnimateTo(focused ? 1f : 0f, SkUiLook.Current.GetTransition(SkUiTransitionKind.FieldFocus));
        if (focused)
        {
            RestartBlink();
        }
        else
        {
            StopBlink();
            HideEditMenu();
            HideMagnifier();
            HideInsertionHandle();
            _touchSelection = false;
            Editor.FinishComposing();
            Editor.BreakUndoCoalescing();
            _gestures.Cancel();
        }
        // The clear button shows only while focused: the text box changes.
        _owner.InvalidatePaint();
        _owner.InvalidateCaret();
        _owner.InvalidateSemantics();
    }

    /// <summary>Shows the caret solid and starts its blink again (after typing or moving it).</summary>
    private void RestartBlink()
    {
        _blink?.Dispose();
        _blink = null;
        var changed = !_caretShown;
        _caretShown = true;
        if (changed)
            _owner.InvalidateCaret();
        var interval = SkUiLook.Current.CaretBlinkInterval;
        if (_owner.IsFocused && interval > TimeSpan.Zero)
            _blink = SkUiGestureSettings.Schedule(interval, Blink);
    }

    private void Blink()
    {
        _blink = null;
        if (!_owner.IsFocused)
            return;
        // A finger moving the caret: it stays solid, as native carets do.
        _caretShown = !_caretShown || _draggedHandle == CaretHandle;
        _owner.InvalidateCaret();
        var interval = SkUiLook.Current.CaretBlinkInterval;
        if (interval > TimeSpan.Zero)
            _blink = SkUiGestureSettings.Schedule(interval, Blink);
    }

    private void StopBlink()
    {
        _blink?.Dispose();
        _blink = null;
        _caretShown = true;
    }

    /// <summary>Whether the caret is in its blink's on phase (tests).</summary>
    internal bool IsCaretShown => _caretShown;

    #endregion

    #region Editor owner

    void ISkUiTextEditorOwner.OnEditorTextChanged(string oldText, bool byUser)
    {
        _manualScroll = false;
        _touchSelection = false;
        HideEditMenu();
        HideInsertionHandle();
        _layoutDirty = true;
        if (byUser)
            _owner.TextEdited(Editor.Text);
        if (_fromInput == 0)
            _proxy?.StateChanged(this, textChanged: true);
        AutofillHost?.TextChanged(this);
        // An entry is as wide as its text; an editor follows its text only with AutoSize.
        if (!IsMultiline || AutoSize)
            _owner.InvalidateMeasure();
        _owner.InvalidatePaint();
        _owner.InvalidateSemantics();
        _owner.InvalidateCaret();
        if (_owner.IsFocused)
            RestartBlink();
    }

    void ISkUiTextEditorOwner.OnEditorSelectionChanged()
    {
        _manualScroll = false;
        if (!_settingTouchSelection)
        {
            _touchSelection = false;
            HideEditMenu();
            HideInsertionHandle();
        }
        else if (_insertionHandle && _draggedHandle == CaretHandle)
        {
            // The dragged handle follows the caret.
            HandlesChanged();
        }
        _owner.SelectionEdited(Editor.SelectionStart, Editor.SelectionLength);
        if (_fromInput == 0)
            _proxy?.StateChanged(this, textChanged: false);
        _owner.InvalidatePaint();
        _owner.InvalidateCaret();
        _proxy?.GeometryChanged(this);
        if (_owner.IsFocused)
            RestartBlink();
    }

    #endregion

    #region Keys

    private static bool IsApple => OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst();

    /// <summary>
    /// A key while the field has focus: caret movement and selection (arrows, Home / End, Page Up / Down, with Shift and the
    /// platform's word and line modifiers), Backspace / Delete (by word), Return, the clipboard and undo shortcuts, and typed
    /// characters when the platform's input does not deliver them itself.
    /// </summary>
    public bool OnKeyDown(in SkUiKeyEvent key)
    {
        if (!IsEnabled)
            return false;
        var modifiers = key.Modifiers;
        var shift = (modifiers & SkUiKeyModifiers.Shift) != 0;
        var command = (modifiers & (IsApple ? SkUiKeyModifiers.Command : SkUiKeyModifiers.Control)) != 0;
        var word = (modifiers & (IsApple ? SkUiKeyModifiers.Alt : SkUiKeyModifiers.Control)) != 0;
        var lineModifier = IsApple && (modifiers & SkUiKeyModifiers.Command) != 0;
        // A numeric entry's steps (Up / Down, Page Up / Down) and Escape (not while an input method composes).
        if (Steps is { } steps && !(key.Key == SkUiKey.Escape && Editor.HasComposition) && steps.OnKeyDown(key))
            return true;
        switch (key.Key)
        {
            case SkUiKey.Left or SkUiKey.Right:
            {
                var right = key.Key == SkUiKey.Right;
                var move = lineModifier ? (right ? SkUiTextMove.LineEnd : SkUiTextMove.LineStart)
                    : word ? (right ? SkUiTextMove.WordRight : SkUiTextMove.WordLeft)
                    : right ? SkUiTextMove.Right : SkUiTextMove.Left;
                if (lineModifier && IsRightToLeftAt(Editor.Caret))
                    move = move == SkUiTextMove.LineEnd ? SkUiTextMove.LineStart : SkUiTextMove.LineEnd;
                Editor.Move(move, shift, this);
                return true;
            }
            case SkUiKey.Up or SkUiKey.Down:
            {
                var down = key.Key == SkUiKey.Down;
                if (lineModifier)
                    Editor.Move(down ? SkUiTextMove.DocumentEnd : SkUiTextMove.DocumentStart, shift, this);
                else if (IsMultiline)
                    Editor.Move(down ? SkUiTextMove.Down : SkUiTextMove.Up, shift, this);
                else if (IsApple)
                    // A single-line Mac field: Up / Down go to its start / end.
                    Editor.Move(down ? SkUiTextMove.DocumentEnd : SkUiTextMove.DocumentStart, shift, this);
                else
                    return false;
                return true;
            }
            case SkUiKey.Home or SkUiKey.End:
            {
                var end = key.Key == SkUiKey.End;
                var document = (modifiers & SkUiKeyModifiers.Control) != 0 || (IsApple && !IsMultiline);
                Editor.Move(document ? (end ? SkUiTextMove.DocumentEnd : SkUiTextMove.DocumentStart) : end ? SkUiTextMove.LineEnd : SkUiTextMove.LineStart,
                    shift, this);
                return true;
            }
            case SkUiKey.PageUp or SkUiKey.PageDown when IsMultiline:
                Editor.Move(key.Key == SkUiKey.PageDown ? SkUiTextMove.PageDown : SkUiTextMove.PageUp, shift, this);
                return true;
            case SkUiKey.Backspace:
                if (!Editor.IsReadOnly)
                    Editor.DeleteBackward(word: word && !lineModifier, toLineStart: lineModifier, this);
                return true;
            case SkUiKey.Delete:
                if (!Editor.IsReadOnly)
                    Editor.DeleteForward(word);
                return true;
            case SkUiKey.Enter when !command:
                if (IsMultiline)
                {
                    if (!Editor.IsReadOnly)
                        TypeText("\n", SkUiTextEditKind.Other);
                }
                else
                {
                    PerformReturn();
                }
                return true;
            case SkUiKey.Escape when Editor.HasComposition:
                Editor.CancelComposition();
                return true;
            case SkUiKey.Space when !key.HasCommandModifier && !(_proxy?.ReceivesCharacters ?? false):
                if (!Editor.IsReadOnly)
                    TypeText(" ");
                return true;
            case SkUiKey.Character when command:
                return Shortcut(char.ToLowerInvariant(key.Character), shift);
            case SkUiKey.Character when !key.HasCommandModifier:
                // The platform's input method delivers typed characters itself where it receives them.
                if (_proxy?.ReceivesCharacters ?? false)
                    return false;
                if (!Editor.IsReadOnly && key.Character != '\0')
                    TypeText(key.Character.ToString());
                return true;
        }
        return false;
    }

    private bool Shortcut(char character, bool shift)
    {
        var action = character switch
        {
            'a' => SkUiTextEditAction.SelectAll,
            'c' => SkUiTextEditAction.Copy,
            'x' => SkUiTextEditAction.Cut,
            'v' => SkUiTextEditAction.Paste,
            'z' => shift ? SkUiTextEditAction.Redo : SkUiTextEditAction.Undo,
            'y' when !IsApple => SkUiTextEditAction.Redo,
            _ => (SkUiTextEditAction?)null
        };
        if (action is not { } value)
            return false;
        PerformEditAction(value);
        return true;
    }

    #endregion

    #region Pointer

    /// <summary>The field's size from the owner.</summary>
    private (float Width, float Height) Size => ((float)_owner.FieldSize.Width, (float)_owner.FieldSize.Height);

    /// <summary>A point in field coordinates in the layout's (text box) coordinates.</summary>
    private SKPoint ToText(Point local)
    {
        var (width, height) = Size;
        var box = TextBox(width, height);
        EnsureLayout(box.Width);
        var origin = TextOrigin(box);
        return new SKPoint((float)local.X - origin.X, (float)local.Y - origin.Y);
    }

    private (int Index, bool Upstream) CaretAt(Point local)
    {
        var (index, upstream) = _layout.HitTest(ToText(local));
        return (ToSource(index), upstream);
    }

    private int CharacterAt(Point local) => ToSource(_layout.CharacterAt(ToText(local)));

    /// <summary>Whether <paramref name="local"/> is on the shown clear button.</summary>
    internal bool HitsClearButton(Point local)
    {
        var (width, height) = Size;
        return ClearButtonShown && ClearButtonBounds(width, height).Contains((float)local.X, (float)local.Y);
    }

    /// <summary>
    /// Whether the field reacts to a pointer pressed at <paramref name="local"/> (node coordinates): enabled, inside its area (not on
    /// the message under it), and not on a leading or trailing view that handles the pointer itself (a press on an icon or a label
    /// focuses the field).
    /// </summary>
    internal bool AcceptsPointerAt(Point local)
    {
        if (!IsEnabled || _owner.AdornmentTakesPointer(local) || Steps?.ButtonAt(local) is -1 or 1)
            return false;
        if (FieldArea is null && Message is null)
            return true;
        var (width, height) = Size;
        return Area(width, height).Contains((float)local.X, (float)local.Y);
    }

    // Pointer selection: what a drag extends by (characters, words, paragraphs) from the range the press selected.
    private int _dragUnit;
    private (int Start, int End) _dragOrigin;

    /// <summary>A mouse (or pen) press: focuses, places the caret (1 click), selects a word (2) or a paragraph (3).</summary>
    internal void MousePressed(Point local, int clicks)
    {
        HideInsertionHandle();
        if (HitsClearButton(local))
        {
            SetClearPressed(true);
            return;
        }
        Focus();
        _dragUnit = Math.Clamp(clicks, 1, 3);
        if (_dragUnit == 1)
        {
            var (index, upstream) = CaretAt(local);
            Editor.Select(index, index, upstream);
            _dragOrigin = (index, index);
        }
        else
        {
            SelectUnitAt(local, _dragUnit);
            _dragOrigin = (Editor.SelectionStart, Editor.SelectionEnd);
        }
    }

    /// <summary>A mouse drag after a press: the selection extends from where the press was, by its unit.</summary>
    internal void MouseDragged(Point local)
    {
        if (_clearPressed)
        {
            SetClearPressed(HitsClearButton(local));
            return;
        }
        if (_dragUnit == 0)
            return;
        var (index, upstream) = CaretAt(local);
        if (_dragUnit == 1)
        {
            Editor.Select(_dragOrigin.Start, index, upstream);
            return;
        }
        var (start, end) = _dragUnit == 2 ? SkUiTextBoundaries.WordAt(Editor.Text, CharacterAt(local)) : SkUiTextBoundaries.ParagraphAt(Editor.Text, index);
        if (start < _dragOrigin.Start)
            Editor.Select(_dragOrigin.End, start);
        else
            Editor.Select(_dragOrigin.Start, Math.Max(end, _dragOrigin.End));
    }

    /// <summary>The mouse was released (or the drag cancelled).</summary>
    internal void MouseReleased(Point local, bool cancelled)
    {
        if (_clearPressed)
        {
            SetClearPressed(false);
            if (!cancelled && HitsClearButton(local))
                Clear();
        }
        _dragUnit = 0;
    }

    /// <summary>A tap (touch): focuses and shows the keyboard, places the caret (1 tap) or selects a word (2 taps); the clear button clears.</summary>
    internal void Tapped(Point local, int taps)
    {
        if (HitsClearButton(local))
        {
            Clear();
            return;
        }
        var wasFocused = _owner.IsFocused;
        Focus();
        // A tap asks for the keyboard (also the one that focuses: Android's focus alone asks implicitly, which an input method may
        // decline); the proxies ignore it while the keyboard shows.
        _proxy?.ShowKeyboard(this);
        // A numeric entry keeps everything selected when a tap focuses it: typing replaces the value.
        if (!wasFocused && SelectsAllOnFocusTap && _owner.IsFocused && Editor.HasSelection)
            return;
        if (taps >= 2)
        {
            SelectByTouch(() => SelectUnitAt(local, 2));
            ShowEditMenuAtSelection();
            return;
        }
        var (index, upstream) = CaretAt(local);
        // A tap on the caret of a focused field toggles the edit menu (paste), as native fields.
        if (wasFocused && !Editor.HasSelection && index == Editor.Caret)
        {
            if (_menuShown)
                HideEditMenu();
            else
                ShowEditMenuAtSelection();
            ShowInsertionHandle();
            return;
        }
        Editor.Select(index, index, upstream);
        ShowInsertionHandle();
    }

    // The word a long press selected: a drag after it extends the selection by words from it.
    private (int Start, int End) _pressedWord;

    /// <summary>A finger moving after a long press: the selection grows by words from the pressed word, as native fields.</summary>
    internal void TouchDragged(Point local)
    {
        var (start, end) = SkUiTextBoundaries.WordAt(Editor.Text, CharacterAt(local));
        SelectByTouch(() =>
        {
            if (start < _pressedWord.Start)
                Editor.Select(_pressedWord.End, start);
            else
                Editor.Select(_pressedWord.Start, Math.Max(end, _pressedWord.End));
        });
        ShowMagnifier();
    }

    /// <summary>A long press (touch): selects the word under it.</summary>
    internal void LongPressed(Point local)
    {
        Focus();
        SelectByTouch(() => SelectUnitAt(local, 2));
        _pressedWord = (Editor.SelectionStart, Editor.SelectionEnd);
        ShowMagnifier();
    }

    /// <summary>The finger lifted after a long press: the edit menu opens at the selection.</summary>
    internal void LongPressEnded()
    {
        HideMagnifier();
        ShowEditMenuAtSelection();
    }

    /// <summary>A long press was cancelled (the system took the touch): the magnifier goes, the selection stays, no menu.</summary>
    internal void CancelLongPress() => HideMagnifier();

    private void SelectUnitAt(Point local, int unit)
    {
        if (unit == 2)
            Editor.SelectWordAt(CharacterAt(local));
        else
            Editor.SelectParagraphAt(CaretAt(local).Index);
    }

    private void SetClearPressed(bool pressed)
    {
        if (_clearPressed == pressed)
            return;
        _clearPressed = pressed;
        _owner.InvalidatePaint();
    }

    private void Focus()
    {
        if (!_owner.IsFocused)
            _owner.RequestFocus();
    }

    /// <summary>The clear button: the text goes (one undo step), the field keeps focus.</summary>
    internal void Clear()
    {
        if (Editor.Text.Length == 0 || Editor.IsReadOnly)
            return;
        Editor.FinishComposing();
        Editor.Replace(0, Editor.Text.Length, string.Empty);
    }

    #endregion

    #region Scrolling inside an editor

    /// <summary>How far the text can scroll (an editor taller than its text box; DIPs).</summary>
    private float MaxScrollY
    {
        get
        {
            var (width, height) = Size;
            var box = TextBox(width, height);
            EnsureLayout(box.Width);
            return Math.Max(0, _layout.Height - box.Height);
        }
    }

    /// <summary>
    /// Scrolls an editor's text by <paramref name="delta"/> DIPs (positive: towards the end); returns what it could not use. The
    /// caret may leave the view: it scrolls back when it moves.
    /// </summary>
    internal double ScrollBy(double delta)
    {
        if (!IsMultiline || delta == 0)
            return delta;
        var target = Math.Clamp(_scrollY + (float)delta, 0, MaxScrollY);
        var used = target - _scrollY;
        if (Math.Abs(used) < float.Epsilon)
            return delta;
        _scrollY = target;
        _manualScroll = true;
        _owner.InvalidatePaint();
        _owner.InvalidateCaret();
        _proxy?.GeometryChanged(this);
        return delta - used;
    }

    // The user scrolled the text: the caret is not pulled back into view until it moves.
    private bool _manualScroll;

    /// <summary>A wheel or trackpad delta (positive towards the start): an editor scrolls its text and hands on the rest.</summary>
    internal Point Wheel(double deltaX, double deltaY)
    {
        if (Steps is { } steps)
            return IsEnabled ? steps.Wheel(deltaX, deltaY, _owner.IsFocused) : new Point(deltaX, deltaY);
        if (!IsMultiline || deltaY == 0)
            return new Point(deltaX, deltaY);
        var rest = ScrollBy(-deltaY);
        return new Point(deltaX, -rest);
    }

    /// <summary>Whether an editor can scroll its text by <paramref name="delta"/> (a drag deciding between the editor and its scroller).</summary>
    internal bool CanScroll(double delta) => IsMultiline && (delta > 0 ? _scrollY < MaxScrollY - 0.5f : _scrollY > 0.5f);

    #endregion

    #region Screen readers

    /// <summary>
    /// An editable text field: the placeholder as its name, the text (bullets for a password) as its value, the message under it
    /// (helper or error text) read after them, and whether its input is invalid.
    /// </summary>
    public void PopulateSemantics(SkUiSemanticsInfo info)
    {
        info.Role = SkUiSemanticsRole.TextField;
        info.Text = Placeholder;
        info.Value = IsPassword ? new string('•', ToDisplayIndex(Editor.Text.Length)) : Editor.Text;
        info.IsEnabled &= IsEnabled;
        info.Actions |= SkUiSemanticsActions.Activate;
        var (start, end) = IsPassword ? (ToDisplayIndex(Editor.SelectionStart), ToDisplayIndex(Editor.SelectionEnd)) : (Editor.SelectionStart, Editor.SelectionEnd);
        info.TextField = new SkUiSemanticsTextField(start, end, IsPassword, Editor.IsReadOnly, IsMultiline, HasError, Message);
        Steps?.PopulateSemantics(info);
    }

    /// <summary>A screen reader's action: activation focuses the field, increment / decrement step a numeric entry.</summary>
    public bool PerformSemanticsAction(SkUiSemanticsActions action) =>
        action == SkUiSemanticsActions.Activate ? Activate() : Steps?.PerformSemanticsAction(action) ?? false;

    /// <summary>
    /// A text index as the bullets of a password count it (screen readers read the bullets). The bullets are counted again when the
    /// text changed since the last layout: the semantics tree may be built before the next frame lays the field out.
    /// </summary>
    private int ToDisplayIndex(int index)
    {
        if (_bulletStarts is null || _bulletText != Editor.Text)
            DisplayText(Editor.Text);
        return ToDisplay(index);
    }

    /// <summary>Activation (a screen reader's double tap, Space / Enter is typed instead): focuses the field.</summary>
    public bool Activate()
    {
        Focus();
        _proxy?.ShowKeyboard(this);
        return _owner.IsFocused;
    }

    #endregion

    #region Input client

    SkUiTextInputOptions ISkUiTextInputClient.Options => Options;

    /// <summary>What the platform's input is asked for.</summary>
    internal SkUiTextInputOptions Options => DefaultOptions with
    {
        IsSearch = IsSearch,
        Keyboard = Keyboard,
        ReturnType = ReturnType,
        IsPassword = IsPassword,
        IsMultiline = IsMultiline,
        // Numbers are not corrected or completed.
        IsSpellCheckEnabled = IsSpellCheckEnabled && !IsPassword && NumberKeyboard == SkUiNumberKeyboard.None,
        IsTextPredictionEnabled = IsTextPredictionEnabled && !IsPassword && NumberKeyboard == SkUiNumberKeyboard.None,
        IsReadOnly = Editor.IsReadOnly,
        TextTransform = Editor.Transform,
        AutofillHint = AutofillHint ?? (IsPassword ? SkUiAutofillHints.Password : null),
        NumberKeyboard = NumberKeyboard
    };

    ISkUiRenderable ISkUiTextInputClient.Node => _owner.Node;

    /// <summary>The proxy the field is attached to (tests).</summary>
    internal ISkUiTextInputProxy? Proxy => _proxy;

    void ISkUiTextInputClient.InputAttached(ISkUiTextInputProxy? proxy) => _proxy = proxy;

    string? ISkUiTextInputClient.Placeholder => Placeholder;

    /// <summary>The platform's autofill that knows the field (Android), or <c>null</c>.</summary>
    public ISkUiAutofillHost? AutofillHost { get; set; }

    void ISkUiTextInputClient.BeginInputBatch()
    {
        _fromInput++;
        Editor.BeginBatch();
    }

    void ISkUiTextInputClient.EndInputBatch()
    {
        try
        {
            Editor.EndBatch();
        }
        finally
        {
            _fromInput--;
        }
    }

    private void FromInput(Action<SkUiTextEditor> edit)
    {
        _fromInput++;
        try
        {
            edit(Editor);
        }
        finally
        {
            _fromInput--;
        }
    }

    void ISkUiTextInputClient.ReplaceText(int start, int end, string text, int? caret) =>
        FromInput(editor => editor.Replace(start, end, text, InputKind(start, end, text), caret));

    // What an input method's replacement is for undo: a one-character insertion at the caret is typing.
    private SkUiTextEditKind InputKind(int start, int end, string text) =>
        end == start && start == Editor.Caret && text.Length <= 2 ? SkUiTextEditKind.Typing
        : text.Length == 0 && end - start <= 2 ? SkUiTextEditKind.Deleting
        : SkUiTextEditKind.Other;

    void ISkUiTextInputClient.InsertText(string text) => FromInput(editor => editor.InsertText(text));

    void ISkUiTextInputClient.SetComposingText(string text, int? caretInText) => FromInput(editor => editor.SetComposingText(text, caretInText));

    void ISkUiTextInputClient.SetComposingRegion(int start, int end) => FromInput(editor => editor.SetComposingRegion(start, end));

    void ISkUiTextInputClient.FinishComposing() => FromInput(editor => editor.FinishComposing());

    void ISkUiTextInputClient.SetSelection(int anchor, int caret) => FromInput(editor => editor.Select(anchor, caret));

    void ISkUiTextInputClient.DeleteBackward() => FromInput(editor => editor.DeleteBackward());

    /// <summary>
    /// Return in a single-line field: <c>Completed</c> (and <c>ReturnCommand</c>), then as MAUI's native entries: Next moves focus to
    /// the next field; otherwise iOS gives focus up and Android hides the keyboard (its Done, Go, Search, Send; MAUI's Default is Done).
    /// </summary>
    public void PerformReturn()
    {
        if (IsMultiline)
        {
            if (!Editor.IsReadOnly)
                TypeText("\n", SkUiTextEditKind.Other);
            return;
        }
        Editor.FinishComposing();
        _owner.Completed();
        if (!_owner.IsFocused)
            return;
        if (ReturnType == ReturnType.Next && SkUiSemantics.RootOf(_owner.Node) is { } root && root.FocusManager.MoveFocus(forward: true))
            return;
        // A search on a phone gives focus up (the keyboard goes), as MAUI's search bars on iOS and Android.
        if (IsSearch && (OperatingSystem.IsAndroid() || (OperatingSystem.IsIOS() && !OperatingSystem.IsMacCatalyst())))
        {
            _owner.ReleaseFocus();
            return;
        }
        if (IsApple && !OperatingSystem.IsMacCatalyst())
            _owner.ReleaseFocus();
        else if (OperatingSystem.IsAndroid())
            _proxy?.HideKeyboard(this);
    }

    public bool CanPerformEditAction(SkUiTextEditAction action) => action switch
    {
        SkUiTextEditAction.Copy => Editor.HasSelection && !IsPassword,
        SkUiTextEditAction.Cut => Editor.HasSelection && !IsPassword && !Editor.IsReadOnly,
        SkUiTextEditAction.Delete => Editor.HasSelection && !Editor.IsReadOnly,
        SkUiTextEditAction.Paste => !Editor.IsReadOnly && SafeHasText(),
        SkUiTextEditAction.SelectAll => Editor.Text.Length > 0 && Editor.SelectionLength < Editor.Text.Length,
        SkUiTextEditAction.Undo => Editor.CanUndo,
        SkUiTextEditAction.Redo => Editor.CanRedo,
        _ => false
    };

    private static bool SafeHasText()
    {
        try
        {
            return SkUiTextClipboard.HasText();
        }
        catch (Exception)
        {
            // No clipboard on this platform (headless): nothing to paste.
            return false;
        }
    }

    /// <summary>Runs an edit menu action or shortcut (cut, copy and paste use the system clipboard).</summary>
    public void PerformEditAction(SkUiTextEditAction action)
    {
        switch (action)
        {
            case SkUiTextEditAction.SelectAll:
                Editor.SelectAll();
                break;
            case SkUiTextEditAction.Undo:
                Editor.Undo();
                break;
            case SkUiTextEditAction.Redo:
                Editor.Redo();
                break;
            case SkUiTextEditAction.Delete when !Editor.IsReadOnly:
                Editor.DeleteForward();
                break;
            case SkUiTextEditAction.Copy when CanPerformEditAction(SkUiTextEditAction.Copy):
                _ = SetClipboardAsync(Editor.SelectedText);
                break;
            case SkUiTextEditAction.Cut when CanPerformEditAction(SkUiTextEditAction.Cut):
                _ = SetClipboardAsync(Editor.SelectedText);
                Editor.Replace(Editor.SelectionStart, Editor.SelectionEnd, string.Empty);
                break;
            case SkUiTextEditAction.Paste when !Editor.IsReadOnly:
                _ = PasteAsync();
                break;
        }
    }

    private static async Task SetClipboardAsync(string text)
    {
        try
        {
            await SkUiTextClipboard.SetText(text);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.WriteLine($"SkiaUi: copying to the clipboard failed: {exception.Message}");
        }
    }

    private async Task PasteAsync()
    {
        string? text;
        try
        {
            text = await SkUiTextClipboard.GetText();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.WriteLine($"SkiaUi: reading the clipboard failed: {exception.Message}");
            return;
        }
        if (string.IsNullOrEmpty(text) || Editor.IsReadOnly || !IsEnabled)
            return;
        TypeText(text, SkUiTextEditKind.Other);
    }

    /// <summary>
    /// Text from a key, Return or a paste: it goes in at the selection and a composition the input method left is kept as typed
    /// (as a native field takes a key event while composing: Samsung Keyboard sends Enter as a key with the word still composing).
    /// <see cref="SkUiTextEditor.InsertText"/> itself replaces the composition: that is the input method committing it.
    /// </summary>
    private void TypeText(string text, SkUiTextEditKind kind = SkUiTextEditKind.Typing)
    {
        Editor.BeginBatch();
        try
        {
            Editor.FinishComposing();
            Editor.InsertText(text, kind);
        }
        finally
        {
            Editor.EndBatch();
        }
    }

    bool ISkUiTextInputClient.HandleKey(in SkUiKeyEvent key) => OnKeyDown(key);

    /// <summary>The field's origin in surface DIPs (its root bounds' top-left), and the text's origin in the field.</summary>
    private (Point Field, SKPoint Text) Origins()
    {
        var bounds = SkUiDiagnostics.GetRootBounds(_owner.Node) ?? Rect.Zero;
        var (width, height) = Size;
        var box = TextBox(width, height);
        EnsureLayout(box.Width);
        KeepCaretInView(box);
        return (bounds.Location, TextOrigin(box));
    }

    public Rect GetCaretBounds(int index)
    {
        var (field, text) = Origins();
        var caret = _layout.CaretRect(ToDisplay(index), index == Editor.Caret && Editor.CaretUpstream);
        return new Rect(field.X + text.X + caret.Left, field.Y + text.Y + caret.Top, 0, caret.Height);
    }

    public IReadOnlyList<Rect> GetRangeBounds(int start, int end)
    {
        var (field, text) = Origins();
        _rects.Clear();
        _layout.AddRangeRects(ToDisplay(start), ToDisplay(end), _rects);
        var result = new Rect[_rects.Count];
        for (var index = 0; index < _rects.Count; index++)
        {
            var rect = _rects[index];
            result[index] = new Rect(field.X + text.X + rect.Left, field.Y + text.Y + rect.Top, rect.Width, rect.Height);
        }
        return result;
    }

    public int GetIndexAt(Point surfacePoint)
    {
        var (field, _) = Origins();
        return CaretAt(new Point(surfacePoint.X - field.X, surfacePoint.Y - field.Y)).Index;
    }

    public Rect GetTextBounds()
    {
        var bounds = SkUiDiagnostics.GetRootBounds(_owner.Node) ?? Rect.Zero;
        var (width, height) = Size;
        var box = TextBox(width, height);
        return new Rect(bounds.X + box.Left, bounds.Y + box.Top, box.Width, box.Height);
    }

    public Rect GetFieldBounds() => SkUiDiagnostics.GetRootBounds(_owner.Node) ?? Rect.Zero;

    public bool SetTextFromAssistant(string text)
    {
        if (!IsEnabled || Editor.IsReadOnly)
            return false;
        Editor.FinishComposing();
        return Editor.Replace(0, Editor.Text.Length, text ?? string.Empty);
    }

    public bool SelectFromAssistant(int start, int end)
    {
        if (!IsEnabled)
            return false;
        Editor.FinishComposing();
        if (IsPassword)
        {
            // A screen reader counts the bullets.
            ToDisplayIndex(0);
            (start, end) = (ToSource(start), ToSource(end));
        }
        Editor.Select(SkUiTextBoundaries.AlignToGrapheme(Editor.Text, start), SkUiTextBoundaries.AlignToGrapheme(Editor.Text, end));
        return true;
    }

    public bool IsRightToLeftAt(int index)
    {
        if (_layout.Lines.Count == 0)
            return _owner.IsRightToLeft;
        return _layout.Lines[_layout.LineIndexOf(ToDisplay(index))].IsRightToLeft;
    }

    public (int Start, int End) GetLineAt(int index)
    {
        ISkUiTextEditGeometry geometry = this;
        index = Math.Clamp(index, 0, Editor.Text.Length);
        return (geometry.LineStart(index, upstream: false), geometry.LineEnd(index, upstream: false).Index);
    }

    public int MoveByLines(int index, int lines)
    {
        float? goalX = null;
        return ((ISkUiTextEditGeometry)this).MoveVertically(Math.Clamp(index, 0, Editor.Text.Length), upstream: false, lines, ref goalX).Index;
    }

    #endregion

    #region Geometry (caret movement across lines)

    bool ISkUiTextEditGeometry.IsRightToLeftAt(int index, bool upstream) =>
        _layout.Lines.Count == 0 ? _owner.IsRightToLeft : _layout.Lines[_layout.LineIndexOf(ToDisplay(index), upstream)].IsRightToLeft;

    int ISkUiTextEditGeometry.LineStart(int index, bool upstream) =>
        _layout.Lines.Count == 0 ? 0 : ToSource(_layout.Lines[_layout.LineIndexOf(ToDisplay(index), upstream)].Start);

    (int Index, bool Upstream) ISkUiTextEditGeometry.LineEnd(int index, bool upstream)
    {
        if (_layout.Lines.Count == 0)
            return (Editor.Text.Length, false);
        var line = _layout.Lines[_layout.LineIndexOf(ToDisplay(index), upstream)];
        return (ToSource(line.End), !line.EndsParagraph);
    }

    (int Index, bool Upstream) ISkUiTextEditGeometry.MoveVertically(int index, bool upstream, int lines, ref float? goalX)
    {
        if (_layout.Lines.Count == 0)
            return (index, upstream);
        var current = _layout.LineIndexOf(ToDisplay(index), upstream);
        goalX ??= _layout.CaretX(current, ToDisplay(index));
        var target = current + lines;
        if (target < 0)
            return (0, false);
        if (target >= _layout.Lines.Count)
            return (Editor.Text.Length, false);
        var (hit, hitUpstream) = _layout.HitTestLine(target, goalX.Value);
        return (ToSource(hit), hitUpstream);
    }

    int ISkUiTextEditGeometry.PageLines
    {
        get
        {
            var (width, height) = Size;
            var box = TextBox(width, height);
            var lineHeight = Math.Max(1, LineHeight);
            return Math.Max(1, (int)(box.Height / lineHeight) - 1);
        }
    }

    #endregion
}

/// <summary>The caret: a bar in the caret color, clipped to the field's text box. Not animated (the field's timer blinks it).</summary>
internal sealed class SkUiCaretDrawable(SKRect rect, SKRect clip, SKColor color) : SkUiRenderDrawable
{
    /// <summary>Where the caret is drawn (tests).</summary>
    internal SKRect Rect => rect;

    public override bool IsAnimated => false;

    public override void Draw(SKCanvas canvas, in SkUiRenderDrawContext context)
    {
        var paint = context.Paint;
        paint.Color = color;
        var save = canvas.Save();
        canvas.ClipRect(clip);
        canvas.DrawRect(rect, paint);
        canvas.RestoreToCount(save);
    }
}

/// <summary>
/// A text field's pointer input. A mouse or pen press claims the gesture at once: it focuses and places the caret (a double
/// click selects a word, a triple click a paragraph) and dragging selects. A touch competes like a tap, so a drag scrolls the
/// field's scroller: a tap focuses (showing the keyboard) and places the caret, a double tap selects a word, a long press
/// selects the word under it.
/// </summary>
internal sealed class SkUiTextFieldGestures(SkUiTextFieldModel model) : SkUiGestureRecognizer
{
    private long? _pointer;
    private bool _mouse;
    private bool _moved;
    private bool _longPressed;
    private Point _startLocal;
    private IDisposable? _longPress;
    private int _clicks;
    private TimeSpan? _lastReleaseTime;
    private bool _scrolling;
    private double _lastY;
    private bool _secondary;
    private int _handle;
    private bool _caretGrab;
    private bool _onCaretHandle;
    private Point _lastPress;

    /// <inheritdoc />
    protected internal override bool IsExclusive => _mouse || _longPressed || _scrolling || _handle != 0;

    /// <inheritdoc />
    protected internal override bool OnPointerPressed(SkUiPointer pointer)
    {
        if (_pointer is not null || Owner is not { } owner || !model.AcceptsPointerAt(pointer.GetPosition(owner)))
            return false;
        _pointer = pointer.Id;
        _mouse = pointer.Device != SkUiPointerDevice.Touch;
        _moved = _longPressed = _scrolling = _secondary = _caretGrab = _onCaretHandle = false;
        _handle = 0;
        _startLocal = pointer.GetPosition(owner);
        var now = pointer.Timestamp;
        // Clicks and taps in quick succession at one place count up (double, triple click). The time counts from the last release to
        // this press, as Android's double tap and the arena's: a finger holds each tap ~100 ms, so press to press is often over it.
        var repeated = _lastReleaseTime is { } last && now - last <= SkUiGestureSettings.DoubleTapTimeout
                       && Math.Abs(pointer.Position.X - _lastPress.X) <= SkUiGestureSettings.DoubleTapSlop
                       && Math.Abs(pointer.Position.Y - _lastPress.Y) <= SkUiGestureSettings.DoubleTapSlop;
        _clicks = repeated ? _clicks % 3 + 1 : 1;
        _lastPress = pointer.Position;
        if (_mouse)
        {
            Claim();
            if (pointer.Buttons.HasFlag(SkUiPointerButtons.Secondary))
            {
                _secondary = true;
                model.SecondaryClick(_startLocal);
                return true;
            }
            model.MousePressed(_startLocal, _clicks);
            return true;
        }
        var handle = model.HandleAt(_startLocal);
        if (handle is not 0 and not SkUiTextFieldModel.CaretHandle)
        {
            // A selection handle: dragged at once (not a scroll).
            _handle = handle;
            Claim();
            model.BeginHandleDrag(handle, _startLocal);
            return true;
        }
        // The caret's handle, or near the caret where there is none: moving drags the caret, a tap stays a tap (the second
        // tap of a double tap lands beside the handle the first one showed, and still selects the word).
        _onCaretHandle = handle == SkUiTextFieldModel.CaretHandle;
        _caretGrab = _onCaretHandle || model.GrabsCaret(_startLocal);
        if (_onCaretHandle)
            return true;
        var id = pointer.Id;
        _longPress = SkUiGestureSettings.Schedule(SkUiGestureSettings.LongPressDuration, () =>
        {
            if (_pointer != id || _moved)
                return;
            _longPressed = true;
            Claim();
            model.LongPressed(_startLocal);
        });
        return true;
    }

    /// <inheritdoc />
    protected internal override void OnPointerMoved(SkUiPointer pointer)
    {
        if (_pointer != pointer.Id || Owner is not { } owner)
            return;
        if (_secondary)
            return;
        if (_mouse)
        {
            model.MouseDragged(pointer.GetPosition(owner));
            return;
        }
        if (_handle != 0)
        {
            // Past the slop: a drag (before it, the finger may still tap the handle).
            if (!_moved && ExceedsSlop(pointer.TotalX, pointer.TotalY, SkUiPanAxis.Both))
                _moved = true;
            if (_moved)
                model.DragHandle(pointer.GetPosition(owner));
            return;
        }
        if (_longPressed)
        {
            // After a long press, the finger moves the caret (the loupe's drag).
            model.TouchDragged(pointer.GetPosition(owner));
            return;
        }
        if (_scrolling)
        {
            model.ScrollBy(_lastY - pointer.Position.Y);
            _lastY = pointer.Position.Y;
            return;
        }
        if (!_moved && ExceedsSlop(pointer.TotalX, pointer.TotalY, SkUiPanAxis.Both))
        {
            _moved = true;
            _longPress?.Dispose();
            if (_caretGrab)
            {
                _handle = SkUiTextFieldModel.CaretHandle;
                Claim();
                model.BeginHandleDrag(_handle, _startLocal);
                model.DragHandle(pointer.GetPosition(owner));
                return;
            }
            // A vertical drag an editor can follow scrolls its text; any other drag is its scroller's.
            if (Math.Abs(pointer.TotalY) > Math.Abs(pointer.TotalX) && model.CanScroll(-pointer.TotalY))
            {
                _scrolling = true;
                _lastY = pointer.Position.Y;
                Claim();
                return;
            }
            Resign(pointer.Id);
        }
    }

    /// <inheritdoc />
    protected internal override void OnPointerReleased(SkUiPointer pointer)
    {
        if (_pointer != pointer.Id || Owner is not { } owner)
            return;
        var local = pointer.GetPosition(owner);
        _longPress?.Dispose();
        _longPress = null;
        _pointer = null;
        // Only a tap or a click counts towards the next one (not a drag, a long press or a handle).
        _lastReleaseTime = _mouse || !(_moved || _longPressed || _handle != 0) ? pointer.Timestamp : null;
        if (_secondary)
            return;
        if (_mouse)
        {
            model.MouseReleased(local, cancelled: false);
            return;
        }
        if (_handle != 0)
        {
            model.EndHandleDrag(moved: _moved);
            return;
        }
        if (_longPressed)
        {
            model.LongPressEnded();
            return;
        }
        if (_moved)
            return;
        Claim();
        // A tap on the caret's handle (below the text) is a tap on the caret.
        model.Tapped(_onCaretHandle ? model.CaretPoint() : local, _clicks >= 2 ? 2 : 1);
    }

    /// <inheritdoc />
    protected internal override void OnRejected(long pointerId)
    {
        if (_pointer != pointerId)
            return;
        _pointer = null;
        _lastReleaseTime = null;
        _longPress?.Dispose();
        _longPress = null;
        if (_mouse)
        {
            model.MouseReleased(_startLocal, cancelled: true);
            return;
        }
        // A cancelled touch (ACTION_CANCEL, a system edge gesture) ends what it started: the magnifier goes, the caret blinks again.
        if (_handle != 0)
        {
            // As a drag: a cancelled touch on the caret does not toggle the menu; a selection handle's menu comes back.
            model.EndHandleDrag(moved: true);
            _handle = 0;
        }
        else if (_longPressed)
        {
            model.CancelLongPress();
            _longPressed = false;
        }
    }
}
