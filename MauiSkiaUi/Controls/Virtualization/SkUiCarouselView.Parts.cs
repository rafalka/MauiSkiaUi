using System.Collections.ObjectModel;
using MauiSkiaUi.Rendering;

namespace MauiSkiaUi;

public partial class SkUiCarouselView
{
    /// <summary>The recycle key of items without a template (a default label).</summary>
    private static readonly object DefaultItemKey = new();

    /// <summary>The recycle key of an item: its (selected) template, or one key for default labels.</summary>
    private object ItemKey(object? item) =>
        (object?)(_itemTemplate is DataTemplateSelector selector ? selector.SelectTemplate(item, this) : _itemTemplate) ?? DefaultItemKey;

    private ItemCell CreateCell(object key)
    {
        var content = key is DataTemplate template
            ? template.CreateContent() as ISkUiView
              ?? throw new InvalidOperationException($"{nameof(ItemTemplate)} of {nameof(SkUiCarouselView)} must create drawn (SkUi*) views; put native views inside an SkUiMauiContentView.")
            : new SkUiLabel { HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center };
        return new ItemCell(this, key) { Content = content };
    }

    /// <summary>Shows the item at <paramref name="index"/> in <paramref name="cell"/>: binding context, default label text.</summary>
    private void BindCell(ItemCell cell, int index)
    {
        var item = ItemAt(index);
        cell.Index = index;
        cell.Item = item;
        cell.BindingContext = item;
        if (ReferenceEquals(cell.TemplateKey, DefaultItemKey) && cell.Content is SkUiLabel label)
            label.Text = item?.ToString() ?? string.Empty;
    }

    /// <summary>The carousel's scroller: once laid out, it moves to where the items' layout wants it (the current place kept).</summary>
    private sealed class CarouselScroller(SkUiCarouselView owner) : SkUiScrollView
    {
        protected override void ArrangeContent(Size size)
        {
            base.ArrangeContent(size);
            owner.Panel.ApplyPendingOffset();
        }
    }

    /// <summary>Hosts an item's view; placed by the carousel's item effect, if any, at composite time.</summary>
    private sealed class ItemCell(SkUiCarouselView owner, object templateKey) : SkUiContentView
    {
        private SkUiItemEffectLink? _link;

        /// <summary>The template (or default-label key) its view comes from.</summary>
        public object TemplateKey { get; } = templateKey;

        /// <summary>Its place in the strip of items (items repeat along it when the carousel loops).</summary>
        public int Slot;

        /// <summary>The index of the item it shows.</summary>
        public int Index = -1;

        /// <summary>The item it shows.</summary>
        public object? Item;

        /// <summary>The items version it was bound in (<see cref="ItemsPanel"/> rebinds cells of older versions).</summary>
        public int Version;

        /// <summary>The visual state its view was put in.</summary>
        public string? VisualState;

        /// <summary>Its drawing order at the current offset (with an effect).</summary>
        public double Depth;

        internal override void OnGetRenderProps(ref SkUiRenderProps props) => owner.Panel.ApplyEffect(this, ref props, ref _link);

        internal override void ReleaseDrawingResources()
        {
            base.ReleaseDrawingResources();
            _link = null;
        }
    }

    /// <summary>
    /// The strip of items inside the scroller: item views exist for the slots near the viewport only, recycled per template.
    /// Where the items go, looping included (the strip of copies and moving back to its middle copy by whole cycles), is the
    /// <see cref="SkUiCarouselEngine"/>'s; the panel realizes, recycles and places the views and drives the scroller.
    /// </summary>
    private sealed class ItemsPanel : SkUiView, ISkUiSnapPointSource, ISkUiScrollListener, ISkUiCarouselEngineHost
    {
        private readonly SkUiCarouselView _owner;
        private readonly SkUiCarouselEngine _engine;
        private readonly List<ItemCell> _realized = []; // contiguous slots, ascending
        private readonly List<ItemCell> _drawOrder = [];
        private readonly List<View> _visible = [];
        private readonly Dictionary<object, Stack<ItemCell>> _pool = [];
        private int _version;
        private double _arrangedCross = double.NaN;
        private bool _arranged;
        private bool _updating;
        private Point _lastOffset;

