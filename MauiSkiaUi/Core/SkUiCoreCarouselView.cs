using System.Collections;
using System.Collections.Specialized;
using MauiSkiaUi.Rendering;

namespace MauiSkiaUi.Core;

/// <summary>
/// Core carousel (analogue of <see cref="SkUiCarouselView"/>, on the same layout engine): a strip of item nodes scrolled one
/// snap point at a time, with a current item, looping, peek insets, several items in view (<see cref="ItemExtent"/>) or per
/// snap point (<see cref="ItemsPerSnapPoint"/>), item effects evaluated by the compositor, autoplay, and a linked
/// <see cref="SkUiCoreIndicatorView"/> (<see cref="SkUiCoreIndicatorView.SetCarousel"/>). Item nodes come from an item factory
/// (<see cref="SetItemFactory"/>) and are recycled as the strip scrolls: only the items in view and one on either side have
/// nodes. Not on Core: MAUI's <c>ItemsLayout</c>, visual states and scroll bars.
/// </summary>
public partial class SkUiCoreCarouselView : SkUiCoreNode, ISkUiScrollStepper, ISkUiAutoPlayHost
{
    private static readonly object DefaultItemKey = new();

    private readonly CarouselScroller _scroller;
    private readonly ItemsPanel _panel;
    private readonly SkUiScrollController _controller;
    private SkUiWeakListener<SkUiCoreCarouselView>? _sourceListener;
    private IEnumerable? _itemsSource;
    private IList? _source;
    private Func<object?, SkUiCoreNode>? _create;
    private Action<SkUiCoreNode, object?>? _bind;
    private Func<object?, object>? _key;
    private bool _horizontal = true;
    private bool _loop = true;
    private bool _scrollAnimated = true;
    private int _position;
    private object? _currentItem;
    private int _perPage = 1;
    private Thickness _peekAreaInsets;
    private double _itemExtent;
    private double _itemSpacing;
    private ItemsLayoutOrientation _orientation = ItemsLayoutOrientation.Horizontal;
    private SnapPointsType _snapPointsType = SnapPointsType.MandatorySingle;
    private SnapPointsAlignment _snapPointsAlignment = SnapPointsAlignment.Center;
    private ItemsUpdatingScrollMode _itemsUpdatingScrollMode = ItemsUpdatingScrollMode.KeepItemsInView;
    private SkUiCarouselEffect? _effect;
    private bool _isSwipeEnabled = true;
    private bool _isBounceEnabled = true;
    private bool? _requestedAnimation;
    private int _targetIndex = -1;
    private bool _requestingScroll;
    private Task _lastScroll = Task.CompletedTask;
    private double _scrollPosition;
    private bool _isDragging;
    private bool _isScrolling;
    private int _remainingItemsThreshold = -1;
    private int _thresholdCount = -1;
    private SkUiCoreIndicatorView? _indicator;
    private SkUiCoreNode? _emptyView;
    private bool _isEmpty = true;
    private bool _hovered;

    /// <summary>Creates an empty, horizontal, looping carousel.</summary>
    public SkUiCoreCarouselView()
    {
        InitClipToBounds(true);
        _panel = new ItemsPanel(this);
        _scroller = new CarouselScroller(this);
        _scroller.SetHorizontalScrollBarVisibility(ScrollBarVisibility.Never);
        _scroller.SetVerticalScrollBarVisibility(ScrollBarVisibility.Never);
        _scroller.SetOrientation(ScrollOrientation.Horizontal);
        _scroller.SetSnapPointsType(SnapPointsType.MandatorySingle);
        _scroller.SetContent(_panel);
        _scroller.AttachTo(this);
        _controller = ((ISkUiScrollHost)_scroller).Scroller;
        _controller.Stepper = this;
        _controller.DraggingChanged += _ => UpdateDragging();
        _controller.MovingChanged += _ => OnMovingChanged();
        UpdateOverscroll();
    }

    private SkUiScrollController Controller => _controller;

    /// <summary>The carousel's scroller (tests).</summary>
    internal SkUiCoreScrollView ScrollView => _scroller;

    #region Items

    /// <summary>
    /// The items, one node each from the item factory (<see cref="SetItemFactory"/>). A list (<see cref="IList"/>) is read by
    /// index; other sequences are copied. With <see cref="INotifyCollectionChanged"/> (listened to weakly) every change keeps the
    /// current item where it shows (or, when it was removed, the position), and items appended while the carousel flings do
    /// not stop the fling.
    /// </summary>
    public IEnumerable? ItemsSource { get => _itemsSource; set => SetItemsSource(value); }

    /// <summary>The number of items.</summary>
    public int ItemCount => _source?.Count ?? 0;

    /// <summary>Sets <see cref="ItemsSource"/>.</summary>
    public SkUiCoreCarouselView SetItemsSource(IEnumerable? value)
    {
        if (ReferenceEquals(_itemsSource, value))
            return this;
        _itemsSource = value;
        (_sourceListener ??= new SkUiWeakListener<SkUiCoreCarouselView>(this, static (carousel, change) =>
        {
            if (change.Kind == SkUiChangeKind.Collection)
                carousel.ItemsChanged(carousel._itemsSource, keepCurrentItem: true);
        })).Listen(value as INotifyCollectionChanged);
        // Recycled nodes keep their last item until rebound; items of a replaced source go.
        _panel.ClearPool();
        ItemsChanged(value, keepCurrentItem: false);
        OnPropertyChanged(nameof(ItemsSource));
        return this;
    }

