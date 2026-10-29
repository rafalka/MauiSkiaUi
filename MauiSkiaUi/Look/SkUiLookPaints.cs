using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// What a look draws for a Slider (<see cref="SkUiLook.DrawSlider"/>): horizontal, left-to-right coordinates, with
/// <see cref="Fraction"/> measured from the start (0 = minimum). Colors already reflect the enabled state.
/// </summary>
/// <param name="Bounds">The slider's rectangle (for a vertical slider, rotated: its length runs along X).</param>
/// <param name="Fraction">Position of the value between minimum (0) and maximum (1).</param>
/// <param name="Orientation">The slider's orientation (the canvas is already rotated for vertical sliders).</param>
/// <param name="MinimumTrack">Track color between the start and the thumb.</param>
/// <param name="MaximumTrack">Track color between the thumb and the end.</param>
/// <param name="Thumb">Thumb color.</param>
/// <param name="Pressed">How pressed the thumb is, 0–1 (1 while dragged; animated by the press / release transitions).</param>
/// <param name="IsEnabled">The control is enabled.</param>
/// <remarks><see cref="Fraction"/> is the drawn position: after a tap it follows the <see cref="SkUiTransitionKind.SliderThumb"/> transition.</remarks>
public readonly record struct SkUiSliderPaint(
    SKRect Bounds, float Fraction, StackOrientation Orientation, SKColor MinimumTrack, SKColor MaximumTrack, SKColor Thumb,
    float Pressed, bool IsEnabled)
{
    /// <summary>
    /// The slider has a thumb image (<c>ThumbImageSource</c>): the look draws the track only, and the control draws the
    /// image, upright, centered where the look's thumb would be.
    /// </summary>
    public bool HasThumbImage { get; init; }
}

/// <summary>
/// What a look draws for a ProgressBar (<see cref="SkUiLook.DrawProgressBar"/>), in left-to-right coordinates.
/// Colors already reflect the enabled state.
/// </summary>
/// <param name="Bounds">The bar's rectangle.</param>
/// <param name="Progress">Drawn completed fraction (0–1; follows the <see cref="SkUiTransitionKind.Progress"/> transition); unused when <see cref="IsIndeterminate"/>.</param>
/// <param name="IsIndeterminate">Draw the track and one moving segment at the start (the compositor slides it).</param>
/// <param name="Track">Track (remaining) color.</param>
/// <param name="Fill">Progress color.</param>
/// <param name="SegmentFraction">Indeterminate segment length as a fraction of the bar.</param>
/// <param name="IsEnabled">The control is enabled.</param>
public readonly record struct SkUiProgressBarPaint(
    SKRect Bounds, float Progress, bool IsIndeterminate, SKColor Track, SKColor Fill, float SegmentFraction, bool IsEnabled);

/// <summary>
/// What a look draws for a dragger (<see cref="SkUiLook.DrawDragger"/>, also a grid splitter): a line through the middle of
/// <see cref="Bounds"/> and a grip on it, both along the dragger. The control's background is drawn behind them. Left-to-right
/// coordinates (the control swaps a horizontal line's grip start and end in right-to-left layouts); colors already reflect
/// the enabled state.
/// </summary>
/// <param name="Bounds">The dragger's rectangle.</param>
/// <param name="TouchBounds">
/// Its touch area: <see cref="Bounds"/>, or wider across a thinner dragger (<see cref="SkUiLook.DraggerTouchThickness"/>).
/// A painter may draw anywhere in it (the node does not clip a thin dragger), e.g. a pill over a 1 DIP divider.
/// </param>
/// <param name="LineRunsVertically">The line and grip run top to bottom (a horizontal drag); otherwise left to right.</param>
/// <param name="ShowsGrip">Draw the grip.</param>
/// <param name="Grip">Grip color.</param>
/// <param name="GripAlignment">Where the grip sits along the dragger: start, center, end, or along all of it (<see cref="LayoutAlignment.Fill"/>).</param>
/// <param name="GripLength">The grip's length in DIPs (the control's, or <see cref="SkUiLook.DraggerGripLength"/>).</param>
/// <param name="ShowsLine">Draw the line, along the whole dragger.</param>
/// <param name="Line">Line color.</param>
/// <param name="IsDragging">The dragger is being dragged.</param>
/// <param name="IsEnabled">The control is enabled.</param>
public readonly record struct SkUiDraggerPaint(SKRect Bounds, SKRect TouchBounds, bool LineRunsVertically, bool ShowsGrip, SKColor Grip, LayoutAlignment GripAlignment,
    float GripLength, bool ShowsLine, SKColor Line, bool IsDragging, bool IsEnabled);