        public ItemsPanel(SkUiCarouselView owner)
        {
            _owner = owner;
            _engine = new SkUiCarouselEngine(this);
        }

        private bool Horizontal => _owner._horizontal;

        private bool Mirrored => _owner._horizontal && IsRightToLeft;

        bool ISkUiCarouselEngineHost.Loop => _owner._loop;

        bool ISkUiCarouselEngineHost.Horizontal => _owner._horizontal;

        bool ISkUiCarouselEngineHost.Mirrored => Mirrored;

        int ISkUiCarouselEngineHost.Position => _owner._position;

        public int Count => _engine.Count;

        /// <summary>Whether the items repeat without an end.</summary>
        public bool Loops => _engine.Loops;

        /// <summary>Whether the items are laid out (positions and offsets are known).</summary>
        public bool HasGeometry => _engine.HasGeometry;

        public (int First, int Last) RealizedSlots => _realized.Count == 0 ? (-1, -1) : (_realized[0].Slot, _realized[^1].Slot);

        public (double Extent, double Stride, int Slots, int Copies) GeometryInfo => (_engine.Extent, _engine.Stride, _engine.Slots, _engine.Copies);

        #region Geometry

        /// <summary>The scroller's offset along the axis.</summary>
        private double PhysicalOffset => Horizontal ? _owner.Controller.X : _owner.Controller.Y;

        /// <summary>The current logical offset: the one the next layout takes, or the scroller's.</summary>
        private double LogicalOffset => _engine.LogicalOffset(PhysicalOffset);

        /// <summary>The item at the snap point, fractional (see <see cref="SkUiCarouselView.ScrollPosition"/>).</summary>
        public double ScrollPosition => _engine.ScrollPosition(LogicalOffset);

        /// <summary>The page at the snap point, fractional (the indicator's position with several items per snap point).</summary>
        public double PagePosition => _engine.PagePosition(LogicalOffset);

        /// <summary>How far the strip is past the item at <paramref name="index"/>, in items (see <see cref="SkUiCarouselEngine.PastItem"/>).</summary>
        public double PastItem(int index) => _arranged ? _engine.PastItem(index, LogicalOffset) : 0;

        private SkUiCarouselItemMetrics Metrics => _engine.Metrics(_arrangedCross);

        /// <summary>The viewport along the axis offered by the carousel's layout.</summary>
        public void SetViewport(double viewport, bool arranging)
        {
            if (!(arranging ? _engine.SetViewport(viewport, LogicalOffset, _arranged) : _engine.OfferViewport(viewport, LogicalOffset, _arranged)))
                return;
            _disturbed |= _owner.Controller.IsMotionRunning;
            // The carousel measures (and arranges) its scroller right after: asking for another layout here would never settle
            // where nothing bounds the axis, as the measure and the arrange offer different viewports.
            ForgetMeasure();
            _owner.Scroller.ForgetMeasure();
        }

        /// <summary>How long the carousel asks to be along the axis when offered <paramref name="viewport"/>.</summary>
        public double MeasuredViewport(double viewport) => _engine.MeasuredViewport(viewport);

        /// <summary>The direction changed: the item at <paramref name="position"/> is placed again.</summary>
        public void Place(int position)
        {
            _owner.Controller.StopMotion();
            SetCount(_engine.Count, position);
        }

        /// <summary>The number of items changed (or the items did): the item position <paramref name="keep"/> shows once laid out.</summary>
        public void SetCount(int count, double keep)
        {
            _disturbed |= _owner.Controller.IsMotionRunning;
            _engine.SetCount(count, keep);
            _version++;
            if (count == 0)
                ReleaseAll();
            InvalidateMeasureOverride();
        }

        /// <summary>The items' layout changes next: the current place (the fractional item at the snap point) is kept.</summary>
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

        /// <summary>Lays the items out again (a setting or the template changed); their views are rebound.</summary>
        public void Refresh()
        {
            _version++;
            _engine.Invalidate();
            InvalidateMeasureOverride();
        }

