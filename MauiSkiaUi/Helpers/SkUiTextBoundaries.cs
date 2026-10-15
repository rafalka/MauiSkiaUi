using System.Globalization;

namespace MauiSkiaUi;

/// <summary>
/// Caret stops in editable text: grapheme clusters (a caret never splits an emoji, a surrogate pair or a letter with its
/// marks) and words for Ctrl / Alt + arrows, double-click and word deletion. Words are runs of letters, digits, marks and
/// connectors (an apostrophe or period between letters stays inside a word); each CJK ideograph or kana is a word of its
/// own; punctuation runs are words; white space separates them. No allocations.
/// </summary>
internal static class SkUiTextBoundaries
{
    // How far back a grapheme search starts at most when no paragraph start is near: cluster breaks are local, so a start
    // this far back realigns long before the caret.
    private const int GraphemeLookBehind = 64;

    private enum WordClass
    {
        Space,
        Word,
        Ideograph,
        Other
    }

    /// <summary>The grapheme boundary after <paramref name="index"/> (the text's length at its end).</summary>
    internal static int NextGrapheme(string text, int index)
    {
        if (index >= text.Length)
            return text.Length;
        return index + Math.Max(1, StringInfo.GetNextTextElementLength(text.AsSpan(index)));
    }

    /// <summary>The grapheme boundary before <paramref name="index"/> (0 at the start).</summary>
    internal static int PreviousGrapheme(string text, int index)
    {
        if (index <= 0)
            return 0;
        index = Math.Min(index, text.Length);
        // Walk forward from a safe start: the paragraph's start, or a character that starts a cluster a little way back. The
        // character before the index may be a line break itself (a caret at a line's start), so the search starts before it.
        var start = index > 1 ? text.LastIndexOf('\n', index - 2) + 1 : 0;
        if (index - start > GraphemeLookBehind)
        {
            start = index - GraphemeLookBehind;
            while (start > 0 && !StartsCluster(text, start))
                start--;
        }
        var previous = start;
        for (var position = start; position < index;)
        {
            previous = position;
            position += Math.Max(1, StringInfo.GetNextTextElementLength(text.AsSpan(position)));
        }
        return previous;
    }

    /// <summary>The nearest grapheme boundary at or before <paramref name="index"/>.</summary>
    internal static int AlignToGrapheme(string text, int index)
    {
        index = Math.Clamp(index, 0, text.Length);
        if (index == 0 || index == text.Length)
            return index;
        var previous = PreviousGrapheme(text, index);
        return NextGrapheme(text, previous) == index ? index : previous;
    }

    // An ASCII letter, digit, space or punctuation not followed by a mark or joiner always starts a cluster.
    private static bool StartsCluster(string text, int index) =>
        text[index] < 0x80 && (index + 1 >= text.Length || text[index + 1] < 0x80 || text[index + 1] == '\n');

    /// <summary>The start of the word before <paramref name="index"/> (Ctrl / Alt + Left, Ctrl + Backspace): white space is skipped first.</summary>
    internal static int PreviousWordStart(string text, int index)
    {
        index = Math.Clamp(index, 0, text.Length);
        while (index > 0 && ClassBefore(text, index) == WordClass.Space)
            index = PreviousCodePoint(text, index);
        if (index == 0)
            return 0;
        var kind = ClassBefore(text, index);
        if (kind == WordClass.Ideograph)
            return PreviousGrapheme(text, index);
        while (index > 0 && SameWord(text, index, kind, backward: true))
            index = PreviousCodePoint(text, index);
        return AlignToGrapheme(text, index);
    }

    /// <summary>The end of the word after <paramref name="index"/> (Alt + Right on Apple platforms): white space is skipped first.</summary>
    internal static int NextWordEnd(string text, int index)
    {
        index = Math.Clamp(index, 0, text.Length);
        while (index < text.Length && ClassAt(text, index) == WordClass.Space)
            index = NextCodePoint(text, index);
        if (index == text.Length)
            return index;
        var kind = ClassAt(text, index);
        if (kind == WordClass.Ideograph)
            return NextGrapheme(text, index);
        while (index < text.Length && SameWord(text, index, kind, backward: false))
            index = NextCodePoint(text, index);
        return AlignToGraphemeForward(text, index);
    }