    /// <summary>
    /// How item nodes are made: <paramref name="create"/> makes a node for an item, <paramref name="bind"/> shows an item in a
    /// node (called again whenever a recycled node shows another item; it can read <see cref="Position"/>), and
    /// <paramref name="key"/> (optional) gives the recycling key, so nodes made for one kind of item are reused only for that
    /// kind (a template selector's role). Without a factory, each item shows its text in a centered <see cref="SkUiCoreLabel"/>.
    /// Setting a factory makes the nodes again.
    /// </summary>
    public SkUiCoreCarouselView SetItemFactory(Func<object?, SkUiCoreNode> create, Action<SkUiCoreNode, object?> bind, Func<object?, object>? key = null)
    {
        ArgumentNullException.ThrowIfNull(create);
        ArgumentNullException.ThrowIfNull(bind);
        _create = create;
        _bind = bind;
        _key = key;
        _panel.ReleaseAll();
        _panel.ClearPool();
        _panel.Refresh();
        return this;
    }

    /// <summary>Which item is current after the items change (see <see cref="SkUiCarouselView.ItemsUpdatingScrollMode"/>).</summary>
    public ItemsUpdatingScrollMode ItemsUpdatingScrollMode { get => _itemsUpdatingScrollMode; set => SetItemsUpdatingScrollMode(value); }

    /// <summary>Sets <see cref="ItemsUpdatingScrollMode"/>.</summary>
    public SkUiCoreCarouselView SetItemsUpdatingScrollMode(ItemsUpdatingScrollMode value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        SetProperty(ref _itemsUpdatingScrollMode, value, nameof(ItemsUpdatingScrollMode));
        return this;
    }

    /// <summary>The item at <paramref name="index"/>, or <c>null</c> outside the items.</summary>
    private object? ItemAt(int index) => _source is { } source && index >= 0 && index < source.Count ? source[index] : null;

    /// <summary>The recycling key of an item.</summary>
    private object ItemKey(object? item) => _key is { } key ? key(item) : _create is null ? DefaultItemKey : this;

    private SkUiCoreNode CreateItemNode(object? item) =>
        _create is { } create
            ? create(item) ?? throw new InvalidOperationException($"The item factory of {nameof(SkUiCoreCarouselView)} returned null.")
            : new SkUiCoreLabel().SetHorizontalTextAlignment(TextAlignment.Center).SetVerticalTextAlignment(TextAlignment.Center);

    private void BindItemNode(SkUiCoreNode node, object? item)
    {
        if (_bind is { } bind)
            bind(node, item);
        else if (node is SkUiCoreLabel label)
            label.SetText(item?.ToString() ?? string.Empty);
    }

    /// <summary>
    /// The items changed: read again; the current item stays current where it shows (<paramref name="keepCurrentItem"/>, found
    /// by equality), else the position stays (within the items). <see cref="ItemsUpdatingScrollMode"/> can keep the position
    /// or move to the last item instead.
    /// </summary>
    private void ItemsChanged(IEnumerable? value, bool keepCurrentItem)
    {
        var previousItem = _currentItem;
        var previousCount = _panel.Count;
        // A fling, a drag or an animated scroll goes on from where it is: the strip stays that far past the current item.
        var past = _panel.PastItem(_position);
        _source = value switch
        {
            null => null,
            IList list => list,
            _ => value.Cast<object?>().ToList()
        };
        var count = ItemCount;
        var position = _position;
        if (count > 0 && _itemsUpdatingScrollMode == ItemsUpdatingScrollMode.KeepLastItemInView)
        {
            position = PageStart(count - 1);
            past = 0;
            StopScrolling(); // to the last item, not on from it
        }
        else if (count > 0)
        {
            var keep = keepCurrentItem && _itemsUpdatingScrollMode == ItemsUpdatingScrollMode.KeepItemsInView;
            // Where it is while the item there equals it (equal items), else where it moved.
            var index = !keep || previousItem is null ? -1 : Equals(ItemAt(_position), previousItem) ? _position : _source!.IndexOf(previousItem);
            position = PageStart(index >= 0 ? index : Math.Clamp(_position, 0, count - 1));
        }
        _panel.SetCount(count, position + past);
        if (_targetIndex >= 0)
            _targetIndex = position;
        UpdateOverscroll();
        FollowIndicatorCount();
        if (count > 0)
            ApplyPosition(position, fromScroll: true);
        else if (previousCount > 0)
            ApplyPosition(0, fromScroll: true); // the items went: back to the start, with the current item cleared
        else
            SyncCurrentItem(null);
        UpdateEmpty();
        if (count != previousCount)
            CheckThreshold();
    }

    #endregion

    #region Position

    /// <summary>
    /// The current item's index (default 0): the item at the snap point, a page's first with <see cref="ItemsPerSnapPoint"/>.
    /// Scrolling sets it as another item reaches the snap point; setting it scrolls there (animated with
    /// <see cref="IsScrollAnimated"/>; a looping carousel takes the shorter way round). Beyond the last item it becomes the
    /// last page's first; set before the items, it applies once they come.
    /// </summary>
    public int Position { get => _position; set => SetPosition(value); }

