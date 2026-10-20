using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Windows.Input;

namespace MauiSkiaUi;

/// <summary>
/// A numeric entry drawn by SkiaUi for values of any number type <typeparamref name="T"/> (<see cref="int"/>, <see cref="long"/>,
/// <see cref="decimal"/>, <see cref="float"/>, …; D16): <see cref="SkUiNumericEntryBase"/>'s field and buttons, with
/// <see cref="Value"/> of type <typeparamref name="T"/>? and the range, <see cref="Increment"/> and <see cref="LargeIncrement"/> of
/// type <typeparamref name="T"/>. Integer types take no decimal separator and refuse what would overflow them while typing (a
/// <see cref="byte"/> entry refuses 256); a minus sign can be typed only when <see cref="Minimum"/> is negative. In XAML:
/// <c>&lt;sk:SkUiNumericEntry x:TypeArguments="x:Int32" … /&gt;</c>. <see cref="SkUiNumericEntry"/> is the <see cref="double"/> one.
/// The Core twin is <see cref="Core.SkUiCoreNumericEntry{T}"/>.
/// </summary>
/// <typeparam name="T">The value's type: a number with a minimum and a maximum value (generic math).</typeparam>
/// <remarks>
/// Native AOT compiles each <typeparamref name="T"/> an app uses ahead of time; XAML names the type at build time. Bind
/// <see cref="Value"/> to a property of type <typeparamref name="T"/>? (or <typeparamref name="T"/> with <see cref="SkUiNumericEntryBase.AllowNull"/>
/// off): a binding of another type makes MAUI convert the values.
/// </remarks>
// The annotation is for the trimmer's view of typeof(T?) in the bindable properties: a value type has its parameterless constructor anyway.
public class SkUiNumericEntry<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T> : SkUiNumericEntryBase, ISkUiNumericValueHost<T> where T : struct, INumber<T>, IMinMaxValue<T>
{
    private readonly SkUiNumericEntryModel<T> _numeric = new();
    // The value the app asked for before it was clamped: restored when the range widens again (as the steppers').
    private T? _requestedValue;
    private bool _recoercing;

    /// <summary>Creates an empty entry over the type's whole range, stepping by 1.</summary>
    public SkUiNumericEntry()
    {
    }

    private protected override SkUiNumericEntryModel NumericEngine => _numeric;

    /// <summary>The engine, attached to the field (see <see cref="SkUiNumericEntryBase.Numeric"/>).</summary>
    private SkUiNumericEntryModel<T> Engine
    {
        get
        {
            _ = Model;
            return _numeric;
        }
    }

    #region Bindable properties

    /// <summary>Bindable property for <see cref="Value"/> (two-way by default).</summary>
    public static readonly BindableProperty ValueProperty = BindableProperty.Create(nameof(Value), typeof(T?), typeof(SkUiNumericEntry<T>), null, BindingMode.TwoWay,
        validateValue: (view, value) => value is null || SkUiValidate.FiniteNumber<T>(view, value),
        coerceValue: (view, value) => ((SkUiNumericEntry<T>)view).CoerceValue((T?)value),
        propertyChanged: (view, oldValue, newValue) => ((SkUiNumericEntry<T>)view).OnValueChanged((T?)oldValue, (T?)newValue));

    /// <summary>Bindable property for <see cref="Minimum"/>.</summary>
    public static readonly BindableProperty MinimumProperty = BindableProperty.Create(nameof(Minimum), typeof(T), typeof(SkUiNumericEntry<T>), T.MinValue,
        validateValue: (view, value) => SkUiValidate.FiniteNumber<T>(view, value) && (T)value <= ((SkUiNumericEntry<T>)view).Maximum,
        propertyChanged: (view, _, value) => ((SkUiNumericEntry<T>)view).OnRangeChanged(numeric => numeric.Minimum = (T)value));

