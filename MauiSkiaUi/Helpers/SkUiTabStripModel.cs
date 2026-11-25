using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>What the shared tab strip (<see cref="SkUiTabStripModel"/>) reads from its tab view, on either layer, live.</summary>
internal interface ISkUiTabStripOwner
{
    int TabCount { get; }
    string? HeaderOf(int index);
    ImageSource? IconOf(int index);
    string? BadgeOf(int index);
    string? AutomationIdOf(int index);

    /// <summary>What screen readers call tab <paramref name="index"/> without a header text.</summary>
    string? DescriptionOf(int index);

    /// <summary>The tab's own <c>IsEnabled</c> (the tab view's applies on top).</summary>
    bool IsTabItemEnabled(int index);

    int SelectedIndex { get; }
    bool IsEnabled { get; }
    bool IsRightToLeft { get; }
    SkUiTabStripPlacement TabStripPlacement { get; }
    SkUiSegmentWidthMode TabWidthMode { get; }
    SkUiTabIndicatorStyle IndicatorStyle { get; }
    Color? HeaderTextColor { get; }
    Color? SelectedHeaderTextColor { get; }
    Color? IndicatorColor { get; }
    Color? StripBackgroundColor { get; }
    string? FontFamily { get; }
    double FontSize { get; }
    FontAttributes FontAttributes { get; }
    bool FontAutoScalingEnabled { get; }

    /// <summary>The user chose tab <paramref name="index"/> (a tap, a key, a screen reader); returns whether the selection changed.</summary>
    bool SelectByUser(int index);
}

/// <summary>
/// The strip of a tab view, shared by <see cref="SkUiTabView"/> and <see cref="Core.SkUiCoreTabView"/>: the headers' texts, icons
/// and badges laid out by <see cref="SkUiSegmentedEngine"/> and drawn by the look, taps, keys and screen readers, which tab is
/// next, and the indicator: its bounds for each tab, its drawing, and the scroll-linked channels that move it with the panes
/// (D8). The layers own the nodes: the strip, the indicator over its background, and the labels over the indicator.
/// </summary>
/// <param name="owner">The tab view.</param>
/// <param name="host">The strip's node (text layouts and icons).</param>
/// <param name="iconChanged">An icon was loaded: the labels are drawn again.</param>
internal sealed class SkUiTabStripModel(ISkUiTabStripOwner owner, ISkUiTransitionHost host, Action iconChanged)
{
    private sealed class Header
    {
        public SkUiTextLayout? Layout;
        public SkUiTextLayout? BadgeLayout;
        public SkUiIconSlot? Icon;

        public bool HasIcon => Icon?.Source is not null;
        public Size TextSize = new(-1, -1); // negative: not measured yet
    }

    private readonly SkUiSegmentedEngine _engine = new();
    private readonly List<Header> _headers = [];
    private double[] _widths = [];
    private int[] _partIds = [];
    private bool _prepared;
    private int _geometryVersion;
    private (SkUiLook Look, SkUiColorScheme Scheme, SkUiTabStripColors Colors)? _colors;
    private SkUiRenderLink[]? _links;
    private (SkUiRenderNode? Source, int Strip, int Panes) _linked;

    private static SkUiTabStripMetrics Metrics => SkUiLook.Current.TabStripMetrics;

    /// <summary>Makes the tree's look current outside measuring and recording (arranging, links), where nothing else does.</summary>
    private SkUiLookScope EnterLook() => SkUiLookScope.Enter((ISkUiRenderable)host);

    #region Tabs

    /// <summary>Whether the user can select tab <paramref name="index"/>.</summary>
    public bool IsTabEnabled(int index) => owner.IsEnabled && index >= 0 && index < owner.TabCount && owner.IsTabItemEnabled(index);

    /// <summary>The next enabled tab from <paramref name="from"/> in <paramref name="direction"/>, around the ends when <paramref name="wrap"/>; -1 when none.</summary>
    public int NextEnabled(int from, int direction, bool wrap)
    {
        var count = owner.TabCount;
        for (var step = 1; step <= count; step++)
        {
            var index = from + direction * step;
            if (wrap)
                index = (index % count + count) % count;
            else if (index < 0 || index >= count)
                return -1;
            if (IsTabEnabled(index))
                return index;
        }
        return -1;
    }

