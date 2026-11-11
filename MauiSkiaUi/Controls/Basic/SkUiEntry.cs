using System.Windows.Input;

namespace MauiSkiaUi;

/// <summary>
/// A single-line text field drawn by SkiaUi (MAUI's <c>Entry</c>): the text, caret, selection and placeholder are drawn on the
/// surface, the look draws the chrome, and the platform's keyboard and input methods (composition, autocorrect, dictation,
/// autofill) reach it through the surface's text input proxy. MAUI's <c>Entry</c> API by name, so a page ports by changing the
/// prefix; beyond MAUI: <see cref="SkUiInputView.CaretColor"/>, <see cref="SkUiInputView.SelectionColor"/> and
/// <see cref="SkUiInputView.AutofillHint"/>. The Core twin is <see cref="Core.SkUiCoreEntry"/>.
/// </summary>
public class SkUiEntry : SkUiInputView
{
    /// <summary>Creates an empty entry.</summary>
    public SkUiEntry()
    {
    }

    /// <summary>Bindable property for <see cref="IsPassword"/>.</summary>
    public static readonly BindableProperty IsPasswordProperty = BindableProperty.Create(nameof(IsPassword), typeof(bool), typeof(SkUiEntry), false,
        propertyChanged: (view, _, value) => ((SkUiEntry)view).OnIsPasswordChanged((bool)value));

    /// <summary>Bindable property for <see cref="IsPasswordRevealed"/>.</summary>
    public static readonly BindableProperty IsPasswordRevealedProperty = BindableProperty.Create(nameof(IsPasswordRevealed), typeof(bool), typeof(SkUiEntry), false,
        BindingMode.TwoWay, propertyChanged: (view, _, value) => ((SkUiEntry)view).Model.SetPasswordRevealed((bool)value));

    /// <summary>Bindable property for <see cref="ShowsPasswordRevealButton"/>.</summary>
    public static readonly BindableProperty ShowsPasswordRevealButtonProperty = BindableProperty.Create(nameof(ShowsPasswordRevealButton), typeof(bool),
        typeof(SkUiEntry), false, propertyChanged: (view, _, value) => ((SkUiEntry)view).Model.SetShowsPasswordRevealButton((bool)value));

    /// <summary>Bindable property for <see cref="HidesPasswordOnUnfocus"/>.</summary>
    public static readonly BindableProperty HidesPasswordOnUnfocusProperty = BindableProperty.Create(nameof(HidesPasswordOnUnfocus), typeof(bool),
        typeof(SkUiEntry), true, propertyChanged: (view, _, value) => ((SkUiEntry)view).Model.HidesPasswordOnUnfocus = (bool)value);

    /// <summary>Bindable property for <see cref="PasswordRevealMode"/>.</summary>
    public static readonly BindableProperty PasswordRevealModeProperty = BindableProperty.Create(nameof(PasswordRevealMode), typeof(SkUiPasswordRevealMode),
        typeof(SkUiEntry), SkUiPasswordRevealMode.Toggle,
        validateValue: (_, value) => Enum.IsDefined((SkUiPasswordRevealMode)value),
        propertyChanged: (view, _, value) => ((SkUiEntry)view).Model.PasswordRevealMode = (SkUiPasswordRevealMode)value);

    /// <summary>Bindable property for <see cref="ReturnType"/>.</summary>
    public static readonly BindableProperty ReturnTypeProperty = BindableProperty.Create(nameof(ReturnType), typeof(ReturnType), typeof(SkUiEntry), ReturnType.Default,
        validateValue: (_, value) => Enum.IsDefined((ReturnType)value),
        propertyChanged: (view, _, value) => ((SkUiEntry)view).ApplyOptions(model => model.ReturnType = (ReturnType)value));

    /// <summary>Bindable property for <see cref="ReturnCommand"/>.</summary>
    public static readonly BindableProperty ReturnCommandProperty = BindableProperty.Create(nameof(ReturnCommand), typeof(ICommand), typeof(SkUiEntry));

