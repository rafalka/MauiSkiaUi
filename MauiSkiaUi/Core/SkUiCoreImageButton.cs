using System.Windows.Input;
using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// Core image with intrinsic taps, <see cref="ICommand"/>, and pressed/disabled tint overlay (Core analogue of
/// <c>SkUiImageButton</c>): the image sits inside <see cref="Padding"/>, is clipped to <see cref="CornerRadii"/>, and the
/// border is drawn inside the bounds.
/// </summary>
public class SkUiCoreImageButton : SkUiCoreImage, SkUiImageButtonDrawing.IImage
{
    private ICommand? _command;
    private object? _commandParameter;
    private CornerRadius _cornerRadii;
    private Color _borderColor = Colors.Transparent;
    private double _borderWidth;
    private Thickness _padding;
    private SkUiRoundedClip _clip;
    private bool _isPressed;
    private SkUiPressAnimator? _press;
    private EventHandler? _commandChanged;

    /// <summary>Creates an image button with a press/disabled tint overlay painter.</summary>
    public SkUiCoreImageButton() => SetPaintOverlay(PaintButtonOverlay);

    /// <summary>Raised on a completed tap (in addition to <see cref="Command"/>).</summary>
    public event EventHandler? Clicked;

    /// <summary>Raised when a press starts (MAUI order: <c>Pressed</c>, <c>Released</c>, then <c>Clicked</c> for a tap).</summary>
    public event EventHandler? Pressed;

    /// <summary>Raised when a press ends: released, cancelled (e.g. a scroll took over) or moved out.</summary>
    public event EventHandler? Released;

    /// <summary>Optional command executed on a completed tap.</summary>
    public ICommand? Command
    {
        get => _command;
        set => SetCommand(value);
    }

    /// <summary>Parameter passed to <see cref="Command"/>.</summary>
    public object? CommandParameter
    {
        get => _commandParameter;
        set => SetCommandParameter(value);
    }

    /// <summary>Per-corner radii in DIPs: they clip the image, the press / disabled tint and the border.</summary>
    public CornerRadius CornerRadii
    {
        get => _cornerRadii;
        set => SetCornerRadii(value);
    }

    /// <summary>Border color; the border is drawn inside the bounds, over the image.</summary>
    public Color BorderColor
    {
        get => _borderColor;
        set => SetBorderColor(value);
    }

    /// <summary>Border width in DIPs (0: no border).</summary>
    public double BorderWidth
    {
        get => _borderWidth;
        set => SetBorderWidth(value);
    }

    /// <summary>Space between the bounds and the image; adds to the intrinsic size.</summary>
    public Thickness Padding
    {
        get => _padding;
        set => SetPadding(value);
    }

    /// <summary>Whether an eligible pointer is currently pressed inside this button.</summary>
    public bool IsPressed => _isPressed;

    /// <inheritdoc cref="SkUiCoreImage.SetImage" />
    public new SkUiCoreImageButton SetImage(SKImage? image, bool ownsImage = true)
    {
        base.SetImage(image, ownsImage);
        return this;
    }

    /// <inheritdoc cref="SkUiCoreImage.SetAspect" />
    public new SkUiCoreImageButton SetAspect(Aspect value)
    {
        base.SetAspect(value);
        return this;
    }

