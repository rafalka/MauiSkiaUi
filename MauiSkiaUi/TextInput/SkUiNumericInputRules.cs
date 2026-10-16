using System.Globalization;
using System.Text;

namespace MauiSkiaUi;

/// <summary>
/// A numeric entry's input rules (D16), run as the field's own formatter before the app's (<see cref="SkUiTextEditor.InputRules"/>):
/// <list type="bullet">
/// <item>Digits (any script's, as ASCII), the culture's decimal separator once (<c>.</c> and <c>,</c> both type it; typing it again
/// moves the caret after the one there; refused for integers and with no decimal digits) and a minus sign only when the range has
/// negatives.</item>
/// <item>The sign toggles wherever the caret is (<c>-</c> on <c>12|3</c> gives <c>-123</c>; <c>+</c> removes it); nothing goes
/// before it. A separator typed first gets a 0 (<c>,5</c> → <c>0,5</c>).</item>
/// <item>Pasted text is cleaned of spaces, group separators, currency and percent signs (<c>1 234,50 €</c> → <c>1234,50</c>);
/// a group separator groups only before exactly three digits, else <c>.</c> and <c>,</c> are the decimal separator
/// (<c>3.14</c> in de-DE is 3,14); text that is still no number is refused.</item>
/// <item>No more decimal digits than allowed; nothing that would overflow the value's type (a <see cref="byte"/> entry refuses
/// 256). The range is not applied while typing.</item>
/// </list>
/// </summary>
internal sealed class SkUiNumericInputRules(SkUiNumericEntryModel entry) : SkUiTextInputFormatter
{
    private const string Minus = "-";

    public override SkUiTextEditValue Format(SkUiTextEditValue oldValue, SkUiTextEditValue newValue)
    {
        var old = oldValue.Text;
        var text = newValue.Text;
        // The one replacement that made the new text: old [prefix, old.Length - suffix) became new [prefix, text.Length - suffix). The
        // suffix stops at the new caret (it ends the insertion), so a typed digit next to the same digit lands where it was typed.
        var suffix = Math.Min(CommonSuffix(old, text), Math.Min(old.Length, text.Length - Math.Clamp(newValue.SelectionEnd, 0, text.Length)));
        var prefix = Math.Min(CommonPrefix(old, text), Math.Min(old.Length, text.Length) - suffix);
        var before = old[..prefix];
        var after = old[(old.Length - suffix)..];
        var inserted = text[prefix..(text.Length - suffix)];
        var symbols = entry.Symbols;
        if (inserted.Length == 0)
            return IsValid(before + after, symbols) ? newValue : oldValue;
        return inserted.Length == 1 ? Typed(inserted[0], oldValue, before, after, symbols) : Pasted(inserted, oldValue, before, after, symbols);
    }

    private SkUiTextEditValue Typed(char character, SkUiTextEditValue oldValue, string before, string after, SkUiNumericCulture symbols)
    {
        if (SkUiNumericCulture.IsMinus(character) || character == '+')
            return ToggleSign(character == '+', oldValue, symbols);
        string insertion;
        if (char.IsDigit(character))
        {
            insertion = ((char)('0' + (int)char.GetNumericValue(character))).ToString();
        }
        else if (symbols.IsDecimalKey(character))
        {
            if (entry.DecimalDigits == 0)
                return oldValue;
            // One separator: typing it again steps the caret over the one already there.
            var existing = oldValue.Text.IndexOf(symbols.DecimalSeparator, StringComparison.Ordinal);
            if ((before + after).Contains(symbols.DecimalSeparator, StringComparison.Ordinal) && existing >= 0)
                return new SkUiTextEditValue(oldValue.Text, existing + symbols.DecimalSeparator.Length);
            insertion = symbols.DecimalSeparator;
        }
        else
        {
            return oldValue;
        }
        (before, after) = AfterSign(before, after);
        // A separator first gets a 0.
        if (insertion == symbols.DecimalSeparator && !HasDigit(before))
            insertion = "0" + insertion;
        var candidate = before + insertion + after;
        return IsValid(candidate, symbols) ? new SkUiTextEditValue(candidate, before.Length + insertion.Length) : oldValue;
    }

    /// <summary>A minus sign toggles the sign wherever the caret is; a plus removes it. The selection moves with the text.</summary>
    private SkUiTextEditValue ToggleSign(bool plus, SkUiTextEditValue oldValue, SkUiNumericCulture symbols)
    {
        var old = oldValue.Text;
        var negative = old.StartsWith(Minus, StringComparison.Ordinal);
        if (plus ? !negative : !entry.AllowsNegative)
            return oldValue;
        var candidate = negative ? old[Minus.Length..] : Minus + old;
        if (!IsValid(candidate, symbols))
            return oldValue;
        var shift = negative ? -Minus.Length : Minus.Length;
        var lowest = negative ? 0 : Minus.Length;
        return new SkUiTextEditValue(candidate, Math.Clamp(oldValue.SelectionStart + shift, lowest, candidate.Length),
            Math.Clamp(oldValue.SelectionEnd + shift, lowest, candidate.Length));
    }

