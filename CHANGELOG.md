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
- **Text on FreeType hosts (Android, Linux):** fonts use linear (unhinted) metrics, so plain-text measurement matches HarfBuzz shaping (hinted advances differed by a fraction of a pixel per glyph). Typefaces without readable font data (Skia's empty default on a font-less Linux host) are laid out with Skia glyphs instead of throwing.
- **Text rendering modes:** `TextRendering` on `SkUiLabel` / `SkUiCoreLabel` (and buttons) plus the global `SkUiTextOptions.DefaultRendering`. `Auto` (default) takes a fast path for plain Latin text, restoring the pre-HarfBuzz first-frame cost for dense text UIs; `Shaped` always uses HarfBuzz; `Simple` never shapes. Measure-then-draw at a wider width reuses the layout.
- **Core grid:** star-column / star-row cells were measured twice per layout — first at an unresolved (zero) star size, then again after resolution — which also forced expensive zero-width text wrapping. Each cell is now measured once at its resolved size (Galaxy S9, Release, 1,000 buttons: Core layout + first frame 189.5 → 107 ms; SkUi* is 169.5 ms).
- **RTL layout:** `FlowDirection="RightToLeft"` (explicit or inherited) mirrors drawn layouts like native MAUI — grids, stacks, absolute layouts, alignment and margins, hit-testing and native overlays; horizontal scroll views start at the right; Switch / CheckBox / RadioButton and Core table chrome follow. Core nodes gain `FlowDirection`. Demo pages have a shared FlowDirection editor.
- Images decode downsampled to `SkUiImageDecoder.MaxDecodeDimension` (layout keeps source size); `SkUiFonts` picks up MAUI `ConfigureFonts` fonts and is thread-safe.
- Multi-pointer native touch and wheel / trackpad scroll on GPU surfaces.
- Requirements: FR-20 (shadows, future) and NFR-6 (threading).
- **Benchmarks** ([Benchmarks.md](docs/design/Benchmarks.md)): headless runner and a Release on-device bench app sharing one scenario catalog; `scripts/bench.sh --baseline <ref>` compares any git ref with the working tree (headless, Android, iOS, Mac Catalyst). New `SkUiView.GetRenderStatistics()` / `ResetRenderStatistics()` (render-thread frame count and cost).
- **iOS / Mac Catalyst:** commits that arrive while the render loop is idle are rendered at once instead of on the next display-link tick (commit → rendered ≈ 1 ms instead of up to a refresh period); surfaces render nothing before their first commit. Render statistics now include the GPU flush / present.
- **Drags that start on a native overlay** (Entry, Editor, WebView in a `SkUiMauiContentView`) now scroll the drawn scroll view around it (Android, iOS / Mac Catalyst).
  - **How:** the overlay's drawn ancestors see the drag; once a drawn scroll claims it, the native touch is cancelled.
  - **Native keeps:** taps, text selection and cursor placement. Controls that scroll their own content keep precedence.
  - **Windows:** not yet.
- **Native overlays while scrolling (FR-16):**
  - **Clipping:** `SkUiMauiContentView` overlays are clipped to ancestor scroll viewports and clipping ancestors. They no longer draw over or take touches outside the scroller.
  - **Snapshot while scrolling:** new `ScrollMode` (`Auto` = snapshot on Android / Windows, live on Apple; `Snapshot`; `Live`). While an ancestor scroller moves, the native view is replaced by a bitmap that moves in sync with the drawn content, then restored after `SnapshotRestoreDelay`. Focused controls stay live.
  - **`SkUiScrollView.IsScrolling`.**
  - **Fix:** overlays added before their stack was placed in a scroller never registered with it, so they missed offset sync.
  - **Demo:** "Native overlays in ScrollView".
- **Windows (first validated build)** ([WindowsValidation-results.md](docs/design/WindowsValidation-results.md)):
  - GPU surfaces render: root containers are measured and arranged (they stayed 0×0), and a frame drawn at a stale size after a resize is repainted.
  - Fixed an intermittent native crash on pages with GPU surfaces (Skia called into an unloaded `opengl32.dll`).
  - Continuous frames (flings, spinners) are paced to the compositor frame and stop for unloaded surfaces; before, the app could stop responding.
  - RTL no longer mirrors surface pixels; WebView overlays snapshot through WebView2 while scrolling.
  - Native overlays follow ancestors that move without resizing, and re-attach when their content moves to another root (all platforms).
  - The demo's `ApplicationDisplayVersion` is numeric (`1.0.0`), as the Windows build requires.
- **Demo:** "Native nesting" page with drawn surfaces (list, carousel, swipe row, Core scroll view) inside a native MAUI `ScrollView`, with a GPU / software switch.
- **Gesture arena (breaking):**
  - **Mechanism:** one per-pointer gesture arena for SkUi* and Core, replacing per-container touch routing. It hit-tests once per press; passive nodes pass through, and disabled nodes block.
  - **Recognizers:** tap / double tap, long press, pan, swipe, pinch / rotate, and a raw pointer recognizer. There are new `SkUiView` events and commands (`DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, `SwipeDirections`, `PanAxis`, `Gestures`), the same events on Core nodes, and app-wide `SkUiGestureSettings`.
  - **Press feedback:** a button inside a scroller shows its pressed state after 100 ms unless a scroll starts, and controls inside scrollers can drag.
  - **Multi-touch:** each pointer is independent.
  - **Breaking:** `ISkUiView.Touch` / `SkUiCoreNode.Touch` are dispatch entry points and no longer virtual; use `SkUiPointerGestureRecognizer` for custom input. `SkUiTouchRouter` is removed.
- **Nested scrolling and Core scrolling:**
  - **`SkUiCoreScrollView`:** new Core scroller. SkUi* and Core scrollers share one engine (render-thread fling / tween, wheel).
  - **Nesting:** orthogonal scrollers take their own axis; same-axis inner scrollers go first and chain the remainder and the fling outward; the wheel goes to the innermost scroller that can move.
  - **Native coordination:** native ancestors are held back while a drawn gesture may claim the touch (Android `RequestDisallowInterceptTouchEvent`, iOS gate recognizer), and they take over at a drawn scroller's edge.
- **Requirements:** FR-21 (virtual / dynamic scroll layout for endless scrolling) and FR-22 (`SkUiCollectionView` with MAUI `CollectionView` parity, sticky header / footer, selection background, item tap event / command).
- **Visual tree / diagnostics:**
  - Core nodes are `IVisualTreeElement`s. Live Visual Tree and automation agents reach them through `SkUiCoreHost`, and Core add/remove raises `VisualDiagnostics.VisualTreeChanged`. This only happens when MAUI diagnostics are on (Debug builds), and the check is cached.
  - New `SkUiDiagnostics` for drawn elements: `GetRootBounds`, `GetWindowBounds`, `HitTest`, `HitTestWindow`, `SimulateTap`. It works for both SkUi\* and Core, honoring transforms, scroll offsets and RTL.
  - Core nodes gain `AutomationId` / `SetAutomationId`.
  - The demo adds a DevFlow extension `dev.skiaui` (tree, tap, hit) for drawn elements.
- **Android:** continuous render-thread animations are vsync-paced (a `TextureView` swap does not block, so they previously rendered 300–500 discarded frames/s on a Galaxy S9); now 60 fps.

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
