using MauiSkiaUi.LeakTests;

namespace MauiSkiaUi.DeviceTests;

/// <summary>
/// State of the MAUI SkiaSharp surface (software roots), with real handlers (#69): drawn views whose handlers are created
/// under a hidden or a disabled container must show and take input once the container is shown or enabled. The surface
/// keeps its own state (no MAUI parent, so nothing inherited from ancestors goes stale); the root's native view hides
/// with its ancestors.
/// </summary>
internal static class SurfaceStateCheck
{
    public static async Task<LeakResult> RunAsync()
    {
        var navigation = Shell.Current?.Navigation ?? Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation;
        if (navigation is null)
            return new LeakResult("SurfaceState", LeakStatus.Fail, "No navigation to host the check page.");
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var problems = new List<string>();
        ContentPage? page = null;
        try
        {
            var shownLabel = new SkUiLabel { Text = "Created visible" };
            var hiddenLabel = new SkUiLabel { Text = "Created hidden" };
            var disabledButton = new SkUiButton { Text = "Created disabled" };
            var hidden = new ContentView { IsVisible = false, Content = hiddenLabel };
            var disabled = new ContentView { IsEnabled = false, Content = disabledButton };
            page = new ContentPage
            {
                Title = "Surface state", BackgroundColor = Colors.White,
                Content = new VerticalStackLayout { Spacing = 10, Padding = new Thickness(10), Children = { shownLabel, hidden, disabled } }
            };
            await navigation.PushAsync(page, animated: false);
            await Task.Delay(500);

            // The surface keeps its own state (shown, enabled); the root's native view hides with its ancestors.
            void Check(string step, SkUiView view, bool rootShown)
            {
                if ((view.Handler as SkUiViewHandler)?.MauiSurface is not { } surface)
                {
                    problems.Add($"{step}: {view.GetType().Name} has no MAUI surface");
                    return;
                }
                if (((IView)surface).Visibility != Visibility.Visible || !IsNativeShown(surface.Handler?.PlatformView))
                    problems.Add($"{step}: {view.GetType().Name} surface is hidden");
                if (!surface.IsEnabled || !IsNativeEnabled(surface.Handler?.PlatformView))
                    problems.Add($"{step}: {view.GetType().Name} surface is disabled");
                var root = view.Handler?.ContainerView ?? view.Handler?.PlatformView;
                if (IsNativeShown(root) != rootShown)
                    problems.Add($"{step}: {view.GetType().Name} root view {(rootShown ? "is hidden" : "shows")}");
            }

            Check("first layout", shownLabel, rootShown: true);
            Check("first layout", disabledButton, rootShown: true);
            // The case of #69: the label's handler and surface are created while its container is hidden. Without them
            // here, "container shown" would check a surface created while visible, and pass without testing anything.
            if (hiddenLabel.Handler is null)
                problems.Add("first layout: the hidden container's SkUiLabel has no handler, so no surface was created under a hidden ancestor");
            else
                Check("first layout", hiddenLabel, rootShown: false);

            hidden.IsVisible = true;
            disabled.IsEnabled = true;
            await Task.Delay(300);
            Check("container shown", hiddenLabel, rootShown: true);
            Check("container enabled", disabledButton, rootShown: true);

            hidden.IsVisible = false;
            disabled.IsEnabled = false;
            await Task.Delay(300);
            Check("container hidden again", hiddenLabel, rootShown: false);
            Check("container disabled again", disabledButton, rootShown: true);
        }
        catch (Exception exception)
        {
            problems.Add($"{exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            if (page is not null)
                await navigation.PopAsync(animated: false);
        }
        return new LeakResult("SurfaceState", problems.Count == 0 ? LeakStatus.Pass : LeakStatus.Fail,
            problems.Count == 0 ? "surfaces created under a hidden / disabled container show and take input once it is shown / enabled" : string.Join(" · ", problems.Take(8)),
            watch.Elapsed.TotalSeconds);
    }

    private static bool IsNativeShown(object? platformView) => platformView switch
    {
#if ANDROID
        Android.Views.View view => view.Visibility == Android.Views.ViewStates.Visible,
#elif IOS || MACCATALYST
        UIKit.UIView view => !view.Hidden,
#elif WINDOWS
        Microsoft.UI.Xaml.UIElement view => view.Visibility == Microsoft.UI.Xaml.Visibility.Visible,
#endif
        _ => false
    };

    /// <summary>Whether the platform delivers touches to the view (Android skips a disabled view's touch listener).</summary>
    private static bool IsNativeEnabled(object? platformView) => platformView switch
    {
#if ANDROID
        Android.Views.View view => view.Enabled,
#elif IOS || MACCATALYST
        UIKit.UIView view => view.UserInteractionEnabled,
#endif
        _ => true
    };
}
