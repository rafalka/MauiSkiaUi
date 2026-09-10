# SkiaUi implementation plan

Phased delivery plan for [Requirements.md](Requirements.md). Design details live in the mechanism docs linked from [README.md](README.md).

**Workflow reminder (NFR-2):** within each phase, prefer **simple, correct** code first; expand tests; then **optimize** hot paths for speed and near-zero allocations.

---

## Phase 0 — Proof of concept

**Goal:** prove the core host → measure / arrange → paint → events → animation loop works end-to-end with a minimal surface area.

### Deliverables

| Area | Scope |
| --- | --- |
| Core | `ISkUiView : IView`, `SkUiView`, **`SkUiContentView`** (Content host), custom handler + `HwAccelerated` (FR-1 / FR-13 / FR-14) — enough to stand alone and draw |
| Drawing primitives | Basic controls for **boxes**, **ellipses**, and **lines** (Skia-drawn; not full MAUI parity) |
| Layout | Minimal measure/arrange (MAUI-based) so primitives can size/position under `SkUiContentView` — full `SkUiLayout` can stay thin or stubbed if needed for the PoC |
| Events | Basic pointer/touch path into the tree + enough of FR-15 to demonstrate hit / tap (or raw Touch) on a primitive |
| Animation | Minimal FR-7 clock: at least one property or transform animates smoothly on the PoC page |
| Tests | Tests for **basic painting**, **layout**, **event handling**, and **animations** (see [Testing.md](Testing.md)) |
| Demo | **Simple page** in `MauiSkiaUiDemo` showing boxes / ellipses / lines (and a short animation) under `SkUiContentView` |

### Explicitly out of Phase 0

- Full MAUI control set, `SkUiScrollView`, `SkUiMauiContentView`, NuGet polish, per-control documentation pack
- Max-performance / `unsafe` optimizations unless needed to unblock the PoC

### Exit criteria

- [ ] Demo page runs on at least one primary target (Android or Apple).
- [x] Automated tests cover paint, layout, events, and animation smoke paths.
- [ ] Decision: core pipeline is viable → proceed to Phase 1 (or adjust architecture if not).

**Implementation status (2026-09-10):** core contract, custom GPU/software handler, content host, minimal overlay layout, box/ellipse/line primitives, tap capture, frame-driven animation, and XAML demo are implemented. The 19 headless mechanism tests pass; Android/iOS/Mac Catalyst diagnostic builds pass. A manual Mac launch was reported, but automated device verification is blocked by the installed MAUI extension's `MauiDevFlow.targets` (`MSB4099` during package injection). Keep the device and viability gates open until the rendered surface, native taps, animation/idle transitions, and contrast are verified. See README for the exact Phase 0 limits.

### Maps to requirements (partial)

FR-1, FR-7 (minimal), FR-13/14 (minimal), NFR-1/2/3 (smoke), paint path in [DrawingMechanism.md](DrawingMechanism.md), layout seed in [LayoutSystem.md](LayoutSystem.md).

---

## Phase 1 — Initial implementation

**Goal:** first usable MAUI-replacement slice: composition host, layout base, scroll, and core drawn controls — with functional and stress demos, then optimization.

### Deliverables

| Area | Scope |
| --- | --- |
| Host / layout | Harden **`SkUiContentView`**, implement **`SkUiLayout`**, **`SkUiScrollView`** (FR-17 v1) |
| MAUI replacements | **`SkUiLabel`**, **`SkUiGrid`**, **`SkUiImage`**, **`SkUiButton`** (BindableProperty + direct setters per FR-10; styles FR-12 as needed) |
| Shared chrome | Reusable background / layer hooks as needed for Label/Button (FR-9 / NFR-4) — keep simple |
| Gestures | FR-15 enough for Button (intrinsic tap) and Label (passive / opt-in) |
| Tests | Broader unit / mechanism / golden coverage for the new controls and scroll |
| Demo — functional | Simple test page(s) exercising Label / Grid / Image / Button / ScrollView |
| Demo — performance | Page with **~1000 hosted controls** to stress measure/paint and guide optimization |
| Optimize | After tests pass: apply NFR-2 (allocations, hot paint/layout/anim paths) |

### Explicitly out of Phase 1 (unless needed for demos)

