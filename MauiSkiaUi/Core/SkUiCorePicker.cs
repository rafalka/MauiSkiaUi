using System.Collections;
using System.Collections.Specialized;
using System.Windows.Input;

namespace MauiSkiaUi.Core;

/// <summary>
/// Drawn picker (Core analogue of <c>SkUiPicker</c>): the same field drawn by the look and the same list on the app's popup
/// system (<see cref="ISkUiPopupPresenter"/>), anchored to the node's <see cref="SkUiCoreHost"/> (the MAUI view the request
/// carries). Fill it with <see cref="Items"/> or <see cref="ItemsSource"/> (texts from <see cref="ItemDisplay"/>, else
/// <c>ToString()</c>); <see cref="SelectedIndex"/> / <see cref="SelectedItem"/> follow MAUI's picker rules: an index set
/// before the items applies when they come, and on collection changes the selected item is kept (or the index clamped).
/// </summary>
/// <remarks>
/// While disabled (<see cref="SkUiCoreNode.IsEnabled"/> <c>false</c>, also through a Core ancestor or the host) the picker draws
/// its disabled look and does not open; an open popup closes.
/// </remarks>
public partial class SkUiCorePicker : SkUiCorePickerBase
{
    private readonly SkUiPickerItems _items = new();
    private SkUiWeakListener<SkUiCorePicker>? _itemsSourceListener;
    private IList? _itemsSource;
    private Func<object?, string>? _itemDisplay;
    private Func<object?, bool>? _itemEnabled;
    private Func<object?, string>? _selectedItemDisplay;
    private int _selectedIndex = -1;
    private object? _selectedItem;
    private int _pendingSelectedIndex = -1;
    private bool _syncingItems;

    /// <summary>Creates a picker without items.</summary>
    public SkUiCorePicker()
    {
        Model.Items = _items;
        _items.CollectionChanged += OnItemsCollectionChanged;
    }

    #region Properties

    /// <summary>The item texts: add to it to fill the picker; while <see cref="ItemsSource"/> is set, the source's texts (read-only).</summary>
    public IList<string> Items => _items;

    /// <summary>The items to choose from (followed while it is an <see cref="INotifyCollectionChanged"/> list, weakly).</summary>
    public IList? ItemsSource { get => _itemsSource; set => SetItemsSource(value); }

    /// <summary>The text shown for each item of <see cref="ItemsSource"/>; <c>null</c> (default): <c>ToString()</c>.</summary>
    public Func<object?, string>? ItemDisplay { get => _itemDisplay; set => SetItemDisplay(value); }

    /// <summary>
    /// Whether an item (of <see cref="ItemsSource"/>, else a text of <see cref="Items"/>) can be chosen; <c>null</c> (default): every
    /// item. See <c>SkUiPicker.ItemEnabledBinding</c>; read when the items change and on <see cref="RefreshItemStates"/>.
    /// </summary>
    public Func<object?, bool>? ItemEnabled { get => _itemEnabled; set => SetItemEnabled(value); }

    /// <summary>
    /// The text the field shows for a chosen item when it differs from its text in the list (a currency's code for its full name);
    /// <c>null</c> (default): the item's text. See <c>SkUiPicker.SelectedItemDisplayBinding</c>.
    /// </summary>
    public Func<object?, string>? SelectedItemDisplay { get => _selectedItemDisplay; set => SetSelectedItemDisplay(value); }

    /// <summary>Sets <see cref="SelectedItemDisplay"/>.</summary>
    public SkUiCorePicker SetSelectedItemDisplay(Func<object?, string>? value)
    {
        if (!SetProperty(ref _selectedItemDisplay, value, nameof(SelectedItemDisplay)))
            return this;
        UpdateFieldText();
        UpdateMultipleState();
        return this;
    }

    /// <summary>The field's text for the item at <paramref name="index"/>: <see cref="SelectedItemDisplay"/>'s, else the item's text.</summary>
    private string FieldTextOf(int index) =>
        _selectedItemDisplay is { } display && index >= 0 && index < _items.Count ? display(ItemAt(index)) ?? string.Empty
        : index >= 0 && index < _items.Count ? _items[index] : string.Empty;

