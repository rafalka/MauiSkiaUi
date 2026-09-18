using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>Drawn checkbox (Core analogue of <c>SkUiCheckBox</c>).</summary>
public class SkUiCoreCheckBox : SkUiCoreToggleControl
{
    private Color _color = SkUiColors.Accent;

    /// <summary>Fill/checkmark color while checked; unchecked draws a neutral outline.</summary>
    public Color Color
    {
        get => _color;
        set => SetColor(value);
    }

    /// <summary>Sets the checked fill/checkmark color.</summary>
    public SkUiCoreCheckBox SetColor(Color value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!SetProperty(ref _color, value, nameof(Color))) return this;
        InvalidatePaint();
        return this;
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        SkUiLook.Current.MeasureCheckBox(widthConstraint, heightConstraint);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        var size = (float)Math.Min(Frame.Width, Frame.Height);
        var fillColor = IsChecked ? _color : Colors.White;
        var borderColor = IsChecked ? _color : SkUiColors.Muted;
        SkUiLook.Current.DrawCheckBox(canvas, size, IsChecked, ToSkColor(fillColor), ToSkColor(borderColor));
    }
}
