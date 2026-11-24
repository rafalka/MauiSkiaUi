using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi;

public partial class SkUiTabView
{
    /// <summary>The panes' scroller: once laid out, it moves to where the panes' layout wants it (the selected pane).</summary>
    private sealed class PaneScroller(SkUiTabView owner) : SkUiScrollView
    {
        protected override void ArrangeContent(Size size)
        {
            base.ArrangeContent(size);
            owner.Panes.ApplyPendingOffset();
        }
    }

    /// <summary>Hosts one tab's pane: a semantics container named by the tab's header.</summary>
    private sealed class PaneCell(SkUiTabItem tab) : SkUiContentView
    {
        public SkUiTabItem Tab { get; } = tab;
    }

    /// <summary>
    /// The strip of panes inside the pane scroller, on the carousel's layout engine (no looping, one pane per snap point). A
    /// pane is created when it first comes into view and kept after that, laid out at its place: panes out of view are only
    /// scrolled out, so their state, native views and semantics follow the scroll view's usual rules.
    /// </summary>
    private sealed class PaneHost : SkUiView, ISkUiSnapPointSource, ISkUiScrollListener, ISkUiCarouselEngineHost
    {
        private readonly SkUiTabView _owner;
        private readonly SkUiCarouselEngine _engine;
        private readonly Dictionary<SkUiTabItem, PaneCell> _cells = [];
        private readonly List<PaneCell> _children = [];
        private double _arrangedCross = double.NaN;
        private bool _arranged;
        private bool _updating;

        public PaneHost(SkUiTabView owner)
        {
            _owner = owner;
            _engine = new SkUiCarouselEngine(this);
        }

        bool ISkUiCarouselEngineHost.Loop => false;

        bool ISkUiCarouselEngineHost.Horizontal => true;

        bool ISkUiCarouselEngineHost.Mirrored => _owner.IsRightToLeft;

        /// <summary>Panes that fill an unbounded tab view are as wide as the first created one measures (else nothing).</summary>
        double ISkUiCarouselEngineHost.ProbeExtent(double cross) =>
            _children.Count > 0 ? ((IView)_children[0]).Measure(double.PositiveInfinity, cross).Width : 0;

        public bool HasGeometry => _engine.HasGeometry;

        public double ViewportLength => _engine.ViewportLength;

        /// <summary>A version bumped when the panes' geometry changes (the indicator rebuilds its links).</summary>
        public int GeometryVersion { get; private set; }

        private SkUiScrollController Controller => _owner.PaneController;

        private double LogicalOffset => _engine.LogicalOffset(Controller.X);

        public bool IsCreated(SkUiTabItem tab) => _cells.ContainsKey(tab);

        /// <summary>The physical scroll offset at which tab <paramref name="index"/>'s pane is in place.</summary>
        public double PhysicalOffsetOf(int index) => _engine.Physical(_engine.ClampedSnapOffset(index));

        public void SetViewport(double viewport)
        {
            if (_engine.SetViewport(viewport, LogicalOffset, _arranged))
                InvalidateMeasureOverride();
        }

        public void SetCount(int count)
        {
            _engine.SetCount(count, Math.Max(0, Math.Min(_owner._requested, count - 1)));
            InvalidateMeasureOverride();
        }

        /// <summary>The layout changes next (the direction): the current place is kept.</summary>
        public void KeepPlace()
        {
            _engine.KeepCurrentPlace(LogicalOffset, _arranged);
            InvalidateMeasureOverride();
        }

        public int CurrentIndex(int keep) => _engine.CurrentIndex(LogicalOffset, keep);

        public Task ScrollToIndex(int index, bool animated)
        {
            var logical = LogicalOffset;
            if (_engine.DeferPlacement(index, logical, _arranged))
            {
                InvalidateMeasureOverride();
                return Task.CompletedTask;
            }
            EnsurePane(index); // shown at once where the scroll ends
            var target = _engine.Physical(_engine.TargetOffset(index, logical));
            return Controller.ScrollToAsync(target, Controller.Y, animated);
        }

