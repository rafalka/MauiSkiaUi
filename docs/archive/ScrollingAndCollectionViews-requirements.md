# Scrolling and collection views: research, requirements and delivery (archived)

> Archived from `docs/design/ScrollingAndCollectionViews.md`: how peer platforms scroll, the FR-21 / FR-22 requirement checklists, the delivery order and the implementation checklist, as of the 1.0 work. The current design is [docs/design/ScrollingAndCollectionViews.md](../design/ScrollingAndCollectionViews.md); open work is in the [roadmap](../design/Roadmap.md).

## How peer platforms implement this

| Platform | Scroll | Collections | Fully drawn? |
| --- | --- | --- | --- |
| **.NET MAUI** | Native (`UIScrollView`, etc.) | Native recyclers + MAUI cell handlers | **No** |
| **Flutter** | `Scrollable` + `Viewport` + slivers | `ListView` / `SliverList` build only visible children (+ cache extent) | **Yes** |
| **Avalonia** | `ScrollViewer` (Offset / Extent) | `ItemsControl` + `VirtualizingStackPanel` recycle | **Yes** |
| **Uno** | `ScrollViewer` (+ Skia path) | `ItemsRepeater` virtualization | **Mostly yes** on Skia |
| **DrawnUi** | `SkiaScroll` (gestures, viewport, rubber-band) | Templated layout + recycling template + items windowing | **Yes** |

**Lesson:** toolkits that paint with Skia **own** the scroll offset and list virtualization inside the drawn tree. MAUI's scrollers stay platform-native by design, and nesting SkiaUi under them fights the single-surface model.

## Why not MAUI `ScrollView` / `CollectionView` as the main host

1. **Many surfaces / handlers.** A `CollectionView` cell with a standalone `SkUiView` creates a handler per cell.
2. **The viewport lives outside Skia.** Culling, on-demand creation and overlay repositioning need an in-tree offset.
3. **Gesture conflicts.** Drawn gestures are arbitrated in the drawn tree. Native scrollers only coordinate with it at the surface boundary.
4. **`SkUiMauiContentView`.** Overlays need scroll-time sync.
5. **The wrong cost center.** MAUI still pays layout and handler cost per visible cell.

**Escape hatch:** small lists or hybrid pages may put standalone `SkUi*` controls inside MAUI scrollers. Drawn scrollers inside a native `ScrollView` coordinate with it: drawn first, native at the drawn edge. This path is documented as slower interop.


## FR-21 — Virtual / dynamic scroll layout (requirements)

**Purpose:**
- Large and **endless** scroll content without creating every item up front (feeds, logs, chat, search results loaded page by page).
- **Looped** horizontal carousels (after the last item, the first item continues seamlessly).
- The item engine underneath `SkUiCollectionView` (indexed virtual extent only; see mode A below).

The layout **requests** children from a provider while scrolling, before they reach the visible area, so scrolling stays fluent.

### Virtual scroll modes

One engine (`SkUiVirtualVerticalStackLayout`, optional `SkUiVirtualScrollView` host) implements three modes. Apps choose with **`VirtualScrollMode`** (`Indexed`, `InfiniteFeed`, `Loop`). Prefetch, creation budget, visible-window intersection, and optional recycling are shared; **extent**, **index stability**, **scroll-bar semantics**, and **`ScrollTo`** differ.

| Mode | Primary goal | Child count | Logical index | Content extent | Scroll bar thumb |
| --- | --- | --- | --- | --- | --- |
| **A — Indexed** (`Indexed`) | Avoid creating far-off items; stable list position | Grows with realized range (optional release) | Stable `0 … Count−1` (or append) | Sum of measured + estimated sizes; grows with list | Normal (position in content) |
| **B — Infinite feed** (`InfiniteFeed`) | Unbounded generated stream; bounded memory | Fixed: visible + pre/post buffer only | Monotonic or provider-defined; may leave the realized window | **Not** a global scroll range | Velocity / direction control only (no absolute position) |
| **Loop** (`Loop`) | Carousel: infinite scroll over **N** items | Same as B (small window) | `index mod N` | **Not** `N × itemSize` as scroll range; wrap at edges | Usually hidden; same velocity semantics as B when shown |