/// <summary>
/// What a look draws for a Switch (<see cref="SkUiLook.DrawSwitch"/>), in left-to-right coordinates (the control mirrors
/// right-to-left layouts). Colors already reflect the enabled state; the look picks and blends them by
/// <see cref="Visual"/>.
/// </summary>
/// <param name="Bounds">The switch's rectangle.</param>
/// <param name="Visual">State, transition progress and press amount.</param>
/// <param name="OnTrack">Track color when checked.</param>
/// <param name="OffTrack">Track color when unchecked.</param>
/// <param name="Thumb">Thumb color.</param>
/// <param name="IsEnabled">The control is enabled.</param>
public readonly record struct SkUiSwitchPaint(SKRect Bounds, SkUiToggleVisual Visual, SKColor OnTrack, SKColor OffTrack, SKColor Thumb, bool IsEnabled);

/// <summary>
/// What a look draws for a CheckBox (<see cref="SkUiLook.DrawCheckBox"/>): a square of <see cref="Size"/> at the origin.
/// Colors already reflect the enabled state.
/// </summary>
/// <param name="Size">Side length of the box.</param>
/// <param name="Visual">State, transition progress and press amount.</param>
/// <param name="Color">Fill and border when checked or indeterminate.</param>
/// <param name="Background">Fill when unchecked.</param>
/// <param name="Border">Border when unchecked.</param>
/// <param name="IsEnabled">The control is enabled.</param>
public readonly record struct SkUiCheckBoxPaint(float Size, SkUiToggleVisual Visual, SKColor Color, SKColor Background, SKColor Border, bool IsEnabled);

/// <summary>
/// What a look draws for a RadioButton (<see cref="SkUiLook.DrawRadioButton"/>): a circle of <see cref="Size"/> at the
/// origin. Colors already reflect the enabled state.
/// </summary>
/// <param name="Size">Diameter.</param>
/// <param name="Visual">State, transition progress and press amount.</param>
/// <param name="Color">Ring and dot when checked (and the indeterminate bar).</param>
/// <param name="Ring">Ring when unchecked.</param>
/// <param name="IsEnabled">The control is enabled.</param>
public readonly record struct SkUiRadioButtonPaint(float Size, SkUiToggleVisual Visual, SKColor Color, SKColor Ring, bool IsEnabled);

/// <summary>
/// What a look draws behind a Button's text (<see cref="SkUiLook.DrawButton"/>): its rounded fill and border with press
/// feedback. <see cref="Fill"/> is already the disabled color for a disabled button.
/// </summary>
/// <param name="Bounds">The button's rectangle.</param>
/// <param name="CornerRadii">Corner radii of the fill, border and press feedback.</param>
/// <param name="Fill">Fill color (unpressed); for a gradient <see cref="FillPaint"/>, its stops averaged.</param>
/// <param name="Border">Border color.</param>
/// <param name="BorderWidth">Border width (drawn inside the bounds).</param>
/// <param name="Press">Press amount, press point and ripple.</param>
/// <param name="IsEnabled">The button is enabled and its command can execute.</param>
public readonly record struct SkUiButtonPaint(
    SKRect Bounds, CornerRadius CornerRadii, SKColor Fill, SKColor Border, float BorderWidth, SkUiPressVisual Press, bool IsEnabled)
{
    /// <summary>
    /// The fill as a gradient (a <see cref="LinearGradientPaint"/> or <see cref="RadialGradientPaint"/> from a gradient
    /// <c>Background</c>, mapped onto <see cref="Bounds"/>), or <c>null</c> for the solid <see cref="Fill"/>. Looks that draw
    /// colors only can ignore it: <see cref="Fill"/> then holds the stops averaged.
    /// </summary>
    public Paint? FillPaint { get; init; }
}

