# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

SkiaUi (NuGet `SkiaUi.Maui`, namespace `MauiSkiaUi`) is a .NET MAUI library that draws UI with SkiaSharp on one surface per host instead of one platform view per control. User-facing overview: [README.md](README.md); contributor guide (solution layout, CI, current implementation): [Development.md](Development.md).

## Commands

```bash
dotnet build SkiaUi.slnx                                   # everything (needs the MAUI workload + platform SDKs)
dotnet build MauiSkiaUi/MauiSkiaUi.csproj -f net10.0       # fast library-only build (the TFM the tests use)
dotnet build MauiSkiaUiDemo -f net10.0-maccatalyst         # demo app; also samples/MauiSkiaUiSamples

dotnet test tests/MauiSkiaUi.Tests/MauiSkiaUi.Tests.csproj                                     # headless suite (~10 s)
dotnet test tests/MauiSkiaUi.Tests/MauiSkiaUi.Tests.csproj --filter "FullyQualifiedName~LabelShrinkToFitTests"
dotnet test tests/MauiSkiaUi.Tests/MauiSkiaUi.Tests.csproj --filter "FullyQualifiedName~ClassName.MethodName"

scripts/bench.sh --baseline HEAD             # headless perf: working tree vs last commit (prints a verdict table)
scripts/bench.sh --baseline HEAD -n 10 -r 3 -S core-labels,skui-labels   # more samples, chosen scenarios
scripts/bench.sh --baseline HEAD --quiet-gc  # no GC inside iterations: separates code-path changes from GC timing
scripts/bench.sh --list                      # scenario catalog (benchmarks/Scenarios/Scenarios.cs)

scripts/device_verify.sh -l                  # pick a device/simulator, launch the demo, print a checklist
scripts/device_tests.sh                      # memory-leak scenarios on a device (tests/MauiSkiaUi.DeviceTests)
```

Toolchain is pinned: .NET SDK 10.0.400 ([global.json](global.json)); MAUI 10.0.110 and SkiaSharp 4.153.1 via `MauiVersion` / `SkiaSharpVersion` in [Directory.Build.props](Directory.Build.props) (also the product `Version`). Stay on MAUI 10.x. The library is `IsAotCompatible`: trim/AOT analyzer warnings fail its build, and the build is expected to have 0 warnings.

## Architecture

**Two layers, one tree.** `SkUiView` (a MAUI `View`, `SkUi*` controls: bindable properties, XAML, styles, MAUI layout managers) and `SkUiCoreNode` (plain CLR objects, `SkUiCore*`: fluent `Set*` + `INotifyPropertyChanged`, no `BindableObject`). `SkUiCoreHost` hosts a Core tree inside the SkUi tree. Most controls exist on both layers and share one engine (text, shapes, toggles, scrolling, images); keep them at parity when changing one. Only the surface root (`SkUiContentView` / `SkUiLayout` with `SkUiViewHandler`) has a platform view; hosted nodes have logical MAUI parents but no handlers. Native views are hosted as overlays through `SkUiMauiContentView`.

**Layout** uses MAUI's measure/arrange contract and layout managers (`GridLayoutManager`, stack managers, …), not Flutter-style constraints, so `SkUi*` can replace MAUI controls by changing the XAML prefix. Measure results are cached per constraint (`SkUiView.MeasureOverride`, `SkUiCoreNode.Measure`); controls implement `MeasureContent`.

**Rendering is retained and split across threads** ([docs/design/RenderingPipeline.md](docs/design/RenderingPipeline.md)):
- UI thread: tree changes, layout, and *recording* each dirty node's content into its own `SKPicture` (`SkUiRenderRecorder`), plus composite-time properties (`SkUiRenderProps`: offset, transforms, opacity, clip paths, shadow, scroll offset). Both layers implement `ISkUiRenderable`; invalidation is `SkUiRenderDirty` (Content / Props / Children) propagated once per frame to the root.
- A frame is committed as an `SkUiRenderBatch`; `SkUiCompositor` applies it and composites on the render thread (Metal thread on Apple, GL thread on Android; UI thread for software surfaces/Windows). `SkUiFrameRenderer` drives both halves and is what headless tests and benchmarks use directly.
- Render-thread animations (`AnimateAsync`, flings, indeterminate progress) change composite-time props without re-recording.
- **Native object ownership:** objects committed in `SkUiRenderProps` (clip paths, shadow outline/style) are disposed by the compositor when a commit replaces them or the render node goes; the UI side never disposes them, and on reset (`SkUiRenderInvalidation.ResetSubtree` → `ISkUiRenderable.ReleaseDrawingResources`, run when a subtree leaves its drawn parent or the surface is torn down) it forgets them and disposes what only recording used (text blobs, rounded clips, border outlines). Fonts and the text paint are shared (`SkUiTextResources`); nothing should be left to the finalizer.

