using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary><see cref="SkUiProgressBar"/> / <see cref="SkUiCoreProgressBar"/>: fill, direction, indeterminate render-thread slide, ProgressTo.</summary>
public class ProgressBarTests
{
    private static readonly SKColor Fill = SKColors.Red;
    private static readonly SKColor Track = SKColors.Blue;

    private static SkUiProgressBar Bar() => new() { ProgressColor = Colors.Red, TrackColor = Colors.Blue, HeightRequest = 8 };

    [Fact]
    public void DeterminateFillsFromTheStartInEitherDirection()
    {
        var bar = Bar();
        bar.Progress = 0.5;
        var root = new SkUiContentView { Content = bar, Background = Colors.White };
        using (var surface = new SkUiTestSurface(root, 200, 8))
        {
            var bitmap = surface.Frame();
            Assert.Equal(Fill, bitmap.GetPixel(50, 4));
            Assert.Equal(Track, bitmap.GetPixel(150, 4));
        }

        bar.Progress = 3; // clamped
        Assert.Equal(1, bar.Progress);
        bar.Progress = 0.25;
        root.FlowDirection = FlowDirection.RightToLeft;
        using var rtl = new SkUiTestSurface(root, 200, 8);
        var mirrored = rtl.Frame();
        Assert.Equal(Fill, mirrored.GetPixel(180, 4));
        Assert.Equal(Track, mirrored.GetPixel(20, 4));
    }

    [Fact]
    public void IndeterminateSegmentMovesOnTheRenderThread()
    {
        var bar = Bar();
        bar.IsIndeterminate = true;
        var root = new SkUiContentView { Content = bar, Background = Colors.White };
        using var surface = new SkUiTestSurface(root, 200, 8);
        using var bitmap = new SKBitmap(200, 8);
        using var canvas = new SKCanvas(bitmap);
        surface.Frame(0);
        Assert.True(surface.NeedsFrame, "an indeterminate bar keeps frames coming");

        // Render thread only (no UI pump, no re-record): the segment's position follows time.
        int Segment(double ms)
        {
            surface.Renderer.Render(canvas, bitmap.Info, TimeSpan.FromMilliseconds(ms));
            var start = -1;
            for (var x = 0; x < 200; x++)
                if (bitmap.GetPixel(x, 4) == Fill && start < 0)
                    start = x;
            return start;
        }
        var recorded = surface.RecordedPictures;
        var period = SkUiLook.Current.IndeterminateProgressPeriod * 1000;
        var early = Segment(10_000 * period + period * 0.1);
        var later = Segment(10_000 * period + period * 0.4);
        Assert.True(later > early, $"segment start {early} → {later}");
        Assert.Equal(recorded, surface.RecordedPictures);
        // The tiled copies meet without a seam: no background shows through anywhere along the bar.
        Assert.DoesNotContain(Enumerable.Range(2, 196).Select(x => bitmap.GetPixel(x, 4)), color => color == SKColors.White);

        bar.IsIndeterminate = false;
        surface.Frame(1);
        surface.Frame(2);
        Assert.False(surface.NeedsFrame);
    }

    [Fact]
    public async Task ProgressToAnimatesAndReportsReplacement()
    {
        var bar = Bar();
        var clock = bar.AnimationClock;
        var first = bar.ProgressTo(1, 100);
        clock.Tick(TimeSpan.FromMilliseconds(50));
        Assert.InRange(bar.Progress, 0.3, 0.7);
        var second = bar.ProgressTo(0.2, 100);
        Assert.False(await first);
        clock.Tick(TimeSpan.FromMilliseconds(200));
        Assert.True(await second);
        Assert.Equal(0.2, bar.Progress, 3);
    }

    [Fact]
    public async Task ProgressToCompletesWhenStoppedAndContinuesOnAnotherSurface()
    {
        var bar = Bar();
        bar.Progress = double.NaN;
        Assert.Equal(0, bar.Progress);

        var first = new SkUiContentView { Content = bar };
        var stopped = bar.ProgressTo(1, 100);
        first.AnimationClock.StopAll(); // what a handler disconnect does
        Assert.False(await stopped);

        var moved = bar.ProgressTo(1, 100);
        first.AnimationClock.Tick(TimeSpan.FromMilliseconds(50));
        Assert.InRange(bar.Progress, 0.4, 0.6);
        first.Content = null;
        var second = new SkUiContentView { Content = bar };
        second.AnimationClock.Tick(TimeSpan.FromMilliseconds(10));
        Assert.False(moved.IsCompleted);
        second.AnimationClock.Tick(TimeSpan.FromMilliseconds(70)); // the remaining 50 ms
        Assert.True(await moved);
        Assert.Equal(1, bar.Progress);

        var early = new SkUiProgressBar(); // ProgressTo before the bar is in a tree (e.g. in a page constructor)
        var queued = early.ProgressTo(1, 100);
        var third = new SkUiContentView { Content = early };
        third.AnimationClock.Tick(TimeSpan.FromMilliseconds(200));
        Assert.True(await queued);

        var core = new SkUiCoreProgressBar();
        var host = new SkUiCoreHost().SetContent(core);
        var coreTask = core.ProgressTo(1, 100);
        host.AnimationClock.StopAll();
        Assert.False(await coreTask);
    }

    [Fact]
    public void CoreProgressBarMatches()
    {
        var bar = new SkUiCoreProgressBar();
        bar.SetProgressColor(Colors.Red).SetTrackColor(Colors.Blue).SetHeight(8);
        bar.SetProgress(0.5);
        var host = new SkUiCoreHost().SetContent(bar);
        using (var surface = new SkUiTestSurface(host, 200, 8))
        {
            var bitmap = surface.Frame();
            Assert.Equal(Fill, bitmap.GetPixel(50, 4));
            Assert.Equal(Track, bitmap.GetPixel(150, 4));
        }
        bar.SetIsIndeterminate(true);
        using var sliding = new SkUiTestSurface(host, 200, 8);
        sliding.Frame(0);
        Assert.True(sliding.NeedsFrame);
    }
}
