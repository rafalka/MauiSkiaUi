using Android.App;
using Android.Content.PM;
using Android.OS;
using MauiSkiaUi.Benchmarks;

namespace MauiSkiaUiBench;

/// <summary>
/// Launch with extras to run automatically, e.g.
/// <c>adb shell am start -n com.rkdevel.skiauibench/skiauibench.MainActivity --ez autorun true --es scenarios core-labels --ei runs 6</c>.
/// </summary>
[Activity(Name = "skiauibench.MainActivity", Theme = "@style/Maui.SplashTheme", MainLauncher = true, Exported = true, LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        var extras = Intent?.Extras;
        if (extras is not null)
        {
            BenchLaunch.Options = BenchArgs.FromValues(
                extras.GetString("scenarios"),
                extras.ContainsKey("runs") ? extras.GetInt("runs") : null,
                extras.ContainsKey("warmup") ? extras.GetInt("warmup") : null,
                extras.GetBoolean("autorun"),
                extras.GetString("label"));
        }
        base.OnCreate(savedInstanceState);
    }
}
