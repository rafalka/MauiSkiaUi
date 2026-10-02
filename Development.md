# SkiaUi development

Internal guide for working **on** SkiaUi (library + demo + tests). Library **users** should start with [README.md](README.md) and [docs/controls/](docs/controls/README.md).

## Solution structure

| Project | Type | Description |
| --- | --- | --- |
| `MauiSkiaUi` | .NET MAUI class library (`net10.0-*`, plus `net10.0` for tests) | SkiaSharp-based UI controls (`SkUi*` types); NuGet package id **`SkiaUi.Maui`** |
| `MauiSkiaUiDemo` | .NET MAUI application (`net10.0-*`) | Sample host used to develop and verify controls; **in-repo only** (not published) |
| `samples/MauiSkiaUiSamples` | .NET MAUI application (`net10.0-*`) | Usage examples, one page per example in sections, each with its in-app description and source ([samples/README.md](samples/README.md)); built headlessly by `SamplesTests` |
| `tests/MauiSkiaUi.Tests` | Headless xUnit tests (`net10.0`) | Real MAUI nodes and offscreen Skia painting; no device required |
| `tests/MauiSkiaUi.DeviceTests` | .NET MAUI application (`net10.0-*`) | On-device memory-leak scenarios with real handlers and platform views (`scripts/device_tests.sh`) |
| `benchmarks/MauiSkiaUi.Benchmarks` | Console app (`net10.0`) | Headless benchmark runner (layout / text / recording / compositing) |
| `benchmarks/MauiSkiaUiBench` | .NET MAUI application (`net10.0-*`) | On-device benchmark app (Release); shares `benchmarks/Scenarios` with the headless runner |

Solution file: `SkiaUi.slnx`

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) **10.0.400** (see [global.json](global.json); `rollForward: latestPatch`)
- MAUI Controls packages pinned to **10.0.110** via [Directory.Build.props](Directory.Build.props) (`MauiVersion`)
- SkiaSharp packages (SkiaSharp, SkiaSharp.HarfBuzz, SkiaSharp.Views.Maui.Controls, native assets) pinned to **4.153.1** via the same file (`SkiaSharpVersion`). After a bump, [Directory.Build.targets](Directory.Build.targets) clears each iOS / Mac Catalyst app's extracted native frameworks and app bundle once: the Apple SDK re-extracts and copies them by file date, and NuGet keeps the package's (older) dates, so the previous `libSkiaSharp` would stay and fail with "The version of the native libSkiaSharp library … is incompatible"
- Product version (and demo `ApplicationDisplayVersion`) via the same file (`Version` / `ApplicationVersion`). Per-version notes: [CHANGELOG.md](CHANGELOG.md)
- .NET MAUI workload (`dotnet workload install maui`)
- Platform SDKs for the targets you build (Android SDK, Xcode for iOS/Mac Catalyst, etc.)

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
```

In VS Code, select **.NET MAUI: Select Startup Project > MauiSkiaUiDemo**, choose an Android or Apple target, and start debugging. Apply changes with Hot Reload while debugging. The demo includes MauiDevFlow by default in **Debug**, including ordinary builds/F5: the project references the agent and defines `MAUI_DEVFLOW` to activate the existing startup registration. **Release** excludes both the agent package and registration. No extension injection is needed for ordinary Debug builds; Copilot-triggered launch remains subject to the installed extension's injection-target error described under *Verification status*. For checklist-driven manual runs without DevFlow MCP, prefer [`scripts/device_verify.sh`](scripts/device_verify.sh) (see [Testing.md](docs/design/Testing.md#cli-path-no-vs-code--devflow-mcp)).

## Continuous integration (GitHub Actions)

Workflows live under [`.github/workflows/`](.github/workflows/). Shared setup: [`.github/actions/setup-dotnet-maui/`](.github/actions/setup-dotnet-maui/).

| Workflow | Trigger | What it does |
| --- | --- | --- |
| [ci.yml](.github/workflows/ci.yml) | Push / PR to `master` or `devel` | Headless tests (Ubuntu `net10.0`); build iOS/Mac Catalyst (macOS) and Windows TFMs; Mac Catalyst device tests (memory leaks) |
| [nuget-pack.yml](.github/workflows/nuget-pack.yml) | Manual, or tag `v*` | `dotnet pack` on macOS + Windows; merge multi-TFM `.nupkg` / `.snupkg` artifacts |
| [nuget-publish.yml](.github/workflows/nuget-publish.yml) | Manual: version bump (+ dry run), from `master` or `devel` | Bumps the version and changelog, commits and tags the release, packs it, pushes to [nuget.org](https://www.nuget.org/) via [Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) (OIDC; Environment `nuget.org`), creates the GitHub release. See [docs/Releasing.md](docs/Releasing.md) |
| [demo-publish.yml](.github/workflows/demo-publish.yml) | Manual (platform choice), or tag `demo-v*` | Publish demo Android APK (`android-arm64`) and optionally Mac Catalyst as artifacts |

### Secrets and environments

| Name | Used by | Purpose |
| --- | --- | --- |
| `NUGET_USER` | nuget-publish | Your [nuget.org](https://www.nuget.org/) **profile username** (not email) for `NuGet/login` |
| `ANDROID_KEYSTORE_BASE64` | demo-publish (optional) | Base64-encoded `.jks` / `.keystore` for signed Release APKs |
| `ANDROID_KEYSTORE_PASSWORD` | demo-publish (optional) | Keystore password |
| `ANDROID_KEY_ALIAS` | demo-publish (optional) | Key alias |
| `ANDROID_KEY_PASSWORD` | demo-publish (optional) | Key password |

#### nuget.org Trusted Publishing (no long-lived API key)

1. Create a GitHub Environment named **`nuget.org`** (Settings → Environments). Optional: require reviewers.
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

Versions and releases: branches, version format, bumps and the publish procedure are in [docs/Releasing.md](docs/Releasing.md). `nuget-pack.yml` (artifacts only) still accepts an optional version input, or takes it from a `v*` tag or [Directory.Build.props](Directory.Build.props).

Release notes: write entries under `## Unreleased` in [CHANGELOG.md](CHANGELOG.md); publishing renames the section to the version. Workflows run [`scripts/extract-release-notes.py`](scripts/extract-release-notes.py) and the library packs the section as `PackageReleaseNotes` (shown on nuget.org), with a link back to the changelog.

