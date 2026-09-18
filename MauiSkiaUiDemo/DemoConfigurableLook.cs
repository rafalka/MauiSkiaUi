using SkiaSharp;
using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Demo look packs used by <see cref="LookAndColorSchemePage"/> (not shipped library packs).</summary>
internal enum DemoLookStyle
{
    /// <summary>Stock <see cref="DefaultSkUiLook"/> geometry.</summary>
    Default,
    /// <summary>Larger controls, pill switch with inset track, square radio when checked.</summary>
    Chunky,
    /// <summary>Compact sizes, flat switch, thin radio ring.</summary>
    Minimal
}

/// <summary>
/// Configurable look for the demo: style pack plus optional per-control size overrides.
/// Drawing and measure are owned here so the page can swap <see cref="SkUiLook.Current"/> in one step.
/// </summary>
internal sealed class DemoConfigurableLook : DefaultSkUiLook
{
    /// <summary>Which alternate geometry pack to use.</summary>
    public DemoLookStyle Style { get; set; } = DemoLookStyle.Default;

    /// <summary>When set, replaces the pack's <see cref="DefaultButtonMinimumHeight"/>.</summary>
    public double? OverrideButtonMinimumHeight { get; set; }

    /// <summary>When set, replaces the pack's <see cref="DefaultSwitchSize"/>.</summary>
    public Size? OverrideSwitchSize { get; set; }

    /// <summary>When set, replaces the pack's <see cref="DefaultCheckBoxSize"/>.</summary>
    public Size? OverrideCheckBoxSize { get; set; }

    /// <summary>When set, replaces the pack's <see cref="DefaultRadioButtonSize"/>.</summary>
    public Size? OverrideRadioButtonSize { get; set; }

    /// <summary>When set, replaces the pack's <see cref="DefaultActivityIndicatorSize"/>.</summary>
    public Size? OverrideActivityIndicatorSize { get; set; }

    /// <summary>Clears all size overrides so pack defaults apply again.</summary>
    public void ClearSizeOverrides()
    {
        OverrideButtonMinimumHeight = null;
        OverrideSwitchSize = null;
        OverrideCheckBoxSize = null;
        OverrideRadioButtonSize = null;
        OverrideActivityIndicatorSize = null;
    }

    /// <inheritdoc />
    public override double DefaultButtonMinimumHeight =>
        OverrideButtonMinimumHeight ?? Style switch
        {
            DemoLookStyle.Chunky => 52,
            DemoLookStyle.Minimal => 40,
            _ => 44
        };

    /// <inheritdoc />
    public override double DefaultButtonCornerRadius => Style switch
    {
        DemoLookStyle.Chunky => 16,
        DemoLookStyle.Minimal => 2,
        _ => 6
    };

    /// <inheritdoc />
    public override Size DefaultSwitchSize =>
        OverrideSwitchSize ?? Style switch
        {
            DemoLookStyle.Chunky => new Size(64, 36),
            DemoLookStyle.Minimal => new Size(42, 24),
            _ => new Size(51, 31)
        };

    /// <inheritdoc />
    public override Size DefaultCheckBoxSize =>
        OverrideCheckBoxSize ?? Style switch
        {
            DemoLookStyle.Chunky => new Size(32, 32),
            DemoLookStyle.Minimal => new Size(20, 20),
            _ => new Size(24, 24)
        };

    /// <inheritdoc />
    public override Size DefaultRadioButtonSize =>
        OverrideRadioButtonSize ?? Style switch
        {
            DemoLookStyle.Chunky => new Size(32, 32),
            DemoLookStyle.Minimal => new Size(20, 20),
            _ => new Size(24, 24)
        };

    /// <inheritdoc />
    public override Size DefaultActivityIndicatorSize =>
        OverrideActivityIndicatorSize ?? Style switch
        {
            DemoLookStyle.Chunky => new Size(48, 48),
            DemoLookStyle.Minimal => new Size(28, 28),
            _ => new Size(36, 36)
        };

