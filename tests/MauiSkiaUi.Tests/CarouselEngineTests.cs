using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// 1.17 C1 <see cref="SkUiCarouselEngine"/> on its own, with no views: filling items and a fixed extent, peek insets and
/// spacing, each snap alignment, an unbounded viewport measured from the first item, looping (copies, the shorter way round,
/// moving back by whole cycles), right to left, zero and one item, snap offsets clamped at the ends, the item at the snap
/// point, placements deferred to the next layout and the place kept when the viewport changes.
/// </summary>
public class CarouselEngineTests
{
    private const double Tolerance = 1e-9;

    private sealed class Host : ISkUiCarouselEngineHost
    {
        public bool Loop { get; set; }

        public bool Horizontal { get; set; } = true;

        public bool Mirrored { get; set; }

        public double Probe { get; set; } = 100;

        public int Probes { get; private set; }

        public double ProbeExtent(double cross)
        {
            Probes++;
            return Probe;
        }
    }

    private static SkUiCarouselLayout Layout(Thickness insets = default, double spacing = 0, double extent = 0,
        SnapPointsAlignment alignment = SnapPointsAlignment.Center, int perPage = 1) => new(insets, spacing, extent, alignment, null, perPage);

    /// <summary>An engine laid out for <paramref name="count"/> items in a <paramref name="viewport"/> long viewport.</summary>
    private static SkUiCarouselEngine LaidOut(Host host, int count, double viewport, SkUiCarouselLayout layout, int position = 0)
    {
        var engine = new SkUiCarouselEngine(host);
        engine.SetCount(count, position);
        engine.SetViewport(viewport, 0, arranged: false);
        engine.UpdateGeometry(layout, 200, position);
        return engine;
    }

    [Fact]
    public void FillingItemsAreAsLongAsTheViewportAndTheFirstLayoutPlacesThePosition()
    {
        var engine = LaidOut(new Host(), 5, 300, Layout(), position: 2);

        Assert.True(engine.HasGeometry);
        Assert.Equal((300d, 300d, 5, 1), (engine.Extent, engine.Stride, engine.Slots, engine.Copies));
        Assert.Equal(1500, engine.Length, Tolerance);
        Assert.Equal(1200, engine.MaxLogical, Tolerance);
        Assert.True(engine.HasPendingOffset);
        Assert.Equal(600, engine.TakePendingOffset()!.Value, Tolerance);
        Assert.Null(engine.TakePendingOffset()); // once
    }

    [Fact]
    public void PeekInsetsAndSpacingShortenFillingItemsAndCenterTheCurrentOne()
    {
        var engine = LaidOut(new Host(), 5, 300, Layout(new Thickness(40, 0, 40, 0), spacing: 10));

        Assert.Equal(220, engine.Extent, Tolerance);
        Assert.Equal(230, engine.Stride, Tolerance);
        Assert.Equal(40, engine.PadStart, Tolerance);
        Assert.Equal(270, engine.SlotStart(1), Tolerance);
        Assert.Equal(40 + 5 * 230 - 10 + 40, engine.Length, Tolerance);
        // At item 1's snap offset, item 1 starts at the end of the leading inset.
        Assert.Equal(40, engine.SlotStart(1) - engine.SnapOffset(1), Tolerance);
    }

    [Theory]
    [InlineData(SnapPointsAlignment.Start, 200)]
    [InlineData(SnapPointsAlignment.Center, 100)]
    [InlineData(SnapPointsAlignment.End, 0)]
    public void TheAlignmentSaysWhereTheCurrentItemLinesUp(SnapPointsAlignment alignment, double snapOffsetOfThirdItem)
    {
        var engine = LaidOut(new Host(), 5, 300, Layout(extent: 100, alignment: alignment));

        Assert.Equal(snapOffsetOfThirdItem, engine.SnapOffset(2), Tolerance);
    }

    [Fact]
    public void AnUnboundedViewportIsOneMeasuredItemAndTheInsets()
    {
        var host = new Host { Probe = 120 };
        var engine = LaidOut(host, 5, double.PositiveInfinity, Layout(new Thickness(10, 0, 10, 0)));

        Assert.Equal(1, host.Probes);
        Assert.Equal(120, engine.Extent, Tolerance);
        Assert.Equal(140, engine.ViewportLength, Tolerance);
    }

    [Fact]
    public void WithoutItemsNothingIsLaidOutOrMeasured()
    {
        var host = new Host();
        var engine = new SkUiCarouselEngine(host);
        engine.SetCount(0, 0);
        engine.SetViewport(double.PositiveInfinity, 0, arranged: false);

        Assert.False(engine.UpdateGeometry(Layout(), 200, 0));
        Assert.False(engine.HasGeometry);
        Assert.Equal(0, host.Probes);
        Assert.Null(engine.TakePendingOffset());
        Assert.Equal(-1, engine.LastVisibleIndex(0));
    }

    [Fact]
    public void OneItemDoesNotLoop()
    {
        var engine = LaidOut(new Host { Loop = true }, 1, 300, Layout());

        Assert.False(engine.Loops);
        Assert.Equal(1, engine.Copies);
    }