#### Mode A — Indexed virtual extent (default)

This is the default FR-21 / FR-22 behavior.

- **Why:** reduce UI creation cost by realizing only items near the viewport (plus prefetch). Realized items may remain in the layout or be **released** when far away (`ReleaseFactor`), like a collection view, but each slot stays tied to a **stable item index** and contributes to **scroll offset** through cached measured size (and estimates for unrealized indices).
- **Count:** `ItemCount` optional; `null` or endless append via incremental loading still uses **indexed** semantics — extent grows as items are appended; `ScrollToIndex` and pixel `ScrollTo` / `ScrollToAsync` remain meaningful (estimates fill gaps).
- **Release:** optional; released indices stay in the size cache so offset and thumb position do not jump.
- **Use when:** `SkUiCollectionView`, finite or growing lists, `ScrollToIndex`, sticky headers, grouping, and normal scroll bars.

#### Mode B — Infinite feed (`InfiniteFeed`)

- **Why:** content is **generated** continuously (infinite feed, log tail, procedural rows). The app does not model a finite list length; it only needs a **bounded** number of live views.
- **Window:** keep at most **visible area + configured pre- and post-buffer** (reuse `PrefetchFactor` / `PrefetchDistance` and a symmetric **`LeadingBuffer`** / **`TrailingBuffer`** in viewport lengths or DIP). Scrolling forward **appends** at the trailing edge and **drops** children (and optionally recycles) from the leading edge; scrolling backward does the reverse.
- **Mapping:** the provider supplies items by **logical index** or sequence id. Indices of realized children are a **contiguous slice** of that sequence; when the leading edge is dropped, logical indices shift relative to child order — the layout maintains **`FirstRealizedLogicalIndex`** (or equivalent) so hit-testing, accessibility, and events report the correct logical index.
- **Scroll offset:** offset is **relative** to the current window (content translates; dropping leading items adjusts offset so the viewport does not jump). There is **no** stable global pixel extent for the whole infinite stream.
- **Scroll APIs:**
  - **Supported:** pan, fling, `ScrollBy`, relative `ScrollTo` (delta), bringing a **logical** item into view when the provider can materialize it (`ScrollToLogicalIndex` / `ScrollToIndex` with window realization rules).
  - **Not supported / undefined:** absolute pixel `ScrollTo(x, y)` to a position in an infinite stream; thumb proportional to “whole feed length”.
- **Scroll bars:** when visible, they act as **direction and speed** controls (drag / track = scroll velocity or page in direction), **not** as a map of absolute position. Default visibility for this mode is **`ScrollBarVisibility.Never`** or a dedicated **`ScrollBarInteractionMode`** (`Position` vs `Velocity`) on the host scroller.
- **Incremental loading:** natural fit — `LoadMore` / threshold extends the logical sequence at the trailing edge while the window slides.
- **Use when:** infinite social feeds, live logs, AI chat streams where total length is unknown and unbounded.

#### Loop scrolling (`Loop`)

- **Why:** carousel / gallery — **finite** `ItemCount = N`, but the user can scroll forever: after the last item, the **first** item continues without a hard stop (and symmetrically before the first).
- **Mechanism:** same **window trim** as `InfiniteFeed`, with **wrap-around indexing**: logical display index = `index mod N`. As the user approaches an edge, the layout **prepends** or **appends** copies (or rebinds recycled views) for indices `… N−1, 0, 1 …` so motion stays continuous. Offset corrections match mode B so removing off-screen wrap segments does not jump.
- **Snap:** horizontal carousels combine with FR-17 **`SnapPointsType`** / alignment on item boundaries; loop mode does not break snap-to-item.
- **Scroll bars:** typically **hidden**; velocity-style interaction if shown.
- **API:** `ItemCount` (or bound collection count) **required**; `ScrollToIndex(i, …)` uses **`i mod N`**. Optional **`LoopEnabled`** on a virtual stack inside an existing horizontal `SkUiScrollView` for nested carousel demos.
- **Use when:** image carousels, onboarding paging strips, any “infinite” finite set.

#### Mode selection and FR-22