    /// <summary>The first enabled tab from <paramref name="from"/> in <paramref name="direction"/> (including it), or -1.</summary>
    private int FirstEnabled(int from, int direction)
    {
        for (var index = from; index >= 0 && index < owner.TabCount; index += direction)
            if (IsTabEnabled(index))
                return index;
        return -1;
    }

    /// <summary>The tab a page towards larger pane offsets (<paramref name="forward"/>) or smaller ones goes to, or -1.</summary>
    public int PageTarget(bool forward) => NextEnabled(owner.SelectedIndex, forward != owner.IsRightToLeft ? 1 : -1, wrap: false);

    /// <summary>
    /// The panes' snap offsets between <paramref name="from"/> and <paramref name="to"/>: disabled tabs are none, so a swipe
    /// passes them (the selected one stays one: code can select it).
    /// </summary>
    public void AddSnapOffsets(SkUiCarouselEngine panes, double from, double to, List<double> points)
    {
        if (!panes.HasGeometry)
            return;
        for (var index = 0; index < panes.Count; index++)
        {
            if (!IsTabEnabled(index) && index != owner.SelectedIndex)
                continue;
            var offset = PaneOffset(panes, index);
            if (offset >= from - 0.5 && offset <= to + 0.5)
                points.Add(offset);
        }
        points.Sort();
    }

    /// <summary>The panes' physical scroll offset with tab <paramref name="index"/>'s pane in place.</summary>
    private static double PaneOffset(SkUiCarouselEngine panes, int index) => panes.Physical(panes.ClampedSnapOffset(index));

    /// <summary>The tabs, their headers, the font, the look or the widths changed: the headers are measured again.</summary>
    public void Invalidate()
    {
        var count = owner.TabCount;
        while (_headers.Count > count)
        {
            var header = _headers[^1];
            header.Layout?.Release();
            header.BadgeLayout?.Release();
            header.Icon?.Dispose();
            _headers.RemoveAt(_headers.Count - 1);
        }
        while (_headers.Count < count)
            _headers.Add(new Header());
        for (var index = 0; index < count; index++)
        {
            var header = _headers[index];
            header.TextSize = new Size(-1, -1);
            header.Layout?.Invalidate();
            header.BadgeLayout?.Invalidate();
            var icon = owner.IconOf(index);
            if (icon is not null || header.Icon is not null)
                (header.Icon ??= new SkUiIconSlot(host, iconChanged)).Show(icon);
        }
        _prepared = false;
        _colors = null;
    }

    /// <summary>The strip left its surface: the texts' blobs go (the icons stay loaded).</summary>
    public void ReleaseDrawingResources()
    {
        foreach (var header in _headers)
        {
            header.Layout?.Release();
            header.BadgeLayout?.Release();
        }
    }

    #endregion

    #region Layout

    private const double HeaderInset = 4; // around a header's content that is taller than the look's strip

    /// <summary>The headers' widths by their content (in equal widths, each as wide as the widest): what the strip asks for unbounded.</summary>
    public double DesiredWidth
    {
        get
        {
            using var lookScope = EnterLook();
            Prepare();
            return _engine.DesiredWidth();
        }
    }

    /// <summary>The strip's height: the look's, taller with icons.</summary>
    public double DesiredHeight
    {
        get
        {
            using var lookScope = EnterLook();
            var metrics = Metrics;
            var style = TextStyle;
            var (tallest, icons) = (0d, false);
            for (var index = 0; index < _headers.Count; index++)
            {
                tallest = Math.Max(tallest, TextSize(index, style).Height);
                icons |= _headers[index].HasIcon;
            }
            // Larger text (a font size, the system's text size) makes the strip taller than the look's height.
            var content = !icons ? tallest
                : metrics.IconAbove ? metrics.IconSize + (tallest > 0 ? metrics.IconSpacing + tallest : 0) : Math.Max(tallest, metrics.IconSize);
            return Math.Max(icons ? metrics.IconHeight : metrics.Height, Math.Ceiling(content + 2 * HeaderInset));
        }
    }