/// <summary>
/// What a look draws over a pressable control's content (<see cref="SkUiLook.DrawPressOverlay"/>): press feedback over
/// an ImageButton's image, or over any node with <c>ShowsPressEffect</c> (a card, a composite button); and the disabled
/// dimming of an ImageButton.
/// </summary>
/// <param name="Bounds">The control's rectangle.</param>
/// <param name="CornerRadii">Corner radii the feedback is clipped to (the control's rounded shape).</param>
/// <param name="Press">Press amount, press point and ripple.</param>
/// <param name="IsEnabled">The control is enabled (and its command can execute).</param>
public readonly record struct SkUiPressOverlayPaint(SKRect Bounds, CornerRadius CornerRadii, SkUiPressVisual Press, bool IsEnabled);

/// <summary>
/// What a look draws around the drawn control that has keyboard focus (<see cref="SkUiLook.DrawFocusRing"/>), while focus
/// came from the keyboard. Drawn over the control, inside its rectangle (leaf controls clip to their bounds).
/// </summary>
/// <param name="Bounds">The control's rectangle.</param>
/// <param name="CornerRadii">The control's corner radii (its rounded shape).</param>
/// <param name="Color">Ring color (the color scheme's foreground).</param>
/// <param name="Contrast">Inner contrast line, so the ring shows on any fill (the color scheme's background).</param>
public readonly record struct SkUiFocusRingPaint(SKRect Bounds, CornerRadius CornerRadii, SKColor Color, SKColor Contrast)
{
    /// <summary>A ring in the current color scheme's colors.</summary>
    internal static SkUiFocusRingPaint For(SKRect bounds, CornerRadius radii) =>
        new(bounds, radii, SkUiToggleDrawing.ToSkColor(SkUiColors.DefaultForeground), SkUiToggleDrawing.ToSkColor(SkUiColors.DefaultBackground));
}

/// <summary>
/// What a look draws for a scroll bar thumb (<see cref="SkUiLook.DrawScrollBar"/>): a rectangle at the origin, as long as
/// the thumb along <see cref="Orientation"/> and <see cref="SkUiLook.ScrollBarThickness"/> across. The compositor places it
/// on the track and fades it.
/// </summary>
/// <param name="Bounds">The thumb's rectangle.</param>
/// <param name="Orientation">Which bar: <see cref="ScrollOrientation.Vertical"/> or <see cref="ScrollOrientation.Horizontal"/>.</param>
/// <param name="Color">Thumb color (the color scheme's foreground, translucent, unless the bar sets <c>ThumbColor</c>).</param>
public readonly record struct SkUiScrollBarPaint(SKRect Bounds, ScrollOrientation Orientation, SKColor Color)
{
    /// <summary>A pointer hovers or drags the bar: it is <see cref="SkUiLook.ScrollBarExpandedThickness"/> thick and shows its track.</summary>
    public bool IsExpanded { get; init; }

    /// <summary>The thumb is being dragged.</summary>
    public bool IsPressed { get; init; }
}

/// <summary>
/// What a look draws behind an expanded scroll bar's thumb (<see cref="SkUiLook.DrawScrollBarTrack"/>): the track, while a
/// pointer hovers or drags the bar.
/// </summary>
/// <param name="Bounds">The track's rectangle (as thick as the expanded thumb).</param>
/// <param name="Orientation">Which bar: <see cref="ScrollOrientation.Vertical"/> or <see cref="ScrollOrientation.Horizontal"/>.</param>
/// <param name="Color">The thumb's color (the look derives the track's from it).</param>
public readonly record struct SkUiScrollBarTrackPaint(SKRect Bounds, ScrollOrientation Orientation, SKColor Color);