    /// <summary>Pasted (or committed) text, cleaned of what numbers are written with besides digits.</summary>
    private SkUiTextEditValue Pasted(string inserted, SkUiTextEditValue oldValue, string before, string after, SkUiNumericCulture symbols)
    {
        var body = new StringBuilder(inserted.Length);
        var negative = false;
        var groupIsSeparator = symbols.GroupSeparator.Length > 0 && symbols.GroupSeparator != symbols.DecimalSeparator;
        for (var index = 0; index < inserted.Length; index++)
        {
            var character = inserted[index];
            if (char.IsDigit(character))
            {
                body.Append((char)('0' + (int)char.GetNumericValue(character)));
                continue;
            }
            if (char.IsWhiteSpace(character) || char.GetUnicodeCategory(character) == UnicodeCategory.Format || character is '\'' or '’')
                continue;
            if (groupIsSeparator && string.CompareOrdinal(inserted, index, symbols.GroupSeparator, 0, symbols.GroupSeparator.Length) == 0
                && GroupsDigits(inserted, index + symbols.GroupSeparator.Length))
            {
                index += symbols.GroupSeparator.Length - 1;
                continue;
            }
            if (Skip(inserted, index, symbols.CurrencySymbol) is > 0 and var currency)
            {
                index += currency - 1;
                continue;
            }
            if (Skip(inserted, index, symbols.PercentSymbol) is > 0 and var percent)
            {
                index += percent - 1;
                continue;
            }
            if (Skip(inserted, index, symbols.PerMilleSymbol) is > 0 and var perMille)
            {
                index += perMille - 1;
                continue;
            }
            if (symbols.IsDecimalKey(character))
            {
                body.Append(symbols.DecimalSeparator);
                continue;
            }
            // A sign only before the digits.
            if ((SkUiNumericCulture.IsMinus(character) || character == '+') && body.Length == 0 && !negative)
            {
                negative = character != '+';
                continue;
            }
            return oldValue;
        }
        (before, after) = AfterSign(before, after);
        if (negative)
        {
            if (!entry.AllowsNegative || (before.Length > 0 && before != Minus))
                return oldValue;
            before = Minus;
        }
        var insertion = body.ToString();
        if (insertion.StartsWith(symbols.DecimalSeparator, StringComparison.Ordinal) && !HasDigit(before))
            insertion = "0" + insertion;
        var candidate = before + insertion + after;
        return IsValid(candidate, symbols) ? new SkUiTextEditValue(candidate, candidate.Length - after.Length) : oldValue;
    }

    /// <summary>
    /// Whether a group separator before <paramref name="digits"/> groups: exactly three digits follow it, then no digit. Else it is
    /// read as a decimal key (<c>3.14</c> pasted in de-DE is 3,14, not 314) or refuses the text.
    /// </summary>
    private static bool GroupsDigits(string text, int digits)
    {
        var end = digits;
        while (end < text.Length && char.IsDigit(text[end]))
            end++;
        return end - digits == 3;
    }

    private static int Skip(string text, int index, string symbol) =>
        symbol.Length > 0 && string.CompareOrdinal(text, index, symbol, 0, symbol.Length) == 0 ? symbol.Length : 0;

    // Nothing goes before the sign: an insertion at the start of a negative number goes after it.
    private static (string Before, string After) AfterSign(string before, string after) =>
        before.Length == 0 && after.StartsWith(Minus, StringComparison.Ordinal) ? (Minus, after[Minus.Length..]) : (before, after);

    private static bool HasDigit(string text)
    {
        foreach (var character in text)
            if (char.IsAsciiDigit(character))
                return true;
        return false;
    }

    /// <summary>
    /// Whether <paramref name="text"/> is a number being typed: an optional minus (when allowed), digits, at most one separator and
    /// the allowed decimal digits after it, and (with digits) a value the type can hold.
    /// </summary>
    private bool IsValid(string text, SkUiNumericCulture symbols)
    {
        var index = 0;
        if (text.StartsWith(Minus, StringComparison.Ordinal))
        {
            if (!entry.AllowsNegative)
                return false;
            index = Minus.Length;
        }
        var separator = symbols.DecimalSeparator;
        var decimals = -1;
        var digits = false;
        while (index < text.Length)
        {
            if (char.IsAsciiDigit(text[index]))
            {
                digits = true;
                if (decimals >= 0)
                    decimals++;
                index++;
            }
            else if (decimals < 0 && string.CompareOrdinal(text, index, separator, 0, separator.Length) == 0)
            {
                decimals = 0;
                index += separator.Length;
            }
            else
            {
                return false;
            }
        }
        if (decimals >= 0 && entry.DecimalDigits is { } allowed && (allowed == 0 || decimals > allowed))
            return false;
        return !digits || entry.Fits(text);
    }

    private static int CommonPrefix(string a, string b)
    {
        var length = Math.Min(a.Length, b.Length);
        var index = 0;
        while (index < length && a[index] == b[index])
            index++;
        return index;
    }

    private static int CommonSuffix(string a, string b)
    {
        var length = Math.Min(a.Length, b.Length);
        var count = 0;
        while (count < length && a[a.Length - 1 - count] == b[b.Length - 1 - count])
            count++;
        return count;
    }
}
