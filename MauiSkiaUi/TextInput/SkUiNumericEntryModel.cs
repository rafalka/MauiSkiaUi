using System.Globalization;
using System.Numerics;

namespace MauiSkiaUi;

/// <summary>What the numeric entry engine (<see cref="SkUiNumericEntryModel"/>) needs from its control, on either layer.</summary>
internal interface ISkUiNumericEntryHost : ISkUiTransitionHost
{
    /// <summary>The control's text field engine.</summary>
    SkUiTextFieldModel Field { get; }

    /// <summary>Whether the field has keyboard focus.</summary>
    bool IsFocused { get; }

    /// <summary>Whether the user can change the value now: enabled and not read-only.</summary>
    bool CanChange { get; }

    /// <summary>The entry shows <paramref name="text"/>: the control's <c>Text</c> takes it (not as the user's edit).</summary>
    void ShowText(string text);
}

/// <summary>The control's value, of the entry's number type.</summary>
internal interface ISkUiNumericValueHost<T> where T : struct, INumber<T>, IMinMaxValue<T>
{
    /// <summary>The control's <c>Value</c>.</summary>
    T? Value { get; }

    /// <summary>The user made <paramref name="value"/> (committed text, a step): the control's <c>Value</c> takes it.</summary>
    void SetValueFromUser(T? value);
}

/// <summary>
/// The engine both numeric entries share (D16; <see cref="SkUiNumericEntryBase"/>, <see cref="Core.SkUiCoreNumericEntryBase"/>): a
/// composition of the entry's text field engine, not a new editor. The field shows the value formatted (<see cref="Format"/> in
/// <see cref="Culture"/>) and, while the user edits it, the plain number with everything selected; the user's input goes through the
/// numeric input rules (<see cref="SkUiNumericInputRules"/>, a D9 formatter); the text becomes the value on commit (Return, focus
/// leaving, a step) or on each keystroke (<see cref="ValueChangeMode"/>), clamped into the range on commit only. Steps (buttons,
/// keys, the wheel, screen readers) go through <see cref="SkUiTextFieldSteps"/>. The value stays the control's
/// (<see cref="ISkUiNumericValueHost{T}"/>); the typed part is <see cref="SkUiNumericEntryModel{T}"/>.
/// </summary>
internal abstract class SkUiNumericEntryModel : ISkUiTextFieldStepOwner
{
    private ISkUiNumericEntryHost? _host;
    // The entry is setting the field's text itself (not the user, not the app).
    private bool _showing;
    // The text the entry last showed, as the field shows it (transformed, formatted): MAUI applies a Text set inside a change of
    // Text after that change (it queues nested sets of one property), past _showing, so the entry's own text comes back as the app's.
    private string? _shownText;
    // A keystroke is setting the value (ValueChangeMode.OnKeystroke): the text stays as typed.
    private bool _editingValue;
    // The user changed the text since it last showed the value.
    private bool _dirty;

    #region State the controls copy in

    /// <summary>Whether empty text is a <c>null</c> value (default); otherwise it commits 0 clamped into the range.</summary>
    public bool AllowNull = true;

    /// <summary>The .NET numeric format the value is shown in while not edited (<c>"N2"</c>, <c>"C0"</c>, <c>"0.## kg"</c>); <c>null</c>: plain.</summary>
    public string? Format;

    /// <summary>The culture of the text; <c>null</c>: the current culture.</summary>
    public CultureInfo? Culture;

    /// <summary>When typed text becomes the value.</summary>
    public SkUiValueChangeMode ValueChangeMode;

    /// <summary>Whether the user may type (otherwise buttons and keys only).</summary>
    public bool IsEditable = true;

    /// <summary>Whether a step past an end of the range goes on from the other end.</summary>
    public bool AutoReverse;

    /// <summary>The most decimal digits typed and kept (integers: 0); <c>null</c>: any.</summary>
    public int? MaximumDecimalDigits;

    #endregion

