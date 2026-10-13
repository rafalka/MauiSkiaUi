using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace MauiSkiaUi;

public partial class SkUiPicker
{
    private bool _multiple;
    private object[] _selectedSnapshot = [];
    private int[] _selectedIndices = [];
    private bool _changingSelection;
    private SelectedItemsObserver? _selectedItemsObserver;

    /// <summary>The default for <see cref="SelectedTextSeparator"/>.</summary>
    public const string DefaultSelectedTextSeparator = ", ";

    /// <summary>Bindable property for <see cref="SelectionMode"/>.</summary>
    public static readonly BindableProperty SelectionModeProperty = BindableProperty.Create(nameof(SelectionMode), typeof(SkUiSelectionMode), typeof(SkUiPicker),
        SkUiSelectionMode.Single,
        validateValue: (_, value) => (SkUiSelectionMode)value is SkUiSelectionMode.Single or SkUiSelectionMode.Multiple,
        propertyChanged: (view, _, value) => ((SkUiPicker)view).OnSelectionModeChanged((SkUiSelectionMode)value));

    /// <summary>Bindable property for <see cref="SelectedItems"/>.</summary>
    public static readonly BindableProperty SelectedItemsProperty = BindableProperty.Create(nameof(SelectedItems), typeof(IList<object>), typeof(SkUiPicker), null,
        BindingMode.OneWay,
        defaultValueCreator: bindable =>
        {
            // Created when first read; followed from then on (no property change is raised for a default).
            var list = new ObservableCollection<object>();
            var owner = (SkUiPicker)bindable;
            (owner._selectedItemsObserver ??= new SelectedItemsObserver(owner)).Observe(list);
            return list;
        },
        // Never null (a binding may set it): an empty list of the picker's own instead.
        coerceValue: (_, value) => value ?? new ObservableCollection<object>(),
        propertyChanged: (view, _, value) => ((SkUiPicker)view).OnSelectedItemsChanged((IList<object>?)value));

    /// <summary>Bindable property for <see cref="IsPopupClosedOnSelection"/>.</summary>
    public static readonly BindableProperty IsPopupClosedOnSelectionProperty = BindableProperty.Create(nameof(IsPopupClosedOnSelection), typeof(bool), typeof(SkUiPicker), false);

    /// <summary>
    /// With several choices (and <see cref="SkUiPickerUpdateMode.Immediately"/>): whether the list closes after each toggled row, as
    /// a combo box that adds one item at a time (default <c>false</c>: it stays open). A single choice always closes. Applies
    /// from the next opening. SkiaUi extension.
    /// </summary>
    public bool IsPopupClosedOnSelection { get => (bool)GetValue(IsPopupClosedOnSelectionProperty); set => SetValue(IsPopupClosedOnSelectionProperty, value); }

    /// <summary>Sets <see cref="IsPopupClosedOnSelection"/> (same as the property setter).</summary>
    public SkUiPicker SetIsPopupClosedOnSelection(bool value) { IsPopupClosedOnSelection = value; return this; }

    /// <summary>Bindable property for <see cref="SelectedItemsDisplay"/>.</summary>
    public static readonly BindableProperty SelectedItemsDisplayProperty = BindableProperty.Create(nameof(SelectedItemsDisplay), typeof(SkUiSelectedItemsDisplay),
        typeof(SkUiPicker), SkUiSelectedItemsDisplay.Text, validateValue: (_, value) => Enum.IsDefined((SkUiSelectedItemsDisplay)value),
        propertyChanged: (view, _, _) => ((SkUiPicker)view).UpdateMultipleState());

    /// <summary>Bindable property for <see cref="MaxTokenLines"/>.</summary>
    public static readonly BindableProperty MaxTokenLinesProperty = BindableProperty.Create(nameof(MaxTokenLines), typeof(int), typeof(SkUiPicker), -1,
        validateValue: (_, value) => (int)value is -1 or > 0,
        propertyChanged: (view, _, value) => ((SkUiPicker)view).OnMaxTokenLinesChanged((int)value));

