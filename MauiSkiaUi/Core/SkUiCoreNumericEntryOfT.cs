using System.Globalization;
using System.Numerics;

namespace MauiSkiaUi.Core;

/// <summary>
/// A Core numeric entry for values of any number type <typeparamref name="T"/> (the twin of <see cref="SkUiNumericEntry{T}"/>; D16):
/// <see cref="Value"/> of type <typeparamref name="T"/>?, the range, <see cref="Increment"/> and <see cref="LargeIncrement"/> of type
/// <typeparamref name="T"/>. <see cref="SkUiCoreNumericEntry"/> is the <see cref="double"/> one.
/// </summary>
/// <typeparam name="T">The value's type: a number with a minimum and a maximum value (generic math).</typeparam>
public class SkUiCoreNumericEntry<T> : SkUiCoreNumericEntryBase, ISkUiNumericValueHost<T> where T : struct, INumber<T>, IMinMaxValue<T>
{
    private readonly SkUiNumericEntryModel<T> _numeric;
    private T? _value;
    // The value the app asked for before it was clamped: restored when the range widens again.
    private T? _requestedValue;

    /// <summary>Creates an empty entry over the type's whole range, stepping by 1.</summary>
    public SkUiCoreNumericEntry() : this(new SkUiNumericEntryModel<T>())
    {
    }

    private SkUiCoreNumericEntry(SkUiNumericEntryModel<T> numeric) : base(numeric) => _numeric = numeric;

    /// <summary>The value: <c>null</c> while empty (with <see cref="SkUiCoreNumericEntryBase.AllowNull"/>), else within the range (see <see cref="SkUiNumericEntry{T}.Value"/>).</summary>
    public T? Value { get => _value; set => SetValue(value); }

    /// <summary>The smallest value (default the type's smallest); a minus sign can be typed only when it is negative.</summary>
    public T Minimum { get => _numeric.Minimum; set => SetMinimum(value); }

    /// <summary>The largest value (default the type's largest).</summary>
    public T Maximum { get => _numeric.Maximum; set => SetMaximum(value); }

    /// <summary>How much a button, Up / Down and the wheel change the value (default 1); above zero.</summary>
    public T Increment { get => _numeric.Increment; set => SetIncrement(value); }

    /// <summary>How much Page Up / Page Down change the value; <c>null</c> (default): 10 × <see cref="Increment"/>.</summary>
    public T? LargeIncrement { get => _numeric.LargeIncrement; set => SetLargeIncrement(value); }

    /// <summary>Raised when <see cref="Value"/> changes (synchronously), with the old and new value.</summary>
    public event EventHandler<SkUiValueChangedEventArgs<T?>>? ValueChanged;

    /// <summary>Sets <see cref="Value"/> (clamped into the range at once; the asked value comes back when the range widens).</summary>
    public SkUiCoreNumericEntry<T> SetValue(T? value)
    {
        if (value is { } number)
            SkUiValidate.ThrowIfNotFiniteNumber(number, nameof(value));
        _requestedValue = value;
        return ApplyValue(value);
    }

    private SkUiCoreNumericEntry<T> ApplyValue(T? value)
    {
        var old = _value;
        if (!SetProperty(ref _value, _numeric.Coerce(value), nameof(Value)))
            return this;
        _numeric.ValueChanged();
        ValueChanged?.Invoke(this, new SkUiValueChangedEventArgs<T?>(old, _value));
        return this;
    }

    /// <summary>Sets <see cref="Minimum"/> (not above <see cref="Maximum"/>).</summary>
    public SkUiCoreNumericEntry<T> SetMinimum(T value)
    {
        SkUiValidate.ThrowIfNotFiniteNumber(value, nameof(value));
        if (value > _numeric.Maximum)
            throw new ArgumentOutOfRangeException(nameof(value));
        if (SetProperty(ref _numeric.Minimum, value, nameof(Minimum)))
            Recoerce();
        return this;
    }

    /// <summary>Sets <see cref="Maximum"/> (not below <see cref="Minimum"/>).</summary>
    public SkUiCoreNumericEntry<T> SetMaximum(T value)
    {
        SkUiValidate.ThrowIfNotFiniteNumber(value, nameof(value));
        if (value < _numeric.Minimum)
            throw new ArgumentOutOfRangeException(nameof(value));
        if (SetProperty(ref _numeric.Maximum, value, nameof(Maximum)))
            Recoerce();
        return this;
    }

    /// <summary>Sets <see cref="Minimum"/> and <see cref="Maximum"/> in an order that keeps the range valid.</summary>
    public SkUiCoreNumericEntry<T> SetRange(T minimum, T maximum)
    {
        SkUiValidate.ThrowIfNotFiniteNumber(minimum, nameof(minimum));
        SkUiValidate.ThrowIfNotFiniteNumber(maximum, nameof(maximum));
        if (minimum > maximum)
            throw new ArgumentOutOfRangeException(nameof(minimum));
        return minimum > _numeric.Maximum ? SetMaximum(maximum).SetMinimum(minimum) : SetMinimum(minimum).SetMaximum(maximum);
    }

