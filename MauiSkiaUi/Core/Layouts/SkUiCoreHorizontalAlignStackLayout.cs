namespace MauiSkiaUi.Core;

/// <summary>
/// Core horizontal stack that places its children by their <see cref="SkUiCoreNode.HorizontalAlignment"/>, not by their place
/// in the list: Start children stacked from the start side, End children from the end side, and Center (and Fill) children in a
/// row placed by <see cref="CenterStrategy"/>, by default centered in the layout whatever is at the sides; with Fill children, the
/// row takes the whole room between the sides and they share what is left of it. The side children are measured first and the
/// centered ones get the width left. Same engine as <see cref="SkUiHorizontalAlignStackLayout"/>.
/// </summary>
public class SkUiCoreHorizontalAlignStackLayout : SkUiCorePanel
{
    private double _spacing;
    private SkUiCenterStrategy _centerStrategy;
    private SkUiAlignStackState _state;

    /// <summary>Gap between children in DIPs (also the least gap between the sides and the centered children).</summary>
    public double Spacing
    {
        get => _spacing;
        set => SetSpacing(value);
    }

    /// <summary>Where the Center children go (a row with Fill children spans the room between the sides); default <see cref="SkUiCenterStrategy.Parent"/>.</summary>
    public SkUiCenterStrategy CenterStrategy
    {
        get => _centerStrategy;
        set => SetCenterStrategy(value);
    }

    /// <summary>Sets the gap between children in DIPs.</summary>
    public SkUiCoreHorizontalAlignStackLayout SetSpacing(double value)
    {
        SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value));
        if (!SetProperty(ref _spacing, value, nameof(Spacing))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <summary>Sets where the Center children go (a row with Fill children spans the room between the sides).</summary>
    public SkUiCoreHorizontalAlignStackLayout SetCenterStrategy(SkUiCenterStrategy value)
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown center strategy.");
        if (!SetProperty(ref _centerStrategy, value, nameof(CenterStrategy))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <inheritdoc cref="SkUiCorePanel.Add" />
    public new SkUiCoreHorizontalAlignStackLayout Add(ISkUiCoreNode child)
    {
        base.Add(child);
        return this;
    }

    /// <summary>Appends a child and sets its <see cref="SkUiCoreNode.HorizontalAlignment"/> (its group: Start, Center or End).</summary>
    public SkUiCoreHorizontalAlignStackLayout Add(SkUiCoreNode child, LayoutAlignment alignment)
    {
        ArgumentNullException.ThrowIfNull(child);
        child.SetHorizontalAlignment(alignment);
        base.Add(child);
        return this;
    }

    /// <inheritdoc cref="SkUiCorePanel.SetPadding" />
    public new SkUiCoreHorizontalAlignStackLayout SetPadding(Thickness value)
    {
        base.SetPadding(value);
        return this;
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var children = new SkUiCoreNodeChildren(Children);
        return SkUiAlignStackEngine.Measure(ref children, ref _state, _centerStrategy, widthConstraint, heightConstraint, Padding, _spacing);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        var children = new SkUiCoreNodeChildren(Children);
        SkUiAlignStackEngine.Arrange(ref children, ref _state, _centerStrategy, size, Padding, _spacing);
    }
}
