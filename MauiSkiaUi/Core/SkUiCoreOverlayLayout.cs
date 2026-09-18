namespace MauiSkiaUi.Core;

/// <summary>
/// Overlay layout: children share the content slot (Core analogue of <c>SkUiLayout</c>).
/// Insertion order is paint order; hit-testing is front-to-back.
/// </summary>
public class SkUiCoreOverlayLayout : SkUiCorePanel
{
    /// <inheritdoc cref="SkUiCorePanel.Add" />
    public new SkUiCoreOverlayLayout Add(ISkUiCoreNode child)
    {
        base.Add(child);
        return this;
    }

    /// <inheritdoc cref="SkUiCorePanel.SetPadding" />
    public new SkUiCoreOverlayLayout SetPadding(Thickness value)
    {
        base.SetPadding(value);
        return this;
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var childWidth = Math.Max(0, widthConstraint - Padding.HorizontalThickness);
        var childHeight = Math.Max(0, heightConstraint - Padding.VerticalThickness);
        var desired = Size.Zero;
        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;
            var size = child.Measure(childWidth, childHeight);
            desired = new Size(Math.Max(desired.Width, size.Width), Math.Max(desired.Height, size.Height));
        }
        return new Size(desired.Width + Padding.HorizontalThickness, desired.Height + Padding.VerticalThickness);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        var bounds = new Rect(
            Padding.Left,
            Padding.Top,
            Math.Max(0, size.Width - Padding.HorizontalThickness),
            Math.Max(0, size.Height - Padding.VerticalThickness));
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                child.Arrange(Rect.Zero);
                continue;
            }
            child.Arrange(bounds);
        }
    }
}
