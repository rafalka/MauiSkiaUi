using MauiSkiaUi.Core;
using Microsoft.Maui.Controls.Shapes;
using SkiaSharp;
using MauiSkiaUiDemo;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// MAUI parity P7: gradient backgrounds wherever a solid fill was drawn, <c>Clip</c> geometry and <c>Shadow</c> on every
/// view of both layers, composited without re-recording (FR-11, FR-20).
/// </summary>
[Collection(RuntimeXamlCollection.Name)]
public class BrushShadowClipTests
{
    private static LinearGradientBrush RedToBlue() => new()
    {
        StartPoint = new Point(0, 0),
        EndPoint = new Point(1, 0),
        GradientStops = { new GradientStop(Colors.Red, 0), new GradientStop(Colors.Blue, 1) }
    };

    private static LinearGradientPaint RedToBluePaint() => new(
        [new PaintGradientStop(0, Colors.Red), new PaintGradientStop(1, Colors.Blue)], new Point(0, 0), new Point(1, 0));

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

    /// <summary>Red on the left, blue on the right, in the middle row.</summary>
    private static void AssertRedToBlue(SKBitmap bitmap, string name)
    {
        var y = bitmap.Height / 2;
        var left = bitmap.GetPixel(8, y);
        var right = bitmap.GetPixel(bitmap.Width - 8, y);
        Assert.True(left.Red > 200 && left.Blue < 60, $"{name}: left {left}");
        Assert.True(right.Blue > 200 && right.Red < 60, $"{name}: right {right}");
    }

    /// <summary>A fill just short of opaque: shapes with it cast their shadow from what they draw (content shadows).</summary>
    private static readonly Color Translucent = Colors.White.WithAlpha(0.98f);

    private static bool IsDark(SKColor color) => color.Alpha > 200 && color.Red < 40 && color.Green < 40 && color.Blue < 40;

    public static TheoryData<string> GradientViews => ["view", "layout", "label", "chip", "button", "border", "box", "roundedBox", "imageButton", "image"];

    private static SkUiView CreateGradientView(string kind) => kind switch
    {
        "view" => new SkUiContentView(),
        "layout" => new SkUiVerticalStackLayout(),
        "label" => new SkUiLabel { Text = "" },
        "chip" => new SkUiLabel { Text = "", CornerRadius = 8, BorderColor = Colors.Black, BorderWidth = 1 },
        "button" => new SkUiButton { Text = "" },
        "border" => new SkUiBorder { StrokeShape = new RoundRectangle { CornerRadius = 10 }, Stroke = Colors.Black },
        "box" => new SkUiBox(),
        "roundedBox" => new SkUiBox { CornerRadius = 12 },
        "imageButton" => new SkUiImageButton { CornerRadius = 8 },
        _ => new SkUiImage()
    };

    [Theory]
    [MemberData(nameof(GradientViews))]
    public void GradientBackgroundFillsEveryView(string kind)
    {
        var view = CreateGradientView(kind);
        view.Background = RedToBlue();
        using var bitmap = Render(view, 100, 40);
        AssertRedToBlue(bitmap, kind);
    }

    public static TheoryData<string> CoreGradientNodes => ["node", "panel", "label", "button", "border", "box", "imageButton", "table"];

    [Theory]
    [MemberData(nameof(CoreGradientNodes))]
    public void CoreBackgroundFillsEveryNode(string kind)
    {
        SkUiCoreNode node = kind switch
        {
            "node" => new SkUiCoreContentView(),
            "panel" => new SkUiCoreVerticalStackLayout(),
            "label" => new SkUiCoreLabel().SetCornerRadius(8),
            "button" => new SkUiCoreButton(),
            "border" => new SkUiCoreBorder().SetCornerRadius(new CornerRadius(10)).SetStroke(Colors.Black),
            "box" => new SkUiCoreBox().SetCornerRadius(new CornerRadius(12)),
            "imageButton" => new SkUiCoreImageButton().SetCornerRadii(new CornerRadius(8)),
            _ => new SkUiCoreTable()
        };
        node.SetBackground(RedToBluePaint());
        using var bitmap = Render(node, 100, 40);
        AssertRedToBlue(bitmap, kind);
    }

    [Fact]
    public void EmptyBrushesFallBackToBackgroundColorAndBoxColorWins()
    {
        using (var bitmap = Render(new SkUiContentView { Background = new LinearGradientBrush(), BackgroundColor = Colors.Lime }, 20, 20))
            Assert.Equal(SKColors.Lime, bitmap.GetPixel(10, 10));
        using (var bitmap = Render(new SkUiBox { Color = Colors.Lime, Background = RedToBlue() }, 100, 40))
            Assert.Equal(SKColors.Lime, bitmap.GetPixel(8, 20));
        using (var bitmap = Render(new SkUiCoreBox().SetColor(Colors.Lime).SetBackground(RedToBluePaint()), 100, 40))
            Assert.Equal(SKColors.Lime, bitmap.GetPixel(8, 20));
        // A Core label's Background replaces its FillColor.
        using (var bitmap = Render(new SkUiCoreLabel().SetFillColor(Colors.Lime).SetBackground(RedToBluePaint()), 100, 40))
            AssertRedToBlue(bitmap, "core label");
    }

