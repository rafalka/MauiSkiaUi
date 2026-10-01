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
        double number => !(number < 0),
        int number => number >= 0,
        _ => false
    };

    public static bool Finite(BindableObject bindable, object? value) => value is double number && double.IsFinite(number);

    public static bool FinitePositive(BindableObject bindable, object? value) => value is double number && double.IsFinite(number) && number > 0;

    public static bool CornerRadii(BindableObject bindable, object? value) => value is CornerRadius radii && SkUiCornerRadii.IsValid(radii);

    /// <summary>Throws <see cref="ArgumentOutOfRangeException"/> unless <paramref name="value"/> is finite.</summary>
    public static void ThrowIfNotFinite(double value, string name)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(name, value, "The value must be finite.");
    }

    /// <summary>Throws <see cref="ArgumentOutOfRangeException"/> unless <paramref name="value"/> is finite and positive.</summary>
    public static void ThrowIfNotFinitePositive(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0)
            throw new ArgumentOutOfRangeException(name, value, "The value must be finite and positive.");
    }
}
