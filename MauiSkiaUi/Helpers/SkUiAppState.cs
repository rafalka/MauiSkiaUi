namespace MauiSkiaUi;

/// <summary>
/// Whether the app is in the background now (iOS and Mac Catalyst: the application state; Android: the process is neither
/// in the foreground nor visible). Read when it matters (an autoplay step), not followed; Windows and headless code report
/// the foreground.
/// </summary>
internal static class SkUiAppState
{
    /// <summary>Override for tests (per thread): <c>true</c> or <c>false</c> instead of the system's state.</summary>
    [ThreadStatic]
    internal static bool? BackgroundOverride;

    /// <summary>Whether the app is in the background.</summary>
    public static bool IsInBackground => BackgroundOverride ?? IsSystemInBackground();

    private static bool IsSystemInBackground()
    {
#if IOS || MACCATALYST
        return UIKit.UIApplication.SharedApplication.ApplicationState == UIKit.UIApplicationState.Background;
#elif ANDROID
        var info = new Android.App.ActivityManager.RunningAppProcessInfo();
        Android.App.ActivityManager.GetMyMemoryState(info);
        return info.Importance > Android.App.Importance.Visible;
#else
        return false;
#endif
    }
}
