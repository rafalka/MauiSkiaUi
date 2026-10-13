using System.Windows.Input;
using System.Collections;
using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// What the Core pickers share (the Core analogue of <see cref="SkUiPickerBase"/>): the field drawn by the look, the font and color
/// settings, <see cref="IsOpen"/> with <see cref="Opened"/> and <see cref="Closed"/>, the popup's placement and presenter, keyboard
/// and screen readers. A derived picker says what the field shows and what the popup holds.
/// </summary>
/// <remarks>Core nodes have no enabled state: the picker always reacts (hide it, or remove it, to stop it).</remarks>
public abstract class SkUiCorePickerBase : SkUiCoreNode, ISkUiPickerOwner, ISkUiKeyHandler
{
    /// <summary>The field and popup engine the derived pickers fill.</summary>
    private protected SkUiPickerModel Model { get; }
    private bool _isOpen;
    private bool _minimumHeightExplicit;

    private protected SkUiCorePickerBase()
    {
        Model = new SkUiPickerModel(this);
        Focused += (_, _) => Model.SetFocused(true);
        Unfocused += (_, _) => Model.SetFocused(false);
    }

    #region Properties

    /// <summary>Shown while nothing is selected (in <see cref="TitleColor"/>), and as the title of window popups.</summary>
    public string? Title { get => Model.Title; set => SetTitle(value); }

    /// <summary>The title's color in the field; <c>null</c> (default): the color scheme's muted color.</summary>
    public Color? TitleColor { get => Model.TitleColor; set => SetTitleColor(value); }

    /// <summary>The text color; <c>null</c> (default): the color scheme's foreground.</summary>
    public Color? TextColor { get => Model.TextColor; set => SetTextColor(value); }

    /// <summary>Font family (see <c>SkUiPicker.FontFamily</c>).</summary>
    public string? FontFamily { get => Model.FontFamily; set => SetFontFamily(value); }

    /// <summary>Font size in DIPs (default 16).</summary>
    public double FontSize { get => Model.FontSize; set => SetFontSize(value); }

    /// <summary>Bold and italic.</summary>
    public FontAttributes FontAttributes { get => Model.FontAttributes; set => SetFontAttributes(value); }

    /// <summary>Whether the text follows the system text size. Default <c>true</c>.</summary>
    public bool FontAutoScalingEnabled { get => Model.FontAutoScalingEnabled; set => SetFontAutoScalingEnabled(value); }

    /// <summary>DIPs added after each character.</summary>
    public double CharacterSpacing { get => Model.CharacterSpacing; set => SetCharacterSpacing(value); }

    /// <summary>Horizontal text alignment (default start).</summary>
    public TextAlignment HorizontalTextAlignment { get => Model.HorizontalTextAlignment; set => SetHorizontalTextAlignment(value); }

    /// <summary>Vertical text alignment (default centered).</summary>
    public TextAlignment VerticalTextAlignment { get => Model.VerticalTextAlignment; set => SetVerticalTextAlignment(value); }

    /// <summary>Whether the list is open; goes back to <c>false</c> when the popup closes. A picker not on a surface does not open.</summary>
    public bool IsOpen { get => _isOpen; set => SetIsOpen(value); }

    /// <summary>The most the list is tall, in DIPs; -1 (default): no limit.</summary>
    public double MaxPopupHeight { get => Model.MaxPopupHeight; set => SetMaxPopupHeight(value); }

    /// <summary>Where the list opens (see <c>SkUiPicker.PopupPlacement</c>; default the look's).</summary>
    public SkUiPopupPlacement PopupPlacement { get => Model.PopupPlacement; set => SetPopupPlacement(value); }

    /// <summary>The window rectangle an <see cref="SkUiPopupPlacement.Absolute"/> list opens at.</summary>
    public Rect PopupBounds { get => Model.PopupBounds; set => SetPopupBounds(value); }

    /// <summary>This picker's presenter; <c>null</c> (default): the app's (<see cref="SkUiPopups.Presenter"/>).</summary>
    public ISkUiPopupPresenter? PopupPresenter { get => Model.PopupPresenter; set => SetPopupPresenter(value); }

