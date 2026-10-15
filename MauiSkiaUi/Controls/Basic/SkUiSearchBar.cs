using System.Windows.Input;

namespace MauiSkiaUi;

/// <summary>
/// A search field drawn by SkiaUi (MAUI's <c>SearchBar</c>): an entry with the look's search field (a search icon at the start), a
/// clear button whenever it has text, and the keyboard's search key, which raises <see cref="SearchButtonPressed"/> and runs
/// <see cref="SearchCommand"/>. A composition of the entry's field engine, not a new editor. The Core twin is
/// <see cref="Core.SkUiCoreSearchBar"/>.
/// </summary>
public class SkUiSearchBar : SkUiInputView
{
    /// <summary>Creates an empty search bar.</summary>
    public SkUiSearchBar() : base(multiline: false)
    {
        Model.IsSearch = true;
        Model.ReturnType = ReturnType.Search;
    }

    /// <summary>Bindable property for <see cref="SearchCommand"/>.</summary>
    public static readonly BindableProperty SearchCommandProperty = BindableProperty.Create(nameof(SearchCommand), typeof(ICommand), typeof(SkUiSearchBar));

    /// <summary>Bindable property for <see cref="SearchCommandParameter"/>.</summary>
    public static readonly BindableProperty SearchCommandParameterProperty = BindableProperty.Create(nameof(SearchCommandParameter), typeof(object), typeof(SkUiSearchBar));

    /// <summary>Bindable property for <see cref="CancelButtonColor"/>.</summary>
    public static readonly BindableProperty CancelButtonColorProperty = BindableProperty.Create(nameof(CancelButtonColor), typeof(Color), typeof(SkUiSearchBar), null,
        propertyChanged: (view, _, value) => ((SkUiSearchBar)view).Apply(model => model.ClearButtonColor = (Color?)value));

    /// <summary>Bindable property for <see cref="SearchIconColor"/>.</summary>
    public static readonly BindableProperty SearchIconColorProperty = BindableProperty.Create(nameof(SearchIconColor), typeof(Color), typeof(SkUiSearchBar), null,
        propertyChanged: (view, _, value) => ((SkUiSearchBar)view).Apply(model => model.SearchIconColor = (Color?)value));

    /// <summary>Bindable property for <see cref="ReturnType"/>.</summary>
    public static readonly BindableProperty ReturnTypeProperty = BindableProperty.Create(nameof(ReturnType), typeof(ReturnType), typeof(SkUiSearchBar), ReturnType.Search,
        validateValue: (_, value) => Enum.IsDefined((ReturnType)value),
        propertyChanged: (view, _, value) => ((SkUiSearchBar)view).ApplyOptions(model => model.ReturnType = (ReturnType)value));

    /// <summary>Runs when the user searches (the keyboard's search key, Enter), after <see cref="SearchButtonPressed"/>, when it can execute.</summary>
    public ICommand? SearchCommand { get => (ICommand?)GetValue(SearchCommandProperty); set => SetValue(SearchCommandProperty, value); }

    /// <summary>The parameter of <see cref="SearchCommand"/>.</summary>
    public object? SearchCommandParameter { get => GetValue(SearchCommandParameterProperty); set => SetValue(SearchCommandParameterProperty, value); }

    /// <summary>The clear button's color; <c>null</c> (default): the color scheme's muted color.</summary>
    public Color? CancelButtonColor { get => (Color?)GetValue(CancelButtonColorProperty); set => SetValue(CancelButtonColorProperty, value); }

    /// <summary>The search icon's color; <c>null</c> (default): the color scheme's muted color.</summary>
    public Color? SearchIconColor { get => (Color?)GetValue(SearchIconColorProperty); set => SetValue(SearchIconColorProperty, value); }

    /// <summary>The keyboard's return key (default <see cref="ReturnType.Search"/>).</summary>
    public ReturnType ReturnType { get => (ReturnType)GetValue(ReturnTypeProperty); set => SetValue(ReturnTypeProperty, value); }

    /// <summary>Raised when the user searches (the keyboard's search key, Enter).</summary>
    public event EventHandler? SearchButtonPressed;

