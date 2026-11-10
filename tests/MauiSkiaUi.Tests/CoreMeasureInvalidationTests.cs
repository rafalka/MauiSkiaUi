using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// 1.9 R2: a Core node's measure change lays its ancestors out again without re-recording them (as SkUi* views since N5);
/// a container re-records when its arranged size changes or when it paints from its children's layout (the table).
/// </summary>
public class CoreMeasureInvalidationTests
{
    [Fact]
    public void AChildsMeasureChangeReRecordsOnlyTheChildInADeepCoreTree()
    {
        var box = new SkUiCoreBox().SetColor(Colors.Red);
        box.SetWidth(80).SetHeight(20).SetHorizontalAlignment(LayoutAlignment.Start);
        var inner = new SkUiCoreVerticalStackLayout();
        inner.SetBackground(Colors.Yellow);
        inner.Add(box);
        var outer = new SkUiCoreVerticalStackLayout();
        outer.SetBackground(Colors.Green);
        outer.Add(inner);
        var border = new SkUiCoreBorder().SetContent(outer);
        border.SetBackground(Colors.Blue);
        var host = new SkUiCoreHost { Background = Colors.White }.SetContent(border);
        using var surface = new SkUiTestSurface(host, 200, 80);
        surface.Frame();

        var recorded = surface.RecordedPictures;
        box.SetWidth(40);
        SkUiTestHelpers.Arrange(host, 200, 80);
        surface.Frame();

        Assert.Equal(40, box.Frame.Width);
        Assert.Equal(1, surface.RecordedPictures - recorded); // the box; not the stacks, the border or the host
    }

    [Fact]
    public void AContainerWhoseSizeFollowsTheChildReRecordsWhenArranged()
    {
        var box = new SkUiCoreBox().SetColor(Colors.Red);
        box.SetWidth(80).SetHeight(20);
        var stack = new SkUiCoreVerticalStackLayout();
        stack.SetBackground(Colors.Yellow).SetHorizontalAlignment(LayoutAlignment.Start);
        stack.Add(box);
        var outer = new SkUiCoreVerticalStackLayout();
        outer.SetBackground(Colors.Green);
        outer.Add(stack);
        var host = new SkUiCoreHost { Background = Colors.White }.SetContent(outer);
        using var surface = new SkUiTestSurface(host, 200, 80);
        surface.Frame();

        var recorded = surface.RecordedPictures;
        box.SetWidth(40);
        SkUiTestHelpers.Arrange(host, 200, 80);
        surface.Frame();

        Assert.Equal(40, stack.Frame.Width);
        Assert.Equal(2, surface.RecordedPictures - recorded); // the box and the resized stack, not the outer stack or the host
    }

    [Fact]
    public void ATableReRecordsItsTracksWhenACellsMeasureChanges()
    {
        var cell = new SkUiCoreBox().SetColor(Colors.Red);
        cell.SetWidth(30).SetHeight(20);
        var other = new SkUiCoreBox().SetColor(Colors.Blue);
        other.SetHeight(20);
        var table = new SkUiCoreTable()
            .SetColumnBackground(0, Colors.Yellow)
            .SetColumnSeparatorColor(Colors.Black)
            .SetColumnSeparatorThickness(1);
        table.SetColumnDefinitions([
            new SkUiCoreColumnDefinition(SkUiCoreGridLength.Auto),
            new SkUiCoreColumnDefinition(SkUiCoreGridLength.Star)
        ]);
        table.Add(cell);
        table.Add(other);
        table.SetColumn(other, 1);
        var host = new SkUiCoreHost { Background = Colors.White }.SetContent(table);
        using var surface = new SkUiTestSurface(host, 200, 40);
        surface.Frame();

        var recorded = surface.RecordedPictures;
        cell.SetWidth(60);
        SkUiTestHelpers.Arrange(host, 200, 40);
        surface.Frame();

        Assert.Equal(60, table.GetColumnWidth(0), 3);
        // The cell, the table (its column background and separator moved) and the resized neighbor; not the host.
        Assert.Equal(3, surface.RecordedPictures - recorded);
    }
    [Fact]
    public void AnAttachedLayoutPropertyRequestsTheFrameItsLayoutRunsIn()
    {
        var cell = new SkUiCoreBox().SetColor(Colors.Red);
        var grid = new SkUiCoreGrid().SetColumnDefinitions([new SkUiCoreColumnDefinition(), new SkUiCoreColumnDefinition()]);
        grid.Add(cell);
        var host = new SkUiCoreHost().SetContent(grid);
        using var surface = new SkUiTestSurface(host, 200, 40);
        surface.Frame();

        cell.SetValue(SkUiCoreGrid.ColumnProperty, 1); // nothing re-records: only the parent's layout changes
        surface.PumpUi(); // only a frame the change requested runs (no forced one)
        Assert.Equal(100, cell.Frame.X);
    }

    [Fact]
    public void AnInputFieldReRecordsWhenItsAdornmentResizes()
    {
        // The text area is laid out from the trailing view's size in the field's own picture.
        var counter = new SkUiCoreBox().SetColor(Colors.Blue);
        counter.SetWidth(20).SetHeight(10);
        var entry = new SkUiCoreEntry().SetTrailingView(counter);
        var host = new SkUiCoreHost().SetContent(entry);
        using var surface = new SkUiTestSurface(host, 200, 60);
        surface.Frame();
        var recorded = surface.RecordedPictures;

        counter.SetWidth(60);
        surface.Frame();
        Assert.Equal(2, surface.RecordedPictures - recorded); // the counter and the field around it
    }
}
