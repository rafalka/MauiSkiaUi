# Requirements

Requirements for SkiaUi. Checked items (`- [x]`) are implemented; unchecked items are open. Delivered behavior is summarized in [Development.md](../../Development.md) under *Current implementation*.

**Core layer (low-level, no MAUI Controls):** see **[CoreRequirements.md](CoreRequirements.md)** — composition substrate for complex controls / dense trees; fluent + INPC; shared paint/measure via Core delegates. This file remains the MAUI-compatible / XAML-first contract.

## Goals

Build a set of **base controls and layouts drawn with SkiaSharp**, using **GPU / hardware acceleration** where the platform supports it (Metal on iOS / Mac Catalyst, a GL thread on Android — [RenderingPipeline.md](RenderingPipeline.md)), plus a path to **host real MAUI controls** (Entry, Editor, WebView, …) inside the SkiaUi tree when Skia cannot replace them.

**Near drop-in replacement for standard MAUI UI:** a primary goal is to replace a slow MAUI visual tree with a fast SkiaUi tree by providing Skia-drawn copies of MAUI controls and layouts **where painting is enough**. Measure / layout must follow the **MAUI layout system** so existing MAUI layout-manager concepts and algorithms can be reused (see *Layout model*). Controls that require a platform engine (WebView, maps, media) are **hosted**, not reimplemented in Skia (FR-16); text input is drawn and talks to the platform keyboard / IME through a proxy (FR-16, Phase T).

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

Some MAUI controls cannot be replaced by Skia drawing alone (a real browser engine for `WebView`; maps, media). SkiaUi does **not** ship a `SkUiWebView`. `Entry` / `Editor` get drawn equivalents (`SkUiEntry`, `SkUiEditor`) whose keyboard and IME input goes through a platform proxy (FR-16, planned as Phase T); until then, and for native-only needs, they are hosted too. Hosting:

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
- **Colors and property appearance (per control):** use standard **MAUI `Style` / `VisualState` / resource dictionaries** on bindable properties (**FR-12**) to override individual control values.
- **Color scheme** (shared default palette — accent, backgrounds, muted/disabled, …) is a SkiaUi mechanism (**FR-19** / [ColorScheme.md](ColorScheme.md)) used as defaults for Core and MAUI-compatible controls. Distinct from FR-12 and from MAUI `AppTheme`.
- **Control look** (default geometry / Skia drawing **and**, where applicable, default width/height) is a separate SkiaUi mechanism (**FR-18** / [ControlLook.md](ControlLook.md)). Do not call look or scheme “MAUI theming.”
- Layout types derive from **`SkUiLayout`** and expose **`Children`** (`IList<ISkUiView>`) as `[ContentProperty]`.
- **`SkUiContentView`** exposes single **`Content`** (`ISkUiView`) as `[ContentProperty]` for one-child hosting.
- **`SkUiMauiContentView`** exposes **`Content`** (`VisualElement`) as `[ContentProperty(nameof(Content))]` so `<SkUiMauiContentView><Entry …/></SkUiMauiContentView>` works inside SkiaUi layouts (FR-16).
- Attached layout properties (e.g. grid row/column) are supported where the layout requires them.
- XML namespace mapping is documented for apps (clr-namespace / `xmlns`); optional `XmlnsDefinition` for a shorter xmlns.
- Same trees must remain creatable from C# for parity.

### Acceleration

