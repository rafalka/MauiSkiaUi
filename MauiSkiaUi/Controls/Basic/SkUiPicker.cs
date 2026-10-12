using System.Collections;
using System.Collections.Specialized;
using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// A drawn picker: MAUI's <c>Picker</c> API (XAML ports by prefix) with a field drawn by the look and its list on the app's
/// popup system. The field shows the selected item's text, or <see cref="Title"/> in <see cref="TitleColor"/> while nothing is
/// selected; a tap, Space, Enter or Alt+Down opens the list (<see cref="IsOpen"/>), arrow keys change the selection without
/// opening (as a desktop combo box). The list is a drawn, virtualized <see cref="SkUiCollectionView"/> in its own surface,
/// handed to the app's <see cref="ISkUiPopupPresenter"/> (<see cref="PopupPresenter"/>, else <see cref="SkUiPopups.Presenter"/>,
/// else MAUI's action sheet): the selection highlighted and scrolled into view, a tap chooses, keyboard and screen readers.
/// </summary>
/// <remarks>
/// <para>
/// <b>Beyond MAUI:</b> <see cref="PopupPlacement"/> (anchored like a combo box, in the window like a dialog or a sheet, at
/// <see cref="PopupBounds"/>, or the look's choice per platform), <see cref="ItemTemplate"/> (drawn rows),
/// <see cref="MaxPopupHeight"/> and <see cref="PopupPresenter"/>.
/// </para>
/// <para>
/// <b>Selection</b> follows MAUI's rules: <see cref="SelectedIndex"/> and <see cref="SelectedItem"/> stay in step, an index set
/// before the items applies when they come, and on collection changes the selected item is kept (or the index clamped).
/// </para>
/// <para>
/// <b>The popup closes</b> when an item is chosen, the user dismisses it (the presenter's tap outside, Back, Escape),
/// <see cref="IsOpen"/> is set to <c>false</c>, or the picker is disabled, hidden or removed; an anchored popup also closes when
/// a scroller around the picker scrolls or the window resizes. <see cref="Opened"/> and <see cref="Closed"/> follow
/// <see cref="IsOpen"/>, as MAUI's; a choice raises <see cref="SelectedIndexChanged"/> before <see cref="Closed"/>.
/// </para>
/// </remarks>
public class SkUiPicker : SkUiView, ISkUiPickerOwner, ISkUiKeyHandler
{
    private static readonly PickerOpenedEventArgs OpenedArgs = new();
    private static readonly PickerClosedEventArgs ClosedArgs = new();
    private static readonly BindableProperty DisplayProperty = BindableProperty.Create("Display", typeof(string), typeof(SkUiPicker), null);

    private readonly SkUiPickerModel _model;
    private readonly SkUiPickerItems _items = new();
    private SkUiWeakListener<SkUiPicker>? _itemsSourceListener;
    private IList? _itemsSource;
    private BindingBase? _itemDisplayBinding;
    private BindableObject? _displayProbe;
    private bool _hasPendingSelectedIndex;
    private int _pendingSelectedIndex = -1;
    private bool _settingSelectedIndexInternally;
    private bool _suppressSelectedIndexChanged;

    /// <summary>Creates a picker without items.</summary>
    public SkUiPicker()
    {
        _model = new SkUiPickerModel(this) { Items = _items };
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

    /// <summary>Bindable property for <see cref="Title"/>.</summary>
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(nameof(Title), typeof(string), typeof(SkUiPicker), null,
        propertyChanged: (view, _, value) => ((SkUiPicker)view).Apply(model => model.Title = (string?)value, measure: true));

    /// <summary>Bindable property for <see cref="TitleColor"/>.</summary>
    public static readonly BindableProperty TitleColorProperty = BindableProperty.Create(nameof(TitleColor), typeof(Color), typeof(SkUiPicker), null,
        propertyChanged: (view, _, value) => ((SkUiPicker)view).Apply(model => model.TitleColor = (Color?)value));

    /// <summary>Bindable property for <see cref="TextColor"/>.</summary>
    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(SkUiPicker), null,
        propertyChanged: (view, _, value) => ((SkUiPicker)view).Apply(model => model.TextColor = (Color?)value));

    /// <summary>Bindable property for <see cref="FontFamily"/>.</summary>
    public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create(nameof(FontFamily), typeof(string), typeof(SkUiPicker), null,
        propertyChanged: (view, _, value) => ((SkUiPicker)view).Apply(model => model.FontFamily = (string?)value, measure: true));

