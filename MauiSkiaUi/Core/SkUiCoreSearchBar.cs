using System.Windows.Input;

namespace MauiSkiaUi.Core;

/// <summary>
/// A search field on the Core layer (the twin of <see cref="SkUiSearchBar"/>, on the same engine): the look's search field with its
/// icon, a clear button whenever it has text, and the keyboard's search key raising <see cref="SearchButtonPressed"/>.
/// </summary>
public class SkUiCoreSearchBar : SkUiCoreInputView
{
    private ICommand? _searchCommand;
    private object? _searchCommandParameter;

    /// <summary>Creates an empty search bar.</summary>
    public SkUiCoreSearchBar() : base(multiline: false)
    {
        Model.IsSearch = true;
        Model.ReturnType = ReturnType.Search;
    }

    /// <summary>Runs when the user searches, after <see cref="SearchButtonPressed"/>, when it can execute.</summary>
    public ICommand? SearchCommand { get => _searchCommand; set => SetSearchCommand(value); }

    /// <summary>The parameter of <see cref="SearchCommand"/>.</summary>
    public object? SearchCommandParameter { get => _searchCommandParameter; set => SetSearchCommandParameter(value); }

    /// <summary>The clear button's color; <c>null</c> (default): the color scheme's muted color.</summary>
    public Color? CancelButtonColor { get => Model.ClearButtonColor; set => SetCancelButtonColor(value); }

    /// <summary>The search icon's color; <c>null</c> (default): the color scheme's muted color.</summary>
    public Color? SearchIconColor { get => Model.SearchIconColor; set => SetSearchIconColor(value); }

    /// <summary>The keyboard's return key (default <see cref="ReturnType.Search"/>).</summary>
    public ReturnType ReturnType { get => Model.ReturnType; set => SetReturnType(value); }

    /// <summary>Raised when the user searches (the keyboard's search key, Enter).</summary>
    public event EventHandler? SearchButtonPressed;

    /// <summary>Sets <see cref="SearchCommand"/>.</summary>
    public SkUiCoreSearchBar SetSearchCommand(ICommand? value)
    {
        SetProperty(ref _searchCommand, value, nameof(SearchCommand));
        return this;
    }

    /// <summary>Sets <see cref="SearchCommandParameter"/>.</summary>
    public SkUiCoreSearchBar SetSearchCommandParameter(object? value)
    {
        SetProperty(ref _searchCommandParameter, value, nameof(SearchCommandParameter));
        return this;
    }

    /// <summary>Sets <see cref="CancelButtonColor"/>.</summary>
    public SkUiCoreSearchBar SetCancelButtonColor(Color? value)
    {
        Update(ref Model.ClearButtonColor, value, nameof(CancelButtonColor));
        return this;
    }

    /// <summary>Sets <see cref="SearchIconColor"/>.</summary>
    public SkUiCoreSearchBar SetSearchIconColor(Color? value)
    {
        Update(ref Model.SearchIconColor, value, nameof(SearchIconColor));
        return this;
    }

    /// <summary>Sets <see cref="ReturnType"/>.</summary>
    public SkUiCoreSearchBar SetReturnType(ReturnType value)
    {
        ThrowIfUndefined(value);
        if (SetProperty(ref Model.ReturnType, value, nameof(ReturnType)))
            Model.OptionsChanged();
        return this;
    }

