using System.Windows.Input;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// MAUI's gesture recognizers on drawn views (phase G in docs/archive/ImplementationPlan-1.2.md): a recognizer in a drawn
/// view's <c>GestureRecognizers</c> runs in the gesture arena and raises its MAUI events and commands, with MAUI's
/// argument types and the view as the sender.
/// </summary>
public class MauiGestureRecognizerTests
{
    private const double Size = 300;

    private static (SkUiContentView Root, SkUiBox Box) Tree()
    {
        var box = new SkUiBox();
        var root = new SkUiContentView { Content = box };
        SkUiTestHelpers.Arrange(root, Size, Size);
        return (root, box);
    }

    /// <summary>Press, move in steps 10 ms apart, release after <paramref name="holdMs"/> (a hold kills the release velocity).</summary>
    private static void Drag(SkUiView root, Point from, Point to, double startMs = 0, int steps = 10, double holdMs = 10)
    {
        root.Touch(new(1, SkUiTouchAction.Pressed, from, TimeSpan.FromMilliseconds(startMs)));
        for (var step = 1; step <= steps; step++)
        {
            var t = (double)step / steps;
            root.Touch(new(1, SkUiTouchAction.Moved, new Point(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t),
                TimeSpan.FromMilliseconds(startMs + step * 10)));
        }
        root.Touch(new(1, SkUiTouchAction.Released, to, TimeSpan.FromMilliseconds(startMs + steps * 10 + holdMs)));
    }

