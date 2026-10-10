using System.Collections;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MauiSkiaUi.Rendering;

namespace MauiSkiaUi;

/// <summary>
/// A drawn carousel: a scrollable strip of items, one current item, snapping item by item, looping past the last item
/// back to the first (<see cref="Loop"/>, MAUI's default) or stopping at the ends. MAUI's <c>CarouselView</c> API where it
/// fits (<see cref="ItemsSource"/>, <see cref="ItemTemplate"/>, <see cref="Position"/>, <see cref="CurrentItem"/> and their
/// events and commands, <see cref="PeekAreaInsets"/>, <see cref="IndicatorView"/>, the item visual states), with the
/// items layout as properties of its own (<see cref="Orientation"/>, <see cref="ItemSpacing"/>,
/// <see cref="SnapPointsType"/>, <see cref="SnapPointsAlignment"/>), and beyond MAUI: several items in view
/// (<see cref="ItemExtent"/>), effects as items scroll (<see cref="ItemEffect"/>: cover flow, scaling) and loading more
/// items as the end comes near (<see cref="RemainingItemsThreshold"/>). In XAML the element's content is the
/// <see cref="ItemTemplate"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Layout.</b> By default each item fills the carousel along its axis, minus <see cref="PeekAreaInsets"/> (the neighbors
/// peek into the insets), and is centered when current. With <see cref="ItemExtent"/> every item is that long and as many
/// show as fit, <see cref="ItemSpacing"/> apart; <see cref="SnapPointsAlignment"/> says where the current item lines up
/// (<see cref="Microsoft.Maui.Controls.SnapPointsAlignment.Start"/> suits several items in view). Items are as tall as the
/// carousel (as wide, vertically); measured without a height, the carousel is as tall as its tallest item in view.
/// </para>
/// <para>
/// <b>Virtual.</b> Only the items in view and one on either side exist (more with an effect that shows more); their views
/// are recycled per template as the carousel scrolls. Looping repeats the items along a long strip whose middle the
/// carousel returns to as it scrolls (a correction of the offset that nothing shows, also during flings), so it scrolls
/// on in either direction without an end. Scrolling, flings and snaps run on the render thread, as in the scroll view;
/// <see cref="Position"/> follows the item at the snap point while the carousel scrolls.
/// </para>
/// <para>
/// <b>Effects</b> (<see cref="ItemEffect"/>) are evaluated by the compositor for each item from the scroll offset in every
/// frame: scrolling records nothing, and the effect stays smooth while the UI thread is busy.
/// </para>
/// </remarks>
[ContentProperty(nameof(ItemTemplate))]
public partial class SkUiCarouselView : SkUiView, ISkUiScrollStepper
{
    /// <summary>The visual state of the current item's view.</summary>
    public const string CurrentItemVisualState = "CurrentItem";

    /// <summary>The visual state of the view of the item after the current one.</summary>
    public const string NextItemVisualState = "NextItem";

    /// <summary>The visual state of the view of the item before the current one.</summary>
    public const string PreviousItemVisualState = "PreviousItem";

    /// <summary>The visual state of the other items' views.</summary>
    public const string DefaultItemVisualState = "DefaultItem";

    private readonly CarouselScroller _scroller;
    private readonly ItemsPanel _panel;
    private readonly SkUiContentView _emptyHost;
    private readonly SkUiContentSlot _emptySlot;
    private readonly SkUiWeakListener<SkUiCarouselView> _sourceListener;
    private readonly SkUiWeakListener<SkUiCarouselView> _indicatorListener;
    private IList? _source;
    private DataTemplate? _itemTemplate;
    private bool _horizontal = true;
    private bool _loop = true;
    private bool _scrollAnimated = true;
    private int _position;
    private bool _syncing;
    private bool? _requestedAnimation;
    private int _targetIndex = -1;
    private bool _requestingScroll;
    private Task _lastScroll = Task.CompletedTask;
    private double _scrollPosition;
    private bool _isDragging;
    private int _remainingItemsThreshold = -1;
    private int _thresholdCount = -1;
    private SkUiIndicatorView? _indicator;
    private SkUiCarouselEffect? _effect;
    private bool _isEmpty = true;