        public void ApplyPendingOffset()
        {
            if (_engine.TakePendingOffset() is not { } target)
                return;
            var delta = target - Controller.X;
            if (Math.Abs(delta) < 0.01)
            {
                Update();
                return;
            }
            if (Controller.IsMotionRunning || Controller.Dragging)
                Controller.CorrectOffset(delta, 0);
            else
                Controller.SetOffset(target, Controller.Y);
        }

        void ISkUiScrollListener.OnAncestorScrollChanged() => Update();

        /// <summary>The panes moved: panes coming into view for the first time are created (nothing else changes per frame).</summary>
        private void Update()
        {
            if (_updating || !_arranged)
                return;
            _updating = true;
            try
            {
                RealizeVisible(arrange: true);
            }
            finally
            {
                _updating = false;
            }
        }

        private void RealizeVisible(bool arrange)
        {
            if (!HasGeometry || _engine.Count == 0)
                return;
            var (first, last) = _engine.VisibleSlots(LogicalOffset);
            for (var index = first; index <= last; index++)
                if (EnsurePane(index) is { } cell && arrange)
                    ArrangeCell(cell, index);
        }

        /// <summary>Creates tab <paramref name="index"/>'s pane if it has none yet; returns it when it was just created.</summary>
        private PaneCell? EnsurePane(int index)
        {
            var tabs = _owner.Tabs;
            if (index < 0 || index >= tabs.Count)
                return null;
            var tab = tabs[index];
            if (_cells.ContainsKey(tab))
                return null;
            var cell = new PaneCell(tab);
            SemanticProperties.SetDescription(cell, tab.Header);
            cell.Content = MakeContent(tab);
            _cells[tab] = cell;
            _children.Add(cell);
            AddLogicalChild(cell);
            MeasureCell(cell);
            InvalidateRender(SkUiRenderDirty.Children);
            return cell;
        }

        private static ISkUiView? MakeContent(SkUiTabItem tab)
        {
            var view = tab.Content ?? tab.ContentTemplate?.CreateContent() as View;
            if (view is null)
                return null;
            return view as ISkUiView
                ?? throw new InvalidOperationException($"A {nameof(SkUiTabItem)}'s content must be a drawn (SkUi*) view; put native views inside an SkUiMauiContentView.");
        }

        /// <summary>The tab left: its pane goes.</summary>
        public void Forget(SkUiTabItem tab)
        {
            if (!_cells.Remove(tab, out var cell))
                return;
            _children.Remove(cell);
            cell.Content = null;
            RemoveLogicalChild(cell);
            InvalidateRender(SkUiRenderDirty.Children);
            InvalidateMeasureOverride();
        }

        /// <summary>The tab's content or template changed: a created pane shows the new one.</summary>
        public void ContentChanged(SkUiTabItem tab)
        {
            if (_cells.TryGetValue(tab, out var cell))
                cell.Content = MakeContent(tab);
        }

        public void HeaderChanged(SkUiTabItem tab)
        {
            if (_cells.TryGetValue(tab, out var cell))
                SemanticProperties.SetDescription(cell, tab.Header);
        }

        private void MeasureCell(PaneCell cell)
        {
            var cross = double.IsFinite(_arrangedCross) ? _arrangedCross : _engine.CrossConstraint;
            ((IView)cell).Measure(_engine.Extent, cross);
        }

        private void ArrangeCell(PaneCell cell, int index) =>
            // Logical: the layout mirrors frames in right-to-left layouts.
            ((IView)cell).Arrange(new Rect(_engine.SlotStart(index), 0, _engine.Extent, double.IsFinite(_arrangedCross) ? _arrangedCross : 0));

        void ISkUiSnapPointSource.GetSnapOffsets(bool horizontal, double from, double to, List<double> points)
        {
            if (!horizontal || !HasGeometry)
                return;
            // Disabled tabs are no snap points: a swipe passes them (the selected one stays one, set by code).
            var selected = _owner.SelectedIndex;
            for (var index = 0; index < _engine.Count; index++)
            {
                if (!_owner.IsTabEnabled(index) && index != selected)
                    continue;
                var offset = PhysicalOffsetOf(index);
                if (offset >= from - 0.5 && offset <= to + 0.5)
                    points.Add(offset);
            }
            points.Sort();
        }

