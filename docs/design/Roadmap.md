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
| **1.7.0** | 10.0.120 | 27 | 10.0.402 | As 1.1.0 | `master`, released 5 November 2026 |
| **1.8.0** | 10.0.120 | 27 | 10.0.402 | As 1.1.0 | `master`, released 6 November 2026 |
| **1.9.0** | 10.0.120 | 27 | 10.0.402 | As 1.1.0 | `master`, released 10 November 2026 |
| **1.10.0** | 10.0.120 | 27 | 10.0.402 | As 1.1.0 | `master`, released 11 November 2026 |
| **1.10.1** | 10.0.120 | 27 | 10.0.402 | As 1.1.0 | `master`, released 11 November 2026 |
| **1.10.2** | 10.0.120 | 27 | 10.0.402 | As 1.1.0 | `master`, released 11 November 2026 |
| **1.10.3** | 10.0.120 | 27 | 10.0.402 | As 1.1.0 | `master`, released 12 November 2026 |
| **1.11.0** | 10.0.130 (net10) and 11.0.10 (net11) | 27 (net10), 27.2 (net11) | 11.0.100 | As 1.1.0; Android 7.0 (API 24) on net11 | `master`, released 14 November 2026 |
| **1.12.0** | As 1.11.0 | As 1.11.0 | 11.0.100 | As 1.11.0 | `master`, released 16 November 2026 |
| **1.13.0** | As 1.11.0 | As 1.11.0 | 11.0.100 | As 1.11.0 | `master`, released 17 November 2026 |
| **1.13.1** | As 1.11.0 | As 1.11.0 | 11.0.100 | As 1.11.0 | `master`, released 17 November 2026 |
| **1.14.0** | As 1.11.0 | As 1.11.0 | 11.0.100 | As 1.11.0 | `master`, released 18 November 2026 |
| **1.15.0** | As 1.11.0 | As 1.11.0 | 11.0.100 | As 1.11.0 | `master`, released 19 November 2026 |
| **1.15.1** | As 1.11.0 | As 1.11.0 | 11.0.100 | As 1.11.0 | `master`, released 20 November 2026 |
| **1.16.0** | As 1.11.0 | As 1.11.0 | 11.0.100 | As 1.11.0 | `master`, released 24 November 2026 |
| **1.17.0** | As 1.11.0 | As 1.11.0 | 11.0.100 | As 1.11.0 | `master`, released 25 November 2026 |
| **1.18.0** | As 1.11.0 | As 1.11.0 | 11.0.100 | As 1.11.0 | `master`, released 27 November 2026 |

