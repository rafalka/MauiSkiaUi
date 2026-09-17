# Core layer requirements

Requirements for the **SkiaUi Core** layer (`MauiSkiaUi.Core`): a separate, low-level tree for composing complex UI and new controls from primitives **without** MAUI control / XAML overhead.

The MAUI-compatible surface (`SkUiLabel`, `SkUiGrid`, …) remains the primary drop-in API and is specified in [Requirements.md](Requirements.md). This document covers Core only.

**Status:** Core layouts (Absolute, stacks, overlay, ContentView/Border) and basic controls (Label, Button, toggles, Image/ImageButton, ActivityIndicator, shapes) exist under `MauiSkiaUi/Core/` (same assembly today). Grid and ScrollView are deferred. Stress results below motivate promoting Core to a first-class, dependency-clean layer. Items are **not complete** unless checked and summarized in [README.md](README.md).

## Motivation (measured)

Stress harness (1,000 absolute-layout buttons, HW on), same visual task:

| Layer | Generate UI | Add to page | First layout+frame | Overall → idle |
| --- | ---: | ---: | ---: | ---: |
| **Core** | 15.6 ms | 2.9 ms | 271.2 ms | 289.9 ms |
| **MAUI-compatible** | 196.9 ms | 73.4 ms | 344.8 ms | 615.3 ms |

Generate is ~**12×** faster on Core (no `View` / `BindableObject` per cell). That gap is the primary reason Core exists.

## Goals

