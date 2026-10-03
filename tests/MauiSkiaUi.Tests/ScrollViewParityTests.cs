using MauiSkiaUi.Core;
using MauiSkiaUi.Rendering;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// P8 ScrollView parity: look-drawn scroll bars (visibility, fade, RTL side, render-thread placement), overscroll (bounce,
/// stretch) with nested chaining kept, direction-aware flings with live extents, axis-aware wheel input, and MAUI's
/// <c>ScrollToAsync(Element, ScrollToPosition, bool)</c> / <c>ScrollToRequested</c> on both layers.
/// </summary>
public class ScrollViewParityTests
{
    private static SkUiScrollController Scroller(object view) => ((ISkUiScrollHost)view).Scroller;

    /// <summary>The thumb's frame in the scroller's coordinates (bar frame plus the thumb's place in it), before scrolling.</summary>
    private static Rect ThumbFrame(SkUiCoreScrollBar bar) =>
        new(bar.Frame.X + bar.ThumbNode.Frame.X, bar.Frame.Y + bar.ThumbNode.Frame.Y, bar.ThumbNode.Frame.Width, bar.ThumbNode.Frame.Height);

    private static SkUiRenderProps Props(object node)
    {
        var props = SkUiRenderProps.Default;
        ((ISkUiRenderable)node).GetRenderProps(ref props);
        return props;
    }

    private static SkUiScrollView Tall(double height = 400, SkUiOverscrollMode overscroll = SkUiOverscrollMode.None) =>
        new() { Content = new SkUiBox { Color = Colors.White, HeightRequest = height }, Overscroll = overscroll };

    private static void Drag(SkUiView root, long id, Point from, Point to, int startMs, bool release = true, int steps = 4)
    {
        root.Touch(new(id, SkUiTouchAction.Pressed, from, TimeSpan.FromMilliseconds(startMs)));
        for (var step = 1; step <= steps; step++)
        {
            var point = new Point(from.X + (to.X - from.X) * step / steps, from.Y + (to.Y - from.Y) * step / steps);
            root.Touch(new(id, SkUiTouchAction.Moved, point, TimeSpan.FromMilliseconds(startMs + step * 100)));
        }
        if (release)
            root.Touch(new(id, SkUiTouchAction.Released, to, TimeSpan.FromMilliseconds(startMs + steps * 100 + 300)));
    }

    #region Content size

    [Fact]
    public void AScrollViewWithoutASizeFollowsItsContentUntilAConstraintStopsIt()
    {
        // Content that fits: the scroller is as tall as the content (nothing to scroll), and grows with it.
        var content = new SkUiBox { HeightRequest = 100 };
        var scroll = new SkUiScrollView { Content = content };
        var column = new SkUiVerticalStackLayout();
        column.Children.Add(scroll);
        column.Children.Add(new SkUiBox { HeightRequest = 20 });
        SkUiTestHelpers.Arrange(column, 300, double.PositiveInfinity);
        Assert.Equal(100, scroll.Frame.Height);
        Assert.Equal(0, Scroller(scroll).MaxY);
        content.HeightRequest = 300;
        SkUiTestHelpers.Arrange(column, 300, double.PositiveInfinity);
        Assert.Equal(300, scroll.Frame.Height);

        // MaximumHeightRequest: it grows up to it, then scrolls.
        scroll.MaximumHeightRequest = 200;
        SkUiTestHelpers.Arrange(column, 300, double.PositiveInfinity);
        Assert.Equal(200, scroll.Frame.Height);
        Assert.Equal(100, Scroller(scroll).MaxY);
        content.HeightRequest = 150;
        SkUiTestHelpers.Arrange(column, 300, double.PositiveInfinity);
        Assert.Equal(150, scroll.Frame.Height);
        Assert.Equal(0, Scroller(scroll).MaxY);

        // A parent's constraint: below a 50 DIP header, a star row of a 250 DIP grid leaves 200; aligned to the start, the
        // scroller is as tall as its content up to that, then scrolls.
        var grown = new SkUiBox { HeightRequest = 100 };
        var limited = new SkUiScrollView { Content = grown, VerticalOptions = LayoutOptions.Start };
        var grid = new SkUiGrid { RowDefinitions = [new(new GridLength(50)), new(GridLength.Star)] };
        grid.Children.Add(limited);
        Grid.SetRow(limited, 1);
        SkUiTestHelpers.Arrange(grid, 300, 250);
        Assert.Equal(new Rect(0, 50, 300, 100), limited.Frame);
        grown.HeightRequest = 400;
        SkUiTestHelpers.Arrange(grid, 300, 250);
        Assert.Equal(new Rect(0, 50, 300, 200), limited.Frame);
        Assert.Equal(200, Scroller(limited).MaxY);

        // An Auto row does not constrain its cells (MAUI's grid measures them with infinite height): the scroller takes its
        // whole content there, as MAUI's ScrollView does.
        var auto = new SkUiScrollView { Content = new SkUiBox { HeightRequest = 400 } };
        var autoGrid = new SkUiGrid { RowDefinitions = [new(GridLength.Auto), new(GridLength.Star)] };
        autoGrid.Children.Add(auto);
        SkUiTestHelpers.Arrange(autoGrid, 300, 250);
        Assert.Equal(400, auto.Frame.Height);

        // Horizontal scrollers follow their content's width the same way.
        var wide = new SkUiBox { WidthRequest = 120, HeightRequest = 40 };
        var horizontal = new SkUiScrollView { Content = wide, Orientation = ScrollOrientation.Horizontal, MaximumWidthRequest = 200 };
        var row = new SkUiHorizontalStackLayout();
        row.Children.Add(horizontal);
        SkUiTestHelpers.Arrange(row, double.PositiveInfinity, 100);
        Assert.Equal(120, horizontal.Frame.Width);
        wide.WidthRequest = 500;
        SkUiTestHelpers.Arrange(row, double.PositiveInfinity, 100);
        Assert.Equal(200, horizontal.Frame.Width);
        Assert.Equal(300, Scroller(horizontal).MaxX);
    }

    [Fact]
    public void ACoreScrollViewWithoutASizeFollowsItsContentUntilItsMaximum()
    {
        var content = new SkUiCoreBox().SetHeight(100);
        var scroll = new SkUiCoreScrollView();
        scroll.SetContent(content);
        scroll.SetMaximumHeight(200);
        var column = new SkUiCoreVerticalStackLayout().Add(scroll).Add(new SkUiCoreBox().SetHeight(20));
        var host = new SkUiCoreHost().SetContent(column);
        SkUiTestHelpers.Arrange(host, 300, 600);
        Assert.Equal(100, scroll.Frame.Height);
        content.SetHeight(300);
        SkUiTestHelpers.Arrange(host, 300, 600);
        Assert.Equal(200, scroll.Frame.Height);
        Assert.Equal(100, Scroller(scroll).MaxY);
    }