/// <summary>
/// What a look draws for a refresh indicator (<see cref="SkUiLook.DrawRefreshIndicator"/>). It is recorded once per state
/// (style, color, refreshing or not); a pull shows through composite-time opacity and rotation
/// (<see cref="SkUiLook.GetRefreshPullFeedback"/>), unless the look draws the pull itself
/// (<see cref="SkUiLook.RefreshIndicatorDrawsPullProgress"/>), and while refreshing the compositor spins the picture about
/// its center (<see cref="SkUiLook.RefreshIndicatorSpinPeriod"/>).
/// </summary>
/// <param name="Bounds">The indicator's square (<see cref="SkUiLook.RefreshIndicatorSize"/>).</param>
/// <param name="Color">The arc's color (the control's <c>RefreshColor</c>, or the accent).</param>
/// <param name="Style">How the indicator shows: a badge over the content, or inline above it (never <see cref="SkUiRefreshStyle.Default"/>).</param>
/// <param name="IsRefreshing">A refresh runs (the picture spins); otherwise the indicator follows a pull.</param>
/// <param name="PullProgress">
/// How far the pull got, 1 at the trigger distance; always 0 unless <see cref="SkUiLook.RefreshIndicatorDrawsPullProgress"/>.
/// </param>
public readonly record struct SkUiRefreshIndicatorPaint(SKRect Bounds, SKColor Color, SkUiRefreshStyle Style, bool IsRefreshing, double PullProgress);

/// <summary>How a refresh indicator shows a pull, at composite time (<see cref="SkUiLook.GetRefreshPullFeedback"/>).</summary>
/// <param name="Opacity">Multiplies the indicator's opacity (0–1).</param>
/// <param name="Rotation">Added to the indicator's rotation, in degrees.</param>
public readonly record struct SkUiRefreshPullFeedback(double Opacity, double Rotation);

/// <summary>
/// What a look draws for an indicator view (<see cref="SkUiLook.DrawIndicators"/>): a row of indicators in left-to-right
/// coordinates (the control mirrors right-to-left layouts), along <see cref="Orientation"/>. The control lays the row out,
/// so taps hit what is drawn: <see cref="GetIndicatorBounds"/> is each indicator's rectangle, with the selected one
/// <see cref="SelectedExtraLength"/> longer, and <see cref="GetSelection"/> how selected it is while the selection moves.
/// Colors already reflect the enabled state.
/// </summary>
/// <param name="Bounds">The row's rectangle.</param>
/// <param name="Count">How many indicators show (at most the view's <c>MaximumVisible</c>).</param>
/// <param name="First">
/// The item of the first indicator shown (indicators show a window of the items when there are more; with <see cref="Wraps"/>
/// the window runs on past the last item to the first: <see cref="GetItem"/>).
/// </param>
/// <param name="Position">
/// The selected item, fractional while the selection moves (a linked carousel scrolls, a transition runs), in items; with
/// <see cref="Wraps"/>, past the last item it moves on to the first.
/// </param>
/// <param name="ItemCount">How many items there are (<see cref="Count"/> or more).</param>
/// <param name="IndicatorSize">An indicator's size (its thickness across the row, and its length).</param>
/// <param name="Spacing">The gap between indicators.</param>
/// <param name="SelectedExtraLength">How much longer the selected indicator is along the row (<see cref="SkUiLook.GetSelectedIndicatorExtraLength"/>).</param>
/// <param name="Shape">The indicators' shape.</param>
/// <param name="Color">Unselected indicators' color.</param>
/// <param name="SelectedColor">The selected indicator's color.</param>
/// <param name="Orientation">The row's direction.</param>
/// <param name="Wraps">The selection wraps from the last item to the first (a looping carousel).</param>
/// <param name="IsEnabled">The control is enabled.</param>
public readonly record struct SkUiIndicatorPaint(
    SKRect Bounds, int Count, int First, float Position, int ItemCount, float IndicatorSize, float Spacing, float SelectedExtraLength,
    IndicatorShape Shape, SKColor Color, SKColor SelectedColor, StackOrientation Orientation, bool Wraps, bool IsEnabled)
{
    /// <summary>The slot of the indicator under a hovering pointer (0 = the first shown), or -1.</summary>
    public int HoveredSlot { get; init; } = -1;

    /// <summary>Where the previous button goes (before the row: left, or top), or empty when it does not show.</summary>
    public SKRect PreviousButtonBounds { get; init; }

    /// <summary>Where the next button goes (after the row: right, or bottom), or empty when it does not show.</summary>
    public SKRect NextButtonBounds { get; init; }

    /// <summary>
    /// How selected the indicator in <paramref name="slot"/> (0 = the first shown) is, 0–1: 1 for the selected one, shared by
    /// two neighbors while the selection moves between them (the weights of all indicators add up to 1).
    /// </summary>
    public float GetSelection(int slot) => SkUiIndicatorLayout.Selection(GetItem(slot), Position, ItemCount, Wraps);

    /// <summary>The item of the indicator in <paramref name="slot"/> (0 = the first shown).</summary>
    public int GetItem(int slot) => SkUiIndicatorLayout.Item(slot, First, ItemCount, Wraps);

    /// <summary>The rectangle of the indicator in <paramref name="slot"/> (0 = the first shown), inside <see cref="Bounds"/>.</summary>
    public SKRect GetIndicatorBounds(int slot)
    {
        var start = SkUiIndicatorLayout.Start(slot, First, Position, ItemCount, Wraps, IndicatorSize, Spacing, SelectedExtraLength);
        var length = IndicatorSize + SelectedExtraLength * GetSelection(slot);
        return Orientation == StackOrientation.Horizontal
            ? new SKRect(Bounds.Left + start, Bounds.MidY - IndicatorSize / 2, Bounds.Left + start + length, Bounds.MidY + IndicatorSize / 2)
            : new SKRect(Bounds.MidX - IndicatorSize / 2, Bounds.Top + start, Bounds.MidX + IndicatorSize / 2, Bounds.Top + start + length);
    }

    /// <summary>The color of the indicator in <paramref name="slot"/>: <see cref="Color"/> blended towards <see cref="SelectedColor"/> by its selection.</summary>
    public SKColor GetColor(int slot) => SkUiIndicatorLayout.Blend(Color, SelectedColor, GetSelection(slot));
}