    /// <summary>Connects the engine to its control: the field gets the input rules, the step buttons and the number keyboard.</summary>
    public void Attach(ISkUiNumericEntryHost host)
    {
        _host = host;
        var field = host.Field;
        field.Steps = new SkUiTextFieldSteps(field, this, host);
        field.Editor.InputRules = new SkUiNumericInputRules(this);
        field.SelectsAllOnFocusTap = true;
        // The text shows the value: MaxLength limits typing, it never cuts the value.
        field.CutsAssignedText = false;
        UpdateKeyboard();
    }

    private protected ISkUiNumericEntryHost Host => _host ?? throw new InvalidOperationException("The numeric entry engine is not attached.");

    private SkUiTextFieldModel Field => Host.Field;

    /// <summary>The culture's separators and the edit text's number format.</summary>
    internal SkUiNumericCulture Symbols => SkUiNumericCulture.For(Culture ?? CultureInfo.CurrentCulture);

    /// <summary>The decimal digits the user may type: 0 for integers, else <see cref="MaximumDecimalDigits"/> (<c>null</c>: any).</summary>
    internal int? DecimalDigits => IsInteger ? 0 : MaximumDecimalDigits;

    #region The typed value

    /// <summary>Whether the value's type holds whole numbers only.</summary>
    internal abstract bool IsInteger { get; }

    /// <summary>Whether the range has negative numbers (a minus sign can be typed).</summary>
    internal abstract bool AllowsNegative { get; }

    /// <summary>The value's text: the plain number while editing, else formatted; empty for <c>null</c>.</summary>
    private protected abstract string ValueText(bool editing);

    /// <summary>
    /// Makes <paramref name="text"/> the value: empty is <c>null</c> (or 0 clamped without <see cref="AllowNull"/>); a number is
    /// rounded to the decimal digits and clamped into the range, or, for a <paramref name="keystroke"/>, taken only when it is in the
    /// range already. Returns whether the text was a value.
    /// </summary>
    private protected abstract bool TryCommit(string text, bool keystroke);

    /// <summary>Whether a step can move the value (an empty value can always start).</summary>
    private protected abstract bool CanStepValue(int direction);

    /// <summary>One step of the increment or the large increment; returns whether the value moved.</summary>
    private protected abstract bool StepValue(int direction, bool large);

    /// <summary>Whether the plain number <paramref name="text"/> fits the value's type (the input rules refuse what would overflow it).</summary>
    internal abstract bool Fits(string text);

    #endregion

    #region Showing the value

    // While the user edits: focused and the text can be typed in.
    private bool IsEditing => Host.IsFocused && !Field.Editor.IsReadOnly;

    /// <summary>Shows the value: the plain number while the user edits, else formatted.</summary>
    public void ShowValue()
    {
        _dirty = false;
        var text = ValueText(IsEditing);
        _shownText = Field.ShownText(text);
        _showing = true;
        try
        {
            Host.ShowText(text);
        }
        finally
        {
            _showing = false;
        }
    }

    /// <summary>The control's value changed (the user, a step or the app): the buttons follow and, unless a keystroke set it, the text.</summary>
    public void ValueChanged()
    {
        Field.Steps?.ValueChanged();
        if (!_editingValue)
            ShowValue();
    }

    /// <summary>The range changed: the buttons, the screen reader's actions and the keyboard (a minus sign) follow.</summary>
    public void RangeChanged()
    {
        Field.Steps?.ValueChanged();
        UpdateKeyboard();
    }

    /// <summary>The format, the culture or the decimal digits changed: the text and the keyboard follow (unless the user is typing).</summary>
    public void OptionsChanged()
    {
        UpdateKeyboard();
        Field.Steps?.ValueChanged();
        if (!_dirty)
            ShowValue();
    }

    /// <summary>
    /// The field gained focus: while it can be typed in, it shows the plain number with everything selected (a tap that focused it
    /// keeps the selection). Lost: the text is committed and shown formatted.
    /// </summary>
    public void FocusChanged(bool focused)
    {
        if (focused)
        {
            ShowValue();
            if (IsEditing)
                Field.Editor.SelectAll();
            return;
        }
        if (_dirty)
            Commit();
        else
            ShowValue();
    }