    /// <summary>When a choice made in the popup takes effect: at once (default) or, with Cancel and OK buttons, when accepted (see <c>SkUiPickerBase.UpdateMode</c>).</summary>
    public SkUiPickerUpdateMode UpdateMode { get => Model.UpdateMode; set => SetUpdateMode(value); }

    /// <summary>The OK button's text with <see cref="SkUiPickerUpdateMode.WhenFinished"/>; <c>null</c> (default): <see cref="SkUiStrings.AcceptText"/>.</summary>
    public string? AcceptText { get => Model.AcceptText; set => SetAcceptText(value); }

    /// <summary>The Cancel button's text with <see cref="SkUiPickerUpdateMode.WhenFinished"/>; <c>null</c> (default): <see cref="SkUiStrings.CancelText"/>.</summary>
    public string? CancelText { get => Model.CancelText; set => SetCancelText(value); }

    /// <summary>Shown in the field while nothing is chosen; <c>null</c> (default): <see cref="Title"/>.</summary>
    public string? Placeholder { get => Model.Placeholder; set => SetPlaceholder(value); }

    /// <summary>The placeholder's color; <c>null</c> (default): <see cref="TitleColor"/>, else the color scheme's muted color.</summary>
    public Color? PlaceholderColor { get => Model.PlaceholderColor; set => SetPlaceholderColor(value); }

    /// <summary>The popup's width in DIPs; -1 (default): as wide as its content wants.</summary>
    public double PopupWidth { get => Model.PopupWidth; set => SetPopupWidth(value); }

    /// <summary>Runs with <see cref="SelectionChangedCommandParameter"/> after the choice changes (when it can execute), before the change event.</summary>
    public ICommand? SelectionChangedCommand { get => Model.SelectionChangedCommand; set => SetSelectionChangedCommand(value); }

    /// <summary>The parameter of <see cref="SelectionChangedCommand"/>.</summary>
    public object? SelectionChangedCommandParameter { get => Model.SelectionChangedCommandParameter; set => SetSelectionChangedCommandParameter(value); }

    /// <summary>Whether the field shows a clear button while something is chosen (see <c>SkUiPickerBase.IsClearButtonVisible</c>).</summary>
    public bool IsClearButtonVisible { get => Model.ShowsClearButton; set => SetIsClearButtonVisible(value); }

    /// <summary>Whether the popup shows a Clear button (see <c>SkUiPickerBase.IsPopupClearButtonVisible</c>).</summary>
    public bool IsPopupClearButtonVisible { get => Model.ShowsPopupClearButton; set => SetIsPopupClearButtonVisible(value); }

    /// <summary>The popup's Clear button's text; <c>null</c> (default): <see cref="SkUiStrings.ClearText"/>.</summary>
    public string? ClearText { get => Model.ClearText; set => SetClearText(value); }

    /// <summary>Drawn views of the app's above the popup's list or wheels, created for each opening (without a binding context: Core nodes have none).</summary>
    public DataTemplate? PopupHeaderTemplate { get => Model.PopupHeaderTemplate; set => SetPopupHeaderTemplate(value); }

    /// <summary>Drawn views of the app's below the popup's list or wheels (above its buttons), created for each opening.</summary>
    public DataTemplate? PopupFooterTemplate { get => Model.PopupFooterTemplate; set => SetPopupFooterTemplate(value); }

    /// <summary>The least width of the popup in DIPs; -1 (default): the field's width (anchored) or the look's preferred width (window popups).</summary>
    public double MinPopupWidth { get => Model.MinPopupWidth; set => SetMinPopupWidth(value); }

    /// <summary>The most width of the popup in DIPs (its items are truncated at it); -1 (default): the window's safe area.</summary>
    public double MaxPopupWidth { get => Model.MaxPopupWidth; set => SetMaxPopupWidth(value); }

    /// <summary>Raised when <see cref="IsOpen"/> becomes <c>true</c>.</summary>
    public event EventHandler<PickerOpenedEventArgs>? Opened;

