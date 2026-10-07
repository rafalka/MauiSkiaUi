using System.Diagnostics;
using System.Windows.Input;
using MauiSkiaUi.Rendering;

namespace MauiSkiaUi;

/// <summary>
/// The engine of a drawn vertical list whose item views are created on demand: only the items near what its ancestor
/// scrollers show exist (the visible window, the intersection of every ancestor <see cref="SkUiScrollView"/>'s viewport,
/// plus prefetch). Derive from it for a list control of your own, with only the API it needs: the subclass says how many
/// items there are (<see cref="ResetItems"/>, <see cref="InsertItems"/>, <see cref="RemoveItems"/>, …), creates their views
/// (<see cref="CreateItemView"/>) and shows an item in a view (<see cref="BindItemView"/>); the base class decides which
/// items exist, measures, arranges, recycles views per <see cref="GetRecycleKey"/>, keeps what shows in place, and scrolls.
/// <see cref="SkUiVirtualVerticalStackLayout"/> is the subclass for <c>ItemsSource</c> / <c>ItemTemplate</c> and item
/// factories.
/// </summary>
/// <remarks>
/// <para>
/// <b>It needs a drawn scroller above it.</b> The layout does not scroll; it follows the <see cref="SkUiScrollView"/>s
/// around it: put it in one (as its content, or below a header or other content in a scrolled page, also nested in
/// another list), or use <see cref="SkUiVirtualScrollView"/>, a scroll view with one inside, for a plain list. Without a
/// drawn scroller it cannot scroll and creates only the items the surface shows; inside a native MAUI <c>ScrollView</c>
/// (around the surface) the surface is as tall as the list, so every item is created. Shown that way, the layout reports
/// it once as a <c>SkiaUi:</c> trace line.
/// </para>
/// <para>
/// <b>Indexed extent</b> (FR-21 mode A): every item keeps its index and its place in the content, so the scroll offset,
/// the scroll bar and <see cref="ScrollToIndex"/> stay meaningful. Each item may have a different size: realized items are
/// measured, and their sizes are kept after they are released; items not measured yet are estimated
/// (<see cref="EstimatedItemSize"/>, else the average of the measured ones). <see cref="ItemExtent"/> gives every item the
/// same size (the fast path: nothing is estimated). When items before the first visible one change size (they are
/// measured, inserted or removed), the scroll offset moves with them, also during a fling, so what shows stays in place
/// (scroll anchoring).
/// </para>
/// <para>
/// <b>Prefetch, budget and recycling:</b> visible items are created at once; items up to <see cref="PrefetchFactor"/>
/// viewport lengths ahead (in the scroll direction, further while a fling runs) and <see cref="PrefetchBehindFactor"/> behind
/// are created within a per-frame UI-thread budget, chosen automatically from the frame rate, the measured cost of items
/// and the scroll speed (<see cref="PrefetchBudget"/>). Items further than <see cref="ReleaseFactor"/> viewport
/// lengths are released; views with a recycle key are kept and rebound to another item instead of created again.
/// Scrolling never re-records or remeasures items that stay.
/// </para>
/// <para>
/// <b>Deriving.</b> Override <see cref="CreateItemView"/> (and usually <see cref="GetRecycleKey"/> and
/// <see cref="BindItemView"/>), and report the items: <see cref="ResetItems"/> when they are replaced (also first),
/// <see cref="InsertItems"/>, <see cref="RemoveItems"/>, <see cref="ReplaceItems"/> and <see cref="MoveItems"/> for changes
/// (only the items they touch are realized or released), <see cref="HasMoreItems"/> for a list whose length is unknown.
/// Item views are drawn, unparented <see cref="ISkUiView"/>s; the layout attaches, measures, arranges and releases them.
/// Calls come on the UI thread, while scrolling and laying out: keep them cheap and do not change the items from them.
/// </para>
/// <code>
/// public sealed class ContactList : SkUiVirtualVerticalStackLayoutBase
/// {
///     private IReadOnlyList&lt;Contact&gt; _contacts = [];
///
///     public void Show(IReadOnlyList&lt;Contact&gt; contacts) { _contacts = contacts; ResetItems(contacts.Count); }
///
///     protected override object? GetRecycleKey(int index) => _contacts[index].IsGroup ? "header" : "row";
///
///     protected override ISkUiView CreateItemView(int index, object? recycleKey) =>
///         recycleKey is "header" ? new SkUiLabel { FontSize = 13 } : new ContactRow();
///
///     protected override void BindItemView(int index, ISkUiView view, object? recycleKey)
///     {
///         if (view is SkUiLabel header) header.Text = _contacts[index].Name;
///         else ((ContactRow)view).Show(_contacts[index]);
///     }
/// }
/// </code>
/// </remarks>
public abstract class SkUiVirtualVerticalStackLayoutBase : SkUiView, ISkUiScrollListener
{
    /// <summary>How far ahead a running fling's travel is predicted, in seconds.</summary>
    private const double FlingLookahead = 0.1;

    private readonly SkUiVirtualItemSizes _sizes = new();
    private readonly List<Realized> _realized = []; // contiguous indices, ascending
    private readonly Dictionary<object, Stack<ISkUiView>> _pool = [];
    private bool _hasMoreItems;
    private Thickness _padding;
    private double _prefetchFactor = 1;
    private double _prefetchBehindFactor = 0.5;
    private double _releaseFactor = 2;
    private TimeSpan? _prefetchBudget;
    private int _remainingItemsThreshold = -1;
    private int _thresholdCount = -1;

    private double _cross = double.NaN;         // width items are measured at
    private double _arrangedWidth = double.NaN; // width items are arranged at
    private double _reportedLength = double.NaN; // height of the last measure
    private bool _measuring;
    private bool _updating;
    private List<Action>? _deferredEvents;
    private List<SkUiScrollView>? _scrollers;
    private IDisposable? _prefetchTick;
    private bool _startingTick;

    // The current pass: the window in item coordinates (padding excluded), moved by anchor corrections.
    private double _top, _bottom;
    private int _anchor = -1;
    private double _anchorStart;
    private double _shift;
    private bool _childrenChanged;
    private long _passStart;
    private int _prefetched;
    private TimeSpan _passBudget;
    private bool _passMoving;

    // The automatic prefetch budget's inputs: what realizing an item costs here, and a boost after prefetch fell behind.
    private const double MaxBoost = 4;
    private double _itemCost = double.NaN; // ms, moving average
    private double _boost = 1;
    private int _realizedCount;
    private int _peakRealized;

    private double _lastTop = double.NaN;
    private long _lastTopTime;
    private double _velocity;
    private bool _racing; // a render-thread motion passes more than a viewport per frame
    private int _direction = 1;
    private int _pinnedAnchor = -1;
    private Task? _pinTask;

    /// <summary>Creates a layout without items (<see cref="ResetItems"/> gives it some).</summary>
    protected SkUiVirtualVerticalStackLayoutBase() => HwAccelerated = true;

    #region Properties

