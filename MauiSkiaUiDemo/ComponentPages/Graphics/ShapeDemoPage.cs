using MauiSkiaUi;
using NativeShapes = Microsoft.Maui.Controls.Shapes;

namespace MauiSkiaUiDemo;

/// <summary>Shared editors for drawn shapes compared against MAUI's shapes: brushes, stroke model and aspect.</summary>
public abstract class ShapeDemoPage : ComponentDemoPage
{
    // One instance per choice, shared by both sides, so the checks compare by reference.
    private static readonly string[] BrushNames = ["None", "Accent", "Sample", "Linear gradient", "Radial gradient"];
    private static readonly string[] DashNames = ["Solid", "4 2", "1 1", "0 2 (dots)"];

    protected ShapeDemoPage(string title, SkUiShape skia, NativeShapes.Shape native, string fill = "Accent", string stroke = "None", double strokeThickness = 1)
        : base(title, skia, native, widthRange: (20, 260, 160), heightRange: (20, 200, 120))
    {
        var fills = Brushes();
        var strokes = Brushes();
        Choice(nameof(SkUiShape.Fill), BrushNames, fill, name => { skia.Fill = fills[name]; native.Fill = fills[name]!; },
            () => NameOf(fills, skia.Fill), () => NameOf(fills, native.Fill));
        Choice(nameof(SkUiShape.Stroke), BrushNames, stroke, name => { skia.Stroke = strokes[name]; native.Stroke = strokes[name]!; },
            () => NameOf(strokes, skia.Stroke), () => NameOf(strokes, native.Stroke));
        Number(nameof(SkUiShape.StrokeThickness), 0, 16, strokeThickness, value => { skia.StrokeThickness = value; native.StrokeThickness = value; },
            () => skia.StrokeThickness, () => native.StrokeThickness);
        Choice(nameof(SkUiShape.StrokeDashArray), DashNames, "Solid", name => { skia.StrokeDashArray = Dashes(name); native.StrokeDashArray = Dashes(name); },
            () => DashName(skia.StrokeDashArray), () => DashName(native.StrokeDashArray));
        Number(nameof(SkUiShape.StrokeDashOffset), 0, 6, 0, value => { skia.StrokeDashOffset = value; native.StrokeDashOffset = value; },
            () => skia.StrokeDashOffset, () => native.StrokeDashOffset);
        Choice(nameof(SkUiShape.StrokeLineCap), Enum.GetValues<NativeShapes.PenLineCap>(), NativeShapes.PenLineCap.Flat,
            value => { skia.StrokeLineCap = value; native.StrokeLineCap = value; }, () => skia.StrokeLineCap, () => native.StrokeLineCap);
        Choice(nameof(SkUiShape.StrokeLineJoin), Enum.GetValues<NativeShapes.PenLineJoin>(), NativeShapes.PenLineJoin.Miter,
            value => { skia.StrokeLineJoin = value; native.StrokeLineJoin = value; }, () => skia.StrokeLineJoin, () => native.StrokeLineJoin);
        Choice(nameof(SkUiShape.Aspect), Enum.GetValues<Stretch>(), native.Aspect,
            value => { skia.Aspect = value; native.Aspect = value; }, () => skia.Aspect, () => native.Aspect);
        Number(nameof(VisualElement.Rotation), -45, 45, 0, value => { skia.Rotation = value; native.Rotation = value; }, () => skia.Rotation, () => native.Rotation);
    }

    private static Dictionary<string, Brush?> Brushes() => new()
    {
        ["None"] = null,
        ["Accent"] = new SolidColorBrush(Accent),
        ["Sample"] = new SolidColorBrush(DemoColors.SampleA),
        ["Linear gradient"] = new LinearGradientBrush([new GradientStop(Accent, 0), new GradientStop(DemoColors.SampleA, 1)], new Point(0, 0), new Point(1, 1)),
        ["Radial gradient"] = new RadialGradientBrush([new GradientStop(Colors.White, 0), new GradientStop(DemoColors.SampleB, 1)], new Point(0.5, 0.5), 0.5),
    };

    private static string NameOf(Dictionary<string, Brush?> brushes, Brush? brush) => brushes.FirstOrDefault(pair => ReferenceEquals(pair.Value, brush)).Key ?? "?";

