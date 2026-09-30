namespace MauiSkiaUi.Core;

/// <summary>
/// Base of the Core shrink stacks (<see cref="SkUiCoreHorizontalShrinkLayout"/>, <see cref="SkUiCoreVerticalShrinkLayout"/>):
/// a stack that fits its main axis. Without overflow it is a plain stack that takes only the space its children need.
/// When the children overflow, children with a <see cref="ShrinkProperty"/> factor give up space (see
/// <see cref="SkUiShrinkFactor"/>); other children keep their size. Clips its children, as content may still overflow
/// when the children that can shrink have reached their minimum. Same engine as <see cref="SkUiShrinkLayout"/>.
/// </summary>
public abstract class SkUiCoreShrinkLayout : SkUiCorePanel
{
    private readonly bool _vertical;
    private double _spacing;
    private SkUiShrinkState _state;

    /// <summary>
    /// Attached: how a child gives up space when the children do not fit (default <see cref="SkUiShrinkFactor.None"/>).
    /// Set it with <see cref="Add(ISkUiCoreNode, SkUiShrinkFactor)"/>, <see cref="SetShrink(ISkUiCoreNode, SkUiShrinkFactor)"/>
    /// or <c>child.SetValue(SkUiCoreShrinkLayout.ShrinkProperty, SkUiShrinkFactor.Auto)</c>, also before the child is added.
    /// </summary>
    public static readonly SkUiCoreAttachedProperty<SkUiShrinkFactor> ShrinkProperty = new("Shrink", typeof(SkUiCoreShrinkLayout), SkUiShrinkFactor.None);

    private protected SkUiCoreShrinkLayout(bool vertical)
    {
        _vertical = vertical;
        InitClipToBounds(true);
    }

    /// <summary>Gap between children in DIPs.</summary>
    public double Spacing
    {
        get => _spacing;
        set => SetSpacing(value);
    }

    /// <summary>Sets the gap between children in DIPs.</summary>
    public SkUiCoreShrinkLayout SetSpacing(double value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        if (!SetProperty(ref _spacing, value, nameof(Spacing))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <summary>Appends a child; it keeps its own <see cref="ShrinkProperty"/> value (<see cref="SkUiShrinkFactor.None"/> unless set).</summary>
    public new SkUiCoreShrinkLayout Add(ISkUiCoreNode child)
    {
        base.Add(child);
        return this;
    }

    /// <summary>Appends a child and sets its <see cref="ShrinkProperty"/>.</summary>
    public SkUiCoreShrinkLayout Add(ISkUiCoreNode child, SkUiShrinkFactor shrink)
    {
        ArgumentNullException.ThrowIfNull(child);
        // Insert first (it validates the child), then write the factor; one re-measure for both.
        StartUpdating();
        try
        {
            base.Add(child);
            ((SkUiCoreNode)child).SetValue(ShrinkProperty, shrink);
        }
        finally
        {
            EndUpdating();
        }
        return this;
    }

    /// <summary>Changes the shrink factor of a child of this layout.</summary>
    public SkUiCoreShrinkLayout SetShrink(ISkUiCoreNode child, SkUiShrinkFactor shrink)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (child.Parent != this)
            throw new ArgumentException("Child is not in this layout.", nameof(child));
        ((SkUiCoreNode)child).SetValue(ShrinkProperty, shrink);
        return this;
    }

    /// <summary>Changes the shrink factor of the child at <paramref name="index"/>.</summary>
    public SkUiCoreShrinkLayout SetShrink(int index, SkUiShrinkFactor shrink) => SetShrink(Children[index], shrink);

    /// <summary>A node's shrink factor (its <see cref="ShrinkProperty"/>).</summary>
    public SkUiShrinkFactor GetShrink(ISkUiCoreNode child)
    {
        ArgumentNullException.ThrowIfNull(child);
        return child is SkUiCoreNode node ? node.GetValue(ShrinkProperty) : SkUiShrinkFactor.None;
    }

    /// <summary>The shrink factor of the child at <paramref name="index"/>.</summary>
    public SkUiShrinkFactor GetShrink(int index) => GetShrink(Children[index]);

    /// <inheritdoc cref="SkUiCorePanel.SetPadding" />
    public new SkUiCoreShrinkLayout SetPadding(Thickness value)
    {
        base.SetPadding(value);
        return this;
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var children = new SkUiCoreNodeChildren(Children);
        var factors = new AttachedShrinkFactors(Children);
        return SkUiShrinkEngine.Measure(ref children, ref factors, ref _state, _vertical, widthConstraint, heightConstraint, Padding, _spacing);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        var children = new SkUiCoreNodeChildren(Children);
        var factors = new AttachedShrinkFactors(Children);
        SkUiShrinkEngine.Arrange(ref children, ref factors, ref _state, _vertical, size, Padding, _spacing);
    }

    /// <summary>Reads each child's <see cref="ShrinkProperty"/> (panels hold <see cref="SkUiCoreNode"/> children only).</summary>
    private readonly struct AttachedShrinkFactors(IReadOnlyList<ISkUiCoreNode> children) : ISkUiShrinkFactors
    {
        public SkUiShrinkFactor Shrink(int index) => ((SkUiCoreNode)children[index]).GetValue(ShrinkProperty);
    }
}

/// <summary>Core horizontal shrink stack: children with a shrink factor narrow to fit the width.</summary>
public class SkUiCoreHorizontalShrinkLayout : SkUiCoreShrinkLayout
{
    /// <summary>Creates a horizontal shrink stack.</summary>
    public SkUiCoreHorizontalShrinkLayout() : base(vertical: false) { }
}

/// <summary>Core vertical shrink stack: children with a shrink factor shorten to fit the height.</summary>
public class SkUiCoreVerticalShrinkLayout : SkUiCoreShrinkLayout
{
    /// <summary>Creates a vertical shrink stack.</summary>
    public SkUiCoreVerticalShrinkLayout() : base(vertical: true) { }
}