    /// <summary>Bindable property for <see cref="Spacing"/>.</summary>
    public static readonly BindableProperty SpacingProperty = BindableProperty.Create(nameof(Spacing), typeof(double), typeof(SkUiVirtualVerticalStackLayoutBase), 0d,
        validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, value) => ((SkUiVirtualVerticalStackLayoutBase)view).OnSizingChanged(() => ((SkUiVirtualVerticalStackLayoutBase)view)._sizes.Spacing = (double)value));

    /// <summary>Gap between items in DIPs.</summary>
    public double Spacing { get => (double)GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    /// <summary>Bindable property for <see cref="Padding"/>.</summary>
    public static readonly BindableProperty PaddingProperty = BindableProperty.Create(nameof(Padding), typeof(Thickness), typeof(SkUiVirtualVerticalStackLayoutBase), default(Thickness),
        propertyChanged: (view, _, value) => ((SkUiVirtualVerticalStackLayoutBase)view).OnPaddingChanged((Thickness)value));

    /// <summary>Space around the items in DIPs.</summary>
    public Thickness Padding { get => (Thickness)GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }

    /// <summary>Bindable property for <see cref="ItemExtent"/>.</summary>
    public static readonly BindableProperty ItemExtentProperty = BindableProperty.Create(nameof(ItemExtent), typeof(double), typeof(SkUiVirtualVerticalStackLayoutBase), 0d,
        validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, value) => ((SkUiVirtualVerticalStackLayoutBase)view).OnSizingChanged(() => ((SkUiVirtualVerticalStackLayoutBase)view)._sizes.FixedExtent = (double)value, forget: true));

    /// <summary>
    /// When positive, the height of every item in DIPs (items are measured and arranged at it): the fast path, where item
    /// positions are known without measuring and nothing is estimated. 0 (default): each item is as tall as it measures.
    /// </summary>
    public double ItemExtent { get => (double)GetValue(ItemExtentProperty); set => SetValue(ItemExtentProperty, value); }

    /// <summary>Bindable property for <see cref="EstimatedItemSize"/>.</summary>
    public static readonly BindableProperty EstimatedItemSizeProperty = BindableProperty.Create(nameof(EstimatedItemSize), typeof(double), typeof(SkUiVirtualVerticalStackLayoutBase), 0d,
        validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, value) => ((SkUiVirtualVerticalStackLayoutBase)view).OnSizingChanged(() => ((SkUiVirtualVerticalStackLayoutBase)view)._sizes.EstimatedSize = (double)value));

    /// <summary>
    /// The height assumed for items not measured yet, in DIPs (the extent, the scroll bar and <see cref="ScrollToIndex"/> use
    /// it). 0 (default): the average height of the items measured so far. Measured items always use their own height.
    /// </summary>
    public double EstimatedItemSize { get => (double)GetValue(EstimatedItemSizeProperty); set => SetValue(EstimatedItemSizeProperty, value); }

    /// <summary>Bindable property for <see cref="PrefetchFactor"/>.</summary>
    public static readonly BindableProperty PrefetchFactorProperty = BindableProperty.Create(nameof(PrefetchFactor), typeof(double), typeof(SkUiVirtualVerticalStackLayoutBase), 1d,
        validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, value) => ((SkUiVirtualVerticalStackLayoutBase)view).OnWindowSettingChanged(() => ((SkUiVirtualVerticalStackLayoutBase)view)._prefetchFactor = (double)value));

    /// <summary>
    /// How far ahead of the visible window (in the scroll direction) items are created before they show, in viewport
    /// lengths (default 1). A running fling adds the distance it is about to travel.
    /// </summary>
    public double PrefetchFactor { get => (double)GetValue(PrefetchFactorProperty); set => SetValue(PrefetchFactorProperty, value); }

    /// <summary>Bindable property for <see cref="PrefetchBehindFactor"/>.</summary>
    public static readonly BindableProperty PrefetchBehindFactorProperty = BindableProperty.Create(nameof(PrefetchBehindFactor), typeof(double), typeof(SkUiVirtualVerticalStackLayoutBase), 0.5,
        validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, value) => ((SkUiVirtualVerticalStackLayoutBase)view).OnWindowSettingChanged(() => ((SkUiVirtualVerticalStackLayoutBase)view)._prefetchBehindFactor = (double)value));

    /// <summary>How far behind the visible window (against the scroll direction) items are kept created, in viewport lengths (default 0.5).</summary>
    public double PrefetchBehindFactor { get => (double)GetValue(PrefetchBehindFactorProperty); set => SetValue(PrefetchBehindFactorProperty, value); }

    /// <summary>Bindable property for <see cref="ReleaseFactor"/>.</summary>
    public static readonly BindableProperty ReleaseFactorProperty = BindableProperty.Create(nameof(ReleaseFactor), typeof(double), typeof(SkUiVirtualVerticalStackLayoutBase), 2d,
        validateValue: IsValidReleaseFactor,
        propertyChanged: (view, _, value) => ((SkUiVirtualVerticalStackLayoutBase)view).OnWindowSettingChanged(() => ((SkUiVirtualVerticalStackLayoutBase)view)._releaseFactor = (double)value));

    /// <summary>
    /// Items further than this many viewport lengths from the visible window are released (default 2; never less than the
    /// prefetch distance plus half a viewport). Their sizes are kept, so the extent and the scroll position do not move;
    /// views with a recycle key go back to a pool and are rebound (<see cref="GetRecycleKey"/>). <see cref="double.PositiveInfinity"/> keeps every created item.
    /// </summary>
    public double ReleaseFactor { get => (double)GetValue(ReleaseFactorProperty); set => SetValue(ReleaseFactorProperty, value); }

    /// <summary>Bindable property for <see cref="PrefetchBudget"/>.</summary>
    public static readonly BindableProperty PrefetchBudgetProperty = BindableProperty.Create(nameof(PrefetchBudget), typeof(TimeSpan?), typeof(SkUiVirtualVerticalStackLayoutBase), null,
        validateValue: IsValidPrefetchBudget,
        propertyChanged: (view, _, value) => ((SkUiVirtualVerticalStackLayoutBase)view)._prefetchBudget = (TimeSpan?)value);

    /// <summary>
    /// The most UI-thread time spent per frame on creating items before they show (prefetch). <c>null</c> (default): chosen
    /// automatically, each frame, from the measured frame rate, what items cost to create and how fast the list scrolls.
    /// Leave it automatic unless measurements show a reason not to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Creating an item (inflating its template, binding it, measuring it) runs on the UI thread. Items that show are always
    /// created at once, however long that takes: a gap on screen is worse than a slow frame. This budget limits only the
    /// items created ahead of time (<see cref="PrefetchFactor"/>, <see cref="PrefetchBehindFactor"/>): once a frame has
    /// spent this much time on them, prefetch stops and continues on the next frame. At least one item is created per
    /// frame, so prefetch always finishes.
    /// </para>
    /// <para>
    /// Flings and animated scrolls run on the render thread and keep their frame rate while the UI thread is busy. Too small
    /// a budget does not make them stutter: it lets prefetch fall behind a fast fling, so rows at the edge of the viewport
    /// can show blank for a frame or two. Too large a budget makes UI frames long while the list fills (taps, bindings and
    /// UI-thread animations wait).
    /// </para>
    /// <para>
    /// <b>Automatic budget.</b> The frame length is measured from the UI frames (60, 90, 120 Hz, also when the UI runs
    /// slower than the display), and the cost of an item is measured as items are realized (so expensive templates and slow
    /// devices are accounted for). While nothing scrolls, prefetch takes a quarter of a frame. While the list scrolls, it
    /// takes what the items scrolling into the prefetch area per frame cost (speed × frame length ÷ item height × item
    /// cost, with a margin), at least a quarter and at most three quarters of a frame. When items had to be created
    /// as they came into view during a scroll (prefetch fell behind), the next budgets are doubled for a while.
    /// </para>
    /// <para>
    /// <b>A fixed budget</b> replaces all of this: <see cref="TimeSpan.Zero"/> creates one prefetched item per frame
    /// (deterministic steps, e.g. tests); a large value (one second) creates everything ahead in one frame. In XAML a
    /// <see cref="TimeSpan"/> is written <c>hours:minutes:seconds</c> (<c>"0:0:0.004"</c> is 4 ms); <c>{x:Null}</c> is
    /// automatic. Negative values are refused (the property keeps its value).
    /// </para>
    /// </remarks>
    public TimeSpan? PrefetchBudget { get => (TimeSpan?)GetValue(PrefetchBudgetProperty); set => SetValue(PrefetchBudgetProperty, value); }

    /// <summary>The automatic budget's share of a frame while nothing scrolls, and its smallest share while scrolling.</summary>
    internal const double IdleBudgetShare = 0.25;

    /// <summary>The automatic budget's largest share of a frame (while scrolling).</summary>
    internal const double ScrollingBudgetShare = 0.75;

    /// <summary>The automatic budget's margin over the measured demand.</summary>
    internal const double BudgetMargin = 1.5;

    /// <summary>
    /// The automatic prefetch budget in milliseconds: <see cref="IdleBudgetShare"/> of <paramref name="frame"/> while not
    /// <paramref name="scrolling"/>; else what the items scrolling into the prefetch area per frame cost
    /// (<paramref name="speed"/> DIPs / s, <paramref name="itemSize"/> DIPs, <paramref name="itemCost"/> ms each), with
    /// <see cref="BudgetMargin"/> and <paramref name="boost"/>, between the idle share and <see cref="ScrollingBudgetShare"/>.
    /// </summary>
    internal static double AutomaticBudget(double frame, bool scrolling, double speed, double itemSize, double itemCost, double boost)
    {
        var idle = frame * IdleBudgetShare;
        if (!scrolling)
            return idle;
        var itemsPerFrame = Math.Abs(speed) * frame / 1000 / Math.Max(1, itemSize);
        var demand = itemsPerFrame * (double.IsNaN(itemCost) ? 0 : itemCost) * BudgetMargin * boost;
        return Math.Clamp(demand, idle, frame * ScrollingBudgetShare);
    }

    /// <summary>Bindable property for <see cref="RemainingItemsThreshold"/>.</summary>
    public static readonly BindableProperty RemainingItemsThresholdProperty = BindableProperty.Create(nameof(RemainingItemsThreshold), typeof(int), typeof(SkUiVirtualVerticalStackLayoutBase), -1,
        validateValue: IsValidThreshold,
        propertyChanged: (view, _, value) => ((SkUiVirtualVerticalStackLayoutBase)view).OnRemainingItemsThresholdChanged((int)value));

    /// <summary>
    /// When what shows changes and the last visible item is this many items (or fewer) from the end,
    /// <see cref="RemainingItemsThresholdReached"/> is raised (and its command run), once per item count: load the next page,
    /// and it fires again when the new end comes near. -1 (default) never.
    /// </summary>
    public int RemainingItemsThreshold { get => (int)GetValue(RemainingItemsThresholdProperty); set => SetValue(RemainingItemsThresholdProperty, value); }

    /// <summary>Bindable property for <see cref="RemainingItemsThresholdReachedCommand"/>.</summary>
    public static readonly BindableProperty RemainingItemsThresholdReachedCommandProperty = BindableProperty.Create(nameof(RemainingItemsThresholdReachedCommand),
        typeof(ICommand), typeof(SkUiVirtualVerticalStackLayoutBase), null);

    /// <summary>Runs with <see cref="RemainingItemsThresholdReached"/> (when it can execute).</summary>
    public ICommand? RemainingItemsThresholdReachedCommand
    {
        get => (ICommand?)GetValue(RemainingItemsThresholdReachedCommandProperty);
        set => SetValue(RemainingItemsThresholdReachedCommandProperty, value);
    }

    /// <summary>Bindable property for <see cref="RemainingItemsThresholdReachedCommandParameter"/>.</summary>
    public static readonly BindableProperty RemainingItemsThresholdReachedCommandParameterProperty = BindableProperty.Create(nameof(RemainingItemsThresholdReachedCommandParameter),
        typeof(object), typeof(SkUiVirtualVerticalStackLayoutBase), null);

    /// <summary>The parameter of <see cref="RemainingItemsThresholdReachedCommand"/>.</summary>
    public object? RemainingItemsThresholdReachedCommandParameter
    {
        get => GetValue(RemainingItemsThresholdReachedCommandParameterProperty);
        set => SetValue(RemainingItemsThresholdReachedCommandParameterProperty, value);
    }

    /// <summary>The number of items known now (an endless list grows as it is scrolled; see <see cref="HasMoreItems"/>).</summary>
    public int ItemCount => _sizes.Count;

    /// <summary>The first item that shows (at least partly), or -1.</summary>
    public int FirstVisibleIndex { get; private set; } = -1;

    /// <summary>The last item that shows (at least partly), or -1.</summary>
    public int LastVisibleIndex { get; private set; } = -1;

    /// <summary>An item view was created or recycled for an item, and measured (load its images or data here).</summary>
    public event EventHandler<SkUiVirtualItemEventArgs>? ItemRealized;

    /// <summary>An item view left the layout (far from the window, or its item was removed); views with a recycle key are then recycled.</summary>
    public event EventHandler<SkUiVirtualItemEventArgs>? ItemReleased;

    /// <summary><see cref="FirstVisibleIndex"/> or <see cref="LastVisibleIndex"/> changed.</summary>
    public event EventHandler<SkUiVisibleRangeChangedEventArgs>? VisibleRangeChanged;

    /// <summary>The end of the items comes into view; see <see cref="RemainingItemsThreshold"/>.</summary>
    public event EventHandler? RemainingItemsThresholdReached;

    /// <summary>Sets <see cref="Spacing"/> (same as the property setter).</summary>
    public SkUiVirtualVerticalStackLayoutBase SetSpacing(double value) { SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value)); Spacing = value; return this; }

    /// <summary>Sets <see cref="Padding"/> (same as the property setter).</summary>
    public SkUiVirtualVerticalStackLayoutBase SetPadding(Thickness value) { Padding = value; return this; }

    /// <summary>Sets <see cref="ItemExtent"/> (same as the property setter).</summary>
    public SkUiVirtualVerticalStackLayoutBase SetItemExtent(double value) { SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value)); ItemExtent = value; return this; }

    /// <summary>Sets <see cref="EstimatedItemSize"/> (same as the property setter).</summary>
    public SkUiVirtualVerticalStackLayoutBase SetEstimatedItemSize(double value) { SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value)); EstimatedItemSize = value; return this; }

    // Shared with SkUiVirtualScrollView, which validates its forwarded properties the same way.
    internal static bool IsValidReleaseFactor(BindableObject bindable, object? value) => value is double factor && !double.IsNaN(factor) && factor >= 0;
    internal static bool IsValidPrefetchBudget(BindableObject bindable, object? value) => value is null || (value is TimeSpan budget && budget >= TimeSpan.Zero);
    internal static bool IsValidThreshold(BindableObject bindable, object? value) => value is int threshold && threshold >= -1;

    private void OnPaddingChanged(Thickness value)
    {
        _padding = value;
        InvalidateMeasureOverride();
    }

    private void OnSizingChanged(Action apply, bool forget = false)
    {
        apply();
        if (forget)
            _sizes.ForgetAll();
        InvalidateMeasureOverride();
    }

    private void OnWindowSettingChanged(Action apply)
    {
        apply();
        Update();
    }

    private void OnRemainingItemsThresholdChanged(int value)
    {
        _remainingItemsThreshold = value;
        _thresholdCount = -1;
        Update();
    }

    #endregion

    #region Items

    /// <summary>
    /// The recycle key of the item at <paramref name="index"/>: views created for one key are interchangeable (one template,
    /// one kind of row), so the view an item releases is kept and shows the next item with the same key
    /// (<see cref="BindItemView"/>) instead of a new view being created. <c>null</c> (default): the item's views are not
    /// recycled. Keys are compared with <see cref="object.Equals(object?)"/>.
    /// </summary>
    protected virtual object? GetRecycleKey(int index) => null;

    /// <summary>
    /// Creates a view for the item at <paramref name="index"/> when no recycled view with its <paramref name="recycleKey"/>
    /// (<see cref="GetRecycleKey"/>) is waiting: a drawn, unparented <see cref="ISkUiView"/>; <see cref="BindItemView"/> runs
    /// on it next, before it is attached and measured. Returns <c>null</c> only when asked for item <see cref="ItemCount"/>
    /// while <see cref="HasMoreItems"/>: the list ends there. For that probe the layout does not take a recycled view
    /// itself; <see cref="TakeRecycledView"/> gives one to an endless list that recycles.
    /// </summary>
    protected abstract ISkUiView? CreateItemView(int index, object? recycleKey);

    /// <summary>
    /// Shows the item at <paramref name="index"/> in <paramref name="view"/>: one just created by <see cref="CreateItemView"/>,
    /// or a recycled one that showed another item with the same <paramref name="recycleKey"/>. Runs before the view is
    /// attached and measured; set its binding context or its properties here. Default: nothing.
    /// </summary>
    protected virtual void BindItemView(int index, ISkUiView view, object? recycleKey) { }

    /// <summary>
    /// The view of the item at <paramref name="index"/> left the layout (it was released, its item was removed, or the
    /// items were reset): stop what was started for it (image loads, subscriptions). It is recycled next when it has a
    /// recycle key. Default: nothing.
    /// </summary>
    protected virtual void UnbindItemView(int index, ISkUiView view) { }

    /// <summary>The item at <paramref name="index"/> reported with its view (<see cref="ItemRealized"/>, <see cref="ItemReleased"/>). Default: <c>null</c>.</summary>
    protected virtual object? GetItem(int index) => null;

    /// <summary>
    /// Whether items may follow the last known one (an endless list, its length unknown). While <c>true</c>, the layout asks
    /// <see cref="CreateItemView"/> for item <see cref="ItemCount"/> as the end comes near: a view adds the item, <c>null</c>
    /// ends the list (and sets this to <c>false</c>). Set it to <c>true</c> again when more items may follow.
    /// </summary>
    protected bool HasMoreItems
    {
        get => _hasMoreItems;
        set
        {
            if (_hasMoreItems == value)
                return;
            _hasMoreItems = value;
            if (value)
                Update();
        }
    }

    /// <summary>
    /// All items changed: every view is released (and recycled), every size forgotten; the layout now has
    /// <paramref name="count"/> items, and <paramref name="hasMoreItems"/> (<see cref="HasMoreItems"/>). The scroll offset is
    /// kept (the scroller clamps it).
    /// </summary>
    protected void ResetItems(int count, bool hasMoreItems = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ReleaseAll(recycle: true);
        _hasMoreItems = hasMoreItems;
        _thresholdCount = -1;
        _sizes.Clear();
        _sizes.SetCount(count);
        InvalidateMeasureOverride();
        if (!_updating && !_measuring)
            FlushDeferredEvents();
    }

    /// <summary>
    /// <paramref name="count"/> items were inserted at <paramref name="index"/>. Items inserted among the realized ones are
    /// realized at once; the other items keep their views and sizes, and what shows stays in place (unless the start of the
    /// items shows, where inserted items push the others down).
    /// </summary>
    protected void InsertItems(int index, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if ((uint)index > (uint)_sizes.Count)
            throw new ArgumentOutOfRangeException(nameof(index), index, "Items are inserted at an item's index or at the end.");
        ChangeItems(() => Insert(index, count));
    }

    /// <summary><paramref name="count"/> items were removed at <paramref name="index"/>: their views are released; the others keep theirs, and what shows stays in place.</summary>
    protected void RemoveItems(int index, int count)
    {
        CheckRange(index, count);
        ChangeItems(() => Remove(index, count));
    }

    /// <summary><paramref name="count"/> items at <paramref name="index"/> were replaced by others: their views are released (or rebound) and the new items measured.</summary>
    protected void ReplaceItems(int index, int count)
    {
        CheckRange(index, count);
        ChangeItems(() =>
        {
            Remove(index, count);
            Insert(index, count);
        });
    }

    /// <summary>
    /// <paramref name="count"/> items moved from <paramref name="index"/> to <paramref name="newIndex"/> (their index once
    /// moved, as <see cref="System.Collections.Specialized.NotifyCollectionChangedEventArgs.NewStartingIndex"/>). They keep
    /// their measured sizes; their views are released and realized again where they land when that is among the realized
    /// items. The other items keep their views and sizes, and what shows stays in place.
    /// </summary>
    protected void MoveItems(int index, int count, int newIndex)
    {
        CheckRange(index, count);
        if ((uint)newIndex > (uint)(_sizes.Count - count))
            throw new ArgumentOutOfRangeException(nameof(newIndex), newIndex, "The moved items do not fit there.");
        if (count == 0 || index == newIndex)
            return;
        ChangeItems(() => Move(index, count, newIndex));
    }

    /// <summary>Drops the recycled views waiting for reuse (their recycle keys no longer create the same views, e.g. a template changed).</summary>
    protected void ClearRecycledViews() => _pool.Clear();

    /// <summary>
    /// A recycled view waiting with <paramref name="recycleKey"/>, or <c>null</c>: for <see cref="CreateItemView"/> of an
    /// endless list asked for a new item (the layout takes recycled views itself for known items).
    /// </summary>
    protected ISkUiView? TakeRecycledView(object recycleKey) =>
        _pool.TryGetValue(recycleKey, out var pooled) && pooled.TryPop(out var view) ? view : null;

    private void CheckRange(int index, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (index < 0 || index + count > _sizes.Count)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Items {index}..{index + count - 1} are not items of the layout ({_sizes.Count}).");
    }

    /// <summary>Applies a change of the items in one step (scroll corrections it causes do not realize anything mid-change), then lays out again.</summary>
    private void ChangeItems(Action change)
    {
        var nested = _updating;
        _updating = true;
        try
        {
            change();
        }
        finally
        {
            _updating = nested;
        }
        if (_childrenChanged)
        {
            _childrenChanged = false;
            InvalidateRender(SkUiRenderDirty.Children);
        }
        InvalidateMeasureFromChild();
        Update();
    }

    /// <summary>Whether an item can be realized at <paramref name="index"/> (an endless list may still have it).</summary>
    private bool CanRealize(int index) => index >= 0 && (index < _sizes.Count || (index == _sizes.Count && _hasMoreItems));

    /// <summary>A view for the item at <paramref name="index"/>, recycled or created, and bound.</summary>
    private bool TryCreate(int index, out ISkUiView view, out object? key, out object? item)
    {
        view = null!;
        item = null;
        key = null;
        if (!CanRealize(index))
            return false;
        key = GetRecycleKey(index);
        if (index < _sizes.Count && key is not null && TakeRecycledView(key) is { } recycled)
        {
            view = recycled;
        }
        else
        {
            var created = CreateItemView(index, key);
            if (created is null)
            {
                if (index < _sizes.Count)
                    throw new InvalidOperationException($"{GetType().Name} created no view for item {index} of {_sizes.Count}: only an endless list ends, at {nameof(ItemCount)}.");
                _hasMoreItems = false;
                return false;
            }
            view = created;
            if (index == _sizes.Count)
                _sizes.SetCount(index + 1);
        }
        BindItemView(index, view, key);
        item = GetItem(index);
        return true;
    }

    /// <summary>Creates, attaches and measures the item at <paramref name="index"/>, first or last in the realized range.</summary>
    private bool RealizeAt(int index, bool atEnd)
    {
        var started = Stopwatch.GetTimestamp();
        if (!TryCreate(index, out var view, out var key, out var item))
            return false;
        ValidateChild(view);
        var realized = new Realized(index, view, key, item);
        if (atEnd)
            _realized.Add(realized);
        else
            _realized.Insert(0, realized);
        AddLogicalChild((Element)view);
        MeasureItem(index, view);
        // What an item costs here (template or recycling, binding, measuring), for the automatic prefetch budget.
        var cost = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        _itemCost = double.IsNaN(_itemCost) ? cost : _itemCost + (cost - _itemCost) * 0.2;
        _realizedCount++;
        _peakRealized = Math.Max(_peakRealized, _realized.Count);
        _childrenChanged = true;
        if (ItemRealized is not null)
            Raise(() => ItemRealized?.Invoke(this, new SkUiVirtualItemEventArgs(index, view, item)));
        return true;
    }

    private void MeasureItem(int index, ISkUiView view)
    {
        var fixedExtent = _sizes.FixedExtent;
        var size = view.Measure(_cross, fixedExtent > 0 ? fixedExtent : double.PositiveInfinity);
        _sizes.SetSize(index, size.Height);
    }

    private void ReleaseAt(int position, bool recycle = true)
    {
        var realized = _realized[position];
        _realized.RemoveAt(position);
        RemoveLogicalChild((Element)realized.View);
        _childrenChanged = true;
        UnbindItemView(realized.Index, realized.View);
        if (recycle && realized.Key is { } key)
        {
            if (!_pool.TryGetValue(key, out var pooled))
                _pool[key] = pooled = new Stack<ISkUiView>();
            // Up to as many views as were ever realized at once: a far jump releases all of them, and as many are needed again.
            if (pooled.Count < Math.Max(8, _peakRealized))
                pooled.Push(realized.View);
        }
        if (ItemReleased is not null)
            Raise(() => ItemReleased?.Invoke(this, new SkUiVirtualItemEventArgs(realized.Index, realized.View, realized.Item)));
    }

    private void ReleaseAll(bool recycle)
    {
        if (_realized.Count == 0)
            return;
        for (var position = _realized.Count - 1; position >= 0; position--)
            ReleaseAt(position, recycle);
        InvalidateRender(SkUiRenderDirty.Children);
        _childrenChanged = false;
    }

    /// <summary>The realized view of the item at <paramref name="index"/>, or <c>null</c> when it is not realized.</summary>
    public ISkUiView? GetRealizedView(int index)
    {
        var position = PositionOf(index);
        return position < 0 ? null : _realized[position].View;
    }

    /// <summary>Indices of the realized items (tests, diagnostics).</summary>
    internal (int First, int Last) RealizedRange => _realized.Count == 0 ? (-1, -1) : (_realized[0].Index, _realized[^1].Index);

    /// <summary>The views waiting in the recycling pool.</summary>
    internal IEnumerable<ISkUiView> RecycledViews => _pool.Values.SelectMany(static views => views);

    /// <summary>Views waiting in the recycling pool (tests, diagnostics).</summary>
    internal int PooledViews => _pool.Values.Sum(stack => stack.Count);

    /// <summary>Item sizes (tests, diagnostics).</summary>
    internal SkUiVirtualItemSizes Sizes => _sizes;

    private int PositionOf(int index)
    {
        if (_realized.Count == 0)
            return -1;
        var position = index - _realized[0].Index;
        return position >= 0 && position < _realized.Count ? position : -1;
    }

    /// <summary>
    /// Measures the item at <paramref name="index"/> again: its view when realized (as when its content changes size by
    /// itself), else its size is forgotten and estimated until it is realized. Items before the first visible one keep what
    /// shows in place.
    /// </summary>
    public void RemeasureItem(int index)
    {
        if (index < 0 || index >= _sizes.Count)
            throw new ArgumentOutOfRangeException(nameof(index), index, "The index is not an item of the layout.");
        if (GetRealizedView(index) is { } view)
        {
            view.InvalidateMeasure();
            return;
        }
        var anchor = CaptureAnchor();
        _sizes.Forget(index);
        Reanchor(anchor.Index, anchor.Start);
        InvalidateMeasureFromChild();
    }

    #endregion

    #region Collection changes

    private void Insert(int index, int count)
    {
        index = Math.Clamp(index, 0, _sizes.Count);
        var anchor = CaptureAnchor();
        _sizes.Insert(index, count);
        ShiftRealized(index, count);
        // Items inserted inside the realized range are realized there, so the range stays contiguous.
        if (_realized.Count > 0 && index > _realized[0].Index && index <= _realized[^1].Index)
        {
            var position = index - _realized[0].Index;
            for (var offset = 0; offset < count; offset++)
                RealizeInside(index + offset, position + offset);
        }
        if (anchor.Index >= index)
            anchor.Index += count;
        Reanchor(anchor.Index, anchor.Start);
    }

    private void Remove(int index, int count)
    {
        count = Math.Min(count, _sizes.Count - index);
        if (count <= 0)
            return;
        var anchor = CaptureAnchor();
        for (var position = _realized.Count - 1; position >= 0; position--)
            if (_realized[position].Index >= index && _realized[position].Index < index + count)
                ReleaseAt(position);
        _sizes.Remove(index, count);
        ShiftRealized(index + count, -count);
        if (anchor.Index >= index + count)
            anchor.Index -= count;
        else if (anchor.Index >= index)
            anchor.Index = Math.Min(index, _sizes.Count - 1);
        Reanchor(anchor.Index, anchor.Start);
    }

    private void Move(int index, int count, int newIndex)
    {
        var anchor = CaptureAnchor();
        for (var position = _realized.Count - 1; position >= 0; position--)
            if (_realized[position].Index >= index && _realized[position].Index < index + count)
                ReleaseAt(position);
        _sizes.Move(index, newIndex, count);
        // The others follow as after removing the items and inserting them again.
        ShiftRealized(index + count, -count);
        ShiftRealized(newIndex, count);
        // Moved into the realized range: realized there, so the range stays contiguous.
        if (_realized.Count > 0 && newIndex > _realized[0].Index && newIndex <= _realized[^1].Index)
        {
            var position = newIndex - _realized[0].Index;
            for (var offset = 0; offset < count; offset++)
                RealizeInside(newIndex + offset, position + offset);
        }
        if (anchor.Index >= index && anchor.Index < index + count)
            anchor.Index = newIndex + anchor.Index - index;
        else
        {
            if (anchor.Index >= index + count)
                anchor.Index -= count;
            if (anchor.Index >= newIndex)
                anchor.Index += count;
        }
        Reanchor(anchor.Index, anchor.Start);
    }

    private void ShiftRealized(int from, int delta)
    {
        for (var position = 0; position < _realized.Count; position++)
            if (_realized[position].Index >= from)
                _realized[position] = _realized[position] with { Index = _realized[position].Index + delta };
    }

    private void RealizeInside(int index, int position)
    {
        if (!TryCreate(index, out var view, out var key, out var item))
            return;
        ValidateChild(view);
        _realized.Insert(position, new Realized(index, view, key, item));
        AddLogicalChild((Element)view);
        if (!double.IsNaN(_cross))
            MeasureItem(index, view);
        _childrenChanged = true;
        if (ItemRealized is not null)
            Raise(() => ItemRealized?.Invoke(this, new SkUiVirtualItemEventArgs(index, view, item)));
    }

    /// <summary>
    /// The item whose place on screen is kept across a change: the first realized item that shows, while the start of the
    /// items does not show (at the very top, inserted items push the others down).
    /// </summary>
    private (int Index, double Start) CaptureAnchor()
    {
        if (_pinnedAnchor >= 0 && _pinnedAnchor < _sizes.Count)
            return (_pinnedAnchor, _sizes.OffsetOf(_pinnedAnchor));
        if (_realized.Count == 0 || double.IsNaN(_cross))
            return (-1, 0);
        var window = ComputeWindow();
        var top = window.Top - _padding.Top;
        if (top <= 0)
            return (-1, 0);
        foreach (var realized in _realized)
        {
            var end = _sizes.OffsetOf(realized.Index) + _sizes.SizeOf(realized.Index);
            if (end > top)
                return (realized.Index, _sizes.OffsetOf(realized.Index));
        }
        return (-1, 0);
    }

    private void Reanchor(int index, double start)
    {
        if (index < 0 || index >= _sizes.Count)
            return;
        var delta = _sizes.OffsetOf(index) - start;
        if (delta != 0)
            CorrectScroll(delta);
    }

    #endregion

    #region Layout

    /// <inheritdoc />
    internal override IEnumerable<ISkUiView> SkiaChildren
    {
        get
        {
            foreach (var realized in _realized)
                yield return realized.View;
        }
    }

    /// <inheritdoc />
    internal override void AddRenderChildren(List<ISkUiRenderable> children)
    {
        foreach (var realized in _realized)
            if (realized.View is ISkUiRenderable renderable)
                children.Add(renderable);
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var cross = Math.Max(0, widthConstraint - _padding.HorizontalThickness);
        _measuring = true;
        try
        {
            Realize(Phase.Measure, cross);
        }
        finally
        {
            _measuring = false;
        }
        var width = 0d;
        foreach (var realized in _realized)
            width = Math.Max(width, realized.View.DesiredSize.Width);
        _reportedLength = _sizes.TotalLength + _padding.VerticalThickness;
        return new Size(width + _padding.HorizontalThickness, _reportedLength);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        _arrangedWidth = Math.Max(0, size.Width - _padding.HorizontalThickness);
        Realize(Phase.Arrange, _cross);
        ArrangeRealized();
        if (!_updating)
            FlushDeferredEvents();
    }

    private bool _missingScrollerReported;

    /// <summary>
    /// Reports once per layout, as a <c>SkiaUi:</c> trace line, when it is shown without a drawn scroller above it: it then
    /// cannot scroll, and it creates only the items the surface shows (inside a native <c>ScrollView</c>, which makes the
    /// surface as tall as the list, every item). Checked only on a live surface, so trees laid out before they are complete
    /// are not reported.
    /// </summary>
    private void ReportMissingScroller()
    {
        if (_missingScrollerReported || _sizes.Count == 0)
            return;
        for (var ancestor = SkiaParent; ancestor is not null; ancestor = ancestor.SkiaParent)
            if (ancestor is SkUiScrollView)
                return;
        if (!IsShown)
            return;
        _missingScrollerReported = true;
        var name = string.IsNullOrEmpty(AutomationId) ? GetType().Name : $"{GetType().Name} '{AutomationId}'";
        Trace.WriteLine($"SkiaUi: {name} has no drawn scroller (SkUiScrollView) above it: it cannot scroll, and creates only the "
            + "items the surface shows (inside a native ScrollView, every item). Put it inside an SkUiScrollView, or use SkUiVirtualScrollView.");
    }

    private void ArrangeRealized()
    {
        if (_realized.Count == 0 || double.IsNaN(_arrangedWidth))
            return;
        var y = _padding.Top + _sizes.OffsetOf(_realized[0].Index);
        foreach (var realized in _realized)
        {
            var height = _sizes.SizeOf(realized.Index);
            realized.View.Arrange(new Rect(_padding.Left, y, _arrangedWidth, height));
            y += height + _sizes.Spacing;
        }
    }

    void ISkUiScrollListener.OnAncestorScrollChanged() => Update();

    /// <summary>Realizes and releases items for the current window outside a layout pass (scrolling, settings, prefetch frames).</summary>
    private void Update()
    {
        // Not laid out yet: the first layout realizes.
        if (!double.IsNaN(_cross) && !double.IsNaN(_arrangedWidth))
            Realize(Phase.Update, _cross);
        if (!_updating && !_measuring)
            FlushDeferredEvents();
    }

    private enum Phase
    {
        /// <summary>Measuring: realized items are measured; nothing is arranged; the caller reports the new height.</summary>
        Measure,
        /// <summary>Arranging: the caller arranges the realized items.</summary>
        Arrange,
        /// <summary>Between layouts: realized items are arranged here, and a new height asks for a layout.</summary>
        Update
    }

    private void Realize(Phase phase, double cross)
    {
        if (_updating)
            return; // a correction of the scroll offset reports back here
        _updating = true;
        try
        {
            RealizeCore(phase, cross);
        }
        finally
        {
            if (!_racing)
                _sizes.ThawEstimate(); // already thawed by a pass that finished
            _updating = false;
        }
    }

    private void RealizeCore(Phase phase, double cross)
    {
        if (_pinTask is { IsCompleted: true })
        {
            _pinTask = null;
            _pinnedAnchor = -1;
        }
        var window = ComputeWindow();
        _top = window.Top - _padding.Top;
        _bottom = window.Bottom - _padding.Top;
        _shift = 0;
        _childrenChanged = false;
        _passStart = Stopwatch.GetTimestamp();
        _prefetched = 0;
        if (phase == Phase.Update)
            TrackMotion();
        // Items measured in this pass (or, while racing, until the motion slows down) do not move the estimate, and so the
        // anchor, until it ends: see FreezeEstimate. A far animated scroll would otherwise jump by thousands of DIPs per pass.
        var length = window.Length;
        // Racing: the motion passed more than a viewport in its last frame, or an animated ScrollToIndex is still more than two
        // viewports from its item (known before the first frame of the motion reports a speed).
        _racing = RacingTravel() > length
            || (_pinnedAnchor >= 0 && _pinnedAnchor < _sizes.Count && IsFlinging && Math.Abs(_sizes.OffsetOf(_pinnedAnchor) - _top) > 2 * length);
        _sizes.FreezeEstimate();
        ChooseAnchor();

        // Measuring: a new width forgets every size; realized items measure again (cached unless their content changed).
        if (phase == Phase.Measure)
        {
            if (cross != _cross)
            {
                _cross = cross;
                _sizes.ForgetAll();
            }
            foreach (var realized in _realized)
                MeasureItem(realized.Index, realized.View);
            Reanchor();
        }

        if (!IsVisible || double.IsNaN(_cross))
        {
            ReleaseAll(recycle: true);
            Finish(phase, complete: true);
            return;
        }

        var flinging = IsFlinging;
        // A render-thread motion passing more than a viewport per frame (a far animated scroll, the fastest flings) shows
        // each item for a frame at most: prefetched items would never show, so only what shows is created, also in the
        // relayouts of the same frame.
        var racing = _racing;
        var flingAhead = flinging && !racing ? Math.Min(Math.Abs(_velocity) * FlingLookahead, 2 * length) : 0;
        var ahead = racing ? 0 : _prefetchFactor * length + flingAhead;
        var behind = racing ? 0 : _prefetchBehindFactor * length;
        var (down, up) = _direction >= 0 ? (ahead, behind) : (behind, ahead);
        var release = Math.Max(_releaseFactor * length, Math.Max(ahead, behind) + length / 2);

        ReleaseOutside(_top - release, _bottom + release);
        // What shows is created at once. While scrolling, that is prefetch falling behind: the next budgets grow.
        var realizedBefore = _realizedCount;
        Cover(0, 0, budgeted: false);
        _passMoving = IsScrolling;
        if (_passMoving && racing)
            _boost = 1; // creating what shows is all there is to do: not prefetch falling behind
        else if (_passMoving)
            _boost = _realizedCount != realizedBefore ? Math.Min(MaxBoost, _boost * 2) : Math.Max(1, _boost * 0.9);
        else
            _boost = 1;
        // Prefetch in the scroll direction first, within the budget.
        _passBudget = _prefetchBudget ?? TimeSpan.FromMilliseconds(AutomaticBudget(
            AnimationClock.FrameInterval.TotalMilliseconds, _passMoving, _velocity, _sizes.Estimate, _itemCost, _boost));
        _passStart = Stopwatch.GetTimestamp();
        var complete = _direction >= 0
            ? Cover(0, down, budgeted: true) && Cover(up, down, budgeted: true)
            : Cover(up, 0, budgeted: true) && Cover(up, down, budgeted: true);

        Finish(phase, complete);
    }

    private void Finish(Phase phase, bool complete)
    {
        ReportMissingScroller();
        // The average of the items measured in this pass applies now, as one correction (after a racing motion slowed down).
        if (!_racing)
        {
            _sizes.ThawEstimate();
            Reanchor();
        }
        if (_shift != 0)
            CorrectScroll(_shift);
        if (complete)
            StopPrefetchTick();
        else
            StartPrefetchTick();
        if (_childrenChanged)
        {
            _childrenChanged = false;
            InvalidateRender(SkUiRenderDirty.Children);
        }
        UpdateVisibleRange();
        if (phase == Phase.Measure)
            return;
        if (phase == Phase.Update)
            ArrangeRealized();
        // The extent changed (measured items, discovered ones): the scrollers lay out again before the next frame.
        if (_sizes.TotalLength + _padding.VerticalThickness != _reportedLength)
            InvalidateMeasureFromChild();
    }

    /// <summary>
    /// Realizes items until the realized range covers the window widened by <paramref name="above"/> and
    /// <paramref name="below"/>, from its ends. Budgeted: stops once <see cref="PrefetchBudget"/> is used up (after at least one
    /// item per pass); returns whether the range is covered.
    /// </summary>
    private bool Cover(double above, double below, bool budgeted)
    {
        while (true)
        {
            var start = Math.Max(0, _top - above);
            var end = _bottom + below;
            if (end <= start)
                return true;
            int next;
            bool atEnd;
            if (_realized.Count == 0)
            {
                if (_sizes.Count == 0 ? !CanRealize(0) : start >= _sizes.TotalLength && !CanRealize(_sizes.Count))
                    return true;
                next = _sizes.Count == 0 ? 0 : Math.Min(_sizes.IndexAt(start), _sizes.Count - 1);
                if (start >= _sizes.TotalLength && CanRealize(_sizes.Count))
                    next = _sizes.Count;
                atEnd = true;
            }
            else
            {
                var last = _realized[^1].Index;
                var first = _realized[0].Index;
                if (_sizes.OffsetOf(last) + _sizes.SizeOf(last) < end && CanRealize(last + 1))
                {
                    next = last + 1;
                    atEnd = true;
                }
                else if (first > 0 && _sizes.OffsetOf(first) > start)
                {
                    next = first - 1;
                    atEnd = false;
                }
                else
                {
                    return true;
                }
            }
            if (budgeted && _prefetched > 0 && Stopwatch.GetElapsedTime(_passStart) >= _passBudget)
                return false;
            if (!RealizeAt(next, atEnd))
            {
                // The end of an endless list was found: nothing more below, items above may still be missing.
                if (atEnd && !_hasMoreItems && _realized.Count > 0)
                    continue;
                return true;
            }
            if (budgeted)
                _prefetched++;
            if (_anchor < 0)
                ChooseAnchor();
            else
                Reanchor();
        }
    }

    private void ReleaseOutside(double start, double end)
    {
        while (_realized.Count > 0 && _sizes.OffsetOf(_realized[^1].Index) > end)
            ReleaseAt(_realized.Count - 1);
        while (_realized.Count > 0 && _sizes.OffsetOf(_realized[0].Index) + _sizes.SizeOf(_realized[0].Index) < start)
            ReleaseAt(0);
        if (_anchor >= 0 && PositionOf(_anchor) < 0 && _pinnedAnchor < 0)
            ChooseAnchor();
    }

    /// <summary>The anchor of this pass: a pinned target, else the first realized item that shows (unless the start of the items shows).</summary>
    private void ChooseAnchor()
    {
        _anchor = -1;
        if (_pinnedAnchor >= 0 && _pinnedAnchor < _sizes.Count)
            _anchor = _pinnedAnchor;
        else if (_top > 0 && _bottom > _top)
            foreach (var realized in _realized)
            {
                if (_sizes.OffsetOf(realized.Index) + _sizes.SizeOf(realized.Index) <= _top)
                    continue;
                if (_sizes.OffsetOf(realized.Index) < _bottom)
                    _anchor = realized.Index;
                break;
            }
        if (_anchor >= 0)
            _anchorStart = _sizes.OffsetOf(_anchor);
    }

    /// <summary>Sizes before the anchor changed: the window (and, at the end of the pass, the scroll offset) follows the anchor.</summary>
    private void Reanchor()
    {
        if (_anchor < 0)
            return;
        var start = _sizes.OffsetOf(_anchor);
        var delta = start - _anchorStart;
        if (delta == 0)
            return;
        _anchorStart = start;
        _shift += delta;
        _top += delta;
        _bottom += delta;
    }

    /// <summary>Moves the innermost vertical scroller's offset by <paramref name="delta"/> (scroll anchoring).</summary>
    private void CorrectScroll(double delta)
    {
        for (var ancestor = SkiaParent; ancestor is not null; ancestor = ancestor.SkiaParent)
            if (ancestor is SkUiScrollView { Orientation: ScrollOrientation.Vertical or ScrollOrientation.Both } scroller)
            {
                scroller.CorrectScrollOffset(0, delta);
                return;
            }
    }

    /// <summary>
    /// What can show, in this layout's coordinates: the intersection of every ancestor scroller's viewport (and the surface),
    /// with the innermost viewport's length (for prefetch distances). Before the first arrange, positions are unknown and
    /// taken as the start of each parent. Without any bound, nothing shows.
    /// </summary>
    private (double Top, double Bottom, double Length) ComputeWindow()
    {
        double top = double.NegativeInfinity, bottom = double.PositiveInfinity, length = double.PositiveInfinity;
        double y = 0;
        SkUiView node = this;
        while (node.SkiaParent is { } parent)
        {
            if (node.Frame.Height >= 0)
                y += node.Frame.Y;
            if (parent is SkUiScrollView scroller)
            {
                var (offset, viewport) = scroller.VerticalWindow;
                top = Math.Max(top, offset - y);
                bottom = Math.Min(bottom, offset + viewport - y);
                length = Math.Min(length, viewport);
                y -= offset;
            }
            node = parent;
        }
        if (node.Frame.Height > 0)
        {
            top = Math.Max(top, -y);
            bottom = Math.Min(bottom, node.Frame.Height - y);
            length = Math.Min(length, node.Frame.Height);
        }
        if (double.IsInfinity(length))
            return (0, 0, 0);
        return (top, Math.Max(top, bottom), length);
    }

    private bool IsFlinging
    {
        get
        {
            for (var ancestor = SkiaParent; ancestor is not null; ancestor = ancestor.SkiaParent)
                if (ancestor is SkUiScrollView { IsMotionRunning: true })
                    return true;
            return false;
        }
    }

    /// <summary>How far the innermost ancestor scroller's render-thread motion moves per frame (0 without one).</summary>
    private double RacingTravel()
    {
        for (var ancestor = SkiaParent; ancestor is not null; ancestor = ancestor.SkiaParent)
            if (ancestor is SkUiScrollView { IsMotionRunning: true } scroller)
                return scroller.MotionTravel;
        return 0;
    }

    /// <summary>An ancestor scroller is dragged, flung or animated.</summary>
    private bool IsScrolling
    {
        get
        {
            for (var ancestor = SkiaParent; ancestor is not null; ancestor = ancestor.SkiaParent)
                if (ancestor is SkUiScrollView { IsScrolling: true })
                    return true;
            return false;
        }
    }

    /// <summary>The prefetch budget of the last pass (tests, diagnostics, demo status).</summary>
    internal TimeSpan LastPrefetchBudget => _passBudget;

    /// <summary>What realizing an item has cost here, in ms (moving average; NaN before the first).</summary>
    internal double ItemCost => _itemCost;

    /// <summary>Follows the window's movement: the scroll direction, and its speed while a fling runs.</summary>
    private void TrackMotion()
    {
        var now = Stopwatch.GetTimestamp();
        if (!double.IsNaN(_lastTop) && _top != _lastTop)
        {
            _direction = _top > _lastTop ? 1 : -1;
            var seconds = Stopwatch.GetElapsedTime(_lastTopTime, now).TotalSeconds;
            var velocity = seconds > 0 ? (_top - _lastTop) / seconds : 0;
            _velocity = seconds > 0.1 ? 0 : (_velocity + velocity) / 2;
        }
        _lastTop = _top;
        _lastTopTime = now;
    }

    /// <summary>How much of an item must show for it to count as visible, in DIPs.</summary>
    private const double VisibleSliver = 0.5;

    private void UpdateVisibleRange()
    {
        int first = -1, last = -1;
        if (_sizes.Count > 0 && _bottom > _top && _bottom > 0 && _top < _sizes.TotalLength)
        {
            // An item showing less than half a DIP does not count (offsets far down a list are floats on the render thread).
            first = _sizes.IndexAt(Math.Max(0, Math.Min(_top + VisibleSliver, _bottom - VisibleSliver)));
            last = _sizes.IndexAt(Math.Max(0, Math.Max(_bottom - VisibleSliver, _top + VisibleSliver)));
        }
        if (first == FirstVisibleIndex && last == LastVisibleIndex)
            return;
        FirstVisibleIndex = first;
        LastVisibleIndex = last;
        Raise(() =>
        {
            OnPropertyChanged(nameof(FirstVisibleIndex));
            OnPropertyChanged(nameof(LastVisibleIndex));
            VisibleRangeChanged?.Invoke(this, new SkUiVisibleRangeChangedEventArgs(first, last));
        });
        // Checked when what shows changes, not when items are added: a page appended item by item fires once.
        if (_remainingItemsThreshold >= 0 && last >= 0 && last >= _sizes.Count - 1 - _remainingItemsThreshold && _thresholdCount != _sizes.Count)
        {
            _thresholdCount = _sizes.Count;
            Raise(() =>
            {
                RemainingItemsThresholdReached?.Invoke(this, EventArgs.Empty);
                var command = RemainingItemsThresholdReachedCommand;
                var parameter = RemainingItemsThresholdReachedCommandParameter;
                if (command?.CanExecute(parameter) == true)
                    command.Execute(parameter);
            });
        }
    }

    /// <summary>
    /// Raises an event now, or once the realization pass ends (after the arrange when measuring): handlers may change the
    /// items or the layout, which a pass in progress (or a measure) would lose.
    /// </summary>
    private void Raise(Action raise)
    {
        if (_measuring || _updating)
            (_deferredEvents ??= []).Add(raise);
        else
            raise();
    }

    private void FlushDeferredEvents()
    {
        if (_deferredEvents is not { Count: > 0 } events)
            return;
        var pending = events.ToArray();
        events.Clear();
        foreach (var raise in pending)
            raise();
    }

    #endregion

    #region Prefetch frames

    /// <summary>Continues prefetch on the next UI frames (the surface's animation clock).</summary>
    private void StartPrefetchTick()
    {
        if (_prefetchTick is not null)
            return;
        _startingTick = true;
        try
        {
            _prefetchTick = AnimationClock.Start(_ =>
            {
                if (!_startingTick)
                    Update();
            }, TimeSpan.FromSeconds(1), repeat: true);
        }
        finally
        {
            _startingTick = false;
        }
    }

    private void StopPrefetchTick()
    {
        var tick = _prefetchTick;
        _prefetchTick = null;
        tick?.Dispose();
    }

    /// <inheritdoc />
    protected override void OnAnimationRootChanged(bool subtreeDetached = false)
    {
        base.OnAnimationRootChanged(subtreeDetached);
        // A clock of the previous root no longer ticks this layout.
        StopPrefetchTick();
        RegisterWithScrollers(subtreeDetached || Parent is null);
    }

    /// <summary>Follows every ancestor scroller (registered there, so nothing is walked on each offset change).</summary>
    private void RegisterWithScrollers(bool detached)
    {
        if (_scrollers is { Count: > 0 } previous)
        {
            foreach (var scroller in previous)
                scroller.UnregisterScrollListener(this);
            previous.Clear();
        }
        if (detached)
            return;
        for (var ancestor = SkiaParent; ancestor is not null; ancestor = ancestor.SkiaParent)
            if (ancestor is SkUiScrollView scroller)
            {
                scroller.RegisterScrollListener(this);
                (_scrollers ??= []).Add(scroller);
            }
    }

    #endregion

    #region Scrolling to an item

    /// <summary>
    /// Scrolls the innermost vertical scroller around this layout so the item at <paramref name="index"/> shows at
    /// <paramref name="position"/> (<see cref="ScrollToPosition.MakeVisible"/> scrolls only when it is not fully visible),
    /// animated on the render thread or at once. The item is measured first; items between are estimated, and the item
    /// keeps its place on screen while they are measured on the way, so it ends where asked. Before the first layout the
    /// request waits for it; a newer scroll or a touch cancels the task. Without a vertical scroller nothing happens.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not an item, or <paramref name="position"/> is not defined.</exception>
    public Task ScrollToIndex(int index, ScrollToPosition position = ScrollToPosition.MakeVisible, bool animated = true)
    {
        if (!Enum.IsDefined(position))
            throw new ArgumentOutOfRangeException(nameof(position));
        if (index < 0 || index >= _sizes.Count)
            throw new ArgumentOutOfRangeException(nameof(index), index, "The index is not an item of the layout.");
        SkUiScrollView? target = null;
        for (var ancestor = SkiaParent; ancestor is not null && target is null; ancestor = ancestor.SkiaParent)
            if (ancestor is SkUiScrollView { Orientation: ScrollOrientation.Vertical or ScrollOrientation.Both } scroller)
                target = scroller;
        if (target is null)
            return Task.CompletedTask;
        var controller = ((ISkUiScrollHost)target).Scroller;
        // The item keeps its place on screen while items before it are measured, also on the way there.
        _pinnedAnchor = index;
        _pinTask = null;
        var task = controller.ScrollToTargetAsync(() => OffsetOfItem(target, controller, index, position), animated);
        if (task.IsCompleted)
            _pinnedAnchor = -1;
        else
            _pinTask = task;
        return task;
    }

    private Point OffsetOfItem(SkUiScrollView scroller, SkUiScrollController controller, int index, ScrollToPosition position)
    {
        if (index >= _sizes.Count)
            return new Point(controller.X, controller.Y);
        EnsureMeasured(index);
        var origin = SkUiScrollController.GetContentBounds(scroller, this) ?? Rect.Zero;
        var item = new Rect(origin.X + _padding.Left, origin.Y + _padding.Top + _sizes.OffsetOf(index),
            Math.Max(0, origin.Width - _padding.HorizontalThickness), _sizes.SizeOf(index));
        var offset = controller.GetOffsetFor(item, position);
        return new Point(controller.X, offset.Y);
    }

    /// <summary>Gives the item at <paramref name="index"/> its measured size: a view measured off the layout and recycled.</summary>
    private void EnsureMeasured(int index)
    {
        if (_sizes.IsMeasured(index) || PositionOf(index) >= 0 || double.IsNaN(_cross))
            return;
        if (!TryCreate(index, out var view, out var key, out _))
            return;
        var anchor = CaptureAnchor();
        MeasureItem(index, view);
        Reanchor(anchor.Index, anchor.Start);
        UnbindItemView(index, view);
        if (key is not null)
        {
            if (!_pool.TryGetValue(key, out var pooled))
                _pool[key] = pooled = new Stack<ISkUiView>();
            pooled.Push(view);
        }
        InvalidateMeasureFromChild();
    }

    #endregion

    private readonly record struct Realized(int Index, ISkUiView View, object? Key, object? Item);
}
