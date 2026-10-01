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
    /// <summary>A desktop wheel delta, positive towards the start of the scroll axis.</summary>
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
public readonly record struct SkUiTouchEvent(long Id, SkUiTouchAction Action, Point Position, TimeSpan? Timestamp = null, double WheelDelta = 0);