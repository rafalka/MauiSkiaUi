using System.Collections;
using System.Collections.Specialized;
using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// Drawn picker (Core analogue of <c>SkUiPicker</c>): the same field drawn by the look and the same list on the app's popup
/// system (<see cref="ISkUiPopupPresenter"/>), anchored to the node's <see cref="SkUiCoreHost"/> (the MAUI view the request
/// carries). Fill it with <see cref="Items"/> or <see cref="ItemsSource"/> (texts from <see cref="ItemDisplay"/>, else
/// <c>ToString()</c>); <see cref="SelectedIndex"/> / <see cref="SelectedItem"/> follow MAUI's picker rules: an index set
/// before the items applies when they come, and on collection changes the selected item is kept (or the index clamped).
/// </summary>
/// <remarks>Core nodes have no enabled state: the picker always reacts (hide it, or remove it, to stop it).</remarks>
public class SkUiCorePicker : SkUiCoreNode, ISkUiPickerOwner, ISkUiKeyHandler
{
    private readonly SkUiPickerModel _model;
    private readonly SkUiPickerItems _items = new();
    private SkUiWeakListener<SkUiCorePicker>? _itemsSourceListener;
    private IList? _itemsSource;
    private Func<object?, string>? _itemDisplay;
    private int _selectedIndex = -1;
    private object? _selectedItem;
    private int _pendingSelectedIndex = -1;
    private bool _isOpen;
    private bool _minimumHeightExplicit;
    private bool _syncingItems;

    /// <summary>Creates a picker without items.</summary>
    public SkUiCorePicker()
    {
        _model = new SkUiPickerModel(this) { Items = _items };
        _items.CollectionChanged += OnItemsCollectionChanged;
        Focused += (_, _) => _model.SetFocused(true);
        Unfocused += (_, _) => _model.SetFocused(false);
    }

    #region Properties

    /// <summary>The item texts: add to it to fill the picker; while <see cref="ItemsSource"/> is set, the source's texts (read-only).</summary>
    public IList<string> Items => _items;

    /// <summary>The items to choose from (followed while it is an <see cref="INotifyCollectionChanged"/> list, weakly).</summary>
    public IList? ItemsSource { get => _itemsSource; set => SetItemsSource(value); }

    /// <summary>The text shown for each item of <see cref="ItemsSource"/>; <c>null</c> (default): <c>ToString()</c>.</summary>
    public Func<object?, string>? ItemDisplay { get => _itemDisplay; set => SetItemDisplay(value); }

    /// <summary>The selected item's index; -1 (default): none.</summary>
    public int SelectedIndex { get => _selectedIndex; set => SetSelectedIndex(value); }

    /// <summary>The selected item (of <see cref="ItemsSource"/>, else of <see cref="Items"/>), or <c>null</c>; see <see cref="SetSelectedItem"/>.</summary>
    public object? SelectedItem { get => _selectedItem; set => SetSelectedItem(value); }

    /// <summary>Shown while nothing is selected (in <see cref="TitleColor"/>), and as the title of window popups.</summary>
    public string? Title { get => _model.Title; set => SetTitle(value); }

    /// <summary>The title's color in the field; <c>null</c> (default): the color scheme's muted color.</summary>
    public Color? TitleColor { get => _model.TitleColor; set => SetTitleColor(value); }

    /// <summary>The text color; <c>null</c> (default): the color scheme's foreground.</summary>
    public Color? TextColor { get => _model.TextColor; set => SetTextColor(value); }

    /// <summary>Font family (see <c>SkUiPicker.FontFamily</c>).</summary>
    public string? FontFamily { get => _model.FontFamily; set => SetFontFamily(value); }

    /// <summary>Font size in DIPs (default 16).</summary>
    public double FontSize { get => _model.FontSize; set => SetFontSize(value); }

    /// <summary>Bold and italic.</summary>
    public FontAttributes FontAttributes { get => _model.FontAttributes; set => SetFontAttributes(value); }

    /// <summary>Whether the text follows the system text size. Default <c>true</c>.</summary>
    public bool FontAutoScalingEnabled { get => _model.FontAutoScalingEnabled; set => SetFontAutoScalingEnabled(value); }

    /// <summary>DIPs added after each character.</summary>
    public double CharacterSpacing { get => _model.CharacterSpacing; set => SetCharacterSpacing(value); }

    /// <summary>Horizontal text alignment (default start).</summary>
    public TextAlignment HorizontalTextAlignment { get => _model.HorizontalTextAlignment; set => SetHorizontalTextAlignment(value); }

    /// <summary>Vertical text alignment (default centered).</summary>
    public TextAlignment VerticalTextAlignment { get => _model.VerticalTextAlignment; set => SetVerticalTextAlignment(value); }

