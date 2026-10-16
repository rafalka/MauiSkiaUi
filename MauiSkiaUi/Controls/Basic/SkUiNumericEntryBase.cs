using System.Globalization;

namespace MauiSkiaUi;

/// <summary>
/// What every SkUi numeric entry shares, whatever its value's type (D16): an entry for numbers only, with optional − / + buttons
/// in its chrome. The field shows the value formatted (<see cref="Format"/> in <see cref="Culture"/>) and, while the user edits it,
/// the plain number with everything selected; typing takes digits, the culture's decimal separator and (when the range has
/// negatives) a minus sign, and pasted text is cleaned. The text becomes the value on Return, when focus leaves and before a step
/// (or on every keystroke, <see cref="ValueChangeMode"/>), clamped into the range then, never while typing. Buttons
/// (<see cref="ButtonPlacement"/>, <see cref="ButtonOrientation"/>) step without focusing the field or showing the keyboard, and
/// repeat while held; Up / Down and Page Up / Page Down step, Escape reverts what was typed, the wheel steps while focused. The
/// keyboard is picked from the value's type and range. The entry's members (<see cref="SkUiEntry"/>, <see cref="SkUiInputView"/>)
/// work as on an entry; <see cref="Text"/> is the shown text. The value is the derived entry's: <see cref="SkUiNumericEntry"/> on
/// <see cref="double"/>, <see cref="SkUiNumericEntry{T}"/> on any number type. The Core twin is <see cref="Core.SkUiCoreNumericEntryBase"/>.
/// </summary>
/// <remarks>Only SkiaUi's numeric entries derive from it; derive from <see cref="SkUiNumericEntry"/> or <see cref="SkUiNumericEntry{T}"/>.</remarks>
public abstract class SkUiNumericEntryBase : SkUiEntry, ISkUiNumericEntryHost
{
    private protected SkUiNumericEntryBase(SkUiNumericEntryModel numeric)
    {
        Numeric = numeric;
        numeric.Attach(this);
    }

    /// <summary>The numeric engine shared with the Core entries.</summary>
    internal SkUiNumericEntryModel Numeric { get; }

    private SkUiTextFieldSteps Steps => Model.Steps!;

    #region Bindable properties

    /// <summary>Bindable property for <see cref="ButtonPlacement"/>.</summary>
    public static readonly BindableProperty ButtonPlacementProperty = BindableProperty.Create(nameof(ButtonPlacement), typeof(SkUiStepButtonPlacement),
        typeof(SkUiNumericEntryBase), SkUiStepButtonPlacement.None,
        validateValue: (_, value) => Enum.IsDefined((SkUiStepButtonPlacement)value),
        propertyChanged: (view, _, value) => ((SkUiNumericEntryBase)view).ApplySteps(steps => steps.Placement = (SkUiStepButtonPlacement)value));

    /// <summary>Bindable property for <see cref="ButtonOrientation"/>.</summary>
    public static readonly BindableProperty ButtonOrientationProperty = BindableProperty.Create(nameof(ButtonOrientation), typeof(StackOrientation),
        typeof(SkUiNumericEntryBase), StackOrientation.Horizontal, validateValue: SkUiValidate.Orientation,
        propertyChanged: (view, _, value) => ((SkUiNumericEntryBase)view).ApplySteps(steps => steps.Orientation = (StackOrientation)value));

    /// <summary>Bindable property for <see cref="DecrementText"/>.</summary>
    public static readonly BindableProperty DecrementTextProperty = BindableProperty.Create(nameof(DecrementText), typeof(string), typeof(SkUiNumericEntryBase), null,
        propertyChanged: (view, _, value) => ((SkUiNumericEntryBase)view).ApplySteps(steps => steps.DecrementText = (string?)value));

    /// <summary>Bindable property for <see cref="IncrementText"/>.</summary>
    public static readonly BindableProperty IncrementTextProperty = BindableProperty.Create(nameof(IncrementText), typeof(string), typeof(SkUiNumericEntryBase), null,
        propertyChanged: (view, _, value) => ((SkUiNumericEntryBase)view).ApplySteps(steps => steps.IncrementText = (string?)value));

    /// <summary>Bindable property for <see cref="ButtonFontFamily"/>.</summary>
    public static readonly BindableProperty ButtonFontFamilyProperty = BindableProperty.Create(nameof(ButtonFontFamily), typeof(string), typeof(SkUiNumericEntryBase), null,
        propertyChanged: (view, _, value) => ((SkUiNumericEntryBase)view).ApplySteps(steps => steps.FontFamily = (string?)value));

