using System.Collections.ObjectModel;
using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// 1.17: both carousels while things change under them: items added and removed as the strip moves (also with pages), the
/// direction changing, unbounded parents (the layout settles), a position set again inside its notification, equal items, steps
/// round a loop of two pages, the threshold with pages, the current item inside a page, and autoplay out of view.
/// </summary>
public class CarouselChangesTests
{
    private sealed record Carousel(SkUiView Root, ObservableCollection<string> Items, Func<int> Position, Action<int> SetPosition, Func<object?> CurrentItem,
        Func<double> ScrollPosition, Func<double> Offset, ISkUiScrollStepper Stepper, SkUiCarouselView? Drawn, SkUiCoreCarouselView? Core);

    private static Carousel Make(bool core, int count = 6, bool loop = false, int perPage = 1, double extent = 0)
    {
        var items = new ObservableCollection<string>(Enumerable.Range(0, count).Select(static index => $"I{index}"));
        if (core)
        {
            var carousel = new SkUiCoreCarouselView().SetLoop(loop).SetItemsPerSnapPoint(perPage).SetItemExtent(extent).SetItemsSource(items)
                .SetItemFactory(static _ => new SkUiCoreBox(), static (_, _) => { });
            return new Carousel(new SkUiCoreHost().SetContent(carousel), items, () => carousel.Position, value => carousel.SetPosition(value), () => carousel.CurrentItem,
                () => carousel.ScrollPosition, () => carousel.ScrollView.ScrollX, carousel, null, carousel);
        }
        var drawn = new SkUiCarouselView { Loop = loop, ItemsPerSnapPoint = perPage, ItemExtent = extent, ItemsSource = items, ItemTemplate = new DataTemplate(static () => new SkUiBox()) };
        return new Carousel(new SkUiContentView { Content = drawn }, items, () => drawn.Position, value => drawn.Position = value, () => drawn.CurrentItem,
            () => drawn.ScrollPosition, () => drawn.ScrollView.ScrollX, drawn, drawn, null);
    }

    private static void Run(SkUiTestSurface surface, double from, double until)
    {
        for (var ms = from; ms <= until; ms += 16)
        {
            surface.Root.AnimationClock.Tick(TimeSpan.FromMilliseconds(ms));
            surface.Frame(ms);
        }
    }

    public static TheoryData<bool, bool, int, string> Changes()
    {
        var data = new TheoryData<bool, bool, int, string>();
        foreach (var core in new[] { false, true })
            foreach (var loop in new[] { false, true })
                foreach (var perPage in new[] { 1, 2 })
                    foreach (var change in new[] { "append", "insert first", "remove first", "remove last" })
                        data.Add(core, loop, perPage, change);
        return data;
    }

