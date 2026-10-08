using System.Collections;
using System.Collections.Specialized;
using System.Windows.Input;
using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// A drawn, virtualized list with selection, item taps, a header and footer (scrolled or sticky), an empty view, a
/// load-more threshold and pull-to-refresh (FR-22). Its items are created on demand and recycled per template by the
/// FR-21 engine (<see cref="SkUiVirtualVerticalStackLayout"/>), inside a vertical scroller of its own; every item may
/// have its own height. In XAML the element's content is the <see cref="ItemTemplate"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>SkiaUi's own API, not MAUI's <c>CollectionView</c>.</b> Familiar names are kept where they fit
/// (<c>ItemsSource</c>, <c>ItemTemplate</c>, <c>SelectedItem</c>, <c>RemainingItemsThreshold</c>); the mapping from MAUI is in
/// the migration guide.
/// </para>
/// <para>
/// <b>Cost.</b> A sticky header or footer is laid out beside the scroller, so scrolling never re-records it. A selection
/// change re-records the two items whose state changed. Each item view is hosted in a drawn item container that takes
/// the item's taps (tappable views inside the item keep theirs) and draws <see cref="SelectionBackground"/>.
/// </para>
/// <para>
/// The list needs a bounded height (a grid row, a page): measured with an unbounded height (inside a vertical stack or a
/// scroll view) it is as tall as all its items, and creates every one.
/// </para>
/// </remarks>
[ContentProperty(nameof(ItemTemplate))]
public class SkUiCollectionView : SkUiView
{
    /// <summary>How far the top must be pulled (shown past the edge, in DIPs) for a release to start a refresh.</summary>
    internal const double RefreshTriggerDistance = 64;

    private readonly ItemsPart _items;
    private readonly SkUiScrollView _scroller;
    private readonly BodyPart _body;
    private readonly SkUiContentView _stickyHeaderHost = new();
    private readonly SkUiContentView _stickyFooterHost = new();
    private readonly RefreshLayer _refreshLayer;
    private readonly SkUiContentSlot _headerSlot;
    private readonly SkUiContentSlot _footerSlot;
    private readonly SkUiContentSlot _emptySlot;
    private SkUiSelectionMode _selectionMode;
    private object? _selectedItem;
    private Brush? _selectionBackground;
    private bool _isEmpty;
    private static Func<string> _selectedStateText = () => "Selected";