    /// <summary>Bindable property for <see cref="ReturnCommandParameter"/>.</summary>
    public static readonly BindableProperty ReturnCommandParameterProperty = BindableProperty.Create(nameof(ReturnCommandParameter), typeof(object), typeof(SkUiEntry));

    /// <summary>Bindable property for <see cref="ClearButtonVisibility"/>.</summary>
    public static readonly BindableProperty ClearButtonVisibilityProperty = BindableProperty.Create(nameof(ClearButtonVisibility), typeof(ClearButtonVisibility), typeof(SkUiEntry),
        ClearButtonVisibility.Never,
        validateValue: (_, value) => Enum.IsDefined((ClearButtonVisibility)value),
        propertyChanged: (view, _, value) => ((SkUiEntry)view).Apply(model => model.ClearButtonVisibility = (ClearButtonVisibility)value));

    /// <summary>Whether the text is masked (bullets); copying is off and the keyboard offers no suggestions.</summary>
    public bool IsPassword { get => (bool)GetValue(IsPasswordProperty); set => SetValue(IsPasswordProperty, value); }

    /// <summary>
    /// Whether a password's text is drawn instead of bullets (two-way: the reveal button and <see cref="HidesPasswordOnUnfocus"/>
    /// change it). The field stays a password field for the platform (the keyboard, autofill and password managers see one) and
    /// copying stays off; screen readers read the text only while it shows. No effect unless <see cref="IsPassword"/>.
    /// </summary>
    public bool IsPasswordRevealed { get => (bool)GetValue(IsPasswordRevealedProperty); set => SetValue(IsPasswordRevealedProperty, value); }

    /// <summary>
    /// Whether a password field draws the look's reveal button at its end (an eye; default <c>false</c>): a tap shows or hides the
    /// text (<see cref="IsPasswordRevealed"/>) without moving the focus. Screen readers read it as "Show password" / "Hide password".
    /// </summary>
    public bool ShowsPasswordRevealButton { get => (bool)GetValue(ShowsPasswordRevealButtonProperty); set => SetValue(ShowsPasswordRevealButtonProperty, value); }

    /// <summary>Whether a revealed password is masked again when the field loses focus (default <c>true</c>).</summary>
    public bool HidesPasswordOnUnfocus { get => (bool)GetValue(HidesPasswordOnUnfocusProperty); set => SetValue(HidesPasswordOnUnfocusProperty, value); }

    /// <summary>
    /// How the reveal button shows the text: a tap toggles it (<see cref="SkUiPasswordRevealMode.Toggle"/>, the default), or it shows
    /// only while the button is held down (<see cref="SkUiPasswordRevealMode.WhilePressed"/>; screen readers still toggle it).
    /// </summary>
    public SkUiPasswordRevealMode PasswordRevealMode { get => (SkUiPasswordRevealMode)GetValue(PasswordRevealModeProperty); set => SetValue(PasswordRevealModeProperty, value); }

    /// <summary>The soft keyboard's return key.</summary>
    public ReturnType ReturnType { get => (ReturnType)GetValue(ReturnTypeProperty); set => SetValue(ReturnTypeProperty, value); }

    /// <summary>Runs on the return key (after <see cref="Completed"/>) when it can execute.</summary>
    public ICommand? ReturnCommand { get => (ICommand?)GetValue(ReturnCommandProperty); set => SetValue(ReturnCommandProperty, value); }

    /// <summary>The parameter of <see cref="ReturnCommand"/>.</summary>
    public object? ReturnCommandParameter { get => GetValue(ReturnCommandParameterProperty); set => SetValue(ReturnCommandParameterProperty, value); }

    /// <summary>Whether a clear button shows while the field is focused and has text (default <see cref="ClearButtonVisibility.Never"/>).</summary>
    public ClearButtonVisibility ClearButtonVisibility { get => (ClearButtonVisibility)GetValue(ClearButtonVisibilityProperty); set => SetValue(ClearButtonVisibilityProperty, value); }

