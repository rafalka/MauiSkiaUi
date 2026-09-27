using Foundation;
using MauiSkiaUi.Benchmarks;

namespace MauiSkiaUiBench;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp()
    {
        // Launch arguments, e.g. `xcrun simctl launch --console <udid> com.rkdevel.skiauibench --autorun --exit --runs 6`.
        BenchLaunch.Options = BenchArgs.Parse(NSProcessInfo.ProcessInfo.Arguments.Skip(1).ToArray());
        return MauiProgram.CreateMauiApp();
    }
}