    /// <summary>Bindable property for <see cref="ButtonTextColor"/>.</summary>
    public static readonly BindableProperty ButtonTextColorProperty = BindableProperty.Create(nameof(ButtonTextColor), typeof(Color), typeof(SkUiNumericEntryBase), null,
        propertyChanged: (view, _, value) =>
        {
            var entry = (SkUiNumericEntryBase)view;
            entry.Steps.ButtonTextColor = (Color?)value;
            entry.Model.PaintChanged();
        });

    /// <summary>Bindable property for <see cref="AllowNull"/>.</summary>
    public static readonly BindableProperty AllowNullProperty = BindableProperty.Create(nameof(AllowNull), typeof(bool), typeof(SkUiNumericEntryBase), true,
        propertyChanged: (view, _, value) => ((SkUiNumericEntryBase)view).ApplyValueRule(numeric => numeric.AllowNull = (bool)value));

    /// <summary>Bindable property for <see cref="Format"/>.</summary>
    public static readonly BindableProperty FormatProperty = BindableProperty.Create(nameof(Format), typeof(string), typeof(SkUiNumericEntryBase), null,
        propertyChanged: (view, _, value) => ((SkUiNumericEntryBase)view).ApplyOptions(numeric => numeric.Format = (string?)value));

    /// <summary>Bindable property for <see cref="Culture"/>.</summary>
    public static readonly BindableProperty CultureProperty = BindableProperty.Create(nameof(Culture), typeof(CultureInfo), typeof(SkUiNumericEntryBase), null,
        propertyChanged: (view, _, value) => ((SkUiNumericEntryBase)view).ApplyOptions(numeric => numeric.Culture = (CultureInfo?)value));

    /// <summary>Bindable property for <see cref="ValueChangeMode"/>.</summary>
    public static readonly BindableProperty ValueChangeModeProperty = BindableProperty.Create(nameof(ValueChangeMode), typeof(SkUiValueChangeMode),
        typeof(SkUiNumericEntryBase), SkUiValueChangeMode.OnCompleted,
        validateValue: (_, value) => Enum.IsDefined((SkUiValueChangeMode)value),
        propertyChanged: (view, _, value) => ((SkUiNumericEntryBase)view).Numeric.ValueChangeMode = (SkUiValueChangeMode)value);

    /// <summary>Bindable property for <see cref="MaximumDecimalDigits"/>.</summary>
    public static readonly BindableProperty MaximumDecimalDigitsProperty = BindableProperty.Create(nameof(MaximumDecimalDigits), typeof(int?),
        typeof(SkUiNumericEntryBase), null,
        validateValue: (_, value) => value is null or int and >= 0 and <= MaxDecimalDigits,
        propertyChanged: (view, _, value) => ((SkUiNumericEntryBase)view).ApplyValueRule(numeric => numeric.MaximumDecimalDigits = (int?)value, options: true));

    /// <summary>Bindable property for <see cref="IsEditable"/>.</summary>
    public static readonly BindableProperty IsEditableProperty = BindableProperty.Create(nameof(IsEditable), typeof(bool), typeof(SkUiNumericEntryBase), true,
        propertyChanged: (view, _, value) => ((SkUiNumericEntryBase)view).OnIsEditableChanged((bool)value));

    /// <summary>Bindable property for <see cref="AutoReverse"/>.</summary>
    public static readonly BindableProperty AutoReverseProperty = BindableProperty.Create(nameof(AutoReverse), typeof(bool), typeof(SkUiNumericEntryBase), false,
        propertyChanged: (view, _, value) =>
        {
            var entry = (SkUiNumericEntryBase)view;
            entry.Numeric.AutoReverse = (bool)value;
            entry.Numeric.RangeChanged();
        });

    /// <summary>The most <see cref="MaximumDecimalDigits"/> can be (<see cref="decimal"/>'s 28).</summary>
    internal const int MaxDecimalDigits = 28;

    #endregion

    #region Properties

    /// <summary>
    /// Where the − and + buttons sit (default <see cref="SkUiStepButtonPlacement.None"/>: none; keys, the wheel and screen readers
    /// still step): at the end side, the start side, or split (− at the start, + at the end). They follow right-to-left layouts.
    /// </summary>
    public SkUiStepButtonPlacement ButtonPlacement { get => (SkUiStepButtonPlacement)GetValue(ButtonPlacementProperty); set => SetValue(ButtonPlacementProperty, value); }

    /// <summary>
    /// Whether the buttons sit side by side (<see cref="StackOrientation.Horizontal"/>, the default: − then +) or stacked
    /// (<see cref="StackOrientation.Vertical"/>: + above −, a spinner; split, + in a bar above the text and − in one below it).
    /// </summary>
    public StackOrientation ButtonOrientation { get => (StackOrientation)GetValue(ButtonOrientationProperty); set => SetValue(ButtonOrientationProperty, value); }

