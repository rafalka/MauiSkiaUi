using System.Globalization;

namespace MauiSkiaUi.Core;

public partial class SkUiCorePicker
{
    private bool _multiple;
    private SkUiSelectionMode _selectionMode = SkUiSelectionMode.Single;
    private object[] _selected = [];
    private int[] _selectedIndices = [];
    private string _selectedTextSeparator = SkUiPicker.DefaultSelectedTextSeparator;
    private string? _selectedCountFormat;

    /// <summary>
    /// <see cref="SkUiSelectionMode.Single"/> (default): the list chooses one item. <see cref="SkUiSelectionMode.Multiple"/>: the rows are
    /// check boxes, a tap toggles an item and the list stays open; <see cref="SelectedItems"/> holds the choice and
    /// <see cref="SelectedIndex"/> / <see cref="SelectedItem"/> stay -1 / <c>null</c>. See <c>SkUiPicker.SelectionMode</c>.
    /// </summary>
    public SkUiSelectionMode SelectionMode { get => _selectionMode; set => SetSelectionMode(value); }

    /// <summary>The chosen items with <see cref="SkUiSelectionMode.Multiple"/> (items of <see cref="ItemsSource"/>, else texts of <see cref="Items"/>); see <see cref="SetSelectedItems"/>.</summary>
    public IReadOnlyList<object> SelectedItems { get => _selected; set => SetSelectedItems(value); }

    /// <summary>The indices of <see cref="SelectedItems"/> in the items, ascending (empty without <see cref="SkUiSelectionMode.Multiple"/>).</summary>
    public IReadOnlyList<int> SelectedIndices => _selectedIndices;

    /// <summary>What goes between the chosen texts in the field with several choices (default <c>", "</c>).</summary>
    public string SelectedTextSeparator { get => _selectedTextSeparator; set => SetSelectedTextSeparator(value); }

    /// <summary>A format for the field when two or more items are chosen, with the count as <c>{0}</c> (<c>"{0} selected"</c>); <c>null</c> (default): the texts joined.</summary>
    public string? SelectedCountFormat { get => _selectedCountFormat; set => SetSelectedCountFormat(value); }

    /// <summary>Raised once for each change of <see cref="SelectedItems"/> with <see cref="SkUiSelectionMode.Multiple"/>: the items before and after.</summary>
    public event EventHandler<SkUiSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>Sets <see cref="SelectionMode"/> (<see cref="SkUiSelectionMode.Single"/> or <see cref="SkUiSelectionMode.Multiple"/>; the first choice is kept across a switch).</summary>
    public SkUiCorePicker SetSelectionMode(SkUiSelectionMode value)
    {
        if (value is not (SkUiSelectionMode.Single or SkUiSelectionMode.Multiple)) throw new ArgumentOutOfRangeException(nameof(value));
        if (!SetProperty(ref _selectionMode, value, nameof(SelectionMode)))
            return this;
        // The popup shows the other kind of list: it closes.
        SetIsOpen(false);
        if (value == SkUiSelectionMode.Multiple)
        {
            var single = _selectedItem;
            _multiple = true;
            Model.UsesValueText = true;
            Model.ClosedFieldSelects = false;
            Model.ContentFactory = BuildMultipleContent;
            _pendingSelectedIndex = -1;
            ApplySelectedIndex(-1, raise: true);
            ApplyChoice(single is not null ? [single] : _selected);
            return this;
        }
        var first = _selectedIndices.Length > 0 ? _selectedIndices[0] : -1;
        var previous = _selected;
        _multiple = false;
        Model.UsesValueText = false;
        Model.ClosedFieldSelects = true;
        Model.ContentFactory = null;
        _selected = [];
        _selectedIndices = [];
        ApplySelectedIndex(first, raise: true);
        if (previous.Length > 0)
            SelectionChanged?.Invoke(this, new SkUiSelectionChangedEventArgs(previous, []));
        return this;
    }

    /// <summary>
    /// Sets <see cref="SelectedItems"/> (items not in the picker are dropped when it has items; before that they wait). Without
    /// <see cref="SkUiSelectionMode.Multiple"/> the items are kept for when the mode is set.
    /// </summary>
    public SkUiCorePicker SetSelectedItems(IEnumerable<object> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var chosen = items.ToArray();
        if (_multiple)
            ApplyChoice(chosen);
        else
            _selected = chosen;
        return this;
    }

