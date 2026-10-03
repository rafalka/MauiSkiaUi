using MauiSkiaUi.Core;
using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>A drawn scroller (<see cref="SkUiScrollView"/>, <see cref="Core.SkUiCoreScrollView"/>) driven by the shared scroll gesture.</summary>
internal interface ISkUiScrollHost : ISkUiInputNode
{
    SkUiScrollController Scroller { get; }
}

/// <summary>
/// Scroll state and motion shared by the SkUi* and Core scroll views: clamped offsets applied as a composite-time
/// children translation, render-thread tweens / flings with offsets reported back, wheel input, nested-scroll
/// chaining, overscroll past the edges (through the generic children transform: <see cref="SkUiOverscroll"/>), scroll
/// bars as pinned, scroll-linked Core nodes (<see cref="SkUiCoreScrollBar"/>), and scrolling to a target. The owner
/// supplies invalidation and change notification, and adds <see cref="AddScrollBars"/> to its render children.
/// </summary>
internal sealed class SkUiScrollController(ISkUiRenderable owner, Action<SkUiRenderDirty> invalidate, Action offsetChanged)
{
    /// <summary>The largest edge bounce of a fling, as a fraction of the viewport length.</summary>
    internal const double FlingOverscrollFraction = 0.15;

    private SkUiRenderAnimation? _motion;
    private SkUiRenderFling? _fling;
    private TaskCompletionSource? _scrollCompletion;
    private Size _extent;
    private Size _viewport;
    private double _pullX, _pullY;
    private (Func<Point> Resolve, bool Animated, TaskCompletionSource Completion)? _pendingTarget;
    private bool _arranged;

    public ScrollOrientation Orientation { get; set; } = ScrollOrientation.Vertical;

    /// <summary>
    /// The measure constraint of an axis this scroller does not scroll: at least the content's explicit size. As MAUI's
    /// ScrollView, which arranges its content at the content's desired size (platform measures return an explicit
    /// <c>WidthRequest</c> / <c>HeightRequest</c> even when it is larger than the viewport), a 400 DIP wide content of a
    /// vertical scroller with a 300 DIP viewport stays 400 wide and is clipped (not scrolled).
    /// </summary>
    /// <param name="constraint">The viewport's constraint on this axis (inside the padding).</param>
    /// <param name="explicitSize">The content's explicit size (negative or NaN: none).</param>
    /// <param name="maximum">The content's maximum size.</param>
    /// <param name="margin">The content's margin on this axis.</param>
    public static double CrossConstraint(double constraint, double explicitSize, double maximum, double margin) =>
        explicitSize >= 0 ? Math.Max(constraint, Math.Min(explicitSize, maximum) + margin) : constraint;

    /// <summary>Measured content extent (a running fling follows changes).</summary>
    public Size Extent
    {
        get => _extent;
        set
        {
            _extent = value;
            _fling?.SetLimits((float)MaxX, (float)MaxY);
        }
    }

    /// <summary>
    /// Room reserved beside the content for scroll bars with <see cref="ScrollBarVisibility.Always"/> on scrolling axes
    /// (<see cref="SkUiLook.ScrollBarReservedThickness"/>): on the right of the content for the vertical bar (on the left in
    /// right-to-left layouts) and below it for the horizontal one. Reserved even while the content fits, so a layout never
    /// depends on whether its own result overflows; bars that fade (<see cref="ScrollBarVisibility.Default"/>) draw over the
    /// content and reserve nothing.
    /// </summary>
    public (double Left, double Right, double Bottom) Gutters(bool rightToLeft)
    {
        var size = Math.Max(0, SkUiLook.Current.ScrollBarReservedThickness);
        var vertical = Vertical && VerticalScrollBarVisibility == ScrollBarVisibility.Always ? size : 0;
        var horizontal = Horizontal && HorizontalScrollBarVisibility == ScrollBarVisibility.Always ? size : 0;
        return (rightToLeft ? vertical : 0, rightToLeft ? 0 : vertical, horizontal);
    }

    /// <summary>
    /// Arranges the scroller at <paramref name="size"/>: the content's viewport (the scrollport) is what the
    /// <see cref="Gutters"/> leave.
    /// </summary>
    public void Layout(Size size, bool rightToLeft)
    {
        Bounds = size;
        var (left, right, bottom) = Gutters(rightToLeft);
        ScrollportLeft = Math.Min(left, size.Width);
        Viewport = new Size(Math.Max(0, size.Width - left - right), Math.Max(0, size.Height - bottom));
    }

    /// <summary>The scroller's arranged size (scrollport and gutters).</summary>
    public Size Bounds { get; private set; }

    /// <summary>Left edge of the scrollport (a reserved gutter on the left in right-to-left layouts).</summary>
    public double ScrollportLeft { get; private set; }

    /// <summary>The scrollport in the scroller's local coordinates: where the content shows.</summary>
    public Rect Scrollport => new(ScrollportLeft, 0, Viewport.Width, Viewport.Height);

    /// <summary>The content's viewport: the scroller's arranged size minus reserved gutters (a running fling follows changes).</summary>
    public Size Viewport
    {
        get => _viewport;
        private set
        {
            _viewport = value;
            _fling?.SetLimits((float)MaxX, (float)MaxY);
        }
    }

    public double X { get; private set; }
    public double Y { get; private set; }
    public bool Horizontal => Orientation is ScrollOrientation.Horizontal or ScrollOrientation.Both;
    public bool Vertical => Orientation is ScrollOrientation.Vertical or ScrollOrientation.Both;
    public double MaxX => Horizontal ? Math.Max(0, Extent.Width - Viewport.Width) : 0;
    public double MaxY => Vertical ? Math.Max(0, Extent.Height - Viewport.Height) : 0;
    public bool IsMotionRunning => _motion is not null;

    /// <summary>Scroll bar visibility per axis (MAUI's <c>HorizontalScrollBarVisibility</c> / <c>VerticalScrollBarVisibility</c>).</summary>
    public ScrollBarVisibility HorizontalScrollBarVisibility { get; set; }

