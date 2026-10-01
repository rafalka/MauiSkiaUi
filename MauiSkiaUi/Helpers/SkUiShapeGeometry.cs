using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>Shape measure and drawing shared by the shapes of both layers, following MAUI's <c>Shape</c> (no stretch).</summary>
internal static class SkUiShapeGeometry
{
    /// <summary>
    /// A line's intrinsic size as MAUI measures a <c>Line</c>: from the origin to its far end points, plus the stroke.
    /// </summary>
    public static Size MeasureLine(double x1, double y1, double x2, double y2, double strokeWidth) =>
        new(Math.Max(0, Math.Max(x1, x2) + strokeWidth), Math.Max(0, Math.Max(y1, y2) + strokeWidth));

    /// <summary>
    /// Strokes the line from (<paramref name="x1"/>, <paramref name="y1"/>) to (<paramref name="x2"/>, <paramref name="y2"/>)
    /// in local coordinates. As MAUI places a shape without stretch: the drawing area is the bounds inset by half the
    /// stroke, and the line moves only to bring an end that sticks out over its left / top (or else right / bottom) edge back in.
    /// </summary>
    public static void DrawLine(SKCanvas canvas, SKRect bounds, float x1, float y1, float x2, float y2, SKPaint paint)
    {
        var area = bounds;
        area.Inflate(-paint.StrokeWidth / 2, -paint.StrokeWidth / 2);
        var dx = Shift(area.Left, area.Right, Math.Min(x1, x2), Math.Max(x1, x2));
        var dy = Shift(area.Top, area.Bottom, Math.Min(y1, y2), Math.Max(y1, y2));
        paint.Style = SKPaintStyle.Stroke;
        canvas.DrawLine(x1 + dx, y1 + dy, x2 + dx, y2 + dy, paint);

        static float Shift(float start, float end, float min, float max) =>
            start > min ? start - min : max > end ? end - max : 0;
    }
}
