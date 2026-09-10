namespace MauiSkiaUi;

internal sealed class SkUiTouchRouter
{
    private ISkUiView? _captured;
    private long _pointerId;

    internal bool TryPress(ISkUiView child, SkUiTouchEvent touch)
    {
        if (_captured is not null || touch.Action is not (SkUiTouchAction.Pressed or SkUiTouchAction.Wheel)
            || child.InputTransparent || child.Visibility != Visibility.Visible
            || !SkUiView.MapPoint(child, touch.Position, out var local)
            || !new Rect(Point.Zero, child.Frame.Size).Contains(local))
            return false;
        if (!child.Touch(touch with { Position = local }))
            return false;
        if (touch.Action == SkUiTouchAction.Wheel) return true;
        _captured = child;
        _pointerId = touch.Id;
        return true;
    }

    internal bool DeliverCaptured(SkUiTouchEvent touch, out bool handled)
    {
        handled = false;
        if (_captured is null)
            return false;
        if (touch.Id != _pointerId || touch.Action == SkUiTouchAction.Pressed)
            return true;
        var target = _captured;
        if (touch.Action is SkUiTouchAction.Released or SkUiTouchAction.Cancelled)
            _captured = null;
        if (!SkUiView.MapPoint(target, touch.Position, out var local))
        {
            target.Touch(new(_pointerId, SkUiTouchAction.Cancelled, Point.Zero));
            _captured = null;
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
        _captured?.Touch(new(_pointerId, SkUiTouchAction.Cancelled, Point.Zero));
        _captured = null;
    }
}