using System.Diagnostics;
using MauiSkiaUi.Rendering;

namespace MauiSkiaUi;

/// <summary>
/// A single-surface scroller with clamped offsets, tap cancellation, wheel input, and inertial fling.
/// The scroll offset is a composite-time children translation: scrolling never re-records content, and
/// fling / animated scrolls run on the render thread, so they stay smooth while the UI thread is busy.
/// The render thread reports offsets back each frame to keep <see cref="ScrollX"/> / <see cref="ScrollY"/>,
/// hit-testing, <see cref="Scrolled"/> and native overlays in sync.
/// Child <see cref="SkUiTouchAction.Pressed"/> is withheld until a tap is confirmed (movement under 10 DIPs).
/// </summary>
public class SkUiScrollView : SkUiContentView
{
    private ScrollOrientation _orientation = ScrollOrientation.Vertical;
    private Size _extent;
    private Size _viewport;
    private long? _pointer;
    private Point _startPosition;
    private Point _lastPosition;
    private TimeSpan _lastTime;
    private Point _velocity;
    private bool _dragging;
    /// <summary>True after <see cref="SkUiTouchAction.Pressed"/> until pan takeover or release.</summary>
    private bool _contentPressPending;
    private SkUiRenderAnimation? _motion;
    private TaskCompletionSource? _scrollCompletion;
    /// <summary>Registered <see cref="SkUiMauiContentView"/> descendants that need offset sync (avoids O(tree) walks).</summary>
    private List<SkUiMauiContentView>? _overlayDescendants;

    /// <summary>True while a pointer is captured, a pan is active, or a fling/programmatic motion is running.</summary>
    private bool IsScrollInteractionActive => _pointer is not null || _dragging || _motion is not null;

    /// <summary>Creates a scroller whose motion stops when its surface unloads.</summary>
    public SkUiScrollView() => Unloaded += (_, _) => CancelInteraction();

    /// <summary>Bindable enabled scroll axes.</summary>
    public static readonly BindableProperty OrientationProperty = BindableProperty.Create(nameof(Orientation), typeof(ScrollOrientation), typeof(SkUiScrollView), ScrollOrientation.Vertical,
        propertyChanged: (view, _, value) => ((SkUiScrollView)view).SetOrientation((ScrollOrientation)value));
    /// <summary>Enabled axes; Neither disables scrolling.</summary>
    public ScrollOrientation Orientation { get => _orientation; set => SetValue(OrientationProperty, value); }
    /// <summary>Current horizontal offset in DIPs (updated from the render thread during fling).</summary>
    public double ScrollX { get; private set; }
    /// <summary>Current vertical offset in DIPs (updated from the render thread during fling).</summary>
    public double ScrollY { get; private set; }
    /// <summary>Measured scrollable content extent, including padding.</summary>
    public Size ContentSize => _extent;
    /// <summary>Raised after a clamped offset changes.</summary>
    public event EventHandler<ScrolledEventArgs>? Scrolled;

    /// <summary>True while a render-thread fling or animated scroll is running.</summary>
    internal bool IsMotionRunning => _motion is not null;

