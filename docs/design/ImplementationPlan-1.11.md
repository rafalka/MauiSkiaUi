# SkiaUi 1.11 implementation plan: .NET 11 and MAUI 11

**Goal:** every package (`SkiaUi.Maui`, `.CommunityToolkit`, `.Markdown`, `.Markdown.Math`) targets `net11.0-*` with MAUI 11 next to `net10.0-*` with MAUI 10. Apps that stay on .NET 10 keep restoring the net10 assets and never see MAUI 11. No net10 target framework is dropped.

**Why now:** .NET 11 (SDK 11.0.100) and MAUI 11.0.0 went GA on 10 November 2026. STS releases now get 24 months, so .NET 10 (LTS) and .NET 11 both reach end of support on 14 November 2028. Most apps will stay on 10 for a while, and early adopters move to 11.

**Scope:** platform support only: build, test, pack and verify on both lines. New MAUI 11 members (below) go on the parity test's known-gap list and into the [backlog](Roadmap.md#maui-parity) for a later release.

**Baseline:** 1.10.0 (MAUI 10.0.120, SDK 10.0.402, SkiaSharp 4.154.0, Xcode 26.6 / 27.1).

**Status:** Step 1 is done on branch `feature/net11-support` and verified on SDK 10 (net10 only). The rest is not started. Step 0 needs Xcode 27.2 and the .NET 11 workloads (set 11.0.100.1) on the build machine.

## Decisions

- **App heads multi-target too:** the demo, samples, device tests and on-device bench target both lines. Device scripts default to net11 and take `--net 10|11`.
- **Release:** `1.11.0-Prerelease01` first, then `1.11.0` after device checks on both runtimes.
- **SkiaSharp stays on 4.154.0 for both lines.** Move to 4.156 (the first release with net11 assets) in a later release, once it is stable.
- **API differences:** use the compiler's `NET11_0_OR_GREATER`, inside small shims rather than in controls. The `SKUI_NET11` define the roadmap planned is not needed.
- **One build switch:** `-p:SkUiNet=10` or `-p:SkUiNet=11` limits a build to one line. The .NET SDK version is not yet known when `Directory.Build.props` is read, so net11 can't be switched off automatically on SDK 10. CI jobs on SDK 10 pass `SkUiNet=10`.

## Facts that shape the plan

These were checked against the release notes, nuget.org and the workload manifests in November 2026.

- **Android:** MAUI 11 raises the minimum Android to **API 24** (we use 21 on net10). The default `net11.0-android` is API 37. iOS 15, Mac Catalyst 17 and Windows 10.0.17763 stay the same.
- **Xcode:** .NET 11 Apple packs (workload set **11.0.100.1**) need **Xcode 27.2**, which needs macOS 26.6 or newer.
- **net10 Apple builds on SDK 11:** SDK 11's workload builds `net10.0-ios` / `maccatalyst` against the **26.0** iOS pack (26.0.11017), not the 27.1 pack 1.10 was built with. Step 0 decides whether that is acceptable.
- **Community Toolkit:** CommunityToolkit.Maui 17.0.0 targets net11 only, and 15.0.1 (the last net10 line) targets net10 only, so the toolkit version is chosen per TFM.
- **SkiaSharp views:** SkiaSharp.Views.Maui.Controls 4.154 has net10 assets only (it needs MAUI 10.0.20 or newer). net11 builds use that net10 assembly on MAUI 11, so it must be checked on devices.
- **Runtime:** CoreCLR replaces Mono on Android, iOS and Mac Catalyst in .NET 11. Runtime, GC and finalizer timing change under the render thread, so device checks run on both runtimes.
- **No blockers in our code:** SkiaUi uses no MAUI internals (no `UnsafeAccessor` or non-public reflection) and no iOS 26+ APIs. The only Android 36 checks are runtime `IsAndroidVersionAtLeast` calls.
- **New MAUI 11 public API the parity test will flag:**
  - `LongPressGestureRecognizer` (on drawn views)
  - `BoxView.Fill`
  - the `CancellationToken` overloads of `FadeToAsync` and the other animation extensions
  - `VisualElement.InvalidateStyle` / `VisualStateManager.InvalidateVisualStates`

## Step 0: spike, one SDK or two (decides Steps 1 and 5)

Work in a scratch worktree with `global.json` set to 11.0.100:

1. Build every TFM of `MauiSkiaUi` with Xcode 27.2. Check for Xcode-version errors or warnings on the net10 Apple TFMs, which use the 26.0 pack.
2. Compare the net10 Apple assemblies built this way with the 1.10.0 nupkg using ApiCompat. The public API must be identical.
3. Build the iOS demo for `net10.0-ios` with SDK 10.0.402 and Xcode 27.2 (workload set 10.0.404) against the library built on SDK 11, and run it on a simulator.

Outcome:

- **All pass → one SDK:** `global.json` moves to 11.0.100 and builds every TFM.
- **Any fail → two SDKs:** net10 assets keep being built by SDK 10.0.402 with workload 10.0.404, which is the 1.10.2 toolchain. net11 assets are built by SDK 11, and the pack workflow merges both (Step 5). `global.json` still moves to 11.0.100 for development.

## Step 1: MSBuild, one place for TFMs and versions (done)

- **`Directory.Build.props`, pins:** `Maui10Version` 10.0.120 and `Maui11Version` 11.0.0. The net10 floor stays at 10.0.120; also check it with 10.0.130.
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

## Step 4: API compatibility check

- `scripts/api_compat.sh` runs `Microsoft.DotNet.ApiCompat.Tool` (`apicompat package`) on each merged nupkg, with `--baseline-package` set to the 1.10.0 package from nuget.org. It checks two things:
  - net10 and net11 expose the same API.
  - Nothing from 1.10 was removed (the 1.x API promise).
- If reference resolution fails for the Apple assemblies, check `net10.0` / `net11.0` / Android only, and say so in the script.

## Step 5: CI, packing, scripts

- **`.github/actions/setup-dotnet-maui/action.yml`:**
  - An `sdk-version` input overrides `global.json` for net10-only jobs.
  - The default workload set follows the SDK band: 11.0.100.1, or 10.0.404 for SDK 10.
  - The Xcode probe takes its TFMs from the SDK instead of the hard-coded net10 ones.
  - The pin printout greps the new version properties.
- **`ci.yml`:**
  - Headless tests run on both TFMs.
  - Apple: a new SDK 11 / Xcode 27.2 entry builds both lines and the iOS demo for each.
  - The existing Xcode 27.2 (10.0.404) and Xcode 26.6 (10.0.401, `macos-26`) entries become net10-only jobs on SDK 10 with `SkUiNet=10`. They prove that .NET 10 users' toolchains still build.
  - The Android and Windows jobs build both lines. The newest-AndroidX.Core check runs per line.
  - The Mac Catalyst device tests run on net11 and net10.
  - The `xcode-27` runner image has Xcode 27.2 (not its default); CI moved to it in 1.10.2.
- **`nuget-pack.yml` / `nuget-publish.yml`:**
  - With one SDK, the macOS pack holds both lines, and the Windows pack uses both Windows TFMs instead of `-p:TargetFrameworks`.
  - With two SDKs, `pack-apple` splits into net10 (SDK 10) and net11 (SDK 11) jobs, and `scripts/merge_nupkgs.py` merges N packs instead of two.
  - The merge job then runs `api_compat.sh`.
  - A `consumer-check` job (Ubuntu, Android only) restores the merged packages into a minimal net10 app with SDK 10.0.402 and a net11 app with SDK 11. It asserts that each gets its own assets and that net10 never pulls MAUI 11.
- **`demo-publish.yml`:** the attached apps are built for net11.
- **Scripts:** `--net 10|11` (default 11, or `SKUI_NET`) in `device_verify.sh`, `device_tests.sh` (including its `Microsoft.iOS.Sdk.net10*` mlaunch glob) and `page_tour.sh`.

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
  - Say it was checked against MAUI 10.0.120 and 11.0.0.
  - List the MAUI 11 members under "Not available yet".
  - Android 24 on net11.
- **Migration skills in `plugins/skiaui-migration`:** update the MAUI version statements.
- **Others:** [Testing.md](Testing.md) (running per TFM), [Releasing.md](../Releasing.md) if the pack jobs change, and the bug report template's version placeholder.

## Step 7: release

1. Publish `1.11.0-Prerelease01` from `devel` (bump `auto`).
2. Run the device checks below on both runtimes.
3. Publish `1.11.0` (bump `release`).

## Verification

- `dotnet build SkiaUi.slnx -v q -clp:ErrorsOnly` builds both lines with 0 warnings (macOS with Xcode 27.2).
- The headless suite is green on `net10.0` and `net11.0`.
- `scripts/bench.sh --baseline HEAD` (net10) shows no regression. `--net 11` gives a first net11 baseline; expect runtime and GC differences, not code ones.
- `scripts/api_compat.sh` finds no difference between the two lines and nothing removed since 1.10.0.
- Consumer check: the net10 app on SDK 10.0.402 gets the net10 assets and MAUI 10.0.120. The net11 app gets the net11 assets and MAUI 11.0.0.
- Devices, with `scripts/device_verify.sh -l --net 11` and `--net 10`:
  - Run on an iOS simulator, a Mac Catalyst device, and Android emulators at API 24 (net11) and API 21 (net10).
  - Check GPU surfaces (Metal / GL), the software surface (the SkiaSharp net10 view assembly on MAUI 11), text input, native overlays, render-thread animations and Reduce Motion.
- `scripts/device_tests.sh --net 11` and `--net 10` pass the memory-leak scenarios (CoreCLR finalizer timing).
- On net11: Native AOT publish of the demo on iOS and Mac Catalyst, and full trimming on Android.
- CI is green on every job, including the net10-only Xcode 26.6 / 27.1 jobs.
