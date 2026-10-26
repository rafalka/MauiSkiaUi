# SkiaUi development

Internal guide for working **on** SkiaUi (library + demo + tests). Library **users** should start with [README.md](README.md) and [docs/controls/](docs/controls/README.md).

## Solution structure

| Project | Type | Description |
| --- | --- | --- |
| `MauiSkiaUi` | .NET MAUI class library (`net10.0-*`, plus `net10.0` for tests) | SkiaSharp-based UI controls (`SkUi*` types); NuGet package id **`SkiaUi.Maui`**. Depends on MAUI and SkiaSharp only |
| `MauiSkiaUi.CommunityToolkit` | .NET MAUI class library (same TFMs) | CommunityToolkit.Maui integration on SkiaUi's public API: the popup presenter (`SkUiToolkitPopupPresenter`, `UseSkiaUiCommunityToolkit()`); later drawn layouts on the toolkit's layout managers. NuGet package id **`SkiaUi.Maui.CommunityToolkit`**, released with `SkiaUi.Maui` (same version) |
| `MauiSkiaUiDemo` | .NET MAUI application (`net10.0-*`) | Sample host used to develop and verify controls; **in-repo only** (not published). Its own UI (editors, galleries, flyout, page chrome) is drawn with SkiaUi; only pages, Shell, toolbars and the native comparison controls are MAUI |
| `samples/MauiSkiaUiSamples` | .NET MAUI application (`net10.0-*`) | Usage examples, one page per example in sections, each with its in-app description and source ([samples/README.md](samples/README.md)); built headlessly by `SamplesTests` |
| `tests/MauiSkiaUi.Tests` | Headless xUnit tests (`net10.0`) | Real MAUI nodes and offscreen Skia painting; no device required |
| `tests/MauiSkiaUi.DeviceTests` | .NET MAUI application (`net10.0-*`) | On-device memory-leak scenarios with real handlers and platform views (`scripts/device_tests.sh`) |
| `tests/Maestro` | [Maestro](https://maestro.dev) flows | UI-test driver checks of the installed demo on an emulator or simulator (`maestro --device … test tests/Maestro/…`) |
| `benchmarks/MauiSkiaUi.Benchmarks` | Console app (`net10.0`) | Headless benchmark runner (layout / text / recording / compositing) |
| `benchmarks/MauiSkiaUiBench` | .NET MAUI application (`net10.0-*`) | On-device benchmark app (Release); shares `benchmarks/Scenarios` with the headless runner |
| `plugins/skiaui-migration` | Claude Code plugin (Agent Skills) | Migration skills for app developers (`skiaui-audit`, `skiaui-migrate`) with their Python checkers; listed by the repo marketplace `.claude-plugin/marketplace.json` ([README](plugins/skiaui-migration/README.md)) |

Solution file: `SkiaUi.slnx`

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) **10.0.402** (see [global.json](global.json); `rollForward: latestPatch`)
- MAUI Controls packages pinned to **10.0.120** via [Directory.Build.props](Directory.Build.props) (`MauiVersion`)
- CommunityToolkit.Maui (`SkiaUi.Maui.CommunityToolkit`, demo, samples, tests) pinned via the same file (`CommunityToolkitMauiVersion`)
- SkiaSharp packages (SkiaSharp, SkiaSharp.HarfBuzz, SkiaSharp.Views.Maui.Controls, native assets) pinned to **4.153.1** via the same file (`SkiaSharpVersion`). After a bump, [Directory.Build.targets](Directory.Build.targets) clears each iOS / Mac Catalyst app's extracted native frameworks and app bundle once: the Apple SDK re-extracts and copies them by file date, and NuGet keeps the package's (older) dates, so the previous `libSkiaSharp` would stay and fail with "The version of the native libSkiaSharp library … is incompatible"
- Product version (and demo `ApplicationDisplayVersion`) via the same file (`Version` / `ApplicationVersion`). Per-version notes: [CHANGELOG.md](CHANGELOG.md)
- .NET MAUI workload (`dotnet workload install maui`)
- Platform SDKs for the targets you build (Android SDK, Xcode for iOS/Mac Catalyst, etc.). The iOS / Mac Catalyst workload of SDK 10.0.402 needs Xcode 27 (macOS 26.4+); apps built with it use the UIKit scene lifecycle, so each app has a `SceneDelegate` and a `UIApplicationSceneManifest` in its `Info.plist`

