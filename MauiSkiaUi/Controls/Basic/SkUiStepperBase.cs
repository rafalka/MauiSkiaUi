using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// What every SkUi stepper shares, whatever its value's type: look-drawn − and + buttons, side by side or stacked
/// (<see cref="Orientation"/>), texts in place of − and + (<see cref="DecrementText"/>, <see cref="IncrementText"/>) in their font, the
/// shape's <see cref="CornerRadius"/> and outline (<see cref="BorderWidth"/>, <see cref="BorderColor"/>), a button disabled at its end of
/// the range, held buttons repeating, arrow keys and screen readers' increment / decrement. The value is
/// the derived stepper's: <see cref="SkUiStepper"/> on <see cref="double"/> (MAUI's <c>Stepper</c>), <see cref="SkUiStepper{T}"/> on any
/// number type. The Core twin is <see cref="Core.SkUiCoreStepperBase"/>.
/// </summary>
/// <remarks>Only SkiaUi's steppers derive from it; derive from <see cref="SkUiStepper"/> or <see cref="SkUiStepper{T}"/>.</remarks>
public abstract class SkUiStepperBase : SkUiView, ISkUiStepperOwner, ISkUiKeyHandler
{
    /// <summary>
    /// The engine shared with the Core steppers, created on first use: property callbacks can run before the constructor
    /// (see <see cref="SkUiView.StyledOr{T}"/>).
    /// </summary>
    internal SkUiStepperModel Model => field ??= new SkUiStepperModel(this);

    private protected SkUiStepperBase() { }

    #region Bindable properties

    /// <summary>Bindable property for <see cref="Orientation"/>.</summary>
    public static readonly BindableProperty OrientationProperty = BindableProperty.Create(nameof(Orientation), typeof(StackOrientation), typeof(SkUiStepperBase),
        StackOrientation.Horizontal, validateValue: SkUiValidate.Orientation,
        propertyChanged: (view, _, value) => ((SkUiStepperBase)view).OnOrientationChanged((StackOrientation)value));

