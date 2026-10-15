using System.Globalization;

namespace MauiSkiaUi;

/// <summary>
/// A stepper drawn by SkiaUi (MAUI's <c>Stepper</c>): look-drawn − and + buttons that change <see cref="Value"/> by
/// <see cref="Increment"/> within <see cref="Minimum"/>–<see cref="Maximum"/> (coerced as MAUI's), a button disabled at its end of the
/// range, held buttons repeating, arrow keys and screen readers' increment / decrement. Beyond MAUI: what
/// <see cref="SkUiStepperBase"/> adds (stacked buttons, texts in place of − and +). For values of another type (<see cref="int"/>,
/// <see cref="decimal"/>, …) use <see cref="SkUiStepper{T}"/>. The Core twin is <see cref="Core.SkUiCoreStepper"/>.
/// </summary>
public class SkUiStepper : SkUiStepperBase
{
    // MAUI's rounding: 4 significant digits after the increment's most significant one.
    private int _digits = 4;
    // The value the app asked for before it was clamped: restored when the range widens again (as MAUI's).
    private double _requestedValue;
    private bool _userSetValue;
    private bool _recoercing;

    /// <summary>Creates a stepper from 0 to 100 by 1.</summary>
    public SkUiStepper()
    {
    }

    /// <summary>Creates a stepper with a range, a value and an increment (MAUI's constructor).</summary>
    public SkUiStepper(double min, double max, double val, double increment) : this()
    {
        SkUiValidate.ThrowIfNotFiniteNumber(min, nameof(min));
        SkUiValidate.ThrowIfNotFiniteNumber(max, nameof(max));
        SkUiValidate.ThrowIfNotFiniteNumber(val, nameof(val));
        if (min >= max)
            throw new ArgumentOutOfRangeException(nameof(min));
        if (max > Minimum)
        {
            Maximum = max;
            Minimum = min;
        }
        else
        {
            Minimum = min;
            Maximum = max;
        }
        SetIncrement(increment);
        Value = Math.Clamp(val, min, max);
    }

    #region Bindable properties

    /// <summary>Bindable property for <see cref="Maximum"/>.</summary>
    public static readonly BindableProperty MaximumProperty = BindableProperty.Create(nameof(Maximum), typeof(double), typeof(SkUiStepper), 100d,
        validateValue: (view, value) => SkUiValidate.Finite(view, value) && (double)value >= ((SkUiStepper)view).Minimum,
        propertyChanged: (view, _, _) => ((SkUiStepper)view).Recoerce());

    /// <summary>Bindable property for <see cref="Minimum"/>.</summary>
    public static readonly BindableProperty MinimumProperty = BindableProperty.Create(nameof(Minimum), typeof(double), typeof(SkUiStepper), 0d,
        validateValue: (view, value) => SkUiValidate.Finite(view, value) && (double)value <= ((SkUiStepper)view).Maximum,
        propertyChanged: (view, _, _) => ((SkUiStepper)view).Recoerce());

    /// <summary>Bindable property for <see cref="Value"/> (two-way by default).</summary>
    public static readonly BindableProperty ValueProperty = BindableProperty.Create(nameof(Value), typeof(double), typeof(SkUiStepper), 0d, BindingMode.TwoWay,
        validateValue: SkUiValidate.Finite,
        coerceValue: (view, value) => ((SkUiStepper)view).Coerce((double)value),
        propertyChanged: (view, oldValue, newValue) => ((SkUiStepper)view).OnValueChanged((double)oldValue, (double)newValue));

    /// <summary>Bindable property for <see cref="Increment"/>.</summary>
    public static readonly BindableProperty IncrementProperty = BindableProperty.Create(nameof(Increment), typeof(double), typeof(SkUiStepper), 1d,
        validateValue: SkUiValidate.FinitePositive,
        propertyChanged: (view, _, value) => ((SkUiStepper)view)._digits = SkUiStepperMath<double>.Digits((double)value));

    #endregion

    #region Properties

    /// <summary>The largest value (default 100); not below <see cref="Minimum"/>.</summary>
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    /// <summary>The smallest value (default 0); not above <see cref="Maximum"/>.</summary>
    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

    /// <summary>The value (two-way by default), rounded and kept within the range as MAUI's.</summary>
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>How much a button changes the value (default 1); above zero.</summary>
    public double Increment { get => (double)GetValue(IncrementProperty); set => SetValue(IncrementProperty, value); }

    /// <summary>Raised when <see cref="Value"/> changes.</summary>
    public event EventHandler<ValueChangedEventArgs>? ValueChanged;

