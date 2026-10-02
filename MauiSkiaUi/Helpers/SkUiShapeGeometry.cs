using System.Numerics;
using MauiSkiaUi.Core;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// Shape geometry shared by the shapes and borders of both layers, following MAUI's <c>Shape</c>: the paths of the shape
/// types (MAUI's <c>GetPath</c>), measure (<c>MeasureOverride</c> once the platform view reports no size), placement in
/// the bounds by <c>Aspect</c> (<c>TransformPathForBounds</c>) and the conversion to a Skia path.
/// </summary>
internal static class SkUiShapeGeometry
{
    /// <summary>A rectangle in <paramref name="bounds"/>, rounded by the larger of the radii as MAUI's <c>Rectangle</c>.</summary>
    public static PathF Rectangle(Rect bounds, double radiusX, double radiusY)
    {
        var path = new PathF();
        var radius = (float)Math.Max(radiusX, radiusY);
        if (radius > 0)
            path.AppendRoundedRectangle((float)bounds.X, (float)bounds.Y, (float)bounds.Width, (float)bounds.Height, radius);
        else
            path.AppendRectangle((float)bounds.X, (float)bounds.Y, (float)bounds.Width, (float)bounds.Height);
        return path;
    }

    /// <summary>A rectangle in <paramref name="bounds"/> with per-corner radii, as MAUI's <c>RoundRectangle</c>.</summary>
    public static PathF RoundRectangle(Rect bounds, CornerRadius radii)
    {
        var path = new PathF();
        path.AppendRoundedRectangle((float)bounds.X, (float)bounds.Y, (float)bounds.Width, (float)bounds.Height,
            (float)radii.TopLeft, (float)radii.TopRight, (float)radii.BottomLeft, (float)radii.BottomRight);
        return path;
    }

    /// <summary>An ellipse in <paramref name="bounds"/>.</summary>
    public static PathF Ellipse(Rect bounds)
    {
        var path = new PathF();
        path.AppendEllipse((float)bounds.X, (float)bounds.Y, (float)bounds.Width, (float)bounds.Height);
        return path;
    }

    /// <summary>A straight line between two points.</summary>
    public static PathF Line(double x1, double y1, double x2, double y2)
    {
        var path = new PathF();
        path.MoveTo((float)x1, (float)y1);
        path.LineTo((float)x2, (float)y2);
        return path;
    }

    /// <summary>Lines through <paramref name="points"/>; a polygon closes the figure, a polyline does not.</summary>
    public static PathF Polyline(IReadOnlyList<Point>? points, bool close)
    {
        var path = new PathF();
        if (points is not { Count: > 0 })
            return path;
        path.MoveTo((float)points[0].X, (float)points[0].Y);
        for (var index = 1; index < points.Count; index++)
            path.LineTo((float)points[index].X, (float)points[index].Y);
        if (close)
            path.Close();
        return path;
    }

    /// <summary>The bounds MAUI measures and places a path by (flattened, since the exact bounds of curves are off).</summary>
    public static RectF Bounds(PathF path) => path.OperationCount == 0 ? default : path.GetBoundsByFlattening(1);

    /// <summary>
    /// A shape's intrinsic size as MAUI's <c>Shape</c> measures it, from its geometry's <paramref name="pathBounds"/> (empty
    /// for shapes drawn at the size they are given: rectangles and ellipses). One difference: a uniform aspect under one
    /// unbounded constraint scales by the bounded one (MAUI scales by 0 and collapses to the stroke).
    /// </summary>
    public static Size Measure(RectF pathBounds, SkUiCoreStretch aspect, double strokeThickness, double widthConstraint, double heightConstraint)
    {
        double width = pathBounds.Width;
        double height = pathBounds.Height;
        var innerWidth = widthConstraint - strokeThickness;
        var innerHeight = heightConstraint - strokeThickness;
        var scaleX = Scale(innerWidth, width);
        var scaleY = Scale(innerHeight, height);
        switch (aspect)
        {
            case SkUiCoreStretch.None:
                width += pathBounds.X;
                height += pathBounds.Y;
                break;
            case SkUiCoreStretch.Fill:
                if (double.IsFinite(innerWidth)) width = innerWidth;
                if (double.IsFinite(innerHeight)) height = innerHeight;
                break;
            case SkUiCoreStretch.Uniform:
                var minScale = scaleX is null ? scaleY ?? 0 : scaleY is null ? scaleX.Value : Math.Min(scaleX.Value, scaleY.Value);
                width *= minScale;
                height *= minScale;
                break;
            case SkUiCoreStretch.UniformToFill:
                var maxScale = Math.Max(scaleX ?? 0, scaleY ?? 0);
                if (maxScale != 0)
                {
                    width *= maxScale;
                    height *= maxScale;
                }
                break;
        }
        return new Size(Math.Max(0, width + strokeThickness), Math.Max(0, height + strokeThickness));

        // null: the constraint is unbounded or the geometry has no extent on this axis.
        static double? Scale(double constraint, double extent) => constraint / extent is var scale && double.IsFinite(scale) ? Math.Max(0, scale) : null;
    }

