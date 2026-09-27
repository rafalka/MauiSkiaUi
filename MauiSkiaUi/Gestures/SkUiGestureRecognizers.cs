namespace MauiSkiaUi;

/// <summary>
/// Single and double taps. The press state (<see cref="PressedChanged"/>) shows at once when uncontested, after
/// <see cref="SkUiGestureSettings.PressDelay"/> when an ancestor (e.g. a scroll view) competes, and never when that
/// ancestor wins first. When double taps are handled, single taps wait <see cref="SkUiGestureSettings.DoubleTapTimeout"/>.
/// </summary>
public class SkUiTapGestureRecognizer : SkUiGestureRecognizer
{
    private long? _pointer;
    private Point _start;
    private bool _won;
    private bool _released;
    private SkUiPointer _release;
    private bool _pressed;
    private IDisposable? _pressTimer;
    private IDisposable? _singleTimer;
    private SkUiTappedEventArgs? _pendingSingle;
    private TimeSpan _pendingTime;
    private Point _pendingPosition;

    /// <summary>Raised for a single tap (after the double-tap timeout when <see cref="DoubleTapped"/> is handled).</summary>
    public event EventHandler<SkUiTappedEventArgs>? Tapped;

    /// <summary>Raised for a double tap.</summary>
    public event EventHandler<SkUiTappedEventArgs>? DoubleTapped;

    /// <summary>Raised when the press state changes.</summary>
    public event EventHandler<bool>? PressedChanged;

    /// <summary>Whether the tracked pointer currently shows as pressed.</summary>
    public bool IsPressed => _pressed;

    internal Func<bool>? CanTap { get; init; }
    internal Func<bool>? WantsDoubleTap { get; init; }
    internal Action<SkUiTappedEventArgs>? TapHandler { get; init; }
    internal Action<SkUiTappedEventArgs>? DoubleTapHandler { get; init; }
    internal Action<bool>? PressedHandler { get; init; }

    private bool HandlesDoubleTap => DoubleTapped is not null || WantsDoubleTap?.Invoke() == true;

    /// <inheritdoc />
    protected internal override bool OnPointerPressed(SkUiPointer pointer)
    {
        if (_pointer is not null || CanTap?.Invoke() == false)
            return false;
        _pointer = pointer.Id;
        _start = pointer.Position;
        _won = _released = false;
        var id = pointer.Id;
        _pressTimer = SkUiGestureSettings.Schedule(SkUiGestureSettings.PressDelay, () =>
        {
            if (_pointer == id)
                SetPressed(true);
        });
        return true;
    }

    /// <inheritdoc />
    protected internal override void OnPointerMoved(SkUiPointer pointer)
    {
        if (_pointer != pointer.Id)
            return;
        var dx = pointer.Position.X - _start.X;
        var dy = pointer.Position.Y - _start.Y;
        if (dx * dx + dy * dy > SkUiGestureSettings.TouchSlop * SkUiGestureSettings.TouchSlop)
            Resign();
    }

    /// <inheritdoc />
    protected internal override void OnPointerReleased(SkUiPointer pointer)
    {
        if (_pointer != pointer.Id)
            return;
        _released = true;
        _release = pointer;
        if (_won)
            Complete();
    }

    /// <inheritdoc />
    protected internal override void OnAccepted(long pointerId)
    {
        if (_pointer != pointerId)
            return;
        _won = true;
        SetPressed(true);
        if (_released)
            Complete();
    }

    /// <inheritdoc />
    protected internal override void OnRejected(long pointerId)
    {
        if (_pointer != pointerId)
            return;
        Reset();
    }

