using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>Shared fill/stroke settings for Core shape primitives.</summary>
public abstract class SkUiCoreShape : SkUiCoreNode
{
    private Color _color = Colors.Teal;
    private double _strokeWidth = 2;

    /// <summary>Fill or stroke color.</summary>
    public Color Color
    {
        get => _color;
        set => SetColor(value);
    }

    /// <summary>Stroke width in DIPs; ignored by filled primitives.</summary>
    public double StrokeWidth
    {
        get => _strokeWidth;
        set => SetStrokeWidth(value);
    }

    /// <summary>Sets the fill or stroke color.</summary>
    public SkUiCoreShape SetColor(Color value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!SetProperty(ref _color, value, nameof(Color))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets the stroke width in DIPs.</summary>
    public SkUiCoreShape SetStrokeWidth(double value)
    {
        if (!double.IsFinite(value) || value < 0)
            throw new ArgumentOutOfRangeException(nameof(value));
        if (!SetProperty(ref _strokeWidth, value, nameof(StrokeWidth))) return this;
        InvalidatePaint();
        return this;
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => new(48, 48);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        using var paint = new SKPaint
        {
            Color = ToSkColor(_color),
            IsAntialias = true,
            StrokeWidth = (float)_strokeWidth
        };
        DrawShape(canvas, new SKRect(0, 0, (float)Frame.Width, (float)Frame.Height), paint);
    }

    /// <summary>Draws the primitive into its local bounds with a scoped paint.</summary>
    protected abstract void DrawShape(SKCanvas canvas, SKRect bounds, SKPaint paint);
}

/// <summary>Filled rectangular Core primitive.</summary>
public class SkUiCoreBox : SkUiCoreShape
{
    /// <inheritdoc />
    protected override void DrawShape(SKCanvas canvas, SKRect bounds, SKPaint paint) =>
        canvas.DrawRect(bounds, paint);
}

/// <summary>Filled ellipse; input uses rectangular arranged bounds.</summary>
public class SkUiCoreEllipse : SkUiCoreShape
{
    /// <inheritdoc />
    protected override void DrawShape(SKCanvas canvas, SKRect bounds, SKPaint paint) =>
        canvas.DrawOval(bounds, paint);
}

/// <summary>Diagonal line from top-left to bottom-right, inset by half the stroke width.</summary>
public class SkUiCoreLine : SkUiCoreShape
{
    /// <inheritdoc />
    protected override void DrawShape(SKCanvas canvas, SKRect bounds, SKPaint paint)
    {
        paint.Style = SKPaintStyle.Stroke;
        var inset = Math.Min((float)StrokeWidth / 2, Math.Min(bounds.Width, bounds.Height) / 2);
        canvas.DrawLine(inset, inset, bounds.Right - inset, bounds.Bottom - inset, paint);
    }
}