    /// <inheritdoc cref="HorizontalScrollBarVisibility" />
    public ScrollBarVisibility VerticalScrollBarVisibility { get; set; }

    /// <summary>The owner's overscroll setting (<see cref="SkUiOverscrollMode.Default"/>: the look's).</summary>
    public SkUiOverscrollMode Overscroll { get; set; }

    /// <summary>What this scroller does past its edges now: <see cref="Overscroll"/>, or the look's default.</summary>
    public SkUiOverscrollMode EffectiveOverscroll =>
        (Overscroll == SkUiOverscrollMode.Default ? SkUiLook.Current.DefaultOverscroll : Overscroll) is var mode
        && mode is SkUiOverscrollMode.Bounce or SkUiOverscrollMode.Stretch ? mode : SkUiOverscrollMode.None;

    /// <summary>Distance shown past the start (negative) or end (positive) of the horizontal axis, in DIPs.</summary>
    public double OverscrollX { get; private set; }

    /// <inheritdoc cref="OverscrollX" />
    public double OverscrollY { get; private set; }

    /// <summary>The content is shown past an edge (dragged, bouncing or springing back).</summary>
    public bool IsOverscrolled => OverscrollX != 0 || OverscrollY != 0;

    /// <summary>Raised when <see cref="OverscrollX"/> / <see cref="OverscrollY"/> change (e.g. to move native overlays with the content).</summary>
    public event Action? OverscrollChanged;

    private bool _dragging;
    private bool _moving;

    /// <summary>A drag or render-thread motion (fling, animated scroll) is in progress.</summary>
    public bool IsMoving => _moving;

    /// <summary>Raised when <see cref="IsMoving"/> changes (e.g. to freeze native overlays while scrolling).</summary>
    public event Action<bool>? MovingChanged;

    /// <summary>Set by the scroll gesture while a drag owns the pointer.</summary>
    public bool Dragging
    {
        get => _dragging;
        set
        {
            if (value && !_dragging)
                _dragOrigin = new Point(X, Y);
            _dragging = value;
            UpdateMoving();
        }
    }

    /// <summary>The offset when the current (or last) drag started: a single-step snap moves one snap point from it.</summary>
    private Point _dragOrigin;

    private void UpdateMoving()
    {
        var moving = _dragging || _motion is not null;
        if (moving == _moving)
            return;
        _moving = moving;
        if (!moving)
            FadeBars();
        MovingChanged?.Invoke(moving);
    }

    /// <summary>Recognizer that turns drags into scrolling (created on first use).</summary>
    public SkUiScrollGestureRecognizer Gesture => _gesture ??= new SkUiScrollGestureRecognizer(this);
    private SkUiScrollGestureRecognizer? _gesture;

    public void SetOffset(double x, double y)
    {
        EnsureFinite(x, y);
        if (UpdateState(Math.Clamp(x, 0, MaxX), Math.Clamp(y, 0, MaxY)))
            invalidate(SkUiRenderDirty.Props);
    }

    /// <summary>Re-clamps after a layout change (extent / viewport); fading scroll bars stay as they are.</summary>
    public void Clamp()
    {
        if (UpdateState(Math.Clamp(X, 0, MaxX), Math.Clamp(Y, 0, MaxY), showScrollBars: false))
            invalidate(SkUiRenderDirty.Props);
    }

    /// <summary>Scrolls by an offset delta; returns the part that could not be applied (for chaining to an outer scroller).</summary>
    public Point ScrollBy(double dx, double dy)
    {
        var nextX = Horizontal ? Math.Clamp(X + dx, 0, MaxX) : X;
        var nextY = Vertical ? Math.Clamp(Y + dy, 0, MaxY) : Y;
        var remainder = new Point(dx - (nextX - X), dy - (nextY - Y));
        SetOffset(nextX, nextY);
        return remainder;
    }

    /// <summary>Whether an offset change in this direction is possible.</summary>
    public bool CanScroll(double dx, double dy) =>
        (Horizontal && ((dx > 0 && X < MaxX) || (dx < 0 && X > 0)))
        || (Vertical && ((dy > 0 && Y < MaxY) || (dy < 0 && Y > 0)));

    /// <summary>Whether a drag in this direction may pull the content past an edge (the axis scrolls and overscroll is on).</summary>
    public bool CanOverscroll(double dx, double dy) =>
        EffectiveOverscroll != SkUiOverscrollMode.None
        && ((dx != 0 && Horizontal && MaxX > 0) || (dy != 0 && Vertical && MaxY > 0));

    /// <summary>
    /// A wheel or trackpad delta (positive towards the start of each axis). Each axis this scroller can move that way
    /// is used up; the rest is returned for outer scrollers. A vertical-only delta (a mouse wheel) scrolls a
    /// horizontal-only scroller horizontally.
    /// </summary>
    public Point Wheel(double deltaX, double deltaY)
    {
        if (Orientation == ScrollOrientation.Neither || (deltaX == 0 && deltaY == 0))
            return new Point(deltaX, deltaY);
        var mapped = Horizontal && !Vertical && deltaX == 0;
        var dx = mapped ? -deltaY : Horizontal ? -deltaX : 0;
        var dy = Vertical ? -deltaY : 0;
        var useX = dx != 0 && CanScroll(dx, 0);
        var useY = dy != 0 && CanScroll(0, dy);
        if (!useX && !useY)
            return new Point(deltaX, deltaY);
        StopMotion();
        ClearOverscroll();
        SetOffset(useX ? X + dx : X, useY ? Y + dy : Y);
        if (SnapPointsType != SnapPointsType.None)
            SnapAfterWheel();
        // An axis this scroller moved is used up, also when its edge cut the step short: the rest is not handed to the
        // outer scrollers, so a list reaching its end does not jerk the page with the same step (as browsers do); the
        // next wheel event chains outwards (GestureTests.WheelScrollsInnermostScrollerThatCanMove).
        return mapped ? Point.Zero : new Point(useX ? 0 : deltaX, useY ? 0 : deltaY);
    }

