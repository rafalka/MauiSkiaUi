using System.Numerics;
using Microsoft.Maui.Controls.Shapes;

namespace MauiSkiaUi;

/// <summary>An ellipse or circle filling its bounds, as MAUI's <c>Ellipse</c>; input uses the rectangular bounds.</summary>
public class SkUiEllipse : SkUiShape
{
    private protected override Stretch DefaultAspect => Stretch.Fill;
    private protected override bool SizesToBounds => true;
    private protected override PathF CreatePath(Rect area) => SkUiShapeGeometry.Ellipse(area);
}

/// <summary>A rectangle filling its bounds, optionally rounded by <see cref="RadiusX"/> / <see cref="RadiusY"/>, as MAUI's <c>Rectangle</c>.</summary>
public class SkUiRectangle : SkUiShape
{
    /// <summary>Bindable <see cref="RadiusX"/>.</summary>
    public static readonly BindableProperty RadiusXProperty = BindableProperty.Create(nameof(RadiusX), typeof(double), typeof(SkUiRectangle), 0d,
        validateValue: SkUiValidate.NonNegative, propertyChanged: (view, _, _) => ((SkUiRectangle)view).InvalidatePaint());
    /// <summary>Bindable <see cref="RadiusY"/>.</summary>
    public static readonly BindableProperty RadiusYProperty = BindableProperty.Create(nameof(RadiusY), typeof(double), typeof(SkUiRectangle), 0d,
        validateValue: SkUiValidate.NonNegative, propertyChanged: (view, _, _) => ((SkUiRectangle)view).InvalidatePaint());

    /// <summary>The x-axis corner radius; as MAUI, the corners are rounded by the larger of the two radii.</summary>
    public double RadiusX { get => (double)GetValue(RadiusXProperty); set => SetValue(RadiusXProperty, value); }
    /// <summary>The y-axis corner radius.</summary>
    public double RadiusY { get => (double)GetValue(RadiusYProperty); set => SetValue(RadiusYProperty, value); }

    /// <summary>Sets the x-axis corner radius (same as the property setter).</summary>
    public SkUiRectangle SetRadiusX(double value) { SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value)); RadiusX = value; return this; }
    /// <summary>Sets the y-axis corner radius (same as the property setter).</summary>
    public SkUiRectangle SetRadiusY(double value) { SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value)); RadiusY = value; return this; }

    private protected override Stretch DefaultAspect => Stretch.Fill;
    private protected override bool SizesToBounds => true;
    private protected override PathF CreatePath(Rect area) => SkUiShapeGeometry.Rectangle(area, RadiusX, RadiusY);

    private protected override bool TryGetRoundRect(out CornerRadius radii)
    {
        radii = new CornerRadius(Math.Max(RadiusX, RadiusY));
        return true;
    }
}

/// <summary>A rectangle filling its bounds with per-corner radii, as MAUI's <c>RoundRectangle</c>.</summary>
public class SkUiRoundRectangle : SkUiShape
{
    /// <summary>Bindable <see cref="CornerRadius"/>.</summary>
    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(nameof(CornerRadius), typeof(CornerRadius), typeof(SkUiRoundRectangle), default(CornerRadius),
        validateValue: SkUiValidate.CornerRadii, propertyChanged: (view, _, _) => ((SkUiRoundRectangle)view).InvalidatePaint());

