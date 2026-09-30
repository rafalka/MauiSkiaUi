using System.Diagnostics;
using MauiSkiaUi.Core;
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

    [Fact]
    public void CoreWrapAndShrinkRelayoutAllocatesNothing()
    {
        // The shared wrap / shrink engines take struct child adapters and reuse their buffers (NFR-2).
        var wrap = new SkUiCoreWrapLayout().SetSpacing(4).SetRowSpacing(4);
        var shrink = new SkUiCoreHorizontalShrinkLayout();
        for (var index = 0; index < 40; index++)
        {
            wrap.Add(new FixedCoreNode(new Size(20 + index % 7 * 9, 14)));
            shrink.Add(new FixedCoreNode(new Size(20 + index % 7 * 9, 14)), index % 2 == 0 ? SkUiShrinkFactor.Auto : SkUiShrinkFactor.None);
        }
        foreach (var layout in new SkUiCorePanel[] { wrap, shrink })
        {
            var first = (SkUiCoreNode)layout.Children[0];
            void Relayout(int round)
            {
                first.Width = 10 + round % 2;
                layout.Measure(300, 400);
                layout.Arrange(new Rect(0, 0, 300, 400));
            }
            for (var round = 0; round < 20; round++) Relayout(round);
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            for (var round = 0; round < 100; round++) Relayout(round);
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - allocated);
        }
    }
}
