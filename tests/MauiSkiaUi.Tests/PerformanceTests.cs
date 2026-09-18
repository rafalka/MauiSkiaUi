using System.Diagnostics;
using Xunit;
using SkiaSharp;
using Xunit.Abstractions;

namespace MauiSkiaUi.Tests;

public class PerformanceTests(ITestOutputHelper output)
{
    [Fact]
    public void ThousandLabelRecordingMeasurement()
    {
        var grid = new SkUiGrid();
        for (var index = 0; index < 1000; index++)
        {
            grid.RowDefinitions.Add(new RowDefinition(new GridLength(36)));
            var label = new SkUiLabel { Text = $"Sample {index:0000}", FontSize = 16 };
            Grid.SetRow(label, index);
            grid.Children.Add(label);
        }
        var scroll = new SkUiScrollView { Content = grid };
        var timer = Stopwatch.StartNew();
        SkUiTestHelpers.Arrange(scroll, 400, 600);
        output.WriteLine($"Initial layout: {timer.Elapsed.TotalMilliseconds:F2} ms");
        using var recorder = new SKPictureRecorder();
        void Record()
        {
            var canvas = recorder.BeginRecording(new SKRect(0, 0, 400, 600));
            scroll.Paint(canvas);
            using var picture = recorder.EndRecording();
        }
        Record();
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        timer.Restart();
        for (var frame = 0; frame < 30; frame++) Record();
        output.WriteLine($"Warm recording: {timer.Elapsed.TotalMilliseconds / 30:F3} ms/frame; {(GC.GetAllocatedBytesForCurrentThread() - allocated) / 30} managed bytes/frame");
        Assert.Equal(1000, grid.Children.Count);
        Assert.All(grid.Children, child => Assert.Null(child.Handler));
    }
}