## Build and run

```bash
# Restore and build the solution
dotnet build SkiaUi.slnx

# Run headless mechanism tests
dotnet test tests/MauiSkiaUi.Tests/MauiSkiaUi.Tests.csproj

# Performance: working tree vs last commit (headless; add -t android -s <serial> for a device)
./scripts/bench.sh --baseline HEAD

# On-device verification outside VS Code (pick simulator/emulator/device, launch demo, print checklist)
./scripts/device_verify.sh -l
./scripts/device_verify.sh -p android --checklist overlay

# Smoke run on Windows (Git Bash): opens every demo and samples page, fails on crashes and unexpected exceptions
./scripts/page_tour.sh
```

In VS Code, select **.NET MAUI: Select Startup Project > MauiSkiaUiDemo**, choose an Android or Apple target, and start debugging. Apply changes with Hot Reload while debugging. The demo includes MauiDevFlow by default in **Debug**, including ordinary builds/F5: the project references the agent and defines `MAUI_DEVFLOW` to activate the existing startup registration. **Release** excludes both the agent package and registration. No extension injection is needed for ordinary Debug builds; Copilot-triggered launch remains subject to the installed extension's injection-target error described under *Verification status*. For checklist-driven manual runs without DevFlow MCP, prefer [`scripts/device_verify.sh`](scripts/device_verify.sh) (see [Testing.md](docs/design/Testing.md#cli-path-no-vs-code--devflow-mcp)).

## Continuous integration (GitHub Actions)

Workflows live under [`.github/workflows/`](.github/workflows/). Shared setup: [`.github/actions/setup-dotnet-maui/`](.github/actions/setup-dotnet-maui/).

