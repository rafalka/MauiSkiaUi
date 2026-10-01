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
        if (skia is SkUiBox skiaBox && native is BoxView nativeBox)
            Number(nameof(SkUiBox.CornerRadius), 0, 60, 0, value => { skiaBox.CornerRadius = value; nativeBox.CornerRadius = value; }, () => skiaBox.CornerRadius.TopLeft, () => nativeBox.CornerRadius.TopLeft);
        if (skia is SkUiLine skiaLine && native is NativeShapes.Line lineShape)
        {
            // Points in local DIPs on both sides; MAUI's Line has no stretch by default either.
            Number(nameof(SkUiLine.X1), 0, 200, 0, value => { skiaLine.X1 = value; lineShape.X1 = value; }, () => skiaLine.X1, () => lineShape.X1);
            Number(nameof(SkUiLine.Y1), 0, 200, 0, value => { skiaLine.Y1 = value; lineShape.Y1 = value; }, () => skiaLine.Y1, () => lineShape.Y1);
            Number(nameof(SkUiLine.X2), 0, 200, 160, value => { skiaLine.X2 = value; lineShape.X2 = value; }, () => skiaLine.X2, () => lineShape.X2);
            Number(nameof(SkUiLine.Y2), 0, 200, 80, value => { skiaLine.Y2 = value; lineShape.Y2 = value; }, () => skiaLine.Y2, () => lineShape.Y2);
            Number(nameof(SkUiShape.StrokeWidth), 0, 16, 2, value => { skia.StrokeWidth = value; lineShape.StrokeThickness = value; }, () => skia.StrokeWidth, () => lineShape.StrokeThickness);
        }
    }
}

/// <summary>Side-by-side property playground for <see cref="SkUiBox"/>.</summary>
public sealed class BoxDemoPage() : ShapeDemoPage(nameof(SkUiBox), new SkUiBox(), new BoxView());

/// <summary>Side-by-side property playground for <see cref="SkUiEllipse"/>.</summary>
public sealed class EllipseDemoPage() : ShapeDemoPage(nameof(SkUiEllipse), new SkUiEllipse(), new NativeShapes.Ellipse { StrokeThickness = 0 });

/// <summary>Side-by-side property playground for <see cref="SkUiLine"/>.</summary>
public sealed class LineDemoPage() : ShapeDemoPage(nameof(SkUiLine), new SkUiLine(0, 0, 160, 80), new NativeShapes.Line(0, 0, 160, 80));