    /// <summary>Header <paramref name="index"/>'s rectangle in the strip (physical), after <see cref="Arrange"/>.</summary>
    public Rect HeaderBounds(int index) => index >= 0 && index < _engine.Count ? _engine.Segment(index) : Rect.Zero;

    private SkUiTextStyle TextStyle => new(SkUiTypefaces.Resolve(owner.FontFamily, owner.FontAttributes),
        SkUiFontScaling.ScaleFontSize(owner.FontSize, owner.FontAutoScalingEnabled), LineBreakMode.TailTruncation, null, 1, -1, 0,
        owner.IsRightToLeft ? SkUiTextDirection.RightToLeft : SkUiTextDirection.Auto, SkUiTextRendering.Default, owner.FontAttributes);

    private Size TextSize(int index, in SkUiTextStyle style)
    {
        var header = _headers[index];
        if (header.TextSize.Width >= 0)
            return header.TextSize;
        var text = owner.HeaderOf(index) ?? string.Empty;
        if (text.Length == 0)
            return header.TextSize = Size.Zero;
        var size = (header.Layout ??= new SkUiTextLayout(host)).Measure(text, style, default, double.PositiveInfinity);
        return header.TextSize = new Size(Math.Ceiling(size.Width), Math.Ceiling(size.Height));
    }

    /// <summary>A header's content width without padding: its text and icon, side by side or stacked.</summary>
    private double ContentWidth(int index, in SkUiTextStyle style, in SkUiTabStripMetrics metrics)
    {
        var text = TextSize(index, style).Width;
        if (!_headers[index].HasIcon)
            return text;
        return metrics.IconAbove ? Math.Max(text, metrics.IconSize) : text + metrics.IconSize + (text > 0 ? metrics.IconSpacing : 0);
    }

    private void Prepare()
    {
        var style = TextStyle;
        var metrics = Metrics;
        var count = _headers.Count;
        if (_widths.Length < count)
            _widths = new double[count];
        for (var index = 0; index < count; index++)
            _widths[index] = ContentWidth(index, style, metrics) + 2 * metrics.HeaderPadding;
        _engine.Mode = owner.TabWidthMode;
        _engine.Overflows = owner.TabWidthMode == SkUiSegmentWidthMode.Content;
        _engine.Mirrored = owner.IsRightToLeft;
        _engine.SetContentWidths(_widths.AsSpan(0, count));
        _prepared = true;
    }

    /// <summary>The strip's length in a tab view <paramref name="viewport"/> wide: that width, or the headers' when they overflow it.</summary>
    public double Measure(double viewport)
    {
        Prepare();
        if (!double.IsFinite(viewport))
            return _engine.DesiredWidth();
        _engine.Arrange(viewport, DesiredHeight);
        return _engine.Length;
    }

    /// <summary>Lays the headers out in a strip arranged <paramref name="width"/> × <paramref name="height"/> in a tab view <paramref name="viewport"/> wide.</summary>
    public void Arrange(double viewport, double width, double height)
    {
        using var lookScope = EnterLook();
        if (!_prepared || _engine.Mirrored != owner.IsRightToLeft)
            Prepare();
        _engine.Arrange(double.IsFinite(viewport) ? Math.Min(viewport, width) : width, height);
        _geometryVersion++;
    }

    /// <summary>Where the strip scrolls so header <paramref name="index"/> shows, from <paramref name="scrollX"/>; <c>null</c>: nowhere.</summary>
    public double? KeepInView(int index, double scrollX, double viewport)
    {
        if (index < 0 || index >= _engine.Count || !double.IsFinite(viewport) || _engine.Length <= viewport + 0.5)
            return null;
        var header = _engine.Segment(index);
        var target = header.Left < scrollX ? header.Left : header.Right > scrollX + viewport ? header.Right - viewport : scrollX;
        return Math.Abs(target - scrollX) > 0.5 ? target : null;
    }

    #endregion

    #region Indicator

    private bool HasIndicator => owner.TabCount > 0 && owner.IndicatorStyle != SkUiTabIndicatorStyle.None;