    /// <inheritdoc />
    protected override void DrawSwitchCore(SKCanvas canvas, SKRect bounds, bool isChecked, SKColor track, SKColor thumb)
    {
        if (Style == DemoLookStyle.Default)
        {
            base.DrawSwitchCore(canvas, bounds, isChecked, track, thumb);
            return;
        }

        if (Style == DemoLookStyle.Minimal)
        {
            // Flat capsule + square thumb.
            var radius = bounds.Height / 2;
            DrawRoundedBox(canvas, bounds, radius, track, SKColors.Transparent, 0);
            var thumbSize = bounds.Height - 4;
            var thumbX = isChecked ? bounds.Right - thumbSize - 2 : bounds.Left + 2;
            using var paint = new SKPaint { Color = thumb, IsAntialias = true };
            canvas.DrawRect(thumbX, bounds.Top + 2, thumbSize, thumbSize, paint);
            return;
        }

        // Chunky: inset track + large circular thumb with soft rim.
        var inset = 3f;
        var trackBounds = bounds;
        trackBounds.Inflate(-inset, -inset);
        var radiusTrack = trackBounds.Height / 2;
        DrawRoundedBox(canvas, trackBounds, radiusTrack, track, SKColors.Transparent, 0);
        var thumbRadius = bounds.Height / 2 - 1;
        var thumbX2 = isChecked ? bounds.Right - thumbRadius - 1 : bounds.Left + thumbRadius + 1;
        using var thumbPaint = new SKPaint { Color = thumb, IsAntialias = true };
        canvas.DrawCircle(thumbX2, bounds.MidY, thumbRadius, thumbPaint);
        using var rim = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 40),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            IsAntialias = true
        };
        canvas.DrawCircle(thumbX2, bounds.MidY, thumbRadius - 0.75f, rim);
    }

    /// <inheritdoc />
    protected override void DrawCheckBoxCore(SKCanvas canvas, float size, bool isChecked, SKColor fill, SKColor border)
    {
        if (Style == DemoLookStyle.Default)
        {
            base.DrawCheckBoxCore(canvas, size, isChecked, fill, border);
            return;
        }

        var radius = Style == DemoLookStyle.Chunky ? size * 0.28f : size * 0.08f;
        var stroke = Style == DemoLookStyle.Chunky ? 2.5f : 1f;
        DrawRoundedBox(canvas, new SKRect(0, 0, size, size), radius, fill, border, stroke);
        if (!isChecked) return;
        using var check = new SKPaint
        {
            Color = Style == DemoLookStyle.Minimal ? border : SKColors.White,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = size * (Style == DemoLookStyle.Chunky ? 0.14f : 0.1f),
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            IsAntialias = true
        };
        using var builder = new SKPathBuilder();
        builder.MoveTo(size * 0.2f, size * 0.52f);
        builder.LineTo(size * 0.42f, size * 0.74f);
        builder.LineTo(size * 0.82f, size * 0.26f);
        using var path = builder.Detach();
        canvas.DrawPath(path, check);
    }

    /// <inheritdoc />
    protected override void DrawRadioButtonCore(SKCanvas canvas, float size, bool isChecked, SKColor ring, SKColor dot)
    {
        if (Style == DemoLookStyle.Default)
        {
            base.DrawRadioButtonCore(canvas, size, isChecked, ring, dot);
            return;
        }

        if (Style == DemoLookStyle.Minimal)
        {
            var center = size / 2;
            using var ringPaint = new SKPaint
            {
                Color = ring,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = Math.Max(1f, size * 0.05f),
                IsAntialias = true
            };
            canvas.DrawCircle(center, center, center - ringPaint.StrokeWidth / 2, ringPaint);
            if (!isChecked) return;
            using var dotPaint = new SKPaint { Color = dot, IsAntialias = true };
            canvas.DrawCircle(center, center, size * 0.22f, dotPaint);
            return;
        }

        // Chunky: rounded square that fills when checked.
        var bounds = new SKRect(1, 1, size - 1, size - 1);
        if (isChecked)
            DrawRoundedBox(canvas, bounds, size * 0.22f, dot, SKColors.Transparent, 0);
        else
            DrawRoundedBox(canvas, bounds, size * 0.22f, SKColors.Transparent, ring, 2.5f);
    }

    /// <inheritdoc />
    protected override void DrawActivityIndicatorCore(SKCanvas canvas, float width, float height, float sweepStart, SKPaint paint)
    {
        if (Style == DemoLookStyle.Default)
        {
            base.DrawActivityIndicatorCore(canvas, width, height, sweepStart, paint);
            return;
        }

        if (width <= 0 || height <= 0) return;
        var strokeWidth = (float)Math.Max(Style == DemoLookStyle.Chunky ? 3 : 1.5, Math.Min(width, height) * (Style == DemoLookStyle.Chunky ? 0.14 : 0.08));
        paint.StrokeWidth = strokeWidth;
        var bounds = new SKRect(
            strokeWidth / 2,
            strokeWidth / 2,
            width - strokeWidth / 2,
            height - strokeWidth / 2);
        var sweep = Style == DemoLookStyle.Minimal ? 220f : 300f;
        canvas.DrawArc(bounds, sweepStart, sweep, false, paint);
    }
}
