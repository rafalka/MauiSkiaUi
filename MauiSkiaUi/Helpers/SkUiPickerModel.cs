using System.Collections;
using System.Diagnostics;
using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// Whether a popup's choice waits to be accepted (<see cref="SkUiPickerUpdateMode.WhenFinished"/>): then its content shows the OK
/// and Cancel buttons, with these texts; and whether it shows a Clear button (the picker's <c>IsPopupClearButtonVisible</c>).
/// </summary>
internal readonly record struct SkUiPickerPopupChrome(bool RequiresAccept, string AcceptText, string CancelText, bool ShowsClear = false,
    string? ClearText = null)
{
    /// <summary>Whether the content has a footer of buttons.</summary>
    public bool HasButtons => RequiresAccept || ShowsClear;
}

/// <summary>The app's views around a popup's list or wheels: a header and a footer (created per opening, bound to <see cref="BindingContext"/>).</summary>
internal readonly record struct SkUiPopupExtras(DataTemplate? HeaderTemplate, DataTemplate? FooterTemplate, object? BindingContext)
{
    /// <summary>The header's view, or <c>null</c>.</summary>
    public SkUiView? CreateHeader() => Create(HeaderTemplate);

    /// <summary>The footer's view, or <c>null</c>.</summary>
    public SkUiView? CreateFooter() => Create(FooterTemplate);

    private SkUiView? Create(DataTemplate? template)
    {
        if (template?.CreateContent() is not SkUiView view)
            return null;
        view.BindingContext = BindingContext;
        return view;
    }
}

/// <summary>What a popup's content is built for: where it goes, its title, the picker's text style, its buttons and the app's views.</summary>
internal readonly record struct SkUiPickerPopupContext(SkUiPopupPlacement Placement, string? Title, bool RightToLeft, SkUiPickerRowStyle RowStyle,
    SkUiPickerPopupChrome Chrome, SkUiPopupExtras Extras = default);

/// <summary>What every request a picker creates carries besides its content and geometry.</summary>
internal readonly record struct SkUiPopupRequestOptions(string? Title, Color? OverlayColor, bool IsAnimated, SkUiPickerPopupChrome Chrome);

/// <summary>
/// The content of a picker's popup, as the popup session sees it: a drawn surface that measures itself, builds the request that
/// carries it (typed to what it chooses) and follows the picker's selection while it is open.
/// </summary>
internal interface ISkUiPickerPopupContent
{
    /// <summary>The surface handed to the presenter.</summary>
    SkUiContentView Surface { get; }

    /// <summary>Whether the content has keyboard focus.</summary>
    bool IsFocused { get; }

    /// <summary>The size the content wants (at least <paramref name="minimumWidth"/>, at most <paramref name="maximumWidth"/> wide).</summary>
    Size PreferredSize(double minimumWidth, double maximumWidth);

    /// <summary>Creates the request that carries the content (and links it back).</summary>
    SkUiPopupRequest CreateRequest(View anchor, View? platformAnchor, in SkUiPopupLayout layout, in SkUiPopupRequestOptions options);

    /// <summary>The picker's selection changed while the popup is open (<paramref name="index"/>: its selected index, for single lists).</summary>
    void SelectionChanged(int index);

    /// <summary>Which items of <paramref name="column"/> (0 for a list) can be chosen changed while the popup is open.</summary>
    void ItemStatesChanged(int column, IReadOnlyList<bool>? states);

    /// <summary>The popup has gone: lets go of the request and the items.</summary>
    void Release();
}

/// <summary>A chosen item shown as a token (a chip) in the field: its text, its index in the items, and whether its × removes it.</summary>
internal readonly record struct SkUiPickerToken(string Text, int Index, bool IsRemovable);

/// <summary>What the shared picker engine (<see cref="SkUiPickerModel"/>) needs from its control, on either layer.</summary>
internal interface ISkUiPickerOwner : ISkUiTransitionHost
{
    /// <summary>The drawn picker node (its geometry anchors the popup).</summary>
    ISkUiRenderable Node { get; }

    /// <summary>Whether the picker lays out right to left.</summary>
    bool IsRightToLeft { get; }

    /// <summary>The paragraph direction of the picker's text.</summary>
    SkUiTextDirection TextDirection { get; }

    /// <summary>Whether a popup may show now: enabled and visible.</summary>
    bool CanOpen { get; }

    /// <summary>The objects the popup's templated rows bind to (the picker's <c>ItemsSource</c>), or <c>null</c> (the item texts).</summary>
    IList? ItemsSource { get; }

    /// <summary>What the app's popup header and footer bind to (the picker's binding context; Core: none).</summary>
    object? PopupBindingContext { get; }

    /// <summary>The user chose <paramref name="index"/> (in the popup, or with an arrow key on the closed field).</summary>
    void Choose(int index);

    /// <summary>The user cleared the choice (the field's clear button, Delete, the popup's Clear button).</summary>
    void ClearChoice();

    /// <summary>The user removed the chosen item at <paramref name="index"/> (a token's ×, Backspace on the field).</summary>
    void RemoveChoice(int index);

    /// <summary>Sets the control's <c>IsOpen</c> from the popup's lifetime (raising <c>Opened</c> / <c>Closed</c>).</summary>
    void SetIsOpen(bool open);

    /// <summary>The popup had keyboard focus when it closed: the field takes it back (as a native combo box).</summary>
    void FocusField();

    /// <summary>The field's size depends on something that changed (the text, the font).</summary>
    void InvalidateMeasure();

    /// <summary>The field must be drawn again.</summary>
    void InvalidatePaint();

    /// <summary>What screen readers read changed.</summary>
    void InvalidateSemantics();
}

/// <summary>
/// The engine both pickers share (<see cref="SkUiPicker"/>, <see cref="Core.SkUiCorePicker"/>): the field's text and its
/// measure, the field drawn by the look with its states (pressed, pointer over, focused, open, disabled), keyboard and screen
/// readers, and the popup's lifetime on the app's popup system (<see cref="ISkUiPopupPresenter"/>). The controls hold the
/// properties and the selection rules of their layer and copy the values here.
/// </summary>
internal sealed class SkUiPickerModel
{
    /// <summary>The picker font size when none is set: the look's.</summary>
    internal static double DefaultFontSize => SkUiLook.Current.DefaultFontSize;

