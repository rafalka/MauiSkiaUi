using System.Collections;
using System.Collections.Specialized;
using MauiSkiaUi.Rendering;

namespace MauiSkiaUi.Core;

/// <summary>
/// A group of drawn chips (Core analogue of <c>SkUiChipGroup</c>): one <see cref="SkUiCoreChip"/> per item of
/// <see cref="ItemsSource"/> (texts from <see cref="ItemDisplay"/>), wrapping onto rows (<see cref="Wraps"/>) or in one row that
/// scrolls sideways, its <see cref="Padding"/> scrolling with the chips. Taps select by <see cref="SelectionMode"/>
/// (<see cref="SelectedItem"/>, <see cref="SelectedItems"/>); an input chip's × raises <see cref="ChipRemoved"/>. Left / Right,
/// Home / End move the keyboard focus between the chips.
/// </summary>
public class SkUiCoreChipGroup : SkUiCoreNode, ISkUiKeyHandler
{
    private readonly List<SkUiCoreChip> _chips = [];
    private readonly List<object?> _items = [];
    private IEnumerable? _itemsSource;
    private Func<object?, string>? _itemDisplay;
    private SkUiWeakListener<SkUiCoreChipGroup>? _itemsListener;
    private SkUiCoreWrapLayout? _wrap;
    private SkUiCoreScrollView? _scroller;
    private SkUiCoreHorizontalStackLayout? _row;
    private SkUiCoreNode? _panel;
    private SkUiChipKind _chipKind = SkUiChipKind.Filter;
    private SkUiChipSelectionMode _selectionMode = SkUiChipSelectionMode.Multiple;
    private object? _selectedItem;
    private List<object> _selectedItems = [];
    private bool _wraps = true;
    private double _spacing = 8;
    private double _rowSpacing = 8;
    private Thickness _padding;
    private bool _syncing;

    /// <summary>Creates a group of filter chips that wrap, several selectable.</summary>
    public SkUiCoreChipGroup() => ShowPanel();

    /// <summary>One chip per item; <see cref="INotifyCollectionChanged"/> sources are followed (weakly).</summary>
    public IEnumerable? ItemsSource { get => _itemsSource; set => SetItemsSource(value); }

    /// <summary>Each item's text (<c>null</c>: <c>ToString()</c>).</summary>
    public Func<object?, string>? ItemDisplay { get => _itemDisplay; set => SetItemDisplay(value); }

    /// <summary>The chips' kind (default <see cref="SkUiChipKind.Filter"/>).</summary>
    public SkUiChipKind ChipKind { get => _chipKind; set => SetChipKind(value); }

    /// <summary>How many chips taps select (default <see cref="SkUiChipSelectionMode.Multiple"/>).</summary>
    public SkUiChipSelectionMode SelectionMode { get => _selectionMode; set => SetSelectionMode(value); }

    /// <summary>The selected item with <see cref="SkUiChipSelectionMode.Single"/> (<c>null</c>: none).</summary>
    public object? SelectedItem { get => _selectedItem; set => SetSelectedItem(value); }

    /// <summary>The selected items with <see cref="SkUiChipSelectionMode.Multiple"/>, in the order they were selected.</summary>
    public IReadOnlyList<object> SelectedItems { get => _selectedItems; set => SetSelectedItems(value); }

    /// <summary>Whether the chips wrap onto rows (default <c>true</c>); <c>false</c>: one row that scrolls sideways.</summary>
    public bool Wraps { get => _wraps; set => SetWraps(value); }

    /// <summary>The gap between chips in a row, in DIPs (default 8).</summary>
    public double Spacing { get => _spacing; set => SetSpacing(value); }

    /// <summary>The gap between rows, in DIPs (default 8).</summary>
    public double RowSpacing { get => _rowSpacing; set => SetRowSpacing(value); }

    /// <summary>Space around the chips; in a scrolling row it scrolls with them.</summary>
    public Thickness Padding { get => _padding; set => SetPadding(value); }

    /// <summary>The chips, one per item, in order.</summary>
    public IReadOnlyList<SkUiCoreChip> Chips => _chips;