    /// <summary>Per-corner radii (top-left, top-right, bottom-left, bottom-right; <c>"10"</c> or <c>"10,10,0,0"</c> in XAML).</summary>
    public CornerRadius CornerRadius { get => (CornerRadius)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    /// <summary>Sets the corner radii (same as the property setter).</summary>
    public SkUiRoundRectangle SetCornerRadius(CornerRadius value) { SkUiCornerRadii.Validate(value, nameof(value)); CornerRadius = value; return this; }

    private protected override Stretch DefaultAspect => Stretch.Fill;
    private protected override bool SizesToBounds => true;
    private protected override PathF CreatePath(Rect area) => SkUiShapeGeometry.RoundRectangle(area, CornerRadius);

    private protected override bool TryGetRoundRect(out CornerRadius radii)
    {
        radii = CornerRadius;
        return true;
    }
}

/// <summary>
/// A straight line from (<see cref="X1"/>, <see cref="Y1"/>) to (<see cref="X2"/>, <see cref="Y2"/>) in local DIPs, as
/// MAUI's <c>Line</c>: it measures to its far end points plus the stroke and is placed by <see cref="SkUiShape.Aspect"/>.
/// Points are not mirrored in right-to-left layouts.
/// </summary>
public class SkUiLine : SkUiShape
{
    /// <summary>Bindable <see cref="X1"/>.</summary>
    public static readonly BindableProperty X1Property = PointProperty(nameof(X1));
    /// <summary>Bindable <see cref="Y1"/>.</summary>
    public static readonly BindableProperty Y1Property = PointProperty(nameof(Y1));
    /// <summary>Bindable <see cref="X2"/>.</summary>
    public static readonly BindableProperty X2Property = PointProperty(nameof(X2));
    /// <summary>Bindable <see cref="Y2"/>.</summary>
    public static readonly BindableProperty Y2Property = PointProperty(nameof(Y2));

    /// <summary>Creates a line; set its end points with <see cref="X1"/>, <see cref="Y1"/>, <see cref="X2"/>, <see cref="Y2"/>.</summary>
    public SkUiLine() { }

    /// <summary>Creates a line between two points, as MAUI's <c>Line(x1, y1, x2, y2)</c>.</summary>
    public SkUiLine(double x1, double y1, double x2, double y2)
    {
        X1 = x1;
        Y1 = y1;
        X2 = x2;
        Y2 = y2;
    }

    /// <summary>The start point's x coordinate.</summary>
    public double X1 { get => (double)GetValue(X1Property); set => SetValue(X1Property, value); }
    /// <summary>The start point's y coordinate.</summary>
    public double Y1 { get => (double)GetValue(Y1Property); set => SetValue(Y1Property, value); }
    /// <summary>The end point's x coordinate.</summary>
    public double X2 { get => (double)GetValue(X2Property); set => SetValue(X2Property, value); }
    /// <summary>The end point's y coordinate.</summary>
    public double Y2 { get => (double)GetValue(Y2Property); set => SetValue(Y2Property, value); }

    private static BindableProperty PointProperty(string name) => BindableProperty.Create(name, typeof(double), typeof(SkUiLine), 0d,
        validateValue: SkUiValidate.Finite, propertyChanged: (bindable, _, _) => ((SkUiLine)bindable).InvalidateMeasureOverride());

    private protected override PathF CreatePath(Rect area) => SkUiShapeGeometry.Line(X1, Y1, X2, Y2);
}

/// <summary>A closed figure through <see cref="SkUiPolyShape.Points"/>, as MAUI's <c>Polygon</c>.</summary>
public class SkUiPolygon : SkUiPolyShape
{
    /// <summary>Creates an empty polygon.</summary>
    public SkUiPolygon() { }

    /// <summary>Creates a polygon through <paramref name="points"/>.</summary>
    public SkUiPolygon(PointCollection points) => Points = points;

    private protected override bool Closed => true;
}

/// <summary>An open line through <see cref="SkUiPolyShape.Points"/>, as MAUI's <c>Polyline</c>.</summary>
public class SkUiPolyline : SkUiPolyShape
{
    /// <summary>Creates an empty polyline.</summary>
    public SkUiPolyline() { }

    /// <summary>Creates a polyline through <paramref name="points"/>.</summary>
    public SkUiPolyline(PointCollection points) => Points = points;

