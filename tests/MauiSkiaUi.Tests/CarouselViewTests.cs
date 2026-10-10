using System.Collections.ObjectModel;
using MauiSkiaUi.Core;
using MauiSkiaUi.Rendering;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// C4 <see cref="SkUiCarouselView"/>: the layout (filling items, peek insets, several items in view, spacing, right to left,
/// vertical), virtual items and recycling, paging by swipes and flings (snap points from the panel), the position and current
/// item (events, commands, two-way), looping (a strip of copies moved back to its middle by corrections, also during flings),
/// collection changes keeping the current item, loading more, the empty view, the keyboard, and item effects placed by the
/// compositor.
/// </summary>
public class CarouselViewTests
{
    private static long _pointer = 160_000;

    private static SkUiScrollController Scroller(SkUiCarouselView carousel) => ((ISkUiScrollHost)carousel.ScrollView).Scroller;

    private static ObservableCollection<string> Items(int count) => [.. Enumerable.Range(0, count).Select(index => $"Item {index}")];

    private static SkUiCarouselView Carousel(int count = 5, bool loop = false) =>
        new() { ItemsSource = Items(count), Loop = loop, ItemTemplate = new DataTemplate(() => new SkUiBox { Color = Colors.Blue }) };

    /// <summary>A fast swipe from <paramref name="from"/> to <paramref name="to"/> (a fling), released.</summary>
    private static void Swipe(SkUiView root, Point from, Point to, double startMs)
    {
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, from, TimeSpan.FromMilliseconds(startMs)));
        root.Touch(new(id, SkUiTouchAction.Moved, new Point((from.X * 2 + to.X) / 3, (from.Y * 2 + to.Y) / 3), TimeSpan.FromMilliseconds(startMs + 10)));
        root.Touch(new(id, SkUiTouchAction.Moved, to, TimeSpan.FromMilliseconds(startMs + 20)));
        root.Touch(new(id, SkUiTouchAction.Released, to, TimeSpan.FromMilliseconds(startMs + 25)));
    }

    /// <summary>Renders frames until <paramref name="untilMs"/> (16 ms apart) from <paramref name="fromMs"/>.</summary>
    private static void Run(SkUiTestSurface surface, double fromMs, double untilMs)
    {
        for (var ms = fromMs; ms <= untilMs; ms += 16)
            surface.Frame(ms);
    }

    #region Layout

    [Fact]
    public void DefaultsMatchMaui()
    {
        var carousel = new SkUiCarouselView();
        Assert.True(carousel.Loop);
        Assert.Equal(0, carousel.Position);
        Assert.Null(carousel.CurrentItem);
        Assert.Equal(SnapPointsType.MandatorySingle, carousel.SnapPointsType);
        Assert.Equal(SnapPointsAlignment.Center, carousel.SnapPointsAlignment);
        Assert.Equal(ItemsLayoutOrientation.Horizontal, carousel.Orientation);
        Assert.True(carousel.IsSwipeEnabled);
        Assert.True(carousel.IsBounceEnabled);
        Assert.True(carousel.IsScrollAnimated);
        Assert.Equal(default, carousel.PeekAreaInsets);
        Assert.Equal(-1, carousel.RemainingItemsThreshold);
        Assert.Null(carousel.ItemEffect);
    }

    [Fact]
    public void ItemsFillTheCarouselAndOnlyThoseNearTheViewportExist()
    {
        var carousel = Carousel(count: 20);
        using var surface = new SkUiTestSurface(carousel, 300, 200);
        surface.Frame(0);
        Assert.Equal((300d, 300d, 20, 1), carousel.Geometry);
        // The item in view and one on either side (none before the first).
        Assert.Equal((0, 1), carousel.RealizedSlots);
        var first = (SkUiView)carousel.GetRealizedView(0)!;
        Assert.Equal(new Size(300, 200), first.Bounds.Size);
        Assert.Equal("Item 0", first.BindingContext);
        Assert.Equal("Item 0", carousel.CurrentItem);

        Assert.True(carousel.ScrollToIndex(10, animated: false).IsCompleted);
        Assert.Equal(3000, carousel.ScrollView.ScrollX);
        Assert.Equal(10, carousel.Position);
        Assert.Equal("Item 10", carousel.CurrentItem);
        Assert.Equal((9, 11), carousel.RealizedSlots);
    }

    [Fact]
    public void PeekInsetsShortenTheItemsAndTheirNeighborsShowInThem()
    {
        var carousel = Carousel(count: 5);
        carousel.PeekAreaInsets = new Thickness(40, 0);
        carousel.ItemSpacing = 10;
        using var surface = new SkUiTestSurface(carousel, 300, 200);
        surface.Frame(0);
        Assert.Equal((220d, 230d, 5, 1), carousel.Geometry);
        Assert.Equal(new Rect(40, 0, 220, 200), ((SkUiView)carousel.GetRealizedView(0)!.Parent!).Frame);
        carousel.ScrollToIndex(1, animated: false);
        // The second item is centered: its start at the inset, the first one peeking 30 DIPs into the start inset.
        Assert.Equal(230, carousel.ScrollView.ScrollX);
        Assert.Equal(270, ((SkUiView)carousel.GetRealizedView(1)!.Parent!).Frame.X);
    }

    [Fact]
    public void AnItemExtentShowsSeveralItemsAndStartAlignmentLinesTheCurrentOneUpAtTheStart()
    {
        var carousel = Carousel(count: 10);
        carousel.ItemExtent = 90;
        carousel.ItemSpacing = 15;
        carousel.SnapPointsAlignment = SnapPointsAlignment.Start;
        using var surface = new SkUiTestSurface(carousel, 300, 120);
        surface.Frame(0);
        Assert.Equal((90d, 105d, 10, 1), carousel.Geometry);
        // Three items in view, and one more after them.
        Assert.Equal((0, 3), carousel.RealizedSlots);
        carousel.ScrollToIndex(4, animated: false);
        Assert.Equal(420, carousel.ScrollView.ScrollX);
        Assert.Equal([0d, 105, 210, 315, 420, 525, 630, 735], Scroller(carousel).SnapOffsets(horizontal: true));
        Assert.Equal(4, carousel.Position);
    }

    [Fact]
    public void RightToLeftLaysTheItemsOutFromTheRight()
    {
        var carousel = Carousel(count: 4);
        carousel.FlowDirection = FlowDirection.RightToLeft;
        using var surface = new SkUiTestSurface(carousel, 300, 200);
        surface.Frame(0);
        // The first item shows (the strip starts at its right end), the second one is to its left.
        Assert.Equal(900, carousel.ScrollView.ScrollX);
        Assert.Equal(900, ((SkUiView)carousel.GetRealizedView(0)!.Parent!).Frame.X);
        Assert.Equal(600, ((SkUiView)carousel.GetRealizedView(1)!.Parent!).Frame.X);
        carousel.Position = 2;
        surface.Frame(0);
        surface.Frame(1000);
        Assert.Equal(300, carousel.ScrollView.ScrollX);
    }

    [Fact]
    public void AVerticalCarouselPagesDownwards()
    {
        var carousel = Carousel(count: 4);
        carousel.Orientation = ItemsLayoutOrientation.Vertical;
        using var surface = new SkUiTestSurface(carousel, 200, 300);
        surface.Frame(0);
        Assert.Equal(new Rect(0, 300, 200, 300), ((SkUiView)carousel.GetRealizedView(1)!.Parent!).Frame);
        Swipe(carousel, new Point(100, 250), new Point(100, 100), startMs: 0);
        Run(surface, 30, 1500);
        Assert.Equal(300, carousel.ScrollView.ScrollY);
        Assert.Equal(1, carousel.Position);
    }

    [Fact]
    public void WithoutAHeightTheCarouselIsAsTallAsItsTallestItemInView()
    {
        var carousel = new SkUiCarouselView { Loop = false, ItemsSource = Items(3), ItemTemplate = new DataTemplate(() => new SkUiBox { HeightRequest = 80 }) };
        var column = new SkUiVerticalStackLayout { Children = { carousel } };
        SkUiTestHelpers.Arrange(column, 300, 600);
        Assert.Equal(new Size(300, 80), carousel.Bounds.Size);
    }

    [Fact]
    public void ItemsFollowTheCarouselsHeightRequest()
    {
        // As on the demo page: the carousel in a star row above its indicator, in a grid with a height request.
        var carousel = Carousel(count: 4);
        var indicator = new SkUiIndicatorView { HeightRequest = 16 };
        carousel.IndicatorView = indicator;
        var grid = new SkUiGrid { RowDefinitions = [new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)], RowSpacing = 8, HeightRequest = 260, VerticalOptions = LayoutOptions.Center };
        Grid.SetRow(indicator, 1);
        grid.Children.Add(carousel);
        grid.Children.Add(indicator);
        var host = new SkUiContentView { Content = grid };
        using var surface = new SkUiTestSurface(host, 340, 400);
        surface.Frame(0);
        Assert.Equal(236, carousel.Bounds.Height);
        Assert.Equal(new Size(340, 236), ((SkUiView)carousel.GetRealizedView(0)!).Bounds.Size);

        grid.HeightRequest = 180;
        surface.Frame(16);
        Assert.Equal(156, carousel.Bounds.Height);
        Assert.Equal(new Size(340, 156), ((SkUiView)carousel.GetRealizedView(0)!).Bounds.Size);
        Assert.Equal(new Size(340, 156), ((SkUiView)carousel.GetRealizedView(1)!).Bounds.Size);
    }

    #endregion

    #region Paging

    [Fact]
    public void ASwipeMovesOneItemAndRaisesThePositionEventsOnce()
    {
        var carousel = Carousel(count: 5);
        using var surface = new SkUiTestSurface(carousel, 300, 200);
        surface.Frame(0);
        var positions = new List<(int, int)>();
        var items = new List<object?>();
        carousel.PositionChanged += (_, args) => positions.Add((args.PreviousPosition, args.CurrentPosition));
        carousel.CurrentItemChanged += (_, args) => items.Add(args.CurrentItem);

        Swipe(carousel, new Point(250, 100), new Point(50, 100), startMs: 0);
        Run(surface, 30, 2000);
        Assert.Equal(300, carousel.ScrollView.ScrollX);
        Assert.Equal(1, carousel.Position);
        Assert.Equal([(0, 1)], positions);
        Assert.Equal(["Item 1"], items);
        Assert.False(Scroller(carousel).Dragging, "dragging");
        Assert.False(Scroller(carousel).IsMotionRunning, "motion");
        Assert.False(carousel.IsScrolling);

        // A slow drag of less than half an item settles back.
        var id = ++_pointer;
        carousel.Touch(new(id, SkUiTouchAction.Pressed, new Point(200, 100), TimeSpan.FromMilliseconds(2000)));
        carousel.Touch(new(id, SkUiTouchAction.Moved, new Point(160, 100), TimeSpan.FromMilliseconds(2100)));
        carousel.Touch(new(id, SkUiTouchAction.Moved, new Point(120, 100), TimeSpan.FromMilliseconds(2200)));
        Assert.True(carousel.IsDragging);
        carousel.Touch(new(id, SkUiTouchAction.Released, new Point(120, 100), TimeSpan.FromMilliseconds(2500)));
        Assert.False(carousel.IsDragging);
        Run(surface, 2500, 4000);
        Assert.Equal(300, carousel.ScrollView.ScrollX);
        Assert.Equal(1, carousel.Position);
        Assert.Single(positions);
    }

    [Fact]
    public void SettingThePositionScrollsThereAndKeepsItDuringTheScroll()
    {
        var carousel = Carousel(count: 8);
        using var surface = new SkUiTestSurface(carousel, 300, 200);
        surface.Frame(0);
        var positions = new List<int>();
        carousel.PositionChanged += (_, args) => positions.Add(args.CurrentPosition);
        carousel.Position = 5;
        Assert.Equal([5], positions);
        Assert.Equal("Item 5", carousel.CurrentItem);
        Assert.True(carousel.IsScrolling);
        Run(surface, 0, 1000);
        Assert.Equal(1500, carousel.ScrollView.ScrollX);
        Assert.Equal([5], positions);

        // The current item scrolls there too; an item that is not in the list is ignored.
        carousel.CurrentItem = "Item 2";
        Assert.Equal(2, carousel.Position);
        carousel.CurrentItem = "Missing";
        Assert.Equal("Item 2", carousel.CurrentItem);
        Run(surface, 1000, 2000);
        Assert.Equal(600, carousel.ScrollView.ScrollX);

        // Beyond the last item: the last one.
        carousel.IsScrollAnimated = false;
        carousel.Position = 50;
        Assert.Equal(7, carousel.Position);
        Assert.Equal(2100, carousel.ScrollView.ScrollX);
    }

    [Fact]
    public void APositionSetBeforeTheItemsAppliesOnceTheyCome()
    {
        var carousel = new SkUiCarouselView { Loop = false, Position = 3 };
        using var surface = new SkUiTestSurface(carousel, 300, 200);
        surface.Frame(0);
        carousel.ItemsSource = Items(5);
        surface.Frame(16);
        Assert.Equal(3, carousel.Position);
        Assert.Equal("Item 3", carousel.CurrentItem);
        Assert.Equal(900, carousel.ScrollView.ScrollX);
    }

    [Fact]
    public void SwipingCanBeTurnedOffWhileThePositionStillScrolls()
    {
        var carousel = Carousel(count: 5);
        carousel.IsSwipeEnabled = false;
        using var surface = new SkUiTestSurface(carousel, 300, 200);
        surface.Frame(0);
        Swipe(carousel, new Point(250, 100), new Point(50, 100), startMs: 0);
        Run(surface, 30, 1000);
        Assert.Equal(0, carousel.ScrollView.ScrollX);
        carousel.ScrollToIndex(2, animated: false);
        Assert.Equal(600, carousel.ScrollView.ScrollX);
    }

    #endregion

    #region Looping

    [Fact]
    public void ALoopingCarouselStartsInTheMiddleOfItsStripAndWrapsBothWays()
    {
        var carousel = Carousel(count: 3, loop: true);
        using var surface = new SkUiTestSurface(carousel, 300, 200);
        surface.Frame(0);
        var (_, stride, slots, copies) = carousel.Geometry;
        Assert.True(copies >= 3 && copies % 2 == 1);
        Assert.Equal(3 * copies, slots);
        var home = copies / 2 * 3 * stride;
        Assert.Equal(home, carousel.ScrollView.ScrollX);
        Assert.Equal(0, carousel.Position);
        // The last item shows before the first.
        Assert.Equal("Item 2", ((SkUiView)carousel.GetRealizedView(2)!).BindingContext);

        // Backwards from the first item: the last one.
        Swipe(carousel, new Point(50, 100), new Point(250, 100), startMs: 0);
        Run(surface, 30, 1500);
        Assert.Equal(2, carousel.Position);
        Assert.Equal(home - stride, carousel.ScrollView.ScrollX);
        // Setting the position takes the shorter way round: from the last to the first is one item forward.
        carousel.Position = 0;
        Run(surface, 1500, 2500);
        Assert.Equal(home, carousel.ScrollView.ScrollX);
    }

    [Fact]
    public void ALoopingCarouselScrollsOnWithoutAnEndAndMovesBackToItsMiddle()
    {
        var carousel = Carousel(count: 3, loop: true);
        using var surface = new SkUiTestSurface(carousel, 300, 200);
        surface.Frame(0);
        var (_, stride, slots, _) = carousel.Geometry;
        var length = slots * stride;
        var time = 0d;
        // 40 swipes forward: far past the strip's end, were it not moved back.
        for (var swipe = 0; swipe < 40; swipe++)
        {
            Swipe(carousel, new Point(250, 100), new Point(50, 100), startMs: time);
            Run(surface, time + 30, time + 1500);
            time += 1500;
            Assert.Equal((swipe + 1) % 3, carousel.Position);
            Assert.InRange(carousel.ScrollView.ScrollX, stride, length - 2 * stride);
            Assert.Equal(0, carousel.ScrollView.ScrollX % stride, 3);
        }
        Assert.Equal($"Item {carousel.Position}", carousel.CurrentItem);
    }

    [Fact]
    public void AFlingOfALoopingCarouselGoesOnAcrossTheMoveBackToTheMiddle()
    {
        var carousel = Carousel(count: 3, loop: true);
        carousel.SnapPointsType = SnapPointsType.None;
        using var surface = new SkUiTestSurface(carousel, 300, 200);
        surface.Frame(0);
        var (_, stride, slots, _) = carousel.Geometry;
        var start = carousel.ScrollView.ScrollX;
        var previous = start;
        var travelled = 0d;
        Assert.True(Scroller(carousel).StartFling(new Point(3000, 0)));
        for (var ms = 0; ms < 4000; ms += 16)
        {
            surface.Frame(ms);
            var x = carousel.ScrollView.ScrollX;
            var step = x - previous;
            // Moved back by whole cycles: what shows goes on.
            var cycle = 3 * stride;
            step -= Math.Round(step / cycle) * cycle;
            Assert.InRange(step, -0.5, 200);
            travelled += step;
            previous = x;
            Assert.InRange(x, 0, slots * stride - 300);
        }
        Assert.False(carousel.IsScrolling);
        // A 3000 DIP/s fling travels about 1500 DIPs.
        Assert.InRange(travelled, 1300, 1600);
        Assert.Equal((int)Math.Round(travelled / stride) % 3, carousel.Position);
    }

    [Fact]
    public void ASingleItemDoesNotLoop()
    {
        var carousel = Carousel(count: 1, loop: true);
        using var surface = new SkUiTestSurface(carousel, 300, 200);
        surface.Frame(0);
        Assert.Equal((300d, 300d, 1, 1), carousel.Geometry);
        Assert.Equal(0, carousel.ScrollView.ScrollX);
    }

    #endregion

    #region Items

    [Fact]
    public void CollectionChangesKeepTheCurrentItem()
    {
        var items = Items(5);
        var carousel = new SkUiCarouselView { Loop = false, ItemsSource = items, IsScrollAnimated = false };
        using var surface = new SkUiTestSurface(carousel, 300, 200);
        surface.Frame(0);
        carousel.Position = 2;

        items.Insert(0, "New");
        surface.Frame(16);
        Assert.Equal(3, carousel.Position);
        Assert.Equal("Item 2", carousel.CurrentItem);
        Assert.Equal(900, carousel.ScrollView.ScrollX);

        items.Add("Last");
        surface.Frame(32);
        Assert.Equal(3, carousel.Position);
        Assert.Equal(900, carousel.ScrollView.ScrollX);

        // The current item removed: the position stays (another item there).
        items.RemoveAt(3);
        surface.Frame(48);
        Assert.Equal(3, carousel.Position);
        Assert.Equal("Item 3", carousel.CurrentItem);
        Assert.Equal("Item 3", ((SkUiView)carousel.GetRealizedView(3)!).BindingContext);

        items.Clear();
        surface.Frame(64);
        Assert.Null(carousel.CurrentItem);
        Assert.Equal((-1, -1), carousel.RealizedSlots);
    }

    [Fact]
    public void ItemsAppendedDuringAFlingDoNotStopIt()
    {
        var items = Items(30);
        var carousel = new SkUiCarouselView { Loop = false, ItemsSource = items, SnapPointsType = SnapPointsType.None };
        using var surface = new SkUiTestSurface(carousel, 300, 200);
        surface.Frame(0);
        Assert.True(Scroller(carousel).StartFling(new Point(2000, 0)));
        surface.Frame(0);
        surface.Frame(100);
        var during = carousel.ScrollView.ScrollX;
        items.Add("More");
        surface.Frame(116);
        Assert.True(carousel.IsScrolling);
        Run(surface, 132, 3000);
        Assert.InRange(carousel.ScrollView.ScrollX, during + 300, 1100);
    }

    [Fact]
    public void TheThresholdAsksForMoreItemsOncePerCount()
    {
        var items = Items(6);
        var carousel = new SkUiCarouselView { Loop = false, ItemsSource = items, RemainingItemsThreshold = 1, IsScrollAnimated = false };
        var reached = 0;
        carousel.RemainingItemsThresholdReached += (_, _) => reached++;
        using var surface = new SkUiTestSurface(carousel, 300, 200);
        surface.Frame(0);
        Assert.Equal(0, reached);
        carousel.Position = 4;
        Assert.Equal(1, reached);
        carousel.Position = 5;
        Assert.Equal(1, reached);
        items.Add("Item 6");
        surface.Frame(16);
        Assert.Equal(2, reached);
    }

    [Fact]
    public void TheEmptyViewShowsWithoutItems()
    {
        var empty = new SkUiLabel { Text = "Nothing" };
        var carousel = new SkUiCarouselView { EmptyView = empty };
        using var surface = new SkUiTestSurface(carousel, 300, 200);
        surface.Frame(0);
        Assert.True(empty.IsShown);
        carousel.ItemsSource = Items(2);
        surface.Frame(16);
        Assert.False(empty.IsShown);
    }

    [Fact]
    public void ViewsAreRecycledPerTemplateAsTheCarouselScrolls()
    {
        var created = 0;
        var carousel = new SkUiCarouselView
        {
            Loop = false,
            ItemsSource = Items(50),
            IsScrollAnimated = false,
            ItemTemplate = new DataTemplate(() => { created++; return new SkUiBox(); })
        };
        using var surface = new SkUiTestSurface(carousel, 300, 200);
        surface.Frame(0);
        for (var index = 1; index < 50; index++)
        {
            carousel.Position = index;
            surface.Frame(index * 16);
        }
        Assert.InRange(created, 2, 6);
    }

    [Fact]
    public void ItemViewsTakeTheCurrentNextAndPreviousVisualStates()
    {
        var carousel = Carousel(count: 4, loop: true);
        carousel.ItemExtent = 100;
        using var surface = new SkUiTestSurface(carousel, 300, 200);
        surface.Frame(0);
        Assert.Equal(SkUiCarouselView.CurrentItemVisualState, carousel.ItemVisualState(0));
        Assert.Equal(SkUiCarouselView.NextItemVisualState, carousel.ItemVisualState(1));
        Assert.Equal(SkUiCarouselView.DefaultItemVisualState, carousel.ItemVisualState(2));
        Assert.Equal(SkUiCarouselView.PreviousItemVisualState, carousel.ItemVisualState(3));
        carousel.IsScrollAnimated = false;
        carousel.Position = 1;
        Assert.Equal(SkUiCarouselView.PreviousItemVisualState, carousel.ItemVisualState(0));
        Assert.Equal(SkUiCarouselView.CurrentItemVisualState, carousel.ItemVisualState(1));
        Assert.Equal(SkUiCarouselView.NextItemVisualState, carousel.ItemVisualState(2));
    }

    #endregion
}

