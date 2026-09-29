# SkiaUi scrolling and collection views

Design notes and implementation checklists for **FR-17** (scrolling), **FR-21** (virtual / dynamic scroll layout) and **FR-22** (collection view) in [Requirements.md](Requirements.md). Aligns with [LayoutSystem.md](LayoutSystem.md), [RenderingPipeline.md](RenderingPipeline.md), [EventMechanism.md](EventMechanism.md) (gesture arena), [AnimationMechanism.md](AnimationMechanism.md) and FR-16 (`SkUiMauiContentView` overlays while scrolling).

## Goal

Provide **SkiaUi-owned** scrolling, on-demand item creation and virtualized collections, so large or scrollable UIs stay on **one shared Skia surface**. The toolkit supplies pan and fling, nested scrolling, viewport clipping and item recycling itself, without relying on MAUI `ScrollView` / `CollectionView` as the primary composition model.

Apps should be able to:

- Author a scrollable form or stack entirely under `SkUiContentView` in XAML. This is **done**: `SkUiScrollView`.
- Scroll inside Core-built complex controls. This is **done**: `SkUiCoreScrollView`.
- Nest scrollers, such as horizontal carousels in a vertical feed or a scrollable panel inside a scrollable page. This is **done**: gesture arena plus scroll chaining.
- Build endless feeds whose items are created on demand while the user scrolls, before they become visible (FR-21).
- Bind large `ItemsSource` lists with recycled templates, selection, grouping, sticky header / footer and item tap commands, without one MAUI handler per row (FR-22).

## Decision summary

| Topic | Choice |
| --- | --- |
| Scroll engine | One shared engine for both layers. `SkUiScrollController` holds the state and motion; `SkUiScrollGestureRecognizer` handles drags. It drives `SkUiScrollView` (SkUi*) and `SkUiCoreScrollView` (Core). |
| Offset model | A composite-time children translation. Scrolling never re-records content, and fling and animated scrolls run on the render thread, which reports offsets back ([RenderingPipeline.md](RenderingPipeline.md)). |
| Gestures | Scroll drags take part in the **gesture arena** ([EventMechanism.md](EventMechanism.md)). Content taps win unless the pointer moves past the touch slop along a direction the scroller can move. |
| Nested scrolling | Axis-aware claims, inner scrollers first. The part of a drag an inner scroller cannot absorb chains to outer scrollers on the same axis, and a fling goes to the innermost scroller that can move. |
| Native ancestors | Drawn continuous gestures hold native parents back while they may claim. Once none can claim, the native parent may take over (Android `RequestDisallowInterceptTouchEvent`; iOS gate recognizer). |
| On-demand items (FR-21) | **`SkUiVirtualStackLayout`**: a layout that *requests* its children from a provider as its visible window (plus prefetch) grows. It sits inside any drawn scroller. `SkUiVirtualScrollView` is the combined convenience control. |
| Collections (FR-22) | **`SkUiCollectionView`**, built on the FR-21 engine with template recycling: MAUI `CollectionView` API parity plus sticky header / footer, selection background, and item tap event / command. |
| Core layer | `SkUiCoreScrollView` is done. A Core virtual stack is added only if the FR-21 engine stays layer-agnostic, so it costs a thin wrapper. No Core collection view: templates and bindings are MAUI concepts. |
| Not primary | Nesting SkiaUi trees inside MAUI `ScrollView` / `CollectionView` is compat / migration only. Leaf views keep `HwAccelerated = false` (FR-14). |
| Layout contract | Content and items use MAUI measure / arrange ([LayoutSystem.md](LayoutSystem.md)). |
| Overlays while scrolling | **Android / Windows:** snapshot freeze by default. **Apple:** live sync. Opt-out per overlay (FR-16). |

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

## Recommended composition

