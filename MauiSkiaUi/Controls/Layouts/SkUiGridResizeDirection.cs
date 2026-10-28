namespace MauiSkiaUi;

/// <summary>What a grid splitter (<see cref="SkUiGridSplitter"/>, <see cref="Core.SkUiCoreGridSplitter"/>) resizes.</summary>
public enum SkUiGridResizeDirection
{
    /// <summary>
    /// Rows when the splitter spans every column of a grid with several rows, columns when it spans every row of a grid with
    /// several columns, otherwise by its shape (taller than wide: columns).
    /// </summary>
    Auto,

    /// <summary>The columns before and after the splitter's column.</summary>
    Columns,

    /// <summary>The rows above and below the splitter's row.</summary>
    Rows
}
