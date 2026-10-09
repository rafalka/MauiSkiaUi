using MauiSkiaUi.Rendering;
using ISwipeItem = Microsoft.Maui.Controls.ISwipeItem;

namespace MauiSkiaUi;

/// <summary>
/// Content with swipe actions (MAUI's <c>SwipeView</c>): swiping the <see cref="Content"/> reveals the
/// <see cref="SwipeItems"/> on that side (<see cref="LeftItems"/> when swiped to the right, <see cref="TopItems"/> when
/// swiped down, …). Items are MAUI's <see cref="SwipeItem"/>s, drawn as buttons with their text, icon and background, or
/// drawn views (<see cref="SkUiSwipeItemView"/>); MAUI's <c>SwipeItemView</c> hosts native views and is not supported.
/// </summary>
/// <remarks>
/// <para>
/// As MAUI's handlers: in <see cref="SwipeMode.Reveal"/> a release past <see cref="Threshold"/> (default 60 % of the items'
/// width) leaves the items open, and a fast swipe opens them too; tapping an item invokes it. In
/// <see cref="SwipeMode.Execute"/> a release past the threshold invokes the first visible item. An invoked item closes the
/// view unless its <see cref="SwipeItems.SwipeBehaviorOnInvoked"/> is <see cref="SwipeBehaviorOnInvoked.RemainOpen"/>.
/// A tap on the content while open closes it (the content does not get it), as does scrolling a drawn scroller around it or
/// a new binding context (a recycled list row).
/// </para>
/// <para>
/// The swipe competes with scrolling through the gesture arena: a horizontal drag reveals left or right items, a vertical
/// one scrolls (or reveals top or bottom items when there are any). Content and items move at composite time (no
/// re-recording while dragging); items are created when their side is first revealed. Left and right are physical sides,
/// also in right-to-left layouts.
/// </para>
/// </remarks>
[ContentProperty(nameof(Content))]
public partial class SkUiSwipeView : SkUiView
{
    /// <summary>Width of a <see cref="SwipeItem"/> beside the content in <see cref="SwipeMode.Reveal"/> (MAUI's handlers' size).</summary>
    internal const double SwipeItemWidth = 100;

    /// <summary>The part of the open distance a release must pass when <see cref="Threshold"/> is not set (MAUI's handlers').</summary>
    internal const double DefaultThresholdFraction = 0.6;

    /// <summary>The open distance of <see cref="SwipeMode.Execute"/> items, as a part of the content's length (MAUI's handlers').</summary>
    internal const double ExecuteOpenFraction = 0.8;

    /// <summary>Length of the open and close animations in milliseconds (MAUI's handlers').</summary>
    internal const uint SettleLength = 200;

    private static readonly OpenSwipeItem[] Sides = [OpenSwipeItem.LeftItems, OpenSwipeItem.RightItems, OpenSwipeItem.TopItems, OpenSwipeItem.BottomItems];

    private readonly ContentPart _contentPart;
    private readonly SkUiTween _motion; // the distance shown while settling
    private readonly SkUiWeakListener<SkUiSwipeView>[] _itemsListeners = new SkUiWeakListener<SkUiSwipeView>[4];
    private readonly ItemsPart?[] _parts = new ItemsPart?[4];
    private SwipeGesture? _gesture;
    private SkUiTapGestureRecognizer? _closeTap;
    private ItemsPart? _shownPart; // the side shown (revealed, open, or settling); null when closed
    private double _shown; // how far the content is moved off its place, DIPs (>= 0)
    private double _dragStart; // the signed offset when the current drag started
    private bool _open;
    private bool _dragging;
    private Thickness _padding;
    private Rect _contentRect;
    private List<SkUiScrollController>? _watchedScrollers;
    private readonly Action<bool> _ancestorMovingChanged;

