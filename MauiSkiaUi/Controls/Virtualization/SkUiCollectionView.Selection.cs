using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;

namespace MauiSkiaUi;

public partial class SkUiCollectionView
{
    private SkUiSelectionMode _selectionMode;
    private object? _selectedItem;
    private Brush? _selectionBackground;
    // The selected items of a multiple selection, for lookups while binding (SelectedItems may be any list).
    private readonly HashSet<object> _selectedSet = [];
    private IReadOnlyList<object> _selectedSnapshot = [];
    private SelectedItemsObserver? _selectedItemsObserver;
    private bool _changingSelection;
    private static Func<string> _selectedStateText = () => "Selected";

    /// <summary>Bindable property for <see cref="SelectionMode"/>.</summary>
    public static readonly BindableProperty SelectionModeProperty = BindableProperty.Create(nameof(SelectionMode), typeof(SkUiSelectionMode),
        typeof(SkUiCollectionView), SkUiSelectionMode.None,
        validateValue: (_, value) => Enum.IsDefined((SkUiSelectionMode)value),
        propertyChanged: (bindable, _, value) => ((SkUiCollectionView)bindable).OnSelectionModeChanged((SkUiSelectionMode)value));

    /// <summary>Bindable property for <see cref="SelectedItem"/> (two-way by default: taps change it).</summary>
    public static readonly BindableProperty SelectedItemProperty = BindableProperty.Create(nameof(SelectedItem), typeof(object), typeof(SkUiCollectionView), null,
        BindingMode.TwoWay,
        propertyChanged: (bindable, oldValue, newValue) => ((SkUiCollectionView)bindable).OnSelectedItemChanged(oldValue, newValue));

    /// <summary>Bindable property for <see cref="SelectedItems"/>.</summary>
    public static readonly BindableProperty SelectedItemsProperty = BindableProperty.Create(nameof(SelectedItems), typeof(IList<object>), typeof(SkUiCollectionView), null,
        BindingMode.OneWay,
        defaultValueCreator: bindable =>
        {
            // Created when first read; followed from then on (no property change is raised for a default).
            var list = new ObservableCollection<object>();
            var owner = (SkUiCollectionView)bindable;
            (owner._selectedItemsObserver ??= new SelectedItemsObserver(owner)).Observe(list);
            return list;
        },
        propertyChanged: (bindable, _, value) => ((SkUiCollectionView)bindable).OnSelectedItemsChanged((IList<object>?)value));

    /// <summary>Bindable property for <see cref="SelectionChangedCommand"/>.</summary>
    public static readonly BindableProperty SelectionChangedCommandProperty = BindableProperty.Create(nameof(SelectionChangedCommand), typeof(ICommand), typeof(SkUiCollectionView));

    /// <summary>Bindable property for <see cref="SelectionChangedCommandParameter"/>.</summary>
    public static readonly BindableProperty SelectionChangedCommandParameterProperty = BindableProperty.Create(nameof(SelectionChangedCommandParameter), typeof(object), typeof(SkUiCollectionView));

    /// <summary>Bindable property for <see cref="SelectionBackground"/>.</summary>
    public static readonly BindableProperty SelectionBackgroundProperty = BindableProperty.Create(nameof(SelectionBackground), typeof(Brush), typeof(SkUiCollectionView), null,
        propertyChanged: (bindable, _, value) => ((SkUiCollectionView)bindable).OnSelectionBackgroundChanged((Brush?)value));

    /// <summary>Bindable property for <see cref="ItemTappedCommand"/>.</summary>
    public static readonly BindableProperty ItemTappedCommandProperty = BindableProperty.Create(nameof(ItemTappedCommand), typeof(ICommand), typeof(SkUiCollectionView));

    /// <summary>Bindable property for <see cref="ItemTappedCommandParameter"/>.</summary>
    public static readonly BindableProperty ItemTappedCommandParameterProperty = BindableProperty.Create(nameof(ItemTappedCommandParameter), typeof(object), typeof(SkUiCollectionView));