    [Fact]
    public void ExplicitlySizedContentKeepsItsSizeAcrossTheScrollAxisAndIsClipped()
    {
        // As MAUI's ScrollView: a 400 DIP wide content of a 300 DIP vertical scroller stays 400 wide (clipped, not scrolled).
        var content = new SkUiBox { Color = Colors.White, WidthRequest = 400, HeightRequest = 640 };
        var scroll = new SkUiScrollView { Content = content, Padding = new Thickness(4, 0) };
        Assert.Equal(new Size(300, 200), ((IView)scroll).Measure(300, 200));
        using var surface = new SkUiTestSurface(scroll, 300, 200);
        Assert.Equal(new Rect(4, 0, 400, 640), content.Frame);
        Assert.Equal(new Size(408, 640), scroll.ContentSize);
        Assert.Equal(0, Scroller(scroll).MaxX);
        scroll.Touch(new(1, SkUiTouchAction.Wheel, new Point(50, 50), WheelDeltaX: -50));
        Assert.Equal(0, scroll.ScrollX);
        var bitmap = surface.Frame(0);
        Assert.Equal(SKColors.White, bitmap.GetPixel(299, 100));

        // Within the viewport, or without an explicit size, the content fills it as before.
        content.WidthRequest = 200;
        SkUiTestHelpers.Arrange(scroll, 300, 200);
        Assert.Equal(200, content.Frame.Width);
        content.WidthRequest = -1;
        SkUiTestHelpers.Arrange(scroll, 300, 200);
        Assert.Equal(292, content.Frame.Width);

        // A horizontal scroller keeps an explicit height the same way; Core scrollers too.
        var row = new SkUiBox { WidthRequest = 600, HeightRequest = 300 };
        var horizontal = new SkUiScrollView { Content = row, Orientation = ScrollOrientation.Horizontal };
        SkUiTestHelpers.Arrange(horizontal, 300, 200);
        Assert.Equal(new Size(600, 300), row.Frame.Size);
        Assert.Equal(0, Scroller(horizontal).MaxY);

        var coreContent = new SkUiCoreBox().SetWidth(400).SetHeight(640);
        var core = new SkUiCoreScrollView();
        core.SetContent(coreContent);
        SkUiTestHelpers.Arrange(new SkUiCoreHost().SetContent(core), 300, 200);
        Assert.Equal(new Size(400, 640), coreContent.Frame.Size);
        Assert.Equal(0, Scroller(core).MaxX);
    }

    #endregion

    #region Scroll bars

    [Fact]
    public void DefaultScrollBarShowsWhileScrollingAndFadesOutOnTheRenderThreadWithoutRecording()
    {
        var scroll = Tall();
        using var surface = new SkUiTestSurface(scroll, 100, 100);
        surface.Frame(0);
        var bar = Scroller(scroll).ScrollBars.Vertical!;
        Assert.Equal(0, bar.Opacity);
        Assert.Equal(SKColors.White, surface.Frame(5).GetPixel(96, 10));
        var recorded = surface.RecordedPictures;

        // Track: 2 DIPs from the edges (96 long); thumb 96 · 100 / 400 = 24 long, 72 DIPs of travel over 300 of offset.
        Assert.Equal(new Rect(94, 2, 4, 24), ThumbFrame(bar));
        // The bar is the 16 DIP hover strip along the edge.
        Assert.Equal(new Rect(84, 2, 16, 96), bar.Frame);
        scroll.ScrollTo(0, 150);
        var bitmap = surface.Frame(10);
        Assert.Equal(1, bar.Opacity);
        Assert.NotEqual(SKColors.White, bitmap.GetPixel(96, 50)); // 2 + 150 · 0.24 = 38 … 62
        Assert.Equal(SKColors.White, bitmap.GetPixel(96, 20));
        Assert.Equal(recorded, surface.RecordedPictures);

        // Visible for the look's delay (500 ms), then gone after its fade (250 ms).
        Assert.NotEqual(SKColors.White, surface.Frame(400).GetPixel(96, 50));
        var fading = surface.Frame(700).GetPixel(96, 50);
        Assert.NotEqual(SKColors.White, fading);
        Assert.Equal(SKColors.White, surface.Frame(800).GetPixel(96, 50));
        surface.Frame(810);
        Assert.Equal(0, bar.Opacity);
        Assert.False(surface.NeedsFrame);
        Assert.Equal(recorded, surface.RecordedPictures);
    }

    [Fact]
    public void ScrollBarThumbFollowsARenderThreadFlingWithoutReRecording()
    {
        var scroll = Tall(4000);
        scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Always;
        using var surface = new SkUiTestSurface(scroll, 100, 100);
        surface.Frame(0);
        var recorded = surface.RecordedPictures;
        Assert.True(Scroller(scroll).StartFling(new Point(0, 3000)));
        surface.Frame(10);
        // The bar always shows, in its reserved gutter (nothing else is drawn there).
        var thumbAt = (SKBitmap bitmap) => Enumerable.Range(0, 100).First(y => bitmap.GetPixel(96, y).Alpha != 0);
        var first = thumbAt(surface.Frame(20));
        var later = thumbAt(surface.Frame(300));
        Assert.True(later > first);
        // The render thread draws the thumb where the UI side places it for the reported offset.
        var props = Props(Scroller(scroll).ScrollBars.Vertical!.ThumbNode);
        Assert.InRange(later, 2 + props.TranslationY - 1, 2 + props.TranslationY + 1);
        Assert.Equal(recorded, surface.RecordedPictures);
    }

    [Fact]
    public void ScrollBarVisibilityAlwaysNeverAndContentThatFits()
    {
        var always = Tall();
        always.VerticalScrollBarVisibility = ScrollBarVisibility.Always;
        using (var surface = new SkUiTestSurface(always, 100, 100))
            Assert.NotEqual(SKColors.White, surface.Frame(0).GetPixel(96, 10));
        Assert.Equal(1, Scroller(always).ScrollBars.Vertical!.Opacity);

        always.VerticalScrollBarVisibility = ScrollBarVisibility.Never;
        Assert.False(Scroller(always).ScrollBars.Vertical!.IsVisible);

        var never = Tall();
        never.VerticalScrollBarVisibility = ScrollBarVisibility.Never;
        SkUiTestHelpers.Arrange(never, 100, 100);
        Assert.Null(Scroller(never).ScrollBars.Vertical);

        var fits = Tall(50);
        fits.VerticalScrollBarVisibility = ScrollBarVisibility.Always;
        SkUiTestHelpers.Arrange(fits, 100, 100);
        Assert.Null(Scroller(fits).ScrollBars.Vertical);

        // Vertical-only scrollers have no horizontal bar.
        Assert.Null(Scroller(always).ScrollBars.Horizontal);
    }

