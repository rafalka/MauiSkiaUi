using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// A filled rectangle with optional rounded corners, as MAUI's <c>BoxView</c>: it fills with <see cref="Color"/>, or with a
/// solid <see cref="VisualElement.Background"/> when no color is set, rounded by <see cref="CornerRadius"/>, and measures
/// 40 × 40 DIPs unless sized.
/// </summary>
public class SkUiBox : SkUiView
{
    private Color? _color;
    private CornerRadius _cornerRadius;
    private SkUiRoundedClip _shape;

    /// <summary>Bindable <see cref="Color"/>.</summary>
    public static readonly BindableProperty ColorProperty = BindableProperty.Create(nameof(Color), typeof(Color), typeof(SkUiBox), null,
        propertyChanged: (view, _, value) => ((SkUiBox)view).OnColorChanged((Color?)value));

    /// <summary>Bindable <see cref="CornerRadius"/>.</summary>
    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(nameof(CornerRadius), typeof(CornerRadius), typeof(SkUiBox), default(CornerRadius),
        validateValue: SkUiValidate.CornerRadii,
        propertyChanged: (view, _, value) => ((SkUiBox)view).OnCornerRadiusChanged((CornerRadius)value));

    /// <summary>The fill color (<c>null</c> by default, as MAUI: the solid background fills instead).</summary>
    public Color? Color { get => (Color?)GetValue(ColorProperty); set => SetValue(ColorProperty, value); }

    /// <summary>Per-corner radii (MAUI's BoxView <c>CornerRadius</c>; <c>"8"</c> or <c>"8,8,0,0"</c> in XAML). Larger radii than the box allows are scaled down.</summary>
    public CornerRadius CornerRadius { get => (CornerRadius)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    /// <summary>Sets the fill color (same as the property setter).</summary>
    public SkUiBox SetColor(Color? value)
    {
        Color = value;
        return this;
    }

    /// <summary>Sets the corner radii (same as the property setter).</summary>
    public SkUiBox SetCornerRadius(CornerRadius value)
    {
        SkUiCornerRadii.Validate(value, nameof(value));
        CornerRadius = value;
        return this;
    }

    private void OnColorChanged(Color? value)
    {
        _color = value;
        InvalidatePaint();
    }

    private void OnCornerRadiusChanged(CornerRadius value)
    {
        if (_cornerRadius == value) return;
        _cornerRadius = value;
        InvalidatePaint();
    }

    /// <inheritdoc />
    internal override CornerRadius PressEffectCornerRadii => _cornerRadius;

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => new(40, 40);

    /// <summary>The box draws its own (rounded) fill; a plain rectangular background only when it has neither color nor corners.</summary>
    protected override void OnPaintBackground(SKCanvas canvas)
    {
        if (_color is null && !SkUiCornerRadii.HasAny(_cornerRadius))
            base.OnPaintBackground(canvas);
    }

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        if ((_color ?? (SkUiCornerRadii.HasAny(_cornerRadius) ? ResolveSolidBackgroundColor() : null)) is not { } color)
            return;
        using var paint = new SKPaint { Color = ToSkColor(color), IsAntialias = true };
        var width = (float)Width;
        var height = (float)Height;
        if (SkUiCornerRadii.HasAny(_cornerRadius))
            canvas.DrawPath(_shape.Get(width, height, _cornerRadius), paint);
        else
            canvas.DrawRect(0, 0, width, height, paint);
    }
}