    /// <summary>Bindable property for <see cref="FontSize"/>.</summary>
    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(nameof(FontSize), typeof(double), typeof(SkUiPicker), SkUiPickerModel.DefaultFontSize,
        validateValue: SkUiValidate.FinitePositive,
        propertyChanged: (view, _, value) => ((SkUiPicker)view).Apply(model => model.FontSize = (double)value, measure: true));

    /// <summary>Bindable property for <see cref="FontAttributes"/>.</summary>
    public static readonly BindableProperty FontAttributesProperty = BindableProperty.Create(nameof(FontAttributes), typeof(FontAttributes), typeof(SkUiPicker), FontAttributes.None,
        propertyChanged: (view, _, value) => ((SkUiPicker)view).Apply(model => model.FontAttributes = (FontAttributes)value, measure: true));

    /// <summary>Bindable property for <see cref="FontAutoScalingEnabled"/>.</summary>
    public static readonly BindableProperty FontAutoScalingEnabledProperty = BindableProperty.Create(nameof(FontAutoScalingEnabled), typeof(bool), typeof(SkUiPicker), true,
        propertyChanged: (view, _, value) => ((SkUiPicker)view).Apply(model => model.FontAutoScalingEnabled = (bool)value, measure: true));

    /// <summary>Bindable property for <see cref="CharacterSpacing"/>.</summary>
    public static readonly BindableProperty CharacterSpacingProperty = BindableProperty.Create(nameof(CharacterSpacing), typeof(double), typeof(SkUiPicker), 0d,
        validateValue: SkUiValidate.Finite,
        propertyChanged: (view, _, value) => ((SkUiPicker)view).Apply(model => model.CharacterSpacing = (double)value, measure: true));

    /// <summary>Bindable property for <see cref="HorizontalTextAlignment"/>.</summary>
    public static readonly BindableProperty HorizontalTextAlignmentProperty = BindableProperty.Create(nameof(HorizontalTextAlignment), typeof(TextAlignment), typeof(SkUiPicker), TextAlignment.Start,
        validateValue: (_, value) => Enum.IsDefined((TextAlignment)value),
        propertyChanged: (view, _, value) => ((SkUiPicker)view).Apply(model => model.HorizontalTextAlignment = (TextAlignment)value));

    /// <summary>Bindable property for <see cref="VerticalTextAlignment"/>.</summary>
    public static readonly BindableProperty VerticalTextAlignmentProperty = BindableProperty.Create(nameof(VerticalTextAlignment), typeof(TextAlignment), typeof(SkUiPicker), TextAlignment.Center,
        validateValue: (_, value) => Enum.IsDefined((TextAlignment)value),
        propertyChanged: (view, _, value) => ((SkUiPicker)view).Apply(model => model.VerticalTextAlignment = (TextAlignment)value));

    /// <summary>Bindable property for <see cref="IsOpen"/> (two-way by default).</summary>
    public static readonly BindableProperty IsOpenProperty = BindableProperty.Create(nameof(IsOpen), typeof(bool), typeof(SkUiPicker), false, BindingMode.TwoWay,
        // A picker that cannot show a popup stays closed (as a MAUI picker without a handler; unlike MAUI's, it does not open later).
        coerceValue: (view, value) => (bool)value && !((SkUiPicker)view).CanOpen ? false : value,
        propertyChanged: (view, _, value) => ((SkUiPicker)view).OnIsOpenChanged((bool)value));

    /// <summary>Bindable property for <see cref="ItemTemplate"/>.</summary>
    public static readonly BindableProperty ItemTemplateProperty = BindableProperty.Create(nameof(ItemTemplate), typeof(DataTemplate), typeof(SkUiPicker), null,
        propertyChanged: (view, _, value) => ((SkUiPicker)view)._model.ItemTemplate = (DataTemplate?)value);

    /// <summary>Bindable property for <see cref="MaxPopupHeight"/>.</summary>
    public static readonly BindableProperty MaxPopupHeightProperty = BindableProperty.Create(nameof(MaxPopupHeight), typeof(double), typeof(SkUiPicker), -1d,
        validateValue: (_, value) => value is double number && SkUiPickerValidation.IsMaxPopupHeight(number),
        propertyChanged: (view, _, value) => ((SkUiPicker)view)._model.MaxPopupHeight = (double)value);

