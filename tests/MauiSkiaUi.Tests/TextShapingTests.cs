using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// HarfBuzz shaping, bidi and font fallback. Tests that need a system font for a script (Arabic, Hebrew,
/// Devanagari, emoji) return early when the machine has none (e.g. bare Linux CI agents).
/// </summary>
public class TextShapingTests
{
    private const string Hebrew = "שלום עולם";
    private const string Arabic = "مرحبا بالعالم";

    private static SKTypeface Primary() =>
        SKTypeface.FromFile(Path.Combine(AppContext.BaseDirectory, "Assets", "RobotoMono-Regular.ttf"));

    private static bool HasFontFor(int codePoint) =>
        SKFontManager.Default.MatchCharacter(codePoint) is { } face && face.GetGlyph(codePoint) != 0;

    private static SkUiShaping.Paragraph Shape(string text, SkUiTextDirection direction = SkUiTextDirection.Auto)
    {
        var fonts = new Dictionary<SKTypeface, SKFont>();
        return SkUiShaping.Shape(text, Primary(), 16, direction, face => fonts.TryGetValue(face, out var f) ? f : fonts[face] = new SKFont(face, 16));
    }

    [Fact]
    public void BidiResolvesLevelsForMixedText()
    {
        const string text = "abc אבג def";
        var levels = SkUiBidi.ResolveLevels(text, SkUiBidi.BaseLevel(text, SkUiTextDirection.Auto));
        Assert.Equal(0, SkUiBidi.BaseLevel(text, SkUiTextDirection.Auto));
        Assert.Equal(new byte[] { 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0 }, levels);
    }

    [Fact]
    public void BidiFirstStrongRtlAndNumbersInsideRtl()
    {
        const string text = "סכום 123 ש\"ח";
        var baseLevel = SkUiBidi.BaseLevel(text, SkUiTextDirection.Auto);
        Assert.Equal(1, baseLevel);
        var levels = SkUiBidi.ResolveLevels(text, baseLevel);
        // European digits inside an RTL paragraph resolve to level 2 (left-to-right number in RTL text).
        Assert.Equal(2, levels[text.IndexOf('1')]);
        Assert.Equal(1, levels[0]);
    }

    [Fact]
    public void BidiArabicIndicDigitsAreArabicNumbers()
    {
        const string text = "عدد ١٢٣";
        var levels = SkUiBidi.ResolveLevels(text, 1);
        Assert.Equal(2, levels[text.IndexOf('١')]);
    }

    [Fact]
    public void VisualOrderReversesRtlRunsAndKeepsNestedLtr()
    {
        // Logical: [L=0][R=1][EN=2][R=1][L=0] → display: 0, 3, 2, 1, 4
        Assert.Equal(new[] { 0, 3, 2, 1, 4 }, SkUiBidi.VisualOrder(new byte[] { 0, 1, 2, 1, 0 }));
        Assert.Equal(new[] { 2, 1, 0 }, SkUiBidi.VisualOrder(new byte[] { 1, 1, 1 }));
        Assert.Equal(new[] { 0, 1, 2 }, SkUiBidi.VisualOrder(new byte[] { 0, 0, 0 }));
    }

    [Fact]
    public void ArabicShapesWithJoiningAndLamAlefLigature()
    {
        if (!HasFontFor('ل')) return;
        var ligature = Shape("لا");
        var glyphs = ligature.Runs.Sum(run => run.Glyphs.Length);
        // Lam + Alef form one ligature glyph in Arabic fonts.
        Assert.Equal(1, glyphs);
        var word = Shape(Arabic);
        Assert.All(word.Runs, run => Assert.Equal(1, run.Level % 2));
        Assert.NotSame(Primary(), word.Runs[0].Typeface);
    }

    [Fact]
    public void DevanagariReordersVowelSign()
    {
        if (!HasFontFor('क')) return;
        // कि: the i-matra (U+093F) is drawn before the consonant; HarfBuzz reorders it (clusters not monotonic in visual order).
        var paragraph = Shape("कि");
        var run = Assert.Single(paragraph.Runs);
        Assert.True(run.Glyphs.Length >= 2);
        // The reordered pre-base matra shares the syllable's cluster, so all advance lands on the first code unit.
        Assert.All(run.Clusters, cluster => Assert.Equal(0u, cluster));
        Assert.Equal(paragraph.Width, paragraph.Advances.Sum(), 2);
    }