    [Fact]
    public void ASwipeRecognizerRaisesItsCommandThenSwipedInItsDirections()
    {
        var (root, box) = Tree();
        var events = new List<string>();
        var swipe = new SwipeGestureRecognizer
        {
            Direction = SwipeDirection.Left,
            CommandParameter = "row",
            Command = new TestCommand(parameter => events.Add($"command {parameter}")),
        };
        swipe.Swiped += (sender, args) => events.Add($"swiped {args.Direction} {args.Parameter} {ReferenceEquals(sender, box)}");
        box.GestureRecognizers.Add(swipe);

        Drag(root, new Point(250, 150), new Point(50, 150));
        Drag(root, new Point(50, 150), new Point(250, 150), startMs: 1000); // not an allowed direction
        Assert.Equal(["command row", "swiped Left row True"], events);

        // A slow drag shorter than the threshold is no swipe.
        swipe.Threshold = 250;
        Drag(root, new Point(250, 150), new Point(50, 150), startMs: 2000, holdMs: 500);
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public void ASwipeRecognizerWithoutDirectionsNeverSwipes()
    {
        var (root, box) = Tree();
        var swiped = 0;
        var swipe = new SwipeGestureRecognizer();
        swipe.Swiped += (_, _) => swiped++;
        box.GestureRecognizers.Add(swipe);

        Drag(root, new Point(250, 150), new Point(50, 150));
        Assert.Equal(0, swiped);
    }

    [Fact]
    public void APanRecognizerReportsMauisStatusesTotalsAndOneIdPerPan()
    {
        var (root, box) = Tree();
        var updates = new List<PanUpdatedEventArgs>();
        var pan = new PanGestureRecognizer();
        pan.PanUpdated += (sender, args) =>
        {
            Assert.Same(box, sender);
            updates.Add(args);
        };
        box.GestureRecognizers.Add(pan);

        Drag(root, new Point(100, 100), new Point(180, 140));
        Assert.Equal(GestureStatus.Started, updates[0].StatusType);
        Assert.Equal(GestureStatus.Completed, updates[^1].StatusType);
        Assert.All(updates[1..^1], update => Assert.Equal(GestureStatus.Running, update.StatusType));
        Assert.Equal((80d, 40d), (updates[^2].TotalX, updates[^2].TotalY));
        Assert.Single(updates.Select(update => update.GestureId).Distinct());

        var first = updates[0].GestureId;
        updates.Clear();
        Drag(root, new Point(100, 100), new Point(100, 200), startMs: 1000);
        Assert.NotEqual(first, updates[0].GestureId);
    }

    [Fact]
    public void APinchRecognizerReportsTheScaleChangeAndTheOriginAsAFraction()
    {
        var (root, box) = Tree();
        var updates = new List<PinchGestureUpdatedEventArgs>();
        var pinch = new PinchGestureRecognizer();
        pinch.PinchUpdated += (_, args) => updates.Add(args);
        box.GestureRecognizers.Add(pinch);

        // Two fingers around the center move apart from 100 to 200 DIPs.
        root.Touch(new(1, SkUiTouchAction.Pressed, new Point(100, 150), TimeSpan.Zero));
        root.Touch(new(2, SkUiTouchAction.Pressed, new Point(200, 150), TimeSpan.Zero));
        for (var step = 1; step <= 10; step++)
        {
            var time = TimeSpan.FromMilliseconds(step * 10);
            root.Touch(new(1, SkUiTouchAction.Moved, new Point(100 - step * 5, 150), time));
            root.Touch(new(2, SkUiTouchAction.Moved, new Point(200 + step * 5, 150), time));
        }
        root.Touch(new(1, SkUiTouchAction.Released, new Point(50, 150), TimeSpan.FromMilliseconds(200)));
        root.Touch(new(2, SkUiTouchAction.Released, new Point(250, 150), TimeSpan.FromMilliseconds(200)));

        Assert.Equal(GestureStatus.Started, updates[0].Status);
        Assert.Equal(GestureStatus.Completed, updates[^1].Status);
        var running = updates.Where(update => update.Status == GestureStatus.Running).ToList();
        Assert.Equal(2, running.Aggregate(1d, (scale, update) => scale * update.Scale), 3);
        Assert.Equal(0.5, running[^1].ScaleOrigin.X, 3);
        Assert.Equal(0.5, running[^1].ScaleOrigin.Y, 3);
    }

    [Fact]
    public void APanInsideAScrollerTakesTheDragAsTheInnermostRecognizer()
    {
        var box = new SkUiBox { HeightRequest = 200 };
        var column = new SkUiVerticalStackLayout { Children = { box, new SkUiBox { HeightRequest = 1000 } } };
        var scroll = new SkUiScrollView { Content = column };
        SkUiTestHelpers.Arrange(scroll, Size, Size);
        var updates = new List<GestureStatus>();
        var pan = new PanGestureRecognizer();
        pan.PanUpdated += (_, args) => updates.Add(args.StatusType);
        box.GestureRecognizers.Add(pan);

        Drag(scroll, new Point(150, 150), new Point(150, 50)); // on the pannable box
        Assert.Equal(GestureStatus.Completed, updates[^1]);
        Assert.Equal(0, scroll.ScrollY);

        updates.Clear();
        Drag(scroll, new Point(150, 280), new Point(150, 180), startMs: 1000); // below it: the scroller's
        Assert.Empty(updates);
        Assert.True(scroll.ScrollY > 0);
    }

    [Fact]
    public void ARemovedRecognizerStopsAtTheNextPress()
    {
        var (root, box) = Tree();
        var updates = 0;
        var pan = new PanGestureRecognizer();
        pan.PanUpdated += (_, _) => updates++;
        box.GestureRecognizers.Add(pan);
        Drag(root, new Point(100, 100), new Point(200, 100));
        Assert.True(updates > 0);

        updates = 0;
        box.GestureRecognizers.Remove(pan);
        Drag(root, new Point(100, 100), new Point(200, 100), startMs: 1000);
        Assert.Equal(0, updates);
    }

    private static void Click(SkUiView root, Point at, double ms, SkUiPointerButtons buttons = SkUiPointerButtons.None)
    {
        var device = buttons == SkUiPointerButtons.None ? SkUiPointerDevice.Touch : SkUiPointerDevice.Mouse;
        root.Touch(new SkUiTouchEvent(3, SkUiTouchAction.Pressed, at, TimeSpan.FromMilliseconds(ms), Device: device) { Buttons = buttons });
        root.Touch(new SkUiTouchEvent(3, SkUiTouchAction.Released, at, TimeSpan.FromMilliseconds(ms + 30), Device: device) { Buttons = buttons });
    }

    [Fact]
    public void ASecondaryButtonTapRecognizerTakesOnlySecondaryClicks()
    {
        var (root, box) = Tree();
        var events = new List<string>();
        root.GestureRecognizers.Add(new TapGestureRecognizer { Command = new TestCommand(_ => events.Add("parent")) });
        var menu = new TapGestureRecognizer { Buttons = ButtonsMask.Secondary };
        menu.Tapped += (_, args) => events.Add($"menu {args.Buttons}");
        box.GestureRecognizers.Add(menu);

        Click(root, new Point(20, 20), 0, SkUiPointerButtons.Primary);
        Click(root, new Point(20, 20), 1000, SkUiPointerButtons.Secondary);
        Click(root, new Point(20, 20), 2000); // a touch is a primary press
        Assert.Equal(["parent", "menu Secondary", "parent"], events);
    }

    [Fact]
    public void TapRecognizersRequiringMoreThanTwoTapsCountTheSequence()
    {
        using var clock = new ManualGestureClock();
        var (root, box) = Tree();
        var events = new List<string>();
        box.GestureRecognizers.Add(new TapGestureRecognizer { Command = new TestCommand(_ => events.Add("single")) });
        box.GestureRecognizers.Add(new TapGestureRecognizer { NumberOfTapsRequired = 3, Command = new TestCommand(_ => events.Add("triple")) });

        Click(root, new Point(20, 20), 0);
        Click(root, new Point(22, 20), 100);
        Click(root, new Point(20, 22), 200);
        Assert.Equal(["triple"], events); // at once: no recognizer wants more

        Click(root, new Point(20, 20), 2000);
        Assert.Equal(["triple"], events); // waits for a possible second tap
        clock.Advance(SkUiGestureSettings.DoubleTapTimeout);
        Assert.Equal(["triple", "single"], events);
    }

    [Fact]
    public void TapSequencesCountOneButtonAtATime()
    {
        using var clock = new ManualGestureClock();
        var (root, box) = Tree();
        var events = new List<string>();
        const ButtonsMask both = ButtonsMask.Primary | ButtonsMask.Secondary;
        box.GestureRecognizers.Add(new TapGestureRecognizer { Buttons = both, Command = new TestCommand(_ => events.Add("single")) });
        box.GestureRecognizers.Add(new TapGestureRecognizer { NumberOfTapsRequired = 2, Buttons = both, Command = new TestCommand(_ => events.Add("double")) });

        // A secondary click right after a primary one ends the primary's sequence and starts its own.
        Click(root, new Point(20, 20), 0, SkUiPointerButtons.Primary);
        Click(root, new Point(20, 20), 100, SkUiPointerButtons.Secondary);
        Assert.Equal(["single"], events);
        Click(root, new Point(20, 20), 200, SkUiPointerButtons.Secondary);
        Assert.Equal(["single", "double"], events);
        clock.Advance(SkUiGestureSettings.DoubleTapTimeout);
        Assert.Equal(2, events.Count);

        // So does a tap too far from the last one.
        Click(root, new Point(20, 20), 2000);
        Click(root, new Point(250, 250), 2100);
        Assert.Equal(["single", "double", "single"], events);
        clock.Advance(SkUiGestureSettings.DoubleTapTimeout);
        Assert.Equal(["single", "double", "single", "single"], events);
    }

    [Fact]
    public void APointerRecognizerOnAChildLeavesTheReleaseToTheParentsTap()
    {
        var (root, box) = Tree();
        var events = new List<string>();
        root.GestureRecognizers.Add(new TapGestureRecognizer { Command = new TestCommand(_ => events.Add("parent tapped")) });
        var pointer = new PointerGestureRecognizer();
        pointer.PointerPressed += (_, _) => events.Add("pressed");
        pointer.PointerReleased += (_, _) => events.Add("released");
        box.GestureRecognizers.Add(pointer);

        Click(root, new Point(20, 20), 0);
        Assert.Equal(["pressed", "released", "parent tapped"], events);
    }

    [Fact]
    public void ASpanTakesOnlyPressesOfButtonsItsRecognizersWant()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var events = new List<string>();
        var span = new Span { Text = "Tap here" };
        span.GestureRecognizers.Add(new TapGestureRecognizer { Command = new TestCommand(_ => events.Add("span")) });
        var label = new SkUiLabel
        {
            FontFamily = SkUiTestHelpers.BundledFontFamily,
            FormattedText = new FormattedString { Spans = { span } },
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start
        };
        var root = new SkUiContentView { Content = label };
        root.GestureRecognizers.Add(new TapGestureRecognizer { Buttons = ButtonsMask.Secondary, Command = new TestCommand(_ => events.Add("menu")) });
        SkUiTestHelpers.Arrange(root, Size, Size);

        // A right click on the primary-only span leaves the press to the parent's secondary recognizer.
        Click(root, new Point(10, 8), 0, SkUiPointerButtons.Secondary);
        Click(root, new Point(10, 8), 1000, SkUiPointerButtons.Primary);
        Assert.Equal(["menu", "span"], events);
    }

