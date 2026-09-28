using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>Native overlays inside scrollers: viewport clip and snapshot freeze while moving.</summary>
[Collection(nameof(OverlayScrollTests))] // uses the static capture hook
public class OverlayScrollTests
{
    private static (SkUiContentView Root, SkUiScrollView Scroll, SkUiMauiContentView Overlay) Form(SkUiOverlayScrollMode mode)
    {
        var overlay = new SkUiMauiContentView { Content = new BoxView(), HeightRequest = 40, ScrollMode = mode };
        var stack = new SkUiVerticalStackLayout();
        stack.Children.Add(new SkUiBox { HeightRequest = 100 });
        stack.Children.Add(overlay);
        stack.Children.Add(new SkUiBox { HeightRequest = 1000 });
        var scroll = new SkUiScrollView { Content = stack, Margin = new Thickness(0, 50, 0, 0) };
        var root = new SkUiContentView { Content = scroll };
        SkUiTestHelpers.Arrange(root, 200, 250); // scroll viewport: y 50..250
        return (root, scroll, overlay);
    }

    [Fact]
    public void ClipIsTheScrollViewportAndEmptyWhenScrolledOut()
    {
        var (_, scroll, overlay) = Form(SkUiOverlayScrollMode.Live);
        Assert.Equal(new Rect(0, 150, 200, 40), overlay.ComputeRootRelativeFrame());
        Assert.Equal(new Rect(0, 50, 200, 200), overlay.ComputeRootRelativeClip());

        scroll.ScrollTo(0, 120); // overlay at root y 30..70: only 50..70 visible
        Assert.Equal(new Rect(0, 30, 200, 40), overlay.ComputeRootRelativeFrame());
        Assert.Equal(20, overlay.ComputeRootRelativeFrame().Intersect(overlay.ComputeRootRelativeClip()).Height, 3);

        scroll.ScrollTo(0, 400);
        var visible = overlay.ComputeRootRelativeFrame().Intersect(overlay.ComputeRootRelativeClip());
        Assert.True(visible.Width <= 0 || visible.Height <= 0);
    }

    [Fact]
    public void SnapshotReplacesTheNativeViewWhileScrollingAndRestoresAfterTheDelay()
    {
        using var clock = new ManualGestureClock();
        var captures = 0;
        SkUiMauiContentView.CaptureOverride = (_, done) =>
        {
            captures++;
            using var bitmap = new SKBitmap(10, 10);
            bitmap.Erase(SKColors.Magenta);
            done(SKImage.FromBitmap(bitmap));
            return true;
        };
        try
        {
            var (root, scroll, overlay) = Form(SkUiOverlayScrollMode.Snapshot);
            using var surface = new SkUiTestSurface(root, 200, 250);
            surface.Frame(0);

            root.Touch(new(1, SkUiTouchAction.Pressed, new Point(100, 150), TimeSpan.Zero));
            Assert.False(overlay.IsShowingSnapshot);
            root.Touch(new(1, SkUiTouchAction.Moved, new Point(100, 120), TimeSpan.FromMilliseconds(100)));
            Assert.True(scroll.IsScrolling);
            Assert.True(overlay.IsShowingSnapshot);
            Assert.Equal(1, captures);
            // The snapshot is drawn content: it scrolls with the rest (root y = 50 + 100 − 30 = 120).
            Assert.Equal(SKColors.Magenta, surface.Frame(120).GetPixel(100, 130));

            root.Touch(new(1, SkUiTouchAction.Released, new Point(100, 120), TimeSpan.FromMilliseconds(600)));
            Assert.False(scroll.IsScrolling);
            Assert.True(overlay.IsShowingSnapshot); // restore waits for the delay
            clock.Advance(SkUiMauiContentView.SnapshotRestoreDelay);
            Assert.False(overlay.IsShowingSnapshot);
            Assert.NotEqual(SKColors.Magenta, surface.Frame(1000).GetPixel(100, 130));

            // A new drag within the delay keeps the same snapshot (no re-capture).
            root.Touch(new(2, SkUiTouchAction.Pressed, new Point(100, 150), TimeSpan.FromSeconds(2)));
            root.Touch(new(2, SkUiTouchAction.Moved, new Point(100, 120), TimeSpan.FromSeconds(2.1)));
            root.Touch(new(2, SkUiTouchAction.Released, new Point(100, 120), TimeSpan.FromSeconds(3)));
            clock.Advance(TimeSpan.FromMilliseconds(50));
            root.Touch(new(3, SkUiTouchAction.Pressed, new Point(100, 150), TimeSpan.FromSeconds(3.05)));
            root.Touch(new(3, SkUiTouchAction.Moved, new Point(100, 120), TimeSpan.FromSeconds(3.1)));
            Assert.Equal(2, captures);
            root.Touch(new(3, SkUiTouchAction.Released, new Point(100, 120), TimeSpan.FromSeconds(4)));
            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.False(overlay.IsShowingSnapshot);
        }
        finally
        {
            SkUiMauiContentView.CaptureOverride = null;
        }
    }

