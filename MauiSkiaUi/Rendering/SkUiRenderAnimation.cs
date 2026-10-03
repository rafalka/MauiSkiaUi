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

    /// <summary>
    /// UI thread: invokes <see cref="Finished"/> once. Every path that drops an animation (completion, cancel,
    /// compositor dispose, render-state reset) ends here, so awaiters never hang.
    /// </summary>
    internal void NotifyFinished(bool completed)
    {
        if (Interlocked.Exchange(ref _notified, 1) == 0)
            Finished?.Invoke(this, completed);
    }

    private int _notified;

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
/// Inertial scroll: exponential velocity decay (UIScrollView-like, rate 0.998 per millisecond) of the scroll offset
/// within <c>[0, max]</c> per axis. An axis stops when its velocity is spent or when it reaches the edge it moves
/// towards (a fling away from an edge runs normally). With overscroll, the content runs past that edge with the
/// velocity it had there and springs back (a critically damped spring, peaking within the axis' overscroll limit),
/// drawn through the children transform (<see cref="SkUiOverscroll.Apply"/>). The limits follow extent changes while
/// the fling runs (<see cref="SetLimits"/>). Reports offsets and overscroll to the UI thread every frame.
/// </summary>
internal sealed class SkUiRenderFling : SkUiRenderAnimation
{
    private const double DecayPerMs = 0.998;
    private const double StopVelocity = 10;

    /// <summary>Stiffness of edge bounces and spring-backs (1/s): a bounce peaks 1/ω after the edge; both settle within about 6/ω.</summary>
    internal const double SpringOmega = 12;

    private static readonly double LogDecay = Math.Log(DecayPerMs);

    /// <summary>Seconds per (DIP / s): how far a fling travels in total is its velocity times this (about 0.5 s).</summary>
    internal static readonly double TotalTravelPerVelocity = -1 / LogDecay / 1000;
    private readonly Action<float, float, float, float> _report;
    private readonly SkUiOverscrollMode _mode;
    private readonly float _width, _height;
    private Axis _x, _y;
    private volatile float _maxX, _maxY;
    private float _overscrollX, _overscrollY;

    /// <param name="velocityX">Horizontal offset velocity (DIPs / s).</param>
    /// <param name="velocityY">Vertical offset velocity (DIPs / s).</param>
    /// <param name="maxX">Largest horizontal offset.</param>
    /// <param name="maxY">Largest vertical offset.</param>
    /// <param name="overscrollLimitX">Largest horizontal edge bounce (DIPs); 0 stops at the edge.</param>
    /// <param name="overscrollLimitY">Largest vertical edge bounce (DIPs); 0 stops at the edge.</param>
    /// <param name="mode">How overscroll is drawn.</param>
    /// <param name="width">Viewport width (a stretch scales by overscroll over the viewport).</param>
    /// <param name="height">Viewport height.</param>
    /// <param name="report">UI-thread feedback: offset X / Y, overscroll X / Y.</param>
    internal SkUiRenderFling(float velocityX, float velocityY, float maxX, float maxY, float overscrollLimitX, float overscrollLimitY,
        SkUiOverscrollMode mode, float width, float height, Action<float, float, float, float> report)
    {
        _x = new Axis(velocityX, overscrollLimitX);
        _y = new Axis(velocityY, overscrollLimitY);
        _maxX = Math.Max(0, maxX);
        _maxY = Math.Max(0, maxY);
        _mode = mode;
        _width = width;
        _height = height;
        _report = report;
    }

    internal override int PropertyMask => SkUiRenderOverscrollSettle.ScrollMask;

    /// <summary>Any thread: the scroll extent changed, so the fling stops at (or bounces from) the new edges.</summary>
    internal void SetLimits(float maxX, float maxY)
    {
        _maxX = Math.Max(0, maxX);
        _maxY = Math.Max(0, maxY);
    }

    internal override void OnStart(in SkUiRenderProps props)
    {
        _x.Start = props.ChildrenOffsetX;
        _y.Start = props.ChildrenOffsetY;
    }

    internal override bool Advance(TimeSpan elapsed, ref SkUiRenderProps props)
    {
        var ms = elapsed.TotalMilliseconds;
        var doneX = _x.Advance(ms, _maxX, out var x, out _overscrollX);
        var doneY = _y.Advance(ms, _maxY, out var y, out _overscrollY);
        SkUiOverscroll.Apply(ref props, x, y, _overscrollX, _overscrollY, _mode, _width, _height);
        return doneX && doneY;
    }

    internal override Action? TakeReport(in SkUiRenderProps props)
    {
        var x = props.ChildrenOffsetX - (_mode == SkUiOverscrollMode.Bounce ? _overscrollX : 0);
        var y = props.ChildrenOffsetY - (_mode == SkUiOverscrollMode.Bounce ? _overscrollY : 0);
        var overscrollX = _overscrollX;
        var overscrollY = _overscrollY;
        return () => _report(x, y, overscrollX, overscrollY);
    }

    /// <summary>One axis of the fling: decay towards an edge, then (with overscroll) the bounce from it.</summary>
    private struct Axis(float velocity, float overscrollLimit)
    {
        public float Start;
        private bool _bouncing;
        private double _bounceStart;
        private double _bounceVelocity;

