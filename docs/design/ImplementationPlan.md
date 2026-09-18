# SkiaUi implementation plan

Delivery tracking for [Requirements.md](Requirements.md). Design details live in the mechanism docs linked from [Development.md](../../Development.md).

**Workflow reminder (NFR-2):** prefer **simple, correct** code first; expand tests; then **optimize** hot paths for speed and near-zero allocations.

---

## Completed

Shipped library surface (headless-tested). **Device acceptance** for visual/overlay checks remains open — see [Testing.md](Testing.md) and [Development.md](../../Development.md).

### Core pipeline

| Area | Delivered |
| --- | --- |
| Host | `ISkUiView : IView`, `SkUiView`, `SkUiContentView`, custom handler + `HwAccelerated` (FR-1 / FR-13 / FR-14) |
| Layout base | Handler-independent measure/arrange; `SkUiLayout` overlay |
| Drawing primitives | `SkUiBox`, `SkUiEllipse`, `SkUiLine` |
| Events | Shared tap path + `Tapped` / `TappedCommand` (FR-15 v1) |
| Animation | `SkUiAnimationClock` — paint / render-transform tiers (FR-7 v1) |
| Tests | [PipelineTests](../../tests/MauiSkiaUi.Tests/PipelineTests.cs) (frame renderer, paint, input, clock, primitives) |

### Layouts & scroll

| Type | Notes |
| --- | --- |
| `SkUiGrid` | MAUI `GridLayoutManager` |
| `SkUiVerticalStackLayout` / `SkUiHorizontalStackLayout` | MAUI stack managers |
| `SkUiAbsoluteLayout` | MAUI `AbsoluteLayoutManager` + attached bounds/flags |
| `SkUiScrollView` | Clamped offsets, pan/fling, wheel, picture cache (FR-17 v1) |
| `SkUiBorder` | Rounded rect fill/stroke/clip |

Tests: [LayoutTests](../../tests/MauiSkiaUi.Tests/LayoutTests.cs), [ScrollViewTests](../../tests/MauiSkiaUi.Tests/ScrollViewTests.cs).

### Basic controls

| Type | Notes |
| --- | --- |
| `SkUiLabel` | Plain LTR text, wrap/truncate, fonts (system names) |
| `SkUiButton` | Intrinsic tap, commands, chrome, visual states |
| `SkUiImage` / `SkUiImageButton` | Async decode; ImageButton adds tap/chrome |
| `SkUiActivityIndicator` | Clock-driven; stops on detach |
| `SkUiSwitch` / `SkUiCheckBox` / `SkUiRadioButton` | Shared toggle base; RadioButton select-only |

Tests: [BasicControlsTests](../../tests/MauiSkiaUi.Tests/BasicControlsTests.cs).

### Native hosting

| Type | Notes |
| --- | --- |
| `SkUiMauiContentView` | FR-16 native overlay (`Editor` / `WebView` demo); v1 live-sync (no snapshot-during-scroll) |

Tests: [MauiContentViewTests](../../tests/MauiSkiaUi.Tests/MauiContentViewTests.cs).

### Look, colors, Core, demos

| Area | Delivered |
| --- | --- |
| Control look / color scheme | FR-18 / FR-19 (`SkUiLook`, `SkUiColorScheme`) |
| Core layer | `MauiSkiaUi.Core` layouts + basic controls (Grid/ScrollView deferred on Core) |
| Gallery | Per-control demo pages + Composition / Look & colors / Stress / Primitives |
| Docs | Per-control markdown under [docs/controls/](../controls/README.md) |
| Perf measurement | [PerformanceTests](../../tests/MauiSkiaUi.Tests/PerformanceTests.cs) (1,000-label recording) |

### Explicitly deferred from completed work

- `SkUiFlexLayout`
- Snapshot-during-scroll for overlays (FR-16/17)
- Full double-tap / long-press / swipe / multi-touch
- Drawn-tree accessibility / keyboard
- Shape-aware hit-testing
- NuGet packaging polish beyond current metadata (ongoing)

---

## To be implemented

### Controls & layouts

| Item | Notes |
| --- | --- |
| **`SkUiFlexLayout`** | MAUI FlexLayout parity via layout manager |
| **`SkUiExpander`** | MCT-like header + collapsible content |
| **`SkUiStateContainer`** | MCT-like loading / empty / error / success |
| **`SkUiSwipeView`** | List-row swipe actions; needs FR-15 capture |
| **Label + rounded border pattern** | Prefer `SkUiBorder` + Label; document vs MAUI Label |
| **Virtualizing collection** | FR-17 collection view (beyond ScrollView) |

### Mechanisms (open items in design docs)

| Area | See |
| --- | --- |
| Gesture bubbling, multi-touch, capture details | [EventMechanism.md](EventMechanism.md) |
| Optional layout animation tier | [AnimationMechanism.md](AnimationMechanism.md) |
| Overlay snapshot-during-scroll; nested scroll | [ScrollingAndCollectionViews.md](ScrollingAndCollectionViews.md) |
| Opt-in paint caches beyond full-tree redraw | [DrawingMechanism.md](DrawingMechanism.md) |
| Per-tree look attachment; OS theme sync helpers | [ControlLook.md](ControlLook.md), [ColorScheme.md](ColorScheme.md) |
| Core: Grid, ScrollView, dependency-clean package split | [CoreRequirements.md](CoreRequirements.md) |

### Verification & packaging

| Item | Notes |
| --- | --- |
| On-device acceptance | Native overlay position, contrast, input — [Testing.md](Testing.md) checklists |
| NuGet readiness | FR-6 packaging / publish pipeline |
| Windows TFM verification | Compile/run on a Windows host when available |

---

## Cross-cutting

| Topic | Expectation |
| --- | --- |
| Correctness → optimize | NFR-2 on every new hot path |
| Extensibility | Virtual hooks / interfaces / shared helpers (NFR-4) |
| Testing | Grow coverage by **functionality** ([Testing.md](Testing.md)); classes named `*Tests` by area |
| Tracking | Check off [Requirements.md](Requirements.md); summarize shipped behavior in [Development.md](../../Development.md) |

## Suggested order for new work

1. Types + handler / host wiring  
2. Measure / arrange  
3. Paint  
4. Events / gestures  
5. Animation (when in scope)  
6. Demo page  
7. Tests (add to the matching `*Tests` class or a new area-named file)  
8. Optimize / docs  
