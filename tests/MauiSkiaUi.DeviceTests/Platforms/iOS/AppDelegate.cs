using Foundation;

namespace MauiSkiaUi.DeviceTests;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp()
    {
        // Launch arguments, e.g. `xcrun simctl launch --console <udid> com.rkdevel.skiauidevicetests --autorun --exit`.
        DeviceTestOptions.Current = DeviceTestOptions.Parse(NSProcessInfo.ProcessInfo.Arguments.Skip(1).ToArray());
        return MauiProgram.CreateMauiApp();
    }
}
