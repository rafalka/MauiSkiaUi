using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>Gesture arena: nested scrolling, press feedback, tap variants, pans, swipes, pinch, native coordination.</summary>
public class GestureTests
{
    private static SkUiBox Box(double width, double height) =>
        new() { WidthRequest = width, HeightRequest = height, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };

    /// <summary>Press, move in steps, release — 10 ms per sample.</summary>
    private static void Drag(ISkUiView root, Point from, Point to, long id = 1, int steps = 10, double startMs = 0)
    {
        root.Touch(new(id, SkUiTouchAction.Pressed, from, TimeSpan.FromMilliseconds(startMs)));
        for (var step = 1; step <= steps; step++)
        {
            var t = (double)step / steps;
            root.Touch(new(id, SkUiTouchAction.Moved, new Point(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t),
                TimeSpan.FromMilliseconds(startMs + step * 10)));
        }
        root.Touch(new(id, SkUiTouchAction.Released, to, TimeSpan.FromMilliseconds(startMs + steps * 10 + 500)));
    }

    private static (SkUiScrollView Outer, SkUiScrollView Inner) Carousel()
    {
        var row = new SkUiHorizontalStackLayout();
        for (var i = 0; i < 10; i++) row.Children.Add(Box(100, 50));
        var inner = new SkUiScrollView { Orientation = ScrollOrientation.Horizontal, HeightRequest = 50, Content = row };
        var column = new SkUiVerticalStackLayout();
        column.Children.Add(inner);
        for (var i = 0; i < 20; i++) column.Children.Add(Box(300, 50));
        var outer = new SkUiScrollView { Content = column };
        SkUiTestHelpers.Arrange(outer, 300, 400);
        return (outer, inner);
    }

    [Fact]
    public void NestedOrthogonalScrollersEachTakeTheirOwnAxis()
    {
        var (outer, inner) = Carousel();
        Drag(outer, new Point(200, 25), new Point(50, 25));
        Assert.Equal(150, inner.ScrollX);
        Assert.Equal(0, outer.ScrollY);

        Drag(outer, new Point(200, 25), new Point(200, -125));
        Assert.Equal(150, inner.ScrollX);
        Assert.Equal(150, outer.ScrollY);
    }

    [Fact]
    public void SameAxisInnerScrollsFirstThenChainsToOuterAtItsEdge()
    {
        var innerContent = new SkUiVerticalStackLayout();
        for (var i = 0; i < 4; i++) innerContent.Children.Add(Box(300, 50));
        var inner = new SkUiScrollView { HeightRequest = 100, Content = innerContent }; // extent 200, max 100
        var column = new SkUiVerticalStackLayout();
        column.Children.Add(inner);
        for (var i = 0; i < 20; i++) column.Children.Add(Box(300, 50));
        var outer = new SkUiScrollView { Content = column };
        SkUiTestHelpers.Arrange(outer, 300, 400);

        // 150 up: 100 scroll the inner one to its end, the remaining 50 chain to the outer one.
        Drag(outer, new Point(150, 90), new Point(150, -60));
        Assert.Equal(100, inner.ScrollY);
        Assert.Equal(50, outer.ScrollY);

        // Inner at its end: a new upward drag starting on it goes straight to the outer scroller.
        outer.ScrollTo(0, 0);
        Drag(outer, new Point(150, 50), new Point(150, 20));
        Assert.Equal(100, inner.ScrollY);
        Assert.Equal(30, outer.ScrollY);

        // Downward drag: the inner scroller can move back, so it takes it.
        Drag(outer, new Point(150, 20), new Point(150, 60));
        Assert.Equal(60, inner.ScrollY);
        Assert.Equal(30, outer.ScrollY);
    }

