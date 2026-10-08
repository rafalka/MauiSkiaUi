using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;

namespace MauiSkiaUi;

public partial class SkUiCollectionView
{
    /// <summary>What a row of the items layout shows.</summary>
    private enum RowKind : byte
    {
        /// <summary>One item (<see cref="Span"/> 1) or a grid row of up to <see cref="Span"/> items.</summary>
        Items,
        /// <summary>A group's header (<see cref="GroupHeaderTemplate"/>).</summary>
        GroupHeader,
        /// <summary>A group's footer (<see cref="GroupFooterTemplate"/>).</summary>
        GroupFooter
    }

    /// <summary>
    /// A row of the items layout: <paramref name="Count"/> items from <paramref name="Start"/> of group
    /// <paramref name="Group"/> (-1: the flat list), or a group's header or footer.
    /// </summary>
    private readonly record struct Row(RowKind Kind, int Group, int Start, int Count);

    /// <summary>A group of a grouped list: its items, whether they show, and where its rows and items start.</summary>
    private sealed class Group(object? value)
    {
        public readonly object? Value = value;
        public IList Items = Array.Empty<object?>();
        public bool Snapshot;
        /// <summary>The item count the rows were built for (a live list has changed already when its change is reported).</summary>
        public int Count;
        public bool Expanded = true;
        public int FirstRow;
        public int RowCount;
        public int FirstItem;
        public ItemsModel.SourceObserver? Observer;
    }

    /// <summary>
    /// The rows of the items layout built from <see cref="ItemsSource"/>: for a flat single-column list one row per item (no
    /// table: row <c>i</c> is item <c>i</c>); otherwise a table of group headers, item rows of up to <see cref="Span"/> items and
    /// group footers. Changes of the source (and of each group) become the smallest changes of the rows, reported to the
    /// layout, so unaffected rows keep their views and sizes.
    /// </summary>
    private sealed class ItemsModel
    {
        private readonly SkUiCollectionView _owner;
        private readonly SourceObserver _sourceObserver;
        private IList? _source;     // the items, or the groups when grouped
        private bool _snapshot;
        private int _count;         // flat: the item count the rows were built for
        private readonly List<Group> _groups = [];
        private readonly List<Row> _rows = [];
        private IEnumerable? _value;

        public ItemsModel(SkUiCollectionView owner)
        {
            _owner = owner;
            _sourceObserver = new SourceObserver(this, null);
        }

        public bool Grouped { get; private set; }

        public int Span { get; private set; } = 1;

        public bool HasGroupHeaders { get; private set; }

        public bool HasGroupFooters { get; private set; }

        /// <summary>A flat single-column list: row <c>i</c> is item <c>i</c>, no table.</summary>
        private bool Trivial => !Grouped && Span == 1;

        public int RowCount => Trivial ? _count : _rows.Count;

        public int ItemCount => Grouped ? _groups.Count == 0 ? 0 : _groups[^1].FirstItem + _groups[^1].Count : _count;

        public IReadOnlyList<Group> Groups => _groups;

        public Row RowAt(int row) => Trivial ? new Row(RowKind.Items, -1, row, 1) : _rows[row];

        public object? ItemAt(int group, int index) => group < 0 ? _source![index] : _groups[group].Items[index];

        public int GlobalIndex(int group, int index) => group < 0 ? index : _groups[group].FirstItem + index;

        /// <summary>The group and index of the item at <paramref name="globalIndex"/> (counted across the groups).</summary>
        public (int Group, int Index) Locate(int globalIndex)
        {
            if (!Grouped)
                return (-1, globalIndex);
            int low = 0, high = _groups.Count - 1;
            while (low < high)
            {
                var middle = (low + high + 1) / 2;
                if (_groups[middle].FirstItem <= globalIndex)
                    low = middle;
                else
                    high = middle - 1;
            }
            return (low, globalIndex - _groups[low].FirstItem);
        }

        /// <summary>The row showing item <paramref name="index"/> of <paramref name="group"/>, or -1 (its group is collapsed).</summary>
        public int RowOfItem(int group, int index)
        {
            if (group < 0)
                return index / Span;
            var g = _groups[group];
            return g.Expanded ? ItemRowBase(group) + index / Span : -1;
        }

        /// <summary>The row of a group's header, or -1 (no header rows).</summary>
        public int HeaderRowOf(int group) => HasGroupHeaders ? _groups[group].FirstRow : -1;

