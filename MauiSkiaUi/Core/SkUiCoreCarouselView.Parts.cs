using MauiSkiaUi.Rendering;

namespace MauiSkiaUi.Core;

public partial class SkUiCoreCarouselView
{
    /// <summary>The carousel's scroller: once laid out, it moves to where the items' layout wants it (the current place kept).</summary>
    private sealed class CarouselScroller(SkUiCoreCarouselView owner) : SkUiCoreScrollView
    {
        protected override void ArrangeContent(Size size)
        {
            base.ArrangeContent(size);
            owner._panel.ApplyPendingOffset();
        }

        private protected override void OnScrolled()
        {
            owner._panel.Update();
            owner.OnScrolled();
        }

        internal override void OnPointerOverChanged(bool isOver)
        {
            base.OnPointerOverChanged(isOver);
            owner.OnScrollerHovered(isOver);
        }
    }

    /// <summary>Hosts an item's node; placed by the carousel's item effect, if any, at composite time.</summary>
    private sealed class ItemCell(SkUiCoreCarouselView owner, object key) : SkUiCoreContentView
    {
        private SkUiItemEffectLink? _link;

        /// <summary>The recycling key its node was made for.</summary>
        public object Key { get; } = key;

        /// <summary>Its place in the strip of items (items repeat along it when the carousel loops).</summary>
        public int Slot;

        /// <summary>The index of the item it shows.</summary>
        public int Index = -1;

        /// <summary>The item it shows.</summary>
        public object? Item;

        /// <summary>The items version it was bound in (the panel rebinds cells of older versions).</summary>
        public int Version;

        /// <summary>Its drawing order at the current offset (with an effect).</summary>
        public double Depth;

        internal override void OnGetRenderProps(ref SkUiRenderProps props)
        {
            base.OnGetRenderProps(ref props);
            owner._panel.ApplyEffect(this, ref props, ref _link);
        }

        internal override void ReleaseDrawingResources()
        {
            base.ReleaseDrawingResources();
            _link = null;
        }
    }

    /// <summary>
    /// The strip of items inside the scroller (as <see cref="SkUiCarouselView"/>'s panel): item nodes exist for the slots near
    /// the viewport only, recycled per key. Where the items go is the <see cref="SkUiCarouselEngine"/>'s.
    /// </summary>
    private sealed class ItemsPanel : SkUiCoreNode, ISkUiSnapPointSource, ISkUiCarouselEngineHost
    {
        private readonly SkUiCoreCarouselView _owner;
        private readonly SkUiCarouselEngine _engine;
        private readonly List<ItemCell> _realized = []; // contiguous slots, ascending
        private readonly List<ItemCell> _drawOrder = [];
        private readonly List<SkUiCoreNode> _visible = [];
        private readonly Dictionary<object, Stack<ItemCell>> _pool = [];
        private int _version;
        private double _arrangedCross = double.NaN;
        private bool _arranged;
        private bool _updating;
        private Point _lastOffset;

        public ItemsPanel(SkUiCoreCarouselView owner)
        {
            _owner = owner;
            _engine = new SkUiCarouselEngine(this);
        }

        private bool Horizontal => _owner._horizontal;

        private bool Mirrored => _owner._horizontal && _owner.IsRightToLeft;

        bool ISkUiCarouselEngineHost.Loop => _owner._loop;

        bool ISkUiCarouselEngineHost.Horizontal => _owner._horizontal;

        bool ISkUiCarouselEngineHost.Mirrored => Mirrored;

        int ISkUiCarouselEngineHost.Position => _owner._position;

        public int Count => _engine.Count;

        public bool Loops => _engine.Loops;

        public bool HasGeometry => _engine.HasGeometry;

        /// <summary>Item nodes made by the factory so far (tests).</summary>
        public int Created { get; private set; }

        public (int First, int Last) RealizedSlots => _realized.Count == 0 ? (-1, -1) : (_realized[0].Slot, _realized[^1].Slot);

        public (double Extent, double Stride, int Slots, int Copies) GeometryInfo => (_engine.Extent, _engine.Stride, _engine.Slots, _engine.Copies);

        #region Geometry

        private double PhysicalOffset => Horizontal ? _owner.Controller.X : _owner.Controller.Y;

        private double LogicalOffset => _engine.LogicalOffset(PhysicalOffset);

        public double ScrollPosition => _engine.ScrollPosition(LogicalOffset);