    /// <summary>Bindable property for <see cref="Maximum"/>.</summary>
    public static readonly BindableProperty MaximumProperty = BindableProperty.Create(nameof(Maximum), typeof(T), typeof(SkUiNumericEntry<T>), T.MaxValue,
        validateValue: (view, value) => SkUiValidate.FiniteNumber<T>(view, value) && (T)value >= ((SkUiNumericEntry<T>)view).Minimum,
        propertyChanged: (view, _, value) => ((SkUiNumericEntry<T>)view).OnRangeChanged(numeric => numeric.Maximum = (T)value));

    /// <summary>Bindable property for <see cref="Increment"/>.</summary>
    public static readonly BindableProperty IncrementProperty = BindableProperty.Create(nameof(Increment), typeof(T), typeof(SkUiNumericEntry<T>), T.One,
        validateValue: SkUiValidate.PositiveNumber<T>,
        propertyChanged: (view, _, value) => ((SkUiNumericEntry<T>)view).OnRangeChanged(numeric => numeric.Increment = (T)value));

    /// <summary>Bindable property for <see cref="LargeIncrement"/>.</summary>
    public static readonly BindableProperty LargeIncrementProperty = BindableProperty.Create(nameof(LargeIncrement), typeof(T?), typeof(SkUiNumericEntry<T>), null,
        validateValue: (view, value) => value is null || SkUiValidate.PositiveNumber<T>(view, value),
        propertyChanged: (view, _, value) => ((SkUiNumericEntry<T>)view).Engine.LargeIncrement = (T?)value);

    #endregion

    #region Properties

