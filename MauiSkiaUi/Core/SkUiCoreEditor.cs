using System.Windows.Input;

namespace MauiSkiaUi.Core;

/// <summary>
/// A multiline text field on the Core layer (the twin of <see cref="SkUiEditor"/>, on the same engine): wrapped lines, Return inserts
/// a line break, the text scrolls inside the field, and <see cref="AutoSize"/> grows the field with its text. <see cref="Completed"/>,
/// then <see cref="ReturnCommand"/>, run when it loses focus.
/// </summary>
public class SkUiCoreEditor : SkUiCoreInputView
{
    private ICommand? _returnCommand;
    private object? _returnCommandParameter;

    /// <summary>Creates an empty editor.</summary>
    public SkUiCoreEditor() : base(multiline: true)
    {
        Unfocused += (_, _) =>
        {
            if (!IsEnabled)
                return;
            Completed?.Invoke(this, EventArgs.Empty);
            if (_returnCommand is { } command && command.CanExecute(_returnCommandParameter))
                command.Execute(_returnCommandParameter);
        };
    }

    /// <summary>Whether the measured height follows the text (see <see cref="SkUiEditor.AutoSize"/>).</summary>
    public EditorAutoSizeOption AutoSize
    {
        get => Model.AutoSize ? EditorAutoSizeOption.TextChanges : EditorAutoSizeOption.Disabled;
        set => SetAutoSize(value);
    }

    /// <summary>Runs when the editor completes (after <see cref="Completed"/>, when it loses focus) and can execute (see <see cref="SkUiEditor.ReturnCommand"/>).</summary>
    public ICommand? ReturnCommand { get => _returnCommand; set => SetReturnCommand(value); }

    /// <summary>The parameter of <see cref="ReturnCommand"/>.</summary>
    public object? ReturnCommandParameter { get => _returnCommandParameter; set => SetReturnCommandParameter(value); }

    /// <summary>Raised when the editor loses focus, before <see cref="ReturnCommand"/>.</summary>
    public event EventHandler? Completed;

    /// <summary>Sets <see cref="ReturnCommand"/>.</summary>
    public SkUiCoreEditor SetReturnCommand(ICommand? value)
    {
        SetProperty(ref _returnCommand, value, nameof(ReturnCommand));
        return this;
    }

    /// <summary>Sets <see cref="ReturnCommandParameter"/>.</summary>
    public SkUiCoreEditor SetReturnCommandParameter(object? value)
    {
        SetParameterProperty(ref _returnCommandParameter, value, nameof(ReturnCommandParameter));
        return this;
    }

    /// <summary>Sets <see cref="AutoSize"/>.</summary>
    public SkUiCoreEditor SetAutoSize(EditorAutoSizeOption value)
    {
        ThrowIfUndefined(value);
        if (SetProperty(ref Model.AutoSize, value == EditorAutoSizeOption.TextChanges, nameof(AutoSize)))
            InvalidateMeasure();
        return this;
    }