    /// <summary>
    /// With several choices: how the field shows them, <see cref="SkUiSelectedItemsDisplay.Text"/> (default: the texts joined, or a
    /// count) or <see cref="SkUiSelectedItemsDisplay.Tokens"/> (a token each, drawn by the look, whose × removes the item; Backspace
    /// on the focused field removes the last one; the field grows by lines up to <see cref="MaxTokenLines"/>). SkiaUi extension.
    /// </summary>
    public SkUiSelectedItemsDisplay SelectedItemsDisplay { get => (SkUiSelectedItemsDisplay)GetValue(SelectedItemsDisplayProperty); set => SetValue(SelectedItemsDisplayProperty, value); }

    /// <summary>
    /// With tokens: the most lines of them in the field (default -1: as many as they need); the tokens that do not fit show as one
    /// "+N" token (<see cref="SkUiStrings.MoreTokensFormat"/>). SkiaUi extension.
    /// </summary>
    public int MaxTokenLines { get => (int)GetValue(MaxTokenLinesProperty); set => SetValue(MaxTokenLinesProperty, value); }

    /// <summary>Sets <see cref="SelectedItemsDisplay"/> (same as the property setter).</summary>
    public SkUiPicker SetSelectedItemsDisplay(SkUiSelectedItemsDisplay value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        SelectedItemsDisplay = value;
        return this;
    }

    /// <summary>Sets <see cref="MaxTokenLines"/> (same as the property setter; -1: no limit).</summary>
    public SkUiPicker SetMaxTokenLines(int value)
    {
        if (value is not (-1 or > 0)) throw new ArgumentOutOfRangeException(nameof(value), value, "The lines must be positive, or -1 for no limit.");
        MaxTokenLines = value;
        return this;
    }

    private void OnMaxTokenLinesChanged(int value)
    {
        Model.MaxTokenLines = value;
        Model.Tokens = Model.Tokens; // laid out again
        Model.TextChanged();
    }

    /// <inheritdoc />
    private protected override void RemoveChoice(int index)
    {
        if (_multiple && Model.IsItemEnabled(index) && ItemAtIndex(index) is { } item && SelectedItems.Contains(item))
            ReplaceSelectedItems([.. _selectedSnapshot.Where(chosen => !Equals(chosen, item))]);
    }

    /// <summary>Bindable property for <see cref="IsSelectAllVisible"/>.</summary>
    public static readonly BindableProperty IsSelectAllVisibleProperty = BindableProperty.Create(nameof(IsSelectAllVisible), typeof(bool), typeof(SkUiPicker), false);

    /// <summary>Bindable property for <see cref="SelectAllText"/>.</summary>
    public static readonly BindableProperty SelectAllTextProperty = BindableProperty.Create(nameof(SelectAllText), typeof(string), typeof(SkUiPicker), null);

    /// <summary>
    /// With several choices: whether a "Select all" row shows above the rows, a check box that is checked when every item that can
    /// be chosen is, in between when some are; a tap chooses them all, or none when all were (one change). Default <c>false</c>.
    /// Applies from the next opening. SkiaUi extension.
    /// </summary>
    public bool IsSelectAllVisible { get => (bool)GetValue(IsSelectAllVisibleProperty); set => SetValue(IsSelectAllVisibleProperty, value); }

    /// <summary>The "Select all" row's text; <c>null</c> (default): <see cref="SkUiStrings.SelectAllText"/>. SkiaUi extension.</summary>
    public string? SelectAllText { get => (string?)GetValue(SelectAllTextProperty); set => SetValue(SelectAllTextProperty, value); }

    /// <summary>Sets <see cref="IsSelectAllVisible"/> (same as the property setter).</summary>
    public SkUiPicker SetIsSelectAllVisible(bool value) { IsSelectAllVisible = value; return this; }

