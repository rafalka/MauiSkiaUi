using System.Globalization;

namespace MauiSkiaUi.Core;

/// <summary>
/// The Core twin of <see cref="SkUiValuePicker{T}"/>: a drawn picker of one value of a type SkiaUi has no picker for, whose popup holds a
/// drawn body of yours (<see cref="CreateValueContent"/>) in SkiaUi's popup chrome. Derive a closed type; <c>null</c> is "nothing
/// chosen" (a value type that can be empty is picked as its nullable). Fluent <c>Set*</c> and <c>PropertyChanged</c>, as Core nodes.
/// </summary>
/// <typeparam name="T">The value's type.</typeparam>
public abstract class SkUiCoreValuePicker<T> : SkUiCorePickerBase
{
    private T _value = default!;
    private string? _valueFormat;

    /// <summary>Creates a picker with no value.</summary>
    protected SkUiCoreValuePicker() => RefreshFieldText();

    /// <summary>The chosen value; <c>null</c> (or <c>default</c>): none.</summary>
    public T Value { get => _value; set => SetValue(value); }

    /// <summary>A <c>string.Format</c> format of the field, with the value as <c>{0}</c>, in the current culture; <c>null</c>: the value's text.</summary>
    public string? ValueFormat { get => _valueFormat; set => SetValueFormat(value); }

    /// <summary>Raised when <see cref="Value"/> changed (the user in the popup, clearing, or the app), after <c>SelectionChangedCommand</c>.</summary>
    public event EventHandler<SkUiValueChangedEventArgs<T>>? ValueChanged;

    /// <summary>Sets <see cref="Value"/>.</summary>
    public SkUiCoreValuePicker<T> SetValue(T value)
    {
        var old = _value;
        if (!SetProperty(ref _value, value, nameof(Value)))
            return this;
        RefreshFieldText();
        if (SelectionChangedCommand is { } command && command.CanExecute(SelectionChangedCommandParameter))
            command.Execute(SelectionChangedCommandParameter);
        ValueChanged?.Invoke(this, new SkUiValueChangedEventArgs<T>(old, value));
        return this;
    }

    /// <summary>Sets <see cref="ValueFormat"/>.</summary>
    public SkUiCoreValuePicker<T> SetValueFormat(string? value)
    {
        if (SetProperty(ref _valueFormat, value, nameof(ValueFormat)))
            RefreshFieldText();
        return this;
    }

    /// <inheritdoc cref="SkUiValuePicker{T}.CreateValueContent" />
    protected abstract SkUiValuePopupContent<T> CreateValueContent(SkUiPickerPopupContext context);

    /// <summary>Whether <paramref name="value"/> is a choice (the field shows it); by default, when it is not <c>null</c>.</summary>
    protected virtual bool HasValue(T value) => value is not null;

    /// <summary>The field's text for <paramref name="value"/> (a choice): <see cref="ValueFormat"/> applied, else its <c>ToString()</c>.</summary>
    protected virtual string? FormatValue(T value)
    {
        if (_valueFormat is { } format)
        {
            try
            {
                return string.Format(CultureInfo.CurrentCulture, format, value);
            }
            catch (FormatException)
            {
                // A malformed format: the value's own text.
            }
        }
        return Convert.ToString(value, CultureInfo.CurrentCulture);
    }

    /// <summary>What screen readers read for <paramref name="value"/> when it differs from <see cref="FormatValue"/>; <c>null</c> (default): the field's text.</summary>
    protected virtual string? DescribeValue(T value) => null;

    /// <summary>Shows the field's text again (what <see cref="FormatValue"/> or <see cref="DescribeValue"/> read changed).</summary>
    protected void RefreshFieldText()
    {
        if (HasValue(_value))
            SetFieldText(FormatValue(_value), DescribeValue(_value));
        else
            SetFieldText(null);
    }

    /// <inheritdoc />
    /// <remarks>The value goes back to <c>default</c>.</remarks>
    protected sealed override void OnClearSelection() => SetValue(default!);

    /// <inheritdoc />
    protected sealed override SkUiPickerPopupContent CreatePopupContent(SkUiPickerPopupContext context)
    {
        var content = CreateValueContent(context) ?? throw new InvalidOperationException($"{GetType().Name}.CreateValueContent returned null.");
        content.Bind(() => _value, value => SetValue(value));
        return content;
    }
}
