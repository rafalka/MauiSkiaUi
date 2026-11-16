using System.Diagnostics;

namespace MauiSkiaUi;

/// <summary>
/// The light tick a native wheel picker gives as a row passes its middle (<see cref="SkUiWheelsView"/>), through the platform's own
/// feedback so it follows the system's haptics setting and needs no permission: Android's <c>View.PerformHapticFeedback</c>
/// (<c>SegmentFrequentTick</c> on Android 14+, else <c>ClockTick</c>), iOS's and Mac Catalyst's <c>UISelectionFeedbackGenerator</c>;
/// nothing on Windows and headless. At most one tick per frame, so a fast fling does not buzz.
/// </summary>
internal static class SkUiHaptics
{
    /// <summary>The shortest time between two ticks (one frame at 60 Hz).</summary>
    private static readonly long MinimumInterval = Stopwatch.Frequency / 60;

    private static long _lastTick = long.MinValue;

#if IOS || MACCATALYST
    private static UIKit.UISelectionFeedbackGenerator? _generator;
#endif

    /// <summary>Counts ticks instead of giving them (tests, per thread); <c>null</c>: the platform's.</summary>
    [ThreadStatic]
    internal static Action<SkUiView>? TestHook;

    /// <summary>A wheel starts turning: readies the feedback, so the first tick is not late (iOS).</summary>
    public static void Prepare()
    {
#if IOS || MACCATALYST
        if (TestHook is null)
            (_generator ??= new UIKit.UISelectionFeedbackGenerator()).Prepare();
#endif
    }

    /// <summary>A row of a wheel in <paramref name="view"/>'s surface passed the middle: one tick, unless one was given this frame.</summary>
    public static void SelectionTick(SkUiView view)
    {
        var now = Stopwatch.GetTimestamp();
        if (_lastTick != long.MinValue && now - _lastTick < MinimumInterval)
            return;
        _lastTick = now;
        if (TestHook is { } hook)
        {
            hook(view);
            return;
        }
#if ANDROID
        if (SkUiDiagnostics.GetSurfaceRoot(view)?.Handler?.PlatformView is Android.Views.View platformView)
            platformView.PerformHapticFeedback(OperatingSystem.IsAndroidVersionAtLeast(34)
                ? Android.Views.FeedbackConstants.SegmentFrequentTick
                : Android.Views.FeedbackConstants.ClockTick);
#elif IOS || MACCATALYST
        (_generator ??= new UIKit.UISelectionFeedbackGenerator()).SelectionChanged();
#endif
    }

    /// <summary>Forgets the last tick's time (tests: the next tick is not held back).</summary>
    internal static void ResetForTests() => _lastTick = long.MinValue;
}
