# SkiaUi architecture review (2026-09-25)

Review of the PRDs in this folder and the `MauiSkiaUi` implementation, compared against local checkouts of .NET MAUI, DrawnUi, Flutter, Avalonia, Uno Platform, and Open-Maui (see [`.cursor/rules/reference-sources.mdc`](../../.cursor/rules/reference-sources.mdc)). Goal: the fastest and most flexible Skia-drawn UI for MAUI.

**Summary.** The foundation is sound. The strongest design decisions are the two layers (MAUI-compatible `SkUi*` plus the lightweight Core layer), keeping MAUI's layout rules, and hosting native controls as overlays. To be the fastest option, three things are missing:

- a threading model;
- retained paint caching with cheap invalidation;
- a single shared engine. Today `SkUi*` and Core each have their own paint, touch, invalidation, and animation code.

## 1. Implementation problems

Ordered by severity.

1. **Paint likely runs off the UI thread on Android (verify first).**
   - MAUI's `SKGLView` on Android is a `SKGLTextureView`, which draws on its own `GLThread`.
   - That means [`SkUiViewHandler.PaintSurface`](../../MauiSkiaUi/SkUiViewHandler.cs) walks the whole tree on the GL thread while the UI thread mutates it.
   - Under `HasRenderLoop` it also ticks animations there, so setters and `PropertyChanged` fire off the UI thread.
   - `SkUiFrameRenderer._gate` is not thread-safe.
2. **iOS uses deprecated OpenGL ES.**
   - SkiaSharp's MAUI `SKGLView` on iOS is backed by `GLKView`; the assembly itself carries the warning "Use 'Metal' instead".
   - The "ghost strokes" that led to the full-frame offscreen compose plus Src blit (`SkUiFrameRenderer.ReplayViaOpaqueBlit`) are most likely a GLKView artifact.
   - That workaround costs an extra full-screen copy every frame.
   - DrawnUi (`SKMetalViewRetained`) and Uno (`UnoSKMetalView`) both use `MTKView` with Metal.
3. **Invalidation walks the whole ancestor chain every time.**
   - `InvalidatePaint` / `FlushInvalidation` in `SkUiView` and `SkUiCoreNode` walk up to the root and raise an event at each level, even when the path is already dirty.
   - Every arrange also calls `InvalidatePaint`.
   - So the first layout of N nodes costs O(N × depth) just in propagation.
4. **Core arrange cache almost never hits.** `SkUiCoreNode.Arrange` compares the incoming `bounds` against `_frame`, but `_frame` is the margin-deflated, explicit-size-capped rect. Any node with a margin or explicit size therefore re-arranges its whole subtree on every parent arrange.
5. **Text costs a lot on every measure and paint.**
   - Every measure and paint calls `SKTypeface.FromFamilyName` and creates and disposes an `SKFont`.
   - `SkUiLabel` word wrap is O(n²): it re-measures the growing substring for each grapheme.
   - There is no shaping (HarfBuzz), so complex scripts, RTL, ligatures, and emoji fallback are wrong.
   - The label logic is duplicated between the MAUI and Core layers; FR-C6 is not done.
6. **Allocations in the paint path, which NFR-2 forbids.**
   - A new `SKPaint` is created on every paint for opacity and for the default background (`SkUiView`).
   - `DefaultSkUiLook` allocates a paint and a path for every rounded rectangle.
7. **Every node clips to its bounds** (`SkUiView.Paint`). This costs on every node and makes shadows, focus rings, and press-scale overflow impossible. Clipping should be opt-in, like MAUI `IsClippedToBounds`.
8. **Smaller issues:**
   - Images decode at full source resolution, not display size.
   - `SkUiFonts` is not thread-safe and needs manual registration; MAUI's `IFontRegistrar.GetFont` could resolve `ConfigureFonts` aliases instead.
   - Core has no opacity or transforms.
   - Touch handles a single pointer, with no nested-scroll arbitration.
   - `SkUiScrollView` records the whole content extent into one `SKPicture`, which does not scale to long content.

## 2. Recommended architectural changes