    /// <summary>How long wheel / trackpad input must pause before a scroller with snap points settles on one.</summary>
    internal static readonly TimeSpan WheelSnapDelay = TimeSpan.FromMilliseconds(150);

    private int _wheelVersion;

    /// <summary>Settles on the nearest snap point once the wheel or trackpad stops (needs a UI dispatcher).</summary>
    private void SnapAfterWheel()
    {
        var version = ++_wheelVersion;
        Microsoft.Maui.Dispatching.Dispatcher.GetForCurrentThread()?.DispatchDelayed(WheelSnapDelay, () =>
        {
            if (version == _wheelVersion && !_moving)
                Snap(Point.Zero);
        });
    }

    public IDisposable AnimateTo(double x, double y, TimeSpan duration)
    {
        StopMotion();
        EnsureFinite(x, y);
        ClearOverscroll();
        var motion = StartTween(x, y, duration, completion: null);
        return new MotionHandle(this, motion);
    }

    public Task ScrollToAsync(double x, double y, bool animated)
    {
        StopMotion();
        DropPendingTarget();
        ClearOverscroll();
        if (!animated)
        {
            SetOffset(x, y);
            return Task.CompletedTask;
        }
        EnsureFinite(x, y);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _scrollCompletion = completion;
        StartTween(x, y, TimeSpan.FromMilliseconds(300), completion);
        return completion.Task;
    }

    /// <summary>
    /// Scrolls to the offset <paramref name="resolve"/> computes from the laid-out tree; before the scroller's first
    /// arrange the request waits for it (as MAUI's). A newer request, a content change or unloading cancels the task.
    /// </summary>
    public Task ScrollToTargetAsync(Func<Point> resolve, bool animated)
    {
        if (_arranged)
        {
            var target = resolve();
            return ScrollToAsync(target.X, target.Y, animated);
        }
        StopMotion();
        DropPendingTarget();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingTarget = (resolve, animated, completion);
        return completion.Task;
    }

    /// <summary>The owner arranged its content: resolves a scroll request that waited for the layout.</summary>
    public void OnArranged()
    {
        _arranged = true;
        if (_pendingTarget is not { } pending)
            return;
        _pendingTarget = null;
        var target = pending.Resolve();
        var task = ScrollToAsync(target.X, target.Y, pending.Animated);
        if (task.IsCompleted)
            pending.Completion.TrySetResult();
        else
            task.ContinueWith(static (done, state) =>
            {
                var completion = (TaskCompletionSource)state!;
                if (done.IsCanceled) completion.TrySetCanceled(); else completion.TrySetResult();
            }, pending.Completion, TaskScheduler.Default);
    }

    private void DropPendingTarget()
    {
        var pending = _pendingTarget;
        _pendingTarget = null;
        pending?.Completion.TrySetCanceled();
    }

    /// <summary>
    /// The offset that shows <paramref name="target"/> (in content coordinates) at <paramref name="position"/>, as MAUI's
    /// <c>ScrollView.GetScrollPositionForElement</c>: <see cref="ScrollToPosition.MakeVisible"/> keeps the offset when the
    /// target is fully visible and otherwise aligns its start or end (End when it begins after the viewport's start), so a
    /// target larger than the viewport moves even when part of it shows, exactly as MAUI 10's. Not clamped (scrolling clamps).
    /// </summary>
    public Point GetOffsetFor(Rect target, ScrollToPosition position)
    {
        var width = Viewport.Width;
        var height = Viewport.Height;
        var x = target.X;
        var y = target.Y;
        if (position == ScrollToPosition.MakeVisible)
        {
            var visible = new Rect(X, Y, width, height);
            if (visible.Contains(target))
                return new Point(X, Y);
            position = Orientation switch
            {
                ScrollOrientation.Vertical => y > visible.Y ? ScrollToPosition.End : ScrollToPosition.Start,
                ScrollOrientation.Horizontal => x > visible.X ? ScrollToPosition.End : ScrollToPosition.Start,
                _ => x > visible.X || y > visible.Y ? ScrollToPosition.End : ScrollToPosition.Start
            };
        }
        switch (position)
        {
            case ScrollToPosition.Center:
                x = x - width / 2 + target.Width / 2;
                y = y - height / 2 + target.Height / 2;
                break;
            case ScrollToPosition.End:
                x = x - width + target.Width;
                y = y - height + target.Height;
                break;
        }
        return new Point(x, y);
    }

    /// <summary>
    /// The bounds of <paramref name="target"/> in the children space of <paramref name="scroller"/> (its scroll content
    /// coordinates; nested scrollers' offsets included), or <c>null</c> when it is not a descendant. The target is a drawn
    /// node of either layer, or a MAUI element inside a hosted <see cref="SkUiMauiContentView"/>. Like MAUI, transforms
    /// are ignored.
    /// </summary>
    public static Rect? GetContentBounds(ISkUiRenderable scroller, object target)
    {
        if (ReferenceEquals(target, scroller))
        {
            var own = GetProps(scroller);
            return new Rect(0, 0, own.Width, own.Height);
        }
        double x = 0, y = 0, width, height;
        var current = target;
        if (target is VisualElement { } element && target is not ISkUiRenderable)
        {
            width = element.Width;
            height = element.Height;
            // A MAUI element hosted inside a drawn node: its layout position up to that node.
            while (current is VisualElement hosted and not ISkUiRenderable)
            {
                x += hosted.X;
                y += hosted.Y;
                current = hosted.Parent;
            }
        }
        else if (target is ISkUiRenderable drawn)
        {
            var props = GetProps(drawn);
            width = props.Width;
            height = props.Height;
        }
        else
        {
            return null;
        }
        if (current is not ISkUiRenderable node)
            return null;
        while (!ReferenceEquals(node, scroller))
        {
            var props = GetProps(node);
            x += props.X;
            y += props.Y;
            if (node.RenderParent is not { } parent)
                return null;
            if (!ReferenceEquals(parent, scroller))
            {
                var parentProps = GetProps(parent);
                x -= parentProps.ChildrenOffsetX;
                y -= parentProps.ChildrenOffsetY;
            }
            node = parent;
        }
        return new Rect(x, y, Math.Max(0, width), Math.Max(0, height));
    }

