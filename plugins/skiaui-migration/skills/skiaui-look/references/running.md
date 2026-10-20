# Running the comparison page

## Opening it from a script

Let the app open the page and pick its options from the environment in debug builds, for example in the app's `AppShell` or first page:

```csharp
#if DEBUG
if (Environment.GetEnvironmentVariable("APP_LOOK_COMPARISON") is { Length: > 0 } look)
{
    var page = new SkUiLookComparisonPage();
    page.SelectLook(look);                                              // a name from the page's picker, ignoring case
    page.ShowAppStyles = Environment.GetEnvironmentVariable("APP_LOOK_APP_STYLES") != "0";
    page.Overlay = Environment.GetEnvironmentVariable("APP_LOOK_OVERLAY") == "1";
    Dispatcher.Dispatch(async () => await Navigation.PushAsync(page));
}
#endif
```

Stock look names: `Default`, `Android (Material 2)`, `Android (Material 3)`, `iOS`, `Mac Catalyst`, `Windows`, and the names passed to `AddLook`.

## Per platform

Wait about 10 seconds after launch for the first report, then take the screenshot before the app is stopped.

**iOS simulator** (environment variables take the `SIMCTL_CHILD_` prefix; `--console-pty` streams the console):

```bash
dotnet build -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64
xcrun simctl install booted bin/Debug/net10.0-ios/iossimulator-arm64/MyApp.app
SIMCTL_CHILD_APP_LOOK_COMPARISON=iOS SIMCTL_CHILD_APP_LOOK_APP_STYLES=0 \
  xcrun simctl launch --console-pty --terminate-running-process booted com.example.myapp > ios.log 2>&1 &
sleep 12; xcrun simctl io booted screenshot ios.png
```

**Android emulator** (environment variables cannot be passed to `am start`: pass intent extras and copy them into the environment in `MainActivity.OnCreate` before `base.OnCreate`, or read the extras directly):

```bash
dotnet build -f net10.0-android -t:Install
adb logcat -c
adb shell am start -S -n com.example.myapp/<activity> -e APP_LOOK_COMPARISON "'Android (Material 2)'" -e APP_LOOK_APP_STYLES 0
sleep 12; adb logcat -d | grep SkUiLookComparison > android.log; adb exec-out screencap -p > android.png
```

Build with `-p:UseMaterial3=true` to compare the Material 3 look, and use `-t:Rebuild` when switching it on or off: an incremental build can keep the old value.

**Mac Catalyst** (run the executable inside the bundle to keep the environment and the console):

```bash
dotnet build -f net10.0-maccatalyst
APP_LOOK_COMPARISON="Mac Catalyst" APP_LOOK_APP_STYLES=0 \
  "bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/MyApp.app/Contents/MacOS/MyApp" > mac.log 2>&1 &
```

Capture the window with `screencapture -l <window id>` (find the id with `CGWindowListCopyWindowInfo` for the app's process) or ask the user for a screenshot.

**Windows**: the console is not shown for a packaged WinUI app; press "Copy report" and paste the report, or write `page.Report` to a file from the app.

## Reading the logs

```bash
python3 "${CLAUDE_SKILL_DIR}/scripts/look_report.py" ios.log android.log mac.log
```

It prints a table per run (look, platform, idiom, options) with the differing rows first and the size differences in DIPs. Without `CLAUDE_SKILL_DIR`, run it from this skill's `scripts` folder.