    /// <inheritdoc cref="SkUiCoreInputView.SetText" />
    public new SkUiCoreEditor SetText(string? value) { base.SetText(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetPlaceholder" />
    public new SkUiCoreEditor SetPlaceholder(string? value) { base.SetPlaceholder(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetPlaceholderColor" />
    public new SkUiCoreEditor SetPlaceholderColor(Color? value) { base.SetPlaceholderColor(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetPlaceholderFontFamily" />
    public new SkUiCoreEditor SetPlaceholderFontFamily(string? value) { base.SetPlaceholderFontFamily(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetPlaceholderFontAttributes" />
    public new SkUiCoreEditor SetPlaceholderFontAttributes(FontAttributes? value) { base.SetPlaceholderFontAttributes(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetTextColor" />
    public new SkUiCoreEditor SetTextColor(Color? value) { base.SetTextColor(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetCharacterSpacing" />
    public new SkUiCoreEditor SetCharacterSpacing(double value) { base.SetCharacterSpacing(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetFontFeatures" />
    public new SkUiCoreEditor SetFontFeatures(SkUiFontFeatures? value) { base.SetFontFeatures(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetTextTransform" />
    public new SkUiCoreEditor SetTextTransform(TextTransform value) { base.SetTextTransform(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetFontFamily" />
    public new SkUiCoreEditor SetFontFamily(string? value) { base.SetFontFamily(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetFontSize" />
    public new SkUiCoreEditor SetFontSize(double value) { base.SetFontSize(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetFontAttributes" />
    public new SkUiCoreEditor SetFontAttributes(FontAttributes value) { base.SetFontAttributes(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetFontAutoScalingEnabled" />
    public new SkUiCoreEditor SetFontAutoScalingEnabled(bool value) { base.SetFontAutoScalingEnabled(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetHorizontalTextAlignment" />
    public new SkUiCoreEditor SetHorizontalTextAlignment(TextAlignment value) { base.SetHorizontalTextAlignment(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetVerticalTextAlignment" />
    public new SkUiCoreEditor SetVerticalTextAlignment(TextAlignment value) { base.SetVerticalTextAlignment(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetMaxLength" />
    public new SkUiCoreEditor SetMaxLength(int value) { base.SetMaxLength(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetIsReadOnly" />
    public new SkUiCoreEditor SetIsReadOnly(bool value) { base.SetIsReadOnly(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetKeyboard" />
    public new SkUiCoreEditor SetKeyboard(Keyboard value) { base.SetKeyboard(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetIsSpellCheckEnabled" />
    public new SkUiCoreEditor SetIsSpellCheckEnabled(bool value) { base.SetIsSpellCheckEnabled(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetIsTextPredictionEnabled" />
    public new SkUiCoreEditor SetIsTextPredictionEnabled(bool value) { base.SetIsTextPredictionEnabled(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetCursorPosition" />
    public new SkUiCoreEditor SetCursorPosition(int value) { base.SetCursorPosition(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetSelectionLength" />
    public new SkUiCoreEditor SetSelectionLength(int value) { base.SetSelectionLength(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetCaretColor" />
    public new SkUiCoreEditor SetCaretColor(Color? value) { base.SetCaretColor(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetSelectionColor" />
    public new SkUiCoreEditor SetSelectionColor(Color? value) { base.SetSelectionColor(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetAutofillHint" />
    public new SkUiCoreEditor SetAutofillHint(string? value) { base.SetAutofillHint(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetInputFormatters" />
    public new SkUiCoreEditor SetInputFormatters(params IEnumerable<SkUiTextInputFormatter> formatters) { base.SetInputFormatters(formatters); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetFormatsAssignedText" />
    public new SkUiCoreEditor SetFormatsAssignedText(bool value) { base.SetFormatsAssignedText(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetLeadingView" />
    public new SkUiCoreEditor SetLeadingView(SkUiCoreNode? value) { base.SetLeadingView(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetTrailingView" />
    public new SkUiCoreEditor SetTrailingView(SkUiCoreNode? value) { base.SetTrailingView(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetHasError" />
    public new SkUiCoreEditor SetHasError(bool value) { base.SetHasError(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetErrorText" />
    public new SkUiCoreEditor SetErrorText(string? value) { base.SetErrorText(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetHelperText" />
    public new SkUiCoreEditor SetHelperText(string? value) { base.SetHelperText(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetErrorColor" />
    public new SkUiCoreEditor SetErrorColor(Color? value) { base.SetErrorColor(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetFieldStyle" />
    public new SkUiCoreEditor SetFieldStyle(SkUiTextFieldStyle value) { base.SetFieldStyle(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetTextPadding" />
    public new SkUiCoreEditor SetTextPadding(Thickness? value) { base.SetTextPadding(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetFillColor" />
    public new SkUiCoreEditor SetFillColor(Color? value) { base.SetFillColor(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetBorderColor" />
    public new SkUiCoreEditor SetBorderColor(Color? value) { base.SetBorderColor(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetBorderWidth" />
    public new SkUiCoreEditor SetBorderWidth(double? value) { base.SetBorderWidth(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetCornerRadii" />
    public new SkUiCoreEditor SetCornerRadii(CornerRadius? value) { base.SetCornerRadii(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetCornerRadius" />
    public new SkUiCoreEditor SetCornerRadius(double? value) { base.SetCornerRadius(value); return this; }
}