    /// <summary>Bindable property for <see cref="ShowsItemPressEffect"/>.</summary>
    public static readonly BindableProperty ShowsItemPressEffectProperty = BindableProperty.Create(nameof(ShowsItemPressEffect), typeof(bool), typeof(SkUiCollectionView), false,
        propertyChanged: (bindable, _, value) => ((SkUiCollectionView)bindable).OnShowsItemPressEffectChanged((bool)value));

    /// <summary>
    /// How taps select items: <see cref="SkUiSelectionMode.None"/> (default), <see cref="SkUiSelectionMode.Single"/>,
    /// <see cref="SkUiSelectionMode.SingleDeselect"/> (tapping the selected item clears the selection) or
    /// <see cref="SkUiSelectionMode.Multiple"/> (taps add and remove items in <see cref="SelectedItems"/>). Changing the mode
    /// clears the selection the new mode does not use (<see cref="SelectedItem"/> for <see cref="SkUiSelectionMode.None"/>
    /// and <see cref="SkUiSelectionMode.Multiple"/>, <see cref="SelectedItems"/> for the others).
    /// </summary>
    public SkUiSelectionMode SelectionMode { get => (SkUiSelectionMode)GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }

    /// <summary>
    /// The selected item of a single selection (an item of the list, compared with <see cref="object.Equals(object?)"/>), or
    /// <c>null</c>. Its view's root goes to the <c>Selected</c> visual state (<c>CommonStates</c>) and its container draws
    /// <see cref="SelectionBackground"/>. Shown only while <see cref="SelectionMode"/> is <see cref="SkUiSelectionMode.Single"/>
    /// or <see cref="SkUiSelectionMode.SingleDeselect"/>. Removing the item from the source clears it.
    /// </summary>
    public object? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }

    /// <summary>
    /// The selected items of a multiple selection (<see cref="SkUiSelectionMode.Multiple"/>): by default an
    /// <see cref="ObservableCollection{T}"/> of the list's own; set a list of yours to share it with a view model. Taps add and
    /// remove items in it; changes made to it (an <see cref="INotifyCollectionChanged"/> list, or after setting another list)
    /// show at once and raise <see cref="SelectionChanged"/>. Items removed from the source are removed from it.
    /// </summary>
    public IList<object> SelectedItems { get => (IList<object>)GetValue(SelectedItemsProperty); set => SetValue(SelectedItemsProperty, value); }

    /// <summary>Runs with <see cref="SelectionChangedCommandParameter"/> after the selection changes (when it can execute), before <see cref="SelectionChanged"/>.</summary>
    public ICommand? SelectionChangedCommand
    {
        get => (ICommand?)GetValue(SelectionChangedCommandProperty);
        set => SetValue(SelectionChangedCommandProperty, value);
    }

    /// <summary>The parameter of <see cref="SelectionChangedCommand"/>.</summary>
    public object? SelectionChangedCommandParameter
    {
        get => GetValue(SelectionChangedCommandParameterProperty);
        set => SetValue(SelectionChangedCommandParameterProperty, value);
    }

    /// <summary>
    /// Drawn behind each selected item's view (in XAML a color, e.g. <c>"#1F0A84FF"</c>, or a gradient); <c>null</c> (default):
    /// the accent color (<see cref="SkUiColors.Accent"/>) at 12 % opacity. A transparent brush draws nothing (style the
    /// item with the <c>Selected</c> visual state instead).
    /// </summary>
    public Brush? SelectionBackground { get => (Brush?)GetValue(SelectionBackgroundProperty); set => SetValue(SelectionBackgroundProperty, value); }

    /// <summary>Runs after <see cref="ItemTapped"/> (when it can execute), with <see cref="ItemTappedCommandParameter"/> when set, else the tapped item.</summary>
    public ICommand? ItemTappedCommand { get => (ICommand?)GetValue(ItemTappedCommandProperty); set => SetValue(ItemTappedCommandProperty, value); }

    /// <summary>The parameter of <see cref="ItemTappedCommand"/>; when not set, the tapped item.</summary>
    public object? ItemTappedCommandParameter { get => GetValue(ItemTappedCommandParameterProperty); set => SetValue(ItemTappedCommandParameterProperty, value); }

    /// <summary>Whether item containers show the press effect (<see cref="SkUiView.ShowsPressEffect"/>) while an item is pressed (default <c>false</c>).</summary>
    public bool ShowsItemPressEffect { get => (bool)GetValue(ShowsItemPressEffectProperty); set => SetValue(ShowsItemPressEffectProperty, value); }

    /// <summary>
    /// A tap is about to change the selection: set <see cref="SkUiSelectionChangingEventArgs.Cancel"/> to keep it. Raised for
    /// taps only (changing <see cref="SelectedItem"/> or <see cref="SelectedItems"/> is the app's own decision).
    /// </summary>
    public event EventHandler<SkUiSelectionChangingEventArgs>? SelectionChanging;

    /// <summary>
    /// The selection changed (by a tap, by the app, by <see cref="SelectAll"/> / <see cref="ClearSelection"/>, or because
    /// selected items were removed), after <see cref="SelectionChangedCommand"/>; once per change, also when it touches many
    /// items.
    /// </summary>
    public event EventHandler<SkUiSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>
    /// An item was tapped (on the item outside views that take taps themselves, such as buttons), whatever the
    /// <see cref="SelectionMode"/>; raised after the tap changed the selection.
    /// </summary>
    public event EventHandler<SkUiItemTappedEventArgs>? ItemTapped;

    /// <summary>
    /// The value screen readers read for a selected item ("Selected" by default). Set it once at startup to localize (it is
    /// called each time the semantics are read, so it may follow the current culture).
    /// </summary>
    public static Func<string> SelectedStateText
    {
        get => _selectedStateText;
        set => _selectedStateText = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Sets <see cref="SelectionMode"/> (same as the property setter).</summary>
    public SkUiCollectionView SetSelectionMode(SkUiSelectionMode value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        SelectionMode = value;
        return this;
    }

    /// <summary>Selects every item (<see cref="SkUiSelectionMode.Multiple"/> only; otherwise nothing happens), raising <see cref="SelectionChanged"/> once.</summary>
    public void SelectAll()
    {
        if (_selectionMode != SkUiSelectionMode.Multiple)
            return;
        var all = new List<object>();
        for (var index = 0; index < _model.ItemCount; index++)
        {
            var (group, item) = _model.Locate(index);
            if (_model.ItemAt(group, item) is { } value)
                all.Add(value);
        }
        ReplaceSelectedItems(all);
    }

    /// <summary>Clears the selection (<see cref="SelectedItem"/> and <see cref="SelectedItems"/>), raising <see cref="SelectionChanged"/> once.</summary>
    public void ClearSelection()
    {
        if (_selectionMode == SkUiSelectionMode.Multiple)
            ReplaceSelectedItems([]);
        else
            SelectedItem = null;
    }

    /// <summary>Whether item containers take taps.</summary>
    private bool WantsItemTaps => _selectionMode != SkUiSelectionMode.None || ItemTapped is not null || ItemTappedCommand is not null;

    private bool ShowsSelection(object? item) => _selectionMode switch
    {
        SkUiSelectionMode.None => false,
        SkUiSelectionMode.Multiple => item is not null && _selectedSet.Contains(item),
        _ => _selectedItem is not null && Equals(item, _selectedItem)
    };

    private void OnSelectionModeChanged(SkUiSelectionMode value)
    {
        _selectionMode = value;
        if (value is SkUiSelectionMode.None or SkUiSelectionMode.Multiple && _selectedItem is not null)
            SelectedItem = null;
        if (value != SkUiSelectionMode.Multiple && _selectedSet.Count > 0)
            ReplaceSelectedItems([]);
        RefreshSelection();
    }

    private void OnSelectedItemChanged(object? oldValue, object? newValue)
    {
        _selectedItem = newValue;
        RefreshSelection();
        RaiseSelectionChanged(new SkUiSelectionChangedEventArgs(oldValue, newValue));
    }

    private void RaiseSelectionChanged(SkUiSelectionChangedEventArgs args)
    {
        if (SelectionChangedCommand is { } command && command.CanExecute(SelectionChangedCommandParameter))
            command.Execute(SelectionChangedCommandParameter);
        SelectionChanged?.Invoke(this, args);
        OnSelectionChangedKeepVisible();
    }

    private void OnSelectedItemsChanged(IList<object>? value)
    {
        (_selectedItemsObserver ??= new SelectedItemsObserver(this)).Observe(value as INotifyCollectionChanged);
        SyncSelectedItems();
    }

    /// <summary>Reads <see cref="SelectedItems"/> again: shows it and raises <see cref="SelectionChanged"/> when it differs.</summary>
    private void SyncSelectedItems()
    {
        if (_changingSelection)
            return;
        var previous = _selectedSnapshot;
        var current = SelectedItems is { } items ? items.ToArray() : [];
        if (previous.Count == current.Length && previous.SequenceEqual(current))
            return;
        _selectedSnapshot = current;
        _selectedSet.Clear();
        foreach (var item in current)
            _selectedSet.Add(item);
        RefreshSelection();
        RaiseSelectionChanged(new SkUiSelectionChangedEventArgs(previous, current));
    }

    /// <summary>Sets the contents of <see cref="SelectedItems"/> in one change (one <see cref="SelectionChanged"/>).</summary>
    private void ReplaceSelectedItems(IReadOnlyList<object> items)
    {
        var list = SelectedItems;
        _changingSelection = true;
        try
        {
            list.Clear();
            foreach (var item in items)
                list.Add(item);
        }
        finally
        {
            _changingSelection = false;
        }
        SyncSelectedItems();
    }

    /// <summary>Shows the selection on the realized items (only items whose state changes re-record).</summary>
    private void RefreshSelection()
    {
        foreach (var host in RealizedItemHosts())
            host.IsSelected = ShowsSelection(((BindableObject)host).BindingContext);
    }

    /// <summary>The containers of the realized items (in grid rows too).</summary>
    private IEnumerable<ItemHost> RealizedItemHosts()
    {
        foreach (var view in _items.RealizedViews)
            if (view is ItemHost host)
                yield return host;
            else if (view is GridRowPart row)
                foreach (var cell in row.Cells)
                    yield return cell;
    }

    private void OnSelectionBackgroundChanged(Brush? value)
    {
        _selectionBackground = value;
        foreach (var host in RealizedItemHosts())
            if (host.IsSelected)
                host.InvalidatePaint();
    }

    private void OnShowsItemPressEffectChanged(bool value)
    {
        foreach (var host in RealizedItemHosts().Concat(_cellPool.Values.SelectMany(static cells => cells)))
            host.ShowsPressEffect = value;
        foreach (var view in _items.RecycledViews)
            if (view is ItemHost host)
                host.ShowsPressEffect = value;
            else if (view is GridRowPart row)
                foreach (var cell in row.Cells)
                    cell.ShowsPressEffect = value;
    }

    /// <summary>A tap on an item: the selection first (cancelable), then <see cref="ItemTapped"/> and its command.</summary>
    private void OnItemHostTapped(ItemHost host)
    {
        if (LocateHost(host) is not { } location)
            return;
        var (group, index) = location;
        var item = _model.ItemAt(group, index);
        _tapSelecting = item;
        try
        {
            SelectByTap(item);
        }
        finally
        {
            _tapSelecting = null;
        }
        var global = _model.GlobalIndex(group, index);
        ItemTapped?.Invoke(this, new SkUiItemTappedEventArgs(item, global, group < 0 ? null : _model.Groups[group].Value));
        if (ItemTappedCommand is { } command)
        {
            var parameter = IsSet(ItemTappedCommandParameterProperty) ? ItemTappedCommandParameter : item;
            if (command.CanExecute(parameter))
                command.Execute(parameter);
        }
    }

    /// <summary>The selection change of a tap on <paramref name="item"/> (cancelable by <see cref="SelectionChanging"/>).</summary>
    private void SelectByTap(object? item)
    {
        switch (_selectionMode)
        {
            case SkUiSelectionMode.Single or SkUiSelectionMode.SingleDeselect:
            {
                var selected = _selectionMode == SkUiSelectionMode.SingleDeselect && Equals(item, _selectedItem) ? null : item;
                if (!Equals(selected, _selectedItem))
                {
                    var args = new SkUiSelectionChangingEventArgs(_selectedItem, selected);
                    SelectionChanging?.Invoke(this, args);
                    if (!args.Cancel)
                        SetValue(SelectedItemProperty, selected);
                }
                break;
            }
            case SkUiSelectionMode.Multiple when item is not null:
            {
                var current = _selectedSet.Contains(item)
                    ? _selectedSnapshot.Where(selected => !Equals(selected, item)).ToArray()
                    : [.. _selectedSnapshot, item];
                var args = new SkUiSelectionChangingEventArgs(_selectedSnapshot, current);
                SelectionChanging?.Invoke(this, args);
                if (args.Cancel)
                    break;
                var list = SelectedItems;
                _changingSelection = true;
                try
                {
                    if (!list.Remove(item))
                        list.Add(item);
                }
                finally
                {
                    _changingSelection = false;
                }
                SyncSelectedItems();
                break;
            }
        }
    }

    /// <summary>The group and index (in its group, or in the flat list) of the item a realized container shows, or <c>null</c>.</summary>
    private (int Group, int Index)? LocateHost(ItemHost host)
    {
        int rowIndex, slot;
        if (((Element)host).Parent is GridRowPart rowPart)
        {
            rowIndex = _items.IndexOfRealizedView(rowPart);
            slot = rowPart.IndexOf(host);
        }
        else
        {
            rowIndex = _items.IndexOfRealizedView(host);
            slot = 0;
        }
        if (rowIndex < 0 || slot < 0 || _model.RowAt(rowIndex) is not { Kind: RowKind.Items } row || slot >= row.Count)
            return null;
        return (row.Group, row.Start + slot);
    }

    /// <summary>The container of the item at <paramref name="index"/> while it is realized, else <c>null</c>.</summary>
    private ItemHost? RealizedHost(int index)
    {
        if ((uint)index >= (uint)_model.ItemCount)
            return null;
        var (group, item) = _model.Locate(index);
        var row = _model.RowOfItem(group, item);
        if (row < 0)
            return null;
        return _items.GetRealizedView(row) switch
        {
            ItemHost host => host,
            GridRowPart rowPart => rowPart.CellAt(item - _model.RowAt(row).Start),
            _ => null
        };
    }

    /// <summary>The items changed: selected items that left the source are no longer selected.</summary>
    private void OnSelectionSourceChanged(IList? removed, bool reset)
    {
        if (!reset && removed is null)
            return;
        _selectionFromSource = true;
        try
        {
            DropRemovedSelection(removed, reset);
        }
        finally
        {
            _selectionFromSource = false;
        }
    }

    private void DropRemovedSelection(IList? removed, bool reset)
    {
        if (_selectedItem is { } selected
            && (reset || removed!.Cast<object?>().Any(item => Equals(item, selected)))
            && _model.IndexOfItem(selected) < 0)
            SelectedItem = null;
        if (_selectedSet.Count == 0)
            return;
        var gone = _selectedSnapshot.Where(item => (reset || removed!.Cast<object?>().Any(removedItem => Equals(removedItem, item))) && _model.IndexOfItem(item) < 0).ToList();
        if (gone.Count > 0)
            ReplaceSelectedItems(_selectedSnapshot.Except(gone).ToArray());
    }

    /// <summary>Follows an <see cref="INotifyCollectionChanged"/> <see cref="SelectedItems"/> list without the list keeping the view alive.</summary>
    private sealed class SelectedItemsObserver(SkUiCollectionView owner)
    {
        private readonly WeakReference<SkUiCollectionView> _owner = new(owner);
        private INotifyCollectionChanged? _source;

        public void Observe(INotifyCollectionChanged? source)
        {
            if (ReferenceEquals(source, _source))
                return;
            if (_source is not null)
                _source.CollectionChanged -= OnChanged;
            _source = source;
            if (source is not null)
                source.CollectionChanged += OnChanged;
        }

        private void OnChanged(object? sender, NotifyCollectionChangedEventArgs args)
        {
            if (_owner.TryGetTarget(out var owner))
                owner.SyncSelectedItems();
            else
                Observe(null);
        }
    }
}
