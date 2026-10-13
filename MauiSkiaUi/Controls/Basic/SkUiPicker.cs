using System.Collections;
using System.Collections.Specialized;
using System.Windows.Input;

namespace MauiSkiaUi;

/// <summary>
/// A drawn picker: MAUI's <c>Picker</c> API (XAML ports by prefix) with a field drawn by the look and its list on the app's
/// popup system. The field shows the selected item's text, or <see cref="SkUiPickerBase.Title"/> in <see cref="SkUiPickerBase.TitleColor"/> while nothing is
/// selected; a tap, Space, Enter or Alt+Down opens the list (<see cref="SkUiPickerBase.IsOpen"/>), arrow keys change the selection without
/// opening (as a desktop combo box). The list is a drawn, virtualized <see cref="SkUiCollectionView"/> in its own surface,
/// handed to the app's <see cref="ISkUiPopupPresenter"/> (<see cref="SkUiPickerBase.PopupPresenter"/>, else <see cref="SkUiPopups.Presenter"/>,
/// else MAUI's action sheet): the selection highlighted and scrolled into view, a tap chooses, keyboard and screen readers.
/// </summary>
/// <remarks>
/// <para>
/// <b>Beyond MAUI:</b> <see cref="SkUiPickerBase.PopupPlacement"/> (anchored like a combo box, in the window like a dialog or a sheet, at
/// <see cref="SkUiPickerBase.PopupBounds"/>, or the look's choice per platform), <see cref="ItemTemplate"/> (drawn rows),
/// <see cref="SkUiPickerBase.MaxPopupHeight"/> and <see cref="SkUiPickerBase.PopupPresenter"/>.
/// </para>
/// <para>
/// <b>Several choices:</b> <see cref="SelectionMode"/> <see cref="SkUiSelectionMode.Multiple"/> turns the list into check boxes
/// that stay open (<see cref="SelectedItems"/>, <see cref="SelectedIndices"/>, <see cref="SelectionChanged"/>); the field shows the
/// chosen texts joined by <see cref="SelectedTextSeparator"/>.
/// </para>
/// <para>
/// <b>Selection</b> follows MAUI's rules: <see cref="SelectedIndex"/> and <see cref="SelectedItem"/> stay in step, an index set
/// before the items applies when they come, and on collection changes the selected item is kept (or the index clamped).
/// </para>
/// <para>
/// <b>The popup closes</b> when an item is chosen, the user dismisses it (the presenter's tap outside, Back, Escape),
/// <see cref="SkUiPickerBase.IsOpen"/> is set to <c>false</c>, or the picker is disabled, hidden or removed; an anchored popup also closes when
/// a scroller around the picker scrolls or the window resizes. <see cref="SkUiPickerBase.Opened"/> and <see cref="SkUiPickerBase.Closed"/> follow
/// <see cref="SkUiPickerBase.IsOpen"/>, as MAUI's; a choice raises <see cref="SelectedIndexChanged"/> before <see cref="SkUiPickerBase.Closed"/>.
/// </para>
/// </remarks>
public partial class SkUiPicker : SkUiPickerBase
{
    private static readonly BindableProperty DisplayProperty = BindableProperty.Create("Display", typeof(string), typeof(SkUiPicker), null);
    private static readonly BindableProperty EnabledProperty = BindableProperty.Create("Enabled", typeof(bool), typeof(SkUiPicker), true);

    private readonly SkUiPickerItems _items = new();
    private SkUiWeakListener<SkUiPicker>? _itemsSourceListener;
    private IList? _itemsSource;
    private BindingBase? _itemDisplayBinding;
    private BindableObject? _displayProbe;
    private BindingBase? _itemEnabledBinding;
    private BindableObject? _enabledProbe;
    private bool _hasPendingSelectedIndex;
    private int _pendingSelectedIndex = -1;
    private bool _settingSelectedIndexInternally;
    private bool _suppressSelectedIndexChanged;

    /// <summary>Creates a picker without items.</summary>
    public SkUiPicker()
    {
        Model.Items = _items;
        _items.CollectionChanged += OnItemsCollectionChanged;
    }

    #region Properties

    /// <summary>Bindable property for <see cref="ItemsSource"/>.</summary>
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(nameof(ItemsSource), typeof(IList), typeof(SkUiPicker), null,
        propertyChanged: (view, old, value) => ((SkUiPicker)view).OnItemsSourceChanged((IList?)value));

