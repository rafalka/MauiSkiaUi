# SkiaUi layout system

Design notes and implementation checklist for **FR-3**, **FR-3a**, and dual-mode measure/arrange (**FR-13**) in [Requirements.md](Requirements.md).

## Goal

Provide a **MAUI-compatible** measure / arrange pipeline so SkiaUi can ship near drop-in copies of MAUI layouts (e.g. `SkUiGrid`) and **reuse MAUI layout managers** where practical, while supporting two hosting modes:

| Mode | Meaning |
| --- | --- |
| **Standalone** | `SkUiView` (or subclass) is a child of the MAUI visual tree; our custom handler creates a SW or GL platform surface (`HwAccelerated`). |
| **Hosted** | Node is `SkUiContentView.Content` or an item in `SkUiLayout.Children`; **no** MAUI handler / platform view; measure/arrange via `IView`, paint/touch on the parent’s shared surface. |

Apps should be able to move a slow MAUI subtree to SkiaUi without rewriting layout markup (Grid rows/columns, stacks, margins, alignment).

## Decision summary

| Topic | Choice |
| --- | --- |
| Layout contract | **MAUI** measure + arrange / layout managers (**not** Flutter `BoxConstraints`) |
| Why | Drop-in replacement goal; reuse `GridLayoutManager` and peers |
| Type surface | `ISkUiView : IView`; `SkUiView` / `SkUiContentView` / `SkUiLayout` |
| Platform base | `SkUiView` does **not** derive from `SKGLView`; custom handler maps `HwAccelerated` |
| Hosted measure | Own `MeasureOverride` / `ArrangeOverride` — **must not** depend on `Handler` |
| Coordinates | Same as MAUI (DIPs / density) for Measure, Arrange, Paint, Touch |

## Architecture

```
Standalone root (handler + surface)
        │  IView.Measure / IView.Arrange
        ▼
SkUiContentView / SkUiLayout / SkUiView
        │  layout manager or Content forward
        ▼
Children (ISkUiView / IView)          ← Handler == null when hosted
        │  MeasureOverride / ArrangeOverride (SkiaUi-owned)
        ▼
DesiredSize / Frame (DIPs)
        │
        ▼
Paint on shared SKCanvas (hosted) or own surface (standalone)
```

### Who owns the layout pass?

| Role | Responsibility |
| --- | --- |
| **MAUI parent** (e.g. `VerticalStackLayout`) | Measures/arranges the **standalone** SkiaUi root via `IView` like any other MAUI view. |
| **SkUi layout managers** | On `SkUiLayout` subclasses (e.g. `SkUiGrid`), run MAUI-compatible measure/arrange over `Children` (reuse or port `GridLayoutManager`, stack managers, etc.). |
| **`SkUiContentView`** | Forwards measure/arrange to single `Content`. |
| **`SkUiView.MeasureOverride` / `ArrangeOverride`** | Single implementation used in **both** modes; sets `DesiredSize` / `Frame` without requiring a handler. |
| **Custom handler** | Standalone only: creates platform view; may call into the same measure path or rely on `IView.Measure` already having run. Does **not** measure hosted descendants. |

## Hosted vs standalone

### Detection (to implement)

Document and implement a clear rule, for example:

- **Hosted** when the logical SkiaUi parent is another `ISkUiView` (`Content` or `Children`), **or** when `Handler` is null and a SkiaUi ancestor owns the surface.
- **Standalone** when the view is attached to the MAUI visual tree with our handler created (root of a SkiaUi island, or a lone control in MAUI layout).

Hosted children must **never** create a handler even if `HwAccelerated` is true (FR-13 / FR-14).

### Standalone mode

1. MAUI layout calls `IView.Measure` → `VisualElement` path → **`SkUiView.MeasureOverride`**.
2. Override returns desired size (respect Width/Height request, min/max, margins — see below).
3. MAUI layout calls `IView.Arrange` → **`SkUiView.ArrangeOverride`** sets `Frame` (and may call `Handler?.PlatformArrange` for the **root** surface only).
4. Handler paints the Skia surface; if the root has `Content` / `Children`, paint walks the tree with arranged bounds.

For a standalone **`SkUiLayout`** (e.g. `SkUiGrid` used directly in a MAUI page): measure/arrange of children still runs inside the layout manager; children remain **hosted** (no per-child handler) even though the layout itself is standalone.