        public double PagePosition => _engine.PagePosition(LogicalOffset);

        public double PastItem(int index) => _arranged ? _engine.PastItem(index, LogicalOffset) : 0;

        private SkUiCarouselItemMetrics Metrics => _engine.Metrics(_arrangedCross);

        public void SetViewport(double viewport)
        {
            if (!_engine.SetViewport(viewport, LogicalOffset, _arranged))
                return;
            _disturbed |= _owner.Controller.IsMotionRunning;
            // The carousel measures (and arranges) its scroller right after: asking for another layout here would never settle
            // where nothing bounds the axis, as the measure and the arrange offer different viewports.
            ForgetMeasure();
            _owner._scroller.ForgetMeasure();
        }

        /// <summary>
        /// A measure offers <paramref name="viewport"/> along the axis; returns the length to ask for: that, or what the items take
        /// when nothing bounds it (the strip stays laid out for the viewport it is arranged in).
        /// </summary>
        public double Offer(double viewport, double cross)
        {
            if (double.IsFinite(viewport))
            {
                SetViewport(viewport);
                return viewport;
            }
            var asked = _engine.UnboundedViewport(CurrentLayout, cross);
            if (!_arranged)
                SetViewport(asked);
            return asked;
        }

        /// <summary>The direction changed: the item at <paramref name="position"/> is placed again.</summary>
        public void Place(int position)
        {
            _owner.Controller.StopMotion();
            SetCount(_engine.Count, position);
        }

        public void SetCount(int count, double keep)
        {
            _disturbed |= _owner.Controller.IsMotionRunning;
            _engine.SetCount(count, keep);
            _version++;
            if (count == 0)
                ReleaseAll();
            InvalidateMeasure();
        }

        public void KeepCurrentPlace()
        {
            _disturbed |= _owner.Controller.IsMotionRunning;
            _engine.KeepCurrentPlace(LogicalOffset, _arranged);
        }

        /// <summary>
        /// Whether the layout changed under a running motion since the last rest (read once): it may stop short of a snap point
        /// then (the offset clamped, the items' lengths changed), and the strip goes on to one.
        /// </summary>
        public bool TakeDisturbed()
        {
            var disturbed = _disturbed;
            _disturbed = false;
            return disturbed;
        }

        private bool _disturbed;

        /// <summary>Whether the item at <paramref name="index"/> is at the snap point (or will be, once laid out).</summary>
        public bool RestsOn(int index) => _engine.HasPendingOffset
            || Math.Abs(_engine.TargetOffset(index, LogicalOffset) - LogicalOffset) < SkUiCarouselEngine.VisibilityTolerance;

        public void Refresh()
        {
            _version++;
            _engine.Invalidate();
            InvalidateMeasure();
        }

        private SkUiCarouselLayout CurrentLayout => new(_owner._peekAreaInsets, _owner._itemSpacing, _owner._itemExtent, _owner._snapPointsAlignment, _owner._effect,
            _owner._perPage);

        private void UpdateGeometry(double crossConstraint)
        {
            if (_engine.UpdateGeometry(CurrentLayout, crossConstraint, _owner._position))
                _version++; // cells show other slots now: placed again from scratch
        }

        double ISkUiCarouselEngineHost.ProbeExtent(double cross)
        {
            // The first item's own node while it shows (it may have changed since), else one of its kind.
            foreach (var shown in _realized)
                if (shown.Index == 0)
                {
                    var length = Probe(shown);
                    MeasureCell(shown);
                    return length;
                }
            var cell = TakeCell(0);
            var probed = Probe(cell);
            ReturnCell(cell);
            return probed;

            double Probe(ItemCell item) =>
                Horizontal ? item.Measure(double.PositiveInfinity, cross).Width : item.Measure(cross, double.PositiveInfinity).Height;
        }

        #endregion

        #region Cells

        private ItemCell TakeCell(int index)
        {
            var item = _owner.ItemAt(index);
            var key = _owner.ItemKey(item);
            ItemCell cell;
            if (_pool.TryGetValue(key, out var pooled) && pooled.TryPop(out var recycled))
            {
                cell = recycled;
            }
            else
            {
                cell = new ItemCell(_owner, key);
                cell.SetContent(_owner.CreateItemNode(item));
                Created++;
            }
            Bind(cell, index);
            cell.Version = _version;
            return cell;
        }

        private void Bind(ItemCell cell, int index)
        {
            var item = _owner.ItemAt(index);
            cell.Index = index;
            cell.Item = item;
            if (cell.Content is { } node)
                _owner.BindItemNode(node, item);
        }

