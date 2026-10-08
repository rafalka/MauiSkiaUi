using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using MauiSkiaUi;

namespace MauiSkiaUiSamples.Samples.Controls.Contacts;

/// <summary>
/// What a list control shows of a source list: the items that pass <see cref="Filter"/>, in <see cref="Sort"/> order,
/// as one list (<see cref="Items"/>) or in groups (<see cref="Groups"/>, when <see cref="GroupKeys"/> is set; an item may be
/// in several groups). It follows the source and its items as they change, with the smallest changes of its own
/// collections, so a list bound to it (an <c>SkUiCollectionView</c>) changes only the rows they touch.
/// </summary>
/// <remarks>
/// <para>
/// <b>Changes.</b> Items added to or removed from the source are inserted at their sorted place (in each of their groups)
/// or removed; a group is created for its first item and removed with its last. When an item raises
/// <see cref="INotifyPropertyChanged.PropertyChanged"/> for a property in <see cref="WatchedProperties"/> (any property
/// when empty), it moves to its new place: a move within a list keeps it in the list (a list control keeps it selected),
/// and new groups get it before old ones lose it. Changing a setting (filter, sort, grouping) rebuilds the collections
/// (one reset each); groups keep their expanded state by key.
/// </para>
/// <para>
/// One instance per screen; it listens to the source and its items (ordinary events: it lives as long as they do).
/// Call <see cref="Dispose"/> to stop.
/// </para>
/// </remarks>
public sealed class LiveGroupedList<TItem, TKey> : IDisposable
    where TItem : class
    where TKey : notnull
{
    private readonly ObservableCollection<TItem> _source;
    private readonly Dictionary<TItem, TKey[]> _shown = new(ReferenceEqualityComparer.Instance); // shown items and their groups
    private readonly HashSet<TKey> _collapsed = [];
    private Func<TItem, bool>? _filter;
    private IComparer<TItem>? _sort;
    private Func<TItem, IEnumerable<TKey>>? _groupKeys;
    private IComparer<TKey>? _groupOrder;

    public LiveGroupedList(ObservableCollection<TItem> source)
    {
        _source = source;
        _source.CollectionChanged += OnSourceChanged;
        foreach (var item in _source)
            Watch(item);
        Refresh();
    }

    /// <summary>The shown items when not grouped (empty while grouped).</summary>
    public ShownCollection<TItem> Items { get; } = [];

    /// <summary>The groups when <see cref="GroupKeys"/> is set (empty otherwise), each with its shown items.</summary>
    public ShownCollection<ItemGroup<TKey, TItem>> Groups { get; } = [];

    /// <summary>Whether items are shown in groups.</summary>
    public bool IsGrouped => _groupKeys is not null;

    /// <summary>What a list control shows: <see cref="Groups"/> when grouped, else <see cref="Items"/>.</summary>
    public IEnumerable Shown => IsGrouped ? Groups : Items;

    /// <summary>Which items are shown; <c>null</c>: all.</summary>
    public Func<TItem, bool>? Filter { get => _filter; set { _filter = value; Refresh(); } }

    /// <summary>The order of the items (in each group); <c>null</c>: the source's order.</summary>
    public IComparer<TItem>? Sort { get => _sort; set { _sort = value; Refresh(); } }

    /// <summary>The groups of an item (none: the item is not shown); <c>null</c>: not grouped.</summary>
    public Func<TItem, IEnumerable<TKey>>? GroupKeys { get => _groupKeys; set { _groupKeys = value; Refresh(); } }

    /// <summary>The order of the groups; <c>null</c>: <see cref="Comparer{T}.Default"/>.</summary>
    public IComparer<TKey>? GroupOrder { get => _groupOrder; set { _groupOrder = value; Refresh(); } }

    /// <summary>A group's title (<see cref="ItemGroup{TKey, TItem}.Title"/>); <c>null</c>: the key's text.</summary>
    public Func<TKey, string>? GroupTitle { get; set; }

    /// <summary>Item properties that can change where an item is shown (empty: any property).</summary>
    public HashSet<string> WatchedProperties { get; } = [];

    /// <summary>Sets several settings with one rebuild.</summary>
    public void Configure(Func<TItem, bool>? filter, IComparer<TItem>? sort, Func<TItem, IEnumerable<TKey>>? groupKeys, IComparer<TKey>? groupOrder = null)
    {
        (_filter, _sort, _groupKeys, _groupOrder) = (filter, sort, groupKeys, groupOrder);
        Refresh();
    }

    /// <summary>Builds the shown collections again (after a setting changed, or something the filter reads).</summary>
    public void Refresh()
    {
        foreach (var group in Groups.Where(group => !group.IsExpanded))
            _collapsed.Add(group.Key);
        foreach (var group in Groups.Where(group => group.IsExpanded))
            _collapsed.Remove(group.Key);
        _shown.Clear();
        var shown = _source.Where(Passes).ToList();
        if (_sort is not null)
            shown.Sort(_sort);
        var items = new List<TItem>();
        var groups = new Dictionary<TKey, List<TItem>>();
        foreach (var item in shown)
        {
            var keys = KeysOf(item);
            if (keys is null)
            {
                items.Add(item);
                _shown[item] = [];
                continue;
            }
            if (keys.Length == 0)
                continue;
            _shown[item] = keys;
            foreach (var key in keys)
            {
                if (!groups.TryGetValue(key, out var members))
                    groups[key] = members = [];
                members.Add(item);
            }
        }
        Items.ReplaceAll(items);
        Groups.ReplaceAll(groups.OrderBy(pair => pair.Key, GroupComparer).Select(pair => NewGroup(pair.Key, pair.Value)).ToList());
    }

    /// <summary>The groups an item is shown in now.</summary>
    public IReadOnlyList<ItemGroup<TKey, TItem>> GroupsOf(TItem item) =>
        _shown.TryGetValue(item, out var keys) ? Groups.Where(group => keys.Contains(group.Key)).ToList() : [];

    public void Dispose()
    {
        _source.CollectionChanged -= OnSourceChanged;
        foreach (var item in _source)
            Unwatch(item);
    }

    private IComparer<TKey> GroupComparer => _groupOrder ?? Comparer<TKey>.Default;

    private bool Passes(TItem item) => _filter?.Invoke(item) ?? true;

    /// <summary>The distinct groups of an item, or <c>null</c> when not grouped.</summary>
    private TKey[]? KeysOf(TItem item) => _groupKeys?.Invoke(item).Distinct().ToArray();

    private ItemGroup<TKey, TItem> NewGroup(TKey key, IEnumerable<TItem> items) =>
        new(key, GroupTitle?.Invoke(key) ?? key.ToString() ?? "", items) { IsExpanded = !_collapsed.Contains(key) };

    #region Following the source

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        switch (args.Action)
        {
            case NotifyCollectionChangedAction.Add when args.NewItems is { } added:
                foreach (TItem item in added)
                {
                    Watch(item);
                    Show(item);
                }
                return;
            case NotifyCollectionChangedAction.Remove when args.OldItems is { } removed:
                foreach (TItem item in removed)
                {
                    Unwatch(item);
                    Hide(item);
                }
                return;
            case NotifyCollectionChangedAction.Replace when args.OldItems is { } oldItems && args.NewItems is { } newItems:
                foreach (TItem item in oldItems)
                {
                    Unwatch(item);
                    Hide(item);
                }
                foreach (TItem item in newItems)
                {
                    Watch(item);
                    Show(item);
                }
                return;
            case NotifyCollectionChangedAction.Move when _sort is not null:
                return; // sorted: the source's order does not matter
        }
        // A reset (or a move of an unsorted source): every item is listened to again and placed again.
        foreach (var item in _shown.Keys.ToList())
            Unwatch(item);
        foreach (var item in _source)
        {
            Unwatch(item);
            Watch(item);
        }
        Refresh();
    }

    private void Watch(TItem item)
    {
        if (item is INotifyPropertyChanged notifying)
            notifying.PropertyChanged += OnItemChanged;
    }

    private void Unwatch(TItem item)
    {
        if (item is INotifyPropertyChanged notifying)
            notifying.PropertyChanged -= OnItemChanged;
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (sender is not TItem item)
            return;
        if (WatchedProperties.Count > 0 && args.PropertyName is { Length: > 0 } property && !WatchedProperties.Contains(property))
            return;
        Update(item);
    }

    /// <summary>Shows a new item at its places.</summary>
    private void Show(TItem item)
    {
        if (!Passes(item))
            return;
        var keys = KeysOf(item);
        if (keys is null)
        {
            Items.Insert(PlaceIn(Items, item), item);
            _shown[item] = [];
            return;
        }
        if (keys.Length == 0)
            return;
        _shown[item] = keys;
        foreach (var key in keys)
            AddTo(key, item);
    }

    /// <summary>Removes an item from where it is shown.</summary>
    private void Hide(TItem item)
    {
        if (!_shown.Remove(item, out var keys))
            return;
        if (!IsGrouped)
        {
            Items.Remove(item);
            return;
        }
        foreach (var key in keys)
            RemoveFrom(key, item);
    }

    /// <summary>An item changed: moved to its new place (in the same list, or into new groups before old ones lose it).</summary>
    private void Update(TItem item)
    {
        var shownBefore = _shown.TryGetValue(item, out var before);
        if (!Passes(item))
        {
            if (shownBefore)
                Hide(item);
            return;
        }
        if (!shownBefore)
        {
            Show(item);
            return;
        }
        if (!IsGrouped)
        {
            Reposition(Items, item);
            return;
        }
        var after = KeysOf(item)!;
        _shown[item] = after;
        foreach (var key in after)
            if (before!.Contains(key))
                Reposition(FindGroup(key)!, item);
            else
                AddTo(key, item);
        foreach (var key in before!)
            if (!after.Contains(key))
                RemoveFrom(key, item);
        if (after.Length == 0)
            _shown.Remove(item);
    }

    private ItemGroup<TKey, TItem>? FindGroup(TKey key) => Groups.FirstOrDefault(group => EqualityComparer<TKey>.Default.Equals(group.Key, key));

    private void AddTo(TKey key, TItem item)
    {
        if (FindGroup(key) is { } group)
        {
            group.Insert(PlaceIn(group, item), item);
            return;
        }
        // The group's first item: the group comes in at its place.
        var created = NewGroup(key, [item]);
        var index = 0;
        while (index < Groups.Count && GroupComparer.Compare(Groups[index].Key, key) < 0)
            index++;
        Groups.Insert(index, created);
    }

    private void RemoveFrom(TKey key, TItem item)
    {
        if (FindGroup(key) is not { } group)
            return;
        group.Remove(item);
        if (group.Count == 0)
        {
            if (!group.IsExpanded)
                _collapsed.Add(key);
            Groups.Remove(group);
        }
    }

    /// <summary>Moves an item to its sorted place within a list: one move (a list control keeps it, and its selection).</summary>
    private void Reposition(ObservableCollection<TItem> list, TItem item)
    {
        var from = list.IndexOf(item);
        if (from < 0)
            return;
        var to = PlaceIn(list, item, exclude: from);
        if (to != from)
            list.Move(from, to);
    }

    /// <summary>
    /// Where an item goes in a list: by <see cref="Sort"/> (after equal items), else by the source's order. With
    /// <paramref name="exclude"/>, the list is read without the item at that index (a move's target index).
    /// </summary>
    private int PlaceIn(IList<TItem> list, TItem item, int exclude = -1)
    {
        var count = exclude < 0 ? list.Count : list.Count - 1;
        TItem At(int index) => exclude >= 0 && index >= exclude ? list[index + 1] : list[index];
        if (_sort is { } sort)
        {
            int low = 0, high = count;
            while (low < high)
            {
                var middle = (low + high) / 2;
                if (sort.Compare(At(middle), item) <= 0)
                    low = middle + 1;
                else
                    high = middle;
            }
            return low;
        }
        var sourceIndex = _source.IndexOf(item);
        var place = 0;
        while (place < count && _source.IndexOf(At(place)) < sourceIndex)
            place++;
        return place;
    }

    #endregion
}