/// <summary>
/// What a look draws for a picker's field (<see cref="SkUiLook.DrawPickerField"/>), behind the text the control draws: the
/// chrome (underline, outline or fill) in <see cref="Bounds"/> and the chevron in <see cref="ChevronBounds"/> (at the end side:
/// the left in right-to-left layouts).
/// </summary>
/// <param name="Bounds">The field's rectangle.</param>
/// <param name="ChevronBounds">Where the chevron goes.</param>
/// <param name="Accent">The focus and open color (the color scheme's accent).</param>
/// <param name="Border">The idle underline or outline color (the color scheme's muted color).</param>
/// <param name="Foreground">The chevron color (the picker's text color).</param>
/// <param name="Focused">How focused the field is, 0–1 (keyboard focus or an open popup), animated by <see cref="SkUiTransitionKind.FieldFocus"/>.</param>
/// <param name="Open">How open the popup is, 0–1 (the chevron turns), animated by <see cref="SkUiTransitionKind.PickerOpen"/>.</param>
/// <param name="Press">Press amount and point.</param>
/// <param name="IsPointerOver">A hovering pointer is over the field.</param>
/// <param name="IsEnabled">The field is enabled.</param>
/// <param name="IsRightToLeft">The field lays out right to left.</param>
public readonly record struct SkUiPickerFieldPaint(
    SKRect Bounds, SKRect ChevronBounds, SKColor Accent, SKColor Border, SKColor Foreground, float Focused, float Open, SkUiPressVisual Press,
    bool IsPointerOver, bool IsEnabled, bool IsRightToLeft)
{
    /// <summary>The picker's own field style (its <c>FieldStyle</c>); <see cref="SkUiPickerFieldStyle.Default"/>: the look's.</summary>
    public SkUiPickerFieldStyle Style { get; init; }

    /// <summary>The picker's own idle chrome over its style's (its <c>FillColor</c>, <c>BorderColor</c>, <c>BorderWidth</c>, <c>CornerRadii</c>).</summary>
    public SkUiFieldChrome Chrome { get; init; }
}

/// <summary>
/// The idle chrome a field (a text field, a search bar, a picker) sets over its style's: its <c>FillColor</c>, <c>BorderColor</c>,
/// <c>BorderWidth</c> and <c>CornerRadii</c>, each <c>null</c> for the style's. Focus, error and disabled emphasis stay the look's:
/// a focused outline is still thicker and in the accent color, an error still draws in the error color, a disabled field is dimmed.
/// </summary>
/// <param name="Fill">The field's fill in place of its style's (none for an outline or an underline).</param>
/// <param name="Border">The idle outline or underline color in place of the color scheme's muted color.</param>
/// <param name="BorderWidth">The idle outline or underline width in DIPs (0: none while idle); the look thickens it while focused.</param>
/// <param name="CornerRadii">The corner radii in DIPs in place of the look's (<see cref="DefaultSkUiLook.FieldCornerRadius"/>).</param>
public readonly record struct SkUiFieldChrome(SKColor? Fill, SKColor? Border, float? BorderWidth, CornerRadius? CornerRadii);