    /// <summary>Raised when <see cref="IsOpen"/> becomes <c>false</c>.</summary>
    public event EventHandler<PickerClosedEventArgs>? Closed;

    private static readonly PickerOpenedEventArgs OpenedArgs = new();
    private static readonly PickerClosedEventArgs ClosedArgs = new();

    /// <summary>Sets <see cref="Title"/>.</summary>
    public SkUiCorePickerBase SetTitle(string? value) => Update(ref Model.Title, value, nameof(Title), measure: true);

    /// <summary>Sets <see cref="TitleColor"/>.</summary>
    public SkUiCorePickerBase SetTitleColor(Color? value) => Update(ref Model.TitleColor, value, nameof(TitleColor));

    /// <summary>Sets <see cref="TextColor"/>.</summary>
    public SkUiCorePickerBase SetTextColor(Color? value) => Update(ref Model.TextColor, value, nameof(TextColor));

    /// <summary>Sets <see cref="FontFamily"/>.</summary>
    public SkUiCorePickerBase SetFontFamily(string? value) => Update(ref Model.FontFamily, value, nameof(FontFamily), measure: true);

    /// <summary>Sets <see cref="FontSize"/>.</summary>
    public SkUiCorePickerBase SetFontSize(double value)
    {
        SkUiValidate.ThrowIfNotFinitePositive(value, nameof(value));
        return Update(ref Model.FontSize, value, nameof(FontSize), measure: true);
    }

    /// <summary>Sets <see cref="FontAttributes"/>.</summary>
    public SkUiCorePickerBase SetFontAttributes(FontAttributes value) => Update(ref Model.FontAttributes, value, nameof(FontAttributes), measure: true);

    /// <summary>Sets <see cref="FontAutoScalingEnabled"/>.</summary>
    public SkUiCorePickerBase SetFontAutoScalingEnabled(bool value) => Update(ref Model.FontAutoScalingEnabled, value, nameof(FontAutoScalingEnabled), measure: true);

    /// <summary>Sets <see cref="CharacterSpacing"/>.</summary>
    public SkUiCorePickerBase SetCharacterSpacing(double value)
    {
        SkUiValidate.ThrowIfNotFinite(value, nameof(value));
        return Update(ref Model.CharacterSpacing, value, nameof(CharacterSpacing), measure: true);
    }

    /// <summary>Sets <see cref="HorizontalTextAlignment"/>.</summary>
    public SkUiCorePickerBase SetHorizontalTextAlignment(TextAlignment value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        return Update(ref Model.HorizontalTextAlignment, value, nameof(HorizontalTextAlignment));
    }

    /// <summary>Sets <see cref="VerticalTextAlignment"/>.</summary>
    public SkUiCorePickerBase SetVerticalTextAlignment(TextAlignment value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        return Update(ref Model.VerticalTextAlignment, value, nameof(VerticalTextAlignment));
    }

    /// <summary>Sets <see cref="IsOpen"/> (opens or closes the list).</summary>
    public SkUiCorePickerBase SetIsOpen(bool value)
    {
        if (value == _isOpen)
            return this;
        if (value && !CanOpen)
            return this; // not on a surface, or hidden: it does not open
        SetProperty(ref _isOpen, value, nameof(IsOpen));
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
        return this;
    }

    /// <summary>Sets <see cref="MaxPopupHeight"/> (-1: no limit).</summary>
    public SkUiCorePickerBase SetMaxPopupHeight(double value)
    {
        SkUiPickerValidation.ThrowIfNotMaxPopupHeight(value, nameof(value));
        return Update(ref Model.MaxPopupHeight, value, nameof(MaxPopupHeight), paint: false);
    }

    /// <summary>Sets <see cref="PopupPlacement"/>.</summary>
    public SkUiCorePickerBase SetPopupPlacement(SkUiPopupPlacement value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        return Update(ref Model.PopupPlacement, value, nameof(PopupPlacement), paint: false);
    }

    /// <summary>Sets <see cref="PopupBounds"/>.</summary>
    public SkUiCorePickerBase SetPopupBounds(Rect value) => Update(ref Model.PopupBounds, value, nameof(PopupBounds), paint: false);

