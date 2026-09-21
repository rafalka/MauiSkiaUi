namespace MauiSkiaUi.Core;

/// <summary>
/// Track size for Core grid rows/columns: absolute DIPs, Auto, or weighted star (<c>*</c>).
/// Owned by Core so layouts do not depend on MAUI Controls types.
/// </summary>
public readonly struct SkUiCoreGridLength : IEquatable<SkUiCoreGridLength>
{
    /// <summary>One Auto track.</summary>
    public static SkUiCoreGridLength Auto { get; } = new(1, SkUiCoreGridUnitType.Auto);

    /// <summary>One star (<c>*</c>) track.</summary>
    public static SkUiCoreGridLength Star { get; } = new(1, SkUiCoreGridUnitType.Star);

    /// <summary>Creates an absolute length of <paramref name="value"/> DIPs.</summary>
    public SkUiCoreGridLength(double value) : this(value, SkUiCoreGridUnitType.Absolute) { }

    /// <summary>Creates a length with the given unit and weight/value.</summary>
    public SkUiCoreGridLength(double value, SkUiCoreGridUnitType type)
    {
        if (type == SkUiCoreGridUnitType.Absolute)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));
        }
        else if (value <= 0 || double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Auto/Star weight must be a positive finite number.");
        }

        Value = value;
        GridUnitType = type;
    }

    /// <summary>Absolute DIPs, or star weight (ignored for Auto).</summary>
    public double Value { get; }

    /// <summary>Absolute, Auto, or Star.</summary>
    public SkUiCoreGridUnitType GridUnitType { get; }

    /// <summary>True when the track is absolute DIPs.</summary>
    public bool IsAbsolute => GridUnitType == SkUiCoreGridUnitType.Absolute;

    /// <summary>True when the track sizes to content.</summary>
    public bool IsAuto => GridUnitType == SkUiCoreGridUnitType.Auto;

    /// <summary>True when the track is a star share.</summary>
    public bool IsStar => GridUnitType == SkUiCoreGridUnitType.Star;

    /// <inheritdoc />
    public bool Equals(SkUiCoreGridLength other) =>
        Value.Equals(other.Value) && GridUnitType == other.GridUnitType;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SkUiCoreGridLength other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Value, GridUnitType);

    /// <inheritdoc />
    public override string ToString() => GridUnitType switch
    {
        SkUiCoreGridUnitType.Auto => "Auto",
        SkUiCoreGridUnitType.Star => Value == 1 ? "*" : $"{Value}*",
        _ => Value.ToString("0.##")
    };

    /// <summary>Equality.</summary>
    public static bool operator ==(SkUiCoreGridLength left, SkUiCoreGridLength right) => left.Equals(right);

    /// <summary>Inequality.</summary>
    public static bool operator !=(SkUiCoreGridLength left, SkUiCoreGridLength right) => !left.Equals(right);
}
