using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>A drawn indeterminate spinner, similar to MAUI's ActivityIndicator.</summary>
public class SkUiActivityIndicator : SkUiView
{
    private bool _isRunning;
    private Color _color = SkUiColors.Muted;
    private IDisposable? _spin;
    private float _sweepStart;
    private SKPaint? _strokePaint;

    /// <summary>Bindable running state; animates only while true.</summary>
    public static readonly BindableProperty IsRunningProperty = BindableProperty.Create(nameof(IsRunning), typeof(bool), typeof(SkUiActivityIndicator), false,
        propertyChanged: (view, _, value) => ((SkUiActivityIndicator)view).SetIsRunning((bool)value));
    /// <summary>Bindable spinner color.</summary>
    public static readonly BindableProperty ColorProperty = BindableProperty.Create(nameof(Color), typeof(Color), typeof(SkUiActivityIndicator), null,
        defaultValueCreator: _ => SkUiColors.Muted,
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
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        SkUiLook.Current.MeasureActivityIndicator(widthConstraint, heightConstraint);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        if (!_isRunning) return;
        // Recover when StopAll cleared clock registrations but IsRunning stayed true (e.g. older
        // Unloaded handlers, or host recreate that stopped the clock without clearing intent).
        if (!AnimationClock.IsRunning)
        {
            BindSpin();
            if (!AnimationClock.IsRunning)
                return;
        }
        var paint = _strokePaint ??= new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            IsAntialias = true,
            Color = ToSkColor(_color)
        };
        SkUiLook.Current.DrawActivityIndicator(canvas, (float)Width, (float)Height, _sweepStart, paint);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(IsVisible) && !IsVisible) SetIsRunning(false);
    }

    /// <summary>
    /// Unbinds the spin callback when this control or an ancestor subtree is detached; keeps
    /// <see cref="IsRunning"/> so a temporary rehost (e.g. HwAccelerated host recreate) can resume.
    /// Rebinds onto the current <see cref="SkUiView.AnimationClock"/> when the shared root changes
    /// while still running (e.g. <c>IsRunning</c> was set before the control joined its surface-owning ancestor).
    /// </summary>
    protected override void OnAnimationRootChanged(bool subtreeDetached = false)
    {
        if (Parent is null || subtreeDetached)
        {
            _spin?.Dispose();
            _spin = null;
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
