using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// A stepper on the Core layer (the twin of <see cref="SkUiStepper"/>, on the same engine): look-drawn − and + buttons, held buttons
/// repeating, keys and screen readers, and the optional editable value between the buttons (<see cref="IsValueVisible"/>). The value
/// is kept within the range and rounded by the increment as MAUI's stepper does.
/// </summary>
/// <remarks>Core nodes have no enabled state: the stepper always reacts (hide it, or remove it, to stop it).</remarks>
public class SkUiCoreStepper : SkUiCoreNode, ISkUiStepperOwner, ISkUiKeyHandler, ISkUiTextInputNode
{
    private double _value;
    private double _minimum;
    private double _maximum = 100;
    private double _increment = 1;
    private int _digits = 4;
    private double _requestedValue;

    /// <summary>The engine shared with <see cref="SkUiStepper"/>.</summary>
    internal SkUiStepperModel Model { get; }

    /// <summary>Creates a stepper from 0 to 100 by 1.</summary>
    public SkUiCoreStepper()
    {
        Model = new SkUiStepperModel(this);
        Focused += (_, _) => Model.SetFocused(true);
        Unfocused += (_, _) => Model.SetFocused(false);
    }

    /// <summary>The value, rounded and kept within the range.</summary>
    public double Value { get => _value; set => SetValue(value); }

    /// <summary>The smallest value (default 0).</summary>
    public double Minimum { get => _minimum; set => SetMinimum(value); }

    /// <summary>The largest value (default 100).</summary>
    public double Maximum { get => _maximum; set => SetMaximum(value); }

    /// <summary>How much a button changes the value (default 1).</summary>
    public double Increment { get => _increment; set => SetIncrement(value); }

    /// <summary>Whether the value shows between the buttons, editable.</summary>
    public bool IsValueVisible { get => Model.IsValueVisible; set => SetIsValueVisible(value); }

    /// <summary>How the shown value is formatted (a .NET numeric format; default "G").</summary>
    public string? ValueFormat { get => Model.ValueFormat; set => SetValueFormat(value); }

    /// <summary>The color of − and + and of the shown value; <c>null</c> (default): the color scheme's foreground.</summary>
    public Color? TextColor { get => Model.TextColor; set => SetTextColor(value); }

    /// <summary>Raised when <see cref="Value"/> changes.</summary>
    public event EventHandler<ValueChangedEventArgs>? ValueChanged;

    /// <summary>Sets <see cref="Value"/> (kept within the range; the asked value comes back when the range widens).</summary>
    public SkUiCoreStepper SetValue(double value)
    {
        _requestedValue = value;
        return ApplyValue(value);
    }

    private SkUiCoreStepper ApplyValue(double value)
    {
        var old = _value;
        if (!SetProperty(ref _value, Math.Clamp(Math.Round(value, _digits), _minimum, _maximum), nameof(Value)))
            return this;
        Model.ValueChanged();
        ValueChanged?.Invoke(this, new ValueChangedEventArgs(old, _value));
        return this;
    }

    /// <summary>Sets <see cref="Minimum"/> (not above <see cref="Maximum"/>).</summary>
    public SkUiCoreStepper SetMinimum(double value)
    {
        if (value > _maximum)
            throw new ArgumentOutOfRangeException(nameof(value));
        if (SetProperty(ref _minimum, value, nameof(Minimum)))
            Recoerce();
        return this;
    }

    /// <summary>Sets <see cref="Maximum"/> (not below <see cref="Minimum"/>).</summary>
    public SkUiCoreStepper SetMaximum(double value)
    {
        if (value < _minimum)
            throw new ArgumentOutOfRangeException(nameof(value));
        if (SetProperty(ref _maximum, value, nameof(Maximum)))
            Recoerce();
        return this;
    }

    private void Recoerce()
    {
        ApplyValue(_requestedValue);
        Model.ValueChanged();
    }