    private readonly ISkUiPickerOwner _owner;
    private readonly SkUiTextLayout _layout;
    private readonly SkUiTween _focus;
    private readonly SkUiTween _open;
    private SkUiPressAnimator? _press;
    private IReadOnlyList<string> _items = [];
    private PopupSession? _session;
    private bool _reopen;

    public SkUiPickerModel(ISkUiPickerOwner owner)
    {
        _owner = owner;
        _layout = new SkUiTextLayout(owner);
        _focus = new SkUiTween(owner);
        _open = new SkUiTween(owner);
    }

    #region State the controls copy in

    /// <summary>
    /// The item texts, in order (the control keeps the list; set again when it changes). An open popup closes: it shows the
    /// items it opened with, and a choice made there is mapped to the item it showed (<see cref="Ended"/>).
    /// </summary>
    public IReadOnlyList<string> Items
    {
        get => _items;
        set
        {
            _items = value;
            if (_session is { } session)
            {
                session.ItemsChanged = true;
                Close();
            }
            TextChanged();
        }
    }

    /// <summary>The selected index (-1: none).</summary>
    public int SelectedIndex = -1;

    private IReadOnlyList<bool>? _itemEnabled;

    /// <summary>
    /// Which items can be chosen (<c>null</c>: all; the control's <c>ItemEnabledBinding</c>), set by the control after the
    /// items; an open list follows.
    /// </summary>
    public IReadOnlyList<bool>? ItemEnabled
    {
        get => _itemEnabled;
        set
        {
            _itemEnabled = value;
            _session?.ItemStatesChanged(0, value);
        }
    }

    /// <summary>Whether the item at <paramref name="index"/> can be chosen.</summary>
    public bool IsItemEnabled(int index) => SkUiItemStates.IsEnabled(_itemEnabled, index);

    /// <summary>Which items of a column changed while a popup may be open (wheels).</summary>
    public void ColumnItemStatesChanged(int column, IReadOnlyList<bool>? states) => _session?.ItemStatesChanged(column, states);

    public string? Title;
    public Color? TitleColor;
    public string? Placeholder;
    public Color? PlaceholderColor;
    public double PopupWidth = -1;
    public double MinPopupWidth = -1;
    public double MaxPopupWidth = -1;

    /// <summary>
    /// The popup's width range in a window <paramref name="windowWidth"/> wide: <see cref="PopupWidth"/> both ways when set, else
    /// <see cref="MinPopupWidth"/> (NaN when not set: the placement's default) and <see cref="MaxPopupWidth"/> (the window when not set).
    /// </summary>
    internal (double Minimum, double Maximum) PopupWidthRange(double windowWidth)
    {
        if (PopupWidth >= 0)
        {
            var width = Math.Min(PopupWidth, windowWidth);
            return (width, width);
        }
        var maximum = MaxPopupWidth >= 0 ? Math.Min(MaxPopupWidth, windowWidth) : windowWidth;
        var minimum = MinPopupWidth >= 0 ? Math.Min(MinPopupWidth, maximum) : double.NaN;
        return (minimum, maximum);
    }
    public DataTemplate? PopupHeaderTemplate;

    /// <summary>Whether the field shows the choice as tokens (several choices).</summary>
    public bool ShowsTokens;

    /// <summary>The most lines of tokens (-1: as many as they need); the rest show as "+N".</summary>
    public int MaxTokenLines = -1;

    private IReadOnlyList<SkUiPickerToken> _tokens = [];

    /// <summary>The tokens of the choice, in the order of the items (the control sets them with the choice).</summary>
    public IReadOnlyList<SkUiPickerToken> Tokens
    {
        get => _tokens;
        set
        {
            _tokens = value;
            _tokenLayoutWidth = double.NaN;
        }
    }

    /// <summary>Whether the field draws tokens now: in that mode with something chosen.</summary>
    internal bool TokensShown => ShowsTokens && _tokens.Count > 0;
    public DataTemplate? PopupFooterTemplate;
    public bool ShowsClearButton;
    public bool ShowsPopupClearButton;
    public string? ClearText;
    public System.Windows.Input.ICommand? SelectionChangedCommand;
    public object? SelectionChangedCommandParameter;
    public Color? TextColor;
    public string? FontFamily;
    /// <summary>The font size set on the control, or NaN for the look's (<see cref="EffectiveFontSize"/>).</summary>
    public double FontSize = double.NaN;

    /// <summary>The font size to lay text out with: <see cref="FontSize"/>, or the look's.</summary>
    public double EffectiveFontSize => SkUiLook.FontSizeOrDefault(FontSize);
    public FontAttributes FontAttributes;
    public bool FontAutoScalingEnabled = true;
    public double CharacterSpacing;
    public TextAlignment HorizontalTextAlignment = TextAlignment.Start;
    public TextAlignment VerticalTextAlignment = TextAlignment.Center;
    public DataTemplate? ItemTemplate;
    public Brush? SelectionBackground;
    public double MaxPopupHeight = -1;
    public SkUiPopupPlacement PopupPlacement = SkUiPopupPlacement.Default;
    public Rect PopupBounds = Rect.Zero;
    public ISkUiPopupPresenter? PopupPresenter;
    public SkUiPickerUpdateMode UpdateMode = SkUiPickerUpdateMode.Immediately;
    public string? AcceptText;
    public string? CancelText;
    public bool IsEnabled = true;
    public bool IsPointerOver;
    public bool HasExplicitMinimumHeight;

    /// <summary>The control's <c>FieldStyle</c>: how the look draws its field; <see cref="SkUiPickerFieldStyle.Default"/>: the look's.</summary>
    public SkUiPickerFieldStyle FieldStyle;

    /// <summary>The control's own idle chrome over its style's (<c>FillColor</c>, <c>BorderColor</c>, <c>BorderWidth</c>, <c>CornerRadii</c>).</summary>
    public SkUiFieldChromeState Chrome;

    /// <summary>
    /// Whether the field shows <see cref="ValueText"/> instead of the item at <see cref="SelectedIndex"/> (multiple selection,
    /// columns); the control sets the text and calls <see cref="TextChanged"/>.
    /// </summary>
    public bool UsesValueText;

