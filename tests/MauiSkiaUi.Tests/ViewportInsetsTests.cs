using System.Collections.ObjectModel;
using System.Windows.Input;
using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Phase V of docs/design/ImplementationPlan-1.6.md: <c>ViewportInsets</c> on the scrollers and lists (the content scrolls under
/// views drawn over the viewport, and every behavior uses the band they leave uncovered) and <see cref="SkUiView.SafeAreaInsets"/>
/// on the surface root.
/// </summary>
[Collection(RuntimeXamlCollection.Name)]
public class ViewportInsetsTests
{
    private const double RowHeight = 50;

    private static SkUiScrollController Controller(ISkUiScrollHost host) => host.Scroller;

    private static SkUiVerticalStackLayout Stack(int count, double size = 100, bool horizontal = false)
    {
        var stack = new SkUiVerticalStackLayout();
        for (var index = 0; index < count; index++)
            stack.Children.Add(horizontal ? new SkUiBox { WidthRequest = size } : new SkUiBox { HeightRequest = size });
        return stack;
    }

    private static SkUiScrollView Scroller(View content, Thickness insets, ScrollOrientation orientation = ScrollOrientation.Vertical) => new()
    {
        Content = (ISkUiView)content,
        ViewportInsets = insets,
        Orientation = orientation,
        Overscroll = SkUiOverscrollMode.None
    };

    #region V1: scroll views

    [Fact]
    public void ContentIsArrangedInsideTheInsetsAndTheExtentGrowsByThem()
    {
        var content = new SkUiBox { HeightRequest = 1000 };
        var scroll = Scroller(content, new Thickness(0, 50, 0, 30));
        SkUiTestHelpers.Arrange(scroll, 300, 400);

        Assert.Equal(new Rect(0, 50, 300, 1000), content.Frame);
        Assert.Equal(1080, scroll.ContentSize.Height);
        // At the end of the range the content's end is above the bottom inset.
        scroll.ScrollTo(0, 10_000);
        Assert.Equal(680, scroll.ScrollY);
        Assert.Equal(370, content.Frame.Bottom - scroll.ScrollY);
    }

    [Fact]
    public void PaddingKeepsItsMeaningInsideTheInsets()
    {
        var content = new SkUiBox { HeightRequest = 1000 };
        var scroll = Scroller(content, new Thickness(0, 50, 0, 30));
        scroll.Padding = new Thickness(0, 7, 0, 3);
        SkUiTestHelpers.Arrange(scroll, 300, 400);

        Assert.Equal(57, content.Frame.Y);
        Assert.Equal(1090, scroll.ContentSize.Height);
    }

    [Fact]
    public void InsetsAcrossTheScrollAxisPadTheContentWithoutScrollingIt()
    {
        var content = new SkUiBox { HeightRequest = 1000 };
        var scroll = Scroller(content, new Thickness(10, 0, 40, 0));
        SkUiTestHelpers.Arrange(scroll, 300, 400);

        Assert.Equal(new Rect(10, 0, 250, 1000), content.Frame);
        Assert.Equal(0, Controller(scroll).MaxX);
    }

    [Fact]
    public void InsetsArePhysicalEdgesRightToLeft()
    {
        var content = new SkUiBox { HeightRequest = 1000 };
        var scroll = Scroller(content, new Thickness(10, 0, 40, 0));
        scroll.FlowDirection = FlowDirection.RightToLeft;
        SkUiTestHelpers.Arrange(scroll, 300, 400);

        Assert.Equal(new Rect(10, 0, 250, 1000), content.Frame);
    }

    [Fact]
    public void AHorizontalScrollerStartsAfterItsStartInsetInBothDirections()
    {
        var content = new SkUiBox { WidthRequest = 1000 };
        var scroll = Scroller(content, new Thickness(40, 0, 10, 0), ScrollOrientation.Horizontal);
        SkUiTestHelpers.Arrange(scroll, 300, 200);
        Assert.Equal(40, content.Frame.X);
        Assert.Equal(1050, scroll.ContentSize.Width);

        var rtlContent = new SkUiBox { WidthRequest = 1000 };
        var rtl = Scroller(rtlContent, new Thickness(40, 0, 10, 0), ScrollOrientation.Horizontal);
        rtl.FlowDirection = FlowDirection.RightToLeft;
        SkUiTestHelpers.Arrange(rtl, 300, 200);
        // At its start, the right end: the content's right edge shows before the right inset.
        Assert.Equal(750, rtl.ScrollX);
        Assert.Equal(290, rtlContent.Frame.Right - rtl.ScrollX);
    }

    [Fact]
    public void TheVerticalScrollBarRunsAlongTheBandAndStaysOutOfASideInset()
    {
        var look = SkUiLook.Current;
        var scroll = Scroller(new SkUiBox { HeightRequest = 1000 }, new Thickness(0, 50, 25, 30));
        SkUiTestHelpers.Arrange(scroll, 300, 400);

        var bar = Controller(scroll).ScrollBars.Vertical!;
        var margin = Math.Max(0, look.ScrollBarMargin);
        Assert.Equal(50 + margin, bar.Frame.Y);
        Assert.Equal(400 - 50 - 30 - 2 * margin, bar.Frame.Height);
        Assert.Equal(300 - 25, bar.Frame.Right);
    }

    [Fact]
    public void TheCoreScrollBarRunsAlongTheBandToo()
    {
        var scroll = new SkUiCoreScrollView().SetViewportInsets(new Thickness(0, 50, 25, 30));
        scroll.SetContent(new SkUiCoreBox().SetHeight(1000));
        SkUiTestHelpers.Arrange(new SkUiCoreHost().SetContent(scroll), 300, 400);

        var bar = Controller(scroll).ScrollBars.Vertical!;
        var margin = Math.Max(0, SkUiLook.Current.ScrollBarMargin);
        Assert.Equal(50 + margin, bar.Frame.Y);
        Assert.Equal(400 - 50 - 30 - 2 * margin, bar.Frame.Height);
        Assert.Equal(300 - 25, bar.Frame.Right);
    }

    [Fact]
    public void AScrollBarThatAlwaysShowsKeepsItsGutterBesideTheContentAndOutOfTheInset()
    {
        var content = new SkUiBox { HeightRequest = 1000 };
        var scroll = Scroller(content, new Thickness(0, 0, 25, 0));
        scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Always;
        SkUiTestHelpers.Arrange(scroll, 300, 400);

        var bar = Controller(scroll).ScrollBars.Vertical!;
        Assert.Equal(content.Frame.Right, bar.Frame.X);
        Assert.Equal(300 - 25, bar.Frame.Right);
    }

    [Fact]
    public void TheThumbIsAsLongAsTheVisiblePartOfTheContent()
    {
        var scroll = Scroller(new SkUiBox { HeightRequest = 1000 }, new Thickness(0, 100, 0, 100));
        SkUiTestHelpers.Arrange(scroll, 300, 500);

        var bar = Controller(scroll).ScrollBars.Vertical!;
        // 300 of the band show 1000 of content: three tenths of the track.
        Assert.Equal(bar.Frame.Height * 0.3, bar.ThumbNode.Frame.Height, 3);
    }

    [Theory]
    [InlineData(SnapPointsAlignment.Start, 200)]
    [InlineData(SnapPointsAlignment.Center, 150)]
    [InlineData(SnapPointsAlignment.End, 100)]
    public void SnapPointsLineUpWithTheBand(SnapPointsAlignment alignment, double thirdChild)
    {
        var scroll = Scroller(Stack(10), new Thickness(0, 50, 0, 50));
        scroll.SnapPointsType = SnapPointsType.Mandatory;
        scroll.SnapPointsAlignment = alignment;
        SkUiTestHelpers.Arrange(scroll, 300, 300);

        Assert.Contains(thirdChild, Controller(scroll).SnapOffsets(horizontal: false));
    }

    [Theory]
    [InlineData(ScrollToPosition.Start, 400)]
    [InlineData(ScrollToPosition.Center, 350)]
    [InlineData(ScrollToPosition.End, 300)]
    public async Task ScrollingToAnElementPlacesItInTheBand(ScrollToPosition position, double offset)
    {
        var stack = Stack(10);
        var scroll = Scroller(stack, new Thickness(0, 50, 0, 50));
        SkUiTestHelpers.Arrange(scroll, 300, 300);

        await scroll.ScrollToAsync((Element)stack.Children[4], position, animated: false);
        Assert.Equal(offset, scroll.ScrollY);
    }

    [Fact]
    public async Task MakeVisibleCountsARowUnderABarAsNotVisible()
    {
        var stack = Stack(10);
        var scroll = Scroller(stack, new Thickness(0, 50, 0, 50));
        SkUiTestHelpers.Arrange(scroll, 300, 300);

        // The second row (150..250) shows above the bottom bar at 250; the third (250..350) is under it.
        await scroll.ScrollToAsync((Element)stack.Children[1], ScrollToPosition.MakeVisible, animated: false);
        Assert.Equal(0, scroll.ScrollY);
        await scroll.ScrollToAsync((Element)stack.Children[2], ScrollToPosition.MakeVisible, animated: false);
        Assert.Equal(100, scroll.ScrollY);
    }

    [Fact]
    public async Task RawOffsetsStayRaw()
    {
        var scroll = Scroller(Stack(10), new Thickness(0, 50, 0, 50));
        SkUiTestHelpers.Arrange(scroll, 300, 300);

        await scroll.ScrollToAsync(0, 123, animated: false);
        Assert.Equal(123, scroll.ScrollY);
    }

    [Fact]
    public async Task ACoreNodeIsPlacedInTheBand()
    {
        var target = new SkUiCoreBox().SetHeight(100);
        var stack = new SkUiCoreVerticalStackLayout();
        for (var index = 0; index < 4; index++)
            stack.Add(new SkUiCoreBox().SetHeight(100));
        stack.Add(target);
        for (var index = 0; index < 5; index++)
            stack.Add(new SkUiCoreBox().SetHeight(100));
        var scroll = new SkUiCoreScrollView().SetViewportInsets(new Thickness(0, 50, 0, 50));
        scroll.SetContent(stack);
        SkUiTestHelpers.Arrange(new SkUiCoreHost().SetContent(scroll), 300, 300);

        Assert.Equal(50, stack.Frame.Y);
        Assert.Equal(1100, scroll.ContentSize.Height);
        await scroll.ScrollToAsync(target, ScrollToPosition.Start, animated: false);
        Assert.Equal(400, scroll.ScrollY);
    }

    [Fact]
    public void CoreContentIsArrangedInsideTheInsetsAndCoreAndSkUiAgree()
    {
        var coreContent = new SkUiCoreBox().SetHeight(1000);
        var core = new SkUiCoreScrollView().SetViewportInsets(new Thickness(10, 50, 40, 30));
        core.SetContent(coreContent);
        SkUiTestHelpers.Arrange(new SkUiCoreHost().SetContent(core), 300, 400);

        var content = new SkUiBox { HeightRequest = 1000 };
        var scroll = Scroller(content, new Thickness(10, 50, 40, 30));
        SkUiTestHelpers.Arrange(scroll, 300, 400);

        Assert.Equal(content.Frame, coreContent.Frame);
        Assert.Equal(scroll.ContentSize, core.ContentSize);
    }

    [Fact]
    public void InsetsChangingWhileScrolledKeepTheVisibleContentInPlace()
    {
        var content = new SkUiBox { HeightRequest = 1000 };
        var scroll = Scroller(content, new Thickness(0, 50, 0, 0));
        SkUiTestHelpers.Arrange(scroll, 300, 400);
        scroll.ScrollTo(0, 200);
        var shown = content.Frame.Y - scroll.ScrollY;

        scroll.ViewportInsets = new Thickness(0, 90, 0, 0);
        SkUiTestHelpers.Arrange(scroll, 300, 400);
        Assert.Equal(240, scroll.ScrollY);
        Assert.Equal(shown, content.Frame.Y - scroll.ScrollY);

        scroll.ViewportInsets = new Thickness(0, 20, 0, 0);
        SkUiTestHelpers.Arrange(scroll, 300, 400);
        Assert.Equal(170, scroll.ScrollY);
        Assert.Equal(shown, content.Frame.Y - scroll.ScrollY);
    }

    [Fact]
    public void AScrollerAtItsStartStaysThereWhenTheInsetsChange()
    {
        var content = new SkUiBox { HeightRequest = 1000 };
        var scroll = Scroller(content, default);
        SkUiTestHelpers.Arrange(scroll, 300, 400);

        // A bar measured after the first layout: the first row stays clear of it.
        scroll.ViewportInsets = new Thickness(0, 60, 0, 0);
        SkUiTestHelpers.Arrange(scroll, 300, 400);
        Assert.Equal(0, scroll.ScrollY);
        Assert.Equal(60, content.Frame.Y);
    }

    [Fact]
    public void AShrinkingInsetStopsAtTheStartOfTheRange()
    {
        var scroll = Scroller(new SkUiBox { HeightRequest = 1000 }, new Thickness(0, 90, 0, 0));
        SkUiTestHelpers.Arrange(scroll, 300, 400);
        scroll.ScrollTo(0, 30);

        scroll.ViewportInsets = default;
        Assert.Equal(0, scroll.ScrollY);
        SkUiTestHelpers.Arrange(scroll, 300, 400);
        Assert.Equal(0, scroll.ScrollY);
    }

    [Fact]
    public void ACoreRightToLeftHorizontalScrollerKeepsItsContentWhenAnInsetChanges()
    {
        var content = new SkUiCoreBox().SetWidth(1000);
        var scroll = new SkUiCoreScrollView().SetOrientation(ScrollOrientation.Horizontal);
        scroll.SetContent(content);
        var host = new SkUiCoreHost().SetContent(scroll);
        host.FlowDirection = FlowDirection.RightToLeft;
        SkUiTestHelpers.Arrange(host, 300, 200);
        Assert.Equal(700, scroll.ScrollX);

        scroll.SetViewportInsets(new Thickness(0, 0, 30, 0));
        SkUiTestHelpers.Arrange(host, 300, 200);
        Assert.Equal(730, scroll.ScrollX);
        Assert.Equal(270, content.Frame.Right - scroll.ScrollX);

        scroll.ScrollTo(300, 0);
        var shown = content.Frame.X - scroll.ScrollX;
        scroll.SetViewportInsets(new Thickness(25, 0, 30, 0));
        SkUiTestHelpers.Arrange(host, 300, 200);
        Assert.Equal(shown, content.Frame.X - scroll.ScrollX);
    }

    [Fact]
    public void ARightToLeftHorizontalScrollerKeepsItsContentWhenAnInsetChanges()
    {
        var content = new SkUiBox { WidthRequest = 1000 };
        var scroll = Scroller(content, default, ScrollOrientation.Horizontal);
        scroll.FlowDirection = FlowDirection.RightToLeft;
        SkUiTestHelpers.Arrange(scroll, 300, 200);
        Assert.Equal(700, scroll.ScrollX);

        // At its start (the right end): it stays there, inside the new inset.
        scroll.ViewportInsets = new Thickness(0, 0, 30, 0);
        SkUiTestHelpers.Arrange(scroll, 300, 200);
        Assert.Equal(730, scroll.ScrollX);
        Assert.Equal(270, content.Frame.Right - scroll.ScrollX);

        // Scrolled: what shows stays where it shows, with either side changing.
        scroll.ScrollTo(300, 0);
        SkUiTestHelpers.Arrange(scroll, 300, 200);
        var shown = content.Frame.X - scroll.ScrollX;
        scroll.ViewportInsets = new Thickness(0, 0, 60, 0);
        SkUiTestHelpers.Arrange(scroll, 300, 200);
        Assert.Equal(shown, content.Frame.X - scroll.ScrollX);
        scroll.ViewportInsets = new Thickness(25, 0, 60, 0);
        SkUiTestHelpers.Arrange(scroll, 300, 200);
        Assert.Equal(shown, content.Frame.X - scroll.ScrollX);
    }

    [Theory]
    [InlineData(100, 1100)]
    [InlineData(10, 1030)]
    public void TheKeyboardAndTheEndInsetCombineAsTheLargerOfTheTwo(double keyboard, double extent)
    {
        var scroll = Scroller(new SkUiBox { HeightRequest = 1000 }, new Thickness(0, 0, 0, 30));
        scroll.KeyboardInset = keyboard;
        SkUiTestHelpers.Arrange(scroll, 300, 400);
        Assert.Equal(extent, scroll.ContentSize.Height);
    }

    [Theory]
    [InlineData(100, 1100)]
    [InlineData(10, 1030)]
    public void TheCoreKeyboardAndEndInsetCombineTheSameWay(double keyboard, double extent)
    {
        var scroll = new SkUiCoreScrollView().SetViewportInsets(new Thickness(0, 0, 0, 30));
        scroll.SetContent(new SkUiCoreBox().SetHeight(1000));
        scroll.KeyboardInset = keyboard;
        SkUiTestHelpers.Arrange(new SkUiCoreHost().SetContent(scroll), 300, 400);
        Assert.Equal(extent, scroll.ContentSize.Height);
    }

    [Fact]
    public void AFocusedFieldIsRevealedAboveABottomBarThatIsTallerThanTheKeyboard()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var column = new SkUiVerticalStackLayout();
        for (var index = 0; index < 8; index++)
            column.Children.Add(new SkUiBox { HeightRequest = 60 });
        var field = new SkUiEntry { FontFamily = SkUiTestHelpers.BundledFontFamily };
        column.Children.Add(field);
        var scroller = new SkUiScrollView { Content = column, ViewportInsets = new Thickness(0, 0, 0, 150) };
        using var surface = new SkUiTestSurface(new SkUiContentView { Content = scroller }, 300, 600);
        surface.Frame();
        Assert.True(field.Focus());

        surface.Root.FocusManager.SetKeyboardTop(500);
        for (var time = 0; time <= 1000; time += 50)
            surface.Frame(time);

        var bounds = SkUiDiagnostics.GetRootBounds(field)!.Value;
        Assert.True(bounds.Bottom <= 450.5, $"field bottom {bounds.Bottom}");
    }

    [Fact]
    public void APageIsTheBand()
    {
        var scroll = Scroller(Stack(10), new Thickness(0, 50, 0, 50));
        SkUiTestHelpers.Arrange(scroll, 300, 300);
        Assert.Equal(200, Controller(scroll).PageLength(vertical: true));

        scroll.ViewportInsets = new Thickness(0, 150, 0, 150);
        SkUiTestHelpers.Arrange(scroll, 300, 300);
        Assert.Equal(300, Controller(scroll).PageLength(vertical: true)); // all covered: the viewport still pages
    }

    [Fact]
    public void InvalidInsetsAreIgnoredByThePropertyAndRefusedByTheSetters()
    {
        var insets = new Thickness(1, 2, 3, 4);
        var scroll = new SkUiScrollView { ViewportInsets = insets };
        scroll.ViewportInsets = new Thickness(-1);
        Assert.Equal(insets, scroll.ViewportInsets);
        Assert.Throws<ArgumentOutOfRangeException>(() => scroll.SetViewportInsets(new Thickness(double.NaN)));

        var list = new SkUiCollectionView { ViewportInsets = insets };
        list.ViewportInsets = new Thickness(-1);
        Assert.Equal(insets, list.ViewportInsets);
        Assert.Throws<ArgumentOutOfRangeException>(() => list.SetViewportInsets(new Thickness(-1)));

        Assert.Throws<ArgumentOutOfRangeException>(() => new SkUiCoreScrollView().SetViewportInsets(new Thickness(-1)));
    }

    [Fact]
    public void TheCoreSetterReportsTheChangeOnce()
    {
        var scroll = new SkUiCoreScrollView();
        var changes = 0;
        scroll.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(SkUiCoreScrollView.ViewportInsets)) changes++; };
        scroll.SetViewportInsets(new Thickness(5));
        scroll.SetViewportInsets(new Thickness(5));
        Assert.Equal(new Thickness(5), scroll.ViewportInsets);
        Assert.Equal(1, changes);
    }

    #endregion

    [Fact]
    public void EveryScrollerAndListTakesInsetsThroughTheInterface()
    {
        ISkUiViewportInsets[] targets = [new SkUiScrollView(), new SkUiVirtualScrollView(), new SkUiCoreScrollView(), new SkUiCollectionView()];
        var insets = new Thickness(1, 2, 3, 4);
        foreach (var target in targets)
        {
            target.ViewportInsets = insets;
            Assert.Equal(insets, target.ViewportInsets);
        }
    }

    #region Insets set during layout

    /// <summary>A page like an edge-to-edge one: a scroller, and a bar over it whose size becomes the scroller's inset as it is arranged.</summary>
    private static (SkUiGrid Root, SkUiBox Content, SkUiBox Bar) BarOverScroller()
    {
        var content = new SkUiBox { HeightRequest = 2000 };
        var scroll = new SkUiScrollView { Content = content };
        var bar = new SkUiBox { HeightRequest = 80, VerticalOptions = LayoutOptions.Start };
        var root = new SkUiGrid { Children = { scroll, bar } };
        bar.SizeChanged += (_, _) => scroll.ViewportInsets = new Thickness(0, Math.Max(0, bar.Height), 0, 0);
        return (root, content, bar);
    }

    [Fact]
    public void InsetsSetWhileTheFirstLayoutArrangesTheBarsApplyByTheFirstFrame()
    {
        var (root, content, _) = BarOverScroller();
        using var surface = new SkUiTestSurface(root, 300, 500);
        surface.Frame();
        Assert.Equal(80, content.Frame.Y);
    }

    [Fact]
    public void InsetsSetWhileALaterLayoutArrangesTheBarsApplyByTheNextFrame()
    {
        var (root, content, bar) = BarOverScroller();
        using var surface = new SkUiTestSurface(root, 300, 500);
        surface.Frame();

        bar.HeightRequest = 120;
        surface.Frame(16);
        Assert.Equal(120, content.Frame.Y);
    }

    #endregion

    #region V2: lists

    private sealed record Row(int Index);

    private sealed class Section(string name, IEnumerable<Row> rows) : List<Row>(rows)
    {
        public string Name { get; } = name;
    }

    private sealed class Command(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute();
    }

    private static ObservableCollection<Row> Rows(int count, int from = 0) =>
        new(Enumerable.Range(from, count).Select(index => new Row(index)));

    private static SkUiCollectionView List(int rows, Thickness insets) => new()
    {
        ItemsSource = Rows(rows),
        ItemTemplate = new DataTemplate(() => new SkUiBox { HeightRequest = RowHeight }),
        ViewportInsets = insets,
        VerticalScrollBarVisibility = ScrollBarVisibility.Never,
        Overscroll = SkUiOverscrollMode.None,
        PrefetchBudget = TimeSpan.FromSeconds(10)
    };

    /// <summary>Where a row shows in the list.</summary>
    private static Rect RowOnScreen(SkUiCollectionView list, int index)
    {
        var view = (View)list.GetRealizedView(index)!;
        var (x, y) = (view.Frame.X, view.Frame.Y);
        for (var parent = view.Parent as View; parent is not null && !ReferenceEquals(parent, list); parent = parent.Parent as View)
        {
            x += parent.Frame.X;
            y += parent.Frame.Y;
            if (parent is SkUiScrollView scroller)
                y -= scroller.ScrollY;
        }
        return new Rect(x, y, view.Frame.Width, view.Frame.Height);
    }

    private static SkUiScrollController Controller(SkUiCollectionView list) => ((ISkUiScrollHost)list.ScrollView).Scroller;

    private static Rect LayerFrame(SkUiView owner) => ((View)owner.SkiaChildren.OfType<SkUiRefreshLayer>().Single()).Frame;

    [Fact]
    public void ListRowsStartAfterTheTopInsetAndEndAboveTheBottomOne()
    {
        var list = List(20, new Thickness(0, 60, 0, 40));
        SkUiTestHelpers.Arrange(list, 300, 500);

        Assert.Equal(60, RowOnScreen(list, 0).Y);
        Assert.Equal(20 * RowHeight + 100, list.ScrollView.ContentSize.Height);

        _ = list.ScrollToAsync(10_000, animated: false);
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.Equal(460, RowOnScreen(list, 19).Bottom);
    }

    [Fact]
    public void TheListsInsetsAreReadByItsScroller()
    {
        var list = List(20, new Thickness(1, 2, 3, 4));
        Assert.Equal(new Thickness(1, 2, 3, 4), list.ScrollView.ViewportInsets);
    }

    [Theory]
    [InlineData(ScrollToPosition.Start, 60)]
    [InlineData(ScrollToPosition.Center, 235)]
    [InlineData(ScrollToPosition.End, 410)]
    public async Task ScrollingToARowPlacesItInTheBand(ScrollToPosition position, double y)
    {
        var list = List(40, new Thickness(0, 60, 0, 40));
        SkUiTestHelpers.Arrange(list, 300, 500);

        await list.ScrollToIndex(20, position, animated: false);
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.Equal(y, RowOnScreen(list, 20).Y, 1);
    }

    [Fact]
    public async Task AVirtualLayoutInAScrollViewFollowsTheScrollersInsets()
    {
        var layout = new SkUiVirtualVerticalStackLayout
        {
            ItemsSource = Rows(40),
            ItemTemplate = new DataTemplate(() => new SkUiBox { HeightRequest = RowHeight }),
            PrefetchBudget = TimeSpan.FromSeconds(10)
        };
        var scroll = Scroller(layout, new Thickness(0, 60, 0, 40));
        SkUiTestHelpers.Arrange(scroll, 300, 500);

        await layout.ScrollToIndex(20, ScrollToPosition.Start, animated: false);
        Assert.Equal(20 * RowHeight, scroll.ScrollY);
    }

    [Fact]
    public async Task AVirtualScrollViewHasTheInsetsOfAScrollView()
    {
        var list = new SkUiVirtualScrollView
        {
            ItemsSource = Rows(40),
            ItemTemplate = new DataTemplate(() => new SkUiBox { HeightRequest = RowHeight }),
            PrefetchBudget = TimeSpan.FromSeconds(10),
            ViewportInsets = new Thickness(0, 60, 0, 40)
        };
        SkUiTestHelpers.Arrange(list, 300, 500);

        await list.ScrollToIndex(20, ScrollToPosition.End, animated: false);
        Assert.Equal(20 * RowHeight + 60 + RowHeight - 460, list.ScrollY);
    }

    [Fact]
    public void TheStickyHeaderAndFooterArePinnedInsideTheInsets()
    {
        var header = new SkUiBox { HeightRequest = 40 };
        var footer = new SkUiBox { HeightRequest = 30 };
        var list = List(20, new Thickness(10, 60, 20, 40));
        list.SetHeader(header, sticky: true);
        list.Footer = footer;
        list.IsStickyFooter = true;
        SkUiTestHelpers.Arrange(list, 300, 500);

        Assert.Equal(new Rect(10, 60, 270, 40), ((View)((Element)header).Parent).Frame);
        Assert.Equal(new Rect(10, 430, 270, 30), ((View)((Element)footer).Parent).Frame);
        // The scroller's insets add up: the rows start below the header, across inside the side insets.
        Assert.Equal((40d, 30d), list.ScrollView.Insets);
        Assert.Equal(new Rect(10, 100, 270, RowHeight), RowOnScreen(list, 0));
        Assert.Equal(20 * RowHeight + 60 + 40 + 40 + 30, list.ScrollView.ContentSize.Height);
    }

    [Fact]
    public void StickyPartsMirrorRightToLeft()
    {
        var header = new SkUiBox { HeightRequest = 40 };
        var list = List(20, new Thickness(10, 0, 20, 0));
        list.SetHeader(header, sticky: true);
        list.FlowDirection = FlowDirection.RightToLeft;
        SkUiTestHelpers.Arrange(list, 300, 500);

        Assert.Equal(new Rect(10, 0, 270, 40), ((View)((Element)header).Parent).Frame);
    }

    [Fact]
    public void AHorizontalListPinsItsStickyHeaderAfterTheStartInset()
    {
        var header = new SkUiBox { WidthRequest = 40 };
        var list = List(20, new Thickness(60, 5, 30, 15));
        list.Orientation = ItemsLayoutOrientation.Horizontal;
        list.ItemExtent = 100;
        list.SetHeader(header, sticky: true);
        SkUiTestHelpers.Arrange(list, 500, 200);

        Assert.Equal(new Rect(60, 5, 40, 180), ((View)((Element)header).Parent).Frame);
    }

    [Fact]
    public void StickyGroupHeadersPinUnderTheTopInset()
    {
        var sections = new ObservableCollection<Section>
        {
            new("A", Rows(10)),
            new("B", Rows(10, 10))
        };
        var list = new SkUiCollectionView
        {
            IsGrouped = true,
            ItemsSource = sections,
            ItemTemplate = new DataTemplate(() => new SkUiBox { HeightRequest = RowHeight }),
            GroupHeaderTemplate = new DataTemplate(() => new SkUiBox { HeightRequest = 30 }),
            IsStickyGroupHeader = true,
            ViewportInsets = new Thickness(0, 60, 0, 0),
            VerticalScrollBarVisibility = ScrollBarVisibility.Never,
            Overscroll = SkUiOverscrollMode.None,
            PrefetchBudget = TimeSpan.FromSeconds(10)
        };
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.Equal(-1, list.StickyGroupShown); // the first group's own header shows below the inset

        _ = list.ScrollToAsync(100, animated: false);
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.Equal(0, list.StickyGroupShown);
        Assert.Equal(new Rect(0, 60, 300, 30), list.StickyGroupHost!.Frame);
        Assert.Equal(0, list.StickyGroupHost.TranslationY);

        // The next group's header (530 in the items) pushes it back when it reaches the sticky header's end, below the inset.
        _ = list.ScrollToAsync(510, animated: false);
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.Equal(0, list.StickyGroupShown);
        Assert.Equal(-10, list.StickyGroupHost.TranslationY, 1);
    }

    [Theory]
    [InlineData(SnapPointsAlignment.Start, 150)] // row 3
    [InlineData(SnapPointsAlignment.Center, 25)] // row 4
    [InlineData(SnapPointsAlignment.End, 50)] // row 8
    public void ListSnapPointsLineUpWithTheBand(SnapPointsAlignment alignment, double expected)
    {
        var list = List(20, new Thickness(0, 60, 0, 40));
        list.ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical)
        {
            SnapPointsType = SnapPointsType.Mandatory,
            SnapPointsAlignment = alignment
        };
        SkUiTestHelpers.Arrange(list, 300, 500);

        Assert.Contains(expected, Controller(list).SnapOffsets(horizontal: false));
    }

    [Fact]
    public void AListWithoutInsetsSnapsAsBefore()
    {
        var list = List(20, default);
        list.ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { SnapPointsType = SnapPointsType.Mandatory };
        SkUiTestHelpers.Arrange(list, 300, 400);
        Assert.Equal(Enumerable.Range(0, 13).Select(row => row * RowHeight), Controller(list).SnapOffsets(horizontal: false));
    }

    [Fact]
    public void LoadMoreWaitsForTheBandsEnd()
    {
        var loads = 0;
        var list = List(20, new Thickness(0, 0, 0, 100));
        list.LoadMoreMode = SkUiLoadMoreMode.Auto;
        list.LoadMoreCommand = new Command(() => loads++);
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.Equal(0, loads);

        // The last row (950..1000) shows under the bar, in the viewport but not in the band (it ends at 920).
        _ = list.ScrollToAsync(520, animated: false);
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.Equal(19, list.LastVisibleIndex);
        Assert.Equal(0, loads);

        _ = list.ScrollToAsync(600, animated: false);
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.Equal(1, loads);
    }

    [Fact]
    public void AStartInsetChangingNearTheEndDoesNotReachTheLastRow()
    {
        var loads = 0;
        var list = List(20, default);
        list.LoadMoreMode = SkUiLoadMoreMode.Auto;
        list.LoadMoreCommand = new Command(() => loads++);
        SkUiTestHelpers.Arrange(list, 300, 500);
        _ = list.ScrollToAsync(430, animated: false);
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.Equal(18, list.LastVisibleIndex);
        var ranges = 0;
        list.VisibleRangeChanged += (_, _) => ranges++;

        // What shows stays in place: row 19 neither shows nor is reached, also not between the change and the layout.
        list.ViewportInsets = new Thickness(0, 100, 0, 0);
        Assert.Equal(0, loads);
        SkUiTestHelpers.Arrange(list, 300, 500);

        Assert.Equal(530, list.ScrollY);
        Assert.Equal(18, list.LastVisibleIndex);
        Assert.Equal(0, loads);
        Assert.Equal(0, ranges);
    }

    [Fact]
    public void AStartInsetChangingNearTheEndDoesNotReachTheThreshold()
    {
        var reached = 0;
        var list = new SkUiVirtualScrollView
        {
            ItemsSource = Rows(20),
            ItemTemplate = new DataTemplate(() => new SkUiBox { HeightRequest = RowHeight }),
            PrefetchBudget = TimeSpan.FromSeconds(10),
            RemainingItemsThreshold = 0
        };
        list.RemainingItemsThresholdReached += (_, _) => reached++;
        SkUiTestHelpers.Arrange(list, 300, 500);
        list.ScrollTo(0, 430);
        SkUiTestHelpers.Arrange(list, 300, 500);

        list.ViewportInsets = new Thickness(0, 100, 0, 0);
        SkUiTestHelpers.Arrange(list, 300, 500);

        Assert.Equal(18, list.LastVisibleIndex);
        Assert.Equal(0, reached);
    }

    [Fact]
    public void ALayoutPassBeforeTheArrangeSeesTheWindowTheArrangeWillProduce()
    {
        // Top and side insets at once (the rows measure again at the new width), as when the safe area binds to them.
        var reached = 0;
        var ranges = new List<(int First, int Last)>();
        var list = new SkUiVirtualScrollView
        {
            ItemsSource = Rows(20),
            ItemTemplate = new DataTemplate(() => new SkUiBox { HeightRequest = RowHeight }),
            PrefetchBudget = TimeSpan.FromSeconds(10),
            RemainingItemsThreshold = 0
        };
        list.RemainingItemsThresholdReached += (_, _) => reached++;
        SkUiTestHelpers.Arrange(list, 300, 500);
        list.ScrollTo(0, 430);
        SkUiTestHelpers.Arrange(list, 300, 500);
        list.VisibleRangeChanged += (_, args) => ranges.Add((args.FirstVisibleIndex, args.LastVisibleIndex));

        list.ViewportInsets = new Thickness(10, 100, 10, 0);
        SkUiTestHelpers.Arrange(list, 300, 500);

        Assert.Equal(530, list.ScrollY);
        Assert.Equal(18, list.LastVisibleIndex);
        Assert.Equal(0, reached);
        Assert.Empty(ranges);
    }

    [Fact]
    public void AListsLayoutPassBeforeTheArrangeSeesTheWindowTheArrangeWillProduce()
    {
        var reached = 0;
        var list = List(20, default);
        list.RemainingItemsThreshold = 0;
        list.RemainingItemsThresholdReached += (_, _) => reached++;
        SkUiTestHelpers.Arrange(list, 300, 500);
        _ = list.ScrollToAsync(430, animated: false);
        SkUiTestHelpers.Arrange(list, 300, 500);

        list.ViewportInsets = new Thickness(10, 100, 10, 0);
        SkUiTestHelpers.Arrange(list, 300, 500);

        Assert.Equal(18, list.LastVisibleIndex);
        Assert.Equal(0, reached);
    }

    [Fact]
    public void ItemsChangedBeforeTheArrangeSeeTheWindowTheArrangeWillProduce()
    {
        var reached = 0;
        var rows = Rows(20);
        var list = new SkUiVirtualScrollView
        {
            ItemsSource = rows,
            ItemTemplate = new DataTemplate(() => new SkUiBox { HeightRequest = RowHeight }),
            PrefetchBudget = TimeSpan.FromSeconds(10),
            RemainingItemsThreshold = 0
        };
        list.RemainingItemsThresholdReached += (_, _) => reached++;
        SkUiTestHelpers.Arrange(list, 300, 500);
        list.ScrollTo(0, 430);
        SkUiTestHelpers.Arrange(list, 300, 500);

        list.ViewportInsets = new Thickness(0, 100, 0, 0);
        rows.Insert(19, new Row(99));
        SkUiTestHelpers.Arrange(list, 300, 500);

        Assert.Equal(18, list.LastVisibleIndex);
        Assert.Equal(0, reached);
    }

    [Fact]
    public void ARightToLeftEndInsetChangingNearTheEndDoesNotReachTheLastRow()
    {
        var reached = 0;
        var list = List(20, default);
        list.Orientation = ItemsLayoutOrientation.Horizontal;
        list.ItemExtent = 100;
        list.FlowDirection = FlowDirection.RightToLeft;
        list.RemainingItemsThreshold = 0;
        list.RemainingItemsThresholdReached += (_, _) => reached++;
        SkUiTestHelpers.Arrange(list, 500, 200);
        // The end is on the left: 150 from it, the last row (at 0..100 of 2000) does not show.
        _ = list.ScrollToAsync(150, animated: false);
        SkUiTestHelpers.Arrange(list, 500, 200);
        Assert.Equal(18, list.LastVisibleIndex);
        var ranges = 0;
        list.VisibleRangeChanged += (_, _) => ranges++;
        // A list that fired on its way in: the threshold fires once per item count, so a new item opens it again.
        ((ObservableCollection<Row>)list.ItemsSource!).Insert(0, new Row(99));
        SkUiTestHelpers.Arrange(list, 500, 200);
        var last = list.LastVisibleIndex;
        var offset = list.ScrollX;
        reached = 0;
        ranges = 0;

        list.ViewportInsets = new Thickness(100, 0, 0, 0);
        SkUiTestHelpers.Arrange(list, 500, 200);

        Assert.Equal(offset + 100, list.ScrollX);
        Assert.Equal(last, list.LastVisibleIndex);
        Assert.Equal(0, reached);
        Assert.Equal(0, ranges);
    }

    [Fact]
    public void AScrollToATargetRightAfterAnInsetChangeUsesTheNewLayout()
    {
        var stack = Stack(10);
        var scroll = Scroller(stack, default);
        SkUiTestHelpers.Arrange(scroll, 300, 300);

        // The target's frame and the extent follow the new insets only with the arrange: the request waits for it.
        scroll.ViewportInsets = new Thickness(0, 50, 0, 50);
        var scrolled = scroll.ScrollToAsync((Element)stack.Children[9], ScrollToPosition.End, animated: false);
        SkUiTestHelpers.Arrange(scroll, 300, 300);

        Assert.True(scrolled.IsCompletedSuccessfully);
        Assert.Equal(800, scroll.ScrollY);
    }

    [Fact]
    public void AStartInsetShrinkingDoesNotReachTheFirstRow()
    {
        var loads = 0;
        var list = List(20, new Thickness(0, 100, 0, 0));
        list.LoadMoreMode = SkUiLoadMoreMode.Auto;
        list.LoadMorePosition = SkUiLoadMorePosition.Start;
        list.LoadMoreCommand = new Command(() => loads++);
        SkUiTestHelpers.Arrange(list, 300, 500);
        _ = list.ScrollToAsync(200, animated: false);
        SkUiTestHelpers.Arrange(list, 300, 500);
        // Loaded: the load-more row at the start goes.
        list.IsLoadMoreActive = false;
        SkUiTestHelpers.Arrange(list, 300, 500);
        var scrolled = list.ScrollY;
        loads = 0;

        list.ViewportInsets = default;
        Assert.Equal(0, loads);
        SkUiTestHelpers.Arrange(list, 300, 500);

        Assert.Equal(scrolled - 100, list.ScrollY);
        Assert.Equal(0, loads);
    }

    [Fact]
    public void TheFirstLayoutCountsTheStartInsetBeforeTheContentIsArranged()
    {
        var reached = 0;
        var list = new SkUiVirtualScrollView
        {
            ItemsSource = Rows(10),
            ItemTemplate = new DataTemplate(() => new SkUiBox { HeightRequest = RowHeight }),
            PrefetchBudget = TimeSpan.FromSeconds(10),
            RemainingItemsThreshold = 0,
            ViewportInsets = new Thickness(0, 60, 0, 0)
        };
        list.RemainingItemsThresholdReached += (_, _) => reached++;
        SkUiTestHelpers.Arrange(list, 300, 500);

        // The band ends at 440 in the rows: the last row (450..500) is not in it.
        Assert.Equal(8, list.LastVisibleIndex);
        Assert.Equal(0, reached);
    }

    [Fact]
    public void LoadMoreRunsWhenTheBandReachesTheEndWithoutAScroll()
    {
        var loads = 0;
        // 500 of rows in 500: only the bottom bar keeps the last row out of the band, and nothing scrolls when it goes.
        var list = List(10, new Thickness(0, 0, 0, 100));
        list.LoadMoreMode = SkUiLoadMoreMode.Auto;
        list.LoadMoreCommand = new Command(() => loads++);
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.Equal(0, loads);

        list.ViewportInsets = default;
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.Equal(0, list.ScrollY);
        Assert.Equal(1, loads);
    }

    [Fact]
    public void LoadMoreAtTheStartWaitsForTheBandsStart()
    {
        var loads = 0;
        var list = List(20, new Thickness(0, 100, 0, 0));
        list.LoadMoreMode = SkUiLoadMoreMode.Auto;
        list.LoadMorePosition = SkUiLoadMorePosition.Start;
        list.LoadMoreCommand = new Command(() => loads++);
        SkUiTestHelpers.Arrange(list, 300, 500);
        // Asked at once, at the start; done, and scrolled away.
        Assert.Equal(1, loads);
        _ = list.ScrollToAsync(600, animated: false);
        SkUiTestHelpers.Arrange(list, 300, 500);
        list.IsLoadMoreActive = false;
        loads = 0;

        // The first row (0..50 in the items, 100..150 in the extent) is under the top bar at 80, and shows below it at 40.
        _ = list.ScrollToAsync(120, animated: false);
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.Equal(0, list.FirstVisibleIndex);
        Assert.Equal(0, loads);

        _ = list.ScrollToAsync(40, animated: false);
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.Equal(1, loads);
    }

    [Fact]
    public void TheRemainingItemsThresholdWaitsForTheBandsEnd()
    {
        var reached = 0;
        var list = new SkUiVirtualScrollView
        {
            ItemsSource = Rows(20),
            ItemTemplate = new DataTemplate(() => new SkUiBox { HeightRequest = RowHeight }),
            PrefetchBudget = TimeSpan.FromSeconds(10),
            RemainingItemsThreshold = 0,
            ViewportInsets = new Thickness(0, 0, 0, 100)
        };
        list.RemainingItemsThresholdReached += (_, _) => reached++;
        SkUiTestHelpers.Arrange(list, 300, 500);

        list.ScrollTo(0, 520);
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.Equal(19, list.LastVisibleIndex);
        Assert.Equal(0, reached);

        list.ScrollTo(0, 600);
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.Equal(1, reached);
    }

    [Fact]
    public void KeepSelectionVisibleStopsAboveTheBottomBar()
    {
        var rows = Rows(40);
        var list = List(0, new Thickness(0, 60, 0, 100));
        list.ItemsSource = rows;
        list.SelectionMode = SkUiSelectionMode.Single;
        list.KeepSelectionVisible = true;
        using var surface = new SkUiTestSurface(list, 300, 500);
        surface.Frame();

        // Row 7 (410..460 on the screen) is under the bottom bar, which begins at 400.
        list.SelectedItem = rows[7];
        for (var time = 0; time <= 2000; time += 16)
            surface.Frame(time);
        var shown = RowOnScreen(list, 7);
        Assert.True(shown.Y >= 59.5 && shown.Bottom <= 400.5, $"row at {shown.Y}..{shown.Bottom}");
    }

    [Fact]
    public void AutoScrollZonesAreAtTheEndsOfTheBand()
    {
        var list = List(100, new Thickness(0, 60, 0, 100));
        list.CanReorderItems = true;
        SkUiTestHelpers.Arrange(list, 300, 500);

        // A mouse press and drag lifts the row; held just above the bottom bar, the list scrolls.
        const long id = 950_000;
        list.Touch(new(id, SkUiTouchAction.Pressed, new Point(150, 85), TimeSpan.Zero, Device: SkUiPointerDevice.Mouse));
        foreach (var (y, time) in new[] { (150d, 100), (250, 200), (395, 300) })
            list.Touch(new(id, SkUiTouchAction.Moved, new Point(150, y), TimeSpan.FromMilliseconds(time), Device: SkUiPointerDevice.Mouse));
        Assert.True(list.IsReorderAutoScrolling);
        list.Touch(new(id, SkUiTouchAction.Released, new Point(150, 395), TimeSpan.FromMilliseconds(400), Device: SkUiPointerDevice.Mouse));

        // The same place without the bar is the middle of the list: nothing scrolls.
        var plain = List(100, default);
        plain.CanReorderItems = true;
        SkUiTestHelpers.Arrange(plain, 300, 500);
        const long plainId = 950_001;
        plain.Touch(new(plainId, SkUiTouchAction.Pressed, new Point(150, 25), TimeSpan.Zero, Device: SkUiPointerDevice.Mouse));
        foreach (var (y, time) in new[] { (100d, 100), (200, 200), (395, 300) })
            plain.Touch(new(plainId, SkUiTouchAction.Moved, new Point(150, y), TimeSpan.FromMilliseconds(time), Device: SkUiPointerDevice.Mouse));
        Assert.False(plain.IsReorderAutoScrolling);
        plain.Touch(new(plainId, SkUiTouchAction.Released, new Point(150, 395), TimeSpan.FromMilliseconds(400), Device: SkUiPointerDevice.Mouse));
    }

    [Fact]
    public void TheListsRefreshIndicatorComesDownFromTheBandsStart()
    {
        var header = new SkUiBox { HeightRequest = 40 };
        var list = List(20, new Thickness(0, 60, 0, 30));
        list.SetHeader(header, sticky: true);
        SkUiTestHelpers.Arrange(list, 300, 500);

        Assert.Equal(new Rect(0, 100, 300, 370), LayerFrame(list));
    }

    [Fact]
    public void TheRefreshViewsIndicatorComesDownFromItsScrollersStartInset()
    {
        var scroll = new SkUiScrollView
        {
            Content = new SkUiBox { HeightRequest = 2000 },
            Overscroll = SkUiOverscrollMode.None,
            ViewportInsets = new Thickness(0, 60, 0, 0)
        };
        var refresh = new SkUiRefreshView { Content = scroll };
        SkUiTestHelpers.Arrange(refresh, 300, 500);
        Assert.Equal(new Rect(0, 60, 300, 440), LayerFrame(refresh));

        // It follows the inset.
        scroll.ViewportInsets = new Thickness(0, 90, 0, 0);
        SkUiTestHelpers.Arrange(refresh, 300, 500);
        Assert.Equal(new Rect(0, 90, 300, 410), LayerFrame(refresh));

        // Nothing covered: over the whole view, as before.
        scroll.ViewportInsets = default;
        SkUiTestHelpers.Arrange(refresh, 300, 500);
        Assert.Equal(new Rect(0, 0, 300, 500), LayerFrame(refresh));
    }

    [Fact]
    public void TheRefreshViewsIndicatorStartsAtTheBandOfAScrollerBelowAHeader()
    {
        var scroll = new SkUiScrollView
        {
            Content = new SkUiBox { HeightRequest = 2000 },
            Overscroll = SkUiOverscrollMode.None,
            ViewportInsets = new Thickness(0, 60, 0, 0)
        };
        var column = new SkUiGrid { RowDefinitions = [new(GridLength.Auto), new(GridLength.Star)] };
        column.Children.Add(new SkUiBox { HeightRequest = 40 });
        Grid.SetRow(scroll, 1);
        column.Children.Add(scroll);
        var refresh = new SkUiRefreshView { Content = column };
        using var surface = new SkUiTestSurface(refresh, 300, 500);
        surface.Frame();

        Assert.Equal(100, LayerFrame(refresh).Y);
    }

    [Fact]
    public void TheRefreshViewsLayerStaysAboveItsScrollersEndInset()
    {
        var scroll = new SkUiScrollView
        {
            Content = new SkUiBox { HeightRequest = 2000 },
            Overscroll = SkUiOverscrollMode.None,
            ViewportInsets = new Thickness(0, 0, 0, 80)
        };
        var refresh = new SkUiRefreshView { Content = scroll };
        using var surface = new SkUiTestSurface(refresh, 300, 500);
        surface.Frame();
        Assert.Equal(new Rect(0, 0, 300, 420), LayerFrame(refresh));

        // Both ends, and a later change of only the end reaches it.
        scroll.ViewportInsets = new Thickness(0, 60, 0, 80);
        surface.Frame();
        Assert.Equal(new Rect(0, 60, 300, 360), LayerFrame(refresh));
        scroll.ViewportInsets = new Thickness(0, 60, 0, 100);
        surface.Frame();
        Assert.Equal(new Rect(0, 60, 300, 340), LayerFrame(refresh));

        // The keyboard covers the scroller, but no view of the surface does: the layer stays.
        scroll.ViewportInsets = default;
        ((ISkUiScrollHost)scroll).KeyboardInset = 200;
        surface.Frame();
        Assert.Equal(new Rect(0, 0, 300, 500), LayerFrame(refresh));
    }

    [Fact]
    public void TheRefreshViewsLayerEndsAtTheBandOfAScrollerAboveAFooter()
    {
        var scroll = new SkUiScrollView
        {
            Content = new SkUiBox { HeightRequest = 2000 },
            Overscroll = SkUiOverscrollMode.None,
            ViewportInsets = new Thickness(0, 0, 0, 60)
        };
        var column = new SkUiGrid { RowDefinitions = [new(GridLength.Star), new(GridLength.Auto)] };
        column.Children.Add(scroll);
        var footer = new SkUiBox { HeightRequest = 40 };
        Grid.SetRow(footer, 1);
        column.Children.Add(footer);
        var refresh = new SkUiRefreshView { Content = column };
        using var surface = new SkUiTestSurface(refresh, 300, 500);
        surface.Frame();

        Assert.Equal(new Rect(0, 0, 300, 400), LayerFrame(refresh));
    }

    [Fact]
    public void APullThroughAScrollerWithAnInsetStartsAsBefore()
    {
        var refreshes = 0;
        var refresh = new SkUiRefreshView
        {
            Content = new SkUiScrollView
            {
                Content = new SkUiBox { HeightRequest = 2000 },
                Overscroll = SkUiOverscrollMode.None,
                VerticalScrollBarVisibility = ScrollBarVisibility.Never,
                ViewportInsets = new Thickness(0, 60, 0, 0)
            },
            RefreshStyle = SkUiRefreshStyle.Overlay
        };
        refresh.Refreshing += (_, _) => refreshes++;
        SkUiTestHelpers.Arrange(refresh, 300, 500);

        const long id = 950_100;
        var from = new Point(150, 100);
        refresh.Touch(new(id, SkUiTouchAction.Pressed, from, TimeSpan.Zero));
        for (var step = 1; step <= 6; step++)
            refresh.Touch(new(id, SkUiTouchAction.Moved, new Point(150, 100 + step * 50), TimeSpan.FromMilliseconds(step * 100)));
        refresh.Touch(new(id, SkUiTouchAction.Released, new Point(150, 400), TimeSpan.FromMilliseconds(900)));

        Assert.Equal(1, refreshes);
        Assert.True(refresh.IsRefreshing);
        Assert.Equal(SkUiLook.Current.RefreshRestDistance - SkUiLook.Current.RefreshIndicatorSize, refresh.RefreshIndicator.TranslationY);
    }

    #endregion

    #region V3: safe area

    private static readonly Thickness PhoneInsets = new(0, 47, 0, 34);

    private sealed class WindowScope : IDisposable
    {
        public WindowScope(Size window, Point origin, Thickness safeArea) => Set(window, origin, safeArea);

        public int Reads { get; private set; }

        public void Set(Size window, Point origin, Thickness safeArea) =>
            SkUiView.WindowPlacementOverride = _ =>
            {
                Reads++;
                return (window, origin, safeArea);
            };

        public void Dispose() => SkUiView.WindowPlacementOverride = null;
    }

    [Fact]
    public void SafeAreaInsetsFollowTheWindowWhateverTheEdgesKeepClear()
    {
        using var _ = new WindowScope(new Size(400, 800), Point.Zero, PhoneInsets);
        var root = new SkUiGrid();
        Assert.Equal(default, root.SafeAreaInsets);

        SkUiTestHelpers.Arrange(root, 400, 800);
        Assert.Equal(PhoneInsets, root.SafeAreaInsets);
        Assert.Equal(default, root.SafeAreaInsetInUse); // edge to edge: the root itself pads nothing
    }

    [Fact]
    public void SafeAreaInsetsChangeWithRotationAndTellBindings()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        using var scope = new WindowScope(new Size(400, 800), Point.Zero, PhoneInsets);
        var root = new SkUiGrid();
        var bar = new SkUiContentView { BindingContext = root };
        bar.SetBinding(SkUiContentView.PaddingProperty, nameof(SkUiView.SafeAreaInsets));
        root.Children.Add(bar);
        SkUiTestHelpers.Arrange(root, 400, 800);
        Assert.Equal(PhoneInsets, bar.Padding);

        var landscape = new Thickness(47, 0, 47, 21);
        scope.Set(new Size(800, 400), Point.Zero, landscape);
        SkUiTestHelpers.Arrange(root, 800, 400);
        Assert.Equal(landscape, root.SafeAreaInsets);
        Assert.Equal(landscape, bar.Padding);
    }

    [Fact]
    public void AScrollersViewportInsetsBindToTheSafeArea()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        using var scope = new WindowScope(new Size(400, 800), Point.Zero, PhoneInsets);
        var root = new SkUiGrid();
        var content = new SkUiBox { HeightRequest = 2000 };
        var scroll = new SkUiScrollView { Content = content, BindingContext = root };
        scroll.SetBinding(SkUiScrollView.ViewportInsetsProperty, nameof(SkUiView.SafeAreaInsets));
        root.Children.Add(scroll);
        SkUiTestHelpers.Arrange(root, 400, 800);

        Assert.Equal(PhoneInsets, scroll.ViewportInsets);
        Assert.Equal(47, content.Frame.Y);
        Assert.Equal(2000 + 47 + 34, scroll.ContentSize.Height);
    }

    [Fact]
    public void SafeAreaInsetsCountOnlyWhatReachesIntoTheRoot()
    {
        // Below a native header of 100 and above nothing: the status bar does not reach it, the home indicator does.
        using var _ = new WindowScope(new Size(400, 800), new Point(0, 100), PhoneInsets);
        var root = new SkUiGrid();
        Assert.Equal(default, root.SafeAreaInsets);
        SkUiTestHelpers.Arrange(root, 400, 700);
        Assert.Equal(new Thickness(0, 0, 0, 34), root.SafeAreaInsets);
    }

    [Fact]
    public void ViewsInsideASurfaceReadNoSafeArea()
    {
        using var _ = new WindowScope(new Size(400, 800), Point.Zero, PhoneInsets);
        var child = new SkUiBox();
        var root = new SkUiGrid { Children = { child } };
        Assert.Equal(default, child.SafeAreaInsets);
        SkUiTestHelpers.Arrange(root, 400, 800);
        Assert.Equal(default, child.SafeAreaInsets);
    }

    [Fact]
    public void AFirstReadOnALoadedRootReturnsTheInsetsAndKeepsFollowing()
    {
        using var scope = new WindowScope(new Size(400, 800), Point.Zero, PhoneInsets);
        var root = new SkUiGrid();
        SkUiTestHelpers.Arrange(root, 400, 800);
        // Loaded already: MAUI invokes a new Loaded handler while it is added, here inside the value's creation.
        typeof(VisualElement).GetField("_isLoadedFired", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(root, true);

        Assert.Equal(PhoneInsets, root.SafeAreaInsets);

        var landscape = new Thickness(47, 0, 47, 21);
        scope.Set(new Size(800, 400), Point.Zero, landscape);
        SkUiTestHelpers.Arrange(root, 800, 400);
        Assert.Equal(landscape, root.SafeAreaInsets);
    }

    [Fact]
    public void NothingAsksThePlatformUntilSafeAreaInsetsAreRead()
    {
        using var scope = new WindowScope(new Size(400, 800), Point.Zero, PhoneInsets);
        var root = new SkUiGrid();
        SkUiTestHelpers.Arrange(root, 400, 800);
        Assert.Equal(0, scope.Reads);

        Assert.Equal(PhoneInsets, root.SafeAreaInsets);
        Assert.Equal(1, scope.Reads);
    }

    #endregion
}
