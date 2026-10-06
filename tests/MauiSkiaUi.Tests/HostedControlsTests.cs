using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Hosted-control regression suite (A6): native overlays in nested scrollers, during flings, animated scrolls and
/// bounces, in containers that switch their content, and while their content is replaced or re-parented. What only a
/// device shows (IME, keyboard, real platform views) is in the device tests' hosted check and Testing.md.
/// </summary>
[Collection(nameof(OverlayScrollTests))] // uses the static capture hook
public class HostedControlsTests : IDisposable
{
    private int _captures;

    public HostedControlsTests() =>
        SkUiMauiContentView.CaptureOverride = (_, done) =>
        {
            _captures++;
            using var bitmap = new SKBitmap(10, 10);
            bitmap.Erase(SKColors.Magenta);
            done(SKImage.FromBitmap(bitmap));
            return true;
        };

    public void Dispose() => SkUiMauiContentView.CaptureOverride = null;

    private static SkUiScrollController Scroller(SkUiScrollView view) => ((ISkUiScrollHost)view).Scroller;

    private static SkUiMauiContentView Overlay(double height = 40, SkUiOverlayScrollMode mode = SkUiOverlayScrollMode.Snapshot) =>
        new() { Content = new BoxView(), HeightRequest = height, ScrollMode = mode };

    /// <summary>A vertical scroller (viewport 200 × 200) with <paramref name="overlay"/> 100 DIPs down a 1000 DIP column.</summary>
    private static SkUiScrollView Column(SkUiMauiContentView overlay, SkUiOverscrollMode overscroll = SkUiOverscrollMode.None)
    {
        var stack = new SkUiVerticalStackLayout { Children = { new SkUiBox { HeightRequest = 100 }, overlay, new SkUiBox { HeightRequest = 1000 } } };
        return new SkUiScrollView { Content = stack, Overscroll = overscroll };
    }

    [Fact]
    public void NestedScrollersOffsetAndClipTheOverlayByBoth()
    {
        // Outer vertical scroller (200 × 200); inside, 50 DIPs down, a horizontal one (200 × 60) with the overlay at x 250.
        var overlay = Overlay(60, SkUiOverlayScrollMode.Live);
        overlay.WidthRequest = 100;
        var row = new SkUiHorizontalStackLayout { Children = { new SkUiBox { WidthRequest = 250 }, overlay, new SkUiBox { WidthRequest = 400 } } };
        var inner = new SkUiScrollView { Orientation = ScrollOrientation.Horizontal, HeightRequest = 60, Content = row };
        var column = new SkUiVerticalStackLayout { Children = { new SkUiBox { HeightRequest = 50 }, inner, new SkUiBox { HeightRequest = 1000 } } };
        var outer = new SkUiScrollView { Content = column };
        SkUiTestHelpers.Arrange(outer, 200, 200);

        Assert.Equal(new Rect(250, 50, 100, 60), overlay.ComputeRootRelativeFrame());
        Assert.True(Visible(overlay).IsEmpty); // right of the inner viewport

        inner.ScrollTo(200, 0);
        Assert.Equal(new Rect(50, 50, 100, 60), overlay.ComputeRootRelativeFrame());
        Assert.Equal(new Rect(0, 50, 200, 60), overlay.ComputeRootRelativeClip()); // the inner viewport, inside the outer one

        outer.ScrollTo(0, 80); // the inner scroller is half above the outer viewport
        Assert.Equal(new Rect(50, -30, 100, 60), overlay.ComputeRootRelativeFrame());
        Assert.Equal(new Rect(50, 0, 100, 30), Visible(overlay));

        // Both scrollers move it: either one moving freezes it in snapshot mode.
        overlay.ScrollMode = SkUiOverlayScrollMode.Snapshot;
        using var clock = new ManualGestureClock();
        inner.Touch(new(1, SkUiTouchAction.Pressed, new Point(100, 10), TimeSpan.Zero));
        inner.Touch(new(1, SkUiTouchAction.Moved, new Point(60, 12), TimeSpan.FromMilliseconds(100)));
        Assert.True(inner.IsScrolling);
        Assert.False(outer.IsScrolling);
        Assert.True(overlay.IsShowingSnapshot);
        inner.Touch(new(1, SkUiTouchAction.Released, new Point(60, 12), TimeSpan.FromMilliseconds(900)));
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.False(overlay.IsShowingSnapshot);
        Assert.Equal(new Rect(10, -30, 100, 60), overlay.ComputeRootRelativeFrame()); // placed where the drag left it
    }

