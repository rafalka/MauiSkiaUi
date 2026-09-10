# SkiaUi scrolling and collection views

Design notes and implementation checklist for **FR-17** (custom scroll and list/collection controls) in [Requirements.md](Requirements.md). Aligns with [LayoutSystem.md](LayoutSystem.md), [DrawingMechanism.md](DrawingMechanism.md), [EventMechanism.md](EventMechanism.md), [AnimationMechanism.md](AnimationMechanism.md), and FR-16 (`SkUiMauiContentView` overlays / scroll snapshots).

## Goal

Provide **SkiaUi-owned** scrolling and (later) virtualized collections so large or scrollable UIs stay on **one shared Skia surface**, with pan/fling, viewport clipping, and optional item recycling — without relying on MAUI `ScrollView` / `CollectionView` as the primary composition model.

Apps should be able to:

- Author a scrollable form or stack entirely under `SkUiContentView` in XAML.
- Pan and fling with physics that match platform expectations reasonably (thresholds, inertia, optional bounce/overscroll).
- Later: bind large `ItemsSource` lists with recycled cell templates without one MAUI handler / surface per row.

## Decision summary

| Topic | Choice |
| --- | --- |
| Primary scroll | **`SkUiScrollView`** — fully drawn; owns offset, viewport, clip, pan/fling |
| Primary lists (later) | **`SkUiCollectionView`** (or templated virtualizing layout) — recycle pool on shared surface |
| Not primary | Nesting SkiaUi trees inside MAUI `ScrollView` / `CollectionView` |
| Why | One surface (FR-13/14); Skia-owned viewport for cull/paint; FR-15 gestures; FR-16 overlay sync / snapshots |
| MAUI nest | **Compat / migration only** — document cost; leaf `HwAccelerated = false` |
| Layout contract | Still **MAUI measure/arrange** for content and cells ([LayoutSystem.md](LayoutSystem.md)) |
| Gestures | Scroll consumes pan along its axis via **raw touch + capture**; tap/swipe still FR-15 where not scrolling |
| Overlay while scroll | **Android/Windows:** snapshot freeze by default; **Apple:** live sync; **opt-out** per overlay |
| Closest analogues | **DrawnUi** `SkiaScroll` + templated `SkiaLayout`; Flutter `Scrollable`/`Viewport`/slivers; Avalonia `ScrollViewer` + `VirtualizingStackPanel`; Uno `ItemsRepeater` |

## How peer platforms implement this

| Platform | Scroll | Collections | Fully drawn? |
| --- | --- | --- | --- |
| **.NET MAUI** | Native (`UIScrollView`, etc.) | Native recyclers + MAUI cell handlers | **No** |
| **Flutter** | `Scrollable` + `Viewport` + slivers | `ListView` / `SliverList` build only visible children | **Yes** |
| **Avalonia** | `ScrollViewer` (Offset / Extent) | `ItemsControl` + `VirtualizingStackPanel` recycle | **Yes** |
| **Uno** | `ScrollViewer` (+ Skia path) | `ItemsRepeater` virtualization | **Mostly yes** on Skia |
| **DrawnUi** | `SkiaScroll` (gestures, viewport, rubber-band) | Templated layout + `RecyclingTemplate` + items windowing | **Yes** |

**Lesson:** toolkits that paint with Skia **own** scroll offset and list virtualization inside the drawn tree. MAUI’s scrollers stay platform-native by design — nesting SkiaUi under them fights the single-surface model.

## Why not MAUI `ScrollView` / `CollectionView` as the main host

1. **Many surfaces / handlers** — a `CollectionView` cell with standalone `SkUiView` creates a handler per cell (even SW). That is what FR-14 defaults try to avoid for “many cells.”
2. **Viewport lives outside Skia** — culling, selective paint, and overlay repositioning need an in-tree offset; native scroll does not give a clean SkiaUi viewport API.
3. **Gesture conflict** — FR-15 owns pan/swipe on the drawn tree; nested native scroll fights SkiaUi capture and physics.
4. **`SkUiMauiContentView`** — overlays need scroll-time sync; on Android/Windows, snapshot freeze while scrolling (FR-16 decided policy).
5. **Wrong cost center** — MAUI still pays layout/handler cost per visible cell; SkiaUi’s goal is to move that work onto one surface.

