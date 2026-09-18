using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>Drawn on/off pill switch (Core analogue of <c>SkUiSwitch</c>).</summary>
public class SkUiCoreSwitch : SkUiCoreToggleControl
{
    private Color _onColor = SkUiColors.Accent;
    private Color _thumbColor = Colors.White;

    /// <summary>Track color while toggled on.</summary>
    public Color OnColor
    {
        get => _onColor;
        set => SetOnColor(value);
    }

    /// <summary>Thumb (knob) color.</summary>
    public Color ThumbColor
    {
        get => _thumbColor;
        set => SetThumbColor(value);
    }

    /// <summary>Sets the on-track color.</summary>
    public SkUiCoreSwitch SetOnColor(Color value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!SetProperty(ref _onColor, value, nameof(OnColor))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets the thumb color.</summary>
    public SkUiCoreSwitch SetThumbColor(Color value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!SetProperty(ref _thumbColor, value, nameof(ThumbColor))) return this;
        InvalidatePaint();
        return this;
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        SkUiLook.Current.MeasureSwitch(widthConstraint, heightConstraint);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        var trackColor = IsChecked ? _onColor : SkUiColors.TrackOff;
        SkUiLook.Current.DrawSwitch(
            canvas,
            new SKRect(0, 0, (float)Frame.Width, (float)Frame.Height),
            IsChecked,
            ToSkColor(trackColor),
            ToSkColor(_thumbColor));
    }
}