    /// <summary>
    /// The choice changed (any kind: an index, items, a column): runs the control's <c>SelectionChangedCommand</c> with its
    /// parameter when it can execute. The controls call it before their change events.
    /// </summary>
    public void RunSelectionChangedCommand()
    {
        if (SelectionChangedCommand is { } command && command.CanExecute(SelectionChangedCommandParameter))
            command.Execute(SelectionChangedCommandParameter);
    }

    /// <summary>The field's text while <see cref="UsesValueText"/>; <c>null</c>: nothing is chosen (the title shows).</summary>
    public string? ValueText;

    /// <summary>Whether the closed field changes the selection with arrow keys and typing (single lists; not for several choices).</summary>
    public bool ClosedFieldSelects = true;

    /// <summary>
    /// Builds the popup's content; <c>null</c> (default): the list of <see cref="Items"/> to choose one from. Called for each
    /// opening, with the items as they are then.
    /// </summary>
    public Func<SkUiPickerPopupContext, ISkUiPickerPopupContent>? ContentFactory;

    /// <summary>The selected item's text, or <c>null</c> without a selection (the title shows then).</summary>
    public string? SelectedText => UsesValueText ? ValueText
        : SelectedIndex >= 0 && SelectedIndex < _items.Count ? _items[SelectedIndex] ?? string.Empty : null;

    /// <summary>
    /// The text the field shows for the selected item when it differs from the item's text in the list (the control's
    /// <c>SelectedItemDisplayBinding</c>: a currency's code for its full name); <c>null</c>: the item's text. Screen readers read the item's text.
    /// </summary>
    public string? SelectedFieldText;

    /// <summary>The text the field shows for the choice: <see cref="SelectedFieldText"/>, else <see cref="SelectedText"/>; <c>null</c> without a choice.</summary>
    private string? ShownSelection => SelectedText is null ? null : SelectedFieldText ?? SelectedText;

    /// <summary>The hint the field shows while nothing is chosen: the placeholder, else the title.</summary>
    private string? PlaceholderText => Placeholder ?? Title;

    /// <summary>The text the field shows: the selection's, else the hint (else nothing).</summary>
    private string DisplayText => ShownSelection ?? PlaceholderText ?? string.Empty;

    /// <summary>The shown text, the font or the items changed: lay the text out again and measure.</summary>
    public void TextChanged()
    {
        _layout.Invalidate();
        _owner.InvalidateMeasure();
        _owner.InvalidatePaint();
        _owner.InvalidateSemantics();
    }

    /// <summary>A paint-only property changed (a color).</summary>
    public void PaintChanged() => _owner.InvalidatePaint();

    /// <summary>
    /// The selection changed: new text, and an open popup's highlight follows (its request keeps the selection it opened
    /// with).
    /// </summary>
    public void SelectionChanged()
    {
        _session?.SelectionChanged(SelectedIndex);
        TextChanged();
    }

    #endregion

    #region Field

    /// <summary>The text style of the field (and of the popup's rows without a template).</summary>
    internal SkUiTextStyle TextStyle => new(SkUiTypefaces.Resolve(FontFamily, FontAttributes), SkUiFontScaling.ScaleFontSize(EffectiveFontSize, FontAutoScalingEnabled),
        LineBreakMode.TailTruncation, null, 1, -1, CharacterSpacing, _owner.TextDirection, SkUiTextRendering.Default, FontAttributes);

    /// <summary>Where the text sits in the field: the look's padding, with the chevron's room at the end side.</summary>
    private Thickness TextInset
    {
        get
        {
            var look = SkUiLook.Current;
            var padding = look.PickerFieldPadding;
            var chevron = look.PickerChevronWidth + (ClearButtonShown ? look.PickerClearButtonWidth : 0);
            return _owner.IsRightToLeft
                ? new Thickness(padding.Right + chevron, padding.Top, padding.Left, padding.Bottom)
                : new Thickness(padding.Left, padding.Top, padding.Right + chevron, padding.Bottom);
        }
    }

    /// <summary>The chevron's rectangle in a field of <paramref name="width"/> × <paramref name="height"/>.</summary>
    private SKRect ChevronBounds(float width, float height)
    {
        var chevron = (float)SkUiLook.Current.PickerChevronWidth;
        return _owner.IsRightToLeft ? new SKRect(0, 0, chevron, height) : new SKRect(width - chevron, 0, width, height);
    }

    /// <summary>
    /// Where content of the control's own goes in the field (a field template), left to right (drawn children are mirrored in
    /// right-to-left layouts): the look's padding, with the chevron's and the clear button's room at the end.
    /// </summary>
    internal Thickness ContentInset
    {
        get
        {
            var look = SkUiLook.Current;
            var padding = look.PickerFieldPadding;
            return new Thickness(padding.Left, padding.Top, padding.Right + look.PickerChevronWidth + (ClearButtonShown ? look.PickerClearButtonWidth : 0),
                padding.Bottom);
        }
    }

    /// <summary>The field's size around content of <paramref name="content"/>'s size: the inset, at least the look's touch height.</summary>
    internal Size MeasureAround(Size content)
    {
        var inset = ContentInset;
        var size = new Size(content.Width + inset.HorizontalThickness, content.Height + inset.VerticalThickness);
        return HasExplicitMinimumHeight ? size : new Size(size.Width, Math.Max(size.Height, SkUiLook.Current.DefaultPickerMinimumHeight));
    }

    /// <summary>Whether the field shows its clear button: it has one and something is chosen.</summary>
    internal bool ClearButtonShown => ShowsClearButton && SelectedText is not null;

    /// <summary>The clear button's rectangle in a field of <paramref name="width"/> × <paramref name="height"/>: before the chevron, at the end side.</summary>
    private SKRect ClearButtonBounds(float width, float height)
    {
        var look = SkUiLook.Current;
        var chevron = (float)look.PickerChevronWidth;
        var clear = (float)look.PickerClearButtonWidth;
        return _owner.IsRightToLeft ? new SKRect(chevron, 0, chevron + clear, height) : new SKRect(width - chevron - clear, 0, width - chevron, height);
    }

    /// <summary>Whether <paramref name="point"/> (field coordinates) is on the shown clear button.</summary>
    public bool HitsClearButton(Point point, double width, double height) =>
        ClearButtonShown && ClearButtonBounds((float)width, (float)height).Contains((float)point.X, (float)point.Y);

