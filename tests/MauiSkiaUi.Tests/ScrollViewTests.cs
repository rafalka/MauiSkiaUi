using Xunit;
using SkiaSharp;

namespace MauiSkiaUi.Tests;

public class ScrollViewTests
{
    [Theory]
    [InlineData(ScrollOrientation.Vertical, 0, 40)]
    [InlineData(ScrollOrientation.Horizontal, 40, 0)]
    [InlineData(ScrollOrientation.Both, 0, 40)]
    public void WheelScrollsDocumentedAxisPerOrientation(ScrollOrientation orientation, double expectedX, double expectedY)
    {
        var scroll = new SkUiScrollView { Content = new SkUiBox { WidthRequest = 400, HeightRequest = 400 }, Orientation = orientation };
        SkUiTestHelpers.Arrange(scroll, 100, 100);
        scroll.Touch(new(1, SkUiTouchAction.Wheel, new Point(50, 50), WheelDelta: -40));
        Assert.Equal(expectedX, scroll.ScrollX);
        Assert.Equal(expectedY, scroll.ScrollY);
    }

    [Fact]
    public async Task AsyncScrollCompletesOnRenderThreadAndDisablingCancelsMotion()
    {
        var scroll = new SkUiScrollView { Content = new SkUiBox { HeightRequest = 500 } };
        using var surface = new SkUiTestSurface(scroll, 100, 100);
        surface.Frame(0);
        var completed = scroll.ScrollToAsync(0, 200);
        Assert.True(scroll.IsMotionRunning);
        surface.Frame(1000);
        surface.Frame(1400);
        await completed;
        Assert.Equal(200, scroll.ScrollY);
        Assert.False(scroll.IsMotionRunning);
        var cancelled = scroll.ScrollToAsync(0, 300);
        scroll.IsEnabled = false;
        Assert.False(scroll.IsMotionRunning);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
    }

    [Theory]
    [InlineData(double.NaN, 0, "horizontalOffset")]
    [InlineData(0, double.PositiveInfinity, "verticalOffset")]
    [InlineData(double.NegativeInfinity, double.NaN, "horizontalOffset")]
    public void ScrollOffsetValidationNamesTheInvalidArgument(double x, double y, string paramName)
    {
        var scroll = new SkUiScrollView { Content = new SkUiBox { WidthRequest = 400, HeightRequest = 400 } };
        SkUiTestHelpers.Arrange(scroll, 100, 100);
        var sync = Assert.Throws<ArgumentOutOfRangeException>(() => scroll.ScrollTo(x, y));
        Assert.Equal(paramName, sync.ParamName);
        var async = Assert.Throws<ArgumentOutOfRangeException>(() => { _ = scroll.ScrollToAsync(x, y); });
        Assert.Equal(paramName, async.ParamName);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void ScrolledCompositionMatchesFullPixelGolden(int density)
    {
        var grid = new SkUiGrid { RowDefinitions = [new(new GridLength(8)), new(new GridLength(8))] };
        grid.Children.Add(new SkUiBox { Color = Colors.Red });
        var blue = new SkUiBox { Color = Colors.Blue };
        Grid.SetRow(blue, 1);
        grid.Children.Add(blue);
        // Scroll bars would cover most of this 8 DIP viewport.
        var scroll = new SkUiScrollView { Content = grid, VerticalScrollBarVisibility = ScrollBarVisibility.Never };
        SkUiTestHelpers.Arrange(scroll, 8, 8);
        scroll.ScrollTo(0, 4);
        using var bitmap = new SKBitmap(8 * density, 8 * density);
        using var canvas = new SKCanvas(bitmap);
        canvas.Scale(density);
        scroll.Paint(canvas);
        for (var row = 0; row < bitmap.Height; row++)
            for (var column = 0; column < bitmap.Width; column++)
                Assert.Equal(row < 4 * density ? SKColors.Red : SKColors.Blue, bitmap.GetPixel(column, row));
    }

    [Fact]
    public void ContentSpinRunsOnRenderThreadWithoutReRecording()
    {
        var indicator = new SkUiActivityIndicator { IsRunning = true, WidthRequest = 36, HeightRequest = 36 };
        var scroll = new SkUiScrollView { Content = indicator };
        using var surface = new SkUiTestSurface(scroll, 40, 40);
        surface.Frame(0);
        var recorded = surface.RecordedPictures;
        Assert.True(surface.NeedsFrame);

        surface.Frame(250);
        surface.Frame(500);
        Assert.Equal(recorded, surface.RecordedPictures);
        Assert.True(surface.NeedsFrame);

        indicator.IsRunning = false;
        surface.Frame(750);
        Assert.False(surface.NeedsFrame);
    }

    [Fact]
    public void OffsetOnlyScrollNeverReRecordsAndContentChangeReRecordsOnlyThatNode()
    {
        var grid = new SkUiGrid { RowDefinitions = [new(new GridLength(8)), new(new GridLength(8))] };
        var red = new SkUiBox { Color = Colors.Red };
        var blue = new SkUiBox { Color = Colors.Blue };
        Grid.SetRow(blue, 1);
        grid.Children.Add(red);
        grid.Children.Add(blue);
        var scroll = new SkUiScrollView { Content = grid, VerticalScrollBarVisibility = ScrollBarVisibility.Never };
        using var surface = new SkUiTestSurface(scroll, 8, 8);

        var bitmap = surface.Frame();
        var recorded = surface.RecordedPictures;
        Assert.Equal(SKColors.Red, bitmap.GetPixel(4, 2));

        scroll.ScrollTo(0, 4);
        bitmap = surface.Frame();
        Assert.Equal(recorded, surface.RecordedPictures);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(4, 2));
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(4, 6));

