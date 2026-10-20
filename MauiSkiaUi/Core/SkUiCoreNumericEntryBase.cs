using System.Globalization;

namespace MauiSkiaUi.Core;

/// <summary>
/// What every Core numeric entry shares, whatever its value's type (the twin of <see cref="SkUiNumericEntryBase"/>, on the same
/// engine; D16): an entry for numbers only with optional − / + buttons, the value formatted while not edited, typed text committed
/// and clamped on Return, focus loss or a step, buttons that step without focusing the field. Fluent <c>Set*</c> setters and
/// <see cref="System.ComponentModel.INotifyPropertyChanged"/>. The value is the derived entry's: <see cref="SkUiCoreNumericEntry"/>
/// on <see cref="double"/>, <see cref="SkUiCoreNumericEntry{T}"/> on any number type.
/// </summary>
/// <remarks>Only SkiaUi's numeric entries derive from it; derive from <see cref="SkUiCoreNumericEntry"/> or <see cref="SkUiCoreNumericEntry{T}"/>.</remarks>
public abstract class SkUiCoreNumericEntryBase : SkUiCoreEntry, ISkUiNumericEntryHost
{
    private protected SkUiCoreNumericEntryBase(SkUiNumericEntryModel numeric)
    {
        Numeric = numeric;
        numeric.Attach(this);
        // After the field's own focus handling (the base subscribed first).
        Focused += (_, _) => numeric.FocusChanged(true);
        Unfocused += (_, _) => numeric.FocusChanged(false);
    }

    /// <summary>The numeric engine shared with <see cref="SkUiNumericEntryBase"/>.</summary>
    internal SkUiNumericEntryModel Numeric { get; }

    private SkUiTextFieldSteps Steps => Model.Steps!;

    #region Properties

    /// <summary>Where the − and + buttons sit (default none; see <see cref="SkUiNumericEntryBase.ButtonPlacement"/>).</summary>
    public SkUiStepButtonPlacement ButtonPlacement { get => Steps.Placement; set => SetButtonPlacement(value); }

    /// <summary>Side by side or stacked buttons (see <see cref="SkUiNumericEntryBase.ButtonOrientation"/>).</summary>
    public StackOrientation ButtonOrientation { get => Steps.Orientation; set => SetButtonOrientation(value); }

    /// <summary>The − button's text, in place of the look's −.</summary>
    public string? DecrementText { get => Steps.DecrementText; set => SetDecrementText(value); }

    /// <summary>The + button's text, in place of the look's +.</summary>
    public string? IncrementText { get => Steps.IncrementText; set => SetIncrementText(value); }

    /// <summary>The buttons' texts' font; <c>null</c> (default): the entry's.</summary>
    public string? ButtonFontFamily { get => Steps.FontFamily; set => SetButtonFontFamily(value); }

    /// <summary>The color of − and + or of their texts; <c>null</c> (default): the entry's text color.</summary>
    public Color? ButtonTextColor { get => Steps.ButtonTextColor; set => SetButtonTextColor(value); }

    /// <summary>Whether the value can be empty (default <c>true</c>; see <see cref="SkUiNumericEntryBase.AllowNull"/>).</summary>
    public bool AllowNull { get => Numeric.AllowNull; set => SetAllowNull(value); }

    /// <summary>The .NET numeric format of the value while not edited (see <see cref="SkUiNumericEntryBase.Format"/>).</summary>
    public string? Format { get => Numeric.Format; set => SetFormat(value); }

    /// <summary>The culture of the shown and typed text; <c>null</c> (default): the current culture.</summary>
    public CultureInfo? Culture { get => Numeric.Culture; set => SetCulture(value); }

    /// <summary>When typed text becomes the value (see <see cref="SkUiNumericEntryBase.ValueChangeMode"/>).</summary>
    public SkUiValueChangeMode ValueChangeMode { get => Numeric.ValueChangeMode; set => SetValueChangeMode(value); }

    /// <summary>The most decimal digits typed and kept (see <see cref="SkUiNumericEntryBase.MaximumDecimalDigits"/>).</summary>
    public int? MaximumDecimalDigits { get => Numeric.MaximumDecimalDigits; set => SetMaximumDecimalDigits(value); }

    /// <summary>Whether the value can be typed (default <c>true</c>; see <see cref="SkUiNumericEntryBase.IsEditable"/>).</summary>
    public bool IsEditable { get => Numeric.IsEditable; set => SetIsEditable(value); }

    /// <summary>Whether a step past an end of the range goes on from the other end (default <c>false</c>).</summary>
    public bool AutoReverse { get => Numeric.AutoReverse; set => SetAutoReverse(value); }

    /// <summary>The shown text (formatted, or the plain number while edited). Set the value instead; text set here is committed as typed text.</summary>
    public new string? Text => base.Text;

    #endregion

    #region Setters

    /// <summary>Sets <see cref="ButtonPlacement"/>.</summary>
    public SkUiCoreNumericEntryBase SetButtonPlacement(SkUiStepButtonPlacement value)
    {
        ThrowIfUndefined(value);
        return UpdateSteps(ref Steps.Placement, value, nameof(ButtonPlacement));
    }

