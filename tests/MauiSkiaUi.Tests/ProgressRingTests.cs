using MauiSkiaUi.Core;
using Microsoft.Maui.Controls.Shapes;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary><see cref="SkUiProgressRing"/> / <see cref="SkUiCoreProgressRing"/>: the arc, content in the inner square, measuring, the render-thread spin.</summary>
public class ProgressRingTests
{
    private static readonly SKColor Fill = SKColors.Red;
    private static readonly SKColor Track = SKColors.Blue;

    private static SkUiProgressRing Ring() => new()
    {
        ProgressColor = Colors.Red, TrackColor = Colors.Blue, Thickness = 10, LineCap = PenLineCap.Flat, WidthRequest = 100, HeightRequest = 100
    };

    // Mid-stroke points of a 100 DIP ring of thickness 10 (radius 45 around 50, 50) at 45° into each quadrant.
    private static readonly (int X, int Y) TopRight = (82, 18), BottomRight = (82, 82), BottomLeft = (18, 82), TopLeft = (18, 18);

    private static SKColor At(SKBitmap bitmap, (int X, int Y) point) => bitmap.GetPixel(point.X, point.Y);

    [Fact]
    public void ArcStartsAtTheTopAndGrowsClockwiseOrCounterclockwise()
    {
        var ring = Ring();
        ring.Progress = 0.25;
        var root = new SkUiContentView { Content = ring, Background = Colors.White };
        using (var surface = new SkUiTestSurface(root, 100, 100))
        {
            var bitmap = surface.Frame();
            Assert.Equal(Fill, At(bitmap, TopRight));
            Assert.Equal(Track, At(bitmap, BottomRight));
            Assert.Equal(Track, At(bitmap, BottomLeft));
            Assert.Equal(Track, At(bitmap, TopLeft));
            Assert.Equal(SKColors.White, bitmap.GetPixel(50, 50)); // the inside stays clear
        }

        ring.IsClockwise = false;
        ring.Progress = 0.5;
        using (var surface = new SkUiTestSurface(root, 100, 100))
        {
            var bitmap = surface.Frame();
            Assert.Equal(Fill, At(bitmap, TopLeft));
            Assert.Equal(Fill, At(bitmap, BottomLeft));
            Assert.Equal(Track, At(bitmap, TopRight));
        }

        ring.IsClockwise = true;
        ring.StartAngle = 90; // from the bottom
        ring.Progress = 0.25;
        using (var surface = new SkUiTestSurface(root, 100, 100))
        {
            var bitmap = surface.Frame();
            Assert.Equal(Fill, At(bitmap, BottomLeft));
            Assert.Equal(Track, At(bitmap, TopRight));
        }
    }

    [Fact]
    public void ContentSitsInTheSquareInsideTheRing()
    {
        var content = new SkUiBox { Color = Colors.Green };
        var ring = Ring();
        ring.Content = content;
        ring.Padding = new Thickness(2);
        SkUiTestHelpers.Arrange(new SkUiContentView { Content = ring }, 100, 100);

        var side = 80 / Math.Sqrt(2); // the inner circle's diameter is 100 - 2 × 10
        Assert.Equal((100 - side) / 2 + 2, content.Bounds.X, 3);
        Assert.Equal((100 - side) / 2 + 2, content.Bounds.Y, 3);
        Assert.Equal(side - 4, content.Bounds.Width, 3);
        Assert.Equal(side - 4, content.Bounds.Height, 3);
    }

    [Fact]
    public void MeasuresTheLookSizeOrGrowsForItsContent()
    {
        var look = SkUiLook.Current;
        var empty = new SkUiProgressRing();
        var size = empty.Measure(double.PositiveInfinity, double.PositiveInfinity);
        Assert.Equal(look.DefaultProgressRingSize, size.Width, 3);
        Assert.Equal(look.DefaultProgressRingSize, size.Height, 3);

        var ring = new SkUiProgressRing { Thickness = 4, Content = new SkUiBox { WidthRequest = 60, HeightRequest = 20 } };
        size = ring.Measure(double.PositiveInfinity, double.PositiveInfinity);
        Assert.Equal(60 * Math.Sqrt(2) + 8, size.Width, 3);
        Assert.Equal(size.Width, size.Height, 3);

        var core = new SkUiCoreProgressRing().SetThickness(4).SetContent(new SkUiCoreBox().SetWidth(60).SetHeight(20));
        var coreSize = core.Measure(double.PositiveInfinity, double.PositiveInfinity);
        Assert.Equal(size.Width, coreSize.Width, 3);
    }

