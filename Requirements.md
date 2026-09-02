# Requirements

Planned work for SkiaUi. Items here are **not yet implemented** unless moved into [README.md](README.md) under *Current implementation*.

## Goals

Build a set of **base controls and layouts drawn entirely with SkiaSharp**, using **GPU / hardware acceleration** where the platform supports it (`SKGLView`).

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
└── SkUiContentView : SKGLView          ← only MAUI ↔ Skia bridge
        └── Content : ISkUiView         ← root of the SkiaUi tree (XAML content)
                ├── ISkUiView (control / layout)
                └── ISkUiView ...
```

### Bridge: `SkUiContentView`

- Derives from SkiaSharp’s **`SKGLView`** (OpenGL / GPU-backed surface when available).
- Exposes a **`Content`** property of type **`ISkUiView`**, marked as the XAML `[ContentProperty(nameof(Content))]` so a single child element becomes the root.
- Is the **only** type that participates in the MAUI layout / input system for SkiaUi content.
- Forwards the following to `Content` (and through the SkiaUi tree as needed):

| MAUI / SKGLView concern | Forwarded to `ISkUiView` as |
| --- | --- |
| Available size / constraint changes | **Measure** |
| Final arranged bounds | **Layout** |
| `PaintSurface` (GL) | **Paint** |
| `Touch` (`EnableTouchEvents`) | **Touch** |

- Enables touch (`EnableTouchEvents = true`).
- Invalidates the GL surface when the SkiaUi tree requests a redraw.
- Converts MAUI density / pixel sizes into the coordinate space used by `ISkUiView` (document the chosen unit: device-independent vs pixels).

### Contract: `ISkUiView`

Every SkiaUi control and layout implements `ISkUiView`. Proposed surface (names may be refined during implementation):

- **Measure** — given constraints, return desired size.
- **Layout** — given final rectangle, position self / children.
- **Paint** — draw into the provided `SKCanvas` (and related GL paint args as needed).
- **Touch** — handle pointer / touch; return whether the event was handled (for hit-test bubbling).

Layouts are `ISkUiView` implementations that own child `ISkUiView` instances and participate in the same measure / layout / paint / touch pipeline. Controls do **not** inherit MAUI `View`; they are pure SkiaUi nodes hosted under `SkUiContentView`.

### XAML object model

- Concrete control/layout types (e.g. `SkUiGrid`, `SkUiLabel`) are instantiable from XAML (public parameterless constructors, bindable or settable properties as needed).
- Layout types expose a child collection (e.g. `Children`) as their `[ContentProperty]`, so nested elements in XAML populate the SkiaUi tree.
- Attached layout properties (e.g. grid row/column) are supported where the layout requires them.
- XML namespace mapping is documented for apps (clr-namespace / `xmlns`); optional `XmlnsDefinition` for a shorter xmlns.
- Same trees must remain creatable from C# for parity.

### Acceleration

- Prefer **`SKGLView`** over `SKCanvasView` for the host bridge so painting can use HW acceleration.
- Document platform fallbacks if GL is unavailable (future option: software `SKCanvasView` host); not required for the first milestone if `SKGLView` covers primary targets.

## Functional requirements

### FR-1 — Core bridge and contract

- [ ] Define `ISkUiView` with Measure, Layout, Paint, and Touch.
- [ ] Implement `SkUiContentView : SKGLView` with `Content` (`ISkUiView`) and `[ContentProperty]`.
- [ ] Wire `OnPaintSurface` / `OnTouch` (and size changes) to the content tree.
- [ ] Register SkiaSharp MAUI handlers (`UseSkiaSharp`) from a library entry point; demo calls it from `MauiProgram`.
- [ ] Remove template placeholders (`Class1`, platform stubs) once the public API exists.
- [ ] XML docs on all public types and members.

### FR-2 — XAML composition

- [ ] Entire SkiaUi subtree under `SkUiContentView` can be authored in XAML (nested layouts and controls).
- [ ] Layouts use a content-property child collection so markup like `<SkUiGrid><SkUiLabel .../></SkUiGrid>` works.
- [ ] Demo (or sample page) shows the target markup pattern with MAUI parents outside and SkiaUi inside the bridge.
- [ ] Document the `xmlns` to use for `MauiSkiaUi` types.

### FR-3 — Base layout primitives

- [ ] At least one layout `ISkUiView` suitable for XAML nesting (e.g. `SkUiGrid` or stack) that measures / arranges children and paints / hit-tests them in z-order.
- [ ] Clear invalidation rules: property or structure changes request redraw (and remeasure when needed).

### FR-4 — Base controls

- [ ] Initial control set drawn entirely via Skia (no nested MAUI visuals inside the Skia tree), including at least `SkUiLabel` (or equivalent text control).
- [ ] Each control implements `ISkUiView`, is XAML-constructible, and works under `SkUiContentView`.

### FR-5 — Demo gallery

- [ ] Demo hosts `SkUiContentView` with sample trees defined primarily in XAML.
- [ ] Gallery pages for layouts and controls.
- [ ] Verified on Android and at least one Apple target (iOS or Mac Catalyst).

### FR-6 — Packaging readiness

- [ ] Stable root namespace (`MauiSkiaUi` or agreed name).
- [ ] NuGet metadata when publishing is required.

## Non-functional requirements

### NFR-1 — Platforms

- Primary: `net10.0-android`, `net10.0-ios`, `net10.0-maccatalyst`
- Secondary (when developing on Windows): `net10.0-windows10.0.19041.0`

### NFR-2 — Performance

- Hot paint / touch paths minimize allocations.
- Redraw only when invalidated; optional `HasRenderLoop` only for continuous animation scenarios.
- Layout passes should be avoidable when constraints and tree are unchanged.

### NFR-3 — Quality

- Nullable reference types enabled.
- Public API documented with XML comments.
- Small, focused controls; composition via layouts rather than monolithic views.

## Out of scope (for now)

- Full design-system parity with native MAUI controls
- Embedding MAUI views inside the SkiaUi tree
- Blazor Hybrid / multi-project MAUI host
- Software-only fallback host (unless `SKGLView` proves insufficient on a target)

## Reference sources

Use these local checkouts when designing or implementing SkiaUi (layout, input, XAML, rendering patterns). Prefer patterns that fit the `SkUiContentView` → `ISkUiView` model; do not copy frameworks wholesale.

| Framework | Local path | Typical relevance |
| --- | --- | --- |
| .NET MAUI | `/Users/rkukla/devel/maui/maui` | Host integration, XAML/`ContentProperty`, gesture/handler patterns |
| Flutter | `/Users/rkukla/devel/flutter` | Render-object style measure/layout/paint, hit-testing |
| Avalonia | `/Users/rkukla/devel/Avalonia` | Visual tree, layout, XAML controls outside MAUI |
| Uno Platform | `/Users/rkukla/devel/uno` | Cross-platform control/layout and composition approaches |
| DrawnUi (MAUI) | `/Users/rkukla/devel/maui/drawnui` | Skia-drawn MAUI controls; closest prior art for a drawn UI layer |

When borrowing an idea, note the source briefly in design discussion or code comments where non-obvious.

## Open decisions

- Exact method signatures and types for Measure / Layout / Paint / Touch (Skia vs custom size/rect/touch args)
- Coordinate system: DIPs vs physical pixels, and how density is applied in `SkUiContentView`
- Hit-test / touch routing (capture, bubbling, multi-touch)
- Naming: `SkUi*` vs `MauiSkiaUi*` for public types (markup examples use `SkUi*`)
- Whether layout children are `IList<ISkUiView>` only or also support a single-child content layout
- BindableProperty usage on SkiaUi nodes vs plain CLR properties for XAML/bindings
- Visual style tokens / theming
- NuGet publish now vs in-repo only

## Tracking

When a requirement is completed, check it off here and summarize the delivered behavior in [README.md](README.md).
