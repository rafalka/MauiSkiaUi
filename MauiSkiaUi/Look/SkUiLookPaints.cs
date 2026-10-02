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
/// <param name="Fill">Fill color (unpressed).</param>
/// <param name="Border">Border color.</param>
/// <param name="BorderWidth">Border width (drawn inside the bounds).</param>
/// <param name="Press">Press amount, press point and ripple.</param>
/// <param name="IsEnabled">The button is enabled and its command can execute.</param>
public readonly record struct SkUiButtonPaint(
    SKRect Bounds, CornerRadius CornerRadii, SKColor Fill, SKColor Border, float BorderWidth, SkUiPressVisual Press, bool IsEnabled);

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
