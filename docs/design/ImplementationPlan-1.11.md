# SkiaUi 1.11 implementation plan: .NET 11 and MAUI 11

**Goal:** every package (`SkiaUi.Maui`, `.CommunityToolkit`, `.Markdown`, `.Markdown.Math`) targets `net11.0-*` with MAUI 11 next to `net10.0-*` with MAUI 10. Apps that stay on .NET 10 keep restoring the net10 assets and never see MAUI 11. No net10 target framework is dropped.

**Why now:** .NET 11 (SDK 11.0.100) and MAUI 11.0.0 went GA on 10 November 2026. STS releases now get 24 months, so .NET 10 (LTS) and .NET 11 both reach end of support on 14 November 2028. Most apps will stay on 10 for a while, and early adopters move to 11.

**Scope:** platform support only: build, test, pack and verify on both lines. New MAUI 11 members (below) go on the parity test's known-gap list and into the [backlog](Roadmap.md#maui-parity) for a later release.

**Baseline:** 1.10.0 (MAUI 10.0.120, SDK 10.0.402, SkiaSharp 4.154.0, Xcode 26.6 / 27.1).

**Status:** Steps 0 (one SDK) and 1 are done on branch `feature/net11-support`. The headless part of Steps 2 and 3 is done: the library builds for `net11.0` with 0 warnings and the headless suite is green on `net10.0` and `net11.0` with MAUI 11.0.10 (findings below). Step 2 is done on macOS: the solution builds with 0 warnings on SDK 11 (net10 Apple app heads on SDK 10), and the net11 demo runs on an iOS 27.2 simulator (CoreCLR, Metal). Windows builds wait for CI (Step 5). Steps 4 (API check script) and 5 (CI, packing, scripts) are done; Step 5 waits for its first CI run, which also builds Windows. Next: Step 6 (docs), then the net10 bump to MAUI 10.0.130.

## Decisions

- **App heads multi-target too:** the demo, samples, device tests and on-device bench target both lines. Device scripts default to net11 and take `--net 10|11`.
- **Release:** `1.11.0-Prerelease01` first, then `1.11.0` after device checks on both runtimes.
- **SkiaSharp stays on 4.154.0 for both lines.** Move to 4.156 (the first release with net11 assets) in a later release, once it is stable.
- **API differences:** use the compiler's `NET11_0_OR_GREATER`, inside small shims rather than in controls. The `SKUI_NET11` define the roadmap planned is not needed.
- **Build switches:** `-p:SkUiNet=10` or `-p:SkUiNet=11` limits a build to one line; SDK 10 builds the net10 line by itself (`Directory.Build.props` reads the SDK's major version from MSBuild's path). `-p:SkUiPlatforms=…` limits it to some platforms (Step 5).

## Facts that shape the plan

These were checked against the release notes, nuget.org and the workload manifests in November 2026.