    /// <summary>The indicator's rectangle for tab <paramref name="index"/> in the strip.</summary>
    private Rect IndicatorBounds(int index, in SkUiTextStyle style)
    {
        var text = TextSize(index, style).Width;
        var label = text > 0 ? text : !_headers[index].HasIcon ? 0 : Metrics.IconSize;
        return SkUiLook.Current.GetTabIndicatorBounds(HeaderBounds(index), label, owner.IndicatorStyle,
            owner.TabStripPlacement == SkUiTabStripPlacement.Bottom);
    }

    /// <summary>
    /// Where the indicator's node is arranged in a strip <paramref name="stripWidth"/> wide: as wide as the widest tab's
    /// indicator, at the strip's physical start, where the links' positions count from. Logical: both layers mirror frames right
    /// to left.
    /// </summary>
    public Rect IndicatorFrame(double stripWidth)
    {
        if (!HasIndicator)
            return Rect.Zero;
        using var lookScope = EnterLook();
        var style = TextStyle;
        var frame = IndicatorBounds(0, style);
        var width = frame.Width;
        for (var index = 1; index < _headers.Count; index++)
            width = Math.Max(width, IndicatorBounds(index, style).Width);
        return new Rect(owner.IsRightToLeft ? stripWidth - width : 0, frame.Y, width, frame.Height);
    }

    public void DrawIndicator(SKCanvas canvas, float width, float height)
    {
        if (!HasIndicator || width <= 0 || height <= 0)
            return;
        var look = SkUiLook.Current;
        // A pill sits behind the selected header's text: by default a light tint of the look's color, so the text shows.
        var color = owner.IndicatorColor
            ?? (owner.IndicatorStyle == SkUiTabIndicatorStyle.Pill ? LookColors(look).Indicator.WithAlpha(0.16f) : LookColors(look).Indicator);
        look.DrawTabIndicator(canvas, new SkUiTabIndicatorPaint { Bounds = new SKRect(0, 0, width, height), Color = ToSk(color), Style = owner.IndicatorStyle });
    }

    /// <summary>
    /// Places the indicator's node by the channels that move it with the panes: each tab's pane offset mapped onto its
    /// indicator's position (and width, when they differ), piecewise linear between them. Built again only when the headers,
    /// the panes or the source changed.
    /// </summary>
    public void ApplyLinks(ref SkUiRenderProps props, SkUiRenderNode source, SkUiScrollController controller, SkUiCarouselEngine panes, int panesVersion)
    {
        if (!HasIndicator || !panes.HasGeometry || panes.Count != _headers.Count || _engine.Count != _headers.Count)
            return;
        using var lookScope = EnterLook();
        var key = (source, _geometryVersion, panesVersion);
        if (_links is not { } links || _linked != key)
        {
            links = _links = BuildLinks(source, panes);
            _linked = key;
        }
        props.Links = links;
        var input = controller.LinkInput;
        props.LinkValues = SkUiRenderLinkValues.Evaluate(links, input.X, input.Y);
    }

    /// <summary>The indicator's node was reset: it links to the source's new render node.</summary>
    public void ForgetLinks() => _links = null;

    private SkUiRenderLink[] BuildLinks(SkUiRenderNode source, SkUiCarouselEngine panes)
    {
        var count = _headers.Count;
        var style = TextStyle;
        var inputs = new float[count];
        var positions = new float[count];
        var widths = new float[count];
        var sameWidth = true;
        for (var index = 0; index < count; index++)
        {
            var bounds = IndicatorBounds(index, style);
            inputs[index] = (float)PaneOffset(panes, index);
            positions[index] = (float)bounds.X;
            widths[index] = (float)bounds.Width;
            sameWidth &= Math.Abs(widths[index] - widths[0]) < 0.01f;
        }
        if (count > 1 && inputs[0] > inputs[^1])
        {
            // Right to left: the first pane is at the largest offset.
            Array.Reverse(inputs);
            Array.Reverse(positions);
            Array.Reverse(widths);
        }
        var move = new SkUiRenderLink(source, horizontal: true, SkUiRenderLinkTarget.TranslationX, inputs, positions);
        if (sameWidth)
            return [move];
        var rounded = owner.IndicatorStyle == SkUiTabIndicatorStyle.Pill
            || SkUiLook.Current is DefaultSkUiLook { TabStripStyle: SkUiTabStripStyle.LabelUnderline or SkUiTabStripStyle.ShortBar };
        return [move, new SkUiRenderLink(source, horizontal: true, SkUiRenderLinkTarget.ClipWidth, inputs, widths, SkUiRenderLinkClipEdge.Start,
            rounded ? SkUiRenderLinkClipShape.Capsule : SkUiRenderLinkClipShape.Rectangle)];
    }