        private void UpdateGeometry(double crossConstraint)
        {
            var layout = new SkUiCarouselLayout(_owner.PeekAreaInsets, _owner.LayoutItemSpacing, _owner.ItemExtent, _owner.LayoutSnapPointsAlignment, _owner._effect,
                _owner._perPage);
            if (_engine.UpdateGeometry(layout, crossConstraint, _owner._position))
                _version++; // cells show other slots now: placed again from scratch
        }

        /// <summary>Items that fill an unbounded carousel are as long as the first one measures.</summary>
        double ISkUiCarouselEngineHost.ProbeExtent(double cross)
        {
            var cell = TakeCell(0);
            var size = Horizontal ? ((IView)cell).Measure(double.PositiveInfinity, cross) : ((IView)cell).Measure(cross, double.PositiveInfinity);
            ReturnCell(cell);
            return Horizontal ? size.Width : size.Height;
        }

        #endregion

        #region Cells

        private ItemCell TakeCell(int index)
        {
            var key = _owner.ItemKey(_owner.ItemAt(index));
            var cell = _pool.TryGetValue(key, out var pooled) && pooled.TryPop(out var recycled) ? recycled : _owner.CreateCell(key);
            _owner.BindCell(cell, index);
            cell.Version = _version;
            return cell;
        }

        private void ReturnCell(ItemCell cell)
        {
            if (!_pool.TryGetValue(cell.TemplateKey, out var pooled))
                _pool[cell.TemplateKey] = pooled = new Stack<ItemCell>();
            if (pooled.Count < 16)
                pooled.Push(cell);
        }

        private ItemCell Realize(int slot)
        {
            var cell = TakeCell(_engine.IndexOfSlot(slot));
            cell.Slot = slot;
            cell.VisualState = null;
            AddLogicalChild(cell);
            MeasureCell(cell);
            UpdateVisualState(cell);
            return cell;
        }

        private void Release(int position)
        {
            var cell = _realized[position];
            _realized.RemoveAt(position);
            RemoveLogicalChild(cell);
            ReturnCell(cell);
        }

        /// <summary>Releases every item view (recycled).</summary>
        public void ReleaseAll()
        {
            if (_realized.Count == 0)
                return;
            for (var position = _realized.Count - 1; position >= 0; position--)
                Release(position);
            InvalidateRender(SkUiRenderDirty.Children);
            UpdateVisibleViews();
        }

        /// <summary>Drops the recycled views (their template or items changed).</summary>
        public void ClearPool()
        {
            foreach (var cells in _pool.Values)
                foreach (var cell in cells)
                    cell.BindingContext = null;
            _pool.Clear();
        }

        private void MeasureCell(ItemCell cell)
        {
            var cross = double.IsFinite(_arrangedCross) ? _arrangedCross : _engine.CrossConstraint;
            var extent = _engine.Extent;
            if (Horizontal)
                ((IView)cell).Measure(extent, cross);
            else
                ((IView)cell).Measure(cross, extent);
        }

        private void ArrangeCell(ItemCell cell)
        {
            // Logical: the layout mirrors frames in right-to-left layouts.
            var start = _engine.SlotStart(cell.Slot);
            var extent = _engine.Extent;
            ((IView)cell).Arrange(Horizontal ? new Rect(start, 0, extent, _arrangedCross) : new Rect(0, start, _arrangedCross, extent));
        }

        /// <summary>The slots that show (with one item of prefetch on either side, and the items an effect shows).</summary>
        private (int First, int Last) SlotWindow() =>
            _engine.SlotWindow(LogicalOffset, _owner._effect is { } effect ? effect.GetVisibleRange(Metrics) : 0);