    /// <summary>
    /// The value (two-way by default): <c>null</c> while the field is empty (with <see cref="SkUiNumericEntryBase.AllowNull"/>), else
    /// within the range and rounded to <see cref="SkUiNumericEntryBase.MaximumDecimalDigits"/>. A value the app sets is clamped at once
    /// (the asked value comes back when the range widens); typed text only on commit.
    /// </summary>
    public T? Value { get => (T?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>The smallest value (default the type's smallest); a minus sign can be typed only when it is negative.</summary>
    public T Minimum { get => (T)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

    /// <summary>The largest value (default the type's largest).</summary>
    public T Maximum { get => (T)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    /// <summary>How much a button, Up / Down and the wheel change the value (default 1); above zero.</summary>
    public T Increment { get => (T)GetValue(IncrementProperty); set => SetValue(IncrementProperty, value); }

    /// <summary>How much Page Up / Page Down change the value; <c>null</c> (default): 10 × <see cref="Increment"/>.</summary>
    public T? LargeIncrement { get => (T?)GetValue(LargeIncrementProperty); set => SetValue(LargeIncrementProperty, value); }

    /// <summary>Raised when <see cref="Value"/> changes (synchronously), with the old and new value.</summary>
    public event EventHandler<SkUiValueChangedEventArgs<T?>>? ValueChanged;

    #endregion

    #region Fluent setters

    /// <summary>Sets <see cref="Value"/>.</summary>
    public SkUiNumericEntry<T> SetValue(T? value)
    {
        if (value is { } number)
            SkUiValidate.ThrowIfNotFiniteNumber(number, nameof(value));
        Value = value;
        return this;
    }

    /// <summary>Sets <see cref="Minimum"/>.</summary>
    public SkUiNumericEntry<T> SetMinimum(T value)
    {
        SkUiValidate.ThrowIfNotFiniteNumber(value, nameof(value));
        if (value > Maximum)
            throw new ArgumentOutOfRangeException(nameof(value));
        Minimum = value;
        return this;
    }

    /// <summary>Sets <see cref="Maximum"/>.</summary>
    public SkUiNumericEntry<T> SetMaximum(T value)
    {
        SkUiValidate.ThrowIfNotFiniteNumber(value, nameof(value));
        if (value < Minimum)
            throw new ArgumentOutOfRangeException(nameof(value));
        Maximum = value;
        return this;
    }

    /// <summary>Sets <see cref="Minimum"/> and <see cref="Maximum"/> in an order that keeps the range valid.</summary>
    public SkUiNumericEntry<T> SetRange(T minimum, T maximum)
    {
        SkUiValidate.ThrowIfNotFiniteNumber(minimum, nameof(minimum));
        SkUiValidate.ThrowIfNotFiniteNumber(maximum, nameof(maximum));
        if (minimum > maximum)
            throw new ArgumentOutOfRangeException(nameof(minimum));
        if (minimum > Maximum)
        {
            Maximum = maximum;
            Minimum = minimum;
        }
        else
        {
            Minimum = minimum;
            Maximum = maximum;
        }
        return this;
    }

    /// <summary>Sets <see cref="Increment"/>.</summary>
    public SkUiNumericEntry<T> SetIncrement(T value) { SkUiValidate.ThrowIfNotPositiveNumber(value, nameof(value)); Increment = value; return this; }

    /// <summary>Sets <see cref="LargeIncrement"/>.</summary>
    public SkUiNumericEntry<T> SetLargeIncrement(T? value)
    {
        if (value is { } number)
            SkUiValidate.ThrowIfNotPositiveNumber(number, nameof(value));
        LargeIncrement = value;
        return this;
    }

    /// <inheritdoc cref="SkUiNumericEntryBase.SetButtonPlacement" />
    public new SkUiNumericEntry<T> SetButtonPlacement(SkUiStepButtonPlacement value) { base.SetButtonPlacement(value); return this; }

    /// <inheritdoc cref="SkUiNumericEntryBase.SetButtonOrientation" />
    public new SkUiNumericEntry<T> SetButtonOrientation(StackOrientation value) { base.SetButtonOrientation(value); return this; }

    /// <inheritdoc cref="SkUiNumericEntryBase.SetDecrementText" />
    public new SkUiNumericEntry<T> SetDecrementText(string? value) { base.SetDecrementText(value); return this; }

    /// <inheritdoc cref="SkUiNumericEntryBase.SetIncrementText" />
    public new SkUiNumericEntry<T> SetIncrementText(string? value) { base.SetIncrementText(value); return this; }

    /// <inheritdoc cref="SkUiNumericEntryBase.SetButtonFontFamily" />
    public new SkUiNumericEntry<T> SetButtonFontFamily(string? value) { base.SetButtonFontFamily(value); return this; }

    /// <inheritdoc cref="SkUiNumericEntryBase.SetButtonTextColor" />
    public new SkUiNumericEntry<T> SetButtonTextColor(Color? value) { base.SetButtonTextColor(value); return this; }

    /// <inheritdoc cref="SkUiNumericEntryBase.SetAllowNull" />
    public new SkUiNumericEntry<T> SetAllowNull(bool value) { base.SetAllowNull(value); return this; }

    /// <inheritdoc cref="SkUiNumericEntryBase.SetFormat" />
    public new SkUiNumericEntry<T> SetFormat(string? value) { base.SetFormat(value); return this; }

    /// <inheritdoc cref="SkUiNumericEntryBase.SetCulture" />
    public new SkUiNumericEntry<T> SetCulture(CultureInfo? value) { base.SetCulture(value); return this; }

    /// <inheritdoc cref="SkUiNumericEntryBase.SetValueChangeMode" />
    public new SkUiNumericEntry<T> SetValueChangeMode(SkUiValueChangeMode value) { base.SetValueChangeMode(value); return this; }

    /// <inheritdoc cref="SkUiNumericEntryBase.SetMaximumDecimalDigits" />
    public new SkUiNumericEntry<T> SetMaximumDecimalDigits(int? value) { base.SetMaximumDecimalDigits(value); return this; }

    /// <inheritdoc cref="SkUiNumericEntryBase.SetIsEditable" />
    public new SkUiNumericEntry<T> SetIsEditable(bool value) { base.SetIsEditable(value); return this; }

    /// <inheritdoc cref="SkUiNumericEntryBase.SetAutoReverse" />
    public new SkUiNumericEntry<T> SetAutoReverse(bool value) { base.SetAutoReverse(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetPlaceholder" />
    public new SkUiNumericEntry<T> SetPlaceholder(string? value) { base.SetPlaceholder(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetHorizontalTextAlignment" />
    public new SkUiNumericEntry<T> SetHorizontalTextAlignment(TextAlignment value) { base.SetHorizontalTextAlignment(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetFontFamily" />
    public new SkUiNumericEntry<T> SetFontFamily(string? value) { base.SetFontFamily(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetFontSize" />
    public new SkUiNumericEntry<T> SetFontSize(double value) { base.SetFontSize(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetIsReadOnly" />
    public new SkUiNumericEntry<T> SetIsReadOnly(bool value) { base.SetIsReadOnly(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetText" />
    public new SkUiNumericEntry<T> SetText(string? value) { base.SetText(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetPlaceholderColor" />
    public new SkUiNumericEntry<T> SetPlaceholderColor(Color? value) { base.SetPlaceholderColor(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetTextColor" />
    public new SkUiNumericEntry<T> SetTextColor(Color? value) { base.SetTextColor(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetCharacterSpacing" />
    public new SkUiNumericEntry<T> SetCharacterSpacing(double value) { base.SetCharacterSpacing(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetTextTransform" />
    public new SkUiNumericEntry<T> SetTextTransform(TextTransform value) { base.SetTextTransform(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetFontAttributes" />
    public new SkUiNumericEntry<T> SetFontAttributes(FontAttributes value) { base.SetFontAttributes(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetFontAutoScalingEnabled" />
    public new SkUiNumericEntry<T> SetFontAutoScalingEnabled(bool value) { base.SetFontAutoScalingEnabled(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetVerticalTextAlignment" />
    public new SkUiNumericEntry<T> SetVerticalTextAlignment(TextAlignment value) { base.SetVerticalTextAlignment(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetMaxLength" />
    public new SkUiNumericEntry<T> SetMaxLength(int value) { base.SetMaxLength(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetKeyboard" />
    public new SkUiNumericEntry<T> SetKeyboard(Keyboard value) { base.SetKeyboard(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetIsSpellCheckEnabled" />
    public new SkUiNumericEntry<T> SetIsSpellCheckEnabled(bool value) { base.SetIsSpellCheckEnabled(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetIsTextPredictionEnabled" />
    public new SkUiNumericEntry<T> SetIsTextPredictionEnabled(bool value) { base.SetIsTextPredictionEnabled(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetCursorPosition" />
    public new SkUiNumericEntry<T> SetCursorPosition(int value) { base.SetCursorPosition(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetSelectionLength" />
    public new SkUiNumericEntry<T> SetSelectionLength(int value) { base.SetSelectionLength(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetCaretColor" />
    public new SkUiNumericEntry<T> SetCaretColor(Color? value) { base.SetCaretColor(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetSelectionColor" />
    public new SkUiNumericEntry<T> SetSelectionColor(Color? value) { base.SetSelectionColor(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetAutofillHint" />
    public new SkUiNumericEntry<T> SetAutofillHint(string? value) { base.SetAutofillHint(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetInputFormatters" />
    public new SkUiNumericEntry<T> SetInputFormatters(params IEnumerable<SkUiTextInputFormatter> formatters) { base.SetInputFormatters(formatters); return this; }

    /// <inheritdoc cref="SkUiInputView.SetFormatsAssignedText" />
    public new SkUiNumericEntry<T> SetFormatsAssignedText(bool value) { base.SetFormatsAssignedText(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetLeadingView" />
    public new SkUiNumericEntry<T> SetLeadingView(ISkUiView? value) { base.SetLeadingView(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetTrailingView" />
    public new SkUiNumericEntry<T> SetTrailingView(ISkUiView? value) { base.SetTrailingView(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetHasError" />
    public new SkUiNumericEntry<T> SetHasError(bool value) { base.SetHasError(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetErrorText" />
    public new SkUiNumericEntry<T> SetErrorText(string? value) { base.SetErrorText(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetHelperText" />
    public new SkUiNumericEntry<T> SetHelperText(string? value) { base.SetHelperText(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetErrorColor" />
    public new SkUiNumericEntry<T> SetErrorColor(Color? value) { base.SetErrorColor(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetFieldStyle" />
    public new SkUiNumericEntry<T> SetFieldStyle(SkUiTextFieldStyle value) { base.SetFieldStyle(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetTextPadding" />
    public new SkUiNumericEntry<T> SetTextPadding(Thickness? value) { base.SetTextPadding(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetFillColor" />
    public new SkUiNumericEntry<T> SetFillColor(Color? value) { base.SetFillColor(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetBorderColor" />
    public new SkUiNumericEntry<T> SetBorderColor(Color? value) { base.SetBorderColor(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetBorderWidth" />
    public new SkUiNumericEntry<T> SetBorderWidth(double? value) { base.SetBorderWidth(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetCornerRadii" />
    public new SkUiNumericEntry<T> SetCornerRadii(CornerRadius? value) { base.SetCornerRadii(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetCornerRadius" />
    public new SkUiNumericEntry<T> SetCornerRadius(int? value) { base.SetCornerRadius(value); return this; }

    /// <inheritdoc cref="SkUiEntry.SetIsPassword" />
    public new SkUiNumericEntry<T> SetIsPassword(bool value) { base.SetIsPassword(value); return this; }

    /// <inheritdoc cref="SkUiEntry.SetReturnType" />
    public new SkUiNumericEntry<T> SetReturnType(ReturnType value) { base.SetReturnType(value); return this; }

    /// <inheritdoc cref="SkUiEntry.SetReturnCommand" />
    public new SkUiNumericEntry<T> SetReturnCommand(ICommand? value) { base.SetReturnCommand(value); return this; }

    /// <inheritdoc cref="SkUiEntry.SetReturnCommandParameter" />
    public new SkUiNumericEntry<T> SetReturnCommandParameter(object? value) { base.SetReturnCommandParameter(value); return this; }

    /// <inheritdoc cref="SkUiEntry.SetClearButtonVisibility" />
    public new SkUiNumericEntry<T> SetClearButtonVisibility(ClearButtonVisibility value) { base.SetClearButtonVisibility(value); return this; }

    #endregion

    #region Value

    private T? CoerceValue(T? value)
    {
        if (!_recoercing)
            _requestedValue = value;
        return Engine.Coerce(value);
    }

    private protected override void Recoerce()
    {
        _recoercing = true;
        try
        {
            Value = _requestedValue;
        }
        finally
        {
            _recoercing = false;
        }
        Engine.RangeChanged();
    }

    private void OnRangeChanged(Action<SkUiNumericEntryModel<T>> apply)
    {
        apply(Engine);
        Recoerce();
    }

    private void OnValueChanged(T? oldValue, T? newValue)
    {
        Engine.ValueChanged();
        ValueChanged?.Invoke(this, new SkUiValueChangedEventArgs<T?>(oldValue, newValue));
    }

    T? ISkUiNumericValueHost<T>.Value => Value;

    void ISkUiNumericValueHost<T>.SetValueFromUser(T? value) => Value = value;

    #endregion
}

/// <summary>
/// A numeric entry on <see cref="double"/>? (D16): see <see cref="SkUiNumericEntryBase"/> and <see cref="SkUiNumericEntry{T}"/>. For other number types (<see cref="int"/>,
/// <see cref="decimal"/>, …) use <see cref="SkUiNumericEntry{T}"/>. The Core twin is <see cref="Core.SkUiCoreNumericEntry"/>.
/// </summary>
public class SkUiNumericEntry : SkUiNumericEntry<double>
{
}
