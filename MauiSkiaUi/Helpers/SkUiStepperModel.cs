using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>What the shared stepper engine (<see cref="SkUiStepperModel"/>) needs from its control, on either layer.</summary>
internal interface ISkUiStepperOwner : ISkUiTransitionHost
{
    /// <summary>Whether the value can move down (-1) or up (+1) within the range.</summary>
    bool CanStep(int direction);

    /// <summary>One step down (-1) or up (+1), by the user: the control sets its <c>Value</c> (coerced to the range).</summary>
    void Step(int direction);

    /// <summary>The range and the value for screen readers.</summary>
    SkUiSemanticsRange SemanticsRange { get; }

    /// <summary>The value as screen readers read it (current culture).</summary>
    string ValueText { get; }

    /// <summary>The stepper's arranged size.</summary>
    Size StepperSize { get; }

    /// <summary>Whether the stepper lays out right to left (+ on the left when horizontal).</summary>
    bool IsRightToLeft { get; }

    void InvalidateMeasure();
    void InvalidatePaint();
    void InvalidateSemantics();
}

/// <summary>
/// The engine every stepper shares (<see cref="SkUiStepperBase"/>, <see cref="Core.SkUiCoreStepperBase"/>: on <see cref="double"/> as
/// MAUI's, or typed): the value stays the control's, asked through <see cref="ISkUiStepperOwner"/>. The − and + buttons drawn by the
/// look side by side or stacked (<see cref="Orientation"/>; pressed, disabled at the range's ends, focused), or texts of the control's
/// own on them (<see cref="DecrementText"/>, <see cref="IncrementText"/>: words or an icon font's symbols), the shape's corner radius and
/// outline (<see cref="CornerRadius"/>, <see cref="BorderWidth"/>, <see cref="BorderColor"/>), taps and held buttons repeating, keys and
/// screen readers.
/// </summary>
internal sealed class SkUiStepperModel : ISkUiStepButtons
{
    private readonly ISkUiStepperOwner _owner;
    private readonly SkUiTween _decrementPress;
    private readonly SkUiTween _incrementPress;
    private readonly SkUiTween _focus;
    private readonly SkUiStepperGestures _gestures;
    private readonly SkUiStepRepeat _repeat = new();
    // The buttons' texts, laid out on the shared text engine; made the first time a text is set.
    private SkUiTextLayout? _decrementLayout;
    private SkUiTextLayout? _incrementLayout;

    public SkUiStepperModel(ISkUiStepperOwner owner)
    {
        _owner = owner;
        _decrementPress = new SkUiTween(owner);
        _incrementPress = new SkUiTween(owner);
        _focus = new SkUiTween(owner);
        _gestures = new SkUiStepperGestures(this);
    }

    /// <summary>The recognizer of the buttons (the controls add it).</summary>
    internal void CollectGestureRecognizers(List<SkUiGestureRecognizer> recognizers) => recognizers.Add(_gestures);

    #region State the controls copy in

    public bool IsEnabled = true;
    public bool IsPointerOver;
    public Color? TextColor;

    /// <summary>Whether the buttons sit side by side (− at the start side) or stacked (+ on top).</summary>
    public StackOrientation Orientation = StackOrientation.Horizontal;

    /// <summary>The − button's text; <c>null</c> or empty: the look draws its −.</summary>
    public string? DecrementText;

    /// <summary>The + button's text; <c>null</c> or empty: the look draws its +.</summary>
    public string? IncrementText;

    public string? FontFamily;
    public double FontSize = DefaultFontSize;
    public FontAttributes FontAttributes;
    public bool FontAutoScalingEnabled = true;

    internal const double DefaultFontSize = 16;

    /// <summary>The corner radius in DIPs; <c>null</c>: the look's (<see cref="SkUiLook.DefaultStepperCornerRadius"/>).</summary>
    public double? CornerRadius;

    /// <summary>The outline's width in DIPs; 0: no outline.</summary>
    public double BorderWidth;

