namespace MauiSkiaUi;

/// <summary>
/// A drag of a <see cref="SkUiDragger"/> or <see cref="Core.SkUiCoreDragger"/>, in its parent's coordinates: when a handler
/// moves or resizes the dragger, its own movement does not count, so the totals keep following the pointer. Travel across the
/// dragger's axis is 0 (a <see cref="SkUiPanAxis.Vertical"/> dragger reports no X travel).
/// </summary>
/// <param name="totalX">Horizontal travel since the drag started, in DIPs (right is positive).</param>
/// <param name="totalY">Vertical travel since the drag started, in DIPs (down is positive).</param>
/// <param name="deltaX">Horizontal travel since the previous event.</param>
/// <param name="deltaY">Vertical travel since the previous event.</param>
/// <param name="isKeyboard">An arrow key moved the dragger: one started, delta and completed event per key press.</param>
/// <param name="isCanceled">On completion: the drag was taken over or the pointer was lost, not released.</param>
public class SkUiDragEventArgs(double totalX, double totalY, double deltaX, double deltaY, bool isKeyboard = false, bool isCanceled = false) : EventArgs
{
    /// <summary>Horizontal travel since the drag started, in DIPs (right is positive).</summary>
    public double TotalX { get; } = totalX;

    /// <summary>Vertical travel since the drag started, in DIPs (down is positive).</summary>
    public double TotalY { get; } = totalY;

    /// <summary>Horizontal travel since the previous event.</summary>
    public double DeltaX { get; } = deltaX;

    /// <summary>Vertical travel since the previous event.</summary>
    public double DeltaY { get; } = deltaY;

    /// <summary>An arrow key moved the dragger (one started, delta and completed event per key press).</summary>
    public bool IsKeyboard { get; } = isKeyboard;

    /// <summary>On completion: the drag was taken over or the pointer was lost, not released.</summary>
    public bool IsCanceled { get; } = isCanceled;
}