        /// <summary>The global index of <paramref name="item"/> (found with <see cref="object.Equals(object?)"/>), or -1.</summary>
        public int IndexOfItem(object? item)
        {
            if (_source is null)
                return -1;
            if (!Grouped)
                return _source.IndexOf(item);
            foreach (var group in _groups)
                if (group.Items.IndexOf(item) is >= 0 and var index)
                    return group.FirstItem + index;
            return -1;
        }

        /// <summary>The index of a group (by reference first, then <see cref="object.Equals(object?)"/>), or -1.</summary>
        public int IndexOfGroup(object? value)
        {
            for (var index = 0; index < _groups.Count; index++)
                if (ReferenceEquals(_groups[index].Value, value))
                    return index;
            for (var index = 0; index < _groups.Count; index++)
                if (Equals(_groups[index].Value, value))
                    return index;
            return -1;
        }

        private int ItemRowBase(int group) => group < 0 ? 0 : _groups[group].FirstRow + (HasGroupHeaders ? 1 : 0);

        private static int RowsFor(int items, int span) => (items + span - 1) / span;

        #region Configuration

        public void SetSource(IEnumerable? value)
        {
            _value = value;
            _sourceObserver.Observe(value);
            Rebuild();
        }

        /// <summary>Sets how rows are built; rebuilds them when that changed (or <paramref name="force"/>: a group template changed).</summary>
        public void Configure(bool grouped, int span, bool groupHeaders, bool groupFooters, bool force = false)
        {
            if (!force && grouped == Grouped && span == Span && groupHeaders == HasGroupHeaders && groupFooters == HasGroupFooters)
                return;
            Grouped = grouped;
            Span = span;
            HasGroupHeaders = groupHeaders;
            HasGroupFooters = groupFooters;
            Rebuild();
        }

        /// <summary>Reads the source again and starts over: every row is new.</summary>
        public void Rebuild()
        {
            foreach (var group in _groups)
                group.Observer?.Observe(null);
            _groups.Clear();
            _rows.Clear();
            (_source, _snapshot) = Read(_value);
            _count = _source?.Count ?? 0;
            if (Grouped && _source is { } groups)
            {
                int row = 0, item = 0;
                foreach (var value in groups)
                {
                    var group = CreateGroup(value);
                    _groups.Add(group);
                    var rows = BuildRows(_groups.Count - 1);
                    (group.FirstRow, group.RowCount, group.FirstItem) = (row, rows.Count, item);
                    _rows.AddRange(rows);
                    row += rows.Count;
                    item += group.Count;
                }
            }
            else if (!Trivial)
            {
                _rows.AddRange(BuildRows(-1));
            }
            _owner._items.Reset(RowCount);
            _owner.OnItemsChanged(removed: null, reset: true);
        }

        private static (IList? List, bool Snapshot) Read(IEnumerable? value) => value switch
        {
            null => (null, false),
            IList list => (list, false),
            _ => (value.Cast<object?>().ToList(), true)
        };

        private Group CreateGroup(object? value)
        {
            var group = new Group(value);
            // A group is the list of its items (MAUI's grouped sources: a List<T> subclass with the group's own properties).
            (group.Items, group.Snapshot) = Read(value is string ? null : value as IEnumerable) is ({ } items, var snapshot)
                ? (items, snapshot) : (Array.Empty<object?>(), false);
            group.Count = group.Items.Count;
            group.Expanded = _owner.InitialExpanded(value);
            if (value is INotifyCollectionChanged or INotifyPropertyChanged)
            {
                group.Observer = new SourceObserver(this, group);
                group.Observer.Observe(value);
            }
            return group;
        }

        /// <summary>The rows of a group (or of the flat list for <paramref name="group"/> -1).</summary>
        private List<Row> BuildRows(int group)
        {
            var rows = new List<Row>();
            var count = group < 0 ? _count : _groups[group].Count;
            if (group >= 0 && HasGroupHeaders)
                rows.Add(new Row(RowKind.GroupHeader, group, 0, 0));
            if (group < 0 || _groups[group].Expanded)
            {
                for (var start = 0; start < count; start += Span)
                    rows.Add(new Row(RowKind.Items, group, start, Math.Min(Span, count - start)));
                if (group >= 0 && HasGroupFooters)
                    rows.Add(new Row(RowKind.GroupFooter, group, 0, 0));
            }
            return rows;
        }

        /// <summary>Builds a group's rows again in the table (the layout is told separately).</summary>
        private void ReplaceSegment(int group)
        {
            if (Trivial)
                return;
            var rows = BuildRows(group);
            if (group < 0)
            {
                _rows.Clear();
                _rows.AddRange(rows);
                return;
            }
            var g = _groups[group];
            _rows.RemoveRange(g.FirstRow, g.RowCount);
            _rows.InsertRange(g.FirstRow, rows);
            g.RowCount = rows.Count;
            Renumber(group + 1);
        }

