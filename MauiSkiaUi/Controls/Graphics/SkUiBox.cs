using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// A filled rectangle with optional rounded corners, as MAUI's <c>BoxView</c>: it fills with <see cref="Fill"/> (MAUI 11), else
/// <see cref="Color"/>, else <see cref="VisualElement.Background"/> (solid or gradient), rounded by <see cref="CornerRadius"/>,
/// and measures 40 × 40 DIPs unless sized.
/// </summary>
public class SkUiBox : SkUiView
{
    private Color? _color;
    private Brush? _fill;
    private SkUiFill _fillResolved;
    private SkUiWeakListener<SkUiBox>? _fillListener;
    private CornerRadius _cornerRadius;
    private SkUiRoundedClip _shape;

    /// <summary>Bindable <see cref="Color"/>.</summary>
    public static readonly BindableProperty ColorProperty = BindableProperty.Create(nameof(Color), typeof(Color), typeof(SkUiBox), null,
        propertyChanged: (view, _, value) => ((SkUiBox)view).OnColorChanged((Color?)value));

    /// <summary>Bindable <see cref="Fill"/>.</summary>
    public static readonly BindableProperty FillProperty = BindableProperty.Create(nameof(Fill), typeof(Brush), typeof(SkUiBox), null,
        propertyChanged: (view, _, value) => ((SkUiBox)view).OnFillChanged((Brush?)value));

    /// <summary>Bindable <see cref="CornerRadius"/>.</summary>
    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(nameof(CornerRadius), typeof(CornerRadius), typeof(SkUiBox), default(CornerRadius),
        validateValue: SkUiValidate.CornerRadii,
        propertyChanged: (view, _, value) => ((SkUiBox)view).OnCornerRadiusChanged((CornerRadius)value));

    /// <summary>The fill color (<c>null</c> by default, as MAUI: the solid background fills instead).</summary>
    public Color? Color { get => (Color?)GetValue(ColorProperty); set => SetValue(ColorProperty, value); }

    /// <summary>
    /// The brush that fills the box (MAUI 11's <c>BoxView.Fill</c>; <c>null</c> by default): solid colors and gradients
    /// (<c>Fill="Red"</c> in XAML). When set it wins over <see cref="Color"/> and the background, as in MAUI, even when empty.
    /// </summary>
    public Brush? Fill { get => (Brush?)GetValue(FillProperty); set => SetValue(FillProperty, value); }

    /// <summary>Per-corner radii (MAUI's BoxView <c>CornerRadius</c>; <c>"8"</c> or <c>"8,8,0,0"</c> in XAML). Larger radii than the box allows are scaled down.</summary>
    public CornerRadius CornerRadius { get => (CornerRadius)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    /// <summary>Sets the fill color (same as the property setter).</summary>
    public SkUiBox SetColor(Color? value)
    {
        Color = value;
        return this;
    }

    /// <summary>Sets the fill brush (same as the property setter).</summary>
    public SkUiBox SetFill(Brush? value)
    {
        Fill = value;
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

    private void OnFillChanged(Brush? value)
    {
        ReleaseBrushBindingContext(_fill, value);
        _fill = value;
        (_fillListener ??= new(this, static (box, _) => box.OnFillBrushChanged())).Listen(value);
        InheritBrushBindingContext(value);
        OnFillBrushChanged();
    }

    /// <summary>The fill brush was set or changed in place (a gradient stop): resolved once here, not on every recording.</summary>
    private void OnFillBrushChanged()
    {
        _fillResolved = _fill switch
        {
            null => default,
            SolidColorBrush { Color: { } color } => SkUiFill.From(color),
            SolidColorBrush => default,
            _ => SkUiFill.From((Paint)_fill), // gradients; other brushes draw nothing (and warn once)
        };
        InvalidatePaint();
    }

    /// <inheritdoc />
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        InheritBrushBindingContext(_fill);
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

    /// <summary>The box draws its own (rounded) fill; a plain rectangular background only when it has neither fill, color nor corners.</summary>
    protected override void OnPaintBackground(SKCanvas canvas)
    {
        if (_fill is null && _color is null && !SkUiCornerRadii.HasAny(_cornerRadius))
            base.OnPaintBackground(canvas);
    }

    /// <summary>
    /// The fill drawn by the box itself: <see cref="Fill"/>, else <see cref="Color"/>, else (with rounded corners) the background,
    /// solid or gradient.
    /// </summary>
    private SkUiFill? BoxFill() => _fill is not null ? _fillResolved
        : _color is { } color ? SkUiFill.From(color)
        : SkUiCornerRadii.HasAny(_cornerRadius) ? ResolveBackgroundFill() : null;

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        if (BoxFill() is not { IsVisible: true } fill)
            return;
        var width = (float)Width;
        var height = (float)Height;
        if (SkUiCornerRadii.HasAny(_cornerRadius))
            SkUiShapePainter.Fill(canvas, _shape.Get(width, height, _cornerRadius), fill, new SKRect(0, 0, width, height));
        else
            SkUiShapePainter.FillRect(canvas, new SKRect(0, 0, width, height), fill, antialias: true);
    }

    /// <inheritdoc />
    internal override void ReleaseDrawingResources()
    {
        base.ReleaseDrawingResources();
        _shape.Release();
    }

    /// <inheritdoc />
    internal override SKPath? CreateShadowOutline(float width, float height)
    {
        if (PaintBackground is not null || (BoxFill() ?? ResolveBackgroundFill()) is not { IsOpaque: true })
            return null;
        return SkUiCornerRadii.HasAny(_cornerRadius) ? new SKPath(_shape.Get(width, height, _cornerRadius)) : RectangleOutline(width, height);
    }
}
