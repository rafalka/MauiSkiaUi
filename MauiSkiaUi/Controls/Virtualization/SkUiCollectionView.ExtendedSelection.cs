namespace MauiSkiaUi;

// Extended selection (desktop lists): with IsExtendedSelectionEnabled and SelectionMode Multiple, a click selects only its
// item, Ctrl / Command+click toggles one, Shift+click selects the range from the anchor; the keyboard moves the selection
// with the current item, Shift extends it, Ctrl / Command moves without selecting. Each gesture is one change of
// SelectedItems: one cancelable SelectionChanging, one SelectionChanged, one command run.
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

    /// <summary>Bindable property for <see cref="IsExtendedSelectionEnabled"/>.</summary>
    public static readonly BindableProperty IsExtendedSelectionEnabledProperty = BindableProperty.Create(nameof(IsExtendedSelectionEnabled),
        typeof(bool), typeof(SkUiCollectionView), false,
        propertyChanged: (view, _, value) => ((SkUiCollectionView)view)._extendedSelection = (bool)value);

    /// <summary>
    /// Desktop selection with <see cref="SelectionMode"/> <see cref="SkUiSelectionMode.Multiple"/> (default <c>false</c>):
    /// a click selects only its item, Ctrl+click (Command+click on Apple) adds or removes one, Shift+click selects the
    /// items from the last clicked one (Ctrl+Shift+click adds them). With the keyboard, the arrow, Home / End and page keys
    /// select the item they move to, with Shift the range from the anchor, with Ctrl / Command they move without
    /// selecting; Space adds or removes the current item, Ctrl / Command+A selects all. Touch keeps the multiple selection's
    /// toggling tap. Ranges run in source order across groups; items of collapsed groups are left out.
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
        if (selection is not null)
            ChangeSelectionByGesture(selection);
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
        ChangeSelectionByGesture(all);
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
    /// The current item moved by a key in an extended selection: without modifiers it is selected alone (and anchors), with
    /// Shift the range from the anchor (from where the move started when there is none) is, with Ctrl / Command nothing changes.
    /// </summary>
    private void SelectAfterMove(SkUiKeyModifiers modifiers, int startedAt)
    {
        if (HasToggleModifier(modifiers) || _currentKind != CurrentKind.Item || ResolveCurrent() is not (>= 0 and var current))
            return;
        var (group, index) = Model.Locate(current);
        var shift = (modifiers & SkUiKeyModifiers.Shift) != 0;
        if (shift && ResolveAnchor() < 0 && startedAt >= 0)
        {
            var (anchorGroup, anchorIndex) = Model.Locate(startedAt);
            SetAnchor(anchorGroup, anchorIndex);
        }
        var item = Model.ItemAt(group, index);
        _tapSelecting = item; // the selection's focus, for KeepSelectionVisible
        _tapSelectingIndex = current;
        try
        {
            SelectByGesture(group, index, shift ? SelectionGesture.Range : SelectionGesture.Only);
        }
        finally
        {
            _tapSelecting = null;
        }
    }
}
