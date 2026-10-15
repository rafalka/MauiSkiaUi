using System.Globalization;
using System.Numerics;

namespace MauiSkiaUi;

/// <summary>
/// A stepper drawn by SkiaUi for values of any number type <typeparamref name="T"/> (<see cref="int"/>, <see cref="uint"/>,
/// <see cref="long"/>, <see cref="decimal"/>, <see cref="float"/>, …): <see cref="SkUiStepper"/>'s buttons and behavior, with
/// <see cref="Value"/>, <see cref="Minimum"/>, <see cref="Maximum"/> and <see cref="Increment"/> of type <typeparamref name="T"/>.
/// Integers and <see cref="decimal"/> step exactly; binary floating point is rounded by the increment as MAUI's stepper rounds
/// doubles. A step stops at the range's end, also at the type's limits (a <see cref="uint"/> never wraps below 0). In XAML:
/// <c>&lt;sk:SkUiStepper x:TypeArguments="x:Int32" … /&gt;</c>. The Core twin is <see cref="Core.SkUiCoreStepper{T}"/>.
/// </summary>
/// <typeparam name="T">The value's type: a number with a minimum and a maximum value (generic math).</typeparam>
/// <remarks>
/// Native AOT compiles each <typeparamref name="T"/> an app uses ahead of time; XAML names the type at build time (XamlC or the
/// XAML source generator). Bind <see cref="Value"/> to a property of type <typeparamref name="T"/>: a binding of another type makes
/// MAUI convert the values. Screen readers get the range and value as <see cref="double"/>: near the ends of <see cref="long"/>,
/// <see cref="ulong"/> or <see cref="decimal"/> they hear the nearest <see cref="double"/>, while the stepper itself stays exact.
/// </remarks>
public class SkUiStepper<T> : SkUiStepperBase where T : struct, INumber<T>, IMinMaxValue<T>
{
    private int _digits = SkUiStepperMath<T>.Digits(T.One);
    // The value the app asked for before it was clamped: restored when the range widens again (as MAUI's).
    private T _requestedValue;
    private bool _userSetValue;
    private bool _recoercing;

    /// <summary>Creates a stepper from 0 to 100 by 1.</summary>
    public SkUiStepper()
    {
    }