    /// <inheritdoc cref="SkUiCoreInputView.SetText" />
    public new SkUiCoreSearchBar SetText(string? value) { base.SetText(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetPlaceholder" />
    public new SkUiCoreSearchBar SetPlaceholder(string? value) { base.SetPlaceholder(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetPlaceholderColor" />
    public new SkUiCoreSearchBar SetPlaceholderColor(Color? value) { base.SetPlaceholderColor(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetTextColor" />
    public new SkUiCoreSearchBar SetTextColor(Color? value) { base.SetTextColor(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetCharacterSpacing" />
    public new SkUiCoreSearchBar SetCharacterSpacing(double value) { base.SetCharacterSpacing(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetTextTransform" />
    public new SkUiCoreSearchBar SetTextTransform(TextTransform value) { base.SetTextTransform(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetFontFamily" />
    public new SkUiCoreSearchBar SetFontFamily(string? value) { base.SetFontFamily(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetFontSize" />
    public new SkUiCoreSearchBar SetFontSize(double value) { base.SetFontSize(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetFontAttributes" />
    public new SkUiCoreSearchBar SetFontAttributes(FontAttributes value) { base.SetFontAttributes(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetFontAutoScalingEnabled" />
    public new SkUiCoreSearchBar SetFontAutoScalingEnabled(bool value) { base.SetFontAutoScalingEnabled(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetHorizontalTextAlignment" />
    public new SkUiCoreSearchBar SetHorizontalTextAlignment(TextAlignment value) { base.SetHorizontalTextAlignment(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetVerticalTextAlignment" />
    public new SkUiCoreSearchBar SetVerticalTextAlignment(TextAlignment value) { base.SetVerticalTextAlignment(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetMaxLength" />
    public new SkUiCoreSearchBar SetMaxLength(int value) { base.SetMaxLength(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetIsReadOnly" />
    public new SkUiCoreSearchBar SetIsReadOnly(bool value) { base.SetIsReadOnly(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetKeyboard" />
    public new SkUiCoreSearchBar SetKeyboard(Keyboard value) { base.SetKeyboard(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetIsSpellCheckEnabled" />
    public new SkUiCoreSearchBar SetIsSpellCheckEnabled(bool value) { base.SetIsSpellCheckEnabled(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetIsTextPredictionEnabled" />
    public new SkUiCoreSearchBar SetIsTextPredictionEnabled(bool value) { base.SetIsTextPredictionEnabled(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetCursorPosition" />
    public new SkUiCoreSearchBar SetCursorPosition(int value) { base.SetCursorPosition(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetSelectionLength" />
    public new SkUiCoreSearchBar SetSelectionLength(int value) { base.SetSelectionLength(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetCaretColor" />
    public new SkUiCoreSearchBar SetCaretColor(Color? value) { base.SetCaretColor(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetSelectionColor" />
    public new SkUiCoreSearchBar SetSelectionColor(Color? value) { base.SetSelectionColor(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetAutofillHint" />
    public new SkUiCoreSearchBar SetAutofillHint(string? value) { base.SetAutofillHint(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetInputFormatters" />
    public new SkUiCoreSearchBar SetInputFormatters(params IEnumerable<SkUiTextInputFormatter> formatters) { base.SetInputFormatters(formatters); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetFormatsAssignedText" />
    public new SkUiCoreSearchBar SetFormatsAssignedText(bool value) { base.SetFormatsAssignedText(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetLeadingView" />
    public new SkUiCoreSearchBar SetLeadingView(SkUiCoreNode? value) { base.SetLeadingView(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetTrailingView" />
    public new SkUiCoreSearchBar SetTrailingView(SkUiCoreNode? value) { base.SetTrailingView(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetHasError" />
    public new SkUiCoreSearchBar SetHasError(bool value) { base.SetHasError(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetErrorText" />
    public new SkUiCoreSearchBar SetErrorText(string? value) { base.SetErrorText(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetHelperText" />
    public new SkUiCoreSearchBar SetHelperText(string? value) { base.SetHelperText(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetErrorColor" />
    public new SkUiCoreSearchBar SetErrorColor(Color? value) { base.SetErrorColor(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetFieldStyle" />
    public new SkUiCoreSearchBar SetFieldStyle(SkUiTextFieldStyle value) { base.SetFieldStyle(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetTextPadding" />
    public new SkUiCoreSearchBar SetTextPadding(Thickness? value) { base.SetTextPadding(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetFillColor" />
    public new SkUiCoreSearchBar SetFillColor(Color? value) { base.SetFillColor(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetBorderColor" />
    public new SkUiCoreSearchBar SetBorderColor(Color? value) { base.SetBorderColor(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetBorderWidth" />
    public new SkUiCoreSearchBar SetBorderWidth(double? value) { base.SetBorderWidth(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetCornerRadii" />
    public new SkUiCoreSearchBar SetCornerRadii(CornerRadius? value) { base.SetCornerRadii(value); return this; }

    /// <inheritdoc cref="SkUiCoreInputView.SetCornerRadius" />
    public new SkUiCoreSearchBar SetCornerRadius(double? value) { base.SetCornerRadius(value); return this; }

    /// <inheritdoc />
    private protected override void OnReturnPressed()
    {
        if (!IsEnabled)
            return;
        SearchButtonPressed?.Invoke(this, EventArgs.Empty);
        if (_searchCommand is { } command && command.CanExecute(_searchCommandParameter))
            command.Execute(_searchCommandParameter);
    }
}
