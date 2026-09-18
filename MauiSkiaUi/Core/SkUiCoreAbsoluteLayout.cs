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
    private readonly Dictionary<ISkUiCoreNode, (Rect Bounds, SkUiCoreAbsoluteLayoutFlags Flags)> _placements = new();

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
        InsertChild(Children.Count, child);
        _placements[child] = (bounds, flags);
        return this;
    }

    /// <summary>Updates bounds/flags for an existing child.</summary>
    public SkUiCoreAbsoluteLayout SetLayoutBounds(ISkUiCoreNode child, Rect bounds, SkUiCoreAbsoluteLayoutFlags flags = SkUiCoreAbsoluteLayoutFlags.None)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (!_placements.ContainsKey(child))
            throw new ArgumentException("Child is not in this layout.", nameof(child));
        _placements[child] = (bounds, flags);
        InvalidateMeasure();
        return this;
    }

    /// <summary>Removes a child if present, including its absolute placement.</summary>
    public new bool Remove(ISkUiCoreNode child)
    {
        _placements.Remove(child);
        return base.Remove(child);
    }

    /// <summary>Removes all children and placements.</summary>
    public new void Clear()
    {
        _placements.Clear();
        base.Clear();
    }

    /// <inheritdoc />
    protected override void OnChildRemoved(ISkUiCoreNode child) => _placements.Remove(child);

    /// <summary>Hides parameterless <see cref="SkUiCorePanel.Add"/> — absolute children require bounds.</summary>
    [Obsolete("Use Add(child, bounds) or Add(child, bounds, flags).", error: true)]
    public new SkUiCoreAbsoluteLayout Add(ISkUiCoreNode child) =>
        throw new NotSupportedException("Absolute layout children require bounds. Use Add(child, bounds).");

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
        var (bounds, flags) = _placements[child];
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
        var (bounds, flags) = _placements[child];
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