    /// <summary>Bindable <see cref="Content"/>.</summary>
    public static readonly BindableProperty ContentProperty = BindableProperty.Create(
        nameof(Content), typeof(ISkUiView), typeof(SkUiSwipeView), null,
        validateValue: (bindable, value) =>
        {
            if (value is ISkUiView child && !ReferenceEquals(((SkUiSwipeView)bindable).Content, child))
                ((SkUiSwipeView)bindable).ValidateChild(child);
            return true;
        },
        propertyChanged: (bindable, _, newValue) => ((SkUiSwipeView)bindable)._contentPart.Content = (ISkUiView?)newValue);

    /// <summary>Bindable <see cref="LeftItems"/>.</summary>
    public static readonly BindableProperty LeftItemsProperty = CreateItemsProperty(nameof(LeftItems), OpenSwipeItem.LeftItems);

    /// <summary>Bindable <see cref="RightItems"/>.</summary>
    public static readonly BindableProperty RightItemsProperty = CreateItemsProperty(nameof(RightItems), OpenSwipeItem.RightItems);

    /// <summary>Bindable <see cref="TopItems"/>.</summary>
    public static readonly BindableProperty TopItemsProperty = CreateItemsProperty(nameof(TopItems), OpenSwipeItem.TopItems);

    /// <summary>Bindable <see cref="BottomItems"/>.</summary>
    public static readonly BindableProperty BottomItemsProperty = CreateItemsProperty(nameof(BottomItems), OpenSwipeItem.BottomItems);

    /// <summary>Bindable <see cref="Threshold"/>.</summary>
    public static readonly BindableProperty ThresholdProperty = BindableProperty.Create(
        nameof(Threshold), typeof(double), typeof(SkUiSwipeView), 0d, validateValue: SkUiValidate.Finite);

    /// <summary>Bindable <see cref="Padding"/>.</summary>
    public static readonly BindableProperty PaddingProperty = BindableProperty.Create(
        nameof(Padding), typeof(Thickness), typeof(SkUiSwipeView), default(Thickness),
        propertyChanged: (bindable, _, newValue) => ((SkUiSwipeView)bindable).OnPaddingChanged((Thickness)newValue));

    private static readonly BindablePropertyKey IsOpenPropertyKey = BindableProperty.CreateReadOnly(
        nameof(IsOpen), typeof(bool), typeof(SkUiSwipeView), false);

    /// <summary>Bindable read-only <see cref="IsOpen"/> (for triggers).</summary>
    public static readonly BindableProperty IsOpenProperty = IsOpenPropertyKey.BindableProperty;

    private static BindableProperty CreateItemsProperty(string name, OpenSwipeItem side) => BindableProperty.Create(
        name, typeof(SwipeItems), typeof(SkUiSwipeView), null,
        validateValue: (_, value) =>
        {
            if (value is SwipeItems items)
                ValidateItems(items);
            return true;
        },
        propertyChanged: (bindable, oldValue, newValue) => ((SkUiSwipeView)bindable).OnItemsChanged(side, (SwipeItems?)oldValue, (SwipeItems?)newValue),
        defaultValueCreator: _ => new SwipeItems());

    /// <summary>Creates a closed swipe view (GPU-backed when it is a surface of its own, as content views are).</summary>
    public SkUiSwipeView()
    {
        HwAccelerated = true;
        ClipToBounds = true; // as MAUI's platform views: items and the moved content stay inside
        _ancestorMovingChanged = OnAncestorMovingChanged;
        _motion = new SkUiTween(new MotionHost(this));
        _contentPart = new ContentPart();
        AttachChild(_contentPart);
        foreach (var side in Sides)
            OnItemsChanged(side, null, GetItems(side)); // the default collections are created, not set
    }