    [Fact]
    public void RootSurfacesClearOnlyWithAnOpaqueBackground()
    {
        // A gradient from transparent wins over BackgroundColor: no red under its transparent end.
        var gradient = new LinearGradientBrush
        {
            EndPoint = new Point(1, 0),
            GradientStops = { new GradientStop(Colors.Transparent, 0), new GradientStop(Colors.Blue, 1) }
        };
        using (var surface = new SkUiTestSurface(new SkUiContentView { BackgroundColor = Colors.Red, Background = gradient }, 100, 20))
        {
            var bitmap = surface.Frame();
            Assert.True(bitmap.GetPixel(1, 10).Alpha < 10, bitmap.GetPixel(1, 10).ToString());
            Assert.True(bitmap.GetPixel(98, 10).Blue > 200);
        }
        // A translucent color is drawn once, not over a clear of the same color.
        using (var surface = new SkUiTestSurface(new SkUiContentView { Background = Colors.Red.WithAlpha(0.5f) }, 20, 20))
            Assert.InRange(surface.Frame().GetPixel(10, 10).Alpha, 120, 135);
        // An opaque root still clears with its color.
        using (var surface = new SkUiTestSurface(new SkUiContentView { BackgroundColor = Colors.Lime }, 20, 20))
            Assert.Equal(SKColors.Lime, surface.Frame().GetPixel(10, 10));
    }

    [Fact]
    public void GradientBackgroundsFollowRoundedShapes()
    {
        var box = new SkUiBox { CornerRadius = 20, Background = RedToBlue() };
        using var bitmap = Render(box, 100, 40);
        Assert.Equal(0, bitmap.GetPixel(1, 1).Alpha); // outside the rounded corner
        var border = new SkUiBorder { StrokeShape = new Ellipse(), StrokeThickness = 0, Background = RedToBlue() };
        using var ellipse = Render(border, 100, 40);
        Assert.Equal(0, ellipse.GetPixel(2, 2).Alpha);
        Assert.True(ellipse.GetPixel(50, 20).Alpha > 200);
    }

    [Fact]
    public void ButtonPassesTheGradientToTheLook()
    {
        var button = new SkUiButton { Text = "", Background = RedToBlue() };
        using var bitmap = Render(button, 100, 40);
        AssertRedToBlue(bitmap, "button");
        // A disabled button keeps the disabled color.
        button.IsEnabled = false;
        using var disabled = Render(button, 100, 40);
        Assert.Equal(disabled.GetPixel(8, 20), disabled.GetPixel(92, 20));
    }

