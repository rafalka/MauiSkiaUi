namespace MauiSkiaUi;

/// <summary>A root-owned clock ticked before paint using monotonic elapsed frame time.</summary>
public sealed class SkUiAnimationClock
{
    private readonly List<RunningAnimation> _animations = [];
    private TimeSpan _frameTime;

    internal TimeSpan FrameTime => _frameTime;

    /// <summary>Raised when continuous frames need to start or stop.</summary>
    public event EventHandler? RunningChanged;

    /// <summary>True while at least one animation requires frames.</summary>
    public bool IsRunning => _animations.Count > 0;

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

    /// <summary>Advances all active animations; tests may supply deterministic frame times.</summary>
    public void Tick(TimeSpan elapsed)
    {
        if (elapsed < _frameTime)
            throw new ArgumentOutOfRangeException(nameof(elapsed), "Frame time must be monotonic.");
        _frameTime = elapsed;
        foreach (var animation in _animations.ToArray())
        {
            if (!_animations.Contains(animation))
                continue;
            var progress = (elapsed - animation.Started).TotalMilliseconds / animation.Duration.TotalMilliseconds;
            animation.Apply(animation.Easing.Ease(animation.Repeat ? progress % 1 : Math.Min(progress, 1)));
            if (!animation.Repeat && progress >= 1)
                animation.Dispose();
        }
    }

    /// <summary>Cancels every animation, releasing callbacks and stopping continuous frames.</summary>
    /// <remarks>Preserves the last frame time. Subsequent animations start on the same monotonic timeline; do not restart Tick timestamps at zero.</remarks>
    public void StopAll()
    {
        if (!IsRunning)
            return;
        _animations.Clear();
        RunningChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class RunningAnimation(
        SkUiAnimationClock owner, Action<double> apply, TimeSpan duration, Easing easing, bool repeat, TimeSpan started) : IDisposable
    {
        internal Action<double> Apply { get; } = apply;
        internal TimeSpan Duration { get; } = duration;
        internal Easing Easing { get; } = easing;
        internal bool Repeat { get; } = repeat;
        internal TimeSpan Started { get; } = started;

        public void Dispose()
        {
            if (owner._animations.Remove(this) && !owner.IsRunning)
                owner.RunningChanged?.Invoke(owner, EventArgs.Empty);
        }
    }
}