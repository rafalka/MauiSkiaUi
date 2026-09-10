# Requirements

Planned work for SkiaUi. Items here are **not yet implemented** unless moved into [README.md](README.md) under *Current implementation*.

## Goals

Build a set of **base controls and layouts drawn with SkiaSharp**, using **GPU / hardware acceleration** where the platform supports it (`SKGLView`), plus a path to **host real MAUI controls** (Entry, Editor, WebView, …) inside the SkiaUi tree when Skia cannot replace them.

**Near drop-in replacement for standard MAUI UI:** a primary goal is to replace a slow MAUI visual tree with a fast SkiaUi tree by providing Skia-drawn copies of MAUI controls and layouts **where painting is enough**. Measure / layout must follow the **MAUI layout system** so existing MAUI layout-manager concepts and algorithms can be reused (see *Layout model*). Controls that require system widgets (text input IME, WebView) are **hosted**, not reimplemented in Skia (FR-16).

MAUI hosts a single accelerated surface for the SkiaUi tree; Skia-drawn nodes paint into that surface. Hosted MAUI controls are **native overlays** positioned to match their arranged slots in the tree (not painted into the Skia canvas).

**XAML-first composition:** the entire SkiaUi tree inside the bridge must be writable in XAML the same way MAUI layouts nest children — no mandatory code-behind to assemble the tree.

Example target markup:

```xml
<VerticalStackLayout>
  <SkUiContentView>
    <SkUiGrid>
      <SkUiLabel Text="Name" />
      <SkUiMauiContentView Grid.Row="0" Grid.Column="1">
        <Entry Placeholder="Type here" />
      </SkUiMauiContentView>
      <SkUiMauiContentView Grid.Row="1" Grid.ColumnSpan="2">
        <WebView Source="https://example.com" />
      </SkUiMauiContentView>
    </SkUiGrid>
  </SkUiContentView>
</VerticalStackLayout>
```

## Architecture