    /// <summary>Sets <see cref="SelectAllText"/> (same as the property setter).</summary>
    public SkUiPicker SetSelectAllText(string? value) { SelectAllText = value; return this; }

    /// <summary>Bindable property for <see cref="SelectedTextSeparator"/>.</summary>
    public static readonly BindableProperty SelectedTextSeparatorProperty = BindableProperty.Create(nameof(SelectedTextSeparator), typeof(string), typeof(SkUiPicker),
        DefaultSelectedTextSeparator, validateValue: SkUiValidate.NotNull,
        propertyChanged: (view, _, _) => ((SkUiPicker)view).UpdateMultipleState());

    /// <summary>Bindable property for <see cref="SelectedCountFormat"/>.</summary>
    public static readonly BindableProperty SelectedCountFormatProperty = BindableProperty.Create(nameof(SelectedCountFormat), typeof(string), typeof(SkUiPicker), null,
        propertyChanged: (view, _, _) => ((SkUiPicker)view).UpdateMultipleState());

    /// <summary>
    /// <see cref="SkUiSelectionMode.Single"/> (default): the list chooses one item, as MAUI's picker.
    /// <see cref="SkUiSelectionMode.Multiple"/>: the rows are check boxes; a tap toggles an item and the list stays open (it closes
    /// like a combo box's, on a dismissal, Enter or Escape), every change applies at once. Then <see cref="SelectedItems"/> holds the
    /// choice and <see cref="SelectedIndex"/> / <see cref="SelectedItem"/> stay -1 / <c>null</c> (setting them does nothing).
    /// Switching keeps the first choice from one mode to the other. SkiaUi extension.
    /// </summary>
    public SkUiSelectionMode SelectionMode { get => (SkUiSelectionMode)GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }

    /// <summary>
    /// The chosen items with <see cref="SkUiSelectionMode.Multiple"/> (items of <see cref="ItemsSource"/>, else texts of
    /// <see cref="Items"/>), in the order of the items when the user chooses. Change this list (an <see cref="INotifyCollectionChanged"/>
    /// one is followed, weakly) or set a new one, to choose from code. Items that leave the list are dropped from it. Never <c>null</c>.
    /// SkiaUi extension.
    /// </summary>
    public IList<object> SelectedItems { get => (IList<object>)GetValue(SelectedItemsProperty); set => SetValue(SelectedItemsProperty, value); }

    /// <summary>The indices of <see cref="SelectedItems"/> in the items, ascending (empty without <see cref="SkUiSelectionMode.Multiple"/>).</summary>
    public IReadOnlyList<int> SelectedIndices => _selectedIndices;

    /// <summary>What goes between the chosen texts in the field with several choices (default <c>", "</c>).</summary>
    public string SelectedTextSeparator { get => (string)GetValue(SelectedTextSeparatorProperty); set => SetValue(SelectedTextSeparatorProperty, value); }

    /// <summary>
    /// With several choices, a format for the field when two or more items are chosen, <c>string.Format</c>'s with the count as
    /// <c>{0}</c> (<c>"{0} selected"</c>); <c>null</c> (default): the chosen texts joined by <see cref="SelectedTextSeparator"/>.
    /// </summary>
    public string? SelectedCountFormat { get => (string?)GetValue(SelectedCountFormatProperty); set => SetValue(SelectedCountFormatProperty, value); }

    /// <summary>
    /// Raised once for each change of <see cref="SelectedItems"/> with <see cref="SkUiSelectionMode.Multiple"/> (a tap, the app,
    /// items that left the list): the items before and after.
    /// </summary>
    public event EventHandler<SkUiSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>Sets <see cref="SelectionMode"/> (same as the property setter).</summary>
    public SkUiPicker SetSelectionMode(SkUiSelectionMode value)
    {
        if (value is not (SkUiSelectionMode.Single or SkUiSelectionMode.Multiple)) throw new ArgumentOutOfRangeException(nameof(value));
        SelectionMode = value;
        return this;
    }