    /// <summary>Creates an empty carousel (GPU-backed when it is a surface of its own).</summary>
    public SkUiCarouselView()
    {
        HwAccelerated = true;
        _emptySlot = new SkUiContentSlot(this, EmptyViewProperty, EmptyViewTemplateProperty);
        _sourceListener = new SkUiWeakListener<SkUiCarouselView>(this, static (carousel, change) =>
        {
            if (change.Kind == SkUiChangeKind.Collection)
                carousel.OnSourceChanged();
        });
        _indicatorListener = new SkUiWeakListener<SkUiCarouselView>(this, static (carousel, change) =>
        {
            if (change.PropertyName == nameof(SkUiIndicatorView.Position) && change.Sender is SkUiIndicatorView indicator)
                carousel.OnIndicatorPositionChanged(indicator);
        });
        _panel = new ItemsPanel(this);
        _scroller = new CarouselScroller(this)
        {
            Orientation = ScrollOrientation.Horizontal,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
            VerticalScrollBarVisibility = ScrollBarVisibility.Never,
            SnapPointsType = SnapPointsType.MandatorySingle,
            Content = _panel
        };
        Controller.Stepper = this;
        Controller.DraggingChanged += _ => UpdateDragging();
        _scroller.RegisterScrollListener(_panel);
        _scroller.Scrolled += (_, _) => OnScrolled();
        _scroller.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(IsScrolling))
                OnMovingChanged();
        };
        _emptyHost = new SkUiContentView();
        AttachChild(_scroller);
        AttachChild(_emptyHost);
    }

    private SkUiScrollController Controller => ((ISkUiScrollHost)_scroller).Scroller;

    #region Items

    /// <summary>Bindable property for <see cref="ItemsSource"/>.</summary>
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable), typeof(SkUiCarouselView), null,
        propertyChanged: (view, _, value) => ((SkUiCarouselView)view).OnItemsSourceChanged((IEnumerable?)value));

    /// <summary>Bindable property for <see cref="ItemTemplate"/>.</summary>
    public static readonly BindableProperty ItemTemplateProperty = BindableProperty.Create(nameof(ItemTemplate), typeof(DataTemplate), typeof(SkUiCarouselView), null,
        propertyChanged: (view, _, value) => ((SkUiCarouselView)view).OnItemTemplateChanged((DataTemplate?)value));

    /// <summary>
    /// The items, one view each from <see cref="ItemTemplate"/>, with the item as its binding context. A list
    /// (<see cref="IList"/>) is read by index; other sequences are copied. With
    /// <see cref="System.Collections.Specialized.INotifyCollectionChanged"/> (listened to weakly) every change keeps the current
    /// item where it shows (or, when it was removed, the position), and items appended while the carousel flings do not stop
    /// the fling.
    /// </summary>
    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>
    /// Creates an item's view (drawn SkUi* views only); a <see cref="DataTemplateSelector"/> chooses per item, and views are
    /// recycled per selected template. Without a template each item shows its text in an <see cref="SkUiLabel"/>.
    /// </summary>
    public DataTemplate? ItemTemplate { get => (DataTemplate?)GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }

    /// <summary>The number of items.</summary>
    public int ItemCount => _source?.Count ?? 0;

    /// <summary>Sets <see cref="ItemsSource"/> (same as the property setter).</summary>
    public SkUiCarouselView SetItemsSource(IEnumerable? value) { ItemsSource = value; return this; }

    /// <summary>Sets <see cref="ItemTemplate"/> (same as the property setter).</summary>
    public SkUiCarouselView SetItemTemplate(DataTemplate? value) { ItemTemplate = value; return this; }

    private void OnItemsSourceChanged(IEnumerable? value)
    {
        _sourceListener.Listen(value as System.Collections.Specialized.INotifyCollectionChanged);
        // Recycled views keep their last item until rebound (as MAUI's); items of a replaced source go.
        _panel.ClearPool();
        ItemsChanged(value, keepCurrentItem: false);
    }

    private void OnSourceChanged() => ItemsChanged(ItemsSource, keepCurrentItem: true);

    private void OnItemTemplateChanged(DataTemplate? value)
    {
        _itemTemplate = value;
        // Views of the old templates go; the items are realized again with the new ones.
        _panel.ReleaseAll();
        _panel.ClearPool();
        _panel.Refresh();
    }

    /// <summary>
    /// The items changed: read again; the current item stays current where it shows (<paramref name="keepCurrentItem"/>,
    /// found by equality), else the position stays (within the items).
    /// </summary>
    private void ItemsChanged(IEnumerable? value, bool keepCurrentItem)
    {
        var previousItem = CurrentItem;
        var previousCount = _panel.Count;
        // Between two items (a fling, a drag): the strip stays that far past the current item.
        var within = _panel.HasGeometry && _panel.CurrentIndex(_position) == _position ? _panel.WithinItem : 0;
        _source = value switch
        {
            null => null,
            IList list => list,
            _ => value.Cast<object?>().ToList()
        };
        var count = ItemCount;
        var position = _position;
        if (count > 0)
        {
            var index = keepCurrentItem && previousItem is not null ? _source!.IndexOf(previousItem) : -1;
            position = index >= 0 ? index : Math.Clamp(Position, 0, count - 1);
        }
        _panel.SetCount(count, position + within);
        UpdateOverscroll();
        _indicator?.SetValue(SkUiIndicatorView.CountProperty, count);
        if (count > 0)
            ApplyPosition(_position, position, fromScroll: true);
        else
            SyncCurrentItem(null);
        UpdateEmpty();
        if (count != previousCount)
            CheckThreshold();
    }

    /// <summary>The item at <paramref name="index"/>, or <c>null</c> outside the items.</summary>
    private object? ItemAt(int index) => _source is { } source && index >= 0 && index < source.Count ? source[index] : null;

    /// <summary>The view the item template created for the item at <paramref name="index"/> while it shows (the one nearest the current place when the items repeat); else <c>null</c>.</summary>
    public ISkUiView? GetRealizedView(int index) => _panel.RealizedView(index);

    #endregion

    #region Position

    /// <summary>Bindable property for <see cref="Position"/> (two-way by default: scrolling sets it).</summary>
    public static readonly BindableProperty PositionProperty = BindableProperty.Create(nameof(Position), typeof(int), typeof(SkUiCarouselView), 0, BindingMode.TwoWay,
        validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, old, value) => ((SkUiCarouselView)view).OnPositionChanged((int)old, (int)value));

    /// <summary>Bindable property for <see cref="CurrentItem"/> (two-way by default: scrolling sets it).</summary>
    public static readonly BindableProperty CurrentItemProperty = BindableProperty.Create(nameof(CurrentItem), typeof(object), typeof(SkUiCarouselView), null, BindingMode.TwoWay,
        propertyChanged: (view, old, value) => ((SkUiCarouselView)view).OnCurrentItemChanged(old, value));

    /// <summary>Bindable property for <see cref="PositionChangedCommand"/>.</summary>
    public static readonly BindableProperty PositionChangedCommandProperty = BindableProperty.Create(nameof(PositionChangedCommand), typeof(ICommand), typeof(SkUiCarouselView));

    /// <summary>Bindable property for <see cref="PositionChangedCommandParameter"/>.</summary>
    public static readonly BindableProperty PositionChangedCommandParameterProperty = BindableProperty.Create(nameof(PositionChangedCommandParameter), typeof(object), typeof(SkUiCarouselView));

    /// <summary>Bindable property for <see cref="CurrentItemChangedCommand"/>.</summary>
    public static readonly BindableProperty CurrentItemChangedCommandProperty = BindableProperty.Create(nameof(CurrentItemChangedCommand), typeof(ICommand), typeof(SkUiCarouselView));

    /// <summary>Bindable property for <see cref="CurrentItemChangedCommandParameter"/>.</summary>
    public static readonly BindableProperty CurrentItemChangedCommandParameterProperty = BindableProperty.Create(nameof(CurrentItemChangedCommandParameter), typeof(object), typeof(SkUiCarouselView));

    /// <summary>Bindable property for <see cref="IsScrollAnimated"/>.</summary>
    public static readonly BindableProperty IsScrollAnimatedProperty = BindableProperty.Create(nameof(IsScrollAnimated), typeof(bool), typeof(SkUiCarouselView), true,
        propertyChanged: (view, _, value) => ((SkUiCarouselView)view)._scrollAnimated = (bool)value);

    /// <summary>
    /// The current item's index (default 0): the item at the snap point. Scrolling sets it as another item reaches the snap
    /// point; setting it scrolls there (animated with <see cref="IsScrollAnimated"/>; a looping carousel takes the shorter
    /// way round) and keeps it during that scroll. Beyond the last item it becomes the last; set before the items, it applies
    /// once they come.
    /// </summary>
    public int Position { get => (int)GetValue(PositionProperty); set => SetValue(PositionProperty, value); }

    /// <summary>The current item (default <c>null</c>): the item at <see cref="Position"/>. Setting an item of the list scrolls to it; others are ignored.</summary>
    public object? CurrentItem { get => GetValue(CurrentItemProperty); set => SetValue(CurrentItemProperty, value); }

    /// <summary>Runs with <see cref="PositionChangedCommandParameter"/> after <see cref="Position"/> changes (when it can execute).</summary>
    public ICommand? PositionChangedCommand { get => (ICommand?)GetValue(PositionChangedCommandProperty); set => SetValue(PositionChangedCommandProperty, value); }

    /// <summary>The parameter of <see cref="PositionChangedCommand"/>.</summary>
    public object? PositionChangedCommandParameter { get => GetValue(PositionChangedCommandParameterProperty); set => SetValue(PositionChangedCommandParameterProperty, value); }

    /// <summary>Runs with <see cref="CurrentItemChangedCommandParameter"/> after <see cref="CurrentItem"/> changes (when it can execute).</summary>
    public ICommand? CurrentItemChangedCommand { get => (ICommand?)GetValue(CurrentItemChangedCommandProperty); set => SetValue(CurrentItemChangedCommandProperty, value); }

    /// <summary>The parameter of <see cref="CurrentItemChangedCommand"/>.</summary>
    public object? CurrentItemChangedCommandParameter { get => GetValue(CurrentItemChangedCommandParameterProperty); set => SetValue(CurrentItemChangedCommandParameterProperty, value); }

    /// <summary>Whether setting <see cref="Position"/> or <see cref="CurrentItem"/> scrolls with an animation (default <c>true</c>).</summary>
    public bool IsScrollAnimated { get => (bool)GetValue(IsScrollAnimatedProperty); set => SetValue(IsScrollAnimatedProperty, value); }

    /// <summary>
    /// Where the carousel is, in items: the index of the item at the snap point, fractional while it scrolls (2.5: halfway
    /// from the third item to the fourth). A looping carousel's runs from 0 up to <see cref="ItemCount"/> and wraps. For
    /// effects of the app's own that follow the scrolling (a background that moves with it).
    /// </summary>
    public double ScrollPosition => _scrollPosition;

    /// <summary>Raised after <see cref="Position"/> changes (MAUI's arguments).</summary>
    public event EventHandler<PositionChangedEventArgs>? PositionChanged;

    /// <summary>Raised after <see cref="CurrentItem"/> changes (MAUI's arguments).</summary>
    public event EventHandler<CurrentItemChangedEventArgs>? CurrentItemChanged;

    /// <summary>
    /// Raised as the carousel scrolls (MAUI's arguments): deltas and offsets of the scroller, the first and last items in view
    /// and the item at the snap point.
    /// </summary>
    public event EventHandler<ItemsViewScrolledEventArgs>? Scrolled;

    /// <summary>Sets <see cref="Position"/> (same as the property setter).</summary>
    public SkUiCarouselView SetPosition(int value) { ArgumentOutOfRangeException.ThrowIfNegative(value); Position = value; return this; }

    /// <summary>Sets <see cref="CurrentItem"/> (same as the property setter).</summary>
    public SkUiCarouselView SetCurrentItem(object? value) { CurrentItem = value; return this; }

    /// <summary>Sets <see cref="IsScrollAnimated"/> (same as the property setter).</summary>
    public SkUiCarouselView SetIsScrollAnimated(bool value) { IsScrollAnimated = value; return this; }

    /// <summary>
    /// Scrolls to the item at <paramref name="index"/> (it becomes <see cref="Position"/>), animated on the render thread or
    /// at once; a looping carousel takes the shorter way round. The task completes when the scroll ends; a touch or a newer
    /// scroll cancels it. Before the first layout the carousel starts there.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not an item.</exception>
    public Task ScrollToIndex(int index, bool animated = true)
    {
        if (index < 0 || index >= ItemCount)
            throw new ArgumentOutOfRangeException(nameof(index), index, "The index is not an item of the carousel.");
        _requestedAnimation = animated;
        try
        {
            if (Position != index)
                Position = index;
            else
                ScrollToCurrent(animated);
        }
        finally
        {
            _requestedAnimation = null;
        }
        return _lastScroll;
    }

    /// <summary>Scrolls to <paramref name="item"/> (found with <see cref="object.Equals(object?)"/>); see <see cref="ScrollToIndex"/>. An item that is not in the list is ignored.</summary>
    public Task ScrollToItem(object? item, bool animated = true)
    {
        var index = _source?.IndexOf(item) ?? -1;
        return index < 0 ? Task.CompletedTask : ScrollToIndex(index, animated);
    }

    /// <summary>
    /// MAUI's <c>ItemsView.ScrollTo</c>: scrolls to the item at <paramref name="index"/> (<see cref="ScrollToIndex"/>). The group
    /// and position are ignored (a carousel has no groups, and the item lines up at the snap point).
    /// </summary>
    public void ScrollTo(int index, int groupIndex = -1, ScrollToPosition position = ScrollToPosition.MakeVisible, bool animate = true)
    {
        if (index >= 0 && index < ItemCount)
            _ = ScrollToIndex(index, animate);
    }

    /// <summary>MAUI's <c>ItemsView.ScrollTo</c>: scrolls to <paramref name="item"/> (<see cref="ScrollToItem"/>); the group and position are ignored.</summary>
    public void ScrollTo(object item, object? group = null, ScrollToPosition position = ScrollToPosition.MakeVisible, bool animate = true) =>
        _ = ScrollToItem(item, animate);

    private void OnPositionChanged(int previous, int value)
    {
        if (_syncing)
            return;
        var count = ItemCount;
        if (count > 0 && value >= count)
        {
            Position = count - 1;
            return;
        }
        ApplyPosition(previous, value, fromScroll: false);
    }

    private void OnCurrentItemChanged(object? previous, object? value)
    {
        if (_syncing)
            return;
        var index = _source?.IndexOf(value) ?? -1;
        if (index < 0)
        {
            // Not an item: the current item stays.
            SyncCurrentItem(previous);
            return;
        }
        RaiseCurrentItemChanged(previous, value);
        if (index != Position)
            Position = index;
    }

    /// <summary>
    /// <paramref name="value"/> is the position now (set by the app, or reached by scrolling: <paramref name="fromScroll"/>):
    /// the current item, events, commands, the indicator and the items' visual states follow; set by the app, the carousel
    /// scrolls there.
    /// </summary>
    private void ApplyPosition(int previous, int value, bool fromScroll)
    {
        _position = value;
        if (Position != value)
        {
            _syncing = true;
            try { Position = value; }
            finally { _syncing = false; }
        }
        var previousItem = CurrentItem;
        var item = ItemAt(value);
        if (!Equals(previousItem, item))
        {
            SyncCurrentItem(item);
            RaiseCurrentItemChanged(previousItem, item);
        }
        if (previous != value)
            RaisePositionChanged(previous, value);
        if (_indicator is { } indicator && indicator.Position != value)
            indicator.Position = value;
        _panel.UpdateVisualStates();
        if (!fromScroll)
            ScrollToCurrent(_requestedAnimation ?? _scrollAnimated);
        if (!Controller.IsMoving)
            FollowWithIndicator();
        CheckThreshold();
        InvalidateSemantics();
    }

    /// <summary>Sets <see cref="CurrentItem"/> without treating it as the app's request.</summary>
    private void SyncCurrentItem(object? item)
    {
        if (Equals(CurrentItem, item))
            return;
        _syncing = true;
        try { CurrentItem = item; }
        finally { _syncing = false; }
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
            _lastScroll = _panel.ScrollToIndex(_position, animated && IsShown);
        }
        finally
        {
            _requestingScroll = false;
        }
        if (_lastScroll.IsCompleted)
            _targetIndex = -1;
    }

    private void RaisePositionChanged(int previous, int value)
    {
        if (PositionChanged is { } handler && TryCreatePositionArgs(previous, value) is { } args)
            handler(this, args);
        if (PositionChangedCommand is { } command && command.CanExecute(PositionChangedCommandParameter))
            command.Execute(PositionChangedCommandParameter);
    }

    private void RaiseCurrentItemChanged(object? previous, object? value)
    {
        if (CurrentItemChanged is { } handler && TryCreateCurrentItemArgs(previous, value) is { } args)
            handler(this, args);
        if (CurrentItemChangedCommand is { } command && command.CanExecute(CurrentItemChangedCommandParameter))
            command.Execute(CurrentItemChangedCommandParameter);
    }

    private static PositionChangedEventArgs? TryCreatePositionArgs(int previous, int value)
    {
        try
        {
            return CreatePositionChangedArgs(previous, value);
        }
        catch (MissingMethodException)
        {
            return null;
        }
    }

    private static CurrentItemChangedEventArgs? TryCreateCurrentItemArgs(object? previous, object? value)
    {
        try
        {
            return CreateCurrentItemChangedArgs(previous, value);
        }
        catch (MissingMethodException)
        {
            return null;
        }
    }

    // MAUI's event arguments have internal constructors only; the accessors are trimming- and Native-AOT-safe.
    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    private static extern PositionChangedEventArgs CreatePositionChangedArgs(int previousPosition, int currentPosition);

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    private static extern CurrentItemChangedEventArgs CreateCurrentItemChangedArgs(object? previousItem, object? currentItem);

    #endregion

    #region Scrolling

    /// <summary>Bindable property for <see cref="IsSwipeEnabled"/>.</summary>
    public static readonly BindableProperty IsSwipeEnabledProperty = BindableProperty.Create(nameof(IsSwipeEnabled), typeof(bool), typeof(SkUiCarouselView), true,
        propertyChanged: (view, _, value) => ((SkUiCarouselView)view).OnSwipeEnabledChanged((bool)value));

    /// <summary>Bindable property for <see cref="IsBounceEnabled"/>.</summary>
    public static readonly BindableProperty IsBounceEnabledProperty = BindableProperty.Create(nameof(IsBounceEnabled), typeof(bool), typeof(SkUiCarouselView), true,
        propertyChanged: (view, _, _) => ((SkUiCarouselView)view).UpdateOverscroll());

    /// <summary>Whether drags and the wheel scroll the carousel (default <c>true</c>); <see cref="Position"/>, the keyboard and screen readers always do.</summary>
    public bool IsSwipeEnabled { get => (bool)GetValue(IsSwipeEnabledProperty); set => SetValue(IsSwipeEnabledProperty, value); }

    /// <summary>
    /// Whether a carousel that does not loop bounces (or stretches, per the look's <see cref="SkUiLook.DefaultOverscroll"/>)
    /// past its first and last items (default <c>true</c>).
    /// </summary>
    public bool IsBounceEnabled { get => (bool)GetValue(IsBounceEnabledProperty); set => SetValue(IsBounceEnabledProperty, value); }

    /// <summary>Whether the user drags the carousel now.</summary>
    public bool IsDragging => _isDragging;

    /// <summary>Whether the carousel is dragged, flung or animated.</summary>
    public bool IsScrolling => _scroller.IsScrolling;

    /// <summary>Sets <see cref="IsSwipeEnabled"/> (same as the property setter).</summary>
    public SkUiCarouselView SetIsSwipeEnabled(bool value) { IsSwipeEnabled = value; return this; }

    /// <summary>Sets <see cref="IsBounceEnabled"/> (same as the property setter).</summary>
    public SkUiCarouselView SetIsBounceEnabled(bool value) { IsBounceEnabled = value; return this; }

    private void OnSwipeEnabledChanged(bool value)
    {
        Controller.IsUserScrollEnabled = value;
        if (!value)
            _scroller.CancelGestures();
    }

    private void UpdateOverscroll() =>
        _scroller.Overscroll = _panel.Loops || !IsBounceEnabled ? SkUiOverscrollMode.None : SkUiOverscrollMode.Default;

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
                ApplyPosition(_position, current, fromScroll: true);
        }
        FollowWithIndicator();
        if (Scrolled is { } handler)
            handler(this, _panel.CreateScrolledArgs());
        CheckThreshold();
    }

    private void OnMovingChanged()
    {
        OnPropertyChanged(nameof(IsScrolling));
        UpdateDragging();
        if (Controller.IsMoving || _requestingScroll)
            return;
        // Settled: on the snap point of the position reached (or asked for).
        _targetIndex = -1;
        if (_panel.HasGeometry && ItemCount > 0)
        {
            var current = _panel.CurrentIndex(_position);
            if (current != _position)
                ApplyPosition(_position, current, fromScroll: true);
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

    #endregion

    #region Layout properties

    /// <summary>Bindable property for <see cref="Loop"/>.</summary>
    public static readonly BindableProperty LoopProperty = BindableProperty.Create(nameof(Loop), typeof(bool), typeof(SkUiCarouselView), true,
        propertyChanged: (view, _, value) => ((SkUiCarouselView)view).OnLayoutChanged(carousel => carousel._loop = (bool)value));

    /// <summary>Bindable property for <see cref="PeekAreaInsets"/>.</summary>
    public static readonly BindableProperty PeekAreaInsetsProperty = BindableProperty.Create(nameof(PeekAreaInsets), typeof(Thickness), typeof(SkUiCarouselView), default(Thickness),
        validateValue: (_, value) => value is Thickness insets && IsValidInsets(insets),
        propertyChanged: (view, _, _) => ((SkUiCarouselView)view).OnLayoutChanged(null));

    /// <summary>Bindable property for <see cref="ItemExtent"/>.</summary>
    public static readonly BindableProperty ItemExtentProperty = BindableProperty.Create(nameof(ItemExtent), typeof(double), typeof(SkUiCarouselView), 0d,
        validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, _) => ((SkUiCarouselView)view).OnLayoutChanged(null));

    /// <summary>Bindable property for <see cref="ItemSpacing"/>.</summary>
    public static readonly BindableProperty ItemSpacingProperty = BindableProperty.Create(nameof(ItemSpacing), typeof(double), typeof(SkUiCarouselView), 0d,
        validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, _) => ((SkUiCarouselView)view).OnLayoutChanged(null));

    /// <summary>Bindable property for <see cref="Orientation"/>.</summary>
    public static readonly BindableProperty OrientationProperty = BindableProperty.Create(nameof(Orientation), typeof(ItemsLayoutOrientation), typeof(SkUiCarouselView),
        ItemsLayoutOrientation.Horizontal, validateValue: (_, value) => Enum.IsDefined((ItemsLayoutOrientation)value),
        propertyChanged: (view, _, value) => ((SkUiCarouselView)view).OnOrientationChanged((ItemsLayoutOrientation)value));

    /// <summary>Bindable property for <see cref="SnapPointsType"/>.</summary>
    public static readonly BindableProperty SnapPointsTypeProperty = BindableProperty.Create(nameof(SnapPointsType), typeof(SnapPointsType), typeof(SkUiCarouselView),
        SnapPointsType.MandatorySingle, validateValue: (_, value) => Enum.IsDefined((SnapPointsType)value),
        propertyChanged: (view, _, value) => ((SkUiCarouselView)view)._scroller.SnapPointsType = (SnapPointsType)value);

    /// <summary>Bindable property for <see cref="SnapPointsAlignment"/>.</summary>
    public static readonly BindableProperty SnapPointsAlignmentProperty = BindableProperty.Create(nameof(SnapPointsAlignment), typeof(SnapPointsAlignment), typeof(SkUiCarouselView),
        SnapPointsAlignment.Center, validateValue: (_, value) => Enum.IsDefined((SnapPointsAlignment)value),
        propertyChanged: (view, _, _) => ((SkUiCarouselView)view).OnLayoutChanged(null));

    /// <summary>
    /// Whether scrolling goes on past the last item to the first (and before the first to the last) without an end (default
    /// <c>true</c>, as MAUI's). A single item does not loop.
    /// </summary>
    public bool Loop { get => (bool)GetValue(LoopProperty); set => SetValue(LoopProperty, value); }

    /// <summary>
    /// Room at the start and end of the carousel's axis (left and right; top and bottom when vertical) where the neighbors of
    /// the current item peek in: the items are that much shorter (unless <see cref="ItemExtent"/> sets their length), and a
    /// carousel that does not loop keeps the room before its first item and after its last.
    /// </summary>
    public Thickness PeekAreaInsets { get => (Thickness)GetValue(PeekAreaInsetsProperty); set => SetValue(PeekAreaInsetsProperty, value); }

    /// <summary>
    /// When positive, the length of every item along the axis in DIPs: as many items show as fit, <see cref="ItemSpacing"/>
    /// apart. 0 (default): each item fills the carousel minus <see cref="PeekAreaInsets"/>. SkiaUi extension.
    /// </summary>
    public double ItemExtent { get => (double)GetValue(ItemExtentProperty); set => SetValue(ItemExtentProperty, value); }

    /// <summary>The gap between items along the axis in DIPs (default 0; MAUI's <c>LinearItemsLayout.ItemSpacing</c>).</summary>
    public double ItemSpacing { get => (double)GetValue(ItemSpacingProperty); set => SetValue(ItemSpacingProperty, value); }

    /// <summary>The scroll axis: horizontal (default) or vertical (MAUI's <c>LinearItemsLayout.Orientation</c>).</summary>
    public ItemsLayoutOrientation Orientation { get => (ItemsLayoutOrientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>
    /// How drags and flings end (MAUI's <c>LinearItemsLayout.SnapPointsType</c>): <see cref="SnapPointsType.MandatorySingle"/>
    /// (default) one item per swipe, <see cref="SnapPointsType.Mandatory"/> on the item nearest to where the fling would stop,
    /// <see cref="SnapPointsType.None"/> anywhere.
    /// </summary>
    public SnapPointsType SnapPointsType { get => (SnapPointsType)GetValue(SnapPointsTypeProperty); set => SetValue(SnapPointsTypeProperty, value); }

    /// <summary>
    /// Where the current item lines up (MAUI's <c>LinearItemsLayout.SnapPointsAlignment</c>): <see cref="SnapPointsAlignment.Center"/>
    /// (default) in the middle, <see cref="SnapPointsAlignment.Start"/> or <see cref="SnapPointsAlignment.End"/> at the edges,
    /// inside <see cref="PeekAreaInsets"/>. A carousel that does not loop cannot scroll its first and last items past its ends.
    /// </summary>
    public SnapPointsAlignment SnapPointsAlignment { get => (SnapPointsAlignment)GetValue(SnapPointsAlignmentProperty); set => SetValue(SnapPointsAlignmentProperty, value); }

    /// <summary>Sets <see cref="Loop"/> (same as the property setter).</summary>
    public SkUiCarouselView SetLoop(bool value) { Loop = value; return this; }

    /// <summary>Sets <see cref="PeekAreaInsets"/> (same as the property setter).</summary>
    public SkUiCarouselView SetPeekAreaInsets(Thickness value)
    {
        if (!IsValidInsets(value)) throw new ArgumentOutOfRangeException(nameof(value), value, "Insets must be finite and not negative.");
        PeekAreaInsets = value;
        return this;
    }

    /// <summary>Sets <see cref="ItemExtent"/> (same as the property setter).</summary>
    public SkUiCarouselView SetItemExtent(double value) { SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value)); ItemExtent = value; return this; }

    /// <summary>Sets <see cref="ItemSpacing"/> (same as the property setter).</summary>
    public SkUiCarouselView SetItemSpacing(double value) { SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value)); ItemSpacing = value; return this; }

    /// <summary>Sets <see cref="Orientation"/> (same as the property setter).</summary>
    public SkUiCarouselView SetOrientation(ItemsLayoutOrientation value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        Orientation = value;
        return this;
    }

    /// <summary>Sets <see cref="SnapPointsType"/> (same as the property setter).</summary>
    public SkUiCarouselView SetSnapPointsType(SnapPointsType value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        SnapPointsType = value;
        return this;
    }

    /// <summary>Sets <see cref="SnapPointsAlignment"/> (same as the property setter).</summary>
    public SkUiCarouselView SetSnapPointsAlignment(SnapPointsAlignment value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        SnapPointsAlignment = value;
        return this;
    }

    private static bool IsValidInsets(Thickness insets) =>
        double.IsFinite(insets.Left) && double.IsFinite(insets.Top) && double.IsFinite(insets.Right) && double.IsFinite(insets.Bottom)
        && insets is { Left: >= 0, Top: >= 0, Right: >= 0, Bottom: >= 0 };

    /// <summary>A setting of the items' layout changed: the current place is kept, and the items are laid out again.</summary>
    private void OnLayoutChanged(Action<SkUiCarouselView>? apply)
    {
        // The panel is null while the base constructor runs.
        _panel?.KeepCurrentPlace();
        apply?.Invoke(this);
        if (_panel is null)
            return;
        _panel.Refresh();
        UpdateOverscroll();
    }

    private void OnOrientationChanged(ItemsLayoutOrientation value)
    {
        OnLayoutChanged(carousel => carousel._horizontal = value == ItemsLayoutOrientation.Horizontal);
        _scroller.Orientation = _horizontal ? ScrollOrientation.Horizontal : ScrollOrientation.Vertical;
        if (_indicator is { } indicator)
            indicator.Orientation = _horizontal ? StackOrientation.Horizontal : StackOrientation.Vertical;
    }

    #endregion

    #region Effects

    /// <summary>Bindable property for <see cref="ItemEffect"/>.</summary>
    public static readonly BindableProperty ItemEffectProperty = BindableProperty.Create(nameof(ItemEffect), typeof(SkUiCarouselEffect), typeof(SkUiCarouselView), null,
        propertyChanged: (view, _, value) => ((SkUiCarouselView)view).OnItemEffectChanged((SkUiCarouselEffect?)value));

    /// <summary>
    /// How items change as they scroll (default <c>null</c>: they move with the scrolling, as in a scroll view): a
    /// <see cref="SkUiCoverFlowEffect"/>, a <see cref="SkUiScaleEffect"/>, or an effect of the app's own. Evaluated on the
    /// render thread for every frame (see <see cref="SkUiCarouselEffect"/>). An effect object can be shared by carousels.
    /// SkiaUi extension.
    /// </summary>
    public SkUiCarouselEffect? ItemEffect { get => (SkUiCarouselEffect?)GetValue(ItemEffectProperty); set => SetValue(ItemEffectProperty, value); }

    /// <summary>Sets <see cref="ItemEffect"/> (same as the property setter).</summary>
    public SkUiCarouselView SetItemEffect(SkUiCarouselEffect? value) { ItemEffect = value; return this; }

    private void OnItemEffectChanged(SkUiCarouselEffect? value)
    {
        if (_effect is not null)
            _effect.Changed -= OnEffectSettingsChanged;
        _effect = value;
        if (value is not null)
            value.Changed += OnEffectSettingsChanged;
        _panel.OnEffectChanged();
    }

    private void OnEffectSettingsChanged(object? sender, EventArgs args) => _panel.OnEffectChanged();

    /// <inheritdoc />
    protected override void OnParentSet()
    {
        base.OnParentSet();
        EnsureEmptyView();
    }

    #endregion

    #region Indicator

    /// <summary>Bindable property for <see cref="IndicatorView"/>.</summary>
    public static readonly BindableProperty IndicatorViewProperty = BindableProperty.Create(nameof(IndicatorView), typeof(SkUiIndicatorView), typeof(SkUiCarouselView), null,
        propertyChanged: (view, old, value) => ((SkUiCarouselView)view).OnIndicatorViewChanged((SkUiIndicatorView?)old, (SkUiIndicatorView?)value));

    /// <summary>
    /// An indicator view that shows the carousel's items (MAUI's link, usually <c>{x:Reference}</c>): its count and position
    /// follow the carousel, its selection moves with the scrolling (across the wrap of a looping carousel), and a tap on an
    /// indicator scrolls the carousel to its item. A vertical carousel makes its row vertical.
    /// </summary>
    public SkUiIndicatorView? IndicatorView { get => (SkUiIndicatorView?)GetValue(IndicatorViewProperty); set => SetValue(IndicatorViewProperty, value); }

    /// <summary>Sets <see cref="IndicatorView"/> (same as the property setter).</summary>
    public SkUiCarouselView SetIndicatorView(SkUiIndicatorView? value) { IndicatorView = value; return this; }

    private void OnIndicatorViewChanged(SkUiIndicatorView? previous, SkUiIndicatorView? value)
    {
        if (previous is not null && ReferenceEquals(previous.Carousel, this))
            previous.Link(null);
        _indicator = value;
        _indicatorListener.Listen(value);
        if (value is null)
            return;
        value.Carousel?.UnlinkIndicator(value);
        value.Link(this);
        value.Count = ItemCount;
        value.Orientation = _horizontal ? StackOrientation.Horizontal : StackOrientation.Vertical;
        value.Position = _position;
        FollowWithIndicator();
    }

    /// <summary>Another carousel links <paramref name="indicator"/>: this one lets it go.</summary>
    private void UnlinkIndicator(SkUiIndicatorView indicator)
    {
        if (!ReferenceEquals(_indicator, indicator))
            return;
        _indicator = null;
        _indicatorListener.Listen(null);
        IndicatorView = null;
    }

    /// <summary>The linked indicator's position changed: a tap (or the app) asks for an item.</summary>
    private void OnIndicatorPositionChanged(SkUiIndicatorView indicator)
    {
        if (!ReferenceEquals(indicator, _indicator) || indicator.Position == _position || ItemCount == 0)
            return;
        Position = Math.Min(indicator.Position, ItemCount - 1);
    }

    /// <summary>The indicator draws the scroll position while the carousel moves, and the position once it rests.</summary>
    private void FollowWithIndicator()
    {
        if (_indicator is not { } indicator)
            return;
        var moving = Controller.IsMoving && _panel.HasGeometry;
        indicator.Follow(moving ? _panel.ScrollPosition : _position, _panel.Loops);
    }

    #endregion

    #region Loading more

    /// <summary>Bindable property for <see cref="RemainingItemsThreshold"/>.</summary>
    public static readonly BindableProperty RemainingItemsThresholdProperty = BindableProperty.Create(nameof(RemainingItemsThreshold), typeof(int), typeof(SkUiCarouselView), -1,
        validateValue: SkUiVirtualVerticalStackLayoutBase.IsValidThreshold,
        propertyChanged: (view, _, value) => ((SkUiCarouselView)view).OnRemainingItemsThresholdChanged((int)value));

    /// <summary>Bindable property for <see cref="RemainingItemsThresholdReachedCommand"/>.</summary>
    public static readonly BindableProperty RemainingItemsThresholdReachedCommandProperty = BindableProperty.Create(nameof(RemainingItemsThresholdReachedCommand),
        typeof(ICommand), typeof(SkUiCarouselView));

    /// <summary>Bindable property for <see cref="RemainingItemsThresholdReachedCommandParameter"/>.</summary>
    public static readonly BindableProperty RemainingItemsThresholdReachedCommandParameterProperty = BindableProperty.Create(nameof(RemainingItemsThresholdReachedCommandParameter),
        typeof(object), typeof(SkUiCarouselView));

    /// <summary>
    /// When the last item in view (in a looping carousel, the current item) is this many items (or fewer) from the end,
    /// <see cref="RemainingItemsThresholdReached"/> is raised (and its command run), once per item count: load the next items,
    /// and it fires again when the new end comes near. -1 (default) never. Items appended keep a running fling going.
    /// </summary>
    public int RemainingItemsThreshold { get => (int)GetValue(RemainingItemsThresholdProperty); set => SetValue(RemainingItemsThresholdProperty, value); }

    /// <summary>Runs with <see cref="RemainingItemsThresholdReached"/> (when it can execute).</summary>
    public ICommand? RemainingItemsThresholdReachedCommand
    {
        get => (ICommand?)GetValue(RemainingItemsThresholdReachedCommandProperty);
        set => SetValue(RemainingItemsThresholdReachedCommandProperty, value);
    }

    /// <summary>The parameter of <see cref="RemainingItemsThresholdReachedCommand"/>.</summary>
    public object? RemainingItemsThresholdReachedCommandParameter
    {
        get => GetValue(RemainingItemsThresholdReachedCommandParameterProperty);
        set => SetValue(RemainingItemsThresholdReachedCommandParameterProperty, value);
    }

    /// <summary>The end of the items comes near; see <see cref="RemainingItemsThreshold"/>.</summary>
    public event EventHandler? RemainingItemsThresholdReached;

    /// <summary>Sets <see cref="RemainingItemsThreshold"/> (same as the property setter).</summary>
    public SkUiCarouselView SetRemainingItemsThreshold(int value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, -1);
        RemainingItemsThreshold = value;
        return this;
    }

    private void OnRemainingItemsThresholdChanged(int value)
    {
        _remainingItemsThreshold = value;
        _thresholdCount = -1;
        CheckThreshold();
    }

    private void CheckThreshold()
    {
        var count = ItemCount;
        if (_remainingItemsThreshold < 0 || count == 0 || _thresholdCount == count || !_panel.HasGeometry)
            return;
        var last = _panel.Loops ? _position : _panel.LastVisibleIndex;
        if (last < count - 1 - _remainingItemsThreshold)
            return;
        _thresholdCount = count;
        RemainingItemsThresholdReached?.Invoke(this, EventArgs.Empty);
        var command = RemainingItemsThresholdReachedCommand;
        var parameter = RemainingItemsThresholdReachedCommandParameter;
        if (command?.CanExecute(parameter) == true)
            command.Execute(parameter);
    }

    #endregion

    #region Empty view

    /// <summary>Bindable property for <see cref="EmptyView"/>.</summary>
    public static readonly BindableProperty EmptyViewProperty = BindableProperty.Create(nameof(EmptyView), typeof(ISkUiView), typeof(SkUiCarouselView), null,
        validateValue: (bindable, value) =>
        {
            var carousel = (SkUiCarouselView)bindable;
            if (value is ISkUiView view && !ReferenceEquals(carousel.EmptyView, view) && !ReferenceEquals(carousel._emptyHost.Content, view))
                carousel.ValidateChild(view);
            return true;
        },
        propertyChanged: (view, _, _) => ((SkUiCarouselView)view).OnEmptyViewChanged());

    /// <summary>Bindable property for <see cref="EmptyViewTemplate"/>.</summary>
    public static readonly BindableProperty EmptyViewTemplateProperty = BindableProperty.Create(nameof(EmptyViewTemplate), typeof(DataTemplate), typeof(SkUiCarouselView), null,
        propertyChanged: (view, _, _) =>
        {
            var carousel = (SkUiCarouselView)view;
            carousel._emptySlot.OnTemplateChanged();
            carousel.EnsureEmptyView();
        });

    /// <summary>A drawn view shown instead of the items while there are none (<see cref="ItemsSource"/> is <c>null</c> or empty); it fills the carousel.</summary>
    public ISkUiView? EmptyView { get => (ISkUiView?)GetValue(EmptyViewProperty); set => SetValue(EmptyViewProperty, value); }

    /// <summary>Creates <see cref="EmptyView"/> when it is not set, the first time the carousel is empty in a tree (a <see cref="DataTemplateSelector"/> chooses by the binding context).</summary>
    public DataTemplate? EmptyViewTemplate { get => (DataTemplate?)GetValue(EmptyViewTemplateProperty); set => SetValue(EmptyViewTemplateProperty, value); }

    /// <summary>Sets <see cref="EmptyView"/> (same as the property setter).</summary>
    public SkUiCarouselView SetEmptyView(ISkUiView? value)
    {
        if (!ReferenceEquals(EmptyView, value) && value is not null) ValidateChild(value);
        EmptyView = value;
        return this;
    }

    private void OnEmptyViewChanged()
    {
        _emptySlot.OnContentChanged();
        if (EmptyView is null && !_emptySlot.IsSetting)
            EnsureEmptyView();
        _emptyHost.Content = EmptyView;
    }

    private void EnsureEmptyView()
    {
        if (_isEmpty && Parent is not null)
            _emptySlot.Ensure();
    }

    private void UpdateEmpty()
    {
        var empty = ItemCount == 0;
        if (empty == _isEmpty)
            return;
        _isEmpty = empty;
        _emptyHost.IsVisible = empty;
        EnsureEmptyView();
        InvalidateMeasureOverride();
    }

    /// <inheritdoc />
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        if (_emptySlot.Reselect())
            EnsureEmptyView();
    }

    #endregion

    #region Layout

    /// <summary>The carousel's scroller (tests).</summary>
    internal SkUiScrollView ScrollView => _scroller;

    /// <summary>The panel of the items (tests).</summary>
    internal (int First, int Last) RealizedSlots => _panel.RealizedSlots;

    /// <summary>The visual state of the item view at <paramref name="index"/> (tests).</summary>
    internal string? ItemVisualState(int index) => _panel.VisualStateOf(index);

    /// <summary>The panel's item layout (tests): item length, stride, slots and copies of the items.</summary>
    internal (double Extent, double Stride, int Slots, int Copies) Geometry => _panel.GeometryInfo;

    /// <inheritdoc />
    internal override IEnumerable<ISkUiView> SkiaChildren
    {
        get
        {
            yield return _scroller;
            yield return _emptyHost;
        }
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        UpdateEmpty();
        var empty = _isEmpty ? ((IView)_emptyHost).Measure(widthConstraint, heightConstraint) : Size.Zero;
        _panel.SetViewport(_horizontal ? widthConstraint : heightConstraint);
        var size = ((IView)_scroller).Measure(widthConstraint, heightConstraint);
        // Unbounded along the axis, the carousel is as long as its viewport (an item and the peek insets), not its strip.
        size = _horizontal ? new Size(Math.Min(size.Width, _panel.ViewportLength), size.Height) : new Size(size.Width, Math.Min(size.Height, _panel.ViewportLength));
        return new Size(Math.Max(size.Width, empty.Width), Math.Max(size.Height, empty.Height));
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        var bounds = new Rect(0, 0, size.Width, size.Height);
        // Items that fill the carousel are as long as its arranged viewport, which the measure may not have known.
        _panel.SetViewport(_horizontal ? size.Width : size.Height);
        ((IView)_scroller).Measure(size.Width, size.Height);
        ((IView)_scroller).Arrange(bounds);
        if (_isEmpty)
        {
            ((IView)_emptyHost).Measure(size.Width, size.Height);
            ((IView)_emptyHost).Arrange(bounds);
        }
    }

    #endregion

    #region Keyboard and screen readers

    /// <summary>A step towards larger offsets (<paramref name="forward"/>), as the next or previous item in the reading direction.</summary>
    private bool LogicalForward(bool forward) => _horizontal && IsRightToLeft ? !forward : forward;

    bool ISkUiScrollStepper.CanStep(bool forward)
    {
        var count = ItemCount;
        if (count <= 1)
            return false;
        return _panel.Loops || (LogicalForward(forward) ? _position < count - 1 : _position > 0);
    }

    bool ISkUiScrollStepper.Step(bool forward, bool page)
    {
        if (!((ISkUiScrollStepper)this).CanStep(forward))
            return false;
        var count = ItemCount;
        var next = _position + (LogicalForward(forward) ? 1 : -1);
        _ = ScrollToIndex(_panel.Loops ? (next + count) % count : Math.Clamp(next, 0, count - 1));
        return true;
    }

    bool ISkUiScrollStepper.StepToEdge(bool end)
    {
        var count = ItemCount;
        var target = end ? count - 1 : 0;
        if (count == 0 || target == _position)
            return false;
        _ = ScrollToIndex(target);
        return true;
    }

    #endregion
}