    [Fact]
    public void FlingGoesToTheInnermostScrollerThatCanMoveThatWay()
    {
        var (outer, inner) = Carousel();
        using var surface = new SkUiTestSurface(outer, 300, 400);
        surface.Frame(0);
        inner.ScrollTo(700, 0); // at its end
        outer.Touch(new(1, SkUiTouchAction.Pressed, new Point(250, 25), TimeSpan.Zero));
        outer.Touch(new(1, SkUiTouchAction.Moved, new Point(150, 25), TimeSpan.FromMilliseconds(20)));
        outer.Touch(new(1, SkUiTouchAction.Released, new Point(150, 25), TimeSpan.FromMilliseconds(25)));
        Assert.False(inner.IsMotionRunning); // cannot move further right; no same-axis outer scroller
        Assert.Equal(0, outer.ScrollY);
        inner.ScrollTo(300, 0);
        outer.Touch(new(2, SkUiTouchAction.Pressed, new Point(250, 25), TimeSpan.FromMilliseconds(100)));
        outer.Touch(new(2, SkUiTouchAction.Moved, new Point(150, 25), TimeSpan.FromMilliseconds(120)));
        outer.Touch(new(2, SkUiTouchAction.Released, new Point(150, 25), TimeSpan.FromMilliseconds(125)));
        Assert.True(inner.IsMotionRunning);
    }

    [Fact]
    public void ContestedPressShowsAfterDelayAndScrollingCancelsIt()
    {
        using var clock = new ManualGestureClock();
        var clicks = 0;
        var button = new SkUiButton { Text = "B", HeightRequest = 40 };
        button.Clicked += (_, _) => clicks++;
        var column = new SkUiVerticalStackLayout();
        column.Children.Add(button);
        column.Children.Add(Box(300, 1000));
        var scroll = new SkUiScrollView { Content = column };
        SkUiTestHelpers.Arrange(scroll, 300, 400);

        scroll.Touch(new(1, SkUiTouchAction.Pressed, new Point(50, 20), TimeSpan.Zero));
        Assert.False(button.IsPressed);
        clock.Advance(SkUiGestureSettings.PressDelay);
        Assert.True(button.IsPressed); // held without moving: feedback while the finger is down
        scroll.Touch(new(1, SkUiTouchAction.Moved, new Point(50, -30), TimeSpan.FromMilliseconds(150)));
        Assert.False(button.IsPressed);
        scroll.Touch(new(1, SkUiTouchAction.Released, new Point(50, -30), TimeSpan.FromMilliseconds(400)));
        Assert.Equal(0, clicks);
        Assert.Equal(50, scroll.ScrollY);
    }

    [Fact]
    public void ControlsInsideScrollersCanStillDrag()
    {
        var pans = new List<GestureStatus>();
        var slider = Box(300, 40);
        slider.PanAxis = SkUiPanAxis.Horizontal;
        slider.PanUpdated += (_, args) => pans.Add(args.Status);
        var column = new SkUiVerticalStackLayout();
        column.Children.Add(slider);
        column.Children.Add(Box(300, 1000));
        var scroll = new SkUiScrollView { Content = column };
        SkUiTestHelpers.Arrange(scroll, 300, 400);

        Drag(scroll, new Point(20, 20), new Point(200, 20));
        Assert.Equal(GestureStatus.Started, pans[0]);
        Assert.Equal(GestureStatus.Completed, pans[^1]);
        Assert.Equal(0, scroll.ScrollY);

        pans.Clear();
        Drag(scroll, new Point(20, 20), new Point(20, -100));
        Assert.Empty(pans);
        Assert.Equal(120, scroll.ScrollY);
    }

    [Fact]
    public void DoubleTapDelaysSingleTapAndLongPressClaims()
    {
        using var clock = new ManualGestureClock();
        var box = Box(100, 100);
        var single = 0;
        var doubles = 0;
        var longs = 0;
        box.Tapped += (_, _) => single++;
        box.DoubleTapped += (_, args) => { doubles++; Assert.Equal(2, args.NumberOfTaps); };
        box.LongPressed += (_, _) => longs++;
        var root = new SkUiContentView { Content = box };
        SkUiTestHelpers.Arrange(root, 200, 200);

        Tap(root, new Point(10, 10), 0);
        Tap(root, new Point(12, 12), 150);
        Assert.Equal(1, doubles);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(0, single);

        Tap(root, new Point(10, 10), 2000);
        Assert.Equal(0, single);
        clock.Advance(SkUiGestureSettings.DoubleTapTimeout);
        Assert.Equal(1, single);

        root.Touch(new(9, SkUiTouchAction.Pressed, new Point(10, 10), TimeSpan.FromSeconds(5)));
        clock.Advance(SkUiGestureSettings.LongPressDuration);
        Assert.Equal(1, longs);
        root.Touch(new(9, SkUiTouchAction.Released, new Point(10, 10), TimeSpan.FromSeconds(6)));
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(1, single); // the long press consumed that interaction
    }

