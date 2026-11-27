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

        /// <summary>
        /// The panes' container is named by the selected tab. As its text, not its description: a description stands for what
        /// is inside, and the pane's texts would no longer be elements.
        /// </summary>
        protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
        {
            base.OnPopulateSemantics(info);
            info.Text = owner._selectedTab?.Header;
        }

        public void SemanticsChanged() => InvalidateSemantics();
    }

    /// <summary>Hosts one tab's pane.</summary>
    private sealed class PaneCell(SkUiTabView owner, SkUiTabItem tab) : SkUiContentView, ISkUiKeyboardScope
    {
        public SkUiTabItem Tab { get; } = tab;

        bool ISkUiKeyboardScope.TakesTabStops => ReferenceEquals(owner._selectedTab, Tab);
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

        public int Position => Math.Clamp(_owner._requested, 0, Math.Max(0, _engine.Count - 1));

        /// <summary>Panes that fill an unbounded tab view are as wide as the first created one measures (else nothing).</summary>
        double ISkUiCarouselEngineHost.ProbeExtent(double cross) =>
            _children.Count > 0 ? ((IView)_children[0]).Measure(double.PositiveInfinity, cross).Width : 0;

        public bool HasGeometry => _engine.HasGeometry;

        /// <summary>How wide the panes are when nothing bounds them: as the first created (the selected one's, to begin with) asks for.</summary>
        public double UnboundedWidth(double cross)
        {
            if (_children.Count == 0)
                EnsurePane(Position);
            var width = _engine.UnboundedViewport(default, cross);
            if (_children.Count > 0 && _engine.HasGeometry)
                MeasureCell(_children[0]); // probed unbounded: as laid out again
            return width;
        }

        public bool IsArranged => _arranged;

        /// <summary>A version bumped when the panes' geometry changes (the indicator rebuilds its links).</summary>
        public int GeometryVersion { get; private set; }

        private SkUiScrollController Controller => _owner.PaneController;

        private double LogicalOffset => _engine.LogicalOffset(Controller.X);

        public bool IsCreated(SkUiTabItem tab) => _cells.ContainsKey(tab);

        public SkUiCarouselEngine Engine => _engine;

        public void SetViewport(double viewport)
        {
            if (!_engine.SetViewport(viewport, LogicalOffset, _arranged))
                return;
            _disturbed |= Controller.IsMotionRunning;
            // The tab view measures (and arranges) the scroller right after: see the carousel's panel.
            ForgetMeasure();
            _owner.PaneScrollView.ForgetMeasure();
        }

        /// <summary>The tabs changed (or the direction): tab <paramref name="place"/>, fractional while the panes move, shows once laid out.</summary>
        public void SetCount(int count, double place)
        {
            _disturbed |= Controller.IsMotionRunning;
            _engine.SetCount(count, place);
            InvalidateMeasureOverride();
        }

        /// <summary>How far the panes are past tab <paramref name="index"/>, in panes.</summary>
        public double PastTab(int index) => _arranged ? _engine.PastItem(index, LogicalOffset) : 0;

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
        public bool RestsOn(int index) => _engine.HasPendingOffset || Math.Abs(OffsetOf(index) - Controller.X) < SkUiCarouselEngine.VisibilityTolerance;

        /// <summary>The scroll offset that shows tab <paramref name="index"/>'s pane.</summary>
        public double OffsetOf(int index) => _engine.HasGeometry ? _engine.Physical(_engine.ClampedSnapOffset(index)) : 0;

        /// <summary>The tab whose pane <paramref name="node"/> is in, or <c>null</c>.</summary>
        public SkUiTabItem? TabOf(ISkUiRenderable node)
        {
            for (var current = node; current is not null; current = current.RenderParent)
                if (current is PaneCell cell && _children.Contains(cell))
                    return cell.Tab;
            return null;
        }

        public int CurrentIndex(int keep) => _engine.CurrentIndex(LogicalOffset, keep);

        public Task ScrollToIndex(int index, bool animated)
        {
            var logical = LogicalOffset;
            if (_engine.DeferPlacement(index, logical, _arranged))
            {
                Controller.StopMotion(); // it would go on from the placement
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
            {
                Controller.CorrectOffset(delta, 0);
                InvalidateMeasureFromChild(); // the scroller's extent comes back with the next measure
            }
            else
            {
                Controller.SetOffset(target, Controller.Y);
            }
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
                RealizeVisible();
            }
            finally
            {
                _updating = false;
            }
        }

        private void RealizeVisible()
        {
            if (!HasGeometry || _engine.Count == 0)
                return;
            var (first, last) = _engine.VisibleSlots(LogicalOffset);
            for (var index = first; index <= last; index++)
                EnsurePane(index);
        }

        /// <summary>
        /// Creates tab <paramref name="index"/>'s pane if it has none yet, measured and arranged at its place (whoever asks: a
        /// layout, the panes scrolling, a scroll to it that has not started).
        /// </summary>
        private void EnsurePane(int index)
        {
            var tabs = _owner.Tabs;
            if (index < 0 || index >= tabs.Count)
                return;
            var tab = tabs[index];
            if (_cells.ContainsKey(tab))
                return;
            var cell = new PaneCell(_owner, tab) { BindingContext = tab.BindingContext, Content = MakeContent(tab) };
            _cells[tab] = cell;
            _children.Add(cell);
            AddLogicalChild(cell);
            MeasureCell(cell);
            if (_arranged)
                ArrangeCell(cell, index);
            InvalidateRender(SkUiRenderDirty.Children);
            // Panes as tall as their content: the new one may be taller than the others.
            if (!double.IsFinite(_engine.CrossConstraint))
                InvalidateMeasureOverride();
        }

        /// <summary>The tab's binding context changed: its pane binds to it (which is the tab view's unless the tab has its own).</summary>
        public void BindPane(SkUiTabItem tab)
        {
            if (_cells.TryGetValue(tab, out var cell))
                cell.BindingContext = tab.BindingContext;
        }

        private ISkUiView? MakeContent(SkUiTabItem tab)
        {
            var template = tab.ContentTemplate is DataTemplateSelector selector ? selector.SelectTemplate(tab.BindingContext, _owner) : tab.ContentTemplate;
            var view = tab.Content ?? template?.CreateContent() as View;
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
            if (horizontal)
                _owner.Strip.Model.AddSnapOffsets(_engine, from, to, points);
        }

        internal override IEnumerable<ISkUiView> SkiaChildren => _children;

        protected override Size MeasureContent(double widthConstraint, double heightConstraint)
        {
            if (_engine.NeedsLayout(heightConstraint))
            {
                _engine.UpdateGeometry(new SkUiCarouselLayout(default, 0, 0, SnapPointsAlignment.Start, null), heightConstraint, Position);
                GeometryVersion++;
            }
            RealizeVisible();
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
                RealizeVisible();
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

    /// <summary>
    /// The strip of headers (<see cref="SkUiTabStripModel"/>): its background, the indicator over it, and the labels and the
    /// app's header views over that.
    /// </summary>
    private sealed class TabStrip : SkUiView, ISkUiKeyHandler, ISkUiTabHeaderHost
    {
        private readonly SkUiTabView _owner;
        private readonly Dictionary<SkUiTabItem, HeaderCell> _headerCells = [];
        private List<HeaderCell> _headerOrder = []; // in the tabs' order: drawing
        private bool _laidOut;

        public TabStrip(SkUiTabView owner)
        {
            _owner = owner;
            Model = new SkUiTabStripModel(owner, this, this, () => Labels!.InvalidatePaint());
            Indicator = new TabIndicator(owner, Model);
            Labels = new TabLabels(Model);
            AttachChild(Indicator);
            AttachChild(Labels);
        }

        public SkUiTabStripModel Model { get; }

        public TabIndicator Indicator { get; }

        /// <summary>The headers' texts, icons and badges, drawn over the indicator (a pill sits behind the text).</summary>
        public TabLabels Labels { get; }

        /// <summary>The tab view's width (the strip fills it, or scrolls past it with content widths).</summary>
        public double ViewportWidth { get; set; } = double.NaN;

        public void TabsChanged()
        {
            SyncHeaders();
            Model.Invalidate();
            InvalidateMeasureOverride();
            InvalidatePaint();
            Labels.InvalidatePaint();
            InvalidateSemantics();
            Indicator.InvalidateRender(SkUiRenderDirty.Props);
            Indicator.InvalidatePaint(); // its style may have changed
            UpdateHeaderStates();
        }

        public void SemanticsChanged() => InvalidateSemantics();

        public void SelectionChanged(int index)
        {
            Labels.InvalidatePaint(); // the selected header's color: only the labels re-record
            InvalidateSemantics();
            KeepInView(index, _owner.IsShown && !SkUiMotion.IsMotionReduced);
            UpdateHeaderStates();
        }

        public void ColorsChanged()
        {
            InvalidatePaint();
            Labels.InvalidatePaint();
            Indicator.InvalidatePaint();
            UpdateHeaderStates();
        }

        #region Header views

        /// <summary>
        /// The tabs, their header templates or the tab view's changed: each tab with a template has a header view made from it
        /// (made again when its template changes), the others none.
        /// </summary>
        private void SyncHeaders()
        {
            var tabs = _owner._tabs;
            if (_headerCells.Count == 0 && (tabs is null || !HasAnyTemplate(tabs)))
                return;
            var kept = new List<HeaderCell>();
            for (var index = 0; tabs is not null && index < tabs.Count; index++)
            {
                var tab = tabs[index];
                if (_owner.HeaderTemplateOf(tab) is not { } template)
                    continue;
                if (!_headerCells.TryGetValue(tab, out var cell))
                {
                    cell = new HeaderCell(tab);
                    _headerCells[tab] = cell;
                    AddLogicalChild(cell);
                }
                if (!ReferenceEquals(cell.Template, template))
                {
                    cell.Template = template;
                    cell.Content = template.CreateContent() as ISkUiView
                        ?? throw new InvalidOperationException($"A tab header template must make a drawn (SkUi*) view.");
                }
                cell.IsEnabled = tab.IsEnabled;
                kept.Add(cell);
            }
            foreach (var cell in _headerOrder)
                if (!kept.Contains(cell) && _headerCells.Remove(cell.Tab))
                {
                    cell.Content = null;
                    RemoveLogicalChild(cell);
                }
            _headerOrder = kept;
            InvalidateRender(SkUiRenderDirty.Children);

            bool HasAnyTemplate(IList<SkUiTabItem> items)
            {
                if (_owner.HeaderTemplate is not null)
                    return items.Count > 0;
                foreach (var item in items)
                    if (item.HeaderTemplate is not null)
                        return true;
                return false;
            }
        }

        /// <summary>Each tab learns whether it is selected and its header's text color; a header view's root is in the <c>Selected</c> visual state with it.</summary>
        private void UpdateHeaderStates()
        {
            if (_owner._tabs is not { } tabs)
                return;
            var selected = _owner.SelectedIndex;
            for (var index = 0; index < tabs.Count; index++)
            {
                var tab = tabs[index];
                tab.SetHeaderState(index == selected, Model.CurrentTextColor(index));
                if (_headerCells.Count > 0 && _headerCells.TryGetValue(tab, out var cell) && cell.Content is SkUiView root)
                    root.IsSelectedItem = index == selected;
            }
        }

        private HeaderCell? CellAt(int index) =>
            _headerCells.Count > 0 && _owner._tabs is { } tabs && index >= 0 && index < tabs.Count && _headerCells.TryGetValue(tabs[index], out var cell) ? cell : null;

        /// <summary>Tab <paramref name="index"/>'s header view (tests).</summary>
        public ISkUiView? HeaderViewOf(int index) => CellAt(index)?.Content;

        bool ISkUiTabHeaderHost.HasHeaderView(int index) => CellAt(index) is not null;

        Size ISkUiTabHeaderHost.MeasureHeaderView(int index, double widthConstraint, double heightConstraint) =>
            CellAt(index) is IView cell ? cell.Measure(widthConstraint, heightConstraint) : Size.Zero;

        void ISkUiTabHeaderHost.ArrangeHeaderView(int index, Rect bounds)
        {
            if (CellAt(index) is not IView cell)
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
            KeepInView(_owner.SelectedIndex, animated: false);
        }

        /// <summary>The strip took focus: its selected header shows; returns where its scroller ends.</summary>
        public Point? Reveal(bool animated)
        {
            if (_owner._stripScroller is not { } scroller)
                return null;
            if (Model.KeepInView(_owner.SelectedIndex, scroller.ScrollX, ViewportWidth) is not { } target)
                return new Point(scroller.ScrollX, scroller.ScrollY);
            _ = scroller.ScrollToAsync(target, 0, animated);
            return new Point(target, scroller.ScrollY);
        }

        private void KeepInView(int index, bool animated)
        {
            if (_owner._stripScroller is { } scroller && Model.KeepInView(index, scroller.ScrollX, ViewportWidth) is { } target)
                _ = scroller.ScrollToAsync(target, 0, animated);
        }

        internal override IEnumerable<ISkUiView> SkiaChildren
        {
            get
            {
                yield return Indicator;
                yield return Labels;
                foreach (var cell in _headerOrder)
                    yield return cell;
            }
        }

        protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
            new(Model.Measure(ViewportWidth), Math.Min(heightConstraint, Model.DesiredHeight));

        protected override void ArrangeContent(Size size)
        {
            Model.Arrange(ViewportWidth, size.Width, size.Height);
            _laidOut = true;
            var bounds = Model.IndicatorFrame(size.Width);
            ((IView)Indicator).Measure(bounds.Width, bounds.Height);
            ((IView)Indicator).Arrange(bounds);
            Indicator.InvalidateRender(SkUiRenderDirty.Props);
            ((IView)Labels).Measure(size.Width, size.Height);
            ((IView)Labels).Arrange(new Rect(0, 0, size.Width, size.Height));
            Labels.InvalidatePaint();
        }

        protected override void OnPaintContent(SKCanvas canvas) => Model.DrawBackground(canvas, (float)Width, (float)Height);

        protected override bool HandlesTap => Model.TakesInput;

        protected override void OnTapped(SkUiTappedEventArgs args)
        {
            RaiseTapped(args);
            Model.Tap(args.Position);
        }

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
    /// Hosts a tab's header view of the app's (1.18 T), over the indicator: its binding context is the tab item. Taps go through
    /// to the strip, keyboard focus does not go in, and screen readers read the strip's header element instead.
    /// </summary>
    private sealed class HeaderCell : SkUiContentView
    {
        public HeaderCell(SkUiTabItem tab)
        {
            Tab = tab;
            InputTransparent = true;
            AutomationProperties.SetExcludedWithChildren(this, true);
            BindingContext = tab;
        }

        public SkUiTabItem Tab { get; }

        /// <summary>The template the view was made from (a selector's choice).</summary>
        public DataTemplate? Template { get; set; }
    }

    /// <summary>The strip's headers, a layer of their own over the indicator; taps go through to the strip.</summary>
    private sealed class TabLabels : SkUiView
    {
        private readonly SkUiTabStripModel _model;

        public TabLabels(SkUiTabStripModel model)
        {
            _model = model;
            InputTransparent = true;
        }

        protected override void OnPaintContent(SKCanvas canvas) => _model.DrawHeaders(canvas);
    }

    /// <summary>The indicator: drawn once in its own bounds and moved with the panes on the render thread by its links (D8).</summary>
    private sealed class TabIndicator : SkUiView
    {
        private readonly SkUiTabView _owner;
        private readonly SkUiTabStripModel _model;

        public TabIndicator(SkUiTabView owner, SkUiTabStripModel model)
        {
            _owner = owner;
            _model = model;
            InputTransparent = true;
        }

        protected override void OnPaintContent(SKCanvas canvas) => _model.DrawIndicator(canvas, (float)Width, (float)Height);

        internal override void OnGetRenderProps(ref SkUiRenderProps props)
        {
            if (_owner._paneScroller is { } scroller)
                _model.ApplyLinks(ref props, ((ISkUiRenderable)scroller).RenderState.Node, _owner.PaneController, _owner.Panes.Engine, _owner.Panes.GeometryVersion);
        }

        internal override void ReleaseDrawingResources()
        {
            base.ReleaseDrawingResources();
            _model.ForgetLinks();
        }
    }
}
