using System.Collections;
using System.Collections.Specialized;

namespace MauiSkiaUi;

public partial class SkUiCollectionView
{
    #region Reordering

    /// <summary>Bindable property for <see cref="CanReorderItems"/>.</summary>
    public static readonly BindableProperty CanReorderItemsProperty = BindableProperty.Create(nameof(CanReorderItems), typeof(bool), typeof(SkUiCollectionView), false,
        propertyChanged: (view, _, value) => ((SkUiCollectionView)view).OnCanReorderItemsChanged((bool)value));

    /// <summary>Bindable property for <see cref="CanMixGroups"/>.</summary>
    public static readonly BindableProperty CanMixGroupsProperty = BindableProperty.Create(nameof(CanMixGroups), typeof(bool), typeof(SkUiCollectionView), false,
        propertyChanged: (view, _, value) => ((SkUiCollectionView)view).OnCanMixGroupsChanged((bool)value));

    /// <summary>
    /// Whether the user can move items to other places (MAUI's; default <c>false</c>). A touch or pen lifts an item with a long
    /// press, the mouse with a press and drag. The other items move aside as the lifted one passes them, and the list
    /// scrolls when it is brought near either end. On release the item leaves its list (<c>RemoveAt</c>) and goes to its
    /// new place (<c>Insert</c>), and <see cref="ReorderCompleted"/> is raised. Screen readers offer "Move up" and "Move down"
    /// on each item (left and right where one place is sideways). Only items whose list can change move:
    /// <see cref="ItemsSource"/> (or, grouped, its group) must be an <see cref="IList"/> that is neither read-only nor of a
    /// fixed size. Lists and grids of both orientations; group headers and footers stay where they are.
    /// </summary>
    public bool CanReorderItems { get => (bool)GetValue(CanReorderItemsProperty); set => SetValue(CanReorderItemsProperty, value); }

    /// <summary>
    /// In a grouped list, whether reordering moves items to other groups too (MAUI's; default <c>false</c>: only within
    /// their own group). Collapsed groups take no items.
    /// </summary>
    public bool CanMixGroups { get => (bool)GetValue(CanMixGroupsProperty); set => SetValue(CanMixGroupsProperty, value); }

    /// <summary>Raised after the user moved an item (the source has changed). An item put back where it was raises nothing.</summary>
    public event EventHandler? ReorderCompleted;

    /// <summary>Sets <see cref="CanReorderItems"/> (same as the property setter).</summary>
    public SkUiCollectionView SetCanReorderItems(bool value) { CanReorderItems = value; return this; }

    /// <summary>Sets <see cref="CanMixGroups"/> (same as the property setter).</summary>
    public SkUiCollectionView SetCanMixGroups(bool value) { CanMixGroups = value; return this; }

    private bool _canReorderItems;
    private bool _canMixGroups;
    private ReorderDrag? _drag;
    private ReorderGesture? _reorderGesture;
    private DragHost? _dragHost;
    private IDisposable? _autoScroll;
    private TimeSpan _autoScrollTime;
    /// <summary>A reorder's change of the source is under way: anchoring, the selection and the drag itself leave it alone.</summary>
    private bool _committingReorder;
    /// <summary>The translations given to rows and cells while an item is dragged, and what they were before (a footer the app translated).</summary>
    private readonly Dictionary<SkUiView, (Point Target, Point Resting)> _displaced = [];

    private void OnCanReorderItemsChanged(bool value)
    {
        _canReorderItems = value;
        if (!value)
            CancelReorder();
        InvalidateItemSemantics();
    }

    private void OnCanMixGroupsChanged(bool value)
    {
        _canMixGroups = value;
        InvalidateItemSemantics();
    }

    /// <summary>The items' screen-reader actions may have changed.</summary>
    private void InvalidateItemSemantics()
    {
        if (!_partsCreated)
            return;
        foreach (var host in RealizedItemHosts())
            host.RefreshSemantics();
    }

    /// <summary>A list that can change: <see cref="ItemsSource"/> (<paramref name="group"/> -1) or a group's items; else <c>null</c>.</summary>
    private IList? WritableList(int group) =>
        group < Model.Groups.Count && Model.ListOf(group) is { IsReadOnly: false, IsFixedSize: false } list ? list : null;

    /// <summary>Whether the item <paramref name="host"/> shows can be lifted now.</summary>
    private bool CanLift(ItemHost host) =>
        _canReorderItems && _drag is null && IsEnabled && !ReferenceEquals(host, _dragHost?.Proxy)
        && LocateHost(host) is { } location && WritableList(location.Group) is not null;

    /// <summary>Whether <paramref name="group"/> can take items moved from <paramref name="from"/>.</summary>
    private bool CanTake(int group, int from) =>
        group == from || (_canMixGroups && group >= 0 && Model.Groups[group].Expanded && WritableList(group) is not null);

    #endregion

    #region Lifting and dragging

