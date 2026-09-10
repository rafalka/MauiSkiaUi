using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// A drawn radio button, similar to MAUI's RadioButton. <see cref="GroupName"/> is exposed for app-level
/// bookkeeping only; unlike MAUI's <c>RadioButton</c>, this control does not automatically uncheck other
/// radio buttons sharing the same group/parent — apps must clear siblings themselves (e.g. in the checked-changed event).
/// </summary>
public class SkUiRadioButton : SkUiToggleControl
{
    private Color color = Color.FromArgb("#087F83");
    private string? groupName;

    /// <summary>Bindable dot/ring color while checked.</summary>
    public static readonly BindableProperty ColorProperty = BindableProperty.Create(nameof(Color), typeof(Color), typeof(SkUiRadioButton), Color.FromArgb("#087F83"),
        propertyChanged: (view, _, value) => ((SkUiRadioButton)view).SetColor((Color)value));
    /// <summary>Bindable group name for app-level mutual exclusion (see remarks).</summary>
    public static readonly BindableProperty GroupNameProperty = BindableProperty.Create(nameof(GroupName), typeof(string), typeof(SkUiRadioButton), null,
        propertyChanged: (view, _, value) => ((SkUiRadioButton)view).SetGroupName((string?)value));

    /// <summary>Ring/dot color while checked.</summary>
    public Color Color { get => color; set => SetValue(ColorProperty, value); }
    /// <summary>Group name for app-level mutual exclusion; not enforced by this control.</summary>
    public string? GroupName { get => groupName; set => SetValue(GroupNameProperty, value); }

    /// <summary>Sets the color without bindable write-back.</summary>
    public SkUiRadioButton SetColor(Color value) { ArgumentNullException.ThrowIfNull(value); color = value; InvalidatePaint(); return this; }
    /// <summary>Sets the group name without bindable write-back.</summary>
    public SkUiRadioButton SetGroupName(string? value) { groupName = value; return this; }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => new(24, 24);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        var size = (float)Math.Min(Width, Height);
        var center = size / 2;
        var ringColor = IsChecked ? color : Color.FromArgb("#8A9A9C");
        if (!IsEnabled) ringColor = ringColor.MultiplyAlpha(0.5f);
        using var ring = new SKPaint { Color = ToSkColor(ringColor), Style = SKPaintStyle.Stroke, StrokeWidth = size * 0.08f, IsAntialias = true };
        canvas.DrawCircle(center, center, center - ring.StrokeWidth / 2, ring);
        if (!IsChecked) return;
        var dotColor = IsEnabled ? color : color.MultiplyAlpha(0.5f);
        using var dot = new SKPaint { Color = ToSkColor(dotColor), IsAntialias = true };
        canvas.DrawCircle(center, center, size * 0.28f, dot);
    }
}