    [Fact]
    public void BothBarsLeaveTheCornerFreeAndTheVerticalBarIsOnTheLeftInRightToLeft()
    {
        var content = new SkUiBox { WidthRequest = 400, HeightRequest = 400 };
        var scroll = new SkUiScrollView { Content = content, Orientation = ScrollOrientation.Both };
        SkUiTestHelpers.Arrange(scroll, 100, 100);
        var (vertical, horizontal) = Scroller(scroll).ScrollBars;
        // Tracks 90 long; 90 · 100 / 400 = 22.5 is below the look's 24 DIP minimum thumb.
        Assert.Equal(new Rect(94, 2, 4, 24), ThumbFrame(vertical!));
        Assert.Equal(new Rect(2, 94, 24, 4), ThumbFrame(horizontal!));

        var rtl = new SkUiScrollView { Content = new SkUiBox { WidthRequest = 400, HeightRequest = 400 }, Orientation = ScrollOrientation.Both, FlowDirection = FlowDirection.RightToLeft };
        SkUiTestHelpers.Arrange(rtl, 100, 100);
        (vertical, horizontal) = Scroller(rtl).ScrollBars;
        Assert.Equal(2, ThumbFrame(vertical!).X);
        Assert.Equal(8, ThumbFrame(horizontal!).X);
        // RTL horizontal scrollers start at the right end: so does the thumb.
        Assert.Equal(300, rtl.ScrollX);
        Assert.Equal(90 - 24, Props(horizontal.ThumbNode).TranslationX, 3);
    }

    [Fact]
    public void FadingBarsDrawOverTheContentAndBarsThatAlwaysShowReserveAGutter()
    {
        var content = new SkUiBox { Color = Colors.White, HeightRequest = 400 };
        var scroll = new SkUiScrollView { Content = content };
        using var surface = new SkUiTestSurface(scroll, 100, 100);
        surface.Frame(0);
        Assert.Equal(100, content.Frame.Width);

        // Always: the content is laid out 12 DIPs narrower and clipped there; the bar sits in the gutter.
        scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Always;
        SkUiTestHelpers.Arrange(scroll, 100, 100);
        Assert.Equal(88, content.Frame.Width);
        Assert.Equal(new Size(88, 100), Scroller(scroll).Viewport);
        Assert.Equal(new Rect(88, 2, 12, 96), scroll.VerticalScrollBar.Frame);
        Assert.Equal(new Rect(94, 2, 4, 24), ThumbFrame(scroll.VerticalScrollBar));
        var bitmap = surface.Frame(10);
        Assert.Equal(SKColors.White, bitmap.GetPixel(80, 50));
        Assert.Equal(0, bitmap.GetPixel(90, 50).Alpha);
        Assert.NotEqual(0, bitmap.GetPixel(96, 10).Alpha);

        // Hovering the gutter expands the bar inside it.
        scroll.Touch(new(0, SkUiTouchAction.HoverMoved, new Point(95, 10)));
        Assert.Equal(new Rect(90, 2, 8, 24), ThumbFrame(scroll.VerticalScrollBar));

        // A touch drag (no hover) that starts in the gutter still scrolls.
        scroll.Touch(new(0, SkUiTouchAction.HoverExited, Point.Zero));
        Drag(scroll, 1, new Point(95, 80), new Point(95, 40), startMs: 0);
        Assert.Equal(40, scroll.ScrollY);

        // Reserved even when the content fits (the bar hides), so the layout never flips with its own result.
        content.HeightRequest = 50;
        SkUiTestHelpers.Arrange(scroll, 100, 100);
        Assert.Equal(88, content.Frame.Width);
        Assert.False(scroll.VerticalScrollBar.IsVisible);

        scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Default;
        SkUiTestHelpers.Arrange(scroll, 100, 100);
        Assert.Equal(100, content.Frame.Width);
    }

    [Fact]
    public void GuttersOfBothBarsAndRightToLeftOnBothLayers()
    {
        var content = new SkUiBox { WidthRequest = 400, HeightRequest = 400 };
        var scroll = new SkUiScrollView
        {
            Content = content, Orientation = ScrollOrientation.Both,
            VerticalScrollBarVisibility = ScrollBarVisibility.Always, HorizontalScrollBarVisibility = ScrollBarVisibility.Always
        };
        SkUiTestHelpers.Arrange(scroll, 100, 100);
        Assert.Equal(new Size(88, 88), Scroller(scroll).Viewport);
        Assert.Equal((312d, 312d), (Scroller(scroll).MaxX, Scroller(scroll).MaxY));
        Assert.Equal(new Rect(88, 2, 12, 84), scroll.VerticalScrollBar.Frame);
        Assert.Equal(new Rect(2, 88, 84, 12), scroll.HorizontalScrollBar.Frame);

        // Right to left: the vertical gutter is on the left, and the content right of it.
        var rtlContent = new SkUiBox { Color = Colors.White, HeightRequest = 400 };
        var rtl = new SkUiScrollView { Content = rtlContent, FlowDirection = FlowDirection.RightToLeft, VerticalScrollBarVisibility = ScrollBarVisibility.Always };
        using var surface = new SkUiTestSurface(rtl, 100, 100);
        var bitmap = surface.Frame(0);
        Assert.Equal(new Rect(12, 0, 88, 400), rtlContent.Frame);
        Assert.Equal(new Rect(0, 2, 12, 96), rtl.VerticalScrollBar.Frame);
        Assert.Equal(new Rect(2, 2, 4, 24), ThumbFrame(rtl.VerticalScrollBar));
        Assert.Equal(SKColors.White, bitmap.GetPixel(20, 50));
        Assert.Equal(0, bitmap.GetPixel(9, 50).Alpha);

        // Core: the same gutter.
        var coreContent = new SkUiCoreBox().SetHeight(400);
        var core = new SkUiCoreScrollView().SetVerticalScrollBarVisibility(ScrollBarVisibility.Always);
        core.SetContent(coreContent);
        SkUiTestHelpers.Arrange(new SkUiCoreHost().SetContent(core), 100, 100);
        Assert.Equal(88, coreContent.Frame.Width);
        Assert.Equal(ThumbFrame(scroll.VerticalScrollBar) with { Height = 24 }, ThumbFrame(core.VerticalScrollBar) with { Height = 24 });
    }