    private static DoubleCollection Dashes(string name) => name switch
    {
        "4 2" => [4, 2],
        "1 1" => [1, 1],
        "0 2 (dots)" => [0, 2],
        _ => []
    };

    private static string DashName(DoubleCollection? dashes) =>
        DashNames.FirstOrDefault(name => Dashes(name).SequenceEqual(dashes ?? [])) ?? "?";
}

/// <summary>Side-by-side property playground for <see cref="SkUiEllipse"/>.</summary>
public sealed class EllipseDemoPage() : ShapeDemoPage(nameof(SkUiEllipse), new SkUiEllipse(), new NativeShapes.Ellipse());

/// <summary>Side-by-side property playground for <see cref="SkUiRectangle"/>.</summary>
public sealed class RectangleDemoPage : ShapeDemoPage
{
    public RectangleDemoPage() : base(nameof(SkUiRectangle), new SkUiRectangle(), new NativeShapes.Rectangle(), stroke: "Sample", strokeThickness: 4)
    {
        var skia = (SkUiRectangle)SkiaControl;
        var native = (NativeShapes.Rectangle)NativeControl!;
        Number(nameof(SkUiRectangle.RadiusX), 0, 40, 0, value => { skia.RadiusX = value; native.RadiusX = value; }, () => skia.RadiusX, () => native.RadiusX);
        Number(nameof(SkUiRectangle.RadiusY), 0, 40, 0, value => { skia.RadiusY = value; native.RadiusY = value; }, () => skia.RadiusY, () => native.RadiusY);
    }
}

/// <summary>Side-by-side property playground for <see cref="SkUiRoundRectangle"/>.</summary>
public sealed class RoundRectangleDemoPage : ShapeDemoPage
{
    public RoundRectangleDemoPage() : base(nameof(SkUiRoundRectangle), new SkUiRoundRectangle(), new NativeShapes.RoundRectangle(), stroke: "Sample", strokeThickness: 4)
    {
        var skia = (SkUiRoundRectangle)SkiaControl;
        var native = (NativeShapes.RoundRectangle)NativeControl!;
        Number("CornerRadius (top)", 0, 60, 24, value => Apply(skia, native, top: value), () => skia.CornerRadius.TopLeft, () => native.CornerRadius.TopLeft);
        Number("CornerRadius (bottom)", 0, 60, 4, value => Apply(skia, native, bottom: value), () => skia.CornerRadius.BottomLeft, () => native.CornerRadius.BottomLeft);
    }

    private static void Apply(SkUiRoundRectangle skia, NativeShapes.RoundRectangle native, double? top = null, double? bottom = null)
    {
        var current = skia.CornerRadius;
        var radii = new CornerRadius(top ?? current.TopLeft, top ?? current.TopRight, bottom ?? current.BottomLeft, bottom ?? current.BottomRight);
        skia.CornerRadius = radii;
        native.CornerRadius = radii;
    }
}

/// <summary>Side-by-side property playground for <see cref="SkUiLine"/>.</summary>
public sealed class LineDemoPage : ShapeDemoPage
{
    public LineDemoPage() : base(nameof(SkUiLine), new SkUiLine(0, 0, 160, 80), new NativeShapes.Line(0, 0, 160, 80), fill: "None", stroke: "Accent", strokeThickness: 4)
    {
        var skia = (SkUiLine)SkiaControl;
        var native = (NativeShapes.Line)NativeControl!;
        // Points in local DIPs on both sides; MAUI's Line has no stretch by default either.
        Number(nameof(SkUiLine.X1), 0, 200, 0, value => { skia.X1 = value; native.X1 = value; }, () => skia.X1, () => native.X1);
        Number(nameof(SkUiLine.Y1), 0, 200, 0, value => { skia.Y1 = value; native.Y1 = value; }, () => skia.Y1, () => native.Y1);
        Number(nameof(SkUiLine.X2), 0, 200, 160, value => { skia.X2 = value; native.X2 = value; }, () => skia.X2, () => native.X2);
        Number(nameof(SkUiLine.Y2), 0, 200, 80, value => { skia.Y2 = value; native.Y2 = value; }, () => skia.Y2, () => native.Y2);
    }
}

/// <summary>Shared point presets of the polygon and polyline playgrounds.</summary>
public abstract class PolyShapeDemoPage : ShapeDemoPage
{
    private static readonly string[] Presets = ["Triangle", "Star", "Zigzag"];