    /// <summary>What a drag lifted: where the item was, the proxy that shows it, where it goes.</summary>
    private sealed class ReorderDrag(ReorderGesture gesture, object? item, int group, int index, int row, ItemHost proxy, Size size, Point grab)
    {
        public readonly ReorderGesture Gesture = gesture;
        public readonly object? Item = item;
        public readonly int Group = group;
        public readonly int Index = index;
        /// <summary>The item's row (a list's row, or a grid row).</summary>
        public readonly int Row = row;
        public readonly ItemHost Proxy = proxy;
        public readonly Size Size = size;
        /// <summary>Where the pointer took the item, in its container.</summary>
        public readonly Point Grab = grab;
        /// <summary>The item's place across the axis in the list (a list moves it only along).</summary>
        public double HomeAcross;
        /// <summary>The newest pointer sample (it maps through the current scroll offset after an auto-scroll step).</summary>
        public SkUiPointer Pointer;
        /// <summary>Where the item goes: its group and index once moved.</summary>
        public (int Group, int Index) Target;
        /// <summary>A list: the row the item's row takes (rows between move aside).</summary>
        public int TargetRow;
        /// <summary>Released: the proxy goes to the target, then the source changes.</summary>
        public bool Dropping;
    }

    /// <summary>Lifts the item <paramref name="host"/> shows (the gesture claimed the pointer); <c>false</c> when it cannot move.</summary>
    private bool BeginReorder(ItemHost host, ReorderGesture gesture, SkUiPointer pointer)
    {
        if (!CanLift(host) || LocateHost(host) is not { } location)
            return false;
        var (group, index) = location;
        var row = Model.RowOfItem(group, index);
        var item = Model.ItemAt(group, index);
        var size = new Size(host.Width, host.Height);
        if (row < 0 || size.Width <= 0 || size.Height <= 0)
            return false;
        Controller.StopMotion();
        var proxy = TakeCell(ItemKey(item));
        BindItemHost(proxy, item);
        // Where the press took it (a mouse lifts it past the touch slop): the item stays at that spot under the pointer.
        var drag = new ReorderDrag(gesture, item, group, index, row, proxy, size, pointer.MapToElement(host, pointer.StartPosition))
        {
            Pointer = pointer,
            Target = location,
            TargetRow = row
        };
        var inList = pointer.MapToElement(this, pointer.StartPosition);
        drag.HomeAcross = _horizontal ? inList.Y - drag.Grab.Y : inList.X - drag.Grab.X;
        _drag = drag;
        (_dragHost ??= Adopt(new DragHost())).Show(proxy, size);
        host.Opacity = 0;
        var look = SkUiLookScope.Resolve(this);
        if (look.GetReorderLiftShadow() is { } shadow)
            proxy.Shadow = shadow;
        Animate(proxy, SkUiAnimatableProperty.Scale, look.ReorderLiftScale, look.GetTransition(SkUiTransitionKind.ReorderLift));
        MoveProxy(drag);
        UpdateTarget(drag); // a mouse lifts it already moved
        return true;
    }

    /// <summary>The pointer moved the lifted item.</summary>
    private void DragReorder(ReorderGesture gesture, SkUiPointer pointer)
    {
        if (_drag is not { Dropping: false } drag || !ReferenceEquals(drag.Gesture, gesture))
            return;
        drag.Pointer = pointer;
        MoveProxy(drag);
        UpdateTarget(drag);
        UpdateAutoScroll(drag);
    }

    /// <summary>Puts the proxy under the pointer (a list keeps it on the item's line).</summary>
    private void MoveProxy(ReorderDrag drag)
    {
        var inList = drag.Pointer.GetPosition(this);
        var x = inList.X - drag.Grab.X;
        var y = inList.Y - drag.Grab.Y;
        if (Model.Span == 1)
        {
            if (_horizontal)
                y = drag.HomeAcross;
            else
                x = drag.HomeAcross;
        }
        drag.Proxy.TranslationX = x;
        drag.Proxy.TranslationY = y;
    }

    /// <summary>The proxy's center in the items' layout (its own coordinates).</summary>
    private Point ProxyCenterInItems(ReorderDrag drag)
    {
        var offset = ItemsOffset(drag);
        return new Point(drag.Proxy.TranslationX + drag.Size.Width / 2 - offset.X, drag.Proxy.TranslationY + drag.Size.Height / 2 - offset.Y);
    }

    /// <summary>How far the items' layout is from the list now: a point in the list less this is the point in the items.</summary>
    private Point ItemsOffset(ReorderDrag drag)
    {
        var inList = drag.Pointer.GetPosition(this);
        var inItems = drag.Pointer.GetPosition(ItemsHost);
        return new Point(inList.X - inItems.X, inList.Y - inItems.Y);
    }

    /// <summary>A position along the axis in the items' layout, in item coordinates (from the layout's right edge, right to left).</summary>
    private double AlongItems(Point inItems) =>
        !_horizontal ? inItems.Y : ItemsHost.IsRightToLeft ? ItemsHost.Width - inItems.X : inItems.X;

    /// <summary>Picks the target under the proxy; when it changes, the rows or cells it passes move aside.</summary>
    private void UpdateTarget(ReorderDrag drag)
    {
        var center = ProxyCenterInItems(drag);
        var changed = Model.Span == 1 ? UpdateListTarget(drag, AlongItems(center)) : UpdateGridTarget(drag, center);
        if (changed)
            ApplyDisplacements(drag, animated: true);
    }

