namespace MauiSkiaUi;

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

    public static Size Measure<TChildren>(ref TChildren children, double widthConstraint, Thickness padding, double spacing, double rowSpacing)
        where TChildren : struct, ISkUiLayoutChildren
    {
        var contentWidth = Math.Max(0, widthConstraint - padding.HorizontalThickness);
        double x = 0, rowTop = 0, rowHeight = 0, maxRowWidth = 0;
        var rowHasItems = false;
        for (var index = 0; index < children.Count; index++)
        {
            if (!children.IsVisible(index))
                continue;
            var size = children.Measure(index, contentWidth, double.PositiveInfinity);
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
        return new Size(maxRowWidth + padding.HorizontalThickness, rowTop + rowHeight + padding.VerticalThickness);
    }

    public static void Arrange<TChildren>(ref TChildren children, Size size, Thickness padding, double spacing, double rowSpacing)
        where TChildren : struct, ISkUiLayoutChildren
    {
        var contentWidth = Math.Max(0, size.Width - padding.HorizontalThickness);
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
