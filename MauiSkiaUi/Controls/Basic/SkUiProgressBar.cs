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
    private SkUiProgressTween? _tween;
    private SkUiTween? _fill;
    private SKPath? _clip;
    private SKSize _clipSize;

    /// <summary>Bindable <see cref="Progress"/>.</summary>
    public static readonly BindableProperty ProgressProperty = BindableProperty.Create(nameof(Progress), typeof(double), typeof(SkUiProgressBar), 0d,
        coerceValue: (_, value) => SkUiProgressTween.Clamp((double)value),
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

    /// <summary>Completed fraction, 0–1 (clamped; NaN becomes 0).</summary>
    public double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }

    /// <summary>Shows activity without a known amount: a segment moves along the bar and <see cref="Progress"/> is not drawn.</summary>
    public bool IsIndeterminate { get => (bool)GetValue(IsIndeterminateProperty); set => SetValue(IsIndeterminateProperty, value); }

    /// <summary>Fill (and moving segment) color.</summary>
    public Color ProgressColor { get => (Color)GetValue(ProgressColorProperty); set => SetValue(ProgressColorProperty, value); }

    /// <summary>Track color behind the fill.</summary>
    public Color TrackColor { get => (Color)GetValue(TrackColorProperty); set => SetValue(TrackColorProperty, value); }

    /// <summary>Sets the progress (clamped) (same as the property setter).</summary>
    public SkUiProgressBar SetProgress(double value)
    {
        value = SkUiProgressTween.Clamp(value);
        if (WriteBindable(ProgressProperty, value)) return this;
        if (_progress == value) return this;
        SkUiProgressBarDrawing.ProgressChanged(this, ref _fill, _progress, value, _tween);
        _progress = value;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets <see cref="IsIndeterminate"/> (same as the property setter).</summary>
    public SkUiProgressBar SetIsIndeterminate(bool value)
    {
        if (WriteBindable(IsIndeterminateProperty, value)) return this;
        if (_isIndeterminate == value) return this;
        _isIndeterminate = value;
        InvalidatePaint();
        InvalidateRender(SkUiRenderDirty.Props);
        return this;
    }

    /// <summary>Sets the progress color (same as the property setter).</summary>
    public SkUiProgressBar SetProgressColor(Color value) { ArgumentNullException.ThrowIfNull(value); if (WriteBindable(ProgressColorProperty, value)) return this; _progressColor = value; InvalidatePaint(); return this; }

    /// <summary>Sets the track color (same as the property setter).</summary>
    public SkUiProgressBar SetTrackColor(Color value) { ArgumentNullException.ThrowIfNull(value); if (WriteBindable(TrackColorProperty, value)) return this; _trackColor = value; InvalidatePaint(); return this; }

    /// <summary>
    /// Animates <see cref="Progress"/> to <paramref name="value"/> over <paramref name="length"/> ms, like MAUI's
    /// <c>ProgressTo</c>. Returns <c>true</c> when it ran to completion; <c>false</c> when a newer call replaced it or
    /// the animation was stopped (e.g. the page closed). It pauses while the bar is detached and continues once the bar
    /// is in a tree again (also when called before the bar was added).
    /// </summary>
    public Task<bool> ProgressTo(double value, uint length = 250, Easing? easing = null)
    {
        if (_fill is { IsRunning: true } fill)
        {
            fill.Jump((float)_progress); // ProgressTo owns Progress now: no smoothing on top
            InvalidatePaint();
        }
        return (_tween ??= new SkUiProgressTween(progress => Progress = progress)).Start(AnimationClock, _progress, value, length, easing);
    }

    /// <inheritdoc />
    protected override void OnAnimationRootChanged(bool subtreeDetached = false)
    {
        base.OnAnimationRootChanged(subtreeDetached);
        _tween?.Rebind(AnimationClock, subtreeDetached);
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
        SkUiProgressBarDrawing.Draw(canvas, (float)Width, (float)Height, IsRightToLeft, SkUiProgressBarDrawing.Drawn(_fill, _progress), _isIndeterminate,
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
    /// <summary>
    /// <c>Progress</c> changed: the drawn fill follows with the look's <see cref="SkUiTransitionKind.Progress"/>
    /// transition (none by default), except while <c>ProgressTo</c> animates it.
    /// </summary>
    public static void ProgressChanged(ISkUiTransitionHost host, ref SkUiTween? fill, double from, double to, SkUiProgressTween? progressTo)
    {
        var transition = progressTo?.IsRunning == true ? SkUiTransition.None : SkUiLook.Current.GetTransition(SkUiTransitionKind.Progress);
        if (transition.IsNone)
        {
            fill?.Jump((float)to);
            return;
        }
        fill ??= new SkUiTween(host);
        if (!fill.IsRunning)
            fill.Jump((float)from);
        fill.AnimateTo((float)to, transition);
    }

    /// <summary>The fill to draw for <paramref name="progress"/>.</summary>
    public static float Drawn(SkUiTween? fill, double progress) => fill is { IsRunning: true } ? fill.Value : (float)progress;

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

/// <summary>
/// <c>ProgressTo</c> for <see cref="SkUiProgressBar"/> / <see cref="Core.SkUiCoreProgressBar"/>: one tween at a time on
/// the UI-thread animation clock. <see cref="Rebind"/> moves a running tween to another clock (the bar moved to another
/// surface), keeping its curve and remaining time; while the bar is detached, the tween pauses.
/// </summary>
internal sealed class SkUiProgressTween(Action<double> setProgress)
{
    /// <summary>Whether a <c>ProgressTo</c> is in progress.</summary>
    public bool IsRunning => _completion is not null;

    private TaskCompletionSource<bool>? _completion;
    private IDisposable? _handle;
    private double _from;
    private double _to;
    private double _position;
    private double _lengthMs;
    private Easing _easing = Easing.Linear;

    /// <summary>0–1; NaN (which <see cref="Math.Clamp(double, double, double)"/> passes through) becomes 0.</summary>
    public static double Clamp(double value) => double.IsNaN(value) ? 0 : Math.Clamp(value, 0, 1);

    public Task<bool> Start(SkUiAnimationClock clock, double from, double to, uint length, Easing? easing)
    {
        Finish(false);
        _from = from;
        _to = Clamp(to);
        _easing = easing ?? Easing.Linear;
        _lengthMs = Math.Max(1, length);
        _position = 0;
        var completion = _completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Run(clock);
        return completion.Task;
    }

    public void Rebind(SkUiAnimationClock clock, bool detached)
    {
        if (_completion is null)
            return;
        if (detached)
        {
            var handle = _handle;
            _handle = null;
            handle?.Dispose(); // paused: its stopped callback sees it is no longer current
        }
        else
            Run(clock);
    }

    private void Run(SkUiAnimationClock clock)
    {
        var previous = _handle;
        _handle = null;
        previous?.Dispose(); // its stopped callback sees it is no longer current
        var start = _position;
        IDisposable? handle = null;
        // A linear clock with the easing applied here: completion is exactly t >= 1, whatever the easing overshoots.
        handle = clock.Start(t =>
        {
            _position = start + (1 - start) * Math.Min(t, 1);
            setProgress(_from + (_to - _from) * _easing.Ease(_position));
            if (t >= 1 && handle is not null && ReferenceEquals(_handle, handle))
            {
                _handle = null;
                Finish(true);
            }
        }, TimeSpan.FromMilliseconds(Math.Max(1, _lengthMs * (1 - start))), easing: null, repeat: false, stopped: () =>
        {
            if (handle is not null && ReferenceEquals(_handle, handle))
            {
                _handle = null;
                Finish(false);
            }
        });
        _handle = handle;
    }

    private void Finish(bool result)
    {
        var completion = _completion;
        _completion = null;
        var handle = _handle;
        _handle = null;
        handle?.Dispose();
        completion?.TrySetResult(result);
    }
}