    [Fact]
    public void HorizontalSwipeOnARowInsideAVerticalScroller()
    {
        var swipes = new List<SwipeDirection>();
        var row = Box(300, 50);
        row.SwipeDirections = SwipeDirection.Left | SwipeDirection.Right;
        row.Swiped += (_, args) => swipes.Add(args.Direction);
        var column = new SkUiVerticalStackLayout();
        column.Children.Add(row);
        column.Children.Add(Box(300, 1000));
        var scroll = new SkUiScrollView { Content = column };
        SkUiTestHelpers.Arrange(scroll, 300, 400);

        Drag(scroll, new Point(250, 25), new Point(50, 25));
        Assert.Equal([SwipeDirection.Left], swipes);
        Assert.Equal(0, scroll.ScrollY);

        Drag(scroll, new Point(150, 25), new Point(150, -75));
        Assert.Single(swipes);
        Assert.Equal(100, scroll.ScrollY);
    }

    [Fact]
    public void PinchReportsScaleAndBeatsTheScroller()
    {
        var scales = new List<double>();
        var photo = Box(300, 300);
        photo.PinchUpdated += (_, args) => { if (args.Status == GestureStatus.Running) scales.Add(args.TotalScale); };
        var column = new SkUiVerticalStackLayout();
        column.Children.Add(photo);
        column.Children.Add(Box(300, 1000));
        var scroll = new SkUiScrollView { Content = column };
        SkUiTestHelpers.Arrange(scroll, 300, 400);

        scroll.Touch(new(1, SkUiTouchAction.Pressed, new Point(100, 150), TimeSpan.Zero));
        scroll.Touch(new(2, SkUiTouchAction.Pressed, new Point(200, 150), TimeSpan.FromMilliseconds(5)));
        scroll.Touch(new(1, SkUiTouchAction.Moved, new Point(50, 150), TimeSpan.FromMilliseconds(20)));
        scroll.Touch(new(2, SkUiTouchAction.Moved, new Point(250, 150), TimeSpan.FromMilliseconds(25)));
        scroll.Touch(new(1, SkUiTouchAction.Released, new Point(50, 150), TimeSpan.FromMilliseconds(40)));
        scroll.Touch(new(2, SkUiTouchAction.Released, new Point(250, 150), TimeSpan.FromMilliseconds(45)));

        Assert.Equal(2, scales[^1], 3);
        Assert.Equal(0, scroll.ScrollY);
    }

    [Fact]
    public void TwoPointersPressTwoButtonsIndependently()
    {
        var a = new SkUiButton { Text = "A", WidthRequest = 100, HeightRequest = 40, HorizontalOptions = LayoutOptions.Start };
        var b = new SkUiButton { Text = "B", WidthRequest = 100, HeightRequest = 40, HorizontalOptions = LayoutOptions.Start };
        var clicks = new List<string>();
        a.Clicked += (_, _) => clicks.Add("A");
        b.Clicked += (_, _) => clicks.Add("B");
        var row = new SkUiHorizontalStackLayout { Spacing = 10 };
        row.Children.Add(a);
        row.Children.Add(b);
        SkUiTestHelpers.Arrange(row, 300, 40);

        row.Touch(new(1, SkUiTouchAction.Pressed, new Point(20, 20)));
        row.Touch(new(2, SkUiTouchAction.Pressed, new Point(150, 20)));
        Assert.True(a.IsPressed && b.IsPressed);
        row.Touch(new(2, SkUiTouchAction.Released, new Point(150, 20)));
        row.Touch(new(1, SkUiTouchAction.Released, new Point(20, 20)));
        Assert.Equal(["B", "A"], clicks);
    }

    [Fact]
    public void PointerRecognizerReceivesTheWholeGesture()
    {
        var samples = new List<SkUiTouchAction>();
        var canvas = Box(300, 300);
        var pointer = new SkUiPointerGestureRecognizer();
        pointer.Pointer += (_, args) => samples.Add(args.Action);
        canvas.Gestures.Add(pointer);
        var column = new SkUiVerticalStackLayout();
        column.Children.Add(canvas);
        column.Children.Add(Box(300, 1000));
        var scroll = new SkUiScrollView { Content = column };
        SkUiTestHelpers.Arrange(scroll, 300, 400);

        Drag(scroll, new Point(100, 250), new Point(100, 50), steps: 3);
        Assert.Equal([SkUiTouchAction.Pressed, SkUiTouchAction.Moved, SkUiTouchAction.Moved, SkUiTouchAction.Moved, SkUiTouchAction.Released], samples);
        Assert.Equal(0, scroll.ScrollY);
    }