        red.Color = Colors.Lime;
        scroll.ScrollTo(0, 0);
        bitmap = surface.Frame();
        Assert.Equal(recorded + 1, surface.RecordedPictures);
        Assert.Equal(SKColors.Lime, bitmap.GetPixel(4, 2));
    }

    [Fact]
    public void ScrollFillsFullViewportWhenExtentNarrowerThanViewport()
    {
        // Measure with a narrow constraint (extent stays narrow), then arrange into a wider viewport.
        // Arrange expands content via max(extent, viewport).
        var grid = new SkUiGrid
        {
            ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)],
            RowDefinitions = [new(new GridLength(40))],
        };
        var left = new SkUiBox { Color = Colors.Red };
        var right = new SkUiBox { Color = Colors.Blue };
        Grid.SetColumn(right, 1);
        grid.Children.Add(left);
        grid.Children.Add(right);
        var scroll = new SkUiScrollView { Content = grid };
        ((IView)scroll).Measure(40, 40);
        ((IView)scroll).Arrange(new Rect(0, 0, 100, 40));
        Assert.True(scroll.ContentSize.Width <= 40);

        using var bitmap = new SKBitmap(100, 40);
        using var canvas = new SKCanvas(bitmap);
        scroll.Paint(canvas);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(20, 20));
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(80, 20));
    }

    [Fact]
    public void LongOffsetScrollReplaysRetainedNodesAndCullsOffscreen()
    {
        var column = new SkUiVerticalStackLayout();
        for (var i = 0; i < 8; i++)
            column.Children.Add(new SkUiBox
            {
                Color = i % 2 == 0 ? Colors.Red : Colors.Blue,
                HeightRequest = 50,
                WidthRequest = 40,
            });
        var scroll = new SkUiScrollView { Content = column };
        using var surface = new SkUiTestSurface(scroll, 40, 50);
        surface.Frame();
        var recorded = surface.RecordedPictures;

        scroll.ScrollTo(0, 40);
        surface.Frame();
        scroll.ScrollTo(0, 200);
        var bitmap = surface.Frame();
        Assert.Equal(recorded, surface.RecordedPictures);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(20, 25));
    }

    [Fact]
    public void ScrollTapReRecordsOnlyTheButton()
    {
        var button = new SkUiButton { Text = "Go", WidthRequest = 80, HeightRequest = 40 };
        var clicks = 0;
        button.Clicked += (_, _) => clicks++;
        var scroll = new SkUiScrollView { Content = button };
        using var surface = new SkUiTestSurface(scroll, 100, 80);
        surface.Frame();
        var recorded = surface.RecordedPictures;

        Assert.True(scroll.Touch(new(1, SkUiTouchAction.Pressed, new Point(20, 20), TimeSpan.Zero)));
        Assert.True(scroll.Touch(new(1, SkUiTouchAction.Released, new Point(20, 20), TimeSpan.FromMilliseconds(20))));
        surface.Frame();
        Assert.Equal(1, clicks);
        Assert.InRange(surface.RecordedPictures - recorded, 1, 2);
    }

    [Fact]
    public void ScrollTapCommandMutationReRecordsContent()
    {
        var button = new SkUiButton { Text = "Go", WidthRequest = 80, HeightRequest = 40 };
        button.Clicked += (_, _) => button.Text = "Done";
        var scroll = new SkUiScrollView { Content = button };
        using var surface = new SkUiTestSurface(scroll, 100, 80);
        surface.Frame();
        var recorded = surface.RecordedPictures;

        Assert.True(scroll.Touch(new(1, SkUiTouchAction.Pressed, new Point(20, 20), TimeSpan.Zero)));
        Assert.True(scroll.Touch(new(1, SkUiTouchAction.Released, new Point(20, 20), TimeSpan.FromMilliseconds(20))));
        surface.Frame();
        Assert.Equal("Done", button.Text);
        Assert.True(surface.RecordedPictures > recorded);
    }

    [Fact]
    public void AnimateScrollToRunsOnRenderThreadAndReportsOffsets()
    {
        var box = new SkUiBox { Color = Colors.Red, HeightRequest = 200, WidthRequest = 40 };
        var scroll = new SkUiScrollView { Content = box };
        using var surface = new SkUiTestSurface(scroll, 40, 50);
        surface.Frame(0);
        var scrolled = 0;
        scroll.Scrolled += (_, _) => scrolled++;

        scroll.AnimateScrollTo(0, 100, TimeSpan.FromMilliseconds(100));
        surface.Frame(10);
        surface.Frame(60);
        Assert.InRange(scroll.ScrollY, 1, 99);
        surface.Frame(200);
        Assert.Equal(100, scroll.ScrollY);
        Assert.False(scroll.IsMotionRunning);
        Assert.True(scrolled >= 2);
        box.Color = Colors.Blue;
        Assert.Equal(SKColors.Blue, surface.Frame(210).GetPixel(20, 25));
    }

    [Fact]
    public void PanDoesNotPressChildOrReRecordContent()
    {
        var button = new SkUiButton { Text = "Go", WidthRequest = 80, HeightRequest = 200 };
        var scroll = new SkUiScrollView { Content = button };
        using var surface = new SkUiTestSurface(scroll, 100, 80);
        surface.Frame();
        var recorded = surface.RecordedPictures;

        Assert.True(scroll.Touch(new(1, SkUiTouchAction.Pressed, new Point(20, 20), TimeSpan.Zero)));
        surface.Frame();
        Assert.True(scroll.Touch(new(1, SkUiTouchAction.Moved, new Point(20, -40), TimeSpan.FromMilliseconds(50))));
        surface.Frame();
        Assert.True(scroll.Touch(new(1, SkUiTouchAction.Released, new Point(20, -40), TimeSpan.FromMilliseconds(200))));
        surface.Frame();
        Assert.False(button.IsPressed);
        Assert.Equal(recorded, surface.RecordedPictures);
    }

    [Fact]
    public void ScrollClampsOffsetsAndDoesNotRemeasureContent()
    {
        var child = new MeasureProbe { WidthRequest = 400, HeightRequest = 600 };
        var scroll = new SkUiScrollView { Content = child, Orientation = ScrollOrientation.Both };
        SkUiTestHelpers.Arrange(scroll, 100, 150);
        var count = child.Measures;
        scroll.ScrollTo(900, 900);
        Assert.Equal(300, scroll.ScrollX);
        Assert.Equal(450, scroll.ScrollY);
        // Offset is applied in paint/touch only; arranged frame stays at the content origin.
        Assert.Equal(new Rect(0, 0, 400, 600), child.Frame);
        Assert.Equal(count, child.Measures);
        scroll.ScrollTo(-1, -1);
        Assert.Equal(0, scroll.ScrollY);
        child.HeightRequest = 100;
        SkUiTestHelpers.Arrange(scroll, 100, 150);
        Assert.Equal(0, scroll.ScrollY);
    }

    [Fact]
    public void ScrollPanCancelsButtonAndFlingRunsOnRenderThread()
    {
        var button = new SkUiButton { HeightRequest = 500 };
        var scroll = new SkUiScrollView { Content = button };
        using var surface = new SkUiTestSurface(scroll, 150, 100);
        surface.Frame(0);
        var clicks = 0;
        button.Clicked += (_, _) => clicks++;
        scroll.Touch(new(1, SkUiTouchAction.Pressed, new Point(30, 80), TimeSpan.Zero));
        Assert.False(button.IsPressed);
        scroll.Touch(new(1, SkUiTouchAction.Released, new Point(30, 80), TimeSpan.FromMilliseconds(20)));
        Assert.Equal(1, clicks);
        Assert.False(button.IsPressed);
        scroll.Touch(new(2, SkUiTouchAction.Pressed, new Point(30, 80), TimeSpan.FromMilliseconds(100)));
        Assert.False(button.IsPressed);
        scroll.Touch(new(2, SkUiTouchAction.Moved, new Point(30, 20), TimeSpan.FromMilliseconds(150)));
        Assert.False(button.IsPressed);
        Assert.Equal(60, scroll.ScrollY);
        scroll.Touch(new(2, SkUiTouchAction.Released, new Point(30, 20), TimeSpan.FromMilliseconds(160)));
        Assert.True(scroll.IsMotionRunning);
        surface.Frame(1000);
        surface.Frame(1200);
        Assert.True(scroll.ScrollY > 60);
        surface.Frame(9000);
        Assert.False(scroll.IsMotionRunning);
        // The scroll bar fades out on the render thread after the fling.
        surface.Frame(9010);
        surface.Frame(10000);
        var bar = ((ISkUiScrollHost)scroll).Scroller.ScrollBars.Vertical!;
        Assert.Equal(0, bar.Opacity);
        surface.Frame(10010);
        Assert.False(surface.NeedsFrame);
        Assert.Equal(1, clicks);
    }

    [Fact]
    public void TouchDuringFlingStopsItWithoutJumpingBack()
    {
        var scroll = new SkUiScrollView { Content = new SkUiBox { HeightRequest = 5000 } };
        using var surface = new SkUiTestSurface(scroll, 100, 100);
        surface.Frame(0);
        scroll.Touch(new(1, SkUiTouchAction.Pressed, new Point(50, 90), TimeSpan.Zero));
        scroll.Touch(new(1, SkUiTouchAction.Moved, new Point(50, 60), TimeSpan.FromMilliseconds(10)));
        scroll.Touch(new(1, SkUiTouchAction.Moved, new Point(50, 30), TimeSpan.FromMilliseconds(20)));
        scroll.Touch(new(1, SkUiTouchAction.Released, new Point(50, 30), TimeSpan.FromMilliseconds(25)));
        surface.Frame(100);
        surface.Frame(300);
        var during = scroll.ScrollY;
        Assert.True(during > 60);
        scroll.Touch(new(2, SkUiTouchAction.Pressed, new Point(50, 50), TimeSpan.FromMilliseconds(400)));
        Assert.False(scroll.IsMotionRunning);
        surface.Frame(400);
        surface.Frame(600);
        Assert.Equal(during, scroll.ScrollY, 3);
    }

    private sealed class MeasureProbe : SkUiBox
    {
        public int Measures { get; private set; }
        protected override Size MeasureContent(double widthConstraint, double heightConstraint)
        {
            Measures++;
            return base.MeasureContent(widthConstraint, heightConstraint);
        }
    }
}
