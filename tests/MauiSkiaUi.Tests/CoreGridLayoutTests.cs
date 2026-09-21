using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>Layout calculation tests for <see cref="SkUiCoreGrid"/>.</summary>
public class CoreGridLayoutTests
{
    [Fact]
    public void EmptyDefinitions_ImplyOneStarRowAndColumn()
    {
        var grid = new SkUiCoreGrid();
        // No explicit size → Fill stretches into the star cell.
        var box = new SkUiCoreBox();
        grid.Add(box);

        grid.Measure(200, 100);
        grid.Arrange(new Rect(0, 0, 200, 100));

        Assert.Equal(1, grid.RowCount);
        Assert.Equal(1, grid.ColumnCount);
        Assert.Equal(200, grid.GetColumnWidth(0), 1);
        Assert.Equal(100, grid.GetRowHeight(0), 1);
        Assert.Equal(new Rect(0, 0, 200, 100), box.Frame);
    }

    [Fact]
    public void AbsoluteTracks_MatchSizesPlusSpacingAndPadding()
    {
        var grid = new SkUiCoreGrid()
            .SetPadding(new Thickness(10))
            .SetRowSpacing(5)
            .SetColumnSpacing(8)
            .SetRowDefinitions([
                new SkUiCoreRowDefinition(new SkUiCoreGridLength(30)),
                new SkUiCoreRowDefinition(new SkUiCoreGridLength(40))
            ])
            .SetColumnDefinitions([
                new SkUiCoreColumnDefinition(new SkUiCoreGridLength(50)),
                new SkUiCoreColumnDefinition(new SkUiCoreGridLength(60))
            ]);

        var a = new SkUiCoreBox();
        var b = new SkUiCoreBox();
        grid.Add(a, 0, 0);
        grid.Add(b, 1, 1);

        var desired = grid.Measure(double.PositiveInfinity, double.PositiveInfinity);
        // 50+8+60 + pad20 = 138; 30+5+40 + pad20 = 95
        Assert.Equal(138, desired.Width, 1);
        Assert.Equal(95, desired.Height, 1);

        grid.Arrange(new Rect(0, 0, desired.Width, desired.Height));
        Assert.Equal(new Rect(10, 10, 50, 30), a.Frame);
        Assert.Equal(new Rect(10 + 50 + 8, 10 + 30 + 5, 60, 40), b.Frame);
    }

    [Fact]
    public void AutoTracks_SizeToLargestChildInTrack()
    {
        var grid = new SkUiCoreGrid()
            .SetRowDefinitions([new SkUiCoreRowDefinition(SkUiCoreGridLength.Auto)])
            .SetColumnDefinitions([
                new SkUiCoreColumnDefinition(SkUiCoreGridLength.Auto),
                new SkUiCoreColumnDefinition(SkUiCoreGridLength.Auto)
            ]);

        var wide = FixedBox(80, 20);
        var tall = FixedBox(30, 40);
        grid.Add(wide, 0, 0);
        grid.Add(tall, 0, 1);

        grid.Measure(double.PositiveInfinity, double.PositiveInfinity);
        grid.Arrange(new Rect(0, 0, grid.DesiredSize.Width, grid.DesiredSize.Height));

        Assert.Equal(80, grid.GetColumnWidth(0), 1);
        Assert.Equal(30, grid.GetColumnWidth(1), 1);
        Assert.Equal(40, grid.GetRowHeight(0), 1);
        // Explicit sizes keep desired frame; Fill does not stretch past Width/Height.
        Assert.Equal(new Rect(0, 0, 80, 20), wide.Frame);
        Assert.Equal(new Rect(80, 0, 30, 40), tall.Frame);
        Assert.Equal(new Rect(0, 0, 80, 40), grid.GetCellBounds(0, 0));
    }

