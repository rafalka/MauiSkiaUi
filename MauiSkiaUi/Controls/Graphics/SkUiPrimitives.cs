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

/// <summary>A filled rectangle with optional rounded corners, similar to MAUI's BoxView.</summary>
public class SkUiBox : SkUiShape
{
    private CornerRadius _cornerRadius;
    private SkUiRoundedClip _shape;

    /// <summary>Bindable <see cref="CornerRadius"/>.</summary>
    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(nameof(CornerRadius), typeof(CornerRadius), typeof(SkUiBox), default(CornerRadius),
        propertyChanged: (view, _, value) => ((SkUiBox)view).SetCornerRadius((CornerRadius)value));

    /// <summary>Per-corner radii (MAUI's BoxView <c>CornerRadius</c>; <c>"8"</c> or <c>"8,8,0,0"</c> in XAML). Larger radii than the box allows are scaled down.</summary>
    public CornerRadius CornerRadius { get => _cornerRadius; set => SetValue(CornerRadiusProperty, value); }

    /// <summary>Sets the corner radii without bindable write-back.</summary>
    public SkUiBox SetCornerRadius(CornerRadius value)
    {
        SkUiCornerRadii.Validate(value, nameof(value));
        if (_cornerRadius == value) return this;
        _cornerRadius = value;
        InvalidatePaint();
        return this;
    }

    /// <inheritdoc />
    internal override CornerRadius PressEffectCornerRadii => _cornerRadius;

    /// <inheritdoc />
    protected override void DrawShape(SKCanvas canvas, SKRect bounds, SKPaint paint)
    {
        if (SkUiCornerRadii.HasAny(_cornerRadius))
            canvas.DrawPath(_shape.Get(bounds.Width, bounds.Height, _cornerRadius), paint);
        else
            canvas.DrawRect(bounds, paint);
    }
}

/// <summary>A filled ellipse; input uses its rectangular arranged bounds.</summary>
public class SkUiEllipse : SkUiShape
{
    /// <inheritdoc />
    protected override void DrawShape(SKCanvas canvas, SKRect bounds, SKPaint paint) => canvas.DrawOval(bounds, paint);
}

/// <summary>
/// A straight line from (<see cref="X1"/>, <see cref="Y1"/>) to (<see cref="X2"/>, <see cref="Y2"/>) in local DIPs, similar to
/// MAUI's <c>Line</c>: it measures to its far end points plus the stroke, and is placed as MAUI places a shape without
/// stretch. Points are not mirrored in right-to-left layouts.
/// </summary>
public class SkUiLine : SkUiShape
{
    /// <summary>Bindable <see cref="X1"/>.</summary>
    public static readonly BindableProperty X1Property = PointProperty(nameof(X1));
    /// <summary>Bindable <see cref="Y1"/>.</summary>
    public static readonly BindableProperty Y1Property = PointProperty(nameof(Y1));
    /// <summary>Bindable <see cref="X2"/>.</summary>
    public static readonly BindableProperty X2Property = PointProperty(nameof(X2));
    /// <summary>Bindable <see cref="Y2"/>.</summary>
    public static readonly BindableProperty Y2Property = PointProperty(nameof(Y2));

    /// <summary>Creates a line; set its end points with <see cref="X1"/>, <see cref="Y1"/>, <see cref="X2"/>, <see cref="Y2"/>.</summary>
    public SkUiLine() { }

    /// <summary>Creates a line between two points, as MAUI's <c>Line(x1, y1, x2, y2)</c>.</summary>
    public SkUiLine(double x1, double y1, double x2, double y2)
    {
        X1 = x1;
        Y1 = y1;
        X2 = x2;
        Y2 = y2;
    }

    /// <summary>The start point's x coordinate.</summary>
    public double X1 { get => (double)GetValue(X1Property); set => SetValue(X1Property, value); }
    /// <summary>The start point's y coordinate.</summary>
    public double Y1 { get => (double)GetValue(Y1Property); set => SetValue(Y1Property, value); }
    /// <summary>The end point's x coordinate.</summary>
    public double X2 { get => (double)GetValue(X2Property); set => SetValue(X2Property, value); }
    /// <summary>The end point's y coordinate.</summary>
    public double Y2 { get => (double)GetValue(Y2Property); set => SetValue(Y2Property, value); }

    private static BindableProperty PointProperty(string name) => BindableProperty.Create(name, typeof(double), typeof(SkUiLine), 0d,
        validateValue: (_, value) => value is double coordinate && double.IsFinite(coordinate),
        propertyChanged: (bindable, _, _) => ((SkUiLine)bindable).InvalidateMeasureOverride());

    /// <inheritdoc />
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(StrokeWidth))
            InvalidateMeasureOverride();
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        SkUiShapeGeometry.MeasureLine(X1, Y1, X2, Y2, StrokeWidth);

    /// <inheritdoc />
    protected override void DrawShape(SKCanvas canvas, SKRect bounds, SKPaint paint) =>
        SkUiShapeGeometry.DrawLine(canvas, bounds, (float)X1, (float)Y1, (float)X2, (float)Y2, paint);
}