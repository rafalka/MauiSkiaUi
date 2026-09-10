using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>Shared bindable color and stroke settings for proof-of-concept primitives.</summary>
public abstract class SkUiShape : SkUiView
{
    /// <summary>The primitive's fill or line color.</summary>
    public static readonly BindableProperty ColorProperty = BindableProperty.Create(
        nameof(Color), typeof(Color), typeof(SkUiShape), Colors.Teal,
        propertyChanged: (bindable, _, _) => ((SkUiShape)bindable).InvalidatePaint());

    /// <summary>The stroke width, in DIPs.</summary>
    public static readonly BindableProperty StrokeWidthProperty = BindableProperty.Create(
        nameof(StrokeWidth), typeof(double), typeof(SkUiShape), 2d,
        validateValue: (_, value) => value is double width && double.IsFinite(width) && width >= 0,
        propertyChanged: (bindable, _, _) => ((SkUiShape)bindable).InvalidatePaint());

    /// <summary>The primitive's fill or stroke color.</summary>
    public Color Color
    {
        get => (Color)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    /// <summary>The line thickness in DIPs; ignored by filled primitives.</summary>
    public double StrokeWidth
    {
        get => (double)GetValue(StrokeWidthProperty);
        set => SetValue(StrokeWidthProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => new(48, 48);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        using var paint = new SKPaint { Color = ToSkColor(Color), IsAntialias = true, StrokeWidth = (float)StrokeWidth };
        DrawShape(canvas, new SKRect(0, 0, (float)Width, (float)Height), paint);
    }

    /// <summary>Draws the primitive into its local bounds with a scoped paint.</summary>
    protected abstract void DrawShape(SKCanvas canvas, SKRect bounds, SKPaint paint);
}

/// <summary>A filled rectangular primitive.</summary>
public class SkUiBox : SkUiShape
{
    /// <inheritdoc />
    protected override void DrawShape(SKCanvas canvas, SKRect bounds, SKPaint paint) => canvas.DrawRect(bounds, paint);
}

/// <summary>A filled ellipse; input uses its rectangular arranged bounds.</summary>
public class SkUiEllipse : SkUiShape
{
    /// <inheritdoc />
    protected override void DrawShape(SKCanvas canvas, SKRect bounds, SKPaint paint) => canvas.DrawOval(bounds, paint);
}

/// <summary>A diagonal line from top-left to bottom-right, inset by half the stroke width.</summary>
public class SkUiLine : SkUiShape
{
    /// <inheritdoc />
    protected override void DrawShape(SKCanvas canvas, SKRect bounds, SKPaint paint)
    {
        paint.Style = SKPaintStyle.Stroke;
        var inset = Math.Min((float)StrokeWidth / 2, Math.Min(bounds.Width, bounds.Height) / 2);
        canvas.DrawLine(inset, inset, bounds.Right - inset, bounds.Bottom - inset, paint);
    }
}