    /// <summary>Sets <see cref="SelectedTextSeparator"/>.</summary>
    public SkUiCorePicker SetSelectedTextSeparator(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (SetProperty(ref _selectedTextSeparator, value, nameof(SelectedTextSeparator)))
            UpdateMultipleState();
        return this;
    }

    /// <summary>Sets <see cref="SelectedCountFormat"/>.</summary>
    public SkUiCorePicker SetSelectedCountFormat(string? value)
    {
        if (SetProperty(ref _selectedCountFormat, value, nameof(SelectedCountFormat)))
            UpdateMultipleState();
        return this;
    }

    /// <summary>Chooses every item (<see cref="SkUiSelectionMode.Multiple"/> only), raising <see cref="SelectionChanged"/> once.</summary>
    public void SelectAll()
    {
        if (!_multiple)
            return;
        var all = new List<object>(_items.Count);
        for (var index = 0; index < _items.Count; index++)
            if (ItemAt(index) is { } item)
                all.Add(item);
        ApplyChoice(all.ToArray());
    }

    /// <summary>Clears the choice (<see cref="SkUiSelectionMode.Multiple"/>: <see cref="SelectedItems"/>; else <see cref="SelectedIndex"/>), raising the change event once.</summary>
    public void ClearSelection()
    {
        if (_multiple)
            ApplyChoice([]);
        else
            SetSelectedIndex(-1);
    }

    /// <summary>Takes <paramref name="chosen"/> as the choice: the indices, the field's text, the open popup, <see cref="SelectionChanged"/>.</summary>
    private void ApplyChoice(object[] chosen)
    {
        var previous = _selected;
        _selected = chosen;
        UpdateMultipleState();
        OnPropertyChanged(nameof(SelectedItems));
        if (!(previous.Length == chosen.Length && previous.SequenceEqual(chosen)))
            SelectionChanged?.Invoke(this, new SkUiSelectionChangedEventArgs(previous, chosen));
    }

    /// <summary>
    /// The items changed: chosen items that are no longer there leave the choice, and the indices and the field's text are worked out
    /// again. <paramref name="keepUnknown"/>: the picker had no items yet (the choice came first), so unknown items stay.
    /// </summary>
    private void ReconcileMultiple(bool keepUnknown)
    {
        var kept = keepUnknown ? _selected : _selected.Where(item => IndexOfItem(item) >= 0).ToArray();
        if (kept.Length != _selected.Length)
            ApplyChoice(kept);
        else
            UpdateMultipleState();
    }

    private void UpdateMultipleState()
    {
        if (!_multiple)
            return;
        var indices = new SortedSet<int>();
        foreach (var item in _selected)
            if (IndexOfItem(item) is var index and >= 0)
                indices.Add(index);
        _selectedIndices = [.. indices];
        Model.ValueText = ComposeSelectedText();
        Model.SelectionChanged();
    }

    private string? ComposeSelectedText()
    {
        if (_selectedIndices.Length == 0)
            return null;
        if (_selectedCountFormat is { } format && _selectedIndices.Length > 1)
            return string.Format(CultureInfo.CurrentCulture, format, _selectedIndices.Length);
        return string.Join(_selectedTextSeparator, _selectedIndices.Select(index => _items[index]));
    }

    /// <summary>The popup chose these indices (ascending): they become the choice.</summary>
    private void ApplyChosenIndices(IReadOnlyList<int> indices)
    {
        if (!_multiple)
            return;
        var items = new List<object>(indices.Count);
        foreach (var index in indices)
            if (ItemAt(index) is { } item)
                items.Add(item);
        ApplyChoice(items.ToArray());
    }

    private ISkUiPickerPopupContent BuildMultipleContent(SkUiPickerPopupContext context)
    {
        // Weak: a request kept by a presenter (a popup still shown after its page went) keeps no picker alive.
        var weak = new WeakReference<SkUiCorePicker>(this);
        return new SkUiPickerPopup(_items.ToArray(), -1, Model.ItemTemplate, _itemsSource, context.RowStyle, context.Title, context.RightToLeft,
            context.Chrome, indices =>
            {
                if (weak.TryGetTarget(out var picker))
                    picker.ApplyChosenIndices(indices);
            },
            () => weak.TryGetTarget(out var picker) ? picker._selectedIndices : [], _selectedIndices);
    }
}
