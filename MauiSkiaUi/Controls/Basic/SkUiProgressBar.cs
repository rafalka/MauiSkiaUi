using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// A drawn progress bar, similar to MAUI's ProgressBar, plus <see cref="IsIndeterminate"/>: a segment moves along the
/// bar on the render thread (like <see cref="SkUiActivityIndicator"/>'s spin), so it keeps moving while the UI thread
/// is busy. Drawn by <see cref="SkUiLook.DrawProgressBar"/>; right-to-left layouts fill and move from the right.
/// </summary>
public class SkUiProgressBar : SkUiView
{
    private double _progress;
    private bool _isIndeterminate;
    private Color _progressColor = SkUiColors.Accent;
    private Color _trackColor = SkUiColors.TrackOff;
    private IDisposable? _progressAnimation;
    private TaskCompletionSource<bool>? _progressCompletion;
    private SKPath? _clip;
    private SKSize _clipSize;

    /// <summary>Bindable <see cref="Progress"/>.</summary>
    public static readonly BindableProperty ProgressProperty = BindableProperty.Create(nameof(Progress), typeof(double), typeof(SkUiProgressBar), 0d,
        coerceValue: (_, value) => Math.Clamp((double)value, 0, 1),
        propertyChanged: (view, _, value) => ((SkUiProgressBar)view).SetProgress((double)value));

    /// <summary>Bindable <see cref="IsIndeterminate"/>.</summary>
    public static readonly BindableProperty IsIndeterminateProperty = BindableProperty.Create(nameof(IsIndeterminate), typeof(bool), typeof(SkUiProgressBar), false,
        propertyChanged: (view, _, value) => ((SkUiProgressBar)view).SetIsIndeterminate((bool)value));

    /// <summary>Bindable <see cref="ProgressColor"/>.</summary>
    public static readonly BindableProperty ProgressColorProperty = BindableProperty.Create(nameof(ProgressColor), typeof(Color), typeof(SkUiProgressBar), null,
        defaultValueCreator: _ => SkUiColors.Accent, propertyChanged: (view, _, value) => ((SkUiProgressBar)view).SetProgressColor((Color)value));

    /// <summary>Bindable <see cref="TrackColor"/>.</summary>
    public static readonly BindableProperty TrackColorProperty = BindableProperty.Create(nameof(TrackColor), typeof(Color), typeof(SkUiProgressBar), null,
        defaultValueCreator: _ => SkUiColors.TrackOff, propertyChanged: (view, _, value) => ((SkUiProgressBar)view).SetTrackColor((Color)value));

    /// <summary>Completed fraction, 0–1 (clamped).</summary>
    public double Progress { get => _progress; set => SetValue(ProgressProperty, value); }

    /// <summary>Shows activity without a known amount: a segment moves along the bar and <see cref="Progress"/> is not drawn.</summary>
    public bool IsIndeterminate { get => _isIndeterminate; set => SetValue(IsIndeterminateProperty, value); }

    /// <summary>Fill (and moving segment) color.</summary>
    public Color ProgressColor { get => _progressColor; set => SetValue(ProgressColorProperty, value); }

    /// <summary>Track color behind the fill.</summary>
    public Color TrackColor { get => _trackColor; set => SetValue(TrackColorProperty, value); }

