using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>What a text field's step buttons and keys (<see cref="SkUiTextFieldSteps"/>) need from the control: a numeric entry's value.</summary>
internal interface ISkUiTextFieldStepOwner
{
    /// <summary>Whether the value can move down (-1) or up (+1) now.</summary>
    bool CanStep(int direction);

    /// <summary>
    /// One step down (-1) or up (+1) by the user (<paramref name="large"/>: Page Up / Page Down's step): the typed text is committed
    /// first. Returns whether the value moved (a held button stops repeating when it does not).
    /// </summary>
    bool Step(int direction, bool large);

    /// <summary>Escape: the text goes back to the last committed value; returns whether it did anything.</summary>
    bool Revert();
}

/// <summary>
/// A text field's − and + buttons, keys and wheel (a numeric entry's, D16): look-drawn parts of the field model's row
/// (<see cref="SkUiTextFieldModel"/>) placed by <see cref="Placement"/> and <see cref="Orientation"/>, texts of the control's own in
/// place of the glyphs, held buttons repeating (<see cref="SkUiStepperGestures"/>, as the stepper's). A button never focuses the
/// field: an unfocused field steps without showing the keyboard. Up / Down and Page Up / Page Down step, Escape reverts, the wheel
/// steps while the field is focused, and screen readers get increment / decrement. The value is the control's
/// (<see cref="ISkUiTextFieldStepOwner"/>).
/// </summary>
internal sealed class SkUiTextFieldSteps : ISkUiStepButtons
{
    // Around a button's text: it never touches the button's edges.
    private static readonly Thickness TextPadding = new(6, 2);

    // Wheel distance (DIPs) for one step: a trackpad's small deltas add up, a wheel's notch steps at once.
    private const double WheelStep = 20;

    private readonly SkUiTextFieldModel _field;
    private readonly ISkUiTextFieldStepOwner _owner;
    private readonly SkUiTween _decrementPress;
    private readonly SkUiTween _incrementPress;
    private readonly SkUiStepRepeat _repeat = new();
    private SkUiTextLayout? _decrementLayout;
    private SkUiTextLayout? _incrementLayout;
    // A button's size for the look it was measured with: a text's size follows the field's font, the rest the look.
    private Size? _buttonSize;
    private SkUiLook? _buttonSizeLook;
    private double _wheel;

    public SkUiTextFieldSteps(SkUiTextFieldModel field, ISkUiTextFieldStepOwner owner, ISkUiTransitionHost host)
    {
        _field = field;
        _owner = owner;
        _decrementPress = new SkUiTween(host);
        _incrementPress = new SkUiTween(host);
        Gestures = new SkUiStepperGestures(this);
    }

    /// <summary>The buttons' recognizer (the controls add it next to the field's).</summary>
    public SkUiGestureRecognizer Gestures { get; }

    #region State the controls copy in

    /// <summary>Where the buttons sit; <see cref="SkUiStepButtonPlacement.None"/>: none (keys and the wheel still step).</summary>
    public SkUiStepButtonPlacement Placement;

    /// <summary>Side by side or stacked.</summary>
    public StackOrientation Orientation = StackOrientation.Horizontal;

    /// <summary>The − button's text; <c>null</c> or empty: the look's −.</summary>
    public string? DecrementText;

    /// <summary>The + button's text; <c>null</c> or empty: the look's +.</summary>
    public string? IncrementText;

    /// <summary>The texts' font family; <c>null</c>: the field's.</summary>
    public string? FontFamily;

    /// <summary>The glyphs' and texts' color; <c>null</c>: the field's text color.</summary>
    public Color? ButtonTextColor;

    #endregion

    /// <summary>Whether the field shows buttons.</summary>
    public bool HasButtons => Placement != SkUiStepButtonPlacement.None;

    private bool IsVertical => Orientation == StackOrientation.Vertical;

    /// <summary>A text, the buttons' font or the placement changed: the field (and its buttons) are measured and drawn again.</summary>
    public void Changed() => _field.TextChanged();

    /// <summary>The field's text shape changed (its font, size, direction): the buttons' texts are laid out and measured again.</summary>
    public void InvalidateLayout()
    {
        _decrementLayout?.Invalidate();
        _incrementLayout?.Invalidate();
        _buttonSize = null;
    }

