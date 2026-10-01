using System.Windows.Input;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>A drawn text button with intrinsic taps, commands, and press/disabled feedback.</summary>
public class SkUiButton : SkUiLabel
{
    private ICommand? _command;
    private object? _commandParameter;
    private Color _fillColor = SkUiColors.Accent;
    private SkUiPressAnimator? _press;

    /// <summary>Bindable command executed on a valid release.</summary>
    public static readonly BindableProperty CommandProperty = BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(SkUiButton), null, propertyChanged: (view, _, value) => ((SkUiButton)view).SetCommand((ICommand?)value));
    /// <summary>Bindable command argument.</summary>
    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(SkUiButton), null, propertyChanged: (view, _, value) => ((SkUiButton)view).SetCommandParameter(value));
    /// <summary>Bindable button fill.</summary>
    public static readonly BindableProperty FillColorProperty = BindableProperty.Create(nameof(FillColor), typeof(Color), typeof(SkUiButton), null,
        defaultValueCreator: _ => SkUiColors.Accent,
        propertyChanged: (view, _, value) => ((SkUiButton)view).SetFillColor((Color)value));

    /// <summary>Creates a centered, padded button.</summary>
    public SkUiButton()
    {
        SetTextColor(Colors.White);
        SetPadding(new Thickness(18, 12));
        SetHorizontalTextAlignment(TextAlignment.Center);
        SetVerticalTextAlignment(TextAlignment.Center);
        SetCornerRadii(new CornerRadius(DefaultCornerRadius));
        SetPaintBackground(PaintButtonBackground);
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var size = base.MeasureContent(widthConstraint, heightConstraint);
        // MinimumHeightRequest default is -1; only then resolve the active look's button minimum.
        if (MinimumHeightRequest < 0)
        {
            var min = SkUiLook.Current.DefaultButtonMinimumHeight;
            size = new Size(size.Width, Math.Max(size.Height, min));
        }
        return size;
    }

    /// <inheritdoc />
    protected override Color DefaultTextColor => Colors.White;
    /// <inheritdoc />
    protected override TextAlignment DefaultTextAlignment => TextAlignment.Center;
    /// <inheritdoc />
    protected override Thickness DefaultPadding => new(18, 12);
    /// <inheritdoc />
    protected override double DefaultCornerRadius => SkUiLook.Current.DefaultButtonCornerRadius;

    /// <summary>Raised for a valid enabled tap, even without a command.</summary>
    public event EventHandler? Clicked;
    /// <summary>Raised when a press starts (MAUI order: <c>Pressed</c>, <c>Released</c>, then <c>Clicked</c> for a tap).</summary>
    public event EventHandler? Pressed;
    /// <summary>Raised when a press ends: released, cancelled (e.g. a scroll took over) or moved out.</summary>
    public event EventHandler? Released;
    /// <summary>Command; CanExecute also controls tap eligibility and disabled appearance.</summary>
    public ICommand? Command { get => _command; set => SetValue(CommandProperty, value); }
    /// <summary>Command argument.</summary>
    public object? CommandParameter { get => _commandParameter; set => SetValue(CommandParameterProperty, value); }
    /// <summary>Button background fill.</summary>
    public Color FillColor { get => _fillColor; set => SetValue(FillColorProperty, value); }

    /// <summary>Sets command without bindable write-back; command notifications use a weak target.</summary>
    public SkUiButton SetCommand(ICommand? value)
    {
        if (_command == value) return this;
        if (_command is not null && _commandChanged is not null) _command.CanExecuteChanged -= _commandChanged;
        _command = value;
        if (value is not null)
        {
            var weak = new WeakReference<SkUiButton>(this);
            EventHandler? listener = null;
            listener = (sender, _) =>
            {
                if (weak.TryGetTarget(out var button)) button.UpdateState();
                else if (sender is ICommand oldCommand) oldCommand.CanExecuteChanged -= listener;
            };
            _commandChanged = listener;
            value.CanExecuteChanged += listener;
        }
        UpdateState();
        return this;
    }
    private EventHandler? _commandChanged;
    /// <summary>Sets command argument without bindable write-back.</summary>
    public SkUiButton SetCommandParameter(object? value) { _commandParameter = value; UpdateState(); return this; }
    /// <summary>Sets all four corner radii without bindable write-back.</summary>
    public new SkUiButton SetCornerRadius(int value) { base.SetCornerRadius(value); return this; }
    /// <summary>Sets the per-corner radii without bindable write-back.</summary>
    public new SkUiButton SetCornerRadii(CornerRadius value) { base.SetCornerRadii(value); return this; }
    /// <summary>Sets fill without bindable write-back.</summary>
    public SkUiButton SetFillColor(Color value) { ArgumentNullException.ThrowIfNull(value); _fillColor = value; InvalidatePaint(); return this; }
    /// <summary>Sets border color without bindable write-back.</summary>
    public new SkUiButton SetBorderColor(Color value) { base.SetBorderColor(value); return this; }
    /// <summary>Sets border width without bindable write-back.</summary>
    public new SkUiButton SetBorderWidth(double value) { base.SetBorderWidth(value); return this; }
    /// <inheritdoc />
    protected override bool HandlesTap => true;
    /// <inheritdoc />
    protected override bool CanReceiveTap => _command?.CanExecute(_commandParameter) ?? true;
    /// <inheritdoc />
    protected override void OnPressedChanged()
    {
        (_press ??= new SkUiPressAnimator(this)).SetPressed(IsPressed, PressPosition);
        InvalidatePaint();
        (IsPressed ? Pressed : Released)?.Invoke(this, EventArgs.Empty);
    }
    private void UpdateState()
    {
        ChangeVisualState();
        InvalidatePaint();
    }
    /// <summary>MAUI's button states: <c>Pressed</c> while an enabled press is held, otherwise the common states.</summary>
    protected override void ChangeVisualState()
    {
        if (IsVisualStateEnabled && IsPressed)
            VisualStateManager.GoToState(this, PressedVisualState);
        else
            base.ChangeVisualState();
    }

    /// <summary>The name of the visual state while pressed (MAUI's <c>ButtonElement.PressedVisualState</c>).</summary>
    public const string PressedVisualState = "Pressed";

    /// <inheritdoc />
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(IsEnabled)) UpdateState();
    }
    /// <summary>
    /// Raises the shared Tapped event and Clicked, then executes Command. TappedCommand (inherited from SkUiView)
    /// is intentionally not executed here to avoid running two commands from a single tap; use Command instead.
    /// </summary>
    protected override void OnTapped(SkUiTappedEventArgs args)
    {
        RaiseTapped(args);
        Clicked?.Invoke(this, EventArgs.Empty);
        if (_command?.CanExecute(_commandParameter) == true) _command.Execute(_commandParameter);
    }
    /// <summary>
    /// Draws the rounded fill, border and press feedback (<see cref="SkUiLook.DrawButton"/>) registered as
    /// <see cref="SkUiView.PaintBackground"/>. Subclasses may call or re-register this painter.
    /// </summary>
    protected void PaintButtonBackground(SKCanvas canvas)
    {
        var enabled = IsEnabled && CanReceiveTap;
        var color = enabled ? ResolveSolidBackgroundColor() ?? _fillColor : SkUiColors.Disabled;
        SkUiLook.Current.DrawButton(canvas, new SkUiButtonPaint(new SKRect(0, 0, (float)Width, (float)Height), CornerRadii,
            ToSkColor(color), ToSkColor(BorderColor), (float)BorderWidth, _press?.Visual ?? SkUiPressVisual.None, enabled));
    }
}
