using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>Shared fill/stroke settings for Core shape primitives.</summary>
public abstract class SkUiCoreShape : SkUiCoreNode
{
    private Color _color = Colors.Teal;
    private double _strokeWidth = 2;

    /// <summary>Fill or stroke color.</summary>
    public Color Color
    {
        get => _color;
        set => SetColor(value);
    }

    /// <summary>Stroke width in DIPs; ignored by filled primitives.</summary>
    public double StrokeWidth
    {
        get => _strokeWidth;
        set => SetStrokeWidth(value);
    }

    /// <summary>Sets the fill or stroke color.</summary>
    public SkUiCoreShape SetColor(Color value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!SetProperty(ref _color, value, nameof(Color))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets the stroke width in DIPs.</summary>
    public SkUiCoreShape SetStrokeWidth(double value)
    {
        if (!double.IsFinite(value) || value < 0)
            throw new ArgumentOutOfRangeException(nameof(value));
        if (!SetProperty(ref _strokeWidth, value, nameof(StrokeWidth))) return this;
        if (StrokeAffectsSize)
            InvalidateMeasure();
        else
            InvalidatePaint();
        return this;
    }

    /// <summary>Whether the stroke width is part of the intrinsic size (lines).</summary>
    private protected virtual bool StrokeAffectsSize => false;

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => new(48, 48);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        using var paint = new SKPaint
        {
            Color = ToSkColor(_color),
            IsAntialias = true,
            StrokeWidth = (float)_strokeWidth
        };
        DrawShape(canvas, new SKRect(0, 0, (float)Frame.Width, (float)Frame.Height), paint);
    }

    /// <summary>Draws the primitive into its local bounds with a scoped paint.</summary>
    protected abstract void DrawShape(SKCanvas canvas, SKRect bounds, SKPaint paint);
}

/// <summary>Filled rectangle with optional rounded corners (Core analogue of <c>SkUiBox</c>, MAUI's BoxView).</summary>
public class SkUiCoreBox : SkUiCoreShape
{
    private CornerRadius _cornerRadius;
    private SkUiRoundedClip _shape;

    /// <summary>Per-corner radii in DIPs. Larger radii than the box allows are scaled down.</summary>
    public CornerRadius CornerRadius
    {
        get => _cornerRadius;
        set => SetCornerRadius(value);
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
    protected override void DrawShape(SKCanvas canvas, SKRect bounds, SKPaint paint)
    {
        if (SkUiCornerRadii.HasAny(_cornerRadius))
            canvas.DrawPath(_shape.Get(bounds.Width, bounds.Height, _cornerRadius), paint);
        else
            canvas.DrawRect(bounds, paint);
    }
}

/// <summary>Filled ellipse; input uses rectangular arranged bounds.</summary>
public class SkUiCoreEllipse : SkUiCoreShape
{
    /// <inheritdoc />
    protected override void DrawShape(SKCanvas canvas, SKRect bounds, SKPaint paint) =>
        canvas.DrawOval(bounds, paint);
}

/// <summary>
/// Straight line from (<see cref="X1"/>, <see cref="Y1"/>) to (<see cref="X2"/>, <see cref="Y2"/>) in local DIPs (Core
/// analogue of <c>SkUiLine</c>, MAUI's <c>Line</c>): measures to its far end points plus the stroke.
/// </summary>
public class SkUiCoreLine : SkUiCoreShape
{
    private double _x1;
    private double _y1;
    private double _x2;
    private double _y2;

    /// <summary>Creates a line; set its end points with <see cref="SetPoints"/>.</summary>
    public SkUiCoreLine() { }

    /// <summary>Creates a line between two points.</summary>
    public SkUiCoreLine(double x1, double y1, double x2, double y2) => SetPoints(x1, y1, x2, y2);

    /// <summary>The start point's x coordinate.</summary>
    public double X1 { get => _x1; set => SetX1(value); }

    /// <summary>The start point's y coordinate.</summary>
    public double Y1 { get => _y1; set => SetY1(value); }

    /// <summary>The end point's x coordinate.</summary>
    public double X2 { get => _x2; set => SetX2(value); }

    /// <summary>The end point's y coordinate.</summary>
    public double Y2 { get => _y2; set => SetY2(value); }

    /// <summary>Sets the start point's x coordinate.</summary>
    public SkUiCoreLine SetX1(double value) => SetPoint(ref _x1, value, nameof(X1));

    /// <summary>Sets the start point's y coordinate.</summary>
    public SkUiCoreLine SetY1(double value) => SetPoint(ref _y1, value, nameof(Y1));

    /// <summary>Sets the end point's x coordinate.</summary>
    public SkUiCoreLine SetX2(double value) => SetPoint(ref _x2, value, nameof(X2));

    /// <summary>Sets the end point's y coordinate.</summary>
    public SkUiCoreLine SetY2(double value) => SetPoint(ref _y2, value, nameof(Y2));

    /// <summary>Sets both end points.</summary>
    public SkUiCoreLine SetPoints(double x1, double y1, double x2, double y2) => SetX1(x1).SetY1(y1).SetX2(x2).SetY2(y2);

    private SkUiCoreLine SetPoint(ref double field, double value, string name)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(name, value, "Line coordinates must be finite.");
        if (SetProperty(ref field, value, name))
            InvalidateMeasure();
        return this;
    }

    private protected override bool StrokeAffectsSize => true;

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        SkUiShapeGeometry.MeasureLine(_x1, _y1, _x2, _y2, StrokeWidth);

    /// <inheritdoc />
    protected override void DrawShape(SKCanvas canvas, SKRect bounds, SKPaint paint) =>
        SkUiShapeGeometry.DrawLine(canvas, bounds, (float)_x1, (float)_y1, (float)_x2, (float)_y2, paint);
}