    [Fact]
    public void ClipGeometryClipsContentAndChildrenButNotInput()
    {
        var tapped = 0;
        var view = new SkUiContentView
        {
            Clip = new EllipseGeometry { Center = new Point(50, 50), RadiusX = 50, RadiusY = 50 },
            Content = new SkUiBox { Color = Colors.Red },
            Background = Colors.Blue
        };
        view.Tapped += (_, _) => tapped++;
        using var bitmap = Render(view, 100, 100);
        Assert.Equal(0, bitmap.GetPixel(3, 3).Alpha);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(50, 50));
        // Input keeps the rectangular bounds (FR-11).
        view.Touch(new(1, SkUiTouchAction.Pressed, new Point(3, 3), TimeSpan.Zero));
        view.Touch(new(1, SkUiTouchAction.Released, new Point(3, 3), TimeSpan.FromMilliseconds(30)));
        Assert.Equal(1, tapped);
    }

    [Fact]
    public void CoreClipTakesShapesAndGeometries()
    {
        using (var bitmap = Render(new SkUiCoreBox().SetColor(Colors.Red).SetClip(new SkUiCoreEllipse()), 100, 40))
        {
            Assert.Equal(0, bitmap.GetPixel(2, 2).Alpha); // placed in the bounds, as when it shapes a border
            Assert.Equal(SKColors.Red, bitmap.GetPixel(50, 20));
        }
        using (var bitmap = Render(new SkUiCoreBox().SetColor(Colors.Red).SetClip(new RectangleGeometry(new Rect(10, 10, 20, 20))), 100, 40))
        {
            Assert.Equal(0, bitmap.GetPixel(5, 5).Alpha); // in the node's coordinates
            Assert.Equal(SKColors.Red, bitmap.GetPixel(20, 20));
        }
    }

    [Fact]
    public void ClipEditsReclipWithoutRecording()
    {
        var geometry = new EllipseGeometry { Center = new Point(20, 20), RadiusX = 20, RadiusY = 20 };
        var box = new SkUiBox { Color = Colors.Red, Clip = geometry };
        using var surface = new SkUiTestSurface(new SkUiContentView { Content = box }, 40, 40);
        Assert.Equal(0, surface.Frame().GetPixel(1, 1).Alpha);
        var recorded = surface.RecordedPictures;
        geometry.RadiusX = geometry.RadiusY = 40;
        Assert.Equal(SKColors.Red, surface.Frame(16).GetPixel(1, 1));
        box.Clip = null;
        Assert.Equal(SKColors.Red, surface.Frame(32).GetPixel(1, 1));
        Assert.Equal(recorded, surface.RecordedPictures);

        var core = new SkUiCoreBox().SetColor(Colors.Red);
        var shape = new SkUiCoreRectangle();
        core.SetClip(shape);
        using var coreSurface = new SkUiTestSurface(new SkUiCoreHost().SetContent(core), 40, 40);
        Assert.Equal(SKColors.Red, coreSurface.Frame().GetPixel(1, 1));
        recorded = coreSurface.RecordedPictures;
        shape.SetRadiusX(20);
        Assert.Equal(0, coreSurface.Frame(16).GetPixel(1, 1).Alpha); // a property of the clip shape re-clips
        Assert.Equal(recorded, coreSurface.RecordedPictures);
    }

    /// <summary>A 40 × 40 box at (20, 20) in an 80 × 80 transparent root.</summary>
    private static (SkUiContentView Root, SkUiBox Box) ShadowScene(Shadow shadow, double cornerRadius = 0)
    {
        var box = new SkUiBox { Color = Colors.White, CornerRadius = cornerRadius, Shadow = shadow, WidthRequest = 40, HeightRequest = 40 };
        return (new SkUiContentView { Padding = 20, Content = box }, box);
    }

    private static Shadow Sharp(double x = 10, double y = 10, float opacity = 1) =>
        new() { Brush = Colors.Black, Offset = new Point(x, y), Radius = 0, Opacity = opacity };

    [Fact]
    public void ShadowPaintsOutsideTheBoundsAtItsOffset()
    {
        var (root, _) = ShadowScene(Sharp());
        using var bitmap = Render(root, 80, 80);
        Assert.True(IsDark(bitmap.GetPixel(65, 65)), bitmap.GetPixel(65, 65).ToString()); // not cut by the box's own clip
        Assert.Equal(SKColors.White, bitmap.GetPixel(25, 25));
        Assert.Equal(0, bitmap.GetPixel(15, 15).Alpha);
        Assert.Equal(0, bitmap.GetPixel(25, 65).Alpha);
    }

    [Fact]
    public void ShadowFollowsTheRoundedFill()
    {
        var (root, _) = ShadowScene(Sharp(), cornerRadius: 20);
        using var bitmap = Render(root, 80, 80);
        Assert.Equal(0, bitmap.GetPixel(68, 68).Alpha); // outside the shadow's circle
        Assert.True(IsDark(bitmap.GetPixel(50, 66)));
    }

    [Fact]
    public void ShadowWithoutAnOpaqueFillFollowsTheDrawnContent()
    {
        var ellipse = new SkUiEllipse { Fill = Translucent, StrokeThickness = 0, WidthRequest = 40, HeightRequest = 40, Shadow = Sharp() };
        var root = new SkUiContentView { Padding = 20, Content = ellipse };
        using var bitmap = Render(root, 80, 80);
        Assert.Equal(0, bitmap.GetPixel(68, 68).Alpha);
        Assert.True(IsDark(bitmap.GetPixel(50, 66)));
        // Transparent text casts its glyphs' shadow (in the bundled font: Linux agents may have no system fonts).
        using var font = SkUiTestHelpers.UseBundledFont();
        var label = new SkUiLabel
        {
            Text = "IIII", FontSize = 30, FontFamily = SkUiTestHelpers.BundledFontFamily, TextColor = Colors.White, Shadow = Sharp(0, 20)
        };
        using var text = Render(new SkUiContentView { Content = label }, 120, 80);
        var shadowed = 0;
        for (var x = 0; x < 120; x++)
            for (var y = 40; y < 80; y++)
                if (IsDark(text.GetPixel(x, y))) shadowed++;
        Assert.InRange(shadowed, 20, 120 * 40 / 2);
    }

    [Fact]
    public void OpaqueShapesCastFromTheirOutline()
    {
        var ellipse = new SkUiEllipse { Fill = Colors.White, StrokeThickness = 0, WidthRequest = 40, HeightRequest = 40, Shadow = Sharp() };
        var ring = new SkUiEllipse { Stroke = Colors.White, StrokeThickness = 6, WidthRequest = 40, HeightRequest = 40, Shadow = Sharp() };
        var dashed = new SkUiEllipse { Stroke = Colors.White, StrokeThickness = 6, StrokeDashArray = [2, 2], WidthRequest = 40, HeightRequest = 40, Shadow = Sharp() };
        var core = new SkUiCoreEllipse().SetFill(Colors.White).SetStrokeThickness(0).SetShadow(new SkUiCoreShadow(Colors.Black, new Point(10, 10), 0));
        foreach (var (view, outline) in new (ISkUiShadowCaster Caster, bool Outline)[] { (ellipse, true), (ring, true), (dashed, false), (core, true) }.Select(pair => (pair.Caster, pair.Outline)))
        {
            using var path = view.CreateShadowOutline(40, 40);
            Assert.Equal(outline, path is not null);
        }
        // Drawn from the outline: the circle's silhouette, and no content raster.
        using var surface = new SkUiTestSurface(new SkUiContentView { Padding = 20, Content = ellipse }, 80, 80);
        surface.Frame();
        var bitmap = surface.Frame(16);
        Assert.Equal(0, bitmap.GetPixel(68, 68).Alpha);
        Assert.True(IsDark(bitmap.GetPixel(50, 66)));
        Assert.Equal(0, surface.Renderer.Compositor.ShadowRasterizations + surface.Renderer.Compositor.LiveShadows);
        // A stroke-only ring's shadow is a ring too.
        using var rings = Render(new SkUiContentView { Padding = 20, Content = ring }, 80, 80);
        Assert.Equal(0, rings.GetPixel(56, 56).Alpha); // in the hole of the ring's shadow, outside the ring itself
        Assert.True(IsDark(rings.GetPixel(50, 67)));
    }

    [Fact]
    public void ShadowBlursAndFades()
    {
        var (blurred, _) = ShadowScene(new Shadow { Brush = Colors.Black, Offset = new Point(10, 10), Radius = 10 });
        using (var bitmap = Render(blurred, 80, 80))
        {
            Assert.InRange(bitmap.GetPixel(72, 50).Alpha, 1, 200); // soft edge past the sharp shadow
            Assert.True(bitmap.GetPixel(50, 75).Alpha > 0);
        }
        var (faded, _) = ShadowScene(Sharp(opacity: 0.5f));
        using (var bitmap = Render(faded, 80, 80))
            Assert.InRange(bitmap.GetPixel(65, 65).Alpha, 120, 135);
        var (translucent, _) = ShadowScene(new Shadow { Brush = Colors.Black.WithAlpha(0.5f), Offset = new Point(10, 10), Radius = 0, Opacity = 0.5f });
        using (var bitmap = Render(translucent, 80, 80))
            Assert.InRange(bitmap.GetPixel(65, 65).Alpha, 56, 72); // the color's alpha and the opacity multiply, as MAUI
    }

    [Fact]
    public void GradientShadowsMapOntoTheBounds()
    {
        var shadow = new Shadow { Brush = RedToBlue(), Offset = new Point(0, 30), Radius = 0 };
        var box = new SkUiBox { Color = Colors.White, Shadow = shadow, WidthRequest = 80, HeightRequest = 30 };
        using var bitmap = Render(new SkUiContentView { Padding = new Thickness(0, 0, 0, 40), Content = box }, 80, 70);
        var left = bitmap.GetPixel(4, 45);
        var right = bitmap.GetPixel(76, 45);
        Assert.True(left.Red > 200 && left.Blue < 60, left.ToString());
        Assert.True(right.Blue > 200 && right.Red < 60, right.ToString());
    }

    [Fact]
    public void ShadowChangesNeitherLayoutNorInput()
    {
        var (root, box) = ShadowScene(Sharp());
        var plain = new SkUiBox { Color = Colors.White, WidthRequest = 40, HeightRequest = 40 };
        Assert.Equal(((IView)plain).Measure(200, 200), ((IView)box).Measure(200, 200));
        var tapped = 0;
        box.Tapped += (_, _) => tapped++;
        SkUiTestHelpers.Arrange(root, 80, 80);
        root.Touch(new(1, SkUiTouchAction.Pressed, new Point(65, 65), TimeSpan.Zero));
        root.Touch(new(1, SkUiTouchAction.Released, new Point(65, 65), TimeSpan.FromMilliseconds(30)));
        Assert.Equal(0, tapped);
        root.Touch(new(2, SkUiTouchAction.Pressed, new Point(30, 30), TimeSpan.FromMilliseconds(100)));
        root.Touch(new(2, SkUiTouchAction.Released, new Point(30, 30), TimeSpan.FromMilliseconds(130)));
        Assert.Equal(1, tapped);
    }

    [Fact]
    public void ShadowIsClippedByAClippingParentOnly()
    {
        var box = new SkUiBox { Color = Colors.White, Shadow = Sharp() };
        var parent = new SkUiContentView { Content = box };
        var root = new SkUiContentView { Padding = 20, Content = parent };
        using (var bitmap = Render(root, 80, 80))
            Assert.True(IsDark(bitmap.GetPixel(65, 65))); // layouts do not clip by default
        parent.ClipToBounds = true;
        using (var bitmap = Render(root, 80, 80))
            Assert.Equal(0, bitmap.GetPixel(65, 65).Alpha);
    }

    [Fact]
    public void ShadowFollowsTheClip()
    {
        var (root, box) = ShadowScene(Sharp());
        box.Clip = new EllipseGeometry { Center = new Point(20, 20), RadiusX = 20, RadiusY = 20 };
        using var bitmap = Render(root, 80, 80);
        Assert.Equal(0, bitmap.GetPixel(68, 68).Alpha);
        Assert.Equal(0, bitmap.GetPixel(22, 22).Alpha); // the box itself is clipped too
        Assert.True(IsDark(bitmap.GetPixel(50, 66)));
    }

    [Fact]
    public void OpacityFadesTheShadowWithTheView()
    {
        var (root, box) = ShadowScene(Sharp());
        box.Opacity = 0.5;
        using var bitmap = Render(root, 80, 80);
        Assert.InRange(bitmap.GetPixel(65, 65).Alpha, 120, 135);
    }

    [Fact]
    public void ShadowEditsRedrawWithoutRecording()
    {
        var shadow = Sharp();
        var (root, _) = ShadowScene(shadow);
        using var surface = new SkUiTestSurface(root, 80, 80);
        Assert.True(IsDark(surface.Frame().GetPixel(65, 65)));
        var recorded = surface.RecordedPictures;
        shadow.Offset = new Point(-10, -10);
        var moved = surface.Frame(16);
        Assert.Equal(0, moved.GetPixel(65, 65).Alpha);
        Assert.True(IsDark(moved.GetPixel(15, 15)));
        shadow.Brush = RedToBlue();
        Assert.True(surface.Frame(32).GetPixel(12, 30).Red > 200);
        ((LinearGradientBrush)shadow.Brush).GradientStops[0].Color = Colors.Lime;
        Assert.True(surface.Frame(48).GetPixel(12, 30).Green > 200); // a gradient stop of the shadow's brush
        Assert.Equal(recorded, surface.RecordedPictures);
    }

    [Fact]
    public void ScrollingAndTransformsNeitherRecordNorBlurAgain()
    {
        var card = new SkUiEllipse { Fill = Translucent, StrokeThickness = 0, HeightRequest = 40, Shadow = new Shadow { Brush = Colors.Black, Radius = 8 } };
        var clipped = new SkUiBox { Color = Colors.Red, HeightRequest = 40, Clip = new EllipseGeometry { Center = new Point(20, 20), RadiusX = 20, RadiusY = 20 } };
        var stack = new SkUiVerticalStackLayout { Padding = 20, Spacing = 20 };
        stack.Children.Add(card);
        stack.Children.Add(clipped);
        for (var index = 0; index < 4; index++)
            stack.Children.Add(new SkUiBox { Color = Colors.Gray, HeightRequest = 40, Shadow = Sharp() });
        var scroll = new SkUiScrollView { Content = stack };
        using var surface = new SkUiTestSurface(scroll, 100, 160);
        var compositor = surface.Renderer.Compositor;
        surface.Frame();
        Assert.Equal(1, compositor.LiveShadows); // the content shadow, while its subtree is new
        surface.Frame(16);
        Assert.Equal(1, compositor.ShadowRasterizations); // then rasterized once
        var recorded = surface.RecordedPictures;
        for (var step = 1; step <= 4; step++)
        {
            scroll.ScrollTo(0, step * 10);
            surface.Frame(16 + step * 16);
        }
        card.TranslationX = 6;
        clipped.Rotation = 10;
        card.Opacity = 0.8;
        surface.Frame(200);
        Assert.Equal(recorded, surface.RecordedPictures);
        Assert.Equal(1, compositor.ShadowRasterizations);
        Assert.Equal(1, compositor.LiveShadows);

        // A content change rasterizes again, once it is stable.
        card.Fill = Colors.Yellow.WithAlpha(0.98f); // still translucent: still a content shadow
        surface.Frame(216);
        surface.Frame(232);
        surface.Frame(248);
        Assert.Equal(2, compositor.ShadowRasterizations);
    }

    [Fact]
    public void ReplacedShadowsAndClipsReleaseTheirNativeObjects()
    {
        var shadow = new Shadow { Brush = RedToBlue(), Offset = new Point(4, 4), Radius = 6 };
        var clip = new EllipseGeometry { Center = new Point(20, 20), RadiusX = 20, RadiusY = 20 };
        var box = new SkUiBox { Color = Colors.White, Shadow = shadow, Clip = clip, WidthRequest = 40, HeightRequest = 40 };
        using var surface = new SkUiTestSurface(new SkUiContentView { Padding = 20, Content = box }, 80, 80);
        surface.Frame();
        var committed = box.RenderState.Node.Props;
        var style = committed.Shadow!.Style;
        var oldClip = committed.ClipPath!;
        Assert.NotEqual(IntPtr.Zero, style.Shader!.Handle);
        shadow.Radius = 10; // a new style
        clip.RadiusX = 18; // a new clip path
        surface.Frame(16);
        Assert.Equal(IntPtr.Zero, style.Shader.Handle);
        Assert.Equal(IntPtr.Zero, style.Blur!.Handle);
        Assert.Equal(IntPtr.Zero, oldClip.Handle);
        // The current ones stay alive and keep drawing.
        Assert.NotEqual(IntPtr.Zero, box.RenderState.Node.Props.Shadow!.Style.Shader!.Handle);
        Assert.NotEqual(IntPtr.Zero, box.RenderState.Node.Props.ClipPath!.Handle);
        Assert.True(surface.Frame(32).GetPixel(44, 66).Alpha > 0); // below the clipped box, in its shadow
    }

    [Fact]
    public void ContentShadowsRasterizeOnIdleFramesWithinABudget()
    {
        var stack = new SkUiVerticalStackLayout { Padding = 10, Spacing = 10 };
        for (var index = 0; index < 8; index++)
            stack.Children.Add(new SkUiEllipse { Fill = Translucent, StrokeThickness = 0, HeightRequest = 30, Shadow = Sharp(3, 3) });
        using var surface = new SkUiTestSurface(new SkUiContentView { Content = stack }, 120, 420);
        var compositor = surface.Renderer.Compositor;
        surface.Frame();
        Assert.Equal(8, compositor.LiveShadows); // new subtrees: drawn live…
        Assert.True(surface.NeedsFrame); // …and a follow-up frame is requested, not left for the next scroll
        var counts = new List<int>();
        for (var frame = 1; frame <= 5 && surface.NeedsFrame; frame++)
        {
            var before = compositor.ShadowRasterizations;
            surface.Frame(frame * 16);
            counts.Add(compositor.ShadowRasterizations - before);
        }
        Assert.Equal([3, 3, 2], counts); // spread over frames by the per-frame budget
        Assert.False(surface.NeedsFrame); // then idle
        Assert.Equal(8, compositor.ShadowRasterizations);
    }

    [Fact]
    public void RenderThreadAnimationsDoNotBlurAgain()
    {
        var card = new SkUiEllipse { Fill = Translucent, StrokeThickness = 0, Shadow = new Shadow { Brush = Colors.Black, Radius = 8 } };
        using var surface = new SkUiTestSurface(new SkUiContentView { Padding = 30, Content = card }, 120, 120);
        var compositor = surface.Renderer.Compositor;
        surface.Frame();
        surface.Frame(16);
        _ = card.AnimateAsync(SkUiAnimatableProperty.Scale, 1.5, 100);
        for (var time = 32; time <= 200; time += 16)
            surface.Frame(time);
        Assert.Equal(1, compositor.ShadowRasterizations);
    }

    [Fact]
    public void CompositorMatchesTheImmediatePainter()
    {
        static SkUiView Scene()
        {
            var stack = new SkUiVerticalStackLayout { Padding = 16, Spacing = 16, Background = Colors.White };
            stack.Children.Add(new SkUiBorder
            {
                HeightRequest = 30, StrokeShape = new RoundRectangle { CornerRadius = 8 }, Stroke = Colors.Black, Background = RedToBlue(),
                Shadow = new Shadow { Brush = Colors.Black, Offset = new Point(3, 4), Radius = 6, Opacity = 0.6f }
            });
            stack.Children.Add(new SkUiEllipse { Fill = Colors.Teal, StrokeThickness = 0, HeightRequest = 30, Shadow = Sharp(4, 4) });
            stack.Children.Add(new SkUiBox
            {
                Color = Colors.Orange, HeightRequest = 30, Shadow = Sharp(), Clip = new EllipseGeometry { Center = new Point(40, 15), RadiusX = 40, RadiusY = 15 }
            });
            return stack;
        }
        using var immediate = Render(Scene(), 120, 160);
        using var surface = new SkUiTestSurface(Scene(), 120, 160);
        surface.Frame();
        var live = surface.Frame(16).Copy(); // the content shadow from the compositor's raster
        var different = 0;
        for (var x = 0; x < 120; x++)
            for (var y = 0; y < 160; y++)
            {
                var a = immediate.GetPixel(x, y);
                var b = live.GetPixel(x, y);
                if (Math.Abs(a.Red - b.Red) > 8 || Math.Abs(a.Green - b.Green) > 8 || Math.Abs(a.Blue - b.Blue) > 8 || Math.Abs(a.Alpha - b.Alpha) > 8)
                    different++;
            }
        live.Dispose();
        Assert.Equal(0, different);
    }

    [Fact]
    public void CoreShadowsMatchTheSkUiLayer()
    {
        var (root, _) = ShadowScene(new Shadow { Brush = Colors.Black, Offset = new Point(6, 8), Radius = 6, Opacity = 0.7f }, cornerRadius: 10);
        using var expected = Render(root, 80, 80);
        var core = new SkUiCoreBox().SetColor(Colors.White).SetCornerRadius(new CornerRadius(10))
            .SetShadow(new SkUiCoreShadow(Colors.Black, new Point(6, 8), 6, 0.7f)).SetWidth(40).SetHeight(40);
        var host = new SkUiCoreContentView().SetPadding(new Thickness(20)).SetContent(core);
        using var actual = Render(host, 80, 80);
        for (var x = 0; x < 80; x += 3)
            for (var y = 0; y < 80; y += 3)
                Assert.Equal(expected.GetPixel(x, y), actual.GetPixel(x, y));

        // A content shadow on Core: an ellipse's silhouette.
        var ellipse = new SkUiCoreEllipse().SetFill(Translucent).SetStrokeThickness(0).SetShadow(new SkUiCoreShadow(Colors.Black, new Point(10, 10), 0));
        ellipse.SetWidth(40).SetHeight(40);
        using var silhouette = Render(new SkUiCoreContentView().SetPadding(new Thickness(20)).SetContent(ellipse), 80, 80);
        Assert.Equal(0, silhouette.GetPixel(68, 68).Alpha);
        Assert.True(IsDark(silhouette.GetPixel(50, 66)));
    }

    [Fact]
    public void MauiDocSamplesLoadWithThePrefixChanged()
    {
        const string xaml = """
            <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <sk:SkUiVerticalStackLayout>
                <sk:SkUiImage WidthRequest="250" HeightRequest="310">
                  <sk:SkUiImage.Shadow>
                    <Shadow Brush="Black" Offset="20,20" Radius="40" Opacity="0.8" />
                  </sk:SkUiImage.Shadow>
                </sk:SkUiImage>
                <sk:SkUiLabel Text="Shadow" Shadow="4 4 16 Black 0.5" />
                <sk:SkUiImage>
                  <sk:SkUiImage.Clip>
                    <EllipseGeometry RadiusX="100" RadiusY="100" Center="180,180" />
                  </sk:SkUiImage.Clip>
                </sk:SkUiImage>
                <sk:SkUiBorder StrokeShape="RoundRectangle 12" Stroke="LightGray" HeightRequest="120" WidthRequest="120">
                  <sk:SkUiBorder.Background>
                    <LinearGradientBrush EndPoint="1,0">
                      <GradientStop Color="Yellow" Offset="0.1" />
                      <GradientStop Color="Green" Offset="1.0" />
                    </LinearGradientBrush>
                  </sk:SkUiBorder.Background>
                </sk:SkUiBorder>
                <sk:SkUiBox WidthRequest="120" HeightRequest="60">
                  <sk:SkUiBox.Background>
                    <RadialGradientBrush Center="0.5,0.5" Radius="0.5">
                      <GradientStop Color="Red" Offset="0.1" />
                      <GradientStop Color="DarkBlue" Offset="1.0" />
                    </RadialGradientBrush>
                  </sk:SkUiBox.Background>
                </sk:SkUiBox>
              </sk:SkUiVerticalStackLayout>
            </ContentView>
            """;
        var root = new ContentView();
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var children = ((SkUiVerticalStackLayout)root.Content).Children;
        var shadow = ((SkUiImage)children[0]).Shadow;
        Assert.Equal((new Point(20, 20), 40f, 0.8f), (shadow.Offset, shadow.Radius, shadow.Opacity));
        Assert.Equal(16, ((SkUiLabel)children[1]).Shadow.Radius);
        Assert.IsType<EllipseGeometry>(((SkUiImage)children[2]).Clip);
        var border = (SkUiBorder)children[3];
        SkUiTestHelpers.Arrange(border, 120, 120);
        using var bitmap = Render(border.Paint, 120, 120);
        var left = bitmap.GetPixel(14, 60);
        var right = bitmap.GetPixel(106, 60);
        Assert.True(left.Red > 200 && left.Green > 200, left.ToString()); // yellow
        Assert.True(right.Red < 60 && right.Green > 90, right.ToString()); // green
        var box = (SkUiBox)children[4];
        SkUiTestHelpers.Arrange(box, 120, 60);
        using var radial = Render(box.Paint, 120, 60);
        Assert.True(radial.GetPixel(60, 30).Red > 200);
        Assert.True(radial.GetPixel(4, 30).Blue > 100);
    }
}

