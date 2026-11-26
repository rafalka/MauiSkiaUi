using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace MauiSkiaUi;

/// <summary>
/// A group of drawn chips, one <see cref="SkUiChip"/> per item of <see cref="ItemsSource"/> (texts through
/// <see cref="ItemDisplayBinding"/>), wrapping onto rows (<see cref="Wraps"/>, default) or in one horizontally scrolling row whose
/// <see cref="Padding"/> scrolls with the chips. Taps select by <see cref="SelectionMode"/> (two-way <see cref="SelectedItem"/> for
/// one, <see cref="SelectedItems"/> for several); an input chip's × raises <see cref="ChipRemoved"/>, and the app takes the item
/// out of its items. Left / Right, Home / End move the keyboard focus between the chips. Each chip's binding context and
/// <c>CommandParameter</c> are its item, so an implicit <see cref="SkUiChip"/> style can bind to it.
/// </summary>
public class SkUiChipGroup : SkUiView, ISkUiKeyHandler
{
    private readonly List<SkUiChip> _chips = [];
    private readonly List<object?> _items = [];
    private SkUiDisplayBinding? _itemDisplay;
    private SkUiWeakListener<SkUiChipGroup>? _itemsListener;
    private SkUiWeakListener<SkUiChipGroup>? _selectedItemsListener;
    private SkUiWrapLayout? _wrap;
    private SkUiScrollView? _scroller;
    private SkUiHorizontalStackLayout? _row;
    private ISkUiView? _panel;
    private SkUiChipKind _chipKind = SkUiChipKind.Filter;
    private SkUiChipSelectionMode _selectionMode = SkUiChipSelectionMode.Multiple;
    private bool _wraps = true;
    private double _spacing = 8;
    private double _rowSpacing = 8;
    private Thickness _padding;
    private bool _syncing;
    private IReadOnlyList<object> _lastSelection = [];

    /// <summary>Creates a group of filter chips that wrap, several selectable.</summary>
    public SkUiChipGroup() => ShowPanel(); // after styles applied in the base constructor: their chips go into it

    #region Bindable properties