    /// <summary>Sets orientation without bindable write-back.</summary>
    public SkUiScrollView SetOrientation(ScrollOrientation value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        CancelInteraction();
        _orientation = value;
        InvalidateMeasureOverride();
        return this;
    }
    private bool Horizontal => _orientation is ScrollOrientation.Horizontal or ScrollOrientation.Both;
    private bool Vertical => _orientation is ScrollOrientation.Vertical or ScrollOrientation.Both;
    private double MaxX => Horizontal ? Math.Max(0, _extent.Width - _viewport.Width) : 0;
    private double MaxY => Vertical ? Math.Max(0, _extent.Height - _viewport.Height) : 0;

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        _extent = base.MeasureContent(Horizontal ? double.PositiveInfinity : widthConstraint, Vertical ? double.PositiveInfinity : heightConstraint);
        return new Size(Math.Min(widthConstraint, _extent.Width), Math.Min(heightConstraint, _extent.Height));
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        _viewport = size;
        // Content stays arranged at its layout origin; the offset is a composite-time children translation.
        Content?.Arrange(new Rect(
            Padding.Left, Padding.Top,
            Math.Max(0, Math.Max(_extent.Width, _viewport.Width) - Padding.HorizontalThickness),
            Math.Max(0, Math.Max(_extent.Height, _viewport.Height) - Padding.VerticalThickness)));
        InvalidateRender(SkUiRenderDirty.Props);
        SetOffset(ScrollX, ScrollY);
    }

    /// <inheritdoc />
    internal override void OnGetRenderProps(ref SkUiRenderProps props)
    {
        props.ChildrenOffsetX = (float)ScrollX;
        props.ChildrenOffsetY = (float)ScrollY;
        props.ChildrenClipRect = new SkiaSharp.SKRect(0, 0, (float)_viewport.Width, (float)_viewport.Height);
    }

    /// <summary>Clamps and sets an offset without remeasuring or rearranging content.</summary>
    public SkUiScrollView ScrollTo(double horizontalOffset, double verticalOffset)
    {
        StopMotion();
        SetOffset(horizontalOffset, verticalOffset);
        return this;
    }

    /// <summary>Starts a render-thread scroll animation; disposing the returned handle cancels it.</summary>
    public IDisposable AnimateScrollTo(double horizontalOffset, double verticalOffset, TimeSpan duration)
    {
        StopMotion();
        EnsureFiniteOffsets(horizontalOffset, verticalOffset);
        var motion = StartTween(horizontalOffset, verticalOffset, duration, completion: null);
        return new MotionHandle(this, motion);
    }

    /// <summary>Scrolls immediately or animates over 300 ms on the render thread. A superseding gesture, scroll, or unload cancels the task.</summary>
    public Task ScrollToAsync(double horizontalOffset, double verticalOffset, bool animated = true)
    {
        StopMotion();
        if (!animated) { SetOffset(horizontalOffset, verticalOffset); return Task.CompletedTask; }
        EnsureFiniteOffsets(horizontalOffset, verticalOffset);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _scrollCompletion = completion;
        StartTween(horizontalOffset, verticalOffset, TimeSpan.FromMilliseconds(300), completion);
        return completion.Task;
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
                RenderState.ActiveAnimations?.Remove(animation);
                if (((SkUiRenderTween)animation).LastValues is { } values && ReferenceEquals(_motion, animation))
                    ApplyRenderOffset(values[0], values[1]);
                if (ReferenceEquals(_motion, animation))
                    _motion = null;
                if (completion is not null)
                {
                    if (ReferenceEquals(_scrollCompletion, completion)) _scrollCompletion = null;
                    if (completed) completion.TrySetResult(); else completion.TrySetCanceled();
                }
            }
        };
        _motion = tween;
        SkUiRenderInvalidation.Enqueue(this, tween);
        return tween;
    }

    /// <summary>Applies an offset produced by the render thread: updates state and listeners without re-committing it.</summary>
    private void ApplyRenderOffset(float x, float y)
    {
        RenderState.Acknowledge(SkUiRenderProperty.ChildrenOffsetX, x);
        RenderState.Acknowledge(SkUiRenderProperty.ChildrenOffsetY, y);
        UpdateOffsetState(x, y);
    }

    private void SetOffset(double horizontalOffset, double verticalOffset)
    {
        EnsureFiniteOffsets(horizontalOffset, verticalOffset);
        if (UpdateOffsetState(Math.Clamp(horizontalOffset, 0, MaxX), Math.Clamp(verticalOffset, 0, MaxY)))
            InvalidateRender(SkUiRenderDirty.Props);
    }

    private bool UpdateOffsetState(double nextX, double nextY)
    {
        if (!Horizontal) nextX = 0;
        if (!Vertical) nextY = 0;
        if (nextX == ScrollX && nextY == ScrollY) return false;
        ScrollX = nextX;
        ScrollY = nextY;
        OnPropertyChanged(nameof(ScrollX));
        OnPropertyChanged(nameof(ScrollY));
        SyncRegisteredOverlays();
        Scrolled?.Invoke(this, new ScrolledEventArgs(ScrollX, ScrollY));
        return true;
    }

    /// <summary>Registers a hosted overlay so offset updates can sync it without walking the full tree.</summary>
    internal void RegisterOverlayDescendant(SkUiMauiContentView overlay)
    {
        _overlayDescendants ??= [];
        if (!_overlayDescendants.Contains(overlay))
            _overlayDescendants.Add(overlay);
    }

    /// <summary>Removes a previously registered overlay descendant.</summary>
    internal void UnregisterOverlayDescendant(SkUiMauiContentView overlay) =>
        _overlayDescendants?.Remove(overlay);

    /// <summary>Pushes updated root-relative bounds to registered <see cref="SkUiMauiContentView"/> descendants.</summary>
    private void SyncRegisteredOverlays()
    {
        if (_overlayDescendants is null || _overlayDescendants.Count == 0)
            return;
        foreach (var overlay in _overlayDescendants)
            overlay.NotifyAncestorScrollOffsetChanged();
    }

    /// <summary>Throws with the name of the first non-finite offset so call sites can see which argument failed.</summary>
    private static void EnsureFiniteOffsets(double horizontalOffset, double verticalOffset)
    {
        if (!double.IsFinite(horizontalOffset))
            throw new ArgumentOutOfRangeException(nameof(horizontalOffset));
        if (!double.IsFinite(verticalOffset))
            throw new ArgumentOutOfRangeException(nameof(verticalOffset));
    }

    /// <summary>Cancels the running render-thread motion; its last shown offset is reported back asynchronously.</summary>
    private void StopMotion()
    {
        var motion = _motion;
        _motion = null;
        // The compositor is producing frames while the motion runs, so it observes the flag on its next frame.
        motion?.Cancel();
        _scrollCompletion?.TrySetCanceled();
        _scrollCompletion = null;
    }

    private void CancelInteraction()
    {
        StopMotion();
        if (!_contentPressPending)
            CancelContentTouch();
        _pointer = null;
        _dragging = false;
        _contentPressPending = false;
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
        StopMotion();
        _pointer = null;
        _dragging = false;
        _contentPressPending = false;
        ScrollX = ScrollY = 0;
        _extent = Size.Zero;
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

    private sealed class MotionHandle(SkUiScrollView owner, SkUiRenderAnimation motion) : IDisposable
    {
        public void Dispose()
        {
            if (ReferenceEquals(owner._motion, motion))
                owner.StopMotion();
            else
                motion.Cancel();
        }
    }

    /// <summary>Maps a viewport-local point into content-local space including scroll offset.</summary>
    private SkUiTouchEvent MapToContent(SkUiTouchEvent touch) =>
        touch with { Position = new Point(touch.Position.X + ScrollX, touch.Position.Y + ScrollY) };

    /// <inheritdoc />
    public override bool Touch(SkUiTouchEvent touch)
    {
        if (!IsVisible || InputTransparent || !IsEnabled)
        {
            StopMotion();
            _pointer = null;
            _dragging = false;
            _contentPressPending = false;
            return base.Touch(touch);
        }
        if (_orientation == ScrollOrientation.Neither) return base.Touch(MapToContent(touch));
        if (touch.Action == SkUiTouchAction.Wheel)
        {
            // SkUiTouchEvent carries a single WheelDelta with no axis indicator, so a plain wheel always
            // scrolls the vertical axis when it is enabled (Vertical or Both), and only scrolls horizontally
            // when the horizontal axis is the sole enabled one. Both does not distinguish a horizontal-wheel
            // gesture (e.g. shift+wheel) from a vertical one; that requires a richer wheel event and is deferred.
            ScrollTo(ScrollX - (Horizontal && !Vertical ? touch.WheelDelta : 0), ScrollY - (Vertical ? touch.WheelDelta : 0));
            return true;
        }
        var now = touch.Timestamp ?? TimeSpan.FromSeconds((double)Stopwatch.GetTimestamp() / Stopwatch.Frequency);
        if (touch.Action == SkUiTouchAction.Pressed)
        {
            if (_pointer is not null || !new Rect(Point.Zero, _viewport).Contains(touch.Position)) return false;
            StopMotion();
            _pointer = touch.Id;
            _startPosition = _lastPosition = touch.Position;
            _lastTime = now;
            _velocity = Point.Zero;
            _dragging = false;
            // Defer child press until release (tap) or abandon it on pan — avoids content-picture rebuilds while scrolling.
            _contentPressPending = true;
            return true;
        }
        if (_pointer != touch.Id) return false;
        if (touch.Action == SkUiTouchAction.Moved)
        {
            var distanceX = Horizontal ? touch.Position.X - _startPosition.X : 0;
            var distanceY = Vertical ? touch.Position.Y - _startPosition.Y : 0;
            if (!_dragging && distanceX * distanceX + distanceY * distanceY > 100)
            {
                _dragging = true;
                _contentPressPending = false;
            }
            if (_dragging)
            {
                var deltaX = Horizontal ? _lastPosition.X - touch.Position.X : 0;
                var deltaY = Vertical ? _lastPosition.Y - touch.Position.Y : 0;
                var seconds = (now - _lastTime).TotalSeconds;
                if (seconds > 0) _velocity = new Point(Math.Clamp(deltaX / seconds, -3000, 3000), Math.Clamp(deltaY / seconds, -3000, 3000));
                SetOffset(ScrollX + deltaX, ScrollY + deltaY);
            }
            _lastPosition = touch.Position;
            _lastTime = now;
            return true;
        }
        if (touch.Action is SkUiTouchAction.Released or SkUiTouchAction.Cancelled)
        {
            var wasDragging = _dragging;
            var pendingPress = _contentPressPending;
            _pointer = null;
            _dragging = false;
            _contentPressPending = false;
            if (!wasDragging && pendingPress && touch.Action == SkUiTouchAction.Released)
            {
                // Confirmed tap: suppress only synthetic pressed chrome; release/tapped callbacks may
                // invalidate normally so click handlers that mutate content dirty the picture.
                base.Touch(MapToContent(touch with { Action = SkUiTouchAction.Pressed, Position = _startPosition }));
                base.Touch(MapToContent(touch));
            }
            else if (wasDragging && touch.Action == SkUiTouchAction.Released && (now - _lastTime).TotalMilliseconds <= 100)
                StartFling(_velocity);
            return true;
        }
        return true;
    }

    private void StartFling(Point speed)
    {
        var magnitude = Math.Max(Math.Abs(speed.X), Math.Abs(speed.Y));
        if (magnitude < 40) return;
        SkUiRenderFling? fling = null;
        fling = new SkUiRenderFling((float)speed.X, (float)speed.Y, (float)MaxX, (float)MaxY,
            (x, y) => { if (ReferenceEquals(_motion, fling)) ApplyRenderOffset(x, y); })
        {
            Finished = (animation, _) =>
            {
                RenderState.ActiveAnimations?.Remove(animation);
                if (ReferenceEquals(_motion, animation))
                    _motion = null;
            }
        };
        _motion = fling;
        SkUiRenderInvalidation.Enqueue(this, fling);
    }
}
