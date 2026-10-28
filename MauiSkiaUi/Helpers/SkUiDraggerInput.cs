using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>What a dragger's layer provides to <see cref="SkUiDraggerModel"/>.</summary>
internal interface ISkUiDraggerOwner
{
    /// <summary>The axis the dragger follows now.</summary>
    SkUiPanAxis DragAxis { get; }

    /// <summary>Whether a drag can start (a grid splitter needs tracks on both sides); otherwise the pointer goes to others.</summary>
    bool CanDrag { get; }

    /// <summary>The dragger's position in its parent (its frame's origin), to measure travel while the dragger moves.</summary>
    Point Offset { get; }

    /// <summary>DIPs an arrow key moves the dragger.</summary>
    double KeyboardIncrement { get; }

    void Started(SkUiDragEventArgs args);

    void Moved(SkUiDragEventArgs args);

    void Ended(SkUiDragEventArgs args);
}

/// <summary>
/// Input shared by <see cref="SkUiDragger"/> and <see cref="Core.SkUiCoreDragger"/>: a drag along the axis claims the pointer
/// past the touch slop (a drag across a one-axis dragger goes to scrollers around it), then reports the travel from where the
/// pointer went down, in the parent's coordinates. An arrow key along the axis is a drag of one keyboard increment.
/// </summary>
internal sealed class SkUiDraggerModel(ISkUiDraggerOwner owner)
{
    private Point _pressed;
    private Point _total;
    private bool _dragging;
    private SkUiDraggerGestureRecognizer? _gesture;

    /// <summary>Whether a drag (or a key's drag) is in progress.</summary>
    public bool IsDragging => _dragging;

    /// <summary>The drag recognizer (one per dragger).</summary>
    public SkUiGestureRecognizer Gesture => _gesture ??= new SkUiDraggerGestureRecognizer
    {
        Axis = () => owner.DragAxis,
        Pressed = position =>
        {
            if (!owner.CanDrag)
                return false;
            _pressed = InParent(position);
            return true;
        },
        Began = position =>
        {
            _dragging = true;
            _total = default;
            owner.Started(new SkUiDragEventArgs(0, 0, 0, 0));
            Move(position);
        },
        Moved = Move,
        Ended = canceled =>
        {
            _dragging = false;
            owner.Ended(new SkUiDragEventArgs(_total.X, _total.Y, 0, 0, isCanceled: canceled));
        }
    };

    private Point InParent(Point local) => new(local.X + owner.Offset.X, local.Y + owner.Offset.Y);

    private void Move(Point position)
    {
        var now = InParent(position);
        var total = Mask(now.X - _pressed.X, now.Y - _pressed.Y);
        // Exact: no travel since the last event, nothing to report.
        if (total == _total)
            return;
        var delta = new Point(total.X - _total.X, total.Y - _total.Y);
        _total = total;
        owner.Moved(new SkUiDragEventArgs(total.X, total.Y, delta.X, delta.Y));
    }

    // Travel across a one-axis dragger does not count.
    private Point Mask(double x, double y) => owner.DragAxis switch
    {
        SkUiPanAxis.Horizontal => new Point(x, 0),
        SkUiPanAxis.Vertical => new Point(0, y),
        _ => new Point(x, y)
    };

    /// <summary>Arrow keys along the axis: a drag of one keyboard increment each (started, delta, completed).</summary>
    public bool OnKeyDown(in SkUiKeyEvent key)
    {
        if (key.HasCommandModifier || _dragging || !owner.CanDrag)
            return false;
        var step = owner.KeyboardIncrement;
        var travel = key.Key switch
        {
            SkUiKey.Left => new Point(-step, 0),
            SkUiKey.Right => new Point(step, 0),
            SkUiKey.Up => new Point(0, -step),
            SkUiKey.Down => new Point(0, step),
            _ => default
        };
        travel = Mask(travel.X, travel.Y);
        if (travel == default)
            return false;
        _dragging = true;
        owner.Started(new SkUiDragEventArgs(0, 0, 0, 0, isKeyboard: true));
        owner.Moved(new SkUiDragEventArgs(travel.X, travel.Y, travel.X, travel.Y, isKeyboard: true));
        _dragging = false;
        owner.Ended(new SkUiDragEventArgs(travel.X, travel.Y, 0, 0, isKeyboard: true));
        return true;
    }
}