    /// <summary>Sets <see cref="SelectedItems"/> (same as the property setter).</summary>
    public SkUiPicker SetSelectedItems(IList<object> value) { ArgumentNullException.ThrowIfNull(value); SelectedItems = value; return this; }

    /// <summary>Sets <see cref="SelectedTextSeparator"/> (same as the property setter).</summary>
    public SkUiPicker SetSelectedTextSeparator(string value) { ArgumentNullException.ThrowIfNull(value); SelectedTextSeparator = value; return this; }

    /// <summary>Sets <see cref="SelectedCountFormat"/> (same as the property setter).</summary>
    public SkUiPicker SetSelectedCountFormat(string? value) { SelectedCountFormat = value; return this; }

    /// <summary>Chooses every item (<see cref="SkUiSelectionMode.Multiple"/> only; otherwise nothing happens), raising <see cref="SelectionChanged"/> once.</summary>
    public void SelectAll()
    {
        if (!_multiple)
            return;
        var all = new List<object>(_items.Count);
        for (var index = 0; index < _items.Count; index++)
            // Disabled items keep their state: chosen ones stay chosen.
            if ((Model.IsItemEnabled(index) || _selectedIndices.Contains(index)) && ItemAtIndex(index) is { } item)
                all.Add(item);
        ReplaceSelectedItems(all);
    }

    /// <inheritdoc />
    private protected override void ClearChoice()
    {
        if (_multiple)
            ReplaceSelectedItems([]);
        else
            SelectedIndex = -1;
    }

    private void OnSelectionModeChanged(SkUiSelectionMode mode)
    {
        var multiple = mode == SkUiSelectionMode.Multiple;
        if (multiple == _multiple)
            return;
        // The popup shows the other kind of list: it closes.
        ClosePopup();
        try
        {
            ChangeSelectionMode(multiple);
        }
        finally
        {
            UpdateFieldView();
        }
    }

    private void ChangeSelectionMode(bool multiple)
    {
        if (multiple)
        {
            var single = SelectedItem;
            _multiple = true;
            Model.UsesValueText = true;
            Model.ClosedFieldSelects = false;
            Model.ContentFactory = BuildMultipleContent;
            SetSelectedIndexInternally(-1);
            if (single is not null)
                ReplaceSelectedItems([single]);
            else
                SyncSelectedItems(force: true);
            return;
        }
        var first = _selectedIndices.Length > 0 ? _selectedIndices[0] : -1;
        _multiple = false;
        Model.ShowsTokens = false;
        Model.Tokens = [];
        Model.UsesValueText = false;
        Model.ClosedFieldSelects = true;
        Model.ContentFactory = null;
        ReplaceSelectedItems([]);
        _selectedIndices = [];
        SetSelectedIndexInternally(first);
        Model.SelectionChanged();
    }

    private void OnSelectedItemsChanged(IList<object>? value)
    {
        (_selectedItemsObserver ??= new SelectedItemsObserver(this)).Observe(value as INotifyCollectionChanged);
        SyncSelectedItems();
    }