    /// <summary>Sets <see cref="Maximum"/>.</summary>
    public SkUiStepper SetMaximum(double value) { SkUiValidate.ThrowIfNotFiniteNumber(value, nameof(value)); if (value < Minimum) throw new ArgumentOutOfRangeException(nameof(value)); Maximum = value; return this; }

    /// <summary>Sets <see cref="Minimum"/>.</summary>
    public SkUiStepper SetMinimum(double value) { SkUiValidate.ThrowIfNotFiniteNumber(value, nameof(value)); if (value > Maximum) throw new ArgumentOutOfRangeException(nameof(value)); Minimum = value; return this; }

    /// <summary>Sets <see cref="Value"/>.</summary>
    public SkUiStepper SetValue(double value) { SkUiValidate.ThrowIfNotFiniteNumber(value, nameof(value)); Value = value; return this; }

    /// <summary>Sets <see cref="Increment"/>.</summary>
    public SkUiStepper SetIncrement(double value) { SkUiValidate.ThrowIfNotPositiveNumber(value, nameof(value)); Increment = value; return this; }

    /// <summary>Sets <see cref="SkUiStepperBase.Orientation"/>.</summary>
    public new SkUiStepper SetOrientation(StackOrientation value) { base.SetOrientation(value); return this; }

    /// <summary>Sets <see cref="SkUiStepperBase.TextColor"/>.</summary>
    public new SkUiStepper SetTextColor(Color? value) { base.SetTextColor(value); return this; }

    /// <summary>Sets <see cref="SkUiStepperBase.DecrementText"/>.</summary>
    public new SkUiStepper SetDecrementText(string? value) { base.SetDecrementText(value); return this; }

    /// <summary>Sets <see cref="SkUiStepperBase.IncrementText"/>.</summary>
    public new SkUiStepper SetIncrementText(string? value) { base.SetIncrementText(value); return this; }

    /// <summary>Sets <see cref="SkUiStepperBase.FontFamily"/>.</summary>
    public new SkUiStepper SetFontFamily(string? value) { base.SetFontFamily(value); return this; }

    /// <summary>Sets <see cref="SkUiStepperBase.FontSize"/>.</summary>
    public new SkUiStepper SetFontSize(double value) { base.SetFontSize(value); return this; }

    /// <summary>Sets <see cref="SkUiStepperBase.FontAttributes"/>.</summary>
    public new SkUiStepper SetFontAttributes(FontAttributes value) { base.SetFontAttributes(value); return this; }

    /// <summary>Sets <see cref="SkUiStepperBase.FontAutoScalingEnabled"/>.</summary>
    public new SkUiStepper SetFontAutoScalingEnabled(bool value) { base.SetFontAutoScalingEnabled(value); return this; }

    /// <summary>Sets <see cref="SkUiStepperBase.CornerRadius"/>.</summary>
    public new SkUiStepper SetCornerRadius(int value) { base.SetCornerRadius(value); return this; }

    /// <summary>Sets <see cref="SkUiStepperBase.BorderWidth"/>.</summary>
    public new SkUiStepper SetBorderWidth(double value) { base.SetBorderWidth(value); return this; }

    /// <summary>Sets <see cref="SkUiStepperBase.BorderColor"/>.</summary>
    public new SkUiStepper SetBorderColor(Color? value) { base.SetBorderColor(value); return this; }

    #endregion

    #region Value

    private double Coerce(double value)
    {
        if (!_recoercing)
        {
            _requestedValue = value;
            _userSetValue = true;
        }
        return SkUiStepperMath<double>.Coerce(value, Minimum, Maximum, _digits);
    }

    private void Recoerce()
    {
        _recoercing = true;
        try
        {
            Value = _userSetValue ? _requestedValue : Math.Clamp(Value, Minimum, Maximum);
        }
        finally
        {
            _recoercing = false;
        }
        OnValueOrRangeChanged();
    }

    private void OnValueChanged(double oldValue, double newValue)
    {
        OnValueOrRangeChanged();
        ValueChanged?.Invoke(this, new ValueChangedEventArgs(oldValue, newValue));
    }

    private protected override bool CanStep(int direction) => direction < 0 ? Value > Minimum : Value < Maximum;

    // As MAUI's: the next value is coerced to the range.
    private protected override void Step(int direction) => Value += direction * Increment;

    private protected override SkUiSemanticsRange SemanticsRange => new(Minimum, Maximum, Value);

    private protected override string ValueText => Value.ToString("G", CultureInfo.CurrentCulture);

    private protected override void SetValueFromSemantics(double value) => Value = value;

    #endregion
}