| Workflow | Trigger | What it does |
| --- | --- | --- |
| [ci.yml](.github/workflows/ci.yml) | Push / PR to `master` or `devel` | Headless tests (Ubuntu `net10.0`); build iOS/Mac Catalyst (macOS) and Windows TFMs; Mac Catalyst device tests (memory leaks) |
| [nuget-pack.yml](.github/workflows/nuget-pack.yml) | Manual, or tag `v*` | `dotnet pack` on macOS + Windows; merge multi-TFM `.nupkg` / `.snupkg` artifacts |
| [nuget-publish.yml](.github/workflows/nuget-publish.yml) | Manual: version bump (+ dry run), from `master` or `devel` | Bumps the version and changelog, commits and tags the release, packs it, pushes to [nuget.org](https://www.nuget.org/) via [Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) (OIDC; Environment `nuget.org`), creates the GitHub release. See [docs/Releasing.md](docs/Releasing.md) |
| [demo-publish.yml](.github/workflows/demo-publish.yml) | Manual (app and platform choice, optional release tag), tag `demo-v*`, or called by `nuget-publish.yml` after a release | Publish the demo and/or samples app: Android APK (`android-arm64`) and/or an unsigned universal Mac Catalyst `.app` in a zip, as artifacts; with a release tag, built from it and attached to that GitHub release. A `demo-v*` tag publishes both Android APKs |

### Secrets and environments

| Name | Used by | Purpose |
| --- | --- | --- |
| `NUGET_USER` | nuget-publish | Your [nuget.org](https://www.nuget.org/) **profile username** (not email) for `NuGet/login` |
| `RELEASE_DEPLOY_KEY` | nuget-publish (environment `nuget.org`) | Private half of a write deploy key; pushes the release commit and tag past the `master` ruleset ([docs/Releasing.md](docs/Releasing.md#setup)) |
| `ANDROID_KEYSTORE_BASE64` | demo-publish (optional) | Base64-encoded `.jks` / `.keystore` for signed Release APKs |
| `ANDROID_KEYSTORE_PASSWORD` | demo-publish (optional) | Keystore password |
| `ANDROID_KEY_ALIAS` | demo-publish (optional) | Key alias |
| `ANDROID_KEY_PASSWORD` | demo-publish (optional) | Key password |

#### nuget.org Trusted Publishing (no long-lived API key)

1. Create a GitHub Environment named **`nuget.org`** (Settings → Environments). Require yourself as reviewer and limit deployment branches to `master` and `devel`.
2. Add repository or environment secret **`NUGET_USER`** = your nuget.org profile username.
3. On nuget.org (account menu → **Trusted Publishing**) add a policy:
   - **Repository Owner:** `rafalka`
   - **Repository:** `MauiSkiaUi`
   - **Workflow File:** `nuget-publish.yml` (file name only — no `.github/workflows/` path)
   - **Environment:** `nuget.org` (must match the workflow `environment:`)
4. Push this workflow to GitHub, then publish once within any temporary 7-day activation window so the policy becomes permanently active.

Docs: [Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing).

Without Android signing secrets, the demo Android job still publishes an APK for sideload testing.

Demo app icon/splash SVGs intentionally omit SVG `<filter>` elements: MAUI **10.0.101** Resizetizer regresses on filtered SVGs ([dotnet/maui#38319](https://github.com/dotnet/maui/issues/38319)).

Versions and releases: branches, version format, bumps and the publish procedure are in [docs/Releasing.md](docs/Releasing.md). `nuget-pack.yml` (artifacts only) still accepts an optional version input, or takes it from a `v*` tag or [Directory.Build.props](Directory.Build.props); a manual run of a version with no changelog section yet packs the `## Unreleased` notes.

Release notes: write entries under `## Unreleased` in [CHANGELOG.md](CHANGELOG.md), in its `### New features` / `### Breaking changes` / `### Fixes` / `### Other` subsections; publishing renames the section to the version. Workflows run [`scripts/extract-release-notes.py`](scripts/extract-release-notes.py): the library packs a plain-text summary as `PackageReleaseNotes` (shown on nuget.org: a link to the section, new feature titles, breaking changes), and the GitHub release gets the whole section ([docs/Releasing.md](docs/Releasing.md#changelog)).

## Documentation

Start with [docs/design/Architecture.md](docs/design/Architecture.md). Every document, for users and contributors: [docs/README.md](docs/README.md).

- **Contributor docs** describe SkiaUi as it is: [Architecture](docs/design/Architecture.md), [Requirements](docs/design/Requirements.md), [Roadmap](docs/design/Roadmap.md) and the design doc of each mechanism. Change them in the same pull request as the code they describe.
- **User docs:** a page per control in [docs/controls/](docs/controls/README.md) (NFR-5) and the [migration guide](docs/Migration.md). The guide and the migration skills (`plugins/skiaui-migration`: skill references and the control / gap tables in both scripts) describe the current MAUI parity and gaps: update them when a change adds parity, a control, or closes a gap.
- **History:** [docs/archive/](docs/archive/README.md) keeps the design considerations, reviews and the 1.0 plan as written. Don't update archived documents; link them for the reasoning behind a decision.

Cursor rule for public reference sources: [`.cursor/rules/reference-sources.mdc`](.cursor/rules/reference-sources.mdc).

## Adding a control

1. **Look entry point:** `SkUiLook.Draw*` / `Measure*` with a paint struct (carrying the transition state, with a `SkUiTransitionKind` if the control has state visuals), drawn by `DefaultSkUiLook`, then by the platform looks ([ControlLook.md](docs/design/ControlLook.md)).
2. **Both layers:** the SkUi\* control and its Core twin over one shared engine or painter; MAUI's names where MAUI has the control ([Requirements.md](docs/design/Requirements.md#principles)).
3. Measure / arrange, then paint.
4. Input through the gesture arena; user changes written back to the bindable properties.
5. Animation: state changes through the shared transition animators; continuous motion on the render thread where possible.
6. A demo page in the component gallery (`MauiSkiaUiDemo/ComponentPages`, next to the native MAUI control where one exists; `ComponentDemoTests` enforces it) and a page under [docs/controls/](docs/controls/README.md).
7. Headless tests (a XAML parity test with MAUI's doc samples for MAUI counterparts, SkUi-vs-Core pixels), a memory-leak scenario in `tests/Shared/MemoryLeaks`, a benchmark scenario if it is performance-sensitive; then optimize.
8. A `CHANGELOG.md` entry under `## Unreleased`; the [migration guide](docs/Migration.md) and skills when MAUI parity changes; the [roadmap](docs/design/Roadmap.md) and [requirements](docs/design/Requirements.md) status when it closes an item.

## Demo asset

The bundled `earth.jpg` is NASA's Blue Marble western hemisphere image, downloaded from [NASA Visible Earth](https://eoimages.gsfc.nasa.gov/images/imagerecords/57000/57723/globe_west_2048.jpg). Credit: NASA / Visible Earth. It is packaged under `Resources/Raw` so the demo works offline.