```xml
<SkUiContentView>
  <SkUiScrollView>
    <SkUiVerticalStackLayout>
      <SkUiLabel Text="Recent" />
      <SkUiScrollView Orientation="Horizontal">       <!-- nested carousel: horizontal drags -->
        <SkUiHorizontalStackLayout>…</SkUiHorizontalStackLayout>
      </SkUiScrollView>
      <SkUiVirtualStackLayout ItemsSource="{Binding Feed}" PrefetchFactor="1.5"> <!-- FR-21 -->
        <SkUiVirtualStackLayout.ItemTemplate>
          <DataTemplate><local:FeedCard /></DataTemplate>
        </SkUiVirtualStackLayout.ItemTemplate>
      </SkUiVirtualStackLayout>
    </SkUiVerticalStackLayout>
  </SkUiScrollView>
</SkUiContentView>
```

```xml
<SkUiContentView>
  <SkUiCollectionView ItemsSource="{Binding Orders}" SelectionMode="Single"
                      IsStickyHeader="True" SelectionBackground="#1F0A84FF"
                      ItemTappedCommand="{Binding OpenOrder}">          <!-- FR-22 -->
    <SkUiCollectionView.Header><local:OrdersHeader /></SkUiCollectionView.Header>
    <SkUiCollectionView.ItemTemplate>
      <DataTemplate><local:OrderRow /></DataTemplate>
    </SkUiCollectionView.ItemTemplate>
  </SkUiCollectionView>
</SkUiContentView>
```

Core complex controls use `SkUiCoreScrollView`:

```csharp
var list = new SkUiCoreScrollView().SetContent(new SkUiCoreVerticalStackLayout().Add(...));
```

## Scroll engine (implemented)

```
SkUiScrollView (SkUi*) ─┐                      ┌─ SkUiCoreScrollView (Core)
                        ├─ SkUiScrollController ┤
                        │   offset / extent / viewport / orientation, clamping
                        │   render-thread tween (ScrollToAsync / AnimateScrollTo) and fling
                        │   wheel (innermost scroller that can move)
                        │   ScrollBy → remainder (for chaining)
                        └─ SkUiScrollGestureRecognizer (arena member)
```

| Concern | Behavior |
| --- | --- |
| **Viewport** | The scroller's arranged size. Children are clipped to it (`ChildrenClipRect`), and hit-testing stops at it. |
| **Content extent** | The content's desired size, measured unconstrained along the scroll axes. Content is arranged at `max(extent, viewport)`. |
| **Offset** | `(ScrollX, ScrollY)`, clamped to `[0, max(0, extent − viewport)]`. It is a children translation, so there is no re-record, remeasure or rearrange. |
| **Motion** | Tweens and flings run on the render thread with exponential decay. The last shown offset is reported back when motion stops, and a press during motion stops it. |
| **RTL** | Horizontal scrollers start at their logical start (the right end). Children are mirrored inside the extent (`ChildrenSpaceWidth`). |
| **Overlays** | `SkUiScrollView` syncs registered `SkUiMauiContentView` descendants on each offset change. |

### Gestures and nesting rules (implemented)

- **Claiming:** a scroller claims when the pointer moves more than `SkUiGestureSettings.TouchSlop` along an enabled axis, that axis dominates the movement, and the scroller can move in that direction. Until then, content recognizers (taps, pans, swipes) compete normally.
- **Orthogonal nesting:** each scroller gets the drags of its own axis (a horizontal carousel inside a vertical page).
- **Same-axis nesting:** the inner scroller claims first while it can move. The part of each drag it cannot absorb goes to the next outer scroller on that axis. A drag that starts at the inner scroller's edge, moving outward, is claimed by the outer scroller.
- **Fling:** the fling goes to the innermost scroller that can move in its direction.
- **Stopping a fling:** a press during a fling stops it and claims the pointer, so the content under the finger is not tapped.
- **Press feedback:** a button inside a scroller shows its pressed state after `PressDelay` (100 ms) unless a scroll starts first; there is no flash while scrolling.
- **Native ancestors:** while a drawn scroller may still claim (within the slop), native ancestors are held back. If the drawn scroller cannot move in the drag direction, the native parent takes over once the finger passes the slop.

### Overlays while scrolling (FR-16)

