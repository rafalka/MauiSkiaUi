using MauiSkiaUi.Core;

namespace MauiSkiaUi;

/// <summary>
/// Layer-agnostic access to a layout's children, so one layout engine serves both the SkUi* and the Core layer.
/// Engines take the adapters as generic struct parameters (no boxing, no allocation per pass).
/// Slots are in the layout's local coordinates, left to right; RTL mirroring happens in the nodes.
/// </summary>
internal interface ISkUiLayoutChildren
{
    int Count { get; }
    bool IsVisible(int index);
    /// <summary>Measures a child; the result includes its margins.</summary>
    Size Measure(int index, double widthConstraint, double heightConstraint);
    /// <summary>The child's last measured size, including margins.</summary>
    Size DesiredSize(int index);
    /// <summary>The smallest size the child accepts (its minimum size requests), including margins.</summary>
    Size MinimumSize(int index);
    /// <summary>Arranges a visible child in its slot; the child aligns itself inside it.</summary>
    void Arrange(int index, Rect slot);
    /// <summary>Handles a child that takes no space (collapsed / hidden).</summary>
    void ArrangeHidden(int index);
}

/// <summary>SkUi* children: MAUI visibility, alignment by the child's own <c>ComputeFrame</c>.</summary>
internal readonly struct SkUiViewChildren(IList<ISkUiView> children) : ISkUiLayoutChildren
{
    public int Count => children.Count;
    public bool IsVisible(int index) => children[index].Visibility != Visibility.Collapsed;
    public Size Measure(int index, double widthConstraint, double heightConstraint) => children[index].Measure(widthConstraint, heightConstraint);
    public Size DesiredSize(int index) => children[index].DesiredSize;

    public Size MinimumSize(int index)
    {
        var child = children[index];
        var margin = child.Margin;
        return new Size(Minimum(child.MinimumWidth) + margin.HorizontalThickness, Minimum(child.MinimumHeight) + margin.VerticalThickness);
    }

    // Unset minimum requests can read as NaN (and Math.Max(0, NaN) is NaN).
    private static double Minimum(double value) => double.IsNaN(value) || value < 0 ? 0 : value;

    public void Arrange(int index, Rect slot) => children[index].Arrange(slot);
    // As MAUI's stack managers: collapsed children are skipped.
    public void ArrangeHidden(int index) { }
}

/// <summary>Core children: <see cref="ISkUiCoreNode.IsVisible"/>, alignment applied by the container (Core nodes do not align themselves).</summary>
internal readonly struct SkUiCoreNodeChildren(IReadOnlyList<ISkUiCoreNode> children) : ISkUiLayoutChildren
{
    public int Count => children.Count;
    public bool IsVisible(int index) => children[index].IsVisible;
    public Size Measure(int index, double widthConstraint, double heightConstraint) => children[index].Measure(widthConstraint, heightConstraint);
    public Size DesiredSize(int index) => children[index].DesiredSize;

    public Size MinimumSize(int index) => children[index] is SkUiCoreNode node
        ? new Size(Math.Max(0, node.MinimumWidth) + node.Margin.HorizontalThickness, Math.Max(0, node.MinimumHeight) + node.Margin.VerticalThickness)
        : Size.Zero;

    public void Arrange(int index, Rect slot)
    {
        var child = children[index];
        if (child is SkUiCoreNode node)
            slot = SkUiCoreAbsoluteLayout.AlignInSlot(slot, child.DesiredSize, node.HorizontalAlignment, node.VerticalAlignment);
        child.Arrange(slot);
    }

    public void ArrangeHidden(int index) => children[index].Arrange(Rect.Zero);
}