    private static SkUiRenderProps GetProps(ISkUiRenderable node)
    {
        var props = SkUiRenderProps.Default;
        node.GetRenderProps(ref props);
        return props;
    }

    /// <summary>Starts a render-thread fling with an offset velocity (DIPs / s); returns <c>false</c> when too slow.</summary>
    public bool StartFling(Point velocity)
    {
        var limit = SkUiGestureSettings.FlingMaximumVelocity;
        var vx = Horizontal ? Math.Clamp(velocity.X, -limit, limit) : 0;
        var vy = Vertical ? Math.Clamp(velocity.Y, -limit, limit) : 0;
        if (Math.Max(Math.Abs(vx), Math.Abs(vy)) < SkUiGestureSettings.FlingMinimumVelocity)
            return false;
        if (SnapPointsType != SnapPointsType.None)
            return Snap(new Point(vx, vy));
        StopMotion();
        var mode = EffectiveOverscroll;
        var bounce = mode != SkUiOverscrollMode.None;
        // A fling can only run past the edge it moves towards: a stretch scales about that edge.
        if (vx != 0) _stretchEndX = vx > 0;
        if (vy != 0) _stretchEndY = vy > 0;
        if (mode == SkUiOverscrollMode.Stretch)
            invalidate(SkUiRenderDirty.Props);
        SkUiRenderFling? fling = null;
        fling = new SkUiRenderFling((float)vx, (float)vy, (float)MaxX, (float)MaxY,
            bounce && MaxX > 0 ? (float)(Viewport.Width * FlingOverscrollFraction) : 0,
            bounce && MaxY > 0 ? (float)(Viewport.Height * FlingOverscrollFraction) : 0,
            mode, (float)Viewport.Width, (float)Viewport.Height,
            (x, y, overscrollX, overscrollY) => { if (ReferenceEquals(_motion, fling)) ApplyRenderScroll(x, y, overscrollX, overscrollY); })
        {
            Finished = (animation, _) =>
            {
                owner.RenderState.ActiveAnimations?.Remove(animation);
                if (ReferenceEquals(_motion, animation))
                {
                    _motion = null;
                    _fling = null;
                }
                UpdateMoving();
            }
        };
        _motion = _fling = fling;
        UpdateMoving();
        SkUiRenderInvalidation.Enqueue(owner, fling);
        return true;
    }

    /// <summary>Cancels the running render-thread motion; its last shown offset is reported back asynchronously.</summary>
    public void StopMotion()
    {
        var motion = _motion;
        _motion = null;
        _fling = null;
        motion?.Cancel();
        _scrollCompletion?.TrySetCanceled();
        _scrollCompletion = null;
        UpdateMoving();
    }

    /// <summary>Content replaced: stop and return to the origin.</summary>
    public void Reset()
    {
        StopMotion();
        DropPendingTarget();
        Gesture.Cancel();
        ClearOverscroll();
        X = Y = 0;
        Extent = Size.Zero;
    }

    #region Snap points

    /// <summary>
    /// Whether drags, flings and wheel scrolling end on a snap point (MAUI's <see cref="Microsoft.Maui.Controls.SnapPointsType"/>):
    /// <c>Mandatory</c> on the one nearest to where the motion would stop, <c>MandatorySingle</c> one snap point from where
    /// the drag started.
    /// </summary>
    public SnapPointsType SnapPointsType { get; set; }

    /// <summary>Which edge (or the center) of a content child lines up with the viewport's at a snap point.</summary>
    public SnapPointsAlignment SnapPointsAlignment { get; set; }

    /// <summary>
    /// Settles on a snap point with a render-thread spring that starts at <paramref name="velocity"/> (offset DIPs / s):
    /// the nearest to where a fling would stop, or (single) the next one from the drag's start in its direction. Returns
    /// <c>false</c> without snap points or when already on the target.
    /// </summary>
    public bool Snap(Point velocity)
    {
        if (SnapPointsType == SnapPointsType.None)
            return false;
        var x = Horizontal && MaxX > 0 ? SnapTarget(horizontal: true, X, velocity.X, _dragOrigin.X) : X;
        var y = Vertical && MaxY > 0 ? SnapTarget(horizontal: false, Y, velocity.Y, _dragOrigin.Y) : Y;
        if (Math.Abs(x - X) < 0.5 && Math.Abs(y - Y) < 0.5)
        {
            SetOffset(x, y);
            return false;
        }
        StopMotion();
        SkUiRenderScrollSpring? spring = null;
        spring = new SkUiRenderScrollSpring((float)x, (float)y, Horizontal ? (float)velocity.X : 0, Vertical ? (float)velocity.Y : 0,
            (float)MaxX, (float)MaxY,
            (reportedX, reportedY) => { if (ReferenceEquals(_motion, spring)) ApplyRenderScroll(reportedX, reportedY, 0, 0); })
        {
            Finished = (animation, _) =>
            {
                owner.RenderState.ActiveAnimations?.Remove(animation);
                if (ReferenceEquals(_motion, animation))
                    _motion = null;
                UpdateMoving();
            }
        };
        _motion = spring;
        UpdateMoving();
        SkUiRenderInvalidation.Enqueue(owner, spring);
        return true;
    }

