using System.Numerics;
using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// Base class of the Core shapes (Core analogue of <c>SkUiShape</c>, MAUI's <c>Shape</c>): <see cref="Fill"/> and
/// <see cref="Stroke"/> paints (solid colors and gradients, MAUI Graphics <see cref="Paint"/>), <see cref="StrokeThickness"/>,
/// dashes, caps, joins and <see cref="Aspect"/>, measured, stretched and placed as MAUI's shapes through the engine the
/// SkUi* shapes use. Shapes are MAUI Graphics <see cref="IShape"/>s, so they also shape a <see cref="SkUiCoreBorder"/>.
/// </summary>
/// <remarks>Input uses the rectangular arranged bounds. Image paints are not drawn.</remarks>
public abstract class SkUiCoreShape : SkUiCoreNode, IShape, ISkUiShapeOutline
{
    private Paint? _fill;
    private Paint? _stroke;
    private double _strokeThickness = 1;
    private double[] _strokeDashArray = [];
    private double _strokeDashOffset;
    private LineCap _strokeLineCap = LineCap.Butt;
    private LineJoin _strokeLineJoin = LineJoin.Miter;
    private double _strokeMiterLimit = 10;
    private SkUiCoreStretch _aspect;

    /// <summary>Creates a shape with no fill and no stroke, as MAUI's shapes.</summary>
    protected SkUiCoreShape() => _aspect = DefaultAspect;

    /// <summary>The paint of the interior (<c>null</c>: none).</summary>
    public Paint? Fill { get => _fill; set => SetFill(value); }
    /// <summary>The paint of the outline (<c>null</c>: none).</summary>
    public Paint? Stroke { get => _stroke; set => SetStroke(value); }
    /// <summary>The outline width in DIPs (1 by default, as MAUI).</summary>
    public double StrokeThickness { get => _strokeThickness; set => SetStrokeThickness(value); }
    /// <summary>Dashes and gaps of the outline, in multiples of <see cref="StrokeThickness"/> (empty: solid).</summary>
    public IReadOnlyList<double> StrokeDashArray { get => _strokeDashArray; set => SetStrokeDashArray([.. value]); }
    /// <summary>Where the dash pattern starts, in multiples of <see cref="StrokeThickness"/>.</summary>
    public double StrokeDashOffset { get => _strokeDashOffset; set => SetStrokeDashOffset(value); }
    /// <summary>The caps at the ends of open figures and dashes (<see cref="LineCap.Butt"/>: MAUI's <c>Flat</c>).</summary>
    public LineCap StrokeLineCap { get => _strokeLineCap; set => SetStrokeLineCap(value); }
    /// <summary>The joins at the vertices of the outline.</summary>
    public LineJoin StrokeLineJoin { get => _strokeLineJoin; set => SetStrokeLineJoin(value); }
    /// <summary>The limit on the ratio of a miter join's length to half the stroke thickness (10 by default).</summary>
    public double StrokeMiterLimit { get => _strokeMiterLimit; set => SetStrokeMiterLimit(value); }
    /// <summary>How the geometry is fitted into the bounds (<see cref="SkUiCoreStretch.None"/>; rectangles and ellipses: <see cref="SkUiCoreStretch.Fill"/>).</summary>
    public SkUiCoreStretch Aspect { get => _aspect; set => SetAspect(value); }

