using System.Collections;
using System.Collections.Specialized;

namespace MauiSkiaUi;

/// <summary>
/// One column of a <see cref="SkUiMultiColumnPicker"/> (or <see cref="Core.SkUiCoreMultiColumnPicker"/>): the items to choose from
/// (<see cref="ItemsSource"/>, shown as <see cref="ItemDisplayBinding"/>, else <c>ToString()</c>), the chosen one
/// (<see cref="SelectedIndex"/>, <see cref="SelectedItem"/>, both two-way) and how its wheel looks (<see cref="Loop"/>,
/// <see cref="Width"/>). Bindable, so XAML columns bind to a view model; the picker follows every change (a column can be shared
/// by pickers, and does not keep them alive).
/// </summary>
/// <remarks>
/// <para>
/// Screen readers read each wheel as an adjustable element named by MAUI's <c>SemanticProperties.Description</c> set on the column
/// (<see cref="SetSemanticDescription"/>), else its <see cref="Header"/>, with <c>SemanticProperties.Hint</c> as its hint.
/// </para>
/// <para>
/// The selection follows the rules of <see cref="SkUiPicker"/> where they fit: an index beyond the items is clamped to the last
/// item, an index set before the items applies when they come, and a selected item that moves in a changing source stays selected.
/// </para>
/// </remarks>
public class SkUiPickerColumn : BindableObject
{
    private static readonly BindableProperty DisplayProperty = BindableProperty.Create("Display", typeof(string), typeof(SkUiPickerColumn), null);
    private static readonly BindableProperty EnabledProperty = BindableProperty.Create("Enabled", typeof(bool), typeof(SkUiPickerColumn), true);

    /// <summary>
    /// The name this column raises <see cref="BindableObject.PropertyChanged"/> with once its selection changed and
    /// <see cref="SelectedIndex"/> and <see cref="SelectedItem"/> agree again (MAUI raises a property's change before its own callback
    /// runs, so the item still lags the index at the index's change).
    /// </summary>
    internal const string SelectionChanged = "Selection";

    /// <summary>The name this column raises <see cref="BindableObject.PropertyChanged"/> with when which of its items can be chosen changed.</summary>
    internal const string ItemStatesChanged = "ItemStates";

    /// <summary>The name this column raises <see cref="BindableObject.PropertyChanged"/> with when its items (their texts) changed.</summary>
    internal const string TextsChanged = "Texts";

    private readonly SkUiWeakListener<SkUiPickerColumn> _sourceListener;
    private string[]? _texts;
    private BindingBase? _itemDisplayBinding;
    private BindableObject? _displayProbe;
    private BindingBase? _itemEnabledBinding;
    private BindableObject? _enabledProbe;
    private bool[]? _itemStates;
    private bool _itemStatesValid;
    private object? _pendingItem;
    private bool _syncing;

    /// <summary>Creates a column without items.</summary>
    public SkUiPickerColumn() =>
        _sourceListener = new SkUiWeakListener<SkUiPickerColumn>(this, static (column, change) =>
        {
            if (change.Kind == SkUiChangeKind.Collection)
                column.OnItemsChanged(keepSelectedItem: true);
        });

    /// <summary>Bindable property for <see cref="ItemsSource"/>.</summary>
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(nameof(ItemsSource), typeof(IList), typeof(SkUiPickerColumn), null,
        propertyChanged: (column, _, value) => ((SkUiPickerColumn)column).OnItemsSourceChanged((IList?)value));

    /// <summary>Bindable property for <see cref="SelectedIndex"/> (two-way by default).</summary>
    public static readonly BindableProperty SelectedIndexProperty = BindableProperty.Create(nameof(SelectedIndex), typeof(int), typeof(SkUiPickerColumn), -1, BindingMode.TwoWay,
        validateValue: (_, value) => (int)value >= -1,
        coerceValue: (column, value) => ((SkUiPickerColumn)column).CoerceIndex((int)value),
        propertyChanged: (column, _, _) => ((SkUiPickerColumn)column).OnSelectedIndexChanged());

    /// <summary>Bindable property for <see cref="SelectedItem"/> (two-way by default).</summary>
    public static readonly BindableProperty SelectedItemProperty = BindableProperty.Create(nameof(SelectedItem), typeof(object), typeof(SkUiPickerColumn), null, BindingMode.TwoWay,
        propertyChanged: (column, _, value) => ((SkUiPickerColumn)column).OnSelectedItemChanged(value));

    /// <summary>Bindable property for <see cref="Loop"/>.</summary>
    public static readonly BindableProperty LoopProperty = BindableProperty.Create(nameof(Loop), typeof(bool), typeof(SkUiPickerColumn), false);