    [Fact]
    public void PointerAndTapRecognizersReadAButtonChordAlike()
    {
        var (root, box) = Tree();
        var events = new List<string>();
        box.GestureRecognizers.Add(new TapGestureRecognizer { Buttons = ButtonsMask.Secondary, Command = new TestCommand(_ => events.Add("tap")) });
        var pointer = new PointerGestureRecognizer { Buttons = ButtonsMask.Secondary };
        pointer.PointerPressed += (_, args) => events.Add($"pressed {args.Button}");
        pointer.PointerReleased += (_, args) => events.Add($"released {args.Button}");
        box.GestureRecognizers.Add(pointer);

        Click(root, new Point(20, 20), 0, SkUiPointerButtons.Primary | SkUiPointerButtons.Secondary);
        Assert.Equal(["pressed Secondary", "released Secondary", "tap"], events);
    }

    private static List<string> Record(PointerGestureRecognizer pointer, View relativeTo)
    {
        var events = new List<string>();
        void Add(string name, PointerEventArgs args) => events.Add($"{name} {args.GetPosition(relativeTo)} {args.Button}");
        pointer.PointerEntered += (_, args) => Add("entered", args);
        pointer.PointerMoved += (_, args) => Add("moved", args);
        pointer.PointerExited += (_, args) => Add("exited", args);
        pointer.PointerPressed += (_, args) => Add("pressed", args);
        pointer.PointerReleased += (_, args) => Add("released", args);
        return events;
    }