    [Fact]
    public void ScrollModeChangesKeepTheMovingScrollerCount()
    {
        using var clock = new ManualGestureClock();
        SkUiMauiContentView.CaptureOverride = (_, done) => { done(SKImage.Create(new SKImageInfo(4, 4))); return true; };
        try
        {
            var (_, _, overlay) = Form(SkUiOverlayScrollMode.Snapshot);
            overlay.NotifyAncestorScrollMotion(true); // scroller A
            Assert.True(overlay.IsShowingSnapshot);
            overlay.ScrollMode = SkUiOverlayScrollMode.Live;
            Assert.False(overlay.IsShowingSnapshot);
            overlay.NotifyAncestorScrollMotion(true); // scroller B, while Live
            overlay.ScrollMode = SkUiOverlayScrollMode.Snapshot;
            Assert.True(overlay.IsShowingSnapshot); // A and B still move
            overlay.NotifyAncestorScrollMotion(false); // A stops; B still moves
            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.True(overlay.IsShowingSnapshot);
            overlay.NotifyAncestorScrollMotion(false);
            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.False(overlay.IsShowingSnapshot);
        }
        finally
        {
            SkUiMauiContentView.CaptureOverride = null;
        }
    }

    [Fact]
    public void StaleAsyncCapturesAreIgnored()
    {
        using var clock = new ManualGestureClock();
        var pending = new List<Action<SKImage?>>();
        SkUiMauiContentView.CaptureOverride = (_, done) => { pending.Add(done); return true; };
        try
        {
            var (_, _, overlay) = Form(SkUiOverlayScrollMode.Snapshot);
            overlay.NotifyAncestorScrollMotion(true);
            overlay.NotifyAncestorScrollMotion(false);
            clock.Advance(TimeSpan.FromSeconds(1)); // restored before the first capture finished
            overlay.NotifyAncestorScrollMotion(true);
            Assert.Equal(2, pending.Count);

            pending[0](SKImage.Create(new SKImageInfo(4, 4))); // stale: must not clear the second capture
            Assert.False(overlay.IsShowingSnapshot);
            overlay.NotifyAncestorScrollMotion(false);
            overlay.NotifyAncestorScrollMotion(true);
            Assert.Equal(2, pending.Count); // second capture still in flight: no third one
            pending[1](SKImage.Create(new SKImageInfo(4, 4)));
            Assert.True(overlay.IsShowingSnapshot);

            overlay.ScrollMode = SkUiOverlayScrollMode.Live;
            overlay.ScrollMode = SkUiOverlayScrollMode.Snapshot; // still moving: captures again
            Assert.Equal(3, pending.Count);
            overlay.ScrollMode = SkUiOverlayScrollMode.Live;
            pending[2](SKImage.Create(new SKImageInfo(4, 4))); // completes after switching to Live
            Assert.False(overlay.IsShowingSnapshot);
        }
        finally
        {
            SkUiMauiContentView.CaptureOverride = null;
        }
    }

    [Fact]
    public void LiveModeNeverSnapshotsAndProgrammaticJumpsDoNot()
    {
        var captures = 0;
        SkUiMauiContentView.CaptureOverride = (_, _) => { captures++; return false; };
        try
        {
            var (root, scroll, overlay) = Form(SkUiOverlayScrollMode.Live);
            root.Touch(new(1, SkUiTouchAction.Pressed, new Point(100, 150), TimeSpan.Zero));
            root.Touch(new(1, SkUiTouchAction.Moved, new Point(100, 100), TimeSpan.FromMilliseconds(100)));
            root.Touch(new(1, SkUiTouchAction.Released, new Point(100, 100), TimeSpan.FromMilliseconds(600)));
            Assert.Equal(0, captures);

            overlay.ScrollMode = SkUiOverlayScrollMode.Snapshot;
            scroll.ScrollTo(0, 10); // an instant jump is not motion
            Assert.Equal(0, captures);
            Assert.False(overlay.IsShowingSnapshot);
        }
        finally
        {
            SkUiMauiContentView.CaptureOverride = null;
        }
    }