    private protected override bool Closed => false;
}

/// <summary>Points and fill rule of <see cref="SkUiPolygon"/> and <see cref="SkUiPolyline"/>.</summary>
public abstract class SkUiPolyShape : SkUiShape
{
    private readonly SkUiWeakListener<SkUiPolyShape> _pointsListener;

    /// <summary>Bindable <see cref="Points"/>.</summary>
    public static readonly BindableProperty PointsProperty = BindableProperty.Create(nameof(Points), typeof(PointCollection), typeof(SkUiPolyShape), null,
        defaultValueCreator: _ => new PointCollection(),
        propertyChanged: (view, _, value) => ((SkUiPolyShape)view).OnPointsChanged((PointCollection?)value));
    /// <summary>Bindable <see cref="FillRule"/>.</summary>
    public static readonly BindableProperty FillRuleProperty = BindableProperty.Create(nameof(FillRule), typeof(FillRule), typeof(SkUiPolyShape), FillRule.EvenOdd,
        propertyChanged: (view, _, _) => ((SkUiPolyShape)view).InvalidatePaint());

    private protected SkUiPolyShape() => _pointsListener = new(this, static (shape, _) => shape.InvalidateMeasureOverride());

    /// <summary>The vertices in local DIPs (<c>"0,0 40,0 20,30"</c> in XAML). Edits of the collection redraw.</summary>
    public PointCollection Points { get => (PointCollection)GetValue(PointsProperty); set => SetValue(PointsProperty, value); }
    /// <summary>How the interior is determined where the figure crosses itself (<see cref="FillRule.EvenOdd"/> by default).</summary>
    public FillRule FillRule { get => (FillRule)GetValue(FillRuleProperty); set => SetValue(FillRuleProperty, value); }

    /// <summary>Sets the vertices (same as the property setter).</summary>
    public SkUiPolyShape SetPoints(params Point[] value) { Points = [.. value]; return this; }
    /// <summary>Sets the fill rule (same as the property setter).</summary>
    public SkUiPolyShape SetFillRule(FillRule value) { FillRule = value; return this; }

    private void OnPointsChanged(PointCollection? value)
    {
        _pointsListener.Listen(value);
        InvalidateMeasureOverride();
    }

    /// <summary>Whether the figure closes (polygon) or stays open (polyline).</summary>
    private protected abstract bool Closed { get; }

    private protected override WindingMode Winding => FillRule == FillRule.EvenOdd ? WindingMode.EvenOdd : WindingMode.NonZero;

    private protected override PathF CreatePath(Rect area)
    {
        var points = Points;
        _pointsListener.Listen(points); // the default collection is created on first read
        return SkUiShapeGeometry.Polyline(points, Closed);
    }
}

/// <summary>
/// A shape drawn from <see cref="Data"/>, a MAUI <see cref="Geometry"/> (path markup such as <c>"M 0,0 L 20,10 Z"</c> in
/// XAML, or geometry objects), as MAUI's <c>Path</c>, with an optional <see cref="RenderTransform"/>.
/// </summary>
/// <remarks>
/// Replacing <see cref="Data"/>, its figures, or properties of a geometry object redraws; editing the segments inside an
/// existing figure does not (MAUI raises that change internally), so assign the figures again after such edits.
/// </remarks>
public class SkUiPath : SkUiShape
{
    private readonly SkUiWeakListener<SkUiPath> _dataListener;
    private readonly SkUiWeakListener<SkUiPath> _figuresListener;
    private readonly SkUiWeakListener<SkUiPath> _transformListener;

    /// <summary>Bindable <see cref="Data"/>.</summary>
    public static readonly BindableProperty DataProperty = BindableProperty.Create(nameof(Data), typeof(Geometry), typeof(SkUiPath), null,
        propertyChanged: (view, _, value) => ((SkUiPath)view).OnDataChanged((Geometry?)value));
    /// <summary>Bindable <see cref="RenderTransform"/>.</summary>
    public static readonly BindableProperty RenderTransformProperty = BindableProperty.Create(nameof(RenderTransform), typeof(Transform), typeof(SkUiPath), null,
        propertyChanged: (view, _, value) => ((SkUiPath)view).OnRenderTransformChanged((Transform?)value));