1. **Core as the engine, `SkUi*` as thin facades.** Each `SkUi*` owns a Core node (FR-C6, generalized), so hit-testing, paint, invalidation, and the clock exist once. Measured: Core is 12–52× faster, and the per-node MAUI `View` is the dominant cost.
2. **Own the platform surfaces and define a threading model.**
   - The UI thread records a frame (`SKPicture`); a render thread replays it (Uno's `FramePicture`). This removes the Android race by design.
   - Apple: Metal (`MTKView`, paused and driven by a display link).
   - Android: a GL `TextureView`/`SurfaceView` that only renders when dirty.
3. **Phased frame pipeline** (Flutter): animate → layout → paint → semantics, all on one vsync tick.
   - Dirty lists sorted by depth.
   - Idempotent dirty marking that stops at the first node already dirty.
   - Relayout boundaries: a tight or fixed-size child stops measure propagation.
   - A layout pass cap, as in Avalonia (`MaxPasses = 10`).
4. **Retained paint at repaint boundaries.**
   - Keep an `SKPicture` per boundary node.
   - Apply opacity, transform, and clip at composite time, so animating them never re-records.
   - Collapse subtrees that have been stable for N frames (Uno).
   - Raster-cache with a per-frame budget (Flutter: 3 stable frames, at most 3 new entries per frame).
   - Generalize the ad-hoc `SkUiScrollView` picture cache into this.
5. **Hot-path resource caching.**
   - Pool paints; cache typefaces and fonts; cache `SKTextBlob`s per line.
   - Shape text with HarfBuzz.
   - Paint-only text changes must not re-run line breaking (Flutter `RenderComparison`).
6. **Gesture arena.**
   - Hit-test once per pointer-down.
   - Recognizers compete for the gesture using movement thresholds.
   - Multi-pointer support and nested-scroll handoff.
   - Android: `RequestDisallowInterceptTouchEvent`. iOS: handle `TouchesCancelled`.
7. **Sliver-style virtualization.**
   - O(1) fixed-extent indexing.
   - For variable sizes: estimate, then correct with scroll anchoring.
   - Recycle views by recycle key.
   - A cache extent beyond the viewport, plus keep-alive.
8. **Masked native overlays** (Uno): a z-ordered `SKPath` turned into a `CAShapeLayer` mask, so Skia content can draw over native views and overlays can be clipped.
9. **Use MAUI services:**
   - `ITicker` for pacing and reduce-motion;
   - `IFontRegistrar` for fonts;
   - `IVisualTreeElement` / `VisualDiagnostics` so drawn nodes show up in Live Visual Tree.

## 3. PRD review

- **Stale checkboxes:** FR-3a, FR-14, the DrawingMechanism checklist, and the ScrollView v1 checklist are unchecked, but Development.md and ImplementationPlan.md say they are done. DrawingMechanism.md has a duplicated `## Architecture` heading.
- **Missing requirements:**
  - threading model;
  - accessibility;
  - text shaping, RTL, and bidi;
  - keyboard focus and tab order;
  - shadows (added as FR-20);
  - a per-scenario performance budget (frame ms, allocations per frame) enforced by `PerformanceTests`.
- **FR-16, "no `SkUiEntry`":** DrawnUi, Uno, and Avalonia all draw their text box and route IME through an invisible native field. Native overlays break under transforms, clipping, and scroll snapshots. Recommendation:
  - keep overlays for WebView and media;
  - plan a drawn `SkUiEntry` with a hidden IME proxy for v2.
- **CoreRequirements:** FR-C1 (separate assembly) and FR-C6 (shared paint and measure) are the architectural keystone and should come before new controls.

## 4. Suggested new features

Ordered by value.

1. Virtualized `SkUiCollectionView`: fixed-extent path, estimate plus anchoring, grouping, sticky headers.
2. Accessibility: a semantics tree mapped to `ExploreByTouchHelper` (Android) and `UIAccessibilityElement` (iOS). DrawnUi has neither on mobile, so this would be a differentiator.
3. Drawn `SkUiEntry` / `SkUiEditor` with a hidden IME proxy.
4. HarfBuzz shaping, RTL, and spans / FormattedText.
5. Brushes and effects: gradients (only solid colors today), shadows (FR-20), blur, `SKRuntimeEffect` shaders.
6. Slider, ProgressBar, Stepper, Picker, SwipeView, RefreshView, CarouselView, Expander.
7. Spring and fling physics simulations; `FadeTo`/`TranslateTo`-style async helpers.
8. SVG and Lottie (Skottie).
9. Diagnostics overlay: FPS, dirty regions, cache hits.
10. Golden-image rendering tests.

## 5. Comparison with other Skia-in-MAUI options

Only **DrawnUi** solves the same problem.

- **Open-Maui** is a Linux MAUI backend that draws everything on the CPU.
- **Uno** and **Avalonia** are full frameworks with Skia renderers; their drawn trees cannot be hosted inside a MAUI page.
- **MAUI `GraphicsView`** is not Skia (CoreGraphics on the CPU on iOS).

| | **SkiaUi** | **DrawnUi** | **Raw `SKCanvasView` / GraphicsView** | **Open-Maui** |
| --- | --- | --- | --- | --- |
| Node cost | Light Core nodes, plus MAUI-View facades | Every node is a `VisualElement` | n/a (draw everything yourself) | Heavy |
| Layout | MAUI layout managers (drop-in parity) | Own system | none | Reimplemented, no measure cache |
| Size | ~9.4k LOC, readable | ~132k LOC, god-classes | tiny | ~82k LOC |
| GPU | GL; GLKView on iOS | Metal on iOS, GL thread on Android, ANGLE on Windows | varies | CPU only |
| Caching | Full redraw; ScrollView picture only | Rich cache types, but hand-tuned and fragile | manual | none |
| Controls | ~20 | ~70 (Shell, markdown, Lottie, SVG, shaders, drawn editor) | none | ~55 handlers |
| Virtualization | none | yes, with many knobs | none | index cache, no recycling |
| Text | no shaping | HarfBuzz, spans | manual | no shaping |
| Accessibility | none | Windows only | none | stub |
| Docs / tests | strong PRDs, headless tests | docs, few tests | n/a | weak |

**SkiaUi strengths:**

- MAUI layout and XAML parity, which makes it a true drop-in.
- Core nodes are about an order of magnitude cheaper than DrawnUi's `VisualElement` nodes.
- The codebase is small enough to optimize thoroughly.
- Look, color scheme, and style are cleanly separated.
- Design is documented and tests run headless.

**SkiaUi weaknesses:**

- Far fewer controls, and no virtualization.
- No drawn text input, text shaping, RTL, or accessibility.
- iOS runs on deprecated GL, and the Android paint thread is likely unsafe.
- No general retained cache.
- Single-pointer gestures only.
- Still prerelease.

**Verdict:** SkiaUi can beat DrawnUi on raw speed and predictability, because it has lighter nodes and needs no per-control cache tuning. That depends on changes 2.1–2.4 above, not on adding features.
