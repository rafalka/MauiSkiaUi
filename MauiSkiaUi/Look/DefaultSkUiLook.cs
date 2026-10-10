using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// Built-in control look: current cross-platform geometry and intrinsic sizes (former <c>SkUiChrome</c>).
/// Subclass or set painter/measure delegates to customize without replacing every control type.
/// State changes animate (<see cref="GetTransitionCore"/>): switch thumbs slide, check marks draw in, radio dots grow,
/// pressed buttons dim or ripple (<see cref="PressEffect"/>) and slider thumbs glide to tapped values.
/// </summary>
public class DefaultSkUiLook : SkUiLook
{
    /// <summary>Shared default look used as the process <see cref="SkUiLook.Current"/>.</summary>
    public static DefaultSkUiLook Instance { get; } = new();

    /// <summary>Button and ImageButton press feedback (default <see cref="SkUiPressEffect.Dim"/>). Repaint after changing it.</summary>
    public SkUiPressEffect PressEffect { get; set; } = SkUiPressEffect.Dim;

    /// <summary>
    /// Overscroll of scrollers that do not set their own: the platform's by default (<see cref="SkUiOverscrollMode.Bounce"/> on
    /// iOS and Mac Catalyst, <see cref="SkUiOverscrollMode.Stretch"/> on Android, none elsewhere).
    /// </summary>
    public SkUiOverscrollMode Overscroll { get; set; } = PlatformOverscroll;

    /// <summary>The platform's overscroll: iOS and Mac Catalyst bounce, Android 12+ stretches, Windows stops at the edge.</summary>
    public static SkUiOverscrollMode PlatformOverscroll =>
        OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst() ? SkUiOverscrollMode.Bounce
        : OperatingSystem.IsAndroid() ? SkUiOverscrollMode.Stretch
        : SkUiOverscrollMode.None;

    /// <inheritdoc />
    public override SkUiOverscrollMode DefaultOverscroll => Overscroll == SkUiOverscrollMode.Default ? SkUiOverscrollMode.None : Overscroll;

    /// <summary>
    /// Pull-to-refresh style of controls that do not set their own: the platform's by default
    /// (<see cref="SkUiRefreshStyle.Inline"/> on iOS and Mac Catalyst, <see cref="SkUiRefreshStyle.Overlay"/> elsewhere).
    /// </summary>
    public SkUiRefreshStyle RefreshStyle { get; set; } = PlatformRefreshStyle;

    /// <summary>The platform's pull-to-refresh style: iOS and Mac Catalyst inline (UIRefreshControl), others an overlay badge (Android's).</summary>
    public static SkUiRefreshStyle PlatformRefreshStyle =>
        OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst() ? SkUiRefreshStyle.Inline : SkUiRefreshStyle.Overlay;

    /// <inheritdoc />
    public override SkUiRefreshStyle DefaultRefreshStyle => RefreshStyle == SkUiRefreshStyle.Default ? SkUiRefreshStyle.Overlay : RefreshStyle;

    /// <summary>How indicator views draw (default <see cref="SkUiIndicatorStyle.Dots"/>). Call <see cref="SkUiLook.NotifyChanged"/> after changing it.</summary>
    public SkUiIndicatorStyle IndicatorStyle { get; set; } = SkUiIndicatorStyle.Dots;

    /// <inheritdoc />
    /// <remarks>A <see cref="SkUiIndicatorStyle.Pill"/> is twice the indicator size longer.</remarks>
    public override double GetSelectedIndicatorExtraLength(double indicatorSize, IndicatorShape shape) =>
        IndicatorStyle == SkUiIndicatorStyle.Pill ? 2 * indicatorSize : 0;