    /// <summary>Creates an empty path.</summary>
    public SkUiPath()
    {
        _dataListener = new(this, static (path, change) =>
        {
            if (change.PropertyName == nameof(PathGeometry.Figures)) path.ListenToFigures();
            path.InvalidateMeasureOverride();
        });
        _figuresListener = new(this, static (path, _) => path.InvalidateMeasureOverride());
        _transformListener = new(this, static (path, _) => path.InvalidatePaint());
    }

    /// <summary>Creates a path drawing <paramref name="data"/>.</summary>
    public SkUiPath(Geometry data) : this() => Data = data;

    /// <summary>The geometry to draw, in local DIPs (path markup in XAML).</summary>
    [System.ComponentModel.TypeConverter(typeof(PathGeometryConverter))]
    public Geometry? Data { get => (Geometry?)GetValue(DataProperty); set => SetValue(DataProperty, value); }
    /// <summary>A transform applied to the placed geometry when it is drawn (not to measure).</summary>
    public Transform? RenderTransform { get => (Transform?)GetValue(RenderTransformProperty); set => SetValue(RenderTransformProperty, value); }

    /// <summary>Sets the geometry (same as the property setter).</summary>
    public SkUiPath SetData(Geometry? value) { Data = value; return this; }
    /// <summary>Sets the geometry from path markup (<c>"M 0,0 L 20,10 Z"</c>, as the XAML attribute).</summary>
    public SkUiPath SetData(string markup)
    {
        ArgumentNullException.ThrowIfNull(markup);
        Data = (Geometry?)new PathGeometryConverter().ConvertFromInvariantString(markup);
        return this;
    }
    /// <summary>Sets the render transform (same as the property setter).</summary>
    public SkUiPath SetRenderTransform(Transform? value) { RenderTransform = value; return this; }

    private void OnDataChanged(Geometry? value)
    {
        if (!ReferenceEquals(_dataListener.Source, value))
            ReleaseBindingContext(_dataListener.Source as BindableObject);
        _dataListener.Listen(value);
        ListenToFigures();
        // Bindings inside the geometry (a bound radius, center or figure) resolve against the path, as in MAUI.
        if (value is not null)
            SetInheritedBindingContext(value, BindingContext);
        InvalidateMeasureOverride();
    }

    private void ListenToFigures() => _figuresListener.Listen((Data as PathGeometry)?.Figures);

    private void OnRenderTransformChanged(Transform? value)
    {
        if (!ReferenceEquals(_transformListener.Source, value))
            ReleaseBindingContext(_transformListener.Source as BindableObject);
        _transformListener.Listen(value);
        if (value is not null)
            SetInheritedBindingContext(value, BindingContext);
        InvalidatePaint();
    }

    /// <inheritdoc />
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        if (Data is { } data)
            SetInheritedBindingContext(data, BindingContext);
        if (RenderTransform is { } transform)
            SetInheritedBindingContext(transform, BindingContext);
    }

    /// <summary>As MAUI: the fill rule of a path or group geometry, else even-odd.</summary>
    private protected override WindingMode Winding => Data switch
    {
        PathGeometry geometry => geometry.FillRule == FillRule.Nonzero ? WindingMode.NonZero : WindingMode.EvenOdd,
        GeometryGroup group => group.FillRule == FillRule.Nonzero ? WindingMode.NonZero : WindingMode.EvenOdd,
        _ => WindingMode.EvenOdd
    };

    private protected override Matrix3x2? GeometryTransform => RenderTransform?.Value is { } matrix ? matrix.ToMatrix3X2() : null;

    private protected override PathF CreatePath(Rect area)
    {
        var path = new PathF();
        Data?.AppendPath(path);
        return path;
    }
}
