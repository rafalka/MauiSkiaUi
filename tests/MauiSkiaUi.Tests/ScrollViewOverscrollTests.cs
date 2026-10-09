using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// C2 overscroll events on <see cref="SkUiScrollView"/> and <see cref="SkUiCoreScrollView"/>: <c>PullEdges</c> (pulls past
/// the edges of content that does not overflow, with overscroll off), <c>Overscrolled</c> while dragging and springing back
/// (one reused argument instance), <c>PullReleased</c> per edge, physical edges in right-to-left layouts, overscroll modes
/// without pull edges, and pull edges beside a refresh view.
/// </summary>
public class ScrollViewOverscrollTests
{
    private static long _pointer = 180_000;

    private static SkUiScrollView Scroller(double contentHeight, SkUiScrollEdges edges = SkUiScrollEdges.None) => new()
    {
        Content = new SkUiBox { HeightRequest = contentHeight, WidthRequest = 300 },
        Overscroll = SkUiOverscrollMode.None,
        VerticalScrollBarVisibility = ScrollBarVisibility.Never,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
        PullEdges = edges
    };

    /// <summary>A slow drag (100 ms per step: no fling).</summary>
    private static void Drag(SkUiView root, Point from, Point to, bool release = true, int steps = 6)
    {
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, from, TimeSpan.FromMilliseconds(0)));
        for (var step = 1; step <= steps; step++)
        {
            var point = new Point(from.X + (to.X - from.X) * step / steps, from.Y + (to.Y - from.Y) * step / steps);
            root.Touch(new(id, SkUiTouchAction.Moved, point, TimeSpan.FromMilliseconds(step * 100)));
        }
        if (release)
            Release(root, to, steps * 100 + 300);
    }

    private static void Release(SkUiView root, Point at, double ms = 1000) =>
        root.Touch(new(_pointer, SkUiTouchAction.Released, at, TimeSpan.FromMilliseconds(ms)));

    [Fact]
    public void WithoutPullEdgesShortContentIsNotPulled()
    {
        var scroller = Scroller(100);
        var reports = 0;
        scroller.Overscrolled += (_, _) => reports++;
        scroller.PullReleased += (_, _) => reports++;
        SkUiTestHelpers.Arrange(scroller, 300, 400);

        Drag(scroller, new Point(150, 50), new Point(150, 350));
        Drag(scroller, new Point(150, 350), new Point(150, 50));
        Assert.Equal(0, reports);
        Assert.Equal(SkUiScrollEdges.None, scroller.PullEdges);
    }

    [Fact]
    public void PullEdgesReportPullsAtTheTopAndBottomOfShortContent()
    {
        var scroller = Scroller(100, SkUiScrollEdges.Top | SkUiScrollEdges.Bottom);
        var shown = new List<(double X, double Y, bool Dragging)>();
        var released = new List<SkUiPullReleasedEventArgs>();
        scroller.Overscrolled += (_, args) => shown.Add((args.OverscrollX, args.OverscrollY, args.IsDragging));
        scroller.PullReleased += (_, args) => released.Add(args);
        SkUiTestHelpers.Arrange(scroller, 300, 400);

        // Down: past the top, growing with resistance, while dragging.
        Drag(scroller, new Point(150, 50), new Point(150, 350), release: false);
        Assert.NotEmpty(shown);
        Assert.All(shown, report => Assert.True(report.Y < 0 && report.X == 0 && report.Dragging));
        Assert.True(shown.Zip(shown.Skip(1)).All(pair => pair.Second.Y < pair.First.Y));
        var pulled = -scroller.OverscrollY;
        Assert.InRange(pulled, 50, 299); // the rubber band resists
        Release(scroller, new Point(150, 350));
        var top = Assert.Single(released);
        Assert.Equal(SkUiScrollEdges.Top, top.Edge);
        Assert.Equal(pulled, top.Distance);
        Assert.Equal(0, scroller.ScrollY);

        // Up: past the bottom.
        released.Clear();
        scroller.ScrollTo(0, 0); // drops the spring-back (no frames run headlessly)
        Drag(scroller, new Point(150, 350), new Point(150, 50));
        var bottom = Assert.Single(released);
        Assert.Equal(SkUiScrollEdges.Bottom, bottom.Edge);
        Assert.True(bottom.Distance > 0);
    }

    [Fact]
    public void OnlyTheChosenEdgesAndTheScrolledAxesPull()
    {
        var scroller = Scroller(100, SkUiScrollEdges.Bottom | SkUiScrollEdges.Left);
        var released = new List<SkUiScrollEdges>();
        scroller.PullReleased += (_, args) => released.Add(args.Edge);
        SkUiTestHelpers.Arrange(scroller, 300, 400);

        Drag(scroller, new Point(150, 50), new Point(150, 350)); // the top does not pull
        Drag(scroller, new Point(20, 200), new Point(280, 205)); // a vertical scroller has no left edge to pull
        Assert.Empty(released);
        Drag(scroller, new Point(150, 350), new Point(150, 50));
        Assert.Equal([SkUiScrollEdges.Bottom], released);

        Assert.Throws<ArgumentOutOfRangeException>(() => scroller.SetPullEdges((SkUiScrollEdges)16));
        scroller.PullEdges = (SkUiScrollEdges)16; // ignored, as invalid bindable values are
        Assert.Equal(SkUiScrollEdges.Bottom | SkUiScrollEdges.Left, scroller.PullEdges);
    }

    [Fact]
    public void HorizontalEdgesArePhysicalAlsoInRightToLeftLayouts()
    {
        var scroller = Scroller(100, SkUiScrollEdges.Left | SkUiScrollEdges.Right);
        scroller.Orientation = ScrollOrientation.Horizontal;
        scroller.FlowDirection = FlowDirection.RightToLeft;
        ((SkUiBox)scroller.Content!).WidthRequest = 100;
        var released = new List<SkUiPullReleasedEventArgs>();
        scroller.PullReleased += (_, args) => released.Add(args);
        SkUiTestHelpers.Arrange(scroller, 300, 400);

        // The finger moves right: the content shows past its left edge.
        Drag(scroller, new Point(20, 200), new Point(280, 200));
        Assert.Equal(SkUiScrollEdges.Left, Assert.Single(released).Edge);
        Assert.True(released[0].Distance > 0);
        scroller.ScrollTo(0, 0);
        Drag(scroller, new Point(280, 200), new Point(20, 200));
        Assert.Equal(SkUiScrollEdges.Right, released[1].Edge);
    }

    [Fact]
    public void TheSpringBackIsReportedUntilZeroWithOneReusedArgumentsInstance()
    {
        var scroller = Scroller(100, SkUiScrollEdges.Top);
        var instances = new HashSet<SkUiOverscrolledEventArgs>();
        var last = (Y: double.NaN, Dragging: true);
        var settling = 0;
        scroller.Overscrolled += (_, args) =>
        {
            instances.Add(args);
            last = (args.OverscrollY, args.IsDragging);
            if (!args.IsDragging)
                settling++;
        };
        using var surface = new SkUiTestSurface(scroller, 300, 400);
        surface.Frame(0);

        Drag(scroller, new Point(150, 50), new Point(150, 350));
        Assert.True(scroller.OverscrollY < 0); // released: springs back on the render thread
        for (var frame = 1; frame <= 60; frame++)
            surface.Frame(frame * 16);
        Assert.True(settling > 1);
        Assert.Equal((0d, false), last);
        Assert.Equal(0, scroller.OverscrollY);
        Assert.Single(instances);
    }

    [Fact]
    public void ADragCatchingASpringBackBeforeItsFirstFrameTakesTheOverscrollBack()
    {
        // A press right after a pull is released (an inline refresh that just ended springs back the same way) catches the
        // spring-back before it reported a frame: the drag must take back the overscroll still shown, not leave it to be
        // reported as a second pull. Content that scrolls: the drag would otherwise scroll it with the overscroll left shown.
        var scroller = Scroller(2000, SkUiScrollEdges.Top);
        var pulls = new List<double>();
        scroller.PullReleased += (_, args) => pulls.Add(args.Distance);
        using var surface = new SkUiTestSurface(scroller, 300, 400);
        surface.Frame(0);

        Drag(scroller, new Point(150, 50), new Point(150, 350));
        Assert.Single(pulls);
        Assert.True(scroller.OverscrollY < 0);
        Drag(scroller, new Point(150, 350), new Point(150, 50)); // no frame in between
        Assert.Single(pulls);
        Assert.Equal(0, scroller.OverscrollY);
    }

    [Fact]
    public void OverscrollModesReportWithoutPullEdges()
    {
        var scroller = Scroller(1000);
        scroller.Overscroll = SkUiOverscrollMode.Bounce;
        var released = new List<SkUiScrollEdges>();
        var reports = 0;
        scroller.Overscrolled += (_, _) => reports++;
        scroller.PullReleased += (_, args) => released.Add(args.Edge);
        SkUiTestHelpers.Arrange(scroller, 300, 400);

        Drag(scroller, new Point(150, 50), new Point(150, 350));
        Assert.True(reports > 0);
        Assert.Equal([SkUiScrollEdges.Top], released);
    }

    [Fact]
    public void CoreScrollViewsHaveTheSameEvents()
    {
        var core = new SkUiCoreScrollView();
        core.SetContent(new SkUiCoreBox().SetHeight(100));
        core.SetOverscroll(SkUiOverscrollMode.None).SetPullEdges(SkUiScrollEdges.Top);
        var reports = 0;
        var released = new List<SkUiPullReleasedEventArgs>();
        core.Overscrolled += (sender, args) =>
        {
            Assert.Same(core, sender);
            reports++;
        };
        core.PullReleased += (_, args) => released.Add(args);
        var host = new SkUiCoreHost().SetContent(core);
        SkUiTestHelpers.Arrange(host, 300, 400);

        Drag(host, new Point(150, 50), new Point(150, 350), release: false);
        Assert.True(reports > 0);
        Assert.True(core.OverscrollY < 0);
        Release(host, new Point(150, 350));
        Assert.Equal(SkUiScrollEdges.Top, Assert.Single(released).Edge);
        Assert.Equal(SkUiScrollEdges.Top, core.PullEdges);
    }

    [Fact]
    public void PullEdgesWorkBesideARefreshViewPullingTheTop()
    {
        var scroller = Scroller(100, SkUiScrollEdges.Bottom);
        var refresh = new SkUiRefreshView { Content = scroller };
        var bottom = 0;
        scroller.PullReleased += (_, args) => { if (args.Edge == SkUiScrollEdges.Bottom) bottom++; };
        SkUiTestHelpers.Arrange(refresh, 300, 400);

        Drag(refresh, new Point(150, 350), new Point(150, 50));
        Assert.Equal(1, bottom);
        Assert.False(refresh.IsRefreshing);
        scroller.ScrollTo(0, 0);
        Drag(refresh, new Point(150, 50), new Point(150, 380));
        Assert.True(refresh.IsRefreshing);
        Assert.Equal(SkUiScrollEdges.Bottom, scroller.PullEdges);
    }
}
