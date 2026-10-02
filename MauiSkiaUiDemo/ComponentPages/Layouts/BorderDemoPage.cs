using MauiSkiaUi;
using NativeShapes = Microsoft.Maui.Controls.Shapes;

namespace MauiSkiaUiDemo;

/// <summary>Side-by-side property playground for <see cref="SkUiBorder"/>.</summary>
public sealed class BorderDemoPage : ComponentDemoPage
{
    private static readonly string[] ShapeNames = ["CornerRadius", "Rectangle", "RoundRectangle 24,4,4,24", "Ellipse", "Ticket (path)"];
    private static readonly string[] DashNames = ["Solid", "4 2", "0 2 (dots)"];
    private const string Ticket = "M 0,10 A 10,10 0 0 0 10,0 L 90,0 A 10,10 0 0 0 100,10 L 100,40 A 10,10 0 0 0 100,60 L 100,90 A 10,10 0 0 0 90,100 L 10,100 A 10,10 0 0 0 0,90 L 0,60 A 10,10 0 0 0 0,40 Z";

    private string _shape = "CornerRadius";
    private double _cornerRadius = 10;

    public BorderDemoPage() : base(nameof(SkUiBorder), new SkUiBorder(), new Border())
    {
        var skia = (SkUiBorder)SkiaControl;
        var native = (Border)NativeControl!;
        // Contrasting label fill so the padding and the stroke inset are visible against the border's background.
        var contentFill = DemoColors.SampleB;
        skia.Content = new SkUiLabel
        {
            Text = "Bordered", Background = contentFill, TextColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center,
        };
        native.Content = new Label
        {
            Text = "Bordered", Background = contentFill, TextColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center,
        };
        ColorEditor(nameof(VisualElement.Background), DemoColors.SoftSurface, value =>
        {
            skia.Background = value;
            native.Background = value;
        }, () => ((SolidColorBrush)skia.Background).Color, () => ((SolidColorBrush)native.Background).Color);
        var gradient = new LinearGradientBrush([new GradientStop(Accent, 0), new GradientStop(DemoColors.SampleA, 1)], new Point(0, 0), new Point(1, 0));
        Toggle("Stroke gradient", false, value =>
        {
            skia.Stroke = value ? gradient : Accent;
            native.Stroke = value ? gradient : Accent;
        }, () => skia.Stroke is LinearGradientBrush, () => native.Stroke is LinearGradientBrush);
        Number(nameof(SkUiBorder.StrokeThickness), 0, 12, 3, value => { skia.StrokeThickness = value; native.StrokeThickness = value; }, () => skia.StrokeThickness, () => native.StrokeThickness);
        Number(nameof(SkUiBorder.Padding), 0, 28, 12, value => { skia.Padding = value; native.Padding = value; }, () => skia.Padding.Left, () => native.Padding.Left);
        Choice(nameof(SkUiBorder.StrokeShape), ShapeNames, _shape, name => { _shape = name; ApplyShape(skia, native); }, () => _shape, () => _shape);
        Number("CornerRadius (shorthand)", 0, 40, _cornerRadius, value => { _cornerRadius = value; ApplyShape(skia, native); }, () => skia.CornerRadius.TopLeft);
        Choice(nameof(SkUiBorder.StrokeDashArray), DashNames, "Solid", name => { skia.StrokeDashArray = Dashes(name); native.StrokeDashArray = Dashes(name); },
            () => DashName(skia.StrokeDashArray), () => DashName(native.StrokeDashArray));
        Choice(nameof(SkUiBorder.StrokeLineCap), Enum.GetValues<NativeShapes.PenLineCap>(), NativeShapes.PenLineCap.Flat,
            value => { skia.StrokeLineCap = value; native.StrokeLineCap = value; }, () => skia.StrokeLineCap, () => native.StrokeLineCap);
        Choice(nameof(SkUiBorder.StrokeLineJoin), Enum.GetValues<NativeShapes.PenLineJoin>(), NativeShapes.PenLineJoin.Miter,
            value => { skia.StrokeLineJoin = value; native.StrokeLineJoin = value; }, () => skia.StrokeLineJoin, () => native.StrokeLineJoin);
        // The border as one tappable card: the look's press feedback over it and its content, clipped to its corners.
        var taps = 0;
        skia.Tapped += (_, _) => Feedback($"Taps: {++taps}");
        Toggle(nameof(SkUiView.ShowsPressEffect), true, value => skia.ShowsPressEffect = value, () => skia.ShowsPressEffect);
        OnReset(() => taps = 0);
    }

    /// <summary>The drawn border takes SkiaUi's shapes (or its CornerRadius shorthand); MAUI's takes its own.</summary>
    private void ApplyShape(SkUiBorder skia, Border native)
    {
        skia.CornerRadius = _cornerRadius;
        switch (_shape)
        {
            case "Rectangle":
                skia.StrokeShape = new SkUiRectangle();
                native.StrokeShape = new NativeShapes.Rectangle();
                break;
            case "RoundRectangle 24,4,4,24":
                skia.StrokeShape = new SkUiRoundRectangle { CornerRadius = new CornerRadius(24, 4, 4, 24) };
                native.StrokeShape = new NativeShapes.RoundRectangle { CornerRadius = new CornerRadius(24, 4, 4, 24) };
                break;
            case "Ellipse":
                skia.StrokeShape = new SkUiEllipse();
                native.StrokeShape = new NativeShapes.Ellipse();
                break;
            case "Ticket (path)":
                skia.StrokeShape = new SkUiPath { Aspect = Stretch.Fill }.SetData(Ticket);
                native.StrokeShape = new NativeShapes.Path { Aspect = Stretch.Fill, Data = (NativeShapes.Geometry?)new NativeShapes.PathGeometryConverter().ConvertFromInvariantString(Ticket) };
                break;
            default:
                skia.StrokeShape = null;
                native.StrokeShape = new NativeShapes.RoundRectangle { CornerRadius = _cornerRadius };
                break;
        }
    }

    private static DoubleCollection Dashes(string name) => name switch
    {
        "4 2" => [4, 2],
        "0 2 (dots)" => [0, 2],
        _ => []
    };

    private static string DashName(DoubleCollection? dashes) =>
        DashNames.FirstOrDefault(name => Dashes(name).SequenceEqual(dashes ?? [])) ?? "?";
}
