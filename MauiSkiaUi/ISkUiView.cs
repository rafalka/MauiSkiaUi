using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>A handler-independent view painted in local device-independent coordinates.</summary>
public interface ISkUiView : IView
{
    /// <summary>Paints this node and its descendants at the current canvas origin.</summary>
    void Paint(SKCanvas canvas);

    /// <summary>Delivers a pointer event in this node's local coordinates.</summary>
    bool Touch(SkUiTouchEvent touch);
}

/// <summary>The supported phases of a pointer interaction.</summary>
public enum SkUiTouchAction
{
    /// <summary>A pointer was pressed.</summary>
    Pressed,
    /// <summary>A captured pointer moved.</summary>
    Moved,
    /// <summary>A pointer was released.</summary>
    Released,
    /// <summary>The platform cancelled the interaction.</summary>
    Cancelled,
    /// <summary>
    /// A desktop wheel or trackpad scroll: <see cref="SkUiTouchEvent.WheelDelta"/> (vertical) and
    /// <see cref="SkUiTouchEvent.WheelDeltaX"/> (horizontal), positive towards the start of each axis.
    /// </summary>
    Wheel,
    /// <summary>
    /// A pointer moved over the surface without contact (mouse, trackpad, pen or iPad pointer hover): updates
    /// <see cref="SkUiView.IsPointerOver"/> and the <c>PointerOver</c> visual state. Not part of any gesture.
    /// </summary>
    HoverMoved,
    /// <summary>The hovering pointer left the surface (or started touching it): nothing is pointer-over any more.</summary>
    HoverExited
}

/// <summary>A pointer sample with a stable id and a local position in DIPs.</summary>
/// <param name="Id">Stable pointer id.</param>
/// <param name="Action">The phase.</param>
/// <param name="Position">Local position in DIPs.</param>
/// <param name="Timestamp">Sample time (now when <c>null</c>).</param>
/// <param name="WheelDelta">Vertical wheel / trackpad scroll in DIPs, positive towards the top (<see cref="SkUiTouchAction.Wheel"/>).</param>
/// <param name="WheelDeltaX">Horizontal wheel / trackpad scroll in DIPs, positive towards the left (<see cref="SkUiTouchAction.Wheel"/>).</param>
/// <param name="Device">What produced the sample: a finger, a mouse (or trackpad click) or a pen.</param>
public readonly record struct SkUiTouchEvent(long Id, SkUiTouchAction Action, Point Position, TimeSpan? Timestamp = null, double WheelDelta = 0,
    double WheelDeltaX = 0, SkUiPointerDevice Device = SkUiPointerDevice.Touch)
{
    /// <summary>A touch sample (<see cref="SkUiPointerDevice.Touch"/>): the constructor of builds before <see cref="Device"/>, kept for binary compatibility.</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public SkUiTouchEvent(long Id, SkUiTouchAction Action, Point Position, TimeSpan? Timestamp, double WheelDelta, double WheelDeltaX)
        : this(Id, Action, Position, Timestamp, WheelDelta, WheelDeltaX, SkUiPointerDevice.Touch)
    {
    }
}

/// <summary>What produces a pointer's samples (<see cref="SkUiTouchEvent.Device"/>, <see cref="SkUiPointer.Device"/>).</summary>
public enum SkUiPointerDevice
{
    /// <summary>A finger on a touch screen (also when the platform does not tell).</summary>
    Touch,

    /// <summary>A mouse, or a trackpad click (iPad pointer, Mac Catalyst, Windows, Android with a mouse).</summary>
    Mouse,

    /// <summary>A pen or stylus (Apple Pencil, Android stylus, Windows pen).</summary>
    Pen
}