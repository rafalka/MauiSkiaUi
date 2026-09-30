using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// Core absolute layout: children are positioned by explicit bounds (absolute DIPs and/or 0–1 proportional flags).
/// Proportional X/Y match MAUI <c>AbsoluteLayoutManager</c>:
/// <c>x = proportion * (available − arrangedWidth)</c> (and the same for Y/height).
/// Child <see cref="SkUiCoreNode.HorizontalAlignment"/> / <see cref="SkUiCoreNode.VerticalAlignment"/>
/// then align within that destination (Fill by default; Center matches MAUI <c>LayoutOptions.Center</c>).
/// </summary>
public class SkUiCoreAbsoluteLayout : SkUiCorePanel
{
    /// <summary>
    /// Attached: a child's layout bounds (absolute DIPs, or 0–1 proportions per <see cref="LayoutFlagsProperty"/>).
    /// Non-positive non-proportional width / height mean the child's measured size. Default: measured size at (0,0).
    /// </summary>
    public static readonly SkUiCoreAttachedProperty<Rect> LayoutBoundsProperty = new("LayoutBounds", typeof(SkUiCoreAbsoluteLayout), Rect.Zero);
    /// <summary>Attached: which components of <see cref="LayoutBoundsProperty"/> are proportional (default none).</summary>
    public static readonly SkUiCoreAttachedProperty<SkUiCoreAbsoluteLayoutFlags> LayoutFlagsProperty = new("LayoutFlags", typeof(SkUiCoreAbsoluteLayout), SkUiCoreAbsoluteLayoutFlags.None);

    /// <inheritdoc cref="SkUiCorePanel.SetPadding" />
    public new SkUiCoreAbsoluteLayout SetPadding(Thickness value)
    {
        base.SetPadding(value);
        return this;
    }

    /// <summary>Appends a child with absolute DIP bounds and no proportional flags.</summary>
    public SkUiCoreAbsoluteLayout Add(ISkUiCoreNode child, Rect bounds) =>
        Add(child, bounds, SkUiCoreAbsoluteLayoutFlags.None);

    /// <summary>Appends a child with layout bounds and optional proportional flags.</summary>
    public SkUiCoreAbsoluteLayout Add(ISkUiCoreNode child, Rect bounds, SkUiCoreAbsoluteLayoutFlags flags)
    {
        ArgumentNullException.ThrowIfNull(child);
        // Insert first (it validates the child), then write the placement; one re-measure for both.
        StartUpdating();
        try
        {
            InsertChild(Children.Count, child);
            WritePlacement((SkUiCoreNode)child, bounds, flags);
        }
        finally
        {
            EndUpdating();
        }
        return this;
    }

    /// <summary>Appends a child placed by its own attached <see cref="LayoutBoundsProperty"/> / <see cref="LayoutFlagsProperty"/> (measured size at (0,0) unless set).</summary>
    public new SkUiCoreAbsoluteLayout Add(ISkUiCoreNode child)
    {
        base.Add(child);
        return this;
    }

