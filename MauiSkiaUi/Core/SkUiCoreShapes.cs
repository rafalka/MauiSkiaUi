using System.Numerics;

namespace MauiSkiaUi.Core;

/// <summary>Ellipse or circle filling its bounds (Core analogue of <c>SkUiEllipse</c>, MAUI's <c>Ellipse</c>).</summary>
public class SkUiCoreEllipse : SkUiCoreShape
{
    private protected override SkUiCoreStretch DefaultAspect => SkUiCoreStretch.Fill;
    private protected override bool SizesToBounds => true;
    private protected override PathF CreatePath(Rect area) => SkUiShapeGeometry.Ellipse(area);
}

/// <summary>Rectangle filling its bounds, rounded by the larger of <see cref="RadiusX"/> / <see cref="RadiusY"/> (MAUI's <c>Rectangle</c>).</summary>
public class SkUiCoreRectangle : SkUiCoreShape
{
    private double _radiusX;
    private double _radiusY;

    /// <summary>The x-axis corner radius.</summary>
    public double RadiusX { get => _radiusX; set => SetRadiusX(value); }
    /// <summary>The y-axis corner radius.</summary>
    public double RadiusY { get => _radiusY; set => SetRadiusY(value); }

    /// <summary>Sets the x-axis corner radius.</summary>
    public SkUiCoreRectangle SetRadiusX(double value) => SetRadius(ref _radiusX, value, nameof(RadiusX));
    /// <summary>Sets the y-axis corner radius.</summary>
    public SkUiCoreRectangle SetRadiusY(double value) => SetRadius(ref _radiusY, value, nameof(RadiusY));

    private SkUiCoreRectangle SetRadius(ref double field, double value, string name)
    {
        SkUiValidate.ThrowIfNegativeOrNotFinite(value, name);
        if (SetProperty(ref field, value, name))
            InvalidatePaint();
        return this;
    }

    private protected override SkUiCoreStretch DefaultAspect => SkUiCoreStretch.Fill;
    private protected override bool SizesToBounds => true;
    private protected override PathF CreatePath(Rect area) => SkUiShapeGeometry.Rectangle(area, _radiusX, _radiusY);

    private protected override bool TryGetRoundRect(out CornerRadius radii)
    {
        radii = new CornerRadius(Math.Max(_radiusX, _radiusY));
        return true;
    }
}

/// <summary>Rectangle filling its bounds with per-corner radii (Core analogue of <c>SkUiRoundRectangle</c>, MAUI's <c>RoundRectangle</c>).</summary>
public class SkUiCoreRoundRectangle : SkUiCoreShape
{
    private CornerRadius _cornerRadius;

    /// <summary>Per-corner radii in DIPs.</summary>
    public CornerRadius CornerRadius { get => _cornerRadius; set => SetCornerRadius(value); }

    /// <summary>Sets the corner radii (a number converts to the same radius on every corner).</summary>
    public SkUiCoreRoundRectangle SetCornerRadius(CornerRadius value)
    {
        SkUiCornerRadii.Validate(value, nameof(value));
        if (SetProperty(ref _cornerRadius, value, nameof(CornerRadius)))
            InvalidatePaint();
        return this;
    }

    private protected override SkUiCoreStretch DefaultAspect => SkUiCoreStretch.Fill;
    private protected override bool SizesToBounds => true;
    private protected override PathF CreatePath(Rect area) => SkUiShapeGeometry.RoundRectangle(area, _cornerRadius);

    private protected override bool TryGetRoundRect(out CornerRadius radii)
    {
        radii = _cornerRadius;
        return true;
    }
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
        SkUiValidate.ThrowIfNotFinite(value, name);
        if (SetProperty(ref field, value, name))
            InvalidateGeometry();
        return this;
    }

    private protected override PathF CreatePath(Rect area) => SkUiShapeGeometry.Line(_x1, _y1, _x2, _y2);
}

/// <summary>Points and fill rule of <see cref="SkUiCorePolygon"/> and <see cref="SkUiCorePolyline"/>.</summary>
public abstract class SkUiCorePolyShape : SkUiCoreShape
{
    private Point[] _points = [];
    private WindingMode _fillRule = WindingMode.EvenOdd;

    private protected SkUiCorePolyShape() { }

    /// <summary>The vertices in local DIPs.</summary>
    public IReadOnlyList<Point> Points { get => _points; set => SetPoints([.. value]); }
    /// <summary>How the interior is determined where the figure crosses itself (even-odd by default, as MAUI).</summary>
    public WindingMode FillRule { get => _fillRule; set => SetFillRule(value); }