**Text** is one engine for both label layers ([docs/controls/SkUiLabel.md](docs/controls/SkUiLabel.md)): `SkUiTextLayout` (plain text; HarfBuzz shaping with a simple Latin fast path, bidi, font fallback, wrap/truncation, custom `SkUiTextLineBreaker`s, cached lines keyed by `SkUiTextStyle` + width), `SkUiRichTextLayout` (spans and HTML via `SkUiHtml`), `SkUiShaping`, and `SkUiTextFit` (shrink / tighten / grow to fit; fitted text is laid out at base size and drawn with a canvas scale). `SkUiLabel` and `SkUiCoreLabel` hold parallel state and build the same `SkUiTextStyle`; buttons derive from the labels.

**Other shared mechanisms:** a per-pointer gesture arena (`MauiSkiaUi/Gestures`, [EventMechanism.md](docs/design/EventMechanism.md)); the look (`SkUiLook` / `DefaultSkUiLook`: sizes, chrome, transitions) and `SkUiColorScheme` drive defaults on both layers; one image loader and LRU cache for both layers (`MauiSkiaUi/Imaging`); a lazily built semantics tree per surface for accessibility (`MauiSkiaUi/Accessibility`); weak subscriptions to long-lived sources via `SkUiWeakListener` / `SkUiWeakEvent`.

## Code style

Follow JetBrains ReSharper's default C# code style and inspections. In particular:

- **Naming:** private instance fields and private static (non-readonly) fields `_camelCase`; `static readonly` fields and constants `PascalCase` (`private static readonly SkUiWeakEvent Changed = new();`, not `_changed`); locals and parameters `camelCase`. Some older `static readonly` fields still start with `_`; don't copy them.
- **`var`** for local variable declarations.
- **`field` keyword** (C# 14) in properties whose backing field only the accessors use, instead of a separate private field.
- **Floating point:** compare computed values with a tolerance, `Math.Abs(a - b) < Tolerance` with a named `const` (as `SkUiWrapEngine.Tolerance`, `SkUiTextFit.HeightTolerance`), never `==` (ReSharper's "possible loss of precision" / float equality inspection). Exact `==` stays where it is the point: a setter's "did the value change" early-out, cache keys, and sentinel values (`scale == 1`); say so in a comment when it is not obvious.

## Conventions

- **Property pattern (SkUi layer):** each `BindableProperty` has a `validateValue` from `SkUiValidate` (invalid values are ignored with a warning, as MAUI does), a fluent `SetX` that throws on invalid input and assigns the property, and a change callback that copies the value into a field the layout/paint code reads. Core: `SetX` validates, calls `SetProperty`, invalidates. CLR getters read the bindable store.
- Strings that are shown and compared (action sheet choices, keys) are `const`s, also in samples; fixed `PropertyChangedEventArgs` / reset event args are `static readonly`, not allocated per raise.
- Allocation and per-frame cost matter (NFR-2: simple and correct first, then optimize hot paths to near-zero allocations). Check perf-sensitive changes with `scripts/bench.sh --baseline HEAD`; a single phase jumping while others drop is usually GC timing (re-check with `--quiet-gc`). New benchmark scenarios set newer options through `TrySet` / `TrySetOption` (reflection) so a `--baseline` build of an older commit still compiles.
- **Tests** are headless xUnit on real MAUI objects with offscreen Skia (`tests/MauiSkiaUi.Tests`, `InternalsVisibleTo`). Classes that load XAML at runtime (or construct controls that do) belong to `[Collection(RuntimeXamlCollection.Name)]`. Text tests register the bundled Roboto Mono with `using var _ = SkUiTestHelpers.UseBundledFont();` and `FontFamily = SkUiTestHelpers.BundledFontFamily` (Linux CI has no system fonts). Leak scenarios live in `tests/Shared/MemoryLeaks` and run headless (`MemoryLeakTests`) and on devices.
- **When a feature ships:** add an entry under `## Unreleased` in [CHANGELOG.md](CHANGELOG.md) (`### New features` / `### Breaking changes` / `### Fixes` / `### Other`); update the control's page in [docs/controls/](docs/controls/README.md), the design doc it touches, and [docs/design/ImplementationPlan.md](docs/design/ImplementationPlan.md) / [Requirements.md](docs/design/Requirements.md) (check items off); give controls a page in the demo's component gallery (`MauiSkiaUiDemo/ComponentPages`, next to the native MAUI control where one exists). When MAUI parity or a gap changes, also update [docs/Migration.md](docs/Migration.md) and the migration skills in `plugins/skiaui-migration`. Parity never costs performance or architecture: where a MAUI API works against them, SkiaUi ships its own API plus a conversion path in those docs.
- Reference designs (consult, don't copy wholesale): .NET MAUI (the layout model and API parity), Open-Maui, Avalonia, Uno, DrawnUi (native overlays), SkiaSharp, and Flutter for paint / compositor ideas only; what each is consulted for is in [Requirements.md](docs/design/Requirements.md#reference-sources). Design docs index: [Development.md#design-documentation](Development.md#design-documentation).
