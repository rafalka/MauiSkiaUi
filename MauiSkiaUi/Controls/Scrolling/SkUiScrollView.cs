using System.Runtime.CompilerServices;
using MauiSkiaUi.Rendering;

namespace MauiSkiaUi;

/// <summary>A descendant that follows what an ancestor <see cref="SkUiScrollView"/> shows (a virtual layout realizing its items).</summary>
internal interface ISkUiScrollListener
{
    /// <summary>The scroller's offset or viewport changed (also during its layout).</summary>
    void OnAncestorScrollChanged();
}

/// <summary>
/// A single-surface scroller with clamped offsets, wheel input and inertial fling.
/// The scroll offset is a composite-time children translation: scrolling never re-records content, and
/// fling / animated scrolls run on the render thread, so they stay smooth while the UI thread is busy.
/// The render thread reports offsets back each frame to keep <see cref="ScrollX"/> / <see cref="ScrollY"/>,
/// hit-testing, <see cref="Scrolled"/> and native overlays in sync.
/// Drags take part in the gesture arena: content taps win unless the pointer moves past the touch slop along a
/// direction this scroller can move; nested scrollers get the drags of their own axis, and a scroller at its edge
/// hands the drag (and the fling) to the outer scroller on the same axis. Past the outermost edge the content
/// overscrolls (<see cref="Overscroll"/>: bounce or stretch, per look by default) and springs back.
/// Scroll bars (<see cref="VerticalScrollBarVisibility"/>, <see cref="HorizontalScrollBarVisibility"/>) are drawn by the
/// look, placed from the offset on the render thread, and fade out after scrolling stops.
/// <see cref="Overscrolled"/> and <see cref="PullReleased"/> report pulls past the edges (with <see cref="PullEdges"/>, also
/// of content that does not overflow), for effects and gestures built on them (stretchy headers, pull to dismiss).
/// </summary>
public class SkUiScrollView : SkUiContentView, ISkUiScrollHost
{
    /// <summary>A container in a skeleton made from content, also while empty: its children are the bones.</summary>
    internal override bool IsSkeletonContainer => true;

    private SkUiScrollController? _scroller;
    /// <summary>Registered <see cref="SkUiMauiContentView"/> descendants that need offset sync (avoids O(tree) walks).</summary>
    private List<SkUiMauiContentView>? _overlayDescendants;
    /// <summary>Registered descendants that follow what this scroller shows (virtual layouts).</summary>
    private List<ISkUiScrollListener>? _scrollListeners;
    /// <summary>The viewport the last measure offered the content (the arranged viewport is not known before the first arrange).</summary>
    private Size _measureViewport;

    /// <summary>Creates a scroller whose motion stops when its surface unloads.</summary>
    public SkUiScrollView()
    {
        Unloaded += (_, _) => CancelInteraction();
    }

    /// <summary>
    /// The scrolling engine, created on first use: property callbacks can run before the constructor (MAUI applies
    /// app-level implicit styles inside its base constructor).
    /// </summary>
    private SkUiScrollController Scroller
    {
        get
        {
            if (_scroller is null)
            {
                _scroller = new SkUiScrollController(this, InvalidateRender, OnOffsetChanged);
                _scroller.MovingChanged += OnMovingChanged;
                _scroller.OverscrollChanged += OnOverscrollReported;
                _scroller.PullReleased += OnPullReleased;
            }
            return _scroller;
        }
    }

    SkUiScrollController ISkUiScrollHost.Scroller => Scroller;

    double ISkUiScrollHost.KeyboardInset { get => KeyboardInset; set => KeyboardInset = value; }

