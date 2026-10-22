# SkiaUi requirements

What SkiaUi must do and the rules every change follows. How it is built: [Architecture.md](Architecture.md). What comes next: [Roadmap.md](Roadmap.md). The detailed 1.0 requirement checklists (every FR's delivered items) are archived in [docs/archive/Requirements-1.0.md](../archive/Requirements-1.0.md) and [docs/archive/CoreRequirements.md](../archive/CoreRequirements.md).

## Goals

- **Fast MAUI UI.** Replace a slow MAUI visual tree with controls and layouts drawn by SkiaSharp on one GPU surface per region of a page, instead of one platform view per control.
- **Near drop-in for MAUI.** A drawn copy of a MAUI control has MAUI's API and behavior, and lays out with MAUI's measure / arrange model and layout managers, so a page ports by changing the XAML prefix (`sk:SkUiLabel` for `Label`).
- **XAML first, code too.** Whole drawn trees are written in XAML as MAUI layouts nest children, with bindings, styles and visual states; the same trees can be built in C#.
- **A low-level layer** (Core: `SkUiCore*`) for dense trees and app-made controls built in code, without `BindableObject` costs, drawn by the same engines.
- **Native where it must be.** Controls that need a platform engine (web views, maps, media) are hosted as native overlays inside the drawn tree, not redrawn. Text input is drawn; the platform keyboard and input methods are reached through a proxy.

## Scope

**In scope:** drawn controls and layouts for page content; scrolling, virtual lists and collections; drawn text input; looks that match each platform's native controls; accessibility, keyboard focus and font scaling; popups through the app's popup system; hosting native controls.

**Out of scope:** pages, Shell and navigation (they stay MAUI); popup windows of SkiaUi's own; drawn web views, maps and media; controls MAUI made obsolete (Frame, ListView, TableView, cells); Blazor Hybrid hosts; MAUI `GestureRecognizers` as SkiaUi's gesture system (a bridge runs taps).

## Principles

- **MAUI names and behavior win.** Where SkiaUi diverged from a MAUI member, the MAUI member is added and SkiaUi's renamed or removed. Each MAUI counterpart is checked by XAML parity tests: the control's samples from the MAUI docs, with only the prefix changed, load, bind and measure as documented.
- **Parity never costs performance or architecture.** Where a MAUI API works against them (UI-thread animation callbacks, per-frame bindable changes, a view per cell), SkiaUi ships its own API plus a conversion path in [Migration.md](../Migration.md) and the migration skills (`plugins/skiaui-migration`).
- **Both layers.** A control exists on SkUi\* and Core through one shared engine or painter, unless it is layout-only on one of them; SkUi-vs-Core pixel tests keep them equal.
- **Drawn by the look.** Every visual goes through `SkUiLook` entry points and paint structs, with colors from the color scheme, so a look can redraw any control.
- **Simple and correct first, then fast.** A new path ships simple and tested; hot paths are then optimized toward no allocations per frame, measured with `scripts/bench.sh`.

## Reference sources

Use these public repositories when designing or implementing SkiaUi (layout, input, XAML, rendering patterns). Prefer patterns that fit the `SkUiContentView` → `ISkUiView` model; do not copy frameworks wholesale.

| Framework | URL | Typical relevance |
| --- | --- | --- |
| .NET MAUI | https://github.com/dotnet/maui | **Primary layout model:** `IView` measure/arrange, layout managers, XAML/`ContentProperty`, gesture/handler patterns; control API parity |
| Open-Maui | https://github.com/open-maui/maui-linux | MAUI-compatible stack / Linux and alternate host patterns |
| Flutter | https://github.com/flutter/flutter | Optional: paint / compositor `Layer` ideas only — **not** the SkiaUi layout contract |
| Avalonia | https://github.com/AvaloniaUI/Avalonia | Visual tree, `Border`/`ContentPresenter` composition, Composition visuals for opacity/transform layers |
| Uno Platform | https://github.com/unoplatform/uno | Cross-platform control/layout; Border/ContentControl and ControlTemplate part patterns |
| DrawnUi (MAUI) | https://github.com/taublast/drawnui | Skia-drawn MAUI controls; **`SkiaMauiElement`** native overlay + snapshot pattern (FR-16); `SkiaControl` child trees |
| SkiaSharp | https://github.com/mono/SkiaSharp | SkiaSharp / `SKGLView` APIs, MAUI views, paint surface, GPU hosting |

When borrowing an idea, note the source briefly in design discussion or code comments where non-obvious.

## Platforms

| Platform | Target | Minimum |
| --- | --- | --- |
| Android | `net10.0-android` | Android 5.0 (API 21) |
| iOS | `net10.0-ios` | iOS 15 |
| Mac Catalyst | `net10.0-maccatalyst` | Mac Catalyst 15 (17 from 1.1.0) |
| Windows | `net10.0-windows10.0.19041.0` | Windows 10 1809 |

.NET MAUI 10.x only; the MAUI and Xcode version of each release is in the [roadmap](Roadmap.md#releases). `net10.0` builds the library for headless tests.

## Non-functional requirements

| # | Requirement |
| --- | --- |
| NFR-2 Performance | Redraw only what changed: retained per-node pictures, composite-time offsets, transforms, opacity and clips; measure cached per constraint. No per-frame heap allocations on hot paths (measure, arrange, record, composite, input, animation ticks); `unsafe` and spans where profiling shows a benefit. Frames are requested only while something changes or animates. |
| NFR-6 Threading | The UI thread only lays out, records changed nodes and commits. GPU surfaces composite, rasterize and run composite-time animations on a render thread that never touches MAUI objects, so motion stays smooth while the UI thread is busy ([RenderingPipeline.md](RenderingPipeline.md)). |
| NFR-3 Quality | Nullable reference types; 0 build warnings; trimming and Native AOT compatible (analyzer warnings fail the build); headless tests by functionality, a memory-leak scenario for each stateful control, a demo page for each control ([Testing.md](Testing.md)). |
| NFR-4 Extensibility | Virtual hooks and interfaces where apps customize (paint phases, measure, look painters, popup presenters, line breakers); shared helpers instead of copied chrome; documented extension points. |
| NFR-5 Documentation | XML docs on every public member; one page per control in [docs/controls/](../controls/README.md): for MAUI counterparts, the differences and extensions only, with a link to MAUI's docs; the migration guide and skills updated when parity changes. |

NFR-1 (platforms) is the table above.

## Functional requirements

Status: **Shipped** (in the current release line), **Partly** (shipped, with open items in the [roadmap](Roadmap.md)), **Open**.

| # | Requirement | Status | Docs |
| --- | --- | --- | --- |
| FR-1 | Bridge and contract: `ISkUiView : IView`, `SkUiView`, `SkUiContentView`, `SkUiLayout`, custom handler, `UseSkiaUi()` | Shipped | [Architecture](Architecture.md#one-surface-many-drawn-views), [SkUiView](../controls/SkUiView.md) |
| FR-2 | XAML composition of whole drawn trees | Shipped | [SkUiLayout](../controls/SkUiLayout.md) |
| FR-3, FR-3a | Layouts on MAUI's measure / arrange and layout managers; selective layout | Shipped | [LayoutSystem](LayoutSystem.md) |
| FR-4 | Basic controls (label, button, image, toggles, indicators, shapes) | Shipped | [controls](../controls/README.md) |
| FR-5 | Demo gallery: a page per control, native side by side where MAUI has one | Partly (device pass open) | [Development](../../Development.md#solution-structure) |
| FR-6 | NuGet packaging | Shipped | [Releasing](../Releasing.md) |
| FR-7 | Animation: render-thread composite animations, UI clock | Shipped | [Architecture](Architecture.md#animation) |
| FR-8, FR-9 | Transparency; Background / Content / Overlay paint phases | Shipped | [Architecture](Architecture.md#drawing-and-the-render-pipeline) |
| FR-10 | Bindable properties with fluent setters, update batching | Shipped | [Architecture](Architecture.md#two-layers-one-tree) |
| FR-11 | Clipping (bounds, MAUI `Clip`, shapes); hit-testing by arranged bounds | Shipped | [RenderingPipeline](RenderingPipeline.md#shadows-and-clips-fr-20-fr-11) |
| FR-12 | MAUI styles and visual states on SkUi\* controls | Shipped | [SkUiView](../controls/SkUiView.md) |
| FR-13, FR-14 | Hosted vs standalone views; `HwAccelerated` and the custom handler | Shipped | [LayoutSystem](LayoutSystem.md#hosted-vs-standalone) |
| FR-15 | SkiaUi-owned gestures (arena per pointer); MAUI taps bridged | Partly (other MAUI recognizers) | [EventMechanism](EventMechanism.md) |
| FR-16 | Native controls hosted as overlays; drawn text input through a platform proxy | Shipped | [SkUiMauiContentView](../controls/SkUiMauiContentView.md), [TextInput](TextInput.md) |
| FR-17 | Scrolling inside the drawn tree | Shipped | [ScrollingAndCollectionViews](ScrollingAndCollectionViews.md) |
| FR-18 | Control look: shapes, sizes and motion; platform looks | Shipped | [ControlLook](ControlLook.md) |
| FR-19 | Color scheme: default palette, light / dark and platform schemes | Partly (per-tree schemes) | [ColorScheme](ColorScheme.md) |
| FR-20 | Shadows on every view | Shipped | [RenderingPipeline](RenderingPipeline.md#shadows-and-clips-fr-20-fr-11) |
| FR-21 | Virtual scroll layout (items realized near the viewport, recycled) | Partly (scroll modes, horizontal layout) | [SkUiVirtualVerticalStackLayout](../controls/SkUiVirtualVerticalStackLayout.md) |
| FR-22 | `SkUiCollectionView` | Partly (reordering, snap points) | [SkUiCollectionView](../controls/SkUiCollectionView.md) |
| FR-23 | Three-state toggles | Shipped | [SkUiToggleControl](../controls/SkUiToggleControl.md) |
| FR-24 | Slider (also vertical) | Shipped | [SkUiSlider](../controls/SkUiSlider.md) |
| FR-25 | Progress bar (also indeterminate) | Shipped | [SkUiProgressBar](../controls/SkUiProgressBar.md) |
| FR-26 | State-change animations drawn by the look | Shipped | [ControlLook](ControlLook.md#state-change-transitions-fr-26) |
| FR-27 | Composition layouts: flex, wrap, shrink, align stack | Shipped | [SkUiFlexLayout](../controls/SkUiFlexLayout.md), [SkUiWrapLayout](../controls/SkUiWrapLayout.md), [SkUiShrinkLayout](../controls/SkUiShrinkLayout.md) |
| FR-28 | Accessibility, keyboard focus, font scaling | Shipped | [Accessibility](Accessibility.md) |
| FR-29 | Pickers on the app's popup system | Shipped | [SkUiPicker](../controls/SkUiPicker.md), [Popups](Popups.md) |
| FR-30 | Avatar view | Shipped | [SkUiAvatarView](../controls/SkUiAvatarView.md) |
| FR-31 | Shimmer (skeleton loading) | Shipped | [SkUiShimmer](../controls/SkUiShimmer.md) |
| FR-32 | Carousel and indicator view | Shipped | [SkUiCarouselView](../controls/SkUiCarouselView.md) |
| FR-33 | Calendar | Open | [Roadmap](Roadmap.md#new-controls-and-packages) |

### Core layer (FR-C)

| # | Requirement | Status |
| --- | --- | --- |
| FR-C1 | Core types use no MAUI Controls types (`Microsoft.Maui.Graphics` primitives are allowed). They ship in the `SkiaUi.Maui` assembly and package | Partly (a separate Core assembly is open) |
| FR-C2 | Core is for dense trees and app-made controls, not XAML pages; `SkUiCoreHost` is its bridge into the SkUi tree | Shipped |
| FR-C3 | Core-owned layout, paint and touch, without MAUI layout managers | Shipped |
| FR-C4, FR-C5 | Fluent `Set*` as the one apply path, CLR properties, `INotifyPropertyChanged` (code bindings, MVVM Toolkit) | Shipped |
| FR-C6 | SkUi\* controls and their Core twins share engines and painters (not SkUi\* controls wrapping Core instances) | Shipped |
| FR-C8 | Core control set: layouts (stacks, absolute, grid, table, overlay, border, scroll view, wrap, shrink, align stack) and the controls listed in [SkUiCore.md](../controls/SkUiCore.md) | Shipped |
