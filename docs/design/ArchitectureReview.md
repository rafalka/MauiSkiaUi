# SkiaUi architecture review (2026-09-29)

Re-review of the implementation against the previous review (2026-09-25), checked in the code rather than the docs. Goal unchanged: the fastest and most flexible Skia-drawn UI for MAUI.

**Resolved since the previous review** (removed from this document):
- **Rendering:** retained compositor with UI-thread recording and render-thread compositing (Metal on Apple, GL thread on Android), per-node pictures and composite-time properties, opt-in clipping.
- **Text:** shared engine with HarfBuzz shaping, bidi and font fallback; resource caching on the hot paths; downsampled image decode; thread-safe fonts with `ConfigureFonts`.
- **Core and input:** Core opacity / transforms and arrange cache; per-pointer gesture arena with nested scrolling and native coordination.
- **Diagnostics and requirements:** Core nodes in the Live Visual Tree; threading (NFR-6) and shadows (FR-20) recorded as requirements; stale checkboxes fixed.

**Summary.** The engine work the previous review asked for is done; the remaining risks are breadth and a few structural costs:
- the two layers still have duplicate control implementations, which are drifting apart;
- layout invalidation still walks to the root and re-records ancestors;
- there is no raster cache;
- virtualization, accessibility and look-driven state animations are missing.

## 1. Open from the previous review

| Item | Status | Notes |
| --- | --- | --- |
| Invalidation cost (old 1.3) | Partial | Render marks stop early; **measure** invalidation still walks to the root with no early-out and marks every ancestor's content dirty (N5) |
| Core as the engine, SkUi\* as facades (2.1) | Partial | Shared: render pipeline, text engine, gesture arena, scroll engine, look. Duplicated: two node bases and every control pair — already diverging (N3, N4, N13). See §4 |
| Phased pipeline: relayout boundaries (2.3) | Open | No relayout boundaries; MAUI drives layout, so no pass cap is needed |
| Raster cache of stable subtrees (2.4) | Open | Every animated frame replays the whole tree (N7) |
| Virtualization (2.7) | Open | FR-21 / FR-22 recorded; readiness gaps in §4 |
| Masked native overlays (2.8) | Open | Overlays clip to rectangles only; drawn content can't cover them |
| `ITicker` / reduce motion (2.9) | Open | Own `SkUiUiTicker`; the OS reduce-motion setting is not honored |
| Accessibility, keyboard focus, OS font scaling | Open | No semantics tree; drawn UI is invisible to TalkBack / VoiceOver (N8) |
| Enforced performance budget | Open | `PerformanceTests` logs numbers for the immediate painter, not the retained record / commit path |
| Drawn `SkUiEntry` with an IME proxy | Decided against | FR-16: host native text input; revisit only if overlays block a real need |
| Core as a separate assembly (FR-C1) | Open, re-decide | Core now uses MAUI Controls types (`IVisualElementController` for flow direction) |
| Features (old §4) | Open | CollectionView, accessibility, brushes and effects (non-solid `Background` is ignored), SwipeView / RefreshView / Carousel / Expander / Stepper / Picker, spring physics, SVG / Lottie, an on-screen diagnostics overlay, stored golden images. Slider, ProgressBar and spans / `FormattedText` are now shipped |

## 2. New findings

Ordered by severity. File references are to the code at the time of the review.

