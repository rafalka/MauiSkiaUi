namespace MauiSkiaUi;

/// <summary>Per-layout results of <see cref="SkUiAlignStackEngine.Measure{TChildren}"/>, checked by arrange.</summary>
internal struct SkUiAlignStackState
{
    /// <summary>Whether the children were measured since the layout was created.</summary>
    public bool IsMeasured;
    /// <summary>Child count the children were measured for.</summary>
    public int Count;
    /// <summary>Content width the children were measured for (infinite when unconstrained).</summary>
    public double AvailableWidth;
    /// <summary>Content width of the children plus spacing.</summary>
    public double TotalWidth;
    /// <summary>Content height the children were measured for (infinite when unconstrained).</summary>
    public double AvailableHeight;
    /// <summary>Height of the tallest child.</summary>
    public double Height;
}

/// <summary>
/// Horizontal align stack shared by <see cref="SkUiHorizontalAlignStackLayout"/> and
/// <see cref="Core.SkUiCoreHorizontalAlignStackLayout"/>. Children are grouped by their horizontal alignment, not by their place
/// in the list: Start children are stacked from the start side, End children from the end side (in list order, the last one at
/// the end), and Center and Fill children in a row placed by the <see cref="SkUiCenterStrategy"/>. The start and end children are
/// measured first; the centered ones get the width left (a title truncates). Every child gets the full content height and
/// aligns itself vertically in it.
/// </summary>
internal static class SkUiAlignStackEngine
{
    private const double Tolerance = 0.01;

    /// <summary>Whether <paramref name="arranged"/> space differs from what was measured for (unconstrained: the content no longer fits).</summary>
    private static bool Differs(double arranged, double measured, double content) =>
        double.IsPositiveInfinity(measured) ? arranged + Tolerance < content : Math.Abs(arranged - measured) > Tolerance;

    public static Size Measure<TChildren>(ref TChildren children, ref SkUiAlignStackState state, SkUiCenterStrategy strategy,
        double widthConstraint, double heightConstraint, Thickness padding, double spacing)
        where TChildren : struct, ISkUiLayoutChildren
    {
        var count = children.Count;
        var availableWidth = Math.Max(0, widthConstraint - padding.HorizontalThickness);
        var contentHeight = Math.Max(0, heightConstraint - padding.VerticalThickness);
        var visible = 0;
        for (var index = 0; index < count; index++)
        {
            if (children.IsVisible(index))
                visible++;
        }

        // The sides first, then the centered children in what is left.
        var remaining = availableWidth - (visible > 1 ? spacing * (visible - 1) : 0);
        double height = 0;
        var start = MeasureGroup(ref children, LayoutAlignment.Start, ref remaining, contentHeight, spacing, ref height);
        var end = MeasureGroup(ref children, LayoutAlignment.End, ref remaining, contentHeight, spacing, ref height);
        var center = MeasureGroup(ref children, LayoutAlignment.Center, ref remaining, contentHeight, spacing, ref height);

        var groups = (start.Count > 0 ? 1 : 0) + (center.Count > 0 ? 1 : 0) + (end.Count > 0 ? 1 : 0);
        var total = start.Width + center.Width + end.Width + (groups > 1 ? spacing * (groups - 1) : 0);
        var width = total;
        if (center.Count > 0 && Resolve(strategy, start.Width, end.Width) != SkUiCenterStrategy.AvailableSpace)
        {
            // Centered in the layout: as wide as the wider side on both sides of the centered children, as far as the width allows.
            var side = Math.Max(start.Width + (start.Count > 0 ? spacing : 0), end.Width + (end.Count > 0 ? spacing : 0));
            width = Math.Max(total, Math.Min(center.Width + 2 * side, availableWidth));
        }

        state.IsMeasured = true;
        state.Count = count;
        state.AvailableWidth = availableWidth;
        state.TotalWidth = total;
        state.AvailableHeight = contentHeight;
        state.Height = height;
        return new Size(width + padding.HorizontalThickness, height + padding.VerticalThickness);
    }

