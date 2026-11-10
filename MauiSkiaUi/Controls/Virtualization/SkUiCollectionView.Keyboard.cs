using MauiSkiaUi.Rendering;

namespace MauiSkiaUi;

// Keyboard item navigation: the list is one tab stop with a current item, moved by the arrow keys, Home / End and the page
// keys, and activated by Space / Enter as a tap would. The current item is kept by item (and group), not by index or
// container, so it survives collection changes, recycling and scrolling out of view; its container draws the focus ring
// while the list has keyboard focus from the keyboard.
public partial class SkUiCollectionView : ISkUiKeyHandler, ISkUiItemFocusTarget
{
    private enum CurrentKind : byte { None, Item, Header }

    private CurrentKind _currentKind;
    private object? _currentItem;
    // The group's value: the current item's group (grouped lists), or the group whose header is current.
    private object? _currentGroup;
    // Where the current item (a global item index) or header (a group index) was last found: checked first, and the place of
    // the neighbor that takes over when the current item is removed.
    private int _currentIndex = -1;
    // The slot across a grid row that moves along the rows keep, also through shorter rows.
    private int _currentSlot;
    // The list has keyboard focus that came from the keyboard: the current item's container draws the ring.
    private bool _ringShown;
    private SkUiView? _ringHost;
    // Whether the list had keyboard focus at the last focus change (IsFocused is already set when the hook runs).
    private bool _hadFocus;

    /// <summary>A list takes keyboard focus while it is interactive: items take taps, or groups expand and collapse.</summary>
    internal override bool TakesKeyboardFocus => WantsItemTaps || HeaderStops;

    /// <summary>The ring is drawn on the current item's container (on the list itself only while it has nothing to stop on).</summary>
    internal override bool ShowsFocusRing => false;

    /// <summary>Whether group headers are stops of the arrow keys (they expand and collapse their group).</summary>
    private bool HeaderStops => AllowGroupExpandCollapse && Model.HasGroupHeaders;

    /// <summary>The global index of the current item, or -1 (tests, diagnostics).</summary>
    internal int CurrentItemIndex => _currentKind == CurrentKind.Item ? ResolveCurrent() : -1;

    /// <summary>The group whose header is current, or -1 (tests, diagnostics).</summary>
    internal int CurrentHeaderGroup => _currentKind == CurrentKind.Header ? ResolveCurrent() : -1;

    /// <summary>The container drawing the focus ring, or <c>null</c> (tests).</summary>
    internal SkUiView? FocusRingHost => _ringHost;

    private protected override void OnKeyboardFocusChanged(bool focused, bool ringVisible)
    {
        var arriving = focused && !_hadFocus;
        _hadFocus = focused;
        if (arriving && ResolveCurrent() < 0)
            ChooseInitialCurrent();
        _ringShown = ringVisible;
        RefreshRing();
        if (arriving && ringVisible)
            RevealCurrent();
    }

    void ISkUiItemFocusTarget.MakeCurrent(ISkUiRenderable inside)
    {
        for (var current = inside; current is not null && !ReferenceEquals(current, this); current = current.RenderParent)
        {
            if (current is ItemHost host)
            {
                if (LocateHost(host) is { } location)
                    SetCurrentItem(location.Group, location.Index);
                return;
            }
            if (current is GroupHeaderHost header)
            {
                if (GroupOfHeader(header) is >= 0 and var group && HeaderStops)
                    SetCurrentHeader(group);
                return;
            }
        }
    }

    bool ISkUiKeyHandler.OnKeyDown(in SkUiKeyEvent key)
    {
        // Ctrl / Command take part in an extended selection's keys (move without selecting, toggle, select all); Alt never.
        if (!IsVisualStateEnabled || (key.HasCommandModifier && ((key.Modifiers & SkUiKeyModifiers.Alt) != 0 || !ExtendsSelection)))
            return false;
        if (!IsFocused)
            return key.Key is not (SkUiKey.Enter or SkUiKey.Space or SkUiKey.Tab or SkUiKey.Escape or SkUiKey.Character or SkUiKey.None)
                && TakeFocusFromItem() && Navigate(key);
        return Navigate(key);
    }

