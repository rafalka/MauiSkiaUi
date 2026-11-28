using System.Numerics;
using System.Runtime.CompilerServices;

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
        TimeSpan time => time >= TimeSpan.Zero,
        _ => false
    };

    public static bool Finite(BindableObject bindable, object? value) => value is double number && double.IsFinite(number);

    public static bool FinitePositive(BindableObject bindable, object? value) => value is double number && double.IsFinite(number) && number > 0;

    /// <summary>A size in DIPs, or -1 for the look's.</summary>
    public static bool NonNegativeOrLook(BindableObject bindable, object? value) => value is double number && IsNonNegativeOrLook(number);

    /// <summary>Whether <paramref name="value"/> is finite and not negative, or -1 (the look's).</summary>
    public static bool IsNonNegativeOrLook(double value) => double.IsFinite(value) && (value >= 0 || value == -1); // exact: -1 is a sentinel

    /// <summary>Throws unless <paramref name="value"/> is finite and not negative, or -1 (the look's).</summary>
    public static void ThrowIfNotNonNegativeOrLook(double value, string name)
    {
        if (!IsNonNegativeOrLook(value))
            throw new ArgumentOutOfRangeException(name, value, "Must be a finite, non-negative number, or -1 for the look's.");
    }

    /// <summary>Whether <paramref name="value"/> is a combination of MAUI's four swipe directions (none included).</summary>
    public static bool IsSwipeDirections(SwipeDirection value) =>
        (value & ~(SwipeDirection.Left | SwipeDirection.Right | SwipeDirection.Up | SwipeDirection.Down)) == 0;

    /// <summary>Throws unless <paramref name="value"/> is a combination of the four swipe directions.</summary>
    public static void ThrowIfNotSwipeDirections(SwipeDirection value, string name)
    {
        if (!IsSwipeDirections(value))
            throw new ArgumentOutOfRangeException(name, value, "Expected a combination of Left, Right, Up and Down.");
    }

    /// <summary>A row limit: -1 (none) or at least 1.</summary>
    public static bool RowLimit(BindableObject bindable, object? value) => value is int rows && (rows == -1 || rows >= 1);

    /// <summary>Throws unless <paramref name="value"/> is -1 (no limit) or at least 1.</summary>
    public static void ThrowIfNotRowLimit(int value, string name)
    {
        if (value != -1 && value < 1)
            throw new ArgumentOutOfRangeException(name, value, "Expected -1 (no limit) or at least 1.");
    }

    /// <summary>A scale up: finite, at least 1.</summary>
    public static bool AtLeastOne(BindableObject bindable, object? value) => value is double number && double.IsFinite(number) && number >= 1;

    /// <summary>A scale down to a fraction: more than 0, at most 1.</summary>
    public static bool Fraction(BindableObject bindable, object? value) => value is double number && number is > 0 and <= 1;

    public static bool CornerRadii(BindableObject bindable, object? value) => value is CornerRadius radii && SkUiCornerRadii.IsValid(radii);

    /// <summary><c>null</c> (the look's), or a value <see cref="NonNegative"/> takes.</summary>
    public static bool OptionalNonNegative(BindableObject bindable, object? value) => value is null || NonNegative(bindable, value);

    /// <summary><c>null</c> (the look's), or radii <see cref="CornerRadii"/> takes.</summary>
    public static bool OptionalCornerRadii(BindableObject bindable, object? value) => value is null || CornerRadii(bindable, value);

    /// <summary><c>null</c> (the look's), or a thickness whose sides are finite and not negative.</summary>
    public static bool OptionalThickness(BindableObject bindable, object? value) => value is null || value is Thickness thickness && IsThickness(thickness);

    /// <summary>A thickness with finite, non-negative sides.</summary>
    public static bool ThicknessValue(BindableObject bindable, object? value) => value is Thickness thickness && IsThickness(thickness);

    /// <summary>Whether every side of <paramref name="thickness"/> is finite and not negative.</summary>
    public static bool IsThickness(Thickness thickness) =>
        IsLength(thickness.Left) && IsLength(thickness.Top) && IsLength(thickness.Right) && IsLength(thickness.Bottom);

    private static bool IsLength(double value) => double.IsFinite(value) && value >= 0;

    /// <summary>Throws <see cref="ArgumentOutOfRangeException"/> unless <paramref name="value"/> is <c>null</c> or a thickness <see cref="OptionalThickness"/> takes.</summary>
    public static void ThrowIfNotThickness(Thickness? value, string name)
    {
        if (value is { } thickness && !IsThickness(thickness))
            throw new ArgumentOutOfRangeException(name, thickness, "Every side must be finite and not negative.");
    }

    /// <summary>A finite number of type <typeparamref name="T"/> (integers and <see cref="decimal"/> always are).</summary>
    public static bool FiniteNumber<T>(BindableObject bindable, object? value) where T : struct, INumber<T> => value is T number && T.IsFinite(number);

    /// <summary>A finite number of type <typeparamref name="T"/> above zero.</summary>
    public static bool PositiveNumber<T>(BindableObject bindable, object? value) where T : struct, INumber<T> =>
        value is T number && T.IsFinite(number) && number > T.Zero;

    /// <summary>An autofill hint: <c>null</c> or empty (none), or one hint without white space (white space is reserved for a list of hints).</summary>
    public static bool AutofillHint(BindableObject bindable, object? value) => value is null || value is string hint && IsAutofillHint(hint);

    /// <summary>Whether <paramref name="hint"/> is no hint (empty) or one: no white space.</summary>
    public static bool IsAutofillHint(string hint)
    {
        foreach (var character in hint)
            if (char.IsWhiteSpace(character))
                return false;
        return true;
    }

    /// <summary>Throws <see cref="ArgumentException"/> unless <paramref name="value"/> is <c>null</c> or one hint (the rule of <see cref="AutofillHint"/>).</summary>
    public static void ThrowIfNotAutofillHint(string? value, string name)
    {
        if (value is not null && !IsAutofillHint(value))
            throw new ArgumentException("An autofill hint has no white space (it is reserved for a list of hints).", name);
    }

    /// <summary>A press scale: -1 (the look's) or finite and more than 0.</summary>
    public static bool PressedScale(BindableObject bindable, object? value) => value is double scale && IsPressedScale(scale);

    /// <summary>Whether <paramref name="scale"/> is -1 (the look's) or finite and more than 0.</summary>
    public static bool IsPressedScale(double scale) => scale == -1 || double.IsFinite(scale) && scale > 0; // exact: -1 is a sentinel

    /// <summary>Throws unless <paramref name="value"/> is -1 (the look's) or finite and more than 0.</summary>
    public static void ThrowIfNotPressedScale(double value, string name)
    {
        if (!IsPressedScale(value))
            throw new ArgumentOutOfRangeException(name, value, "Expected -1 (the look's) or a finite scale more than 0.");
    }

    /// <summary>A defined value of <typeparamref name="TEnum"/> (for a flags enum, one defined value: use <see cref="Flags{TEnum}"/> for combinations).</summary>
    public static bool Defined<TEnum>(BindableObject bindable, object? value) where TEnum : struct, Enum => value is TEnum defined && Enum.IsDefined(defined);

    /// <summary>A combination of the defined flags of <typeparamref name="TEnum"/>, none included.</summary>
    public static bool Flags<TEnum>(BindableObject bindable, object? value) where TEnum : struct, Enum => value is TEnum flags && IsFlags(flags);

    /// <summary><c>null</c> (not set), or a value <see cref="Flags{TEnum}"/> takes.</summary>
    public static bool OptionalFlags<TEnum>(BindableObject bindable, object? value) where TEnum : struct, Enum => value is null || Flags<TEnum>(bindable, value);

    /// <summary>Whether <paramref name="value"/> has no bit outside the defined flags of <typeparamref name="TEnum"/>.</summary>
    public static bool IsFlags<TEnum>(TEnum value) where TEnum : struct, Enum => (Bits(value) & ~FlagMask<TEnum>.Value) == 0;

    /// <summary>Throws <see cref="ArgumentOutOfRangeException"/> unless <paramref name="value"/> is defined (the rule of <see cref="Defined{TEnum}"/>).</summary>
    public static void ThrowIfNotDefined<TEnum>(TEnum value, string name) where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(name, value, $"Not a defined {typeof(TEnum).Name}.");
    }

    /// <summary>Throws <see cref="ArgumentOutOfRangeException"/> unless <paramref name="value"/> combines defined flags (the rule of <see cref="Flags{TEnum}"/>).</summary>
    public static void ThrowIfNotFlags<TEnum>(TEnum value, string name) where TEnum : struct, Enum
    {
        if (!IsFlags(value))
            throw new ArgumentOutOfRangeException(name, value, $"Not a combination of the defined {typeof(TEnum).Name} flags.");
    }

    /// <summary>Throws <see cref="ArgumentOutOfRangeException"/> unless <paramref name="value"/> is <c>null</c> or combines defined flags (the rule of <see cref="OptionalFlags{TEnum}"/>).</summary>
    public static void ThrowIfNotFlags<TEnum>(TEnum? value, string name) where TEnum : struct, Enum
    {
        if (value is { } flags)
            ThrowIfNotFlags(flags, name);
    }

    /// <summary>The bits of an enum value, read as the unsigned integer of its size (as the mask is, so signed values compare too).</summary>
    private static ulong Bits<TEnum>(TEnum value) where TEnum : struct, Enum => Unsafe.SizeOf<TEnum>() switch
    {
        1 => Unsafe.As<TEnum, byte>(ref value),
        2 => Unsafe.As<TEnum, ushort>(ref value),
        4 => Unsafe.As<TEnum, uint>(ref value),
        _ => Unsafe.As<TEnum, ulong>(ref value)
    };

    /// <summary>Every defined flag of <typeparamref name="TEnum"/> together, computed once per enum.</summary>
    private static class FlagMask<TEnum> where TEnum : struct, Enum
    {
        public static readonly ulong Value = Enum.GetValues<TEnum>().Aggregate(0UL, (mask, flag) => mask | Bits(flag));
    }

    /// <summary>A defined <see cref="StackOrientation"/> (horizontal or vertical).</summary>
    public static bool Orientation(BindableObject bindable, object? value) => value is StackOrientation orientation && Enum.IsDefined(orientation);

    /// <summary>A defined <see cref="SkUiPressFeedback"/>.</summary>
    public static bool PressFeedback(BindableObject bindable, object? value) => value is SkUiPressFeedback feedback && Enum.IsDefined(feedback);

    /// <summary>A defined <see cref="SkUiPanAxis"/>.</summary>
    public static bool PanAxis(BindableObject bindable, object? value) => value is SkUiPanAxis axis && Enum.IsDefined(axis);

    /// <summary>A defined <see cref="LayoutAlignment"/>.</summary>
    public static bool Alignment(BindableObject bindable, object? value) => value is LayoutAlignment alignment && Enum.IsDefined(alignment);

    /// <summary>A defined <see cref="SkUiGridResizeBehavior"/>.</summary>
    public static bool ResizeBehavior(BindableObject bindable, object? value) => value is SkUiGridResizeBehavior behavior && Enum.IsDefined(behavior);

    /// <summary>A defined <see cref="SkUiGridResizeDirection"/>.</summary>
    public static bool ResizeDirection(BindableObject bindable, object? value) => value is SkUiGridResizeDirection direction && Enum.IsDefined(direction);

    /// <summary>A size limit: not negative, possibly infinite (no limit).</summary>
    public static bool SizeLimit(BindableObject bindable, object? value) => value is double number && !double.IsNaN(number) && number >= 0;

    /// <summary>A defined <see cref="Microsoft.Maui.Controls.Shapes.PenLineCap"/>.</summary>
    public static bool LineCap(BindableObject bindable, object? value) => value is Microsoft.Maui.Controls.Shapes.PenLineCap cap && Enum.IsDefined(cap);

    /// <summary>A defined <see cref="SkUiCenterStrategy"/>.</summary>
    public static bool CenterStrategy(BindableObject bindable, object? value) => value is SkUiCenterStrategy strategy && Enum.IsDefined(strategy);

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