- **`SkUiView` does not subclass `SKGLView`.** A dedicated SkiaUi MAUI **handler** chooses the platform view from **`HwAccelerated`**: GPU (Metal on Apple, `GLTextureView` on Android, `SKGLView` on Windows) vs software (`SKCanvasView`). Value is fixed at handler creation (init-only semantics; settable CLR property for XAML, not C# `init`).
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

Core pipeline code is implemented and headless-tested. Device checks on Android (Galaxy S9), iOS and Windows 11 are recorded in [EventMechanism.md](EventMechanism.md#verification) and [ScrollingAndCollectionViews.md](ScrollingAndCollectionViews.md#implemented); memory-leak scenarios also run on devices ([Testing.md](Testing.md)).

### FR-2 — XAML composition

- [x] Entire SkiaUi subtree under `SkUiContentView` can be authored in XAML (nested layouts and controls).
- [x] Layouts use a content-property child collection so markup like `<SkUiGrid><SkUiLabel .../></SkUiGrid>` works.
- [x] Demo (or sample page) shows the target markup pattern with MAUI parents outside and SkiaUi inside the bridge.
- [x] Document the `xmlns` to use for `MauiSkiaUi` types.

### FR-3 — Base layout primitives

- [x] Implement at least one concrete **`SkUiLayout`** subclass suitable for XAML nesting (e.g. `SkUiGrid` or stack) that measures / arranges `Children` and paints / hit-tests them in z-order.
- [x] Clear invalidation rules: property or structure changes request redraw (and remeasure when needed).
- [x] Layouts dirty-track children so only the affected subset is re-measured, re-laid out, or re-recorded; unchanged siblings keep cached measure results and their retained pictures (see NFR-2 / [RenderingPipeline.md](RenderingPipeline.md)).

### FR-3a — MAUI-based layout system

Design and checklist: [LayoutSystem.md](LayoutSystem.md).

- [x] Measure / arrange semantics match **MAUI’s layout system** (available size in, desired size out; arrange assigns final bounds) so SkiaUi layouts can reuse MAUI layout-manager concepts and stay near drop-in compatible.
- [x] Built-in layouts (stack, grid, etc.) follow MAUI layout behavior (including multi-pass measure where MAUI does, e.g. star rows/columns), optimized with dirty tracking and caching (NFR-2 / FR-3) rather than a Flutter box-constraint pipeline.
- [x] Changing a child’s offset alone must not require that child to remeasure or repaint when its size and visual content are unchanged.
- [x] Document how SkiaUi layouts map to MAUI layout managers / attached properties (Grid row/column, stack orientation, etc.).
- [x] Do **not** use Flutter `BoxConstraints` / constraints-down–sizes-up as the layout contract; Flutter refs are optional for paint/compositor patterns only.
- [x] **`SkUiView.MeasureOverride` / `ArrangeOverride`** work with **`Handler == null`** (hosted mode); do not rely on `ComputeDesiredSize`’s handler path. Invalidation propagates without a platform handler (see LayoutSystem.md).

### FR-4 — Base controls

- [x] Initial **Skia-drawn** control set (no nested MAUI visuals for these types), including at least `SkUiLabel` (or equivalent text control).
- [x] Do **not** implement a custom Skia `SkUiWebView` — use **`SkUiMauiContentView`** hosting instead (FR-16).
- [ ] Drawn `SkUiEntry` / `SkUiEditor` (and Core twins) with an IME proxy (FR-16, Phase T).
- [x] Each Skia-drawn control derives from **`SkUiView`** (implements `ISkUiView`), is XAML-constructible, works under `SkUiContentView` / `SkUiLayout`, and can be used standalone in the MAUI tree (FR-13).
- [x] Labels (both layers) have MAUI Label's text properties (`MaxLines`, `LineHeight`, `CharacterSpacing`, `TextDecorations`, `TextTransform`) and custom line breaking (`SkUiTextLineBreaker`), so text can get shorter in its own way (fewer decimals, another ellipsis) instead of the stock ellipsis.
- [x] Labels (both layers) draw optional rounded chrome — per-corner radii, border, fill — so badges, chips, tags and tabs need no wrapping border node; buttons inherit it and keep MAUI's uniform `CornerRadius`.

### FR-5 — Demo gallery

- [x] Demo hosts `SkUiContentView` with sample trees defined primarily in XAML.
- [x] Every concrete UI component we create (controls, primitives, composition hosts, and layouts) has its own navigable demo page in `MauiSkiaUiDemo`; adding a component includes adding its page in the same change.
- [x] Each component page provides interactive editors for its meaningful properties, common visibility/enabled/size/opacity settings, and a reset to known defaults. Changes apply immediately without rebuilding the app.
- [x] MAUI control reimplementations show the SkUi component and its native MAUI counterpart with equivalent content, constraints, and shared property values. Previews are side-by-side on wide screens and stacked on narrow screens, remaining usable after resizing or rotation.
- [x] Each page exposes observable behavior (such as independent click counts, scroll offsets, image loading/error status, and arranged bounds) and an explicit property-check action. Property checks are not a substitute for native interaction and visual verification.
- [x] SkUi-only components have a dedicated standalone demo without a misleading native-equivalence claim. Unsupported parity features are documented; comparisons allow platform-native appearance differences.
- [x] Components are organized into four groups — **Basic controls** (leaf, non-layout, non-shape controls such as `SkUiView`, `SkUiLabel`, `SkUiButton`, `SkUiImage`), **Layouts** (composition hosts and multi/single-child layouts such as `SkUiContentView`, `SkUiLayout`, `SkUiGrid`), **Graphics** (BoxView and MAUI's shapes: `SkUiBox`, `SkUiEllipse`, `SkUiLine`, `SkUiRectangle`, `SkUiRoundRectangle`, `SkUiPath`, `SkUiPolygon`, `SkUiPolyline`), and **Scrolling & collections** (`SkUiScrollView` and, later, virtualizing collection view controls). The `MauiSkiaUi` library's source files are organized under `Controls/Basic/`, `Controls/Layouts/`, `Controls/Graphics/`, and `Controls/Scrolling/`; cross-cutting infrastructure lives in root-level `Extensions/` (builder extensions) and `Helpers/` (touch routing, animation clock, frame renderer) folders, with the core contract/base class (`ISkUiView`, `SkUiView`, `SkUiViewHandler`) at the project root (namespace stays `MauiSkiaUi`). Further infrastructure folders: `Gestures/` (pointer routing, gesture arena, recognizers), `Imaging/` (image sources, loader, memory / disk caches, decoder, transformations), `Rendering/` (retained compositor), `Surfaces/` (Metal / GL surfaces), `Look/`, `ColorScheme/`, and `Core/` (Core layer). The demo gallery lists components under matching section headers in the same order, plus a separate **Core** group.
- [ ] Gallery navigation, property changes, reset, and responsive comparisons are covered by automated tests where possible and device checks on Android and Apple. Preview and editor state must not leak between pages. *(Automated coverage done via `ComponentDemoTests`; device checks still open.)*
- [x] Gallery pages for layouts, Skia-drawn controls, and **hosted** Entry / Editor / WebView via `SkUiMauiContentView` (FR-16): `MauiContentViewDemoPage` (Editor / WebView), "Native overlays in ScrollView" (Entry).
- [ ] Verified on Android and at least one Apple target (iOS or Mac Catalyst). *(Partial: gestures, scrolling and native overlays verified on a Galaxy S9, an iPhone / the iOS simulator and Windows 11; a full gallery pass is still open.)*

### FR-6 — Packaging readiness

- [x] Public types use the **`SkUi*`** naming convention (e.g. `SkUiView`, `SkUiContentView`, `SkUiLayout`, `SkUiGrid`, `SkUiLabel`, `SkUiMauiContentView`); project/assembly name remains **`MauiSkiaUi`**; NuGet package id is **`SkiaUi.Maui`**.
- [x] Prepare **`SkiaUi.Maui`** for NuGet publish (package id, versioning, metadata, symbols as needed): `MauiSkiaUi.csproj` package metadata + `.snupkg`, `nuget-pack` / `nuget-publish` workflows, [CHANGELOG.md](../../CHANGELOG.md), [Releasing.md](../Releasing.md).
- [x] **`MauiSkiaUiDemo`** stays **in-repo only** — not published as a NuGet package.

### FR-7 — Animation

Design details and checklist: [AnimationMechanism.md](AnimationMechanism.md).

- [x] Support property / transform / opacity (and similar) animations on `ISkUiView` nodes, driven so that active animations can sustain **butter-smooth ~60 fps** on target devices with GPU-backed surfaces: render-thread `AnimateAsync` (opacity / translation / rotation / scale) plus UI-thread `SkUiAnimationClock` callbacks. *(Device frame-rate targets are not claimed.)*
- [x] Animation clock / ticker integrates with the bridge invalidation model (continuous frames while any animation is active; idle when none are).
- [x] Animated nodes mark themselves dirty each frame as needed; layouts and the paint path still honor selective Measure / Layout / Paint and cached bitmaps for **non-animated** siblings (NFR-2 / FR-3).
- [x] Demo or gallery sample shows at least one continuous animation under `SkUiContentView` (Primitives page transform animation; Stress page spinners).

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
- [x] Favor **reusable drawing helpers / primitives** across controls (e.g. shared rounded-rectangle / border / fill used by many Background layers — not copy-pasted Skia paths per control). Reuse is via shared paint utilities, interfaces, or layer implementations (NFR-4), not by requiring every chrome piece to be its own `ISkUiView`. The shared painters are the public, replaceable **control look** (`SkUiLook.Current`, **FR-18**) used by both Core and MAUI-compatible controls.
- [x] Hit-testing remains **view-level** (arranged bounds); overlays do not get separate hit geometry in v1 (FR-11 / [EventMechanism.md](EventMechanism.md)).
- [x] Apply this model consistently to built-in controls; keep Option B-style nesting for true **child content** in layouts only (`Children`), not for a control’s own chrome layers.

### FR-10 — Bindable properties, direct setters, and update batching

Goal: stay a near **drop-in replacement** for standard MAUI controls (XAML + bindings) while offering a faster path when bindings are not needed.

- [x] Public stylable / bindable API surface uses **`BindableProperty`** (e.g. `BackgroundColor`) so XAML and data binding work like MAUI.
- [x] For each such property, also expose a **direct setter** (e.g. `SetBackgroundColor(...)`), the property setter in fluent form.
- [x] One update path: a direct setter sets its property (`SetFillColor(v)` is `FillColor = v`); the property's change handler (`OnFillColorChanged`) is the single place that applies a value to the control.
- [x] Direct setters support a **fluent interface** (return `this` / the control type for method chaining).
- [x] **Getters read the bindable store and direct setters write it**, as on MAUI's controls. *(Revised: direct setters used to skip the store, which desynced it from control state; worse, getters returned a field that the change handler updates after MAUI raises `PropertyChanged`, so bindings, triggers and `x:Reference` sources read the old value.)* Each value check is the property's `validateValue` (MAUI then ignores an invalid value with a warning, on every path) and is repeated by the direct setter, which throws; invalid values never reach the store. A direct setter is a local value over a style's, as the property setter is. Paint and layout keep reading private fields that the change handlers keep in step with the store (tested on every control).
- [x] Support **semi-transactions** via `StartUpdating()` → `EndUpdating()`: after `StartUpdating()`, setting values (via bindable properties or direct setters) must **not** invalidate measure / layout / paint immediately; coalesced invalidation runs when `EndUpdating()` is called. Nested start/end behavior (reentrancy / count) must be defined and documented.

### FR-11 — Clipping and masking

Design details: [DrawingMechanism.md](DrawingMechanism.md).

**Decision (hit-test vs paint):** match common iOS / Android / MAUI control behavior. **Clip / rounded corners / masks constrain painting.** Default **hit-testing uses the arranged layout bounds** (rectangle). A tap in a visually empty rounded corner that is still inside the layout rect **still hits that control** — it does **not** fall through to siblings underneath. Shape-aware hit-testing (path / mask contains-point) is an **optional opt-in** for special controls later, not the v1 default (avoids non-standard complexity).

- [x] Support **clipping / masking** so painted content is constrained to a shape (rectangle, rounded rectangle, path/mask, and similar), not only the layout bounds. *(Rectangle via `ClipToBounds`, rounded rectangle via button chrome, any shape via a border's `StrokeShape` (P6), and MAUI's `Clip` geometry on every view of both layers (`SkUiCoreNode.SetClip`, P7), composite-time. Alpha masks beyond shapes are not planned.)*
- [x] Outside the clip, the control must not paint (those pixels remain transparent / show content underneath) — e.g. a button with rounded corners does not fill the rectangular corner regions outside the round rect.
- [x] **Default hit-testing uses arranged bounds**, independent of clip/mask paint shape (same as typical UIKit / Android / MAUI buttons).
- [x] Document that shape-limited hits are **opt-in / future** (e.g. virtual hit-test override), not required for rounded buttons in v1.
- [x] Clip shape stays consistent across Background / Content / Overlay layers unless a layer explicitly opts out (documented).
- [x] Clip changes participate in selective invalidation and transparency-aware redraw (NFR-2 / FR-8).
- [x] Demo or gallery sample: rounded control with visually clear corners that remain within the control’s rectangular hit target (`BorderDemoPage`, `ButtonDemoPage` `CornerRadius`).

### FR-12 — Styles and VisualStates (MAUI per-control property appearance)

**Naming:** this is **MAUI styling** of individual controls. It is **not** control look (FR-18) and **not** the shared SkiaUi **color scheme** (FR-19).

**Status:** implemented (P2 in [ImplementationPlan.md](ImplementationPlan.md); `Focused` / `Unfocused` with P10) except the collection views' `Selected`.

- [x] Style SkiaUi controls with standard **MAUI** mechanisms: `Style` (implicit and explicit), `Setter`s on `BindableProperty`s, resource dictionaries, and `VisualStateManager` visual state groups with setters.
- [x] `Normal` / `Disabled` on every `SkUi*` view (MAUI's `VisualElement` raises them on `IsEnabled` changes); `SkUiButton` raises `Normal` / `Pressed` / `Disabled`, including Disabled when its command cannot execute.
- [x] Every control raises the visual states of its MAUI counterpart: `SkUiView` overrides `ChangeVisualState` with SkiaUi's input state instead of MAUI's platform-set flags (checked against MAUI's controls step by step in `VisualStateTests`).
  - [x] `Pressed` on `SkUiImageButton`.
  - [x] Toggle states: `IsChecked` (CheckBox), `On` / `Off` (Switch), `Checked` / `Unchecked` (RadioButton).
  - [x] `PointerOver` from hover tracking in the pointer router (mouse, trackpad, pen, iPad pointer); MAUI's `IsPointerOver` is internal, so SkiaUi keeps its own flag (`SkUiView.IsPointerOver`).
  - [x] `Focused` / `Unfocused` from keyboard focus of drawn controls (P10, FR-28).
  - [x] `Selected` on collection items (FR-22, B2): the root of the selected item's view (`Disabled` wins; before `PointerOver`).
- [x] State triggers (`StateTrigger`, `AdaptiveTrigger`, `CompareStateTrigger`) verified on drawn controls.
- Core nodes are not `VisualElement`s: their state visuals come from the look and its transitions (FR-18, FR-26), not from VSM.
- [x] Prefer FR-12 for **per-control** overrides (this button’s fill, that label’s font). Shared **default** palette across Core + MAUI controls is **FR-19** (color scheme); do not require apps to duplicate Accent/Background tokens only via MAUI resources for Core trees.
- [x] Document sample `Style` resources for common controls in the demo or docs.
- [x] Direct setters remain available (FR-10); styles and XAML setters go through bindable properties (and thus call direct setters). A direct setter is a local value, as the property setter is: it overrides a style's value.
- [x] Keep docs clear: **FR-12** = per-control MAUI Style/VSM; **FR-18** = shape / default sizes; **FR-19** = shared default colors.

### FR-13 — Dual-mode: `ISkUiView : IView`

- [x] **`ISkUiView` derives from MAUI `IView`**; concrete types use **`SkUiView`** / **`SkUiContentView`** / **`SkUiLayout`** so the same type is an `IView` for MAUI and a Skia node in a hosted tree.
- [x] **Standalone:** the control participates in the MAUI layout / input pipeline via `IView` and our **custom handler**, which creates a SW or GL platform view from **`HwAccelerated`** (FR-14).
- [x] **Hosted in SkiaUi tree:** when a **Skia-drawn** control is a child of another SkiaUi parent (`SkUiContentView.Content` or `SkUiLayout.Children`), do **not** allocate a MAUI handler or create a Skia platform view for that child; measure / arrange use `IView`, paint / touch use `ISkUiView` on the parent’s shared surface.
- [x] **`SkUiMauiContentView` exception:** when hosted, the wrapper still has no Skia surface of its own, but **must** create/manage the wrapped MAUI control’s handler and native overlay (FR-16). Implemented via the root handler's `SkUiOverlayContainer` and `FindRoot`/`AttachOverlay`/`UpdateOverlayBounds`; see FR-16 for the current limits (no rotation/scale/opacity composition).
- [x] Document how hosted vs standalone mode is detected and what that means for XAML nesting — see [LayoutSystem.md](LayoutSystem.md#detection).

### FR-14 — `HwAccelerated` and custom handler

- [x] **`SkUiView` does not derive from `SKGLView`**; platform mapping is via a SkiaUi MAUI handler.
- [x] Non-bindable **`HwAccelerated`** on `SkUiView`: `true` → GL platform view; `false` → software Skia platform view.
- [x] **Init-only semantics:** value is applied at handler/platform-view creation and **cannot change afterward**. Not a C# `init` property (XAML requires a settable CLR property); changing it after handler creation throws `InvalidOperationException`.
- [x] Defaults: **`SkUiContentView` / `SkUiLayout` → true**; other controls → **false** (safe for many standalone instances, e.g. collection cells).
- [x] Apps may opt a leaf control into HW by setting `HwAccelerated = true` **before** the handler is created (e.g. in XAML or before the view is added to the visual tree).
- [x] Document cost of many HW-accelerated standalone surfaces and recommend composing under one `SkUiContentView` / `SkUiLayout` when possible.

### FR-15 — SkiaUi-owned gestures (not MAUI `GestureRecognizers`)

**Decision:** a **shared SkiaUi gesture arena** on the drawn tree, used by SkUi* and Core alike. MAUI `View.GestureRecognizers` / `GesturePlatformManager` are not the primary path: hosted children have no platform handler (FR-13), and long press is not a MAUI recognizer. Design, decisions and API: [EventMechanism.md](EventMechanism.md).

- [x] **Recognizers:** single tap, double tap, long press, swipe, pan and pinch / rotate are classified from the root host's pointer stream, in the same DIP coordinates as Measure / Arrange / Paint. A raw pointer recognizer replaces custom touch overrides.
- [x] **Implemented once:** the arena, hit-testing and recognizers exist once for both layers. `SkUiView` and Core nodes expose events and commands, and recognizers are created only while used. There are no per-control gesture state machines.
- [x] **Passive by default:** passive controls (e.g. `SkUiLabel`) do not take pointers unless the app opts in. Untouched labels let hits through to views underneath.
- [x] **Intrinsic handling:** active controls (`SkUiButton`, toggles, Core buttons) handle taps and press feedback without app handlers.
- [x] **`InputTransparent` and `IsEnabled`:** input-transparent views are skipped; disabled views block (they are hit, nothing reacts).
- [x] **Hit region:** hit-testing uses **arranged bounds** (FR-11).
- [x] **One path:** standalone and hosted modes share the API and rules; only the root maps platform input.
- [x] **Competition:** gestures compete in a per-pointer arena. Nested scrolling works, controls inside scrollers can drag, contested presses show after `PressDelay`, and multi-touch is independent per pointer.
- [x] **Native coordination:** native ancestors are coordinated at the surface (Android disallow-intercept; iOS gate recognizer).
- [x] Demo / gallery gesture page: passive label made tappable, `InputTransparent` pass-through, long press / double tap / swipe / pinch, nested scrollers. *(Spread over Core "ScrollView + gestures", `ViewDemoPage` and "Native nesting"; there is no dedicated SkUi* gesture page.)*
- [x] **Overlays:** taps and text input over `SkUiMauiContentView` overlays stay with the native control. Drags that start on an overlay are also offered to the continuous recognizers (scroll, pan, swipe, pinch) of its drawn ancestors, which may claim them ([EventMechanism.md](EventMechanism.md#drags-that-start-on-a-native-overlay)).

### FR-16 — Host MAUI controls (`SkUiMauiContentView`)

**Decision (amended):** do **not** build a Skia WebView or other platform-engine controls; build drawn text editors (`SkUiEntry`, `SkUiEditor`) whose input goes through a platform IME proxy (Phase T in [ImplementationPlan.md](ImplementationPlan.md#phase-t--drawn-text-input-fr-16-amended)). Hosting stays for everything else and as a fallback for native text fields. Host real MAUI **`Entry`**, **`Editor`**, **`WebView`**, and other `VisualElement`s that need system components via a dedicated SkiaUi wrapper (DrawnUi `SkiaMauiElement` / Avalonia `NativeControlHost` / Flutter platform-view overlay pattern).

**Rationale:** browser engines, maps and media are platform services; reimplementing them in Skia is high cost and lower quality. Text input differs: the keyboard, IME composition, autocorrect and the edit menu stay platform services reached through the proxy, while the text, caret and selection are drawn, so text fields lose the overlay limits (snapshots, rectangle clips, nothing drawn on top, one platform view per list cell, no Core use). Overlay hosting keeps SkiaUi layouts fast while allowing drop-in use of existing MAUI controls inside `SkUiGrid` / other layouts.

- [x] Implement **`SkUiMauiContentView : SkUiView`** (`ISkUiView`) with **`Content`** (`VisualElement`) marked `[ContentProperty(nameof(Content))]`.
- [x] XAML works as a child of `SkUiLayout` / `SkUiContentView`, e.g. `<SkUiMauiContentView><Entry …/></SkUiMauiContentView>` and `<SkUiMauiContentView><WebView …/></SkUiMauiContentView>`; attached layout properties (`Grid.Row`, etc.) apply to the **`SkUiMauiContentView`**.
- [x] **Measure / arrange:** placeholder participates in the SkiaUi MAUI-based layout pipeline (FR-3a); `MeasureContent`/`ArrangeContent` delegate straight to the wrapped `VisualElement`'s own `Measure`/`Arrange`. A handlerless `VisualElement` (not yet attached) measures as `Size.Zero`, matching plain MAUI `IView.Measure` behavior without a handler — this is a MAUI platform limitation, not a SkiaUi one.
- [x] **Native overlay:** create the wrapped element’s MAUI handler; add its platform view as a **sibling** of the standalone root’s Skia surface inside a small native container (`SkUiOverlayContainer`, one implementation per platform) that the root handler now returns instead of the bare Skia surface. Position/size sync to the placeholder’s root-relative arranged bounds, including this node's and every ancestor's `TranslationX`/`TranslationY` (`ComputeRootRelativeFrame`). The overlay is **clipped** to the intersection of ancestor scroll viewports and clipping ancestors (`ClipToBounds`), through a per-overlay clip wrapper, so it neither draws nor takes touches outside them. **Not yet synced:** rotation, scale and opacity on this node or its ancestors.
- [x] **Lifecycle:** the standalone root's handler notifies `SkUiMauiContentView` descendants (via a `SkiaChildren` tree walk) on connect/disconnect; attach/detach also runs on `OnParentSet`/content replacement for dynamic insertion into an already-connected tree. BindingContext propagates through the existing `AddLogicalChild`/`RemoveLogicalChild` ownership already used for hosted children.
- [x] **Paint / snapshot while scrolling:** `ScrollMode` = `Auto` (Android / Windows: snapshot; Apple: live), `Snapshot`, or `Live`.
  - While an ancestor scroller moves (drag, including chained drags; fling; animated scroll), the native view is captured, hidden and drawn as a bitmap, so it moves in sync with the drawn content on the render thread.
  - It is restored `SnapshotRestoreDelay` (150 ms) after motion stops.
  - A focused control stays live.
  - `HighlightSnapshots` is a diagnostics outline.
- [x] **Input:** the overlay node never takes drawn pointers; the native view receives real platform input directly. Only drags that a drawn ancestor's continuous gesture claims (e.g. a scroll) are handed over to the drawn tree ([EventMechanism.md](EventMechanism.md#drags-that-start-on-a-native-overlay)).
- [x] **Z-order / clipping:** overlays are added after the Skia surface (so they paint on top). Each overlay sits in a clip wrapper sized to its visible rectangle (ancestor scroll viewports / clipping ancestors); it is hidden when fully scrolled out. Masking by drawn content on top of an overlay (drawn popups over native views) is still open (review 2.8).
- [x] **Scope for v1 demos:** `MauiContentViewDemoPage` hosts both **`Editor`** and **`WebView`** (switchable) under a `SkUiContentView` in the gallery.
- [x] Explicitly **out of product scope:** `SkUiWebView` (and similar Skia reimplementations of platform-engine controls). `SkUiEntry` / `SkUiEditor` are back in scope with an IME proxy (Phase T).
- [x] Document cost: each hosted control is a real platform view; prefer few overlays, not one per collection cell, unless measured acceptable.
- [x] XML docs on `SkUiMauiContentView` describing overlay model, gesture boundary vs FR-15, and the v1 limits above.
Primary references: DrawnUi `SkiaMauiElement`; MAUI handlers for Entry / Editor / WebView; Flutter platform views / Avalonia `NativeControlHost` for composition tradeoffs.

### FR-17 — Scrolling and collection views

Design details and checklist: [ScrollingAndCollectionViews.md](ScrollingAndCollectionViews.md).

**Decision:** **SkiaUi-owned** scrolling, on-demand item creation (FR-21) and virtualized collections (FR-22) on the shared surface. MAUI `ScrollView` / `CollectionView` are not the primary host for scrollable SkiaUi trees.

- [x] **`SkUiScrollView`:** `Content`, orientation, viewport clip, offsets, pan / fling (gesture arena + render-thread fling), and scroll APIs / events.
- [x] **Layout:** content is measured for its full extent on the scroll axis; offset-only changes do not remeasure or re-record (FR-3a / NFR-2).
- [x] **Core layer:** `SkUiCoreScrollView` shares the scroll engine, and Core and SkUi* scrollers nest freely.
- [x] **Nested scrolling:**
  - orthogonal scrollers take their own axis;
  - same-axis inner scrollers go first and chain the remainder to outer ones;
  - the fling goes to the innermost scroller that can move;
  - the wheel goes to the innermost scroller that can move;
  - native ancestors take over at a drawn scroller's edge.
- [x] **Overlays:** `SkUiMauiContentView` overlays sync and clip while scrolling. On Android / Windows the FR-16 snapshot freeze applies (Apple live sync), with the `ScrollMode` opt-out. Demo: "Native overlays in ScrollView".
- [x] **Scroll bars and overscroll (P8):** look-drawn scroll bars with MAUI's `HorizontalScrollBarVisibility` / `VerticalScrollBarVisibility` (fade, RTL side), bounce / stretch overscroll per look that keeps nested chaining, `ScrollToAsync(Element, ScrollToPosition, bool)` / `ScrollToRequested`, the horizontal wheel and trackpad on `Both`, direction-aware flings with live extents.
- [x] **Polish (P8):** snap points (`SnapPointsType`, `SnapPointsAlignment`); draggable, hover-expanded scroll bars (desktop); public `SkUiCoreScrollBar` that apps can style or place.
- [x] **Demo gallery:** long content, nested carousels, Core scroll view (`ScrollViewDemoPage`, Core "ScrollView + gestures", "Native overlays in ScrollView", "Native nesting").
- [x] **Compat:** MAUI `ScrollView` / `CollectionView` nesting is documented as **compat only** (standalone cells keep `HwAccelerated = false` per FR-14).

### FR-18 — Control look (shape / chrome / default sizes; not MAUI styles)

Design details: [ControlLook.md](ControlLook.md).

**Problem:** Core and MAUI-compatible controls share drawing helpers (`SkUiChrome`), but the helpers are sealed/internal and cannot be swapped for platform-inspired or brand geometry. Default intrinsic sizes are hardcoded per control. Apps need Android-like / iOS-like / custom **shapes and default sizes** without forking every control, and without conflating that with **color scheme** (FR-19) or MAUI **styles** (FR-12).

**Decision:** introduce a public, extensible **control look** API (`SkUiLook` / `DefaultSkUiLook` — exact names open). Prefer the word **look** over **theme** in public API and docs.

- [x] Support **control look**: default Skia geometry and drawing for stock controls (Switch, CheckBox, RadioButton, ActivityIndicator, rounded button/border chrome, press tint, image destination, and similar shared painters).
- [x] Where applicable, the look also owns **default control width/height** (intrinsic measure) and related defaults (e.g. button minimum height, default corner radius). Apps can override those defaults by replacing the look, subclassing measure/size members, or replacing a size delegate. Explicit per-control size requests still win.
- [x] **Do not** fold this into FR-12 or FR-19. FR-12 = MAUI Style/VSM on individual controls; FR-19 = shared default **colors**; FR-18 = **how** chrome is drawn and **default sizes**.
- [x] App can **replace the entire look** (e.g. set current look to an Android-like or iOS-like pack, or a custom subclass) — shapes and default sizes together when the pack defines both.
- [x] App can **change drawing and/or default size for one control kind** without replacing the whole pack — by **overriding a virtual** on a look subclass **and/or** replacing a **per-painter or per-size delegate** on the look instance.
- [x] Promote today’s `SkUiChrome` painters into the default look implementation; both `SkUi*` and `SkUiCore*` call the **active** look for paint **and** default measure so Core and MAUI-compatible stay aligned.
- [ ] Optional resolution: control-local look → tree/host look → app current look → `DefaultSkUiLook` (global `Current` shipped; per-tree deferred).
- [x] Changing the active look raises `CurrentChanged`; apps invalidate measure/paint (automatic tree walk deferred).
- [x] Document naming clearly vs FR-12 / FR-19; tests cover swap full look + override a single painter and a single default size.
- [ ] Built-in platform-inspired packs may ship later; FR-18 v1 requires the **extensibility model** + default look (including overridable default sizes), not full OS parity.
- [x] Gallery sample page for look packs (`LookAndColorSchemePage`).

### FR-19 — Color scheme (shared default palette; not MAUI styles / not look)

Design details: [ColorScheme.md](ColorScheme.md).

**Problem:** Core and MAUI-compatible controls hardcode shared defaults (`SkUiColors.Accent`, TrackOff, Disabled, …). Apps need light/dark (or brand) **palettes** and the ability to change one token (e.g. accent only) without MAUI styles alone (Core has no Style) and without changing control geometry (FR-18).

**Decision:** introduce a public, extensible **color scheme** API (`SkUiColorScheme` / light & dark packs — exact names open). Prefer **“color scheme”** / **“palette”** over **“theme”** in public API and docs.

- [x] Support **color scheme**: shared default colors used by Core and MAUI-compatible controls (at least accent, default background, default foreground/text, muted, track-off, disabled — extend as controls need).
- [x] App can **replace the entire scheme** (e.g. light vs dark pack, or a custom subclass).
- [x] App can **change particular colors** on the active scheme (e.g. only `Accent` or only `DefaultBackground`) without replacing the whole pack.
- [x] Promote today’s `SkUiColors` into the default / light scheme; both `SkUi*` and `SkUiCore*` resolve **defaults** from the active scheme.
- [x] Precedence: explicit control property / FR-12 Style wins over scheme defaults; construction snapshots `Current`; paint-time tokens via `SkUiColors` follow `Current` live.
- [ ] Optional resolution: control-local scheme → tree/host scheme → app current scheme → built-in default (global `Current` shipped; per-tree deferred).
- [x] Changing the active scheme (or a token on it) raises events; apps invalidate paint for scheme-following controls (automatic tree walk deferred).
- [x] **Do not** conflate with FR-18 (look) or treat as a replacement for FR-12 (per-control MAUI Style).
- [x] Document naming; tests cover swap light/dark + change Accent; Core and MAUI-compatible share scheme accessors.
- [x] Gallery sample: swap light/dark + change Accent only (`LookAndColorSchemePage`).

### FR-20 — Shadows

**Status:** implemented (P7 in [ImplementationPlan.md](ImplementationPlan.md)), except render-thread tweening of shadow properties.

- [x] Drop shadows on any Skia-drawn node (Core and `SkUi*`): color, offset, blur radius, opacity; MAUI `VisualElement.Shadow` (`IShadow`) parity on `SkUi*` (`Shadow` element and markup; gradient brushes); `SkUiCoreNode.SetShadow(IShadow)` with `SkUiCoreShadow`.
- [x] Shadow follows the node's shape (rounded rect / path / ellipse / text alpha), not only its rectangle: an opaque fill casts from its outline (MAUI's Android fast path), anything else from the alpha of the drawn subtree; a `Clip` shapes the shadow too.
- [x] Shadow paints **outside** the node's arranged bounds: the render node's ink bounds (`SkUiRenderProps.InkBounds`: visual bounds plus the shadow) drive culling and opacity layers; recording cull rects stay the visual bounds (a shadow is never recorded).
- [x] Clip-to-bounds is **opt-in** per node, so a child's shadow is not cut by its own clip; a parent's opt-in clip still applies.
- [x] Shadow does not affect layout or hit-testing.
- [x] Shadow blur is expensive: outline shadows are blurred by Skia's cached mask filters; content shadows are rasterized on the render thread once their subtree is stable and reused (keyed by subtree version, shadow, size, density), so offset / opacity / transform animation and scrolling never blur again.
- [ ] Shadow properties are animatable on the render thread like opacity/transform. *(Changes are composite-time — no re-record — but not tweened on the render thread.)*

Initial controls, layouts, and scroll are delivered with headless tests. Device checks cover gestures, scrolling and native overlays (Galaxy S9, iPhone / iOS simulator, Windows 11) and memory-leak scenarios; checked items do not imply full visual / contrast acceptance on every platform. See [Development.md](../../Development.md) for the precise v1 API limits.

### FR-21 — Virtual / dynamic scroll layout (on-demand children)

Design: [ScrollingAndCollectionViews.md](ScrollingAndCollectionViews.md#fr-21--virtual--dynamic-scroll-layout-requirements). Purpose: **on-demand scroll content** (indexed virtual lists, **`InfiniteFeed`**, **loop carousels**), and the item engine for FR-22 (indexed mode only).

- [ ] **`VirtualScrollMode`:** `Indexed` (default), `InfiniteFeed`, `Loop` — one engine; extent, scroll-bar, and `ScrollTo` semantics per [Virtual scroll modes](ScrollingAndCollectionViews.md#virtual-scroll-modes).
- [x] **`SkUiVirtualVerticalStackLayout`** (B1; a horizontal twin, `SkUiVirtualHorizontalStackLayout`, later) **requests** its children while the user scrolls.
  - It works inside any drawn scroller, below other content, and nested in another virtual layout.
  - Its window is the intersection of all ancestor viewports.
- [ ] **`SkUiVirtualScrollView`:** convenience control combining a scroller and a virtual stack; exposes `VirtualScrollMode` and scroll-bar interaction for modes B / Loop. (B1: the control ships for mode A; `VirtualScrollMode` and velocity scroll bars come with modes B / Loop.)
- [x] **Mode A — Indexed (default):** stable item indices; optional release of far items with **cached sizes** so scroll position and thumb stay meaningful; supports absolute / index scroll APIs and normal scroll bars. **`SkUiCollectionView` uses this mode only.**
- [ ] **Mode B — `InfiniteFeed`:** bounded child count (visible + pre/post buffer); append at trailing edge / remove leading when scrolling forward (reverse when scrolling back); **no** global content extent; **relative** scroll (`ScrollBy`, fling, logical bring-into-view) only; scroll bars **velocity / direction**, not absolute position (default hidden).
- [ ] **Mode Loop:** finite `ItemCount`; seamless wrap (last → first, first → last) using the same window trim as B; `ScrollToIndex` on `index mod N`; suited to horizontal carousels with snap points.
- [ ] **Providers** (any one is enough):
  - [x] a per-index factory with an unknown / endless count (`null` ends the list): `ItemFactory`, `ItemFactoryCount`;
  - [x] `ItemsSource` + `ItemTemplate` / selector with incremental collection changes;
  - [ ] incremental loading: `RemainingItemsThreshold` event / command done; async load-more hook and loading placeholder open.
- [ ] **Prefetch:** items are created **before** they become visible. `PrefetchFactor` (viewport lengths, default 1.0) or `PrefetchDistance`, plus a behind-distance for reverse scrolling. During render-thread flings, prefetch extends by the predicted travel. (Done: `PrefetchFactor`, `PrefetchBehindFactor`, fling prediction; open: `PrefetchDistance`.)
- [x] **Creation budget:** items are created within a per-frame UI-thread budget, synchronously only to avoid visible gaps. Nothing runs on the render thread. (`PrefetchBudget`, default 4 ms.)
- [ ] **Release and sizing:**
  - [x] default: **each item may have a different size** (per-index measure + cache, not uniform row height);
  - [x] optional release of far items (`ReleaseFactor`), keeping their measured sizes;
  - [x] recycling pool keyed by template;
  - [x] optional `EstimatedItemSize` for unrealized indices; `RemeasureItem` when content changes (also automatic when a realized item's measure changes); fixed `ItemExtent` fast path;
  - [ ] optional `QueryItemSize`;
  - [x] scroll anchoring when earlier items change size (also during render-thread flings and animated scrolls).
- [x] **API:** `ScrollToIndex` (position, animated); `ItemRealized` / `ItemReleased` / `VisibleRangeChanged` events.
- [ ] **Core variant** `SkUiCoreVirtualVerticalStackLayout`: **only if cheap**, meaning a thin wrapper over a layer-agnostic engine.
- [ ] **Benchmarks and tests:**
  - device benchmark: endless 10k-item fling at device fps with no blank frames (scenario `virtual-fling` added; device run pending);
  - memory stays flat with release (leak scenario `VirtualListScrolled` passes headless; device run pending);
  - [x] prefetch-before-visible, budget, anchoring and threshold tests (`VirtualVerticalStackLayoutTests`).

### FR-22 — `SkUiCollectionView` (virtualized collection)

Design: [ScrollingAndCollectionViews.md](ScrollingAndCollectionViews.md#fr-22--skuicollectionview-requirements). Built on FR-21 **indexed** mode with template recycling. A Core variant is **not required**: templates and bindings are MAUI concepts.

**Not MAUI parity:** unlike most SkUi* controls, FR-22 does **not** require mirroring MAUI's `CollectionView` API. The control is **SkUi-first**; porting from MAUI uses [Migration.md](../Migration.md) and the [skiaui-migration skills](../plugins/skiaui-migration/README.md), kept in sync when the control ships or changes.

- [x] **Data / templates:** `ItemsSource`, item template / selector, `EmptyView` (+ template), incremental source updates (B2).
- [x] **Layouts:** vertical / horizontal linear lists (spacing); grid with span (B2: vertical; B3: `Orientation`, `Span`, `SpanSpacing`; snap points not done).
- [x] **Item sizing (default variable)** (B2, from FR-21; `QueryItemSize` still open): assume **each item may have a different size**; per-index measure + cache (FR-21); optional `EstimatedItemSize` for unrealized rows only; optional `QueryItemSize`; **`RemeasureItem`** when item content changes; scroll anchoring on size changes.
- [x] **Header / footer** (+ templates); **sticky** header / footer; pinned parts not re-recorded while scrolling (B2: sticky parts are drawn over the list, which scrolls behind them with insets of their heights).
- [x] **Grouping:** group header / footer templates; **sticky group headers** (B3: `IsGrouped`, `GroupHeaderTemplate`, `GroupFooterTemplate`, `IsStickyGroupHeader`).
- [x] **Expandable groups** (B3: `AllowGroupExpandCollapse`, `ISkUiExpandableGroup`, `AutoExpandGroups`, `ExpandGroup` / `CollapseGroup` / `ExpandAll` / `CollapseAll`, the four events): per-group **`IsExpanded`**; collapsed groups omit items from layout / virtualization; toggle from group header; scroll anchoring when expand / collapse changes height; accessibility expanded / collapsed; `ScrollTo` and indices respect collapsed groups.
- [x] **Selection:** (B2: `None`, `Single`, `SingleDeselect`, two-way `SelectedItem`, cancelable `SelectionChanging`, `SelectionChanged` + command, `Selected` state, `SelectionBackground`; B3: `Multiple`, `SelectedItems`, `SelectAll` / `ClearSelection`, selection lists in the events; open: a selected template, extended selection) `SelectionMode` — `None`, `Single`, **`SingleDeselect`**, **`Multiple`**; selected item(s); **`SelectionChanging`** (cancelable) and **`SelectionChanged`** (+ optional command); **`SelectAll` / `ClearSelection`**; `Selected` visual state; optional `SelectionBackground` / selected template (minimal re-record). Extended (Shift/Ctrl range on desktop) later with FR-10.
- [x] **Item tap:** `ItemTapped` (+ command); inner controls keep their taps (B2).
- [x] **Scroll:** `ScrollTo` item (and group when grouped); visible range / scrolled; scroll bars (FR-17). (B2: `ScrollToIndex`, `ScrollToItem`, `ScrollToAsync`, `VisibleRangeChanged`, `Scrolled`; B3: `ScrollToGroup`, items of collapsed groups expand, horizontal scroll bars.)
- [x] **Load more:** (B2: `RemainingItemsThreshold`; B3: the modes, position, row, `IsLoadMoreActive`, `LoadingMore`) **`LoadMoreMode`** (`None`, **`Manual`**, **`Auto`**, **`AutoOnUserScroll`**); **`LoadMorePosition`** (`End` / **`Start`**); **`LoadMoreCommand`** (+ parameter); **`LoadMoreTemplate`**; **`IsLoadMoreActive`** while loading; optional **`RemainingItemsThreshold`** (+ event / command) alongside load-more UX.
- [x] **Pull to refresh** (`IsPullToRefreshEnabled`, `IsRefreshing`, `RefreshCommand`, drawn indicator; B2).
- [x] **Migration artifacts (with MVP):** `collection-view.md` in `skiaui-migrate/references`, [Migration.md](../Migration.md) CollectionView section, and plugin gap / audit updates.
- [ ] **Later (out of FR-22 MVP):** reordering; row swipe; keyboard item navigation; animated insert / remove.
- [ ] **Benchmarks and tests:**
  - 10k items at device fps while flinging;
  - recycling (no steady-state allocations per scroll);
  - selection re-records at most two items;
  - `ItemTapped` versus buttons inside items;
  - grouping, sticky headers, **expand / collapse** (no realize while collapsed, stable anchoring);
  - load more modes and start/end position;
  - variable-height items and remeasure anchoring;
  - extended selection modes and SelectAll;
  - migration doc covers supported MAUI patterns and intentional gaps.

### FR-23 — Three-state toggles

- [x] `SkUiCheckState` (Unchecked / Checked / Indeterminate) on every toggle (`SkUiCheckBox`, `SkUiSwitch`, `SkUiRadioButton` and the Core ones), through the shared toggle bases: `CheckState` is the state.
- [x] MAUI parity: `bool IsChecked` stays, as the two-state view of `CheckState` (true only for Checked; setting it sets Checked or Unchecked); `CheckedChanged` fires only when it changes, `CheckStateChanged` on every change.
- [x] `IsThreeState`: taps cycle Unchecked → Checked → Indeterminate → Unchecked; otherwise an app-set Indeterminate (e.g. a partly checked group) goes to Checked on tap. Radio buttons only ever select.
- [x] User changes write back to the two-way bindable properties (`CheckState`, `IsChecked`).
- [x] Looks draw the state: `DrawCheckBox` / `DrawSwitch` / `DrawRadioButton` take `SkUiCheckState` (default: dash, centered thumb, bar).
- [x] Headless tests (`ToggleStateTests`); demo pages edit `CheckState` / `IsThreeState`.

### FR-24 — Slider

- [x] `SkUiSlider` / `SkUiCoreSlider` with MAUI's API: `Minimum`, `Maximum`, `Value` (two-way, clamped, also on range changes), `MinimumTrackColor`, `MaximumTrackColor`, `ThumbColor`, `ValueChanged`, `DragStarted` / `DragCompleted` (+ commands on `SkUiSlider`).
- [x] `Orientation`: horizontal or vertical (minimum at the bottom); right-to-left layouts mirror horizontal sliders.
- [x] Input through the gesture arena: a drag along the slider claims after the slop (a cross-axis drag scrolls the page instead), a tap moves the value to the tapped position.
- [x] Look-customizable: `SkUiLook.DrawSlider(SkUiSliderPaint)`, `MeasureSlider`, `SliderThumbRadius`, delegates; looks draw only the horizontal case (the control rotates the canvas).
- [x] Headless tests (`SliderTests`), demo page, leak scenario.
- [x] `ThumbImageSource` (P4): drawn upright at its intrinsic size in place of the look's thumb (`SkUiSliderPaint.HasThumbImage`), through the shared image loader.
- [x] Accessibility (P10): a slider element with its range; screen readers and arrow keys step it by 5 %.
- [ ] Step / snapping.

### FR-25 — ProgressBar

- [x] `SkUiProgressBar` / `SkUiCoreProgressBar` with MAUI's API: `Progress` (clamped 0–1), `ProgressColor`, `ProgressTo(value, length, easing)`; plus `TrackColor`.
- [x] `IsIndeterminate`: a moving segment animated on the render thread (compositor content slide, `SkUiRenderProps.ContentSlidePeriod`), with no UI-thread work or re-recording per frame.
- [x] Look-customizable: `SkUiLook.DrawProgressBar(SkUiProgressBarPaint)`, sizes, segment length, period, corner radius, delegates.
- [x] Headless tests (`ProgressBarTests`), demo page, leak scenario.

### FR-26 — State-change animations

**Status:** implemented on the UI-thread design; the render-thread variants stay open. Decision and device measurements: [ArchitectureReview.md](ArchitectureReview.md#state-change-animations). Look API: [ControlLook.md](ControlLook.md#state-change-transitions-fr-26).

- [x] **Animate state changes:**
  - Switch: thumb slide and track color.
  - Check box: check mark / dash drawing in, fill.
  - Radio button: dot scale.
  - Slider: press halo while dragged; the thumb glides to tapped values.
  - Progress: value changes (a look option; off in the default look, as MAUI).
  - Transitions to and from Indeterminate.
- [x] **Press effects** on buttons and image buttons: a highlight fade (`Dim`) or a ripple from the touch point, clipped to the control's shape, fading on release or cancel (`DefaultSkUiLook.PressEffect`). Toggles and sliders pass their press amount to the look too.
  - [x] On any other tappable node, including composite buttons built from several Core nodes: `ShowsPressEffect` (drawn over the node and its children, clipped to its rounded shape).
  - [ ] Several ripples at once (a new press restarts the ripple).
  - [ ] Press scale (the text is drawn by the control, so it needs a composite-time transform).
- [x] **Configurable by `SkUiLook`:** each look sets the duration and easing per kind of state change, or none (`GetTransition` / `GetTransitionCore` / `TransitionProvider`). Looks draw from continuous parameters (`SkUiToggleVisual`, `SkUiPressVisual`, drawn slider fraction and progress), not only the discrete state.
- [x] **Visual only:** state and events change at once (`CheckedChanged` does not wait for the animation). Animations are interruptible and reverse from their current position.
- [x] **Smooth under load:** bounded to re-recording the one small node per frame (about 0.05 ms per animating control on a Galaxy S9). Frames drop only while the UI thread itself is blocked.
  - [ ] Render-thread painters: an opt-in, if apps need transitions during UI-thread work.
- [x] Respect the OS reduce-motion setting and a global off switch (`SkUiMotion`).
- [x] Same on `SkUi*` and Core; headless tests with a deterministic clock (`TransitionTests`).

### FR-27 — Composition layouts (flex, wrap, shrink)

**Status:** implemented (Phase A1–A3 in [ImplementationPlan.md](ImplementationPlan.md)). Docs: [SkUiFlexLayout.md](../controls/SkUiFlexLayout.md), [SkUiWrapLayout.md](../controls/SkUiWrapLayout.md), [SkUiShrinkLayout.md](../controls/SkUiShrinkLayout.md).

- [x] **`SkUiFlexLayout`** (SkUi* only): MAUI `FlexLayout` API (`Direction`, `Wrap`, `JustifyContent`, `AlignItems`, `AlignContent`, `Position`; attached `Order`, `Grow`, `Shrink`, `AlignSelf`, `Basis` reused from MAUI) laid out by MAUI's `FlexLayoutManager`. MAUI's flex engine is internal, so it is ported. A Core flex layout is not planned.
- [x] **Wrap layout** (`SkUiWrapLayout`, `SkUiCoreWrapLayout`): children flow left to right and wrap to new rows; `Spacing` between items, `RowSpacing` between rows; children align vertically inside their row. One engine for both layers.
- [x] **Shrink stacks** (`SkUiHorizontalShrinkLayout`, `SkUiVerticalShrinkLayout` and their Core twins): a stack that fits its content on the main axis. Without overflow it is a plain stack. On overflow, children share the overflow by their shrink factor (`SkUiShrinkFactor`): `None` (default) keeps its size; `Auto` shrinks in proportion to its natural size, only when above the average; a number `f` shrinks by `f` × natural size (CSS `flex-shrink`). No child goes below its minimum size; the rest is shared again. One engine for both layers and axes.
- [x] RTL through the standard frame mirroring; attached-property changes relayout; headless tests (`FlexLayoutTests` with a MAUI `FlexLayout` parity sweep, `WrapLayoutTests`, `ShrinkLayoutTests`), demo pages, docs, leak scenario (`LayoutsRelayout`).

### FR-28 — Accessibility, keyboard focus and font scaling

**Status:** implemented (P10 in [ImplementationPlan.md](ImplementationPlan.md), N8). Design: [Accessibility.md](Accessibility.md); docs: [SkUiView.md](../controls/SkUiView.md#accessibility-and-keyboard), [SkUiCore.md](../controls/SkUiCore.md#accessibility-and-keyboard).

- [x] A semantics tree per surface from both layers: MAUI's `SemanticProperties` (`Description`, `Hint`, `HeadingLevel`) and `AutomationProperties` (`IsInAccessibleTree`, `ExcludedWithChildren`, `Name`, `HelpText`) on SkUi* views, the same as Core node properties; roles, states, ranges and actions from the controls; tappable groups read as one element; visible bounds through transforms, scrolling and clips; stable ids.
- [x] Platform accessibility: Android `ExploreByTouchHelper` (virtual views), iOS / Mac Catalyst accessibility elements, Windows automation peers; actions (activate, long press, increment / decrement, set value, scroll by page); change notifications; hosted native views read natively; `SetSemanticFocus` for drawn nodes.
- [x] Keyboard focus: MAUI's `Focus()` / `Unfocus()` / `IsFocused` / `Focused` / `Unfocused` on drawn views, Core focus API, `IsTabStop` / `TabIndex`, Tab / Shift+Tab within the surface and on to native controls, Space / Enter activation, arrow / page / home / end keys, a look-drawn focus ring (focus-visible), scroll into view.
- [x] OS font scaling: `FontAutoScalingEnabled` (labels, buttons, radio buttons, spans, HTML, font images; both layers), system text size per platform, `SkUiFontScaling.Factor` override, live surfaces re-measure on change.
- [x] Extensible: `OnPopulateSemantics` / `OnSemanticsAction` / `OnSemanticsSetValue` on both layers for own controls.
- [x] Headless tests (`AccessibilityTests`, `KeyboardFocusTests`, `FontScalingTests`), leak scenario (`AccessibleFocused`), demo page (**Accessibility**), Mac Catalyst checked through the accessibility API and a hardware keyboard.
- [ ] Device runs with TalkBack, VoiceOver (iOS) and Narrator; keyboards on Android and Windows.
- [ ] Links inside text as elements, custom actions, live regions, arrow keys within radio groups.

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
- **Paint ([RenderingPipeline.md](RenderingPipeline.md)):** retained compositor. Each node keeps recorded Content / Overlay `SKPicture`s (vector, low memory — not per-node bitmaps); only nodes whose content changed are re-recorded; offset / transform / opacity / clip / scroll offset are composite-time. Raster caching of stable subtrees is a later optimization.
- Invalidation must be granular enough for selective **layout** (per-node dirty flags for size and arrangement). Paint invalidation coalesces to the root surface (FR-10).
- **Transparency:** live paint walk must composite overlapping translucent nodes correctly (FR-8). “Expand dirty region / opaque cover blit” rules apply only if/when retained paint caches or partial-surface updates are introduced — not as a v1 prerequisite.

**Implementation approach (correctness first, then max performance):**

1. **Initial implementation** of a feature or critical path should use **simple, easy-to-understand** code so behavior is obvious and reviewable.
2. Once **tests confirm** correctness, **optimize** that path toward **maximum performance** and **minimum memory allocation** (including `unsafe` / low-level techniques where profiling shows benefit).
3. Do not ship clever/unsafe micro-optimizations before the simple version is covered by tests; keep optimized code as readable as practical and document non-obvious invariants.

### NFR-6 — Threading (UI thread offload)

The MAUI UI thread is often overloaded; SkiaUi must keep motion smooth when it stalls. Design: [RenderingPipeline.md](RenderingPipeline.md).

- [x] UI thread does only what must read MAUI objects: layout, recording dirty nodes, committing a batch.
- [x] GPU surfaces composite and rasterize on a render thread: shared Metal render thread on iOS / Mac Catalyst (no GLKView / OpenGL ES), `GLTextureView` GL thread on Android.
- [x] Composite-time animations (opacity, transforms, scroll offset, fling, animated scroll, spinners) run on the render thread and report values back; the UI's explicit value wins over a running animation.
- [x] Nothing on the render thread touches MAUI / `BindableObject` state.
- [ ] Record Core-only subtrees off the UI thread.
- [ ] Raster cache of stable subtrees with a per-frame budget.
- [ ] Hit-test against render-thread transforms while animations run.

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

- **Code:** XML comments on all public types and members. Add **inline comments for non-obvious** logic (invariants, performance tricks, `unsafe` blocks, hosted-vs-standalone quirks). Do not comment the trivial.
- **Per-control markdown:** each public control / layout (e.g. `SkUiLabel`, `SkUiGrid`, `SkUiScrollView`, `SkUiMauiContentView`) has its own **`.md`** file describing how it works and how to use it (XAML / C# samples, key properties, gestures, theming). Index: [docs/controls/README.md](../controls/README.md) (linked from [README.md](../../README.md)).
- **MAUI reimplementations:** when a `SkUi*` type is a near drop-in for a standard MAUI control or layout, **do not** restate the full MAUI feature encyclopedia. Prefer a short overview, then **link to the official MAUI documentation** for baseline behavior. **Must document SkiaUi differences and extensions** (API deltas, unsupported MAUI features, direct setters, layers, `HwAccelerated`, gesture model, performance notes, etc.).
- **SkiaUi-specific / infrastructure types** (`SkUiView`, `SkUiContentView`, `SkUiLayout`, `SkUiMauiContentView`, mechanisms): fuller how-it-works docs are expected (may point at [LayoutSystem.md](LayoutSystem.md), [DrawingMechanism.md](DrawingMechanism.md), etc. for shared pipelines).
- Keep control docs in sync when behavior or public API changes.

## Out of scope (for now)

- Full design-system parity with native MAUI controls for **Skia-drawn** copies (hosted MAUI Entry/Editor/WebView keep their native look via FR-16). **Control look** (FR-18) enables approximate platform-inspired shapes; pixel-perfect OS clones are not required.
- Custom Skia reimplementations of **WebView** (and similar browser / platform-engine controls) — use `SkUiMauiContentView` instead (FR-16). Drawn Entry / Editor are planned with an IME proxy (Phase T)
- Blazor Hybrid / multi-project MAUI host
- Software-only fallback host (unless needed when `HwAccelerated` is true but GL is unavailable — then fall back per Acceleration)
- Using MAUI `GestureRecognizers` / `GesturePlatformManager` as the gesture system for SkiaUi trees (a compatibility bridge runs `TapGestureRecognizer` on the arena; other recognizers are reported — see [EventMechanism.md](EventMechanism.md#maui-gesture-recognizers))

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

- Exact public names for animation helpers / `ISkUiAnimator` (tier APIs sketched in [AnimationMechanism.md](AnimationMechanism.md)).
- Scroll details still open in [ScrollingAndCollectionViews.md](ScrollingAndCollectionViews.md#open-items): how FR-21 / FR-22 provide their extent inside an outer scroller.
- **Control look (FR-18)** remaining: per-tree look attachment (vs process-wide `Current`), and optional OS theme sync helpers. Type names, `Current`, virtual/delegate painters, and default size tokens are decided — see Decided and [ControlLook.md](ControlLook.md).
- **Color scheme (FR-19)** remaining: optional OS light/dark synchronization helpers. Type names, light/dark packs, `Current`, and construction-snapshot vs paint-time token reads are decided — see Decided and [ColorScheme.md](ColorScheme.md).

## Decided

- **HW acceleration / handler (FR-14):** `SkUiView` does **not** derive from `SKGLView`. A **custom MAUI handler** creates a GL or software Skia platform view from non-bindable **`HwAccelerated`**. Defaults: **`SkUiContentView` and `SkUiLayout` → true**; other controls → **false**, so many standalone instances (e.g. in `CollectionView`) do not each open a GL surface. Hosted children never create a platform view. **`HwAccelerated` is init-only in practice** (frozen at handler creation; not a C# `init` property because of XAML).
- **Class hierarchy:** **`SkUiView`** is the base class (implements `ISkUiView`). **`SkUiContentView : SkUiView`** exposes **`Content`** (`ISkUiView`). **`SkUiLayout : SkUiView`** is the base for all layouts and exposes **`Children`** (`IList<ISkUiView>`). Both Content and multi-child layouts are supported via these two types (not mutually exclusive).
- **`ISkUiView : IView` (FR-1 / FR-13):** `ISkUiView` derives from MAUI `IView`. Measure/Arrange come from `IView`; `ISkUiView` adds **Paint** and **Touch**. Standalone use in the MAUI tree uses our handler + `HwAccelerated`; when hosted under another SkiaUi parent, skip MAUI handler and platform-view allocation. Hosted-vs-standalone detection: [LayoutSystem.md](LayoutSystem.md#detection).
- **Layout system (FR-3a) — MAUI-based:** measure/arrange follows the **MAUI layout system** (not Flutter box constraints) so SkiaUi can ship drop-in-ish copies of MAUI controls/layouts and **reuse MAUI layout managers**. Multi-pass measure where MAUI requires it is expected; optimize via caching/dirty flags. Flutter is not the layout contract. Hosted vs standalone measure/arrange: [LayoutSystem.md](LayoutSystem.md).
- **Styles / VisualStates (FR-12):** use **MAUI styles** (`Style`, resource dictionaries, `VisualStateManager` as applicable) on `BindableProperty`s for **per-control** property appearance. Shared default **colors** are **FR-19** (color scheme). Control **shape / default sizes** are **FR-18** (control look).
- **Control look (FR-18):** public `SkUiLook` / `DefaultSkUiLook` with `Current`, virtual cores, and optional per-painter / per-measure delegates. Controls call the active look for paint and intrinsic sizes. Details: [ControlLook.md](ControlLook.md).
- **Color scheme (FR-19):** public `SkUiColorScheme` with `LightSkUiColorScheme` / `DarkSkUiColorScheme`, `Current`, and mutable tokens. `SkUiColors` reads the active scheme. Construction snapshots accents; paint-time tokens follow `Current`. Details: [ColorScheme.md](ColorScheme.md).
- **Properties — BindableProperty + direct setters (FR-10):** use `BindableProperty` for near drop-in MAUI / XAML / binding parity. Also expose fluent direct setters (e.g. `SetBackgroundColor`), the property setters in chainable form; bindable property changed callbacks **call** those setters to apply values. Getters read the store and direct setters write it (revised: the earlier "direct setters do not write back" rule broke binding sources). Both paths honor `StartUpdating()` / `EndUpdating()` semi-transactions: defer measure/draw invalidation until `EndUpdating()`.
- **Coordinate system:** same as MAUI — `ISkUiView` sizes, positions, and touch coordinates use MAUI device-independent units (DIPs) and the same density semantics as the host; `SkUiContentView` maps to/from the Skia pixel surface as an implementation detail of the bridge.
- **Public type naming:** `SkUi*` (e.g. `SkUiView`, `SkUiContentView`, `SkUiLayout`, `SkUiGrid`, `SkUiLabel`, `SkUiMauiContentView`, `ISkUiView`). Project/assembly name remains `MauiSkiaUi`; NuGet package id is **`SkiaUi.Maui`**.
- **NuGet:** publish **`SkiaUi.Maui`** as a NuGet package; **`MauiSkiaUiDemo`** is in-repo only (not published).
- **Drawing layers (FR-9) — Option A:** dedicated layer structure (Background / Content / Overlay paint phases on the control), not nested `ISkUiView` hosts for chrome. Layers need the owning control’s data (e.g. table/grid draws Background **lines** from its own row/column measurements). Nested hosting remains for layout `Children` only. Toolkit notes (Flutter composition vs engine `Layer` tree, Avalonia/Uno templates, DrawnUi child trees) stay relevant as reference for caching/reuse, not as the chosen chrome model. **Customization:** Background/Overlay via `PaintBackground` / `PaintOverlay` delegates; Content remains virtual `OnPaintContent`. Same on Core. Details: [DrawingMechanism.md](DrawingMechanism.md).
- **Code performance (NFR-2):** critical paths (especially animation tick + paint) aim for **maximum speed** and **zero / near-zero allocations**, using modern C#/.NET techniques including **`unsafe`** where justified. **Correctness-first workflow:** ship simple, readable code first; after tests confirm behavior, optimize for max performance and min allocations.
- **Flexibility and reuse (NFR-4):** extensible controls via **virtual hooks** and **interfaces**; shared building blocks for background drawing, animations, layers, and similar — avoid copy-paste chrome.
- **Documentation (NFR-5):** XML + comments for non-obvious code; **one `.md` per control** (how it works / how to use). MAUI reimplementations: link to official MAUI docs for baseline behavior; document **differences and extensions** only (do not duplicate full MAUI manuals).
- **Paint caching (NFR-2) — retained compositor (supersedes v1 full-tree redraw):** per-node `SKPicture`s re-recorded only on content change; composite-time properties never re-record; the full frame is cleared and composited each present (no dependence on retained GPU backbuffers). No per-node bitmaps by default. Details: [RenderingPipeline.md](RenderingPipeline.md).
- **Threading (NFR-6):** record on the UI thread, composite + animate on a render thread (Metal on Apple, GL thread on Android). Details: [RenderingPipeline.md](RenderingPipeline.md).
- **Clip default:** `ClipToBounds` is on for leaf controls and off for layouts / content hosts (MAUI `Layout.IsClippedToBounds` parity) so children and their shadows (FR-20) can overflow.
- **Gestures (FR-15) — SkiaUi-owned:** do **not** use MAUI `GestureRecognizers` as the primary API for the drawn tree. One shared gesture arena (one arena per pointer, so multi-touch is independent) classifies tap / double-tap / long-press / swipe / pan / pinch for SkUi* views and Core nodes alike; `ISkUiView.Paint(SKCanvas)` and `ISkUiView.Touch(SkUiTouchEvent)` are the surface entry points. Passive-by-default (e.g. label) vs intrinsic handlers (e.g. button); honor `InputTransparent`. Details: [EventMechanism.md](EventMechanism.md).
- **Clip vs hit-test (FR-11):** clip/mask/rounded corners affect **paint** only by default. Hit-testing uses **arranged bounds** (iOS / Android / MAUI-like). Shape-aware hit-testing is optional/future opt-in, not v1 default.
- **Animation (FR-7):** two tiers — render-thread composite animations (`AnimateAsync`, fling, animated scroll, spin; preferred) and the UI-thread `SkUiAnimationClock` ticked by a UI vsync ticker for arbitrary property callbacks; **time-based** progress; no layout dirty. Details: [AnimationMechanism.md](AnimationMechanism.md), [RenderingPipeline.md](RenderingPipeline.md).
- **MAUI control hosting (FR-16):** no custom Skia `SkUiWebView`; drawn `SkUiEntry` / `SkUiEditor` with an IME proxy are planned (Phase T). Host real MAUI `Entry`, `Editor`, `WebView`, and other `VisualElement`s via **`SkUiMauiContentView`**: `ISkUiView` placeholder in the SkiaUi tree; native platform view overlaid on the standalone root’s container and synced to arranged bounds (DrawnUi `SkiaMauiElement` pattern). Content property is **`Content`** (`VisualElement`, `[ContentProperty]`). Input stays with the native control; FR-15 does not own overlay hits.
- **Snapshot-during-scroll (FR-16 / FR-17):** while an ancestor scroll/fling is active, **Android and Windows** use **snapshot freeze** by default (hide native overlay, paint bitmap on Skia until motion settles); **Apple** uses **live sync only**. Apps may **opt out** per overlay for special cases (e.g. keep WebView live). Details: [ScrollingAndCollectionViews.md](ScrollingAndCollectionViews.md#overlays-while-scrolling-fr-16).
- **Scrolling / collections (FR-17):** implement **`SkUiScrollView`** (and later a virtualizing collection) **inside** the SkiaUi tree on the shared surface. Do **not** use MAUI `ScrollView` / `CollectionView` as the primary composition model for scrollable SkiaUi UI. Nesting standalone `SkUi*` under MAUI scrollers remains a documented compat path only. Details: [ScrollingAndCollectionViews.md](ScrollingAndCollectionViews.md).
- **Core layer:** low-level composition API without MAUI Controls / XAML — [CoreRequirements.md](CoreRequirements.md) (separate from this MAUI-compatible contract).

## Tracking

When a requirement is completed, check it off here and summarize the delivered behavior in [Development.md](../../Development.md).

Delivery tracking: **[ImplementationPlan.md](ImplementationPlan.md)** (**Completed** vs **To be implemented**).