        private void ReturnCell(ItemCell cell)
        {
            if (!_pool.TryGetValue(cell.Key, out var pooled))
                _pool[cell.Key] = pooled = new Stack<ItemCell>();
            if (pooled.Count < 16)
                pooled.Push(cell);
        }

        private ItemCell Realize(int slot)
        {
            var cell = TakeCell(_engine.IndexOfSlot(slot));
            cell.Slot = slot;
            cell.AttachTo(this);
            MeasureCell(cell);
            return cell;
        }

        private void Release(int position)
        {
            var cell = _realized[position];
            _realized.RemoveAt(position);
            cell.AttachTo(null);
            ReturnCell(cell);
        }

        public void ReleaseAll()
        {
            if (_realized.Count == 0)
                return;
            for (var position = _realized.Count - 1; position >= 0; position--)
                Release(position);
            InvalidateRender(SkUiRenderDirty.Children);
            UpdateVisibleNodes();
        }

        public void ClearPool() => _pool.Clear();

        private void MeasureCell(ItemCell cell)
        {
            var cross = double.IsFinite(_arrangedCross) ? _arrangedCross : _engine.CrossConstraint;
            var extent = _engine.Extent;
            if (Horizontal)
                cell.Measure(extent, cross);
            else
                cell.Measure(cross, extent);
        }

        private void ArrangeCell(ItemCell cell)
        {
            // Logical: Core arrange mirrors frames in right-to-left layouts.
            var start = _engine.SlotStart(cell.Slot);
            var extent = _engine.Extent;
            cell.Arrange(Horizontal ? new Rect(start, 0, extent, _arrangedCross) : new Rect(0, start, _arrangedCross, extent));
        }

        private (int First, int Last) SlotWindow() =>
            _engine.SlotWindow(LogicalOffset, _owner._effect is { } effect ? effect.GetVisibleRange(Metrics) : 0);

        private bool RealizeWindow(bool arrange)
        {
            if (!HasGeometry || !double.IsFinite(_engine.ViewportLength) || _engine.ViewportLength <= 0)
            {
                var had = _realized.Count > 0;
                ReleaseAll();
                return had;
            }
            var (first, last) = SlotWindow();
            var changed = false;
            while (_realized.Count > 0 && (_realized[0].Slot < first || _realized[0].Slot > last))
            {
                Release(0);
                changed = true;
            }
            while (_realized.Count > 0 && (_realized[^1].Slot > last || _realized[^1].Slot < first))
            {
                Release(_realized.Count - 1);
                changed = true;
            }
            // Cells of an older layout or items show their slot's item again (kept when it is the same item and key).
            for (var position = 0; position < _realized.Count; position++)
            {
                var cell = _realized[position];
                if (cell.Version == _version)
                    continue;
                var index = _engine.IndexOfSlot(cell.Slot);
                var item = _owner.ItemAt(index);
                if (!Equals(_owner.ItemKey(item), cell.Key))
                {
                    Release(position);
                    _realized.Insert(position, Realize(cell.Slot));
                    changed = true;
                }
                else if (index != cell.Index || !ReferenceEquals(item, cell.Item))
                {
                    Bind(cell, index);
                }
                _realized[position].Version = _version;
                MeasureCell(_realized[position]);
            }
            if (_realized.Count == 0)
            {
                for (var slot = first; slot <= last; slot++)
                    _realized.Add(Realize(slot));
                changed |= last >= first;
            }
            else
            {
                for (var slot = _realized[0].Slot - 1; slot >= first; slot--)
                {
                    _realized.Insert(0, Realize(slot));
                    changed = true;
                }
                for (var slot = _realized[^1].Slot + 1; slot <= last; slot++)
                {
                    _realized.Add(Realize(slot));
                    changed = true;
                }
            }
            if (arrange)
                foreach (var cell in _realized)
                    ArrangeCell(cell);
            if (changed)
                InvalidateRender(SkUiRenderDirty.Children);
            UpdateVisibleNodes();
            return changed;
        }

