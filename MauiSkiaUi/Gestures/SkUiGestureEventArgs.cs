namespace MauiSkiaUi;

/// <summary>A classified tap in local DIPs.</summary>
public sealed class SkUiTappedEventArgs(Point position, int numberOfTaps = 1) : EventArgs
{
    /// <summary>The release position relative to the tapped node.</summary>
    public Point Position { get; } = position;

    /// <summary>1 for a single tap, 2 for a double tap.</summary>
    public int NumberOfTaps { get; } = numberOfTaps;
}

/// <summary>A long press (the pointer was held within the touch slop for <see cref="SkUiGestureSettings.LongPressDuration"/>).</summary>
public sealed class SkUiLongPressedEventArgs(Point position) : EventArgs
{
    /// <summary>Press position relative to the node.</summary>
    public Point Position { get; } = position;
}

/// <summary>A completed swipe.</summary>
public sealed class SkUiSwipedEventArgs(SwipeDirection direction, Point start, Point end) : EventArgs
{
    /// <summary>Dominant swipe direction.</summary>
    public SwipeDirection Direction { get; } = direction;

    /// <summary>Start position relative to the node.</summary>
    public Point Start { get; } = start;

    /// <summary>End position relative to the node.</summary>
    public Point End { get; } = end;
}

/// <summary>Pan progress. Totals are relative to the pan start, in surface DIPs (unaffected by the node's own transforms).</summary>
public sealed class SkUiPanUpdatedEventArgs(GestureStatus status, double totalX, double totalY, double velocityX = 0, double velocityY = 0) : EventArgs
{
    /// <summary>Started, Running, Completed or Canceled.</summary>
    public GestureStatus Status { get; } = status;

    /// <summary>Horizontal distance since the pan started.</summary>
    public double TotalX { get; } = totalX;

    /// <summary>Vertical distance since the pan started.</summary>
    public double TotalY { get; } = totalY;

    /// <summary>Horizontal velocity (DIPs / s), set on Completed.</summary>
    public double VelocityX { get; } = velocityX;

    /// <summary>Vertical velocity (DIPs / s), set on Completed.</summary>
    public double VelocityY { get; } = velocityY;
}

/// <summary>Pinch / rotate progress with two pointers.</summary>
public sealed class SkUiPinchUpdatedEventArgs(GestureStatus status, double scale, double totalScale, double rotation, double totalRotation, Point origin) : EventArgs
{
    /// <summary>Started, Running, Completed or Canceled.</summary>
    public GestureStatus Status { get; } = status;

    /// <summary>Scale change since the previous update (multiply into an accumulated scale).</summary>
    public double Scale { get; } = scale;

    /// <summary>Scale since the pinch started.</summary>
    public double TotalScale { get; } = totalScale;

    /// <summary>Rotation change (degrees, clockwise) since the previous update.</summary>
    public double Rotation { get; } = rotation;

    /// <summary>Rotation (degrees) since the pinch started.</summary>
    public double TotalRotation { get; } = totalRotation;

    /// <summary>Midpoint of the two pointers relative to the node.</summary>
    public Point Origin { get; } = origin;
}

/// <summary>Raw pointer sample for <see cref="SkUiPointerGestureRecognizer"/>.</summary>
public sealed class SkUiPointerEventArgs(long pointerId, SkUiTouchAction action, Point position) : EventArgs
{
    /// <summary>Stable pointer id.</summary>
    public long PointerId { get; } = pointerId;

    /// <summary>Pressed, Moved, Released or Cancelled.</summary>
    public SkUiTouchAction Action { get; } = action;

    /// <summary>Position relative to the node.</summary>
    public Point Position { get; } = position;
}
