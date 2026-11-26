using MauiSkiaUi.Rendering;
using Microsoft.Maui.Controls.Shapes;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// A drawn circular progress indicator: an arc of <see cref="Progress"/> over a track around a circle, with optional
/// <see cref="SkUiContentView.Content"/> (a value, an icon) laid out in the square inscribed in the inner circle
/// (<see cref="SkUiContentView.Padding"/> insets it further). <see cref="ProgressTo"/> animates the value as the progress
/// bar's does; <see cref="IsIndeterminate"/> turns an arc on the render thread, as <see cref="SkUiActivityIndicator"/>
/// spins, while the content stays put. Drawn by <see cref="SkUiLook.DrawProgressRing"/>; read as a progress bar.
/// </summary>
public class SkUiProgressRing : SkUiContentView
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

    /// <summary>Bindable <see cref="Progress"/>.</summary>
    public static readonly BindableProperty ProgressProperty = BindableProperty.Create(nameof(Progress), typeof(double), typeof(SkUiProgressRing), 0d,
        coerceValue: (_, value) => SkUiProgressTween.Clamp((double)value),
        propertyChanged: (view, _, value) => ((SkUiProgressRing)view).OnProgressChanged((double)value));

    /// <summary>Bindable <see cref="IsIndeterminate"/>.</summary>
    public static readonly BindableProperty IsIndeterminateProperty = BindableProperty.Create(nameof(IsIndeterminate), typeof(bool), typeof(SkUiProgressRing), false,
        propertyChanged: (view, _, value) => ((SkUiProgressRing)view).OnIsIndeterminateChanged((bool)value));

    /// <summary>Bindable <see cref="ProgressColor"/>.</summary>
    public static readonly BindableProperty ProgressColorProperty = BindableProperty.Create(nameof(ProgressColor), typeof(Color), typeof(SkUiProgressRing), null,
        defaultValueCreator: _ => SkUiColors.Accent, validateValue: SkUiValidate.NotNull,
        propertyChanged: (view, _, value) => ((SkUiProgressRing)view).OnProgressColorChanged((Color)value));

    /// <summary>Bindable <see cref="TrackColor"/>.</summary>
    public static readonly BindableProperty TrackColorProperty = BindableProperty.Create(nameof(TrackColor), typeof(Color), typeof(SkUiProgressRing), null,
        defaultValueCreator: _ => SkUiColors.TrackOff, validateValue: SkUiValidate.NotNull,
        propertyChanged: (view, _, value) => ((SkUiProgressRing)view).OnTrackColorChanged((Color)value));

    /// <summary>Bindable <see cref="Thickness"/>.</summary>
    public static readonly BindableProperty ThicknessProperty = BindableProperty.Create(nameof(Thickness), typeof(double), typeof(SkUiProgressRing), -1d,
        validateValue: SkUiValidate.NonNegativeOrLook,
        propertyChanged: (view, _, value) => ((SkUiProgressRing)view).OnThicknessChanged((double)value));

    /// <summary>Bindable <see cref="StartAngle"/>.</summary>
    public static readonly BindableProperty StartAngleProperty = BindableProperty.Create(nameof(StartAngle), typeof(double), typeof(SkUiProgressRing), -90d,
        validateValue: SkUiValidate.Finite,
        propertyChanged: (view, _, value) => ((SkUiProgressRing)view).OnStartAngleChanged((double)value));

    /// <summary>Bindable <see cref="IsClockwise"/>.</summary>
    public static readonly BindableProperty IsClockwiseProperty = BindableProperty.Create(nameof(IsClockwise), typeof(bool), typeof(SkUiProgressRing), true,
        propertyChanged: (view, _, value) => ((SkUiProgressRing)view).OnIsClockwiseChanged((bool)value));

    /// <summary>Bindable <see cref="LineCap"/>.</summary>
    public static readonly BindableProperty LineCapProperty = BindableProperty.Create(nameof(LineCap), typeof(PenLineCap), typeof(SkUiProgressRing), PenLineCap.Round,
        validateValue: SkUiValidate.LineCap,
        propertyChanged: (view, _, value) => ((SkUiProgressRing)view).OnLineCapChanged((PenLineCap)value));

    /// <summary>Completed fraction, 0–1 (clamped; NaN becomes 0).</summary>
    public double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }

    /// <summary>Shows activity without a known amount: an arc turns around the ring and <see cref="Progress"/> is not drawn.</summary>
    public bool IsIndeterminate { get => (bool)GetValue(IsIndeterminateProperty); set => SetValue(IsIndeterminateProperty, value); }

    /// <summary>Arc color.</summary>
    public Color ProgressColor { get => (Color)GetValue(ProgressColorProperty); set => SetValue(ProgressColorProperty, value); }

    /// <summary>Track color around the rest of the circle (transparent: no track).</summary>
    public Color TrackColor { get => (Color)GetValue(TrackColorProperty); set => SetValue(TrackColorProperty, value); }

    /// <summary>Stroke thickness in DIPs (default -1: the look's <see cref="SkUiLook.ProgressRingThickness"/>).</summary>
    public double Thickness { get => (double)GetValue(ThicknessProperty); set => SetValue(ThicknessProperty, value); }

    /// <summary>Where the progress starts, in degrees clockwise from 3 o'clock (default -90: the top).</summary>
    public double StartAngle { get => (double)GetValue(StartAngleProperty); set => SetValue(StartAngleProperty, value); }

    /// <summary>Whether the progress grows clockwise (default <c>true</c>); right-to-left layouts do not change it.</summary>
    public bool IsClockwise { get => (bool)GetValue(IsClockwiseProperty); set => SetValue(IsClockwiseProperty, value); }

    /// <summary>The ends of the progress arc (default <see cref="PenLineCap.Round"/>).</summary>
    public PenLineCap LineCap { get => (PenLineCap)GetValue(LineCapProperty); set => SetValue(LineCapProperty, value); }

    /// <summary>Sets the progress (clamped) (same as the property setter).</summary>
    public SkUiProgressRing SetProgress(double value) { Progress = SkUiProgressTween.Clamp(value); return this; }

    /// <summary>Sets <see cref="IsIndeterminate"/> (same as the property setter).</summary>
    public SkUiProgressRing SetIsIndeterminate(bool value) { IsIndeterminate = value; return this; }

    /// <summary>Sets the progress color (same as the property setter).</summary>
    public SkUiProgressRing SetProgressColor(Color value) { ArgumentNullException.ThrowIfNull(value); ProgressColor = value; return this; }

    /// <summary>Sets the track color (same as the property setter).</summary>
    public SkUiProgressRing SetTrackColor(Color value) { ArgumentNullException.ThrowIfNull(value); TrackColor = value; return this; }

    /// <summary>Sets <see cref="Thickness"/>: DIPs, or -1 for the look's.</summary>
    public SkUiProgressRing SetThickness(double value)
    {
        SkUiValidate.ThrowIfNotNonNegativeOrLook(value, nameof(value));
        Thickness = value;
        return this;
    }

    /// <summary>Sets <see cref="StartAngle"/> in degrees.</summary>
    public SkUiProgressRing SetStartAngle(double value)
    {
        SkUiValidate.ThrowIfNotFinite(value, nameof(value));
        StartAngle = value;
        return this;
    }

    /// <summary>Sets <see cref="IsClockwise"/> (same as the property setter).</summary>
    public SkUiProgressRing SetIsClockwise(bool value) { IsClockwise = value; return this; }

    /// <summary>Sets <see cref="LineCap"/>.</summary>
    public SkUiProgressRing SetLineCap(PenLineCap value)
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value), value, "Not a defined line cap.");
        LineCap = value;
        return this;
    }

    private void OnProgressChanged(double value)
    {
        if (_progress == value) return; // exact: did the value change
        if (_isIndeterminate)
        {
            // Not drawn: nothing to re-record (turning indeterminate off repaints).
            _fill?.Jump((float)value);
            _progress = value;
            return;
        }
        SkUiProgressBarDrawing.ProgressChanged(this, ref _fill, _progress, value, _tween);
        _progress = value;
        InvalidatePaint();
    }

    private void OnIsIndeterminateChanged(bool value)
    {
        if (_isIndeterminate == value) return;
        _isIndeterminate = value;
        InvalidatePaint();
        InvalidateRender(SkUiRenderDirty.Props);
    }

    private void OnProgressColorChanged(Color value) { _progressColor = value; InvalidatePaint(); }
    private void OnTrackColorChanged(Color value) { _trackColor = value; InvalidatePaint(); }
    private void OnThicknessChanged(double value) { _thickness = value; InvalidateMeasureOverride(); InvalidatePaint(); }
    private void OnStartAngleChanged(double value) { _startAngle = value; InvalidatePaint(); }
    private void OnIsClockwiseChanged(bool value) { _isClockwise = value; InvalidatePaint(); }
    private void OnLineCapChanged(PenLineCap value) { _lineCap = value; InvalidatePaint(); }

    /// <summary>
    /// Animates <see cref="Progress"/> to <paramref name="value"/> over <paramref name="length"/> ms, as
    /// <see cref="SkUiProgressBar.ProgressTo"/>: <c>true</c> when it ran to completion, <c>false</c> when a newer call
    /// replaced it or the animation was stopped. It pauses while the ring is detached.
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

    private float StrokeThickness => SkUiProgressRingDrawing.Thickness(_thickness);

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        SkUiProgressRingDrawing.Measure(widthConstraint, heightConstraint, StrokeThickness, Padding,
            LayoutChild is { } child ? child.Measure : null);

    /// <inheritdoc />
    protected override void ArrangeContent(Size size) =>
        LayoutChild?.Arrange(SkUiProgressRingDrawing.ContentBounds(size, StrokeThickness, Padding));

    /// <inheritdoc />
    protected internal override void AddSkeletonBones(SkUiSkeletonBuilder bones) => bones.Add(SkUiSkeletonPartKind.ActivityIndicator, bones.Bounds);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        base.OnPaintContent(canvas);
        var enabled = IsEnabled;
        SkUiProgressRingDrawing.Draw(canvas, (float)Width, (float)Height, SkUiProgressBarDrawing.Drawn(_fill, _progress), _isIndeterminate,
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

/// <summary>Measuring, content bounds, drawing and the render-thread spin shared by both progress rings.</summary>
internal static class SkUiProgressRingDrawing
{
    private static readonly double Sqrt2 = Math.Sqrt(2);

    /// <summary>The stroke thickness for a <c>Thickness</c> property value (-1: the look's).</summary>
    public static float Thickness(double value) => value < 0 ? SkUiLook.Current.ProgressRingThickness : (float)value;

    /// <summary>The ring's size: the look's, grown to fit the content (measured inside the inner circle's square).</summary>
    public static Size Measure(double widthConstraint, double heightConstraint, float thickness, Thickness padding, Func<double, double, Size>? measureContent)
    {
        var content = Size.Zero;
        if (measureContent is not null)
        {
            var available = Math.Min(widthConstraint, heightConstraint);
            var side = double.IsFinite(available) ? Math.Max(0, available - 2 * thickness) / Sqrt2 : double.PositiveInfinity;
            var size = measureContent(Math.Max(0, side - padding.HorizontalThickness), Math.Max(0, side - padding.VerticalThickness));
            content = new Size(size.Width + padding.HorizontalThickness, size.Height + padding.VerticalThickness);
        }
        return SkUiLook.Current.MeasureProgressRing(widthConstraint, heightConstraint, thickness, content);
    }

    /// <summary>Where the content goes in a ring of <paramref name="size"/>: the square inscribed in the inner circle, less the padding.</summary>
    public static Rect ContentBounds(Size size, float thickness, Thickness padding)
    {
        var diameter = Math.Min(size.Width, size.Height);
        var side = Math.Max(0, diameter - 2 * thickness) / Sqrt2;
        var left = (size.Width - side) / 2 + padding.Left;
        var top = (size.Height - side) / 2 + padding.Top;
        return new Rect(left, top, Math.Max(0, side - padding.HorizontalThickness), Math.Max(0, side - padding.VerticalThickness));
    }

    /// <summary>Draws the ring in the square centred in <paramref name="width"/> × <paramref name="height"/>.</summary>
    public static void Draw(SKCanvas canvas, float width, float height, float progress, bool indeterminate, SKColor track, SKColor fill,
        float thickness, float startAngle, bool clockwise, PenLineCap lineCap, bool enabled)
    {
        var diameter = Math.Min(width, height);
        if (diameter <= 0) return;
        var left = (width - diameter) / 2;
        var top = (height - diameter) / 2;
        var look = SkUiLook.Current;
        look.DrawProgressRing(canvas, new SkUiProgressRingPaint(new SKRect(left, top, left + diameter, top + diameter), progress, indeterminate,
            track, fill, thickness, startAngle, clockwise, lineCap, look.IndeterminateProgressRingSweep, enabled));
    }

    /// <summary>Indeterminate: the compositor turns the recorded ring about its centre (children stay put).</summary>
    public static void SetSpin(ref SkUiRenderProps props, bool indeterminate) =>
        props.ContentSpinPeriod = indeterminate ? Math.Max(0, SkUiLook.Current.IndeterminateProgressRingPeriod) : 0;
}