        internal override IEnumerable<ISkUiView> SkiaChildren => _children;

        protected override Size MeasureContent(double widthConstraint, double heightConstraint)
        {
            if (_engine.NeedsLayout(heightConstraint))
            {
                var position = Math.Max(0, Math.Min(_owner._requested, _engine.Count - 1));
                if (_engine.UpdateGeometry(new SkUiCarouselLayout(default, 0, 0, SnapPointsAlignment.Start, null), heightConstraint, position))
                    GeometryVersion++;
                else
                    GeometryVersion++;
            }
            RealizeVisible(arrange: false);
            var across = 0d;
            foreach (var cell in _children)
            {
                MeasureCell(cell);
                across = Math.Max(across, ((IView)cell).DesiredSize.Height);
            }
            if (double.IsFinite(heightConstraint))
                across = Math.Min(across, heightConstraint);
            return new Size(_engine.Length, across);
        }

        protected override void ArrangeContent(Size size)
        {
            if (size.Height != _arrangedCross) // exact: an arranged size
            {
                _arrangedCross = size.Height;
                foreach (var cell in _children)
                    MeasureCell(cell);
            }
            _arranged = true;
            _updating = true;
            try
            {
                RealizeVisible(arrange: false);
                var tabs = _owner.Tabs;
                foreach (var cell in _children)
                    ArrangeCell(cell, tabs.IndexOf(cell.Tab));
            }
            finally
            {
                _updating = false;
            }
        }
    }

    /// <summary>The strip of headers, drawn by the look on the segmented engine; it hosts the indicator.</summary>
    private sealed class TabStrip : SkUiView, ISkUiKeyHandler
    {
        private sealed class Header
        {
            public SkUiTextLayout? Layout;
            public SkUiImageSlot? IconSlot;
            public ImageSource? Icon;
            public Size TextSize = new(-1, -1);
        }

        private readonly SkUiTabView _owner;
        private readonly SkUiSegmentedEngine _engine = new();
        private readonly List<Header> _headers = [];
        private double[] _widths = [];
        private int[] _partIds = [];
        private bool _prepared;
        private (SkUiLook Look, SkUiColorScheme Scheme, SkUiTabStripColors Colors)? _colors;

        public TabStrip(SkUiTabView owner)
        {
            _owner = owner;
            Indicator = new TabIndicator(owner, this);
            Labels = new TabLabels(this);
            AttachChild(Indicator);
            AttachChild(Labels);
        }

        public TabIndicator Indicator { get; }

        /// <summary>The headers' texts, icons and badges, drawn over the indicator (a pill sits behind the text).</summary>
        public TabLabels Labels { get; }

        /// <summary>The tab view's width (the strip fills it, or scrolls past it with content widths).</summary>
        public double ViewportWidth { get; set; } = double.NaN;

        /// <summary>A version bumped when the headers' geometry changes (the indicator rebuilds its links).</summary>
        public int GeometryVersion { get; private set; }

        private SkUiTabStripMetrics Metrics => SkUiLook.Current.TabStripMetrics;

        private bool HasIcons
        {
            get
            {
                foreach (var tab in _owner.Tabs)
                    if (tab.Icon is not null)
                        return true;
                return false;
            }
        }

        public double DesiredHeight => HasIcons ? Metrics.IconHeight : Metrics.Height;

        public Rect HeaderBounds(int index) => index >= 0 && index < _engine.Count ? _engine.Segment(index) : Rect.Zero;

