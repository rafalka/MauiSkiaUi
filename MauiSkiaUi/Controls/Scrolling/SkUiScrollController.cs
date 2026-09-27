using MauiSkiaUi.Rendering;

namespace MauiSkiaUi;

/// <summary>A drawn scroller (<see cref="SkUiScrollView"/>, <see cref="Core.SkUiCoreScrollView"/>) driven by the shared scroll gesture.</summary>
internal interface ISkUiScrollHost : ISkUiInputNode
{
    SkUiScrollController Scroller { get; }
}

/// <summary>
/// Scroll state and motion shared by the SkUi* and Core scroll views: clamped offsets applied as a composite-time
/// children translation, render-thread tweens / flings with offsets reported back, wheel input, and nested-scroll
/// chaining. The owner supplies invalidation and change notification.
/// </summary>
internal sealed class SkUiScrollController(ISkUiRenderable owner, Action<SkUiRenderDirty> invalidate, Action offsetChanged)
{
    private SkUiRenderAnimation? _motion;
    private TaskCompletionSource? _scrollCompletion;

    public ScrollOrientation Orientation { get; set; } = ScrollOrientation.Vertical;
    public Size Extent { get; set; }
    public Size Viewport { get; set; }
    public double X { get; private set; }
    public double Y { get; private set; }
    public bool Horizontal => Orientation is ScrollOrientation.Horizontal or ScrollOrientation.Both;
    public bool Vertical => Orientation is ScrollOrientation.Vertical or ScrollOrientation.Both;
    public double MaxX => Horizontal ? Math.Max(0, Extent.Width - Viewport.Width) : 0;
    public double MaxY => Vertical ? Math.Max(0, Extent.Height - Viewport.Height) : 0;
    public bool IsMotionRunning => _motion is not null;

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
            _dragging = value;
            UpdateMoving();
        }
    }

    private void UpdateMoving()
    {
        var moving = _dragging || _motion is not null;
        if (moving == _moving)
            return;
        _moving = moving;
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

    /// <summary>Re-clamps after a layout change (extent / viewport).</summary>
    public void Clamp() => SetOffset(X, Y);

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

    /// <summary>
    /// A wheel delta (positive towards the start). One delta without an axis: vertical when enabled, otherwise
    /// horizontal. Returns <c>false</c> when this scroller cannot move that way (an outer one may).
    /// </summary>
    public bool Wheel(double delta)
    {
        if (Orientation == ScrollOrientation.Neither || delta == 0)
            return false;
        var dx = Horizontal && !Vertical ? -delta : 0;
        var dy = Vertical ? -delta : 0;
        if (!CanScroll(dx, dy))
            return false;
        StopMotion();
        SetOffset(X + dx, Y + dy);
        return true;
    }

    public IDisposable AnimateTo(double x, double y, TimeSpan duration)
    {
        StopMotion();
        EnsureFinite(x, y);
        var motion = StartTween(x, y, duration, completion: null);
        return new MotionHandle(this, motion);
    }

    public Task ScrollToAsync(double x, double y, bool animated)
    {
        StopMotion();
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

    /// <summary>Starts a render-thread fling with an offset velocity (DIPs / s); returns <c>false</c> when too slow.</summary>
    public bool StartFling(Point velocity)
    {
        var limit = SkUiGestureSettings.FlingMaximumVelocity;
        var vx = Horizontal ? Math.Clamp(velocity.X, -limit, limit) : 0;
        var vy = Vertical ? Math.Clamp(velocity.Y, -limit, limit) : 0;
        if (Math.Max(Math.Abs(vx), Math.Abs(vy)) < SkUiGestureSettings.FlingMinimumVelocity)
            return false;
        StopMotion();
        SkUiRenderFling? fling = null;
        fling = new SkUiRenderFling((float)vx, (float)vy, (float)MaxX, (float)MaxY,
            (x, y) => { if (ReferenceEquals(_motion, fling)) ApplyRenderOffset(x, y); })
        {
            Finished = (animation, _) =>
            {
                owner.RenderState.ActiveAnimations?.Remove(animation);
                if (ReferenceEquals(_motion, animation))
                    _motion = null;
                UpdateMoving();
            }
        };
        _motion = fling;
        UpdateMoving();
        SkUiRenderInvalidation.Enqueue(owner, fling);
        return true;
    }

    /// <summary>Cancels the running render-thread motion; its last shown offset is reported back asynchronously.</summary>
    public void StopMotion()
    {
        var motion = _motion;
        _motion = null;
        motion?.Cancel();
        _scrollCompletion?.TrySetCanceled();
        _scrollCompletion = null;
        UpdateMoving();
    }

    /// <summary>Content replaced: stop and return to the origin.</summary>
    public void Reset()
    {
        StopMotion();
        Gesture.Cancel();
        X = Y = 0;
        Extent = Size.Zero;
    }

    private SkUiRenderAnimation StartTween(double x, double y, TimeSpan duration, TaskCompletionSource? completion)
    {
        SkUiRenderTween? tween = null;
        tween = new SkUiRenderTween(
            [SkUiRenderProperty.ChildrenOffsetX, SkUiRenderProperty.ChildrenOffsetY],
            [(float)Math.Clamp(x, 0, MaxX), (float)Math.Clamp(y, 0, MaxY)],
            duration, Easing.CubicOut)
        {
            Report = (reportedX, reportedY) => { if (ReferenceEquals(_motion, tween)) ApplyRenderOffset(reportedX, reportedY); },
            Finished = (animation, completed) =>
            {
                owner.RenderState.ActiveAnimations?.Remove(animation);
                if (((SkUiRenderTween)animation).LastValues is { } values && ReferenceEquals(_motion, animation))
                    ApplyRenderOffset(values[0], values[1]);
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

    /// <summary>Applies an offset produced by the render thread: updates state and listeners without re-committing it.</summary>
    private void ApplyRenderOffset(float x, float y)
    {
        owner.RenderState.Acknowledge(SkUiRenderProperty.ChildrenOffsetX, x);
        owner.RenderState.Acknowledge(SkUiRenderProperty.ChildrenOffsetY, y);
        UpdateState(x, y);
    }

    private bool UpdateState(double x, double y)
    {
        if (!Horizontal) x = 0;
        if (!Vertical) y = 0;
        if (x == X && y == Y) return false;
        X = x;
        Y = y;
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
/// the scroller cannot absorb is passed to outer scrollers on the same axis; the release fling goes to the innermost
/// scroller that can move in its direction. A press during a fling stops it and claims (the content is not tapped).
/// </summary>
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
            return false;
        var local = pointer.GetPosition(owner);
        if (local.X < 0 || local.Y < 0 || local.X > scroller.Viewport.Width || local.Y > scroller.Viewport.Height)
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
                || !scroller.CanScroll(-dx, -dy))
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
        // Start the fling (if any) before clearing the drag, so a continuous motion stays "moving".
        try
        {
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

    private void Fling()
    {
        var finger = _velocity.Velocity;
        var velocity = new Point(-finger.X, -finger.Y);
        if (scroller.CanScroll(velocity.X, velocity.Y))
        {
            scroller.StartFling(velocity);
            return;
        }
        foreach (var outer in OuterScrollers())
            if (outer.CanScroll(velocity.X, velocity.Y))
            {
                outer.StartFling(velocity);
                return;
            }
    }

    protected internal override void OnRejected(long pointerId)
    {
        if (_pointer == pointerId)
        {
            _pointer = null;
            if (_dragging)
                scroller.Dragging = false;
            _dragging = false;
            EndChained();
        }
    }

    private void Apply(double dx, double dy)
    {
        // Axes this scroller does not drive are masked out, so a vertical drag never scrolls an outer horizontal scroller.
        var remainder = scroller.ScrollBy(scroller.Horizontal ? dx : 0, scroller.Vertical ? dy : 0);
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
    }

    private IEnumerable<SkUiScrollController> OuterScrollers()
    {
        for (var node = Node?.RenderParent; node is not null; node = node.RenderParent)
            if (node is ISkUiScrollHost { IsHitTestVisible: true, IsInputEnabled: true } host)
                yield return host.Scroller;
    }
}