Native overlays (`SkUiMauiContentView`) sit as **sibling platform views** of the Skia surface. Their frames must track the placeholder's arranged bounds, which move when the scroll offset changes.

| Strategy | Behavior |
| --- | --- |
| **Live sync** | On every scroll frame, set the native view's position, transform and clip to match the placeholder. The native view stays visible and interactive. |
| **Snapshot freeze** | When scrolling starts, capture a bitmap of the native view, hide the view, and paint the bitmap on the Skia canvas. When motion settles, show the native view again. |

#### Decided policy

| Platform | Default while scrolling / flinging | Notes |
| --- | --- | --- |
| **Android** | **Snapshot freeze** | Automatic under an actively scrolling scroller |
| **Windows** | **Snapshot freeze** | Same as Android |
| **iOS / Mac Catalyst** | **Live sync only** | The native overlay is repositioned each frame |
| **All** | **Opt-out** | `SkUiMauiContentView.ScrollMode` = `Live` (or `Snapshot` to opt in on Apple) |

**Rationale:** Android and Windows cannot cheaply keep live native overlays in step with 60 fps Skia motion; Apple platforms usually can.

#### Implemented

- **Motion signal:** the scroll engine reports motion start / end: drag (including outer scrollers moving through chained drags), fling, and animated scroll. An instant `ScrollTo` is not motion.
- **On motion start:** each overlay under the moving scroller captures its native view, hides it, and draws the bitmap as ordinary drawn content (render-thread composited, clipped by the viewport).
  - Capture: Android `View.Draw` (unaffected by what covers the view on screen), iOS `DrawViewHierarchy`, Windows `RenderTargetBitmap` (WebView2: `CoreWebView2.CapturePreviewAsync`, which `RenderTargetBitmap` cannot capture).
- **Restore:** `SkUiMauiContentView.SnapshotRestoreDelay` (150 ms) after motion stops, with fresh bounds. A new drag within the delay reuses the snapshot.
- **Focus:** a focused control stays live.
- **Clipping:** every overlay sits in a clip wrapper sized to its visible rectangle (ancestor scroll viewports and clipping ancestors), so it neither draws nor takes touches outside it.
  - On Android, wrappers are positioned directly: the MAUI parent may skip re-measuring the container, so a relayout request is not enough.
- **Demo:** "Native overlays in ScrollView" (mode switch, snapshot highlighting, restore delay).
- **Verified** on a Galaxy S9: snapshots during the drag, restore after, clipping under the drawn header / footer, typing into an Entry inside a nested carousel. On the iOS simulator: live sync and clipping.
- **Verified on Windows 11** (mouse, GPU and software surfaces; [WindowsValidation-results.md](WindowsValidation-results.md)): snapshots during the drag (WebView included), restore after, clipping and hit-test clipping under the drawn header, focused controls stay live, Live mode. On Windows the UI thread also composites, so a UI stall pauses the fling together with the snapshots.

## FR-21 — Virtual / dynamic scroll layout (requirements)

**Purpose:**
- Endless scrolling (feeds, logs, search results loaded page by page).
- The item engine underneath `SkUiCollectionView`.

The layout **requests** children from a provider while scrolling, before they reach the visible area, so scrolling stays fluent.

### Shape

- **`SkUiVirtualStackLayout`:** a stack layout whose children are created on demand. Vertical is required first; horizontal is a later extension.
  - It works as the content of a drawn scroller or anywhere below one (several virtual sections in one page, headers above the list).
  - It also works nested inside another virtual layout.
- **Visible window:** the layout computes its window as the intersection of the viewports of all ancestor scrollers, in its own coordinates. It does not care which ancestor scrolls.
- **`SkUiVirtualScrollView`:** a convenience control combining a vertical scroller with a virtual stack, for the common single-list case.
- **Core variant `SkUiCoreVirtualStackLayout`:** optional. It is added only if the engine is written against the shared render / input node contracts, so that it is a thin wrapper.

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
- **Creation budget:** items are created within a per-frame UI-thread budget (for example `CreationBudget` = 4 ms), spread over frames.
  - Creation is synchronous only when the visible area would otherwise show a gap.
  - All work stays off the render thread (NFR-6).