### Hosted mode

1. Parent layout manager (or `SkUiContentView`) calls `child.Measure(w, h)` / `child.Arrange(bounds)` on `IView`.
2. Same **`MeasureOverride` / `ArrangeOverride`** run; `Handler` is null.
3. Must **not** fall through to MAUI’s default `ComputeDesiredSize`, which returns **`Size.Zero` when `Handler == null`**:

```csharp
// Microsoft.Maui.Layouts.LayoutExtensions.ComputeDesiredSize
if (view.Handler == null)
    return Size.Zero;
```

4. Paint/touch come only from the ancestor that owns the surface.

## Measure and arrange — what to implement on `SkUiView`

### `MeasureOverride` (required)

Override on `SkUiView` (and refine in layouts/controls as needed):

- **Do not call `base.MeasureOverride`** for the default path that uses `ComputeDesiredSize` / handler `GetDesiredSize`.
- Compute intrinsic / content size in DIPs (text metrics, images, etc.).
- Apply the same semantics MAUI layout managers expect:
  - **Margins** — either include them like `ComputeDesiredSize` (subtract from constraints, add to returned size) or ensure managers and overrides agree; mismatch breaks Grid/Stack parity.
  - **WidthRequest / HeightRequest / Minimum / Maximum** — honor `IView` dimension properties.
  - **Visibility** — collapsed nodes skipped by managers (same as MAUI).
- Set / return value consistent with `IView.Measure` assigning **`DesiredSize`**.
- Support **selective measure**: if constraints and dirty flags unchanged, return cached `DesiredSize` without re-entering children (NFR-2).

Layouts (`SkUiLayout`): `MeasureOverride` (or `CrossPlatformMeasure` if mirroring MAUI `Layout`) should invoke the **layout manager** (`Measure(widthConstraint, heightConstraint)`), which in turn calls each child’s `IView.Measure`.

### `ArrangeOverride` (required)

- Set **`Frame`** from arranged bounds (typically via `ComputeFrame` for margin/alignment parity, or an equivalent that works with `Handler == null`).
- Position hosted children / update Skia layout rects used by Paint and hit-testing.
- **`Handler?.PlatformArrange(Frame)`** only when standalone (root with a surface); never required for hosted children.
- Changing offset alone must not force child remeasure/repaint when size and visual content are unchanged (FR-3a).

### `InvalidateMeasureOverride` (required for hosted)

Default MAUI behavior invokes the handler. With no handler, invalidation must:

- Mark the node dirty (measure / arrange / paint flags).
- Propagate to the SkiaUi parent / root host so the standalone ancestor schedules a MAUI invalidate or a Skia redraw as appropriate.
- Honor `StartUpdating` / `EndUpdating` coalescing (FR-10).

## Reusing MAUI layout managers

### Feasibility

Because every child implements **`IView`**, shipped managers such as **`GridLayoutManager`** can call `child.Measure` / `child.Arrange` **if**:

1. `SkUiGrid` (etc.) implements the matching host interface (`IGridLayout` / `ILayout` as required).
2. Children’s `MeasureOverride` / `ArrangeOverride` work with **`Handler == null`** (above).
3. `DesiredSize`, `Visibility`, margins, and alignment properties match what the manager reads from `IView`.

### `SkUiGrid` sketch

```
SkUiGrid : SkUiLayout, IGridLayout
  CreateLayoutManager() → new GridLayoutManager(this)   // or ported copy
  CrossPlatformMeasure / MeasureOverride → manager.Measure(...)
  CrossPlatformArrange / ArrangeOverride → manager.ArrangeChildren(...)
  Children as IList<ISkUiView> also exposed as ILayout/IContainer enumeration
  Attached Row/Column/Span properties → IGridLayout.GetRow/GetColumn/...
```

### Reuse vs port

| Approach | When |
| --- | --- |
| **Call shipped `GridLayoutManager`** | Prefer if `IGridLayout` + dual-mode Measure/Arrange are solid; least Grid math to maintain. |
| **Port / copy `GridStructure`** | If interface surface or multi-pass interaction with selective caching is awkward; keep MAUI behavior, own the code. |
| **Reimplement from scratch** | Last resort; still target MAUI-compatible results for drop-in markup. |