    [Fact]
    public void AFlingFreezesOverlaysUntilItEndsAndThenPlacesThemWhereTheContentStopped()
    {
        using var clock = new ManualGestureClock();
        var overlay = Overlay();
        var scroll = Column(overlay);
        using var surface = new SkUiTestSurface(scroll, 200, 200);
        surface.Frame(0);

        Assert.True(Scroller(scroll).StartFling(new Point(0, 600)));
        surface.Frame(10);
        Assert.True(overlay.IsShowingSnapshot);
        Assert.True(overlay.IsNativeHidden);
        // The snapshot is drawn content: during the render-thread fling it moves with the column.
        surface.Frame(150);
        var y = 100 - scroll.ScrollY;
        Assert.True(scroll.ScrollY > 0);
        if (y > 0)
            Assert.Equal(SKColors.Magenta, surface.Frame(160).GetPixel(100, (int)(100 - scroll.ScrollY) + 20));

        surface.Frame(5000);
        Assert.False(scroll.IsScrolling);
        Assert.True(overlay.IsShowingSnapshot); // restored after the delay
        clock.Advance(SkUiMauiContentView.SnapshotRestoreDelay);
        Assert.False(overlay.IsShowingSnapshot);
        Assert.False(overlay.IsNativeHidden);
        Assert.Equal(new Rect(0, 100 - scroll.ScrollY, 200, 40), overlay.ComputeRootRelativeFrame());
        Assert.Equal(1, _captures);
    }

    [Fact]
    public async Task AnAnimatedScrollFreezesOverlaysAndAnInstantOneDoesNot()
    {
        using var clock = new ManualGestureClock();
        var overlay = Overlay();
        var scroll = Column(overlay);
        using var surface = new SkUiTestSurface(scroll, 200, 200);
        surface.Frame(0);

        var scrolled = scroll.ScrollToAsync(0, 300, animated: true);
        surface.Frame(10);
        Assert.True(overlay.IsShowingSnapshot);
        for (var ms = 26; !scrolled.IsCompleted && ms < 3000; ms += 16)
            surface.Frame(ms);
        await scrolled;
        Assert.Equal(300, scroll.ScrollY);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.False(overlay.IsShowingSnapshot);
        Assert.Equal(-200, overlay.ComputeRootRelativeFrame().Y);

        await scroll.ScrollToAsync(0, 50, animated: false);
        Assert.False(overlay.IsShowingSnapshot);
        Assert.Equal(1, _captures);
        Assert.Equal(50, overlay.ComputeRootRelativeFrame().Y);
    }

    [Fact]
    public async Task ScrollingToAHostedControlBringsItIntoTheViewport()
    {
        var overlay = Overlay(40, SkUiOverlayScrollMode.Live);
        var stack = new SkUiVerticalStackLayout { Children = { new SkUiBox { HeightRequest = 600 }, overlay, new SkUiBox { HeightRequest = 600 } } };
        var scroll = new SkUiScrollView { Content = stack };
        SkUiTestHelpers.Arrange(scroll, 200, 200);
        Assert.True(Visible(overlay).IsEmpty);

        await scroll.ScrollToAsync(overlay, ScrollToPosition.MakeVisible, animated: false);
        Assert.Equal(new Rect(0, 160, 200, 40), overlay.ComputeRootRelativeFrame()); // at the viewport's bottom
        Assert.Equal(overlay.ComputeRootRelativeFrame(), Visible(overlay));
        await scroll.ScrollToAsync(overlay, ScrollToPosition.Center, animated: false);
        Assert.Equal(80, overlay.ComputeRootRelativeFrame().Y);
    }

