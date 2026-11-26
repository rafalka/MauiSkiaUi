using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// 1.18 A1 (issue #132): OpenType features on drawn text (<c>FontFeatures</c>), passed to HarfBuzz, part of the text style
/// (so of every cache key), on labels, spans and text inputs of both layers; typed (<see cref="SkUiFontFeature"/>,
/// <see cref="SkUiFontFeatures"/>) with a text form for XAML. Neither bundled font has <c>tnum</c>; Lobster's ligatures
/// (<c>-liga</c>) and kerning (<c>-kern</c>) change its widths, Roboto Mono's small capitals (<c>smcp</c>) its glyphs.
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
        new(typeface, 20, LineBreakMode.NoWrap, Rendering: rendering, Features: features is null ? null : SkUiFontFeatures.Parse(features));

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
    public void FeaturesAreNamedValidatedAndWrittenAsCssReadsThem()
    {
        Assert.Equal(("tnum", 1, true), (SkUiFontFeature.TabularFigures.Tag, SkUiFontFeature.TabularFigures.Value, SkUiFontFeature.TabularFigures.IsEnabled));
        Assert.Equal(new SkUiFontFeature("kern", 0), SkUiFontFeature.Kerning.Disabled);
        Assert.Equal(SkUiFontFeature.Disable("liga"), SkUiFontFeature.StandardLigatures.Disabled);
        Assert.Equal(("ss03", "cv12"), (SkUiFontFeature.StylisticSet(3).Tag, SkUiFontFeature.CharacterVariant(12).Tag));
        Assert.Equal(["tnum", "-kern", "salt=2"], new[] { SkUiFontFeature.TabularFigures, SkUiFontFeature.Kerning.Disabled, SkUiFontFeature.StylisticAlternates.WithValue(2) }.Select(f => f.ToString()));
        // CSS's forms, as HarfBuzz reads them; ranges and anything else are not features.
        Assert.Equal(SkUiFontFeature.Kerning.Disabled, SkUiFontFeature.Parse("kern=0"));
        Assert.Equal(SkUiFontFeature.Kerning.Disabled, SkUiFontFeature.Parse(" kern off "));
        Assert.Equal(SkUiFontFeature.StandardLigatures, SkUiFontFeature.Parse("+liga"));
        Assert.Equal(new SkUiFontFeature("salt", 3), SkUiFontFeature.Parse("salt=3"));
        Assert.False(SkUiFontFeature.TryParse("liga[3:5]", out _));
        Assert.False(SkUiFontFeature.TryParse("not a feature!", out _));
        Assert.False(SkUiFontFeature.TryParse(null, out _));
        Assert.Throws<FormatException>(() => SkUiFontFeature.Parse(""));
        Assert.Throws<ArgumentException>(() => new SkUiFontFeature("tabular"));
        Assert.Throws<ArgumentException>(() => new SkUiFontFeature(" tnu"));
        Assert.Throws<ArgumentException>(() => new SkUiFontFeature("t nu"));
        Assert.Throws<ArgumentException>(() => new SkUiFontFeature("-abc")); // would not read back
        Assert.Throws<ArgumentException>(() => new SkUiFontFeature("a,bc"));
        Assert.False(SkUiFontFeature.TryParse("tnu", out _)); // HarfBuzz would pad it to "tnu "
        Assert.False(SkUiFontFeatures.TryParse("tnum, ab", out _));
        Assert.Throws<ArgumentException>(() => default(SkUiFontFeature).Disabled);
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkUiFontFeature("salt", -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SkUiFontFeature.StylisticSet(21));
        Assert.Throws<ArgumentOutOfRangeException>(() => SkUiFontFeature.CharacterVariant(0));
        Assert.Equal("", default(SkUiFontFeature).Tag);
    }

    [Fact]
    public void ListsAreBuiltByCollectionExpressionsOrParsedAndCompareByValue()
    {
        SkUiFontFeatures built = [SkUiFontFeature.TabularFigures, SkUiFontFeature.StandardLigatures.Disabled, SkUiFontFeature.StylisticSet(1)];
        var parsed = SkUiFontFeatures.Parse("tnum,  -liga ,ss01=1");
        Assert.Equal(built, parsed);
        Assert.Equal(built.GetHashCode(), parsed.GetHashCode());
        Assert.Equal("tnum, -liga, ss01", built.ToString());
        Assert.Equal(built, SkUiFontFeatures.Parse(built.ToString()));
        Assert.Equal(3, built.Count);
        Assert.Equal(SkUiFontFeature.StandardLigatures.Disabled, built[1]);
        Assert.Same(parsed, SkUiFontFeatures.Parse("tnum,  -liga ,ss01=1")); // parsed once per text
        Assert.NotEqual(built, SkUiFontFeatures.Create(SkUiFontFeature.TabularFigures));
        Assert.NotEqual(built, [SkUiFontFeature.StandardLigatures.Disabled, SkUiFontFeature.TabularFigures, SkUiFontFeature.StylisticSet(1)]); // order counts
        SkUiFontFeatures none = [];
        Assert.Same(SkUiFontFeatures.Empty, none);
        Assert.Same(SkUiFontFeatures.Empty, SkUiFontFeatures.Parse(" , "));
        Assert.Equal("", none.ToString());
        Assert.True(SkUiFontFeatures.TryParse("kern=0, +liga", out var list));
        Assert.Equal([SkUiFontFeature.Kerning.Disabled, SkUiFontFeature.StandardLigatures], list);
        Assert.False(SkUiFontFeatures.TryParse("tnum, not a feature!", out _));
        Assert.False(SkUiFontFeatures.TryParse(null, out _));
        Assert.Throws<FormatException>(() => SkUiFontFeatures.Parse("tnum, liga[0:2]"));
        Assert.Throws<ArgumentException>(() => SkUiFontFeatures.Create(SkUiFontFeature.TabularFigures, default));
    }

    [Fact]
    public void SupportedByListsTheFeaturesOfAFont()
    {
        using var lobster = UseLobster();
        using var mono = SkUiTestHelpers.UseBundledFont();
        var lobsterTags = SkUiFontFeatures.SupportedBy(LobsterFamily);
        Assert.Contains("liga", lobsterTags);
        Assert.Contains("kern", lobsterTags);
        Assert.DoesNotContain("tnum", lobsterTags);
        Assert.Equal(lobsterTags.Order(StringComparer.Ordinal), lobsterTags);
        Assert.Equal(lobsterTags.Distinct(), lobsterTags);
        Assert.Contains("smcp", SkUiFontFeatures.SupportedBy(SkUiTestHelpers.BundledFontFamily));
        Assert.Contains(SkUiFontFeature.Kerning.Tag, lobsterTags);
        Assert.DoesNotContain("smcp", lobsterTags);
    }

    [Fact]
    public void KnownFeaturesHaveANameADescriptionAndADefault()
    {
        var tabular = SkUiFontFeature.Describe("tnum");
        Assert.Equal(("tnum", "Tabular figures", SkUiFontFeatureDefault.Off), (tabular?.Tag, tabular?.Name, tabular?.Default));
        Assert.False(string.IsNullOrWhiteSpace(tabular?.Description));
        Assert.Equal(SkUiFontFeatureDefault.On, SkUiFontFeature.Kerning.Info?.Default);
        Assert.Equal(SkUiFontFeatureDefault.On, SkUiFontFeature.StandardLigatures.Disabled.Info?.Default); // the tag's, whatever the value
        Assert.Equal(SkUiFontFeatureDefault.Required, SkUiFontFeature.Describe("ccmp")?.Default);
        Assert.Equal("Stylistic set 7", SkUiFontFeature.StylisticSet(7).Info?.Name);
        Assert.Equal("Character variant 42", SkUiFontFeature.CharacterVariant(42).Info?.Name);
        Assert.Null(SkUiFontFeature.Describe("cv00"));
        Assert.Null(SkUiFontFeature.Describe("cv+1"));
        Assert.Null(SkUiFontFeature.Describe("abcd"));
        Assert.Null(default(SkUiFontFeature).Info);
        Assert.Throws<ArgumentNullException>(() => SkUiFontFeature.Describe(null!));
        // Every listed tag once, valid, described; every named member and every tag of the bundled fonts is known.
        var known = SkUiFontFeature.Known;
        Assert.Equal(known.Count, known.Select(info => info.Tag).Distinct().Count());
        Assert.All(known, info =>
        {
            Assert.Equal(info.Tag, new SkUiFontFeature(info.Tag).Tag);
            Assert.False(string.IsNullOrWhiteSpace(info.Name));
            Assert.EndsWith(".", info.Description);
        });
        Assert.Contains(known, info => info.Tag == "ss20");
        SkUiFontFeature[] named = [SkUiFontFeature.Kerning, SkUiFontFeature.StandardLigatures, SkUiFontFeature.ContextualLigatures,
            SkUiFontFeature.DiscretionaryLigatures, SkUiFontFeature.ContextualAlternates, SkUiFontFeature.StylisticAlternates, SkUiFontFeature.Swash,
            SkUiFontFeature.TabularFigures, SkUiFontFeature.ProportionalFigures, SkUiFontFeature.LiningFigures, SkUiFontFeature.OldstyleFigures,
            SkUiFontFeature.SlashedZero, SkUiFontFeature.Fractions, SkUiFontFeature.Ordinals, SkUiFontFeature.Superscript, SkUiFontFeature.Subscript,
            SkUiFontFeature.SmallCaps, SkUiFontFeature.CapitalsToSmallCaps, SkUiFontFeature.CaseSensitiveForms];
        Assert.All(named, feature => Assert.NotNull(feature.Info));
        using var lobster = UseLobster();
        using var mono = SkUiTestHelpers.UseBundledFont();
        Assert.All(SkUiFontFeatures.SupportedBy(LobsterFamily).Concat(SkUiFontFeatures.SupportedBy(SkUiTestHelpers.BundledFontFamily)),
            tag => Assert.NotNull(SkUiFontFeature.Describe(tag)));
    }

    [Fact]
    public void TheConverterReadsTheTextFormAndBlankIsNone()
    {
        var converter = new SkUiFontFeaturesConverter();
        Assert.True(converter.CanConvertFrom(typeof(string)));
        Assert.Equal([SkUiFontFeature.TabularFigures, SkUiFontFeature.Kerning.Disabled], (SkUiFontFeatures)converter.ConvertFromInvariantString("tnum, -kern")!);
        Assert.Same(SkUiFontFeatures.Empty, converter.ConvertFromInvariantString("  ")); // none: on a span, the font's defaults
        Assert.Equal("tnum", converter.ConvertToInvariantString(SkUiFontFeatures.Create(SkUiFontFeature.TabularFigures)));
        Assert.Throws<FormatException>(() => converter.ConvertFromInvariantString("not a feature!"));
        Assert.IsType<SkUiFontFeaturesConverter>(System.ComponentModel.TypeDescriptor.GetConverter(typeof(SkUiFontFeatures)));
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
        SKBitmap Draw(SkUiFontFeatures? features) =>
            Render(new SkUiCoreLabel().SetText("Text").SetFontFamily(SkUiTestHelpers.BundledFontFamily).SetFontSize(20).SetFontFeatures(features), 160, 40);
        using var plain = Draw(null);
        using var smallCaps = Draw([SkUiFontFeature.SmallCaps]);
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
        label.FontFeatures = [SkUiFontFeature.StandardLigatures.Disabled];
        var noLigatures = label.Measure(double.PositiveInfinity, double.PositiveInfinity).Width;
        Assert.True(noLigatures > plain + 1, $"-liga {noLigatures}, default {plain}");

        var core = new SkUiCoreLabel().SetText(Ligatures).SetFontFamily(LobsterFamily).SetFontSize(20).SetLineBreakMode(LineBreakMode.NoWrap);
        Assert.Equal(plain, core.Measure(double.PositiveInfinity, double.PositiveInfinity).Width, 0.01);
        core.SetFontFeatures([SkUiFontFeature.StandardLigatures.Disabled]);
        Assert.Equal(noLigatures, core.Measure(double.PositiveInfinity, double.PositiveInfinity).Width, 0.01);
        Assert.Equal([SkUiFontFeature.StandardLigatures.Disabled], core.FontFeatures);

        // No features is the font's defaults, on both layers.
        label.FontFeatures = [];
        Assert.Equal(plain, label.Measure(double.PositiveInfinity, double.PositiveInfinity).Width, 0.01);
        core.SetFontFeatures(SkUiFontFeatures.Empty);
        Assert.Equal(plain, core.Measure(double.PositiveInfinity, double.PositiveInfinity).Width, 0.01);
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
            FontFamily = LobsterFamily, FontSize = 20, LineBreakMode = LineBreakMode.NoWrap, FontFeatures = [SkUiFontFeature.StandardLigatures.Disabled],
            FormattedText = new FormattedString { Spans = { span } }
        };
        double Measure() => label.Measure(double.PositiveInfinity, double.PositiveInfinity).Width;
        var inherited = Measure();
        Assert.True(inherited > plainWidth + 1);
        SkUiSpan.SetFontFeatures(span, [SkUiFontFeature.StandardLigatures]);
        Assert.Equal(plainWidth, Measure(), 0.01);
        SkUiSpan.SetFontFeatures(span, []); // the font's defaults, not the label's
        Assert.Equal(plainWidth, Measure(), 0.01);
        SkUiSpan.SetFontFeatures(span, null);
        Assert.Equal(inherited, Measure(), 0.01);

        var coreSpan = new SkUiCoreSpan(Ligatures);
        var core = new SkUiCoreLabel().SetFontFamily(LobsterFamily).SetFontSize(20).SetLineBreakMode(LineBreakMode.NoWrap)
            .SetFontFeatures([SkUiFontFeature.StandardLigatures.Disabled]).SetSpans([coreSpan]);
        Assert.Equal(inherited, core.Measure(double.PositiveInfinity, double.PositiveInfinity).Width, 0.01);
        coreSpan.SetFontFeatures([SkUiFontFeature.StandardLigatures]);
        Assert.Equal(plainWidth, core.Measure(double.PositiveInfinity, double.PositiveInfinity).Width, 0.01);
    }

    [Fact]
    public void BindingsTakeFeaturesNotText()
    {
        // A view model holds the list: MAUI's bindings do not run type converters, so a text source is not taken.
        var label = new SkUiLabel { BindingContext = new { Features = SkUiFontFeatures.Parse("tnum, -liga"), Text = "tnum" } };
        label.SetBinding(SkUiLabel.FontFeaturesProperty, "Text");
        Assert.Null(label.FontFeatures);
        label.SetBinding(SkUiLabel.FontFeaturesProperty, "Features");
        Assert.Equal([SkUiFontFeature.TabularFigures, SkUiFontFeature.StandardLigatures.Disabled], label.FontFeatures);
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
        entry.FontFeatures = SkUiFontFeatures.Parse("-liga");
        var noLigatures = EntryWidth();
        Assert.True(noLigatures > plain + 1, $"-liga {noLigatures}, default {plain}");

        var core = new SkUiCoreEntry().SetText(Ligatures).SetFontFamily(LobsterFamily).SetFontSize(20).SetFontFeatures([SkUiFontFeature.StandardLigatures.Disabled]);
        using (Render(core, 400, 60))
            Assert.Equal(noLigatures, core.Model.Layout.Width, 0.01);
        Assert.Equal("-liga", core.FontFeatures?.ToString());
    }
}