    /// <summary>The value or the range changed: the buttons' states and the screen reader's actions follow.</summary>
    public void ValueChanged()
    {
        if (HasButtons)
            _field.PaintChanged();
        _field.SemanticsChanged();
    }

    #region Geometry

    private SkUiTextStyle TextStyle => _field.ButtonTextStyle(FontFamily);

    private SkUiTextLayout? Layout(int button)
    {
        var text = button < 0 ? DecrementText : IncrementText;
        if (string.IsNullOrEmpty(text))
            return null;
        return button < 0 ? _decrementLayout ??= new SkUiTextLayout(_field) : _incrementLayout ??= new SkUiTextLayout(_field);
    }

    /// <summary>One button's size: the look's, or a text's that needs more (with its padding).</summary>
    private Size ButtonSize
    {
        get
        {
            var look = SkUiLook.Current;
            if (_buttonSize is { } size && ReferenceEquals(_buttonSizeLook, look))
                return size;
            size = look.TextFieldStepButtonSize;
            if (Layout(-1) is not null || Layout(1) is not null)
            {
                var style = TextStyle;
                foreach (var direction in (ReadOnlySpan<int>)[-1, 1])
                {
                    if (Layout(direction) is not { } layout)
                        continue;
                    var text = layout.Measure(direction < 0 ? DecrementText! : IncrementText!, style, TextPadding, double.PositiveInfinity);
                    size = new Size(Math.Max(size.Width, Math.Ceiling(text.Width)), Math.Max(size.Height, Math.Ceiling(text.Height)));
                }
            }
            _buttonSize = size;
            _buttonSizeLook = look;
            return size;
        }
    }

    /// <summary>
    /// The room the buttons take in the field: widths at its start and end sides (beside the text, the whole field's height), heights
    /// above and below the text (split and stacked: bars across the field).
    /// </summary>
    internal (float Start, float End, float Top, float Bottom) Room
    {
        get
        {
            if (!HasButtons)
                return default;
            var size = ButtonSize;
            var width = (float)size.Width;
            var height = (float)size.Height;
            return (Placement, IsVertical) switch
            {
                (SkUiStepButtonPlacement.Split, true) => (0, 0, height, height),
                (SkUiStepButtonPlacement.Split, false) => (width, width, 0, 0),
                (SkUiStepButtonPlacement.Start, _) => (IsVertical ? width : 2 * width, 0, 0, 0),
                _ => (0, IsVertical ? width : 2 * width, 0, 0)
            };
        }
    }

    /// <summary>Which button <paramref name="local"/> (node coordinates) is on: -1 (−), +1 (+), 0 (none).</summary>
    public int ButtonAt(Point local)
    {
        if (!HasButtons)
            return 0;
        var (decrement, increment) = _field.StepButtonBounds();
        var (x, y) = ((float)local.X, (float)local.Y);
        return decrement.Contains(x, y) ? -1 : increment.Contains(x, y) ? 1 : 0;
    }

    #endregion

    #region Drawing

    /// <summary>Draws the look's buttons in <paramref name="decrement"/> and <paramref name="increment"/>, then the buttons' texts.</summary>
    public void Draw(SKCanvas canvas, SKRect area, SKRect decrement, SKRect increment, SKColor border, SKColor textColor, bool enabled, bool rightToLeft)
    {
        if (!HasButtons)
            return;
        var foreground = ButtonTextColor is { } color ? SkUiToggleDrawing.ToSkColor(color) : textColor;
        var decrementText = Layout(-1) is not null;
        var incrementText = Layout(1) is not null;
        var canDecrement = _owner.CanStep(-1);
        var canIncrement = _owner.CanStep(1);
        SkUiLook.Current.DrawTextFieldStepButtons(canvas, new SkUiTextFieldStepButtonsPaint
        {
            Field = area,
            DecrementBounds = decrement,
            IncrementBounds = increment,
            Placement = Placement,
            Orientation = Orientation,
            Border = border,
            Foreground = foreground,
            DecrementPressed = _decrementPress.Value,
            IncrementPressed = _incrementPress.Value,
            CanDecrement = canDecrement,
            CanIncrement = canIncrement,
            IsEnabled = enabled,
            IsRightToLeft = rightToLeft,
            HasDecrementText = decrementText,
            HasIncrementText = incrementText
        });
        if (!decrementText && !incrementText)
            return;
        var style = TextStyle;
        DrawText(canvas, -1, decrement, style, foreground, enabled && canDecrement);
        DrawText(canvas, 1, increment, style, foreground, enabled && canIncrement);
    }

