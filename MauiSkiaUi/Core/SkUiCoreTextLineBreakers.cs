using System.Globalization;
using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// Splits label text into display lines for a given available width in DIPs.
/// Used by <see cref="SkUiCoreLabel"/> so wrap/truncate policy is replaceable.
/// </summary>
/// <param name="text">Full label text (may contain newlines).</param>
/// <param name="availableWidth">Content width after padding; may be infinite.</param>
/// <param name="font">Font used for measuring glyphs.</param>
/// <returns>One entry per painted line (empty string allowed for blank paragraphs).</returns>
public delegate IReadOnlyList<string> SkUiCoreTextLineBreaker(string text, double availableWidth, SKFont font);

/// <summary>
/// Built-in line breakers matching MAUI <see cref="LineBreakMode"/> semantics (LTR, no complex shaping).
/// Stock instances are stable (same reference for a given mode) so labels can compare by identity.
/// </summary>
public static class SkUiCoreTextLineBreakers
{
    /// <summary>Single line per paragraph; no wrap or ellipsis.</summary>
    public static SkUiCoreTextLineBreaker NoWrap { get; } = BreakNoWrap;

    /// <summary>Wrap at word boundaries when possible.</summary>
    public static SkUiCoreTextLineBreaker WordWrap { get; } = BreakWordWrap;

    /// <summary>Wrap at grapheme boundaries.</summary>
    public static SkUiCoreTextLineBreaker CharacterWrap { get; } = BreakCharacterWrap;

    /// <summary>One truncated line per paragraph with leading ellipsis.</summary>
    public static SkUiCoreTextLineBreaker HeadTruncation { get; } = BreakHeadTruncation;

    /// <summary>One truncated line per paragraph with middle ellipsis.</summary>
    public static SkUiCoreTextLineBreaker MiddleTruncation { get; } = BreakMiddleTruncation;

    /// <summary>One truncated line per paragraph with trailing ellipsis.</summary>
    public static SkUiCoreTextLineBreaker TailTruncation { get; } = BreakTailTruncation;

    /// <summary>Returns the stock breaker for <paramref name="mode"/>.</summary>
    public static SkUiCoreTextLineBreaker For(LineBreakMode mode) => mode switch
    {
        LineBreakMode.NoWrap => NoWrap,
        LineBreakMode.WordWrap => WordWrap,
        LineBreakMode.CharacterWrap => CharacterWrap,
        LineBreakMode.HeadTruncation => HeadTruncation,
        LineBreakMode.MiddleTruncation => MiddleTruncation,
        LineBreakMode.TailTruncation => TailTruncation,
        _ => WordWrap
    };

    private enum TruncationKind { Head, Middle, Tail }

    private static IReadOnlyList<string> BreakNoWrap(string text, double availableWidth, SKFont font) =>
        SplitParagraphs(text, paragraph => [paragraph]);

    private static IReadOnlyList<string> BreakWordWrap(string text, double availableWidth, SKFont font) =>
        Wrap(text, availableWidth, font, preferWordBreak: true);

    private static IReadOnlyList<string> BreakCharacterWrap(string text, double availableWidth, SKFont font) =>
        Wrap(text, availableWidth, font, preferWordBreak: false);

    private static IReadOnlyList<string> BreakHeadTruncation(string text, double availableWidth, SKFont font) =>
        TruncateEachParagraph(text, availableWidth, font, TruncationKind.Head);

    private static IReadOnlyList<string> BreakMiddleTruncation(string text, double availableWidth, SKFont font) =>
        TruncateEachParagraph(text, availableWidth, font, TruncationKind.Middle);

    private static IReadOnlyList<string> BreakTailTruncation(string text, double availableWidth, SKFont font) =>
        TruncateEachParagraph(text, availableWidth, font, TruncationKind.Tail);

