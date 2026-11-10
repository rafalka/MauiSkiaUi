namespace MauiSkiaUi;

// Extended selection (desktop lists): with IsExtendedSelectionEnabled and SelectionMode Multiple, a click selects only its
// item, Ctrl / Command+click toggles one, Shift+click selects the range from the anchor. The keyboard never drops the
// selection by moving: the arrow keys move the current item (the focus ring) alone, Space / Insert toggle it, Shift+arrows
// toggle the items they move onto, past where the current item was when the range started (that item keeps its state).
// Each gesture is one change of SelectedItems: one cancelable SelectionChanging, one SelectionChanged, one command run.
public partial class SkUiCollectionView
{
    /// <summary>How a gesture changes the selection.</summary>
    private enum SelectionGesture : byte
    {
        /// <summary>The selection mode's tap: select (single), toggle (multiple).</summary>
        Tap,
        /// <summary>Only the item is selected.</summary>
        Only,
        /// <summary>The item is added or removed.</summary>
        Toggle,
        /// <summary>The items from the anchor to the item, only.</summary>
        Range,
        /// <summary>The items from the anchor to the item, added to the selection.</summary>
        AddRange,
        /// <summary>The selection stays.</summary>
        None
    }

    private bool _extendedSelection;
    // Where ranges start: the item a click, a toggle or an unextended move last selected (and its group, and where it was).
    private object? _anchorItem;
    private object? _anchorGroup;
    private int _anchorIndex = -1;
    // A range the keyboard is extending (Shift+arrows, Shift+Space): the selection it toggles, taken when it started, and
    // the selection it left (a change of the selection by anything else starts the next range from what is selected then).
    private IReadOnlyList<object>? _rangeBase;
    private IReadOnlyList<object>? _rangeResult;

    /// <summary>Bindable property for <see cref="IsExtendedSelectionEnabled"/>.</summary>
    public static readonly BindableProperty IsExtendedSelectionEnabledProperty = BindableProperty.Create(nameof(IsExtendedSelectionEnabled),
        typeof(bool), typeof(SkUiCollectionView), false,
        propertyChanged: (view, _, value) => ((SkUiCollectionView)view)._extendedSelection = (bool)value);

    /// <summary>
    /// Desktop selection with <see cref="SelectionMode"/> <see cref="SkUiSelectionMode.Multiple"/> (default <c>false</c>):
    /// a click selects only its item, Ctrl+click (Command+click on Apple) adds or removes one, Shift+click selects the
    /// items from the last clicked one (Ctrl+Shift+click adds them). With the keyboard, the arrow, Home / End and page keys
    /// move the current item (the focus ring) and leave the selection as it is; Space adds or removes the current item,
    /// Insert does and moves on to the next one, Shift with the arrows toggles the items it moves onto past where the current
    /// item was when Shift was first pressed (selected ones are deselected; the item it started on keeps its state, so a
    /// click then Shift+Down selects both), Shift+Space toggles them up to the current item, Ctrl / Command+A selects all,
    /// and Enter taps the item (<see cref="ItemTapped"/>: opens it) without selecting. Modifier clicks, Space and Insert
    /// do not raise <see cref="ItemTapped"/>. Touch keeps the multiple selection's toggling tap. Ranges run in source order
    /// across groups; items of collapsed groups are left out.
    /// </summary>
    public bool IsExtendedSelectionEnabled
    {
        get => (bool)GetValue(IsExtendedSelectionEnabledProperty);
        set => SetValue(IsExtendedSelectionEnabledProperty, value);
    }

    /// <summary>Sets <see cref="IsExtendedSelectionEnabled"/> (same as the property setter).</summary>
    public SkUiCollectionView SetIsExtendedSelectionEnabled(bool value)
    {
        IsExtendedSelectionEnabled = value;
        return this;
    }

    /// <summary>Whether gestures select as desktop lists do.</summary>
    private bool ExtendsSelection => _extendedSelection && _selectionMode == SkUiSelectionMode.Multiple;

    private static bool HasToggleModifier(SkUiKeyModifiers modifiers) => (modifiers & (SkUiKeyModifiers.Control | SkUiKeyModifiers.Command)) != 0;

    /// <summary>What a click with <paramref name="modifiers"/> does to an extended selection.</summary>
    private static SelectionGesture ClickGesture(SkUiKeyModifiers modifiers) =>
        (modifiers & SkUiKeyModifiers.Shift) != 0
            ? HasToggleModifier(modifiers) ? SelectionGesture.AddRange : SelectionGesture.Range
            : HasToggleModifier(modifiers) ? SelectionGesture.Toggle : SelectionGesture.Only;