- **`SkUiCollectionView`** uses **mode A only** (indexed extent, recycling, grouping, sticky headers, index-based scroll APIs).
- **`SkUiVirtualScrollView`** exposes **`VirtualScrollMode`** for app-authored feeds (A/B) and carousels (Loop).
- Engine implementation should share: visible-window math, prefetch / budget, provider callbacks, recycling pool, and render-thread fling coordination; mode-specific code owns **extent reporting to `SkUiScrollController`**, **leading/trailing trim**, and **scroll-bar interaction**.

### Shape

- **`SkUiVirtualVerticalStackLayout`:** a vertical stack layout whose children are created on demand. A horizontal twin (`SkUiVirtualHorizontalStackLayout`) is a later extension.
  - It works as the content of a drawn scroller or anywhere below one (several virtual sections in one page, headers above the list).
  - It also works nested inside another virtual layout.
- **Visible window:** the layout computes its window as the intersection of the viewports of all ancestor scrollers, in its own coordinates. It does not care which ancestor scrolls.
- **`SkUiVirtualScrollView`:** a convenience control combining a vertical scroller with a virtual stack, for the common single-list case.
- **Core variant `SkUiCoreVirtualVerticalStackLayout`:** optional. It is added only if the engine is written against the shared render / input node contracts, so that it is a thin wrapper.

### Item provider

Three ways to supply items; any one is enough:
- **Per-index factory:** a callback or event such as `ItemRequested(index) → ISkUiView?`, returning `null` at the end.
  - `ItemCount` is optional; `null` means unknown / endless.
- **Binding:** `ItemsSource` + `ItemTemplate` / `ItemTemplateSelector` (MAUI-familiar), with `INotifyCollectionChanged` insert, remove, move, replace and reset.
- **Incremental loading:**
  - a `RemainingItemsThreshold` + `RemainingItemsThresholdReached` event / command, and / or
  - an async `LoadMore` hook returning whether more exists.
  - A configurable loading placeholder item is shown while a page loads.

### Prefetch and budget

- **`PrefetchFactor`:** how far ahead to create items, in viewport lengths (default 1.0). Items are requested while still outside the visible area. An optional DIP variant, `PrefetchDistance`, and a smaller behind-distance for reverse scrolling are also required.
- **Scroll direction and fling:** prefetch follows the scroll direction.
  - During a render-thread fling, the UI thread receives offset reports every frame. The window is extended by the predicted fling travel for the next frames, because the render thread can only show items that already exist.
- **Creation budget:** items are created within a per-frame UI-thread budget (for example `PrefetchBudget` = 4 ms), spread over frames.
  - Creation is synchronous only when the visible area would otherwise show a gap.
  - All work stays off the render thread (NFR-6).
- **Nested virtual layouts** receive the clipped window of their ancestor, so an inner list inside a card only realizes what could be visible.

### Release, recycling and sizing

- **`ReleaseFactor`:** items farther than this many viewport lengths from the window are released (default: never in endless-append mode). Their measured size is kept, so the extent and scroll position stay stable.
- **Recycling:** optional here, required for FR-22. Released items return to a pool keyed by template / recycle key and are rebound (`BindingContext`) instead of recreated.
- **Sizing:**
  - **Default assumption:** every item may have a **different** size (per-index measure + cache). The engine does not assume a uniform row height unless the app opts in.
  - `EstimatedItemSize` is optional hint for **unmeasured** indices only (extent and scroll-to estimates); it is not a substitute for measuring realized items.
  - Measured sizes are cached **per index**; when a realized item's content changes size, **`RemeasureItem`** (or equivalent) updates the cache and extent with scroll anchoring.
  - Optional **`QueryItemSize`** (index, data item → size, handled): skip full template measure when the app already knows the size (optimization, not the default path).
  - When items before the visible area change size (or are inserted), the first visible item keeps its position on screen (scroll anchoring).

### API, events, behavior