    #endregion

    #region Drawing

    private SkUiTabStripColors LookColors(SkUiLook look)
    {
        var scheme = SkUiColorScheme.Current;
        if (_colors is { } cached && ReferenceEquals(cached.Look, look) && ReferenceEquals(cached.Scheme, scheme))
            return cached.Colors;
        var colors = look.GetTabStripColors(scheme);
        _colors = (look, scheme, colors);
        return colors;
    }

    /// <summary>The strip's background and divider (the headers are the labels layer's, over the indicator).</summary>
    public void DrawBackground(SKCanvas canvas, float width, float height)
    {
        var look = SkUiLook.Current;
        var colors = LookColors(look);
        look.DrawTabStrip(canvas, new SkUiTabStripPaint
        {
            Bounds = new SKRect(0, 0, width, height),
            Background = ToSk(owner.StripBackgroundColor ?? colors.Background),
            Divider = ToSk(colors.Divider),
            IsBottom = owner.TabStripPlacement == SkUiTabStripPlacement.Bottom,
            IsEnabled = owner.IsEnabled
        });
    }

    /// <summary>Each header's icon and text, in the look's color for whether it is selected, and its badge.</summary>
    public void DrawHeaders(SKCanvas canvas)
    {
        var look = SkUiLook.Current;
        var colors = LookColors(look);
        var metrics = Metrics;
        var style = TextStyle;
        var text = ToSk(owner.HeaderTextColor ?? colors.Text);
        var selectedText = ToSk(owner.SelectedHeaderTextColor ?? colors.SelectedText);
        var selected = owner.SelectedIndex;
        for (var index = 0; index < _headers.Count && index < _engine.Count; index++)
        {
            var bounds = _engine.Segment(index);
            var rect = new SKRect((float)bounds.Left, (float)bounds.Top, (float)bounds.Right, (float)bounds.Bottom);
            DrawHeader(canvas, index, rect, look.GetTabHeaderTextColor(text, selectedText, index == selected ? 1 : 0, IsTabEnabled(index)), style, metrics);
            if (owner.BadgeOf(index) is { } badge)
                DrawBadge(canvas, _headers[index], rect, badge);
        }
    }

    private void DrawHeader(SKCanvas canvas, int index, SKRect rect, SKColor color, in SkUiTextStyle style, in SkUiTabStripMetrics metrics)
    {
        var header = _headers[index];
        var textSize = TextSize(index, style);
        var hasIcon = header.HasIcon;
        var icon = (float)metrics.IconSize;
        var room = Math.Max(0, rect.Width - 2 * (float)metrics.HeaderPadding);
        if (hasIcon && metrics.IconAbove)
        {
            var gap = (float)metrics.IconSpacing;
            var total = icon + (textSize.Height > 0 ? gap + (float)textSize.Height : 0);
            var top = rect.MidY - total / 2;
            DrawIcon(canvas, header, new SKRect(rect.MidX - icon / 2, top, rect.MidX + icon / 2, top + icon), color);
            var stacked = Math.Min((float)textSize.Width, room);
            DrawText(canvas, index, style, color, rect.MidX - stacked / 2, top + icon + gap, stacked, (float)textSize.Height);
            return;
        }
        var rtl = owner.IsRightToLeft;
        var iconPart = hasIcon ? icon + (textSize.Width > 0 ? (float)metrics.IconSpacing : 0) : 0;
        var textWidth = Math.Min((float)textSize.Width, Math.Max(0, room - iconPart));
        var content = iconPart + textWidth;
        var start = rtl ? rect.MidX + content / 2 : rect.MidX - content / 2;
        if (hasIcon)
        {
            var left = rtl ? start - icon : start;
            DrawIcon(canvas, header, new SKRect(left, rect.MidY - icon / 2, left + icon, rect.MidY + icon / 2), color);
        }
        DrawText(canvas, index, style, color, rtl ? start - iconPart - textWidth : start + iconPart, rect.Top, textWidth, rect.Height);
    }