    /// <summary>Whether the list is open; goes back to <c>false</c> when the popup closes. A picker not on a surface does not open.</summary>
    public bool IsOpen { get => _isOpen; set => SetIsOpen(value); }

    /// <summary>The list's rows: a template of drawn <c>SkUi*</c> views bound to the items (see <c>SkUiPicker.ItemTemplate</c>).</summary>
    public DataTemplate? ItemTemplate { get => _model.ItemTemplate; set => SetItemTemplate(value); }

    /// <summary>The most the list is tall, in DIPs; -1 (default): no limit.</summary>
    public double MaxPopupHeight { get => _model.MaxPopupHeight; set => SetMaxPopupHeight(value); }

    /// <summary>Where the list opens (see <c>SkUiPicker.PopupPlacement</c>; default the look's).</summary>
    public SkUiPopupPlacement PopupPlacement { get => _model.PopupPlacement; set => SetPopupPlacement(value); }

    /// <summary>The window rectangle an <see cref="SkUiPopupPlacement.Absolute"/> list opens at.</summary>
    public Rect PopupBounds { get => _model.PopupBounds; set => SetPopupBounds(value); }

    /// <summary>This picker's presenter; <c>null</c> (default): the app's (<see cref="SkUiPopups.Presenter"/>).</summary>
    public ISkUiPopupPresenter? PopupPresenter { get => _model.PopupPresenter; set => SetPopupPresenter(value); }

    /// <summary>Raised when <see cref="SelectedIndex"/> changes (by the user or the app; not when an index set before the items applies).</summary>
    public event EventHandler? SelectedIndexChanged;

    /// <summary>Raised when <see cref="IsOpen"/> becomes <c>true</c>.</summary>
    public event EventHandler<PickerOpenedEventArgs>? Opened;

    /// <summary>Raised when <see cref="IsOpen"/> becomes <c>false</c>.</summary>
    public event EventHandler<PickerClosedEventArgs>? Closed;

