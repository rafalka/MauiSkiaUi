using System.Windows.Input;
using System.Collections;
using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>When a choice made in a picker's popup takes effect (a picker's <c>UpdateMode</c>; MAUI's iOS <c>Picker.UpdateMode</c>).</summary>
public enum SkUiPickerUpdateMode
{
    /// <summary>At once, as the user chooses (default): a tap on a row, each toggled row, each wheel's move. Dismissing keeps it.</summary>
    Immediately,
    /// <summary>
    /// When the user accepts: the popup shows Cancel and OK buttons below its content; OK, Enter (and Space on wheels)
    /// apply what was chosen in it, Cancel, Escape or a dismissal discard it.
    /// </summary>
    WhenFinished
}

/// <summary>How a picker's field shows several chosen items (its <c>SelectedItemsDisplay</c>).</summary>
public enum SkUiSelectedItemsDisplay
{
    /// <summary>Their texts joined (<c>SelectedTextSeparator</c>), or a count (<c>SelectedCountFormat</c>) (default).</summary>
    Text,
    /// <summary>A token (a chip) each, with a × that removes it; the field grows by lines (<c>MaxTokenLines</c>).</summary>
    Tokens
}

/// <summary>
/// What the drawn pickers share: the field drawn by the look (the text, the title, the chevron, the states), the font and
/// color properties, <see cref="IsOpen"/> with <see cref="Opened"/> and <see cref="Closed"/>, the popup's placement and the
/// <see cref="PopupPresenter"/>, keyboard and screen readers. A derived picker says what the field shows and what the popup
/// holds (<see cref="SkUiPicker"/>: a list to choose from; <see cref="SkUiMultiColumnPicker"/>: wheels).
/// </summary>
public abstract class SkUiPickerBase : SkUiView, ISkUiPickerOwner, ISkUiKeyHandler
{
    private static readonly PickerOpenedEventArgs OpenedArgs = new();
    private static readonly PickerClosedEventArgs ClosedArgs = new();

    private SkUiPickerModel? _model;

    /// <summary>
    /// The field and popup engine the derived pickers fill, created on first use: property callbacks can run before the
    /// constructor (see <see cref="SkUiView.StyledOr{T}"/>).
    /// </summary>
    private protected SkUiPickerModel Model
    {
        get
        {
            if (_model is null)
            {
                _model = new SkUiPickerModel(this);
                OnModelCreated(_model);
            }
            return _model;
        }
    }

    private protected SkUiPickerBase() { }

    /// <summary>Sets up a derived picker's part of the engine once it exists, before any property is applied to it.</summary>
    private protected virtual void OnModelCreated(SkUiPickerModel model) { }

    #region Properties

    /// <summary>Bindable property for <see cref="Title"/>.</summary>
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(nameof(Title), typeof(string), typeof(SkUiPickerBase), null,
        propertyChanged: (view, _, value) => ((SkUiPickerBase)view).Apply(model => model.Title = (string?)value, measure: true));

    /// <summary>Bindable property for <see cref="TitleColor"/>.</summary>
    public static readonly BindableProperty TitleColorProperty = BindableProperty.Create(nameof(TitleColor), typeof(Color), typeof(SkUiPickerBase), null,
        propertyChanged: (view, _, value) => ((SkUiPickerBase)view).Apply(model => model.TitleColor = (Color?)value));