    private double SnapTarget(bool horizontal, double current, double velocity, double origin)
    {
        var points = SnapOffsets(horizontal);
        if (points.Count == 0)
            return current;
        // Where a fling with this velocity would stop (the decay's total travel).
        var projected = current + velocity * SkUiRenderFling.TotalTravelPerVelocity;
        if (SnapPointsType == SnapPointsType.MandatorySingle && Math.Abs(velocity) >= SkUiGestureSettings.FlingMinimumVelocity)
        {
            if (velocity > 0)
            {
                foreach (var point in points)
                    if (point > origin + 0.5)
                        return point;
                return points[^1];
            }
            for (var index = points.Count - 1; index >= 0; index--)
                if (points[index] < origin - 0.5)
                    return points[index];
            return points[0];
        }
        var target = SnapPointsType == SnapPointsType.MandatorySingle ? current : projected;
        var nearest = points[0];
        foreach (var point in points)
            if (Math.Abs(point - target) < Math.Abs(nearest - target))
                nearest = point;
        return nearest;
    }

    /// <summary>
    /// The snap offsets along one axis, sorted: each child of the content (or the content itself, without children) aligned
    /// to the viewport by <see cref="SnapPointsAlignment"/>, clamped to the scroll range.
    /// </summary>
    internal List<double> SnapOffsets(bool horizontal)
    {
        var points = new List<double>();
        var children = new List<ISkUiRenderable>();
        owner.GetRenderChildren(children);
        var content = children.FirstOrDefault(child => child is not SkUiCoreScrollBar);
        if (content is null)
            return points;
        var contentProps = GetProps(content);
        var items = new List<ISkUiRenderable>();
        content.GetRenderChildren(items);
        if (items.Count == 0)
            items.Add(content);
        var viewport = horizontal ? Viewport.Width : Viewport.Height;
        var max = horizontal ? MaxX : MaxY;
        foreach (var item in items)
        {
            var props = GetProps(item);
            if (!props.IsVisible || (horizontal ? props.Width : props.Height) <= 0)
                continue;
            var start = ReferenceEquals(item, content) ? 0 : horizontal ? contentProps.X + props.X : contentProps.Y + props.Y;
            var size = horizontal ? props.Width : props.Height;
            var offset = SnapPointsAlignment switch
            {
                SnapPointsAlignment.Center => start + size / 2 - viewport / 2,
                SnapPointsAlignment.End => start + size - viewport,
                _ => start
            };
            points.Add(Math.Clamp(offset, 0, max));
        }
        points.Sort();
        for (var index = points.Count - 1; index > 0; index--)
            if (points[index] - points[index - 1] < 0.5)
                points.RemoveAt(index);
        return points;
    }

    #endregion

    #region Overscroll

    /// <summary>
    /// Takes back overscroll first when a drag moves towards the content again; returns the part of the delta left for
    /// scrolling.
    /// </summary>
    public Point Unpull(double dx, double dy)
    {
        if (_pullX == 0 && _pullY == 0)
            return new Point(dx, dy);
        dx = Unpull(ref _pullX, dx);
        dy = Unpull(ref _pullY, dy);
        ApplyPull();
        return new Point(dx, dy);
    }

    private static double Unpull(ref double pull, double delta)
    {
        if ((pull < 0 && delta > 0) || (pull > 0 && delta < 0))
        {
            var next = pull < 0 ? Math.Min(0, pull + delta) : Math.Max(0, pull + delta);
            delta -= next - pull;
            pull = next;
        }
        return delta;
    }

    /// <summary>Pulls the content past its edges by the part of a drag no scroller could use (on axes that overscroll).</summary>
    public void Pull(double dx, double dy)
    {
        if (dx != 0 && CanOverscroll(dx, 0))
            _pullX += dx;
        if (dy != 0 && CanOverscroll(0, dy))
            _pullY += dy;
        ApplyPull();
    }

    private void ApplyPull()
    {
        if (_pullX != 0) _stretchEndX = _pullX > 0;
        if (_pullY != 0) _stretchEndY = _pullY > 0;
        SetOverscroll(SkUiOverscroll.RubberBand(_pullX, Viewport.Width), SkUiOverscroll.RubberBand(_pullY, Viewport.Height));
    }

    /// <summary>
    /// Springs back from overscroll on the render thread (a drag released past an edge); returns <c>false</c> when the
    /// content is not past an edge.
    /// </summary>
    public bool SettleOverscroll()
    {
        _pullX = _pullY = 0;
        if (!IsOverscrolled)
            return false;
        StopMotion();
        SkUiRenderOverscrollSettle? settle = null;
        settle = new SkUiRenderOverscrollSettle((float)X, (float)Y, (float)OverscrollX, (float)OverscrollY, EffectiveOverscroll,
            (float)Viewport.Width, (float)Viewport.Height,
            (x, y) => { if (ReferenceEquals(_motion, settle)) ApplyRenderScroll((float)X, (float)Y, x, y); })
        {
            Finished = (animation, _) =>
            {
                owner.RenderState.ActiveAnimations?.Remove(animation);
                if (ReferenceEquals(_motion, animation))
                    _motion = null;
                UpdateMoving();
            }
        };
        _motion = settle;
        UpdateMoving();
        SkUiRenderInvalidation.Enqueue(owner, settle);
        return true;
    }

    /// <summary>Drops any overscroll at once (wheel, programmatic scrolls, unloading).</summary>
    public void ClearOverscroll()
    {
        _pullX = _pullY = 0;
        SetOverscroll(0, 0);
    }

    private void SetOverscroll(double x, double y)
    {
        if (x == OverscrollX && y == OverscrollY)
            return;
        OverscrollX = x;
        OverscrollY = y;
        invalidate(SkUiRenderDirty.Props);
        OnScrolled();
        OverscrollChanged?.Invoke();
    }

    #endregion

    #region Render properties and scroll bars

    private bool _stretchEndX, _stretchEndY;
    private SkUiCoreScrollBar? _verticalBar, _horizontalBar;

    /// <summary>The children offset the UI side shows: the scroll offset, plus a bounce past the edges.</summary>
    public SKPoint ChildrenOffset => EffectiveOverscroll == SkUiOverscrollMode.Bounce
        ? new SKPoint((float)(X + OverscrollX), (float)(Y + OverscrollY))
        : new SKPoint((float)X, (float)Y);

