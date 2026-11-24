using System.Collections.ObjectModel;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// 1.17 C4 <see cref="SkUiCarouselView.AutoPlayInterval"/> on the manual gesture clock: one step per interval, back to the
/// first item at the end of a carousel that does not loop, a page with several items per snap point; the waits (a drag and
/// the rest after it, hover, keyboard focus inside, the app's pause, hidden itself or through an ancestor, removed from the
/// tree, a screen reader running, the app in the background), and reduce motion jumping. In the global-state collection:
/// one test turns reduce motion on.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public class CarouselAutoPlayTests
{
    private const double Interval = 1000;
    private static long _pointer = 230_000;

    private static ObservableCollection<string> Items(int count) => [.. Enumerable.Range(0, count).Select(index => $"Item {index}")];

    private static SkUiCarouselView Carousel(int count = 3, bool loop = false) => new()
    {
        ItemsSource = Items(count),
        Loop = loop,
        AutoPlayInterval = TimeSpan.FromMilliseconds(Interval),
        ItemTemplate = new DataTemplate(() => new SkUiButton { Text = "Card" })
    };

    /// <summary>Advances the clock and renders a frame every 16 ms for <paramref name="milliseconds"/>.</summary>
    private sealed class Player(ManualGestureClock clock, SkUiTestSurface surface)
    {
        public double Now { get; private set; }

        public void Play(double milliseconds)
        {
            for (var end = Now + milliseconds; Now < end - 1e-9;)
            {
                Now += 16;
                clock.Advance(TimeSpan.FromMilliseconds(16));
                surface.Frame(Now);
            }
        }

        /// <summary>Plays until <paramref name="done"/> or <paramref name="limit"/> ms have passed; returns whether it got there.</summary>
        public bool PlayUntil(Func<bool> done, double limit = 10_000)
        {
            for (var end = Now + limit; Now < end; Play(16))
                if (done())
                    return true;
            return done();
        }
    }

    [Fact]
    public void ItStepsOncePerIntervalAfterRestingAndGoesBackToTheFirstItemAtTheEnd()
    {
        using var clock = new ManualGestureClock();
        var carousel = Carousel();
        Assert.False(carousel.IsAutoPlayScheduled); // not shown yet
        using var surface = new SkUiTestSurface(carousel, 300, 200);
        surface.Frame(0);
        Assert.True(carousel.IsAutoPlayScheduled);
        var player = new Player(clock, surface);
        var steps = new List<(int Position, double At)>();
        carousel.PositionChanged += (_, args) => steps.Add((args.CurrentPosition, player.Now));

        player.Play(Interval - 32);
        Assert.Empty(steps);
        Assert.True(player.PlayUntil(() => steps.Count == 3));

        Assert.Equal([1, 2, 0], steps.Select(step => step.Position)); // the end of a carousel that does not loop: the first
        Assert.InRange(steps[0].At, Interval - 16, Interval + 16);
        for (var index = 1; index < steps.Count; index++)
            Assert.True(steps[index].At - steps[index - 1].At > Interval, "a full interval after the previous step came to rest");
        Assert.True(player.PlayUntil(() => !carousel.IsScrolling));
        Assert.Equal(0, carousel.ScrollView.ScrollX);

        // Off: nothing is scheduled.
        carousel.AutoPlayInterval = TimeSpan.Zero;
        Assert.False(carousel.IsAutoPlayScheduled);
        player.Play(3 * Interval);
        Assert.Equal(3, steps.Count);
    }

    [Fact]
    public void WithSeveralItemsPerSnapPointItStepsAPageAndALoopGoesOnToTheFirstPage()
    {
        using var clock = new ManualGestureClock();
        var carousel = Carousel(count: 10, loop: true);
        carousel.ItemsPerSnapPoint = 3;
        using var surface = new SkUiTestSurface(carousel, 300, 200);
        surface.Frame(0);
        var player = new Player(clock, surface);
        var positions = new List<int>();
        carousel.PositionChanged += (_, args) => positions.Add(args.CurrentPosition);

        Assert.True(player.PlayUntil(() => positions.Count == 5));

        Assert.Equal([3, 6, 9, 0, 3], positions);
        Assert.True(player.PlayUntil(() => !carousel.IsScrolling));
        Assert.Equal(3, carousel.ScrollPosition, 6); // forward round the loop, not back across it
    }

    [Fact]
    public void ADragHoldsItAndTheWaitStartsAgainOnceTheCarouselRests()
    {
        using var clock = new ManualGestureClock();
        var carousel = Carousel(count: 5);
        using var surface = new SkUiTestSurface(carousel, 300, 200);
        surface.Frame(0);
        var player = new Player(clock, surface);
        player.Play(Interval - 200);

        // A swipe just before the step was due: it moves one item, and the step waits.
        var id = ++_pointer;
        var at = player.Now;
        carousel.Touch(new(id, SkUiTouchAction.Pressed, new Point(250, 100), TimeSpan.FromMilliseconds(at)));
        carousel.Touch(new(id, SkUiTouchAction.Moved, new Point(150, 100), TimeSpan.FromMilliseconds(at + 10)));
        carousel.Touch(new(id, SkUiTouchAction.Moved, new Point(50, 100), TimeSpan.FromMilliseconds(at + 20)));
        carousel.Touch(new(id, SkUiTouchAction.Released, new Point(50, 100), TimeSpan.FromMilliseconds(at + 25)));
        Assert.True(player.PlayUntil(() => !carousel.IsScrolling));
        Assert.Equal(1, carousel.Position);

        player.Play(Interval - 48);
        Assert.Equal(1, carousel.Position);
        player.Play(64);
        Assert.Equal(2, carousel.Position);
    }

    [Fact]
    public void HoverKeyboardFocusInsideAndTheAppsPauseHoldIt()
    {
        using var clock = new ManualGestureClock();
        var carousel = Carousel(count: 5, loop: true);
        carousel.HeightRequest = 200;
        var root = new SkUiVerticalStackLayout { Children = { carousel } };
        using var surface = new SkUiTestSurface(root, 300, 300);
        surface.Frame(0);
        var player = new Player(clock, surface);

        var scroller = (ISkUiInputNode)carousel.ScrollView;
        scroller.SetPointerOver(true); // a mouse or pen over it
        player.Play(2.5 * Interval);
        Assert.Equal(0, carousel.Position);
        scroller.SetPointerOver(false);
        Assert.True(player.PlayUntil(() => carousel.Position == 1, Interval));
        Assert.True(player.PlayUntil(() => !carousel.IsScrolling));

        var card = (SkUiView)carousel.GetRealizedView(1)!;
        Assert.True(root.FocusManager.Focus(card));
        player.Play(2.5 * Interval);
        Assert.Equal(1, carousel.Position);
        root.FocusManager.Unfocus(card);
        Assert.True(player.PlayUntil(() => carousel.Position == 2, Interval + 16));
        Assert.True(player.PlayUntil(() => !carousel.IsScrolling));

        carousel.IsAutoPlayPaused = true;
        Assert.False(carousel.IsAutoPlayScheduled);
        player.Play(3 * Interval);
        Assert.Equal(2, carousel.Position);
        carousel.IsAutoPlayPaused = false;
        player.Play(Interval - 32);
        Assert.Equal(2, carousel.Position); // a full interval after the pause ends
        player.Play(64);
        Assert.Equal(3, carousel.Position);
    }

    [Fact]
    public void HiddenOrRemovedItStopsAndShownAgainItWaitsAFullInterval()
    {
        using var clock = new ManualGestureClock();
        var carousel = Carousel(count: 5, loop: true);
        carousel.HeightRequest = 200;
        var stack = new SkUiVerticalStackLayout { Children = { carousel } };
        var root = new SkUiVerticalStackLayout { Children = { stack } };
        using var surface = new SkUiTestSurface(root, 300, 300);
        surface.Frame(0);
        var player = new Player(clock, surface);
        player.Play(Interval / 2);

        carousel.IsVisible = false;
        Assert.False(carousel.IsAutoPlayScheduled);
        player.Play(2 * Interval);
        carousel.IsVisible = true;
        Assert.True(carousel.IsAutoPlayScheduled);
        player.Play(Interval - 32);
        Assert.Equal(0, carousel.Position);
        player.Play(64);
        Assert.Equal(1, carousel.Position);
        Assert.True(player.PlayUntil(() => !carousel.IsScrolling));

        stack.IsVisible = false; // an ancestor
        Assert.False(carousel.IsAutoPlayScheduled);
        stack.IsVisible = true;
        Assert.True(carousel.IsAutoPlayScheduled);

        stack.Children.Remove(carousel);
        Assert.False(carousel.IsAutoPlayScheduled);
        player.Play(2 * Interval);
        Assert.Equal(1, carousel.Position);
        stack.Children.Add(carousel);
        Assert.True(carousel.IsAutoPlayScheduled);
    }

    [Fact]
    public void AScreenReaderRunningOrTheAppInTheBackgroundHoldsIt()
    {
        using var clock = new ManualGestureClock();
        var carousel = Carousel(count: 5, loop: true);
        using var surface = new SkUiTestSurface(carousel, 300, 200);
        surface.Frame(0);
        var player = new Player(clock, surface);
        try
        {
            SkUiScreenReader.Override = true;
            player.Play(2.5 * Interval);
            Assert.Equal(0, carousel.Position);
            SkUiScreenReader.Override = false;
            SkUiAppState.BackgroundOverride = true;
            player.Play(2 * Interval);
            Assert.Equal(0, carousel.Position);
            SkUiAppState.BackgroundOverride = false;
            Assert.True(player.PlayUntil(() => carousel.Position == 1, Interval));
        }
        finally
        {
            SkUiScreenReader.Override = null;
            SkUiAppState.BackgroundOverride = null;
        }
    }

    [Fact]
    public void WithReduceMotionAStepJumps()
    {
        using var clock = new ManualGestureClock();
        var saved = SkUiMotion.ReduceMotion;
        SkUiMotion.ReduceMotion = true;
        try
        {
            var carousel = Carousel(count: 5);
            using var surface = new SkUiTestSurface(carousel, 300, 200);
            surface.Frame(0);
            var player = new Player(clock, surface);

            Assert.True(player.PlayUntil(() => carousel.Position == 1, Interval + 16));
            Assert.Equal(300, carousel.ScrollView.ScrollX); // there at once
            Assert.False(carousel.IsScrolling);
            Assert.True(carousel.IsAutoPlayScheduled);
        }
        finally
        {
            SkUiMotion.ReduceMotion = saved;
        }
    }

    [Fact]
    public void ASingleItemDoesNotStepAndANegativeIntervalIsRejected()
    {
        using var clock = new ManualGestureClock();
        var carousel = Carousel(count: 1);
        using var surface = new SkUiTestSurface(carousel, 300, 200);
        surface.Frame(0);
        var player = new Player(clock, surface);
        var changes = 0;
        carousel.PositionChanged += (_, _) => changes++;

        player.Play(3 * Interval);
        Assert.Equal(0, changes);

        carousel.AutoPlayInterval = TimeSpan.FromSeconds(-1); // ignored, as MAUI ignores invalid values
        Assert.Equal(TimeSpan.FromMilliseconds(Interval), carousel.AutoPlayInterval);
        Assert.Throws<ArgumentOutOfRangeException>(() => carousel.SetAutoPlayInterval(TimeSpan.FromSeconds(-1)));
    }
}