    /// <summary>Reads <see cref="SelectedItems"/> again: shows it and raises <see cref="SelectionChanged"/> when it differs.</summary>
    private void SyncSelectedItems(bool force = false)
    {
        if (_changingSelection || !_multiple)
            return;
        var previous = _selectedSnapshot;
        var current = SelectedItems.ToArray();
        if (!force && previous.Length == current.Length && previous.SequenceEqual(current))
            return;
        _selectedSnapshot = current;
        UpdateMultipleState();
        if (previous.Length == current.Length && previous.SequenceEqual(current))
            return;
        Model.RunSelectionChangedCommand();
        SelectionChanged?.Invoke(this, new SkUiSelectionChangedEventArgs(previous, current));
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

    /// <summary>
    /// The items changed: chosen items that are no longer there leave <see cref="SelectedItems"/>, and the indices and the field's
    /// text are worked out again. <paramref name="keepUnknown"/>: the picker had no items yet (the choice came first), so
    /// items it does not know yet stay.
    /// </summary>
    private void ReconcileMultiple(bool keepUnknown)
    {
        var kept = keepUnknown ? _selectedSnapshot : _selectedSnapshot.Where(item => IndexOfItem(item) >= 0).ToArray();
        if (kept.Length != _selectedSnapshot.Length)
            ReplaceSelectedItems(kept);
        UpdateMultipleState();
    }

    /// <summary>The indices of the chosen items, the field's text, and an open popup's checks.</summary>
    private void UpdateMultipleState()
    {
        if (!_multiple || Model is null)
            return;
        var indices = new SortedSet<int>();
        foreach (var item in _selectedSnapshot)
            if (IndexOfItem(item) is var index and >= 0)
                indices.Add(index);
        var changed = !_selectedIndices.SequenceEqual(indices);
        _selectedIndices = [.. indices];
        Model.ShowsTokens = SelectedItemsDisplay == SkUiSelectedItemsDisplay.Tokens;
        Model.Tokens = [.. _selectedIndices.Select(index => new SkUiPickerToken(FieldTextOf(index), index, Model.IsItemEnabled(index)))];
        Model.ValueText = ComposeSelectedText();
        // Only a change of the chosen items reaches an open popup: its checks follow them, which would wipe a draft waiting for
        // OK (WhenFinished) on a change of the field's text alone (the separator, the count format, tokens, the items' states).
        if (changed)
            Model.SelectionChanged();
        else
            Model.TextChanged();
    }

    private string? ComposeSelectedText()
    {
        if (_selectedIndices.Length == 0)
            return null;
        if (SelectedCountFormat is { } format && _selectedIndices.Length > 1)
            return string.Format(System.Globalization.CultureInfo.CurrentCulture, format, _selectedIndices.Length);
        return string.Join(SelectedTextSeparator, _selectedIndices.Select(FieldTextOf));
    }

    private int IndexOfItem(object item) => _itemsSource?.IndexOf(item) ?? (item is string text ? _items.IndexOf(text) : -1);

    private object? ItemAtIndex(int index)
    {
        if (index < 0)
            return null;
        if (_itemsSource is { } source)
            return index < source.Count ? source[index] : null;
        return index < _items.Count ? _items[index] : null;
    }

    /// <summary>The popup chose these indices (ascending): they become <see cref="SelectedItems"/>.</summary>
    private void ApplyChosenIndices(IReadOnlyList<int> indices)
    {
        if (!_multiple)
            return;
        var items = new List<object>(indices.Count);
        foreach (var index in indices)
            if (ItemAtIndex(index) is { } item)
                items.Add(item);
        ReplaceSelectedItems(items);
    }

    private ISkUiPickerPopupContent BuildMultipleContent(SkUiPickerPopupContext context)
    {
        // Weak: a request kept by a presenter (a popup still shown after its page went) keeps no picker alive.
        var weak = new WeakReference<SkUiPicker>(this);
        return new SkUiPickerPopup(_items.ToArray(), -1, Model.ItemTemplate, _itemsSource, context.RowStyle, context.Title, context.RightToLeft,
            context.Chrome, indices =>
            {
                if (weak.TryGetTarget(out var picker))
                    picker.ApplyChosenIndices(indices);
            },
            () => weak.TryGetTarget(out var picker) ? picker._selectedIndices : [], _selectedIndices, IsPopupClosedOnSelection, Model.ItemEnabled,
            context.Extras, IsSelectAllVisible ? SelectAllText ?? SkUiStrings.Current.SelectAllText : null);
    }

    /// <summary>Follows an <see cref="INotifyCollectionChanged"/> <see cref="SelectedItems"/> list without the list keeping the picker alive.</summary>
    private sealed class SelectedItemsObserver(SkUiPicker owner)
    {
        private readonly WeakReference<SkUiPicker> _owner = new(owner);
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
