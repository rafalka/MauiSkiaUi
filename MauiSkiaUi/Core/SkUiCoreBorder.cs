using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// Single-child host with a drawn outline (Core analogue of <c>SkUiBorder</c>, MAUI's <c>Border</c>): the background fills
/// the <see cref="StrokeShape"/> (a Core shape such as <see cref="SkUiCoreRoundRectangle"/>, or any MAUI Graphics
/// <see cref="IShape"/>; without one, a rectangle rounded by <see cref="CornerRadius"/>), the <see cref="Stroke"/> paint
/// outlines it with dashes, caps and joins, and the content sits inside <see cref="SkUiCoreContentView.Padding"/> plus the
/// stroke and is clipped to the stroke's inner edge. Drawn by the same geometry as the SkUi* border.
/// </summary>
public class SkUiCoreBorder : SkUiCoreContentView
{
    private Paint? _stroke;
    private double _strokeThickness = 1;
    private double[] _strokeDashArray = [];
    private double _strokeDashOffset;
    private LineCap _strokeLineCap = LineCap.Butt;
    private LineJoin _strokeLineJoin = LineJoin.Miter;
    private double _strokeMiterLimit = 10;
    private CornerRadius _cornerRadius;
    private IShape? _strokeShape;
    private Color _backgroundColor = Colors.Transparent;
    private readonly SkUiBorderGeometry _geometry = new();
    private readonly SkUiWeakListener<SkUiCoreBorder> _shapeListener;

    /// <summary>Creates a border that paints fill in <see cref="SkUiCoreNode.PaintBackground"/> and stroke in <see cref="SkUiCoreNode.PaintOverlay"/> (after content).</summary>
    public SkUiCoreBorder()
    {
        _shapeListener = new(this, static (border, _) => { border._geometry.Invalidate(); border.InvalidateShape(); });
        _backgroundPainter = PaintBorderBackground;
        _overlayPainter = PaintBorderOverlay;
        SetPaintBackground(_backgroundPainter);
        SetPaintOverlay(_overlayPainter);
    }

    private readonly Action<SKCanvas> _backgroundPainter;
    private readonly Action<SKCanvas> _overlayPainter;

    /// <summary>Solid fill of the outline; transparent by default. A set <see cref="SkUiCoreNode.Background"/> (solid or gradient) replaces it.</summary>
    public Color BackgroundColor { get => _backgroundColor; set => SetBackgroundColor(value); }

    /// <summary>
    /// The shape of the outline: a Core shape (<see cref="SkUiCoreRoundRectangle"/>, <see cref="SkUiCoreEllipse"/>,
    /// <see cref="SkUiCorePath"/>, …) or any MAUI Graphics <see cref="IShape"/>. <c>null</c> (default): a rectangle rounded by
    /// <see cref="CornerRadius"/>. Changes of a Core shape's properties redraw the border.
    /// </summary>
    public IShape? StrokeShape { get => _strokeShape; set => SetStrokeShape(value); }

    /// <summary>The paint of the outline (<c>null</c>: none).</summary>
    public Paint? Stroke { get => _stroke; set => SetStroke(value); }

    /// <summary>The outline width in DIPs (1 by default, as MAUI); the content is inset by it.</summary>
    public double StrokeThickness { get => _strokeThickness; set => SetStrokeThickness(value); }

    /// <summary>Dashes and gaps of the outline, in multiples of <see cref="StrokeThickness"/> (empty: solid).</summary>
    public IReadOnlyList<double> StrokeDashArray { get => _strokeDashArray; set => SetStrokeDashArray([.. value]); }

    /// <summary>Where the dash pattern starts, in multiples of <see cref="StrokeThickness"/>.</summary>
    public double StrokeDashOffset { get => _strokeDashOffset; set => SetStrokeDashOffset(value); }

    /// <summary>The caps of the dashes (<see cref="LineCap.Butt"/>: MAUI's <c>Flat</c>).</summary>
    public LineCap StrokeLineCap { get => _strokeLineCap; set => SetStrokeLineCap(value); }

