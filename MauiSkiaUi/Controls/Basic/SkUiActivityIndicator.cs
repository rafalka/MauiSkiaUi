using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// A drawn indeterminate spinner, similar to MAUI's ActivityIndicator. The arc is recorded once and rotated
/// by the compositor on the render thread (one revolution per second), so it keeps spinning while the UI
/// thread is busy and costs no re-recording per frame.
/// </summary>
public class SkUiActivityIndicator : SkUiView
{
    private bool _isRunning;
    private Color _color = SkUiColors.Muted;
    private SKPaint? _strokePaint;

    /// <summary>Bindable running state; animates only while true.</summary>
    public static readonly BindableProperty IsRunningProperty = BindableProperty.Create(nameof(IsRunning), typeof(bool), typeof(SkUiActivityIndicator), false,
        propertyChanged: (view, _, value) => ((SkUiActivityIndicator)view).OnIsRunningChanged((bool)value));
    /// <summary>Bindable spinner color.</summary>
    public static readonly BindableProperty ColorProperty = BindableProperty.Create(nameof(Color), typeof(Color), typeof(SkUiActivityIndicator), null,
        defaultValueCreator: _ => SkUiColors.Muted,
        validateValue: SkUiValidate.NotNull,
        propertyChanged: (view, _, value) => ((SkUiActivityIndicator)view).OnColorChanged((Color)value));

    /// <summary>Whether the spinner is animating.</summary>
    public bool IsRunning { get => (bool)GetValue(IsRunningProperty); set => SetValue(IsRunningProperty, value); }
    /// <summary>Spinner stroke color.</summary>
    public Color Color { get => (Color)GetValue(ColorProperty); set => SetValue(ColorProperty, value); }

    /// <summary>Sets running state (same as the property setter).</summary>
    public SkUiActivityIndicator SetIsRunning(bool value)
    {
        IsRunning = value;
        return this;
    }

    private void OnIsRunningChanged(bool value)
    {
        if (_isRunning == value) return;
        _isRunning = value;
        InvalidatePaint();
    }
    /// <summary>Sets color (same as the property setter).</summary>
    public SkUiActivityIndicator SetColor(Color value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Color = value;
        return this;
    }

    private void OnColorChanged(Color value)
    {
        _color = value;
        if (_strokePaint is not null)
            _strokePaint.Color = ToSkColor(value);
        InvalidatePaint();
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
        SkUiLook.Current.DrawActivityIndicator(canvas, (float)Width, (float)Height, 0, paint);
    }

    /// <inheritdoc />
    internal override void ReleaseDrawingResources()
    {
        base.ReleaseDrawingResources();
        // Recording copied it into the picture; the next record makes it again.
        _strokePaint?.Dispose();
        _strokePaint = null;
    }

    /// <inheritdoc />
    internal override void OnGetRenderProps(ref SkUiRenderProps props) =>
        props.ContentSpinPeriod = _isRunning ? 1 : 0;

    /// <inheritdoc />
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(IsVisible) && !IsVisible) SetIsRunning(false);
    }

    /// <inheritdoc />
    protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
    {
        base.OnPopulateSemantics(info);
        // Running: busy, read like a native indeterminate progress indicator; stopped, it shows nothing.
        if (_isRunning)
            info.Role = SkUiSemanticsRole.ProgressBar;
    }
}