    [Fact]
    public void StarTracks_SplitRemainingSpaceByWeight()
    {
        var grid = new SkUiCoreGrid()
            .SetColumnDefinitions([
                new SkUiCoreColumnDefinition(new SkUiCoreGridLength(40)),
                new SkUiCoreColumnDefinition(SkUiCoreGridLength.Star),
                new SkUiCoreColumnDefinition(new SkUiCoreGridLength(2, SkUiCoreGridUnitType.Star))
            ])
            .SetRowDefinitions([new SkUiCoreRowDefinition(SkUiCoreGridLength.Star)]);

        var a = new SkUiCoreBox();
        var b = new SkUiCoreBox();
        var c = new SkUiCoreBox();
        grid.Add(a, 0, 0);
        grid.Add(b, 0, 1);
        grid.Add(c, 0, 2);

        grid.Measure(200, 100);
        grid.Arrange(new Rect(0, 0, 200, 100));

        Assert.Equal(40, grid.GetColumnWidth(0), 1);
        Assert.Equal(160.0 / 3, grid.GetColumnWidth(1), 1);
        Assert.Equal(320.0 / 3, grid.GetColumnWidth(2), 1);
        Assert.Equal(100, grid.GetRowHeight(0), 1);
    }

    [Fact]
    public void ColumnSpan_UnionsTracksAndSpacing()
    {
        var grid = new SkUiCoreGrid()
            .SetColumnDefinitions([
                new SkUiCoreColumnDefinition(new SkUiCoreGridLength(50)),
                new SkUiCoreColumnDefinition(new SkUiCoreGridLength(50))
            ])
            .SetRowDefinitions([new SkUiCoreRowDefinition(new SkUiCoreGridLength(40))])
            .SetColumnSpacing(10);

        var spanned = new SkUiCoreBox();
        grid.Add(spanned, 0, 0, rowSpan: 1, columnSpan: 2);

        grid.Measure(double.PositiveInfinity, double.PositiveInfinity);
        grid.Arrange(new Rect(0, 0, grid.DesiredSize.Width, grid.DesiredSize.Height));

        Assert.Equal(new Rect(0, 0, 110, 40), spanned.Frame);
        Assert.Equal(new Rect(0, 0, 110, 40), grid.GetCellBounds(0, 0, 1, 2));
    }

    [Fact]
    public void AutoSpan_GrowsTracksForSpannedContent()
    {
        var grid = new SkUiCoreGrid()
            .SetColumnDefinitions([
                new SkUiCoreColumnDefinition(SkUiCoreGridLength.Auto),
                new SkUiCoreColumnDefinition(SkUiCoreGridLength.Auto)
            ])
            .SetRowDefinitions([new SkUiCoreRowDefinition(SkUiCoreGridLength.Auto)]);

        var spanned = FixedBox(100, 20);
        grid.Add(spanned, 0, 0, columnSpan: 2);

        grid.Measure(double.PositiveInfinity, double.PositiveInfinity);
        grid.Arrange(new Rect(0, 0, grid.DesiredSize.Width, grid.DesiredSize.Height));

        Assert.Equal(100, grid.DesiredSize.Width, 1);
        Assert.Equal(100, spanned.Frame.Width, 1);
    }

    [Fact]
    public void HiddenChild_IsSkippedByTracks()
    {
        var grid = new SkUiCoreGrid()
            .SetColumnDefinitions([
                new SkUiCoreColumnDefinition(SkUiCoreGridLength.Auto),
                new SkUiCoreColumnDefinition(SkUiCoreGridLength.Auto)
            ])
            .SetRowDefinitions([new SkUiCoreRowDefinition(SkUiCoreGridLength.Auto)]);

        var visible = FixedBox(40, 20);
        var hidden = FixedBox(200, 200);
        hidden.IsVisible = false;
        grid.Add(visible, 0, 0);
        grid.Add(hidden, 0, 1);

        grid.Measure(double.PositiveInfinity, double.PositiveInfinity);
        grid.Arrange(new Rect(0, 0, grid.DesiredSize.Width, grid.DesiredSize.Height));

        Assert.Equal(40, grid.GetColumnWidth(0), 1);
        Assert.Equal(0, grid.GetColumnWidth(1), 1);
        Assert.Equal(Rect.Zero, hidden.Frame);
    }