    [Fact]
    public void CoreScrollViewDrawsTheSameScrollBars()
    {
        var skui = new SkUiScrollView { Content = new SkUiBox { HeightRequest = 400 }, VerticalScrollBarVisibility = ScrollBarVisibility.Always };
        SkUiTestHelpers.Arrange(skui, 100, 100);
        var core = new SkUiCoreScrollView().SetVerticalScrollBarVisibility(ScrollBarVisibility.Always);
        core.SetContent(new SkUiCoreBox().SetHeight(400));
        var host = new SkUiCoreHost().SetContent(core);
        using var surface = new SkUiTestSurface(host, 100, 100);
        var bitmap = surface.Frame(0);
        Assert.Equal(ThumbFrame(Scroller(skui).ScrollBars.Vertical!), ThumbFrame(Scroller(core).ScrollBars.Vertical!));
        Assert.NotEqual(0, bitmap.GetPixel(96, 10).Alpha);
        core.ScrollTo(0, 300);
        skui.ScrollTo(0, 300);
        Assert.Equal(Props(Scroller(skui).ScrollBars.Vertical!.ThumbNode).TranslationY, Props(Scroller(core).ScrollBars.Vertical!.ThumbNode).TranslationY);
        Assert.Equal(72, Props(Scroller(core).ScrollBars.Vertical!.ThumbNode).TranslationY, 3);
    }

    [Fact]
    public void TapsReachTheContentUnderAFadingScrollBar()
    {
        var clicks = 0;
        var button = new SkUiButton { Text = "Go", HeightRequest = 400 };
        button.Clicked += (_, _) => clicks++;
        var scroll = new SkUiScrollView { Content = button };
        SkUiTestHelpers.Arrange(scroll, 100, 100);
        scroll.ScrollTo(0, 10);
        Assert.Equal(1, scroll.VerticalScrollBar.Opacity);
        scroll.Touch(new(1, SkUiTouchAction.Pressed, new Point(96, 10), TimeSpan.Zero));
        scroll.Touch(new(1, SkUiTouchAction.Released, new Point(96, 10), TimeSpan.FromMilliseconds(20)));
        Assert.Equal(1, clicks);
    }

    [Fact]
    public void AHoveringPointerExpandsTheBarAndDragsTheThumb()
    {
        var scroll = Tall();
        using var surface = new SkUiTestSurface(scroll, 100, 100);
        surface.Frame(0);
        var bar = scroll.VerticalScrollBar;
        Assert.False(bar.IsExpanded);

        // Hovering the 16 DIP strip along the edge shows and widens the bar (8 DIPs) with its track.
        scroll.Touch(new(0, SkUiTouchAction.HoverMoved, new Point(95, 10)));
        Assert.True(bar.IsExpanded);
        Assert.Equal(1, bar.Opacity);
        Assert.Equal(new Rect(90, 2, 8, 24), ThumbFrame(bar));
        var bitmap = surface.Frame(10);
        Assert.NotEqual(SKColors.White, bitmap.GetPixel(94, 60)); // the track below the thumb

        // Dragging the thumb 36 DIPs moves the content 36 · 300 / 72 = 150.
        scroll.Touch(new(1, SkUiTouchAction.Pressed, new Point(95, 10), TimeSpan.FromMilliseconds(100)));
        scroll.Touch(new(1, SkUiTouchAction.Moved, new Point(95, 30), TimeSpan.FromMilliseconds(150)));
        scroll.Touch(new(1, SkUiTouchAction.Moved, new Point(80, 46), TimeSpan.FromMilliseconds(200)));
        Assert.True(bar.IsDragging);
        Assert.True(scroll.IsScrolling);
        Assert.Equal(150, scroll.ScrollY);
        scroll.Touch(new(1, SkUiTouchAction.Released, new Point(80, 46), TimeSpan.FromMilliseconds(250)));
        Assert.False(bar.IsDragging);
        Assert.Equal(150, scroll.ScrollY);
        Assert.False(scroll.IsMotionRunning);

        // Leaving the strip narrows the bar again.
        scroll.Touch(new(0, SkUiTouchAction.HoverMoved, new Point(40, 10)));
        Assert.False(bar.IsExpanded);
        Assert.Equal(new Rect(94, 2, 4, 24), ThumbFrame(bar));
    }

    [Fact]
    public void APressOnTheTrackPagesByAViewport()
    {
        var scroll = Tall();
        using var surface = new SkUiTestSurface(scroll, 100, 100);
        surface.Frame(0);
        scroll.Touch(new(0, SkUiTouchAction.HoverMoved, new Point(95, 80)));
        scroll.Touch(new(1, SkUiTouchAction.Pressed, new Point(95, 80), TimeSpan.Zero));
        scroll.Touch(new(1, SkUiTouchAction.Released, new Point(95, 80), TimeSpan.FromMilliseconds(20)));
        surface.Frame(10);
        surface.Frame(500);
        Assert.Equal(100, scroll.ScrollY);
    }

    [Fact]
    public void TouchesOnTheBarStripScrollTheContentAndNonInteractiveBarsIgnoreHover()
    {
        var scroll = Tall();
        SkUiTestHelpers.Arrange(scroll, 100, 100);
        Drag(scroll, 1, new Point(95, 80), new Point(95, 40), startMs: 0);
        Assert.Equal(40, scroll.ScrollY);

        scroll.VerticalScrollBar.IsInteractive = false;
        scroll.Touch(new(0, SkUiTouchAction.HoverMoved, new Point(95, 10)));
        Assert.False(scroll.VerticalScrollBar.IsExpanded);
    }

    [Fact]
    public void ABarPlacedByTheAppFollowsItsScroller()
    {
        var scroll = Tall();
        scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Never;
        var bar = new SkUiCoreScrollBar(scroll, ScrollOrientation.Vertical).SetVisibility(ScrollBarVisibility.Always).SetThumbColor(Colors.Red);
        var grid = new SkUiGrid { ColumnDefinitions = [new(GridLength.Star), new(new GridLength(16))] };
        grid.Children.Add(scroll);
        var host = new SkUiCoreHost().SetContent(bar);
        Grid.SetColumn(host, 1);
        grid.Children.Add(host);
        using var surface = new SkUiTestSurface(grid, 116, 100);
        var bitmap = surface.Frame(0);

        // The bar fills its column; its thumb (25 DIPs: 100 · 100 / 400) is centered across it.
        Assert.Equal(new Rect(0, 0, 16, 100), bar.Frame);
        Assert.Equal(new Rect(6, 0, 4, 25), bar.ThumbNode.Frame);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(108, 10));
        Assert.Null(Scroller(scroll).ScrollBars.Vertical is { IsVisible: true } ? scroll : null);