    private void Complete()
    {
        var release = _release;
        var local = Owner is { } owner ? release.GetPosition(owner) : release.Position;
        Reset();
        if (!IsInsideOwner(local))
            return;
        var args = new SkUiTappedEventArgs(local);
        if (!HandlesDoubleTap)
        {
            RaiseTap(args);
            return;
        }
        var dx = release.Position.X - _pendingPosition.X;
        var dy = release.Position.Y - _pendingPosition.Y;
        if (_pendingSingle is not null && release.Timestamp - _pendingTime <= SkUiGestureSettings.DoubleTapTimeout
            && dx * dx + dy * dy <= SkUiGestureSettings.DoubleTapSlop * SkUiGestureSettings.DoubleTapSlop)
        {
            _singleTimer?.Dispose();
            _singleTimer = null;
            _pendingSingle = null;
            var doubleArgs = new SkUiTappedEventArgs(local, 2);
            DoubleTapped?.Invoke(Owner, doubleArgs);
            DoubleTapHandler?.Invoke(doubleArgs);
            return;
        }
        _pendingSingle = args;
        _pendingTime = release.Timestamp;
        _pendingPosition = release.Position;
        _singleTimer?.Dispose();
        _singleTimer = SkUiGestureSettings.Schedule(SkUiGestureSettings.DoubleTapTimeout, () =>
        {
            if (_pendingSingle is { } pending)
            {
                _pendingSingle = null;
                RaiseTap(pending);
            }
        });
        if (_singleTimer is null)
            RaiseTap(args); // no timer source: single taps cannot wait for a possible second tap
    }

    private void RaiseTap(SkUiTappedEventArgs args)
    {
        Tapped?.Invoke(Owner, args);
        TapHandler?.Invoke(args);
    }

    private void Reset()
    {
        _pressTimer?.Dispose();
        _pressTimer = null;
        _pointer = null;
        _won = _released = false;
        SetPressed(false);
    }

    private void SetPressed(bool value)
    {
        if (_pressed == value)
            return;
        _pressed = value;
        PressedHandler?.Invoke(value);
        PressedChanged?.Invoke(Owner, value);
    }
}

/// <summary>Long press: the pointer stays within the touch slop for <see cref="Duration"/>; then it claims the gesture.</summary>
public class SkUiLongPressGestureRecognizer : SkUiGestureRecognizer
{
    private long? _pointer;
    private Point _start;
    private SkUiPointer _last;
    private bool _fired;
    private IDisposable? _timer;

    /// <summary>Raised when the hold duration elapses.</summary>
    public event EventHandler<SkUiLongPressedEventArgs>? LongPressed;

    /// <summary>Hold duration; <see cref="SkUiGestureSettings.LongPressDuration"/> when unset.</summary>
    public TimeSpan? Duration { get; set; }

    internal Action<SkUiLongPressedEventArgs>? Handler { get; init; }

    /// <inheritdoc />
    protected internal override bool OnPointerPressed(SkUiPointer pointer)
    {
        if (_pointer is not null)
            return false;
        var id = pointer.Id;
        _timer = SkUiGestureSettings.Schedule(Duration ?? SkUiGestureSettings.LongPressDuration, () => Fire(id));
        if (_timer is null)
            return false; // no timer source
        _pointer = id;
        _start = pointer.Position;
        _last = pointer;
        _fired = false;
        return true;
    }

    /// <inheritdoc />
    protected internal override void OnPointerMoved(SkUiPointer pointer)
    {
        if (_pointer != pointer.Id)
            return;
        _last = pointer;
        var dx = pointer.Position.X - _start.X;
        var dy = pointer.Position.Y - _start.Y;
        if (!_fired && dx * dx + dy * dy > SkUiGestureSettings.TouchSlop * SkUiGestureSettings.TouchSlop)
            Resign();
    }

    /// <inheritdoc />
    protected internal override void OnPointerReleased(SkUiPointer pointer)
    {
        if (_pointer != pointer.Id)
            return;
        if (!_fired)
            Resign();
        Reset();
    }

    /// <inheritdoc />
    protected internal override void OnRejected(long pointerId)
    {
        if (_pointer == pointerId)
            Reset();
    }

    private void Fire(long pointerId)
    {
        if (_pointer != pointerId || _fired)
            return;
        Claim();
        if (_pointer != pointerId)
            return; // lost the arena
        _fired = true;
        var args = new SkUiLongPressedEventArgs(Owner is { } owner ? _last.GetPosition(owner) : _last.Position);
        LongPressed?.Invoke(Owner, args);
        Handler?.Invoke(args);
    }

    private void Reset()
    {
        _timer?.Dispose();
        _timer = null;
        _pointer = null;
    }
}

/// <summary>Pan (drag) along <see cref="Axis"/>; claims once the movement passes the touch slop in that axis.</summary>
public class SkUiPanGestureRecognizer : SkUiGestureRecognizer
{
    private long? _pointer;
    private Point _start;
    private bool _active;
    private SkUiVelocityTracker _velocity;