    /// <summary>The outline's color; <c>null</c>: the color scheme's muted color.</summary>
    public Color? BorderColor;

    private bool IsVertical => Orientation == StackOrientation.Vertical;

    #endregion

    #region Value

    /// <summary>The control's value changed: the buttons' enabled states and the screen reader's value follow.</summary>
    public void ValueChanged()
    {
        _owner.InvalidatePaint();
        _owner.InvalidateSemantics();
    }

    private bool CanStep(int direction) => _owner.CanStep(direction);

    /// <summary>One step down (-1) or up (+1), if the range allows; returns whether the value moved.</summary>
    public bool Step(int direction)
    {
        if (!IsEnabled || !CanStep(direction))
            return false;
        _owner.Step(direction);
        return true;
    }

    #endregion

    #region Texts

    // Around a button's text: it never touches the button's edges.
    private static readonly Thickness TextPadding = new(8, 4);

    private SkUiTextStyle TextStyle => new(SkUiTypefaces.Resolve(FontFamily, FontAttributes), SkUiFontScaling.ScaleFontSize(FontSize, FontAutoScalingEnabled),
        LineBreakMode.TailTruncation, null, 1, -1, 0, _owner.IsRightToLeft ? SkUiTextDirection.RightToLeft : SkUiTextDirection.Auto,
        SkUiTextRendering.Default, FontAttributes);

    // A text keeps clear of the outline too.
    private Thickness TextInset => new(TextPadding.Left + BorderWidth, TextPadding.Top + BorderWidth, TextPadding.Right + BorderWidth,
        TextPadding.Bottom + BorderWidth);

    /// <summary>A text, the font or the outline's width changed: the buttons are measured and drawn again.</summary>
    public void TextChanged()
    {
        _decrementLayout?.Invalidate();
        _incrementLayout?.Invalidate();
        _owner.InvalidateMeasure();
        _owner.InvalidatePaint();
    }

    private SkUiTextLayout? Layout(int button)
    {
        var text = button < 0 ? DecrementText : IncrementText;
        if (string.IsNullOrEmpty(text))
            return null;
        return button < 0 ? _decrementLayout ??= new SkUiTextLayout(_owner) : _incrementLayout ??= new SkUiTextLayout(_owner);
    }

    private void DrawText(SKCanvas canvas, int button, SKRect rect, in SkUiTextStyle style, SKColor foreground)
    {
        if (Layout(button) is not { } layout || rect.Width <= 0 || rect.Height <= 0)
            return;
        var paint = SkUiTextResources.TextPaint;
        // Dimmed as the look's glyph when the button cannot step.
        paint.Color = foreground.WithAlpha((byte)(foreground.Alpha * (IsEnabled && CanStep(button) ? 1f : SkUiLook.StepperDimmedOpacity)));
        var save = canvas.Save();
        canvas.Translate(rect.Left, rect.Top);
        layout.Draw(canvas, button < 0 ? DecrementText! : IncrementText!, style, TextInset, rect.Width, rect.Height, TextAlignment.Center,
            TextAlignment.Center, paint);
        canvas.RestoreToCount(save);
    }

    #endregion

    #region Layout and drawing

    /// <summary>
    /// The − and + rectangles in a stepper of the given size, each half of it: side by side with − at the start side, or stacked with +
    /// on top.
    /// </summary>
    internal (SKRect Decrement, SKRect Increment) Parts(float width, float height)
    {
        if (IsVertical)
        {
            var middle = height / 2;
            return (new SKRect(0, middle, width, height), new SKRect(0, 0, width, middle));
        }
        var center = width / 2;
        var start = new SKRect(0, 0, center, height);
        var end = new SKRect(center, 0, width, height);
        return _owner.IsRightToLeft ? (end, start) : (start, end);
    }