/// <summary>
/// A drag along an axis (or any direction): claims the pointer once it passes the touch slop, and resigns a drag across a
/// one-axis dragger. Continuous, so it holds off native ancestors while the drag may still start.
/// </summary>
internal sealed class SkUiDraggerGestureRecognizer : SkUiGestureRecognizer
{
    private long? _pointer;
    private Point _start;
    private bool _active;

    internal required Func<SkUiPanAxis> Axis { get; init; }
    /// <summary>A pointer went down (owner coordinates); <c>false</c> leaves it to others.</summary>
    internal required Func<Point, bool> Pressed { get; init; }
    internal required Action<Point> Began { get; init; }
    internal required Action<Point> Moved { get; init; }
    /// <summary>The drag ended: released (<c>false</c>) or canceled (<c>true</c>).</summary>
    internal required Action<bool> Ended { get; init; }

    /// <inheritdoc />
    protected internal override bool IsExclusive => true;

    /// <inheritdoc />
    protected internal override bool OnPointerPressed(SkUiPointer pointer)
    {
        if (_pointer is not null || !Pressed(Local(pointer)))
            return false;
        _pointer = pointer.Id;
        _start = pointer.Position;
        _active = false;
        return true;
    }

    /// <inheritdoc />
    protected internal override void OnPointerMoved(SkUiPointer pointer)
    {
        if (_pointer != pointer.Id)
            return;
        if (_active)
        {
            Moved(Local(pointer));
            return;
        }
        var dx = pointer.Position.X - _start.X;
        var dy = pointer.Position.Y - _start.Y;
        var axis = Axis();
        if (ExceedsSlop(dx, dy, axis))
        {
            Claim();
            if (_pointer != pointer.Id)
                return;
            _active = true;
            Began(Local(pointer));
        }
        else if (axis != SkUiPanAxis.Both && ExceedsSlop(dx, dy, axis == SkUiPanAxis.Vertical ? SkUiPanAxis.Horizontal : SkUiPanAxis.Vertical))
        {
            Resign(); // a drag across the dragger belongs to scrollers around it
        }
    }

    /// <inheritdoc />
    protected internal override void OnPointerReleased(SkUiPointer pointer)
    {
        if (_pointer != pointer.Id)
            return;
        if (_active)
        {
            Moved(Local(pointer));
            Finish(canceled: false);
            return;
        }
        _pointer = null;
        Resign();
    }

    /// <inheritdoc />
    protected internal override void OnRejected(long pointerId)
    {
        if (_pointer != pointerId)
            return;
        if (_active)
            Finish(canceled: true);
        _pointer = null;
    }

    private void Finish(bool canceled)
    {
        _active = false;
        _pointer = null;
        Ended(canceled);
    }

    private Point Local(SkUiPointer pointer) => Owner is { } owner ? pointer.GetPosition(owner) : pointer.Position;
}

/// <summary>Measuring and drawing shared by both dragger layers.</summary>
internal static class SkUiDraggerMath
{
    /// <summary>
    /// Whether the line (and the grip) runs top to bottom: across a horizontal drag; for a two-axis dragger, along its
    /// longer side.
    /// </summary>
    public static bool LineRunsVertically(SkUiPanAxis axis, Size size) => axis switch
    {
        SkUiPanAxis.Horizontal => true,
        SkUiPanAxis.Vertical => false,
        _ => size.Width < size.Height
    };

    /// <summary>The look's size for a dragger on <paramref name="axis"/>.</summary>
    public static Size Measure(SkUiPanAxis axis) => SkUiLook.Current.MeasureDragger(axis == SkUiPanAxis.Horizontal);

