# SkiaUi roadmap

What the next releases bring, and the work after them. What has shipped is in the [changelog](../../CHANGELOG.md) and the [control docs](../controls/README.md); the requirements behind it are in [Requirements.md](Requirements.md). The plan that took SkiaUi to 1.0 (phases P, A–F, L, M, U) is archived in [docs/archive/ImplementationPlan-1.0.md](../archive/ImplementationPlan-1.0.md).

## Releases

| Version | .NET MAUI | Xcode | .NET SDK | Minimum OS | Branch |
| --- | --- | --- | --- | --- | --- |
| **1.0.0** | 10.0.110 | 26 | 10.0.400 | iOS 15, Mac Catalyst 15, Android 5.0 (API 21), Windows 10 1809 | `master`, released 22 October 2026 |
| **1.1.0** | 10.0.120 | 27 | 10.0.402 | iOS 15, **Mac Catalyst 17**, Android 5.0 (API 21), Windows 10 1809 | `devel` (in progress) |

SkiaUi stays on MAUI 10.x. Development happens on `devel`; `master` holds stable releases ([Releasing.md](../Releasing.md#branches-and-versions)).

### 1.0.0: first stable release (shipped)

The controls and API listed in the [changelog](../../CHANGELOG.md#100) and its prerelease sections: MAUI parity of the shipped controls, page-shell layouts and containers, virtual lists and the collection view, list chrome, the carousel, pickers, drawn text input, platform looks, the migration blockers found by porting real apps, and the demo and samples drawing their own UI. Its public API is the one every `1.x` release keeps. The device checks that were open at release stay in the [verification backlog](#verification-backlog).

### 1.1.0: MAUI 10.0.120 and Xcode 27 (next, on `devel`)

From [#49](https://github.com/rafalka/MauiSkiaUi/pull/49):

- MAUI 10.0.120 and .NET SDK 10.0.402, whose iOS and Mac Catalyst workloads (27.x) build with Xcode 27. CI's macOS jobs move to `macos-26` (Xcode 27 needs macOS 26.4 or newer).
- **UIKit scene lifecycle:** iOS 27 closes apps built with Xcode 27 that do not use it. The demo, samples, benchmark and device-test apps get a `SceneDelegate` and a scene manifest. The library needs no change. Apps that use SkiaUi need the same change for their own Xcode 27 builds, so the migration guide and the README say so with this release.
- **Mac Catalyst minimum 15.0 → 17.0:** the 27.x Mac Catalyst workload refuses lower minimums (library, toolkit package and apps). iOS stays at 15.0.
- **Fix:** in MAUI 10.0.120, showing or hiding a view invalidates the measure of every descendant whose visibility changed; drawn views ignore that invalidation, so showing a view again does not re-record everything it holds.

Work merged into `devel` after 1.0.0 ships with 1.1.0 too.

## Backlog (1.x)

Taken up when a real app needs it, roughly in this order within each area. Items name the control or mechanism; their designs are in the linked docs.

### New controls and packages

- **Calendar** (`SkUiCalendar` + `SkUiCoreCalendar`, [FR-33](Requirements.md#functional-requirements)): one drawn node per month, render-thread swiping on the carousel's engine, year / decade / century views, single, multiple and range selection, per-day styles; then a calendar popup for the date pickers and a date range picker. Steps F1–F7 in the [archived plan](../archive/ImplementationPlan-1.0.md#phase-f--calendar-skuicalendar).
- **Runtime SVG images** in an optional `SkiaUi.Maui.Svg` package on Svg.Skia: SVG sources for both layers, drawn as vectors at any size, recoloring, trimming checks ([archived plan, phase E](../archive/ImplementationPlan-1.0.md#phase-e--svg-images-later)).
- **Community Toolkit layouts** in `SkiaUi.Maui.CommunityToolkit`: drawn `UniformItemsLayout` and `DockLayout` on the toolkit's layout managers; needs `SkUiLayout.AffectsChildLayout` and `OnChildrenChanged` made public ([Popups.md](Popups.md#integration-packages)).
- **Tile / wrap-grid layout** for galleries of equal tiles.
- `SkUiTwoStateView` (a second content set chosen by a `bool?`); Core twins of the state container and the expander (the expander needs `ScaleY` / `AnchorY` on Core nodes).

### MAUI parity

- **Gesture recognizers:** run MAUI's `SwipeGestureRecognizer`, `PanGestureRecognizer` and `PinchGestureRecognizer` on drawn views through the arena, and `PointerGestureRecognizer` from hover and pointer samples (taps already run).
- **CollectionView:** reordering, snap points on the items layout, a selected-item template, extended selection, keyboard item navigation, animated insert and remove.
- **CarouselView / IndicatorView:** `ItemsUpdatingScrollMode`, `VisibleViews`, `IndicatorTemplate`; autoplay, several items per snap point, a Core carousel.
- **Images:** `ImageBrush`; fade-in, download progress, a per-view cache key, downsampling to the view's size, Android vector drawables.
- **Labels:** span press feedback, HTML images, `sub` / `sup`, block indents; one fitted size for a group of labels.
- **Templates:** a drawn `RadioButton.DefaultTemplate`; button image tint.

### Text input

- Password reveal (show / hide while keeping the password keyboard and autofill).
- Autofill groups smaller than a surface; keyboard inline suggestions and the autofilled highlight on Android.
- A searchable picker list (an editable field that filters the list) and a token template.
- A split caret at bidi boundaries, visual Left / Right through mixed-direction lines, allocation-free re-shaping per keystroke.
- Number entries: repeat acceleration. More ideas: [TextInput.md](TextInput.md#possible-future-extensions).

### Lists and scrolling

- Virtual stack: a public horizontal layout, `VirtualScrollMode` (`InfiniteFeed`, `Loop`), `PrefetchDistance`, `QueryItemSize`, a load-more hook with a placeholder, a Core twin.
- Scroll bars: the thumb shrinking during a bounce (iOS), repeated paging on a held track press; native overlays following a stretch.
- Pull to refresh from other edges; render-thread effects linked to overscroll.

### Looks, colors and motion

- Per-tree color schemes (looks already work per tree); reading the device's dynamic color (Android 12+), the Windows and Mac accent colors and the OS dark mode into the schemes.
- Look details: a Fluent pips pager, progress bar heights, popup chrome (shadows, corner radii); Android text 1–2 % wider than a TextView; the radio label about 2 DIPs wider than MAUI's on Apple.
- Transitions: several ripples at once, press scale, opt-in render-thread painters; render-thread animation of shadow properties.

### Accessibility

- Links inside text as elements, custom actions (swipe view items, refresh), live regions, arrow keys within radio groups, a platform expanded / collapsed state for the expander.
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

- **Text input:** CJK and Korean composition, dictation, Google / iOS / third-party password managers and Android autofill on devices; Windows runs (compiled only); VoiceOver text navigation on a device; the Windows touch keyboard; drawn popups over a focused field.
- **Accessibility:** TalkBack, VoiceOver (iOS) and Narrator on devices; keyboards on Android and Windows; an Appium run with `ExposeAutomationIds`.
- **Hosted controls:** the manual checklist (soft keyboard, IME, WebView nesting) on devices and Windows.
- **Controls:** feel and frame time on devices for the collection view (fling at 10k items, memory), virtual stack, swipe view, refresh view, carousel, expander, avatar view, shimmer (a scrolling list of shimmering rows), scroll bars and overscroll; the pickers' presenters on Windows, in landscape, on rotation and window resize, with screen readers; tooltips on Windows and iPad.
- **Demo gallery:** a full pass on Android and an Apple device.

## Not planned

By design: drawn WebView, media and maps (host them in `SkUiMauiContentView`); Shell, navigation, pages and popup windows of SkiaUi's own (drawn controls open popups through the app's popup system); controls MAUI made obsolete (Frame, ListView, TableView, cells, compatibility layouts); context flyouts and drag and drop; clones of other vendors' controls; a Core flex layout (Core uses the wrap and shrink layouts and the grid).