**Allowed escape hatch:** small lists or hybrid pages may put standalone `SkUi*` controls inside MAUI `CollectionView` / `ScrollView`. Document as slower interop; prefer composing under one `SkUiContentView` + `SkUiScrollView`.

## Recommended composition

```xml
<SkUiContentView>
  <SkUiScrollView Orientation="Vertical">
    <SkUiVerticalStack>
      <SkUiLabel Text="…" />
      <SkUiMauiContentView>
        <Entry Placeholder="…" />
      </SkUiMauiContentView>
      <!-- more content -->
    </SkUiVerticalStack>
  </SkUiScrollView>
</SkUiContentView>
```

Later (virtualized):

```xml
<SkUiContentView>
  <SkUiCollectionView ItemsSource="{Binding Items}">
    <SkUiCollectionView.ItemTemplate>
      <DataTemplate>
        <SkUiGrid>
          <SkUiLabel Text="{Binding Title}" />
        </SkUiGrid>
      </DataTemplate>
    </SkUiCollectionView.ItemTemplate>
  </SkUiCollectionView>
</SkUiContentView>
```

Not recommended as the default:

```xml
<!-- Avoid for large / scroll-heavy SkiaUi UI -->
<ScrollView>
  <SkUiContentView>…</SkUiContentView>
</ScrollView>
```

## Architecture

### Scroll (`SkUiScrollView`)

```
Standalone SkUi root (one SK surface)
        │
        ▼
SkUiScrollView : SkUiLayout (or content host)
        │  Measure: viewport = arranged size; measure Content unconstrained (or max) on scroll axis
        │  Arrange: Content at -Offset; clip to viewport
        │  Touch: capture pan on scroll axis; fling → animation clock (FR-7)
        │  Paint: clip → translate by -Offset → paint Content (and chrome / scrollbars)
        ▼
Content : ISkUiView (stack, grid, …)
```

| Concern | Behavior |
| --- | --- |
| **Viewport** | Arranged bounds of the scroll view |
| **Content extent** | Desired size of `Content` (after measure with loose constraint on the scroll axis) |
| **Offset** | `(ScrollX, ScrollY)` in DIPs; clamped to `[0, max(0, extent − viewport)]` (plus optional overscroll) |
| **Clip** | Paint and hit-test content against viewport (hit-test: transform pointer by `+Offset` then hit content) |
| **Orientation** | Vertical, Horizontal, or Both (Both is harder; v1 may ship Vertical + Horizontal only) |
| **Chrome** | Optional scrollbar(s) as paint layers or child chrome (FR-9), not MAUI `ScrollBar` |

### Collection / virtualization (phase 2)

```
SkUiCollectionView (or SkUiLayout + ItemsSource)
        │  Knows viewport (often nested in / acting as scroll)
        │  Realizes only cells intersecting viewport (+ cache margin)
        │  Recycle pool keyed by template / recycle key
        ▼
Cell instances : ISkUiView (Handler == null; painted on shared surface)
```

Virtualization is **not** MAUI `CollectionView` recycling. Cells are hosted SkiaUi nodes: no per-cell platform view unless a cell embeds `SkUiMauiContentView` (discourage many overlays in lists — FR-16 cost note).

## Phasing

| Phase | Deliverable | Scope |
| --- | --- | --- |
| **v1** | `SkUiScrollView` | Single `Content`; Vertical (then Horizontal); pan + fling; clip; `ScrollX`/`ScrollY` / `ScrollTo`; optional scrollbar paint |
| **v1.x** | Polish | Nested scroll (outer vs inner), snap points, keyboard / focus bring-into-view, wheel (desktop) |
| **v2** | Virtualizing collection | `ItemsSource` + `ItemTemplate`, recycle pool, scroll-to-index, variable-size rows (start with fixed/estimated height) |
| **Later** | Advanced lists | Grouping, grid items layout, sticky headers, horizontal carousels, infinite / windowed source |

Do **not** block v1 demos on full collection virtualization. Non-virtualizing scroll covers forms, settings, and short content.

## `SkUiScrollView` — design details