    /// <summary>The field's text for the selected item (one choice).</summary>
    private void UpdateFieldText()
    {
        Model.SelectedFieldText = _selectedItemDisplay is null || _multiple || _selectedIndex < 0 ? null : FieldTextOf(_selectedIndex);
        Model.TextChanged();
    }

    /// <summary>Sets <see cref="ItemEnabled"/>.</summary>
    public SkUiCorePicker SetItemEnabled(Func<object?, bool>? value)
    {
        if (SetProperty(ref _itemEnabled, value, nameof(ItemEnabled)))
            RefreshItemStates();
        return this;
    }

    /// <summary>Reads which items can be chosen again (<see cref="ItemEnabled"/>), after the app's data changed; an open list follows.</summary>
    public void RefreshItemStates()
    {
        if (ItemEnabled is not { } enabled)
        {
            Model.ItemEnabled = null;
            UpdateMultipleState();
            return;
        }
        var states = new bool[_items.Count];
        for (var index = 0; index < states.Length; index++)
            states[index] = enabled(ItemAt(index));
        Model.ItemEnabled = states;
        // Tokens: an item that can no longer be chosen keeps its token, without its ×.
        UpdateMultipleState();
    }

    /// <summary>The selected item's index; -1 (default): none.</summary>
    public int SelectedIndex { get => _selectedIndex; set => SetSelectedIndex(value); }

    /// <summary>The selected item (of <see cref="ItemsSource"/>, else of <see cref="Items"/>), or <c>null</c>; see <see cref="SetSelectedItem"/>.</summary>
    public object? SelectedItem { get => _selectedItem; set => SetSelectedItem(value); }

    /// <summary>The list's rows: a template of drawn <c>SkUi*</c> views bound to the items (see <c>SkUiPicker.ItemTemplate</c>).</summary>
    public DataTemplate? ItemTemplate { get => Model.ItemTemplate; set => SetItemTemplate(value); }

    /// <summary>What is behind the selected rows of the list; <c>null</c> (default): the look's highlight (see <c>SkUiPicker.SelectionBackground</c>).</summary>
    public Brush? SelectionBackground { get => Model.SelectionBackground; set => SetSelectionBackground(value); }

    /// <summary>Raised when <see cref="SelectedIndex"/> changes (by the user or the app; not when an index set before the items applies).</summary>
    public event EventHandler? SelectedIndexChanged;

    /// <summary>Sets <see cref="ItemsSource"/>.</summary>
    public SkUiCorePicker SetItemsSource(IList? value)
    {
        if (!SetProperty(ref _itemsSource, value, nameof(ItemsSource)))
            return this;
        (_itemsSourceListener ??= new(this, static (picker, change) =>
        {
            if (change.Kind == SkUiChangeKind.Collection)
                picker.SyncItems(forceClamp: false);
        })).Listen(value as INotifyCollectionChanged);
        _items.IsReadOnly = value is not null;
        if (value is null)
            _items.Clear();
        else
            SyncItems(forceClamp: true);
        return this;
    }

    /// <summary>Sets <see cref="ItemDisplay"/>.</summary>
    public SkUiCorePicker SetItemDisplay(Func<object?, string>? value)
    {
        if (SetProperty(ref _itemDisplay, value, nameof(ItemDisplay)))
            SyncItems(forceClamp: false);
        return this;
    }