/// <summary>The carousel's documented XAML: MAUI's names, a linked indicator, an effect, bindings.</summary>
[Collection(RuntimeXamlCollection.Name)]
public class CarouselViewXamlTests
{
    public sealed class Destination(string name)
    {
        public string Name { get; } = name;
    }

    public sealed class Model
    {
        public ObservableCollection<Destination> Destinations { get; } = [new("Moscow"), new("Algarve"), new("Athens"), new("Dubai")];
        public object? Selected { get; set; }
        public int Loads { get; private set; }
        public Command LoadMoreCommand => new(() => Loads++);
    }

    [Fact]
    public void TheDocumentedXamlLoads()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        const string xaml = """
            <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                         xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <sk:SkUiGrid RowDefinitions="*,Auto" RowSpacing="8">
                <sk:SkUiCarouselView x:Name="Gallery"
                                     ItemsSource="{Binding Destinations}"
                                     CurrentItem="{Binding Selected}"
                                     PeekAreaInsets="40,0"
                                     ItemSpacing="12"
                                     Loop="False"
                                     IndicatorView="{x:Reference Dots}"
                                     RemainingItemsThreshold="1"
                                     RemainingItemsThresholdReachedCommand="{Binding LoadMoreCommand}">
                  <sk:SkUiCarouselView.ItemEffect>
                    <sk:SkUiCoverFlowEffect RotationAngle="45" SideItemScale="0.75" />
                  </sk:SkUiCarouselView.ItemEffect>
                  <DataTemplate>
                    <sk:SkUiBorder StrokeShape="RoundRectangle 16" StrokeThickness="0" BackgroundColor="Teal" Padding="16">
                      <sk:SkUiLabel Text="{Binding Name}" TextColor="White" FontSize="22" VerticalOptions="End" />
                    </sk:SkUiBorder>
                  </DataTemplate>
                </sk:SkUiCarouselView>
                <sk:SkUiIndicatorView x:Name="Dots" Grid.Row="1" HorizontalOptions="Center" />
              </sk:SkUiGrid>
            </ContentView>
            """;
        var model = new Model();
        var root = new ContentView { BindingContext = model };
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var grid = (SkUiGrid)root.Content;
        var carousel = (SkUiCarouselView)grid.Children[0];
        var dots = (SkUiIndicatorView)grid.Children[1];
        using var surface = new SkUiTestSurface(grid, 300, 220);
        surface.Frame(0);

        Assert.Same(dots, carousel.IndicatorView);
        Assert.Equal(4, dots.Count);
        Assert.Equal(45, Assert.IsType<SkUiCoverFlowEffect>(carousel.ItemEffect).RotationAngle);
        Assert.Equal((220d, 232d, 4, 1), carousel.Geometry);
        Assert.Same(model.Destinations[0], model.Selected);
        Assert.Equal("Moscow", ((SkUiLabel)((SkUiBorder)carousel.GetRealizedView(0)!).Content!).Text);
        carousel.IsScrollAnimated = false;
        carousel.Position = 3;
        Assert.Same(model.Destinations[3], model.Selected);
        Assert.Equal(1, model.Loads);
        Assert.Equal(3, dots.Position);
    }
}