/// <summary>Gradients and shadows across look and color scheme swaps (P7 acceptance).</summary>
[Collection(GlobalStateCollection.Name)]
public class BrushLookSwapTests
{
    [Fact]
    public void GradientsAndShadowsSurviveLookAndSchemeSwaps()
    {
        var previousLook = SkUiLook.Current;
        var previousScheme = SkUiColorScheme.Current;
        try
        {
            var button = new SkUiButton
            {
                Text = "",
                Background = new LinearGradientBrush
                {
                    EndPoint = new Point(1, 0),
                    GradientStops = { new GradientStop(Colors.Red, 0), new GradientStop(Colors.Blue, 1) }
                },
                Shadow = new Shadow { Brush = Colors.Black, Offset = new Point(0, 10), Radius = 0 },
                WidthRequest = 100, HeightRequest = 30
            };
            var root = new SkUiContentView { Padding = new Thickness(0, 0, 0, 20), Content = button };
            using var surface = new SkUiTestSurface(root, 100, 50);
            SkUiButtonPaint? drawn = null;
            var look = new DefaultSkUiLook();
            look.ButtonPainter = (canvas, paint) => { drawn = paint; DefaultSkUiLook.Instance.DrawButton(canvas, paint); };
            SkUiLook.Current = look;
            SkUiColorScheme.Current = new DarkSkUiColorScheme();
            Rendering.SkUiRenderInvalidation.MarkLookChanged(root);
            SkUiTestHelpers.Arrange(root, 100, 50);
            var bitmap = surface.Frame(16);
            Assert.IsType<LinearGradientPaint>(drawn!.Value.FillPaint);
            Assert.True(bitmap.GetPixel(6, 15).Red > 200);
            Assert.True(bitmap.GetPixel(94, 15).Blue > 200);
            Assert.True(bitmap.GetPixel(50, 36).Alpha > 200); // the shadow below the rounded button
        }
        finally
        {
            SkUiLook.Current = previousLook;
            SkUiColorScheme.Current = previousScheme;
        }
    }
}

