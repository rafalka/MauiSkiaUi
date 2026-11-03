namespace MauiSkiaUi;

/// <summary>What popups are placed in: the window's size, its safe area, and where a drawn surface sits in it (UI thread).</summary>
internal static class SkUiWindowMetrics
{
    /// <summary>
    /// The size in DIPs of the window <paramref name="surfaceRoot"/> is shown in, in the coordinates popups are placed in (those
    /// of <see cref="SurfaceOrigin"/>); zero when unknown.
    /// </summary>
    public static Size WindowSize(SkUiView surfaceRoot) => WindowSize(surfaceRoot.Window, PlatformWindowSize(surfaceRoot));

    /// <summary>
    /// The platform window's size (<paramref name="platformSize"/>) when known, else the frame of <paramref name="window"/>; zero
    /// when neither is. MAUI's frame is in screen points, which on Mac Catalyst (iPad idiom: the app scaled to 77 %) are not
    /// the app's: there the platform window is larger than the frame says.
    /// </summary>
    public static Size WindowSize(Window? window, Size platformSize)
    {
        if (IsKnown(platformSize.Width, platformSize.Height))
            return platformSize;
        return window is { Width: var width, Height: var height } && IsKnown(width, height) ? new Size(width, height) : Size.Zero;
    }

    private static bool IsKnown(double width, double height) => width > 0 && height > 0 && double.IsFinite(width) && double.IsFinite(height);

    /// <summary>The bounds of the platform window <paramref name="surfaceRoot"/> is shown in, DIPs; zero without one (headless).</summary>
    private static Size PlatformWindowSize(SkUiView surfaceRoot)
    {
#if IOS || MACCATALYST
        if (surfaceRoot.Handler?.PlatformView is UIKit.UIView { Window: { } window })
            return new Size(window.Bounds.Width, window.Bounds.Height);
#endif
        _ = surfaceRoot;
        return Size.Zero;
    }

    /// <summary>
    /// The position of <paramref name="surfaceRoot"/> (a view with a platform view) in its window, DIPs; the window's origin
    /// without a connected platform view (headless).
    /// </summary>
    public static Point SurfaceOrigin(SkUiView surfaceRoot) => SkUiDiagnostics.GetWindowOrigin(surfaceRoot) ?? Point.Zero;

    /// <summary>The safe area insets of the window <paramref name="surfaceRoot"/> is shown in (status bar, notch, home indicator), DIPs.</summary>
    public static Thickness SafeAreaInsets(SkUiView surfaceRoot)
    {
#if IOS || MACCATALYST
        if (surfaceRoot.Handler?.PlatformView is UIKit.UIView { Window: { } window })
        {
            var insets = window.SafeAreaInsets;
            return new Thickness(insets.Left, insets.Top, insets.Right, insets.Bottom);
        }
#elif ANDROID
        if (surfaceRoot.Handler?.PlatformView is Android.Views.View { IsAttachedToWindow: true } view
            && AndroidX.Core.View.ViewCompat.GetRootWindowInsets(view) is { } windowInsets)
        {
            var insets = windowInsets.GetInsets(AndroidX.Core.View.WindowInsetsCompat.Type.SystemBars() | AndroidX.Core.View.WindowInsetsCompat.Type.DisplayCutout());
            var density = view.Resources?.DisplayMetrics?.Density ?? 1;
            if (insets is not null)
                return new Thickness(insets.Left / density, insets.Top / density, insets.Right / density, insets.Bottom / density);
        }
#endif
        _ = surfaceRoot;
        return default;
    }
}