    /// <summary>Fills the owner's scroll render properties: the children offset, clip and (stretch overscroll) scale.</summary>
    public void FillRenderProps(ref SkUiRenderProps props)
    {
        var width = (float)Viewport.Width;
        var height = (float)Viewport.Height;
        var left = (float)ScrollportLeft;
        SkUiOverscroll.Apply(ref props, (float)X, (float)Y, (float)OverscrollX, (float)OverscrollY, EffectiveOverscroll, width, height);
        props.ChildrenScaleOrigin = new SKPoint(left + (_stretchEndX ? width : 0), _stretchEndY ? height : 0);
        props.ChildrenClipRect = new SKRect(left, 0, left + width, height);
    }

    /// <summary>Appends the scroller's own scroll bars (pinned Core nodes) after the owner's content.</summary>
    public void AddScrollBars(List<ISkUiRenderable> children)
    {
        if (_verticalBar is not null)
            children.Add(_verticalBar);
        if (_horizontalBar is not null)
            children.Add(_horizontalBar);
    }

    /// <summary>The scroller's own bar for <paramref name="orientation"/>, created on first use (shown by layout).</summary>
    public SkUiCoreScrollBar ScrollBar(ScrollOrientation orientation)
    {
        var vertical = orientation == ScrollOrientation.Vertical;
        if ((vertical ? _verticalBar : _horizontalBar) is { } existing)
            return existing;
        var bar = new SkUiCoreScrollBar((ISkUiScrollHost)owner, orientation, owned: true);
        bar.ApplyVisibility(vertical ? VerticalScrollBarVisibility : HorizontalScrollBarVisibility);
        switch (owner)
        {
            case SkUiCoreNode node: bar.AttachTo(node); break;
            case SkUiView view: bar.HostOwner = view; break;
        }
        if (vertical)
            _verticalBar = bar;
        else
            _horizontalBar = bar;
        invalidate(SkUiRenderDirty.Children);
        return bar;
    }

    /// <summary>A bar placed by the app follows this scroller (held weakly: dropping the bar releases it).</summary>
    internal void Attach(SkUiCoreScrollBar bar)
    {
        if (ReferenceEquals(bar, _verticalBar) || ReferenceEquals(bar, _horizontalBar))
            return;
        _placedBars ??= [];
        _placedBars.RemoveAll(reference => !reference.TryGetTarget(out _));
        _placedBars.Add(new WeakReference<SkUiCoreScrollBar>(bar));
    }

    private List<WeakReference<SkUiCoreScrollBar>>? _placedBars;

    private void ForEachBar(Action<SkUiCoreScrollBar> action)
    {
        if (_verticalBar is not null)
            action(_verticalBar);
        if (_horizontalBar is not null)
            action(_horizontalBar);
        if (_placedBars is null)
            return;
        foreach (var reference in _placedBars)
            if (reference.TryGetTarget(out var bar))
                action(bar);
    }

    /// <summary>
    /// Lays out the scroll bars after the owner arranged its content. Each own bar is a strip along the viewport edge (the
    /// vertical one on the left in right-to-left layouts) where a hovering pointer expands it; its thumb runs along the
    /// edge, both bars leave the corner free, and the thumb is as long as the visible part of the content (at least the
    /// look's minimum). Bars placed by the app re-lay out their thumbs.
    /// </summary>
    public void ArrangeScrollBars(bool rightToLeft)
    {
        var showVertical = Vertical && VerticalScrollBarVisibility != ScrollBarVisibility.Never && MaxY > 0;
        var showHorizontal = Horizontal && HorizontalScrollBarVisibility != ScrollBarVisibility.Never && MaxX > 0;
        var look = SkUiLook.Current;
        var margin = Math.Max(0, look.ScrollBarMargin);
        var hit = Math.Max(0, Math.Max(look.ScrollBarHitThickness, look.ScrollBarExpandedThickness));
        var corner = Math.Max(0, look.ScrollBarThickness) + margin;
        // A bar that always shows sits in its reserved gutter; a fading one in a hover strip over the scrollport's edge.
        var (gutterLeft, gutterRight, gutterBottom) = Gutters(rightToLeft);
        var port = Scrollport;
        var reservedVertical = gutterLeft + gutterRight > 0;
        var reservedHorizontal = gutterBottom > 0;
        if (showVertical || _verticalBar is not null)
        {
            var bar = ScrollBar(ScrollOrientation.Vertical);
            bar.ApplyVisibility(VerticalScrollBarVisibility);
            bar.SetIsVisible(showVertical);
            if (showVertical)
            {
                var strip = reservedVertical ? gutterLeft + gutterRight : Math.Min(port.Width, hit);
                var x = reservedVertical ? (rightToLeft ? 0 : port.Right) : rightToLeft ? port.Left : port.Right - strip;
                var track = Math.Max(0, port.Height - 2 * margin - (showHorizontal && !reservedHorizontal ? corner : 0));
                bar.ArrangeOwned(new Rect(x, margin, strip, track), thumbAtStart: rightToLeft);
            }
        }
        if (showHorizontal || _horizontalBar is not null)
        {
            var bar = ScrollBar(ScrollOrientation.Horizontal);
            bar.ApplyVisibility(HorizontalScrollBarVisibility);
            bar.SetIsVisible(showHorizontal);
            if (showHorizontal)
            {
                var strip = reservedHorizontal ? gutterBottom : Math.Min(port.Height, hit);
                var y = reservedHorizontal ? port.Bottom : port.Bottom - strip;
                var corneredVertical = showVertical && !reservedVertical;
                var left = port.Left + margin + (corneredVertical && rightToLeft ? corner : 0);
                var track = Math.Max(0, port.Width - 2 * margin - (corneredVertical ? corner : 0));
                bar.ArrangeOwned(new Rect(left, y, track, strip), thumbAtStart: false);
            }
        }
        ForEachBar(static bar => bar.OnMetricsChanged());
    }

    /// <summary>The offset or overscroll changed: bars that fade show (and fade out again once nothing moves).</summary>
    private void OnScrolled()
    {
        var moving = _moving;
        ForEachBar(bar => bar.OnScrolled(moving));
    }

