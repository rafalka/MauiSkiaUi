using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>A drawn node whose state changes animate (<see cref="SkUiView"/>, <see cref="Core.SkUiCoreNode"/>).</summary>
internal interface ISkUiTransitionHost
{
    /// <summary>
    /// The clock transitions run on, or <c>null</c> when the node has not been drawn on a surface yet: state set
    /// before a page appears shows at once instead of animating on a clock nobody ticks.
    /// </summary>
    SkUiAnimationClock? TransitionClock { get; }

    /// <summary>Re-records the node's content for the next frame of a transition.</summary>
    void InvalidateTransition();
}

/// <summary>
/// One animated number for state-change transitions, on the host's UI-thread <see cref="SkUiAnimationClock"/>. Each
/// frame re-records only the host's own picture. When the clock is stopped (the page closed, the surface replaced), the
/// tween jumps to its target.
/// </summary>
internal sealed class SkUiTween(ISkUiTransitionHost host)
{
    private IDisposable? _handle;
    private Action<bool>? _completed;
    private float _from;
    private float _to;
    private float _value;

    /// <summary>The value to draw.</summary>
    public float Value => _value;

    /// <summary>Where the tween is going (or has arrived).</summary>
    public float Target => _to;

    /// <summary>Whether a transition is in progress.</summary>
    public bool IsRunning => _handle is not null;

    /// <summary>Stops any transition and shows <paramref name="value"/> (the caller repaints).</summary>
    public void Jump(float value)
    {
        Stop();
        _from = _to = _value = value;
    }

    /// <summary>
    /// Animates from the current value to <paramref name="target"/> with <paramref name="transition"/>, its duration
    /// scaled by <paramref name="durationScale"/> (e.g. the remaining part of a reversed transition). Without a clock
    /// or a transition, it jumps. <paramref name="completed"/> runs with <c>true</c> when the transition finished or
    /// jumped, <c>false</c> when its clock stopped it (it has jumped to the target then); not when it was replaced.
    /// </summary>
    public void AnimateTo(float target, SkUiTransition transition, Action<bool>? completed = null, float durationScale = 1)
    {
        Stop();
        var clock = host.TransitionClock;
        var duration = TimeSpan.FromTicks((long)(transition.Duration.Ticks * Math.Clamp(durationScale, 0, 1)));
        if (clock is null || transition.IsNone || duration < TimeSpan.FromMilliseconds(1) || _value == target)
        {
            _from = _to = _value = target;
            host.InvalidateTransition();
            completed?.Invoke(true);
            return;
        }
        _from = _value;
        _to = target;
        _completed = completed;
        var easing = transition.Easing ?? Easing.Linear;
        IDisposable? handle = null;
        handle = clock.Start(t =>
        {
            _value = t >= 1 ? _to : _from + (_to - _from) * (float)easing.Ease(t);
            host.InvalidateTransition();
            if (t >= 1 && handle is not null && ReferenceEquals(_handle, handle))
                Finish(finished: true);
        }, duration, easing: null, repeat: false, stopped: () =>
        {
            if (handle is null || !ReferenceEquals(_handle, handle))
                return;
            _value = _to;
            host.InvalidateTransition();
            Finish(finished: false);
        });
        _handle = handle;
    }

    private void Finish(bool finished)
    {
        _handle = null;
        var completed = _completed;
        _completed = null;
        completed?.Invoke(finished);
    }

    private void Stop()
    {
        var handle = _handle;
        _handle = null;
        _completed = null;
        handle?.Dispose(); // its stopped callback sees it is no longer current
    }
}

/// <summary>
/// A toggle's <see cref="SkUiCheckState"/> transition: from the previous state to the new one with the look's
/// <paramref name="kind"/> transition. Toggling back mid-way reverses from where it is.
/// </summary>
internal sealed class SkUiToggleAnimator(ISkUiTransitionHost host, SkUiTransitionKind kind)
{
    private readonly SkUiTween _progress = new(host);
    private SkUiCheckState _from;
    private SkUiCheckState _to;