```
MAUI visual tree
└── SkUiContentView : SkUiView          ← typical composed-tree host (HwAccelerated default true)
        ├── platform: Skia surface (GL/SW)
        ├── platform: native overlays for SkUiMauiContentView children (siblings of surface)
        └── Content : ISkUiView
                └── SkUiGrid : SkUiLayout : SkUiView
                        ├── Children[0] : SkUiLabel          ← painted into shared surface
                        └── Children[1] : SkUiMauiContentView   ← placeholder in tree; real Entry/WebView overlaid
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
| **`SkUiMauiContentView`** | Derives from **`SkUiView`**; hosts a MAUI **`VisualElement`** (Entry, Editor, WebView, …) as a native overlay (FR-16) |

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

Layouts are `SkUiLayout` subclasses (hence `ISkUiView` / `IView`) that own **`Children`**. Single-child hosting uses **`SkUiContentView.Content`**. When nested under a SkiaUi parent, a Skia-drawn child must **not** allocate a MAUI handler / platform view — only the outer root host owns the GL/SW Skia surface (FR-13). **Exception:** `SkUiMauiContentView` creates a handler / platform view for its **wrapped** MAUI `VisualElement` and attaches it as an overlay on the standalone root’s platform container (FR-16) — the `SkUiMauiContentView` node itself still has no Skia surface.

Measure, Arrange, and Paint must be **selective**: parents (especially layouts) invoke children only when necessary and reuse cached measure sizes and cached painted bitmaps for unchanged children (see NFR-2 / FR-3).

### MAUI control hosting (`SkUiMauiContentView`)

Some MAUI controls cannot be replaced by Skia drawing alone (system keyboard / IME for `Entry` / `Editor`; real browser engine for `WebView`). SkiaUi does **not** ship custom `SkUiEntry`, `SkUiEditor`, or `SkUiWebView`. Instead:

- **`SkUiMauiContentView : SkUiView`** is an `ISkUiView` placeholder that participates in SkiaUi measure / arrange / z-order like any other child of `SkUiGrid` / `SkUiLayout` / `SkUiContentView`.
- It exposes **`Content`** of type MAUI **`VisualElement`**, marked `[ContentProperty(nameof(Content))]` — e.g. `Entry`, `Editor`, `WebView`, `Picker`, `MediaElement`.
- After arrange, the wrapper creates (if needed) the wrapped element’s **normal MAUI handler**, adds the platform view as a **sibling overlay** of the standalone root’s Skia surface, and syncs frame / transform / visibility / opacity / clip to the placeholder’s arranged bounds (DrawnUi `SkiaMauiElement` pattern).
- **Paint:** the placeholder does not paint the live native control into `SKCanvas` while idle. On Android/Windows during scroll/fling, paints a **snapshot** bitmap instead (Apple: live overlay sync). Touches over a visible native overlay hit the **native** control; SkiaUi gestures (FR-15) do not own that input. See FR-16 / [ScrollingAndCollectionViews.md](ScrollingAndCollectionViews.md#overlays-while-scrolling-fr-16).
- Apps compose mixed trees in XAML: Skia labels/buttons/layouts alongside hosted Entry / Editor / WebView.

### Layout model (MAUI-based)

SkiaUi’s measure / layout pipeline must follow the **.NET MAUI layout system** (measure + arrange / layout managers), **not** Flutter’s box-constraint model. Design details for hosted vs standalone measure/arrange, `MeasureOverride` / `ArrangeOverride`, and layout-manager reuse: [LayoutSystem.md](LayoutSystem.md).

**Rationale:** a main goal is to replace an existing slow MAUI UI tree with a fast SkiaUi tree by shipping Skia-drawn copies of standard MAUI controls and layouts. To **reuse MAUI layout managers** (and stay behavior-compatible with MAUI Grid, Stack, Absolute, etc.), the layout contract must be MAUI-based.

- **Measure:** parents measure children with an available size (and MAUI-equivalent constraint semantics: width/height requests, minimums, expansions as in MAUI `IView` / layout managers). Children return a desired size.
- **Arrange / Layout:** parents assign final bounds to children; alignment, margins, and padding follow MAUI conventions where we claim drop-in parity.
- Multi-pass measure (e.g. star/`*` Grid) is **allowed and expected** where MAUI layout managers require it; optimize with caching and dirty tracking (NFR-2 / FR-3) rather than inventing a different constraint model.
- Prefer reusing or porting MAUI layout-manager logic into SkiaUi layouts over reimplementing Flutter-style constraint solvers.
- Selective invalidation still applies: a dirty subtree should not force unrelated siblings to remeasure when their available size and content are unchanged.

Primary reference: .NET MAUI layout (`Layout`, layout managers, `IView.Measure` / arrange) in [dotnet/maui](https://github.com/dotnet/maui). Flutter remains useful for paint/compositor ideas only — **not** for the layout contract.

### XAML object model

- Concrete control/layout types (e.g. `SkUiGrid`, `SkUiLabel`) are instantiable from XAML (public parameterless constructors).
- Properties that mirror MAUI controls use **`BindableProperty`** for XAML and data-binding parity; performance-oriented **direct setters** are also available (see FR-10 / Decided).
- **Theming** uses standard **MAUI `Style` / `VisualState` / resource dictionaries** applied to those bindable properties (FR-12) — not a separate SkiaUi theme system.
- Layout types derive from **`SkUiLayout`** and expose **`Children`** (`IList<ISkUiView>`) as `[ContentProperty]`.
- **`SkUiContentView`** exposes single **`Content`** (`ISkUiView`) as `[ContentProperty]` for one-child hosting.
- **`SkUiMauiContentView`** exposes **`Content`** (`VisualElement`) as `[ContentProperty(nameof(Content))]` so `<SkUiMauiContentView><Entry …/></SkUiMauiContentView>` works inside SkiaUi layouts (FR-16).
- Attached layout properties (e.g. grid row/column) are supported where the layout requires them.
- XML namespace mapping is documented for apps (clr-namespace / `xmlns`); optional `XmlnsDefinition` for a shorter xmlns.
- Same trees must remain creatable from C# for parity.

### Acceleration

- **`SkUiView` does not subclass `SKGLView`.** A dedicated SkiaUi MAUI **handler** chooses the platform view from **`HwAccelerated`**: GL (`SKGLView`-class) vs software (`SKCanvasView`-class). Value is fixed at handler creation (init-only semantics; settable CLR property for XAML, not C# `init`).
- **Defaults:** `SkUiContentView` and `SkUiLayout` → `HwAccelerated = true`; other `SkUiView` controls (e.g. `SkUiLabel`, buttons) → `HwAccelerated = false`.
- **Rationale:** standalone SkiaUi controls may appear many times on one page (e.g. inside `CollectionView`). Many simultaneous GL surfaces are costly; leaf controls default to software. Composition hosts (`SkUiContentView` / `SkUiLayout`) keep HW on by default so one accelerated surface can draw a whole subtree.
- Hosted Skia-drawn children never create their own Skia surface regardless of `HwAccelerated` (FR-13). `SkUiMauiContentView` creates a handler only for its wrapped MAUI control (FR-16).
- Document fallback if GL is unavailable when `HwAccelerated` is true.

## Functional requirements

### FR-1 — Core bridge and contract

- [x] Define **`ISkUiView : IView`** adding **Paint** and **Touch** handling; Measure/Arrange come from `IView`.
- [x] Implement **`SkUiView`** as the base class implementing `ISkUiView` (shared invalidation, update batching, layers hooks); **not** derived from `SKGLView`.
- [x] Implement **`SkUiContentView : SkUiView`** with **`Content`** (`ISkUiView`) and `[ContentProperty(nameof(Content))]`; default **`HwAccelerated = true`**.
- [x] Implement **`SkUiLayout : SkUiView`** with **`Children`** (`IList<ISkUiView>`) and `[ContentProperty(nameof(Children))]`; all layouts derive from `SkUiLayout`; default **`HwAccelerated = true`**.
- [x] Other controls deriving from `SkUiView` default **`HwAccelerated = false`**.
- [x] Provide a **custom MAUI handler** for `SkUiView` that creates a GL or software Skia platform view based on `HwAccelerated`.
- [x] Wire standalone-host paint / touch / size changes to the tree via `IView` measure/arrange and `ISkUiView` paint/touch.
- [x] Register SkiaSharp / SkiaUi handlers from a library entry point; demo calls it from `MauiProgram`.
- [x] Remove template placeholders (`Class1`, platform stubs) once the public API exists.
- [x] XML docs on all public types and members.

Phase 0 code is implemented; platform compilation is checked, but device-level verification remains open (see README).

### FR-2 — XAML composition

- [x] Entire SkiaUi subtree under `SkUiContentView` can be authored in XAML (nested layouts and controls).
- [x] Layouts use a content-property child collection so markup like `<SkUiGrid><SkUiLabel .../></SkUiGrid>` works.
- [x] Demo (or sample page) shows the target markup pattern with MAUI parents outside and SkiaUi inside the bridge.
- [x] Document the `xmlns` to use for `MauiSkiaUi` types.

### FR-3 — Base layout primitives

- [x] Implement at least one concrete **`SkUiLayout`** subclass suitable for XAML nesting (e.g. `SkUiGrid` or stack) that measures / arranges `Children` and paints / hit-tests them in z-order.
- [x] Clear invalidation rules: property or structure changes request redraw (and remeasure when needed).
- [ ] Layouts dirty-track children so only the affected subset is re-measured, re-laid out, or re-painted; unchanged siblings keep cached measure results and cached painted bitmaps (see NFR-2).

### FR-3a — MAUI-based layout system

Design and checklist: [LayoutSystem.md](LayoutSystem.md).

- [ ] Measure / arrange semantics match **MAUI’s layout system** (available size in, desired size out; arrange assigns final bounds) so SkiaUi layouts can reuse MAUI layout-manager concepts and stay near drop-in compatible.
- [ ] Built-in layouts (stack, grid, etc.) follow MAUI layout behavior (including multi-pass measure where MAUI does, e.g. star rows/columns), optimized with dirty tracking and caching (NFR-2 / FR-3) rather than a Flutter box-constraint pipeline.
- [ ] Changing a child’s offset alone must not require that child to remeasure or repaint when its size and visual content are unchanged.
- [x] Document how SkiaUi layouts map to MAUI layout managers / attached properties (Grid row/column, stack orientation, etc.).
- [x] Do **not** use Flutter `BoxConstraints` / constraints-down–sizes-up as the layout contract; Flutter refs are optional for paint/compositor patterns only.
- [x] **`SkUiView.MeasureOverride` / `ArrangeOverride`** work with **`Handler == null`** (hosted mode); do not rely on `ComputeDesiredSize`’s handler path. Invalidation propagates without a platform handler (see LayoutSystem.md).

### FR-4 — Base controls

- [x] Initial **Skia-drawn** control set (no nested MAUI visuals for these types), including at least `SkUiLabel` (or equivalent text control).
- [x] Do **not** implement custom Skia `SkUiEntry`, `SkUiEditor`, or `SkUiWebView` — use **`SkUiMauiContentView`** hosting instead (FR-16).
- [x] Each Skia-drawn control derives from **`SkUiView`** (implements `ISkUiView`), is XAML-constructible, works under `SkUiContentView` / `SkUiLayout`, and can be used standalone in the MAUI tree (FR-13).

### FR-5 — Demo gallery

- [x] Demo hosts `SkUiContentView` with sample trees defined primarily in XAML.
- [x] Every concrete UI component we create (controls, primitives, composition hosts, and layouts) has its own navigable demo page in `MauiSkiaUiDemo`; adding a component includes adding its page in the same change.
- [x] Each component page provides interactive editors for its meaningful properties, common visibility/enabled/size/opacity settings, and a reset to known defaults. Changes apply immediately without rebuilding the app.
- [x] MAUI control reimplementations show the SkUi component and its native MAUI counterpart with equivalent content, constraints, and shared property values. Previews are side-by-side on wide screens and stacked on narrow screens, remaining usable after resizing or rotation.
- [x] Each page exposes observable behavior (such as independent click counts, scroll offsets, image loading/error status, and arranged bounds) and an explicit property-check action. Property checks are not a substitute for native interaction and visual verification.
- [x] SkUi-only components have a dedicated standalone demo without a misleading native-equivalence claim. Unsupported parity features are documented; comparisons allow platform-native appearance differences.
- [x] Components are organized into four groups — **Basic controls** (leaf, non-layout, non-shape controls such as `SkUiView`, `SkUiLabel`, `SkUiButton`, `SkUiImage`), **Layouts** (composition hosts and multi/single-child layouts such as `SkUiContentView`, `SkUiLayout`, `SkUiGrid`), **Graphics** (drawn shape primitives such as `SkUiBox`, `SkUiEllipse`, `SkUiLine`), and **Scrolling & collections** (`SkUiScrollView` and, later, virtualizing collection view controls). The `MauiSkiaUi` library's source files are organized under `Controls/Basic/`, `Controls/Layouts/`, `Controls/Graphics/`, and `Controls/Scrolling/`; cross-cutting infrastructure lives in root-level `Extensions/` (builder extensions) and `Helpers/` (touch routing, animation clock, frame renderer) folders, with the core contract/base class (`ISkUiView`, `SkUiView`, `SkUiViewHandler`) at the project root (namespace stays `MauiSkiaUi`). The demo gallery lists components under matching section headers in the same order.
- [ ] Gallery navigation, property changes, reset, and responsive comparisons are covered by automated tests where possible and device checks on Android and Apple. Preview and editor state must not leak between pages. *(Automated coverage done via `ComponentDemoTests`; device checks still open.)*
- [ ] Gallery pages for layouts, Skia-drawn controls, and **hosted** Entry / Editor / WebView via `SkUiMauiContentView` (FR-16).
- [ ] Verified on Android and at least one Apple target (iOS or Mac Catalyst).

### FR-6 — Packaging readiness

- [ ] Public types use the **`SkUi*`** naming convention (e.g. `SkUiView`, `SkUiContentView`, `SkUiLayout`, `SkUiGrid`, `SkUiLabel`, `SkUiMauiContentView`); assembly / project / NuGet package id remains **`MauiSkiaUi`**.
- [ ] Prepare **`MauiSkiaUi`** for NuGet publish (package id, versioning, metadata, symbols as needed).
- [ ] **`MauiSkiaUiDemo`** stays **in-repo only** — not published as a NuGet package.

### FR-7 — Animation

Design details and checklist: [AnimationMechanism.md](AnimationMechanism.md).

- [ ] Support property / transform / opacity (and similar) animations on `ISkUiView` nodes, driven so that active animations can sustain **butter-smooth ~60 fps** on target devices with GPU-backed `SKGLView`.
- [ ] Animation clock / ticker integrates with the bridge invalidation model (continuous frames while any animation is active; idle when none are).
- [ ] Animated nodes mark themselves dirty each frame as needed; layouts and the paint path still honor selective Measure / Layout / Paint and cached bitmaps for **non-animated** siblings (NFR-2 / FR-3).
- [ ] Demo or gallery sample shows at least one continuous animation under `SkUiContentView`.

### FR-8 — Transparency

Design details (compositing + cache rules): [DrawingMechanism.md](DrawingMechanism.md).

- [x] Support transparency on SkiaUi nodes: per-node opacity / alpha, and content that is partially or fully transparent (including clear / translucent backgrounds).
- [x] Compositing preserves correct z-order when transparent nodes overlap opaque or other transparent siblings (live paint walk).
- [x] Selective redraw / retained caches (when introduced) must **account for transparency**; v1 full-tree redraw does not need opaque-cover dirty expansion — see [DrawingMechanism.md](DrawingMechanism.md).
- [x] Demo or gallery sample shows overlapping transparent content redrawing correctly.

### FR-9 — Drawing layers (Option A)

Design and checklist: [DrawingMechanism.md](DrawingMechanism.md).

**Decision:** use a **dedicated layer structure** on the control (named slots / paint phases such as Background, Content, Overlay) — **not** nesting separate `ISkUiView` hosts for each chrome layer (Option B rejected for this purpose).

**Rationale:** layers often need **data owned by the control**. Example: a table / grid draws grid **lines on the Background layer** using that grid’s own row and column measurements; a nested “background child view” would not naturally own those metrics without awkward coupling or duplication.

- [x] Support splitting a control’s drawing into ordered **layers** (Background / Content / Overlay, extensible as needed), e.g. text with background fill, glyphs, and badge/focus overlay.
- [x] Layers are paint (and optional cache) phases of the **same** `ISkUiView`, with direct access to that control’s layout and state — not a separate child layout tree per layer.
- [x] Layers participate in selective Paint when opt-in caches exist (NFR-2 / FR-8); v1 may repaint all phases on each node paint with no per-layer bitmap cache — see [DrawingMechanism.md](DrawingMechanism.md).
- [x] Favor **reusable drawing helpers / primitives** across controls (e.g. shared rounded-rectangle / border / fill used by many Background layers — not copy-pasted Skia paths per control). Reuse is via shared paint utilities, interfaces, or layer implementations (NFR-4), not by requiring every chrome piece to be its own `ISkUiView`. `SkUiChrome` is shared by Button's fill/border and its content clip.
- [x] Hit-testing remains **view-level** (arranged bounds); overlays do not get separate hit geometry in v1 (FR-11 / [EventMechanism.md](EventMechanism.md)).
- [x] Apply this model consistently to built-in controls; keep Option B-style nesting for true **child content** in layouts only (`Children`), not for a control’s own chrome layers.

### FR-10 — Bindable properties, direct setters, and update batching

Goal: stay a near **drop-in replacement** for standard MAUI controls (XAML + bindings) while offering a faster path when bindings are not needed.

- [x] Public stylable / bindable API surface uses **`BindableProperty`** (e.g. `BackgroundColor`) so XAML and data binding work like MAUI.
- [x] For each such property, also expose a **direct setter** (e.g. `SetBackgroundColor(...)`) that updates control state without going through the bindable-property pipeline.
- [x] Bindable property change handlers **must call** the corresponding direct setter (single source of apply logic).
- [x] Direct setters support a **fluent interface** (return `this` / the control type for method chaining).
- [x] **Direct setters do not write back to the `BindableProperty`.** Using setters alone can desync the bindable property value from control state. This is intentional for performance when bindings are unused.
- [x] **Document this clearly** in public XML docs and library docs: prefer bindable properties / XAML when sync and bindings matter; use direct setters when maximizing throughput and you accept possible desync.
- [x] Support **semi-transactions** via `StartUpdating()` → `EndUpdating()`: after `StartUpdating()`, setting values (via bindable properties or direct setters) must **not** invalidate measure / layout / paint immediately; coalesced invalidation runs when `EndUpdating()` is called. Nested start/end behavior (reentrancy / count) must be defined and documented.

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

- [x] Theme SkiaUi controls with standard **MAUI styles**: `Style` (implicit and explicit), `Setter`s on `BindableProperty`s, resource dictionaries, and `VisualStateManager` / visual states where applicable for control interaction (e.g. Pressed, Disabled).
- [x] Do **not** invent a parallel SkiaUi-only theme/token system; apps style `SkUi*` types the same way they style MAUI controls.
- [x] Document sample `Style` resources for common controls in the demo or docs.
- [x] Direct setters remain available (FR-10); styles and XAML setters go through bindable properties (and thus call direct setters). Document that applying styles does not replace the direct-setter desync note when setters are used afterward.

### FR-13 — Dual-mode: `ISkUiView : IView`

- [x] **`ISkUiView` derives from MAUI `IView`**; concrete types use **`SkUiView`** / **`SkUiContentView`** / **`SkUiLayout`** so the same type is an `IView` for MAUI and a Skia node in a hosted tree.
- [x] **Standalone:** the control participates in the MAUI layout / input pipeline via `IView` and our **custom handler**, which creates a SW or GL platform view from **`HwAccelerated`** (FR-14).
- [x] **Hosted in SkiaUi tree:** when a **Skia-drawn** control is a child of another SkiaUi parent (`SkUiContentView.Content` or `SkUiLayout.Children`), do **not** allocate a MAUI handler or create a Skia platform view for that child; measure / arrange use `IView`, paint / touch use `ISkUiView` on the parent’s shared surface.
- [x] **`SkUiMauiContentView` exception:** when hosted, the wrapper still has no Skia surface of its own, but **must** create/manage the wrapped MAUI control’s handler and native overlay (FR-16). Implemented via the root handler's `SkUiOverlayContainer` and `FindRoot`/`AttachOverlay`/`UpdateOverlayBounds`; see FR-16 for the current v1 limits (no rotation/scale/opacity composition, no snapshot-during-scroll).
- [x] Document how hosted vs standalone mode is detected and what that means for XAML nesting.

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
- [ ] Document that input over **`SkUiMauiContentView`** overlays is handled by the **native MAUI control**, not by FR-15 (hit-test / gestures skip or defer to the overlay region).

### FR-16 — Host MAUI controls (`SkUiMauiContentView`)

**Decision:** do **not** build custom Skia editors or a Skia WebView. Host real MAUI **`Entry`**, **`Editor`**, **`WebView`**, and other `VisualElement`s that need system components via a dedicated SkiaUi wrapper (DrawnUi `SkiaMauiElement` / Avalonia `NativeControlHost` / Flutter platform-view overlay pattern).

**Rationale:** IME, accessibility, and browser engines are platform services; reimplementing them in Skia is high cost and lower quality. Overlay hosting keeps SkiaUi layouts fast while allowing drop-in use of existing MAUI controls inside `SkUiGrid` / other layouts.

- [x] Implement **`SkUiMauiContentView : SkUiView`** (`ISkUiView`) with **`Content`** (`VisualElement`) marked `[ContentProperty(nameof(Content))]`.
- [x] XAML works as a child of `SkUiLayout` / `SkUiContentView`, e.g. `<SkUiMauiContentView><Entry …/></SkUiMauiContentView>` and `<SkUiMauiContentView><WebView …/></SkUiMauiContentView>`; attached layout properties (`Grid.Row`, etc.) apply to the **`SkUiMauiContentView`**.
- [x] **Measure / arrange:** placeholder participates in the SkiaUi MAUI-based layout pipeline (FR-3a); `MeasureContent`/`ArrangeContent` delegate straight to the wrapped `VisualElement`'s own `Measure`/`Arrange`. A handlerless `VisualElement` (not yet attached) measures as `Size.Zero`, matching plain MAUI `IView.Measure` behavior without a handler — this is a MAUI platform limitation, not a SkiaUi one.
- [x] **Native overlay:** create the wrapped element’s MAUI handler; add its platform view as a **sibling** of the standalone root’s Skia surface inside a small native container (`SkUiOverlayContainer`, one implementation per platform) that the root handler now returns instead of the bare Skia surface. Position/size sync to the placeholder’s root-relative arranged bounds, including this node's and every ancestor's `TranslationX`/`TranslationY` (`ComputeRootRelativeFrame`). **Not yet synced: rotation, scale, opacity, and clip** on this node or any ancestor between here and the root.
- [x] **Lifecycle:** the standalone root's handler notifies `SkUiMauiContentView` descendants (via a `SkiaChildren` tree walk) on connect/disconnect; attach/detach also runs on `OnParentSet`/content replacement for dynamic insertion into an already-connected tree. BindingContext propagates through the existing `AddLogicalChild`/`RemoveLogicalChild` ownership already used for hosted children.
- [ ] **Paint:** does not draw the live native control into `SKCanvas` (correct per spec). **Snapshot-during-scroll is not implemented** — the overlay stays live-synced on all platforms during scroll/fling in v1; document this gap until measured as a problem.
- [x] **Input:** `SkUiMauiContentView.Touch` always returns `false`, so SkiaUi's touch router never consumes hits in this region; the native view receives real platform input directly.
- [ ] **Z-order / clipping:** overlays are added after the Skia surface (so they paint on top); clipping the native view to parent bounds (e.g. for a WebView inside a smaller container) is not implemented.
- [x] **Scope for v1 demos:** `MauiContentViewDemoPage` hosts both **`Editor`** and **`WebView`** (switchable) under a `SkUiContentView` in the gallery.
- [x] Explicitly **out of product scope:** `SkUiEntry`, `SkUiEditor`, `SkUiWebView` (and similar full Skia reimplementations of those controls).
- [x] Document cost: each hosted control is a real platform view; prefer few overlays, not one per collection cell, unless measured acceptable.
- [x] XML docs on `SkUiMauiContentView` describing overlay model, gesture boundary vs FR-15, and the v1 limits above.
- [ ] Primary references: DrawnUi `SkiaMauiElement`; MAUI handlers for Entry / Editor / WebView; Flutter platform views / Avalonia `NativeControlHost` for composition tradeoffs.

### FR-17 — Scrolling and collection views

Design details and checklist: [ScrollingAndCollectionViews.md](ScrollingAndCollectionViews.md).

**Decision:** implement **SkiaUi-owned** scroll and (later) virtualized lists on the shared surface. Do **not** treat MAUI `ScrollView` / `CollectionView` as the primary host for scrollable SkiaUi trees.

- [x] Implement **`SkUiScrollView`** with `Content` (`ISkUiView`), orientation, viewport clip, offset, pan/fling (FR-15 capture + FR-7 fling animation), and scroll APIs/events.
- [x] Measure content for full extent on the scroll axis; offset-only changes must not force content remeasure (FR-3a / NFR-2).
- [ ] Sync **`SkUiMauiContentView`** overlays while scrolling; on Android/Windows apply FR-16 snapshot freeze (Apple live sync); honor opt-out.
- [ ] Demo gallery: long content under `SkUiScrollView` (Skia-drawn + hosted Entry).
- [ ] **Later:** virtualizing **`SkUiCollectionView`** (or equivalent) with `ItemsSource` / `ItemTemplate` and recycle pool on the shared surface — not MAUI `CollectionView` recycling.
- [x] Document MAUI `ScrollView`/`CollectionView` nesting as **compat only** (standalone cells keep `HwAccelerated = false` per FR-14).

Phase 1 code and headless tests are delivered. Device interaction/rendering/contrast acceptance remains blocked by the installed MAUI extension; checked implementation items do not imply native platform verification. See README for the precise v1 API limits.

## Non-functional requirements

### NFR-1 — Platforms

- Primary: `net10.0-android`, `net10.0-ios`, `net10.0-maccatalyst`
- Secondary (when developing on Windows): `net10.0-windows10.0.19041.0`

### NFR-2 — Performance

- Hot paint / touch / animation paths must be **as fast as possible**, especially while animating (~60 fps budget — FR-7 / [AnimationMechanism.md](AnimationMechanism.md)).
- **Zero / near-zero allocation goal** on critical paths (measure, arrange, paint, touch, animator tick): avoid per-frame heap allocations (no LINQ/boxing/`string` work in the hot loop; reuse buffers; prefer structs / `span` / pooling).
- Use **modern C# / .NET** techniques where they help throughput or GC pressure, including (when justified and safe) **`unsafe`**, pointers, `ref`/`ref struct`, `Span<T>` / `Memory<T>`, stackalloc, AggressiveInlining, and similar — constrained to well-reviewed hot paths.
- Redraw only when invalidated; use a continuous render loop (`HasRenderLoop` or equivalent) only while animations (or other continuous scenarios) are active — see FR-7 and open decisions.
- Layout passes should be avoidable when constraints and tree are unchanged.
- **Selective Measure / Arrange:** call into a child `ISkUiView` only when that child (or its constraints / arranged bounds) actually needs work. Unchanged children must not be re-entered on every parent **layout** pass. Layouts re-measure / re-arrange only the dirty subset and reuse **cached measure** results.
- **Paint (v1 — see [DrawingMechanism.md](DrawingMechanism.md)):** on surface invalidate, **clear and repaint the full hosted tree**. Do **not** require per-node retained bitmaps in v1 (memory cost; DrawnUi defaults cache off). Opt-in `SKPicture` / `SKImage` cache may follow when profiling shows need. Selective paint via blit/replay applies only where an opt-in cache exists.
- Invalidation must be granular enough for selective **layout** (per-node dirty flags for size and arrangement). Paint invalidation coalesces to the root surface (FR-10).
- **Transparency:** live paint walk must composite overlapping translucent nodes correctly (FR-8). “Expand dirty region / opaque cover blit” rules apply only if/when retained paint caches or partial-surface updates are introduced — not as a v1 prerequisite.

**Implementation approach (correctness first, then max performance):**

1. **Initial implementation** of a feature or critical path should use **simple, easy-to-understand** code so behavior is obvious and reviewable.
2. Once **tests confirm** correctness, **optimize** that path toward **maximum performance** and **minimum memory allocation** (including `unsafe` / low-level techniques where profiling shows benefit).
3. Do not ship clever/unsafe micro-optimizations before the simple version is covered by tests; keep optimized code as readable as practical and document non-obvious invariants.

### NFR-3 — Quality

- Nullable reference types enabled.
- Public API documented with XML comments (see also NFR-5).
- Small, focused controls; composition via layouts rather than monolithic views.
- Testing strategy (unit, mechanism, golden/visual, device): [Testing.md](Testing.md).

### NFR-4 — Flexibility and reuse

Design controls and shared infrastructure so they stay **easy to extend** and **hard to copy-paste**:

- Prefer **virtual methods** (and protected overridable hooks) on `SkUiView` / layouts / layers so apps and derived controls can customize measure, arrange, paint phases, hit-testing, and similar without forking the type.
- Prefer **interfaces** for pluggable behavior (e.g. background painters, animators, easing, layout managers, gesture participation) instead of sealing logic into concrete-only classes when multiple implementations are expected.
- Extract **common parts** into reusable helpers or types — especially **background drawing** (rounded rect / border / fill), **animation** clock/animators (FR-7), layers (FR-9), clip helpers (FR-11), and similar chrome — so controls compose shared code rather than duplicating Skia paths and state machines.
- Keep extension points documented (which methods/interfaces to override or implement) and stable enough for library consumers building custom `SkUi*` controls.
- Balance with NFR-2: shared abstractions may start simple; after tests pass, optimize hot shared paths without removing the extension model.

### NFR-5 — Documentation

Ship **well-documented** library code and user-facing docs:

- **Code:** XML comments on all public types and members. Add **inline comments for non-obvious** logic (invariants, performance tricks, `unsafe` blocks, hosted-vs-standalone quirks, intentional BindableProperty desync — FR-10). Do not comment the trivial.
- **Per-control markdown:** each public control / layout (e.g. `SkUiLabel`, `SkUiGrid`, `SkUiScrollView`, `SkUiMauiContentView`) has its own **`.md`** file describing how it works and how to use it (XAML / C# samples, key properties, gestures, theming). Keep an index link from [README.md](README.md) (or a `docs/` / `docs/controls/` folder — choose one layout and stick to it).
- **MAUI reimplementations:** when a `SkUi*` type is a near drop-in for a standard MAUI control or layout, **do not** restate the full MAUI feature encyclopedia. Prefer a short overview, then **link to the official MAUI documentation** for baseline behavior. **Must document SkiaUi differences and extensions** (API deltas, unsupported MAUI features, direct setters, layers, `HwAccelerated`, gesture model, performance notes, etc.).
- **SkiaUi-specific / infrastructure types** (`SkUiView`, `SkUiContentView`, `SkUiLayout`, `SkUiMauiContentView`, mechanisms): fuller how-it-works docs are expected (may point at [LayoutSystem.md](LayoutSystem.md), [DrawingMechanism.md](DrawingMechanism.md), etc. for shared pipelines).
- Keep control docs in sync when behavior or public API changes.

## Out of scope (for now)

- Full design-system parity with native MAUI controls for **Skia-drawn** copies (hosted MAUI Entry/Editor/WebView keep their native look via FR-16)
- Custom Skia reimplementations of **Entry**, **Editor**, **WebView** (and similar IME/browser controls) — use `SkUiMauiContentView` instead (FR-16)
- Blazor Hybrid / multi-project MAUI host
- Software-only fallback host (unless needed when `HwAccelerated` is true but GL is unavailable — then fall back per Acceleration)
- Using MAUI `GestureRecognizers` / `GesturePlatformManager` as the gesture system for SkiaUi trees (optional future compatibility bridge only — see [EventMechanism.md](EventMechanism.md))

## Reference sources

Use these public repositories when designing or implementing SkiaUi (layout, input, XAML, rendering patterns). Prefer patterns that fit the `SkUiContentView` → `ISkUiView` model; do not copy frameworks wholesale.

| Framework | URL | Typical relevance |
| --- | --- | --- |
| .NET MAUI | https://github.com/dotnet/maui | **Primary layout model:** `IView` measure/arrange, layout managers, XAML/`ContentProperty`, gesture/handler patterns; control API parity |
| Open-Maui | https://github.com/open-maui/maui-linux | MAUI-compatible stack / Linux and alternate host patterns |
| Flutter | https://github.com/flutter/flutter | Optional: paint / compositor `Layer` ideas only — **not** the SkiaUi layout contract |
| Avalonia | https://github.com/AvaloniaUI/Avalonia | Visual tree, `Border`/`ContentPresenter` composition, Composition visuals for opacity/transform layers |
| Uno Platform | https://github.com/unoplatform/uno | Cross-platform control/layout; Border/ContentControl and ControlTemplate part patterns |
| DrawnUi (MAUI) | https://github.com/taublast/drawnui | Skia-drawn MAUI controls; **`SkiaMauiElement`** native overlay + snapshot pattern (FR-16); `SkiaControl` child trees |
| SkiaSharp | https://github.com/mono/SkiaSharp | SkiaSharp / `SKGLView` APIs, MAUI views, paint surface, GPU hosting |

When borrowing an idea, note the source briefly in design discussion or code comments where non-obvious.

## Open decisions

- Exact method signatures for **`ISkUiView.Paint`** and **raw touch handling** (Skia canvas / paint args; touch event type and return value); Measure/Arrange remain MAUI `IView` APIs (FR-3a). Remaining paint/layer API naming: [DrawingMechanism.md](DrawingMechanism.md). High-level gestures are specified under FR-15 / [EventMechanism.md](EventMechanism.md); remaining open items there include bubbling vs tunneling, multi-touch, and exact public API names.
- Hit-test / touch **capture** and multi-touch details beyond FR-15’s single-pointer gesture set (default hit region remains arranged bounds per FR-11). Scroll needs capture for pan — see [ScrollingAndCollectionViews.md](ScrollingAndCollectionViews.md).
- Exact public names for animation helpers / `ISkUiAnimator` (tier APIs sketched in [AnimationMechanism.md](AnimationMechanism.md)).
- Scroll v1 details still open in [ScrollingAndCollectionViews.md](ScrollingAndCollectionViews.md): overscroll (clamp vs bounce), both-axes in v1, nested scroll rules, collection-as-scroll vs outer `SkUiScrollView` extent provider; snapshot opt-out property name.

## Decided

- **HW acceleration / handler (FR-14):** `SkUiView` does **not** derive from `SKGLView`. A **custom MAUI handler** creates a GL or software Skia platform view from non-bindable **`HwAccelerated`**. Defaults: **`SkUiContentView` and `SkUiLayout` → true**; other controls → **false**, so many standalone instances (e.g. in `CollectionView`) do not each open a GL surface. Hosted children never create a platform view. **`HwAccelerated` is init-only in practice** (frozen at handler creation; not a C# `init` property because of XAML).
- **Class hierarchy:** **`SkUiView`** is the base class (implements `ISkUiView`). **`SkUiContentView : SkUiView`** exposes **`Content`** (`ISkUiView`). **`SkUiLayout : SkUiView`** is the base for all layouts and exposes **`Children`** (`IList<ISkUiView>`). Both Content and multi-child layouts are supported via these two types (not mutually exclusive).
- **`ISkUiView : IView` (FR-1 / FR-13):** `ISkUiView` derives from MAUI `IView`. Measure/Arrange come from `IView`; `ISkUiView` adds **Paint** and **Touch**. Standalone use in the MAUI tree uses our handler + `HwAccelerated`; when hosted under another SkiaUi parent, skip MAUI handler and platform-view allocation. Hosted-vs-standalone detection remains to document.
- **Layout system (FR-3a) — MAUI-based:** measure/arrange follows the **MAUI layout system** (not Flutter box constraints) so SkiaUi can ship drop-in-ish copies of MAUI controls/layouts and **reuse MAUI layout managers**. Multi-pass measure where MAUI requires it is expected; optimize via caching/dirty flags. Flutter is not the layout contract. Hosted vs standalone measure/arrange: [LayoutSystem.md](LayoutSystem.md).
- **Theming (FR-12):** use **MAUI styles** (`Style`, resource dictionaries, `VisualStateManager` as applicable) on `BindableProperty`s — no separate SkiaUi theme system.
- **Properties — BindableProperty + direct setters (FR-10):** use `BindableProperty` for near drop-in MAUI / XAML / binding parity. Also expose fluent direct setters (e.g. `SetBackgroundColor`) that update control state; bindable property changed callbacks **call** those setters. Direct setters **do not** update the `BindableProperty` (intentional desync risk when bypassing bindings) — **must be stated clearly in documentation**. Both paths honor `StartUpdating()` / `EndUpdating()` semi-transactions: defer measure/draw invalidation until `EndUpdating()`.
- **Coordinate system:** same as MAUI — `ISkUiView` sizes, positions, and touch coordinates use MAUI device-independent units (DIPs) and the same density semantics as the host; `SkUiContentView` maps to/from the Skia pixel surface as an implementation detail of the bridge.
- **Public type naming:** `SkUi*` (e.g. `SkUiView`, `SkUiContentView`, `SkUiLayout`, `SkUiGrid`, `SkUiLabel`, `SkUiMauiContentView`, `ISkUiView`). Package / project name remains `MauiSkiaUi`.
- **NuGet:** publish **`MauiSkiaUi`** as a NuGet package; **`MauiSkiaUiDemo`** is in-repo only (not published).
- **Drawing layers (FR-9) — Option A:** dedicated layer structure (Background / Content / Overlay paint phases on the control), not nested `ISkUiView` hosts for chrome. Layers need the owning control’s data (e.g. table/grid draws Background **lines** from its own row/column measurements). Nested hosting remains for layout `Children` only. Toolkit notes (Flutter composition vs engine `Layer` tree, Avalonia/Uno templates, DrawnUi child trees) stay relevant as reference for caching/reuse, not as the chosen chrome model. Concrete layer API names — still TBD during implementation. Paint pipeline, clip, and transparency: [DrawingMechanism.md](DrawingMechanism.md).
- **Code performance (NFR-2):** critical paths (especially animation tick + paint) aim for **maximum speed** and **zero / near-zero allocations**, using modern C#/.NET techniques including **`unsafe`** where justified. **Correctness-first workflow:** ship simple, readable code first; after tests confirm behavior, optimize for max performance and min allocations.
- **Flexibility and reuse (NFR-4):** extensible controls via **virtual hooks** and **interfaces**; shared building blocks for background drawing, animations, layers, and similar — avoid copy-paste chrome.
- **Documentation (NFR-5):** XML + comments for non-obvious code; **one `.md` per control** (how it works / how to use). MAUI reimplementations: link to official MAUI docs for baseline behavior; document **differences and extensions** only (do not duplicate full MAUI manuals).
- **Paint caching (NFR-2) — full-tree redraw in v1:** on surface invalidate, clear and **repaint the full hosted tree**. No default per-node retained bitmaps (memory cost; DrawnUi/Avalonia default cache off; SkiaSharp GL presents do not reliably retain pixels). Selective **measure/arrange** remains required. Opt-in `Picture` / `Image` cache later when profiling shows asymmetric dirty. Details: [DrawingMechanism.md](DrawingMechanism.md).
- **Gestures (FR-15) — SkiaUi-owned:** do **not** use MAUI `GestureRecognizers` as the primary API for the drawn tree. Shared tap / double-tap / long-press / swipe classification and delivery live on **`SkUiView`** for all controls. Passive-by-default (e.g. label) vs intrinsic handlers (e.g. button); honor `InputTransparent`. Details: [EventMechanism.md](EventMechanism.md).
- **Clip vs hit-test (FR-11):** clip/mask/rounded corners affect **paint** only by default. Hit-testing uses **arranged bounds** (iOS / Android / MAUI-like). Shape-aware hit-testing is optional/future opt-in, not v1 default.
- **Animation (FR-7):** vsync-driven root registry (`HasRenderLoop` while active); **time-based** progress; v1 = paint + **render transforms** (no layout dirty); layout animation optional/later. Details: [AnimationMechanism.md](AnimationMechanism.md).
- **MAUI control hosting (FR-16):** no custom Skia `SkUiEntry` / `SkUiEditor` / `SkUiWebView`. Host real MAUI `Entry`, `Editor`, `WebView`, and other `VisualElement`s via **`SkUiMauiContentView`**: `ISkUiView` placeholder in the SkiaUi tree; native platform view overlaid on the standalone root’s container and synced to arranged bounds (DrawnUi `SkiaMauiElement` pattern). Content property is **`Content`** (`VisualElement`, `[ContentProperty]`). Input stays with the native control; FR-15 does not own overlay hits.
- **Snapshot-during-scroll (FR-16 / FR-17):** while an ancestor scroll/fling is active, **Android and Windows** use **snapshot freeze** by default (hide native overlay, paint bitmap on Skia until motion settles); **Apple** uses **live sync only**. Apps may **opt out** per overlay for special cases (e.g. keep WebView live). Details: [ScrollingAndCollectionViews.md](ScrollingAndCollectionViews.md#overlays-while-scrolling-fr-16).
- **Scrolling / collections (FR-17):** implement **`SkUiScrollView`** (and later a virtualizing collection) **inside** the SkiaUi tree on the shared surface. Do **not** use MAUI `ScrollView` / `CollectionView` as the primary composition model for scrollable SkiaUi UI. Nesting standalone `SkUi*` under MAUI scrollers remains a documented compat path only. Details: [ScrollingAndCollectionViews.md](ScrollingAndCollectionViews.md).

## Tracking

When a requirement is completed, check it off here and summarize the delivered behavior in [README.md](README.md).

Delivery order and phase exit criteria: **[ImplementationPlan.md](ImplementationPlan.md)** (Phase 0 PoC → Phase 1 initial controls → Phase 2 gallery/docs → Phase 3 extensions).
