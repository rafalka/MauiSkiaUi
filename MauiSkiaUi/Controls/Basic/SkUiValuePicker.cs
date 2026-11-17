using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace MauiSkiaUi;

/// <summary>
/// A drawn picker of one value of a type SkiaUi has no picker for (a color, a duration, a node of a tree): the field shows the value,
/// and the popup holds a drawn body of yours (<see cref="CreateValueContent"/>) in SkiaUi's popup chrome, on the app's popup system
/// like <see cref="SkUiPicker"/>'s list. Derive a closed type (<c>class ColorPicker : SkUiValuePicker&lt;Color?&gt;</c>), so XAML needs
/// no type arguments. Core twin: <see cref="Core.SkUiCoreValuePicker{T}"/>. SkiaUi control.
/// </summary>
/// <remarks>
/// <para>
/// <b>Value.</b> <see cref="Value"/> binds two-way; <c>null</c> is "nothing chosen" (the field shows <c>Placeholder</c>, else <c>Title</c>),
/// so a value type that can be empty is picked as its nullable (<c>SkUiValuePicker&lt;TimeSpan?&gt;</c>). The user's choices are written
/// as handler values, so the app's one-way bindings stay. Clearing (the clear buttons, <see cref="SkUiPickerBase.ClearSelection"/>) sets
/// it to <c>default</c>.
/// </para>
/// <para>
/// <b>Field.</b> <see cref="FormatValue"/> (by default <see cref="ValueFormat"/>, else the value's <c>ToString()</c>) gives the field's
/// text; <see cref="DescribeValue"/> what screen readers read when it differs.
/// </para>
/// <para>
/// Everything else is the picker's (<see cref="SkUiPickerBase"/>): <c>Title</c>, <c>Placeholder</c>, <c>UpdateMode</c>, the popup's
/// placement, width and presenter, the app's popup header and footer, the clear buttons, the leading view and field chrome,
/// <c>IsOpen</c>, keyboard and screen readers. A presenter that shows its own UI handles <see cref="SkUiValuePopupRequest{T}"/>.
/// </para>
/// </remarks>
/// <typeparam name="T">The value's type.</typeparam>
// The annotation is what BindableProperty.Create asks of a property type (it may create a default value) under trimming.
public abstract class SkUiValuePicker<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T> : SkUiPickerBase
{
    /// <summary>Bindable property for <see cref="Value"/> (two-way by default).</summary>
    public static readonly BindableProperty ValueProperty = BindableProperty.Create(nameof(Value), typeof(T), typeof(SkUiValuePicker<T>), default(T),
        BindingMode.TwoWay, propertyChanged: (picker, oldValue, newValue) => ((SkUiValuePicker<T>)picker).OnValueChanged((T)oldValue!, (T)newValue!));

    /// <summary>Bindable property for <see cref="ValueFormat"/>.</summary>
    public static readonly BindableProperty ValueFormatProperty = BindableProperty.Create(nameof(ValueFormat), typeof(string), typeof(SkUiValuePicker<T>), null,
        propertyChanged: (picker, _, _) => ((SkUiValuePicker<T>)picker).FieldTextChanged());

    private bool _fieldTextShown;

    /// <summary>
    /// Creates a picker with no value. The field's text is worked out when the picker is first measured (or the value changes after
    /// that), so <see cref="FormatValue"/> and the other overrides never run before the derived constructor.
    /// </summary>
    protected SkUiValuePicker() => SetFieldText(null);

    /// <summary>The chosen value; <c>null</c> (or <c>default</c>): none. Two-way by default.</summary>
    public T Value { get => (T)GetValue(ValueProperty)!; set => SetValue(ValueProperty, value); }

    /// <summary>
    /// A <c>string.Format</c> format of the field, with the value as <c>{0}</c> (<c>"{0:hh\\:mm}"</c>), in the current culture;
    /// <c>null</c> (default): <see cref="FormatValue"/>'s own text (the value's <c>ToString()</c>).
    /// </summary>
    public string? ValueFormat { get => (string?)GetValue(ValueFormatProperty); set => SetValue(ValueFormatProperty, value); }