    /// <summary>A list: the row whose middle the proxy's center passed, if the item can go there.</summary>
    private bool UpdateListTarget(ReorderDrag drag, double along)
    {
        var count = Model.RowCount;
        if (count == 0)
            return false;
        var lifted = drag.Row;
        var row = ItemsHost.RowAtOffset(Math.Max(0, along));
        row = Math.Clamp(row, 0, count - 1);
        var middle = ItemsHost.RowOffset(row) + ItemsHost.RowSize(row) / 2;
        var targetRow = row == lifted ? lifted
            : row > lifted ? along >= middle ? row : row - 1
            : along <= middle ? row : row + 1;
        if (targetRow == drag.TargetRow || ListTargetAt(drag, targetRow) is not { } target)
            return false;
        drag.TargetRow = targetRow;
        drag.Target = target;
        return true;
    }

    /// <summary>
    /// Where the lifted row taking row <paramref name="targetRow"/>'s place puts the item: after that row when it moves down
    /// (towards the end), before it when it moves up; <c>null</c> when that is no place for it (between a group's footer and
    /// the next header, inside a collapsed group, another group without <see cref="CanMixGroups"/>).
    /// </summary>
    private (int Group, int Index)? ListTargetAt(ReorderDrag drag, int targetRow)
    {
        if (targetRow == drag.Row)
            return (drag.Group, drag.Index);
        (int Group, int Index)? target;
        var row = Model.RowAt(targetRow);
        if (targetRow > drag.Row)
        {
            target = row.Kind switch
            {
                // Within its own list the item takes the index of the one it passes; in another it goes after it.
                RowKind.Items => (row.Group, row.Group == drag.Group ? row.Start : row.Start + 1),
                RowKind.GroupHeader when Model.Groups[row.Group].Expanded => (row.Group, 0),
                _ => null
            };
        }
        else
        {
            target = row.Kind switch
            {
                RowKind.Items => (row.Group, row.Start),
                RowKind.GroupFooter => (row.Group, Model.Groups[row.Group].Count),
                // Before a header: the end of the group before, when its last item is right before the header.
                RowKind.GroupHeader when targetRow > 0 && Model.RowAt(targetRow - 1) is { Kind: RowKind.Items } before => (before.Group, before.Start + 1),
                _ => null
            };
        }
        return target is { } place && CanTake(place.Group, drag.Group) ? place : null;
    }

    /// <summary>A grid: the cell under the proxy's center (at most one past a group's last item), if the item can go there.</summary>
    private bool UpdateGridTarget(ReorderDrag drag, Point center)
    {
        if (Model.RowCount == 0)
            return false;
        var rowIndex = Math.Clamp(ItemsHost.RowAtOffset(Math.Max(0, AlongItems(center))), 0, Model.RowCount - 1);
        var row = Model.RowAt(rowIndex);
        (int Group, int Index)? target = row.Kind switch
        {
            RowKind.Items => (row.Group, row.Start + ColumnAt(center)),
            RowKind.GroupHeader when Model.Groups[row.Group].Expanded => (row.Group, 0),
            RowKind.GroupFooter => (row.Group, int.MaxValue),
            _ => null
        };
        if (target is not { } place || !CanTake(place.Group, drag.Group))
            return false;
        var count = GroupCount(place.Group);
        // Its own group has one item fewer once it leaves.
        place.Index = Math.Min(place.Index, place.Group == drag.Group ? count - 1 : count);
        if (place == drag.Target)
            return false;
        drag.Target = place;
        return true;
    }

    private int GroupCount(int group) => group < 0 ? Model.ItemCount : Model.Groups[group].Count;

    /// <summary>A grid's column under <paramref name="inItems"/> (mirrored right to left in a vertical grid).</summary>
    private int ColumnAt(Point inItems)
    {
        var across = _horizontal ? ItemsHost.Height : ItemsHost.Width;
        var (cell, gap) = GridCell(across);
        var position = _horizontal ? inItems.Y : ItemsHost.IsRightToLeft ? across - inItems.X : inItems.X;
        return Math.Clamp((int)Math.Floor(position / Math.Max(1, cell + gap)), 0, Model.Span - 1);
    }

    /// <summary>The size of a grid cell across the rows and the gap between cells, for rows <paramref name="across"/> long.</summary>
    private (double Cell, double Gap) GridCell(double across) =>
        (Math.Max(0, (across - (Model.Span - 1) * _spanSpacing) / Model.Span), _spanSpacing);

    #endregion

    #region Moving aside

    /// <summary>
    /// Gives every realized row (list) or cell (grid) the translation the target asks for, animated with the look's
    /// <see cref="SkUiTransitionKind.ReorderMove"/> or at once.
    /// </summary>
    private void ApplyDisplacements(ReorderDrag drag, bool animated)
    {
        var (first, last) = ItemsHost.RealizedRange;
        if (first < 0)
            return;
        var transition = animated ? SkUiLookScope.Resolve(this).GetTransition(SkUiTransitionKind.ReorderMove) : SkUiTransition.None;
        for (var row = first; row <= last; row++)
            if (ItemsHost.GetRealizedView(row) is SkUiView view)
                DisplaceRow(drag, row, view, transition);
    }

