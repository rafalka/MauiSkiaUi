using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>A drawn on/off pill switch, similar to MAUI's Switch.</summary>
public class SkUiSwitch : SkUiToggleControl
{
    private Color _onColor = SkUiColors.Accent;
    private Color _thumbColor = Colors.White;

    /// <summary>Bindable track color while toggled on.</summary>
    public static readonly BindableProperty OnColorProperty = BindableProperty.Create(nameof(OnColor), typeof(Color), typeof(SkUiSwitch), null,
        defaultValueCreator: _ => SkUiColors.Accent,
        propertyChanged: (view, _, value) => ((SkUiSwitch)view).SetOnColor((Color)value));
    /// <summary>Bindable thumb color.</summary>
    public static readonly BindableProperty ThumbColorProperty = BindableProperty.Create(nameof(ThumbColor), typeof(Color), typeof(SkUiSwitch), Colors.White,
        propertyChanged: (view, _, value) => ((SkUiSwitch)view).SetThumbColor((Color)value));

    /// <summary>Track color while toggled on.</summary>
    public Color OnColor { get => _onColor; set => SetValue(OnColorProperty, value); }
    /// <summary>Thumb (knob) color.</summary>
    public Color ThumbColor { get => _thumbColor; set => SetValue(ThumbColorProperty, value); }

    /// <summary>Sets the on-color without bindable write-back.</summary>
    public SkUiSwitch SetOnColor(Color value) { ArgumentNullException.ThrowIfNull(value); _onColor = value; InvalidatePaint(); return this; }
    /// <summary>Sets the thumb color without bindable write-back.</summary>
    public SkUiSwitch SetThumbColor(Color value) { ArgumentNullException.ThrowIfNull(value); _thumbColor = value; InvalidatePaint(); return this; }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        SkUiLook.Current.MeasureSwitch(widthConstraint, heightConstraint);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        var trackColor = IsChecked ? _onColor : SkUiColors.TrackOff;
        var thumbColor = _thumbColor;
        if (!IsEnabled)
        {
            trackColor = trackColor.MultiplyAlpha(0.5f);
            thumbColor = thumbColor.MultiplyAlpha(0.7f);
        }
        SkUiLook.Current.DrawSwitch(
            canvas,
            new SKRect(0, 0, (float)Width, (float)Height),
            IsChecked,
            ToSkColor(trackColor),
            ToSkColor(thumbColor));
    }
}