    /// <summary>Raised after the selection changed (a tap, or the app).</summary>
    public event EventHandler<SkUiSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>Raised when a chip is removed (its ×, a screen reader's action, Delete / Backspace).</summary>
    public event EventHandler<SkUiChipRemovedEventArgs>? ChipRemoved;

    /// <summary>Sets <see cref="ItemsSource"/>.</summary>
    public SkUiCoreChipGroup SetItemsSource(IEnumerable? value)
    {
        if (ReferenceEquals(_itemsSource, value))
            return this;
        _itemsSource = value;
        (_itemsListener ??= new SkUiWeakListener<SkUiCoreChipGroup>(this, static (group, change) =>
        {
            if (change.Kind == SkUiChangeKind.Collection)
                group.Rebuild();
        })).Listen(value as INotifyCollectionChanged);
        OnPropertyChanged(nameof(ItemsSource));
        Rebuild();
        return this;
    }

    /// <summary>Sets <see cref="ItemDisplay"/>.</summary>
    public SkUiCoreChipGroup SetItemDisplay(Func<object?, string>? value)
    {
        if (SetProperty(ref _itemDisplay, value, nameof(ItemDisplay)))
            Rebuild();
        return this;
    }

    /// <summary>Sets <see cref="ChipKind"/>.</summary>
    public SkUiCoreChipGroup SetChipKind(SkUiChipKind value)
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value), value, "Not a defined chip kind.");
        if (SetProperty(ref _chipKind, value, nameof(ChipKind)))
            foreach (var chip in _chips)
                chip.SetKind(value);
        return this;
    }

    /// <summary>Sets <see cref="SelectionMode"/>.</summary>
    public SkUiCoreChipGroup SetSelectionMode(SkUiChipSelectionMode value)
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value), value, "Not a defined selection mode.");
        if (!SetProperty(ref _selectionMode, value, nameof(SelectionMode)))
            return this;
        if (_selectionMode == SkUiChipSelectionMode.None)
            ClearChips();
        else
            SyncChips();
        foreach (var chip in _chips)
            chip.OnGroupModeChanged(); // a check shows or goes; read as a toggle or a button
        InvalidateSemantics();
        return this;
    }

    /// <summary>Sets <see cref="SelectedItem"/>.</summary>
    public SkUiCoreChipGroup SetSelectedItem(object? value)
    {
        var old = _selectedItem;
        if (Equals(old, value))
            return this;
        _selectedItem = value;
        OnPropertyChanged(nameof(SelectedItem));
        SyncChips();
        if (_selectionMode == SkUiChipSelectionMode.Single)
            RaiseSelectionChanged(new SkUiSelectionChangedEventArgs(old, value));
        return this;
    }

    /// <summary>Sets <see cref="SelectedItems"/> (copied).</summary>
    public SkUiCoreChipGroup SetSelectedItems(IEnumerable<object>? value)
    {
        var next = value?.ToList() ?? [];
        if (next.SequenceEqual(_selectedItems))
            return this;
        var previous = _selectedItems;
        _selectedItems = next;
        OnPropertyChanged(nameof(SelectedItems));
        SyncChips();
        if (_selectionMode == SkUiChipSelectionMode.Multiple)
            RaiseSelectionChanged(new SkUiSelectionChangedEventArgs(previous, next));
        return this;
    }

    /// <summary>Sets <see cref="Wraps"/>.</summary>
    public SkUiCoreChipGroup SetWraps(bool value)
    {
        if (SetProperty(ref _wraps, value, nameof(Wraps)))
            ShowPanel();
        return this;
    }

    /// <summary>Sets <see cref="Spacing"/>.</summary>
    public SkUiCoreChipGroup SetSpacing(double value)
    {
        SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value));
        if (!SetProperty(ref _spacing, value, nameof(Spacing)))
            return this;
        _wrap?.SetSpacing(value);
        _row?.SetSpacing(value);
        return this;
    }

    /// <summary>Sets <see cref="RowSpacing"/>.</summary>
    public SkUiCoreChipGroup SetRowSpacing(double value)
    {
        SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value));
        if (SetProperty(ref _rowSpacing, value, nameof(RowSpacing)))
            _wrap?.SetRowSpacing(value);
        return this;
    }

    /// <summary>Sets <see cref="Padding"/>.</summary>
    public SkUiCoreChipGroup SetPadding(Thickness value)
    {
        SkUiValidate.ThrowIfNotThickness(value, nameof(value));
        if (!SetProperty(ref _padding, value, nameof(Padding)))
            return this;
        _wrap?.SetPadding(value);
        _row?.SetPadding(value);
        return this;
    }

    private void RaiseSelectionChanged(SkUiSelectionChangedEventArgs args)
    {
        SelectionChanged?.Invoke(this, args);
        InvalidateSemantics();
    }

    /// <summary>The panel the chips are in: a wrap layout, or a stack in a horizontal scroller.</summary>
    private void ShowPanel()
    {
        _wrap?.Clear();
        _row?.Clear();
        SkUiCoreNode panel;
        if (_wraps)
        {
            var wrap = _wrap ??= new SkUiCoreWrapLayout();
            wrap.SetSpacing(_spacing).SetRowSpacing(_rowSpacing).SetPadding(_padding);
            foreach (var chip in _chips)
                wrap.Add(chip);
            panel = wrap;
        }
        else
        {
            var row = _row ??= new SkUiCoreHorizontalStackLayout();
            row.SetSpacing(_spacing).SetPadding(_padding);
            foreach (var chip in _chips)
                row.Add(chip);
            panel = _scroller ??= new SkUiCoreScrollView().SetOrientation(ScrollOrientation.Horizontal)
                .SetHorizontalScrollBarVisibility(ScrollBarVisibility.Never);
            if (!ReferenceEquals(_scroller.Content, row))
                _scroller.SetContent(row);
        }
        if (ReferenceEquals(panel, _panel))
            return;
        _panel?.AttachTo(null);
        _panel = panel;
        panel.AttachTo(this);
        InvalidateRender(SkUiRenderDirty.Children);
        InvalidateMeasure();
    }

    private SkUiCorePanel PanelOfChips => _wraps ? _wrap! : _row!;

    /// <summary>One chip per item: chips are reused by position, extra ones removed.</summary>
    private void Rebuild()
    {
        _items.Clear();
        if (_itemsSource is { } source)
            foreach (var item in source)
                _items.Add(item);
        var panel = PanelOfChips;
        while (_chips.Count > _items.Count)
        {
            var last = _chips[^1];
            _chips.RemoveAt(_chips.Count - 1);
            panel.Remove(last);
            last.Group = null;
        }
        for (var index = 0; index < _items.Count; index++)
        {
            SkUiCoreChip chip;
            if (index < _chips.Count)
                chip = _chips[index];
            else
            {
                chip = new SkUiCoreChip { Group = this };
                _chips.Add(chip);
                panel.Add(chip);
            }
            var item = _items[index];
            chip.SetKind(_chipKind);
            chip.SetText(_itemDisplay is { } display ? display(item) : item?.ToString() ?? string.Empty);
            chip.CommandParameter = item;
        }
        SyncChips();
        InvalidateSemantics();
    }

    private bool IsItemSelected(object? item) => _selectionMode switch
    {
        SkUiChipSelectionMode.Single => item is not null && Equals(item, _selectedItem),
        SkUiChipSelectionMode.Multiple => item is not null && _selectedItems.Contains(item),
        _ => false
    };

    /// <summary>The selection mode became None: no chip stays selected.</summary>
    private void ClearChips()
    {
        _syncing = true;
        try
        {
            foreach (var chip in _chips)
                chip.SetIsSelected(false);
        }
        finally
        {
            _syncing = false;
        }
    }

    private void SyncChips()
    {
        // Without selection the group leaves the chips alone (an app may set one); a change to None clears them once.
        if (_selectionMode == SkUiChipSelectionMode.None)
            return;
        _syncing = true;
        try
        {
            for (var index = 0; index < _chips.Count; index++)
                _chips[index].SetIsSelected(IsItemSelected(_items[index]));
        }
        finally
        {
            _syncing = false;
        }
    }

    internal void OnChipTapped(SkUiCoreChip chip)
    {
        var index = _chips.IndexOf(chip);
        if (index >= 0 && _items[index] is { } item)
            Select(item, !chip.IsSelected);
    }

    internal void OnChipSelectedChanged(SkUiCoreChip chip)
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
                    SetSelectedItem(item);
                else if (Equals(_selectedItem, item))
                    SetSelectedItem(null);
                break;
            case SkUiChipSelectionMode.Multiple:
                var next = new List<object>(_selectedItems);
                if (selected)
                {
                    if (!next.Contains(item))
                        next.Add(item);
                }
                else
                    next.Remove(item);
                SetSelectedItems(next);
                break;
        }
    }

    /// <summary>The chip's item and its index, while the chip is one of the group's.</summary>
    internal bool TryGetItem(SkUiCoreChip chip, out object? item, out int index)
    {
        index = _chips.IndexOf(chip);
        item = index >= 0 ? _items[index] : null;
        return index >= 0;
    }

    /// <summary>A chip was removed: <see cref="ChipRemoved"/> with the item it showed then.</summary>
    internal void RaiseChipRemoved(object? item, int index) => ChipRemoved?.Invoke(this, new SkUiChipRemovedEventArgs(item, index));

    /// <inheritdoc />
    internal override IReadOnlyList<IVisualTreeElement> VisualChildren => _panel is null ? [] : [_panel];

    /// <inheritdoc />
    internal override void AddRenderChildren(List<ISkUiRenderable> children)
    {
        if (_panel is not null)
            children.Add(_panel);
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        _panel?.Measure(widthConstraint, heightConstraint) ?? Size.Zero;

    /// <inheritdoc />
    protected override void ArrangeContent(Size size) => _panel?.Arrange(new Rect(0, 0, size.Width, size.Height));

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
            if (_chips[index].Focus())
                break;
        return true; // at an end: the focus stays
    }
}
