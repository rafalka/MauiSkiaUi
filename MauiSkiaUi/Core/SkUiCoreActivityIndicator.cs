using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>Indeterminate spinner (Core analogue of <c>SkUiActivityIndicator</c>).</summary>
public class SkUiCoreActivityIndicator : SkUiCoreNode
{
    private bool _isRunning;
    private Color _color = SkUiColors.Muted;
    private IDisposable? _spin;
    private float _sweepStart;
    private SKPaint? _strokePaint;

    /// <summary>Whether the spinner is animating.</summary>
    public bool IsRunning
    {
        get => _isRunning;
        set => SetIsRunning(value);
    }

    /// <summary>Spinner stroke color.</summary>
    public Color Color
    {
        get => _color;
        set => SetColor(value);
    }

    /// <summary>Sets running state; animates only while <c>true</c>.</summary>
    public SkUiCoreActivityIndicator SetIsRunning(bool value)
    {
        if (!SetProperty(ref _isRunning, value, nameof(IsRunning))) return this;
        BindSpin();
        if (!_isRunning) InvalidatePaint();
        return this;
    }

    /// <summary>Sets the spinner stroke color.</summary>
    public SkUiCoreActivityIndicator SetColor(Color value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!SetProperty(ref _color, value, nameof(Color))) return this;
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
        var paint = _strokePaint ??= new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            IsAntialias = true,
            Color = ToSkColor(_color)
        };
        SkUiLook.Current.DrawActivityIndicator(canvas, (float)Frame.Width, (float)Frame.Height, _sweepStart, paint);
    }

    /// <inheritdoc />
    protected override void OnIsVisibleChanged()
    {
        if (!IsVisible)
            SetIsRunning(false);
    }

    /// <inheritdoc />
    protected override void OnAnimationRootChanged()
    {
        if (_isRunning)
            BindSpin();
    }

    private void BindSpin()
    {
        _spin?.Dispose();
        _spin = null;
        if (!_isRunning || !IsVisible)
            return;
        _spin = AnimationClock.Start(
            progress => _sweepStart = (float)(progress * 360),
            TimeSpan.FromSeconds(1),
            repeat: true);
        // Mark ancestors dirty once so scroll content caches switch to live paint while the clock runs.
        InvalidatePaint();
    }
}