- **`VirtualScrollMode`:** `Indexed` (default), `InfiniteFeed`, `Loop`. Documented per-mode limits on extent and scroll APIs (see **Virtual scroll modes**).
- **Scrolling to an index:**
  - **Indexed (A):** `ScrollToIndex(index, position = MakeVisible | Start | Center | End, animated)` — realizes the target, using estimates in between.
  - **Infinite feed (B):** `ScrollToLogicalIndex` / `ScrollToIndex` brings a logical item into view if the provider can supply it; no absolute content pixel target.
  - **Loop:** `ScrollToIndex(index, …)` uses `index mod ItemCount`; shortest path on the ring is optional polish.
- **Scroll host integration:** in modes B and Loop, `SkUiScrollView` (or `SkUiVirtualScrollView`) reports a **window extent** to the controller for clamping and overscroll, not unbounded stream length; **`ScrollBarInteractionMode`** = `Position` | `Velocity` (default `Position` for mode A, `Velocity` for B / Loop when bars are shown).
- **Events:**
  - `ItemRealized` / `ItemReleased` (index, view), for loading images or data;
  - `VisibleRangeChanged` (first / last visible index).
  - **Infinite feed / loop:** range events expose **logical** first / last visible index (and `FirstRealizedLogicalIndex` when useful).
- **Items are ordinary drawn children.** Taps, swipes and nested carousels inside items go through the gesture arena, and scrolling is the ancestor scroller's.
- **Programmatic content changes** never remeasure unaffected realized items. An append measures only the new items.

### Acceptance and performance

- **Benchmark scenarios** (headless and device):
  - endless feed with 10k+ items, flinging at device fps with no blank frames while items are created within budget;
  - release enabled: managed memory stays flat over a long scroll;
  - per-item creation cost reported.
- **Tests:**
  - prefetch creates items before they intersect the viewport;
  - the creation budget is honored;
  - anchoring is stable when an earlier item changes height;
  - the endless provider stops at `null`;
  - `RemainingItemsThreshold` fires once per page;
  - nested windows.
  - **Infinite feed:** forward scroll appends and trims without viewport jump; reverse scroll prepends and trims; child count stays within buffer bounds; logical index in events stays correct after trim.
  - **Loop:** scroll past last item shows first with no gap; scroll backward past first shows last; `N=1` edge case; snap still aligns to items.
  - **Scroll bars (B / Loop):** thumb does not imply global position; track drag still scrolls content in the expected direction.

## FR-22 — `SkUiCollectionView` (requirements)

A virtualizing, recycling list / grid built on the FR-21 **indexed** engine (`VirtualScrollMode.Indexed` only).

### API philosophy (not MAUI parity)

Unlike most SkUi* controls, **`SkUiCollectionView` is not required to mirror MAUI's `CollectionView` API**. SkiaUi defines the control around drawn-tree performance (one surface, recycling, scroll integration). Apps moving from MAUI use **migration documentation and agent skills**, not a parity checklist in this requirement.

- **Authoritative product API:** this section and [Requirements.md](Requirements-1.0.md) (FR-22).
- **MAUI mapping:** [Migration.md](../Migration.md) (human-readable) and [skiaui-migration skills](../../plugins/skiaui-migration/README.md) (`skiaui-migrate` / `skiaui-audit` references). When `SkUiCollectionView` ships or gains members, update those artifacts together (see the plugin README).
- **Reuse where it helps:** familiar MAUI concepts (`ItemsSource`, `DataTemplate`, `INotifyCollectionChanged`, bindable selection) are fine when they match SkiaUi's model; names and behavior may differ where MAUI's API does not fit virtualization or the single-surface model.

### Functional requirements

