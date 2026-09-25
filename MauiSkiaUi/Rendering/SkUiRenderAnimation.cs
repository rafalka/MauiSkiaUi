namespace MauiSkiaUi.Rendering;

/// <summary>
/// An animation of composite-time properties that runs on the render thread, so it keeps its frame rate
/// while the UI thread is busy. Created on the UI thread, sent with the next committed frame, advanced by
/// <see cref="SkUiCompositor"/>; <see cref="Finished"/> and reports are posted back to the UI thread.
/// </summary>
internal abstract class SkUiRenderAnimation
{
    private int _cancelled;

    /// <summary>UI-side owner (for bookkeeping when the node is detached).</summary>
    internal SkUiRenderState? Owner;

    /// <summary>Render node being animated; set when the animation is committed.</summary>
    internal SkUiRenderNode? Target;

    internal bool Started;
    internal TimeSpan StartTime;

    /// <summary>True once cancelled from either thread.</summary>
    internal bool IsCancelled => Volatile.Read(ref _cancelled) != 0;

    /// <summary>UI-thread callback: <c>true</c> when the animation ran to completion, <c>false</c> when cancelled.</summary>
    internal Action<SkUiRenderAnimation, bool>? Finished;

    /// <summary>Bit mask of <see cref="SkUiRenderProperty"/> values this animation writes.</summary>
    internal abstract int PropertyMask { get; }

    /// <summary>Stops the animation at its current value; safe to call from any thread.</summary>
    public void Cancel() => Interlocked.Exchange(ref _cancelled, 1);

    /// <summary>
    /// UI thread: true when the UI explicitly set an animated property, so completion handlers must not write
    /// the animation's last value back over the UI's new value.
    /// </summary>
    internal bool SupersededByUi { get; private set; }

    /// <summary>UI thread: cancels because the UI set an animated property.</summary>
    internal void Supersede()
    {
        SupersededByUi = true;
        Cancel();
    }

    /// <summary>Render thread: captures starting values on the first frame.</summary>
    internal virtual void OnStart(in SkUiRenderProps props) { }

    /// <summary>Render thread: writes animated values; returns <c>true</c> when finished.</summary>
    internal abstract bool Advance(TimeSpan elapsed, ref SkUiRenderProps props);

    /// <summary>Render thread: optional per-frame UI feedback (latest values), or <c>null</c>.</summary>
    internal virtual Action? TakeReport(in SkUiRenderProps props) => null;
}

/// <summary>Time-based tween of one or more properties toward target values.</summary>
internal sealed class SkUiRenderTween : SkUiRenderAnimation
{
    private readonly SkUiRenderProperty[] _properties;
    private readonly float[] _targets;
    private readonly float[] _starts;
    private readonly TimeSpan _duration;
    private readonly Easing _easing;
    private readonly int _mask;

    internal SkUiRenderTween(SkUiRenderProperty[] properties, float[] targets, TimeSpan duration, Easing? easing)
    {
        if (properties.Length != targets.Length || properties.Length == 0)
            throw new ArgumentException("Each animated property needs one target.", nameof(targets));
        _properties = properties;
        _targets = targets;
        _starts = new float[properties.Length];
        _duration = duration <= TimeSpan.Zero ? TimeSpan.FromTicks(1) : duration;
        _easing = easing ?? Easing.Linear;
        foreach (var property in properties)
            _mask |= 1 << (int)property;
    }

    /// <summary>Final values in property order (read on completion).</summary>
    internal IReadOnlyList<float> Targets => _targets;

    internal IReadOnlyList<SkUiRenderProperty> Properties => _properties;

    /// <summary>
    /// Values last written on the render thread (property order), or <c>null</c> if the tween never ran.
    /// Read on the UI thread from <see cref="SkUiRenderAnimation.Finished"/> (posted after the write).
    /// </summary>
    internal float[]? LastValues { get; private set; }

    internal override int PropertyMask => _mask;

    internal override void OnStart(in SkUiRenderProps props)
    {
        for (var index = 0; index < _properties.Length; index++)
            _starts[index] = props.Get(_properties[index]);
    }

    internal override bool Advance(TimeSpan elapsed, ref SkUiRenderProps props)
    {
        var progress = Math.Clamp(elapsed.TotalMilliseconds / _duration.TotalMilliseconds, 0, 1);
        var eased = (float)_easing.Ease(progress);
        var values = LastValues ?? new float[_properties.Length];
        for (var index = 0; index < _properties.Length; index++)
        {
            var value = progress >= 1 ? _targets[index] : _starts[index] + (_targets[index] - _starts[index]) * eased;
            props.Set(_properties[index], value);
            values[index] = value;
        }
        LastValues = values;
        return progress >= 1;
    }

    /// <summary>Optional per-frame UI feedback with the first two animated values.</summary>
    internal Action<float, float>? Report { get; init; }

    internal override Action? TakeReport(in SkUiRenderProps props)
    {
        if (Report is not { } report)
            return null;
        var first = props.Get(_properties[0]);
        var second = _properties.Length > 1 ? props.Get(_properties[1]) : 0;
        return () => report(first, second);
    }
}

/// <summary>
/// Inertial scroll: exponential velocity decay (UIScrollView-like, rate 0.998 per millisecond) of the
/// children offset, clamped to <c>[0, max]</c> per axis. Reports offsets to the UI thread every frame.
/// </summary>
internal sealed class SkUiRenderFling(float velocityX, float velocityY, float maxX, float maxY, Action<float, float> report)
    : SkUiRenderAnimation
{
    private const double DecayPerMs = 0.998;
    private const double StopVelocity = 10;
    private float _startX, _startY;

    internal override int PropertyMask =>
        (1 << (int)SkUiRenderProperty.ChildrenOffsetX) | (1 << (int)SkUiRenderProperty.ChildrenOffsetY);

    internal override void OnStart(in SkUiRenderProps props)
    {
        _startX = props.ChildrenOffsetX;
        _startY = props.ChildrenOffsetY;
    }

    internal override bool Advance(TimeSpan elapsed, ref SkUiRenderProps props)
    {
        var ms = elapsed.TotalMilliseconds;
        var decay = Math.Pow(DecayPerMs, ms);
        // ∫ v·d^t dt (t in ms, v in DIP/s) = v/1000 · (d^t − 1) / ln d
        var travel = (decay - 1) / Math.Log(DecayPerMs) / 1000;
        var x = Math.Clamp(_startX + velocityX * travel, 0, Math.Max(0, maxX));
        var y = Math.Clamp(_startY + velocityY * travel, 0, Math.Max(0, maxY));
        props.ChildrenOffsetX = (float)x;
        props.ChildrenOffsetY = (float)y;
        var stoppedX = velocityX == 0 || Math.Abs(velocityX * decay) < StopVelocity || x <= 0 || x >= maxX;
        var stoppedY = velocityY == 0 || Math.Abs(velocityY * decay) < StopVelocity || y <= 0 || y >= maxY;
        return stoppedX && stoppedY;
    }

    internal override Action? TakeReport(in SkUiRenderProps props)
    {
        var x = props.ChildrenOffsetX;
        var y = props.ChildrenOffsetY;
        return () => report(x, y);
    }
}
