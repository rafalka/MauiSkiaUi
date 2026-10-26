using System.Runtime.CompilerServices;

namespace MauiSkiaUi;

/// <summary>
/// The bridge for MAUI's swipe, pan, pinch and pointer recognizers in a drawn view's <c>GestureRecognizers</c> (taps are
/// <see cref="SkUiMauiTaps"/>'): each runs as an arena member of the view, on the drawn recognizer of its kind, and is
/// raised through MAUI's public API (<see cref="SwipeGestureRecognizer.SendSwiped"/>, <see cref="IPanGestureController"/>,
/// <see cref="IPinchGestureController"/>), so its events and commands fire as on MAUI. Pointer recognizers take their
/// entered / moved / exited events from hover routing and their pressed / released ones from presses, raised through
/// MAUI's internal <c>Send*</c> methods. A drawn recognizer is created for a MAUI one the first time a press finds it and
/// dropped once the MAUI one left the collection; changes to the collection apply from the next press. Views without
/// these recognizers allocate nothing.
/// </summary>
internal sealed class SkUiMauiGestures(View view)
{
    private readonly List<(IGestureRecognizer Maui, SkUiGestureRecognizer Drawn)> _bridged = [];
    private bool _hovering;
    private SkUiPointerRouter? _hoverRouter;
    private Point _hoverPosition;

    /// <summary>Whether the bridge runs <paramref name="recognizer"/>: a swipe, a pinch, a pointer, or a one-pointer pan.</summary>
    internal static bool Runs(IGestureRecognizer recognizer) =>
        recognizer is SwipeGestureRecognizer or PinchGestureRecognizer or PointerGestureRecognizer or PanGestureRecognizer { TouchPoints: 1 };

    /// <summary>Whether <paramref name="recognizers"/> has a pointer recognizer (hover routing asks on every move).</summary>
    internal static bool HasPointer(IList<IGestureRecognizer> recognizers)
    {
        for (var index = 0; index < recognizers.Count; index++)
            if (recognizers[index] is PointerGestureRecognizer)
                return true;
        return false;
    }

    /// <summary>Whether <paramref name="recognizers"/> has one the bridge runs.</summary>
    internal static bool HasAny(IList<IGestureRecognizer> recognizers)
    {
        for (var index = 0; index < recognizers.Count; index++)
            if (Runs(recognizers[index]))
                return true;
        return false;
    }

    /// <summary>Appends the drawn recognizers of the bridged MAUI ones, in the collection's order.</summary>
    public void Collect(List<SkUiGestureRecognizer> recognizers)
    {
        var mauis = view.GestureRecognizers;
        for (var index = _bridged.Count - 1; index >= 0; index--)
            if (!mauis.Contains(_bridged[index].Maui))
                _bridged.RemoveAt(index);
        for (var index = 0; index < mauis.Count; index++)
        {
            var maui = mauis[index];
            if (!Runs(maui))
                continue;
            var drawn = Find(maui) ?? Bridge(maui);
            if (maui is SwipeGestureRecognizer swipe && drawn is SkUiSwipeGestureRecognizer drawnSwipe)
                drawnSwipe.Threshold = swipe.Threshold;
            recognizers.Add(drawn);
        }
    }

    /// <summary>Cancels the bridged recognizers' gestures.</summary>
    public void CancelAll()
    {
        foreach (var (_, drawn) in _bridged)
            drawn.Cancel();
    }

    private SkUiGestureRecognizer? Find(IGestureRecognizer maui)
    {
        foreach (var (bridged, drawn) in _bridged)
            if (ReferenceEquals(bridged, maui))
                return drawn;
        return null;
    }

    private SkUiGestureRecognizer Bridge(IGestureRecognizer maui)
    {
        SkUiGestureRecognizer drawn = maui switch
        {
            SwipeGestureRecognizer swipe => new SkUiSwipeGestureRecognizer
            {
                // MAUI's default Direction is none: such a recognizer never swipes.
                DirectionProvider = () => swipe.Direction,
                Handler = args => swipe.SendSwiped(view, args.Direction),
            },
            PanGestureRecognizer pan => BridgePan(pan),
            PointerGestureRecognizer pointer => new Presses(this, pointer),
            _ => BridgePinch((PinchGestureRecognizer)maui),
        };
        _bridged.Add((maui, drawn));
        return drawn;
    }

