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
            owner._panel.ApplyPendingOffset();
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

        internal override void OnGetRenderProps(ref SkUiRenderProps props) => owner._panel.ApplyEffect(this, ref props, ref _link);

        internal override void ReleaseDrawingResources()
        {
            base.ReleaseDrawingResources();
            _link = null;
        }
    }

    /// <summary>
    /// The strip of items inside the scroller: item views exist for the slots near the viewport only, recycled per template.
    /// A looping carousel repeats the items (<c>slot mod count</c>) along a strip of several copies and moves the offset back
    /// to the middle copy by whole cycles as it scrolls (scroll corrections, which running flings and snaps follow). All
    /// positions are worked out in logical offsets (from the start of the items; mirrored from the physical offset in
    /// right-to-left layouts).
    /// </summary>
    private sealed class ItemsPanel(SkUiCarouselView owner) : SkUiView, ISkUiSnapPointSource, ISkUiScrollListener
    {
        /// <summary>How far from the middle copy (in cycles of all items) a looping carousel goes before it moves back.</summary>
        private const double RecenterDistance = 0.75;

        /// <summary>How many points one snap query lists at most.</summary>
        private const int MaxSnapPoints = 4096;

        private readonly List<ItemCell> _realized = []; // contiguous slots, ascending
        private readonly List<ItemCell> _drawOrder = [];
        private readonly Dictionary<object, Stack<ItemCell>> _pool = [];
        private int _count;
        private int _version;
        private double _viewport = double.NaN; // offered along the axis (infinite: unbounded)
        private double _viewportLength;
        private double _crossConstraint = double.PositiveInfinity;
        private double _arrangedCross = double.NaN;
        private double _extent;
        private double _stride;
        private double _spacing;
        private double _padStart;
        private double _padEnd;
        private double _align;
        private double _length;
        private int _slots;
        private int _copies = 1;
        private bool _geometryValid;
        private bool _arranged;
        private bool _updating;
        private bool _placeRequested = true; // the first layout places the current item
        private double? _keep;               // the item position to show once the layout changed
        private double? _pendingLogical;     // the offset the scroller takes after its next arrange
        private Point _lastOffset;

        private bool Horizontal => owner._horizontal;

        private bool Mirrored => owner._horizontal && IsRightToLeft;

        public int Count => _count;

        /// <summary>Whether the items repeat without an end.</summary>
        public bool Loops => owner._loop && _count > 1;

        /// <summary>Whether the items are laid out (positions and offsets are known).</summary>
        public bool HasGeometry => _geometryValid && _stride > 0 && _count > 0 && _slots > 0;

        /// <summary>The viewport along the axis (the offered one, or an item and the peek insets when unbounded).</summary>
        public double ViewportLength => _viewportLength;

        public (int First, int Last) RealizedSlots => _realized.Count == 0 ? (-1, -1) : (_realized[0].Slot, _realized[^1].Slot);

        public (double Extent, double Stride, int Slots, int Copies) GeometryInfo => (_extent, _stride, _slots, _copies);

        #region Geometry

        /// <summary>The largest logical offset.</summary>
        private double MaxLogical => Math.Max(0, _length - _viewportLength);

        /// <summary>Where the middle copy of the items starts (looping).</summary>
        private int HomeSlot => _copies / 2 * _count;

        /// <summary>The scroller's offset along the axis.</summary>
        private double PhysicalOffset => Horizontal ? owner.Controller.X : owner.Controller.Y;

        /// <summary>The current logical offset: the one the next layout takes, or the scroller's.</summary>
        private double LogicalOffset => _pendingLogical ?? Logical(PhysicalOffset);

        private double Logical(double physical) => Mirrored ? MaxLogical - physical : physical;

        private double Physical(double logical) => Mirrored ? MaxLogical - logical : logical;

        /// <summary>The logical offset that lines slot <paramref name="slot"/> up at the snap point (not clamped).</summary>
        private double SnapOffset(int slot) => slot * _stride - _align;

        /// <summary><see cref="SnapOffset"/> within the scroll range (a strip that does not loop cannot pass its ends).</summary>
        private double ClampedSnapOffset(int slot) => Math.Clamp(SnapOffset(slot), 0, MaxLogical);

        /// <summary>The slot at the snap point, fractional while scrolling.</summary>
        private double FractionalSlot => (LogicalOffset + _align) / _stride;

        /// <summary>The item at the snap point, fractional (see <see cref="SkUiCarouselView.ScrollPosition"/>).</summary>
        public double ScrollPosition => Loops ? Mod(FractionalSlot, _count) : Math.Clamp(FractionalSlot, 0, _count - 1);

        /// <summary>How far the strip is past the nearest snap point, in items (-0.5 to 0.5).</summary>
        public double WithinItem => HasGeometry ? FractionalSlot - NearestSlot : 0;

        private int IndexOfSlot(int slot) => Loops ? (int)Mod(slot, _count) : slot;

        /// <summary>
        /// The slot nearest to the snap point. Halves round up (to the next slot), the same way in both directions: banker's
        /// rounding would round half-item offsets alternately down and up.
        /// </summary>
        private int NearestSlot => (int)Math.Floor(FractionalSlot + 0.5);

        private static double Mod(double value, double count) => (value % count + count) % count;

        private SkUiCarouselItemMetrics Metrics => new(_extent, double.IsFinite(_arrangedCross) ? _arrangedCross : 0, _spacing, _viewportLength, Horizontal);

        /// <summary>The viewport along the axis offered by the carousel's layout.</summary>
        public void SetViewport(double viewport)
        {
            // Exact: a layout offering the same length needs nothing (NaN before the first).
            if (viewport == _viewport || (double.IsInfinity(viewport) && double.IsInfinity(_viewport)))
                return;
            KeepCurrentPlace();
            _viewport = viewport;
            InvalidateMeasureOverride();
        }

        /// <summary>The number of items changed (or the items did): the item position <paramref name="keep"/> shows once laid out.</summary>
        public void SetCount(int count, double keep)
        {
            _count = count;
            _version++;
            _keep = count > 0 ? keep : null;
            _placeRequested = true;
            _geometryValid = false;
            if (count == 0)
                ReleaseAll();
            InvalidateMeasureOverride();
        }

        /// <summary>The items' layout changes next: the current place (the fractional item at the snap point) is kept.</summary>
        public void KeepCurrentPlace()
        {
            if (HasGeometry && _arranged)
                _keep ??= Loops ? Mod(FractionalSlot, _count) : FractionalSlot;
            _placeRequested = true;
            _geometryValid = false;
        }

        /// <summary>Lays the items out again (a setting or the template changed); their views are rebound.</summary>
        public void Refresh()
        {
            _version++;
            _geometryValid = false;
            InvalidateMeasureOverride();
        }

        private void UpdateGeometry(double crossConstraint)
        {
            _crossConstraint = crossConstraint;
            var insets = owner.PeekAreaInsets;
            var (start, end) = Horizontal ? (Mirrored ? (insets.Right, insets.Left) : (insets.Left, insets.Right)) : (insets.Top, insets.Bottom);
            var spacing = owner.ItemSpacing;
            var itemExtent = owner.ItemExtent;
            var extent = itemExtent > 0 ? itemExtent
                : double.IsFinite(_viewport) ? Math.Max(0, _viewport - start - end)
                : ProbeExtent(crossConstraint);
            var viewport = double.IsFinite(_viewport) ? _viewport : extent + start + end;
            var stride = extent + spacing;
            var inner = viewport - start - end;
            var align = owner.SnapPointsAlignment switch
            {
                SnapPointsAlignment.Center => (inner - extent) / 2,
                SnapPointsAlignment.End => inner - extent,
                _ => 0
            };
            var copies = 1;
            if (Loops && stride > 0)
            {
                // Room on either side of the middle copy for the drift before moving back, the longest fling, the viewport
                // and the items an effect shows.
                var cycle = _count * stride;
                var range = owner._effect?.GetVisibleRange(new SkUiCarouselItemMetrics(extent, 0, spacing, viewport, Horizontal)) ?? 0;
                var reach = RecenterDistance * cycle + SkUiGestureSettings.FlingMaximumVelocity * SkUiRenderFling.TotalTravelPerVelocity
                    + viewport + (2 + range) * stride;
                copies = 2 * (int)Math.Ceiling(reach / cycle) + 1;
            }
            var slots = stride > 0 ? _count * copies : 0;
            var length = slots > 0 ? start + slots * stride - spacing + end : start + end;
            // Exact: any change of the layout's numbers lays the items out again from the kept place.
            var changed = !_geometryValid && (extent != _extent || stride != _stride || align != _align || copies != _copies || slots != _slots
                || start != _padStart || end != _padEnd || viewport != _viewportLength || length != _length);
            (_extent, _stride, _spacing, _align, _copies, _slots, _padStart, _padEnd, _viewportLength, _length) =
                (extent, stride, spacing, align, copies, slots, start, end, viewport, length);
            _geometryValid = true;
            if ((changed || _placeRequested || _keep is not null) && HasGeometry)
            {
                var place = _keep ?? Math.Min(owner._position, _count - 1);
                _pendingLogical = Loops ? SnapOffset(HomeSlot) + place * _stride : Math.Clamp(place * _stride - _align, 0, MaxLogical);
                // Cells show other slots now: placed again from scratch.
                _version++;
            }
            _keep = null;
            _placeRequested = false;
        }

        /// <summary>Items that fill an unbounded carousel are as long as the first one measures.</summary>
        private double ProbeExtent(double cross)
        {
            if (_count == 0)
                return 0;
            var cell = TakeCell(0);
            var size = Horizontal ? ((IView)cell).Measure(double.PositiveInfinity, cross) : ((IView)cell).Measure(cross, double.PositiveInfinity);
            ReturnCell(cell);
            return Horizontal ? size.Width : size.Height;
        }

        #endregion

        #region Cells

        private ItemCell TakeCell(int index)
        {
            var key = owner.ItemKey(owner.ItemAt(index));
            var cell = _pool.TryGetValue(key, out var pooled) && pooled.TryPop(out var recycled) ? recycled : owner.CreateCell(key);
            owner.BindCell(cell, index);
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
            var cell = TakeCell(IndexOfSlot(slot));
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
            var cross = double.IsFinite(_arrangedCross) ? _arrangedCross : _crossConstraint;
            if (Horizontal)
                ((IView)cell).Measure(_extent, cross);
            else
                ((IView)cell).Measure(cross, _extent);
        }

        private void ArrangeCell(ItemCell cell)
        {
            // Logical: the layout mirrors frames in right-to-left layouts.
            var start = _padStart + cell.Slot * _stride;
            ((IView)cell).Arrange(Horizontal ? new Rect(start, 0, _extent, _arrangedCross) : new Rect(0, start, _arrangedCross, _extent));
        }

        /// <summary>The slots that show (with one item of prefetch on either side, and the items an effect shows).</summary>
        private (int First, int Last) SlotWindow()
        {
            // Slots that overlap the viewport widened by one stride on either side (touching it is not overlapping).
            var offset = LogicalOffset;
            var first = (int)Math.Floor((offset - _stride - _padStart - _extent) / _stride) + 1;
            var last = (int)Math.Ceiling((offset + _viewportLength + _stride - _padStart) / _stride) - 1;
            if (owner._effect is { } effect && effect.GetVisibleRange(Metrics) is > 0 and var range)
            {
                var center = FractionalSlot;
                first = Math.Min(first, (int)Math.Floor(center - range));
                last = Math.Max(last, (int)Math.Ceiling(center + range));
            }
            return (Math.Max(0, first), Math.Min(_slots - 1, last));
        }

        /// <summary>Realizes the slots that show and releases the others; rebinds cells whose item changed. Returns whether the cells changed.</summary>
        private bool RealizeWindow(bool arrange)
        {
            if (!HasGeometry || !double.IsFinite(_viewportLength) || _viewportLength <= 0)
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
                var index = IndexOfSlot(cell.Slot);
                var item = owner.ItemAt(index);
                if (!Equals(owner.ItemKey(item), cell.TemplateKey))
                {
                    Release(position);
                    _realized.Insert(position, Realize(cell.Slot));
                    changed = true;
                }
                else if (index != cell.Index || !ReferenceEquals(item, cell.Item))
                {
                    owner.BindCell(cell, index);
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
            return changed;
        }

        /// <summary>The view of the item at <paramref name="index"/> nearest to the snap point, or <c>null</c>.</summary>
        public ISkUiView? RealizedView(int index)
        {
            ItemCell? best = null;
            var center = HasGeometry ? FractionalSlot : 0;
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
            var current = HasGeometry ? NearestSlotOf(owner._position) : owner._position;
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

        /// <summary>The item at the snap point: <paramref name="keep"/> while its (clamped) snap point is where the strip is.</summary>
        public int CurrentIndex(int keep)
        {
            if (!HasGeometry)
                return keep;
            if (!Loops)
            {
                // Items whose snap points the ends clamp share them: the current one stays current there.
                if (keep >= 0 && keep < _count && Math.Abs(ClampedSnapOffset(keep) - LogicalOffset) < 0.5)
                    return keep;
                return Math.Clamp(NearestSlot, 0, _count - 1);
            }
            return IndexOfSlot(NearestSlot);
        }

        /// <summary>The last item in view (not looping).</summary>
        public int LastVisibleIndex => HasGeometry
            ? Math.Clamp((int)Math.Floor((LogicalOffset + _viewportLength - _padStart - 0.5) / _stride), 0, _count - 1)
            : -1;

        /// <summary>The logical offset of the item at <paramref name="index"/>: looping, the nearest copy of it (the shorter way round).</summary>
        private double TargetOffset(int index)
        {
            if (!Loops)
                return ClampedSnapOffset(index);
            return SnapOffset(NearestSlotOf(index));
        }

        /// <summary>The slot of the item at <paramref name="index"/> nearest to the snap point (the shorter way round when looping).</summary>
        private int NearestSlotOf(int index)
        {
            if (!Loops)
                return index;
            var current = NearestSlot;
            var delta = (int)Mod(index - Mod(current, _count), _count);
            if (delta > _count / 2)
                delta -= _count;
            return current + delta;
        }

        /// <summary>Scrolls so the item at <paramref name="index"/> is at the snap point; before the layout, the layout places it.</summary>
        public Task ScrollToIndex(int index, bool animated)
        {
            if (!HasGeometry || !_arranged || _pendingLogical is not null)
            {
                _keep = index;
                _placeRequested = true;
                if (HasGeometry)
                    _pendingLogical = TargetOffset(index);
                _geometryValid = false;
                InvalidateMeasureOverride();
                return Task.CompletedTask;
            }
            var target = Physical(TargetOffset(index));
            var controller = owner.Controller;
            return Horizontal ? controller.ScrollToAsync(target, controller.Y, animated) : controller.ScrollToAsync(controller.X, target, animated);
        }

        /// <summary>After the scroller's arrange: it takes the offset the layout wants (the kept place).</summary>
        public void ApplyPendingOffset()
        {
            if (_pendingLogical is not { } logical)
                return;
            _pendingLogical = null;
            if (!HasGeometry)
                return;
            var controller = owner.Controller;
            var target = Physical(Math.Clamp(logical, 0, MaxLogical));
            var delta = target - PhysicalOffset;
            if (Math.Abs(delta) < 0.01)
            {
                Update();
                owner.OnLayoutScrolled();
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
            if (!Loops || !HasGeometry || _pendingLogical is not null)
                return;
            var cycle = _count * _stride;
            var drift = LogicalOffset - SnapOffset(HomeSlot);
            if (Math.Abs(drift) < RecenterDistance * cycle)
                return;
            var cycles = (int)Math.Round(drift / cycle);
            if (cycles == 0)
                return;
            // The cells move with their slots: each keeps its item (slot mod count) and only moves in the strip.
            var slots = cycles * _count;
            foreach (var cell in _realized)
                cell.Slot -= slots;
            var logical = -cycles * cycle;
            var physical = Mirrored ? -logical : logical;
            owner.Controller.CorrectOffset(Horizontal ? physical : 0, Horizontal ? 0 : physical);
            InvalidateMeasureFromChild(); // the scroller's extent comes back with the next measure
            InvalidateRender(SkUiRenderDirty.Children);
        }

        /// <summary>The scrolled-event arguments for the current offset.</summary>
        public ItemsViewScrolledEventArgs CreateScrolledArgs()
        {
            var controller = owner.Controller;
            var offset = new Point(controller.X, controller.Y);
            var delta = offset - _lastOffset;
            _lastOffset = offset;
            var logical = LogicalOffset;
            var first = Math.Clamp((int)Math.Floor((logical - _padStart) / _stride), 0, _slots - 1);
            var last = Math.Clamp((int)Math.Floor((logical + _viewportLength - _padStart - 0.5) / _stride), 0, _slots - 1);
            return new ItemsViewScrolledEventArgs
            {
                HorizontalDelta = delta.Width,
                VerticalDelta = delta.Height,
                HorizontalOffset = offset.X,
                VerticalOffset = offset.Y,
                FirstVisibleItemIndex = IndexOfSlot(first),
                CenterItemIndex = CurrentIndex(owner._position),
                LastVisibleItemIndex = IndexOfSlot(last)
            };
        }

        void ISkUiSnapPointSource.GetSnapOffsets(bool horizontal, double from, double to, List<double> points)
        {
            if (!HasGeometry || horizontal != Horizontal)
                return;
            var (low, high) = Mirrored ? (Logical(to), Logical(from)) : (from, to);
            var first = Math.Max(0, (int)Math.Ceiling((low + _align) / _stride));
            var last = Math.Min(_slots - 1, (int)Math.Floor((high + _align) / _stride));
            if (!Loops)
            {
                // Clamped snap points of the first and last items lie at the ends.
                first = Math.Min(first, low <= 0 ? 0 : first);
                last = Math.Max(last, high >= MaxLogical ? _slots - 1 : last);
            }
            if (last - first >= MaxSnapPoints)
                last = first + MaxSnapPoints - 1;
            for (var slot = first; slot <= last; slot++)
                points.Add(Physical(Loops ? SnapOffset(slot) : ClampedSnapOffset(slot)));
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
            if (owner._effect is not { } effect || !HasGeometry)
            {
                link = null;
                return;
            }
            var source = ((ISkUiRenderable)owner._scroller).RenderState.Node;
            var factor = (float)(-1 / _stride);
            var @base = (float)(Physical(SnapOffset(cell.Slot)) / _stride);
            var metrics = Metrics;
            // Exact: a link is immutable once committed; any change of its numbers makes a new one.
            if (link is null || !ReferenceEquals(link.Source, source) || !ReferenceEquals(link.Effect, effect)
                || link.Factor != factor || link.Base != @base || link.Metrics != metrics || link.Horizontal != Horizontal)
                link = new SkUiItemEffectLink(source, Horizontal, factor, @base, effect, metrics);
            props.ItemEffect = link;
            var offset = owner._scroller.VisualScrollOffset;
            link.Apply(ref props, (float)offset.X, (float)offset.Y);
        }

        #endregion

        #region Layout

        internal override IEnumerable<ISkUiView> SkiaChildren => _realized;

        /// <inheritdoc />
        /// <remarks>With an effect, nearer items are hit (and drawn) over farther ones: back to front by their depth at the current offset.</remarks>
        internal override void AddRenderChildren(List<ISkUiRenderable> children)
        {
            if (owner._effect is not { } effect || !HasGeometry || _realized.Count < 2)
            {
                foreach (var cell in _realized)
                    children.Add(cell);
                return;
            }
            var offset = owner._scroller.VisualScrollOffset;
            var physical = Horizontal ? offset.X : offset.Y;
            var metrics = Metrics;
            _drawOrder.Clear();
            foreach (var cell in _realized)
            {
                cell.Depth = effect.GetItemTransform((Physical(SnapOffset(cell.Slot)) - physical) / _stride, metrics).ZIndex;
                _drawOrder.Add(cell);
            }
            _drawOrder.Sort(static (a, b) => a.Depth != b.Depth ? a.Depth.CompareTo(b.Depth) : a.Slot.CompareTo(b.Slot));
            foreach (var cell in _drawOrder)
                children.Add(cell);
            _drawOrder.Clear();
        }

        internal override void OnGetRenderProps(ref SkUiRenderProps props) => props.SortsChildrenByDepth = owner._effect is not null;

        protected override Size MeasureContent(double widthConstraint, double heightConstraint)
        {
            var cross = Horizontal ? heightConstraint : widthConstraint;
            if (!_geometryValid || cross != _crossConstraint)
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
            return Horizontal ? new Size(_length, across) : new Size(across, _length);
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
