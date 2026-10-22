# SkiaUi layout system

How drawn views measure and arrange: MAUI's measure / arrange contract and layout managers, not Flutter-style box constraints, so a `SkUi*` layout replaces its MAUI counterpart by changing the XAML prefix. Overview: [Architecture.md](Architecture.md#layout). The original design notes are archived in [docs/archive/LayoutSystem.md](../archive/LayoutSystem.md).

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

## Measure and arrange on `SkUiView`

### `MeasureOverride`

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

### `ArrangeOverride`

- Set **`Frame`** from arranged bounds (typically via `ComputeFrame` for margin/alignment parity, or an equivalent that works with `Handler == null`).
- Position hosted children / update Skia layout rects used by Paint and hit-testing.
- **`Handler?.PlatformArrange(Frame)`** only when standalone (root with a surface); never required for hosted children.
- Changing offset alone must not force child remeasure/repaint when size and visual content are unchanged (FR-3a).

### `InvalidateMeasureOverride`

Default MAUI behavior invokes the handler. With no handler, invalidation must:

- Mark the node dirty (measure / arrange / paint flags).
- Propagate to the SkiaUi parent / root host so the standalone ancestor schedules a MAUI invalidate or a Skia redraw as appropriate.
- Honor `StartUpdating` / `EndUpdating` coalescing (FR-10).

## Reusing MAUI layout managers

Every child implements **`IView`**, so MAUI's own managers measure and arrange drawn children: `SkUiGrid` runs `GridLayoutManager` (as `IGridLayout`), the stacks `VerticalStackLayoutManager` / `HorizontalStackLayoutManager`, `SkUiAbsoluteLayout` `AbsoluteLayoutManager`. This works because children's `MeasureOverride` / `ArrangeOverride` run without a handler (above) and expose the `DesiredSize`, visibility, margins and alignment the managers read. Grid behavior (Auto / star / absolute tracks, spans, spacing) is therefore MAUI's, with no grid math of SkiaUi's own.

**Flex:** `SkUiFlexLayout` calls MAUI's shipped `FlexLayoutManager`, but the flex engine it drives through `IFlexLayout.Layout` (`Microsoft.Maui.Layouts.Flex.Item`) is internal to MAUI. SkiaUi carries a port of that engine (`SkUiFlexItem`, from dotnet/maui 10.0.101) with the algorithm unchanged; a headless test checks its frames against MAUI's own `FlexLayout` over every container combination.

**Layouts without a MAUI manager** (wrap, shrink, align stack) use one engine per algorithm for both layers (`SkUiWrapEngine`, `SkUiShrinkEngine`, `SkUiAlignStackEngine`). Engines reach children through struct adapters (`ISkUiLayoutChildren`: `SkUiViewChildren` for SkUi*, `SkUiCoreNodeChildren` for Core), so they take no allocation per pass. The SkUi* adapter lets the child align itself (`ComputeFrame`); the Core adapter aligns it in its slot (`AlignInSlot`), since Core nodes do not align themselves.

**Attached properties:** MAUI's attached-property callbacks only invalidate MAUI's own layout types, so each `SkUiLayout` lists the child properties it reads (`AffectsChildLayout`: `Grid.Row`…, `AbsoluteLayout.LayoutBounds`…, `FlexLayout.Grow`…, `SkUiShrinkLayout.Shrink`) and re-measures when one changes.

Multi-pass measure (Auto then `*` columns/rows) is **expected**. Optimize with per-node caches and dirty flags; do not switch to Flutter constraints to avoid passes.

## Selective measure / arrange / paint

- Measure results are cached per constraint (`SkUiView.MeasureOverride`, `SkUiCoreNode.Measure`); controls implement `MeasureContent`. A node measured again with the same constraints and nothing invalidated returns its cached size without visiting its children.
- Arrange-only changes (same size, new offset) update `Frame` and the node's composite-time offset: no remeasure, no re-record.
- A child's measure change re-lays out its ancestors without re-recording them; only nodes whose size changed re-record (SkUi* views; Core nodes still re-record measure-dirty parents, see the [roadmap](Roadmap.md)).
- Paint is retained per node ([RenderingPipeline.md](RenderingPipeline.md)).

## Right-to-left (FlowDirection)

MAUI mirrors native views for `FlowDirection="RightToLeft"`. Hosted SkiaUi nodes have no native view, so SkiaUi mirrors them itself:

- Layout managers still compute left-to-right frames (MAUI `GridLayoutManager`, stack, absolute and flex managers are reused unchanged; the wrap and shrink engines are left to right too).
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

