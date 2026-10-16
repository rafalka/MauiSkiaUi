using System.Numerics;
using System.Runtime.InteropServices;

namespace MauiSkiaUi;

/// <summary>
/// The value rules of the typed steppers (<see cref="SkUiStepper{T}"/>, <see cref="Core.SkUiCoreStepper{T}"/>) in generic math:
/// MAUI's rounding by the increment for binary floating point (<see cref="float"/>, <see cref="double"/>, <see cref="Half"/>,
/// <see cref="NFloat"/>), exact <see cref="decimal"/> and integer values, and steps that stop at the range's ends instead of
/// overflowing. The double steppers use its rounding too.
/// </summary>
internal static class SkUiStepperMath<T> where T : struct, INumber<T>, IMinMaxValue<T>
{
    // A constant per instantiation (each value type gets its own code, also under Native AOT): the checks below cost nothing.
    internal static bool IsBinaryFloatingPoint =>
        typeof(T) == typeof(double) || typeof(T) == typeof(float) || typeof(T) == typeof(Half) || typeof(T) == typeof(NFloat);

    /// <summary>The default <c>Maximum</c>, 100 as MAUI's (it fits every numeric type, <see cref="sbyte"/> included).</summary>
    public static readonly T DefaultMaximum = T.CreateChecked(100);

    /// <summary>
    /// The digits values are rounded to after an increment: 4 significant digits after its most significant one, as MAUI's stepper
    /// (binary floating point only; other types are exact).
    /// </summary>
    public static int Digits(T increment) =>
        IsBinaryFloatingPoint ? (int)Math.Clamp(-Math.Log10(double.CreateTruncating(increment)) + 4, 1, 15) : 0;

    /// <summary>The value rounded (binary floating point) and kept within the range.</summary>
    public static T Coerce(T value, T minimum, T maximum, int digits) => T.Clamp(Round(value, digits), minimum, maximum);

    private static T Round(T value, int digits) =>
        IsBinaryFloatingPoint ? T.CreateTruncating(Math.Round(double.CreateTruncating(value), digits)) : value;

    /// <summary>Whether the value can move down (-1) or up (+1).</summary>
    public static bool CanStep(T value, T minimum, T maximum, int direction) => direction < 0 ? value > minimum : value < maximum;

    /// <summary>
    /// One step down (-1) or up (+1), stopping at the range's end: a step past the type's own limits (a <see cref="uint"/> below 0,
    /// <see cref="int.MaxValue"/> + 1) lands on the range's end instead of wrapping or throwing.
    /// </summary>
    public static T Step(T value, T increment, T minimum, T maximum, int direction) => direction < 0
        ? value < T.MinValue + increment ? minimum : T.Max(value - increment, minimum)
        : value > T.MaxValue - increment ? maximum : T.Min(value + increment, maximum);
}
