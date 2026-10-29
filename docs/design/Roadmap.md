# SkiaUi roadmap

What the next releases bring, and the work after them. What has shipped is in the [changelog](../../CHANGELOG.md) and the [control docs](../controls/README.md); the requirements behind it are in [Requirements.md](Requirements.md). The plan that took SkiaUi to 1.0 (phases P, A–F, L, M, U) is archived in [docs/archive/ImplementationPlan-1.0.md](../archive/ImplementationPlan-1.0.md).

## Releases

| Version | .NET MAUI | Xcode | .NET SDK | Minimum OS | Branch |
| --- | --- | --- | --- | --- | --- |
| **1.0.0** | 10.0.110 | 26 | 10.0.400 | iOS 15, Mac Catalyst 15, Android 5.0 (API 21), Windows 10 1809 | `master`, released 22 October 2026 |
| **1.1.0** | 10.0.120 | 27 | 10.0.402 | iOS 15, **Mac Catalyst 17**, Android 5.0 (API 21), Windows 10 1809 | `master`, released 26 October 2026 |
| **1.2.0** | 10.0.120 | 27 | 10.0.402 | As 1.1.0 | `master`, released 27 October 2026 |
| **1.3.0** | 10.0.120 | 27 | 10.0.402 | As 1.1.0 | `master`, released 28 October 2026 |
| **1.4.0** | 10.0.120 or a later 10.0.x | 27 | 10.0.402 | As 1.1.0 | `devel` (in progress) |