    /// <summary>Sets the tap command. CanExecuteChanged uses a weak target so long-lived commands do not retain this node after dispose.</summary>
    public SkUiCoreImageButton SetCommand(ICommand? value)
    {
        if (ReferenceEquals(_command, value)) return this;
        if (_command is not null && _commandChanged is not null)
            _command.CanExecuteChanged -= _commandChanged;
        if (!SetProperty(ref _command, value, nameof(Command))) return this;
        if (value is not null)
        {
            var weak = new WeakReference<SkUiCoreImageButton>(this);
            EventHandler? listener = null;
            listener = (sender, _) =>
            {
                if (weak.TryGetTarget(out var button))
                    button.InvalidatePaint();
                else if (sender is ICommand oldCommand)
                    oldCommand.CanExecuteChanged -= listener;
            };
            _commandChanged = listener;
            value.CanExecuteChanged += listener;
        }
        else
        {
            _commandChanged = null;
        }
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets the command parameter.</summary>
    public SkUiCoreImageButton SetCommandParameter(object? value)
    {
        if (!SetProperty(ref _commandParameter, value, nameof(CommandParameter))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets the per-corner radii in DIPs.</summary>
    public SkUiCoreImageButton SetCornerRadii(CornerRadius value)
    {
        SkUiCornerRadii.Validate(value, nameof(value));
        if (!SetProperty(ref _cornerRadii, value, nameof(CornerRadii))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets all four <see cref="CornerRadii"/> to <paramref name="value"/> DIPs.</summary>
    public SkUiCoreImageButton SetCornerRadius(double value)
    {
        if (!double.IsFinite(value) || value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), value, "The corner radius must be finite and non-negative.");
        return SetCornerRadii(new CornerRadius(value));
    }

    /// <summary>Sets the border color.</summary>
    public SkUiCoreImageButton SetBorderColor(Color value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!SetProperty(ref _borderColor, value, nameof(BorderColor))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets the border width in DIPs.</summary>
    public SkUiCoreImageButton SetBorderWidth(double value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        if (!SetProperty(ref _borderWidth, value, nameof(BorderWidth))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets the padding around the image.</summary>
    public SkUiCoreImageButton SetPadding(Thickness value)
    {
        if (!SetProperty(ref _padding, value, nameof(Padding))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <summary>Convenience: assigns a <see cref="SkUiCoreCommand"/> that invokes <paramref name="execute"/>.</summary>
    public SkUiCoreImageButton SetClicked(Action? execute)
    {
        SetCommand(execute is null ? null : new SkUiCoreCommand(execute));
        return this;
    }

    private bool CanExecuteCommand => _command?.CanExecute(_commandParameter) ?? true;

    /// <inheritdoc />
    internal override CornerRadius PressEffectCornerRadii => _cornerRadii;

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        SkUiImageButtonDrawing.Measure(base.MeasureContent(widthConstraint, heightConstraint), _padding);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas) =>
        SkUiImageButtonDrawing.PaintContent(canvas, this, (float)Frame.Width, (float)Frame.Height, _padding, _cornerRadii, ref _clip);

    /// <summary>
    /// Draws press / disabled feedback (<see cref="SkUiLook.DrawPressOverlay"/>) and the border, registered as
    /// <see cref="SkUiCoreNode.PaintOverlay"/>. Subclasses may call or re-register this painter.
    /// </summary>
    protected void PaintButtonOverlay(SKCanvas canvas) =>
        SkUiImageButtonDrawing.PaintOverlay(canvas, (float)Frame.Width, (float)Frame.Height, _cornerRadii, ToSkColor(_borderColor), (float)_borderWidth,
            _press?.Visual ?? SkUiPressVisual.None, CanExecuteCommand);

    void SkUiImageButtonDrawing.IImage.Paint(SKCanvas canvas, SKRect area) => PaintImage(canvas, area);

    /// <inheritdoc />
    internal override bool HasIntrinsicTap => CanExecuteCommand;

    /// <inheritdoc />
    internal override void OnIntrinsicTap(SkUiTappedEventArgs args)
    {
        if (!CanExecuteCommand)
            return;
        Clicked?.Invoke(this, EventArgs.Empty);
        if (_command?.CanExecute(_commandParameter) == true)
            _command.Execute(_commandParameter);
    }

    /// <inheritdoc />
    internal override void OnGesturePressedChanged(bool pressed) => SetPressed(pressed);

    private void SetPressed(bool value)
    {
        if (!SetProperty(ref _isPressed, value, nameof(IsPressed))) return;
        (_press ??= new SkUiPressAnimator(this)).SetPressed(value, PressPosition);
        InvalidatePaint();
        (value ? Pressed : Released)?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        if (_command is not null && _commandChanged is not null)
            _command.CanExecuteChanged -= _commandChanged;
        _command = null;
        _commandChanged = null;
        base.Dispose();
    }
}
