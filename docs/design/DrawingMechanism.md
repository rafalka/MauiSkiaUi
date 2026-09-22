# SkiaUi drawing mechanism

Design notes and implementation checklist for **FR-9** (layers), **FR-8** (transparency), **FR-11** (clip/mask), surface ownership (**FR-14** / **FR-13**), and selective paint / caching (**NFR-2**) in [Requirements.md](Requirements.md).

## Goal

Provide one **shared paint pipeline** for every `SkUi*` node: a standalone root owns the Skia surface; hosted descendants paint into that surface’s `SKCanvas` without their own handlers.

Apps and built-in controls should be able to:

- Split a control’s chrome into ordered **Background / Content / Overlay** paint phases (Option A — FR-9), not nested chrome `ISkUiView`s.
- Clip / mask painting without changing default hit-testing (FR-11).
- Reuse **cached bitmaps** (or equivalent retained paint) for unchanged nodes and layers, with **transparency-aware** invalidation (FR-8 / NFR-2).
- Share drawing helpers (rounded rect, border, fill) across controls instead of copy-pasted Skia paths.

## Decision summary

| Topic | Choice |
| --- | --- |
| Chrome model | **Option A** — named layer slots / paint phases on the same `ISkUiView` |
| Not chosen | Option B — nest separate `ISkUiView` hosts for background/content/overlay chrome |
| Why Option A | Layers need the owning control’s data (e.g. grid lines use that grid’s row/column metrics) |
| Surface ownership | Only the **standalone** root (handler + `HwAccelerated`) owns GL/SW platform view |
| Hosted paint | Parent walks tree → `ISkUiView.Paint` on shared `SKCanvas` |
| Coordinates | Paint args use **MAUI DIPs**; DIP ↔ pixel mapping only at the root surface |
| Clip vs hit-test | Clip/mask affect **paint**; default hit-test uses **arranged bounds** (FR-11) |
| Caching (v1) | **Default: live paint** on each present. **iOS HW:** compose into a CPU back-buffer then **Src-blit** the full frame onto `SKGLView` (direct tree paint left shrink ghosts). |
| Caching (later) | Opt-in per node (`None` / `Picture` / `Image`); **not** per-layer bitmaps by default; no opaque-coverage dirty-rect compositor in v1 |

## Recommended solution (from references)

Cross-check against DrawnUi, Flutter, Avalonia, and SkiaSharp hosting. Closest product analogue is **DrawnUi** (Skia tree inside MAUI). Flutter/Avalonia inform *ideas*; do not import their compositor trees.

### What the references actually do

| Stack | Paint model | Bitmap / retained cache | Default |
| --- | --- | --- | --- |
| **DrawnUi** | Immediate recursive `Render` on one surface | Rich `SkiaCacheType` (Operations/`SKPicture`, Image, GPU, double-buffer, composite) | **`UseCache = None`** — opt-in per control; many GPU/memory pitfalls documented |
| **Flutter** | Selective *record* at `RepaintBoundary`; full *composite* each frame | Retained `Picture` / `OffsetLayer`; engine raster cache separate | Boundaries are **explicit**; caching when parent+child always dirty together can *hurt* |
| **Avalonia** | Record draw-list on UI thread; compositor replays; dirty rects when surface retains | `Visual.CacheMode` → optional `BitmapCache` | **Opt-in**; idle = no frame; not “cache every control” |
| **SkiaSharp `SKGLView`** | Present typically **clears**; correct apps redraw | App must hold `SKPicture` / `SKImage` if it wants retention | Partial “keep previous GL pixels” is unreliable |

### Verdict on our harder requirements