    [Fact]
    public void APointerRecognizerFollowsHoverWithPositionsRelativeToDrawnElements()
    {
        var box = new SkUiBox { WidthRequest = 100, HeightRequest = 100, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start, Margin = new Thickness(50, 0, 0, 0) };
        var root = new SkUiContentView { Content = box };
        SkUiTestHelpers.Arrange(root, Size, Size);
        var pointer = new PointerGestureRecognizer();
        box.GestureRecognizers.Add(pointer);
        var events = Record(pointer, box);
        string? inRoot = null;
        pointer.PointerMoved += (_, args) => inRoot ??= $"{args.GetPosition(root)} {args.GetPosition(null)}";

        root.Touch(new(0, SkUiTouchAction.HoverMoved, new Point(10, 10))); // beside the box
        root.Touch(new(0, SkUiTouchAction.HoverMoved, new Point(60, 20)));
        root.Touch(new(0, SkUiTouchAction.HoverMoved, new Point(70, 30)));
        root.Touch(new(0, SkUiTouchAction.HoverMoved, new Point(200, 30))); // left it
        Assert.Equal(
        [
            $"entered {new Point(10, 20)} Primary", $"moved {new Point(10, 20)} Primary", $"moved {new Point(20, 30)} Primary",
            $"exited {new Point(20, 30)} Primary",
        ], events);
        Assert.Equal($"{new Point(60, 20)} ", inRoot); // the window's coordinates are not known to a drawn surface
    }

    [Fact]
    public void APointerRecognizerReportsPressesOfItsButtonsWithoutTakingTheGesture()
    {
        var (root, box) = Tree();
        var taps = 0;
        box.Tapped += (_, _) => taps++;
        var pointer = new PointerGestureRecognizer();
        box.GestureRecognizers.Add(pointer);
        var events = Record(pointer, box);

        root.Touch(new(1, SkUiTouchAction.Pressed, new Point(20, 30), TimeSpan.Zero));
        root.Touch(new(1, SkUiTouchAction.Released, new Point(20, 30), TimeSpan.FromMilliseconds(50)));
        Assert.Equal([$"pressed {new Point(20, 30)} Primary", $"released {new Point(20, 30)} Primary"], events);
        Assert.Equal(1, taps); // the view's tap still runs

        // A secondary click counts only when Buttons has it.
        events.Clear();
        SecondaryClick(root, 1000);
        Assert.Empty(events);
        pointer.Buttons = ButtonsMask.Primary | ButtonsMask.Secondary;
        SecondaryClick(root, 2000);
        Assert.Equal([$"pressed {new Point(40, 40)} Secondary", $"released {new Point(40, 40)} Secondary"], events);

        static void SecondaryClick(SkUiView root, double ms)
        {
            root.Touch(new SkUiTouchEvent(2, SkUiTouchAction.Pressed, new Point(40, 40), TimeSpan.FromMilliseconds(ms), Device: SkUiPointerDevice.Mouse) { Buttons = SkUiPointerButtons.Secondary });
            root.Touch(new SkUiTouchEvent(2, SkUiTouchAction.Released, new Point(40, 40), TimeSpan.FromMilliseconds(ms + 50), Device: SkUiPointerDevice.Mouse) { Buttons = SkUiPointerButtons.Secondary });
        }
    }

    [Fact]
    public void APointerRecognizerInAScrollerIsReleasedWhenTheScrollerTakesTheDrag()
    {
        var box = new SkUiBox { HeightRequest = 200 };
        var scroll = new SkUiScrollView { Content = new SkUiVerticalStackLayout { Children = { box, new SkUiBox { HeightRequest = 1000 } } } };
        SkUiTestHelpers.Arrange(scroll, Size, Size);
        var pointer = new PointerGestureRecognizer();
        box.GestureRecognizers.Add(pointer);
        var events = Record(pointer, box);

        Drag(scroll, new Point(150, 150), new Point(150, 50));
        Assert.True(scroll.ScrollY > 0);
        Assert.Equal(2, events.Count);
        Assert.StartsWith("pressed", events[0]);
        Assert.StartsWith("released", events[1]);
    }

    private sealed class TestCommand(Action<object?> execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute(parameter);
    }
}