    /// <summary>
    /// A navigation key while a view inside an item has focus (a button in the item): that item becomes current and the list
    /// takes focus, so the key moves on from it. Text fields keep their keys.
    /// </summary>
    private bool TakeFocusFromItem()
    {
        if (SkUiSemantics.RootOf(this)?.FocusManager is not { Focused: { } focused } manager || focused is ISkUiTextInputNode)
            return false;
        for (ISkUiRenderable? current = focused; current is not null && !ReferenceEquals(current, this); current = current.RenderParent)
        {
            if (current is ItemHost host)
            {
                if (LocateHost(host) is not { } location)
                    return false;
                SetCurrentItem(location.Group, location.Index);
                return manager.Focus(this, ring: true);
            }
            if (current is GroupHeaderHost header)
            {
                if (GroupOfHeader(header) is not (>= 0 and var group) || !HeaderStops)
                    return false;
                SetCurrentHeader(group);
                return manager.Focus(this, ring: true);
            }
        }
        return false;
    }

    private bool Navigate(in SkUiKeyEvent key)
    {
        if (key.Key is SkUiKey.Enter or SkUiKey.Space)
            return Activate(key);
        if (key.Key == SkUiKey.Character && ExtendsSelection && HasToggleModifier(key.Modifiers) && char.ToLowerInvariant(key.Character) == 'a')
        {
            if (!key.IsRepeat)
                SelectAllByGesture();
            return true;
        }
        var (kind, index) = (_currentKind, ResolveCurrent());
        if (!Move(key))
            return false;
        // An extended selection follows the moves of the current item.
        if (ExtendsSelection && (_currentKind != kind || ResolveCurrent() != index))
            SelectAfterMove(key.Modifiers, kind == CurrentKind.Item ? index : -1);
        return true;
    }

    /// <summary>The arrow, Home / End and page keys: moves the current stop; returns whether the key was used.</summary>
    private bool Move(in SkUiKeyEvent key)
    {
        if (key.Key is not (SkUiKey.Left or SkUiKey.Right or SkUiKey.Up or SkUiKey.Down or SkUiKey.Home or SkUiKey.End
            or SkUiKey.PageUp or SkUiKey.PageDown))
            return false;
        if (CurrentRow(out var slot) is not (>= 0 and var row))
        {
            // Nothing current yet (or it left): the first key picks the starting item and shows it.
            if (!ChooseInitialCurrent())
                return false;
            RevealCurrent();
            return true;
        }
        var arrow = key.Key;
        // Right to left, Left and Right swap: items and grid cells run from the right.
        if (IsRightToLeft && arrow is SkUiKey.Left or SkUiKey.Right)
            arrow = arrow == SkUiKey.Left ? SkUiKey.Right : SkUiKey.Left;
        var (alongBack, alongOn, acrossBack, acrossOn) = _horizontal
            ? (SkUiKey.Left, SkUiKey.Right, SkUiKey.Up, SkUiKey.Down)
            : (SkUiKey.Up, SkUiKey.Down, SkUiKey.Left, SkUiKey.Right);
        if (arrow == alongBack || arrow == alongOn)
            return MoveAlong(row, arrow == alongOn);
        if (arrow == acrossBack || arrow == acrossOn)
            return MoveAcross(row, slot, arrow == acrossOn);
        return arrow switch
        {
            SkUiKey.Home => MoveTo(NextStop(-1, 1), 0),
            SkUiKey.End => MoveTo(NextStop(Model.RowCount, -1), int.MaxValue),
            _ => Page(row, arrow == SkUiKey.PageDown)
        };
    }

    /// <summary>To the previous or next stop along the rows, keeping the slot across a grid row.</summary>
    private bool MoveAlong(int row, bool forward)
    {
        var target = NextStop(row, forward ? 1 : -1);
        return target >= 0 && MoveTo(target, _currentSlot, keepSlot: true);
    }

