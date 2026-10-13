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

    /// <summary>The field and popup engine the derived pickers fill.</summary>
    private protected SkUiPickerModel Model { get; }

    private protected SkUiPickerBase() => Model = new SkUiPickerModel(this);

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
    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(nameof(FontSize), typeof(double), typeof(SkUiPickerBase), SkUiPickerModel.DefaultFontSize,
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

    /// <summary>The popup's width in DIPs (within the window); -1 (default): as wide as its content wants (at least the field's width when anchored). SkiaUi extension.</summary>
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

    /// <summary>The user chose the item at <paramref name="index"/> in the popup or with an arrow key on the closed field (lists).</summary>
    private protected virtual void ChooseItem(int index)
    {
    }

    #endregion

    #region Popup

    private void OnIsOpenChanged(bool value)
    {
        if (value)
        {
            Opened?.Invoke(this, OpenedArgs);
            Model.Open();
        }
        else
        {
            Closed?.Invoke(this, ClosedArgs);
            Model.Close();
        }
        InvalidatePaint();
    }

    /// <summary>The open popup's request (tests), or <c>null</c>.</summary>
    internal SkUiPopupRequest? PopupRequest => Model.Request;

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
