using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>Layout/geometry tests for <see cref="SkUiCoreTable"/> chrome helpers.</summary>
public class CoreTableLayoutTests
{
    [Fact]
    public void RowAndColumnBackgroundRects_MatchTrackMetrics()
    {
        var table = CreateThreeByTwoTable();
        table.Measure(300, 200);
        table.Arrange(new Rect(0, 0, 300, 200));

        var row0 = table.GetRowBackgroundRect(0);
        Assert.Equal(table.Padding.Left, row0.X, 1);
        Assert.Equal(table.GetRowOffset(0), row0.Y, 1);
        Assert.Equal(table.GetRowHeight(0), row0.Height, 1);
        Assert.Equal(300 - table.Padding.HorizontalThickness, row0.Width, 1);

        var col1 = table.GetColumnBackgroundRect(1);
        Assert.Equal(table.GetColumnOffset(1), col1.X, 1);
        Assert.Equal(table.Padding.Top, col1.Y, 1);
        Assert.Equal(table.GetColumnWidth(1), col1.Width, 1);
        Assert.Equal(200 - table.Padding.VerticalThickness, col1.Height, 1);
    }

    [Fact]
    public void SeparatorsWithoutSpans_AreFullTrackBoundaries()
    {
        var table = CreateThreeByTwoTable();
        table.SetRowSeparatorThickness(2).SetColumnSeparatorThickness(3);
        table.Measure(300, 200);
        table.Arrange(new Rect(0, 0, 300, 200));

        var rowSep = table.GetRowSeparatorSegments(0);
        Assert.Single(rowSep);
        Assert.Equal(table.GetRowOffset(0) + table.GetRowHeight(0), rowSep[0].Y, 1);
        Assert.Equal(300 - table.Padding.HorizontalThickness, rowSep[0].Width, 1);
        Assert.Equal(2, rowSep[0].Height, 1);

        var colSep = table.GetColumnSeparatorSegments(0);
        Assert.Single(colSep);
        Assert.Equal(table.GetColumnOffset(0) + table.GetColumnWidth(0), colSep[0].X, 1);
        Assert.Equal(3, colSep[0].Width, 1);
        Assert.Equal(200 - table.Padding.VerticalThickness, colSep[0].Height, 1);
    }

    [Fact]
    public void ColumnSpan_OmitsVerticalSeparatorThroughMergedCell()
    {
        var table = new SkUiCoreTable()
            .SetPadding(new Thickness(0))
            .SetColumnDefinitions([
                new SkUiCoreColumnDefinition(new SkUiCoreGridLength(100)),
                new SkUiCoreColumnDefinition(new SkUiCoreGridLength(100)),
                new SkUiCoreColumnDefinition(new SkUiCoreGridLength(100))
            ])
            .SetRowDefinitions([
                new SkUiCoreRowDefinition(new SkUiCoreGridLength(40)),
                new SkUiCoreRowDefinition(new SkUiCoreGridLength(40))
            ]) as SkUiCoreTable;

        Assert.NotNull(table);
        var header = new SkUiCoreBox().SetWidth(10).SetHeight(10);
        var cell = new SkUiCoreBox().SetWidth(10).SetHeight(10);
        table!.Add(header, 0, 0, columnSpan: 2); // merges col 0-1 on row 0
        table.Add(cell, 1, 0);

        table.SetColumnSeparatorThickness(1);
        table.Measure(300, 80);
        table.Arrange(new Rect(0, 0, 300, 80));

        var segments = table.GetColumnSeparatorSegments(0);
        // Vertical line after col 0 should skip row 0 (merged) but keep row 1.
        Assert.NotEmpty(segments);
        Assert.All(segments, s => Assert.True(s.Y >= table.GetRowOffset(1) - 0.1));
        Assert.DoesNotContain(segments, s => s.Y < table.GetRowOffset(1) && s.Bottom > table.GetRowOffset(0) + 1);
    }

    [Fact]
    public void RowSpan_OmitsHorizontalSeparatorThroughMergedCell()
    {
        var table = new SkUiCoreTable()
            .SetPadding(new Thickness(0))
            .SetColumnDefinitions([
                new SkUiCoreColumnDefinition(new SkUiCoreGridLength(50)),
                new SkUiCoreColumnDefinition(new SkUiCoreGridLength(50))
            ])
            .SetRowDefinitions([
                new SkUiCoreRowDefinition(new SkUiCoreGridLength(40)),
                new SkUiCoreRowDefinition(new SkUiCoreGridLength(40))
            ]) as SkUiCoreTable;

        Assert.NotNull(table);
        var tall = new SkUiCoreBox().SetWidth(10).SetHeight(10);
        table!.Add(tall, 0, 0, rowSpan: 2);
        table.Add(new SkUiCoreBox().SetWidth(10).SetHeight(10), 0, 1);

        table.SetRowSeparatorThickness(1);
        table.Measure(100, 80);
        table.Arrange(new Rect(0, 0, 100, 80));

        var segments = table.GetRowSeparatorSegments(0);
        Assert.NotEmpty(segments);
        // Horizontal line should skip column 0.
        Assert.All(segments, s => Assert.True(s.X >= table.GetColumnOffset(1) - 0.1));
    }

