using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>A drawn on/off pill switch, similar to MAUI's Switch.</summary>
public class SkUiSwitch : SkUiToggleControl
{
    private Color onColor = Color.FromArgb("#087F83");
    private Color thumbColor = Colors.White;

    /// <summary>Bindable track color while toggled on.</summary>
    public static readonly BindableProperty OnColorProperty = BindableProperty.Create(nameof(OnColor), typeof(Color), typeof(SkUiSwitch), Color.FromArgb("#087F83"),
        propertyChanged: (view, _, value) => ((SkUiSwitch)view).SetOnColor((Color)value));
    /// <summary>Bindable thumb color.</summary>
    public static readonly BindableProperty ThumbColorProperty = BindableProperty.Create(nameof(ThumbColor), typeof(Color), typeof(SkUiSwitch), Colors.White,
        propertyChanged: (view, _, value) => ((SkUiSwitch)view).SetThumbColor((Color)value));

    /// <summary>Track color while toggled on.</summary>
    public Color OnColor { get => onColor; set => SetValue(OnColorProperty, value); }
    /// <summary>Thumb (knob) color.</summary>
    public Color ThumbColor { get => thumbColor; set => SetValue(ThumbColorProperty, value); }

    /// <summary>Sets the on-color without bindable write-back.</summary>
    public SkUiSwitch SetOnColor(Color value) { ArgumentNullException.ThrowIfNull(value); onColor = value; InvalidatePaint(); return this; }
    /// <summary>Sets the thumb color without bindable write-back.</summary>
    public SkUiSwitch SetThumbColor(Color value) { ArgumentNullException.ThrowIfNull(value); thumbColor = value; InvalidatePaint(); return this; }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => new(51, 31);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        var trackColor = IsChecked ? onColor : Color.FromArgb("#C5D4D6");
        if (!IsEnabled) trackColor = trackColor.MultiplyAlpha(0.5f);
        var radius = (float)Height / 2;
        SkUiChrome.DrawRoundedBox(canvas, new SKRect(0, 0, (float)Width, (float)Height), radius, ToSkColor(trackColor), SKColors.Transparent, 0);
        var thumbRadius = radius - 2;
        var thumbX = IsChecked ? (float)Width - radius : radius;
        using var thumb = new SKPaint { Color = ToSkColor(IsEnabled ? thumbColor : thumbColor.MultiplyAlpha(0.7f)), IsAntialias = true };
        canvas.DrawCircle(thumbX, radius, thumbRadius, thumb);
    }
}
