using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>The zoom views: the gestures, and scale and offset staying in range. <see cref="MarkdownSurfaceTests"/> shows that zooming records nothing.</summary>
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
        // A positive delta is the wheel turned away from the reader: in.
        Assert.Equal(new Point(0, 0), handler.OnWheel(0, 100));
        Assert.True(zoom.ZoomScale > 1);
        zoom.ResetZoom();
        Assert.Equal(new Point(0, -100), handler.OnWheel(0, -100));
    }

    [Fact]
    public void WithoutWheelZoomTheWheelPassesOn()
    {
        var (zoom, _) = Core();
        zoom.SetIsWheelZoomEnabled(false);
        Assert.Equal(new Point(3, 100), ((ISkUiWheelHandler)zoom).OnWheel(3, 100));
        Assert.Equal(1, zoom.ZoomScale);
        var drawn = new SkUiZoomView { Content = new SkUiBox(), IsWheelZoomEnabled = false };
        SkUiTestHelpers.Arrange(drawn, 200, 100);
        Assert.Equal(new Point(0, 100), ((ISkUiWheelHandler)drawn).OnWheel(0, 100));
        drawn.IsWheelZoomEnabled = true;
        Assert.Equal(new Point(0, 0), ((ISkUiWheelHandler)drawn).OnWheel(0, 100));
        Assert.True(drawn.ZoomScale > 1);
    }

    private static void Pinch(Action<SkUiTouchEvent> touch, Point middle, double from, double to)
    {
        var time = TimeSpan.Zero;
        touch(new(1, SkUiTouchAction.Pressed, new Point(middle.X - from, middle.Y), time));
        touch(new(2, SkUiTouchAction.Pressed, new Point(middle.X + from, middle.Y), time));
        const int Steps = 10;
        for (var step = 1; step <= Steps; step++)
        {
            time += TimeSpan.FromMilliseconds(16);
            var half = from + (to - from) * step / Steps;
            touch(new(1, SkUiTouchAction.Moved, new Point(middle.X - half, middle.Y), time));
            touch(new(2, SkUiTouchAction.Moved, new Point(middle.X + half, middle.Y), time));
        }
        touch(new(1, SkUiTouchAction.Released, new Point(middle.X - to, middle.Y), time));
        touch(new(2, SkUiTouchAction.Released, new Point(middle.X + to, middle.Y), time));
    }

    [Fact]
    public void APinchKeepsThePointBetweenTheFingers()
    {
        var (zoom, content) = Core();
        Pinch(touch => zoom.Touch(touch), new Point(150, 50), 20, 40);
        Assert.True(zoom.ZoomScale > 1.5, zoom.ZoomScale.ToString(System.Globalization.CultureInfo.InvariantCulture));
        // 50 from the center: moved out by (scale - 1) times that, so it stays under the fingers.
        Assert.Equal(-(zoom.ZoomScale - 1) * 50, content.TranslationX, 0);
        Assert.Equal(0, content.TranslationY, 0);
    }

    private static void PinchWithOneFinger(Action<SkUiTouchEvent> touch)
    {
        var time = TimeSpan.Zero;
        touch(new(1, SkUiTouchAction.Pressed, new Point(100, 50), time));
        touch(new(2, SkUiTouchAction.Pressed, new Point(150, 50), time));
        for (var step = 1; step <= 10; step++)
        {
            time += TimeSpan.FromMilliseconds(16);
            touch(new(1, SkUiTouchAction.Moved, new Point(100 - 4 * step, 50), time));
        }
        touch(new(1, SkUiTouchAction.Released, new Point(60, 50), time));
        touch(new(2, SkUiTouchAction.Released, new Point(150, 50), time));
    }

    [Fact]
    public void ZoomedInAPinchWithOneFingerStillBeatsThePan()
    {
        var (core, _) = Core();
        Pinch(touch => core.Touch(touch), new Point(100, 50), 20, 30);
        var before = core.ZoomScale;
        Assert.True(before > 1.2);
        PinchWithOneFinger(touch => core.Touch(touch));
        Assert.True(core.ZoomScale > before * 1.5, core.ZoomScale.ToString(System.Globalization.CultureInfo.InvariantCulture));

        var drawn = new SkUiZoomView { Content = new SkUiBox() };
        SkUiTestHelpers.Arrange(drawn, 200, 100);
        Pinch(touch => drawn.Touch(touch), new Point(100, 50), 20, 30);
        before = drawn.ZoomScale;
        PinchWithOneFinger(touch => drawn.Touch(touch));
        Assert.True(drawn.ZoomScale > before * 1.5, drawn.ZoomScale.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static void MovingPinch(Action<SkUiTouchEvent> touch, double fromMiddle, double toMiddle, double from, double to)
    {
        var time = TimeSpan.Zero;
        const double Y = 50;
        touch(new(1, SkUiTouchAction.Pressed, new Point(fromMiddle - from, Y), time));
        touch(new(2, SkUiTouchAction.Pressed, new Point(fromMiddle + from, Y), time));
        const int Steps = 10;
        for (var step = 1; step <= Steps; step++)
        {
            time += TimeSpan.FromMilliseconds(16);
            var middle = fromMiddle + (toMiddle - fromMiddle) * step / Steps;
            var half = from + (to - from) * step / Steps;
            touch(new(1, SkUiTouchAction.Moved, new Point(middle - half, Y), time));
            touch(new(2, SkUiTouchAction.Moved, new Point(middle + half, Y), time));
        }
        touch(new(1, SkUiTouchAction.Released, new Point(toMiddle - to, Y), time));
        touch(new(2, SkUiTouchAction.Released, new Point(toMiddle + to, Y), time));
    }

    [Fact]
    public void WhatIsBetweenThePinchingFingersFollowsThem()
    {
        var (core, coreContent) = Core();
        MovingPinch(touch => core.Touch(touch), 100, 130, 20, 40);
        // The pinch starts once the fingers are 12 further apart, around 109: the point there is 9 from the center at scale 1, and ends under the fingers at 130.
        var expected = 30 - core.ZoomScale * 9;
        Assert.Equal(expected, coreContent.TranslationX, 0);

        var drawnContent = new SkUiBox();
        var drawn = new SkUiZoomView { Content = drawnContent };
        SkUiTestHelpers.Arrange(drawn, 200, 100);
        MovingPinch(touch => drawn.Touch(touch), 100, 130, 20, 40);
        Assert.Equal(expected, drawnContent.TranslationX, 0);
    }

    [Fact]
    public void ZoomedInTwoFingersDraggingTogetherMoveTheContent()
    {
        var (core, coreContent) = Core();
        Pinch(touch => core.Touch(touch), new Point(100, 50), 20, 40);
        var scale = core.ZoomScale;
        MovingPinch(touch => core.Touch(touch), 100, 40, 20, 20);
        Assert.Equal(scale, core.ZoomScale, 3);
        // The first 12 only start the gesture.
        Assert.Equal(-48, coreContent.TranslationX, 0);

        var drawnContent = new SkUiBox();
        var drawn = new SkUiZoomView { Content = drawnContent };
        SkUiTestHelpers.Arrange(drawn, 200, 100);
        Pinch(touch => drawn.Touch(touch), new Point(100, 50), 20, 40);
        MovingPinch(touch => drawn.Touch(touch), 100, 40, 20, 20);
        Assert.Equal(-48, drawnContent.TranslationX, 0);
    }

    [Fact]
    public void ThePinchOfTheDrawnViewKeepsThePointToo()
    {
        var content = new SkUiBox();
        var zoom = new SkUiZoomView { Content = content };
        SkUiTestHelpers.Arrange(zoom, 200, 100);
        Pinch(touch => zoom.Touch(touch), new Point(150, 50), 20, 40);
        Assert.True(zoom.ZoomScale > 1.5);
        Assert.Equal(-(zoom.ZoomScale - 1) * 50, content.TranslationX, 0);
    }

    [Fact]
    public void ADoubleTapZoomsInWhenTheSmallestScaleIsAboveOne()
    {
        var (zoom, _) = Core();
        zoom.SetMinimumZoomScale(2);
        zoom.SetDoubleTapZoomScale(4);
        Assert.Equal(2, zoom.ZoomScale);
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
        Assert.Equal(4, zoom.ZoomScale);
        time += TimeSpan.FromSeconds(1);
        Tap();
        Tap();
        Assert.Equal(2, zoom.ZoomScale);
    }

    [Fact]
    public void TheGeometryFollowsWhereTheContentIs()
    {
        var content = new SkUiCoreBox();
        var zoom = new SkUiCoreZoomView();
        zoom.SetPadding(new Thickness(40, 0, 0, 0));
        zoom.SetContent(content);
        zoom.Measure(200, 100);
        zoom.Arrange(new Rect(0, 0, 200, 100));
        Assert.Equal(40, content.Frame.X);
        // The content's center is at x = 120; the point at 150 is 30 from it.
        zoom.ZoomTo(2, new Point(150, 50));
        Assert.Equal(-30, content.TranslationX, 3);
        zoom.ZoomTo(5, new Point(200, 50));
        Assert.True(Math.Abs(content.TranslationX) <= 160 * 4 / 2.0 + 0.001);
    }

    [Fact]
    public void ReplacedContentIsLeftUntransformedAndTheNewOneStartsAtRest()
    {
        var (zoom, content) = Core();
        zoom.ZoomTo(3, new Point(150, 50));
        var next = new SkUiCoreBox();
        zoom.SetContent(next);
        Assert.Equal(1, content.Scale);
        Assert.Equal(0, content.TranslationX);
        Assert.Equal(1, zoom.ZoomScale);
        Assert.Equal(1, next.Scale);

        var drawn = new SkUiBox();
        var view = new SkUiZoomView { Content = drawn };
        SkUiTestHelpers.Arrange(view, 200, 100);
        view.ZoomTo(3, new Point(150, 50));
        view.Content = new SkUiBox();
        Assert.Equal(1, drawn.Scale);
        Assert.Equal(0, drawn.TranslationX);
        Assert.Equal(1, view.ZoomScale);
    }

    [Fact]
    public void ADragAndTheWheelMoveTheDrawnViewToo()
    {
        var content = new SkUiBox();
        var zoom = new SkUiZoomView { Content = content };
        SkUiTestHelpers.Arrange(zoom, 200, 100);
        zoom.ZoomTo(3, new Point(100, 50));
        var time = TimeSpan.Zero;
        zoom.Touch(new(1, SkUiTouchAction.Pressed, new Point(100, 50), time));
        for (var step = 1; step <= 5; step++)
        {
            time += TimeSpan.FromMilliseconds(16);
            zoom.Touch(new(1, SkUiTouchAction.Moved, new Point(100 + 8 * step, 50), time));
        }
        zoom.Touch(new(1, SkUiTouchAction.Released, new Point(140, 50), time));
        Assert.True(content.TranslationX > 10);
        var scale = zoom.ZoomScale;
        ((ISkUiWheelHandler)zoom).OnWheel(0, 100);
        Assert.True(zoom.ZoomScale > scale);
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
