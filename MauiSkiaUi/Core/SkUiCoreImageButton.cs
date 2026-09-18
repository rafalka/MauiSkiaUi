using System.Windows.Input;
using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// Core image with intrinsic taps, <see cref="ICommand"/>, and pressed/disabled tint overlay
/// (Core analogue of <c>SkUiImageButton</c>).
/// </summary>
public class SkUiCoreImageButton : SkUiCoreImage
{
    private ICommand? _command;
    private object? _commandParameter;
    private double _cornerRadius;
    private long? _pressedPointer;
    private bool _isPressed;
    private EventHandler? _commandChanged;

    /// <summary>Creates an image button with a press/disabled tint overlay painter.</summary>
    public SkUiCoreImageButton() => SetPaintOverlay(PaintButtonOverlay);

    /// <summary>Raised on a completed tap (in addition to <see cref="Command"/>).</summary>
    public event EventHandler? Clicked;

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

    /// <summary>Corner radius in DIPs for the pressed/disabled tint clip; the image itself is not clipped.</summary>
    public double CornerRadius
    {
        get => _cornerRadius;
        set => SetCornerRadius(value);
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

    /// <summary>Sets the tint clip corner radius in DIPs.</summary>
    public SkUiCoreImageButton SetCornerRadius(double value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        if (!SetProperty(ref _cornerRadius, value, nameof(CornerRadius))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Convenience: assigns a <see cref="SkUiCoreCommand"/> that invokes <paramref name="execute"/>.</summary>
    public SkUiCoreImageButton SetClicked(Action? execute)
    {
        SetCommand(execute is null ? null : new SkUiCoreCommand(execute));
        return this;
    }

    private bool CanExecuteCommand => _command?.CanExecute(_commandParameter) ?? true;

    /// <summary>Draws pressed/disabled tint registered as <see cref="SkUiCoreNode.PaintOverlay"/>. Subclasses may call or re-register this painter.</summary>
    protected void PaintButtonOverlay(SKCanvas canvas) =>
        SkUiLook.Current.DrawPressTint(
            canvas,
            new SKRect(0, 0, (float)Frame.Width, (float)Frame.Height),
            (float)_cornerRadius,
            disabled: !CanExecuteCommand,
            pressed: _isPressed);

    /// <inheritdoc />
    public override bool Touch(SkUiTouchEvent touch)
    {
        switch (touch.Action)
        {
            case SkUiTouchAction.Pressed:
                if (_pressedPointer is not null || !CanExecuteCommand) return false;
                _pressedPointer = touch.Id;
                SetPressed(true);
                return true;
            case SkUiTouchAction.Moved:
                if (_pressedPointer != touch.Id) return false;
                if (!Contains(touch.Position))
                {
                    _pressedPointer = null;
                    SetPressed(false);
                }
                return true;
            case SkUiTouchAction.Released:
                if (_pressedPointer != touch.Id) return false;
                _pressedPointer = null;
                var wasPressed = _isPressed;
                SetPressed(false);
                if (wasPressed && Contains(touch.Position) && CanExecuteCommand)
                {
                    Clicked?.Invoke(this, EventArgs.Empty);
                    if (_command?.CanExecute(_commandParameter) == true)
                        _command.Execute(_commandParameter);
                }
                return true;
            case SkUiTouchAction.Cancelled:
                if (_pressedPointer != touch.Id) return false;
                _pressedPointer = null;
                SetPressed(false);
                return true;
            default:
                return false;
        }
    }

    private bool Contains(Point position) =>
        position.X >= 0 && position.Y >= 0 && position.X < Frame.Width && position.Y < Frame.Height;

    private void SetPressed(bool value)
    {
        if (!SetProperty(ref _isPressed, value, nameof(IsPressed))) return;
        InvalidatePaint();
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
