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
        var drawn = new SkUiLabel { Text = "Bordered", Background = Colors.White, TextColor = Ink, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center };
        var standard = new Label { Text = "Bordered", Background = Colors.White, TextColor = Ink, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center };
        skia.Content = drawn;
        native.Content = standard;
        native.Stroke = Accent;
        native.StrokeThickness = 2;
        native.StrokeShape = new NativeShapes.RoundRectangle { CornerRadius = 10 };
        ColorEditor(nameof(SkUiBorder.Stroke), Accent, value => { skia.Stroke = value; native.Stroke = new SolidColorBrush(value); }, () => skia.Stroke!, () => ((SolidColorBrush)native.Stroke).Color);
        Number(nameof(SkUiBorder.StrokeThickness), 0, 8, 2, value => { skia.StrokeThickness = value; native.StrokeThickness = value; }, () => skia.StrokeThickness, () => native.StrokeThickness);
        Number(nameof(SkUiBorder.CornerRadius), 0, 30, 10, value => { skia.CornerRadius = value; native.StrokeShape = new NativeShapes.RoundRectangle { CornerRadius = value }; }, () => skia.CornerRadius);
    }
}
