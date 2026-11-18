using MauiSkiaUi.Core;
using MauiSkiaUi.Rendering;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace MauiSkiaUi.Tests;

/// <summary>
/// A label that draws nothing but plain text casts its shadow from its glyphs on GPU surfaces (1.14 B2): the committed shadow
/// carries a picture of the glyphs in the shadow's color and blur, which the compositor draws instead of rasterizing the
/// content. Raster surfaces keep the raster (the tests force the glyph path on a raster canvas to compare the two).
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class TextShadowTests(ITestOutputHelper output)
{
    private const int Width = 160;
    private const int Height = 80; // the label (20 DIP margins) gets its full line height: no overflow

    /// <summary>Largest difference of one channel between the glyph shadow and the raster (of 255; the shadow is 35 % black).</summary>
    private const int Tolerance = 12;

    /// <summary>
    /// The same for a 1× surface. Where FreeType hints glyphs (Linux, Android), the raster blurs the hinted glyph masks and the
    /// glyph picture the unhinted outlines, which a small blur at 1× shows (25 measured on Linux, 10 on macOS); at phones'
    /// densities the two meet <see cref="Tolerance"/>.
    /// </summary>
    private const int LowDensityTolerance = 28;

    private static Shadow TitleShadow(float radius) => new() { Brush = Colors.Black, Offset = new Point(1, 2), Radius = radius, Opacity = 0.35f };

    private static SkUiLabel Label(string text = "Order 0042") => new()
    {
        Text = text, FontSize = 16, FontAttributes = FontAttributes.Bold, FontFamily = SkUiTestHelpers.BundledFontFamily, TextColor = Colors.White,
        Margin = new Thickness(20)
    };

    private static SkUiView Scene(bool core, float radius)
    {
        if (!core)
        {
            var label = Label();
            label.Shadow = TitleShadow(radius);
            return new SkUiContentView { Content = label, Background = Colors.White };
        }
        var coreLabel = new SkUiCoreLabel().SetText("Order 0042").SetFontSize(16).SetFontAttributes(FontAttributes.Bold)
            .SetFontFamily(SkUiTestHelpers.BundledFontFamily).SetTextColor(Colors.White);
        coreLabel.SetMargin(new Thickness(20)).SetShadow(new SkUiCoreShadow(Colors.Black, new Point(1, 2), radius, 0.35f));
        return new SkUiCoreHost { Background = Colors.White }.SetContent(coreLabel);
    }

    private static (SKBitmap Bitmap, SkUiCompositor Compositor) Render(SkUiView root, int density, bool glyphs)
    {
        using var surface = new SkUiTestSurface(root, Width, Height, density);
        surface.Renderer.Compositor.GlyphShadowsOnRaster = glyphs;
        surface.Frame(0);
        surface.Frame(16); // a content shadow is rasterized once its subtree was unchanged for a frame
        var bitmap = surface.Frame(32).Copy();
        return (bitmap, surface.Renderer.Compositor);
    }

    [Theory]
    [InlineData(false, 3f, 1)]
    [InlineData(false, 3f, 3)]
    [InlineData(false, 8f, 1)]
    [InlineData(false, 8f, 3)]
    [InlineData(true, 3f, 1)]
    [InlineData(true, 8f, 3)]
    public void TheGlyphShadowLooksLikeTheRaster(bool core, float radius, int density)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (raster, rasterCompositor) = Render(Scene(core, radius), density, glyphs: false);
        var (glyph, glyphCompositor) = Render(Scene(core, radius), density, glyphs: true);
        using (raster)
        using (glyph)
        {
            Assert.True(rasterCompositor.ShadowRasterizations > 0);
            Assert.Equal(0, rasterCompositor.GlyphShadows);
            Assert.True(glyphCompositor.GlyphShadows > 0);
            Assert.Equal(0, glyphCompositor.ShadowRasterizations + glyphCompositor.LiveShadows);
            int largest = 0, shadowed = 0;
            for (var y = 0; y < raster.Height; y++)
                for (var x = 0; x < raster.Width; x++)
                {
                    var (a, b) = (raster.GetPixel(x, y), glyph.GetPixel(x, y));
                    largest = Math.Max(largest, Math.Max(Math.Abs(a.Red - b.Red), Math.Max(Math.Abs(a.Green - b.Green), Math.Abs(a.Blue - b.Blue))));
                    if (a.Red < 250) shadowed++;
                }
            output.WriteLine($"radius {radius} at {density}×: largest difference {largest}, {shadowed} shadowed pixels");
            Assert.True(shadowed > 100 * density * density, $"{shadowed} shadowed pixels");
            Assert.True(largest <= (density == 1 ? LowDensityTolerance : Tolerance), $"largest difference {largest}");
        }
    }

    private static SkUiRenderShadow? Committed(ISkUiRenderable node, SkUiView root)
    {
        using var surface = new SkUiTestSurface(root, Width, Height);
        surface.Frame(0);
        return node.RenderState.Committed.Shadow;
    }

    [Fact]
    public void OnlyPlainTextCastsFromItsGlyphs()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        SkUiLabel Shadowed(SkUiLabel label)
        {
            label.Shadow = TitleShadow(3);
            return label;
        }
        var plain = Shadowed(Label());
        Assert.NotNull(Committed(plain, new SkUiContentView { Content = plain })?.Glyphs);

        var background = Shadowed(Label());
        background.Background = new SolidColorBrush(Colors.Red.WithAlpha(0.5f));
        var chrome = Shadowed(Label());
        chrome.BorderColor = Colors.Blue;
        chrome.BorderWidth = 1;
        var rich = Shadowed(Label());
        rich.FormattedText = new FormattedString { Spans = { new Span { Text = "Order " }, new Span { Text = "0042", TextColor = Colors.Red } } };
        var clipped = Shadowed(Label());
        clipped.Clip = new Microsoft.Maui.Controls.Shapes.EllipseGeometry(new Point(40, 10), 40, 10);
        var empty = Shadowed(Label(""));
        empty.WidthRequest = 40;
        empty.HeightRequest = 20;
        var button = new SkUiButton { Text = "Order", FontFamily = SkUiTestHelpers.BundledFontFamily, Background = Colors.Transparent, Shadow = TitleShadow(3) };
        var underlined = Shadowed(Label());
        underlined.TextDecorations = TextDecorations.Underline; // the line's mask over the glyphs' would darken where they cross
        var shrunk = Shadowed(Label("A much longer order title"));
        shrunk.ShrinkToFit = true;
        shrunk.WidthRequest = 80; // drawn at a fit scale, which would scale the blur
        var grown = Shadowed(Label("Order"));
        grown.GrowToFill = true;
        grown.WidthRequest = 120;
        grown.HeightRequest = 30; // its lines fit, drawn at a fit scale above 1, which would scale the blur
        var tight = Shadowed(Label());
        tight.LineHeight = 0.7; // line boxes inside the frame, glyphs past it
        var overflowing = Shadowed(Label("A much longer order title"));
        overflowing.LineBreakMode = LineBreakMode.NoWrap;
        overflowing.WidthRequest = 60; // glyphs past the frame: the raster casts from what ClipToBounds keeps
        foreach (var view in new SkUiView[] { background, chrome, rich, clipped, empty, button, underlined, shrunk, grown, tight, overflowing })
        {
            var shadow = Committed(view, new SkUiContentView { Content = view });
            Assert.NotNull(shadow);
            Assert.Null(shadow.Glyphs);
        }

        var core = new SkUiCoreLabel().SetText("Order").SetFontFamily(SkUiTestHelpers.BundledFontFamily);
        core.SetShadow(new SkUiCoreShadow(Colors.Black, new Point(1, 2), 3, 0.35f));
        Assert.NotNull(Committed(core, new SkUiCoreHost().SetContent(core))?.Glyphs);
        var coreChip = new SkUiCoreLabel().SetText("Order").SetFontFamily(SkUiTestHelpers.BundledFontFamily).SetFillColor(Colors.Yellow.WithAlpha(0.5f));
        coreChip.SetShadow(new SkUiCoreShadow(Colors.Black, new Point(1, 2), 3, 0.35f));
        Assert.Null(Committed(coreChip, new SkUiCoreHost().SetContent(coreChip))?.Glyphs);
        var coreRounded = new SkUiCoreLabel().SetText("Order").SetFontFamily(SkUiTestHelpers.BundledFontFamily).SetCornerRadii(new CornerRadius(8));
        coreRounded.SetShadow(new SkUiCoreShadow(Colors.Black, new Point(1, 2), 3, 0.35f)); // the radii clip the glyphs
        Assert.Null(Committed(coreRounded, new SkUiCoreHost().SetContent(coreRounded))?.Glyphs);
    }

    [Fact]
    public void NewTextReplacesTheGlyphsAndTheOldOnesAreReleased()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var label = Label("Order 0042");
        label.Shadow = TitleShadow(3);
        label.WidthRequest = 120; // the same size for both texts: only the content changes
        var root = new SkUiContentView { Content = label, Background = Colors.White };
        using var surface = new SkUiTestSurface(root, Width, Height);
        surface.Renderer.Compositor.GlyphShadowsOnRaster = true;
        surface.Frame(0);
        var first = ((ISkUiRenderable)label).RenderState.Committed.Shadow!.Glyphs!;
        using var before = surface.Frame(16).Copy();

        label.Text = "Order 1111";
        surface.Frame(32);
        var second = ((ISkUiRenderable)label).RenderState.Committed.Shadow!.Glyphs!;
        Assert.NotSame(first, second);
        Assert.Equal(IntPtr.Zero, first.Handle); // the compositor released the replaced picture
        var differs = false;
        using var after = surface.Frame(48).Copy();
        for (var x = 0; x < Width && !differs; x++)
            for (var y = 40; y < Height && !differs; y++) // the line's bottom edge and below it, where the shadow reaches
                differs = before.GetPixel(x, y) != after.GetPixel(x, y);
        Assert.True(differs, "the shadow follows the new text");

        root.Content = null;
        surface.Frame(64);
        Assert.Equal(IntPtr.Zero, second.Handle); // and the last one once the label left the surface
    }
}
