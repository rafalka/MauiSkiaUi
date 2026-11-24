using SkiaSharp;

namespace MauiSkiaUi;

public partial class DefaultSkUiLook
{
    #region Segmented control

    /// <summary>How segmented controls draw (default <see cref="SkUiSegmentedStyle.Pill"/>; the platform looks draw theirs).</summary>
    public virtual SkUiSegmentedStyle SegmentedStyle => SkUiSegmentedStyle.Pill;

    /// <summary>Whether the selection fades between segments instead of sliding (<see cref="SkUiSegmentedStyle.Outlined"/> and <see cref="SkUiSegmentedStyle.Tinted"/>).</summary>
    public bool SegmentedSelectionFades => SegmentedStyle is SkUiSegmentedStyle.Outlined or SkUiSegmentedStyle.Tinted;

    /// <inheritdoc />
    /// <remarks>
    /// By style: a pill in the accent color with white text; a raised thumb (white, or lighter than the background in a dark
    /// scheme) with the text color; an accent-tinted fill with the text color (outlined) or the accent (tinted); an accent bar
    /// with the text color over a dimmer unselected text (bar).
    /// </remarks>
    public override SkUiSegmentedColors GetSegmentedColors(SkUiColorScheme scheme)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        var foreground = scheme.DefaultForeground;
        return SegmentedStyle switch
        {
            SkUiSegmentedStyle.Thumb => new SkUiSegmentedColors(scheme.TrackOff, IsDark(scheme) ? Lighter(scheme.DefaultBackground, foreground) : Colors.White,
                foreground, foreground, scheme.Muted),
            SkUiSegmentedStyle.Outlined => new SkUiSegmentedColors(Colors.Transparent, scheme.Accent.WithAlpha(0.24f), foreground, foreground, scheme.Muted),
            SkUiSegmentedStyle.Tinted => new SkUiSegmentedColors(Colors.Transparent, scheme.Accent.WithAlpha(0.12f), foreground, scheme.Accent, scheme.Muted),
            SkUiSegmentedStyle.Bar => new SkUiSegmentedColors(Colors.Transparent, scheme.Accent, foreground.WithAlpha(0.7f), foreground, scheme.Muted),
            _ => base.GetSegmentedColors(scheme)
        };
    }

    private static bool IsDark(SkUiColorScheme scheme) => scheme.DefaultBackground.GetLuminosity() < 0.5f;

    private static Color Lighter(Color background, Color foreground) =>
        new(background.Red + (foreground.Red - background.Red) * 0.3f, background.Green + (foreground.Green - background.Green) * 0.3f,
            background.Blue + (foreground.Blue - background.Blue) * 0.3f);

    /// <inheritdoc />
    /// <remarks>Fading styles: the selected segment by the change's progress, the one it leaves by the rest.</remarks>
    public override float GetSegmentSelection(in SkUiSegmentedPaint paint, int index)
    {
        if (!SegmentedSelectionFades)
            return base.GetSegmentSelection(paint, index);
        var progress = Math.Clamp(paint.Progress, 0, 1);
        if (index >= 0 && index == paint.SelectedIndex)
            return paint.PreviousIndex == index ? 1 : progress;
        return index >= 0 && index == paint.PreviousIndex ? 1 - progress : 0;
    }

    /// <inheritdoc />
    /// <remarks>Outlined: the check and the gap after it, growing in with the selection.</remarks>
    public override float GetSegmentLeadingWidth(in SkUiSegmentedPaint paint, int index)
    {
        var metrics = SegmentedMetrics;
        if (SegmentedStyle != SkUiSegmentedStyle.Outlined || metrics.CheckSize <= 0)
            return 0;
        return (float)(metrics.CheckSize + metrics.IconSpacing) * GetSegmentSelection(paint, index);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <see cref="SkUiSegmentedStyle.Pill"/>: a filled track and a pill marker. <see cref="SkUiSegmentedStyle.Thumb"/>: a
    /// filled track, dividers that fade next to the thumb, and a thumb with a soft shadow. <see cref="SkUiSegmentedStyle.Outlined"/>
    /// and <see cref="SkUiSegmentedStyle.Tinted"/>: an outlined track with dividers, the selected segment's fill fading in, and
    /// (outlined) a check before its content. <see cref="SkUiSegmentedStyle.Bar"/>: a bar as wide as the selected content under
    /// it. Every style tints the pressed and hovered segments and draws an accent ring while focused.
    /// </remarks>
    protected override void DrawSegmentedCore(SKCanvas canvas, SkUiSegmentedPaint paint)
    {
        var bounds = paint.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0 || paint.Segments is not { } segments)
            return;
        var radius = Math.Clamp(paint.CornerRadius, 0, Math.Min(bounds.Width, bounds.Height) / 2);
        var dim = paint.IsEnabled ? 1f : SegmentDimmedOpacity;
        var style = SegmentedStyle;
        var fill = Paint(paint.Track);
        if (paint.Track.Alpha > 0)
            canvas.DrawRoundRect(bounds, radius, radius, fill);
        var save = canvas.Save();
        using (var clip = new SKRoundRect(bounds, radius))
            canvas.ClipRoundRect(clip, antialias: true);
        switch (style)
        {
            case SkUiSegmentedStyle.Outlined or SkUiSegmentedStyle.Tinted:
                for (var index = 0; index < segments.Count; index++)
                {
                    var amount = GetSegmentSelection(paint, index);
                    if (amount <= 0)
                        continue;
                    fill.Color = Fade(paint.SelectionColor, amount * dim);
                    canvas.DrawRect(segments[index], fill);
                }
                break;
            case SkUiSegmentedStyle.Pill or SkUiSegmentedStyle.Thumb when !paint.Selection.IsEmpty:
                var marker = paint.Selection;
                var inset = Math.Max(0, marker.Top - bounds.Top);
                var markerRadius = Math.Max(0, radius - inset);
                if (style == SkUiSegmentedStyle.Thumb)
                {
                    // Dividers between segments the thumb is not next to, fading as it nears them.
                    DrawDividers(canvas, paint, fadeNearSelection: true, dim);
                    var shadow = marker;
                    shadow.Offset(0, 1);
                    shadow.Inflate(0.5f, 0.5f);
                    canvas.DrawRoundRect(shadow, markerRadius + 0.5f, markerRadius + 0.5f, Paint(new SKColor(0, 0, 0, (byte)(36 * dim))));
                    fill = Paint(Fade(paint.SelectionColor, dim));
                }
                else
                {
                    fill.Color = Fade(paint.SelectionColor, dim);
                }
                canvas.DrawRoundRect(marker, markerRadius, markerRadius, fill);
                break;
            case SkUiSegmentedStyle.Bar when !paint.Selection.IsEmpty:
                DrawSegmentBar(canvas, paint, dim);
                break;
        }
        // Pressed and hovered segments: a tint of the text color over the segment, rounded as the marker where segments are
        // apart from each other (outlined and tinted segments share their dividers: square inside the track's clip).
        var tintRadius = style is SkUiSegmentedStyle.Outlined or SkUiSegmentedStyle.Tinted
            ? 0 : Math.Max(0, radius - (float)Math.Max(0, (segments.Count > 0 ? segments[0].Top : bounds.Top) - bounds.Top));
        DrawSegmentTint(canvas, paint, paint.PressedIndex, 0.12f * Math.Clamp(paint.Pressed, 0, 1), tintRadius);
        if (paint.HoveredIndex != paint.PressedIndex || paint.Pressed <= 0)
            DrawSegmentTint(canvas, paint, paint.HoveredIndex, 0.06f, tintRadius);
        canvas.RestoreToCount(save);
        if (style is SkUiSegmentedStyle.Outlined or SkUiSegmentedStyle.Tinted)
        {
            DrawDividers(canvas, paint, fadeNearSelection: false, dim);
            var stroke = Paint(Fade(paint.Border, dim));
            stroke.Style = SKPaintStyle.Stroke;
            stroke.StrokeWidth = 1;
            var outline = bounds;
            outline.Inflate(-0.5f, -0.5f);
            canvas.DrawRoundRect(outline, Math.Max(0, radius - 0.5f), Math.Max(0, radius - 0.5f), stroke);
            if (style == SkUiSegmentedStyle.Outlined)
                for (var index = 0; index < segments.Count; index++)
                    DrawSegmentCheck(canvas, paint, index);
        }
        if (paint.IsEnabled && paint.Focused > 0)
        {
            var width = 2 * Math.Clamp(paint.Focused, 0, 1);
            var ring = bounds;
            ring.Inflate(-width / 2, -width / 2);
            var stroke = Paint(paint.Accent);
            stroke.Style = SKPaintStyle.Stroke;
            stroke.StrokeWidth = width;
            canvas.DrawRoundRect(ring, Math.Max(0, radius - width / 2), Math.Max(0, radius - width / 2), stroke);
        }
    }

    private static SKColor Fade(SKColor color, float amount) => color.WithAlpha((byte)(color.Alpha * Math.Clamp(amount, 0, 1)));

    /// <summary>One-DIP dividers between neighbors, inset from the top and bottom; next to the selection they fade with it (thumb) or stay (outlined).</summary>
    private void DrawDividers(SKCanvas canvas, in SkUiSegmentedPaint paint, bool fadeNearSelection, float dim)
    {
        var segments = paint.Segments;
        if (segments.Count < 2)
            return;
        var bounds = paint.Bounds;
        var inset = fadeNearSelection ? bounds.Height * 0.25f : 0;
        var line = Paint(paint.Border);
        for (var index = 0; index + 1 < segments.Count; index++)
        {
            // Between this segment and the next one along the row, whichever side that is.
            var (a, b) = (segments[index], segments[index + 1]);
            var x = a.Left < b.Left ? (a.Right + b.Left) / 2 : (b.Right + a.Left) / 2;
            var amount = fadeNearSelection ? 1 - Math.Max(GetSegmentSelection(paint, index), GetSegmentSelection(paint, index + 1)) : 1;
            if (amount <= 0)
                continue;
            line.Color = Fade(paint.Border, amount * dim);
            canvas.DrawRect(new SKRect(x - 0.5f, bounds.Top + inset, x + 0.5f, bounds.Bottom - inset), line);
        }
    }

    /// <summary>The bar style's marker: a 3 DIP bar with rounded ends at the selection's bottom, as wide as the selected content (blended while it moves).</summary>
    private void DrawSegmentBar(SKCanvas canvas, in SkUiSegmentedPaint paint, float dim)
    {
        var marker = paint.Selection;
        var widths = paint.ContentWidths;
        float Content(int index) => widths is not null && index >= 0 && index < widths.Count ? widths[index] : marker.Width / 2;
        var content = paint.PreviousIndex >= 0 && paint.PreviousIndex != paint.SelectedIndex
            ? Content(paint.PreviousIndex) + (Content(paint.SelectedIndex) - Content(paint.PreviousIndex)) * Math.Clamp(paint.Progress, 0, 1)
            : Content(paint.SelectedIndex);
        var width = Math.Clamp(content, Math.Min(16, marker.Width), marker.Width);
        const float thickness = 3;
        var bar = new SKRect(marker.MidX - width / 2, marker.Bottom - thickness - 1, marker.MidX + width / 2, marker.Bottom - 1);
        canvas.DrawRoundRect(bar, thickness / 2, thickness / 2, Paint(Fade(paint.SelectionColor, dim)));
    }

    private static void DrawSegmentTint(SKCanvas canvas, in SkUiSegmentedPaint paint, int index, float amount, float radius)
    {
        if (amount <= 0 || index < 0 || index >= paint.Count || !paint.IsSegmentEnabled(index))
            return;
        var segment = paint.Segments[index];
        var corner = Math.Min(radius, Math.Min(segment.Width, segment.Height) / 2);
        canvas.DrawRoundRect(segment, corner, corner, Paint(paint.Text.WithAlpha((byte)(255 * amount))));
    }

    /// <summary>The outlined style's check, at the start of the selected segment's centered content, growing and fading in with the selection.</summary>
    private void DrawSegmentCheck(SKCanvas canvas, in SkUiSegmentedPaint paint, int index)
    {
        var amount = GetSegmentSelection(paint, index);
        // As wide as the room it takes before the content, which grows with the selection.
        var size = (float)SegmentedMetrics.CheckSize * amount;
        if (amount <= 0 || size <= 0)
            return;
        var segment = paint.Segments[index];
        var content = paint.ContentWidths is { } widths && index < widths.Count ? widths[index] : 0;
        var leading = GetSegmentLeadingWidth(paint, index);
        var start = segment.MidX - (content + leading) / 2;
        var left = paint.IsRightToLeft ? segment.MidX + (content + leading) / 2 - size : start;
        var (top, scale) = (segment.MidY - size / 2, size / 24f);
        using var builder = new SKPathBuilder();
        // Material's check glyph (24 DIP grid), scaled to the check size.
        builder.MoveTo(new SKPoint(left + 5 * scale, top + 12.5f * scale));
        builder.LineTo(new SKPoint(left + 9.5f * scale, top + 17 * scale));
        builder.LineTo(new SKPoint(left + 19 * scale, top + 7.5f * scale));
        using var path = builder.Detach();
        var color = GetSegmentTextColor(paint, index);
        var stroke = Paint(color.WithAlpha((byte)(color.Alpha * amount)));
        stroke.Style = SKPaintStyle.Stroke;
        stroke.StrokeWidth = 2 * scale;
        stroke.StrokeCap = SKStrokeCap.Round;
        stroke.StrokeJoin = SKStrokeJoin.Round;
        canvas.DrawPath(path, stroke);
    }

    #endregion

    #region Tab strip

    /// <summary>How a tab view's strip and indicator draw (default <see cref="SkUiTabStripStyle.HeaderUnderline"/>; the platform looks draw theirs).</summary>
    public virtual SkUiTabStripStyle TabStripStyle => SkUiTabStripStyle.HeaderUnderline;

    /// <inheritdoc />
    /// <remarks>
    /// <see cref="SkUiTabStripStyle.Filled"/>: the accent strip with white headers (dimmer unselected) and a white indicator,
    /// no divider. <see cref="SkUiTabStripStyle.ShortBar"/>: no divider.
    /// </remarks>
    public override SkUiTabStripColors GetTabStripColors(SkUiColorScheme scheme)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        return TabStripStyle switch
        {
            SkUiTabStripStyle.Filled => new SkUiTabStripColors(scheme.Accent, Colors.White.WithAlpha(0.7f), Colors.White, Colors.White, Colors.Transparent),
            SkUiTabStripStyle.ShortBar => base.GetTabStripColors(scheme) with { Divider = Colors.Transparent },
            _ => base.GetTabStripColors(scheme)
        };
    }

    /// <inheritdoc />
    /// <remarks>
    /// Underlines by style: as wide as the header (<see cref="SkUiTabStripStyle.HeaderUnderline"/>,
    /// <see cref="SkUiTabStripStyle.Filled"/>), as wide as the label (<see cref="SkUiTabStripStyle.LabelUnderline"/>), or a
    /// 16 DIP bar centered under the label, 4 DIPs from the edge (<see cref="SkUiTabStripStyle.ShortBar"/>).
    /// </remarks>
    public override Rect GetTabIndicatorBounds(Rect header, double labelWidth, SkUiTabIndicatorStyle style, bool stripAtBottom)
    {
        if (style != SkUiTabIndicatorStyle.Underline)
            return base.GetTabIndicatorBounds(header, labelWidth, style, stripAtBottom);
        var thickness = TabStripMetrics.IndicatorThickness;
        var width = TabStripStyle switch
        {
            SkUiTabStripStyle.LabelUnderline => Math.Clamp(labelWidth, 0, header.Width),
            SkUiTabStripStyle.ShortBar => Math.Min(16, header.Width),
            _ => header.Width
        };
        var gap = TabStripStyle == SkUiTabStripStyle.ShortBar ? 4 : 0;
        var y = stripAtBottom ? header.Top + gap : header.Bottom - thickness - gap;
        return new Rect(header.Center.X - width / 2, y, width, thickness);
    }

    /// <inheritdoc />
    /// <remarks>Label underlines and short bars have rounded ends; header underlines are square.</remarks>
    protected override void DrawTabIndicatorCore(SKCanvas canvas, SkUiTabIndicatorPaint paint)
    {
        if (paint.Style == SkUiTabIndicatorStyle.Underline && TabStripStyle is SkUiTabStripStyle.LabelUnderline or SkUiTabStripStyle.ShortBar)
        {
            var bounds = paint.Bounds;
            var radius = Math.Min(bounds.Width, bounds.Height) / 2;
            canvas.DrawRoundRect(bounds, radius, radius, Paint(paint.Color));
            return;
        }
        base.DrawTabIndicatorCore(canvas, paint);
    }

    #endregion
}
