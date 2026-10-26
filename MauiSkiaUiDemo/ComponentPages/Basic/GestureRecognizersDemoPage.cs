using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// MAUI's gesture recognizers on a drawn view next to the same recognizer on a native one: the drawn view runs them in the
/// gesture arena, the native one through MAUI's platform code. Each side shows the last event it raised.
/// </summary>
public sealed class GestureRecognizersDemoPage : ComponentDemoPage
{
    private const string Waiting = "Try the gesture on each side";

    /// <summary>The recognizer both views get.</summary>
    public enum Recognizer
    {
        Tap,
        DoubleTap,
        TripleTap,
        SecondaryTap,
        Swipe,
        Pan,
        Pinch,
        Pointer
    }

    private string _skiaEvent = Waiting;
    private string _nativeEvent = Waiting;

    public GestureRecognizersDemoPage() : base(nameof(SkUiBox), new SkUiBox { Color = Accent, CornerRadius = 12 }, new BoxView { Color = Accent, CornerRadius = 12 })
    {
        var skia = (SkUiBox)SkiaControl;
        var native = (BoxView)NativeControl!;
        Choice(nameof(Recognizer), Enum.GetValues<Recognizer>(), Recognizer.Tap, value =>
        {
            Attach(skia, value, text => _skiaEvent = text);
            Attach(native, value, text => _nativeEvent = text);
            _skiaEvent = _nativeEvent = Waiting;
            Show();
        }, () => Kind(skia), () => Kind(native));
    }

    private static Recognizer Kind(View view) => view.GestureRecognizers.FirstOrDefault() switch
    {
        TapGestureRecognizer { Buttons: ButtonsMask.Secondary } => Recognizer.SecondaryTap,
        TapGestureRecognizer { NumberOfTapsRequired: 3 } => Recognizer.TripleTap,
        TapGestureRecognizer { NumberOfTapsRequired: 2 } => Recognizer.DoubleTap,
        SwipeGestureRecognizer => Recognizer.Swipe,
        PanGestureRecognizer => Recognizer.Pan,
        PinchGestureRecognizer => Recognizer.Pinch,
        PointerGestureRecognizer => Recognizer.Pointer,
        _ => Recognizer.Tap
    };

    /// <summary>Replaces <paramref name="view"/>'s recognizers with one of <paramref name="kind"/> that reports to <paramref name="report"/>.</summary>
    private void Attach(View view, Recognizer kind, Action<string> report)
    {
        void Raise(string text)
        {
            report(text);
            Show();
        }

        view.GestureRecognizers.Clear();
        IGestureRecognizer recognizer;
        switch (kind)
        {
            case Recognizer.Pan:
                var pan = new PanGestureRecognizer();
                pan.PanUpdated += (_, args) => Raise($"Pan {args.StatusType} {args.TotalX:F0}, {args.TotalY:F0}");
                recognizer = pan;
                break;
            case Recognizer.Pinch:
                var pinch = new PinchGestureRecognizer();
                pinch.PinchUpdated += (_, args) => Raise($"Pinch {args.Status} ×{args.Scale:F2} at {args.ScaleOrigin.X:F2}, {args.ScaleOrigin.Y:F2}");
                recognizer = pinch;
                break;
            case Recognizer.Swipe:
                var swipe = new SwipeGestureRecognizer { Direction = SwipeDirection.Left | SwipeDirection.Right | SwipeDirection.Up | SwipeDirection.Down };
                swipe.Swiped += (_, args) => Raise($"Swiped {args.Direction}");
                recognizer = swipe;
                break;
            case Recognizer.Pointer:
                var pointer = new PointerGestureRecognizer { Buttons = ButtonsMask.Primary | ButtonsMask.Secondary };
                void Pointer(string name, PointerEventArgs args) => Raise($"Pointer {name} {Format(args.GetPosition(view))} {args.Button}");
                pointer.PointerEntered += (_, args) => Pointer("entered", args);
                pointer.PointerMoved += (_, args) => Pointer("moved", args);
                pointer.PointerExited += (_, args) => Pointer("exited", args);
                pointer.PointerPressed += (_, args) => Pointer("pressed", args);
                pointer.PointerReleased += (_, args) => Pointer("released", args);
                recognizer = pointer;
                break;
            default:
                var tap = new TapGestureRecognizer
                {
                    NumberOfTapsRequired = kind switch { Recognizer.DoubleTap => 2, Recognizer.TripleTap => 3, _ => 1 },
                    Buttons = kind == Recognizer.SecondaryTap ? ButtonsMask.Secondary : ButtonsMask.Primary
                };
                var count = 0;
                tap.Tapped += (_, args) => Raise($"{kind} {++count} at {Format(args.GetPosition(view))}");
                recognizer = tap;
                break;
        }
        view.GestureRecognizers.Add(recognizer);
    }

    private static string Format(Point? point) => point is { } value ? $"{value.X:F0}, {value.Y:F0}" : "-";

    private void Show() => Feedback(_skiaEvent, _nativeEvent);
}
