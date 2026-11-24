# SkiaUi 1.17 implementation plan: carousel parity, segmented control and tab view

**Goal:** the carousel closes its last MAUI gaps and gains what apps build around it by hand: MAUI's `VisibleViews`, autoplay, several items per snap point, and a carousel on the Core layer. On the same carousel engine, the review's segmented control and tab view (F1): a one-of-many selector with a sliding selection on both layers, and a tab strip over panes that are created when first shown and swiped on the render thread.

**Status:** proposed 24 November 2026 on `1.17-implementation`. The decisions below are proposed; each is agreed before the step that depends on it starts.

**Scope:** the carousel item of the backlog's [MAUI parity](Roadmap.md#maui-parity) and F1 of the [review plan](ImplementationPlan-PerformanceAndControls.md#f1-segmented-control-and-tab-view), which this plan replaces where the two differ. The review's chips (F5) and range slider and rating (F6), planned for 1.17 by the [1.16 plan's D1](ImplementationPlan-1.16.md#decisions), are not in it ([D1](#decisions)).

**Baseline:** 1.16.0 (net10 with MAUI 10.0.130 and net11 with MAUI 11.0.10, SDK 11.0.100, SkiaSharp 4.154.0).

**Rules** (from [Requirements.md](Requirements.md#principles)):

- 1.x keeps the 1.0 API: everything here is additive. The engine extraction in C1 is internal.
- Both layers: the carousel, the segmented control and (at the cut line) the tab view each get a Core twin on one shared engine.
- NFR-2: swiping a carousel or a tab view records nothing and allocates nothing per frame on either thread; `VisibleViews` and the strip's indicator add nothing per frame while the set of views in view stays the same.

## Decisions

Proposed 24 November 2026.

- **D1 1.17 is the carousel and F1.** Chips (F5) and range slider and rating (F6) leave 1.17. They go back to the backlog after the sheet (F3, 1.18), and their release is decided when 1.18 is planned. The carousel work comes first because the tab view and the Core carousel both need its engine ([D2](#decisions)).
- **D2 One carousel engine, three users.** The carousel's layout logic lives today in its private `ItemsPanel` (a `SkUiView`): item geometry, the strip of copies that loops, moving back by whole cycles, snap offsets, the current index and the window of realized slots. It moves to an internal `SkUiCarouselEngine` with no view types (as `SkUiIndicatorLayout` and the planned `SkUiSegmentedEngine`), used by `SkUiCarouselView`, `SkUiCoreCarouselView` and `SkUiTabView`. What stays per user: creating, recycling or keeping item views, visual states, and the render-thread effect links.
- **D3 The tab view keeps its panes and is not a carousel inside.** The review's brief built the tab view as a segmented strip over a `SkUiCarouselView`. A carousel treats its items as data and recycles their views per template, which loses a pane's state (scroll offset, text typed into a field) when it scrolls out. The tab view uses the engine ([D2](#decisions)) with a pane host of its own: a pane is created when it first comes into view (selected, or a drag or fling reaching it), not before, and is kept after that; `ContentTemplate` panes are created once. No looping, `MandatorySingle` snapping, bounce at the ends.
- **D4 `VisibleViews` is MAUI's.** A read-only bindable property (`VisibleViewsProperty`) of type `ObservableCollection<View>`, as on MAUI's `CarouselView`: the template root views whose bounds intersect the viewport, in strip order, not the extra views realized on either side. With an item effect, the views the effect draws (its visible range, opacity above 0). Copies of an item in a looping carousel are separate views and each is listed. It is compared with the previous set after layout and on each scroll report (render-thread flings report every frame) and changed only when the set changes: one `Remove` / `Insert` per view leaving or entering, never `Clear` and refill. The parity test's exemption goes.
- **D5 Several items per snap point: `ItemsPerSnapPoint`** (SkiaUi, `int`, default 1, at least 1).
  - **Snap points** are every N items from the first. In a loop each cycle starts again at its first item, so pages stay the same on every round (10 items, 3 per page: 1–3, 4–6, 7–9, 10, then 1–3); the short last page is a short step. `Mandatory` snaps to the nearest page; `None` ignores N.
  - **Size:** with `ItemExtent = 0` (fill), N items fill the viewport minus `PeekAreaInsets` and the spacing between them. With a positive `ItemExtent` the extent wins and N only sets the snapping.
  - **Position** stays the index of the item at the snap point (MAUI's meaning), so it is always a page's first item. Setting it to another item scrolls to that item's page and writes back the page's first item (coerced first, then `WriteBack`). Arrow keys and screen readers' scroll actions step a page.
  - **Indicator:** a linked indicator view counts pages (count ÷ N, rounded up); its position is the page, and a tap on a dot scrolls to that page.
- **D6 Autoplay: `AutoPlayInterval`** (SkiaUi; MAUI has none; `TimeSpan`, default `Zero`: off).
  - Each interval the carousel steps one snap point forward in the reading direction, animated as `ScrollToIndex(…, true)`. At the end of a carousel that does not loop it goes back to the first item, animated.
  - **It waits** while a drag, fling or scroll is in progress, and for a full interval after the carousel comes to rest (the timer restarts); while a mouse or pen hovers it; while keyboard focus or a screen reader's focus is inside it; while the carousel is not shown (not in a window, hidden itself or through an ancestor, the surface not visible, the app in the background). These pauses and `IsAutoPlayPaused` (settable, for an app's pause button) are what WCAG 2.2.2 asks of content that moves by itself.
  - **Reduce motion:** steps still happen, without the slide (a jump to the next page). A screen reader running pauses autoplay altogether, as content changing under its focus is lost.
  - **The timer** runs on the UI thread (it changes `Position`, which is UI state) through an internal timer source of the surface; headless tests drive a manual one, since they have no dispatcher. No render-thread state.
- **D7 The Core carousel takes an item factory, not a `DataTemplate`.** `SkUiCoreCarouselView.SetItemFactory(create, bind, key = null)`: `Func<object?, SkUiCoreNode>` creates a node, `Action<SkUiCoreNode, object?>` binds an item to it (called again when the node is recycled), and an optional `Func<object?, object>` gives the recycling key (the template selector's role). Without a factory, a label with the item's text. It is the first Core list that recycles; the virtual stack's Core twin on the backlog reuses the same factory shape. Core item views have no visual states (Core has no `VisualStateManager`); `bind` can read `Position`.
- **D8 The strip's indicator follows the panes on the render thread** through 1.16's scroll links (`SkUiRenderLink` channels), with the pane scroller as the source: a tab view swiped or flung moves its underline in step with no UI-thread work per frame. With equal tab widths that is one `TranslationX` channel and the width is constant. With content widths both the position and the width are piecewise linear in the offset (one piece per pair of neighbor tabs): an internal channel kind with one break point per tab (a `float[]` built when the tabs or their widths change), since the public effect's 33-point easing table cannot hit each tab's edges exactly. The width follows as a `ClipWidth` of a full-width bar, so the indicator's rounded ends keep their radius. Not public API.
- **D9 Looks.** The segmented control and the tab strip are drawn by every look, from the platforms' guidelines (MAUI has no native counterpart to measure against): default (a track and a sliding pill), iOS 26 (a capsule track with a raised thumb), Material 3 (outlined segmented buttons with a check on the selected ones; the selection fades instead of sliding; a tab strip with an underline the width of its label), Material 2, Mac Catalyst and Windows (segments with a sliding accent bar). Sizes and transitions come from `SkUiLook` as for other controls.
- **D10 Measure the duplication** (the review's [D3](ImplementationPlan-PerformanceAndControls.md#decisions)): the PR reports how many lines the SkUi\* and Core twins of the segmented control and of the carousel write twice. The source generator spike is decided from those numbers after 1.17.
- **D11 Release:** `1.17.0`, no prerelease, unless C1 shows a slower swipe in the carousel bench that the extraction cannot win back.

## Facts that shape the plan

- **The carousel today** is `SkUiCarouselView.cs` (1,310 lines) and `SkUiCarouselView.Parts.cs` (778): a `CarouselScroller` (a `SkUiScrollView`), the `ItemsPanel` (implements `ISkUiSnapPointSource` and `ISkUiScrollListener`, holds the geometry, the loop strip, recentering, the realized window, the recycling pool per template key and the visual states) and `ItemCell` hosts whose `OnGetRenderProps` applies the item effect (`SkUiItemEffectLink`, `SortsChildrenByDepth`). `GetRealizedView(index)` is public; `VisibleViews` is exempt in `MauiApiParityTests.cs:117`.
- **The scroll engine is shared:** `SkUiScrollController`, snap point sources (`ISkUiSnapPointSource`, internal) and the render-thread flings serve `SkUiScrollView` and `SkUiCoreScrollView` alike; `SkUiCoreScrollView` already has `SnapPointsType` and `SnapPointsAlignment`. A Core carousel needs no new scroll code.
- **Item effects are already layer-neutral:** `SkUiCarouselEffect` (and `SkUiCoverFlowEffect`, `SkUiScaleEffect`) is a plain class evaluated by the compositor from the scroll offset.
- **The indicator has both layers:** `SkUiIndicatorView` follows a linked carousel's fractional position (`Follow(scrollPosition, loops)`); `SkUiCoreIndicatorView` is driven by `Position` and `Count` and has no link yet.
- **No Core list recycles today:** the virtual stack's Core twin is on the backlog. Core pickers take a `DataTemplate` for their popups only.
- **Panes created on demand** have a precedent: `SkUiContentView.ContentLoading="WhenShown"` creates its template only once shown.
- **Render-thread following** of a scroller is 1.16's `SkUiRenderLink` channel list (translation, scale, opacity, clips), evaluated by the compositor against the source's offset, with the UI side evaluating the same values for hit-testing and semantics.
- **Headless tests have no dispatcher** ([Testing.md](Testing.md#writing-tests-for-ci)); anything timed needs a clock the test drives.
- **The look** (`SkUiLook.cs`, `DefaultSkUiLook.cs` and the platform looks) is one class family that knows every control; new members go there as for the stepper and the calendar.

## API

### Carousel (SkUi\* and Core)

| Member | Type | Default | Notes |
| --- | --- | --- | --- |
| `VisibleViews` | `ObservableCollection<View>` (read-only) | empty | SkUi\* only, MAUI's ([D4](#decisions)) |
| `VisibleNodes` | `IReadOnlyList<SkUiCoreNode>` | empty | Core only; `VisibleNodesChanged` event when the set changes |
| `ItemsPerSnapPoint` | `int` | `1` | Both layers ([D5](#decisions)) |
| `AutoPlayInterval` | `TimeSpan` | `Zero` | Both layers; `Zero` is off ([D6](#decisions)) |
| `IsAutoPlayPaused` | `bool` | `false` | Both layers; the app's pause |

`SkUiIndicatorView` with a carousel whose `ItemsPerSnapPoint` is above 1 counts pages. `SkUiCoreIndicatorView` gains `SetCarousel(SkUiCoreCarouselView?)`, the Core counterpart of MAUI's `IndicatorView` link.

### `SkUiCoreCarouselView`

The SkUi\* carousel's members in Core form (fluent `Set*`, `INotifyPropertyChanged`): `ItemsSource` (an `INotifyCollectionChanged` source listened to weakly), `SetItemFactory` ([D7](#decisions)), `Position`, `CurrentItem`, `PositionChanged` / `CurrentItemChanged` (MAUI's argument types), `Loop`, `PeekAreaInsets`, `ItemExtent`, `ItemSpacing`, `Orientation`, `SnapPointsType`, `SnapPointsAlignment`, `ItemsUpdatingScrollMode`, `ItemEffect`, `IsSwipeEnabled`, `IsBounceEnabled`, `IsScrollAnimated`, `ScrollPosition`, `ScrollToIndex` / `ScrollToItem` (tasks), `RemainingItemsThreshold` and its event, `EmptyView` (a node), `GetRealizedNode(index)`, and the members of the table above. Not on Core: `ItemsLayout` (a MAUI `BindableObject`), visual states, scroll bars.

### Segmented control and tab view

The review plan's [F1 tables](ImplementationPlan-PerformanceAndControls.md#f1-segmented-control-and-tab-view) are the contract, with these changes:

- `SkUiTabView` is built per [D3](#decisions) and [D8](#decisions), not on a `SkUiCarouselView`.
- `SkUiCoreTabView` (cut line, [S8](#s8-core-tab-view-cut-line)): the tab view's members in Core form, with `SkUiCoreTabItem` taking a `Content` node or a `ContentFactory` (`Func<SkUiCoreNode>`).
- Step 1's review of the control pages may change the tables; changes are copied back here.

## Phase C: the carousel

### C1 The engine (internal)

- `Controls/Virtualization/SkUiCarouselEngine.cs` per [D2](#decisions): input is the count, the viewport, the insets, the extent, the spacing, the alignment, looping, the direction and the scroll offset; output is each slot's rectangle, the snap offsets (the `ISkUiSnapPointSource` query), the current index, the slots to realize, and the correction when the strip moves back by cycles. `ItemsPanel` keeps the views, the pool, visual states and effect links, and asks the engine for everything else.
- *Check:* `CarouselViewTests`, `IndicatorViewTests`, `IndicatorTemplateTests` and the `CarouselSwiped` leak scenario green without changes; new `CarouselEngineTests` on the engine alone (fill and extent, peek insets, each alignment, looping and moving back, right to left, zero and one item); `scripts/bench.sh --baseline HEAD -S carousel-swipe` shows no slower frame.

### C2 `VisibleViews`

- Per [D4](#decisions), on the SkUi\* carousel. The set is kept in a reused list and compared in place.
- *Check:* tests: the views in view after the first layout, after a swipe, during a render-thread fling (reported frames), with peek insets (neighbors listed while they show), in a loop with fewer items than fit, with cover flow; one change per view entering or leaving; zero allocations over a fling whose set does not change. The parity exemption removed and `MauiApiParityTests` green.

### C3 Several items per snap point

- Per [D5](#decisions): the engine's snap offsets and fill extent, `Position` coercion, keys and screen-reader steps, the indicator's page count.
- *Check:* tests: 10 items with 3 per page, looping and not (snap offsets, the short page, stable pages on the second round), `Mandatory` and `None`, `Position = 4` going to 3 and writing back without cutting a one-way binding, arrow keys, the indicator's count, position and dot taps, fill extent with spacing and insets.

### C4 Autoplay

- Per [D6](#decisions): an internal timer source on the surface (dispatcher timer on devices, a manual source in tests), started only while the carousel is shown and not paused.
- *Check:* tests with the manual source: one step per interval; a drag restarts the wait; hover, focus inside and `IsAutoPlayPaused` stop it; hidden (itself, an ancestor, removed from the tree) stops it and showing it again starts a fresh interval; the end of a carousel that does not loop goes back to the first; reduce motion jumps; screen reader running pauses; with `ItemsPerSnapPoint = 3` it steps a page. A leak scenario: a page with an autoplaying carousel, popped while a step animates (`CarouselAutoPlay`).

### C5 Core carousel

- `Core/SkUiCoreCarouselView.cs` per [the API](#skuicorecarouselview), on the engine and `SkUiCoreScrollView`'s controller, with C2 to C4 included; `SkUiCoreIndicatorView.SetCarousel`.
- *Check:* the carousel tests that apply ported to Core (`CoreCarouselViewTests`); a test drawing the same carousel on both layers (cover flow, mid-scroll) and comparing pixels; factory recycling (a node is created once per realized slot and rebound when it is reused, keys keep pools apart); leak scenario `CoreCarouselSwiped`; bench `core-carousel-swipe` (options through `TrySet`).

## Phase S: segmented control and tab view (F1)

Steps S1 to S6 follow the review plan's [F1 steps](ImplementationPlan-PerformanceAndControls.md#f1-segmented-control-and-tab-view) 1 to 6 unchanged:

- **S1** control pages (`docs/controls/SkUiSegmentedControl.md`, `SkUiTabView.md`), reviewed before S2.
- **S2** `SkUiSegmentedEngine` (segment and selection rectangles, equal and content widths, right to left).
- **S3** the look ([D9](#decisions)): `SkUiSegmentedPaint`, `MeasureSegmented`, `DrawSegmented`, and the tab strip's `DrawTabStrip`, in the default and platform looks; `PlatformLookShapeTests` entries.
- **S4** `SkUiSegmentedControl`: tap selects through `WriteBack`, the selection animates with `SkUiTween` (snaps under reduce motion).
- **S5** `SkUiCoreSegmentedControl`, pixel-compared with S4.
- **S6** keys (Left / Right mirrored in right to left, Home / End, one tab stop) and semantics (each segment selectable, "selected, 2 of 4").

### S7 Tab view

- `SkUiTabView` per [D3](#decisions): a strip (a `SkUiSegmentedEngine` row; with `TabWidthMode="Content"` inside a horizontal scroller that keeps the selected tab in view) and a pane host on the carousel engine (C1). `SelectedIndex` is the one source of truth: a tap on a tab, a swipe and a key all go through it; setting it scrolls the panes, animated by the look.
- The indicator follows the pane scroller per [D8](#decisions).
- Each pane is a semantics container named by its tab; the strip is a tab list (`TabList` / `TabItem` roles where the platform has them).
- *Check:* `TabViewTests`: only the first pane created; a drag toward the second creates it and both stay; a pane's state survives going out of view and back; swipe changes `SelectedIndex`; `IsSwipeEnabled = false`; strip at the top and bottom; badge text; a disabled tab is skipped by swipes and keys; the indicator's committed links at three offsets match the UI side's placement (equal and content widths, right to left); zero re-records and allocations over a fling; leak scenario `TabViewSwiped`.

### S8 Core tab view (cut line)

- `SkUiCoreTabView` on the same engine and strip, after C5 and S7. Ships in 1.17 if the rest is done; otherwise it goes to the backlog.
- *Check:* the S7 tests that apply on Core; a pixel comparison with S7.

## Docs, demo and release

1. **Docs:** [SkUiCarouselView.md](../controls/SkUiCarouselView.md) (`VisibleViews`, several items per snap point, autoplay with its pauses, a Core section; the "not available" line shrinks to a grouped source), [SkUiIndicatorView.md](../controls/SkUiIndicatorView.md) (pages, the Core link), the S1 pages, [SkUiCore.md](../controls/SkUiCore.md) and the controls index; [ScrollingAndCollectionViews.md](ScrollingAndCollectionViews.md) (the carousel engine and its three users); [ControlLook.md](ControlLook.md) (the new painters); [Migration.md](../Migration.md) and the migration skills (`VisibleViews` no longer open; vendors' segmented controls and tab views and the Community Toolkit's `TabView` patterns mapped to the new controls; a timer setting `Position` maps to `AutoPlayInterval`).
2. **Demo:** the component gallery's carousel page gains autoplay, pages of three and a Core tab; new "Segmented control" and "Tab view" pages (Material and iOS looks side by side, content widths, a strip at the bottom).
3. **Bench:** `core-carousel-swipe` (C5), `skui-segmented` and `core-segmented` (200 controls), `skui-tabview-swipe` (five panes of lists, swiped and flung).
4. **Device checks** (the [verification backlog](Roadmap.md#verification-backlog) if not done before release): the tab view's swipe and its indicator on the Galaxy S9 and the iPhone 8 (frame times and feel), autoplay with TalkBack and VoiceOver on, the segmented control's look on each platform.
5. **Release:** changelog (`### New features`), [Roadmap.md](Roadmap.md) (1.17 section; the carousel item and F1 leave the backlog), [Requirements.md](Requirements.md) (FR-32 updated, FR-37 segmented control and tab view), the review plan's F1 marked shipped.

## Order and sizes

| Step | Size | Depends on | Notes |
| --- | --- | --- | --- |
| C1 | M | | Internal; blocks C5 and S7 |
| C2 | S | C1 | |
| C3 | M | C1 | Engine snap offsets and the indicator |
| C4 | S | C1 | Timer source |
| C5 | M | C2, C3, C4 | Gets all three from the start |
| S1 | S | | Reviewed before S2 |
| S2 to S6 | M | S1 | Independent of phase C |
| S7 | M | C1, S4 | The render-thread indicator ([D8](#decisions)) |
| S8 | S | C5, S7 | Cut line |
| Docs, demo, bench | M | C5, S7 | Bench scenarios can be written with their steps |

Two tracks can run side by side: phase C (one agent; C2 to C5 touch the same carousel files and run one at a time) and S1 to S6 (another agent in its own worktree; it touches the look, `ImplicitStyleTests` and the gallery, which phase C does not). S7 starts once both C1 and S4 are merged; S8 last.

## Not in 1.17

- **Chips, range slider and rating** (F5, F6): back to the backlog ([D1](#decisions)).
- **A grouped carousel source:** MAUI's `CarouselView` has none either.
- **Visual states for Core carousel items:** Core has no `VisualStateManager` ([D7](#decisions)).
- **Unloading far tab panes:** panes stay once created; an option to release them is a later step if an app needs it.
- **Tab strips on the left or right**, an overflow menu for tabs that do not fit, and tabs that reorder by drag.
- **Autoplay progress in the indicator** (a dot that fills as the interval runs).
- **The virtual stack's Core twin:** it reuses the Core carousel's factory shape ([D7](#decisions)) when it comes.

## Verification

- Headless suite green on net10 and net11, library build at 0 warnings.
- `carousel-swipe` no slower than 1.16.0; `skui-tabview-swipe` and `core-carousel-swipe` with zero re-records and zero allocations per swipe frame.
- Leak scenarios `CarouselAutoPlay`, `CoreCarouselSwiped`, `SegmentedControlShown` and `TabViewSwiped` pass headless and on devices (`scripts/device_tests.sh`).
- `MauiApiParityTests` without the `VisibleViews` exemption.
- The gallery pages checked on the S9 and the iPhone 8 (or the checks left on the verification backlog, named in the release notes' section).

**Done when:** a carousel lists its views in view as MAUI's does, autoplays with the pauses of [D6](#decisions), pages several items per snap point and exists on the Core layer; the segmented control works on both layers in every look; and a tab view of five list panes swipes and flings with its indicator in step, with zero re-records and only the panes that were shown created.