    private static readonly PickerOpenedEventArgs OpenedArgs = new();
    private static readonly PickerClosedEventArgs ClosedArgs = new();

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
        _pendingSelectedIndex = -1;
        ApplySelectedIndex(IndexOfItem(item), raise: true);
        return this;
    }

    /// <summary>Sets <see cref="Title"/>.</summary>
    public SkUiCorePicker SetTitle(string? value) => Update(ref _model.Title, value, nameof(Title), measure: true);

    /// <summary>Sets <see cref="TitleColor"/>.</summary>
    public SkUiCorePicker SetTitleColor(Color? value) => Update(ref _model.TitleColor, value, nameof(TitleColor));

    /// <summary>Sets <see cref="TextColor"/>.</summary>
    public SkUiCorePicker SetTextColor(Color? value) => Update(ref _model.TextColor, value, nameof(TextColor));

    /// <summary>Sets <see cref="FontFamily"/>.</summary>
    public SkUiCorePicker SetFontFamily(string? value) => Update(ref _model.FontFamily, value, nameof(FontFamily), measure: true);

    /// <summary>Sets <see cref="FontSize"/>.</summary>
    public SkUiCorePicker SetFontSize(double value)
    {
        SkUiValidate.ThrowIfNotFinitePositive(value, nameof(value));
        return Update(ref _model.FontSize, value, nameof(FontSize), measure: true);
    }

    /// <summary>Sets <see cref="FontAttributes"/>.</summary>
    public SkUiCorePicker SetFontAttributes(FontAttributes value) => Update(ref _model.FontAttributes, value, nameof(FontAttributes), measure: true);

    /// <summary>Sets <see cref="FontAutoScalingEnabled"/>.</summary>
    public SkUiCorePicker SetFontAutoScalingEnabled(bool value) => Update(ref _model.FontAutoScalingEnabled, value, nameof(FontAutoScalingEnabled), measure: true);

    /// <summary>Sets <see cref="CharacterSpacing"/>.</summary>
    public SkUiCorePicker SetCharacterSpacing(double value)
    {
        SkUiValidate.ThrowIfNotFinite(value, nameof(value));
        return Update(ref _model.CharacterSpacing, value, nameof(CharacterSpacing), measure: true);
    }

    /// <summary>Sets <see cref="HorizontalTextAlignment"/>.</summary>
    public SkUiCorePicker SetHorizontalTextAlignment(TextAlignment value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        return Update(ref _model.HorizontalTextAlignment, value, nameof(HorizontalTextAlignment));
    }

    /// <summary>Sets <see cref="VerticalTextAlignment"/>.</summary>
    public SkUiCorePicker SetVerticalTextAlignment(TextAlignment value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        return Update(ref _model.VerticalTextAlignment, value, nameof(VerticalTextAlignment));
    }

    /// <summary>Sets <see cref="IsOpen"/> (opens or closes the list).</summary>
    public SkUiCorePicker SetIsOpen(bool value)
    {
        if (value == _isOpen)
            return this;
        if (value && !CanOpen)
            return this; // not on a surface, or hidden: it does not open
        SetProperty(ref _isOpen, value, nameof(IsOpen));
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
        return this;
    }

    /// <summary>Sets <see cref="ItemTemplate"/>.</summary>
    public SkUiCorePicker SetItemTemplate(DataTemplate? value) => Update(ref _model.ItemTemplate, value, nameof(ItemTemplate), paint: false);

    /// <summary>Sets <see cref="MaxPopupHeight"/> (-1: no limit).</summary>
    public SkUiCorePicker SetMaxPopupHeight(double value)
    {
        SkUiPickerValidation.ThrowIfNotMaxPopupHeight(value, nameof(value));
        return Update(ref _model.MaxPopupHeight, value, nameof(MaxPopupHeight), paint: false);
    }

    /// <summary>Sets <see cref="PopupPlacement"/>.</summary>
    public SkUiCorePicker SetPopupPlacement(SkUiPopupPlacement value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        return Update(ref _model.PopupPlacement, value, nameof(PopupPlacement), paint: false);
    }

    /// <summary>Sets <see cref="PopupBounds"/>.</summary>
    public SkUiCorePicker SetPopupBounds(Rect value) => Update(ref _model.PopupBounds, value, nameof(PopupBounds), paint: false);

    /// <summary>Sets <see cref="PopupPresenter"/>.</summary>
    public SkUiCorePicker SetPopupPresenter(ISkUiPopupPresenter? value) => Update(ref _model.PopupPresenter, value, nameof(PopupPresenter), paint: false);

    /// <inheritdoc />
    public override SkUiCoreNode SetMinimumHeight(double value)
    {
        _minimumHeightExplicit = true;
        return base.SetMinimumHeight(value);
    }

    private SkUiCorePicker Update<T>(ref T field, T value, string name, bool measure = false, bool paint = true)
    {
        if (!SetProperty(ref field, value, name))
            return this;
        if (measure)
            _model.TextChanged();
        else if (paint)
            _model.PaintChanged();
        return this;
    }

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
        Reconcile(selected is not null ? source.IndexOf(selected) : _selectedIndex, forceClamp);
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        _model.Items = _items;
        if (_syncingItems || _items.IsReadOnly)
            return;
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
        _model.SelectedIndex = index;
        _model.SelectionChanged();
        if (changed && raise)
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

    #region Field

    private bool CanOpen => IsVisible && SkUiDiagnostics.GetSurfaceRoot(this) is not null;

    /// <summary>The open popup's request (tests), or <c>null</c>.</summary>
    internal SkUiPopupRequest? PopupRequest => _model.Request;

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        _model.HasExplicitMinimumHeight = _minimumHeightExplicit;
        return _model.Measure(widthConstraint, heightConstraint);
    }

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas) => _model.Draw(canvas, (float)Frame.Width, (float)Frame.Height);

    /// <inheritdoc />
    internal override bool HasIntrinsicTap => true;

    /// <inheritdoc />
    internal override void OnIntrinsicTap(SkUiTappedEventArgs args) => SetIsOpen(!_isOpen);

    /// <inheritdoc />
    internal override void OnGesturePressedChanged(bool pressed) => _model.SetPressed(pressed, PressPosition);

    /// <inheritdoc />
    internal override void OnPointerOverChanged(bool isOver)
    {
        _model.IsPointerOver = isOver;
        _model.PaintChanged();
    }

    /// <inheritdoc />
    protected override void OnIsVisibleChanged()
    {
        base.OnIsVisibleChanged();
        if (!IsVisible)
            SetIsOpen(false);
    }

    /// <inheritdoc />
    protected override void OnAnimationRootChanged(bool subtreeDetached = false)
    {
        base.OnAnimationRootChanged(subtreeDetached);
        if (subtreeDetached)
            SetIsOpen(false);
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

    SkUiTextDirection ISkUiPickerOwner.TextDirection => InheritedDirection();

    bool ISkUiPickerOwner.CanOpen => CanOpen;

    IList? ISkUiPickerOwner.ItemsSource => _itemsSource;

    void ISkUiPickerOwner.Choose(int index)
    {
        if (index >= 0 && index < _items.Count)
            SetSelectedIndex(index);
    }

    void ISkUiPickerOwner.SetIsOpen(bool open) => SetIsOpen(open);

    void ISkUiPickerOwner.FocusField()
    {
        if (IsVisible)
            Focus();
    }

    void ISkUiPickerOwner.InvalidateMeasure() => InvalidateMeasure();

    void ISkUiPickerOwner.InvalidatePaint() => InvalidatePaint();

    void ISkUiPickerOwner.InvalidateSemantics() => InvalidateSemantics();

    #endregion
}