    /// <summary>Moves one realized row (its cells, in a grid) where the drag wants it; the lifted item stays transparent.</summary>
    private void DisplaceRow(ReorderDrag drag, int rowIndex, SkUiView view, SkUiTransition transition)
    {
        if (view is GridRowPart rowPart)
        {
            var row = Model.RowAt(rowIndex);
            for (var slot = 0; slot < rowPart.Cells.Count && slot < row.Count; slot++)
            {
                var cell = rowPart.Cells[slot];
                var index = row.Start + slot;
                var lifted = row.Group == drag.Group && index == drag.Index;
                cell.Opacity = lifted ? 0 : 1;
                Displace(cell, lifted ? Point.Zero : CellShift(drag, rowIndex, rowPart, row.Group, index), transition);
            }
            return;
        }
        if (rowIndex == drag.Row)
        {
            view.Opacity = 0;
            return;
        }
        var shift = 0d;
        var t = drag.TargetRow;
        var length = drag.Size.Height;
        if (_horizontal)
            length = drag.Size.Width;
        length += ItemsHost.ItemSpacing;
        if (t > drag.Row && rowIndex > drag.Row && rowIndex <= t)
            shift = -length;
        else if (t < drag.Row && rowIndex >= t && rowIndex < drag.Row)
            shift = length;
        // Item coordinates run from the right edge right to left.
        var sign = _horizontal && ItemsHost.IsRightToLeft ? -1 : 1;
        Displace(view, _horizontal ? new Point(sign * shift, 0) : new Point(0, shift), transition);
    }

    /// <summary>Where cell <paramref name="index"/> of <paramref name="group"/> goes while the drag shows the item at its target: its neighbor's cell.</summary>
    private Point CellShift(ReorderDrag drag, int rowIndex, GridRowPart rowPart, int group, int index)
    {
        var moved = index;
        if (group == drag.Group && index > drag.Index)
            moved--;
        if (group == drag.Target.Group && moved >= drag.Target.Index && (group != drag.Group || index != drag.Index))
            moved++;
        if (moved == index)
            return Point.Zero;
        var from = CellOrigin(rowIndex, rowPart, index - Model.RowAt(rowIndex).Start);
        return CellPosition(group, moved) is { } to ? new Point(to.X - from.X, to.Y - from.Y) : Point.Zero;
    }

    /// <summary>Where the cell of item <paramref name="index"/> of <paramref name="group"/> is in the items' layout, while its row is realized (or follows one).</summary>
    private Point? CellPosition(int group, int index)
    {
        var rowIndex = Model.RowOfItem(group, index);
        var slot = index % Model.Span;
        if (rowIndex >= 0 && rowIndex < Model.RowCount && ItemsHost.GetRealizedView(rowIndex) is GridRowPart rowPart
            && Model.RowAt(rowIndex) is { Kind: RowKind.Items } row && row.Group == group)
            return CellOrigin(rowIndex, rowPart, index - row.Start);
        // A place past the group's last row: a new row after it.
        if (rowIndex > 0 && ItemsHost.GetRealizedView(rowIndex - 1) is GridRowPart previous && Model.RowAt(rowIndex - 1).Group == group)
        {
            var origin = CellOrigin(rowIndex - 1, previous, slot);
            var step = (_horizontal ? previous.Width : previous.Height) + ItemsHost.ItemSpacing;
            var sign = _horizontal && ItemsHost.IsRightToLeft ? -1 : 1;
            return _horizontal ? new Point(origin.X + sign * step, origin.Y) : new Point(origin.X, origin.Y + step);
        }
        return null;
    }

    /// <summary>The top left corner of cell <paramref name="slot"/> of a realized grid row, in the items' layout.</summary>
    private Point CellOrigin(int rowIndex, GridRowPart rowPart, int slot)
    {
        var frame = rowPart.Frame;
        var across = _horizontal ? frame.Height : frame.Width;
        var (cell, gap) = GridCell(across);
        var at = slot * (cell + gap);
        if (_horizontal)
            return new Point(frame.X, frame.Y + at);
        return new Point(frame.X + (rowPart.IsRightToLeft ? across - at - cell : at), frame.Y);
    }

    /// <summary>Sends <paramref name="view"/> towards a translation of <paramref name="target"/> from where it rests.</summary>
    private void Displace(SkUiView view, Point target, SkUiTransition transition)
    {
        if (!_displaced.TryGetValue(view, out var entry))
        {
            if (target == Point.Zero)
                return;
            entry = (Point.Zero, new Point(view.TranslationX, view.TranslationY));
        }
        // Exact: an unchanged target keeps a running move.
        if (entry.Target == target)
            return;
        _displaced[view] = (target, entry.Resting);
        if (_horizontal || Model.Span > 1)
            Animate(view, SkUiAnimatableProperty.TranslationX, entry.Resting.X + target.X, transition);
        if (!_horizontal || Model.Span > 1)
            Animate(view, SkUiAnimatableProperty.TranslationY, entry.Resting.Y + target.Y, transition);
    }

    /// <summary>Animates a composite-time property on the render thread while the list shows, else sets it.</summary>
    private void Animate(SkUiView view, SkUiAnimatableProperty property, double to, SkUiTransition transition)
    {
        if (!transition.IsNone && IsShown)
        {
            _ = view.AnimateAsync(property, to, (uint)Math.Max(1, transition.Duration.TotalMilliseconds), transition.Easing);
            return;
        }
        view.StopAnimations(property);
        ((ISkUiAnimationTarget)view).SetAnimatable(property, to);
    }

