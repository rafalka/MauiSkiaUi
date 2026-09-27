using System.Diagnostics;

namespace MauiSkiaUi;

/// <summary>
/// App-wide gesture thresholds (DIPs / time), shared by SkUi* and Core recognizers. Values apply to gestures that
/// start after a change.
/// </summary>
public static class SkUiGestureSettings
{
    /// <summary>Movement (DIPs) before a press stops being a tap and pans / scrolls may claim it.</summary>
    public static double TouchSlop { get; set; } = 10;

    /// <summary>
    /// Delay before a contested press (e.g. a button inside a scroll view) shows its pressed state, so starting a
    /// scroll does not flash buttons. Uncontested presses show immediately.
    /// </summary>
    public static TimeSpan PressDelay { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>Hold duration for a long press.</summary>
    public static TimeSpan LongPressDuration { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Maximum time between two taps of a double tap (also the delay of a single tap when double taps are handled).</summary>
    public static TimeSpan DoubleTapTimeout { get; set; } = TimeSpan.FromMilliseconds(300);

    /// <summary>Maximum distance (DIPs) between the two taps of a double tap.</summary>
    public static double DoubleTapSlop { get; set; } = 40;

    /// <summary>Default swipe distance (DIPs); a fast flick (<see cref="SwipeVelocity"/>) also counts.</summary>
    public static double SwipeThreshold { get; set; } = 100;

    /// <summary>Release velocity (DIPs / s) that completes a swipe shorter than its threshold.</summary>
    public static double SwipeVelocity { get; set; } = 800;

    /// <summary>Minimum release velocity (DIPs / s) that starts a scroll fling.</summary>
    public static double FlingMinimumVelocity { get; set; } = 40;

    /// <summary>Maximum fling velocity (DIPs / s).</summary>
    public static double FlingMaximumVelocity { get; set; } = 3000;

    /// <summary>Timer override for tests (per thread).</summary>
    [ThreadStatic]
    internal static Func<TimeSpan, Action, IDisposable>? Scheduler;

    /// <summary>
    /// Runs <paramref name="action"/> on the UI thread after <paramref name="delay"/>; <c>null</c> when no timer
    /// source exists (headless code without a MAUI dispatcher), in which case time-based gestures do not fire.
    /// </summary>
    internal static IDisposable? Schedule(TimeSpan delay, Action action)
    {
        if (Scheduler is { } scheduler)
            return scheduler(delay, action);
        if (Dispatcher.GetForCurrentThread() is not { } dispatcher)
            return null;
        var handle = new DispatchedTimer(action);
        dispatcher.DispatchDelayed(delay, handle.Run);
        return handle;
    }

    /// <summary>Whether <see cref="Schedule"/> can run timers on this thread.</summary>
    internal static bool CanSchedule => Scheduler is not null || Dispatcher.GetForCurrentThread() is not null;

    private static readonly Stopwatch Clock = Stopwatch.StartNew();

    /// <summary>Monotonic time used when a pointer sample carries no timestamp.</summary>
    internal static TimeSpan Now => Clock.Elapsed;

    private sealed class DispatchedTimer(Action action) : IDisposable
    {
        private bool _cancelled;
        public void Run() { if (!_cancelled) action(); }
        public void Dispose() => _cancelled = true;
    }
}