    /// <summary>Sets the fill paint.</summary>
    public SkUiCoreShape SetFill(Paint? value)
    {
        if (!SetProperty(ref _fill, value, nameof(Fill))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets a solid fill color.</summary>
    public SkUiCoreShape SetFill(Color value) => _fill is SolidPaint { Color: var color } && color == value ? this : SetFill(new SolidPaint(value));

    /// <summary>Sets the stroke paint.</summary>
    public SkUiCoreShape SetStroke(Paint? value)
    {
        if (!SetProperty(ref _stroke, value, nameof(Stroke))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets a solid stroke color.</summary>
    public SkUiCoreShape SetStroke(Color value) => _stroke is SolidPaint { Color: var color } && color == value ? this : SetStroke(new SolidPaint(value));

    /// <summary>Sets the stroke thickness in DIPs.</summary>
    public SkUiCoreShape SetStrokeThickness(double value)
    {
        SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value));
        if (!SetProperty(ref _strokeThickness, value, nameof(StrokeThickness))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <summary>Sets the dash pattern, in multiples of the stroke thickness (no values: solid).</summary>
    public SkUiCoreShape SetStrokeDashArray(params double[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (_strokeDashArray.AsSpan().SequenceEqual(value)) return this;
        _strokeDashArray = [.. value];
        OnPropertyChanged(nameof(StrokeDashArray));
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets the dash offset, in multiples of the stroke thickness.</summary>
    public SkUiCoreShape SetStrokeDashOffset(double value)
    {
        SkUiValidate.ThrowIfNotFinite(value, nameof(value));
        if (!SetProperty(ref _strokeDashOffset, value, nameof(StrokeDashOffset))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets the line caps.</summary>
    public SkUiCoreShape SetStrokeLineCap(LineCap value)
    {
        if (!SetProperty(ref _strokeLineCap, value, nameof(StrokeLineCap))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets the line joins.</summary>
    public SkUiCoreShape SetStrokeLineJoin(LineJoin value)
    {
        if (!SetProperty(ref _strokeLineJoin, value, nameof(StrokeLineJoin))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets the miter limit.</summary>
    public SkUiCoreShape SetStrokeMiterLimit(double value)
    {
        SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value));
        if (!SetProperty(ref _strokeMiterLimit, value, nameof(StrokeMiterLimit))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets the aspect.</summary>
    public SkUiCoreShape SetAspect(SkUiCoreStretch value)
    {
        if (!SetProperty(ref _aspect, value, nameof(Aspect))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <summary>The <see cref="Aspect"/> of a new shape (<see cref="SkUiCoreStretch.Fill"/> for rectangles and ellipses).</summary>
    private protected virtual SkUiCoreStretch DefaultAspect => SkUiCoreStretch.None;

    /// <summary>Whether the geometry is the drawing area itself (rectangles and ellipses), see <c>SkUiShape</c>.</summary>
    private protected virtual bool SizesToBounds => false;

    /// <summary>The fill rule of the geometry.</summary>
    private protected virtual WindingMode Winding => WindingMode.NonZero;

    /// <summary>A transform applied to the placed geometry when drawing.</summary>
    private protected virtual Matrix3x2? GeometryTransform => null;

    /// <summary>The geometry: in <paramref name="area"/> when <see cref="SizesToBounds"/>, else in its own coordinates.</summary>
    private protected abstract PathF CreatePath(Rect area);

    /// <summary>Raised by subclasses when their geometry changed.</summary>
    private protected void InvalidateGeometry() => InvalidateMeasure();

    private PathF PlacedPath(Rect bounds, double strokeThickness)
    {
        if (SizesToBounds)
            return CreatePath(bounds.Inflate(-strokeThickness / 2, -strokeThickness / 2));
        var path = CreatePath(bounds);
        SkUiShapeGeometry.PlaceInBounds(path, bounds, strokeThickness, _aspect);
        return path;
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => SkUiShapeGeometry.Measure(
        SizesToBounds ? default : SkUiShapeGeometry.Bounds(CreatePath(default)), _aspect, _strokeThickness, widthConstraint, heightConstraint);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        var stroked = _strokeThickness > 0 && SkUiShapePainter.IsVisible(_stroke);
        if (!stroked && !SkUiShapePainter.IsVisible(_fill)) return;
        var bounds = new Rect(0, 0, Frame.Width, Frame.Height);
        using var path = SkUiShapeGeometry.ToSkia(PlacedPath(bounds, _strokeThickness), Winding, GeometryTransform);
        float[]? dashes = _strokeDashArray.Length == 0 ? null : Array.ConvertAll(_strokeDashArray, value => (float)value);
        SkUiShapePainter.Draw(canvas, path, _fill, new SKRect(0, 0, (float)Frame.Width, (float)Frame.Height), stroked ? _stroke : null,
            new SkUiStrokeStyle(_strokeThickness, dashes, _strokeDashOffset, _strokeLineCap, _strokeLineJoin, _strokeMiterLimit));
    }

    /// <summary>The geometry placed in <paramref name="bounds"/> inset by half the stroke, as MAUI's <c>IShape.PathForBounds</c>.</summary>
    PathF IShape.PathForBounds(Rect bounds) => PlacedPath(bounds, _strokeThickness);

    /// <inheritdoc />
    WindingMode ISkUiShapeOutline.Winding => Winding;

    /// <inheritdoc />
    PathF ISkUiShapeOutline.GetOutline(Rect bounds) => PlacedPath(bounds, 0);

    /// <inheritdoc />
    bool ISkUiShapeOutline.TryGetRoundRect(out CornerRadius radii) => TryGetRoundRect(out radii);

    /// <summary>A rounded rectangle's radii: borders draw it with the look's rounded geometry.</summary>
    private protected virtual bool TryGetRoundRect(out CornerRadius radii)
    {
        radii = default;
        return false;
    }
}