        /// <summary>Position at <paramref name="ms"/> after the start; returns <c>true</c> once this axis is at rest.</summary>
        public bool Advance(double ms, float max, out float offset, out float overscroll)
        {
            overscroll = 0;
            if (!_bouncing)
            {
                var start = Math.Clamp(Start, 0, max);
                var decay = Math.Pow(DecayPerMs, ms);
                // ∫ v·d^t dt (t in ms, v in DIP/s) = v/1000 · (d^t − 1) / ln d
                var position = start + velocity * (decay - 1) / LogDecay / 1000;
                var edge = velocity < 0 ? 0 : max;
                if (velocity == 0 || (velocity < 0 ? position > 0 : position < max))
                {
                    offset = (float)Math.Clamp(position, 0, max);
                    return Math.Abs(velocity * decay) < StopVelocity;
                }
                offset = edge;
                if (overscrollLimit <= 0)
                    return true;
                // Run past the edge with the velocity the content had when it got there.
                var reached = 1 + (edge - start) * LogDecay * 1000 / velocity;
                _bounceStart = reached > 0 ? Math.Log(reached) / LogDecay : ms;
                var limit = overscrollLimit * SpringOmega * Math.E;
                _bounceVelocity = Math.Clamp(velocity * Math.Pow(DecayPerMs, _bounceStart), -limit, limit);
                _bouncing = true;
            }
            offset = _bounceVelocity < 0 ? 0 : max;
            // x(t) = v·t·e^(−ωt): leaves the edge with the fling's velocity, peaks at v / (ω·e) after 1/ω, settles.
            var t = Math.Max(0, ms - _bounceStart) / 1000;
            var value = _bounceVelocity * t * Math.Exp(-SpringOmega * t);
            if (t > 1 / SpringOmega && Math.Abs(value) < 0.5)
                return true;
            overscroll = (float)value;
            return false;
        }
    }
}

/// <summary>
/// Spring-back from overscroll after a drag released past an edge: a critically damped spring, <c>o·(1 + ωt)·e^(−ωt)</c>,
/// drawn through the children transform (<see cref="SkUiOverscroll.Apply"/>). Reports the overscroll to the UI thread every frame.
/// </summary>
internal sealed class SkUiRenderOverscrollSettle(float x, float y, float overscrollX, float overscrollY, SkUiOverscrollMode mode,
    float width, float height, Action<float, float> report) : SkUiRenderAnimation
{
    /// <summary>The children offset and scale: what scroll motion writes.</summary>
    internal const int ScrollMask =
        (1 << (int)SkUiRenderProperty.ChildrenOffsetX) | (1 << (int)SkUiRenderProperty.ChildrenOffsetY)
        | (1 << (int)SkUiRenderProperty.ChildrenScaleX) | (1 << (int)SkUiRenderProperty.ChildrenScaleY);

    private float _x, _y;

    internal override int PropertyMask => ScrollMask;

    internal override bool Advance(TimeSpan elapsed, ref SkUiRenderProps props)
    {
        var t = elapsed.TotalSeconds;
        var factor = (1 + SkUiRenderFling.SpringOmega * t) * Math.Exp(-SkUiRenderFling.SpringOmega * t);
        _x = (float)(overscrollX * factor);
        _y = (float)(overscrollY * factor);
        var done = Math.Abs(_x) < 0.25 && Math.Abs(_y) < 0.25;
        if (done)
            _x = _y = 0;
        SkUiOverscroll.Apply(ref props, x, y, _x, _y, mode, width, height);
        return done;
    }

    internal override Action? TakeReport(in SkUiRenderProps props)
    {
        var overscrollX = _x;
        var overscrollY = _y;
        return () => report(overscrollX, overscrollY);
    }
}

/// <summary>
/// Settles the scroll offset on a target (a snap point) with a critically damped spring that starts at the release
/// velocity: <c>x(t) = target + (d + (v + ω·d)·t)·e^(−ωt)</c> per axis, with <c>d</c> the start's distance from the
/// target. A fling towards the target eases into it without passing it (its speed towards the target is capped at
/// <c>ω·|d|</c>, the most a critically damped spring takes without overshooting); a slow release glides there. Reports
/// offsets every frame.
/// </summary>
internal sealed class SkUiRenderScrollSpring(float targetX, float targetY, float velocityX, float velocityY, float maxX, float maxY,
    Action<float, float> report) : SkUiRenderAnimation
{
    /// <summary>Stiffness (1/s): settles within about half a second.</summary>
    internal const double Omega = 12;

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
        var t = elapsed.TotalSeconds;
        var x = Position(_startX, targetX, velocityX, t);
        var y = Position(_startY, targetY, velocityY, t);
        var done = t > 2 || (t > 1 / Omega && Math.Abs(x - targetX) < 0.5 && Math.Abs(y - targetY) < 0.5);
        // A fast release may carry the spring past its target: never past the content's edges.
        props.ChildrenOffsetX = done ? targetX : (float)Math.Clamp(x, 0, Math.Max(0, maxX));
        props.ChildrenOffsetY = done ? targetY : (float)Math.Clamp(y, 0, Math.Max(0, maxY));
        return done;
    }

    private static double Position(double start, double target, double velocity, double t)
    {
        var distance = start - target;
        // Towards the target (opposite sign to the distance): no faster than lands exactly on it.
        if (velocity * distance < 0)
            velocity = Math.Sign(velocity) * Math.Min(Math.Abs(velocity), Omega * Math.Abs(distance));
        return target + (distance + (velocity + Omega * distance) * t) * Math.Exp(-Omega * t);
    }

    internal override Action? TakeReport(in SkUiRenderProps props)
    {
        var x = props.ChildrenOffsetX;
        var y = props.ChildrenOffsetY;
        return () => report(x, y);
    }
}
