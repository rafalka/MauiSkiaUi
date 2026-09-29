using Android.App;
using Android.Content.PM;
using Android.OS;

namespace MauiSkiaUi.DeviceTests;

/// <summary>
/// Launch with extras to run automatically, e.g.
/// <c>adb shell am start -n com.rkdevel.skiauidevicetests/skiauidevicetests.MainActivity --ez autorun true --ez exit true --es scenarios ButtonsClicked</c>.
/// </summary>
[Activity(Name = "skiauidevicetests.MainActivity", Theme = "@style/Maui.SplashTheme", MainLauncher = true, Exported = true, LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        if (Intent?.Extras is { } extras)
            DeviceTestOptions.Current = DeviceTestOptions.FromValues(extras.GetBoolean("autorun"), extras.GetBoolean("exit"), extras.GetString("scenarios"));
        base.OnCreate(savedInstanceState);
    }
}