    private void FadeBars() => ForEachBar(static bar => bar.OnScrollEnded());

    /// <summary>The scroller's own bars, if created (tests and diagnostics).</summary>
    internal (SkUiCoreScrollBar? Vertical, SkUiCoreScrollBar? Horizontal) ScrollBars => (_verticalBar, _horizontalBar);

    #endregion

    private SkUiRenderAnimation StartTween(double x, double y, TimeSpan duration, TaskCompletionSource? completion)
    {
        SkUiRenderTween? tween = null;
        tween = new SkUiRenderTween(
            [SkUiRenderProperty.ChildrenOffsetX, SkUiRenderProperty.ChildrenOffsetY],
            [(float)Math.Clamp(x, 0, MaxX), (float)Math.Clamp(y, 0, MaxY)],
            duration, Easing.CubicOut)
        {
            Report = (reportedX, reportedY) => { if (ReferenceEquals(_motion, tween)) ApplyRenderScroll(reportedX, reportedY, 0, 0); },
            Finished = (animation, completed) =>
            {
                owner.RenderState.ActiveAnimations?.Remove(animation);
                if (((SkUiRenderTween)animation).LastValues is { } values && ReferenceEquals(_motion, animation))
                    ApplyRenderScroll(values[0], values[1], 0, 0);
                if (ReferenceEquals(_motion, animation))
                    _motion = null;
                UpdateMoving();
                if (completion is not null)
                {
                    if (ReferenceEquals(_scrollCompletion, completion)) _scrollCompletion = null;
                    if (completed) completion.TrySetResult(); else completion.TrySetCanceled();
                }
            }
        };
        _motion = tween;
        UpdateMoving();
        SkUiRenderInvalidation.Enqueue(owner, tween);
        return tween;
    }

    /// <summary>
    /// Applies an offset and overscroll produced by the render thread: updates state and listeners without re-committing
    /// them, and continues a later drag from the overscroll shown.
    /// </summary>
    private void ApplyRenderScroll(float x, float y, float overscrollX, float overscrollY)
    {
        var props = SkUiRenderProps.Default;
        SkUiOverscroll.Apply(ref props, x, y, overscrollX, overscrollY, EffectiveOverscroll, (float)Viewport.Width, (float)Viewport.Height);
        var state = owner.RenderState;
        state.Acknowledge(SkUiRenderProperty.ChildrenOffsetX, props.ChildrenOffsetX);
        state.Acknowledge(SkUiRenderProperty.ChildrenOffsetY, props.ChildrenOffsetY);
        state.Acknowledge(SkUiRenderProperty.ChildrenScaleX, props.ChildrenScaleX);
        state.Acknowledge(SkUiRenderProperty.ChildrenScaleY, props.ChildrenScaleY);
        _pullX = SkUiOverscroll.InverseRubberBand(overscrollX, Viewport.Width);
        _pullY = SkUiOverscroll.InverseRubberBand(overscrollY, Viewport.Height);
        var overscrolled = overscrollX != OverscrollX || overscrollY != OverscrollY;
        OverscrollX = overscrollX;
        OverscrollY = overscrollY;
        UpdateState(x, y);
        if (overscrolled)
        {
            OnScrolled();
            OverscrollChanged?.Invoke();
        }
    }

    private bool UpdateState(double x, double y, bool showScrollBars = true)
    {
        if (!Horizontal) x = 0;
        if (!Vertical) y = 0;
        if (x == X && y == Y) return false;
        X = x;
        Y = y;
        if (showScrollBars)
            OnScrolled();
        offsetChanged();
        return true;
    }

    /// <summary>Throws with the name of the first non-finite offset so call sites can see which argument failed.</summary>
    private static void EnsureFinite(double horizontalOffset, double verticalOffset)
    {
        if (!double.IsFinite(horizontalOffset))
            throw new ArgumentOutOfRangeException(nameof(horizontalOffset));
        if (!double.IsFinite(verticalOffset))
            throw new ArgumentOutOfRangeException(nameof(verticalOffset));
    }

    private sealed class MotionHandle(SkUiScrollController owner, SkUiRenderAnimation motion) : IDisposable
    {
        public void Dispose()
        {
            if (ReferenceEquals(owner._motion, motion))
                owner.StopMotion();
            else
                motion.Cancel();
        }
    }
}

/// <summary>
/// Drag-to-scroll for one scroller. It claims when the pointer moves past the touch slop along an enabled axis in a
/// direction this scroller can still move (so an inner horizontal scroller inside a vertical one gets horizontal
/// drags, and an inner scroller at its edge lets the outer one take over). While dragging, the part of the movement
/// the scroller cannot absorb is passed to outer scrollers on the same axis, and what none of them can use pulls this
/// scroller past its edge (overscroll; a drag back takes the pull back first). The release springs back from
/// overscroll, or flings the innermost scroller that can move in its direction. A press during a fling stops it and
/// claims (the content is not tapped).
/// </summary>
/// <remarks>
/// At an edge, a scroller with overscroll also claims a drag outwards, unless an outer drawn scroller or a native
/// ancestor (a MAUI ScrollView around the surface) can still scroll that way: those keep the drag, so chaining is
/// unchanged by overscroll.
/// </remarks>
internal sealed class SkUiScrollGestureRecognizer(SkUiScrollController scroller) : SkUiGestureRecognizer
{
    private long? _pointer;
    private Point _start;
    private Point _last;
    private bool _dragging;
    private SkUiVelocityTracker _velocity;
    /// <summary>Outer scrollers that received chained drag movement (they are "moving" until release).</summary>
    private List<SkUiScrollController>? _chained;

    protected internal override bool IsExclusive => true;

