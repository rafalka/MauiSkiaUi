using System.Collections.ObjectModel;
using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// 1.17 C5 <see cref="SkUiCoreCarouselView"/>: the carousel tests that apply on Core (filling items, peek insets, a swipe, the
/// position set and set before the items, looping the shorter way round, item changes keeping the current item, loading
/// more, the empty view, keys, right to left, several items per snap point, visible nodes), the item factory's recycling
/// (one node per realized slot, rebound when reused, keys keeping pools apart), the same carousel drawn on both layers
/// (cover flow, mid-scroll), the linked <see cref="SkUiCoreIndicatorView"/>, and autoplay.
/// </summary>
public class CoreCarouselViewTests
{
    private static long _pointer = 260_000;

    private static ObservableCollection<string> Items(int count) => [.. Enumerable.Range(0, count).Select(index => $"Item {index}")];

    private static SkUiCoreCarouselView Carousel(int count = 5, bool loop = false) =>
        new SkUiCoreCarouselView().SetItemsSource(Items(count)).SetLoop(loop)
            .SetItemFactory(static _ => new SkUiCoreButton(), static (node, item) => ((SkUiCoreButton)node).SetText((string?)item));

    private static SkUiCoreHost Host(SkUiCoreNode content) => new SkUiCoreHost().SetContent(content);

