# Changelog

Release notes for the NuGet package **SkiaUi.Maui**.

Pack and publish workflows copy the body under `## <version>` into the package `PackageReleaseNotes` field. nuget.org shows that text on the package page. The heading must match `Version` in [Directory.Build.props](Directory.Build.props) exactly (newest section first). A link back to this file is appended when the notes are extracted.

## Unreleased

- **Retained compositor with a render thread** ([RenderingPipeline.md](docs/design/RenderingPipeline.md)): the UI thread records only the nodes whose content changed; GPU surfaces composite on a render thread. Offset, transform, opacity, clip and scroll offset are composite-time properties (no re-record).
- **Metal on iOS / Mac Catalyst** replaces the deprecated GLKView-based `SKGLView`; Android GPU surfaces render on their own GL thread.
- **Render-thread animation:** new `AnimateAsync` on `SkUiView` and Core nodes (opacity, translation, rotation, scale); `SkUiScrollView` fling / `ScrollToAsync` / `AnimateScrollTo` and activity indicators keep running while the UI thread is busy.
- **Breaking:** `ClipToBounds` is on for leaf controls but off for layouts / content hosts (MAUI parity); containers no longer paint children inside `OnPaintContent` (the engine composites them); `SkUiView.PaintChild` / `SkUiCoreNode.PaintChild` and `SkUiScrollView` picture-cache diagnostics were removed; `PaintInvalidated` fires on the invalidated node and once per frame on the root.
- Core nodes gain `Opacity`, `TranslationX/Y`, `Rotation`, `Scale`, `ClipToBounds` with transform-aware hit testing; Core arrange cache fixed.
- Text: one shared engine for `SkUiLabel` / `SkUiCoreLabel` with cached typefaces / fonts and linear wrapping.
- **Text shaping and RTL:** HarfBuzz shaping (new dependency `SkiaSharp.HarfBuzz`) for complex scripts, ligatures and emoji sequences; bidirectional text via the Unicode bidi algorithm; per-character system font fallback; RTL-aware `Start` / `End` alignment. `SkUiLabel` follows `FlowDirection`; `SkUiCoreLabel` gains `TextDirection`.
- **Text rendering modes:** `TextRendering` on `SkUiLabel` / `SkUiCoreLabel` (and buttons) plus the global `SkUiTextOptions.DefaultRendering`. `Auto` (default) takes a fast path for plain Latin text, restoring the pre-HarfBuzz first-frame cost for dense text UIs; `Shaped` always uses HarfBuzz; `Simple` never shapes. Measure-then-draw at a wider width reuses the layout.
- **Core grid:** star-column / star-row cells were measured twice per layout — first at an unresolved (zero) star size, then again after resolution — which also forced expensive zero-width text wrapping. Each cell is now measured once at its resolved size (Galaxy S9, Release, 1,000 buttons: Core layout + first frame 189.5 → 107 ms; SkUi* is 169.5 ms).
- **RTL layout:** `FlowDirection="RightToLeft"` (explicit or inherited) mirrors drawn layouts like native MAUI — grids, stacks, absolute layouts, alignment and margins, hit-testing and native overlays; horizontal scroll views start at the right; Switch / CheckBox / RadioButton and Core table chrome follow. Core nodes gain `FlowDirection`. Demo pages have a shared FlowDirection editor.
- Images decode downsampled to `SkUiImageDecoder.MaxDecodeDimension` (layout keeps source size); `SkUiFonts` picks up MAUI `ConfigureFonts` fonts and is thread-safe.
- Multi-pointer native touch and wheel / trackpad scroll on GPU surfaces.
- Requirements: FR-20 (shadows, future) and NFR-6 (threading).

## 1.0.0-Prerelease03

- Borders (`SkUiBorder` / `SkUiCoreBorder`) support MAUI-style per-corner `CornerRadius`; fill paints in Background and stroke in Overlay so content cannot cover the border.
- Look helpers (`SkUiLook` / `DefaultSkUiLook` / `SkUiChrome`) gain matching per-corner draw/path overloads while keeping uniform-radius subclass hooks.
- `SkUiCoreLabel` wraps and truncates via stock `LineBreakMode` breakers or a custom `LineBreaker` delegate (same modes as `SkUiLabel`).
- iOS GPU present: compose into a retained offscreen surface (GPU when available) and `Src`-blit the full frame to avoid WidthRequest shrink ghosts; root clear stays transparent when no solid background.
- Hardened iOS Skia surface teardown before handler disconnect; keep HW animations alive while a `ScrollView` is tracking.
- Component demo pages can toggle `HwAccelerated` on the preview host without leaving the page.

## 1.0.0-Prerelease02

- Core grid (`SkUiCoreGrid`): Auto, absolute, and star tracks, spans, spacing, and per-track min/max that clamp a star share instead of inflating it.
- Core table (`SkUiCoreTable`): row, column, and cell backgrounds plus span-aware separators on top of the same grid layout.
- Demo gallery pages for Core grid and table; stress harness builds grids and disconnects the previous tree before GC so iOS does not crash during UIView teardown.
- Dependency updates: SkiaSharp 4.152.1, CommunityToolkit.Maui 15.0.1, and current test SDK packages.

## 1.0.0-Prerelease01

- MAUI-compatible `SkUi*` controls and layouts drawn with SkiaSharp on one shared surface, with GPU acceleration when available.
- Core layer (`SkUiCore*`) for custom controls and dense UI without a MAUI `View` per node, hosted through `SkUiCoreHost`.
- Native overlays (`SkUiMauiContentView`) for Entry, Editor, WebView, and similar.
- Shared control look and color scheme; XAML, bindings, and styles on `SkUi*`.
- Demo gallery, stress harness, and headless tests.
- GitHub Actions for CI, multi-TFM NuGet pack/publish (Trusted Publishing), and demo builds.
- NuGet package id `SkiaUi.Maui`.