- **Data and templates:** bindable `ItemsSource`; `ItemTemplate` / selector; `EmptyView` (+ template); incremental collection changes (`INotifyCollectionChanged` or equivalent).
- **Layouts (SkUi-defined):** at minimum vertical and horizontal **linear** lists with item spacing; **grid** with span; optional snap alignment on the scroll axis (reuse FR-17 snap types where applicable).
- **Item sizing:** same default as FR-21 — **each item may differ in size**; per-index measure cache, optional `EstimatedItemSize` for unrealized rows, optional `QueryItemSize` when sizes are known without measure, **`RemeasureItem`** when template content changes; scroll anchoring on size changes (FR-21).
- **Header / footer:** optional header and footer (content or template); **sticky** header / footer (`IsStickyHeader`, `IsStickyFooter`) pinned without re-recording while items scroll.
- **Grouping:** grouped `ItemsSource`; group header (and optional footer) templates; **sticky group headers** (`IsStickyGroupHeader`).
- **Expandable groups:** groups can be **collapsed** so their items are omitted from layout and virtualization (not merely hidden — unrealized while collapsed).
  - Per-group **`IsExpanded`** (bindable on the group model and/or driven from the collection view), default expanded unless the app sets otherwise.
  - **Toggle** from the group header (tap on header or a dedicated affordance in the template); optional `GroupExpanding` / `GroupExpanded` / `GroupCollapsing` / `GroupCollapsed` events (or commands) so the app can persist state or load lazily.
  - **Scroll anchoring:** collapsing or expanding a group above the viewport must not jump unrelated content (reuse FR-21 anchoring; the first visible row stays stable when possible).
  - **Sticky header** shows the current group header including collapsed / expanded state; accessibility reports **expanded** / **collapsed** for the group.
  - Realized item indices and `ScrollTo` account for collapsed groups (only expanded groups contribute items to the flat index or API documents group + item addressing).
- **Selection:** **`SelectionMode`**: `None`, `Single`, **`SingleDeselect`** (tap selected row to clear), **`Multiple`**; selected item(s); **`SelectionChanging`** (cancelable) and **`SelectionChanged`** (+ optional command); **`SelectAll`** / **`ClearSelection`** when mode allows; **`Selected` visual state** on the item root; optional **`SelectionBackground`** (and template override) with re-record limited to affected items. **`Extended`** selection (Shift range, Ctrl/Cmd toggle on desktop) is a later enhancement with keyboard focus (FR-10).
- **Item activation:** `ItemTapped` event and command (+ parameter, default the item), with item / flat index / group in args; independent of selection; inner interactive children keep their own taps.
- **Scrolling:** hosted in or as a scroller; `ScrollTo` to item (and group when grouped); visible-range / scrolled notifications; scroll bar visibility consistent with FR-17.
- **Load more (UI + API):**
  - **`LoadMoreMode`:** `None`, **`Manual`** (footer/start row with button — runs command on tap), **`Auto`** (run when scroll reaches boundary), **`AutoOnUserScroll`** (same as Auto but only after the user has scrolled — avoids load on first layout).
  - **`LoadMorePosition`:** **`End`** (default) or **`Start`** (e.g. chat / inverted feeds).
  - **`LoadMoreCommand`** (+ parameter); **`LoadMoreTemplate`** for the load-more row (manual button and/or progress indicator).
  - **`IsLoadMoreActive`:** true while the command is running (keeps the load-more row visible / shows busy state).
  - Reaching the boundary may still raise **`RemainingItemsThreshold`** / **`RemainingItemsThresholdReached`** for apps that prefer a threshold-only hook; load-more mode and threshold can coexist (threshold for prefetch, load-more row for UX).
- **Pull to refresh:** `IsRefreshing` / `RefreshCommand` at the scroll start (drawn indicator).

**Later (not FR-22 MVP):** reordering (long-press drag, gesture arena); row swipe actions (shipped as `SkUiSwipeView`, C1); horizontal-only polish; keyboard-focused item navigation; animated insert / remove.

### Product backlog (common list control features)

Feature-rich list controls are a useful benchmark for **list UX**, not an API target. SkiaUi keeps **SkUi-first** FR-22; the items below are candidates to add to FR-22 or Phase C when they fit the single-surface model.

