# SkiaUi implementation plan

Delivery tracking for [Requirements.md](Requirements.md): what is shipped, and the order of the next work. Design details live in the mechanism docs linked from [Development.md](../../Development.md); architecture findings in [ArchitectureReview.md](ArchitectureReview.md).

**Workflow reminder (NFR-2):** prefer **simple, correct** code first; expand tests; then **optimize** hot paths for speed and near-zero allocations.

---

## Shipped

Headless-tested; device-verified on a Galaxy S9, iPhone / iOS simulator, Mac Catalyst and Windows 11 for gestures, scrolling, overlays and memory leaks ([Testing.md](Testing.md)).

| Area | Delivered |
| --- | --- |
| Host and rendering | `ISkUiView : IView`, `SkUiView`, `SkUiContentView`, custom handler + `HwAccelerated`; retained compositor with UI-thread recording and render-thread compositing: Metal (Apple), GL thread (Android), ANGLE / software (Windows) — [RenderingPipeline.md](RenderingPipeline.md) |
| Layouts | `SkUiGrid`, stacks, `SkUiAbsoluteLayout`, `SkUiBorder`, `SkUiContentView` on MAUI's layout managers; **`SkUiFlexLayout`** (MAUI `FlexLayoutManager` over a ported flex engine, frames checked against MAUI's `FlexLayout`); **`SkUiWrapLayout`** and **shrink stacks** on engines shared with Core (FR-27, A1–A3); RTL mirroring |
| Scrolling | `SkUiScrollView` / `SkUiCoreScrollView` on one engine: render-thread fling and animated scroll, wheel, nested and same-axis chaining, native-parent coordination |
| Input | Per-pointer gesture arena for SkUi* and Core: tap, double tap, long press, pan, swipe, pinch, pointer recognizers — [EventMechanism.md](EventMechanism.md) |
| Text | Shared engine: HarfBuzz shaping, bidi / RTL, per-character font fallback, wrap / truncation, `TextRendering` fast path |
| Controls | Label, Button, Image, ImageButton, ActivityIndicator, Switch / CheckBox / RadioButton (three-state `CheckState`, FR-23), **Slider** (horizontal / vertical, FR-24), **ProgressBar** (determinate / render-thread indeterminate, FR-25), shapes — each on both layers |
| MAUI parity P1 | Switch `IsToggled` / `Toggled`; `CheckedChangedEventArgs`; radio group exclusion, `Value`, MAUI's `RadioButtonGroup.GroupName` / `SelectedValue` on drawn layouts; Button / ImageButton `Pressed` / `Released`; BoxView `CornerRadius`; Line `X1`…`Y2`; Image `Aspect.Center` and http; ImageButton `Padding`, border and clipped image — both layers (Phase P) |
| MAUI parity P2 | Visual states from SkiaUi's input state on every SkUi* view (`Disabled` incl. commands that cannot execute, `PointerOver`, `Normal`, focus group), MAUI's per-control states (buttons `Pressed`, CheckBox `IsChecked`, Switch `On` / `Off`, RadioButton `Checked` / `Unchecked`); hover input on every platform (`SkUiTouchAction.HoverMoved` / `HoverExited`, `SkUiView.IsPointerOver`) |
| MAUI parity P4 | Images on both layers through one loader and cache (N9): `MauiImage` (the density file Resizetizer made, SVG items by their PNG name), `FontImageSource` (glyph through the text engine), HTTP(S) with MAUI's `CachingEnabled` / `CacheValidity`, streams; a decoded-image memory cache (LRU, shared leases, cleared on OS memory pressure) and a download disk cache (`SkUiImageCache`), shared in-flight loads; EXIF orientation; FFImageLoading-style `Transformations` (circle, rounded, crop, flip, rotate, blur, tint, color matrix / grayscale / sepia) and downsampling; animated GIF / WebP (`IsAnimationPlaying`); Slider `ThumbImageSource` |
| MAUI parity P5 | Label spans and `TextType="Html"` (own parser, tappable links) on both layers through one formatted-text engine: MAUI's `FormattedText` / `Span` on `SkUiLabel` (XAML ports by prefix; span styles and bindings; `Text` and `FormattedText` replace each other), `SkUiCoreSpan` on `SkUiCoreLabel`; per-span font, size, attributes, colors, background, decorations, character spacing, line height and transform, MAUI's inheritance from the label; spans wrap, truncate and align as one paragraph with bidi across spans; span `TapGestureRecognizer`s (Core: `Tapped`) hit-tested on the shaped runs, also in RTL; paint-only span changes keep the shaped lines |
| MAUI parity P6 | MAUI's `Shape` API on both layers through one engine (`SkUiShapeGeometry`, `SkUiShapePainter`): `Fill` / `Stroke` brushes (solid, linear and radial gradients), `StrokeThickness`, `StrokeDashArray` / `StrokeDashOffset`, `StrokeLineCap` / `StrokeLineJoin` / `StrokeMiterLimit`, `Aspect`, measured, stretched and placed as MAUI's shapes (checked against MAUI's own shapes); stroke-only shapes; new `SkUiRectangle`, `SkUiRoundRectangle`, `SkUiPath` (MAUI geometries, path markup, `RenderTransform`), `SkUiPolygon`, `SkUiPolyline` and Core twins; `SkUiBox` is BoxView again (not a shape). Border `StrokeShape` (shapes of either layer, MAUI's, `RoundRectangle 10` markup), brush stroke, dashes, caps, joins; content inset by the stroke and clipped to its inner edge (`SkUiBorderGeometry`). Labels, buttons and image buttons draw their rounded chrome through one shared state (`SkUiChromeState`) |
| MAUI parity P7 | Gradient `Background` (linear, radial) wherever a solid fill was drawn, on both layers: the default rectangle, label / button chrome through the look (`DrawRoundedBox(…, Paint …)`, `SkUiButtonPaint.FillPaint`), border outlines, boxes, image buttons; Core nodes get `Background` (`Paint`). `Shadow` (FR-20) and `Clip` geometry (FR-11) on every view of both layers (`SkUiCoreShadow`, `SetShadow`, `SetClip`), as render-node properties: shadows of opaque fills are blurred outlines (MAUI's Android rule), others are cast from the drawn subtree and rasterized once on the render thread (`SkUiShadowPainter`, per-node cache keyed by a subtree version), so scrolling, transforms and `AnimateAsync` record nothing and blur nothing again (`SkUiVisualEffects`, `SkUiRenderShadow`) |
| MAUI parity P8 | ScrollView on both layers through the shared engine: MAUI's `HorizontalScrollBarVisibility` / `VerticalScrollBarVisibility` with look-drawn scroll bars (`SkUiLook.DrawScrollBar`; fade on the render thread, RTL side), overscroll per look or scroller (`Overscroll`: rubber-band bounce, Android stretch) that keeps nested chaining and hands over to native ancestors, `ScrollToAsync(Element, ScrollToPosition, bool)` / `GetScrollPositionForElement` / `ScrollToRequested` (Core: `ScrollToAsync(SkUiCoreNode, …)`), horizontal wheel and trackpad on `Both` (`SkUiTouchEvent.WheelDeltaX`), direction-aware flings with live extents (N11). Generic render features for it: children scale, pinned children and scroll links (`SkUiRenderLink`); scroll bars are pinned, scroll-linked Core nodes (`SkUiCoreScrollBar`) |
| MAUI parity P3 | Label text properties on both layers through the shared text engine: `MaxLines` (tail truncation wraps and ellipsizes the last line), `LineHeight`, `CharacterSpacing` (keeps the `Auto` fast path), `TextDecorations`, `TextTransform`, Core `FontAttributes`; custom line breaking for both layers (`SkUiTextLineBreaker` with a context that measures like the engine and falls back to the stock modes: custom ellipsis, fewer decimals) |
| Native hosting | `SkUiMauiContentView` (FR-16): viewport clipping, snapshot while scrolling, drags from overlays handed to drawn scrollers |
| Animation | Render-thread `AnimateAsync`, fling, spin and content slide; UI-thread `SkUiAnimationClock` (FR-7) |
| State-change transitions | Look-driven toggle, press (dim / ripple), slider-thumb and progress transitions on both layers; reduce motion (`SkUiMotion`); measured on a Galaxy S9 and Mac Catalyst (FR-26, [ArchitectureReview.md](ArchitectureReview.md#state-change-animations)) |
| Look and colors | `SkUiLook` (FR-18), `SkUiColorScheme` (FR-19) |
| Core layer | Layouts (stacks, absolute, grid, table, overlay, border, scroll view, **wrap**, **shrink stacks**) and basic controls; `SkUiCoreHost` |
| Diagnostics | Core nodes in the Live Visual Tree, `SkUiDiagnostics`, DevFlow `dev.skiaui` extension |
| Quality | Headless suite; memory-leak scenarios headless and on devices (`scripts/device_tests.sh`, CI on Mac Catalyst with Native AOT); trimmable and Native-AOT-compatible library; benchmarks (`scripts/bench.sh`) |
| Packaging | NuGet `SkiaUi.Maui`; publish workflow bumps the version ([Releasing.md](../Releasing.md)) |

---

## Next

Ordered for adoption by real apps (replace a MAUI page's tree with one drawn surface): **full MAUI parity of the controls that already ship comes first**, so a MAUI page ports by changing the XAML prefix instead of rewriting it; then containers for page shells, virtualized lists, list chrome, and the remaining new controls. Within a phase, the order is the suggested order.

Phase 0 (state-change animations, FR-26) is shipped: looks draw from continuous parameters, transitions run on the UI clock re-recording one control per frame. New controls with state visuals follow the same pattern (a paint struct with the transition, a `SkUiTransitionKind`, the shared animators). Left open: several ripples at once, press scale, render-thread painters.

### Phase P — MAUI parity of shipped controls

Every shipped control gets the MAUI API it is missing. P1–P8 are shipped (see **Shipped**); P9 onwards is next. Most controls are still partial: they draw and behave like their MAUI counterpart for the common properties, but miss secondary API that real pages use (renamed state properties, text styling, image sources, brushes, shape geometry, scrollbars, accessibility).

**Parity rules:**
- **MAUI names and signatures win.** Where SkiaUi diverged (`IsChecked` on Switch, `EventHandler<bool>` for `CheckedChanged`, `StrokeWidth` on shapes), the MAUI member is added and the SkiaUi one is renamed or removed before 1.0, marked **Breaking** in the changelog. SkiaUi extensions stay (`CheckState` / `IsThreeState`, vertical `Slider`, `IsIndeterminate`, label chrome, `ShowsPressEffect`).
- **Both layers.** A property lands on the SkUi* control and on its Core twin through the shared engine or painter (fluent `Set*` + CLR property on Core; no attached-property XAML there). Core-only drift found on the way is fixed in the same item (N13).
- **Drawn by the look.** New visuals (scrollbars, decorations, shadows, radio content) get `SkUiLook` entry points and paint structs, as in **Suggested order for a new control**.
- **Checked against MAUI.** Each item adds a XAML parity test: the control's samples from the MAUI docs, with only the namespace prefix changed, load, bind and measure as documented.

| # | Deliverable | Controls | Notes |
| --- | --- | --- | --- |
| P1 | **Drop-in names and small properties** (shipped) | Switch, CheckBox, RadioButton, Button, ImageButton, BoxView, Line, Image | Switch `IsToggled` / `Toggled` (`ToggledEventArgs`) as the two-state view of `CheckState`; `CheckedChanged` with `CheckedChangedEventArgs` on all toggles; RadioButton `Value`, automatic exclusion by `GroupName`, MAUI's own `RadioButtonGroup.GroupName` / `SelectedValue` (two-way) working on a parent drawn layout; Button / ImageButton `Pressed` / `Released`; BoxView `CornerRadius`; Line `X1` / `Y1` / `X2` / `Y2`; Image `Aspect.Center` and `http://` URIs (where the platform allows cleartext); ImageButton `BorderColor` / `BorderWidth` / `Padding` and the bitmap clipped to `CornerRadius` |
| P2 | **Visual states** (shipped) ([MAUI visual states](https://learn.microsoft.com/dotnet/maui/user-interface/visual-states)) | All SkUi* views | VSM setters, styles and state triggers already work, and `Normal` / `Disabled` come from MAUI's `VisualElement`; only `SkUiButton` raises `Pressed`. `SkUiView` overrides `ChangeVisualState` with SkiaUi's own input state, so every control raises MAUI's states: `Pressed` on ImageButton; `IsChecked` (CheckBox), `On` / `Off` (Switch), `Checked` / `Unchecked` (RadioButton); `PointerOver` from hover tracking in the pointer router (mouse, trackpad, pen and iPad pointer: Mac Catalyst, Windows, iPadOS, Android with a mouse; MAUI's `IsPointerOver` is internal, so SkiaUi keeps its own flag). `Focused` / `Unfocused` follow P10's focus; `Selected` arrives with CollectionView items (B2). Core nodes are not `VisualElement`s: their state visuals stay with the look and transitions |
| P3 | **Label text properties** (shipped) | Label, Button (inherits), Core label | `MaxLines`, `LineHeight`, `CharacterSpacing`, `TextDecorations` (underline / strikethrough), `TextTransform`; Core label `FontAttributes` (N13). In the shared text engine, so the line cache and the `Auto` fast path keep working. Also custom line breaking on both layers (`LineBreaker`: `SkUiTextLineBreaker`, replacing Core's `SkUiCoreTextLineBreaker`) for shorter forms instead of an ellipsis |
| P4 | **Image sources and cache** (shipped) | Image, ImageButton, Slider, Core images | `MauiImage` resources (the build-processed file per platform, e.g. PNGs generated from SVGs), `FontImageSource` (glyph through the font pipeline), one loader + decoded-image cache shared by both layers and keyed by source, decode size and transformations (N9), `UriImageSource.CachingEnabled` / `CacheValidity` on a download disk cache, EXIF orientation, Slider `ThumbImageSource`, animated GIF / WebP (`IsAnimationPlaying`). Beyond MAUI, after FFImageLoading: `Transformations` (`ISkUiImageTransformation`; circle, rounded corners, crop, flip, rotate, blur, tint, color matrix), `DownsampleWidth` / `DownsampleHeight`, `CacheType`, an app-wide `SkUiImageCache.CacheKeyFactory` (tokenized URLs share entries; `SkUiImageCacheKeys.IgnoreQueryParameters`). Core takes a layer-agnostic `SkUiImageSource`. Placeholders (`LoadingPlaceholder`, `ErrorPlaceholder`) and load events (`LoadingStarted`, `LoadingFinished` with status and origin). Left open: fade-in, download progress, a per-view cache key (FFImageLoading's `CacheKeyFactory` with the binding context; the app-wide factory covers URLs and streams), downsampling to the view's size, Android vector drawables, `FontImageSource.FontAutoScalingEnabled` (P10) |
| P5 | **Label spans** (`FormattedText`, `Span`) (shipped) | Label, Core label | Per-span font, size, colours, decorations, character spacing and line height; span `GestureRecognizers` → per-span tap hit-testing on the shaped runs; bidi across spans. A separate formatted-text engine (`SkUiRichTextLayout`) next to the plain one, sharing shaping, breaking, placement and decorations, so plain labels keep their fast path and line cache; labels keep one class per layer (MAUI's Label has both `Text` and `FormattedText`). Also `TextType="Html"` on both layers: SkiaUi's own tolerant parser (`SkUiHtml`, Android's `Html.fromHtml` tag subset plus inline `style`) into the same engine, identical on every platform, links raise `LinkTapped` / `LinkTappedCommand`. Left open: span press feedback, `LineBreaker` / the simple fast path for spans, HTML images, `sub` / `sup` baseline shift, block indents |
| P6 | **Shape model and Border shapes** (shipped) | `SkUiShape` family, Border | MAUI `Shape` API on `SkUiShape`: `Fill` and `Stroke` brushes, `StrokeThickness` (replaces `StrokeWidth`), `StrokeDashArray` / `StrokeDashOffset` / `StrokeLineCap` / `StrokeLineJoin` / `StrokeMiterLimit`, `Aspect`; stroke-only ellipses. New `SkUiRectangle`, `SkUiRoundRectangle`, `SkUiPath` (geometries + path markup), `SkUiPolygon`, `SkUiPolyline`. Border `StrokeShape` (any of these shapes, incl. `RoundRectangle 10` markup), `Stroke` as `Brush`, dashes and joins; content clip follows the stroke shape. Left open: `ImageBrush`, segment edits inside an existing path figure (MAUI raises them internally; reassign the figures), gradient backgrounds (P7), shape-aware hit testing |
| P7 | **Brushes, shadow and clip on every view** (shipped) | All views | Gradient `Background` (`LinearGradientBrush`, `RadialGradientBrush`) wherever a solid fill is drawn (look paint structs carry a brush); `Shadow` (FR-20); `Clip` geometry (FR-11's open path / mask item). Shadows and clips are composite-time, so they do not re-record on scroll or transform. Left open: Core buttons' first frame on the Galaxy S9 is ~25 % slower than before P7 (`scripts/bench.sh` `core-buttons` `firstFrame` 110 → 140 ms, Release; bisected to the P7 commit; not the Core alignment change, the GPU warm-up or the per-recording fill allocations; Core labels and SkUi* buttons unchanged; not visible headlessly); `ImageBrush`, render-thread animation of shadow properties (they change at composite time without re-recording, but do not tween on the render thread), shape-aware hit testing, gradient surface clear colors (a gradient root clears transparent and draws its background) |
| P8 | **ScrollView parity** (shipped) | ScrollView, Core scroll view | `HorizontalScrollBarVisibility` / `VerticalScrollBarVisibility` with look-drawn scrollbars (fade, RTL side); bounce / overscroll per look (rubber band, stretch) that keeps nested chaining; `ScrollToAsync(Element, ScrollToPosition, bool)` and `ScrollToRequested`; horizontal wheel and trackpad on `Both`; direction-aware fling stop and live extents (N11, also needed by Phase B). Built on generic render features (children scale, pinned children, scroll links) instead of scroll-specific compositor code: scroll bars are Core nodes the collection view reuses, and pinned headers can use the links. Also (beyond MAUI): snap points (`SnapPointsType`, `SnapPointsAlignment` on the content's children, render-thread spring), desktop scroll bars that a hovering pointer expands for dragging and paging, and the public `SkUiCoreScrollBar` (style a scroller's bars or place one anywhere in the surface). Left open: the thumb shrinking during a bounce (iOS), repeat paging on a held track press, native overlays following a stretch, not verified on devices yet (overscroll and snap feel, hover expansion, native-ancestor hand-over, Windows wheel axes) |
| P9 | **Content and templates** | RadioButton, Button | RadioButton `Content` (string drawn by the look, or a drawn view) with `TextColor`, font, `CharacterSpacing`, `TextTransform`, `BorderColor` / `BorderWidth` / `CornerRadius`, and `ControlTemplate` with SkUi* content; Button `ImageSource` + `ContentLayout` (position and spacing), on P4's image loader |
| P10 | **Accessibility, focus and font scaling** | All views, both layers | Semantics tree from `SemanticProperties` (`Description`, `Hint`, `HeadingLevel`) and `AutomationProperties`, mapped to platform accessibility (Android `ExploreByTouchHelper`, iOS accessibility elements, Windows automation peers); keyboard focus (`Focus()` / `Unfocus()`, `IsFocused`, `Focused` / `Unfocused`, tab order, activation keys); `FontAutoScalingEnabled` and OS text size (N8). Needed before broad production use |
| P11 | **MAUI `GestureRecognizers` bridge** | All views | The optional compatibility bridge from [Requirements.md](Requirements.md#out-of-scope-for-now): `TapGestureRecognizer` (`NumberOfTapsRequired`, `Command`, `Buttons`), `SwipeGestureRecognizer`, `PanGestureRecognizer`, `PinchGestureRecognizer` and `PointerGestureRecognizer` mapped onto arena recognizers, so MAUI XAML with recognizers keeps working. The arena stays the gesture system; confirm the scope before starting |

P1–P3 are small and unblock the most XAML (P2's hover tracking is the only new input path); P4 removed the largest porting blocker (icons are on almost every page) and fixed N9; P6–P7 shared the brush and geometry plumbing, so they went together. P10 can start in parallel with any item, since it touches the node base classes and the platform handlers rather than individual controls.

**Shared chrome (done with P6).** Labels, buttons and image buttons on both layers draw their rounded chrome (`CornerRadii`, `BorderColor`, `BorderWidth`; SkUi* also MAUI's `int CornerRadius`) through one internal state, `SkUiChromeState` (stored radii and the Core default-radius resolution, border color and width, validation, the cached clip, fill / border / clip drawing), checked by a SkUi-vs-Core pixel test; the public API is unchanged. No public interface for now: styles and XAML target concrete types, the fluent setters return concrete types, and `SkUiBorder` uses MAUI's other names (`Stroke`, `StrokeThickness`). Add one only when a consumer needs it (a look painter API, shared editors). P7 gave the chrome gradient fills (`DrawRoundedBox(…, Paint …)`); its border color stays a color.

### Phase A — Page shells: composition layouts and containers

A1–A3 are shipped (see **Shipped**); A4 onwards follows Phase P.

| # | Deliverable | Layer | Why |
| --- | --- | --- | --- |
| A1 | **`SkUiFlexLayout`** (MAUI `FlexLayoutManager` over a ported flex engine; MAUI's engine is internal) | SkUi* only (Core flex not planned) | MAUI parity; wrapping rows, grow / shrink / basis without a Grid |
| A2 | **Wrap layout** (`SkUiCoreWrapLayout` + `SkUiWrapLayout`, one shared engine) | Core + SkUi* | Chips, tag and filter rows; `Spacing` / `RowSpacing` without the flex model |
| A3 | **Shrink stacks** (`SkUi[Core]HorizontalShrinkLayout`, `SkUi[Core]VerticalShrinkLayout`) | Core + SkUi* | Rows that fit: on overflow, children shrink by their shrink factor (`None`, `Auto`, or a number as CSS `flex-shrink`; truncating / wrapping labels next to fixed icons) |
| A4 | **`SkUiStateContainer`** (loading / empty / error / content) | Core + SkUi* | Community Toolkit parity; busy and skeleton screens |
| A5 | **`SkUiExpander`** (header + animated collapsible content) | Core + SkUi* | Community Toolkit parity; also hosts native content (e.g. a WebView) |
| A6 | **Hosted-control regression suite** | Tests + device checklist | Entry / Editor / WebView in drawn scrollers: focus, IME, scroll nesting, snapshots |
| A7 | **Hardening from adoption** | Both | Label, Grid, Border, ScrollView bugs found while porting real pages |

Weighted growth (leftover space shared by weight) is not a separate layout: use flex `Grow` on SkUi* and grid stars in Core.

### Phase B — Virtualized lists (FR-21, FR-22)

| # | Deliverable | Notes |
| --- | --- | --- |
| B1 | **`SkUiVirtualStackLayout`** (+ virtual scroll) | Vertical first; fixed-extent fast path; estimate + anchoring for variable sizes; recycling; prefetch |
| B2 | **`SkUiCollectionView` MVP** | `ItemsSource` + `ItemTemplate` / selector, single selection, `ItemTapped` / command, header / footer / `EmptyView`, `RemainingItemsThreshold`, pull-to-refresh (`IsRefreshing` / `RefreshCommand`) |
| B3 | **Phase 2** | Grouping, sticky group headers, grid layout, multiple selection, horizontal |

### Phase C — List chrome and text

| # | Deliverable | Notes |
| --- | --- | --- |
| C1 | **`SkUiSwipeView`** | Row actions; competes with vertical scrolling through the arena |
| C2 | **`SkUiRefreshView`** / pull-to-refresh on scroll views | If not already delivered with B2 |
| C3 | **Label auto-fit** (shrink to fit, fit number) | After the measure cache is proven for it |
| C4 | **Tile / wrap-grid layout** | Dashboard tiles |

Label spans moved to P5.

### Phase D — Remaining new MAUI controls

Shadows, scrollbars and bounce, shapes, the image cache and Slider `ThumbImageSource` moved to Phase P (P4, P6–P8; shipped).

| # | Deliverable |
| --- | --- |
| D1 | Carousel + `IndicatorView` (horizontal virtual list with snap points) |
| D2 | Stepper |

### Phase E — SVG images (later)

An SVG added as a `MauiImage` already works: Resizetizer turns it into density PNGs at build time and P4 loads those. Phase E draws `.svg` files **at runtime** (downloaded, from Raw assets or streams, recolored, at any size), as FFImageLoading's `SvgCachedImage` does. Not MAUI parity (MAUI's `Image` has no runtime SVG), so it follows the phases above unless an app needs it first.

| # | Deliverable | Notes |
| --- | --- | --- |
| E1 | **SVG source** in an optional package (`SkiaUi.Maui.Svg`) | Built on **Svg.Skia** (the parser MAUI's Resizetizer uses), so the core package keeps no extra dependency. `SkUiImageSource.FromSvg(...)` for Core and an `SvgImageSource` (`ImageSource` subclass, usable in XAML) for SkUi*; a source-type hook in the loader instead of a hard-coded list. Parsed to an `SKPicture` off the UI thread; the parsed picture is cached in the memory cache (cost estimated from the document), keyed by source; intrinsic size from `width` / `height` / `viewBox` |
| E2 | **Vector drawing** | The picture is drawn scaled into the destination with `Aspect`: sharp at every size and zoom, no raster per size. A raster (keyed by pixel size) only when transformations are set or a document is too costly to replay each frame. Recoloring: `TintColor` (as `MauiImage`) and FFImageLoading's `ReplaceStringMap` (text replacements before parsing, part of the key) |
| E3 | **Checks** | Trimming / Native AOT of Svg.Skia's XML / CSS stack (else the package is marked not trimmable); unsupported features (filters, embedded text, CSS) degrade or report `LoadError`; device frame time and memory against `MauiImage` PNGs; a leak scenario |

### Architecture work alongside

From [ArchitectureReview.md](ArchitectureReview.md) (finding numbers N\*). Scheduled next to the phases above, not after them.

- **Now (small, correctness):** disabled controls must not block ancestor scrolling (N2); Core `IsEnabled` / `InputTransparent` with one blocking rule (N3); child alignment in every Core container (N4); scheme defaults resolved at paint time instead of snapshotted at construction (rest of N6; look / scheme swaps already redraw live surfaces).
- **With Phase A:** measure invalidation without re-recording ancestors (N5); containers that re-measure often (flex, wrap, expander) benefit first.
- **Before Phase B:** relayout boundaries; raster cache of stable subtrees (N7). The shared image cache (N9) shipped with P4; fling live extents (N11) shipped with P8.
- **Accessibility (N8):** scheduled as P10; reduce motion already follows the OS (`SkUiMotion`).
- **Drawn over native:** overlay masks, so drawn popups can cover hosted controls; cheaper overlay bookkeeping on Android (N10, N12).
- **One implementation per control:** extract layer-agnostic engines and shared node mechanics, gated by SkUi-vs-Core parity tests (N13). Already shared: toggle drawing and all transition animators (`SkUiToggleDrawing`, `SkUiTransitionAnimators`), the rounded chrome of labels and buttons (`SkUiChromeState`), shapes and border geometry (P6), backgrounds, shadows and clips (`SkUiVisualEffects`, P7).

---

## MAUI parity at a glance

Checked against `Microsoft.Maui.Controls` 10.0.110 (the pinned version). **Partial** means the control ships but misses MAUI API that real pages use; the missing parts are scheduled in Phase P.

| MAUI | SkiaUi | Status |
| --- | --- | --- |
| ActivityIndicator, ProgressBar | SkUi* + Core | Done (ProgressBar plus indeterminate) |
| CheckBox | SkUi* + Core | Done, plus three states |
| Slider | SkUi* + Core | Done, plus vertical; `ThumbImageSource` (P4) |
| Switch | SkUi* + Core | Done, plus three states (`On` / `Off` visual states: P2) |
| RadioButton | SkUi* + Core | Partial: `Content`, `ControlTemplate` (P9); group exclusion, `Value`, `RadioButtonGroup` done (P1) |
| Label | SkUi* + Core | Done: text properties (P3), spans and span taps, `TextType="Html"` (P5), plus custom line breaking and tappable HTML links |
| Button | SkUi* + Core | Partial: `ImageSource` + `ContentLayout` (P9); text properties done (P3) |
| Image, ImageButton | SkUi* + Core | Done (P4), plus shared memory / disk cache, transformations, downsampling; runtime SVG files: Phase E |
| BoxView | SkUi* + Core | Done, gradient `Background` (P7) |
| Ellipse, Line, Rectangle, RoundRectangle, Path, Polygon, Polyline | SkUi* + Core | Done (P6): brushes, stroke model, `Aspect`, geometries and path markup; `ImageBrush` not drawn |
| Border | SkUi* + Core | Done (P6): `StrokeShape`, brush stroke, dashes, stroke inset and shape clip; gradient `Background` (P7) |
| ScrollView | SkUi* + Core | Done (P8): scroll bars (draggable on desktop, `SkUiCoreScrollBar`), overscroll (bounce, stretch), scroll to element, both wheel axes, plus snap points |
| Grid, VerticalStackLayout, HorizontalStackLayout, AbsoluteLayout, ContentView | SkUi* + Core | Done |
| FlexLayout | `SkUiFlexLayout` (SkUi* only) | Done (A1) |
| StackLayout, BindableLayout | Stacks; templated items via CollectionView | Map |
| Every view: visual states | MAUI's states per control, `PointerOver` from hover, state triggers | Done (P2); `Focused` waits for keyboard focus (P10), `Selected` for CollectionView (B2) |
| Every view: `Shadow`, gradient `Background`, `Clip` | SkUi* + Core | Done (P7); `ImageBrush` not drawn |
| Every view: `SemanticProperties`, focus, font scaling | — | P10 |
| Every view: `GestureRecognizers` | Arena gestures (`Tapped`, …) | P11 (bridge, scope to confirm) |
| CollectionView | — | B1–B3 |
| RefreshView, SwipeView | — | C1, C2 |
| CarouselView, IndicatorView | — | D1 |
| Stepper | — | D2 |
| Entry, Editor, SearchBar, WebView, pickers, Map, media | Hosted (`SkUiMauiContentView`) | By design |
| Pages, Shell, navigation | MAUI | Out of scope |
| Frame, ListView, TableView, cells (`TextCell`, `ImageCell`, `SwitchCell`, `EntryCell`, `ViewCell`), Compatibility layouts | — | Out of scope: obsolete in MAUI (Frame since .NET 9, the rest in .NET 10); use Border and CollectionView |

**Not planned (by design):** drawn Entry / Editor / WebView / media / maps (host them); Shell and navigation replacements; MAUI-obsolete controls (above); tooltips, context flyouts and drag and drop gestures; vendor control clones; a Core flex layout (Core uses the wrap and shrink layouts and the grid).

---

## Acceptance checks

| Item | Must verify |
| --- | --- |
| Every Phase P item | MAUI doc samples for the control, with only the prefix changed, load, bind and measure as documented; the same result on SkUi* and Core; changelog lists renamed members as **Breaking** |
| P1 names and small properties | `IsToggled` and `IsChecked` stay in sync both ways; one checked radio per group across nested layouts, `SelectedValue` two-way; `Pressed` / `Released` pair up, also when a scroll cancels the press; Line measures and places its points as MAUI's unstretched `Line` (points are not mirrored in RTL, as on Android and iOS) — headless tests in `ControlParityTests` / `RadioButtonGroupTests` |
| P2 visual states | Each control goes through the same states as its MAUI counterpart for the same steps (`VisualStateTests` compares every group after each step); `PointerOver` enters and leaves with the mouse on Mac Catalyst, Windows and Android, the iPad pointer, and clears when the pointer leaves the surface; `StateTrigger` / `CompareStateTrigger` / `AdaptiveTrigger` on a drawn control |
| P3 text properties | `MaxLines` truncates with the `LineBreakMode` ellipsis; `LineHeight` and `CharacterSpacing` change measure; decorations follow bidi runs; the `Auto` fast path still applies to plain Latin text; custom breakers measure as the engine draws and can fall back to the stock modes — headless tests in `LabelTextPropertiesTests` |
| P4 images | `MauiImage` and `FontImageSource` resolve on Android, iOS, Mac Catalyst and Windows (checked on Mac Catalyst, the iOS simulator and an Android emulator by the device tests' render check: density files at their base size, SVG items, glyphs; downloads in the disk cache on Mac Catalyst; Windows not yet run); a second view of the same source decodes nothing and shows synchronously; concurrent loads share one decode; evicted images live while shown; transformations, EXIF, GIF frames, disk validity — headless tests in `ImageLoadingTests` / `ImageTransformationTests` / `ImageSourceParityTests`; memory flat after release (`ImagesReloaded` leak scenario, Mac Catalyst Native AOT) |
| P5 spans | Mixed styles wrap inside one paragraph; span taps hit the right run, also in RTL; MAUI's doc samples load, bind and tap; replaced formatted strings are collectable — headless tests in `LabelSpansTests` / `LabelHtmlTests`, `LabelsReshaped` leak scenario |
| P6 shapes and Border | Geometry matches MAUI's shapes (stretch, stroke alignment, dashes); Border clips content to its `StrokeShape` — headless tests in `ShapeTests` (measure against MAUI's own shapes, MAUI doc samples, brushes, dashes, caps, joins, fill rules, change tracking, SkUi-vs-Core pixels) and `BorderShapeTests` (markup, stroke inset, concentric / exact clips, dashes from the top-left, shape edits, Core pixels), `ShapesRestyled` leak scenario (shared brushes and stroke shapes) |
| P7 brushes, shadow, clip | Gradients and shadows survive look / scheme swaps; scrolling and transform animations do not re-record shadowed or clipped nodes — headless tests in `BrushShadowClipTests` (gradients on every filled view of both layers, clip and hit bounds, outline and content silhouettes, blur and opacity, parent clips, no re-record and a single rasterization while scrolling, transforming and animating, compositor vs immediate painter, SkUi* vs Core pixels, MAUI doc samples) and `BrushLookSwapTests`, `EffectsRestyled` leak scenario (shared brushes, geometries and shadows) |
| P8 scroll | Scrollbars show and fade per visibility; bounce keeps nested chaining and fling hand-off; scroll-to-element lands at each `ScrollToPosition` — headless tests in `ScrollViewParityTests` (bar frames, fade timing, render-thread thumb during a fling without recording, RTL and corner, Core parity, taps through bars; hover expansion, thumb drags and track paging, touches through the strip, bars placed by the app; snap points (single and mandatory, alignment, Core, after the wheel); bounce and stretch drags, compositor vs immediate painter, chaining and native hand-over, fling bounce, direction-aware and growing flings, two-axis wheel chaining, every `ScrollToPosition`, Core nodes and nested scrollers, waiting for layout, `ScrollToRequested`, MAUI doc markup) |
| P10 accessibility | TalkBack, VoiceOver and Narrator read and activate drawn controls; keyboard tab order; text grows with the OS text size |
| Flex layout | Frames match MAUI `FlexLayout` for direction, wrap, justify, align and grow / shrink / basis / order; attached-property edits relayout; works under an infinite constraint (scroll view); RTL |
| Wrap layout | Wraps across width; re-measures when a child's size changes; RTL; same frames on both layers |
| Shrink stack | Without overflow it is a plain stack; on overflow children shrink by factor (Auto: only those above the average), stop at their minimum size, and `None` children keep their size; both axes, both layers |
| StateContainer | Switching states releases the previous content (leak scenario) |
| Expander | Header tap toggles with animation; nested in a scroll view; hosted native child |
| Virtual stack | No blank frames while flinging on a device; recycling without per-item allocations; memory flat after release |
| CollectionView MVP | Template recycling; selection + `ItemTapped`; `EmptyView`; load-more threshold; pull-to-refresh |
| SwipeView | Wins horizontal swipes, loses vertical scrolls |
| Hosted controls | Entry focus + IME; WebView scroll nesting; snapshots during flings (Android / Windows) |

---

## Cross-cutting

| Topic | Expectation |
| --- | --- |
| Correctness → optimize | NFR-2 on every new hot path |
| Both layers | New controls ship on SkUi* and Core with the same look entry points |
| Extensibility | Looks draw every control; virtual hooks / interfaces / shared helpers (NFR-4) |
| Testing | Headless tests by functionality ([Testing.md](Testing.md)); a leak scenario for every new stateful control; demo page per control (enforced by `ComponentDemoTests`) |
| Trimming / AOT | No reflection; analyzers fail the build |
| Tracking | Check off [Requirements.md](Requirements.md); summarize shipped behavior in [Development.md](../../Development.md); changelog under `## Unreleased` |

## Suggested order for a new control

1. Look entry point (`SkUiLook.Draw*` / `Measure*`, paint struct carrying the transition state, `SkUiTransitionKind` if it has state visuals, default look)
2. Types on both layers, shared math / input helpers
3. Measure / arrange, paint
4. Input through the gesture arena (write user changes back to bindables)
5. Animation: state changes through the shared transition animators; continuous motion on the render thread when possible
6. Demo page, docs page under [docs/controls/](../controls/README.md)
7. Tests, leak scenario, then optimize
