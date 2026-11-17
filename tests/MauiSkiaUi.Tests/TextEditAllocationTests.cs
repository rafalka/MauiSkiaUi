using Xunit;
using Xunit.Abstractions;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Re-laying out an edited paragraph reuses the layout's own objects (1.14 B1): the paragraph and line objects and the
/// lines' caret-edge arrays an edit replaced are refilled by the next edit. A reused layout gives what a fresh one gives.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class TextEditAllocationTests(ITestOutputHelper output)
{
    private const string Sentence = "The quick brown fox jumps over the lazy dog and keeps running far away now ok";

    private static SkUiTextStyle Style(SkUiTextRendering rendering = SkUiTextRendering.Auto) =>
        new SkUiTextStyle(SkUiTypefaces.Resolve(SkUiTestHelpers.BundledFontFamily), 14) with { Rendering = rendering };

    /// <summary>
    /// What an edit of one 78-character paragraph still allocates, per rendering path: the shaped paragraph (its runs,
    /// glyph, position and cluster arrays, advances and, when shaped, bidi levels), the shaped line and its text blob, and
    /// for text with several paragraphs the edited paragraph's string. At 1.13.0: plain 2,580, shaped 3,228, simple 1,480 and
    /// plain with three paragraphs 2,760 bytes; each path now about 984 bytes less (the paragraph and line objects and three
    /// caret-edge arrays of 78 entries).
    /// </summary>
    [Theory]
    [InlineData(SkUiTextRendering.Auto, false, 1596)]
    [InlineData(SkUiTextRendering.Shaped, false, 2244)]
    [InlineData(SkUiTextRendering.Simple, false, 496)]
    [InlineData(SkUiTextRendering.Auto, true, 1776)]
    public void AnEditAllocatesOnlyTheShapedParagraphAndItsBlob(SkUiTextRendering rendering, bool paragraphs, long bound)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var texts = new string[100];
        for (var i = 0; i < texts.Length; i++) // typing and deleting one character, as the strings an app gets
            texts[i] = (paragraphs ? "first line\n" : "") + Sentence + new string('x', i % 2) + (paragraphs ? "\nlast line" : "");
        var layout = new SkUiTextEditLayout();
        var style = Style(rendering);
        foreach (var text in texts)
            layout.Update(text, style, double.PositiveInfinity);
        var fewest = long.MaxValue;
        for (var batch = 0; batch < 3; batch++)
        {
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            foreach (var text in texts)
                layout.Update(text, style, double.PositiveInfinity);
            fewest = Math.Min(fewest, GC.GetAllocatedBytesForCurrentThread() - allocated);
        }
        var perEdit = fewest / texts.Length;
        output.WriteLine($"{rendering}{(paragraphs ? ", three paragraphs" : "")}: {perEdit} bytes per edit");
        Assert.Equal(1, layout.LastShapedParagraphs);
        Assert.True(perEdit <= bound, $"{perEdit} bytes per edit");
    }

    [Fact]
    public void TypingOnAndOnReallocatesTheEdgesOncePerSixteenCharacters()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var style = Style();
        var texts = new string[101]; // 100 characters typed after an 80-character paragraph
        for (var i = 0; i < texts.Length; i++)
            texts[i] = Sentence + "ab" + new string('c', i);
        long fewest = long.MaxValue;
        for (var batch = 0; batch < 3; batch++)
        {
            var layout = new SkUiTextEditLayout();
            layout.Update(texts[0], style, double.PositiveInfinity);
            layout.Update(texts[1], style, double.PositiveInfinity); // the first edit keeps a spare
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 2; i < texts.Length; i++)
                layout.Update(texts[i], style, double.PositiveInfinity);
            fewest = Math.Min(fewest, GC.GetAllocatedBytesForCurrentThread() - allocated);
        }
        var perKeystroke = fewest / (texts.Length - 2);
        output.WriteLine($"typing: {perKeystroke} bytes per keystroke");
        Assert.True(perKeystroke <= TypingBytesPerKeystroke, $"{perKeystroke} bytes per keystroke");
    }

    /// <summary>The shaped paragraph and blob growing with the text, plus the edges reallocated every 16 characters (at 1.13.0: 3,816).</summary>
    private const long TypingBytesPerKeystroke = 2523;

    public static TheoryData<SkUiTextRendering, double> Paths() => new()
    {
        { SkUiTextRendering.Auto, double.PositiveInfinity },
        { SkUiTextRendering.Shaped, double.PositiveInfinity },
        { SkUiTextRendering.Simple, double.PositiveInfinity },
        { SkUiTextRendering.Auto, 120 },
        { SkUiTextRendering.Shaped, 120 },
    };

    [Theory]
    [MemberData(nameof(Paths))]
    public void AReusedLayoutGivesWhatAFreshOneGives(SkUiTextRendering rendering, double wrapWidth)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var style = Style(rendering);
        string[] edits =
        [
            "a", "ab", Sentence, Sentence + " and more words to wrap onto further lines", "short",
            "שלום world", "abc שלום עולם def", "😀 emoji 😀😀", "a😀b", "",
            "one\ntwo", "one\ntwo\nthree", "one\nTWO changed\nthree", "one\nthree", Sentence + "\n" + Sentence, "x",
        ];
        var reused = new SkUiTextEditLayout();
        foreach (var text in edits)
        {
            reused.Update(text, style, wrapWidth);
            var fresh = new SkUiTextEditLayout();
            fresh.Update(text, style, wrapWidth);
            Assert.Equal(fresh.Lines.Count, reused.Lines.Count);
            Assert.Equal(fresh.Width, reused.Width);
            Assert.Equal(fresh.Height, reused.Height);
            for (var index = 0; index < fresh.Lines.Count; index++)
            {
                var (expected, actual) = (fresh.Lines[index], reused.Lines[index]);
                Assert.Equal((expected.Start, expected.End, expected.EndsParagraph, expected.Top, expected.Ascent, expected.Height),
                    (actual.Start, actual.End, actual.EndsParagraph, actual.Top, actual.Ascent, actual.Height));
                Assert.Equal((expected.Width, expected.Offset, expected.BaseLevel, expected.Text), (actual.Width, actual.Offset, actual.BaseLevel, actual.Text));
                Assert.Equal(expected.Blob is null, actual.Blob is null);
                Assert.True(expected.Left.AsSpan(0, expected.Length).SequenceEqual(actual.Left.AsSpan(0, actual.Length)), $"left edges of line {index} of \"{text}\"");
                Assert.True(expected.Right.AsSpan(0, expected.Length).SequenceEqual(actual.Right.AsSpan(0, actual.Length)), $"right edges of line {index} of \"{text}\"");
                Assert.True(expected.Levels.AsSpan(0, expected.Length).SequenceEqual(actual.Levels.AsSpan(0, actual.Length)), $"levels of line {index} of \"{text}\"");
            }
            for (var caret = 0; caret <= text.Length; caret++)
                Assert.Equal(fresh.CaretRect(caret), reused.CaretRect(caret));
        }
    }
}
