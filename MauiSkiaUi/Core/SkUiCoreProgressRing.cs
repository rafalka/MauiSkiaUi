using MauiSkiaUi.Rendering;
using Microsoft.Maui.Controls.Shapes;
using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// Drawn circular progress indicator (Core analogue of <c>SkUiProgressRing</c>): an arc of <see cref="Progress"/> over a
/// track, with an optional <see cref="SkUiCoreContentView.Content"/> node laid out in the square inscribed in the inner
/// circle (<see cref="SkUiCoreContentView.Padding"/> insets it further). <see cref="IsIndeterminate"/> turns an arc on the
/// render thread while the content stays put. Drawn by <see cref="SkUiLook.DrawProgressRing"/>.
/// </summary>
public class SkUiCoreProgressRing : SkUiCoreContentView
{
    private double _progress;
    private bool _isIndeterminate;
    private Color _progressColor = SkUiColors.Accent;
    private Color _trackColor = SkUiColors.TrackOff;
    private double _thickness = -1;
    private double _startAngle = -90;
    private bool _isClockwise = true;
    private PenLineCap _lineCap = PenLineCap.Round;
    private SkUiProgressTween? _tween;
    private SkUiTween? _fill;

    /// <summary>Completed fraction, 0–1 (clamped).</summary>
    public double Progress { get => _progress; set => SetProgress(value); }

    /// <summary>Shows activity without a known amount: an arc turns around the ring and <see cref="Progress"/> is not drawn.</summary>
    public bool IsIndeterminate { get => _isIndeterminate; set => SetIsIndeterminate(value); }

    /// <summary>Arc color.</summary>
    public Color ProgressColor { get => _progressColor; set => SetProgressColor(value); }

    /// <summary>Track color around the rest of the circle (transparent: no track).</summary>
    public Color TrackColor { get => _trackColor; set => SetTrackColor(value); }

    /// <summary>Stroke thickness in DIPs (default -1: the look's <see cref="SkUiLook.ProgressRingThickness"/>).</summary>
    public double Thickness { get => _thickness; set => SetThickness(value); }

    /// <summary>Where the progress starts, in degrees clockwise from 3 o'clock (default -90: the top).</summary>
    public double StartAngle { get => _startAngle; set => SetStartAngle(value); }

    /// <summary>Whether the progress grows clockwise (default <c>true</c>).</summary>
    public bool IsClockwise { get => _isClockwise; set => SetIsClockwise(value); }

    /// <summary>The ends of the progress arc (default <see cref="PenLineCap.Round"/>).</summary>
    public PenLineCap LineCap { get => _lineCap; set => SetLineCap(value); }

    /// <summary>Sets the progress (clamped).</summary>
    public SkUiCoreProgressRing SetProgress(double value)
    {
        var old = _progress;
        if (!SetProperty(ref _progress, SkUiProgressTween.Clamp(value), nameof(Progress)))
            return this;
        if (_isIndeterminate)
            _fill?.Jump((float)_progress); // not drawn: nothing to re-record (turning indeterminate off repaints)
        else
        {
            SkUiProgressBarDrawing.ProgressChanged(this, ref _fill, old, _progress, _tween);
            InvalidatePaint();
        }
        return this;
    }

    /// <summary>Sets <see cref="IsIndeterminate"/>.</summary>
    public SkUiCoreProgressRing SetIsIndeterminate(bool value)
    {
        if (SetProperty(ref _isIndeterminate, value, nameof(IsIndeterminate)))
        {
            InvalidatePaint();
            InvalidateRender(SkUiRenderDirty.Props);
        }
        return this;
    }

    /// <summary>Sets the progress color.</summary>
    public SkUiCoreProgressRing SetProgressColor(Color value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (SetProperty(ref _progressColor, value, nameof(ProgressColor)))
            InvalidatePaint();
        return this;
    }

    /// <summary>Sets the track color.</summary>
    public SkUiCoreProgressRing SetTrackColor(Color value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (SetProperty(ref _trackColor, value, nameof(TrackColor)))
            InvalidatePaint();
        return this;
    }

    /// <summary>Sets <see cref="Thickness"/>: DIPs, or -1 for the look's.</summary>
    public SkUiCoreProgressRing SetThickness(double value)
    {
        SkUiValidate.ThrowIfNotNonNegativeOrLook(value, nameof(value));
        if (SetProperty(ref _thickness, value, nameof(Thickness)))
        {
            InvalidateMeasure();
            InvalidatePaint();
        }
        return this;
    }

    /// <summary>Sets <see cref="StartAngle"/> in degrees.</summary>
    public SkUiCoreProgressRing SetStartAngle(double value)
    {
        SkUiValidate.ThrowIfNotFinite(value, nameof(value));
        if (SetProperty(ref _startAngle, value, nameof(StartAngle)))
            InvalidatePaint();
        return this;
    }

    /// <summary>Sets <see cref="IsClockwise"/>.</summary>
    public SkUiCoreProgressRing SetIsClockwise(bool value)
    {
        if (SetProperty(ref _isClockwise, value, nameof(IsClockwise)))
            InvalidatePaint();
        return this;
    }

    /// <summary>Sets <see cref="LineCap"/>.</summary>
    public SkUiCoreProgressRing SetLineCap(PenLineCap value)
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value), value, "Not a defined line cap.");
        if (SetProperty(ref _lineCap, value, nameof(LineCap)))
            InvalidatePaint();
        return this;
    }

    /// <summary>
    /// Animates <see cref="Progress"/> to <paramref name="value"/> over <paramref name="length"/> ms: <c>true</c> when it ran
    /// to completion, <c>false</c> when a newer call replaced it or the animation was stopped. It pauses while the node is detached.
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

    private float StrokeThickness => SkUiProgressRingDrawing.Thickness(_thickness);

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        SkUiProgressRingDrawing.Measure(widthConstraint, heightConstraint, StrokeThickness, Padding,
            Content is { } child ? child.Measure : null);

    /// <inheritdoc />
    protected override void ArrangeContent(Size size) =>
        Content?.Arrange(SkUiProgressRingDrawing.ContentBounds(size, StrokeThickness, Padding));

    /// <inheritdoc />
    protected internal override void AddSkeletonBones(SkUiSkeletonBuilder bones) => bones.Add(SkUiSkeletonPartKind.ActivityIndicator, bones.Bounds);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        base.OnPaintContent(canvas);
        var enabled = IsEnabled;
        SkUiProgressRingDrawing.Draw(canvas, (float)Frame.Width, (float)Frame.Height, SkUiProgressBarDrawing.Drawn(_fill, _progress), _isIndeterminate,
            ToSkColor(SkUiColors.EnabledTint(_trackColor, enabled)), ToSkColor(SkUiColors.EnabledTint(_progressColor, enabled)),
            StrokeThickness, (float)_startAngle, _isClockwise, _lineCap, enabled);
    }

    /// <inheritdoc />
    internal override void OnGetRenderProps(ref SkUiRenderProps props)
    {
        base.OnGetRenderProps(ref props);
        SkUiProgressRingDrawing.SetSpin(ref props, _isIndeterminate);
    }

    /// <inheritdoc />
    protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
    {
        base.OnPopulateSemantics(info);
        info.Role = SkUiSemanticsRole.ProgressBar;
        if (!_isIndeterminate)
            info.Range = new SkUiSemanticsRange(0, 1, _progress);
    }
}