    /// <summary>The − button's text, in place of the look's − (a word, or an icon font's symbol with <see cref="ButtonFontFamily"/>).</summary>
    public string? DecrementText { get => (string?)GetValue(DecrementTextProperty); set => SetValue(DecrementTextProperty, value); }

    /// <summary>The + button's text, in place of the look's +.</summary>
    public string? IncrementText { get => (string?)GetValue(IncrementTextProperty); set => SetValue(IncrementTextProperty, value); }

    /// <summary>The font of <see cref="DecrementText"/> and <see cref="IncrementText"/>; <c>null</c> (default): the entry's <see cref="SkUiInputView.FontFamily"/>.</summary>
    public string? ButtonFontFamily { get => (string?)GetValue(ButtonFontFamilyProperty); set => SetValue(ButtonFontFamilyProperty, value); }

    /// <summary>The color of − and + or of their texts; <c>null</c> (default): the entry's text color.</summary>
    public Color? ButtonTextColor { get => (Color?)GetValue(ButtonTextColorProperty); set => SetValue(ButtonTextColorProperty, value); }

    /// <summary>
    /// Whether the value can be empty (default <c>true</c>: empty text is a <c>null</c> value, the placeholder shows). With
    /// <c>false</c>, empty text commits 0 (clamped into the range), and a <c>null</c> value set by the app becomes that.
    /// </summary>
    public bool AllowNull { get => (bool)GetValue(AllowNullProperty); set => SetValue(AllowNullProperty, value); }

    /// <summary>
    /// The .NET numeric format the value is shown in while the field is not being edited (<c>"N2"</c>, <c>"C0"</c>, <c>"P1"</c>,
    /// <c>"0.## kg"</c>); <c>null</c> (default): plain. While edited, the field shows the plain number. The format does not round
    /// <see cref="SkUiNumericEntry{T}.Value"/>: <see cref="MaximumDecimalDigits"/> does.
    /// </summary>
    public string? Format { get => (string?)GetValue(FormatProperty); set => SetValue(FormatProperty, value); }

    /// <summary>The culture of the shown and typed text (its decimal separator, group separator, currency); <c>null</c> (default): the current culture.</summary>
    public CultureInfo? Culture { get => (CultureInfo?)GetValue(CultureProperty); set => SetValue(CultureProperty, value); }

    /// <summary>
    /// When typed text becomes the value: <see cref="SkUiValueChangeMode.OnCompleted"/> (default: Return, focus leaving, a step) or
    /// <see cref="SkUiValueChangeMode.OnKeystroke"/> (also every edit that makes a number within the range).
    /// </summary>
    public SkUiValueChangeMode ValueChangeMode { get => (SkUiValueChangeMode)GetValue(ValueChangeModeProperty); set => SetValue(ValueChangeModeProperty, value); }

    /// <summary>
    /// The most decimal digits that can be typed, and that values are rounded to (half away from zero; default <c>null</c>: any;
    /// integer types: none). The only rounding the entry does.
    /// </summary>
    public int? MaximumDecimalDigits { get => (int?)GetValue(MaximumDecimalDigitsProperty); set => SetValue(MaximumDecimalDigitsProperty, value); }

    /// <summary>
    /// Whether the value can be typed (default <c>true</c>); with <c>false</c>, the buttons, keys and wheel still step and no keyboard
    /// shows. <see cref="SkUiInputView.IsReadOnly"/> stops every change, the buttons included.
    /// </summary>
    public bool IsEditable { get => (bool)GetValue(IsEditableProperty); set => SetValue(IsEditableProperty, value); }

    /// <summary>Whether a step past <c>Maximum</c> goes on from <c>Minimum</c>, and below <c>Minimum</c> from <c>Maximum</c> (default <c>false</c>).</summary>
    public bool AutoReverse { get => (bool)GetValue(AutoReverseProperty); set => SetValue(AutoReverseProperty, value); }

    /// <summary>The shown text (formatted, or the plain number while edited). Set the value instead; text set here is committed as typed text.</summary>
    public new string? Text => base.Text;

    #endregion

    #region Fluent setters

    /// <summary>Sets <see cref="ButtonPlacement"/>.</summary>
    public SkUiNumericEntryBase SetButtonPlacement(SkUiStepButtonPlacement value) { ThrowIfUndefined(value); ButtonPlacement = value; return this; }

