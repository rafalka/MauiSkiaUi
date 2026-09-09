# Requirements

Planned work for SkiaUi. Items here are **not yet implemented** unless moved into [README.md](README.md) under *Current implementation*.

## Goals

Build a set of **base controls and layouts drawn entirely with SkiaSharp**, using **GPU / hardware acceleration** where the platform supports it (`SKGLView`).

**Near drop-in replacement for standard MAUI UI:** a primary goal is to replace a slow MAUI visual tree with a fast SkiaUi tree by providing Skia-drawn copies of MAUI controls and layouts. Measure / layout must therefore follow the **MAUI layout system** so existing MAUI layout-manager concepts and algorithms can be reused (see *Layout model*).

MAUI hosts a single accelerated surface; all SkiaUi visuals live inside a custom view tree that does its own measure, layout, paint, and hit-testing.

**XAML-first composition:** the entire SkiaUi tree inside the bridge must be writable in XAML the same way MAUI layouts nest children — no mandatory code-behind to assemble the tree.

Example target markup:

```xml
<VerticalStackLayout>
  <SkUiContentView>
    <SkUiGrid>
      <SkUiLabel Text="abcd" />
      <SkUiLabel Text="bcdef" />
    </SkUiGrid>
  </SkUiContentView>
</VerticalStackLayout>
```

## Architecture

```
MAUI visual tree
└── SkUiContentView : SkUiView          ← typical composed-tree host (HwAccelerated default true)
        └── Content : ISkUiView
                └── SkUiGrid : SkUiLayout : SkUiView
                        ├── Children[0] : ISkUiView
                        └── Children[1] : ISkUiView
```

### Type hierarchy

| Type | Role |
| --- | --- |
| **`ISkUiView`** | Interface: `ISkUiView : IView`; adds Paint + Touch |
| **`SkUiView`** | Base class for SkiaUi views; implements `ISkUiView` |
| **`SkUiContentView`** | Derives from `SkUiView`; single-child host with **`Content`** (`ISkUiView`) |
| **`SkUiLayout`** | Derives from `SkUiView`; base for all layouts with **`Children`** (`IList<ISkUiView>`) |
| Concrete layouts (e.g. `SkUiGrid`) | Derive from **`SkUiLayout`** |
| Concrete controls (e.g. `SkUiLabel`) | Derive from **`SkUiView`** (or a control-specific base under `SkUiView`) |

### `SkUiView`

- Shared base for content hosts, layouts, and controls.
- Implements **`ISkUiView`** (hence MAUI **`IView`**): Measure/Arrange from `IView`; Paint and Touch from `ISkUiView`.
- Owns shared behavior: invalidation, `StartUpdating` / `EndUpdating` (FR-10), layers (FR-9), clip/mask (FR-11), selective cache hooks (NFR-2).
- Does **not** derive from `SKGLView` / `SKCanvasView`. A **custom MAUI handler** creates the platform surface when the view is standalone (see *Hardware acceleration*).
- **`HwAccelerated`** (CLR property, **not** a `BindableProperty`): when `true`, the handler creates a GPU / GL platform view (`SKGLView`-equivalent); when `false`, a software Skia platform view (`SKCanvasView`-equivalent). **Init-only in practice:** the value is read when the handler creates the platform view and **must not change afterward** (ignore or throw if set later — document). It cannot be a C# `init` accessor because XAML needs a normal settable property.
- When **hosted** under another SkiaUi parent, does **not** allocate a MAUI handler / platform view (FR-13) — `HwAccelerated` is irrelevant for that instance.
- When **standalone** in the MAUI tree (including many cells in a `CollectionView`), participates via `IView` + our handler; default `HwAccelerated` avoids spawning many GL surfaces (FR-14).

### `SkUiContentView`

- Derives from **`SkUiView`**.
- **`HwAccelerated` defaults to `true`** (hosts a drawn subtree; one GL surface for the composition is desired).
- Exposes **`Content`** of type **`ISkUiView`**, marked `[ContentProperty(nameof(Content))]` so a single child in XAML becomes the root of the inner tree.
- Typical outer host for a composed SkiaUi subtree inside a MAUI page (see example markup).
- Forwards measure / arrange / paint / touch to `Content` (and through the tree as needed):

| Host concern | Forwarded as |
| --- | --- |
| Available size / constraint changes | **`IView.Measure`** |
| Final arranged bounds | **`IView.Arrange`** |
| Paint surface (GL or SW) | **`ISkUiView.Paint`** |
| Touch / gestures | **`ISkUiView` touch** + shared SkiaUi gestures (FR-15 / [EventMechanism.md](EventMechanism.md)) |

