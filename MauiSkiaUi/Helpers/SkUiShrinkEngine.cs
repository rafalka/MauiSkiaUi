namespace MauiSkiaUi;

/// <summary>Per-layout results of <see cref="SkUiShrinkEngine.Measure{TChildren, TFactors}"/>, reused by arrange.</summary>
internal struct SkUiShrinkState
{
    /// <summary>Main-axis slot size per child index (0 for hidden children).</summary>
    public double[]? Slots;
    /// <summary>Scratch: shrink weight per child index while solving (0 = not shrinking, or frozen at its minimum).</summary>
    public double[]? Weights;
    /// <summary>Child count the slots were computed for.</summary>
    public int Count;
    /// <summary>Content main-axis space the slots were computed for (infinite when unconstrained).</summary>
    public double AvailableMain;
    /// <summary>Content main-axis size of the slots plus spacing.</summary>
    public double TotalMain;
}

/// <summary>
/// The shrink factor of each child of a shrink layout, by child index. Each layer keeps it its own way (an attached
/// bindable property on SkUi*, a Core attached property on Core) and passes it to the engine next to the children.
/// </summary>
internal interface ISkUiShrinkFactors
{
    SkUiShrinkFactor Shrink(int index);
}

/// <summary>
/// Shrink stack shared by the horizontal / vertical shrink layouts on both layers. It is a stack that fits its main
/// axis. Without overflow it is a plain stack. When the children overflow, the overflow is shared by weight:
/// factor × natural size for an explicit <see cref="SkUiShrinkFactor"/>, natural size for <see cref="SkUiShrinkFactor.Auto"/>
/// children larger than the average visible child, 0 otherwise. A child whose share would take it below its minimum
/// size is frozen there and the rest is shared again among the others (as CSS flex-shrink). Shrunk children are
/// re-measured at their new size (a label truncates or wraps).
/// </summary>
internal static class SkUiShrinkEngine
{
    private const double Tolerance = 0.01;

    public static Size Measure<TChildren, TFactors>(ref TChildren children, ref TFactors factors, ref SkUiShrinkState state, bool vertical,
        double widthConstraint, double heightConstraint, Thickness padding, double spacing)
        where TChildren : struct, ISkUiLayoutChildren
        where TFactors : struct, ISkUiShrinkFactors
    {
        var count = children.Count;
        if (state.Slots is null || state.Slots.Length < count)
        {
            state.Slots = new double[Math.Max(count, 4)];
            state.Weights = new double[state.Slots.Length];
        }
        var slots = state.Slots;
        var weights = state.Weights!;
        var mainPadding = vertical ? padding.VerticalThickness : padding.HorizontalThickness;
        var crossPadding = vertical ? padding.HorizontalThickness : padding.VerticalThickness;
        var availableMain = Math.Max(0, (vertical ? heightConstraint : widthConstraint) - mainPadding);
        var contentCross = Math.Max(0, (vertical ? widthConstraint : heightConstraint) - crossPadding);

        // Natural sizes: unconstrained on the main axis.
        double natural = 0, cross = 0;
        var visible = 0;
        for (var index = 0; index < count; index++)
        {
            slots[index] = 0;
            weights[index] = 0;
            if (!children.IsVisible(index))
                continue;
            var size = MeasureChild(ref children, index, vertical, double.PositiveInfinity, contentCross);
            slots[index] = vertical ? size.Height : size.Width;
            natural += slots[index];
            cross = Math.Max(cross, vertical ? size.Width : size.Height);
            visible++;
        }
        var totalSpacing = visible > 1 ? spacing * (visible - 1) : 0;
        var total = natural + totalSpacing;

        if (total > availableMain + Tolerance && Shrink(ref children, ref factors, slots, weights, vertical, availableMain, totalSpacing, natural, visible))
        {
            cross = 0;
            total = totalSpacing;
            for (var index = 0; index < count; index++)
            {
                if (!children.IsVisible(index))
                    continue;
                // Shrunk children are re-measured at their slot; the others keep their natural measure.
                var size = weights[index] < 0
                    ? MeasureChild(ref children, index, vertical, slots[index], contentCross)
                    : children.DesiredSize(index);
                total += slots[index];
                cross = Math.Max(cross, vertical ? size.Width : size.Height);
            }
        }

        state.Count = count;
        state.AvailableMain = availableMain;
        state.TotalMain = total;
        return vertical
            ? new Size(cross + crossPadding, total + mainPadding)
            : new Size(total + mainPadding, cross + crossPadding);
    }

