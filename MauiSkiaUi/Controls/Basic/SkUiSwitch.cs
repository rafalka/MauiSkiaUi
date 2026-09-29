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
    protected override SkUiTransitionKind TransitionKind => SkUiTransitionKind.Switch;

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas) =>
        SkUiToggleDrawing.DrawSwitch(canvas, (float)Width, (float)Height, IsRightToLeft, ToggleVisual, _onColor, _thumbColor, IsEnabled);
}
