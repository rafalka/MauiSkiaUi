using System.Globalization;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>What the shared stepper engine (<see cref="SkUiStepperModel"/>) needs from its control, on either layer.</summary>
internal interface ISkUiStepperOwner : ISkUiTextFieldOwner
{
    double Value { get; }
    double Minimum { get; }
    double Maximum { get; }
    double Increment { get; }

    /// <summary>The user stepped or typed a value: the control sets its <c>Value</c> (coerced to the range as MAUI's).</summary>
    void SetValueFromUser(double value);
}

/// <summary>
/// The engine both steppers share (<see cref="SkUiStepper"/>, <see cref="Core.SkUiCoreStepper"/>): the − and + buttons drawn by the
/// look (pressed, disabled at the range's ends, focused), taps and held buttons repeating, keys and screen readers, and the optional
/// value between the buttons (<see cref="IsValueVisible"/>), edited in a text field on the shared field engine: a numeric keyboard,
/// committed on Return or when focus leaves, coerced to the range; text that is not a number goes back to the value.
/// </summary>
internal sealed class SkUiStepperModel
{
    private readonly ISkUiStepperOwner _owner;
    private readonly SkUiTween _decrementPress;
    private readonly SkUiTween _incrementPress;
    private readonly SkUiTween _focus;
    private readonly SkUiStepperGestures _gestures;
    private IDisposable? _repeat;
    private int _held;

    public SkUiStepperModel(ISkUiStepperOwner owner)
    {
        _owner = owner;
        _decrementPress = new SkUiTween(owner);
        _incrementPress = new SkUiTween(owner);
        _focus = new SkUiTween(owner);
        Field = new SkUiTextFieldModel(owner)
        {
            DrawsChrome = false,
            HorizontalTextAlignment = TextAlignment.Center,
            Keyboard = Keyboard.Numeric,
            Padding = new Thickness(4, 0)
        };
        _gestures = new SkUiStepperGestures(this);
    }

    /// <summary>The value's text field (shown between the buttons with <see cref="IsValueVisible"/>).</summary>
    public SkUiTextFieldModel Field { get; }

    /// <summary>The recognizers of the buttons and of the value field (the controls add them).</summary>
    internal void CollectGestureRecognizers(List<SkUiGestureRecognizer> recognizers)
    {
        recognizers.Add(_gestures);
        if (IsValueVisible)
            recognizers.Add(Field.Gestures);
    }

    #region State the controls copy in

    public bool IsEnabled = true;
    public bool IsPointerOver;
    public Color? TextColor;
    public string? ValueFormat;

    /// <summary>Whether the value shows between the buttons, editable (SkiaUi; MAUI's stepper has buttons only).</summary>
    public bool IsValueVisible { get; private set; }

    /// <summary>Shows or hides the value field; returns whether it changed.</summary>
    public bool SetValueVisible(bool visible)
    {
        if (IsValueVisible == visible)
            return false;
        IsValueVisible = visible;
        UpdateText();
        return true;
    }

    #endregion

    #region Value

    /// <summary>The value as the field shows it (<see cref="ValueFormat"/>, the current culture).</summary>
    internal string FormatValue(double value) => value.ToString(string.IsNullOrEmpty(ValueFormat) ? "G" : ValueFormat, CultureInfo.CurrentCulture);

    /// <summary>The control's value changed: the field shows it (unless the user is typing in it).</summary>
    public void ValueChanged()
    {
        if (!IsValueVisible || (_owner.IsFocused && _editing))
        {
            _owner.InvalidatePaint();
            _owner.InvalidateSemantics();
            return;
        }
        UpdateText();
    }

    // Whether the field holds text the user typed and has not committed.
    private bool _editing;

    private void UpdateText()
    {
        _editing = false;
        Field.SetText(IsValueVisible ? FormatValue(_owner.Value) : string.Empty);
        _owner.InvalidateMeasure();
        _owner.InvalidatePaint();
        _owner.InvalidateSemantics();
    }

    /// <summary>The field's text changed by the user (not yet committed).</summary>
    public void FieldEdited() => _editing = true;