| # | Severity | Finding | Direction |
| --- | --- | --- | --- |
| N1 | High — **fixed** | `SkUiAnimationClock.Tick` indexed its list while callbacks could dispose animations or call `StopAll`: skipped animations or `ArgumentOutOfRangeException` inside the frame callback (app crash) | Fixed: removals during a tick are tombstoned and compacted after the loop (`AnimationClockTests`) |
| N2 | Medium | A press on a disabled node (or a button whose command can't execute) is swallowed with an empty arena, so ancestor scrollers never join: a list of disabled buttons can't be scrolled from them (`SkUiPointerRouter`) | A disabled node blocks only its own recognizers; still collect ancestors' exclusive ones (scroll, pan) |
| N3 | Medium | A disabled Core button lets taps through to what is underneath (`SkUiCoreButton.HasIntrinsicTap => CanExecuteCommand`); `SkUiButton` blocks | Give Core `IsEnabled` / `InputTransparent` and one blocking rule for both layers |
| N4 | Medium | Core stacks and content views ignore child alignment (only absolute, grid and host call `AlignInSlot`), although Core promises MAUI `ComputeFrame` behavior | Align in every Core container; SkUi-vs-Core parity test |
| N5 | Medium | `InvalidateMeasure` walks to the root without an early-out, and each level calls `InvalidatePaint`: every ancestor re-records its picture (e.g. `SkUiCoreTable` redraws all track backgrounds on a cell text change) | Propagate layout without content dirtiness (size changes already re-record); stop at measure-dirty parents; relayout boundaries (§4) |
| N6 | Medium — **partly fixed** | Swapping `SkUiLook.Current` / `SkUiColorScheme.Current` left retained pictures stale; some controls snapshot scheme colors at construction | Fixed: live surfaces re-measure and redraw their drawn tree on `CurrentChanged` (`SkUiLook.NotifyChanged()` for looks changed in place). Open: resolve scheme defaults at paint time ("not explicitly set") instead of snapshots; hook `RequestedThemeChanged` |
| N7 | Medium | Any running render-thread animation (a 36-DIP spinner, an indeterminate bar) re-composites the whole surface at display rate | Raster-cache stable siblings (§4) |
| N8 | Medium | No OS font scaling (`FontAutoScalingEnabled`), no semantics tree, no keyboard focus / activation | Accessibility work (§4) |
| N9 | Medium — **fixed** | No shared image cache: every instance re-loads and re-decodes the same source and holds several copies of the encoded bytes; duplicated between SkUi\* and Core | Fixed (P4): one loader (`SkUiImageLoader`) and one image slot for both layers; decoded images in a memory LRU keyed by source, decode size and transformations, leased by the views that show them; shared in-flight loads; a download disk cache (`ImageLoadingTests`) |
| N10 | Low–Medium | Android overlays allocate `Rect` Java peers per offset report and look overlays up with LINQ per child; clip computation walks ancestors per ancestor (O(depth²)) | Reuse rectangles, key overlays by clip view, one ancestor walk |
| N11 | Low | Fling stop test ignores direction (a flick inward from an edge stops at once); `maxX` / `maxY` are frozen at fling start | Direction-aware stop; live extent updates (also needed by FR-21) |
| N12 | Low | `NotifyMoved` walks whole subtrees on every offset change even without overlays | Gate on a "subtree has overlays" counter |
| N13 | Low | SkUi vs Core drift: button padding defaults, corner radius resolution, Core label without `FontAttributes`, Core without `IsEnabled` / anchor / `ScaleX`/`ScaleY`, some SkUi setters without equality early-outs | Shared defaults and parity tests |
| N14 | Low | Per-label native objects (`SKPaint`, an `SKFont` per fallback typeface) freed by finalizers | Share fonts by (typeface, size); one recording paint |
| N15 | Low (unverified) | Single-entry line cache: text measured at several widths in one pass re-shapes | Two-entry width cache |
| N16 | Low | Hidden subtrees' descendants still record on change; hit-testing ignores rounded `ChildrenClipPath` | Skip hidden subtrees; honor the clip path in hit-testing |
| N17 | Low (unverified) | One Apple render thread for all surfaces (`NextDrawable` can block the others); no GPU-cache purge on memory warnings / trim-memory | Measure with several surfaces; purge on memory pressure |
| N18 | — | Test gaps: no record / commit budgets, no concurrent commit / render / dispose stress, no SkUi-vs-Core pixel parity matrix; global statics force serialized test collections | Add as the areas are touched |

## 3. Comparison with other Skia-in-MAUI options

Only **DrawnUi** solves the same problem (Uno and Avalonia can't be hosted inside a MAUI page; Open-Maui is a CPU Linux backend; MAUI `GraphicsView` is not Skia).

| | **SkiaUi** | **DrawnUi** |
| --- | --- | --- |
| Node cost | Light Core nodes, plus MAUI-View SkUi\* controls | Every node is a `VisualElement` |
| Layout | MAUI layout managers (drop-in parity) | Own system |
| Rendering | Retained per-node pictures, render-thread compositing and animation; no raster cache yet | Rich, hand-tuned cache types |
| GPU | Metal, GL thread, ANGLE | Metal, GL thread, ANGLE |
| Text | HarfBuzz, bidi, fallback; spans (P5) | HarfBuzz, spans |
| Controls | ~20 per layer | ~70 |
| Virtualization / accessibility | Not yet | Yes / Windows only |
| Quality | ~16k LOC, headless suite, leak tests on devices, AOT-clean | ~132k LOC, few tests |

**Verdict:** the engine now has the speed foundations. The gap to DrawnUi is breadth (lists, containers, effects) plus accessibility, not architecture — provided the structural items below land before the control count grows further.

## 4. Recommendations

**SkUi\*-over-Core (old 2.1).** Don't wrap a Core node in every SkUi\* (double node memory, and the `BindableObject` cost stays). Instead:
- Keep extracting layer-agnostic engines, as done for text, scrolling, the slider and progress drawing: toggles, button chrome, the image loader.
- Factor the duplicated node mechanics (measure cache, invalidation flags, render properties, alignment) into one internal helper used by both node bases.
- Gate it with a parity test matrix.

**Relayout boundaries (2.3).** A node whose size can't change stops upward propagation and re-arranges only its own subtree: explicit width and height, tightly constrained by its parent, or re-measured in place with an unchanged size. Remove the ancestor `InvalidatePaint` first (N5).

**Raster cache (2.4).** Render thread only (needs the GPU context), in stages:
1. An opt-in cache hint on a node.
2. Automatic caching: subtrees stable for 3+ frames while only composite properties animate, at most ~3 new entries per frame, with a byte budget.
3. Invalidation from the compositor's update pass.
4. Dropped on context loss.

The spinner and fling cases (N7) are the benchmark.

**Virtualization readiness (FR-21 / FR-22).**
- **Already fits:** composite-time offsets, culling, per-item render nodes, dirty-path recording.
- **Close first:**
  - realize items from the fling's predicted target offset, since UI-side offsets arrive late;
  - recycle without reparenting, which today resets and re-records the subtree;
  - keep item re-measure local to the list (a relayout boundary) and correct the scroll anchor;
  - live extent updates for the fling (N11);
  - ~~the shared image cache (N9)~~ (shipped with P4).

**Overlay masks (2.8).** The practical need is drawn popups over hosted controls and rounded clipping.
- Compute each overlay's occluding region from higher-z drawn nodes that opt in, plus ancestor clip paths.
- Apply it as a mask: `CAShapeLayer` on iOS, a path clip on Android, a geometric clip on Windows.
- Fall back to the snapshot mode while an occluder overlaps.

**Accessibility.**
- Build a semantics tree during recording (own dirty flag), from MAUI `SemanticProperties` / `AutomationProperties` on SkUi\* and a semantics API on Core.
- Expose it through `ExploreByTouchHelper` (Android), accessibility elements on the surface view (iOS) and an automation peer (Windows). Reuse the router's hit-testing and `SkUiDiagnostics.SimulateTap` for bounds and actions.
- Add OS font scaling, desktop keyboard focus / activation and reduce-motion.

## State-change animations

Requirement FR-26: switch, check box and radio transitions, press feedback (dim, ripple), slider and progress motion, configurable by `SkUiLook`. **Implemented** on the UI-thread design below; the render-thread variants stay open for later.

**Design as built** ([ControlLook.md](ControlLook.md#state-change-transitions-fr-26), [AnimationMechanism.md](AnimationMechanism.md#state-change-transitions)):

1. **Looks draw from continuous parameters.**
   - Paint structs carry the transition: `SkUiToggleVisual` (from / to state, eased progress, press amount, with `Weight` / `Blend` helpers) and `SkUiPressVisual` (press amount, press point, ripple spread and fade). The slider's drawn fraction and press amount, and the progress bar's drawn fill, travel in their paint structs too.
   - `GetTransition(kind)` gives each transition's duration and easing, or `None`.
2. **Per-control animation state lives in the control.** Internal animators are shared by both layers: `SkUiTween`, `SkUiToggleAnimator`, `SkUiPressAnimator`, `SkUiSliderVisual`.
   - The logical state changes at once. Interrupted transitions reverse from their current point.
   - A quick tap shows its full press before releasing.
   - Controls animate only after their first frame, and a stopped clock jumps to the end state.
3. **UI-thread clock, re-recording only the animating control.** Paint invalidation marks just that node's content; ancestors are only walked, so N5 (which is about measure invalidation) was not a prerequisite. `TransitionTests` checks that one picture is recorded per frame.
4. **Press position.** The tap recognizer now reports it. The ripple is drawn with the button's background, clipped to its rounded path; for image buttons, in the overlay.
5. **One timeline.** Every transition runs on the UI clock; nothing mixes it with the render clock.
6. **Reduce motion.** `SkUiMotion` follows the OS setting and can be overridden. While reduced, every transition is `None`.

**Measurements.** Device benchmarks `toggle-transitions` and `toggle-transitions-busy` ([Benchmarks.md](Benchmarks.md)): 96 switches and check boxes re-toggled every 120 ms, so all of them are always mid-transition. Release builds, medians of 6 runs.

| Device | Scenario | UI animation frames / s | UI work per frame (avg / max) | Render per frame (avg) |
| --- | --- | --- | --- | --- |
| Galaxy S9 (2018, 60 Hz) | 96 animating | 58.4 | 4.7 / 40 ms | 4.1 ms |
| Galaxy S9 | + UI thread blocked 25 ms every 100 ms | 51.5 | 4.6 / 32 ms | 4.5 ms |
| Mac Catalyst (Apple M5 Pro) | 96 animating | 59.9 | 0.6 / 2.4 ms | 0.6 ms |
| Mac Catalyst | + busy UI thread | 49.9 | 0.5 / 2.5 ms | 0.6 ms |
| Reference: `spinners` (render thread), Galaxy S9 | 120 spinning | — | — | 2.2 ms at 59.8 fps |

**Decision: keep the UI-thread design.**
- **Cheap:** about 0.05 ms of UI work per animating control per frame on a 2018 phone. A real screen animates one to a few controls at a time.
- **Where it loses frames:** only when the UI thread itself is blocked (−7 to −10 frames per second at 25% blocked). Then taps and bindings stall too, and transitions last 80–450 ms.
- **What it keeps:** look painters stay ordinary single-threaded code that app authors can write without thread-safety rules, and tests drive them with a deterministic clock.
- **Occasional long frames** (up to 40 ms on the S9, with all 96 controls animating) don't show on the Mac. Probably GC or JIT; not investigated.
- **Not measured:** a physical iPhone / iPad. The benchmark app has no provisioning profile for device builds.

**Kept open (version 2, render thread).** Both options leave the look API as it is:
- *Render-thread painters:* the compositor calls the look's painter per frame with an immutable paint-struct snapshot instead of replaying a picture. It needs painters that are safe to run on the render thread, so it would be an opt-in per look.
- *Child render nodes:* moving parts (thumb, check glyph, ripple) animated with composite-time tweens. It fits simple slides, but not blended colors or partially drawn check marks.

Revisit if a real app shows jank from UI-thread work during transitions.

**Not done yet (FR-26):** several ripples at once (a new press restarts the ripple); press scale (the look draws the chrome, but the text is drawn by the control, so scaling needs a composite-time transform).