    [Fact]
    public void CellBackground_FillsFullCellRectBehindCenteredContent()
    {
        var table = new SkUiCoreTable()
            .SetPadding(new Thickness(0))
            .SetColumnDefinitions([
                new SkUiCoreColumnDefinition(new SkUiCoreGridLength(100)),
                new SkUiCoreColumnDefinition(new SkUiCoreGridLength(100))
            ])
            .SetRowDefinitions([
                new SkUiCoreRowDefinition(new SkUiCoreGridLength(80))
            ]);

        var label = new SkUiCoreLabel().SetText("Hi");
        label.Margin = new Thickness(10);
        label.HorizontalAlignment = LayoutAlignment.Center;
        label.VerticalAlignment = LayoutAlignment.Center;
        table.Add(label, 0, 0, columnSpan: 2);
        table.SetCellBackground(0, 0, Colors.Orange, columnSpan: 2);

        table.Measure(200, 80);
        table.Arrange(new Rect(0, 0, 200, 80));

        Assert.Equal(new Rect(0, 0, 200, 80), table.GetCellBackgroundRect(0, 0));
        Assert.True(label.Frame.Width < 200);
        Assert.True(label.Frame.Height < 80);

        using var bitmap = new SKBitmap(200, 80);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        table.Paint(canvas);
        Assert.Equal(SKColors.Orange, bitmap.GetPixel(5, 5));
    }

    [Fact]
    public void TrackBackgroundOrder_ChangesWhichFillWinsAtIntersection()
    {
        var table = new SkUiCoreTable();
        table.SetPadding(new Thickness(0))
            .SetColumnDefinitions([
                new SkUiCoreColumnDefinition(new SkUiCoreGridLength(50)),
                new SkUiCoreColumnDefinition(new SkUiCoreGridLength(50))
            ])
            .SetRowDefinitions([
                new SkUiCoreRowDefinition(new SkUiCoreGridLength(50)),
                new SkUiCoreRowDefinition(new SkUiCoreGridLength(50))
            ]);
        // No content children — assert Background-layer fills only.
        table.SetRowBackground(0, Colors.Red);
        table.SetColumnBackground(0, Colors.Blue);
        table.Measure(100, 100);
        table.Arrange(new Rect(0, 0, 100, 100));

        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);

        table.TrackBackgroundOrder = SkUiCoreTableTrackBackgroundOrder.ColumnsOverRows;
        canvas.Clear(SKColors.White);
        table.Paint(canvas);
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(25, 25));

        table.TrackBackgroundOrder = SkUiCoreTableTrackBackgroundOrder.RowsOverColumns;
        canvas.Clear(SKColors.White);
        table.Paint(canvas);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(25, 25));
    }

    [Fact]
    public void PaintBackground_DoesNotThrow()
    {
        var table = CreateThreeByTwoTable();
        table.SetRowBackground(0, Colors.LightBlue)
            .SetColumnBackground(1, Colors.LightYellow)
            .SetRowSeparatorColor(Colors.Gray)
            .SetColumnSeparatorColor(Colors.DarkGray);
        table.Measure(300, 200);
        table.Arrange(new Rect(0, 0, 300, 200));

        using var bitmap = new SKBitmap(300, 200);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        table.Paint(canvas);
    }

    private static SkUiCoreTable CreateThreeByTwoTable()
    {
        var table = new SkUiCoreTable()
            .SetPadding(new Thickness(5))
            .SetRowSpacing(4)
            .SetColumnSpacing(6)
            .SetColumnDefinitions([
                new SkUiCoreColumnDefinition(SkUiCoreGridLength.Star),
                new SkUiCoreColumnDefinition(SkUiCoreGridLength.Star),
                new SkUiCoreColumnDefinition(SkUiCoreGridLength.Star)
            ])
            .SetRowDefinitions([
                new SkUiCoreRowDefinition(SkUiCoreGridLength.Star),
                new SkUiCoreRowDefinition(SkUiCoreGridLength.Star)
            ]) as SkUiCoreTable;
        Assert.NotNull(table);
        for (var r = 0; r < 2; r++)
        for (var c = 0; c < 3; c++)
            table!.Add(new SkUiCoreBox().SetWidth(10).SetHeight(10), r, c);
        return table!;
    }
}