    /// <summary>The field's size: the shown text (or the title) with the padding and chevron, at least the look's touch height.</summary>
    public Size Measure(double widthConstraint, double heightConstraint)
    {
        var inset = TextInset;
        if (TokensShown)
        {
            var available = Math.Max(0, widthConstraint - inset.HorizontalThickness);
            var block = LayoutTokens(available);
            var tokensSize = new Size(Math.Min(widthConstraint, block.Width + inset.HorizontalThickness), block.Height + inset.VerticalThickness);
            return HasExplicitMinimumHeight ? tokensSize : new Size(tokensSize.Width, Math.Max(tokensSize.Height, SkUiLook.Current.DefaultPickerMinimumHeight));
        }
        var text = DisplayText;
        var size = text.Length == 0
            ? new Size(inset.HorizontalThickness, inset.VerticalThickness + SkUiFontScaling.ScaleFontSize(EffectiveFontSize, FontAutoScalingEnabled) * 1.2)
            : _layout.Measure(text, TextStyle, inset, widthConstraint, heightConstraint);
        if (!HasExplicitMinimumHeight)
            size = new Size(size.Width, Math.Max(size.Height, SkUiLook.Current.DefaultPickerMinimumHeight));
        return size;
    }

    /// <summary>Draws the field: the look's chrome and chevron, then the text (the selection's, or the title in the title color).</summary>
    public void Draw(SKCanvas canvas, float width, float height)
    {
        var foreground = ToSkColor(TextColor ?? SkUiColors.DefaultForeground);
        SkUiLook.Current.DrawPickerField(canvas, new SkUiPickerFieldPaint(new SKRect(0, 0, width, height), ChevronBounds(width, height),
            ToSkColor(SkUiColors.Accent), ToSkColor(SkUiColors.Muted), foreground, _focus.Value, _open.Value, _press?.Visual ?? SkUiPressVisual.None,
            IsPointerOver, IsEnabled, _owner.IsRightToLeft) { Style = FieldStyle, Chrome = Chrome.ToPaint() });
        if (ClearButtonShown)
        {
            var bounds = ClearButtonBounds(width, height);
            var pressed = _press?.Visual is { Pressed: > 0 } && bounds.Contains((float)_pressOrigin.X, (float)_pressOrigin.Y);
            SkUiLook.Current.DrawPickerClearButton(canvas, new SkUiClearButtonPaint(bounds, ToSkColor(SkUiColors.Muted),
                ToSkColor(SkUiColors.DefaultBackground), pressed, IsEnabled));
        }
        if (TokensShown)
        {
            DrawTokens(canvas, width, height, foreground);
            return;
        }
        var selected = ShownSelection;
        var text = selected ?? PlaceholderText;
        if (string.IsNullOrEmpty(text) || HidesText)
            return;
        var color = selected is not null ? foreground : ToSkColor(PlaceholderColor ?? TitleColor ?? SkUiColors.Muted);
        if (!IsEnabled)
            color = color.WithAlpha((byte)(color.Alpha * 0.5f));
        var inset = TextInset;
        canvas.Save();
        canvas.ClipRect(new SKRect((float)inset.Left, 0, (float)(width - inset.Right), height));
        var paint = SkUiTextResources.TextPaint;
        paint.Color = color;
        _layout.Draw(canvas, text, TextStyle, inset, width, height, HorizontalTextAlignment, VerticalTextAlignment, paint);
        canvas.Restore();
    }

    /// <summary>Whether the control shows the choice with views of its own (a field template): the field draws no text for it.</summary>
    public bool HidesText;

    /// <summary>The text's native resources go (the picker left its surface).</summary>
    public void ReleaseDrawingResources()
    {
        _layout.Release();
        foreach (var layout in _tokenText)
            layout.Release();
        _tokenLayoutWidth = double.NaN;
    }

    #region Tokens

    /// <summary>Room around a token's text, the gap between tokens, and the room of a token's ×, in DIPs.</summary>
    private const double TokenPaddingX = 10;
    private const double TokenPaddingY = 4;
    private const double TokenGap = 6;
    private const double TokenRemoveWidth = 20;

    private readonly List<SkUiTextLayout> _tokenText = [];
    private readonly List<TokenBox> _tokenBoxes = [];
    private double _tokenLayoutWidth = double.NaN;
    private Size _tokenBlock;

    /// <summary>A token as laid out: its rectangle and its ×'s (empty when it cannot be removed) in the tokens' block, and its text.</summary>
    private readonly record struct TokenBox(int Token, SKRect Bounds, SKRect Remove, string Text);

    private SkUiTextLayout TokenLayout(int slot)
    {
        while (_tokenText.Count <= slot)
            _tokenText.Add(new SkUiTextLayout(_owner));
        return _tokenText[slot];
    }

    private double TokenTextWidth(int slot, string text, out double lineHeight)
    {
        var layout = TokenLayout(slot);
        layout.Invalidate();
        var size = layout.Measure(text, TextStyle, default, double.PositiveInfinity, double.PositiveInfinity);
        lineHeight = size.Height;
        return size.Width;
    }