    /// <summary>
    /// The stepper's size: two buttons, side by side or stacked, each the look's size or, when a text needs more, its padded text's
    /// (clear of the outline).
    /// </summary>
    public Size Measure(double widthConstraint, double heightConstraint)
    {
        var button = SkUiLook.Current.StepperButtonSize;
        if (Layout(-1) is not null || Layout(1) is not null)
        {
            var style = TextStyle;
            var inset = TextInset;
            foreach (var direction in (ReadOnlySpan<int>)[-1, 1])
            {
                if (Layout(direction) is not { } layout)
                    continue;
                var text = layout.Measure(direction < 0 ? DecrementText! : IncrementText!, style, inset, double.PositiveInfinity);
                button = new Size(Math.Max(button.Width, Math.Ceiling(text.Width)), Math.Max(button.Height, Math.Ceiling(text.Height)));
            }
        }
        var size = IsVertical ? new Size(button.Width, button.Height * 2) : new Size(button.Width * 2, button.Height);
        return new Size(Math.Min(widthConstraint, size.Width), Math.Min(heightConstraint, size.Height));
    }

    /// <summary>Draws the look's stepper, then the buttons' texts (the look leaves out the glyphs they replace).</summary>
    public void Draw(SKCanvas canvas, float width, float height)
    {
        var (decrement, increment) = Parts(width, height);
        var foreground = SkUiToggleDrawing.ToSkColor(TextColor ?? SkUiColors.DefaultForeground);
        var decrementText = Layout(-1) is not null;
        var incrementText = Layout(1) is not null;
        SkUiLook.Current.DrawStepper(canvas, new SkUiStepperPaint
        {
            Bounds = new SKRect(0, 0, width, height),
            DecrementBounds = decrement,
            IncrementBounds = increment,
            Orientation = Orientation,
            Accent = SkUiToggleDrawing.ToSkColor(SkUiColors.Accent),
            Border = SkUiToggleDrawing.ToSkColor(SkUiColors.Muted),
            Foreground = foreground,
            DecrementPressed = _decrementPress.Value,
            IncrementPressed = _incrementPress.Value,
            CanDecrement = CanStep(-1),
            CanIncrement = CanStep(1),
            Focused = _focus.Value,
            IsPointerOver = IsPointerOver,
            IsEnabled = IsEnabled,
            IsRightToLeft = _owner.IsRightToLeft,
            HasDecrementText = decrementText,
            HasIncrementText = incrementText,
            CornerRadius = (float)(CornerRadius ?? SkUiLook.Current.DefaultStepperCornerRadius),
            BorderWidth = (float)BorderWidth,
            BorderColor = SkUiToggleDrawing.ToSkColor(BorderColor ?? SkUiColors.Muted)
        });
        if (!decrementText && !incrementText)
            return;
        var style = TextStyle;
        DrawText(canvas, -1, decrement, style, foreground);
        DrawText(canvas, 1, increment, style, foreground);
    }

    #endregion

    #region Input

    /// <summary>The stepper gained or lost keyboard focus; losing it stops a held button.</summary>
    public void SetFocused(bool focused)
    {
        _focus.AnimateTo(focused ? 1f : 0f, SkUiLook.Current.GetTransition(SkUiTransitionKind.FieldFocus));
        if (!focused)
            Release();
        _owner.InvalidatePaint();
    }

    /// <summary>A button pressed (-1: −, +1: +): it steps, then repeats while held.</summary>
    public void Press(int button)
    {
        if (!IsEnabled)
            return;
        (button < 0 ? _decrementPress : _incrementPress).AnimateTo(1, SkUiLook.Current.GetTransition(SkUiTransitionKind.Press));
        _repeat.Start(button, Step);
    }

    /// <summary>The held button was released (or the press cancelled).</summary>
    public void Release()
    {
        var held = _repeat.Held;
        if (!_repeat.Stop())
            return;
        (held < 0 ? _decrementPress : _incrementPress).AnimateTo(0, SkUiLook.Current.GetTransition(SkUiTransitionKind.Release));
    }

    /// <summary>Which button <paramref name="local"/> is on: -1 (−), +1 (+), 0 (outside).</summary>
    public int ButtonAt(Point local)
    {
        var size = _owner.StepperSize;
        var (decrement, increment) = Parts((float)size.Width, (float)size.Height);
        var (x, y) = ((float)local.X, (float)local.Y);
        return decrement.Contains(x, y) ? -1 : increment.Contains(x, y) ? 1 : 0;
    }

