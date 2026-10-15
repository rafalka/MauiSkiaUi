using System.Globalization;
using System.Numerics;

namespace MauiSkiaUi.Core;

/// <summary>
/// A stepper on the Core layer for values of any number type <typeparamref name="T"/> (the twin of <see cref="SkUiStepper{T}"/>):
/// integers and <see cref="decimal"/> step exactly, binary floating point is rounded by the increment as MAUI's stepper rounds doubles,
/// and a step stops at the range's end, also at the type's limits.
/// </summary>
/// <typeparam name="T">The value's type: a number with a minimum and a maximum value (generic math).</typeparam>
/// <remarks>
/// Screen readers get the range and value as <see cref="double"/>: near the ends of <see cref="long"/>, <see cref="ulong"/> or
/// <see cref="decimal"/> they hear the nearest <see cref="double"/>, while the stepper itself stays exact.
/// </remarks>
public class SkUiCoreStepper<T> : SkUiCoreStepperBase where T : struct, INumber<T>, IMinMaxValue<T>
{
    private T _value;
    private T _minimum;
    private T _maximum = SkUiStepperMath<T>.DefaultMaximum;
    private T _increment = T.One;
    private int _digits = SkUiStepperMath<T>.Digits(T.One);
    private T _requestedValue;

    /// <summary>The value, kept within the range (and rounded by the increment for binary floating point).</summary>
    public T Value { get => _value; set => SetValue(value); }

    /// <summary>The smallest value (default 0).</summary>
    public T Minimum { get => _minimum; set => SetMinimum(value); }

    /// <summary>The largest value (default 100).</summary>
    public T Maximum { get => _maximum; set => SetMaximum(value); }

    /// <summary>How much a button changes the value (default 1); above zero.</summary>
    public T Increment { get => _increment; set => SetIncrement(value); }

    /// <summary>Raised when <see cref="Value"/> changes.</summary>
    public event EventHandler<SkUiValueChangedEventArgs<T>>? ValueChanged;

    /// <summary>Sets <see cref="Value"/> (kept within the range; the asked value comes back when the range widens).</summary>
    public SkUiCoreStepper<T> SetValue(T value)
    {
        SkUiValidate.ThrowIfNotFiniteNumber(value, nameof(value));
        _requestedValue = value;
        return ApplyValue(value);
    }

    private SkUiCoreStepper<T> ApplyValue(T value)
    {
        var old = _value;
        if (!SetProperty(ref _value, SkUiStepperMath<T>.Coerce(value, _minimum, _maximum, _digits), nameof(Value)))
            return this;
        OnValueOrRangeChanged();
        ValueChanged?.Invoke(this, new SkUiValueChangedEventArgs<T>(old, _value));
        return this;
    }

    /// <summary>Sets <see cref="Minimum"/> (not above <see cref="Maximum"/>).</summary>
    public SkUiCoreStepper<T> SetMinimum(T value)
    {
        SkUiValidate.ThrowIfNotFiniteNumber(value, nameof(value));
        if (value > _maximum)
            throw new ArgumentOutOfRangeException(nameof(value));
        if (SetProperty(ref _minimum, value, nameof(Minimum)))
            Recoerce();
        return this;
    }

    /// <summary>Sets <see cref="Maximum"/> (not below <see cref="Minimum"/>).</summary>
    public SkUiCoreStepper<T> SetMaximum(T value)
    {
        SkUiValidate.ThrowIfNotFiniteNumber(value, nameof(value));
        if (value < _minimum)
            throw new ArgumentOutOfRangeException(nameof(value));
        if (SetProperty(ref _maximum, value, nameof(Maximum)))
            Recoerce();
        return this;
    }

    private void Recoerce()
    {
        ApplyValue(_requestedValue);
        OnValueOrRangeChanged();
    }

    /// <summary>Sets <see cref="Increment"/> (above zero).</summary>
    public SkUiCoreStepper<T> SetIncrement(T value)
    {
        SkUiValidate.ThrowIfNotPositiveNumber(value, nameof(value));
        if (SetProperty(ref _increment, value, nameof(Increment)))
            _digits = SkUiStepperMath<T>.Digits(value);
        return this;
    }

    /// <summary>Sets <see cref="SkUiCoreStepperBase.Orientation"/>.</summary>
    public new SkUiCoreStepper<T> SetOrientation(StackOrientation value) { base.SetOrientation(value); return this; }

    /// <summary>Sets <see cref="SkUiCoreStepperBase.TextColor"/>.</summary>
    public new SkUiCoreStepper<T> SetTextColor(Color? value) { base.SetTextColor(value); return this; }

    /// <summary>Sets <see cref="SkUiCoreStepperBase.DecrementText"/>.</summary>
    public new SkUiCoreStepper<T> SetDecrementText(string? value) { base.SetDecrementText(value); return this; }

    /// <summary>Sets <see cref="SkUiCoreStepperBase.IncrementText"/>.</summary>
    public new SkUiCoreStepper<T> SetIncrementText(string? value) { base.SetIncrementText(value); return this; }

    /// <summary>Sets <see cref="SkUiCoreStepperBase.FontFamily"/>.</summary>
    public new SkUiCoreStepper<T> SetFontFamily(string? value) { base.SetFontFamily(value); return this; }

    /// <summary>Sets <see cref="SkUiCoreStepperBase.FontSize"/>.</summary>
    public new SkUiCoreStepper<T> SetFontSize(double value) { base.SetFontSize(value); return this; }

    /// <summary>Sets <see cref="SkUiCoreStepperBase.FontAttributes"/>.</summary>
    public new SkUiCoreStepper<T> SetFontAttributes(FontAttributes value) { base.SetFontAttributes(value); return this; }

    /// <summary>Sets <see cref="SkUiCoreStepperBase.FontAutoScalingEnabled"/>.</summary>
    public new SkUiCoreStepper<T> SetFontAutoScalingEnabled(bool value) { base.SetFontAutoScalingEnabled(value); return this; }

    /// <summary>Sets <see cref="SkUiCoreStepperBase.CornerRadius"/>.</summary>
    public new SkUiCoreStepper<T> SetCornerRadius(double value) { base.SetCornerRadius(value); return this; }

    /// <summary>Sets <see cref="SkUiCoreStepperBase.BorderWidth"/>.</summary>
    public new SkUiCoreStepper<T> SetBorderWidth(double value) { base.SetBorderWidth(value); return this; }

    /// <summary>Sets <see cref="SkUiCoreStepperBase.BorderColor"/>.</summary>
    public new SkUiCoreStepper<T> SetBorderColor(Color? value) { base.SetBorderColor(value); return this; }

    private protected override bool CanStep(int direction) => SkUiStepperMath<T>.CanStep(_value, _minimum, _maximum, direction);

    private protected override void Step(int direction) => SetValue(SkUiStepperMath<T>.Step(_value, _increment, _minimum, _maximum, direction));

    private protected override SkUiSemanticsRange SemanticsRange =>
        new(double.CreateSaturating(_minimum), double.CreateSaturating(_maximum), double.CreateSaturating(_value));

    private protected override string ValueText => _value.ToString(null, CultureInfo.CurrentCulture);

    private protected override void SetValueFromSemantics(double value)
    {
        if (double.IsFinite(value))
            SetValue(T.CreateSaturating(value));
    }
}