| Requirement | Feasible? | Recommendation |
| --- | --- | --- |
| Option A layers (FR-9) | **Easy** | Keep. Virtual paint phases only — no extra views, no extra bitmaps. |
| Clip paint / bounds hit-test (FR-11) | **Easy** | Keep. Matches DrawnUi/MAUI/iOS/Android norms. |
| Node opacity (FR-8) | **Easy** | Keep. `SaveLayer` / alpha when α&lt;1; α=0/1 fast paths (Flutter pattern). |
| Full-tree paint on invalidate | **Easy / correct** | **v1 default.** Matches DrawnUi default and SkiaSharp GL reality. |
| Selective **measure/arrange** (FR-3 / NFR-2 layout half) | **Necessary** | Keep. Independent of paint cache. |
| Per-node **bitmap** cache by default (NFR-2 paint half as written) | **Hard / costly** | **Soften.** Memory = width×height×4×density² per cached node; lists and high-DPI explode. DrawnUi defaults off for this reason. |
| Per-**layer** bitmap caches | **Harder** | **Defer.** 2–3× memory; rare win vs whole-node `SKPicture` or single Image. |
| Transparency-aware dirty regions + opaque blit shortcuts | **Hard** | **Defer.** Needed only for partial-surface or composite caches. With full-tree redraw, z-order + alpha is enough. |
| GPU offscreen cache sharing `GRContext` | **Fragile** | **Defer.** DrawnUi: never reuse GPU surfaces; context dies on resume; opacity issues. Prefer CPU `SKPicture` / `SKImage` first if caching. |
| `ImageComposite` / erase-dirty-child | **Niche** | **Defer.** DrawnUi: useful for large containers; “children that love to invalidate parent” kills it. |
| Partial `InvalidateSurface` rects on GL | **Unreliable** | **v1: always full present** from a full tree paint (or blit of caches into a full present). |

**Bottom line:** yes — requiring default retained bitmap caching (and especially per-layer + opaque-coverage compositor) is **harder than needed** and can **waste more memory than it saves CPU**. Prefer **redraw the tree** until profiling proves a hotspot; then add **opt-in** cache at asymmetric boundaries (Flutter’s usefulness rule).

### v1 paint architecture (Decided)

```
Invalidate (coalesced via StartUpdating/EndUpdating)
        │
        ▼
Root handler: InvalidateSurface
        │
        ▼
OnPaintSurface: Clear + scale DIPs↔px
        │
        ▼
Walk full ISkUiView tree in z-order
  per node: Save → opacity/clip → Background → Content (+ children) → Overlay → Restore
```

No retained bitmaps required for correctness.

**Still implement dirty flags**, but for:

1. **Measure / arrange** selectivity (mandatory).
2. **Coalescing** paint invalidation to the root (one surface invalidate).
3. **Future** cache nodes (dirty means “rebuild picture/image”; clean means “blit/replay only”).

Do **not** invent a dirty-rect compositor in v1.

### Layer API (v1 — keep simple)

```csharp
// SkUiView / SkUiCoreNode paint phases
Action<SKCanvas>? PaintBackground;   // chrome; SkUiView defaults to solid MAUI Background when unset
protected virtual void OnPaintContent(SKCanvas canvas) { }  // structure only (virtual)
Action<SKCanvas>? PaintOverlay;      // chrome after content

// SkUiLayout / SkUiContentView: OnPaintContent paints Children / Content
```

- Children paint as a **sub-phase after Content chrome** (Avalonia-like: parent chrome under children; overlay after).
- Shared helpers (`DrawRoundedRect`, border, label text) — not nested chrome views.
- No per-layer cache objects in v1.

### Opt-in cache (v1.1+ when needed)

Mirror DrawnUi’s lesson, not its full enum:

| Mode | Storage | Use when |
| --- | --- | --- |
| **`None`** (default) | — | Almost everything |
| **`Picture`** | `SKPicture` | Stable vector-ish chrome (label, icon, shape); low memory; replay cost |
| **`Image`** | `SKImage` / CPU surface | Expensive static subtree; asymmetric repaint vs siblings |

Rules borrowed from references:

- Cache only when **parent and child do not usually dirty together** (Flutter RepaintBoundary usefulness).
- Size / density change → drop cache.
- Animating node → `None` or don’t cache that node; let static **siblings** use `Picture`/`Image` if profiling needs it.
- No GPU cache until CPU picture/image is proven insufficient.
- No default cache inside scrolling cells unless cell template opts in (or a list-level plane cache later, DrawnUi `SkiaCachedStack` style).