    [Fact]
    public void ALoopingStripHoldsAnOddNumberOfCopiesAndStartsInTheMiddleOne()
    {
        var engine = LaidOut(new Host { Loop = true }, 5, 300, Layout(), position: 3);

        Assert.True(engine.Loops);
        Assert.True(engine.Copies > 1 && engine.Copies % 2 == 1);
        Assert.Equal(5 * engine.Copies, engine.Slots);
        var home = engine.Copies / 2 * 5;
        var logical = engine.TakePendingOffset()!.Value;
        Assert.Equal(engine.SnapOffset(home + 3), logical, Tolerance);
        Assert.Equal(3, engine.CurrentIndex(logical, 0));
        Assert.Equal(3, engine.IndexOfSlot(home + 3));
    }

    [Fact]
    public void ALoopingStripGoesTheShorterWayRoundAndWrapsItsFractionalPosition()
    {
        var engine = LaidOut(new Host { Loop = true }, 5, 300, Layout());
        var logical = engine.TakePendingOffset()!.Value; // item 0 in the middle copy
        var home = engine.NearestSlot(logical);

        Assert.Equal(home - 1, engine.NearestSlotOf(4, logical));
        Assert.Equal(home + 2, engine.NearestSlotOf(2, logical));
        Assert.Equal(home - 2, engine.NearestSlotOf(3, logical));
        Assert.Equal(4.5, engine.ScrollPosition(logical - engine.Stride / 2), Tolerance);
    }

    [Fact]
    public void ALoopingStripFarFromItsMiddleMovesBackByWholeCycles()
    {
        var engine = LaidOut(new Host { Loop = true }, 5, 300, Layout());
        var home = engine.TakePendingOffset()!.Value;

        Assert.Equal(0, engine.RecenterCycles(home + 0.5 * engine.CycleLength, out _));
        Assert.Equal(1, engine.RecenterCycles(home + engine.CycleLength, out var correction));
        Assert.Equal(-engine.CycleLength, correction, Tolerance);
        Assert.Equal(-2, engine.RecenterCycles(home - 2 * engine.CycleLength, out correction));
        Assert.Equal(2 * engine.CycleLength, correction, Tolerance);
    }

    [Fact]
    public void APendingPlacementHoldsTheStripWhereItIs()
    {
        var engine = LaidOut(new Host { Loop = true }, 5, 300, Layout());
        var home = engine.SnapOffset(engine.Copies / 2 * 5);

        Assert.Equal(0, engine.RecenterCycles(home + engine.CycleLength, out _)); // not taken yet
        Assert.Equal(home, engine.LogicalOffset(12345), Tolerance);               // the pending offset wins over the scroller's
    }

    [Fact]
    public void RightToLeftMirrorsOffsetsAndTheInsets()
    {
        var host = new Host { Mirrored = true };
        var engine = LaidOut(host, 5, 300, Layout(new Thickness(40, 0, 0, 0), alignment: SnapPointsAlignment.Start));

        Assert.Equal(0, engine.PadStart, Tolerance); // the left inset is the trailing one
        Assert.Equal(engine.MaxLogical, engine.TakePendingOffset()!.Value, Tolerance); // the first item is at the right end
        Assert.Equal(engine.MaxLogical - 100, engine.Physical(100), Tolerance);
        Assert.Equal(100, engine.Logical(engine.Physical(100)), Tolerance);
    }

    [Fact]
    public void SnapOffsetsOfAStripThatDoesNotLoopAreClampedAtItsEnds()
    {
        var engine = LaidOut(new Host(), 5, 300, Layout(extent: 100));
        var points = new List<double>();

        engine.GetSnapOffsets(0, engine.MaxLogical, points);

        Assert.Equal([0, 0, 100, 200, 200], points);
    }

    [Fact]
    public void ItemsSharingAClampedSnapPointKeepTheCurrentOne()
    {
        var engine = LaidOut(new Host(), 5, 300, Layout(extent: 100));

        Assert.Equal(0, engine.CurrentIndex(0, keep: 0));
        Assert.Equal(1, engine.CurrentIndex(0, keep: 1));
        Assert.Equal(1, engine.CurrentIndex(0, keep: 3)); // not at its snap point: the nearest
        Assert.Equal(2, engine.LastVisibleIndex(0));
    }

    [Fact]
    public void TheSlotWindowTakesOneItemOnEitherSideAndTheItemsAnEffectShows()
    {
        var engine = LaidOut(new Host(), 5, 300, Layout());

        Assert.Equal((0, 2), engine.SlotWindow(300, 0));
        Assert.Equal((0, 4), engine.SlotWindow(300, 3));
        Assert.Equal((1, 1), engine.VisibleSlots(300));
    }

