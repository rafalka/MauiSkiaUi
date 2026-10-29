---
name: skiaui-look
description: Checks that SkiaUi's drawn controls (SkiaUi.Maui, SkUi* types) look like the .NET MAUI native controls they replace, on each platform, and builds an app's own look (a SkUiLook subclass) when they should differ or when the app's styles change them. Adds SkUiLookComparisonPage to the app, runs it on simulators, emulators and Mac Catalyst, reads its size report and screenshots, and tunes the look's sizes and shapes until every row matches. Use when asked whether SkiaUi controls match MAUI's, which platform look to use, why a drawn control is bigger or smaller than the native one, or to create or adjust a custom or brand look.
argument-hint: "[platform | look name]"
---

# Match SkiaUi's look to the app's MAUI controls

A look (`SkUiLook`, usually a `DefaultSkUiLook` subclass) owns the shapes, sizes, motion and default text size of every drawn control; colors come from the color scheme (`SkUiColorScheme`). SkiaUi ships one look per platform, each measured against MAUI's native controls on that platform:

| Look | Matches |
| --- | --- |
| `IosSkUiLook` | MAUI on iOS 26 |
| `MacSkUiLook` | MAUI on Mac Catalyst in the iPad idiom (MAUI's default, `UIDeviceFamily` 2: the iOS controls, scaled to 77% by the system) |
| `AndroidSkUiLook` | MAUI on Android as it ships (Material 2, Material Components) |
| `AndroidMaterial3SkUiLook` | MAUI on Android with `<UseMaterial3>true</UseMaterial3>` |
| `WindowsSkUiLook` | MAUI on Windows 11 (WinUI 3, Fluent) |
| `DefaultSkUiLook` | the same on every platform (no platform's) |

`DefaultSkUiLook.ForCurrentPlatform()` picks the platform's look (on Android, the Material 3 one when the app turns MAUI's Material 3 on). Set the look once at startup: `UseSkiaUi(options => options.Look = …)`.

## Rules

1. **Measure, don't guess.** Every size you change comes from the comparison page's report or a screenshot of the native control on the device, never from design guidelines alone: MAUI's controls are not always the guidelines' (iOS buttons have square corners, Android's check box sits in 32 dp).
2. **Compare on the platform.** The native column shows the platform the app runs on. Compare the iOS look on iOS, the Material looks on Android.
3. **Change the look, not the controls.** Sizes, shapes and motion go into a look subclass; colors into a color scheme; per-screen differences into the app's `SkUi*` styles. Never set sizes on every control instance to make one row match.
4. **Override only what differs.** Derive from the closest stock look and override the members you measured (see [look-knobs.md](references/look-knobs.md)). Do not copy a whole look.
5. **Do not invent API.** Use the members in the reference or found in the SkiaUi package. If a difference has no member (a different shape, not a size), report it instead of approximating it.

## Workflow

1. **Setup** (once): the app references `SkiaUi.Maui` and calls `.UseSkiaUi(...)`. Add a way to open the comparison page in debug builds, for example a toolbar item or a debug menu entry:

   ```csharp
   #if DEBUG
   await Navigation.PushAsync(new SkUiLookComparisonPage()        // the app's look (SkUiLook.Current)
       .AddLook("Brand", new BrandLook())                         // the app's own look, if it has one
       .AddRow("Primary button",                                  // the app's own pairs of styled controls
           () => new Button { Text = "Pay", Style = (Style)Application.Current!.Resources["PrimaryButton"] },
           () => new SkUiButton { Text = "Pay", Style = (Style)Application.Current!.Resources["SkPrimaryButton"] }));
   #endif
   ```

   For scripted runs, the page also has `SelectLook(name)`, `ShowAppStyles`, `Overlay` and `UseLookColors`; set them from environment variables the app reads at startup (see [running.md](references/running.md)).
2. **Run on each platform** the app ships (iOS simulator, Android emulator, Mac Catalyst, Windows) with the look under test. [running.md](references/running.md) has the launch and log commands.
3. **Read the report.** After each layout the page writes one line per row to the console:

   ```text
   [SkUiLookComparison] look=iOS platform=iOS idiom=Phone appStyles=False lookColors=False
   [SkUiLookComparison] Switch (on): native=63x28 skiaui=63x28 same
   [SkUiLookComparison] Button: native=78x36 skiaui=74.8x36 DIFFERS
   ```

   Summarize one or more logs with `python3 "${CLAUDE_SKILL_DIR}/scripts/look_report.py" ios.log android.log` (a table per platform, differences first). On Windows, redirect an unpackaged app's standard output to a file, or use the page's "Copy report" button.
4. **Look at the screenshots**, side by side and with "Overlay" on: sizes can match while shapes differ (a capsule thumb against a round one, a box inset in its touch area). Zoom into a control and measure in device pixels (divide by the screen scale) when a shape's part matters.
5. **Decide per row** with [diagnosing.md](references/diagnosing.md): the stock look is wrong (fix the look; in SkiaUi itself, also its tests and docs), the app's styles change the native control (add the same setters to a `SkUi*` style, or a look member when every control of that type changes), or the difference is a shape SkiaUi does not draw yet (report it).
6. **Write or adjust the look:** derive from the closest stock look and override the members from [look-knobs.md](references/look-knobs.md). Text sizes come from `DefaultFontSize`; Android's taller text lines from `TextIncludesFontPadding`; Apple's tighter text from `AppliesFontTracking`.
7. **Repeat** steps 2–6 until every row says `same` (sizes within 0.5 DIP) and the overlay shows the same shapes, on every platform. Known differences by design: the Stepper on Android and Windows (SkiaUi keeps its segmented stepper rather than MAUI's two separate buttons). Native text sizes are rounded to the pixel (Android) or half point (Mac Catalyst), so text rows can differ by that; say so rather than tuning padding to hide it.
8. **Report** the look you chose or wrote, the rows that still differ and why, and the platforms you did not check.

## Things to know

- Each row's SkiaUi control is its own surface (the page compares one control at a time); real screens put one surface around a region.
- Values a control reads when it is created (button padding, default colors) come from the look current at creation; the page creates the drawn controls under the selected look. Text sizes and shapes follow a subtree's own look (`SkUiView.Look`) when they are laid out.
- Mac Catalyst and Windows round native sizes to whole points, so a row can differ by under 0.5 there and still be right.
- On Windows, WinUI's check box and radio button are at least 120 wide; MAUI maps `MinimumWidthRequest` to that `MinWidth`, so a style's `MinimumWidthRequest="44"` makes the native check box 44 wide. `WindowsSkUiLook` gives way the same way, so copy the setter to the `SkUi*` style.
