namespace MauiSkiaUi;

/// <summary>What popups are placed in: the window's size, its safe area, and where a drawn surface sits in it (UI thread).</summary>
internal static class SkUiWindowMetrics
{
    /// <summary>The window's size in DIPs (zero when unknown).</summary>
    public static Size WindowSize(Window? window) =>
        window is { Width: > 0 and var width, Height: > 0 and var height } && double.IsFinite(width) && double.IsFinite(height)
            ? new Size(width, height)
            : Size.Zero;

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
