using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi.Core;

public partial class SkUiCoreTabView
{
    /// <summary>The panes' scroller: once laid out, it moves to where the panes' layout wants it, and it is named by the selected tab.</summary>
    private sealed class PaneScroller(SkUiCoreTabView owner) : SkUiCoreScrollView
    {
        protected override void ArrangeContent(Size size)
        {
            base.ArrangeContent(size);
            owner._panes.ApplyPendingOffset();
        }

        private protected override void OnScrolled() => owner._panes.Update();

        /// <remarks>As its text: a description would stand for what is inside, and hide the pane's texts.</remarks>
        protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
        {
            base.OnPopulateSemantics(info);
            info.Text = owner._selectedTab?.Header;
        }

        public void SemanticsChanged() => InvalidateSemantics();
    }

    /// <summary>Hosts one tab's pane.</summary>
    private sealed class PaneCell(SkUiCoreTabView owner, SkUiCoreTabItem tab) : SkUiCoreContentView, ISkUiKeyboardScope
    {
        public SkUiCoreTabItem Tab { get; } = tab;

        bool ISkUiKeyboardScope.TakesTabStops => ReferenceEquals(owner._selectedTab, Tab);
    }

    /// <summary>The strip of panes inside the pane scroller (as <see cref="SkUiTabView"/>'s): panes are created when they first come into view and kept at their places.</summary>
    private sealed class PaneHost : SkUiCoreNode, ISkUiSnapPointSource, ISkUiCarouselEngineHost
    {
        private readonly SkUiCoreTabView _owner;
        private readonly Dictionary<SkUiCoreTabItem, PaneCell> _cells = [];
        private readonly List<PaneCell> _children = [];
        private double _arrangedCross = double.NaN;
        private bool _arranged;
        private bool _updating;

        public PaneHost(SkUiCoreTabView owner)
        {
            _owner = owner;
            Engine = new SkUiCarouselEngine(this);
        }

        public SkUiCarouselEngine Engine { get; }

        /// <summary>A version bumped when the panes' geometry changes (the indicator rebuilds its links).</summary>
        public int GeometryVersion { get; private set; }

        bool ISkUiCarouselEngineHost.Loop => false;

        bool ISkUiCarouselEngineHost.Horizontal => true;

        bool ISkUiCarouselEngineHost.Mirrored => _owner.IsRightToLeft;

        double ISkUiCarouselEngineHost.ProbeExtent(double cross) => _children.Count > 0 ? _children[0].Measure(double.PositiveInfinity, cross).Width : 0;

        private SkUiScrollController Controller => _owner._controller;

        private double LogicalOffset => Engine.LogicalOffset(Controller.X);

        public bool IsCreated(SkUiCoreTabItem tab) => _cells.ContainsKey(tab);

        public int CurrentIndex(int keep) => Engine.CurrentIndex(LogicalOffset, keep);

        /// <summary>The tab whose pane shows at scroll offset <paramref name="offset"/> (the nearest; <paramref name="keep"/> on a tie).</summary>
        public int IndexAt(double offset, int keep) => Engine.CurrentIndex(Engine.Logical(offset), keep);

        public int Position => Math.Clamp(_owner._requested, 0, Math.Max(0, Engine.Count - 1));

        /// <summary>How wide the panes are when nothing bounds them: as the first created (the selected one's, to begin with) asks for.</summary>
        public double UnboundedWidth(double cross)
        {
            if (_children.Count == 0)
                EnsurePane(Position);
            var width = Engine.UnboundedViewport(default, cross);
            if (_children.Count > 0 && Engine.HasGeometry)
                MeasureCell(_children[0]); // probed unbounded: as laid out again
            return width;
        }

        public bool IsArranged => _arranged;

        public void SetViewport(double viewport)
        {
            if (!Engine.SetViewport(viewport, LogicalOffset, _arranged))
                return;
            _disturbed |= Controller.IsMotionRunning;
            // The tab view measures (and arranges) the scroller right after: see the carousel's panel.
            ForgetMeasure();
            _owner._paneScroller.ForgetMeasure();
        }

        /// <summary>The tabs changed (or the direction): tab <paramref name="place"/>, fractional while the panes move, shows once laid out.</summary>
        public void SetCount(int count, double place)
        {
            _disturbed |= Controller.IsMotionRunning;
            Engine.SetCount(count, place);
            InvalidateMeasure();
        }

