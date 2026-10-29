# SkiaUi architecture

How SkiaUi is built today: the trees, how a frame is produced, and where each mechanism lives. Each section links the design doc with the details. The decisions that led here, the reviews and the 1.0 delivery plan are in [docs/archive/](../archive/README.md).

## One surface, many drawn views

A MAUI page normally creates one platform view per control. SkiaUi draws a region of the page on **one Skia surface**: the outermost SkiaUi view under a MAUI parent (usually a `SkUiContentView` or a `SkUi*` layout) is the **surface root**. It is the only drawn view with a MAUI handler (`SkUiViewHandler`) and a platform view. Everything nested in it is a **hosted** node: it has a logical MAUI parent (binding context, resources, XAML ownership) but no handler, and it measures, draws and takes input through the root.

```
MAUI page
└── SkUiContentView                      surface root: SkUiViewHandler + platform surface
    │                                    (SkUiOverlayContainer: Skia surface + native overlays)
    └── SkUiScrollView                   hosted
        └── SkUiGrid                     hosted
            ├── SkUiLabel                hosted, drawn into the root's surface
            ├── SkUiCoreHost             hosted; hosts a Core tree
            │   └── SkUiCoreVerticalStackLayout → SkUiCoreLabel, …
            └── SkUiMauiContentView      hosted placeholder; its MAUI control is a native overlay
                └── WebView
```

`HwAccelerated` (a plain CLR property, read when the handler creates the platform view) picks a GPU surface (Metal on iOS / Mac Catalyst, a GL thread on Android, `SKGLView` on Windows) or a software one. Content hosts and layouts default to the GPU; leaf controls used directly in a MAUI page default to software, so many standalone controls do not create many GPU surfaces. Hosted nodes ignore it.

## Two layers, one tree

| Layer | Types | For |
| --- | --- | --- |
| **SkUi\*** | `SkUiView` (a MAUI `View`) and its subclasses: `SkUiLabel`, `SkUiGrid`, … | XAML pages. Bindable properties, styles and visual states, MAUI's layout managers, MAUI's names so a page ports by changing the XAML prefix |
| **Core** | `SkUiCoreNode` (a plain CLR object) and `SkUiCore*` | Dense trees and app-made controls built in code. Fluent `Set*` plus `INotifyPropertyChanged`, no `BindableObject`; layouts of their own |

`SkUiCoreHost` hosts a Core tree inside the SkUi tree. Most controls exist on both layers and share one engine (text, shapes, toggles, scrolling, images, pickers, text input), so both layers draw the same pixels; parity tests compare them. Both layers live in the one `SkiaUi.Maui` assembly and package; Core types use no MAUI Controls types. Details: [docs/controls/SkUiCore.md](../controls/SkUiCore.md).

**Property pattern (SkUi\*).** Each `BindableProperty` validates with `SkUiValidate` (invalid values are ignored with a warning, as MAUI does); a fluent `SetX` throws on invalid input and assigns the property; the change callback copies the value into a field that layout and paint read. Change callbacks can run before the constructor body (MAUI applies app-level implicit styles inside the base `View` constructor), so their state comes from field initializers or is created on first use. Core's `SetX` validates, calls `SetProperty` and invalidates.

## Layout

Drawn views use **MAUI's measure / arrange contract**, not Flutter-style constraints. `SkUiView` overrides `MeasureOverride` / `ArrangeOverride` so they work without a handler, which lets MAUI's own layout managers (`GridLayoutManager`, the stack and absolute managers, `FlexLayoutManager` over a ported flex engine) lay out drawn children. Layouts MAUI does not have (wrap, shrink stacks, align stack) use one allocation-free engine per algorithm, shared by both layers. Measure results are cached per constraint; an arrange that only moves a node changes its composite-time offset and records nothing. Right-to-left is mirrored by SkiaUi at arrange time, since hosted nodes have no native view for MAUI to mirror. All geometry is in DIPs; only the handler maps to pixels.

Details: [LayoutSystem.md](LayoutSystem.md).

## Drawing and the render pipeline

Rendering is **retained** and split across two threads ([RenderingPipeline.md](RenderingPipeline.md)):

- **UI thread:** tree changes, layout, and *recording* each node whose content changed into its own `SKPicture` (`SkUiRenderRecorder`), plus the composite-time properties of each node (`SkUiRenderProps`: offset, transforms, opacity, clip paths, shadow, scroll offset, …). Both layers implement `ISkUiRenderable`; invalidation (`SkUiRenderDirty`: Content / Props / Children) is propagated once per frame to the root. A frame is committed as an `SkUiRenderBatch`.
- **Render thread:** `SkUiCompositor` applies batches and composites the retained tree: the shared Metal render thread on Apple, the view's GL thread on Android, the UI thread for software surfaces and on Windows. Nothing on the render thread touches MAUI objects.

