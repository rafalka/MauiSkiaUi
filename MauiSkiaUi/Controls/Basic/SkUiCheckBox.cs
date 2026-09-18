using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>A drawn checkbox, similar to MAUI's CheckBox.</summary>
public class SkUiCheckBox : SkUiToggleControl
{
    private Color _color = SkUiColors.Accent;

    /// <summary>Bindable checkmark/fill color while checked.</summary>
    public static readonly BindableProperty ColorProperty = BindableProperty.Create(nameof(Color), typeof(Color), typeof(SkUiCheckBox), null,
        defaultValueCreator: _ => SkUiColors.Accent,
        propertyChanged: (view, _, value) => ((SkUiCheckBox)view).SetColor((Color)value));

    /// <summary>Fill/checkmark color while checked; unchecked always draws a neutral outline.</summary>
    public Color Color { get => _color; set => SetValue(ColorProperty, value); }

    /// <summary>Sets the color without bindable write-back.</summary>
    public SkUiCheckBox SetColor(Color value) { ArgumentNullException.ThrowIfNull(value); _color = value; InvalidatePaint(); return this; }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        SkUiLook.Current.MeasureCheckBox(widthConstraint, heightConstraint);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        var size = (float)Math.Min(Width, Height);
        var fillColor = IsChecked ? _color : Colors.White;
        var borderColor = IsChecked ? _color : SkUiColors.Muted;
        if (!IsEnabled)
        {
            fillColor = fillColor.MultiplyAlpha(0.5f);
            borderColor = borderColor.MultiplyAlpha(0.5f);
        }
        SkUiLook.Current.DrawCheckBox(canvas, size, IsChecked, ToSkColor(fillColor), ToSkColor(borderColor));
    }
}