    /// <summary>Sets <see cref="SelectedIndex"/> (-1: none; clamped to the items; before the items, applied when they come).</summary>
    public SkUiCorePicker SetSelectedIndex(int value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, -1);
        if (_multiple)
            return this; // the choice is SelectedItems
        if (_items.Count == 0)
        {
            _pendingSelectedIndex = value;
            return this;
        }
        _pendingSelectedIndex = -1;
        ApplySelectedIndex(Math.Min(value, _items.Count - 1), raise: true);
        return this;
    }

    /// <summary>Selects <paramref name="item"/> (of <see cref="ItemsSource"/>, else a text of <see cref="Items"/>); one not in the list clears the selection.</summary>
    public SkUiCorePicker SetSelectedItem(object? item)
    {
        if (_multiple)
            return this; // the choice is SelectedItems
        _pendingSelectedIndex = -1;
        ApplySelectedIndex(IndexOfItem(item), raise: true);
        return this;
    }

    /// <summary>Sets <see cref="SelectionBackground"/>.</summary>
    public SkUiCorePicker SetSelectionBackground(Brush? value)
    {
        Update(ref Model.SelectionBackground, value, nameof(SelectionBackground), paint: false);
        return this;
    }

    /// <summary>Sets <see cref="ItemTemplate"/>.</summary>
    public SkUiCorePicker SetItemTemplate(DataTemplate? value)
    {
        Update(ref Model.ItemTemplate, value, nameof(ItemTemplate), paint: false);
        return this;
    }

    /// <summary>Sets <see cref="SkUiCorePickerBase.Title"/>.</summary>
    public new SkUiCorePicker SetTitle(string? value) { base.SetTitle(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.TitleColor"/>.</summary>
    public new SkUiCorePicker SetTitleColor(Color? value) { base.SetTitleColor(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.TextColor"/>.</summary>
    public new SkUiCorePicker SetTextColor(Color? value) { base.SetTextColor(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.FontFamily"/>.</summary>
    public new SkUiCorePicker SetFontFamily(string? value) { base.SetFontFamily(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.FontSize"/>.</summary>
    public new SkUiCorePicker SetFontSize(double value) { base.SetFontSize(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.FontAttributes"/>.</summary>
    public new SkUiCorePicker SetFontAttributes(FontAttributes value) { base.SetFontAttributes(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.FontAutoScalingEnabled"/>.</summary>
    public new SkUiCorePicker SetFontAutoScalingEnabled(bool value) { base.SetFontAutoScalingEnabled(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.CharacterSpacing"/>.</summary>
    public new SkUiCorePicker SetCharacterSpacing(double value) { base.SetCharacterSpacing(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.HorizontalTextAlignment"/>.</summary>
    public new SkUiCorePicker SetHorizontalTextAlignment(TextAlignment value) { base.SetHorizontalTextAlignment(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.VerticalTextAlignment"/>.</summary>
    public new SkUiCorePicker SetVerticalTextAlignment(TextAlignment value) { base.SetVerticalTextAlignment(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.IsOpen"/> (opens or closes the list).</summary>
    public new SkUiCorePicker SetIsOpen(bool value) { base.SetIsOpen(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.MaxPopupHeight"/> (-1: no limit).</summary>
    public new SkUiCorePicker SetMaxPopupHeight(double value) { base.SetMaxPopupHeight(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.PopupPlacement"/>.</summary>
    public new SkUiCorePicker SetPopupPlacement(SkUiPopupPlacement value) { base.SetPopupPlacement(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.PopupBounds"/>.</summary>
    public new SkUiCorePicker SetPopupBounds(Rect value) { base.SetPopupBounds(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.PopupPresenter"/>.</summary>
    public new SkUiCorePicker SetPopupPresenter(ISkUiPopupPresenter? value) { base.SetPopupPresenter(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.UpdateMode"/> (same as the property setter).</summary>
    public new SkUiCorePicker SetUpdateMode(SkUiPickerUpdateMode value) { base.SetUpdateMode(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.AcceptText"/> (same as the property setter).</summary>
    public new SkUiCorePicker SetAcceptText(string? value) { base.SetAcceptText(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.CancelText"/> (same as the property setter).</summary>
    public new SkUiCorePicker SetCancelText(string? value) { base.SetCancelText(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.MinPopupWidth"/> (same as the property setter).</summary>
    public new SkUiCorePicker SetMinPopupWidth(double value) { base.SetMinPopupWidth(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.MaxPopupWidth"/> (same as the property setter).</summary>
    public new SkUiCorePicker SetMaxPopupWidth(double value) { base.SetMaxPopupWidth(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.PopupHeaderTemplate"/> (same as the property setter).</summary>
    public new SkUiCorePicker SetPopupHeaderTemplate(DataTemplate? value) { base.SetPopupHeaderTemplate(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.PopupFooterTemplate"/> (same as the property setter).</summary>
    public new SkUiCorePicker SetPopupFooterTemplate(DataTemplate? value) { base.SetPopupFooterTemplate(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.IsClearButtonVisible"/> (same as the property setter).</summary>
    public new SkUiCorePicker SetIsClearButtonVisible(bool value) { base.SetIsClearButtonVisible(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.IsPopupClearButtonVisible"/> (same as the property setter).</summary>
    public new SkUiCorePicker SetIsPopupClearButtonVisible(bool value) { base.SetIsPopupClearButtonVisible(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.ClearText"/> (same as the property setter).</summary>
    public new SkUiCorePicker SetClearText(string? value) { base.SetClearText(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.Placeholder"/> (same as the property setter).</summary>
    public new SkUiCorePicker SetPlaceholder(string? value) { base.SetPlaceholder(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.PlaceholderColor"/> (same as the property setter).</summary>
    public new SkUiCorePicker SetPlaceholderColor(Color? value) { base.SetPlaceholderColor(value); return this; }

    /// <inheritdoc cref="SkUiCorePickerBase.SetFieldStyle" />
    public new SkUiCorePicker SetFieldStyle(SkUiPickerFieldStyle value) { base.SetFieldStyle(value); return this; }

    /// <inheritdoc cref="SkUiCorePickerBase.SetFillColor" />
    public new SkUiCorePicker SetFillColor(Color? value) { base.SetFillColor(value); return this; }

    /// <inheritdoc cref="SkUiCorePickerBase.SetBorderColor" />
    public new SkUiCorePicker SetBorderColor(Color? value) { base.SetBorderColor(value); return this; }

    /// <inheritdoc cref="SkUiCorePickerBase.SetBorderWidth" />
    public new SkUiCorePicker SetBorderWidth(double? value) { base.SetBorderWidth(value); return this; }

    /// <inheritdoc cref="SkUiCorePickerBase.SetCornerRadii" />
    public new SkUiCorePicker SetCornerRadii(CornerRadius? value) { base.SetCornerRadii(value); return this; }

    /// <inheritdoc cref="SkUiCorePickerBase.SetCornerRadius" />
    public new SkUiCorePicker SetCornerRadius(double? value) { base.SetCornerRadius(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.PopupWidth"/> (same as the property setter).</summary>
    public new SkUiCorePicker SetPopupWidth(double value) { base.SetPopupWidth(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.SelectionChangedCommand"/> (same as the property setter).</summary>
    public new SkUiCorePicker SetSelectionChangedCommand(ICommand? value) { base.SetSelectionChangedCommand(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.SelectionChangedCommandParameter"/> (same as the property setter).</summary>
    public new SkUiCorePicker SetSelectionChangedCommandParameter(object? value) { base.SetSelectionChangedCommandParameter(value); return this; }

    #endregion

    #region Items and selection

    /// <summary>
    /// The items again from <see cref="ItemsSource"/>; <paramref name="forceClamp"/> (a new source) applies an index waiting for
    /// items even when it is past the end (clamped), as MAUI's picker.
    /// </summary>
    private void SyncItems(bool forceClamp)
    {
        if (_itemsSource is not { } source)
            return;
        var selected = _selectedItem;
        var wasEmpty = _items.Count == 0;
        var texts = new string[source.Count];
        for (var index = 0; index < texts.Length; index++)
            texts[index] = _itemDisplay is { } display ? display(source[index]) ?? string.Empty : source[index]?.ToString() ?? string.Empty;
        _syncingItems = true;
        try
        {
            _items.Reset(texts);
        }
        finally
        {
            _syncingItems = false;
        }
        if (_multiple)
        {
            ReconcileMultiple(keepUnknown: wasEmpty);
            return;
        }
        Reconcile(selected is not null ? source.IndexOf(selected) : _selectedIndex, forceClamp);
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        Model.Items = _items;
        if (ItemEnabled is not null)
            RefreshItemStates();
        if (_syncingItems || _items.IsReadOnly)
            return;
        if (_multiple)
        {
            ReconcileMultiple(keepUnknown: args.Action == NotifyCollectionChangedAction.Add && _items.Count == args.NewItems?.Count);
            return;
        }
        // The app changed the texts: the selected text keeps its selection where it moved (as MAUI's).
        var moved = _selectedItem is string text ? _items.IndexOf(text) : -1;
        Reconcile(moved >= 0 ? moved : _selectedIndex, forceClamp: false);
    }

    /// <summary>
    /// After the items changed: an index waiting for items applies (without the event) once there is an item at it, or at once
    /// clamped with <paramref name="forceClamp"/> (a new source; as MAUI's picker); else the selection is clamped.
    /// </summary>
    private void Reconcile(int index, bool forceClamp)
    {
        if (_pendingSelectedIndex >= 0 && _items.Count > 0 && (forceClamp || _pendingSelectedIndex < _items.Count))
        {
            var pending = Math.Min(_pendingSelectedIndex, _items.Count - 1);
            _pendingSelectedIndex = -1;
            ApplySelectedIndex(pending, raise: false);
            return;
        }
        ApplySelectedIndex(Math.Clamp(index, -1, _items.Count - 1), raise: true);
    }

    private void ApplySelectedIndex(int index, bool raise)
    {
        var item = ItemAt(index);
        var changed = SetProperty(ref _selectedIndex, index, nameof(SelectedIndex));
        if (!Equals(_selectedItem, item))
        {
            _selectedItem = item;
            OnPropertyChanged(nameof(SelectedItem));
        }
        // The index may be the same while its text changed (a replaced item): the field shows the new text.
        Model.SelectedIndex = index;
        if (_selectedItemDisplay is not null)
            Model.SelectedFieldText = _multiple || index < 0 ? null : FieldTextOf(index);
        Model.SelectionChanged();
        if (!changed || !raise)
            return;
        Model.RunSelectionChangedCommand();
        SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
    }

    private object? ItemAt(int index)
    {
        if (index < 0)
            return null;
        if (_itemsSource is { } source)
            return index < source.Count ? source[index] : null;
        return index < _items.Count ? _items[index] : null;
    }

    private int IndexOfItem(object? item)
    {
        if (_itemsSource is { } source)
            return source.IndexOf(item);
        return item is string text ? _items.IndexOf(text) : -1;
    }

    #endregion

    #region Selector

    /// <summary>
    /// Chooses the picker's items when it opens, and returns the chosen ones, taken from <see cref="SkUiPickerSelection.Items"/> (none:
    /// clear the choice; <c>null</c>: dismissed, nothing changes). By default the drawn list on the presenter (<c>PopupPresenter</c>,
    /// else the app's); override it for a picker of your own that shows its own UI for <paramref name="selection"/> (its items, texts,
    /// which can be chosen, the current choice, whether several may be chosen). Close the UI when <paramref name="cancellationToken"/>
    /// is cancelled (the picker closed it: <c>IsOpen = false</c>, disabled, removed, its items changed); what is returned then is
    /// ignored. The picker applies the answer with its selection rules and events, as a choice made in the drawn list. For wheels,
    /// dates and full control over the popup, override <c>ShowPopupAsync</c> instead. Called on the UI thread, once per opening.
    /// SkiaUi extension.
    /// </summary>
    /// <param name="selection">What to choose from.</param>
    /// <param name="cancellationToken">Cancelled when the selector must close now.</param>
    protected virtual async Task<IReadOnlyList<object>?> SelectItemsAsync(SkUiPickerSelection selection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(selection);
        await base.ShowPopupAsync(selection.PopupRequest, cancellationToken);
        return selection.Chosen();
    }

    /// <inheritdoc />
    /// <remarks>Lists (one choice or several) go through <see cref="SelectItemsAsync"/>.</remarks>
    protected override async Task ShowPopupAsync(SkUiPopupRequest request, CancellationToken cancellationToken)
    {
        if (SkUiPickerSelection.From(request, ItemsSource) is not { } selection)
        {
            await base.ShowPopupAsync(request, cancellationToken);
            return;
        }
        selection.Apply(await SelectItemsAsync(selection, cancellationToken));
    }

    #endregion

    #region Base

    /// <inheritdoc />
    private protected override IList? PopupItemsSource => _itemsSource;

    /// <inheritdoc />
    private protected override void ChooseItem(int index)
    {
        if (!_multiple && index >= 0 && index < _items.Count)
            SetSelectedIndex(index);
    }

    #endregion
}