### Animation (FR-7) interaction

- While any animation is active: root may use continuous render loop; **each frame full-tree paint** is acceptable for modest trees.
- Avoid remeasure of non-animated subtrees (layout dirty flags).
- Introduce sibling `Picture`/`Image` cache only if frame time shows static regions dominating CPU.

### What to change in Requirements / NFR-2

**Done (agreed):** NFR-2 / FR-8/9 and Requirements *Decided* now state: selective measure/arrange required; v1 full-tree paint; opt-in retained paint later; no opaque-cover dirty compositor in v1.

### Implementation priority

1. Handler + root clear + full paint walk + DIP mapping  
2. Layer virtuals + clip + opacity + shared draw helpers  
3. Invalidation coalesce to root  
4. Selective measure/arrange (LayoutSystem)  
5. Demos (label, rounded clip, translucent overlap)  
6. **Only if needed:** `UseCache` = Picture / Image on hot controls  

## Architecture

## Architecture

```
Standalone SkUi root (handler → SKGLView or SKCanvasView)
        │  OnPaintSurface / equivalent
        │  clear surface; map DIPs ↔ pixels
        ▼
Root ISkUiView.Paint(canvas, …)
        │
        ├─ apply opacity / transform / clip (if any)
        ├─ Background layer  (paint or blit cache)
        ├─ Content layer     (own chrome + forward to Content / Children)
        └─ Overlay layer
                │
                ▼
Hosted children: Paint on same canvas at arranged Frame (DIPs)
```

Raw surface clear + full present happens at the **platform view**. Selective work is achieved by **not re-entering** clean children / layers and by **blitting** retained caches — not by assuming the GL view preserves previous pixels (SkiaSharp GL surfaces are effectively fully redrawn each present; see open items).

### Who owns the paint pass?

| Role | Responsibility |
| --- | --- |
| **Custom handler** | Standalone only: creates GL or SW Skia platform view from `HwAccelerated`; wires size / invalidate / paint callback; density mapping. |
| **Standalone root** | Entry `Paint`: clear (or compose from caches), apply root transform, paint self + walk hosted tree. |
| **`SkUiView` paint core** | Applies opacity, clip/mask, layer order; optional per-layer cache; virtual hooks for control chrome. |
| **`SkUiContentView`** | After own layers as needed, paints `Content` into content bounds. |
| **`SkUiLayout`** | Paints children in **z-order** within arranged frames; skips clean children when cache allows. |
| **Leaf controls** | Implement layer hooks (e.g. fill Background, glyphs on Content, badge on Overlay). |

Hosted children must **never** create a surface or handler even if `HwAccelerated` is true (FR-13 / FR-14).

## Hosted vs standalone (paint)

### Standalone

1. MAUI handler receives paint from the platform Skia view.
2. Root converts canvas / dirty region into SkiaUi paint args (DIPs).
3. Root calls its own `Paint`, which draws layers and descends into hosted `Content` / `Children`.
4. Invalidation from anywhere in the hosted tree propagates to this root’s `InvalidateSurface` (or equivalent), coalesced with `StartUpdating` / `EndUpdating` (FR-10).

### Hosted

1. No handler; no platform view.
2. Parent’s paint walk calls `child.Paint(...)` with the shared canvas and the child’s arranged bounds (and optional dirty rect).
3. Child paints layers relative to its `Frame`; does not clear the full surface.
4. Offset-only arrange changes may update transform / blit position without rebuilding layer caches when size and visual content are unchanged (FR-3a).

## Drawing layers (FR-9 — Option A)

### Model

Each `ISkUiView` may paint through ordered **slots** on the **same** instance:

| Layer | Typical use |
| --- | --- |
| **Background** | Fill, border, grid lines, chrome behind content |
| **Content** | Primary visuals (text, glyphs, icons) and/or painting hosted children |
| **Overlay** | Badges, focus ring, press highlight, drag affordances |

