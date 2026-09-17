using System.Windows.Input;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>A drawn image with intrinsic taps, commands, and pressed/disabled tint, similar to MAUI's ImageButton.</summary>
public class SkUiImageButton : SkUiImage
{
    private ICommand? _command;
    private object? _commandParameter;
    private double _cornerRadius;

    /// <summary>Bindable command executed on a valid release.</summary>
    public static readonly BindableProperty CommandProperty = BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(SkUiImageButton), null,
        propertyChanged: (view, _, value) => ((SkUiImageButton)view).SetCommand((ICommand?)value));
    /// <summary>Bindable command argument.</summary>
    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(SkUiImageButton), null,
        propertyChanged: (view, _, value) => ((SkUiImageButton)view).SetCommandParameter(value));
    /// <summary>Bindable corner radius applied to the pressed/disabled tint overlay clip.</summary>
    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(nameof(CornerRadius), typeof(double), typeof(SkUiImageButton), 0d,
        propertyChanged: (view, _, value) => ((SkUiImageButton)view).SetCornerRadius((double)value));

    /// <summary>Raised for a valid enabled tap, even without a command.</summary>
    public event EventHandler? Clicked;
    /// <summary>Command; CanExecute also controls tap eligibility and the disabled tint.</summary>
    public ICommand? Command { get => _command; set => SetValue(CommandProperty, value); }
    /// <summary>Command argument.</summary>
    public object? CommandParameter { get => _commandParameter; set => SetValue(CommandParameterProperty, value); }
    /// <summary>Corner radius in DIPs for the pressed/disabled tint clip; the image itself is not clipped.</summary>
    public double CornerRadius { get => _cornerRadius; set => SetValue(CornerRadiusProperty, value); }

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
                if (weak.TryGetTarget(out var button)) button.InvalidatePaint();
                else if (sender is ICommand oldCommand) oldCommand.CanExecuteChanged -= listener;
            };
            _commandChanged = listener;
            value.CanExecuteChanged += listener;
        }
        InvalidatePaint();
        return this;
    }
    private EventHandler? _commandChanged;
    /// <summary>Sets command argument without bindable write-back.</summary>
    public SkUiImageButton SetCommandParameter(object? value) { _commandParameter = value; InvalidatePaint(); return this; }
    /// <summary>Sets the tint clip corner radius without bindable write-back.</summary>
    public SkUiImageButton SetCornerRadius(double value) { ArgumentOutOfRangeException.ThrowIfNegative(value); _cornerRadius = value; InvalidatePaint(); return this; }

    /// <summary>Creates an image button with a press/disabled tint overlay painter.</summary>
    public SkUiImageButton() => SetPaintOverlay(PaintButtonOverlay);

    /// <inheritdoc />
    protected override bool HandlesTap => true;
    /// <inheritdoc />
    protected override bool CanReceiveTap => _command?.CanExecute(_commandParameter) ?? true;
    /// <inheritdoc />
    protected override void OnPressedChanged() => InvalidatePaint();

    /// <inheritdoc />
    protected override void OnTapped(SkUiTappedEventArgs args)
    {
        RaiseTapped(args);
        Clicked?.Invoke(this, EventArgs.Empty);
        if (_command?.CanExecute(_commandParameter) == true) _command.Execute(_commandParameter);
    }

    /// <summary>Draws pressed/disabled tint registered as <see cref="SkUiView.PaintOverlay"/>. Subclasses may call or re-register this painter.</summary>
    protected void PaintButtonOverlay(SKCanvas canvas) =>
        SkUiLook.Current.DrawPressTint(
            canvas,
            new SKRect(0, 0, (float)Width, (float)Height),
            (float)_cornerRadius,
            disabled: !IsEnabled || !CanReceiveTap,
            pressed: IsPressed);
}
