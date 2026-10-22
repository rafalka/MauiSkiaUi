using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// MAUI's child APIs on drawn layouts (U1 in docs/archive/ImplementationPlan-1.0.md): <c>Layout.Add</c> / <c>Insert</c> / <c>Remove</c> /
/// <c>Clear</c>, MAUI's <c>GridExtensions</c> and Grid's per-view members, so code-behind ports by renaming the type; and
/// the Core twins.
/// </summary>
public class LayoutChildApiTests
{
    [Fact]
    public void LayoutMembersEditChildrenAsMauisLayout()
    {
        var a = new SkUiBox();
        var b = new SkUiBox();
        var c = new SkUiBox();
        var stack = new SkUiVerticalStackLayout { a, c };

        stack.Insert(1, b);
        Assert.Equal([a, b, c], stack.Children);
        Assert.Equal(3, stack.Count);
        Assert.Same(b, stack[1]);
        Assert.Equal(2, stack.IndexOf(c));
        var contains = stack.Contains(a);
        Assert.True(contains);
        Assert.Same(stack, a.Parent);

        Assert.True(stack.Remove(b));
        Assert.False(stack.Remove(b));
        Assert.Null(b.Parent);
        stack.RemoveAt(1);
        Assert.Equal([a], stack.Children);

        // As MAUI's Layout: null is ignored, and so is an index past the end.
        stack.Add(null!);
        stack.Insert(0, null!);
        stack.RemoveAt(5);
        Assert.Equal([a], stack.Children);

        stack.Clear();
        Assert.Empty(stack.Children);
        Assert.Null(a.Parent);
    }

    [Fact]
    public void LayoutMembersRejectNativeViews()
    {
        var stack = new SkUiHorizontalStackLayout();
        Assert.Throws<ArgumentException>(() => stack.Add(new Label()));
        Assert.Throws<ArgumentException>(() => stack.Insert(0, new Label()));
        Assert.False(stack.Remove(new Label()));
        Assert.Equal(-1, stack.IndexOf(new Label()));
    }

    [Fact]
    public void AddRangeInvalidatesOnce()
    {
        var stack = new SkUiVerticalStackLayout();
        var host = new SkUiContentView { Content = stack };
        SkUiTestHelpers.Arrange(host, 100, 100);
        var invalidations = 0;
        host.LayoutInvalidated += (_, _) => invalidations++;

        stack.AddRange([new SkUiBox(), new SkUiBox(), new SkUiBox()]);

        Assert.Equal(3, stack.Count);
        Assert.Equal(1, invalidations);
    }

    /// <summary>The same calls on MAUI's Grid and SkUiGrid give the same tracks and placements.</summary>
    [Fact]
    public void GridAddMatchesMauisGridExtensions()
    {
        var maui = new Grid();
        var drawn = new SkUiGrid();
        Build(maui, (view, column, row) => maui.Add(view, column, row), (view, row, column, rowSpan, columnSpan) => maui.AddWithSpan(view, row, column, rowSpan, columnSpan),
            () => new Label());
        Build(drawn, (view, column, row) => drawn.Add(view, column, row), (view, row, column, rowSpan, columnSpan) => drawn.AddWithSpan(view, row, column, rowSpan, columnSpan),
            () => new SkUiLabel());

        Assert.Equal(maui.RowDefinitions.Count, drawn.RowDefinitions.Count);
        Assert.Equal(maui.ColumnDefinitions.Count, drawn.ColumnDefinitions.Count);
        Assert.Equal(Placements(maui, maui.Children), Placements(drawn, drawn.Children));
        Assert.Equal([(1, 0, 1, 1), (0, 0, 1, 1), (2, 1, 1, 1), (1, 2, 2, 3)], Placements(drawn, drawn.Children));
        Assert.Equal((3, 5), (drawn.RowDefinitions.Count, drawn.ColumnDefinitions.Count));
        return;

        static void Build(IGridLayout grid, Action<IView, int, int> add, Action<IView, int, int, int, int> addWithSpan, Func<View> create)
        {
            // Placed before it is added: Add(view) keeps the cell, as MAUI's Layout.Add.
            var placed = create();
            Grid.SetRow(placed, 1);
            ((IList<IView>)grid).Add(placed);
            add(create(), 0, 0);
            add(create(), 1, 2);
            addWithSpan(create(), 1, 2, 2, 3);
        }

        static List<(int Row, int Column, int RowSpan, int ColumnSpan)> Placements(IGridLayout grid, IEnumerable<IView> children) =>
            children.Select(view => (grid.GetRow(view), grid.GetColumn(view), grid.GetRowSpan(view), grid.GetColumnSpan(view))).ToList();
    }

    [Fact]
    public void GridAddLaysOutInTheCell()
    {
        var grid = new SkUiGrid().AddColumnDefinition(new ColumnDefinition(40)).AddColumnDefinition(new ColumnDefinition(60))
            .AddRowDefinition(new RowDefinition(20)).AddRowDefinition(new RowDefinition(30));
        var box = new SkUiBox();
        grid.Add(box, 1, 1);
        SkUiTestHelpers.Arrange(grid, 100, 50);
        Assert.Equal(new Rect(40, 20, 60, 30), box.Frame);

        grid.SetColumn(box, 0).SetRowSpan(box, 2);
        SkUiTestHelpers.Arrange(grid, 100, 50);
        Assert.Equal(new Rect(0, 20, 40, 30), box.Frame);
        Assert.Equal(0, grid.GetColumn(box));
        Assert.Equal(1, grid.GetRow(box));
        Assert.Equal(2, grid.GetRowSpan(box));
        Assert.Equal(1, grid.GetColumnSpan(box));
    }

    [Fact]
    public void GridAddValidatesAsMaui()
    {
        var grid = new SkUiGrid();
        Assert.Throws<ArgumentNullException>(() => grid.Add(null!, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.Add(new SkUiBox(), -1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.AddWithSpan(new SkUiBox(), rowSpan: 0));
        Assert.Throws<ArgumentException>(() => grid.Add(new Label(), 0, 0));
        Assert.Empty(grid.Children);
    }

    [Fact]
    public void CorePanelInsertsAndRemovesAtIndexes()
    {
        var a = new SkUiCoreBox();
        var b = new SkUiCoreBox();
        var c = new SkUiCoreBox();
        var stack = new SkUiCoreVerticalStackLayout().Add(a).Add(c);

        stack.Insert(1, b);
        Assert.Equal([a, b, c], stack.Children);
        Assert.Equal(1, stack.IndexOf(b));
        Assert.Same(stack, b.Parent);

        stack.RemoveAt(0);
        Assert.Equal([b, c], stack.Children);
        Assert.Null(a.Parent);
        Assert.Equal(-1, stack.IndexOf(a));
        Assert.Throws<ArgumentOutOfRangeException>(() => stack.RemoveAt(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => stack.Insert(3, a));
    }

    [Fact]
    public void CoreGridReadsPlacements()
    {
        var grid = new SkUiCoreGrid();
        var box = new SkUiCoreBox();
        grid.Add(box, 1, 2, 3, 4);
        Assert.Equal(1, grid.GetRow(box));
        Assert.Equal(2, grid.GetColumn(box));
        Assert.Equal(3, grid.GetRowSpan(box));
        Assert.Equal(4, grid.GetColumnSpan(box));
    }
}