    /// <summary>
    /// Lays the tokens out in lines <paramref name="available"/> wide (wrapping; with <see cref="MaxTokenLines"/>, the tokens that do
    /// not fit become a "+N" token at the end of the last line); returns the block's size.
    /// </summary>
    private Size LayoutTokens(double available)
    {
        // Exact: the same width needs no new layout (NaN: none yet).
        if (available == _tokenLayoutWidth)
            return _tokenBlock;
        _tokenBoxes.Clear();
        var unbounded = double.IsInfinity(available);
        double x = 0, y = 0, widest = 0, lineHeight = 0;
        var line = 0;
        for (var index = 0; index < _tokens.Count; index++)
        {
            var token = _tokens[index];
            var textWidth = TokenTextWidth(index, token.Text, out var textHeight);
            lineHeight = Math.Max(lineHeight, textHeight + 2 * TokenPaddingY);
            var width = textWidth + 2 * TokenPaddingX + (token.IsRemovable ? TokenRemoveWidth : 0);
            if (!unbounded)
                width = Math.Min(width, available);
            if (x > 0 && !unbounded && x + width > available)
            {
                if (MaxTokenLines > 0 && line + 1 >= MaxTokenLines)
                {
                    AddMoreToken(index, available, lineHeight, ref x, y);
                    break;
                }
                line++;
                x = 0;
                y += lineHeight + TokenGap;
            }
            _tokenBoxes.Add(new TokenBox(index, new SKRect((float)x, (float)y, (float)(x + width), (float)(y + lineHeight)), default, token.Text));
            x += width + TokenGap;
            widest = Math.Max(widest, x - TokenGap);
        }
        // Every box is a line tall; the × at the end side (mirrored right to left).
        var rightToLeft = _owner.IsRightToLeft;
        var blockWidth = unbounded ? widest : available;
        for (var box = 0; box < _tokenBoxes.Count; box++)
        {
            var current = _tokenBoxes[box];
            var bounds = new SKRect(current.Bounds.Left, current.Bounds.Top, current.Bounds.Right, current.Bounds.Top + (float)lineHeight);
            if (rightToLeft)
                bounds = new SKRect((float)blockWidth - bounds.Right, bounds.Top, (float)blockWidth - bounds.Left, bounds.Bottom);
            var removable = current.Token >= 0 && _tokens[current.Token].IsRemovable;
            var remove = !removable ? SKRect.Empty
                : rightToLeft ? new SKRect(bounds.Left, bounds.Top, bounds.Left + (float)TokenRemoveWidth + (float)TokenPaddingX / 2, bounds.Bottom)
                : new SKRect(bounds.Right - (float)TokenRemoveWidth - (float)TokenPaddingX / 2, bounds.Top, bounds.Right, bounds.Bottom);
            _tokenBoxes[box] = current with { Bounds = bounds, Remove = remove };
        }
        var lines = _tokenBoxes.Count == 0 ? 0 : _tokenBoxes.Max(box => box.Bounds.Bottom);
        _tokenBlock = new Size(Math.Max(widest, 0), lines);
        _tokenLayoutWidth = available;
        return _tokenBlock;
    }

    /// <summary>The tokens from <paramref name="first"/> on do not fit: a "+N" token closes the last line (moving tokens into it as needed).</summary>
    private void AddMoreToken(int first, double available, double lineHeight, ref double x, double y)
    {
        var hidden = _tokens.Count - first;
        while (true)
        {
            var text = string.Format(System.Globalization.CultureInfo.CurrentCulture, SkUiStrings.Current.MoreTokensFormat, hidden);
            var width = TokenTextWidth(_tokens.Count, text, out _) + 2 * TokenPaddingX;
            var lastOnLine = _tokenBoxes.Count > 0 && Math.Abs(_tokenBoxes[^1].Bounds.Top - y) < TokenLineTolerance ? _tokenBoxes[^1] : (TokenBox?)null;
            if (x + width <= available || lastOnLine is null)
            {
                _tokenBoxes.Add(new TokenBox(-1, new SKRect((float)x, (float)y, (float)(x + Math.Min(width, available)), (float)(y + lineHeight)), default, text));
                x += width + TokenGap;
                return;
            }
            // Not even "+N" fits: one more token goes into it.
            _tokenBoxes.RemoveAt(_tokenBoxes.Count - 1);
            x = lastOnLine.Value.Bounds.Left;
            hidden++;
        }
    }

    /// <summary>How close two token tops must be to be on one line.</summary>
    private const float TokenLineTolerance = 0.5f;

    /// <summary>The tokens' block in a field of <paramref name="width"/> × <paramref name="height"/>: inside the inset, centered vertically.</summary>
    private (float Left, float Top) TokenOrigin(float width, float height)
    {
        var inset = TextInset;
        LayoutTokens(Math.Max(0, width - inset.HorizontalThickness));
        var room = height - inset.VerticalThickness;
        return ((float)inset.Left, (float)(inset.Top + Math.Max(0, (room - _tokenBlock.Height) / 2)));
    }

    private void DrawTokens(SKCanvas canvas, float width, float height, SKColor foreground)
    {
        var (left, top) = TokenOrigin(width, height);
        var look = SkUiLook.Current;
        var fill = ToSkColor(look.PopupHighlightColor);
        var muted = ToSkColor(SkUiColors.Muted);
        var text = IsEnabled ? foreground : foreground.WithAlpha((byte)(foreground.Alpha * 0.5f));
        var paint = SkUiTextResources.TextPaint;
        canvas.Save();
        canvas.ClipRect(new SKRect(left, 0, width - (float)TextInset.Right, height));
        for (var index = 0; index < _tokenBoxes.Count; index++)
        {
            var box = _tokenBoxes[index];
            var bounds = box.Bounds;
            bounds.Offset(left, top);
            var remove = box.Remove;
            if (!remove.IsEmpty)
                remove.Offset(left, top);
            var pressed = !remove.IsEmpty && _press?.Visual is { Pressed: > 0 } && remove.Contains((float)_pressOrigin.X, (float)_pressOrigin.Y);
            look.DrawPickerToken(canvas, new SkUiPickerTokenPaint(bounds, remove, fill, muted, pressed, IsEnabled, box.Token < 0));
            var removeRoom = remove.IsEmpty ? 0 : TokenRemoveWidth;
            var inset = _owner.IsRightToLeft
                ? new Thickness(TokenPaddingX + removeRoom, TokenPaddingY, TokenPaddingX, TokenPaddingY)
                : new Thickness(TokenPaddingX, TokenPaddingY, TokenPaddingX + removeRoom, TokenPaddingY);
            canvas.Save();
            canvas.Translate(bounds.Left, bounds.Top);
            paint.Color = text;
            TokenLayout(box.Token < 0 ? _tokens.Count : box.Token)
                .Draw(canvas, box.Text, TextStyle, inset, bounds.Width, bounds.Height, TextAlignment.Start, TextAlignment.Center, paint);
            canvas.Restore();
        }
        canvas.Restore();
    }

    /// <summary>The item whose token's × is at <paramref name="point"/> (field coordinates), or -1.</summary>
    public int TokenRemovedAt(Point point, double width, double height)
    {
        if (!TokensShown)
            return -1;
        var (left, top) = TokenOrigin((float)width, (float)height);
        foreach (var box in _tokenBoxes)
        {
            if (box.Remove.IsEmpty || box.Token < 0)
                continue;
            var remove = box.Remove;
            remove.Offset(left, top);
            // A little room around the × (a touch target).
            remove.Inflate(0, (float)TokenGap / 2);
            if (remove.Contains((float)point.X, (float)point.Y))
                return _tokens[box.Token].Index;
        }
        return -1;
    }

