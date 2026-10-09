using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// Filled rectangle with optional rounded corners (Core analogue of <c>SkUiBox</c>, MAUI's BoxView): fills with
/// <see cref="Color"/>, or with <see cref="SkUiCoreNode.Background"/> (solid or gradient) when no color is set, and measures
/// 40 × 40 DIPs unless sized.
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

    /// <summary>The box fills itself (rounded); its <see cref="SkUiCoreNode.Background"/> is the fill when no color is set.</summary>
    protected override void OnPaintBackground(SKCanvas canvas) { }

    /// <summary>The fill: <see cref="Color"/>, else <see cref="SkUiCoreNode.Background"/> (solid or gradient), as MAUI's BoxView.</summary>
    private SkUiFill BoxFill => _color is { } color ? SkUiFill.From(color) : SkUiFill.From(Background);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        if (BoxFill is not { IsVisible: true } fill)
            return;
        var width = (float)Frame.Width;
        var height = (float)Frame.Height;
        if (SkUiCornerRadii.HasAny(_cornerRadius))
            SkUiShapePainter.Fill(canvas, _shape.Get(width, height, _cornerRadius), fill, new SKRect(0, 0, width, height));
        else
            SkUiShapePainter.FillRect(canvas, new SKRect(0, 0, width, height), fill, antialias: true);
    }

    /// <inheritdoc />
    internal override void ReleaseDrawingResources()
    {
        base.ReleaseDrawingResources();
        _shape.Release();
    }

    /// <inheritdoc />
    internal override SKPath? CreateShadowOutline(float width, float height)
    {
        if (PaintBackground is not null || !BoxFill.IsOpaque)
            return null;
        return SkUiCornerRadii.HasAny(_cornerRadius) ? new SKPath(_shape.Get(width, height, _cornerRadius)) : RectangleOutline(width, height);
    }
}