    /// <summary>A row was realized or rebound while an item is dragged: it shows where the drag wants it, at once.</summary>
    private void OnRowBound(int rowIndex, ISkUiView view)
    {
        if (_drag is { } drag && view is SkUiView row)
            DisplaceRow(drag, rowIndex, row, SkUiTransition.None);
    }

    /// <summary>A row was released: it rests again (a recycled view must not keep the drag's translation or transparency).</summary>
    private void OnRowUnbound(ISkUiView view)
    {
        if (_displaced.Count == 0 && _drag is null)
            return;
        if (view is GridRowPart rowPart)
            foreach (var cell in rowPart.Cells)
                Rest(cell);
        else if (view is SkUiView row)
            Rest(row);
    }

    /// <summary>Puts a view back where it rested before the drag, at once.</summary>
    private void Rest(SkUiView view)
    {
        if (view is ItemHost)
            view.Opacity = 1;
        if (!_displaced.Remove(view, out var entry))
            return;
        // A move aside may still run: it must not end after the view rests (or is recycled).
        view.StopAnimations(SkUiAnimatableProperty.TranslationX);
        view.StopAnimations(SkUiAnimatableProperty.TranslationY);
        view.TranslationX = entry.Resting.X;
        view.TranslationY = entry.Resting.Y;
    }

    /// <summary>Puts every realized row and cell back where it rests.</summary>
    private void RestAll()
    {
        foreach (var view in _displaced.Keys.ToArray())
            Rest(view);
        foreach (var host in RealizedItemHosts())
            host.Opacity = 1;
    }

    #endregion

    #region Auto-scroll

    /// <summary>Starts or stops scrolling the list while the pointer is near one of its ends.</summary>
    private void UpdateAutoScroll(ReorderDrag drag)
    {
        if (AutoScrollSpeed(drag) == 0)
        {
            StopAutoScroll();
            return;
        }
        if (_autoScroll is not null)
            return;
        var clock = AnimationClock;
        _autoScrollTime = clock.FrameTime;
        _autoScroll = clock.Start(_ => AutoScrollStep(), TimeSpan.FromSeconds(1), repeat: true);
    }

    private void StopAutoScroll()
    {
        var tick = _autoScroll;
        _autoScroll = null;
        tick?.Dispose();
    }

    /// <summary>
    /// The scroll speed the pointer asks for, in DIPs per second (negative: towards the physical start, the top or left): up
    /// to the look's <see cref="SkUiLook.ReorderAutoScrollSpeed"/> at an end of the part the sticky header and footer leave.
    /// </summary>
    private double AutoScrollSpeed(ReorderDrag drag)
    {
        var at = drag.Pointer.GetPosition(this);
        var along = _horizontal ? at.X : at.Y;
        var length = _horizontal ? Width : Height;
        var (start, end) = Scroller.Insets;
        if (_horizontal && IsRightToLeft)
            (start, end) = (end, start);
        var low = start;
        var high = length - end;
        if (high - low <= 0)
            return 0;
        var look = SkUiLookScope.Resolve(this);
        var zone = Math.Min(look.ReorderAutoScrollZone, (high - low) * 0.15);
        if (zone <= 0)
            return 0;
        var offset = _horizontal ? Controller.X : Controller.Y;
        var max = _horizontal ? Controller.MaxX : Controller.MaxY;
        if (along < low + zone && offset > 0)
            return -look.ReorderAutoScrollSpeed * Math.Clamp((low + zone - along) / zone, 0, 1);
        if (along > high - zone && offset < max)
            return look.ReorderAutoScrollSpeed * Math.Clamp((along - (high - zone)) / zone, 0, 1);
        return 0;
    }

    private void AutoScrollStep()
    {
        if (_drag is not { Dropping: false } drag)
        {
            StopAutoScroll();
            return;
        }
        // The clock's frame time: steps follow the frames as drawn (a busy frame scrolls at most 50 ms worth).
        var now = AnimationClock.FrameTime;
        var seconds = Math.Clamp((now - _autoScrollTime).TotalSeconds, 0, 0.05);
        _autoScrollTime = now;
        var speed = AutoScrollSpeed(drag);
        if (speed == 0)
        {
            StopAutoScroll();
            return;
        }
        AutoScrollBy(drag, speed * seconds);
    }

    /// <summary>Scrolls the list by <paramref name="delta"/> DIPs while an item is dragged (the proxy stays under the pointer, the target follows).</summary>
    private void AutoScrollBy(ReorderDrag drag, double delta)
    {
        if (_horizontal)
            Controller.ScrollBy(delta, 0);
        else
            Controller.ScrollBy(0, delta);
        UpdateTarget(drag);
    }

    /// <summary>Runs one auto-scroll step of <paramref name="seconds"/> (tests).</summary>
    internal void StepReorderAutoScroll(double seconds)
    {
        if (_drag is { Dropping: false } drag && AutoScrollSpeed(drag) is var speed and not 0)
            AutoScrollBy(drag, speed * seconds);
    }