    private void DrawText(SKCanvas canvas, int index, in SkUiTextStyle style, SKColor color, float left, float top, float width, float height)
    {
        if (_headers[index].Layout is not { } layout || width <= 0)
            return;
        var paint = SkUiTextResources.TextPaint;
        paint.Color = color;
        var save = canvas.Save();
        canvas.Translate(left, top);
        layout.Draw(canvas, owner.HeaderOf(index) ?? string.Empty, style, default, width, height, TextAlignment.Center, TextAlignment.Center, paint);
        canvas.RestoreToCount(save);
    }

    private static void DrawIcon(SKCanvas canvas, Header header, SKRect area, SKColor color)
    {
        var layer = color.Alpha < 255 ? canvas.SaveLayer(new SKRect(area.Left - 1, area.Top - 1, area.Right + 1, area.Bottom + 1), LayerPaint(color.Alpha)) : -1;
        header.Icon!.Slot?.Paint(canvas, area, Aspect.AspectFit);
        if (layer >= 0)
            canvas.RestoreToCount(layer);
    }

    [ThreadStatic] private static SKPaint? _layerPaint;

    private static SKPaint LayerPaint(byte alpha)
    {
        var paint = _layerPaint ??= new SKPaint();
        paint.Color = SKColors.Black.WithAlpha(alpha);
        return paint;
    }

    /// <summary>A badge at the header's top end: a dot for an empty text, else the text in a small pill.</summary>
    private void DrawBadge(SKCanvas canvas, Header item, SKRect header, string text)
    {
        using var scratch = SkUiScratchPaint.Rent();
        var fill = scratch.Paint;
        fill.IsAntialias = true;
        fill.Color = ToSk(SkUiColors.Error);
        var rtl = owner.IsRightToLeft;
        var edge = rtl ? header.Left + 8 : header.Right - 8;
        if (text.Length == 0)
        {
            canvas.DrawCircle(edge, header.Top + 8, 4, fill);
            return;
        }
        var style = new SkUiTextStyle(SkUiTypefaces.Resolve(null, FontAttributes.Bold), SkUiFontScaling.ScaleFontSize(BadgeFontSize, owner.FontAutoScalingEnabled),
            LineBreakMode.NoWrap, null, 1, -1, 0, SkUiTextDirection.Auto, SkUiTextRendering.Default, FontAttributes.Bold);
        var layout = item.BadgeLayout ??= new SkUiTextLayout(host);
        var size = layout.Measure(text, style, default, double.PositiveInfinity);
        var height = Math.Max(16, (float)Math.Ceiling(size.Height) + 2);
        var width = Math.Max(height, (float)Math.Ceiling(size.Width) + 8);
        var pill = rtl ? new SKRect(edge, header.Top + 2, edge + width, header.Top + 2 + height) : new SKRect(edge - width, header.Top + 2, edge, header.Top + 2 + height);
        canvas.DrawRoundRect(pill, height / 2, height / 2, fill);
        var textPaint = SkUiTextResources.TextPaint;
        textPaint.Color = SKColors.White;
        var save = canvas.Save();
        canvas.Translate(pill.Left, pill.Top);
        layout.Draw(canvas, text, style, default, width, height, TextAlignment.Center, TextAlignment.Center, textPaint);
        canvas.RestoreToCount(save);
    }

    private const double BadgeFontSize = 11;

    private static SKColor ToSk(Color color) => SkUiToggleDrawing.ToSkColor(color);

    #endregion

    #region Input, keys and screen readers

    public bool TakesInput => owner.IsEnabled && _engine.Count > 0;

    /// <summary>A tap on the strip: the header there is selected.</summary>
    public void Tap(Point position) => owner.SelectByUser(_engine.HitTest(position.X));

