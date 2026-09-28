using System.Globalization;

namespace MauiSkiaUi;

/// <summary>Paragraph base direction for drawn text.</summary>
public enum SkUiTextDirection
{
    /// <summary>First strong character decides (UAX #9 rules P2/P3); left-to-right when there is none.</summary>
    Auto,
    /// <summary>Left-to-right paragraph.</summary>
    LeftToRight,
    /// <summary>Right-to-left paragraph.</summary>
    RightToLeft
}

/// <summary>
/// Unicode Bidirectional Algorithm (UAX #9) for one paragraph without explicit embeddings: character classes,
/// weak (W1–W7) and neutral (N1–N2) type resolution, implicit levels (I1–I2) and line reordering (L2).
/// Explicit embedding / override / isolate controls (LRE…PDI) are treated as boundary-neutral characters,
/// which is correct for the vast majority of UI strings. LRM / RLM / ALM are honored as strong marks.
/// </summary>
internal static class SkUiBidi
{
    internal enum BidiClass : byte { L, R, AL, EN, ES, ET, AN, CS, NSM, BN, B, S, WS, ON }

    /// <summary>Approximate Unicode Bidi_Class of a code point (block ranges + general category).</summary>
    internal static BidiClass Classify(int cp)
    {
        switch (cp)
        {
            case 0x200E: return BidiClass.L;   // LRM
            case 0x200F: return BidiClass.R;   // RLM
            case 0x061C: return BidiClass.AL;  // ALM
            case '\t': return BidiClass.S;
            case '\n' or '\r' or 0x2029: return BidiClass.B;
            case '+' or '-' or 0x2212 or 0xFB29: return BidiClass.ES;
            case '#' or '$' or '%' or 0x00B0 or 0x00B1 or 0x2030 or 0x2031 or 0x066A: return BidiClass.ET;
            case ',' or '.' or '/' or ':' or 0x00A0 or 0x202F or 0x2044 or 0x060C or 0xFE50 or 0xFE52 or 0xFE55: return BidiClass.CS;
        }
        if (cp is >= '0' and <= '9' or >= 0xFF10 and <= 0xFF19 or >= 0x06F0 and <= 0x06F9)
            return BidiClass.EN;
        if (cp is >= 0x0660 and <= 0x0669 or 0x066B or 0x066C or >= 0x10E60 and <= 0x10E7E)
            return BidiClass.AN;

        var category = CharUnicodeInfo.GetUnicodeCategory(cp);
        switch (category)
        {
            case UnicodeCategory.NonSpacingMark:
            case UnicodeCategory.EnclosingMark:
                return BidiClass.NSM;
            case UnicodeCategory.Format:
                return BidiClass.BN;
            case UnicodeCategory.SpaceSeparator:
                return BidiClass.WS;
            case UnicodeCategory.LineSeparator:
                return BidiClass.WS;
            case UnicodeCategory.ParagraphSeparator:
                return BidiClass.B;
            case UnicodeCategory.CurrencySymbol:
                return BidiClass.ET;
            case UnicodeCategory.Control:
                return BidiClass.BN;
        }

        var rtl = RtlBlock(cp);
        if (rtl is { } strong)
        {
            // Letters, other marks and most punctuation inside RTL blocks take the block's strong class.
            return category is UnicodeCategory.DecimalDigitNumber ? BidiClass.AN : strong;
        }

        return category switch
        {
            UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter
                or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter or UnicodeCategory.SpacingCombiningMark
                or UnicodeCategory.LetterNumber or UnicodeCategory.DecimalDigitNumber or UnicodeCategory.PrivateUse => BidiClass.L,
            UnicodeCategory.OtherNumber => cp is >= 0x2070 and <= 0x2089 or 0x00B2 or 0x00B3 or 0x00B9 ? BidiClass.EN : BidiClass.ON,
            _ => BidiClass.ON
        };
    }

