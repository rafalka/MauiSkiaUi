using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// 1.18 A1 (issue #132): OpenType features on drawn text (<c>FontFeatures</c>), passed to HarfBuzz, part of the text style
/// (so of every cache key), on labels, spans and text inputs of both layers. Neither bundled font has <c>tnum</c>; Lobster's
/// ligatures (<c>-liga</c>) and kerning (<c>-kern</c>) change its widths, Roboto Mono's small capitals (<c>smcp</c>) its glyphs.
/// </summary>
public sealed class FontFeatureTests
{
    private const string Ligatures = "ffl fi ffl fi";
    private const string LobsterFamily = "Test-Lobster-Features";

    private static string LobsterPath => Path.Combine(AppContext.BaseDirectory, "Assets", "Lobster-Regular.ttf");

    private static SKTypeface Lobster() => SKTypeface.FromFile(LobsterPath);

    private static double Width(string text, SKTypeface typeface, string? features, SkUiTextRendering rendering = SkUiTextRendering.Auto) =>
        new SkUiTextLayout().Measure(text, Style(typeface, features, rendering), default, double.PositiveInfinity).Width;

    private static SkUiTextStyle Style(SKTypeface typeface, string? features, SkUiTextRendering rendering = SkUiTextRendering.Auto) =>
        new(typeface, 20, LineBreakMode.NoWrap, Rendering: rendering, Features: SkUiFontFeatures.Parse(features));

    private static IDisposable UseLobster()
    {
        SkUiFonts.Register(LobsterFamily, () => File.OpenRead(LobsterPath));
        return new Unregister();
    }

    private sealed class Unregister : IDisposable
    {
        public void Dispose() => SkUiFonts.Unregister(LobsterFamily);
    }

    [Fact]
    public void ListsParseOnceNormalizedAndCompareByValue()
    {
        var features = SkUiFontFeatures.Parse("tnum,  -liga ,ss01=1");
        Assert.NotNull(features);
        Assert.Equal("tnum, -liga, ss01=1", features.Text);
        Assert.Equal(3, features.Features.Length);
        Assert.Same(features, SkUiFontFeatures.Parse("tnum,  -liga ,ss01=1"));
        Assert.Equal(features, SkUiFontFeatures.Parse("tnum, -liga, ss01=1"));
        Assert.NotEqual(features, SkUiFontFeatures.Parse("tnum"));
        Assert.Null(SkUiFontFeatures.Parse(null));
        Assert.Null(SkUiFontFeatures.Parse("  "));
        Assert.Null(SkUiFontFeatures.Parse(" , "));
        Assert.True(SkUiFontFeatures.IsValid(null));
        Assert.True(SkUiFontFeatures.IsValid("kern=0, +liga"));
        Assert.False(SkUiFontFeatures.IsValid("tnum, not a feature!"));
        Assert.Null(SkUiFontFeatures.Parse("tnum, not a feature!"));
    }

    [Fact]
    public void FeaturesReachHarfBuzzOnThePlainAndShapedPaths()
    {
        using var typeface = Lobster();
        var plain = Width(Ligatures, typeface, null);
        var noLigatures = Width(Ligatures, typeface, "-liga");
        Assert.True(noLigatures > plain + 1, $"-liga {noLigatures}, default {plain}");
        Assert.Equal(noLigatures, Width(Ligatures, typeface, "-liga", SkUiTextRendering.Shaped), 0.01);
        // Kerning off moves the glyphs of a text Lobster kerns.
        const string kerned = "10:11 fi ffl 0123 Th";
        Assert.True(Math.Abs(Width(kerned, typeface, "-kern") - Width(kerned, typeface, null)) > 0.3);
        // Listed and then turned back on: a later setting of a tag wins.
        Assert.Equal(plain, Width(Ligatures, typeface, "-liga, +liga"), 0.01);
    }

    [Fact]
    public void TheSimpleRendererShapesTextWithFeatures()
    {
        using var typeface = Lobster();
        var simple = Width(Ligatures, typeface, null, SkUiTextRendering.Simple);
        using var font = new SKFont(typeface, 20) { LinearMetrics = true };
        Assert.Equal(font.MeasureText(Ligatures), simple, 0.01);
        Assert.Equal(Width(Ligatures, typeface, "-liga"), Width(Ligatures, typeface, "-liga", SkUiTextRendering.Simple), 0.01);
    }

    [Fact]
    public void FeaturesArePartOfTheLayoutsCacheKey()
    {
        using var typeface = Lobster();
        var layout = new SkUiTextLayout();
        var first = layout.Measure(Ligatures, Style(typeface, null), default, double.PositiveInfinity).Width;
        var second = layout.Measure(Ligatures, Style(typeface, "-liga"), default, double.PositiveInfinity).Width;
        var third = layout.Measure(Ligatures, Style(typeface, null), default, double.PositiveInfinity).Width;
        Assert.True(second > first + 1);
        Assert.Equal(first, third, 0.01);
        Assert.NotEqual(Style(typeface, null), Style(typeface, "-liga"));
        Assert.Equal(Style(typeface, "-liga"), Style(typeface, " -liga"));
    }

    private static SKBitmap Render(SkUiCoreNode node, int width, int height)
    {
        node.SetWidth(width).SetHeight(height);
        node.Measure(width, height);
        node.Arrange(new Rect(0, 0, width, height));
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        node.Paint(canvas);
        return bitmap;
    }

    private static SKBitmap Render(SkUiView view, int width, int height)
    {
        view.WidthRequest = width;
        view.HeightRequest = height;
        SkUiTestHelpers.Arrange(view, width, height);
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        view.Paint(canvas);
        return bitmap;
    }