    /// <inheritdoc />
    /// <remarks>
    /// Circles (or squares) in a blend of the two colors by selection; a <see cref="SkUiIndicatorStyle.Pill"/> rounds the
    /// stretched selected indicator (squares keep a small corner).
    /// </remarks>
    protected override void DrawIndicatorsCore(SKCanvas canvas, SkUiIndicatorPaint paint)
    {
        if (paint.Count <= 0 || paint.IndicatorSize <= 0)
            return;
        var fill = Paint(paint.Color);
        var radius = paint.Shape == IndicatorShape.Circle ? paint.IndicatorSize / 2 : Math.Min(1.5f, paint.IndicatorSize / 4);
        for (var slot = 0; slot < paint.Count; slot++)
        {
            fill.Color = paint.GetColor(slot);
            canvas.DrawRoundRect(paint.GetIndicatorBounds(slot), radius, radius, fill);
        }
    }

    private static readonly Shadow OverlayRefreshShadow = new() { Brush = Colors.Black, Opacity = 0.25f, Radius = 4, Offset = new Point(0, 1) };

    /// <inheritdoc />
    /// <remarks>The overlay badge casts a soft shadow (Android's); inline indicators none.</remarks>
    public override Shadow? GetRefreshIndicatorShadow(SkUiRefreshStyle style) => style == SkUiRefreshStyle.Overlay ? OverlayRefreshShadow : null;

    /// <inheritdoc />
    protected override SkUiTransition GetTransitionCore(SkUiTransitionKind kind) => kind switch
    {
        SkUiTransitionKind.Switch => SkUiTransition.FromMilliseconds(200, Easing.CubicInOut),
        SkUiTransitionKind.CheckBox => SkUiTransition.FromMilliseconds(160, Easing.CubicInOut),
        SkUiTransitionKind.RadioButton => SkUiTransition.FromMilliseconds(160, Easing.CubicInOut),
        SkUiTransitionKind.Press => SkUiTransition.FromMilliseconds(80, Easing.CubicOut),
        SkUiTransitionKind.Release => SkUiTransition.FromMilliseconds(220, Easing.CubicOut),
        // Only rippling looks spend frames on the ripple.
        SkUiTransitionKind.Ripple => PressEffect == SkUiPressEffect.Ripple ? SkUiTransition.FromMilliseconds(450, Easing.CubicOut) : SkUiTransition.None,
        SkUiTransitionKind.SliderThumb => SkUiTransition.FromMilliseconds(150, Easing.CubicOut),
        SkUiTransitionKind.IndicatorPosition => SkUiTransition.FromMilliseconds(250, Easing.CubicOut),
        // Determinate progress follows Progress at once, as MAUI's ProgressBar (ProgressTo animates).
        _ => SkUiTransition.None
    };

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

    [ThreadStatic] private static SKPaint? _paint;

