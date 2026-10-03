namespace MauiSkiaUi;

/// <summary>A pointer sample delivered to <see cref="SkUiGestureRecognizer"/>s.</summary>
public readonly struct SkUiPointer
{
    private readonly SkUiPointerRouter? _router;

    internal SkUiPointer(long id, Point position, Point startPosition, TimeSpan timestamp, SkUiPointerRouter router)
    {
        Id = id;
        Position = position;
        StartPosition = startPosition;
        Timestamp = timestamp;
        _router = router;
    }

    /// <summary>Stable pointer id.</summary>
    public long Id { get; }

    /// <summary>Position in the dispatching surface root's coordinates (DIPs).</summary>
    public Point Position { get; }

    /// <summary>Press position in the same coordinates.</summary>
    public Point StartPosition { get; }

    /// <summary>Sample time.</summary>
    public TimeSpan Timestamp { get; }

    /// <summary>Distance moved since the press (surface DIPs).</summary>
    public double TotalX => Position.X - StartPosition.X;

    /// <inheritdoc cref="TotalX" />
    public double TotalY => Position.Y - StartPosition.Y;

    /// <summary>
    /// <see cref="Position"/> in <paramref name="element"/>'s local coordinates (an <see cref="SkUiView"/> or Core node
    /// under the same surface), through current transforms and scroll offsets.
    /// </summary>
    public Point GetPosition(object element) => _router?.MapToNode(element, Position) ?? Position;

    /// <summary>Maps any surface-root point into <paramref name="element"/>'s local coordinates.</summary>
    public Point MapToElement(object element, Point surfacePoint) => _router?.MapToNode(element, surfacePoint) ?? surfacePoint;

    /// <summary>The router that dispatched this sample.</summary>
    internal SkUiPointerRouter? Router => _router;
}

/// <summary>
/// A gesture recognizer taking part in the gesture arena. On each press, the element under the pointer and its
/// ancestors contribute recognizers; all of them receive the pointer's samples until one <see cref="Claim"/>s the
/// gesture (the others are rejected) or they <see cref="Resign()"/>. When a single one remains it wins; on release an
/// undecided arena is won by the innermost remaining recognizer (a tap beats an ancestor's unstarted scroll).
/// Add recognizers to <see cref="SkUiView.Gestures"/> or <see cref="Core.SkUiCoreNode.AddGestureRecognizer"/>.
/// </summary>
public abstract class SkUiGestureRecognizer
{
    private readonly List<SkUiGestureArena> _arenas = new(1);

    internal ISkUiInputNode? Node { get; set; }

    /// <summary>The element (<see cref="SkUiView"/> or Core node) whose gestures this recognizer handles.</summary>
    public object? Owner => Node;

    /// <summary>Whether the recognizer currently takes part in at least one pointer's arena.</summary>
    public bool IsTracking => _arenas.Count > 0;

    /// <summary>
    /// Continuous gestures (pan, scroll, swipe, pinch): when such a recognizer claims, native ancestors (e.g. a MAUI
    /// ScrollView hosting the surface) are kept from taking over the touch.
    /// </summary>
    protected internal virtual bool IsExclusive => false;

    /// <summary>A pointer pressed on the owner (or a descendant). Return <c>false</c> to stay out of this pointer's arena.</summary>
    protected internal virtual bool OnPointerPressed(SkUiPointer pointer) => true;

    /// <summary>A tracked pointer moved.</summary>
    protected internal virtual void OnPointerMoved(SkUiPointer pointer) { }

    /// <summary>A tracked pointer was released (the arena may still decide afterwards).</summary>
    protected internal virtual void OnPointerReleased(SkUiPointer pointer) { }

    /// <summary>This recognizer won the pointer's arena (explicitly or as the last / innermost member).</summary>
    protected internal virtual void OnAccepted(long pointerId) { }

    /// <summary>This recognizer lost, resigned, or the pointer was cancelled / its element detached or disabled.</summary>
    protected internal virtual void OnRejected(long pointerId) { }

    /// <summary>Claims every pointer this recognizer tracks; competing recognizers are rejected.</summary>
    protected void Claim()
    {
        foreach (var arena in _arenas.ToArray())
            arena.Resolve(this, explicitClaim: true);
    }

