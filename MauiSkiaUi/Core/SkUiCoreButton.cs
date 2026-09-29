using System.Windows.Input;
using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// Minimal Core button: rounded fill, single-line label, intrinsic tap handling, and optional
/// <see cref="ICommand"/>.
/// </summary>
public class SkUiCoreButton : SkUiCoreLabel
{
    private bool _minimumHeightExplicit;
    private bool _isPressed;
    private ICommand? _command;
    private object? _commandParameter;
    private EventHandler? _commandChanged;

    /// <summary>Creates a centered white-on-accent button.</summary>
    public SkUiCoreButton()
    {
        SetTextColor(Colors.White);
        SetPadding(new Thickness(6));
        SetHorizontalTextAlignment(TextAlignment.Center);
        SetVerticalTextAlignment(TextAlignment.Center);
        SetFillColor(SkUiColors.Accent);
        SetPaintBackground(PaintButtonBackground);
    }

    /// <summary>Raised on a completed tap inside the button bounds (in addition to <see cref="Command"/>).</summary>
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

    /// <summary>Whether an eligible pointer is currently pressed inside this button.</summary>
    public bool IsPressed => _isPressed;

    /// <inheritdoc />
    protected override double DefaultCornerRadius => SkUiLook.Current.DefaultButtonCornerRadius;

    /// <summary>Sets fill color.</summary>
    public new SkUiCoreButton SetFillColor(Color value) { base.SetFillColor(value); return this; }

    /// <summary>Sets border color.</summary>
    public new SkUiCoreButton SetBorderColor(Color value) { base.SetBorderColor(value); return this; }

    /// <summary>Sets border width in DIPs.</summary>
    public new SkUiCoreButton SetBorderWidth(double value) { base.SetBorderWidth(value); return this; }

    /// <summary>Sets all four corner radii in DIPs (app-explicit, so look swaps do not replace them).</summary>
    public new SkUiCoreButton SetCornerRadius(int value) { base.SetCornerRadius(value); return this; }

    /// <summary>Sets the per-corner radii in DIPs (app-explicit, so look swaps do not replace them).</summary>
    public new SkUiCoreButton SetCornerRadii(CornerRadius value) { base.SetCornerRadii(value); return this; }

    /// <inheritdoc />
    public override SkUiCoreNode SetMinimumHeight(double value)
    {
        _minimumHeightExplicit = true;
        return base.SetMinimumHeight(value);
    }

    /// <summary>Sets the tap command. CanExecuteChanged uses a weak target so long-lived commands do not retain this node.</summary>
    public SkUiCoreButton SetCommand(ICommand? value)
    {
        if (ReferenceEquals(_command, value)) return this;
        if (_command is not null && _commandChanged is not null)
            _command.CanExecuteChanged -= _commandChanged;
        if (!SetProperty(ref _command, value, nameof(Command))) return this;
        if (value is not null)
        {
            var weak = new WeakReference<SkUiCoreButton>(this);
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

    private bool CanExecuteCommand => _command?.CanExecute(_commandParameter) ?? true;

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var size = base.MeasureContent(widthConstraint, heightConstraint);
        if (!_minimumHeightExplicit)
        {
            var min = SkUiLook.Current.DefaultButtonMinimumHeight;
            size = new Size(size.Width, Math.Max(size.Height, min));
        }
        return size;
    }

    /// <summary>Draws the rounded fill/border registered as <see cref="SkUiCoreNode.PaintBackground"/>. Subclasses may call or re-register this painter.</summary>
    protected void PaintButtonBackground(SKCanvas canvas) =>
        PaintChrome(canvas, !CanExecuteCommand ? SkUiColors.Disabled : _isPressed ? FillColor.MultiplyAlpha(0.75f) : FillColor);

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
        InvalidatePaint();
    }
}
