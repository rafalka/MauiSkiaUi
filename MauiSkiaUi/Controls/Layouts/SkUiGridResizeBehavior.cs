namespace MauiSkiaUi;

/// <summary>Which tracks a grid splitter (<see cref="SkUiGridSplitter"/>, <see cref="Core.SkUiCoreGridSplitter"/>) resizes.</summary>
public enum SkUiGridResizeBehavior
{
    /// <summary>
    /// From the splitter's alignment along the resize direction (<c>HorizontalOptions</c> for columns): at the end of its
    /// track, <see cref="CurrentAndNext"/>; at the start, <see cref="PreviousAndCurrent"/>; otherwise (a splitter in a track of
    /// its own) <see cref="PreviousAndNext"/>.
    /// </summary>
    BasedOnAlignment,

    /// <summary>The tracks before and after the splitter's track (the splitter has a row or column of its own).</summary>
    PreviousAndNext,

    /// <summary>The splitter's track and the one after it (a splitter at the end of a cell, as in a table header).</summary>
    CurrentAndNext,

    /// <summary>The track before the splitter's and the splitter's track (a splitter at the start of a cell).</summary>
    PreviousAndCurrent,

    /// <summary>Only the splitter's track: it becomes absolute and the grid grows or shrinks (a column in a scrolling table).</summary>
    CurrentOnly
}