        /// <summary>How far the panes are past tab <paramref name="index"/>, in panes.</summary>
        public double PastTab(int index) => _arranged ? Engine.PastItem(index, LogicalOffset) : 0;

        /// <summary>
        /// Whether the layout changed under a running motion since the last rest (read once): it may stop short of a snap point
        /// then (the offset clamped, the items' lengths changed), and the panes go on to one.
        /// </summary>
        public bool TakeDisturbed()
        {
            var disturbed = _disturbed;
            _disturbed = false;
            return disturbed;
        }

        private bool _disturbed;

        /// <summary>Whether tab <paramref name="index"/>'s pane is in place (or will be, once laid out).</summary>
        public bool RestsOn(int index) => Engine.HasPendingOffset || Math.Abs(OffsetOf(index) - Controller.X) < SkUiCarouselEngine.VisibilityTolerance;

        /// <summary>The scroll offset that shows tab <paramref name="index"/>'s pane.</summary>
        public double OffsetOf(int index) => Engine.HasGeometry ? Engine.Physical(Engine.ClampedSnapOffset(index)) : 0;

        /// <summary>The tab whose pane <paramref name="node"/> is in, or <c>null</c>.</summary>
        public SkUiCoreTabItem? TabOf(ISkUiRenderable node)
        {
            for (var current = node; current is not null; current = current.RenderParent)
                if (current is PaneCell cell && _children.Contains(cell))
                    return cell.Tab;
            return null;
        }

        public Task ScrollToIndex(int index, bool animated)
        {
            var logical = LogicalOffset;
            if (Engine.DeferPlacement(index, logical, _arranged))
            {
                Controller.StopMotion(); // it would go on from the placement
                InvalidateMeasure();
                return Task.CompletedTask;
            }
            EnsurePane(index); // shown at once where the scroll ends
            return Controller.ScrollToAsync(Engine.Physical(Engine.TargetOffset(index, logical)), Controller.Y, animated);
        }

        public void ApplyPendingOffset()
        {
            if (Engine.TakePendingOffset() is not { } target)
                return;
            var delta = target - Controller.X;
            if (Math.Abs(delta) < 0.01)
                Update();
            else if (Controller.IsMotionRunning || Controller.Dragging)
            {
                Controller.CorrectOffset(delta, 0);
                InvalidateMeasure(); // the scroller's extent comes back with the next measure
            }
            else
            {
                Controller.SetOffset(target, Controller.Y);
            }
        }

        /// <summary>The panes moved: panes coming into view for the first time are created (nothing else changes per frame).</summary>
        public void Update()
        {
            if (_updating || !_arranged)
                return;
            _updating = true;
            try { RealizeVisible(); }
            finally { _updating = false; }
        }

        private void RealizeVisible()
        {
            if (!Engine.HasGeometry || Engine.Count == 0)
                return;
            var (first, last) = Engine.VisibleSlots(LogicalOffset);
            for (var index = first; index <= last; index++)
                EnsurePane(index);
        }

        /// <summary>Creates tab <paramref name="index"/>'s pane if it has none yet, measured and arranged at its place.</summary>
        private void EnsurePane(int index)
        {
            if (index < 0 || index >= _owner._tabs.Count || _owner._tabs[index] is var tab && _cells.ContainsKey(tab))
                return;
            tab = _owner._tabs[index];
            var cell = new PaneCell(_owner, tab);
            cell.SetContent(MakeContent(tab));
            _cells[tab] = cell;
            _children.Add(cell);
            cell.AttachTo(this);
            MeasureCell(cell);
            if (_arranged)
                ArrangeCell(cell, index);
            InvalidateRender(SkUiRenderDirty.Children);
            // Panes as tall as their content: the new one may be taller than the others.
            if (!double.IsFinite(Engine.CrossConstraint))
                InvalidateMeasure();
        }

        private static SkUiCoreNode? MakeContent(SkUiCoreTabItem tab) => tab.Content ?? tab.ContentFactory?.Invoke();

        /// <summary>The tab left: its pane goes.</summary>
        public void Forget(SkUiCoreTabItem tab)
        {
            if (!_cells.Remove(tab, out var cell))
                return;
            _children.Remove(cell);
            cell.SetContent(null);
            cell.AttachTo(null);
            InvalidateRender(SkUiRenderDirty.Children);
            InvalidateMeasure();
        }