    [Fact]
    public void EmojiFallsBackToAnotherTypefaceWithoutSplittingModifiers()
    {
        if (!HasFontFor(0x1F44D)) return;
        var paragraph = Shape("ok 👍🏽 ok");
        Assert.Contains(paragraph.Runs, run => !ReferenceEquals(run.Typeface, paragraph.Runs[0].Typeface));
        var emojiRun = paragraph.Runs.First(run => paragraph.Text.Substring(run.Start, run.Length).Contains("👍"));
        Assert.Contains("🏽", paragraph.Text.Substring(emojiRun.Start, emojiRun.Length), StringComparison.Ordinal);
    }

    [Fact]
    public void AdvancesSumToShapedWidth()
    {
        var paragraph = Shape("Hello, shaped world!");
        Assert.Equal(paragraph.Width, paragraph.Advances.Sum(), 2);
        using var font = new SKFont(Primary(), 16);
        // HarfBuzz uses unhinted advances; Skia's MeasureText may differ by a fraction of a pixel.
        Assert.InRange(paragraph.Width - font.MeasureText(paragraph.Text), -0.5, 0.5);
    }

    [Fact]
    public void RtlLabelStartAlignsRightAndLtrLeft()
    {
        if (!HasFontFor('ש')) return;
        Assert.True(InkCenterX(Hebrew) > 100, "RTL Start alignment should hug the right edge");
        Assert.True(InkCenterX("Hello") < 100, "LTR Start alignment should hug the left edge");
    }

    [Fact]
    public void ExplicitRightToLeftFlowDirectionRightAlignsLatinText()
    {
        Assert.True(InkCenterX("Hello", FlowDirection.RightToLeft) > 100);
    }

    [Fact]
    public void RtlTextWrapsAndEveryLineFits()
    {
        if (!HasFontFor('م')) return;
        var label = new SkUiLabel { Text = string.Join(' ', Enumerable.Repeat(Arabic, 6)) };
        var size = ((IView)label).Measure(120, double.PositiveInfinity);
        Assert.True(size.Width <= 120.5);
        Assert.True(size.Height > label.FontSize * 2);
    }

    [Fact]
    public void RtlTailTruncationRemovesLogicalEnd()
    {
        if (!HasFontFor('ש')) return;
        var layout = new SkUiTextLayout();
        var full = layout.Measure(Hebrew, Primary(), 16, default, double.PositiveInfinity, SkUiCoreTextLineBreakers.NoWrap);
        var truncated = new SkUiTextLayout().Measure(Hebrew, Primary(), 16, default, full.Width / 2, SkUiCoreTextLineBreakers.TailTruncation);
        Assert.True(truncated.Width <= full.Width / 2 + 0.5);
        Assert.True(truncated.Width > 0);
    }

    [Fact]
    public void CjkWrapsBetweenIdeographsWithoutSpaces()
    {
        if (!HasFontFor('漢')) return;
        var layout = new SkUiTextLayout();
        var size = layout.Measure("漢字漢字漢字漢字漢字漢字漢字漢字", Primary(), 16, default, 60, SkUiCoreTextLineBreakers.WordWrap);
        Assert.True(size.Width <= 60.5, $"width {size.Width}");
        Assert.True(size.Height > 16 * 2);
    }

    [Fact]
    public void CustomLineBreakerLinesAreShaped()
    {
        var layout = new SkUiTextLayout();
        SkUiCoreTextLineBreaker halves = (text, _, _) => [text[..(text.Length / 2)], text[(text.Length / 2)..]];
        using var font = new SKFont(Primary(), 16);
        var size = layout.Measure("abcdef", Primary(), 16, default, 500, halves);
        Assert.Equal(font.Spacing * 2, (float)size.Height, 1);
        Assert.Equal(font.MeasureText("abc"), (float)size.Width, 1);
    }

    private static double InkCenterX(string text, FlowDirection flow = FlowDirection.MatchParent)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var label = new SkUiLabel { Text = text, FlowDirection = flow, TextColor = Colors.Black, FontFamily = SkUiTestHelpers.BundledFontFamily };
        SkUiTestHelpers.Arrange(label, 200, 40);
        using var bitmap = new SKBitmap(200, 40);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            label.Paint(canvas);
        }
        double sum = 0, count = 0;
        for (var y = 0; y < 40; y++)
            for (var x = 0; x < 200; x++)
                if (bitmap.GetPixel(x, y).Red < 128) { sum += x; count++; }
        Assert.True(count > 0, "text drew no ink");
        return sum / count;
    }
}
