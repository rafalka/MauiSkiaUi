namespace MauiSkiaUi.Core;

/// <summary>
/// Controls which track background wins at row/column intersections in <see cref="SkUiCoreTable"/>.
/// The second layer in the name paints on top (covers the first at overlaps).
/// </summary>
public enum SkUiCoreTableTrackBackgroundOrder
{
    /// <summary>Paint rows first, then columns — column fills cover row fills (default).</summary>
    ColumnsOverRows = 0,

    /// <summary>Paint columns first, then rows — row fills cover column fills.</summary>
    RowsOverColumns = 1
}