    /// <summary>Raised with Started, Running, Completed or Canceled.</summary>
    public event EventHandler<SkUiPanUpdatedEventArgs>? PanUpdated;

    /// <summary>Axes the pan reacts to (default both).</summary>
    public SkUiPanAxis Axis { get; set; }

    internal Action<SkUiPanUpdatedEventArgs>? Handler { get; init; }

    /// <inheritdoc />
    protected internal override bool IsExclusive => true;

    /// <inheritdoc />
    protected internal override bool OnPointerPressed(SkUiPointer pointer)
    {
        if (_pointer is not null)
            return false;
        _pointer = pointer.Id;
        _start = pointer.Position;
        _active = false;
        _velocity.Reset(pointer.Timestamp, pointer.Position);
        return true;
    }

    /// <inheritdoc />
    protected internal override void OnPointerMoved(SkUiPointer pointer)
    {
        if (_pointer != pointer.Id)
            return;
        _velocity.Add(pointer.Timestamp, pointer.Position);
        var dx = pointer.Position.X - _start.X;
        var dy = pointer.Position.Y - _start.Y;
        if (!_active)
        {
            if (!ExceedsSlop(dx, dy, Axis))
                return;
            Claim();
            if (_pointer != pointer.Id)
                return;
            _active = true;
            Raise(new(GestureStatus.Started, Mask(dx, true), Mask(dy, false)));
        }
        Raise(new(GestureStatus.Running, Mask(dx, true), Mask(dy, false)));
    }

    /// <inheritdoc />
    protected internal override void OnPointerReleased(SkUiPointer pointer)
    {
        if (_pointer != pointer.Id)
            return;
        _velocity.Add(pointer.Timestamp, pointer.Position);
        if (_active)
        {
            var velocity = _velocity.Velocity;
            Raise(new(GestureStatus.Completed, Mask(pointer.Position.X - _start.X, true), Mask(pointer.Position.Y - _start.Y, false),
                Mask(velocity.X, true), Mask(velocity.Y, false)));
            _active = false;
        }
        else
        {
            Resign();
        }
        _pointer = null;
    }

    /// <inheritdoc />
    protected internal override void OnRejected(long pointerId)
    {
        if (_pointer != pointerId)
            return;
        if (_active)
            Raise(new(GestureStatus.Canceled, 0, 0));
        _active = false;
        _pointer = null;
    }

    private double Mask(double value, bool horizontal) =>
        Axis == SkUiPanAxis.Both || (Axis == SkUiPanAxis.Horizontal) == horizontal ? value : 0;

    private void Raise(SkUiPanUpdatedEventArgs args)
    {
        PanUpdated?.Invoke(Owner, args);
        Handler?.Invoke(args);
    }
}

/// <summary>Swipe in one of <see cref="Direction"/>: claims when the movement passes the slop in an allowed direction.</summary>
public class SkUiSwipeGestureRecognizer : SkUiGestureRecognizer
{
    private long? _pointer;
    private Point _start;
    private SwipeDirection? _direction;
    private SkUiVelocityTracker _velocity;

    /// <summary>Raised for a completed swipe.</summary>
    public event EventHandler<SkUiSwipedEventArgs>? Swiped;

    /// <summary>Allowed directions (default all).</summary>
    public SwipeDirection Direction { get; set; } = SwipeDirection.Left | SwipeDirection.Right | SwipeDirection.Up | SwipeDirection.Down;

    /// <summary>Minimum distance (DIPs); <see cref="SkUiGestureSettings.SwipeThreshold"/> when unset.</summary>
    public double? Threshold { get; set; }

    internal Action<SkUiSwipedEventArgs>? Handler { get; init; }
    internal Func<SwipeDirection>? DirectionProvider { get; init; }

    /// <inheritdoc />
    protected internal override bool IsExclusive => true;

    private SwipeDirection Allowed => DirectionProvider?.Invoke() ?? Direction;

    /// <inheritdoc />
    protected internal override bool OnPointerPressed(SkUiPointer pointer)
    {
        if (_pointer is not null || Allowed == 0)
            return false;
        _pointer = pointer.Id;
        _start = pointer.Position;
        _direction = null;
        _velocity.Reset(pointer.Timestamp, pointer.Position);
        return true;
    }