    [Fact]
    public void Alignment_CentersChildInsideLargerCell()
    {
        var grid = new SkUiCoreGrid()
            .SetColumnDefinitions([new SkUiCoreColumnDefinition(new SkUiCoreGridLength(100))])
            .SetRowDefinitions([new SkUiCoreRowDefinition(new SkUiCoreGridLength(80))]);

        var box = FixedBox(40, 20);
        box.HorizontalAlignment = LayoutAlignment.Center;
        box.VerticalAlignment = LayoutAlignment.Center;
        grid.Add(box);

        grid.Measure(100, 80);
        grid.Arrange(new Rect(0, 0, 100, 80));

        Assert.Equal(new Rect(30, 30, 40, 20), box.Frame);
    }

    [Fact]
    public void DefinitionMinMax_ClampsAbsoluteAndAuto()
    {
        var grid = new SkUiCoreGrid()
            .SetColumnDefinitions([
                new SkUiCoreColumnDefinition(new SkUiCoreGridLength(200)).SetMaxWidth(80).SetMinWidth(40)
            ])
            .SetRowDefinitions([
                new SkUiCoreRowDefinition(SkUiCoreGridLength.Auto).SetMinHeight(50).SetMaxHeight(60)
            ]);

        var box = FixedBox(10, 10);
        grid.Add(box);

        grid.Measure(double.PositiveInfinity, double.PositiveInfinity);
        grid.Arrange(new Rect(0, 0, grid.DesiredSize.Width, grid.DesiredSize.Height));

        Assert.Equal(80, grid.GetColumnWidth(0), 1);
        Assert.Equal(50, grid.GetRowHeight(0), 1);
    }

    [Fact]
    public void StarMax_RedistributesLeftover()
    {
        var grid = new SkUiCoreGrid()
            .SetColumnDefinitions([
                new SkUiCoreColumnDefinition(SkUiCoreGridLength.Star).SetMaxWidth(40),
                new SkUiCoreColumnDefinition(SkUiCoreGridLength.Star)
            ])
            .SetRowDefinitions([new SkUiCoreRowDefinition(SkUiCoreGridLength.Star)]);

        grid.Add(new SkUiCoreBox(), 0, 0);
        grid.Add(new SkUiCoreBox(), 0, 1);

        grid.Measure(200, 50);
        grid.Arrange(new Rect(0, 0, 200, 50));

        Assert.Equal(40, grid.GetColumnWidth(0), 1);
        Assert.Equal(160, grid.GetColumnWidth(1), 1);
    }

    [Fact]
    public void StarMin_IsFloorNotAddedToProportionalShare()
    {
        var grid = new SkUiCoreGrid()
            .SetColumnDefinitions([
                new SkUiCoreColumnDefinition(SkUiCoreGridLength.Star).SetMinWidth(40),
                new SkUiCoreColumnDefinition(SkUiCoreGridLength.Star)
            ])
            .SetRowDefinitions([
                new SkUiCoreRowDefinition(SkUiCoreGridLength.Star).SetMinHeight(40),
                new SkUiCoreRowDefinition(SkUiCoreGridLength.Star)
            ]);

        grid.Add(new SkUiCoreBox(), 0, 0);
        grid.Add(new SkUiCoreBox(), 0, 1);
        grid.Add(new SkUiCoreBox(), 1, 0);

        grid.Measure(200, 200);
        grid.Arrange(new Rect(0, 0, 200, 200));

        Assert.Equal(100, grid.GetColumnWidth(0), 1);
        Assert.Equal(100, grid.GetColumnWidth(1), 1);
        Assert.Equal(100, grid.GetRowHeight(0), 1);
        Assert.Equal(100, grid.GetRowHeight(1), 1);
    }

    [Fact]
    public void StarMin_TakesFromSiblingWhenShareIsSmaller()
    {
        var grid = new SkUiCoreGrid()
            .SetColumnDefinitions([
                new SkUiCoreColumnDefinition(SkUiCoreGridLength.Star).SetMinWidth(150),
                new SkUiCoreColumnDefinition(SkUiCoreGridLength.Star)
            ])
            .SetRowDefinitions([new SkUiCoreRowDefinition(SkUiCoreGridLength.Star)]);

        grid.Add(new SkUiCoreBox(), 0, 0);
        grid.Add(new SkUiCoreBox(), 0, 1);

        grid.Measure(200, 40);
        grid.Arrange(new Rect(0, 0, 200, 40));

        Assert.Equal(150, grid.GetColumnWidth(0), 1);
        Assert.Equal(50, grid.GetColumnWidth(1), 1);
    }