    /// <summary>Bindable property for <see cref="SelectedIndex"/> (two-way by default).</summary>
    public static readonly BindableProperty SelectedIndexProperty = BindableProperty.Create(nameof(SelectedIndex), typeof(int), typeof(SkUiPicker), -1, BindingMode.TwoWay,
        coerceValue: (view, value) => ((SkUiPicker)view).CoerceSelectedIndex((int)value),
        propertyChanged: (view, _, value) => ((SkUiPicker)view).OnSelectedIndexChanged());

    /// <summary>Bindable property for <see cref="SelectedItem"/> (two-way by default).</summary>
    public static readonly BindableProperty SelectedItemProperty = BindableProperty.Create(nameof(SelectedItem), typeof(object), typeof(SkUiPicker), null, BindingMode.TwoWay,
        propertyChanged: (view, _, value) => ((SkUiPicker)view).UpdateSelectedIndex(value));

    /// <summary>Bindable property for <see cref="SelectionBackground"/>.</summary>
    public static readonly BindableProperty SelectionBackgroundProperty = BindableProperty.Create(nameof(SelectionBackground), typeof(Brush), typeof(SkUiPicker), null,
        propertyChanged: (view, _, value) => ((SkUiPicker)view).Model.SelectionBackground = (Brush?)value);

    /// <summary>Bindable property for <see cref="ItemTemplate"/>.</summary>
    public static readonly BindableProperty ItemTemplateProperty = BindableProperty.Create(nameof(ItemTemplate), typeof(DataTemplate), typeof(SkUiPicker), null,
        propertyChanged: (view, _, value) => ((SkUiPicker)view).Model.ItemTemplate = (DataTemplate?)value);

    /// <summary>
    /// The item texts. Add to it to fill the picker (as MAUI's); while <see cref="ItemsSource"/> is set it holds the source's
    /// display texts (<see cref="ItemDisplayBinding"/>) and cannot be changed.
    /// </summary>
    public IList<string> Items => _items;

