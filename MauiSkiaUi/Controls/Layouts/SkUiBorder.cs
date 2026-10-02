using Microsoft.Maui.Controls.Shapes;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// A single-child host with a drawn outline, as MAUI's <c>Border</c>: the background fills the <see cref="StrokeShape"/>
/// (any shape of either drawn layer or MAUI's, <c>"RoundRectangle 10"</c> in XAML; without one, a rectangle rounded by
/// <see cref="CornerRadius"/>), the <see cref="Stroke"/> brush outlines it with dashes, caps and joins, and the content sits
/// inside <see cref="SkUiContentView.Padding"/> plus the stroke and is clipped to the stroke's inner edge.
/// </summary>
/// <remarks>
/// As MAUI on Android and Windows, the stroke is centered on the shape fitted into the bounds inset by half the stroke, so
/// it stays inside the bounds. Input uses the rectangular arranged bounds.
/// </remarks>
public class SkUiBorder : SkUiContentView
{
    private Brush? _stroke;
    private double _strokeThickness = 1;
    private double _strokeDashOffset;
    private PenLineCap _strokeLineCap;
    private PenLineJoin _strokeLineJoin;
    private double _strokeMiterLimit = 10;
    private CornerRadius _cornerRadius;
    private IShape? _strokeShape;
    private readonly SkUiBorderGeometry _geometry = new();
    private readonly SkUiWeakListener<SkUiBorder> _strokeListener;
    private readonly SkUiWeakListener<SkUiBorder> _dashListener;
    private readonly SkUiWeakListener<SkUiBorder> _shapeListener;

