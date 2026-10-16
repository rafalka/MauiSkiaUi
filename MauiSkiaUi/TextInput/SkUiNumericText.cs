using System.Globalization;
using System.Numerics;

namespace MauiSkiaUi;

/// <summary>
/// What a numeric entry reads and writes numbers with in one culture: its decimal separator, group separator and symbols, and the
/// number format of the plain text shown while the user edits (the culture's separator, no group separators, an ASCII minus sign
/// whatever the culture's, so a typed <c>-</c> always fits).
/// </summary>
internal sealed class SkUiNumericCulture
{
    // The culture last asked for: an entry asks on every edit, and nearly always for the same culture.
    private static SkUiNumericCulture? _last;

    private SkUiNumericCulture(CultureInfo culture)
    {
        Culture = culture;
        var format = culture.NumberFormat;
        DecimalSeparator = string.IsNullOrEmpty(format.NumberDecimalSeparator) ? "." : format.NumberDecimalSeparator;
        GroupSeparator = format.NumberGroupSeparator;
        CurrencySymbol = format.CurrencySymbol;
        PercentSymbol = format.PercentSymbol;
        PerMilleSymbol = format.PerMilleSymbol;
        var edit = (NumberFormatInfo)format.Clone();
        edit.NegativeSign = "-";
        edit.PositiveSign = "+";
        edit.NumberDecimalSeparator = DecimalSeparator;
        EditFormat = NumberFormatInfo.ReadOnly(edit);
    }

    /// <summary>The symbols of <paramref name="culture"/>.</summary>
    public static SkUiNumericCulture For(CultureInfo culture)
    {
        var last = _last;
        if (last is not null && ReferenceEquals(last.Culture, culture))
            return last;
        return _last = new SkUiNumericCulture(culture);
    }

    public CultureInfo Culture { get; }

    /// <summary>The plain number's format while editing: the culture's, with <c>-</c> and <c>+</c> as signs.</summary>
    public NumberFormatInfo EditFormat { get; }

    public string DecimalSeparator { get; }
    public string GroupSeparator { get; }
    public string CurrencySymbol { get; }
    public string PercentSymbol { get; }
    public string PerMilleSymbol { get; }

    /// <summary>Whether <paramref name="character"/> is typed as the decimal separator: <c>.</c> and <c>,</c> from any number pad, the Arabic one, the culture's.</summary>
    public bool IsDecimalKey(char character) =>
        character is '.' or ',' or '٫' || (DecimalSeparator.Length == 1 && DecimalSeparator[0] == character);

    /// <summary>Whether <paramref name="character"/> is a minus sign: ASCII, the Unicode minus, a hyphen, a dash.</summary>
    public static bool IsMinus(char character) => character is '-' or '−' or '‐' or '‑' or '‒' or '–' or '﹣' or '－';
}

/// <summary>
/// A numeric entry's numbers as text (D16), in generic math: the plain number shown while editing and parsed back, the formatted
/// value shown otherwise, and rounding to a number of decimal digits.
/// </summary>
internal static class SkUiNumericText<T> where T : struct, INumber<T>, IMinMaxValue<T>
{
    /// <summary>Whether <typeparamref name="T"/> holds whole numbers only (no decimal separator).</summary>
    public static bool IsInteger => !SkUiStepperMath<T>.IsBinaryFloatingPoint && typeof(T) != typeof(decimal);

    private static NumberStyles Styles => IsInteger
        ? NumberStyles.AllowLeadingSign | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite
        : NumberStyles.Float;

    /// <summary>
    /// The plain number the user edits: the culture's decimal separator, no group separators, <c>-</c> for negatives, no exponent
    /// (a binary floating-point value that would print one is written out through <see cref="decimal"/> when it fits).
    /// </summary>
    public static string Raw(T value, SkUiNumericCulture symbols)
    {
        var format = symbols.EditFormat;
        if (typeof(T) == typeof(decimal))
            return value.ToString("G29", format);
        var text = value.ToString(null, format);
        if (!SkUiStepperMath<T>.IsBinaryFloatingPoint || text.IndexOfAny(['E', 'e']) < 0)
            return text;
        var number = double.CreateTruncating(value);
        return Math.Abs(number) < 7.9e28 ? ((decimal)number).ToString("G29", format) : text;
    }

    /// <summary>The value as shown while the entry is not edited: <paramref name="format"/> (a .NET numeric format) in the culture.</summary>
    public static string Display(T value, string? format, CultureInfo culture)
    {
        if (string.IsNullOrEmpty(format))
            return value.ToString(null, culture);
        try
        {
            return value.ToString(format, culture);
        }
        catch (FormatException)
        {
            // An invalid format string shows the plain value (as a binding's StringFormat would not throw at the user).
            return value.ToString(null, culture);
        }
    }

    /// <summary>
    /// Parses the plain number (the edit text, or text the app set): any minus sign, the culture's decimal separator, white space
    /// around it. False for text that is no finite <typeparamref name="T"/> (also past the type's range).
    /// </summary>
    public static bool TryParse(string text, SkUiNumericCulture symbols, out T value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        Span<char> buffer = text.Length <= 128 ? stackalloc char[text.Length] : new char[text.Length];
        var length = 0;
        foreach (var character in text)
        {
            // Bidi marks (an Arabic culture's sign) and spaces are not part of the number.
            if (char.GetUnicodeCategory(character) == UnicodeCategory.Format)
                continue;
            buffer[length++] = SkUiNumericCulture.IsMinus(character) ? '-' : character;
        }
        if (!T.TryParse(buffer[..length], Styles, symbols.EditFormat, out value))
            return false;
        return T.IsFinite(value);
    }

    /// <summary><paramref name="value"/> rounded to <paramref name="digits"/> decimal digits (half away from zero); integers stay.</summary>
    public static T Round(T value, int digits)
    {
        if (IsInteger)
            return value;
        if (typeof(T) == typeof(decimal))
            return T.CreateTruncating(Math.Round(decimal.CreateTruncating(value), Math.Min(digits, 28), MidpointRounding.AwayFromZero));
        return T.CreateTruncating(Math.Round(double.CreateTruncating(value), Math.Min(digits, 15), MidpointRounding.AwayFromZero));
    }
}
