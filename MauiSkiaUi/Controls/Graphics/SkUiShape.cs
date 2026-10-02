using System.Numerics;
using MauiSkiaUi.Core;
using Microsoft.Maui.Controls.Shapes;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// Base class of the drawn shapes (<see cref="SkUiEllipse"/>, <see cref="SkUiLine"/>, <see cref="SkUiRectangle"/>,
/// <see cref="SkUiRoundRectangle"/>, <see cref="SkUiPath"/>, <see cref="SkUiPolygon"/>, <see cref="SkUiPolyline"/>) with
/// MAUI's <c>Shape</c> API: <see cref="Fill"/> and <see cref="Stroke"/> brushes (solid colors and gradients),
/// <see cref="StrokeThickness"/>, dashes, caps, joins and <see cref="Aspect"/>. Shapes measure, stretch and place their
/// geometry as MAUI's shapes do, and are MAUI <see cref="IShape"/>s, so they also shape a <see cref="SkUiBorder"/>.
/// </summary>
/// <remarks>Input uses the rectangular arranged bounds. <c>ImageBrush</c> fills and strokes are not drawn.</remarks>
public abstract class SkUiShape : SkUiView, IShape, ISkUiShapeOutline
{
    private Brush? _fill;
    private Brush? _stroke;
    private double _strokeThickness = 1;
    private double _strokeDashOffset;
    private PenLineCap _strokeLineCap;
    private PenLineJoin _strokeLineJoin;
    private double _strokeMiterLimit = 10;
    private Stretch _aspect;
    private readonly SkUiWeakListener<SkUiShape> _fillListener;
    private readonly SkUiWeakListener<SkUiShape> _strokeListener;
    private readonly SkUiWeakListener<SkUiShape> _dashListener;