    /// <summary>Bindable <see cref="StrokeShape"/>.</summary>
    public static readonly BindableProperty StrokeShapeProperty = BindableProperty.Create(nameof(StrokeShape), typeof(IShape), typeof(SkUiBorder), null,
        propertyChanged: (view, _, value) => ((SkUiBorder)view).OnStrokeShapeChanged((IShape?)value));
    /// <summary>Bindable <see cref="Stroke"/>.</summary>
    public static readonly BindableProperty StrokeProperty = BindableProperty.Create(nameof(Stroke), typeof(Brush), typeof(SkUiBorder), null,
        propertyChanged: (view, _, value) => ((SkUiBorder)view).OnStrokeChanged((Brush?)value));
    /// <summary>Bindable <see cref="StrokeThickness"/>.</summary>
    public static readonly BindableProperty StrokeThicknessProperty = BindableProperty.Create(nameof(StrokeThickness), typeof(double), typeof(SkUiBorder), 1d,
        validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, value) => ((SkUiBorder)view).OnStrokeThicknessChanged((double)value));
    /// <summary>Bindable <see cref="StrokeDashArray"/>.</summary>
    public static readonly BindableProperty StrokeDashArrayProperty = BindableProperty.Create(nameof(StrokeDashArray), typeof(DoubleCollection), typeof(SkUiBorder), null,
        defaultValueCreator: _ => new DoubleCollection(),
        propertyChanged: (view, _, value) => ((SkUiBorder)view).OnStrokeDashArrayChanged((DoubleCollection?)value));
    /// <summary>Bindable <see cref="StrokeDashOffset"/>.</summary>
    public static readonly BindableProperty StrokeDashOffsetProperty = BindableProperty.Create(nameof(StrokeDashOffset), typeof(double), typeof(SkUiBorder), 0d,
        validateValue: SkUiValidate.Finite,
        propertyChanged: (view, _, value) => ((SkUiBorder)view).OnStrokeDashOffsetChanged((double)value));
    /// <summary>Bindable <see cref="StrokeLineCap"/>.</summary>
    public static readonly BindableProperty StrokeLineCapProperty = BindableProperty.Create(nameof(StrokeLineCap), typeof(PenLineCap), typeof(SkUiBorder), PenLineCap.Flat,
        propertyChanged: (view, _, value) => ((SkUiBorder)view).OnStrokeLineCapChanged((PenLineCap)value));
    /// <summary>Bindable <see cref="StrokeLineJoin"/>.</summary>
    public static readonly BindableProperty StrokeLineJoinProperty = BindableProperty.Create(nameof(StrokeLineJoin), typeof(PenLineJoin), typeof(SkUiBorder), PenLineJoin.Miter,
        propertyChanged: (view, _, value) => ((SkUiBorder)view).OnStrokeLineJoinChanged((PenLineJoin)value));
    /// <summary>Bindable <see cref="StrokeMiterLimit"/>.</summary>
    public static readonly BindableProperty StrokeMiterLimitProperty = BindableProperty.Create(nameof(StrokeMiterLimit), typeof(double), typeof(SkUiBorder), 10d,
        validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, value) => ((SkUiBorder)view).OnStrokeMiterLimitChanged((double)value));
    /// <summary>Bindable <see cref="CornerRadius"/>.</summary>
    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(nameof(CornerRadius), typeof(CornerRadius), typeof(SkUiBorder), default(CornerRadius),
        validateValue: SkUiValidate.CornerRadii,
        propertyChanged: (view, _, value) => ((SkUiBorder)view).OnCornerRadiusChanged((CornerRadius)value));

    /// <summary>Creates a border that paints fill in <see cref="SkUiView.PaintBackground"/> and stroke in <see cref="SkUiView.PaintOverlay"/> (after content).</summary>
    public SkUiBorder()
    {
        _strokeListener = new(this, static (border, _) => border.InvalidatePaint());
        _dashListener = new(this, static (border, _) => border.InvalidatePaint());
        _shapeListener = new(this, static (border, change) => border.OnStrokeShapeEdited(change.PropertyName));
        _backgroundPainter = PaintBorderBackground;
        _overlayPainter = PaintBorderOverlay;
        SetPaintBackground(_backgroundPainter);
        SetPaintOverlay(_overlayPainter);
    }

    private readonly Action<SKCanvas> _backgroundPainter;
    private readonly Action<SKCanvas> _overlayPainter;

    /// <summary>
    /// The shape of the outline: a shape of either drawn layer (<see cref="SkUiRoundRectangle"/>, <see cref="SkUiEllipse"/>,
    /// <see cref="SkUiPath"/>, …), MAUI's own (<c>RoundRectangle</c>, …) or any <see cref="IShape"/>. XAML takes MAUI's
    /// markup: <c>"RoundRectangle 10"</c>, <c>"Ellipse"</c>, <c>"Path M 0,0 …"</c>. <c>null</c> (default): a rectangle rounded by
    /// <see cref="CornerRadius"/>, which is MAUI's default rectangle until a radius is set.
    /// </summary>
    [System.ComponentModel.TypeConverter(typeof(StrokeShapeTypeConverter))]
    public IShape? StrokeShape { get => (IShape?)GetValue(StrokeShapeProperty); set => SetValue(StrokeShapeProperty, value); }
    /// <summary>The brush of the outline (<c>null</c>: none). XAML accepts colors and gradients.</summary>
    public Brush? Stroke { get => (Brush?)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    /// <summary>The outline width in DIPs (1 by default, as MAUI); the content is inset by it.</summary>
    public double StrokeThickness { get => (double)GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
    /// <summary>Dashes and gaps of the outline, in multiples of <see cref="StrokeThickness"/> (empty: solid). XAML: <c>"4,2"</c>.</summary>
    public DoubleCollection StrokeDashArray { get => (DoubleCollection)GetValue(StrokeDashArrayProperty); set => SetValue(StrokeDashArrayProperty, value); }
    /// <summary>Where the dash pattern starts, in multiples of <see cref="StrokeThickness"/>.</summary>
    public double StrokeDashOffset { get => (double)GetValue(StrokeDashOffsetProperty); set => SetValue(StrokeDashOffsetProperty, value); }
    /// <summary>The caps of the dashes.</summary>
    public PenLineCap StrokeLineCap { get => (PenLineCap)GetValue(StrokeLineCapProperty); set => SetValue(StrokeLineCapProperty, value); }
    /// <summary>The joins at the corners of the outline.</summary>
    public PenLineJoin StrokeLineJoin { get => (PenLineJoin)GetValue(StrokeLineJoinProperty); set => SetValue(StrokeLineJoinProperty, value); }
    /// <summary>The limit on the ratio of a miter join's length to half the stroke thickness (10 by default).</summary>
    public double StrokeMiterLimit { get => (double)GetValue(StrokeMiterLimitProperty); set => SetValue(StrokeMiterLimitProperty, value); }
    /// <summary>
    /// Per-corner radii of the outline when no <see cref="StrokeShape"/> is set (SkiaUi shorthand for
    /// <c>StrokeShape="RoundRectangle …"</c>; <c>"10"</c> or <c>"16,4,4,16"</c> in XAML: top-left, top-right, bottom-left,
    /// bottom-right). 0 by default: a rectangle, as MAUI's default shape.
    /// </summary>
    public CornerRadius CornerRadius { get => (CornerRadius)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    /// <summary>Sets the outline shape (same as the property setter).</summary>
    public SkUiBorder SetStrokeShape(IShape? value) { StrokeShape = value; return this; }
    /// <summary>Sets the outline brush (same as the property setter).</summary>
    public SkUiBorder SetStroke(Brush? value) { Stroke = value; return this; }
    /// <summary>Sets the outline width (same as the property setter).</summary>
    public SkUiBorder SetStrokeThickness(double value) { SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value)); StrokeThickness = value; return this; }
    /// <summary>Sets the dash pattern, in multiples of the stroke thickness (no values: solid).</summary>
    public SkUiBorder SetStrokeDashArray(params double[] value) { StrokeDashArray = [.. value]; return this; }
    /// <summary>Sets the dash offset (same as the property setter).</summary>
    public SkUiBorder SetStrokeDashOffset(double value) { SkUiValidate.ThrowIfNotFinite(value, nameof(value)); StrokeDashOffset = value; return this; }
    /// <summary>Sets the dash caps (same as the property setter).</summary>
    public SkUiBorder SetStrokeLineCap(PenLineCap value) { StrokeLineCap = value; return this; }
    /// <summary>Sets the corner joins (same as the property setter).</summary>
    public SkUiBorder SetStrokeLineJoin(PenLineJoin value) { StrokeLineJoin = value; return this; }
    /// <summary>Sets the miter limit (same as the property setter).</summary>
    public SkUiBorder SetStrokeMiterLimit(double value) { SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value)); StrokeMiterLimit = value; return this; }
    /// <summary>Sets a uniform corner radius (same as the property setter).</summary>
    public SkUiBorder SetCornerRadius(double uniformRadius) => SetCornerRadius(new CornerRadius(uniformRadius));
    /// <summary>Sets independent corner radii (same as the property setter).</summary>
    public SkUiBorder SetCornerRadius(CornerRadius value) { SkUiCornerRadii.Validate(value, nameof(value)); CornerRadius = value; return this; }

    private void OnStrokeShapeChanged(IShape? value)
    {
        _strokeShape = value;
        _shapeListener.Listen(value);
        if (value is BindableObject bindable)
            SetInheritedBindingContext(bindable, BindingContext);
        InvalidateShape();
    }

    /// <summary>A property of the stroke shape changed (its corner radius, points, data, …).</summary>
    private void OnStrokeShapeEdited(string? propertyName)
    {
        if (propertyName is nameof(Window) or nameof(Parent) or nameof(BindingContext)) return;
        _geometry.Invalidate();
        InvalidateShape();
    }

    private void OnStrokeChanged(Brush? value)
    {
        _stroke = value;
        _strokeListener.Listen(value);
        if (value is GradientBrush || value?.GetType() == typeof(SolidColorBrush))
            SetInheritedBindingContext(value, BindingContext);
        InvalidatePaint();
    }

    private void OnStrokeThicknessChanged(double value)
    {
        _strokeThickness = value;
        InvalidateMeasureOverride(); // the content is inset by the stroke
        InvalidateRender(Rendering.SkUiRenderDirty.Props);
    }

    private void OnStrokeDashArrayChanged(DoubleCollection? value) { _dashListener.Listen(value); InvalidatePaint(); }
    private void OnStrokeDashOffsetChanged(double value) { _strokeDashOffset = value; InvalidatePaint(); }
    private void OnStrokeLineCapChanged(PenLineCap value) { _strokeLineCap = value; InvalidatePaint(); }
    private void OnStrokeLineJoinChanged(PenLineJoin value) { _strokeLineJoin = value; InvalidatePaint(); }
    private void OnStrokeMiterLimitChanged(double value) { _strokeMiterLimit = value; InvalidatePaint(); }

    private void OnCornerRadiusChanged(CornerRadius value)
    {
        _cornerRadius = value;
        InvalidateShape();
    }

    /// <summary>The outline changed: repaint and re-clip the content.</summary>
    private void InvalidateShape()
    {
        InvalidatePaint();
        InvalidateRender(Rendering.SkUiRenderDirty.Props);
    }

    /// <inheritdoc />
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        if (_strokeShape is BindableObject shape)
            SetInheritedBindingContext(shape, BindingContext);
        if (_stroke is GradientBrush || _stroke?.GetType() == typeof(SolidColorBrush))
            SetInheritedBindingContext(_stroke, BindingContext);
    }

    /// <summary>As MAUI's Border, the content sits inside the padding plus the stroke.</summary>
    private protected override Thickness ContentInset => Padding + _strokeThickness;

    /// <summary>
    /// Fills the outline with the background (a solid color or a gradient; registered as <see cref="SkUiView.PaintBackground"/>).
    /// Subclasses may call or re-register this painter.
    /// </summary>
    protected void PaintBorderBackground(SKCanvas canvas)
    {
        var fill = ResolveBackgroundPaint();
        if (!SkUiShapePainter.IsVisible(fill)) return;
        SkUiShapePainter.Fill(canvas, Outline(), fill, new SKRect(0, 0, (float)Width, (float)Height));
    }

    /// <inheritdoc />
    /// <remarks>An opaque background casts the shadow from the outline (with an opaque stroke, its outer edge), as MAUI on Android.</remarks>
    internal override SKPath? CreateShadowOutline(float width, float height)
    {
        if (!ReferenceEquals(PaintBackground, _backgroundPainter) || !ReferenceEquals(PaintOverlay, _overlayPainter))
            return null;
        Paint? stroke = _stroke;
        return SkUiBorderGeometry.ShadowOutline(Outline(), ResolveBackgroundPaint(), stroke, _strokeThickness);
    }

    /// <summary>
    /// Strokes the outline, registered as <see cref="SkUiView.PaintOverlay"/> so opaque content cannot cover the border.
    /// </summary>
    protected void PaintBorderOverlay(SKCanvas canvas)
    {
        Paint? stroke = _stroke;
        if (_strokeThickness <= 0 || !SkUiShapePainter.IsVisible(stroke)) return;
        SkUiShapePainter.Stroke(canvas, Outline(), stroke, new SKRect(0, 0, (float)Width, (float)Height), StrokeStyle());
    }

    private SKPath Outline() => _geometry.Outline(_strokeShape, _cornerRadius, (float)Width, (float)Height, _strokeThickness);

    private SkUiStrokeStyle StrokeStyle()
    {
        var dashArray = StrokeDashArray;
        _dashListener.Listen(dashArray); // the default collection is created on first read
        float[]? dashes = null;
        if (dashArray is { Count: > 0 })
        {
            dashes = new float[dashArray.Count];
            for (var index = 0; index < dashes.Length; index++)
                dashes[index] = (float)dashArray[index];
        }
        return new SkUiStrokeStyle(_strokeThickness, dashes, _strokeDashOffset,
            _strokeLineCap switch { PenLineCap.Round => LineCap.Round, PenLineCap.Square => LineCap.Square, _ => LineCap.Butt },
            _strokeLineJoin switch { PenLineJoin.Round => LineJoin.Round, PenLineJoin.Bevel => LineJoin.Bevel, _ => LineJoin.Miter },
            _strokeMiterLimit);
    }

    /// <inheritdoc />
    internal override CornerRadius PressEffectCornerRadii => _strokeShape switch
    {
        null => _cornerRadius,
        RoundRectangle shape => shape.CornerRadius,
        SkUiRoundRectangle shape => shape.CornerRadius,
        _ => default
    };

    /// <summary>Clips children to the inside of the stroke (applied by the compositor).</summary>
    internal override void OnGetRenderProps(ref MauiSkiaUi.Rendering.SkUiRenderProps props) =>
        props.ChildrenClipPath = _geometry.ContentClip(_strokeShape, _cornerRadius, props.Width, props.Height, _strokeThickness);
}