    /// <summary>
    /// Across the rows: to the previous or next item of a grid (on to the next row at its end); on a group header, collapses
    /// (back) or expands (on) the group, and on into its first item when it is expanded; back from an item of a one-column
    /// list, to its group's header.
    /// </summary>
    private bool MoveAcross(int row, int slot, bool forward)
    {
        var current = Model.RowAt(row);
        if (current.Kind == RowKind.GroupHeader)
        {
            var group = current.Group;
            if (forward == Model.Groups[group].Expanded)
                return !forward || NextStop(row, 1) is not (>= 0 and var first) || Model.RowAt(first).Group != group || MoveTo(first, 0);
            ChangeGroupExpanded(group, forward, fromGroup: false);
            return true;
        }
        if (Model.Span > 1)
        {
            var next = slot + (forward ? 1 : -1);
            if (next >= 0 && next < current.Count)
                return MoveTo(row, next);
            var target = NextStop(row, forward ? 1 : -1);
            return target >= 0 && MoveTo(target, forward ? 0 : int.MaxValue);
        }
        if (!forward && current.Group >= 0 && HeaderStops && Model.HeaderRowOf(current.Group) is >= 0 and var header)
            return MoveTo(header, 0);
        return false;
    }

    /// <summary>
    /// A page back or on: to the stop about as far as the part of the viewport the insets and sticky parts leave uncovered,
    /// so the item just past what showed comes into view at the other edge.
    /// </summary>
    private bool Page(int row, bool forward)
    {
        var (insetStart, insetEnd) = Controller.EffectiveInsets;
        var viewport = _horizontal ? Controller.Viewport.Width : Controller.Viewport.Height;
        var band = Math.Max(0, viewport - insetStart - insetEnd - StickyGroupInset(row));
        var start = ItemsHost.RowOffset(row);
        var end = start + ItemsHost.RowSize(row);
        int target;
        if (forward)
        {
            var limit = start + band;
            target = ItemsHost.RowAtOffset(Math.Min(limit, Math.Max(0, ItemsHost.TotalLength - PageTolerance)));
            // The last row that ends within a band from the current row's start (at least the next one).
            if (target > row + 1 && ItemsHost.RowOffset(target) + ItemsHost.RowSize(target) > limit + PageTolerance)
                target--;
            target = StopNear(target, row, forward: true);
        }
        else
        {
            var limit = end - band;
            target = ItemsHost.RowAtOffset(Math.Max(0, limit));
            if (target < row - 1 && ItemsHost.RowOffset(target) < limit - PageTolerance)
                target++;
            target = StopNear(target, row, forward: false);
        }
        return target >= 0 && MoveTo(target, _currentSlot, keepSlot: true);
    }

    // Offsets this close count as the same place (estimated sizes are fractional).
    private const double PageTolerance = 0.5;

    /// <summary>The stop at <paramref name="target"/> or the nearest one towards <paramref name="from"/> (exclusive); else the next one past <paramref name="from"/>.</summary>
    private int StopNear(int target, int from, bool forward)
    {
        var step = forward ? -1 : 1;
        for (var row = target; forward ? row > from : row < from; row += step)
            if (IsStop(row))
                return row;
        return NextStop(from, forward ? 1 : -1);
    }

    /// <summary>
    /// Space / Enter: activates the current item as a tap would, or expands / collapses the current group. In an extended
    /// selection Space adds or removes the current item (Shift+Space: the range from the anchor), as a Ctrl+click, and Enter
    /// taps it without changing the selection.
    /// </summary>
    private bool Activate(in SkUiKeyEvent key)
    {
        if (CurrentRow(out var slot) is not (>= 0 and var row))
            return false;
        // A held key is used without activating again (a held Enter would select and tap many times).
        if (key.IsRepeat)
            return true;
        var current = Model.RowAt(row);
        if (current.Kind == RowKind.GroupHeader)
        {
            ChangeGroupExpanded(current.Group, !Model.Groups[current.Group].Expanded, fromGroup: false);
            return true;
        }
        if (!WantsItemTaps)
            return false;
        var gesture = !ExtendsSelection ? SelectionGesture.Tap
            : key.Key == SkUiKey.Enter ? SelectionGesture.None
            : (key.Modifiers & SkUiKeyModifiers.Shift) != 0 ? SelectionGesture.Range
            : SelectionGesture.Toggle;
        ActivateItem(current.Group, current.Start + slot, gesture);
        return true;
    }

