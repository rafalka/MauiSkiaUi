namespace MauiSkiaUi;

/// <summary>What <see cref="SkUiWrapEngine.Measure{TChildren}"/> measured the children for, so arrange can tell whether it is stale.</summary>
internal struct SkUiWrapState
{
    /// <summary>Whether a measure ran since the layout was created.</summary>
    public bool Measured;
    /// <summary>Child count at the last measure.</summary>
    public int Count;
    /// <summary>Content width the children were measured against (infinite when unconstrained).</summary>
    public double ContentWidth;
    /// <summary>Widest visible child at the last measure.</summary>
    public double MaxChildWidth;
}

/// <summary>
/// Wrap layout shared by <see cref="SkUiWrapLayout"/> and <see cref="Core.SkUiCoreWrapLayout"/>: children flow left
/// to right and wrap onto a new row when the next one does not fit. Each child gets a slot as tall as its row, so
/// vertical alignment works inside the row. After hartez/CustomLayoutExamples, with children measured against the
/// content width and a child that starts a row never wrapping (no empty rows).
/// </summary>
internal static class SkUiWrapEngine
{
    // Absorbs floating-point rounding between measure and arrange, so a row that fits never wraps.
    private const double Tolerance = 0.01;

    public static Size Measure<TChildren>(ref TChildren children, ref SkUiWrapState state, double widthConstraint, Thickness padding, double spacing, double rowSpacing)
        where TChildren : struct, ISkUiLayoutChildren
    {
        var contentWidth = Math.Max(0, widthConstraint - padding.HorizontalThickness);
        double x = 0, rowTop = 0, rowHeight = 0, maxRowWidth = 0, maxChildWidth = 0;
        var rowHasItems = false;
        for (var index = 0; index < children.Count; index++)
        {
            if (!children.IsVisible(index))
                continue;
            var size = children.Measure(index, contentWidth, double.PositiveInfinity);
            maxChildWidth = Math.Max(maxChildWidth, size.Width);
            if (rowHasItems && x + spacing + size.Width > contentWidth + Tolerance)
            {
                maxRowWidth = Math.Max(maxRowWidth, x);
                rowTop += rowHeight + rowSpacing;
                x = 0;
                rowHeight = 0;
                rowHasItems = false;
            }
            if (rowHasItems)
                x += spacing;
            x += size.Width;
            rowHeight = Math.Max(rowHeight, size.Height);
            rowHasItems = true;
        }
        maxRowWidth = Math.Max(maxRowWidth, x);
        state.Measured = true;
        state.Count = children.Count;
        state.ContentWidth = contentWidth;
        state.MaxChildWidth = maxChildWidth;
        return new Size(maxRowWidth + padding.HorizontalThickness, rowTop + rowHeight + padding.VerticalThickness);
    }

    public static void Arrange<TChildren>(ref TChildren children, ref SkUiWrapState state, Size size, Thickness padding, double spacing, double rowSpacing)
        where TChildren : struct, ISkUiLayoutChildren
    {
        var contentWidth = Math.Max(0, size.Width - padding.HorizontalThickness);
        // Arranged at another width than measured (e.g. measured wide, then given a narrower slot): width-sensitive
        // children (wrapping labels) must be measured again for the real width, or rows and row heights are wrong.
        // An unconstrained measure stays valid while every child still fits at its natural width.
        var stale = !state.Measured || state.Count != children.Count
            || (double.IsPositiveInfinity(state.ContentWidth)
                ? contentWidth + Tolerance < state.MaxChildWidth
                : Math.Abs(contentWidth - state.ContentWidth) > Tolerance);
        if (stale)
            Measure(ref children, ref state, size.Width, padding, spacing, rowSpacing);
        var rowStart = 0;
        var rowTop = padding.Top;
        double x = 0, rowHeight = 0;
        var rowHasItems = false;
        for (var index = 0; index < children.Count; index++)
        {
            if (!children.IsVisible(index))
                continue;
            var desired = children.DesiredSize(index);
            if (rowHasItems && x + spacing + desired.Width > contentWidth + Tolerance)
            {
                ArrangeRow(ref children, rowStart, index, padding.Left, rowTop, rowHeight, spacing);
                rowTop += rowHeight + rowSpacing;
                rowStart = index;
                x = 0;
                rowHeight = 0;
                rowHasItems = false;
            }
            if (rowHasItems)
                x += spacing;
            x += desired.Width;
            rowHeight = Math.Max(rowHeight, desired.Height);
            rowHasItems = true;
        }
        ArrangeRow(ref children, rowStart, children.Count, padding.Left, rowTop, rowHeight, spacing);
    }

    private static void ArrangeRow<TChildren>(ref TChildren children, int start, int end, double left, double top, double height, double spacing)
        where TChildren : struct, ISkUiLayoutChildren
    {
        var x = left;
        for (var index = start; index < end; index++)
        {
            if (!children.IsVisible(index))
            {
                children.ArrangeHidden(index);
                continue;
            }
            var width = children.DesiredSize(index).Width;
            children.Arrange(index, new Rect(x, top, width, height));
            x += width + spacing;
        }
    }
}