    /// <summary><c>IsReadOnly</c> or <c>IsEditable</c> changed: the text shows the plain or the formatted number, and the buttons follow.</summary>
    public void EditabilityChanged()
    {
        Field.Steps?.ValueChanged();
        if (!_dirty)
            ShowValue();
    }

    private void UpdateKeyboard()
    {
        var keyboard = SkUiNumberKeyboard.Digits | (DecimalDigits == 0 ? 0 : SkUiNumberKeyboard.Decimal) | (AllowsNegative ? SkUiNumberKeyboard.Signed : 0);
        if (Field.NumberKeyboard == keyboard)
            return;
        Field.NumberKeyboard = keyboard;
        Field.OptionsChanged();
    }

    #endregion

    #region Text from the user and the app

    /// <summary>The user edited the text: with <see cref="SkUiValueChangeMode.OnKeystroke"/>, a number in the range becomes the value.</summary>
    public void TextEdited()
    {
        if (_showing)
            return;
        _shownText = null;
        _dirty = true;
        if (ValueChangeMode != SkUiValueChangeMode.OnKeystroke)
            return;
        _editingValue = true;
        try
        {
            TryCommit(Field.Editor.Text, keystroke: true);
        }
        finally
        {
            _editingValue = false;
        }
    }

    /// <summary>The app set the control's text: it is committed as typed text would be (not cut to <c>MaxLength</c>).</summary>
    public void TextAssigned()
    {
        if (_showing || Field.Editor.Text == _shownText)
            return;
        _dirty = true;
        Commit();
    }

    /// <summary>
    /// Commits the typed text (Return, focus leaving, before a step): it becomes the value, clamped into the range; text that is no
    /// number goes back to the value. The text then shows the value (leading zeros trimmed, clamped). Returns whether the text was a
    /// value.
    /// </summary>
    public bool Commit()
    {
        if (!_dirty)
            return true;
        Field.Editor.FinishComposing();
        var committed = Host.CanChange && TryCommit(Field.Editor.Text, keystroke: false);
        ShowValue();
        return committed;
    }

    #endregion

    #region Steps

    bool ISkUiTextFieldStepOwner.CanStep(int direction) => Host.CanChange && CanStepValue(direction);

    /// <summary>A step by the user: the typed text is committed first, then stepped (focus and the keyboard stay as they are).</summary>
    public bool Step(int direction, bool large)
    {
        if (!Host.CanChange)
            return false;
        if (_dirty)
            Commit();
        if (!CanStepValue(direction))
            return false;
        var moved = StepValue(direction, large);
        if (moved && IsEditing)
            Field.Editor.SelectAll();
        return moved;
    }

    /// <summary>Escape: typed text goes back to the value (everything selected); nothing to revert hands the key on.</summary>
    public bool Revert()
    {
        if (!_dirty)
            return false;
        ShowValue();
        if (IsEditing)
            Field.Editor.SelectAll();
        return true;
    }

    #endregion
}

/// <summary>The numeric entry engine for values of type <typeparamref name="T"/> (see <see cref="SkUiNumericEntryModel"/>).</summary>
internal sealed class SkUiNumericEntryModel<T> : SkUiNumericEntryModel where T : struct, INumber<T>, IMinMaxValue<T>
{
    private static readonly T Ten = T.CreateTruncating(10);

    /// <summary>The smallest value (default the type's).</summary>
    public T Minimum = T.MinValue;

    /// <summary>The largest value (default the type's).</summary>
    public T Maximum = T.MaxValue;

    /// <summary>A button's, Up / Down's and the wheel's step (default 1).</summary>
    public T Increment = T.One;

    /// <summary>Page Up / Page Down's step; <c>null</c>: 10 × <see cref="Increment"/>.</summary>
    public T? LargeIncrement;

    private ISkUiNumericValueHost<T> Values => (ISkUiNumericValueHost<T>)Host;

