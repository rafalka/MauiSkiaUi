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

    private void OnIsPasswordChanged(bool value)
    {
        Model.IsPassword = value;
        Model.TextChanged();
        Model.OptionsChanged();
    }

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