    /// <summary>The current item: the item at <see cref="Position"/>. Setting an item of the list scrolls to it; others are ignored.</summary>
    public object? CurrentItem { get => _currentItem; set => SetCurrentItem(value); }

    /// <summary>Whether setting <see cref="Position"/> or <see cref="CurrentItem"/> scrolls with an animation (default <c>true</c>).</summary>
    public bool IsScrollAnimated { get => _scrollAnimated; set => SetIsScrollAnimated(value); }

    /// <summary>The item at the snap point, fractional while scrolling (2.5: halfway from the third item to the fourth).</summary>
    public double ScrollPosition => _scrollPosition;

    /// <summary>Raised after <see cref="Position"/> changes (MAUI's arguments).</summary>
    public event EventHandler<PositionChangedEventArgs>? PositionChanged;

    /// <summary>Raised after <see cref="CurrentItem"/> changes (MAUI's arguments).</summary>
    public event EventHandler<CurrentItemChangedEventArgs>? CurrentItemChanged;

    /// <summary>Raised after the strip scrolls (MAUI's arguments).</summary>
    public event EventHandler<ItemsViewScrolledEventArgs>? Scrolled;

    /// <summary>Sets <see cref="Position"/>.</summary>
    public SkUiCoreCarouselView SetPosition(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        var count = ItemCount;
        if (count > 0)
            value = PageStart(Math.Min(value, count - 1));
        if (value != _position)
            ApplyPosition(value, fromScroll: false);
        return this;
    }

    /// <summary>Sets <see cref="CurrentItem"/>.</summary>
    public SkUiCoreCarouselView SetCurrentItem(object? value)
    {
        var index = _source?.IndexOf(value) ?? -1;
        if (index >= 0)
            SetPosition(index);
        return this;
    }

    /// <summary>Sets <see cref="IsScrollAnimated"/>.</summary>
    public SkUiCoreCarouselView SetIsScrollAnimated(bool value)
    {
        SetProperty(ref _scrollAnimated, value, nameof(IsScrollAnimated));
        return this;
    }

    /// <summary>
    /// Scrolls to the item at <paramref name="index"/> (its page's first becomes <see cref="Position"/>), animated on the render
    /// thread or at once; a looping carousel takes the shorter way round. The task completes when the scroll ends; a touch or
    /// a newer scroll cancels it. Before the first layout the carousel starts there.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not an item.</exception>
    public Task ScrollToIndex(int index, bool animated = true)
    {
        if (index < 0 || index >= ItemCount)
            throw new ArgumentOutOfRangeException(nameof(index), index, "The index is not an item of the carousel.");
        index = PageStart(index);
        _requestedAnimation = animated;
        try
        {
            if (_position != index)
                ApplyPosition(index, fromScroll: false);
            else
                ScrollToCurrent(animated);
        }
        finally
        {
            _requestedAnimation = null;
        }
        return _lastScroll;
    }

    private (int From, int To, bool Forward)? _step;

    /// <summary>Steps to the page from <paramref name="index"/>, the way asked for also when a loop's other way round is shorter.</summary>
    private void StepTo(int index, bool forward, bool animated)
    {
        _step = (_position, index, forward);
        try { _ = ScrollToIndex(index, animated); }
        finally { _step = null; }
    }

    /// <summary>Stops a running scroll; the position is not taken from where it stopped.</summary>
    private void StopScrolling()
    {
        _requestingScroll = true;
        try { Controller.StopMotion(); }
        finally { _requestingScroll = false; }
    }

    /// <summary>Scrolls to <paramref name="item"/> (found with <see cref="object.Equals(object?)"/>); see <see cref="ScrollToIndex"/>. An item that is not in the list is ignored.</summary>
    public Task ScrollToItem(object? item, bool animated = true)
    {
        var index = _source?.IndexOf(item) ?? -1;
        return index < 0 ? Task.CompletedTask : ScrollToIndex(index, animated);
    }

    /// <summary>
    /// <paramref name="value"/> is the position now (set by the app, or reached by scrolling: <paramref name="fromScroll"/>):
    /// the current item, events, the indicator follow; set by the app, the carousel scrolls there.
    /// </summary>
    private void ApplyPosition(int value, bool fromScroll)
    {
        var previous = _position;
        _position = value;
        if (previous != value)
            OnPropertyChanged(nameof(Position));
        if (_position != value)
            return; // a handler moved on (here and below)
        var previousItem = _currentItem;
        var item = ItemAt(value);
        if (!Equals(previousItem, item))
        {
            SyncCurrentItem(item);
            if (_position == value && CurrentItemChanged is { } itemHandler && SkUiCarouselView.TryCreateCurrentItemArgs(previousItem, item) is { } itemArgs)
                itemHandler(this, itemArgs);
        }
        if (_position != value)
            return;
        if (previous != value && PositionChanged is { } handler && SkUiCarouselView.TryCreatePositionArgs(previous, value) is { } args)
            handler(this, args);
        if (_position != value)
            return;
        _indicator?.FollowCarousel(value / _perPage);
        if (!fromScroll)
            ScrollToCurrent(_requestedAnimation ?? _scrollAnimated);
        if (!Controller.IsMoving)
            FollowWithIndicator();
        CheckThreshold();
        InvalidateSemantics();
    }