    /// <summary>Bindable property for <see cref="PopupPlacement"/>.</summary>
    public static readonly BindableProperty PopupPlacementProperty = BindableProperty.Create(nameof(PopupPlacement), typeof(SkUiPopupPlacement), typeof(SkUiPicker), SkUiPopupPlacement.Default,
        validateValue: (_, value) => Enum.IsDefined((SkUiPopupPlacement)value),
        propertyChanged: (view, _, value) => ((SkUiPicker)view)._model.PopupPlacement = (SkUiPopupPlacement)value);

    /// <summary>Bindable property for <see cref="PopupBounds"/>.</summary>
    public static readonly BindableProperty PopupBoundsProperty = BindableProperty.Create(nameof(PopupBounds), typeof(Rect), typeof(SkUiPicker), Rect.Zero,
        propertyChanged: (view, _, value) => ((SkUiPicker)view)._model.PopupBounds = (Rect)value);

    /// <summary>Bindable property for <see cref="PopupPresenter"/>.</summary>
    public static readonly BindableProperty PopupPresenterProperty = BindableProperty.Create(nameof(PopupPresenter), typeof(ISkUiPopupPresenter), typeof(SkUiPicker), null,
        propertyChanged: (view, _, value) => ((SkUiPicker)view)._model.PopupPresenter = (ISkUiPopupPresenter?)value);

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

    /// <summary>The index of the selected item; -1 (default): none. Two-way by default.</summary>
    public int SelectedIndex { get => (int)GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }

    /// <summary>The selected item (of <see cref="ItemsSource"/>, else of <see cref="Items"/>), or <c>null</c>. Two-way by default.</summary>
    public object? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }

    /// <summary>Shown while nothing is selected (in <see cref="TitleColor"/>), and as the title of window popups and action sheets.</summary>
    public string? Title { get => (string?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>The color of <see cref="Title"/> in the field; <c>null</c> (default): the color scheme's muted color.</summary>
    public Color? TitleColor { get => (Color?)GetValue(TitleColorProperty); set => SetValue(TitleColorProperty, value); }

    /// <summary>The selected item's text color (also the chevron and the list's rows); <c>null</c> (default): the color scheme's foreground.</summary>
    public Color? TextColor { get => (Color?)GetValue(TextColorProperty); set => SetValue(TextColorProperty, value); }

    /// <summary>System font family, or a name registered with <see cref="SkUiFonts.Register"/> / MAUI's <c>ConfigureFonts</c>.</summary>
    public string? FontFamily { get => (string?)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    /// <summary>Font size in DIPs (default 16).</summary>
    [System.ComponentModel.TypeConverter(typeof(FontSizeConverter))]
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>Bold and italic.</summary>
    public FontAttributes FontAttributes { get => (FontAttributes)GetValue(FontAttributesProperty); set => SetValue(FontAttributesProperty, value); }

    /// <summary>Whether the text follows the system text size (<see cref="SkUiFontScaling"/>), as MAUI's. Default <c>true</c>.</summary>
    public bool FontAutoScalingEnabled { get => (bool)GetValue(FontAutoScalingEnabledProperty); set => SetValue(FontAutoScalingEnabledProperty, value); }

    /// <summary>DIPs added after each character.</summary>
    public double CharacterSpacing { get => (double)GetValue(CharacterSpacingProperty); set => SetValue(CharacterSpacingProperty, value); }

    /// <summary>The text's horizontal alignment in the field (default start; Start follows the text's direction).</summary>
    public TextAlignment HorizontalTextAlignment { get => (TextAlignment)GetValue(HorizontalTextAlignmentProperty); set => SetValue(HorizontalTextAlignmentProperty, value); }

    /// <summary>The text's vertical alignment in the field (default centered).</summary>
    public TextAlignment VerticalTextAlignment { get => (TextAlignment)GetValue(VerticalTextAlignmentProperty); set => SetValue(VerticalTextAlignmentProperty, value); }

    /// <summary>
    /// Whether the list is open (MAUI 10's). Set it to open or close the list; it goes back to <c>false</c> when the popup
    /// closes. A picker that is disabled, hidden or not on a drawn surface does not open (it stays <c>false</c>). Two-way by default.
    /// </summary>
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>
    /// The rows of the list: a template of drawn views (<c>SkUi*</c>) bound to the items of <see cref="ItemsSource"/> (or to the
    /// texts of <see cref="Items"/>); a <see cref="DataTemplateSelector"/> picks per item. <c>null</c> (default): the item texts
    /// in the picker's font. The field still shows the display text. Presenters that show their own UI ignore it. SkiaUi extension.
    /// </summary>
    public DataTemplate? ItemTemplate { get => (DataTemplate?)GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }

    /// <summary>The most the list is tall, in DIPs; -1 (default): as tall as its window allows. SkiaUi extension.</summary>
    public double MaxPopupHeight { get => (double)GetValue(MaxPopupHeightProperty); set => SetValue(MaxPopupHeightProperty, value); }

    /// <summary>
    /// Where the list opens: <see cref="SkUiPopupPlacement.Anchored"/> (below or above the picker, at least its width, as a combo
    /// box), <see cref="SkUiPopupPlacement.Window"/> (in the window, aligned by the look: centered by default),
    /// <see cref="SkUiPopupPlacement.Absolute"/> (at <see cref="PopupBounds"/>) or <see cref="SkUiPopupPlacement.Default"/>
    /// (default: the look's, <see cref="SkUiLook.DefaultPopupPlacement"/>, per platform and idiom). SkiaUi extension.
    /// </summary>
    public SkUiPopupPlacement PopupPlacement { get => (SkUiPopupPlacement)GetValue(PopupPlacementProperty); set => SetValue(PopupPlacementProperty, value); }

    /// <summary>The window rectangle an <see cref="SkUiPopupPlacement.Absolute"/> list opens at (empty: as a window popup). SkiaUi extension.</summary>
    public Rect PopupBounds { get => (Rect)GetValue(PopupBoundsProperty); set => SetValue(PopupBoundsProperty, value); }

    /// <summary>The presenter of this picker's list; <c>null</c> (default): the app's (<see cref="SkUiPopups.Presenter"/>). SkiaUi extension.</summary>
    public ISkUiPopupPresenter? PopupPresenter { get => (ISkUiPopupPresenter?)GetValue(PopupPresenterProperty); set => SetValue(PopupPresenterProperty, value); }

    /// <summary>Raised when <see cref="SelectedIndex"/> changes (by the user or the app; not when an index set before the items applies).</summary>
    public event EventHandler? SelectedIndexChanged;

    /// <summary>Raised when <see cref="IsOpen"/> becomes <c>true</c> (the list opens).</summary>
    public event EventHandler<PickerOpenedEventArgs>? Opened;

    /// <summary>Raised when <see cref="IsOpen"/> becomes <c>false</c> (the list closes).</summary>
    public event EventHandler<PickerClosedEventArgs>? Closed;

    /// <summary>Sets <see cref="ItemsSource"/> (same as the property setter).</summary>
    public SkUiPicker SetItemsSource(IList? value) { ItemsSource = value; return this; }

    /// <summary>Sets <see cref="ItemDisplayBinding"/> (same as the property setter).</summary>
    public SkUiPicker SetItemDisplayBinding(BindingBase? value) { ItemDisplayBinding = value; return this; }

    /// <summary>Sets <see cref="SelectedIndex"/> (same as the property setter).</summary>
    public SkUiPicker SetSelectedIndex(int value) { ArgumentOutOfRangeException.ThrowIfLessThan(value, -1); SelectedIndex = value; return this; }

    /// <summary>Sets <see cref="SelectedItem"/> (same as the property setter).</summary>
    public SkUiPicker SetSelectedItem(object? value) { SelectedItem = value; return this; }

    /// <summary>Sets <see cref="Title"/> (same as the property setter).</summary>
    public SkUiPicker SetTitle(string? value) { Title = value; return this; }

    /// <summary>Sets <see cref="TitleColor"/> (same as the property setter).</summary>
    public SkUiPicker SetTitleColor(Color? value) { TitleColor = value; return this; }

    /// <summary>Sets <see cref="TextColor"/> (same as the property setter).</summary>
    public SkUiPicker SetTextColor(Color? value) { TextColor = value; return this; }

    /// <summary>Sets <see cref="FontFamily"/> (same as the property setter).</summary>
    public SkUiPicker SetFontFamily(string? value) { FontFamily = value; return this; }

    /// <summary>Sets <see cref="FontSize"/> (same as the property setter).</summary>
    public SkUiPicker SetFontSize(double value) { SkUiValidate.ThrowIfNotFinitePositive(value, nameof(value)); FontSize = value; return this; }

    /// <summary>Sets <see cref="FontAttributes"/> (same as the property setter).</summary>
    public SkUiPicker SetFontAttributes(FontAttributes value) { FontAttributes = value; return this; }

    /// <summary>Sets <see cref="FontAutoScalingEnabled"/> (same as the property setter).</summary>
    public SkUiPicker SetFontAutoScalingEnabled(bool value) { FontAutoScalingEnabled = value; return this; }

    /// <summary>Sets <see cref="CharacterSpacing"/> (same as the property setter).</summary>
    public SkUiPicker SetCharacterSpacing(double value) { SkUiValidate.ThrowIfNotFinite(value, nameof(value)); CharacterSpacing = value; return this; }

    /// <summary>Sets <see cref="HorizontalTextAlignment"/> (same as the property setter).</summary>
    public SkUiPicker SetHorizontalTextAlignment(TextAlignment value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        HorizontalTextAlignment = value;
        return this;
    }

    /// <summary>Sets <see cref="VerticalTextAlignment"/> (same as the property setter).</summary>
    public SkUiPicker SetVerticalTextAlignment(TextAlignment value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        VerticalTextAlignment = value;
        return this;
    }

    /// <summary>Sets <see cref="IsOpen"/> (same as the property setter).</summary>
    public SkUiPicker SetIsOpen(bool value) { IsOpen = value; return this; }

    /// <summary>Sets <see cref="ItemTemplate"/> (same as the property setter).</summary>
    public SkUiPicker SetItemTemplate(DataTemplate? value) { ItemTemplate = value; return this; }

    /// <summary>Sets <see cref="MaxPopupHeight"/> (same as the property setter; -1: no limit).</summary>
    public SkUiPicker SetMaxPopupHeight(double value)
    {
        SkUiPickerValidation.ThrowIfNotMaxPopupHeight(value, nameof(value));
        MaxPopupHeight = value;
        return this;
    }

    /// <summary>Sets <see cref="PopupPlacement"/> (same as the property setter).</summary>
    public SkUiPicker SetPopupPlacement(SkUiPopupPlacement value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        PopupPlacement = value;
        return this;
    }

    /// <summary>Sets <see cref="PopupBounds"/> (same as the property setter).</summary>
    public SkUiPicker SetPopupBounds(Rect value) { PopupBounds = value; return this; }

    /// <summary>Sets <see cref="PopupPresenter"/> (same as the property setter).</summary>
    public SkUiPicker SetPopupPresenter(ISkUiPopupPresenter? value) { PopupPresenter = value; return this; }

    private void Apply(Action<SkUiPickerModel> apply, bool measure = false)
    {
        apply(_model);
        if (measure)
            _model.TextChanged();
        else
            _model.PaintChanged();
    }

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
        _model.Items = _items;
        // Changes made by the picker from its source reconcile the selection themselves.
        if (_items.IsReadOnly)
            return;
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
        _items.Reset(DisplayTexts(source));
        if (!TryApplyPendingSelectedIndex(forceClamp: true))
            ClampSelectedIndex(SelectedIndex);
    }

    private void ResyncItemsAndReconcileSelection()
    {
        if (_itemsSource is not { } source)
            return;
        _items.Reset(DisplayTexts(source));
        if (TryApplyPendingSelectedIndex(forceClamp: false))
            return;
        ClampSelectedIndex(SelectedItem is { } selected ? source.IndexOf(selected) : SelectedIndex);
    }

    private object CoerceSelectedIndex(int value)
    {
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
        _model.SelectedIndex = SelectedIndex;
        UpdateSelectedItem(SelectedIndex);
        _model.SelectionChanged();
        if (!_suppressSelectedIndexChanged)
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

    #region Popup

    private void OnIsOpenChanged(bool value)
    {
        if (value)
        {
            Opened?.Invoke(this, OpenedArgs);
            _model.Open();
        }
        else
        {
            Closed?.Invoke(this, ClosedArgs);
            _model.Close();
        }
        InvalidatePaint();
    }

    /// <summary>The open popup's request (tests), or <c>null</c>.</summary>
    internal SkUiPopupRequest? PopupRequest => _model.Request;

    /// <summary>Whether a popup is showing (or closing) now.</summary>
    internal bool IsPopupShown => _model.IsOpen;

    private bool CanOpen => IsEnabled && IsVisible && SkUiDiagnostics.GetSurfaceRoot(this) is not null;

    #endregion

    #region Field

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        _model.HasExplicitMinimumHeight = MinimumHeightRequest >= 0;
        return _model.Measure(widthConstraint, heightConstraint);
    }

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas) => _model.Draw(canvas, (float)Width, (float)Height);

    /// <inheritdoc />
    protected override bool HandlesTap => true;

    /// <inheritdoc />
    protected override bool CanReceiveTap => IsEnabled;

    /// <inheritdoc />
    protected override void OnTapped(SkUiTappedEventArgs args)
    {
        RaiseTapped(args);
        ExecuteTappedCommand();
        IsOpen = !IsOpen;
    }

    /// <inheritdoc />
    protected override void OnPressedChanged() => _model.SetPressed(IsPressed, PressPosition);

    /// <inheritdoc />
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        switch (propertyName)
        {
            case nameof(IsEnabled):
                _model.IsEnabled = IsEnabled;
                _model.PaintChanged();
                if (!IsEnabled)
                    ClosePopup();
                break;
            case nameof(IsVisible) when !IsVisible:
                ClosePopup();
                break;
            case nameof(IsPointerOver):
                _model.IsPointerOver = IsPointerOver;
                _model.PaintChanged();
                break;
            case nameof(IsFocused):
                _model.SetFocused(IsFocused);
                break;
            case nameof(MinimumHeightRequest):
                InvalidateMeasureOverride();
                break;
        }
    }

    /// <summary>The picker can no longer show its popup: it closes (<see cref="IsOpen"/> goes to <c>false</c>, raising <see cref="Closed"/>).</summary>
    private void ClosePopup()
    {
        if (IsOpen)
            IsOpen = false;
        else
            _model.Close();
    }

    /// <inheritdoc />
    protected override void OnAnimationRootChanged(bool subtreeDetached = false)
    {
        base.OnAnimationRootChanged(subtreeDetached);
        if (subtreeDetached || SkUiDiagnostics.GetSurfaceRoot(this) is null)
            ClosePopup();
    }

    /// <inheritdoc />
    internal override void OnEffectiveFlowDirectionChanged()
    {
        base.OnEffectiveFlowDirectionChanged();
        _model.TextChanged();
    }

    /// <inheritdoc />
    internal override void ReleaseDrawingResources()
    {
        base.ReleaseDrawingResources();
        _model.ReleaseDrawingResources();
        // The surface was torn down or the picker left it: its popup goes (IsOpen follows once it has).
        _model.Close();
    }

    /// <inheritdoc />
    protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
    {
        base.OnPopulateSemantics(info);
        _model.PopulateSemantics(info);
    }

    /// <inheritdoc />
    internal override bool TakesKeyboardFocus => true;

    bool ISkUiKeyHandler.OnKeyDown(in SkUiKeyEvent key) => _model.OnKeyDown(key);

    #endregion

    #region Owner

    ISkUiRenderable ISkUiPickerOwner.Node => this;

    bool ISkUiPickerOwner.IsRightToLeft => IsRightToLeft;

    SkUiTextDirection ISkUiPickerOwner.TextDirection
    {
        get
        {
            var effective = ((IVisualElementController)this).EffectiveFlowDirection;
            if (effective.HasFlag(EffectiveFlowDirection.RightToLeft)) return SkUiTextDirection.RightToLeft;
            return effective.HasFlag(EffectiveFlowDirection.Explicit) ? SkUiTextDirection.LeftToRight : SkUiTextDirection.Auto;
        }
    }

    bool ISkUiPickerOwner.CanOpen => CanOpen;

    IList? ISkUiPickerOwner.ItemsSource => _itemsSource;

    void ISkUiPickerOwner.Choose(int index)
    {
        if (index >= 0 && index < _items.Count && index != SelectedIndex)
            SelectedIndex = index;
    }

    void ISkUiPickerOwner.SetIsOpen(bool open)
    {
        if (IsOpen != open)
            IsOpen = open;
    }

    void ISkUiPickerOwner.FocusField()
    {
        if (IsEnabled && IsVisible)
            Focus();
    }

    void ISkUiPickerOwner.InvalidateMeasure() => InvalidateMeasureOverride();

    void ISkUiPickerOwner.InvalidatePaint() => InvalidatePaint();

    void ISkUiPickerOwner.InvalidateSemantics() => InvalidateSemantics();

    #endregion
}

/// <summary>Checks of the pickers' own values.</summary>
internal static class SkUiPickerValidation
{
    /// <summary>How close to -1 a <c>MaxPopupHeight</c> must be to mean "no limit".</summary>
    internal const double Tolerance = 1e-9;

    public static bool IsMaxPopupHeight(double value) => !double.IsNaN(value) && (value >= 0 || Math.Abs(value + 1) < Tolerance);

    public static void ThrowIfNotMaxPopupHeight(double value, string name)
    {
        if (!IsMaxPopupHeight(value))
            throw new ArgumentOutOfRangeException(name, value, "The height must be at least 0, or -1 for no limit.");
    }
}