/// <summary>What a look draws a text field's chrome with (<see cref="SkUiLook.DrawTextField"/>).</summary>
public readonly record struct SkUiTextFieldPaint
{
    /// <summary>The field's rectangle.</summary>
    public SKRect Bounds { get; init; }

    /// <summary>The focus color (the color scheme's accent).</summary>
    public SKColor Accent { get; init; }

    /// <summary>The idle underline or outline color (the color scheme's muted color).</summary>
    public SKColor Border { get; init; }

    /// <summary>The field's text color.</summary>
    public SKColor Foreground { get; init; }

    /// <summary>How focused the field is, 0–1, animated by <see cref="SkUiTransitionKind.FieldFocus"/>.</summary>
    public float Focused { get; init; }

    /// <summary>A hovering pointer is over the field.</summary>
    public bool IsPointerOver { get; init; }

    /// <summary>The field is enabled.</summary>
    public bool IsEnabled { get; init; }

    /// <summary>The field's text cannot be changed.</summary>
    public bool IsReadOnly { get; init; }

    /// <summary>The field is an editor (several lines).</summary>
    public bool IsMultiline { get; init; }

    /// <summary>The field lays out right to left.</summary>
    public bool IsRightToLeft { get; init; }

    /// <summary>
    /// The field's input is invalid (its <c>HasError</c>): drawn in <see cref="Error"/> instead of the border and accent colors.
    /// </summary>
    public bool HasError { get; init; }

    /// <summary>The error color (the field's <c>ErrorColor</c>, else the color scheme's <see cref="SkUiColorScheme.Error"/>).</summary>
    public SKColor Error { get; init; }

    /// <summary>The field's own style (its <c>FieldStyle</c>); <see cref="SkUiTextFieldStyle.Default"/>: the look's.</summary>
    public SkUiTextFieldStyle Style { get; init; }

    /// <summary>The field's own idle chrome over its style's (its <c>FillColor</c>, <c>BorderColor</c>, <c>BorderWidth</c>, <c>CornerRadii</c>).</summary>
    public SkUiFieldChrome Chrome { get; init; }
}

/// <summary>What a look draws a handle of a touch selection with (<see cref="SkUiLook.DrawTextSelectionHandle"/>).</summary>
public readonly record struct SkUiTextSelectionHandlePaint
{
    /// <summary>The selection's edge the handle belongs to: a zero-width rectangle as tall as its line (field coordinates).</summary>
    public SKRect Edge { get; init; }

    /// <summary>The selection's start (else its end).</summary>
    public bool IsStart { get; init; }

    /// <summary>The handle's color (the field's caret color).</summary>
    public SKColor Color { get; init; }
}

/// <summary>What a look draws the handle under a text field's caret with (<see cref="SkUiLook.DrawTextInsertionHandle"/>).</summary>
public readonly record struct SkUiTextInsertionHandlePaint
{
    /// <summary>The caret's rectangle, field coordinates.</summary>
    public SKRect Caret { get; init; }

    /// <summary>The caret's color.</summary>
    public SKColor Color { get; init; }
}