        scroll.ScrollTo(0, 150);
        Assert.Equal(37.5, Props(bar.ThumbNode).TranslationY, 3);
        bitmap = surface.Frame(10);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(108, 50));
        Assert.NotEqual(SKColors.Red, bitmap.GetPixel(108, 10));

        Assert.Throws<InvalidOperationException>(() => scroll.VerticalScrollBar.Visibility = ScrollBarVisibility.Always);
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkUiCoreScrollBar(scroll, ScrollOrientation.Both));
    }

    #endregion

    #region Snap points

    private static SkUiScrollView Pager(int pages = 5, double width = 100)
    {
        var row = new SkUiHorizontalStackLayout();
        for (var index = 0; index < pages; index++)
            row.Children.Add(new SkUiBox { WidthRequest = width, HeightRequest = 100 });
        return new SkUiScrollView { Content = row, Orientation = ScrollOrientation.Horizontal, SnapPointsType = SnapPointsType.MandatorySingle };
    }

    [Fact]
    public void MandatorySingleMovesOnePagePerSwipeAndSettlesBackOnASlowDrag()
    {
        var scroll = Pager();
        using var surface = new SkUiTestSurface(scroll, 100, 100);
        surface.Frame(0);

        // A slow 30 DIP drag settles back on the page it started from.
        Drag(scroll, 1, new Point(80, 50), new Point(50, 50), startMs: 0);
        Assert.Equal(30, scroll.ScrollX);
        Assert.True(scroll.IsMotionRunning);
        surface.Frame(10);
        surface.Frame(1000);
        Assert.Equal(0, scroll.ScrollX);

        // A fast swipe moves exactly one page, however hard.
        scroll.Touch(new(2, SkUiTouchAction.Pressed, new Point(90, 50), TimeSpan.FromMilliseconds(2000)));
        scroll.Touch(new(2, SkUiTouchAction.Moved, new Point(60, 50), TimeSpan.FromMilliseconds(2010)));
        scroll.Touch(new(2, SkUiTouchAction.Moved, new Point(20, 50), TimeSpan.FromMilliseconds(2020)));
        scroll.Touch(new(2, SkUiTouchAction.Released, new Point(20, 50), TimeSpan.FromMilliseconds(2025)));
        for (var ms = 2030; ms < 3500; ms += 16)
        {
            surface.Frame(ms);
            Assert.InRange(scroll.ScrollX, 0, 100);
        }
        Assert.Equal(100, scroll.ScrollX);
        Assert.False(scroll.IsMotionRunning);
    }

    [Fact]
    public void MandatorySnapsNearWhereTheFlingWouldStop()
    {
        var scroll = Pager(pages: 20);
        scroll.SnapPointsType = SnapPointsType.Mandatory;
        using var surface = new SkUiTestSurface(scroll, 100, 100);
        surface.Frame(0);
        Assert.True(Scroller(scroll).StartFling(new Point(1000, 0)));
        surface.Frame(10);
        surface.Frame(3000);
        // A 1000 DIP/s fling travels about 500 DIPs: the page at 500.
        Assert.Equal(500, scroll.ScrollX);
    }

    [Fact]
    public void SnapOffsetsFollowTheAlignmentAndStayInRange()
    {
        var scroll = Pager(pages: 4, width: 80);
        scroll.SnapPointsAlignment = SnapPointsAlignment.Center;
        SkUiTestHelpers.Arrange(scroll, 100, 100);
        // Centers 40, 120, 200, 280 minus half the viewport, clamped to [0, 220].
        Assert.Equal([0d, 70, 150, 220], Scroller(scroll).SnapOffsets(horizontal: true));
        scroll.SnapPointsAlignment = SnapPointsAlignment.End;
        Assert.Equal([0d, 60, 140, 220], Scroller(scroll).SnapOffsets(horizontal: true));

        var core = new SkUiCoreScrollView().SetOrientation(ScrollOrientation.Horizontal).SetSnapPointsType(SnapPointsType.Mandatory);
        var row = new SkUiCoreHorizontalStackLayout();
        for (var index = 0; index < 4; index++)
            row.Add(new SkUiCoreBox().SetWidth(80));
        core.SetContent(row);
        SkUiTestHelpers.Arrange(new SkUiCoreHost().SetContent(core), 100, 100);
        Assert.Equal([0d, 80, 160, 220], Scroller(core).SnapOffsets(horizontal: true));
    }

    [Fact]
    public void WheelScrollingSettlesOnASnapPointOnceItPauses()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var scroll = Pager();
        scroll.SnapPointsType = SnapPointsType.Mandatory;
        using var surface = new SkUiTestSurface(scroll, 100, 100);
        surface.Frame(0);
        scroll.Touch(new(1, SkUiTouchAction.Wheel, new Point(50, 50), WheelDeltaX: -40));
        scroll.Touch(new(2, SkUiTouchAction.Wheel, new Point(50, 50), WheelDeltaX: -30));
        Assert.Equal(70, scroll.ScrollX);
        TestDispatcherProvider.RunDelayed();
        surface.Frame(10);
        surface.Frame(1000);
        Assert.Equal(100, scroll.ScrollX);
    }

    #endregion

    #region Overscroll

    [Fact]
    public void BounceFollowsADragPastTheTopWithResistanceAndSpringsBack()
    {
        var scroll = Tall(overscroll: SkUiOverscrollMode.Bounce);
        using var surface = new SkUiTestSurface(scroll, 100, 100);
        surface.Frame(0);
        var scrolled = 0;
        scroll.Scrolled += (_, _) => scrolled++;

        Drag(scroll, 1, new Point(50, 20), new Point(50, 80), startMs: 0, release: false);
        var overscroll = Scroller(scroll).OverscrollY;
        // A 60 DIP pull shows (1 − 1 / (60 · 0.55 / 100 + 1)) · 100 ≈ 24.8 DIPs.
        Assert.Equal(-24.8, overscroll, 1);
        Assert.Equal(0, scroll.ScrollY);
        Assert.Equal(0, scrolled);
        var bitmap = surface.Frame(500);
        Assert.Equal(0, bitmap.GetPixel(50, 10).Alpha);
        Assert.Equal(SKColors.White, bitmap.GetPixel(50, 30));
        // The compositor and the immediate painter draw (and hit-test) the same bounce.
        using (var painted = new SKBitmap(100, 100))
        using (var canvas = new SKCanvas(painted))
        {
            scroll.Paint(canvas);
            Assert.Equal(0, painted.GetPixel(50, 10).Alpha);
            Assert.Equal(SKColors.White, painted.GetPixel(50, 30));
        }

        // Dragging back takes the pull back before scrolling.
        scroll.Touch(new(1, SkUiTouchAction.Moved, new Point(50, 50), TimeSpan.FromMilliseconds(600)));
        Assert.Equal(0, scroll.ScrollY);
        Assert.InRange(Scroller(scroll).OverscrollY, overscroll + 1, -1);

        scroll.Touch(new(1, SkUiTouchAction.Released, new Point(50, 50), TimeSpan.FromMilliseconds(900)));
        Assert.True(scroll.IsMotionRunning);
        surface.Frame(1000);
        surface.Frame(1100);
        Assert.InRange(Scroller(scroll).OverscrollY, -14, -0.25);
        surface.Frame(2000);
        Assert.Equal(0, Scroller(scroll).OverscrollY);
        Assert.False(scroll.IsMotionRunning);
        Assert.Equal(SKColors.White, surface.Frame(2010).GetPixel(50, 10));
        Assert.Equal(0, scrolled);
    }

    [Fact]
    public void StretchScalesTheContentAwayFromThePulledEdge()
    {
        var scroll = Tall(overscroll: SkUiOverscrollMode.Stretch);
        SkUiTestHelpers.Arrange(scroll, 100, 100);
        Drag(scroll, 1, new Point(50, 20), new Point(50, 80), startMs: 0, release: false);
        var props = Props(scroll);
        Assert.Equal(0, props.ChildrenOffsetY);
        Assert.Equal(1 + SkUiOverscroll.StretchFactor * 24.8f / 100, props.ChildrenScaleY, 3);
        Assert.Equal(0, props.ChildrenScaleOrigin.Y);
        scroll.Touch(new(1, SkUiTouchAction.Released, new Point(50, 80), TimeSpan.FromMilliseconds(900)));

        scroll.ScrollTo(0, 300);
        Drag(scroll, 2, new Point(50, 80), new Point(50, 20), startMs: 2000, release: false);
        props = Props(scroll);
        Assert.Equal(300, props.ChildrenOffsetY);
        Assert.True(props.ChildrenScaleY > 1);
        Assert.Equal(100, props.ChildrenScaleOrigin.Y);
    }

    [Fact]
    public void WithoutOverscrollADragPastTheEdgeIsNotTaken()
    {
        var scroll = Tall();
        SkUiTestHelpers.Arrange(scroll, 100, 100);
        Drag(scroll, 1, new Point(50, 20), new Point(50, 80), startMs: 0, release: false);
        Assert.Equal(0, Scroller(scroll).OverscrollY);
        Assert.False(Scroller(scroll).Dragging);
        Assert.Equal(1, Props(scroll).ChildrenScaleY);
    }

    [Fact]
    public void OverscrollKeepsNestedChainingAndOnlyTheDraggedScrollerOverscrolls()
    {
        var inner = new SkUiScrollView { HeightRequest = 100, Content = new SkUiBox { HeightRequest = 200 }, Overscroll = SkUiOverscrollMode.Bounce };
        var column = new SkUiVerticalStackLayout();
        column.Children.Add(new SkUiBox { HeightRequest = 100 });
        column.Children.Add(inner);
        column.Children.Add(new SkUiBox { HeightRequest = 1000 });
        var outer = new SkUiScrollView { Content = column, Overscroll = SkUiOverscrollMode.Bounce };
        SkUiTestHelpers.Arrange(outer, 300, 400);
        outer.ScrollTo(0, 50);

        // Inner at its top, outer can still scroll up: the outer scroller takes the drag, nothing overscrolls.
        Drag(outer, 1, new Point(150, 60), new Point(150, 90), startMs: 0);
        Assert.Equal(20, outer.ScrollY);
        Assert.Equal(0, Scroller(inner).OverscrollY);
        Assert.Equal(0, Scroller(outer).OverscrollY);

        // Inner scrolled: its drag scrolls it to the top, chains into the outer scroller, then pulls the inner one.
        inner.ScrollTo(0, 50);
        Drag(outer, 2, new Point(150, 100), new Point(150, 200), startMs: 2000, release: false);
        Assert.Equal(0, inner.ScrollY);
        Assert.Equal(0, outer.ScrollY);
        Assert.True(Scroller(inner).OverscrollY < 0);
        Assert.Equal(0, Scroller(outer).OverscrollY);
    }

    [Fact]
    public void ANativeAncestorThatCanScrollKeepsTheDragAtTheEdge()
    {
        var scroll = Tall(overscroll: SkUiOverscrollMode.Bounce);
        SkUiTestHelpers.Arrange(scroll, 100, 100);
        var asked = new List<(double, double)>();
        scroll.Router.NativeAncestorCanScroll = (dx, dy) => { asked.Add((dx, dy)); return true; };
        Drag(scroll, 1, new Point(50, 20), new Point(50, 80), startMs: 0, release: false);
        Assert.Equal(0, Scroller(scroll).OverscrollY);
        Assert.Contains(asked, delta => delta.Item2 < 0);
        Assert.Equal(SkUiNativeGestureState.None, scroll.Router.NativeState);
        scroll.Touch(new(1, SkUiTouchAction.Released, new Point(50, 80), TimeSpan.FromMilliseconds(900)));

        scroll.Router.NativeAncestorCanScroll = (_, _) => false;
        Drag(scroll, 2, new Point(50, 20), new Point(50, 80), startMs: 2000, release: false);
        Assert.True(Scroller(scroll).OverscrollY < 0);
    }

    [Fact]
    public void AFlingIntoTheEdgeBouncesPastItAndSettles()
    {
        var scroll = Tall(overscroll: SkUiOverscrollMode.Bounce);
        using var surface = new SkUiTestSurface(scroll, 100, 100);
        surface.Frame(0);
        scroll.ScrollTo(0, 250);
        Assert.True(Scroller(scroll).StartFling(new Point(0, 2000)));
        var peak = 0.0;
        for (var ms = 10; ms <= 600; ms += 16)
        {
            surface.Frame(ms);
            peak = Math.Max(peak, Scroller(scroll).OverscrollY);
            Assert.InRange(scroll.ScrollY, 250, 300);
        }
        // Limited to 15 % of the viewport.
        Assert.InRange(peak, 1, 15.01);
        surface.Frame(3000);
        Assert.Equal(300, scroll.ScrollY);
        Assert.Equal(0, Scroller(scroll).OverscrollY);
        Assert.False(scroll.IsMotionRunning);
    }

    #endregion

    #region Fling (N11)

    [Fact]
    public void AFlingAwayFromAnEdgeRunsAndAFlingIntoItStopsAtOnce()
    {
        var scroll = Tall();
        using var surface = new SkUiTestSurface(scroll, 100, 100);
        surface.Frame(0);
        Assert.True(Scroller(scroll).StartFling(new Point(0, 1000)));
        surface.Frame(10);
        surface.Frame(100);
        Assert.True(scroll.ScrollY > 50);
        surface.Frame(5000);

        scroll.ScrollTo(0, 0);
        Assert.True(Scroller(scroll).StartFling(new Point(0, -1000)));
        surface.Frame(5010);
        Assert.False(scroll.IsMotionRunning);
        Assert.Equal(0, scroll.ScrollY);
    }

    [Fact]
    public void AFlingFollowsTheExtentWhenContentGrows()
    {
        var box = new SkUiBox { HeightRequest = 400 };
        var scroll = new SkUiScrollView { Content = box };
        using var surface = new SkUiTestSurface(scroll, 100, 100);
        surface.Frame(0);
        Assert.True(Scroller(scroll).StartFling(new Point(0, 3000)));
        surface.Frame(10);
        surface.Frame(110);
        Assert.InRange(scroll.ScrollY, 100, 299);
        box.HeightRequest = 3000;
        SkUiTestHelpers.Arrange(scroll, 100, 100);
        surface.Frame(400);
        Assert.True(scroll.ScrollY > 300);
        Assert.True(scroll.IsMotionRunning);
    }

    #endregion

    #region Wheel

    [Fact]
    public void HorizontalWheelScrollsBothAndChainsEachAxisToTheScrollerThatCanUseIt()
    {
        var both = new SkUiScrollView { Content = new SkUiBox { WidthRequest = 400, HeightRequest = 400 }, Orientation = ScrollOrientation.Both };
        SkUiTestHelpers.Arrange(both, 100, 100);
        both.Touch(new(1, SkUiTouchAction.Wheel, new Point(50, 50), WheelDelta: -30, WheelDeltaX: -40));
        Assert.Equal((40d, 30d), (both.ScrollX, both.ScrollY));

        // A vertical list inside a horizontal pager: a diagonal trackpad scroll moves each along its own axis.
        var inner = new SkUiScrollView { WidthRequest = 100, Content = new SkUiBox { HeightRequest = 400 } };
        var row = new SkUiHorizontalStackLayout();
        row.Children.Add(inner);
        row.Children.Add(new SkUiBox { WidthRequest = 400, HeightRequest = 100 });
        var outer = new SkUiScrollView { Content = row, Orientation = ScrollOrientation.Horizontal };
        SkUiTestHelpers.Arrange(outer, 200, 100);
        Assert.True(outer.Touch(new(2, SkUiTouchAction.Wheel, new Point(50, 50), WheelDelta: -30, WheelDeltaX: -40)));
        Assert.Equal(30, inner.ScrollY);
        Assert.Equal(40, outer.ScrollX);

        // A plain mouse wheel still scrolls a horizontal-only scroller.
        outer.Touch(new(3, SkUiTouchAction.Wheel, new Point(150, 50), WheelDelta: -20));
        Assert.Equal(60, outer.ScrollX);
    }

    #endregion

    #region Scroll to element

    private static (SkUiScrollView Scroll, SkUiBox[] Items) List(int count = 10)
    {
        var stack = new SkUiVerticalStackLayout();
        var items = new SkUiBox[count];
        for (var index = 0; index < count; index++)
            stack.Children.Add(items[index] = new SkUiBox { HeightRequest = 50 });
        return (new SkUiScrollView { Content = stack }, items);
    }

    [Theory]
    [InlineData(ScrollToPosition.Start, 200)]
    [InlineData(ScrollToPosition.Center, 175)]
    [InlineData(ScrollToPosition.End, 150)]
    [InlineData(ScrollToPosition.MakeVisible, 150)]
    public async Task ScrollToElementLandsAtEachPosition(ScrollToPosition position, double expected)
    {
        var (scroll, items) = List();
        SkUiTestHelpers.Arrange(scroll, 100, 100);
        await scroll.ScrollToAsync(items[4], position, animated: false);
        Assert.Equal(expected, scroll.ScrollY);
        Assert.Equal(new Point(0, expected), scroll.GetScrollPositionForElement(items[4], position));
    }

    [Fact]
    public async Task MakeVisibleKeepsTheOffsetForAVisibleElementAndAlignsTheNearerEdgeOtherwise()
    {
        var (scroll, items) = List();
        SkUiTestHelpers.Arrange(scroll, 100, 100);
        scroll.ScrollTo(0, 120);
        await scroll.ScrollToAsync(items[3], ScrollToPosition.MakeVisible, false);
        Assert.Equal(120, scroll.ScrollY);
        await scroll.ScrollToAsync(items[1], ScrollToPosition.MakeVisible, false);
        Assert.Equal(50, scroll.ScrollY);
        // Past the end: clamped.
        await scroll.ScrollToAsync(items[9], ScrollToPosition.Start, false);
        Assert.Equal(400, scroll.ScrollY);
    }

    [Fact]
    public async Task ScrollToRaisesMauisScrollToRequestedAndAnimatesOnTheRenderThread()
    {
        var (scroll, items) = List();
        var requests = new List<ScrollToRequestedEventArgs>();
        scroll.ScrollToRequested += (_, args) => requests.Add(args);
        using var surface = new SkUiTestSurface(scroll, 100, 100);
        surface.Frame(0);

        var task = scroll.ScrollToAsync(items[6], ScrollToPosition.Start, animated: true);
        Assert.True(scroll.IsMotionRunning);
        surface.Frame(10);
        surface.Frame(500);
        await task;
        Assert.Equal(300, scroll.ScrollY);
        await scroll.ScrollToAsync(10, 20, false);

        Assert.Equal(2, requests.Count);
        Assert.Equal((ScrollToMode.Element, (Element)items[6], ScrollToPosition.Start, true),
            (requests[0].Mode, requests[0].Element, requests[0].Position, requests[0].ShouldAnimate));
        Assert.Equal((ScrollToMode.Position, 10d, 20d, false),
            (requests[1].Mode, requests[1].ScrollX, requests[1].ScrollY, requests[1].ShouldAnimate));
    }

    [Fact]
    public async Task ScrollToElementBeforeTheFirstLayoutWaitsForIt()
    {
        var (scroll, items) = List();
        var task = scroll.ScrollToAsync(items[5], ScrollToPosition.Start, false);
        Assert.False(task.IsCompleted);
        SkUiTestHelpers.Arrange(scroll, 100, 100);
        await task;
        Assert.Equal(250, scroll.ScrollY);

        // Replacing the content drops a request still waiting.
        var (pending, pendingItems) = List();
        var dropped = pending.ScrollToAsync(pendingItems[5], ScrollToPosition.Start, false);
        pending.Content = new SkUiBox();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dropped);
    }

    [Fact]
    public async Task ScrollToElementValidatesLikeMaui()
    {
        var (scroll, _) = List();
        SkUiTestHelpers.Arrange(scroll, 100, 100);
        Assert.Throws<ArgumentException>(() => { _ = scroll.ScrollToAsync(new SkUiBox(), ScrollToPosition.Start, false); });
        Assert.Throws<ArgumentException>(() => { _ = scroll.ScrollToAsync((Element)scroll.Content!, (ScrollToPosition)42, false); });
        // A scroller that cannot scroll ignores the request (MAUI checks this first).
        scroll.Orientation = ScrollOrientation.Neither;
        await scroll.ScrollToAsync(new SkUiBox(), ScrollToPosition.Start, false);
    }

    [Fact]
    public async Task ScrollToFindsCoreNodesAndNestedScrollersOnBothLayers()
    {
        // A Core node under a host inside a SkUi* scroll view.
        var nodes = new SkUiCoreNode[10];
        var stack = new SkUiCoreVerticalStackLayout();
        for (var index = 0; index < nodes.Length; index++)
            stack.Add(nodes[index] = new SkUiCoreBox().SetHeight(50));
        var column = new SkUiVerticalStackLayout();
        column.Children.Add(new SkUiBox { HeightRequest = 100 });
        column.Children.Add(new SkUiCoreHost().SetContent(stack));
        var scroll = new SkUiScrollView { Content = column };
        SkUiTestHelpers.Arrange(scroll, 100, 100);
        await scroll.ScrollToAsync(nodes[2], ScrollToPosition.Start, false);
        Assert.Equal(200, scroll.ScrollY);

        // The Core scroll view.
        var coreStack = new SkUiCoreVerticalStackLayout();
        var coreNodes = new SkUiCoreNode[10];
        for (var index = 0; index < coreNodes.Length; index++)
            coreStack.Add(coreNodes[index] = new SkUiCoreBox().SetHeight(50));
        var core = new SkUiCoreScrollView();
        core.SetContent(coreStack);
        var host = new SkUiCoreHost().SetContent(core);
        SkUiTestHelpers.Arrange(host, 100, 100);
        await core.ScrollToAsync(coreNodes[7], ScrollToPosition.End, false);
        Assert.Equal(300, core.ScrollY);
        Assert.Throws<ArgumentException>(() => { _ = core.ScrollToAsync(new SkUiCoreBox(), ScrollToPosition.Start, false); });

        // An element inside a nested scroller: its offset counts.
        var inner = List(4);
        inner.Scroll.HeightRequest = 100;
        var outerColumn = new SkUiVerticalStackLayout();
        outerColumn.Children.Add(new SkUiBox { HeightRequest = 300 });
        outerColumn.Children.Add(inner.Scroll);
        outerColumn.Children.Add(new SkUiBox { HeightRequest = 300 });
        var outer = new SkUiScrollView { Content = outerColumn };
        SkUiTestHelpers.Arrange(outer, 100, 100);
        inner.Scroll.ScrollTo(0, 50);
        await outer.ScrollToAsync(inner.Items[2], ScrollToPosition.Start, false);
        Assert.Equal(350, outer.ScrollY);
    }

    #endregion
}

