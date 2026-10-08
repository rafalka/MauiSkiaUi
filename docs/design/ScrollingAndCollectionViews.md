# SkiaUi scrolling and collection views

Design notes and implementation checklists for **FR-17** (scrolling), **FR-21** (virtual / dynamic scroll layout) and **FR-22** (collection view) in [Requirements.md](Requirements.md). Aligns with [LayoutSystem.md](LayoutSystem.md), [RenderingPipeline.md](RenderingPipeline.md), [EventMechanism.md](EventMechanism.md) (gesture arena), [AnimationMechanism.md](AnimationMechanism.md) and FR-16 (`SkUiMauiContentView` overlays while scrolling).

## Goal

Provide **SkiaUi-owned** scrolling, on-demand item creation and virtualized collections, so large or scrollable UIs stay on **one shared Skia surface**. The toolkit supplies pan and fling, nested scrolling, viewport clipping and item recycling itself, without relying on MAUI `ScrollView` / `CollectionView` as the primary composition model.

Apps should be able to:

- Author a scrollable form or stack entirely under `SkUiContentView` in XAML. This is **done**: `SkUiScrollView`.
- Scroll inside Core-built complex controls. This is **done**: `SkUiCoreScrollView`.
- Nest scrollers, such as horizontal carousels in a vertical feed or a scrollable panel inside a scrollable page. This is **done**: gesture arena plus scroll chaining.
- Build feeds and carousels whose items are created on demand while the user scrolls, before they become visible (FR-21): indexed lists with a virtual extent, infinite feeds (`InfiniteFeed`), and looped carousels.
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
| On-demand items (FR-21) | **`SkUiVirtualVerticalStackLayout`**: a layout that *requests* its children from a provider as its visible window (plus prefetch) grows. **`VirtualScrollMode`** selects indexed virtual extent (default), `InfiniteFeed`, or loop. It sits inside any drawn scroller. `SkUiVirtualScrollView` is the combined convenience control. The vertical, indexed mode is **done** (B1, see [Virtual stack](#virtual-stack-implemented)); `VirtualScrollMode` arrives with the other modes. |
| Collections (FR-22) | **`SkUiCollectionView`**, built on the FR-21 **indexed** engine with template recycling. **SkUi-first** API (not MAUI parity); MAUI `CollectionView` mapping lives in [Migration.md](../Migration.md) and the [migration skills](../../plugins/skiaui-migration/README.md). The MVP is **done** (B2, see [Collection view](#collection-view-implemented)); grouping, grid, horizontal and multiple selection are B3. |
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
- **Verified on Windows 11** (mouse, GPU and software surfaces; [WindowsValidation-results.md](WindowsValidation-results.md)): snapshots during the drag (WebView included), restore after, clipping and hit-test clipping under the drawn header, focused controls stay live, Live mode. On Windows the UI thread also composites, so a UI stall pauses the fling together with the snapshots.

## Virtual stack (implemented)

FR-21's indexed mode, vertical (Phase B1): `SkUiVirtualVerticalStackLayout` and `SkUiVirtualScrollView` ([control guide](../controls/SkUiVirtualVerticalStackLayout.md)). The engine is the abstract `SkUiVirtualVerticalStackLayoutBase`; subclasses create and bind item views by index, give recycle keys and report item changes (`SkUiVirtualVerticalStackLayout` does it for `ItemsSource` / `ItemTemplate` / `ItemFactory`; FR-22's collection view uses an internal subclass of it that hosts each template view in an item container). It derives from `SkUiView`, not `SkUiLayout`: its children are the realized items only, in index order (`SkiaChildren`, render children), added as logical children without `SkUiLayout`'s per-child measure invalidation.

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

Not yet: horizontal, `InfiniteFeed` / `Loop` (`VirtualScrollMode`), `PrefetchDistance` in DIPs, `QueryItemSize`, an async load-more hook with a placeholder item, the Core twin, raster caching of rows. Device runs of the `virtual-fling` benchmark and the `VirtualListScrolled` leak scenario are pending.

## Collection view (implemented)

FR-22's MVP (Phase B2): `SkUiCollectionView` ([control guide](../controls/SkUiCollectionView.md)). A composite drawn view; nothing in it is a platform view.

```
SkUiCollectionView
 ├─ sticky header host        (IsStickyHeader: laid out above the scroller, never re-recorded by scrolling)
 ├─ SkUiScrollView            (vertical; PullsAtVerticalStart with pull-to-refresh)
 │   └─ body: header host · items (internal SkUiVirtualVerticalStackLayout) · empty view host · footer host
 │              └─ item container (SkUiContentView: taps, selection background) ── template view (Selected state)
 ├─ sticky footer host
 └─ refresh layer             (over the scroller, input-transparent, clipped): indicator moved by composite-time props
```

| Concern | Behavior |
| --- | --- |
| **Items** | The items layout is an internal subclass of `SkUiVirtualVerticalStackLayout`: each template view (or default label) is wrapped in an item container when created (`WrapItemView`), so containers are recycled with their views, per template. Binding sets the container's binding context (the template view inherits it) and its selected state (`OnItemBound`). Indices are not kept in containers (collection changes move them): a tap looks its container up among the realized items. |
| **Selection** | `SelectedItem` is compared with `Equals`. A change walks the realized containers and sets their state; only the two whose state changes repaint (their background) and move their template root's `CommonStates` (`SkUiView.IsSelectedItem`: `Disabled`, then `Selected`, then `PointerOver`, then `Normal`). Source changes (`SourceChanged` from the items layout, after they are applied) clear the selection when its item was removed or the source replaced without it. |
| **Taps** | The container takes single taps while a selection mode, an `ItemTapped` handler or a command needs them. Child-first hit-testing gives views inside the item that take taps their own (buttons). Order: cancelable `SelectionChanging`, `SelectedItem` (command, `SelectionChanged`), `ItemTapped`, `ItemTappedCommand`. |
| **Header / footer** | One view each, moved between the sticky host (beside the scroller) and the scrolled host in the body when `IsStickyHeader` / `IsStickyFooter` change. Templates (`SkUiContentSlot`) run once the list is in a tree, with its binding context. |
| **Empty view** | Shown instead of the items while the items layout has none; the body arranges it over the height the viewport has left between header and footer (the scroller arranges its content at least as tall as the viewport). Its template runs the first time the list is empty. |
| **Pull to refresh** | `SkUiScrollController.PullsAtVerticalStart` lets a drag pull the top past the edge when the content does not overflow or overscroll is off (the pull is tracked in `OverscrollY` and springs back on the render thread; the content moves only as the overscroll mode draws it). The indicator follows `OverscrollChanged` with opacity, rotation and translation only (no re-record while dragging); `PullReleased` past 64 DIPs sets `IsRefreshing`, which (as MAUI's `RefreshView`) raises `Refreshing` and runs the command; the indicator then spins through `ContentSpinPeriod` on the render thread. |

Not yet (B3 and later): grouping and expandable groups, multiple selection, grid and horizontal layouts, load-more modes with a load-more row, reordering, swipe actions, keyboard item navigation.

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

- **Authoritative product API:** this section and [Requirements.md](Requirements.md) (FR-22).
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

**Later (not FR-22 MVP):** reordering (long-press drag, gesture arena); row swipe actions (`SkUiSwipeView` / Phase C); horizontal-only polish; keyboard-focused item navigation; animated insert / remove.

### Product backlog (Syncfusion [SfListView](https://help.syncfusion.com/maui/listview/overview) and peers)

Syncfusion’s control is a useful benchmark for **list UX**, not an API target. SkiaUi keeps **SkUi-first** FR-22; the items below are candidates to add to FR-22 or Phase C when they fit the single-surface model.

| Area | Syncfusion / market pattern | SkUi today | Recommendation |
| --- | --- | --- | --- |
| Virtualization | View reuse, templates, selector | FR-21 + FR-22 recycling | **Ship** (core) |
| Layout | Linear, grid, orientation | Planned linear + grid | **Ship** |
| Variable height | `AutoFitMode`, `QueryItemSize`, `DynamicHeight` | Per-index measure default (FR-21 / FR-22) | **In FR-22:** optional **`QueryItemSize`**, **`RemeasureItem`**; no uniform-height default |
| Grouping | Descriptors, sticky headers | Sticky group headers | **Ship** |
| Expand / collapse | `AllowGroupExpandCollapse`, ExpandAll / CollapseAll | Expandable groups (FR-22) | **Add:** **`ExpandAll` / `CollapseAll`**, optional **`AutoExpandGroups`** for new groups ([grouping](https://help.syncfusion.com/maui/listview/grouping)) |
| Multi-level groups | Nested `GroupDescriptor` | Not specified | **Candidate:** hierarchical groups + indented headers (later) |
| Sort / filter | `SortDescriptors`, `Filter`, `LiveDataUpdateMode`, custom comparers | App / view model | **Light built-in optional:** `Filter` predicate + **`RefreshFilter`**; sort stays in VM unless we add optional **`SortDescriptor`**s (heavier; defer) |
| Filtering UI | `FilteringUITemplate`, popup | — | **Out of control** — app composes chips / search above the list ([filtering](https://help.syncfusion.com/maui/listview/filtering)) |
| Selection | Single, SingleDeselect, Multiple, Extended; SelectAll | FR-22 | **In FR-22:** SingleDeselect, Multiple, SelectionChanging, SelectAll / ClearSelection; **Extended** desktop later ([selection](https://help.syncfusion.com/maui/listview/selection)) |
| Select → scroll | Auto `ScrollTo` when `SelectedItem` changes | — | **Candidate:** `ScrollSelectedIntoView` (optional) |
| Load more | Manual / Auto / AutoOnScroll; top or bottom; template row | FR-22 load-more API | **In FR-22** ([load more](https://help.syncfusion.com/maui/listview/loadmore)) |
| Pull to refresh | Often `SfPullToRefresh` wrapper | Built-in on collection | **Ship**; document horizontal list limitation (Syncfusion: no PTR on horizontal) |
| Swipe actions | Start/end templates, threshold, full swipe delete | Phase C `SkUiSwipeView` | **Ship in Phase C**; optional **`SwipeThreshold` / `SwipeOffset`**, programmatic reset ([swiping](https://help.syncfusion.com/maui/listview/swiping)) |
| Reorder | OnHold / drag indicator, drag template | FR-22 later | **Ship later** with **`DragStartMode`**, optional **`DragItemTemplate`** ([drag and drop](https://help.syncfusion.com/maui/listview/item-drag-and-drop)) |
| Scroll / scroll-to | `ScrollTo` / `ScrollToRowIndex`, animated | FR-17 + FR-22 | **Ship**; grouped + variable height may weaken exact `Center` first time (document like Syncfusion) ([scrolling](https://help.syncfusion.com/maui/listview/scrolling)) |
| Item tap context | `ItemType` (Header, GroupHeader, Record, LoadMore) | Item + index + group | **Add:** **`ItemTappedEventArgs.ItemKind`** (record, header, footer, group header, load-more) |
| Appearance | Item fade on appear, fade on scroll | — | **Look / optional:** `EnableFadeOnScroll`; **`ItemAppearing`** hook for light animation (FR-7 render thread preferred) ([appearance](https://help.syncfusion.com/maui/listview/viewappearance)) |
| RTL | `FlowDirection` | FR-17 RTL scroll bars | **Inherit** MAUI `FlowDirection` on list + items ([RTL](https://help.syncfusion.com/maui/listview/right-to-left)) |
| Accessibility | Screen reader, keyboard nav (partial in Syncfusion table) | FR-10 | **Ship** with collection: roles, group expanded/collapsed, list item position |

**Intentionally not a Syncfusion-style `DataSource`:** grouping, sorting, and filtering logic can stay in the view model (`CollectionView` / LINQ / dynamic data) for v1; built-in filter + load-more modes cover most list screens without a second data layer inside the control.

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
| FR-21 | `VirtualScrollMode`; horizontal; `PrefetchDistance`, `QueryItemSize`, load-more hook; optional Core variant | Next |
| FR-21 | **`InfiniteFeed`:** trim/prepend-append, logical index mapping, relative scroll APIs, velocity scroll bars | Later |
| FR-21 | **`Loop`** carousel: wrap indexing, offset correction, horizontal + snap; demo nested carousel on virtual loop | Later |
| FR-22 | `SkUiCollectionView` MVP: linear layout, templates, single selection, sticky header / footer, item tap, empty view, load-more threshold, pull to refresh, migration doc + skill reference (B2) | **Done** |
| FR-22 | Grouping, **expandable groups**, sticky group headers, grid, horizontal, multiple selection, load-more modes (B3) | Next |
| FR-22 | Reordering, row swipe, other list chrome (see Phase C) | Later |
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

See the requirement sections above; check items off in [Requirements.md](Requirements.md).

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