/// <summary>1.18 A1: <c>FontFeatures</c> in XAML and styles, as CSS's text, on controls and spans.</summary>
[Collection(RuntimeXamlCollection.Name)]
public sealed class FontFeatureXamlTests
{
    [Fact]
    public void XamlAndStylesTakeTheTextForm()
    {
        const string xaml = """
            <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:skui="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <ContentView.Resources>
                <Style x:Key="Clock" TargetType="skui:SkUiLabel" xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">
                  <Setter Property="FontFeatures" Value="tnum, zero" />
                </Style>
              </ContentView.Resources>
              <VerticalStackLayout>
                <skui:SkUiLabel FontFeatures="tnum, -liga" />
                <skui:SkUiLabel Style="{StaticResource Clock}" />
                <skui:SkUiEntry FontFeatures="ss01" />
                <skui:SkUiLabel>
                  <skui:SkUiLabel.FormattedText>
                    <FormattedString>
                      <Span Text="Caps" skui:SkUiSpan.FontFeatures="smcp, c2sc" />
                      <Span Text="Plain" skui:SkUiSpan.FontFeatures="" />
                    </FormattedString>
                  </skui:SkUiLabel.FormattedText>
                </skui:SkUiLabel>
              </VerticalStackLayout>
            </ContentView>
            """;
        var root = new ContentView();
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var children = ((VerticalStackLayout)root.Content).Children;
        Assert.Equal([SkUiFontFeature.TabularFigures, SkUiFontFeature.StandardLigatures.Disabled], ((SkUiLabel)children[0]).FontFeatures);
        Assert.Equal([SkUiFontFeature.TabularFigures, SkUiFontFeature.SlashedZero], ((SkUiLabel)children[1]).FontFeatures);
        Assert.Equal([SkUiFontFeature.StylisticSet(1)], ((SkUiEntry)children[2]).FontFeatures);
        var span = ((SkUiLabel)children[3]).FormattedText!.Spans[0];
        Assert.Equal([SkUiFontFeature.SmallCaps, SkUiFontFeature.CapitalsToSmallCaps], SkUiSpan.GetFontFeatures(span));
        Assert.Same(SkUiFontFeatures.Empty, SkUiSpan.GetFontFeatures(((SkUiLabel)children[3]).FormattedText!.Spans[1]));
    }
}