    /// <summary>Whether the list auto-scrolls for a dragged item (tests).</summary>
    internal bool IsReorderAutoScrolling => _autoScroll is not null;

    #endregion

    #region Drop and cancel

    /// <summary>The pointer was released: the proxy goes to the target's place, then the source changes.</summary>
    private void DropReorder(ReorderGesture gesture)
    {
        if (_drag is not { Dropping: false } drag || !ReferenceEquals(drag.Gesture, gesture))
            return;
        StopAutoScroll();
        drag.Dropping = true;
        var look = SkUiLookScope.Resolve(this);
        var transition = look.GetTransition(SkUiTransitionKind.ReorderMove);
        if (transition.IsNone || !IsShown || TargetOrigin(drag) is not { } to)
        {
            FinishReorder(drag, commit: true);
            return;
        }
        Animate(drag.Proxy, SkUiAnimatableProperty.Scale, 1, look.GetTransition(SkUiTransitionKind.ReorderLift));
        var length = (uint)Math.Max(1, transition.Duration.TotalMilliseconds);
        var x = drag.Proxy.AnimateAsync(SkUiAnimatableProperty.TranslationX, to.X, length, transition.Easing);
        var y = drag.Proxy.AnimateAsync(SkUiAnimatableProperty.TranslationY, to.Y, length, transition.Easing);
        _ = Settle();

        // Moved (or stopped: the proxy was detached), the item moves unless the drag ended meanwhile.
        async Task Settle()
        {
            await Task.WhenAll(x, y);
            FinishReorder(drag, commit: true);
        }
    }

    /// <summary>Where the proxy goes on a drop, in the list: the target's place as the moved rows show it.</summary>
    private Point? TargetOrigin(ReorderDrag drag)
    {
        var offset = ItemsOffset(drag);
        Point inItems;
        if (Model.Span > 1)
        {
            // Cells are fixed places: the item ends in the cell its target index has now (the others moved aside from it).
            if (CellPosition(drag.Target.Group, drag.Target.Index) is not { } cell)
                return null;
            inItems = cell;
        }
        else
        {
            var t = drag.TargetRow;
            var size = _horizontal ? drag.Size.Width : drag.Size.Height;
            var start = t > drag.Row ? ItemsHost.RowOffset(t) + ItemsHost.RowSize(t) - size : ItemsHost.RowOffset(t);
            // Across the axis the proxy keeps its line (below).
            inItems = !_horizontal ? new Point(0, start)
                : ItemsHost.IsRightToLeft ? new Point(ItemsHost.Width - start - size, 0) : new Point(start, 0);
        }
        var x = inItems.X + offset.X;
        var y = inItems.Y + offset.Y;
        // A list keeps the proxy on the item's line.
        if (Model.Span == 1)
        {
            if (_horizontal)
                y = drag.HomeAcross;
            else
                x = drag.HomeAcross;
        }
        return new Point(x, y);
    }

    /// <summary>The pointer was cancelled or the gesture lost: the item goes back, nothing moves.</summary>
    private void CancelReorder(ReorderGesture gesture)
    {
        if (_drag is { } drag && ReferenceEquals(drag.Gesture, gesture) && !drag.Dropping)
            CancelReorder();
    }

    /// <summary>Ends a drag without moving anything (the list changed, left the tree, stopped reordering).</summary>
    private void CancelReorder()
    {
        if (_drag is not { } drag)
            return;
        FinishReorder(drag, commit: false);
        // The pointer stays out of the list until it is released.
        drag.Gesture.Cancel();
    }

    /// <summary>Ends the drag: everything rests, the proxy goes back to the pool, and the item moves when <paramref name="commit"/>.</summary>
    private void FinishReorder(ReorderDrag drag, bool commit)
    {
        if (!ReferenceEquals(_drag, drag))
            return;
        StopAutoScroll();
        _drag = null;
        RestAll();
        _dragHost?.Show(null, Size.Zero);
        var proxy = drag.Proxy;
        proxy.ClearValue(ShadowProperty);
        proxy.StopAnimations(SkUiAnimatableProperty.Scale);
        proxy.StopAnimations(SkUiAnimatableProperty.TranslationX);
        proxy.StopAnimations(SkUiAnimatableProperty.TranslationY);
        proxy.Scale = 1;
        proxy.TranslationX = 0;
        proxy.TranslationY = 0;
        ((BindableObject)proxy).BindingContext = null;
        ReturnCell(proxy);
        if (commit && drag.Target != (drag.Group, drag.Index) && MoveItem(drag.Group, drag.Index, drag.Target.Group, drag.Target.Index, drag.Item))
            ReorderCompleted?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Moves the item at <paramref name="index"/> of <paramref name="group"/> to <paramref name="toIndex"/> of
    /// <paramref name="toGroup"/> (its index once moved), as MAUI's handlers do: <c>RemoveAt</c>, then <c>Insert</c>
    /// (another group's list first gets the item, so it never leaves the selection). The list keeps its scroll offset;
    /// lists that do not notify are read as if they had. <c>false</c> when the item is no longer there, or its place is not (the
    /// source changed while a released item settled).
    /// </summary>
    private bool MoveItem(int group, int index, int toGroup, int toIndex, object? item)
    {
        if (WritableList(group) is not { } from || WritableList(toGroup) is not { } to || index >= from.Count || !Equals(from[index], item)
            || toIndex > (ReferenceEquals(from, to) ? from.Count - 1 : to.Count))
            return false;
        var keepsOffset = ItemsHost.KeepsScrollOffset;
        _committingReorder = true;
        ItemsHost.KeepsScrollOffset = true;
        try
        {
            if (ReferenceEquals(from, to))
            {
                from.RemoveAt(index);
                from.Insert(toIndex, item);
                if (from is not INotifyCollectionChanged)
                    Model.Notify(group, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, item, toIndex, index));
            }
            else
            {
                to.Insert(toIndex, item);
                if (to is not INotifyCollectionChanged)
                    Model.Notify(toGroup, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, toIndex));
                from.RemoveAt(index);
                if (from is not INotifyCollectionChanged)
                    Model.Notify(group, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
            }
        }
        finally
        {
            ItemsHost.KeepsScrollOffset = keepsOffset;
            _committingReorder = false;
        }
        return true;
    }