    /// <summary>R for Hebrew-like scripts, AL for Arabic-like scripts, null elsewhere.</summary>
    private static BidiClass? RtlBlock(int cp) => cp switch
    {
        >= 0x0590 and <= 0x05FF => BidiClass.R,   // Hebrew
        >= 0x0600 and <= 0x07BF => BidiClass.AL,  // Arabic, Syriac, Arabic Supplement, Thaana
        >= 0x07C0 and <= 0x085F => BidiClass.R,   // NKo, Samaritan, Mandaic
        >= 0x0860 and <= 0x08FF => BidiClass.AL,  // Syriac Supplement, Arabic Extended
        >= 0xFB1D and <= 0xFB4F => BidiClass.R,   // Hebrew presentation forms
        >= 0xFB50 and <= 0xFDFF => BidiClass.AL,  // Arabic presentation forms A
        >= 0xFE70 and <= 0xFEFF => BidiClass.AL,  // Arabic presentation forms B
        >= 0x10800 and <= 0x10CFF => BidiClass.R, // historic RTL scripts
        >= 0x10D00 and <= 0x10D3F => BidiClass.AL, // Hanifi Rohingya
        >= 0x10D40 and <= 0x10FFF => BidiClass.R,
        >= 0x1E800 and <= 0x1EC6F => BidiClass.R,  // Mende Kikakui, Adlam
        >= 0x1EC70 and <= 0x1EEFF => BidiClass.AL, // Arabic mathematical / Siyaq numbers
        _ => null
    };

    /// <summary>Resolves the base level (0 = LTR, 1 = RTL) of <paramref name="text"/>.</summary>
    internal static byte BaseLevel(string text, SkUiTextDirection direction)
    {
        if (direction == SkUiTextDirection.LeftToRight) return 0;
        if (direction == SkUiTextDirection.RightToLeft) return 1;
        for (var index = 0; index < text.Length; index++)
        {
            var cp = text.ConvertToUtf32OrReplacement(index);
            var type = Classify(cp);
            if (type == BidiClass.L) return 0;
            if (type is BidiClass.R or BidiClass.AL) return 1;
            if (char.IsHighSurrogate(text[index])) index++;
        }
        return 0;
    }

    /// <summary>Resolves an embedding level per UTF-16 code unit (low surrogates copy their high surrogate).</summary>
    internal static byte[] ResolveLevels(string text, byte baseLevel)
    {
        var n = text.Length;
        var levels = new byte[n];
        if (n == 0) return levels;
        var types = new BidiClass[n];
        var original = new BidiClass[n];
        var hasRtlOrNumbers = false;
        for (var index = 0; index < n; index++)
        {
            var cp = text.ConvertToUtf32OrReplacement(index);
            var type = Classify(cp);
            types[index] = original[index] = type;
            hasRtlOrNumbers |= type is BidiClass.R or BidiClass.AL or BidiClass.AN || (baseLevel == 1 && type == BidiClass.EN);
            if (char.IsHighSurrogate(text[index]) && index + 1 < n)
            {
                index++;
                types[index] = original[index] = BidiClass.BN;
            }
        }
        // Fast path: pure left-to-right text in an LTR paragraph.
        if (!hasRtlOrNumbers && baseLevel == 0)
            return levels;

        var sos = baseLevel % 2 == 0 ? BidiClass.L : BidiClass.R;

        // W1: NSM takes the type of the previous character (sos at start). BN (and surrogate tails) inherit too.
        var previous = sos;
        for (var index = 0; index < n; index++)
        {
            if (types[index] is BidiClass.NSM or BidiClass.BN)
                types[index] = previous is BidiClass.BN ? sos : previous;
            else
                previous = types[index];
        }
        // W2: EN after AL (looking back to the last strong) becomes AN.
        var lastStrong = sos;
        for (var index = 0; index < n; index++)
        {
            var type = types[index];
            if (type is BidiClass.L or BidiClass.R or BidiClass.AL) lastStrong = type;
            else if (type == BidiClass.EN && lastStrong == BidiClass.AL) types[index] = BidiClass.AN;
        }
        // W3: AL → R.
        for (var index = 0; index < n; index++)
            if (types[index] == BidiClass.AL) types[index] = BidiClass.R;
        // W4: a single separator between two numbers of the same kind joins them.
        for (var index = 1; index < n - 1; index++)
        {
            var type = types[index];
            var before = types[index - 1];
            var after = types[index + 1];
            if (type == BidiClass.ES && before == BidiClass.EN && after == BidiClass.EN) types[index] = BidiClass.EN;
            else if (type == BidiClass.CS && before == after && before is BidiClass.EN or BidiClass.AN) types[index] = before;
        }
        // W5: terminators adjacent to European numbers become EN.
        for (var index = 0; index < n; index++)
        {
            if (types[index] != BidiClass.ET) continue;
            var end = index;
            while (end < n && types[end] == BidiClass.ET) end++;
            var touchesNumber = (index > 0 && types[index - 1] == BidiClass.EN) || (end < n && types[end] == BidiClass.EN);
            if (touchesNumber)
                for (var fill = index; fill < end; fill++) types[fill] = BidiClass.EN;
            index = end - 1;
        }
        // W6: remaining separators and terminators become neutral.
        for (var index = 0; index < n; index++)
            if (types[index] is BidiClass.ES or BidiClass.ET or BidiClass.CS) types[index] = BidiClass.ON;
        // W7: EN after L (looking back to the last strong) becomes L.
        lastStrong = sos;
        for (var index = 0; index < n; index++)
        {
            var type = types[index];
            if (type is BidiClass.L or BidiClass.R) lastStrong = type;
            else if (type == BidiClass.EN && lastStrong == BidiClass.L) types[index] = BidiClass.L;
        }
        // N1/N2: neutral runs take the surrounding direction when both sides agree, else the embedding direction.
        for (var index = 0; index < n; index++)
        {
            if (!IsNeutral(types[index])) continue;
            var end = index;
            while (end < n && IsNeutral(types[end])) end++;
            var leading = index == 0 ? sos : StrongDirection(types[index - 1]);
            var trailing = end >= n ? sos : StrongDirection(types[end]);
            var resolved = leading == trailing ? leading : sos;
            for (var fill = index; fill < end; fill++) types[fill] = resolved;
            index = end - 1;
        }
        // I1/I2: implicit levels.
        for (var index = 0; index < n; index++)
        {
            var type = types[index];
            var level = baseLevel;
            if (level % 2 == 0)
            {
                if (type == BidiClass.R) level += 1;
                else if (type is BidiClass.AN or BidiClass.EN) level += 2;
            }
            else if (type is BidiClass.L or BidiClass.EN or BidiClass.AN)
            {
                level += 1;
            }
            levels[index] = level;
        }
        // L1 (partial): paragraph-final whitespace and segment separators return to the paragraph level.
        for (var index = n - 1; index >= 0 && original[index] is BidiClass.WS or BidiClass.S or BidiClass.BN; index--)
            levels[index] = baseLevel;
        for (var index = 0; index < n; index++)
            if (original[index] == BidiClass.S) levels[index] = baseLevel;
        return levels;
    }