    /// <summary>A fast swipe from <paramref name="from"/> to <paramref name="to"/> (a fling), released.</summary>
    private static void Swipe(SkUiView root, Point from, Point to, double startMs)
    {
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, from, TimeSpan.FromMilliseconds(startMs)));
        root.Touch(new(id, SkUiTouchAction.Moved, new Point((from.X * 2 + to.X) / 3, (from.Y * 2 + to.Y) / 3), TimeSpan.FromMilliseconds(startMs + 10)));
        root.Touch(new(id, SkUiTouchAction.Moved, to, TimeSpan.FromMilliseconds(startMs + 20)));
        root.Touch(new(id, SkUiTouchAction.Released, to, TimeSpan.FromMilliseconds(startMs + 25)));
    }

    private static void Run(SkUiTestSurface surface, double fromMs, double untilMs)
    {
        for (var ms = fromMs; ms <= untilMs; ms += 16)
            surface.Frame(ms);
    }

    private static void SwipeForward(SkUiView root, SkUiTestSurface surface, double startMs)
    {
        Swipe(root, new Point(250, 100), new Point(50, 100), startMs);
        Run(surface, startMs + 30, startMs + 1900);
    }

    #region Layout and paging

    [Fact]
    public void FillingItemsSwipeOneAtATimeAndRaiseThePositionEventsOnce()
    {
        var carousel = Carousel();
        var host = Host(carousel);
        using var surface = new SkUiTestSurface(host, 300, 200);
        surface.Frame(0);
        Assert.Equal((300d, 300d, 5, 1), carousel.Geometry);
        var positions = new List<(int, int)>();
        var items = new List<object?>();
        carousel.PositionChanged += (_, args) => positions.Add((args.PreviousPosition, args.CurrentPosition));
        carousel.CurrentItemChanged += (_, args) => items.Add(args.CurrentItem);

        SwipeForward(host, surface, 0);

        Assert.Equal(300, carousel.ScrollView.ScrollX);
        Assert.Equal(1, carousel.Position);
        Assert.Equal([(0, 1)], positions);
        Assert.Equal(["Item 1"], items);
        Assert.Equal("Item 1", carousel.CurrentItem);
        Assert.False(carousel.IsDragging);
        Assert.False(carousel.IsScrolling);
    }

    [Fact]
    public void SwipingCanBeTurnedOff()
    {
        var carousel = Carousel().SetIsSwipeEnabled(false);
        var host = Host(carousel);
        using var surface = new SkUiTestSurface(host, 300, 200);
        surface.Frame(0);
        SwipeForward(host, surface, 0);
        Assert.Equal((0, 0d), (carousel.Position, carousel.ScrollView.ScrollX));

        carousel.SetIsSwipeEnabled(true);
        SwipeForward(host, surface, 3000);
        Assert.Equal(1, carousel.Position);
    }

    [Fact]
    public void PeekInsetsAndSpacingShortenTheItemsAndShowTheNeighbors()
    {
        var carousel = Carousel().SetPeekAreaInsets(new Thickness(40, 0)).SetItemSpacing(10);
        using var surface = new SkUiTestSurface(Host(carousel), 300, 200);
        surface.Frame(0);

        Assert.Equal(220, carousel.Geometry.Extent, 3);
        Assert.Equal(230, carousel.Geometry.Stride, 3);
        Assert.Equal(220, carousel.GetRealizedNode(0)!.Frame.Width, 3);
        Assert.Equal(["Item 0", "Item 1"], carousel.VisibleNodes.Select(static node => ((SkUiCoreButton)node).Text));
    }

    [Fact]
    public void SettingThePositionScrollsThereAndBeyondTheLastItemIsTheLast()
    {
        var carousel = Carousel(count: 8);
        using var surface = new SkUiTestSurface(Host(carousel), 300, 200);
        surface.Frame(0);
        var positions = new List<int>();
        carousel.PositionChanged += (_, args) => positions.Add(args.CurrentPosition);

        carousel.Position = 5;
        Assert.Equal([5], positions);
        Assert.True(carousel.IsScrolling);
        Run(surface, 0, 1000);
        Assert.Equal(1500, carousel.ScrollView.ScrollX);
        Assert.Equal([5], positions); // held during the scroll

        carousel.CurrentItem = "Item 2";
        Assert.Equal(2, carousel.Position);
        carousel.CurrentItem = "Missing";
        Assert.Equal("Item 2", carousel.CurrentItem);
        carousel.IsScrollAnimated = false;
        carousel.Position = 50;
        Assert.Equal(7, carousel.Position);
        Assert.Equal(2100, carousel.ScrollView.ScrollX);
    }

    [Fact]
    public void APositionSetBeforeTheItemsAppliesOnceTheyCome()
    {
        var carousel = new SkUiCoreCarouselView().SetLoop(false).SetPosition(3);
        using var surface = new SkUiTestSurface(Host(carousel), 300, 200);
        surface.Frame(0);
        carousel.SetItemsSource(Items(5));
        surface.Frame(16);

        Assert.Equal(3, carousel.Position);
        Assert.Equal("Item 3", carousel.CurrentItem);
        Assert.Equal(900, carousel.ScrollView.ScrollX);
    }

    [Fact]
    public void ALoopingCarouselGoesTheShorterWayRoundAndSwipesPastTheLastItem()
    {
        var carousel = Carousel(loop: true).SetIsScrollAnimated(false);
        var host = Host(carousel);
        using var surface = new SkUiTestSurface(host, 300, 200);
        surface.Frame(0);
        var start = carousel.ScrollView.ScrollX;

        carousel.Position = 4;
        Assert.Equal(start - 300, carousel.ScrollView.ScrollX, 3); // one item back, not four forward
        SwipeForward(host, surface, 0);
        Assert.Equal(0, carousel.Position);
        Assert.Equal(0, carousel.ScrollPosition, 6);
    }

    [Fact]
    public void ItemChangesKeepTheCurrentItemAndLoadingMoreFiresOncePerCount()
    {
        var items = Items(5);
        var carousel = new SkUiCoreCarouselView().SetItemsSource(items).SetLoop(false).SetIsScrollAnimated(false).SetRemainingItemsThreshold(1);
        var reached = 0;
        carousel.RemainingItemsThresholdReached += (_, _) => reached++;
        using var surface = new SkUiTestSurface(Host(carousel), 300, 200);
        surface.Frame(0);
        carousel.Position = 2;

        items.Insert(0, "First");
        surface.Frame(16);
        Assert.Equal(3, carousel.Position);
        Assert.Equal("Item 2", carousel.CurrentItem);
        Assert.Equal(900, carousel.ScrollView.ScrollX);

        Assert.Equal(0, reached);
        carousel.Position = 4;
        Assert.Equal(1, reached);
        carousel.Position = 5;
        Assert.Equal(1, reached); // once per item count
        items.Add("More");
        surface.Frame(32); // laid out again: the new end is near
        Assert.Equal(2, reached);
        carousel.Position = 6;
        Assert.Equal(2, reached);
    }

    [Fact]
    public void TheEmptyViewShowsWithoutItems()
    {
        var empty = new SkUiCoreLabel().SetText("Nothing");
        var carousel = new SkUiCoreCarouselView().SetEmptyView(empty);
        using var surface = new SkUiTestSurface(Host(carousel), 300, 200);
        surface.Frame(0);
        Assert.Contains(empty, ((IVisualTreeElement)carousel).GetVisualChildren());
        Assert.Equal(300, empty.Frame.Width);

        carousel.SetItemsSource(Items(2));
        surface.Frame(16);
        Assert.DoesNotContain(empty, ((IVisualTreeElement)carousel).GetVisualChildren());
        Assert.Throws<InvalidOperationException>(() => new SkUiCoreCarouselView().SetEmptyView(empty));
    }

    [Fact]
    public void ArrowKeysAndScreenReadersStepAndRightToLeftStartsAtTheRight()
    {
        var carousel = Carousel(count: 4).SetIsScrollAnimated(false);
        var host = Host(carousel);
        host.FlowDirection = FlowDirection.RightToLeft;
        using var surface = new SkUiTestSurface(host, 300, 200);
        surface.Frame(0);
        Assert.Equal(900, carousel.ScrollView.ScrollX); // the first item at the right end

        Assert.True(host.FocusManager.Focus(carousel.GetRealizedNode(0)!));
        Assert.True(host.FocusManager.KeyDown(SkUiKey.Left)); // the next item, to the left
        Assert.Equal(1, carousel.Position);
        Run(surface, 16, 1000); // a step scrolls animated
        Assert.Equal(600, carousel.ScrollView.ScrollX);
        var stepper = (ISkUiScrollStepper)carousel;
        Assert.True(stepper.StepToEdge(end: true));
        Assert.Equal(3, carousel.Position);
        Assert.False(stepper.CanStep(forward: false)); // towards smaller offsets: past the last item, to the left
    }

    [Fact]
    public void PagesOfThreeSwipePageByPageAndThePositionIsAPagesFirstItem()
    {
        var carousel = Carousel(count: 10).SetItemsPerSnapPoint(3);
        var host = Host(carousel);
        using var surface = new SkUiTestSurface(host, 300, 200);
        surface.Frame(0);
        Assert.Equal(100, carousel.Geometry.Extent, 3);
        var scrolls = new List<double>();
        for (var swipe = 0; swipe < 3; swipe++)
        {
            SwipeForward(host, surface, swipe * 2000);
            scrolls.Add(carousel.ScrollView.ScrollX);
        }

        Assert.Equal([300, 600, 700], scrolls);
        Assert.Equal(9, carousel.Position);
        carousel.SetIsScrollAnimated(false).SetPosition(4);
        Assert.Equal(3, carousel.Position);
        Assert.Equal(300, carousel.ScrollView.ScrollX);
        Assert.Throws<ArgumentOutOfRangeException>(() => carousel.SetItemsPerSnapPoint(0));
    }

    [Fact]
    public void VisibleNodesFollowTheScrollingAndChangeOnlyWhenTheSetDoes()
    {
        var carousel = Carousel(count: 6);
        var host = Host(carousel);
        using var surface = new SkUiTestSurface(host, 300, 200);
        surface.Frame(0);
        var changes = 0;
        carousel.VisibleNodesChanged += (_, _) => changes++;
        Assert.Single(carousel.VisibleNodes);
        Assert.Same(carousel.GetRealizedNode(0), carousel.VisibleNodes[0]);

        SwipeForward(host, surface, 0);

        Assert.Same(carousel.GetRealizedNode(1), Assert.Single(carousel.VisibleNodes));
        Assert.Equal(2, changes); // item 1 came in, item 0 went
        surface.Frame(2000);
        Assert.Equal(2, changes);
    }

    #endregion

    #region Item factory

    [Fact]
    public void TheFactoryMakesANodePerRealizedSlotRebindsRecycledOnesAndKeysKeepPoolsApart()
    {
        var created = 0;
        var binds = 0;
        var carousel = new SkUiCoreCarouselView().SetItemsSource(Enumerable.Range(0, 20).ToList()).SetLoop(false).SetIsScrollAnimated(false)
            .SetItemFactory(item =>
            {
                created++;
                return new SkUiCoreBox { Tag = (int)item! % 2 };
            }, (node, item) =>
            {
                binds++;
                Assert.Equal((int)item! % 2, node.Tag); // a node made for even items shows even items only
            }, static item => (int)item! % 2);
        using var surface = new SkUiTestSurface(Host(carousel), 300, 200);
        surface.Frame(0);
        Assert.Equal(2, created); // the item in view and the next one
        Assert.Equal(created, carousel.CreatedItemNodes);

        for (var position = 1; position < 12; position++)
        {
            carousel.Position = position;
            surface.Frame(position * 16);
        }

        Assert.InRange(created, 3, 4); // a window of three slots, recycled per key
        Assert.True(binds > created, "recycled nodes are bound again");

        // A new factory makes the nodes again.
        var labels = 0;
        carousel.SetItemFactory(_ => { labels++; return new SkUiCoreLabel(); }, static (node, item) => ((SkUiCoreLabel)node).SetText(item?.ToString()));
        surface.Frame(500);
        Assert.Equal(3, labels);
        Assert.Equal("11", ((SkUiCoreLabel)carousel.GetRealizedNode(11)!).Text);
    }

    [Fact]
    public void WithoutAFactoryEachItemShowsItsText()
    {
        var carousel = new SkUiCoreCarouselView().SetItemsSource(Items(3));
        using var surface = new SkUiTestSurface(Host(carousel), 300, 200);
        surface.Frame(0);

        Assert.Equal("Item 0", Assert.IsType<SkUiCoreLabel>(carousel.GetRealizedNode(0)).Text);
    }

    #endregion

    #region Both layers

    private static readonly Color[] Colors7 =
        [Colors.Red, Colors.Orange, Colors.Gold, Colors.Green, Colors.Teal, Colors.Blue, Colors.Purple];

    [Fact]
    public void TheSameCoverFlowCarouselDrawsTheSameOnBothLayersMidScroll()
    {
        var effect = new SkUiCoverFlowEffect();
        var skui = new SkUiCarouselView
        {
            ItemsSource = Enumerable.Range(0, 7).ToList(), Loop = false, ItemExtent = 140, Position = 3, IsScrollAnimated = false, ItemEffect = effect,
            ItemTemplate = new DataTemplate(() =>
            {
                var box = new SkUiBox();
                box.SetBinding(SkUiBox.ColorProperty, new Binding(".", converter: new IndexColor()));
                return box;
            })
        };
        var core = new SkUiCoreCarouselView().SetItemsSource(Enumerable.Range(0, 7).ToList()).SetLoop(false).SetItemExtent(140)
            .SetPosition(3).SetIsScrollAnimated(false).SetItemEffect(effect)
            .SetItemFactory(static _ => new SkUiCoreBox(), static (node, item) => ((SkUiCoreBox)node).SetColor(Colors7[(int)item!]));
        using var skuiSurface = new SkUiTestSurface(new SkUiContentView { Content = skui, Background = Colors.White }, 400, 200);
        using var coreSurface = new SkUiTestSurface(new SkUiContentView { Content = Host(core), Background = Colors.White }, 400, 200);
        skuiSurface.Frame(0);
        coreSurface.Frame(0);

        // Halfway to the next item.
        _ = skui.ScrollView.ScrollToAsync(skui.ScrollView.ScrollX + 70, 0, animated: false);
        core.ScrollView.ScrollTo(core.ScrollView.ScrollX + 70, 0);
        skuiSurface.Frame(16);
        coreSurface.Frame(16);

        Assert.Equal(skui.ScrollView.ScrollX, core.ScrollView.ScrollX, 3);
        var differing = 0;
        for (var y = 0; y < 200; y += 2)
            for (var x = 0; x < 400; x += 2)
                if (skuiSurface.Bitmap.GetPixel(x, y) != coreSurface.Bitmap.GetPixel(x, y))
                    differing++;
        Assert.True(differing < 40, $"{differing} sampled pixels differ");
    }

    private sealed class IndexColor : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
            value is int index ? Colors7[index] : null;

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
            throw new NotSupportedException();
    }

    #endregion

    #region Indicator

    [Fact]
    public void ALinkedIndicatorCountsFollowsAndATapScrollsTheCarousel()
    {
        var carousel = Carousel(count: 4);
        var indicator = new SkUiCoreIndicatorView().SetCarousel(carousel);
        var host = Host(new SkUiCoreVerticalStackLayout().Add(carousel.SetHeight(180)).Add(indicator.SetHeight(20)));
        using var surface = new SkUiTestSurface(host, 300, 200);
        surface.Frame(0);
        Assert.Same(carousel, indicator.Carousel);
        Assert.Equal((4, 0), (indicator.Count, indicator.Position));

        SwipeForward(host, surface, 0);
        Assert.Equal(1, indicator.Position);
        Assert.Equal(1, indicator.DisplayedPosition, 3);

        indicator.SetPosition(3); // a tap
        Assert.Equal(3, carousel.Position);
        carousel.SetItemsPerSnapPoint(2);
        Assert.Equal((2, 1), (indicator.Count, indicator.Position));
        Assert.Equal(2, carousel.Position);

        // A carousel shows one indicator: linking another lets the first go.
        var second = new SkUiCoreIndicatorView().SetCarousel(carousel);
        Assert.Null(indicator.Carousel);
        Assert.Same(carousel, second.Carousel);
        indicator.SetPosition(0);
        Assert.Equal(2, carousel.Position);
        second.SetCarousel(null);
        Assert.Null(carousel.Indicator);
    }

    #endregion

    #region Autoplay

    [Fact]
    public void AutoplayStepsPerIntervalAndStopsWhileHiddenOrOutOfTheTree()
    {
        using var clock = new ManualGestureClock();
        var carousel = Carousel(loop: true).SetAutoPlayInterval(TimeSpan.FromSeconds(1));
        var stack = new SkUiCoreVerticalStackLayout();
        stack.Add(carousel.SetHeight(200));
        using var surface = new SkUiTestSurface(Host(stack), 300, 200);
        surface.Frame(0);
        Assert.True(carousel.IsAutoPlayScheduled);
        var now = 0d;
        void Play(double ms)
        {
            for (var end = now + ms; now < end;)
            {
                now += 16;
                clock.Advance(TimeSpan.FromMilliseconds(16));
                surface.Frame(now);
            }
        }

        Play(1050);
        Assert.Equal(1, carousel.Position);
        Play(1500);
        Assert.Equal(2, carousel.Position);

        carousel.SetIsVisible(false);
        Assert.False(carousel.IsAutoPlayScheduled);
        carousel.SetIsVisible(true);
        Assert.True(carousel.IsAutoPlayScheduled);
        carousel.SetIsAutoPlayPaused(true);
        Assert.False(carousel.IsAutoPlayScheduled);
        Play(3000);
        Assert.Equal(2, carousel.Position);
        carousel.SetIsAutoPlayPaused(false);

        // Hidden through an ancestor: held at the ticks, and a full interval once shown again.
        stack.SetIsVisible(false);
        Play(2500);
        Assert.Equal(2, carousel.Position);
        stack.SetIsVisible(true);
        Play(2100);
        Assert.Equal(3, carousel.Position);

        stack.Remove(carousel);
        Assert.False(carousel.IsAutoPlayScheduled);
    }

    #endregion
}