    public static void Arrange<TChildren>(ref TChildren children, ref SkUiAlignStackState state, SkUiCenterStrategy strategy,
        Size size, Thickness padding, double spacing)
        where TChildren : struct, ISkUiLayoutChildren
    {
        var availableWidth = Math.Max(0, size.Width - padding.HorizontalThickness);
        var availableHeight = Math.Max(0, size.Height - padding.VerticalThickness);
        // Arranged in a different size than measured (or never measured): measure again for the real space, so the centered
        // children get what is left there and height-dependent children (wrapping, aspect-fit) fit the height. An unconstrained
        // measure stays valid as long as its content fits.
        var stale = !state.IsMeasured || state.Count != children.Count
            || Differs(availableWidth, state.AvailableWidth, state.TotalWidth)
            || Differs(availableHeight, state.AvailableHeight, state.Height);
        if (stale)
            Measure(ref children, ref state, strategy, size.Width, size.Height, padding, spacing);

        var count = children.Count;
        var top = padding.Top;
        var height = availableHeight;
        var left = padding.Left;
        var right = left + availableWidth;

        // Start children, from the start side.
        var x = left;
        var startCount = 0;
        for (var index = 0; index < count; index++)
        {
            if (!children.IsVisible(index))
            {
                children.ArrangeHidden(index);
                continue;
            }
            if (Group(children.HorizontalAlignment(index)) != LayoutAlignment.Start)
                continue;
            if (startCount++ > 0)
                x += spacing;
            var width = Math.Max(0, Math.Min(children.DesiredSize(index).Width, right - x));
            children.Arrange(index, new Rect(x, top, width, height));
            x += width;
        }
        var regionLeft = startCount > 0 ? Math.Min(x + spacing, right) : left;

        // End children, from the end side: the last one at the end.
        x = right;
        var endCount = 0;
        for (var index = count - 1; index >= 0; index--)
        {
            if (!children.IsVisible(index) || Group(children.HorizontalAlignment(index)) != LayoutAlignment.End)
                continue;
            if (endCount++ > 0)
                x -= spacing;
            var width = Math.Max(0, Math.Min(children.DesiredSize(index).Width, x - regionLeft));
            x -= width;
            children.Arrange(index, new Rect(x, top, width, height));
        }
        var regionRight = Math.Max(regionLeft, endCount > 0 ? x - spacing : right);

        // Centered children, in a row placed by the strategy and kept between the sides.
        double centerWidth = 0;
        var centerCount = 0;
        for (var index = 0; index < count; index++)
        {
            if (!children.IsVisible(index) || Group(children.HorizontalAlignment(index)) != LayoutAlignment.Center)
                continue;
            centerWidth += children.DesiredSize(index).Width + (centerCount++ > 0 ? spacing : 0);
        }
        if (centerCount == 0)
            return;
        var inParent = left + (availableWidth - centerWidth) / 2;
        var inSpace = (regionLeft + regionRight - centerWidth) / 2;
        x = Resolve(strategy, regionLeft - left - (startCount > 0 ? spacing : 0), right - regionRight - (endCount > 0 ? spacing : 0)) switch
        {
            SkUiCenterStrategy.AvailableSpace => inSpace,
            SkUiCenterStrategy.ParentOrAvailableSpace => inParent + Tolerance >= regionLeft && inParent + centerWidth <= regionRight + Tolerance ? inParent : inSpace,
            _ => inParent
        };
        x = Math.Max(regionLeft, Math.Min(x, regionRight - centerWidth));
        centerCount = 0;
        for (var index = 0; index < count; index++)
        {
            if (!children.IsVisible(index) || Group(children.HorizontalAlignment(index)) != LayoutAlignment.Center)
                continue;
            if (centerCount++ > 0)
                x += spacing;
            var width = Math.Max(0, Math.Min(children.DesiredSize(index).Width, regionRight - x));
            children.Arrange(index, new Rect(x, top, width, height));
            x += width;
        }
    }

    /// <summary>The group of a child: Start, End, or Center (for Center and Fill).</summary>
    private static LayoutAlignment Group(LayoutAlignment alignment) =>
        alignment is LayoutAlignment.Start or LayoutAlignment.End ? alignment : LayoutAlignment.Center;

    /// <summary>The strategy for sides as wide as <paramref name="startWidth"/> and <paramref name="endWidth"/>.</summary>
    private static SkUiCenterStrategy Resolve(SkUiCenterStrategy strategy, double startWidth, double endWidth) =>
        strategy != SkUiCenterStrategy.AvailableSpaceWhenBothSides ? strategy
        : startWidth > Tolerance && endWidth > Tolerance ? SkUiCenterStrategy.AvailableSpace
        : SkUiCenterStrategy.ParentOrAvailableSpace;

    /// <summary>
    /// Measures the visible children of <paramref name="group"/> in list order, each in the width still
    /// <paramref name="remaining"/>. Returns the group's width (with the spacing between its children) and its child count.
    /// </summary>
    private static (double Width, int Count) MeasureGroup<TChildren>(ref TChildren children, LayoutAlignment group, ref double remaining,
        double contentHeight, double spacing, ref double height)
        where TChildren : struct, ISkUiLayoutChildren
    {
        double width = 0;
        var count = 0;
        for (var index = 0; index < children.Count; index++)
        {
            if (!children.IsVisible(index) || Group(children.HorizontalAlignment(index)) != group)
                continue;
            // A child is arranged no wider than the width left: it counts that much (a label's least width, a character, does not
            // push the row past its constraint). One with no width left is still measured (its desired size stays current), but
            // its height does not count either, or a wrapping label measured at 0 (a character per line) would make the row that tall.
            var room = Math.Max(0, remaining);
            var size = children.Measure(index, room, contentHeight);
            var used = Math.Min(size.Width, room);
            remaining -= used;
            width += used + (count++ > 0 ? spacing : 0);
            if (room > Tolerance)
                height = Math.Max(height, size.Height);
        }
        return (width, count);
    }
}