    [Fact]
    public void NativeStateReflectsDrawnContinuousGestures()
    {
        var (outer, _) = Carousel();
        var router = outer.Router;
        outer.Touch(new(1, SkUiTouchAction.Pressed, new Point(200, 200), TimeSpan.Zero));
        Assert.Equal(SkUiNativeGestureState.Pending, router.NativeState);
        outer.Touch(new(1, SkUiTouchAction.Moved, new Point(200, 150), TimeSpan.FromMilliseconds(10)));
        Assert.Equal(SkUiNativeGestureState.Claimed, router.NativeState);
        outer.Touch(new(1, SkUiTouchAction.Released, new Point(200, 150), TimeSpan.FromMilliseconds(500)));
        Assert.Equal(SkUiNativeGestureState.None, router.NativeState);

        // At the top, pulling down cannot scroll: once past the slop the native parent may take over.
        outer.ScrollTo(0, 0);
        outer.Touch(new(2, SkUiTouchAction.Pressed, new Point(200, 200), TimeSpan.FromSeconds(1)));
        outer.Touch(new(2, SkUiTouchAction.Moved, new Point(200, 260), TimeSpan.FromSeconds(1.01)));
        Assert.Equal(SkUiNativeGestureState.None, router.NativeState);
        outer.Touch(new(2, SkUiTouchAction.Released, new Point(200, 260), TimeSpan.FromSeconds(1.5)));

        var button = new SkUiButton { Text = "Only taps" };
        SkUiTestHelpers.Arrange(button, 100, 40);
        button.Touch(new(3, SkUiTouchAction.Pressed, new Point(10, 10)));
        Assert.Equal(SkUiNativeGestureState.None, button.Router.NativeState);
    }

    [Fact]
    public void WheelScrollsInnermostScrollerThatCanMove()
    {
        var innerContent = new SkUiVerticalStackLayout();
        innerContent.Children.Add(Box(300, 200));
        var inner = new SkUiScrollView { HeightRequest = 100, Content = innerContent };
        var column = new SkUiVerticalStackLayout();
        column.Children.Add(inner);
        column.Children.Add(Box(300, 1000));
        var outer = new SkUiScrollView { Content = column };
        SkUiTestHelpers.Arrange(outer, 300, 400);

        outer.Touch(new(0, SkUiTouchAction.Wheel, new Point(50, 50), WheelDelta: -80));
        Assert.Equal(80, inner.ScrollY);
        outer.Touch(new(0, SkUiTouchAction.Wheel, new Point(50, 50), WheelDelta: -80));
        Assert.Equal(100, inner.ScrollY);
        Assert.Equal(0, outer.ScrollY);
        outer.Touch(new(0, SkUiTouchAction.Wheel, new Point(50, 50), WheelDelta: -80));
        Assert.Equal(100, inner.ScrollY);
        Assert.True(outer.ScrollY > 0);
    }

    [Fact]
    public void CoreScrollViewScrollsTapsAndNestsInsideSkUiScrollView()
    {
        var clicks = 0;
        var button = new SkUiCoreButton();
        button.SetText("Go").SetHeight(40);
        button.Clicked += (_, _) => clicks++;
        var stack = new SkUiCoreVerticalStackLayout().Add(button);
        for (var i = 0; i < 10; i++)
            stack.Add(new SkUiCoreBox().SetHeight(50));
        var coreScroll = new SkUiCoreScrollView();
        coreScroll.SetContent(stack);
        coreScroll.SetHeight(200);
        var host = new SkUiCoreHost().SetContent(coreScroll);
        var column = new SkUiVerticalStackLayout();
        column.Children.Add(host);
        column.Children.Add(Box(300, 1000));
        var outer = new SkUiScrollView { Content = column };
        SkUiTestHelpers.Arrange(outer, 300, 400);

        Tap(outer, new Point(20, 20), 0);
        Assert.Equal(1, clicks);

        Drag(outer, new Point(150, 150), new Point(150, 50), startMs: 1000);
        Assert.Equal(100, coreScroll.ScrollY);
        Assert.Equal(0, outer.ScrollY);
        Assert.Equal(new Rect(0, -100, 300, 40), SkUiDiagnostics.GetRootBounds(button));

        // Core scroller at its end (extent 540, viewport 200): further upward drags chain to the outer scroller.
        Drag(outer, new Point(150, 190), new Point(150, -110), startMs: 3000);
        Assert.Equal(340, coreScroll.ScrollY);
        Assert.Equal(60, outer.ScrollY);
    }

