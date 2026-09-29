# SkiaUi implementation plan

Delivery tracking for [Requirements.md](Requirements.md): what is shipped, and the order of the next work. Design details live in the mechanism docs linked from [Development.md](../../Development.md); architecture findings in [ArchitectureReview.md](ArchitectureReview.md).

**Workflow reminder (NFR-2):** prefer **simple, correct** code first; expand tests; then **optimize** hot paths for speed and near-zero allocations.

---

## Shipped

Headless-tested; device-verified on a Galaxy S9, iPhone / iOS simulator, Mac Catalyst and Windows 11 for gestures, scrolling, overlays and memory leaks ([Testing.md](Testing.md)).

| Area | Delivered |
| --- | --- |
| Host and rendering | `ISkUiView : IView`, `SkUiView`, `SkUiContentView`, custom handler + `HwAccelerated`; retained compositor with UI-thread recording and render-thread compositing: Metal (Apple), GL thread (Android), ANGLE / software (Windows) — [RenderingPipeline.md](RenderingPipeline.md) |
| Layouts | `SkUiGrid`, stacks, `SkUiAbsoluteLayout`, `SkUiBorder`, `SkUiContentView` on MAUI's layout managers; RTL mirroring |
| Scrolling | `SkUiScrollView` / `SkUiCoreScrollView` on one engine: render-thread fling and animated scroll, wheel, nested and same-axis chaining, native-parent coordination |
| Input | Per-pointer gesture arena for SkUi* and Core: tap, double tap, long press, pan, swipe, pinch, pointer recognizers — [EventMechanism.md](EventMechanism.md) |
| Text | Shared engine: HarfBuzz shaping, bidi / RTL, per-character font fallback, wrap / truncation, `TextRendering` fast path |
| Controls | Label, Button, Image, ImageButton, ActivityIndicator, Switch / CheckBox / RadioButton (three-state `CheckState`, FR-23), **Slider** (horizontal / vertical, FR-24), **ProgressBar** (determinate / render-thread indeterminate, FR-25), shapes — each on both layers |
| Native hosting | `SkUiMauiContentView` (FR-16): viewport clipping, snapshot while scrolling, drags from overlays handed to drawn scrollers |
| Animation | Render-thread `AnimateAsync`, fling, spin and content slide; UI-thread `SkUiAnimationClock` (FR-7) |
| State-change transitions | Look-driven toggle, press (dim / ripple), slider-thumb and progress transitions on both layers; reduce motion (`SkUiMotion`); measured on a Galaxy S9 and Mac Catalyst (FR-26, [ArchitectureReview.md](ArchitectureReview.md#state-change-animations)) |
| Look and colors | `SkUiLook` (FR-18), `SkUiColorScheme` (FR-19) |
| Core layer | Layouts (stacks, absolute, grid, table, overlay, border, scroll view) and basic controls; `SkUiCoreHost` |
| Diagnostics | Core nodes in the Live Visual Tree, `SkUiDiagnostics`, DevFlow `dev.skiaui` extension |
| Quality | Headless suite; memory-leak scenarios headless and on devices (`scripts/device_tests.sh`, CI on Mac Catalyst with Native AOT); trimmable and Native-AOT-compatible library; benchmarks (`scripts/bench.sh`) |
| Packaging | NuGet `SkiaUi.Maui`; publish workflow bumps the version ([Releasing.md](../Releasing.md)) |

---

## Next

Ordered for adoption by real apps (replace a MAUI page's tree with one drawn surface): layouts and containers for page shells first, then virtualized lists, then list chrome, then the remaining MAUI parity. Within a phase, the order is the suggested order.

Phase 0 (state-change animations, FR-26) is shipped: looks draw from continuous parameters, transitions run on the UI clock re-recording one control per frame. New controls with state visuals follow the same pattern (a paint struct with the transition, a `SkUiTransitionKind`, the shared animators). Left open: several ripples at once, press scale, render-thread painters.

### Phase A — Page shells: composition layouts and containers

| # | Deliverable | Layer | Why |
| --- | --- | --- | --- |
| A1 | **Wrap layout** (`SkUiCoreWrapLayout`, then `SkUiWrapLayout`) | Core first | Chips, tag and filter rows; not covered by stock MAUI layouts |
| A2 | **Weighted / stretch stack** (children share leftover space by weight) | Core + SkUi* | Common app layout; star rows without a Grid |
| A3 | **`SkUiStateContainer`** (loading / empty / error / content) | Core + SkUi* | Community Toolkit parity; busy and skeleton screens |
| A4 | **`SkUiExpander`** (header + animated collapsible content) | Core + SkUi* | Community Toolkit parity; also hosts native content (e.g. a WebView) |
| A5 | **Hosted-control regression suite** | Tests + device checklist | Entry / Editor / WebView in drawn scrollers: focus, IME, scroll nesting, snapshots |
| A6 | **Hardening from adoption** | Both | Label, Grid, Border, ScrollView bugs found while porting real pages |

### Phase B — Virtualized lists (FR-21, FR-22)

| # | Deliverable | Notes |
| --- | --- | --- |
| B1 | **`SkUiVirtualStackLayout`** (+ virtual scroll) | Vertical first; fixed-extent fast path; estimate + anchoring for variable sizes; recycling; prefetch |
| B2 | **`SkUiCollectionView` MVP** | `ItemsSource` + `ItemTemplate` / selector, single selection, `ItemTapped` / command, header / footer / `EmptyView`, `RemainingItemsThreshold`, pull-to-refresh (`IsRefreshing` / `RefreshCommand`) |
| B3 | **Phase 2** | Grouping, sticky group headers, grid layout, multiple selection, horizontal |

### Phase C — List chrome and text

| # | Deliverable | Notes |
| --- | --- | --- |
| C1 | **`SkUiSwipeView`** | Row actions; competes with vertical scrolling through the arena |
| C2 | **`SkUiRefreshView`** / pull-to-refresh on scroll views | If not already delivered with B2 |
| C3 | **Label auto-fit** (shrink to fit, fit number) | After the measure cache is proven for it |
| C4 | **Tile / wrap-grid layout** | Dashboard tiles |
| C5 | **Label spans** (`FormattedString`) | MAUI parity |

### Phase D — Remaining MAUI parity and polish

| # | Deliverable |
| --- | --- |
| D1 | `SkUiFlexLayout` (MAUI `FlexLayoutManager`) |
| D2 | Carousel + `IndicatorView` (horizontal virtual list with snapping) |
| D3 | Shadows (FR-20) |
| D4 | Scroll polish: scrollbars, bounce, snap points |
| D5 | Shapes: Path, Polygon, Polyline, Rectangle / RoundRectangle |
| D6 | Image cache integration hooks |
| D7 | Stepper; Slider `ThumbImageSource` and step |

### Architecture work alongside

From [ArchitectureReview.md](ArchitectureReview.md) (finding numbers N\*). Scheduled next to the phases above, not after them.

- **Now (small, correctness):** disabled controls must not block ancestor scrolling (N2); Core `IsEnabled` / `InputTransparent` with one blocking rule (N3); child alignment in every Core container (N4); look / color-scheme swaps invalidate retained pictures (N6).
- **With Phase A:** measure invalidation without re-recording ancestors (N5); containers that re-measure often (wrap layout, expander) benefit first.
- **Before Phase B:** relayout boundaries; shared image cache (N9); fling live extents (N11); raster cache of stable subtrees (N7).
- **Accessibility:** semantics tree mapped to platform accessibility (Android `ExploreByTouchHelper`, iOS accessibility elements), OS font scaling, keyboard focus, reduce-motion (N8). Needed before broad production use.
- **Drawn over native:** overlay masks, so drawn popups can cover hosted controls; cheaper overlay bookkeeping on Android (N10, N12).
- **One implementation per control:** extract layer-agnostic engines and shared node mechanics, gated by SkUi-vs-Core parity tests (N13). Toggle drawing and all transition animators are already shared (`SkUiToggleDrawing`, `SkUiTransitionAnimators`).

---

## MAUI parity at a glance

| MAUI | SkiaUi | Status |
| --- | --- | --- |
| Label, Button, Image, ImageButton, ActivityIndicator, BoxView, Ellipse, Line | SkUi* + Core | Done (Label spans: C5) |
| CheckBox, Switch, RadioButton | SkUi* + Core | Done, plus three states |
| Slider, ProgressBar | SkUi* + Core | Done, plus vertical / indeterminate |
| Grid, VerticalStackLayout, HorizontalStackLayout, AbsoluteLayout, Border, ContentView | SkUi* + Core | Done |
| ScrollView | SkUi* + Core | Done (polish: D4) |
| FlexLayout | — | D1 |
| CollectionView (ListView, TableView map here) | — | B1–B3 |
| RefreshView, SwipeView | — | C1, C2 |
| CarouselView, IndicatorView | — | D2 |
| Path, Polygon, Polyline, Rectangle, RoundRectangle | Box with corner radius covers rectangles | D5 |
| Stepper | — | D7 |
| Shadow | — | D3 |
| Entry, Editor, SearchBar, WebView, pickers, Map, media | Hosted (`SkUiMauiContentView`) | By design |
| Pages, Shell, navigation | MAUI | Out of scope |

**Not planned (by design):** drawn Entry / Editor / WebView / media / maps (host them); Shell and navigation replacements; ListView cell API ports; vendor control clones.

---

## Acceptance checks

| Item | Must verify |
| --- | --- |
| Wrap layout | Wraps across width; re-measures when a child's size changes; RTL |
| Weighted stack | Weighted children fill the leftover space; fixed children keep their size |
| StateContainer | Switching states releases the previous content (leak scenario) |
| Expander | Header tap toggles with animation; nested in a scroll view; hosted native child |
| Virtual stack | No blank frames while flinging on a device; recycling without per-item allocations; memory flat after release |
| CollectionView MVP | Template recycling; selection + `ItemTapped`; `EmptyView`; load-more threshold; pull-to-refresh |
| SwipeView | Wins horizontal swipes, loses vertical scrolls |
| Hosted controls | Entry focus + IME; WebView scroll nesting; snapshots during flings (Android / Windows) |

---

## Cross-cutting

| Topic | Expectation |
| --- | --- |
| Correctness → optimize | NFR-2 on every new hot path |
| Both layers | New controls ship on SkUi* and Core with the same look entry points |
| Extensibility | Looks draw every control; virtual hooks / interfaces / shared helpers (NFR-4) |
| Testing | Headless tests by functionality ([Testing.md](Testing.md)); a leak scenario for every new stateful control; demo page per control (enforced by `ComponentDemoTests`) |
| Trimming / AOT | No reflection; analyzers fail the build |
| Tracking | Check off [Requirements.md](Requirements.md); summarize shipped behavior in [Development.md](../../Development.md); changelog under `## Unreleased` |

## Suggested order for a new control

1. Look entry point (`SkUiLook.Draw*` / `Measure*`, paint struct carrying the transition state, `SkUiTransitionKind` if it has state visuals, default look)
2. Types on both layers, shared math / input helpers
3. Measure / arrange, paint
4. Input through the gesture arena (write user changes back to bindables)
5. Animation: state changes through the shared transition animators; continuous motion on the render thread when possible
6. Demo page, docs page under [docs/controls/](../controls/README.md)
7. Tests, leak scenario, then optimize