    /// <inheritdoc />
    protected internal override void OnPointerMoved(SkUiPointer pointer)
    {
        if (_pointer != pointer.Id)
            return;
        _velocity.Add(pointer.Timestamp, pointer.Position);
        if (_direction is not null)
            return;
        var dx = pointer.Position.X - _start.X;
        var dy = pointer.Position.Y - _start.Y;
        if (!ExceedsSlop(dx, dy, SkUiPanAxis.Both))
            return;
        var direction = Math.Abs(dx) >= Math.Abs(dy)
            ? dx > 0 ? SwipeDirection.Right : SwipeDirection.Left
            : dy > 0 ? SwipeDirection.Down : SwipeDirection.Up;
        if ((Allowed & direction) == 0)
        {
            Resign();
            return;
        }
        Claim();
        if (_pointer == pointer.Id)
            _direction = direction;
    }

    /// <inheritdoc />
    protected internal override void OnPointerReleased(SkUiPointer pointer)
    {
        if (_pointer != pointer.Id)
            return;
        _velocity.Add(pointer.Timestamp, pointer.Position);
        if (_direction is { } direction)
        {
            var dx = pointer.Position.X - _start.X;
            var dy = pointer.Position.Y - _start.Y;
            var velocity = _velocity.Velocity;
            var (distance, speed) = direction switch
            {
                SwipeDirection.Right => (dx, velocity.X),
                SwipeDirection.Left => (-dx, -velocity.X),
                SwipeDirection.Down => (dy, velocity.Y),
                _ => (-dy, -velocity.Y)
            };
            if (distance >= (Threshold ?? SkUiGestureSettings.SwipeThreshold)
                || (speed >= SkUiGestureSettings.SwipeVelocity && distance > SkUiGestureSettings.TouchSlop))
            {
                var owner = Owner;
                var args = new SkUiSwipedEventArgs(direction,
                    owner is null ? _start : pointer.MapToElement(owner, _start),
                    owner is null ? pointer.Position : pointer.GetPosition(owner));
                Swiped?.Invoke(owner, args);
                Handler?.Invoke(args);
            }
        }
        else
        {
            Resign();
        }
        _pointer = null;
        _direction = null;
    }

    /// <inheritdoc />
    protected internal override void OnRejected(long pointerId)
    {
        if (_pointer == pointerId)
        {
            _pointer = null;
            _direction = null;
        }
    }
}

/// <summary>Two-pointer pinch / rotate on the owner.</summary>
public class SkUiPinchGestureRecognizer : SkUiGestureRecognizer
{
    private readonly List<(long Id, Point Position)> _pointers = new(2);
    private bool _active;
    private double _startDistance, _startAngle, _lastDistance, _lastAngle;

    /// <summary>Raised with Started, Running, Completed or Canceled.</summary>
    public event EventHandler<SkUiPinchUpdatedEventArgs>? PinchUpdated;

    internal Action<SkUiPinchUpdatedEventArgs>? Handler { get; init; }

    /// <inheritdoc />
    protected internal override bool IsExclusive => true;

    /// <inheritdoc />
    protected internal override bool OnPointerPressed(SkUiPointer pointer)
    {
        if (_pointers.Count >= 2)
            return false;
        _pointers.Add((pointer.Id, pointer.Position));
        if (_pointers.Count == 2)
        {
            _startDistance = _lastDistance = Distance();
            _startAngle = _lastAngle = Angle();
        }
        return true;
    }

    /// <inheritdoc />
    protected internal override void OnPointerMoved(SkUiPointer pointer)
    {
        var index = _pointers.FindIndex(entry => entry.Id == pointer.Id);
        if (index < 0)
            return;
        _pointers[index] = (pointer.Id, pointer.Position);
        if (_pointers.Count < 2)
            return;
        var distance = Distance();
        var angle = Angle();
        if (!_active)
        {
            if (Math.Abs(distance - _startDistance) <= SkUiGestureSettings.TouchSlop && Math.Abs(NormalizeAngle(angle - _startAngle)) < 5)
                return;
            Claim();
            if (_pointers.Count < 2)
                return;
            _active = true;
            _lastDistance = _startDistance;
            _lastAngle = _startAngle;
            Raise(GestureStatus.Started, 1, 0, pointer);
        }
        Raise(GestureStatus.Running, _lastDistance > 0 ? distance / _lastDistance : 1, NormalizeAngle(angle - _lastAngle), pointer);
        _lastDistance = distance;
        _lastAngle = angle;
    }