- When acting as a standalone root host: enables touch, invalidates the surface on redraw requests, maps MAUI ↔ Skia coordinates. **`ISkUiView` / `IView` Measure / Arrange / Paint / Touch use the same coordinate system as MAUI** (DIPs / density).

### `SkUiLayout`

- Derives from **`SkUiView`**.
- **`HwAccelerated` defaults to `true`** (layout hosts drawn children; intended as a composition root when used standalone).
- Base class for **all** SkiaUi layouts.
- Exposes **`Children`** as **`IList<ISkUiView>`**, marked `[ContentProperty(nameof(Children))]` so nested XAML children populate the list.
- Measures / arranges / paints / hit-tests children using the MAUI-based layout model (FR-3a) and selective dirty tracking (FR-3 / NFR-2).
- Concrete layouts (grid, stack, etc.) subclass `SkUiLayout` and plug in layout-manager behavior.

### Contract: `ISkUiView`

`ISkUiView` **derives from MAUI `IView`**. Every SkiaUi control and layout implements `ISkUiView` (and therefore `IView`). Measure and arrange come from `IView`; SkiaUi adds paint and touch.

Inherited from **`IView`** (names as in MAUI; used for MAUI layout managers and standalone hosting):

- **Measure** — `Size Measure(double widthConstraint, double heightConstraint)` (and related `IView` layout surface).
- **Arrange** — arrange into final bounds (MAUI arrange / layout-manager path).

Added by **`ISkUiView`** (exact signatures TBD during implementation):

- **Paint** — draw into the provided `SKCanvas` (and related GL paint args as needed), including Background / Content / Overlay layers (FR-9).
- **Touch** — handle pointer / touch; return whether the event was handled (for hit-test bubbling). Default hit-testing uses the control’s **arranged bounds** (FR-11); clip/mask affects paint, with shape-aware hits only as an opt-in. Higher-level gestures (tap, double tap, long press, swipe) are classified and delivered by SkiaUi’s own mechanism (FR-15), not MAUI `GestureRecognizers` — see [EventMechanism.md](EventMechanism.md).

Layouts are `SkUiLayout` subclasses (hence `ISkUiView` / `IView`) that own **`Children`**. Single-child hosting uses **`SkUiContentView.Content`**. When nested under a SkiaUi parent, a child must **not** allocate a MAUI handler / platform view — only the outer root host owns the GL surface (FR-13).

Measure, Arrange, and Paint must be **selective**: parents (especially layouts) invoke children only when necessary and reuse cached measure sizes and cached painted bitmaps for unchanged children (see NFR-2 / FR-3).

### Layout model (MAUI-based)

SkiaUi’s measure / layout pipeline must follow the **.NET MAUI layout system** (measure + arrange / layout managers), **not** Flutter’s box-constraint model. Design details for hosted vs standalone measure/arrange, `MeasureOverride` / `ArrangeOverride`, and layout-manager reuse: [LayoutSystem.md](LayoutSystem.md).

**Rationale:** a main goal is to replace an existing slow MAUI UI tree with a fast SkiaUi tree by shipping Skia-drawn copies of standard MAUI controls and layouts. To **reuse MAUI layout managers** (and stay behavior-compatible with MAUI Grid, Stack, Absolute, etc.), the layout contract must be MAUI-based.

- **Measure:** parents measure children with an available size (and MAUI-equivalent constraint semantics: width/height requests, minimums, expansions as in MAUI `IView` / layout managers). Children return a desired size.
- **Arrange / Layout:** parents assign final bounds to children; alignment, margins, and padding follow MAUI conventions where we claim drop-in parity.
- Multi-pass measure (e.g. star/`*` Grid) is **allowed and expected** where MAUI layout managers require it; optimize with caching and dirty tracking (NFR-2 / FR-3) rather than inventing a different constraint model.
- Prefer reusing or porting MAUI layout-manager logic into SkiaUi layouts over reimplementing Flutter-style constraint solvers.
- Selective invalidation still applies: a dirty subtree should not force unrelated siblings to remeasure when their available size and content are unchanged.

Primary reference: .NET MAUI layout (`Layout`, layout managers, `IView.Measure` / arrange) under the local MAUI checkout. Flutter remains useful for paint/compositor ideas only — **not** for the layout contract.

### XAML object model