        /// <summary>Realizes the slots that show and releases the others; rebinds cells whose item changed. Returns whether the cells changed.</summary>
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
            // Cells of an older layout or items show their slot's item again (kept when it is the same item and template).
            for (var position = 0; position < _realized.Count; position++)
            {
                var cell = _realized[position];
                if (cell.Version == _version)
                    continue;
                var index = _engine.IndexOfSlot(cell.Slot);
                var item = _owner.ItemAt(index);
                if (!Equals(_owner.ItemKey(item), cell.TemplateKey))
                {
                    Release(position);
                    _realized.Insert(position, Realize(cell.Slot));
                    changed = true;
                }
                else if (index != cell.Index || !ReferenceEquals(item, cell.Item))
                {
                    _owner.BindCell(cell, index);
                    cell.VisualState = null;
                    UpdateVisualState(cell);
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
            UpdateVisibleViews();
            return changed;
        }

        /// <summary>
        /// Brings the carousel's <see cref="SkUiCarouselView.VisibleViews"/> (once it was read) up to the item views in view, in
        /// strip order: with an item effect, the ones it draws (not transparent, moved and scaled into the viewport). The
        /// collection changes only when the set does.
        /// </summary>
        public void UpdateVisibleViews()
        {
            if (_owner._visibleViews is not { } views)
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
                    if (cell.Content is not View view)
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
                        _visible.Add(view);
                }
            }
            Sync(views, _visible);
            _visible.Clear();
        }

        /// <summary>Changes <paramref name="views"/> into <paramref name="target"/> by removing, moving and inserting only what differs.</summary>
        private static void Sync(ObservableCollection<View> views, List<View> target)
        {
            for (var index = 0; index < target.Count; index++)
            {
                while (index < views.Count && !target.Contains(views[index]))
                    views.RemoveAt(index);
                if (index < views.Count && ReferenceEquals(views[index], target[index]))
                    continue;
                var existing = -1;
                for (var later = index + 1; later < views.Count; later++)
                    if (ReferenceEquals(views[later], target[index]))
                    {
                        existing = later;
                        break;
                    }
                if (existing > index)
                    views.Move(existing, index);
                else
                    views.Insert(index, target[index]);
            }
            while (views.Count > target.Count)
                views.RemoveAt(views.Count - 1);
        }

        /// <summary>The view of the item at <paramref name="index"/> nearest to the snap point, or <c>null</c>.</summary>
        public ISkUiView? RealizedView(int index)
        {
            ItemCell? best = null;
            var center = HasGeometry ? _engine.FractionalSlot(LogicalOffset) : 0;
            foreach (var cell in _realized)
                if (cell.Index == index && (best is null || Math.Abs(cell.Slot - center) < Math.Abs(best.Slot - center)))
                    best = cell;
            return best?.Content;
        }

        /// <summary>The visual state the view of the item at <paramref name="index"/> nearest to the snap point is in (tests).</summary>
        public string? VisualStateOf(int index)
        {
            var view = RealizedView(index);
            foreach (var cell in _realized)
                if (ReferenceEquals(cell.Content, view))
                    return cell.VisualState;
            return null;
        }

        /// <summary>The visual states of the realized item views, with their items (tests).</summary>
        public IEnumerable<(int Index, string? State)> VisualStates => _realized.Select(static cell => (cell.Index, cell.VisualState));

        public void UpdateVisualStates()
        {
            foreach (var cell in _realized)
                UpdateVisualState(cell);
        }

        private void UpdateVisualState(ItemCell cell)
        {
            // By slot, not by item: a looping carousel can show copies of an item, and only the current item's copy nearest
            // to the snap point (and its neighbors) take the roles; the other copies are default items.
            var current = HasGeometry ? _engine.NearestSlotOf(_owner._position, LogicalOffset) : _owner._position;
            var state = (cell.Slot - current) switch
            {
                0 => CurrentItemVisualState,
                1 => NextItemVisualState,
                -1 => PreviousItemVisualState,
                _ => DefaultItemVisualState
            };
            if (ReferenceEquals(state, cell.VisualState))
                return;
            cell.VisualState = state;
            if (cell.Content is VisualElement view)
                VisualStateManager.GoToState(view, state);
        }

        #endregion

        #region Scrolling

        /// <summary>The item at the snap point (a page's first): <paramref name="keep"/> while its (clamped) snap point is where the strip is.</summary>
        public int CurrentIndex(int keep) => _engine.CurrentIndex(LogicalOffset, keep);

        /// <summary>The last item in view (not looping).</summary>
        public int LastVisibleIndex => _engine.LastVisibleIndex(LogicalOffset);

