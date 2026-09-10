using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>A drawn checkbox, similar to MAUI's CheckBox.</summary>
public class SkUiCheckBox : SkUiToggleControl
{
    private Color color = Color.FromArgb("#087F83");

    /// <summary>Bindable checkmark/fill color while checked.</summary>
    public static readonly BindableProperty ColorProperty = BindableProperty.Create(nameof(Color), typeof(Color), typeof(SkUiCheckBox), Color.FromArgb("#087F83"),
        propertyChanged: (view, _, value) => ((SkUiCheckBox)view).SetColor((Color)value));

    /// <summary>Fill/checkmark color while checked; unchecked always draws a neutral outline.</summary>
    public Color Color { get => color; set => SetValue(ColorProperty, value); }

    /// <summary>Sets the color without bindable write-back.</summary>
    public SkUiCheckBox SetColor(Color value) { ArgumentNullException.ThrowIfNull(value); color = value; InvalidatePaint(); return this; }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => new(24, 24);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        var size = (float)Math.Min(Width, Height);
        var bounds = new SKRect(0, 0, size, size);
        var fillColor = IsChecked ? color : Colors.White;
        var borderColor = IsChecked ? color : Color.FromArgb("#8A9A9C");
        if (!IsEnabled) { fillColor = fillColor.MultiplyAlpha(0.5f); borderColor = borderColor.MultiplyAlpha(0.5f); }
        SkUiChrome.DrawRoundedBox(canvas, bounds, size * 0.2f, ToSkColor(fillColor), ToSkColor(borderColor), 1.5f);
        if (!IsChecked) return;
        using var check = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Stroke, StrokeWidth = size * 0.12f, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round, IsAntialias = true };
        using var builder = new SKPathBuilder();
        builder.MoveTo(size * 0.22f, size * 0.55f);
        builder.LineTo(size * 0.42f, size * 0.75f);
        builder.LineTo(size * 0.8f, size * 0.28f);
        using var path = builder.Detach();
        canvas.DrawPath(path, check);
    }
}
