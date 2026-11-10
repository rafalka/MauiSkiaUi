namespace MauiSkiaUi;

/// <summary>The tile layout's options, as both layers hold them.</summary>
internal readonly record struct SkUiTileOptions(
    double TileSize, double MinimumTileSize, double AspectRatio, double ColumnSpacing, double RowSpacing, int MaximumRows, Thickness Padding);

/// <summary>What <see cref="SkUiTileEngine.Measure{TChildren}"/> worked out, for arrange and the layers.</summary>
internal struct SkUiTileState
{
    /// <summary>Whether a measure ran since the layout was created or its options changed.</summary>
    public bool Measured;
    /// <summary>Child count at the last measure.</summary>
    public int Count;
    /// <summary>Content width the tiles were computed for (infinite when unconstrained).</summary>
    public double ContentWidth;
    /// <summary>Width the tiles and their gaps take (at most <see cref="ContentWidth"/>).</summary>
    public double UsedWidth;
    /// <summary>Columns of the grid of tiles.</summary>
    public int Columns;
    /// <summary>Size of a tile.</summary>
    public double TileWidth, TileHeight;
    /// <summary>Children before this index can be shown; from it on, they are past <see cref="SkUiTileOptions.MaximumRows"/>.</summary>
    public int ShownEnd;
    /// <summary>Visible children past <see cref="SkUiTileOptions.MaximumRows"/>: the overflow tile's count (0: no overflow).</summary>
    public int HiddenCount;
    /// <summary>The last shown tile, where the overflow tile goes (in the layout's coordinates).</summary>
    public Rect LastTile;

    /// <summary>Whether the child at <paramref name="index"/> has a tile (it may still be invisible).</summary>
    public readonly bool IsShown(int index) => index < ShownEnd;
}

/// <summary>
/// Tile layout shared by <see cref="SkUiTileLayout"/> and <see cref="Core.SkUiCoreTileLayout"/>: equal tiles in rows, as many
/// columns of at most <see cref="SkUiTileOptions.TileSize"/> as the width takes. The tiles stretch to fill the row but never
/// past the tile size (what is left stays at the row's end), and a column is dropped when the tiles would be smaller than
/// the minimum. With a row limit, the visible children past it get no tile and are neither measured nor drawn; the last
/// tile then holds the overflow tile, laid out by the layer.
/// </summary>
internal static class SkUiTileEngine
{
    // Absorbs floating-point rounding between measure and arrange, so a width that fits n tiles never drops one.
    private const double Tolerance = 0.01;

    public static Size Measure<TChildren>(ref TChildren children, ref SkUiTileState state, double widthConstraint, in SkUiTileOptions options)
        where TChildren : struct, ISkUiLayoutChildren
    {
        var padding = options.Padding;
        var contentWidth = Math.Max(0, widthConstraint - padding.HorizontalThickness);
        var visible = 0;
        for (var index = 0; index < children.Count; index++)
            if (children.IsVisible(index))
                visible++;

        var (columns, tile) = Columns(contentWidth, visible, options);
        var tileHeight = tile / options.AspectRatio;
        var capacity = options.MaximumRows > 0 ? (long)options.MaximumRows * columns : long.MaxValue;
        var shown = (int)Math.Min(visible, capacity);

        var shownEnd = 0;
        var placed = 0;
        for (var index = 0; index < children.Count && placed < shown; index++)
        {
            shownEnd = index + 1;
            if (!children.IsVisible(index))
                continue;
            children.Measure(index, tile, tileHeight);
            placed++;
        }
        if (placed == shown)
        {
            // Invisible children right after the last shown one take no tile either way: keep them with the shown ones.
            while (shownEnd < children.Count && !children.IsVisible(shownEnd))
                shownEnd++;
        }

        var rows = shown == 0 ? 0 : (shown + columns - 1) / columns;
        var usedColumns = Math.Min(columns, shown);
        var usedWidth = usedColumns == 0 ? 0 : usedColumns * tile + (usedColumns - 1) * options.ColumnSpacing;
        var height = rows == 0 ? 0 : rows * tileHeight + (rows - 1) * options.RowSpacing;

        state.Measured = true;
        state.Count = children.Count;
        state.ContentWidth = contentWidth;
        state.UsedWidth = usedWidth;
        state.Columns = columns;
        state.TileWidth = tile;
        state.TileHeight = tileHeight;
        state.ShownEnd = shownEnd;
        state.HiddenCount = visible - shown;
        state.LastTile = shown == 0 ? Rect.Zero : Tile(shown - 1, state, options);
        return new Size(usedWidth + padding.HorizontalThickness, height + padding.VerticalThickness);
    }

    public static void Arrange<TChildren>(ref TChildren children, ref SkUiTileState state, Size size, in SkUiTileOptions options)
        where TChildren : struct, ISkUiLayoutChildren
    {
        var contentWidth = Math.Max(0, size.Width - options.Padding.HorizontalThickness);
        // Arranged at a width the measured tiles do not fit (narrower than they take, or wider than they were computed
        // for, where more or larger tiles fit): computed again for the real width. Unconstrained: one row of every tile.
        var stale = !state.Measured || state.Count != children.Count
            || contentWidth + Tolerance < state.UsedWidth
            || (!double.IsPositiveInfinity(state.ContentWidth) && contentWidth > state.ContentWidth + Tolerance);
        if (stale)
            Measure(ref children, ref state, size.Width, options);
        var tile = 0;
        for (var index = 0; index < state.ShownEnd; index++)
        {
            if (!children.IsVisible(index))
            {
                children.ArrangeHidden(index);
                continue;
            }
            children.Arrange(index, Tile(tile++, state, options));
        }
    }

    /// <summary>The columns and the tile size for a content width (see the class remarks).</summary>
    internal static (int Columns, double Tile) Columns(double contentWidth, int visible, in SkUiTileOptions options)
    {
        var spacing = options.ColumnSpacing;
        if (double.IsPositiveInfinity(contentWidth))
            return (Math.Max(1, visible), options.TileSize);
        // The fewest columns whose tiles are at most the tile size, so the tiles are as large as they may be.
        var columns = Math.Max(1, (int)Math.Ceiling((contentWidth + spacing - Tolerance) / (options.TileSize + spacing)));
        var tile = TileWidth(contentWidth, columns, spacing);
        while (columns > 1 && tile < options.MinimumTileSize - Tolerance)
            tile = TileWidth(contentWidth, --columns, spacing);
        return (columns, Math.Max(0, Math.Min(tile, options.TileSize)));
    }

    private static double TileWidth(double contentWidth, int columns, double spacing) => (contentWidth - (columns - 1) * spacing) / columns;

    private static Rect Tile(int tile, in SkUiTileState state, in SkUiTileOptions options)
    {
        var column = tile % state.Columns;
        var row = tile / state.Columns;
        return new Rect(
            options.Padding.Left + column * (state.TileWidth + options.ColumnSpacing),
            options.Padding.Top + row * (state.TileHeight + options.RowSpacing),
            state.TileWidth, state.TileHeight);
    }
}
