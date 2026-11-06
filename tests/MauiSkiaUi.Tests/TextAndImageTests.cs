using System.Diagnostics;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

public class TextAndImageTests
{
    private static SKFont Font()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "RobotoMono-Regular.ttf");
        return new SKFont(SKTypeface.FromFile(path), 16);
    }

    [Fact]
    public void WordWrapBreaksAtSpacesAndEveryLineFits()
    {
        using var font = Font();
        const string text = "The quick brown fox jumps over the lazy dog again and again";
        var lines = SkUiTestHelpers.BreakLines(text, 100, LineBreakMode.WordWrap, font.Typeface);
        Assert.True(lines.Count > 1);
        Assert.Equal(text.Replace(" ", ""), string.Concat(lines).Replace(" ", ""));
        foreach (var line in lines)
        {
            Assert.True(font.MeasureText(line) <= 100.5f, $"'{line}' overflows");
            Assert.False(line.StartsWith(' '));
        }
    }

    [Fact]
    public void CharacterWrapNeverSplitsGraphemes()
    {
        using var font = Font();
        const string text = "ab👍🏽cd👩‍👩‍👧éfghijk";
        var lines = SkUiTestHelpers.BreakLines(text, 20, LineBreakMode.CharacterWrap, font.Typeface);
        Assert.Equal(text, string.Concat(lines));
        foreach (var line in lines)
            Assert.False(char.IsLowSurrogate(line[0]) || line[0] == '́' || line[0] == '‍', $"line starts mid-grapheme: {line}");
    }

    private const string Words = "lorem ipsum dolor sit amet";
    private const int ShortParagraphCopies = 250;
    private const double NarrowWidth = 300;

    /// <summary>How many times longer the long paragraph (about 50k characters) is, and its long lines, than the short ones.</summary>
    private const int LengthFactor = 8;

    /// <summary>
    /// Halfway between linear (the factor itself) and quadratic (its square), on a log scale: about 2.8 times off either.
    /// </summary>
    private static readonly double SlowdownLimit = Math.Pow(LengthFactor, 1.5);

    /// <summary>How many rounds <see cref="LongParagraphWrapsInLinearTime"/> times (it keeps each size's fastest).</summary>
    private const int TimedRounds = 20;

    // How time grows with the text, not how many milliseconds it takes: load from other tests and processes (forced GCs,
    // other builds) can stretch a run many times over, and a machine's speed says nothing about the complexity. A
    // quadratic scan per line only shows when the lines grow too, and a scan of the rest of the text per line only when
    // the number of lines grows, so both are compared with the same short paragraph. The sizes take turns and each keeps
    // its fastest of many short runs, so load has to hit one size's every run, and not the others', to change a ratio.
    [Fact]
    public void LongParagraphWrapsInLinearTime()
    {
        using var font = Font();
        var shortText = string.Join(' ', Enumerable.Repeat(Words, ShortParagraphCopies));
        var longText = string.Join(' ', Enumerable.Repeat(Words, ShortParagraphCopies * LengthFactor));
        const double wideWidth = NarrowWidth * LengthFactor;
        Assert.True(SkUiTestHelpers.BreakLines(longText, NarrowWidth, LineBreakMode.WordWrap, font.Typeface).Count > 100);
        Assert.True(SkUiTestHelpers.BreakLines(longText, wideWidth, LineBreakMode.WordWrap, font.Typeface).Count > 100);

        var bestShort = TimeSpan.MaxValue;
        var bestManyLines = TimeSpan.MaxValue;
        var bestLongLines = TimeSpan.MaxValue;
        for (var round = 0; round < TimedRounds; round++)
        {
            bestShort = Min(bestShort, TimeWrap(shortText, NarrowWidth));
            bestManyLines = Min(bestManyLines, TimeWrap(longText, NarrowWidth));
            bestLongLines = Min(bestLongLines, TimeWrap(longText, wideWidth));
        }
        var manyLines = bestManyLines / bestShort;
        var longLines = bestLongLines / bestShort;
        var times = $"short {bestShort.TotalMilliseconds:F2} ms, many lines {bestManyLines.TotalMilliseconds:F2} ms, long lines {bestLongLines.TotalMilliseconds:F2} ms";
        Assert.True(manyLines < SlowdownLimit, $"{LengthFactor} times the lines took {manyLines:F1} times as long ({times}).");
        Assert.True(longLines < SlowdownLimit, $"{LengthFactor} times the line length took {longLines:F1} times as long ({times}).");
        return;

        TimeSpan TimeWrap(string text, double width)
        {
            var start = Stopwatch.GetTimestamp();
            SkUiTestHelpers.BreakLines(text, width, LineBreakMode.WordWrap, font.Typeface);
            return Stopwatch.GetElapsedTime(start);
        }

        static TimeSpan Min(TimeSpan first, TimeSpan second) => first < second ? first : second;
    }

    [Fact]
    public void TailTruncationFitsWidthAndEndsWithEllipsis()
    {
        using var font = Font();
        var lines = SkUiTestHelpers.BreakLines("A fairly long single line of text", 120, LineBreakMode.TailTruncation, font.Typeface);
        var line = Assert.Single(lines);
        Assert.EndsWith("...", line);
        Assert.True(font.MeasureText(line) <= 120);
    }

    [Fact]
    public void LabelColorChangeReRecordsWithoutRewrapping()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var label = new CountingBreakLabel { Text = "Hello wrapped world", FontFamily = SkUiTestHelpers.BundledFontFamily, WidthRequest = 60 };
        var root = new SkUiContentView { Content = label };
        using var surface = new SkUiTestSurface(root, 100, 100);
        surface.Frame();
        var breaks = label.Breaks;
        label.TextColor = Colors.Red;
        surface.Frame();
        label.TextColor = Colors.Blue;
        surface.Frame();
        Assert.Equal(breaks, label.Breaks);
    }

    private sealed class CountingBreakLabel : SkUiLabel
    {
        public int Breaks { get; private set; }

        public CountingBreakLabel() => MeasureInvalidated += (_, _) => { };

        protected override Size MeasureContent(double widthConstraint, double heightConstraint)
        {
            Breaks++;
            return base.MeasureContent(widthConstraint, heightConstraint);
        }
    }
}