    private void SyncCurrentItem(object? item)
    {
        if (Equals(_currentItem, item))
            return;
        _currentItem = item;
        OnPropertyChanged(nameof(CurrentItem));
    }

    /// <summary>Scrolls to <see cref="Position"/>; the position stays there until the scroll ends or a drag takes over.</summary>
    private void ScrollToCurrent(bool animated)
    {
        if (ItemCount == 0)
        {
            _lastScroll = Task.CompletedTask;
            return;
        }
        _targetIndex = _position;
        // The scroller stops a running scroll first, which reports it rests for a moment: the position is not taken from it.
        _requestingScroll = true;
        try
        {
            _lastScroll = _panel.ScrollToIndex(_position, animated && IsShownNow(), _step);
        }
        finally
        {
            _requestingScroll = false;
        }
        if (_lastScroll.IsCompleted)
            _targetIndex = -1;
    }

    /// <summary>The scroller's offset changed (scrolling, flings, snaps, corrections).</summary>
    private void OnScrolled()
    {
        UpdateDragging();
        if (Controller.Dragging)
            _targetIndex = -1; // a drag takes over from a scroll to a position
        if (!_panel.HasGeometry || ItemCount == 0)
            return;
        var position = _panel.ScrollPosition;
        if (Math.Abs(position - _scrollPosition) > 1e-6)
        {
            _scrollPosition = position;
            OnPropertyChanged(nameof(ScrollPosition));
        }
        if (_targetIndex < 0)
        {
            var current = _panel.CurrentIndex(_position);
            if (current != _position)
                ApplyPosition(current, fromScroll: true);
        }
        FollowWithIndicator();
        Scrolled?.Invoke(this, _panel.CreateScrolledArgs());
        CheckThreshold();
    }

    private void OnMovingChanged()
    {
        var moving = Controller.IsMoving;
        if (moving != _isScrolling)
        {
            _isScrolling = moving;
            OnPropertyChanged(nameof(IsScrolling));
        }
        UpdateDragging();
        if (moving || _requestingScroll)
            return;
        // Settled: on the snap point of the position reached (or asked for); autoplay waits a full interval from here.
        var asked = _targetIndex >= 0;
        _targetIndex = -1;
        _autoPlay?.Restart();
        if (_panel.HasGeometry && ItemCount > 0)
        {
            var disturbed = _panel.TakeDisturbed();
            var current = asked && disturbed ? _position : _panel.CurrentIndex(_position);
            if (current != _position)
                ApplyPosition(current, fromScroll: true);
            // A change of the items or the layout stopped it on the way: it goes on.
            if (disturbed && (asked || _snapPointsType != SnapPointsType.None) && !Controller.IsMoving && !_panel.RestsOn(_position))
                ScrollToCurrent(_scrollAnimated);
        }
        FollowWithIndicator();
    }

    private void UpdateDragging()
    {
        var dragging = Controller.Dragging;
        if (dragging == _isDragging)
            return;
        _isDragging = dragging;
        OnPropertyChanged(nameof(IsDragging));
    }

    /// <summary>A scroll correction or a layout moved the carousel by whole cycles of its items (looping): what shows stays.</summary>
    internal void OnLayoutScrolled() => OnScrolled();

    /// <summary>Whether the user drags the carousel now.</summary>
    public bool IsDragging => _isDragging;

    /// <summary>Whether the carousel is dragged, flung or animated.</summary>
    public bool IsScrolling => _isScrolling;

    #endregion

    #region Layout properties

    /// <summary>Whether scrolling goes on past the last item to the first and before the first to the last (default <c>true</c>). A single item does not loop.</summary>
    public bool Loop { get => _loop; set => SetLoop(value); }

    /// <summary>Room at the start and end of the axis where the neighbors of the current item peek in (see <see cref="SkUiCarouselView.PeekAreaInsets"/>).</summary>
    public Thickness PeekAreaInsets { get => _peekAreaInsets; set => SetPeekAreaInsets(value); }

    /// <summary>When positive, every item's length along the axis in DIPs (several in view); 0 (default) fills the carousel minus the insets.</summary>
    public double ItemExtent { get => _itemExtent; set => SetItemExtent(value); }

    /// <summary>The gap between items along the axis in DIPs (default 0).</summary>
    public double ItemSpacing { get => _itemSpacing; set => SetItemSpacing(value); }

    /// <summary>The scroll axis: horizontal (default) or vertical.</summary>
    public ItemsLayoutOrientation Orientation { get => _orientation; set => SetOrientation(value); }

    /// <summary>How drags and flings end: one snap point per swipe (default), the nearest to where a fling would stop, or anywhere.</summary>
    public SnapPointsType SnapPointsType { get => _snapPointsType; set => SetSnapPointsType(value); }

    /// <summary>Where the current item (or page; with an <see cref="ItemEffect"/> the item) lines up: in the middle (default), or at the start or end inside the insets.</summary>
    public SnapPointsAlignment SnapPointsAlignment { get => _snapPointsAlignment; set => SetSnapPointsAlignment(value); }

    /// <summary>
    /// How many items one snap point moves past (default 1; see <see cref="SkUiCarouselView.ItemsPerSnapPoint"/>): pages of that
    /// many from the first, the same on every round of a loop; <see cref="Position"/> is always a page's first item, and a
    /// linked indicator shows one indicator per page.
    /// </summary>
    public int ItemsPerSnapPoint { get => _perPage; set => SetItemsPerSnapPoint(value); }