    private SkUiPanGestureRecognizer BridgePan(IPanGestureController pan)
    {
        var gestureId = 0;
        return new SkUiPanGestureRecognizer
        {
            Handler = args =>
            {
                switch (args.Status)
                {
                    case GestureStatus.Started:
                        // As MAUI's platform code: one id per pan, from the recognizer type's counter.
                        gestureId = PanGestureRecognizer.CurrentId.Increment();
                        pan.SendPanStarted(view, gestureId);
                        break;
                    case GestureStatus.Running:
                        pan.SendPan(view, args.TotalX, args.TotalY, gestureId);
                        break;
                    case GestureStatus.Completed:
                        pan.SendPanCompleted(view, gestureId);
                        break;
                    default:
                        pan.SendPanCanceled(view, gestureId);
                        break;
                }
            },
        };
    }

    private SkUiPinchGestureRecognizer BridgePinch(IPinchGestureController pinch) => new()
    {
        Handler = args =>
        {
            switch (args.Status)
            {
                case GestureStatus.Started:
                    pinch.SendPinchStarted(view, ScaleOrigin(args.Origin));
                    break;
                case GestureStatus.Running:
                    pinch.SendPinch(view, args.Scale, ScaleOrigin(args.Origin));
                    break;
                case GestureStatus.Completed:
                    pinch.SendPinchEnded(view);
                    break;
                default:
                    pinch.SendPinchCanceled(view);
                    break;
            }
        },
    };

    /// <summary>MAUI's <c>ScaleOrigin</c>: the pointers' midpoint as a 0–1 fraction of the view's size.</summary>
    private Point ScaleOrigin(Point origin) =>
        new(view.Width > 0 ? origin.X / view.Width : 0.5, view.Height > 0 ? origin.Y / view.Height : 0.5);

    /// <summary>A hovering pointer moved over the view (or a descendant): <c>PointerEntered</c> first, then <c>PointerMoved</c>.</summary>
    public void Hovered(SkUiPointerRouter router, Point position)
    {
        var entered = !_hovering;
        _hovering = true;
        _hoverRouter = router;
        _hoverPosition = position;
        var getPosition = PositionsAt(router, position);
        foreach (var recognizer in view.GestureRecognizers.ToArray())
        {
            if (recognizer is not PointerGestureRecognizer pointer)
                continue;
            if (entered)
                Send(pointer, PointerEvent.Entered, getPosition, ButtonsMask.Primary);
            Send(pointer, PointerEvent.Moved, getPosition, ButtonsMask.Primary);
        }
    }

    /// <summary>The hovering pointer left the view (or the hover ended): <c>PointerExited</c> at its last position.</summary>
    public void HoverEnded()
    {
        if (!_hovering)
            return;
        _hovering = false;
        var getPosition = _hoverRouter is { } router ? PositionsAt(router, _hoverPosition) : null;
        _hoverRouter = null;
        foreach (var recognizer in view.GestureRecognizers.ToArray())
            if (recognizer is PointerGestureRecognizer pointer)
                Send(pointer, PointerEvent.Exited, getPosition, ButtonsMask.Primary);
    }

    /// <summary>
    /// MAUI's <c>PointerEventArgs.GetPosition</c>: relative to the view or any element drawn on the same surface; <c>null</c>
    /// for other elements and for the window (<c>GetPosition(null)</c>), which a drawn surface does not know.
    /// </summary>
    private static Func<IElement?, Point?> PositionsAt(SkUiPointerRouter router, Point rootPosition) =>
        element => element is not null && router.Contains(element) ? router.MapToNode(element, rootPosition) : null;

    private enum PointerEvent
    {
        Entered,
        Exited,
        Moved,
        Pressed,
        Released
    }

    private static bool? _canSendPointer;