    /// <summary>
    /// Shares the overflow among the shrinking children by weight, freezing any child at its minimum size. Leaves
    /// each shrunk child's slot in <paramref name="slots"/> and marks it with a negative weight. Returns whether any
    /// child shrank.
    /// </summary>
    private static bool Shrink<TChildren, TFactors>(ref TChildren children, ref TFactors factors, double[] slots, double[] weights,
        bool vertical, double availableMain, double totalSpacing, double natural, int visible)
        where TChildren : struct, ISkUiLayoutChildren
        where TFactors : struct, ISkUiShrinkFactors
    {
        var count = children.Count;
        var available = Math.Max(0, availableMain - totalSpacing);
        var average = available / visible;
        var overflow = natural - available;
        double totalWeight = 0;
        for (var index = 0; index < count; index++)
        {
            if (!children.IsVisible(index))
                continue;
            var factor = factors.Shrink(index);
            var weight = factor.IsAuto
                ? (slots[index] > average ? slots[index] : 0)
                : factor.Value * slots[index];
            // A child already at (or below) its minimum cannot give anything up.
            if (weight > 0 && slots[index] > MinimumMain(ref children, index, vertical))
            {
                weights[index] = weight;
                totalWeight += weight;
            }
        }
        if (totalWeight <= 0)
            return false;

        // Distribute by weight; freeze children that would go below their minimum and share the rest again.
        // Each round freezes at least one child or finishes, so it runs at most once per child.
        var shrank = false;
        while (overflow > Tolerance && totalWeight > 0)
        {
            var frozen = false;
            for (var index = 0; index < count; index++)
            {
                if (weights[index] <= 0)
                    continue;
                var minimum = MinimumMain(ref children, index, vertical);
                if (slots[index] - overflow * weights[index] / totalWeight < minimum)
                {
                    overflow -= slots[index] - minimum;
                    totalWeight -= weights[index];
                    slots[index] = minimum;
                    weights[index] = -1;
                    frozen = shrank = true;
                }
            }
            if (frozen)
                continue;
            for (var index = 0; index < count; index++)
            {
                if (weights[index] <= 0)
                    continue;
                slots[index] -= overflow * weights[index] / totalWeight;
                weights[index] = -1;
            }
            return true;
        }
        return shrank;
    }

    private static double MinimumMain<TChildren>(ref TChildren children, int index, bool vertical)
        where TChildren : struct, ISkUiLayoutChildren
    {
        var minimum = children.MinimumSize(index);
        return vertical ? minimum.Height : minimum.Width;
    }

    public static void Arrange<TChildren, TFactors>(ref TChildren children, ref TFactors factors, ref SkUiShrinkState state, bool vertical,
        Size size, Thickness padding, double spacing)
        where TChildren : struct, ISkUiLayoutChildren
        where TFactors : struct, ISkUiShrinkFactors
    {
        var mainPadding = vertical ? padding.VerticalThickness : padding.HorizontalThickness;
        var availableMain = Math.Max(0, (vertical ? size.Height : size.Width) - mainPadding);
        // Arranged in a different main size than measured (or never measured): solve again for the real space.
        // An unconstrained measure stays valid as long as its content fits.
        var stale = state.Slots is null || state.Count != children.Count
            || (double.IsPositiveInfinity(state.AvailableMain)
                ? availableMain + Tolerance < state.TotalMain
                : Math.Abs(availableMain - state.AvailableMain) > Tolerance);
        if (stale)
            Measure(ref children, ref factors, ref state, vertical, size.Width, size.Height, padding, spacing);

        var slots = state.Slots!;
        var contentCross = Math.Max(0, (vertical ? size.Width - padding.HorizontalThickness : size.Height - padding.VerticalThickness));
        var position = vertical ? padding.Top : padding.Left;
        for (var index = 0; index < children.Count; index++)
        {
            if (!children.IsVisible(index))
            {
                children.ArrangeHidden(index);
                continue;
            }
            var slot = slots[index];
            children.Arrange(index, vertical
                ? new Rect(padding.Left, position, contentCross, slot)
                : new Rect(position, padding.Top, slot, contentCross));
            position += slot + spacing;
        }
    }

    private static Size MeasureChild<TChildren>(ref TChildren children, int index, bool vertical, double main, double cross)
        where TChildren : struct, ISkUiLayoutChildren =>
        vertical ? children.Measure(index, cross, main) : children.Measure(index, main, cross);
}
