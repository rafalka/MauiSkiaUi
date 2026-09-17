using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// Built-in control look: current cross-platform geometry and intrinsic sizes (former <c>SkUiChrome</c>).
/// Subclass or set painter/measure delegates to customize without replacing every control type.
/// </summary>
public class DefaultSkUiLook : SkUiLook
{
    /// <summary>Shared default look used as the process <see cref="SkUiLook.Current"/>.</summary>
    public static DefaultSkUiLook Instance { get; } = new();

    /// <inheritdoc />
    protected override void DrawRoundedBoxCore(SKCanvas canvas, SKRect bounds, float radius, SKColor fill, SKColor border, float width)
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

    /// <inheritdoc />
    protected override void DrawSwitchCore(SKCanvas canvas, SKRect bounds, bool isChecked, SKColor track, SKColor thumb)
    {
        var radius = bounds.Height / 2;
        DrawRoundedBox(canvas, bounds, radius, track, SKColors.Transparent, 0);
        var thumbRadius = radius - 2;
        var thumbX = isChecked ? bounds.Right - radius : bounds.Left + radius;
        using var paint = new SKPaint { Color = thumb, IsAntialias = true };
        canvas.DrawCircle(thumbX, bounds.Top + radius, thumbRadius, paint);
    }

    /// <inheritdoc />
    protected override void DrawCheckBoxCore(SKCanvas canvas, float size, bool isChecked, SKColor fill, SKColor border)
    {
        var bounds = new SKRect(0, 0, size, size);
        DrawRoundedBox(canvas, bounds, size * 0.2f, fill, border, 1.5f);
        if (!isChecked) return;
        using var check = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = size * 0.12f,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            IsAntialias = true
        };
        using var builder = new SKPathBuilder();
        builder.MoveTo(size * 0.22f, size * 0.55f);
        builder.LineTo(size * 0.42f, size * 0.75f);
        builder.LineTo(size * 0.8f, size * 0.28f);
        using var path = builder.Detach();
        canvas.DrawPath(path, check);
    }

    /// <inheritdoc />
    protected override void DrawRadioButtonCore(SKCanvas canvas, float size, bool isChecked, SKColor ring, SKColor dot)
    {
        var center = size / 2;
        using var ringPaint = new SKPaint
        {
            Color = ring,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = size * 0.08f,
            IsAntialias = true
        };
        canvas.DrawCircle(center, center, center - ringPaint.StrokeWidth / 2, ringPaint);
        if (!isChecked) return;
        using var dotPaint = new SKPaint { Color = dot, IsAntialias = true };
        canvas.DrawCircle(center, center, size * 0.28f, dotPaint);
    }

    /// <inheritdoc />
    protected override void DrawActivityIndicatorCore(SKCanvas canvas, float width, float height, float sweepStart, SKPaint paint)
    {
        if (width <= 0 || height <= 0) return;
        var strokeWidth = (float)Math.Max(2, Math.Min(width, height) * 0.1);
        paint.StrokeWidth = strokeWidth;
        var bounds = new SKRect(
            strokeWidth / 2,
            strokeWidth / 2,
            width - strokeWidth / 2,
            height - strokeWidth / 2);
        canvas.DrawArc(bounds, sweepStart, 270, false, paint);
    }

    /// <inheritdoc />
    protected override void DrawImageCore(SKCanvas canvas, SKImage image, float viewWidth, float viewHeight, Aspect aspect)
    {
        var destination = ComputeImageDestination(viewWidth, viewHeight, image.Width, image.Height, aspect);
        canvas.DrawImage(image, destination, new SKSamplingOptions(SKFilterMode.Linear));
    }

    /// <inheritdoc />
    protected override void DrawPressTintCore(SKCanvas canvas, SKRect bounds, float cornerRadius, bool disabled, bool pressed)
    {
        if (!disabled && !pressed) return;
        var tint = disabled ? new SKColor(0, 0, 0, 96) : new SKColor(0, 0, 0, 48);
        using var paint = new SKPaint { Color = tint };
        if (cornerRadius > 0)
        {
            using var path = CreateRoundRectPath(bounds, cornerRadius);
            canvas.DrawPath(path, paint);
        }
        else
        {
            canvas.DrawRect(bounds, paint);
        }
    }
}
