using System.Numerics;
using MauiSkiaUi.Core;
using Microsoft.Maui.Controls.Shapes;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// MAUI parity P6, shapes: MAUI's <c>Shape</c> API (brushes, stroke model, aspect) on the SkUi* shapes and their Core
/// twins, measured as MAUI's own shapes measure and drawn identically on both layers.
/// </summary>
[Collection(RuntimeXamlCollection.Name)]
public class ShapeTests
{
    private static readonly SKColor Red = SKColors.Red;
    private static readonly SKColor Blue = SKColors.Blue;

    private static SKBitmap Render(Action<SKCanvas> paint, int width, int height)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        paint(canvas);
        return bitmap;
    }

    private static SKBitmap Render(SkUiView view, int width, int height)
    {
        SkUiTestHelpers.Arrange(view, width, height);
        return Render(view.Paint, width, height);
    }

    private static SKBitmap Render(SkUiCoreNode node, int width, int height)
    {
        node.Measure(width, height);
        node.Arrange(new Rect(0, 0, width, height));
        return Render(node.Paint, width, height);
    }

    private static Size Measure(IView view, double width, double height) => view.Measure(width, height);

    public static TheoryData<string, double, double> MeasureCases => new()
    {
        // Unstretched geometry: its far points plus the stroke; under any constraint.
        { "Line", double.PositiveInfinity, double.PositiveInfinity },
        { "Polyline", double.PositiveInfinity, double.PositiveInfinity },
        { "Polygon", 300, 300 },
        { "Path", double.PositiveInfinity, double.PositiveInfinity },
        // Stretched geometry: scaled into the constraint.
        { "PathFill", 120, 90 },
        { "PathUniform", 120, 90 },
        { "PathUniformToFill", 120, double.PositiveInfinity },
        // Rectangles and ellipses fill what they are given.
        { "Ellipse", 120, 90 },
        { "Rectangle", 70, 40 },
        { "RoundRectangle", 50, 60 },
    };

    [Theory]
    [MemberData(nameof(MeasureCases))]
    public void ShapesMeasureLikeMauiShapes(string kind, double width, double height)
    {
        var (skia, maui) = Pair(kind);
        Assert.Equal(Measure(maui, width, height), Measure(skia, width, height));
    }

    private static (SkUiShape Skia, Shape Maui) Pair(string kind)
    {
        const string data = "M 10,10 L 50,20 L 30,60 Z";
        (SkUiShape, Shape) pair = kind switch
        {
            "Line" => (new SkUiLine(10, 5, 50, 25), new Line(10, 5, 50, 25)),
            "Polyline" => (new SkUiPolyline([new(5, 5), new(40, 30), new(70, 10)]), new Polyline([new(5, 5), new(40, 30), new(70, 10)])),
            "Polygon" => (new SkUiPolygon([new(0, 20), new(40, 0), new(80, 20)]), new Polygon([new(0, 20), new(40, 0), new(80, 20)])),
            "Path" => (new SkUiPath().SetData(data), new Microsoft.Maui.Controls.Shapes.Path { Data = Geometry(data) }),
            "PathFill" => (new SkUiPath { Aspect = Stretch.Fill }.SetData(data), new Microsoft.Maui.Controls.Shapes.Path { Aspect = Stretch.Fill, Data = Geometry(data) }),
            "PathUniform" => (new SkUiPath { Aspect = Stretch.Uniform }.SetData(data), new Microsoft.Maui.Controls.Shapes.Path { Aspect = Stretch.Uniform, Data = Geometry(data) }),
            "PathUniformToFill" => (new SkUiPath { Aspect = Stretch.UniformToFill }.SetData(data), new Microsoft.Maui.Controls.Shapes.Path { Aspect = Stretch.UniformToFill, Data = Geometry(data) }),
            "Ellipse" => (new SkUiEllipse(), new Ellipse()),
            "Rectangle" => (new SkUiRectangle(), new Microsoft.Maui.Controls.Shapes.Rectangle()),
            _ => (new SkUiRoundRectangle(), new RoundRectangle()),
        };
        pair.Item1.StrokeThickness = pair.Item2.StrokeThickness = 3;
        return pair;
    }

    private static Geometry Geometry(string markup) => (Geometry)new PathGeometryConverter().ConvertFromInvariantString(markup)!;

    [Fact]
    public void DefaultsFollowMaui()
    {
        var ellipse = new SkUiEllipse();
        var line = new SkUiLine();
        var maui = new Microsoft.Maui.Controls.Shapes.Rectangle();
        Assert.Null(ellipse.Fill);
        Assert.Null(ellipse.Stroke);
        Assert.Equal((1d, 10d, PenLineCap.Flat, PenLineJoin.Miter), (ellipse.StrokeThickness, ellipse.StrokeMiterLimit, ellipse.StrokeLineCap, ellipse.StrokeLineJoin));
        Assert.Empty(ellipse.StrokeDashArray);
        // MAUI sets Fill on rectangles and ellipses; other shapes keep None.
        Assert.Equal(maui.Aspect, new SkUiRectangle().Aspect);
        Assert.Equal(Stretch.Fill, ellipse.Aspect);
        Assert.Equal(Stretch.Fill, new SkUiRoundRectangle().Aspect);
        Assert.Equal(Stretch.None, line.Aspect);
        Assert.Equal(SkUiCoreStretch.Fill, new SkUiCoreEllipse().Aspect);
        Assert.Equal(SkUiCoreStretch.None, new SkUiCorePath().Aspect);
        // Without fill or stroke a shape draws nothing.
        using var empty = Render(new SkUiEllipse(), 20, 20);
        Assert.Equal(0, empty.GetPixel(10, 10).Alpha);
    }

    [Fact]
    public void StrokeOnlyEllipseKeepsItsInteriorClear()
    {
        var ellipse = new SkUiEllipse { Stroke = Colors.Red, StrokeThickness = 4 };
        using var bitmap = Render(ellipse, 40, 40);
        Assert.Equal(0, bitmap.GetPixel(20, 20).Alpha);
        Assert.Equal(Red, bitmap.GetPixel(20, 2)); // inside the bounds: the stroke is inset by half its thickness
        Assert.Equal(Red, bitmap.GetPixel(37, 20));
        Assert.Equal(0, bitmap.GetPixel(1, 1).Alpha);
    }

    [Fact]
    public void FillAndStrokeBrushesPaintInsideAndOnTheOutline()
    {
        var rectangle = new SkUiRectangle { Fill = Colors.Blue, Stroke = Colors.Red, StrokeThickness = 6 };
        using var bitmap = Render(rectangle, 40, 30);
        Assert.Equal(Blue, bitmap.GetPixel(20, 15));
        Assert.Equal(Red, bitmap.GetPixel(1, 15));
        Assert.Equal(Red, bitmap.GetPixel(20, 28));
    }

    [Fact]
    public void GradientBrushesMapOntoTheShape()
    {
        var linear = new SkUiRectangle
        {
            Fill = new LinearGradientBrush([new GradientStop(Colors.Red, 0), new GradientStop(Colors.Blue, 1)], new Point(0, 0), new Point(1, 0)),
        };
        using (var bitmap = Render(linear, 100, 10))
        {
            var left = bitmap.GetPixel(2, 5);
            var right = bitmap.GetPixel(97, 5);
            Assert.True(left.Red > 240 && left.Blue < 15, $"{left}");
            Assert.True(right.Blue > 240 && right.Red < 15, $"{right}");
        }

        var radial = new SkUiEllipse
        {
            Fill = new RadialGradientBrush([new GradientStop(Colors.Red, 0), new GradientStop(Colors.Blue, 1)], new Point(0.5, 0.5), 0.5),
        };
        using (var bitmap = Render(radial, 60, 60))
        {
            var center = bitmap.GetPixel(30, 30);
            var edge = bitmap.GetPixel(30, 2);
            Assert.True(center.Red > 240 && center.Blue < 15, $"{center}");
            Assert.True(edge.Blue > edge.Red, $"{edge}");
        }
    }

    [Fact]
    public void DashesAreMultiplesOfTheThicknessAndStartAtTheOffset()
    {
        // Dash 2, gap 2 at a thickness of 4: 8 DIPs on, 8 off. The line is placed half the stroke down.
        var line = new SkUiLine(0, 0, 64, 0) { Stroke = Colors.Red, StrokeThickness = 4 };
        line.StrokeDashArray = [2, 2];
        using (var bitmap = Render(line, 64, 4))
        {
            Assert.Equal(Red, bitmap.GetPixel(4, 2));
            Assert.Equal(0, bitmap.GetPixel(12, 2).Alpha);
            Assert.Equal(Red, bitmap.GetPixel(20, 2));
        }
        line.StrokeDashOffset = 2; // shifts the pattern back by 8 DIPs: a gap first
        using (var bitmap = Render(line, 64, 4))
        {
            Assert.Equal(0, bitmap.GetPixel(4, 2).Alpha);
            Assert.Equal(Red, bitmap.GetPixel(12, 2));
        }
        // Editing the default collection in place redraws.
        var invalidated = 0;
        line.PaintInvalidated += (_, _) => invalidated++;
        line.StrokeDashArray.Add(1);
        Assert.True(invalidated > 0);
    }

    [Fact]
    public void RoundCapsReachPastTheEndsAndJoinsShapeCorners()
    {
        var flat = new SkUiLine(10, 10, 30, 10) { Stroke = Colors.Red, StrokeThickness = 8, Aspect = Stretch.None };
        var round = new SkUiLine(10, 10, 30, 10) { Stroke = Colors.Red, StrokeThickness = 8, StrokeLineCap = PenLineCap.Round };
        using (var bitmap = Render(flat, 40, 20))
            Assert.Equal(0, bitmap.GetPixel(8, 10).Alpha);
        using (var bitmap = Render(round, 40, 20))
            Assert.Equal(Red, bitmap.GetPixel(8, 10));

        // A sharp corner: a miter reaches far past the corner, a bevel cuts it off.
        PointCollection points = [new(10, 50), new(30, 10), new(50, 50)];
        var miter = new SkUiPolyline(points) { Stroke = Colors.Red, StrokeThickness = 8, StrokeMiterLimit = 10 };
        var bevel = new SkUiPolyline([.. points]) { Stroke = Colors.Red, StrokeThickness = 8, StrokeLineJoin = PenLineJoin.Bevel };
        using (var bitmap = Render(miter, 60, 60))
            Assert.Equal(Red, bitmap.GetPixel(30, 4));
        using (var bitmap = Render(bevel, 60, 60))
            Assert.Equal(0, bitmap.GetPixel(30, 4).Alpha);
    }

    [Fact]
    public void FillRuleDecidesTheInsideOfCrossingFigures()
    {
        // A five-point star drawn in one stroke: even-odd leaves the center pentagon empty, non-zero fills it.
        PointCollection star = [new(50, 0), new(79, 90), new(2, 35), new(98, 35), new(21, 90)];
        var evenOdd = new SkUiPolygon(star) { Fill = Colors.Red };
        var nonZero = new SkUiPolygon([.. star]) { Fill = Colors.Red, FillRule = FillRule.Nonzero };
        using (var bitmap = Render(evenOdd, 100, 100))
        {
            Assert.Equal(0, bitmap.GetPixel(50, 50).Alpha);
            Assert.Equal(Red, bitmap.GetPixel(50, 10));
        }
        using (var bitmap = Render(nonZero, 100, 100))
            Assert.Equal(Red, bitmap.GetPixel(50, 50));

        // Path geometries carry their own rule, as in MAUI (whose markup parser skips F0 / F1: even-odd).
        var path = new SkUiPath { Fill = Colors.Red }.SetData("F1 M 50,0 L 79,90 L 2,35 L 98,35 L 21,90 Z");
        using (var bitmap = Render(path, 100, 100))
            Assert.Equal(0, bitmap.GetPixel(50, 50).Alpha);
        ((PathGeometry)path.Data!).FillRule = FillRule.Nonzero;
        using (var bitmap = Render(path, 100, 100))
            Assert.Equal(Red, bitmap.GetPixel(50, 50));
    }

    [Fact]
    public void AspectScalesAndCentersTheGeometry()
    {
        // A 10 × 20 triangle in 100 × 100 with a 2-DIP stroke: the area is 98 × 98, uniform scale 4.9, centered horizontally.
        const string data = "M 0,0 L 10,0 L 10,20 Z";
        var uniform = new SkUiPath { Fill = Colors.Red, StrokeThickness = 2, Aspect = Stretch.Uniform }.SetData(data);
        using (var bitmap = Render(uniform, 100, 100))
        {
            Assert.Equal(Red, bitmap.GetPixel(70, 20));
            Assert.Equal(0, bitmap.GetPixel(20, 20).Alpha); // left of the centered shape
            Assert.Equal(0, bitmap.GetPixel(80, 20).Alpha); // right of it
        }
        var fill = new SkUiPath { Fill = Colors.Red, StrokeThickness = 2, Aspect = Stretch.Fill }.SetData(data);
        using (var bitmap = Render(fill, 100, 100))
            Assert.Equal(Red, bitmap.GetPixel(90, 20));
        // None: the geometry keeps its coordinates, moved in by half the stroke where it would be cut.
        var none = new SkUiPath { Fill = Colors.Red, StrokeThickness = 2 }.SetData(data);
        using (var bitmap = Render(none, 100, 100))
        {
            Assert.Equal(Red, bitmap.GetPixel(9, 3));
            Assert.Equal(0, bitmap.GetPixel(30, 30).Alpha);
        }
    }

    [Fact]
    public void PathRenderTransformAppliesWhenDrawing()
    {
        var path = new SkUiPath { Fill = Colors.Red, StrokeThickness = 0 }.SetData("M 0,0 L 10,0 L 10,10 L 0,10 Z");
        path.RenderTransform = new TranslateTransform { X = 20 };
        using var bitmap = Render(path, 40, 20);
        Assert.Equal(0, bitmap.GetPixel(5, 5).Alpha);
        Assert.Equal(Red, bitmap.GetPixel(25, 5));
        var invalidated = 0;
        path.PaintInvalidated += (_, _) => invalidated++;
        ((TranslateTransform)path.RenderTransform).X = 10;
        Assert.True(invalidated > 0);
    }

    [Fact]
    public void BrushPointsAndGeometryChangesRedraw()
    {
        var brush = new SolidColorBrush(Colors.Red);
        var polygon = new SkUiPolygon([new(0, 0), new(10, 0), new(5, 10)]) { Fill = brush };
        var paints = 0;
        polygon.PaintInvalidated += (_, _) => paints++;
        brush.Color = Colors.Blue;
        Assert.Equal(1, paints);
        Assert.Equal(new Size(11, 11), Measure(polygon, double.PositiveInfinity, double.PositiveInfinity));
        polygon.Points.Add(new Point(40, 40));
        Assert.Equal(new Size(41, 41), Measure(polygon, double.PositiveInfinity, double.PositiveInfinity));

        var gradient = new LinearGradientBrush([new GradientStop(Colors.Red, 0)], new Point(0, 0), new Point(1, 0));
        polygon.Fill = gradient;
        paints = 0;
        gradient.GradientStops.Add(new GradientStop(Colors.Blue, 1));
        Assert.True(paints > 0);
        paints = 0;
        brush.Color = Colors.Green; // no longer the fill: not listened to
        Assert.Equal(0, paints);

        var geometry = new EllipseGeometry(new Point(10, 10), 5, 5);
        var path = new SkUiPath(geometry);
        Assert.Equal(16, Measure(path, double.PositiveInfinity, double.PositiveInfinity).Width, 0);
        geometry.RadiusX = 8;
        Assert.Equal(19, Measure(path, double.PositiveInfinity, double.PositiveInfinity).Width, 0);
    }

    [Fact]
    public void SharedBrushesDoNotKeepShapesAlive()
    {
        var brush = new SolidColorBrush(Colors.Red);
        var references = Create(brush);
        for (var attempt = 0; attempt < 5 && references.Any(reference => reference.TryGetTarget(out _)); attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        Assert.All(references, reference => Assert.False(reference.TryGetTarget(out _)));
        brush.Color = Colors.Blue; // listeners of collected shapes unsubscribe on the next change
        GC.KeepAlive(brush);

        static WeakReference<SkUiShape>[] Create(Brush brush) =>
            Enumerable.Range(0, 4).Select(_ => new WeakReference<SkUiShape>(new SkUiEllipse { Fill = brush, Stroke = brush })).ToArray();
    }

    [Fact]
    public void ASharedBrushSubscribesOnceAndForgetsCollectedShapes()
    {
        var brush = new SolidColorBrush(Colors.Red);
        var live = new SkUiEllipse { Fill = brush };
        Churn(brush);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Churn(brush); // adding listeners prunes the dead ones
        // One subscription on the brush, whatever the number of shapes using it.
        Assert.Single(PropertyChangedSubscribers(brush));
        var paints = 0;
        live.PaintInvalidated += (_, _) => paints++;
        brush.Color = Colors.Blue;
        Assert.Equal(1, paints);
        GC.KeepAlive(live);

        static void Churn(Brush brush)
        {
            for (var index = 0; index < 50; index++)
                _ = new SkUiRectangle { Fill = brush, Stroke = brush };
        }

        static Delegate[] PropertyChangedSubscribers(BindableObject source)
        {
            var field = typeof(BindableObject).GetField("PropertyChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return (field?.GetValue(source) as Delegate)?.GetInvocationList() ?? [];
        }
    }

    [Fact]
    public void ShapesImplementMauiShapeForBounds()
    {
        IShape ellipse = new SkUiEllipse { StrokeThickness = 4 };
        var bounds = ellipse.PathForBounds(new Rect(0, 0, 40, 20)).GetBoundsByFlattening(0.01f);
        // Inset by half its own stroke, as MAUI's shapes.
        Assert.Equal((2, 2, 36, 16), (Math.Round(bounds.X, 1), Math.Round(bounds.Y, 1), Math.Round(bounds.Width, 1), Math.Round(bounds.Height, 1)));
        IShape core = new SkUiCoreEllipse().SetStrokeThickness(4);
        Assert.Equal(bounds, core.PathForBounds(new Rect(0, 0, 40, 20)).GetBoundsByFlattening(0.01f));
    }

    public static TheoryData<string> Kinds => ["Ellipse", "Rectangle", "RoundRectangle", "Line", "Polygon", "Polyline", "Path", "PathUniform"];

    [Theory]
    [MemberData(nameof(Kinds))]
    public void CoreShapesDrawTheSamePixels(string kind)
    {
        var gradient = new LinearGradientBrush([new GradientStop(Colors.Red, 0), new GradientStop(Colors.Blue, 1)], new Point(0, 0), new Point(1, 1));
        var (skia, core) = Twins(kind);
        skia.Fill = gradient;
        skia.Stroke = Colors.Black;
        skia.StrokeThickness = 3;
        skia.StrokeDashArray = [3, 1];
        skia.StrokeLineJoin = PenLineJoin.Round;
        skia.StrokeLineCap = PenLineCap.Round;
        core.SetFill((Paint)gradient).SetStroke(Colors.Black).SetStrokeThickness(3).SetStrokeDashArray(3, 1)
            .SetStrokeLineJoin(LineJoin.Round).SetStrokeLineCap(LineCap.Round);
        Assert.Equal(Measure(skia, 90, 70), core.Measure(90, 70));
        using var expected = Render(skia, 90, 70);
        using var actual = Render(core, 90, 70);
        Assert.Equal(expected.Pixels, actual.Pixels);
    }

    private static (SkUiShape Skia, SkUiCoreShape Core) Twins(string kind)
    {
        const string data = "M 10,10 C 40,0 60,50 80,20 L 70,60 Z";
        return kind switch
        {
            "Ellipse" => (new SkUiEllipse(), new SkUiCoreEllipse()),
            "Rectangle" => (new SkUiRectangle { RadiusX = 6, RadiusY = 10 }, new SkUiCoreRectangle().SetRadiusX(6).SetRadiusY(10)),
            "RoundRectangle" => (new SkUiRoundRectangle { CornerRadius = new CornerRadius(20, 4, 0, 10) }, new SkUiCoreRoundRectangle().SetCornerRadius(new CornerRadius(20, 4, 0, 10))),
            "Line" => (new SkUiLine(5, 60, 80, 8), new SkUiCoreLine(5, 60, 80, 8)),
            "Polygon" => (new SkUiPolygon([new(5, 5), new(80, 30), new(20, 60)]), new SkUiCorePolygon(new(5, 5), new(80, 30), new(20, 60))),
            "Polyline" => (new SkUiPolyline([new(5, 5), new(80, 30), new(20, 60)]), new SkUiCorePolyline(new(5, 5), new(80, 30), new(20, 60))),
            "Path" => (new SkUiPath().SetData(data), new SkUiCorePath(data)),
            _ => (new SkUiPath { Aspect = Stretch.Uniform }.SetData(data), (SkUiCorePath)new SkUiCorePath(data).SetAspect(SkUiCoreStretch.Uniform)),
        };
    }

    [Fact]
    public void CorePathMarkupAndTransform()
    {
        var path = new SkUiCorePath("F1 M 0,0 L 10,0 L 10,10 Z"); // the fill-rule prefix is skipped, as by MAUI's parser
        Assert.Equal(WindingMode.EvenOdd, path.FillRule);
        Assert.Equal(new Size(11, 11), path.Measure(double.PositiveInfinity, double.PositiveInfinity));
        path.SetRenderTransform(Matrix3x2.CreateTranslation(20, 0)).SetFill(Colors.Red);
        using var bitmap = Render(path, 40, 20);
        Assert.Equal(Red, bitmap.GetPixel(29, 2));
        Assert.Equal(0, bitmap.GetPixel(9, 2).Alpha);
    }

    [Fact]
    public void BoxViewTwinFillsWithColorOrBackground()
    {
        var box = new SkUiBox();
        Assert.Null(box.Color);
        Assert.Equal(new Size(40, 40), Measure(box, double.PositiveInfinity, double.PositiveInfinity));
        box.BackgroundColor = Colors.Blue;
        box.CornerRadius = 10;
        using (var bitmap = Render(box, 40, 40))
        {
            Assert.Equal(Blue, bitmap.GetPixel(20, 20));
            Assert.Equal(0, bitmap.GetPixel(0, 0).Alpha); // the background follows the corners
        }
        box.Color = Colors.Red; // the color wins
        using (var bitmap = Render(box, 40, 40))
            Assert.Equal(Red, bitmap.GetPixel(20, 20));
    }

    [Fact]
    public void MauiDocSamplesLoadWithThePrefixChanged()
    {
        // From the MAUI shapes docs (learn.microsoft.com/dotnet/maui/user-interface/shapes), with only the prefix changed.
        const string xaml = """
            <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <sk:SkUiVerticalStackLayout>
                <sk:SkUiEllipse Fill="Red" WidthRequest="150" HeightRequest="50" HorizontalOptions="Start" />
                <sk:SkUiRectangle Fill="Red" Stroke="Black" StrokeThickness="3" RadiusX="50" RadiusY="10" WidthRequest="200" HeightRequest="100" HorizontalOptions="Start" />
                <sk:SkUiRoundRectangle CornerRadius="40" Fill="Blue" WidthRequest="200" HeightRequest="200" HorizontalOptions="Start" />
                <sk:SkUiLine X1="40" Y1="0" X2="0" Y2="120" Stroke="DarkBlue" StrokeDashArray="1,1" StrokeDashOffset="6" StrokeLineCap="Round" />
                <sk:SkUiPolygon Points="40,10 70,80 10,50" Fill="AliceBlue" Stroke="Green" StrokeThickness="5" StrokeLineJoin="Round" />
                <sk:SkUiPolyline Points="0,0 10,30 15,0 18,60 23,30 35,30 40,0 43,60 48,30 100,30" Stroke="Red" />
                <sk:SkUiPath Data="M 10,100 L 100,100 100,50Z" Stroke="Black" Aspect="Uniform" HorizontalOptions="Start" />
                <sk:SkUiEllipse WidthRequest="50" HeightRequest="50">
                  <sk:SkUiEllipse.Fill>
                    <LinearGradientBrush EndPoint="1,0">
                      <GradientStop Color="Yellow" Offset="0.1" />
                      <GradientStop Color="Green" Offset="1.0" />
                    </LinearGradientBrush>
                  </sk:SkUiEllipse.Fill>
                </sk:SkUiEllipse>
                <sk:SkUiPath Stroke="Black" Fill="Gray">
                  <sk:SkUiPath.Data>
                    <EllipseGeometry Center="50,50" RadiusX="50" RadiusY="25" />
                  </sk:SkUiPath.Data>
                  <sk:SkUiPath.RenderTransform>
                    <RotateTransform CenterX="0" CenterY="0" Angle="45" />
                  </sk:SkUiPath.RenderTransform>
                </sk:SkUiPath>
              </sk:SkUiVerticalStackLayout>
            </ContentView>
            """;
        var root = new ContentView();
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var children = ((SkUiVerticalStackLayout)root.Content).Children.Cast<SkUiShape>().ToArray();
        Assert.Equal(Colors.Red, ((SolidColorBrush)children[0].Fill!).Color);
        Assert.Equal((50d, 10d), (((SkUiRectangle)children[1]).RadiusX, ((SkUiRectangle)children[1]).RadiusY));
        Assert.Equal(new CornerRadius(40), ((SkUiRoundRectangle)children[2]).CornerRadius);
        var line = (SkUiLine)children[3];
        Assert.Equal([1d, 1d], line.StrokeDashArray);
        Assert.Equal((6d, PenLineCap.Round), (line.StrokeDashOffset, line.StrokeLineCap));
        Assert.Equal(3, ((SkUiPolygon)children[4]).Points.Count);
        Assert.Equal(10, ((SkUiPolyline)children[5]).Points.Count);
        Assert.IsType<PathGeometry>(((SkUiPath)children[6]).Data);
        Assert.IsType<LinearGradientBrush>(children[7].Fill);
        Assert.IsType<RotateTransform>(((SkUiPath)children[8]).RenderTransform);

        // Measured as MAUI measures the same markup. Sized shapes measure to their requests (MAUI's platform views report
        // them; without a handler MAUI would add the stroke), so only the self-sized ones are compared.
        var maui = new ContentView();
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(maui, xaml
            .Replace("sk:SkUiVerticalStackLayout", "VerticalStackLayout").Replace("sk:SkUi", ""));
        var mauiChildren = ((VerticalStackLayout)maui.Content).Children.Cast<View>().ToArray();
        for (var index = 0; index < children.Length; index++)
        {
            if (children[index].WidthRequest >= 0)
                Assert.Equal(new Size(children[index].WidthRequest, children[index].HeightRequest), ((IView)children[index]).Measure(300, double.PositiveInfinity));
            else if (children[index].Aspect == Stretch.Uniform)
                // MAUI scales by 0 under the unbounded height and collapses to the stroke (1 × 1); SkiaUi scales by the
                // width: the 90 × 50 triangle fits 299 DIPs wide.
                Assert.Equal(new Size(300, 50 * 299d / 90 + 1), ((IView)children[index]).Measure(300, double.PositiveInfinity));
            else
                Assert.Equal(((IView)mauiChildren[index]).Measure(300, double.PositiveInfinity), ((IView)children[index]).Measure(300, double.PositiveInfinity));
        }
    }
}