## Design documentation

Architecture, requirements, and mechanism checklists live under **[docs/design/](docs/design/)**:

| Doc | Topic |
| --- | --- |
| [Requirements.md](docs/design/Requirements.md) | Goals, architecture, XAML model, backlog, public reference repos |
| [CoreRequirements.md](docs/design/CoreRequirements.md) | Low-level Core layer (no MAUI Controls / XAML-first) |
| [ImplementationPlan.md](docs/design/ImplementationPlan.md) | Completed surface vs backlog (to be implemented) |
| [LayoutSystem.md](docs/design/LayoutSystem.md) | MAUI measure/arrange, hosted vs standalone (FR-3 / FR-3a / FR-13) |
| [DrawingMechanism.md](docs/design/DrawingMechanism.md) | Paint pipeline, layers, clip/mask, caching (FR-8 / FR-9 / FR-11) |
| [ControlLook.md](docs/design/ControlLook.md) | Control look packs / default sizes (FR-18) |
| [ColorScheme.md](docs/design/ColorScheme.md) | Shared default palette (FR-19) |
| [EventMechanism.md](docs/design/EventMechanism.md) | SkiaUi-owned gestures (FR-15) |
| [AnimationMechanism.md](docs/design/AnimationMechanism.md) | Vsync clock, paint / transform animation (FR-7) |
| [ScrollingAndCollectionViews.md](docs/design/ScrollingAndCollectionViews.md) | `SkUiScrollView` / collections (FR-17) |
| [Testing.md](docs/design/Testing.md) | Unit / mechanism / golden / device strategy; `device_verify.sh` |
| [RenderingPipeline.md](docs/design/RenderingPipeline.md) | Retained compositor, UI vs render thread, Metal / GL surfaces, render-thread animation (NFR-6) |
| [Benchmarks.md](docs/design/Benchmarks.md) | Headless + device benchmarks, before/after comparisons (`scripts/bench.sh`) |
| [ArchitectureReview.md](docs/design/ArchitectureReview.md) | 2026-09 review vs DrawnUi / Flutter / Avalonia / Uno / Open-Maui and implementation status |

Per-control user docs (NFR-5): [docs/controls/](docs/controls/README.md).

Cursor rule for public reference sources: [`.cursor/rules/reference-sources.mdc`](.cursor/rules/reference-sources.mdc).

When a requirement ships, check it off in the relevant design doc and summarize delivered behavior under *Current implementation* below.

