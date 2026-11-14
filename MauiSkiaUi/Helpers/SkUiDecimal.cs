using System.Globalization;

namespace MauiSkiaUi;

/// <summary>
/// <see cref="double"/> to <see cref="decimal"/> as a person reads the number: rounded to 15 significant digits, so 0.1 becomes
/// 0.1m. .NET 10's conversion does this; .NET 11's is exact (0.1 becomes 0.1000000000000000055511151231), which would show and
/// store the binary error of every value a double carries into a decimal (a screen reader setting a decimal stepper, a numeric
/// entry writing out a small double). Both target lines give the same result through here.
/// </summary>
internal static class SkUiDecimal
{
    /// <summary>The decimal of a finite <paramref name="value"/>, clamped to <see cref="decimal"/>'s range.</summary>
    public static decimal FromDouble(double value)
    {
        if (value >= (double)decimal.MaxValue)
            return decimal.MaxValue;
        if (value <= (double)decimal.MinValue)
            return decimal.MinValue;
        // "G15" never needs more than 22 characters ("-1.23456789012345E-308"); parsing rounds what decimal can't hold to 0.
        Span<char> text = stackalloc char[32];
        value.TryFormat(text, out var length, "G15", CultureInfo.InvariantCulture);
        return decimal.Parse(text[..length], NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}