/// <summary>The demo's effects stress scene: every effect builds on every layer, and scrolling it stays composite-time.</summary>
public class EffectsStressSceneTests
{
    public static TheoryData<EffectsStressLayer, StressEffects> Scenes()
    {
        var data = new TheoryData<EffectsStressLayer, StressEffects>();
        foreach (var layer in new[] { EffectsStressLayer.SkUi, EffectsStressLayer.Core })
            foreach (var effects in Enum.GetValues<StressEffects>())
                data.Add(layer, effects);
        return data;
    }

    [Theory]
    [MemberData(nameof(Scenes))]
    public void ScrollingTheSceneRecordsNothing(EffectsStressLayer layer, StressEffects effects)
    {
        var scroller = Assert.IsType<SkUiScrollView>(EffectsStressScene.Build(layer, 40, effects, hwAccelerated: false));
        using var surface = new SkUiTestSurface(scroller, 360, 400);
        surface.Frame();
        surface.Frame(16); // content shadows rasterize once their subtree is stable
        var recorded = surface.RecordedPictures;
        var rasterized = surface.Renderer.Compositor.ShadowRasterizations;
        for (var step = 1; step <= 10; step++)
        {
            scroller.ScrollTo(0, step * 80);
            surface.Frame(16 + step * 16);
        }
        Assert.True(scroller.ScrollY > 0);
        Assert.Equal(recorded, surface.RecordedPictures);
        // Text shadows of cards scrolled into view rasterize once each; cards already shown never again.
        if (!effects.HasFlag(StressEffects.TextShadow))
            Assert.Equal(rasterized, surface.Renderer.Compositor.ShadowRasterizations);
    }

