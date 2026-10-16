using System.Windows.Input;
using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// What the drawn text fields share (MAUI's <c>InputView</c>; <see cref="SkUiEntry"/>, <see cref="SkUiEditor"/>): the text, caret,
/// selection and placeholder drawn on the surface, the look's chrome, and the platform's keyboard and input methods (composition,
/// autocorrect, dictation, autofill) through the surface's text input proxy. MAUI's members by name; beyond MAUI:
/// <see cref="CaretColor"/>, <see cref="SelectionColor"/>, <see cref="AutofillHint"/>, input formatters, leading and trailing
/// views, and validation chrome (<see cref="HasError"/>, <see cref="ErrorText"/>, <see cref="HelperText"/>).
/// </summary>
public abstract class SkUiInputView : SkUiView, ISkUiTextFieldOwner, ISkUiKeyHandler, ISkUiTextInputNode, ISkUiWheelHandler
{
    /// <summary>The field engine shared with the Core fields.</summary>
    internal SkUiTextFieldModel Model { get; }

    private protected SkUiInputView(bool multiline)
    {
        Model = new SkUiTextFieldModel(this, multiline);
    }

    #region Bindable properties

    /// <summary>Bindable property for <see cref="Text"/> (two-way by default).</summary>
    public static readonly BindableProperty TextProperty = BindableProperty.Create(nameof(Text), typeof(string), typeof(SkUiInputView), null, BindingMode.TwoWay,
        propertyChanged: (view, oldValue, newValue) => ((SkUiInputView)view).OnTextChanged((string?)oldValue, (string?)newValue));

