using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// The stroke of a shape or border, as MAUI's <c>IStroke</c>: thickness in DIPs, the dash pattern and offset in multiples
/// of the thickness (as MAUI and WPF), caps, joins and the miter limit.
/// </summary>
internal readonly record struct SkUiStrokeStyle(double Thickness, float[]? Dashes, double DashOffset, LineCap Cap, LineJoin Join, double MiterLimit)
{
    /// <summary>A solid stroke with MAUI's default caps, joins and miter limit.</summary>
    public static SkUiStrokeStyle Solid(double thickness) => new(thickness, null, 0, LineCap.Butt, LineJoin.Miter, 10);
}

/// <summary>Fills and strokes shape paths with MAUI Graphics paints (solid colors and gradients); shared by both layers.</summary>
internal static class SkUiShapePainter
{
    [ThreadStatic] private static SKPaint? _paint;

    /// <summary>
    /// Fills, then strokes <paramref name="path"/> as MAUI's <c>ShapeDrawable</c>: a fill gradient spans
    /// <paramref name="fillBounds"/> (the view), a stroke gradient the path's bounds.
    /// </summary>
    public static void Draw(SKCanvas canvas, SKPath path, Paint? fill, SKRect fillBounds, Paint? stroke, in SkUiStrokeStyle style)
    {
        if (fill is not null)
            Fill(canvas, path, fill, fillBounds);
        if (stroke is not null)
            Stroke(canvas, path, stroke, path.Bounds, style);
    }

    /// <summary>Fills <paramref name="path"/> with <paramref name="fill"/> mapped onto <paramref name="bounds"/>.</summary>
    public static void Fill(SKCanvas canvas, SKPath path, Paint? fill, SKRect bounds)
    {
        var paint = Reset();
        if (Apply(paint, fill, bounds))
            canvas.DrawPath(path, paint);
        Release(paint);
    }

    /// <summary>Fills <paramref name="rect"/> (by default not antialiased, as a plain background) with <paramref name="fill"/> mapped onto it.</summary>
    public static void FillRect(SKCanvas canvas, SKRect rect, Paint? fill, bool antialias = false)
    {
        if (fill is null)
            return;
        var paint = Reset();
        paint.IsAntialias = antialias;
        if (Apply(paint, fill, rect))
            canvas.DrawRect(rect, paint);
        Release(paint);
    }

    /// <summary>Fills <paramref name="rect"/> with a resolved fill (a solid color allocates nothing).</summary>
    public static void FillRect(SKCanvas canvas, SKRect rect, in SkUiFill fill, bool antialias = false)
    {
        if (fill.Gradient is { } gradient)
        {
            FillRect(canvas, rect, gradient, antialias);
            return;
        }
        if (fill.Color.Alpha == 0)
            return;
        var paint = Reset();
        paint.IsAntialias = antialias;
        paint.Color = fill.Color;
        canvas.DrawRect(rect, paint);
    }

    /// <summary>Fills <paramref name="path"/> with a resolved fill mapped onto <paramref name="bounds"/> (a solid color allocates nothing).</summary>
    public static void Fill(SKCanvas canvas, SKPath path, in SkUiFill fill, SKRect bounds)
    {
        if (fill.Gradient is { } gradient)
        {
            Fill(canvas, path, gradient, bounds);
            return;
        }
        if (fill.Color.Alpha == 0)
            return;
        var paint = Reset();
        paint.Color = fill.Color;
        canvas.DrawPath(path, paint);
    }

    /// <summary>Strokes <paramref name="path"/> with <paramref name="stroke"/> mapped onto <paramref name="bounds"/>.</summary>
    public static void Stroke(SKCanvas canvas, SKPath path, Paint? stroke, SKRect bounds, in SkUiStrokeStyle style)
    {
        if (style.Thickness <= 0)
            return;
        var paint = Reset();
        if (Apply(paint, stroke, bounds))
        {
            ApplyStroke(paint, style);
            canvas.DrawPath(path, paint);
        }
        Release(paint);
    }

    /// <summary>
    /// The shadow silhouette of a shape drawn along <paramref name="path"/> (which it takes over): the fill area when the
    /// fill is opaque, grown by the stroke when an opaque, undashed stroke is drawn (or the stroke area alone without a
    /// fill); <c>null</c> when anything visible is translucent or dashed, so the shadow is cast from what is drawn instead.
    /// </summary>
    public static SKPath? ShapeShadowOutline(SKPath path, Paint? fill, Paint? stroke, in SkUiStrokeStyle style)
    {
        var filled = IsVisible(fill);
        var stroked = style.Thickness > 0 && IsVisible(stroke);
        if ((!filled && !stroked) || (filled && !IsOpaque(fill)) || (stroked && (!IsOpaque(stroke) || style.Dashes is { Length: > 0 })))
        {
            path.Dispose();
            return null;
        }
        if (!stroked)
            return path;
        using var paint = new SKPaint();
        ApplyStroke(paint, style);
        var area = paint.GetFillPath(path);
        if (!filled)
        {
            path.Dispose();
            return area;
        }
        using (area)
        {
            var union = path.Op(area, SKPathOp.Union) ?? new SKPath(path);
            path.Dispose();
            return union;
        }
    }