/// <summary>MAUI's ScrollView doc markup with the prefix changed loads and behaves as documented.</summary>
[Collection(RuntimeXamlCollection.Name)]
public class ScrollViewXamlParityTests
{
    [Fact]
    public async Task MauiDocSampleLoadsBindsAndScrollsToAnElement()
    {
        const string xaml = """
            <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                         xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <sk:SkUiScrollView x:Name="scrollView" Orientation="Both"
                                 HorizontalScrollBarVisibility="Always" VerticalScrollBarVisibility="{Binding Bars}">
                <sk:SkUiVerticalStackLayout>
                  <sk:SkUiBox Color="Red" HeightRequest="600" WidthRequest="150" HorizontalOptions="Center" />
                  <sk:SkUiLabel x:Name="finalLabel" Text="Final" HeightRequest="40" WidthRequest="400" />
                </sk:SkUiVerticalStackLayout>
              </sk:SkUiScrollView>
            </ContentView>
            """;
        var root = new ContentView { BindingContext = new BarsModel { Bars = ScrollBarVisibility.Never } };
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var scroll = (SkUiScrollView)root.Content;
        Assert.Equal(ScrollOrientation.Both, scroll.Orientation);
        Assert.Equal(ScrollBarVisibility.Always, scroll.HorizontalScrollBarVisibility);
        Assert.Equal(ScrollBarVisibility.Never, scroll.VerticalScrollBarVisibility);

        SkUiTestHelpers.Arrange(scroll, 200, 200);
        var label = root.FindByName<SkUiLabel>("finalLabel");
        var scrolled = new List<ScrolledEventArgs>();
        scroll.Scrolled += (_, args) => scrolled.Add(args);
        await scroll.ScrollToAsync(label, ScrollToPosition.End, false);
        // The horizontal bar always shows: it reserves 12 DIPs below the content (viewport 200 × 188).
        Assert.Equal((200d, 452d), (scroll.ScrollX, scroll.ScrollY));
        Assert.Equal((200d, 452d), (scrolled[^1].ScrollX, scrolled[^1].ScrollY));
        Assert.Equal(new Size(400, 640), scroll.ContentSize);
    }