    [Fact]
    public void ANewViewportKeepsTheFractionalItemAtTheSnapPoint()
    {
        var engine = LaidOut(new Host(), 5, 300, Layout());
        engine.TakePendingOffset();

        Assert.True(engine.SetViewport(400, 450, arranged: true)); // 1.5 items along
        Assert.False(engine.SetViewport(400, 450, arranged: true));
        Assert.True(engine.NeedsLayout(200));
        Assert.True(engine.UpdateGeometry(Layout(), 200, 0));
        Assert.Equal(600, engine.TakePendingOffset()!.Value, Tolerance);
        Assert.False(engine.NeedsLayout(200));
        Assert.True(engine.NeedsLayout(250));
    }

    [Fact]
    public void APlacementBeforeTheFirstArrangeWaitsForTheLayout()
    {
        var engine = LaidOut(new Host(), 5, 300, Layout());

        Assert.True(engine.DeferPlacement(3, engine.LogicalOffset(0), arranged: false));
        Assert.True(engine.UpdateGeometry(Layout(), 200, 0));
        Assert.Equal(900, engine.TakePendingOffset()!.Value, Tolerance);
        Assert.False(engine.DeferPlacement(1, 900, arranged: true)); // laid out and arranged: scroll now
        Assert.Equal(300, engine.TargetOffset(1, 900), Tolerance);
    }

    #region Pages

    [Fact]
    public void PagesOfThreeFillTheViewportAndTheShortLastPageRestsAtTheEnd()
    {
        var engine = LaidOut(new Host(), 10, 300, Layout(perPage: 3));
        var points = new List<double>();

        engine.GetSnapOffsets(0, engine.MaxLogical, points);

        Assert.Equal((100d, 4), (engine.Extent, engine.Pages));
        Assert.Equal(700, engine.MaxLogical, Tolerance);
        Assert.Equal([0, 300, 600, 700], points);
        Assert.Equal(6, engine.CurrentIndex(640, keep: 0));
        Assert.Equal(9, engine.CurrentIndex(660, keep: 0)); // nearer to where the last page rests than to the page before
        Assert.Equal(9, engine.CurrentIndex(700, keep: 0));
        Assert.Equal(6, engine.CurrentIndex(600, keep: 7)); // not a page's first item: the page there
        Assert.Equal(0, engine.PagePosition(0), Tolerance);
        Assert.Equal(1.5, engine.PagePosition(450), Tolerance);
        Assert.Equal(2.5, engine.PagePosition(650), Tolerance); // the short step to the last page
        Assert.Equal(3, engine.PagePosition(700), Tolerance);
    }

    [Fact]
    public void FillingPagesLeaveRoomForTheInsetsAndTheSpacingBetweenTheirItems()
    {
        var engine = LaidOut(new Host(), 10, 300, Layout(new Thickness(30, 0, 30, 0), spacing: 6, perPage: 3));

        Assert.Equal(76, engine.Extent, Tolerance); // (300 - 2 × 30 - 2 × 6) / 3
        Assert.Equal(82, engine.Stride, Tolerance);
        Assert.Equal(3 * 82, engine.SnapOffset(3), Tolerance); // the page fills the room between the insets: no alignment shift
    }

    [Fact]
    public void WithAnItemExtentThePageOfItsItemsLinesUp()
    {
        var engine = LaidOut(new Host(), 10, 300, Layout(extent: 50, perPage: 3));

        Assert.Equal(50, engine.Extent, Tolerance);
        Assert.Equal(150 - 75, engine.SnapOffset(3), Tolerance); // the 150-long page centered in 300
    }

    [Fact]
    public void AnUnboundedViewportHoldsAPageOfMeasuredItems()
    {
        var engine = LaidOut(new Host { Probe = 120 }, 10, double.PositiveInfinity, Layout(spacing: 10, perPage: 2));

        Assert.Equal(120, engine.Extent, Tolerance);
        Assert.Equal(250, engine.ViewportLength, Tolerance);
    }

    [Fact]
    public void LoopingPagesStartAgainWithEachCycle()
    {
        var engine = LaidOut(new Host { Loop = true }, 10, 300, Layout(perPage: 3));
        var home = engine.Copies / 2 * 10;
        var points = new List<double>();

        engine.GetSnapOffsets(engine.SnapOffset(home), engine.SnapOffset(home + 20), points);

        // 1–3, 4–6, 7–9, 10, then 1–3 again: the short page is a short step, and the second round is the same.
        Assert.Equal([300, 300, 300, 100, 300, 300, 300, 100], points.Zip(points.Skip(1), static (a, b) => b - a));
        var lastPage = engine.SnapOffset(home + 9);
        Assert.Equal(home + 9, engine.NearestPageSlot(lastPage + 40));
        Assert.Equal(home + 10, engine.NearestPageSlot(lastPage + 50)); // halves go to the next page
        Assert.Equal(0, engine.CurrentIndex(lastPage + 60, keep: 9));
        Assert.Equal(3.5, engine.PagePosition(lastPage + 50), Tolerance);
        Assert.Equal(1.5, engine.PagePosition(engine.SnapOffset(home + 13) + 150), Tolerance);
        Assert.Equal(home + 3, engine.NearestSlotOf(3, engine.SnapOffset(home)));
    }

    #endregion
}