SkiaUi stays on MAUI 10.x. Development happens on `devel`; `master` holds stable releases ([Releasing.md](../Releasing.md#branches-and-versions)).

### 1.0.0: first stable release (shipped)

The controls and API listed in the [changelog](../../CHANGELOG.md#100) and its prerelease sections: MAUI parity of the shipped controls, page-shell layouts and containers, virtual lists and the collection view, list chrome, the carousel, pickers, drawn text input, platform looks, the migration blockers found by porting real apps, and the demo and samples drawing their own UI. Its public API is the one every `1.x` release keeps. The device checks that were open at release stay in the [verification backlog](#verification-backlog).

### 1.1.0: MAUI 10.0.120 and Xcode 27 (shipped)

From [#49](https://github.com/rafalka/MauiSkiaUi/pull/49):

- MAUI 10.0.120 and .NET SDK 10.0.402, whose iOS and Mac Catalyst workloads (27.x) build with Xcode 27.1 (workload set 10.0.402.1). CI's macOS jobs move to the `xcode-27` image, the only one with Xcode 27.1 (it needs macOS 26.6 or newer); the Apple build also runs with Xcode 26.6 (workload set 10.0.401) on `macos-26`.
- **UIKit scene lifecycle:** iOS 27 closes apps built with Xcode 27 that do not use it. The demo, samples, benchmark and device-test apps get a `SceneDelegate` and a scene manifest. The library needs no change. Apps that use SkiaUi need the same change for their own Xcode 27 builds, so the migration guide and the README say so with this release.
- **Mac Catalyst minimum 15.0 → 17.0:** the 27.x Mac Catalyst workload refuses lower minimums (library, toolkit package and apps). iOS stays at 15.0.
- **Fix:** in MAUI 10.0.120, showing or hiding a view invalidates the measure of every descendant whose visibility changed; drawn views ignore that invalidation, so showing a view again does not re-record everything it holds.

Work merged into `devel` after 1.0.0 shipped with 1.1.0 too.

### 1.2.0: MAUI parity (shipped)

More MAUI pages port by changing the XAML prefix: MAUI's pan, swipe, pinch and pointer recognizers on drawn views; the collection view's and carousel's `ItemsLayout`, `ScrollTo`, snap points, `ItemsUpdatingScrollMode` and text headers; `IndicatorTemplate`; MAUI 10's new members (`SafeAreaEdges` on the surface root, `Switch.OffColor`, `CheckBox.Command`); and a test that compares the drawn controls' API with MAUI's. `TemplateBinding`, data headers and platform-specifics become conversions in the migration checker. Collection view reordering follows in 1.3. Steps, order and decisions: [the archived plan](../archive/ImplementationPlan-1.2.md); what it left open is below ([1.3.0](#130-reordering-and-drawn-form-controls-next-on-devel), [backlog](#backlog-1x), [verification backlog](#verification-backlog), [held for 2.0](#held-for-20), [not planned](#not-planned)).

### 1.3.0: reordering and drawn form controls (shipped)

Collection view reordering, moved from 1.2 (a new feature rather than a port by prefix, and the only L-sized step), and what building drawn form controls on 1.x asked for ([milestone](https://github.com/rafalka/MauiSkiaUi/milestone/1)):

- **Reordering (on `devel`):** MAUI's `CanReorderItems`, `CanMixGroups` and `ReorderCompleted` on the collection view, in lists and grids of both orientations, grouped or not ([design](ScrollingAndCollectionViews.md#reordering)).
  - Lifting: a long press (touch, pen) or a press and drag (mouse). A copy of the item follows the pointer at composite time, scaled and shadowed by the look; the items it passes move aside with render-thread animations; the list auto-scrolls near its ends.
  - Dropping: the copy moves to its place, then the item moves in its `IList` as MAUI's handlers move it (`RemoveAt`, `Insert`); between groups only with `CanMixGroups`. The item stays selected and the list keeps its offset.
  - Accessibility: "Move up" / "Move down" custom actions on TalkBack and VoiceOver (none in UI Automation on Windows).
  - Leak scenario `CollectionViewReordered`; device runs in the [verification backlog](#verification-backlog).
- **Fixed on `devel`:** centered single lines in fonts with a line gap ([#61](https://github.com/rafalka/MauiSkiaUi/issues/61)), the dim press on light and transparent buttons ([#62](https://github.com/rafalka/MauiSkiaUi/issues/62)), and `RunsTappedCommand`, so a host that hands its `TappedCommand` to the node it draws is one element and focus stop ([#63](https://github.com/rafalka/MauiSkiaUi/issues/63)).
- **Added:** `PressFeedback = Fade` for `ShowsPressEffect`, a composited fade to the look's `PressedContentOpacity` for dark content the tint hardly changes ([#64](https://github.com/rafalka/MauiSkiaUi/issues/64)); `DefaultMinimumHeight`, a protected virtual on both button layers used while no minimum height is set ([#65](https://github.com/rafalka/MauiSkiaUi/issues/65)); and `SkUiDisplayBinding`, the public item-text reader the pickers now share, which lets go of the last item ([#66](https://github.com/rafalka/MauiSkiaUi/issues/66)); `SkUiDragger` / `SkUiCoreDragger`, a handle that reports drags in its parent's coordinates for an app's own resizing ([doc](../controls/SkUiDragger.md)), and the grid splitters built on it, which resize grid rows or columns in drawn grids, MAUI's `Grid` and Core tables (column grips in the header) ([doc](../controls/SkUiGridSplitter.md)); the demo's component pages resize their preview area with one.

### 1.4.0: images and look details (next, on `devel`)

Steps, order and open decisions: [ImplementationPlan-1.4.md](ImplementationPlan-1.4.md).

- **Images:** a cache key per view (done: `CacheKey`, `SkUiImageCache.RemoveByKeyAsync`), downsampling to the view's size, download progress, fade-in, Android vector (XML) drawables.
- **Look details:** progress bar heights, popup chrome (corner radii, shadows), Android text 1–2 % wider than a `TextView`, the radio label about 2 DIPs wider than MAUI's on Apple, an opt-in Fluent pips pager.

## Backlog (1.x)

Taken up when a real app needs it, roughly in this order within each area. Items name the control or mechanism; their designs are in the linked docs.

### New controls and packages

- **Calendar** (`SkUiCalendar` + `SkUiCoreCalendar`, [FR-33](Requirements.md#functional-requirements)): one drawn node per month, render-thread swiping on the carousel's engine, year / decade / century views, single, multiple and range selection, per-day styles; then a calendar popup for the date pickers and a date range picker. Steps F1–F7 in the [archived plan](../archive/ImplementationPlan-1.0.md#phase-f--calendar-skuicalendar).
- **Runtime SVG images** in an optional `SkiaUi.Maui.Svg` package on Svg.Skia: SVG sources for both layers, drawn as vectors at any size, recoloring, trimming checks ([archived plan, phase E](../archive/ImplementationPlan-1.0.md#phase-e--svg-images-later)).
- **Community Toolkit layouts** in `SkiaUi.Maui.CommunityToolkit`: drawn `UniformItemsLayout` and `DockLayout` on the toolkit's layout managers; needs `SkUiLayout.AffectsChildLayout` and `OnChildrenChanged` made public ([Popups.md](Popups.md#integration-packages)).
- **Tile / wrap-grid layout** for galleries of equal tiles.
- `SkUiTwoStateView` (a second content set chosen by a `bool?`); Core twins of the state container and the expander (the expander needs `ScaleY` / `AnchorY` on Core nodes).

### MAUI parity

- **CollectionView:** a selected-item template, extended selection, keyboard item navigation, animated insert and remove.
- **CarouselView / IndicatorView:** `VisibleViews`; autoplay, several items per snap point, a Core carousel.
- **Gesture recognizers on drawn views:** `PanGestureRecognizer` with `TouchPoints` other than 1 (reported and not run since 1.2).
- **Labels:** span press feedback, HTML images, `sub` / `sup`, block indents; one fitted size for a group of labels.
- **Templates:** button image tint.
- **Drawn meanings of platform-specifics,** as SkiaUi members on both layers: a tap on a slider's track that moves the thumb, swipe view transition modes (reveal / drag), the press ripple's color, search bar chrome (minimal / prominent); pulling from other edges is under [Lists and scrolling](#lists-and-scrolling).

### Text input

- Password reveal (show / hide while keeping the password keyboard and autofill).
- Autofill groups smaller than a surface; keyboard inline suggestions and the autofilled highlight on Android.
- A searchable picker list (an editable field that filters the list) and a token template.
- A split caret at bidi boundaries, visual Left / Right through mixed-direction lines, allocation-free re-shaping per keystroke.
- Shrink to fit on the drawn entry (the migration checker reports iOS `Entry.AdjustsFontSizeToFitWidth` with advice until then).
- Number entries: repeat acceleration. More ideas: [TextInput.md](TextInput.md#possible-future-extensions).

### Lists and scrolling

- Virtual stack: a public horizontal layout, `VirtualScrollMode` (`InfiniteFeed`, `Loop`), `PrefetchDistance`, `QueryItemSize`, a load-more hook with a placeholder, a Core twin.
- Scroll bars: the thumb shrinking during a bounce (iOS), repeated paging on a held track press; native overlays following a stretch.
- Pull to refresh from other edges; render-thread effects linked to overscroll.

### Looks, colors and motion

- Per-tree color schemes (looks already work per tree); reading the device's dynamic color (Android 12+), the Windows and Mac accent colors and the OS dark mode into the schemes.
- Transitions: several ripples at once, press scale, opt-in render-thread painters; render-thread animation of shadow properties.

### Accessibility

- Links inside text as elements, custom actions on swipe view items and refresh (reorderable items have theirs), public custom actions for own controls, live regions, arrow keys within radio groups, a platform expanded / collapsed state for the expander.
- iPad keyboard navigation, Windows' non-linear text scale, font images following a later text size change.

### Architecture and performance

- A separate Core assembly without `Microsoft.Maui.Controls`.
- Child alignment in every Core container; scheme defaults resolved at paint time instead of snapshotted when a control is created.
- Measure invalidation without re-recording ancestors for Core nodes (done for SkUi\* views).
- Relayout boundaries; raster cache of stable subtrees with a per-frame budget.
- Recording Core-only subtrees off the UI thread; hit-testing against render-thread transforms while animations run.
- Overlay masks, so drawn popups can cover hosted native controls; cheaper overlay bookkeeping on Android.
- Core buttons' first frame on the Galaxy S9 is about 25 % slower since gradient backgrounds landed (`core-buttons` `firstFrame` 110 → 140 ms, Release).

## Verification backlog

Device runs that are still open. How to run them: [Testing.md](Testing.md#device-acceptance-checklists-still-open).

- **Text input:** dictation, Google / iOS / third-party password managers on devices; drawn popups over a focused field. (CJK and Korean composition, Android autofill on devices, Windows runs, VoiceOver text navigation on a device and the Windows touch keyboard: verified, reported 29 October 2026.)
- **Accessibility:** TalkBack, VoiceOver (iOS) and Narrator on devices; keyboards on Android and Windows; an Appium run with `ExposeAutomationIds`.
- **Hosted controls:** the manual checklist (soft keyboard, IME, WebView nesting) on devices and Windows.
- **Controls:** feel and frame time on devices for the collection view (fling at 10k items, memory), virtual stack, swipe view, refresh view, carousel, expander, avatar view, shimmer (a scrolling list of shimmering rows), scroll bars and overscroll; the pickers' presenters on Windows, in landscape, on rotation and window resize, with screen readers; tooltips on Windows and iPad.
- **Demo gallery:** a full pass on Android and an Apple device.
- **MAUI gesture recognizers on drawn views (1.2):** swipe, pan, pinch, pointer, secondary-button and triple taps on Android, iOS, Mac Catalyst and Windows, against the native view on the gallery's "MAUI gesture recognizers" page.
- **Snap points on lists (1.2):** flings and drags ending on rows on a phone (Android and iOS), with the gallery's "Items layouts and snap points" page.
- **Reordering collection view items (1.3):** a long press and drag on Android and iOS, a mouse drag on Mac Catalyst and Windows, auto-scroll near the ends, grids and grouped lists, the lift's look; the "Move up" / "Move down" actions with TalkBack and VoiceOver; on the gallery's reordering page.
- **`SafeAreaEdges` on the surface root (1.2):** a drawn layout root on a notched iPhone (portrait and landscape, with the keyboard for `SoftInput`) and on an Android phone with edge-to-edge.

## Held for 2.0

Breaking changes for MAUI parity, kept out of 1.x so the 1.0 API stays as it is. Until then, [Migration.md](../Migration.md#lists-collectionview) has the conversions.

- `SkUiCollectionView.Scrolled` raises MAUI's `ItemsViewScrolledEventArgs` (deltas, first / center / last visible index) instead of `ScrolledEventArgs`, as the carousel already does.
- `SkUiCollectionView.SelectionChanged` raises MAUI's `SelectionChangedEventArgs` (its constructors are internal: raised through an accessor) instead of `SkUiSelectionChangedEventArgs`; `PreviousItem` / `CurrentItem` move elsewhere or go.
- `SkUiCollectionView.SelectionMode` takes MAUI's `SelectionMode`; `SingleDeselect` moves elsewhere (for example a separate property).

## Not planned

By design: drawn WebView, media and maps (host them in `SkUiMauiContentView`); Shell, navigation, pages and popup windows of SkiaUi's own (drawn controls open popups through the app's popup system); controls MAUI made obsolete (Frame, ListView, TableView, cells, compatibility layouts); context flyouts and drag and drop; `ImageBrush` (internal in MAUI 10, so no MAUI page can use one: an `SkUiImage` behind the content does it); clones of other vendors' controls; `{TemplateBinding}` and `RelativeSource TemplatedParent` in drawn templates (MAUI resolves them through an internal interface; the migration checker rewrites them to `RelativeSource AncestorType`); reading platform-specific attached properties at runtime (the checker converts the ones with a SkiaUi member, and their other drawn meanings are SkiaUi members in the backlog); `SafeAreaEdges` on drawn views inside a surface (only the surface root applies it, so layout does not depend on where a view is in the window); a Core flex layout (Core uses the wrap and shrink layouts and the grid).