    public sealed class BarsModel
    {
        public ScrollBarVisibility Bars { get; set; }
    }
}

/// <summary>The look draws the scroll bars and picks the default overscroll (process-wide look).</summary>
[Collection(GlobalStateCollection.Name)]
public class ScrollViewLookTests
{
    [Fact]
    public void TheLookDrawsTheThumbsAndPicksTheDefaultOverscroll()
    {
        var previous = SkUiLook.Current;
        var drawn = new List<SkUiScrollBarPaint>();
        try
        {
            SkUiLook.Current = new DefaultSkUiLook { ScrollBarPainter = (_, bar) => drawn.Add(bar), Overscroll = SkUiOverscrollMode.Stretch };
            var scroll = new SkUiScrollView
            {
                Content = new SkUiBox { WidthRequest = 400, HeightRequest = 400 },
                Orientation = ScrollOrientation.Both,
                VerticalScrollBarVisibility = ScrollBarVisibility.Always,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Always
            };
            using var surface = new SkUiTestSurface(scroll, 100, 100);
            surface.Frame(0);
            Assert.Contains(drawn, bar => bar.Orientation == ScrollOrientation.Vertical && bar.Bounds == new SKRect(0, 0, 4, 24));
            Assert.Contains(drawn, bar => bar.Orientation == ScrollOrientation.Horizontal && bar.Bounds == new SKRect(0, 0, 24, 4));
            Assert.Equal(SkUiOverscrollMode.Stretch, ((ISkUiScrollHost)scroll).Scroller.EffectiveOverscroll);
            scroll.Overscroll = SkUiOverscrollMode.None;
            Assert.Equal(SkUiOverscrollMode.None, ((ISkUiScrollHost)scroll).Scroller.EffectiveOverscroll);

            // Scrolling draws nothing again.
            var count = drawn.Count;
            scroll.ScrollTo(100, 100);
            surface.Frame(10);
            Assert.Equal(count, drawn.Count);
        }
        finally
        {
            SkUiLook.Current = previous;
        }
    }
}
