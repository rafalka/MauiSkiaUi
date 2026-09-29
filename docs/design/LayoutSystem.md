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

### Detection

The rule is structural, decided by the node's MAUI `Parent`:

- **Hosted:** the parent is another SkiaUi node — the view was assigned to `SkUiContentView.Content` (or another single-child host) or added to `SkUiLayout.Children`. A child must be unparented and handlerless when it is added (otherwise `InvalidOperationException`), cycles are rejected, and it becomes a logical child of that parent (binding context, XAML ownership). MAUI never creates a handler for it, because no MAUI layout owns it; if one is forced anyway, the frame renderer rejects it.
- **Standalone:** the parent is a MAUI element (page, MAUI layout, `ScrollView`, collection cell). MAUI creates `SkUiViewHandler`, and this node becomes the root of a SkiaUi island: it owns the surface (`HwAccelerated`), the render loop and pointer routing for its hosted descendants.
- Moving a node between a SkiaUi parent and a MAUI parent switches its mode. Remove it from its old parent first: a standalone node must have lost its handler before it can be hosted.

**XAML nesting:** anything nested inside a SkiaUi element in markup is hosted; only the outermost SkiaUi element under a MAUI parent is standalone. `SkUiMauiContentView` is hosted like any other node, but creates a handler for its wrapped MAUI control (FR-16).

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
- Retained per-node pictures for unchanged paint ([RenderingPipeline.md](RenderingPipeline.md)); transparency-aware invalidation (FR-8).

## Right-to-left (FlowDirection)

MAUI mirrors native views for `FlowDirection="RightToLeft"`. Hosted SkiaUi nodes have no native view, so SkiaUi mirrors them itself:

- Layout managers still compute left-to-right frames (MAUI `GridLayoutManager`, stack and absolute managers are reused unchanged).
- `SkUiView.ArrangeOverride` mirrors the **final** frame, after margins and alignment, inside the parent's children space when the parent's effective flow direction is RTL: `x' = parentWidth − frame.Right`. That is what a native mirrored layout does: grid column 0 moves to the right, horizontal stacks run right to left, `Start` alignment and leading margins land on the right.
- Effective direction comes from MAUI (`IVisualElementController.EffectiveFlowDirection`), which already propagates to logical children. Every descendant whose effective direction changes gets `PropertyChanged(FlowDirection)` and invalidates its layout.
- Hit-testing, native overlays (`ComputeRootRelativeFrame`) and the compositor all read `Frame`, so they follow the mirrored layout with no extra work.
- A horizontal `SkUiScrollView` starts at its logical start (the right end) in RTL. `ScrollX` stays physical (0 = left edge).
- Core mirrors the same way: `SkUiCoreNode.FlowDirection` (`MatchParent` inherits from the Core parent, then from the `SkUiCoreHost`).
- Direction-dependent chrome: the Switch thumb travels the other way; CheckBox / RadioButton glyphs sit at the start edge (not mirrored); `SkUiCoreTable` chrome is mirrored with its cells. Images and activity indicators are not mirrored, matching MAUI.

## Coordinate system

- All Measure / Arrange / Paint / Touch inputs and `Frame` / `DesiredSize` use **MAUI DIPs**.
- The standalone root handler maps DIPs ↔ Skia pixels (`IgnorePixelScaling` / density) at the surface boundary only.
- Layout managers and hosted children never work in raw pixels.

## Implementation checklist

### Core (`SkUiView`)

- [x] Override **`MeasureOverride`**: handler-independent; margins + dimension requests; cache + dirty flags.
- [x] Override **`ArrangeOverride`**: set `Frame`; optional `Handler?.PlatformArrange` for standalone root only.
- [x] Override **`InvalidateMeasureOverride`** (and arrange/paint invalidation): propagate in hosted tree without handler.
- [x] Document hosted vs standalone detection ([Detection](#detection)).
- [x] Ensure `IView.Measure` / `Arrange` assign `DesiredSize` / `Frame` consistently with MAUI.

### Content host (`SkUiContentView`)

- [x] Measure: constrain and measure `Content`; return size including padding if any.
- [x] Arrange: arrange `Content` into content bounds.
- [x] Do not create a handler for `Content` when nested.

### Layouts (`SkUiLayout` + concrete)

- [x] Implement MAUI layout host interfaces as needed (`ILayout`, `IGridLayout`, …).
- [x] Wire **layout managers** (reuse or port) for Grid, Vertical/Horizontal stack, Absolute, etc.
- [x] Attached properties for Grid row/column/span with drop-in names/behavior.
- [x] Selective dirty tracking over `Children` (FR-3).
- [x] Default `HwAccelerated = true` when the layout is a standalone root (FR-14).

### Controls

- [x] Leaf `MeasureOverride` based on content (e.g. `SkUiLabel` text / font metrics).
- [x] Default `HwAccelerated = false` for leaves used standalone in collections (FR-14).

### Verification

- [x] Hosted tree under `SkUiContentView`: children `Handler == null`; non-zero measures; correct Grid Auto/`*` layout.
- [x] Standalone `SkUiLabel` / `SkUiGrid` in a MAUI `VerticalStackLayout`: correct size and position.
- [x] Standalone `SkUiGrid` with hosted children: one surface, children arranged by manager.
- [x] Parity samples vs MAUI `Grid` / `StackLayout` for the same markup structure (spot-check).
- [x] Invalidation: property change on hosted child remeasures only what is needed and redraws the root surface.

## Open items

- How aggressively to wrap stock managers for selective child measure vs porting them. Today `SkUiLayout` overrides `MeasureOverride` / `ArrangeOverride`, calls the stock MAUI managers, and unchanged children answer from their measure cache.

Resolved: detection is structural ([Detection](#detection)); composite-time animations run on the render thread and never trigger layout passes ([RenderingPipeline.md](RenderingPipeline.md#animation-tiers)).

## References

- [Requirements.md](Requirements.md) — FR-3, FR-3a, FR-13, FR-14, NFR-2, Decided (layout system).
- [DrawingMechanism.md](DrawingMechanism.md) — paint walk, layers, clip, and caching on the same arranged tree.
- [EventMechanism.md](EventMechanism.md) — input delivery on the same arranged tree (hit-test uses bounds; clip is paint-only by default).
- Local MAUI: `Microsoft.Maui.Layouts.GridLayoutManager`, `LayoutManager`, `LayoutExtensions.ComputeDesiredSize` / `ComputeFrame`.
- Local MAUI Controls: `VisualElement.MeasureOverride` / `ArrangeOverride`, `Layout` + `CreateLayoutManager()`.
