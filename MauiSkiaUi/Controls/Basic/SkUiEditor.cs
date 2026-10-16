namespace MauiSkiaUi;

/// <summary>
/// A multiline text field drawn by SkiaUi (MAUI's <c>Editor</c>): wrapped lines, Return inserts a line break, the text scrolls inside
/// the field to keep the caret in view (and by wheel or drag, handing what it cannot scroll to the scrollers around it), and
/// <see cref="AutoSize"/> grows the field with its text. MAUI's <c>Editor</c> API by name; <see cref="Completed"/> is raised when it
/// loses focus, as MAUI's. The Core twin is <see cref="Core.SkUiCoreEditor"/>.
/// </summary>
public class SkUiEditor : SkUiInputView
{
    /// <summary>Creates an empty editor.</summary>
    public SkUiEditor() : base(multiline: true)
    {
    }

    /// <summary>Bindable property for <see cref="AutoSize"/>.</summary>
    public static readonly BindableProperty AutoSizeProperty = BindableProperty.Create(nameof(AutoSize), typeof(EditorAutoSizeOption), typeof(SkUiEditor),
        EditorAutoSizeOption.Disabled,
        validateValue: (_, value) => Enum.IsDefined((EditorAutoSizeOption)value),
        propertyChanged: (view, _, value) => ((SkUiEditor)view).OnAutoSizeChanged((EditorAutoSizeOption)value));

    /// <summary>
    /// Whether the editor's measured height follows its text (<see cref="EditorAutoSizeOption.TextChanges"/>: it grows and shrinks as
    /// lines are typed) or stays as first measured (<see cref="EditorAutoSizeOption.Disabled"/>, default: the text scrolls inside it).
    /// </summary>
    public EditorAutoSizeOption AutoSize { get => (EditorAutoSizeOption)GetValue(AutoSizeProperty); set => SetValue(AutoSizeProperty, value); }

    /// <summary>Raised when the editor loses focus after the user was in it (MAUI's <c>Editor.Completed</c>).</summary>
    public event EventHandler? Completed;

    /// <summary>Sets <see cref="AutoSize"/>.</summary>
    public SkUiEditor SetAutoSize(EditorAutoSizeOption value) { ThrowIfUndefined(value); AutoSize = value; return this; }

    /// <inheritdoc cref="SkUiInputView.SetText" />
    public new SkUiEditor SetText(string? value) { base.SetText(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetPlaceholder" />
    public new SkUiEditor SetPlaceholder(string? value) { base.SetPlaceholder(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetPlaceholderColor" />
    public new SkUiEditor SetPlaceholderColor(Color? value) { base.SetPlaceholderColor(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetTextColor" />
    public new SkUiEditor SetTextColor(Color? value) { base.SetTextColor(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetCharacterSpacing" />
    public new SkUiEditor SetCharacterSpacing(double value) { base.SetCharacterSpacing(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetTextTransform" />
    public new SkUiEditor SetTextTransform(TextTransform value) { base.SetTextTransform(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetFontFamily" />
    public new SkUiEditor SetFontFamily(string? value) { base.SetFontFamily(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetFontSize" />
    public new SkUiEditor SetFontSize(double value) { base.SetFontSize(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetFontAttributes" />
    public new SkUiEditor SetFontAttributes(FontAttributes value) { base.SetFontAttributes(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetFontAutoScalingEnabled" />
    public new SkUiEditor SetFontAutoScalingEnabled(bool value) { base.SetFontAutoScalingEnabled(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetHorizontalTextAlignment" />
    public new SkUiEditor SetHorizontalTextAlignment(TextAlignment value) { base.SetHorizontalTextAlignment(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetVerticalTextAlignment" />
    public new SkUiEditor SetVerticalTextAlignment(TextAlignment value) { base.SetVerticalTextAlignment(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetMaxLength" />
    public new SkUiEditor SetMaxLength(int value) { base.SetMaxLength(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetIsReadOnly" />
    public new SkUiEditor SetIsReadOnly(bool value) { base.SetIsReadOnly(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetKeyboard" />
    public new SkUiEditor SetKeyboard(Keyboard value) { base.SetKeyboard(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetIsSpellCheckEnabled" />
    public new SkUiEditor SetIsSpellCheckEnabled(bool value) { base.SetIsSpellCheckEnabled(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetIsTextPredictionEnabled" />
    public new SkUiEditor SetIsTextPredictionEnabled(bool value) { base.SetIsTextPredictionEnabled(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetCursorPosition" />
    public new SkUiEditor SetCursorPosition(int value) { base.SetCursorPosition(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetSelectionLength" />
    public new SkUiEditor SetSelectionLength(int value) { base.SetSelectionLength(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetCaretColor" />
    public new SkUiEditor SetCaretColor(Color? value) { base.SetCaretColor(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetSelectionColor" />
    public new SkUiEditor SetSelectionColor(Color? value) { base.SetSelectionColor(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetAutofillHint" />
    public new SkUiEditor SetAutofillHint(string? value) { base.SetAutofillHint(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetInputFormatters" />
    public new SkUiEditor SetInputFormatters(params IEnumerable<SkUiTextInputFormatter> formatters) { base.SetInputFormatters(formatters); return this; }

    /// <inheritdoc cref="SkUiInputView.SetFormatsAssignedText" />
    public new SkUiEditor SetFormatsAssignedText(bool value) { base.SetFormatsAssignedText(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetLeadingView" />
    public new SkUiEditor SetLeadingView(ISkUiView? value) { base.SetLeadingView(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetTrailingView" />
    public new SkUiEditor SetTrailingView(ISkUiView? value) { base.SetTrailingView(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetHasError" />
    public new SkUiEditor SetHasError(bool value) { base.SetHasError(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetErrorText" />
    public new SkUiEditor SetErrorText(string? value) { base.SetErrorText(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetHelperText" />
    public new SkUiEditor SetHelperText(string? value) { base.SetHelperText(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetErrorColor" />
    public new SkUiEditor SetErrorColor(Color? value) { base.SetErrorColor(value); return this; }

    private void OnAutoSizeChanged(EditorAutoSizeOption value)
    {
        Model.AutoSize = value == EditorAutoSizeOption.TextChanges;
        InvalidateMeasureOverride();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(IsFocused) && !IsFocused && IsEnabled)
            Completed?.Invoke(this, EventArgs.Empty);
    }
}
