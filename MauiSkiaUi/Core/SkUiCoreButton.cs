using System.Windows.Input;
using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// Minimal Core button: rounded fill, single-line label, intrinsic tap handling, and optional
/// <see cref="ICommand"/>.
/// </summary>
public class SkUiCoreButton : SkUiCoreLabel
{
    private Color _fillColor = SkUiColors.Accent;
    private Color _borderColor = Colors.Transparent;
    private double _borderWidth;
    private double _cornerRadius = SkUiLook.Current.DefaultButtonCornerRadius;
    private long? _pressedPointer;
    private bool _isPressed;
    private ICommand? _command;
    private object? _commandParameter;

    /// <summary>Creates a centered white-on-accent button.</summary>
    public SkUiCoreButton()
    {
        SetTextColor(Colors.White);
        SetPadding(new Thickness(6));
        SetHorizontalTextAlignment(TextAlignment.Center);
        SetVerticalTextAlignment(TextAlignment.Center);
        SetMinimumHeight(SkUiLook.Current.DefaultButtonMinimumHeight);
        SetPaintBackground(PaintButtonBackground);
    }

    /// <summary>Raised on a completed tap inside the button bounds (in addition to <see cref="Command"/>).</summary>
    public event EventHandler? Clicked;

    /// <summary>Fill color.</summary>
    public Color FillColor
    {
        get => _fillColor;
        set => SetFillColor(value);
    }

    /// <summary>Border color.</summary>
    public Color BorderColor
    {
        get => _borderColor;
        set => SetBorderColor(value);
    }

    /// <summary>Border width in DIPs.</summary>
    public double BorderWidth
    {
        get => _borderWidth;
        set => SetBorderWidth(value);
    }

    /// <summary>Corner radius in DIPs.</summary>
    public double CornerRadius
    {
        get => _cornerRadius;
        set => SetCornerRadius(value);
    }

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

    /// <summary>Whether an eligible pointer is currently pressed inside this button.</summary>
    public bool IsPressed => _isPressed;

    /// <summary>Sets fill color.</summary>
    public SkUiCoreButton SetFillColor(Color value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!SetProperty(ref _fillColor, value, nameof(FillColor))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets border color.</summary>
    public SkUiCoreButton SetBorderColor(Color value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!SetProperty(ref _borderColor, value, nameof(BorderColor))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets border width in DIPs.</summary>
    public SkUiCoreButton SetBorderWidth(double value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        if (!SetProperty(ref _borderWidth, value, nameof(BorderWidth))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets corner radius in DIPs.</summary>
    public SkUiCoreButton SetCornerRadius(double value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        if (!SetProperty(ref _cornerRadius, value, nameof(CornerRadius))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets the tap command.</summary>
    public SkUiCoreButton SetCommand(ICommand? value)
    {
        if (ReferenceEquals(_command, value)) return this;
        var previous = _command;
        if (!SetProperty(ref _command, value, nameof(Command))) return this;
        if (previous is not null)
            previous.CanExecuteChanged -= OnCommandCanExecuteChanged;
        if (_command is not null)
            _command.CanExecuteChanged += OnCommandCanExecuteChanged;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets the command parameter.</summary>
    public SkUiCoreButton SetCommandParameter(object? value)
    {
        if (!SetProperty(ref _commandParameter, value, nameof(CommandParameter))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>
    /// Convenience: assigns a <see cref="SkUiCoreCommand"/> that invokes <paramref name="execute"/>.
    /// Prefer <see cref="SetCommand"/> when the caller already owns an <see cref="ICommand"/>.
    /// </summary>
    public SkUiCoreButton SetClicked(Action? execute)
    {
        SetCommand(execute is null ? null : new SkUiCoreCommand(execute));
        return this;
    }

    private void OnCommandCanExecuteChanged(object? sender, EventArgs e) => InvalidatePaint();

    private bool CanExecuteCommand => _command?.CanExecute(_commandParameter) ?? true;

    /// <summary>Draws the rounded fill/border registered as <see cref="SkUiCoreNode.PaintBackground"/>. Subclasses may call or re-register this painter.</summary>
    protected void PaintButtonBackground(SKCanvas canvas)
    {
        var color = !CanExecuteCommand ? SkUiColors.Disabled
            : _isPressed ? _fillColor.MultiplyAlpha(0.75f) : _fillColor;
        SkUiLook.Current.DrawRoundedBox(
            canvas,
            new SKRect(0, 0, (float)Frame.Width, (float)Frame.Height),
            (float)_cornerRadius,
            ToSkColor(color),
            ToSkColor(_borderColor),
            (float)_borderWidth);
    }

    /// <summary>Clips text to the same rounded-rect geometry as the background fill so glyphs never bleed past the corners.</summary>
    protected override void OnPaintContent(SKCanvas canvas)
    {
        var saveCount = canvas.Save();
        try
        {
            using var clip = SkUiLook.Current.CreateRoundRectPath(
                new SKRect(0, 0, (float)Frame.Width, (float)Frame.Height),
                (float)_cornerRadius);
            canvas.ClipPath(clip, antialias: true);
            base.OnPaintContent(canvas);
        }
        finally
        {
            canvas.RestoreToCount(saveCount);
        }
    }

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
                var inside = touch.Position.X >= 0 && touch.Position.Y >= 0
                    && touch.Position.X < Frame.Width && touch.Position.Y < Frame.Height;
                if (!inside)
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
                if (wasPressed
                    && touch.Position.X >= 0 && touch.Position.Y >= 0
                    && touch.Position.X < Frame.Width && touch.Position.Y < Frame.Height
                    && CanExecuteCommand)
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

    private void SetPressed(bool value)
    {
        if (!SetProperty(ref _isPressed, value, nameof(IsPressed))) return;
        InvalidatePaint();
    }
}
