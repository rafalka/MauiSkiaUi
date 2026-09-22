# SkiaUi animation mechanism

Design notes and implementation checklist for **FR-7** in [Requirements.md](Requirements.md). Aligns with [DrawingMechanism.md](DrawingMechanism.md) (paint / opacity / transform at paint time) and [LayoutSystem.md](LayoutSystem.md) (selective measure/arrange).

## Goal

Sustain **butter-smooth ~60 fps** motion on GPU-backed roots (`HwAccelerated` → `SKGLView`-class surface) for:

1. **Animated painting** — colors, opacity, strokes, and other paint-only properties.
2. **In-area render transforms** — `TranslationX` / `TranslationY`, `Rotation`, `ScaleX` / `ScaleY` (and anchor) applied at paint time **without** remeasure/rearrange.
3. **Optional layout animation** — animated size / margin / arranged bounds that *do* dirty measure/arrange (later; not required for v1 demos).

The clock must integrate with root invalidation: continuous frames **only while** any animation is active; idle when none are. Non-animated siblings must not be remeasured each frame (NFR-2 / FR-3).

**Code performance (NFR-2):** animator tick and per-frame paint paths are critical — target **zero allocations** and maximum throughput (modern C# / .NET, including `unsafe` where justified). Prefer a **simple correct** first implementation, then optimize after tests prove behavior.

## Decision summary

| Topic | Choice |
| --- | --- |
| Frame source | **Display vsync** via root `HasRenderLoop` (or SW invalidate equivalent) while animators run — **not** a fixed `Timer(1000/60)` |
| “60 fps” meaning | Target **display-smooth** motion; nominal budget ~16.7 ms on 60 Hz. Prefer **time-based** interpolation so 90/120 Hz devices stay correct (and still look smooth) |
| Primary animation class | **Paint + render-transform** (cheap). Layout animation is **opt-in / later** |
| Clock ownership | **Standalone SkUi root** owns animator registry + render-loop on/off |
| Tick timing | Animators tick from **frame timestamp** (`frameTimeNanos` / elapsed ms) before paint |
| Value application | Prefer **FR-10 direct setters** inside tick callbacks; optional bindable write-back for XAML sync |
| Transform model | MAUI-like `Translation*` / `Scale*` / `Rotation` + `AnchorX`/`AnchorY` → `SKCanvas` matrix at paint |
| Layout dirty on transform/paint anim | **Never** — only paint invalidate / transform dirty |
| Layout anim | Explicit API that marks measure/arrange dirty; document cost |
| Closest analogue | **DrawnUi** animator registry + `TickFrame` before draw; ideas from Flutter ticker / Avalonia render vs layout transforms |

## Recommended solution (from references + web)

### What the references actually do

| Stack | Clock | Paint / transform anim | Layout anim | Lesson for SkiaUi |
| --- | --- | --- | --- | --- |
| **SkiaSharp `SKGLView`** | `HasRenderLoop = true` → continuous present; no per-frame `Invalidate` needed | App redraws every frame; use **elapsed time**, not fixed dt | N/A | Drive loop from surface; **interpolate by wall/frame time** ([SkiaSharp #620](https://github.com/mono/SkiaSharp/issues/620), [#805](https://github.com/mono/SkiaSharp/issues/805)) |
| **DrawnUi** | Root tracks `ISkiaAnimator`s; `ExecuteAnimators(frameTimeNanos)` then paint; keeps refreshing until registry empty | `Opacity`, `Translation*`, `Scale*`, `Rotation` via animators + control props; matrix at draw; inverse for hit-test | Possible via property invalidation | **Best product fit**: registry on root, tick-before-paint, auto stop when idle |
| **.NET MAUI** | `IAnimationManager` + `ITicker` (platform vsync); `Animation.Commit` / `ViewExtensions` | `TranslationX/Y`, `Scale`, `Rotation`, `Opacity` on `VisualElement` are **render** properties | Animating `WidthRequest` etc. forces layout | Reuse **Easing** / familiar commit API; keep transform props render-only |
| **Flutter** | `Ticker` / `AnimationController` vsync’d to display | Prefer `Transform` / opacity / transition widgets — no layout | Animating layout is expensive | Prefer transform/opacity over size for motion |
| **Avalonia** | Composition / property animations | **RenderTransform** = fast, no re-layout | **LayoutTransform** = siblings adjust; animating it recalculates layout each frame | Split **render** vs **layout** transforms explicitly ([docs](https://docs.avaloniaui.net/docs/graphics-animation/render-vs-layout-transforms)) |
| **OpenMaui / general mobile** | Choreographer / vsync frame budget | Redraw dirty content each vsync | Layout only when needed | Do not hardcode 16.67 ms timers; sync to refresh |

### Verdict: best mechanism for SkiaUi

Use a **root-owned, vsync-driven animator clock** that:

1. Registers running animators on the **standalone** surface owner (`SkUiContentView` / standalone `SkUiLayout` / standalone `SkUiView`).
2. Sets continuous presents when the active count goes from 0 → 1:
   - **Most platforms (GL):** `HasRenderLoop = true`.
   - **iOS / Mac Catalyst (GL):** a root-owned `CADisplayLink` on **`NSRunLoopCommonModes`** ticks and `InvalidateSurface`s — `SKGLView`'s own `HasRenderLoop` link is default-mode only and stalls while a sibling `UIScrollView` is tracking.
   - **Software:** delayed dispatcher pump (~16 ms) + `InvalidateSurface`.
3. Each frame: **tick all animators with frame time** → apply values → **one full-tree paint** (v1 paint model in DrawingMechanism.md).
4. When the last animator finishes: stop the continuous present path (`HasRenderLoop = false` / invalidate the iOS display link / stop the SW pump).

**Do not** implement a separate fixed-rate 60 Hz timer that calls `InvalidateSurface`. SkiaSharp and platform docs consistently show that timers drift, fight vsync, and under-deliver on touch/load; GL already presents on the display cadence when the render loop is on.

**Do** treat “60 fps” as:

- **Quality bar:** animation work + paint of the active tree fits in one vsync (~16.7 ms @ 60 Hz).
- **Correctness bar:** progress = `elapsed / duration` (easing applied), so a dropped frame advances further in time instead of stuttering in place or speeding up on 120 Hz.

Optional later: **cap** effective tick rate at 60 Hz on 120 Hz devices for battery; default should follow display refresh for smoothness.

### Why not MAUI `Animation` alone?

MAUI’s `Animation` + `IAnimationManager` is a good **API shape** (commit, easing, parent/child animations) and can be **wrapped or reused** for tick math. It does **not** replace:

- Wiring `HasRenderLoop` on the Skia surface.
- Applying values into the SkiaUi tree without forcing MAUI platform arrange on hosted children.
- Keeping layout dirty flags selective during transform/paint motion.

DrawnUi’s pattern (animators owned by the drawn root, tick with `frameTimeNanos`, then draw) maps cleanly onto `SkUiContentView` → `ISkUiView`.

## Architecture

```
Start animator (paint / transform / layout)
        │
        ▼
Register on standalone SkUi root animator registry
        │  activeCount 0→1 → HasRenderLoop = true (GL)
        ▼
Each vsync / present:
        │
        ├─ Tick animators(frameTimeNanos)     ← time-based progress
        │     ├─ paint props → direct setters (paint dirty only)
        │     ├─ transform props → paint dirty only
        │     └─ layout props → measure/arrange dirty (optional path)
        │
        ├─ Selective Measure/Arrange if any layout-dirty
        │     (skip clean non-animated subtrees)
        │
        └─ Clear + full tree Paint (v1)
              per node: Save → Opacity → RenderTransform → Clip
                        → Background → Content (+ children) → Overlay → Restore
        │
        ▼
Animator finished → unregister
        │  activeCount →0 → HasRenderLoop = false
```

### Who owns what?

| Role | Responsibility |
| --- | --- |
| **Standalone root** | Animator registry; render-loop on/off; supplies frame time into paint path; coalesces invalidation |
| **`SkUiAnimation` / animator** | Duration, easing, from/to (or callback), pause/stop; `Tick(frameTime)` → progress → apply |
| **`SkUiView`** | Holds current Opacity / transform / paint props; applies canvas state in paint; exposes animate helpers |
| **Layout managers** | Only run when measure/arrange dirty (layout animations or real layout changes) |
| **Hosted children** | Never own a render loop; register animators upward to the surface-owning root |

## Animation tiers

### Tier 1 — Animated painting (v1 required)

**Properties:** `Opacity`, `BackgroundColor`, brush/stroke colors, layer-specific paints, similar chrome fields.

**Invalidation:** paint only (coalesce to root surface). **No** measure/arrange.

**Implementation notes:**

- Tick writes via **direct setters** (`SetOpacity`, `SetBackgroundColor`, …) inside `StartUpdating`/`EndUpdating` when multiple props change in one frame (FR-10).
- Opacity &lt; 1 uses existing paint path (`SaveLayer` / alpha — DrawingMechanism FR-8).
- Color tweens: interpolate in a stable space (e.g. RGBA components, or MAUI `Color` channels); document gamut limits.

### Tier 2 — In-area render transforms (v1 required)

**Properties (MAUI-aligned):** `TranslationX`, `TranslationY`, `Rotation` (degrees), `ScaleX`, `ScaleY`, `AnchorX`, `AnchorY` (0–1 relative to arranged bounds).

**Semantics (Avalonia “render transform” / MAUI visual transform):**

- Applied **after** arrange, **at paint time**, as an `SKMatrix` about the anchor point inside the node’s arranged frame.
- **Does not** change `DesiredSize`, `Frame`, or siblings’ layout.
- Visual overlap with neighbors is allowed (expected for press scale, slide-in, spin).

**Invalidation:** paint / transform dirty only — **never** measure dirty.

**Hit-testing (proposed):**

| Option | Behavior | Recommendation |
| --- | --- | --- |
| A | Hits use **arranged bounds** only (ignore transform) | Simpler; wrong for large rotations/scales |
| B | Map pointer through **inverse render matrix**, then bounds test | Matches DrawnUi / typical MAUI transforms |

**v1 recommendation: Option B** for nodes with non-identity transform; arranged bounds remain the layout contract (FR-11 clip still paint-only). Document that layout hit region without transform is incorrect once rotated/scaled.

**Paint order (per node):**

```
Save
  Apply Opacity (if needed)
  Concat RenderTransform (anchor → rotate/scale/translate → un-anchor)
  Apply Clip/Mask
  Background → Content (+ children) → Overlay
Restore
```

Children inherit the parent canvas transform naturally (drawn in parent space). Prefer animating the **moving node’s** transform rather than re-arranging children each frame.

### Tier 3 — Animated layout (optional / later)

**Properties:** `WidthRequest`, `HeightRequest`, `Margin`, padding, grid row height, stack spacing, or interpolated `Arrange` bounds.

**Semantics:** Each tick marks measure and/or arrange dirty; layout managers run; siblings may move.

**Cost:** Same class of jank Avalonia documents for layout-transform animation — full subtree layout work **every frame**.

**Guidance:**

- Prefer Tier 2 (e.g. animate `TranslationX` or `ScaleY` for expand/collapse **visuals**) when siblings need not reserve space mid-flight.
- Use Tier 3 when the **layout contract** must stay correct (neighbors must not overlap; accordion that pushes content).
- API should be explicit (e.g. `AnimateLayoutBounds` / animating layout bindables) so authors do not accidentally layout-animate by tweening `HeightRequest` without understanding cost.
- Still honor selective dirty flags: only the dirty subset remeasures.

**Not required for FR-7 demo;** land after paint + transform path is solid.

## Public surface (target)

Exact names TBD; intent:

### Low-level

- `ISkUiAnimator` — `Start` / `Stop` / `Pause` / `Resume`; `bool Tick(long frameTimeNanos)`; finished → unregister.
- Root: `RegisterAnimator` / `UnregisterAnimator`; `int ActiveAnimatorCount`; drives render loop.
- `SkUiAnimation` — duration, easing (`Microsoft.Maui.Easing` or thin wrap), from/to `double` or custom lerp, `Action<double> onValue`, optional finished callback.
- Batch: parent animation with child animations (MAUI `Animation` tree style) optional in v1.1.

### High-level helpers on `SkUiView`

Fluent / Task-based helpers (DrawnUi-style), e.g.:

- `FadeToAsync(opacity, duration, easing)`
- `TranslateToAsync(x, y, …)`
- `ScaleToAsync(sx, sy, …)` / `RotateToAsync(degrees, …)`
- `ColorToAsync` for background / text color

These create a registered animator, apply via direct setters, complete when finished.

### XAML / bindable

- Transform and opacity as **bindable properties** (MAUI parity) with FR-10 direct setters underneath.
- Optional later: style / visual-state driven transitions (Avalonia-like) — not v1.

## Clock and “fixed 60 fps” policy

| Approach | Use? | Why |
| --- | --- | --- |
| `Timer` / `DispatcherTimer` at 16.67 ms → `InvalidateSurface` | **No** (primary) | Drifts vs vsync; SkiaSharp issues show unstable FPS; doubles work with GL loop |
| `HasRenderLoop = true` while animating | **Yes** | Surface presents on display cadence; natural 60/90/120 Hz |
| Time-based `progress = elapsed/duration` | **Yes** | Correct under jank and high refresh |
| Frame-count `progress += 1/60` | **No** | Speeds up on 120 Hz; freezes wrong on drops |
| Hard cap at 60 Hz presents | Optional later | Battery vs smoothness tradeoff |

**Decided for SkiaUi:** vsync-driven loop + time-based animation math. Marketing “60 fps” = **smooth on 60 Hz class devices** with a ~16.7 ms frame budget — not a software metronome.

### Open decision (Requirements) — resolved here

> Should a HW-accelerated root set `HasRenderLoop = true` whenever any descendant has an active animation? When the last animation ends, turn it off?

**Yes.** Active animator count on the surface-owning root toggles the loop. Paint may full-walk each frame in v1; measure/arrange only if layout-dirty. Sibling `Picture`/`Image` cache only if profiling shows static regions dominating (DrawingMechanism).

## Invalidation rules

| Animated change | Measure | Arrange | Paint | Render loop |
| --- | --- | --- | --- | --- |
| Color / brush / chrome paint | — | — | Dirty | On while running |
| Opacity | — | — | Dirty | On |
| Translation / Rotation / Scale / Anchor | — | — | Dirty (transform) | On |
| Width/Height/Margin (layout anim) | Dirty | Dirty | Dirty | On |
| Animator starts (0→1) | — | — | — | **Enable** |
| Last animator ends | — | — | Final frame optional | **Disable** |

`StartUpdating` / `EndUpdating` should wrap multi-property ticks so one frame does not thrash invalidate flags.

## Interaction with other mechanisms

| Area | Interaction |
| --- | --- |
| **Drawing (FR-8/9)** | Opacity + transform wrap layer paint; animating node should not retain `Picture`/`Image` cache while running (DrawingMechanism) |
| **Layout (FR-3a)** | Transform/paint anim must not clear layout caches; layout anim uses existing dirty flags |
| **Gestures (FR-15)** | Hit-test uses inverse render transform when non-identity (proposed); long-press timers independent of anim clock |
| **Theming (FR-12)** | Visual states may *start* animations; animation writes go through setters |
| **SW roots** | No `HasRenderLoop`; use continuous invalidate or platform ticker while `ActiveAnimatorCount > 0` |

## Reduced motion / accessibility

Honor platform “reduce motion” when available (MAUI / OS setting): shorten or jump to end for non-essential motion. Document a library-level switch (e.g. `SkUiAnimation.SystemEnabled`) mirroring MAUI ticker `SystemEnabled`.

## Out of scope (v1)

- Full Avalonia-style composition-thread animations (no separate render thread compositor).
- Lottie / Skottie as the core property system (optional control later via SkiaSharp Skottie).
- Physics/spring graph as default (nice DrawnUi extras; add after basics).
- Animating MAUI parents outside the SkiaUi island.
- Default layout animation helpers.

## Implementation checklist

### Clock and registry

- [ ] Animator registry on standalone root (thread-affinity: UI thread).
- [ ] `Register` / `Unregister`; `ActiveAnimatorCount`; toggle `HasRenderLoop` (GL) or continuous invalidate (SW).
- [ ] Pass `frameTimeNanos` (or high-res elapsed) into tick before paint.
- [ ] Auto-disable loop when count hits 0; dispose/unregister on node detach.

### Tier 1 — Paint

- [ ] Animate `Opacity` and at least one color property via helpers + direct setters.
- [ ] Paint-only invalidation; no measure dirty.
- [ ] Demo: pulsing opacity / color under `SkUiContentView`.

### Tier 2 — Render transforms

- [ ] Bindable + direct setters: `TranslationX/Y`, `Rotation`, `ScaleX/Y`, `AnchorX/Y`.
- [ ] Build `SKMatrix` in paint; identity fast path.
- [ ] Inverse matrix in hit-test when transform non-identity.
- [ ] Transform changes never set measure dirty.
- [ ] Demo: translate / rotate / scale animation without layout thrash.

### Tier 3 — Layout (optional)

- [ ] Explicit layout-animation path that dirties measure/arrange.
- [ ] Docs warn about cost; gallery sample optional.
- [ ] Selective layout still skips unrelated clean subtrees.

### API polish

- [ ] XML docs on public animation types and transform semantics (render vs layout).
- [ ] Easing reuse (`Microsoft.Maui.Easing`) or documented equivalent.
- [ ] Reduced-motion / `SystemEnabled` behavior documented.
- [ ] Cross-link FR-7 in Requirements as designed here; mark demo item when shipped.

## Tracking

When implemented, check items above and summarize in [Development.md](../../Development.md) under *Current implementation*. Keep FR-7 checkboxes in [Requirements.md](Requirements.md) in sync.