    [Fact]
    public void ChainedDragMovesTheOuterScrollerAndFreezesItsOverlays()
    {
        using var clock = new ManualGestureClock();
        SkUiMauiContentView.CaptureOverride = (_, done) => { done(SKImage.Create(new SKImageInfo(4, 4))); return true; };
        try
        {
            var inner = new SkUiScrollView { HeightRequest = 100, Content = new SkUiBox { HeightRequest = 150 } }; // max 50
            var overlay = new SkUiMauiContentView { Content = new BoxView(), HeightRequest = 40, ScrollMode = SkUiOverlayScrollMode.Snapshot };
            var stack = new SkUiVerticalStackLayout();
            stack.Children.Add(inner);
            stack.Children.Add(overlay);
            stack.Children.Add(new SkUiBox { HeightRequest = 1000 });
            var outer = new SkUiScrollView { Content = stack };
            SkUiTestHelpers.Arrange(outer, 200, 300);

            outer.Touch(new(1, SkUiTouchAction.Pressed, new Point(100, 90), TimeSpan.Zero));
            outer.Touch(new(1, SkUiTouchAction.Moved, new Point(100, 60), TimeSpan.FromMilliseconds(100)));
            Assert.False(overlay.IsShowingSnapshot); // only the inner scroller moved
            outer.Touch(new(1, SkUiTouchAction.Moved, new Point(100, 0), TimeSpan.FromMilliseconds(200)));
            Assert.Equal(50, inner.ScrollY);
            Assert.True(outer.ScrollY > 0);
            Assert.True(overlay.IsShowingSnapshot); // the outer scroller moves via chaining
            outer.Touch(new(1, SkUiTouchAction.Released, new Point(100, 0), TimeSpan.FromMilliseconds(900)));
            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.False(overlay.IsShowingSnapshot);
        }
        finally
        {
            SkUiMauiContentView.CaptureOverride = null;
        }
    }

    [Fact]
    public void DragsStartingOnAnOverlayScrollTheDrawnScrollerButTapsStayNative()
    {
        var (root, scroll, overlay) = Form(SkUiOverlayScrollMode.Live);
        var drawnTaps = 0;
        scroll.Tapped += (_, _) => drawnTaps++;
        var router = root.Router;
        // Pointer on the overlay at root (100, 170): a tap stays native (no drawn tap), nothing is claimed.
        router.DispatchFromOverlay(new(900, SkUiTouchAction.Pressed, new Point(100, 170), TimeSpan.Zero), overlay);
        Assert.Equal(SkUiNativeGestureState.Pending, router.StateOf(900));
        router.DispatchFromOverlay(new(900, SkUiTouchAction.Released, new Point(100, 170), TimeSpan.FromMilliseconds(80)), overlay);
        Assert.Equal(0, drawnTaps);
        Assert.Equal(0, scroll.ScrollY);

        // A vertical drag is claimed by the drawn scroller (the platform then cancels the native touch).
        router.DispatchFromOverlay(new(901, SkUiTouchAction.Pressed, new Point(100, 170), TimeSpan.FromSeconds(1)), overlay);
        router.DispatchFromOverlay(new(901, SkUiTouchAction.Moved, new Point(100, 130), TimeSpan.FromSeconds(1.05)), overlay);
        Assert.Equal(SkUiNativeGestureState.Claimed, router.StateOf(901));
        router.DispatchFromOverlay(new(901, SkUiTouchAction.Moved, new Point(100, 100), TimeSpan.FromSeconds(1.1)), overlay);
        Assert.Equal(70, scroll.ScrollY);
        router.DispatchFromOverlay(new(901, SkUiTouchAction.Released, new Point(100, 100), TimeSpan.FromSeconds(2)), overlay);
        Assert.Equal(SkUiNativeGestureState.None, router.StateOf(901));

        // A horizontal drag (text selection / cursor) is not the vertical scroller's: it stays native.
        router.DispatchFromOverlay(new(902, SkUiTouchAction.Pressed, new Point(60, 150), TimeSpan.FromSeconds(3)), overlay);
        router.DispatchFromOverlay(new(902, SkUiTouchAction.Moved, new Point(160, 152), TimeSpan.FromSeconds(3.1)), overlay);
        Assert.Equal(SkUiNativeGestureState.None, router.StateOf(902));
        router.DispatchFromOverlay(new(902, SkUiTouchAction.Released, new Point(160, 152), TimeSpan.FromSeconds(3.5)), overlay);
        Assert.Equal(70, scroll.ScrollY);
    }
}