    /// <summary>The start of the next word after <paramref name="index"/> (Ctrl + Right on Windows and Android): the current word and the white space after it are skipped.</summary>
    internal static int NextWordStart(string text, int index)
    {
        index = Math.Clamp(index, 0, text.Length);
        if (index < text.Length && ClassAt(text, index) != WordClass.Space)
        {
            var kind = ClassAt(text, index);
            if (kind == WordClass.Ideograph)
                index = NextGrapheme(text, index);
            else
                while (index < text.Length && SameWord(text, index, kind, backward: false))
                    index = NextCodePoint(text, index);
        }
        // A line break ends the skip: Ctrl + Right stops at the start of the next line.
        while (index < text.Length && ClassAt(text, index) == WordClass.Space && text[index] != '\n')
            index = NextCodePoint(text, index);
        return AlignToGraphemeForward(text, index);
    }

    /// <summary>The word (or run of white space, or punctuation) holding the character at <paramref name="index"/>: what a double click selects.</summary>
    internal static (int Start, int End) WordAt(string text, int index)
    {
        if (text.Length == 0)
            return (0, 0);
        index = Math.Clamp(index, 0, text.Length);
        // Past the last character: the word before.
        if (index == text.Length)
            index = PreviousCodePoint(text, index);
        var kind = ClassAt(text, index);
        if (kind == WordClass.Ideograph)
        {
            var start = AlignToGrapheme(text, index);
            return (start, NextGrapheme(text, start));
        }
        if (kind == WordClass.Space && text[index] == '\n')
            return (index, index + 1);
        var from = index;
        while (from > 0 && text[from - 1] != '\n' && SameWord(text, from, kind, backward: true))
            from = PreviousCodePoint(text, from);
        var to = index;
        while (to < text.Length && text[to] != '\n' && SameWord(text, to, kind, backward: false))
            to = NextCodePoint(text, to);
        return (AlignToGrapheme(text, from), AlignToGraphemeForward(text, to));
    }

    /// <summary>The paragraph (between line breaks) at <paramref name="index"/>: what a triple click selects.</summary>
    internal static (int Start, int End) ParagraphAt(string text, int index)
    {
        index = Math.Clamp(index, 0, text.Length);
        var start = index > 0 ? text.LastIndexOf('\n', index - 1) + 1 : 0;
        var end = text.IndexOf('\n', index);
        return (start, end < 0 ? text.Length : end);
    }

    private static int AlignToGraphemeForward(string text, int index)
    {
        var aligned = AlignToGrapheme(text, index);
        return aligned == index ? index : NextGrapheme(text, aligned);
    }

    /// <summary>
    /// Whether the code point at (forward) or before (backward) <paramref name="index"/> continues a word of
    /// <paramref name="kind"/>: same class, or an apostrophe / period / middle dot between two word characters.
    /// </summary>
    private static bool SameWord(string text, int index, WordClass kind, bool backward)
    {
        var position = backward ? PreviousCodePoint(text, index) : index;
        var current = Classify(text, position);
        if (current == kind)
            return true;
        if (kind != WordClass.Word || !IsWordJoiner(text[position]))
            return false;
        var before = position > 0 ? Classify(text, PreviousCodePoint(text, position)) : WordClass.Space;
        var after = position + 1 < text.Length ? Classify(text, position + 1) : WordClass.Space;
        return before == WordClass.Word && after == WordClass.Word;
    }

    private static bool IsWordJoiner(char c) => c is '\'' or '’' or '.' or '·';

    private static WordClass ClassAt(string text, int index) => Classify(text, index);

    private static WordClass ClassBefore(string text, int index) => Classify(text, PreviousCodePoint(text, index));

    private static WordClass Classify(string text, int index)
    {
        var cp = text.ConvertToUtf32OrReplacement(index);
        if (cp <= 0xFFFF && char.IsWhiteSpace((char)cp))
            return WordClass.Space;
        if (cp is >= 0x3040 and <= 0x30FF or >= 0x3400 and <= 0x9FFF or >= 0xF900 and <= 0xFAFF or >= 0x20000 and <= 0x3FFFF)
            return WordClass.Ideograph;
        if (cp == '_')
            return WordClass.Word;
        return CharUnicodeInfo.GetUnicodeCategory(cp) switch
        {
            UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter
                or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter or UnicodeCategory.DecimalDigitNumber
                or UnicodeCategory.LetterNumber or UnicodeCategory.OtherNumber or UnicodeCategory.NonSpacingMark
                or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark or UnicodeCategory.ConnectorPunctuation
                or UnicodeCategory.Format => WordClass.Word,
            _ => WordClass.Other
        };
    }

    private static int PreviousCodePoint(string text, int index)
    {
        if (index <= 0)
            return 0;
        index--;
        if (index > 0 && char.IsLowSurrogate(text[index]) && char.IsHighSurrogate(text[index - 1]))
            index--;
        return index;
    }

    private static int NextCodePoint(string text, int index)
    {
        if (index >= text.Length)
            return text.Length;
        return index + (char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]) ? 2 : 1);
    }
}