    /// <summary>Raised when the user finishes with the return key (before <see cref="ReturnCommand"/>).</summary>
    public event EventHandler? Completed;

    /// <summary>Sets <see cref="IsPassword"/>.</summary>
    public SkUiEntry SetIsPassword(bool value) { IsPassword = value; return this; }

    /// <summary>Sets <see cref="IsPasswordRevealed"/>.</summary>
    public SkUiEntry SetIsPasswordRevealed(bool value) { IsPasswordRevealed = value; return this; }

    /// <summary>Sets <see cref="ShowsPasswordRevealButton"/>.</summary>
    public SkUiEntry SetShowsPasswordRevealButton(bool value) { ShowsPasswordRevealButton = value; return this; }

    /// <summary>Sets <see cref="HidesPasswordOnUnfocus"/>.</summary>
    public SkUiEntry SetHidesPasswordOnUnfocus(bool value) { HidesPasswordOnUnfocus = value; return this; }

    /// <summary>Sets <see cref="PasswordRevealMode"/>.</summary>
    public SkUiEntry SetPasswordRevealMode(SkUiPasswordRevealMode value) { ThrowIfUndefined(value); PasswordRevealMode = value; return this; }

    /// <summary>Sets <see cref="ReturnType"/>.</summary>
    public SkUiEntry SetReturnType(ReturnType value) { ThrowIfUndefined(value); ReturnType = value; return this; }

    /// <summary>Sets <see cref="ReturnCommand"/>.</summary>
    public SkUiEntry SetReturnCommand(ICommand? value) { ReturnCommand = value; return this; }

    /// <summary>Sets <see cref="ReturnCommandParameter"/>.</summary>
    public SkUiEntry SetReturnCommandParameter(object? value) { ReturnCommandParameter = value; return this; }

    /// <summary>Sets <see cref="ClearButtonVisibility"/>.</summary>
    public SkUiEntry SetClearButtonVisibility(ClearButtonVisibility value) { ThrowIfUndefined(value); ClearButtonVisibility = value; return this; }