        public void TabsChanged()
        {
            var tabs = _owner.Tabs;
            while (_headers.Count > tabs.Count)
            {
                var header = _headers[^1];
                header.Layout?.Release();
                header.IconSlot?.Dispose();
                _headers.RemoveAt(_headers.Count - 1);
            }
            while (_headers.Count < tabs.Count)
                _headers.Add(new Header());
            for (var index = 0; index < tabs.Count; index++)
            {
                var header = _headers[index];
                header.TextSize = new Size(-1, -1);
                header.Layout?.Invalidate();
                var icon = tabs[index].Icon;
                if (!ReferenceEquals(header.Icon, icon))
                {
                    header.Icon = icon;
                    if (icon is not null || header.IconSlot is not null)
                        (header.IconSlot ??= new SkUiImageSlot(this, InvalidatePaint)).Load(SkUiImageSource.FromMaui(icon), default);
                }
            }
            _prepared = false;
            _colors = null;
            InvalidateMeasureOverride();
            InvalidatePaint();
            Labels.InvalidatePaint();
            InvalidateSemantics();
            Indicator.GeometryChanged();
        }

        public void SelectionChanged(int index)
        {
            Labels.InvalidatePaint(); // the selected header's color: only the labels re-record
            InvalidateSemantics();
            KeepInView(index);
        }

        public void ColorsChanged()
        {
            InvalidatePaint();
            Labels.InvalidatePaint();
            Indicator.InvalidatePaint();
        }

        public SkUiTabStripColors LookColors(SkUiLook look)
        {
            var scheme = SkUiColorScheme.Current;
            if (_colors is { } cached && ReferenceEquals(cached.Look, look) && ReferenceEquals(cached.Scheme, scheme))
                return cached.Colors;
            var colors = look.GetTabStripColors(scheme);
            _colors = (look, scheme, colors);
            return colors;
        }

        private SkUiTextStyle TextStyle => new(SkUiTypefaces.Resolve(_owner.FontFamily, _owner.FontAttributes),
            SkUiFontScaling.ScaleFontSize(_owner.FontSize, _owner.FontAutoScalingEnabled), LineBreakMode.TailTruncation, null, 1, -1, 0,
            _owner.IsRightToLeft ? SkUiTextDirection.RightToLeft : SkUiTextDirection.Auto, SkUiTextRendering.Default, _owner.FontAttributes);

        private Size TextSize(int index, in SkUiTextStyle style)
        {
            var header = _headers[index];
            if (header.TextSize.Width >= 0)
                return header.TextSize;
            var text = _owner.Tabs[index].Header ?? string.Empty;
            if (text.Length == 0)
                return header.TextSize = Size.Zero;
            var size = (header.Layout ??= new SkUiTextLayout(this)).Measure(text, style, default, double.PositiveInfinity);
            return header.TextSize = new Size(Math.Ceiling(size.Width), Math.Ceiling(size.Height));
        }

        /// <summary>A header's content width without padding: its text and icon, side by side or stacked.</summary>
        private double ContentWidth(int index, in SkUiTextStyle style, in SkUiTabStripMetrics metrics)
        {
            var text = TextSize(index, style).Width;
            if (_headers[index].Icon is null)
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
            _engine.Mode = _owner.TabWidthMode;
            _engine.Overflows = _owner.TabWidthMode == SkUiSegmentWidthMode.Content;
            _engine.Inset = 0;
            _engine.Spacing = 0;
            _engine.Mirrored = _owner.IsRightToLeft;
            _engine.SetContentWidths(_widths.AsSpan(0, count));
            _prepared = true;
        }

        /// <summary>The strip's length for the tab view's width: that width, or the headers' when they overflow it.</summary>
        private double LengthFor(double viewport)
        {
            if (!_prepared)
                Prepare();
            if (!double.IsFinite(viewport))
                return _engine.DesiredWidth();
            _engine.Arrange(viewport, DesiredHeight);
            return _engine.Length;
        }

        protected override Size MeasureContent(double widthConstraint, double heightConstraint)
        {
            Prepare();
            var length = LengthFor(ViewportWidth);
            return new Size(length, Math.Min(heightConstraint, DesiredHeight));
        }