    /// <summary>Sets <see cref="ButtonOrientation"/>.</summary>
    public SkUiCoreNumericEntryBase SetButtonOrientation(StackOrientation value)
    {
        SkUiValidate.ThrowIfNotOrientation(value, nameof(value));
        return UpdateSteps(ref Steps.Orientation, value, nameof(ButtonOrientation));
    }

    /// <summary>Sets <see cref="DecrementText"/>.</summary>
    public SkUiCoreNumericEntryBase SetDecrementText(string? value) => UpdateSteps(ref Steps.DecrementText, value, nameof(DecrementText));

    /// <summary>Sets <see cref="IncrementText"/>.</summary>
    public SkUiCoreNumericEntryBase SetIncrementText(string? value) => UpdateSteps(ref Steps.IncrementText, value, nameof(IncrementText));

    /// <summary>Sets <see cref="ButtonFontFamily"/>.</summary>
    public SkUiCoreNumericEntryBase SetButtonFontFamily(string? value) => UpdateSteps(ref Steps.FontFamily, value, nameof(ButtonFontFamily));

    /// <summary>Sets <see cref="ButtonTextColor"/>.</summary>
    public SkUiCoreNumericEntryBase SetButtonTextColor(Color? value)
    {
        if (SetProperty(ref Steps.ButtonTextColor, value, nameof(ButtonTextColor)))
            Model.PaintChanged();
        return this;
    }

    /// <summary>Sets <see cref="AllowNull"/>.</summary>
    public SkUiCoreNumericEntryBase SetAllowNull(bool value)
    {
        if (SetProperty(ref Numeric.AllowNull, value, nameof(AllowNull)))
            Recoerce();
        return this;
    }

    /// <summary>Sets <see cref="Format"/>.</summary>
    public SkUiCoreNumericEntryBase SetFormat(string? value)
    {
        if (SetProperty(ref Numeric.Format, value, nameof(Format)))
            Numeric.OptionsChanged();
        return this;
    }

    /// <summary>Sets <see cref="Culture"/>.</summary>
    public SkUiCoreNumericEntryBase SetCulture(CultureInfo? value)
    {
        if (SetProperty(ref Numeric.Culture, value, nameof(Culture)))
            Numeric.OptionsChanged();
        return this;
    }

    /// <summary>Sets <see cref="ValueChangeMode"/>.</summary>
    public SkUiCoreNumericEntryBase SetValueChangeMode(SkUiValueChangeMode value)
    {
        ThrowIfUndefined(value);
        SetProperty(ref Numeric.ValueChangeMode, value, nameof(ValueChangeMode));
        return this;
    }

    /// <summary>Sets <see cref="MaximumDecimalDigits"/> (0–28, or <c>null</c>).</summary>
    public SkUiCoreNumericEntryBase SetMaximumDecimalDigits(int? value)
    {
        if (value is { } digits)
            ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)digits, (uint)SkUiNumericEntryBase.MaxDecimalDigits, nameof(value));
        if (!SetProperty(ref Numeric.MaximumDecimalDigits, value, nameof(MaximumDecimalDigits)))
            return this;
        Recoerce();
        Numeric.OptionsChanged();
        return this;
    }

    /// <summary>Sets <see cref="IsEditable"/>.</summary>
    public SkUiCoreNumericEntryBase SetIsEditable(bool value)
    {
        if (!SetProperty(ref Numeric.IsEditable, value, nameof(IsEditable)))
            return this;
        Model.SetReadOnly(IsReadOnly, value);
        Numeric.EditabilityChanged();
        return this;
    }

    /// <summary>Sets <see cref="AutoReverse"/>.</summary>
    public SkUiCoreNumericEntryBase SetAutoReverse(bool value)
    {
        if (SetProperty(ref Numeric.AutoReverse, value, nameof(AutoReverse)))
            Numeric.RangeChanged();
        return this;
    }

    private SkUiCoreNumericEntryBase UpdateSteps<T>(ref T field, T value, string name)
    {
        if (!SetProperty(ref field, value, name))
            return this;
        Steps.Changed();
        return this;
    }

    #endregion

    #region Changes

    /// <summary>The value is coerced again by the range and rules (the value the app asked for comes back when it fits).</summary>
    private protected abstract void Recoerce();

    /// <inheritdoc />
    private protected override void ApplyReadOnly(bool value)
    {
        Model.SetReadOnly(value, Numeric.IsEditable);
        Numeric.EditabilityChanged();
    }

    /// <inheritdoc />
    protected override void OnIsEnabledChanged()
    {
        base.OnIsEnabledChanged();
        Numeric.EditabilityChanged();
    }

    /// <inheritdoc />
    private protected override void OnTextAssigned() => Numeric.TextAssigned();

    /// <inheritdoc />
    private protected override void OnTextEdited() => Numeric.TextEdited();

    /// <summary>Return: the typed text is committed, then <see cref="SkUiCoreEntry.Completed"/> and <see cref="SkUiCoreEntry.ReturnCommand"/>.</summary>
    private protected override void OnReturnPressed()
    {
        Numeric.Commit();
        base.OnReturnPressed();
    }

    #endregion

    #region Host

    SkUiTextFieldModel ISkUiNumericEntryHost.Field => Model;

    bool ISkUiNumericEntryHost.IsFocused => IsFocused;

    bool ISkUiNumericEntryHost.CanChange => IsEnabled && !IsReadOnly;

    void ISkUiNumericEntryHost.ShowText(string text) => SetText(text);

    #endregion
}
