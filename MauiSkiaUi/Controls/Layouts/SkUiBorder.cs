using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>A single-child host with a drawn rounded rectangle fill/border, similar to MAUI's Border.</summary>
/// <remarks>
/// Shape is a rounded rectangle with independent per-corner radii (MAUI <see cref="CornerRadius"/>).
/// Arbitrary <c>IShape</c> / <c>StrokeShape</c> is not implemented.
/// </remarks>
public class SkUiBorder : SkUiContentView
{
    private Color? _stroke;
    private double _strokeThickness = 1;
    private CornerRadius _cornerRadius = new(6);

    /// <summary>Bindable border color; null paints no border.</summary>
    public static readonly BindableProperty StrokeProperty = BindableProperty.Create(nameof(Stroke), typeof(Color), typeof(SkUiBorder), null,
        propertyChanged: (view, _, value) => ((SkUiBorder)view).SetStroke((Color?)value));
    /// <summary>Bindable border thickness in DIPs.</summary>
    public static readonly BindableProperty StrokeThicknessProperty = BindableProperty.Create(nameof(StrokeThickness), typeof(double), typeof(SkUiBorder), 1d,
        propertyChanged: (view, _, value) => ((SkUiBorder)view).SetStrokeThickness((double)value));
    /// <summary>Bindable per-corner radii in DIPs (top-left, top-right, bottom-left, bottom-right).</summary>
    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(nameof(CornerRadius), typeof(CornerRadius), typeof(SkUiBorder), new CornerRadius(6),
        propertyChanged: (view, _, value) => ((SkUiBorder)view).SetCornerRadius((CornerRadius)value));

    /// <summary>Border color; null paints no border.</summary>
    public Color? Stroke { get => _stroke; set => SetValue(StrokeProperty, value); }
    /// <summary>Border thickness in DIPs.</summary>
    public double StrokeThickness { get => _strokeThickness; set => SetValue(StrokeThicknessProperty, value); }
    /// <summary>
    /// Per-corner radii in DIPs. XAML accepts a uniform value or <c>tl,tr,bl,br</c>;
    /// a uniform <see cref="double"/> assigns via implicit conversion.
    /// </summary>
    public CornerRadius CornerRadius { get => _cornerRadius; set => SetValue(CornerRadiusProperty, value); }

    /// <summary>Sets the border color without bindable write-back.</summary>
    public SkUiBorder SetStroke(Color? value) { _stroke = value; InvalidatePaint(); return this; }
    /// <summary>Sets the border thickness without bindable write-back.</summary>
    public SkUiBorder SetStrokeThickness(double value) { ArgumentOutOfRangeException.ThrowIfNegative(value); _strokeThickness = value; InvalidatePaint(); return this; }
    /// <summary>Sets a uniform corner radius without bindable write-back.</summary>
    public SkUiBorder SetCornerRadius(double uniformRadius) => SetCornerRadius(new CornerRadius(uniformRadius));
    /// <summary>Sets independent corner radii without bindable write-back.</summary>
    public SkUiBorder SetCornerRadius(CornerRadius value)
    {
        ValidateCornerRadius(value);
        _cornerRadius = value;
        InvalidatePaint();
        return this;
    }

    /// <summary>Creates a border that paints fill in <see cref="SkUiView.PaintBackground"/> and stroke in <see cref="SkUiView.PaintOverlay"/> (after content).</summary>
    public SkUiBorder()
    {
        SetPaintBackground(PaintBorderBackground);
        SetPaintOverlay(PaintBorderOverlay);
    }

    /// <summary>Draws the rounded fill registered as <see cref="SkUiView.PaintBackground"/>. Subclasses may call or re-register this painter.</summary>
    protected void PaintBorderBackground(SKCanvas canvas)
    {
        var fill = ResolveSolidBackgroundColor() ?? Colors.Transparent;
        var bounds = new SKRect(0, 0, (float)Width, (float)Height);
        SkUiLook.Current.DrawRoundedBox(canvas, bounds, _cornerRadius, ToSkColor(fill), SKColors.Transparent, 0);
    }

    /// <summary>
    /// Draws the stroke registered as <see cref="SkUiView.PaintOverlay"/> so opaque content cannot cover the border.
    /// </summary>
    protected void PaintBorderOverlay(SKCanvas canvas)
    {
        if (_stroke is null || _strokeThickness <= 0) return;
        var bounds = new SKRect(0, 0, (float)Width, (float)Height);
        SkUiLook.Current.DrawRoundedBox(canvas, bounds, _cornerRadius, SKColors.Transparent, ToSkColor(_stroke), (float)_strokeThickness);
    }

    /// <summary>Clips content to the same rounded-rect geometry as the fill/border.</summary>
    protected override void OnPaintContent(SKCanvas canvas)
    {
        var saveCount = canvas.Save();
        try
        {
            using var clip = SkUiLook.Current.CreateRoundRectPath(new SKRect(0, 0, (float)Width, (float)Height), _cornerRadius);
            canvas.ClipPath(clip, antialias: true);
            base.OnPaintContent(canvas);
        }
        finally { canvas.RestoreToCount(saveCount); }
    }

    private static void ValidateCornerRadius(CornerRadius value)
    {
        if (value.TopLeft < 0 || value.TopRight < 0 || value.BottomLeft < 0 || value.BottomRight < 0)
            throw new ArgumentOutOfRangeException(nameof(value), value, "Corner radii must be non-negative.");
    }
}