    /// <summary>
    /// Commits what the user typed (Return, focus leaving): a number sets the value (coerced to the range), anything else is
    /// dropped; the field shows the value either way.
    /// </summary>
    public void Commit()
    {
        if (!_editing)
            return;
        _editing = false;
        var text = Field.Editor.Text.Trim();
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var typed) && double.IsFinite(typed))
            _owner.SetValueFromUser(typed);
        UpdateText();
    }

    private bool CanStep(int direction) => direction < 0 ? _owner.Value > _owner.Minimum : _owner.Value < _owner.Maximum;

    /// <summary>One step down (-1) or up (+1), if the range allows; returns whether the value moved.</summary>
    public bool Step(int direction)
    {
        if (!IsEnabled || !CanStep(direction))
            return false;
        Commit();
        _owner.SetValueFromUser(_owner.Value + direction * _owner.Increment);
        return true;
    }

    #endregion

    #region Layout and drawing

    private static SKRect Mirror(SKRect rect, float width, bool rightToLeft) =>
        rightToLeft ? new SKRect(width - rect.Right, rect.Top, width - rect.Left, rect.Bottom) : rect;

    /// <summary>The −, value and + rectangles in a stepper of the given size (− at the start side).</summary>
    internal (SKRect Decrement, SKRect Value, SKRect Increment) Parts(float width, float height)
    {
        var button = SkUiLook.Current.StepperButtonSize;
        var buttonWidth = (float)Math.Min(button.Width, width / 2);
        var decrement = new SKRect(0, 0, buttonWidth, height);
        var increment = new SKRect(width - buttonWidth, 0, width, height);
        var value = IsValueVisible ? new SKRect(buttonWidth, 0, width - buttonWidth, height) : SKRect.Empty;
        var rightToLeft = _owner.IsRightToLeft;
        return (Mirror(decrement, width, rightToLeft), IsValueVisible ? Mirror(value, width, rightToLeft) : value, Mirror(increment, width, rightToLeft));
    }

    /// <summary>The stepper's size: two buttons (the look's), and the value's width between them when it shows.</summary>
    public Size Measure(double widthConstraint, double heightConstraint)
    {
        var look = SkUiLook.Current;
        var button = look.StepperButtonSize;
        var width = button.Width * 2;
        var height = button.Height;
        if (IsValueVisible)
        {
            Field.FieldArea = null;
            var value = Field.Measure(double.PositiveInfinity, heightConstraint);
            // The widest of the range's ends, so the stepper keeps its width as the value changes.
            width += Math.Max(look.StepperValueMinimumWidth, value.Width);
        }
        return new Size(Math.Min(widthConstraint, width), Math.Min(heightConstraint, height));
    }

    /// <summary>Draws the look's stepper, then the value field between the buttons.</summary>
    public void Draw(SKCanvas canvas, float width, float height)
    {
        var (decrement, value, increment) = Parts(width, height);
        var foreground = SkUiToggleDrawing.ToSkColor(TextColor ?? SkUiColors.DefaultForeground);
        SkUiLook.Current.DrawStepper(canvas, new SkUiStepperPaint(new SKRect(0, 0, width, height), decrement, increment, value,
            SkUiToggleDrawing.ToSkColor(SkUiColors.Accent), SkUiToggleDrawing.ToSkColor(SkUiColors.Muted), foreground,
            _decrementPress.Value, _incrementPress.Value, CanStep(-1), CanStep(1), _focus.Value, IsPointerOver, IsEnabled, _owner.IsRightToLeft));
        if (!IsValueVisible)
            return;
        Field.FieldArea = value;
        Field.Draw(canvas, width, height);
    }

    /// <summary>The value field's caret while it is focused (composite-time).</summary>
    public Rendering.SkUiRenderDrawable? GetCaretDrawable(float width, float height)
    {
        if (!IsValueVisible)
            return null;
        Field.FieldArea = Parts(width, height).Value;
        return Field.GetCaretDrawable(width, height);
    }

    #endregion

    #region Input

    /// <summary>The stepper gained or lost keyboard focus; losing it commits what was typed.</summary>
    public void SetFocused(bool focused)
    {
        _focus.AnimateTo(focused ? 1f : 0f, SkUiLook.Current.GetTransition(SkUiTransitionKind.FieldFocus));
        if (IsValueVisible)
            Field.SetFocused(focused);
        if (!focused)
        {
            Commit();
            Release();
        }
        _owner.InvalidatePaint();
    }

    /// <summary>A button pressed (-1: −, +1: +): it steps, then repeats while held.</summary>
    internal void Press(int button)
    {
        if (!IsEnabled)
            return;
        _held = button;
        (button < 0 ? _decrementPress : _incrementPress).AnimateTo(1, SkUiLook.Current.GetTransition(SkUiTransitionKind.Press));
        Step(button);
        _repeat?.Dispose();
        _repeat = SkUiGestureSettings.Schedule(SkUiLook.Current.StepperRepeatDelay, Repeat);
    }

    private void Repeat()
    {
        _repeat = null;
        if (_held == 0 || !Step(_held))
            return;
        _repeat = SkUiGestureSettings.Schedule(SkUiLook.Current.StepperRepeatInterval, Repeat);
    }

    /// <summary>The held button was released (or the press cancelled).</summary>
    internal void Release()
    {
        _repeat?.Dispose();
        _repeat = null;
        if (_held == 0)
            return;
        (_held < 0 ? _decrementPress : _incrementPress).AnimateTo(0, SkUiLook.Current.GetTransition(SkUiTransitionKind.Release));
        _held = 0;
    }

    /// <summary>Which button <paramref name="local"/> is on: -1 (−), +1 (+), 0 (neither: the value field, or outside).</summary>
    internal int ButtonAt(Point local)
    {
        var size = _owner.FieldSize;
        var (decrement, _, increment) = Parts((float)size.Width, (float)size.Height);
        var (x, y) = ((float)local.X, (float)local.Y);
        return decrement.Contains(x, y) ? -1 : increment.Contains(x, y) ? 1 : 0;
    }

    /// <summary>
    /// Keys: Up / Down step (also while the value is edited; Return commits it). Without the value, Left / Right, Home / End and
    /// Page Up / Down reach the focus manager's range keys (stepping, the ends).
    /// </summary>
    public bool OnKeyDown(in SkUiKeyEvent key)
    {
        if (!IsEnabled)
            return false;
        if (key.Key is SkUiKey.Up or SkUiKey.Down && !key.HasCommandModifier)
        {
            Step(key.Key == SkUiKey.Up ? 1 : -1);
            return true;
        }
        if (!IsValueVisible)
            return false;
        if (key.Key == SkUiKey.Enter)
        {
            Commit();
            return true;
        }
        return Field.OnKeyDown(key);
    }

    /// <summary>An adjustable value for screen readers: the range, increment and decrement.</summary>
    public void PopulateSemantics(SkUiSemanticsInfo info)
    {
        info.Role = SkUiSemanticsRole.Slider;
        info.Range = new SkUiSemanticsRange(_owner.Minimum, _owner.Maximum, _owner.Value);
        info.Value = FormatValue(_owner.Value);
        info.IsEnabled &= IsEnabled;
        if (IsEnabled)
            info.Actions |= SkUiSemanticsActions.Increment | SkUiSemanticsActions.Decrement;
    }

    /// <summary>A screen reader's increment / decrement, or setting the value.</summary>
    public bool PerformSemanticsAction(SkUiSemanticsActions action) => action switch
    {
        SkUiSemanticsActions.Increment => Step(1),
        SkUiSemanticsActions.Decrement => Step(-1),
        _ => false
    };

    /// <summary>The drawing resources go (the stepper left its surface).</summary>
    public void ReleaseDrawingResources()
    {
        Field.ReleaseDrawingResources();
        Release();
    }

    #endregion
}