- **Nested virtual layouts** receive the clipped window of their ancestor, so an inner list inside a card only realizes what could be visible.

### Release, recycling and sizing

- **`ReleaseFactor`:** items farther than this many viewport lengths from the window are released (default: never in endless-append mode). Their measured size is kept, so the extent and scroll position stay stable.
- **Recycling:** optional here, required for FR-22. Released items return to a pool keyed by template / recycle key and are rebound (`BindingContext`) instead of recreated.
- **Sizing:**
  - `EstimatedItemSize` covers items not yet measured, and measured sizes are cached per index.
  - The extent is measured plus estimated; for unknown counts it grows as items are appended.
  - When items before the visible area change size (or are inserted), the first visible item keeps its position on screen (scroll anchoring).

### API, events, behavior

- **Scrolling to an index:** `ScrollToIndex(index, position = MakeVisible | Start | Center | End, animated)`. It realizes the target, using estimates in between.
- **Events:**
  - `ItemRealized` / `ItemReleased` (index, view), for loading images or data;
  - `VisibleRangeChanged` (first / last visible index).
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

## FR-22 — `SkUiCollectionView` (requirements)

A virtualizing, recycling list / grid built on the FR-21 engine.

### MAUI `CollectionView` parity

| Area | Members |
| --- | --- |
| **Data** | `ItemsSource`, `ItemTemplate`, `ItemTemplateSelector`, `EmptyView` / `EmptyViewTemplate`; `INotifyCollectionChanged` incremental updates |
| **Header / footer** | `Header` / `HeaderTemplate`, `Footer` / `FooterTemplate` |
| **Layout** | `ItemsLayout`:<br>• `LinearItemsLayout` (vertical / horizontal, `ItemSpacing`)<br>• `GridItemsLayout` (`Span`, horizontal / vertical spacing)<br>• `SnapPointsType` / `SnapPointsAlignment`<br>• `ItemSizingStrategy` (`MeasureAllItems` / `MeasureFirstItem`) |
| **Selection** | `SelectionMode` (None / Single / Multiple), `SelectedItem`, `SelectedItems`, `SelectionChanged`, `SelectionChangedCommand` (+ parameter). The item root gets the `Selected` visual state. |
| **Grouping** | `IsGrouped`, `GroupHeaderTemplate`, `GroupFooterTemplate` |
| **Scrolling** | `ScrollTo(index / item, groupIndex, position, animate)`, `ScrollToRequested`, `Scrolled` (deltas, offsets, first / center / last visible index), `HorizontalScrollBarVisibility` / `VerticalScrollBarVisibility` |
| **Incremental loading** | `RemainingItemsThreshold`, `RemainingItemsThresholdReached` (+ command) |
| **Updates** | `ItemsUpdatingScrollMode`: `KeepItemsInView`, `KeepScrollOffset`, `KeepLastItemInView` |
| **Reordering** | `CanReorderItems`, `CanMixGroups`, `ReorderCompleted`. Drag starts on long press, through the gesture arena. |

### Additional requirements

- **Sticky header / footer:**
  - `IsStickyHeader` / `IsStickyFooter` keep the header / footer pinned while items scroll beneath.
  - `IsStickyGroupHeader` pins the current group's header.
  - Pinned parts are separate composite nodes, so scrolling does not re-record them.
- **Selected item background:**
  - `SelectionBackground` (brush) is drawn behind the selected item's content, with an optional `SelectedItemTemplate` override.
  - A selection change re-records only the affected items.
- **Item tap:** `ItemTapped` event and `ItemTappedCommand` (+ `ItemTappedCommandParameter`, default: the item).
  - The event args carry the item, index, group and position.
  - It is raised whether or not selection is enabled; selection updates after tap handlers.
  - Taps on interactive children inside an item (buttons) do not raise `ItemTapped`; the innermost recognizer wins.