    /// <summary>Updates bounds/flags for a child of this layout.</summary>
    public SkUiCoreAbsoluteLayout SetLayoutBounds(ISkUiCoreNode child, Rect bounds, SkUiCoreAbsoluteLayoutFlags flags = SkUiCoreAbsoluteLayoutFlags.None)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (child.Parent != this)
            throw new ArgumentException("Child is not in this layout.", nameof(child));
        StartUpdating();
        try
        {
            WritePlacement((SkUiCoreNode)child, bounds, flags);
        }
        finally
        {
            EndUpdating();
        }
        return this;
    }

    /// <summary>Removes a child if present; its attached placement stays on the child.</summary>
    public new bool Remove(ISkUiCoreNode child) => base.Remove(child);

    /// <summary>Removes all children; their attached placements stay on them.</summary>
    public new void Clear() => base.Clear();

    private static void WritePlacement(SkUiCoreNode node, Rect bounds, SkUiCoreAbsoluteLayoutFlags flags)
    {
        node.SetValue(LayoutBoundsProperty, bounds);
        node.SetValue(LayoutFlagsProperty, flags);
    }

    private static (Rect Bounds, SkUiCoreAbsoluteLayoutFlags Flags) ReadPlacement(ISkUiCoreNode child)
    {
        var node = (SkUiCoreNode)child;
        return (node.GetValue(LayoutBoundsProperty), node.GetValue(LayoutFlagsProperty));
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        // Match arrange: proportional children use the padded content slot, not the outer constraint.
        var slotWidth = double.IsInfinity(widthConstraint)
            ? 0
            : Math.Max(0, widthConstraint - Padding.HorizontalThickness);
        var slotHeight = double.IsInfinity(heightConstraint)
            ? 0
            : Math.Max(0, heightConstraint - Padding.VerticalThickness);
        var contentWidth = 0.0;
        var contentHeight = 0.0;

        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;
            var measureSlot = ResolveMeasureConstraints(child, slotWidth, slotHeight);
            child.Measure(measureSlot.Width, measureSlot.Height);
            var arranged = ResolveDestination(child, slotWidth, slotHeight, child.DesiredSize);
            contentWidth = Math.Max(contentWidth, arranged.Right);
            contentHeight = Math.Max(contentHeight, arranged.Bottom);
        }

        var width = double.IsInfinity(widthConstraint) ? contentWidth + Padding.HorizontalThickness
            : Math.Max(widthConstraint, contentWidth + Padding.HorizontalThickness);
        var height = double.IsInfinity(heightConstraint) ? contentHeight + Padding.VerticalThickness
            : Math.Max(0, contentHeight + Padding.VerticalThickness);
        return new Size(width, height);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        var slotWidth = Math.Max(0, size.Width - Padding.HorizontalThickness);
        var slotHeight = Math.Max(0, size.Height - Padding.VerticalThickness);
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                child.Arrange(Rect.Zero);
                continue;
            }
            var destination = ResolveDestination(child, slotWidth, slotHeight, child.DesiredSize);
            destination = new Rect(
                Padding.Left + destination.X,
                Padding.Top + destination.Y,
                destination.Width,
                destination.Height);
            if (child is SkUiCoreNode node)
                destination = AlignInSlot(destination, child.DesiredSize, node.HorizontalAlignment, node.VerticalAlignment);
            child.Arrange(destination);
        }
    }

    /// <summary>Measure constraints for a child (proportional size → fraction of the slot).</summary>
    private Size ResolveMeasureConstraints(ISkUiCoreNode child, double slotWidth, double slotHeight)
    {
        var (bounds, flags) = ReadPlacement(child);
        // Non-proportional non-positive bounds mean "auto": measure unconstrained so ResolveDestination
        // can fall back to the intrinsic DesiredSize (a zero constraint would also measure as zero).
        var width = flags.HasFlag(SkUiCoreAbsoluteLayoutFlags.Width)
            ? (slotWidth <= 0 ? double.PositiveInfinity : bounds.Width * slotWidth)
            : (bounds.Width <= 0 ? double.PositiveInfinity : bounds.Width);
        var height = flags.HasFlag(SkUiCoreAbsoluteLayoutFlags.Height)
            ? (slotHeight <= 0 ? double.PositiveInfinity : bounds.Height * slotHeight)
            : (bounds.Height <= 0 ? double.PositiveInfinity : bounds.Height);
        return new Size(Math.Max(0, width), Math.Max(0, height));
    }

    /// <summary>
    /// Resolves the absolute destination before alignment, using MAUI AbsoluteLayoutManager math:
    /// size first, then <c>position = proportion * (available − size)</c> when proportional.
    /// </summary>
    private Rect ResolveDestination(ISkUiCoreNode child, double slotWidth, double slotHeight, Size desired)
    {
        var (bounds, flags) = ReadPlacement(child);
        var width = ResolveDimension(
            flags.HasFlag(SkUiCoreAbsoluteLayoutFlags.Width), bounds.Width, slotWidth, desired.Width);
        var height = ResolveDimension(
            flags.HasFlag(SkUiCoreAbsoluteLayoutFlags.Height), bounds.Height, slotHeight, desired.Height);
        var x = flags.HasFlag(SkUiCoreAbsoluteLayoutFlags.X)
            ? bounds.X * Math.Max(0, slotWidth - width)
            : bounds.X;
        var y = flags.HasFlag(SkUiCoreAbsoluteLayoutFlags.Y)
            ? bounds.Y * Math.Max(0, slotHeight - height)
            : bounds.Y;
        return new Rect(x, y, Math.Max(0, width), Math.Max(0, height));
    }

    private static double ResolveDimension(bool proportional, double fromBounds, double available, double measured)
    {
        if (proportional && !double.IsInfinity(available))
            return fromBounds * available;
        // Core does not use MAUI's AutoSize (-1); a non-proportional bound is absolute DIPs.
        // When the bound is non-positive, fall back to the measured size (Fill/Center then apply).
        if (fromBounds <= 0)
            return measured;
        return fromBounds;
    }

    /// <summary>Applies Core alignment within an absolute destination (MAUI ComputeFrame analogue).</summary>
    internal static Rect AlignInSlot(Rect slot, Size desired, LayoutAlignment horizontal, LayoutAlignment vertical)
    {
        var width = horizontal == LayoutAlignment.Fill
            ? slot.Width
            : Math.Min(Math.Max(0, desired.Width), slot.Width);
        var height = vertical == LayoutAlignment.Fill
            ? slot.Height
            : Math.Min(Math.Max(0, desired.Height), slot.Height);
        var x = slot.X + AlignOffset(slot.Width - width, horizontal);
        var y = slot.Y + AlignOffset(slot.Height - height, vertical);
        return new Rect(x, y, width, height);
    }

    private static double AlignOffset(double free, LayoutAlignment alignment) => alignment switch
    {
        LayoutAlignment.Center => free / 2,
        LayoutAlignment.End => free,
        _ => 0
    };
}