    private static bool IsNeutral(BidiClass type) => type is BidiClass.ON or BidiClass.WS or BidiClass.S or BidiClass.B or BidiClass.BN;

    private static BidiClass StrongDirection(BidiClass type) => type == BidiClass.L ? BidiClass.L : BidiClass.R;

    /// <summary>
    /// L2: visual order of runs given their levels — reverse every maximal sequence at each level from the
    /// highest down to the lowest odd level. Returns run indices in left-to-right display order.
    /// </summary>
    internal static int[] VisualOrder(IReadOnlyList<byte> runLevels)
    {
        var count = runLevels.Count;
        var order = new int[count];
        for (var index = 0; index < count; index++) order[index] = index;
        if (count <= 1) return order;
        byte highest = 0, lowestOdd = byte.MaxValue;
        foreach (var level in runLevels)
        {
            highest = Math.Max(highest, level);
            if (level % 2 == 1) lowestOdd = Math.Min(lowestOdd, level);
        }
        if (lowestOdd == byte.MaxValue) return order;
        for (var level = highest; level >= lowestOdd; level--)
        {
            for (var index = 0; index < count; index++)
            {
                if (runLevels[order[index]] < level) continue;
                var end = index;
                while (end < count && runLevels[order[end]] >= level) end++;
                Array.Reverse(order, index, end - index);
                index = end;
            }
            if (level == 0) break;
        }
        return order;
    }
}

internal static class SkUiCharExtensions
{
    /// <summary>Code point at <paramref name="index"/>; lone surrogates map to U+FFFD.</summary>
    internal static int ConvertToUtf32OrReplacement(this string text, int index)
    {
        var c = text[index];
        if (char.IsHighSurrogate(c) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
            return char.ConvertToUtf32(c, text[index + 1]);
        return char.IsSurrogate(c) ? 0xFFFD : c;
    }
}