- Concrete control/layout types (e.g. `SkUiGrid`, `SkUiLabel`) are instantiable from XAML (public parameterless constructors).
- Properties that mirror MAUI controls use **`BindableProperty`** for XAML and data-binding parity; performance-oriented **direct setters** are also available (see FR-10 / Decided).
- **Theming** uses standard **MAUI `Style` / `VisualState` / resource dictionaries** applied to those bindable properties (FR-12) — not a separate SkiaUi theme system.
- Layout types derive from **`SkUiLayout`** and expose **`Children`** (`IList<ISkUiView>`) as `[ContentProperty]`.
- **`SkUiContentView`** exposes single **`Content`** (`ISkUiView`) as `[ContentProperty]` for one-child hosting.
- Attached layout properties (e.g. grid row/column) are supported where the layout requires them.
- XML namespace mapping is documented for apps (clr-namespace / `xmlns`); optional `XmlnsDefinition` for a shorter xmlns.
- Same trees must remain creatable from C# for parity.

### Acceleration

- **`SkUiView` does not subclass `SKGLView`.** A dedicated SkiaUi MAUI **handler** chooses the platform view from **`HwAccelerated`**: GL (`SKGLView`-class) vs software (`SKCanvasView`-class). Value is fixed at handler creation (init-only semantics; settable CLR property for XAML, not C# `init`).
- **Defaults:** `SkUiContentView` and `SkUiLayout` → `HwAccelerated = true`; other `SkUiView` controls (e.g. `SkUiLabel`, buttons) → `HwAccelerated = false`.
- **Rationale:** standalone SkiaUi controls may appear many times on one page (e.g. inside `CollectionView`). Many simultaneous GL surfaces are costly; leaf controls default to software. Composition hosts (`SkUiContentView` / `SkUiLayout`) keep HW on by default so one accelerated surface can draw a whole subtree.
- Hosted children never create their own surface regardless of `HwAccelerated` (FR-13).
- Document fallback if GL is unavailable when `HwAccelerated` is true.

## Functional requirements

### FR-1 — Core bridge and contract

- [ ] Define **`ISkUiView : IView`** adding **Paint** and **Touch** handling; Measure/Arrange come from `IView`.
- [ ] Implement **`SkUiView`** as the base class implementing `ISkUiView` (shared invalidation, update batching, layers hooks); **not** derived from `SKGLView`.
- [ ] Implement **`SkUiContentView : SkUiView`** with **`Content`** (`ISkUiView`) and `[ContentProperty(nameof(Content))]`; default **`HwAccelerated = true`**.
- [ ] Implement **`SkUiLayout : SkUiView`** with **`Children`** (`IList<ISkUiView>`) and `[ContentProperty(nameof(Children))]`; all layouts derive from `SkUiLayout`; default **`HwAccelerated = true`**.
- [ ] Other controls deriving from `SkUiView` default **`HwAccelerated = false`**.
- [ ] Provide a **custom MAUI handler** for `SkUiView` that creates a GL or software Skia platform view based on `HwAccelerated`.
- [ ] Wire standalone-host paint / touch / size changes to the tree via `IView` measure/arrange and `ISkUiView` paint/touch.
- [ ] Register SkiaSharp / SkiaUi handlers from a library entry point; demo calls it from `MauiProgram`.
- [ ] Remove template placeholders (`Class1`, platform stubs) once the public API exists.
- [ ] XML docs on all public types and members.

### FR-2 — XAML composition

- [ ] Entire SkiaUi subtree under `SkUiContentView` can be authored in XAML (nested layouts and controls).
- [ ] Layouts use a content-property child collection so markup like `<SkUiGrid><SkUiLabel .../></SkUiGrid>` works.
- [ ] Demo (or sample page) shows the target markup pattern with MAUI parents outside and SkiaUi inside the bridge.
- [ ] Document the `xmlns` to use for `MauiSkiaUi` types.

### FR-3 — Base layout primitives

- [ ] Implement at least one concrete **`SkUiLayout`** subclass suitable for XAML nesting (e.g. `SkUiGrid` or stack) that measures / arranges `Children` and paints / hit-tests them in z-order.
- [ ] Clear invalidation rules: property or structure changes request redraw (and remeasure when needed).
- [ ] Layouts dirty-track children so only the affected subset is re-measured, re-laid out, or re-painted; unchanged siblings keep cached measure results and cached painted bitmaps (see NFR-2).

### FR-3a — MAUI-based layout system

Design and checklist: [LayoutSystem.md](LayoutSystem.md).

- [ ] Measure / arrange semantics match **MAUI’s layout system** (available size in, desired size out; arrange assigns final bounds) so SkiaUi layouts can reuse MAUI layout-manager concepts and stay near drop-in compatible.
- [ ] Built-in layouts (stack, grid, etc.) follow MAUI layout behavior (including multi-pass measure where MAUI does, e.g. star rows/columns), optimized with dirty tracking and caching (NFR-2 / FR-3) rather than a Flutter box-constraint pipeline.
- [ ] Changing a child’s offset alone must not require that child to remeasure or repaint when its size and visual content are unchanged.
- [ ] Document how SkiaUi layouts map to MAUI layout managers / attached properties (Grid row/column, stack orientation, etc.).
- [ ] Do **not** use Flutter `BoxConstraints` / constraints-down–sizes-up as the layout contract; Flutter refs are optional for paint/compositor patterns only.
- [ ] **`SkUiView.MeasureOverride` / `ArrangeOverride`** work with **`Handler == null`** (hosted mode); do not rely on `ComputeDesiredSize`’s handler path. Invalidation propagates without a platform handler (see LayoutSystem.md).

### FR-4 — Base controls

- [ ] Initial control set drawn entirely via Skia (no nested MAUI visuals inside the Skia tree), including at least `SkUiLabel` (or equivalent text control).
- [ ] Each control derives from **`SkUiView`** (implements `ISkUiView`), is XAML-constructible, works under `SkUiContentView` / `SkUiLayout`, and can be used standalone in the MAUI tree (FR-13).

### FR-5 — Demo gallery

- [ ] Demo hosts `SkUiContentView` with sample trees defined primarily in XAML.
- [ ] Gallery pages for layouts and controls.
- [ ] Verified on Android and at least one Apple target (iOS or Mac Catalyst).

### FR-6 — Packaging readiness

- [ ] Public types use the **`SkUi*`** naming convention (e.g. `SkUiView`, `SkUiContentView`, `SkUiLayout`, `SkUiGrid`, `SkUiLabel`); assembly / project / NuGet package id remains **`MauiSkiaUi`**.
- [ ] Prepare **`MauiSkiaUi`** for NuGet publish (package id, versioning, metadata, symbols as needed).
- [ ] **`MauiSkiaUiDemo`** stays **in-repo only** — not published as a NuGet package.

### FR-7 — Animation

- [ ] Support property / transform / opacity (and similar) animations on `ISkUiView` nodes, driven so that active animations can sustain **butter-smooth ~60 fps** on target devices with GPU-backed `SKGLView`.
- [ ] Animation clock / ticker integrates with the bridge invalidation model (continuous frames while any animation is active; idle when none are).
- [ ] Animated nodes mark themselves dirty each frame as needed; layouts and the paint path still honor selective Measure / Layout / Paint and cached bitmaps for **non-animated** siblings (NFR-2 / FR-3).
- [ ] Demo or gallery sample shows at least one continuous animation under `SkUiContentView`.

### FR-8 — Transparency

Design details (compositing + cache rules): [DrawingMechanism.md](DrawingMechanism.md).

- [ ] Support transparency on SkiaUi nodes: per-node opacity / alpha, and content that is partially or fully transparent (including clear / translucent backgrounds).
- [ ] Compositing preserves correct z-order when transparent nodes overlap opaque or other transparent siblings (live paint walk).
- [ ] Selective redraw / retained caches (when introduced) must **account for transparency**; v1 full-tree redraw does not need opaque-cover dirty expansion — see [DrawingMechanism.md](DrawingMechanism.md).
- [ ] Demo or gallery sample shows overlapping transparent content redrawing correctly.

### FR-9 — Drawing layers (Option A)

Design and checklist: [DrawingMechanism.md](DrawingMechanism.md).

**Decision:** use a **dedicated layer structure** on the control (named slots / paint phases such as Background, Content, Overlay) — **not** nesting separate `ISkUiView` hosts for each chrome layer (Option B rejected for this purpose).

**Rationale:** layers often need **data owned by the control**. Example: a table / grid draws grid **lines on the Background layer** using that grid’s own row and column measurements; a nested “background child view” would not naturally own those metrics without awkward coupling or duplication.

- [ ] Support splitting a control’s drawing into ordered **layers** (Background / Content / Overlay, extensible as needed), e.g. text with background fill, glyphs, and badge/focus overlay.
- [ ] Layers are paint (and optional cache) phases of the **same** `ISkUiView`, with direct access to that control’s layout and state — not a separate child layout tree per layer.
- [ ] Layers participate in selective Paint when opt-in caches exist (NFR-2 / FR-8); v1 may repaint all phases on each node paint with no per-layer bitmap cache — see [DrawingMechanism.md](DrawingMechanism.md).
- [ ] Favor **reusable drawing helpers / primitives** across controls (e.g. shared rounded-rectangle / border / fill used by many Background layers — not copy-pasted Skia paths per control). Reuse is via shared paint utilities or layer implementations, not by requiring every chrome piece to be its own `ISkUiView`.
- [ ] Hit-testing remains **view-level** (arranged bounds); overlays do not get separate hit geometry in v1 (FR-11 / [EventMechanism.md](EventMechanism.md)).
- [ ] Apply this model consistently to built-in controls; keep Option B-style nesting for true **child content** in layouts only (`Children`), not for a control’s own chrome layers.

### FR-10 — Bindable properties, direct setters, and update batching

Goal: stay a near **drop-in replacement** for standard MAUI controls (XAML + bindings) while offering a faster path when bindings are not needed.

- [ ] Public stylable / bindable API surface uses **`BindableProperty`** (e.g. `BackgroundColor`) so XAML and data binding work like MAUI.
- [ ] For each such property, also expose a **direct setter** (e.g. `SetBackgroundColor(...)`) that updates control state without going through the bindable-property pipeline.
- [ ] Bindable property change handlers **must call** the corresponding direct setter (single source of apply logic).
- [ ] Direct setters support a **fluent interface** (return `this` / the control type for method chaining).
- [ ] **Direct setters do not write back to the `BindableProperty`.** Using setters alone can desync the bindable property value from control state. This is intentional for performance when bindings are unused.
- [ ] **Document this clearly** in public XML docs and library docs: prefer bindable properties / XAML when sync and bindings matter; use direct setters when maximizing throughput and you accept possible desync.
- [ ] Support **semi-transactions** via `StartUpdating()` → `EndUpdating()`: after `StartUpdating()`, setting values (via bindable properties or direct setters) must **not** invalidate measure / layout / paint immediately; coalesced invalidation runs when `EndUpdating()` is called. Nested start/end behavior (reentrancy / count) must be defined and documented.

### FR-11 — Clipping and masking

Design details: [DrawingMechanism.md](DrawingMechanism.md).

**Decision (hit-test vs paint):** match common iOS / Android / MAUI control behavior. **Clip / rounded corners / masks constrain painting.** Default **hit-testing uses the arranged layout bounds** (rectangle). A tap in a visually empty rounded corner that is still inside the layout rect **still hits that control** — it does **not** fall through to siblings underneath. Shape-aware hit-testing (path / mask contains-point) is an **optional opt-in** for special controls later, not the v1 default (avoids non-standard complexity).

- [ ] Support **clipping / masking** so painted content is constrained to a shape (rectangle, rounded rectangle, path/mask, and similar), not only the layout bounds.
- [ ] Outside the clip, the control must not paint (those pixels remain transparent / show content underneath) — e.g. a button with rounded corners does not fill the rectangular corner regions outside the round rect.
- [ ] **Default hit-testing uses arranged bounds**, independent of clip/mask paint shape (same as typical UIKit / Android / MAUI buttons).
- [ ] Document that shape-limited hits are **opt-in / future** (e.g. virtual hit-test override), not required for rounded buttons in v1.
- [ ] Clip shape stays consistent across Background / Content / Overlay layers unless a layer explicitly opts out (documented).
- [ ] Clip changes participate in selective invalidation and transparency-aware redraw (NFR-2 / FR-8).
- [ ] Demo or gallery sample: rounded control with visually clear corners that remain within the control’s rectangular hit target.

### FR-12 — Theming via MAUI styles

- [ ] Theme SkiaUi controls with standard **MAUI styles**: `Style` (implicit and explicit), `Setter`s on `BindableProperty`s, resource dictionaries, and `VisualStateManager` / visual states where applicable for control interaction (e.g. Pressed, Disabled).
- [ ] Do **not** invent a parallel SkiaUi-only theme/token system; apps style `SkUi*` types the same way they style MAUI controls.
- [ ] Document sample `Style` resources for common controls in the demo or docs.
- [ ] Direct setters remain available (FR-10); styles and XAML setters go through bindable properties (and thus call direct setters). Document that applying styles does not replace the direct-setter desync note when setters are used afterward.

### FR-13 — Dual-mode: `ISkUiView : IView`

- [ ] **`ISkUiView` derives from MAUI `IView`**; concrete types use **`SkUiView`** / **`SkUiContentView`** / **`SkUiLayout`** so the same type is an `IView` for MAUI and a Skia node in a hosted tree.
- [ ] **Standalone:** the control participates in the MAUI layout / input pipeline via `IView` and our **custom handler**, which creates a SW or GL platform view from **`HwAccelerated`** (FR-14).
- [ ] **Hosted in SkiaUi tree:** when the control is a child of another SkiaUi parent (`SkUiContentView.Content` or `SkUiLayout.Children`), do **not** allocate a MAUI handler or create a platform view for that child; measure / arrange use `IView`, paint / touch use `ISkUiView` on the parent’s shared surface.
- [ ] Document how hosted vs standalone mode is detected and what that means for XAML nesting.

### FR-14 — `HwAccelerated` and custom handler

- [ ] **`SkUiView` does not derive from `SKGLView`**; platform mapping is via a SkiaUi MAUI handler.
- [ ] Non-bindable **`HwAccelerated`** on `SkUiView`: `true` → GL platform view; `false` → software Skia platform view.
- [ ] **Init-only semantics:** value is applied at handler/platform-view creation and **cannot change afterward**. Not a C# `init` property (XAML requires a settable CLR property); document that setting after handler creation is unsupported (define ignore vs exception).
- [ ] Defaults: **`SkUiContentView` / `SkUiLayout` → true**; other controls → **false** (safe for many standalone instances, e.g. collection cells).
- [ ] Apps may opt a leaf control into HW by setting `HwAccelerated = true` **before** the handler is created (e.g. in XAML or before the view is added to the visual tree).
- [ ] Document cost of many HW-accelerated standalone surfaces and recommend composing under one `SkUiContentView` / `SkUiLayout` when possible.

### FR-15 — SkiaUi-owned gestures (not MAUI `GestureRecognizers`)

**Decision:** implement a **shared SkiaUi gesture / event mechanism** on the drawn tree. Do **not** rely on MAUI `View.GestureRecognizers` / `GesturePlatformManager` as the primary path (hosted children have no platform handler — FR-13; long press is not a MAUI recognizer). Design details and implementation checklist: [EventMechanism.md](EventMechanism.md).

- [ ] Classify **single tap**, **double tap**, **long press**, and **swipe** from the root host’s pointer/touch stream and deliver them through the `ISkUiView` tree (same DIP coordinates as Measure / Arrange / Paint / Touch).
- [ ] Implement the mechanism **once** on **`SkUiView`** (events, bindable command properties, participation rules) so every control/layout inherits it — **no per-control gesture state machines**.
- [ ] **Default passive controls** (e.g. `SkUiLabel`): do **not** consume gestures unless the app opts in (subscribe to an event and/or set a command such as `TappedCommand`). Untouched labels must not steal hits from views underneath.
- [ ] **Default active controls** (e.g. `SkUiButton`): **do** handle tap (and related press feedback / visual states) even when the app has not registered an event or command — intrinsic interaction remains correct.
- [ ] Honor **`IView.InputTransparent`**: when `true`, the view is skipped for hit-testing / gesture delivery (pointer passes through to views below). Also honor **`IsEnabled`** (disabled views do not raise gestures; define whether they still block hits — document in EventMechanism.md).
- [ ] Hit-testing for gestures uses **arranged bounds** by default (FR-11); clip/mask does not shrink the hit region unless a future opt-in is used.
- [ ] Standalone and hosted modes use the **same** gesture API and delivery rules; only the root host maps platform input into the tree.
- [ ] Demo / gallery: passive label that becomes tappable via command/event; button that works without app handlers; `InputTransparent` pass-through sample.
- [ ] Public XML docs describe opt-in vs intrinsic handling and that MAUI `GestureRecognizers` are not the SkiaUi gesture API.

## Non-functional requirements

### NFR-1 — Platforms

- Primary: `net10.0-android`, `net10.0-ios`, `net10.0-maccatalyst`
- Secondary (when developing on Windows): `net10.0-windows10.0.19041.0`

### NFR-2 — Performance

- Hot paint / touch paths minimize allocations.
- Redraw only when invalidated; use a continuous render loop (`HasRenderLoop` or equivalent) only while animations (or other continuous scenarios) are active — see FR-7 and open decisions.
- Layout passes should be avoidable when constraints and tree are unchanged.
- **Selective Measure / Arrange:** call into a child `ISkUiView` only when that child (or its constraints / arranged bounds) actually needs work. Unchanged children must not be re-entered on every parent **layout** pass. Layouts re-measure / re-arrange only the dirty subset and reuse **cached measure** results.
- **Paint (v1 — see [DrawingMechanism.md](DrawingMechanism.md)):** on surface invalidate, **clear and repaint the full hosted tree**. Do **not** require per-node retained bitmaps in v1 (memory cost; DrawnUi defaults cache off). Opt-in `SKPicture` / `SKImage` cache may follow when profiling shows need. Selective paint via blit/replay applies only where an opt-in cache exists.
- Invalidation must be granular enough for selective **layout** (per-node dirty flags for size and arrangement). Paint invalidation coalesces to the root surface (FR-10).
- **Transparency:** live paint walk must composite overlapping translucent nodes correctly (FR-8). “Expand dirty region / opaque cover blit” rules apply only if/when retained paint caches or partial-surface updates are introduced — not as a v1 prerequisite.

### NFR-3 — Quality

- Nullable reference types enabled.
- Public API documented with XML comments.
- Small, focused controls; composition via layouts rather than monolithic views.

## Out of scope (for now)

- Full design-system parity with native MAUI controls
- Embedding MAUI views inside the SkiaUi tree
- Blazor Hybrid / multi-project MAUI host
- Software-only fallback host (unless needed when `HwAccelerated` is true but GL is unavailable — then fall back per Acceleration)
- Using MAUI `GestureRecognizers` / `GesturePlatformManager` as the gesture system for SkiaUi trees (optional future compatibility bridge only — see [EventMechanism.md](EventMechanism.md))

## Reference sources

Use these local checkouts when designing or implementing SkiaUi (layout, input, XAML, rendering patterns). Prefer patterns that fit the `SkUiContentView` → `ISkUiView` model; do not copy frameworks wholesale.

| Framework | Local path | Typical relevance |
| --- | --- | --- |
| .NET MAUI | `/Users/rkukla/devel/maui/maui` | **Primary layout model:** `IView` measure/arrange, layout managers, XAML/`ContentProperty`, gesture/handler patterns; control API parity |
| Open-Maui | `/Users/rkukla/devel/maui-linux` | MAUI-compatible stack / Linux and alternate host patterns |
| Flutter | `/Users/rkukla/devel/flutter` | Optional: paint / compositor `Layer` ideas only — **not** the SkiaUi layout contract |
| Avalonia | `/Users/rkukla/devel/Avalonia` | Visual tree, `Border`/`ContentPresenter` composition, Composition visuals for opacity/transform layers |
| Uno Platform | `/Users/rkukla/devel/uno` | Cross-platform control/layout; Border/ContentControl and ControlTemplate part patterns |
| DrawnUi (MAUI) | `/Users/rkukla/devel/maui/drawnui` | Skia-drawn MAUI controls; `SkiaControl` child trees for composed drawn UI |
| SkiaSharp | `/Users/rkukla/devel/maui/SkiaSharp` | SkiaSharp / `SKGLView` APIs, MAUI views, paint surface, GPU hosting |

When borrowing an idea, note the source briefly in design discussion or code comments where non-obvious.

## Open decisions

- Exact method signatures for **`ISkUiView.Paint`** and **raw touch handling** (Skia canvas / paint args; touch event type and return value); Measure/Arrange remain MAUI `IView` APIs (FR-3a). Remaining paint/layer API naming: [DrawingMechanism.md](DrawingMechanism.md). High-level gestures are specified under FR-15 / [EventMechanism.md](EventMechanism.md); remaining open items there include bubbling vs tunneling, multi-touch, and exact public API names.
- Hit-test / touch **capture** and multi-touch details beyond FR-15’s single-pointer gesture set (default hit region remains arranged bounds per FR-11).
- **Animation render loop:** Should a HW-accelerated root host set `HasRenderLoop = true` whenever any descendant has an active animation? If yes, how do we avoid re-measuring **non-animated** children on every frame (layout dirty flags)? Paint may full-walk each animation frame in v1; sibling paint caches only if profiling requires. When the last animation ends, must `HasRenderLoop` turn back off automatically?

## Decided

- **HW acceleration / handler (FR-14):** `SkUiView` does **not** derive from `SKGLView`. A **custom MAUI handler** creates a GL or software Skia platform view from non-bindable **`HwAccelerated`**. Defaults: **`SkUiContentView` and `SkUiLayout` → true**; other controls → **false**, so many standalone instances (e.g. in `CollectionView`) do not each open a GL surface. Hosted children never create a platform view. **`HwAccelerated` is init-only in practice** (frozen at handler creation; not a C# `init` property because of XAML).
- **Class hierarchy:** **`SkUiView`** is the base class (implements `ISkUiView`). **`SkUiContentView : SkUiView`** exposes **`Content`** (`ISkUiView`). **`SkUiLayout : SkUiView`** is the base for all layouts and exposes **`Children`** (`IList<ISkUiView>`). Both Content and multi-child layouts are supported via these two types (not mutually exclusive).
- **`ISkUiView : IView` (FR-1 / FR-13):** `ISkUiView` derives from MAUI `IView`. Measure/Arrange come from `IView`; `ISkUiView` adds **Paint** and **Touch**. Standalone use in the MAUI tree uses our handler + `HwAccelerated`; when hosted under another SkiaUi parent, skip MAUI handler and platform-view allocation. Hosted-vs-standalone detection remains to document.
- **Layout system (FR-3a) — MAUI-based:** measure/arrange follows the **MAUI layout system** (not Flutter box constraints) so SkiaUi can ship drop-in-ish copies of MAUI controls/layouts and **reuse MAUI layout managers**. Multi-pass measure where MAUI requires it is expected; optimize via caching/dirty flags. Flutter is not the layout contract. Hosted vs standalone measure/arrange: [LayoutSystem.md](LayoutSystem.md).
- **Theming (FR-12):** use **MAUI styles** (`Style`, resource dictionaries, `VisualStateManager` as applicable) on `BindableProperty`s — no separate SkiaUi theme system.
- **Properties — BindableProperty + direct setters (FR-10):** use `BindableProperty` for near drop-in MAUI / XAML / binding parity. Also expose fluent direct setters (e.g. `SetBackgroundColor`) that update control state; bindable property changed callbacks **call** those setters. Direct setters **do not** update the `BindableProperty` (intentional desync risk when bypassing bindings) — **must be stated clearly in documentation**. Both paths honor `StartUpdating()` / `EndUpdating()` semi-transactions: defer measure/draw invalidation until `EndUpdating()`.
- **Coordinate system:** same as MAUI — `ISkUiView` sizes, positions, and touch coordinates use MAUI device-independent units (DIPs) and the same density semantics as the host; `SkUiContentView` maps to/from the Skia pixel surface as an implementation detail of the bridge.
- **Public type naming:** `SkUi*` (e.g. `SkUiView`, `SkUiContentView`, `SkUiLayout`, `SkUiGrid`, `SkUiLabel`, `ISkUiView`). Package / project name remains `MauiSkiaUi`.
- **NuGet:** publish **`MauiSkiaUi`** as a NuGet package; **`MauiSkiaUiDemo`** is in-repo only (not published).
- **Drawing layers (FR-9) — Option A:** dedicated layer structure (Background / Content / Overlay paint phases on the control), not nested `ISkUiView` hosts for chrome. Layers need the owning control’s data (e.g. table/grid draws Background **lines** from its own row/column measurements). Nested hosting remains for layout `Children` only. Toolkit notes (Flutter composition vs engine `Layer` tree, Avalonia/Uno templates, DrawnUi child trees) stay relevant as reference for caching/reuse, not as the chosen chrome model. Concrete layer API names — still TBD during implementation. Paint pipeline, clip, and transparency: [DrawingMechanism.md](DrawingMechanism.md).
- **Paint caching (NFR-2) — full-tree redraw in v1:** on surface invalidate, clear and **repaint the full hosted tree**. No default per-node retained bitmaps (memory cost; DrawnUi/Avalonia default cache off; SkiaSharp GL presents do not reliably retain pixels). Selective **measure/arrange** remains required. Opt-in `Picture` / `Image` cache later when profiling shows asymmetric dirty. Details: [DrawingMechanism.md](DrawingMechanism.md).
- **Gestures (FR-15) — SkiaUi-owned:** do **not** use MAUI `GestureRecognizers` as the primary API for the drawn tree. Shared tap / double-tap / long-press / swipe classification and delivery live on **`SkUiView`** for all controls. Passive-by-default (e.g. label) vs intrinsic handlers (e.g. button); honor `InputTransparent`. Details: [EventMechanism.md](EventMechanism.md).
- **Clip vs hit-test (FR-11):** clip/mask/rounded corners affect **paint** only by default. Hit-testing uses **arranged bounds** (iOS / Android / MAUI-like). Shape-aware hit-testing is optional/future opt-in, not v1 default.

## Tracking

When a requirement is completed, check it off here and summarize the delivered behavior in [README.md](README.md).