        protected override void ArrangeContent(Size size)
        {
            if (!_prepared || _engine.Mirrored != _owner.IsRightToLeft)
                Prepare();
            var viewport = double.IsFinite(ViewportWidth) ? Math.Min(ViewportWidth, size.Width) : size.Width;
            _engine.Arrange(viewport, size.Height);
            GeometryVersion++;
            var bounds = Indicator.MaxBounds(size.Height);
            ((IView)Indicator).Measure(bounds.Width, bounds.Height);
            ((IView)Indicator).Arrange(bounds);
            Indicator.GeometryChanged();
            ((IView)Labels).Measure(size.Width, size.Height);
            ((IView)Labels).Arrange(new Rect(0, 0, size.Width, size.Height));
            Labels.InvalidatePaint();
        }

        /// <summary>The label's width of header <paramref name="index"/> (its text, or its icon alone).</summary>
        public double LabelWidth(int index)
        {
            var text = TextSize(index, TextStyle).Width;
            return text > 0 ? text : _headers[index].Icon is null ? 0 : Metrics.IconSize;
        }

        internal override IEnumerable<ISkUiView> SkiaChildren
        {
            get
            {
                yield return Indicator;
                yield return Labels;
            }
        }

        /// <summary>The strip's background and divider (the headers are the labels layer's, over the indicator).</summary>
        protected override void OnPaintContent(SKCanvas canvas)
        {
            var look = SkUiLook.Current;
            var colors = LookColors(look);
            look.DrawTabStrip(canvas, new SkUiTabStripPaint
            {
                Bounds = new SKRect(0, 0, (float)Width, (float)Height),
                Background = ToSk(_owner.StripBackgroundColor ?? colors.Background),
                Divider = ToSk(colors.Divider),
                IsBottom = _owner.TabStripPlacement == SkUiTabStripPlacement.Bottom,
                IsEnabled = _owner.IsEnabled
            });
        }

        /// <summary>Each header's icon and text, in the look's color for whether it is selected, and its badge.</summary>
        public void DrawHeaders(SKCanvas canvas)
        {
            var look = SkUiLook.Current;
            var colors = LookColors(look);
            var metrics = Metrics;
            var style = TextStyle;
            var text = ToSk(_owner.HeaderTextColor ?? colors.Text);
            var selectedText = ToSk(_owner.SelectedHeaderTextColor ?? colors.SelectedText);
            var rtl = _owner.IsRightToLeft;
            var selected = _owner.SelectedIndex;
            for (var index = 0; index < _headers.Count && index < _engine.Count; index++)
            {
                var rect = ToSk(_engine.Segment(index));
                var color = look.GetTabHeaderTextColor(text, selectedText, index == selected ? 1 : 0, _owner.IsTabEnabled(index));
                DrawHeader(canvas, index, rect, color, style, metrics, rtl);
                if (_owner.Tabs[index].BadgeText is { } badge)
                    DrawBadge(canvas, rect, badge, rtl);
            }
        }

        private void DrawHeader(SKCanvas canvas, int index, SKRect rect, SKColor color, in SkUiTextStyle style, in SkUiTabStripMetrics metrics, bool rtl)
        {
            var header = _headers[index];
            var textSize = TextSize(index, style);
            var hasIcon = header.Icon is not null && header.IconSlot is not null;
            var icon = (float)metrics.IconSize;
            var room = Math.Max(0, rect.Width - 2 * (float)metrics.HeaderPadding);
            if (hasIcon && metrics.IconAbove)
            {
                var gap = (float)metrics.IconSpacing;
                var total = icon + (textSize.Height > 0 ? gap + (float)textSize.Height : 0);
                var top = rect.MidY - total / 2;
                DrawIcon(canvas, header, new SKRect(rect.MidX - icon / 2, top, rect.MidX + icon / 2, top + icon), color);
                if (header.Layout is { } stacked && textSize.Width > 0)
                    DrawText(canvas, stacked, index, style, color, rect.MidX - Math.Min((float)textSize.Width, room) / 2, top + icon + gap,
                        Math.Min((float)textSize.Width, room), (float)textSize.Height);
                return;
            }
            var iconPart = hasIcon ? icon + (textSize.Width > 0 ? (float)metrics.IconSpacing : 0) : 0;
            var textWidth = Math.Min((float)textSize.Width, Math.Max(0, room - iconPart));
            var content = iconPart + textWidth;
            var start = rtl ? rect.MidX + content / 2 : rect.MidX - content / 2;
            if (hasIcon)
            {
                var left = rtl ? start - icon : start;
                DrawIcon(canvas, header, new SKRect(left, rect.MidY - icon / 2, left + icon, rect.MidY + icon / 2), color);
            }
            if (header.Layout is { } layout && textWidth > 0)
                DrawText(canvas, layout, index, style, color, rtl ? start - iconPart - textWidth : start + iconPart, rect.Top, textWidth, rect.Height);
        }

