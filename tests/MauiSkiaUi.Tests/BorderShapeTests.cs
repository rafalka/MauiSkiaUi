using MauiSkiaUi.Core;
using Microsoft.Maui.Controls.Shapes;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// MAUI parity P6, borders: <c>StrokeShape</c> (shapes of either layer or MAUI's, and MAUI's markup), brush strokes,
/// dashes, the content inset by the stroke and clipped to its inner edge, on both layers.
/// </summary>
[Collection(RuntimeXamlCollection.Name)]
public class BorderShapeTests
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

    /// <summary>A border around a red box that fills the content slot.</summary>
    private static SkUiBorder Bordered(IShape? shape, double thickness = 4) => new()
    {
        StrokeShape = shape,
        Stroke = Colors.Blue,
        StrokeThickness = thickness,
        Content = new SkUiBox { Color = Colors.Red },
    };

    [Fact]
    public void ContentSitsInsidePaddingAndStroke()
    {
        var content = new SkUiBox();
        var border = new SkUiBorder { StrokeThickness = 3, Padding = new Thickness(5), Content = content };
        SkUiTestHelpers.Arrange(border, 100, 60);
        Assert.Equal(new Rect(8, 8, 84, 44), content.Frame);
        Assert.Equal(new Size(56, 56), ((IView)border).Measure(double.PositiveInfinity, double.PositiveInfinity)); // 40 + 2 × (5 + 3)
        border.StrokeThickness = 1;
        SkUiTestHelpers.Arrange(border, 100, 60);
        Assert.Equal(6, content.Frame.X);

        var coreContent = new SkUiCoreBox();
        var core = new SkUiCoreBorder().SetStrokeThickness(3).SetPadding(new Thickness(5)).SetContent(coreContent);
        core.Measure(100, 60);
        core.Arrange(new Rect(0, 0, 100, 60));
        Assert.Equal(new Rect(8, 8, 84, 44), coreContent.Frame);
    }

    [Fact]
    public void DefaultShapeIsMauisRectangle()
    {
        var border = Bordered(null);
        Assert.Equal(default, border.CornerRadius);
        using var bitmap = Render(border, 40, 40);
        Assert.Equal(Blue, bitmap.GetPixel(0, 0)); // square corners, the stroke inside the bounds
        Assert.Equal(Red, bitmap.GetPixel(4, 4)); // the content right inside the stroke
    }

    [Fact]
    public void ContentIsClippedToTheInnerEdgeOfAnEllipse()
    {
        using var bitmap = Render(Bordered(new SkUiEllipse()), 60, 60);
        Assert.Equal(Red, bitmap.GetPixel(30, 30));
        Assert.Equal(Blue, bitmap.GetPixel(30, 2)); // the stroke along the ellipse
        Assert.Equal(0, bitmap.GetPixel(6, 6).Alpha); // inside the stroke inset, outside the ellipse: no content
        Assert.Equal(0, bitmap.GetPixel(1, 1).Alpha);
    }

    [Fact]
    public void RoundedContentClipIsConcentricWithTheStroke()
    {
        // RoundRectangle 20 with a 4-DIP stroke: the stroke follows a 20-DIP arc 2 DIPs in, the content a 18-DIP arc 4 DIPs in.
        using var bitmap = Render(Bordered(new RoundRectangle { CornerRadius = 20 }), 80, 80);
        Assert.Equal(Red, bitmap.GetPixel(40, 40));
        Assert.Equal(Blue, bitmap.GetPixel(40, 1));
        Assert.Equal(0, bitmap.GetPixel(3, 3).Alpha); // outside the rounded corner
        Assert.Equal(Red, bitmap.GetPixel(10, 10)); // inside the content arc
        // A corner pixel just inside the stroke's arc is stroke or content, never the background behind.
        var corner = bitmap.GetPixel(7, 7);
        Assert.True(corner.Alpha == 255 && corner.Green == 0, $"{corner}");
    }

    [Fact]
    public void MauiMarkupShapesTheBorder()
    {
        // From the MAUI Border docs, with only the prefix changed.
        const string xaml = """
            <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <sk:SkUiVerticalStackLayout>
                <sk:SkUiBorder Stroke="#C49B33" StrokeThickness="4" StrokeShape="RoundRectangle 40,0,0,40" Background="#2B0B98"
                               Padding="16,8" HorizontalOptions="Center">
                  <sk:SkUiLabel Text=".NET MAUI" TextColor="White" FontSize="18" FontAttributes="Bold" />
                </sk:SkUiBorder>
                <sk:SkUiBorder StrokeThickness="4" Background="#2B0B98" Padding="16,8" HorizontalOptions="Center">
                  <sk:SkUiBorder.StrokeShape>
                    <RoundRectangle CornerRadius="40,0,0,40" />
                  </sk:SkUiBorder.StrokeShape>
                  <sk:SkUiBorder.Stroke>
                    <LinearGradientBrush EndPoint="0,1">
                      <GradientStop Color="Orange" Offset="0.1" />
                      <GradientStop Color="Brown" Offset="1.0" />
                    </LinearGradientBrush>
                  </sk:SkUiBorder.Stroke>
                  <sk:SkUiLabel Text=".NET MAUI" TextColor="White" FontSize="18" FontAttributes="Bold" />
                </sk:SkUiBorder>
                <sk:SkUiBorder StrokeShape="Ellipse" StrokeDashArray="4,2" StrokeLineCap="Round" StrokeLineJoin="Bevel" Stroke="Red" />
                <sk:SkUiBorder>
                  <sk:SkUiBorder.StrokeShape>
                    <sk:SkUiRoundRectangle CornerRadius="12" />
                  </sk:SkUiBorder.StrokeShape>
                </sk:SkUiBorder>
              </sk:SkUiVerticalStackLayout>
            </ContentView>
            """;
        var root = new ContentView();
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var borders = ((SkUiVerticalStackLayout)root.Content).Children.Cast<SkUiBorder>().ToArray();
        Assert.Equal(new CornerRadius(40, 0, 0, 40), Assert.IsType<RoundRectangle>(borders[0].StrokeShape).CornerRadius);
        Assert.Equal(Color.FromArgb("#C49B33"), ((SolidColorBrush)borders[0].Stroke!).Color);
        Assert.IsType<LinearGradientBrush>(borders[1].Stroke);
        Assert.IsType<RoundRectangle>(borders[1].StrokeShape);
        Assert.IsType<Ellipse>(borders[2].StrokeShape);
        Assert.Equal([4d, 2d], borders[2].StrokeDashArray);
        Assert.Equal((PenLineCap.Round, PenLineJoin.Bevel), (borders[2].StrokeLineCap, borders[2].StrokeLineJoin));
        Assert.Equal(new CornerRadius(12), Assert.IsType<SkUiRoundRectangle>(borders[3].StrokeShape).CornerRadius);
        // The label sits inside the padding plus the stroke.
        SkUiTestHelpers.Arrange(borders[0], 200, 60);
        Assert.Equal((20d, 12d), (borders[0].Content!.Frame.X, borders[0].Content!.Frame.Y));
    }

    [Fact]
    public void BindingsReachBrushesAndStrokeShapes()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        // Gradient stops inside a shape's brush and a border's stroke shape bind against the view's binding context, as on MAUI.
        const string xaml = """
            <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <sk:SkUiVerticalStackLayout>
                <sk:SkUiEllipse StrokeThickness="{Binding Thickness}" WidthRequest="40" HeightRequest="40">
                  <sk:SkUiEllipse.Fill>
                    <LinearGradientBrush>
                      <GradientStop Color="{Binding Start}" Offset="0" />
                      <GradientStop Color="Blue" Offset="1" />
                    </LinearGradientBrush>
                  </sk:SkUiEllipse.Fill>
                </sk:SkUiEllipse>
                <sk:SkUiBorder Stroke="{Binding Start}">
                  <sk:SkUiBorder.StrokeShape>
                    <RoundRectangle CornerRadius="{Binding Radius}" />
                  </sk:SkUiBorder.StrokeShape>
                </sk:SkUiBorder>
              </sk:SkUiVerticalStackLayout>
            </ContentView>
            """;
        var root = new ContentView();
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var model = new ShapeModel { Start = Colors.Red, Thickness = 3, Radius = 12 };
        root.BindingContext = model;
        var children = ((SkUiVerticalStackLayout)root.Content).Children;
        var ellipse = (SkUiEllipse)children[0];
        var border = (SkUiBorder)children[1];
        Assert.Equal(3, ellipse.StrokeThickness);
        Assert.Equal(Colors.Red, ((LinearGradientBrush)ellipse.Fill!).GradientStops[0].Color);
        Assert.Equal(Colors.Red, ((SolidColorBrush)border.Stroke!).Color);
        Assert.Equal(new CornerRadius(12), ((RoundRectangle)border.StrokeShape!).CornerRadius);

        var paints = 0;
        ellipse.PaintInvalidated += (_, _) => paints++;
        var borderPaints = 0;
        border.PaintInvalidated += (_, _) => borderPaints++;
        model.Start = Colors.Green;
        model.Radius = 4;
        Assert.Equal(Colors.Green, ((LinearGradientBrush)ellipse.Fill!).GradientStops[0].Color);
        Assert.True(paints > 0);
        Assert.Equal(new CornerRadius(4), ((RoundRectangle)border.StrokeShape!).CornerRadius);
        Assert.True(borderPaints > 0);
    }

    private sealed class ShapeModel : System.ComponentModel.INotifyPropertyChanged
    {
        private Color _start = Colors.Black;
        private double _radius;

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public double Thickness { get; init; }

        public Color Start
        {
            get => _start;
            set { _start = value; PropertyChanged?.Invoke(this, new(nameof(Start))); }
        }

        public double Radius
        {
            get => _radius;
            set { _radius = value; PropertyChanged?.Invoke(this, new(nameof(Radius))); }
        }
    }

    [Fact]
    public void GradientAndDashedStrokes()
    {
        var gradient = new SkUiBorder
        {
            StrokeThickness = 6,
            Stroke = new LinearGradientBrush([new GradientStop(Colors.Red, 0), new GradientStop(Colors.Blue, 1)], new Point(0, 0), new Point(1, 0)),
        };
        using (var bitmap = Render(gradient, 100, 20))
        {
            var left = bitmap.GetPixel(2, 10);
            var right = bitmap.GetPixel(97, 10);
            Assert.True(left.Red > 230 && left.Blue < 25, $"{left}");
            Assert.True(right.Blue > 230 && right.Red < 25, $"{right}");
        }

        // Dash 2, gap 2 at a thickness of 4 along the top edge (from the top-left corner, 2 DIPs in): 8 on, 8 off.
        var dashed = new SkUiBorder { Stroke = Colors.Red, StrokeThickness = 4 };
        dashed.StrokeDashArray = [2, 2];
        using (var bitmap = Render(dashed, 100, 40))
        {
            Assert.Equal(Red, bitmap.GetPixel(6, 2));
            Assert.Equal(0, bitmap.GetPixel(14, 2).Alpha);
            Assert.Equal(Red, bitmap.GetPixel(22, 2));
        }
    }

    [Fact]
    public void StrokeShapeEditsRedrawAndReclip()
    {
        var shape = new RoundRectangle { CornerRadius = 0 };
        var border = Bordered(shape);
        using (var bitmap = Render(border, 60, 60))
            Assert.Equal(Blue, bitmap.GetPixel(1, 1));
        var invalidated = 0;
        border.PaintInvalidated += (_, _) => invalidated++;
        shape.CornerRadius = 30; // a circle
        Assert.True(invalidated > 0);
        using (var bitmap = Render(border, 60, 60))
        {
            Assert.Equal(0, bitmap.GetPixel(1, 1).Alpha);
            Assert.Equal(0, bitmap.GetPixel(5, 5).Alpha); // the content is re-clipped too (it filled this corner before)
        }

        var own = new SkUiRoundRectangle();
        border.StrokeShape = own;
        invalidated = 0;
        own.CornerRadius = 10;
        Assert.True(invalidated > 0);

        var coreShape = new SkUiCoreRoundRectangle();
        var core = new SkUiCoreBorder().SetStrokeShape(coreShape).SetStroke(Colors.Blue);
        var coreInvalidated = 0;
        core.PaintInvalidated += (_, _) => coreInvalidated++;
        coreShape.SetCornerRadius(8);
        Assert.True(coreInvalidated > 0);
    }

    public static TheoryData<string> Shapes => ["None", "CornerRadius", "Rectangle", "RoundRectangle", "Ellipse", "Path", "MauiRoundRectangle"];

    [Theory]
    [MemberData(nameof(Shapes))]
    public void CoreBorderDrawsTheSamePixels(string kind)
    {
        const string ticket = "M 0,10 L 10,0 L 90,0 L 100,10 L 100,90 L 90,100 L 10,100 L 0,90 Z";
        var skia = new SkUiBorder
        {
            Stroke = Colors.Blue, StrokeThickness = 5, BackgroundColor = Colors.White, Padding = new Thickness(2),
            Content = new SkUiBox { Color = Colors.Red },
        };
        skia.StrokeDashArray = [3, 1];
        var core = new SkUiCoreBorder().SetStroke(Colors.Blue).SetStrokeThickness(5).SetBackgroundColor(Colors.White)
            .SetPadding(new Thickness(2)).SetContent(new SkUiCoreBox().SetColor(Colors.Red));
        core.SetStrokeDashArray(3, 1);
        switch (kind)
        {
            case "CornerRadius":
                skia.CornerRadius = new CornerRadius(20, 4, 0, 12);
                core.SetCornerRadius(new CornerRadius(20, 4, 0, 12));
                break;
            case "Rectangle":
                skia.StrokeShape = new SkUiRectangle { RadiusX = 9 };
                core.SetStrokeShape(new SkUiCoreRectangle().SetRadiusX(9));
                break;
            case "RoundRectangle":
                skia.StrokeShape = new SkUiRoundRectangle { CornerRadius = 16 };
                core.SetStrokeShape(new SkUiCoreRoundRectangle().SetCornerRadius(16));
                break;
            case "Ellipse":
                skia.StrokeShape = new SkUiEllipse();
                core.SetStrokeShape(new SkUiCoreEllipse());
                break;
            case "Path":
                skia.StrokeShape = new SkUiPath { Aspect = Stretch.Fill }.SetData(ticket);
                core.SetStrokeShape(new SkUiCorePath(ticket).SetAspect(SkUiCoreStretch.Fill));
                break;
            case "MauiRoundRectangle":
                // Any MAUI Graphics shape works on Core too; MAUI's own round rectangle draws as the Core one.
                skia.StrokeShape = new RoundRectangle { CornerRadius = 16 };
                core.SetStrokeShape(new SkUiCoreRoundRectangle().SetCornerRadius(16));
                break;
        }
        using var expected = Render(skia, 90, 70);
        using var actual = Render(core, 90, 70);
        Assert.Equal(expected.Pixels, actual.Pixels);
    }

    [Fact]
    public void SharedStrokeShapesDoNotKeepBordersAlive()
    {
        var shape = new RoundRectangle { CornerRadius = 8 };
        var references = Create(shape);
        for (var attempt = 0; attempt < 5 && references.Any(reference => reference.TryGetTarget(out _)); attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        Assert.All(references, reference => Assert.False(reference.TryGetTarget(out _)));
        GC.KeepAlive(shape);

        static WeakReference<SkUiBorder>[] Create(IShape shape) =>
            Enumerable.Range(0, 4).Select(_ => new WeakReference<SkUiBorder>(new SkUiBorder { StrokeShape = shape })).ToArray();
    }
}