        /// <summary>Scrolls so the item at <paramref name="index"/> is at the snap point; before the layout, the layout places it.</summary>
        public Task ScrollToIndex(int index, bool animated, (int From, int To, bool Forward)? step)
        {
            var logical = LogicalOffset;
            if (_engine.DeferPlacement(index, logical, _arranged))
            {
                _owner.Controller.StopMotion(); // it would go on from the placement
                InvalidateMeasureOverride();
                return Task.CompletedTask;
            }
            var target = _engine.Physical(step is var (from, to, forward) && to == index ? _engine.StepOffset(from, to, forward, logical) : _engine.TargetOffset(index, logical));
            var controller = _owner.Controller;
            return Horizontal ? controller.ScrollToAsync(target, controller.Y, animated) : controller.ScrollToAsync(controller.X, target, animated);
        }

        /// <summary>After the scroller's arrange: it takes the offset the layout wants (the kept place).</summary>
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
                InvalidateMeasureFromChild(); // the scroller's extent comes back with the next measure
            }
            else
            {
                controller.SetOffset(Horizontal ? target : controller.X, Horizontal ? controller.Y : target);
            }
        }

        void ISkUiScrollListener.OnAncestorScrollChanged() => Update();

        private void Update()
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

        /// <summary>Looping: once the strip is far from its middle copy, moves it back by whole cycles (what shows stays the same).</summary>
        private void Recenter()
        {
            var cycles = _engine.RecenterCycles(LogicalOffset, out var physical);
            if (cycles == 0)
                return;
            // The cells move with their slots: each keeps its item (slot mod count) and only moves in the strip.
            var slots = cycles * _engine.Count;
            foreach (var cell in _realized)
                cell.Slot -= slots;
            _owner.Controller.CorrectOffset(Horizontal ? physical : 0, Horizontal ? 0 : physical);
            InvalidateMeasureFromChild(); // the scroller's extent comes back with the next measure
            InvalidateRender(SkUiRenderDirty.Children);
        }

        /// <summary>The scrolled-event arguments for the current offset.</summary>
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

        /// <summary>The carousel's effect (or its settings) changed: the items are placed and drawn again.</summary>
        public void OnEffectChanged()
        {
            KeepCurrentPlace();
            foreach (var cell in _realized)
                cell.InvalidateRender(SkUiRenderDirty.Props);
            InvalidateRender(SkUiRenderDirty.Props | SkUiRenderDirty.Children);
            InvalidateMeasureOverride();
        }

        /// <summary>Places <paramref name="cell"/> by the effect (UI side: for hit-testing and immediate painting) and links it for the compositor.</summary>
        public void ApplyEffect(ItemCell cell, ref SkUiRenderProps props, ref SkUiItemEffectLink? link)
        {
            if (_owner._effect is not { } effect || !HasGeometry)
            {
                link = null;
                return;
            }
            var source = ((ISkUiRenderable)_owner.Scroller).RenderState.Node;
            var (factor, @base) = _engine.EffectLinkTerms(cell.Slot);
            var metrics = Metrics;
            // Exact: a link is immutable once committed; any change of its numbers makes a new one.
            if (link is null || !ReferenceEquals(link.Source, source) || !ReferenceEquals(link.Effect, effect)
                || link.Factor != factor || link.Base != @base || link.Metrics != metrics || link.Horizontal != Horizontal)
                link = new SkUiItemEffectLink(source, Horizontal, factor, @base, effect, metrics);
            props.ItemEffect = link;
            var offset = _owner.Scroller.VisualScrollOffset;
            link.Apply(ref props, (float)offset.X, (float)offset.Y);
        }

        #endregion

        #region Layout

        internal override IEnumerable<ISkUiView> SkiaChildren => _realized;

        /// <inheritdoc />
        /// <remarks>With an effect, nearer items are hit (and drawn) over farther ones: back to front by their depth at the current offset.</remarks>
        internal override void AddRenderChildren(List<ISkUiRenderable> children)
        {
            if (_owner._effect is not { } effect || !HasGeometry || _realized.Count < 2)
            {
                foreach (var cell in _realized)
                    children.Add(cell);
                return;
            }
            var offset = _owner.Scroller.VisualScrollOffset;
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
                var desired = ((IView)cell).DesiredSize;
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
            if (cross != _arrangedCross)
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
