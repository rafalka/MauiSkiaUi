using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// A stepper drawn by SkiaUi (MAUI's <c>Stepper</c>): look-drawn − and + buttons that change <see cref="Value"/> by
/// <see cref="Increment"/> within <see cref="Minimum"/>–<see cref="Maximum"/> (coerced as MAUI's), a button disabled at its end of the
/// range, held buttons repeating, arrow keys and screen readers' increment / decrement. Beyond MAUI: <see cref="IsValueVisible"/> shows
/// the value between the buttons, editable with the numeric keyboard (committed on Return or when focus leaves). The Core twin is
/// <see cref="Core.SkUiCoreStepper"/>.
/// </summary>
public class SkUiStepper : SkUiView, ISkUiStepperOwner, ISkUiKeyHandler, ISkUiTextInputNode
{
    // MAUI's rounding: 4 significant digits after the increment's most significant one.
    private int _digits = 4;
    // The value the app asked for before it was clamped: restored when the range widens again (as MAUI's).
    private double _requestedValue;
    private bool _userSetValue;
    private bool _recoercing;

    /// <summary>The engine shared with the Core stepper.</summary>
    internal SkUiStepperModel Model { get; }

    /// <summary>Creates a stepper from 0 to 100 by 1.</summary>
    public SkUiStepper() => Model = new SkUiStepperModel(this);

    /// <summary>Creates a stepper with a range, a value and an increment (MAUI's constructor).</summary>
    public SkUiStepper(double min, double max, double val, double increment) : this()
    {
        if (min >= max)
            throw new ArgumentOutOfRangeException(nameof(min));
        if (max > Minimum)
        {
            Maximum = max;
            Minimum = min;
        }
        else
        {
            Minimum = min;
            Maximum = max;
        }
        Increment = increment;
        Value = Math.Clamp(val, min, max);
    }

    #region Bindable properties

    /// <summary>Bindable property for <see cref="Maximum"/>.</summary>
    public static readonly BindableProperty MaximumProperty = BindableProperty.Create(nameof(Maximum), typeof(double), typeof(SkUiStepper), 100d,
        validateValue: (view, value) => (double)value >= ((SkUiStepper)view).Minimum,
        propertyChanged: (view, _, _) => ((SkUiStepper)view).Recoerce());

    /// <summary>Bindable property for <see cref="Minimum"/>.</summary>
    public static readonly BindableProperty MinimumProperty = BindableProperty.Create(nameof(Minimum), typeof(double), typeof(SkUiStepper), 0d,
        validateValue: (view, value) => (double)value <= ((SkUiStepper)view).Maximum,
        propertyChanged: (view, _, _) => ((SkUiStepper)view).Recoerce());

    /// <summary>Bindable property for <see cref="Value"/> (two-way by default).</summary>
    public static readonly BindableProperty ValueProperty = BindableProperty.Create(nameof(Value), typeof(double), typeof(SkUiStepper), 0d, BindingMode.TwoWay,
        coerceValue: (view, value) => ((SkUiStepper)view).Coerce((double)value),
        propertyChanged: (view, oldValue, newValue) => ((SkUiStepper)view).OnValueChanged((double)oldValue, (double)newValue));

    /// <summary>Bindable property for <see cref="Increment"/>.</summary>
    public static readonly BindableProperty IncrementProperty = BindableProperty.Create(nameof(Increment), typeof(double), typeof(SkUiStepper), 1d,
        propertyChanged: (view, _, value) => ((SkUiStepper)view)._digits = (int)Math.Clamp(-Math.Log10((double)value) + 4, 1, 15));

    /// <summary>Bindable property for <see cref="IsValueVisible"/>.</summary>
    public static readonly BindableProperty IsValueVisibleProperty = BindableProperty.Create(nameof(IsValueVisible), typeof(bool), typeof(SkUiStepper), false,
        propertyChanged: (view, _, value) => ((SkUiStepper)view).OnIsValueVisibleChanged((bool)value));