    /// <summary>Left / Right the previous / next enabled tab (mirrored right to left), Home / End the first / last; no wrap.</summary>
    public bool OnKeyDown(in SkUiKeyEvent key)
    {
        if (!TakesInput || key.HasCommandModifier)
            return false;
        if (key.Key is SkUiKey.Enter or SkUiKey.Space)
            return true; // the selected tab is the focused one: activating it changes nothing (and is no tap in the middle)
        var rtl = owner.IsRightToLeft;
        var target = key.Key switch
        {
            SkUiKey.Right => NextEnabled(owner.SelectedIndex, rtl ? -1 : 1, wrap: false),
            SkUiKey.Left => NextEnabled(owner.SelectedIndex, rtl ? 1 : -1, wrap: false),
            SkUiKey.Home => FirstEnabled(0, 1),
            SkUiKey.End => FirstEnabled(owner.TabCount - 1, -1),
            _ => -2
        };
        if (target == -2)
            return false;
        if (target >= 0)
            owner.SelectByUser(target);
        return true;
    }

    /// <summary>Ctrl+Tab / Ctrl+Shift+Tab, from anywhere inside the tab view: the next / previous enabled tab, around the ends.</summary>
    public bool OnTabViewKeyDown(in SkUiKeyEvent key)
    {
        if (key.Key != SkUiKey.Tab || (key.Modifiers & SkUiKeyModifiers.Control) == 0 || !owner.IsEnabled || owner.TabCount == 0)
            return false;
        var target = NextEnabled(owner.SelectedIndex, (key.Modifiers & SkUiKeyModifiers.Shift) != 0 ? -1 : 1, wrap: true);
        if (target >= 0)
            owner.SelectByUser(target);
        return true;
    }

    private int PartId(int index)
    {
        if (_partIds.Length <= index)
            Array.Resize(ref _partIds, Math.Max(index + 1, _partIds.Length * 2));
        ref var id = ref _partIds[index];
        if (id == 0)
            id = SkUiSemanticsTree.NewId();
        return id;
    }

    /// <summary>The strip is a tab list: each header an element ("Reviews, 3", "Selected, 2 of 3"), activated to select it.</summary>
    public void PopulateSemantics(SkUiSemanticsInfo info)
    {
        var count = owner.TabCount;
        if (count == 0)
            return;
        info.IsEnabled &= owner.IsEnabled;
        info.IsHorizontal = true;
        info.Actions &= ~SkUiSemanticsActions.Activate; // the headers are activated, not the strip
        var selected = owner.SelectedIndex;
        info.Value = selected >= 0 && selected < count ? HeaderLabel(selected) : null;
        if (owner.IsEnabled)
            info.Actions |= SkUiSemanticsActions.Increment | SkUiSemanticsActions.Decrement;
        var strings = SkUiStrings.Current;
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        for (var index = 0; index < count && index < _engine.Count; index++)
        {
            var header = HeaderLabel(index);
            var label = owner.BadgeOf(index) is { Length: > 0 } badge ? string.Concat(header, ", ", badge) : header;
            var place = string.Format(culture, strings.ItemPositionFormat, index + 1, count);
            var id = owner.AutomationIdOf(index) is { Length: > 0 } own ? own : index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            info.AddPart(new SkUiSemanticsPart(PartId(index), index, _engine.Segment(index), label, "_" + id, owner.IsTabItemEnabled(index),
                index == selected ? string.Concat(strings.SelectedStateText, ", ", place) : place));
        }
    }

    private string HeaderLabel(int index) => owner.HeaderOf(index) is { Length: > 0 } header ? header : owner.DescriptionOf(index) ?? string.Empty;

    /// <summary>A screen reader's increment / decrement on the strip: the next / previous enabled tab.</summary>
    public bool PerformSemanticsAction(SkUiSemanticsActions action)
    {
        if (action == SkUiSemanticsActions.Activate)
            return true; // not a tap in the middle
        var target = action switch
        {
            SkUiSemanticsActions.Increment => NextEnabled(owner.SelectedIndex, 1, wrap: false),
            SkUiSemanticsActions.Decrement => NextEnabled(owner.SelectedIndex, -1, wrap: false),
            _ => -1
        };
        return target >= 0 && owner.SelectByUser(target);
    }

    /// <summary>A screen reader activated header <paramref name="part"/>.</summary>
    public bool PerformSemanticsPartAction(int part, SkUiSemanticsActions action) =>
        action == SkUiSemanticsActions.Activate && IsTabEnabled(part) && (owner.SelectByUser(part) || part == owner.SelectedIndex);

    #endregion
}
