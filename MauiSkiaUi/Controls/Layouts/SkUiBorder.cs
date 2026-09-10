using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>A single-child host with a drawn rounded rectangle fill/border, similar to MAUI's Border.</summary>
/// <remarks>Only rounded-rectangle shapes are supported in v1; arbitrary <c>IShape</c> strokes (MAUI's full <c>StrokeShape</c>) are not implemented.</remarks>
public class SkUiBorder : SkUiContentView
{
    private Color? _stroke;
    private double _strokeThickness = 1;
    private double _cornerRadius = 6;

    /// <summary>Bindable border color; null paints no border.</summary>
    public static readonly BindableProperty StrokeProperty = BindableProperty.Create(nameof(Stroke), typeof(Color), typeof(SkUiBorder), null,
        propertyChanged: (view, _, value) => ((SkUiBorder)view).SetStroke((Color?)value));
    /// <summary>Bindable border thickness in DIPs.</summary>
    public static readonly BindableProperty StrokeThicknessProperty = BindableProperty.Create(nameof(StrokeThickness), typeof(double), typeof(SkUiBorder), 1d,
        propertyChanged: (view, _, value) => ((SkUiBorder)view).SetStrokeThickness((double)value));
    /// <summary>Bindable corner radius in DIPs, applied to all four corners.</summary>
    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(nameof(CornerRadius), typeof(double), typeof(SkUiBorder), 6d,
        propertyChanged: (view, _, value) => ((SkUiBorder)view).SetCornerRadius((double)value));

    /// <summary>Border color; null paints no border.</summary>
    public Color? Stroke { get => _stroke; set => SetValue(StrokeProperty, value); }
    /// <summary>Border thickness in DIPs.</summary>
    public double StrokeThickness { get => _strokeThickness; set => SetValue(StrokeThicknessProperty, value); }
    /// <summary>Corner radius in DIPs.</summary>
    public double CornerRadius { get => _cornerRadius; set => SetValue(CornerRadiusProperty, value); }

    /// <summary>Sets the border color without bindable write-back.</summary>
    public SkUiBorder SetStroke(Color? value) { _stroke = value; InvalidatePaint(); return this; }
    /// <summary>Sets the border thickness without bindable write-back.</summary>
    public SkUiBorder SetStrokeThickness(double value) { ArgumentOutOfRangeException.ThrowIfNegative(value); _strokeThickness = value; InvalidatePaint(); return this; }
    /// <summary>Sets the corner radius without bindable write-back.</summary>
    public SkUiBorder SetCornerRadius(double value) { ArgumentOutOfRangeException.ThrowIfNegative(value); _cornerRadius = value; InvalidatePaint(); return this; }

    /// <inheritdoc />
    protected override void OnPaintBackground(SKCanvas canvas)
    {
        var fill = (Background as SolidColorBrush)?.Color ?? BackgroundColor ?? Colors.Transparent;
        SkUiChrome.DrawRoundedBox(canvas, new SKRect(0, 0, (float)Width, (float)Height), (float)_cornerRadius,
            ToSkColor(fill), ToSkColor(_stroke ?? Colors.Transparent), (float)(_stroke is null ? 0 : _strokeThickness));
    }

    /// <summary>Clips content to the same rounded-rect geometry as the fill/border.</summary>
    protected override void OnPaintContent(SKCanvas canvas)
    {
        var saveCount = canvas.Save();
        try
        {
            using var clip = SkUiChrome.CreateRoundRectPath(new SKRect(0, 0, (float)Width, (float)Height), (float)_cornerRadius);
            canvas.ClipPath(clip, antialias: true);
            base.OnPaintContent(canvas);
        }
        finally { canvas.RestoreToCount(saveCount); }
    }
}