- Full gallery for every MAUI control, `SkUiMauiContentView` (FR-16) unless required for a specific demo, Flex/Absolute/Stack parity pack, full NFR-5 control doc set

### Exit criteria

- [ ] Phase 1 controls usable from XAML under `SkUiContentView` / `SkUiLayout`.
- [x] Functional demo + ~1000-control performance page exist; major bottlenecks addressed after measurement.
- [x] Tests green for Phase 1 surface; README *Current implementation* updated.

**Implementation status (2026-09-10):** Grid (MAUI manager), Label, Button, Image, padded hosts/layouts, and ScrollView are implemented with bindable/direct setters, styles/visual states, taps, clamped pan/fling, wheel input, and async scroll APIs. The functional XAML page and 1,000-button stress page compile on Android/iOS/Mac Catalyst. A same-day review found one High-severity issue (SkUiImage's decode completion could mutate state off the UI thread) and three Medium issues (Button double-executing `Command`/`TappedCommand`; Button text not clipped to its rounded background; Grid attached-property invalidation matching literal strings), all fixed with regression tests. All 62 headless cases pass. Measured 1,000-label warm recording improved from 8.106 ms / 568,384 B per frame to 0.635 ms / 9,488 B after conservative paint culling and cached Z-order. Native device acceptance remains open: Copilot launch still fails in the installed DevFlow targets (`MSB4099`), with no agent available for tree/screenshot/input/contrast verification. XAML compilation is verified, runtime usability is not. See README for text/image limitations and deferred nested-scroll, native accessibility, virtualization, and overlays.

### Maps to requirements (partial)

FR-1–4, FR-3a, FR-5 (partial gallery), FR-7, FR-9–12, FR-14–15, FR-17 (ScrollView), NFR-1–4.

---

## Phase 2 — Actual implementation

**Goal:** native MAUI hosting (FR-16), broaden drawn controls/layouts, grow the gallery, and ship documentation.

### Priority 1 (do first)

| Deliverable | Scope |
| --- | --- |
| **`SkUiMauiContentView`** | FR-16: `ISkUiView` placeholder hosting a MAUI `VisualElement` as a native overlay on the standalone root |
| **Demo / test page** | Page that hosts at least **`Editor`** and **`WebView`** inside `SkUiMauiContentView` (under `SkUiContentView` / layout) |

### Deliverables

| Area | Scope |
| --- | --- |
| Hosting | **`SkUiMauiContentView`** + Editor/WebView test page (Priority 1 above) |
| Controls | **`SkUiBorder`**, **`SkUiActivityIndicator`**, **`SkUiImageButton`**, **`SkUiSwitch`**, **`SkUiCheckBox`**, **`SkUiRadioButton`** |
| Layouts | **Stack** layouts (vertical/horizontal), **`SkUiAbsoluteLayout`**, **`SkUiFlexLayout`** |
| Graphics | **`SkUiBox`** already exists — optional MAUI `BoxView` parity polish only |
| Gallery | Extend **control gallery** (layouts + controls from Phase 1–2 + MauiContentView samples) |
| Docs | **Documentation** per NFR-5: per-control `.md`; MAUI reimplementations link to official docs + document differences/extensions |
| Packaging | Progress toward FR-6 NuGet readiness for `MauiSkiaUi` |

**Do not Skia-reimplement:** `Entry` / `Editor` / `WebView` / `MediaElement` — host via `SkUiMauiContentView`.

### Exit criteria

- [x] `SkUiMauiContentView` hosts **Editor** and **WebView** on a demo/test page.
- [x] Phase 2 controls/layouts (`Border`, `ActivityIndicator`, `ImageButton`, Switch/CheckBox/RadioButton, vertical/horizontal stacks, Absolute) in library + gallery samples. **`SkUiFlexLayout` is deferred** (not implemented this pass — scope/time; add alongside the Phase 3 backlog when needed).
- [x] Per-control docs for shipped public types (index from README → [docs/controls/](docs/controls/README.md)).
- [ ] Platform verification on Android and at least one Apple target (FR-5).

**Implementation status (2026-09-10):** `SkUiMauiContentView` (FR-16) hosts a real MAUI `VisualElement` as a native overlay: a small native container (`SkUiOverlayContainer`, one implementation per platform) wraps the Skia surface and any attached overlays, positioned via each platform's own absolute-layout primitives (Android `View.Layout`, iOS/Mac Catalyst `UIView.Frame`, Windows `Canvas`). The root handler notifies `SkUiMauiContentView` descendants on connect/disconnect via a new `SkiaChildren` tree walk; `ComputeRootRelativeFrame()` sums this node's and every ancestor's `Frame` offset plus `TranslationX`/`TranslationY` up to the standalone root. **v1 limits:** rotation/scale/opacity are not composed through ancestors, there is no snapshot-during-scroll, and native input bypasses SkiaUi's touch router entirely (`Touch` always returns `false`). `SkUiBorder`, `SkUiActivityIndicator`, `SkUiImageButton`, `SkUiSwitch`, `SkUiCheckBox`, `SkUiRadioButton` (select-only tap, matching MAUI's RadioButton; no automatic group exclusion — documented), `SkUiVerticalStackLayout`, `SkUiHorizontalStackLayout`, and `SkUiAbsoluteLayout` are implemented with bindable/direct setters and MAUI layout-manager reuse where applicable, each with a gallery demo page (native side-by-side where a MAUI counterpart exists). A same-day review found and fixed a High-severity issue (`SkUiRadioButton` unchecked itself on a second tap, unlike MAUI) and two Medium issues (`SkUiBorder` ignored `BackgroundColor`; `SkUiActivityIndicator` could keep animating after being removed from its tree), all with regression tests. All 70 headless tests pass; Android/iOS/Mac Catalyst diagnostic builds pass with 0 warnings. Native overlay attachment/positioning and the rest of native rendering remain **unverified on-device** — Copilot-triggered launch still fails in the installed extension's DevFlow injection target (`MSB4099`); this is a pre-existing tooling issue unrelated to Phase 2 code. Per-control markdown docs (NFR-5) are under `docs/controls/` (indexed from README). NuGet packaging (FR-6) is not started.

### Maps to requirements (partial)

FR-4–6, FR-5 gallery, FR-12, FR-16, NFR-5, remaining layout FR-3 / FR-3a coverage.

---

## Phase 3 — Extensions

**Goal:** SkiaUi-specific and MCT-like extensions beyond the Phase 2 MAUI core set.

### Deliverables (current backlog)

| Extension | Notes |
| --- | --- |
| **`SkUiLabel` with rounded-rectangle border** | Prefer composing **`SkUiBorder`** + Label (or Border content); document vs MAUI Label (NFR-5) |
| **`SkUiExpander`** | MCT-like header + collapsible content |
| **`SkUiStateContainer`** | MCT-like loading / empty / error / success switching |
| **`SkUiSwipeView`** | List-row swipe actions; needs FR-15 capture |

Further extensions are added here as they are decided.

### Exit criteria

- [ ] Rounded-border Label (or Border+Label pattern) in gallery + control docs.
- [ ] Expander, StateContainer, and SwipeView implemented with demos/docs.
- [ ] Shared drawing helpers reused where chrome overlaps (Border / round-rect).

---

## Cross-cutting (all phases)

| Topic | Expectation |
| --- | --- |
| Correctness → optimize | NFR-2 workflow on every new hot path |
| Extensibility | Virtual hooks / interfaces / shared helpers (NFR-4) |
| Testing | Follow [Testing.md](Testing.md); grow coverage with each phase |
| Tracking | Check off items in [Requirements.md](Requirements.md); summarize shipped behavior in [README.md](README.md) |

## Suggested order within a phase

1. Types + handler / host wiring  
2. Measure / arrange  
3. Paint  
4. Events / gestures  
5. Animation (when in scope)  
6. Demo page  
7. Tests  
8. Optimize (Phase 1+) / docs (Phase 2+)

---

## Status

| Phase | Status |
| --- | --- |
| 0 — Proof of concept | **Implemented; device verification blocked by MAUI extension** |
| 1 — Initial implementation | **Implemented and headless-tested; device acceptance blocked by MAUI extension** |
| 2 — Actual implementation | **FR-16 hosting + Phase 2 controls/layouts implemented and headless-tested (FlexLayout deferred); device acceptance blocked** |
| 3 — Extensions | Not started (backlog: Label + round-rect border) |
