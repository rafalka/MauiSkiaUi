# SkiaUi scrolling and collection views

How scrolling (FR-17), virtual lists (FR-21) and the collection view (FR-22) work, on both layers. Overview: [Architecture.md](Architecture.md#scrolling-and-lists); user docs: [SkUiScrollView.md](../controls/SkUiScrollView.md), [SkUiVirtualVerticalStackLayout.md](../controls/SkUiVirtualVerticalStackLayout.md), [SkUiCollectionView.md](../controls/SkUiCollectionView.md). The requirement checklists and the delivery order are archived in [docs/archive/ScrollingAndCollectionViews-requirements.md](../archive/ScrollingAndCollectionViews-requirements.md).

## Goal

Provide **SkiaUi-owned** scrolling, on-demand item creation and virtualized collections, so large or scrollable UIs stay on **one shared Skia surface**. The toolkit supplies pan and fling, nested scrolling, viewport clipping and item recycling itself, without relying on MAUI `ScrollView` / `CollectionView` as the primary composition model.

Apps should be able to:

- Author a scrollable form or stack entirely under `SkUiContentView` in XAML (`SkUiScrollView`).
- Scroll inside Core-built complex controls (`SkUiCoreScrollView`).
- Nest scrollers, such as horizontal carousels in a vertical feed or a scrollable panel inside a scrollable page (gesture arena plus scroll chaining).
- Build lists and carousels whose items are created on demand while the user scrolls, before they become visible (FR-21).
- Bind large `ItemsSource` lists with recycled templates, selection, grouping, sticky header / footer and item tap commands, without one MAUI handler per row (FR-22).

## Decision summary

| Topic | Choice |
| --- | --- |
| Scroll engine | One shared engine for both layers. `SkUiScrollController` holds the state and motion; `SkUiScrollGestureRecognizer` handles drags. It drives `SkUiScrollView` (SkUi*) and `SkUiCoreScrollView` (Core). |
| Offset model | A composite-time children translation. Scrolling never re-records content, and fling and animated scrolls run on the render thread, which reports offsets back ([RenderingPipeline.md](RenderingPipeline.md)). |
| Gestures | Scroll drags take part in the **gesture arena** ([EventMechanism.md](EventMechanism.md)). Content taps win unless the pointer moves past the touch slop along a direction the scroller can move. |
| Nested scrolling | Axis-aware claims, inner scrollers first. The part of a drag an inner scroller cannot absorb chains to outer scrollers on the same axis, and a fling goes to the innermost scroller that can move. |
| Overscroll | Per look (`SkUiLook.DefaultOverscroll`; the default look follows the platform) or per scroller (`Overscroll`): bounce (rubber band, iOS) or stretch (Android 12+). Drawn through the generic children transform (offset past its range, or children scale), so a render-thread fling bounces with no UI work. Only what no scroller can use overscrolls, so chaining is unchanged. |
| Scroll bars | Public Core nodes (`SkUiCoreScrollBar`): the bar is the track (pinned to the viewport, or placed by the app), its thumb a scroll-linked child drawn by the look once per length; the compositor moves it from the offset and the UI starts render-thread fades. A hovering pointer expands the bar for dragging and paging. Fading bars (`Default`) draw over the content; bars that always show reserve a gutter, and the content gets the rest (the scrollport). Reused by FR-21 / FR-22 through the shared controller. |
| Snap points | `SnapPointsType` / `SnapPointsAlignment` (MAUI's CollectionView enums) on the content's children; drags, flings and paused wheel input settle with a render-thread spring (`SkUiRenderScrollSpring`) that never passes its target. FR-22's `ItemsLayout` snap points can reuse it. |
| Native ancestors | Drawn continuous gestures hold native parents back while they may claim. Once none can claim, the native parent may take over (Android `RequestDisallowInterceptTouchEvent`; iOS gate recognizer). |
| On-demand items (FR-21) | **`SkUiVirtualVerticalStackLayout`**: a layout that *requests* its children from a provider as its visible window (plus prefetch) grows. It sits inside any drawn scroller; `SkUiVirtualScrollView` is the combined convenience control. Vertical, with an indexed virtual extent ([Virtual stack](#virtual-stack-implemented)); infinite feeds and loops (`VirtualScrollMode`) are on the [roadmap](Roadmap.md#lists-and-scrolling). |
| Collections (FR-22) | **`SkUiCollectionView`**, built on the FR-21 **indexed** engine with template recycling. **SkUi-first** API (not MAUI parity); MAUI `CollectionView` mapping lives in [Migration.md](../Migration.md) and the [migration skills](../../plugins/skiaui-migration/README.md). Done (B2–B3, see [Collection view](#collection-view-implemented)): lists and grids, vertical and horizontal, groups (collapsible, sticky headers), single and multiple selection, loading more. |
| Core layer | `SkUiCoreScrollView` is done. A Core virtual stack is added only if the FR-21 engine stays layer-agnostic, so it costs a thin wrapper. No Core collection view: templates and bindings are MAUI concepts. |
| Not primary | Nesting SkiaUi trees inside MAUI `ScrollView` / `CollectionView` is compat / migration only. Leaf views keep `HwAccelerated = false` (FR-14). |
| Layout contract | Content and items use MAUI measure / arrange ([LayoutSystem.md](LayoutSystem.md)). |
| Overlays while scrolling | **Android / Windows:** snapshot freeze by default. **Apple:** live sync. Opt-out per overlay (FR-16). |

## Recommended composition

```xml
<SkUiContentView>
  <SkUiScrollView>
    <SkUiVerticalStackLayout>
      <SkUiLabel Text="Recent" />
      <SkUiScrollView Orientation="Horizontal">       <!-- nested carousel: horizontal drags -->
        <SkUiHorizontalStackLayout>…</SkUiHorizontalStackLayout>
      </SkUiScrollView>
      <SkUiVirtualVerticalStackLayout ItemsSource="{Binding Feed}" PrefetchFactor="1.5"> <!-- FR-21 -->
        <SkUiVirtualVerticalStackLayout.ItemTemplate>
          <DataTemplate><local:FeedCard /></DataTemplate>
        </SkUiVirtualVerticalStackLayout.ItemTemplate>
      </SkUiVirtualVerticalStackLayout>
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
| **Overlays** | `SkUiScrollView` syncs registered `SkUiMauiContentView` descendants on each offset change (and bounce). |
| **Overscroll** | A drag's leftover after chaining pulls the dragged scroller past its edge through a rubber band (shown = `(1 − 1 / (pull · 0.55 / viewport + 1)) · viewport`); a drag back takes the pull back first; the release springs back (critically damped, on the render thread). A fling that reaches an edge runs past it with its velocity there (peaking within 15 % of the viewport) and settles. At an edge a scroller claims an outward drag only when no outer drawn scroller and no native ancestor (`SkUiPointerRouter.NativeAncestorCanScroll`) can scroll that way. |
| **Scroll bars** | MAUI's `ScrollBarVisibility` per axis: `Default` shows while the offset changes and fades out after the look's delay, `Always` while the content overflows, `Never` hides. The vertical bar is on the left in RTL; both bars leave the corner free. |
| **Scroll to a target** | `ScrollToAsync(Element / Core node, ScrollToPosition, animated)` with MAUI's `GetScrollPositionForElement` arithmetic (nested scrollers' offsets included), waiting for the first layout; `ScrollToRequested` with MAUI's arguments. |
| **Wheel** | Two axes (`WheelDelta`, `WheelDeltaX`); each axis is used up by the innermost scroller that can move that way. |

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
- **Verified on Windows 11** (mouse, GPU and software surfaces; [WindowsValidation-results.md](../archive/WindowsValidation-results.md)): snapshots during the drag (WebView included), restore after, clipping and hit-test clipping under the drawn header, focused controls stay live, Live mode. On Windows the UI thread also composites, so a UI stall pauses the fling together with the snapshots.

## Virtual stack (implemented)

FR-21's indexed mode, vertical: `SkUiVirtualVerticalStackLayout` and `SkUiVirtualScrollView` ([control guide](../controls/SkUiVirtualVerticalStackLayout.md)). The engine is the abstract `SkUiVirtualVerticalStackLayoutBase`; subclasses create and bind item views by index, give recycle keys and report item changes (`SkUiVirtualVerticalStackLayout` does it for `ItemsSource` / `ItemTemplate` / `ItemFactory`; FR-22's collection view is another subclass, whose items are its rows). It derives from `SkUiView`, not `SkUiLayout`: its children are the realized items only, in index order (`SkiaChildren`, render children), added as logical children without `SkUiLayout`'s per-child measure invalidation.

```
ancestor SkUiScrollView(s) ── offset / viewport changes ──▶ ISkUiScrollListener (registered, no tree walks)
        │                                                          │
        ▼                                                          ▼
SkUiVirtualVerticalStackLayout ── window (∩ of ancestor viewports) ──▶ Realize(phase)
        │   SkUiVirtualItemSizes: measured sizes, estimates, Fenwick offsets       │ release far items → pool per template
        │                                                          │ cover window (sync), prefetch (budgeted, clock frames)
        └── anchoring: offset correction ──▶ SkUiScrollController.CorrectOffset ──▶ render-thread motion shifted in the same frame
```

| Concern | Behavior |
| --- | --- |
| **Window** | Walk up the drawn ancestors: each `SkUiScrollView` clips to `[offset, offset + viewport]` in its children space, the surface root to its height. Before the first arrange, positions are unknown (taken as 0) and a scroller's viewport is the one its measure offered the content (`VerticalWindow`), so the first measure realizes the first screen. Without any bound nothing is realized. |
| **Sizes** (`SkUiVirtualItemSizes`) | Per-index measured heights (`NaN` until measured) and two Fenwick trees (sum, count of measured items): `OffsetOf(i)` in O(log n), `IndexAt(y)` by binary search; unmeasured items use `EstimatedItemSize` or the measured average. `ItemExtent`: offsets are a multiplication. Appends keep the trees; inserts, removes and moves rebuild them (O(n)). A new width forgets every size. |
| **Phases** | *Measure*: realized items are measured again (cached unless dirty), then the window is realized; the layout reports the sum of sizes as its height. *Arrange*: the window is realized with the real geometry and items are arranged at their offsets. *Update* (scroll listener, prefetch frames, collection changes): realize, arrange, and when the height changed, `InvalidateMeasureFromChild` (a relayout before the next frame, no re-record). A surface root relays out up to three passes per frame (`RelayoutIfNeeded`), so a relayout that realizes items converges in the same frame. Events raised while measuring are deferred to the arrange (a handler changing the layout would be lost by the measure). |
| **Realization** | Release items outside the release distance (from the ends: the realized range stays contiguous), then cover the visible window at once, then prefetch ahead in the scroll direction and behind, within a budget per pass (at least one item); unfinished prefetch continues on the surface's `SkUiAnimationClock` frames. The budget is automatic unless `PrefetchBudget` fixes it: the clock measures the UI frame interval from its ticks (lower quartile of the last 16, so dropped frames do not lengthen it); each list keeps a moving average of what realizing an item costs; idle passes get a quarter of a frame, scrolling passes the cost of the items scrolling into the prefetch area per frame (× 1.5) between a quarter and three quarters of a frame, doubled (up to 4×, decaying) after a scrolling pass had to create visible items. While a render-thread motion runs, the window ahead grows by velocity × 100 ms (at most two viewports). A motion that passes more than a viewport per frame (`SkUiScrollController.MotionTravel`, between its reports), or an animated `ScrollToIndex` still more than two viewports from its item, is *racing*: each item on the way shows for a frame at most, so only visible items are created (no prefetch), and the estimate stays frozen until it slows down (an average moving with every measured item would move the target by thousands of DIPs per pass and realize ever more items). Released views are pooled up to the most ever realized at once, so a far jump recycles them all. An animated scroll to a target re-resolves it when it completes and lands exactly there (`ScrollToTargetAsync`). Measured on a Galaxy S9 (Release): an animated scroll to the middle of 10,000 rows takes about 330 ms, its 300 ms animation included, realizing ~110 rows and creating no new views once warm. |
| **Anchoring** | The anchor is the first realized item that shows (not while the list's start shows), or the item a `ScrollToIndex` aims at (pinned until its task completes). After each realization or remeasure, a change of the anchor's offset moves the window, and the innermost vertical scroller's offset by the same delta (`CorrectOffset`; the extent grows by it until the next measure, so nothing clamps it first). Collection changes map the anchor's index through the change. |
| **Corrections during motion** | `CorrectOffset` while a fling, tween or snap runs on the render thread: the UI acknowledges the corrected offset (not an explicit change, which would stop the motion) and queues `SkUiRenderState.PendingScrollShift`; the recorder sends it as `SkUiRenderUpdate.ScrollShift` with the frame that carries the new layout, and the compositor moves the motion (`SkUiRenderAnimation.ShiftScroll`: fling start, tween start and target, spring and settle targets) and the shown offset. Motions report offsets without their applied shifts; the controller adds the shifts it requested for the current motion, so reports from frames before the shift was applied do not pull the offset back. Once committed, a node's scroll offset and scale on the render thread change only by its motions, corrections and values the UI sets explicitly: a value the UI only acknowledged can be older than what shows (a motion that ended there before its last report reached the UI), so it never moves the content back, and a correction arriving after the motion ended moves the shown offset once. An animated scroll to a target lands on it when it completes only while it is still the latest scroll request. |
| **Recycling** | Released views go to a pool per recycle key (`GetRecycleKey`; for `SkUiVirtualVerticalStackLayout` the template a `DataTemplateSelector` chose, and one key for default `SkUiLabel`s) and are rebound (`BindItemView`; there, by `BindingContext`) after `UnbindItemView`. Detaching resets their render state (re-recorded when reused, as the content changed anyway). Factory views are not pooled (`ItemReleased`). |
| **Collection changes** | `INotifyCollectionChanged` through a weak observer. Add / remove / replace / move update sizes and realized indices (moved items keep their measured sizes; an `Add` without its index starts over); items inserted inside the realized range are realized there; unaffected items keep their views and sizes. Non-list sources are copied and reset on change. |

Not yet: a public horizontal layout (the engine's axis is generic and the collection view uses it), `InfiniteFeed` / `Loop` (`VirtualScrollMode`), `PrefetchDistance` in DIPs, `QueryItemSize`, an async load-more hook with a placeholder item, the Core twin, raster caching of rows. Device runs of the `virtual-fling` benchmark and the `VirtualListScrolled` leak scenario are pending.

## Collection view (implemented)

FR-22: `SkUiCollectionView` ([control guide](../controls/SkUiCollectionView.md)). A composite drawn view; nothing in it is a platform view. The engine's axis is generic (`SkUiVirtualVerticalStackLayoutBase.IsHorizontal`, internal): sizes, offsets, the window (`HorizontalWindow` of each scroller, mirrored right to left), scroll corrections and scroll targets follow it.

```
SkUiCollectionView                      (render order; screen-reader order: header, sticky group header, list, footer)
 ├─ SkUiScrollView            (fills the view, vertical or horizontal; Insets = sticky sizes; PullsAtVerticalStart with pull-to-refresh)
 │   └─ body (after the start inset): header host · load-more host (Start) · items · empty view host · load-more host (End) · footer host
 │              items = ItemsPart (an engine subclass) over ItemsModel's rows:
 │                 item row        → item container (SkUiContentView: taps, selection background) ── template view (Selected state)
 │                 grid row        → GridRowPart ── Span item containers (cells pooled per template)
 │                 group header    → GroupHeaderHost (taps expand / collapse; Expanded / Collapsed state)
 │                 group footer    → the template's view
 ├─ sticky group header host  (IsStickyGroupHeader: the current group's header, after the start inset; translated while pushed)
 ├─ sticky header host        (IsStickyHeader: over the start; its own drag recognizer for the scroller)
 ├─ sticky footer host        (IsStickyFooter: over the end)
 └─ refresh layer             (between the sticky parts, input-transparent, clipped): indicator moved by composite-time props
```

| Concern | Behavior |
| --- | --- |
| **Rows** (`ItemsModel`) | The engine's items are rows: an item, a grid row of up to `Span` items, a group header or footer. A flat single-column list has no table (row `i` is item `i`); others keep a table (`Row(Kind, Group, Start, Count)`) and per group its items, item count, expanded state, first row and first item. Changes are mapped to the smallest row changes: one item per row → the same insert / remove / replace / move of rows; grid rows → the rows from the first changed item are rebound (`ReplaceItems`) and rows added or removed at the group's end; group inserts / removes → their row ranges; resets and changes out of step → the group's rows (or everything) again. Sources and each group are listened to weakly (`INotifyCollectionChanged`; `INotifyPropertyChanged` of `ISkUiExpandableGroup` groups). API indices count items across groups (collapsed included); `FirstVisibleIndex` / `LastVisibleIndex` come from the engine's visible rows, skipping headers and footers. |
| **MAUI's layout and sizing** | `ItemsLayout` (MAUI's `LinearItemsLayout` / `GridItemsLayout`) is not a second layout path: it writes `Orientation`, `Span`, `ItemSpacing` and `SpanSpacing`, again on the layout object's `PropertyChanged` (weakly followed: layouts are often shared and static). `ItemSizingStrategy.MeasureFirstItem` turns on `SizesFromFirstItem` in the virtual stack: the first row measured becomes the fixed extent (the fast path), cleared whenever the sizes are forgotten, so the next row measured sets it again; grouped lists keep measuring every row |
| **Items** | Recycle keys: the item's (selected) template, or one key for default labels; one key for grid rows; per kind and template for group headers and footers. An item container is created with its template's view and recycled with it. Binding sets the container's binding context (the template view inherits it) and its selected state. A grid row keeps its cells while their template fits the item they show next; others go back to a pool of cells per template, and pooled cells of the right template take their place. Indices are not kept in containers (collection changes move them): a tap looks its container (or its grid row and slot) up among the realized rows. |
| **Selection** | Compared with `Equals`. Single: `SelectedItem`. Multiple: `SelectedItems` (any `IList<object>`; an observable one is followed weakly) mirrored in a set for lookups while binding; taps, `SelectAll` and `ClearSelection` change the list in one step and raise one `SelectionChanged` with both lists. A change walks the realized containers (and grid cells) and sets their state; only those whose state changes repaint and move their template root's `CommonStates` (`SkUiView.IsSelectedItem`: `Disabled`, then `Selected`, then `PointerOver`, then `Normal`). Removed items (and items of removed groups) leave the selection. |
| **Keeping the selection visible** | `KeepSelectionVisible`: after every selection change (not when items leaving the source shrank it) and after an arrange with a new size (or the first one), the list reveals the selection through the engine's `ScrollToIndex` (sizes, sticky insets and the sticky group header accounted). Single: `MakeVisible` on the selected row. Multiple: one scan of the rows gives the selected rows in order; around a focus row (the row of the item a tap last selected, else the first selected one) a two-pointer search over their offsets picks the window that fits the uncovered band and holds the most; when what shows already holds as many (and the focus), nothing moves, else the window's first row goes to the start or its last to the end, whichever needs less. A tap's own change only uses `MakeVisible` on the tapped row. Animated for selection changes, immediate for size changes; before the first layout the reveal waits for it. |
| **Taps** | The container takes single taps while a selection mode, an `ItemTapped` handler or a command needs them. Child-first hit-testing gives views inside the item that take taps their own (buttons). Order: cancelable `SelectionChanging`, the selection (command, `SelectionChanged`), `ItemTapped` (item, index across groups, group), `ItemTappedCommand`. |
| **Groups** | Expanding or collapsing changes the group's rows (items and footer inserted / removed): collapsed items are not realized, and anchoring keeps what shows in place. `GroupExpanding` / `GroupCollapsing` can cancel (taps and API); a group's own `ISkUiExpandableGroup.IsExpanded` is written on changes and followed when it changes. The realized header (and the sticky one) moves to `Expanded` / `Collapsed`. Scrolling to an item of a collapsed group expands it. |
| **Sticky group header** | On every scroll report (also each frame of a render-thread fling), layout and change: the row at the start of the uncovered band (engine `VisibleStart` + start inset) gives the group; while its own header has scrolled past, a header view for the group (one per template, rebound when the group changes) shows over the list, translated back by the next group's header when it comes closer than its size (no re-record while scrolling). The engine's `ScrollTargetInset` keeps that header's size free before items scrolled to. A tap on it collapses the group and scrolls its header to the start. |
| **Header / footer** | One view each, moved between the sticky host (drawn over the scroller) and the scrolled host in the body when `IsStickyHeader` / `IsStickyFooter` change. The four hosts are created the first time they show a view (most lists have no header or footer, fewer sticky ones) and kept after. The scroller fills the view and the items scroll behind sticky parts; their measured sizes along the axis (margins included) become the scroller's `Insets` (`SkUiScrollController.Insets`: start / end of the scroll axis, the start on the right of a right-to-left horizontal scroller), set while the collection measures and invalidating only the scroller: the content is arranged after the start inset and the extent grows by both, so nothing is covered at the start or the end; `GetOffsetFor` (scroll to an item or element, `MakeVisible`) works in the uncovered band; the scroll bar of that axis runs beside it. Items behind the sticky parts are realized and count as visible (they show through translucent parts). A sticky host has its own `SkUiScrollGestureRecognizer` for the list's controller and is a wheel proxy (`ISkUiWheelProxy`): a drag or the wheel on it scrolls the list and a press stops a fling, while a tap is claimed by nothing and so never reaches the items behind; views in it keep their taps. Templates (`SkUiContentSlot`) run once the list is in a tree, with its binding context. |
| **Data headers** | `HeaderProperty`, `FooterProperty` and `EmptyViewProperty` take a view or, as on MAUI, data: the content slot (`SkUiContentSlot`) then makes the view shown, the template's with the data as its binding context or a label of its text, once the list is in a tree, and the list places what the slot shows |
| **Empty view** | Shown instead of the items while there are no rows; the body arranges it over the length the viewport has left beside the other parts (the scroller arranges its content at least as long as the viewport). Its template runs the first time the list is empty. |
| **Load more** | A load-more host in the body before or after the items (created the first time it shows): visible in manual mode while the command can execute (its `CanExecuteChanged` is followed weakly), and in every mode while loading. Automatic modes ask when the engine's first (start) or last (end) visible row is the first or last row, or there are none, checked on visible-range changes, scrolls and changes; `AutoOnUserScroll` once a scroll happened while dragging. `IsLoadMoreActive` stops further asks until the app clears it; the next layout checks again. At the start, `AnchorsAtStart` keeps what shows in place also while the start of the items shows, so inserted older items push the offset, not the content. |
| **Pull to refresh** | `SkUiScrollController.PullsAtVerticalStart` lets a drag pull the top past the edge when the content does not overflow or overscroll is off (the pull is tracked in `OverscrollY` and springs back on the render thread; the content moves only as the overscroll mode draws it). The shared helper and indicator do the rest ([Pull to refresh](#pull-to-refresh-implemented)): the indicator follows `OverscrollChanged` at composite time, `PullReleased` past the trigger distance sets `IsRefreshing`, which (as MAUI's `RefreshView`) raises `Refreshing` and runs the command. Vertical lists only. |

Row swipe actions: [`SkUiSwipeView`](../controls/SkUiSwipeView.md) in the item template (C1; the swipe loses vertical drags to the list, a scroll closes an open row, a recycled row shows closed). Not yet: reordering, snap points on the items, keyboard item navigation, extended (Shift / Ctrl) selection, animated insert / remove.

## Pull to refresh (implemented)

Two owners share one helper, `SkUiPullToRefresh`: the indicator (the public Core node `SkUiCoreRefreshIndicator` in an input-transparent, clipping layer, `SkUiRefreshLayer`), placed for the owner's style; a pull released past the trigger distance (the owner's `RefreshTriggerDistance`, or the look's) starting a refresh; and `IsRefreshing` becoming `true` raising `Refreshing` and running the command, then, with automatic completion, setting it back. Owners implement `ISkUiRefreshOwner` (their `IsRefreshing`, whether a refresh can start, the command, style, trigger distance, mouse setting, completion, the event).

- **A list that refreshes by itself** (`IsPullToRefreshEnabled`): `SkUiCollectionView` lets the helper pull its scroller at its vertical start (`SkUiScrollController.PullsAtVerticalStart`, `PullReleased`, `OverscrollChanged`) and places the layer between its sticky parts.
- **`SkUiRefreshView`** ([SkUiRefreshView.md](../controls/SkUiRefreshView.md)), MAUI's `RefreshView` around any drawn content, reports the pulls of the scrollers in its content and of itself to the same helper:

| Concern | Behavior |
| --- | --- |
| **Which scroller** | Chosen on each press, from the gesture arena: the outermost vertical scroller recognizer between the pressed node and the refresh view (members are innermost first), so side-by-side lists each pull, and inner scrollers chain to it at their top. Scrollers inside a nested refresh view are left to it. No tree walk and no hook on tree changes: a later press picks again. |
| **Pull through it** | The refresh view sets `SkUiScrollController.PullsForRefreshView` (kept apart from the owner's `PullsAtVerticalStart`, which a collection view toggles itself) and follows `PullReleased` / `OverscrollChanged`, as the collection view does. A drag that scrolls the scroller to its top pulls on in the same drag (the scroll gesture's remainder becomes the pull). |
| **Pull of the view itself** | Without a scroller under the press, the view's own exclusive recognizer joins the arena; it claims a downward drag past the slop while the content's first vertical scroller (breadth first, through render children of both layers) is at its top, and resigns on upward or sideways drags. Arena order (innermost first) lets scrollers and inner drags decide before it on every move. The pull is rubber-banded over the view's height; released early, the indicator goes back on the UI clock (`SkUiTween`). |
| **Lifetime** | The link to the pulled scroller holds both ends weakly and is subscribed to the controller's events: a scroller that left the content (found outside it on its next pull or overscroll report, or replaced by the next press) keeps the view alive in no direction (leak scenario `RefreshPulled`). The link ends when the view's subtree is detached, its content changes, or the pull is turned off. |
| **Rules** | MAUI's: `IsRefreshing` is coerced to `false` while disabled, the pull is off or the command cannot execute; disabling the view or turning `IsRefreshEnabled` off ends a refresh; a command that cannot execute does not. |

| Concern | Behavior |
| --- | --- |
| **Indicator** | Drawn by the look (`DrawRefreshIndicator`; default: the activity indicator, on a badge as an overlay), recorded once per state. Pulls change only composite-time properties: the layer sets the indicator's `TranslationY` and `PullProgress`, which the look's `GetRefreshPullFeedback` turns into opacity and rotation in the node's render properties; while refreshing, `ContentSpinPeriod` spins the picture on the render thread. A look that draws the pull itself (`RefreshIndicatorDrawsPullProgress`) is recorded again on progress changes. |
| **Overlay** | The badge comes down with the gap shown past the top (up to 1.5 × the trigger) and rests with its bottom at `RefreshRestDistance` while refreshing. |
| **Inline** | The indicator slides down with the gap, from the pulled scroller's top in the layer (the refresh view measures it through root bounds), centered in the first rest-distance DIPs. While refreshing, a scroller holds its content down by the rest distance: `SkUiScrollController.TopRest` makes spring-backs at the top settle there (`SkUiRenderOverscrollSettle` takes a resting target), a drag takes the room back first, and clearing it springs the content up. The refresh view's own pull (content without a scroller) moves the content part (composite-time `TranslationY`) and holds it on the UI clock. A scroller shows the hold only with bounce overscroll. |
| **Mouse** | The scroll gesture records the pressing device (`DragDevice`); the pull flags are `SkUiPullInput` values (`Touch`, `TouchAndMouse`), so a mouse drag pulls a scroller's start only when the owner allows it, and the helper ignores mouse overscroll (a bounce) for the indicator and the trigger. The refresh view's own recognizer declines mouse presses unless allowed. |
| **Completion** | With `Automatic`, a run counts its pending parts: the start itself, a command that cannot execute right after it ran (watched weakly until it can), and each deferral. The last completion (marshaled to the UI thread's dispatcher, captured when the run starts) sets `IsRefreshing` to `false`; ending a refresh otherwise cancels the run, so late completions change nothing. |

## Overscroll events (implemented)

`SkUiScrollView` and `SkUiCoreScrollView` expose what the shared controller already tracks, for effects and gestures at the edges (C2; the scroll view has no pull-to-refresh of its own: `SkUiRefreshView` around it does that):

- **`PullEdges`** generalizes the pull at the vertical start to every edge: `SkUiScrollController.CanOverscroll` lets a drag pull past an edge in `PullEdges` (or the top, for `PullsAtVerticalStart` and `PullsForRefreshView`) when the offset is at that edge, on the scrolled axes only, whatever the overscroll mode and the content's size. The pull is tracked and rubber-banded as any overscroll; `None` draws nothing.
- **`Overscrolled`** is raised from `OverscrollChanged` (drags, render-thread bounce and spring-back reports), with one `SkUiOverscrolledEventArgs` per controller reused for every report (no allocation per frame).
- **`PullReleased`** comes from the controller's release report, now per edge (`Action<SkUiScrollEdges, double>`); the pull-to-refresh owners keep only the top.
- Effects that follow the events run on the UI thread, a frame behind the render thread during a bounce when the UI thread is busy; render-thread scroll links for them are not public yet.

## Carousel

`SkUiCarouselView` ([SkUiCarouselView.md](../controls/SkUiCarouselView.md)) is a horizontal or vertical `SkUiScrollView` around a panel of its own, not the virtual stack: carousel items have one length (the viewport minus the peek insets, or `ItemExtent`), so their positions are known without measuring, and looping needs slots rather than items (one item can show in several views at once).

| Topic | How |
| --- | --- |
| **Slots** | The panel lays out `count × copies` slots (`copies` = 1 without looping), slot `s` showing item `s mod count`; slot `s` starts at `peekStart + s · stride` (`stride` = item length + spacing). Only the slots overlapping the viewport widened by one stride on either side are realized (more for an effect's `GetVisibleRange`), in item cells recycled per template. Logical offsets run from the start of the items; right to left they mirror the physical offset (`max − offset`), and frames are mirrored by the layout. |
| **Snap points** | The scroller asks content that implements `ISkUiSnapPointSource` for the snap offsets between the lowest and highest of the current offset, the fling's projected end and the drag's origin, widened by a viewport, instead of lining up realized children; the panel lists slot `s` at `s · stride − align` (`align` from `SnapPointsAlignment` inside the insets), clamped without looping. The scroll view's render-thread spring does the rest (`MandatorySingle`: the next point from the drag's origin). |
| **Position** | The item at the snap point, from the offset on every report (render-thread flings report each frame); a programmatic scroll holds the target until it ends or a drag takes over (the scroller's stop before the new scroll is not taken as a rest). Without looping, items whose clamped snap points coincide at the ends keep the current one. |
| **Loop** | The strip has enough copies on either side of the middle one for the drift before moving back (0.75 of a cycle), the longest fling (`FlingMaximumVelocity` × the decay's travel), the viewport and the effect's range. Once the offset is 0.75 of a cycle from the middle copy, the panel renumbers its cells by whole cycles (each keeps its item) and moves the offset by the same cycles with `CorrectOffset`: a running fling, snap or drag goes on from the corrected offset (the shift travels with the frame that brings the moved cells), and the scroller's extent is restored by the relayout it asks for. Positions set take the shorter way round on the ring. |
| **Changes** | Settings, viewport and item changes keep the current place (the fractional slot at the snap point); the scroller takes it after its next arrange: set when nothing moves, corrected (`CorrectOffset`) while a fling or drag runs, so appended items do not stop a fling. Cells whose slot shows another item or template are rebound or swapped; the others stay. |
| **Effects** | Each cell's render properties carry an `SkUiItemEffectLink` (the position `−offset / stride + snap(s) / stride` and the effect), evaluated by the compositor into the cell's placement and opacity every frame, and the panel's `SortsChildrenByDepth` draws the cells back to front by the effect's depth ([RenderingPipeline.md](RenderingPipeline.md#retained-render-tree)). |
| **Input** | `IsSwipeEnabled` is the controller's `IsUserScrollEnabled` (drags and the wheel); the keyboard and screen readers step item by item through the controller's `Stepper` (`ISkUiScrollStepper`). |

## Open items

- The thumb does not shrink during a bounce (iOS shrinks it); a held press on the track pages once (desktop scroll bars repeat).
- Native overlays follow a bounce, not a stretch (Android and Windows snapshot them while scrolling anyway).
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
