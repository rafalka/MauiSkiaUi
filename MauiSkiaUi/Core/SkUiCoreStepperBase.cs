using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// What every Core stepper shares, whatever its value's type (the twin of <see cref="SkUiStepperBase"/>, on the same engine):
/// look-drawn − and + buttons, side by side or stacked (<see cref="Orientation"/>), texts in place of − and + (<see cref="DecrementText"/>,
/// <see cref="IncrementText"/>), the shape's <see cref="CornerRadius"/> and outline (<see cref="BorderWidth"/>, <see cref="BorderColor"/>),
/// held buttons repeating, keys and screen readers. The value is the derived stepper's:
/// <see cref="SkUiCoreStepper"/> on <see cref="double"/>, <see cref="SkUiCoreStepper{T}"/> on any number type.
/// </summary>
/// <remarks>
/// Only SkiaUi's steppers derive from it. Core nodes have no enabled state: a stepper always reacts (hide it, or remove it, to stop
/// it).
/// </remarks>
public abstract class SkUiCoreStepperBase : SkUiCoreNode, ISkUiStepperOwner, ISkUiKeyHandler
{
    /// <summary>The engine shared with the SkUi steppers.</summary>
    internal SkUiStepperModel Model { get; }

    private protected SkUiCoreStepperBase()
    {
        Model = new SkUiStepperModel(this);
        Focused += (_, _) => Model.SetFocused(true);
        Unfocused += (_, _) => Model.SetFocused(false);
    }

    /// <summary>Whether the buttons sit side by side, − at the start side (default), or stacked, + on top.</summary>
    public StackOrientation Orientation { get => Model.Orientation; set => SetOrientation(value); }

    /// <summary>The color of − and + or of their texts; <c>null</c> (default): the color scheme's foreground.</summary>
    public Color? TextColor { get => Model.TextColor; set => SetTextColor(value); }

    /// <summary>The − button's text (see <c>SkUiStepper.DecrementText</c>); <c>null</c> or empty (default): the look's −.</summary>
    public string? DecrementText { get => Model.DecrementText; set => SetDecrementText(value); }

    /// <summary>The + button's text (see <c>SkUiStepper.IncrementText</c>); <c>null</c> or empty (default): the look's +.</summary>
    public string? IncrementText { get => Model.IncrementText; set => SetIncrementText(value); }

    /// <summary>The texts' font family (see <c>SkUiStepper.FontFamily</c>).</summary>
    public string? FontFamily { get => Model.FontFamily; set => SetFontFamily(value); }

    /// <summary>The texts' font size in DIPs (default 16).</summary>
    public double FontSize { get => Model.FontSize; set => SetFontSize(value); }

    /// <summary>Bold and / or italic texts.</summary>
    public FontAttributes FontAttributes { get => Model.FontAttributes; set => SetFontAttributes(value); }

    /// <summary>Whether the texts follow the system text size (default <c>true</c>).</summary>
    public bool FontAutoScalingEnabled { get => Model.FontAutoScalingEnabled; set => SetFontAutoScalingEnabled(value); }

    /// <summary>
    /// The corner radius in DIPs (default the look's <see cref="SkUiLook.DefaultStepperCornerRadius"/>, 8): 0 gives square corners; half the
    /// stepper's height or more, a pill.
    /// </summary>
    public double CornerRadius { get => Model.CornerRadius ?? SkUiLook.Current.DefaultStepperCornerRadius; set => SetCornerRadius(value); }

    /// <summary>
    /// The outline's width in DIPs (default 0: no outline), drawn inside the stepper; the divider between the buttons takes its width and
    /// color.
    /// </summary>
    public double BorderWidth { get => Model.BorderWidth; set => SetBorderWidth(value); }

    /// <summary>The outline's color; <c>null</c> (default): the color scheme's muted color.</summary>
    public Color? BorderColor { get => Model.BorderColor; set => SetBorderColor(value); }