    /// <summary>Bindable property for <see cref="ValueFormat"/>.</summary>
    public static readonly BindableProperty ValueFormatProperty = BindableProperty.Create(nameof(ValueFormat), typeof(string), typeof(SkUiStepper), null,
        propertyChanged: (view, _, value) =>
        {
            var stepper = (SkUiStepper)view;
            stepper.Model.ValueFormat = (string?)value;
            stepper.Model.ValueChanged();
        });

    /// <summary>Bindable property for <see cref="TextColor"/>.</summary>
    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(SkUiStepper), null,
        propertyChanged: (view, _, value) =>
        {
            var stepper = (SkUiStepper)view;
            stepper.Model.TextColor = (Color?)value;
            stepper.Model.Field.TextColor = (Color?)value;
            stepper.InvalidatePaint();
        });

    /// <summary>Bindable property for <see cref="FontSize"/>.</summary>
    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(nameof(FontSize), typeof(double), typeof(SkUiStepper), SkUiTextFieldModel.DefaultFontSize,
        validateValue: SkUiValidate.FinitePositive,
        propertyChanged: (view, _, value) =>
        {
            var stepper = (SkUiStepper)view;
            stepper.Model.Field.FontSize = (double)value;
            stepper.Model.Field.TextChanged();
        });

    #endregion

    #region Properties

    /// <summary>The largest value (default 100); not below <see cref="Minimum"/>.</summary>
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    /// <summary>The smallest value (default 0); not above <see cref="Maximum"/>.</summary>
    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

    /// <summary>The value (two-way by default), rounded and kept within the range as MAUI's.</summary>
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>How much a button changes the value (default 1).</summary>
    public double Increment { get => (double)GetValue(IncrementProperty); set => SetValue(IncrementProperty, value); }

    /// <summary>Whether the value shows between the buttons, editable (SkiaUi; default <c>false</c>, MAUI's buttons only).</summary>
    public bool IsValueVisible { get => (bool)GetValue(IsValueVisibleProperty); set => SetValue(IsValueVisibleProperty, value); }

    /// <summary>How the shown value is formatted (a .NET numeric format, current culture; SkiaUi; default "G").</summary>
    public string? ValueFormat { get => (string?)GetValue(ValueFormatProperty); set => SetValue(ValueFormatProperty, value); }

    /// <summary>The color of − and + and of the shown value (SkiaUi); <c>null</c> (default): the color scheme's foreground.</summary>
    public Color? TextColor { get => (Color?)GetValue(TextColorProperty); set => SetValue(TextColorProperty, value); }

    /// <summary>The shown value's font size in DIPs (SkiaUi; default 16).</summary>
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>Raised when <see cref="Value"/> changes.</summary>
    public event EventHandler<ValueChangedEventArgs>? ValueChanged;

    /// <summary>Sets <see cref="Maximum"/>.</summary>
    public SkUiStepper SetMaximum(double value) { if (value < Minimum) throw new ArgumentOutOfRangeException(nameof(value)); Maximum = value; return this; }

    /// <summary>Sets <see cref="Minimum"/>.</summary>
    public SkUiStepper SetMinimum(double value) { if (value > Maximum) throw new ArgumentOutOfRangeException(nameof(value)); Minimum = value; return this; }

    /// <summary>Sets <see cref="Value"/>.</summary>
    public SkUiStepper SetValue(double value) { Value = value; return this; }

    /// <summary>Sets <see cref="Increment"/>.</summary>
    public SkUiStepper SetIncrement(double value) { Increment = value; return this; }

    /// <summary>Sets <see cref="IsValueVisible"/>.</summary>
    public SkUiStepper SetIsValueVisible(bool value) { IsValueVisible = value; return this; }

    /// <summary>Sets <see cref="ValueFormat"/>.</summary>
    public SkUiStepper SetValueFormat(string? value) { ValueFormat = value; return this; }

    /// <summary>Sets <see cref="TextColor"/>.</summary>
    public SkUiStepper SetTextColor(Color? value) { TextColor = value; return this; }

    /// <summary>Sets <see cref="FontSize"/>.</summary>
    public SkUiStepper SetFontSize(double value) { SkUiValidate.ThrowIfNotFinitePositive(value, nameof(value)); FontSize = value; return this; }

    #endregion

    #region Value

    private double Coerce(double value)
    {
        if (!_recoercing)
        {
            _requestedValue = value;
            _userSetValue = true;
        }
        return Math.Clamp(Math.Round(value, _digits), Minimum, Maximum);
    }

    private void Recoerce()
    {
        _recoercing = true;
        try
        {
            Value = _userSetValue ? _requestedValue : Math.Clamp(Value, Minimum, Maximum);
        }
        finally
        {
            _recoercing = false;
        }
        Model.ValueChanged();
    }

    private void OnValueChanged(double oldValue, double newValue)
    {
        Model.ValueChanged();
        ValueChanged?.Invoke(this, new ValueChangedEventArgs(oldValue, newValue));
    }

    private void OnIsValueVisibleChanged(bool visible)
    {
        if (!Model.SetValueVisible(visible))
            return;
        InvalidateMeasureOverride();
        if (IsFocused)
            Model.Field.SetFocused(visible);
        SkUiSemantics.RootOf(this)?.FocusManagerIfCreated?.TextInputChanged(this);
    }

    #endregion

    #region View

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => Model.Measure(widthConstraint, heightConstraint);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas) => Model.Draw(canvas, (float)Width, (float)Height);

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
                Model.IsEnabled = Model.Field.IsEnabled = IsEnabled;
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
        Model.Field.TextChanged();
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
        Value = value;
        return true;
    }

    /// <inheritdoc />
    internal override bool TakesKeyboardFocus => true;

    /// <inheritdoc />
    internal override bool ShowsFocusRing => false;

    bool ISkUiKeyHandler.OnKeyDown(in SkUiKeyEvent key) => Model.OnKeyDown(key);

    ISkUiTextInputClient ISkUiTextInputNode.TextInputClient => Model.Field;

    bool ISkUiTextInputNode.WantsTextInput => Model.IsValueVisible;

    bool ISkUiTextInputNode.CanTakeTextFocus => IsVisible && IsEnabled;

    #endregion

    #region Owner

    double ISkUiStepperOwner.Value => Value;

    void ISkUiStepperOwner.SetValueFromUser(double value) => Value = value;

    ISkUiRenderable ISkUiTextFieldOwner.Node => this;

    Size ISkUiTextFieldOwner.FieldSize => new(Width, Height);

    bool ISkUiTextFieldOwner.IsRightToLeft => IsRightToLeft;

    SkUiTextDirection ISkUiTextFieldOwner.TextDirection => IsRightToLeft ? SkUiTextDirection.RightToLeft : SkUiTextDirection.Auto;

    bool ISkUiTextFieldOwner.IsFocused => IsFocused;

    void ISkUiTextFieldOwner.TextEdited(string text) => Model.FieldEdited();

    void ISkUiTextFieldOwner.SelectionEdited(int start, int length)
    {
    }

    void ISkUiTextFieldOwner.Completed() => Model.Commit();

    bool ISkUiTextFieldOwner.RequestFocus() => Focus();

    void ISkUiTextFieldOwner.ReleaseFocus() => Unfocus();

    void ISkUiTextFieldOwner.InvalidateMeasure() => InvalidateMeasureOverride();

    void ISkUiTextFieldOwner.InvalidatePaint() => InvalidatePaint();

    void ISkUiTextFieldOwner.InvalidateSemantics() => InvalidateSemantics();

    void ISkUiTextFieldOwner.InvalidateCaret() => InvalidateRender(SkUiRenderDirty.Props);

    #endregion
}