        private void DrawText(SKCanvas canvas, SkUiTextLayout layout, int index, in SkUiTextStyle style, SKColor color, float left, float top, float width,
            float height)
        {
            var paint = SkUiTextResources.TextPaint;
            paint.Color = color;
            var save = canvas.Save();
            canvas.Translate(left, top);
            layout.Draw(canvas, _owner.Tabs[index].Header ?? string.Empty, style, default, width, height, TextAlignment.Center, TextAlignment.Center, paint);
            canvas.RestoreToCount(save);
        }

        private static void DrawIcon(SKCanvas canvas, Header header, SKRect area, SKColor color)
        {
            var layer = color.Alpha < 255 ? canvas.SaveLayer(new SKRect(area.Left - 1, area.Top - 1, area.Right + 1, area.Bottom + 1), LayerPaint(color.Alpha)) : -1;
            header.IconSlot!.Paint(canvas, area, Aspect.AspectFit);
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
        private static void DrawBadge(SKCanvas canvas, SKRect header, string text, bool rtl)
        {
            using var scratch = SkUiScratchPaint.Rent();
            var fill = scratch.Paint;
            fill.IsAntialias = true;
            fill.Color = ToSk(SkUiColors.Error);
            var edge = rtl ? header.Left + 8 : header.Right - 8;
            if (text.Length == 0)
            {
                canvas.DrawCircle(edge, header.Top + 8, 4, fill);
                return;
            }
            using var font = new SKFont(SkUiTypefaces.Resolve(null, FontAttributes.Bold), 11);
            var width = Math.Max(16, font.MeasureText(text) + 8);
            var pill = rtl ? new SKRect(edge, header.Top + 2, edge + width, header.Top + 18) : new SKRect(edge - width, header.Top + 2, edge, header.Top + 18);
            canvas.DrawRoundRect(pill, 8, 8, fill);
            fill.Color = SKColors.White;
            canvas.DrawText(text, pill.MidX, pill.MidY + 4, SKTextAlign.Center, font, fill);
        }

        /// <summary>With content widths, the strip scrolls so the selected header shows.</summary>
        private void KeepInView(int index)
        {
            if (index < 0 || index >= _engine.Count || _owner._stripScroller is not { } scroller || !double.IsFinite(ViewportWidth)
                || _engine.Length <= ViewportWidth + 0.5)
                return;
            var header = _engine.Segment(index);
            var x = scroller.ScrollX;
            var target = header.Left < x ? header.Left : header.Right > x + ViewportWidth ? header.Right - ViewportWidth : x;
            if (Math.Abs(target - x) > 0.5)
                _ = scroller.ScrollToAsync(target, 0, _owner.IsShown && !SkUiMotion.IsMotionReduced);
        }

        #region Input, keys and screen readers

        protected override bool HandlesTap => _owner.IsEnabled && _engine.Count > 0;

        protected override void OnTapped(SkUiTappedEventArgs args)
        {
            RaiseTapped(args);
            if (args.Position.Y >= 0 && args.Position.Y <= Height)
                _owner.SelectByUser(_engine.HitTest(args.Position.X));
        }

        internal override bool TakesKeyboardFocus => _engine.Count > 0;

        /// <summary>Left / Right the previous / next enabled tab (mirrored right to left), Home / End the first / last; no wrap.</summary>
        bool ISkUiKeyHandler.OnKeyDown(in SkUiKeyEvent key)
        {
            if (!_owner.IsEnabled || _engine.Count == 0 || key.HasCommandModifier)
                return false;
            var rtl = _owner.IsRightToLeft;
            var selected = _owner.SelectedIndex;
            var target = key.Key switch
            {
                SkUiKey.Right => _owner.NextEnabled(selected, rtl ? -1 : 1, wrap: false),
                SkUiKey.Left => _owner.NextEnabled(selected, rtl ? 1 : -1, wrap: false),
                SkUiKey.Home => _owner.FirstEnabled(0, 1),
                SkUiKey.End => _owner.FirstEnabled(_engine.Count - 1, -1),
                _ => -2
            };
            if (target == -2)
                return false;
            if (target >= 0)
                _owner.SelectByUser(target);
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

        /// <summary>The strip is a tab list: each header an element ("Reviews, 3, Selected, 2 of 3"), activated to select it.</summary>
        protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
        {
            base.OnPopulateSemantics(info);
            var tabs = _owner.Tabs;
            if (tabs.Count == 0)
                return;
            info.IsEnabled &= _owner.IsEnabled;
            info.IsHorizontal = true;
            var selected = _owner.SelectedIndex;
            info.Value = selected >= 0 && selected < tabs.Count ? tabs[selected].Header : null;
            if (_owner.IsEnabled)
                info.Actions |= SkUiSemanticsActions.Increment | SkUiSemanticsActions.Decrement;
            var strings = SkUiStrings.Current;
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            for (var index = 0; index < tabs.Count && index < _engine.Count; index++)
            {
                var tab = tabs[index];
                var label = tab.BadgeText is { Length: > 0 } badge ? string.Concat(tab.Header, ", ", badge) : tab.Header ?? string.Empty;
                var place = string.Format(culture, strings.ItemPositionFormat, index + 1, tabs.Count);
                info.AddPart(new SkUiSemanticsPart(PartId(index), index, _engine.Segment(index), label,
                    "_" + (tab.AutomationId is { Length: > 0 } id ? id : index.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    tab.IsEnabled, index == selected ? string.Concat(strings.SelectedStateText, ", ", place) : place));
            }
        }

        protected override bool OnSemanticsAction(SkUiSemanticsActions action)
        {
            var target = action switch
            {
                SkUiSemanticsActions.Increment => _owner.NextEnabled(_owner.SelectedIndex, 1, wrap: false),
                SkUiSemanticsActions.Decrement => _owner.NextEnabled(_owner.SelectedIndex, -1, wrap: false),
                _ => -1
            };
            return target >= 0 && _owner.SelectByUser(target) || base.OnSemanticsAction(action);
        }

        internal override bool OnSemanticsPartAction(int part, SkUiSemanticsActions action) =>
            action == SkUiSemanticsActions.Activate && _owner.IsTabEnabled(part) && (_owner.SelectByUser(part) || part == _owner.SelectedIndex);

        #endregion

        internal override void OnEffectiveFlowDirectionChanged()
        {
            base.OnEffectiveFlowDirectionChanged();
            _prepared = false;
            InvalidateMeasureOverride();
        }

        internal override void ReleaseDrawingResources()
        {
            base.ReleaseDrawingResources();
            foreach (var header in _headers)
                header.Layout?.Release();
        }
    }

    /// <summary>The strip's headers, a layer of their own over the indicator; taps go through to the strip.</summary>
    private sealed class TabLabels : SkUiView
    {
        private readonly TabStrip _strip;

        public TabLabels(TabStrip strip)
        {
            _strip = strip;
            InputTransparent = true;
        }

        protected override void OnPaintContent(SKCanvas canvas) => _strip.DrawHeaders(canvas);
    }

    /// <summary>
    /// The indicator: drawn once in its own bounds, and placed (and, with headers of different widths, clipped to each one's
    /// width) by piecewise scroll-linked channels from the pane scroller's offset, so it moves with the panes on the render
    /// thread with no UI-thread work per frame (D8).
    /// </summary>
    private sealed class TabIndicator(SkUiTabView owner, TabStrip strip) : SkUiView
    {
        private SkUiRenderLink[]? _links;
        private (SkUiRenderNode? Source, int Strip, int Panes, SkUiTabIndicatorStyle IndicatorStyle, bool Rtl) _linked;

        private SkUiTabIndicatorStyle IndicatorStyle => owner.IndicatorStyle;

        /// <summary>The indicator's rectangle for tab <paramref name="index"/> in the strip.</summary>
        public Rect BoundsFor(int index) =>
            SkUiLook.Current.GetTabIndicatorBounds(strip.HeaderBounds(index), strip.LabelWidth(index), IndicatorStyle,
                owner.TabStripPlacement == SkUiTabStripPlacement.Bottom);

        /// <summary>Where the node is arranged: at the strip's start, as wide as the widest tab's indicator; the links move it.</summary>
        public Rect MaxBounds(double stripHeight)
        {
            var count = owner.Tabs.Count;
            if (count == 0 || IndicatorStyle == SkUiTabIndicatorStyle.None)
                return Rect.Zero;
            var first = BoundsFor(0);
            var width = first.Width;
            for (var index = 1; index < count; index++)
                width = Math.Max(width, BoundsFor(index).Width);
            return new Rect(0, first.Y, width, first.Height);
        }

        public void GeometryChanged() => InvalidateRender(SkUiRenderDirty.Props);

        protected override void OnPaintContent(SKCanvas canvas)
        {
            if (IndicatorStyle == SkUiTabIndicatorStyle.None || Width <= 0 || Height <= 0)
                return;
            var look = SkUiLook.Current;
            // A pill sits behind the selected header's text: by default a light tint of the look's color, so the text shows.
            var color = owner.IndicatorColor
                ?? (IndicatorStyle == SkUiTabIndicatorStyle.Pill ? strip.LookColors(look).Indicator.WithAlpha(0.16f) : strip.LookColors(look).Indicator);
            var paint = new SkUiTabIndicatorPaint { Bounds = new SKRect(0, 0, (float)Width, (float)Height), Color = ToSk(color), Style = IndicatorStyle };
            look.DrawTabIndicator(canvas, paint);
        }

        internal override void OnGetRenderProps(ref SkUiRenderProps props)
        {
            var count = owner.Tabs.Count;
            if (count == 0 || IndicatorStyle == SkUiTabIndicatorStyle.None || owner._paneScroller is not { } scroller || !owner.Panes.HasGeometry)
                return;
            var source = ((ISkUiRenderable)scroller).RenderState.Node;
            var key = (source, strip.GeometryVersion, owner.Panes.GeometryVersion, IndicatorStyle, owner.IsRightToLeft);
            if (_links is not { } links || _linked != key)
            {
                links = _links = BuildLinks(source, count);
                _linked = key;
            }
            props.Links = links;
            var input = owner.PaneController.LinkInput;
            props.LinkValues = SkUiRenderLinkValues.Evaluate(links, input.X, input.Y);
        }

        /// <summary>Each tab's pane offset mapped onto its indicator's position (and width, when they differ), in ascending offsets.</summary>
        private SkUiRenderLink[] BuildLinks(SkUiRenderNode source, int count)
        {
            var inputs = new float[count];
            var positions = new float[count];
            var widths = new float[count];
            var sameWidth = true;
            for (var index = 0; index < count; index++)
            {
                var bounds = BoundsFor(index);
                inputs[index] = (float)owner.Panes.PhysicalOffsetOf(index);
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
            var rounded = IndicatorStyle == SkUiTabIndicatorStyle.Pill
                || SkUiLook.Current is DefaultSkUiLook { TabStripStyle: SkUiTabStripStyle.LabelUnderline or SkUiTabStripStyle.ShortBar };
            var clip = new SkUiRenderLink(source, horizontal: true, SkUiRenderLinkTarget.ClipWidth, inputs, widths, SkUiRenderLinkClipEdge.Start,
                rounded ? SkUiRenderLinkClipShape.Capsule : SkUiRenderLinkClipShape.Rectangle);
            return [move, clip];
        }
    }

    private static SKRect ToSk(Rect rect) => new((float)rect.Left, (float)rect.Top, (float)rect.Right, (float)rect.Bottom);

    private static SKColor ToSk(Color color) => SkUiToggleDrawing.ToSkColor(color);
}