    /// <summary>The tokens as laid out in a field of <paramref name="width"/> × <paramref name="height"/>: their texts and rectangles (tests).</summary>
    internal IReadOnlyList<(string Text, Rect Bounds, Rect Remove)> TokenBoxes(double width, double height)
    {
        var (left, top) = TokenOrigin((float)width, (float)height);
        return [.. _tokenBoxes.Select(box => (box.Text, new Rect(box.Bounds.Left + left, box.Bounds.Top + top, box.Bounds.Width, box.Bounds.Height),
            box.Remove.IsEmpty ? Rect.Zero : new Rect(box.Remove.Left + left, box.Remove.Top + top, box.Remove.Width, box.Remove.Height)))];
    }

    #endregion

    /// <summary>The field is pressed or released (<paramref name="origin"/>: the press point).</summary>
    public void SetPressed(bool pressed, Point origin)
    {
        if (pressed)
            _pressOrigin = origin;
        (_press ??= new SkUiPressAnimator(_owner, ripple: false)).SetPressed(pressed, origin);
        _owner.InvalidatePaint();
    }

    private Point _pressOrigin;

    /// <summary>The field gained or lost keyboard focus.</summary>
    public void SetFocused(bool focused) => UpdateHighlight(focused);

    private bool _keyboardFocused;

    private void UpdateHighlight(bool? focused = null)
    {
        if (focused is { } value)
            _keyboardFocused = value;
        var target = _keyboardFocused || IsOpen ? 1f : 0f;
        if (Math.Abs(_focus.Target - target) > 0)
            _focus.AnimateTo(target, SkUiTransitionKind.FieldFocus);
        var open = IsOpen ? 1f : 0f;
        if (Math.Abs(_open.Target - open) > 0)
            _open.AnimateTo(open, SkUiTransitionKind.PickerOpen);
    }

    #endregion

    #region Keyboard and screen readers

    /// <summary>
    /// The closed field's keys, as a desktop combo box's: Alt+Down / Alt+Up (and F4-less platforms' Alt+arrows) open; Up / Down
    /// (Left / Right) select the previous / next item, Home / End the first / last, without opening. Space and Enter open
    /// through the activation.
    /// </summary>
    public bool OnKeyDown(in SkUiKeyEvent key)
    {
        if (!IsEnabled || IsOpen)
            return false;
        if ((key.Modifiers & SkUiKeyModifiers.Alt) != 0 && key.Key is SkUiKey.Down or SkUiKey.Up)
        {
            _owner.SetIsOpen(true);
            return true;
        }
        // Backspace with tokens: the last token that can be removed goes.
        if (TokensShown && key.Key == SkUiKey.Backspace && !key.HasCommandModifier)
        {
            for (var index = _tokens.Count - 1; index >= 0; index--)
                if (_tokens[index].IsRemovable)
                {
                    _owner.RemoveChoice(_tokens[index].Index);
                    return true;
                }
            return false;
        }
        // The clear button's key.
        if (ClearButtonShown && key.Key is SkUiKey.Delete or SkUiKey.Backspace && !key.HasCommandModifier)
        {
            _owner.ClearChoice();
            return true;
        }
        if (key.HasCommandModifier || _items.Count == 0 || !ClosedFieldSelects)
            return false;
        var current = SelectedIndex;
        var rightToLeft = _owner.IsRightToLeft;
        var forward = key.Key is SkUiKey.Down or SkUiKey.End || key.Key == (rightToLeft ? SkUiKey.Left : SkUiKey.Right);
        var target = key.Key switch
        {
            SkUiKey.Down => current + 1,
            SkUiKey.Up => current < 0 ? 0 : current - 1,
            SkUiKey.Right => rightToLeft ? Math.Max(0, current - 1) : current + 1,
            SkUiKey.Left => rightToLeft ? current + 1 : Math.Max(0, current - 1),
            SkUiKey.Home => 0,
            SkUiKey.End => _items.Count - 1,
            SkUiKey.Character => SkUiTypeAhead.Next(_items, current, key.Character, ref _typeAhead, _itemEnabled),
            _ => int.MinValue
        };
        if (target == int.MinValue)
            return false;
        // Disabled items are passed by, in the key's direction (Home: the first enabled one; End: the last).
        target = SkUiItemStates.Nearest(_itemEnabled, _items.Count, target, key.Key == SkUiKey.Home ? 1 : key.Key == SkUiKey.End ? -1 : forward ? 1 : -1);
        if (target >= 0 && target != current)
            _owner.Choose(target);
        return true;
    }

    private SkUiTypeAhead _typeAhead;

    /// <summary>A combo box: its title as the name, the selection as the value, whether the list is open, opened by activation.</summary>
    public void PopulateSemantics(SkUiSemanticsInfo info)
    {
        info.Role = SkUiSemanticsRole.ComboBox;
        info.Text = Title ?? Placeholder;
        info.Value = SelectedText;
        info.IsExpanded = IsOpen;
        info.IsEnabled &= IsEnabled;
    }

    #endregion

    #region Popup

    /// <summary>Whether a popup is open (or closing).</summary>
    public bool IsOpen => _session is not null;

    /// <summary>The open popup's request (tests); <c>null</c> while closed.</summary>
    internal SkUiPopupRequest? Request => _session?.Request;

    /// <summary>The control's <c>IsOpen</c> became <c>true</c>: opens the popup (after a closing one has gone).</summary>
    public void Open()
    {
        if (_session is { } running)
        {
            // A popup closing while it opens again: open the next one once this one has gone.
            _reopen = running.IsClosing;
            return;
        }
        if (!_owner.CanOpen || SkUiDiagnostics.GetSurfaceRoot(_owner.Node) is not { } root || AnchorOf(_owner.Node) is not { } anchor)
        {
            Trace.WriteLine("SkiaUi: a picker that is disabled, hidden or not on a drawn surface cannot open.");
            _owner.SetIsOpen(false);
            return;
        }
        var session = new PopupSession(this, anchor, root);
        _session = session;
        UpdateHighlight();
        _owner.InvalidateSemantics();
        try
        {
            session.Start();
        }
        catch (Exception exception)
        {
            // Not shown (a failure building or placing it, e.g. in the item template): closed again, so the picker can open
            // later. Reported as presenter failures are (this runs in the IsOpen change, where a throw would leave it open).
            Trace.WriteLine($"SkiaUi: a picker's popup could not be built: {exception}");
            session.Abort();
            if (ReferenceEquals(_session, session))
            {
                _session = null;
                _reopen = false;
                UpdateHighlight();
                _owner.InvalidateSemantics();
                _owner.SetIsOpen(false);
            }
        }
    }