    /// <summary>Creates a stepper with a range, a value and an increment (as MAUI's constructor).</summary>
    public SkUiStepper(T min, T max, T val, T increment) : this()
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
        Value = T.Clamp(val, min, max);
    }

    #region Bindable properties

    /// <summary>Bindable property for <see cref="Maximum"/>.</summary>
    public static readonly BindableProperty MaximumProperty = BindableProperty.Create(nameof(Maximum), typeof(T), typeof(SkUiStepper<T>),
        SkUiStepperMath<T>.DefaultMaximum,
        validateValue: (view, value) => SkUiValidate.FiniteNumber<T>(view, value) && (T)value >= ((SkUiStepper<T>)view).Minimum,
        propertyChanged: (view, _, _) => ((SkUiStepper<T>)view).Recoerce());

    /// <summary>Bindable property for <see cref="Minimum"/>.</summary>
    public static readonly BindableProperty MinimumProperty = BindableProperty.Create(nameof(Minimum), typeof(T), typeof(SkUiStepper<T>), T.Zero,
        validateValue: (view, value) => SkUiValidate.FiniteNumber<T>(view, value) && (T)value <= ((SkUiStepper<T>)view).Maximum,
        propertyChanged: (view, _, _) => ((SkUiStepper<T>)view).Recoerce());

    /// <summary>Bindable property for <see cref="Value"/> (two-way by default).</summary>
    public static readonly BindableProperty ValueProperty = BindableProperty.Create(nameof(Value), typeof(T), typeof(SkUiStepper<T>), T.Zero, BindingMode.TwoWay,
        validateValue: SkUiValidate.FiniteNumber<T>,
        coerceValue: (view, value) => ((SkUiStepper<T>)view).Coerce((T)value),
        propertyChanged: (view, oldValue, newValue) => ((SkUiStepper<T>)view).OnValueChanged((T)oldValue, (T)newValue));

    /// <summary>Bindable property for <see cref="Increment"/>.</summary>
    public static readonly BindableProperty IncrementProperty = BindableProperty.Create(nameof(Increment), typeof(T), typeof(SkUiStepper<T>), T.One,
        validateValue: SkUiValidate.PositiveNumber<T>,
        propertyChanged: (view, _, value) => ((SkUiStepper<T>)view)._digits = SkUiStepperMath<T>.Digits((T)value));

    #endregion

    #region Properties

    /// <summary>The largest value (default 100); not below <see cref="Minimum"/>.</summary>
    public T Maximum { get => (T)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    /// <summary>The smallest value (default 0); not above <see cref="Maximum"/>.</summary>
    public T Minimum { get => (T)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

    /// <summary>The value (two-way by default), kept within the range (and rounded by the increment for binary floating point).</summary>
    public T Value { get => (T)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>How much a button changes the value (default 1); above zero.</summary>
    public T Increment { get => (T)GetValue(IncrementProperty); set => SetValue(IncrementProperty, value); }

    /// <summary>Raised when <see cref="Value"/> changes.</summary>
    public event EventHandler<SkUiValueChangedEventArgs<T>>? ValueChanged;

    /// <summary>Sets <see cref="Maximum"/>.</summary>
    public SkUiStepper<T> SetMaximum(T value)
    {
        SkUiValidate.ThrowIfNotFiniteNumber(value, nameof(value));
        if (value < Minimum)
            throw new ArgumentOutOfRangeException(nameof(value));
        Maximum = value;
        return this;
    }

    /// <summary>Sets <see cref="Minimum"/>.</summary>
    public SkUiStepper<T> SetMinimum(T value)
    {
        SkUiValidate.ThrowIfNotFiniteNumber(value, nameof(value));
        if (value > Maximum)
            throw new ArgumentOutOfRangeException(nameof(value));
        Minimum = value;
        return this;
    }

    /// <summary>Sets <see cref="Value"/>.</summary>
    public SkUiStepper<T> SetValue(T value) { SkUiValidate.ThrowIfNotFiniteNumber(value, nameof(value)); Value = value; return this; }

    /// <summary>Sets <see cref="Increment"/>.</summary>
    public SkUiStepper<T> SetIncrement(T value) { SkUiValidate.ThrowIfNotPositiveNumber(value, nameof(value)); Increment = value; return this; }

    /// <summary>Sets <see cref="SkUiStepperBase.Orientation"/>.</summary>
    public new SkUiStepper<T> SetOrientation(StackOrientation value) { base.SetOrientation(value); return this; }

    /// <summary>Sets <see cref="SkUiStepperBase.TextColor"/>.</summary>
    public new SkUiStepper<T> SetTextColor(Color? value) { base.SetTextColor(value); return this; }

    /// <summary>Sets <see cref="SkUiStepperBase.DecrementText"/>.</summary>
    public new SkUiStepper<T> SetDecrementText(string? value) { base.SetDecrementText(value); return this; }

    /// <summary>Sets <see cref="SkUiStepperBase.IncrementText"/>.</summary>
    public new SkUiStepper<T> SetIncrementText(string? value) { base.SetIncrementText(value); return this; }

    /// <summary>Sets <see cref="SkUiStepperBase.FontFamily"/>.</summary>
    public new SkUiStepper<T> SetFontFamily(string? value) { base.SetFontFamily(value); return this; }

    /// <summary>Sets <see cref="SkUiStepperBase.FontSize"/>.</summary>
    public new SkUiStepper<T> SetFontSize(double value) { base.SetFontSize(value); return this; }

    /// <summary>Sets <see cref="SkUiStepperBase.FontAttributes"/>.</summary>
    public new SkUiStepper<T> SetFontAttributes(FontAttributes value) { base.SetFontAttributes(value); return this; }

    /// <summary>Sets <see cref="SkUiStepperBase.FontAutoScalingEnabled"/>.</summary>
    public new SkUiStepper<T> SetFontAutoScalingEnabled(bool value) { base.SetFontAutoScalingEnabled(value); return this; }

    /// <summary>Sets <see cref="SkUiStepperBase.CornerRadius"/>.</summary>
    public new SkUiStepper<T> SetCornerRadius(int value) { base.SetCornerRadius(value); return this; }

    /// <summary>Sets <see cref="SkUiStepperBase.BorderWidth"/>.</summary>
    public new SkUiStepper<T> SetBorderWidth(double value) { base.SetBorderWidth(value); return this; }

    /// <summary>Sets <see cref="SkUiStepperBase.BorderColor"/>.</summary>
    public new SkUiStepper<T> SetBorderColor(Color? value) { base.SetBorderColor(value); return this; }

    #endregion

    #region Value

    private T Coerce(T value)
    {
        if (!_recoercing)
        {
            _requestedValue = value;
            _userSetValue = true;
        }
        return SkUiStepperMath<T>.Coerce(value, Minimum, Maximum, _digits);
    }

    private void Recoerce()
    {
        _recoercing = true;
        try
        {
            Value = _userSetValue ? _requestedValue : T.Clamp(Value, Minimum, Maximum);
        }
        finally
        {
            _recoercing = false;
        }
        OnValueOrRangeChanged();
    }

    private void OnValueChanged(T oldValue, T newValue)
    {
        OnValueOrRangeChanged();
        ValueChanged?.Invoke(this, new SkUiValueChangedEventArgs<T>(oldValue, newValue));
    }

    private protected override bool CanStep(int direction) => SkUiStepperMath<T>.CanStep(Value, Minimum, Maximum, direction);

    private protected override void Step(int direction) => Value = SkUiStepperMath<T>.Step(Value, Increment, Minimum, Maximum, direction);

    private protected override SkUiSemanticsRange SemanticsRange =>
        new(double.CreateSaturating(Minimum), double.CreateSaturating(Maximum), double.CreateSaturating(Value));

    private protected override string ValueText => Value.ToString(null, CultureInfo.CurrentCulture);

    private protected override void SetValueFromSemantics(double value)
    {
        if (double.IsFinite(value))
            Value = T.CreateSaturating(value);
    }

    #endregion
}
