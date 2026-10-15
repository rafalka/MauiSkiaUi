using System.Numerics;

namespace MauiSkiaUi;

/// <summary>
/// Value checks of the drawn controls' bindable properties, used twice: as the property's <c>validateValue</c> (MAUI then
/// ignores an invalid value on every path, XAML, bindings, styles and the property setter, with a logged warning) and
/// by the fluent <c>Set*</c> setter, which throws first. Invalid values never reach the store.
/// </summary>
internal static class SkUiValidate
{
    public static bool NotNull(BindableObject bindable, object? value) => value is not null;

    public static bool NonNegative(BindableObject bindable, object? value) => value switch
    {
        double number => double.IsFinite(number) && number >= 0,
        int number => number >= 0,
        _ => false
    };

    public static bool Finite(BindableObject bindable, object? value) => value is double number && double.IsFinite(number);

    public static bool FinitePositive(BindableObject bindable, object? value) => value is double number && double.IsFinite(number) && number > 0;

    /// <summary>A scale up: finite, at least 1.</summary>
    public static bool AtLeastOne(BindableObject bindable, object? value) => value is double number && double.IsFinite(number) && number >= 1;

    /// <summary>A scale down to a fraction: more than 0, at most 1.</summary>
    public static bool Fraction(BindableObject bindable, object? value) => value is double number && number is > 0 and <= 1;

    public static bool CornerRadii(BindableObject bindable, object? value) => value is CornerRadius radii && SkUiCornerRadii.IsValid(radii);

    /// <summary>A finite number of type <typeparamref name="T"/> (integers and <see cref="decimal"/> always are).</summary>
    public static bool FiniteNumber<T>(BindableObject bindable, object? value) where T : struct, INumber<T> => value is T number && T.IsFinite(number);

    /// <summary>A finite number of type <typeparamref name="T"/> above zero.</summary>
    public static bool PositiveNumber<T>(BindableObject bindable, object? value) where T : struct, INumber<T> =>
        value is T number && T.IsFinite(number) && number > T.Zero;

    /// <summary>A defined <see cref="StackOrientation"/> (horizontal or vertical).</summary>
    public static bool Orientation(BindableObject bindable, object? value) => value is StackOrientation orientation && Enum.IsDefined(orientation);

    /// <summary>Throws <see cref="ArgumentOutOfRangeException"/> unless <paramref name="value"/> is finite and not negative (the rule of <see cref="NonNegative"/>).</summary>
    public static void ThrowIfNegativeOrNotFinite(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0)
            throw new ArgumentOutOfRangeException(name, value, "The value must be finite and not negative.");
    }

    /// <summary>Throws <see cref="ArgumentOutOfRangeException"/> unless <paramref name="value"/> is finite.</summary>
    public static void ThrowIfNotFinite(double value, string name)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(name, value, "The value must be finite.");
    }

    /// <summary>Throws <see cref="ArgumentOutOfRangeException"/> unless <paramref name="value"/> is more than 0 and at most 1 (the rule of <see cref="Fraction"/>).</summary>
    public static void ThrowIfNotFraction(double value, string name)
    {
        if (value is not (> 0 and <= 1))
            throw new ArgumentOutOfRangeException(name, value, "The value must be more than 0 and at most 1.");
    }

    /// <summary>Throws <see cref="ArgumentOutOfRangeException"/> unless <paramref name="value"/> is finite and at least 1 (the rule of <see cref="AtLeastOne"/>).</summary>
    public static void ThrowIfNotAtLeastOne(double value, string name)
    {
        if (!double.IsFinite(value) || value < 1)
            throw new ArgumentOutOfRangeException(name, value, "The value must be finite and at least 1.");
    }

    /// <summary>Throws <see cref="ArgumentOutOfRangeException"/> unless <paramref name="value"/> is a defined orientation (the rule of <see cref="Orientation"/>).</summary>
    public static void ThrowIfNotOrientation(StackOrientation value, string name)
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(name, value, "The orientation must be horizontal or vertical.");
    }

    /// <summary>Throws <see cref="ArgumentOutOfRangeException"/> unless <paramref name="value"/> is finite (the rule of <see cref="FiniteNumber{T}"/>).</summary>
    public static void ThrowIfNotFiniteNumber<T>(T value, string name) where T : struct, INumber<T>
    {
        if (!T.IsFinite(value))
            throw new ArgumentOutOfRangeException(name, value, "The value must be finite.");
    }

    /// <summary>Throws <see cref="ArgumentOutOfRangeException"/> unless <paramref name="value"/> is finite and above zero (the rule of <see cref="PositiveNumber{T}"/>).</summary>
    public static void ThrowIfNotPositiveNumber<T>(T value, string name) where T : struct, INumber<T>
    {
        if (!T.IsFinite(value) || value <= T.Zero)
            throw new ArgumentOutOfRangeException(name, value, "The value must be finite and above zero.");
    }

    /// <summary>Throws <see cref="ArgumentOutOfRangeException"/> unless <paramref name="value"/> is finite and positive.</summary>
    public static void ThrowIfNotFinitePositive(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0)
            throw new ArgumentOutOfRangeException(name, value, "The value must be finite and positive.");
    }
}