    /// <summary>The control's <c>IsOpen</c> became <c>false</c>, or the picker can no longer show its popup: the popup closes.</summary>
    public void Close()
    {
        _reopen = false;
        _session?.Close();
    }

    /// <summary>The popup is gone: applies a choice, then <c>IsOpen</c> goes back to <c>false</c> (or the next popup opens).</summary>
    private void Ended(PopupSession session, int choice, bool cleared, bool hadFocus)
    {
        if (!ReferenceEquals(_session, session))
            return;
        _session = null;
        UpdateHighlight();
        _owner.InvalidateSemantics();
        choice = session.ItemsChanged ? session.CurrentIndexOf(choice) : choice;
        if (cleared)
            _owner.ClearChoice();
        else if (choice >= 0 && choice < _items.Count)
            _owner.Choose(choice);
        if (hadFocus)
            _owner.FocusField();
        if (_reopen && _owner.CanOpen)
        {
            _reopen = false;
            Open();
            return;
        }
        _reopen = false;
        _owner.SetIsOpen(false);
    }

    /// <summary>The MAUI view a request anchors to: the picker itself, or for a Core node its host (the nearest drawn view).</summary>
    private static View? AnchorOf(ISkUiRenderable node)
    {
        for (ISkUiRenderable? current = node; current is not null; current = current.RenderParent)
            if (current is SkUiView view)
                return view;
        return null;
    }

    /// <summary>
    /// One opening of the popup: builds the drawn list and the request, hands them to the presenter, closes on the control's
    /// triggers, and reports the choice. It keeps the model only until the presenter's task finished.
    /// </summary>
    private sealed class PopupSession(SkUiPickerModel model, View anchor, SkUiView surfaceRoot)
    {
        private readonly List<SkUiScrollController> _scrollers = [];
        private readonly List<ScrollView> _nativeScrollers = [];
        private Window? _window;
        private ISkUiPickerPopupContent? _content;
        private string[] _texts = [];
        private object?[]? _sourceItems;

        public SkUiPopupRequest? Request { get; private set; }

        /// <summary>The picker's items changed while the popup was open (it closes; a choice made in it is mapped).</summary>
        public bool ItemsChanged { get; set; }

        /// <summary>
        /// Where the item the popup showed at <paramref name="choice"/> is now: the same object of the picker's source, else the
        /// same text (at the same index when it is still there); -1 when it has gone.
        /// </summary>
        public int CurrentIndexOf(int choice)
        {
            if (choice < 0 || choice >= _texts.Length)
                return -1;
            if (_sourceItems is { } shown && model._owner.ItemsSource is { } source)
                return source.IndexOf(shown[choice]);
            var text = _texts[choice];
            var items = model._items;
            if (choice < items.Count && items[choice] == text)
                return choice;
            for (var index = 0; index < items.Count; index++)
                if (items[index] == text)
                    return index;
            return -1;
        }

        /// <summary>The picker's selection changed while open: the list's highlight follows.</summary>
        public void SelectionChanged(int index) => _content?.SelectionChanged(index);

        /// <summary>Which items can be chosen changed while open: the content follows.</summary>
        public void ItemStatesChanged(int column, IReadOnlyList<bool>? states) => _content?.ItemStatesChanged(column, states);

        public bool IsClosing => Request?.CloseRequested.IsCancellationRequested ?? false;

        public void Start()
        {
            var look = SkUiLook.Current;
            var placement = model.PopupPlacement == SkUiPopupPlacement.Default ? look.DefaultPopupPlacement : model.PopupPlacement;
            if (placement == SkUiPopupPlacement.Default)
                placement = SkUiPopupPlacement.Anchored;
            var rightToLeft = model._owner.IsRightToLeft;
            var rootBounds = SkUiDiagnostics.GetRootBounds(model._owner.Node) ?? Rect.Zero;
            var origin = SkUiWindowMetrics.SurfaceOrigin(surfaceRoot);
            var anchorBounds = rootBounds.Offset(origin.X, origin.Y);
            var windowSize = SkUiWindowMetrics.WindowSize(anchor.Window);
            var rowStyle = new SkUiPickerRowStyle(model.FontFamily, model.EffectiveFontSize, model.FontAttributes, model.FontAutoScalingEnabled, model.CharacterSpacing,
                model.TextColor, model.SelectionBackground);
            var presenter = SkUiPopups.Resolve(model.PopupPresenter);
            var requiresAccept = model.UpdateMode == SkUiPickerUpdateMode.WhenFinished;
            // The texts are read at each opening, so a culture switch applies to the next popup.
            var strings = SkUiStrings.Current;
            var chrome = new SkUiPickerPopupChrome(requiresAccept, model.AcceptText ?? strings.AcceptText, model.CancelText ?? strings.CancelText,
                model.ShowsPopupClearButton, model.ClearText ?? strings.ClearText);
            var extras = new SkUiPopupExtras(model.PopupHeaderTemplate, model.PopupFooterTemplate, model._owner.PopupBindingContext);
            var context = new SkUiPickerPopupContext(placement, placement == SkUiPopupPlacement.Anchored ? null : model.Title, rightToLeft, rowStyle, chrome, extras);
            var content = model.ContentFactory is { } factory ? factory(context) : BuildList(context);
            var maximumHeight = model.MaxPopupHeight >= 0 ? model.MaxPopupHeight : double.PositiveInfinity;
            var (minimumWidth, maximumWidth) = model.PopupWidthRange(windowSize.Width > 0 ? windowSize.Width : double.PositiveInfinity);
            // The content is measured within the range: as wide as its widest item (the first ones'), so an anchored list is
            // wider than a narrow field when its items are; without a minimum of the app's, at least the field's width (anchored)
            // or the look's preferred width (window popups).
            var preferred = content.PreferredSize(
                double.IsNaN(minimumWidth) ? Math.Min(placement == SkUiPopupPlacement.Anchored ? anchorBounds.Width : look.PopupPreferredWidth, maximumWidth) : minimumWidth,
                maximumWidth);
            var layout = SkUiPopupGeometry.Layout(new SkUiPopupPlacementInput(anchorBounds, rootBounds, windowSize,
                SkUiWindowMetrics.SafeAreaInsets(surfaceRoot), look.PopupMargin, look.PopupAnchorGap, placement, look.PopupHorizontalOptions,
                look.PopupVerticalOptions, preferred, maximumHeight, model.PopupBounds, rightToLeft, minimumWidth, maximumWidth));
            _content = content;
            var request = content.CreateRequest(anchor, surfaceRoot, layout,
                new SkUiPopupRequestOptions(model.Title, look.GetPopupOverlayColor(layout.Placement), !SkUiMotion.IsMotionReduced, chrome));
            content.Surface.WidthRequest = layout.Bounds.Width;
            content.Surface.HeightRequest = layout.Bounds.Height;
            Request = request;
            if (layout.Placement == SkUiPopupPlacement.Anchored)
                WatchAnchor();
            _ = RunAsync(request, presenter);
        }

