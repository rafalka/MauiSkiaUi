namespace MauiSkiaUi.Core;

/// <summary>
/// Core horizontal stack: children arranged left-to-right with optional spacing.
/// Cross-axis slots stretch to the content height (no MAUI layout manager).
/// </summary>
public class SkUiCoreHorizontalStackLayout : SkUiCorePanel
{
    private double _spacing;

    /// <summary>Gap between children in DIPs.</summary>
    public double Spacing
    {
        get => _spacing;
        set => SetSpacing(value);
    }

    /// <summary>Sets the gap between children in DIPs.</summary>
    public SkUiCoreHorizontalStackLayout SetSpacing(double value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        if (!SetProperty(ref _spacing, value, nameof(Spacing))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <inheritdoc cref="SkUiCorePanel.Add" />
    public new SkUiCoreHorizontalStackLayout Add(ISkUiCoreNode child)
    {
        base.Add(child);
        return this;
    }

    /// <inheritdoc cref="SkUiCorePanel.SetPadding" />
    public new SkUiCoreHorizontalStackLayout SetPadding(Thickness value)
    {
        base.SetPadding(value);
        return this;
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var contentHeight = Math.Max(0, heightConstraint - Padding.VerticalThickness);
        var totalWidth = 0.0;
        var maxHeight = 0.0;
        var visible = 0;
        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;
            child.Measure(double.PositiveInfinity, contentHeight);
            maxHeight = Math.Max(maxHeight, child.DesiredSize.Height);
            totalWidth += child.DesiredSize.Width;
            visible++;
        }
        if (visible > 1)
            totalWidth += _spacing * (visible - 1);
        return new Size(totalWidth + Padding.HorizontalThickness, maxHeight + Padding.VerticalThickness);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        var contentHeight = Math.Max(0, size.Height - Padding.VerticalThickness);
        var x = Padding.Left;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                child.Arrange(Rect.Zero);
                continue;
            }
            var width = child.DesiredSize.Width;
            child.Arrange(new Rect(x, Padding.Top, width, contentHeight));
            x += width + _spacing;
        }
    }
}