/// <summary>
/// A stepper's buttons: a mouse press steps at once and repeats while held; a touch steps when lifted (a drag scrolls the
/// stepper's scroller instead), or repeats once held past the long-press time.
/// </summary>
internal sealed class SkUiStepperGestures(SkUiStepperModel model) : SkUiGestureRecognizer
{
    private long? _pointer;
    private int _button;
    private bool _mouse;
    private bool _holding;
    private IDisposable? _hold;

    /// <inheritdoc />
    protected internal override bool IsExclusive => _mouse || _holding;

    /// <inheritdoc />
    protected internal override bool OnPointerPressed(SkUiPointer pointer)
    {
        if (_pointer is not null || Owner is not { } owner)
            return false;
        var button = model.ButtonAt(pointer.GetPosition(owner));
        if (button == 0)
            return false;
        _pointer = pointer.Id;
        _button = button;
        _mouse = pointer.Device != SkUiPointerDevice.Touch;
        _holding = false;
        if (_mouse)
        {
            Claim();
            model.Press(button);
            return true;
        }
        var id = pointer.Id;
        _hold = SkUiGestureSettings.Schedule(SkUiGestureSettings.LongPressDuration, () =>
        {
            if (_pointer != id)
                return;
            _holding = true;
            Claim();
            model.Press(_button);
        });
        return true;
    }

    /// <inheritdoc />
    protected internal override void OnPointerMoved(SkUiPointer pointer)
    {
        if (_pointer != pointer.Id || Owner is not { } owner)
            return;
        // Off the button: it stops repeating (and a touch no longer steps when lifted).
        if (model.ButtonAt(pointer.GetPosition(owner)) != _button)
        {
            model.Release();
            if (!_mouse && !_holding)
            {
                _hold?.Dispose();
                Resign(pointer.Id);
                _pointer = null;
            }
        }
    }

    /// <inheritdoc />
    protected internal override void OnPointerReleased(SkUiPointer pointer)
    {
        if (_pointer != pointer.Id || Owner is not { } owner)
            return;
        _pointer = null;
        _hold?.Dispose();
        _hold = null;
        if (_mouse || _holding)
        {
            model.Release();
            return;
        }
        if (model.ButtonAt(pointer.GetPosition(owner)) == _button)
        {
            Claim();
            model.Press(_button);
            model.Release();
        }
    }

    /// <inheritdoc />
    protected internal override void OnRejected(long pointerId)
    {
        if (_pointer != pointerId)
            return;
        _pointer = null;
        _hold?.Dispose();
        _hold = null;
        model.Release();
    }
}