    /// <summary>Makes the stop at <paramref name="row"/> current (its item at <paramref name="slot"/>, clamped) and shows it.</summary>
    private bool MoveTo(int row, int slot, bool keepSlot = false)
    {
        if (row < 0 || row >= Model.RowCount)
            return false;
        var target = Model.RowAt(row);
        if (target.Kind == RowKind.GroupHeader)
            SetCurrentHeader(target.Group, keepSlot);
        else
            SetCurrentItem(target.Group, target.Start + Math.Clamp(slot, 0, target.Count - 1), keepSlot);
        RevealCurrent();
        return true;
    }

    private bool IsStop(int row) => Model.RowAt(row).Kind switch
    {
        RowKind.Items => true,
        RowKind.GroupHeader => HeaderStops,
        _ => false
    };

    /// <summary>The first stop after (<paramref name="step"/> 1) or before (-1) <paramref name="row"/>, or -1.</summary>
    private int NextStop(int row, int step)
    {
        for (var next = row + step; next >= 0 && next < Model.RowCount; next += step)
            if (IsStop(next))
                return next;
        return -1;
    }

    /// <summary>Makes the item at <paramref name="index"/> of <paramref name="group"/> current.</summary>
    private void SetCurrentItem(int group, int index, bool keepSlot = false)
    {
        _currentKind = CurrentKind.Item;
        _currentItem = Model.ItemAt(group, index);
        _currentGroup = group < 0 ? null : Model.Groups[group].Value;
        _currentIndex = Model.GlobalIndex(group, index);
        if (!keepSlot && Model.RowOfItem(group, index) is >= 0 and var row)
            _currentSlot = index - Model.RowAt(row).Start;
        RefreshRing();
    }

    /// <summary>Makes the header of the group at <paramref name="group"/> current, while headers are stops.</summary>
    private void SetCurrentHeader(int group, bool keepSlot = false)
    {
        if (!HeaderStops)
            return;
        _currentKind = CurrentKind.Header;
        _currentItem = null;
        _currentGroup = Model.Groups[group].Value;
        _currentIndex = group;
        if (!keepSlot)
            _currentSlot = 0;
        RefreshRing();
    }

    private void ClearCurrent()
    {
        _currentKind = CurrentKind.None;
        _currentItem = _currentGroup = null;
        _currentIndex = -1;
        _currentSlot = 0;
        RefreshRing();
    }

    /// <summary>
    /// Where the current item or header is now (a global item index or a group index), found again when the items moved;
    /// -1 when there is none or it left the list (<see cref="_currentIndex"/> then still holds where it was).
    /// </summary>
    private int ResolveCurrent()
    {
        switch (_currentKind)
        {
            case CurrentKind.Header:
            {
                var groups = Model.Groups;
                if ((uint)_currentIndex < (uint)groups.Count && SameGroup(groups[_currentIndex].Value))
                    return _currentIndex;
                var found = Model.IndexOfGroup(_currentGroup);
                if (found >= 0)
                    _currentIndex = found;
                return found;
            }
            case CurrentKind.Item:
                return ResolveItem(_currentItem, _currentGroup, ref _currentIndex);
            default:
                return -1;
        }
    }

    // How far around its last place an item is looked for before the whole list is searched.
    private const int NearbyProbe = 4;

    /// <summary>
    /// Where <paramref name="item"/> of the group <paramref name="group"/> (its value; <c>null</c> in a flat list) is now, as a
    /// global index: at <paramref name="index"/>, where it was last found, then a few places around it (items inserted or
    /// removed before it move it by a few), before a search of the whole list, so a stream of single inserts at the start
    /// never searches the list for each one. Updates <paramref name="index"/> when found; -1 when it left the list
    /// (<paramref name="index"/> then still holds where it was).
    /// </summary>
    private int ResolveItem(object? item, object? group, ref int index)
    {
        for (var distance = 0; distance <= NearbyProbe; distance++)
        {
            if (IsItemAt(index + distance, item, group))
                return index += distance;
            if (distance > 0 && IsItemAt(index - distance, item, group))
                return index -= distance;
        }
        var found = Model.Grouped && Model.IndexOfGroup(group) is >= 0 and var inGroup ? Model.IndexOfItem(item, inGroup) : -1;
        if (found < 0)
            found = Model.IndexOfItem(item);
        if (found >= 0)
            index = found;
        return found;
    }