    /// <summary>Sets <see cref="SearchCommand"/>.</summary>
    public SkUiSearchBar SetSearchCommand(ICommand? value) { SearchCommand = value; return this; }

    /// <summary>Sets <see cref="SearchCommandParameter"/>.</summary>
    public SkUiSearchBar SetSearchCommandParameter(object? value) { SearchCommandParameter = value; return this; }

    /// <summary>Sets <see cref="CancelButtonColor"/>.</summary>
    public SkUiSearchBar SetCancelButtonColor(Color? value) { CancelButtonColor = value; return this; }

    /// <summary>Sets <see cref="SearchIconColor"/>.</summary>
    public SkUiSearchBar SetSearchIconColor(Color? value) { SearchIconColor = value; return this; }

    /// <summary>Sets <see cref="ReturnType"/>.</summary>
    public SkUiSearchBar SetReturnType(ReturnType value) { ThrowIfUndefined(value); ReturnType = value; return this; }

    /// <inheritdoc cref="SkUiInputView.SetText" />
    public new SkUiSearchBar SetText(string? value) { base.SetText(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetPlaceholder" />
    public new SkUiSearchBar SetPlaceholder(string? value) { base.SetPlaceholder(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetPlaceholderColor" />
    public new SkUiSearchBar SetPlaceholderColor(Color? value) { base.SetPlaceholderColor(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetTextColor" />
    public new SkUiSearchBar SetTextColor(Color? value) { base.SetTextColor(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetCharacterSpacing" />
    public new SkUiSearchBar SetCharacterSpacing(double value) { base.SetCharacterSpacing(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetTextTransform" />
    public new SkUiSearchBar SetTextTransform(TextTransform value) { base.SetTextTransform(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetFontFamily" />
    public new SkUiSearchBar SetFontFamily(string? value) { base.SetFontFamily(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetFontSize" />
    public new SkUiSearchBar SetFontSize(double value) { base.SetFontSize(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetFontAttributes" />
    public new SkUiSearchBar SetFontAttributes(FontAttributes value) { base.SetFontAttributes(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetFontAutoScalingEnabled" />
    public new SkUiSearchBar SetFontAutoScalingEnabled(bool value) { base.SetFontAutoScalingEnabled(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetHorizontalTextAlignment" />
    public new SkUiSearchBar SetHorizontalTextAlignment(TextAlignment value) { base.SetHorizontalTextAlignment(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetVerticalTextAlignment" />
    public new SkUiSearchBar SetVerticalTextAlignment(TextAlignment value) { base.SetVerticalTextAlignment(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetMaxLength" />
    public new SkUiSearchBar SetMaxLength(int value) { base.SetMaxLength(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetIsReadOnly" />
    public new SkUiSearchBar SetIsReadOnly(bool value) { base.SetIsReadOnly(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetKeyboard" />
    public new SkUiSearchBar SetKeyboard(Keyboard value) { base.SetKeyboard(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetIsSpellCheckEnabled" />
    public new SkUiSearchBar SetIsSpellCheckEnabled(bool value) { base.SetIsSpellCheckEnabled(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetIsTextPredictionEnabled" />
    public new SkUiSearchBar SetIsTextPredictionEnabled(bool value) { base.SetIsTextPredictionEnabled(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetCursorPosition" />
    public new SkUiSearchBar SetCursorPosition(int value) { base.SetCursorPosition(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetSelectionLength" />
    public new SkUiSearchBar SetSelectionLength(int value) { base.SetSelectionLength(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetCaretColor" />
    public new SkUiSearchBar SetCaretColor(Color? value) { base.SetCaretColor(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetSelectionColor" />
    public new SkUiSearchBar SetSelectionColor(Color? value) { base.SetSelectionColor(value); return this; }

    /// <inheritdoc cref="SkUiInputView.SetAutofillHint" />
    public new SkUiSearchBar SetAutofillHint(string? value) { base.SetAutofillHint(value); return this; }

    /// <inheritdoc />
    private protected override void OnReturnPressed()
    {
        if (!IsEnabled)
            return;
        SearchButtonPressed?.Invoke(this, EventArgs.Empty);
        if (SearchCommand is { } command && command.CanExecute(SearchCommandParameter))
            command.Execute(SearchCommandParameter);
    }
}