    protected PolyShapeDemoPage(string title, SkUiPolyShape skia, NativeShapes.Shape native, BindableProperty pointsProperty, BindableProperty fillRuleProperty, string fill)
        : base(title, skia, native, fill: fill, stroke: "Sample", strokeThickness: 3)
    {
        Choice(nameof(SkUiPolyShape.Points), Presets, "Star", name => { skia.Points = Points(name); native.SetValue(pointsProperty, Points(name)); },
            () => PresetOf(skia.Points), () => PresetOf((PointCollection)native.GetValue(pointsProperty)));
        Choice(nameof(SkUiPolyShape.FillRule), Enum.GetValues<NativeShapes.FillRule>(), NativeShapes.FillRule.EvenOdd,
            value => { skia.FillRule = value; native.SetValue(fillRuleProperty, value); },
            () => skia.FillRule, () => (NativeShapes.FillRule)native.GetValue(fillRuleProperty));
    }

    private static PointCollection Points(string name) => name switch
    {
        "Triangle" => [new(10, 90), new(60, 10), new(110, 90)],
        "Zigzag" => [new(0, 60), new(25, 10), new(50, 60), new(75, 10), new(100, 60), new(125, 10)],
        _ => [new(60, 0), new(75, 40), new(120, 40), new(85, 65), new(100, 110), new(60, 82), new(20, 110), new(35, 65), new(0, 40), new(45, 40)],
    };

    private static string PresetOf(PointCollection points) => Presets.FirstOrDefault(name => Points(name).SequenceEqual(points)) ?? "?";
}

/// <summary>Side-by-side property playground for <see cref="SkUiPolygon"/>.</summary>
public sealed class PolygonDemoPage() : PolyShapeDemoPage(nameof(SkUiPolygon), new SkUiPolygon(), new NativeShapes.Polygon(),
    NativeShapes.Polygon.PointsProperty, NativeShapes.Polygon.FillRuleProperty, "Accent");

/// <summary>Side-by-side property playground for <see cref="SkUiPolyline"/>.</summary>
public sealed class PolylineDemoPage() : PolyShapeDemoPage(nameof(SkUiPolyline), new SkUiPolyline(), new NativeShapes.Polyline(),
    NativeShapes.Polyline.PointsProperty, NativeShapes.Polyline.FillRuleProperty, "None");

/// <summary>Side-by-side property playground for <see cref="SkUiPath"/>.</summary>
public sealed class PathDemoPage : ShapeDemoPage
{
    private static readonly string[] Presets = ["Heart", "Arcs", "Curves", "Two rings"];

    public PathDemoPage() : base(nameof(SkUiPath), new SkUiPath(), new NativeShapes.Path(), stroke: "Sample", strokeThickness: 2)
    {
        var skia = (SkUiPath)SkiaControl;
        var native = (NativeShapes.Path)NativeControl!;
        var selected = "Heart";
        Choice(nameof(SkUiPath.Data), Presets, selected, name =>
        {
            selected = name;
            skia.SetData(Markup(name));
            native.Data = (NativeShapes.Geometry?)new NativeShapes.PathGeometryConverter().ConvertFromInvariantString(Markup(name));
        }, () => skia.Data is null ? "?" : selected, () => native.Data is null ? "?" : selected);
        Number("RenderTransform rotation", -45, 45, 0, value =>
        {
            skia.RenderTransform = new NativeShapes.RotateTransform(value, 60, 50);
            native.RenderTransform = new NativeShapes.RotateTransform(value, 60, 50);
        }, () => ((NativeShapes.RotateTransform)skia.RenderTransform!).Angle, () => ((NativeShapes.RotateTransform)native.RenderTransform).Angle);
    }

    private static string Markup(string name) => name switch
    {
        "Arcs" => "M 10,100 A 50,50 0 0 1 110,100 M 30,100 A 30,30 0 1 0 90,100",
        "Curves" => "M 0,60 C 30,0 60,120 90,60 S 150,0 180,60 Q 200,90 220,60",
        "Two rings" => "M 60,10 A 45,45 0 1 1 59.9,10 Z M 60,30 A 25,25 0 1 1 59.9,30 Z",
        _ => "M 60,105 C 10,70 0,40 20,20 C 40,0 60,15 60,30 C 60,15 80,0 100,20 C 120,40 110,70 60,105 Z",
    };
}