1. **Composition substrate** — build complex controls and dense trees from Core primitives (label, button, shapes, layouts), not from MAUI `View` instances.
2. **Zero MAUI Controls dependency** — Core must not reference `Microsoft.Maui.Controls`, handlers, `BindableObject`, `Element`, `View`, or `IView`.
3. **No XAML / Styles / MAUI Binding** — Core is code-first. No `[ContentProperty]`, no resource styles, no `BindableProperty`.
4. **Observable for app / control authors** — Core raises **property change notifications** via `INotifyPropertyChanged` on `SkUiCoreNode`; see FR-C5.
5. **Fluent API** — ergonomic `Set*` chaining for tree construction (stress / factories / control ctors).
6. **Single implementation of paint & measure** — MAUI-compatible controls **delegate** to Core (or a shared Core engine) so Label/Button/etc. are not duplicated. Shared chrome geometry / default sizes use one **control look** ([FR-18](Requirements.md#fr-18--control-look-shape--chrome--default-sizes-not-maui-styles) / [ControlLook.md](ControlLook.md)); shared default colors use one **color scheme** ([FR-19](Requirements.md#fr-19--color-scheme-shared-default-palette-not-maui-styles--not-look) / [ColorScheme.md](ColorScheme.md)).

Non-goals for Core:

- Drop-in replacement for MAUI pages/XAML (that remains `SkUi*`).
- Replacing `SkUiMauiContentView` / native overlays.
- Reusing MAUI `GridLayoutManager` / `AbsoluteLayoutManager` inside Core (Core layouts are owned implementations).

## Architecture

```
MAUI page / SkUiScrollView / SkUiLayout
└── SkUiCoreHost : SkUiView          ← only bridge type (lives in MauiSkiaUi)
        └── Content : ISkUiCoreNode  ← Core tree (no View identity)
                └── SkUiCoreAbsoluteLayout
                        ├── SkUiCoreButton
                        └── …

Complex control authoring (library or app):
└── SkUiFancyChart : SkUiView        ← public MAUI-compatible control (optional)
        └── owns SkUiCore* subtree   ← internal Core composition (or expose via Host)
```

**Sharing with MAUI-compatible controls (preferred):**

```
SkUiCoreLabel          ← public Core type; owns fields, Measure, Paint, Set*, INPC
     ▲
     │ composition (delegate)
SkUiLabel : SkUiView   ← BindableProperty → core.Set*; Measure/Paint → core
```

Do **not** make `SkUiLabel` inherit `SkUiCoreLabel` (irreconcilable bases: `View` vs Core node).

## Type naming

| Layer | Pattern | Examples |
| --- | --- | --- |
| Core | `SkUiCore*` / `ISkUiCore*` | `SkUiCoreNode`, `SkUiCoreLabel`, `SkUiCoreAbsoluteLayout` |
| Bridge | `SkUiCoreHost` | Hosts one Core root inside MAUI-compatible tree |
| MAUI-compatible | `SkUi*` (unchanged) | `SkUiLabel`, `SkUiButton`, `SkUiAbsoluteLayout` |

Namespace: `MauiSkiaUi.Core`.  
Target packaging: prefer a **separate project/assembly** (`MauiSkiaUi.Core`) referenced by `MauiSkiaUi` (see FR-C1).

---

## Functional requirements

### FR-C1 — Separate assembly, no MAUI Controls

- [ ] Move Core types into project **`MauiSkiaUi.Core`** (or equivalent) that does **not** set `UseMaui` and does **not** reference `Microsoft.Maui.Controls`.
- [ ] **Forbidden references:** `Microsoft.Maui.Controls`, MAUI handlers, `BindableObject` / `Element` / `VisualElement` / `View`, MAUI `IView` / layout managers.
- [ ] **Allowed references:** `SkiaSharp`; BCL; optionally **`Microsoft.Maui.Graphics`** only for DIP primitives (`Size`, `Rect`, `Point`, `Thickness`, `Color`) shared with the host — **or** Core-owned DIP aliases with conversion at `SkUiCoreHost` (open decision below).
- [ ] `MauiSkiaUi` references Core and owns **`SkUiCoreHost`** plus all MAUI-compatible wrappers.
- [ ] Public Core API remains usable from apps that only need the host bridge (document NuGet surface: single package vs split).

### FR-C2 — Role and tree rules

- [ ] Core is for **complex UI / custom controls / dense lists**, not for XAML page markup.
- [ ] Core layouts accept **only** `ISkUiCoreNode` children (never `ISkUiView` / MAUI views).
- [ ] MAUI-compatible layouts accept **only** `ISkUiView` children (unchanged). Mixing requires **`SkUiCoreHost`**.
- [ ] Core nodes support parent/child attach rules, cycle detection, and detach (no MAUI logical tree).

### FR-C3 — Layout, paint, touch (Core-owned)

- [ ] Measure / arrange / paint / touch use **DIPs**, same semantics as the host surface.
- [ ] Implement Core layouts **without** MAUI layout managers:
  - [x] Absolute (+ proportional flags) — prototype
  - [x] Vertical / horizontal stack (+ overlay)
  - [ ] Grid (Auto / absolute / `*`) — port algorithm or simplify; do not call `GridLayoutManager`
  - [ ] ScrollView — deferred (host Core under MAUI-compatible `SkUiScrollView` for now)
- [x] Invalidation: per-node dirty flags; `StartUpdating` / `EndUpdating` batching (mirror FR-10 batching, no bindables).
- [x] Layers: Background/Overlay via painters (`PaintBackground` / `PaintOverlay`); Content via virtual `OnPaintContent` (FR-9 / DrawingMechanism).
- [x] Gestures: Core-owned pointer delivery (hit-test arranged bounds); bridge maps host `SkUiTouchEvent` ↔ Core. No MAUI `GestureRecognizers`.
- [x] Primitives + basic controls (Label, Button, Border, ContentView, toggles, Image/ImageButton, ActivityIndicator, shapes). Grid/ScrollView excluded for now.

### FR-C4 — Fluent API + CLR properties (single apply path)

**Decision — fluent `Set*` is the apply path; CLR setters call `Set*`.**

| Path | Behavior |
| --- | --- |
| `SetText(value)` | Validates, updates field, raises INPC, invalidates measure/paint as needed, **returns `this`** |
| `Text { set; }` | Calls `SetText(value)` (discard fluent return) — **stays in sync** with the field |
| Construction | Prefer fluent: `new SkUiCoreLabel().SetText("Hi").SetFontSize(14)` |

Rationale:

- One place for validation + invalidation (same lesson as FR-10).
- Fluent chaining is for **builders**; property setters are for **assignment / INPC consumers**.
- Unlike MAUI FR-10, Core **must keep** property getters and fields synchronized (no intentional BindableProperty desync).

Do **not** implement “property setter returns `this`” (illegal in C#) or make fluent methods only update fields while setters duplicate logic.

Checklist:

- [ ] Every stylable Core property exposes both CLR get/set and fluent `Set*` returning the concrete type (or a generic fluent interface).
- [ ] `Set*` is the sole mutation/apply implementation; setters and any code-gen INPC helpers call it.
- [ ] Document fluent vs property usage in [docs/controls/SkUiCore.md](docs/controls/SkUiCore.md).

### FR-C5 — Property change notifications (MVVM Toolkit)

Core does **not** support MAUI `Binding` / XAML, but **does** support `INotifyPropertyChanged` so:

- parent controls can listen to child property changes;
- apps can use **code** bindings / reactive helpers;
- view-models can drive Core nodes without BindableProperty.

**Decision — hand-rolled INPC on `SkUiCoreNode` (no CommunityToolkit.Mvvm).**

Fluent `Set*` remains the apply path; Core does not use `[ObservableProperty]` generators. Commands use `System.Windows.Input.ICommand` (plus a tiny `SkUiCoreCommand` helper for `SetClicked`).

Checklist:

- [x] `SkUiCoreNode` implements **`INotifyPropertyChanged`** with protected `SetProperty` / `OnPropertyChanged`.
- [x] CLR property setters call the matching `Set*` (FR-C4).
- [x] `SkUiCoreButton` / `SkUiCoreImageButton` expose `ICommand? Command` (+ `CommandParameter`).
- [x] No CommunityToolkit.Mvvm package reference.
- [ ] Document that MAUI `{Binding}` on Core types remains **out of scope**; use code bindings or wrap with `SkUi*` for XAML.

### FR-C6 — Shared paint / measure via Core delegate

**Decision — MAUI-compatible controls compose/delegate to Core (or a Core engine type).**

```csharp
// Sketch — naming TBD
public class SkUiLabel : SkUiView
{
    private readonly SkUiCoreLabel _core = new(); // or shared engine extracted from Core

    public string Text
    {
        get => _core.Text;
        set => SetValue(TextProperty, value); // BP → SetText → _core.SetText
    }

    public SkUiLabel SetText(string? value) { _core.SetText(value); return this; }

    protected override Size MeasureContent(double w, double h) => _core.Measure(w, h);
    protected override void OnPaintContent(SKCanvas canvas) => _core.Paint(canvas);
}
```

Requirements:

- [ ] Extract / align **text measure + paint** so `SkUiLabel` and `SkUiCoreLabel` share one implementation (Core type is the source of truth).
- [ ] Same for Button chrome + label (and later Image, toggles, etc.) — prioritize high-churn controls first.
- [ ] MAUI bindable `propertyChanged` handlers call the same `Set*` that mutates the Core delegate (FR-10 remains: BP → Set*; Set* does not write back to BP).
- [ ] Core delegate lifetime: owned by the MAUI control; not inserted into a Core layout tree unless the author also builds a Core subtree.
- [ ] Avoid duplicating line-breaking / typeface resolution / chrome path code in both layers; chrome geometry and default sizes go through the shared **control look** (FR-18 / [ControlLook.md](ControlLook.md)); default colors go through the shared **color scheme** (FR-19 / [ColorScheme.md](ColorScheme.md)).
- [ ] Unit tests: Core label measure/paint golden behavior; MAUI `SkUiLabel` matches for the same inputs (delegate path).

Alternative rejected for v1: parallel “engine” structs neither public as Core nor used as nodes — prefer **public Core controls** so composition and delegation are the same types.

### FR-C7 — Host bridge

- [x] `SkUiCoreHost : SkUiView` hosts one Core root; forwards measure / arrange / paint / touch (prototype).
- [ ] Host invalidation hooks remain correct under `StartUpdating` / scroll picture caching.
- [ ] Document that Host is the **only** supported way to place Core under `SkUiScrollView` / `SkUiLayout` / pages.
- [ ] Optional later: `SkUiView` “owned visual children” API for internal Core subtrees without a public Host child (advanced; not required for v1).

### FR-C8 — Control set (Core)

Minimum public Core primitives (expand as MAUI wrappers gain delegates):

| Control | Status |
| --- | --- |
| `SkUiCoreNode` / `ISkUiCoreNode` | Prototype |
| `SkUiCoreAbsoluteLayout` | Prototype |
| `SkUiCoreLabel` | Prototype (extend: wrap, font attrs, alignment parity) |
| `SkUiCoreButton` | Prototype |
| `SkUiCoreBox` / shape primitives | Todo |
| Stack layouts | Todo |
| Grid | Todo |
| Activity indicator / toggles | Todo as needed for composition |

### FR-C9 — Demo and proof

- [x] Stress page toggle: MAUI AbsoluteLayout + `SkUiButton` vs Core AbsoluteLayout + `SkUiCoreButton`.
- [ ] Keep stress comparison as a regression gate when sharing paint/measure (FR-C6) so Generate cost does not regress toward MAUI levels for the Core path.
- [ ] Gallery snippet or docs sample: custom control built from Core primitives, hosted via `SkUiCoreHost` or wrapped as `SkUiView`.

---

## Non-functional requirements

### NFR-C1 — Performance

- Core node construction must stay far cheaper than `new SkUiView()` (order-of-magnitude Generate wins as in Motivation).
- Hot `Set*` with unchanged values: no INPC, no invalidate.
- Prefer struct dirty flags; avoid per-frame allocations in paint (align with NFR-2 in [Requirements.md](Requirements.md)).

### NFR-C2 — Quality & docs

- XML docs on all public Core types.
- [docs/controls/SkUiCore.md](docs/controls/SkUiCore.md) is the how-to; this file is the contract.
- Tests for layout math, INPC, host bridge, and MAUI↔Core measure/paint parity after FR-C6.

### NFR-C3 — Versioning

- Breaking Core API is allowed while marked experimental/prototype; stabilize before advertising as primary composition API.
- MAUI-compatible public API must not break when Core is extracted (FR-C1 / FR-C6).

---

## Out of scope (Core)

- XAML inflation of Core types  
- MAUI `Style` / `VisualStateManager` on Core  
- MAUI `Binding` / `x:Bind` to Core  
- Core reuse of MAUI layout managers  
- Hosting native Entry/Editor/WebView inside Core (use `SkUiMauiContentView` on the MAUI-compatible side)  

---

## Open decisions

~~1. **DIP types…**~~ **Decided:** allow `Microsoft.Maui.Graphics`.  
~~2. **INPC base…**~~ **Decided:** hand-rolled `INotifyPropertyChanged` on `SkUiCoreNode` (no Mvvm Toolkit).  
~~3. **Delegate shape…**~~ **Decided:** own public `SkUiCoreLabel` (etc.).  
~~4. **NuGet…**~~ **Decided for v1:** one package; in-repo Core project/assembly optional.

(No open Core decisions remaining for the initial implementation wave.)

---

## Decided (Core)

- **Purpose:** low-level composition layer for complex UI / new controls; not a XAML replacement.
- **No MAUI Controls** on Core; no XAML / Styles / BindableProperty.
- **Fluent `Set*` is the apply path;** CLR property setters call `Set*` and stay synchronized; fluent returns `this`.
- **INPC:** `SkUiCoreNode` implements `INotifyPropertyChanged`; buttons use `ICommand` / `SkUiCoreCommand`.
- **Shared paint/measure:** MAUI-compatible controls **delegate to Core** (composition), not duplicate Skia logic.
- **Core layouts are hand-implemented** (no MAUI layout managers).
- **`SkUiCoreHost`** is the bridge into the MAUI-compatible tree.
- **Naming:** keep `SkUiLabel` etc. for MAUI-compatible; Core uses `SkUiCore*` prefix.
- **Paint layers:** Background/Overlay = `PaintBackground` / `PaintOverlay` delegates; Content = virtual `OnPaintContent` only. Same on `SkUiView` and `SkUiCoreNode`.
- **DIP types:** allow `Microsoft.Maui.Graphics` in Core; forbid `Microsoft.Maui.Controls`.
- **Delegate shape (FR-C6):** MAUI control owns a public Core control instance (e.g. `SkUiCoreLabel`).
- **NuGet (v1):** ship Core inside the main `MauiSkiaUi` package; separate Core assembly in-repo is fine.

---

## Tracking

When a Core requirement ships, check it off here and note it under *Current implementation* in [README.md](README.md). Cross-link MAUI-compat work that adopts Core delegates (FR-C6) in per-control docs under [docs/controls/](docs/controls/README.md).
