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
/// <param name="IsPressed">The thumb is being dragged.</param>
/// <param name="IsEnabled">The control is enabled.</param>
public readonly record struct SkUiSliderPaint(
    SKRect Bounds, float Fraction, StackOrientation Orientation, SKColor MinimumTrack, SKColor MaximumTrack, SKColor Thumb,
    bool IsPressed, bool IsEnabled);

/// <summary>
/// What a look draws for a ProgressBar (<see cref="SkUiLook.DrawProgressBar"/>), in left-to-right coordinates.
/// Colors already reflect the enabled state.
/// </summary>
/// <param name="Bounds">The bar's rectangle.</param>
/// <param name="Progress">Completed fraction (0–1); unused when <see cref="IsIndeterminate"/>.</param>
/// <param name="IsIndeterminate">Draw the track and one moving segment at the start (the compositor slides it).</param>
/// <param name="Track">Track (remaining) color.</param>
/// <param name="Fill">Progress color.</param>
/// <param name="SegmentFraction">Indeterminate segment length as a fraction of the bar.</param>
/// <param name="IsEnabled">The control is enabled.</param>
public readonly record struct SkUiProgressBarPaint(
    SKRect Bounds, float Progress, bool IsIndeterminate, SKColor Track, SKColor Fill, float SegmentFraction, bool IsEnabled);