    [Fact]
    public void CoreGestureEventsAndCustomRecognizers()
    {
        var taps = 0;
        var box = new SkUiCoreBox().SetWidth(100).SetHeight(100);
        box.Tapped += (_, _) => taps++;
        var pans = 0;
        var draggable = new SkUiCoreBox().SetWidth(100).SetHeight(100);
        var pan = new SkUiPanGestureRecognizer();
        pan.PanUpdated += (_, args) => { if (args.Status == GestureStatus.Completed) pans++; };
        draggable.AddGestureRecognizer(pan);
        var host = new SkUiCoreHost().SetContent(new SkUiCoreHorizontalStackLayout().Add(box).Add(draggable));
        SkUiTestHelpers.Arrange(host, 200, 100);

        Tap(host, new Point(50, 50), 0);
        Drag(host, new Point(150, 50), new Point(190, 90), startMs: 1000);
        Assert.Equal(1, taps);
        Assert.Equal(1, pans);
    }

    [Fact]
    public void MauiTapRecognizersRunOnDrawnTapsAndActivation()
    {
        var box = Box(100, 100);
        SemanticProperties.SetDescription(box, "Card");
        var order = new List<string>();
        var recognizer = new TapGestureRecognizer { Command = new Command<object?>(parameter => order.Add($"command {parameter}")), CommandParameter = "p" };
        recognizer.Tapped += (sender, args) =>
        {
            Assert.Same(box, sender);
            Assert.Equal("p", args.Parameter);
            Assert.Equal(ButtonsMask.Primary, args.Buttons);
            order.Add($"recognizer {args.GetPosition(box)}");
        };
        box.Tapped += (_, _) => order.Add("event");
        box.GestureRecognizers.Add(recognizer);
        var root = new SkUiContentView { Content = box };
        SkUiTestHelpers.Arrange(root, 200, 200);

        Tap(root, new Point(30, 40), 0);
        Assert.Equal(["event", "command p", $"recognizer {new Point(30, 40)}"], order);

        // Screen readers and the keyboard run the same tap.
        order.Clear();
        var owner = new SkUiSemanticsOwner(root);
        Assert.True(owner.Perform(owner.Tree.Find(box)!.Id, SkUiSemanticsActions.Activate));
        Assert.Equal(["event", "command p", $"recognizer {new Point(50, 50)}"], order);
    }