    /// <summary>Sets <see cref="PopupPresenter"/>.</summary>
    public SkUiCorePickerBase SetPopupPresenter(ISkUiPopupPresenter? value) => Update(ref Model.PopupPresenter, value, nameof(PopupPresenter), paint: false);

    /// <summary>Sets <see cref="UpdateMode"/>.</summary>
    public SkUiCorePickerBase SetUpdateMode(SkUiPickerUpdateMode value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        return Update(ref Model.UpdateMode, value, nameof(UpdateMode), paint: false);
    }

    /// <summary>Sets <see cref="AcceptText"/>.</summary>
    public SkUiCorePickerBase SetAcceptText(string? value) => Update(ref Model.AcceptText, value, nameof(AcceptText), paint: false);

    /// <summary>Sets <see cref="CancelText"/>.</summary>
    public SkUiCorePickerBase SetCancelText(string? value) => Update(ref Model.CancelText, value, nameof(CancelText), paint: false);

    /// <summary>Sets <see cref="MinPopupWidth"/>.</summary>
    public SkUiCorePickerBase SetMinPopupWidth(double value)
    {
        SkUiPickerValidation.ThrowIfNotOptionalLength(value, nameof(value));
        return Update(ref Model.MinPopupWidth, value, nameof(MinPopupWidth), paint: false);
    }

    /// <summary>Sets <see cref="MaxPopupWidth"/>.</summary>
    public SkUiCorePickerBase SetMaxPopupWidth(double value)
    {
        SkUiPickerValidation.ThrowIfNotOptionalLength(value, nameof(value));
        return Update(ref Model.MaxPopupWidth, value, nameof(MaxPopupWidth), paint: false);
    }

    /// <summary>Sets <see cref="PopupHeaderTemplate"/>.</summary>
    public SkUiCorePickerBase SetPopupHeaderTemplate(DataTemplate? value) => Update(ref Model.PopupHeaderTemplate, value, nameof(PopupHeaderTemplate), paint: false);

    /// <summary>Sets <see cref="PopupFooterTemplate"/>.</summary>
    public SkUiCorePickerBase SetPopupFooterTemplate(DataTemplate? value) => Update(ref Model.PopupFooterTemplate, value, nameof(PopupFooterTemplate), paint: false);

    /// <summary>Sets <see cref="IsClearButtonVisible"/>.</summary>
    public SkUiCorePickerBase SetIsClearButtonVisible(bool value) => Update(ref Model.ShowsClearButton, value, nameof(IsClearButtonVisible), measure: true);

    /// <summary>Sets <see cref="IsPopupClearButtonVisible"/>.</summary>
    public SkUiCorePickerBase SetIsPopupClearButtonVisible(bool value) => Update(ref Model.ShowsPopupClearButton, value, nameof(IsPopupClearButtonVisible), paint: false);

    /// <summary>Sets <see cref="ClearText"/>.</summary>
    public SkUiCorePickerBase SetClearText(string? value) => Update(ref Model.ClearText, value, nameof(ClearText), paint: false);

    /// <summary>Sets <see cref="Placeholder"/>.</summary>
    public SkUiCorePickerBase SetPlaceholder(string? value) => Update(ref Model.Placeholder, value, nameof(Placeholder), measure: true);

    /// <summary>Sets <see cref="PlaceholderColor"/>.</summary>
    public SkUiCorePickerBase SetPlaceholderColor(Color? value) => Update(ref Model.PlaceholderColor, value, nameof(PlaceholderColor));

    /// <summary>Sets <see cref="PopupWidth"/>.</summary>
    public SkUiCorePickerBase SetPopupWidth(double value)
    {
        SkUiPickerValidation.ThrowIfNotOptionalLength(value, nameof(value));
        return Update(ref Model.PopupWidth, value, nameof(PopupWidth), paint: false);
    }

