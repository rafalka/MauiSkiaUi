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