    /// <summary>
    /// Places <paramref name="path"/> in <paramref name="viewBounds"/> as MAUI's <c>TransformPathForBounds</c>: the area is
    /// the bounds inset by half the stroke; without stretch the path only moves to bring an edge that sticks out over the
    /// left / top (or else right / bottom) back in, otherwise it is scaled by <paramref name="aspect"/>.
    /// </summary>
    public static void PlaceInBounds(PathF path, Rect viewBounds, double strokeThickness, SkUiCoreStretch aspect)
    {
        if (path.OperationCount == 0)
            return;
        var pathBounds = Bounds(path);
        var area = new Rect(viewBounds.X + strokeThickness / 2, viewBounds.Y + strokeThickness / 2,
            viewBounds.Width - strokeThickness, viewBounds.Height - strokeThickness);
        Matrix3x2 transform;
        if (aspect == SkUiCoreStretch.None)
        {
            var dx = area.Left > pathBounds.Left ? area.Left - pathBounds.Left : pathBounds.Right > area.Right ? area.Right - pathBounds.Right : 0;
            var dy = area.Top > pathBounds.Top ? area.Top - pathBounds.Top : pathBounds.Bottom > area.Bottom ? area.Bottom - pathBounds.Bottom : 0;
            if (dx == 0 && dy == 0)
                return;
            transform = Matrix3x2.CreateTranslation((float)dx, (float)dy);
        }
        else
        {
            var scaleX = Finite(area.Width / pathBounds.Width);
            var scaleY = Finite(area.Height / pathBounds.Height);
            switch (aspect)
            {
                case SkUiCoreStretch.Fill:
                    transform = Matrix3x2.CreateScale(scaleX, scaleY)
                        * Matrix3x2.CreateTranslation((float)(area.Left - scaleX * pathBounds.Left), (float)(area.Top - scaleY * pathBounds.Top));
                    break;
                case SkUiCoreStretch.Uniform:
                    var min = Math.Min(scaleX, scaleY);
                    transform = Matrix3x2.CreateScale(min, min) * Matrix3x2.CreateTranslation(
                        (float)(area.Left - min * pathBounds.Left + (area.Width - min * pathBounds.Width) / 2),
                        (float)(area.Top - min * pathBounds.Top + (area.Height - min * pathBounds.Height) / 2));
                    break;
                default:
                    var max = Math.Max(scaleX, scaleY);
                    transform = Matrix3x2.CreateScale(max, max)
                        * Matrix3x2.CreateTranslation((float)(area.Left - max * pathBounds.Left), (float)(area.Top - max * pathBounds.Top));
                    break;
            }
        }
        if (!transform.IsIdentity)
            path.Transform(transform);

        static float Finite(double scale) => double.IsFinite(scale) ? (float)scale : 0;
    }

    /// <summary>Converts a MAUI Graphics path to a Skia path with the given fill rule (as MAUI Graphics' <c>AsSkiaPath</c>).</summary>
    public static SKPath ToSkia(PathF path, WindingMode winding = WindingMode.NonZero, Matrix3x2? transform = null)
    {
        var fillType = winding == WindingMode.EvenOdd ? SKPathFillType.EvenOdd : SKPathFillType.Winding;
        using var builder = new SKPathBuilder();
        builder.FillType = fillType;
        var pointIndex = 0;
        var arcAngleIndex = 0;
        var arcClockwiseIndex = 0;
        for (var index = 0; index < path.OperationCount; index++)
        {
            switch (path.GetSegmentType(index))
            {
                case PathOperation.Move:
                    builder.MoveTo(Point(path[pointIndex++]));
                    break;
                case PathOperation.Line:
                    builder.LineTo(Point(path[pointIndex++]));
                    break;
                case PathOperation.Quad:
                    builder.QuadTo(Point(path[pointIndex++]), Point(path[pointIndex++]));
                    break;
                case PathOperation.Cubic:
                    builder.CubicTo(Point(path[pointIndex++]), Point(path[pointIndex++]), Point(path[pointIndex++]));
                    break;
                case PathOperation.Arc:
                    var topLeft = path[pointIndex++];
                    var bottomRight = path[pointIndex++];
                    var startAngle = path.GetArcAngle(arcAngleIndex++);
                    var endAngle = path.GetArcAngle(arcAngleIndex++);
                    var clockwise = path.GetArcClockwise(arcClockwiseIndex++);
                    while (startAngle < 0) startAngle += 360;
                    while (endAngle < 0) endAngle += 360;
                    var sweep = GeometryUtil.GetSweep(startAngle, endAngle, clockwise);
                    builder.AddArc(new SKRect(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y), -startAngle, clockwise ? sweep : -sweep);
                    break;
                case PathOperation.Close:
                    builder.Close();
                    break;
            }
        }
        var result = builder.Detach();
        if (transform is not { IsIdentity: false } matrix)
            return result;
        using (result)
        {
            using var transformed = new SKPathBuilder();
            transformed.FillType = fillType;
            transformed.AddPath(result, new SKMatrix(matrix.M11, matrix.M21, matrix.M31, matrix.M12, matrix.M22, matrix.M32, 0, 0, 1));
            return transformed.Detach();
        }

        static SKPoint Point(PointF point) => new(point.X, point.Y);
    }
}