    /// <summary>Bindable property for <see cref="TextColor"/>.</summary>
    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(SkUiPickerBase), null,
        propertyChanged: (view, _, value) => ((SkUiPickerBase)view).Apply(model => model.TextColor = (Color?)value));

    /// <summary>Bindable property for <see cref="FontFamily"/>.</summary>
    public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create(nameof(FontFamily), typeof(string), typeof(SkUiPickerBase), null,
        propertyChanged: (view, _, value) => ((SkUiPickerBase)view).Apply(model => model.FontFamily = (string?)value, measure: true));

    /// <summary>Bindable property for <see cref="FontSize"/>.</summary>
    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(nameof(FontSize), typeof(double), typeof(SkUiPickerBase), 0d, defaultValueCreator: _ => SkUiPickerModel.DefaultFontSize,
        validateValue: SkUiValidate.FinitePositive,
        propertyChanged: (view, _, value) => ((SkUiPickerBase)view).Apply(model => model.FontSize = (double)value, measure: true));

    /// <summary>Bindable property for <see cref="FontAttributes"/>.</summary>
    public static readonly BindableProperty FontAttributesProperty = BindableProperty.Create(nameof(FontAttributes), typeof(FontAttributes), typeof(SkUiPickerBase), FontAttributes.None,
        propertyChanged: (view, _, value) => ((SkUiPickerBase)view).Apply(model => model.FontAttributes = (FontAttributes)value, measure: true));

    /// <summary>Bindable property for <see cref="FontAutoScalingEnabled"/>.</summary>
    public static readonly BindableProperty FontAutoScalingEnabledProperty = BindableProperty.Create(nameof(FontAutoScalingEnabled), typeof(bool), typeof(SkUiPickerBase), true,
        propertyChanged: (view, _, value) => ((SkUiPickerBase)view).Apply(model => model.FontAutoScalingEnabled = (bool)value, measure: true));

    /// <summary>Bindable property for <see cref="CharacterSpacing"/>.</summary>
    public static readonly BindableProperty CharacterSpacingProperty = BindableProperty.Create(nameof(CharacterSpacing), typeof(double), typeof(SkUiPickerBase), 0d,
        validateValue: SkUiValidate.Finite,
        propertyChanged: (view, _, value) => ((SkUiPickerBase)view).Apply(model => model.CharacterSpacing = (double)value, measure: true));

    /// <summary>Bindable property for <see cref="HorizontalTextAlignment"/>.</summary>
    public static readonly BindableProperty HorizontalTextAlignmentProperty = BindableProperty.Create(nameof(HorizontalTextAlignment), typeof(TextAlignment), typeof(SkUiPickerBase), TextAlignment.Start,
        validateValue: (_, value) => Enum.IsDefined((TextAlignment)value),
        propertyChanged: (view, _, value) => ((SkUiPickerBase)view).Apply(model => model.HorizontalTextAlignment = (TextAlignment)value));

    /// <summary>Bindable property for <see cref="VerticalTextAlignment"/>.</summary>
    public static readonly BindableProperty VerticalTextAlignmentProperty = BindableProperty.Create(nameof(VerticalTextAlignment), typeof(TextAlignment), typeof(SkUiPickerBase), TextAlignment.Center,
        validateValue: (_, value) => Enum.IsDefined((TextAlignment)value),
        propertyChanged: (view, _, value) => ((SkUiPickerBase)view).Apply(model => model.VerticalTextAlignment = (TextAlignment)value));

    /// <summary>Bindable property for <see cref="IsOpen"/> (two-way by default).</summary>
    public static readonly BindableProperty IsOpenProperty = BindableProperty.Create(nameof(IsOpen), typeof(bool), typeof(SkUiPickerBase), false, BindingMode.TwoWay,
        // A picker that cannot show a popup stays closed (as a MAUI picker without a handler; unlike MAUI's, it does not open later).
        coerceValue: (view, value) => (bool)value && !((SkUiPickerBase)view).CanOpen ? false : value,
        propertyChanged: (view, _, value) => ((SkUiPickerBase)view).OnIsOpenChanged((bool)value));

    /// <summary>Bindable property for <see cref="MaxPopupHeight"/>.</summary>
    public static readonly BindableProperty MaxPopupHeightProperty = BindableProperty.Create(nameof(MaxPopupHeight), typeof(double), typeof(SkUiPickerBase), -1d,
        validateValue: (_, value) => value is double number && SkUiPickerValidation.IsMaxPopupHeight(number),
        propertyChanged: (view, _, value) => ((SkUiPickerBase)view).Model.MaxPopupHeight = (double)value);

    /// <summary>Bindable property for <see cref="PopupPlacement"/>.</summary>
    public static readonly BindableProperty PopupPlacementProperty = BindableProperty.Create(nameof(PopupPlacement), typeof(SkUiPopupPlacement), typeof(SkUiPickerBase), SkUiPopupPlacement.Default,
        validateValue: (_, value) => Enum.IsDefined((SkUiPopupPlacement)value),
        propertyChanged: (view, _, value) => ((SkUiPickerBase)view).Model.PopupPlacement = (SkUiPopupPlacement)value);

    /// <summary>Bindable property for <see cref="PopupBounds"/>.</summary>
    public static readonly BindableProperty PopupBoundsProperty = BindableProperty.Create(nameof(PopupBounds), typeof(Rect), typeof(SkUiPickerBase), Rect.Zero,
        propertyChanged: (view, _, value) => ((SkUiPickerBase)view).Model.PopupBounds = (Rect)value);

    /// <summary>Bindable property for <see cref="PopupPresenter"/>.</summary>
    public static readonly BindableProperty PopupPresenterProperty = BindableProperty.Create(nameof(PopupPresenter), typeof(ISkUiPopupPresenter), typeof(SkUiPickerBase), null,
        propertyChanged: (view, _, value) => ((SkUiPickerBase)view).Model.PopupPresenter = (ISkUiPopupPresenter?)value);

    /// <summary>Shown while nothing is selected (in <see cref="TitleColor"/>), and as the title of window popups and action sheets.</summary>
    public string? Title { get => (string?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>The color of <see cref="Title"/> in the field; <c>null</c> (default): the color scheme's muted color.</summary>
    public Color? TitleColor { get => (Color?)GetValue(TitleColorProperty); set => SetValue(TitleColorProperty, value); }

    /// <summary>The selected item's text color (also the chevron and the list's rows); <c>null</c> (default): the color scheme's foreground.</summary>
    public Color? TextColor { get => (Color?)GetValue(TextColorProperty); set => SetValue(TextColorProperty, value); }

    /// <summary>System font family, or a name registered with <see cref="SkUiFonts.Register"/> / MAUI's <c>ConfigureFonts</c>.</summary>
    public string? FontFamily { get => (string?)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    /// <summary>Font size in DIPs (default 16).</summary>
    [System.ComponentModel.TypeConverter(typeof(FontSizeConverter))]
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>Bold and italic.</summary>
    public FontAttributes FontAttributes { get => (FontAttributes)GetValue(FontAttributesProperty); set => SetValue(FontAttributesProperty, value); }

    /// <summary>Whether the text follows the system text size (<see cref="SkUiFontScaling"/>), as MAUI's. Default <c>true</c>.</summary>
    public bool FontAutoScalingEnabled { get => (bool)GetValue(FontAutoScalingEnabledProperty); set => SetValue(FontAutoScalingEnabledProperty, value); }

    /// <summary>DIPs added after each character.</summary>
    public double CharacterSpacing { get => (double)GetValue(CharacterSpacingProperty); set => SetValue(CharacterSpacingProperty, value); }

    /// <summary>The text's horizontal alignment in the field (default start; Start follows the text's direction).</summary>
    public TextAlignment HorizontalTextAlignment { get => (TextAlignment)GetValue(HorizontalTextAlignmentProperty); set => SetValue(HorizontalTextAlignmentProperty, value); }

    /// <summary>The text's vertical alignment in the field (default centered).</summary>
    public TextAlignment VerticalTextAlignment { get => (TextAlignment)GetValue(VerticalTextAlignmentProperty); set => SetValue(VerticalTextAlignmentProperty, value); }

    /// <summary>
    /// Whether the list is open (MAUI 10's). Set it to open or close the list; it goes back to <c>false</c> when the popup
    /// closes. A picker that is disabled, hidden or not on a drawn surface does not open (it stays <c>false</c>). Two-way by default.
    /// </summary>
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>The most the list is tall, in DIPs; -1 (default): as tall as its window allows. SkiaUi extension.</summary>
    public double MaxPopupHeight { get => (double)GetValue(MaxPopupHeightProperty); set => SetValue(MaxPopupHeightProperty, value); }

    /// <summary>
    /// Where the list opens: <see cref="SkUiPopupPlacement.Anchored"/> (below or above the picker, at least its width, as a combo
    /// box), <see cref="SkUiPopupPlacement.Window"/> (in the window, aligned by the look: centered by default),
    /// <see cref="SkUiPopupPlacement.Absolute"/> (at <see cref="PopupBounds"/>) or <see cref="SkUiPopupPlacement.Default"/>
    /// (default: the look's, <see cref="SkUiLook.DefaultPopupPlacement"/>, per platform and idiom). SkiaUi extension.
    /// </summary>
    public SkUiPopupPlacement PopupPlacement { get => (SkUiPopupPlacement)GetValue(PopupPlacementProperty); set => SetValue(PopupPlacementProperty, value); }

    /// <summary>The window rectangle an <see cref="SkUiPopupPlacement.Absolute"/> list opens at (empty: as a window popup). SkiaUi extension.</summary>
    public Rect PopupBounds { get => (Rect)GetValue(PopupBoundsProperty); set => SetValue(PopupBoundsProperty, value); }

    /// <summary>The presenter of this picker's list; <c>null</c> (default): the app's (<see cref="SkUiPopups.Presenter"/>). SkiaUi extension.</summary>
    public ISkUiPopupPresenter? PopupPresenter { get => (ISkUiPopupPresenter?)GetValue(PopupPresenterProperty); set => SetValue(PopupPresenterProperty, value); }

    /// <summary>Bindable property for <see cref="UpdateMode"/>.</summary>
    public static readonly BindableProperty UpdateModeProperty = BindableProperty.Create(nameof(UpdateMode), typeof(SkUiPickerUpdateMode), typeof(SkUiPickerBase),
        SkUiPickerUpdateMode.Immediately, validateValue: (_, value) => Enum.IsDefined((SkUiPickerUpdateMode)value),
        propertyChanged: (view, _, value) => ((SkUiPickerBase)view).Model.UpdateMode = (SkUiPickerUpdateMode)value);

    /// <summary>Bindable property for <see cref="AcceptText"/>.</summary>
    public static readonly BindableProperty AcceptTextProperty = BindableProperty.Create(nameof(AcceptText), typeof(string), typeof(SkUiPickerBase), null,
        propertyChanged: (view, _, value) => ((SkUiPickerBase)view).Model.AcceptText = (string?)value);

    /// <summary>Bindable property for <see cref="CancelText"/>.</summary>
    public static readonly BindableProperty CancelTextProperty = BindableProperty.Create(nameof(CancelText), typeof(string), typeof(SkUiPickerBase), null,
        propertyChanged: (view, _, value) => ((SkUiPickerBase)view).Model.CancelText = (string?)value);

    /// <summary>
    /// When a choice made in the popup takes effect: <see cref="SkUiPickerUpdateMode.Immediately"/> (default) or, with Cancel and OK
    /// buttons, <see cref="SkUiPickerUpdateMode.WhenFinished"/>. Applies from the next opening. MAUI's iOS-only
    /// <c>Picker.UpdateMode</c>, on every platform. SkiaUi extension.
    /// </summary>
    public SkUiPickerUpdateMode UpdateMode { get => (SkUiPickerUpdateMode)GetValue(UpdateModeProperty); set => SetValue(UpdateModeProperty, value); }

    /// <summary>
    /// The OK button's text with <see cref="SkUiPickerUpdateMode.WhenFinished"/>; <c>null</c> (default): the app's,
    /// <see cref="SkUiStrings.AcceptText"/> ("OK"). Bind it to a localized resource, or translate the app's (<see cref="SkUiStrings"/>). SkiaUi extension.
    /// </summary>
    public string? AcceptText { get => (string?)GetValue(AcceptTextProperty); set => SetValue(AcceptTextProperty, value); }

    /// <summary>The Cancel button's text with <see cref="SkUiPickerUpdateMode.WhenFinished"/>; <c>null</c> (default): <see cref="SkUiStrings.CancelText"/> ("Cancel"). SkiaUi extension.</summary>
    public string? CancelText { get => (string?)GetValue(CancelTextProperty); set => SetValue(CancelTextProperty, value); }

    /// <summary>Bindable property for <see cref="Placeholder"/>.</summary>
    public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(SkUiPickerBase), null,
        propertyChanged: (view, _, value) => ((SkUiPickerBase)view).Apply(model => model.Placeholder = (string?)value, measure: true));

    /// <summary>Shown in the field while nothing is chosen (in <see cref="PlaceholderColor"/>); <c>null</c> (default): <see cref="Title"/>, as MAUI's picker, which shows its title there. Set it when the popup's title and the field's hint differ. SkiaUi extension.</summary>
    public string? Placeholder { get => (string?)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    /// <summary>Bindable property for <see cref="PlaceholderColor"/>.</summary>
    public static readonly BindableProperty PlaceholderColorProperty = BindableProperty.Create(nameof(PlaceholderColor), typeof(Color), typeof(SkUiPickerBase), null,
        propertyChanged: (view, _, value) => ((SkUiPickerBase)view).Apply(model => model.PlaceholderColor = (Color?)value));

    /// <summary>The color of the placeholder in the field; <c>null</c> (default): <see cref="TitleColor"/>, else the color scheme's muted color. SkiaUi extension.</summary>
    public Color? PlaceholderColor { get => (Color?)GetValue(PlaceholderColorProperty); set => SetValue(PlaceholderColorProperty, value); }

    /// <summary>Bindable property for <see cref="PopupWidth"/>.</summary>
    public static readonly BindableProperty PopupWidthProperty = BindableProperty.Create(nameof(PopupWidth), typeof(double), typeof(SkUiPickerBase), -1d,
        validateValue: (_, value) => value is double number && SkUiPickerValidation.IsMaxPopupHeight(number),
        propertyChanged: (view, _, value) => ((SkUiPickerBase)view).Model.PopupWidth = (double)value);

    /// <summary>The popup's exact width in DIPs (within the window), whatever its items; it replaces <see cref="MinPopupWidth"/> and <see cref="MaxPopupWidth"/>. -1 (default): as wide as its items want, within those. SkiaUi extension.</summary>
    public double PopupWidth { get => (double)GetValue(PopupWidthProperty); set => SetValue(PopupWidthProperty, value); }

    /// <summary>Bindable property for <see cref="SelectionChangedCommand"/>.</summary>
    public static readonly BindableProperty SelectionChangedCommandProperty = BindableProperty.Create(nameof(SelectionChangedCommand), typeof(ICommand), typeof(SkUiPickerBase), null,
        propertyChanged: (view, _, value) => ((SkUiPickerBase)view).Model.SelectionChangedCommand = (ICommand?)value);

    /// <summary>Runs with <see cref="SelectionChangedCommandParameter"/> after the choice changes (when it can execute), before the change event: the selected index, the chosen items, a column's item. SkiaUi extension.</summary>
    public ICommand? SelectionChangedCommand { get => (ICommand?)GetValue(SelectionChangedCommandProperty); set => SetValue(SelectionChangedCommandProperty, value); }

    /// <summary>Bindable property for <see cref="SelectionChangedCommandParameter"/>.</summary>
    public static readonly BindableProperty SelectionChangedCommandParameterProperty = BindableProperty.Create(nameof(SelectionChangedCommandParameter), typeof(object), typeof(SkUiPickerBase), null,
        propertyChanged: (view, _, value) => ((SkUiPickerBase)view).Model.SelectionChangedCommandParameter = (object?)value);

    /// <summary>The parameter of <see cref="SelectionChangedCommand"/>. SkiaUi extension.</summary>
    public object? SelectionChangedCommandParameter { get => (object?)GetValue(SelectionChangedCommandParameterProperty); set => SetValue(SelectionChangedCommandParameterProperty, value); }

    /// <summary>Bindable property for <see cref="IsClearButtonVisible"/>.</summary>
    public static readonly BindableProperty IsClearButtonVisibleProperty = BindableProperty.Create(nameof(IsClearButtonVisible), typeof(bool), typeof(SkUiPickerBase), false,
        propertyChanged: (view, _, value) => ((SkUiPickerBase)view).Apply(model => model.ShowsClearButton = (bool)value, measure: true));

    /// <summary>Whether the field shows a clear button (drawn by the look, before the chevron) while something is chosen: a tap on it, or Delete / Backspace on the focused field, clears the choice (<see cref="ClearSelection"/>) without opening the popup. Default <c>false</c>. SkiaUi extension.</summary>
    public bool IsClearButtonVisible { get => (bool)GetValue(IsClearButtonVisibleProperty); set => SetValue(IsClearButtonVisibleProperty, value); }

    /// <summary>Bindable property for <see cref="IsPopupClearButtonVisible"/>.</summary>
    public static readonly BindableProperty IsPopupClearButtonVisibleProperty = BindableProperty.Create(nameof(IsPopupClearButtonVisible), typeof(bool), typeof(SkUiPickerBase), false,
        propertyChanged: (view, _, value) => ((SkUiPickerBase)view).Model.ShowsPopupClearButton = (bool)value);

    /// <summary>Whether the popup shows a Clear button (at the start of its footer): it clears the choice, at once and closing a single choice's list with <see cref="SkUiPickerUpdateMode.Immediately"/>, or in the draft OK applies with <see cref="SkUiPickerUpdateMode.WhenFinished"/>. Default <c>false</c>. SkiaUi extension.</summary>
    public bool IsPopupClearButtonVisible { get => (bool)GetValue(IsPopupClearButtonVisibleProperty); set => SetValue(IsPopupClearButtonVisibleProperty, value); }

    /// <summary>Bindable property for <see cref="ClearText"/>.</summary>
    public static readonly BindableProperty ClearTextProperty = BindableProperty.Create(nameof(ClearText), typeof(string), typeof(SkUiPickerBase), null,
        propertyChanged: (view, _, value) => ((SkUiPickerBase)view).Model.ClearText = (string?)value);

    /// <summary>The popup's Clear button's text; <c>null</c> (default): <see cref="SkUiStrings.ClearText"/> ("Clear"). SkiaUi extension.</summary>
    public string? ClearText { get => (string?)GetValue(ClearTextProperty); set => SetValue(ClearTextProperty, value); }

    /// <summary>Bindable property for <see cref="PopupHeaderTemplate"/>.</summary>
    public static readonly BindableProperty PopupHeaderTemplateProperty = BindableProperty.Create(nameof(PopupHeaderTemplate), typeof(DataTemplate), typeof(SkUiPickerBase), null,
        propertyChanged: (view, _, value) => ((SkUiPickerBase)view).Model.PopupHeaderTemplate = (DataTemplate?)value);

    /// <summary>Drawn views (<c>SkUi*</c>) of the app's above the popup's list or wheels (below the title): a hint, a filter of its own, actions. Created for each opening with the picker's binding context. SkiaUi extension.</summary>
    public DataTemplate? PopupHeaderTemplate { get => (DataTemplate?)GetValue(PopupHeaderTemplateProperty); set => SetValue(PopupHeaderTemplateProperty, value); }

    /// <summary>Bindable property for <see cref="PopupFooterTemplate"/>.</summary>
    public static readonly BindableProperty PopupFooterTemplateProperty = BindableProperty.Create(nameof(PopupFooterTemplate), typeof(DataTemplate), typeof(SkUiPickerBase), null,
        propertyChanged: (view, _, value) => ((SkUiPickerBase)view).Model.PopupFooterTemplate = (DataTemplate?)value);

    /// <summary>Drawn views (<c>SkUi*</c>) of the app's below the popup's list or wheels (above its buttons). Created for each opening with the picker's binding context. SkiaUi extension.</summary>
    public DataTemplate? PopupFooterTemplate { get => (DataTemplate?)GetValue(PopupFooterTemplateProperty); set => SetValue(PopupFooterTemplateProperty, value); }

    /// <summary>Bindable property for <see cref="MinPopupWidth"/>.</summary>
    public static readonly BindableProperty MinPopupWidthProperty = BindableProperty.Create(nameof(MinPopupWidth), typeof(double), typeof(SkUiPickerBase), -1d,
        validateValue: (_, value) => value is double number && SkUiPickerValidation.IsMaxPopupHeight(number),
        propertyChanged: (view, _, value) => ((SkUiPickerBase)view).Model.MinPopupWidth = (double)value);

    /// <summary>The least width of the popup in DIPs (a list of short items, as single numbers); -1 (default): an anchored popup is at least the field's width, a window popup the look's <see cref="SkUiLook.PopupPreferredWidth"/>. Set, it replaces those (an anchored popup may then be narrower than its field). The popup is wider when its items are. SkiaUi extension.</summary>
    public double MinPopupWidth { get => (double)GetValue(MinPopupWidthProperty); set => SetValue(MinPopupWidthProperty, value); }

    /// <summary>Bindable property for <see cref="MaxPopupWidth"/>.</summary>
    public static readonly BindableProperty MaxPopupWidthProperty = BindableProperty.Create(nameof(MaxPopupWidth), typeof(double), typeof(SkUiPickerBase), -1d,
        validateValue: (_, value) => value is double number && SkUiPickerValidation.IsMaxPopupHeight(number),
        propertyChanged: (view, _, value) => ((SkUiPickerBase)view).Model.MaxPopupWidth = (double)value);

    /// <summary>The most width of the popup in DIPs (its items are truncated at it); -1 (default): the window's safe area. A popup is as wide as its widest item within <see cref="MinPopupWidth"/> and this, so a narrow field (a currency's code) can open a list of wider items (its full name). SkiaUi extension.</summary>
    public double MaxPopupWidth { get => (double)GetValue(MaxPopupWidthProperty); set => SetValue(MaxPopupWidthProperty, value); }

    /// <summary>Raised when <see cref="IsOpen"/> becomes <c>true</c> (the list opens).</summary>
    public event EventHandler<PickerOpenedEventArgs>? Opened;

    /// <summary>Raised when <see cref="IsOpen"/> becomes <c>false</c> (the list closes).</summary>
    public event EventHandler<PickerClosedEventArgs>? Closed;

    /// <summary>Sets <see cref="Title"/> (same as the property setter).</summary>
    public SkUiPickerBase SetTitle(string? value) { Title = value; return this; }

    /// <summary>Sets <see cref="TitleColor"/> (same as the property setter).</summary>
    public SkUiPickerBase SetTitleColor(Color? value) { TitleColor = value; return this; }

    /// <summary>Sets <see cref="TextColor"/> (same as the property setter).</summary>
    public SkUiPickerBase SetTextColor(Color? value) { TextColor = value; return this; }

    /// <summary>Sets <see cref="FontFamily"/> (same as the property setter).</summary>
    public SkUiPickerBase SetFontFamily(string? value) { FontFamily = value; return this; }

    /// <summary>Sets <see cref="FontSize"/> (same as the property setter).</summary>
    public SkUiPickerBase SetFontSize(double value) { SkUiValidate.ThrowIfNotFinitePositive(value, nameof(value)); FontSize = value; return this; }

    /// <summary>Sets <see cref="FontAttributes"/> (same as the property setter).</summary>
    public SkUiPickerBase SetFontAttributes(FontAttributes value) { FontAttributes = value; return this; }

    /// <summary>Sets <see cref="FontAutoScalingEnabled"/> (same as the property setter).</summary>
    public SkUiPickerBase SetFontAutoScalingEnabled(bool value) { FontAutoScalingEnabled = value; return this; }

    /// <summary>Sets <see cref="CharacterSpacing"/> (same as the property setter).</summary>
    public SkUiPickerBase SetCharacterSpacing(double value) { SkUiValidate.ThrowIfNotFinite(value, nameof(value)); CharacterSpacing = value; return this; }

    /// <summary>Sets <see cref="HorizontalTextAlignment"/> (same as the property setter).</summary>
    public SkUiPickerBase SetHorizontalTextAlignment(TextAlignment value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        HorizontalTextAlignment = value;
        return this;
    }

    /// <summary>Sets <see cref="VerticalTextAlignment"/> (same as the property setter).</summary>
    public SkUiPickerBase SetVerticalTextAlignment(TextAlignment value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        VerticalTextAlignment = value;
        return this;
    }

    /// <summary>Sets <see cref="IsOpen"/> (same as the property setter).</summary>
    public SkUiPickerBase SetIsOpen(bool value) { IsOpen = value; return this; }

    /// <summary>Sets <see cref="MaxPopupHeight"/> (same as the property setter; -1: no limit).</summary>
    public SkUiPickerBase SetMaxPopupHeight(double value)
    {
        SkUiPickerValidation.ThrowIfNotMaxPopupHeight(value, nameof(value));
        MaxPopupHeight = value;
        return this;
    }

    /// <summary>Sets <see cref="PopupPlacement"/> (same as the property setter).</summary>
    public SkUiPickerBase SetPopupPlacement(SkUiPopupPlacement value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        PopupPlacement = value;
        return this;
    }

    /// <summary>Sets <see cref="PopupBounds"/> (same as the property setter).</summary>
    public SkUiPickerBase SetPopupBounds(Rect value) { PopupBounds = value; return this; }

    /// <summary>Sets <see cref="PopupPresenter"/> (same as the property setter).</summary>
    public SkUiPickerBase SetPopupPresenter(ISkUiPopupPresenter? value) { PopupPresenter = value; return this; }

    /// <summary>Sets <see cref="UpdateMode"/> (same as the property setter).</summary>
    public SkUiPickerBase SetUpdateMode(SkUiPickerUpdateMode value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        UpdateMode = value;
        return this;
    }

    /// <summary>Sets <see cref="AcceptText"/> (same as the property setter).</summary>
    public SkUiPickerBase SetAcceptText(string? value) { AcceptText = value; return this; }

    /// <summary>Sets <see cref="CancelText"/> (same as the property setter).</summary>
    public SkUiPickerBase SetCancelText(string? value) { CancelText = value; return this; }

    /// <summary>Sets <see cref="MinPopupWidth"/> (same as the property setter).</summary>
    public SkUiPickerBase SetMinPopupWidth(double value) { SkUiPickerValidation.ThrowIfNotOptionalLength(value, nameof(value)); MinPopupWidth = value; return this; }

    /// <summary>Sets <see cref="MaxPopupWidth"/> (same as the property setter).</summary>
    public SkUiPickerBase SetMaxPopupWidth(double value) { SkUiPickerValidation.ThrowIfNotOptionalLength(value, nameof(value)); MaxPopupWidth = value; return this; }

    /// <summary>Sets <see cref="PopupHeaderTemplate"/> (same as the property setter).</summary>
    public SkUiPickerBase SetPopupHeaderTemplate(DataTemplate? value) { PopupHeaderTemplate = value; return this; }

    /// <summary>Sets <see cref="PopupFooterTemplate"/> (same as the property setter).</summary>
    public SkUiPickerBase SetPopupFooterTemplate(DataTemplate? value) { PopupFooterTemplate = value; return this; }

    /// <summary>Sets <see cref="IsClearButtonVisible"/> (same as the property setter).</summary>
    public SkUiPickerBase SetIsClearButtonVisible(bool value) { IsClearButtonVisible = value; return this; }

    /// <summary>Sets <see cref="IsPopupClearButtonVisible"/> (same as the property setter).</summary>
    public SkUiPickerBase SetIsPopupClearButtonVisible(bool value) { IsPopupClearButtonVisible = value; return this; }

    /// <summary>Sets <see cref="ClearText"/> (same as the property setter).</summary>
    public SkUiPickerBase SetClearText(string? value) { ClearText = value; return this; }

    /// <summary>Sets <see cref="Placeholder"/> (same as the property setter).</summary>
    public SkUiPickerBase SetPlaceholder(string? value) { Placeholder = value; return this; }

    /// <summary>Sets <see cref="PlaceholderColor"/> (same as the property setter).</summary>
    public SkUiPickerBase SetPlaceholderColor(Color? value) { PlaceholderColor = value; return this; }

    /// <summary>Sets <see cref="PopupWidth"/> (same as the property setter).</summary>
    public SkUiPickerBase SetPopupWidth(double value) { SkUiPickerValidation.ThrowIfNotOptionalLength(value, nameof(value)); PopupWidth = value; return this; }

    /// <summary>Sets <see cref="SelectionChangedCommand"/> (same as the property setter).</summary>
    public SkUiPickerBase SetSelectionChangedCommand(ICommand? value) { SelectionChangedCommand = value; return this; }

    /// <summary>Sets <see cref="SelectionChangedCommandParameter"/> (same as the property setter).</summary>
    public SkUiPickerBase SetSelectionChangedCommandParameter(object? value) { SelectionChangedCommandParameter = value; return this; }

    private void Apply(Action<SkUiPickerModel> apply, bool measure = false)
    {
        apply(Model);
        if (measure)
            Model.TextChanged();
        else
            Model.PaintChanged();
    }

    /// <summary>The objects a list popup's templated rows bind to (the picker's <c>ItemsSource</c>), or <c>null</c>.</summary>
    private protected virtual IList? PopupItemsSource => null;

    /// <summary>
    /// Clears the choice: no selected item (a list), no chosen items (several choices), no item in any column (wheels); the field
    /// shows its placeholder. Raises the change events (and <see cref="SelectionChangedCommand"/>) when something was chosen.
    /// </summary>
    public void ClearSelection() => ClearChoice();

    /// <summary>What <see cref="ClearSelection"/> clears, for this kind of picker.</summary>
    private protected abstract void ClearChoice();

    /// <summary>Removes the chosen item at <paramref name="index"/> (several choices shown as tokens); nothing for other pickers.</summary>
    private protected virtual void RemoveChoice(int index)
    {
    }

    /// <summary>The user chose the item at <paramref name="index"/> in the popup or with an arrow key on the closed field (lists).</summary>
    private protected virtual void ChooseItem(int index)
    {
    }

    #endregion

    #region Popup

    private void OnIsOpenChanged(bool value)
    {
        RaiseOpenedOrClosed(value);
        if (value)
            Model.Open();
        else
            Model.Close();
        InvalidatePaint();
    }

    /// <summary>Raises <see cref="Opened"/> (<paramref name="opened"/>) or <see cref="Closed"/>; a picker with MAUI's own event types raises those too.</summary>
    private protected virtual void RaiseOpenedOrClosed(bool opened)
    {
        if (opened)
            Opened?.Invoke(this, OpenedArgs);
        else
            Closed?.Invoke(this, ClosedArgs);
    }

    /// <summary>The open popup's request (tests), or <c>null</c>.</summary>
    internal SkUiPopupRequest? PopupRequest => Model.Request;

    /// <summary>The field's tokens as laid out: texts, rectangles and ×'s (tests).</summary>
    internal IReadOnlyList<(string Text, Rect Bounds, Rect Remove)> TokenBoxes => Model.TokenBoxes(Width, Height);

    /// <summary>Whether a popup is showing (or closing) now.</summary>
    internal bool IsPopupShown => Model.IsOpen;

    private bool CanOpen => IsEnabled && IsVisible && SkUiDiagnostics.GetSurfaceRoot(this) is not null;

    #endregion

    #region Field

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        Model.HasExplicitMinimumHeight = MinimumHeightRequest >= 0;
        return Model.Measure(widthConstraint, heightConstraint);
    }

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas) => Model.Draw(canvas, (float)Width, (float)Height);

    /// <inheritdoc />
    protected override bool HandlesTap => true;

    /// <inheritdoc />
    protected override bool CanReceiveTap => IsEnabled;

    /// <inheritdoc />
    protected override void OnTapped(SkUiTappedEventArgs args)
    {
        RaiseTapped(args);
        ExecuteTappedCommand();
        // The clear button clears without opening.
        if (Model.HitsClearButton(args.Position, Width, Height))
            ClearChoice();
        else if (Model.TokenRemovedAt(args.Position, Width, Height) is var removed and >= 0)
            RemoveChoice(removed);
        else
            IsOpen = !IsOpen;
    }

    /// <inheritdoc />
    protected override void OnPressedChanged() => Model.SetPressed(IsPressed, PressPosition);

    /// <inheritdoc />
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        switch (propertyName)
        {
            case nameof(IsEnabled):
                Model.IsEnabled = IsEnabled;
                Model.PaintChanged();
                if (!IsEnabled)
                    ClosePopup();
                break;
            case nameof(IsVisible) when !IsVisible:
                ClosePopup();
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

    /// <summary>The picker can no longer show its popup: it closes (<see cref="IsOpen"/> goes to <c>false</c>, raising <see cref="Closed"/>).</summary>
    private protected void ClosePopup()
    {
        if (IsOpen)
            IsOpen = false;
        else
            Model.Close();
    }

    /// <inheritdoc />
    protected override void OnAnimationRootChanged(bool subtreeDetached = false)
    {
        base.OnAnimationRootChanged(subtreeDetached);
        if (subtreeDetached || SkUiDiagnostics.GetSurfaceRoot(this) is null)
            ClosePopup();
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
        // The surface was torn down or the picker left it: its popup goes (IsOpen follows once it has).
        Model.Close();
    }

    /// <inheritdoc />
    protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
    {
        base.OnPopulateSemantics(info);
        Model.PopulateSemantics(info);
    }

    /// <inheritdoc />
    internal override bool TakesKeyboardFocus => true;

    bool ISkUiKeyHandler.OnKeyDown(in SkUiKeyEvent key) => Model.OnKeyDown(key);

    #endregion

    #region Owner

    ISkUiRenderable ISkUiPickerOwner.Node => this;

    bool ISkUiPickerOwner.IsRightToLeft => IsRightToLeft;

    SkUiTextDirection ISkUiPickerOwner.TextDirection
    {
        get
        {
            var effective = ((IVisualElementController)this).EffectiveFlowDirection;
            if (effective.HasFlag(EffectiveFlowDirection.RightToLeft)) return SkUiTextDirection.RightToLeft;
            return effective.HasFlag(EffectiveFlowDirection.Explicit) ? SkUiTextDirection.LeftToRight : SkUiTextDirection.Auto;
        }
    }

    bool ISkUiPickerOwner.CanOpen => CanOpen;

    IList? ISkUiPickerOwner.ItemsSource => PopupItemsSource;

    void ISkUiPickerOwner.Choose(int index) => ChooseItem(index);

    void ISkUiPickerOwner.ClearChoice() => ClearChoice();

    void ISkUiPickerOwner.RemoveChoice(int index) => RemoveChoice(index);

    object? ISkUiPickerOwner.PopupBindingContext => BindingContext;

    void ISkUiPickerOwner.SetIsOpen(bool open)
    {
        if (IsOpen != open)
            IsOpen = open;
    }

    void ISkUiPickerOwner.FocusField()
    {
        if (IsEnabled && IsVisible)
            Focus();
    }

    void ISkUiPickerOwner.InvalidateMeasure() => InvalidateMeasureOverride();

    void ISkUiPickerOwner.InvalidatePaint() => InvalidatePaint();

    void ISkUiPickerOwner.InvalidateSemantics() => InvalidateSemantics();

    #endregion
}

/// <summary>Checks of the pickers' own values.</summary>
internal static class SkUiPickerValidation
{
    /// <summary>How close to -1 a <c>MaxPopupHeight</c> must be to mean "no limit".</summary>
    internal const double Tolerance = 1e-9;

    public static bool IsMaxPopupHeight(double value) => !double.IsNaN(value) && (value >= 0 || Math.Abs(value + 1) < Tolerance);

    public static void ThrowIfNotOptionalLength(double value, string name)
    {
        if (!IsMaxPopupHeight(value))
            throw new ArgumentOutOfRangeException(name, value, "The length must be at least 0, or -1 for the content's.");
    }

    public static void ThrowIfNotMaxPopupHeight(double value, string name)
    {
        if (!IsMaxPopupHeight(value))
            throw new ArgumentOutOfRangeException(name, value, "The height must be at least 0, or -1 for no limit.");
    }
}