/// <summary>
/// A group of a <see cref="LiveGroupedList{TItem, TKey}"/>: its items, its key and title, whether it is expanded (an
/// <c>SkUiCollectionView</c> follows <see cref="IsExpanded"/>), and a check state for a header checkbox.
/// </summary>
public sealed class ItemGroup<TKey, TItem>(TKey key, string title, IEnumerable<TItem> items) : ObservableCollection<TItem>(items), ISkUiExpandableGroup
{
    private bool _expanded = true;
    private SkUiCheckState _checkState;

    public TKey Key { get; } = key;

    public string Title { get; } = title;

    public override string ToString() => Title;

    public bool IsExpanded
    {
        get => _expanded;
        set
        {
            if (_expanded == value)
                return;
            _expanded = value;
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsExpanded)));
        }
    }

    /// <summary>How many of the group's items are checked (selected): none, all, or some (<see cref="SkUiCheckState.Indeterminate"/>).</summary>
    public SkUiCheckState CheckState
    {
        get => _checkState;
        set
        {
            if (_checkState == value)
                return;
            _checkState = value;
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(CheckState)));
        }
    }
}

/// <summary>An <see cref="ObservableCollection{T}"/> whose contents can be replaced with one reset (instead of an event per item).</summary>
public sealed class ShownCollection<T> : ObservableCollection<T>
{
    /// <summary>Replaces the contents, raising one <see cref="NotifyCollectionChangedAction.Reset"/>.</summary>
    public void ReplaceAll(IEnumerable<T> items)
    {
        CheckReentrancy();
        Items.Clear();
        foreach (var item in items)
            Items.Add(item);
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