    /// <summary>Bindable property for <see cref="Placeholder"/>.</summary>
    public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(SkUiInputView), null,
        propertyChanged: (view, _, value) => ((SkUiInputView)view).Apply(model => model.Placeholder = (string?)value, measure: true));

    /// <summary>Bindable property for <see cref="PlaceholderColor"/>.</summary>
    public static readonly BindableProperty PlaceholderColorProperty = BindableProperty.Create(nameof(PlaceholderColor), typeof(Color), typeof(SkUiInputView), null,
        propertyChanged: (view, _, value) => ((SkUiInputView)view).Apply(model => model.PlaceholderColor = (Color?)value));

    /// <summary>Bindable property for <see cref="TextColor"/>.</summary>
    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(SkUiInputView), null,
        propertyChanged: (view, _, value) => ((SkUiInputView)view).Apply(model => model.TextColor = (Color?)value));

    /// <summary>Bindable property for <see cref="CharacterSpacing"/>.</summary>
    public static readonly BindableProperty CharacterSpacingProperty = BindableProperty.Create(nameof(CharacterSpacing), typeof(double), typeof(SkUiInputView), 0d,
        validateValue: SkUiValidate.Finite,
        propertyChanged: (view, _, value) => ((SkUiInputView)view).Apply(model => model.CharacterSpacing = (double)value, measure: true));

    /// <summary>Bindable property for <see cref="TextTransform"/>.</summary>
    public static readonly BindableProperty TextTransformProperty = BindableProperty.Create(nameof(TextTransform), typeof(TextTransform), typeof(SkUiInputView), TextTransform.Default,
        validateValue: (_, value) => Enum.IsDefined((TextTransform)value),
        propertyChanged: (view, _, value) => ((SkUiInputView)view).OnTextTransformChanged((TextTransform)value));

    /// <summary>Bindable property for <see cref="FontFamily"/>.</summary>
    public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create(nameof(FontFamily), typeof(string), typeof(SkUiInputView), null,
        propertyChanged: (view, _, value) => ((SkUiInputView)view).Apply(model => model.FontFamily = (string?)value, measure: true));

    /// <summary>Bindable property for <see cref="FontSize"/>.</summary>
    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(nameof(FontSize), typeof(double), typeof(SkUiInputView), SkUiTextFieldModel.DefaultFontSize,
        validateValue: SkUiValidate.FinitePositive,
        propertyChanged: (view, _, value) => ((SkUiInputView)view).Apply(model => model.FontSize = (double)value, measure: true));

    /// <summary>Bindable property for <see cref="FontAttributes"/>.</summary>
    public static readonly BindableProperty FontAttributesProperty = BindableProperty.Create(nameof(FontAttributes), typeof(FontAttributes), typeof(SkUiInputView), FontAttributes.None,
        propertyChanged: (view, _, value) => ((SkUiInputView)view).Apply(model => model.FontAttributes = (FontAttributes)value, measure: true));

    /// <summary>Bindable property for <see cref="FontAutoScalingEnabled"/>.</summary>
    public static readonly BindableProperty FontAutoScalingEnabledProperty = BindableProperty.Create(nameof(FontAutoScalingEnabled), typeof(bool), typeof(SkUiInputView), true,
        propertyChanged: (view, _, value) => ((SkUiInputView)view).Apply(model => model.FontAutoScalingEnabled = (bool)value, measure: true));

    /// <summary>Bindable property for <see cref="HorizontalTextAlignment"/>.</summary>
    public static readonly BindableProperty HorizontalTextAlignmentProperty = BindableProperty.Create(nameof(HorizontalTextAlignment), typeof(TextAlignment), typeof(SkUiInputView), TextAlignment.Start,
        validateValue: (_, value) => Enum.IsDefined((TextAlignment)value),
        propertyChanged: (view, _, value) => ((SkUiInputView)view).Apply(model => model.HorizontalTextAlignment = (TextAlignment)value));

    /// <summary>Bindable property for <see cref="VerticalTextAlignment"/>.</summary>
    public static readonly BindableProperty VerticalTextAlignmentProperty = BindableProperty.Create(nameof(VerticalTextAlignment), typeof(TextAlignment), typeof(SkUiInputView), TextAlignment.Center,
        validateValue: (_, value) => Enum.IsDefined((TextAlignment)value),
        propertyChanged: (view, _, value) => ((SkUiInputView)view).Apply(model => model.VerticalTextAlignment = (TextAlignment)value, measure: true),
        defaultValueCreator: view => view is SkUiEditor ? TextAlignment.Start : TextAlignment.Center);

    /// <summary>Bindable property for <see cref="MaxLength"/>.</summary>
    public static readonly BindableProperty MaxLengthProperty = BindableProperty.Create(nameof(MaxLength), typeof(int), typeof(SkUiInputView), int.MaxValue,
        validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, value) => ((SkUiInputView)view).OnMaxLengthChanged((int)value));

    /// <summary>Bindable property for <see cref="IsReadOnly"/>.</summary>
    public static readonly BindableProperty IsReadOnlyProperty = BindableProperty.Create(nameof(IsReadOnly), typeof(bool), typeof(SkUiInputView), false,
        propertyChanged: (view, _, value) => ((SkUiInputView)view).OnIsReadOnlyChanged((bool)value));

    /// <summary>Bindable property for <see cref="Keyboard"/>.</summary>
    public static readonly BindableProperty KeyboardProperty = BindableProperty.Create(nameof(Keyboard), typeof(Keyboard), typeof(SkUiInputView), Keyboard.Default,
        coerceValue: (_, value) => (Keyboard?)value ?? Keyboard.Default,
        propertyChanged: (view, _, value) => ((SkUiInputView)view).ApplyOptions(model => model.Keyboard = (Keyboard)value));

    /// <summary>Bindable property for <see cref="IsSpellCheckEnabled"/>.</summary>
    public static readonly BindableProperty IsSpellCheckEnabledProperty = BindableProperty.Create(nameof(IsSpellCheckEnabled), typeof(bool), typeof(SkUiInputView), true,
        propertyChanged: (view, _, value) => ((SkUiInputView)view).ApplyOptions(model => model.IsSpellCheckEnabled = (bool)value));

    /// <summary>Bindable property for <see cref="IsTextPredictionEnabled"/>.</summary>
    public static readonly BindableProperty IsTextPredictionEnabledProperty = BindableProperty.Create(nameof(IsTextPredictionEnabled), typeof(bool), typeof(SkUiInputView), true,
        propertyChanged: (view, _, value) => ((SkUiInputView)view).ApplyOptions(model => model.IsTextPredictionEnabled = (bool)value));

    /// <summary>Bindable property for <see cref="CursorPosition"/>.</summary>
    public static readonly BindableProperty CursorPositionProperty = BindableProperty.Create(nameof(CursorPosition), typeof(int), typeof(SkUiInputView), 0,
        validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, _) => ((SkUiInputView)view).OnCursorChanged());

    /// <summary>Bindable property for <see cref="SelectionLength"/>.</summary>
    public static readonly BindableProperty SelectionLengthProperty = BindableProperty.Create(nameof(SelectionLength), typeof(int), typeof(SkUiInputView), 0,
        validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, _) => ((SkUiInputView)view).OnCursorChanged());

    /// <summary>Bindable property for <see cref="CaretColor"/>.</summary>
    public static readonly BindableProperty CaretColorProperty = BindableProperty.Create(nameof(CaretColor), typeof(Color), typeof(SkUiInputView), null,
        propertyChanged: (view, _, value) => ((SkUiInputView)view).Apply(model => model.CaretColor = (Color?)value));

    /// <summary>Bindable property for <see cref="SelectionColor"/>.</summary>
    public static readonly BindableProperty SelectionColorProperty = BindableProperty.Create(nameof(SelectionColor), typeof(Color), typeof(SkUiInputView), null,
        propertyChanged: (view, _, value) => ((SkUiInputView)view).Apply(model => model.SelectionColor = (Color?)value));

    /// <summary>Bindable property for <see cref="AutofillHint"/>.</summary>
    public static readonly BindableProperty AutofillHintProperty = BindableProperty.Create(nameof(AutofillHint), typeof(string), typeof(SkUiInputView), null,
        propertyChanged: (view, _, value) => ((SkUiInputView)view).ApplyOptions(model => model.AutofillHint = (string?)value));

    /// <summary>Bindable property for <see cref="FormatsAssignedText"/>.</summary>
    public static readonly BindableProperty FormatsAssignedTextProperty = BindableProperty.Create(nameof(FormatsAssignedText), typeof(bool), typeof(SkUiInputView), false,
        propertyChanged: (view, _, value) => ((SkUiInputView)view).OnFormatsAssignedTextChanged((bool)value));

    /// <summary>Bindable property for <see cref="LeadingView"/>.</summary>
    public static readonly BindableProperty LeadingViewProperty = AdornmentProperty(nameof(LeadingView));

    /// <summary>Bindable property for <see cref="TrailingView"/>.</summary>
    public static readonly BindableProperty TrailingViewProperty = AdornmentProperty(nameof(TrailingView));

    /// <summary>Bindable property for <see cref="HasError"/>.</summary>
    public static readonly BindableProperty HasErrorProperty = BindableProperty.Create(nameof(HasError), typeof(bool), typeof(SkUiInputView), false,
        propertyChanged: (view, _, value) => ((SkUiInputView)view).OnHasErrorChanged((bool)value));

    /// <summary>Bindable property for <see cref="ErrorText"/>.</summary>
    public static readonly BindableProperty ErrorTextProperty = BindableProperty.Create(nameof(ErrorText), typeof(string), typeof(SkUiInputView), null,
        propertyChanged: (view, _, value) => ((SkUiInputView)view).ApplyMessage(model => model.ErrorText = (string?)value));

    /// <summary>Bindable property for <see cref="HelperText"/>.</summary>
    public static readonly BindableProperty HelperTextProperty = BindableProperty.Create(nameof(HelperText), typeof(string), typeof(SkUiInputView), null,
        propertyChanged: (view, _, value) => ((SkUiInputView)view).ApplyMessage(model => model.HelperText = (string?)value));

    /// <summary>Bindable property for <see cref="ErrorColor"/>.</summary>
    public static readonly BindableProperty ErrorColorProperty = BindableProperty.Create(nameof(ErrorColor), typeof(Color), typeof(SkUiInputView), null,
        propertyChanged: (view, _, value) => ((SkUiInputView)view).Apply(model => model.ErrorColor = (Color?)value));

    private static BindableProperty AdornmentProperty(string name) => BindableProperty.Create(name, typeof(ISkUiView), typeof(SkUiInputView), null,
        validateValue: (bindable, value) =>
        {
            var owner = (SkUiInputView)bindable;
            if (value is not ISkUiView child || ReferenceEquals(owner.GetValue(name == nameof(LeadingView) ? LeadingViewProperty : TrailingViewProperty), child))
                return true;
            if (ReferenceEquals(owner.LeadingView, child) || ReferenceEquals(owner.TrailingView, child))
                throw new InvalidOperationException("The leading and trailing views must be different views.");
            owner.ValidateChild(child);
            return true;
        },
        propertyChanged: (bindable, oldValue, newValue) => ((SkUiInputView)bindable).OnAdornmentChanged((ISkUiView?)oldValue, (ISkUiView?)newValue));

    #endregion

    #region Properties

    /// <summary>The text (two-way by default). Typing, the input method, paste and undo set it and raise <see cref="TextChanged"/>.</summary>
    public string? Text { get => (string?)GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>Shown while the text is empty, in <see cref="PlaceholderColor"/>.</summary>
    public string? Placeholder { get => (string?)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    /// <summary>The placeholder's color; <c>null</c> (default): the color scheme's muted color.</summary>
    public Color? PlaceholderColor { get => (Color?)GetValue(PlaceholderColorProperty); set => SetValue(PlaceholderColorProperty, value); }

    /// <summary>The text's color; <c>null</c> (default): the color scheme's foreground.</summary>
    public Color? TextColor { get => (Color?)GetValue(TextColorProperty); set => SetValue(TextColorProperty, value); }

    /// <summary>DIPs added after each character.</summary>
    public double CharacterSpacing { get => (double)GetValue(CharacterSpacingProperty); set => SetValue(CharacterSpacingProperty, value); }

    /// <summary>Upper or lower case: typed and pasted text is transformed (as MAUI's entries transform what they show).</summary>
    public TextTransform TextTransform { get => (TextTransform)GetValue(TextTransformProperty); set => SetValue(TextTransformProperty, value); }

    /// <summary>System font family, or a name registered with <see cref="SkUiFonts.Register"/> / MAUI's <c>ConfigureFonts</c>.</summary>
    public string? FontFamily { get => (string?)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    /// <summary>Font size in DIPs (default 16).</summary>
    [System.ComponentModel.TypeConverter(typeof(FontSizeConverter))]
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>Bold and italic.</summary>
    public FontAttributes FontAttributes { get => (FontAttributes)GetValue(FontAttributesProperty); set => SetValue(FontAttributesProperty, value); }

    /// <summary>Whether the text follows the system text size (<see cref="SkUiFontScaling"/>), as MAUI's. Default <c>true</c>.</summary>
    public bool FontAutoScalingEnabled { get => (bool)GetValue(FontAutoScalingEnabledProperty); set => SetValue(FontAutoScalingEnabledProperty, value); }

    /// <summary>The text's horizontal alignment (default Start, which follows the text's direction).</summary>
    public TextAlignment HorizontalTextAlignment { get => (TextAlignment)GetValue(HorizontalTextAlignmentProperty); set => SetValue(HorizontalTextAlignmentProperty, value); }

    /// <summary>The text's vertical alignment in the field (default Center for an entry, Start for an editor).</summary>
    public TextAlignment VerticalTextAlignment { get => (TextAlignment)GetValue(VerticalTextAlignmentProperty); set => SetValue(VerticalTextAlignmentProperty, value); }

    /// <summary>The most characters (UTF-16 code units) the text may have (default <see cref="int.MaxValue"/>): input is cut there; longer text the app sets is cut.</summary>
    public int MaxLength { get => (int)GetValue(MaxLengthProperty); set => SetValue(MaxLengthProperty, value); }

    /// <summary>Whether the text cannot be changed by the user (it can still be selected and copied).</summary>
    public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>The soft keyboard's layout (MAUI's <c>Keyboard.Text</c>, <c>Numeric</c>, <c>Email</c>, … or a custom one).</summary>
    public Keyboard Keyboard { get => (Keyboard)GetValue(KeyboardProperty); set => SetValue(KeyboardProperty, value); }

    /// <summary>Whether spelling is checked (default <c>true</c>; off for passwords).</summary>
    public bool IsSpellCheckEnabled { get => (bool)GetValue(IsSpellCheckEnabledProperty); set => SetValue(IsSpellCheckEnabledProperty, value); }

    /// <summary>Whether the keyboard suggests words (default <c>true</c>; off for passwords).</summary>
    public bool IsTextPredictionEnabled { get => (bool)GetValue(IsTextPredictionEnabledProperty); set => SetValue(IsTextPredictionEnabledProperty, value); }

    /// <summary>Where the caret is (the selection's start); follows the user.</summary>
    public int CursorPosition { get => (int)GetValue(CursorPositionProperty); set => SetValue(CursorPositionProperty, value); }

    /// <summary>How many characters are selected from <see cref="CursorPosition"/>; follows the user.</summary>
    public int SelectionLength { get => (int)GetValue(SelectionLengthProperty); set => SetValue(SelectionLengthProperty, value); }

    /// <summary>The caret's color (SkiaUi); <c>null</c> (default): the look's (<see cref="SkUiLook.DefaultCaretColor"/>).</summary>
    public Color? CaretColor { get => (Color?)GetValue(CaretColorProperty); set => SetValue(CaretColorProperty, value); }

    /// <summary>The color behind selected text (SkiaUi); <c>null</c> (default): the look's (<see cref="SkUiLook.DefaultSelectionColor"/>).</summary>
    public Color? SelectionColor { get => (Color?)GetValue(SelectionColorProperty); set => SetValue(SelectionColorProperty, value); }

    /// <summary>
    /// What the field holds for password managers and autofill (SkiaUi; <see cref="SkUiAutofillHints"/>, or a platform's own hint);
    /// <c>null</c> (default): a password field is a password, others none.
    /// </summary>
    /// <remarks>One hint. Hints have no white space: it is reserved for a list of hints (as Android and Flutter take) later.</remarks>
    public string? AutofillHint { get => (string?)GetValue(AutofillHintProperty); set => SetValue(AutofillHintProperty, value); }

    /// <summary>
    /// Formatters that rewrite or refuse the user's edits, in order (SkiaUi; <see cref="SkUiTextInputFormatter"/>: filtering,
    /// length limits, masks): typing, committed input method text, paste, cut and deletion. Changing them formats the text
    /// already in the field (<see cref="Text"/> follows); text the app sets later is formatted only with <see cref="FormatsAssignedText"/>.
    /// </summary>
    public IList<SkUiTextInputFormatter> InputFormatters => field ??= CreateInputFormatters();

    /// <summary>
    /// Whether text the app sets (<see cref="Text"/>, a binding) goes through <see cref="InputFormatters"/> too (SkiaUi; default
    /// <c>false</c>, as Flutter): the field shows the formatted text and <see cref="Text"/> follows it. Turning it on formats the
    /// text already there.
    /// </summary>
    public bool FormatsAssignedText { get => (bool)GetValue(FormatsAssignedTextProperty); set => SetValue(FormatsAssignedTextProperty, value); }

    /// <summary>
    /// A drawn view inside the field at its start side, before the text (SkiaUi; Material's leading icon or prefix): an icon
    /// (<see cref="SkUiImage"/>), a short label (<see cref="SkUiLabel"/>), a button. It is measured at its own size and placed
    /// beside the text, aligned with it (<see cref="VerticalTextAlignment"/>); the text is inset so they never overlap. A press
    /// on a view that handles pointers itself (a button) is the view's, and does not focus the field; a press on an icon or a
    /// label focuses the field. A search bar's icon comes before it.
    /// </summary>
    public ISkUiView? LeadingView { get => (ISkUiView?)GetValue(LeadingViewProperty); set => SetValue(LeadingViewProperty, value); }

    /// <summary>
    /// A drawn view inside the field at its end side, after the text (SkiaUi; Material's trailing icon or suffix); see
    /// <see cref="LeadingView"/>. The clear button comes before it.
    /// </summary>
    public ISkUiView? TrailingView { get => (ISkUiView?)GetValue(TrailingViewProperty); set => SetValue(TrailingViewProperty, value); }

    /// <summary>
    /// Whether the field's input is invalid (SkiaUi; Material's error state): the look draws the field's outline or underline in the
    /// color scheme's <see cref="SkUiColorScheme.Error"/> and the message under it (<see cref="ErrorText"/>, else
    /// <see cref="HelperText"/>) in that color; screen readers are told. The app decides when (a validation library, a view model's
    /// errors, a behavior); the field does not validate. <see cref="ErrorColor"/> overrides the color. Visual states: <see cref="InvalidVisualState"/> /
    /// <see cref="InvalidFocusedVisualState"/> while set, <see cref="ValidVisualState"/> otherwise.
    /// </summary>
    public bool HasError { get => (bool)GetValue(HasErrorProperty); set => SetValue(HasErrorProperty, value); }

    /// <summary>
    /// The message under the field while <see cref="HasError"/> is set (SkiaUi; Material's error text), in the error color; it
    /// replaces <see cref="HelperText"/> then. <c>null</c> or empty: the helper text stays (in the error color).
    /// </summary>
    public string? ErrorText { get => (string?)GetValue(ErrorTextProperty); set => SetValue(ErrorTextProperty, value); }

    /// <summary>
    /// A message under the field (SkiaUi; Material's supporting text): a hint that stays while the user types, unlike
    /// <see cref="Placeholder"/>, in the color scheme's muted color and the look's <see cref="SkUiLook.TextFieldMessageFontSize"/>. It
    /// wraps at the field's width and makes the field taller; screen readers read it after the field.
    /// </summary>
    public string? HelperText { get => (string?)GetValue(HelperTextProperty); set => SetValue(HelperTextProperty, value); }

    /// <summary>
    /// The color of the error chrome and message while <see cref="HasError"/> is set (SkiaUi); <c>null</c> (default): the color
    /// scheme's <see cref="SkUiColorScheme.Error"/>.
    /// </summary>
    public Color? ErrorColor { get => (Color?)GetValue(ErrorColorProperty); set => SetValue(ErrorColorProperty, value); }

    /// <summary>The visual state while <see cref="HasError"/> is not set (SkiaUi; with <see cref="InvalidVisualState"/>, in a group of their own).</summary>
    public const string ValidVisualState = "Valid";

    /// <summary>
    /// The visual state while <see cref="HasError"/> is set and the field is enabled (SkiaUi; Telerik's <c>Invalid</c>); also while it
    /// is focused when no group has <see cref="InvalidFocusedVisualState"/>.
    /// </summary>
    public const string InvalidVisualState = "Invalid";

    /// <summary>The visual state while <see cref="HasError"/> is set and the field is focused (SkiaUi; Telerik's <c>InvalidFocused</c>), when a group has it.</summary>
    public const string InvalidFocusedVisualState = "InvalidFocused";

    /// <summary>Raised when the text changes (by the user or the app), with the old and new text.</summary>
    public event EventHandler<TextChangedEventArgs>? TextChanged;

    #endregion

    #region Fluent setters

    /// <summary>Sets <see cref="Text"/>.</summary>
    public SkUiInputView SetText(string? value) { Text = value; return this; }

    /// <summary>Sets <see cref="Placeholder"/>.</summary>
    public SkUiInputView SetPlaceholder(string? value) { Placeholder = value; return this; }

    /// <summary>Sets <see cref="PlaceholderColor"/>.</summary>
    public SkUiInputView SetPlaceholderColor(Color? value) { PlaceholderColor = value; return this; }

    /// <summary>Sets <see cref="TextColor"/>.</summary>
    public SkUiInputView SetTextColor(Color? value) { TextColor = value; return this; }

    /// <summary>Sets <see cref="CharacterSpacing"/>.</summary>
    public SkUiInputView SetCharacterSpacing(double value) { SkUiValidate.ThrowIfNotFinite(value, nameof(value)); CharacterSpacing = value; return this; }

    /// <summary>Sets <see cref="TextTransform"/>.</summary>
    public SkUiInputView SetTextTransform(TextTransform value) { ThrowIfUndefined(value); TextTransform = value; return this; }

    /// <summary>Sets <see cref="FontFamily"/>.</summary>
    public SkUiInputView SetFontFamily(string? value) { FontFamily = value; return this; }

    /// <summary>Sets <see cref="FontSize"/>.</summary>
    public SkUiInputView SetFontSize(double value) { SkUiValidate.ThrowIfNotFinitePositive(value, nameof(value)); FontSize = value; return this; }

    /// <summary>Sets <see cref="FontAttributes"/>.</summary>
    public SkUiInputView SetFontAttributes(FontAttributes value) { FontAttributes = value; return this; }

    /// <summary>Sets <see cref="FontAutoScalingEnabled"/>.</summary>
    public SkUiInputView SetFontAutoScalingEnabled(bool value) { FontAutoScalingEnabled = value; return this; }

    /// <summary>Sets <see cref="HorizontalTextAlignment"/>.</summary>
    public SkUiInputView SetHorizontalTextAlignment(TextAlignment value) { ThrowIfUndefined(value); HorizontalTextAlignment = value; return this; }

    /// <summary>Sets <see cref="VerticalTextAlignment"/>.</summary>
    public SkUiInputView SetVerticalTextAlignment(TextAlignment value) { ThrowIfUndefined(value); VerticalTextAlignment = value; return this; }

    /// <summary>Sets <see cref="MaxLength"/>.</summary>
    public SkUiInputView SetMaxLength(int value) { ArgumentOutOfRangeException.ThrowIfNegative(value); MaxLength = value; return this; }

    /// <summary>Sets <see cref="IsReadOnly"/>.</summary>
    public SkUiInputView SetIsReadOnly(bool value) { IsReadOnly = value; return this; }

    /// <summary>Sets <see cref="Keyboard"/>.</summary>
    public SkUiInputView SetKeyboard(Keyboard value) { ArgumentNullException.ThrowIfNull(value); Keyboard = value; return this; }

    /// <summary>Sets <see cref="IsSpellCheckEnabled"/>.</summary>
    public SkUiInputView SetIsSpellCheckEnabled(bool value) { IsSpellCheckEnabled = value; return this; }

    /// <summary>Sets <see cref="IsTextPredictionEnabled"/>.</summary>
    public SkUiInputView SetIsTextPredictionEnabled(bool value) { IsTextPredictionEnabled = value; return this; }

    /// <summary>Sets <see cref="CursorPosition"/>.</summary>
    public SkUiInputView SetCursorPosition(int value) { ArgumentOutOfRangeException.ThrowIfNegative(value); CursorPosition = value; return this; }

    /// <summary>Sets <see cref="SelectionLength"/>.</summary>
    public SkUiInputView SetSelectionLength(int value) { ArgumentOutOfRangeException.ThrowIfNegative(value); SelectionLength = value; return this; }

    /// <summary>Sets <see cref="CaretColor"/>.</summary>
    public SkUiInputView SetCaretColor(Color? value) { CaretColor = value; return this; }

    /// <summary>Sets <see cref="SelectionColor"/>.</summary>
    public SkUiInputView SetSelectionColor(Color? value) { SelectionColor = value; return this; }

    /// <summary>Sets <see cref="AutofillHint"/>.</summary>
    public SkUiInputView SetAutofillHint(string? value) { AutofillHint = value; return this; }

    /// <summary>Replaces <see cref="InputFormatters"/> with <paramref name="formatters"/> (in order).</summary>
    public SkUiInputView SetInputFormatters(params IEnumerable<SkUiTextInputFormatter> formatters)
    {
        ArgumentNullException.ThrowIfNull(formatters);
        var list = formatters.ToList();
        if (list.Contains(null!))
            throw new ArgumentException("A formatter is null.", nameof(formatters));
        var target = InputFormatters;
        target.Clear();
        foreach (var formatter in list)
            target.Add(formatter);
        return this;
    }

    /// <summary>Sets <see cref="FormatsAssignedText"/>.</summary>
    public SkUiInputView SetFormatsAssignedText(bool value) { FormatsAssignedText = value; return this; }

    /// <summary>Sets <see cref="LeadingView"/>.</summary>
    public SkUiInputView SetLeadingView(ISkUiView? value) { LeadingView = value; return this; }

    /// <summary>Sets <see cref="TrailingView"/>.</summary>
    public SkUiInputView SetTrailingView(ISkUiView? value) { TrailingView = value; return this; }

    /// <summary>Sets <see cref="HasError"/>.</summary>
    public SkUiInputView SetHasError(bool value) { HasError = value; return this; }

    /// <summary>Sets <see cref="ErrorText"/>.</summary>
    public SkUiInputView SetErrorText(string? value) { ErrorText = value; return this; }

    /// <summary>Sets <see cref="HelperText"/>.</summary>
    public SkUiInputView SetHelperText(string? value) { HelperText = value; return this; }

    /// <summary>Sets <see cref="ErrorColor"/>.</summary>
    public SkUiInputView SetErrorColor(Color? value) { ErrorColor = value; return this; }

    private protected static void ThrowIfUndefined<T>(T value) where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value), value, null);
    }

    #endregion

    #region Changes

    private protected void Apply(Action<SkUiTextFieldModel> apply, bool measure = false)
    {
        apply(Model);
        if (measure)
            Model.TextChanged();
        else
            Model.PaintChanged();
    }

    private protected void ApplyOptions(Action<SkUiTextFieldModel> apply)
    {
        apply(Model);
        Model.OptionsChanged();
    }

    private void ApplyMessage(Action<SkUiTextFieldModel> apply)
    {
        apply(Model);
        Model.MessageChanged();
    }

    private void OnHasErrorChanged(bool value)
    {
        Model.HasError = value;
        Model.MessageChanged();
        ChangeVisualState();
    }

    private void OnTextChanged(string? oldValue, string? newValue)
    {
        var shown = Model.SetText(newValue);
        TextChanged?.Invoke(this, new TextChangedEventArgs(oldValue, newValue));
        // Cut or transformed: Text follows what the field shows (and a two-way binding gets it).
        if (shown is not null)
            SetValue(TextProperty, shown);
        else if (!_textEditing)
            OnTextAssigned();
    }

    // The user's edit is setting Text (not the app).
    private bool _textEditing;

    /// <summary>The app set <see cref="Text"/> (not the user's edit), after the field took it.</summary>
    private protected virtual void OnTextAssigned()
    {
    }

    /// <summary>The user edited the text (typing, the input method, paste, undo), after <see cref="Text"/> followed.</summary>
    private protected virtual void OnTextEdited()
    {
    }

    private System.Collections.ObjectModel.ObservableCollection<SkUiTextInputFormatter> CreateInputFormatters()
    {
        var formatters = new System.Collections.ObjectModel.ObservableCollection<SkUiTextInputFormatter>();
        formatters.CollectionChanged += (_, _) => OnFormattersChanged();
        Model.Editor.Formatters = formatters;
        return formatters;
    }

    private void OnFormatsAssignedTextChanged(bool value)
    {
        Model.FormatsAssignedText = value;
        if (value)
            OnFormattersChanged();
    }

    private void OnFormattersChanged()
    {
        if (Model.FormatsChanged() is { } shown)
            SetValue(TextProperty, shown);
    }

    private void OnTextTransformChanged(TextTransform value)
    {
        if (Model.SetTransform(value) is { } shown)
            SetValue(TextProperty, shown);
    }

    private void OnMaxLengthChanged(int value)
    {
        Model.Editor.MaxLength = value;
        // Text longer than the new limit is cut (as MAUI's entries do).
        if (Text is { } text && text.Length > value)
            Text = text[..value];
    }

    private protected virtual void OnIsReadOnlyChanged(bool value) => Model.SetReadOnly(value);

    private void OnAdornmentChanged(ISkUiView? oldValue, ISkUiView? newValue)
    {
        if (oldValue is not null)
            DetachChild(oldValue);
        if (newValue is not null)
            AttachChild(newValue);
        Model.TextChanged();
    }

    private bool _settingSelection;

    private void OnCursorChanged()
    {
        if (!_settingSelection)
            Model.SetSelection(CursorPosition, SelectionLength);
    }

    #endregion

    #region View

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        Model.HasExplicitMinimumHeight = MinimumHeightRequest >= 0;
        Model.LeadingSize = MeasureAdornment(LeadingView, widthConstraint, heightConstraint);
        Model.TrailingSize = MeasureAdornment(TrailingView, widthConstraint, heightConstraint);
        return Model.Measure(widthConstraint, heightConstraint);
    }

    private static Size MeasureAdornment(ISkUiView? view, double widthConstraint, double heightConstraint) =>
        view is null || view.Visibility == Visibility.Collapsed ? Size.Zero : view.Measure(widthConstraint, heightConstraint);

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        if (LeadingView is null && TrailingView is null)
            return;
        var (leading, trailing) = Model.AdornmentFrames(size.Width, size.Height);
        LeadingView?.Arrange(leading);
        TrailingView?.Arrange(trailing);
    }

    /// <inheritdoc />
    internal override IEnumerable<ISkUiView> SkiaChildren
    {
        get
        {
            if (LeadingView is { } leading)
                yield return leading;
            if (TrailingView is { } trailing)
                yield return trailing;
        }
    }

    /// <inheritdoc />
    internal override void AddRenderChildren(List<ISkUiRenderable> children)
    {
        // Without the iterator: no allocation per walk.
        if (LeadingView is ISkUiRenderable leading)
            children.Add(leading);
        if (TrailingView is ISkUiRenderable trailing)
            children.Add(trailing);
    }

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas) => Model.Draw(canvas, (float)Width, (float)Height);

    /// <summary>
    /// The common and focus states, then, while enabled, the validity states: <see cref="InvalidFocusedVisualState"/> (focused,
    /// when a group has it) or <see cref="InvalidVisualState"/> while <see cref="HasError"/>, else <see cref="ValidVisualState"/>.
    /// Put them in a group of their own (e.g. <c>ValidityStates</c>), or <c>Invalid</c> / <c>InvalidFocused</c> in
    /// <c>CommonStates</c>, where they take the place of the state the field would be in.
    /// </summary>
    protected override void ChangeVisualState()
    {
        base.ChangeVisualState();
        if (!IsVisualStateEnabled)
            return;
        if (!HasError)
            VisualStateManager.GoToState(this, ValidVisualState);
        else if (!IsFocused || !VisualStateManager.GoToState(this, InvalidFocusedVisualState))
            VisualStateManager.GoToState(this, InvalidVisualState);
    }

    /// <inheritdoc />
    internal override void OnGetRenderProps(ref SkUiRenderProps props)
    {
        base.OnGetRenderProps(ref props);
        Model.GetRenderProps(ref props);
    }

    /// <inheritdoc />
    private protected override void ForgetOwnDrawable() => Model.ForgetCaret();

    /// <inheritdoc />
    internal override void CollectGestureRecognizers(List<SkUiGestureRecognizer> recognizers)
    {
        base.CollectGestureRecognizers(recognizers);
        if (!IsEnabled)
            return;
        recognizers.Add(Model.Gestures);
        if (Model.Steps is { HasButtons: true } steps)
            recognizers.Add(steps.Gestures);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        switch (propertyName)
        {
            case nameof(IsEnabled):
                Model.IsEnabled = IsEnabled;
                if (!IsEnabled)
                    Model.Steps?.Release();
                Model.PaintChanged();
                Model.SemanticsChanged();
                break;
            case nameof(IsPointerOver):
                Model.IsPointerOver = IsPointerOver;
                Model.PaintChanged();
                break;
            case nameof(IsFocused):
                Model.SetFocused(IsFocused);
                break;
            case nameof(MinimumHeightRequest):
                InvalidateMeasureOverride();
                break;
        }
    }

    /// <inheritdoc />
    internal override void OnEffectiveFlowDirectionChanged()
    {
        base.OnEffectiveFlowDirectionChanged();
        Model.TextChanged();
    }

    /// <inheritdoc />
    internal override void ReleaseDrawingResources()
    {
        base.ReleaseDrawingResources();
        Model.ReleaseDrawingResources();
    }

    /// <inheritdoc />
    protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
    {
        base.OnPopulateSemantics(info);
        Model.PopulateSemantics(info);
    }

    /// <inheritdoc />
    protected override bool OnSemanticsAction(SkUiSemanticsActions action) => Model.PerformSemanticsAction(action) || base.OnSemanticsAction(action);

    /// <inheritdoc />
    internal override bool TakesKeyboardFocus => true;

    /// <inheritdoc />
    internal override bool ShowsFocusRing => false;

    bool ISkUiKeyHandler.OnKeyDown(in SkUiKeyEvent key) => Model.OnKeyDown(key);

    ISkUiTextInputClient ISkUiTextInputNode.TextInputClient => Model;

    bool ISkUiTextInputNode.WantsTextInput => true;

    Point ISkUiWheelHandler.OnWheel(double deltaX, double deltaY) => Model.Wheel(deltaX, deltaY);

    bool ISkUiTextInputNode.CanTakeTextFocus => IsVisible && IsEnabled;

    bool ISkUiTextInputNode.HitsTextHandle(Point local) => Model.HandleAt(local) != 0;

    #endregion

    #region Owner

    ISkUiRenderable ISkUiTextFieldOwner.Node => this;

    Size ISkUiTextFieldOwner.FieldSize => new(Width, Height);

    bool ISkUiTextFieldOwner.IsRightToLeft => IsRightToLeft;

    SkUiTextDirection ISkUiTextFieldOwner.TextDirection
    {
        get
        {
            var effective = ((IVisualElementController)this).EffectiveFlowDirection;
            if (effective.HasFlag(EffectiveFlowDirection.RightToLeft)) return SkUiTextDirection.RightToLeft;
            return effective.HasFlag(EffectiveFlowDirection.Explicit) ? SkUiTextDirection.LeftToRight : SkUiTextDirection.Auto;
        }
    }

    bool ISkUiTextFieldOwner.IsFocused => IsFocused;

    void ISkUiTextFieldOwner.TextEdited(string text)
    {
        _textEditing = true;
        try
        {
            SetValue(TextProperty, text);
        }
        finally
        {
            _textEditing = false;
        }
        OnTextEdited();
    }

    void ISkUiTextFieldOwner.SelectionEdited(int start, int length)
    {
        _settingSelection = true;
        try
        {
            CursorPosition = start;
            SelectionLength = length;
        }
        finally
        {
            _settingSelection = false;
        }
    }

    void ISkUiTextFieldOwner.Completed() => OnReturnPressed();

    /// <summary>The return key in a single-line field (an entry's <c>Completed</c>).</summary>
    private protected virtual void OnReturnPressed()
    {
    }

    bool ISkUiTextFieldOwner.RequestFocus() => Focus();

    void ISkUiTextFieldOwner.ReleaseFocus() => Unfocus();

    void ISkUiTextFieldOwner.InvalidateMeasure() => InvalidateMeasureOverride();

    void ISkUiTextFieldOwner.InvalidatePaint() => InvalidatePaint();

    void ISkUiTextFieldOwner.InvalidateSemantics() => InvalidateSemantics();

    void ISkUiTextFieldOwner.InvalidateCaret() => InvalidateRender(SkUiRenderDirty.Props);

    bool ISkUiTextFieldOwner.AdornmentTakesPointer(Point local) =>
        SkUiTextFieldAdornments.TakesPointer(LeadingView as ISkUiRenderable, LeadingView?.Frame ?? Rect.Zero, local)
        || SkUiTextFieldAdornments.TakesPointer(TrailingView as ISkUiRenderable, TrailingView?.Frame ?? Rect.Zero, local);

    #endregion
}