    /// <summary>Leaves every arena (rejected).</summary>
    protected void Resign()
    {
        foreach (var arena in _arenas.ToArray())
            arena.Remove(this, rejected: true);
    }

    /// <summary>Leaves one pointer's arena (rejected).</summary>
    protected void Resign(long pointerId)
    {
        foreach (var arena in _arenas.ToArray())
            if (arena.PointerId == pointerId)
                arena.Remove(this, rejected: true);
    }

    /// <summary>Whether this recognizer won <paramref name="pointerId"/>'s arena.</summary>
    protected bool HasWon(long pointerId)
    {
        foreach (var arena in _arenas)
            if (arena.PointerId == pointerId)
                return ReferenceEquals(arena.Winner, this);
        return false;
    }

    /// <summary>Stops tracking all pointers (e.g. the owner was disabled or detached).</summary>
    public void Cancel()
    {
        foreach (var arena in _arenas)
            arena.Invalidated = true;
        Resign();
    }

    /// <summary>The owner's arranged size (DIPs).</summary>
    protected Size OwnerSize => Node is { } node ? SkUiPointerRouter.GetSize(node) : Size.Zero;

    /// <summary>Whether a point in owner coordinates lies inside the owner's arranged rectangle.</summary>
    protected bool IsInsideOwner(Point local)
    {
        var size = OwnerSize;
        return local.X >= 0 && local.Y >= 0 && local.X <= size.Width && local.Y <= size.Height;
    }

    /// <summary>Whether a movement exceeds <see cref="SkUiGestureSettings.TouchSlop"/> along <paramref name="axis"/> (and dominates the other axis).</summary>
    protected static bool ExceedsSlop(double dx, double dy, SkUiPanAxis axis)
    {
        var slop = SkUiGestureSettings.TouchSlop;
        return axis switch
        {
            SkUiPanAxis.Horizontal => Math.Abs(dx) > slop && Math.Abs(dx) >= Math.Abs(dy),
            SkUiPanAxis.Vertical => Math.Abs(dy) > slop && Math.Abs(dy) >= Math.Abs(dx),
            _ => dx * dx + dy * dy > slop * slop
        };
    }

    internal void JoinArena(SkUiGestureArena arena) => _arenas.Add(arena);

    internal void LeaveArena(SkUiGestureArena arena) => _arenas.Remove(arena);
}

/// <summary>Axes a pan recognizer reacts to.</summary>
public enum SkUiPanAxis
{
    /// <summary>Any direction.</summary>
    Both,
    /// <summary>Horizontal movement only.</summary>
    Horizontal,
    /// <summary>Vertical movement only.</summary>
    Vertical
}

/// <summary>Estimates pointer velocity from the samples of the last 100 ms.</summary>
internal struct SkUiVelocityTracker
{
    private const int Capacity = 16;
    private static readonly TimeSpan Window = TimeSpan.FromMilliseconds(100);
    private (TimeSpan Time, Point Position)[]? _samples;
    private int _count;
    private int _next;

    public void Reset(TimeSpan time, Point position)
    {
        _samples ??= new (TimeSpan, Point)[Capacity];
        _count = 0;
        _next = 0;
        Add(time, position);
    }

    public void Add(TimeSpan time, Point position)
    {
        _samples ??= new (TimeSpan, Point)[Capacity];
        _samples[_next] = (time, position);
        _next = (_next + 1) % Capacity;
        _count = Math.Min(_count + 1, Capacity);
    }

    /// <summary>Pointer velocity (DIPs / s) over the window ending at the newest sample; zero when it is stale.</summary>
    public readonly Point Velocity
    {
        get
        {
            if (_samples is null || _count < 2)
                return Point.Zero;
            var newest = _samples[(_next - 1 + Capacity) % Capacity];
            var oldest = newest;
            for (var index = 2; index <= _count; index++)
            {
                var sample = _samples[(_next - index + Capacity * 2) % Capacity];
                if (newest.Time - sample.Time > Window)
                    break;
                oldest = sample;
            }
            var seconds = (newest.Time - oldest.Time).TotalSeconds;
            return seconds <= 0
                ? Point.Zero
                : new Point((newest.Position.X - oldest.Position.X) / seconds, (newest.Position.Y - oldest.Position.Y) / seconds);
        }
    }
}
