using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// Single-child host with a drawn rounded fill/border (Core analogue of <c>SkUiBorder</c>).
/// Only rounded-rectangle shapes are supported.
/// </summary>
public class SkUiCoreBorder : SkUiCoreContentView
{
    private Color? _stroke;
    private double _strokeThickness = 1;
    private double _cornerRadius = 6;
    private Color _backgroundColor = Colors.Transparent;

    /// <summary>Creates a border that paints chrome via <see cref="SkUiCoreNode.PaintBackground"/>.</summary>
    public SkUiCoreBorder() => SetPaintBackground(PaintBorderBackground);

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

    /// <summary>Corner radius in DIPs, applied to all four corners.</summary>
    public double CornerRadius
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

    /// <summary>Sets the corner radius in DIPs.</summary>
    public SkUiCoreBorder SetCornerRadius(double value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        if (!SetProperty(ref _cornerRadius, value, nameof(CornerRadius))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Draws the rounded fill/border registered as <see cref="SkUiCoreNode.PaintBackground"/>. Subclasses may call or re-register this painter.</summary>
    protected void PaintBorderBackground(SKCanvas canvas)
    {
        SkUiLook.Current.DrawRoundedBox(
            canvas,
            new SKRect(0, 0, (float)Frame.Width, (float)Frame.Height),
            (float)_cornerRadius,
            ToSkColor(_backgroundColor),
            ToSkColor(_stroke ?? Colors.Transparent),
            (float)(_stroke is null ? 0 : _strokeThickness));
    }

    /// <summary>Clips content to the same rounded-rect geometry as the fill/border.</summary>
    protected override void OnPaintContent(SKCanvas canvas)
    {
        var save = canvas.Save();
        try
        {
            using var clip = SkUiLook.Current.CreateRoundRectPath(
                new SKRect(0, 0, (float)Frame.Width, (float)Frame.Height),
                (float)_cornerRadius);
            canvas.ClipPath(clip, antialias: true);
            base.OnPaintContent(canvas);
        }
        finally
        {
            canvas.RestoreToCount(save);
        }
    }
}