    /// <summary>The view that is swiped.</summary>
    public ISkUiView? Content
    {
        get => (ISkUiView?)GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <summary>Items revealed on the left when the content is swiped to the right.</summary>
    public SwipeItems LeftItems
    {
        get => (SwipeItems)GetValue(LeftItemsProperty);
        set => SetValue(LeftItemsProperty, value);
    }

    /// <summary>Items revealed on the right when the content is swiped to the left.</summary>
    public SwipeItems RightItems
    {
        get => (SwipeItems)GetValue(RightItemsProperty);
        set => SetValue(RightItemsProperty, value);
    }

    /// <summary>Items revealed at the top when the content is swiped down.</summary>
    public SwipeItems TopItems
    {
        get => (SwipeItems)GetValue(TopItemsProperty);
        set => SetValue(TopItemsProperty, value);
    }

    /// <summary>Items revealed at the bottom when the content is swiped up.</summary>
    public SwipeItems BottomItems
    {
        get => (SwipeItems)GetValue(BottomItemsProperty);
        set => SetValue(BottomItemsProperty, value);
    }

    /// <summary>
    /// How far (DIPs) a release must have moved the content to open the items (<see cref="SwipeMode.Reveal"/>) or invoke
    /// the first one (<see cref="SwipeMode.Execute"/>), at most the items' open distance; 0 (default): 60 % of it.
    /// </summary>
    public double Threshold
    {
        get => (double)GetValue(ThresholdProperty);
        set => SetValue(ThresholdProperty, value);
    }

    /// <summary>Inset around the content; items are revealed beside the content, inside the padding.</summary>
    public Thickness Padding
    {
        get => (Thickness)GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    /// <summary>Whether items are open (or opening): set when a swipe or <see cref="Open"/> opens them, cleared when they close.</summary>
    public bool IsOpen => (bool)GetValue(IsOpenProperty);

    /// <summary>Raised when a swipe starts moving the content, with the direction of the side it reveals.</summary>
    public event EventHandler<SwipeStartedEventArgs>? SwipeStarted;

    /// <summary>Raised while a swipe moves the content, with its offset (DIPs; negative to the left and up).</summary>
    public event EventHandler<SwipeChangingEventArgs>? SwipeChanging;

    /// <summary>Raised when a swipe is released, with whether the items stay open.</summary>
    public event EventHandler<SwipeEndedEventArgs>? SwipeEnded;

    /// <summary>Sets <see cref="Content"/> (same as the property setter).</summary>
    public SkUiSwipeView SetContent(ISkUiView? value)
    {
        if (!ReferenceEquals(Content, value) && value is not null) ValidateChild(value);
        Content = value;
        return this;
    }

    /// <summary>Sets <see cref="Threshold"/> (same as the property setter).</summary>
    public SkUiSwipeView SetThreshold(double value)
    {
        SkUiValidate.ThrowIfNotFinite(value, nameof(value));
        Threshold = value;
        return this;
    }

    /// <summary>Sets <see cref="Padding"/> (same as the property setter).</summary>
    public SkUiSwipeView SetPadding(Thickness value) { Padding = value; return this; }

    /// <summary>
    /// Opens the items of one side (another open side closes at once). Nothing happens when that side has no visible
    /// items; before the view is laid out it opens once it is.
    /// </summary>
    public void Open(OpenSwipeItem openSwipeItem, bool animated = true)
    {
        if (!Enum.IsDefined(openSwipeItem))
            throw new ArgumentOutOfRangeException(nameof(openSwipeItem));
        if (!HasVisibleItems(openSwipeItem))
            return;
        Reveal(openSwipeItem);
        Settle(open: true, animated);
    }

    /// <summary>Closes open (or revealed) items.</summary>
    public void Close(bool animated = true)
    {
        if (_shownPart is not null)
            Settle(open: false, animated);
    }

    /// <summary>The content's host, moved while swiping (tests).</summary>
    internal SkUiView ContentHost => _contentPart;

    /// <summary>The host of a side's items, once that side has been revealed (tests).</summary>
    internal SkUiView? GetItemsHost(OpenSwipeItem side) => _parts[(int)side];

    /// <summary>The side shown, or <c>null</c> when closed (tests).</summary>
    internal OpenSwipeItem? ShownSide => _shownPart?.Side;

    /// <summary>The content's offset along the shown side's axis (DIPs; negative to the left and up).</summary>
    internal double Offset => _shownPart is { } part ? Sign(part.Side) * _shown : 0;

    /// <summary>Whether the open or close animation runs (tests).</summary>
    internal bool IsSettling => _motion.IsRunning;

    private SwipeItems GetItems(OpenSwipeItem side) => side switch
    {
        OpenSwipeItem.LeftItems => LeftItems,
        OpenSwipeItem.RightItems => RightItems,
        OpenSwipeItem.TopItems => TopItems,
        _ => BottomItems
    };

    private static bool IsHorizontal(OpenSwipeItem side) => side is OpenSwipeItem.LeftItems or OpenSwipeItem.RightItems;

    /// <summary>The direction of the content's move when <paramref name="side"/> is revealed: +1 right / down, -1 left / up.</summary>
    private static int Sign(OpenSwipeItem side) => side is OpenSwipeItem.LeftItems or OpenSwipeItem.TopItems ? 1 : -1;

    /// <summary>The swipe direction that reveals <paramref name="side"/>.</summary>
    private static SwipeDirection DirectionOf(OpenSwipeItem side) => side switch
    {
        OpenSwipeItem.LeftItems => SwipeDirection.Right,
        OpenSwipeItem.RightItems => SwipeDirection.Left,
        OpenSwipeItem.TopItems => SwipeDirection.Down,
        _ => SwipeDirection.Up
    };

    private static OpenSwipeItem Opposite(OpenSwipeItem side) => side switch
    {
        OpenSwipeItem.LeftItems => OpenSwipeItem.RightItems,
        OpenSwipeItem.RightItems => OpenSwipeItem.LeftItems,
        OpenSwipeItem.TopItems => OpenSwipeItem.BottomItems,
        _ => OpenSwipeItem.TopItems
    };

    /// <summary>Whether a side has an item to show.</summary>
    private bool HasVisibleItems(OpenSwipeItem side)
    {
        var items = GetItems(side);
        for (var index = 0; index < items.Count; index++) // checked on every press: no enumerator
            if (IsItemVisible(items[index]))
                return true;
        return false;
    }

    internal static bool IsItemVisible(ISwipeItem? item) => item?.IsVisible == true;

    /// <summary>Whether a press may start or continue a swipe.</summary>
    private bool CanSwipe => IsEnabled && (_shownPart is not null
        || HasVisibleItems(OpenSwipeItem.LeftItems) || HasVisibleItems(OpenSwipeItem.RightItems)
        || HasVisibleItems(OpenSwipeItem.TopItems) || HasVisibleItems(OpenSwipeItem.BottomItems));

    /// <summary>Only <see cref="SwipeItem"/>s and drawn views can be shown.</summary>
    private static void ValidateItems(SwipeItems items)
    {
        foreach (var item in items)
            if (item is not (SwipeItem or ISkUiView))
                throw new NotSupportedException(
                    $"{item?.GetType().Name ?? "null"} cannot be drawn in an SkUiSwipeView: use SwipeItem or SkUiSwipeItemView (MAUI's SwipeItemView hosts native views).");
    }

    private void OnItemsChanged(OpenSwipeItem side, SwipeItems? oldValue, SwipeItems? newValue)
    {
        var index = (int)side;
        if (oldValue is not null)
        {
            _parts[index]?.Populate(null);
            RemoveLogicalChild(oldValue);
            ReleaseBindingContext(oldValue);
        }
        if (newValue is not null)
        {
            // As MAUI's SwipeView: the items are logical children, so they bind against this view's context.
            AddLogicalChild(newValue);
            InheritBindingContext(newValue);
        }
        // Shared collections (resources) must not keep the view alive: one weak listener per side.
        (_itemsListeners[index] ??= new SkUiWeakListener<SkUiSwipeView>(this, side switch
        {
            OpenSwipeItem.LeftItems => static (view, change) => view.OnItemsContentChanged(OpenSwipeItem.LeftItems, change),
            OpenSwipeItem.RightItems => static (view, change) => view.OnItemsContentChanged(OpenSwipeItem.RightItems, change),
            OpenSwipeItem.TopItems => static (view, change) => view.OnItemsContentChanged(OpenSwipeItem.TopItems, change),
            _ => static (view, change) => view.OnItemsContentChanged(OpenSwipeItem.BottomItems, change)
        })).Listen(newValue);
        _parts[index]?.Populate(newValue);
        OnSideItemsChanged(side);
    }

    private void OnItemsContentChanged(OpenSwipeItem side, SkUiChange change)
    {
        if (change.Kind == SkUiChangeKind.Collection)
        {
            var items = GetItems(side);
            ValidateItems(items);
            _parts[(int)side]?.Populate(items);
        }
        else if (change.PropertyName is not (nameof(SwipeItems.Mode) or nameof(SwipeItems.SwipeBehaviorOnInvoked)))
        {
            return;
        }
        OnSideItemsChanged(side);
    }

    /// <summary>A side's items changed (collection, mode, an item's visibility): the shown side follows.</summary>
    internal void OnSideItemsChanged(OpenSwipeItem side)
    {
        var part = _parts[(int)side];
        part?.InvalidateItems();
        if (part is null || !ReferenceEquals(part, _shownPart))
            return;
        if (!HasVisibleItems(side))
        {
            // Nothing left to show: a drag in progress ends, and the view closes at once.
            _gesture?.Cancel();
            Settle(open: false, animated: false);
            return;
        }
        LayoutPart(part);
        if (_open && !_dragging)
            Settle(open: true, animated: false);
        else
        {
            _shown = Math.Min(_shown, part.OpenDistance);
            ApplyOffset();
        }
    }

    private void OnPaddingChanged(Thickness value)
    {
        _padding = value;
        InvalidateMeasureOverride();
    }

    /// <summary>Shows a side's items (created on first use) behind the content, hiding another side at once.</summary>
    private void Reveal(OpenSwipeItem side)
    {
        var index = (int)side;
        var part = _parts[index];
        if (part is null)
        {
            part = _parts[index] = new ItemsPart(this, side);
            part.Populate(GetItems(side));
            AttachChild(part);
        }
        if (!ReferenceEquals(part, _shownPart))
        {
            if (_shownPart is not null)
            {
                _motion.Jump(0);
                _shown = 0;
                _shownPart.IsVisible = false;
            }
            _shownPart = part;
            part.IsVisible = true;
            _contentPart.InputTransparent = true; // taps on the content close the view instead
            WatchScrollers(true);
        }
        LayoutPart(part);
    }

    /// <summary>Animates (or jumps) to open or closed; closed hides the items and gives the content its input back.</summary>
    private void Settle(bool open, bool animated)
    {
        if (_shownPart is not { } part)
            return;
        SetIsOpen(open);
        var target = open ? part.OpenDistance : 0;
        var length = animated && !SkUiMotion.IsMotionReduced ? SettleLength : 0;
        var distance = Math.Abs(target - _shown);
        _motion.Jump((float)_shown);
        _motion.AnimateTo((float)target,
            length == 0 ? SkUiTransition.None : SkUiTransition.FromMilliseconds(length, Easing.CubicOut),
            _ =>
            {
                if (!_open && !_dragging && ReferenceEquals(_shownPart, part))
                    Hide();
            },
            durationScale: part.OpenDistance > 0 ? (float)Math.Clamp(distance / part.OpenDistance, 0.25, 1) : 1);
    }

    /// <summary>Closed: the items are hidden and the content is interactive again.</summary>
    private void Hide()
    {
        if (_shownPart is { } part)
            part.IsVisible = false;
        _shownPart = null;
        _shown = 0;
        _contentPart.InputTransparent = false;
        _contentPart.TranslationX = _contentPart.TranslationY = 0;
        WatchScrollers(false);
        NotifyContentMoved();
    }

    private void SetIsOpen(bool value)
    {
        if (_open == value)
            return;
        _open = value;
        SetValue(IsOpenPropertyKey, value);
    }

    /// <summary>Moves the content by the shown distance and the items along with its edge (composite-time).</summary>
    private void ApplyOffset()
    {
        if (_shownPart is not { } part)
            return;
        var offset = Sign(part.Side) * _shown;
        // The items' inner edge follows the content's edge: they slide in from the side.
        var itemsOffset = offset - Sign(part.Side) * part.Length;
        if (IsHorizontal(part.Side))
        {
            _contentPart.TranslationX = offset;
            _contentPart.TranslationY = 0;
            part.TranslationX = itemsOffset;
            part.TranslationY = 0;
        }
        else
        {
            _contentPart.TranslationX = 0;
            _contentPart.TranslationY = offset;
            part.TranslationX = 0;
            part.TranslationY = itemsOffset;
        }
        NotifyContentMoved();
    }

    /// <summary>Native views in the content follow it (they are clipped to this view, which clips its bounds by default).</summary>
    private void NotifyContentMoved()
    {
        if (SurfaceHostsNativeViews)
            _contentPart.NotifyMoved();
    }

    #region Swipe input

    /// <summary>A swipe took the pointer: it moves the content from where it is now, revealing <paramref name="side"/>.</summary>
    private void BeginDrag(OpenSwipeItem side)
    {
        _dragging = true;
        Reveal(side);
        _motion.Jump((float)_shown);
        _dragStart = Offset;
        SwipeStarted?.Invoke(this, new SwipeStartedEventArgs(DirectionOf(side)));
    }

    /// <summary>The pointer moved <paramref name="delta"/> DIPs along the shown side's axis since the press.</summary>
    private void DragTo(double delta)
    {
        if (_shownPart is not { } part)
            return;
        var offset = _dragStart + delta;
        var side = part.Side;
        if (offset * Sign(side) < 0 && HasVisibleItems(Opposite(side)))
        {
            // Dragged past the closed position: the other side of the axis is revealed.
            side = Opposite(side);
            Reveal(side);
            part = _shownPart!;
        }
        _shown = Math.Clamp(offset * Sign(side), 0, part.OpenDistance);
        ApplyOffset();
        SwipeChanging?.Invoke(this, new SwipeChangingEventArgs(DirectionOf(side), Offset));
    }

    /// <summary>The swipe ended with <paramref name="velocity"/> (DIPs / s along the axis): open, invoke or close.</summary>
    private void EndDrag(double velocity)
    {
        _dragging = false;
        if (_shownPart is not { } part)
            return;
        var side = part.Side;
        if (!HasVisibleItems(side))
        {
            // Its items went while it was dragged.
            Settle(open: false, animated: false);
            SwipeEnded?.Invoke(this, new SwipeEndedEventArgs(DirectionOf(side), false));
            return;
        }
        var opening = velocity * Sign(side); // positive towards open
        var threshold = Threshold > 0 ? Math.Min(Threshold, part.OpenDistance) : DefaultThresholdFraction * part.OpenDistance;
        var passed = _shown > 0 && _shown >= threshold;
        var items = part.Items;
        if (items?.Mode == SwipeMode.Execute)
        {
            var remainOpen = items.SwipeBehaviorOnInvoked == SwipeBehaviorOnInvoked.RemainOpen;
            Settle(open: passed && remainOpen, animated: true);
            SwipeEnded?.Invoke(this, new SwipeEndedEventArgs(DirectionOf(side), _open));
            if (passed && items.FirstOrDefault(IsItemVisible) is { } first)
                ((Microsoft.Maui.ISwipeItem)first).OnInvoked();
            return;
        }
        var fling = Math.Abs(velocity) >= SkUiGestureSettings.SwipeVelocity;
        var open = fling ? opening > 0 && _shown > 0 : passed;
        Settle(open, animated: true);
        SwipeEnded?.Invoke(this, new SwipeEndedEventArgs(DirectionOf(side), open));
    }

    /// <summary>An item was tapped: it is invoked, and the view closes unless its items remain open.</summary>
    internal void InvokeItem(ISwipeItem item)
    {
        var part = _shownPart;
        var remainOpen = part?.Items?.SwipeBehaviorOnInvoked == SwipeBehaviorOnInvoked.RemainOpen;
        try
        {
            ((Microsoft.Maui.ISwipeItem)item).OnInvoked();
        }
        finally
        {
            if (!remainOpen && ReferenceEquals(part, _shownPart))
                Close();
        }
    }

    /// <inheritdoc />
    internal override void CollectGestureRecognizers(List<SkUiGestureRecognizer> recognizers)
    {
        base.CollectGestureRecognizers(recognizers);
        if (!CanSwipe)
            return;
        recognizers.Add(_gesture ??= new SwipeGesture(this));
        if (_shownPart is not null)
            recognizers.Add(_closeTap ??= new SkUiTapGestureRecognizer
            {
                CanTap = () => _shownPart is not null,
                TapHandler = _ => Close()
            });
    }

    /// <inheritdoc />
    internal override void CancelGestures()
    {
        base.CancelGestures();
        _gesture?.Cancel();
        _closeTap?.Cancel();
    }

    /// <summary>
    /// The swipe: claims a drag that passes the touch slop towards a side with items (or along the shown side's axis while
    /// items are shown) and moves the content with it; drags along the other axis are left to scrollers.
    /// </summary>
    private sealed class SwipeGesture(SkUiSwipeView owner) : SkUiGestureRecognizer
    {
        private long? _pointer;
        private bool _active;
        private OpenSwipeItem _side;
        private SkUiVelocityTracker _velocity;

        protected internal override bool IsExclusive => true;

        protected internal override bool OnPointerPressed(SkUiPointer pointer)
        {
            if (_pointer is not null || !owner.CanSwipe)
                return false;
            _pointer = pointer.Id;
            _active = false;
            _velocity.Reset(pointer.Timestamp, pointer.Position);
            return true;
        }

        protected internal override void OnPointerMoved(SkUiPointer pointer)
        {
            if (_pointer != pointer.Id)
                return;
            _velocity.Add(pointer.Timestamp, pointer.Position);
            var dx = pointer.TotalX;
            var dy = pointer.TotalY;
            if (!_active)
            {
                if (Decide(dx, dy) is not { } side)
                    return;
                Claim();
                if (_pointer != pointer.Id)
                    return; // lost the arena
                _active = true;
                _side = side;
                owner.BeginDrag(side);
            }
            owner.DragTo(IsHorizontal(_side) ? dx : dy);
        }

        /// <summary>The side to reveal once the drag passes the slop; <c>null</c> while undecided (resigned when it cannot swipe that way).</summary>
        private OpenSwipeItem? Decide(double dx, double dy)
        {
            if (owner._shownPart is { } shown)
            {
                var along = IsHorizontal(shown.Side) ? SkUiPanAxis.Horizontal : SkUiPanAxis.Vertical;
                if (ExceedsSlop(dx, dy, along))
                    return shown.Side;
                if (ExceedsSlop(dx, dy, along == SkUiPanAxis.Horizontal ? SkUiPanAxis.Vertical : SkUiPanAxis.Horizontal))
                    Resign();
                return null;
            }
            OpenSwipeItem side;
            if (ExceedsSlop(dx, dy, SkUiPanAxis.Horizontal))
                side = dx > 0 ? OpenSwipeItem.LeftItems : OpenSwipeItem.RightItems;
            else if (ExceedsSlop(dx, dy, SkUiPanAxis.Vertical))
                side = dy > 0 ? OpenSwipeItem.TopItems : OpenSwipeItem.BottomItems;
            else
                return null;
            if (owner.IsEnabled && owner.HasVisibleItems(side))
                return side;
            Resign();
            return null;
        }

        protected internal override void OnPointerReleased(SkUiPointer pointer)
        {
            if (_pointer != pointer.Id)
                return;
            _velocity.Add(pointer.Timestamp, pointer.Position);
            var active = _active;
            _pointer = null;
            _active = false;
            if (!active)
            {
                Resign();
                return;
            }
            var velocity = _velocity.Velocity;
            owner.EndDrag(IsHorizontal(_side) ? velocity.X : velocity.Y);
        }

        protected internal override void OnRejected(long pointerId)
        {
            if (_pointer != pointerId)
                return;
            var active = _active;
            _pointer = null;
            _active = false;
            if (active)
                owner.EndDrag(0);
        }
    }

    #endregion

    #region Scrolling around

    /// <summary>Watches the drawn scrollers around the view while items are shown: a scroll closes them.</summary>
    private void WatchScrollers(bool watch)
    {
        if (_watchedScrollers is { } watched)
        {
            foreach (var scroller in watched)
                scroller.MovingChanged -= _ancestorMovingChanged;
            _watchedScrollers = null;
        }
        if (!watch)
            return;
        for (var node = ((ISkUiRenderable)this).RenderParent; node is not null; node = node.RenderParent)
            if (node is ISkUiScrollHost host)
            {
                host.Scroller.MovingChanged += _ancestorMovingChanged;
                (_watchedScrollers ??= []).Add(host.Scroller);
            }
    }

    private void OnAncestorMovingChanged(bool moving)
    {
        if (moving && !_dragging)
            Close();
    }

    /// <inheritdoc />
    protected override void OnAnimationRootChanged(bool subtreeDetached = false)
    {
        base.OnAnimationRootChanged(subtreeDetached);
        // Moved or detached (this view or an ancestor): watch the scrollers of the new place, none while detached.
        WatchScrollers(!subtreeDetached && Parent is not null && _shownPart is not null);
        if (subtreeDetached && _shownPart is not null && !_dragging)
            Settle(_open, animated: false);
    }

    #endregion

    /// <inheritdoc />
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        foreach (var side in Sides)
            InheritBindingContext(GetItems(side));
        // A recycled row shows another item: its items are not open.
        if (_shownPart is not null && !_dragging)
            Settle(open: false, animated: false);
    }

    /// <inheritdoc />
    internal override IEnumerable<ISkUiView> SkiaChildren
    {
        get
        {
            // Items are drawn behind the content; only the shown side is visible.
            foreach (var part in _parts)
                if (part is not null)
                    yield return part;
            yield return _contentPart;
        }
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var size = ((IView)_contentPart).Measure(
            Math.Max(0, widthConstraint - _padding.HorizontalThickness),
            Math.Max(0, heightConstraint - _padding.VerticalThickness));
        return new Size(size.Width + _padding.HorizontalThickness, size.Height + _padding.VerticalThickness);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        _contentRect = new Rect(_padding.Left, _padding.Top,
            Math.Max(0, size.Width - _padding.HorizontalThickness), Math.Max(0, size.Height - _padding.VerticalThickness));
        ((IView)_contentPart).Arrange(_contentRect);
        if (_shownPart is not { } part)
            return;
        LayoutPart(part);
        // The open distance follows the size; a drag or an animation keeps its own course.
        if (_open && !_dragging && !_motion.IsRunning)
            _shown = part.OpenDistance;
        _shown = Math.Min(_shown, part.OpenDistance);
        ApplyOffset();
    }

    /// <summary>Measures a side's items against the content and places them along the content's edge on that side.</summary>
    private void LayoutPart(ItemsPart part)
    {
        var content = _contentRect;
        ((IView)part).Measure(content.Width, content.Height);
        var length = part.Length;
        ((IView)part).Arrange(part.Side switch
        {
            OpenSwipeItem.LeftItems => new Rect(content.X, content.Y, length, content.Height),
            OpenSwipeItem.RightItems => new Rect(content.Right - length, content.Y, length, content.Height),
            OpenSwipeItem.TopItems => new Rect(content.X, content.Y, content.Width, length),
            _ => new Rect(content.X, content.Bottom - length, content.Width, length)
        });
    }

    private sealed class MotionHost(SkUiSwipeView owner) : ISkUiTransitionHost
    {
        public SkUiAnimationClock? TransitionClock => ((ISkUiTransitionHost)owner).TransitionClock;

        public void InvalidateTransition()
        {
            owner._shown = Math.Max(0, owner._motion.Value);
            owner.ApplyOffset();
        }
    }

    /// <summary>Hosts the content; the swipe view owns its translation and input transparency, so the content's own are never touched.</summary>
    private sealed class ContentPart : SkUiContentView;
}