/// <summary>What a look draws a search bar's field with (<see cref="SkUiLook.DrawSearchField"/>).</summary>
public readonly record struct SkUiSearchFieldPaint
{
    /// <summary>The field's rectangle.</summary>
    public SKRect Bounds { get; init; }

    /// <summary>The search icon's room (at the start side).</summary>
    public SKRect IconBounds { get; init; }

    /// <summary>The focus color (the color scheme's accent).</summary>
    public SKColor Accent { get; init; }

    /// <summary>The field's fill color (the color scheme's muted color).</summary>
    public SKColor Fill { get; init; }

    /// <summary>The search icon's color (the search bar's <c>SearchIconColor</c>).</summary>
    public SKColor IconColor { get; init; }

    /// <summary>How focused the field is, 0–1, animated by <see cref="SkUiTransitionKind.FieldFocus"/>.</summary>
    public float Focused { get; init; }

    /// <summary>A hovering pointer is over the field.</summary>
    public bool IsPointerOver { get; init; }

    /// <summary>The field is enabled.</summary>
    public bool IsEnabled { get; init; }

    /// <summary>The field lays out right to left (the icon on the right).</summary>
    public bool IsRightToLeft { get; init; }

    /// <summary>The search bar's input is invalid (its <c>HasError</c>): outlined in <see cref="Error"/>.</summary>
    public bool HasError { get; init; }

    /// <summary>The error color (the field's <c>ErrorColor</c>, else the color scheme's <see cref="SkUiColorScheme.Error"/>).</summary>
    public SKColor Error { get; init; }

    /// <summary>
    /// The search bar's own field style (its <c>FieldStyle</c>). <see cref="SkUiTextFieldStyle.Default"/>: the look's search field.
    /// Any other: the control has drawn a text field of that style (<see cref="SkUiLook.DrawTextField"/>) and the search field
    /// draws its icon only.
    /// </summary>
    public SkUiTextFieldStyle Style { get; init; }

    /// <summary>The search bar's own idle chrome over the search field's (its <c>FillColor</c>, <c>BorderColor</c>, <c>BorderWidth</c>, <c>CornerRadii</c>).</summary>
    public SkUiFieldChrome Chrome { get; init; }
}

/// <summary>What a look draws a stepper with (<see cref="SkUiLook.DrawStepper"/>).</summary>
public readonly record struct SkUiStepperPaint
{
    /// <summary>The stepper's rectangle.</summary>
    public SKRect Bounds { get; init; }

    /// <summary>The − button (at the start side, or at the bottom when vertical).</summary>
    public SKRect DecrementBounds { get; init; }

    /// <summary>The + button (at the end side, or on top when vertical).</summary>
    public SKRect IncrementBounds { get; init; }

    /// <summary>Whether the buttons sit side by side or stacked.</summary>
    public StackOrientation Orientation { get; init; }

    /// <summary>The focus color (the color scheme's accent).</summary>
    public SKColor Accent { get; init; }

    /// <summary>The segments' tint and the divider's color (the color scheme's muted color).</summary>
    public SKColor Border { get; init; }

    /// <summary>The − and + color (the stepper's text color).</summary>
    public SKColor Foreground { get; init; }

    /// <summary>How pressed the − button is, 0–1.</summary>
    public float DecrementPressed { get; init; }

    /// <summary>How pressed the + button is, 0–1.</summary>
    public float IncrementPressed { get; init; }

    /// <summary>The − button can step (the value is above the minimum).</summary>
    public bool CanDecrement { get; init; }

    /// <summary>The + button can step (the value is below the maximum).</summary>
    public bool CanIncrement { get; init; }

    /// <summary>How focused the stepper is, 0–1, animated by <see cref="SkUiTransitionKind.FieldFocus"/>.</summary>
    public float Focused { get; init; }

    /// <summary>A hovering pointer is over the stepper.</summary>
    public bool IsPointerOver { get; init; }

    /// <summary>The stepper is enabled.</summary>
    public bool IsEnabled { get; init; }

    /// <summary>The stepper lays out right to left (+ on the left when horizontal).</summary>
    public bool IsRightToLeft { get; init; }

    /// <summary>The control draws a text of its own on the − button (its <c>DecrementText</c>): no − glyph.</summary>
    public bool HasDecrementText { get; init; }

    /// <summary>The control draws a text of its own on the + button (its <c>IncrementText</c>): no + glyph.</summary>
    public bool HasIncrementText { get; init; }

    /// <summary>
    /// The stepper's corner radius in DIPs (its <c>CornerRadius</c>, or <see cref="SkUiLook.DefaultStepperCornerRadius"/>).
    /// </summary>
    public float CornerRadius { get; init; }

    /// <summary>The outline's width in DIPs (its <c>BorderWidth</c>); 0: no outline.</summary>
    public float BorderWidth { get; init; }

    /// <summary>The outline's color (its <c>BorderColor</c>, or the color scheme's muted color).</summary>
    public SKColor BorderColor { get; init; }
}

