namespace MauiSkiaUi;

/// <summary>
/// Whether a screen reader is running now: VoiceOver on iOS and Mac Catalyst, a touch-exploration service (TalkBack) on
/// Android, a UI Automation client listening for focus changes (Narrator, or another assistive client) on Windows. Read when
/// it matters (an autoplay step), not followed.
/// </summary>
internal static class SkUiScreenReader
{
    /// <summary>Override for tests (per thread): <c>true</c> or <c>false</c> instead of the system's state.</summary>
    [ThreadStatic]
    internal static bool? Override;

    /// <summary>Whether a screen reader is running.</summary>
    public static bool IsRunning => Override ?? IsSystemRunning();

#if ANDROID
    private static Android.Views.Accessibility.AccessibilityManager? _manager;
#endif

    private static bool IsSystemRunning()
    {
#if IOS || MACCATALYST
        return UIKit.UIAccessibility.IsVoiceOverRunning;
#elif ANDROID
        // Looked up again if something disposed the shared peer (as the input method's, issue #140).
        return SkUiAndroidServices.Get(ref _manager, Android.App.Application.Context, Android.Content.Context.AccessibilityService)
            is { IsEnabled: true, IsTouchExplorationEnabled: true };
#elif WINDOWS
        try
        {
            return Microsoft.UI.Xaml.Automation.Peers.AutomationPeer.ListenerExists(
                Microsoft.UI.Xaml.Automation.Peers.AutomationEvents.AutomationFocusChanged);
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