    /// <summary>Bindable <see cref="ItemsSource"/>.</summary>
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable), typeof(SkUiChipGroup), null,
        propertyChanged: (view, _, value) => ((SkUiChipGroup)view).OnItemsSourceChanged((IEnumerable?)value));

    /// <summary>Bindable <see cref="ChipKind"/>.</summary>
    public static readonly BindableProperty ChipKindProperty = BindableProperty.Create(nameof(ChipKind), typeof(SkUiChipKind), typeof(SkUiChipGroup),
        SkUiChipKind.Filter, validateValue: static (_, value) => value is SkUiChipKind kind && Enum.IsDefined(kind),
        propertyChanged: (view, _, value) => ((SkUiChipGroup)view).OnChipKindChanged((SkUiChipKind)value));

    /// <summary>Bindable <see cref="SelectionMode"/>.</summary>
    public static readonly BindableProperty SelectionModeProperty = BindableProperty.Create(nameof(SelectionMode), typeof(SkUiChipSelectionMode), typeof(SkUiChipGroup),
        SkUiChipSelectionMode.Multiple, validateValue: static (_, value) => value is SkUiChipSelectionMode mode && Enum.IsDefined(mode),
        propertyChanged: (view, _, value) => ((SkUiChipGroup)view).OnSelectionModeChanged((SkUiChipSelectionMode)value));

    /// <summary>Bindable <see cref="SelectedItem"/> (two-way by default).</summary>
    public static readonly BindableProperty SelectedItemProperty = BindableProperty.Create(nameof(SelectedItem), typeof(object), typeof(SkUiChipGroup), null,
        BindingMode.TwoWay, propertyChanged: (view, old, value) => ((SkUiChipGroup)view).OnSelectedItemChanged(old, value));

    /// <summary>Bindable <see cref="SelectedItems"/>.</summary>
    public static readonly BindableProperty SelectedItemsProperty = BindableProperty.Create(nameof(SelectedItems), typeof(IList<object>), typeof(SkUiChipGroup), null,
        BindingMode.OneWay,
        defaultValueCreator: bindable =>
        {
            // Created when first read; followed from then on (no property change is raised for a default).
            var list = new ObservableCollection<object>();
            var owner = (SkUiChipGroup)bindable;
            owner.ListenToSelectedItems(list);
            return list;
        },
        coerceValue: (_, value) => value ?? new ObservableCollection<object>(),
        propertyChanged: (view, _, value) => ((SkUiChipGroup)view).OnSelectedItemsChanged((IList<object>)value));

    /// <summary>Bindable <see cref="Wraps"/>.</summary>
    public static readonly BindableProperty WrapsProperty = BindableProperty.Create(nameof(Wraps), typeof(bool), typeof(SkUiChipGroup), true,
        propertyChanged: (view, _, value) => ((SkUiChipGroup)view).OnWrapsChanged((bool)value));

    /// <summary>Bindable <see cref="Spacing"/>.</summary>
    public static readonly BindableProperty SpacingProperty = BindableProperty.Create(nameof(Spacing), typeof(double), typeof(SkUiChipGroup), 8d,
        validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, value) => ((SkUiChipGroup)view).OnSpacingChanged((double)value));

    /// <summary>Bindable <see cref="RowSpacing"/>.</summary>
    public static readonly BindableProperty RowSpacingProperty = BindableProperty.Create(nameof(RowSpacing), typeof(double), typeof(SkUiChipGroup), 8d,
        validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, value) => ((SkUiChipGroup)view).OnRowSpacingChanged((double)value));

    /// <summary>Bindable <see cref="Padding"/>.</summary>
    public static readonly BindableProperty PaddingProperty = BindableProperty.Create(nameof(Padding), typeof(Thickness), typeof(SkUiChipGroup), default(Thickness),
        validateValue: SkUiValidate.ThicknessValue,
        propertyChanged: (view, _, value) => ((SkUiChipGroup)view).OnPaddingChanged((Thickness)value));

    #endregion

    /// <summary>One chip per item; <see cref="System.Collections.Specialized.INotifyCollectionChanged"/> sources are followed (weakly).</summary>
    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>The binding that gives each item's text (as MAUI's <c>Picker.ItemDisplayBinding</c>); <c>null</c>: <c>ToString()</c>.</summary>
    public BindingBase? ItemDisplayBinding
    {
        get => _itemDisplay?.Binding;
        set
        {
            if (ReferenceEquals(_itemDisplay?.Binding, value))
                return;
            _itemDisplay = value is null ? null : new SkUiDisplayBinding(value);
            OnPropertyChanged();
            Rebuild();
        }
    }

    /// <summary>The chips' kind (default <see cref="SkUiChipKind.Filter"/>; <see cref="SkUiChipKind.Input"/> chips show their ×).</summary>
    public SkUiChipKind ChipKind { get => (SkUiChipKind)GetValue(ChipKindProperty); set => SetValue(ChipKindProperty, value); }

    /// <summary>How many chips taps select (default <see cref="SkUiChipSelectionMode.Multiple"/>).</summary>
    public SkUiChipSelectionMode SelectionMode { get => (SkUiChipSelectionMode)GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }

    /// <summary>The selected item with <see cref="SkUiChipSelectionMode.Single"/> (<c>null</c>: none).</summary>
    public object? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }

    /// <summary>
    /// The selected items with <see cref="SkUiChipSelectionMode.Multiple"/>: taps add and remove items in this list (followed when
    /// it raises collection changes); a binding may give one of the view model's.
    /// </summary>
    public IList<object> SelectedItems { get => (IList<object>)GetValue(SelectedItemsProperty); set => SetValue(SelectedItemsProperty, value); }

    /// <summary>Whether the chips wrap onto rows (default <c>true</c>); <c>false</c>: one row that scrolls sideways.</summary>
    public bool Wraps { get => (bool)GetValue(WrapsProperty); set => SetValue(WrapsProperty, value); }

    /// <summary>The gap between chips in a row, in DIPs (default 8).</summary>
    public double Spacing { get => (double)GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    /// <summary>The gap between rows, in DIPs (default 8).</summary>
    public double RowSpacing { get => (double)GetValue(RowSpacingProperty); set => SetValue(RowSpacingProperty, value); }

    /// <summary>Space around the chips; in a scrolling row it scrolls with them (the row's edge insets).</summary>
    public Thickness Padding { get => (Thickness)GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }

    /// <summary>The chips, one per item, in order.</summary>
    public IReadOnlyList<SkUiChip> Chips => _chips;

    /// <summary>Raised after the selection changed (a tap, or the app).</summary>
    public event EventHandler<SkUiSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>Raised when a chip is removed (its ×, a screen reader's action, Delete / Backspace).</summary>
    public event EventHandler<SkUiChipRemovedEventArgs>? ChipRemoved;

    /// <summary>Sets <see cref="ItemsSource"/> (same as the property setter).</summary>
    public SkUiChipGroup SetItemsSource(IEnumerable? value) { ItemsSource = value; return this; }

    /// <summary>Sets <see cref="ItemDisplayBinding"/> (same as the property setter).</summary>
    public SkUiChipGroup SetItemDisplayBinding(BindingBase? value) { ItemDisplayBinding = value; return this; }

    /// <summary>Sets <see cref="ChipKind"/>.</summary>
    public SkUiChipGroup SetChipKind(SkUiChipKind value)
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value), value, "Not a defined chip kind.");
        ChipKind = value;
        return this;
    }

    /// <summary>Sets <see cref="SelectionMode"/>.</summary>
    public SkUiChipGroup SetSelectionMode(SkUiChipSelectionMode value)
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value), value, "Not a defined selection mode.");
        SelectionMode = value;
        return this;
    }

    /// <summary>Sets <see cref="SelectedItem"/> (same as the property setter).</summary>
    public SkUiChipGroup SetSelectedItem(object? value) { SelectedItem = value; return this; }

    /// <summary>Sets <see cref="Wraps"/> (same as the property setter).</summary>
    public SkUiChipGroup SetWraps(bool value) { Wraps = value; return this; }

    /// <summary>Sets <see cref="Spacing"/>.</summary>
    public SkUiChipGroup SetSpacing(double value) { SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value)); Spacing = value; return this; }

    /// <summary>Sets <see cref="RowSpacing"/>.</summary>
    public SkUiChipGroup SetRowSpacing(double value) { SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value)); RowSpacing = value; return this; }

    /// <summary>Sets <see cref="Padding"/>.</summary>
    public SkUiChipGroup SetPadding(Thickness value) { SkUiValidate.ThrowIfNotThickness(value, nameof(value)); Padding = value; return this; }

    #region Changes

    private void OnItemsSourceChanged(IEnumerable? value)
    {
        (_itemsListener ??= new SkUiWeakListener<SkUiChipGroup>(this, static (group, change) =>
        {
            if (change.Kind == SkUiChangeKind.Collection)
                group.Rebuild();
        })).Listen(value as INotifyCollectionChanged);
        Rebuild();
    }

    private void OnChipKindChanged(SkUiChipKind value)
    {
        _chipKind = value;
        foreach (var chip in _chips)
            chip.Kind = value;
    }

    private void OnSelectionModeChanged(SkUiChipSelectionMode value)
    {
        _selectionMode = value;
        // What a later multiple-selection change reports as before.
        _lastSelection = value == SkUiChipSelectionMode.Multiple ? SelectedItems.ToArray() : [];
        if (_selectionMode == SkUiChipSelectionMode.None)
            ClearChips();
        else
            SyncChips();
        foreach (var chip in _chips)
            chip.OnGroupModeChanged(); // a check shows or goes; read as a toggle or a button
        InvalidateSemantics();
    }

    private void OnSelectedItemChanged(object? old, object? value)
    {
        SyncChips();
        if (_selectionMode == SkUiChipSelectionMode.Single)
            RaiseSelectionChanged(new SkUiSelectionChangedEventArgs(old, value));
    }

    private void OnSelectedItemsChanged(IList<object> value)
    {
        ListenToSelectedItems(value);
        SyncSelectedItems();
    }

    /// <summary>Follows the selected items' list (weakly: a long-lived view model's list keeps no group alive).</summary>
    private void ListenToSelectedItems(IList<object> list) =>
        (_selectedItemsListener ??= new SkUiWeakListener<SkUiChipGroup>(this, static (group, change) =>
        {
            if (change.Kind == SkUiChangeKind.Collection)
                group.SyncSelectedItems();
        })).Listen(list as INotifyCollectionChanged);

    /// <summary>The selected items' list changed (a tap, or the app): the chips follow.</summary>
    private void SyncSelectedItems()
    {
        SyncChips();
        if (_selectionMode != SkUiChipSelectionMode.Multiple)
            return;
        var current = SelectedItems.ToArray();
        var previous = _lastSelection;
        _lastSelection = current;
        if (!previous.SequenceEqual(current))
            RaiseSelectionChanged(new SkUiSelectionChangedEventArgs(previous, current));
    }

    private void RaiseSelectionChanged(SkUiSelectionChangedEventArgs args)
    {
        SelectionChanged?.Invoke(this, args);
        InvalidateSemantics();
    }

    private void OnWrapsChanged(bool value)
    {
        if (_wraps == value) return;
        _wraps = value;
        if (_panel is not null) // a style applied in the base constructor: the constructor shows the panel
            ShowPanel();
    }

    private void OnSpacingChanged(double value)
    {
        _spacing = value;
        _wrap?.SetSpacing(value);
        _row?.SetSpacing(value);
    }

    private void OnRowSpacingChanged(double value)
    {
        _rowSpacing = value;
        _wrap?.SetRowSpacing(value);
    }

    private void OnPaddingChanged(Thickness value)
    {
        _padding = value;
        _wrap?.SetPadding(value);
        _row?.SetPadding(value);
    }

    #endregion

    #region Chips

    /// <summary>The panel the chips are in: a wrap layout, or a stack in a horizontal scroller.</summary>
    private void ShowPanel()
    {
        var panelChildren = _wraps ? (_wrap ??= new SkUiWrapLayout()).Children : (_row ??= new SkUiHorizontalStackLayout()).Children;
        var other = _wraps ? _row?.Children : _wrap?.Children;
        other?.Clear();
        panelChildren.Clear();
        foreach (var chip in _chips)
            panelChildren.Add(chip);
        ISkUiView panel;
        if (_wraps)
        {
            _wrap!.SetSpacing(_spacing).SetRowSpacing(_rowSpacing);
            _wrap.Padding = _padding;
            panel = _wrap;
        }
        else
        {
            _row!.SetSpacing(_spacing);
            _row.Padding = _padding;
            panel = _scroller ??= new SkUiScrollView
            {
                Orientation = ScrollOrientation.Horizontal,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
                Content = _row
            };
        }
        if (ReferenceEquals(panel, _panel))
            return;
        if (_panel is not null)
            DetachChild(_panel);
        _panel = panel;
        AttachChild(panel);
    }

    /// <summary>The panel's children, or <c>null</c> before the constructor made the panel (it adds the chips then).</summary>
    private IList<ISkUiView>? PanelChildren => _panel is null ? null : _wraps ? _wrap!.Children : _row!.Children;

    /// <summary>One chip per item: chips are reused by position, extra ones removed.</summary>
    private void Rebuild()
    {
        _items.Clear();
        if (ItemsSource is { } source)
            foreach (var item in source)
                _items.Add(item);
        var texts = (_itemDisplay ?? SkUiDisplayBinding.ItemText).GetTexts(_items);
        var children = PanelChildren;
        while (_chips.Count > _items.Count)
        {
            var last = _chips[^1];
            _chips.RemoveAt(_chips.Count - 1);
            children?.Remove(last);
            last.Group = null;
        }
        for (var index = 0; index < _items.Count; index++)
        {
            SkUiChip chip;
            if (index < _chips.Count)
                chip = _chips[index];
            else
            {
                chip = new SkUiChip { Group = this };
                _chips.Add(chip);
                children?.Add(chip);
            }
            chip.Kind = _chipKind;
            chip.Text = texts[index];
            chip.BindingContext = _items[index];
            chip.CommandParameter = _items[index];
        }
        SyncChips();
        InvalidateSemantics();
    }

    /// <summary>Whether the item is selected in the current mode.</summary>
    private bool IsItemSelected(object? item) => _selectionMode switch
    {
        SkUiChipSelectionMode.Single => item is not null && Equals(item, SelectedItem),
        SkUiChipSelectionMode.Multiple => item is not null && SelectedItems.Contains(item),
        _ => false
    };

    /// <summary>The selection mode became None: no chip stays selected.</summary>
    private void ClearChips()
    {
        _syncing = true;
        try
        {
            foreach (var chip in _chips)
                chip.SelectByUser(false);
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>The chips show the selection.</summary>
    private void SyncChips()
    {
        // Without selection the group leaves the chips alone (an app may set one); a change to None clears them once.
        if (_selectionMode == SkUiChipSelectionMode.None)
            return;
        _syncing = true;
        try
        {
            // Handler specificity: a binding of a chip's IsSelected (an implicit style's) stays.
            for (var index = 0; index < _chips.Count; index++)
                _chips[index].SelectByUser(IsItemSelected(_items[index]));
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>A chip was tapped: the selection follows the mode's rule.</summary>
    internal void OnChipTapped(SkUiChip chip)
    {
        var index = _chips.IndexOf(chip);
        if (index < 0 || _items[index] is not { } item)
            return;
        Select(item, !chip.IsSelected);
    }

    /// <summary>The app set a chip's <see cref="SkUiChip.IsSelected"/>: the group's selection follows.</summary>
    internal void OnChipSelectedChanged(SkUiChip chip)
    {
        if (_syncing)
            return;
        var index = _chips.IndexOf(chip);
        if (index >= 0 && _items[index] is { } item && chip.IsSelected != IsItemSelected(item))
            Select(item, chip.IsSelected);
    }

    private void Select(object item, bool selected)
    {
        switch (_selectionMode)
        {
            case SkUiChipSelectionMode.Single:
                if (selected)
                    WriteBack(SelectedItemProperty, item);
                else if (Equals(SelectedItem, item))
                    WriteBack(SelectedItemProperty, null);
                break;
            case SkUiChipSelectionMode.Multiple:
                var list = SelectedItems;
                if (list.IsReadOnly)
                {
                    var copy = new ObservableCollection<object>(list);
                    if (selected) copy.Add(item); else copy.Remove(item);
                    WriteBack(SelectedItemsProperty, copy);
                }
                else if (selected)
                {
                    if (!list.Contains(item))
                        list.Add(item);
                }
                else
                    list.Remove(item);
                if (list is not INotifyCollectionChanged)
                    SyncSelectedItems(); // a plain list raises nothing
                break;
        }
    }

    /// <summary>The chip's item and its index, while the chip is one of the group's.</summary>
    internal bool TryGetItem(SkUiChip chip, out object? item, out int index)
    {
        index = _chips.IndexOf(chip);
        item = index >= 0 ? _items[index] : null;
        return index >= 0;
    }

    /// <summary>A chip was removed: <see cref="ChipRemoved"/> with the item it showed then.</summary>
    internal void RaiseChipRemoved(object? item, int index) => ChipRemoved?.Invoke(this, new SkUiChipRemovedEventArgs(item, index));

    #endregion

    /// <inheritdoc />
    internal override IEnumerable<ISkUiView> SkiaChildren { get { if (_panel is { } panel) yield return panel; } }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        _panel?.Measure(widthConstraint, heightConstraint) ?? Size.Zero;

    /// <inheritdoc />
    protected override void ArrangeContent(Size size) => _panel?.Arrange(new Rect(0, 0, size.Width, size.Height));

    /// <summary>Left / Right (mirrored right to left), Home / End: the keyboard focus moves to the previous / next, first / last chip.</summary>
    bool ISkUiKeyHandler.OnKeyDown(in SkUiKeyEvent key)
    {
        if (key.HasCommandModifier || _chips.Count == 0)
            return false;
        var focused = _chips.FindIndex(chip => chip.IsFocused);
        if (focused < 0)
            return false;
        var step = IsRightToLeft ? -1 : 1;
        // Where the search starts and which way it goes; a chip that takes no focus (disabled) is passed over.
        var (from, direction) = key.Key switch
        {
            SkUiKey.Right => (focused + step, step),
            SkUiKey.Left => (focused - step, -step),
            SkUiKey.Home => (0, 1),
            SkUiKey.End => (_chips.Count - 1, -1),
            _ => (-2, 0)
        };
        if (direction == 0)
            return false;
        for (var index = from; index >= 0 && index < _chips.Count; index += direction)
            if (SkUiSemantics.RootOf(this)?.FocusManager.Focus(_chips[index]) == true)
                break;
        return true; // at an end: the focus stays
    }
}