/// <summary>What a look draws a numeric entry's − and + buttons with (<see cref="SkUiLook.DrawTextFieldStepButtons"/>).</summary>
public readonly record struct SkUiTextFieldStepButtonsPaint
{
    /// <summary>The field's rectangle (its chrome's bounds; the buttons are inside it).</summary>
    public SKRect Field { get; init; }

    /// <summary>The − button.</summary>
    public SKRect DecrementBounds { get; init; }

    /// <summary>The + button.</summary>
    public SKRect IncrementBounds { get; init; }

    /// <summary>Where the buttons sit: at the end side, the start side, or split (− at the start or bottom, + at the end or top).</summary>
    public SkUiStepButtonPlacement Placement { get; init; }

    /// <summary>
    /// Side by side (<see cref="StackOrientation.Horizontal"/>) or stacked (<see cref="StackOrientation.Vertical"/>): + above − in one
    /// column, or with <see cref="SkUiStepButtonPlacement.Split"/>, bars across the field above and below the text.
    /// </summary>
    public StackOrientation Orientation { get; init; }

    /// <summary>The dividers' and the pressed tint's color (the color scheme's muted color).</summary>
    public SKColor Border { get; init; }

    /// <summary>The − and + color (the entry's <c>ButtonTextColor</c>, else its text color).</summary>
    public SKColor Foreground { get; init; }

    /// <summary>How pressed the − button is, 0–1.</summary>
    public float DecrementPressed { get; init; }

    /// <summary>How pressed the + button is, 0–1.</summary>
    public float IncrementPressed { get; init; }

    /// <summary>The − button can step (above the minimum, or the value wraps).</summary>
    public bool CanDecrement { get; init; }

    /// <summary>The + button can step.</summary>
    public bool CanIncrement { get; init; }

    /// <summary>The entry is enabled.</summary>
    public bool IsEnabled { get; init; }

    /// <summary>The entry lays out right to left (the rectangles are already mirrored).</summary>
    public bool IsRightToLeft { get; init; }

    /// <summary>The entry draws a text of its own on the − button (its <c>DecrementText</c>): no − glyph.</summary>
    public bool HasDecrementText { get; init; }

    /// <summary>The entry draws a text of its own on the + button (its <c>IncrementText</c>): no + glyph.</summary>
    public bool HasIncrementText { get; init; }
}

/// <summary>
/// What a look draws a field's clear button with: a picker's (<see cref="SkUiLook.DrawPickerClearButton"/>) or a text field's
/// (<see cref="SkUiLook.DrawTextFieldClearButton"/>).
/// </summary>
/// <param name="Bounds">The button's room in the field (at the end side; a picker's before its chevron).</param>
/// <param name="Color">The button's color (the color scheme's muted color, or a text field's <c>ClearButtonColor</c>).</param>
/// <param name="Background">The field's background color (the cross's color in the default look).</param>
/// <param name="IsPressed">The button is pressed.</param>
/// <param name="IsEnabled">The field is enabled.</param>
public readonly record struct SkUiClearButtonPaint(SKRect Bounds, SKColor Color, SKColor Background, bool IsPressed, bool IsEnabled);

/// <summary>A button of a popup's footer (<see cref="SkUiLook.StylePopupButton"/>).</summary>
public enum SkUiPopupButton
{
    /// <summary>OK: applies what was chosen in the popup.</summary>
    Accept,
    /// <summary>Cancel: closes, discarding what was chosen in the popup.</summary>
    Cancel,
    /// <summary>Clear: clears the choice.</summary>
    Clear
}

/// <summary>What a look draws a picker field's token with (<see cref="SkUiLook.DrawPickerToken"/>).</summary>
/// <param name="Bounds">The token's rectangle in the field.</param>
/// <param name="RemoveBounds">Its ×'s room at the end side; empty when it cannot be removed (an item that cannot be chosen, "+N").</param>
/// <param name="Fill">The token's background (the look's popup highlight color).</param>
/// <param name="Foreground">The ×'s color (the color scheme's muted color).</param>
/// <param name="IsRemovePressed">The × is pressed.</param>
/// <param name="IsEnabled">The field is enabled.</param>
/// <param name="IsMore">The token stands for the chosen items that do not fit ("+N").</param>
public readonly record struct SkUiPickerTokenPaint(SKRect Bounds, SKRect RemoveBounds, SKColor Fill, SKColor Foreground, bool IsRemovePressed, bool IsEnabled,
    bool IsMore);
