namespace MauiSkiaUi;

/// <summary>
/// Takes the platform's Back for an open modal sheet while it is the topmost modal pane of its surface: Android's back button and
/// gesture through the activity's back dispatcher (ahead of MAUI's navigation, which registered first). Elsewhere there is no Back to
/// take: Escape reaches the sheet through keyboard focus.
/// </summary>
internal static partial class SkUiSheetBackHandler
{
    /// <summary>Starts taking Back for <paramref name="sheet"/>; disposing stops. <c>null</c> where the platform has no Back.</summary>
    public static IDisposable? Register(SkUiSheet sheet) => RegisterCore(sheet);

#if ANDROID
    private static IDisposable? RegisterCore(SkUiSheet sheet)
    {
        if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is not AndroidX.Activity.ComponentActivity activity)
            return null;
        var callback = new Callback(sheet, activity.OnBackPressedDispatcher);
        activity.OnBackPressedDispatcher.AddCallback(callback);
        return callback;
    }

    private sealed class Callback(SkUiSheet sheet, AndroidX.Activity.OnBackPressedDispatcher dispatcher)
        : AndroidX.Activity.OnBackPressedCallback(true), IDisposable
    {
        // Weak: the dispatcher lives as long as the activity, and a sheet that went without closing must not stay with it.
        private readonly WeakReference<SkUiSheet> _sheet = new(sheet);

        public override void HandleOnBackPressed()
        {
            if (_sheet.TryGetTarget(out var target) && target.OnBackPressed())
                return;
            // Not the topmost sheet (or gone): the press goes on to whoever registered before.
            Enabled = false;
            try
            {
                dispatcher.OnBackPressed();
            }
            finally
            {
                Enabled = _sheet.TryGetTarget(out _);
            }
        }

        void IDisposable.Dispose()
        {
            Remove();
            base.Dispose();
        }
    }
#else
    private static IDisposable? RegisterCore(SkUiSheet sheet) => null;
#endif
}