- Layers are **paint (and optional cache) phases**, not child nodes in the layout tree.
- Nested `ISkUiView` hosting remains only for **`Content` / `Children`** (true content), not for a control’s own chrome.
- Extensibility: allow additional named slots later if needed; v1 ships Background / Content / Overlay.

### Control data access

Layer paint code runs in the context of the owning control so it can read layout metrics and state directly (e.g. `SkUiGrid` Background draws lines from current row/column arrangements). Do **not** require a nested “background view” to discover those metrics.

### Suggested paint order (per node)

1. Save canvas state; translate to arranged origin (DIPs → canvas space as defined by root).
2. Apply **node opacity** (FR-8) and **clip/mask** (FR-11) — same clip for all layers unless a layer opts out (documented).
3. Paint **Background** (or blit Background cache).
4. Paint **Content** chrome; for layouts / content hosts, paint **children** here (or as an explicit sub-phase documented on `SkUiLayout` / `SkUiContentView`).
5. Paint **Overlay**.
6. Restore canvas state.

Exact API: **Background / Overlay** use optional `PaintBackground` / `PaintOverlay` delegates (`Action<SKCanvas>?`); **Content** stays virtual `OnPaintContent` only (layouts and leaf content). When `PaintBackground` is unset on `SkUiView`, protected `PaintDefaultBackground` paints solid MAUI `Background`/`BackgroundColor`. Control chrome painters registered as delegates are `protected` so subclasses can call or re-register them. Same split on `SkUiCoreNode` (no default background).

### Reusable drawing helpers

Favor shared primitives used by many Background/Content layers (`Helpers/SkUiChrome.cs` today; **FR-18** / [ControlLook.md](ControlLook.md) promotes these into a public replaceable **control look**):

- Rounded rectangle fill / stroke / clip path (`DrawRoundedBox`, `CreateRoundRectPath`) — uniform radius (Button, ImageButton tint) or per-corner `CornerRadius` (Border)
- Switch / CheckBox / RadioButton / ActivityIndicator / Image destination — one painter each for Core + MAUI-compatible controls
- Pressed/disabled tint overlay (`DrawPressTint`)
- Text run helpers for labels (still per-control; Core is single-line)

Reuse is via **utilities or shared look implementations**, not by making every chrome piece an `ISkUiView`. Apps customize shapes by swapping or subclassing the look (not by forking each control’s `OnPaintContent`).

**Do not confuse** control look with:

- **Color scheme** ([ColorScheme.md](ColorScheme.md) / FR-19) — default palette
- **MAUI `Style` / VSM** ([FR-12](Requirements.md#fr-12--styles-and-visualstates-maui-per-control-property-appearance)) — per-control property overrides

### Layers vs hit-testing

Default: hit-testing is **not** layer-shaped (FR-11). Overlays that must steal input do so via normal participation / hit-test rules on the **view** ([EventMechanism.md](EventMechanism.md)), not by hit-testing the Overlay bitmap. Optional future: layer-level “input intercept” flags if a chrome overlay must capture without a separate child view — **out of scope for v1** unless a built-in control proves it necessary.

## Clipping and masking (FR-11)

| Concern | Rule |
| --- | --- |
| Paint | Content outside clip/mask must not be drawn (corners stay transparent / show underneath). |
| Hit-test | Uses **arranged layout rectangle** by default. |
| Layers | One clip applies across Background / Content / Overlay unless a layer explicitly opts out. |
| Invalidation | Clip changes dirty paint (and caches) for affected layers; transparency-aware redraw applies (FR-8). |

Shape-aware hit-testing (path contains-point) is **opt-in / future**, not v1 default.

## Transparency and compositing (FR-8)

- Per-node opacity / alpha and translucent content are supported.
- Z-order: layouts paint children in declared / z-order; transparent overlaps must composite correctly.
- **Cache rule:** do **not** treat a cached bitmap as fully covering a region if the node (or its cache) is transparent or has alpha &lt; 1. Expand dirty regions or recompose overlapping siblings / ancestors as needed.
- Opaque blit shortcuts apply only when coverage is known (fully opaque node + opaque cache matching the invalidated area).

## Selective paint and caching (NFR-2)

**Layout half (required):** per-node dirty flags for size / arrangement; parents remeasure / rearrange only dirty subsets; unchanged children keep cached `DesiredSize` / frames (see [LayoutSystem.md](LayoutSystem.md)).

**Paint half (phased):**

| Phase | Behavior |
| --- | --- |
| **v1** | On root invalidate: clear surface, **paint full tree**. Dirty flags only coalesce invalidation to the root. No retained bitmaps required. |
| **Later** | Opt-in `Picture` / `Image` cache on nodes with asymmetric repaint; blit/replay instead of re-entering paint body. |

Do **not** implement in v1:

- Per-layer bitmap caches
- Opaque-coverage dirty-region compositor
- GPU offscreen caches sharing `GRContext`
- Partial GL `InvalidateSurface` as the correctness path

Animation frames (FR-7): full-tree paint each frame is OK initially; add sibling caches only when profiling demands it. `StartUpdating` / `EndUpdating` still coalesce to one invalidate (FR-10).

### Cache implementation notes (when opt-in lands)

- Prefer **`SKPicture`** first (low memory); **`SKImage`** for expensive static subtrees.
- Invalidate / recreate on size change, density change, and content change.
- Always assume the **root surface present** may clear; correctness must not depend on uncleared GL backbuffers.
- Treat transparency as a reason **not** to assume a blit covers neighbors — with full-tree redraw this is automatic; with caches, fall back to replaying overlapping nodes.

## Coordinate system

- `ISkUiView.Paint` sizes, positions, clips, and child frames use **MAUI DIPs** (same as Measure / Arrange / Touch).
- Only the standalone root handler maps DIPs ↔ Skia pixels (`IgnorePixelScaling` / density) at the surface boundary.
- Layer caches are typically allocated in **device pixels** sized from DIP bounds × density; document the chosen convention in XML docs when implemented.

## `HwAccelerated` and the paint path (FR-14)

| Setting | Standalone root | Hosted child |
| --- | --- | --- |
| `true` | Handler creates GL / `SKGLView`-class surface | Ignored — no surface |
| `false` | Handler creates software / `SKCanvasView`-class surface | Ignored — no surface |

Defaults: `SkUiContentView` / `SkUiLayout` → `true`; leaf controls → `false`. Prefer composing many controls under one accelerated host rather than many standalone GL surfaces.

Document fallback if GL is unavailable when `HwAccelerated` is true.

## Public surface (target)

Exact signatures TBD (Requirements open decisions); intent:

- **`ISkUiView.Paint(...)`** — canvas + paint context (bounds, density/scale, optional dirty rect, GR context when available).
- **`SkUiView`** virtual layer hooks and clip/opacity application.
- Invalidation APIs: mark node / layer dirty; propagate to root host.
- Shared drawing helpers in a documented primitives namespace/class.
- XML docs: Option A layers, clip vs hit-test, cache/transparency caveats, DIP coordinates.

## What we need to implement

### Core (surface + paint walk)

- [ ] Custom handler paint callback → root `Paint` with DIP mapping.
- [ ] Root clear / present policy documented and implemented.
- [ ] Tree paint walk: `SkUiContentView` → `Content`; `SkUiLayout` → `Children` in z-order.
- [ ] Hosted nodes paint without handler; invalidation bubbles to standalone ancestor.
- [ ] Honor arranged `Frame` / transforms for child placement.
- [ ] Optional dirty-rect or overlap tests to skip non-intersecting children.

### Layers (FR-9)

- [ ] Background / Overlay via **`PaintBackground` / `PaintOverlay` delegates**; Content via virtual **`OnPaintContent`** (**Decided**).
- [ ] Default empty implementations; controls override as needed.
- [ ] Layouts: paint **Children / Content after Content chrome**, then Overlay.
- [ ] Shared drawing helpers for common chrome (**FR-18:** public control look; today `SkUiChrome`).
- [ ] Apply model to built-in controls (`SkUiLabel`, button, grid lines example when grid exists).

### Clip / mask (FR-11)

- [ ] Rectangle / rounded-rect / path mask constraints on paint.
- [ ] Clip applied consistently across layers (with documented opt-out).
- [ ] Demo: rounded control with transparent corners still hit-testing full layout rect.

### Transparency (FR-8)

- [ ] Node opacity in paint path (`SaveLayer` when 0&lt;α&lt;1; fast paths for 0/1).
- [ ] Overlap compositing correct for translucent siblings on the live walk.
- [ ] Demo: overlapping transparent content.
- [ ] *(Later)* Transparency rules for opt-in caches — not required for v1 full redraw.

### Caching / performance (NFR-2)

- [ ] **v1:** full-tree paint on root invalidate; no retained paint cache by default.
- [ ] Dirty flags coalesce paint to root; selective **measure/arrange** remains required (LayoutSystem).
- [ ] *(Later)* Opt-in `UseCache` = `None` | `Picture` | `Image`; drop on size/density change.
- [ ] *(Later)* Animation path may keep static sibling pictures warm — only if profiling needs it.

### Verification

- [ ] Standalone leaf paints on SW surface (`HwAccelerated = false`).
- [ ] Hosted tree under `SkUiContentView`: one surface; children visible and correctly positioned.
- [ ] Clip demo + transparency overlap demo.
- [ ] Property batching: `StartUpdating` / `EndUpdating` yields a single invalidate/paint.
- [ ] *(Later)* Opt-in Picture cache: content update rebuilds picture; unchanged sibling can replay.

## Open items

Record answers here when decided; keep Requirements “Open decisions” in sync for cross-cutting items.

**Decided:** v1 full-tree paint; no default retained paint cache; opt-in `Picture`/`Image` later — see *Recommended solution* and Requirements *Decided*.

1. **`ISkUiView.Paint` signature:** canvas-only vs paint-args object; density/scale; void return for v1.
2. ~~**Layer API names:** …~~ **Decided:** Content = virtual `OnPaintContent`; Background/Overlay = `PaintBackground` / `PaintOverlay` delegates (fluent `SetPaint*`). No virtual `OnPaintBackground` / `OnPaintOverlay`.
3. **Children vs Content:** **Decided** — paint Children / Content after Content chrome, before Overlay.
4. **When to add `UseCache`:** after first gallery profiling, or stub the enum early with only `None` implemented?
5. **Animation / `HasRenderLoop`:** full-tree paint per frame until measured otherwise (still open in Requirements).
6. **Overlay input:** v1 view-level hit-test only (**Decided**).

## References

- [Requirements.md](Requirements.md) — FR-8, FR-9, FR-11, FR-13, FR-14, NFR-2, Decided (drawing layers, clip vs hit-test, coordinates, HW acceleration).
- [LayoutSystem.md](LayoutSystem.md) — arranged bounds and DIP frames that paint consumes.
- [EventMechanism.md](EventMechanism.md) — hit-test uses arranged bounds; clip is paint-only by default.
- **DrawnUi** ([taublast/drawnui](https://github.com/taublast/drawnui)) — closest analogue: default `SkiaCacheType.None`; opt-in Picture/Image/GPU/composite; warns on GPU reuse and composite invalidation churn.
- **Flutter** — `RepaintBoundary` / `OffsetLayer`: cache only for asymmetric dirty; selective record + full composite; not a second layout model.
- **Avalonia** — record/replay draw lists; optional `BitmapCache`; dirty rects when frame retained; chrome via parent `Render` order, not nested chrome visuals.
- **SkiaSharp** — `SKGLView` / `SKCanvasView` present; treat GL backbuffer as cleared each frame; retain via app-owned `SKPicture` / `SKImage` if needed.
