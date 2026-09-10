using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>A drawn text button with intrinsic taps, commands, and press/disabled feedback.</summary>
public class SkUiButton : SkUiLabel
{
    private ICommand? command;
    private object? commandParameter;
    private double cornerRadius = 6;
    private Color fillColor = Color.FromArgb("#087F83");
    private Color borderColor = Colors.Transparent;
    private double borderWidth;

    /// <summary>Bindable command executed on a valid release.</summary>
    public static readonly BindableProperty CommandProperty = BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(SkUiButton), null, propertyChanged: (view, _, value) => ((SkUiButton)view).SetCommand((ICommand?)value));
    /// <summary>Bindable command argument.</summary>
    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(SkUiButton), null, propertyChanged: (view, _, value) => ((SkUiButton)view).SetCommandParameter(value));
    /// <summary>Bindable rounded corner radius.</summary>
    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(nameof(CornerRadius), typeof(double), typeof(SkUiButton), 6d, propertyChanged: (view, _, value) => ((SkUiButton)view).SetCornerRadius((double)value));
    /// <summary>Bindable button fill.</summary>
    public static readonly BindableProperty FillColorProperty = BindableProperty.Create(nameof(FillColor), typeof(Color), typeof(SkUiButton), Color.FromArgb("#087F83"), propertyChanged: (view, _, value) => ((SkUiButton)view).SetFillColor((Color)value));
    /// <summary>Bindable border color.</summary>
    public static readonly BindableProperty BorderColorProperty = BindableProperty.Create(nameof(BorderColor), typeof(Color), typeof(SkUiButton), Colors.Transparent, propertyChanged: (view, _, value) => ((SkUiButton)view).SetBorderColor((Color)value));
    /// <summary>Bindable border width.</summary>
    public static readonly BindableProperty BorderWidthProperty = BindableProperty.Create(nameof(BorderWidth), typeof(double), typeof(SkUiButton), 0d, propertyChanged: (view, _, value) => ((SkUiButton)view).SetBorderWidth((double)value));

    /// <summary>Creates a centered, padded button.</summary>
    public SkUiButton()
    {
        SetTextColor(Colors.White);
        SetPadding(new Thickness(18, 12));
        SetHorizontalTextAlignment(TextAlignment.Center);
        SetVerticalTextAlignment(TextAlignment.Center);
        MinimumHeightRequest = 44;
    }

    /// <inheritdoc />
    protected override Color DefaultTextColor => Colors.White;
    /// <inheritdoc />
    protected override TextAlignment DefaultTextAlignment => TextAlignment.Center;
    /// <inheritdoc />
    protected override Thickness DefaultPadding => new(18, 12);

    /// <summary>Raised for a valid enabled tap, even without a command.</summary>
    public event EventHandler? Clicked;
    /// <summary>Command; CanExecute also controls tap eligibility and disabled appearance.</summary>
    public ICommand? Command { get => command; set => SetValue(CommandProperty, value); }
    /// <summary>Command argument.</summary>
    public object? CommandParameter { get => commandParameter; set => SetValue(CommandParameterProperty, value); }
    /// <summary>Rounded radius in DIPs; hit bounds remain rectangular.</summary>
    public double CornerRadius { get => cornerRadius; set => SetValue(CornerRadiusProperty, value); }
    /// <summary>Button background fill.</summary>
    public Color FillColor { get => fillColor; set => SetValue(FillColorProperty, value); }
    /// <summary>Border color.</summary>
    public Color BorderColor { get => borderColor; set => SetValue(BorderColorProperty, value); }
    /// <summary>Border width in DIPs.</summary>
    public double BorderWidth { get => borderWidth; set => SetValue(BorderWidthProperty, value); }

    /// <summary>Sets command without bindable write-back; command notifications use a weak target.</summary>
    public SkUiButton SetCommand(ICommand? value)
    {
        if (command == value) return this;
        if (command is not null && commandChanged is not null) command.CanExecuteChanged -= commandChanged;
        command = value;
        if (value is not null)
        {
            var weak = new WeakReference<SkUiButton>(this);
            EventHandler? listener = null;
            listener = (sender, _) =>
            {
                if (weak.TryGetTarget(out var button)) button.UpdateState();
                else if (sender is ICommand oldCommand) oldCommand.CanExecuteChanged -= listener;
            };
            commandChanged = listener;
            value.CanExecuteChanged += listener;
        }
        UpdateState();
        return this;
    }
    private EventHandler? commandChanged;
    /// <summary>Sets command argument without bindable write-back.</summary>
    public SkUiButton SetCommandParameter(object? value) { commandParameter = value; UpdateState(); return this; }
    /// <summary>Sets radius without bindable write-back.</summary>
    public SkUiButton SetCornerRadius(double value) { ArgumentOutOfRangeException.ThrowIfNegative(value); cornerRadius = value; InvalidatePaint(); return this; }
    /// <summary>Sets fill without bindable write-back.</summary>
    public SkUiButton SetFillColor(Color value) { ArgumentNullException.ThrowIfNull(value); fillColor = value; InvalidatePaint(); return this; }
    /// <summary>Sets border color without bindable write-back.</summary>
    public SkUiButton SetBorderColor(Color value) { ArgumentNullException.ThrowIfNull(value); borderColor = value; InvalidatePaint(); return this; }
    /// <summary>Sets border width without bindable write-back.</summary>
    public SkUiButton SetBorderWidth(double value) { ArgumentOutOfRangeException.ThrowIfNegative(value); borderWidth = value; InvalidatePaint(); return this; }
    /// <inheritdoc />
    protected override bool HandlesTap => true;
    /// <inheritdoc />
    protected override bool CanReceiveTap => command?.CanExecute(commandParameter) ?? true;
    /// <inheritdoc />
    protected override void OnPressedChanged() => UpdateState();
    private void UpdateState()
    {
        VisualStateManager.GoToState(this, !IsEnabled || !CanReceiveTap ? "Disabled" : IsPressed ? "Pressed" : "Normal");
        InvalidatePaint();
    }
    /// <inheritdoc />
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(IsEnabled)) UpdateState();
    }
    /// <inheritdoc />
    protected override void OnTapped(SkUiTappedEventArgs args)
    {
        base.OnTapped(args);
        Clicked?.Invoke(this, EventArgs.Empty);
        if (command?.CanExecute(commandParameter) == true) command.Execute(commandParameter);
    }
    /// <inheritdoc />
    protected override void OnPaintBackground(SKCanvas canvas)
    {
        var color = (Background as SolidColorBrush)?.Color ?? fillColor;
        if (!IsEnabled || !CanReceiveTap) color = Color.FromArgb("#596467");
        else if (IsPressed) color = color.MultiplyAlpha(0.75f);
        SkUiChrome.DrawRoundedBox(canvas, new SKRect(0, 0, (float)Width, (float)Height), (float)cornerRadius,
            ToSkColor(color), ToSkColor(borderColor), (float)borderWidth);
    }
}

internal static class SkUiChrome
{
    internal static void DrawRoundedBox(SKCanvas canvas, SKRect bounds, float radius, SKColor fill, SKColor border, float width)
    {
        using var paint = new SKPaint { Color = fill, IsAntialias = true };
        canvas.DrawRoundRect(bounds, radius, radius, paint);
        if (width <= 0) return;
        bounds.Inflate(-width / 2, -width / 2);
        paint.Color = border;
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = width;
        canvas.DrawRoundRect(bounds, Math.Max(0, radius - width / 2), Math.Max(0, radius - width / 2), paint);
    }
}