    /// <summary>Makes the item at <paramref name="index"/> of <paramref name="group"/> the anchor of ranges.</summary>
    private void SetAnchor(int group, int index)
    {
        _anchorItem = Model.ItemAt(group, index);
        _anchorGroup = group < 0 ? null : Model.Groups[group].Value;
        _anchorIndex = Model.GlobalIndex(group, index);
        _rangeBase = null; // a range from here toggles what is selected when it starts
    }

    /// <summary>Forgets the anchor and the keyboard's range (the items were reset).</summary>
    private void ForgetAnchor()
    {
        _anchorItem = _anchorGroup = null;
        _anchorIndex = -1;
        _rangeBase = _rangeResult = null;
    }

    /// <summary>The anchor's global index, or -1 (none, or it left the list).</summary>
    private int ResolveAnchor() => _anchorIndex < 0 ? -1 : ResolveItem(_anchorItem, _anchorGroup, ref _anchorIndex);

    /// <summary>
    /// The selection change of an extended gesture on the item at <paramref name="index"/> of <paramref name="group"/>, in one
    /// step (cancelable by <see cref="SelectionChanging"/>). Every gesture but a range makes the item the anchor.
    /// </summary>
    private void SelectByGesture(int group, int index, SelectionGesture gesture)
    {
        var item = Model.ItemAt(group, index);
        var global = Model.GlobalIndex(group, index);
        IReadOnlyList<object>? selection = null;
        switch (gesture)
        {
            case SelectionGesture.Only:
                selection = item is null ? [] : [item];
                break;
            case SelectionGesture.Toggle when item is not null:
                selection = _selectedSet.Contains(item)
                    ? _selectedSnapshot.Where(selected => !Equals(selected, item)).ToArray()
                    : [.. _selectedSnapshot, item];
                break;
            case SelectionGesture.Range or SelectionGesture.AddRange:
            {
                var anchor = ResolveAnchor();
                var range = ItemsBetween(anchor < 0 ? global : anchor, global);
                if (gesture == SelectionGesture.AddRange)
                {
                    var added = _selectedSnapshot.ToList();
                    foreach (var ranged in range)
                        if (!_selectedSet.Contains(ranged))
                            added.Add(ranged);
                    range = added;
                }
                selection = range;
                if (anchor < 0)
                    SetAnchor(group, index);
                break;
            }
        }
        if (gesture is not (SelectionGesture.Range or SelectionGesture.AddRange or SelectionGesture.None))
            SetAnchor(group, index);
        if (selection is null)
            return;
        var before = _selectedSnapshot;
        ChangeSelectionByGesture(selection);
        // A Shift+click's range goes on with Shift+arrows from where it ended (they toggle the items past the anchor, so
        // the range's start is what the range toggles: the selection with those items toggled back).
        if (gesture is SelectionGesture.Range or SelectionGesture.AddRange && !ReferenceEquals(before, _selectedSnapshot))
        {
            _rangeBase = Toggled(_selectedSnapshot, ItemsPastAnchor(ResolveAnchor(), global));
            _rangeResult = _selectedSnapshot;
        }
    }

    /// <summary>
    /// The items from global index <paramref name="from"/> to <paramref name="to"/> (either way round), in source order, each
    /// once (an item may show in several groups), without the items of collapsed groups.
    /// </summary>
    private List<object> ItemsBetween(int from, int to)
    {
        var (first, last) = from <= to ? (from, to) : (to, from);
        var items = new List<object>(last - first + 1);
        var seen = new HashSet<object>();
        for (var index = first; index <= last; index++)
        {
            var (group, inGroup) = Model.Locate(index);
            if (group >= 0 && !Model.Groups[group].Expanded)
                continue;
            if (Model.ItemAt(group, inGroup) is { } item && seen.Add(item))
                items.Add(item);
        }
        return items;
    }

    /// <summary>Selects every item (Ctrl / Command+A) as one gesture: cancelable, one change.</summary>
    private void SelectAllByGesture()
    {
        var all = new List<object>();
        var seen = new HashSet<object>();
        for (var index = 0; index < Model.ItemCount; index++)
        {
            var (group, item) = Model.Locate(index);
            if (Model.ItemAt(group, item) is { } value && seen.Add(value))
                all.Add(value);
        }
        // The current item is the selection's focus (KeepSelectionVisible keeps it in view, not the first item).
        var current = CurrentItemIndex;
        if (current >= 0)
        {
            var (group, index) = Model.Locate(current);
            _tapSelecting = Model.ItemAt(group, index);
            _tapSelectingIndex = current;
        }
        try
        {
            ChangeSelectionByGesture(all);
        }
        finally
        {
            _tapSelecting = null;
        }
    }

