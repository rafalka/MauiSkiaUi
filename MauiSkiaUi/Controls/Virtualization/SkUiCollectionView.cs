using System.Collections;
using System.Windows.Input;
using MauiSkiaUi.Core;
using MauiSkiaUi.Rendering;

namespace MauiSkiaUi;

/// <summary>
/// A drawn, virtualized list or grid with selection, item taps, groups (collapsible, with sticky group headers), a header
/// and footer (scrolled or sticky), an empty view, loading more and pull-to-refresh (FR-22). Its rows are created on demand
/// and recycled per template by the FR-21 engine (<see cref="SkUiVirtualVerticalStackLayoutBase"/>), inside a scroller of
/// its own, vertical or horizontal; every row may have its own size. In XAML the element's content is the
/// <see cref="ItemTemplate"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>SkiaUi's own API, not MAUI's <c>CollectionView</c>.</b> Familiar names are kept where they fit
/// (<c>ItemsSource</c>, <c>ItemTemplate</c>, <c>SelectedItem</c>, <c>SelectedItems</c>, <c>IsGrouped</c>,
/// <c>RemainingItemsThreshold</c>); the mapping from MAUI is in the migration guide.
/// </para>
/// <para>
/// <b>Rows.</b> The engine lays out rows: an item, a grid row of up to <see cref="Span"/> items (as tall as its tallest
/// item), a group header or a group footer. Indices in this API (<see cref="ItemCount"/>, <see cref="FirstVisibleIndex"/>,
/// <see cref="ScrollToIndex"/>, <see cref="SkUiItemTappedEventArgs.Index"/>) count items only, across the groups, collapsed
/// groups included; <see cref="RemainingItemsThreshold"/> counts rows.
/// </para>
/// <para>
/// <b>Cost.</b> Sticky parts (header, footer, the current group's header) are drawn over the scroller, not in it, so
/// scrolling does not re-record them (the sticky group header is rebound when the group changes). A selection change
/// re-records the items whose state changed. Each item view is hosted in a drawn item container that takes the item's taps
/// (tappable views inside the item keep theirs) and draws <see cref="SelectionBackground"/>.
/// </para>
/// <para>
/// The list needs a bounded size along its axis (a grid row, a page): measured unbounded (inside a stack or a scroll view
/// of the same axis) it is as long as all its rows, and creates every one.
/// </para>
/// </remarks>
[ContentProperty(nameof(ItemTemplate))]
public partial class SkUiCollectionView : SkUiView, ISkUiItemsView, ISkUiRefreshOwner
{
    private readonly ItemsPart _items;
    private readonly ItemsModel _model;
    private readonly SkUiScrollView _scroller;
    private readonly BodyPart _body;
    // Created the first time they show a view: most lists have no header or footer, fewer have sticky ones.
    private StickyHost? _stickyHeaderHost;
    private StickyHost? _stickyFooterHost;
    private readonly SkUiPullToRefresh _refresh;
    private readonly SkUiRefreshLayer _refreshLayer;
    private readonly SkUiContentSlot _headerSlot;
    private readonly SkUiContentSlot _footerSlot;
    private readonly SkUiContentSlot _emptySlot;
    private DataTemplate? _itemTemplate;
    private bool _horizontal;
    private double _spanSpacing;
    private bool _isEmpty;
    private int _firstVisible = -1;
    private int _lastVisible = -1;