    /// <summary>Whether drags and the wheel scroll the carousel (default <c>true</c>); <see cref="Position"/>, the keyboard and screen readers always do.</summary>
    public bool IsSwipeEnabled { get => _isSwipeEnabled; set => SetIsSwipeEnabled(value); }

    /// <summary>Whether a carousel that does not loop bounces (or stretches, per the look) past its first and last items (default <c>true</c>).</summary>
    public bool IsBounceEnabled { get => _isBounceEnabled; set => SetIsBounceEnabled(value); }

    /// <summary>Sets <see cref="Loop"/>.</summary>
    public SkUiCoreCarouselView SetLoop(bool value)
    {
        if (_loop == value) return this;
        OnLayoutChanged(() => _loop = value);
        OnPropertyChanged(nameof(Loop));
        return this;
    }

    /// <summary>Sets <see cref="PeekAreaInsets"/>.</summary>
    public SkUiCoreCarouselView SetPeekAreaInsets(Thickness value)
    {
        SkUiValidate.ThrowIfNotThickness(value, nameof(value));
        if (_peekAreaInsets == value) return this;
        OnLayoutChanged(() => _peekAreaInsets = value);
        OnPropertyChanged(nameof(PeekAreaInsets));
        return this;
    }

    /// <summary>Sets <see cref="ItemExtent"/>.</summary>
    public SkUiCoreCarouselView SetItemExtent(double value)
    {
        SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value));
        if (_itemExtent.Equals(value)) return this; // exact: an unchanged setting
        OnLayoutChanged(() => _itemExtent = value);
        OnPropertyChanged(nameof(ItemExtent));
        return this;
    }

    /// <summary>Sets <see cref="ItemSpacing"/>.</summary>
    public SkUiCoreCarouselView SetItemSpacing(double value)
    {
        SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value));
        if (_itemSpacing.Equals(value)) return this; // exact: an unchanged setting
        OnLayoutChanged(() => _itemSpacing = value);
        OnPropertyChanged(nameof(ItemSpacing));
        return this;
    }

    /// <summary>Sets <see cref="Orientation"/>.</summary>
    public SkUiCoreCarouselView SetOrientation(ItemsLayoutOrientation value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        if (_orientation == value) return this;
        _orientation = value;
        OnLayoutChanged(() => _horizontal = value == ItemsLayoutOrientation.Horizontal);
        _scroller.SetOrientation(_horizontal ? ScrollOrientation.Horizontal : ScrollOrientation.Vertical);
        _indicator?.SetOrientation(_horizontal ? StackOrientation.Horizontal : StackOrientation.Vertical);
        OnPropertyChanged(nameof(Orientation));
        return this;
    }

    /// <summary>Sets <see cref="SnapPointsType"/>.</summary>
    public SkUiCoreCarouselView SetSnapPointsType(SnapPointsType value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        if (!SetProperty(ref _snapPointsType, value, nameof(SnapPointsType))) return this;
        _scroller.SetSnapPointsType(value);
        return this;
    }

    /// <summary>Sets <see cref="SnapPointsAlignment"/>.</summary>
    public SkUiCoreCarouselView SetSnapPointsAlignment(SnapPointsAlignment value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        if (_snapPointsAlignment == value) return this;
        OnLayoutChanged(() => _snapPointsAlignment = value);
        OnPropertyChanged(nameof(SnapPointsAlignment));
        return this;
    }

    /// <summary>Sets <see cref="ItemsPerSnapPoint"/>; the position goes to its page's first item, at once.</summary>
    public SkUiCoreCarouselView SetItemsPerSnapPoint(int value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
        if (_perPage == value) return this;
        OnLayoutChanged(() => _perPage = value);
        var count = ItemCount;
        var page = count > 0 ? PageStart(Math.Min(_position, count - 1)) : _position;
        if (page != _position)
        {
            _requestedAnimation = false;
            try { ApplyPosition(page, fromScroll: false); }
            finally { _requestedAnimation = null; }
        }
        FollowIndicatorCount();
        FollowWithIndicator();
        OnPropertyChanged(nameof(ItemsPerSnapPoint));
        return this;
    }

    /// <summary>Sets <see cref="IsSwipeEnabled"/>.</summary>
    public SkUiCoreCarouselView SetIsSwipeEnabled(bool value)
    {
        if (!SetProperty(ref _isSwipeEnabled, value, nameof(IsSwipeEnabled))) return this;
        Controller.IsUserScrollEnabled = value;
        if (!value)
            _scroller.CancelGestures();
        return this;
    }

    /// <summary>Sets <see cref="IsBounceEnabled"/>.</summary>
    public SkUiCoreCarouselView SetIsBounceEnabled(bool value)
    {
        if (SetProperty(ref _isBounceEnabled, value, nameof(IsBounceEnabled)))
            UpdateOverscroll();
        return this;
    }

    /// <summary>The first item of the page that holds the item at <paramref name="index"/>.</summary>
    private int PageStart(int index) => index - index % _perPage;

    /// <summary>The number of pages of <see cref="ItemsPerSnapPoint"/> items (the last one maybe shorter).</summary>
    private int PageCount => (ItemCount + _perPage - 1) / _perPage;

    /// <summary>A setting of the items' layout changed: the current place is kept, and the items are laid out again.</summary>
    private void OnLayoutChanged(Action apply)
    {
        _panel.KeepCurrentPlace();
        apply();
        _panel.Refresh();
        UpdateOverscroll();
        FollowWithIndicator(); // whether its selection wraps (Loop)
    }

    /// <summary>Bouncing at the ends: not while the strip loops.</summary>
    private void UpdateOverscroll() =>
        _scroller.SetOverscroll(_panel.Loops || !_isBounceEnabled ? SkUiOverscrollMode.None : SkUiOverscrollMode.Default);

    #endregion

    #region Effects

    /// <summary>
    /// How items change as they scroll (default <c>null</c>: they move with the scrolling): a <see cref="SkUiCoverFlowEffect"/>,
    /// a <see cref="SkUiScaleEffect"/>, or an effect of the app's own, evaluated on the render thread for every frame. An effect
    /// object can be shared by carousels of both layers.
    /// </summary>
    public SkUiCarouselEffect? ItemEffect { get => _effect; set => SetItemEffect(value); }

    /// <summary>Sets <see cref="ItemEffect"/>.</summary>
    public SkUiCoreCarouselView SetItemEffect(SkUiCarouselEffect? value)
    {
        if (ReferenceEquals(_effect, value)) return this;
        if (_effect is not null)
            _effect.Changed -= OnEffectSettingsChanged;
        _effect = value;
        if (value is not null)
            value.Changed += OnEffectSettingsChanged;
        _panel.OnEffectChanged();
        OnPropertyChanged(nameof(ItemEffect));
        return this;
    }

    private void OnEffectSettingsChanged(object? sender, EventArgs args) => _panel.OnEffectChanged();

    #endregion

    #region Indicator

    /// <summary>The linked indicator (set through <see cref="SkUiCoreIndicatorView.SetCarousel"/>).</summary>
    internal SkUiCoreIndicatorView? Indicator => _indicator;

    /// <summary><paramref name="indicator"/> links to this carousel: another indicator it showed lets go.</summary>
    internal void LinkIndicator(SkUiCoreIndicatorView indicator)
    {
        if (_indicator is { } previous && !ReferenceEquals(previous, indicator))
            previous.SetCarousel(null);
        _indicator = indicator;
        indicator.SetOrientation(_horizontal ? StackOrientation.Horizontal : StackOrientation.Vertical);
        FollowIndicatorCount();
        indicator.FollowCarousel(_position / _perPage);
        FollowWithIndicator();
    }

    /// <summary><paramref name="indicator"/> no longer shows this carousel.</summary>
    internal void UnlinkIndicator(SkUiCoreIndicatorView indicator)
    {
        if (ReferenceEquals(_indicator, indicator))
            _indicator = null;
    }

    /// <summary>The linked indicator's position changed by a tap (or the app): the carousel goes to that page.</summary>
    internal void OnIndicatorPositionChanged(SkUiCoreIndicatorView indicator)
    {
        if (!ReferenceEquals(indicator, _indicator) || ItemCount == 0)
            return;
        var index = PageStart(Math.Min(indicator.Position, PageCount - 1) * _perPage);
        if (index != _position)
            SetPosition(index);
    }

    private void FollowIndicatorCount() => _indicator?.FollowCarouselCount(PageCount, _position / _perPage);

    /// <summary>The indicator draws the scroll position while the carousel moves, and the position once it rests.</summary>
    private void FollowWithIndicator()
    {
        if (_indicator is not { } indicator)
            return;
        var moving = Controller.IsMoving && _panel.HasGeometry;
        indicator.FollowDrawn(moving ? _panel.PagePosition : _position / _perPage, _panel.Loops);
    }

    #endregion

    #region Loading more

    /// <summary>
    /// When the last item in view (in a looping carousel, the current item) is this many items (or fewer) from the end,
    /// <see cref="RemainingItemsThresholdReached"/> is raised, once per item count. -1 (default) never.
    /// </summary>
    public int RemainingItemsThreshold { get => _remainingItemsThreshold; set => SetRemainingItemsThreshold(value); }

    /// <summary>The end of the items comes near; see <see cref="RemainingItemsThreshold"/>.</summary>
    public event EventHandler? RemainingItemsThresholdReached;

    /// <summary>Sets <see cref="RemainingItemsThreshold"/>.</summary>
    public SkUiCoreCarouselView SetRemainingItemsThreshold(int value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, -1);
        if (!SetProperty(ref _remainingItemsThreshold, value, nameof(RemainingItemsThreshold))) return this;
        _thresholdCount = -1;
        CheckThreshold();
        return this;
    }

    private void CheckThreshold()
    {
        var count = ItemCount;
        if (_remainingItemsThreshold < 0 || count == 0 || _thresholdCount == count || !_panel.HasGeometry)
            return;
        var last = _panel.Loops ? _position + _perPage - 1 : _panel.LastVisibleIndex;
        if (last < count - 1 - _remainingItemsThreshold)
            return;
        _thresholdCount = count;
        RemainingItemsThresholdReached?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Empty view

    /// <summary>A node shown instead of the items while there are none; it fills the carousel.</summary>
    public SkUiCoreNode? EmptyView { get => _emptyView; set => SetEmptyView(value); }

    /// <summary>Sets <see cref="EmptyView"/>; the node must not have a parent.</summary>
    public SkUiCoreCarouselView SetEmptyView(SkUiCoreNode? value)
    {
        if (ReferenceEquals(_emptyView, value)) return this;
        if (value is not null && (value.Parent is not null || WouldCreateParentCycle(value)))
            throw new InvalidOperationException("The empty view already has a parent.");
        _emptyView?.AttachTo(null);
        _emptyView = value;
        value?.AttachTo(this);
        InvalidateRender(SkUiRenderDirty.Children);
        InvalidateMeasure();
        OnPropertyChanged(nameof(EmptyView));
        return this;
    }

    private void UpdateEmpty()
    {
        var empty = ItemCount == 0;
        if (empty == _isEmpty)
            return;
        _isEmpty = empty;
        InvalidateRender(SkUiRenderDirty.Children);
        InvalidateMeasure();
    }

    #endregion

    #region Visible nodes

    private List<SkUiCoreNode>? _visibleNodes;
    private EventHandler? _visibleNodesChanged;

    /// <summary>
    /// The item nodes in view, in the order of the strip (see <see cref="SkUiCarouselView.VisibleViews"/>): peeking neighbors
    /// included, the nodes made ahead on either side left out; with an item effect, the nodes it draws. The list is kept up to
    /// date in place from the first time it is read (or <see cref="VisibleNodesChanged"/> has a handler).
    /// </summary>
    public IReadOnlyList<SkUiCoreNode> VisibleNodes
    {
        get
        {
            if (_visibleNodes is null)
            {
                _visibleNodes = [];
                _panel.UpdateVisibleNodes();
            }
            return _visibleNodes;
        }
    }

    /// <summary>Raised after <see cref="VisibleNodes"/> changes: a node entered or left the view (not per frame).</summary>
    public event EventHandler? VisibleNodesChanged
    {
        add
        {
            _ = VisibleNodes;
            _visibleNodesChanged += value;
        }
        remove => _visibleNodesChanged -= value;
    }

    /// <summary>The list kept up to date, or <c>null</c> while nothing reads it.</summary>
    internal List<SkUiCoreNode>? TrackedVisibleNodes => _visibleNodes;

    internal void RaiseVisibleNodesChanged() => _visibleNodesChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>The node of the item at <paramref name="index"/> while it has one (the copy nearest the current place), or <c>null</c>.</summary>
    public SkUiCoreNode? GetRealizedNode(int index) => _panel.RealizedNode(index);

    /// <summary>The panel's item layout (tests): item length, stride, slots and copies of the items.</summary>
    internal (double Extent, double Stride, int Slots, int Copies) Geometry => _panel.GeometryInfo;

    /// <summary>The realized slots (tests).</summary>
    internal (int First, int Last) RealizedSlots => _panel.RealizedSlots;

    /// <summary>Item nodes made by the factory so far (tests).</summary>
    internal int CreatedItemNodes => _panel.Created;

    #endregion

    #region Layout

    /// <inheritdoc />
    internal override IReadOnlyList<IVisualTreeElement> VisualChildren =>
        _isEmpty && _emptyView is { } empty ? [_scroller, empty] : [_scroller];

    /// <inheritdoc />
    internal override void AddRenderChildren(List<ISkUiRenderable> children)
    {
        children.Add(_scroller);
        if (_isEmpty && _emptyView is { } empty)
            children.Add(empty);
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        UpdateEmpty();
        var empty = _isEmpty && _emptyView is { } view ? view.Measure(widthConstraint, heightConstraint) : Size.Zero;
        var offered = _horizontal ? widthConstraint : heightConstraint;
        _panel.SetViewport(offered, arranging: false);
        var size = _scroller.Measure(widthConstraint, heightConstraint);
        // Unbounded along the axis, the carousel is as long as its viewport (an item and the peek insets), not its strip.
        var length = _panel.MeasuredViewport(offered);
        size = _horizontal ? new Size(Math.Min(size.Width, length), size.Height) : new Size(size.Width, Math.Min(size.Height, length));
        return new Size(Math.Max(size.Width, empty.Width), Math.Max(size.Height, empty.Height));
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        var bounds = new Rect(0, 0, size.Width, size.Height);
        // Items that fill the carousel are as long as its arranged viewport, which the measure may not have known.
        _panel.SetViewport(_horizontal ? size.Width : size.Height, arranging: true);
        _scroller.Measure(size.Width, size.Height);
        _scroller.Arrange(bounds);
        if (_isEmpty && _emptyView is { } empty)
        {
            empty.Measure(size.Width, size.Height);
            empty.Arrange(bounds);
        }
    }

    /// <inheritdoc />
    internal override void OnEffectiveFlowDirectionChanged()
    {
        base.OnEffectiveFlowDirectionChanged();
        _requestingScroll = true;
        try { _panel.Place(_position); } // the offset is the other direction's: only the position says where the strip is
        finally { _requestingScroll = false; }
    }

    #endregion

    #region Keyboard and screen readers

    /// <summary>A step towards larger offsets (<paramref name="forward"/>), as the next or previous item in the reading direction.</summary>
    private bool LogicalForward(bool forward) => _horizontal && IsRightToLeft ? !forward : forward;

    bool ISkUiScrollStepper.CanStep(bool forward)
    {
        if (PageCount <= 1)
            return false;
        return _panel.Loops || (LogicalForward(forward) ? _position < PageStart(ItemCount - 1) : _position > 0);
    }

    bool ISkUiScrollStepper.Step(bool forward, bool page)
    {
        if (!((ISkUiScrollStepper)this).CanStep(forward))
            return false;
        var last = PageStart(ItemCount - 1);
        var next = _position + (LogicalForward(forward) ? _perPage : -_perPage);
        StepTo(next > last ? 0 : next < 0 ? last : next, LogicalForward(forward), animated: true);
        return true;
    }

    bool ISkUiScrollStepper.StepToEdge(bool end)
    {
        var count = ItemCount;
        var target = end ? PageStart(count - 1) : 0;
        if (count == 0 || target == _position)
            return false;
        _ = ScrollToIndex(target);
        return true;
    }

    #endregion

    #region Autoplay

    private SkUiAutoPlay? _autoPlay;
    private TimeSpan _autoPlayInterval;
    private bool _isAutoPlayPaused;
    private bool _hiddenAtTick;

    /// <summary>
    /// When positive, the carousel moves by itself (see <see cref="SkUiCarouselView.AutoPlayInterval"/>): one snap point (or
    /// page) forward per interval, waiting while the user scrolls, hovers or has keyboard focus inside, while it is hidden or
    /// out of the tree, while the app is in the background and while a screen reader runs. Whether a Core carousel is shown
    /// through its ancestors is read at each step: shown again, it waits at least a full interval. Default
    /// <see cref="TimeSpan.Zero"/>: off.
    /// </summary>
    public TimeSpan AutoPlayInterval { get => _autoPlayInterval; set => SetAutoPlayInterval(value); }

    /// <summary>Whether autoplay is paused by the app (default <c>false</c>); unpaused, the next step comes a full interval later.</summary>
    public bool IsAutoPlayPaused { get => _isAutoPlayPaused; set => SetIsAutoPlayPaused(value); }

    /// <summary>Sets <see cref="AutoPlayInterval"/>.</summary>
    public SkUiCoreCarouselView SetAutoPlayInterval(TimeSpan value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero);
        if (!SetProperty(ref _autoPlayInterval, value, nameof(AutoPlayInterval))) return this;
        var autoPlay = _autoPlay ??= new SkUiAutoPlay(this) { IsPaused = _isAutoPlayPaused };
        autoPlay.Interval = value;
        autoPlay.IsShown = IsInTree();
        _hiddenAtTick = false;
        autoPlay.Restart();
        return this;
    }

    /// <summary>Sets <see cref="IsAutoPlayPaused"/>.</summary>
    public SkUiCoreCarouselView SetIsAutoPlayPaused(bool value)
    {
        if (!SetProperty(ref _isAutoPlayPaused, value, nameof(IsAutoPlayPaused)) || _autoPlay is not { } autoPlay) return this;
        autoPlay.IsPaused = value;
        autoPlay.Restart();
        return this;
    }

    /// <summary>Whether an autoplay step is scheduled (tests).</summary>
    internal bool IsAutoPlayScheduled => _autoPlay?.IsScheduled == true;

    /// <summary>Whether the carousel is in a tree that a drawn view hosts.</summary>
    private bool IsInTree()
    {
        for (ISkUiRenderable? node = this; node is not null; node = node.RenderParent)
            if (node is SkUiView)
                return true;
        return false;
    }

    /// <inheritdoc />
    protected override void OnAnimationRootChanged(bool subtreeDetached = false)
    {
        base.OnAnimationRootChanged(subtreeDetached);
        RestartAutoPlay();
    }

    /// <inheritdoc />
    protected override void OnIsVisibleChanged()
    {
        base.OnIsVisibleChanged();
        RestartAutoPlay();
    }

    /// <summary>The carousel moved into or out of a tree, or was shown or hidden: a fresh interval, or none.</summary>
    private void RestartAutoPlay()
    {
        if (_autoPlay is not { } autoPlay)
            return;
        autoPlay.IsShown = IsInTree() && IsVisible;
        _hiddenAtTick = false;
        autoPlay.Restart();
    }

    bool ISkUiAutoPlayHost.IsAutoPlayHeld
    {
        get
        {
            // Hidden through an ancestor (not followed): seen at a tick, and once shown again a full interval passes first.
            if (!IsShownNow())
            {
                _hiddenAtTick = true;
                return true;
            }
            if (_hiddenAtTick)
            {
                _hiddenAtTick = false;
                return true;
            }
            return PageCount <= 1 || Controller.IsMoving || Controller.Dragging || _hovered
                || SkUiSemantics.FocusedInside(this) is not null || SkUiSemantics.IsScrolledOut(this);
        }
    }

    void ISkUiAutoPlayHost.AutoPlayStep(bool animated)
    {
        var next = _position + _perPage;
        StepTo(next > PageStart(ItemCount - 1) ? 0 : next, forward: true, animated);
    }

    /// <inheritdoc />
    internal override void OnPointerOverChanged(bool isOver)
    {
        base.OnPointerOverChanged(isOver);
        _hovered = isOver;
    }

    /// <summary>The scroller is hovered (it covers the carousel): a mouse or pen is over the items.</summary>
    internal void OnScrollerHovered(bool isOver) => _hovered = isOver;

    #endregion
}