    [Fact]
    public void FeaturesChangeTheGlyphsDrawn()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        SKBitmap Draw(string? features) =>
            Render(new SkUiCoreLabel().SetText("Text").SetFontFamily(SkUiTestHelpers.BundledFontFamily).SetFontSize(20).SetFontFeatures(features), 160, 40);
        using var plain = Draw(null);
        using var smallCaps = Draw("smcp");
        Assert.False(plain.GetPixelSpan().SequenceEqual(smallCaps.GetPixelSpan()));
        using var again = Draw(null);
        Assert.True(plain.GetPixelSpan().SequenceEqual(again.GetPixelSpan()));
    }

    [Fact]
    public void LabelsOfBothLayersMeasureWithTheirFeatures()
    {
        using var _ = UseLobster();
        var label = new SkUiLabel { Text = Ligatures, FontFamily = LobsterFamily, FontSize = 20, LineBreakMode = LineBreakMode.NoWrap };
        var plain = label.Measure(double.PositiveInfinity, double.PositiveInfinity).Width;
        label.FontFeatures = "-liga";
        var noLigatures = label.Measure(double.PositiveInfinity, double.PositiveInfinity).Width;
        Assert.True(noLigatures > plain + 1, $"-liga {noLigatures}, default {plain}");

        var core = new SkUiCoreLabel().SetText(Ligatures).SetFontFamily(LobsterFamily).SetFontSize(20).SetLineBreakMode(LineBreakMode.NoWrap);
        Assert.Equal(plain, core.Measure(double.PositiveInfinity, double.PositiveInfinity).Width, 0.01);
        core.SetFontFeatures("-liga");
        Assert.Equal(noLigatures, core.Measure(double.PositiveInfinity, double.PositiveInfinity).Width, 0.01);
        Assert.Equal("-liga", core.FontFeatures);
    }

    [Fact]
    public void SpansTakeTheLabelsFeaturesUnlessTheySetTheirOwn()
    {
        using var _ = UseLobster();
        var plainWidth = new SkUiLabel { Text = Ligatures, FontFamily = LobsterFamily, FontSize = 20, LineBreakMode = LineBreakMode.NoWrap }
            .Measure(double.PositiveInfinity, double.PositiveInfinity).Width;
        var span = new Span { Text = Ligatures };
        var label = new SkUiLabel
        {
            FontFamily = LobsterFamily, FontSize = 20, LineBreakMode = LineBreakMode.NoWrap, FontFeatures = "-liga",
            FormattedText = new FormattedString { Spans = { span } }
        };
        double Measure() => label.Measure(double.PositiveInfinity, double.PositiveInfinity).Width;
        var inherited = Measure();
        Assert.True(inherited > plainWidth + 1);
        SkUiSpan.SetFontFeatures(span, "+liga");
        Assert.Equal(plainWidth, Measure(), 0.01);

        var coreSpan = new SkUiCoreSpan(Ligatures);
        var core = new SkUiCoreLabel().SetFontFamily(LobsterFamily).SetFontSize(20).SetLineBreakMode(LineBreakMode.NoWrap)
            .SetFontFeatures("-liga").SetSpans([coreSpan]);
        Assert.Equal(inherited, core.Measure(double.PositiveInfinity, double.PositiveInfinity).Width, 0.01);
        coreSpan.SetFontFeatures("+liga");
        Assert.Equal(plainWidth, core.Measure(double.PositiveInfinity, double.PositiveInfinity).Width, 0.01);
    }

    [Fact]
    public void InvalidListsAreIgnoredByPropertiesAndRejectedBySetters()
    {
        var label = new SkUiLabel { FontFeatures = "tnum" };
        label.FontFeatures = "tnum, not a feature!";
        Assert.Equal("tnum", label.FontFeatures);
        Assert.Throws<ArgumentException>(() => label.SetFontFeatures("not a feature!"));
        Assert.Throws<ArgumentException>(() => new SkUiCoreLabel().SetFontFeatures("not a feature!"));
        Assert.Throws<ArgumentException>(() => new SkUiCoreSpan("x").SetFontFeatures("not a feature!"));
        Assert.Throws<ArgumentException>(() => SkUiSpan.SetFontFeatures(new Span(), "not a feature!"));
        var entry = new SkUiEntry().SetFontFeatures("tnum");
        entry.FontFeatures = "not a feature!";
        Assert.Equal("tnum", entry.FontFeatures);
        Assert.Throws<ArgumentException>(() => new SkUiCoreEntry().SetFontFeatures("not a feature!"));
    }

    [Fact]
    public void TextInputsShapeTheirTextWithTheirFeatures()
    {
        using var _ = UseLobster();
        var entry = new SkUiEntry { Text = Ligatures, FontFamily = LobsterFamily, FontSize = 20 };
        float EntryWidth()
        {
            using var bitmap = Render(entry, 400, 60);
            return entry.Model.Layout.Width;
        }
        var plain = EntryWidth();
        entry.FontFeatures = "-liga";
        var noLigatures = EntryWidth();
        Assert.True(noLigatures > plain + 1, $"-liga {noLigatures}, default {plain}");

        var core = new SkUiCoreEntry().SetText(Ligatures).SetFontFamily(LobsterFamily).SetFontSize(20).SetFontFeatures("-liga");
        using (Render(core, 400, 60))
            Assert.Equal(noLigatures, core.Model.Layout.Width, 0.01);
        Assert.Equal("-liga", core.FontFeatures);
    }
}