    /// <summary>
    /// The items to choose from; each shows as its <see cref="ItemDisplayBinding"/> (else <c>ToString()</c>). An
    /// <see cref="INotifyCollectionChanged"/> list is followed (weakly: the list does not keep the picker alive).
    /// </summary>
    public IList? ItemsSource { get => (IList?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>The binding that picks the text shown for each item of <see cref="ItemsSource"/> (MAUI's; <c>null</c>: <c>ToString()</c>).</summary>
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
            ResetItems();
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// The binding that says whether an item can be chosen (a <c>bool</c> of the item of <see cref="ItemsSource"/>, or of the text of
    /// <see cref="Items"/>: <c>{Binding IsAvailable}</c>); <c>null</c> (default): every item. Disabled items show (dimmed; a template's
    /// root in the <c>Disabled</c> visual state) but taps, the keyboard and type-ahead pass them by, and <c>SelectAll</c> skips them.
    /// The app may still select one from code. Read when the items change and on <see cref="RefreshItemStates"/> (an open list
    /// follows). SkiaUi extension.
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

    /// <summary>Reads which items can be chosen again (<see cref="ItemEnabledBinding"/>), after the app's data changed; an open list follows.</summary>
    public void RefreshItemStates()
    {
        if (_itemEnabledBinding is null)
        {
            Model.ItemEnabled = null;
            return;
        }
        var states = new bool[_items.Count];
        if (_enabledProbe is null)
        {
            _enabledProbe = new DisplayProbe();
            _enabledProbe.SetBinding(EnabledProperty, _itemEnabledBinding);
        }
        for (var index = 0; index < states.Length; index++)
        {
            _enabledProbe.BindingContext = _itemsSource is { } source ? (index < source.Count ? source[index] : null) : _items[index];
            states[index] = _enabledProbe.GetValue(EnabledProperty) is not false;
        }
        // The probe must not keep the last item alive.
        _enabledProbe.BindingContext = null;
        Model.ItemEnabled = states;
    }

    /// <summary>Sets <see cref="ItemEnabledBinding"/> (same as the property setter).</summary>
    public SkUiPicker SetItemEnabledBinding(BindingBase? value) { ItemEnabledBinding = value; return this; }

    /// <summary>The index of the selected item; -1 (default): none. Two-way by default.</summary>
    public int SelectedIndex { get => (int)GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }

    /// <summary>The selected item (of <see cref="ItemsSource"/>, else of <see cref="Items"/>), or <c>null</c>. Two-way by default.</summary>
    public object? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }

    /// <summary>
    /// The rows of the list: a template of drawn views (<c>SkUi*</c>) bound to the items of <see cref="ItemsSource"/> (or to the
    /// texts of <see cref="Items"/>); a <see cref="DataTemplateSelector"/> picks per item. <c>null</c> (default): the item texts
    /// in the picker's font. The field still shows the display text. Presenters that show their own UI ignore it. SkiaUi extension.
    /// </summary>
    public DataTemplate? ItemTemplate { get => (DataTemplate?)GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }

    /// <summary>
    /// What is behind the selected rows of the list (the highlighted row; with several choices the chosen ones), as a collection
    /// view's <c>SelectionBackground</c>: a brush (in XAML a color); <c>null</c> (default): the look's
    /// <see cref="SkUiLook.PopupHighlightColor"/>; <c>Transparent</c>: none, to style selected rows only through the
    /// <c>Selected</c> visual state of the <see cref="ItemTemplate"/>'s root. Applies from the next opening. SkiaUi extension.
    /// </summary>
    public Brush? SelectionBackground { get => (Brush?)GetValue(SelectionBackgroundProperty); set => SetValue(SelectionBackgroundProperty, value); }

    /// <summary>Raised when <see cref="SelectedIndex"/> changes (by the user or the app; not when an index set before the items applies).</summary>
    public event EventHandler? SelectedIndexChanged;

    /// <summary>Sets <see cref="ItemsSource"/> (same as the property setter).</summary>
    public SkUiPicker SetItemsSource(IList? value) { ItemsSource = value; return this; }

    /// <summary>Sets <see cref="ItemDisplayBinding"/> (same as the property setter).</summary>
    public SkUiPicker SetItemDisplayBinding(BindingBase? value) { ItemDisplayBinding = value; return this; }

    /// <summary>Sets <see cref="SelectedIndex"/> (same as the property setter).</summary>
    public SkUiPicker SetSelectedIndex(int value) { ArgumentOutOfRangeException.ThrowIfLessThan(value, -1); SelectedIndex = value; return this; }

    /// <summary>Sets <see cref="SelectedItem"/> (same as the property setter).</summary>
    public SkUiPicker SetSelectedItem(object? value) { SelectedItem = value; return this; }

    /// <summary>Sets <see cref="ItemTemplate"/> (same as the property setter).</summary>
    public SkUiPicker SetItemTemplate(DataTemplate? value) { ItemTemplate = value; return this; }

    /// <summary>Sets <see cref="SelectionBackground"/> (same as the property setter).</summary>
    public SkUiPicker SetSelectionBackground(Brush? value) { SelectionBackground = value; return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.Title"/> (same as the property setter).</summary>
    public new SkUiPicker SetTitle(string? value) { base.SetTitle(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.TitleColor"/> (same as the property setter).</summary>
    public new SkUiPicker SetTitleColor(Color? value) { base.SetTitleColor(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.TextColor"/> (same as the property setter).</summary>
    public new SkUiPicker SetTextColor(Color? value) { base.SetTextColor(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.FontFamily"/> (same as the property setter).</summary>
    public new SkUiPicker SetFontFamily(string? value) { base.SetFontFamily(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.FontSize"/> (same as the property setter).</summary>
    public new SkUiPicker SetFontSize(double value) { base.SetFontSize(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.FontAttributes"/> (same as the property setter).</summary>
    public new SkUiPicker SetFontAttributes(FontAttributes value) { base.SetFontAttributes(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.FontAutoScalingEnabled"/> (same as the property setter).</summary>
    public new SkUiPicker SetFontAutoScalingEnabled(bool value) { base.SetFontAutoScalingEnabled(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.CharacterSpacing"/> (same as the property setter).</summary>
    public new SkUiPicker SetCharacterSpacing(double value) { base.SetCharacterSpacing(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.HorizontalTextAlignment"/> (same as the property setter).</summary>
    public new SkUiPicker SetHorizontalTextAlignment(TextAlignment value) { base.SetHorizontalTextAlignment(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.VerticalTextAlignment"/> (same as the property setter).</summary>
    public new SkUiPicker SetVerticalTextAlignment(TextAlignment value) { base.SetVerticalTextAlignment(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.IsOpen"/> (same as the property setter).</summary>
    public new SkUiPicker SetIsOpen(bool value) { base.SetIsOpen(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.MaxPopupHeight"/> (same as the property setter; -1: no limit).</summary>
    public new SkUiPicker SetMaxPopupHeight(double value) { base.SetMaxPopupHeight(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.PopupPlacement"/> (same as the property setter).</summary>
    public new SkUiPicker SetPopupPlacement(SkUiPopupPlacement value) { base.SetPopupPlacement(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.PopupBounds"/> (same as the property setter).</summary>
    public new SkUiPicker SetPopupBounds(Rect value) { base.SetPopupBounds(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.PopupPresenter"/> (same as the property setter).</summary>
    public new SkUiPicker SetPopupPresenter(ISkUiPopupPresenter? value) { base.SetPopupPresenter(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.UpdateMode"/> (same as the property setter).</summary>
    public new SkUiPicker SetUpdateMode(SkUiPickerUpdateMode value) { base.SetUpdateMode(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.AcceptText"/> (same as the property setter).</summary>
    public new SkUiPicker SetAcceptText(string? value) { base.SetAcceptText(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.CancelText"/> (same as the property setter).</summary>
    public new SkUiPicker SetCancelText(string? value) { base.SetCancelText(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.IsClearButtonVisible"/> (same as the property setter).</summary>
    public new SkUiPicker SetIsClearButtonVisible(bool value) { base.SetIsClearButtonVisible(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.IsPopupClearButtonVisible"/> (same as the property setter).</summary>
    public new SkUiPicker SetIsPopupClearButtonVisible(bool value) { base.SetIsPopupClearButtonVisible(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.ClearText"/> (same as the property setter).</summary>
    public new SkUiPicker SetClearText(string? value) { base.SetClearText(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.Placeholder"/> (same as the property setter).</summary>
    public new SkUiPicker SetPlaceholder(string? value) { base.SetPlaceholder(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.PlaceholderColor"/> (same as the property setter).</summary>
    public new SkUiPicker SetPlaceholderColor(Color? value) { base.SetPlaceholderColor(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.PopupWidth"/> (same as the property setter).</summary>
    public new SkUiPicker SetPopupWidth(double value) { base.SetPopupWidth(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.SelectionChangedCommand"/> (same as the property setter).</summary>
    public new SkUiPicker SetSelectionChangedCommand(ICommand? value) { base.SetSelectionChangedCommand(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.SelectionChangedCommandParameter"/> (same as the property setter).</summary>
    public new SkUiPicker SetSelectionChangedCommandParameter(object? value) { base.SetSelectionChangedCommandParameter(value); return this; }

    #endregion

    #region Items and selection (MAUI's rules)

    private void OnItemsSourceChanged(IList? value)
    {
        _itemsSource = value;
        (_itemsSourceListener ??= new(this, static (picker, change) =>
        {
            if (change.Kind == SkUiChangeKind.Collection)
                picker.ResyncItemsAndReconcileSelection();
        })).Listen(value as INotifyCollectionChanged);
        if (value is not null)
        {
            _items.IsReadOnly = true;
            ResetItems();
        }
        else
        {
            // Unlocked, then cleared: the selection follows as for the app clearing Items.
            _items.IsReadOnly = false;
            _items.Clear();
        }
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        Model.Items = _items;
        if (_itemEnabledBinding is not null)
            RefreshItemStates();
        // Changes made by the picker from its source reconcile the selection themselves.
        if (_items.IsReadOnly)
            return;
        if (_multiple)
        {
            ReconcileMultiple(keepUnknown: args.Action == NotifyCollectionChangedAction.Add && _items.Count == args.NewItems?.Count);
            return;
        }
        if (!TryApplyPendingSelectedIndex(forceClamp: false))
            ClampSelectedIndex(GetSelectedIndex());
    }

    private string GetDisplayMember(object? item)
    {
        if (_itemDisplayBinding is null)
            return item?.ToString() ?? string.Empty;
        if (_displayProbe is null)
        {
            _displayProbe = new DisplayProbe();
            _displayProbe.SetBinding(DisplayProperty, _itemDisplayBinding);
        }
        _displayProbe.BindingContext = item;
        return (string?)_displayProbe.GetValue(DisplayProperty) ?? string.Empty;
    }

    private string[] DisplayTexts(IList source)
    {
        var texts = new string[source.Count];
        for (var index = 0; index < texts.Length; index++)
            texts[index] = GetDisplayMember(source[index]);
        // The probe must not keep the last item alive.
        if (_displayProbe is not null)
            _displayProbe.BindingContext = null;
        return texts;
    }

    private void ResetItems()
    {
        if (_itemsSource is not { } source)
            return;
        var wasEmpty = _items.Count == 0;
        _items.Reset(DisplayTexts(source));
        if (_multiple)
        {
            ReconcileMultiple(keepUnknown: wasEmpty);
            return;
        }
        if (!TryApplyPendingSelectedIndex(forceClamp: true))
            ClampSelectedIndex(SelectedIndex);
    }

    private void ResyncItemsAndReconcileSelection()
    {
        if (_itemsSource is not { } source)
            return;
        var wasEmpty = _items.Count == 0;
        _items.Reset(DisplayTexts(source));
        if (_multiple)
        {
            ReconcileMultiple(keepUnknown: wasEmpty);
            return;
        }
        if (TryApplyPendingSelectedIndex(forceClamp: false))
            return;
        ClampSelectedIndex(SelectedItem is { } selected ? source.IndexOf(selected) : SelectedIndex);
    }

    private object CoerceSelectedIndex(int value)
    {
        // Several choices: SelectedItems holds them; the single selection stays empty.
        if (_multiple)
            return -1;
        if (!_settingSelectedIndexInternally)
        {
            if (_items.Count == 0)
            {
                // An index before the items: applied when they come (as MAUI's).
                if (value >= 0)
                    SetPendingSelectedIndex(value);
                else
                    ClearPendingSelectedIndex();
                return -1;
            }
            ClearPendingSelectedIndex();
        }
        return Math.Clamp(value, -1, _items.Count - 1);
    }

    private void OnSelectedIndexChanged()
    {
        Model.SelectedIndex = SelectedIndex;
        UpdateSelectedItem(SelectedIndex);
        Model.SelectionChanged();
        if (_suppressSelectedIndexChanged)
            return;
        Model.RunSelectionChangedCommand();
        SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
    }

    private int GetSelectedIndex()
    {
        if (SelectedItem is not { } selected)
            return SelectedIndex;
        var index = _itemsSource?.IndexOf(selected) ?? (selected is string text ? _items.IndexOf(text) : -1);
        return index >= 0 ? index : SelectedIndex;
    }

    private void ClampSelectedIndex(int selectedIndex)
    {
        var newIndex = Math.Clamp(selectedIndex, -1, _items.Count - 1);
        SetSelectedIndexInternally(newIndex);
        // The index may not have changed, but the item at it may have.
        if (newIndex == selectedIndex)
            UpdateSelectedItem(newIndex);
    }

    private void UpdateSelectedIndex(object? selectedItem)
    {
        ClearPendingSelectedIndex();
        if (_itemsSource is { } source)
        {
            SetSelectedIndexInternally(source.IndexOf(selectedItem));
            return;
        }
        SetSelectedIndexInternally(selectedItem is string text ? _items.IndexOf(text) : -1);
    }

    private void UpdateSelectedItem(int index)
    {
        if (index == -1)
        {
            SetValue(SelectedItemProperty, null);
            return;
        }
        if (_itemsSource is { } source)
        {
            SetValue(SelectedItemProperty, index < source.Count ? source[index] : null);
            return;
        }
        SetValue(SelectedItemProperty, _items[index]);
    }

    private void SetPendingSelectedIndex(int value)
    {
        _hasPendingSelectedIndex = true;
        _pendingSelectedIndex = value;
    }

    private void ClearPendingSelectedIndex()
    {
        _hasPendingSelectedIndex = false;
        _pendingSelectedIndex = -1;
    }

    private bool TryApplyPendingSelectedIndex(bool forceClamp)
    {
        if (!_hasPendingSelectedIndex || _items.Count == 0)
            return false;
        var pending = _pendingSelectedIndex;
        if (!forceClamp && pending >= _items.Count)
            return false;
        ClearPendingSelectedIndex();
        var selectedIndex = Math.Clamp(pending, -1, _items.Count - 1);
        var suppress = _suppressSelectedIndexChanged;
        _suppressSelectedIndexChanged = true;
        try
        {
            var old = SelectedIndex;
            SetSelectedIndexInternally(selectedIndex);
            if (old == selectedIndex)
                UpdateSelectedItem(selectedIndex);
        }
        finally
        {
            _suppressSelectedIndexChanged = suppress;
        }
        return true;
    }

    private void SetSelectedIndexInternally(int value)
    {
        var setting = _settingSelectedIndexInternally;
        _settingSelectedIndexInternally = true;
        try
        {
            SetValue(SelectedIndexProperty, value);
        }
        finally
        {
            _settingSelectedIndexInternally = setting;
        }
    }

    /// <summary>A bindable object that evaluates <see cref="ItemDisplayBinding"/> for one item at a time.</summary>
    private sealed class DisplayProbe : BindableObject;

    #endregion

    #region Base

    /// <inheritdoc />
    private protected override IList? PopupItemsSource => _itemsSource;

    /// <inheritdoc />
    private protected override void ChooseItem(int index)
    {
        if (!_multiple && index >= 0 && index < _items.Count && index != SelectedIndex)
            SelectedIndex = index;
    }

    #endregion
}