    /// <summary>
    /// Raises <paramref name="kind"/> on <paramref name="recognizer"/> through MAUI's internal <c>Send*</c> (its command, then
    /// its event); on a MAUI version without it (probed once on a new recognizer, so no app code runs), the command only.
    /// </summary>
    private void Send(PointerGestureRecognizer recognizer, PointerEvent kind, Func<IElement?, Point?>? getPosition, ButtonsMask button)
    {
        if (_canSendPointer is null)
        {
            try
            {
                SendPointerMoved(new PointerGestureRecognizer(), view, null, null, ButtonsMask.Primary);
                _canSendPointer = true;
            }
            catch (MissingMethodException)
            {
                _canSendPointer = false;
            }
        }
        if (_canSendPointer == true)
        {
            switch (kind)
            {
                case PointerEvent.Entered: SendPointerEntered(recognizer, view, getPosition, null, button); break;
                case PointerEvent.Exited: SendPointerExited(recognizer, view, getPosition, null, button); break;
                case PointerEvent.Moved: SendPointerMoved(recognizer, view, getPosition, null, button); break;
                case PointerEvent.Pressed: SendPointerPressed(recognizer, view, getPosition, null, button); break;
                default: SendPointerReleased(recognizer, view, getPosition, null, button); break;
            }
            return;
        }
        var (command, parameter) = kind switch
        {
            PointerEvent.Entered => (recognizer.PointerEnteredCommand, recognizer.PointerEnteredCommandParameter),
            PointerEvent.Exited => (recognizer.PointerExitedCommand, recognizer.PointerExitedCommandParameter),
            PointerEvent.Moved => (recognizer.PointerMovedCommand, recognizer.PointerMovedCommandParameter),
            PointerEvent.Pressed => (recognizer.PointerPressedCommand, recognizer.PointerPressedCommandParameter),
            _ => (recognizer.PointerReleasedCommand, recognizer.PointerReleasedCommandParameter),
        };
        if (command?.CanExecute(parameter) == true)
            command.Execute(parameter);
    }

    // MAUI raises the pointer events only from these internal methods; the accessors are trimming- and Native-AOT-safe.
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "SendPointerEntered")]
    private static extern void SendPointerEntered(PointerGestureRecognizer recognizer, View sender, Func<IElement?, Point?>? getPosition,
        PlatformPointerEventArgs? platformArgs, ButtonsMask button);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "SendPointerExited")]
    private static extern void SendPointerExited(PointerGestureRecognizer recognizer, View sender, Func<IElement?, Point?>? getPosition,
        PlatformPointerEventArgs? platformArgs, ButtonsMask button);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "SendPointerMoved")]
    private static extern void SendPointerMoved(PointerGestureRecognizer recognizer, View sender, Func<IElement?, Point?>? getPosition,
        PlatformPointerEventArgs? platformArgs, ButtonsMask button);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "SendPointerPressed")]
    private static extern void SendPointerPressed(PointerGestureRecognizer recognizer, View sender, Func<IElement?, Point?>? getPosition,
        PlatformPointerEventArgs? platformArgs, ButtonsMask button);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "SendPointerReleased")]
    private static extern void SendPointerReleased(PointerGestureRecognizer recognizer, View sender, Func<IElement?, Point?>? getPosition,
        PlatformPointerEventArgs? platformArgs, ButtonsMask button);

    /// <summary>
    /// A pointer recognizer's presses: an arena member that observes and never claims, so the view's other gestures and its
    /// scrolling ancestors run as without it. <c>PointerReleased</c> follows on release, and also when another recognizer
    /// takes the pointer (a scroller's drag), so presses and releases pair up. Only buttons in <c>Buttons</c> count (a touch
    /// is the primary button).
    /// </summary>
    private sealed class Presses(SkUiMauiGestures bridge, PointerGestureRecognizer maui) : SkUiGestureRecognizer
    {
        private long? _pointer;
        private ButtonsMask _button;
        private SkUiPointer _last;

        protected internal override bool OnPointerPressed(SkUiPointer pointer)
        {
            var button = pointer.Buttons switch
            {
                SkUiPointerButtons.None or SkUiPointerButtons.Primary => ButtonsMask.Primary,
                SkUiPointerButtons.Secondary => ButtonsMask.Secondary,
                _ => (ButtonsMask)0
            };
            if (_pointer is not null || (maui.Buttons & button) == 0)
                return false;
            _pointer = pointer.Id;
            _button = button;
            _last = pointer;
            bridge.Send(maui, PointerEvent.Pressed, PositionsOf(pointer), button);
            return true;
        }

        protected internal override void OnPointerMoved(SkUiPointer pointer)
        {
            if (_pointer == pointer.Id)
                _last = pointer;
        }

        protected internal override void OnPointerReleased(SkUiPointer pointer)
        {
            if (_pointer != pointer.Id)
                return;
            _pointer = null;
            bridge.Send(maui, PointerEvent.Released, PositionsOf(pointer), _button);
        }

        protected internal override void OnRejected(long pointerId)
        {
            if (_pointer != pointerId)
                return;
            _pointer = null;
            bridge.Send(maui, PointerEvent.Released, PositionsOf(_last), _button);
        }

        private static Func<IElement?, Point?>? PositionsOf(SkUiPointer pointer) =>
            pointer.Router is { } router ? PositionsAt(router, pointer.Position) : null;
    }
}