    /// <summary>Bindable property for <see cref="Header"/>.</summary>
    public static readonly BindableProperty HeaderProperty = BindableProperty.Create(nameof(Header), typeof(string), typeof(SkUiPickerColumn), null);

    /// <summary>Bindable property for <see cref="ItemTemplate"/>.</summary>
    public static readonly BindableProperty ItemTemplateProperty = BindableProperty.Create(nameof(ItemTemplate), typeof(DataTemplate), typeof(SkUiPickerColumn), null);

    /// <summary>Bindable property for <see cref="Width"/>.</summary>
    public static readonly BindableProperty WidthProperty = BindableProperty.Create(nameof(Width), typeof(double), typeof(SkUiPickerColumn), -1d,
        validateValue: (_, value) => value is double width && IsValidWidth(width));

    /// <summary>
    /// The items of the column (any <see cref="IList"/>; an <see cref="INotifyCollectionChanged"/> one is followed, weakly). Each shows
    /// as its <see cref="ItemDisplayBinding"/>, else <c>ToString()</c>.
    /// </summary>
    public IList? ItemsSource { get => (IList?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>The binding that picks the text shown for each item (MAUI's <c>Picker</c> rule; <c>null</c>: <c>ToString()</c>).</summary>
    public BindingBase? ItemDisplayBinding
    {
        get => _itemDisplayBinding;
        set
        {
            if (ReferenceEquals(_itemDisplayBinding, value))
                return;
            OnPropertyChanging();
            _itemDisplayBinding = value;
            if (_displayProbe is not null)
            {
                _displayProbe.RemoveBinding(DisplayProperty);
                _displayProbe = null;
            }
            OnItemsChanged(keepSelectedItem: false);
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// The binding that says whether an item can be chosen (a <c>bool</c> of the item, e.g. <c>{Binding IsAvailable}</c>);
    /// <c>null</c> (default): every item. Disabled items show dimmed, a wheel does not stop on them (it rolls on to the nearest one
    /// that can be chosen) and the keyboard passes them by. Read when the items change and on <see cref="RefreshItemStates"/>
    /// (an open popup follows). SkiaUi extension.
    /// </summary>
    public BindingBase? ItemEnabledBinding
    {
        get => _itemEnabledBinding;
        set
        {
            if (ReferenceEquals(_itemEnabledBinding, value))
                return;
            OnPropertyChanging();
            _itemEnabledBinding = value;
            if (_enabledProbe is not null)
            {
                _enabledProbe.RemoveBinding(EnabledProperty);
                _enabledProbe = null;
            }
            RefreshItemStates();
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Reads which items can be chosen again (<see cref="ItemEnabledBinding"/>), after the app's data changed: an open popup's
    /// wheel dims and passes by the items now disabled (moving off its item when that one is), as a date picker's days of a
    /// shorter month.
    /// </summary>
    public void RefreshItemStates()
    {
        _itemStatesValid = false;
        OnPropertyChanged(ItemStatesChanged);
    }

    /// <summary>The index of the chosen item; -1 (default): none. Clamped to the items. Two-way by default.</summary>
    public int SelectedIndex { get => (int)GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }

    /// <summary>The chosen item (of <see cref="ItemsSource"/>), or <c>null</c>. Two-way by default.</summary>
    public object? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }

    /// <summary>Whether the wheel's items repeat without an end (default <c>false</c>: it stops at the first and last item), as a clock's hours.</summary>
    public bool Loop { get => (bool)GetValue(LoopProperty); set => SetValue(LoopProperty, value); }

    /// <summary>
    /// A header above the column's wheel in the popup ("Hour", "Minute"), also what screen readers call the wheel; <c>null</c>
    /// (default): none. SkiaUi extension.
    /// </summary>
    public string? Header { get => (string?)GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }

    /// <summary>
    /// The wheel's items: a template of drawn views (<c>SkUi*</c>) bound to the items of <see cref="ItemsSource"/>; a
    /// <see cref="DataTemplateSelector"/> picks per item. The root of the item in the middle row is in the <c>Selected</c> visual
    /// state (as the picker list's selected row), the roots of items that cannot be chosen in <c>Disabled</c>. <c>null</c>
    /// (default): the item texts in the picker's font. The field still shows the texts. SkiaUi extension.
    /// </summary>
    public DataTemplate? ItemTemplate { get => (DataTemplate?)GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }

    /// <summary>
    /// The text shown for each item when there is no <see cref="ItemDisplayBinding"/>: a function of the item (not bindable; for
    /// columns built in code, as <see cref="Range"/>'s); <c>null</c> (default): <c>ToString()</c>.
    /// </summary>
    public Func<object?, string>? ItemDisplay
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
                return;
            field = value;
            OnItemsChanged(keepSelectedItem: true);
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// A column of the whole numbers from <paramref name="start"/> to <paramref name="end"/> (inclusive) every <paramref name="step"/>
    /// (minutes every 5: <c>Range(0, 55, 5, "00")</c>), shown with the .NET number <paramref name="format"/> in the current culture
    /// (<c>null</c>: as they are). The items are <c>int</c>s, so <see cref="SelectedItem"/> takes and gives a number.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="step"/> is not positive.</exception>
    public static SkUiPickerColumn Range(int start, int end, int step = 1, string? format = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(step);
        var values = new List<int>();
        // Counted in a long: the last step may pass int.MaxValue.
        for (long value = start; value <= end; value += step)
            values.Add((int)value);
        return new SkUiPickerColumn
        {
            ItemDisplay = item => item is int number ? number.ToString(format, System.Globalization.CultureInfo.CurrentCulture) : item?.ToString() ?? string.Empty,
            ItemsSource = values
        };
    }

    /// <summary>
    /// The width of the column's wheel in DIPs; -1 (default): as wide as its widest text. The columns of a wheel popup share the
    /// popup's width in proportion to these widths.
    /// </summary>
    public double Width { get => (double)GetValue(WidthProperty); set => SetValue(WidthProperty, value); }

    /// <summary>Sets <see cref="ItemsSource"/> (same as the property setter).</summary>
    public SkUiPickerColumn SetItemsSource(IList? value) { ItemsSource = value; return this; }

    /// <summary>Sets <see cref="ItemDisplayBinding"/> (same as the property setter).</summary>
    public SkUiPickerColumn SetItemDisplayBinding(BindingBase? value) { ItemDisplayBinding = value; return this; }

    /// <summary>Sets <see cref="ItemEnabledBinding"/> (same as the property setter).</summary>
    public SkUiPickerColumn SetItemEnabledBinding(BindingBase? value) { ItemEnabledBinding = value; return this; }

    /// <summary>Sets <see cref="SelectedIndex"/> (same as the property setter).</summary>
    public SkUiPickerColumn SetSelectedIndex(int value) { ArgumentOutOfRangeException.ThrowIfLessThan(value, -1); SelectedIndex = value; return this; }

    /// <summary>Sets <see cref="SelectedItem"/> (same as the property setter).</summary>
    public SkUiPickerColumn SetSelectedItem(object? value) { SelectedItem = value; return this; }

    /// <summary>Sets <see cref="Header"/> (same as the property setter).</summary>
    public SkUiPickerColumn SetHeader(string? value) { Header = value; return this; }

    /// <summary>
    /// Sets MAUI's <see cref="SemanticProperties.DescriptionProperty"/> on the column: what screen readers call its wheel (else its
    /// <see cref="Header"/>), also without a header shown.
    /// </summary>
    public SkUiPickerColumn SetSemanticDescription(string? value) { SemanticProperties.SetDescription(this, value); return this; }

    /// <summary>Sets MAUI's <see cref="SemanticProperties.HintProperty"/> on the column: what screen readers read after its wheel.</summary>
    public SkUiPickerColumn SetSemanticHint(string? value) { SemanticProperties.SetHint(this, value); return this; }

    /// <summary>Sets <see cref="ItemTemplate"/> (same as the property setter).</summary>
    public SkUiPickerColumn SetItemTemplate(DataTemplate? value) { ItemTemplate = value; return this; }

    /// <summary>Sets <see cref="ItemDisplay"/> (same as the property setter).</summary>
    public SkUiPickerColumn SetItemDisplay(Func<object?, string>? value) { ItemDisplay = value; return this; }

    /// <summary>Sets <see cref="Loop"/> (same as the property setter).</summary>
    public SkUiPickerColumn SetLoop(bool value) { Loop = value; return this; }

    /// <summary>Sets <see cref="Width"/> (same as the property setter; -1: as wide as the widest text).</summary>
    public SkUiPickerColumn SetWidth(double value)
    {
        if (!IsValidWidth(value))
            throw new ArgumentOutOfRangeException(nameof(value), value, "The width must be at least 0, or -1 for the widest text.");
        Width = value;
        return this;
    }

    private static bool IsValidWidth(double width) => double.IsFinite(width) && (width >= 0 || Math.Abs(width + 1) < SkUiPickerValidation.Tolerance);

    /// <summary>The number of items.</summary>
    internal int Count => ItemsSource?.Count ?? 0;

    /// <summary>The display texts of the items (computed once, until the items change).</summary>
    internal string[] Texts => _texts ??= ComputeTexts();

    /// <summary>Which items can be chosen (<c>null</c>: all), computed once until the items or the states change.</summary>
    internal bool[]? ItemStates
    {
        get
        {
            if (!_itemStatesValid)
            {
                _itemStates = ComputeItemStates();
                _itemStatesValid = true;
            }
            return _itemStates;
        }
    }

    private bool[]? ComputeItemStates()
    {
        if (_itemEnabledBinding is null || ItemsSource is not { } source)
            return null;
        var states = new bool[source.Count];
        _enabledProbe ??= CreateEnabledProbe(_itemEnabledBinding);
        for (var index = 0; index < states.Length; index++)
        {
            _enabledProbe.BindingContext = source[index];
            states[index] = _enabledProbe.GetValue(EnabledProperty) is not false;
        }
        // The probe must not keep the last item alive.
        _enabledProbe.BindingContext = null;
        return states;
    }

    private static BindableObject CreateEnabledProbe(BindingBase binding)
    {
        var probe = new DisplayProbe();
        probe.SetBinding(EnabledProperty, binding);
        return probe;
    }

    /// <summary>The item at <paramref name="index"/>, or <c>null</c> outside the items.</summary>
    internal object? ItemAt(int index) => ItemsSource is { } source && index >= 0 && index < source.Count ? source[index] : null;

    private int CoerceIndex(int value)
    {
        var count = Count;
        // Before the items: kept, and clamped once they come.
        return count > 0 ? Math.Min(value, count - 1) : value;
    }

    private void OnItemsSourceChanged(IList? value)
    {
        _sourceListener.Listen(value as INotifyCollectionChanged);
        OnItemsChanged(keepSelectedItem: false);
    }

    private void OnItemsChanged(bool keepSelectedItem)
    {
        _texts = null;
        _itemStatesValid = false;
        var item = keepSelectedItem ? SelectedItem : _pendingItem;
        if (item is not null && ItemsSource is { Count: > 0 } source && source.IndexOf(item) is var moved and >= 0)
        {
            // The chosen item stays chosen where it moved (or arrived).
            _pendingItem = null;
            if (moved != SelectedIndex)
                SetValue(SelectedIndexProperty, moved);
        }
        CoerceValue(SelectedIndexProperty);
        SyncSelectedItem();
        OnPropertyChanged(TextsChanged);
    }

    private void OnSelectedItemChanged(object? value)
    {
        if (_syncing)
            return;
        if (value is not null && ItemsSource is not { Count: > 0 })
        {
            // Before the items: applied when they come.
            _pendingItem = value;
            return;
        }
        _pendingItem = null;
        var index = value is null ? -1 : ItemsSource?.IndexOf(value) ?? -1;
        _syncing = true;
        try
        {
            SetValue(SelectedIndexProperty, index);
        }
        finally
        {
            _syncing = false;
        }
        // An item that is not in the items clears the choice, and the property with it.
        if (index < 0 && value is not null)
            SyncSelectedItem();
    }

    private void OnSelectedIndexChanged()
    {
        SyncSelectedItem();
        OnPropertyChanged(SelectionChanged);
    }

    /// <summary>Makes <see cref="SelectedItem"/> the item at <see cref="SelectedIndex"/>.</summary>
    private void SyncSelectedItem()
    {
        if (_syncing)
            return;
        _syncing = true;
        try
        {
            SetValue(SelectedItemProperty, ItemAt(SelectedIndex));
        }
        finally
        {
            _syncing = false;
        }
    }

    private string[] ComputeTexts()
    {
        if (ItemsSource is not { } source)
            return [];
        var texts = new string[source.Count];
        for (var index = 0; index < texts.Length; index++)
        {
            if (_itemDisplayBinding is null)
            {
                texts[index] = ItemDisplay is { } display ? display(source[index]) ?? string.Empty : source[index]?.ToString() ?? string.Empty;
                continue;
            }
            if (_displayProbe is null)
            {
                _displayProbe = new DisplayProbe();
                _displayProbe.SetBinding(DisplayProperty, _itemDisplayBinding);
            }
            _displayProbe.BindingContext = source[index];
            texts[index] = (string?)_displayProbe.GetValue(DisplayProperty) ?? string.Empty;
        }
        // The probe must not keep the last item alive.
        if (_displayProbe is not null)
            _displayProbe.BindingContext = null;
        return texts;
    }

    /// <summary>A bindable object that evaluates <see cref="ItemDisplayBinding"/> for one item at a time.</summary>
    private sealed class DisplayProbe : BindableObject;
}
