namespace MauiSkiaUi;

/// <summary>
/// App-wide motion preference for state-change transitions (<see cref="SkUiLook.GetTransition"/>). By default it
/// follows the operating system's reduce-motion setting: iOS / Mac Catalyst "Reduce Motion", Android "Remove
/// animations" (animator duration scale 0), Windows "Animation effects" off. Activity animations (spinners, the
/// indeterminate progress bar) keep running: they show that work is in progress.
/// </summary>
public static class SkUiMotion
{
    /// <summary>
    /// <c>true</c> or <c>false</c> overrides the system setting; <c>null</c> (default) follows it.
    /// </summary>
    public static bool? ReduceMotion { get; set; }

    /// <summary>Whether state-change transitions are skipped (they jump to the new state).</summary>
    public static bool IsMotionReduced => ReduceMotion ?? IsSystemMotionReduced();

#if WINDOWS
    private static Windows.UI.ViewManagement.UISettings? _settings;
#endif

    private static bool IsSystemMotionReduced()
    {
#if IOS || MACCATALYST
        return UIKit.UIAccessibility.IsReduceMotionEnabled;
#elif ANDROID
        // Android 8+: false when the user removed animations (Accessibility) or set the animator scale to 0.
        return OperatingSystem.IsAndroidVersionAtLeast(26) && !Android.Animation.ValueAnimator.AreAnimatorsEnabled();
#elif WINDOWS
        try
        {
            return !(_settings ??= new Windows.UI.ViewManagement.UISettings()).AnimationsEnabled;
        }
        catch (Exception)
        {
            return false;
        }
#else
        return false;
#endif
    }
}
