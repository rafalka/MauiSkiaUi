# Changelog

Release notes for the NuGet package **SkiaUi.Maui**.

Add entries under `## Unreleased`. Publishing (the NuGet publish workflow, [docs/Releasing.md](docs/Releasing.md)) renames that section to the new version, and the pack workflows copy a version's section into the package `PackageReleaseNotes` field shown on nuget.org, with a link back to this file. Newest section first; headings are exactly `## <version>`.

## Unreleased

- **Fixed: drawn controls as binding sources.** A binding, trigger or `x:Reference` whose source was a drawn control (`IsEnabled="{Binding IsChecked, Source={x:Reference Agree}}"`, a `DataTrigger` on a label's text, a `CompareStateTrigger` on a control property) got the initial value but never updated. MAUI raises `PropertyChanged` before a property's change callback, and SkiaUi's getters returned a field that only the callback updates. Getters now read the bindable store, as MAUI's controls do.
  - **Breaking (FR-10 revised):** fluent `Set*` setters write the bindable store; they are the property setters in chainable form (`label.SetText("a")` is `label.Text = "a"`), so bindings and triggers see them and `GetValue` agrees. Before, they changed only the control and left the store (and bindings) behind. Like the property setter, a direct setter is a local value over a style's, unless it repeats the style's value. Arguments are still validated before anything changes. `SkUiSlider.SetMinimum` / `SetMaximum` / `SetSliderValue` now keep the requested value as the properties do.

- **MAUI visual states (P2)** on every `SkUi*` view, from SkiaUi's own input state: `Disabled` (also while a button's command cannot execute), `PointerOver`, `Normal`, and `Focused` / `Unfocused` in a focus group, plus each control's MAUI states: `Pressed` on `SkUiButton` / `SkUiImageButton`, `IsChecked` on `SkUiCheckBox` (when the `CommonStates` group defines it, as in MAUI), `On` / `Off` on `SkUiSwitch`, `Checked` / `Unchecked` on `SkUiRadioButton`. Checked against MAUI's controls step by step. `StateTrigger`, `CompareStateTrigger` and `AdaptiveTrigger` work on drawn controls.
  - **Hover:** new `SkUiView.IsPointerOver` and `SkUiTouchAction.HoverMoved` / `HoverExited`. A mouse, trackpad, pen or iPad pointer over the surface makes the view under it and its ancestors pointer-over. Windows reports hover through SkiaSharp; Android surfaces use the view's `Hover` event, Apple surfaces a `UIHoverGestureRecognizer` ([EventMechanism.md](docs/design/EventMechanism.md#hover)).
  - Before, only `SkUiButton` raised `Normal` / `Pressed` / `Disabled`.