    [Fact]
    public void OverlaysFollowABounceAndLandBackInPlace()
    {
        var overlay = Overlay(40, SkUiOverlayScrollMode.Live);
        var scroll = Column(overlay, SkUiOverscrollMode.Bounce);
        using var clock = new ManualGestureClock();
        using var surface = new SkUiTestSurface(scroll, 200, 200);
        surface.Frame(0);

        scroll.Touch(new(1, SkUiTouchAction.Pressed, new Point(100, 20), TimeSpan.Zero));
        scroll.Touch(new(1, SkUiTouchAction.Moved, new Point(100, 60), TimeSpan.FromMilliseconds(50)));
        scroll.Touch(new(1, SkUiTouchAction.Moved, new Point(100, 120), TimeSpan.FromMilliseconds(100)));
        Assert.Equal(0, scroll.ScrollY);
        Assert.True(Scroller(scroll).OverscrollY < 0);
        // The column is drawn pulled down past the top: the native view goes with it, not with the clamped offset.
        Assert.True(overlay.ComputeRootRelativeFrame().Y > 100);

        scroll.Touch(new(1, SkUiTouchAction.Released, new Point(100, 120), TimeSpan.FromMilliseconds(400)));
        for (var ms = 400; ms < 3000; ms += 16)
            surface.Frame(ms);
        Assert.Equal(0, Scroller(scroll).OverscrollY);
        Assert.Equal(100, overlay.ComputeRootRelativeFrame().Y);
    }

    [Fact]
    public void ReplacingContentWhileASnapshotShowsDropsTheOldImage()
    {
        using var clock = new ManualGestureClock();
        var overlay = Overlay();
        var scroll = Column(overlay);
        using var surface = new SkUiTestSurface(scroll, 200, 200);
        overlay.NotifyAncestorScrollMotion(true);
        Assert.True(overlay.IsShowingSnapshot);

        // The bitmap shows the old control: replacing the content must not keep drawing it. Still moving, so the new
        // control is captured in turn.
        overlay.Content = new BoxView();
        Assert.Equal(2, _captures);
        Assert.True(overlay.IsShowingSnapshot);

        SkUiMauiContentView.CaptureOverride = (_, _) => false; // capture unavailable (not laid out yet, no handler)
        overlay.Content = new BoxView();
        Assert.False(overlay.IsShowingSnapshot);
        Assert.False(overlay.IsNativeHidden); // the new control shows live

        overlay.Content = null;
        overlay.NotifyAncestorScrollMotion(false);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.False(overlay.IsShowingSnapshot);
    }

    [Fact]
    public void AnOverlayAddedDuringMotionFreezesAndOneRemovedDuringMotionIsReset()
    {
        using var clock = new ManualGestureClock();
        var overlay = Overlay();
        var scroll = Column(overlay);
        var stack = (SkUiVerticalStackLayout)scroll.Content!;
        SkUiTestHelpers.Arrange(scroll, 200, 200);

        scroll.Touch(new(1, SkUiTouchAction.Pressed, new Point(100, 180), TimeSpan.Zero));
        scroll.Touch(new(1, SkUiTouchAction.Moved, new Point(100, 120), TimeSpan.FromMilliseconds(100)));
        Assert.True(overlay.IsShowingSnapshot);

        stack.Children.Remove(overlay);
        Assert.False(overlay.IsShowingSnapshot);

        // Added again while the drag goes on: it joins the moving scroller frozen.
        var added = Overlay();
        stack.Children.Insert(1, added);
        Assert.True(added.IsShowingSnapshot);

        // Built off-tree and placed in the scroller as a branch: still registered with it.
        var branch = new SkUiVerticalStackLayout { Children = { new SkUiBox { HeightRequest = 10 } } };
        var nested = Overlay();
        branch.Children.Add(nested);
        stack.Children.Insert(0, branch);
        Assert.True(nested.IsShowingSnapshot);

        scroll.Touch(new(1, SkUiTouchAction.Released, new Point(100, 120), TimeSpan.FromMilliseconds(900)));
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.False(added.IsShowingSnapshot);
        Assert.False(nested.IsShowingSnapshot);
    }