    /// <summary>Per-thread reusable fill paint (recording is single-threaded per surface; pictures copy paint state).</summary>
    private static SKPaint Paint(SKColor color)
    {
        var paint = _paint ??= new SKPaint();
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
    protected override void DrawButtonCore(SKCanvas canvas, SkUiButtonPaint button)
    {
        var dim = PressEffect == SkUiPressEffect.Dim ? 1 - 0.25f * button.Press.Pressed : 1;
        if (button.FillPaint is { } gradient)
        {
            DrawRoundedBox(canvas, button.Bounds, button.CornerRadii, SkUiShapePainter.WithOpacity(gradient, dim), button.Border, button.BorderWidth);
        }
        else
        {
            var fill = button.Fill;
            if (dim < 1)
                fill = fill.WithAlpha((byte)(fill.Alpha * dim));
            DrawRoundedBox(canvas, button.Bounds, button.CornerRadii, fill, button.Border, button.BorderWidth);
        }
        if (PressEffect == SkUiPressEffect.Ripple && button.IsEnabled)
            DrawRipple(canvas, button.Bounds, button.CornerRadii, button.Press, RippleColor(button.Fill));
    }

    /// <inheritdoc />
    protected override void DrawSwitchCore(SKCanvas canvas, SkUiSwitchPaint toggle)
    {
        var bounds = toggle.Bounds;
        var visual = toggle.Visual;
        var radius = bounds.Height / 2;
        // Indeterminate: halfway between off and on, thumb in the middle.
        var track = visual.Blend(toggle.OffTrack, toggle.OnTrack, Mix(toggle.OffTrack, toggle.OnTrack, 0.5f));
        DrawRoundedBox(canvas, bounds, radius, track, SKColors.Transparent, 0);
        var thumbRadius = radius - 2;
        var start = bounds.Left + radius;
        var end = bounds.Right - radius;
        var thumbX = visual.Blend(start, end, bounds.MidX);
        // Pressed: the thumb stretches towards the middle (iOS-like).
        var stretch = thumbRadius * 0.35f * visual.Pressed;
        var position = end > start ? (thumbX - start) / (end - start) : 0;
        var thumb = new SKRect(thumbX - thumbRadius - stretch * position, bounds.Top + radius - thumbRadius,
            thumbX + thumbRadius + stretch * (1 - position), bounds.Top + radius + thumbRadius);
        canvas.DrawRoundRect(thumb, thumbRadius, thumbRadius, Paint(toggle.Thumb));
    }

    /// <inheritdoc />
    protected override void DrawCheckBoxCore(SKCanvas canvas, SkUiCheckBoxPaint box)
    {
        var size = box.Size;
        var visual = box.Visual;
        var on = 1 - visual.Weight(SkUiCheckState.Unchecked);
        var bounds = new SKRect(0, 0, size, size);
        DrawRoundedBox(canvas, bounds, size * 0.2f, Mix(box.Background, box.Color, on), Mix(box.Border, box.Color, on), 1.5f);
        var check = visual.Weight(SkUiCheckState.Checked);
        var dash = visual.Weight(SkUiCheckState.Indeterminate);
        if (check <= 0 && dash <= 0) return;
        var paint = Paint(SKColors.White);
        Stroke(paint, SKColors.White, size * 0.12f);
        paint.StrokeCap = SKStrokeCap.Round;
        paint.StrokeJoin = SKStrokeJoin.Round;
        if (dash > 0)
        {
            // The dash grows from the middle.
            var half = size * 0.24f * dash;
            canvas.DrawLine(size * 0.5f - half, size * 0.5f, size * 0.5f + half, size * 0.5f, paint);
        }
        if (check > 0)
            DrawCheckMark(canvas, size, check, paint);
    }

    /// <summary>The check mark's two strokes, drawn in up to <paramref name="amount"/> of their length (0–1).</summary>
    private static void DrawCheckMark(SKCanvas canvas, float size, float amount, SKPaint paint)
    {
        var a = new SKPoint(size * 0.22f, size * 0.55f);
        var b = new SKPoint(size * 0.42f, size * 0.75f);
        var c = new SKPoint(size * 0.8f, size * 0.28f);
        var first = SKPoint.Distance(a, b);
        var drawn = (first + SKPoint.Distance(b, c)) * Math.Clamp(amount, 0, 1);
        if (drawn <= first)
        {
            canvas.DrawLine(a, Lerp(a, b, drawn / first), paint);
            return;
        }
        using var builder = new SKPathBuilder();
        builder.MoveTo(a);
        builder.LineTo(b);
        builder.LineTo(Lerp(b, c, (drawn - first) / SKPoint.Distance(b, c)));
        using var path = builder.Detach();
        canvas.DrawPath(path, paint);
    }

    /// <inheritdoc />
    protected override void DrawRadioButtonCore(SKCanvas canvas, SkUiRadioButtonPaint radio)
    {
        var size = radio.Size;
        var visual = radio.Visual;
        var center = size / 2;
        var paint = Paint(SKColors.Transparent);
        Stroke(paint, Mix(radio.Ring, radio.Color, 1 - visual.Weight(SkUiCheckState.Unchecked)), size * 0.08f);
        canvas.DrawCircle(center, center, center - paint.StrokeWidth / 2, paint);
        // The dot (or the indeterminate bar) grows from the center.
        var dot = visual.Weight(SkUiCheckState.Checked);
        if (dot > 0)
            canvas.DrawCircle(center, center, size * 0.28f * dot, Paint(radio.Color));
        var bar = visual.Weight(SkUiCheckState.Indeterminate);
        if (bar > 0)
        {
            var half = size * 0.14f;
            var length = size * 0.26f * bar;
            canvas.DrawRoundRect(new SKRect(center - length, center - half / 2, center + length, center + half / 2), half / 2, half / 2, Paint(radio.Color));
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
        if (slider.HasThumbImage)
            return; // the control draws the image
        if (slider.Pressed > 0)
            canvas.DrawCircle(thumbX, bounds.MidY, radius * (1 + 0.8f * slider.Pressed),
                Paint(slider.Thumb.WithAlpha((byte)(slider.Thumb.Alpha / 5 * slider.Pressed))));
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
    /// <remarks>A rounded bar, fully rounded across its thickness; darker while dragged.</remarks>
    protected override void DrawScrollBarCore(SKCanvas canvas, SkUiScrollBarPaint bar)
    {
        var radius = Math.Min(bar.Bounds.Width, bar.Bounds.Height) / 2;
        var color = bar.IsPressed ? bar.Color.WithAlpha((byte)Math.Min(255, bar.Color.Alpha * 1.6f)) : bar.Color;
        canvas.DrawRoundRect(bar.Bounds, radius, radius, Paint(color));
    }

    /// <inheritdoc />
    /// <remarks>A rounded strip in the thumb's color at a quarter of its opacity.</remarks>
    protected override void DrawScrollBarTrackCore(SKCanvas canvas, SkUiScrollBarTrackPaint track)
    {
        var radius = Math.Min(track.Bounds.Width, track.Bounds.Height) / 2;
        canvas.DrawRoundRect(track.Bounds, radius, radius, Paint(track.Color.WithAlpha((byte)(track.Color.Alpha / 4))));
    }

    /// <inheritdoc />
    protected override void DrawPressOverlayCore(SKCanvas canvas, SkUiPressOverlayPaint overlay)
    {
        // Over arbitrary content (images, cards): a dark tint or a dark ripple shows on light and mid-tone content alike.
        var alpha = !overlay.IsEnabled ? 96 : PressEffect == SkUiPressEffect.Dim ? 48 * overlay.Press.Pressed : 0;
        if (alpha > 0)
        {
            var paint = Paint(new SKColor(0, 0, 0, (byte)alpha));
            if (IsUniformCornerRadius(overlay.CornerRadii) && overlay.CornerRadii.TopLeft <= 0)
            {
                canvas.DrawRect(overlay.Bounds, paint);
            }
            else
            {
                using var path = CreateRoundRectPath(overlay.Bounds, overlay.CornerRadii);
                canvas.DrawPath(path, paint);
            }
        }
        if (PressEffect == SkUiPressEffect.Ripple && overlay.IsEnabled)
            DrawRipple(canvas, overlay.Bounds, overlay.CornerRadii, overlay.Press, new SKColor(0, 0, 0, 56));
    }

    /// <summary>Thickness of the default focus ring's outer line, DIPs.</summary>
    public virtual float FocusRingThickness => 2;

    /// <inheritdoc />
    /// <remarks>
    /// Two lines inside the bounds, following the control's corners: the foreground color outside and a one-DIP line of the
    /// background color inside it, so the ring shows on light, dark and accent fills alike (as Windows' focus visuals).
    /// </remarks>
    protected override void DrawFocusRingCore(SKCanvas canvas, SkUiFocusRingPaint ring)
    {
        var width = FocusRingThickness;
        var bounds = ring.Bounds;
        if (bounds.Width <= 2 * width || bounds.Height <= 2 * width)
            return;
        var paint = Paint(ring.Color);
        DrawRingLine(canvas, paint, bounds, ring.CornerRadii, width, 0, ring.Color);
        DrawRingLine(canvas, paint, bounds, ring.CornerRadii, 1, width, ring.Contrast);
    }

    private void DrawRingLine(SKCanvas canvas, SKPaint paint, SKRect bounds, CornerRadius radii, float width, float inset, SKColor color)
    {
        var outset = inset + width / 2;
        bounds.Inflate(-outset, -outset);
        Stroke(paint, color, width);
        var shrunk = new CornerRadius(Math.Max(0, radii.TopLeft - outset), Math.Max(0, radii.TopRight - outset),
            Math.Max(0, radii.BottomLeft - outset), Math.Max(0, radii.BottomRight - outset));
        if (IsUniformCornerRadius(shrunk))
        {
            canvas.DrawRoundRect(bounds, (float)shrunk.TopLeft, (float)shrunk.TopLeft, paint);
            return;
        }
        using var path = CreateRoundRectPath(bounds, shrunk);
        canvas.DrawPath(path, paint);
    }

    /// <summary>
    /// A Material-like ripple: a circle spreading from the press point to the farthest corner, fading after the
    /// release, over a light press overlay; clipped to the control's rounded shape.
    /// </summary>
    protected void DrawRipple(SKCanvas canvas, SKRect bounds, CornerRadius radii, SkUiPressVisual press, SKColor color)
    {
        if (!press.HasRipple && press.Pressed <= 0) return;
        var save = canvas.Save();
        using (var clip = CreateRoundRectPath(bounds, radii))
            canvas.ClipPath(clip, antialias: true);
        if (press.Pressed > 0)
            canvas.DrawRect(bounds, Paint(color.WithAlpha((byte)(color.Alpha * 0.4f * press.Pressed))));
        if (press.HasRipple)
        {
            var origin = press.Origin;
            var reach = Math.Max(
                Math.Max(SKPoint.Distance(origin, new SKPoint(bounds.Left, bounds.Top)), SKPoint.Distance(origin, new SKPoint(bounds.Right, bounds.Top))),
                Math.Max(SKPoint.Distance(origin, new SKPoint(bounds.Left, bounds.Bottom)), SKPoint.Distance(origin, new SKPoint(bounds.Right, bounds.Bottom))));
            var radius = reach * (0.1f + 0.9f * press.Ripple);
            canvas.DrawCircle(origin, radius, Paint(color.WithAlpha((byte)(color.Alpha * (1 - press.RippleFade)))));
        }
        canvas.RestoreToCount(save);
    }

    /// <summary>Ripple color over <paramref name="fill"/>: dark over light fills, light over dark ones.</summary>
    private static SKColor RippleColor(SKColor fill) =>
        0.299f * fill.Red + 0.587f * fill.Green + 0.114f * fill.Blue > 160 && fill.Alpha > 64
            ? new SKColor(0, 0, 0, 60)
            : new SKColor(255, 255, 255, 90);

    private static SKPoint Lerp(SKPoint from, SKPoint to, float amount) =>
        new(from.X + (to.X - from.X) * amount, from.Y + (to.Y - from.Y) * amount);

    /// <summary>Blends two colors (straight alpha): 0 gives <paramref name="from"/>, 1 <paramref name="to"/>.</summary>
    protected static SKColor Mix(SKColor from, SKColor to, float amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        if (amount <= 0) return from;
        if (amount >= 1) return to;
        static byte Channel(byte a, byte b, float t) => (byte)Math.Round(a + (b - a) * t);
        return new SKColor(Channel(from.Red, to.Red, amount), Channel(from.Green, to.Green, amount),
            Channel(from.Blue, to.Blue, amount), Channel(from.Alpha, to.Alpha, amount));
    }
}