    /// <summary>The visual for a control whose state is <paramref name="state"/>.</summary>
    public SkUiToggleVisual Visual(SkUiCheckState state, float pressed) =>
        _progress.IsRunning && _to == state
            ? new SkUiToggleVisual(state, _from, _progress.Value, pressed)
            : SkUiToggleVisual.Settled(state, pressed);

    /// <summary>The control's state changed from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public void Changed(SkUiCheckState from, SkUiCheckState to)
    {
        var transition = SkUiLook.Current.GetTransition(kind);
        if (_progress.IsRunning && to == _from)
        {
            // Back to where it came from: continue from the current point, over the part already travelled.
            var shown = _progress.Value;
            (_from, _to) = (_to, _from);
            _progress.Jump(1 - shown);
            _progress.AnimateTo(1, transition, durationScale: shown);
            return;
        }
        if (_progress.IsRunning)
            from = _progress.Value >= 0.5f ? _to : _from; // a third state mid-way: start from the nearer one
        _from = from;
        _to = to;
        _progress.Jump(0);
        _progress.AnimateTo(1, transition);
    }
}

/// <summary>
/// Press feedback: <see cref="SkUiPressVisual.Pressed"/> with the look's press / release transitions and, for buttons,
/// a ripple from the press point. A quick tap shows its press fully before it releases.
/// </summary>
internal sealed class SkUiPressAnimator
{
    private readonly SkUiTween _pressed;
    private readonly SkUiTween? _ripple;
    private readonly SkUiTween? _fade;
    private SKPoint _origin;
    private bool _isPressed;
    private bool _releasePending;

    /// <param name="host">The pressed control.</param>
    /// <param name="ripple">Whether to track a ripple (buttons); toggles only show <see cref="Pressed"/>.</param>
    public SkUiPressAnimator(ISkUiTransitionHost host, bool ripple = true)
    {
        _pressed = new SkUiTween(host);
        if (!ripple)
            return;
        _ripple = new SkUiTween(host);
        _fade = new SkUiTween(host);
        _fade.Jump(1);
    }

    /// <summary>The press feedback to draw.</summary>
    public SkUiPressVisual Visual => new(_pressed.Value, _origin, _ripple?.Value ?? 0, _fade?.Value ?? 1);

    /// <summary>The press amount to draw (0–1).</summary>
    public float Pressed => _pressed.Value;

    /// <summary>The control's press state changed; <paramref name="origin"/> is the press point in its coordinates.</summary>
    public void SetPressed(bool pressed, Point origin)
    {
        if (pressed == _isPressed)
            return;
        _isPressed = pressed;
        var look = SkUiLook.Current;
        if (pressed)
        {
            _releasePending = false;
            _origin = new SKPoint((float)origin.X, (float)origin.Y);
            if (_ripple is not null && _fade is not null)
            {
                _fade.Jump(0);
                _ripple.Jump(0);
                _ripple.AnimateTo(1, look.GetTransition(SkUiTransitionKind.Ripple));
            }
            _pressed.AnimateTo(1, look.GetTransition(SkUiTransitionKind.Press), finished =>
            {
                if (!_releasePending)
                    return;
                _releasePending = false;
                Release(animate: finished);
            });
        }
        else if (_pressed.IsRunning && _pressed.Target == 1)
        {
            _releasePending = true; // released before the press showed fully: release once it has
        }
        else
        {
            Release(animate: true);
        }
    }

    private void Release(bool animate)
    {
        if (_isPressed)
            return;
        var transition = animate ? SkUiLook.Current.GetTransition(SkUiTransitionKind.Release) : SkUiTransition.None;
        _pressed.AnimateTo(0, transition);
        _fade?.AnimateTo(1, transition);
    }
}