    /// <summary>
    /// The source changed while an item is held (not by the drop itself): the drag ends without moving anything. A released
    /// item settling into its place still moves when it lands (an append from a load-more meanwhile changes nothing), unless
    /// it is no longer where it was (<see cref="MoveItem"/>).
    /// </summary>
    private void OnItemsChangedWhileDragging()
    {
        if (_drag is { Dropping: false } && !_committingReorder)
            CancelReorder();
    }

    /// <summary>The item being dragged and where it goes (tests): its group, index, target group and target index.</summary>
    internal (int Group, int Index, int TargetGroup, int TargetIndex)? ReorderState =>
        _drag is { } drag ? (drag.Group, drag.Index, drag.Target.Group, drag.Target.Index) : null;

    /// <summary>The proxy that shows the dragged item (tests).</summary>
    internal SkUiView? ReorderProxy => _drag?.Proxy;

    #endregion

    #region Screen readers

    /// <summary>
    /// The places one move takes the item <paramref name="host"/> shows to, backward and forward (one place, or across a
    /// group's edge with <see cref="CanMixGroups"/>); <c>null</c> where it cannot go.
    /// </summary>
    private ((int Group, int Index)? Backward, (int Group, int Index)? Forward) ReorderSteps(ItemHost host)
    {
        if (!_canReorderItems || _drag is not null || LocateHost(host) is not { } location || WritableList(location.Group) is null)
            return (null, null);
        var (group, index) = location;
        (int, int)? backward = index > 0 ? (group, index - 1) : null;
        (int, int)? forward = index < GroupCount(group) - 1 ? (group, index + 1) : null;
        if (group >= 0 && _canMixGroups)
        {
            if (backward is null)
                for (var other = group - 1; other >= 0 && backward is null; other--)
                    if (CanTake(other, group))
                        backward = (other, Model.Groups[other].Count);
            if (forward is null)
                for (var other = group + 1; other < Model.Groups.Count && forward is null; other++)
                    if (CanTake(other, group))
                        forward = (other, 0);
        }
        return (backward, forward);
    }

    /// <summary>The screen-reader texts of one move backward and forward: the direction one place goes on screen.</summary>
    private (string Backward, string Forward) ReorderStepTexts()
    {
        var strings = SkUiStrings.Current;
        // A list steps along its axis; a grid steps across it, within a row.
        var sideways = _horizontal == (Model.Span == 1);
        if (!sideways)
            return (strings.MoveUpText, strings.MoveDownText);
        return IsRightToLeft ? (strings.MoveRightText, strings.MoveLeftText) : (strings.MoveLeftText, strings.MoveRightText);
    }

    /// <summary>Offers the moves of <paramref name="host"/>'s item to screen readers.</summary>
    private void AddReorderActions(ItemHost host, SkUiSemanticsInfo info)
    {
        var (backward, forward) = ReorderSteps(host);
        if (backward is null && forward is null)
            return;
        var (backwardText, forwardText) = ReorderStepTexts();
        if (backward is not null)
            info.AddCustomAction(backwardText);
        if (forward is not null)
            info.AddCustomAction(forwardText);
    }

    /// <summary>A screen reader moves <paramref name="host"/>'s item (<paramref name="action"/>: the index among the moves offered).</summary>
    private bool PerformReorderAction(ItemHost host, int action)
    {
        var (backward, forward) = ReorderSteps(host);
        var step = action == 0 ? backward ?? forward : action == 1 && backward is not null ? forward : null;
        if (step is not { } to || LocateHost(host) is not { } from)
            return false;
        var item = Model.ItemAt(from.Group, from.Index);
        if (!MoveItem(from.Group, from.Index, to.Group, to.Index, item))
            return false;
        ReorderCompleted?.Invoke(this, EventArgs.Empty);
        // The screen reader stays on the item: it shows, and its container (another one now) takes the focus once laid out.
        var global = Model.GlobalIndex(to.Group, to.Index);
        _ = ScrollToIndex(global, ScrollToPosition.MakeVisible, animated: false);
        Microsoft.Maui.Dispatching.Dispatcher.GetForCurrentThread()?.Dispatch(() =>
        {
            if (RealizedHost(global) is { } moved)
                SkUiSemantics.SetSemanticFocus(moved);
        });
        return true;
    }

