using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// Drawn progress bar (Core analogue of <c>SkUiProgressBar</c>), with <see cref="IsIndeterminate"/>: a segment moves
/// along the bar on the render thread. Drawn by <see cref="SkUiLook.DrawProgressBar"/>.
/// </summary>
public class SkUiCoreProgressBar : SkUiCoreNode
{
    private double _progress;
    private bool _isIndeterminate;
    private Color _progressColor = SkUiColors.Accent;
    private Color _trackColor = SkUiColors.TrackOff;
    private SkUiProgressTween? _tween;
    private SkUiTween? _fill;
    private SKPath? _clip;
    private SKSize _clipSize;

    /// <summary>Completed fraction, 0–1 (clamped).</summary>
    public double Progress { get => _progress; set => SetProgress(value); }

    /// <summary>Shows activity without a known amount: a segment moves along the bar and <see cref="Progress"/> is not drawn.</summary>
    public bool IsIndeterminate { get => _isIndeterminate; set => SetIsIndeterminate(value); }

    /// <summary>Fill (and moving segment) color.</summary>
    public Color ProgressColor { get => _progressColor; set => SetProgressColor(value); }

    /// <summary>Track color behind the fill.</summary>
    public Color TrackColor { get => _trackColor; set => SetTrackColor(value); }

    /// <summary>Sets the progress (clamped).</summary>
    public SkUiCoreProgressBar SetProgress(double value)
    {
        var old = _progress;
        if (SetProperty(ref _progress, SkUiProgressTween.Clamp(value), nameof(Progress)))
        {
            SkUiProgressBarDrawing.ProgressChanged(this, ref _fill, old, _progress, _tween);
            InvalidatePaint();
        }
        return this;
    }

    /// <summary>Sets <see cref="IsIndeterminate"/>.</summary>
    public SkUiCoreProgressBar SetIsIndeterminate(bool value)
    {
        if (SetProperty(ref _isIndeterminate, value, nameof(IsIndeterminate)))
            InvalidatePaint();
        return this;
    }

    /// <summary>Sets the progress color.</summary>
    public SkUiCoreProgressBar SetProgressColor(Color value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (SetProperty(ref _progressColor, value, nameof(ProgressColor)))
            InvalidatePaint();
        return this;
    }

    /// <summary>Sets the track color.</summary>
    public SkUiCoreProgressBar SetTrackColor(Color value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (SetProperty(ref _trackColor, value, nameof(TrackColor)))
            InvalidatePaint();
        return this;
    }

    /// <summary>
    /// Animates <see cref="Progress"/> to <paramref name="value"/> over <paramref name="length"/> ms. Returns
    /// <c>true</c> when it ran to completion; <c>false</c> when a newer call replaced it or the animation was stopped
    /// (e.g. the page closed). It pauses while the node is detached and continues once it is in a host tree again.
    /// </summary>
    public Task<bool> ProgressTo(double value, uint length = 250, Easing? easing = null)
    {
        if (_fill is { IsRunning: true } fill)
        {
            fill.Jump((float)_progress); // ProgressTo owns Progress now: no smoothing on top
            InvalidatePaint();
        }
        return (_tween ??= new SkUiProgressTween(progress => SetProgress(progress))).Start(AnimationClock, _progress, value, length, easing);
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
    protected override void OnPaintContent(SKCanvas canvas) =>
        SkUiProgressBarDrawing.Draw(canvas, (float)Frame.Width, (float)Frame.Height, IsRightToLeft, SkUiProgressBarDrawing.Drawn(_fill, _progress), _isIndeterminate,
            ToSkColor(_trackColor), ToSkColor(_progressColor), enabled: true);

    /// <inheritdoc />
    internal override void OnGetRenderProps(ref SkUiRenderProps props) =>
        SkUiProgressBarDrawing.SetSlide(ref props, _isIndeterminate, IsRightToLeft, ref _clip, ref _clipSize);

    /// <inheritdoc />
    protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
    {
        base.OnPopulateSemantics(info);
        info.Role = SkUiSemanticsRole.ProgressBar;
        if (!_isIndeterminate)
            info.Range = new SkUiSemanticsRange(0, 1, _progress);
    }
}
