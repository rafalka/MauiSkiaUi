namespace MauiSkiaUi.Core;

/// <summary>
/// Core vertical stack: children arranged top-to-bottom with optional spacing.
/// Cross-axis slots stretch to the content width (no MAUI layout manager).
/// </summary>
public class SkUiCoreVerticalStackLayout : SkUiCorePanel
{
    private double _spacing;

    /// <summary>Gap between children in DIPs.</summary>
    public double Spacing
    {
        get => _spacing;
        set => SetSpacing(value);
    }

    /// <summary>Sets the gap between children in DIPs.</summary>
    public SkUiCoreVerticalStackLayout SetSpacing(double value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        if (!SetProperty(ref _spacing, value, nameof(Spacing))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <inheritdoc cref="SkUiCorePanel.Add" />
    public new SkUiCoreVerticalStackLayout Add(ISkUiCoreNode child)
    {
        base.Add(child);
        return this;
    }

    /// <inheritdoc cref="SkUiCorePanel.SetPadding" />
    public new SkUiCoreVerticalStackLayout SetPadding(Thickness value)
    {
        base.SetPadding(value);
        return this;
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var contentWidth = Math.Max(0, widthConstraint - Padding.HorizontalThickness);
        var totalHeight = 0.0;
        var maxWidth = 0.0;
        var visible = 0;
        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;
            child.Measure(contentWidth, double.PositiveInfinity);
            maxWidth = Math.Max(maxWidth, child.DesiredSize.Width);
            totalHeight += child.DesiredSize.Height;
            visible++;
        }
        if (visible > 1)
            totalHeight += _spacing * (visible - 1);
        return new Size(maxWidth + Padding.HorizontalThickness, totalHeight + Padding.VerticalThickness);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        var contentWidth = Math.Max(0, size.Width - Padding.HorizontalThickness);
        var y = Padding.Top;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                child.Arrange(Rect.Zero);
                continue;
            }
            var height = child.DesiredSize.Height;
            child.Arrange(new Rect(Padding.Left, y, contentWidth, height));
            y += height + _spacing;
        }
    }
}
