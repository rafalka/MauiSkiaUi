using Android.App;
using Android.Content.PM;
using Android.OS;

namespace MauiSkiaUiDemo;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
	/// <summary>
	/// Scripted runs: string extras named <c>SKUI_*</c> become environment variables before the app starts, e.g.
	/// <c>adb shell am start -n …/crc….MainActivity -e SKUI_DEMO_ROUTE effects-stress -e SKUI_EFFECTS_STRESS matrix</c>
	/// (environment variables cannot be set for an Android app from outside).
	/// </summary>
	protected override void OnCreate(Bundle? savedInstanceState)
	{
		if (Intent?.Extras is { } extras)
			foreach (var key in extras.KeySet() ?? [])
				if (key.StartsWith("SKUI_", StringComparison.Ordinal) && extras.GetString(key) is { } value)
					System.Environment.SetEnvironmentVariable(key, value);
		base.OnCreate(savedInstanceState);
	}
}