SkiaUi up to 1.10 targets MAUI 10.x only; 1.11 adds net11 targets with MAUI 11 next to them ([1.11.0](#1110-net-11-and-maui-11-shipped)). Development happens on `devel`; `master` holds stable releases ([Releasing.md](../Releasing.md#branches-and-versions)).

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

Steps and decisions: [ImplementationPlan-1.6.md](../archive/ImplementationPlan-1.6.md). The rest of what was planned for 1.6 moved to [1.9.0](#190-performance-tiles-core-containers-and-password-reveal-shipped).

- **Viewport insets (device checks in the [verification backlog](#verification-backlog)):** `ViewportInsets` on the scroll views and the collection view (the virtual stack follows its scroller's), so edge-to-edge pages scroll content under drawn top and bottom bars while scroll bars, `ScrollTo`, snap points, the refresh indicator, sticky headers and keyboard reveal work in the uncovered band; the surface root's `SafeAreaInsets` readable for binding.
- **Fix:** hosted native views follow a resize of what clips them ([#81](https://github.com/rafalka/MauiSkiaUi/issues/81), fixed in [#82](https://github.com/rafalka/MauiSkiaUi/pull/82)).

### 1.7.0: vector icons (shipped)

From [#83](https://github.com/rafalka/MauiSkiaUi/pull/83): [what it costs](../Performance.md#vector-icons), [how to use it](../controls/SkUiImage.md#vector-icons).

One feature: an app's `.svg` icons drawn at runtime on both layers, through a small SVG reader in `SkiaUi.Maui`, with no new dependency or package. A `MauiImage` keeps working as today, and an app can use both. Steps and decisions: [ImplementationPlan-1.7.md](../archive/ImplementationPlan-1.7.md).

- **Reader:** the part of SVG that icons use: shapes and paths, fill and stroke, dashes, transforms, clips, masks, class rules and one-line text. What it knows it cannot draw is a `LoadError` naming it, and a file cannot make requests, take the app down or hold a thread for long.
- **Drawing:** a bitmap at the view's size in exact pixels, so an icon stays sharp at any size and draws as cheaply as an image.
- **Recoloring:** `SkUiVectorImageSource`, shaped like `FontImageSource`, whose `Color` is the starting value of `currentColor`.
- **Loading:** in the image decoder, so every source gets it, along with the caches, progress and load events. Files ship as `MauiAsset`.

### 1.8.0: Markdown (shipped)

From [#84](https://github.com/rafalka/MauiSkiaUi/pull/84): [how to use it](../controls/SkUiMarkdownView.md), [what it costs](../Performance.md#markdown).

One feature: a drawn Markdown view on both layers, in new optional packages, with no new dependency in `SkiaUi.Maui`. Steps, order, a spike's findings and decisions: [ImplementationPlan-1.8.md](../archive/ImplementationPlan-1.8.md).

- **View:** `SkUiMarkdownView` + `SkUiCoreMarkdown` in `SkiaUi.Maui.Markdown` on Markdig: text, quotes, alerts, code, lists, tables, images and links, styled by one style object over the color scheme; a growing text only rebuilds its tail.
- **Untrusted documents:** nothing opens by itself, remote images stay off until an app allows them, a document cannot read a local file, and hostile input cannot take the app down.
- **Math:** block and inline LaTeX in `SkiaUi.Maui.Markdown.Math` on CSharpMath.
- **In `SkiaUi.Maui`:** inline objects in text, a baseline shift on spans (real superscript and subscript) and a zoom view.

### 1.9.0: performance, tiles, Core containers and password reveal (shipped)

Moved from 1.6, then from 1.8. Built on the branch `1.9-implementation`. Steps, order and open decisions: [ImplementationPlan-1.9.md](../archive/ImplementationPlan-1.9.md).

- **Fixes taken in:** reorder from a drag handle (#86, `IsReorderHandle`), a drawn root sized by its content grows with it again (#85), VoiceOver's explore by touch finds buttons inside drawn controls (done).

- **Performance:** the device results measured again on the current toolchain (done: Core's first frame is faster, the stress page's first-frame column was the wrong measure); Core measure invalidation without re-recording ancestors (done); shaped borders sharing their content clips (done).
- **Layouts and containers:** a tile layout for galleries of equal tiles, with an overflow tile (done: [SkUiTileLayout](../controls/SkUiTileLayout.md)); Core twins of the state container and the expander (done: `SkUiCoreStateContainer`, `SkUiCoreExpander`; with independent `ScaleX` / `ScaleY` and `AnchorX` / `AnchorY` on Core nodes, so animating one scale axis of a Core node no longer scales both).
- **Text input:** password reveal (show / hide) in drawn password fields (done: `IsPasswordRevealed`, `ShowsPasswordRevealButton`, `HidesPasswordOnUnfocus`, `PasswordRevealMode` (toggle or hold) on both entries; device checks in the [verification backlog](#verification-backlog)).

.NET 11 / MAUI 11 shipped in [1.11.0](#1110-net-11-and-maui-11-shipped).

### 1.10.0: Community Toolkit layouts and collection view parity (shipped)

Built on the branch `1.10-implementation`. Steps, order and open decisions: [ImplementationPlan-1.10.md](../archive/ImplementationPlan-1.10.md).

- **Community Toolkit layouts** in `SkiaUi.Maui.CommunityToolkit`: drawn `SkUiDockLayout` and `SkUiUniformItemsLayout` on the toolkit's layout managers, with the toolkit's attached `DockPosition`; `SkUiLayout.AffectsChildLayout` and `OnChildrenChanged` made public for them (and for app layouts on other managers) (done: [SkUiDockLayout](../controls/SkUiDockLayout.md), [SkUiUniformItemsLayout](../controls/SkUiUniformItemsLayout.md)).
- **Collection view parity:** keyboard item navigation (the list as one tab stop with a current item), extended (Shift / Ctrl) selection, a selected-item template, and animated inserts, removes and moves (opt-in in 1.x) (done: [Keyboard](../controls/SkUiCollectionView.md#keyboard), [Animated changes](../controls/SkUiCollectionView.md#animated-changes), `SelectedItemTemplate`; device checks in the [verification backlog](#verification-backlog)).

### 1.11.0: .NET 11 and MAUI 11 (shipped)

Every package targets `net11.0-*` with MAUI 11 next to `net10.0-*` with MAUI 10, so apps that stay on .NET 10 keep restoring the net10 assets and never see MAUI 11; no net10 target framework is dropped. Platform support only: the MAUI 11 members new to the API go to [MAUI parity](#maui-parity). Built on the branch `feature/net11-support`, with device checks on both runtimes (CoreCLR replaces Mono on mobile in .NET 11). Steps, decisions and verification: [ImplementationPlan-1.11.md](../archive/ImplementationPlan-1.11.md).

- Per-TFM MAUI (10.0.130 / 11.0.10), Community Toolkit (15.0.1 / 17.0.0) and minimum Android (API 21 / 24); SkiaSharp 4.154.0 on both lines.
- .NET SDK 11.0.100 and Xcode 27.2 build both lines' packages (decided in step 0); SDK 10 builds the net10 iOS / Mac Catalyst app heads and the .NET 10 line alone (`net10/`).
- CI over both lines (the Xcode 26.6 / 27.2 jobs stay as net10-only jobs on SDK 10), headless tests on `net10.0` and `net11.0`, an API comparison between the lines and against the newest 1.x release, and net10 / net11 consumer apps restoring the packed packages.

### 1.12.0: inline picker wheels and pickers of your own (shipped)

Additive only. Steps, decisions and verification: [ImplementationPlan-1.12.md](../archive/ImplementationPlan-1.12.md).

- **Inline wheels** ([#98](https://github.com/rafalka/MauiSkiaUi/issues/98)) (device checks on the [verification backlog](#verification-backlog): [SkUiPickerWheels](../controls/SkUiPickerWheels.md)): `SkUiPickerWheels`, the popup's wheels as a public control placed in a layout and always open. It has `SkUiPickerColumn` columns, commits once the wheel settles, has optional haptics, keeps vertical drags from scrolling a parent, and gives each column screen reader and keyboard support. The multi-column and date / time popups use it, so there is one implementation. SkUi\* only until a Core carousel exists.
- **Pickers of your own** ([CustomPickers.md](../controls/CustomPickers.md)): the picker base classes can be derived from outside the library, with a public popup content class (SkiaUi's popup chrome around the app's drawn body) and a typed request for presenters. Built on them: `SkUiValuePicker<T>` / `SkUiCoreValuePicker<T>`, for value types SkiaUi does not ship (a color, a duration). Samples are built on public API only, with a guide.

### 1.13.0 and 1.13.1: calendar (shipped)

Additive only. The drawn calendar of [FR-33](Requirements.md#functional-requirements), on SkiaUi's own API (the date picker's names where it has them: `MinimumDate`, `MaximumDate`, `BlackoutDates`, `DateEnabled`). Steps, decisions and verification: [ImplementationPlan-1.13.md](../archive/ImplementationPlan-1.13.md); the control: [SkUiCalendar](../controls/SkUiCalendar.md). Its device checks are on the [verification backlog](#verification-backlog). 1.13.1 fixed the semantics tree after `AnimateAsync` ([#103](https://github.com/rafalka/MauiSkiaUi/issues/103)).

- **Wave 1, the calendar:** `SkUiCalendar` and `SkUiCoreCalendar` on one engine and one drawn node. Month, year, decade and century views (the title steps up, a cell steps down, a pinch zooms out or in, `MinimumDisplayMode` / `MaximumDisplayMode` limit them); swiping and previous / next buttons between pages, horizontally or vertically, animated by the look; one to six weeks; day names, week numbers and the neighbor months' days; none, single, multiple and range selection, with `RangeSelectionTarget` for apps that pick which end a tap sets; the date picker's day rules; events under the days (dots, bars, icon glyphs, captions) and a day painter for days drawn the app's way; today, selection, range band and fonts for every part, drawn by the look; keyboard and screen readers.
- **Wave 2:** the calendar as the date pickers' popup (`PopupStyle`: `Wheels`, `Calendar`, or the look's, wheels on the built-in looks; `CalendarStyle`); `SkUiDateRangePicker` and `SkUiCoreDateRangePicker` on the range selection; range rules (`MinimumRangeDays`, `MaximumRangeDays`, `AllowsDisabledDaysInRange`) and several ranges (`MultipleRanges`, `SelectedRanges`); a screen-reader element per day and for the header's title and buttons. `SkUiDateTimePicker` keeps its wheels.

### 1.14.0: performance (shipped)

Moved from 1.13 on 17 November 2026. No new control and no behavior change: the allocations a review of the code found on input events, on frames that re-record and on first frames of large trees (16 November 2026). Steps, decisions and verification: [ImplementationPlan-1.14.md](../archive/ImplementationPlan-1.14.md); the review's findings and briefs: [ImplementationPlan-PerformanceAndControls.md](ImplementationPlan-PerformanceAndControls.md). Runtime async was tried and not kept. Also fixed: surfaces removed from a live layout are collected on iOS and Mac Catalyst. Left on the [verification backlog](#verification-backlog): the .NET 11 device measurements (A7 step 4).

- **Wave 1** (independent steps): `System.Threading.Lock` for the ten `object` gates (compositor, frame renderer, Metal surface, image caches, fonts); the gesture arena without an array per pointer event; the text plain-path check without a dictionary lookup per character; image memory-cache hits without a `Task` or a load context.
- **Wave 2:** pooled render batches and updates; shared scratch paints in looks and painters; `LangVersion` 14 for both lines, `params ReadOnlySpan<T>` overloads of the `params` setters, a runtime-async trial on net11, and [Performance.md](../Performance.md) measured again on .NET 11.
- **Wave 3, from the backlog:** an edited paragraph re-shaped without new arrays per keystroke; text shadows cast on the GPU (44 fps on the Galaxy S9 today; the one step that changes pixels, with its look checks done before release); cheaper overlay bookkeeping on Android, after a measurement; and the device frame-time checks of the collection view, virtual stack, carousel, swipe view, refresh view and shimmer from the [verification backlog](#verification-backlog), run with the .NET 11 measurements.

### 1.15.0 and 1.15.1: the calendar, finished (shipped)

Additive only. Closes what 1.13 left open on the calendar; the review's controls (F1, F5, F6) moved to 1.16, then to 1.17 when scroll-linked effects took 1.16. Built on `1.15-implementation`: A2 to A4, the date and time popup, platform looks' calendars, image markers, event lines and other calendar systems (wave C); the slide's frame times were measured on both phones and keep up, so the render-thread slide is not needed; the device checks by hand stay on the verification backlog. Steps, decisions and verification: [ImplementationPlan-1.15.md](../archive/ImplementationPlan-1.15.md).

- **Wave A, verify and harden:** 1.13's device checks (slide frame times, feel, TalkBack / VoiceOver, keyboards, the pickers' calendar popups), calendar bench scenarios and zero-allocation slide frames, an edge-case sweep, a Core / SkUi parity test.
- **Wave B, the gaps:** render-thread page slides (not needed: the S9 keeps up after a fix A1 found), a calendar popup with time wheels for `SkUiDateTimePicker`, platform looks' calendars, image markers, event lines in a month view (not a scheduler).
- **Wave C:** other calendar systems (Thai, Taiwan, Korean, Japanese, Hijri, Umm al-Qura, Persian, Hebrew) through `System.Globalization.Calendar`; in 1.15.

### 1.16.0: scroll-linked effects (shipped)

Additive only. F2 of the [November 2026 review](ImplementationPlan-PerformanceAndControls.md#f2-scroll-linked-effects), moved ahead of the review's controls (F1, F5, F6 go to 1.17) on 23 November 2026: a view follows a scroller's offset or its scroll direction on the render thread, with no re-recording, on both layers. Takes in the render-thread effects linked to overscroll from [Lists and scrolling](#lists-and-scrolling). Built on `1.16-implementation`: phase A, the public API (B1 to B4), docs and the gallery page; headless benches and snapping to the ends (B5); the device benches ran on 23 November 2026, and the feel by hand is on the [verification backlog](#verification-backlog). Steps, decisions and verification: [ImplementationPlan-1.16.md](../archive/ImplementationPlan-1.16.md).

- **Phase A, the engine:** a list of scroll links per render node in place of the single translation link (the scroll bar thumb moves to it), evaluated by the compositor onto translation, scale, opacity and a clipped height or width (kept at the start, end or center, with rectangle, rounded or capsule corners, the shadow following); scroll-direction reveals whose state lives on the render thread; overscroll (bounce and stretch) as input; the same values on the UI side for hit-testing, the immediate painter and screen readers.
- **Phase B, the API:** `SkUiScrollEffect` (source, orientation, input range, property, output range, easing) on `SkUiView.ScrollEffects` and `SkUiCoreNode.AddScrollEffect`, and `SkUiScrollReveal` (tracked or triggered show / hide by scroll direction, shown near the start, settling at rest, `Show` / `Hide`), with factories for parallax, fade-in, collapse and hide-on-scroll; reduce motion keeps fades and clips and stops parallax and zoom; snapping a collapsing header to its ends if time allows.
- **Docs, demo and bench:** recipes (collapsing header with parallax and pull-to-stretch, a collection view's sticky header and footer by direction, a hiding floating button, an extended button shrinking to its icon, a fading toolbar), a gallery page, `skui-scroll-effects` / `core-scroll-effects` benches, device frame times on the S9 and the iPhone 8.

### 1.17.0: carousel parity, segmented control and tab view (shipped)

Additive only. The carousel item of [MAUI parity](#maui-parity) and F1 of the [November 2026 review](ImplementationPlan-PerformanceAndControls.md#f1-segmented-control-and-tab-view). The review's chips (F5) and range slider and rating (F6), planned here by the 1.16 plan, go back to the backlog after the sheet (24 November 2026). Built on `1.17-implementation`: step 0, phases C and S, the benches and docs; the device checks by hand are on the [verification backlog](#verification-backlog). Steps, decisions and verification: [ImplementationPlan-1.17.md](../archive/ImplementationPlan-1.17.md).

- **Step 0:** SkiaSharp 4.157.0 (was 4.154.0) and HarfBuzzSharp 14.4.0.200, before the features.
- **Phase C, the carousel:** its layout moves to an internal engine with no view types, shared by three users; MAUI's `VisibleViews`; several items per snap point (`ItemsPerSnapPoint`, pages stable on every round of a loop, the indicator counting pages); autoplay (`AutoPlayInterval`, waiting while the user scrolls, hovers or has focus inside, and while the carousel is not shown; `IsAutoPlayPaused`); `SkUiCoreCarouselView` with an item factory and a linked `SkUiCoreIndicatorView`.
- **Phase S, F1:** `SkUiSegmentedControl` and `SkUiCoreSegmentedControl` (text and icon segments, a sliding selection, keys and screen readers, drawn by every look); `SkUiTabView`, a tab strip over panes created when first shown and kept, swiped on the carousel engine, its indicator following the panes on the render thread through 1.16's scroll links; `SkUiCoreTabView` on the same strip and engine.

### 1.18.0: the sheet, chips, tab headers and the time-clock screen set (shipped)

Additive only. Tab headers placed here on 25 November 2026; on 26 November 2026 the [November 2026 review's](ImplementationPlan-PerformanceAndControls.md#decisions) sheet (F3) and chips (F5) and the nine requests filed from one real screen set ([#128](https://github.com/rafalka/MauiSkiaUi/issues/128) to [#136](https://github.com/rafalka/MauiSkiaUi/issues/136)), each reviewed against the architecture. Steps, decisions and verification: [ImplementationPlan-1.18.md](../archive/ImplementationPlan-1.18.md). Every phase shipped, phase T included (the cut line was not needed); released 27 November 2026. The device checks are on the [verification backlog](#verification-backlog).

- **Phase A, small pieces:** OpenType font features on drawn text (`FontFeatures="tnum"` in XAML, `[SkUiFontFeature.TabularFigures]` in code, #132); a press scale on both layers, combinable with the overlay (`PressedScale`, #133); swipe views that close each other (`ExclusiveGroup`), `CloseAsync`, `SkUiCollectionView.CloseSwipeViews` and swipe items as custom actions (#135); hit slop and a minimum touch target that grow hit-testing and semantics, not layout (#134).
- **Phase P, progress:** a determinate progress ring with centred content (#128) and a stacked bar of several parts (#129), both layers.
- **Phase L, live regions:** `SkUiAccessibility.LiveRegion`, `Announce` and `UpdatesFrequently` on Android, Apple and Windows (#131).
- **Phase S, the sheet (F3, #136):** an in-surface sheet with detents, drag, a scrim, a dialog presentation for tablets and confirmations, stacking, `ShowAsync<TResult>` and a popup presenter for the pickers. SkUi\* only.
- **Phase K, chips (F5, #130):** `SkUiChip`, `SkUiChipGroup` and their Core twins, wrapping or in one scrolling row.
- **Phase T, custom tab headers:** a header template for the whole strip or one tab in `SkUiTabView` (1.17's headers are text, an icon and a badge drawn by the look, not views), bound to the tab item with its selected state and the look's text color for it (`IsSelected`, `CurrentTextColor`, the `Selected` visual state); `SkUiCoreTabView` with a header node per tab or a factory. The open questions were decided in T1 ([SkUiTabView.md](../controls/SkUiTabView.md#custom-header-decisions-118-t1)): a header view measures by its content (no look padding) in content widths, the look keeps the strip and the indicator (sized to the view where it follows the label), and the header stays one element of the tab list, read from `Header`.

## Backlog (1.x)

Taken up when a real app needs it, roughly in this order within each area. Items name the control or mechanism; their designs are in the linked docs.

### New controls and packages

- **Calendar:** built in [1.13](#1130-and-1131-calendar-shipped); non-Gregorian calendars, a calendar popup with time wheels for `SkUiDateTimePicker`, platform looks' calendars, image markers and a month view with event lines are [1.15](#1150-and-1151-the-calendar-finished-shipped). A scheduler stays out of scope; so do the Julian and East Asian lunisolar calendars.
- **Full SVG** in an optional `SkiaUi.Maui.Svg` package on Svg.Skia, for what [1.7's icon reader](../archive/ImplementationPlan-1.7.md) leaves out: gradients, `<use>`, filters and CSS in downloaded files and rich illustrations. Needs a vector entry in the image cache and protection against hostile files (design and a spike's findings: [the full SVG plan](ImplementationPlan-FullSvg.md)).
- **From the November 2026 review** (briefs, order and open decisions: [ImplementationPlan-PerformanceAndControls.md](ImplementationPlan-PerformanceAndControls.md#track-f-features)), scroll-linked effects in 1.16, the segmented control and tab view (F1) in [1.17](#1170-carousel-parity-segmented-control-and-tab-view-shipped) and the rest after it, roughly in this order. Each is the common pattern on SkiaUi's own API, not a copy of one vendor's control:
  - **Scroll-linked effects** (F2, [1.16](#1160-scroll-linked-effects-shipped)): a public link from a scroller's offset to translation, opacity, scale and a clipped height on the render thread (collapsing headers, parallax); takes in the overscroll-linked effects under [Lists and scrolling](#lists-and-scrolling).
  - **Sheet** (F3, [1.18](#1180-the-sheet-chips-tab-headers-and-the-time-clock-screen-set-shipped), built): a bottom or side sheet inside a surface, with detents, a scrim and a popup presenter for the pickers. A drawn container, not a window. Later: a Core sheet, overlay masks so it can cover hosted native views, a platform pane title.
  - **Charts** (F4) in an optional `SkiaUi.Maui.Charts` package: sparkline, line, area and bar first; pie, donut, gauges, pan and zoom later.
  - **Chips** (F5, [1.18](#1180-the-sheet-chips-tab-headers-and-the-time-clock-screen-set-shipped), built), which the pickers' token display can then build on (it still draws its own tokens); **range slider and rating** (F6); **autocomplete** (F7).
  - **Lottie** (F8) in an optional `SkiaUi.Maui.Lottie` package on Skottie, drawn by the compositor.
  - **Data grid** (F9): virtualized rows, frozen header and columns, sorting, column resize; waits for the virtual stack's horizontal layout.
  - **Backdrop blur** (F10): only if it holds 50 fps on the Galaxy S9.

### MAUI parity

- **MAUI 11 members** (net11 only; `BoxView.Fill` and `Editor.ReturnCommand` are known gaps in the parity test from 1.11): `LongPressGestureRecognizer` on drawn views, `BoxView.Fill`, `Editor.ReturnCommand` / `ReturnCommandParameter`, the `CancellationToken` overloads of `FadeToAsync` and the other animation extensions, `VisualElement.InvalidateStyle` / `VisualStateManager.InvalidateVisualStates`.
- **Gesture recognizers on drawn views:** `PanGestureRecognizer` with `TouchPoints` other than 1 (reported and not run since 1.2).
- **Labels:** span press feedback, HTML images, block indents; one fitted size for a group of labels.
- **Templates:** button image tint.
- **Drawn meanings of platform-specifics,** as SkiaUi members on both layers: a tap on a slider's track that moves the thumb, swipe view transition modes (reveal / drag), the press ripple's color, search bar chrome (minimal / prominent); pulling from other edges is under [Lists and scrolling](#lists-and-scrolling).

### Text input

- Autofill groups smaller than a surface; keyboard inline suggestions and the autofilled highlight on Android.
- A token template for pickers (the searchable list shipped: `IsSearchEnabled`).
- A split caret at bidi boundaries, visual Left / Right through mixed-direction lines; re-shaping per keystroke without the shaping engine's arrays (1.14 reuses the edit layout's own objects: [TextInput.md](TextInput.md); the runs' glyph arrays are the rest).
- Shrink to fit on the drawn entry (the migration checker reports iOS `Entry.AdjustsFontSizeToFitWidth` with advice until then).
- Number entries: repeat acceleration. More ideas: [TextInput.md](TextInput.md#possible-future-extensions).

### Lists and scrolling

- Virtual stack: a public horizontal layout, `VirtualScrollMode` (`InfiniteFeed`, `Loop`), `PrefetchDistance`, `QueryItemSize`, a load-more hook with a placeholder, a Core twin.
- Scroll bars: the thumb shrinking during a bounce (iOS), repeated paging on a held track press; native overlays following a stretch.
- Pull to refresh from other edges (render-thread effects linked to overscroll: [1.16](#1160-scroll-linked-effects-shipped)).
- Markdown: long documents as virtualized rows, measuring and recording only the top-level blocks near the viewport (the first frame of a 100 KB document takes about 760 ms on the S9, [Performance.md](../Performance.md#markdown)). Not planned for now. Needs the virtual stack's Core twin or starts on `SkUiMarkdownView` only, and an opt-in way to scroll, since the view is as tall as its document today. Design notes: [1.8 plan, V2](../archive/ImplementationPlan-1.8.md#phase-v--long-documents).

### Looks, colors and motion

- Per-tree color schemes (looks already work per tree); reading the device's dynamic color (Android 12+), the Windows and Mac accent colors and the OS dark mode into the schemes.
- Transitions: several ripples at once, opt-in render-thread painters; render-thread animation of shadow properties.

### Accessibility

- Links inside text as elements, custom actions on refresh (swipe view items and live regions: [1.18](#1180-the-sheet-chips-tab-headers-and-the-time-clock-screen-set-shipped); reorderable items have theirs), public custom actions for own controls, arrow keys within radio groups, a platform expanded / collapsed state for the expander.
- iPad keyboard navigation, Windows' non-linear text scale, font images following a later text size change.

### Architecture and performance

- Scheme defaults resolved at paint time on the Core layer: Core controls copy `SkUiColors.Accent`, `TrackOff`, `Muted` and so on into fields when they are created, so a Core tree made before `SkUiColorScheme.Current` changes (dark mode) keeps the old colors.
- Shadows of rich text (spans, HTML) and of label subclasses on GPU surfaces: still rasterized on the CPU once per item; plain-text labels cast theirs from the glyphs since 1.14 ([Benchmarks.md](Benchmarks.md#114-text-shadow-spike-18-november-2026)).
- Relayout boundaries below the surface root (the root is one); raster cache of stable subtrees with a per-frame budget, for what is expensive to composite every frame (dashed strokes of complex paths: 33 fps on the S9).
- Recording Core-only subtrees off the UI thread (the look, the color scheme and the text and image caches would have to be safe to read from it); hit-testing against render-thread transforms while animations run.
- Overlay masks, so drawn popups can cover hosted native controls (mostly WebView, media and maps now that text fields are drawn). (Overlay bookkeeping on Android: measured and fixed in 1.14, [Benchmarks.md](Benchmarks.md#114-overlay-bookkeeping-on-android-17-november-2026).)
- Windows GPU surfaces can still flash the window's backdrop for a frame when their swap chain panel changes size (rare since `SkUiSurfaceSlack`). `SKSwapChainPanel` handles a size change by presenting an empty buffer before the real frame (`pendingSizeChange` in SkiaSharp's `AngleSwapChainPanel`), and that blank can reach the screen. The fix needs a way around that swap: our own ANGLE panel, or a SkiaSharp change.
- From the November 2026 review: a way for optional packages to take part in the look (today a package carries its own style object, as Markdown does).
- A test that keeps the Core layer to [FR-C1](Requirements.md#core-layer-fr-c): no Core node derives from `BindableObject` or `Element`, and the `Microsoft.Maui.Controls` types the Core API uses (`FontAttributes`, `DataTemplate` for picker popups, the host's `SkUiView`) are a list a new one has to join.

## Verification backlog

Device runs that are still open. How to run them: [Testing.md](Testing.md#device-acceptance-checklists-still-open).

- **Text input:** dictation, Google / iOS / third-party password managers on devices; drawn popups over a focused field. Password reveal (1.9): checked 9 November 2026 on the Galaxy S9 (Android 10, Samsung Keyboard, Samsung Pass) and the iPhone 8 (iOS 16.7) by the device tests' password reveal check, and on the S9 by hand on the samples' sign-in form (see [TextInput.md](TextInput.md#text-fields-d3)); still open: Google and third-party autofill services and iOS Passwords on a revealed field, TalkBack and VoiceOver themselves speaking it, Windows' masked box and Narrator. (CJK and Korean composition, Android autofill on devices, Windows runs, VoiceOver text navigation on a device and the Windows touch keyboard: verified, reported 29 October 2026.)
- **Segmented control, tab view and carousels (1.17):** the feel of the swipes under a finger on the Galaxy S9 and the iPhone 8; TalkBack and VoiceOver on the segments, the tab list, the panes (paging with a swipe, the pane's texts read under its tab's name) and a carousel that autoplays; keyboards on Windows and Mac Catalyst (Ctrl+Tab); the looks against the platforms' own on each; the gallery pages on iOS. Done on 25 November 2026: the five leak scenarios with real handlers on the S9, the iPhone 8, Mac Catalyst and an Android API 37 emulator; the benches on both phones against 1.16.0 ([Benchmarks.md](Benchmarks.md#117-on-the-galaxy-s9-and-the-iphone-8-25-november-2026)); the gallery's two tab view pages on the S9 and the emulator with injected touches (the indicator in step with the panes mid-drag), the looks page on the emulator.
- **Scroll-linked effects (1.16):** the feel of the gallery page's recipes under a finger (stretch, settle, triggered reveals) on both phones. The device benches ran on 23 November 2026 on the S9 and the iPhone 8: no regression without effects ([Benchmarks.md](Benchmarks.md#116-on-the-galaxy-s9-and-the-iphone-8-23-november-2026)).
- **Accessibility:** TalkBack, VoiceOver (iOS) and Narrator on devices; keyboards on Android and Windows; an Appium run with `ExposeAutomationIds`.
- **1.18 phase A:** the press scale's feel and frame times on the Galaxy S9 and the iPhone 8; hit slop and `MinimumTouchTargetSize` under a finger (the gallery's touch targets page); TalkBack and VoiceOver reaching a swipe row's items as custom actions; font features drawn on the phones (the label and entry pages' `FontFeatures` editor).
- **1.18 phases P, L, K:** live regions, `Announce` and `UpdatesFrequently` with TalkBack, VoiceOver and Narrator (the demo's Accessibility page: the clock status, the timer, Announce); the progress ring's indeterminate turn and the stacked bar's animation on the Galaxy S9 and the iPhone 8; chips under a finger and with screen readers (a filter chip as a toggle, the remove action, arrow keys in a group) and the scrolling chip row's feel.
- **1.18 phase S, the sheet:** the drag, the fling and the hand-off from a list inside under a finger on the Galaxy S9 and the iPhone 8 (frame times while it moves); Android's Back button and gesture closing the topmost sheet; a field in a sheet above the soft keyboard; TalkBack, VoiceOver and Narrator in a modal sheet (the page not read, focus on the title, the grabber's actions); the gallery's `SheetDemoPage` cases and pickers on `SkUiSheetPopupPresenter`.
- **1.18 phase T, tab headers:** the gallery's "Headers" editor (strip template, one tab's own) and "Header nodes" on the Mac and a phone: header views in the look's colors, the `Selected` state when a tap and a swipe settle, a tap on a header view selecting its tab; TalkBack and VoiceOver reading the tab list as before.
- **Hosted controls:** the manual checklist (soft keyboard, IME, WebView nesting) on devices and Windows.
- **Controls:** the feel under a finger (a drag of the refresh view, the swipe view and the carousel; scroll bars and overscroll) and the avatar view on devices; frame times of the collection view (with memory at 10,000 items), virtual lists, swipe view, refresh view, carousel, expanders and a scrolling list of shimmering rows were measured in 1.14 ([Performance.md](../Performance.md#list-controls-on-devices)), which left two leads: the iPhone 8's heap rising across a bench run (a leak of surfaces removed from a live layout on iOS and Mac Catalyst, fixed in 1.14), and the S9 counting more frames than its screen shows; the pickers' presenters on Windows (the toolkit's anchored list was checked on 29 October 2026: frame, keyboard, dismissal), in landscape, on rotation and window resize, with screen readers; tooltips on Windows and iPad.
- **Demo gallery:** a full pass on Android and an Apple device.
- **Calendar (1.13):** frame times of a slide were measured on the Galaxy S9 and the iPhone 8 in 1.15 ([Performance.md](../Performance.md#calendar-on-devices)). Still open, by hand on both phones: the swipe's feel and settle, the pinch, a calendar inside a scroll view; TalkBack and VoiceOver on the calendar's element, each day's element and the header's; the keyboard on Mac Catalyst and Windows; the date pickers' calendar popup, the date and time popup and the range picker on each presenter (anchored, window, bottom pane).
- **Inline picker wheels (1.12):** the feel of `SkUiPickerWheels` on a phone (a drag inside a scrolling page and a sheet, haptics), TalkBack and VoiceOver on each column; run with the calendar's checks.
- **.NET 11 on devices (1.14 A7 step 4):** [Performance.md](../Performance.md) measured again on the Galaxy S9 and the iPhone 8 with the net11 build (CoreCLR on Android and iOS, cached interface dispatch on iOS), in the same session as the controls' frame-time checks above (B4).
- **Collection view keyboard, extended selection and item animations (1.10):** keyboard navigation and the focus ring on Windows, Mac Catalyst, an iPad and an Android device with a hardware keyboard; Ctrl / Command and Shift clicks there (the pointer's modifier keys from each platform; Command+A on Mac Catalyst, where the app's Edit menu may take it first); the feel of the item animations on the Galaxy S9 and the iPhone 8, with the gallery's "keyboard and extended selection", "selected item template" and "animated changes" pages.
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
