using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>Shared rounded-rectangle fill/border/clip geometry used by Button, Border, ImageButton, and similar chrome.</summary>
internal static class SkUiChrome
{
    /// <summary>Builds the rounded-rect path shared by a background fill/border and a matching content clip.</summary>
    internal static SKPath CreateRoundRectPath(SKRect bounds, float radius)
    {
        using var roundRect = new SKRoundRect(bounds, radius, radius);
        using var builder = new SKPathBuilder();
        builder.AddRoundRect(roundRect);
        return builder.Detach();
    }

    internal static void DrawRoundedBox(SKCanvas canvas, SKRect bounds, float radius, SKColor fill, SKColor border, float width)
    {
        using var paint = new SKPaint { Color = fill, IsAntialias = true };
        using var path = CreateRoundRectPath(bounds, radius);
        canvas.DrawPath(path, paint);
        if (width <= 0) return;
        bounds.Inflate(-width / 2, -width / 2);
        paint.Color = border;
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = width;
        using var strokePath = CreateRoundRectPath(bounds, Math.Max(0, radius - width / 2));
        canvas.DrawPath(strokePath, paint);
    }
}
