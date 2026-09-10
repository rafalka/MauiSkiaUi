using Microsoft.Maui;
using Microsoft.Maui.Graphics;

namespace MauiSkiaUi;

internal sealed class SkUiTouchRouter
{
    private ISkUiView? captured;
    private long pointerId;

    internal bool TryPress(ISkUiView child, SkUiTouchEvent touch)
    {
        if (captured is not null || touch.Action != SkUiTouchAction.Pressed
            || child.InputTransparent || child.Visibility != Visibility.Visible
            || !SkUiView.MapPoint(child, touch.Position, out var local)
            || !new Rect(Point.Zero, child.Frame.Size).Contains(local))
            return false;
        if (!child.Touch(touch with { Position = local }))
            return false;
        captured = child;
        pointerId = touch.Id;
        return true;
    }

    internal bool DeliverCaptured(SkUiTouchEvent touch, out bool handled)
    {
        handled = false;
        if (captured is null)
            return false;
        if (touch.Id != pointerId || touch.Action == SkUiTouchAction.Pressed)
            return true;
        var target = captured;
        if (touch.Action is SkUiTouchAction.Released or SkUiTouchAction.Cancelled)
            captured = null;
        if (!SkUiView.MapPoint(target, touch.Position, out var local))
        {
            target.Touch(new(pointerId, SkUiTouchAction.Cancelled, Point.Zero));
            captured = null;
        }
        else
        {
            target.Touch(touch with { Position = local });
        }
        handled = true;
        return true;
    }

    internal void Cancel()
    {
        captured?.Touch(new(pointerId, SkUiTouchAction.Cancelled, Point.Zero));
        captured = null;
    }
}