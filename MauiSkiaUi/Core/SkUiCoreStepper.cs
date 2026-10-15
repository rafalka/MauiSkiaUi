using System.Globalization;

namespace MauiSkiaUi.Core;

/// <summary>
/// A stepper on the Core layer (the twin of <see cref="SkUiStepper"/>): <see cref="SkUiCoreStepperBase"/>'s buttons over a
/// <see cref="double"/> value, kept within the range and rounded by the increment as MAUI's stepper does. For values of another type
/// use <see cref="SkUiCoreStepper{T}"/>.
/// </summary>
public class SkUiCoreStepper : SkUiCoreStepperBase
{
    private double _value;
    private double _minimum;
    private double _maximum = 100;
    private double _increment = 1;
    private int _digits = 4;
    private double _requestedValue;

    /// <summary>The value, rounded and kept within the range.</summary>
    public double Value { get => _value; set => SetValue(value); }

    /// <summary>The smallest value (default 0).</summary>
    public double Minimum { get => _minimum; set => SetMinimum(value); }

    /// <summary>The largest value (default 100).</summary>
    public double Maximum { get => _maximum; set => SetMaximum(value); }

    /// <summary>How much a button changes the value (default 1); above zero.</summary>
    public double Increment { get => _increment; set => SetIncrement(value); }

    /// <summary>Raised when <see cref="Value"/> changes.</summary>
    public event EventHandler<ValueChangedEventArgs>? ValueChanged;

    /// <summary>Sets <see cref="Value"/> (kept within the range; the asked value comes back when the range widens).</summary>
    public SkUiCoreStepper SetValue(double value)
    {
        SkUiValidate.ThrowIfNotFiniteNumber(value, nameof(value));
        _requestedValue = value;
        return ApplyValue(value);
    }

    private SkUiCoreStepper ApplyValue(double value)
    {
        var old = _value;
        if (!SetProperty(ref _value, SkUiStepperMath<double>.Coerce(value, _minimum, _maximum, _digits), nameof(Value)))
            return this;
        OnValueOrRangeChanged();
        ValueChanged?.Invoke(this, new ValueChangedEventArgs(old, _value));
        return this;
    }

    /// <summary>Sets <see cref="Minimum"/> (not above <see cref="Maximum"/>).</summary>
    public SkUiCoreStepper SetMinimum(double value)
    {
        SkUiValidate.ThrowIfNotFiniteNumber(value, nameof(value));
        if (value > _maximum)
            throw new ArgumentOutOfRangeException(nameof(value));
        if (SetProperty(ref _minimum, value, nameof(Minimum)))
            Recoerce();
        return this;
    }

    /// <summary>Sets <see cref="Maximum"/> (not below <see cref="Minimum"/>).</summary>
    public SkUiCoreStepper SetMaximum(double value)
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

    /// <summary>Sets <see cref="Increment"/> (finite, above zero).</summary>
    public SkUiCoreStepper SetIncrement(double value)
    {
        SkUiValidate.ThrowIfNotPositiveNumber(value, nameof(value));
        if (SetProperty(ref _increment, value, nameof(Increment)))
            _digits = SkUiStepperMath<double>.Digits(value);
        return this;
    }

    /// <summary>Sets <see cref="SkUiCoreStepperBase.Orientation"/>.</summary>
    public new SkUiCoreStepper SetOrientation(StackOrientation value) { base.SetOrientation(value); return this; }

    /// <summary>Sets <see cref="SkUiCoreStepperBase.TextColor"/>.</summary>
    public new SkUiCoreStepper SetTextColor(Color? value) { base.SetTextColor(value); return this; }

    /// <summary>Sets <see cref="SkUiCoreStepperBase.DecrementText"/>.</summary>
    public new SkUiCoreStepper SetDecrementText(string? value) { base.SetDecrementText(value); return this; }

    /// <summary>Sets <see cref="SkUiCoreStepperBase.IncrementText"/>.</summary>
    public new SkUiCoreStepper SetIncrementText(string? value) { base.SetIncrementText(value); return this; }

    /// <summary>Sets <see cref="SkUiCoreStepperBase.FontFamily"/>.</summary>
    public new SkUiCoreStepper SetFontFamily(string? value) { base.SetFontFamily(value); return this; }

    /// <summary>Sets <see cref="SkUiCoreStepperBase.FontSize"/>.</summary>
    public new SkUiCoreStepper SetFontSize(double value) { base.SetFontSize(value); return this; }

    /// <summary>Sets <see cref="SkUiCoreStepperBase.FontAttributes"/>.</summary>
    public new SkUiCoreStepper SetFontAttributes(FontAttributes value) { base.SetFontAttributes(value); return this; }

    /// <summary>Sets <see cref="SkUiCoreStepperBase.FontAutoScalingEnabled"/>.</summary>
    public new SkUiCoreStepper SetFontAutoScalingEnabled(bool value) { base.SetFontAutoScalingEnabled(value); return this; }

    /// <summary>Sets <see cref="SkUiCoreStepperBase.CornerRadius"/>.</summary>
    public new SkUiCoreStepper SetCornerRadius(double value) { base.SetCornerRadius(value); return this; }

    /// <summary>Sets <see cref="SkUiCoreStepperBase.BorderWidth"/>.</summary>
    public new SkUiCoreStepper SetBorderWidth(double value) { base.SetBorderWidth(value); return this; }

    /// <summary>Sets <see cref="SkUiCoreStepperBase.BorderColor"/>.</summary>
    public new SkUiCoreStepper SetBorderColor(Color? value) { base.SetBorderColor(value); return this; }

    private protected override bool CanStep(int direction) => direction < 0 ? _value > _minimum : _value < _maximum;

    // As MAUI's: the next value is coerced to the range.
    private protected override void Step(int direction) => SetValue(_value + direction * _increment);

    private protected override SkUiSemanticsRange SemanticsRange => new(_minimum, _maximum, _value);

    private protected override string ValueText => _value.ToString("G", CultureInfo.CurrentCulture);

    private protected override void SetValueFromSemantics(double value) => SetValue(value);
}
