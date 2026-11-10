using System.Windows.Input;

namespace MauiSkiaUi.Core;

/// <summary>
/// A single-line text field on the Core layer (the twin of <see cref="SkUiEntry"/>, on the same engine): drawn text, caret,
/// selection and placeholder, the look's chrome, and the platform's keyboard and input methods through the surface's text input
/// proxy. Fluent <c>Set*</c> setters and <see cref="System.ComponentModel.INotifyPropertyChanged"/>.
/// </summary>
public class SkUiCoreEntry : SkUiCoreInputView
{
    private ICommand? _returnCommand;
    private object? _returnCommandParameter;

    /// <summary>Creates an empty entry.</summary>
    public SkUiCoreEntry() : base(multiline: false)
    {
    }

    /// <summary>Whether the text is masked (bullets).</summary>
    public bool IsPassword { get => Model.IsPassword; set => SetIsPassword(value); }

    /// <summary>
    /// Whether a password's text is drawn instead of bullets (the reveal button and <see cref="HidesPasswordOnUnfocus"/> change it,
    /// raising <see cref="System.ComponentModel.INotifyPropertyChanged.PropertyChanged"/>). The field stays a password field for the
    /// platform and copying stays off; screen readers read the text only while it shows. No effect unless <see cref="IsPassword"/>.
    /// </summary>
    public bool IsPasswordRevealed { get => Model.IsPasswordRevealed; set => SetIsPasswordRevealed(value); }

    /// <summary>Whether a password field draws the look's reveal button at its end (default <c>false</c>).</summary>
    public bool ShowsPasswordRevealButton { get => Model.ShowsPasswordRevealButton; set => SetShowsPasswordRevealButton(value); }

    /// <summary>Whether a revealed password is masked again when the field loses focus (default <c>true</c>).</summary>
    public bool HidesPasswordOnUnfocus { get => Model.HidesPasswordOnUnfocus; set => SetHidesPasswordOnUnfocus(value); }

    /// <summary>
    /// How the reveal button shows the text: a tap toggles it (<see cref="SkUiPasswordRevealMode.Toggle"/>, the default), or it shows
    /// only while the button is held down (<see cref="SkUiPasswordRevealMode.WhilePressed"/>; screen readers still toggle it).
    /// </summary>
    public SkUiPasswordRevealMode PasswordRevealMode { get => Model.PasswordRevealMode; set => SetPasswordRevealMode(value); }

    /// <summary>The soft keyboard's return key.</summary>
    public ReturnType ReturnType { get => Model.ReturnType; set => SetReturnType(value); }

    /// <summary>Runs on the return key (after <see cref="Completed"/>) when it can execute.</summary>
    public ICommand? ReturnCommand { get => _returnCommand; set => SetReturnCommand(value); }

    /// <summary>The parameter of <see cref="ReturnCommand"/>.</summary>
    public object? ReturnCommandParameter { get => _returnCommandParameter; set => SetReturnCommandParameter(value); }

    /// <summary>Whether a clear button shows while the field is focused and has text.</summary>
    public ClearButtonVisibility ClearButtonVisibility { get => Model.ClearButtonVisibility; set => SetClearButtonVisibility(value); }

    /// <summary>Raised when the user finishes with the return key (before <see cref="ReturnCommand"/>).</summary>
    public event EventHandler? Completed;

    /// <summary>Sets <see cref="IsPassword"/>.</summary>
    public SkUiCoreEntry SetIsPassword(bool value)
    {
        if (!SetProperty(ref Model.IsPassword, value, nameof(IsPassword)))
            return this;
        Model.TextChanged();
        Model.OptionsChanged();
        return this;
    }

    /// <summary>Sets <see cref="IsPasswordRevealed"/>.</summary>
    public SkUiCoreEntry SetIsPasswordRevealed(bool value)
    {
        // The model calls back (OnPasswordRevealEdited), which raises the change.
        Model.SetPasswordRevealed(value);
        return this;
    }