Multi-pass measure (Auto then `*` columns/rows) is **expected**. Optimize with per-node caches and dirty flags; do not switch to Flutter constraints to avoid passes.

## Selective measure / arrange / paint

Aligned with NFR-2 / FR-3:

- Per-node dirty flags: size, arrangement, visual layers.
- Layout managers should ideally skip clean children when constraints are unchanged; if a stock manager always measures all children, wrap or port so SkiaUi can short-circuit via cached `DesiredSize` when safe.
- Arrange-only changes (same size, new offset) update `Frame` / paint transform without remeasure.
- Cached bitmaps for unchanged paint; transparency-aware invalidation (FR-8).

## Coordinate system

- All Measure / Arrange / Paint / Touch inputs and `Frame` / `DesiredSize` use **MAUI DIPs**.
- The standalone root handler maps DIPs ↔ Skia pixels (`IgnorePixelScaling` / density) at the surface boundary only.
- Layout managers and hosted children never work in raw pixels.

## Implementation checklist

### Core (`SkUiView`)

- [ ] Override **`MeasureOverride`**: handler-independent; margins + dimension requests; cache + dirty flags.
- [ ] Override **`ArrangeOverride`**: set `Frame`; optional `Handler?.PlatformArrange` for standalone root only.
- [ ] Override **`InvalidateMeasureOverride`** (and arrange/paint invalidation): propagate in hosted tree without handler.
- [ ] Document hosted vs standalone detection.
- [ ] Ensure `IView.Measure` / `Arrange` assign `DesiredSize` / `Frame` consistently with MAUI.

### Content host (`SkUiContentView`)

- [ ] Measure: constrain and measure `Content`; return size including padding if any.
- [ ] Arrange: arrange `Content` into content bounds.
- [ ] Do not create a handler for `Content` when nested.

### Layouts (`SkUiLayout` + concrete)

- [ ] Implement MAUI layout host interfaces as needed (`ILayout`, `IGridLayout`, …).
- [ ] Wire **layout managers** (reuse or port) for Grid, Vertical/Horizontal stack, Absolute, etc.
- [ ] Attached properties for Grid row/column/span with drop-in names/behavior.
- [ ] Selective dirty tracking over `Children` (FR-3).
- [ ] Default `HwAccelerated = true` when the layout is a standalone root (FR-14).

### Controls

- [ ] Leaf `MeasureOverride` based on content (e.g. `SkUiLabel` text / font metrics).
- [ ] Default `HwAccelerated = false` for leaves used standalone in collections (FR-14).

### Verification

- [ ] Hosted tree under `SkUiContentView`: children `Handler == null`; non-zero measures; correct Grid Auto/`*` layout.
- [ ] Standalone `SkUiLabel` / `SkUiGrid` in a MAUI `VerticalStackLayout`: correct size and position.
- [ ] Standalone `SkUiGrid` with hosted children: one surface, children arranged by manager.
- [ ] Parity samples vs MAUI `Grid` / `StackLayout` for the same markup structure (spot-check).
- [ ] Invalidation: property change on hosted child remeasures only what is needed and redraws the root surface.

## Open items

- Exact hosted-vs-standalone detection API (property vs internal flag vs parent walk).
- Whether `SkUiLayout` mirrors MAUI `Layout.CrossPlatformMeasure`/`Arrange` or only overrides `MeasureOverride`/`ArrangeOverride`.
- How aggressively to wrap stock managers for selective child measure vs always porting.
- Interaction of layout passes with animation render loop (avoid full-tree remeasure every frame — see Requirements open decisions).

## References

- [Requirements.md](Requirements.md) — FR-3, FR-3a, FR-13, FR-14, NFR-2, Decided (layout system).
- [DrawingMechanism.md](DrawingMechanism.md) — paint walk, layers, clip, and caching on the same arranged tree.
- [EventMechanism.md](EventMechanism.md) — input delivery on the same arranged tree (hit-test uses bounds; clip is paint-only by default).
- Local MAUI: `Microsoft.Maui.Layouts.GridLayoutManager`, `LayoutManager`, `LayoutExtensions.ComputeDesiredSize` / `ComputeFrame`.
- Local MAUI Controls: `VisualElement.MeasureOverride` / `ArrangeOverride`, `Layout` + `CreateLayoutManager()`.