- **MAUI API parity of existing controls (P1)**, on SkUi* and Core:
  - **Switch:** `IsToggled` (two-way bindable) and `Toggled` (`ToggledEventArgs`), MAUI's names for the two-state view of `CheckState`; they stay in step with `IsChecked`.
  - **RadioButton:** checking one unchecks the rest of its group, with MAUI's rules: siblings in the same parent when there is no `GroupName`, every radio button with the same `GroupName` on the page otherwise. New `Value`, and MAUI's own `RadioButtonGroup.GroupName` / `SelectedValue` work on drawn layouts with unchanged markup: the layout names its radio buttons, and `SelectedValue` binds two-way to the checked one's `Value` ([SkUiRadioButton.md](docs/controls/SkUiRadioButton.md)). `SkUiCoreRadioButton`: the radio buttons in the same parent exclude each other.
  - **Button, ImageButton:** `Pressed` and `Released` events, in MAUI's order (`Pressed`, `Released`, `Clicked`); a cancelled press raises `Released` without `Clicked`.
  - **BoxView:** `SkUiBox` / `SkUiCoreBox` `CornerRadius` (per corner, MAUI's `CornerRadius`).
  - **Line:** `X1`, `Y1`, `X2`, `Y2` and the `(x1, y1, x2, y2)` constructor; the line measures to its far end points plus the stroke and is placed as MAUI places an unstretched `Line` ([SkUiLine.md](docs/controls/SkUiLine.md)).
  - **Image:** `Aspect.Center` (unscaled and centered, also for images the decoder reduced) and plain `http://` sources (they need the platform's cleartext permission).
  - **ImageButton:** `Padding`, `BorderColor` and `BorderWidth`; per-corner `CornerRadii` (with MAUI's `int` `CornerRadius` setting all four, as on `SkUiButton`), which now clip the image and the border as well as the press tint.
  - **Breaking:**
    - `CheckedChanged` on all toggles (both layers) is an `EventHandler<CheckedChangedEventArgs>` (was `EventHandler<bool>`): read `args.Value`.
    - Radio buttons uncheck the other radio buttons of their group (before, `GroupName` was only stored). Apps that cleared siblings in `CheckedChanged` can drop that code.
    - `SkUiCoreRadioButton.GroupName` / `SetGroupName` are removed: a Core group is the radio buttons in one parent.
    - `SkUiLine` / `SkUiCoreLine` draw between their points instead of the diagonal of their bounds; a line without points draws nothing. Set `X2` / `Y2` (e.g. to the old `WidthRequest` / `HeightRequest` minus the stroke) to keep a diagonal.
    - `SkUiImageButton.CornerRadius` and `SetCornerRadius` are `int` (were `double`), as MAUI's `ImageButton`; fractional and per-corner radii go in the new `CornerRadii`.
    - Core labels and buttons have only `CornerRadii`: `SkUiCoreLabel.CornerRadius` / `SkUiCoreButton.CornerRadius` (the `int` view) are removed, `SkUiCoreImageButton.CornerRadius` became `CornerRadii`, and `SetCornerRadius` takes a `double` on all three (sets all four corners).

- **Core attached properties** (`SkUiCoreAttachedProperty<T>`, `node.GetValue` / `SetValue` / `ClearValue` / `IsSet`): typed per-child values that Core layouts read, the Core counterpart of MAUI attached properties ([SkUiCore.md](docs/controls/SkUiCore.md#attached-properties)). Grid cells (`SkUiCoreGrid.RowProperty`, …), absolute bounds (`SkUiCoreAbsoluteLayout.LayoutBoundsProperty` / `LayoutFlagsProperty`) and shrink factors now live on the child instead of in per-layout tables: they can be set before the child is added and stay with it when it moves. The existing layout methods are unchanged and write the same values.
  - `SkUiCoreAbsoluteLayout.Add(child)` is now allowed (it was a compile error): the child is placed by its own `LayoutBounds` / `LayoutFlags`, by default at its measured size at (0,0).
- **`SkUiFlexLayout`:** MAUI's `FlexLayout` API (`Direction`, `Wrap`, `JustifyContent`, `AlignItems`, `AlignContent`; attached `Order`, `Grow`, `Shrink`, `AlignSelf`, `Basis`, the same property objects as MAUI's) laid out by MAUI's `FlexLayoutManager`. MAUI's flex engine is internal, so SkiaUi ships a port of it; frames match MAUI's `FlexLayout`. SkUi* only ([SkUiFlexLayout.md](docs/controls/SkUiFlexLayout.md)).
- **`SkUiWrapLayout` / `SkUiCoreWrapLayout`:** children flow left to right and wrap onto new rows, with `Spacing` between items and `RowSpacing` between rows; children align inside their row. For chips, tags and filter rows ([SkUiWrapLayout.md](docs/controls/SkUiWrapLayout.md)).
- **Shrink stacks** (`SkUiHorizontalShrinkLayout`, `SkUiVerticalShrinkLayout`, `SkUiCoreHorizontalShrinkLayout`, `SkUiCoreVerticalShrinkLayout`): stacks that fit their main axis. On overflow, children shrink by their `SkUiShrinkFactor` (attached `SkUiShrinkLayout.Shrink`; Core `SkUiCoreShrinkLayout.ShrinkProperty`, e.g. `Add(child, SkUiShrinkFactor.Auto)`): `None` (default) keeps its size, `Auto` shrinks in proportion to its natural size when larger than the average child, a number shrinks by factor × natural size as CSS `flex-shrink`; no child goes below its minimum size. A label then truncates or wraps next to fixed icons; otherwise the layout is a plain stack that takes only the space it needs ([SkUiShrinkLayout.md](docs/controls/SkUiShrinkLayout.md)).
- **Fixed:** drawn text (`SkUiLabel`, buttons, Core labels) now finds fonts registered only with MAUI's `ConfigureFonts`, on every platform. MAUI's registrar returns a PostScript name on iOS / Mac Catalyst, an asset file name on Android and an `ms-appx:` / `ms-appdata:` URI on Windows. The fallback only handled file paths, so these fonts fell back to the default font unless the app also called `SkUiFonts.Register`.

## 1.0.0-Prerelease05

- **State-change transitions** ([ControlLook.md](docs/design/ControlLook.md#state-change-transitions-fr-26)):
  - Switches slide, check boxes draw their check mark in, radio dots grow, and button and image-button presses dim or ripple from the press point (`DefaultSkUiLook.PressEffect`).
  - Slider thumbs glide to tapped values; a look can smooth `Progress` changes. The same on SkUi* and Core.
  - Looks draw every point of a transition and set each one's duration and easing: `SkUiLook.GetTransition` / `GetTransitionCore` / `TransitionProvider`, with `SkUiTransition.None` to turn one off.
  - State and events change at once; toggling back mid-way reverses from the current point. Controls animate once they have been drawn, so states set before a page appears show at once.
  - `SkUiMotion` follows the OS reduce-motion setting (`ReduceMotion` overrides it); while reduced, transitions are off.
  - **Press feedback on any control:** `ShowsPressEffect` on every `SkUiView` and Core node draws the look's press overlay over the node and its children while pressed, clipped to its rounded shape. For cards and composite buttons built from several nodes (the node needs a tap handler).
  - **Breaking:** the Switch / CheckBox / RadioButton painters, their `Draw*` entry points and `*Core` overrides take paint structs (`SkUiSwitchPaint`, `SkUiCheckBoxPaint`, `SkUiRadioButtonPaint`) that carry the transition (`SkUiToggleVisual`, `SkUiPressVisual`). `DrawPressTint` / `PressTintPainter` / `DrawPressTintCore` became `DrawPressOverlay` / `PressOverlayPainter` / `DrawPressOverlayCore` with a `SkUiPressOverlayPaint` (per-corner radii), shared by ImageButton and `ShowsPressEffect`. Buttons draw through the new `DrawButton` (`SkUiButtonPaint`). `SkUiSliderPaint.IsPressed` became `Pressed` (0–1).
  - **Fixed:** replacing `SkUiLook.Current` or `SkUiColorScheme.Current` now re-measures and redraws every live surface (before, retained pictures stayed stale until something else changed). New `SkUiLook.NotifyChanged()` for looks changed in place.
  - `SkUiRenderStatistics` also reports UI-thread animation frames (`UiFrames`, `UiAverageMilliseconds`, `UiMaxMilliseconds`); benchmarks gain `toggle-transitions` scenarios and `motionUi*` metrics.
- **Three-state toggles:** `CheckState` (`SkUiCheckState`: Unchecked / Checked / Indeterminate) and `IsThreeState` on check boxes, switches and radio buttons, SkUi* and Core. `IsChecked` stays as the two-state view for MAUI parity; `CheckStateChanged` is new.
  - **Fixed:** a tap now writes back to the bindable `IsChecked` / `CheckState` (two-way by default), so bindings see user changes; before, a tap changed only the drawn state.
  - **Breaking:** `SkUiLook.DrawSwitch` / `DrawCheckBox` / `DrawRadioButton`, their `*Core` overrides and painter delegates take `SkUiCheckState` instead of `bool`.
- **`SkUiSlider` / `SkUiCoreSlider`:** MAUI's Slider API plus `Orientation` (vertical sliders). Drags along the slider claim through the gesture arena, so drags across it still scroll the page; taps jump to the tapped value. Drawn by `SkUiLook.DrawSlider`.
- **`SkUiProgressBar` / `SkUiCoreProgressBar`:** MAUI's ProgressBar API (`Progress`, `ProgressColor`, `ProgressTo`) plus `TrackColor` and `IsIndeterminate`, whose moving segment runs on the render thread. Drawn by `SkUiLook.DrawProgressBar`.
- **Label chrome:** `SkUiLabel` / `SkUiCoreLabel` get per-corner `CornerRadii` (MAUI's `CornerRadius` type, as on `SkUiBorder`), `CornerRadius` (an `int` that sets all four corners, as MAUI Button's property), `BorderColor` and `BorderWidth` (the Core label also `FillColor`), moved down from the buttons, so a badge, chip or tab is one label instead of a label inside a border. Buttons inherit them, so they get per-corner radii too. Square labels without a border draw as before.
  - **Breaking:** `SkUiButton.CornerRadius` / `SkUiCoreButton.CornerRadius` and `SetCornerRadius` are `int` (were `double`), matching MAUI's `Button`; set fractional radii with `CornerRadii` / `SetCornerRadii`.
- **Review fixes:**
  - Toggles: `IsChecked = false` clears Indeterminate; change events and `ValueChanged` run after the bindable properties (and their bindings) are updated.
  - Slider: the requested `Value` is kept and comes back when the range widens, as in MAUI 10 (XAML order `Value` before `Minimum` / `Maximum` no longer loses it).
  - ProgressBar: `ProgressTo` completes with `false` when its animation is stopped (e.g. the page closed) instead of never completing, pauses while the bar is detached and continues once it is attached (also when called before the bar was added); `Progress` NaN becomes 0.
  - A view that gets a drawn parent now hands its local animation clock over (clients rebind first) before the rest stops.
- **Fixed:** an `SkUiAnimationClock` callback that disposed another animation, or called `StopAll`, during a tick skipped animations or threw `ArgumentOutOfRangeException` inside the frame callback.

- **Trimming and Native AOT:** the library is marked trimmable and AOT-compatible, with the trim / AOT analyzers failing its build (MAUI switches the trim analyzer off by default; the library opts back in). It no longer uses reflection. Verified with the device tests on iOS and Mac Catalyst Native AOT and on fully trimmed Android. Android Native AOT (experimental in .NET 10): GPU surfaces work; software surfaces fail in SkiaSharp's `SKCanvasView`.
  - **Breaking:** `SkUiView.OnPaintOverlay` is removed; paint overlays with `PaintOverlay` / `SetPaintOverlay` (as on Core nodes and as documented). The virtual existed only for subclasses and was found by reflection.
  - **Breaking:** custom rounded-rect geometry overrides `SkUiLook.CreateCustomRoundRectPath` (returns `null` for plain corners); `CreateRoundRectPath` is no longer virtual. The default look draws plain corners without a path, as before, now without reflection.

- **Memory leak tests** ([Testing.md](docs/design/Testing.md#memory-leak-tests)): 14 scenarios that exercise controls before closing them (clicks, re-layout, flings, gestures, animations, native overlays, surface switches), checked headless in CI and on devices with real handlers and platform views (`tests/MauiSkiaUi.DeviceTests`, `scripts/device_tests.sh`).
  - **Fixed, iOS / Mac Catalyst:** software surfaces (`HwAccelerated = false`) leaked their handler and SkiaSharp view after the page closed. Their gesture recognizers were never removed, and the gate's delegate held its view: a cycle that the view's native retain kept rooted.
  - **Fixed:** focusing a native overlay while its snapshot was shown (e.g. just after a scroll, before the restore delay) left the native view hidden; it is now restored at once. On iOS, the field that became first responder while hidden also stayed retained after its page closed.

## 1.0.0-Prerelease04

- **Retained compositor with a render thread** ([RenderingPipeline.md](docs/design/RenderingPipeline.md)): the UI thread records only the nodes whose content changed; GPU surfaces composite on a render thread. Offset, transform, opacity, clip and scroll offset are composite-time properties (no re-record).
- **Metal on iOS / Mac Catalyst** replaces the deprecated GLKView-based `SKGLView`; Android GPU surfaces render on their own GL thread.
- **Render-thread animation:** new `AnimateAsync` on `SkUiView` and Core nodes (opacity, translation, rotation, scale); `SkUiScrollView` fling / `ScrollToAsync` / `AnimateScrollTo` and activity indicators keep running while the UI thread is busy.
- **Breaking:** `ClipToBounds` is on for leaf controls but off for layouts / content hosts (MAUI parity); containers no longer paint children inside `OnPaintContent` (the engine composites them); `SkUiView.PaintChild` / `SkUiCoreNode.PaintChild` and `SkUiScrollView` picture-cache diagnostics were removed; `PaintInvalidated` fires on the invalidated node and once per frame on the root.
- Core nodes gain `Opacity`, `TranslationX/Y`, `Rotation`, `Scale`, `ClipToBounds` with transform-aware hit testing; Core arrange cache fixed.
- Text: one shared engine for `SkUiLabel` / `SkUiCoreLabel` with cached typefaces / fonts and linear wrapping.
- **Text shaping and RTL:** HarfBuzz shaping (new dependency `SkiaSharp.HarfBuzz`) for complex scripts, ligatures and emoji sequences; bidirectional text via the Unicode bidi algorithm; per-character system font fallback; RTL-aware `Start` / `End` alignment. `SkUiLabel` follows `FlowDirection`; `SkUiCoreLabel` gains `TextDirection`.
- **Text on FreeType hosts (Android, Linux):** fonts use linear (unhinted) metrics, so plain-text measurement matches HarfBuzz shaping (hinted advances differed by a fraction of a pixel per glyph). Typefaces without readable font data (Skia's empty default on a font-less Linux host) are laid out with Skia glyphs instead of throwing.
- **Text rendering modes:** `TextRendering` on `SkUiLabel` / `SkUiCoreLabel` (and buttons) plus the global `SkUiTextOptions.DefaultRendering`. `Auto` (default) takes a fast path for plain Latin text, restoring the pre-HarfBuzz first-frame cost for dense text UIs; `Shaped` always uses HarfBuzz; `Simple` never shapes. Measure-then-draw at a wider width reuses the layout.
- **Core grid:** star-column / star-row cells were measured twice per layout — first at an unresolved (zero) star size, then again after resolution — which also forced expensive zero-width text wrapping. Each cell is now measured once at its resolved size (Galaxy S9, Release, 1,000 buttons: Core layout + first frame 189.5 → 107 ms; SkUi* is 169.5 ms).
- **RTL layout:** `FlowDirection="RightToLeft"` (explicit or inherited) mirrors drawn layouts like native MAUI — grids, stacks, absolute layouts, alignment and margins, hit-testing and native overlays; horizontal scroll views start at the right; Switch / CheckBox / RadioButton and Core table chrome follow. Core nodes gain `FlowDirection`. Demo pages have a shared FlowDirection editor.
- Images decode downsampled to `SkUiImageDecoder.MaxDecodeDimension` (layout keeps source size); `SkUiFonts` picks up MAUI `ConfigureFonts` fonts and is thread-safe.
- Multi-pointer native touch and wheel / trackpad scroll on GPU surfaces.
- Requirements: FR-20 (shadows, future) and NFR-6 (threading).
- **Benchmarks** ([Benchmarks.md](docs/design/Benchmarks.md)): headless runner and a Release on-device bench app sharing one scenario catalog; `scripts/bench.sh --baseline <ref>` compares any git ref with the working tree (headless, Android, iOS, Mac Catalyst). New `SkUiView.GetRenderStatistics()` / `ResetRenderStatistics()` (render-thread frame count and cost).
- **iOS / Mac Catalyst:** commits that arrive while the render loop is idle are rendered at once instead of on the next display-link tick (commit → rendered ≈ 1 ms instead of up to a refresh period); surfaces render nothing before their first commit. Render statistics now include the GPU flush / present.
- **Drags that start on a native overlay** (Entry, Editor, WebView in a `SkUiMauiContentView`) now scroll the drawn scroll view around it (Android, iOS / Mac Catalyst).
  - **How:** the overlay's drawn ancestors see the drag; once a drawn scroll claims it, the native touch is cancelled.
  - **Native keeps:** taps, text selection and cursor placement. Controls that scroll their own content keep precedence.
  - **Windows:** touch and pen only (mouse drags keep text selection); verified on a Windows touchscreen.
- **Native overlays while scrolling (FR-16):**
  - **Clipping:** `SkUiMauiContentView` overlays are clipped to ancestor scroll viewports and clipping ancestors. They no longer draw over or take touches outside the scroller.
  - **Snapshot while scrolling:** new `ScrollMode` (`Auto` = snapshot on Android / Windows, live on Apple; `Snapshot`; `Live`). While an ancestor scroller moves, the native view is replaced by a bitmap that moves in sync with the drawn content, then restored after `SnapshotRestoreDelay`. Focused controls stay live.
  - **`SkUiScrollView.IsScrolling`.**
  - **Fix:** overlays added before their stack was placed in a scroller never registered with it, so they missed offset sync.
  - **Demo:** "Native overlays in ScrollView".
- **Fixes from the PR #8 review:**
  - A fade-in from `Opacity == 0` (`AnimateAsync`) now shows the content while it animates (#9).
  - `AnimateAsync` and animated scrolls always complete (with `false`) when the surface is disposed or the node detached before the animation ran (#10).
  - iOS / Mac Catalyst: disconnecting a surface waits for a frame being presented; a failed `SKSurface` creation retries on the next vsync (#11; verified on an iPhone).
  - `SkUiMauiContentView`: switching `ScrollMode` keeps the count of moving ancestor scrollers; completions of stale captures (async on Windows) are ignored (#12, #14).
  - `TextRendering.Simple` honours RTL `Start` / `End` alignment (#13).
  - `SkUiFonts`: a lookup before the MAUI font registrar is available is retried later instead of cached as a miss; `Unregister` clears misses (#15).
- **Windows (first validated build)** ([WindowsValidation-results.md](docs/design/WindowsValidation-results.md)):
  - GPU surfaces render: root containers are measured and arranged (they stayed 0×0), and a frame drawn at a stale size after a resize is repainted.
  - Fixed an intermittent native crash on pages with GPU surfaces (Skia called into an unloaded `opengl32.dll`).
  - Continuous frames (flings, spinners) are paced to the compositor frame and stop for unloaded surfaces; before, the app could stop responding.
  - RTL no longer mirrors surface pixels; WebView overlays snapshot through WebView2 while scrolling.
  - **Touch** (validated on a touch laptop): drawn surfaces inside a native `ScrollView` hand the drag to it at their edges (DirectManipulation); drags that start on native overlays scroll the drawn list; contacts lost or lifted elsewhere no longer leave scrolling stuck; touchpad scrolling over an Entry / Editor scrolls the list.
  - Native overlays follow ancestors that move without resizing, and re-attach when their content moves to another root (all platforms).
  - The demo's `ApplicationDisplayVersion` is numeric (`1.0.0`), as the Windows build requires.
- **Demo:** "Native nesting" page with drawn surfaces (list, carousel, swipe row, Core scroll view) inside a native MAUI `ScrollView`, with a GPU / software switch.
- **Gesture arena (breaking):**
  - **Mechanism:** one per-pointer gesture arena for SkUi* and Core, replacing per-container touch routing. It hit-tests once per press; passive nodes pass through, and disabled nodes block.
  - **Recognizers:** tap / double tap, long press, pan, swipe, pinch / rotate, and a raw pointer recognizer. There are new `SkUiView` events and commands (`DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, `SwipeDirections`, `PanAxis`, `Gestures`), the same events on Core nodes, and app-wide `SkUiGestureSettings`.
  - **Press feedback:** a button inside a scroller shows its pressed state after 100 ms unless a scroll starts, and controls inside scrollers can drag.
  - **Multi-touch:** each pointer is independent.
  - **Breaking:** `ISkUiView.Touch` / `SkUiCoreNode.Touch` are dispatch entry points and no longer virtual; use `SkUiPointerGestureRecognizer` for custom input. `SkUiTouchRouter` is removed.
- **Nested scrolling and Core scrolling:**
  - **`SkUiCoreScrollView`:** new Core scroller. SkUi* and Core scrollers share one engine (render-thread fling / tween, wheel).
  - **Nesting:** orthogonal scrollers take their own axis; same-axis inner scrollers go first and chain the remainder and the fling outward; the wheel goes to the innermost scroller that can move.
  - **Native coordination:** native ancestors are held back while a drawn gesture may claim the touch (Android `RequestDisallowInterceptTouchEvent`, iOS gate recognizer), and they take over at a drawn scroller's edge.
- **Requirements:** FR-21 (virtual / dynamic scroll layout for endless scrolling) and FR-22 (`SkUiCollectionView` with MAUI `CollectionView` parity, sticky header / footer, selection background, item tap event / command).
- **Visual tree / diagnostics:**
  - Core nodes are `IVisualTreeElement`s. Live Visual Tree and automation agents reach them through `SkUiCoreHost`, and Core add/remove raises `VisualDiagnostics.VisualTreeChanged`. This only happens when MAUI diagnostics are on (Debug builds), and the check is cached.
  - New `SkUiDiagnostics` for drawn elements: `GetRootBounds`, `GetWindowBounds`, `HitTest`, `HitTestWindow`, `SimulateTap`. It works for both SkUi\* and Core, honoring transforms, scroll offsets and RTL.
  - Core nodes gain `AutomationId` / `SetAutomationId`.
  - The demo adds a DevFlow extension `dev.skiaui` (tree, tap, hit) for drawn elements.
- **Android:** continuous render-thread animations are vsync-paced (a `TextureView` swap does not block, so they previously rendered 300–500 discarded frames/s on a Galaxy S9); now 60 fps.

## 1.0.0-Prerelease03

- Borders (`SkUiBorder` / `SkUiCoreBorder`) support MAUI-style per-corner `CornerRadius`; fill paints in Background and stroke in Overlay so content cannot cover the border.
- Look helpers (`SkUiLook` / `DefaultSkUiLook` / `SkUiChrome`) gain matching per-corner draw/path overloads while keeping uniform-radius subclass hooks.
- `SkUiCoreLabel` wraps and truncates via stock `LineBreakMode` breakers or a custom `LineBreaker` delegate (same modes as `SkUiLabel`).
- iOS GPU present: compose into a retained offscreen surface (GPU when available) and `Src`-blit the full frame to avoid WidthRequest shrink ghosts; root clear stays transparent when no solid background.
- Hardened iOS Skia surface teardown before handler disconnect; keep HW animations alive while a `ScrollView` is tracking.
- Component demo pages can toggle `HwAccelerated` on the preview host without leaving the page.

## 1.0.0-Prerelease02

- Core grid (`SkUiCoreGrid`): Auto, absolute, and star tracks, spans, spacing, and per-track min/max that clamp a star share instead of inflating it.
- Core table (`SkUiCoreTable`): row, column, and cell backgrounds plus span-aware separators on top of the same grid layout.
- Demo gallery pages for Core grid and table; stress harness builds grids and disconnects the previous tree before GC so iOS does not crash during UIView teardown.
- Dependency updates: SkiaSharp 4.152.1, CommunityToolkit.Maui 15.0.1, and current test SDK packages.

## 1.0.0-Prerelease01

- MAUI-compatible `SkUi*` controls and layouts drawn with SkiaSharp on one shared surface, with GPU acceleration when available.
- Core layer (`SkUiCore*`) for custom controls and dense UI without a MAUI `View` per node, hosted through `SkUiCoreHost`.
- Native overlays (`SkUiMauiContentView`) for Entry, Editor, WebView, and similar.
- Shared control look and color scheme; XAML, bindings, and styles on `SkUi*`.
- Demo gallery, stress harness, and headless tests.
- GitHub Actions for CI, multi-TFM NuGet pack/publish (Trusted Publishing), and demo builds.
- NuGet package id `SkiaUi.Maui`.