    /// <summary>Raised when <see cref="Value"/> changed (the user in the popup, clearing, or the app), after <c>SelectionChangedCommand</c>.</summary>
    public event EventHandler<SkUiValueChangedEventArgs<T>>? ValueChanged;

    /// <summary>Sets <see cref="Value"/> (same as the property setter).</summary>
    public SkUiValuePicker<T> SetValue(T value) { Value = value; return this; }

    /// <summary>Sets <see cref="ValueFormat"/> (same as the property setter).</summary>
    public SkUiValuePicker<T> SetValueFormat(string? value) { ValueFormat = value; return this; }

    /// <summary>
    /// The popup's content for this opening: a body of yours that shows <see cref="SkUiValuePopupContent{T}.Value"/> and changes it.
    /// SkiaUi shows it in its popup chrome and applies its choices (at once, or on OK with <c>UpdateMode</c> <see cref="SkUiPickerUpdateMode.WhenFinished"/>).
    /// </summary>
    /// <param name="context">Where the popup goes, its title, the picker's text style and whether a choice waits for OK.</param>
    protected abstract SkUiValuePopupContent<T> CreateValueContent(SkUiPickerPopupContext context);

    /// <summary>Whether <paramref name="value"/> is a choice (the field shows it); by default, when it is not <c>null</c>.</summary>
    protected virtual bool HasValue(T value) => value is not null;

    /// <summary>The field's text for <paramref name="value"/> (a choice): <see cref="ValueFormat"/> applied, else its <c>ToString()</c>.</summary>
    protected virtual string? FormatValue(T value)
    {
        if (ValueFormat is { } format)
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

    /// <summary>What screen readers read for <paramref name="value"/> when it differs from <see cref="FormatValue"/> (a color's name); <c>null</c> (default): the field's text.</summary>
    protected virtual string? DescribeValue(T value) => null;

    /// <summary>Shows the field's text again (what <see cref="FormatValue"/> or <see cref="DescribeValue"/> read changed: a culture switch).</summary>
    protected void RefreshFieldText()
    {
        _fieldTextShown = true;
        var value = Value;
        if (HasValue(value))
            SetFieldText(FormatValue(value), DescribeValue(value));
        else
            SetFieldText(null);
    }

    /// <inheritdoc />
    /// <remarks>The value goes back to <c>default</c>.</remarks>
    protected sealed override void OnClearSelection() => WriteBack(ValueProperty, default(T));

    /// <inheritdoc />
    protected sealed override SkUiPickerPopupContent CreatePopupContent(SkUiPickerPopupContext context)
    {
        var content = CreateValueContent(context) ?? throw new InvalidOperationException($"{GetType().Name}.CreateValueContent returned null.");
        // Weak: a presenter that keeps the request (a popup still shown after its page went) keeps no picker alive.
        var picker = new WeakReference<SkUiValuePicker<T>>(this);
        content.Bind(() => picker.TryGetTarget(out var target) ? target.Value : default!, value =>
        {
            if (picker.TryGetTarget(out var target))
                target.WriteBack(ValueProperty, value);
        });
        return content;
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        if (!_fieldTextShown)
            RefreshFieldText();
        return base.MeasureContent(widthConstraint, heightConstraint);
    }

    /// <summary>The value or its format changed: the field's text, once the picker shows one (before its first measure, that measure works it out).</summary>
    private void FieldTextChanged()
    {
        if (_fieldTextShown)
            RefreshFieldText();
    }

    private void OnValueChanged(T oldValue, T newValue)
    {
        FieldTextChanged();
        if (SelectionChangedCommand is { } command && command.CanExecute(SelectionChangedCommandParameter))
            command.Execute(SelectionChangedCommandParameter);
        ValueChanged?.Invoke(this, new SkUiValueChangedEventArgs<T>(oldValue, newValue));
    }
}