    /// <summary>Configures <paramref name="paint"/> as a stroke of <paramref name="style"/> (width, caps, joins, miter, dashes).</summary>
    public static void ApplyStroke(SKPaint paint, in SkUiStrokeStyle style)
    {
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = (float)style.Thickness;
        paint.StrokeCap = style.Cap switch { LineCap.Round => SKStrokeCap.Round, LineCap.Square => SKStrokeCap.Square, _ => SKStrokeCap.Butt };
        paint.StrokeJoin = style.Join switch { LineJoin.Round => SKStrokeJoin.Round, LineJoin.Bevel => SKStrokeJoin.Bevel, _ => SKStrokeJoin.Miter };
        paint.StrokeMiter = (float)Math.Max(1, style.MiterLimit);
        paint.PathEffect = CreateDash(style);
    }

    /// <summary>
    /// Sets the color or gradient shader of <paramref name="paint"/> from a MAUI Graphics paint mapped onto
    /// <paramref name="bounds"/> (as MAUI Graphics' Skia canvas); <c>false</c> when it draws nothing (no paint, no color,
    /// no gradient stops, or an image paint, which is not supported).
    /// </summary>
    public static bool Apply(SKPaint paint, Paint? brush, SKRect bounds)
    {
        if (!TryCreate(brush, bounds, out var color, out var shader))
            return false;
        paint.Color = color;
        paint.Shader = shader;
        return true;
    }