| Area | Common pattern | SkUi today | Recommendation |
| --- | --- | --- | --- |
| Virtualization | View reuse, templates, selector | FR-21 + FR-22 recycling | **Ship** (core) |
| Layout | Linear, grid, orientation | Planned linear + grid | **Ship** |
| Variable height | `AutoFitMode`, `QueryItemSize`, `DynamicHeight` | Per-index measure default (FR-21 / FR-22) | **In FR-22:** optional **`QueryItemSize`**, **`RemeasureItem`**; no uniform-height default |
| Grouping | Descriptors, sticky headers | Sticky group headers | **Ship** |
| Expand / collapse | `AllowGroupExpandCollapse`, ExpandAll / CollapseAll | Expandable groups (FR-22) | **Add:** **`ExpandAll` / `CollapseAll`**, optional **`AutoExpandGroups`** for new groups |
| Multi-level groups | Nested `GroupDescriptor` | Not specified | **Candidate:** hierarchical groups + indented headers (later) |
| Sort / filter | `SortDescriptors`, `Filter`, `LiveDataUpdateMode`, custom comparers | App / view model | **Light built-in optional:** `Filter` predicate + **`RefreshFilter`**; sort stays in VM unless we add optional **`SortDescriptor`**s (heavier; defer) |
| Filtering UI | `FilteringUITemplate`, popup | — | **Out of control** — app composes chips / search above the list |
| Selection | Single, SingleDeselect, Multiple, Extended; SelectAll | FR-22 | **In FR-22:** SingleDeselect, Multiple, SelectionChanging, SelectAll / ClearSelection; **Extended** desktop later |
| Select → scroll | Auto `ScrollTo` when `SelectedItem` changes | — | **Candidate:** `ScrollSelectedIntoView` (optional) |
| Load more | Manual / Auto / AutoOnScroll; top or bottom; template row | FR-22 load-more API | **In FR-22** |
| Pull to refresh | Often a separate pull-to-refresh wrapper | Built-in on collection | **Ship**; document the horizontal list limitation |
| Swipe actions | Start/end templates, threshold, full swipe delete | `SkUiSwipeView` in the item template (C1): MAUI's `SwipeView` API, `Threshold`, `Execute` mode for a full swipe, `Open` / `Close` | **Shipped** |
| Reorder | OnHold / drag indicator, drag template | FR-22 later | **Ship later** with **`DragStartMode`**, optional **`DragItemTemplate`** |
| Scroll / scroll-to | `ScrollTo` / `ScrollToRowIndex`, animated | FR-17 + FR-22 | **Ship**; grouped + variable height may weaken exact `Center` first time (document it) |
| Item tap context | `ItemType` (Header, GroupHeader, Record, LoadMore) | Item + index + group | **Add:** **`ItemTappedEventArgs.ItemKind`** (record, header, footer, group header, load-more) |
| Appearance | Item fade on appear, fade on scroll | — | **Look / optional:** `EnableFadeOnScroll`; **`ItemAppearing`** hook for light animation (FR-7 render thread preferred) |
| RTL | `FlowDirection` | FR-17 RTL scroll bars | **Inherit** MAUI `FlowDirection` on list + items |
| Accessibility | Screen reader, keyboard nav (often partial) | FR-10 | **Ship** with collection: roles, group expanded/collapsed, list item position |

**Intentionally no `DataSource` layer inside the control:** grouping, sorting, and filtering logic can stay in the view model (`CollectionView` / LINQ / dynamic data) for v1; built-in filter + load-more modes cover most list screens without a second data layer inside the control.

### Behavior and cost

- **Items:** cells are hosted drawn nodes with no platform view. `SkUiMauiContentView` inside cells is discouraged in large lists, because each one is a native overlay.
- **Recycling:** per template / selector result. Rebind updates `BindingContext` and re-records only changed nodes.
- **Measurement:** measure each **realized** item (variable height by default); cache per index; grid rows realized as a unit (row height = max of cells in the row).
- **Migration deliverable (with first shippable MVP):** add `references/collection-view.md` under `skiaui-migrate` (property / pattern mapping, intentional differences, workarounds for unsupported MAUI features) and extend [Migration.md](../Migration.md) § CollectionView; update `gaps.md`, audit script messages, and `check_xaml.py` when the gap closes.

### Acceptance and performance

- 10k-item flat list at device fps during fling;
- recycling: no steady-state per-scroll allocations;
- selection change re-records at most two items;
- sticky header / group header: no re-record while scrolling;
- `ItemTapped` versus a button inside an item;
- grouped list with sticky headers;
- **expand / collapse** does not realize collapsed items; anchoring stable when a group above the viewport toggles;
- load more: Manual / Auto / AutoOnUserScroll at start and end; `IsLoadMoreActive`; threshold still fires when configured;
- variable-height rows with stable anchoring after `RemeasureItem`;
- selection: SingleDeselect, cancelable SelectionChanging, SelectAll / ClearSelection;
- migration doc and skills describe every MAUI `CollectionView` feature the control supports or replaces.