    private void DrawText(SKCanvas canvas, int button, SKRect rect, in SkUiTextStyle style, SKColor foreground, bool active)
    {
        if (Layout(button) is not { } layout || rect.Width <= 0 || rect.Height <= 0)
            return;
        var paint = SkUiTextResources.TextPaint;
        // Dimmed as the look's glyph when the button cannot step.
        paint.Color = foreground.WithAlpha((byte)(foreground.Alpha * (active ? 1f : SkUiLook.StepperDimmedOpacity)));
        var save = canvas.Save();
        canvas.Translate(rect.Left, rect.Top);
        layout.Draw(canvas, button < 0 ? DecrementText! : IncrementText!, style, TextPadding, rect.Width, rect.Height, TextAlignment.Center,
            TextAlignment.Center, paint);
        canvas.RestoreToCount(save);
    }

    #endregion

    #region Input

    /// <summary>A button pressed (-1: −, +1: +): it steps (without focusing the field), then repeats while held.</summary>
    public void Press(int button)
    {
        if (!_field.IsEnabled)
            return;
        (button < 0 ? _decrementPress : _incrementPress).AnimateTo(1, SkUiLook.Current.GetTransition(SkUiTransitionKind.Press));
        _repeat.Start(button, direction => _owner.Step(direction, large: false));
    }

    /// <summary>The held button was released (or the press cancelled).</summary>
    public void Release()
    {
        var held = _repeat.Held;
        if (!_repeat.Stop())
            return;
        (held < 0 ? _decrementPress : _incrementPress).AnimateTo(0, SkUiLook.Current.GetTransition(SkUiTransitionKind.Release));
    }

    /// <summary>Keys while the field has focus: Up / Down step, Page Up / Page Down step by the large step, Escape reverts.</summary>
    public bool OnKeyDown(in SkUiKeyEvent key)
    {
        if (key.HasCommandModifier)
            return false;
        switch (key.Key)
        {
            case SkUiKey.Up or SkUiKey.Down:
                _owner.Step(key.Key == SkUiKey.Up ? 1 : -1, large: false);
                return true;
            case SkUiKey.PageUp or SkUiKey.PageDown:
                _owner.Step(key.Key == SkUiKey.PageUp ? 1 : -1, large: true);
                return true;
            case SkUiKey.Escape:
                return _owner.Revert();
        }
        return false;
    }

    /// <summary>
    /// A wheel or trackpad delta while the field is focused (positive: towards the start, up): steps up or down once per
    /// <see cref="WheelStep"/> DIPs; returns what it did not use (all of it while unfocused, so the page scrolls).
    /// </summary>
    public Point Wheel(double deltaX, double deltaY, bool focused)
    {
        if (!focused || deltaY == 0)
        {
            _wheel = 0;
            return new Point(deltaX, deltaY);
        }
        // A turn back starts counting again.
        if (_wheel != 0 && Math.Sign(_wheel) != Math.Sign(deltaY))
            _wheel = 0;
        _wheel += deltaY;
        while (Math.Abs(_wheel) >= WheelStep)
        {
            var direction = Math.Sign(_wheel);
            _wheel -= direction * WheelStep;
            _owner.Step(direction, large: false);
        }
        return new Point(deltaX, 0);
    }

    /// <summary>A screen reader's increment and decrement, while the value can move.</summary>
    public void PopulateSemantics(SkUiSemanticsInfo info)
    {
        if (!_field.IsEnabled)
            return;
        if (_owner.CanStep(1))
            info.Actions |= SkUiSemanticsActions.Increment;
        if (_owner.CanStep(-1))
            info.Actions |= SkUiSemanticsActions.Decrement;
    }

    /// <summary>A screen reader's increment / decrement.</summary>
    public bool PerformSemanticsAction(SkUiSemanticsActions action) => action switch
    {
        SkUiSemanticsActions.Increment => _field.IsEnabled && _owner.Step(1, large: false),
        SkUiSemanticsActions.Decrement => _field.IsEnabled && _owner.Step(-1, large: false),
        _ => false
    };

    /// <summary>The field left its surface or was disabled: a held button stops and the texts' blobs go.</summary>
    public void ReleaseDrawingResources()
    {
        Release();
        _decrementLayout?.Release();
        _incrementLayout?.Release();
    }

    #endregion
}