    /// <summary>Creates an empty list (GPU-backed when it is a surface of its own).</summary>
    public SkUiCollectionView()
    {
        HwAccelerated = true;
        _headerSlot = new SkUiContentSlot(this, HeaderProperty, HeaderTemplateProperty);
        _footerSlot = new SkUiContentSlot(this, FooterProperty, FooterTemplateProperty);
        _emptySlot = new SkUiContentSlot(this, EmptyViewProperty, EmptyViewTemplateProperty);
        _items = new ItemsPart(this);
        _items.SourceChanged += OnSourceChanged;
        _items.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(FirstVisibleIndex) or nameof(LastVisibleIndex))
                OnPropertyChanged(args.PropertyName);
        };
        _body = new BodyPart(this);
        _scroller = new SkUiScrollView { Content = _body };
        _scroller.Scrolled += (_, args) => Scrolled?.Invoke(this, args);
        _scroller.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(IsScrolling))
                OnPropertyChanged(nameof(IsScrolling));
        };
        var controller = Controller;
        controller.PullReleased += OnPullReleased;
        controller.OverscrollChanged += OnOverscrollChanged;
        _refreshLayer = new RefreshLayer();
        AttachChild(_stickyHeaderHost);
        AttachChild(_scroller);
        AttachChild(_stickyFooterHost);
        AttachChild(_refreshLayer);
        UpdateEmpty();
    }

    private SkUiScrollController Controller => ((ISkUiScrollHost)_scroller).Scroller;

    #region Items

    /// <summary>A bindable property of this view that sets the same property of the items layout.</summary>
    private static BindableProperty ForwardedToItems(BindableProperty itemsProperty, string? name = null, BindableProperty.ValidateValueDelegate? validate = null) =>
        BindableProperty.Create(name ?? itemsProperty.PropertyName, itemsProperty.ReturnType, typeof(SkUiCollectionView), itemsProperty.DefaultValue,
            validateValue: validate,
            propertyChanged: (view, _, value) => ((SkUiCollectionView)view)._items.SetValue(itemsProperty, value));

    /// <summary>Bindable property for <see cref="ItemsSource"/>.</summary>
    public static readonly BindableProperty ItemsSourceProperty = ForwardedToItems(SkUiVirtualVerticalStackLayout.ItemsSourceProperty);

    /// <summary>Bindable property for <see cref="ItemTemplate"/>.</summary>
    public static readonly BindableProperty ItemTemplateProperty = ForwardedToItems(SkUiVirtualVerticalStackLayout.ItemTemplateProperty);

    /// <summary>Bindable property for <see cref="ItemSpacing"/>.</summary>
    public static readonly BindableProperty ItemSpacingProperty = ForwardedToItems(SkUiVirtualVerticalStackLayoutBase.SpacingProperty, nameof(ItemSpacing), SkUiValidate.NonNegative);

    /// <summary>Bindable property for <see cref="ItemExtent"/>.</summary>
    public static readonly BindableProperty ItemExtentProperty = ForwardedToItems(SkUiVirtualVerticalStackLayoutBase.ItemExtentProperty, validate: SkUiValidate.NonNegative);

    /// <summary>Bindable property for <see cref="EstimatedItemSize"/>.</summary>
    public static readonly BindableProperty EstimatedItemSizeProperty = ForwardedToItems(SkUiVirtualVerticalStackLayoutBase.EstimatedItemSizeProperty, validate: SkUiValidate.NonNegative);

    /// <summary>Bindable property for <see cref="RemainingItemsThreshold"/>.</summary>
    public static readonly BindableProperty RemainingItemsThresholdProperty = ForwardedToItems(SkUiVirtualVerticalStackLayoutBase.RemainingItemsThresholdProperty,
        validate: SkUiVirtualVerticalStackLayoutBase.IsValidThreshold);

    /// <summary>Bindable property for <see cref="RemainingItemsThresholdReachedCommand"/>.</summary>
    public static readonly BindableProperty RemainingItemsThresholdReachedCommandProperty = ForwardedToItems(SkUiVirtualVerticalStackLayoutBase.RemainingItemsThresholdReachedCommandProperty);

    /// <summary>Bindable property for <see cref="RemainingItemsThresholdReachedCommandParameter"/>.</summary>
    public static readonly BindableProperty RemainingItemsThresholdReachedCommandParameterProperty = ForwardedToItems(SkUiVirtualVerticalStackLayoutBase.RemainingItemsThresholdReachedCommandParameterProperty);

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayout.ItemsSource" />
    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayout.ItemTemplate" />
    public DataTemplate? ItemTemplate { get => (DataTemplate?)GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }

    /// <summary>Gap between items in DIPs (not between the header or footer and the items).</summary>
    public double ItemSpacing { get => (double)GetValue(ItemSpacingProperty); set => SetValue(ItemSpacingProperty, value); }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.ItemExtent" />
    public double ItemExtent { get => (double)GetValue(ItemExtentProperty); set => SetValue(ItemExtentProperty, value); }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.EstimatedItemSize" />
    public double EstimatedItemSize { get => (double)GetValue(EstimatedItemSizeProperty); set => SetValue(EstimatedItemSizeProperty, value); }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.RemainingItemsThreshold" />
    public int RemainingItemsThreshold { get => (int)GetValue(RemainingItemsThresholdProperty); set => SetValue(RemainingItemsThresholdProperty, value); }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.RemainingItemsThresholdReachedCommand" />
    public ICommand? RemainingItemsThresholdReachedCommand
    {
        get => (ICommand?)GetValue(RemainingItemsThresholdReachedCommandProperty);
        set => SetValue(RemainingItemsThresholdReachedCommandProperty, value);
    }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.RemainingItemsThresholdReachedCommandParameter" />
    public object? RemainingItemsThresholdReachedCommandParameter
    {
        get => GetValue(RemainingItemsThresholdReachedCommandParameterProperty);
        set => SetValue(RemainingItemsThresholdReachedCommandParameterProperty, value);
    }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.RemainingItemsThresholdReached" />
    public event EventHandler? RemainingItemsThresholdReached { add => _items.RemainingItemsThresholdReached += value; remove => _items.RemainingItemsThresholdReached -= value; }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.ItemCount" />
    public int ItemCount => _items.ItemCount;

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.FirstVisibleIndex" />
    public int FirstVisibleIndex => _items.FirstVisibleIndex;

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.LastVisibleIndex" />
    public int LastVisibleIndex => _items.LastVisibleIndex;

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.VisibleRangeChanged" />
    public event EventHandler<SkUiVisibleRangeChangedEventArgs>? VisibleRangeChanged { add => _items.VisibleRangeChanged += value; remove => _items.VisibleRangeChanged -= value; }

    /// <summary>The view the item template created for the item at <paramref name="index"/>, while it is realized; else <c>null</c>.</summary>
    public ISkUiView? GetItemView(int index) => (_items.GetRealizedView(index) as ItemHost)?.Content;

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.RemeasureItem" />
    public void RemeasureItem(int index) => _items.RemeasureItem(index);

    /// <summary>
    /// Scrolls so the item at <paramref name="index"/> shows at <paramref name="position"/>
    /// (<see cref="ScrollToPosition.MakeVisible"/> scrolls only when it is not fully visible), animated on the render thread or
    /// at once; see <see cref="SkUiVirtualVerticalStackLayoutBase.ScrollToIndex"/>. With a sticky header, the item lands
    /// below it (the header is not over the list).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not an item, or <paramref name="position"/> is not defined.</exception>
    public Task ScrollToIndex(int index, ScrollToPosition position = ScrollToPosition.MakeVisible, bool animated = true) =>
        _items.ScrollToIndex(index, position, animated);

    /// <summary>Scrolls to <paramref name="item"/> (found with <see cref="object.Equals(object?)"/>); see <see cref="ScrollToIndex"/>. An item that is not in the list is ignored.</summary>
    public Task ScrollToItem(object? item, ScrollToPosition position = ScrollToPosition.MakeVisible, bool animated = true)
    {
        var index = _items.IndexOfItem(item);
        return index < 0 ? Task.CompletedTask : _items.ScrollToIndex(index, position, animated);
    }

    /// <summary>Sets <see cref="ItemsSource"/> (same as the property setter).</summary>
    public SkUiCollectionView SetItemsSource(IEnumerable? value) { ItemsSource = value; return this; }

    /// <summary>Sets <see cref="ItemTemplate"/> (same as the property setter).</summary>
    public SkUiCollectionView SetItemTemplate(DataTemplate? value) { ItemTemplate = value; return this; }

    #endregion

    #region Scrolling

    /// <summary>Bindable property for <see cref="VerticalScrollBarVisibility"/>.</summary>
    public static readonly BindableProperty VerticalScrollBarVisibilityProperty = BindableProperty.Create(nameof(VerticalScrollBarVisibility),
        typeof(ScrollBarVisibility), typeof(SkUiCollectionView), ScrollBarVisibility.Default,
        validateValue: (_, value) => Enum.IsDefined((ScrollBarVisibility)value),
        propertyChanged: (view, _, value) => ((SkUiCollectionView)view)._scroller.VerticalScrollBarVisibility = (ScrollBarVisibility)value);

    /// <summary>Bindable property for <see cref="Overscroll"/>.</summary>
    public static readonly BindableProperty OverscrollProperty = BindableProperty.Create(nameof(Overscroll), typeof(SkUiOverscrollMode),
        typeof(SkUiCollectionView), SkUiOverscrollMode.Default,
        validateValue: (_, value) => Enum.IsDefined((SkUiOverscrollMode)value),
        propertyChanged: (view, _, value) => ((SkUiCollectionView)view)._scroller.Overscroll = (SkUiOverscrollMode)value);

    /// <inheritdoc cref="SkUiScrollView.VerticalScrollBarVisibility" />
    public ScrollBarVisibility VerticalScrollBarVisibility
    {
        get => (ScrollBarVisibility)GetValue(VerticalScrollBarVisibilityProperty);
        set => SetValue(VerticalScrollBarVisibilityProperty, value);
    }

    /// <inheritdoc cref="SkUiScrollView.Overscroll" />
    public SkUiOverscrollMode Overscroll { get => (SkUiOverscrollMode)GetValue(OverscrollProperty); set => SetValue(OverscrollProperty, value); }

    /// <summary>The scroll offset in DIPs (the header, when not sticky, scrolls with the items).</summary>
    public double ScrollY => _scroller.ScrollY;

    /// <summary>True while the user drags the list or a fling / animated scroll runs.</summary>
    public bool IsScrolling => _scroller.IsScrolling;

    /// <summary>Raised after the scroll offset changes (<see cref="ScrolledEventArgs.ScrollY"/>).</summary>
    public event EventHandler<ScrolledEventArgs>? Scrolled;

    /// <summary>Scrolls to an offset in DIPs, animated on the render thread or at once (a drag or another scroll cancels the task).</summary>
    public Task ScrollToAsync(double offset, bool animated = true) => _scroller.ScrollToAsync(0, offset, animated);

    #endregion

    #region Header, footer, empty view

    private static BindableProperty ViewProperty(string name, Action<SkUiCollectionView> changed) => BindableProperty.Create(
        name, typeof(ISkUiView), typeof(SkUiCollectionView), null,
        validateValue: (bindable, value) =>
        {
            var owner = (SkUiCollectionView)bindable;
            if (value is ISkUiView child && !ReferenceEquals(owner.GetValue(owner.PropertyNamed(name)), child))
                owner.ValidateChild(child);
            return true;
        },
        propertyChanged: (bindable, _, _) => changed((SkUiCollectionView)bindable));

    private BindableProperty PropertyNamed(string name) => name switch
    {
        nameof(Header) => HeaderProperty,
        nameof(Footer) => FooterProperty,
        _ => EmptyViewProperty
    };

    /// <summary>Bindable property for <see cref="Header"/>.</summary>
    public static readonly BindableProperty HeaderProperty = ViewProperty(nameof(Header), view => view.OnHeaderChanged());

    /// <summary>Bindable property for <see cref="HeaderTemplate"/>.</summary>
    public static readonly BindableProperty HeaderTemplateProperty = BindableProperty.Create(nameof(HeaderTemplate), typeof(DataTemplate), typeof(SkUiCollectionView), null,
        propertyChanged: (bindable, _, _) => ((SkUiCollectionView)bindable).OnTemplateChanged(((SkUiCollectionView)bindable)._headerSlot));

    /// <summary>Bindable property for <see cref="IsStickyHeader"/>.</summary>
    public static readonly BindableProperty IsStickyHeaderProperty = BindableProperty.Create(nameof(IsStickyHeader), typeof(bool), typeof(SkUiCollectionView), false,
        propertyChanged: (bindable, _, _) => ((SkUiCollectionView)bindable).PlaceHeader());

    /// <summary>Bindable property for <see cref="Footer"/>.</summary>
    public static readonly BindableProperty FooterProperty = ViewProperty(nameof(Footer), view => view.OnFooterChanged());

    /// <summary>Bindable property for <see cref="FooterTemplate"/>.</summary>
    public static readonly BindableProperty FooterTemplateProperty = BindableProperty.Create(nameof(FooterTemplate), typeof(DataTemplate), typeof(SkUiCollectionView), null,
        propertyChanged: (bindable, _, _) => ((SkUiCollectionView)bindable).OnTemplateChanged(((SkUiCollectionView)bindable)._footerSlot));

    /// <summary>Bindable property for <see cref="IsStickyFooter"/>.</summary>
    public static readonly BindableProperty IsStickyFooterProperty = BindableProperty.Create(nameof(IsStickyFooter), typeof(bool), typeof(SkUiCollectionView), false,
        propertyChanged: (bindable, _, _) => ((SkUiCollectionView)bindable).PlaceFooter());

    /// <summary>Bindable property for <see cref="EmptyView"/>.</summary>
    public static readonly BindableProperty EmptyViewProperty = ViewProperty(nameof(EmptyView), view => view.OnEmptyViewChanged());

    /// <summary>Bindable property for <see cref="EmptyViewTemplate"/>.</summary>
    public static readonly BindableProperty EmptyViewTemplateProperty = BindableProperty.Create(nameof(EmptyViewTemplate), typeof(DataTemplate), typeof(SkUiCollectionView), null,
        propertyChanged: (bindable, _, _) => ((SkUiCollectionView)bindable).OnTemplateChanged(((SkUiCollectionView)bindable)._emptySlot));

    /// <summary>A drawn view above the items: it scrolls with them, or stays at the top with <see cref="IsStickyHeader"/>.</summary>
    public ISkUiView? Header { get => (ISkUiView?)GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }

    /// <summary>
    /// Creates <see cref="Header"/> when it is not set (drawn views; a <see cref="DataTemplateSelector"/> chooses by the
    /// binding context), once the list is in a tree. Its binding context is the list's.
    /// </summary>
    public DataTemplate? HeaderTemplate { get => (DataTemplate?)GetValue(HeaderTemplateProperty); set => SetValue(HeaderTemplateProperty, value); }

    /// <summary>
    /// Whether the header stays at the top while the items scroll (default <c>false</c>: it scrolls away with them). A sticky
    /// header is laid out above the scrolled area, so scrolling does not re-record it, and the items scroll below it.
    /// </summary>
    public bool IsStickyHeader { get => (bool)GetValue(IsStickyHeaderProperty); set => SetValue(IsStickyHeaderProperty, value); }

    /// <summary>A drawn view below the items: it scrolls with them (right after the last item), or stays at the bottom with <see cref="IsStickyFooter"/>.</summary>
    public ISkUiView? Footer { get => (ISkUiView?)GetValue(FooterProperty); set => SetValue(FooterProperty, value); }

    /// <summary>Creates <see cref="Footer"/> when it is not set; see <see cref="HeaderTemplate"/>.</summary>
    public DataTemplate? FooterTemplate { get => (DataTemplate?)GetValue(FooterTemplateProperty); set => SetValue(FooterTemplateProperty, value); }

    /// <summary>Whether the footer stays at the bottom while the items scroll; see <see cref="IsStickyHeader"/>.</summary>
    public bool IsStickyFooter { get => (bool)GetValue(IsStickyFooterProperty); set => SetValue(IsStickyFooterProperty, value); }

    /// <summary>
    /// A drawn view shown instead of the items while there are none (<see cref="ItemsSource"/> is <c>null</c> or empty),
    /// between the header and the footer; it fills the space the list has left (center its content to place it in the
    /// middle).
    /// </summary>
    public ISkUiView? EmptyView { get => (ISkUiView?)GetValue(EmptyViewProperty); set => SetValue(EmptyViewProperty, value); }

    /// <summary>Creates <see cref="EmptyView"/> when it is not set, the first time the list is empty; see <see cref="HeaderTemplate"/>.</summary>
    public DataTemplate? EmptyViewTemplate { get => (DataTemplate?)GetValue(EmptyViewTemplateProperty); set => SetValue(EmptyViewTemplateProperty, value); }

    /// <summary>Sets <see cref="Header"/> (same as the property setter).</summary>
    public SkUiCollectionView SetHeader(ISkUiView? value, bool sticky = false)
    {
        if (!ReferenceEquals(Header, value) && value is not null) ValidateChild(value);
        Header = value;
        IsStickyHeader = sticky;
        return this;
    }

    /// <summary>Sets <see cref="Footer"/> (same as the property setter).</summary>
    public SkUiCollectionView SetFooter(ISkUiView? value, bool sticky = false)
    {
        if (!ReferenceEquals(Footer, value) && value is not null) ValidateChild(value);
        Footer = value;
        IsStickyFooter = sticky;
        return this;
    }

    /// <summary>Sets <see cref="EmptyView"/> (same as the property setter).</summary>
    public SkUiCollectionView SetEmptyView(ISkUiView? value)
    {
        if (!ReferenceEquals(EmptyView, value) && value is not null) ValidateChild(value);
        EmptyView = value;
        return this;
    }

    /// <summary>Whether the list has no items (the empty view shows).</summary>
    internal bool IsEmpty => _isEmpty;

    private void OnHeaderChanged()
    {
        _headerSlot.OnContentChanged();
        if (Header is null && !_headerSlot.IsSetting && Parent is not null)
            _headerSlot.Ensure(); // cleared by the app: the template provides it again
        PlaceHeader();
    }

    private void PlaceHeader() => Place(Header, IsStickyHeader, _stickyHeaderHost, _body.HeaderHost);

    private void PlaceFooter() => Place(Footer, IsStickyFooter, _stickyFooterHost, _body.FooterHost);

    private void OnFooterChanged()
    {
        _footerSlot.OnContentChanged();
        if (Footer is null && !_footerSlot.IsSetting && Parent is not null)
            _footerSlot.Ensure();
        PlaceFooter();
    }

    /// <summary>Shows <paramref name="view"/> in the sticky or the scrolled host (taken out of the other first).</summary>
    private static void Place(ISkUiView? view, bool sticky, SkUiContentView stickyHost, SkUiContentView scrolledHost)
    {
        var (host, other) = sticky ? (stickyHost, scrolledHost) : (scrolledHost, stickyHost);
        if (!ReferenceEquals(other.Content, null))
            other.Content = null;
        if (!ReferenceEquals(host.Content, view))
            host.Content = view;
    }

    private void OnEmptyViewChanged()
    {
        _emptySlot.OnContentChanged();
        if (EmptyView is null && !_emptySlot.IsSetting && _isEmpty && Parent is not null)
            _emptySlot.Ensure();
        _body.EmptyHost.Content = EmptyView;
    }

    private void OnTemplateChanged(SkUiContentSlot slot)
    {
        slot.OnTemplateChanged();
        EnsureTemplateContent();
    }

    /// <summary>Creates template content where it is needed (once in a tree, so every property set before applies first).</summary>
    private void EnsureTemplateContent()
    {
        if (Parent is null)
            return;
        _headerSlot.Ensure();
        _footerSlot.Ensure();
        if (_isEmpty)
            _emptySlot.Ensure();
    }

    /// <inheritdoc />
    protected override void OnParentSet()
    {
        base.OnParentSet();
        EnsureTemplateContent();
    }

    /// <inheritdoc />
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        var reselected = _headerSlot.Reselect() | _footerSlot.Reselect() | _emptySlot.Reselect();
        if (reselected)
            EnsureTemplateContent();
    }

    private void UpdateEmpty()
    {
        var empty = _items.ItemCount == 0;
        if (empty == _isEmpty && _body.EmptyHost.IsVisible == empty)
            return;
        _isEmpty = empty;
        _body.EmptyHost.IsVisible = empty;
        if (empty && Parent is not null)
            _emptySlot.Ensure();
        _body.InvalidateBody();
    }

    #endregion

    #region Selection and taps

    /// <summary>Bindable property for <see cref="SelectionMode"/>.</summary>
    public static readonly BindableProperty SelectionModeProperty = BindableProperty.Create(nameof(SelectionMode), typeof(SkUiSelectionMode),
        typeof(SkUiCollectionView), SkUiSelectionMode.None,
        validateValue: (_, value) => Enum.IsDefined((SkUiSelectionMode)value),
        propertyChanged: (bindable, _, value) => ((SkUiCollectionView)bindable).OnSelectionModeChanged((SkUiSelectionMode)value));

    /// <summary>Bindable property for <see cref="SelectedItem"/> (two-way by default: taps change it).</summary>
    public static readonly BindableProperty SelectedItemProperty = BindableProperty.Create(nameof(SelectedItem), typeof(object), typeof(SkUiCollectionView), null,
        BindingMode.TwoWay,
        propertyChanged: (bindable, oldValue, newValue) => ((SkUiCollectionView)bindable).OnSelectedItemChanged(oldValue, newValue));

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
    /// How taps select items: <see cref="SkUiSelectionMode.None"/> (default), <see cref="SkUiSelectionMode.Single"/>, or
    /// <see cref="SkUiSelectionMode.SingleDeselect"/> (tapping the selected item clears the selection). Changing it to
    /// <see cref="SkUiSelectionMode.None"/> clears <see cref="SelectedItem"/>.
    /// </summary>
    public SkUiSelectionMode SelectionMode { get => (SkUiSelectionMode)GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }

    /// <summary>
    /// The selected item (an item of <see cref="ItemsSource"/>, compared with <see cref="object.Equals(object?)"/>), or
    /// <c>null</c>. Its view's root goes to the <c>Selected</c> visual state (<c>CommonStates</c>) and its container draws
    /// <see cref="SelectionBackground"/>. Shown only while <see cref="SelectionMode"/> is not <see cref="SkUiSelectionMode.None"/>.
    /// Removing the item from the source clears it.
    /// </summary>
    public object? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }

    /// <summary>Runs with <see cref="SelectionChangedCommandParameter"/> after <see cref="SelectedItem"/> changes (when it can execute), before <see cref="SelectionChanged"/>.</summary>
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
    /// Drawn behind the selected item's view (in XAML a color, e.g. <c>"#1F0A84FF"</c>, or a gradient); <c>null</c> (default):
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
    /// taps only (setting <see cref="SelectedItem"/> is the app's own decision).
    /// </summary>
    public event EventHandler<SkUiSelectionChangingEventArgs>? SelectionChanging;

    /// <summary><see cref="SelectedItem"/> changed (by a tap, by the app, or because the item was removed), after <see cref="SelectionChangedCommand"/>.</summary>
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

    /// <summary>Whether item containers take taps.</summary>
    private bool WantsItemTaps => _selectionMode != SkUiSelectionMode.None || ItemTapped is not null || ItemTappedCommand is not null;

    private bool ShowsSelection(object? item) => _selectionMode != SkUiSelectionMode.None && _selectedItem is not null && Equals(item, _selectedItem);

    private void OnSelectionModeChanged(SkUiSelectionMode value)
    {
        _selectionMode = value;
        if (value == SkUiSelectionMode.None && _selectedItem is not null)
            SelectedItem = null;
        else
            RefreshSelection();
    }

    private void OnSelectedItemChanged(object? oldValue, object? newValue)
    {
        _selectedItem = newValue;
        RefreshSelection();
        if (SelectionChangedCommand is { } command && command.CanExecute(SelectionChangedCommandParameter))
            command.Execute(SelectionChangedCommandParameter);
        SelectionChanged?.Invoke(this, new SkUiSelectionChangedEventArgs(oldValue, newValue));
    }

    /// <summary>Shows the selection on the realized items (only items whose state changes re-record).</summary>
    private void RefreshSelection()
    {
        foreach (var view in _items.RealizedViews)
            if (view is ItemHost host)
                host.IsSelected = ShowsSelection(((BindableObject)host).BindingContext);
    }

    private void OnSelectionBackgroundChanged(Brush? value)
    {
        _selectionBackground = value;
        foreach (var view in _items.RealizedViews)
            if (view is ItemHost { IsSelected: true } host)
                host.InvalidatePaint();
    }

    private void OnShowsItemPressEffectChanged(bool value)
    {
        foreach (var view in _items.RealizedViews.Concat(_items.RecycledViews))
            ((SkUiView)view).ShowsPressEffect = value;
    }

    private void OnItemBound(ItemHost host, object? item) => host.IsSelected = ShowsSelection(item);

    /// <summary>A tap on an item: the selection first (cancelable), then <see cref="ItemTapped"/> and its command.</summary>
    private void OnItemHostTapped(ItemHost host)
    {
        var index = _items.IndexOfRealizedView(host);
        if (index < 0)
            return;
        var item = _items.ItemAt(index);
        if (_selectionMode != SkUiSelectionMode.None)
        {
            var selected = _selectionMode == SkUiSelectionMode.SingleDeselect && Equals(item, _selectedItem) ? null : item;
            if (!Equals(selected, _selectedItem))
            {
                var args = new SkUiSelectionChangingEventArgs(_selectedItem, selected);
                SelectionChanging?.Invoke(this, args);
                if (!args.Cancel)
                    SetValue(SelectedItemProperty, selected);
            }
        }
        ItemTapped?.Invoke(this, new SkUiItemTappedEventArgs(item, index));
        if (ItemTappedCommand is { } command)
        {
            var parameter = IsSet(ItemTappedCommandParameterProperty) ? ItemTappedCommandParameter : item;
            if (command.CanExecute(parameter))
                command.Execute(parameter);
        }
    }

    /// <summary>The items changed: the empty view, and the selection when its item left the source.</summary>
    private void OnSourceChanged(NotifyCollectionChangedEventArgs? args)
    {
        UpdateEmpty();
        if (_selectedItem is not { } selected)
            return;
        var check = args is null || args.Action == NotifyCollectionChangedAction.Reset
            || (args.Action is NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Replace
                && args.OldItems is { } removed && removed.Cast<object?>().Any(item => Equals(item, selected)));
        if (check && _items.IndexOfItem(selected) < 0)
            SelectedItem = null;
    }

    #endregion

    #region Pull to refresh

    /// <summary>Bindable property for <see cref="IsPullToRefreshEnabled"/>.</summary>
    public static readonly BindableProperty IsPullToRefreshEnabledProperty = BindableProperty.Create(nameof(IsPullToRefreshEnabled), typeof(bool), typeof(SkUiCollectionView), false,
        propertyChanged: (bindable, _, value) => ((SkUiCollectionView)bindable).Controller.PullsAtVerticalStart = (bool)value);

    /// <summary>Bindable property for <see cref="IsRefreshing"/> (two-way by default: a pull sets it).</summary>
    public static readonly BindableProperty IsRefreshingProperty = BindableProperty.Create(nameof(IsRefreshing), typeof(bool), typeof(SkUiCollectionView), false,
        BindingMode.TwoWay,
        propertyChanged: (bindable, _, value) => ((SkUiCollectionView)bindable).OnIsRefreshingChanged((bool)value));

    /// <summary>Bindable property for <see cref="RefreshCommand"/>.</summary>
    public static readonly BindableProperty RefreshCommandProperty = BindableProperty.Create(nameof(RefreshCommand), typeof(ICommand), typeof(SkUiCollectionView));

    /// <summary>Bindable property for <see cref="RefreshCommandParameter"/>.</summary>
    public static readonly BindableProperty RefreshCommandParameterProperty = BindableProperty.Create(nameof(RefreshCommandParameter), typeof(object), typeof(SkUiCollectionView));

    /// <summary>Bindable property for <see cref="RefreshColor"/>.</summary>
    public static readonly BindableProperty RefreshColorProperty = BindableProperty.Create(nameof(RefreshColor), typeof(Color), typeof(SkUiCollectionView), null,
        propertyChanged: (bindable, _, value) => ((SkUiCollectionView)bindable)._refreshLayer.Spinner.SetColor((Color?)value));

    /// <summary>
    /// Whether pulling the top of the list down and releasing it starts a refresh (default <c>false</c>). The pull works
    /// also when the items do not fill the list and when overscroll is off: a drawn indicator comes down from the top with the
    /// pull, and a release past it (64 DIPs) sets <see cref="IsRefreshing"/>.
    /// </summary>
    public bool IsPullToRefreshEnabled { get => (bool)GetValue(IsPullToRefreshEnabledProperty); set => SetValue(IsPullToRefreshEnabledProperty, value); }

    /// <summary>
    /// Whether a refresh runs: the indicator spins at the top. Set to <c>true</c> (by a pull or by the app, as MAUI's
    /// <c>RefreshView</c>) it raises <see cref="Refreshing"/> and runs <see cref="RefreshCommand"/>; set it back to
    /// <c>false</c> when the refresh is done.
    /// </summary>
    public bool IsRefreshing { get => (bool)GetValue(IsRefreshingProperty); set => SetValue(IsRefreshingProperty, value); }

    /// <summary>Runs with <see cref="RefreshCommandParameter"/> when <see cref="IsRefreshing"/> becomes <c>true</c> (when it can execute).</summary>
    public ICommand? RefreshCommand { get => (ICommand?)GetValue(RefreshCommandProperty); set => SetValue(RefreshCommandProperty, value); }

    /// <summary>The parameter of <see cref="RefreshCommand"/>.</summary>
    public object? RefreshCommandParameter { get => GetValue(RefreshCommandParameterProperty); set => SetValue(RefreshCommandParameterProperty, value); }

    /// <summary>The refresh indicator's color; <c>null</c> (default): the accent color.</summary>
    public Color? RefreshColor { get => (Color?)GetValue(RefreshColorProperty); set => SetValue(RefreshColorProperty, value); }

    /// <summary>Raised when <see cref="IsRefreshing"/> becomes <c>true</c>, before <see cref="RefreshCommand"/>.</summary>
    public event EventHandler? Refreshing;

    /// <summary>The refresh indicator (tests).</summary>
    internal SkUiView RefreshIndicator => _refreshLayer.Spinner;

    private void OnPullReleased(double distance)
    {
        if (IsPullToRefreshEnabled && IsEnabled && !IsRefreshing && distance >= RefreshTriggerDistance)
            IsRefreshing = true;
    }

    private void OnOverscrollChanged()
    {
        if (!IsRefreshing)
            _refreshLayer.ShowPull(IsPullToRefreshEnabled ? Math.Max(0, -Controller.OverscrollY) : 0);
    }

    private void OnIsRefreshingChanged(bool value)
    {
        if (!value)
        {
            // Done: the indicator goes (or follows a pull still in progress).
            OnOverscrollChanged();
            return;
        }
        _refreshLayer.ShowRefreshing();
        Refreshing?.Invoke(this, EventArgs.Empty);
        if (RefreshCommand is { } command && command.CanExecute(RefreshCommandParameter))
            command.Execute(RefreshCommandParameter);
    }

    #endregion

    #region Layout

    /// <summary>The scroller of the header, items, empty view and footer (tests).</summary>
    internal SkUiScrollView ScrollView => _scroller;

    /// <summary>The virtual layout of the items (tests).</summary>
    internal SkUiVirtualVerticalStackLayout ItemsLayout => _items;

    /// <inheritdoc />
    internal override IEnumerable<ISkUiView> SkiaChildren
    {
        get
        {
            // Screen order (focus, semantics); the refresh indicator draws over the list.
            yield return _stickyHeaderHost;
            yield return _scroller;
            yield return _stickyFooterHost;
            yield return _refreshLayer;
        }
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var header = ((IView)_stickyHeaderHost).Measure(widthConstraint, double.PositiveInfinity);
        var footer = ((IView)_stickyFooterHost).Measure(widthConstraint, double.PositiveInfinity);
        var list = ((IView)_scroller).Measure(widthConstraint, Math.Max(0, heightConstraint - header.Height - footer.Height));
        ((IView)_refreshLayer).Measure(widthConstraint, double.PositiveInfinity);
        return new Size(Math.Max(list.Width, Math.Max(header.Width, footer.Width)), header.Height + list.Height + footer.Height);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        var header = ((IView)_stickyHeaderHost).DesiredSize.Height;
        var footer = ((IView)_stickyFooterHost).DesiredSize.Height;
        var list = Math.Max(0, size.Height - header - footer);
        ((IView)_stickyHeaderHost).Arrange(new Rect(0, 0, size.Width, header));
        ((IView)_scroller).Arrange(new Rect(0, header, size.Width, list));
        ((IView)_stickyFooterHost).Arrange(new Rect(0, header + list, size.Width, footer));
        // Over the scrolled area only: the indicator comes down from its top edge, clipped there.
        ((IView)_refreshLayer).Arrange(new Rect(0, header, size.Width, list));
    }

    #endregion

    /// <summary>The items: a virtual stack whose realized views are item containers.</summary>
    private sealed class ItemsPart(SkUiCollectionView owner) : SkUiVirtualVerticalStackLayout
    {
        internal override ISkUiView WrapItemView(ISkUiView content) =>
            new ItemHost(owner) { Content = content, ShowsPressEffect = owner.ShowsItemPressEffect };

        internal override ISkUiView UnwrapItemView(ISkUiView view) => ((ItemHost)view).Content!;

        internal override void OnItemBound(int index, ISkUiView view, object? item)
        {
            if (view is ItemHost host)
                owner.OnItemBound(host, item);
        }
    }

    /// <summary>Hosts an item's view: takes its taps, draws the selection background, and puts its root in the <c>Selected</c> state.</summary>
    private sealed class ItemHost(SkUiCollectionView owner) : SkUiContentView
    {
        private bool _selected;

        public bool IsSelected
        {
            get => _selected;
            set
            {
                if (_selected == value)
                    return;
                _selected = value;
                if (Content is SkUiView root)
                    root.IsSelectedItem = value;
                InvalidatePaint();
                InvalidateSemantics();
            }
        }

        protected override bool HandlesTap => owner.WantsItemTaps;

        protected override void OnTapped(SkUiTappedEventArgs args)
        {
            RaiseTapped(args);
            owner.OnItemHostTapped(this);
        }

        protected override void OnPaintBackground(SKCanvas canvas)
        {
            base.OnPaintBackground(canvas);
            if (!_selected)
                return;
            var rect = new SKRect(0, 0, (float)Width, (float)Height);
            if (owner._selectionBackground is { } brush)
                SkUiShapePainter.FillRect(canvas, rect, (Paint?)brush);
            else
                SkUiShapePainter.FillRect(canvas, rect, SkUiFill.From(SkUiColors.Accent.WithAlpha(0.12f)));
        }

        protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
        {
            base.OnPopulateSemantics(info);
            if (_selected)
                info.Value = SelectedStateText();
        }
    }

    /// <summary>The content of the scroller: the header, the items (or the empty view), the footer.</summary>
    private sealed class BodyPart : SkUiView
    {
        private readonly SkUiCollectionView _owner;

        public BodyPart(SkUiCollectionView owner)
        {
            _owner = owner;
            ClipToBounds = false; // items may draw past their slots (shadows, press scale)
            EmptyHost.IsVisible = false;
            AttachChild(HeaderHost);
            AttachChild(owner._items);
            AttachChild(EmptyHost);
            AttachChild(FooterHost);
        }

        public SkUiContentView HeaderHost { get; } = new();

        public SkUiContentView EmptyHost { get; } = new();

        public SkUiContentView FooterHost { get; } = new();

        public void InvalidateBody() => InvalidateMeasureOverride();

        internal override IEnumerable<ISkUiView> SkiaChildren
        {
            get
            {
                yield return HeaderHost;
                yield return _owner._items;
                yield return EmptyHost;
                yield return FooterHost;
            }
        }

        protected override Size MeasureContent(double widthConstraint, double heightConstraint)
        {
            var header = ((IView)HeaderHost).Measure(widthConstraint, double.PositiveInfinity);
            var items = ((IView)_owner._items).Measure(widthConstraint, double.PositiveInfinity);
            var empty = EmptyHost.IsVisible ? ((IView)EmptyHost).Measure(widthConstraint, double.PositiveInfinity) : Size.Zero;
            var footer = ((IView)FooterHost).Measure(widthConstraint, double.PositiveInfinity);
            return new Size(Math.Max(Math.Max(header.Width, items.Width), Math.Max(empty.Width, footer.Width)),
                header.Height + items.Height + empty.Height + footer.Height);
        }

        protected override void ArrangeContent(Size size)
        {
            var header = ((IView)HeaderHost).DesiredSize.Height;
            var items = ((IView)_owner._items).DesiredSize.Height;
            var footer = ((IView)FooterHost).DesiredSize.Height;
            ((IView)HeaderHost).Arrange(new Rect(0, 0, size.Width, header));
            ((IView)_owner._items).Arrange(new Rect(0, header, size.Width, items));
            var y = header + items;
            if (EmptyHost.IsVisible)
            {
                // The empty view fills what the viewport has left between the header and the footer.
                var empty = Math.Max(((IView)EmptyHost).DesiredSize.Height, size.Height - header - items - footer);
                ((IView)EmptyHost).Arrange(new Rect(0, y, size.Width, empty));
                y += empty;
            }
            ((IView)FooterHost).Arrange(new Rect(0, y, size.Width, footer));
        }
    }

    /// <summary>The area over the scrolled list where the refresh indicator comes down (input passes through).</summary>
    private sealed class RefreshLayer : SkUiView
    {
        public RefreshLayer()
        {
            InputTransparent = true;
            ClipToBounds = true;
            AttachChild(Spinner);
        }

        public RefreshSpinner Spinner { get; } = new();

        internal override IEnumerable<ISkUiView> SkiaChildren { get { yield return Spinner; } }

        protected override Size MeasureContent(double widthConstraint, double heightConstraint)
        {
            ((IView)Spinner).Measure(RefreshSpinner.Size, RefreshSpinner.Size);
            return Size.Zero;
        }

        protected override void ArrangeContent(Size size) =>
            ((IView)Spinner).Arrange(new Rect((size.Width - RefreshSpinner.Size) / 2, 0, RefreshSpinner.Size, RefreshSpinner.Size));

        /// <summary>Follows a pull shown <paramref name="pull"/> DIPs past the top (composite-time: no re-record while dragging).</summary>
        public void ShowPull(double pull)
        {
            var progress = Math.Clamp(pull / RefreshTriggerDistance, 0, 1);
            Spinner.IsSpinning = false;
            Spinner.Opacity = progress;
            Spinner.Rotation = progress * 270;
            Spinner.TranslationY = Math.Min(pull, RefreshTriggerDistance * 1.5) - RefreshSpinner.Size;
        }

        /// <summary>Shows the indicator spinning at its place below the top.</summary>
        public void ShowRefreshing()
        {
            Spinner.Opacity = 1;
            Spinner.Rotation = 0;
            Spinner.TranslationY = RefreshTriggerDistance - RefreshSpinner.Size;
            Spinner.IsSpinning = true;
        }
    }

    /// <summary>A round badge with the look's spinner arc; spun by the compositor while refreshing.</summary>
    private sealed class RefreshSpinner : SkUiView
    {
        public const double Size = 40;

        private bool _spinning;
        private Color? _color;
        private SKPaint? _arc;

        public RefreshSpinner()
        {
            Opacity = 0;
            TranslationY = -Size;
            Shadow = new Shadow { Brush = Colors.Black, Opacity = 0.25f, Radius = 4, Offset = new Point(0, 1) };
        }

        public bool IsSpinning
        {
            get => _spinning;
            set
            {
                if (_spinning == value)
                    return;
                _spinning = value;
                InvalidateRender(SkUiRenderDirty.Props);
            }
        }

        public void SetColor(Color? value)
        {
            _color = value;
            InvalidatePaint();
        }

        protected override Size MeasureContent(double widthConstraint, double heightConstraint) => new(Size, Size);

        protected override void OnPaintContent(SKCanvas canvas)
        {
            var size = (float)Size;
            using (var fill = new SKPaint { IsAntialias = true, Color = ToSkColor(SkUiColors.DefaultBackground) })
                canvas.DrawCircle(size / 2, size / 2, size / 2, fill);
            var arc = _arc ??= new SKPaint { Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, IsAntialias = true };
            arc.Color = ToSkColor(_color ?? SkUiColors.Accent);
            const float inset = 10;
            canvas.Save();
            canvas.Translate(inset, inset);
            SkUiLook.Current.DrawActivityIndicator(canvas, size - 2 * inset, size - 2 * inset, 0, arc);
            canvas.Restore();
        }

        internal override SKPath? CreateShadowOutline(float width, float height)
        {
            using var builder = new SKPathBuilder();
            builder.AddOval(new SKRect(0, 0, width, height));
            return builder.Detach();
        }

        internal override void OnGetRenderProps(ref SkUiRenderProps props) => props.ContentSpinPeriod = _spinning ? 1 : 0;
    }
}