    #endregion

    #region Parts

    /// <summary>The container of the item under <paramref name="pointer"/> (a grid's cell), or <c>null</c> (spacing, a header).</summary>
    private ItemHost? ItemHostAt(SkUiPointer pointer)
    {
        var at = pointer.GetPosition(ItemsHost);
        foreach (var view in ItemsHost.RealizedViews)
        {
            if (view is not SkUiView row || !row.Frame.Contains(at))
                continue;
            if (row is ItemHost host)
                return host;
            if (row is GridRowPart rowPart)
            {
                var local = new Point(at.X - row.Frame.X, at.Y - row.Frame.Y);
                foreach (var cell in rowPart.Cells)
                    if (cell.Frame.Contains(local))
                        return cell;
            }
            return null;
        }
        return null;
    }

    /// <summary>
    /// Lifts the item under the pointer: a long press (touch, pen) or a primary-button press and drag (mouse), then moves it
    /// with the pointer until release. One for the rows (they stay attached while the item's container is recycled).
    /// Exclusive, so native ancestors stay out once it claims.
    /// </summary>
    private sealed class ReorderGesture(SkUiCollectionView owner) : SkUiGestureRecognizer
    {
        private ItemHost? _host;
        private long? _pointer;
        private bool _mouse;
        private bool _lifted;
        private SkUiPointer _last;
        private IDisposable? _timer;

        protected internal override bool IsExclusive => true;

        internal override bool HoldsNativeTaps => !_mouse;

        protected internal override bool OnPointerPressed(SkUiPointer pointer)
        {
            if (_pointer is not null || owner.ItemHostAt(pointer) is not { } host || !owner.CanLift(host))
                return false;
            _mouse = pointer.Device == SkUiPointerDevice.Mouse;
            if (_mouse && (pointer.Buttons & ~SkUiPointerButtons.Primary) != 0)
                return false;
            if (!_mouse)
            {
                var id = pointer.Id;
                _timer = SkUiGestureSettings.Schedule(SkUiGestureSettings.LongPressDuration, () => Lift(id));
                if (_timer is null)
                    return false; // no timer source
            }
            _host = host;
            _pointer = pointer.Id;
            _last = pointer;
            _lifted = false;
            return true;
        }

        protected internal override void OnPointerMoved(SkUiPointer pointer)
        {
            if (_pointer != pointer.Id)
                return;
            _last = pointer;
            if (_lifted)
            {
                owner.DragReorder(this, pointer);
                return;
            }
            var dx = pointer.TotalX;
            var dy = pointer.TotalY;
            if (dx * dx + dy * dy <= SkUiGestureSettings.TouchSlop * SkUiGestureSettings.TouchSlop)
                return;
            // Past the slop: a mouse lifts the item, a finger that has not held it long enough scrolls instead.
            if (_mouse)
                Lift(pointer.Id);
            else
                Resign();
        }

        protected internal override void OnPointerReleased(SkUiPointer pointer)
        {
            if (_pointer != pointer.Id)
                return;
            var lifted = _lifted;
            Reset();
            if (lifted)
                owner.DropReorder(this);
            else
                Resign();
        }

        protected internal override void OnRejected(long pointerId)
        {
            if (_pointer != pointerId)
                return;
            var lifted = _lifted;
            Reset();
            if (lifted)
                owner.CancelReorder(this);
        }

        private void Lift(long pointerId)
        {
            if (_pointer != pointerId || _lifted)
                return;
            _timer?.Dispose();
            _timer = null;
            Claim();
            if (_pointer != pointerId)
                return; // lost the arena
            _lifted = _host is { } host && owner.BeginReorder(host, this, _last);
            if (!_lifted)
                Resign();
        }

        private void Reset()
        {
            _timer?.Dispose();
            _timer = null;
            _host = null;
            _pointer = null;
            _lifted = false;
        }
    }

    /// <summary>
    /// Shows the proxy of a dragged item over the list and its sticky parts: input-transparent, hidden from screen readers,
    /// clipped to the list, and taking no room (the list arranges it over itself; the proxy keeps the item's size).
    /// </summary>
    private sealed class DragHost : SkUiView
    {
        private ItemHost? _proxy;
        private Size _size;

        public DragHost()
        {
            InputTransparent = true;
            AutomationProperties.SetExcludedWithChildren(this, true);
        }

        public ItemHost? Proxy => _proxy;

        public void Show(ItemHost? proxy, Size size)
        {
            if (ReferenceEquals(proxy, _proxy))
                return;
            if (_proxy is not null)
                DetachChild(_proxy);
            _proxy = proxy;
            _size = size;
            if (proxy is not null)
                AttachChild(proxy);
        }

        internal override IEnumerable<ISkUiView> SkiaChildren
        {
            get
            {
                if (_proxy is not null)
                    yield return _proxy;
            }
        }

        protected override Size MeasureContent(double widthConstraint, double heightConstraint)
        {
            if (_proxy is not null)
                ((IView)_proxy).Measure(_size.Width, _size.Height);
            return Size.Zero;
        }

        protected override void ArrangeContent(Size size) => ((IView?)_proxy)?.Arrange(new Rect(Point.Zero, _size));
    }

    #endregion
}
