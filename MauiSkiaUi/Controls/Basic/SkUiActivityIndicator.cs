using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>A drawn indeterminate spinner, similar to MAUI's ActivityIndicator.</summary>
public class SkUiActivityIndicator : SkUiView
{
    private bool _isRunning;
    private Color _color = Colors.Gray;
    private IDisposable? _spin;
    private float _sweepStart;
    private SKPaint? _strokePaint;

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
        BindSpin();
        if (!_isRunning) InvalidatePaint();
        return this;
    }
    /// <summary>Sets color without bindable write-back.</summary>
    public SkUiActivityIndicator SetColor(Color value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _color = value;
        if (_strokePaint is not null)
            _strokePaint.Color = ToSkColor(value);
        InvalidatePaint();
        return this;
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => new(36, 36);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        if (!_isRunning || Width <= 0 || Height <= 0) return;
        var strokeWidth = (float)Math.Max(2, Math.Min(Width, Height) * 0.1);
        var bounds = new SKRect(strokeWidth / 2, strokeWidth / 2, (float)Width - strokeWidth / 2, (float)Height - strokeWidth / 2);
        var paint = _strokePaint ??= new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            IsAntialias = true,
            Color = ToSkColor(_color)
        };
        paint.StrokeWidth = strokeWidth;
        canvas.DrawArc(bounds, _sweepStart, 270, false, paint);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(IsVisible) && !IsVisible) SetIsRunning(false);
    }

    /// <summary>
    /// Stops when detached; rebinds the spin callback onto the current
    /// <see cref="SkUiView.AnimationClock"/> when the shared root changes (e.g. <c>IsRunning</c>
    /// was set before the control joined its surface-owning ancestor).
    /// </summary>
    protected override void OnAnimationRootChanged()
    {
        if (Parent is null)
        {
            SetIsRunning(false);
            return;
        }
        if (_isRunning)
            BindSpin();
    }

    /// <summary>Releases the cached stroke paint when removed from the tree.</summary>
    protected override void OnParentSet()
    {
        base.OnParentSet();
        if (Parent is null)
        {
            _strokePaint?.Dispose();
            _strokePaint = null;
        }
    }

    /// <summary>
    /// Registers or clears the repeating clock callback on the current animation root.
    /// Mutates sweep angle only; the root handler coalesces one <see cref="SkUiView.InvalidatePaint"/> per tick
    /// so hundreds of spinners do not each bubble paint invalidation.
    /// </summary>
    private void BindSpin()
    {
        _spin?.Dispose();
        _spin = null;
        if (!_isRunning)
            return;
        _spin = AnimationClock.Start(
            progress => _sweepStart = (float)(progress * 360),
            TimeSpan.FromSeconds(1),
            null,
            repeat: true);
        // Mark ancestors dirty once so scroll content caches switch to live paint while the clock runs.
        InvalidatePaint();
    }
}