    /// <inheritdoc />
    protected internal override void OnPointerReleased(SkUiPointer pointer) => Drop(pointer.Id, completed: true, pointer);

    /// <inheritdoc />
    protected internal override void OnRejected(long pointerId) => Drop(pointerId, completed: false, null);

    private void Drop(long pointerId, bool completed, SkUiPointer? pointer)
    {
        var index = _pointers.FindIndex(entry => entry.Id == pointerId);
        if (index < 0)
            return;
        if (_active)
        {
            _active = false;
            Raise(completed ? GestureStatus.Completed : GestureStatus.Canceled, 1, 0, pointer);
            _pointers.RemoveAt(index);
            return;
        }
        _pointers.RemoveAt(index);
        if (completed)
            Resign(pointerId); // never pinched: let the arena pick another recognizer (e.g. a tap)
    }

    private void Raise(GestureStatus status, double scale, double rotation, SkUiPointer? pointer)
    {
        var origin = Point.Zero;
        if (_pointers.Count == 2)
        {
            var mid = new Point((_pointers[0].Position.X + _pointers[1].Position.X) / 2, (_pointers[0].Position.Y + _pointers[1].Position.Y) / 2);
            origin = pointer is { } sample && Owner is { } owner ? sample.MapToElement(owner, mid) : mid;
        }
        var args = new SkUiPinchUpdatedEventArgs(status, scale, _startDistance > 0 ? _lastDistance * scale / _startDistance : 1,
            rotation, NormalizeAngle(_lastAngle + rotation - _startAngle), origin);
        PinchUpdated?.Invoke(Owner, args);
        Handler?.Invoke(args);
    }

    private double Distance()
    {
        var dx = _pointers[1].Position.X - _pointers[0].Position.X;
        var dy = _pointers[1].Position.Y - _pointers[0].Position.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private double Angle() => Math.Atan2(_pointers[1].Position.Y - _pointers[0].Position.Y, _pointers[1].Position.X - _pointers[0].Position.X) * 180 / Math.PI;

    private static double NormalizeAngle(double degrees)
    {
        while (degrees > 180) degrees -= 360;
        while (degrees < -180) degrees += 360;
        return degrees;
    }
}

/// <summary>
/// Raw pointer samples for custom interaction (replaces overriding <c>Touch</c>): by default it claims every press
/// on its owner, so it receives the whole gesture.
/// </summary>
public class SkUiPointerGestureRecognizer : SkUiGestureRecognizer
{
    private readonly HashSet<long> _pointers = [];

    /// <summary>Pressed, Moved, Released and Cancelled samples in owner coordinates.</summary>
    public event EventHandler<SkUiPointerEventArgs>? Pointer;

    /// <summary>Claim on press (default); when <c>false</c> it competes like other recognizers and only observes.</summary>
    public bool ClaimOnPress { get; set; } = true;

    /// <summary>Whether owning a gesture keeps native ancestors from taking the touch (default true).</summary>
    public bool BlocksNativeGestures { get; set; } = true;

    /// <inheritdoc />
    protected internal override bool IsExclusive => BlocksNativeGestures;

    /// <inheritdoc />
    protected internal override bool OnPointerPressed(SkUiPointer pointer)
    {
        _pointers.Add(pointer.Id);
        Raise(pointer, SkUiTouchAction.Pressed);
        if (ClaimOnPress)
            Claim();
        return true;
    }

    /// <inheritdoc />
    protected internal override void OnPointerMoved(SkUiPointer pointer)
    {
        if (_pointers.Contains(pointer.Id))
            Raise(pointer, SkUiTouchAction.Moved);
    }

    /// <inheritdoc />
    protected internal override void OnPointerReleased(SkUiPointer pointer)
    {
        if (_pointers.Remove(pointer.Id))
            Raise(pointer, SkUiTouchAction.Released);
    }

    /// <inheritdoc />
    protected internal override void OnRejected(long pointerId)
    {
        if (_pointers.Remove(pointerId))
            Pointer?.Invoke(Owner, new SkUiPointerEventArgs(pointerId, SkUiTouchAction.Cancelled, Point.Zero));
    }

    private void Raise(SkUiPointer pointer, SkUiTouchAction action) =>
        Pointer?.Invoke(Owner, new SkUiPointerEventArgs(pointer.Id, action, Owner is { } owner ? pointer.GetPosition(owner) : pointer.Position));
}
