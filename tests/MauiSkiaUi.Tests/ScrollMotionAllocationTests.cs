using MauiSkiaUi.Core;
using MauiSkiaUi.Rendering;
using Xunit;
using Xunit.Abstractions;

namespace MauiSkiaUi.Tests;

/// <summary>
/// What a frame of a render-thread scroll motion allocates once warm (NFR-2): the motion reports its offset to the UI thread
/// every frame through a pooled batch, and the UI side follows it, without allocating. At 1.16.0 every such frame allocated a
/// closure per report, a list and a posted closure, and another closure for the scroll bars (360 bytes while snapping).
/// </summary>
public sealed class ScrollMotionAllocationTests(ITestOutputHelper output)
{
    private const double FrameMs = 16;
    private const int MiddleFrames = 9;
    private static long _pointer = 7000;

    private static SkUiView Scroller(bool core, SnapPointsType snap, int boxes = 4)
    {
        if (core)
        {
            var coreRow = new SkUiCoreHorizontalStackLayout();
            for (var index = 0; index < boxes; index++)
                coreRow.Add(new SkUiCoreBox().SetWidth(300).SetHeight(400).SetBackground(Colors.Gray));
            var coreScroll = new SkUiCoreScrollView();
            coreScroll.SetOrientation(ScrollOrientation.Horizontal).SetSnapPointsType(snap).SetContent(coreRow);
            return new SkUiCoreHost().SetContent(coreScroll);
        }
        var row = new SkUiHorizontalStackLayout();
        for (var index = 0; index < boxes; index++)
            row.Children.Add(new SkUiBox { WidthRequest = 300, HeightRequest = 400, Color = Colors.Gray });
        return new SkUiScrollView { Orientation = ScrollOrientation.Horizontal, SnapPointsType = snap, Content = row };
    }

    /// <summary>The offset shown, with the part past the edges.</summary>
    private static double ShownOffset(SkUiView root) => root switch
    {
        SkUiCoreHost host => ((SkUiCoreScrollView)host.Content!).ScrollX,
        _ => ((SkUiScrollView)root).ScrollX + ((SkUiScrollView)root).OverscrollX
    };

    private static void Swipe(SkUiView root, Point from, Point to, double startMs)
    {
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, from, TimeSpan.FromMilliseconds(startMs)));
        root.Touch(new(id, SkUiTouchAction.Moved, new Point((from.X * 2 + to.X) / 3, from.Y), TimeSpan.FromMilliseconds(startMs + 10)));
        root.Touch(new(id, SkUiTouchAction.Moved, to, TimeSpan.FromMilliseconds(startMs + 20)));
        root.Touch(new(id, SkUiTouchAction.Released, to, TimeSpan.FromMilliseconds(startMs + 25)));
    }

    /// <summary>The fewest bytes the middle frames of a motion allocate, of a few rounds; every one of them must move the content.</summary>
    /// <param name="surface">The surface that draws the scroller.</param>
    /// <param name="start">Starts the motion at a time (ms); called every other round after <paramref name="back"/>.</param>
    /// <param name="back">Starts the motion that brings the content back.</param>
    private long MiddleFrameBytes(SkUiTestSurface surface, Action<double> start, Action<double> back)
    {
        var ms = 0d;
        surface.Frame(ms);
        var fewest = long.MaxValue;
        for (var round = 0; round < 5 && fewest > 0; round++)
        {
            foreach (var motion in (Action<double>[])[start, back])
            {
                ms += FrameMs;
                motion(ms);
                ms += 30;
                surface.Frame(ms);
                surface.Frame(ms += FrameMs);
                var from = ShownOffset(surface.Root);
                var allocated = GC.GetAllocatedBytesForCurrentThread();
                for (var frame = 0; frame < MiddleFrames; frame++)
                    surface.Frame(ms += FrameMs);
                allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                Assert.True(Math.Abs(ShownOffset(surface.Root) - from) > 1, "the content moves during the measured frames");
                if (round > 0) // the first round warms up
                    fewest = Math.Min(fewest, allocated);
                for (var frame = 0; frame < 250; frame++) // settled, bars faded out
                    surface.Frame(ms += FrameMs);
            }
        }
        output.WriteLine($"{fewest} bytes in {MiddleFrames} frames");
        return fewest;
    }

    [Theory]
    [InlineData(false, SnapPointsType.MandatorySingle)]
    [InlineData(false, SnapPointsType.Mandatory)]
    [InlineData(false, SnapPointsType.None)]
    [InlineData(true, SnapPointsType.MandatorySingle)]
    [InlineData(true, SnapPointsType.None)]
    public void AFlingAllocatesNothing(bool core, SnapPointsType snap)
    {
        var root = Scroller(core, snap);
        using var surface = new SkUiTestSurface(root, 300, 400);
        Assert.Equal(0, MiddleFrameBytes(surface,
            ms => Swipe(root, new Point(250, 200), new Point(50, 200), ms),
            ms => Swipe(root, new Point(50, 200), new Point(250, 200), ms)));
    }

    [Fact]
    public void AnAnimatedScrollAllocatesNothing()
    {
        var root = (SkUiScrollView)Scroller(core: false, SnapPointsType.None);
        using var surface = new SkUiTestSurface(root, 300, 400);
        Assert.Equal(0, MiddleFrameBytes(surface,
            ms => { _ = root.ScrollToAsync(600, 0, animated: true); },
            ms => { _ = root.ScrollToAsync(0, 0, animated: true); }));
    }

    [Fact]
    public void ASpringBackFromOverscrollAllocatesNothing()
    {
        var root = (SkUiScrollView)Scroller(core: false, SnapPointsType.None);
        root.Overscroll = SkUiOverscrollMode.Bounce;
        using var surface = new SkUiTestSurface(root, 300, 400);
        Assert.Equal(0, MiddleFrameBytes(surface, Pull, Pull));
        return;

        // Slow at the release, so the content springs back instead of being flung.
        void Pull(double ms)
        {
            var id = ++_pointer;
            root.Touch(new(id, SkUiTouchAction.Pressed, new Point(20, 200), TimeSpan.FromMilliseconds(ms - 500)));
            root.Touch(new(id, SkUiTouchAction.Moved, new Point(100, 200), TimeSpan.FromMilliseconds(ms - 400)));
            root.Touch(new(id, SkUiTouchAction.Moved, new Point(280, 200), TimeSpan.FromMilliseconds(ms - 300)));
            root.Touch(new(id, SkUiTouchAction.Moved, new Point(280, 200), TimeSpan.FromMilliseconds(ms - 10)));
            root.Touch(new(id, SkUiTouchAction.Released, new Point(280, 200), TimeSpan.FromMilliseconds(ms)));
        }
    }
}