    [Theory]
    [InlineData(StressEffects.None)]
    [InlineData(StressEffects.Gradient)]
    [InlineData(StressEffects.RoundedCorners)]
    [InlineData(StressEffects.ShapedBorder)]
    [InlineData(StressEffects.CardShadow)]
    [InlineData(StressEffects.TextShadow)]
    [InlineData(StressEffects.Clip)]
    [InlineData(StressEffects.Translucent)]
    [InlineData(StressEffects.All & ~StressEffects.Spinner)] // spinners stop at different angles between the two runs
    public void CoreSceneLooksLikeTheSkUiScene(StressEffects effects)
    {
        SKBitmap Render(EffectsStressLayer layer)
        {
            var scroller = (SkUiScrollView)EffectsStressScene.Build(layer, 6, effects, hwAccelerated: false);
            var surface = new SkUiTestSurface(scroller, 360, 400);
            surface.Frame();
            var bitmap = surface.Frame(16).Copy();
            surface.Dispose();
            return bitmap;
        }
        using var skUi = Render(EffectsStressLayer.SkUi);
        using var core = Render(EffectsStressLayer.Core);
        var different = 0;
        for (var x = 0; x < 360; x++)
            for (var y = 0; y < 400; y++)
            {
                var a = skUi.GetPixel(x, y);
                var b = core.GetPixel(x, y);
                if (Math.Abs(a.Red - b.Red) > 8 || Math.Abs(a.Green - b.Green) > 8 || Math.Abs(a.Blue - b.Blue) > 8 || Math.Abs(a.Alpha - b.Alpha) > 8)
                    different++;
            }
        Assert.True(different == 0, $"{different} pixels differ");
    }

    [Fact]
    public void NativeSceneBuilds()
    {
        var scroll = Assert.IsType<ScrollView>(EffectsStressScene.Build(EffectsStressLayer.NativeMaui, 10, StressEffects.All, hwAccelerated: false));
        var list = Assert.IsType<VerticalStackLayout>(scroll.Content);
        Assert.Equal(10, list.Count);
        var card = Assert.IsType<Border>(list[0]);
        Assert.NotNull(card.Shadow);
        Assert.IsType<Microsoft.Maui.Controls.Shapes.Path>(card.StrokeShape);
    }
}