        /// <summary>
        /// Brings the carousel's <see cref="SkUiCoreCarouselView.VisibleNodes"/> (once it is read) up to the item nodes in view,
        /// in strip order; raises the change event only when the set changed.
        /// </summary>
        public void UpdateVisibleNodes()
        {
            if (_owner.TrackedVisibleNodes is not { } nodes)
                return;
            _visible.Clear();
            if (HasGeometry)
            {
                var logical = LogicalOffset;
                var effect = _owner._effect;
                var physical = _engine.Physical(logical);
                var metrics = Metrics;
                foreach (var cell in _realized)
                {
                    if (cell.Content is not { } node)
                        continue;
                    bool show;
                    if (effect is null)
                    {
                        show = _engine.IsSlotInView(cell.Slot, logical);
                    }
                    else
                    {
                        var transform = effect.GetItemTransform(_engine.ItemPosition(cell.Slot, physical), metrics);
                        show = transform.Opacity > 0 && _engine.IsSlotInView(cell.Slot, logical, transform.Translation, transform.Scale);
                    }
                    if (show)
                        _visible.Add(node);
                }
            }
            var same = nodes.Count == _visible.Count;
            for (var index = 0; same && index < nodes.Count; index++)
                same = ReferenceEquals(nodes[index], _visible[index]);
            if (!same)
            {
                nodes.Clear();
                nodes.AddRange(_visible);
            }
            _visible.Clear();
            if (!same)
                _owner.RaiseVisibleNodesChanged();
        }

        /// <summary>The node of the item at <paramref name="index"/> nearest to the snap point, or <c>null</c>.</summary>
        public SkUiCoreNode? RealizedNode(int index)
        {
            ItemCell? best = null;
            var center = HasGeometry ? _engine.FractionalSlot(LogicalOffset) : 0;
            foreach (var cell in _realized)
                if (cell.Index == index && (best is null || Math.Abs(cell.Slot - center) < Math.Abs(best.Slot - center)))
                    best = cell;
            return best?.Content;
        }

        #endregion

        #region Scrolling

        public int CurrentIndex(int keep) => _engine.CurrentIndex(LogicalOffset, keep);

        public int LastVisibleIndex => _engine.LastVisibleIndex(LogicalOffset);

        public Task ScrollToIndex(int index, bool animated, (int From, int To, bool Forward)? step)
        {
            var logical = LogicalOffset;
            if (_engine.DeferPlacement(index, logical, _arranged))
            {
                _owner.Controller.StopMotion(); // it would go on from the placement
                InvalidateMeasure();
                return Task.CompletedTask;
            }
            var target = _engine.Physical(step is var (from, to, forward) && to == index ? _engine.StepOffset(from, to, forward, logical) : _engine.TargetOffset(index, logical));
            var controller = _owner.Controller;
            return Horizontal ? controller.ScrollToAsync(target, controller.Y, animated) : controller.ScrollToAsync(controller.X, target, animated);
        }

        public void ApplyPendingOffset()
        {
            if (_engine.TakePendingOffset() is not { } target)
                return;
            var controller = _owner.Controller;
            var delta = target - PhysicalOffset;
            if (Math.Abs(delta) < 0.01)
            {
                Update();
                _owner.OnLayoutScrolled();
                return;
            }
            if (controller.IsMotionRunning || controller.Dragging)
            {
                // Moved, not set: a running fling or a drag goes on from the new offset.
                controller.CorrectOffset(Horizontal ? delta : 0, Horizontal ? 0 : delta);
                InvalidateMeasure(); // the scroller's extent comes back with the next measure
            }
            else
            {
                controller.SetOffset(Horizontal ? target : controller.X, Horizontal ? controller.Y : target);
            }
        }

        /// <summary>The scroller's offset changed: move back by whole cycles if far from the middle, and realize what shows.</summary>
        public void Update()
        {
            if (_updating || !_arranged)
                return;
            _updating = true;
            try
            {
                Recenter();
                RealizeWindow(arrange: true);
            }
            finally
            {
                _updating = false;
            }
        }

        private void Recenter()
        {
            var cycles = _engine.RecenterCycles(LogicalOffset, out var physical);
            if (cycles == 0)
                return;
            var slots = cycles * _engine.Count;
            foreach (var cell in _realized)
                cell.Slot -= slots;
            _owner.Controller.CorrectOffset(Horizontal ? physical : 0, Horizontal ? 0 : physical);
            InvalidateMeasure();
            InvalidateRender(SkUiRenderDirty.Children);
        }

