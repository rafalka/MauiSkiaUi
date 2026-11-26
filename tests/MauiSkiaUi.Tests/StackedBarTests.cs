using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary><see cref="SkUiStackedBar"/> / <see cref="SkUiCoreStackedBar"/>: parts in proportion, the track, animation, semantics.</summary>
[Collection(GlobalStateCollection.Name)]
public class StackedBarTests
{
    private static readonly SKColor Red = SKColors.Red, Green = new(0, 0x80, 0), Blue = SKColors.Blue;

    private static SkUiStackedBar Bar(params SkUiBarSegment[] segments) =>
        new SkUiStackedBar { TrackColor = Colors.Blue, SegmentSpacing = 0, CornerRadius = 0, HeightRequest = 8 }.SetSegments(segments);

    private static SKBitmap Draw(SkUiView bar, FlowDirection direction = FlowDirection.LeftToRight)
    {
        var root = new SkUiContentView { Content = bar, Background = Colors.White, FlowDirection = direction };
        using var surface = new SkUiTestSurface(root, 200, 8);
        var bitmap = surface.Frame().Copy();
        root.Content = null; // the bar can be drawn again elsewhere
        return bitmap;
    }

    private static void WithMotion(Action body)
    {
        var reduce = SkUiMotion.ReduceMotion;
        SkUiMotion.ReduceMotion = false;
        try { body(); }
        finally { SkUiMotion.ReduceMotion = reduce; }
    }

    [Fact]
    public void PartsTakeTheirShareOfTheMaximumAndTheTrackTheRest()
    {
        var bar = Bar(new SkUiBarSegment(1, Colors.Red), new SkUiBarSegment(1, Colors.Green));
        bar.Maximum = 4;
        using var bitmap = Draw(bar);
        Assert.Equal(Red, bitmap.GetPixel(25, 4));
        Assert.Equal(Green, bitmap.GetPixel(75, 4));
        Assert.Equal(Blue, bitmap.GetPixel(150, 4));

        bar.Maximum = -1; // the sum: the parts fill the bar
        using var filled = Draw(bar);
        Assert.Equal(Red, filled.GetPixel(50, 4));
        Assert.Equal(Green, filled.GetPixel(150, 4));
        Assert.Equal(Green, filled.GetPixel(198, 4));

        bar.Segments[0].Value = -5; // counts as 0
        using var negative = Draw(bar);
        Assert.Equal(Green, negative.GetPixel(2, 4));

        using var rtl = Draw(Bar(new SkUiBarSegment(1, Colors.Red)).SetMaximum(4), FlowDirection.RightToLeft);
        Assert.Equal(Red, rtl.GetPixel(175, 4));
        Assert.Equal(Blue, rtl.GetPixel(25, 4));
    }

    [Fact]
    public void SpacingCutsAGapAfterEachPartThatSomethingFollows()
    {
        var bar = Bar(new SkUiBarSegment(1, Colors.Red), new SkUiBarSegment(1, Colors.Green));
        bar.SegmentSpacing = 10;
        using var bitmap = Draw(bar);
        Assert.Equal(Red, bitmap.GetPixel(88, 4));
        Assert.Equal(SKColors.White, bitmap.GetPixel(95, 4));
        Assert.Equal(Green, bitmap.GetPixel(102, 4));
        Assert.Equal(Green, bitmap.GetPixel(199, 4)); // the last part reaches the end
    }

    [Fact]
    public void ChangesAnimateFromWhatIsDrawnAndJumpUnderReduceMotion()
    {
        WithMotion(() =>
        {
            var segment = new SkUiBarSegment(1, Colors.Red);
            var bar = Bar(segment).SetMaximum(4);
            var root = new SkUiContentView { Content = bar, Background = Colors.White };
            using var surface = new SkUiTestSurface(root, 200, 8);
            Assert.Equal(Blue, surface.Frame().GetPixel(75, 4));

            segment.Value = 3; // 50 → 150 px
            root.AnimationClock.Tick(TimeSpan.FromMilliseconds(1));
            Assert.Equal(Blue, surface.Frame().GetPixel(120, 4));
            root.AnimationClock.Tick(TimeSpan.FromMilliseconds(1000));
            Assert.Equal(Red, surface.Frame().GetPixel(140, 4));

            SkUiMotion.ReduceMotion = true;
            segment.Value = 1;
            Assert.Equal(Blue, surface.Frame().GetPixel(75, 4));
        });
    }

    [Fact]
    public void SegmentsBindToTheBarsContext()
    {
        var segment = new SkUiBarSegment { Color = Colors.Red };
        segment.SetBinding(SkUiBarSegment.ValueProperty, nameof(Hours.Taken));
        var bar = Bar(segment).SetMaximum(4);
        bar.BindingContext = new Hours(2);
        Assert.Equal(2, segment.Value);
        using var bitmap = Draw(bar);
        Assert.Equal(Red, bitmap.GetPixel(90, 4));
        Assert.Equal(Blue, bitmap.GetPixel(110, 4));

        bar.Segments.Clear();
        Assert.Null(segment.Parent);
    }

    private sealed record Hours(double Taken);

    [Fact]
    public void ReadAsOneElementListingTheParts()
    {
        var bar = new SkUiStackedBar().SetSegments(new SkUiBarSegment(0, label: "Taken"), new SkUiBarSegment(24, label: "Scheduled"), new SkUiBarSegment(78.5));
        SkUiTestHelpers.Arrange(bar, 200, 8);
        var node = SkUiSemanticsTree.Build(bar).Flatten().Single();
        Assert.Equal(SkUiSemanticsRole.ProgressBar, node.Role);
        Assert.Null(node.Range);
        Assert.Equal($"Taken 0, Scheduled 24, {78.5.ToString(System.Globalization.CultureInfo.CurrentCulture)}", node.Value);

        var core = new SkUiCoreStackedBar().SetSegments(new SkUiBarValue(3, Label: "Available"));
        var host = new SkUiCoreHost().SetContent(core);
        SkUiTestHelpers.Arrange(host, 200, 8);
        Assert.Equal("Available 3", SkUiSemanticsTree.Build(host).Flatten().Single().Value);
    }

    [Fact]
    public void CoreBarDrawsTheSamePixels()
    {
        using var expected = Draw(Bar(new SkUiBarSegment(1, Colors.Red), new SkUiBarSegment(2, Colors.Green)).SetMaximum(5).SetSegmentSpacing(3).SetCornerRadius(4));

        var core = new SkUiCoreStackedBar().SetTrackColor(Colors.Blue).SetSegmentSpacing(3).SetCornerRadius(4).SetMaximum(5)
            .SetSegments(new SkUiBarValue(1, Colors.Red), new SkUiBarValue(2, Colors.Green));
        core.SetHeight(8);
        using var surface = new SkUiTestSurface(new SkUiCoreHost { Background = Colors.White }.SetContent(core), 200, 8);
        var actual = surface.Frame();
        for (var x = 0; x < 200; x++)
            Assert.Equal(expected.GetPixel(x, 4), actual.GetPixel(x, 4));

        Assert.Throws<ArgumentOutOfRangeException>(() => core.SetMaximum(-3));
        Assert.Equal(2, core.Segments.Count);
    }
}
