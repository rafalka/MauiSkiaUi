namespace MauiSkiaUi.Core;

/// <summary>
/// Core wrap layout: children flow left to right and wrap onto a new row when the next one does not fit
/// (chips, tags, filter rows). <see cref="Spacing"/> separates items in a row, <see cref="RowSpacing"/> separates
/// rows; children align inside their row slot (<see cref="SkUiCoreNode.VerticalAlignment"/>, Fill by default).
/// Same engine as <see cref="SkUiWrapLayout"/>.
/// </summary>
public class SkUiCoreWrapLayout : SkUiCorePanel
{
    private double _spacing;
    private double _rowSpacing;

    /// <summary>Gap between items in a row, in DIPs.</summary>
    public double Spacing
    {
        get => _spacing;
        set => SetSpacing(value);
    }

    /// <summary>Gap between rows, in DIPs.</summary>
    public double RowSpacing
    {
        get => _rowSpacing;
        set => SetRowSpacing(value);
    }

    /// <summary>Sets the gap between items in a row.</summary>
    public SkUiCoreWrapLayout SetSpacing(double value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        if (!SetProperty(ref _spacing, value, nameof(Spacing))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <summary>Sets the gap between rows.</summary>
    public SkUiCoreWrapLayout SetRowSpacing(double value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        if (!SetProperty(ref _rowSpacing, value, nameof(RowSpacing))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <inheritdoc cref="SkUiCorePanel.Add" />
    public new SkUiCoreWrapLayout Add(ISkUiCoreNode child)
    {
        base.Add(child);
        return this;
    }

    /// <inheritdoc cref="SkUiCorePanel.SetPadding" />
    public new SkUiCoreWrapLayout SetPadding(Thickness value)
    {
        base.SetPadding(value);
        return this;
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var children = new SkUiCoreNodeChildren(Children);
        return SkUiWrapEngine.Measure(ref children, widthConstraint, Padding, _spacing, _rowSpacing);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        var children = new SkUiCoreNodeChildren(Children);
        SkUiWrapEngine.Arrange(ref children, size, Padding, _spacing, _rowSpacing);
    }
}
