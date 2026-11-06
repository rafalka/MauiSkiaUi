# SkiaUi roadmap

What the next releases bring, and the work after them. What has shipped is in the [changelog](../../CHANGELOG.md) and the [control docs](../controls/README.md); the requirements behind it are in [Requirements.md](Requirements.md). The plan that took SkiaUi to 1.0 (phases P, A–F, L, M, U) is archived in [docs/archive/ImplementationPlan-1.0.md](../archive/ImplementationPlan-1.0.md).

## Releases

| Version | .NET MAUI | Xcode | .NET SDK | Minimum OS | Branch |
| --- | --- | --- | --- | --- | --- |
| **1.0.0** | 10.0.110 | 26 | 10.0.400 | iOS 15, Mac Catalyst 15, Android 5.0 (API 21), Windows 10 1809 | `master`, released 22 October 2026 |
| **1.1.0** | 10.0.120 | 27 | 10.0.402 | iOS 15, **Mac Catalyst 17**, Android 5.0 (API 21), Windows 10 1809 | `master`, released 26 October 2026 |
| **1.2.0** | 10.0.120 | 27 | 10.0.402 | As 1.1.0 | `master`, released 27 October 2026 |
| **1.3.0** | 10.0.120 | 27 | 10.0.402 | As 1.1.0 | `master`, released 28 October 2026 |
| **1.4.0** | 10.0.120 | 27 | 10.0.402 | As 1.1.0 | `master`, released 29 October 2026 |
| **1.5.0** | 10.0.120 | 27 | 10.0.402 | As 1.1.0 | `master`, released 3 November 2026 |
| **1.5.1** | 10.0.120 | 27 | 10.0.402 | As 1.1.0 | `master`, released 3 November 2026 |
| **1.6.0** | 10.0.120 | 27 | 10.0.402 | As 1.1.0 | `master`, released 4 November 2026 |
| **1.7.0** | 10.0.120 or a later 10.0.x | 27 | 10.0.402 | As 1.1.0 | Planned |
| **1.8.0** | 10.0.120 or a later 10.0.x | 27 | 10.0.402 | As 1.1.0 | Planned |
| **1.9.0** | 10.0.120 or a later 10.0.x | 27 | 10.0.402 | As 1.1.0 | Planned |

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