    /// <summary>Sets <see cref="ButtonOrientation"/>.</summary>
    public SkUiNumericEntryBase SetButtonOrientation(StackOrientation value) { SkUiValidate.ThrowIfNotOrientation(value, nameof(value)); ButtonOrientation = value; return this; }

    /// <summary>Sets <see cref="DecrementText"/>.</summary>
    public SkUiNumericEntryBase SetDecrementText(string? value) { DecrementText = value; return this; }

    /// <summary>Sets <see cref="IncrementText"/>.</summary>
    public SkUiNumericEntryBase SetIncrementText(string? value) { IncrementText = value; return this; }

    /// <summary>Sets <see cref="ButtonFontFamily"/>.</summary>
    public SkUiNumericEntryBase SetButtonFontFamily(string? value) { ButtonFontFamily = value; return this; }

    /// <summary>Sets <see cref="ButtonTextColor"/>.</summary>
    public SkUiNumericEntryBase SetButtonTextColor(Color? value) { ButtonTextColor = value; return this; }

    /// <summary>Sets <see cref="AllowNull"/>.</summary>
    public SkUiNumericEntryBase SetAllowNull(bool value) { AllowNull = value; return this; }

    /// <summary>Sets <see cref="Format"/>.</summary>
    public SkUiNumericEntryBase SetFormat(string? value) { Format = value; return this; }

    /// <summary>Sets <see cref="Culture"/>.</summary>
    public SkUiNumericEntryBase SetCulture(CultureInfo? value) { Culture = value; return this; }

    /// <summary>Sets <see cref="ValueChangeMode"/>.</summary>
    public SkUiNumericEntryBase SetValueChangeMode(SkUiValueChangeMode value) { ThrowIfUndefined(value); ValueChangeMode = value; return this; }

    /// <summary>Sets <see cref="MaximumDecimalDigits"/>.</summary>
    public SkUiNumericEntryBase SetMaximumDecimalDigits(int? value)
    {
        if (value is { } digits)
            ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)digits, (uint)MaxDecimalDigits, nameof(value));
        MaximumDecimalDigits = value;
        return this;
    }

    /// <summary>Sets <see cref="IsEditable"/>.</summary>
    public SkUiNumericEntryBase SetIsEditable(bool value) { IsEditable = value; return this; }

    /// <summary>Sets <see cref="AutoReverse"/>.</summary>
    public SkUiNumericEntryBase SetAutoReverse(bool value) { AutoReverse = value; return this; }

    #endregion

    #region Changes

    private void ApplySteps(Action<SkUiTextFieldSteps> apply)
    {
        apply(Steps);
        Steps.Changed();
    }

    private void ApplyOptions(Action<SkUiNumericEntryModel> apply)
    {
        apply(Numeric);
        Numeric.OptionsChanged();
    }

    // A rule the value is kept by changed: the value is coerced again (the asked value comes back when it fits).
    private void ApplyValueRule(Action<SkUiNumericEntryModel> apply, bool options = false)
    {
        apply(Numeric);
        Recoerce();
        if (options)
            Numeric.OptionsChanged();
    }

    /// <summary>The value is coerced again by the range and rules (the value the app asked for comes back when it fits).</summary>
    private protected abstract void Recoerce();

    private void OnIsEditableChanged(bool value)
    {
        Numeric.IsEditable = value;
        Model.SetReadOnly(IsReadOnly, value);
        Numeric.EditabilityChanged();
    }

    /// <inheritdoc />
    private protected override void OnIsReadOnlyChanged(bool value)
    {
        Model.SetReadOnly(value, IsEditable);
        Numeric.EditabilityChanged();
    }

    /// <inheritdoc />
    private protected override void OnTextAssigned() => Numeric.TextAssigned();

    /// <inheritdoc />
    private protected override void OnTextEdited() => Numeric.TextEdited();

    /// <summary>Return: the typed text is committed, then <see cref="SkUiEntry.Completed"/> and <see cref="SkUiEntry.ReturnCommand"/>.</summary>
    private protected override void OnReturnPressed()
    {
        Numeric.Commit();
        base.OnReturnPressed();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        switch (propertyName)
        {
            case nameof(IsFocused):
                Numeric.FocusChanged(IsFocused);
                break;
            case nameof(IsEnabled):
                Numeric.EditabilityChanged();
                break;
        }
    }

    #endregion

    #region Host

    SkUiTextFieldModel ISkUiNumericEntryHost.Field => Model;

    bool ISkUiNumericEntryHost.IsFocused => IsFocused;

    bool ISkUiNumericEntryHost.CanChange => IsEnabled && !IsReadOnly;

    void ISkUiNumericEntryHost.ShowText(string text) => SetValue(TextProperty, text);

    #endregion
}
