using System.Windows.Input;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>A drawn text button with intrinsic taps, commands, and press/disabled feedback.</summary>
public class SkUiButton : SkUiLabel
{
    private ICommand? _command;
    private object? _commandParameter;
    private double _cornerRadius = 6;
    private Color _fillColor = SkUiColors.Accent;
    private Color _borderColor = Colors.Transparent;
    private double _borderWidth;

    /// <summary>Bindable command executed on a valid release.</summary>
    public static readonly BindableProperty CommandProperty = BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(SkUiButton), null, propertyChanged: (view, _, value) => ((SkUiButton)view).SetCommand((ICommand?)value));
    /// <summary>Bindable command argument.</summary>
    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(SkUiButton), null, propertyChanged: (view, _, value) => ((SkUiButton)view).SetCommandParameter(value));
    /// <summary>Bindable rounded corner radius.</summary>
    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(nameof(CornerRadius), typeof(double), typeof(SkUiButton), 6d, propertyChanged: (view, _, value) => ((SkUiButton)view).SetCornerRadius((double)value));
    /// <summary>Bindable button fill.</summary>
    public static readonly BindableProperty FillColorProperty = BindableProperty.Create(nameof(FillColor), typeof(Color), typeof(SkUiButton), SkUiColors.Accent, propertyChanged: (view, _, value) => ((SkUiButton)view).SetFillColor((Color)value));
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
    public ICommand? Command { get => _command; set => SetValue(CommandProperty, value); }
    /// <summary>Command argument.</summary>
    public object? CommandParameter { get => _commandParameter; set => SetValue(CommandParameterProperty, value); }
    /// <summary>Rounded radius in DIPs; hit bounds remain rectangular.</summary>
    public double CornerRadius { get => _cornerRadius; set => SetValue(CornerRadiusProperty, value); }
    /// <summary>Button background fill.</summary>
    public Color FillColor { get => _fillColor; set => SetValue(FillColorProperty, value); }
    /// <summary>Border color.</summary>
    public Color BorderColor { get => _borderColor; set => SetValue(BorderColorProperty, value); }
    /// <summary>Border width in DIPs.</summary>
    public double BorderWidth { get => _borderWidth; set => SetValue(BorderWidthProperty, value); }

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
    /// <summary>Sets radius without bindable write-back.</summary>
    public SkUiButton SetCornerRadius(double value) { ArgumentOutOfRangeException.ThrowIfNegative(value); _cornerRadius = value; InvalidatePaint(); return this; }
    /// <summary>Sets fill without bindable write-back.</summary>
    public SkUiButton SetFillColor(Color value) { ArgumentNullException.ThrowIfNull(value); _fillColor = value; InvalidatePaint(); return this; }
    /// <summary>Sets border color without bindable write-back.</summary>
    public SkUiButton SetBorderColor(Color value) { ArgumentNullException.ThrowIfNull(value); _borderColor = value; InvalidatePaint(); return this; }
    /// <summary>Sets border width without bindable write-back.</summary>
    public SkUiButton SetBorderWidth(double value) { ArgumentOutOfRangeException.ThrowIfNegative(value); _borderWidth = value; InvalidatePaint(); return this; }
    /// <inheritdoc />
    protected override bool HandlesTap => true;
    /// <inheritdoc />
    protected override bool CanReceiveTap => _command?.CanExecute(_commandParameter) ?? true;
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
    /// <inheritdoc />
    protected override void OnPaintBackground(SKCanvas canvas)
    {
        var color = ResolveSolidBackgroundColor() ?? _fillColor;
        if (!IsEnabled || !CanReceiveTap) color = SkUiColors.Disabled;
        else if (IsPressed) color = color.MultiplyAlpha(0.75f);
        SkUiChrome.DrawRoundedBox(canvas, new SKRect(0, 0, (float)Width, (float)Height), (float)_cornerRadius,
            ToSkColor(color), ToSkColor(_borderColor), (float)_borderWidth);
    }

    /// <summary>Clips text to the same rounded-rect geometry as the background fill so glyphs never bleed past the corners.</summary>
    protected override void OnPaintContent(SKCanvas canvas)
    {
        var saveCount = canvas.Save();
        try
        {
            using var clip = SkUiChrome.CreateRoundRectPath(new SKRect(0, 0, (float)Width, (float)Height), (float)_cornerRadius);
            canvas.ClipPath(clip, antialias: true);
            base.OnPaintContent(canvas);
        }
        finally { canvas.RestoreToCount(saveCount); }
    }
}