    /// <summary>Sets <see cref="ShowsPasswordRevealButton"/>.</summary>
    public SkUiCoreEntry SetShowsPasswordRevealButton(bool value)
    {
        if (Model.ShowsPasswordRevealButton == value)
            return this;
        Model.SetShowsPasswordRevealButton(value);
        OnPropertyChanged(nameof(ShowsPasswordRevealButton));
        return this;
    }

    /// <summary>Sets <see cref="HidesPasswordOnUnfocus"/>.</summary>
    public SkUiCoreEntry SetHidesPasswordOnUnfocus(bool value)
    {
        SetProperty(ref Model.HidesPasswordOnUnfocus, value, nameof(HidesPasswordOnUnfocus));
        return this;
    }

    /// <summary>Sets <see cref="PasswordRevealMode"/>.</summary>
    public SkUiCoreEntry SetPasswordRevealMode(SkUiPasswordRevealMode value)
    {
        ThrowIfUndefined(value);
        SetProperty(ref Model.PasswordRevealMode, value, nameof(PasswordRevealMode));
        return this;
    }

    /// <summary>Sets <see cref="ReturnType"/>.</summary>
    public SkUiCoreEntry SetReturnType(ReturnType value)
    {
        ThrowIfUndefined(value);
        if (SetProperty(ref Model.ReturnType, value, nameof(ReturnType)))
            Model.OptionsChanged();
        return this;
    }

    /// <summary>Sets <see cref="ReturnCommand"/>.</summary>
    public SkUiCoreEntry SetReturnCommand(ICommand? value)
    {
        SetProperty(ref _returnCommand, value, nameof(ReturnCommand));
        return this;
    }

    /// <summary>Sets <see cref="ReturnCommandParameter"/>.</summary>
    public SkUiCoreEntry SetReturnCommandParameter(object? value)
    {
        SetProperty(ref _returnCommandParameter, value, nameof(ReturnCommandParameter));
        return this;
    }

    /// <summary>Sets <see cref="ClearButtonVisibility"/>.</summary>
    public SkUiCoreEntry SetClearButtonVisibility(ClearButtonVisibility value)
    {
        ThrowIfUndefined(value);
        Update(ref Model.ClearButtonVisibility, value, nameof(ClearButtonVisibility));
        return this;
    }