    [Fact]
    public void SpanDeficit_PrefersRemainingAutoRoomOverStar()
    {
        // Two Auto columns (first capped) + Star: a wide spanned cell must give leftover
        // to the second Auto after the first hits Max — not dump it on the Star track.
        var grid = new SkUiCoreGrid()
            .SetPadding(new Thickness(0))
            .SetColumnSpacing(0)
            .SetColumnDefinitions([
                new SkUiCoreColumnDefinition(SkUiCoreGridLength.Auto).SetMaxWidth(20),
                new SkUiCoreColumnDefinition(SkUiCoreGridLength.Auto),
                new SkUiCoreColumnDefinition(SkUiCoreGridLength.Star)
            ])
            .SetRowDefinitions([new SkUiCoreRowDefinition(new SkUiCoreGridLength(20))]);

        var spanned = FixedBox(100, 10);
        grid.Add(spanned, 0, 0, columnSpan: 3);

        grid.Measure(200, 20);
        grid.Arrange(new Rect(0, 0, 200, 20));

        Assert.Equal(20, grid.GetColumnWidth(0), 1);
        Assert.Equal(80, grid.GetColumnWidth(1), 1);
        Assert.Equal(100, grid.GetColumnWidth(2), 1);
    }

    [Fact]
    public void SpotParity_WithMauiGrid_AbsoluteAutoStar()
    {
        var core = new SkUiCoreGrid()
            .SetPadding(new Thickness(10))
            .SetRowSpacing(5)
            .SetColumnSpacing(10)
            .SetRowDefinitions([
                new SkUiCoreRowDefinition(SkUiCoreGridLength.Auto),
                new SkUiCoreRowDefinition(SkUiCoreGridLength.Star)
            ])
            .SetColumnDefinitions([
                new SkUiCoreColumnDefinition(new SkUiCoreGridLength(60)),
                new SkUiCoreColumnDefinition(SkUiCoreGridLength.Star)
            ]);
        var coreFirst = FixedBox(60, 30);
        var coreSecond = new SkUiCoreBox();
        core.Add(coreFirst, 0, 0);
        core.Add(coreSecond, 1, 1);
        core.Measure(240, 160);
        core.Arrange(new Rect(0, 0, 240, 160));

        var maui = new SkUiGrid
        {
            Padding = new Thickness(10),
            RowSpacing = 5,
            ColumnSpacing = 10,
            RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)],
            ColumnDefinitions = [new ColumnDefinition(new GridLength(60)), new ColumnDefinition(GridLength.Star)]
        };
        var mauiFirst = new SkUiBox { WidthRequest = 60, HeightRequest = 30 };
        var mauiSecond = new SkUiBox();
        Grid.SetRow(mauiSecond, 1);
        Grid.SetColumn(mauiSecond, 1);
        maui.Children.Add(mauiFirst);
        maui.Children.Add(mauiSecond);
        SkUiTestHelpers.Arrange(maui, 240, 160);

        Assert.Equal(150, core.GetColumnWidth(1), 1);
        Assert.Equal(mauiFirst.Frame.X, coreFirst.Frame.X, 1);
        Assert.Equal(mauiFirst.Frame.Y, coreFirst.Frame.Y, 1);
        Assert.Equal(mauiFirst.Frame.Width, coreFirst.Frame.Width, 1);
        Assert.Equal(mauiFirst.Frame.Height, coreFirst.Frame.Height, 1);
        Assert.Equal(mauiSecond.Frame.X, coreSecond.Frame.X, 1);
        Assert.Equal(mauiSecond.Frame.Y, coreSecond.Frame.Y, 1);
        Assert.Equal(mauiSecond.Frame.Width, coreSecond.Frame.Width, 1);
        Assert.Equal(mauiSecond.Frame.Height, coreSecond.Frame.Height, 1);
    }

    private static SkUiCoreBox FixedBox(double width, double height)
    {
        var box = new SkUiCoreBox();
        box.Width = width;
        box.Height = height;
        return box;
    }
}
