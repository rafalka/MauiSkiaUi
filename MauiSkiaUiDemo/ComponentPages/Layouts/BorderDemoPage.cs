using MauiSkiaUi;
using NativeShapes = Microsoft.Maui.Controls.Shapes;

namespace MauiSkiaUiDemo;

/// <summary>Side-by-side property playground for <see cref="SkUiBorder"/>.</summary>
public sealed class BorderDemoPage : ComponentDemoPage
{
    public BorderDemoPage() : base(nameof(SkUiBorder), new SkUiBorder(), new Border())
    {
        var skia = (SkUiBorder)SkiaControl;
        var native = (Border)NativeControl!;
        // Contrasting label fill so padding / content slot is visible against the border Background.
        var contentFill = DemoColors.SampleB;
        var drawn = new SkUiLabel
        {
            Text = "Bordered",
            Background = contentFill,
            TextColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
        };
        var standard = new Label
        {
            Text = "Bordered",
            Background = contentFill,
            TextColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
        };
        skia.Content = drawn;
        native.Content = standard;
        native.Stroke = Accent;
        native.StrokeThickness = 2;
        ApplyCornerRadius(skia, native, new CornerRadius(10));
        ColorEditor(nameof(VisualElement.Background), DemoColors.SoftSurface, value =>
        {
            skia.Background = value;
            native.Background = value;
        }, () => ((SolidColorBrush)skia.Background).Color, () => ((SolidColorBrush)native.Background).Color);
        ColorEditor(nameof(SkUiBorder.Stroke), Accent, value => { skia.Stroke = value; native.Stroke = new SolidColorBrush(value); }, () => skia.Stroke!, () => ((SolidColorBrush)native.Stroke).Color);
        Number(nameof(SkUiBorder.StrokeThickness), 0, 8, 2, value => { skia.StrokeThickness = value; native.StrokeThickness = value; }, () => skia.StrokeThickness, () => native.StrokeThickness);
        Number(nameof(SkUiBorder.Padding), 0, 28, 12, value => { skia.Padding = value; native.Padding = value; }, () => skia.Padding.Left, () => native.Padding.Left);
        Number("CornerRadius (uniform)", 0, 30, 10, value => ApplyCornerRadius(skia, native, new CornerRadius(value)), () => skia.CornerRadius.TopLeft);
        Number("TopLeft", 0, 30, 10, value => ApplyCorner(skia, native, tl: value), () => skia.CornerRadius.TopLeft);
        Number("TopRight", 0, 30, 10, value => ApplyCorner(skia, native, tr: value), () => skia.CornerRadius.TopRight);
        Number("BottomLeft", 0, 30, 10, value => ApplyCorner(skia, native, bl: value), () => skia.CornerRadius.BottomLeft);
        Number("BottomRight", 0, 30, 10, value => ApplyCorner(skia, native, br: value), () => skia.CornerRadius.BottomRight);
    }

    private static void ApplyCorner(SkUiBorder skia, Border native, double? tl = null, double? tr = null, double? bl = null, double? br = null)
    {
        var current = skia.CornerRadius;
        ApplyCornerRadius(skia, native, new CornerRadius(
            tl ?? current.TopLeft,
            tr ?? current.TopRight,
            bl ?? current.BottomLeft,
            br ?? current.BottomRight));
    }

    private static void ApplyCornerRadius(SkUiBorder skia, Border native, CornerRadius radii)
    {
        skia.CornerRadius = radii;
        native.StrokeShape = new NativeShapes.RoundRectangle { CornerRadius = radii };
    }
}