More MAUI pages port by changing the XAML prefix: MAUI's pan, swipe, pinch and pointer recognizers on drawn views; the collection view's and carousel's `ItemsLayout`, `ScrollTo`, snap points, `ItemsUpdatingScrollMode` and text headers; `IndicatorTemplate`; MAUI 10's new members (`SafeAreaEdges` on the surface root, `Switch.OffColor`, `CheckBox.Command`); and a test that compares the drawn controls' API with MAUI's. `TemplateBinding`, data headers and platform-specifics become conversions in the migration checker. Collection view reordering follows in 1.3. Steps, order and decisions: [the archived plan](../archive/ImplementationPlan-1.2.md); what it left open is below ([1.3.0](#130-reordering-and-drawn-form-controls-shipped), [backlog](#backlog-1x), [verification backlog](#verification-backlog), [held for 2.0](#held-for-20), [not planned](#not-planned)).

### 1.3.0: reordering and drawn form controls (shipped)

Collection view reordering, moved from 1.2 (a new feature rather than a port by prefix, and the only L-sized step), and what building drawn form controls on 1.x asked for ([milestone](https://github.com/rafalka/MauiSkiaUi/milestone/1)):

- **Reordering (on `devel`):** MAUI's `CanReorderItems`, `CanMixGroups` and `ReorderCompleted` on the collection view, in lists and grids of both orientations, grouped or not ([design](ScrollingAndCollectionViews.md#reordering)).
  - Lifting: a long press (touch, pen) or a press and drag (mouse). A copy of the item follows the pointer at composite time, scaled and shadowed by the look; the items it passes move aside with render-thread animations; the list auto-scrolls near its ends.
  - Dropping: the copy moves to its place, then the item moves in its `IList` as MAUI's handlers move it (`RemoveAt`, `Insert`); between groups only with `CanMixGroups`. The item stays selected and the list keeps its offset.
  - Accessibility: "Move up" / "Move down" custom actions on TalkBack and VoiceOver (none in UI Automation on Windows).
  - Leak scenario `CollectionViewReordered`; device runs in the [verification backlog](#verification-backlog).
- **Fixed on `devel`:** centered single lines in fonts with a line gap ([#61](https://github.com/rafalka/MauiSkiaUi/issues/61)), the dim press on light and transparent buttons ([#62](https://github.com/rafalka/MauiSkiaUi/issues/62)), and `RunsTappedCommand`, so a host that hands its `TappedCommand` to the node it draws is one element and focus stop ([#63](https://github.com/rafalka/MauiSkiaUi/issues/63)).
- **Added:** `PressFeedback = Fade` for `ShowsPressEffect`, a composited fade to the look's `PressedContentOpacity` for dark content the tint hardly changes ([#64](https://github.com/rafalka/MauiSkiaUi/issues/64)); `DefaultMinimumHeight`, a protected virtual on both button layers used while no minimum height is set ([#65](https://github.com/rafalka/MauiSkiaUi/issues/65)); and `SkUiDisplayBinding`, the public item-text reader the pickers now share, which lets go of the last item ([#66](https://github.com/rafalka/MauiSkiaUi/issues/66)); `SkUiDragger` / `SkUiCoreDragger`, a handle that reports drags in its parent's coordinates for an app's own resizing ([doc](../controls/SkUiDragger.md)), and the grid splitters built on it, which resize grid rows or columns in drawn grids, MAUI's `Grid` and Core tables (column grips in the header) ([doc](../controls/SkUiGridSplitter.md)); the demo's component pages resize their preview area with one.

### 1.4.0: images and look details (shipped)

Steps, order and decisions: [the archived plan](../archive/ImplementationPlan-1.4.md).

- **Images:** a cache key per view (done: `CacheKey`, `SkUiImageCache.RemoveByKeyAsync`), downsampling to the view's size (done: `DownsampleToViewSize`), download progress (done: `LoadingProgress`, `DownloadProgress`), fade-in (done: `FadeAnimationEnabled`), Android vector (XML) drawables (done).
- **Look details:** progress bar heights (done: they matched; Material 2's square ends fixed), popup chrome (done: look members, Android's corners measured), Android text 1–2 % wider than a `TextView` (done: plain text kerns; Apple's optical size), the radio label about 2 DIPs wider than MAUI's on Apple (done: padding 7, not 8), an opt-in Fluent pips pager (done: `SkUiIndicatorStyle.Pips`, `NavigationButtonVisibility`).

### 1.5.0 and 1.5.1: pickers (shipped)

Searchable picker lists (`IsSearchEnabled`), the pickers' chevron (`IsChevronVisible`, color, size and room), field padding (`TextPadding`) and a leading view in picker fields, on both layers; resized surfaces no longer stretch their content, and picker popups size every templated row ([#73](https://github.com/rafalka/MauiSkiaUi/issues/73)–[#76](https://github.com/rafalka/MauiSkiaUi/issues/76)). 1.5.1: Android accessibility with `Xamarin.AndroidX.Core` 1.19 ([#78](https://github.com/rafalka/MauiSkiaUi/issues/78)), and bugfix releases from `devel`.

### 1.6.0: viewport insets (shipped)

Steps and decisions: [ImplementationPlan-1.6.md](ImplementationPlan-1.6.md). The rest of what was planned for 1.6 moved to [1.9.0](#190-performance-tiles-core-containers-and-password-reveal-planned).

- **Viewport insets (device checks in the [verification backlog](#verification-backlog)):** `ViewportInsets` on the scroll views and the collection view (the virtual stack follows its scroller's), so edge-to-edge pages scroll content under drawn top and bottom bars while scroll bars, `ScrollTo`, snap points, the refresh indicator, sticky headers and keyboard reveal work in the uncovered band; the surface root's `SafeAreaInsets` readable for binding.
- **Fix:** hosted native views follow a resize of what clips them ([#81](https://github.com/rafalka/MauiSkiaUi/issues/81), fixed in [#82](https://github.com/rafalka/MauiSkiaUi/pull/82)).

### 1.7.0: vector icons (planned)

Built on the branch `1.7-implementation`, not released: [what it costs](../Performance.md#vector-icons), [how to use it](../controls/SkUiImage.md#vector-icons).

One feature: an app's `.svg` icons drawn at runtime on both layers, through a small SVG reader in `SkiaUi.Maui`, with no new dependency or package. A `MauiImage` keeps working as today, and an app can use both. Steps and decisions: [ImplementationPlan-1.7.md](ImplementationPlan-1.7.md).

- **Reader:** the part of SVG that icons use: shapes and paths, fill and stroke, dashes, transforms, clips, masks, class rules and one-line text. What it knows it cannot draw is a `LoadError` naming it, and a file cannot make requests, take the app down or hold a thread for long.
- **Drawing:** a bitmap at the view's size in exact pixels, so an icon stays sharp at any size and draws as cheaply as an image.
- **Recoloring:** `SkUiVectorImageSource`, shaped like `FontImageSource`, whose `Color` is the starting value of `currentColor`.
- **Loading:** in the image decoder, so every source gets it, along with the caches, progress and load events. Files ship as `MauiAsset`.

### 1.8.0: Markdown (planned)

Built on the branch `1.8-implementation`, not released: [how to use it](../controls/SkUiMarkdownView.md), [what it costs](../Performance.md#markdown).

One feature: a drawn Markdown view on both layers, in new optional packages, with no new dependency in `SkiaUi.Maui`. Steps, order, a spike's findings and open decisions: [ImplementationPlan-1.8.md](ImplementationPlan-1.8.md).

- **View:** `SkUiMarkdownView` + `SkUiCoreMarkdown` in `SkiaUi.Maui.Markdown` on Markdig: text, quotes, alerts, code, lists, tables, images and links, styled by one style object over the color scheme; a growing text only rebuilds its tail.
- **Untrusted documents:** nothing opens by itself, remote images stay off until an app allows them, a document cannot read a local file, and hostile input cannot take the app down.
- **Math:** block and inline LaTeX in `SkiaUi.Maui.Markdown.Math` on CSharpMath.
- **In `SkiaUi.Maui`:** inline objects in text, a baseline shift on spans (real superscript and subscript) and a zoom view.

### 1.9.0: performance, tiles, Core containers and password reveal (planned)

Moved from 1.6, then from 1.8. Steps, order and open decisions: [ImplementationPlan-1.9.md](ImplementationPlan-1.9.md).

- **Performance:** the device results measured again on the current toolchain; Core measure invalidation without re-recording ancestors; shaped borders sharing their outlines and clips.
- **Layouts and containers:** a tile layout for galleries of equal tiles (with an overflow tile); Core twins of the state container and the expander (with independent `ScaleX` / `ScaleY` and `AnchorX` / `AnchorY` on Core nodes; animating one scale axis of a Core node no longer scales both).
- **Text input:** password reveal (show / hide) in drawn password fields.

.NET 11 / MAUI 11 waits until it is released.

## Backlog (1.x)

Taken up when a real app needs it, roughly in this order within each area. Items name the control or mechanism; their designs are in the linked docs.

### .NET 11 / MAUI 11 support

Multi-target the one `SkiaUi.Maui` package (`net10.0-*` plus `net11.0-*`) so apps that stay on .NET 10 keep restoring the net10 assets and never see MAUI 11; no net10 TFM is dropped. Not started; ships as an additive minor release, as a prerelease until MAUI 11 is GA.

- Per-TFM `MauiVersion` (and SkiaSharp / Community Toolkit versions where the net11 builds need others) in [Directory.Build.props](../../Directory.Build.props); `SKUI_NET11` define for the few MAUI 11 API differences, behind small shims rather than `#if`s in controls; per-TFM `SupportedOSPlatformVersion`.
- .NET 11 SDK for the net11 TFMs (`global.json` or a separate CI job); keep trim / AOT warnings at 0 on both TFM sets.
- CI: build matrix over both TFM sets, headless tests on `net10.0` and `net11.0`, a public API comparison between them, and a net10 consumer app restoring the packed nupkg.
- Update CLAUDE.md, Development.md, Migration.md and the "stays on MAUI 10.x" note above when it starts.

### New controls and packages

- **Calendar** (`SkUiCalendar` + `SkUiCoreCalendar`, [FR-33](Requirements.md#functional-requirements)): one drawn node per month, render-thread swiping on the carousel's engine, year / decade / century views, single, multiple and range selection, per-day styles; then a calendar popup for the date pickers and a date range picker. Steps F1–F7 in the [archived plan](../archive/ImplementationPlan-1.0.md#phase-f--calendar-skuicalendar).
- **Full SVG** in an optional `SkiaUi.Maui.Svg` package on Svg.Skia, for what [1.7's icon reader](ImplementationPlan-1.7.md) leaves out: gradients, `<use>`, filters and CSS in downloaded files and rich illustrations. Needs a vector entry in the image cache and protection against hostile files (design and a spike's findings: [the full SVG plan](ImplementationPlan-FullSvg.md)).
- **Community Toolkit layouts** in `SkiaUi.Maui.CommunityToolkit`: drawn `UniformItemsLayout` and `DockLayout` on the toolkit's layout managers; needs `SkUiLayout.AffectsChildLayout` and `OnChildrenChanged` made public ([Popups.md](Popups.md#integration-packages)).

### MAUI parity

- **CollectionView:** a selected-item template, extended selection, keyboard item navigation, animated insert and remove.
- **CarouselView / IndicatorView:** `VisibleViews`; autoplay, several items per snap point, a Core carousel.
- **Gesture recognizers on drawn views:** `PanGestureRecognizer` with `TouchPoints` other than 1 (reported and not run since 1.2).
- **Labels:** span press feedback, HTML images, block indents; one fitted size for a group of labels.
- **Templates:** button image tint.
- **Drawn meanings of platform-specifics,** as SkiaUi members on both layers: a tap on a slider's track that moves the thumb, swipe view transition modes (reveal / drag), the press ripple's color, search bar chrome (minimal / prominent); pulling from other edges is under [Lists and scrolling](#lists-and-scrolling).

### Text input

- Autofill groups smaller than a surface; keyboard inline suggestions and the autofilled highlight on Android.
- A token template for pickers (the searchable list shipped: `IsSearchEnabled`).
- A split caret at bidi boundaries, visual Left / Right through mixed-direction lines, allocation-free re-shaping per keystroke.
- Shrink to fit on the drawn entry (the migration checker reports iOS `Entry.AdjustsFontSizeToFitWidth` with advice until then).
- Number entries: repeat acceleration. More ideas: [TextInput.md](TextInput.md#possible-future-extensions).

### Lists and scrolling

- Virtual stack: a public horizontal layout, `VirtualScrollMode` (`InfiniteFeed`, `Loop`), `PrefetchDistance`, `QueryItemSize`, a load-more hook with a placeholder, a Core twin.
- Scroll bars: the thumb shrinking during a bounce (iOS), repeated paging on a held track press; native overlays following a stretch.
- Pull to refresh from other edges; render-thread effects linked to overscroll.
- Markdown: long documents as virtualized rows, measuring and recording only the top-level blocks near the viewport (the first frame of a 100 KB document takes about 760 ms on the S9, [Performance.md](../Performance.md#markdown)). Not planned for now. Needs the virtual stack's Core twin or starts on `SkUiMarkdownView` only, and an opt-in way to scroll, since the view is as tall as its document today. Design notes: [1.8 plan, V2](ImplementationPlan-1.8.md#phase-v--long-documents).

### Looks, colors and motion

- Per-tree color schemes (looks already work per tree); reading the device's dynamic color (Android 12+), the Windows and Mac accent colors and the OS dark mode into the schemes.
- Transitions: several ripples at once, press scale, opt-in render-thread painters; render-thread animation of shadow properties.

### Accessibility

- Links inside text as elements, custom actions on swipe view items and refresh (reorderable items have theirs), public custom actions for own controls, live regions, arrow keys within radio groups, a platform expanded / collapsed state for the expander.
- iPad keyboard navigation, Windows' non-linear text scale, font images following a later text size change.

### Architecture and performance

- Scheme defaults resolved at paint time on the Core layer: Core controls copy `SkUiColors.Accent`, `TrackOff`, `Muted` and so on into fields when they are created, so a Core tree made before `SkUiColorScheme.Current` changes (dark mode) keeps the old colors.
- Text shadows on mid-range Android: they are cast from the glyphs and rasterized on the CPU once per item, which holds a scrolling list on the S9 at 45 fps; cast them on the GPU as outline shadows are.
- Relayout boundaries below the surface root (the root is one); raster cache of stable subtrees with a per-frame budget, for what is expensive to composite every frame (dashed strokes of complex paths: 33 fps on the S9).
- Core's first frame is no faster than SkUi\*'s on devices (1,000 buttons: S9 169 vs 167 ms, iPhone 8 35 vs 37 ms) although Core builds its tree three times faster; `scripts/bench.sh` `core-buttons` `firstFrame` on the S9 went from 110 to 140 ms (Release) with gradient backgrounds (MAUI parity P7). Measured again in 1.9 first ([plan, R1](ImplementationPlan-1.9.md#phase-r--performance-backed-by-measurements)).
- Recording Core-only subtrees off the UI thread (the look, the color scheme and the text and image caches would have to be safe to read from it); hit-testing against render-thread transforms while animations run.
- Overlay masks, so drawn popups can cover hosted native controls (mostly WebView, media and maps now that text fields are drawn); cheaper overlay bookkeeping on Android.
- Windows GPU surfaces can still flash the window's backdrop for a frame when their swap chain panel changes size (rare since `SkUiSurfaceSlack`). `SKSwapChainPanel` handles a size change by presenting an empty buffer before the real frame (`pendingSizeChange` in SkiaSharp's `AngleSwapChainPanel`), and that blank can reach the screen. The fix needs a way around that swap: our own ANGLE panel, or a SkiaSharp change.
- A test that keeps the Core layer to [FR-C1](Requirements.md#core-layer-fr-c): no Core node derives from `BindableObject` or `Element`, and the `Microsoft.Maui.Controls` types the Core API uses (`FontAttributes`, `DataTemplate` for picker popups, the host's `SkUiView`) are a list a new one has to join.

## Verification backlog

Device runs that are still open. How to run them: [Testing.md](Testing.md#device-acceptance-checklists-still-open).

- **Text input:** dictation, Google / iOS / third-party password managers on devices; drawn popups over a focused field. (CJK and Korean composition, Android autofill on devices, Windows runs, VoiceOver text navigation on a device and the Windows touch keyboard: verified, reported 29 October 2026.)
- **Accessibility:** TalkBack, VoiceOver (iOS) and Narrator on devices; keyboards on Android and Windows; an Appium run with `ExposeAutomationIds`.
- **Hosted controls:** the manual checklist (soft keyboard, IME, WebView nesting) on devices and Windows.
- **Controls:** feel and frame time on devices for the collection view (fling at 10k items, memory), virtual stack, swipe view, refresh view, carousel, expander, avatar view, shimmer (a scrolling list of shimmering rows), scroll bars and overscroll; the pickers' presenters on Windows (the toolkit's anchored list was checked on 29 October 2026: frame, keyboard, dismissal), in landscape, on rotation and window resize, with screen readers; tooltips on Windows and iPad.
- **Demo gallery:** a full pass on Android and an Apple device.
- **MAUI gesture recognizers on drawn views (1.2):** swipe, pan, pinch, pointer, secondary-button and triple taps on Android, iOS, Mac Catalyst and Windows, against the native view on the gallery's "MAUI gesture recognizers" page.
- **Snap points on lists (1.2):** flings and drags ending on rows on a phone (Android and iOS), with the gallery's "Items layouts and snap points" page.
- **Reordering collection view items (1.3):** a long press and drag on Android and iOS, a mouse drag on Mac Catalyst and Windows, auto-scroll near the ends, grids and grouped lists, the lift's look; the "Move up" / "Move down" actions with TalkBack and VoiceOver; on the gallery's reordering page.
- **Vector icons (1.7):** files built to be costly on the Galaxy S9 and the iPhone 8 (`A_small_file_cannot_hold_a_thread` covers them headless).
- **Viewport insets (1.6):** the samples app's edge-to-edge orders page was checked through rotations on an iPhone 17 simulator, an Android API 37 emulator (with a cutout and 3-button navigation, and a 180-degree flip between the landscape sides with lopsided side insets, which keeps the window's size) and Mac Catalyst (window resize). A native view cut off at a bar was checked on the iPhone simulator. Still open: Windows; real devices; the gallery's "Edge-to-edge scrolling" page on Android, with its native field; the keyboard covering more than the bottom bar on a device.
- **`SafeAreaEdges` on the surface root (1.2):** a drawn layout root on a notched iPhone (portrait and landscape, with the keyboard for `SoftInput`) and on an Android phone with edge-to-edge.

## Held for 2.0

Breaking changes for MAUI parity, kept out of 1.x so the 1.0 API stays as it is. Until then, [Migration.md](../Migration.md#lists-collectionview) has the conversions.

- `SkUiCollectionView.Scrolled` raises MAUI's `ItemsViewScrolledEventArgs` (deltas, first / center / last visible index) instead of `ScrolledEventArgs`, as the carousel already does.
- `SkUiCollectionView.SelectionChanged` raises MAUI's `SelectionChangedEventArgs` (its constructors are internal: raised through an accessor) instead of `SkUiSelectionChangedEventArgs`; `PreviousItem` / `CurrentItem` move elsewhere or go.
- `SkUiCollectionView.SelectionMode` takes MAUI's `SelectionMode`; `SingleDeselect` moves elsewhere (for example a separate property).

## Not planned

By design: drawn WebView, media and maps (host them in `SkUiMauiContentView`); Shell, navigation, pages and popup windows of SkiaUi's own (drawn controls open popups through the app's popup system); controls MAUI made obsolete (Frame, ListView, TableView, cells, compatibility layouts); context flyouts and drag and drop; `ImageBrush` (internal in MAUI 10, so no MAUI page can use one: an `SkUiImage` behind the content does it); clones of other vendors' controls; `{TemplateBinding}` and `RelativeSource TemplatedParent` in drawn templates (MAUI resolves them through an internal interface; the migration checker rewrites them to `RelativeSource AncestorType`); reading platform-specific attached properties at runtime (the checker converts the ones with a SkiaUi member, and their other drawn meanings are SkiaUi members in the backlog); `SafeAreaEdges` on drawn views inside a surface (only the surface root applies it, so layout does not depend on where a view is in the window); a Core flex layout (Core uses the wrap and shrink layouts and the grid); a separate Core assembly without `Microsoft.Maui.Controls` (a Core tree draws only inside an `SkUiView` such as `SkUiCoreHost`, and the engines it shares with the SkUi\* layer, the look, popups, text input and accessibility all run on MAUI Controls, so an app could not drop the dependency; the Core API also takes MAUI's `FontAttributes` and `DataTemplate`, which 1.x keeps).