    protected internal override bool OnPointerPressed(SkUiPointer pointer)
    {
        if (_pointer is not null || scroller.Orientation == ScrollOrientation.Neither || Owner is not { } owner)
        {
            if (_pointer is not null && SkUiDiagnostics.TraceOn)
                SkUiDiagnostics.Write($"scroll gesture busy: still tracks pointer {_pointer} (dragging={_dragging}), ignores press {pointer.Id}");
            return false;
        }
        var local = pointer.GetPosition(owner);
        if (local.X < 0 || local.Y < 0 || local.X > scroller.Bounds.Width || local.Y > scroller.Bounds.Height)
            return false;
        _pointer = pointer.Id;
        _start = _last = pointer.Position;
        _dragging = false;
        _velocity.Reset(pointer.Timestamp, pointer.Position);
        if (scroller.IsMotionRunning)
        {
            scroller.StopMotion();
            Claim();
            _dragging = _pointer is not null;
            scroller.Dragging = _dragging;
        }
        return true;
    }

    protected internal override void OnPointerMoved(SkUiPointer pointer)
    {
        if (_pointer != pointer.Id)
            return;
        _velocity.Add(pointer.Timestamp, pointer.Position);
        if (!_dragging)
        {
            var dx = scroller.Horizontal ? pointer.Position.X - _start.X : 0;
            var dy = scroller.Vertical ? pointer.Position.Y - _start.Y : 0;
            var axis = scroller.Orientation switch
            {
                ScrollOrientation.Horizontal => SkUiPanAxis.Horizontal,
                ScrollOrientation.Vertical => SkUiPanAxis.Vertical,
                _ => SkUiPanAxis.Both
            };
            // Compare against the full movement so a mostly-vertical drag is not taken by a horizontal scroller.
            if (!ExceedsSlop(axis == SkUiPanAxis.Vertical ? pointer.TotalX : dx, axis == SkUiPanAxis.Horizontal ? pointer.TotalY : dy, axis)
                || !CanTake(-dx, -dy, pointer))
            {
                _last = pointer.Position;
                return;
            }
            Claim();
            if (_pointer != pointer.Id)
                return;
            _dragging = true;
            scroller.Dragging = true;
            Apply(_start.X - pointer.Position.X, _start.Y - pointer.Position.Y);
        }
        else
        {
            Apply(_last.X - pointer.Position.X, _last.Y - pointer.Position.Y);
        }
        _last = pointer.Position;
    }

    protected internal override void OnPointerReleased(SkUiPointer pointer)
    {
        if (_pointer != pointer.Id)
            return;
        _velocity.Add(pointer.Timestamp, pointer.Position);
        var dragging = _dragging;
        _pointer = null;
        _dragging = false;
        if (!dragging)
        {
            Resign();
            return;
        }
        // Start the spring-back or fling (if any) before clearing the drag, so a continuous motion stays "moving".
        try
        {
            if (!scroller.SettleOverscroll())
                Fling();
        }
        finally
        {
            scroller.Dragging = false;
            EndChained();
        }
    }

    private void EndChained()
    {
        if (_chained is null)
            return;
        foreach (var outer in _chained)
            outer.Dragging = false;
        _chained.Clear();
    }

    /// <summary>
    /// Flings the innermost scroller that can move in the release direction; the dragged scroller and the outer ones the
    /// drag moved settle on their snap points (if any) unless they took the fling.
    /// </summary>
    private void Fling()
    {
        var finger = _velocity.Velocity;
        var velocity = new Point(-finger.X, -finger.Y);
        SkUiScrollController? flung = null;
        if (scroller.CanScroll(velocity.X, velocity.Y))
        {
            if (scroller.StartFling(velocity))
                flung = scroller;
        }
        else
        {
            foreach (var outer in OuterScrollers())
                if (outer.CanScroll(velocity.X, velocity.Y))
                {
                    if (outer.StartFling(velocity))
                        flung = outer;
                    break;
                }
        }
        if (!ReferenceEquals(flung, scroller))
            scroller.Snap(Point.Zero);
        if (_chained is not null)
            foreach (var outer in _chained)
                if (!ReferenceEquals(outer, flung))
                    outer.Snap(Point.Zero);
    }

    protected internal override void OnRejected(long pointerId)
    {
        if (_pointer == pointerId)
        {
            _pointer = null;
            if (_dragging)
            {
                scroller.SettleOverscroll();
                scroller.Dragging = false;
            }
            _dragging = false;
            EndChained();
        }
    }

    /// <summary>
    /// Whether this scroller takes a drag in this offset direction: it can scroll that way, or it is at the edge with
    /// overscroll and nothing around it (an outer drawn scroller, a native ancestor) can scroll that way instead.
    /// </summary>
    private bool CanTake(double dx, double dy, SkUiPointer pointer)
    {
        if (scroller.CanScroll(dx, dy))
            return true;
        if (!scroller.CanOverscroll(dx, dy))
            return false;
        foreach (var outer in OuterScrollers())
            if (outer.CanScroll(outer.Horizontal ? dx : 0, outer.Vertical ? dy : 0))
                return false;
        return pointer.Router?.NativeAncestorCanScroll?.Invoke(dx, dy) != true;
    }

    private void Apply(double dx, double dy)
    {
        // Axes this scroller does not drive are masked out, so a vertical drag never scrolls an outer horizontal scroller.
        var delta = scroller.Unpull(scroller.Horizontal ? dx : 0, scroller.Vertical ? dy : 0);
        var remainder = scroller.ScrollBy(delta.X, delta.Y);
        foreach (var outer in OuterScrollers())
        {
            if (remainder.X == 0 && remainder.Y == 0)
                return;
            var before = remainder;
            remainder = outer.ScrollBy(remainder.X, remainder.Y);
            if (remainder != before && !(_chained ??= []).Contains(outer))
            {
                _chained.Add(outer);
                outer.Dragging = true;
            }
        }
        if (remainder.X != 0 || remainder.Y != 0)
            scroller.Pull(remainder.X, remainder.Y);
    }

    private IEnumerable<SkUiScrollController> OuterScrollers()
    {
        for (var node = Node?.RenderParent; node is not null; node = node.RenderParent)
            if (node is ISkUiScrollHost { IsHitTestVisible: true, IsInputEnabled: true } host)
                yield return host.Scroller;
    }
}
