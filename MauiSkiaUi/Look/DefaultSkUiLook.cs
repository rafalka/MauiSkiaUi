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
    /// <remarks>
    /// Plain circular corners draw with <c>DrawRoundRect</c> (no path); a look with
    /// <see cref="SkUiLook.CreateCustomRoundRectPath"/> geometry draws that path instead, so it shapes buttons, switches
    /// and other rounded chrome too.
    /// </remarks>
    protected override void DrawRoundedBoxCore(SKCanvas canvas, SKRect bounds, float radius, SKColor fill, SKColor border, float width)
    {
        var paint = Paint(fill);
        var strokeRadius = Math.Max(0, radius - width / 2);
        using var path = CreateCustomRoundRectPath(bounds, new CornerRadius(radius));
        if (path is null)
        {
            // Plain corners: no path allocation.
            if (fill.Alpha != 0)
                canvas.DrawRoundRect(bounds, radius, radius, paint);
            if (width <= 0 || border.Alpha == 0) return;
            bounds.Inflate(-width / 2, -width / 2);
            Stroke(paint, border, width);
            canvas.DrawRoundRect(bounds, strokeRadius, strokeRadius, paint);
            return;
        }
        canvas.DrawPath(path, paint);
        if (width <= 0) return;
        bounds.Inflate(-width / 2, -width / 2);
        Stroke(paint, border, width);
        using var strokePath = CreateRoundRectPath(bounds, strokeRadius);
        canvas.DrawPath(strokePath, paint);
    }

    [ThreadStatic] private static SKPaint? t_paint;

    /// <summary>Per-thread reusable fill paint (recording is single-threaded per surface; pictures copy paint state).</summary>
    private static SKPaint Paint(SKColor color)
    {
        var paint = t_paint ??= new SKPaint();
        paint.Reset();
        paint.IsAntialias = true;
        paint.Color = color;
        return paint;
    }

    private static void Stroke(SKPaint paint, SKColor color, float width)
    {
        paint.Color = color;
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = width;
    }

    /// <inheritdoc />
    protected override void DrawRoundedBoxCore(SKCanvas canvas, SKRect bounds, CornerRadius radii, SKColor fill, SKColor border, float width)
    {
        if (IsUniformCornerRadius(radii))
        {
            DrawRoundedBoxCore(canvas, bounds, (float)radii.TopLeft, fill, border, width);
            return;
        }

        var paint = Paint(fill);
        using var path = CreateRoundRectPath(bounds, radii);
        canvas.DrawPath(path, paint);
        if (width <= 0) return;
        bounds.Inflate(-width / 2, -width / 2);
        Stroke(paint, border, width);
        using var strokePath = CreateRoundRectPath(bounds, ShrinkCornerRadius(radii, width / 2));
        canvas.DrawPath(strokePath, paint);
    }

    /// <inheritdoc />
    protected override void DrawSwitchCore(SKCanvas canvas, SKRect bounds, SkUiCheckState state, SKColor track, SKColor thumb)
    {
        var radius = bounds.Height / 2;
        DrawRoundedBox(canvas, bounds, radius, track, SKColors.Transparent, 0);
        var thumbRadius = radius - 2;
        // Indeterminate: the thumb rests in the middle of the track.
        var thumbX = state switch
        {
            SkUiCheckState.Checked => bounds.Right - radius,
            SkUiCheckState.Indeterminate => bounds.MidX,
            _ => bounds.Left + radius
        };
        canvas.DrawCircle(thumbX, bounds.Top + radius, thumbRadius, Paint(thumb));
    }

    /// <inheritdoc />
    protected override void DrawCheckBoxCore(SKCanvas canvas, float size, SkUiCheckState state, SKColor fill, SKColor border)
    {
        var bounds = new SKRect(0, 0, size, size);
        DrawRoundedBox(canvas, bounds, size * 0.2f, fill, border, 1.5f);
        if (state == SkUiCheckState.Unchecked) return;
        using var check = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = size * 0.12f,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            IsAntialias = true
        };
        if (state == SkUiCheckState.Indeterminate)
        {
            canvas.DrawLine(size * 0.26f, size * 0.5f, size * 0.74f, size * 0.5f, check);
            return;
        }
        using var builder = new SKPathBuilder();
        builder.MoveTo(size * 0.22f, size * 0.55f);
        builder.LineTo(size * 0.42f, size * 0.75f);
        builder.LineTo(size * 0.8f, size * 0.28f);
        using var path = builder.Detach();
        canvas.DrawPath(path, check);
    }

    /// <inheritdoc />
    protected override void DrawRadioButtonCore(SKCanvas canvas, float size, SkUiCheckState state, SKColor ring, SKColor dot)
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
        if (state == SkUiCheckState.Checked)
        {
            canvas.DrawCircle(center, center, size * 0.28f, Paint(dot));
        }
        else if (state == SkUiCheckState.Indeterminate)
        {
            var half = size * 0.14f;
            canvas.DrawRoundRect(new SKRect(center - size * 0.26f, center - half / 2, center + size * 0.26f, center + half / 2), half / 2, half / 2, Paint(dot));
        }
    }

    /// <inheritdoc />
    protected override void DrawSliderCore(SKCanvas canvas, SkUiSliderPaint slider)
    {
        var bounds = slider.Bounds;
        var radius = SliderThumbRadius;
        var start = bounds.Left + radius;
        var end = Math.Max(start, bounds.Right - radius);
        var thumbX = start + (end - start) * Math.Clamp(slider.Fraction, 0, 1);
        const float track = 4;
        var top = bounds.MidY - track / 2;
        DrawRoundedBox(canvas, new SKRect(start, top, end, top + track), track / 2, slider.MaximumTrack, SKColors.Transparent, 0);
        if (thumbX > start)
            DrawRoundedBox(canvas, new SKRect(start, top, thumbX, top + track), track / 2, slider.MinimumTrack, SKColors.Transparent, 0);
        if (slider.IsPressed)
            canvas.DrawCircle(thumbX, bounds.MidY, radius * 1.8f, Paint(slider.Thumb.WithAlpha((byte)(slider.Thumb.Alpha / 5))));
        canvas.DrawCircle(thumbX, bounds.MidY, radius, Paint(slider.Thumb));
    }

    /// <inheritdoc />
    protected override void DrawProgressBarCore(SKCanvas canvas, SkUiProgressBarPaint bar)
    {
        var bounds = bar.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        var radius = GetProgressBarCornerRadius(bounds.Height);
        if (bar.IsIndeterminate)
        {
            // Square, unantialiased track: copies tiled one bar width apart meet without a seam (the compositor
            // clips the rounded ends).
            using var track = new SKPaint { Color = bar.Track };
            canvas.DrawRect(bounds, track);
        }
        else
        {
            DrawRoundedBox(canvas, bounds, radius, bar.Track, SKColors.Transparent, 0);
        }
        var fraction = bar.IsIndeterminate ? bar.SegmentFraction : Math.Clamp(bar.Progress, 0, 1);
        if (fraction <= 0) return;
        DrawRoundedBox(canvas, new SKRect(bounds.Left, bounds.Top, bounds.Left + bounds.Width * fraction, bounds.Bottom), radius, bar.Fill, SKColors.Transparent, 0);
    }

    /// <inheritdoc />
    protected override void DrawActivityIndicatorCore(SKCanvas canvas, float width, float height, float sweepStart, SKPaint paint)
    {
        if (width <= 0 || height <= 0) return;
        // A circle centered in the slot (MAUI-like), so rotating the recorded arc about the center on the
        // render thread is equivalent to advancing sweepStart.
        var diameter = Math.Min(width, height);
        var strokeWidth = (float)Math.Max(2, diameter * 0.1);
        paint.StrokeWidth = strokeWidth;
        var left = (width - diameter) / 2;
        var top = (height - diameter) / 2;
        var bounds = new SKRect(
            left + strokeWidth / 2,
            top + strokeWidth / 2,
            left + diameter - strokeWidth / 2,
            top + diameter - strokeWidth / 2);
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