        /// <summary>The default content: the items to choose one from.</summary>
        private ISkUiPickerPopupContent BuildList(in SkUiPickerPopupContext context)
        {
            var items = model._items.ToArray();
            _texts = items;
            if (model._owner.ItemsSource is { } source)
            {
                _sourceItems = new object?[source.Count];
                source.CopyTo(_sourceItems, 0);
            }
            return new SkUiPickerPopup(items, model.SelectedIndex, model.ItemTemplate, model._owner.ItemsSource, context.RowStyle, context.Title, context.RightToLeft,
                context.Chrome, itemEnabled: model._itemEnabled, extras: context.Extras);
        }

        private async Task RunAsync(SkUiPopupRequest request, ISkUiPopupPresenter presenter)
        {
            try
            {
                await presenter.ShowAsync(request, request.CloseRequested);
            }
            catch (OperationCanceledException)
            {
                // Closed: the same as returning.
            }
            catch (Exception exception)
            {
                Trace.WriteLine($"SkiaUi: the popup presenter {presenter.GetType().Name} failed: {exception.Message}");
            }
            finally
            {
                StopWatching();
                var choice = request is SkUiListPopupRequest { HasResult: true } list ? list.Result : -1;
                // A list completed with -1: the popup's Clear.
                var cleared = request is SkUiListPopupRequest { HasResult: true, Result: -1 };
                var hadFocus = _content?.IsFocused == true;
                request.MarkCompleted();
                _content?.Release();
                _content = null;
                model.Ended(this, choice, cleared, hadFocus);
                _sourceItems = null;
            }
        }

        public void Close() => Request?.Close();

        /// <summary><see cref="Start"/> failed: nothing was handed to a presenter; stops watching and lets go of the content.</summary>
        public void Abort()
        {
            StopWatching();
            Request?.MarkCompleted();
            _content?.Release();
            _content = null;
            _sourceItems = null;
        }

        /// <summary>An anchored popup closes when its anchor scrolls (a drawn or a MAUI scroller around it) or the window resizes.</summary>
        private void WatchAnchor()
        {
            for (ISkUiRenderable? current = model._owner.Node.RenderParent; current is not null; current = current.RenderParent)
                if (current is ISkUiScrollHost host)
                {
                    host.Scroller.Scrolled += Close;
                    _scrollers.Add(host.Scroller);
                }
            for (var element = surfaceRoot.Parent; element is not null; element = element.Parent)
                if (element is ScrollView scrollView)
                {
                    scrollView.Scrolled += OnNativeScrolled;
                    _nativeScrollers.Add(scrollView);
                }
            _window = anchor.Window;
            if (_window is not null)
                _window.SizeChanged += OnWindowSizeChanged;
        }

        private void StopWatching()
        {
            foreach (var scroller in _scrollers)
                scroller.Scrolled -= Close;
            _scrollers.Clear();
            foreach (var scrollView in _nativeScrollers)
                scrollView.Scrolled -= OnNativeScrolled;
            _nativeScrollers.Clear();
            if (_window is not null)
                _window.SizeChanged -= OnWindowSizeChanged;
            _window = null;
        }

        private void OnNativeScrolled(object? sender, ScrolledEventArgs args) => Close();

        private void OnWindowSizeChanged(object? sender, EventArgs args) => Close();
    }

    #endregion

    private static SKColor ToSkColor(Color color) => SkUiToggleDrawing.ToSkColor(color);
}

/// <summary>
/// Type-ahead in a list of texts: typing jumps to the next item starting with what was typed within a second (case- and
/// culture-insensitive); the same letter again cycles through the items starting with it.
/// </summary>
internal struct SkUiTypeAhead
{
    private const long ResetMilliseconds = 1000;
    private string _typed;
    private long _last;

    /// <summary>The item to go to after typing <paramref name="character"/> at <paramref name="current"/>, or <paramref name="current"/> when none matches.</summary>
    public static int Next(IReadOnlyList<string> items, int current, char character, ref SkUiTypeAhead state, IReadOnlyList<bool>? enabled = null)
    {
        var now = Environment.TickCount64;
        var typed = now - state._last > ResetMilliseconds || state._typed is null ? string.Empty : state._typed;
        state._last = now;
        typed += character;
        // One letter typed again and again: cycle through the items starting with it.
        var repeated = typed.Length > 1 && typed.All(c => char.ToUpperInvariant(c) == char.ToUpperInvariant(typed[0]));
        var prefix = repeated ? typed[..1] : typed;
        state._typed = typed;
        var start = repeated || typed.Length == 1 ? current + 1 : Math.Max(0, current);
        for (var offset = 0; offset < items.Count; offset++)
        {
            var index = (start + offset) % items.Count;
            if (index >= 0 && SkUiItemStates.IsEnabled(enabled, index) && (items[index] ?? string.Empty).StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase))
                return index;
        }
        return current;
    }
}