    /// <summary>Sets <see cref="Orientation"/>.</summary>
    public SkUiCoreStepperBase SetOrientation(StackOrientation value)
    {
        SkUiValidate.ThrowIfNotOrientation(value, nameof(value));
        if (!SetProperty(ref Model.Orientation, value, nameof(Orientation)))
            return this;
        InvalidateMeasure();
        InvalidatePaint();
        InvalidateSemantics();
        return this;
    }

    /// <summary>Sets <see cref="TextColor"/>.</summary>
    public SkUiCoreStepperBase SetTextColor(Color? value)
    {
        if (!SetProperty(ref Model.TextColor, value, nameof(TextColor)))
            return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets <see cref="DecrementText"/>.</summary>
    public SkUiCoreStepperBase SetDecrementText(string? value) => SetTextStyle(ref Model.DecrementText, value, nameof(DecrementText));

    /// <summary>Sets <see cref="IncrementText"/>.</summary>
    public SkUiCoreStepperBase SetIncrementText(string? value) => SetTextStyle(ref Model.IncrementText, value, nameof(IncrementText));

    /// <summary>Sets <see cref="FontFamily"/>.</summary>
    public SkUiCoreStepperBase SetFontFamily(string? value) => SetTextStyle(ref Model.FontFamily, value, nameof(FontFamily));

    /// <summary>Sets <see cref="FontSize"/>.</summary>
    public SkUiCoreStepperBase SetFontSize(double value)
    {
        SkUiValidate.ThrowIfNotFinitePositive(value, nameof(value));
        return SetTextStyle(ref Model.FontSize, value, nameof(FontSize));
    }

    /// <summary>Sets <see cref="FontAttributes"/>.</summary>
    public SkUiCoreStepperBase SetFontAttributes(FontAttributes value) => SetTextStyle(ref Model.FontAttributes, value, nameof(FontAttributes));

    /// <summary>Sets <see cref="FontAutoScalingEnabled"/>.</summary>
    public SkUiCoreStepperBase SetFontAutoScalingEnabled(bool value) => SetTextStyle(ref Model.FontAutoScalingEnabled, value, nameof(FontAutoScalingEnabled));

    /// <summary>Sets <see cref="CornerRadius"/>.</summary>
    public SkUiCoreStepperBase SetCornerRadius(double value)
    {
        SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value));
        double? radius = value;
        if (SetProperty(ref Model.CornerRadius, radius, nameof(CornerRadius)))
            InvalidatePaint();
        return this;
    }

    /// <summary>Sets <see cref="BorderWidth"/>.</summary>
    public SkUiCoreStepperBase SetBorderWidth(double value)
    {
        SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value));
        return SetTextStyle(ref Model.BorderWidth, value, nameof(BorderWidth));
    }

    /// <summary>Sets <see cref="BorderColor"/>.</summary>
    public SkUiCoreStepperBase SetBorderColor(Color? value)
    {
        if (SetProperty(ref Model.BorderColor, value, nameof(BorderColor)))
            InvalidatePaint();
        return this;
    }

    private SkUiCoreStepperBase SetTextStyle<TField>(ref TField field, TField value, string propertyName)
    {
        if (SetProperty(ref field, value, propertyName))
            Model.TextChanged();
        return this;
    }

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

    #endregion

    #region Node

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => Model.Measure(widthConstraint, heightConstraint);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas) => Model.Draw(canvas, (float)Frame.Width, (float)Frame.Height);

    /// <inheritdoc />
    internal override void CollectGestureRecognizers(List<SkUiGestureRecognizer> recognizers)
    {
        base.CollectGestureRecognizers(recognizers);
        Model.CollectGestureRecognizers(recognizers);
    }

    /// <inheritdoc />
    internal override void OnPointerOverChanged(bool isOver)
    {
        Model.IsPointerOver = isOver;
        InvalidatePaint();
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

    Size ISkUiStepperOwner.StepperSize => Frame.Size;

    bool ISkUiStepperOwner.IsRightToLeft => IsRightToLeft;

    void ISkUiStepperOwner.InvalidateMeasure() => InvalidateMeasure();

    void ISkUiStepperOwner.InvalidatePaint() => InvalidatePaint();

    void ISkUiStepperOwner.InvalidateSemantics() => InvalidateSemantics();

    #endregion
}
