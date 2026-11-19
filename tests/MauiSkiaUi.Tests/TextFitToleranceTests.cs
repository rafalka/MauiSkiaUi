using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Text measured at its natural width and laid out at that width less a rounding error (float shaping vs double layout
/// arithmetic, font scaling: issue #110) still fits on its line: it is neither truncated nor wrapped.
/// </summary>
public class TextFitToleranceTests
{
    // Rounding noise of the size in issue #110: measured 83.6673355, laid out in 83.6673279.
    private const double Noise = 1e-5;

    private static readonly SKTypeface Mono =
        SKTypeface.FromFile(Path.Combine(AppContext.BaseDirectory, "Assets", "RobotoMono-Regular.ttf"));

    public static TheoryData<SkUiTextRendering> Renderings => [SkUiTextRendering.Simple, SkUiTextRendering.Shaped];

    private static SkUiTextStyle Style(LineBreakMode mode, SkUiTextRendering rendering, int maxLines = 0) =>
        new(Mono, 17.5, mode, MaxLines: maxLines, Rendering: rendering);

    [Theory]
    [MemberData(nameof(Renderings))]
    public void TailTruncatedTextAtItsNaturalWidthLessNoiseIsNotTruncated(SkUiTextRendering rendering)
    {
        const string text = "19-11-2026";
        var style = Style(LineBreakMode.TailTruncation, rendering, maxLines: 1);
        var natural = new SkUiTextLayout().Measure(text, style, default, double.PositiveInfinity);

        // A fresh layout breaks at the narrower width; the measured one reuses its natural lines.
        var fresh = new SkUiTextLayout().Measure(text, style, default, natural.Width - Noise);
        Assert.Equal(natural.Width, fresh.Width, 3);
        var reused = new SkUiTextLayout();
        reused.Measure(text, style, default, double.PositiveInfinity);
        Assert.Equal(natural.Width, reused.Measure(text, style, default, natural.Width - Noise).Width, 3);
    }

    [Theory]
    [MemberData(nameof(Renderings))]
    public void WrappedLinesAtTheirWidthLessNoiseAreNotBrokenAgain(SkUiTextRendering rendering)
    {
        var style = Style(LineBreakMode.WordWrap, rendering);
        var line = new SkUiTextLayout().Measure("aaa", style, default, double.PositiveInfinity);

        // "aaa bbb" at the width of "aaa": two lines, not "aa" / "a" / "bbb".
        var wrapped = new SkUiTextLayout().Measure("aaa bbb", style, default, line.Width - Noise);
        Assert.Equal(line.Height * 2, wrapped.Height, 3);
        Assert.Equal(line.Width, wrapped.Width, 3);
    }

    [Theory]
    [MemberData(nameof(Renderings))]
    public void FirstFitAtItsNaturalWidthLessNoiseKeepsTheWholeText(SkUiTextRendering rendering)
    {
        // A custom breaker's Fits decides as the stock breaking does: "1,234.56" is not dropped for "1,235".
        IReadOnlyList<string> drawn = [];
        var forms = SkUiTextLineBreakers.FirstFit(_ => ["1,234.56", "1,235"]);
        var style = Style(LineBreakMode.TailTruncation, rendering, maxLines: 1) with
        {
            LineBreaker = context => drawn = forms(context)
        };
        var natural = new SkUiTextLayout().Measure("1,234.56", style, default, double.PositiveInfinity);

        new SkUiTextLayout().Measure("1,234.56", style, default, natural.Width - Noise);

        Assert.Equal(["1,234.56"], drawn);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(300)]
    public void AShrunkFitArrangedAtItsExtentLessNoiseKeepsItsSize(double slot)
    {
        // Shrink to fit (about half size): arranged at the size it measured, less the layout's rounding, the text keeps its scale.
        var style = Style(LineBreakMode.WordWrap, SkUiTextRendering.Simple, maxLines: 1) with { MinimumScale = 0.3f };
        const string text = "0123456789012345678901234567890123456789";
        var layout = new SkUiTextLayout();
        var extent = layout.Measure(text, style, default, slot);

        Assert.Equal(extent.Width, layout.Measure(text, style, default, extent.Width - Noise).Width, 3);
        Assert.Equal(extent.Width, new SkUiTextLayout().Measure(text, style, default, extent.Width - Noise).Width, 3);
    }

    [Fact]
    public void SpanTextAtItsNaturalWidthLessNoiseIsNotTruncated()
    {
        var text = new SkUiRichText.Builder()
            .Add("19-11-", new SkUiTextSpanStyle(Mono, 17.5), new SkUiTextSpanPaint(SKColors.Black))
            .Add("2026", new SkUiTextSpanStyle(Mono, 17.5, FontAttributes.Bold), new SkUiTextSpanPaint(SKColors.Black))
            .Build();
        var style = Style(LineBreakMode.TailTruncation, SkUiTextRendering.Shaped, maxLines: 1);
        var natural = new SkUiRichTextLayout().Measure(text, style, default, double.PositiveInfinity);

        Assert.Equal(natural.Width, new SkUiRichTextLayout().Measure(text, style, default, natural.Width - Noise).Width, 3);
        var reused = new SkUiRichTextLayout();
        reused.Measure(text, style, default, double.PositiveInfinity);
        Assert.Equal(natural.Width, reused.Measure(text, style, default, natural.Width - Noise).Width, 3);
    }

    [Fact]
    public void LabelArrangedAtItsMeasuredWidthLessNoiseShowsAllItsText()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var label = new SkUiLabel
        {
            Text = "19-11-2026", FontFamily = SkUiTestHelpers.BundledFontFamily, FontSize = 17.5, MaxLines = 1,
            LineBreakMode = LineBreakMode.TailTruncation, Padding = new Thickness(6, 0)
        };
        var natural = ((IView)label).Measure(double.PositiveInfinity, double.PositiveInfinity);

        Assert.Equal(natural.Width, ((IView)label).Measure(natural.Width - Noise, double.PositiveInfinity).Width, 3);
    }
}