        public ItemsViewScrolledEventArgs CreateScrolledArgs()
        {
            var controller = _owner.Controller;
            var offset = new Point(controller.X, controller.Y);
            var delta = offset - _lastOffset;
            _lastOffset = offset;
            var (first, last) = _engine.VisibleSlots(LogicalOffset);
            return new ItemsViewScrolledEventArgs
            {
                HorizontalDelta = delta.Width,
                VerticalDelta = delta.Height,
                HorizontalOffset = offset.X,
                VerticalOffset = offset.Y,
                FirstVisibleItemIndex = _engine.IndexOfSlot(first),
                CenterItemIndex = CurrentIndex(_owner._position),
                LastVisibleItemIndex = _engine.IndexOfSlot(last)
            };
        }

        void ISkUiSnapPointSource.GetSnapOffsets(bool horizontal, double from, double to, List<double> points)
        {
            if (horizontal == Horizontal)
                _engine.GetSnapOffsets(from, to, points);
        }

        #endregion

        #region Effects

        public void OnEffectChanged()
        {
            KeepCurrentPlace();
            foreach (var cell in _realized)
                cell.InvalidateRender(SkUiRenderDirty.Props);
            InvalidateRender(SkUiRenderDirty.Props | SkUiRenderDirty.Children);
            InvalidateMeasure();
        }

        public void ApplyEffect(ItemCell cell, ref SkUiRenderProps props, ref SkUiItemEffectLink? link)
        {
            if (_owner._effect is not { } effect || !HasGeometry)
            {
                link = null;
                return;
            }
            var source = ((ISkUiRenderable)_owner._scroller).RenderState.Node;
            var (factor, @base) = _engine.EffectLinkTerms(cell.Slot);
            var metrics = Metrics;
            // Exact: a link is immutable once committed; any change of its numbers makes a new one.
            if (link is null || !ReferenceEquals(link.Source, source) || !ReferenceEquals(link.Effect, effect)
                || link.Factor != factor || link.Base != @base || link.Metrics != metrics || link.Horizontal != Horizontal)
                link = new SkUiItemEffectLink(source, Horizontal, factor, @base, effect, metrics);
            props.ItemEffect = link;
            var offset = _owner.Controller.ChildrenOffset;
            link.Apply(ref props, (float)offset.X, (float)offset.Y);
        }

        #endregion

        #region Layout

        internal override IReadOnlyList<IVisualTreeElement> VisualChildren => _realized;

        internal override void AddRenderChildren(List<ISkUiRenderable> children)
        {
            if (_owner._effect is not { } effect || !HasGeometry || _realized.Count < 2)
            {
                foreach (var cell in _realized)
                    children.Add(cell);
                return;
            }
            var offset = _owner.Controller.ChildrenOffset;
            var physical = Horizontal ? offset.X : offset.Y;
            var metrics = Metrics;
            _drawOrder.Clear();
            foreach (var cell in _realized)
            {
                cell.Depth = effect.GetItemTransform(_engine.ItemPosition(cell.Slot, physical), metrics).ZIndex;
                _drawOrder.Add(cell);
            }
            _drawOrder.Sort(static (a, b) => a.Depth != b.Depth ? a.Depth.CompareTo(b.Depth) : a.Slot.CompareTo(b.Slot));
            foreach (var cell in _drawOrder)
                children.Add(cell);
            _drawOrder.Clear();
        }

        internal override void OnGetRenderProps(ref SkUiRenderProps props) => props.SortsChildrenByDepth = _owner._effect is not null;

        protected override Size MeasureContent(double widthConstraint, double heightConstraint)
        {
            var cross = Horizontal ? heightConstraint : widthConstraint;
            if (_engine.NeedsLayout(cross))
                UpdateGeometry(cross);
            RealizeWindow(arrange: false);
            var across = 0d;
            foreach (var cell in _realized)
            {
                MeasureCell(cell);
                var desired = cell.DesiredSize;
                across = Math.Max(across, Horizontal ? desired.Height : desired.Width);
            }
            if (double.IsFinite(cross))
                across = Math.Min(across, cross);
            var length = _engine.Length;
            return Horizontal ? new Size(length, across) : new Size(across, length);
        }

        protected override void ArrangeContent(Size size)
        {
            var cross = Horizontal ? size.Height : size.Width;
            if (cross != _arrangedCross) // exact: an arranged size
            {
                _arrangedCross = cross;
                foreach (var cell in _realized)
                    MeasureCell(cell);
            }
            _arranged = true;
            _updating = true;
            try
            {
                RealizeWindow(arrange: true);
            }
            finally
            {
                _updating = false;
            }
        }

        #endregion
    }
}
