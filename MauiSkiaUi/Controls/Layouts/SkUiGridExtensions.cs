namespace MauiSkiaUi;

/// <summary>
/// MAUI's <c>GridExtensions</c> for <see cref="SkUiGrid"/>, so <c>grid.Add(view, column, row)</c> and
/// <c>grid.AddWithSpan(…)</c> code-behind ports by renaming <c>Grid</c> to <c>SkUiGrid</c>.
/// </summary>
public static class SkUiGridExtensions
{
    /// <summary>
    /// Adds a drawn view at <paramref name="column"/> and <paramref name="row"/>, spanning one cell; star rows and columns
    /// are appended when the grid has too few for that cell.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="view"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="row"/> or <paramref name="column"/> is negative.</exception>
    public static void Add(this SkUiGrid grid, IView view, int column = 0, int row = 0) =>
        grid.AddWithSpan(view, row, column);

    /// <summary>
    /// Adds a drawn view at <paramref name="row"/> and <paramref name="column"/>, spanning <paramref name="rowSpan"/> rows and
    /// <paramref name="columnSpan"/> columns; star rows and columns are appended when the grid has too few for the span.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="view"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="row"/> or <paramref name="column"/> is negative, or a span is less than 1.
    /// </exception>
    public static void AddWithSpan(this SkUiGrid grid, IView view, int row = 0, int column = 0, int rowSpan = 1, int columnSpan = 1)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        ArgumentOutOfRangeException.ThrowIfLessThan(rowSpan, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(columnSpan, 1);

        // One re-measure for the child, its placement and any new tracks.
        grid.StartUpdating();
        try
        {
            grid.Add(view);
            // As MAUI: a single cell never adds tracks (an empty collection is one star track).
            if (row + rowSpan > 1)
                for (var count = grid.RowDefinitions.Count; count < row + rowSpan; count++)
                    grid.RowDefinitions.Add(new RowDefinition());
            grid.SetRow(view, row).SetRowSpan(view, rowSpan);
            if (column + columnSpan > 1)
                for (var count = grid.ColumnDefinitions.Count; count < column + columnSpan; count++)
                    grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.SetColumn(view, column).SetColumnSpan(view, columnSpan);
        }
        finally
        {
            grid.EndUpdating();
        }
    }
}