    /// <summary>
    /// Replaces <see cref="SelectedItems"/> with <paramref name="selection"/> when it differs: <see cref="SelectionChanging"/>
    /// first (it may cancel), then one change (one <see cref="SelectionChanged"/>, one command run).
    /// </summary>
    private void ChangeSelectionByGesture(IReadOnlyList<object> selection)
    {
        if (selection.Count == _selectedSnapshot.Count && selection.SequenceEqual(_selectedSnapshot))
            return;
        var args = new SkUiSelectionChangingEventArgs(_selectedSnapshot, selection);
        SelectionChanging?.Invoke(this, args);
        if (!args.Cancel)
            ReplaceSelectedItems(selection);
    }

    /// <summary>
    /// The current item moved by a key in an extended selection: without Shift the selection stays and the range anchor moves
    /// with it; with Shift the items past the anchor (where the move started when there is none) up to the current item are
    /// toggled in the selection the range started from (moving back gives the items left their state again).
    /// </summary>
    private void SelectAfterMove(SkUiKeyModifiers modifiers, int startedAt)
    {
        if (_currentKind != CurrentKind.Item || ResolveCurrent() is not (>= 0 and var current))
            return;
        var (group, index) = Model.Locate(current);
        if ((modifiers & SkUiKeyModifiers.Shift) == 0)
        {
            SetAnchor(group, index);
            return;
        }
        if (ResolveAnchor() < 0 && startedAt >= 0)
        {
            var (anchorGroup, anchorIndex) = Model.Locate(startedAt);
            SetAnchor(anchorGroup, anchorIndex);
        }
        SelectByKey(group, index, extendRange: true);
    }

    /// <summary>
    /// Space / Insert on the item at <paramref name="index"/> of <paramref name="group"/> in an extended selection: toggles
    /// it (and anchors there), or with <paramref name="extendRange"/> toggles the range from the anchor. Selects only: the item
    /// is not tapped (Enter is).
    /// </summary>
    private void SelectByKey(int group, int index, bool extendRange)
    {
        _tapSelecting = Model.ItemAt(group, index); // the selection's focus, for KeepSelectionVisible
        _tapSelectingIndex = Model.GlobalIndex(group, index);
        try
        {
            if (extendRange)
                ExtendRange(group, index);
            else
                SelectByGesture(group, index, SelectionGesture.Toggle);
        }
        finally
        {
            _tapSelecting = null;
        }
    }

    /// <summary>
    /// The keyboard's range: what was selected when it started with the items past the anchor up to the item toggled
    /// (selected ones deselected, the others selected), so the range can grow and shrink without losing the rest of the
    /// selection. The anchor keeps its state: it is the item just clicked or toggled, or where the moves started.
    /// </summary>
    private void ExtendRange(int group, int index)
    {
        var anchor = ResolveAnchor();
        if (anchor < 0)
            SetAnchor(group, index);
        // Started now, or the selection changed since this range left it: the range toggles what is selected now.
        if (_rangeBase is null || !ReferenceEquals(_rangeResult, _selectedSnapshot))
            _rangeBase = _selectedSnapshot;
        ChangeSelectionByGesture(Toggled(_rangeBase, ItemsPastAnchor(anchor, Model.GlobalIndex(group, index))));
        _rangeResult = _selectedSnapshot;
    }

    /// <summary>The items past global index <paramref name="anchor"/> up to <paramref name="target"/> (either way), or none.</summary>
    private List<object> ItemsPastAnchor(int anchor, int target) =>
        anchor < 0 || anchor == target ? [] : anchor < target ? ItemsBetween(anchor + 1, target) : ItemsBetween(target, anchor - 1);

    /// <summary><paramref name="selection"/> with <paramref name="items"/> toggled: the selected ones left out, the others added.</summary>
    private static List<object> Toggled(IReadOnlyList<object> selection, List<object> items)
    {
        var toggled = new HashSet<object>(items);
        var selected = new HashSet<object>(selection);
        var result = new List<object>(selection.Count + items.Count);
        foreach (var item in selection)
            if (!toggled.Contains(item))
                result.Add(item);
        foreach (var item in items)
            if (!selected.Contains(item))
                result.Add(item);
        return result;
    }
}
