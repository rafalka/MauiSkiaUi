using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// A resolved fill of either layer without allocating for the common case: a solid <see cref="Color"/>, or a
/// <see cref="Gradient"/> (then <see cref="Color"/> holds its stops averaged, for color-only looks and contrast). Controls
/// resolve their background into one on every recording, so a solid fill must not allocate a MAUI Graphics paint.
/// </summary>
internal readonly record struct SkUiFill(SKColor Color, GradientPaint? Gradient)
{
    /// <summary>A MAUI Graphics paint as a fill: solid colors and gradients; anything else draws nothing (and warns once).</summary>
    public static SkUiFill From(Paint? paint)
    {
        switch (paint)
        {
            case SolidPaint { Color: { } color }:
                return new SkUiFill(SkUiShapePainter.ToSkColor(color), null);
            case GradientPaint { GradientStops.Length: > 0 } gradient:
                return new SkUiFill(SkUiShapePainter.RepresentativeColor(gradient), gradient);
            case null or SolidPaint or GradientPaint:
                return default;
            default:
                SkUiShapePainter.WarnUnsupported(paint);
                return default;
        }
    }

    /// <summary>A solid fill of <paramref name="color"/>.</summary>
    public static SkUiFill From(Color color) => new(SkUiShapePainter.ToSkColor(color), null);

    /// <summary>Whether anything is drawn.</summary>
    public bool IsVisible => Gradient is not null || Color.Alpha > 0;

    /// <summary>Whether it covers what it fills (an opaque color, or a gradient of opaque stops).</summary>
    public bool IsOpaque => Gradient is { } gradient ? SkUiShapePainter.IsOpaque(gradient) : Color.Alpha == 255;

    /// <summary>The fill as a MAUI Graphics paint (allocates for solid colors: for paths that are not per recording).</summary>
    public Paint? ToPaint() => (Paint?)Gradient ?? (Color.Alpha > 0
        ? new SolidPaint(new Color(Color.Red / 255f, Color.Green / 255f, Color.Blue / 255f, Color.Alpha / 255f))
        : null);
}
