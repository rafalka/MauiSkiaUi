using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// Filled rectangle with optional rounded corners (Core analogue of <c>SkUiBox</c>, MAUI's BoxView): fills with
/// <see cref="Color"/> and measures 40 × 40 DIPs unless sized.
/// </summary>
public class SkUiCoreBox : SkUiCoreNode
{
    private Color? _color;
    private CornerRadius _cornerRadius;
    private SkUiRoundedClip _shape;

    /// <summary>The fill color (<c>null</c>: nothing is drawn, as MAUI's BoxView without a color or background).</summary>
    public Color? Color
    {
        get => _color;
        set => SetColor(value);
    }

    /// <summary>Per-corner radii in DIPs. Larger radii than the box allows are scaled down.</summary>
    public CornerRadius CornerRadius
    {
        get => _cornerRadius;
        set => SetCornerRadius(value);
    }

    /// <summary>Sets the fill color.</summary>
    public SkUiCoreBox SetColor(Color? value)
    {
        if (!SetProperty(ref _color, value, nameof(Color))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets the corner radii (a number converts to the same radius on every corner).</summary>
    public SkUiCoreBox SetCornerRadius(CornerRadius value)
    {
        SkUiCornerRadii.Validate(value, nameof(value));
        if (!SetProperty(ref _cornerRadius, value, nameof(CornerRadius))) return this;
        InvalidatePaint();
        return this;
    }

    /// <inheritdoc />
    internal override CornerRadius PressEffectCornerRadii => _cornerRadius;

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => new(40, 40);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        if (_color is not { } color)
            return;
        using var paint = new SKPaint { Color = ToSkColor(color), IsAntialias = true };
        var width = (float)Frame.Width;
        var height = (float)Frame.Height;
        if (SkUiCornerRadii.HasAny(_cornerRadius))
            canvas.DrawPath(_shape.Get(width, height, _cornerRadius), paint);
        else
            canvas.DrawRect(0, 0, width, height, paint);
    }
}
