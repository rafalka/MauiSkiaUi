# SkiaUi 1.4 implementation plan: images and look details

**Goal:** images load the way apps that came from FFImageLoading expect (a fade-in, download progress, a cache key per view, decoding at the size the view shows, Android's XML drawables), and the platform looks close the differences the look comparison still shows (progress bar heights, popup chrome, Android text width, the Apple radio label) plus an opt-in Fluent pips pager.

**Baseline:** .NET MAUI 10.0.120 (or the latest 10.0.x when 1.4 ships), SkiaSharp 4.154.0, branch `1.4-implementation` from `v1.3.0`. What has shipped is in the [changelog](../../CHANGELOG.md); the rest of the 1.x backlog stays in the [roadmap](Roadmap.md#backlog-1x).

**Rules** (as in 1.2, from [Requirements.md](Requirements.md#principles)):

- 1.x keeps the 1.0 API: everything here is additive. New look members are virtuals with today's behavior as their default, unless a [decision](#decisions) says otherwise.
- Both layers: each image member lands on `SkUiImage` (so `SkUiImageButton`) and `SkUiCoreImage` (`Set*`); look members apply to both.
- A feature a view does not use costs nothing per frame or per load (`scripts/bench.sh --baseline v1.3.0`).
- Names follow FFImageLoading where the image API already does (`DownsampleWidth`, `TransformPlaceholders`), since that is where these apps come from.

## Phase I — Images

The loader (`SkUiImageLoader`), the per-view slot (`SkUiImageSlot`) and the caches are shared by `SkUiImage`, `SkUiCoreImage`, button icons, slider thumbs and the avatar; a step changes the shared code once and exposes members on the image views. Docs: [SkUiImage.md](../controls/SkUiImage.md) (and its "Differences from MAUI Image" table) with every step.

- **I1 Per-view cache key** (done). Size: S.
  - `CacheKey` (`string?`, default `null`) on both layers: when set, it is the source's cache identity for that view (memory cache and, for URIs, the download cache), in place of `SkUiImageCache.CacheKeyFactory` and the source's own key. Placeholders keep their own keys. Prefixed apart from built-in and factory keys, as `"key:"` is today (`SkUiImageLoader.SourceKey`).
  - Changing it reloads, as changing `Source` does. `SkUiImageCache.RemoveByKeyAsync(key)` forgets what is cached under a view's or the factory's key (a separate name: a `RemoveAsync(string)` overload would make `RemoveAsync(null)` ambiguous). A view key and an equal factory key name the same entry.
  - Tests: two views with different sources and one key share one decode; a key change reloads; the factory is not consulted when a key is set.
- **I2 Downsampling to the view's size** (done). Size: M.
  - `DownsampleToViewSize` (`bool`, default `false`). The load waits for the first arrange and decodes no larger than the arranged size at the display density: fit inside it, or cover it for `AspectFill` / `Fill` (the existing `DownsampleToFill`). An explicit `DownsampleWidth` / `DownsampleHeight` wins.
  - A view whose size comes from its image (arranged at 0 × 0 while nothing is loaded) loads as without the option, so auto-sized images still measure.
  - Bounds round up to steps of 16 DIPs (whole DIPs as the avatar uses would miss the cache on small layout changes; 64 px steps cost up to twice the pixels for small thumbnails) so recycled list rows and small layout changes hit the memory cache; the image decodes again only when the view grows past the decoded size, never when it shrinks. Intrinsic size (`ImageSize`) does not change, as with the other downsampling.
  - Tests: decode bounds from arrange for each `Aspect`; growth reloads once and shrinking does not; recycled rows share a cache entry. Bench: new `skui-images` / `core-images` scenarios (a grid of local images, update = new sources), with and without the option (`-viewsized`), set through `TrySetOption` so `--baseline v1.3.0` still builds. Measured against I1: no change without the option; with it, arrange +0.7 ms (SkUi) / +0.3 ms (Core) per 1,000 images, where the cache hit now happens, and Core's update +0.7 ms for the extra layout pass of a new source.
- **I3 Fade-in** (done). Size: M.
  - `FadeAnimationEnabled` (default `false`, [D1](#decisions)), `FadeAnimationDuration` (default: the look's new `SkUiTransitionKind.ImageFadeIn`, 200 ms in `DefaultSkUiLook`, `None` under reduce motion), `FadeAnimationForCachedImages` (default `false`: a memory-cache hit shows at once, as in FFImageLoading).
  - On the render thread: while a fade runs, the image is drawn by an `SkUiRenderDrawable` (alpha from the render clock, start stamped when the load completes) above the recorded placeholder, so a fade costs no re-recording per frame and many rows can fade while a list scrolls. When it ends, the node re-records once with the image in its picture and drops the drawable. The drawable owns a picture of the image as the view paints it (so an image button's clip comes along); the picture holds the image's pixels as recorded content does, so no lease is needed (ownership as in [RenderingPipeline.md](RenderingPipeline.md)).
  - Animated images fade in while they play (each frame is a new picture). Button icons do not fade. An image replacing a shown one (another source in its place, a larger decode) shows at once: there is nothing to fade over, and fading from empty would flash.
  - Tests: frames at 0 / half / end of the fade (`SkUiFrameRenderer`), no fade for cache hits by default, none under reduce motion, a view removed mid-fade released at once (leak scenario `ImageFadingIn`: the fade ends when the view's animation root changes).
- **I4 Download progress** (done). Size: S–M.
  - `DownloadProgress` event (`SkUiImageDownloadProgressEventArgs`: `Source`, `BytesReceived`, `TotalBytes` (`long?`, from `Content-Length`), `Progress` (`double?`)) and a read-only bindable `LoadingProgress` (0–1; `null` (unknown length) as `-1`, [D2](#decisions)) for binding a progress bar; Core: the event and a `LoadingProgress` property with change notification.
  - `SkUiImageLoader.DownloadAsync` reads in chunks and reports to the shared in-flight load, which fans out to every view waiting on it, marshalled to the dispatcher and coalesced per view: one dispatch at a time, which reports the latest chunk (at most one report per dispatcher turn). Disk and memory cache hits report nothing.
  - Tests: a fake `HttpMessageHandler` streaming in chunks, with and without `Content-Length`; two views on one download both get progress; a cancelled load stops reporting.
- **I5 Android vector drawables** (done; checked on an Android emulator against MAUI's `Image`, sharp with `DownsampleToViewSize`). Size: S–M.
  - `SkUiPackageImages` on Android detects a resource that is XML (`TypedValue.String` ends with `.xml`) and renders it through `Resources.GetDrawable(id, theme)` into an Android bitmap at the size the view decodes at (intrinsic dp × density, or the downsample bounds: crisp at any size), copied into an `SKImage`. Covers vector, shape, layer-list and inset drawables and `android:tint`; an animated vector draws its first frame.
  - Intrinsic size: the drawable's intrinsic size in dp, so it lays out as MAUI's `Image` does.
  - Device-only (headless tests have no Android resources): a vector drawable in the demo's Android resources and a row on the gallery's image page; the [verification backlog](Roadmap.md#verification-backlog) gets the device check.

## Phase K — Look details

Measured with `SkUiLookComparisonPage` on devices (the `skiaui-look` skill), as the platform looks were. Each step ends with its rows green (within 0.5 DIP) and the comparison screenshots in [ControlLook.md](ControlLook.md#matching-mauis-native-controls) updated.

- **K1 Progress bar heights** (done). Size: S. Re-measure the native `ProgressBar` on iOS 26, Mac Catalyst, Android (M2 and M3) and Windows 11 and fix `DefaultProgressBarHeight`, `ProgressBarTrackHeight` and `ProgressBarTrackLineHeight` in the platform looks where they differ (Android M2's 16 dp box around a 4 dp track is the known suspect).
  - Measured (MAUI 10.0.120): Android M2 (378 × 16 with a 4 dp track), Android M3 (378 × 4, gap and stop dot), iOS 26 (iPhone 17 Pro Max, 198 × 4) and Mac Catalyst (712 × 4, drawn at 77 %) match in size and in pixels: heights, track thickness, gap and widths. The one difference: Material 2's bar has square ends, which the Android look rounded; `AndroidSkUiLook.GetProgressBarCornerRadius` now returns 0 (the Material 3 look keeps round ends). Windows not measured in this pass (no Windows machine); its 3 epx bar over a 1 epx line was measured in 1.0.
- **K2 Popup chrome.** Size: S–M.
  - Look members: `PopupCornerRadius` (`CornerRadius`) and `GetPopupShadow()` (`Shadow?`), per placement (a `BottomPane` rounds only its top corners). Values from the native menus and pickers: iOS 26, Android M2 / M3 (menus 4 dp with elevation 8; M3 dialogs 28 dp), Windows 11 flyouts (8 epx, Fluent's flyout shadow).
  - The drawn popup content (`SkUiPickerPopup`, `SkUiColumnsPopup`) clips its background to the radius. The shadow is outside the content's bounds, so the request carries both as `SkUiPopupRequest.Chrome` for the presenter's container; the CommunityToolkit presenter and the samples' presenters apply it, MAUI's action sheet ignores it. [Popups.md](Popups.md) and [SkUiPicker.md](../controls/SkUiPicker.md#popups-on-your-popup-system) say what a presenter does with it.
- **K3 Android text width.** Size: M (investigation first). Android text measures 1–2 % wider than a `TextView`. Measure widths of fixed strings at 12, 14, 16 and 20 sp against `TextView` (`Paint.measureText`) and test, in order:
  1. The system font: Android 12+ ships Roboto as a variable font, and `TextView` applies axis values (`wght`, `wdth`, `opsz`) that SkiaUi's typeface may not.
  2. Hinting: `SKFont` keeps Skia's default hinting with `LinearMetrics`; Minikin shapes unhinted at the pixel size.
  3. Shaping in DIPs rather than at the device pixel size.
  The fix goes into the engine behind an Android look member (as `TextIncludesFontPadding`), so other looks and the bundled-font tests are unchanged; the label benches show no regression.
- **K4 Apple radio label width.** Size: S. The radio button's label is about 2 DIPs wider than MAUI's template on iOS and Mac Catalyst. Compare MAUI's default radio template (its spacing and the label's margins) with `DefaultRadioButtonContentSpacing` and the label's measure (font tracking applied?), and fix in `IosSkUiLook` (`MacSkUiLook` inherits).
- **K5 Fluent pips pager.** Size: M. MAUI on Windows draws `IndicatorView` as plain ellipses (`MauiPageControl`), which `WindowsSkUiLook` already matches, so the pips pager is an opt-in style ([D3](#decisions)):
  - A `Pips` value for the existing `SkUiIndicatorStyle` (`DefaultSkUiLook.IndicatorStyle`; `Dots`, `Pill` today). `Pips` draws WinUI's `PipsPager`: small pips, a larger selected pip, pips growing under the pointer, and previous / next chevrons (shown on hover with a mouse, always with touch).
  - The chevrons are hit areas of the indicator view on both layers (a tap moves `Position`, the linked carousel follows), with accessibility actions and keyboard arrows.
  - A gallery row next to MAUI's `IndicatorView`; tests for drawing, hit testing and `Position` changes.

## Docs, migration and demo

Done with each step:

- Control pages: [SkUiImage.md](../controls/SkUiImage.md), [SkUiImageButton.md](../controls/SkUiImageButton.md), [SkUiProgressBar.md](../controls/SkUiProgressBar.md), [SkUiRadioButton.md](../controls/SkUiRadioButton.md), the indicator view's and pickers' pages; [ControlLook.md](ControlLook.md) (new look members, the "Known differences" paragraph), [Popups.md](Popups.md), [RenderingPipeline.md](RenderingPipeline.md) (the fade drawable).
- [Migration.md](../Migration.md) and the migration skills (`plugins/skiaui-migration`): FFImageLoading's `FadeAnimationEnabled`, `DownloadProgress`, `CacheKey` and `DownsampleToViewSize` map one to one; Android XML drawables no longer need converting.
- The demo's component gallery: image page rows for fade-in, progress (a slow download), downsampling and the vector drawable; the indicator view's pips style.
- [Roadmap.md](Roadmap.md): items leave the backlog as they ship; [Requirements.md](Requirements.md): status; [CHANGELOG.md](../../CHANGELOG.md): an entry under `## Unreleased` per feature.

## Order

| # | Steps | Size | Why here |
| --- | --- | --- | --- |
| 1 | I1 | S | Smallest; touches the cache key path that I2 extends |
| 2 | I2 | M | On top of I1's key changes; I5 renders at its bounds |
| 3 | I4 | S–M | Independent of the slot's drawing |
| 4 | I3 | M | The only render-pipeline change; after the load path settles |
| 5 | I5 | S–M | Needs a device; uses I2's bounds |
| 6 | K1, K4 | S | Measurement runs, one session per platform |
| 7 | K2 | S–M | New look members and the request's chrome |
| 8 | K3 | M | Open-ended investigation; may need an engine option |
| 9 | K5 | M | Opt-in, lowest demand |

Phase I and phase K are independent tracks. Within K, K1 / K2 / K4 share device sessions.

## Decisions

- **D1 Fade-in off by default.** FFImageLoading fades by default; MAUI's `Image` does not. **Decided:** off, so 1.x apps and screenshot tests draw as before; an app turns it on per view or with an implicit style.
- **D2 `LoadingProgress` when the length is unknown.** **Decided:** `-1` in the bindable (a `ProgressBar` binding then shows nothing useful, but no `double?` converter is needed), `null` in the event's `Progress`.
- **D3 The pips pager is opt-in.** **Decided:** `WindowsSkUiLook` keeps MAUI's ellipses (parity with MAUI on Windows); `Pips` is a style any look can use.

## Acceptance

- Headless suite and memory-leak scenarios pass (new: `ImageFadingIn`); 0 build warnings with the trim and AOT analyzers.
- `scripts/bench.sh --baseline v1.3.0` shows no regression in the existing scenarios; views without the new image options load and draw as in 1.3.
- The look comparison rows for progress bars and radio buttons are green on iOS, Mac Catalyst, Android (M2, M3) and Windows; Android label widths within 0.5 % of `TextView`.
- Device checks of I3, I4 and I5 recorded in [Testing.md](Testing.md) or left in the verification backlog.
