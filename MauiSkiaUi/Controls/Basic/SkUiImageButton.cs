using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>A drawn image with intrinsic taps, commands, and pressed/disabled tint, similar to MAUI's ImageButton.</summary>
public class SkUiImageButton : SkUiImage
{
    private ICommand? command;
    private object? commandParameter;
    private double cornerRadius;

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
    public ICommand? Command { get => command; set => SetValue(CommandProperty, value); }
    /// <summary>Command argument.</summary>
    public object? CommandParameter { get => commandParameter; set => SetValue(CommandParameterProperty, value); }
    /// <summary>Corner radius in DIPs for the pressed/disabled tint clip; the image itself is not clipped.</summary>
    public double CornerRadius { get => cornerRadius; set => SetValue(CornerRadiusProperty, value); }

    /// <summary>Sets command without bindable write-back; command notifications use a weak target.</summary>
    public SkUiImageButton SetCommand(ICommand? value)
    {
        if (command == value) return this;
        if (command is not null && commandChanged is not null) command.CanExecuteChanged -= commandChanged;
        command = value;
        if (value is not null)
        {
            var weak = new WeakReference<SkUiImageButton>(this);
            EventHandler? listener = null;
            listener = (sender, _) =>
            {
                if (weak.TryGetTarget(out var button)) button.InvalidatePaint();
                else if (sender is ICommand oldCommand) oldCommand.CanExecuteChanged -= listener;
            };
            commandChanged = listener;
            value.CanExecuteChanged += listener;
        }
        InvalidatePaint();
        return this;
    }
    private EventHandler? commandChanged;
    /// <summary>Sets command argument without bindable write-back.</summary>
    public SkUiImageButton SetCommandParameter(object? value) { commandParameter = value; InvalidatePaint(); return this; }
    /// <summary>Sets the tint clip corner radius without bindable write-back.</summary>
    public SkUiImageButton SetCornerRadius(double value) { ArgumentOutOfRangeException.ThrowIfNegative(value); cornerRadius = value; InvalidatePaint(); return this; }

    /// <inheritdoc />
    protected override bool HandlesTap => true;
    /// <inheritdoc />
    protected override bool CanReceiveTap => command?.CanExecute(commandParameter) ?? true;
    /// <inheritdoc />
    protected override void OnPressedChanged() => InvalidatePaint();

    /// <inheritdoc />
    protected override void OnTapped(SkUiTappedEventArgs args)
    {
        RaiseTapped(args);
        Clicked?.Invoke(this, EventArgs.Empty);
        if (command?.CanExecute(commandParameter) == true) command.Execute(commandParameter);
    }

    /// <inheritdoc />
    protected override void OnPaintOverlay(SKCanvas canvas)
    {
        base.OnPaintOverlay(canvas);
        if (IsEnabled && CanReceiveTap && !IsPressed) return;
        var tint = !IsEnabled || !CanReceiveTap ? new SKColor(0, 0, 0, 96) : new SKColor(0, 0, 0, 48);
        using var paint = new SKPaint { Color = tint };
        if (cornerRadius > 0)
        {
            using var clip = SkUiChrome.CreateRoundRectPath(new SKRect(0, 0, (float)Width, (float)Height), (float)cornerRadius);
            canvas.DrawPath(clip, paint);
        }
        else
        {
            canvas.DrawRect(0, 0, (float)Width, (float)Height, paint);
        }
    }
}
