using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// 1.17 S2 <see cref="SkUiSegmentedEngine"/> on its own: equal and content widths (desired, wider, narrower), insets and
/// spacing, right to left, zero segments, more content than width (shrinking, and overflowing for a scrolling strip), the
/// selection marker between two segments and at a fractional position, and hit-testing gaps and insets.
/// </summary>
public class SegmentedEngineTests
{
    private const double Tolerance = 1e-9;

    private static SkUiSegmentedEngine Engine(SkUiSegmentWidthMode mode, params double[] widths)
    {
        var engine = new SkUiSegmentedEngine { Mode = mode };
        engine.SetContentWidths(widths);
        return engine;
    }

    private static void AssertRect(Rect expected, Rect actual)
    {
        Assert.Equal(expected.X, actual.X, Tolerance);
        Assert.Equal(expected.Y, actual.Y, Tolerance);
        Assert.Equal(expected.Width, actual.Width, Tolerance);
        Assert.Equal(expected.Height, actual.Height, Tolerance);
    }

    [Fact]
    public void EqualSegmentsAreAsWideAsTheWidestAndSplitAWiderRowEqually()
    {
        var engine = Engine(SkUiSegmentWidthMode.Equal, 40, 80, 60);

        Assert.Equal(240, engine.DesiredWidth(), Tolerance);
        engine.Arrange(300, 32);
        AssertRect(new Rect(0, 0, 100, 32), engine.Segment(0));
        AssertRect(new Rect(200, 0, 100, 32), engine.Segment(2));
        Assert.Equal(300, engine.Length, Tolerance);
    }

    [Fact]
    public void ContentSegmentsKeepTheirShareOfTheRowWhenItIsWiderOrNarrower()
    {
        var engine = Engine(SkUiSegmentWidthMode.Content, 40, 80, 80);

        Assert.Equal(200, engine.DesiredWidth(), Tolerance);
        engine.Arrange(300, 32);
        Assert.Equal((60d, 120d, 120d), (engine.Segment(0).Width, engine.Segment(1).Width, engine.Segment(2).Width));
        Assert.Equal(180, engine.Segment(2).X, Tolerance);

        engine.Arrange(100, 32); // narrower than the content: shrunk in proportion
        Assert.Equal((20d, 40d, 40d), (engine.Segment(0).Width, engine.Segment(1).Width, engine.Segment(2).Width));
        Assert.Equal(100, engine.Length, Tolerance);
    }

    [Fact]
    public void InsetsAndSpacingAreLeftAroundAndBetweenTheSegments()
    {
        var engine = Engine(SkUiSegmentWidthMode.Equal, 50, 50);
        engine.Inset = 2;
        engine.Spacing = 4;

        Assert.Equal(2 * 50 + 4 + 2 * 2, engine.DesiredWidth(), Tolerance);
        engine.Arrange(204, 36);
        AssertRect(new Rect(2, 2, 98, 32), engine.Segment(0));
        AssertRect(new Rect(104, 2, 98, 32), engine.Segment(1));
    }

    [Fact]
    public void RightToLeftPutsTheFirstSegmentAtTheRightEnd()
    {
        var engine = Engine(SkUiSegmentWidthMode.Content, 40, 80);
        engine.Mirrored = true;
        engine.Inset = 2;

        engine.Arrange(124, 30);

        AssertRect(new Rect(82, 2, 40, 26), engine.Segment(0));
        AssertRect(new Rect(2, 2, 80, 26), engine.Segment(1));
        Assert.Equal(0, engine.HitTest(100));
        Assert.Equal(1, engine.HitTest(10));
    }

    [Fact]
    public void WithoutSegmentsTheRowIsItsInsetsAndNothingIsHitOrSelected()
    {
        var engine = Engine(SkUiSegmentWidthMode.Equal);
        engine.Inset = 3;

        Assert.Equal(6, engine.DesiredWidth(), Tolerance);
        engine.Arrange(100, 30);
        Assert.Equal(0, engine.Count);
        Assert.Equal(-1, engine.HitTest(50));
        Assert.Equal(Rect.Zero, engine.Selection(-1, 0, 1));
        Assert.Equal(Rect.Zero, engine.SelectionAt(0));
    }

    [Fact]
    public void AStripThatOverflowsKeepsTheContentWidthsAndGrowsLongerThanTheRow()
    {
        var engine = Engine(SkUiSegmentWidthMode.Content, 120, 90, 150);
        engine.Overflows = true;

        engine.Arrange(200, 48);

        Assert.Equal(360, engine.Length, Tolerance);
        AssertRect(new Rect(210, 0, 150, 48), engine.Segment(2));
        engine.Arrange(400, 48); // fits: shared out as without overflowing
        Assert.Equal(400, engine.Length, Tolerance);
        Assert.Equal(400 * 150 / 360d, engine.Segment(2).Width, Tolerance);

        engine.Mirrored = true;
        engine.Arrange(200, 48);
        AssertRect(new Rect(240, 0, 120, 48), engine.Segment(0)); // mirrored across the whole strip
    }

    [Fact]
    public void TheSelectionBlendsPositionAndWidthBetweenSegments()
    {
        var engine = Engine(SkUiSegmentWidthMode.Content, 40, 120);
        engine.Arrange(160, 32);

        AssertRect(new Rect(0, 0, 40, 32), engine.Selection(-1, 0, 0.3)); // nothing before: on the target
        AssertRect(new Rect(20, 0, 80, 32), engine.Selection(0, 1, 0.5));
        AssertRect(new Rect(40, 0, 120, 32), engine.Selection(0, 1, 2)); // clamped
        Assert.Equal(Rect.Zero, engine.Selection(0, -1, 0.5));
        AssertRect(engine.Selection(0, 1, 0.25), engine.SelectionAt(0.25));
        AssertRect(engine.Segment(1), engine.SelectionAt(7)); // within the segments
    }

    [Fact]
    public void GapsAndInsetsHitTheNearestSegmentAndPointsOutsideTheRowNone()
    {
        var engine = Engine(SkUiSegmentWidthMode.Equal, 50, 50, 50);
        engine.Inset = 4;
        engine.Spacing = 10;
        engine.Arrange(172, 32); // segments 48 wide at 4–52, 62–110 and 120–168

        Assert.Equal(0, engine.HitTest(1));   // the inset
        Assert.Equal(0, engine.HitTest(53));
        Assert.Equal(1, engine.HitTest(59));  // the gap, nearer the second
        Assert.Equal(2, engine.HitTest(171));
        Assert.Equal(-1, engine.HitTest(-1));
        Assert.Equal(-1, engine.HitTest(173));
    }

    [Fact]
    public void LayingOutAgainAllocatesNothing()
    {
        var engine = Engine(SkUiSegmentWidthMode.Content, 40, 80, 60);
        Span<double> widths = [40, 90, 60];
        engine.Arrange(300, 32);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var pass = 0; pass < 100; pass++)
        {
            engine.SetContentWidths(widths);
            engine.Arrange(200 + pass, 32);
            _ = engine.Selection(0, 2, pass / 100d);
            _ = engine.HitTest(pass);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
