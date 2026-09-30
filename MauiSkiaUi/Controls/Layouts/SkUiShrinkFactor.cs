using System.ComponentModel;
using System.Globalization;

namespace MauiSkiaUi;

/// <summary>
/// How a child of a shrink layout (<see cref="SkUiShrinkLayout"/>, <see cref="Core.SkUiCoreShrinkLayout"/>) gives up
/// main-axis space when the children do not fit:
/// <list type="bullet">
/// <item><see cref="None"/> (the default): never shrinks.</item>
/// <item><see cref="Auto"/>: shrinks only when its natural size is above the average of the visible children; its
/// share of the overflow is proportional to its natural size.</item>
/// <item>A positive factor: always shrinks; its share of the overflow is proportional to factor × natural size
/// (as CSS <c>flex-shrink</c>), so 2 gives up twice as much relative to its size as 1.</item>
/// </list>
/// No child shrinks below its minimum size (<c>MinimumWidthRequest</c> / <c>MinimumHeightRequest</c>, or 0).
/// In XAML: <c>"None"</c>, <c>"Auto"</c> or a number.
/// </summary>
[TypeConverter(typeof(SkUiShrinkFactorTypeConverter))]
public readonly struct SkUiShrinkFactor : IEquatable<SkUiShrinkFactor>
{
    private readonly double _value;
    private readonly bool _isAuto;

    private SkUiShrinkFactor(double value, bool isAuto)
    {
        _value = value;
        _isAuto = isAuto;
    }

    /// <summary>Creates an explicit factor (≥ 0; 0 is <see cref="None"/>).</summary>
    public SkUiShrinkFactor(double value)
    {
        if (!(value >= 0) || double.IsPositiveInfinity(value))
            throw new ArgumentOutOfRangeException(nameof(value), value, "A shrink factor is a finite number >= 0.");
        _value = value;
        _isAuto = false;
    }

    /// <summary>Never shrinks (factor 0), the default.</summary>
    public static SkUiShrinkFactor None => default;

    /// <summary>Shrinks only when larger than the average child, in proportion to its natural size.</summary>
    public static SkUiShrinkFactor Auto { get; } = new(1, isAuto: true);

    /// <summary>Whether this is <see cref="Auto"/>.</summary>
    public bool IsAuto => _isAuto;

    /// <summary>Whether this is <see cref="None"/> (never shrinks).</summary>
    public bool IsNone => !_isAuto && _value == 0;

    /// <summary>The factor: 0 for <see cref="None"/>, 1 for <see cref="Auto"/> (which also applies the average rule).</summary>
    public double Value => _value;

    /// <summary>An explicit factor.</summary>
    public static implicit operator SkUiShrinkFactor(double value) => new(value);

    /// <summary>Parses <c>"None"</c>, <c>"Auto"</c> or an invariant-culture number.</summary>
    public static SkUiShrinkFactor Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var value = text.Trim();
        if (value.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            return Auto;
        if (value.Equals("None", StringComparison.OrdinalIgnoreCase))
            return None;
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var factor))
            return new SkUiShrinkFactor(factor);
        throw new FormatException($"'{text}' is not a shrink factor: use None, Auto or a number >= 0.");
    }

    /// <inheritdoc />
    public bool Equals(SkUiShrinkFactor other) => _isAuto == other._isAuto && _value.Equals(other._value);
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SkUiShrinkFactor other && Equals(other);
    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(_isAuto, _value);
    /// <summary>Equality.</summary>
    public static bool operator ==(SkUiShrinkFactor left, SkUiShrinkFactor right) => left.Equals(right);
    /// <summary>Inequality.</summary>
    public static bool operator !=(SkUiShrinkFactor left, SkUiShrinkFactor right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => _isAuto ? "Auto" : _value == 0 ? "None" : _value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>XAML / string conversion for <see cref="SkUiShrinkFactor"/>: <c>"None"</c>, <c>"Auto"</c> or a number.</summary>
public sealed class SkUiShrinkFactorTypeConverter : TypeConverter
{
    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || sourceType == typeof(double) || base.CanConvertFrom(context, sourceType);

    /// <inheritdoc />
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) =>
        destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) => value switch
    {
        string text => SkUiShrinkFactor.Parse(text),
        double factor => new SkUiShrinkFactor(factor),
        _ => base.ConvertFrom(context, culture, value)
    };

    /// <inheritdoc />
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType) =>
        destinationType == typeof(string) && value is SkUiShrinkFactor factor
            ? factor.ToString()
            : base.ConvertTo(context, culture, value, destinationType);
}