    /// <summary>
    /// The color or gradient shader of a MAUI Graphics paint mapped onto <paramref name="bounds"/> (see <see cref="Apply"/>):
    /// a solid paint gives its color and no shader, a gradient white and a shader the caller owns.
    /// </summary>
    public static bool TryCreate(Paint? brush, SKRect bounds, out SKColor color, out SKShader? shader)
    {
        shader = null;
        switch (brush)
        {
            case SolidPaint { Color: { } solid }:
                color = ToSkColor(solid);
                return solid.Alpha > 0;
            case LinearGradientPaint { GradientStops.Length: > 0 } linear:
            {
                var (colors, offsets) = Stops(linear);
                color = SKColors.White;
                shader = SKShader.CreateLinearGradient(
                    new SKPoint(bounds.Left + (float)linear.StartPoint.X * bounds.Width, bounds.Top + (float)linear.StartPoint.Y * bounds.Height),
                    new SKPoint(bounds.Left + (float)linear.EndPoint.X * bounds.Width, bounds.Top + (float)linear.EndPoint.Y * bounds.Height),
                    colors, offsets, SKShaderTileMode.Clamp);
                return true;
            }
            case RadialGradientPaint { GradientStops.Length: > 0 } radial:
            {
                var (colors, offsets) = Stops(radial);
                var radius = (float)radial.Radius * Math.Max(bounds.Width, bounds.Height);
                if (radius <= 0)
                    radius = SKPoint.Distance(new SKPoint(bounds.Left, bounds.Top), new SKPoint(bounds.Right, bounds.Bottom));
                color = SKColors.White;
                shader = SKShader.CreateRadialGradient(
                    new SKPoint(bounds.Left + (float)radial.Center.X * bounds.Width, bounds.Top + (float)radial.Center.Y * bounds.Height),
                    Math.Max(radius, 0.001f), colors, offsets, SKShaderTileMode.Clamp);
                return true;
            }
            default:
                color = default;
                if (brush is not null && brush is not SolidPaint and not GradientPaint)
                    WarnUnsupported(brush);
                return false;
        }
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, bool> _warned = new();

    /// <summary>
    /// Says once per paint type that it is not drawn (an <c>ImageBrush</c> arrives as an image paint), so a ported page with
    /// a missing image fill is easy to diagnose instead of silently empty.
    /// </summary>
    internal static void WarnUnsupported(Paint brush)
    {
        if (_warned.TryAdd(brush.GetType(), true))
            System.Diagnostics.Trace.TraceWarning(
                $"SkiaUi: {brush.GetType().Name} is not drawn (ImageBrush is not supported yet); the fill or stroke stays empty.");
    }

    /// <summary>Whether <paramref name="brush"/> draws anything (see <see cref="Apply"/>).</summary>
    public static bool IsVisible(Paint? brush) => brush switch
    {
        SolidPaint { Color: { } color } => color.Alpha > 0,
        GradientPaint { GradientStops.Length: > 0 } => true,
        _ => false
    };

    /// <summary>Whether <paramref name="brush"/> covers what it fills: an opaque color, or a gradient whose stops are all opaque.</summary>
    public static bool IsOpaque(Paint? brush)
    {
        switch (brush)
        {
            case SolidPaint { Color: { } color }:
                return color.Alpha >= 1;
            case GradientPaint { GradientStops.Length: > 0 } gradient:
                foreach (var stop in gradient.GradientStops)
                    if (stop is not null && (stop.Color?.Alpha ?? 0) < 1)
                        return false;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// One color for a paint, for looks that draw colors only and for contrast decisions (a ripple over a gradient): a
    /// solid paint's color, a gradient's stops averaged; transparent for none.
    /// </summary>
    public static SKColor RepresentativeColor(Paint? brush)
    {
        switch (brush)
        {
            case SolidPaint { Color: { } color }:
                return ToSkColor(color);
            case GradientPaint { GradientStops.Length: > 0 } gradient:
            {
                float red = 0, green = 0, blue = 0, alpha = 0;
                var count = 0;
                foreach (var stop in gradient.GradientStops)
                {
                    if (stop?.Color is not { } color) continue;
                    red += color.Red; green += color.Green; blue += color.Blue; alpha += color.Alpha;
                    count++;
                }
                return count == 0 ? SKColors.Transparent : ToSkColor(new Color(red / count, green / count, blue / count, alpha / count));
            }
            default:
                return SKColors.Transparent;
        }
    }

    /// <summary>A copy of <paramref name="brush"/> with every color's alpha multiplied by <paramref name="opacity"/> (0–1).</summary>
    public static Paint? WithOpacity(Paint? brush, float opacity)
    {
        if (opacity >= 1 || brush is null)
            return brush;
        opacity = Math.Max(0, opacity);
        switch (brush)
        {
            case SolidPaint { Color: { } color }:
                return new SolidPaint(color.WithAlpha(color.Alpha * opacity));
            case LinearGradientPaint linear:
                return new LinearGradientPaint(FadedStops(linear, opacity), linear.StartPoint, linear.EndPoint);
            case RadialGradientPaint radial:
                return new RadialGradientPaint(FadedStops(radial, opacity), radial.Center, radial.Radius);
            default:
                return brush;
        }

        static PaintGradientStop[] FadedStops(GradientPaint gradient, float opacity) =>
            [.. gradient.GradientStops.Where(stop => stop is not null)
                .Select(stop => new PaintGradientStop(stop.Offset, (stop.Color ?? Colors.Transparent).WithAlpha((stop.Color?.Alpha ?? 0) * opacity)))];
    }

    /// <summary>
    /// The dash effect of <paramref name="style"/>: the pattern scaled by the thickness (an odd-length pattern repeats once
    /// more, as in SVG), or <c>null</c> for a solid stroke.
    /// </summary>
    private static SKPathEffect? CreateDash(in SkUiStrokeStyle style)
    {
        if (style.Dashes is not { Length: > 0 } dashes)
            return null;
        var intervals = new float[dashes.Length % 2 == 0 ? dashes.Length : dashes.Length * 2];
        var thickness = (float)style.Thickness;
        var total = 0f;
        for (var index = 0; index < intervals.Length; index++)
        {
            var interval = dashes[index % dashes.Length];
            intervals[index] = float.IsFinite(interval) ? Math.Max(0, interval) * thickness : 0;
            total += intervals[index];
        }
        return total > 0 ? SKPathEffect.CreateDash(intervals, (float)style.DashOffset * thickness) : null;
    }

    /// <summary>The gradient's stops in offset order (stable; missing stops skipped).</summary>
    private static (SKColor[] Colors, float[] Offsets) Stops(GradientPaint gradient)
    {
        var stops = gradient.GradientStops.Where(stop => stop is not null).OrderBy(stop => stop.Offset).ToArray();
        if (stops.Length == 0)
            return ([SKColors.Transparent], [0]);
        var colors = new SKColor[stops.Length];
        var offsets = new float[stops.Length];
        for (var index = 0; index < stops.Length; index++)
        {
            colors[index] = ToSkColor(stops[index].Color ?? Colors.Transparent);
            offsets[index] = Math.Clamp(stops[index].Offset, 0, 1);
        }
        return (colors, offsets);
    }

    private static SKPaint Reset()
    {
        var paint = _paint ??= new SKPaint();
        paint.Reset();
        paint.IsAntialias = true;
        return paint;
    }

    /// <summary>Releases the shader and dash effect (recorded pictures keep their own references).</summary>
    private static void Release(SKPaint paint)
    {
        var shader = paint.Shader;
        var effect = paint.PathEffect;
        paint.Shader = null;
        paint.PathEffect = null;
        shader?.Dispose();
        effect?.Dispose();
    }

    internal static SKColor ToSkColor(Color color) => new(
        (byte)(color.Red * 255), (byte)(color.Green * 255), (byte)(color.Blue * 255), (byte)(color.Alpha * 255));
}