- **Android:** MAUI 11 raises the minimum Android to **API 24** (we use 21 on net10). The default `net11.0-android` is API 37. iOS 15, Mac Catalyst 17 and Windows 10.0.17763 stay the same.
- **Xcode:** .NET 11 Apple packs (workload set **11.0.100.1**) need **Xcode 27.2**, which needs macOS 26.6 or newer.
- **net10 Apple builds on SDK 11:** SDK 11's workload builds `net10.0-ios` / `maccatalyst` against the **26.0** iOS pack (26.0.11017), not the 27.1 pack 1.10 was built with. Step 0 decides whether that is acceptable.
- **Community Toolkit:** CommunityToolkit.Maui 17.0.0 targets net11 only, and 15.0.1 (the last net10 line) targets net10 only, so the toolkit version is chosen per TFM.
- **SkiaSharp views:** SkiaSharp.Views.Maui.Controls 4.154 has net10 assets only (it needs MAUI 10.0.20 or newer). net11 builds use that net10 assembly on MAUI 11, so it must be checked on devices.
- **Runtime:** CoreCLR replaces Mono on Android, iOS and Mac Catalyst in .NET 11. Runtime, GC and finalizer timing change under the render thread, so device checks run on both runtimes.
- **No blockers in our code:** SkiaUi uses no MAUI internals (no `UnsafeAccessor` or non-public reflection) and no iOS 26+ APIs. The only Android 36 checks are runtime `IsAndroidVersionAtLeast` calls.
- **Behavior changes found by the headless suite** (each handled where it lands, both lines keep their own MAUI's behavior):
  - **.NET 11 converts `double` to `decimal` exactly** (`(decimal)0.1` is `0.1000000000000000055511151231`; .NET 10 rounds to 15 significant digits). `SkUiDecimal.FromDouble` keeps the .NET 10 result on both lines, for a screen reader setting a decimal stepper (both layers) and the numeric entry writing out a large double.
  - **MAUI 11 leaves room for a shape's stroke only when it has one** (dotnet/maui#37319, in 11.0.10): a shape without `Stroke` no longer measures or insets by `StrokeThickness`. `SkUiShapeGeometry.StrokeInset` follows each line on both layers, and a stroke appearing or going measures the shape again on net11.
  - **Effective visibility:** MAUI 11.0.0 dropped it (`IView.Visibility` was the view's own `IsVisible`); 11.0.10 brought it back (dotnet/maui#37546). The net11 floor is 11.0.10, so both lines behave as MAUI 10.
- **New MAUI 11 public API the parity test flags** (net11 known gaps in `MauiApiParityTests.Planned`):
  - `LongPressGestureRecognizer` (on drawn views)
  - `BoxView.Fill`
  - `Editor.ReturnCommand` / `ReturnCommandParameter`
  - the `CancellationToken` overloads of `FadeToAsync` and the other animation extensions
  - `VisualElement.InvalidateStyle` / `VisualStateManager.InvalidateVisualStates`

## Step 0: spike, one SDK or two (decides Steps 1 and 5)

Work in a scratch worktree with `global.json` set to 11.0.100:

1. Build every TFM of `MauiSkiaUi` with Xcode 27.2. Check for Xcode-version errors or warnings on the net10 Apple TFMs, which use the 26.0 pack.
2. Compare the net10 Apple assemblies built this way with the 1.10.0 nupkg using ApiCompat. The public API must be identical.
3. Build the iOS demo for `net10.0-ios` with SDK 10.0.402 and Xcode 27.2 (workload set 10.0.404) against the library built on SDK 11, and run it on a simulator.

Results (13 November 2026, Xcode 27.2, SDK 11.0.100, workload set 11.0.100.1):

1. **Pass.** Every TFM builds with 0 warnings once `NSObject.SuperHandle` (obsolete on net11 Apple) is replaced by our own `objc_super` struct in `SkUiViewHandler`'s accessibility hit test (one code path on both lines; check VoiceOver's explore by touch on devices). The net10 Apple TFMs use the 26.0 packs (26.0.11017), the net11 ones 27.2.11039; net10 / net11 Android target API 36 / 37.
2. **Pass.** Package validation (the SDK's ApiCompat: `EnablePackageValidation`, `PackageValidationBaselineVersion=1.10.3`) finds no difference from 1.10.3 for `net10.0`, Android, iOS and Mac Catalyst, and none between the net10 and net11 assets. The only reports are the Windows-only types (`SkUiAppBuilderExtensions`, `SkUiViewHandler`) because a Mac pack has no Windows assembly; the baseline's Windows framework has to be ignored through the `PackageValidationBaselineFrameworkToIgnore` item (not a property) for Step 4.
3. **Pass.** A minimal net10 iOS app built with SDK 10.0.404 (workload set 10.0.404) and Xcode 27.2 restores the SDK 11-built package from a local feed and runs on an iPhone 18 Pro simulator (iOS 27.2): drawn controls render, taps, the typed stepper and the entry with the software keyboard work, as with the same commit packed by SDK 10. SDK 10's workload set 10.0.404 builds `net10.0-ios` against the same 26.0 pack, so the pack is not a difference between the SDKs. (Install workloads from a folder whose `global.json` selects the SDK meant: `dotnet workload install` under SDK 10 pins SDK 10's band to whatever set it is given.)

**Decision: one SDK for the packages.** `global.json` is on 11.0.100 and builds every package TFM; SDK 10 builds the net10 line by itself (`Directory.Build.props` reads the SDK's major version from MSBuild's path and sets `SkUiNet=10`).

**App heads (found in Step 2):** SDK 11's workload carries the net10 Apple packs for Xcode 26 (26.0.11017), which refuse to build an *app* with Xcode 27.2 ("requires Xcode 26.0"); libraries have no such check. SDK 10's own workload set builds net10 Apple apps with its Xcode 27.2 packs (27.2.10000), as .NET 10 apps do. So on SDK 11 the app heads (`SkUiAppTfms`, `SkUiDeviceTfms`) leave out `net10.0-ios` / `net10.0-maccatalyst`, and those are built with SDK 10 (run `dotnet` from a folder whose `global.json` selects 10.0.402). Skipping the Xcode check instead (`ValidateXcodeVersion=false`) was not chosen: it would test a toolchain no .NET 10 app uses.

Outcome:

- **All pass → one SDK:** `global.json` moves to 11.0.100 and builds every TFM.
- **Any fail → two SDKs:** net10 assets keep being built by SDK 10.0.402 with workload 10.0.404, which is the 1.10.2 toolchain. net11 assets are built by SDK 11, and the pack workflow merges both (Step 5). `global.json` still moves to 11.0.100 for development.

## Step 1: MSBuild, one place for TFMs and versions (done)

- **`Directory.Build.props`, pins:** `Maui10Version` 10.0.120 and `Maui11Version` 11.0.10 (the first servicing release; 11.0.0 went GA with the SDK). The net10 floor stays at 10.0.120; also check it with 10.0.130.
- **`Directory.Build.props`, more pins:** `CommunityToolkitMaui10Version` 15.0.1 / `CommunityToolkitMaui11Version` 17.0.0, and `MicrosoftExtensions10Version` / `11` for the demo's `Logging.Debug`.
- **`Directory.Build.props`, TFM lists**, net11 first and with the OS conditions:
  - `SkUiLibraryTfms`: packages
  - `SkUiAppTfms`: demo, samples
  - `SkUiDeviceTfms`: device tests, bench app (no Windows)
  - `SkUiHeadlessTfms`: tests, benchmark runner
- **`Directory.Build.props`, per TFM:** `MauiVersion`, `CommunityToolkitMauiVersion` and `MicrosoftExtensionsVersion` come from these pins. `SupportedOSPlatformVersion` is Android 21 on net10 and 24 on net11, iOS 15, Mac Catalyst 17 and Windows 10.0.17763. This replaced the block copied into 9 project files.
- **MSBuild escaping:** MSBuild escapes the `;` in a property function's result, so the lists are wrapped in `$([MSBuild]::Unescape(...))`.
- **Projects:** every project uses one of the lists. The `SkiaSharp.Views.Maui.Controls` reference is conditioned on "has a platform" instead of `!= 'net10.0'`.
- **Benchmarks:** both benchmark projects fall back to the old net10 lists when `Directory.Build.props` predates the lists, so `scripts/bench.sh --baseline` still builds commits older than 1.11. `bench.sh` gets `--net 10|11` (default 10, or `SKUI_NET`).

## Step 2: make net11 build clean

Build every net11 TFM and fix MAUI 11 obsoletions and API changes until both lines have 0 warnings, including the trim / AOT analyzers (`IsAotCompatible`). Expected:

- `Java.Lang.Object.JavaFinalize` is obsolete.
- Windows `MauiWinUIApplication` members are obsolete.
- MAUI 11 removed obsolete Graphics projects.
- Anything the SkiaSharp net10 view assembly hits on MAUI 11.

Each difference becomes a shim with a short comment. No control-level `#if`.

## Step 3: tests

- The headless suite runs on `net10.0` and `net11.0` (`dotnet test` runs both; `-f net11.0` runs one).
- `MauiApiParityTests`: on net11, a known-gap list under `NET11_0_OR_GREATER` holds the new MAUI 11 members, each linked to the backlog entry. `ImplicitStyleTests` and the memory-leak scenarios run on both lines.
- When a test fails only under MAUI 11, find the behavior change and fix the cause. Never loosen the assertion.

## Step 4: API compatibility check (done)

- `scripts/api_compat.sh` packs the four packages with `-p:SkUiApiBaseline=<version>` (default: the newest release tag), which turns on the SDK's package validation (`Directory.Build.props`). It checks two things:
  - Nothing public from the baseline release on nuget.org was removed (the 1.x API promise).
  - Each framework keeps the API of the older one it is compatible with: net11.0-ios has everything net10.0-ios has, and so on. Not strict, since the platform TFMs add platform types (`SkUiViewHandler`, `SkUiAppBuilderExtensions`, Android's `Resource`) to `net10.0` / `net11.0` by design, so an API added only on net11 would pass; the parity test's net11 gap list is what keeps the lines equal.
- A pack holds the TFMs its OS builds, so the baseline's frameworks that pack has no TFM for are ignored (`PackageValidationBaselineFrameworkToIgnore`, with `%2A` as the wildcard: a bare `*` in `Include` is a file glob and drops the item). CI runs it on macOS (net10.0, Android, iOS, Mac Catalyst) and on Windows (the Windows TFMs).
- Result (macOS, 13 November 2026): no differences for all four packages against 1.10.3 (about 7 minutes, a full Release pack). Making `SkUiMarkdownMath` internal fails it with `CP0001` on each baseline framework.
- The standalone `Microsoft.DotNet.ApiCompat.Tool` on the merged nupkg was not needed: package validation resolves the Apple references through the build.

## Step 5: CI, packing, scripts (done, waiting for the first CI run)

- **`Directory.Build.props`:** `-p:SkUiPlatforms=ios+maccatalyst` (joined by `+`, since `,` and `;` split `-p` values) keeps only those platforms' TFMs in the shared lists. CI jobs use it instead of a `TargetFrameworks` override: an escaped list reaches the inner builds as one TFM (NETSDK1046), and restore needs no other platform's workload (NETSDK1147).
- **`.github/actions/setup-dotnet-maui`:**
  - `sdk-version` (e.g. 10.0.404) rewrites `global.json` for net10-only jobs; `Directory.Build.props` then builds the net10 line by itself.
  - The default workload set follows the SDK: 11.0.100.1 on SDK 11, 10.0.404 on SDK 10.
  - The Xcode probe uses the SDK's own line (net11 Apple TFMs on SDK 11); the pin printout shows both MAUI and toolkit pins.
- **`ci.yml`:**
  - Headless: the library builds for `net10.0` and `net11.0`; `dotnet test` runs both TFMs (one trx each).
  - Apple: SDK 11 / Xcode 27.2 builds both lines' libraries and the net11 iOS demo; SDK 10 / Xcode 27.2 (10.0.404) and SDK 10 / Xcode 26.6 (10.0.401, `macos-26`) build the net10 libraries and the net10 iOS demo, the toolchains .NET 10 users have.
  - Mac Catalyst device tests (Native AOT) run on net11 (SDK 11) and net10 (SDK 10).
  - Android builds each line on SDK 11 (API 36 / build-tools 36.0.0 and API 37 / 37.0.0), each also against the newest AndroidX.Core.
  - Windows builds both lines' Windows TFMs.
- **`nuget-pack.yml` / `nuget-publish.yml`:** one SDK, so the macOS pack holds both lines and the Windows pack both Windows TFMs (`SkUiPlatforms=windows`); `merge_nupkgs.py` is unchanged. Every pack runs the API check (`-p:SkUiApiBaseline`, the newest stable release on nuget.org other than the version being packed: `scripts/api_baseline.py`), so the merge job needs no separate step. `nuget-pack.yml` ends with a `consumer-check` job per line (`scripts/consumer_check.sh`): the merged packages restored into a minimal Android app with SDK 10 (net10) and SDK 11 (net11), asserting each gets its own line's assets and MAUI.
- **`demo-publish.yml`:** publishes the newest line the tag's project targets (net11 from 1.11 on), so a 1.10.x tag still publishes.
- **Scripts:** `--net 10|11` (default: `SKUI_NET`, else net11; net10 on SDK 10) in `device_verify.sh`, `device_tests.sh` (its mlaunch glob follows the TFM) and `page_tour.sh`. net10 iOS / Mac Catalyst apps run `dotnet` from `scripts/sdk10`, whose `global.json` selects SDK 10.0.4xx.
- **Checked locally:** the CI Apple library build with `SkUiPlatforms` on SDK 11; the consumer check on both lines against a local pack (net10: `lib/net10.0-android36.0` + MAUI 10.0.120 on SDK 10.0.404; net11: `lib/net11.0-android37.0` + MAUI 11.0.10). Restoring with `UseMaui` fails on both lines with NU1605: it adds `Microsoft.Maui.Controls` at the workload's version (10.0.110 / 11.0.0), below the packages' floors, as in 1.10 (apps reference a newer Controls).
- **Mac Catalyst device tests (Release, local):** net11 (CoreCLR) passes 47 of 47 once `device_tests.sh` runs the signed universal `.app` without `--aot`: the per-architecture bundle has only the linker's ad-hoc signature (no `allow-jit`), and CoreCLR, which JIT-compiles, is killed on its first JIT page ("Code Signature Invalid"); Mono never hit it. net10 (Mono, SDK 10) passes 46 of 47: `NativeOverlays` keeps a `MauiTextField` alive in about half the runs, and 1.10.3 does the same (4 of 4), so it is not a 1.11 change. net11 Native AOT (the CI job) builds without trim / AOT warnings and shows the same intermittent `NativeOverlays` leak (2 of 4 runs), so the new net11 device-test job may flake on it until that scenario is fixed.

## Step 6: docs

- **`CHANGELOG.md`:** under `## Unreleased`, a new-feature entry for the net11 targets of all four packages, noting Android API 24 and Xcode 27.2 on net11 and that net10 is unchanged.
- **[Roadmap.md](Roadmap.md):**
  - Add a 1.11.0 row and section.
  - Move the backlog item here.
  - Replace "SkiaUi stays on MAUI 10.x".
  - Add the MAUI 11 members to the parity backlog.
- **[Requirements.md](Requirements.md):** per-TFM platform rows, and drop "MAUI 10.x only".
- **`README.md`:**
  - Requirements for each line.
  - Recheck the Native AOT note on net11.
  - Add a .NET 11 row to the Stress-page table after measuring.
- **`Development.md`:** SDK 11, Xcode 27.2, the workload set, `SkUiNet`, per-TFM project table and CI table.
- **`CLAUDE.md`:** the toolchain pin paragraph, and `-f net11.0` commands.
- **[Migration.md](../Migration.md):**
  - Say it was checked against MAUI 10.0.120 and 11.0.10.
  - List the MAUI 11 members under "Not available yet".
  - Android 24 on net11.
- **Migration skills in `plugins/skiaui-migration`:** update the MAUI version statements.
- **Others:** [Testing.md](Testing.md) (running per TFM), [Releasing.md](../Releasing.md) if the pack jobs change, and the bug report template's version placeholder.

## After .NET 11: net10 floor to MAUI 10.0.130

Once Steps 4 to 6 are in, raise `Maui10Version` from 10.0.120 to 10.0.130 (and check the Community Toolkit 15.x and `MicrosoftExtensions10Version` pins against it), run the headless suite on `net10.0` and the net10 device checks, and note it in the changelog. It ships with 1.11.0.

## Step 7: release

1. Publish `1.11.0-Prerelease01` from `devel` (bump `auto`).
2. Run the device checks below on both runtimes.
3. Publish `1.11.0` (bump `release`).

## Verification

- `dotnet build SkiaUi.slnx -v q -clp:ErrorsOnly` builds both lines with 0 warnings (macOS with Xcode 27.2).
- The headless suite is green on `net10.0` and `net11.0`.
- `scripts/bench.sh --baseline HEAD` (net10) shows no regression. `--net 11` gives a first net11 baseline; expect runtime and GC differences, not code ones.
- `scripts/api_compat.sh` finds no difference between the two lines and nothing removed since 1.10.0.
- Consumer check: the net10 app on SDK 10.0.402 gets the net10 assets and MAUI 10.0.120. The net11 app gets the net11 assets and MAUI 11.0.10.
- Devices, with `scripts/device_verify.sh -l --net 11` and `--net 10`:
  - Run on an iOS simulator, a Mac Catalyst device, and Android emulators at API 24 (net11) and API 21 (net10).
  - Check GPU surfaces (Metal / GL), the software surface (the SkiaSharp net10 view assembly on MAUI 11), text input, native overlays, render-thread animations and Reduce Motion.
- `scripts/device_tests.sh --net 11` and `--net 10` pass the memory-leak scenarios (CoreCLR finalizer timing).
- On net11: Native AOT publish of the demo on iOS and Mac Catalyst, and full trimming on Android.
- CI is green on every job, including the net10-only Xcode 26.6 / 27.2 jobs.
