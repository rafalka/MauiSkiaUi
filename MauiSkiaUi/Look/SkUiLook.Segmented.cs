using SkiaSharp;

namespace MauiSkiaUi;

public partial class SkUiLook
{
    #region Segmented control

    /// <summary>The opacity of a disabled segment's text, icon and marker.</summary>
    internal const float SegmentDimmedOpacity = 0.38f;

    /// <summary>
    /// Segmented controls' sizes (default: 36 high, the segments 2 DIPs inside the track, 12 DIPs of padding beside each
    /// segment's content, an 8 DIP corner radius, 18 DIP icons 6 DIPs before the text, no check).
    /// </summary>
    public virtual SkUiSegmentedMetrics SegmentedMetrics => new(36, 2, 0, 12, 8, 18, 6, 0);

    /// <summary>
    /// The colors a segmented control uses where it sets none, from <paramref name="scheme"/> (default: the off track's color
    /// for the track, the accent for the selection, the foreground for the text, white for the selected text, the muted color
    /// for outlines).
    /// </summary>
    public virtual SkUiSegmentedColors GetSegmentedColors(SkUiColorScheme scheme)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        return new SkUiSegmentedColors(scheme.TrackOff, scheme.Accent, scheme.DefaultForeground, Colors.White, scheme.Muted);
    }

    /// <summary>Optional segmented control painter; when set, replaces <see cref="DrawSegmentedCore"/>.</summary>
    public Action<SKCanvas, SkUiSegmentedPaint>? SegmentedPainter { get; set; }

    /// <summary>
    /// Draws a segmented control's chrome (delegate or <see cref="DrawSegmentedCore"/>): the track, the dividers, the selection
    /// marker in <see cref="SkUiSegmentedPaint.Selection"/> (or the selected segment's fill, by
    /// <see cref="GetSegmentSelection"/>), a check, and the pressed, hovered, disabled and focused states. The control draws the
    /// segments' texts and icons over it. It is drawn again on every frame of a selection change, so keep it cheap.
    /// </summary>
    public void DrawSegmented(SKCanvas canvas, SkUiSegmentedPaint paint)
    {
        if (SegmentedPainter is { } painter)
        {
            painter(canvas, paint);
            return;
        }
        DrawSegmentedCore(canvas, paint);
    }

    /// <summary>Default segmented chrome: none (<see cref="DefaultSkUiLook"/> draws it).</summary>
    protected virtual void DrawSegmentedCore(SKCanvas canvas, SkUiSegmentedPaint paint) { }

    /// <summary>
    /// How selected segment <paramref name="index"/> looks now, 0–1: the share of it the sliding marker covers (default), or,
    /// in a look whose selection fades, how far the change has come.
    /// </summary>
    public virtual float GetSegmentSelection(in SkUiSegmentedPaint paint, int index)
    {
        if (paint.Segments is not { } segments || index < 0 || index >= segments.Count || paint.Selection.IsEmpty)
            return 0;
        var segment = segments[index];
        if (segment.Width <= 0)
            return index == paint.SelectedIndex ? 1 : 0;
        var overlap = Math.Min(segment.Right, paint.Selection.Right) - Math.Max(segment.Left, paint.Selection.Left);
        return Math.Clamp(overlap / segment.Width, 0, 1);
    }

    /// <summary>
    /// The color of segment <paramref name="index"/>'s text and icon: the text color blended towards the selected one by
    /// <see cref="GetSegmentSelection"/>, dimmed while the segment or the control is disabled.
    /// </summary>
    public virtual SKColor GetSegmentTextColor(in SkUiSegmentedPaint paint, int index)
    {
        var color = SkUiIndicatorLayout.Blend(paint.Text, paint.SelectedText, GetSegmentSelection(paint, index));
        return paint.IsSegmentEnabled(index) ? color : color.WithAlpha((byte)(color.Alpha * SegmentDimmedOpacity));
    }

    /// <summary>
    /// The room the look takes before segment <paramref name="index"/>'s content for a check, in DIPs (default 0); the control
    /// centers the check and the content together and starts the content after it.
    /// </summary>
    public virtual float GetSegmentLeadingWidth(in SkUiSegmentedPaint paint, int index) => 0;

    #endregion

    #region Tab strip

    /// <summary>
    /// A tab view's strip sizes (default: 44 high with text headers, 56 with icons and text, 16 DIPs of padding beside each
    /// header's content, a 2 DIP underline, 20 DIP icons 6 DIPs before the text).
    /// </summary>
    public virtual SkUiTabStripMetrics TabStripMetrics => new(44, 56, 16, 2, 20, 6, false);

    /// <summary>
    /// The colors a tab view's strip uses where the tab view sets none, from <paramref name="scheme"/> (default: no
    /// background, the foreground for headers, the accent for the selected header and the indicator, the muted color for the
    /// divider).
    /// </summary>
    public virtual SkUiTabStripColors GetTabStripColors(SkUiColorScheme scheme)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        return new SkUiTabStripColors(Colors.Transparent, scheme.DefaultForeground, scheme.Accent, scheme.Accent, scheme.Muted);
    }

    /// <summary>Optional tab strip painter; when set, replaces <see cref="DrawTabStripCore"/>.</summary>
    public Action<SKCanvas, SkUiTabStripPaint>? TabStripPainter { get; set; }

    /// <summary>Draws a tab view's strip behind its headers (delegate or <see cref="DrawTabStripCore"/>): its background and divider.</summary>
    public void DrawTabStrip(SKCanvas canvas, SkUiTabStripPaint paint)
    {
        if (TabStripPainter is { } painter)
        {
            painter(canvas, paint);
            return;
        }
        DrawTabStripCore(canvas, paint);
    }

    /// <summary>Default strip: its background, and a hairline along the edge facing the panes.</summary>
    protected virtual void DrawTabStripCore(SKCanvas canvas, SkUiTabStripPaint paint)
    {
        var bounds = paint.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;
        using var scratch = SkUiScratchPaint.Rent();
        var fill = scratch.Paint;
        fill.IsAntialias = true;
        if (paint.Background.Alpha > 0)
        {
            fill.Color = paint.Background;
            canvas.DrawRect(bounds, fill);
        }
        if (paint.Divider.Alpha > 0)
        {
            fill.Color = paint.Divider;
            canvas.DrawRect(paint.IsBottom ? new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Top + 1)
                : new SKRect(bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom), fill);
        }
    }

    /// <summary>
    /// Where the indicator of the tab whose header is <paramref name="header"/> sits, in the strip's coordinates, for a label
    /// <paramref name="labelWidth"/> wide (default: an underline as wide as the header along the edge facing the panes, or a
    /// pill behind the header 4 DIPs inside it; empty for <see cref="SkUiTabIndicatorStyle.None"/>).
    /// </summary>
    public virtual Rect GetTabIndicatorBounds(Rect header, double labelWidth, SkUiTabIndicatorStyle style, bool stripAtBottom)
    {
        switch (style)
        {
            case SkUiTabIndicatorStyle.None:
                return Rect.Zero;
            case SkUiTabIndicatorStyle.Pill:
                var inset = Math.Min(4, Math.Min(header.Width, header.Height) / 4);
                return header.Inflate(-inset, -inset);
            default:
                var thickness = TabStripMetrics.IndicatorThickness;
                return new Rect(header.X, stripAtBottom ? header.Top : header.Bottom - thickness, header.Width, thickness);
        }
    }

    /// <summary>Optional tab indicator painter; when set, replaces <see cref="DrawTabIndicatorCore"/>.</summary>
    public Action<SKCanvas, SkUiTabIndicatorPaint>? TabIndicatorPainter { get; set; }

    /// <summary>Draws a tab view's indicator in its own bounds (delegate or <see cref="DrawTabIndicatorCore"/>).</summary>
    public void DrawTabIndicator(SKCanvas canvas, SkUiTabIndicatorPaint paint)
    {
        if (paint.Style == SkUiTabIndicatorStyle.None)
            return;
        if (TabIndicatorPainter is { } painter)
        {
            painter(canvas, paint);
            return;
        }
        DrawTabIndicatorCore(canvas, paint);
    }

    /// <summary>Default indicator: a square-ended bar, or a pill with fully rounded ends.</summary>
    protected virtual void DrawTabIndicatorCore(SKCanvas canvas, SkUiTabIndicatorPaint paint)
    {
        var bounds = paint.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;
        using var scratch = SkUiScratchPaint.Rent();
        var fill = scratch.Paint;
        fill.IsAntialias = true;
        fill.Color = paint.Color;
        if (paint.Style == SkUiTabIndicatorStyle.Pill)
        {
            var radius = Math.Min(bounds.Width, bounds.Height) / 2;
            canvas.DrawRoundRect(bounds, radius, radius, fill);
        }
        else
        {
            canvas.DrawRect(bounds, fill);
        }
    }

    /// <summary>
    /// A header's text color: <paramref name="text"/> blended towards <paramref name="selected"/> by how selected it is
    /// (<paramref name="selection"/>, 0–1, fractional while the panes move), dimmed when disabled.
    /// </summary>
    public virtual SKColor GetTabHeaderTextColor(SKColor text, SKColor selected, float selection, bool enabled)
    {
        var color = SkUiIndicatorLayout.Blend(text, selected, Math.Clamp(selection, 0, 1));
        return enabled ? color : color.WithAlpha((byte)(color.Alpha * SegmentDimmedOpacity));
    }

    #endregion
}