        /// <summary>The tab's content or factory changed: a created pane shows the new one.</summary>
        public void ContentChanged(SkUiCoreTabItem tab)
        {
            if (_cells.TryGetValue(tab, out var cell))
                cell.SetContent(MakeContent(tab));
        }

        private void MeasureCell(PaneCell cell) =>
            cell.Measure(Engine.Extent, double.IsFinite(_arrangedCross) ? _arrangedCross : Engine.CrossConstraint);

        // Logical: Core arrange mirrors frames right to left.
        private void ArrangeCell(PaneCell cell, int index) =>
            cell.Arrange(new Rect(Engine.SlotStart(index), 0, Engine.Extent, double.IsFinite(_arrangedCross) ? _arrangedCross : 0));

        void ISkUiSnapPointSource.GetSnapOffsets(bool horizontal, double from, double to, List<double> points)
        {
            if (horizontal)
                _owner._strip.Model.AddSnapOffsets(Engine, from, to, points);
        }

        internal override IReadOnlyList<IVisualTreeElement> VisualChildren => _children;

        internal override void AddRenderChildren(List<ISkUiRenderable> children) => children.AddRange(_children);

        protected override Size MeasureContent(double widthConstraint, double heightConstraint)
        {
            if (Engine.NeedsLayout(heightConstraint))
            {
                Engine.UpdateGeometry(new SkUiCarouselLayout(default, 0, 0, SnapPointsAlignment.Start, null), heightConstraint, Position);
                GeometryVersion++;
            }
            RealizeVisible();
            var across = 0d;
            foreach (var cell in _children)
            {
                MeasureCell(cell);
                across = Math.Max(across, cell.DesiredSize.Height);
            }
            return new Size(Engine.Length, Math.Min(across, heightConstraint));
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
                RealizeVisible();
                foreach (var cell in _children)
                    ArrangeCell(cell, _owner._tabs.IndexOf(cell.Tab));
            }
            finally
            {
                _updating = false;
            }
        }
    }

    /// <summary>
    /// The strip of headers (<see cref="SkUiTabStripModel"/>): its background, the indicator over it, and the labels and the
    /// app's header nodes over that.
    /// </summary>
    private sealed class TabStrip : SkUiCoreNode, ISkUiKeyHandler, ISkUiTabHeaderHost
    {
        private readonly SkUiCoreTabView _owner;
        private readonly TabIndicator _indicator;
        private readonly TabLabels _labels;
        private readonly Dictionary<SkUiCoreTabItem, HeaderCell> _headerCells = [];
        private readonly HashSet<SkUiCoreTabItem> _declined = []; // the factory made no header for them
        private List<HeaderCell> _headerOrder = []; // in the tabs' order: drawing
        private Func<SkUiCoreTabItem, SkUiCoreNode?>? _factory; // the one that made the nodes
        private bool _laidOut;

        public TabStrip(SkUiCoreTabView owner)
        {
            _owner = owner;
            Model = new SkUiTabStripModel(owner, this, this, () => _labels!.InvalidatePaint());
            _indicator = new TabIndicator(owner, Model);
            _labels = new TabLabels(Model);
            _indicator.AttachTo(this);
            _labels.AttachTo(this);
        }

        public SkUiTabStripModel Model { get; }

        public SkUiCoreNode Indicator => _indicator;

        /// <summary>The tab view's width (the strip fills it, or scrolls past it with content widths).</summary>
        public double ViewportWidth { get; set; } = double.NaN;

        public void TabsChanged()
        {
            SyncHeaders();
            Model.Invalidate();
            InvalidateMeasure();
            InvalidatePaint();
            _labels.InvalidatePaint();
            InvalidateSemantics();
            _indicator.InvalidateRender(SkUiRenderDirty.Props);
            _indicator.InvalidatePaint(); // its style may have changed
            UpdateHeaderStates();
        }

        public void SemanticsChanged() => InvalidateSemantics();

        public void SelectionChanged(int index)
        {
            _labels.InvalidatePaint(); // the selected header's color: only the labels re-record
            InvalidateSemantics();
            KeepInView(index, _owner.IsShownNow() && !SkUiMotion.IsMotionReduced);
            UpdateHeaderStates();
        }

        public void ColorsChanged()
        {
            InvalidatePaint();
            _labels.InvalidatePaint();
            _indicator.InvalidatePaint();
            UpdateHeaderStates();
        }

        #region Header nodes

        /// <summary>
        /// The tabs, their header nodes or the tab view's factory changed: each tab's header node is its own, else the factory's
        /// (made once per tab and factory), else none.
        /// </summary>
        private void SyncHeaders()
        {
            var tabs = _owner._tabs;
            var factory = _owner._headerFactory;
            if (!ReferenceEquals(factory, _factory))
            {
                _factory = factory;
                _declined.Clear();
                foreach (var cell in _headerOrder)
                    if (cell.Source is not SkUiCoreNode)
                        cell.Source = null; // made again by the new factory
            }
            if (_headerCells.Count == 0 && (factory is null || tabs.Count == 0) && !AnyOwnHeader())
                return;
            var kept = new List<HeaderCell>();
            foreach (var tab in tabs)
            {
                _headerCells.TryGetValue(tab, out var cell);
                var source = tab.HeaderContent is { } own ? own : (object?)factory;
                if (source is null || (cell is null && ReferenceEquals(source, factory) && _declined.Contains(tab)))
                    continue;
                if (!ReferenceEquals(cell?.Source, source))
                {
                    var node = tab.HeaderContent ?? factory!(tab);
                    if (node is null)
                    {
                        _declined.Add(tab);
                        continue;
                    }
                    if (cell is null)
                    {
                        cell = new HeaderCell(tab);
                        _headerCells[tab] = cell;
                        cell.AttachTo(this);
                    }
                    cell.SetContent(null); // the old node lets go first: the new one may be the same
                    cell.SetContent(node);
                    cell.Source = source;
                }
                cell.SetIsEnabled(tab.IsEnabled);
                kept.Add(cell);
            }
            foreach (var cell in _headerOrder)
                if (!kept.Contains(cell) && _headerCells.Remove(cell.Tab))
                {
                    cell.SetContent(null);
                    cell.AttachTo(null);
                }
            _declined.RemoveWhere(tab => !tabs.Contains(tab));
            _headerOrder = kept;
            InvalidateRender(SkUiRenderDirty.Children);

            bool AnyOwnHeader()
            {
                foreach (var tab in tabs)
                    if (tab.HeaderContent is not null)
                        return true;
                return false;
            }
        }

        /// <summary>Each tab learns whether it is selected and its header's text color.</summary>
        private void UpdateHeaderStates()
        {
            var tabs = _owner._tabs;
            var selected = _owner._selectedIndex;
            for (var index = 0; index < tabs.Count; index++)
                tabs[index].SetHeaderState(index == selected, Model.CurrentTextColor(index, tabs[index].CurrentTextColor), _owner);
        }

        private HeaderCell? CellAt(int index) =>
            _headerCells.Count > 0 && index >= 0 && index < _owner._tabs.Count && _headerCells.TryGetValue(_owner._tabs[index], out var cell) ? cell : null;

        /// <summary>Tab <paramref name="index"/>'s header node (tests).</summary>
        public SkUiCoreNode? HeaderNodeOf(int index) => CellAt(index)?.Content;

        bool ISkUiTabHeaderHost.HasHeaderView(int index) => CellAt(index) is not null;

        Size ISkUiTabHeaderHost.MeasureHeaderView(int index, double widthConstraint, double heightConstraint) =>
            CellAt(index) is { } cell ? cell.Measure(widthConstraint, heightConstraint) : Size.Zero;

        void ISkUiTabHeaderHost.ArrangeHeaderView(int index, Rect bounds)
        {
            if (CellAt(index) is not { } cell)
                return;
            cell.Measure(bounds.Width, bounds.Height);
            cell.Arrange(bounds);
        }

        #endregion

        /// <summary>The strip's scroller was arranged: after a new layout of the headers, the selected one is brought into view.</summary>
        public void AfterArrange()
        {
            if (!_laidOut)
                return;
            _laidOut = false;
            KeepInView(_owner._selectedIndex, animated: false);
        }

        /// <summary>The strip took focus: its selected header shows; returns where its scroller ends.</summary>
        public Point? Reveal(bool animated)
        {
            if (_owner._stripScroller is not { } scroller)
                return null;
            if (Model.KeepInView(_owner._selectedIndex, scroller.ScrollX, ViewportWidth) is not { } target)
                return new Point(scroller.ScrollX, scroller.ScrollY);
            _ = scroller.ScrollToAsync(target, 0, animated);
            return new Point(target, scroller.ScrollY);
        }

        private void KeepInView(int index, bool animated)
        {
            if (_owner._stripScroller is { } scroller && Model.KeepInView(index, scroller.ScrollX, ViewportWidth) is { } target)
                _ = scroller.ScrollToAsync(target, 0, animated);
        }

        internal override IReadOnlyList<IVisualTreeElement> VisualChildren => [_indicator, _labels, .. _headerOrder];

        internal override void AddRenderChildren(List<ISkUiRenderable> children)
        {
            children.Add(_indicator);
            children.Add(_labels);
            children.AddRange(_headerOrder);
        }

        protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
            new(Model.Measure(ViewportWidth), Math.Min(heightConstraint, Model.DesiredHeightIn(ViewportWidth)));

        protected override void ArrangeContent(Size size)
        {
            Model.Arrange(ViewportWidth, size.Width, size.Height);
            _laidOut = true;
            var bounds = Model.IndicatorFrame(size.Width);
            _indicator.Measure(bounds.Width, bounds.Height);
            _indicator.Arrange(bounds);
            _indicator.InvalidateRender(SkUiRenderDirty.Props);
            _labels.Measure(size.Width, size.Height);
            _labels.Arrange(new Rect(0, 0, size.Width, size.Height));
            _labels.InvalidatePaint();
        }

        protected override void OnPaintContent(SKCanvas canvas) => Model.DrawBackground(canvas, (float)Frame.Width, (float)Frame.Height);

        internal override bool HasIntrinsicTap => Model.TakesInput;

        internal override void OnIntrinsicTap(SkUiTappedEventArgs args) => Model.Tap(args.Position);

        internal override bool TakesKeyboardFocus => Model.TakesInput;

        bool ISkUiKeyHandler.OnKeyDown(in SkUiKeyEvent key) => Model.OnKeyDown(key);

        protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
        {
            base.OnPopulateSemantics(info);
            Model.PopulateSemantics(info);
        }

        protected override bool OnSemanticsAction(SkUiSemanticsActions action) => Model.PerformSemanticsAction(action) || base.OnSemanticsAction(action);

        internal override bool OnSemanticsPartAction(int part, SkUiSemanticsActions action) => Model.PerformSemanticsPartAction(part, action);

        internal override void ReleaseDrawingResources()
        {
            base.ReleaseDrawingResources();
            Model.ReleaseDrawingResources();
        }
    }

    /// <summary>
    /// Hosts a tab's header node of the app's (1.18 T), over the indicator. Taps go through to the strip, keyboard focus does
    /// not go in, and screen readers read the strip's header element instead.
    /// </summary>
    private sealed class HeaderCell : SkUiCoreContentView
    {
        public HeaderCell(SkUiCoreTabItem tab)
        {
            Tab = tab;
            SetInputTransparent(true);
            SetExcludedWithChildren(true);
        }

        public SkUiCoreTabItem Tab { get; }

        /// <summary>The tab's own header node, or the factory that made it.</summary>
        public object? Source { get; set; }
    }

    /// <summary>The strip's headers, a layer of their own over the indicator; taps go through to the strip.</summary>
    private sealed class TabLabels : SkUiCoreNode
    {
        private readonly SkUiTabStripModel _model;

        public TabLabels(SkUiTabStripModel model)
        {
            _model = model;
            SetInputTransparent(true);
        }

        protected override void OnPaintContent(SKCanvas canvas) => _model.DrawHeaders(canvas);
    }

    /// <summary>The indicator: drawn once in its own bounds and moved with the panes on the render thread by its links (D8).</summary>
    private sealed class TabIndicator : SkUiCoreNode
    {
        private readonly SkUiCoreTabView _owner;
        private readonly SkUiTabStripModel _model;

        public TabIndicator(SkUiCoreTabView owner, SkUiTabStripModel model)
        {
            _owner = owner;
            _model = model;
            SetInputTransparent(true);
        }

        protected override void OnPaintContent(SKCanvas canvas) => _model.DrawIndicator(canvas, (float)Frame.Width, (float)Frame.Height);

        internal override void OnGetRenderProps(ref SkUiRenderProps props)
        {
            if (_owner._paneScroller is { } scroller)
                _model.ApplyLinks(ref props, ((ISkUiRenderable)scroller).RenderState.Node, _owner._controller, _owner._panes.Engine, _owner._panes.GeometryVersion);
        }

        internal override void ReleaseDrawingResources()
        {
            base.ReleaseDrawingResources();
            _model.ForgetLinks();
        }
    }
}
