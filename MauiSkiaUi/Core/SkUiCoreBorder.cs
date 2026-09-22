using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// Single-child host with a drawn rounded fill/border (Core analogue of <c>SkUiBorder</c>).
/// Shape is a rounded rectangle with independent per-corner radii; arbitrary MAUI <c>IShape</c> is not supported.
/// </summary>
public class SkUiCoreBorder : SkUiCoreContentView
{
    private Color? _stroke;
    private double _strokeThickness = 1;
    private CornerRadius _cornerRadius = new(6);
    private Color _backgroundColor = Colors.Transparent;

    /// <summary>Creates a border that paints fill in <see cref="SkUiCoreNode.PaintBackground"/> and stroke in <see cref="SkUiCoreNode.PaintOverlay"/> (after content).</summary>
    public SkUiCoreBorder()
    {
        SetPaintBackground(PaintBorderBackground);
        SetPaintOverlay(PaintBorderOverlay);
    }

    /// <summary>Solid fill behind content; transparent by default.</summary>
    public Color BackgroundColor
    {
        get => _backgroundColor;
        set => SetBackgroundColor(value);
    }

    /// <summary>Border color; <c>null</c> paints no border.</summary>
    public Color? Stroke
    {
        get => _stroke;
        set => SetStroke(value);
    }

    /// <summary>Border thickness in DIPs.</summary>
    public double StrokeThickness
    {
        get => _strokeThickness;
        set => SetStrokeThickness(value);
    }

    /// <summary>
    /// Per-corner radii in DIPs (top-left, top-right, bottom-left, bottom-right).
    /// A uniform <see cref="double"/> assigns implicitly via <see cref="CornerRadius"/>.
    /// </summary>
    public CornerRadius CornerRadius
    {
        get => _cornerRadius;
        set => SetCornerRadius(value);
    }

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

    /// <summary>Sets the border color; <c>null</c> paints no border.</summary>
    public SkUiCoreBorder SetStroke(Color? value)
    {
        if (!SetProperty(ref _stroke, value, nameof(Stroke))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets the border thickness in DIPs.</summary>
    public SkUiCoreBorder SetStrokeThickness(double value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        if (!SetProperty(ref _strokeThickness, value, nameof(StrokeThickness))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets a uniform corner radius in DIPs for all four corners.</summary>
    public SkUiCoreBorder SetCornerRadius(double uniformRadius) =>
        SetCornerRadius(new CornerRadius(uniformRadius));

    /// <summary>Sets independent corner radii in DIPs.</summary>
    public SkUiCoreBorder SetCornerRadius(CornerRadius value)
    {
        ValidateCornerRadius(value);
        if (!SetProperty(ref _cornerRadius, value, nameof(CornerRadius))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Draws the rounded fill registered as <see cref="SkUiCoreNode.PaintBackground"/>. Subclasses may call or re-register this painter.</summary>
    protected void PaintBorderBackground(SKCanvas canvas)
    {
        var bounds = new SKRect(0, 0, (float)Frame.Width, (float)Frame.Height);
        SkUiLook.Current.DrawRoundedBox(
            canvas,
            bounds,
            _cornerRadius,
            ToSkColor(_backgroundColor),
            SKColors.Transparent,
            0);
    }

    /// <summary>
    /// Draws the stroke registered as <see cref="SkUiCoreNode.PaintOverlay"/> so opaque content cannot cover the border.
    /// </summary>
    protected void PaintBorderOverlay(SKCanvas canvas)
    {
        if (_stroke is null || _strokeThickness <= 0) return;
        var bounds = new SKRect(0, 0, (float)Frame.Width, (float)Frame.Height);
        SkUiLook.Current.DrawRoundedBox(
            canvas,
            bounds,
            _cornerRadius,
            SKColors.Transparent,
            ToSkColor(_stroke),
            (float)_strokeThickness);
    }

    /// <summary>Clips content to the same rounded-rect geometry as the fill/border.</summary>
    protected override void OnPaintContent(SKCanvas canvas)
    {
        var save = canvas.Save();
        try
        {
            using var clip = SkUiLook.Current.CreateRoundRectPath(
                new SKRect(0, 0, (float)Frame.Width, (float)Frame.Height),
                _cornerRadius);
            canvas.ClipPath(clip, antialias: true);
            base.OnPaintContent(canvas);
        }
        finally
        {
            canvas.RestoreToCount(save);
        }
    }

    private static void ValidateCornerRadius(CornerRadius value)
    {
        if (value.TopLeft < 0 || value.TopRight < 0 || value.BottomLeft < 0 || value.BottomRight < 0)
            throw new ArgumentOutOfRangeException(nameof(value), value, "Corner radii must be non-negative.");
    }
}