- **Pull to refresh:** `IsRefreshing` / `RefreshCommand`, driven by pulling at the scroll start (a drawn refresh indicator).
- **Candidate extras** (prioritize after the above):
  - `ItemDoubleTapped` / `ItemLongPressed` (+ commands);
  - swipe actions on items (leading / trailing templates);
  - a "load more" footer mode (automatic or on tap);
  - item appearing / disappearing events;
  - keyboard navigation with a focused item (desktop);
  - animated insert / remove.

### Behavior and cost

- **Items:** cells are hosted drawn nodes with no platform view. `SkUiMauiContentView` inside cells is discouraged in large lists, because each one is a native overlay.
- **Recycling:** per template / selector result. A rebind changes `BindingContext` and re-records only the changed nodes.
- **Measurement:** `MeasureFirstItem` measures one item per template. `MeasureAllItems` measures realized items and caches the results.
- **Grid layout:** items are placed in `Span` columns; a row is realized as a unit.
- **Acceptance:**
  - 10k-item list at device fps during fling;
  - selection change re-records at most two items;
  - sticky header costs nothing while scrolling (no re-record);
  - `ItemTapped` versus a button inside an item;
  - grouping and sticky group headers;
  - incremental load;
  - `ItemsUpdatingScrollMode`.

## Delivery order

| Milestone | Deliverable | Status |
| --- | --- | --- |
| Scroll engine | `SkUiScrollView`: offsets, render-thread fling / tween, wheel, clip, RTL start | **Done** |
| Gestures | Gesture arena; scroll as arena member; press delay; drags inside scrollers | **Done** |
| Nested scrolling | Orthogonal and same-axis nesting, drag and fling chaining, native-parent coordination | **Done** |
| Core scrolling | `SkUiCoreScrollView` on the shared engine (Core and SkUi* nest freely) | **Done** |
| FR-21 | `SkUiVirtualStackLayout` (vertical), provider / binding, prefetch, budget, anchoring; `SkUiVirtualScrollView` | Next |
| FR-21 | Release + recycling pool; fling-predictive prefetch; horizontal; optional Core variant | Next |
| FR-22 | `SkUiCollectionView` linear layout: templates, selection, header / footer (sticky), item tap, empty view | Later |
| FR-22 | Grouping (sticky group headers), grid layout, incremental loading, updating scroll modes, pull to refresh | Later |
| FR-22 | Reordering, candidate extras | Later |
| Polish | Scrollbars, snap points, overscroll / bounce, keyboard / focus bring-into-view, horizontal wheel for `Both` | Later |

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
- [ ] Scrollbars, snap points, overscroll / bounce.
- [x] Demo pages: nested carousels, Core scroll view, gestures (Core "ScrollView + gestures", "Native overlays in ScrollView", "Native nesting").

### FR-21 / FR-22

See the requirement sections above; check items off in [Requirements.md](Requirements.md).

## Open items

- Scrollbar visuals (look, FR-18) and auto-hide policy.
- Overscroll: clamp (current) versus rubber-band bounce per platform.
- The horizontal wheel for `Orientation = Both` (needs an axis-aware wheel event).
- Accessibility / semantics for scrollable regions and collections (platform automation peers).

## References

- [Requirements.md](Requirements.md): FR-13 / 14 / 15 / 16 / 17 / 21 / 22, NFR-2 / 6.
- [EventMechanism.md](EventMechanism.md): gesture arena, recognizers, native coordination.
- [RenderingPipeline.md](RenderingPipeline.md): composite-time offsets, render-thread motion.
- [LayoutSystem.md](LayoutSystem.md): hosted measure / arrange without handlers.
- Local **DrawnUi**: `SkiaScroll`, virtualization / recycling templates, items windowing.
- Local **Flutter**: `scrollable.dart`, `viewport.dart`, slivers, cache extent.
- Local **Avalonia**: `ScrollViewer`, `VirtualizingStackPanel`, `ILogicalScrollable`.
- Local **Uno**: `ScrollViewer`, `ItemsRepeater`.
- Local **MAUI**: `ScrollView` / `CollectionView` handlers (native), for API parity reference only.
