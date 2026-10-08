using System.Collections.ObjectModel;
using System.Collections.Specialized;
using MauiSkiaUi.Rendering;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// FR-21 indexed virtual scrolling (<see cref="SkUiVirtualVerticalStackLayout"/>, <see cref="SkUiVirtualScrollView"/>): realization of
/// the visible window, prefetch and its budget, release and recycling, variable sizes with estimates and scroll anchoring
/// (also during render-thread flings), collection changes, endless factories, the remaining-items threshold, scrolling to an
/// index, nesting.
/// </summary>
[Collection(RuntimeXamlCollection.Name)]
public class VirtualVerticalStackLayoutTests
{
    /// <summary>An item with a height (the item view's height follows it).</summary>
    private sealed record Row(int Index, double Height);

    private sealed class RowView : SkUiBox
    {
        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is Row row)
                HeightRequest = row.Height;
        }
    }

    private sealed class Counter
    {
        public int Created;
    }

    private static DataTemplate RowTemplate(Counter? counter = null) => new(() =>
    {
        if (counter is not null)
            counter.Created++;
        return new RowView();
    });

    private static List<Row> Rows(int count, Func<int, double> height) => Enumerable.Range(0, count).Select(index => new Row(index, height(index))).ToList();

    /// <summary>Heights 30, 60, 90 in turn: an average of 60.</summary>
    private static double Varied(int index) => 30 * (index % 3 + 1);

    /// <summary>Prefetch is not cut short by a slow test machine (the budget has its own test).</summary>
    private static readonly TimeSpan Unlimited = TimeSpan.FromSeconds(10);

    private static SkUiVirtualScrollView List(IEnumerable<Row> rows, Counter? counter = null)
    {
        var list = new SkUiVirtualScrollView
        {
            ItemsSource = rows,
            ItemTemplate = RowTemplate(counter),
            VerticalScrollBarVisibility = ScrollBarVisibility.Never
        };
        list.Items.PrefetchBudget = Unlimited;
        return list;
    }

    /// <summary>Where an item's view shows in the scroller's viewport.</summary>
    private static double ScreenTop(SkUiVirtualScrollView list, int index) =>
        ((View)list.Items.GetRealizedView(index)!).Frame.Y + list.Items.Frame.Y - list.ScrollY;

    [Fact]
    public void FixedExtentRealizesTheVisibleWindowPlusPrefetchOnly()
    {
        var list = List(Rows(10_000, _ => 50));
        list.ItemExtent = 50;
        SkUiTestHelpers.Arrange(list, 300, 500);

        Assert.Equal(10_000 * 50, list.ContentSize.Height);
        // 10 items show; one viewport ahead is prefetched, nothing behind the start.
        Assert.Equal((0, 19), list.Items.RealizedRange);
        Assert.Equal(0, list.FirstVisibleIndex);
        Assert.Equal(9, list.LastVisibleIndex);
        Assert.Equal(new Rect(0, 150, 300, 50), ((View)list.Items.GetRealizedView(3)!).Frame);
        Assert.Equal(10_000 * 50, list.Items.Frame.Height);
    }

    [Fact]
    public void ScrollingRealizesAheadAndReleasesFarItemsIntoThePool()
    {
        var counter = new Counter();
        var list = List(Rows(10_000, _ => 50), counter);
        list.ItemExtent = 50;
        SkUiTestHelpers.Arrange(list, 300, 500);

        list.ScrollTo(0, 100_000); // item 2000
        Assert.Equal(2000, list.FirstVisibleIndex);
        Assert.Equal(2009, list.LastVisibleIndex);
        var (first, last) = list.Items.RealizedRange;
        Assert.True(first <= 2000 && last >= 2019, $"realized {first}..{last}");
        Assert.True(last - first < 60, $"realized {first}..{last}");
        Assert.Null(list.Items.GetRealizedView(0));
        Assert.Equal(new Rect(0, 100_000, 300, 50), ((View)list.Items.GetRealizedView(2000)!).Frame);

        // Once items behind reach the release distance, scrolling on recycles views instead of creating new ones.
        for (var y = 100_000; y < 105_000; y += 250)
            list.ScrollTo(0, y);
        var created = counter.Created;
        for (var y = 105_000; y < 200_000; y += 250)
            list.ScrollTo(0, y);
        Assert.Equal(created, counter.Created);
    }

    [Fact]
    public void ItemsAreRealizedBeforeTheyShow()
    {
        var list = List(Rows(1000, _ => 50));
        list.ItemExtent = 50;
        SkUiTestHelpers.Arrange(list, 300, 500);
        var realizedAt = new Dictionary<int, double>();
        list.ItemRealized += (_, args) => realizedAt[args.Index] = list.ScrollY;

        for (var y = 0; y <= 5000; y += 20)
            list.ScrollTo(0, y);

        // Every item was created while it was still at least half a viewport below the viewport's bottom.
        Assert.NotEmpty(realizedAt);
        foreach (var (index, scrollY) in realizedAt.Where(pair => pair.Key < 100))
            Assert.True(index * 50 - (scrollY + 500) >= 250, $"item {index} created at offset {scrollY}");
    }

    [Fact]
    public void ClockMeasuresTheUiFrameInterval()
    {
        var clock = new SkUiAnimationClock();
        Assert.Equal(SkUiAnimationClock.DefaultFrameInterval, clock.FrameInterval);
        // 120 Hz, with a few frames dropped by a busy UI thread and a pause: the interval stays the frame's.
        var time = 0d;
        foreach (var step in new[] { 8.33, 8.33, 16.67, 8.33, 8.33, 25, 8.33, 500, 8.33, 8.33, 8.33, 16.67 })
            clock.Tick(TimeSpan.FromMilliseconds(time += step));
        Assert.Equal(8.33, clock.FrameInterval.TotalMilliseconds, 2);
        // 60 Hz from then on: the measurement follows once the old samples are gone.
        for (var frame = 0; frame < 16; frame++)
            clock.Tick(TimeSpan.FromMilliseconds(time += 16.67));
        Assert.Equal(16.67, clock.FrameInterval.TotalMilliseconds, 2);
    }

    [Fact]
    public void AutomaticPrefetchBudgetFollowsFrameRateItemCostAndSpeed()
    {
        // Idle: a quarter of the frame, at any frame rate.
        Assert.Equal(16.67 / 4, SkUiVirtualVerticalStackLayout.AutomaticBudget(16.67, scrolling: false, 3000, 50, 2, 1), 3);
        Assert.Equal(8.33 / 4, SkUiVirtualVerticalStackLayout.AutomaticBudget(8.33, scrolling: false, 3000, 50, 2, 1), 3);
        // Scrolling cheap items: the demand (1 item per frame at 0.1 ms) is below the floor.
        Assert.Equal(16.67 / 4, SkUiVirtualVerticalStackLayout.AutomaticBudget(16.67, scrolling: true, 3000, 50, 0.1, 1), 3);
        // Expensive items (a slow device): 3000 DIP/s over 50 DIP rows at 60 Hz is 1 row per frame; 5 ms each, with the margin.
        Assert.Equal(1.0002 * 5 * 1.5, SkUiVirtualVerticalStackLayout.AutomaticBudget(16.67, scrolling: true, 3000, 50, 5, 1), 2);
        // Faster than the frame allows: at most three quarters of it; prefetch falling behind doubles the demand.
        Assert.Equal(16.67 * 0.75, SkUiVirtualVerticalStackLayout.AutomaticBudget(16.67, scrolling: true, 9000, 50, 5, 1), 3);
        Assert.Equal(2 * 1.0002 * 2 * 1.5, SkUiVirtualVerticalStackLayout.AutomaticBudget(16.67, scrolling: true, 3000, 50, 2, 2), 2);
        // Before any item was measured, the floor.
        Assert.Equal(16.67 / 4, SkUiVirtualVerticalStackLayout.AutomaticBudget(16.67, scrolling: true, 3000, 50, double.NaN, 1), 3);
    }

    [Fact]
    public void PrefetchBudgetIsAutomaticByDefault()
    {
        var list = new SkUiVirtualScrollView { ItemsSource = Rows(1000, _ => 50), ItemTemplate = RowTemplate() };
        Assert.Null(list.PrefetchBudget);
        Assert.Null(list.Items.PrefetchBudget);
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.Equal(SkUiAnimationClock.DefaultFrameInterval.TotalMilliseconds / 4, list.Items.LastPrefetchBudget.TotalMilliseconds, 3);
        Assert.False(double.IsNaN(list.Items.ItemCost));

        list.PrefetchBudget = TimeSpan.FromMilliseconds(7);
        list.ScrollTo(0, 100);
        Assert.Equal(TimeSpan.FromMilliseconds(7), list.Items.LastPrefetchBudget);
        list.PrefetchBudget = TimeSpan.FromMilliseconds(-1); // refused
        Assert.Equal(TimeSpan.FromMilliseconds(7), list.Items.PrefetchBudget);
        list.PrefetchBudget = null;
        Assert.Null(list.Items.PrefetchBudget);
    }

    /// <summary>A list control of its own on the base class: a calendar with a year row every 12 months.</summary>
    private sealed class CalendarList : SkUiVirtualVerticalStackLayoutBase
    {
        public int Created, Bound, Unbound;

        public CalendarList(int months)
        {
            PrefetchBudget = Unlimited;
            ResetItems(months);
        }

        public void Append(int months) => InsertItems(ItemCount, months);

        public void Remove(int index, int months) => RemoveItems(index, months);

        protected override object? GetRecycleKey(int index) => index % 12 == 0 ? "year" : "month";

        protected override ISkUiView CreateItemView(int index, object? recycleKey)
        {
            Created++;
            return recycleKey is "year" ? new SkUiLabel { HeightRequest = 80 } : new SkUiBox { HeightRequest = 40 };
        }

        protected override void BindItemView(int index, ISkUiView view, object? recycleKey)
        {
            Bound++;
            // Views of one key are rebound only to items of that key.
            Assert.Equal(index % 12 == 0, view is SkUiLabel);
            ((SkUiView)view).Tag = index;
        }

        protected override void UnbindItemView(int index, ISkUiView view) => Unbound++;

        protected override object? GetItem(int index) => index;
    }

    [Fact]
    public void CustomListsCreateBindAndRecycleThroughTheBaseClass()
    {
        var list = new CalendarList(1200);
        var scroll = new SkUiScrollView { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Never };
        var realized = new List<object?>();
        list.ItemRealized += (_, args) => realized.Add(args.Item);
        SkUiTestHelpers.Arrange(scroll, 300, 500);

        Assert.Equal(1200, list.ItemCount);
        Assert.IsType<SkUiLabel>(list.GetRealizedView(0));
        Assert.Equal(1, ((SkUiView)list.GetRealizedView(1)!).Tag);
        Assert.Equal(new Rect(0, 80, 300, 40), ((View)list.GetRealizedView(1)!).Frame);
        Assert.All(realized, item => Assert.IsType<int>(item));

        // Scrolled on: views are recycled per key, and every released view is unbound.
        for (var y = 0; y < 10_000; y += 200)
            scroll.ScrollTo(0, y);
        var created = list.Created;
        for (var y = 10_000; y < 40_000; y += 200)
            scroll.ScrollTo(0, y);
        Assert.Equal(created, list.Created);
        var (first, last) = list.RealizedRange;
        Assert.Equal(last - first + 1, list.Bound - list.Unbound);

        // Item changes reported by the subclass.
        list.Append(12);
        Assert.Equal(1212, list.ItemCount);
        var shown = list.GetRealizedView(list.FirstVisibleIndex);
        var top = ((View)shown!).Frame.Y - scroll.ScrollY;
        list.Remove(0, 12); // before the viewport: what shows stays
        SkUiTestHelpers.Arrange(scroll, 300, 500);
        Assert.Equal(1200, list.ItemCount);
        Assert.Equal(top, ((View)shown).Frame.Y - scroll.ScrollY, 3);
        Assert.Throws<ArgumentOutOfRangeException>(() => list.Remove(1195, 10));
    }

    /// <summary>A custom list of unknown length: lines appear until there are no more.</summary>
    private sealed class EndlessLog : SkUiVirtualVerticalStackLayoutBase
    {
        private int _length = 75;

        public EndlessLog()
        {
            PrefetchBudget = Unlimited;
            ResetItems(0, hasMoreItems: true);
        }

        public void More(int lines)
        {
            _length += lines;
            HasMoreItems = true;
        }

        protected override object? GetRecycleKey(int index) => "line";

        // A new item: take a recycled view when one waits (the layout does it for known items).
        protected override ISkUiView? CreateItemView(int index, object? recycleKey) =>
            index < _length ? TakeRecycledView("line") ?? new SkUiBox { HeightRequest = 20 } : null;
    }

    [Fact]
    public void EndlessCustomListsGrowUntilTheyEnd()
    {
        var log = new EndlessLog();
        var scroll = new SkUiScrollView { Content = log, VerticalScrollBarVisibility = ScrollBarVisibility.Never };
        void ScrollToEnd()
        {
            for (var step = 0; step < 50; step++)
            {
                scroll.ScrollTo(0, scroll.ScrollY + 400);
                SkUiTestHelpers.Arrange(scroll, 300, 400);
            }
        }
        SkUiTestHelpers.Arrange(scroll, 300, 400);
        ScrollToEnd();
        Assert.Equal(75, log.ItemCount);
        Assert.Equal(75 * 20 - 400, scroll.ScrollY);

        log.More(25);
        ScrollToEnd();
        Assert.Equal(100, log.ItemCount);
        Assert.Equal(99, log.LastVisibleIndex);
    }

    [Fact]
    public void ShownWithoutADrawnScrollerIsReportedOnce()
    {
        var listener = new RecordingTraceListener("has no drawn scroller");
        System.Diagnostics.Trace.Listeners.Add(listener);
        try
        {
            var alone = new SkUiVirtualVerticalStackLayout { ItemsSource = Rows(100, _ => 50), ItemTemplate = RowTemplate(), AutomationId = "lonely-list" };
            var page = new SkUiContentView { Content = new SkUiVerticalStackLayout { Children = { alone } } };
            SkUiTestHelpers.Arrange(page, 300, 500); // not on a surface yet: a tree being built is not reported
            Assert.Empty(listener.Messages);
            using (var surface = new SkUiTestSurface(page, 300, 500))
            {
                // Laid out again on the live surface (twice: reported once).
                alone.ItemSpacing = 1;
                surface.Frame(0);
                alone.ItemSpacing = 2;
                surface.Frame(16);
            }
            var message = Assert.Single(listener.Messages);
            Assert.StartsWith("SkiaUi: SkUiVirtualVerticalStackLayout 'lonely-list' has no drawn scroller (SkUiScrollView) above it", message);

            var scrolled = new SkUiVirtualVerticalStackLayout { ItemsSource = Rows(100, _ => 50), ItemTemplate = RowTemplate(), AutomationId = "lonely-list" };
            var scroll = new SkUiScrollView { Content = new SkUiVerticalStackLayout { Children = { new SkUiBox { HeightRequest = 100 }, scrolled } } };
            using (var surface = new SkUiTestSurface(scroll, 300, 500))
            {
                scrolled.ItemSpacing = 1;
                surface.Frame(0);
            }
            var list = new SkUiVirtualScrollView { ItemsSource = Rows(100, _ => 50) };
            list.Items.AutomationId = "lonely-list";
            using (var surface = new SkUiTestSurface(list, 300, 500))
            {
                list.ItemSpacing = 1;
                surface.Frame(0);
            }
            Assert.Single(listener.Messages);
        }
        finally
        {
            System.Diagnostics.Trace.Listeners.Remove(listener);
        }
    }

    private sealed class RecordingTraceListener(string filter) : System.Diagnostics.TraceListener
    {
        public List<string> Messages { get; } = [];

        public override void Write(string? message) { }

        public override void WriteLine(string? message)
        {
            if (message?.Contains(filter) == true && message.Contains("'lonely-list'"))
                lock (Messages)
                    Messages.Add(message);
        }
    }

    [Fact]
    public void ListsLoadFromXaml()
    {
        const string xaml = """
            <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                         xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <sk:SkUiScrollView>
                <sk:SkUiVerticalStackLayout>
                  <sk:SkUiVirtualScrollView x:Name="Fixed" ItemsSource="{Binding}" ItemSpacing="4" PrefetchBudget="0:0:0.004" HeightRequest="200">
                    <DataTemplate><sk:SkUiLabel Text="{Binding}" /></DataTemplate>
                  </sk:SkUiVirtualScrollView>
                  <sk:SkUiVirtualVerticalStackLayout x:Name="Automatic" ItemsSource="{Binding}" PrefetchBudget="{x:Null}" ItemExtent="30" />
                </sk:SkUiVerticalStackLayout>
              </sk:SkUiScrollView>
            </ContentView>
            """;
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var root = new ContentView { BindingContext = Enumerable.Range(0, 100).Select(index => $"Row {index}").ToList() };
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var fixedBudget = root.FindByName<SkUiVirtualScrollView>("Fixed");
        var automatic = root.FindByName<SkUiVirtualVerticalStackLayout>("Automatic");
        Assert.Equal(TimeSpan.FromMilliseconds(4), fixedBudget.PrefetchBudget);
        Assert.Equal(TimeSpan.FromMilliseconds(4), fixedBudget.Items.PrefetchBudget);
        Assert.NotNull(fixedBudget.ItemTemplate); // the element's content
        Assert.Null(automatic.PrefetchBudget);
        Assert.Equal(100, fixedBudget.ItemCount);
        Assert.Equal(100, automatic.ItemCount);
    }

    [Fact]
    public void PrefetchBudgetSpreadsPrefetchOverFrames()
    {
        var list = List(Rows(1000, _ => 50));
        list.ItemExtent = 50;
        list.Items.PrefetchBudget = TimeSpan.Zero; // one prefetched item per frame
        SkUiTestHelpers.Arrange(list, 300, 500);

        // The visible items are created at once, then one prefetched item per pass.
        var (_, last) = list.Items.RealizedRange;
        Assert.InRange(last, 9, 12);
        var clock = list.AnimationClock;
        Assert.True(clock.IsRunning);
        for (var frame = 1; frame <= 30 && clock.IsRunning; frame++)
            clock.Tick(TimeSpan.FromMilliseconds(16 * frame));
        Assert.Equal((0, 19), list.Items.RealizedRange);
        Assert.False(clock.IsRunning);
    }

    [Fact]
    public void VariableSizesAreMeasuredAndUnmeasuredItemsEstimated()
    {
        var list = List(Rows(1000, Varied));
        SkUiTestHelpers.Arrange(list, 300, 500);

        var sizes = list.Items.Sizes;
        var (first, last) = list.Items.RealizedRange;
        Assert.Equal(0, first);
        for (var index = first; index <= last; index++)
            Assert.Equal(Varied(index), sizes.SizeOf(index));
        // The rest is estimated from the measured average (60).
        Assert.Equal(60, sizes.Estimate, 1);
        var measured = Enumerable.Range(0, last + 1).Sum(Varied);
        Assert.Equal(measured + (1000 - last - 1) * sizes.Estimate, list.Items.Frame.Height, 3);
        Assert.Equal(new Rect(0, 30 + 60 + 90, 300, 30), ((View)list.Items.GetRealizedView(3)!).Frame);
    }

    [Fact]
    public void EstimatedItemSizeReplacesTheAverageForUnmeasuredItems()
    {
        var list = List(Rows(1000, Varied));
        list.EstimatedItemSize = 100;
        SkUiTestHelpers.Arrange(list, 300, 500);
        var sizes = list.Items.Sizes;
        var measured = Enumerable.Range(0, 1000).Where(sizes.IsMeasured).ToList();
        Assert.NotEmpty(measured);
        Assert.Equal(100, sizes.Estimate);
        Assert.Equal(measured.Sum(Varied) + (1000 - measured.Count) * 100, sizes.TotalLength, 3);
    }

    [Fact]
    public void ScrollingUpThroughEstimatedItemsKeepsTheVisibleItemInPlace()
    {
        var list = List(Rows(2000, Varied));
        list.EstimatedItemSize = 45; // every real size differs from the estimate
        SkUiTestHelpers.Arrange(list, 300, 500);

        list.ScrollToIndex(1000, ScrollToPosition.Start, animated: false);
        Assert.Equal(1000, list.FirstVisibleIndex);
        Assert.Equal(0, ScreenTop(list, 1000), 3);

        // Items above are measured on the way up; the anchored item moves exactly as far as the content was scrolled.
        for (var step = 1; step <= 20; step++)
        {
            var before = ScreenTop(list, 1000);
            list.ScrollTo(0, list.ScrollY - 20);
            Assert.Equal(before + 20, ScreenTop(list, 1000), 3);
        }
    }

    [Fact]
    public void ScrollToIndexPlacesTheItemExactly()
    {
        var list = List(Rows(2000, Varied));
        SkUiTestHelpers.Arrange(list, 300, 500);

        list.ScrollToIndex(700, ScrollToPosition.Center, animated: false);
        var view = (View)list.Items.GetRealizedView(700)!;
        Assert.Equal(250 - view.Height / 2, ScreenTop(list, 700), 3);

        list.ScrollToIndex(1500, ScrollToPosition.End, animated: false);
        view = (View)list.Items.GetRealizedView(1500)!;
        Assert.Equal(500 - view.Height, ScreenTop(list, 1500), 3);

        // Already fully visible: MakeVisible does not scroll.
        var offset = list.ScrollY;
        list.ScrollToIndex(1499, ScrollToPosition.MakeVisible, animated: false);
        Assert.Equal(offset, list.ScrollY);

        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = list.ScrollToIndex(2000); });
    }

    [Fact]
    public void AnimatedScrollToIndexLandsOnTheItem()
    {
        var list = List(Rows(2000, Varied));
        list.EstimatedItemSize = 45;
        using var surface = new SkUiTestSurface(list, 300, 500);
        surface.Frame(0);

        var task = list.ScrollToIndex(600, ScrollToPosition.Start, animated: true);
        for (var time = 16; time <= 1000 && !task.IsCompleted; time += 16)
            surface.Frame(time);
        Assert.True(task.IsCompletedSuccessfully);
        Assert.Equal(0, ScreenTop(list, 600), 1);
    }

    [Fact]
    public void AnimatedScrollToAFarIndexShowsTheItemsTheUiRealized()
    {
        // The demo's list: 94,144 rows of four heights, estimated from the measured average (no EstimatedItemSize), so every
        // row measured on the way moves the estimated position of the target, and the animation is corrected many times.
        var list = new SkUiVirtualScrollView
        {
            ItemsSource = Rows(94_144, index => 44 + index % 4 * 18),
            ItemTemplate = new DataTemplate(() => new RowView { Color = Colors.Red }),
            VerticalScrollBarVisibility = ScrollBarVisibility.Never
        };
        using var surface = new SkUiTestSurface(list, 300, 320);
        surface.Frame(0);

        var task = list.ScrollToIndex(47_072, ScrollToPosition.Start, animated: true);
        var time = 0;
        var trace = new System.Text.StringBuilder();
        for (time = 16; time <= 2000 && !task.IsCompleted; time += 16)
        {
            surface.Frame(time);
            trace.AppendLine($"{time}: ui {list.ScrollY:F0} render {((Rendering.ISkUiRenderable)list).RenderState.Node.Props.ChildrenOffsetY:F0} max {list.ContentSize.Height - 320:F0} motion {list.IsMotionRunning} visible {list.FirstVisibleIndex} realized {list.Items.RealizedRange} task {task.Status}");
        }
        Assert.True(task.IsCompletedSuccessfully, trace.ToString());
        for (var frame = 0; frame < 5; frame++)
            surface.Frame(time += 16);

        // The render thread shows the offset the UI realized items for, and the rows are drawn.
        // 3.3 million DIPs down, render-thread floats resolve a quarter of a DIP: both offsets agree within it, and the item
        // lands within a DIP.
        var shown = ((Rendering.ISkUiRenderable)list).RenderState.Node.Props.ChildrenOffsetY;
        Assert.InRange(shown - list.ScrollY, -1, 1);
        Assert.InRange(ScreenTop(list, 47_072), -1, 1);
        Assert.Equal(SkiaSharp.SKColors.Red, surface.Bitmap.GetPixel(150, 160));
    }

    [Fact]
    public void AnimatedScrollAcrossThousandsOfItemsCreatesOnlyWhatShowsOnTheWay()
    {
        // The demo's list (10,000 rows of four heights, estimated from the average): the animation passes ~15,000 DIPs per
        // frame, so each row on the way shows for one frame at most. Only those rows are realized (no prefetch, no row chased
        // by a moving estimate), and their views are recycled.
        var created = 0;
        var list = new SkUiVirtualScrollView
        {
            ItemsSource = Rows(10_000, index => 44 + index % 4 * 18),
            ItemTemplate = new DataTemplate(() => { created++; return new RowView(); }),
            VerticalScrollBarVisibility = ScrollBarVisibility.Never
        };
        var realized = 0;
        list.ItemRealized += (_, _) => realized++;
        using var surface = new SkUiTestSurface(list, 300, 320);
        surface.Frame(0);
        surface.Frame(16);
        var (createdBefore, realizedBefore) = (created, realized);

        var task = list.ScrollToIndex(5000, ScrollToPosition.Start, animated: true);
        var mostInAFrame = 0;
        for (var time = 32; time <= 2000 && !task.IsCompleted; time += 16)
        {
            var before = realized;
            surface.Frame(time);
            mostInAFrame = Math.Max(mostInAFrame, realized - before);
        }

        Assert.True(task.IsCompletedSuccessfully);
        Assert.Equal(5000, list.FirstVisibleIndex);
        Assert.InRange(ScreenTop(list, 5000), -0.5, 0.5);
        // About 5 rows show; the last frames, slowing down, prefetch around the item again.
        Assert.True(mostInAFrame <= 20, $"{mostInAFrame} items realized in one frame");
        Assert.True(realized - realizedBefore <= 200, $"{realized - realizedBefore} items realized on the way");
        Assert.True(created - createdBefore <= 40, $"{created - createdBefore} views created on the way");
    }

    [Fact]
    public void AnAnimatedScrollThatEndsAfterANewerScrollDoesNotOverrideIt()
    {
        var list = List(Rows(2000, Varied));
        list.EstimatedItemSize = 45;
        using var surface = new SkUiTestSurface(list, 300, 500);
        surface.Frame(0);
        var task = list.ScrollToIndex(1500, ScrollToPosition.Start, animated: true);
        surface.Frame(16); // the animation is committed

        // The render thread finishes it; its completion has not reached the UI thread yet, which scrolls elsewhere first.
        using var canvas = new SKCanvas(surface.Bitmap);
        surface.Renderer.Render(canvas, surface.Bitmap.Info, TimeSpan.FromMilliseconds(2000));
        list.ScrollTo(0, 1234);
        surface.PumpUi();
        surface.Frame(2016);

        Assert.True(task.IsCanceled);
        Assert.InRange(list.ScrollY, 0, 3000); // not item 1500, ~67,000 DIPs down
        Assert.Equal(list.ScrollY, ((ISkUiRenderable)list).RenderState.Node.Props.ChildrenOffsetY, 0);
    }

    [Fact]
    public void ACorrectionReachingTheRenderThreadAfterTheMotionEndedIsShownOnce()
    {
        var scroll = new SkUiScrollView { Content = new SkUiBox { HeightRequest = 10_000 }, VerticalScrollBarVisibility = ScrollBarVisibility.Never };
        using var surface = new SkUiTestSurface(scroll, 100, 500);
        surface.Frame(0);
        _ = scroll.ScrollToAsync(0, 1000, animated: true);
        surface.Frame(16); // the animation is committed and starts

        // The render thread finishes the animation at 1000; the UI thread has not had its last report yet when it corrects
        // the offset (anchoring) and changes another property in the same frame.
        using var canvas = new SKCanvas(surface.Bitmap);
        surface.Renderer.Render(canvas, surface.Bitmap.Info, TimeSpan.FromMilliseconds(1000));
        scroll.CorrectScrollOffset(0, 10);
        scroll.Opacity = 0.9;
        surface.Renderer.PresentFrame();
        surface.Renderer.Render(canvas, surface.Bitmap.Info, TimeSpan.FromMilliseconds(1016));
        surface.PumpUi();

        // Shown once, from where the render thread was (not twice, not from the UI's older offset).
        Assert.Equal(1010, scroll.ScrollY, 1);
        Assert.Equal(1010, ((ISkUiRenderable)scroll).RenderState.Node.Props.ChildrenOffsetY, 1);
    }

    [Fact]
    public void FlingKeepsRunningThroughScrollCorrections()
    {
        var scroll = new SkUiScrollView { Content = new SkUiBox { HeightRequest = 100_000 }, VerticalScrollBarVisibility = ScrollBarVisibility.Never };
        using var surface = new SkUiTestSurface(scroll, 100, 500);
        surface.Frame(0);
        scroll.ScrollTo(0, 50_000);
        surface.Frame(10);
        scroll.Touch(new(1, SkUiTouchAction.Pressed, new Point(50, 100), TimeSpan.FromMilliseconds(20)));
        scroll.Touch(new(1, SkUiTouchAction.Moved, new Point(50, 200), TimeSpan.FromMilliseconds(30)));
        scroll.Touch(new(1, SkUiTouchAction.Moved, new Point(50, 300), TimeSpan.FromMilliseconds(40)));
        scroll.Touch(new(1, SkUiTouchAction.Released, new Point(50, 300), TimeSpan.FromMilliseconds(45)));
        Assert.True(scroll.IsMotionRunning);
        surface.Frame(100);
        surface.Frame(200);
        var before = scroll.ScrollY;
        Assert.True(before < 49_800);

        // Content before the viewport grew by 1000: the fling continues from the corrected offset.
        scroll.CorrectScrollOffset(0, 1000);
        Assert.Equal(before + 1000, scroll.ScrollY);
        surface.Frame(200); // same time: the fling has not moved, only the correction
        Assert.True(scroll.IsMotionRunning);
        Assert.Equal(before + 1000, scroll.ScrollY, 1);
        surface.Frame(300);
        Assert.True(scroll.IsMotionRunning);
        Assert.InRange(scroll.ScrollY, before + 1000 - 2000, before + 1000);
    }

    [Fact]
    public void FlingUpThroughVariableItemsKeepsTheAnchorsInPlace()
    {
        var list = List(Rows(5000, Varied));
        list.EstimatedItemSize = 45;
        using var surface = new SkUiTestSurface(list, 300, 500);
        surface.Frame(0);
        list.ScrollToIndex(3000, ScrollToPosition.Start, animated: false);
        surface.Frame(10);
        list.Touch(new(1, SkUiTouchAction.Pressed, new Point(50, 100), TimeSpan.FromMilliseconds(20)));
        list.Touch(new(1, SkUiTouchAction.Moved, new Point(50, 200), TimeSpan.FromMilliseconds(30)));
        list.Touch(new(1, SkUiTouchAction.Moved, new Point(50, 300), TimeSpan.FromMilliseconds(40)));
        list.Touch(new(1, SkUiTouchAction.Released, new Point(50, 300), TimeSpan.FromMilliseconds(45)));
        Assert.True(list.IsMotionRunning);
        for (var time = 60; time <= 600; time += 16)
        {
            surface.Frame(time);
            // What shows is realized, at its measured size.
            for (var index = list.FirstVisibleIndex; index <= list.LastVisibleIndex; index++)
            {
                Assert.NotNull(list.Items.GetRealizedView(index));
                Assert.True(list.Items.Sizes.IsMeasured(index));
            }
        }
        Assert.True(list.FirstVisibleIndex < 3000);
    }

    [Fact]
    public void EndlessFactoryAsksUntilItReturnsNull()
    {
        var asked = new List<int>();
        var list = new SkUiVirtualScrollView { VerticalScrollBarVisibility = ScrollBarVisibility.Never };
        list.Items.PrefetchBudget = Unlimited;
        list.SetItemFactory(index =>
        {
            asked.Add(index);
            return index < 100 ? new SkUiBox { HeightRequest = 40 } : null;
        });
        SkUiTestHelpers.Arrange(list, 300, 400);
        Assert.Equal(20, list.Items.ItemCount); // the window and one viewport ahead
        Assert.Equal(20 * 40, list.Items.DesiredSize.Height);

        for (var y = 0; y < 10_000; y += 100)
        {
            list.ScrollTo(0, y);
            SkUiTestHelpers.Arrange(list, 300, 400);
        }
        Assert.Equal(100, list.Items.ItemCount);
        Assert.Equal(1, asked.Count(index => index == 100)); // asked once past the end
        Assert.Equal(100 * 40 - 400, list.ScrollY);
        Assert.Equal(99, list.LastVisibleIndex);
    }

    [Fact]
    public void FactoryWithCountKnowsItsExtentUpFront()
    {
        var list = new SkUiVirtualScrollView { ItemExtent = 40 };
        list.SetItemFactory(_ => new SkUiBox(), count: 1000);
        SkUiTestHelpers.Arrange(list, 300, 400);
        Assert.Equal(1000, list.Items.ItemCount);
        Assert.Equal(40_000, list.ContentSize.Height);
    }

    [Fact]
    public void RemainingItemsThresholdFiresOncePerPage()
    {
        var rows = new ObservableCollection<Row>(Rows(50, _ => 50));
        var list = List(rows);
        list.ItemExtent = 50;
        list.RemainingItemsThreshold = 5;
        var reached = 0;
        list.RemainingItemsThresholdReached += (_, _) => reached++;
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.Equal(0, reached);

        list.ScrollTo(0, 2000); // last visible: 49
        Assert.Equal(1, reached);
        list.ScrollTo(0, 1800);
        list.ScrollTo(0, 2000);
        Assert.Equal(1, reached);

        foreach (var row in Rows(50, _ => 50))
            rows.Add(row with { Index = row.Index + 50 });
        SkUiTestHelpers.Arrange(list, 300, 500);
        list.ScrollTo(0, 4500);
        Assert.Equal(2, reached);
    }

    [Fact]
    public void InsertingAboveTheViewportKeepsWhatShows()
    {
        var rows = new ObservableCollection<Row>(Rows(500, Varied));
        var list = List(rows);
        SkUiTestHelpers.Arrange(list, 300, 500);
        list.ScrollToIndex(200, ScrollToPosition.Start, animated: false);
        var view = list.Items.GetRealizedView(200);

        rows.Insert(10, new Row(-1, 80));
        rows.Insert(0, new Row(-2, 80));
        SkUiTestHelpers.Arrange(list, 300, 500);

        Assert.Same(view, list.Items.GetRealizedView(202));
        Assert.Equal(0, ScreenTop(list, 202), 3);
        Assert.Equal(202, list.FirstVisibleIndex);
    }

    [Fact]
    public void InsertingAtTheTopWhileAtTheTopShowsTheNewItem()
    {
        var rows = new ObservableCollection<Row>(Rows(100, _ => 50));
        var list = List(rows);
        SkUiTestHelpers.Arrange(list, 300, 500);
        var first = list.Items.GetRealizedView(0);

        rows.Insert(0, new Row(-1, 50));
        SkUiTestHelpers.Arrange(list, 300, 500);

        Assert.Equal(0, list.ScrollY);
        Assert.Same(first, list.Items.GetRealizedView(1));
        Assert.Equal(new Rect(0, 0, 300, 50), ((View)list.Items.GetRealizedView(0)!).Frame);
        Assert.Equal(new Rect(0, 50, 300, 50), ((View)first!).Frame);
    }

    /// <summary>A list that raises Add without an index (allowed: the position is unknown).</summary>
    private sealed class UnindexedList : List<Row>, INotifyCollectionChanged
    {
        public event NotifyCollectionChangedEventHandler? CollectionChanged;

        public void Prepend(Row row)
        {
            Insert(0, row);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, row));
        }
    }

    [Fact]
    public void AnAddWithoutAnIndexStartsOver()
    {
        var rows = new UnindexedList();
        rows.AddRange(Rows(100, _ => 50));
        var list = List(rows);
        SkUiTestHelpers.Arrange(list, 300, 500);

        rows.Prepend(new Row(-1, 50)); // not at the end: the views must not keep their old indices
        SkUiTestHelpers.Arrange(list, 300, 500);

        Assert.Equal(101, list.ItemCount);
        var (first, last) = list.Items.RealizedRange;
        for (var index = first; index <= last; index++)
            Assert.Same(rows[index], ((BindableObject)list.Items.GetRealizedView(index)!).BindingContext);
    }

    [Fact]
    public void MovedItemsKeepTheirMeasuredSizes()
    {
        var rows = new ObservableCollection<Row>(Rows(100, Varied));
        var list = List(rows);
        SkUiTestHelpers.Arrange(list, 300, 500);
        var sizes = list.Items.Sizes;
        Assert.True(sizes.IsMeasured(3));
        var total = sizes.TotalLength;

        rows.Move(3, 80); // a measured 30 DIP item far below the realized range
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.True(sizes.IsMeasured(80));
        Assert.Equal(30, sizes.SizeOf(80));
        Assert.Equal(total, sizes.TotalLength, 3); // not re-estimated

        rows.Move(80, 5); // back among the realized items: realized there, at its size
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.Equal(total, sizes.TotalLength, 3);
        var moved = (View)list.Items.GetRealizedView(5)!;
        Assert.Same(rows[5], moved.BindingContext);
        Assert.Equal(30, moved.Height);
        var (first, last) = list.Items.RealizedRange;
        for (var index = first; index <= last; index++)
            Assert.Same(rows[index], ((BindableObject)list.Items.GetRealizedView(index)!).BindingContext);
    }

    [Fact]
    public void ReplacingTheItemsSourceReleasesTheOldItemsFromRecycledViews()
    {
        var old = Rows(1000, _ => 50);
        var list = List(old);
        list.ItemExtent = 50;
        SkUiTestHelpers.Arrange(list, 300, 500);

        list.ItemsSource = Rows(3, _ => 50);
        SkUiTestHelpers.Arrange(list, 300, 500);

        Assert.NotEmpty(list.Items.RecycledViews);
        Assert.All(list.Items.RecycledViews, view => Assert.DoesNotContain(((BindableObject)view).BindingContext, old.Cast<object?>()));
    }

    [Fact]
    public void TheContentOfAVirtualScrollViewCannotBeReplaced()
    {
        var list = List(Rows(10, _ => 50));
        Assert.Throws<InvalidOperationException>(() => list.Content = new SkUiBox());
        Assert.Same(list.Items, list.Content);
        // Past the property's check (a binding, a style): put back, reported as a trace line.
        list.SetValue(SkUiContentView.ContentProperty, new SkUiBox());
        Assert.Same(list.Items, list.Content);
        Assert.Same(list, list.Items.Parent);
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.Equal(10 * 50, list.ContentSize.Height);
    }

    /// <summary>Changes its measure again on each of its first <see cref="Remaining"/> arranges.</summary>
    private sealed class RestlessView : SkUiView
    {
        public int Remaining;

        protected override void ArrangeContent(Size size)
        {
            if (Remaining-- > 0)
                InvalidateMeasureFromChild();
        }
    }

    [Fact]
    public void ARelayoutStillPendingAfterItsPassesGetsTheNextFrame()
    {
        var restless = new RestlessView();
        var root = new SkUiContentView { Content = restless };
        using var surface = new SkUiTestSurface(root, 100, 100);
        surface.Frame(0);

        restless.Remaining = 7; // more than the passes of one frame
        restless.InvalidateMeasureFromChild();
        surface.Frame(16); // three passes, then a new frame is requested and runs (the UI queue is pumped)
        surface.Frame(32);

        Assert.True(restless.Remaining < 0, $"{restless.Remaining} relayouts left");
        Assert.False(root.RelayoutPending);
    }

    [Fact]
    public void CollectionChangesTouchOnlyTheirItems()
    {
        var counter = new Counter();
        var rows = new ObservableCollection<Row>(Rows(100, _ => 50));
        var list = List(rows, counter);
        SkUiTestHelpers.Arrange(list, 300, 500);
        var views = Enumerable.Range(0, 10).Select(index => list.Items.GetRealizedView(index)).ToArray();
        var released = new List<int>();
        list.ItemReleased += (_, args) => released.Add(args.Index);

        var removed = rows[3];
        rows.RemoveAt(3);
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.Equal([3], released);
        Assert.NotSame(removed, ((BindableObject)views[3]!).BindingContext); // recycled for another item
        Assert.Same(views[4], list.Items.GetRealizedView(3));
        Assert.Equal(new Rect(0, 150, 300, 50), ((View)views[4]!).Frame);

        rows.Insert(5, new Row(-1, 120));
        SkUiTestHelpers.Arrange(list, 300, 500);
        var inserted = (View)list.Items.GetRealizedView(5)!;
        Assert.Equal(new Rect(0, 250, 300, 120), inserted.Frame);
        Assert.Same(views[5], list.Items.GetRealizedView(4));
        Assert.Same(views[6], list.Items.GetRealizedView(6));
        Assert.Equal(new Rect(0, 370, 300, 50), ((View)views[6]!).Frame);

        rows[0] = new Row(-2, 70);
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.Equal(70, ((View)list.Items.GetRealizedView(0)!).Height);
        Assert.Equal(new Rect(0, 70, 300, 50), ((View)views[1]!).Frame);

        rows.Move(1, 0);
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.Equal(new Rect(0, 0, 300, 50), ((View)list.Items.GetRealizedView(0)!).Frame);
        Assert.Same(rows[0], ((BindableObject)list.Items.GetRealizedView(0)!).BindingContext);
        Assert.Same(rows[1], ((BindableObject)list.Items.GetRealizedView(1)!).BindingContext);

        rows.Clear();
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.Equal((-1, -1), list.Items.RealizedRange);
        Assert.Equal(0, list.ContentSize.Height);
    }

    [Fact]
    public void HandlersMayChangeTheItemsWhileItemsAreRealized()
    {
        var rows = new ObservableCollection<Row>(Rows(30, _ => 50));
        var list = List(rows);
        list.ItemExtent = 50;
        // Loads the next page when the last item is realized (during the realization pass).
        list.ItemRealized += (_, args) =>
        {
            if (args.Index == rows.Count - 1)
                for (var index = 0; index < 30; index++)
                    rows.Add(new Row(rows.Count, 50));
        };
        SkUiTestHelpers.Arrange(list, 300, 500);
        for (var y = 0; y <= 20_000; y += 250)
        {
            list.ScrollTo(0, y);
            SkUiTestHelpers.Arrange(list, 300, 500);
        }

        Assert.True(rows.Count > 400);
        Assert.Equal(rows.Count, list.Items.ItemCount);
        var (first, last) = list.Items.RealizedRange;
        Assert.InRange(400, first, last);
        for (var index = first; index <= last; index++)
            Assert.Same(rows[index], ((BindableObject)list.Items.GetRealizedView(index)!).BindingContext);
    }

    [Fact]
    public void ItemSizeChangesMoveTheItemsBelowAndKeepTheAnchor()
    {
        var rows = Rows(200, _ => 50);
        var list = List(rows);
        SkUiTestHelpers.Arrange(list, 300, 500);
        list.ScrollTo(0, 1000); // item 20 at the top
        var above = (View)list.Items.GetRealizedView(19)!;
        var below = (View)list.Items.GetRealizedView(25)!;

        above.HeightRequest = 150; // before the viewport: what shows stays
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.Equal(0, ScreenTop(list, 20), 3);
        Assert.Equal(1100, list.ScrollY);

        var visible = (View)list.Items.GetRealizedView(22)!;
        visible.HeightRequest = 100; // inside the viewport: the items below move down
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.Equal(0, ScreenTop(list, 20), 3);
        Assert.Equal(25 * 50 + 100 + 50, below.Frame.Y);
    }

    [Fact]
    public void NestedBelowAHeaderRealizesOnlyWhatTheViewportCanShow()
    {
        var items = new SkUiVirtualVerticalStackLayout { ItemsSource = Rows(1000, _ => 50), ItemTemplate = RowTemplate(), ItemExtent = 50, PrefetchBudget = Unlimited };
        var scroll = new SkUiScrollView
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Never,
            Content = new SkUiVerticalStackLayout { Children = { new SkUiBox { HeightRequest = 300 }, items } }
        };
        SkUiTestHelpers.Arrange(scroll, 300, 500);
        SkUiTestHelpers.Arrange(scroll, 300, 500);
        Assert.Equal(300 + 50_000, scroll.ContentSize.Height);
        Assert.Equal(0, items.FirstVisibleIndex);
        Assert.Equal(3, items.LastVisibleIndex); // 200 DIPs below the header
        // The first measure does not know where the list will be: it realizes as if at the viewport's top.
        Assert.True(items.RealizedRange.Last <= 19);

        scroll.ScrollTo(0, 300 + 5000);
        Assert.Equal(100, items.FirstVisibleIndex);
        Assert.Equal(109, items.LastVisibleIndex);
        Assert.Null(items.GetRealizedView(0));

        // A virtual stack inside a horizontal carousel item, inside the vertical list: windows nest.
        scroll.ScrollTo(0, 0);
        Assert.Equal(0, items.FirstVisibleIndex);
    }

    [Fact]
    public void WidthChangeMeasuresItemsAgain()
    {
        var list = new SkUiVirtualScrollView
        {
            ItemsSource = Enumerable.Range(0, 100).Select(index => new string('x', 10 + index % 40)).ToList()
        };
        using var font = SkUiTestHelpers.UseBundledFont();
        list.ItemTemplate = new DataTemplate(() =>
        {
            var label = new SkUiLabel { FontFamily = SkUiTestHelpers.BundledFontFamily, LineBreakMode = LineBreakMode.WordWrap };
            label.SetBinding(SkUiLabel.TextProperty, Binding.SelfPath);
            return label;
        });
        SkUiTestHelpers.Arrange(list, 400, 500);
        var wide = list.Items.Sizes.SizeOf(39);
        SkUiTestHelpers.Arrange(list, 100, 500);
        SkUiTestHelpers.Arrange(list, 100, 500);
        Assert.True(list.Items.Sizes.SizeOf(39) > wide);
    }

    [Fact]
    public void ItemsAreHitTestedAndTapped()
    {
        var list = List(Rows(100, _ => 50));
        list.ItemExtent = 50;
        SkUiTestHelpers.Arrange(list, 300, 500);
        list.ScrollTo(0, 1000);
        var tapped = -1;
        var view = (SkUiView)list.Items.GetRealizedView(22)!;
        view.Tapped += (_, _) => tapped = 22;
        list.Touch(new(1, SkUiTouchAction.Pressed, new Point(100, 125), TimeSpan.Zero));
        list.Touch(new(1, SkUiTouchAction.Released, new Point(100, 125), TimeSpan.FromMilliseconds(20)));
        Assert.Equal(22, tapped);
    }

    [Fact]
    public void VisibleRangeChangedAndTemplateChangeRecreatesViews()
    {
        var counter = new Counter();
        var list = List(Rows(100, _ => 50), counter);
        var ranges = new List<(int, int)>();
        list.VisibleRangeChanged += (_, args) => ranges.Add((args.FirstVisibleIndex, args.LastVisibleIndex));
        SkUiTestHelpers.Arrange(list, 300, 500);
        list.ScrollTo(0, 75);
        Assert.Equal([(0, 9), (1, 11)], ranges);

        var other = new Counter();
        list.ItemTemplate = RowTemplate(other);
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.True(other.Created >= 11);
        Assert.Equal(0, list.Items.PooledViews);
    }

    [Fact]
    public void ScrollingDoesNotReRecordItemsThatStay()
    {
        var list = List(Rows(1000, _ => 50));
        list.ItemExtent = 50;
        using var surface = new SkUiTestSurface(list, 300, 500);
        surface.Frame(0);
        list.ScrollTo(0, 10);
        surface.Frame(16);
        var recorded = surface.RecordedPictures;
        list.ScrollTo(0, 20);
        surface.Frame(32);
        Assert.Equal(recorded, surface.RecordedPictures);
    }

    [Fact]
    public void DefaultTemplateShowsTheItemText()
    {
        var list = new SkUiVirtualScrollView { ItemsSource = new[] { "a", "b", "c" } };
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.Equal("b", ((SkUiLabel)list.Items.GetRealizedView(1)!).Text);
    }

    /// <summary>A value of <paramref name="type"/> other than the property defaults.</summary>
    private static object SampleValue(Type type) => type switch
    {
        _ when type == typeof(IEnumerable<int>) || type == typeof(System.Collections.IEnumerable) => new List<int> { 1, 2 },
        _ when type == typeof(DataTemplate) => new DataTemplate(() => new SkUiBox()),
        _ when type == typeof(double) => 3d,
        _ when type == typeof(TimeSpan) || type == typeof(TimeSpan?) => TimeSpan.FromMilliseconds(7),
        _ when type == typeof(int) || type == typeof(int?) => 5,
        _ when type == typeof(System.Windows.Input.ICommand) => new Command(() => { }),
        _ when type == typeof(Func<int, ISkUiView?>) => (Func<int, ISkUiView?>)(_ => null),
        _ => "parameter"
    };

    /// <summary>
    /// Every settable member of <typeparamref name="TInterface"/> on <typeparamref name="TList"/> reaches its layout: the list's
    /// bindable property is declared from the layout's (same name, type and default), and a value set on the list is the
    /// layout's. Members a list keeps itself (a collection view's items and template, which feed its rows) only read back.
    /// </summary>
    private static void AssertForwardsEveryMember<TInterface, TList>(Func<TList> create, Func<TList, SkUiVirtualVerticalStackLayoutBase> layoutOf)
        where TList : TInterface
    {
        var settable = typeof(TInterface).GetInterfaces().Append(typeof(TInterface))
            .SelectMany(type => type.GetProperties()).Where(property => property.CanWrite).ToList();
        Assert.NotEmpty(settable);
        const System.Reflection.BindingFlags statics = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy;
        foreach (var property in settable)
        {
            if (typeof(SkUiVirtualVerticalStackLayout).GetField(property.Name + "Property", statics)?.GetValue(null) is BindableProperty layoutProperty)
            {
                var forwarded = (BindableProperty)typeof(TList).GetField(property.Name + "Property", statics)!.GetValue(null)!;
                Assert.Equal(layoutProperty.PropertyName, forwarded.PropertyName);
                Assert.Equal(layoutProperty.ReturnType, forwarded.ReturnType);
                Assert.Equal(layoutProperty.DefaultValue, forwarded.DefaultValue);
            }
            var list = create();
            var value = SampleValue(property.PropertyType);
            property.SetValue(list, value);
            var layout = layoutOf(list);
            if (layout is TInterface)
                Assert.Equal(value, property.GetValue(layout));
            else if (layout.GetType().GetProperty(property.Name) is { } layoutMember)
                Assert.Equal(value, layoutMember.GetValue(layout));
            Assert.Equal(value, property.GetValue(list));
        }
    }

    [Fact]
    public void ScrollViewForwardsEveryListMemberToItsLayout()
    {
        AssertForwardsEveryMember<ISkUiVirtualList, SkUiVirtualScrollView>(() => new SkUiVirtualScrollView(), list => list.Items);

        // A value the layout refuses is refused by the scroll view too, so they never disagree.
        var refusing = new SkUiVirtualScrollView { ItemSpacing = 4, ReleaseFactor = 1 };
        refusing.ItemSpacing = -1;
        refusing.ReleaseFactor = double.NaN;
        Assert.Equal(4, refusing.ItemSpacing);
        Assert.Equal(4, refusing.Items.ItemSpacing);
        Assert.Equal(1, refusing.ReleaseFactor);
        Assert.Equal(1, refusing.Items.ReleaseFactor);
    }

    [Fact]
    public void CollectionViewForwardsEveryCommonListMemberToItsLayout()
    {
        AssertForwardsEveryMember<ISkUiItemsView, SkUiCollectionView>(() => new SkUiCollectionView(), list => list.ItemsLayout);

        var refusing = new SkUiCollectionView { ItemSpacing = 4, PrefetchBudget = TimeSpan.FromMilliseconds(2) };
        refusing.ItemSpacing = -1;
        refusing.PrefetchBudget = TimeSpan.FromMilliseconds(-1);
        Assert.Equal(4, refusing.ItemsLayout.ItemSpacing);
        Assert.Equal(TimeSpan.FromMilliseconds(2), refusing.ItemsLayout.PrefetchBudget);
    }

    [Fact]
    public void EveryListHasTheCommonMembersUnderTheSameNames()
    {
        // The interfaces are implemented (compile-time); each list also declares a bindable property per settable member.
        foreach (var type in new[] { typeof(SkUiVirtualVerticalStackLayout), typeof(SkUiVirtualScrollView), typeof(SkUiCollectionView) })
        {
            Assert.True(typeof(ISkUiItemsView).IsAssignableFrom(type), type.Name);
            foreach (var property in typeof(ISkUiItemsView).GetProperties().Where(property => property.CanWrite))
                Assert.True(type.GetField(property.Name + "Property", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy) is not null,
                    $"{type.Name}.{property.Name}Property");
        }
    }

    [Fact]
    public void ItemSizesOffsetsAndIndices()
    {
        var sizes = new SkUiVirtualItemSizes { Spacing = 10 };
        sizes.SetCount(5);
        Assert.Equal(5 * 44 + 4 * 10, sizes.TotalLength);
        sizes.SetSize(0, 100);
        sizes.SetSize(2, 20);
        Assert.Equal(60, sizes.Estimate); // average of the measured
        Assert.Equal(0, sizes.OffsetOf(0));
        Assert.Equal(110, sizes.OffsetOf(1));
        Assert.Equal(180, sizes.OffsetOf(2));
        Assert.Equal(210, sizes.OffsetOf(3));
        Assert.Equal(2, sizes.IndexAt(200));
        Assert.Equal(1, sizes.IndexAt(179));
        Assert.Equal(4, sizes.IndexAt(10_000));

        sizes.Insert(1, 2);
        Assert.Equal(7, sizes.Count);
        Assert.True(sizes.IsMeasured(0));
        Assert.False(sizes.IsMeasured(1));
        Assert.True(sizes.IsMeasured(4));
        sizes.Move(4, 0, 1);
        Assert.Equal(20, sizes.SizeOf(0));
        Assert.Equal(100, sizes.SizeOf(1));
        sizes.Remove(0, 2);
        Assert.Equal(5, sizes.Count);
        Assert.Equal(0, sizes.MeasuredCount);
        Assert.Equal(5 * 44 + 4 * 10, sizes.TotalLength);

        sizes.FixedExtent = 30;
        Assert.Equal(3 * 40, sizes.OffsetOf(3));
        Assert.Equal(3, sizes.IndexAt(125));
    }
}