    /// <summary>Bindable enabled scroll axes.</summary>
    public static readonly BindableProperty OrientationProperty = BindableProperty.Create(nameof(Orientation), typeof(ScrollOrientation), typeof(SkUiScrollView), ScrollOrientation.Vertical,
        validateValue: (_, value) => Enum.IsDefined((ScrollOrientation)value),
        propertyChanged: (view, _, value) => ((SkUiScrollView)view).OnOrientationChanged((ScrollOrientation)value));
    /// <summary>Enabled axes; Neither disables scrolling.</summary>
    public ScrollOrientation Orientation { get => (ScrollOrientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    /// <summary>Current horizontal offset in DIPs (updated from the render thread during fling).</summary>
    public double ScrollX => Scroller.X;
    /// <summary>Current vertical offset in DIPs (updated from the render thread during fling).</summary>
    public double ScrollY => Scroller.Y;
    /// <summary>Measured scrollable content extent, including padding.</summary>
    public Size ContentSize => Scroller.Extent;
    /// <summary>Raised after a clamped offset changes.</summary>
    public event EventHandler<ScrolledEventArgs>? Scrolled;

    /// <summary>
    /// Raised when <see cref="ScrollToAsync(double, double, bool)"/> or <see cref="ScrollToAsync(Element, ScrollToPosition, bool)"/>
    /// requests a scroll, before it starts (MAUI's event, with MAUI's arguments).
    /// </summary>
    public event EventHandler<ScrollToRequestedEventArgs>? ScrollToRequested;

    /// <summary>Bindable property for <see cref="HorizontalScrollBarVisibility"/>.</summary>
    public static readonly BindableProperty HorizontalScrollBarVisibilityProperty = BindableProperty.Create(nameof(HorizontalScrollBarVisibility),
        typeof(ScrollBarVisibility), typeof(SkUiScrollView), ScrollBarVisibility.Default,
        validateValue: (_, value) => Enum.IsDefined((ScrollBarVisibility)value),
        propertyChanged: (view, _, value) => ((SkUiScrollView)view).OnScrollBarVisibilityChanged(horizontal: true, (ScrollBarVisibility)value));

    /// <summary>Bindable property for <see cref="VerticalScrollBarVisibility"/>.</summary>
    public static readonly BindableProperty VerticalScrollBarVisibilityProperty = BindableProperty.Create(nameof(VerticalScrollBarVisibility),
        typeof(ScrollBarVisibility), typeof(SkUiScrollView), ScrollBarVisibility.Default,
        validateValue: (_, value) => Enum.IsDefined((ScrollBarVisibility)value),
        propertyChanged: (view, _, value) => ((SkUiScrollView)view).OnScrollBarVisibilityChanged(horizontal: false, (ScrollBarVisibility)value));

    /// <summary>
    /// The horizontal scroll bar (MAUI's): <see cref="ScrollBarVisibility.Default"/> shows it while scrolling and fades it out
    /// after, <see cref="ScrollBarVisibility.Always"/> keeps it while the content is wider than the viewport,
    /// <see cref="ScrollBarVisibility.Never"/> hides it.
    /// </summary>
    public ScrollBarVisibility HorizontalScrollBarVisibility
    {
        get => (ScrollBarVisibility)GetValue(HorizontalScrollBarVisibilityProperty);
        set => SetValue(HorizontalScrollBarVisibilityProperty, value);
    }

    /// <summary>The vertical scroll bar (MAUI's), on the left in right-to-left layouts; see <see cref="HorizontalScrollBarVisibility"/>.</summary>
    public ScrollBarVisibility VerticalScrollBarVisibility
    {
        get => (ScrollBarVisibility)GetValue(VerticalScrollBarVisibilityProperty);
        set => SetValue(VerticalScrollBarVisibilityProperty, value);
    }

    /// <summary>Bindable property for <see cref="Overscroll"/>.</summary>
    public static readonly BindableProperty OverscrollProperty = BindableProperty.Create(nameof(Overscroll), typeof(SkUiOverscrollMode),
        typeof(SkUiScrollView), SkUiOverscrollMode.Default,
        validateValue: (_, value) => Enum.IsDefined((SkUiOverscrollMode)value),
        propertyChanged: (view, _, value) => ((SkUiScrollView)view).OnOverscrollChanged((SkUiOverscrollMode)value));

    /// <summary>
    /// What a drag or fling past the content's edge does: <see cref="SkUiOverscrollMode.Default"/> follows the look
    /// (<see cref="SkUiLook.DefaultOverscroll"/>; the default look bounces on iOS and Mac Catalyst and stretches on
    /// Android). SkiaUi extension.
    /// </summary>
    public SkUiOverscrollMode Overscroll
    {
        get => (SkUiOverscrollMode)GetValue(OverscrollProperty);
        set => SetValue(OverscrollProperty, value);
    }

    /// <summary>Bindable property for <see cref="PullEdges"/>.</summary>
    public static readonly BindableProperty PullEdgesProperty = BindableProperty.Create(nameof(PullEdges), typeof(SkUiScrollEdges),
        typeof(SkUiScrollView), SkUiScrollEdges.None,
        validateValue: (_, value) => ((SkUiScrollEdges)value & ~SkUiScrollEdges.All) == 0,
        propertyChanged: (view, _, value) => ((SkUiScrollView)view).Scroller.PullEdges = (SkUiScrollEdges)value);

    /// <summary>
    /// Edges a drag can pull the content past also when it does not overflow or <see cref="Overscroll"/> is
    /// <see cref="SkUiOverscrollMode.None"/> (default <see cref="SkUiScrollEdges.None"/>; only edges of the scrolled axes).
    /// The pull is reported (<see cref="Overscrolled"/>, <see cref="PullReleased"/>) with the rubber band's resistance, and
    /// moves the content only as the overscroll mode draws it. SkiaUi extension.
    /// </summary>
    public SkUiScrollEdges PullEdges
    {
        get => (SkUiScrollEdges)GetValue(PullEdgesProperty);
        set => SetValue(PullEdgesProperty, value);
    }

    /// <summary>Distance shown past the left (negative) or right (positive) edge, in DIPs (see <see cref="Overscrolled"/>).</summary>
    public double OverscrollX => Scroller.OverscrollX;

    /// <summary>Distance shown past the top (negative) or bottom (positive) edge, in DIPs (see <see cref="Overscrolled"/>).</summary>
    public double OverscrollY => Scroller.OverscrollY;

    /// <summary>
    /// Raised when the distance shown past the edges changes: while a drag pulls the content past an edge (by the overscroll
    /// mode, or <see cref="PullEdges"/>), a fling bounces past it, or the content springs back (once per frame then, reported
    /// from the render thread), ending at 0. The arguments are reused: read them in the handler. Changing translation,
    /// scale, rotation or opacity of a view from it re-records nothing. SkiaUi extension.
    /// </summary>
    public event EventHandler<SkUiOverscrolledEventArgs>? Overscrolled;

    /// <summary>
    /// Raised when a drag is released with the content past an edge, before it springs back, once per edge, with the
    /// distance shown past it (pull to dismiss, to load more, a refresh of your own). SkiaUi extension.
    /// </summary>
    public event EventHandler<SkUiPullReleasedEventArgs>? PullReleased;

    /// <summary>Sets <see cref="PullEdges"/> (same as the property setter).</summary>
    public SkUiScrollView SetPullEdges(SkUiScrollEdges value)
    {
        if ((value & ~SkUiScrollEdges.All) != 0) throw new ArgumentOutOfRangeException(nameof(value));
        PullEdges = value;
        return this;
    }

    private void OnOverscrollReported()
    {
        SyncRegisteredOverlays();
        Overscrolled?.Invoke(this, Scroller.OverscrolledArgs());
    }

    private void OnPullReleased(SkUiScrollEdges edge, double distance) =>
        PullReleased?.Invoke(this, new SkUiPullReleasedEventArgs(edge, distance));

    /// <summary>
    /// The vertical scroll bar: a Core node drawn by the look along the viewport edge (style it with
    /// <see cref="Core.SkUiCoreScrollBar.ThumbColor"/> or <see cref="Core.SkUiCoreScrollBar.IsInteractive"/>); shown by
    /// <see cref="VerticalScrollBarVisibility"/>.
    /// </summary>
    public Core.SkUiCoreScrollBar VerticalScrollBar => Scroller.ScrollBar(ScrollOrientation.Vertical);

    /// <summary>The horizontal scroll bar; see <see cref="VerticalScrollBar"/>.</summary>
    public Core.SkUiCoreScrollBar HorizontalScrollBar => Scroller.ScrollBar(ScrollOrientation.Horizontal);

    /// <summary>Bindable property for <see cref="SnapPointsType"/>.</summary>
    public static readonly BindableProperty SnapPointsTypeProperty = BindableProperty.Create(nameof(SnapPointsType), typeof(SnapPointsType),
        typeof(SkUiScrollView), SnapPointsType.None,
        validateValue: (_, value) => Enum.IsDefined((SnapPointsType)value),
        propertyChanged: (view, _, value) => ((SkUiScrollView)view).Scroller.SnapPointsType = (SnapPointsType)value);

    /// <summary>Bindable property for <see cref="SnapPointsAlignment"/>.</summary>
    public static readonly BindableProperty SnapPointsAlignmentProperty = BindableProperty.Create(nameof(SnapPointsAlignment), typeof(SnapPointsAlignment),
        typeof(SkUiScrollView), SnapPointsAlignment.Start,
        validateValue: (_, value) => Enum.IsDefined((SnapPointsAlignment)value),
        propertyChanged: (view, _, value) => ((SkUiScrollView)view).Scroller.SnapPointsAlignment = (SnapPointsAlignment)value);

    /// <summary>
    /// Whether drags, flings and wheel scrolling end with a child of the content lined up with the viewport (MAUI's
    /// CollectionView enum): <see cref="SnapPointsType.Mandatory"/> on the snap point nearest to where the motion would stop,
    /// <see cref="SnapPointsType.MandatorySingle"/> one child per swipe (carousels). SkiaUi extension (MAUI's ScrollView has no
    /// snap points). Programmatic scrolls do not snap.
    /// </summary>
    public SnapPointsType SnapPointsType
    {
        get => (SnapPointsType)GetValue(SnapPointsTypeProperty);
        set => SetValue(SnapPointsTypeProperty, value);
    }

    /// <summary>Which edge (or the center) of a content child lines up with the viewport's at a snap point.</summary>
    public SnapPointsAlignment SnapPointsAlignment
    {
        get => (SnapPointsAlignment)GetValue(SnapPointsAlignmentProperty);
        set => SetValue(SnapPointsAlignmentProperty, value);
    }

    private void OnScrollBarVisibilityChanged(bool horizontal, ScrollBarVisibility value)
    {
        if (horizontal)
            Scroller.HorizontalScrollBarVisibility = value;
        else
            Scroller.VerticalScrollBarVisibility = value;
        // Always reserves a gutter: the content is laid out again.
        InvalidateMeasureOverride();
        Scroller.ArrangeScrollBars(IsRightToLeft);
    }

    private void OnOverscrollChanged(SkUiOverscrollMode value)
    {
        Scroller.ClearOverscroll();
        Scroller.Overscroll = value;
        InvalidateRender(SkUiRenderDirty.Props);
    }

    /// <summary>Where the content shows, in local coordinates: the arranged rectangle minus reserved scroll bar gutters.</summary>
    internal Rect Scrollport => Scroller.Scrollport;

    /// <summary>Offset at which the content is drawn: the scroll offset plus a bounce past the edges (native overlays follow it).</summary>
    internal Point VisualScrollOffset => new(Scroller.ChildrenOffset.X, Scroller.ChildrenOffset.Y);

    /// <summary>True while a render-thread fling or animated scroll is running.</summary>
    internal bool IsMotionRunning => Scroller.IsMotionRunning;

    /// <summary>How far the running render-thread motion moves per frame (see <see cref="SkUiScrollController.MotionTravel"/>).</summary>
    internal double MotionTravel => Scroller.IsMotionRunning ? Scroller.MotionTravel : 0;

    /// <summary>True while the user drags this scroller or a fling / animated scroll runs.</summary>
    public bool IsScrolling => Scroller.IsMoving;

    private void OnMovingChanged(bool moving)
    {
        if (SkUiDiagnostics.TraceOn)
            SkUiDiagnostics.Write($"scroll {AutomationId ?? GetHashCode().ToString()} moving={moving} dragging={Scroller.Dragging} motion={Scroller.IsMotionRunning} offset={Scroller.X:F0},{Scroller.Y:F0} overlays={_overlayDescendants?.Count ?? 0}");
        if (_overlayDescendants is { Count: > 0 } overlays)
            foreach (var overlay in overlays.ToArray())
                overlay.NotifyAncestorScrollMotion(moving);
        OnPropertyChanged(nameof(IsScrolling));
    }

    /// <summary>Sets orientation (same as the property setter).</summary>
    public SkUiScrollView SetOrientation(ScrollOrientation value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        Orientation = value;
        return this;
    }

    private void OnOrientationChanged(ScrollOrientation value)
    {
        CancelInteraction();
        Scroller.ClearOverscroll();
        Scroller.Orientation = value;
        InvalidateMeasureOverride();
    }

    /// <summary>MAUI's scroll view reads <see cref="SafeAreaRegions.Default"/> (on iOS, the platform's content insets); drawn, it is edge to edge.</summary>
    private protected override SafeAreaEdges DefaultSafeAreaEdges => SafeAreaEdges.Default;

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        // Scroll bars that always show reserve gutters beside the content.
        var (left, right, bottom) = Scroller.Gutters(IsRightToLeft);
        var width = Math.Max(0, widthConstraint - left - right);
        var height = Math.Max(0, heightConstraint - bottom);
        // Across the scroll axis an explicitly sized content keeps its size (clipped), as in MAUI.
        if (LayoutChild is { } content)
        {
            var inset = ContentInset + SafeAreaInset;
            width = SkUiScrollController.CrossConstraint(width, content.Width, content.MaximumWidth, content.Margin.HorizontalThickness + inset.HorizontalThickness);
            height = SkUiScrollController.CrossConstraint(height, content.Height, content.MaximumHeight, content.Margin.VerticalThickness + inset.VerticalThickness);
        }
        _measureViewport = new Size(width, height);
        var extent = base.MeasureContent(Scroller.Horizontal ? double.PositiveInfinity : width, Scroller.Vertical ? double.PositiveInfinity : height);
        var insets = Scroller.EffectiveInsets;
        extent = Scroller.InsetsVertical
            ? new Size(extent.Width, extent.Height + insets.Start + insets.End)
            : new Size(extent.Width + insets.Start + insets.End, extent.Height);
        Scroller.Extent = extent;
        return new Size(Math.Min(widthConstraint, extent.Width + left + right), Math.Min(heightConstraint, extent.Height + bottom));
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        Scroller.Layout(size, IsRightToLeft);
        var viewport = Scroller.Viewport;
        var extent = Scroller.Extent;
        // Content stays arranged at its layout origin (after the start inset; mirrored in right-to-left layouts); the offset is a
        // composite-time children translation.
        var (start, end) = Scroller.EffectiveInsets;
        var (top, bottom, left, right) = Scroller.InsetsVertical ? (start, end, 0d, 0d) : (0d, 0d, start, end);
        // The same inset the measure reserved: the padding plus, on a surface root, the safe area.
        var inset = ContentInset + SafeAreaInset;
        LayoutChild?.Arrange(new Rect(
            inset.Left + left, inset.Top + top,
            Math.Max(0, Math.Max(extent.Width, viewport.Width) - inset.HorizontalThickness - left - right),
            Math.Max(0, Math.Max(extent.Height, viewport.Height) - inset.VerticalThickness - top - bottom)));
        InvalidateRender(SkUiRenderDirty.Props);
        if (!_rtlStartApplied && IsRightToLeft && Scroller.Horizontal && Scroller.MaxX > 0)
        {
            _rtlStartApplied = true;
            Scroller.SetOffset(Scroller.MaxX, Scroller.Y);
        }
        else
        {
            Scroller.Clamp();
        }
        Scroller.ArrangeScrollBars(IsRightToLeft);
        Scroller.OnArranged();
        NotifyScrollListeners();
    }

    /// <summary>
    /// The vertical scroll offset and viewport height, in this scroller's children space: what a descendant can show. Before
    /// the first arrange the viewport is the one the measure offered the content.
    /// </summary>
    internal (double Offset, double Length) VerticalWindow =>
        (Scroller.Y, Scroller.Bounds == Size.Zero ? _measureViewport.Height : Scroller.Viewport.Height);

    /// <summary>The horizontal scroll offset and viewport width (physical, in this scroller's children space); see <see cref="VerticalWindow"/>.</summary>
    internal (double Offset, double Length) HorizontalWindow =>
        (Scroller.X, Scroller.Bounds == Size.Zero ? _measureViewport.Width : Scroller.Viewport.Width);

    /// <summary>
    /// The parts of the viewport covered by views drawn over this scroller, at the start and the end of its scroll axis (a
    /// collection view's sticky header and footer; see <see cref="SkUiScrollController.Insets"/>): the content is arranged
    /// after the start one and the extent grows by both, so at the start and the end of the scroll range nothing of the
    /// content is covered.
    /// Set by the owner while it measures, before it measures this scroller: only this scroller's measure is invalidated.
    /// </summary>
    internal (double Start, double End) Insets
    {
        get => Scroller.Insets;
        set
        {
            if (Scroller.Insets == value)
                return;
            Scroller.Insets = value;
            InvalidateMeasureOverride();
        }
    }

    /// <summary>The soft keyboard's cover of the viewport's bottom (see <see cref="SkUiScrollController.KeyboardInset"/>), set by the focus manager.</summary>
    internal double KeyboardInset
    {
        get => Scroller.KeyboardInset;
        set
        {
            // Exact: a layout pass only when the cover changed.
            if (Scroller.KeyboardInset == value)
                return;
            Scroller.KeyboardInset = value;
            InvalidateMeasureOverride();
        }
    }

    /// <summary>Moves the offset by a layout correction (scroll anchoring; see <see cref="SkUiScrollController.CorrectOffset"/>).</summary>
    internal void CorrectScrollOffset(double dx, double dy) => Scroller.CorrectOffset(dx, dy);

    /// <summary>Registers a descendant that follows this scroller's offset and viewport.</summary>
    internal void RegisterScrollListener(ISkUiScrollListener listener)
    {
        _scrollListeners ??= [];
        if (!_scrollListeners.Contains(listener))
            _scrollListeners.Add(listener);
    }

    /// <summary>Removes a previously registered scroll listener.</summary>
    internal void UnregisterScrollListener(ISkUiScrollListener listener) => _scrollListeners?.Remove(listener);

    private void NotifyScrollListeners()
    {
        if (_scrollListeners is not { Count: > 0 } listeners)
            return;
        // A listener may realize or release views, and so (un)register nested listeners.
        if (listeners.Count == 1)
        {
            listeners[0].OnAncestorScrollChanged();
            return;
        }
        foreach (var listener in listeners.ToArray())
            listener.OnAncestorScrollChanged();
    }

    /// <inheritdoc />
    /// <remarks>Includes a gutter on the left (right-to-left), so mirrored content lands right of it.</remarks>
    internal override double ChildrenSpaceWidth => Math.Max(Scroller.Extent.Width, Scroller.Viewport.Width) + Scroller.ScrollportLeft;

    /// <summary>RTL horizontal scrollers start at their logical start (the right end) once per content / direction.</summary>
    private bool _rtlStartApplied;

    /// <inheritdoc />
    internal override void OnEffectiveFlowDirectionChanged()
    {
        _rtlStartApplied = false;
        base.OnEffectiveFlowDirectionChanged();
    }

    /// <inheritdoc />
    internal override void OnGetRenderProps(ref SkUiRenderProps props) => Scroller.FillRenderProps(ref props);

    /// <inheritdoc />
    internal override void AddRenderChildren(List<ISkUiRenderable> children)
    {
        base.AddRenderChildren(children);
        Scroller.AddScrollBars(children);
    }

    /// <inheritdoc />
    internal override void CollectGestureRecognizers(List<SkUiGestureRecognizer> recognizers)
    {
        base.CollectGestureRecognizers(recognizers);
        if (Scroller.Orientation != ScrollOrientation.Neither && Scroller.IsUserScrollEnabled)
            recognizers.Add(Scroller.Gesture);
    }

    /// <inheritdoc />
    internal override void CancelGestures()
    {
        base.CancelGestures();
        _scroller?.Gesture.Cancel(); // nothing to cancel before the engine exists
    }

    /// <summary>Clamps and sets an offset without remeasuring or rearranging content.</summary>
    public SkUiScrollView ScrollTo(double horizontalOffset, double verticalOffset)
    {
        Scroller.StopMotion();
        Scroller.ClearOverscroll();
        Scroller.SetOffset(horizontalOffset, verticalOffset);
        return this;
    }

    /// <summary>Starts a render-thread scroll animation; disposing the returned handle cancels it.</summary>
    public IDisposable AnimateScrollTo(double horizontalOffset, double verticalOffset, TimeSpan duration) =>
        Scroller.AnimateTo(horizontalOffset, verticalOffset, duration);

    /// <summary>
    /// Scrolls immediately or animates over 300 ms on the render thread (MAUI's <c>ScrollToAsync</c>; nothing happens when
    /// <see cref="Orientation"/> is <see cref="ScrollOrientation.Neither"/>). Raises <see cref="ScrollToRequested"/>. A
    /// superseding gesture, scroll, or unload cancels the task.
    /// </summary>
    public Task ScrollToAsync(double horizontalOffset, double verticalOffset, bool animated = true)
    {
        if (Orientation == ScrollOrientation.Neither)
            return Task.CompletedTask;
        if (!double.IsFinite(horizontalOffset))
            throw new ArgumentOutOfRangeException(nameof(horizontalOffset));
        if (!double.IsFinite(verticalOffset))
            throw new ArgumentOutOfRangeException(nameof(verticalOffset));
        RaiseScrollToRequested(() => CreatePositionRequest(horizontalOffset, verticalOffset, animated));
        return Scroller.ScrollToAsync(horizontalOffset, verticalOffset, animated);
    }

    /// <summary>
    /// Scrolls so that <paramref name="element"/> (a drawn descendant, a Core node under a <see cref="Core.SkUiCoreHost"/>, or a
    /// MAUI view inside a <see cref="SkUiMauiContentView"/>) is at <paramref name="position"/> of the viewport, as MAUI's
    /// <c>ScrollToAsync(Element, ScrollToPosition, bool)</c>: <see cref="ScrollToPosition.MakeVisible"/> scrolls only when it is
    /// not fully visible. Before the first layout the request waits for it. Raises <see cref="ScrollToRequested"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="element"/> is not inside this scroll view, or <paramref name="position"/> is not defined.</exception>
    public Task ScrollToAsync(Element element, ScrollToPosition position, bool animated) => ScrollToTargetAsync(element, position, animated);

    /// <summary>Scrolls a Core node under a <see cref="Core.SkUiCoreHost"/> inside this view into view; see <see cref="ScrollToAsync(Element, ScrollToPosition, bool)"/>.</summary>
    public Task ScrollToAsync(Core.SkUiCoreNode node, ScrollToPosition position, bool animated) => ScrollToTargetAsync(node, position, animated);

    private Task ScrollToTargetAsync(object target, ScrollToPosition position, bool animated)
    {
        // MAUI's order: a scroller that cannot scroll ignores the request before validating it.
        if (Orientation == ScrollOrientation.Neither)
            return Task.CompletedTask;
        ArgumentNullException.ThrowIfNull(target);
        if (!Enum.IsDefined(position))
            throw new ArgumentException("position is not a valid ScrollToPosition", nameof(position));
        if (SkUiScrollController.GetContentBounds(this, target) is null)
            throw new ArgumentException("The element does not belong to this scroll view.", nameof(target));
        if (target is Element element)
            RaiseScrollToRequested(() => CreateElementRequest(element, position, animated));
        return Scroller.ScrollToTargetAsync(() => GetScrollPosition(target, position), animated);
    }

    /// <summary>
    /// The offset that puts <paramref name="item"/> at <paramref name="pos"/> of the viewport (MAUI's
    /// <c>GetScrollPositionForElement</c>; not clamped, the scroll clamps it). <see cref="ScrollToPosition.MakeVisible"/> returns
    /// the current offset when the item is fully visible.
    /// </summary>
    public Point GetScrollPositionForElement(VisualElement item, ScrollToPosition pos) => GetScrollPosition(item, pos);

    private Point GetScrollPosition(object target, ScrollToPosition position) =>
        SkUiScrollController.GetContentBounds(this, target) is { } bounds
            ? Scroller.GetOffsetFor(bounds, position)
            : new Point(ScrollX, ScrollY);

    /// <summary>
    /// Raises <see cref="ScrollToRequested"/> when someone listens. A MAUI version without the internal constructors
    /// (<see cref="MissingMethodException"/>) skips the event; the scroll still runs.
    /// </summary>
    private void RaiseScrollToRequested(Func<ScrollToRequestedEventArgs> create)
    {
        if (ScrollToRequested is not { } handler)
            return;
        ScrollToRequestedEventArgs args;
        try
        {
            args = create();
        }
        catch (MissingMethodException)
        {
            return;
        }
        handler(this, args);
    }

    // MAUI's ScrollToRequestedEventArgs has internal constructors only; the accessors are trimming- and Native-AOT-safe.
    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    private static extern ScrollToRequestedEventArgs CreatePositionRequest(double scrollX, double scrollY, bool shouldAnimate);

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    private static extern ScrollToRequestedEventArgs CreateElementRequest(Element element, ScrollToPosition position, bool shouldAnimate);

    private void OnOffsetChanged()
    {
        OnPropertyChanged(nameof(ScrollX));
        OnPropertyChanged(nameof(ScrollY));
        SyncRegisteredOverlays();
        NotifyScrollListeners();
        Scrolled?.Invoke(this, new ScrolledEventArgs(ScrollX, ScrollY));
    }

    /// <summary>Registers a hosted overlay so offset updates can sync it without walking the full tree.</summary>
    internal void RegisterOverlayDescendant(SkUiMauiContentView overlay)
    {
        _overlayDescendants ??= [];
        if (_overlayDescendants.Contains(overlay))
            return;
        _overlayDescendants.Add(overlay);
        if (Scroller.IsMoving)
            overlay.NotifyAncestorScrollMotion(true);
    }

    /// <summary>Removes a previously registered overlay descendant.</summary>
    internal void UnregisterOverlayDescendant(SkUiMauiContentView overlay)
    {
        if (_overlayDescendants?.Remove(overlay) == true && Scroller.IsMoving)
            overlay.NotifyAncestorScrollMotion(false);
    }

    /// <summary>Pushes updated root-relative bounds to registered <see cref="SkUiMauiContentView"/> descendants.</summary>
    private void SyncRegisteredOverlays()
    {
        if (_overlayDescendants is null || _overlayDescendants.Count == 0)
            return;
        foreach (var overlay in _overlayDescendants)
            overlay.NotifyAncestorScrollOffsetChanged();
    }

    private void CancelInteraction()
    {
        if (_scroller is null)
            return;
        Scroller.StopMotion();
        Scroller.Gesture.Cancel();
        Scroller.ClearOverscroll();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if ((propertyName == nameof(IsEnabled) && !IsEnabled)
            || (propertyName == nameof(IsVisible) && !IsVisible)
            || (propertyName == nameof(InputTransparent) && InputTransparent)) CancelInteraction();
    }

    /// <inheritdoc />
    protected override void OnContentChanged()
    {
        Scroller.Reset();
        _rtlStartApplied = false;
        base.OnContentChanged();
        InvalidateRender(SkUiRenderDirty.Props);
    }

    /// <inheritdoc />
    protected override void OnParentSet()
    {
        if (Parent is null)
            CancelInteraction();
        base.OnParentSet();
    }

    /// <inheritdoc />
    protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
    {
        base.OnPopulateSemantics(info);
        SkUiSemantics.PopulateScroller(info, Scroller);
    }

    /// <inheritdoc />
    protected override bool OnSemanticsAction(SkUiSemanticsActions action) =>
        SkUiSemantics.PerformScroll(Scroller, action) || base.OnSemanticsAction(action);
}