    /// <summary>Creates an empty list (GPU-backed when it is a surface of its own).</summary>
    public SkUiCollectionView()
    {
        HwAccelerated = true;
        _headerSlot = new SkUiContentSlot(this, HeaderProperty, HeaderTemplateProperty);
        _footerSlot = new SkUiContentSlot(this, FooterProperty, FooterTemplateProperty);
        _emptySlot = new SkUiContentSlot(this, EmptyViewProperty, EmptyViewTemplateProperty);
        _items = new ItemsPart(this);
        _items.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(FirstVisibleIndex) or nameof(LastVisibleIndex))
                OnVisibleRowsChanged();
        };
        _items.ScrollTargetInset = StickyGroupInset;
        _model = new ItemsModel(this);
        _body = new BodyPart(this);
        _scroller = new SkUiScrollView { Content = _body };
        _scroller.Scrolled += OnScrollerScrolled;
        _scroller.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(IsScrolling))
                OnPropertyChanged(nameof(IsScrolling));
        };
        _refresh = new SkUiPullToRefresh(this);
        _refresh.PullThrough(Controller);
        _refreshLayer = _refresh.Layer;
        AttachChild(_scroller);
        AttachChild(_refreshLayer);
        UpdateEmpty();
    }

    private SkUiScrollController Controller => ((ISkUiScrollHost)_scroller).Scroller;

    #region Items

    /// <summary>A bindable property of this view that sets the same property of the items layout.</summary>
    private static BindableProperty ForwardedToItems(BindableProperty itemsProperty, BindableProperty.ValidateValueDelegate? validate = null) =>
        BindableProperty.Create(itemsProperty.PropertyName, itemsProperty.ReturnType, typeof(SkUiCollectionView), itemsProperty.DefaultValue,
            validateValue: validate,
            propertyChanged: (view, _, value) => ((SkUiCollectionView)view)._items.SetValue(itemsProperty, value));

    /// <summary>Bindable property for <see cref="ItemsSource"/>.</summary>
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable), typeof(SkUiCollectionView), null,
        propertyChanged: (view, _, value) => ((SkUiCollectionView)view).OnItemsSourceChanged((IEnumerable?)value));

    /// <summary>Bindable property for <see cref="ItemTemplate"/>.</summary>
    public static readonly BindableProperty ItemTemplateProperty = BindableProperty.Create(nameof(ItemTemplate), typeof(DataTemplate), typeof(SkUiCollectionView), null,
        propertyChanged: (view, _, value) => ((SkUiCollectionView)view).OnItemTemplateChanged((DataTemplate?)value));

    /// <summary>Bindable property for <see cref="ItemSpacing"/>.</summary>
    public static readonly BindableProperty ItemSpacingProperty = ForwardedToItems(SkUiVirtualVerticalStackLayoutBase.ItemSpacingProperty, SkUiValidate.NonNegative);

    /// <summary>Bindable property for <see cref="ItemExtent"/>.</summary>
    public static readonly BindableProperty ItemExtentProperty = ForwardedToItems(SkUiVirtualVerticalStackLayoutBase.ItemExtentProperty, SkUiValidate.NonNegative);

    /// <summary>Bindable property for <see cref="EstimatedItemSize"/>.</summary>
    public static readonly BindableProperty EstimatedItemSizeProperty = ForwardedToItems(SkUiVirtualVerticalStackLayoutBase.EstimatedItemSizeProperty, SkUiValidate.NonNegative);

    /// <summary>Bindable property for <see cref="PrefetchFactor"/>.</summary>
    public static readonly BindableProperty PrefetchFactorProperty = ForwardedToItems(SkUiVirtualVerticalStackLayoutBase.PrefetchFactorProperty, SkUiValidate.NonNegative);

    /// <summary>Bindable property for <see cref="PrefetchBehindFactor"/>.</summary>
    public static readonly BindableProperty PrefetchBehindFactorProperty = ForwardedToItems(SkUiVirtualVerticalStackLayoutBase.PrefetchBehindFactorProperty, SkUiValidate.NonNegative);

    /// <summary>Bindable property for <see cref="ReleaseFactor"/>.</summary>
    public static readonly BindableProperty ReleaseFactorProperty = ForwardedToItems(SkUiVirtualVerticalStackLayoutBase.ReleaseFactorProperty, SkUiVirtualVerticalStackLayoutBase.IsValidReleaseFactor);

    /// <summary>Bindable property for <see cref="PrefetchBudget"/>.</summary>
    public static readonly BindableProperty PrefetchBudgetProperty = ForwardedToItems(SkUiVirtualVerticalStackLayoutBase.PrefetchBudgetProperty, SkUiVirtualVerticalStackLayoutBase.IsValidPrefetchBudget);

    /// <summary>Bindable property for <see cref="RemainingItemsThreshold"/>.</summary>
    public static readonly BindableProperty RemainingItemsThresholdProperty = ForwardedToItems(SkUiVirtualVerticalStackLayoutBase.RemainingItemsThresholdProperty,
        SkUiVirtualVerticalStackLayoutBase.IsValidThreshold);

    /// <summary>Bindable property for <see cref="RemainingItemsThresholdReachedCommand"/>.</summary>
    public static readonly BindableProperty RemainingItemsThresholdReachedCommandProperty = ForwardedToItems(SkUiVirtualVerticalStackLayoutBase.RemainingItemsThresholdReachedCommandProperty);

    /// <summary>Bindable property for <see cref="RemainingItemsThresholdReachedCommandParameter"/>.</summary>
    public static readonly BindableProperty RemainingItemsThresholdReachedCommandParameterProperty = ForwardedToItems(SkUiVirtualVerticalStackLayoutBase.RemainingItemsThresholdReachedCommandParameterProperty);

    /// <summary>
    /// The items, one view each from <see cref="ItemTemplate"/>, with the item as its binding context; with
    /// <see cref="IsGrouped"/>, the groups, each the list of its items. A list (<see cref="IList"/>) is read by index; with
    /// <see cref="System.Collections.Specialized.INotifyCollectionChanged"/> its inserts, removes, moves and replacements
    /// (also of each group) change only the rows they touch (sources are listened to weakly). Other sequences are copied
    /// once and read again when they report a change.
    /// </summary>
    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>
    /// Creates an item's view (drawn SkUi* views only); a <see cref="DataTemplateSelector"/> chooses per item, and views are
    /// recycled per selected template. Without a template each item shows its text in an <see cref="SkUiLabel"/>.
    /// </summary>
    public DataTemplate? ItemTemplate { get => (DataTemplate?)GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }

    /// <summary>Gap between rows in DIPs, along the list's axis (not around the header and footer); <see cref="SpanSpacing"/> is the gap within a grid row.</summary>
    public double ItemSpacing { get => (double)GetValue(ItemSpacingProperty); set => SetValue(ItemSpacingProperty, value); }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.PrefetchFactor" />
    public double PrefetchFactor { get => (double)GetValue(PrefetchFactorProperty); set => SetValue(PrefetchFactorProperty, value); }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.PrefetchBehindFactor" />
    public double PrefetchBehindFactor { get => (double)GetValue(PrefetchBehindFactorProperty); set => SetValue(PrefetchBehindFactorProperty, value); }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.ReleaseFactor" />
    public double ReleaseFactor { get => (double)GetValue(ReleaseFactorProperty); set => SetValue(ReleaseFactorProperty, value); }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.PrefetchBudget" />
    public TimeSpan? PrefetchBudget { get => (TimeSpan?)GetValue(PrefetchBudgetProperty); set => SetValue(PrefetchBudgetProperty, value); }

    /// <summary>When positive, the size of every row along the list's axis in DIPs (rows are measured and arranged at it): the fast path. 0 (default): each row is as long as it measures.</summary>
    public double ItemExtent { get => (double)GetValue(ItemExtentProperty); set => SetValue(ItemExtentProperty, value); }

    /// <summary>The size assumed for rows not measured yet, in DIPs; 0 (default): the average of the rows measured so far.</summary>
    public double EstimatedItemSize { get => (double)GetValue(EstimatedItemSizeProperty); set => SetValue(EstimatedItemSizeProperty, value); }

    /// <summary>
    /// When what shows changes and the last visible row is this many rows (or fewer) from the end,
    /// <see cref="RemainingItemsThresholdReached"/> is raised (and its command run), once per row count. -1 (default) never.
    /// Rows are items in a plain list; grid rows, group headers and footers in others.
    /// </summary>
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

    /// <summary>The number of items (in a grouped list, of all groups, collapsed ones included).</summary>
    public int ItemCount => _model.ItemCount;

    /// <summary>The first item that shows (at least partly; in a grid, the first of its row), or -1. Group headers and footers are not items.</summary>
    public int FirstVisibleIndex => _firstVisible;

    /// <summary>The last item that shows (at least partly; in a grid, the last of its row), or -1.</summary>
    public int LastVisibleIndex => _lastVisible;

    /// <summary><see cref="FirstVisibleIndex"/> or <see cref="LastVisibleIndex"/> changed.</summary>
    public event EventHandler<SkUiVisibleRangeChangedEventArgs>? VisibleRangeChanged;

    /// <summary>
    /// The view the item template created for the item at <paramref name="index"/>, while it is realized; else <c>null</c>
    /// (the view itself, not the item container hosting it).
    /// </summary>
    public ISkUiView? GetRealizedView(int index) => RealizedHost(index)?.Content;

    /// <summary>Measures the item at <paramref name="index"/> again (its row, in a grid); see <see cref="SkUiVirtualVerticalStackLayoutBase.RemeasureItem"/>.</summary>
    public void RemeasureItem(int index)
    {
        CheckIndex(index);
        var (group, item) = _model.Locate(index);
        if (_model.RowOfItem(group, item) is >= 0 and var row)
            _items.RemeasureItem(row);
    }

    /// <summary>
    /// Scrolls so the item at <paramref name="index"/> shows at <paramref name="position"/>
    /// (<see cref="ScrollToPosition.MakeVisible"/> scrolls only when it is not fully visible), animated on the render thread or
    /// at once; see <see cref="SkUiVirtualVerticalStackLayoutBase.ScrollToIndex"/>. It lands in the part sticky views do not
    /// cover. An item of a collapsed group expands it first (<see cref="GroupExpanding"/> may keep it collapsed: the list then
    /// scrolls to its header).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not an item, or <paramref name="position"/> is not defined.</exception>
    public Task ScrollToIndex(int index, ScrollToPosition position = ScrollToPosition.MakeVisible, bool animated = true)
    {
        if (!Enum.IsDefined(position))
            throw new ArgumentOutOfRangeException(nameof(position));
        CheckIndex(index);
        var (group, item) = _model.Locate(index);
        if (group >= 0 && !_model.Groups[group].Expanded)
            ChangeGroupExpanded(group, true, fromGroup: false);
        var row = _model.RowOfItem(group, item);
        if (row < 0)
            row = _model.HeaderRowOf(group);
        return row < 0 ? Task.CompletedTask : _items.ScrollToIndex(row, position, animated);
    }

    /// <summary>
    /// Scrolls to <paramref name="item"/> (found with <see cref="object.Equals(object?)"/>); see <see cref="ScrollToIndex"/>.
    /// An item that is not in the list is ignored. In a grouped list an item may show in several groups: this scrolls to its
    /// first appearance; <see cref="ScrollToItem(object?, object?, ScrollToPosition, bool)"/> names the group.
    /// </summary>
    public Task ScrollToItem(object? item, ScrollToPosition position = ScrollToPosition.MakeVisible, bool animated = true)
    {
        var index = _model.IndexOfItem(item);
        return index < 0 ? Task.CompletedTask : ScrollToIndex(index, position, animated);
    }

    /// <summary>
    /// Scrolls to <paramref name="item"/> in <paramref name="group"/> (a group of <see cref="ItemsSource"/>, found by
    /// reference, then with <see cref="object.Equals(object?)"/>; <c>null</c>: its first appearance in any group); see
    /// <see cref="ScrollToIndex"/>. An item or group that is not in the list is ignored.
    /// </summary>
    public Task ScrollToItem(object? item, object? group, ScrollToPosition position = ScrollToPosition.MakeVisible, bool animated = true)
    {
        if (group is null)
            return ScrollToItem(item, position, animated);
        var index = _model.IndexOfGroup(group) is >= 0 and var inGroup ? _model.IndexOfItem(item, inGroup) : -1;
        return index < 0 ? Task.CompletedTask : ScrollToIndex(index, position, animated);
    }

    /// <summary>Sets <see cref="ItemsSource"/> (same as the property setter).</summary>
    public SkUiCollectionView SetItemsSource(IEnumerable? value) { ItemsSource = value; return this; }

    /// <summary>Sets <see cref="ItemTemplate"/> (same as the property setter).</summary>
    public SkUiCollectionView SetItemTemplate(DataTemplate? value) { ItemTemplate = value; return this; }

    private void CheckIndex(int index)
    {
        if ((uint)index >= (uint)_model.ItemCount)
            throw new ArgumentOutOfRangeException(nameof(index), index, "The index is not an item of the list.");
    }

    private void OnItemsSourceChanged(IEnumerable? value)
    {
        _userScrolled = false;
        _model.SetSource(value);
        // Recycled views keep their last item until rebound (as MAUI's CollectionView); items of a replaced source go.
        foreach (var view in _items.RecycledViews.Concat(_cellPool.Values.SelectMany(static cells => cells)))
        {
            ((BindableObject)view).BindingContext = null;
            if (view is GridRowPart row)
                foreach (var cell in row.Cells)
                    ((BindableObject)cell).BindingContext = null;
        }
    }

    private void OnItemTemplateChanged(DataTemplate? value)
    {
        _itemTemplate = value;
        // Views of the old templates go (after the rows released them); every row is measured again with the new ones.
        _model.Rebuild();
        DropViews();
    }

    /// <summary>Drops recycled views and grid cells (their templates no longer create the same views).</summary>
    private void DropViews()
    {
        _items.DropRecycled();
        _cellPool.Clear();
        _kindKeys.Clear();
    }

    /// <summary>The items or groups changed (after the rows did): the empty view, the selection, what shows.</summary>
    private void OnItemsChanged(IList? removed, bool reset)
    {
        UpdateEmpty();
        OnSelectionSourceChanged(removed, reset);
        OnVisibleRowsChanged();
        UpdateStickyGroupHeader();
        CheckLoadMore();
    }

    /// <summary>The visible rows changed: the visible items follow (headers and footers skipped).</summary>
    private void OnVisibleRowsChanged()
    {
        int first = -1, last = -1;
        var (firstRow, lastRow) = (_items.FirstVisibleIndex, _items.LastVisibleIndex);
        if (firstRow >= 0 && lastRow < _model.RowCount)
        {
            for (var row = firstRow; row <= lastRow && first < 0; row++)
                if (_model.RowAt(row) is { Kind: RowKind.Items } items)
                    first = _model.GlobalIndex(items.Group, items.Start);
            for (var row = lastRow; row >= firstRow && last < 0; row--)
                if (_model.RowAt(row) is { Kind: RowKind.Items } items)
                    last = _model.GlobalIndex(items.Group, items.Start + items.Count - 1);
        }
        CheckLoadMore();
        if (first == _firstVisible && last == _lastVisible)
            return;
        _firstVisible = first;
        _lastVisible = last;
        OnPropertyChanged(nameof(FirstVisibleIndex));
        OnPropertyChanged(nameof(LastVisibleIndex));
        VisibleRangeChanged?.Invoke(this, new SkUiVisibleRangeChangedEventArgs(first, last));
    }

    #endregion

    #region Layout properties

    /// <summary>Bindable property for <see cref="Orientation"/>.</summary>
    public static readonly BindableProperty OrientationProperty = BindableProperty.Create(nameof(Orientation), typeof(ItemsLayoutOrientation),
        typeof(SkUiCollectionView), ItemsLayoutOrientation.Vertical,
        validateValue: (_, value) => Enum.IsDefined((ItemsLayoutOrientation)value),
        propertyChanged: (view, _, value) => ((SkUiCollectionView)view).OnOrientationChanged((ItemsLayoutOrientation)value));

    /// <summary>Bindable property for <see cref="Span"/>.</summary>
    public static readonly BindableProperty SpanProperty = BindableProperty.Create(nameof(Span), typeof(int), typeof(SkUiCollectionView), 1,
        validateValue: (_, value) => value is int span && span >= 1,
        propertyChanged: (view, _, _) => ((SkUiCollectionView)view).ApplyStructure());

    /// <summary>Bindable property for <see cref="SpanSpacing"/>.</summary>
    public static readonly BindableProperty SpanSpacingProperty = BindableProperty.Create(nameof(SpanSpacing), typeof(double), typeof(SkUiCollectionView), 0d,
        validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, value) => ((SkUiCollectionView)view).OnSpanSpacingChanged((double)value));

    /// <summary>
    /// The list's axis: <see cref="ItemsLayoutOrientation.Vertical"/> (default) or <see cref="ItemsLayoutOrientation.Horizontal"/>
    /// (rows follow each other from left to right, from right to left in right-to-left layouts; the header and footer are at
    /// its start and end; no pull-to-refresh).
    /// </summary>
    public ItemsLayoutOrientation Orientation { get => (ItemsLayoutOrientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>
    /// How many items share a row (default 1: a list): a grid with that many columns (rows of a horizontal list), each row as
    /// tall (wide) as its tallest (widest) item. Items are as wide as the row divided by the span, less <see cref="SpanSpacing"/>.
    /// In a grouped list each group starts a new row.
    /// </summary>
    public int Span { get => (int)GetValue(SpanProperty); set => SetValue(SpanProperty, value); }

    /// <summary>Gap between the items of a grid row in DIPs (<see cref="ItemSpacing"/> is the gap between rows).</summary>
    public double SpanSpacing { get => (double)GetValue(SpanSpacingProperty); set => SetValue(SpanSpacingProperty, value); }

    /// <summary>Sets <see cref="Span"/> and <see cref="SpanSpacing"/>.</summary>
    public SkUiCollectionView SetSpan(int span, double spacing = 0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(span, 1);
        SkUiValidate.ThrowIfNegativeOrNotFinite(spacing, nameof(spacing));
        Span = span;
        SpanSpacing = spacing;
        return this;
    }

    private void OnOrientationChanged(ItemsLayoutOrientation value)
    {
        _horizontal = value == ItemsLayoutOrientation.Horizontal;
        _items.IsHorizontal = _horizontal;
        _scroller.Orientation = _horizontal ? ScrollOrientation.Horizontal : ScrollOrientation.Vertical;
        _refresh.PullEnabled = IsPullToRefreshEnabled && !_horizontal;
        _body.InvalidateBody();
        InvalidateMeasure();
    }

    private void OnSpanSpacingChanged(double value)
    {
        _spanSpacing = value;
        _items.ForgetItemSizes();
    }

    /// <summary>The structure of the rows changed (grouping, span, group templates): every row is built again.</summary>
    private void ApplyStructure(bool templatesChanged = false)
    {
        _model.Configure(IsGrouped, Span, IsGrouped && GroupHeaderTemplate is not null, IsGrouped && GroupFooterTemplate is not null, force: templatesChanged);
        DropViews();
    }

    #endregion

    #region Scrolling

    /// <summary>Bindable property for <see cref="VerticalScrollBarVisibility"/>.</summary>
    public static readonly BindableProperty VerticalScrollBarVisibilityProperty = BindableProperty.Create(nameof(VerticalScrollBarVisibility),
        typeof(ScrollBarVisibility), typeof(SkUiCollectionView), ScrollBarVisibility.Default,
        validateValue: (_, value) => Enum.IsDefined((ScrollBarVisibility)value),
        propertyChanged: (view, _, value) => ((SkUiCollectionView)view)._scroller.VerticalScrollBarVisibility = (ScrollBarVisibility)value);

    /// <summary>Bindable property for <see cref="HorizontalScrollBarVisibility"/>.</summary>
    public static readonly BindableProperty HorizontalScrollBarVisibilityProperty = BindableProperty.Create(nameof(HorizontalScrollBarVisibility),
        typeof(ScrollBarVisibility), typeof(SkUiCollectionView), ScrollBarVisibility.Default,
        validateValue: (_, value) => Enum.IsDefined((ScrollBarVisibility)value),
        propertyChanged: (view, _, value) => ((SkUiCollectionView)view)._scroller.HorizontalScrollBarVisibility = (ScrollBarVisibility)value);

    /// <summary>Bindable property for <see cref="Overscroll"/>.</summary>
    public static readonly BindableProperty OverscrollProperty = BindableProperty.Create(nameof(Overscroll), typeof(SkUiOverscrollMode),
        typeof(SkUiCollectionView), SkUiOverscrollMode.Default,
        validateValue: (_, value) => Enum.IsDefined((SkUiOverscrollMode)value),
        propertyChanged: (view, _, value) => ((SkUiCollectionView)view)._scroller.Overscroll = (SkUiOverscrollMode)value);

    /// <summary>The scroll bar of a vertical list (MAUI's visibility values; see <see cref="SkUiScrollView.VerticalScrollBarVisibility"/>).</summary>
    public ScrollBarVisibility VerticalScrollBarVisibility
    {
        get => (ScrollBarVisibility)GetValue(VerticalScrollBarVisibilityProperty);
        set => SetValue(VerticalScrollBarVisibilityProperty, value);
    }

    /// <summary>The scroll bar of a horizontal list.</summary>
    public ScrollBarVisibility HorizontalScrollBarVisibility
    {
        get => (ScrollBarVisibility)GetValue(HorizontalScrollBarVisibilityProperty);
        set => SetValue(HorizontalScrollBarVisibilityProperty, value);
    }

    /// <inheritdoc cref="SkUiScrollView.Overscroll" />
    public SkUiOverscrollMode Overscroll { get => (SkUiOverscrollMode)GetValue(OverscrollProperty); set => SetValue(OverscrollProperty, value); }

    /// <summary>The vertical scroll offset in DIPs (the header, when not sticky, scrolls with the items).</summary>
    public double ScrollY => _scroller.ScrollY;

    /// <summary>The horizontal scroll offset in DIPs (of a horizontal list).</summary>
    public double ScrollX => _scroller.ScrollX;

    /// <summary>True while the user drags the list or a fling / animated scroll runs.</summary>
    public bool IsScrolling => _scroller.IsScrolling;

    /// <summary>Raised after the scroll offset changes.</summary>
    public event EventHandler<ScrolledEventArgs>? Scrolled;

    /// <summary>Scrolls to an offset along the list's axis in DIPs, animated on the render thread or at once (a drag or another scroll cancels the task).</summary>
    public Task ScrollToAsync(double offset, bool animated = true) =>
        _horizontal ? _scroller.ScrollToAsync(offset, 0, animated) : _scroller.ScrollToAsync(0, offset, animated);

    private void OnScrollerScrolled(object? sender, ScrolledEventArgs args)
    {
        if (Controller.Dragging)
            _userScrolled = true;
        Scrolled?.Invoke(this, args);
        UpdateStickyGroupHeader();
        CheckLoadMore();
    }

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

    /// <summary>A drawn view before the items: it scrolls with them, or stays at the start with <see cref="IsStickyHeader"/>.</summary>
    public ISkUiView? Header { get => (ISkUiView?)GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }

    /// <summary>
    /// Creates <see cref="Header"/> when it is not set (drawn views; a <see cref="DataTemplateSelector"/> chooses by the
    /// binding context), once the list is in a tree. Its binding context is the list's.
    /// </summary>
    public DataTemplate? HeaderTemplate { get => (DataTemplate?)GetValue(HeaderTemplateProperty); set => SetValue(HeaderTemplateProperty, value); }

    /// <summary>
    /// Whether the header stays at the start while the items scroll (default <c>false</c>: it scrolls away with them). A sticky
    /// header is drawn over the list, and the items scroll behind it: give it a translucent background, margins or rounded
    /// corners to let them show through. At the start of the list the first item is after it (the list's content starts
    /// after the header's size, margins included), scrolling to an item places it after it, and the scroll bar runs
    /// beside the uncovered part. Scrolling does not re-record it.
    /// </summary>
    public bool IsStickyHeader { get => (bool)GetValue(IsStickyHeaderProperty); set => SetValue(IsStickyHeaderProperty, value); }

    /// <summary>A drawn view after the items: it scrolls with them (right after the last item), or stays at the end with <see cref="IsStickyFooter"/>.</summary>
    public ISkUiView? Footer { get => (ISkUiView?)GetValue(FooterProperty); set => SetValue(FooterProperty, value); }

    /// <summary>Creates <see cref="Footer"/> when it is not set; see <see cref="HeaderTemplate"/>.</summary>
    public DataTemplate? FooterTemplate { get => (DataTemplate?)GetValue(FooterTemplateProperty); set => SetValue(FooterTemplateProperty, value); }

    /// <summary>
    /// Whether the footer stays at the end while the items scroll, drawn over the list as a sticky header is (see
    /// <see cref="IsStickyHeader"/>): at the end of the list the last item is before it.
    /// </summary>
    public bool IsStickyFooter { get => (bool)GetValue(IsStickyFooterProperty); set => SetValue(IsStickyFooterProperty, value); }

    /// <summary>
    /// A drawn view shown instead of the items while there are none (<see cref="ItemsSource"/> is <c>null</c> or empty; a
    /// grouped list with groups but no rows to show is empty too), between the header and the footer; it fills the space
    /// the list has left (center its content to place it in the middle).
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

    /// <summary>Whether the list has no rows to show (the empty view shows).</summary>
    internal bool IsEmpty => _isEmpty;

    private void OnHeaderChanged()
    {
        _headerSlot.OnContentChanged();
        if (Header is null && !_headerSlot.IsSetting && Parent is not null)
            _headerSlot.Ensure(); // cleared by the app: the template provides it again
        PlaceHeader();
    }

    private void PlaceHeader() => Place(Header, IsStickyHeader, ref _stickyHeaderHost, ref _body.HeaderHost);

    private void PlaceFooter() => Place(Footer, IsStickyFooter, ref _stickyFooterHost, ref _body.FooterHost);

    private void OnFooterChanged()
    {
        _footerSlot.OnContentChanged();
        if (Footer is null && !_footerSlot.IsSetting && Parent is not null)
            _footerSlot.Ensure();
        PlaceFooter();
    }

    /// <summary>
    /// Shows <paramref name="view"/> in the sticky or the scrolled host (taken out of the other first: a view has one
    /// parent). A host is created, and attached, the first time it shows a view; once created it stays (empty, it measures
    /// nothing).
    /// </summary>
    private void Place(ISkUiView? view, bool sticky, ref StickyHost? stickyHost, ref SkUiContentView? scrolledHost)
    {
        SetContent(sticky ? scrolledHost : stickyHost, null);
        if (sticky)
            SetContent(view is null ? stickyHost : stickyHost ??= Adopt(new StickyHost(Controller)), view);
        else
            SetContent(view is null ? scrolledHost : scrolledHost ??= _body.Adopt(new SkUiContentView()), view);
    }

    private static void SetContent(SkUiContentView? host, ISkUiView? view)
    {
        if (host is not null && !ReferenceEquals(host.Content, view))
            host.Content = view;
    }

    /// <summary>Attaches a part created on demand (before it gets content, so the content's layout change reaches the list).</summary>
    private T Adopt<T>(T part) where T : SkUiView
    {
        AttachChild(part);
        return part;
    }

    /// <summary>Which header and footer hosts exist (tests).</summary>
    internal (bool StickyHeader, bool StickyFooter, bool Header, bool Footer) CreatedHosts =>
        (_stickyHeaderHost is not null, _stickyFooterHost is not null, _body.HeaderHost is not null, _body.FooterHost is not null);

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
        var empty = _model.RowCount == 0;
        if (empty == _isEmpty && _body.EmptyHost.IsVisible == empty)
            return;
        _isEmpty = empty;
        _body.EmptyHost.IsVisible = empty;
        if (empty && Parent is not null)
            _emptySlot.Ensure();
        _body.InvalidateBody();
    }

    #endregion

    #region Pull to refresh

    /// <summary>Bindable property for <see cref="IsPullToRefreshEnabled"/>.</summary>
    public static readonly BindableProperty IsPullToRefreshEnabledProperty = BindableProperty.Create(nameof(IsPullToRefreshEnabled), typeof(bool), typeof(SkUiCollectionView), false,
        propertyChanged: (bindable, _, value) =>
        {
            var view = (SkUiCollectionView)bindable;
            view._refresh.PullEnabled = (bool)value && !view._horizontal;
        });

    /// <summary>Bindable property for <see cref="IsRefreshing"/> (two-way by default: a pull sets it).</summary>
    public static readonly BindableProperty IsRefreshingProperty = BindableProperty.Create(nameof(IsRefreshing), typeof(bool), typeof(SkUiCollectionView), false,
        BindingMode.TwoWay,
        propertyChanged: (bindable, _, value) => ((SkUiCollectionView)bindable)._refresh.OnIsRefreshingChanged((bool)value));

    /// <summary>Bindable property for <see cref="RefreshCommand"/>.</summary>
    public static readonly BindableProperty RefreshCommandProperty = BindableProperty.Create(nameof(RefreshCommand), typeof(ICommand), typeof(SkUiCollectionView));

    /// <summary>Bindable property for <see cref="RefreshCommandParameter"/>.</summary>
    public static readonly BindableProperty RefreshCommandParameterProperty = BindableProperty.Create(nameof(RefreshCommandParameter), typeof(object), typeof(SkUiCollectionView));

    /// <summary>Bindable property for <see cref="RefreshColor"/>.</summary>
    public static readonly BindableProperty RefreshColorProperty = BindableProperty.Create(nameof(RefreshColor), typeof(Color), typeof(SkUiCollectionView), null,
        propertyChanged: (bindable, _, value) => ((SkUiCollectionView)bindable)._refresh.Indicator.SetColor((Color?)value));

    /// <summary>Bindable property for <see cref="RefreshStyle"/>.</summary>
    public static readonly BindableProperty RefreshStyleProperty = BindableProperty.Create(nameof(RefreshStyle), typeof(SkUiRefreshStyle), typeof(SkUiCollectionView),
        SkUiRefreshStyle.Default, validateValue: (_, value) => Enum.IsDefined((SkUiRefreshStyle)value),
        propertyChanged: (bindable, _, _) => ((SkUiCollectionView)bindable)._refresh.OnSettingsChanged());

    /// <summary>Bindable property for <see cref="RefreshTriggerDistance"/>.</summary>
    public static readonly BindableProperty RefreshTriggerDistanceProperty = BindableProperty.Create(nameof(RefreshTriggerDistance), typeof(double), typeof(SkUiCollectionView),
        0d, validateValue: SkUiValidate.NonNegative,
        propertyChanged: (bindable, _, _) => ((SkUiCollectionView)bindable)._refresh.OnSettingsChanged());

    /// <summary>Bindable property for <see cref="IsMousePullEnabled"/>.</summary>
    public static readonly BindableProperty IsMousePullEnabledProperty = BindableProperty.Create(nameof(IsMousePullEnabled), typeof(bool), typeof(SkUiCollectionView), false,
        propertyChanged: (bindable, _, _) => ((SkUiCollectionView)bindable)._refresh.OnSettingsChanged());

    /// <summary>Bindable property for <see cref="RefreshCompletion"/>.</summary>
    public static readonly BindableProperty RefreshCompletionProperty = BindableProperty.Create(nameof(RefreshCompletion), typeof(SkUiRefreshCompletion), typeof(SkUiCollectionView),
        SkUiRefreshCompletion.Manual, validateValue: (_, value) => Enum.IsDefined((SkUiRefreshCompletion)value));

    /// <summary>
    /// Whether pulling the top of a vertical list down and releasing it starts a refresh (default <c>false</c>). The pull works
    /// also when the items do not fill the list and when overscroll is off: the refresh indicator (<see cref="RefreshIndicator"/>)
    /// comes down with the pull, and a release past <see cref="RefreshTriggerDistance"/> sets <see cref="IsRefreshing"/>. Touch
    /// and pen drags pull; mouse drags only with <see cref="IsMousePullEnabled"/>. Horizontal lists are not pulled.
    /// </summary>
    public bool IsPullToRefreshEnabled { get => (bool)GetValue(IsPullToRefreshEnabledProperty); set => SetValue(IsPullToRefreshEnabledProperty, value); }

    /// <summary>
    /// Whether a refresh runs: the indicator spins at the top. Set to <c>true</c> (by a pull or by the app, as MAUI's
    /// <c>RefreshView</c>) it raises <see cref="Refreshing"/> and runs <see cref="RefreshCommand"/>; it goes back to
    /// <c>false</c> when the app sets it, or by itself with <see cref="SkUiRefreshCompletion.Automatic"/> completion.
    /// </summary>
    public bool IsRefreshing { get => (bool)GetValue(IsRefreshingProperty); set => SetValue(IsRefreshingProperty, value); }

    /// <summary>Runs with <see cref="RefreshCommandParameter"/> when <see cref="IsRefreshing"/> becomes <c>true</c> (when it can execute).</summary>
    public ICommand? RefreshCommand { get => (ICommand?)GetValue(RefreshCommandProperty); set => SetValue(RefreshCommandProperty, value); }

    /// <summary>The parameter of <see cref="RefreshCommand"/>.</summary>
    public object? RefreshCommandParameter { get => GetValue(RefreshCommandParameterProperty); set => SetValue(RefreshCommandParameterProperty, value); }

    /// <summary>The refresh indicator's color; <c>null</c> (default): the accent color.</summary>
    public Color? RefreshColor { get => (Color?)GetValue(RefreshColorProperty); set => SetValue(RefreshColorProperty, value); }

    /// <summary>
    /// How the refresh indicator shows: <see cref="SkUiRefreshStyle.Overlay"/> (a badge over the items),
    /// <see cref="SkUiRefreshStyle.Inline"/> (above the items, which move down: with bounce overscroll), or the look's
    /// (<see cref="SkUiRefreshStyle.Default"/>, default; the default look follows the platform).
    /// </summary>
    public SkUiRefreshStyle RefreshStyle { get => (SkUiRefreshStyle)GetValue(RefreshStyleProperty); set => SetValue(RefreshStyleProperty, value); }

    /// <summary>How far the top must be pulled (shown past the edge, DIPs) for a release to refresh; 0 (default): the look's (<see cref="SkUiLook.RefreshTriggerDistance"/>, 64).</summary>
    public double RefreshTriggerDistance { get => (double)GetValue(RefreshTriggerDistanceProperty); set => SetValue(RefreshTriggerDistanceProperty, value); }

    /// <summary>Whether mouse drags pull too (default <c>false</c>: touch and pen only, as desktop apps expect; touch screens of laptops pull).</summary>
    public bool IsMousePullEnabled { get => (bool)GetValue(IsMousePullEnabledProperty); set => SetValue(IsMousePullEnabledProperty, value); }

    /// <summary>
    /// Who sets <see cref="IsRefreshing"/> back to <c>false</c>: the app (<see cref="SkUiRefreshCompletion.Manual"/>, default,
    /// MAUI's rule) or the list when the refresh's work is done (<see cref="SkUiRefreshCompletion.Automatic"/>: an async
    /// command, deferrals taken in <see cref="Refreshing"/>).
    /// </summary>
    public SkUiRefreshCompletion RefreshCompletion { get => (SkUiRefreshCompletion)GetValue(RefreshCompletionProperty); set => SetValue(RefreshCompletionProperty, value); }

    /// <summary>The refresh indicator, drawn by the look (style it: <see cref="SkUiCoreRefreshIndicator.Color"/>, a shadow).</summary>
    public SkUiCoreRefreshIndicator RefreshIndicator => _refresh.Indicator;

    /// <summary>
    /// Raised when <see cref="IsRefreshing"/> becomes <c>true</c>, before <see cref="RefreshCommand"/>; with automatic
    /// completion, <see cref="SkUiRefreshingEventArgs.GetDeferral"/> keeps the refresh running until the deferral completes.
    /// </summary>
    public event EventHandler<SkUiRefreshingEventArgs>? Refreshing;

    bool ISkUiRefreshOwner.CanStartRefresh => IsEnabled && (RefreshCommand?.CanExecute(RefreshCommandParameter) ?? true);

    void ISkUiRefreshOwner.RaiseRefreshing(SkUiRefreshingEventArgs args) => Refreshing?.Invoke(this, args);

    #endregion

    #region Layout

    /// <summary>The scroller of the header, items, empty view and footer (tests).</summary>
    internal SkUiScrollView ScrollView => _scroller;

    /// <summary>The virtual layout of the rows (tests).</summary>
    internal SkUiVirtualVerticalStackLayoutBase ItemsLayout => _items;

    /// <inheritdoc />
    internal override IEnumerable<ISkUiView> SkiaChildren
    {
        get
        {
            // Screen order (focus, semantics): the header, the current group's header, the list, the footer; the refresh indicator.
            if (_stickyHeaderHost is not null)
                yield return _stickyHeaderHost;
            if (_stickyGroupHost is not null)
                yield return _stickyGroupHost;
            yield return _scroller;
            if (_stickyFooterHost is not null)
                yield return _stickyFooterHost;
            yield return _refreshLayer;
        }
    }

    /// <inheritdoc />
    /// <remarks>Drawn (and hit-tested) above the list: the sticky group header, the sticky header and footer, then the refresh indicator.</remarks>
    internal override void AddRenderChildren(List<ISkUiRenderable> children)
    {
        children.Add(_scroller);
        if (_stickyGroupHost is not null)
            children.Add(_stickyGroupHost);
        if (_stickyHeaderHost is not null)
            children.Add(_stickyHeaderHost);
        if (_stickyFooterHost is not null)
            children.Add(_stickyFooterHost);
        children.Add(_refreshLayer);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The list fills the view and a sticky header or footer is drawn over it: their sizes along the axis become the
    /// scroller's <see cref="SkUiScrollView.Insets"/>, so the items scroll behind them but are never covered at the start
    /// or the end.
    /// </remarks>
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var header = MeasurePart(_stickyHeaderHost, widthConstraint, heightConstraint);
        var footer = MeasurePart(_stickyFooterHost, widthConstraint, heightConstraint);
        _scroller.Insets = (Along(header), Along(footer));
        var list = ((IView)_scroller).Measure(widthConstraint, heightConstraint);
        MeasurePart(_stickyGroupHost, widthConstraint, heightConstraint);
        ((IView)_refreshLayer).Measure(widthConstraint, double.PositiveInfinity);
        var across = Math.Max(Across(list), Math.Max(Across(header), Across(footer)));
        var along = Math.Max(Along(list), Along(header) + Along(footer));
        return _horizontal ? new Size(along, across) : new Size(across, along);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        var length = Along(size);
        var header = AlongOf(_stickyHeaderHost);
        var footer = AlongOf(_stickyFooterHost);
        ((IView)_scroller).Arrange(new Rect(0, 0, size.Width, size.Height));
        ((IView?)_stickyHeaderHost)?.Arrange(Slot(0, header, size));
        ((IView?)_stickyFooterHost)?.Arrange(Slot(Math.Max(header, length - footer), footer, size));
        // The current group's header, below the sticky header (moved by a translation while the next group pushes it).
        ((IView?)_stickyGroupHost)?.Arrange(Slot(header, AlongOf(_stickyGroupHost), size));
        // Between the sticky parts: the indicator comes down from below the header, clipped there.
        ((IView)_refreshLayer).Arrange(Slot(header, Math.Max(0, length - header - footer), size));
        UpdateStickyGroupHeader();
        RecheckLoadMore();
        KeepSelectionVisibleAfterArrange(size);
    }

    /// <summary>A size along the list's axis.</summary>
    private double Along(Size size) => _horizontal ? size.Width : size.Height;

    /// <summary>A size across the list's axis.</summary>
    private double Across(Size size) => _horizontal ? size.Height : size.Width;

    /// <summary>The band <paramref name="length"/> long at <paramref name="start"/> along the axis, across all of <paramref name="size"/>.</summary>
    private Rect Slot(double start, double length, Size size) =>
        _horizontal ? new Rect(start, 0, length, size.Height) : new Rect(0, start, size.Width, length);

    /// <summary>Measures a part created on demand, unbounded along the axis (nothing when it does not exist).</summary>
    private Size MeasurePart(SkUiView? part, double width, double height) =>
        part is null ? Size.Zero : _horizontal ? ((IView)part).Measure(double.PositiveInfinity, height) : ((IView)part).Measure(width, double.PositiveInfinity);

    /// <summary>The measured size along the axis of a part created on demand (0 when it does not exist or is hidden).</summary>
    private double AlongOf(SkUiView? part) => part is null || !part.IsVisible ? 0 : Along(((IView)part).DesiredSize);

    #endregion
}