    /// <summary>Bindable property for <see cref="TextColor"/>.</summary>
    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(SkUiStepperBase), null,
        propertyChanged: (view, _, value) =>
        {
            var stepper = (SkUiStepperBase)view;
            stepper.Model.TextColor = (Color?)value;
            stepper.InvalidatePaint();
        });

    /// <summary>Bindable property for <see cref="DecrementText"/>.</summary>
    public static readonly BindableProperty DecrementTextProperty = BindableProperty.Create(nameof(DecrementText), typeof(string), typeof(SkUiStepperBase), null,
        propertyChanged: (view, _, value) => ((SkUiStepperBase)view).OnTextStyleChanged(model => model.DecrementText = (string?)value));

    /// <summary>Bindable property for <see cref="IncrementText"/>.</summary>
    public static readonly BindableProperty IncrementTextProperty = BindableProperty.Create(nameof(IncrementText), typeof(string), typeof(SkUiStepperBase), null,
        propertyChanged: (view, _, value) => ((SkUiStepperBase)view).OnTextStyleChanged(model => model.IncrementText = (string?)value));

    /// <summary>Bindable property for <see cref="FontFamily"/>.</summary>
    public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create(nameof(FontFamily), typeof(string), typeof(SkUiStepperBase), null,
        propertyChanged: (view, _, value) => ((SkUiStepperBase)view).OnTextStyleChanged(model => model.FontFamily = (string?)value));

    /// <summary>Bindable property for <see cref="FontSize"/>.</summary>
    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(nameof(FontSize), typeof(double), typeof(SkUiStepperBase), SkUiStepperModel.DefaultFontSize,
        validateValue: SkUiValidate.FinitePositive,
        propertyChanged: (view, _, value) => ((SkUiStepperBase)view).OnTextStyleChanged(model => model.FontSize = (double)value));

    /// <summary>Bindable property for <see cref="FontAttributes"/>.</summary>
    public static readonly BindableProperty FontAttributesProperty = BindableProperty.Create(nameof(FontAttributes), typeof(FontAttributes), typeof(SkUiStepperBase), FontAttributes.None,
        propertyChanged: (view, _, value) => ((SkUiStepperBase)view).OnTextStyleChanged(model => model.FontAttributes = (FontAttributes)value));

    /// <summary>Bindable property for <see cref="FontAutoScalingEnabled"/>.</summary>
    public static readonly BindableProperty FontAutoScalingEnabledProperty = BindableProperty.Create(nameof(FontAutoScalingEnabled), typeof(bool), typeof(SkUiStepperBase), true,
        propertyChanged: (view, _, value) => ((SkUiStepperBase)view).OnTextStyleChanged(model => model.FontAutoScalingEnabled = (bool)value));

    /// <summary>Bindable property for <see cref="CornerRadius"/>.</summary>
    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(nameof(CornerRadius), typeof(int), typeof(SkUiStepperBase), 0,
        defaultValueCreator: _ => (int)Math.Round(SkUiLook.Current.DefaultStepperCornerRadius), validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, value) => ((SkUiStepperBase)view).OnChromeChanged(model => model.CornerRadius = (int)value));

    /// <summary>Bindable property for <see cref="BorderWidth"/>.</summary>
    public static readonly BindableProperty BorderWidthProperty = BindableProperty.Create(nameof(BorderWidth), typeof(double), typeof(SkUiStepperBase), 0d,
        validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, value) => ((SkUiStepperBase)view).OnTextStyleChanged(model => model.BorderWidth = (double)value));

    /// <summary>Bindable property for <see cref="BorderColor"/>.</summary>
    public static readonly BindableProperty BorderColorProperty = BindableProperty.Create(nameof(BorderColor), typeof(Color), typeof(SkUiStepperBase), null,
        propertyChanged: (view, _, value) => ((SkUiStepperBase)view).OnChromeChanged(model => model.BorderColor = (Color?)value));

    #endregion

    #region Properties

    /// <summary>
    /// Whether the buttons sit side by side, − at the start side (<see cref="StackOrientation.Horizontal"/>, the default, as MAUI's), or
    /// stacked, + on top (<see cref="StackOrientation.Vertical"/>; SkiaUi).
    /// </summary>
    public StackOrientation Orientation { get => (StackOrientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>The color of − and + or of their texts (SkiaUi); <c>null</c> (default): the color scheme's foreground.</summary>
    public Color? TextColor { get => (Color?)GetValue(TextColorProperty); set => SetValue(TextColorProperty, value); }

    /// <summary>
    /// The − button's text, in place of the look's − (SkiaUi): a word, or a symbol of an icon font set by <see cref="FontFamily"/>;
    /// <c>null</c> or empty (default): the look's −.
    /// </summary>
    public string? DecrementText { get => (string?)GetValue(DecrementTextProperty); set => SetValue(DecrementTextProperty, value); }

    /// <summary>
    /// The + button's text, in place of the look's + (SkiaUi): a word, or a symbol of an icon font set by <see cref="FontFamily"/>;
    /// <c>null</c> or empty (default): the look's +.
    /// </summary>
    public string? IncrementText { get => (string?)GetValue(IncrementTextProperty); set => SetValue(IncrementTextProperty, value); }

    /// <summary>
    /// The font of <see cref="DecrementText"/> and <see cref="IncrementText"/> (SkiaUi): a system family, or a name registered with
    /// <see cref="SkUiFonts.Register"/> / MAUI's <c>ConfigureFonts</c>.
    /// </summary>
    public string? FontFamily { get => (string?)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    /// <summary>The texts' font size in DIPs (SkiaUi; default 16).</summary>
    [System.ComponentModel.TypeConverter(typeof(FontSizeConverter))]
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>Bold and / or italic texts (SkiaUi).</summary>
    public FontAttributes FontAttributes { get => (FontAttributes)GetValue(FontAttributesProperty); set => SetValue(FontAttributesProperty, value); }

    /// <summary>Whether the texts follow the system text size (<see cref="SkUiFontScaling"/>), as MAUI's (SkiaUi; default <c>true</c>).</summary>
    public bool FontAutoScalingEnabled { get => (bool)GetValue(FontAutoScalingEnabledProperty); set => SetValue(FontAutoScalingEnabledProperty, value); }

    /// <summary>
    /// The corner radius in DIPs, an <c>int</c> as MAUI Button's (SkiaUi; default the look's <see cref="SkUiLook.DefaultStepperCornerRadius"/>,
    /// 8). 0 gives square corners; half the stepper's height or more, a pill.
    /// </summary>
    public int CornerRadius { get => (int)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    /// <summary>
    /// The outline's width in DIPs (SkiaUi; default 0: no outline). The outline is drawn inside the stepper, and the divider between the
    /// buttons takes its width and color.
    /// </summary>
    public double BorderWidth { get => (double)GetValue(BorderWidthProperty); set => SetValue(BorderWidthProperty, value); }

    /// <summary>The outline's color (SkiaUi); <c>null</c> (default): the color scheme's muted color.</summary>
    public Color? BorderColor { get => (Color?)GetValue(BorderColorProperty); set => SetValue(BorderColorProperty, value); }

    /// <summary>Sets <see cref="Orientation"/>.</summary>
    public SkUiStepperBase SetOrientation(StackOrientation value) { SkUiValidate.ThrowIfNotOrientation(value, nameof(value)); Orientation = value; return this; }

    /// <summary>Sets <see cref="TextColor"/>.</summary>
    public SkUiStepperBase SetTextColor(Color? value) { TextColor = value; return this; }

    /// <summary>Sets <see cref="DecrementText"/>.</summary>
    public SkUiStepperBase SetDecrementText(string? value) { DecrementText = value; return this; }

    /// <summary>Sets <see cref="IncrementText"/>.</summary>
    public SkUiStepperBase SetIncrementText(string? value) { IncrementText = value; return this; }

    /// <summary>Sets <see cref="FontFamily"/>.</summary>
    public SkUiStepperBase SetFontFamily(string? value) { FontFamily = value; return this; }

    /// <summary>Sets <see cref="FontSize"/>.</summary>
    public SkUiStepperBase SetFontSize(double value) { SkUiValidate.ThrowIfNotFinitePositive(value, nameof(value)); FontSize = value; return this; }

    /// <summary>Sets <see cref="FontAttributes"/>.</summary>
    public SkUiStepperBase SetFontAttributes(FontAttributes value) { FontAttributes = value; return this; }

    /// <summary>Sets <see cref="FontAutoScalingEnabled"/>.</summary>
    public SkUiStepperBase SetFontAutoScalingEnabled(bool value) { FontAutoScalingEnabled = value; return this; }

    /// <summary>Sets <see cref="CornerRadius"/>.</summary>
    public SkUiStepperBase SetCornerRadius(int value) { ArgumentOutOfRangeException.ThrowIfNegative(value); CornerRadius = value; return this; }

    /// <summary>Sets <see cref="BorderWidth"/>.</summary>
    public SkUiStepperBase SetBorderWidth(double value) { SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value)); BorderWidth = value; return this; }

    /// <summary>Sets <see cref="BorderColor"/>.</summary>
    public SkUiStepperBase SetBorderColor(Color? value) { BorderColor = value; return this; }

    #endregion

    #region Value (the derived stepper's)

    /// <summary>Whether the value can move down (-1) or up (+1) within the range.</summary>
    private protected abstract bool CanStep(int direction);

    /// <summary>One step down (-1) or up (+1) by the user.</summary>
    private protected abstract void Step(int direction);

    /// <summary>The range and the value for screen readers.</summary>
    private protected abstract SkUiSemanticsRange SemanticsRange { get; }

    /// <summary>The value as screen readers read it.</summary>
    private protected abstract string ValueText { get; }

    /// <summary>A screen reader set the value (Windows' RangeValue).</summary>
    private protected abstract void SetValueFromSemantics(double value);

    /// <summary>The value or the range changed: the buttons' states and the screen reader's value follow.</summary>
    private protected void OnValueOrRangeChanged() => Model.ValueChanged();

    private void OnTextStyleChanged(Action<SkUiStepperModel> apply)
    {
        apply(Model);
        Model.TextChanged();
    }

    private void OnChromeChanged(Action<SkUiStepperModel> apply)
    {
        apply(Model);
        InvalidatePaint();
    }

    private void OnOrientationChanged(StackOrientation value)
    {
        Model.Orientation = value;
        InvalidateMeasureOverride();
        InvalidatePaint();
        InvalidateSemantics();
    }

    #endregion

    #region View

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => Model.Measure(widthConstraint, heightConstraint);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas) => Model.Draw(canvas, (float)Width, (float)Height);

    /// <inheritdoc />
    internal override void CollectGestureRecognizers(List<SkUiGestureRecognizer> recognizers)
    {
        base.CollectGestureRecognizers(recognizers);
        if (IsEnabled)
            Model.CollectGestureRecognizers(recognizers);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        switch (propertyName)
        {
            case nameof(IsEnabled):
                Model.IsEnabled = IsEnabled;
                InvalidatePaint();
                break;
            case nameof(IsPointerOver):
                Model.IsPointerOver = IsPointerOver;
                InvalidatePaint();
                break;
            case nameof(IsFocused):
                Model.SetFocused(IsFocused);
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
    protected override bool OnSemanticsSetValue(double value)
    {
        SetValueFromSemantics(value);
        return true;
    }

    /// <inheritdoc />
    internal override bool TakesKeyboardFocus => true;

    /// <inheritdoc />
    internal override bool ShowsFocusRing => false;

    bool ISkUiKeyHandler.OnKeyDown(in SkUiKeyEvent key) => Model.OnKeyDown(key);

    #endregion

    #region Owner

    bool ISkUiStepperOwner.CanStep(int direction) => CanStep(direction);

    void ISkUiStepperOwner.Step(int direction) => Step(direction);

    SkUiSemanticsRange ISkUiStepperOwner.SemanticsRange => SemanticsRange;

    string ISkUiStepperOwner.ValueText => ValueText;

    Size ISkUiStepperOwner.StepperSize => new(Width, Height);

    bool ISkUiStepperOwner.IsRightToLeft => IsRightToLeft;

    void ISkUiStepperOwner.InvalidateMeasure() => InvalidateMeasureOverride();

    void ISkUiStepperOwner.InvalidatePaint() => InvalidatePaint();

    void ISkUiStepperOwner.InvalidateSemantics() => InvalidateSemantics();

    #endregion
}