    private bool IsItemAt(int index, object? item, object? group)
    {
        if ((uint)index >= (uint)Model.ItemCount)
            return false;
        var (at, inGroup) = Model.Locate(index);
        return Equals(Model.ItemAt(at, inGroup), item) && (at < 0 || IsGroup(Model.Groups[at].Value, group));
    }

    private static bool IsGroup(object? value, object? group) => ReferenceEquals(value, group) || Equals(value, group);

    private bool SameGroup(object? value) => IsGroup(value, _currentGroup);

    /// <summary>The row of the current stop and the slot of its item in the row, or -1 (none, or its group is collapsed).</summary>
    private int CurrentRow(out int slot)
    {
        slot = 0;
        var index = ResolveCurrent();
        if (index < 0)
            return -1;
        if (_currentKind == CurrentKind.Header)
            return HeaderStops ? Model.HeaderRowOf(index) : -1;
        var (group, inGroup) = Model.Locate(index);
        var row = Model.RowOfItem(group, inGroup);
        if (row >= 0)
            slot = inGroup - Model.RowAt(row).Start;
        return row;
    }

    /// <summary>
    /// Focus arrived with nothing current: the selected item (single selection), the item a tap last selected (multiple),
    /// else the first stop in view (the first one before the list is laid out). Returns whether one was chosen.
    /// </summary>
    private bool ChooseInitialCurrent()
    {
        var selected = _selectionMode switch
        {
            SkUiSelectionMode.Single or SkUiSelectionMode.SingleDeselect when _selectedItem is { } item =>
                Equals(item, _tappedSelection) ? RowOfTapped() : RowOfItem(item),
            SkUiSelectionMode.Multiple when _tappedSelection is { } tapped && _selectedSet.Contains(tapped) => RowOfTapped(),
            _ => -1
        };
        if (selected >= 0)
        {
            var row = Model.RowAt(selected);
            var item = _selectionMode == SkUiSelectionMode.Multiple ? _tappedSelection : _selectedItem;
            for (var slot = 0; slot < row.Count; slot++)
                if (Equals(Model.ItemAt(row.Group, row.Start + slot), item))
                {
                    SetCurrentItem(row.Group, row.Start + slot);
                    return true;
                }
        }
        var first = ItemsHost.FirstIndexInBand;
        var stop = first >= 0 && first < Model.RowCount && IsStop(first) ? first : NextStop(Math.Max(-1, first - 1), 1);
        if (stop < 0)
            stop = NextStop(-1, 1);
        if (stop < 0)
        {
            ClearCurrent();
            return false;
        }
        var target = Model.RowAt(stop);
        if (target.Kind == RowKind.GroupHeader)
            SetCurrentHeader(target.Group);
        else
            SetCurrentItem(target.Group, target.Start);
        return true;
    }

    /// <summary>
    /// Scrolls the current stop into view where sticky parts do not cover it, at once (as native lists follow their keyboard
    /// focus: a held arrow key never waits for an animation).
    /// </summary>
    private void RevealCurrent()
    {
        if (CurrentRow(out _) is >= 0 and var row)
            _ = ItemsHost.ScrollToIndex(row, ScrollToPosition.MakeVisible, animated: false);
    }

    /// <summary>The items changed: a removed current item passes to its neighbor (the item now at its place); a reset forgets it.</summary>
    private void OnItemsChangedKeepCurrent(bool reset)
    {
        if (_currentKind == CurrentKind.None || ResolveCurrent() >= 0)
        {
            RefreshRing();
            return;
        }
        if (reset)
        {
            ClearCurrent();
            return;
        }
        if (_currentKind == CurrentKind.Header)
        {
            if (Model.Groups.Count > 0 && HeaderStops)
                SetCurrentHeader(Math.Clamp(_currentIndex, 0, Model.Groups.Count - 1), keepSlot: true);
            else
                ClearCurrent();
            return;
        }
        if (Model.ItemCount == 0)
        {
            ClearCurrent();
            return;
        }
        var (group, index) = Model.Locate(Math.Clamp(_currentIndex, 0, Model.ItemCount - 1));
        SetCurrentItem(group, index, keepSlot: true);
    }