    /// <summary>The joins at the corners of the outline.</summary>
    public LineJoin StrokeLineJoin { get => _strokeLineJoin; set => SetStrokeLineJoin(value); }

    /// <summary>The limit on the ratio of a miter join's length to half the stroke thickness (10 by default).</summary>
    public double StrokeMiterLimit { get => _strokeMiterLimit; set => SetStrokeMiterLimit(value); }

    /// <summary>
    /// Per-corner radii in DIPs (top-left, top-right, bottom-left, bottom-right) of the outline when no
    /// <see cref="StrokeShape"/> is set; 0 by default (a rectangle, as MAUI's default shape).
    /// </summary>
    public CornerRadius CornerRadius { get => _cornerRadius; set => SetCornerRadius(value); }

    /// <inheritdoc cref="SkUiCoreContentView.SetPadding" />
    public new SkUiCoreBorder SetPadding(Thickness value)
    {
        base.SetPadding(value);
        return this;
    }

    /// <inheritdoc cref="SkUiCoreContentView.SetContent" />
    public new SkUiCoreBorder SetContent(SkUiCoreNode? value)
    {
        base.SetContent(value);
        return this;
    }

    /// <summary>Sets the fill color.</summary>
    public SkUiCoreBorder SetBackgroundColor(Color value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!SetProperty(ref _backgroundColor, value, nameof(BackgroundColor))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets the outline shape (<c>null</c>: a rectangle rounded by <see cref="CornerRadius"/>).</summary>
    public SkUiCoreBorder SetStrokeShape(IShape? value)
    {
        if (ReferenceEquals(_strokeShape, value)) return this;
        _strokeShape = value;
        _shapeListener.Listen(value); // a shared shape must not keep the border alive
        OnPropertyChanged(nameof(StrokeShape));
        InvalidateShape();
        return this;
    }

    /// <summary>Sets the outline paint (<c>null</c>: none).</summary>
    public SkUiCoreBorder SetStroke(Paint? value)
    {
        if (!SetProperty(ref _stroke, value, nameof(Stroke))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets a solid outline color.</summary>
    public SkUiCoreBorder SetStroke(Color value) => _stroke is SolidPaint { Color: var color } && color == value ? this : SetStroke(new SolidPaint(value));

    /// <summary>Sets the outline width in DIPs.</summary>
    public SkUiCoreBorder SetStrokeThickness(double value)
    {
        SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value));
        if (!SetProperty(ref _strokeThickness, value, nameof(StrokeThickness))) return this;
        InvalidateMeasure(); // the content is inset by the stroke
        InvalidateRender(Rendering.SkUiRenderDirty.Props);
        return this;
    }

    /// <summary>Sets the dash pattern, in multiples of the stroke thickness (no values: solid).</summary>
    public SkUiCoreBorder SetStrokeDashArray(params double[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (_strokeDashArray.AsSpan().SequenceEqual(value)) return this;
        _strokeDashArray = [.. value];
        OnPropertyChanged(nameof(StrokeDashArray));
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets the dash offset, in multiples of the stroke thickness.</summary>
    public SkUiCoreBorder SetStrokeDashOffset(double value)
    {
        SkUiValidate.ThrowIfNotFinite(value, nameof(value));
        if (SetProperty(ref _strokeDashOffset, value, nameof(StrokeDashOffset))) InvalidatePaint();
        return this;
    }

    /// <summary>Sets the dash caps.</summary>
    public SkUiCoreBorder SetStrokeLineCap(LineCap value)
    {
        if (SetProperty(ref _strokeLineCap, value, nameof(StrokeLineCap))) InvalidatePaint();
        return this;
    }

    /// <summary>Sets the corner joins.</summary>
    public SkUiCoreBorder SetStrokeLineJoin(LineJoin value)
    {
        if (SetProperty(ref _strokeLineJoin, value, nameof(StrokeLineJoin))) InvalidatePaint();
        return this;
    }

    /// <summary>Sets the miter limit.</summary>
    public SkUiCoreBorder SetStrokeMiterLimit(double value)
    {
        SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value));
        if (SetProperty(ref _strokeMiterLimit, value, nameof(StrokeMiterLimit))) InvalidatePaint();
        return this;
    }

    /// <summary>Sets a uniform corner radius in DIPs for all four corners.</summary>
    public SkUiCoreBorder SetCornerRadius(double uniformRadius) => SetCornerRadius(new CornerRadius(uniformRadius));

    /// <summary>Sets independent corner radii in DIPs.</summary>
    public SkUiCoreBorder SetCornerRadius(CornerRadius value)
    {
        SkUiCornerRadii.Validate(value, nameof(value));
        if (SetProperty(ref _cornerRadius, value, nameof(CornerRadius))) InvalidateShape();
        return this;
    }

    private void InvalidateShape()
    {
        InvalidatePaint();
        InvalidateRender(Rendering.SkUiRenderDirty.Props);
    }

    /// <summary>As MAUI's Border, the content sits inside the padding plus the stroke.</summary>
    private protected override Thickness ContentInset => Padding + _strokeThickness;

    /// <inheritdoc />
    internal override CornerRadius PressEffectCornerRadii => _strokeShape switch
    {
        null => _cornerRadius,
        SkUiCoreRoundRectangle shape => shape.CornerRadius,
        _ => default
    };

    /// <summary>
    /// Fills the outline with <see cref="SkUiCoreNode.Background"/> (solid or gradient), else <see cref="BackgroundColor"/>
    /// (registered as <see cref="SkUiCoreNode.PaintBackground"/>). Subclasses may call or re-register this painter.
    /// </summary>
    protected void PaintBorderBackground(SKCanvas canvas)
    {
        var fill = BorderFill;
        if (!fill.IsVisible) return;
        SkUiShapePainter.Fill(canvas, Outline(), fill, new SKRect(0, 0, (float)Frame.Width, (float)Frame.Height));
    }

    private SkUiFill BorderFill => Background is { } background ? SkUiFill.From(background) : SkUiFill.From(_backgroundColor);

    /// <inheritdoc />
    /// <remarks>An opaque fill casts the shadow from the outline (with an opaque stroke, its outer edge), as MAUI on Android.</remarks>
    internal override SKPath? CreateShadowOutline(float width, float height) =>
        ReferenceEquals(PaintBackground, _backgroundPainter) && ReferenceEquals(PaintOverlay, _overlayPainter)
            ? SkUiBorderGeometry.ShadowOutline(Outline(), BorderFill.ToPaint(), _stroke, _strokeThickness)
            : null;

    /// <summary>
    /// Strokes the outline, registered as <see cref="SkUiCoreNode.PaintOverlay"/> so opaque content cannot cover the border.
    /// </summary>
    protected void PaintBorderOverlay(SKCanvas canvas)
    {
        if (_strokeThickness <= 0 || !SkUiShapePainter.IsVisible(_stroke)) return;
        float[]? dashes = _strokeDashArray.Length == 0 ? null : Array.ConvertAll(_strokeDashArray, value => (float)value);
        SkUiShapePainter.Stroke(canvas, Outline(), _stroke, new SKRect(0, 0, (float)Frame.Width, (float)Frame.Height),
            new SkUiStrokeStyle(_strokeThickness, dashes, _strokeDashOffset, _strokeLineCap, _strokeLineJoin, _strokeMiterLimit));
    }

    private SKPath Outline() => _geometry.Outline(_strokeShape, _cornerRadius, (float)Frame.Width, (float)Frame.Height, _strokeThickness);

    /// <summary>Clips children to the inside of the stroke (applied by the compositor).</summary>
    internal override void OnGetRenderProps(ref MauiSkiaUi.Rendering.SkUiRenderProps props) =>
        props.ChildrenClipPath = _geometry.ContentClip(_strokeShape, _cornerRadius, props.Width, props.Height, _strokeThickness);
}
