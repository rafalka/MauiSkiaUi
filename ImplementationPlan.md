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
- [ ] Automated tests cover paint, layout, events, and animation smoke paths.
- [ ] Decision: core pipeline is viable → proceed to Phase 1 (or adjust architecture if not).

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
- [ ] Functional demo + ~1000-control performance page exist; major bottlenecks addressed after measurement.
- [ ] Tests green for Phase 1 surface; README *Current implementation* updated.

### Maps to requirements (partial)

FR-1–4, FR-3a, FR-5 (partial gallery), FR-7, FR-9–12, FR-14–15, FR-17 (ScrollView), NFR-1–4.

---

## Phase 2 — Actual implementation

**Goal:** broaden MAUI replacements, grow the control gallery, and ship documentation.

### Deliverables

| Area | Scope |
| --- | --- |
| Controls | **`SkUiSwitch`**, **`SkUiCheckBox`**, **`SkUiRadioButton`** |
| Layouts | **Stack** layouts (vertical/horizontal as applicable), **`SkUiAbsoluteLayout`**, **`SkUiFlexLayout`** |
| Gallery | Extend **control gallery** in the demo (layouts + controls from Phase 1–2) |
| Docs | **Documentation** per NFR-5: per-control `.md` files; MAUI reimplementations link to official MAUI docs and document **differences / extensions** only; XML + non-obvious comments kept current |
| Packaging | Progress toward FR-6 NuGet readiness for `MauiSkiaUi` as the surface stabilizes |

### Exit criteria

- [ ] Phase 2 controls/layouts in library + gallery samples.
- [ ] Per-control docs present for shipped public types (index from README).
- [ ] Platform verification on Android and at least one Apple target (FR-5).

### Maps to requirements (partial)

FR-4–6, FR-5 gallery, FR-12, NFR-5, remaining layout FR-3 / FR-3a coverage.

---

## Phase 3 — Extensions

**Goal:** SkiaUi-specific extensions beyond straight MAUI parity.

### Deliverables (current backlog)

| Extension | Notes |
| --- | --- |
| **`SkUiLabel` with rounded-rectangle border** | Documented extension vs MAUI Label (NFR-5: differences section); likely shared round-rect / background helper (FR-9 / NFR-4) |

Further extensions are added here as they are decided.

### Exit criteria

- [ ] Rounded-border Label works in gallery + has control doc section for the extension.
- [ ] Shared drawing helper reused where other controls will need the same border later.

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
| 0 — Proof of concept | **Not started** |
| 1 — Initial implementation | Not started |
| 2 — Actual implementation | Not started |
| 3 — Extensions | Not started (backlog: Label + round-rect border) |
