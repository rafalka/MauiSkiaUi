using System.Windows.Input;
using MauiSkiaUi.Rendering;

namespace MauiSkiaUi;

/// <summary>
/// Gesture handlers, commands and recognizers of one element, allocated on first use so passive elements (most Core
/// nodes) carry a single null field. Built-in recognizers exist only while their events / commands are in use.
/// </summary>
internal sealed class SkUiGestureSet(object owner)
{
    public EventHandler<SkUiTappedEventArgs>? Tapped;
    public EventHandler<SkUiTappedEventArgs>? DoubleTapped;
    public EventHandler<SkUiLongPressedEventArgs>? LongPressed;
    public EventHandler<SkUiSwipedEventArgs>? Swiped;
    public EventHandler<SkUiPanUpdatedEventArgs>? PanUpdated;
    public EventHandler<SkUiPinchUpdatedEventArgs>? PinchUpdated;
    public ICommand? DoubleTappedCommand;
    public object? DoubleTappedCommandParameter;
    public ICommand? LongPressedCommand;
    public object? LongPressedCommandParameter;
    public ICommand? SwipedCommand;
    public object? SwipedCommandParameter;
    public SwipeDirection SwipeDirections = SwipeDirection.Left | SwipeDirection.Right | SwipeDirection.Up | SwipeDirection.Down;
    public SkUiPanAxis PanAxis;
    public List<SkUiGestureRecognizer>? Custom;
    /// <summary>Built-in tap recognizer (Core nodes keep it here; SkUi* views hold their own).</summary>
    public SkUiTapGestureRecognizer? Tap;

    private SkUiLongPressGestureRecognizer? _longPress;
    private SkUiSwipeGestureRecognizer? _swipe;
    private SkUiPanGestureRecognizer? _pan;
    private SkUiPinchGestureRecognizer? _pinch;

    public bool WantsDoubleTap => DoubleTapped is not null || DoubleTappedCommand?.CanExecute(DoubleTappedCommandParameter) == true;

    /// <summary>Appends the non-tap recognizers in use (long press, swipe, pan, pinch, then custom ones).</summary>
    public void Collect(List<SkUiGestureRecognizer> recognizers)
    {
        if (LongPressed is not null || LongPressedCommand?.CanExecute(LongPressedCommandParameter) == true)
            recognizers.Add(_longPress ??= new SkUiLongPressGestureRecognizer { Handler = RaiseLongPressed });
        if (Swiped is not null || SwipedCommand?.CanExecute(SwipedCommandParameter) == true)
            recognizers.Add(_swipe ??= new SkUiSwipeGestureRecognizer { Handler = RaiseSwiped, DirectionProvider = () => SwipeDirections });
        if (PanUpdated is not null)
        {
            _pan ??= new SkUiPanGestureRecognizer { Handler = args => PanUpdated?.Invoke(owner, args) };
            _pan.Axis = PanAxis;
            recognizers.Add(_pan);
        }
        if (PinchUpdated is not null)
            recognizers.Add(_pinch ??= new SkUiPinchGestureRecognizer { Handler = args => PinchUpdated?.Invoke(owner, args) });
        if (Custom is { Count: > 0 } custom)
            recognizers.AddRange(custom);
    }

    public void RaiseDoubleTapped(SkUiTappedEventArgs args)
    {
        DoubleTapped?.Invoke(owner, args);
        if (DoubleTappedCommand?.CanExecute(DoubleTappedCommandParameter) == true)
            DoubleTappedCommand.Execute(DoubleTappedCommandParameter);
    }

    private void RaiseLongPressed(SkUiLongPressedEventArgs args)
    {
        LongPressed?.Invoke(owner, args);
        if (LongPressedCommand?.CanExecute(LongPressedCommandParameter) == true)
            LongPressedCommand.Execute(LongPressedCommandParameter);
    }

    private void RaiseSwiped(SkUiSwipedEventArgs args)
    {
        Swiped?.Invoke(owner, args);
        var parameter = SwipedCommandParameter ?? args.Direction;
        if (SwipedCommand?.CanExecute(parameter) == true)
            SwipedCommand.Execute(parameter);
    }

    /// <summary>Cancels every recognizer of this element.</summary>
    public void CancelAll()
    {
        Tap?.Cancel();
        _longPress?.Cancel();
        _swipe?.Cancel();
        _pan?.Cancel();
        _pinch?.Cancel();
        if (Custom is { } custom)
            foreach (var recognizer in custom)
                recognizer.Cancel();
    }

    /// <summary>Cancels the gestures of a subtree that left its surface (only needed while pointers are down).</summary>
    public static void CancelSubtree(ISkUiRenderable node, List<ISkUiRenderable>? scratch = null)
    {
        if (SkUiPointerRouter.ActiveArenas == 0)
            return;
        (node as ISkUiGestureElement)?.CancelGestures();
        scratch ??= [];
        var start = scratch.Count;
        node.GetRenderChildren(scratch);
        var end = scratch.Count;
        for (var index = start; index < end; index++)
            CancelSubtree(scratch[index], scratch);
        scratch.RemoveRange(start, end - start);
    }
}

/// <summary>An element whose gestures can be cancelled (detach, disable, hide).</summary>
internal interface ISkUiGestureElement : ISkUiInputNode
{
    void CancelGestures();
}