    /// <summary>Sets the progress (clamped) without bindable write-back.</summary>
    public SkUiProgressBar SetProgress(double value)
    {
        value = Math.Clamp(value, 0, 1);
        if (_progress == value) return this;
        _progress = value;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets <see cref="IsIndeterminate"/> without bindable write-back.</summary>
    public SkUiProgressBar SetIsIndeterminate(bool value)
    {
        if (_isIndeterminate == value) return this;
        _isIndeterminate = value;
        InvalidatePaint();
        InvalidateRender(SkUiRenderDirty.Props);
        return this;
    }

    /// <summary>Sets the progress color without bindable write-back.</summary>
    public SkUiProgressBar SetProgressColor(Color value) { ArgumentNullException.ThrowIfNull(value); _progressColor = value; InvalidatePaint(); return this; }

    /// <summary>Sets the track color without bindable write-back.</summary>
    public SkUiProgressBar SetTrackColor(Color value) { ArgumentNullException.ThrowIfNull(value); _trackColor = value; InvalidatePaint(); return this; }

    /// <summary>
    /// Animates <see cref="Progress"/> to <paramref name="value"/> over <paramref name="length"/> ms, like MAUI's
    /// <c>ProgressTo</c>. Returns <c>true</c> when it ran to completion; <c>false</c> when a newer call replaced it.
    /// </summary>
    public Task<bool> ProgressTo(double value, uint length = 250, Easing? easing = null)
    {
        CancelProgressAnimation();
        var from = _progress;
        var to = Math.Clamp(value, 0, 1);
        var curve = easing ?? Easing.Linear;
        var completion = _progressCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        // A linear clock with the easing applied here: completion is exactly t >= 1, whatever the easing overshoots.
        _progressAnimation = AnimationClock.Start(t =>
        {
            Progress = from + (to - from) * curve.Ease(Math.Min(t, 1));
            if (t >= 1 && ReferenceEquals(_progressCompletion, completion))
            {
                _progressAnimation = null;
                _progressCompletion = null;
                completion.TrySetResult(true);
            }
        }, TimeSpan.FromMilliseconds(Math.Max(1, length)));
        return completion.Task;
    }

    private void CancelProgressAnimation()
    {
        _progressAnimation?.Dispose();
        _progressAnimation = null;
        _progressCompletion?.TrySetResult(false);
        _progressCompletion = null;
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        SkUiLook.Current.MeasureProgressBar(widthConstraint, heightConstraint);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        var track = _trackColor;
        var fill = _progressColor;
        if (!IsEnabled)
        {
            track = track.MultiplyAlpha(0.5f);
            fill = fill.MultiplyAlpha(0.5f);
        }
        SkUiProgressBarDrawing.Draw(canvas, (float)Width, (float)Height, IsRightToLeft, (float)_progress, _isIndeterminate,
            ToSkColor(track), ToSkColor(fill), IsEnabled);
    }

    /// <inheritdoc />
    internal override void OnGetRenderProps(ref SkUiRenderProps props) =>
        SkUiProgressBarDrawing.SetSlide(ref props, _isIndeterminate, IsRightToLeft, ref _clip, ref _clipSize);

    /// <inheritdoc />
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(FlowDirection))
            InvalidateRender(SkUiRenderDirty.Props);
    }
}

/// <summary>Drawing and render-thread slide shared by <see cref="SkUiProgressBar"/> and <see cref="Core.SkUiCoreProgressBar"/>.</summary>
internal static class SkUiProgressBarDrawing
{
    public static void Draw(SKCanvas canvas, float width, float height, bool rightToLeft, float progress, bool indeterminate,
        SKColor track, SKColor fill, bool enabled)
    {
        var look = SkUiLook.Current;
        var save = canvas.Save();
        if (rightToLeft)
            canvas.Scale(-1, 1, width / 2, 0);
        look.DrawProgressBar(canvas, new SkUiProgressBarPaint(new SKRect(0, 0, width, height), progress, indeterminate, track, fill,
            look.IndeterminateProgressSegment, enabled));
        canvas.RestoreToCount(save);
    }

    /// <summary>Indeterminate: the compositor slides the recorded track + segment across the bar, clipped to its shape.</summary>
    public static void SetSlide(ref SkUiRenderProps props, bool indeterminate, bool rightToLeft, ref SKPath? clip, ref SKSize clipSize)
    {
        if (!indeterminate || props.Width <= 0 || props.Height <= 0)
            return;
        var look = SkUiLook.Current;
        props.ContentSlidePeriod = look.IndeterminateProgressPeriod;
        // One bar width per loop: the recorded bar tiles seamlessly, and the segment wraps around the ends.
        props.ContentSlideDistance = rightToLeft ? -props.Width : props.Width;
        var size = new SKSize(props.Width, props.Height);
        if (clip is null || clipSize != size)
        {
            // Not disposed: a committed frame may still clip with the previous path (the GC frees it).
            clip = look.CreateRoundRectPath(new SKRect(0, 0, props.Width, props.Height), look.GetProgressBarCornerRadius(props.Height));
            clipSize = size;
        }
        props.ContentClipPath = clip;
    }
}
