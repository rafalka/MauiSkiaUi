namespace MauiSkiaUi;

/// <summary>A root-owned clock ticked before paint using monotonic elapsed frame time.</summary>
public sealed class SkUiAnimationClock
{
    private readonly List<RunningAnimation> _animations = [];
    private TimeSpan _frameTime;
    private bool _ticking;
    private bool _removedDuringTick;

    internal TimeSpan FrameTime => _frameTime;

    /// <summary>Raised when continuous frames need to start or stop.</summary>
    public event EventHandler? RunningChanged;

    /// <summary>True while at least one animation requires frames.</summary>
    public bool IsRunning
    {
        get
        {
            if (!_removedDuringTick)
                return _animations.Count > 0;
            foreach (var animation in _animations)
                if (!animation.IsDisposed)
                    return true;
            return false;
        }
    }

    /// <summary>Starts a progress callback. Dispose the returned handle to cancel it.</summary>
    public IDisposable Start(Action<double> apply, TimeSpan duration, Easing? easing = null, bool repeat = false)
    {
        ArgumentNullException.ThrowIfNull(apply);
        if (duration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration));
        var wasRunning = IsRunning;
        var animation = new RunningAnimation(this, apply, duration, easing ?? Easing.Linear, repeat, _frameTime);
        _animations.Add(animation);
        apply(0);
        if (!wasRunning)
            RunningChanged?.Invoke(this, EventArgs.Empty);
        return animation;
    }

    /// <summary>
    /// Advances all active animations; tests may supply deterministic frame times.
    /// Allocation-free: does not snapshot the animator list. Only animations that existed when
    /// the tick began are advanced (callbacks started mid-tick wait for the next frame).
    /// Callbacks may dispose any animation (their own included) or call <see cref="StopAll"/>: removals during a
    /// tick only mark the entry, and the list is compacted after the loop, so no animation is skipped or indexed
    /// out of range.
    /// </summary>
    public void Tick(TimeSpan elapsed)
    {
        if (elapsed < _frameTime)
            throw new ArgumentOutOfRangeException(nameof(elapsed), "Frame time must be monotonic.");
        _frameTime = elapsed;
        var wasRunning = IsRunning;
        _ticking = true;
        try
        {
            // Bound to the count at tick start so Start() from Apply does not double-invoke at progress 0.
            var endExclusive = _animations.Count;
            for (var index = 0; index < endExclusive; index++)
            {
                var animation = _animations[index];
                if (animation.IsDisposed)
                    continue;
                var progress = (elapsed - animation.Started).TotalMilliseconds / animation.Duration.TotalMilliseconds;
                animation.Apply(animation.Easing.Ease(animation.Repeat ? progress % 1 : Math.Min(progress, 1)));
                if (!animation.Repeat && progress >= 1)
                    animation.Dispose();
            }
        }
        finally
        {
            _ticking = false;
            if (_removedDuringTick)
            {
                _removedDuringTick = false;
                _animations.RemoveAll(static animation => animation.IsDisposed);
                if (wasRunning && _animations.Count == 0)
                    RunningChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>Cancels every animation, releasing callbacks and stopping continuous frames.</summary>
    /// <remarks>Preserves the last frame time. Subsequent animations start on the same monotonic timeline; do not restart Tick timestamps at zero.</remarks>
    public void StopAll()
    {
        if (!IsRunning)
            return;
        // Dispose each handle so callers holding Start() disposables can rebind cleanly. Mid-tick this only marks
        // them (Tick compacts afterwards).
        for (var index = _animations.Count - 1; index >= 0 && index < _animations.Count; index--)
            _animations[index].Dispose();
    }

    private sealed class RunningAnimation(
        SkUiAnimationClock owner, Action<double> apply, TimeSpan duration, Easing easing, bool repeat, TimeSpan started) : IDisposable
    {
        internal Action<double> Apply { get; } = apply;
        internal TimeSpan Duration { get; } = duration;
        internal Easing Easing { get; } = easing;
        internal bool Repeat { get; } = repeat;
        internal TimeSpan Started { get; } = started;
        internal bool IsDisposed { get; private set; }

        public void Dispose()
        {
            if (IsDisposed)
                return;
            IsDisposed = true;
            if (owner._ticking)
            {
                // Mid-tick: Tick compacts the list (and raises RunningChanged) after its loop.
                owner._removedDuringTick = true;
                return;
            }
            if (owner._animations.Remove(this) && owner._animations.Count == 0)
                owner.RunningChanged?.Invoke(owner, EventArgs.Empty);
        }
    }
}
