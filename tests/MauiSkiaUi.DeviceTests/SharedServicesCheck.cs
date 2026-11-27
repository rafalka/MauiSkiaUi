using System.Diagnostics;
using MauiSkiaUi.LeakTests;

namespace MauiSkiaUi.DeviceTests;

/// <summary>
/// Android system services shared with the app (#140): the input method manager and the accessibility manager are one .NET peer
/// for the whole app, the autofill manager (API 26+) one per context (the JNI identity map), and code that disposes its copy
/// disposes the one the library kept (MAUI 10.0.130 to 11.0.10 <c>using</c> the input method manager on every native field's focus
/// change). The check focuses a drawn field (the surface's text input looks the input method and autofill managers up), then a
/// native <see cref="Entry"/> and back, reads the screen reader state (which keeps the accessibility manager), disposes separately
/// fetched peers of the three services as such code does, checks the kept peers were those, and focuses the drawn field again:
/// each service is looked up again and the keyboard comes up, with no <see cref="ObjectDisposedException"/> (before the fix, from
/// the keyboard's posted <c>showSoftInput</c>: the app crashed). Passes trivially elsewhere.
/// </summary>
internal static class SharedServicesCheck
{
    private const string Name = "SharedServices";

    public static async Task<LeakResult> RunAsync()
    {
#if ANDROID
        var navigation = Shell.Current?.Navigation ?? Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation;
        if (navigation is null)
            return new LeakResult(Name, LeakStatus.Fail, "No navigation to host the check page.");
        var watch = Stopwatch.StartNew();
        var problems = new List<string>();
        var notes = new List<string>();
        ContentPage? page = null;
        try
        {
            var drawn = new SkUiEntry { Placeholder = "Drawn" };
            var native = new Entry { Placeholder = "Native" };
            var root = new SkUiContentView { Content = new SkUiVerticalStackLayout { Padding = new Thickness(20), Children = { drawn } } };
            page = new ContentPage
            {
                Title = "Shared services", BackgroundColor = Colors.White,
                Content = new VerticalStackLayout { Spacing = 10, Children = { root, native } }
            };
            await navigation.PushAsync(page, animated: false);
            await Task.Delay(500);
            if (root.Handler?.PlatformView is not SkUiOverlayContainer container || container.TextInput is not { } input)
                return new LeakResult(Name, LeakStatus.Fail, "The root has no overlay container or text input.");

            if (!drawn.Focus())
                problems.Add("the drawn field did not take focus");
            await Task.Delay(800);
            var kept = input.Imm;
            drawn.Unfocus();
            await Task.Delay(300);

            // A native field's keyboard (MAUI's ShowSoftInput / HideSoftInput, which dispose the shared peer in affected versions).
            native.Focus();
            await Task.Delay(800);
            native.Unfocus();
            await Task.Delay(500);
            notes.Add("after a native field " + (kept.IsAlive() ? "the kept peer is alive (MAUI did not dispose it)" : "the kept peer was disposed"));

            // As any code that `using`s the service does, whichever MAUI version runs. The screen reader's manager is kept on
            // first read, and the autofill manager on the first focus (API 26+): both must be the peers disposed here.
            _ = SkUiScreenReader.IsRunning;
            var keptAccessibility = KeptField(typeof(SkUiScreenReader), null, "_manager");
            var keptAutofill = KeptField(typeof(SkUiAndroidTextInput), input, "_autofill");
            var context = container.Context;
            context?.GetSystemService(Android.Content.Context.InputMethodService)?.Dispose();
            context?.GetSystemService(Android.Content.Context.AccessibilityService)?.Dispose();
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
                context?.GetSystemService(Java.Lang.Class.FromType(typeof(Android.Views.Autofill.AutofillManager)))?.Dispose();
            if (kept.IsAlive())
                problems.Add("disposing a separately fetched input method manager left the kept peer alive (the check tests nothing)");
            if (keptAccessibility is null || keptAccessibility.IsAlive())
                problems.Add("the screen reader kept no accessibility manager, or disposing a separately fetched one left it alive");
            if (OperatingSystem.IsAndroidVersionAtLeast(26) && (keptAutofill is null || keptAutofill.IsAlive()))
                problems.Add("the text input kept no autofill manager, or disposing a separately fetched one left it alive");

            _ = SkUiScreenReader.IsRunning;
            if (!KeptField(typeof(SkUiScreenReader), null, "_manager").IsAlive())
                problems.Add("the screen reader did not look its accessibility manager up again");
            // Attach restarts input, asks autofill (API 26+) and posts showSoftInput, each on a looked-up service.
            if (!drawn.Focus())
                problems.Add("the drawn field did not take focus again");
            await Task.Delay(1000); // the posted showSoftInput runs
            if (!input.Imm.IsAlive())
                problems.Add("the text input has no live input method manager");
            if (OperatingSystem.IsAndroidVersionAtLeast(26) && !KeptField(typeof(SkUiAndroidTextInput), input, "_autofill").IsAlive())
                problems.Add("the text input did not look its autofill manager up again");
            var imm = context?.GetSystemService(Android.Content.Context.InputMethodService) as Android.Views.InputMethods.InputMethodManager;
            var active = imm is { IsActive: true, IsAcceptingText: true };
            if (!active)
                problems.Add("the input method is not active or does not accept text for the drawn field");
            notes.Add($"refocused: input method active {active}");
            drawn.Unfocus();
            await Task.Delay(300);
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
        return new LeakResult(Name, problems.Count == 0 ? LeakStatus.Pass : LeakStatus.Fail,
            problems.Count == 0 ? string.Join("; ", notes) : string.Join(" · ", problems.Take(8)), watch.Elapsed.TotalSeconds);
#else
        await Task.CompletedTask;
        return new LeakResult(Name, LeakStatus.Pass, "Android only");
#endif
    }

#if ANDROID
    /// <summary>The service peer a private field keeps (static when <paramref name="target"/> is <c>null</c>).</summary>
    private static Java.Lang.Object? KeptField(Type type, object? target, string name) =>
        type.GetField(name, System.Reflection.BindingFlags.NonPublic | (target is null ? System.Reflection.BindingFlags.Static : System.Reflection.BindingFlags.Instance))
            ?.GetValue(target) as Java.Lang.Object;
#endif
}