    [Fact]
    public void MauiTapRecognizersTakeTapsFromWhatIsUnderneathAndCanChangeAnyTime()
    {
        var outerTaps = 0;
        var box = Box(100, 100);
        var root = new SkUiContentView { Content = box };
        root.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => outerTaps++) });
        SkUiTestHelpers.Arrange(root, 200, 200);

        Tap(root, new Point(10, 10), 0);
        Assert.Equal(1, outerTaps); // the passive box leaves the tap to its parent's recognizer

        box.GestureRecognizers.Add(new TapGestureRecognizer { Buttons = ButtonsMask.Secondary, Command = new Command(() => throw new InvalidOperationException()) });
        Tap(root, new Point(10, 10), 1000);
        Assert.Equal(2, outerTaps); // drawn taps are primary-button taps

        // As on MAUI, a recognizer without a command that can execute still takes the tap: the innermost one wins.
        var blocker = new TapGestureRecognizer { Command = new Command(() => throw new InvalidOperationException(), () => false) };
        box.GestureRecognizers.Add(blocker);
        Tap(root, new Point(10, 10), 2000);
        Assert.Equal(2, outerTaps);

        box.GestureRecognizers.Remove(blocker);
        Tap(root, new Point(10, 10), 3000);
        Assert.Equal(3, outerTaps);
    }

    [Fact]
    public void MauiDoubleTapRecognizersDelaySingleTaps()
    {
        using var clock = new ManualGestureClock();
        var box = Box(100, 100);
        var singles = 0;
        var doubles = 0;
        box.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => singles++) });
        box.GestureRecognizers.Add(new TapGestureRecognizer { NumberOfTapsRequired = 2, Command = new Command(() => doubles++) });
        var root = new SkUiContentView { Content = box };
        SkUiTestHelpers.Arrange(root, 200, 200);

        Tap(root, new Point(10, 10), 0);
        Tap(root, new Point(12, 12), 150);
        Assert.Equal(1, doubles);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(0, singles);

        Tap(root, new Point(10, 10), 2000);
        Assert.Equal(0, singles);
        clock.Advance(SkUiGestureSettings.DoubleTapTimeout);
        Assert.Equal(1, singles);
    }

    [Fact]
    public void MauiGestureInputDrawnViewsDoNotRunIsReportedOncePerView()
    {
        var box = Box(100, 100);
        box.AutomationId = "unsupported-gestures";
        box.GestureRecognizers.Add(new TapGestureRecognizer());
        box.GestureRecognizers.Add(new TapGestureRecognizer { NumberOfTapsRequired = 3 });
        box.GestureRecognizers.Add(new PanGestureRecognizer());
        box.Behaviors.Add(new TestPlatformBehavior());
        var root = new SkUiContentView { Content = box };
        SkUiTestHelpers.Arrange(root, 200, 200);
        var listener = new RecordingTraceListener("'unsupported-gestures'");
        System.Diagnostics.Trace.Listeners.Add(listener);
        try
        {
            Tap(root, new Point(10, 10), 0);
            Tap(root, new Point(10, 10), 1000);
        }
        finally
        {
            System.Diagnostics.Trace.Listeners.Remove(listener);
        }

        Assert.Collection(listener.Messages,
            message => Assert.StartsWith("SkiaUi: TapGestureRecognizer on SkUiBox 'unsupported-gestures' is not run", message),
            message => Assert.StartsWith("SkiaUi: PanGestureRecognizer on SkUiBox 'unsupported-gestures' is not run", message),
            message => Assert.StartsWith("SkiaUi: TestPlatformBehavior on SkUiBox 'unsupported-gestures' is not run", message));
    }

    private sealed class TestPlatformBehavior : PlatformBehavior<View>;

    private sealed class RecordingTraceListener(string filter) : System.Diagnostics.TraceListener
    {
        public List<string> Messages { get; } = [];

        public override void Write(string? message) { }

        public override void WriteLine(string? message)
        {
            if (message?.Contains(filter) == true)
                lock (Messages)
                    Messages.Add(message);
        }
    }

    private static void Tap(ISkUiView root, Point point, double ms)
    {
        Assert.True(root.Touch(new(100 + (long)ms, SkUiTouchAction.Pressed, point, TimeSpan.FromMilliseconds(ms))));
        Assert.True(root.Touch(new(100 + (long)ms, SkUiTouchAction.Released, point, TimeSpan.FromMilliseconds(ms + 30))));
    }
}

/// <summary>Deterministic gesture timers for the current test thread.</summary>
internal sealed class ManualGestureClock : IDisposable
{
    private readonly List<(TimeSpan Due, Action Action, Handle Handle)> _timers = [];
    private TimeSpan _now;

    public ManualGestureClock() => SkUiGestureSettings.Scheduler = Schedule;

    private IDisposable Schedule(TimeSpan delay, Action action)
    {
        var handle = new Handle();
        _timers.Add((_now + delay, action, handle));
        return handle;
    }

    public void Advance(TimeSpan by)
    {
        _now += by;
        while (_timers.FindIndex(timer => timer.Due <= _now) is var index and >= 0)
        {
            var timer = _timers[index];
            _timers.RemoveAt(index);
            if (!timer.Handle.Cancelled)
                timer.Action();
        }
    }

    public void Dispose() => SkUiGestureSettings.Scheduler = null;

    private sealed class Handle : IDisposable
    {
        public bool Cancelled { get; private set; }
        public void Dispose() => Cancelled = true;
    }
}