    [Fact]
    public void ContainersThatSwitchContentHideOrDetachTheirNativeViews()
    {
        var inState = Overlay(30, SkUiOverlayScrollMode.Live);
        var layout = new SkUiVerticalStackLayout { Children = { inState } };
        var loading = new SkUiLabel { Text = "Loading" };
        SkUiStateView.SetStateKey(loading, "Loading");
        SkUiStateContainer.GetStateViews(layout).Add(loading);

        var primary = Overlay(30, SkUiOverlayScrollMode.Live);
        var alternate = Overlay(30, SkUiOverlayScrollMode.Live);
        var switcher = new SkUiAlternateContentView { Content = primary, AlternateContent = alternate, ShowsAlternate = false };

        var root = new SkUiContentView { Content = new SkUiVerticalStackLayout { Children = { layout, switcher } } };
        using var surface = new SkUiTestSurface(root, 200, 200);
        surface.Frame(0);
        Assert.False(inState.IsNativeHidden);
        Assert.False(primary.IsNativeHidden);
        Assert.True(Gone(alternate));

        SkUiStateContainer.SetCurrentState(layout, "Loading");
        surface.Frame(16);
        Assert.True(Gone(inState));
        SkUiStateContainer.SetCurrentState(layout, null);
        surface.Frame(32);
        Assert.False(inState.IsNativeHidden);

        switcher.ShowsAlternate = true;
        surface.Frame(48);
        Assert.True(Gone(primary));
        Assert.False(alternate.IsNativeHidden);
        switcher.ShowsAlternate = null;
        surface.Frame(64);
        Assert.True(Gone(alternate));
    }

    [Fact]
    public void AClippingAncestorAndAScrollerClipTheOverlayTogether()
    {
        var overlay = Overlay(80, SkUiOverlayScrollMode.Live);
        var card = new SkUiBorder { HeightRequest = 60, Margin = new Thickness(20, 0), ClipToBounds = true, Content = overlay };
        var stack = new SkUiVerticalStackLayout { Children = { new SkUiBox { HeightRequest = 100 }, card, new SkUiBox { HeightRequest = 1000 } } };
        var scroll = new SkUiScrollView { Content = stack };
        SkUiTestHelpers.Arrange(scroll, 200, 200);
        // The card clips the 80 DIP overlay to its 60 DIP band (inside its stroke).
        var clip = overlay.ComputeRootRelativeClip();
        Assert.InRange(clip.Top, 100, 102);
        Assert.InRange(clip.Bottom, 158, 160);
        Assert.InRange(clip.Left, 20, 22);

        scroll.ScrollTo(0, 130); // the card is 30 DIPs above the viewport
        Assert.Equal(0, Visible(overlay).Top);
        Assert.InRange(Visible(overlay).Bottom, 28, 30);
    }

    /// <summary>Not on screen: removed from the tree, or hidden there.</summary>
    private static bool Gone(SkUiMauiContentView overlay) => overlay.Parent is null || overlay.IsNativeHidden || !overlay.IsShown;

    private static Rect Visible(SkUiMauiContentView overlay)
    {
        var visible = overlay.ComputeRootRelativeFrame().Intersect(overlay.ComputeRootRelativeClip());
        return visible.Width <= 0 || visible.Height <= 0 ? Rect.Zero : visible;
    }
}