        /// <summary>Where the rows and items of groups from <paramref name="from"/> start; their rows' group indices.</summary>
        private void Renumber(int from)
        {
            var row = from == 0 ? 0 : _groups[from - 1].FirstRow + _groups[from - 1].RowCount;
            var item = from == 0 ? 0 : _groups[from - 1].FirstItem + _groups[from - 1].Count;
            for (var index = from; index < _groups.Count; index++)
            {
                var group = _groups[index];
                // Groups inserted or removed before shift the indices the rows keep.
                for (var r = row; r < row + group.RowCount; r++)
                    if (_rows[r].Group != index)
                        _rows[r] = _rows[r] with { Group = index };
                group.FirstRow = row;
                group.FirstItem = item;
                row += group.RowCount;
                item += group.Count;
            }
        }

        #endregion

        #region Changes

        private void OnSourceChanged(Group? group, NotifyCollectionChangedEventArgs args)
        {
            if (group is null)
            {
                if (_snapshot)
                    Rebuild();
                else if (Grouped)
                    OnGroupsChanged(args);
                else
                    OnItemsChanged(-1, _source!, args);
                return;
            }
            var index = _groups.IndexOf(group);
            if (index < 0)
                return;
            if (group.Snapshot)
            {
                var old = group.Count;
                var removed = group.Items;
                group.Items = (group.Value as IEnumerable)?.Cast<object?>().ToList() ?? (IList)Array.Empty<object?>();
                group.Count = old; // ItemsReplaced takes the new count from Items
                ItemsReplaced(index, old, removed);
                return;
            }
            OnItemsChanged(index, group.Items, args);
        }

        /// <summary>A change of the flat list (<paramref name="group"/> -1) or of a group's items.</summary>
        private void OnItemsChanged(int group, IList list, NotifyCollectionChangedEventArgs args)
        {
            var count = group < 0 ? _count : _groups[group].Count;
            switch (args.Action)
            {
                case NotifyCollectionChangedAction.Add when args.NewItems is { } added && args.NewStartingIndex >= 0
                    && args.NewStartingIndex <= count && count + added.Count == list.Count:
                    ApplyItems(group, args.NewStartingIndex, count, list.Count, () => Insert(ItemRowBase(group) + args.NewStartingIndex, added.Count));
                    _owner.OnItemsChanged(null, reset: false);
                    return;
                case NotifyCollectionChangedAction.Remove when args.OldItems is { } removed && args.OldStartingIndex >= 0
                    && args.OldStartingIndex + removed.Count <= count && count - removed.Count == list.Count:
                    ApplyItems(group, args.OldStartingIndex, count, list.Count, () => Remove(ItemRowBase(group) + args.OldStartingIndex, removed.Count));
                    _owner.OnItemsChanged(removed, reset: false);
                    return;
                case NotifyCollectionChangedAction.Replace when args.NewItems is { } replaced && args.NewStartingIndex >= 0
                    && args.NewStartingIndex + replaced.Count <= count && count == list.Count:
                {
                    var first = args.NewStartingIndex;
                    if (Span == 1)
                        ApplyItems(group, first, count, count, () => Replace(ItemRowBase(group) + first, replaced.Count));
                    else
                        ApplyItems(group, first, count, count, () =>
                        {
                            var firstRow = first / Span;
                            Replace(ItemRowBase(group) + firstRow, (first + replaced.Count - 1) / Span - firstRow + 1);
                        });
                    _owner.OnItemsChanged(args.OldItems, reset: false);
                    return;
                }
                case NotifyCollectionChangedAction.Move when args.OldItems is { } moved && args.OldStartingIndex >= 0 && args.NewStartingIndex >= 0
                    && args.OldStartingIndex + moved.Count <= count && args.NewStartingIndex + moved.Count <= count && count == list.Count:
                    if (args.OldStartingIndex != args.NewStartingIndex)
                        ApplyItems(group, Math.Min(args.OldStartingIndex, args.NewStartingIndex), count, count,
                            () => Move(ItemRowBase(group) + args.OldStartingIndex, moved.Count, ItemRowBase(group) + args.NewStartingIndex));
                    _owner.OnItemsChanged(null, reset: false);
                    return;
            }
            // A reset, or a change out of step with the list (raised without its indices).
            if (group < 0)
                Rebuild();
            else
                ItemsReplaced(group, count, null);
        }