    /// <summary>Sets <see cref="Increment"/>.</summary>
    public SkUiCoreStepper SetIncrement(double value)
    {
        if (SetProperty(ref _increment, value, nameof(Increment)))
            _digits = (int)Math.Clamp(-Math.Log10(value) + 4, 1, 15);
        return this;
    }

    /// <summary>Sets <see cref="IsValueVisible"/>.</summary>
    public SkUiCoreStepper SetIsValueVisible(bool value)
    {
        if (!Model.SetValueVisible(value))
            return this;
        OnPropertyChanged(nameof(IsValueVisible));
        InvalidateMeasure();
        if (IsFocused)
            Model.Field.SetFocused(value);
        SkUiSemantics.RootOf(this)?.FocusManagerIfCreated?.TextInputChanged(this);
        return this;
    }

    /// <summary>Sets <see cref="ValueFormat"/>.</summary>
    public SkUiCoreStepper SetValueFormat(string? value)
    {
        if (SetProperty(ref Model.ValueFormat, value, nameof(ValueFormat)))
            Model.ValueChanged();
        return this;
    }

    /// <summary>Sets <see cref="TextColor"/>.</summary>
    public SkUiCoreStepper SetTextColor(Color? value)
    {
        if (!SetProperty(ref Model.TextColor, value, nameof(TextColor)))
            return this;
        Model.Field.TextColor = value;
        InvalidatePaint();
        return this;
    }

    #region Node

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => Model.Measure(widthConstraint, heightConstraint);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas) => Model.Draw(canvas, (float)Frame.Width, (float)Frame.Height);

    /// <inheritdoc />
    internal override void OnGetRenderProps(ref SkUiRenderProps props)
    {
        base.OnGetRenderProps(ref props);
        props.Drawable = Model.GetCaretDrawable((float)props.Width, (float)props.Height);
    }

    /// <inheritdoc />
    private protected override void ForgetOwnDrawable() => Model.Field.ForgetCaret();

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
        SetValue(value);
        return true;
    }

    /// <inheritdoc />
    internal override bool TakesKeyboardFocus => true;

    /// <inheritdoc />
    internal override bool ShowsFocusRing => false;

    bool ISkUiKeyHandler.OnKeyDown(in SkUiKeyEvent key) => Model.OnKeyDown(key);

    ISkUiTextInputClient ISkUiTextInputNode.TextInputClient => Model.Field;

    bool ISkUiTextInputNode.WantsTextInput => Model.IsValueVisible;

    bool ISkUiTextInputNode.CanTakeTextFocus => IsVisible;

    #endregion

    #region Owner

    void ISkUiStepperOwner.SetValueFromUser(double value) => SetValue(value);

    ISkUiRenderable ISkUiTextFieldOwner.Node => this;

    Size ISkUiTextFieldOwner.FieldSize => Frame.Size;

    bool ISkUiTextFieldOwner.IsRightToLeft => IsRightToLeft;

    SkUiTextDirection ISkUiTextFieldOwner.TextDirection => InheritedDirection();

    bool ISkUiTextFieldOwner.IsFocused => IsFocused;

    void ISkUiTextFieldOwner.TextEdited(string text) => Model.FieldEdited();

    void ISkUiTextFieldOwner.SelectionEdited(int start, int length)
    {
    }

    void ISkUiTextFieldOwner.Completed() => Model.Commit();

    bool ISkUiTextFieldOwner.RequestFocus() => Focus();

    void ISkUiTextFieldOwner.ReleaseFocus() => Unfocus();

    void ISkUiTextFieldOwner.InvalidateMeasure() => InvalidateMeasure();

    void ISkUiTextFieldOwner.InvalidatePaint() => InvalidatePaint();

    void ISkUiTextFieldOwner.InvalidateSemantics() => InvalidateSemantics();

    void ISkUiTextFieldOwner.InvalidateCaret() => InvalidateRender(SkUiRenderDirty.Props);

    #endregion
}
