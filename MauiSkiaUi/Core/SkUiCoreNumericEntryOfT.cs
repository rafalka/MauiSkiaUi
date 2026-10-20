using System.Globalization;
using System.Numerics;
using System.Windows.Input;

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

    /// <inheritdoc cref="SkUiCoreNumericEntryBase.SetButtonTextColor" />
    public new SkUiCoreNumericEntry<T> SetButtonTextColor(Color? value) { base.SetButtonTextColor(value); return this; }

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

    /// <inheritdoc cref="SkUiCoreInputView.SetText" />
    public new SkUiCoreNumericEntry<T> SetText(string? value) { base.SetText(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetPlaceholderColor" />
    public new SkUiCoreNumericEntry<T> SetPlaceholderColor(Color? value) { base.SetPlaceholderColor(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetTextColor" />
    public new SkUiCoreNumericEntry<T> SetTextColor(Color? value) { base.SetTextColor(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetCharacterSpacing" />
    public new SkUiCoreNumericEntry<T> SetCharacterSpacing(double value) { base.SetCharacterSpacing(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetTextTransform" />
    public new SkUiCoreNumericEntry<T> SetTextTransform(TextTransform value) { base.SetTextTransform(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetFontAttributes" />
    public new SkUiCoreNumericEntry<T> SetFontAttributes(FontAttributes value) { base.SetFontAttributes(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetFontAutoScalingEnabled" />
    public new SkUiCoreNumericEntry<T> SetFontAutoScalingEnabled(bool value) { base.SetFontAutoScalingEnabled(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetVerticalTextAlignment" />
    public new SkUiCoreNumericEntry<T> SetVerticalTextAlignment(TextAlignment value) { base.SetVerticalTextAlignment(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetMaxLength" />
    public new SkUiCoreNumericEntry<T> SetMaxLength(int value) { base.SetMaxLength(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetKeyboard" />
    public new SkUiCoreNumericEntry<T> SetKeyboard(Keyboard value) { base.SetKeyboard(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetIsSpellCheckEnabled" />
    public new SkUiCoreNumericEntry<T> SetIsSpellCheckEnabled(bool value) { base.SetIsSpellCheckEnabled(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetIsTextPredictionEnabled" />
    public new SkUiCoreNumericEntry<T> SetIsTextPredictionEnabled(bool value) { base.SetIsTextPredictionEnabled(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetCursorPosition" />
    public new SkUiCoreNumericEntry<T> SetCursorPosition(int value) { base.SetCursorPosition(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetSelectionLength" />
    public new SkUiCoreNumericEntry<T> SetSelectionLength(int value) { base.SetSelectionLength(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetCaretColor" />
    public new SkUiCoreNumericEntry<T> SetCaretColor(Color? value) { base.SetCaretColor(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetSelectionColor" />
    public new SkUiCoreNumericEntry<T> SetSelectionColor(Color? value) { base.SetSelectionColor(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetAutofillHint" />
    public new SkUiCoreNumericEntry<T> SetAutofillHint(string? value) { base.SetAutofillHint(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetInputFormatters" />
    public new SkUiCoreNumericEntry<T> SetInputFormatters(params IEnumerable<SkUiTextInputFormatter> formatters) { base.SetInputFormatters(formatters); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetFormatsAssignedText" />
    public new SkUiCoreNumericEntry<T> SetFormatsAssignedText(bool value) { base.SetFormatsAssignedText(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetLeadingView" />
    public new SkUiCoreNumericEntry<T> SetLeadingView(SkUiCoreNode? value) { base.SetLeadingView(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetTrailingView" />
    public new SkUiCoreNumericEntry<T> SetTrailingView(SkUiCoreNode? value) { base.SetTrailingView(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetHasError" />
    public new SkUiCoreNumericEntry<T> SetHasError(bool value) { base.SetHasError(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetErrorText" />
    public new SkUiCoreNumericEntry<T> SetErrorText(string? value) { base.SetErrorText(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetHelperText" />
    public new SkUiCoreNumericEntry<T> SetHelperText(string? value) { base.SetHelperText(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetErrorColor" />
    public new SkUiCoreNumericEntry<T> SetErrorColor(Color? value) { base.SetErrorColor(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetFieldStyle" />
    public new SkUiCoreNumericEntry<T> SetFieldStyle(SkUiTextFieldStyle value) { base.SetFieldStyle(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetTextPadding" />
    public new SkUiCoreNumericEntry<T> SetTextPadding(Thickness? value) { base.SetTextPadding(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetFillColor" />
    public new SkUiCoreNumericEntry<T> SetFillColor(Color? value) { base.SetFillColor(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetBorderColor" />
    public new SkUiCoreNumericEntry<T> SetBorderColor(Color? value) { base.SetBorderColor(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetBorderWidth" />
    public new SkUiCoreNumericEntry<T> SetBorderWidth(double? value) { base.SetBorderWidth(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetCornerRadii" />
    public new SkUiCoreNumericEntry<T> SetCornerRadii(CornerRadius? value) { base.SetCornerRadii(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetCornerRadius" />
    public new SkUiCoreNumericEntry<T> SetCornerRadius(double? value) { base.SetCornerRadius(value); return this; }

    /// <inheritdoc cref="SkUiCoreEntry.SetIsPassword" />
    public new SkUiCoreNumericEntry<T> SetIsPassword(bool value) { base.SetIsPassword(value); return this; }

    /// <inheritdoc cref="SkUiCoreEntry.SetReturnType" />
    public new SkUiCoreNumericEntry<T> SetReturnType(ReturnType value) { base.SetReturnType(value); return this; }

    /// <inheritdoc cref="SkUiCoreEntry.SetReturnCommand" />
    public new SkUiCoreNumericEntry<T> SetReturnCommand(ICommand? value) { base.SetReturnCommand(value); return this; }

    /// <inheritdoc cref="SkUiCoreEntry.SetReturnCommandParameter" />
    public new SkUiCoreNumericEntry<T> SetReturnCommandParameter(object? value) { base.SetReturnCommandParameter(value); return this; }

    /// <inheritdoc cref="SkUiCoreEntry.SetClearButtonVisibility" />
    public new SkUiCoreNumericEntry<T> SetClearButtonVisibility(ClearButtonVisibility value) { base.SetClearButtonVisibility(value); return this; }

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