    [Theory]
    [MemberData(nameof(Changes))]
    public void ItemsChangingDuringAnAnimatedScrollLeaveItOnTheItemAskedFor(bool core, bool loop, int perPage, string change)
    {
        var carousel = Make(core, count: 8, loop: loop, perPage: perPage);
        using var surface = new SkUiTestSurface(carousel.Root, 300, 200);
        surface.Frame(0);
        carousel.SetPosition(2);
        Run(surface, 16, 110); // on the way
        var asked = carousel.CurrentItem();
        switch (change)
        {
            case "append": carousel.Items.Add("New"); break;
            case "insert first": carousel.Items.Insert(0, "New"); break;
            case "remove first": carousel.Items.RemoveAt(0); break;
            default: carousel.Items.RemoveAt(7); break;
        }
        Run(surface, 126, 2500);

        Assert.Equal(carousel.Position(), carousel.ScrollPosition(), 2); // on a snap point, the position's
        Assert.Equal(0, carousel.Position() % perPage);
        // The item asked for, or the page it is in now.
        Assert.Equal(carousel.Items.IndexOf((string)asked!) / perPage * perPage, carousel.Position());
        Assert.Equal(carousel.Items[carousel.Position()], carousel.CurrentItem());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnItemAddedWhileAPageIsDraggedLeavesItUnderTheFinger(bool core)
    {
        var carousel = Make(core, count: 12, perPage: 4);
        using var surface = new SkUiTestSurface(carousel.Root, 300, 200);
        surface.Frame(0);
        carousel.Root.Touch(new(470_001, SkUiTouchAction.Pressed, new Point(250, 100), TimeSpan.Zero));
        carousel.Root.Touch(new(470_001, SkUiTouchAction.Moved, new Point(230, 100), TimeSpan.FromMilliseconds(100)));
        carousel.Root.Touch(new(470_001, SkUiTouchAction.Moved, new Point(120, 100), TimeSpan.FromMilliseconds(300)));
        surface.Frame(300);
        var dragged = carousel.ScrollPosition();
        Assert.InRange(dragged, 1.2, 1.9);
        carousel.Items.Add("New");
        surface.Frame(316);
        Assert.Equal(dragged, carousel.ScrollPosition(), 2);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ChangingTheDirectionKeepsTheItem(bool core, bool loop)
    {
        var carousel = Make(core, loop: loop);
        using var surface = new SkUiTestSurface(carousel.Root, 300, 200);
        surface.Frame(0);
        carousel.SetPosition(1);
        Run(surface, 16, 800);
        var time = 900;
        foreach (var direction in new[] { FlowDirection.RightToLeft, FlowDirection.LeftToRight, FlowDirection.RightToLeft })
        {
            carousel.Root.FlowDirection = direction;
            Run(surface, time, time + 100);
            time += 200;
            Assert.Equal((1, "I1"), (carousel.Position(), carousel.CurrentItem()));
            Assert.Equal(1, carousel.ScrollPosition(), 2);
        }
    }

    public static TheoryData<bool, string, bool> Unbounded()
    {
        var data = new TheoryData<bool, string, bool>();
        foreach (var core in new[] { false, true })
        {
            foreach (var parent in new[] { "horizontal stack", "horizontal scroll", "auto column", "vertical stack" })
                data.Add(core, parent, false);
            data.Add(core, "vertical stack", true);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Unbounded))]
    public void TheLayoutSettlesWhereNothingBoundsTheCarousel(bool core, string parent, bool vertical)
    {
        var items = Enumerable.Range(0, 6).ToList();
        SkUiView view;
        if (core)
        {
            var carousel = new SkUiCoreCarouselView().SetItemsSource(items).SetItemFactory(static _ => new SkUiCoreBox().SetWidth(180).SetHeight(90), static (_, _) => { });
            carousel.SetOrientation(vertical ? ItemsLayoutOrientation.Vertical : ItemsLayoutOrientation.Horizontal);
            view = new SkUiCoreHost().SetContent(carousel);
        }
        else
        {
            view = new SkUiCarouselView
            {
                ItemsSource = items, Orientation = vertical ? ItemsLayoutOrientation.Vertical : ItemsLayoutOrientation.Horizontal,
                ItemTemplate = new DataTemplate(static () => new SkUiBox { WidthRequest = 180, HeightRequest = 90 })
            };
        }
        using var surface = new SkUiTestSurface(LayoutSettling.Place(view, parent), 300, 200);
        LayoutSettling.AssertSettles(surface, parent);
        var size = ((IView)view).DesiredSize;
        Assert.Equal(90, size.Height, 0.5);
        if (parent != "vertical stack")
            Assert.Equal(180, size.Width, 0.5);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StepsRoundALoopOfTwoPagesGoTheWayAskedFor(bool core)
    {
        var carousel = Make(core, count: 4, loop: true, perPage: 3);
        using var surface = new SkUiTestSurface(carousel.Root, 300, 200);
        surface.Frame(0);
        var time = 100d;
        foreach (var forward in new[] { true, true, true, false, false })
        {
            var position = carousel.Position();
            Assert.True(carousel.Stepper.Step(forward, page: true));
            Run(surface, time, time + 130);
            // Between item 0 and item 3 of the first page, or between the short last page and the next round's first.
            var between = carousel.ScrollPosition();
            var (low, high) = forward == (position == 0) ? (0, 3) : (3, 4);
            Assert.True(between > low && between < high, $"from page {position}, {(forward ? "forward" : "back")}: at {between}");
            Run(surface, time + 176, time + 900);
            time += 1000;
            Assert.Equal(position == 0 ? 3 : 0, carousel.Position());
            Assert.Equal(carousel.Position(), carousel.ScrollPosition(), 2);
        }
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public void TheThresholdIsReachedOnTheLastPage(bool core, bool loop)
    {
        var carousel = Make(core, count: 9, loop: loop, perPage: 3);
        var reached = 0;
        if (core)
        {
            carousel.Core!.RemainingItemsThresholdReached += (_, _) => reached++;
            carousel.Core.SetRemainingItemsThreshold(0);
        }
        else
        {
            carousel.Drawn!.RemainingItemsThresholdReached += (_, _) => reached++;
            carousel.Drawn.RemainingItemsThreshold = 0;
        }
        using var surface = new SkUiTestSurface(carousel.Root, 300, 200);
        surface.Frame(0);
        carousel.SetPosition(3);
        Run(surface, 16, 900);
        Assert.Equal(0, reached);
        carousel.SetPosition(6);
        Run(surface, 1000, 1900);
        Assert.Equal(1, reached);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EqualItemsKeepTheirPlace(bool core)
    {
        var carousel = Make(core, count: 0);
        foreach (var item in new[] { "A", "A", "B" })
            carousel.Items.Add(item);
        using var surface = new SkUiTestSurface(carousel.Root, 300, 200);
        surface.Frame(0);
        carousel.SetPosition(1);
        Run(surface, 16, 800);
        carousel.Items.Add("C");
        Run(surface, 900, 1000);
        Assert.Equal(1, carousel.Position());
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public void APositionSetAgainInItsNotificationIsTheOneEverythingShows(bool core, bool inEvent)
    {
        var carousel = Make(core);
        var indicator = 0;
        SkUiIndicatorView? drawnIndicator = null;
        SkUiCoreIndicatorView? coreIndicator = null;
        void Redirect()
        {
            if (carousel.Position() == 1)
                carousel.SetPosition(3);
        }
        if (core)
        {
            coreIndicator = new SkUiCoreIndicatorView().SetCarousel(carousel.Core!);
            if (inEvent)
                carousel.Core!.PositionChanged += (_, _) => Redirect();
            else
                carousel.Core!.PropertyChanged += (_, args) => { if (args.PropertyName == "Position") Redirect(); };
        }
        else
        {
            carousel.Drawn!.IndicatorView = drawnIndicator = new SkUiIndicatorView();
            if (inEvent)
                carousel.Drawn.PositionChanged += (_, _) => Redirect();
            else
                carousel.Drawn.PropertyChanged += (_, args) => { if (args.PropertyName == "Position") Redirect(); };
        }
        using var surface = new SkUiTestSurface(carousel.Root, 300, 200);
        surface.Frame(0);
        carousel.SetPosition(1);
        Run(surface, 16, 1500);
        indicator = coreIndicator?.Position ?? drawnIndicator!.Position;
        Assert.Equal((3, "I3", 3), (carousel.Position(), carousel.CurrentItem(), indicator));
        Assert.Equal(3, carousel.ScrollPosition(), 2);
    }

    [Fact]
    public void ACurrentItemInsideAPageRaisesOneEventForThePagesFirst()
    {
        var carousel = Make(core: false, count: 9, perPage: 3);
        using var surface = new SkUiTestSurface(carousel.Root, 300, 200);
        surface.Frame(0);
        var events = new List<(object?, object?)>();
        carousel.Drawn!.CurrentItemChanged += (_, args) => events.Add((args.PreviousItem, args.CurrentItem));

        carousel.Drawn.CurrentItem = "I1"; // the current page's
        Assert.Empty(events);
        Assert.Equal((0, "I0"), (carousel.Position(), carousel.CurrentItem()));
        carousel.Drawn.CurrentItem = "I4";
        Assert.Equal([("I0", "I3")], events);
        Assert.Equal((3, "I3"), (carousel.Position(), carousel.CurrentItem()));
        carousel.Drawn.CurrentItem = "not an item";
        Assert.Equal([("I0", "I3")], events);
        Assert.Equal("I3", carousel.CurrentItem());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AutoPlayWaitsWhileTheCarouselIsScrolledOutOfView(bool core)
    {
        var carousel = Make(core);
        carousel.Root.HeightRequest = 150;
        var scroll = new SkUiScrollView
        {
            Content = new SkUiVerticalStackLayout { Children = { (ISkUiView)carousel.Root, new SkUiBox { HeightRequest = 2000 } } }
        };
        using var surface = new SkUiTestSurface(scroll, 300, 200);
        surface.Frame(0);
        ISkUiAutoPlayHost host = core ? carousel.Core! : carousel.Drawn!;
        Assert.False(host.IsAutoPlayHeld);
        scroll.ScrollTo(0, 140);
        surface.Frame(16);
        Assert.False(host.IsAutoPlayHeld); // a part still shows
        scroll.ScrollTo(0, 400);
        surface.Frame(32);
        Assert.True(host.IsAutoPlayHeld);
        scroll.ScrollTo(0, 0);
        surface.Frame(48);
        if (core)
            _ = host.IsAutoPlayHeld; // the first tick after being hidden restarts the wait
        Assert.False(host.IsAutoPlayHeld);
    }
}