    private static IReadOnlyList<string> Wrap(string text, double availableWidth, SKFont font, bool preferWordBreak)
    {
        if (double.IsInfinity(availableWidth))
            return BreakNoWrap(text, availableWidth, font);

        availableWidth = Math.Max(0, availableWidth);
        var maxWidth = (float)availableWidth;
        return SplitParagraphs(text, paragraph =>
        {
            // Linear: each line is fitted with one BreakText pass, then snapped to a grapheme boundary.
            var lines = new List<string>();
            if (paragraph.Length == 0)
            {
                lines.Add(paragraph);
                return lines;
            }
            int[]? boundaries = null;
            var start = 0;
            while (start < paragraph.Length)
            {
                var rest = paragraph.AsSpan(start);
                var fit = font.BreakText(rest, maxWidth);
                if (fit >= rest.Length)
                {
                    lines.Add(paragraph[start..]);
                    break;
                }
                boundaries ??= StringInfo.ParseCombiningCharacters(paragraph);
                var end = SnapToGrapheme(boundaries, start + fit);
                if (end <= start)
                    end = NextGrapheme(boundaries, start, paragraph.Length);
                else if (preferWordBreak)
                {
                    var space = paragraph.LastIndexOf(' ', end - 1, end - start);
                    if (space > start) end = space;
                }
                lines.Add(paragraph[start..end]);
                start = end;
                while (start < paragraph.Length && paragraph[start] == ' ')
                    start++;
            }
            return lines;
        });
    }

    /// <summary>Largest grapheme start ≤ <paramref name="index"/>.</summary>
    private static int SnapToGrapheme(int[] boundaries, int index)
    {
        var position = Array.BinarySearch(boundaries, index);
        return position >= 0 ? boundaries[position] : boundaries[Math.Max(0, ~position - 1)];
    }

    /// <summary>First grapheme start after <paramref name="index"/> (at least one grapheme per line).</summary>
    private static int NextGrapheme(int[] boundaries, int index, int length)
    {
        var position = Array.BinarySearch(boundaries, index);
        var next = position >= 0 ? position + 1 : ~position;
        return next < boundaries.Length ? boundaries[next] : length;
    }

    private static IReadOnlyList<string> TruncateEachParagraph(
        string text, double availableWidth, SKFont font, TruncationKind kind) =>
        SplitParagraphs(text, paragraph => [Truncate(paragraph, availableWidth, font, kind)]);

    private static string Truncate(string value, double availableWidth, SKFont font, TruncationKind kind)
    {
        if (double.IsInfinity(availableWidth) || font.MeasureText(value) <= availableWidth)
            return value;

        availableWidth = Math.Max(0, availableWidth);
        const string ellipsis = "...";
        if (font.MeasureText(ellipsis) > availableWidth)
            return string.Empty;

        var boundaries = StringInfo.ParseCombiningCharacters(value);
        if (kind == TruncationKind.Tail)
        {
            // Linear fast path: fit the prefix once, then back off graphemes only if rounding overshoots.
            var prefix = SnapToGrapheme(boundaries, font.BreakText(value, (float)(availableWidth - font.MeasureText(ellipsis))));
            for (var end = prefix; end >= 0; end = end == 0 ? -1 : SnapToGrapheme(boundaries, end - 1))
            {
                var candidate = value[..end] + ellipsis;
                if (font.MeasureText(candidate) <= availableWidth)
                    return candidate;
            }
            return ellipsis;
        }
        for (var count = boundaries.Length - 1; count >= 0; count--)
        {
            var prefixCount = kind switch
            {
                TruncationKind.Head => 0,
                TruncationKind.Middle => (count + 1) / 2,
                _ => count
            };
            var suffixCount = count - prefixCount;
            var candidate = value[..(prefixCount == 0 ? 0 : boundaries[prefixCount])]
                + ellipsis
                + (suffixCount == 0 ? string.Empty : value[boundaries[boundaries.Length - suffixCount]..]);
            if (font.MeasureText(candidate) <= availableWidth)
                return candidate;
        }

        return ellipsis;
    }

    private static IReadOnlyList<string> SplitParagraphs(string text, Func<string, IEnumerable<string>> mapParagraph)
    {
        if (text.Length == 0)
            return [];

        var result = new List<string>();
        foreach (var paragraph in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            result.AddRange(mapParagraph(paragraph));
        return result;
    }
}