    /// <summary>Sets <see cref="SelectionChangedCommand"/>.</summary>
    public SkUiCorePickerBase SetSelectionChangedCommand(ICommand? value) => Update(ref Model.SelectionChangedCommand, value, nameof(SelectionChangedCommand), paint: false);

    /// <summary>Sets <see cref="SelectionChangedCommandParameter"/>.</summary>
    public SkUiCorePickerBase SetSelectionChangedCommandParameter(object? value) => Update(ref Model.SelectionChangedCommandParameter, value, nameof(SelectionChangedCommandParameter), paint: false);

    /// <inheritdoc />
    public override SkUiCoreNode SetMinimumHeight(double value)
    {
        _minimumHeightExplicit = true;
        return base.SetMinimumHeight(value);
    }

    private protected SkUiCorePickerBase Update<T>(ref T field, T value, string name, bool measure = false, bool paint = true)
    {
        if (!SetProperty(ref field, value, name))
            return this;
        if (measure)
            Model.TextChanged();
        else if (paint)
            Model.PaintChanged();
        return this;
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

    #region Field

    private bool CanOpen => IsVisible && SkUiDiagnostics.GetSurfaceRoot(this) is not null;

    /// <summary>The field's tokens as laid out: texts, rectangles and ×'s (tests).</summary>
    internal IReadOnlyList<(string Text, Rect Bounds, Rect Remove)> TokenBoxes => Model.TokenBoxes(Frame.Width, Frame.Height);

    /// <summary>The open popup's request (tests), or <c>null</c>.</summary>
    internal SkUiPopupRequest? PopupRequest => Model.Request;

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        Model.HasExplicitMinimumHeight = _minimumHeightExplicit;
        return Model.Measure(widthConstraint, heightConstraint);
    }

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas) => Model.Draw(canvas, (float)Frame.Width, (float)Frame.Height);

    /// <inheritdoc />
    internal override bool HasIntrinsicTap => true;

    /// <inheritdoc />
    internal override void OnIntrinsicTap(SkUiTappedEventArgs args)
    {
        // The clear button clears without opening.
        if (Model.HitsClearButton(args.Position, Frame.Width, Frame.Height))
            ClearChoice();
        else if (Model.TokenRemovedAt(args.Position, Frame.Width, Frame.Height) is var removed and >= 0)
            RemoveChoice(removed);
        else
            SetIsOpen(!_isOpen);
    }

    /// <inheritdoc />
    internal override void OnGesturePressedChanged(bool pressed) => Model.SetPressed(pressed, PressPosition);

    /// <inheritdoc />
    internal override void OnPointerOverChanged(bool isOver)
    {
        Model.IsPointerOver = isOver;
        Model.PaintChanged();
    }

    /// <inheritdoc />
    protected override void OnIsVisibleChanged()
    {
        base.OnIsVisibleChanged();
        if (!IsVisible)
            SetIsOpen(false);
    }

    /// <inheritdoc />
    protected override void OnAnimationRootChanged(bool subtreeDetached = false)
    {
        base.OnAnimationRootChanged(subtreeDetached);
        if (subtreeDetached)
            SetIsOpen(false);
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

    SkUiTextDirection ISkUiPickerOwner.TextDirection => InheritedDirection();

    bool ISkUiPickerOwner.CanOpen => CanOpen;

    IList? ISkUiPickerOwner.ItemsSource => PopupItemsSource;

    void ISkUiPickerOwner.Choose(int index) => ChooseItem(index);

    void ISkUiPickerOwner.ClearChoice() => ClearChoice();

    void ISkUiPickerOwner.RemoveChoice(int index) => RemoveChoice(index);

    object? ISkUiPickerOwner.PopupBindingContext => null;

    void ISkUiPickerOwner.SetIsOpen(bool open) => SetIsOpen(open);

    void ISkUiPickerOwner.FocusField()
    {
        if (IsVisible)
            Focus();
    }

    void ISkUiPickerOwner.InvalidateMeasure() => InvalidateMeasure();

    void ISkUiPickerOwner.InvalidatePaint() => InvalidatePaint();

    void ISkUiPickerOwner.InvalidateSemantics() => InvalidateSemantics();

    #endregion
}
