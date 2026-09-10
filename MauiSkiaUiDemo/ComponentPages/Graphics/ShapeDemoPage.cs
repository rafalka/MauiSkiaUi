using MauiSkiaUi;
using NativeShapes = Microsoft.Maui.Controls.Shapes;

namespace MauiSkiaUiDemo;

/// <summary>Shared editors for shape primitives compared against MAUI shapes.</summary>
public abstract class ShapeDemoPage : ComponentDemoPage
{
    protected ShapeDemoPage(string title, SkUiShape skia, View native) : base(title, skia, native)
    {
        ColorEditor(nameof(SkUiShape.Color), Accent, value =>
        {
            skia.Color = value;
            if (native is BoxView box) box.Color = value;
            else if (native is NativeShapes.Line line) line.Stroke = value;
            else ((NativeShapes.Shape)native).Fill = value;
        }, () => skia.Color);
        Number(nameof(VisualElement.Rotation), -45, 45, 0, value => { skia.Rotation = value; native.Rotation = value; }, () => skia.Rotation, () => native.Rotation);
        if (native is NativeShapes.Line lineShape)
        {
            void Endpoints() { lineShape.X1 = lineShape.StrokeThickness / 2; lineShape.Y1 = lineShape.StrokeThickness / 2; lineShape.X2 = Math.Max(lineShape.X1, lineShape.Width - lineShape.X1); lineShape.Y2 = Math.Max(lineShape.Y1, lineShape.Height - lineShape.Y1); }
            lineShape.SizeChanged += (_, _) => Endpoints();
            Number(nameof(SkUiShape.StrokeWidth), 0, 16, 2, value => { skia.StrokeWidth = value; lineShape.StrokeThickness = value; Endpoints(); }, () => skia.StrokeWidth, () => lineShape.StrokeThickness);
        }
    }
}

/// <summary>Side-by-side property playground for <see cref="SkUiBox"/>.</summary>
public sealed class BoxDemoPage() : ShapeDemoPage(nameof(SkUiBox), new SkUiBox(), new BoxView());

/// <summary>Side-by-side property playground for <see cref="SkUiEllipse"/>.</summary>
public sealed class EllipseDemoPage() : ShapeDemoPage(nameof(SkUiEllipse), new SkUiEllipse(), new NativeShapes.Ellipse { StrokeThickness = 0 });

/// <summary>Side-by-side property playground for <see cref="SkUiLine"/>.</summary>
public sealed class LineDemoPage() : ShapeDemoPage(nameof(SkUiLine), new SkUiLine(), new NativeShapes.Line { Aspect = Stretch.Fill });