    internal override bool IsInteger => SkUiNumericText<T>.IsInteger;

    internal override bool AllowsNegative => Minimum < T.Zero;

    /// <summary>
    /// The value as the entry keeps it: <c>null</c> (with <see cref="SkUiNumericEntryModel.AllowNull"/>, else 0 within the range), or
    /// rounded to <see cref="SkUiNumericEntryModel.MaximumDecimalDigits"/> and within the range.
    /// </summary>
    public T? Coerce(T? value)
    {
        if (value is not { } number)
            return AllowNull ? null : T.Clamp(T.Zero, Minimum, Maximum);
        return T.Clamp(Round(number), Minimum, Maximum);
    }

    private T Round(T value) => DecimalDigits is { } digits ? SkUiNumericText<T>.Round(value, digits) : value;

    private T LargeStep => LargeIncrement ?? (Increment <= T.MaxValue / Ten ? Increment * Ten : T.MaxValue);

    private protected override string ValueText(bool editing) => Values.Value is not { } value ? string.Empty
        : editing ? SkUiNumericText<T>.Raw(value, Symbols) : SkUiNumericText<T>.Display(value, Format, Symbols.Culture);

    private protected override bool TryCommit(string text, bool keystroke)
    {
        T? value;
        if (!HasDigits(text))
        {
            // A lone sign or separator is no value yet; empty text is none.
            if (text.Trim().Length > 0)
                return false;
            if (!AllowNull)
            {
                if (keystroke)
                    return false;
                value = T.Clamp(T.Zero, Minimum, Maximum);
            }
            else
            {
                value = null;
            }
        }
        else if (!SkUiNumericText<T>.TryParse(text, Symbols, out var parsed))
        {
            return false;
        }
        else
        {
            var rounded = Round(parsed);
            // While typing, the range waits for the commit: with a minimum of 10, the 1 of 15 must be typeable.
            if (keystroke && (rounded < Minimum || rounded > Maximum))
                return false;
            value = T.Clamp(rounded, Minimum, Maximum);
        }
        // Exact: whether the value changes at all.
        if (!Nullable.Equals(Values.Value, value))
            Values.SetValueFromUser(value);
        return true;
    }

    private static bool HasDigits(string text)
    {
        foreach (var character in text)
            if (char.IsAsciiDigit(character))
                return true;
        return false;
    }

    private protected override bool CanStepValue(int direction)
    {
        if (Minimum >= Maximum)
            return false;
        if (Values.Value is not { } value)
            return true;
        return AutoReverse || SkUiStepperMath<T>.CanStep(value, Minimum, Maximum, direction);
    }

    private protected override bool StepValue(int direction, bool large)
    {
        var increment = large ? LargeStep : Increment;
        T next;
        if (Values.Value is not { } value)
        {
            // From no value: one step from 0 when the range holds 0; otherwise the end of the range nearest 0, whichever button (so
            // with a minimum of 10, − and + both start at 10).
            var start = T.Clamp(T.Zero, Minimum, Maximum);
            next = T.IsZero(start) && SkUiStepperMath<T>.CanStep(start, Minimum, Maximum, direction)
                ? SkUiStepperMath<T>.Step(start, increment, Minimum, Maximum, direction)
                : start;
        }
        else if (!SkUiStepperMath<T>.CanStep(value, Minimum, Maximum, direction))
        {
            if (!AutoReverse)
                return false;
            next = direction > 0 ? Minimum : Maximum;
        }
        else
        {
            next = SkUiStepperMath<T>.Step(value, increment, Minimum, Maximum, direction);
        }
        // Binary floating point is rounded by the increment (0.1 + 0.2 shows 0.3), as the steppers round.
        next = Round(SkUiStepperMath<T>.Coerce(next, Minimum, Maximum, SkUiStepperMath<T>.Digits(increment)));
        if (Nullable.Equals(Values.Value, next))
            return false;
        Values.SetValueFromUser(next);
        return true;
    }

    internal override bool Fits(string text) => SkUiNumericText<T>.TryParse(text, Symbols, out _);
}
