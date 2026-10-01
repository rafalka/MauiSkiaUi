using System.Windows.Input;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// A drawn image with intrinsic taps, commands, and pressed/disabled tint, similar to MAUI's ImageButton: the image sits
/// inside <see cref="Padding"/>, is clipped to <see cref="CornerRadii"/> (or the uniform, MAUI-compatible <see cref="CornerRadius"/>), and the border is drawn
/// inside the bounds.
/// </summary>
public class SkUiImageButton : SkUiImage, SkUiImageButtonDrawing.IImage
{
    private ICommand? _command;
    private object? _commandParameter;
    private CornerRadius _cornerRadii;
    private Color _borderColor = Colors.Transparent;
    private double _borderWidth;
    private Thickness _padding;
    private SkUiRoundedClip _clip;

    /// <summary>Bindable command executed on a valid release.</summary>
    public static readonly BindableProperty CommandProperty = BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(SkUiImageButton), null,
        propertyChanged: (view, _, value) => ((SkUiImageButton)view).SetCommand((ICommand?)value));
    /// <summary>Bindable command argument.</summary>
    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(SkUiImageButton), null,
        propertyChanged: (view, _, value) => ((SkUiImageButton)view).SetCommandParameter(value));
    /// <summary>Bindable per-corner radii (0: square).</summary>
    public static readonly BindableProperty CornerRadiiProperty = BindableProperty.Create(nameof(CornerRadii), typeof(CornerRadius), typeof(SkUiImageButton), default(CornerRadius),
        propertyChanged: (view, _, value) => ((SkUiImageButton)view).SetCornerRadii((CornerRadius)value));
    /// <summary>Bindable uniform corner radius (MAUI ImageButton's <c>int</c> <c>CornerRadius</c>): sets all four <see cref="CornerRadii"/>.</summary>
    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(nameof(CornerRadius), typeof(int), typeof(SkUiImageButton), 0,
        propertyChanged: (view, _, value) => ((SkUiImageButton)view).SetCornerRadius((int)value));
    /// <summary>Bindable <see cref="BorderColor"/>.</summary>
    public static readonly BindableProperty BorderColorProperty = BindableProperty.Create(nameof(BorderColor), typeof(Color), typeof(SkUiImageButton), Colors.Transparent,
        propertyChanged: (view, _, value) => ((SkUiImageButton)view).SetBorderColor((Color)value));
    /// <summary>Bindable <see cref="BorderWidth"/>.</summary>
    public static readonly BindableProperty BorderWidthProperty = BindableProperty.Create(nameof(BorderWidth), typeof(double), typeof(SkUiImageButton), 0d,
        propertyChanged: (view, _, value) => ((SkUiImageButton)view).SetBorderWidth((double)value));
    /// <summary>Bindable <see cref="Padding"/>.</summary>
    public static readonly BindableProperty PaddingProperty = BindableProperty.Create(nameof(Padding), typeof(Thickness), typeof(SkUiImageButton), default(Thickness),
        propertyChanged: (view, _, value) => ((SkUiImageButton)view).SetPadding((Thickness)value));

    /// <summary>Raised for a valid enabled tap, even without a command.</summary>
    public event EventHandler? Clicked;
    /// <summary>Raised when a press starts (MAUI order: <c>Pressed</c>, <c>Released</c>, then <c>Clicked</c> for a tap).</summary>
    public event EventHandler? Pressed;
    /// <summary>Raised when a press ends: released, cancelled (e.g. a scroll took over) or moved out.</summary>
    public event EventHandler? Released;
    /// <summary>Command; CanExecute also controls tap eligibility and the disabled tint.</summary>
    public ICommand? Command { get => _command; set => SetValue(CommandProperty, value); }
    /// <summary>Command argument.</summary>
    public object? CommandParameter { get => _commandParameter; set => SetValue(CommandParameterProperty, value); }
    /// <summary>Per-corner radii in DIPs: they clip the image, the press / disabled tint and the border.</summary>
    public CornerRadius CornerRadii { get => _cornerRadii; set => SetValue(CornerRadiiProperty, value); }
    /// <summary>
    /// Uniform view of <see cref="CornerRadii"/>, an <c>int</c> as MAUI ImageButton's <c>CornerRadius</c>: setting it sets all
    /// four corners, and it reads the top-left radius, rounded (whichever of the two is set last wins); use
    /// <see cref="CornerRadii"/> for fractional or per-corner radii.
    /// </summary>
    public int CornerRadius { get => (int)Math.Round(_cornerRadii.TopLeft); set => SetValue(CornerRadiusProperty, value); }
    /// <summary>Border color; the border is drawn inside the bounds, over the image.</summary>
    public Color BorderColor { get => _borderColor; set => SetValue(BorderColorProperty, value); }
    /// <summary>Border width in DIPs (0: no border).</summary>
    public double BorderWidth { get => _borderWidth; set => SetValue(BorderWidthProperty, value); }
    /// <summary>Space between the bounds and the image; adds to the intrinsic size.</summary>
    public Thickness Padding { get => _padding; set => SetValue(PaddingProperty, value); }

    /// <summary>Sets command without bindable write-back; command notifications use a weak target.</summary>
    public SkUiImageButton SetCommand(ICommand? value)
    {
        if (_command == value) return this;
        if (_command is not null && _commandChanged is not null) _command.CanExecuteChanged -= _commandChanged;
        _command = value;
        if (value is not null)
        {
            var weak = new WeakReference<SkUiImageButton>(this);
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
    public SkUiImageButton SetCommandParameter(object? value) { _commandParameter = value; UpdateState(); return this; }
    /// <summary>Sets the per-corner radii without bindable write-back.</summary>
    public SkUiImageButton SetCornerRadii(CornerRadius value) { SkUiCornerRadii.Validate(value, nameof(value)); if (_cornerRadii == value) return this; _cornerRadii = value; InvalidatePaint(); return this; }
    /// <summary>Sets all four corner radii to <paramref name="value"/> without bindable write-back.</summary>
    public SkUiImageButton SetCornerRadius(int value) { ArgumentOutOfRangeException.ThrowIfNegative(value); return SetCornerRadii(new CornerRadius(value)); }
    /// <summary>Sets the border color without bindable write-back.</summary>
    public SkUiImageButton SetBorderColor(Color value) { ArgumentNullException.ThrowIfNull(value); if (_borderColor == value) return this; _borderColor = value; InvalidatePaint(); return this; }
    /// <summary>Sets the border width without bindable write-back.</summary>
    public SkUiImageButton SetBorderWidth(double value) { ArgumentOutOfRangeException.ThrowIfNegative(value); if (_borderWidth == value) return this; _borderWidth = value; InvalidatePaint(); return this; }
    /// <summary>Sets the padding without bindable write-back.</summary>
    public SkUiImageButton SetPadding(Thickness value) { if (_padding == value) return this; _padding = value; InvalidateMeasureOverride(); return this; }

    /// <summary>Creates an image button with a press/disabled tint overlay painter.</summary>
    public SkUiImageButton() => SetPaintOverlay(PaintButtonOverlay);

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

    private SkUiPressAnimator? _press;

    private void UpdateState()
    {
        ChangeVisualState();
        InvalidatePaint();
    }

    /// <summary>MAUI's image button states: <c>Pressed</c> while an enabled press is held, otherwise the common states.</summary>
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
    protected override void OnTapped(SkUiTappedEventArgs args)
    {
        RaiseTapped(args);
        Clicked?.Invoke(this, EventArgs.Empty);
        if (_command?.CanExecute(_commandParameter) == true) _command.Execute(_commandParameter);
    }

    /// <inheritdoc />
    internal override CornerRadius PressEffectCornerRadii => _cornerRadii;

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        SkUiImageButtonDrawing.Measure(base.MeasureContent(widthConstraint, heightConstraint), _padding);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas) =>
        SkUiImageButtonDrawing.PaintContent(canvas, this, (float)Width, (float)Height, _padding, _cornerRadii, ref _clip);

    /// <summary>
    /// Draws press / disabled feedback (<see cref="SkUiLook.DrawPressOverlay"/>) and the border, registered as
    /// <see cref="SkUiView.PaintOverlay"/>. Subclasses may call or re-register this painter.
    /// </summary>
    protected void PaintButtonOverlay(SKCanvas canvas) =>
        SkUiImageButtonDrawing.PaintOverlay(canvas, (float)Width, (float)Height, _cornerRadii, ToSkColor(_borderColor), (float)_borderWidth,
            _press?.Visual ?? SkUiPressVisual.None, IsEnabled && CanReceiveTap);

    void SkUiImageButtonDrawing.IImage.Paint(SKCanvas canvas, SKRect area) => PaintImage(canvas, area);
}