`SkUiFrameRenderer` drives both halves; headless tests and benchmarks use it directly. An immediate painter (`SkUiImmediatePainter`) draws the same phases from live objects for snapshots and tests, and must match the compositor pixel for pixel.

**Paint phases.** A node draws its own chrome in three phases on the same instance: Background (`PaintBackground` / `OnPaintBackground`), Content (`OnPaintContent`), and Overlay (`PaintOverlay`); the compositor draws children between Content and Overlay. Containers express scrolling, clipping and pinned children through render properties instead of drawing children themselves.

**Clips and hit-testing.** `ClipToBounds` is on for leaf controls and off for layouts and content hosts (MAUI's `IsClippedToBounds`), so children and shadows can overflow. Clips (`ClipToBounds`, MAUI's `Clip`, a border's shape) affect painting only; hit-testing uses arranged rectangles, mapped through inverse render transforms. Shadows and clips are render properties resolved by `SkUiVisualEffects`: changing them records nothing.

**Native object ownership.** Objects committed in `SkUiRenderProps` (clip paths, shadow outlines and styles, drawables) are disposed by the compositor when a commit replaces them or the render node goes; the UI side never disposes them. When a subtree leaves its drawn parent or the surface is torn down (`SkUiRenderInvalidation.ResetSubtree` → `ISkUiRenderable.ReleaseDrawingResources`), nodes forget them and dispose what only recording used (text blobs, rounded clips, border outlines). Fonts and the text paint are shared (`SkUiTextResources`); nothing is left to the finalizer.

## Animation

| Tier | API | Thread | Costs |
| --- | --- | --- | --- |
| Composite-time | `AnimateAsync` on both layers (opacity, translation, rotation, scale), flings and animated scrolls, spinners, content slides | Render | Nothing recorded per frame; keeps running while the UI thread is busy |
| Clock-driven drawing | `SkUiRenderDrawable` on a node (a shimmer's wave) | Render | Drawn by the compositor from the shared clock (`SkUiRenderClock`); the engine knows no control |
| UI clock | `SkUiAnimationClock` callbacks (any property) and state-change transitions | UI (vsync ticker) | Re-records the animated node each frame |

Frames are requested only while something animates. Animations use time, not frame counts, so 90 and 120 Hz displays run at the same speed. Values that render-thread animations reach are reported back to the UI thread; a value the UI sets explicitly cancels the animation on that property.

**State-change transitions** (a switch's thumb, a check mark, a press's dim or ripple, a slider's thumb) are drawn by the look from a transition state in the control's paint struct (`SkUiTransitionKind`; animators shared by both layers: `SkUiToggleAnimator`, `SkUiPressAnimator`, `SkUiTween`); they run on the UI clock and re-record only the animating control. `SkUiMotion` follows the OS's reduce-motion setting, and every transition and render-thread effect honors it. Look side: [ControlLook.md](ControlLook.md#state-change-transitions-fr-26).

## Input

One **gesture arena per pointer** (`MauiSkiaUi/Gestures`) for both layers: hit-testing child first through inverse transforms, recognizers (tap, double tap, long press, pan, swipe, pinch, pointer, hover) competing for the pointer, a touch slop, and cancellation when a scroller claims a drag. Native ancestors are coordinated at the surface: native scrollers, and on iOS / Mac Catalyst their tap and long-press recognizers. MAUI's `TapGestureRecognizer` runs on drawn views through the arena; other MAUI gesture recognizers on drawn views are reported, not run. Details: [EventMechanism.md](EventMechanism.md).

## Scrolling and lists

One scroll engine for both layers: render-thread fling and animated scroll, nesting and chaining, overscroll (bounce, stretch), snap points, scroll bars. Virtual lists realize items near the viewport and recycle them per template; `SkUiCollectionView` and `SkUiCarouselView` build on them. Details: [ScrollingAndCollectionViews.md](ScrollingAndCollectionViews.md).

## Text

One engine for both label layers ([SkUiLabel.md](../controls/SkUiLabel.md)): `SkUiTextLayout` (HarfBuzz shaping, plain Latin text as one run, bidi, per-character font fallback, wrapping and truncation, custom line breakers, lines cached by style and width), `SkUiRichTextLayout` (spans, and HTML through SkiaUi's own parser), `SkUiShaping`, and `SkUiTextFit` (shrink, tighten or grow text to fit). Buttons derive from the labels.

**Text input** is drawn too: one editing engine and field model for `SkUiEntry`, `SkUiEditor`, `SkUiSearchBar` and their Core twins, with the platform keyboard, input methods, autocorrect, dictation, clipboard and edit menu reached through one proxy per surface (an `InputConnection` on Android, a hidden `UITextInput` view on Apple, a hidden `TextBox` on Windows). Details: [TextInput.md](TextInput.md).

## Look and colors

- **Control look** (`SkUiLook`, `DefaultSkUiLook` and the platform looks): how controls are drawn, their default sizes and their motion. Controls pass resolved colors and state in a paint struct; the look draws. A look is set app-wide (`SkUiLook.Current`) or per tree (`Look` on a view or node). Details: [ControlLook.md](ControlLook.md).
- **Color scheme** (`SkUiColorScheme`, light / dark and platform schemes): the default palette both layers use where a control's color is not set. Details: [ColorScheme.md](ColorScheme.md).
- **MAUI styles and visual states** set individual SkUi\* properties, as on MAUI controls. Core has no styles; it uses the look, the scheme and `Set*`.

## Images

One loader and cache for both layers (`MauiSkiaUi/Imaging`): MAUI image sources (`MauiImage` files by density, `FontImageSource`, HTTP(S), streams), decoding off the UI thread, a decoded-image memory LRU with leases shared by every view that shows an image, and a download disk cache. Details: [SkUiImage.md](../controls/SkUiImage.md).

## Native controls inside a surface

`SkUiMauiContentView` is a hosted placeholder that measures its MAUI `VisualElement` and shows it as a **native overlay** above the Skia surface (the root's `SkUiOverlayContainer`), positioned and clipped to the drawn tree. While an ancestor scrolls, Android and Windows draw a snapshot of the overlay so it moves with the drawn content; Apple keeps it live. Native views get native input; drags a drawn scroller claims are handed over. Details: [SkUiMauiContentView.md](../controls/SkUiMauiContentView.md).

## Popups

SkiaUi ships no pages or popup windows. Drawn controls that need a popup (the pickers) ask the app's popup system through `ISkUiPopupPresenter`, with a request that carries the anchor, its bounds through every drawn transform, a placement and ready content. Details: [Popups.md](Popups.md).

## Accessibility and localization

Each surface builds a **semantics tree** lazily from the render tree (both layers) and MAUI's `SemanticProperties` / `AutomationProperties`, and bridges it to TalkBack, VoiceOver and Narrator; keyboard focus and OS font scaling are handled by SkiaUi too. Details: [Accessibility.md](Accessibility.md). SkiaUi's own texts (button texts, screen-reader states) are in `SkUiStrings`: [Localization.md](Localization.md).

## Shared mechanisms

- **Weak subscriptions:** a view listens to sources it does not own and that may outlive it (brushes, geometries, image sources, commands) through `SkUiWeakListener<T>`; app-wide events are `SkUiWeakEvent`s.
- **Fixed event args:** `PropertyChangedEventArgs` and reset args that never change are `static readonly`, not allocated per raise.
- **Diagnostics:** Core nodes appear in Visual Studio's Live Visual Tree; `SkUiDiagnostics` reads bounds, hit-tests and simulates taps (also used by the DevFlow `dev.skiaui` extension); `SkUiRenderStatistics` and frame traces measure frames ([Benchmarks.md](Benchmarks.md)).
- **Trimming and Native AOT:** the library is `IsAotCompatible`; trim and AOT analyzer warnings fail its build.

## Source map

| Folder of `MauiSkiaUi/` | Holds |
| --- | --- |
| *(root)* | `SkUiView`, `ISkUiView`, `SkUiViewHandler`, shared element interfaces |
| `Controls/` | SkUi\* controls: `Basic`, `Graphics`, `Layouts`, `Scrolling`, `Virtualization` |
| `Core/` | `SkUiCoreNode` and the Core controls and layouts |
| `Rendering/`, `Surfaces/` | Render nodes, compositor, render clock and animations; Metal and GL surfaces, vsync |
| `Gestures/` | Gesture arena, recognizers, pointer routing |
| `Helpers/` | Shared engines (text, layout, shapes, toggles, transitions, scrolling), animation clock, weak listeners |
| `Look/`, `ColorScheme/` | Looks and color schemes |
| `TextInput/` | Editing engine, field models, platform input proxies |
| `Imaging/` | Image loading, decoding and caches |
| `Popups/` | Popup presenter contract, requests, drawn popup content |
| `Accessibility/` | Semantics tree, platform bridges, focus, font scaling |
| `Localization/`, `Extensions/` | `SkUiStrings`; `UseSkiaUi()` and its options |

The solution's other projects (demo, samples, tests, benchmarks, the toolkit package) are listed in [Development.md](../../Development.md#solution-structure).