        /// <summary>
        /// Applies a change of items from <paramref name="first"/> (<paramref name="oldCount"/> → <paramref name="newCount"/>):
        /// one item per row runs <paramref name="singleRows"/> (the exact change); grid rows from the first changed one are
        /// rebound, and rows added or removed at the end. Collapsed groups only count their items.
        /// </summary>
        private void ApplyItems(int group, int first, int oldCount, int newCount, Action singleRows)
        {
            var expanded = group < 0 || _groups[group].Expanded;
            if (group < 0)
                _count = newCount;
            else
                _groups[group].Count = newCount;
            if (Trivial)
            {
                singleRows();
                return;
            }
            var rowsBefore = expanded ? RowsFor(oldCount, Span) : 0;
            ReplaceSegment(group);
            if (group >= 0)
                Renumber(group);
            if (!expanded)
                return;
            if (Span == 1)
            {
                singleRows();
                return;
            }
            var rowsAfter = RowsFor(newCount, Span);
            var firstRow = first / Span;
            var b = ItemRowBase(group);
            var common = Math.Min(rowsBefore, rowsAfter);
            if (common > firstRow)
                Replace(b + firstRow, common - firstRow);
            if (rowsAfter > rowsBefore)
                Insert(b + rowsBefore, rowsAfter - rowsBefore);
            else if (rowsBefore > rowsAfter)
                Remove(b + rowsAfter, rowsBefore - rowsAfter);
        }

        /// <summary>A group's items were replaced (a reset, or a snapshot read again): its item rows are new.</summary>
        private void ItemsReplaced(int group, int oldCount, IList? removed)
        {
            var g = _groups[group];
            var expanded = g.Expanded;
            var rowsBefore = expanded ? RowsFor(oldCount, Span) : 0;
            g.Count = g.Items.Count;
            ReplaceSegment(group);
            Renumber(group);
            if (expanded)
            {
                var b = ItemRowBase(group);
                if (rowsBefore > 0)
                    Remove(b, rowsBefore);
                var rowsAfter = RowsFor(g.Count, Span);
                if (rowsAfter > 0)
                    Insert(b, rowsAfter);
            }
            _owner.OnItemsChanged(removed, reset: removed is null);
        }

        /// <summary>A change of the groups of a grouped list.</summary>
        private void OnGroupsChanged(NotifyCollectionChangedEventArgs args)
        {
            var source = _source!;
            var count = _groups.Count;
            switch (args.Action)
            {
                case NotifyCollectionChangedAction.Add when args.NewItems is { } added && args.NewStartingIndex >= 0
                    && args.NewStartingIndex <= count && count + added.Count == source.Count:
                    InsertGroups(args.NewStartingIndex, added);
                    _owner.OnItemsChanged(null, reset: false);
                    return;
                case NotifyCollectionChangedAction.Remove when args.OldItems is { } removed && args.OldStartingIndex >= 0
                    && args.OldStartingIndex + removed.Count <= count && count - removed.Count == source.Count:
                    _owner.OnItemsChanged(RemoveGroups(args.OldStartingIndex, removed.Count), reset: false);
                    return;
                case NotifyCollectionChangedAction.Replace when args.NewItems is { } replaced && args.NewStartingIndex >= 0
                    && args.NewStartingIndex + replaced.Count <= count && count == source.Count:
                {
                    var removedItems = RemoveGroups(args.NewStartingIndex, replaced.Count);
                    InsertGroups(args.NewStartingIndex, replaced);
                    _owner.OnItemsChanged(removedItems, reset: false);
                    return;
                }
                case NotifyCollectionChangedAction.Move when args.OldItems is { } moved && args.OldStartingIndex >= 0 && args.NewStartingIndex >= 0
                    && args.OldStartingIndex + moved.Count <= count && args.NewStartingIndex + moved.Count <= count && count == source.Count:
                {
                    // Moved as removed and inserted again (their expanded state is kept).
                    var groups = _groups.GetRange(args.OldStartingIndex, moved.Count);
                    RemoveGroups(args.OldStartingIndex, moved.Count, detach: false);
                    InsertGroups(args.NewStartingIndex, groups);
                    _owner.OnItemsChanged(null, reset: false);
                    return;
                }
            }
            Rebuild();
        }

        private void InsertGroups(int index, IList values)
        {
            var groups = new List<Group>(values.Count);
            foreach (var value in values)
                groups.Add(CreateGroup(value));
            InsertGroups(index, groups);
        }

