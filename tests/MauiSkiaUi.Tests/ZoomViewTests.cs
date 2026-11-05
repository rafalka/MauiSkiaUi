using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>The zoom views: scale and offset stay in range and move only composite-time properties.</summary>
public class ZoomViewTests
{
    private static (SkUiCoreZoomView Zoom, SkUiCoreBox Content) Core()
    {
        var content = new SkUiCoreBox();
        var zoom = new SkUiCoreZoomView();
        zoom.SetContent(content);
        zoom.Measure(200, 100);
        zoom.Arrange(new Rect(0, 0, 200, 100));
        return (zoom, content);
    }

    [Fact]
    public void ItStartsAtOneAndClips()
    {
        var (zoom, content) = Core();
        Assert.Equal(1, zoom.ZoomScale);
        Assert.Equal(1, content.Scale);
        Assert.True(zoom.ClipToBounds);
    }

    [Fact]
    public void ZoomingKeepsTheFocusWhereItIs()
    {
        var (zoom, content) = Core();
        zoom.ZoomTo(2, new Point(150, 50));
        Assert.Equal(2, content.Scale);
        // The point at x = 150 is 50 from the center; twice as far at scale 2, so the content moves by -50.
        Assert.Equal(-50, content.TranslationX, 3);
        Assert.Equal(0, content.TranslationY, 3);
    }

    [Fact]
    public void TheScaleAndOffsetStayInRange()
    {
        var (zoom, content) = Core();
        zoom.ZoomTo(50, new Point(0, 0));
        Assert.Equal(zoom.MaximumZoomScale, zoom.ZoomScale);
        // The content's edge never comes inside the viewport.
        Assert.True(Math.Abs(content.TranslationX) <= 200 * (zoom.ZoomScale - 1) / 2 + 0.001);
        zoom.ZoomTo(0.01, new Point(0, 0));
        Assert.Equal(1, zoom.ZoomScale);
        Assert.Equal(0, content.TranslationX);
        Assert.Equal(0, content.TranslationY);
    }

    [Fact]
    public void ResetGoesBackAndChangesAreReported()
    {
        var (zoom, content) = Core();
        var changes = 0;
        zoom.ZoomChanged += (_, _) => changes++;
        zoom.ZoomTo(3, new Point(10, 10));
        zoom.ResetZoom();
        Assert.Equal(2, changes);
        Assert.Equal(1, content.Scale);
        Assert.Equal(0, content.TranslationX);
    }

    [Fact]
    public void AResizeBringsTheOffsetBackInRange()
    {
        var (zoom, content) = Core();
        zoom.ZoomTo(2, new Point(200, 50));
        Assert.Equal(-100, content.TranslationX, 3);
        zoom.Measure(100, 100);
        zoom.Arrange(new Rect(0, 0, 100, 100));
        Assert.Equal(-50, content.TranslationX, 3);
    }

    [Fact]
    public void BadScalesThrow()
    {
        var (zoom, _) = Core();
        Assert.Throws<ArgumentOutOfRangeException>(() => zoom.SetMaximumZoomScale(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => zoom.SetMinimumZoomScale(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => zoom.SetDoubleTapZoomScale(-1));
    }

    [Fact]
    public void ADoubleTapZoomsInAndBackOut()
    {
        var (zoom, _) = Core();
        var at = new Point(100, 50);
        var time = TimeSpan.Zero;
        void Tap()
        {
            zoom.Touch(new(1, SkUiTouchAction.Pressed, at, time));
            time += TimeSpan.FromMilliseconds(20);
            zoom.Touch(new(1, SkUiTouchAction.Released, at, time));
            time += TimeSpan.FromMilliseconds(40);
        }
        Tap();
        Tap();
        Assert.Equal(zoom.DoubleTapZoomScale, zoom.ZoomScale);
        time += TimeSpan.FromSeconds(1);
        Tap();
        Tap();
        Assert.Equal(1, zoom.ZoomScale);
    }

    [Fact]
    public void ADragPansOnlyWhenZoomedIn()
    {
        var (zoom, content) = Core();
        void Drag(double dx)
        {
            var time = TimeSpan.Zero;
            zoom.Touch(new(1, SkUiTouchAction.Pressed, new Point(100, 50), time));
            for (var step = 1; step <= 5; step++)
            {
                time += TimeSpan.FromMilliseconds(16);
                zoom.Touch(new(1, SkUiTouchAction.Moved, new Point(100 + dx * step / 5, 50), time));
            }
            zoom.Touch(new(1, SkUiTouchAction.Released, new Point(100 + dx, 50), time));
        }
        Drag(40);
        Assert.Equal(0, content.TranslationX);
        zoom.ZoomTo(3, new Point(100, 50));
        Drag(40);
        Assert.True(content.TranslationX > 10);
    }

    [Fact]
    public void TheWheelZoomsAndLeavesTheRestAtTheLimit()
    {
        var (zoom, _) = Core();
        var handler = (ISkUiWheelHandler)zoom;
        Assert.Equal(new Point(0, 0), handler.OnWheel(0, -100));
        Assert.True(zoom.ZoomScale > 1);
        zoom.ResetZoom();
        Assert.Equal(new Point(0, 100), handler.OnWheel(0, 100));
    }

    [Fact]
    public void TheDrawnViewMovesItsContent()
    {
        var content = new SkUiBox();
        var zoom = new SkUiZoomView { Content = content };
        SkUiTestHelpers.Arrange(zoom, 200, 100);
        zoom.ZoomTo(2, new Point(150, 50));
        Assert.Equal(2, content.Scale);
        Assert.Equal(-50, content.TranslationX, 3);
        zoom.ResetZoom();
        Assert.Equal(1, content.Scale);
        Assert.Equal(0, content.TranslationX);
        Assert.Throws<ArgumentOutOfRangeException>(() => zoom.SetMaximumZoomScale(-1));
    }
}