    /// <summary>Bindable <see cref="Fill"/>.</summary>
    public static readonly BindableProperty FillProperty = BindableProperty.Create(nameof(Fill), typeof(Brush), typeof(SkUiShape), null,
        propertyChanged: (view, _, value) => ((SkUiShape)view).OnFillChanged((Brush?)value));
    /// <summary>Bindable <see cref="Stroke"/>.</summary>
    public static readonly BindableProperty StrokeProperty = BindableProperty.Create(nameof(Stroke), typeof(Brush), typeof(SkUiShape), null,
        propertyChanged: (view, _, value) => ((SkUiShape)view).OnStrokeChanged((Brush?)value));
    /// <summary>Bindable <see cref="StrokeThickness"/>.</summary>
    public static readonly BindableProperty StrokeThicknessProperty = BindableProperty.Create(nameof(StrokeThickness), typeof(double), typeof(SkUiShape), 1d,
        validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, value) => ((SkUiShape)view).OnStrokeThicknessChanged((double)value));
    /// <summary>Bindable <see cref="StrokeDashArray"/>.</summary>
    public static readonly BindableProperty StrokeDashArrayProperty = BindableProperty.Create(nameof(StrokeDashArray), typeof(DoubleCollection), typeof(SkUiShape), null,
        defaultValueCreator: _ => new DoubleCollection(),
        propertyChanged: (view, _, value) => ((SkUiShape)view).OnStrokeDashArrayChanged((DoubleCollection?)value));
    /// <summary>Bindable <see cref="StrokeDashOffset"/>.</summary>
    public static readonly BindableProperty StrokeDashOffsetProperty = BindableProperty.Create(nameof(StrokeDashOffset), typeof(double), typeof(SkUiShape), 0d,
        validateValue: SkUiValidate.Finite,
        propertyChanged: (view, _, value) => ((SkUiShape)view).OnStrokeDashOffsetChanged((double)value));
    /// <summary>Bindable <see cref="StrokeLineCap"/>.</summary>
    public static readonly BindableProperty StrokeLineCapProperty = BindableProperty.Create(nameof(StrokeLineCap), typeof(PenLineCap), typeof(SkUiShape), PenLineCap.Flat,
        propertyChanged: (view, _, value) => ((SkUiShape)view).OnStrokeLineCapChanged((PenLineCap)value));
    /// <summary>Bindable <see cref="StrokeLineJoin"/>.</summary>
    public static readonly BindableProperty StrokeLineJoinProperty = BindableProperty.Create(nameof(StrokeLineJoin), typeof(PenLineJoin), typeof(SkUiShape), PenLineJoin.Miter,
        propertyChanged: (view, _, value) => ((SkUiShape)view).OnStrokeLineJoinChanged((PenLineJoin)value));
    /// <summary>Bindable <see cref="StrokeMiterLimit"/>.</summary>
    public static readonly BindableProperty StrokeMiterLimitProperty = BindableProperty.Create(nameof(StrokeMiterLimit), typeof(double), typeof(SkUiShape), 10d,
        validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, value) => ((SkUiShape)view).OnStrokeMiterLimitChanged((double)value));
    /// <summary>Bindable <see cref="Aspect"/>.</summary>
    public static readonly BindableProperty AspectProperty = BindableProperty.Create(nameof(Aspect), typeof(Stretch), typeof(SkUiShape), Stretch.None,
        defaultValueCreator: view => ((SkUiShape)view).DefaultAspect,
        propertyChanged: (view, _, value) => ((SkUiShape)view).OnAspectChanged((Stretch)value));

    /// <summary>Creates a shape with no fill and no stroke, as MAUI's shapes.</summary>
    protected SkUiShape()
    {
        _aspect = DefaultAspect;
        _fillListener = new(this, static (shape, _, _) => shape.InvalidatePaint());
        _strokeListener = new(this, static (shape, _, _) => shape.InvalidatePaint());
        _dashListener = new(this, static (shape, _, _) => shape.InvalidatePaint());
    }

    /// <summary>The brush that paints the interior (<c>null</c>: none). XAML accepts colors and gradients (<c>Fill="Red"</c>).</summary>
    public Brush? Fill { get => (Brush?)GetValue(FillProperty); set => SetValue(FillProperty, value); }
    /// <summary>The brush that paints the outline (<c>null</c>: none).</summary>
    public Brush? Stroke { get => (Brush?)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    /// <summary>The outline width in DIPs (1 by default, as MAUI).</summary>
    public double StrokeThickness { get => (double)GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
    /// <summary>Dashes and gaps of the outline, in multiples of <see cref="StrokeThickness"/> (empty: solid). XAML: <c>"4,2"</c>.</summary>
    public DoubleCollection StrokeDashArray { get => (DoubleCollection)GetValue(StrokeDashArrayProperty); set => SetValue(StrokeDashArrayProperty, value); }
    /// <summary>Where the dash pattern starts, in multiples of <see cref="StrokeThickness"/>.</summary>
    public double StrokeDashOffset { get => (double)GetValue(StrokeDashOffsetProperty); set => SetValue(StrokeDashOffsetProperty, value); }
    /// <summary>The caps at the ends of open figures and dashes.</summary>
    public PenLineCap StrokeLineCap { get => (PenLineCap)GetValue(StrokeLineCapProperty); set => SetValue(StrokeLineCapProperty, value); }
    /// <summary>The joins at the vertices of the outline.</summary>
    public PenLineJoin StrokeLineJoin { get => (PenLineJoin)GetValue(StrokeLineJoinProperty); set => SetValue(StrokeLineJoinProperty, value); }
    /// <summary>The limit on the ratio of a miter join's length to half the stroke thickness (10 by default).</summary>
    public double StrokeMiterLimit { get => (double)GetValue(StrokeMiterLimitProperty); set => SetValue(StrokeMiterLimitProperty, value); }
    /// <summary>
    /// How the geometry is fitted into the bounds: <see cref="Stretch.None"/> by default; rectangles and ellipses default
    /// to <see cref="Stretch.Fill"/>, as in MAUI.
    /// </summary>
    public Stretch Aspect { get => (Stretch)GetValue(AspectProperty); set => SetValue(AspectProperty, value); }

    /// <summary>Sets the fill brush (same as the property setter).</summary>
    public SkUiShape SetFill(Brush? value) { Fill = value; return this; }
    /// <summary>Sets the stroke brush (same as the property setter).</summary>
    public SkUiShape SetStroke(Brush? value) { Stroke = value; return this; }
    /// <summary>Sets the stroke thickness (same as the property setter).</summary>
    public SkUiShape SetStrokeThickness(double value) { SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value)); StrokeThickness = value; return this; }
    /// <summary>Sets the dash pattern, in multiples of the stroke thickness (no values: solid).</summary>
    public SkUiShape SetStrokeDashArray(params double[] value) { StrokeDashArray = [.. value]; return this; }
    /// <summary>Sets the dash offset (same as the property setter).</summary>
    public SkUiShape SetStrokeDashOffset(double value) { SkUiValidate.ThrowIfNotFinite(value, nameof(value)); StrokeDashOffset = value; return this; }
    /// <summary>Sets the line caps (same as the property setter).</summary>
    public SkUiShape SetStrokeLineCap(PenLineCap value) { StrokeLineCap = value; return this; }
    /// <summary>Sets the line joins (same as the property setter).</summary>
    public SkUiShape SetStrokeLineJoin(PenLineJoin value) { StrokeLineJoin = value; return this; }
    /// <summary>Sets the miter limit (same as the property setter).</summary>
    public SkUiShape SetStrokeMiterLimit(double value) { SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value)); StrokeMiterLimit = value; return this; }
    /// <summary>Sets the aspect (same as the property setter).</summary>
    public SkUiShape SetAspect(Stretch value) { Aspect = value; return this; }

    private void OnFillChanged(Brush? value)
    {
        _fill = value;
        _fillListener.Listen(value);
        InheritBindingContext(value);
        InvalidatePaint();
    }

    private void OnStrokeChanged(Brush? value)
    {
        _stroke = value;
        _strokeListener.Listen(value);
        InheritBindingContext(value);
        InvalidatePaint();
    }

    private void OnStrokeThicknessChanged(double value) { if (_strokeThickness == value) return; _strokeThickness = value; InvalidateMeasureOverride(); }
    private void OnStrokeDashArrayChanged(DoubleCollection? value) { _dashListener.Listen(value); InvalidatePaint(); }
    private void OnStrokeDashOffsetChanged(double value) { _strokeDashOffset = value; InvalidatePaint(); }
    private void OnStrokeLineCapChanged(PenLineCap value) { _strokeLineCap = value; InvalidatePaint(); }
    private void OnStrokeLineJoinChanged(PenLineJoin value) { _strokeLineJoin = value; InvalidatePaint(); }
    private void OnStrokeMiterLimitChanged(double value) { _strokeMiterLimit = value; InvalidatePaint(); }
    private void OnAspectChanged(Stretch value) { if (_aspect == value) return; _aspect = value; InvalidateMeasureOverride(); }

    /// <inheritdoc />
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        InheritBindingContext(_fill);
        InheritBindingContext(_stroke);
    }

    /// <summary>
    /// Brushes bind against the shape's binding context, as in MAUI (gradient stops bound to a view model); MAUI's shared
    /// immutable brushes (<c>Brush.Red</c>, …) are subclasses and are left alone.
    /// </summary>
    private void InheritBindingContext(Brush? brush)
    {
        if (brush is GradientBrush || brush?.GetType() == typeof(SolidColorBrush))
            SetInheritedBindingContext(brush, BindingContext);
    }

    /// <summary>The <see cref="Aspect"/> until one is set (<see cref="Stretch.Fill"/> for rectangles and ellipses).</summary>
    private protected virtual Stretch DefaultAspect => Stretch.None;

    /// <summary>
    /// Whether the geometry is the drawing area itself (rectangles and ellipses: <see cref="CreatePath"/> gets the bounds
    /// inset by half the stroke) instead of coordinates placed into it by <see cref="Aspect"/>.
    /// </summary>
    private protected virtual bool SizesToBounds => false;

    /// <summary>The fill rule of the geometry.</summary>
    private protected virtual WindingMode Winding => WindingMode.NonZero;

    /// <summary>A transform applied to the placed geometry when drawing (MAUI's <c>Path.RenderTransform</c>).</summary>
    private protected virtual Matrix3x2? GeometryTransform => null;

    /// <summary>The geometry (MAUI's <c>GetPath</c>): in <paramref name="area"/> when <see cref="SizesToBounds"/>, else in its own coordinates.</summary>
    private protected abstract PathF CreatePath(Rect area);

    /// <summary>The geometry placed in <paramref name="bounds"/> with a stroke of <paramref name="strokeThickness"/>.</summary>
    private PathF PlacedPath(Rect bounds, double strokeThickness)
    {
        if (SizesToBounds)
            return CreatePath(bounds.Inflate(-strokeThickness / 2, -strokeThickness / 2));
        var path = CreatePath(bounds);
        SkUiShapeGeometry.PlaceInBounds(path, bounds, strokeThickness, (SkUiCoreStretch)_aspect);
        return path;
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => SkUiShapeGeometry.Measure(
        SizesToBounds ? default : SkUiShapeGeometry.Bounds(CreatePath(default)), (SkUiCoreStretch)_aspect, _strokeThickness, widthConstraint, heightConstraint);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        Paint? fill = _fill; // MAUI's conversion: solid, linear and radial paints (null stays null)
        Paint? stroke = _stroke;
        var stroked = _strokeThickness > 0 && SkUiShapePainter.IsVisible(stroke);
        if (!stroked && !SkUiShapePainter.IsVisible(fill)) return;
        var bounds = new Rect(0, 0, Width, Height);
        using var path = SkUiShapeGeometry.ToSkia(PlacedPath(bounds, _strokeThickness), Winding, GeometryTransform);
        SkUiShapePainter.Draw(canvas, path, fill, new SKRect(0, 0, (float)Width, (float)Height), stroked ? stroke : null, StrokeStyle());
    }

    private SkUiStrokeStyle StrokeStyle()
    {
        var dashArray = StrokeDashArray;
        _dashListener.Listen(dashArray); // the default collection is created on first read
        float[]? dashes = null;
        if (dashArray is { Count: > 0 })
        {
            dashes = new float[dashArray.Count];
            for (var index = 0; index < dashes.Length; index++)
                dashes[index] = (float)dashArray[index];
        }
        return new SkUiStrokeStyle(_strokeThickness, dashes, _strokeDashOffset, _strokeLineCap switch
        {
            PenLineCap.Round => LineCap.Round,
            PenLineCap.Square => LineCap.Square,
            _ => LineCap.Butt
        }, _strokeLineJoin switch
        {
            PenLineJoin.Round => LineJoin.Round,
            PenLineJoin.Bevel => LineJoin.Bevel,
            _ => LineJoin.Miter
        }, _strokeMiterLimit);
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