    /// <inheritdoc cref="SkUiCoreInputView.SetText" />
    public new SkUiCoreEntry SetText(string? value) { base.SetText(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetPlaceholder" />
    public new SkUiCoreEntry SetPlaceholder(string? value) { base.SetPlaceholder(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetPlaceholderColor" />
    public new SkUiCoreEntry SetPlaceholderColor(Color? value) { base.SetPlaceholderColor(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetTextColor" />
    public new SkUiCoreEntry SetTextColor(Color? value) { base.SetTextColor(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetCharacterSpacing" />
    public new SkUiCoreEntry SetCharacterSpacing(double value) { base.SetCharacterSpacing(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetTextTransform" />
    public new SkUiCoreEntry SetTextTransform(TextTransform value) { base.SetTextTransform(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetFontFamily" />
    public new SkUiCoreEntry SetFontFamily(string? value) { base.SetFontFamily(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetFontSize" />
    public new SkUiCoreEntry SetFontSize(double value) { base.SetFontSize(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetFontAttributes" />
    public new SkUiCoreEntry SetFontAttributes(FontAttributes value) { base.SetFontAttributes(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetFontAutoScalingEnabled" />
    public new SkUiCoreEntry SetFontAutoScalingEnabled(bool value) { base.SetFontAutoScalingEnabled(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetHorizontalTextAlignment" />
    public new SkUiCoreEntry SetHorizontalTextAlignment(TextAlignment value) { base.SetHorizontalTextAlignment(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetVerticalTextAlignment" />
    public new SkUiCoreEntry SetVerticalTextAlignment(TextAlignment value) { base.SetVerticalTextAlignment(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetMaxLength" />
    public new SkUiCoreEntry SetMaxLength(int value) { base.SetMaxLength(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetIsReadOnly" />
    public new SkUiCoreEntry SetIsReadOnly(bool value) { base.SetIsReadOnly(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetKeyboard" />
    public new SkUiCoreEntry SetKeyboard(Keyboard value) { base.SetKeyboard(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetIsSpellCheckEnabled" />
    public new SkUiCoreEntry SetIsSpellCheckEnabled(bool value) { base.SetIsSpellCheckEnabled(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetIsTextPredictionEnabled" />
    public new SkUiCoreEntry SetIsTextPredictionEnabled(bool value) { base.SetIsTextPredictionEnabled(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetCursorPosition" />
    public new SkUiCoreEntry SetCursorPosition(int value) { base.SetCursorPosition(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetSelectionLength" />
    public new SkUiCoreEntry SetSelectionLength(int value) { base.SetSelectionLength(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetCaretColor" />
    public new SkUiCoreEntry SetCaretColor(Color? value) { base.SetCaretColor(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetSelectionColor" />
    public new SkUiCoreEntry SetSelectionColor(Color? value) { base.SetSelectionColor(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetAutofillHint" />
    public new SkUiCoreEntry SetAutofillHint(string? value) { base.SetAutofillHint(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetInputFormatters" />
    public new SkUiCoreEntry SetInputFormatters(params IEnumerable<SkUiTextInputFormatter> formatters) { base.SetInputFormatters(formatters); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetFormatsAssignedText" />
    public new SkUiCoreEntry SetFormatsAssignedText(bool value) { base.SetFormatsAssignedText(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetLeadingView" />
    public new SkUiCoreEntry SetLeadingView(SkUiCoreNode? value) { base.SetLeadingView(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetTrailingView" />
    public new SkUiCoreEntry SetTrailingView(SkUiCoreNode? value) { base.SetTrailingView(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetHasError" />
    public new SkUiCoreEntry SetHasError(bool value) { base.SetHasError(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetErrorText" />
    public new SkUiCoreEntry SetErrorText(string? value) { base.SetErrorText(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetHelperText" />
    public new SkUiCoreEntry SetHelperText(string? value) { base.SetHelperText(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetErrorColor" />
    public new SkUiCoreEntry SetErrorColor(Color? value) { base.SetErrorColor(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetFieldStyle" />
    public new SkUiCoreEntry SetFieldStyle(SkUiTextFieldStyle value) { base.SetFieldStyle(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetTextPadding" />
    public new SkUiCoreEntry SetTextPadding(Thickness? value) { base.SetTextPadding(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetFillColor" />
    public new SkUiCoreEntry SetFillColor(Color? value) { base.SetFillColor(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetBorderColor" />
    public new SkUiCoreEntry SetBorderColor(Color? value) { base.SetBorderColor(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetBorderWidth" />
    public new SkUiCoreEntry SetBorderWidth(double? value) { base.SetBorderWidth(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetCornerRadii" />
    public new SkUiCoreEntry SetCornerRadii(CornerRadius? value) { base.SetCornerRadii(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetCornerRadius" />
    public new SkUiCoreEntry SetCornerRadius(double? value) { base.SetCornerRadius(value); return this; }

    /// <inheritdoc />
    private protected override void OnPasswordRevealEdited(bool revealed) => OnPropertyChanged(nameof(IsPasswordRevealed));

    /// <inheritdoc />
    private protected override void OnReturnPressed()
    {
        if (!IsEnabled)
            return;
        Completed?.Invoke(this, EventArgs.Empty);
        if (_returnCommand is { } command && command.CanExecute(_returnCommandParameter))
            command.Execute(_returnCommandParameter);
    }
}
