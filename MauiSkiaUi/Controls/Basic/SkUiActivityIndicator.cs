using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>A drawn indeterminate spinner, similar to MAUI's ActivityIndicator.</summary>
public class SkUiActivityIndicator : SkUiView
{
    private bool _isRunning;
    private Color _color = Colors.Gray;
    private IDisposable? _spin;
    private float _sweepStart;

    /// <summary>Bindable running state; animates only while true.</summary>
    public static readonly BindableProperty IsRunningProperty = BindableProperty.Create(nameof(IsRunning), typeof(bool), typeof(SkUiActivityIndicator), false,
        propertyChanged: (view, _, value) => ((SkUiActivityIndicator)view).SetIsRunning((bool)value));
    /// <summary>Bindable spinner color.</summary>
    public static readonly BindableProperty ColorProperty = BindableProperty.Create(nameof(Color), typeof(Color), typeof(SkUiActivityIndicator), Colors.Gray,
        propertyChanged: (view, _, value) => ((SkUiActivityIndicator)view).SetColor((Color)value));

    /// <summary>Whether the spinner is animating.</summary>
    public bool IsRunning { get => _isRunning; set => SetValue(IsRunningProperty, value); }
    /// <summary>Spinner stroke color.</summary>
    public Color Color { get => _color; set => SetValue(ColorProperty, value); }

    /// <summary>Sets running state without bindable write-back.</summary>
    public SkUiActivityIndicator SetIsRunning(bool value)
    {
        if (_isRunning == value) return this;
        _isRunning = value;
        _spin?.Dispose();
        _spin = _isRunning ? AnimationClock.Start(progress => { _sweepStart = (float)(progress * 360); InvalidatePaint(); }, TimeSpan.FromSeconds(1), null, repeat: true) : null;
        if (!_isRunning) InvalidatePaint();
        return this;
    }
    /// <summary>Sets color without bindable write-back.</summary>
    public SkUiActivityIndicator SetColor(Color value) { ArgumentNullException.ThrowIfNull(value); _color = value; InvalidatePaint(); return this; }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => new(36, 36);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        if (!_isRunning || Width <= 0 || Height <= 0) return;
        var strokeWidth = (float)Math.Max(2, Math.Min(Width, Height) * 0.1);
        var bounds = new SKRect(strokeWidth / 2, strokeWidth / 2, (float)Width - strokeWidth / 2, (float)Height - strokeWidth / 2);
        using var paint = new SKPaint { Color = ToSkColor(_color), Style = SKPaintStyle.Stroke, StrokeWidth = strokeWidth, StrokeCap = SKStrokeCap.Round, IsAntialias = true };
        using var builder = new SKPathBuilder();
        builder.AddArc(bounds, _sweepStart, 270);
        using var path = builder.Detach();
        canvas.DrawPath(path, paint);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(IsVisible) && !IsVisible) SetIsRunning(false);
    }

    /// <summary>Stops the animation clock when removed from its tree, so a detached spinner cannot keep ticking.</summary>
    protected override void OnParentSet()
    {
        base.OnParentSet();
        if (Parent is null) SetIsRunning(false);
    }
}