    /// <inheritdoc cref="SkUiInputView.SetText" />
    public new SkUiEntry SetText(string? value) { base.SetText(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetPlaceholder" />
    public new SkUiEntry SetPlaceholder(string? value) { base.SetPlaceholder(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetPlaceholderColor" />
    public new SkUiEntry SetPlaceholderColor(Color? value) { base.SetPlaceholderColor(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetTextColor" />
    public new SkUiEntry SetTextColor(Color? value) { base.SetTextColor(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetCharacterSpacing" />
    public new SkUiEntry SetCharacterSpacing(double value) { base.SetCharacterSpacing(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetTextTransform" />
    public new SkUiEntry SetTextTransform(TextTransform value) { base.SetTextTransform(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetFontFamily" />
    public new SkUiEntry SetFontFamily(string? value) { base.SetFontFamily(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetFontSize" />
    public new SkUiEntry SetFontSize(double value) { base.SetFontSize(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetFontAttributes" />
    public new SkUiEntry SetFontAttributes(FontAttributes value) { base.SetFontAttributes(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetFontAutoScalingEnabled" />
    public new SkUiEntry SetFontAutoScalingEnabled(bool value) { base.SetFontAutoScalingEnabled(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetHorizontalTextAlignment" />
    public new SkUiEntry SetHorizontalTextAlignment(TextAlignment value) { base.SetHorizontalTextAlignment(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetVerticalTextAlignment" />
    public new SkUiEntry SetVerticalTextAlignment(TextAlignment value) { base.SetVerticalTextAlignment(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetMaxLength" />
    public new SkUiEntry SetMaxLength(int value) { base.SetMaxLength(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetIsReadOnly" />
    public new SkUiEntry SetIsReadOnly(bool value) { base.SetIsReadOnly(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetKeyboard" />
    public new SkUiEntry SetKeyboard(Keyboard value) { base.SetKeyboard(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetIsSpellCheckEnabled" />
    public new SkUiEntry SetIsSpellCheckEnabled(bool value) { base.SetIsSpellCheckEnabled(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetIsTextPredictionEnabled" />
    public new SkUiEntry SetIsTextPredictionEnabled(bool value) { base.SetIsTextPredictionEnabled(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetCursorPosition" />
    public new SkUiEntry SetCursorPosition(int value) { base.SetCursorPosition(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetSelectionLength" />
    public new SkUiEntry SetSelectionLength(int value) { base.SetSelectionLength(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetCaretColor" />
    public new SkUiEntry SetCaretColor(Color? value) { base.SetCaretColor(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetSelectionColor" />
    public new SkUiEntry SetSelectionColor(Color? value) { base.SetSelectionColor(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetAutofillHint" />
    public new SkUiEntry SetAutofillHint(string? value) { base.SetAutofillHint(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetInputFormatters" />
    public new SkUiEntry SetInputFormatters(params IEnumerable<SkUiTextInputFormatter> formatters) { base.SetInputFormatters(formatters); return this; }

    /// <inheritdoc cref="SkUiInputView.SetFormatsAssignedText" />
    public new SkUiEntry SetFormatsAssignedText(bool value) { base.SetFormatsAssignedText(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetLeadingView" />
    public new SkUiEntry SetLeadingView(ISkUiView? value) { base.SetLeadingView(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetTrailingView" />
    public new SkUiEntry SetTrailingView(ISkUiView? value) { base.SetTrailingView(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetHasError" />
    public new SkUiEntry SetHasError(bool value) { base.SetHasError(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetErrorText" />
    public new SkUiEntry SetErrorText(string? value) { base.SetErrorText(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetHelperText" />
    public new SkUiEntry SetHelperText(string? value) { base.SetHelperText(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetErrorColor" />
    public new SkUiEntry SetErrorColor(Color? value) { base.SetErrorColor(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetFieldStyle" />
    public new SkUiEntry SetFieldStyle(SkUiTextFieldStyle value) { base.SetFieldStyle(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetTextPadding" />
    public new SkUiEntry SetTextPadding(Thickness? value) { base.SetTextPadding(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetFillColor" />
    public new SkUiEntry SetFillColor(Color? value) { base.SetFillColor(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetBorderColor" />
    public new SkUiEntry SetBorderColor(Color? value) { base.SetBorderColor(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetBorderWidth" />
    public new SkUiEntry SetBorderWidth(double? value) { base.SetBorderWidth(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetCornerRadii" />
    public new SkUiEntry SetCornerRadii(CornerRadius? value) { base.SetCornerRadii(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetCornerRadius" />
    public new SkUiEntry SetCornerRadius(int? value) { base.SetCornerRadius(value); return this; }

    private void OnIsPasswordChanged(bool value)
    {
        Model.IsPassword = value;
        Model.TextChanged();
        Model.OptionsChanged();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Only a change the property does not hold yet (the reveal button, a hold, unfocus, a screen reader) is written, as a handler
    /// value (<see cref="SkUiView.WriteBack"/>): the model also follows the property, and a manual value would pin over a style's,
    /// trigger's or one-way binding's value, so a trigger turning off would leave the text shown and a one-way binding would stop.
    /// </remarks>
    private protected override void OnPasswordRevealEdited(bool revealed) => WriteBack(IsPasswordRevealedProperty, revealed);

    /// <inheritdoc />
    private protected override void OnReturnPressed()
    {
        if (!IsEnabled)
            return;
        Completed?.Invoke(this, EventArgs.Empty);
        if (ReturnCommand is { } command && command.CanExecute(ReturnCommandParameter))
            command.Execute(ReturnCommandParameter);
    }
}
