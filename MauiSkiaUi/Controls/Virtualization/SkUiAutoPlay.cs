namespace MauiSkiaUi;

/// <summary>What an <see cref="SkUiAutoPlay"/> asks its control at each tick.</summary>
internal interface ISkUiAutoPlayHost
{
    /// <summary>
    /// Whether the control holds this step: the user is scrolling, hovering it or has keyboard focus inside, or there is
    /// nothing to step to.
    /// </summary>
    bool IsAutoPlayHeld { get; }

    /// <summary>Steps one snap point forward (to the first at the end of a strip that does not loop); not <paramref name="animated"/> under reduce motion.</summary>
    void AutoPlayStep(bool animated);
}

/// <summary>
/// The timer of a control that moves by itself (a carousel's autoplay): a tick every <see cref="Interval"/> on the UI thread
/// (<see cref="SkUiGestureSettings.Schedule"/>: the dispatcher on devices, the manual clock in tests), only while the interval
/// is positive, the app has not paused it and the control is shown. A tick steps unless the host holds it, a screen reader
/// is running (content changing under its focus is lost) or the app is in the background; either way the next tick is a
/// full interval later. <see cref="Restart"/> starts the wait again (the control came to rest, or was shown again). The
/// scheduled callback holds the timer weakly: a pending tick keeps no control alive.
/// </summary>
internal sealed class SkUiAutoPlay(ISkUiAutoPlayHost host)
{
    private WeakReference<SkUiAutoPlay>? _self;
    private IDisposable? _timer;

    /// <summary>The time between steps; zero is off.</summary>
    public TimeSpan Interval { get; set; }

    /// <summary>The app's pause.</summary>
    public bool IsPaused { get; set; }

    /// <summary>Whether the control is shown.</summary>
    public bool IsShown { get; set; }

    /// <summary>Whether a tick is scheduled (tests).</summary>
    public bool IsScheduled => _timer is not null;

    private bool ShouldRun => Interval > TimeSpan.Zero && !IsPaused && IsShown;

    /// <summary>Stops a scheduled tick and, when it should run, waits a full interval for the next one.</summary>
    public void Restart()
    {
        Stop();
        if (ShouldRun)
            Schedule();
    }

    /// <summary>Stops a scheduled tick.</summary>
    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private void Schedule()
    {
        var self = _self ??= new WeakReference<SkUiAutoPlay>(this);
        _timer = SkUiGestureSettings.Schedule(Interval, () =>
        {
            if (self.TryGetTarget(out var autoPlay))
                autoPlay.Tick();
        });
    }

    private void Tick()
    {
        _timer = null;
        if (!ShouldRun)
            return;
        if (!host.IsAutoPlayHeld && !SkUiScreenReader.IsRunning && !SkUiAppState.IsInBackground)
            host.AutoPlayStep(animated: !SkUiMotion.IsMotionReduced);
        if (_timer is null && ShouldRun) // the step may have restarted it
            Schedule();
    }
}