### Type placement

- Prefer **`SkUiScrollView : SkUiLayout`** or a dedicated subclass of `SkUiContentView`-like single-child host with layout overrides.
- Expose **`Content`** (`ISkUiView`) as `[ContentProperty]` for XAML parity with DrawnUi / MAUI `ScrollView`.
- Default **`HwAccelerated = true`** when used as standalone root (same as other composition hosts). When nested under `SkUiContentView`, no surface of its own (FR-13).

### Measure / arrange

- **Viewport size** = constraints from parent (the scroll view’s own arranged size).
- Measure **Content** with:
  - Scroll axis: effectively unconstrained (or a large max), so content reports full desired extent.
  - Cross axis: typically the viewport cross-axis size (stretch), matching MAUI `ScrollView` / stack-in-scroll expectations — document exact parity.
- Arrange Content at origin offset by **`-ScrollOffset`** (content moves under a fixed viewport).
- Changing offset alone must **not** remeasure Content when size unchanged (FR-3a / NFR-2) — only rearrange or apply paint/hit transform.

### Paint

- Apply **clip** to viewport (FR-11).
- Translate canvas by `-Offset` (or arrange-based positions already include offset — pick one model and keep hit-test consistent).
- v1: full-tree paint under root invalidate ([DrawingMechanism.md](DrawingMechanism.md)); optional later: skip painting children whose bounds miss the viewport (cull).

### Input (FR-15 interaction)

- Scroll is an **intrinsic** pan consumer along its enabled axis (like a button is an intrinsic tap consumer).
- Use **pointer capture** while dragging; integrate with EventMechanism capture open items.
- Threshold: small movement → allow tap/click on children; past threshold → scroll wins and cancels child press.
- Fling: velocity → decelerate via FR-7 animator registry on the standalone root (`HasRenderLoop` while animating).
- Nested scroll: define which ancestor claims the gesture (direction lock, leftover delta) — open for v1.x.

### Public surface (target names TBD)

- Properties: `Orientation`, `ScrollX` / `ScrollY` (or `Offset`), `ContentSize` / extent (read-only), `HorizontalScrollBarVisibility` / `VerticalScrollBarVisibility`.
- Methods: `ScrollToAsync` / `ScrollTo` (position or element).
- Events: `Scrolled`, `Scrolling` / `ScrollAnimationEnded` as needed.
- Bindable + FR-10 direct setters where applicable.

### Overlays while scrolling (FR-16)

Native overlays (`SkUiMauiContentView`) sit as **sibling platform views** of the Skia surface. Their frames must track the placeholder’s arranged bounds (which move when scroll offset changes).

Two strategies:

| Strategy | Behavior |
| --- | --- |
| **Live sync** | Every scroll frame: set native view position / transform / clip to match placeholder. Native view stays visible and interactive. |
| **Snapshot freeze** | On scroll/animation start (or first transform change): capture a bitmap of the native view, **hide** the native view, **paint the bitmap** on the Skia canvas (translated with content). When motion settles (debounce timer), show the native view again and drop the snapshot. |

#### Decided policy

| Platform | Default while scrolling / fling | Notes |
| --- | --- | --- |
| **Android** | **Snapshot freeze required** | Auto when placeholder is under an actively scrolling `SkUiScrollView` (or equivalent scroll/fling animation) |
| **Windows** | **Snapshot freeze required** | Same as Android |
| **iOS / Mac Catalyst** | **Live sync only** (snapshot off) | Reposition native overlay each frame; no snapshot path required in v1 |
| **All** | **Opt-out** | Per-overlay property (name TBD, e.g. `UseSnapshotWhileScrolling` / DrawnUi-like `AnimateSnapshot`) so apps can force live native view during scroll (e.g. WebView that must stay interactive / updating) |

**Rationale:** DrawnUi’s production lesson — Android/Windows cannot cheaply track 60 fps Skia motion with live native overlays; Apple usually can. Opt-out covers special cases without making every consumer configure Android/Windows for acceptable demos.

Implementation notes:

- Prefer direct pixel buffers for capture (avoid naive PNG encode/decode round-trips — NFR-2).
- Debounce restore of the native view after motion settles (`FreezeTimeMs`-style).
- While snapshot is showing, native control is not interactive; document hit-test / IME policy (typically: scroll owns the gesture; focus may be deferred until restore).
- FR-17 `SkUiScrollView` must signal overlays when scroll interaction / fling starts and ends (or “transform dirty while animating”) so they can take/clear snapshots.

#### Why Android / Windows need this

DrawnUi’s `SkiaMauiElement` documents this explicitly:

> ANDROID + WINDOWS: To respond to fast skia updates (ex: while scrolling) we are forced to make a native view snapshot, hide the native view and draw the snapshot while we are animating. … OTHER PLATFORMS: Do not need a snapshot, maui view is moved/transformed directly.

| Platform | Typical behavior moving a native overlay every frame | Default |
| --- | --- | --- |
| **iOS / Mac Catalyst** | Repositioning/transforming the overlay (`UIView` frame / transform) usually keeps up with the Skia present rate | Live sync |
| **Android** | Updating a platform `View` layout/visibility every fling frame often **lags** the GL surface → jitter / trail | Snapshot freeze |
| **Windows** | WinUI layout / capture path cannot cheaply track 60 fps Skia motion | Snapshot freeze |

#### Costs and tradeoffs (reference)

| Concern | Snapshot path | Live-sync-only path |
| --- | --- | --- |
| Visual smoothness with Skia content | Good on Android/Windows | Often poor (stutter / desync) |
| Implementation cost | Capture API per platform, bitmap cache, hide/show, debounce | Simpler: only layout native view each frame |
| Memory / CPU | Bitmap alloc + capture at scroll start | Per-frame native layout cost |
| Interactivity while scrolling | Native control **not** interactive (hidden) | IME / focus stay on native view (can fight scroll) |
| Visual fidelity | Stale until scroll ends | Always live |

Velocity-threshold **hybrid** (snapshot only above a speed) is a possible later refinement; not required for v1.

## Collection views — design details (phase 2)

### API shape (MAUI-familiar)

- `ItemsSource` (`IEnumerable` / `IList` + `INotifyCollectionChanged`).
- `ItemTemplate` (`DataTemplate` producing `ISkUiView`).
- Optional: `ItemsLayout` (linear vertical first; grid later), `SelectionMode`, header/footer templates.

### Virtualization model

| Approach | When |
| --- | --- |
| **Viewport realize + recycle** | Default — DrawnUi / Avalonia / Uno ItemsRepeater style |
| **Estimated extent** | Variable-height lists before all rows measured |
| **Windowed source** | Extremely large sources (DrawnUi `ItemsSourceWindow`) — optional later |

Cells remain **hosted** `ISkUiView` nodes (`Handler == null`). Prefer pure Skia cell UI; avoid `SkUiMauiContentView` per row unless measured acceptable.

### Measure strategies

1. **Fixed / first-item height** — simplest; good v2 start (mirrors MAUI `ItemSizingStrategy.MeasureFirstItem` idea).
2. **Per-item measure with cache** — store heights; invalidate on template/data change.
3. **Full measure all items** — only for small sources; not for virtualizing path.

### Scroll integration

- Collection may **embed** scroll (self-scrolling) or be **Content** of `SkUiScrollView`.
- Prefer one owner of offset: either the collection is a scrollable viewport, or it reports extent to an outer `SkUiScrollView` (logical scrollable / extent provider — Avalonia `ILogicalScrollable` idea). Document the chosen pattern before implementing both.

## Interaction with other mechanisms

| Mechanism | Interaction |
| --- | --- |
| **Layout** | Content/cells use same `MeasureOverride` / `ArrangeOverride`; offset changes are arrange/paint, not full-tree remeasure |
| **Drawing** | Viewport clip; optional cull; scrollbars as layers; transparency still live-walk in v1 |
| **Events** | Capture + pan threshold; child taps when not scrolling; overlays exclude FR-15 |
| **Animation** | Fling / `ScrollTo` animations register on root clock; paint-only offset updates |
| **FR-16** | Overlay sync or snapshot during scroll |

## Compat: SkiaUi inside MAUI scrollers

Document explicitly in public docs:

- Supported for **interop / migration**, not recommended for performance-critical lists.
- Standalone cells: `HwAccelerated` defaults **false** (FR-14).
- No shared viewport cull with MAUI scroll offset unless a future bridge syncs it.
- Prefer migrating scrollable regions to `SkUiScrollView` under one `SkUiContentView`.

## Implementation checklist

### `SkUiScrollView` (v1)

- [ ] Type + XAML `Content` / `Orientation` / offset properties; XML docs.
- [ ] Measure: viewport vs content extent; selective cache when constraints unchanged.
- [ ] Arrange: position content from offset; no remeasure on offset-only changes.
- [ ] Paint: clip to viewport; draw content and optional scrollbars.
- [ ] Touch: pan capture, threshold vs child tap, fling via FR-7.
- [ ] Clamp offset; optional overscroll (define v1: clamp-only vs bounce).
- [ ] `ScrollTo` / `Scrolled` API.
- [ ] FR-16: Android/Windows snapshot freeze while scrolling (Apple live sync); opt-out property; scroll start/end signals to overlays.
- [ ] Demo gallery page: long stack under `SkUiScrollView` (with and without hosted Entry).
- [ ] Unit / mechanism tests: measure extent, clamp, offset-only no remeasure, hit-test with offset ([Testing.md](Testing.md)).

### Collection (v2)

- [ ] `ItemsSource` + `ItemTemplate` + recycle pool.
- [ ] Realize / clear cells from viewport (+ cache margin).
- [ ] Fixed or first-item sizing strategy; scroll-to-index.
- [ ] Collection change notifications (`INotifyCollectionChanged`).
- [ ] Demo: large list (1k+ items) at interactive fps.
- [ ] Document: no MAUI `CollectionView` required; discourage overlay-per-cell.

### Docs / Requirements

- [ ] Keep this file as the design source of truth; check off items as implemented.
- [ ] Summarize delivered behavior in [README.md](README.md) when shipped.
- [ ] Cross-link FR entries in [Requirements.md](Requirements.md).

## Open items

- Exact type bases: `SkUiScrollView` as `SkUiLayout` vs specialized content host.
- Property names: `ScrollX`/`ScrollY` vs single `Offset` (`Point` / `Thickness`-like).
- v1 overscroll: clamp-only vs rubber-band bounce.
- Both-axes scroll in v1 or defer.
- Nested scroll negotiation rules.
- Whether collection **is** a scroll view or reports extent to outer `SkUiScrollView`.
- Overscroll (clamp vs bounce), both-axes in v1, nested scroll rules, collection-as-scroll vs outer `SkUiScrollView` extent provider.
- Exact opt-out property name for snapshot-while-scrolling (`UseSnapshotWhileScrolling` vs DrawnUi-like `AnimateSnapshot`).
- Hit-test / IME policy while snapshot is showing (document in FR-16 XML docs).
- Wheel / trackpad / keyboard page-up for desktop TFMs.
- Accessibility / semantics for scrollable regions (platform automation peers) — later.

## References

- [Requirements.md](Requirements.md) — architecture, FR-13/14/15/16, NFR-2.
- [LayoutSystem.md](LayoutSystem.md) — hosted measure/arrange without handlers.
- [DrawingMechanism.md](DrawingMechanism.md) — clip, paint walk, layers.
- [EventMechanism.md](EventMechanism.md) — gestures, capture, participation.
- [AnimationMechanism.md](AnimationMechanism.md) — fling / scroll animation clock.
- Local **DrawnUi**: `SkiaScroll`, `SkiaScroll.Virtual`, `VirtualisationType`, `RecyclingTemplate`, `ItemsSourceWindow`, `ViewsAdapter`.
- Local **Flutter**: `scrollable.dart`, `viewport.dart`, `scroll_view.dart`, slivers.
- Local **Avalonia**: `ScrollViewer`, `VirtualizingStackPanel`, `ILogicalScrollable`.
- Local **Uno**: `ScrollViewer`, `ItemsRepeater` / `ItemsRepeaterScrollHost`.
- Local **MAUI**: `ScrollView` handlers (native); `CollectionView` handlers (platform recyclers) — contrast only, not the SkiaUi primary path.