## Current implementation

### Library (`MauiSkiaUi`)

- Targets Android, iOS, and Mac Catalyst (Windows TFM included when building on Windows).
- `ISkUiView : IView`, `SkUiView`, padded `SkUiContentView`, and overlay `SkUiLayout` implement the shared-surface pipeline.
- Layouts: `SkUiGrid` (MAUI `GridLayoutManager`), stacks, `SkUiAbsoluteLayout`, `SkUiFlexLayout` (MAUI `FlexLayoutManager` over a ported flex engine), `SkUiWrapLayout`, shrink stacks (`SkUiHorizontalShrinkLayout` / `SkUiVerticalShrinkLayout`), `SkUiScrollView`, `SkUiBorder`.
- Core layouts: absolute, stacks, overlay, **`SkUiCoreWrapLayout`**, shrink stacks, **`SkUiCoreGrid`** (owned Auto/absolute/star + per-track min/max), **`SkUiCoreTable`** (row/column backgrounds + span-aware separators), **`SkUiCoreScrollView`** (shared scroll engine). Host via `SkUiCoreHost`. Per-child layout data (grid cells, absolute bounds, shrink factors) is stored on the children as typed Core attached properties (`SkUiCoreAttachedProperty<T>`, [SkUiCore.md](docs/controls/SkUiCore.md#attached-properties)).
- Basic controls: `SkUiLabel`, `SkUiButton`, asynchronous `SkUiImage` / `SkUiImageButton`, `SkUiActivityIndicator`, `SkUiSwitch`, `SkUiCheckBox`, `SkUiRadioButton`.
- Native hosting: `SkUiMauiContentView` (FR-16). See API notes below for specifics and limits.
- `SkUiBox`, `SkUiEllipse`, and `SkUiLine` expose bindable colors and sizing and paint with SkiaSharp (`SkiaSharpVersion`). `SkUiBox` has MAUI BoxView's per-corner `CornerRadius`; `SkUiLine` draws between MAUI's `X1`/`Y1`/`X2`/`Y2` and measures and places itself like MAUI's unstretched `Line`.
- Hosted children have logical MAUI parents and inherited binding contexts, but no handlers or native surfaces. Duplicate ownership and tree cycles are rejected.
- Handler-independent measure/arrange uses MAUI constraint and frame helpers, including margins, requests, alignment, and cached unchanged passes.
- Paint walks Background / Content / Overlay phases, with local rectangular clipping, opacity, translation, rotation, scale, and stable ZIndex ordering.
- Gestures run in a shared gesture arena (one arena per pointer, so multi-touch is independent) with child-first hit-testing, inverse transforms, a 10-DIP touch slop, and cancellation: tap, double tap, long press, swipe, pan, pinch and raw pointer recognizers ([EventMechanism.md](docs/design/EventMechanism.md)). Buttons participate intrinsically with `Clicked`, `Command`, `CommandParameter`, `IsPressed`, and Normal/Pressed/Disabled visual states. Passive and input-transparent nodes pass through; disabled nodes block without firing.
- `SkUiAnimationClock` accepts deterministic frame times and stops rendering when the last animation finishes. Transform animations do not invalidate measure.
- `StartUpdating` / `EndUpdating` batch layout and paint notifications. Native resources and subscriptions are released when the handler disconnects.
- `UseSkiaUi()` registers SkiaSharp and the custom standalone handler. Public library APIs include XML documentation.

### Demo (`MauiSkiaUiDemo`)

- The Shell flyout opens **Components** (MAUI-compatible `SkUi*` demos under Basic controls / Layouts / Graphics / Scrolling), **Core** (`SkUiCoreGrid`, `SkUiCoreTable`, `SkUiCoreScrollView`, … hosted in `SkUiCoreHost`), **Composition**, **Native nesting** (drawn surfaces inside a native MAUI `ScrollView`), **Look & colors**, **Stress**, and **Primitives**.
- The other flyout pages: **Composition** (a XAML control sample with Grid, wrapping Label, an offline NASA image, command-bound Buttons, MAUI styles/visual states, and scrollable content), **Look & colors** (FR-18/19 playground: light/dark/custom accent, Default/Chunky/Minimal look packs, size scale; preview includes SkUi* and Core), **Stress** (two-column grid of buttons under one scroll surface — toggle **Core layer** to compare MAUI-compatible `SkUiGrid` vs `SkUiCoreGrid`; **Animate** uses spinners in the 2nd column on either layer; **Scroll** animates, **Top** resets, **Record** reports CPU picture-recording time), and **Primitives** (box/ellipse/line under one GPU-default `SkUiContentView`, a four-second transform animation with a native status label, tap-to-recolor, and a standalone software-rendered box).
- Conditional MauiDevFlow initialization and Mac Catalyst server entitlement are wired for runtime inspection.

### Usage and limits

Register `builder.UseSkiaUi()` in `MauiProgram`, then compose in XAML (see [README.md](README.md) for short samples).

- `SkUiLayout` is deliberately an **overlay**: every child receives the same padded slot. `SkUiGrid` delegates measurement and arrangement to MAUI's `GridLayoutManager`, including Auto/star/absolute definitions, spans, spacing, padding, and standard `Grid.Row`, `Grid.Column`, `Grid.RowSpan`, and `Grid.ColumnSpan`. Runtime definition/attached-property changes invalidate layout. Other layout packs remain later work.
- `HwAccelerated` is a CLR property set **before handler creation**; changing it after attachment throws. Content hosts/layouts default to GPU; leaves default to software. Hosted values are ignored. The SkiaSharp GPU backend is platform-dependent (Metal on Mac Catalyst). There is no automatic GPU recovery: set `HwAccelerated="False"` before attachment on unsupported devices.
- All tree geometry, paint, and input use DIPs. Only the handler scales to surface pixels. `Paint` receives a local canvas; parents translate to child frames, and input routers undo the same render transform.
- **Threading ([RenderingPipeline.md](docs/design/RenderingPipeline.md)):** tree mutations, layout and recording run on the UI thread; compositing, rasterization and composite-time animations run on a render thread for GPU surfaces (a shared Metal render thread on iOS / Mac Catalyst, the `GLTextureView` GL thread on Android). Software surfaces and Windows composite on the UI thread.
- **Retained compositor:** each node keeps recorded Content / Overlay `SKPicture`s. Only nodes whose content changed are re-recorded. Offset, transform, opacity, clip and scroll offset are composite-time properties. Invalidation reaches the root once per frame.
- The handler shares an internal, headless-tested `SkUiFrameRenderer` (record + commit + density mapping + cleanup) and `SkUiCompositor` (apply + animate + draw). Invalidation raised while recording queues one follow-up frame; `SkUiTestSurface` in the tests drives both halves with explicit times.
- Prefer `AnimateAsync` (opacity / translation / rotation / scale, render thread) over `AnimationClock` callbacks (UI thread, vsync ticker) for motion. Clock callbacks should change paint/transform properties. Dispose their handles on page disappearance; handler unload/disconnect also stops the root clock. Start animations after attaching nodes to their intended root.
- `StopAll()` preserves the clock's monotonic timeline. After restarting an animation on the same clock, continue supplying elapsed timestamps rather than resetting them to zero.
- Hit regions are rectangular arranged bounds, including the visually empty corners of ellipses and the area beside a line. Render transforms are inverted before testing these bounds; shape-aware hits are deferred. Disabled nodes block hits without firing, and `IsVisible="False"` collapses nodes and excludes them from paint/input.
- Backgrounds support solid brushes/colors only. Setting only `BackgroundColor` works even though MAUI leaves `Background` as an empty default brush; an explicit solid `Background` wins over `BackgroundColor`. Custom masks and gradients are deferred. Hosted implementations must also be MAUI `Element` instances for logical ownership.
- Drawn controls are not individual native accessibility elements and do not yet provide keyboard activation. Native status/navigation controls remain available. Full drawn-tree accessibility and device frame-rate targets are not claimed.

### Controls & scroll APIs

```xml
<sk:SkUiContentView Background="White">
	<sk:SkUiScrollView Padding="16">
		<sk:SkUiGrid RowDefinitions="Auto,Auto,Auto" RowSpacing="12">
			<sk:SkUiLabel Text="Observations" FontSize="24" />
			<sk:SkUiImage Grid.Row="1" Source="earth.jpg" HeightRequest="200" />
			<sk:SkUiButton Grid.Row="2" Text="Add observation" Command="{Binding AddCommand}" />
		</sk:SkUiGrid>
	</sk:SkUiScrollView>
</sk:SkUiContentView>
```

- Control-owned bindable properties have one update path: a fluent direct setter (`SetText`, `SetSource`, `SetContent`, `SetPadding`, …) checks its argument and sets the property (`SetText(v)` is `Text = v`), and the property's change callback (`OnTextChanged`) applies the value to the control's fields, which paint and layout read. CLR getters read the bindable store, as MAUI's do: MAUI raises `PropertyChanged` before the change callback, so a field-backed getter would hand bindings, triggers and `x:Reference` sources the old value. Value checks are the properties' `validateValue` (`Helpers/SkUiValidate.cs`; MAUI ignores a rejected value with a warning on XAML, binding, style and property-setter paths), repeated by the direct setters, which throw. `BindableStoreTests` checks that fields and store agree on every control. Both paths honor `StartUpdating` / `EndUpdating`; these batch invalidation, not rollback or asynchronous loading.
- Label supports HarfBuzz-shaped text with bidi / RTL and per-character font fallback ([SkUiLabel.md](docs/controls/SkUiLabel.md)), grapheme-safe word/character wrapping, head/middle/tail truncation, font size, system family names and MAUI `ConfigureFonts` aliases, bold/italic, alignment, and padding, and MAUI Label's `MaxLines`, `LineHeight`, `CharacterSpacing`, `TextDecorations` and `TextTransform` (both layers, one engine). A custom `LineBreaker` (`SkUiTextLineBreaker`) decides the lines itself, with a context that measures as the engine draws and falls back to the stock modes: a custom ellipsis (`SkUiTextLineBreakers.WithEllipsis`), shorter forms such as fewer decimals (`FirstFit`). Rich text, selection, and native font scaling are not implemented.
- Button adds intrinsic taps, command eligibility, rounded fill/border chrome shared with its content clip (`SkUiLook.Current`, so glyphs never bleed past the corners; hit bounds remain rectangular), MAUI's visual states (`Normal` / `PointerOver` / `Pressed` / `Disabled`), and MAUI's `Pressed` / `Released` events (before `Clicked`; a cancelled press also releases). `FillColor` controls its default fill; a solid `Background` overrides it. Setting both `Command` and the inherited `TappedCommand` runs only `Command` on a tap (the shared `Tapped` event still fires for both).
- Image accepts `FileImageSource` (a relative name is the **`MauiImage`** for the display density — Android drawable, Apple `@2x` / `@3x`, Windows `.scale-NNN`, SVG items by their PNG name — else a **Resources/Raw** asset; or an absolute path), `StreamImageSource`, HTTP(S) `UriImageSource` (plain http needs the platform's cleartext permission) and **`FontImageSource`** (glyph drawn through the text engine, synchronous). Core takes the same kinds as `SkUiImageSource` records. `AspectFit`, `AspectFill`, `Fill` and `Center` control painting (`Center` draws at the intrinsic size, also when the decode was downsampled); the intrinsic size is source pixels divided by the source's density (a MauiImage at its base size).
- **One loader and cache for both layers** (`MauiSkiaUi/Imaging/`, [SkUiImage.md](docs/controls/SkUiImage.md#caching)): decoded images in a memory LRU (`SkUiImageCache.MemoryCacheMaxBytes`) keyed by source, decode size and transformations and leased by the views showing them (a cached source shows synchronously; an evicted image lives while shown; cleared on OS memory pressure); concurrent loads of a key share one download and decode; downloads in a disk cache with MAUI's `CachingEnabled` / `CacheValidity`. EXIF orientation is applied; animated GIF / WebP play on the UI clock with `IsAnimationPlaying`. FFImageLoading-style `Transformations` (`ISkUiImageTransformation`: circle, rounded, crop, flip, rotate, blur, tint, color matrix), `DownsampleWidth` / `DownsampleHeight`, `CacheType`, `LoadingPlaceholder` / `ErrorPlaceholder`, and `LoadingStarted` / `LoadingFinished` events (status, error, origin). Slider `ThumbImageSource` uses the same loader.
- Image loading exposes `IsLoading`, `LoadError`, `ImageSize`, and awaitable `LoadingTask`; errors leave a blank image. Replacement cancels old work and rejects stale results. Encoded data is limited to 32 MiB and the decoded edge to `SkUiImageDecoder.MaxDecodeDimension` (2048 px). Source streams are owned/disposed by the loader. The decode continuation is marshaled back to the thread that started loading (via a handler-independent dispatcher capture), so hosted (handlerless) images stay correct even though the actual decode runs on a thread-pool thread. Use setters and `Dispose()` on the UI thread; dispose images when permanently removing them.
- ScrollView measures its single content unconstrained on the enabled axes (`Vertical`, `Horizontal`, `Both`, `Neither`). `ContentSize` includes padding. `ScrollX`/`ScrollY` are read-only, clamped DIPs. Offset changes reposition content without remeasuring it. `ScrollTo`, `ScrollToAsync`, `AnimateScrollTo`, and `Scrolled` provide programmatic access; gesture/unload/disable interrupts animation and cancels pending async scrolls.
- Pan takes over after 10 DIPs and cancels the child's pending tap. A bounded decelerating fling runs on the render thread, stops at bounds/rest, and is interrupted by a new press. A desktop wheel scrolls the vertical axis whenever it is enabled (`Vertical` or `Both`), and only scrolls horizontally when `Horizontal` is the sole enabled axis — `SkUiTouchEvent` carries a single `WheelDelta` with no axis indicator, so `Both` cannot yet distinguish a horizontal-wheel gesture (e.g. shift+wheel) from a vertical one. Nested scrolling (axis-aware, chaining, fling hand-off, native ancestors) and native overlays are supported ([ScrollingAndCollectionViews.md](docs/design/ScrollingAndCollectionViews.md)); bounce, snapping, scrollbars, and virtualization remain deferred. Native MAUI scroller nesting is compatibility-only; prefer one SkiaUi scroll surface.
- Painting conservatively rejects nodes outside the transformed canvas clip and caches stable Z-order until collection/ZIndex changes. All controls remain retained; this is paint culling, not item virtualization.

### Native hosting & additional layouts

```xml
<sk:SkUiGrid RowDefinitions="Auto,Auto" RowSpacing="12">
	<sk:SkUiBorder Stroke="#087F83" StrokeThickness="2" CornerRadius="10">
		<sk:SkUiLabel Text="Bordered content" />
	</sk:SkUiBorder>
	<sk:SkUiMauiContentView Grid.Row="1" HeightRequest="160">
		<Editor Placeholder="Native Editor hosted natively" />
	</sk:SkUiMauiContentView>
</sk:SkUiGrid>
```

- **`SkUiMauiContentView`** (FR-16) hosts a real MAUI `VisualElement` (e.g. `Entry`, `Editor`, `WebView`) as a native overlay instead of a Skia reimplementation. Internally, the standalone root's handler now wraps its Skia surface in a small per-platform native container (`SkUiOverlayContainer`) so overlay views can be added as absolutely positioned siblings (Android `View.Layout`, iOS/Mac Catalyst `UIView.Frame`, Windows `Canvas`). Positioning uses `ComputeRootRelativeFrame()`, which sums this node's and every ancestor's `Frame` offset plus `TranslationX`/`TranslationY` up to the standalone root. Overlays are clipped to ancestor scroll viewports and clipping ancestors; while scrolling they switch to a snapshot on Android / Windows (`ScrollMode`, live on Apple). Taps and text input stay native; only drags claimed by a drawn ancestor's continuous gesture (e.g. a scroll) are handed to the drawn tree. **v1 limits:** rotation/scale/opacity are not composed through ancestors. See `MauiContentViewDemoPage` in the gallery for a working Editor/WebView example, combined in one `SkUiGrid` with `SkUiLabel`/`SkUiButton` — editing the Editor's HTML live-updates the WebView.
- **`SkUiBorder`** draws a rounded-rectangle fill/border/clip around one child, sharing `SkUiLook.Current` geometry with Button. Fill falls back from `Background` (`SolidColorBrush`) to `BackgroundColor` like `SkUiView`. Only rounded rectangles are supported (no arbitrary `IShape` strokes like MAUI's full `Border.StrokeShape`).
- **`SkUiActivityIndicator`** is a drawn indeterminate spinner driven by the shared `AnimationClock`; animates only while `IsRunning`, and stops automatically if hidden or removed from its tree (so a detached spinner cannot keep ticking).
- **`SkUiImageButton`** extends `SkUiImage` with intrinsic taps, `Command`/`CommandParameter`/`Clicked`/`Pressed`/`Released`, a pressed/disabled tint overlay, and MAUI's chrome: `Padding` around the image, per-corner `CornerRadii` (and MAUI's `int` `CornerRadius`, which sets all four) that clip the image, the tint and the border, and `BorderColor` / `BorderWidth` drawn inside the bounds.
- **`SkUiSwitch`**, **`SkUiCheckBox`**, **`SkUiRadioButton`** share a small `SkUiToggleControl` base (three-state `CheckState` with `IsThreeState`, the two-state `IsChecked` view kept for MAUI parity, `CheckedChanged` / `CheckStateChanged`, intrinsic tap-to-toggle that writes back to bindings; `CheckedChanged` uses MAUI's `CheckedChangedEventArgs`). `SkUiSwitch` adds MAUI's `IsToggled` / `Toggled` as the same two-state view. **`SkUiRadioButton` overrides the tap to only select, never uncheck** (matching MAUI's `RadioButton`), and excludes the rest of its group with MAUI's scopes: siblings in the same parent without a `GroupName`, or every radio button with the same `GroupName` on the page. MAUI's own `RadioButtonGroup.GroupName` / `SelectedValue` (two-way) work on drawn layouts: `SkUiLayout` forwards their changes to the internal `SkUiRadioGroups`, because MAUI's controller only handles MAUI's `RadioButton`. Core radio buttons exclude their siblings in the same parent (no `GroupName` / `Value`).
- **State-change transitions** (FR-26): toggles, buttons, image buttons, sliders and progress bars (both layers) pass the transition to the look, which draws every point of it and sets its timing (`SkUiLook.GetTransition`). Switch thumbs slide, check marks draw in, radio dots grow, presses dim or ripple (`DefaultSkUiLook.PressEffect`), slider thumbs glide to tapped values. `ShowsPressEffect` gives any tappable node (a card, a composite button of Core nodes) the same press feedback. They run on the UI clock and re-record only the animating control; controls animate once drawn; `SkUiMotion` follows the OS reduce-motion setting ([ControlLook.md](docs/design/ControlLook.md#state-change-transitions-fr-26)).
- **`SkUiSlider`** follows MAUI's Slider API plus `Orientation` (vertical sliders run bottom-up; RTL mirrors horizontal ones). Drags along the slider claim through the gesture arena, so drags across it still scroll the page; taps jump to the tapped value ([SkUiSlider.md](docs/controls/SkUiSlider.md)).
- **`SkUiProgressBar`** follows MAUI's ProgressBar API plus `TrackColor` and `IsIndeterminate`; the indeterminate segment slides on the render thread without re-recording ([SkUiProgressBar.md](docs/controls/SkUiProgressBar.md)).
- **`SkUiVerticalStackLayout`** / **`SkUiHorizontalStackLayout`** reuse MAUI's `VerticalStackLayoutManager`/`HorizontalStackLayoutManager` (just a `Spacing` property beyond the shared `SkUiLayout` padding/Children). **`SkUiAbsoluteLayout`** reuses `AbsoluteLayoutManager` and delegates to MAUI's `AbsoluteLayout.GetLayoutBounds`/`SetLayoutBounds`/`GetLayoutFlags`/`SetLayoutFlags` attached properties (same delegation pattern `SkUiGrid` uses for `Grid.Row`/`Column`). **`SkUiFlexLayout`** implements `IFlexLayout` for MAUI's `FlexLayoutManager` and reuses MAUI's `FlexLayout.Order` / `Grow` / `Shrink` / `AlignSelf` / `Basis` attached properties; MAUI's flex engine is internal, so a port of it computes the frames, and a headless test matches them against MAUI's `FlexLayout` ([SkUiFlexLayout.md](docs/controls/SkUiFlexLayout.md)). **`SkUiWrapLayout`** / **`SkUiCoreWrapLayout`** wrap children onto rows with `Spacing` and `RowSpacing` ([SkUiWrapLayout.md](docs/controls/SkUiWrapLayout.md)). The **shrink stacks** (both axes, both layers) share an overflow among their children by shrink factor (`None`, `Auto`, or a number as CSS `flex-shrink`, never below a child's minimum size), and are plain stacks that take only the space they need otherwise ([SkUiShrinkLayout.md](docs/controls/SkUiShrinkLayout.md)). Wrap and shrink run one engine for both layers.

### Verification status

- Headless suite is grouped by functionality (`PipelineTests`, `RenderingTests`, `BasicControlsTests`, `TextShapingTests`, `TextRenderingModeTests`, `LabelTextPropertiesTests`, `LayoutTests`, `FlexLayoutTests`, `CoreAttachedPropertyTests`, `WrapLayoutTests`, `ShrinkLayoutTests`, `RtlLayoutTests`, `GestureTests`, `ScrollViewTests`, `MauiContentViewTests`, `OverlayScrollTests`, `MemoryLeakTests`, `VisualTreeTests`, `PerformanceTests`, `CoreGridLayoutTests`, `CoreTableLayoutTests`, `ToggleStateTests`, `SliderTests`, `ProgressBarTests`, `AnimationClockTests`, `TransitionTests`, plus Core / look / demo contract tests). Run `dotnet test tests/MauiSkiaUi.Tests/MauiSkiaUi.Tests.csproj`.
- Memory-leak scenarios also run on devices with real handlers (`tests/MauiSkiaUi.DeviceTests`, `scripts/device_tests.sh`; Mac Catalyst in CI) — see [Testing.md](docs/design/Testing.md#memory-leak-tests).
- The library is `IsAotCompatible` with the trim / AOT analyzers failing its build; see [README.md](README.md) for the Native AOT / trimming results per platform.
- Completed surface includes FR-16 native hosting (`SkUiMauiContentView` + per-platform overlay container) and the layouts/controls listed above, each with a gallery demo page (native side-by-side where MAUI has a direct counterpart). Android/iOS/Mac Catalyst diagnostic builds pass with 0 warnings. Per-control markdown docs (NFR-5) are under [docs/controls/](docs/controls/README.md). See [ImplementationPlan.md](docs/design/ImplementationPlan.md) for completed vs backlog.
- **iOS teardown:** `SkUiViewHandler.DisconnectHandler` unregisters the Metal surface from the render loop (waiting for an in-flight frame), detaches the Skia platform view from `SkUiOverlayContainer`, then disconnects the surface handler before the host is disposed. The root container (`SkUiOverlayContainer`) derives from MAUI `MauiView` so MAUI's Loaded/Unloaded tracking uses MovedToWindow instead of installing `bounds`/`frame` KVO observers on its `CALayer`; leaked observers there caused `EXC_BAD_ACCESS` in `_NSKeyValueObservationInfoGetObservances` from `UIView dealloc` → `removeFromSuperview` (NSObject disposer). Not reproduced locally before or after the change, so the stress page keeps its teardown delays until device runs confirm. [SkiaSharp #3178](https://github.com/mono/SkiaSharp/issues/3178) is historical context for a Metal `GRContext` disposal bug that has since been fixed.
- Review regressions (covered by tests): `SkUiRadioButton` select-only tap; `SkUiBorder` `BackgroundColor` fallback; `SkUiActivityIndicator` stops clock on detach; `ComputeRootRelativeFrame()` includes translations; SkUiImage decode completion on UI thread; Button single-command execution and rounded text clip; Grid attached-property invalidation.
- A 1,000-label, 400x600-DIP headless Debug measurement improved warm CPU recording from **8.106 ms / 568,384 managed bytes per frame** to **0.635 ms / 9,488 bytes** after clip rejection and cached ordering (30 frames, same Mac). These are indicative single-run measurements, not device FPS or release performance guarantees. Native allocations and initial layout costs are not included in the per-frame allocation figure; near-zero allocation remains future work.
- **Device checks:** gestures, nested scrolling and native overlays (positioning, clipping, snapshots) were verified on a Galaxy S9, an iPhone / the iOS simulator and Windows 11 ([EventMechanism.md](docs/design/EventMechanism.md#verification), [ScrollingAndCollectionViews.md](docs/design/ScrollingAndCollectionViews.md#implemented), [WindowsValidation-results.md](docs/design/WindowsValidation-results.md)). Still open: a full gallery pass, rendered colors/contrast, and GPU frame-rate targets. The installed MAUI extension's DevFlow injection target still fails with `MSB4099`, blocking Copilot-triggered launch; the extension was not modified. Headless tests do not replace device checks. See [Testing.md](docs/design/Testing.md#how-to-verify-device-rendering--live-update-behavior).

### Demo asset

The bundled `earth.jpg` is NASA's Blue Marble western hemisphere image, downloaded from [NASA Visible Earth](https://eoimages.gsfc.nasa.gov/images/imagerecords/57000/57723/globe_west_2048.jpg). Credit: NASA / Visible Earth. It is packaged under `Resources/Raw` so the demo works offline.

### Tooling

- Git repository initialized at the solution root.
- `.gitignore` covers .NET/MAUI build outputs, IDE files, and OS artifacts.