    /// <summary>Sets <see cref="Increment"/> (above zero).</summary>
    public SkUiCoreNumericEntry<T> SetIncrement(T value)
    {
        SkUiValidate.ThrowIfNotPositiveNumber(value, nameof(value));
        if (SetProperty(ref _numeric.Increment, value, nameof(Increment)))
            _numeric.RangeChanged();
        return this;
    }

    /// <summary>Sets <see cref="LargeIncrement"/> (above zero, or <c>null</c>).</summary>
    public SkUiCoreNumericEntry<T> SetLargeIncrement(T? value)
    {
        if (value is { } number)
            SkUiValidate.ThrowIfNotPositiveNumber(number, nameof(value));
        SetProperty(ref _numeric.LargeIncrement, value, nameof(LargeIncrement));
        return this;
    }

    /// <inheritdoc cref="SkUiCoreNumericEntryBase.SetButtonPlacement" />
    public new SkUiCoreNumericEntry<T> SetButtonPlacement(SkUiStepButtonPlacement value) { base.SetButtonPlacement(value); return this; }

    /// <inheritdoc cref="SkUiCoreNumericEntryBase.SetButtonOrientation" />
    public new SkUiCoreNumericEntry<T> SetButtonOrientation(StackOrientation value) { base.SetButtonOrientation(value); return this; }

    /// <inheritdoc cref="SkUiCoreNumericEntryBase.SetDecrementText" />
    public new SkUiCoreNumericEntry<T> SetDecrementText(string? value) { base.SetDecrementText(value); return this; }

    /// <inheritdoc cref="SkUiCoreNumericEntryBase.SetIncrementText" />
    public new SkUiCoreNumericEntry<T> SetIncrementText(string? value) { base.SetIncrementText(value); return this; }

    /// <inheritdoc cref="SkUiCoreNumericEntryBase.SetButtonFontFamily" />
    public new SkUiCoreNumericEntry<T> SetButtonFontFamily(string? value) { base.SetButtonFontFamily(value); return this; }

    /// <inheritdoc cref="SkUiCoreNumericEntryBase.SetButtonColor" />
    public new SkUiCoreNumericEntry<T> SetButtonColor(Color? value) { base.SetButtonColor(value); return this; }

    /// <inheritdoc cref="SkUiCoreNumericEntryBase.SetAllowNull" />
    public new SkUiCoreNumericEntry<T> SetAllowNull(bool value) { base.SetAllowNull(value); return this; }

    /// <inheritdoc cref="SkUiCoreNumericEntryBase.SetFormat" />
    public new SkUiCoreNumericEntry<T> SetFormat(string? value) { base.SetFormat(value); return this; }

    /// <inheritdoc cref="SkUiCoreNumericEntryBase.SetCulture" />
    public new SkUiCoreNumericEntry<T> SetCulture(CultureInfo? value) { base.SetCulture(value); return this; }

    /// <inheritdoc cref="SkUiCoreNumericEntryBase.SetValueChangeMode" />
    public new SkUiCoreNumericEntry<T> SetValueChangeMode(SkUiValueChangeMode value) { base.SetValueChangeMode(value); return this; }

    /// <inheritdoc cref="SkUiCoreNumericEntryBase.SetMaximumDecimalDigits" />
    public new SkUiCoreNumericEntry<T> SetMaximumDecimalDigits(int? value) { base.SetMaximumDecimalDigits(value); return this; }

    /// <inheritdoc cref="SkUiCoreNumericEntryBase.SetIsEditable" />
    public new SkUiCoreNumericEntry<T> SetIsEditable(bool value) { base.SetIsEditable(value); return this; }

    /// <inheritdoc cref="SkUiCoreNumericEntryBase.SetAutoReverse" />
    public new SkUiCoreNumericEntry<T> SetAutoReverse(bool value) { base.SetAutoReverse(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetPlaceholder" />
    public new SkUiCoreNumericEntry<T> SetPlaceholder(string? value) { base.SetPlaceholder(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetHorizontalTextAlignment" />
    public new SkUiCoreNumericEntry<T> SetHorizontalTextAlignment(TextAlignment value) { base.SetHorizontalTextAlignment(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetFontFamily" />
    public new SkUiCoreNumericEntry<T> SetFontFamily(string? value) { base.SetFontFamily(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetFontSize" />
    public new SkUiCoreNumericEntry<T> SetFontSize(double value) { base.SetFontSize(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetIsReadOnly" />
    public new SkUiCoreNumericEntry<T> SetIsReadOnly(bool value) { base.SetIsReadOnly(value); return this; }

    private protected override void Recoerce()
    {
        ApplyValue(_requestedValue);
        _numeric.RangeChanged();
    }

    T? ISkUiNumericValueHost<T>.Value => _value;

    void ISkUiNumericValueHost<T>.SetValueFromUser(T? value) => SetValue(value);
}

/// <summary>
/// A Core numeric entry on <see cref="double"/>? (the twin of <see cref="SkUiNumericEntry"/>; D16): see
/// <see cref="SkUiCoreNumericEntryBase"/> and <see cref="SkUiCoreNumericEntry{T}"/>.
/// </summary>
public class SkUiCoreNumericEntry : SkUiCoreNumericEntry<double>
{
}