## Delivery order

| Milestone | Deliverable | Status |
| --- | --- | --- |
| Scroll engine | `SkUiScrollView`: offsets, render-thread fling / tween, wheel, clip, RTL start | **Done** |
| Gestures | Gesture arena; scroll as arena member; press delay; drags inside scrollers | **Done** |
| Nested scrolling | Orthogonal and same-axis nesting, drag and fling chaining, native-parent coordination | **Done** |
| Core scrolling | `SkUiCoreScrollView` on the shared engine (Core and SkUi* nest freely) | **Done** |
| FR-21 | `SkUiVirtualVerticalStackLayout` (vertical), indexed extent, provider / binding, prefetch, budget, anchoring (also during flings); `SkUiVirtualScrollView` (B1) | **Done** |
| FR-21 | Release + recycling pool; fling-predictive prefetch (B1) | **Done** |
| FR-21 | `VirtualScrollMode`; a public horizontal layout (the engine's axis is done, B3); `PrefetchDistance`, `QueryItemSize`, load-more hook; optional Core variant | Next |
| FR-21 | **`InfiniteFeed`:** trim/prepend-append, logical index mapping, relative scroll APIs, velocity scroll bars | Later |
| FR-21 | **`Loop`** carousel: wrap indexing, offset correction, horizontal + snap; demo nested carousel on virtual loop | Later |
| FR-22 | `SkUiCollectionView` MVP: linear layout, templates, single selection, sticky header / footer, item tap, empty view, load-more threshold, pull to refresh, migration doc + skill reference (B2) | **Done** |
| FR-22 | Grouping, **expandable groups**, sticky group headers, grid, horizontal, multiple selection, load-more modes (B3) | **Done** |
| FR-22 | Row swipe (`SkUiSwipeView`, C1) | **Done** |
| Scrolling | `SkUiRefreshView` around other content, on a helper shared with the collection view; overscroll events and pull edges on both scroll views (C2) | **Done** |
| FR-22 | Reordering, other list chrome (see Phase C) | Later |
| Polish | Scroll bars, overscroll / bounce, scroll to element, horizontal wheel for `Both` | **Done** (P8) |
| Polish | Snap points, draggable scroll bars, placed scroll bars | **Done** (P8) |
| Polish | Keyboard / focus bring-into-view | Later |

## Implementation checklist

### Scrolling (FR-17)

- [x] `SkUiScrollView`:
  - [x] `Content` / `Orientation` / offsets, clamp, `ScrollTo` / `ScrollToAsync` / `AnimateScrollTo` / `Scrolled`;
  - [x] offset-only changes never remeasure or re-record.
- [x] Render-thread fling and tween, with offsets reported back and motion stopped by a press.
- [x] Gesture arena integration: tap versus scroll, press delay, drags inside scrollers.
- [x] Nested scrolling: orthogonal, same-axis chaining, fling hand-off, wheel to the innermost scroller that can move.
- [x] Native ancestors: Android disallow-intercept while pending / claimed; iOS gate recognizer.
- [x] `SkUiCoreScrollView` sharing the engine; Core ↔ SkUi* nesting.
- [x] FR-16: Android / Windows snapshot freeze while scrolling (Apple live sync); `ScrollMode` opt-out / opt-in; scroll start / end signals; overlay clipping to viewports.
- [x] Scroll bars, overscroll / bounce / stretch, scroll to element, horizontal wheel on `Both`, direction-aware flings with live extents (P8).
- [x] Snap points; draggable (hover-expanded) scroll bars; scroll bars placed by the app (P8).
- [x] Demo pages: nested carousels, Core scroll view, gestures (Core "ScrollView + gestures", "Native overlays in ScrollView", "Native nesting").

### FR-21 / FR-22

See the requirement sections above; check items off in [Requirements.md](Requirements-1.0.md).