        private void InsertGroups(int index, List<Group> groups)
        {
            _groups.InsertRange(index, groups);
            var firstRow = index == 0 ? 0 : _groups[index - 1].FirstRow + _groups[index - 1].RowCount;
            var rows = new List<Row>();
            for (var offset = 0; offset < groups.Count; offset++)
            {
                var built = BuildRows(index + offset);
                groups[offset].RowCount = built.Count;
                rows.AddRange(built);
            }
            _rows.InsertRange(firstRow, rows);
            Renumber(index);
            if (rows.Count > 0)
                Insert(firstRow, rows.Count);
        }

        private List<object?> RemoveGroups(int index, int count, bool detach = true)
        {
            var removedItems = new List<object?>();
            var firstRow = _groups[index].FirstRow;
            var rowCount = 0;
            for (var offset = 0; offset < count; offset++)
            {
                var group = _groups[index + offset];
                rowCount += group.RowCount;
                removedItems.AddRange(group.Items.Cast<object?>());
                if (detach)
                    group.Observer?.Observe(null);
            }
            _groups.RemoveRange(index, count);
            _rows.RemoveRange(firstRow, rowCount);
            if (index < _groups.Count)
                Renumber(index);
            if (rowCount > 0)
                Remove(firstRow, rowCount);
            return removedItems;
        }

        /// <summary>Shows or hides a group's items (and footer); <c>false</c> when it already was so.</summary>
        public bool SetExpanded(int group, bool expanded)
        {
            var g = _groups[group];
            if (g.Expanded == expanded)
                return false;
            var before = g.RowCount;
            g.Expanded = expanded;
            ReplaceSegment(group);
            var b = ItemRowBase(group);
            if (expanded && g.RowCount > before)
                Insert(b, g.RowCount - before);
            else if (!expanded && before > g.RowCount)
                Remove(b, before - g.RowCount);
            _owner.OnItemsChanged(null, reset: false);
            return true;
        }

        private void Insert(int row, int count) => _owner._items.InsertRows(row, count);

        private void Remove(int row, int count) => _owner._items.RemoveRows(row, count);

        private void Replace(int row, int count)
        {
            if (count > 0)
                _owner._items.ReplaceRows(row, count);
        }

        private void Move(int row, int count, int newRow) => _owner._items.MoveRows(row, count, newRow);

        /// <summary>A group raised <see cref="INotifyPropertyChanged.PropertyChanged"/>: its expanded state may follow.</summary>
        private void OnGroupPropertyChanged(Group group, string? property)
        {
            if (property is not (null or "" or nameof(ISkUiExpandableGroup.IsExpanded)) || group.Value is not ISkUiExpandableGroup expandable)
                return;
            var index = _groups.IndexOf(group);
            if (index >= 0 && expandable.IsExpanded != group.Expanded)
                _owner.ChangeGroupExpanded(index, expandable.IsExpanded, fromGroup: true);
        }

        #endregion

        /// <summary>
        /// Listens to the items (or the groups), or to one group, without the source keeping the list alive.
        /// </summary>
        public sealed class SourceObserver(ItemsModel owner, Group? group)
        {
            private readonly WeakReference<ItemsModel> _owner = new(owner);
            private INotifyCollectionChanged? _collection;
            private INotifyPropertyChanged? _properties;

            public void Observe(object? source)
            {
                var collection = source as INotifyCollectionChanged;
                if (!ReferenceEquals(collection, _collection))
                {
                    if (_collection is not null)
                        _collection.CollectionChanged -= OnCollectionChanged;
                    _collection = collection;
                    if (collection is not null)
                        collection.CollectionChanged += OnCollectionChanged;
                }
                // Groups only: their expanded state (ISkUiExpandableGroup).
                var properties = group is not null && source is ISkUiExpandableGroup ? source as INotifyPropertyChanged : null;
                if (!ReferenceEquals(properties, _properties))
                {
                    if (_properties is not null)
                        _properties.PropertyChanged -= OnPropertyChanged;
                    _properties = properties;
                    if (properties is not null)
                        properties.PropertyChanged += OnPropertyChanged;
                }
            }

            private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
            {
                if (_owner.TryGetTarget(out var model))
                    model.OnSourceChanged(group, args);
                else
                    Observe(null);
            }

            private void OnPropertyChanged(object? sender, PropertyChangedEventArgs args)
            {
                if (_owner.TryGetTarget(out var model))
                    model.OnGroupPropertyChanged(group!, args.PropertyName);
                else
                    Observe(null);
            }
        }
    }
}