    /// <summary>Sets the vertices.</summary>
    public SkUiCorePolyShape SetPoints(params Point[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (_points.AsSpan().SequenceEqual(value)) return this;
        _points = [.. value];
        OnPropertyChanged(nameof(Points));
        InvalidateGeometry();
        return this;
    }

    /// <summary>Sets the fill rule.</summary>
    public SkUiCorePolyShape SetFillRule(WindingMode value)
    {
        if (SetProperty(ref _fillRule, value, nameof(FillRule)))
            InvalidatePaint();
        return this;
    }

    /// <summary>Whether the figure closes (polygon) or stays open (polyline).</summary>
    private protected abstract bool Closed { get; }

    private protected override WindingMode Winding => _fillRule;
    private protected override PathF CreatePath(Rect area) => SkUiShapeGeometry.Polyline(_points, Closed);
}

/// <summary>Closed figure through <see cref="SkUiCorePolyShape.Points"/> (Core analogue of <c>SkUiPolygon</c>, MAUI's <c>Polygon</c>).</summary>
public class SkUiCorePolygon : SkUiCorePolyShape
{
    /// <summary>Creates a polygon through <paramref name="points"/>.</summary>
    public SkUiCorePolygon(params Point[] points) => SetPoints(points);

    private protected override bool Closed => true;
}

/// <summary>Open line through <see cref="SkUiCorePolyShape.Points"/> (Core analogue of <c>SkUiPolyline</c>, MAUI's <c>Polyline</c>).</summary>
public class SkUiCorePolyline : SkUiCorePolyShape
{
    /// <summary>Creates a polyline through <paramref name="points"/>.</summary>
    public SkUiCorePolyline(params Point[] points) => SetPoints(points);

    private protected override bool Closed => false;
}

/// <summary>
/// Shape drawn from <see cref="Data"/>, a MAUI Graphics <see cref="PathF"/> or path markup (Core analogue of <c>SkUiPath</c>,
/// MAUI's <c>Path</c>), with an optional <see cref="RenderTransform"/>. The path is read when drawn: after editing it in
/// place, call <see cref="SetData(PathF)"/> again.
/// </summary>
public class SkUiCorePath : SkUiCoreShape
{
    private PathF? _data;
    private WindingMode _fillRule = WindingMode.EvenOdd;
    private Matrix3x2? _renderTransform;

    /// <summary>Creates an empty path.</summary>
    public SkUiCorePath() { }

    /// <summary>Creates a path drawing <paramref name="markup"/> (see <see cref="SetData(string)"/>).</summary>
    public SkUiCorePath(string markup) => SetData(markup);

    /// <summary>The geometry in local DIPs.</summary>
    public PathF? Data { get => _data; set => SetData(value); }
    /// <summary>How the interior is determined where figures overlap (even-odd by default, as MAUI's path geometries).</summary>
    public WindingMode FillRule { get => _fillRule; set => SetFillRule(value); }
    /// <summary>A transform applied to the placed geometry when drawing (not to measure).</summary>
    public Matrix3x2? RenderTransform { get => _renderTransform; set => SetRenderTransform(value); }

    /// <summary>Sets the geometry.</summary>
    public SkUiCorePath SetData(PathF? value)
    {
        _data = value;
        OnPropertyChanged(nameof(Data));
        InvalidateGeometry();
        return this;
    }

    /// <summary>
    /// Sets the geometry from SVG-style path markup (<c>"M 0,0 L 20,10 Z"</c>, parsed by MAUI Graphics' <c>PathBuilder</c>).
    /// A leading <c>F0</c> / <c>F1</c> is skipped, as MAUI's own parser does: set <see cref="FillRule"/> instead.
    /// </summary>
    public SkUiCorePath SetData(string markup)
    {
        ArgumentNullException.ThrowIfNull(markup);
        var text = markup.TrimStart();
        if (text.Length >= 2 && text[0] == 'F' && (text[1] == '0' || text[1] == '1'))
            text = text[2..];
        return SetData(PathBuilder.Build(text));
    }

    /// <summary>Sets the fill rule.</summary>
    public SkUiCorePath SetFillRule(WindingMode value)
    {
        if (SetProperty(ref _fillRule, value, nameof(FillRule)))
            InvalidatePaint();
        return this;
    }

    /// <summary>Sets the render transform.</summary>
    public SkUiCorePath SetRenderTransform(Matrix3x2? value)
    {
        if (SetProperty(ref _renderTransform, value, nameof(RenderTransform)))
            InvalidatePaint();
        return this;
    }

    private protected override WindingMode Winding => _fillRule;
    private protected override Matrix3x2? GeometryTransform => _renderTransform;

    private protected override PathF CreatePath(Rect area)
    {
        // A copy: placing the geometry moves its points, and the caller's path stays as set.
        return _data is null ? new PathF() : new PathF(_data);
    }
}