    /// <summary>
    /// Keys: Up / Down step; Left / Right, Home / End and Page Up / Down reach the focus manager's range keys (stepping, the ends).
    /// </summary>
    public bool OnKeyDown(in SkUiKeyEvent key)
    {
        if (!IsEnabled || key.Key is not (SkUiKey.Up or SkUiKey.Down) || key.HasCommandModifier)
            return false;
        Step(key.Key == SkUiKey.Up ? 1 : -1);
        return true;
    }

    /// <summary>An adjustable value for screen readers: the range, increment and decrement.</summary>
    public void PopulateSemantics(SkUiSemanticsInfo info)
    {
        info.Role = SkUiSemanticsRole.Slider;
        info.Range = _owner.SemanticsRange;
        info.Value = _owner.ValueText;
        info.IsHorizontal = !IsVertical;
        info.IsEnabled &= IsEnabled;
        if (IsEnabled)
            info.Actions |= SkUiSemanticsActions.Increment | SkUiSemanticsActions.Decrement;
    }

    /// <summary>A screen reader's increment / decrement.</summary>
    public bool PerformSemanticsAction(SkUiSemanticsActions action) => action switch
    {
        SkUiSemanticsActions.Increment => Step(1),
        SkUiSemanticsActions.Decrement => Step(-1),
        _ => false
    };

    /// <summary>The stepper left its surface: a held button stops and the texts' blobs go.</summary>
    public void ReleaseDrawingResources()
    {
        Release();
        _decrementLayout?.Release();
        _incrementLayout?.Release();
    }

    #endregion
}

/// <summary>
/// Step buttons' pointer input (a stepper's, a numeric entry's): a mouse press steps at once and repeats while held; a touch steps
/// when lifted (a drag scrolls the buttons' scroller instead), or repeats once held past the long-press time.
/// </summary>
internal sealed class SkUiStepperGestures(ISkUiStepButtons model) : SkUiGestureRecognizer
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

/// <summary>Step buttons a <see cref="SkUiStepperGestures"/> drives: a stepper's, a numeric entry's.</summary>
internal interface ISkUiStepButtons
{
    /// <summary>Which button <paramref name="local"/> (node coordinates) is on: -1 (−), +1 (+), 0 (none).</summary>
    int ButtonAt(Point local);

    /// <summary>A button pressed: it steps, then repeats while held.</summary>
    void Press(int button);

    /// <summary>The held button was released (or the press cancelled).</summary>
    void Release();
}

/// <summary>
/// A held step button repeating: one step at once, then after the look's <see cref="SkUiLook.StepperRepeatDelay"/> one every
/// <see cref="SkUiLook.StepperRepeatInterval"/> until stopped or a step cannot move.
/// </summary>
internal sealed class SkUiStepRepeat
{
    private IDisposable? _timer;
    private Func<int, bool>? _step;

    /// <summary>The held button (-1, +1), or 0.</summary>
    public int Held { get; private set; }

    /// <summary>Steps by <paramref name="button"/> once and keeps stepping while it is held.</summary>
    public void Start(int button, Func<int, bool> step)
    {
        _timer?.Dispose();
        Held = button;
        _step = step;
        step(button);
        _timer = SkUiGestureSettings.Schedule(SkUiLook.Current.StepperRepeatDelay, Repeat);
    }

    private void Repeat()
    {
        _timer = null;
        if (Held == 0 || _step is not { } step || !step(Held))
            return;
        _timer = SkUiGestureSettings.Schedule(SkUiLook.Current.StepperRepeatInterval, Repeat);
    }

    /// <summary>Stops repeating; returns whether a button was held.</summary>
    public bool Stop()
    {
        _timer?.Dispose();
        _timer = null;
        _step = null;
        if (Held == 0)
            return false;
        Held = 0;
        return true;
    }
}