    /// <summary>
    /// The touch area in dragger coordinates: the dragger, widened across its line to at least <paramref name="touchThickness"/>
    /// (below 0: <paramref name="look"/>'s <see cref="SkUiLook.DraggerTouchThickness"/>) and centered on it; both ways for a
    /// two-axis dragger. A dragger at least that thick is its own touch area.
    /// </summary>
    public static SKRect TouchBounds(Size size, SkUiPanAxis axis, double touchThickness, SkUiLook look) =>
        Widen(size, axis, touchThickness >= 0 ? touchThickness : look.DraggerTouchThickness);

    /// <summary>Whether <paramref name="local"/> (outside the dragger) is in its touch area; only while it can be dragged.</summary>
    public static bool HitsTouchArea(Size size, SkUiPanAxis axis, double touchThickness, SkUiLook look, Point local) =>
        TouchBounds(size, axis, touchThickness, look).Contains((float)local.X, (float)local.Y);

    /// <summary>
    /// What a dragger draws past its bounds: its touch area, and room for the grip at its dragged thickness centered on a
    /// thinner dragger. The node then draws unclipped, over its neighbors.
    /// </summary>
    public static void ApplyOverflow(ref SkUiRenderProps props, SkUiPanAxis axis, double touchThickness, SkUiLook look)
    {
        var size = new Size(props.Width, props.Height);
        var ink = Widen(size, axis, Math.Max(touchThickness >= 0 ? touchThickness : look.DraggerTouchThickness, 2 * look.DraggerGripThickness));
        var overflow = new SKRect(-ink.Left, -ink.Top, ink.Right - props.Width, ink.Bottom - props.Height);
        if (overflow is { Left: <= 0, Top: <= 0, Right: <= 0, Bottom: <= 0 })
            return;
        props.ClipToBounds = false;
        var current = props.Overflow;
        props.Overflow = new SKRect(Math.Max(current.Left, overflow.Left), Math.Max(current.Top, overflow.Top),
            Math.Max(current.Right, overflow.Right), Math.Max(current.Bottom, overflow.Bottom));
    }

    // The dragger's bounds, widened to `thickness` (centered) across a one-axis dragger's line, both ways for a two-axis one.
    private static SKRect Widen(Size size, SkUiPanAxis axis, double thickness)
    {
        var bounds = new SKRect(0, 0, (float)size.Width, (float)size.Height);
        if (axis != SkUiPanAxis.Vertical && bounds.Width < thickness)
            bounds.Inflate((float)(thickness - bounds.Width) / 2, 0);
        if (axis != SkUiPanAxis.Horizontal && bounds.Height < thickness)
            bounds.Inflate(0, (float)(thickness - bounds.Height) / 2);
        return bounds;
    }

    /// <summary>
    /// What the look draws, in left-to-right coordinates: a right-to-left layout swaps a horizontal line's grip start and
    /// end. A grip length below 0 is the look's.
    /// </summary>
    public static SkUiDraggerPaint Paint(Size size, SkUiPanAxis axis, double touchThickness, bool rightToLeft, bool showsGrip, SKColor grip,
        LayoutAlignment gripAlignment, double gripLength, bool showsLine, SKColor line, bool dragging, bool enabled)
    {
        var vertical = LineRunsVertically(axis, size);
        if (rightToLeft && !vertical)
            gripAlignment = gripAlignment switch
            {
                LayoutAlignment.Start => LayoutAlignment.End,
                LayoutAlignment.End => LayoutAlignment.Start,
                _ => gripAlignment
            };
        var length = gripLength >= 0 ? gripLength : SkUiLook.Current.DraggerGripLength;
        return new SkUiDraggerPaint(new SKRect(0, 0, (float)size.Width, (float)size.Height), TouchBounds(size, axis, touchThickness, SkUiLook.Current), vertical,
            showsGrip, grip, gripAlignment, (float)length, showsLine, line, dragging, enabled);
    }
}