    [Fact]
    public void IndeterminateTurnsOnTheRenderThreadAndTheContentStaysPut()
    {
        var content = new SkUiBox { Color = Colors.Green };
        var ring = Ring();
        ring.IsIndeterminate = true;
        ring.Content = content;
        var root = new SkUiContentView { Content = ring, Background = Colors.White };
        using var surface = new SkUiTestSurface(root, 100, 100);
        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);
        surface.Frame(0);
        Assert.True(surface.NeedsFrame, "an indeterminate ring keeps frames coming");
        var recorded = surface.RecordedPictures;
        var period = SkUiLook.Current.IndeterminateProgressRingPeriod * 1000;

        SKColor[] Sample(double ms)
        {
            surface.Renderer.Render(canvas, bitmap.Info, TimeSpan.FromMilliseconds(ms));
            return [At(bitmap, TopRight), At(bitmap, BottomRight), At(bitmap, BottomLeft), At(bitmap, TopLeft), bitmap.GetPixel(32, 32)];
        }
        var quarter = Sample(1000 * period + period * 0.0);
        var later = Sample(1000 * period + period * 0.5);
        Assert.NotEqual(quarter[..4], later[..4]);
        Assert.Equal(SKColors.Green, quarter[4]); // the content (near its corner) does not turn
        Assert.Equal(SKColors.Green, later[4]);
        Assert.Equal(recorded, surface.RecordedPictures);

        ring.IsIndeterminate = false;
        surface.Frame(1);
        surface.Frame(2);
        Assert.False(surface.NeedsFrame);
    }

    [Fact]
    public async Task ProgressToAnimates()
    {
        var ring = new SkUiProgressRing();
        var root = new SkUiContentView { Content = ring };
        var task = ring.ProgressTo(1, 100);
        root.AnimationClock.Tick(TimeSpan.FromMilliseconds(50));
        Assert.InRange(ring.Progress, 0.3, 0.7);
        root.AnimationClock.Tick(TimeSpan.FromMilliseconds(100));
        Assert.True(await task);
        Assert.Equal(1, ring.Progress);

        var core = new SkUiCoreProgressRing();
        var host = new SkUiCoreHost().SetContent(core);
        var coreTask = core.ProgressTo(0.5, 100);
        host.AnimationClock.Tick(TimeSpan.FromMilliseconds(150));
        Assert.True(await coreTask);
        Assert.Equal(0.5, core.Progress, 3);
    }

    [Fact]
    public void CoreRingDrawsTheSamePixels()
    {
        var ring = Ring();
        ring.Progress = 0.6;
        using var skui = new SkUiTestSurface(new SkUiContentView { Content = ring, Background = Colors.White }, 100, 100);
        var expected = skui.Frame();

        var core = new SkUiCoreProgressRing().SetProgressColor(Colors.Red).SetTrackColor(Colors.Blue).SetThickness(10).SetLineCap(PenLineCap.Flat)
            .SetProgress(0.6);
        core.SetWidth(100).SetHeight(100);
        using var coreSurface = new SkUiTestSurface(new SkUiCoreHost { Background = Colors.White }.SetContent(core), 100, 100);
        var actual = coreSurface.Frame();
        for (var y = 0; y < 100; y += 3)
        for (var x = 0; x < 100; x += 3)
            Assert.Equal(expected.GetPixel(x, y), actual.GetPixel(x, y));
    }

    [Fact]
    public void ReadAsAProgressBarAndValidatesItsValues()
    {
        var ring = new SkUiProgressRing { Progress = 0.4 };
        SkUiTestHelpers.Arrange(ring, 100, 100);
        var node = SkUiSemanticsTree.Build(ring).Flatten().Single();
        Assert.Equal(SkUiSemanticsRole.ProgressBar, node.Role);
        Assert.Equal(new SkUiSemanticsRange(0, 1, 0.4), node.Range);

        ring.Thickness = -2; // ignored, as MAUI ignores invalid values
        Assert.Equal(-1, ring.Thickness);
        Assert.Throws<ArgumentOutOfRangeException>(() => ring.SetThickness(-2));
        Assert.Throws<ArgumentOutOfRangeException>(() => ring.SetStartAngle(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkUiCoreProgressRing().SetThickness(double.PositiveInfinity));
    }
}