    /// <summary>A group collapsed: its current item passes to the group's header (a stop), else to the nearest stop.</summary>
    private void OnGroupCollapsedKeepCurrent(int group)
    {
        if (_currentKind != CurrentKind.Item || ResolveCurrent() is not (>= 0 and var index) || Model.Locate(index).Group != group)
            return;
        if (HeaderStops)
        {
            SetCurrentHeader(group);
            return;
        }
        var from = Model.Groups[group].FirstRow;
        var stop = NextStop(from - 1, 1);
        if (stop < 0)
            stop = NextStop(from, -1);
        if (stop < 0)
            return; // every group collapsed: the item stays current, shown again when its group expands
        var target = Model.RowAt(stop);
        SetCurrentItem(target.Group, target.Start);
    }

    /// <summary>What can be a stop changed (headers became stops or stopped being ones): focus and the current stop follow.</summary>
    private void OnCurrentStopsChanged()
    {
        RevalidateFocus();
        if (_currentKind == CurrentKind.Header && !HeaderStops)
            ClearCurrent();
        else
            RefreshRing();
    }

    private int GroupOfHeader(GroupHeaderHost header) =>
        header.StickyGroup >= 0 ? header.StickyGroup
        : ItemsHost.IndexOfRealizedView(header) is >= 0 and var row ? Model.RowAt(row).Group : -1;

    /// <summary>Whether the header of <paramref name="group"/> draws the ring (the sticky header too).</summary>
    private bool ShowsRingOnHeader(int group) =>
        _ringShown && _currentKind == CurrentKind.Header && HeaderStops && ResolveCurrent() == group;

    /// <summary>
    /// Puts the ring on the current stop's container while the list has focus from the keyboard (on the list itself while it
    /// has no stops), and takes it off the container that had it.
    /// </summary>
    private void RefreshRing()
    {
        SkUiView? target = null;
        var hasStops = false;
        if (_ringShown)
        {
            hasStops = Model.ItemCount > 0 || (HeaderStops && Model.Groups.Count > 0);
            if (CurrentRow(out var slot) is >= 0 and var row)
                target = ItemsHost.GetRealizedView(row) switch
                {
                    GridRowPart rowPart => rowPart.CellAt(slot),
                    SkUiView view => view,
                    _ => null
                };
        }
        ShowRingOn(target);
        SetFocusRingVisible(_ringShown && !hasStops);
        if (_stickyGroupHeader is { } sticky)
            sticky.SetFocusRingVisible(_stickyGroup >= 0 && ShowsRingOnHeader(_stickyGroup));
    }

    private void ShowRingOn(SkUiView? host)
    {
        if (!ReferenceEquals(_ringHost, host))
        {
            _ringHost?.SetFocusRingVisible(false);
            _ringHost = host;
        }
        host?.SetFocusRingVisible(true);
    }

    /// <summary>A row was bound to a container: the ring follows when the row (or a cell of it) is the current stop.</summary>
    private void RingAfterBind(int row, ISkUiView view)
    {
        if (!_ringShown && _ringHost is null)
            return;
        if (CurrentRow(out var slot) == row && _ringShown)
            ShowRingOn(view is GridRowPart rowPart ? rowPart.CellAt(slot) : view as SkUiView);
        else if (HoldsRing(view))
            ShowRingOn(null);
    }

    /// <summary>A container was released or recycled: it draws no ring.</summary>
    private void RingAfterUnbind(ISkUiView view)
    {
        if (_ringHost is not null && HoldsRing(view))
            ShowRingOn(null);
    }

    private bool HoldsRing(ISkUiView view) =>
        ReferenceEquals(view, _ringHost) || (view is GridRowPart rowPart && _ringHost is ItemHost cell && rowPart.IndexOf(cell) >= 0);
}
