# SkiaUi

.NET MAUI library of **base controls and layouts drawn with SkiaSharp**, using GPU acceleration via `SKGLView` when available, with **native MAUI control hosting** (`SkUiMauiContentView`) for Entry, Editor, WebView, and similar.

MAUI hosts a single accelerated surface; SkiaUi visuals live in an `ISkUiView` tree (`SkUiView` / `SkUiContentView` / `SkUiLayout`) that receives measure, arrange, paint, and touch from the root host. System-backed controls (Entry, Editor, WebView) are hosted as native overlays via `SkUiMauiContentView` (see FR-16 in Requirements). The SkiaUi tree is intended to be fully authorable in XAML (e.g. `SkUiContentView` → `SkUiGrid` → `SkUiLabel` / `SkUiMauiContentView`). See [Requirements.md](Requirements.md) for the full architecture.


## Solution structure

| Project | Type | Description |
| --- | --- | --- |
| `MauiSkiaUi` | .NET MAUI class library (`net10.0-*`, plus `net10.0` for tests) | SkiaSharp-based UI controls (`SkUi*` types); intended for **NuGet** publish |
| `MauiSkiaUiDemo` | .NET MAUI application (`net10.0-*`) | Sample host used to develop and verify controls; **in-repo only** (not published) |
| `tests/MauiSkiaUi.Tests` | Headless xUnit tests (`net10.0`) | Real MAUI nodes and offscreen Skia painting; no device required |

Solution file: `SkiaUi.slnx`

## Current implementation

### Library (`MauiSkiaUi`)

- Targets Android, iOS, and Mac Catalyst (Windows TFM included when building on Windows).
- `ISkUiView : IView`, `SkUiView`, padded `SkUiContentView`, and overlay `SkUiLayout` implement the shared-surface pipeline.
- Phase 1 adds `SkUiGrid` using MAUI's `GridLayoutManager`, `SkUiLabel`, `SkUiButton`, asynchronous `SkUiImage`, and `SkUiScrollView`.
- Phase 2 adds native hosting (`SkUiMauiContentView`, FR-16), `SkUiBorder`, `SkUiActivityIndicator`, `SkUiImageButton`, `SkUiSwitch`, `SkUiCheckBox`, `SkUiRadioButton`, `SkUiVerticalStackLayout`, `SkUiHorizontalStackLayout`, and `SkUiAbsoluteLayout`. See "Phase 2 APIs" below for specifics and limits.
- `SkUiBox`, `SkUiEllipse`, and `SkUiLine` expose bindable colors and sizing and paint with SkiaSharp 4.151.1.
- Hosted children have logical MAUI parents and inherited binding contexts, but no handlers or native surfaces. Duplicate ownership and tree cycles are rejected.
- Handler-independent measure/arrange uses MAUI constraint and frame helpers, including margins, requests, alignment, and cached unchanged passes.
- Paint walks Background / Content / Overlay phases, with local rectangular clipping, opacity, translation, rotation, scale, and stable ZIndex ordering.
- `Tapped` / `TappedCommand` use child-first hit-testing, inverse transforms, single-pointer capture, a 10-DIP movement threshold, and cancellation. Buttons participate intrinsically with `Clicked`, `Command`, `CommandParameter`, `IsPressed`, and Normal/Pressed/Disabled visual states. Passive and input-transparent nodes pass through; disabled nodes block without firing.
- `SkUiAnimationClock` accepts deterministic frame times and stops rendering when the last animation finishes. Transform animations do not invalidate measure.
- `StartUpdating` / `EndUpdating` batch layout and paint notifications. Native resources and subscriptions are released when the handler disconnects.
- `UseSkiaUi()` registers SkiaSharp and the custom standalone handler. Public library APIs include XML documentation.

### Demo (`MauiSkiaUiDemo`)

- The Shell's home page is the **Components** gallery: every concrete `SkUi*` control/layout/primitive listed under **Basic controls**, **Layouts**, **Graphics**, and **Scrolling & collections** section headers, each opening its own demo page with property editors, a reset action, and (for MAUI reimplementations) a side-by-side/stacked native comparison.
- The gallery's toolbar also opens: **Composition** (a XAML control sample with Grid, wrapping Label, an offline NASA image, command-bound Buttons, MAUI styles/visual states, and scrollable content), **Stress** (1,000 hosted buttons under one scroll surface — **Scroll** animates through it, **Top** resets it, **Record** reports CPU picture-recording time and managed allocations, not GPU FPS), and **Primitives** (the original Phase 0 page: box/ellipse/line under one GPU-default `SkUiContentView`, a four-second transform animation with a native status label, tap-to-recolor, and a standalone software-rendered box).
- Conditional MauiDevFlow initialization and Mac Catalyst server entitlement are wired for runtime inspection.

### Usage and limits

Register `builder.UseSkiaUi()` in `MauiProgram`, then compose in XAML:

```xml
xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi"
```

```xml
<sk:SkUiContentView HeightRequest="200" Background="White">
	<sk:SkUiLayout>
		<sk:SkUiBox WidthRequest="80" HeightRequest="60" Color="Crimson"
					HorizontalOptions="Start" VerticalOptions="Start" Margin="16" />
		<sk:SkUiEllipse WidthRequest="60" HeightRequest="60" Color="Teal"
						HorizontalOptions="End" VerticalOptions="End" Margin="16" />
	</sk:SkUiLayout>
</sk:SkUiContentView>
```

- `SkUiLayout` is deliberately an **overlay**: every child receives the same padded slot. `SkUiGrid` delegates measurement and arrangement to MAUI's `GridLayoutManager`, including Auto/star/absolute definitions, spans, spacing, padding, and standard `Grid.Row`, `Grid.Column`, `Grid.RowSpan`, and `Grid.ColumnSpan`. Runtime definition/attached-property changes invalidate layout. Other layout packs remain later work.
- `HwAccelerated` is a CLR property set **before handler creation**; changing it after attachment throws. Content hosts/layouts default to GPU; leaves default to software. Hosted values are ignored. The SkiaSharp GPU backend is platform-dependent (Metal on Mac Catalyst). There is no automatic GPU recovery: set `HwAccelerated="False"` before attachment on unsupported devices.
- All tree geometry, paint, and input use DIPs. Only the handler scales to surface pixels. `Paint` receives a local canvas; parents translate to child frames, and input routers undo the same render transform.
- Tree mutations and animation callbacks run on the UI thread. Each requested frame records the full tree into a scoped `SKPicture`; the surface replays the snapshot under a lock. This bridges Android's GL render thread safely, not a retained per-node cache. GPU `HasRenderLoop` drives animation; software schedules the next invalidation after paint. No fixed-rate timer is used.
- The handler shares an internal, headless-tested `SkUiFrameRenderer` for recording, replay, density mapping, and cleanup. Invalidation raised during painting queues one follow-up frame even when animation is idle; animation changes applied before painting are included in the current frame.
- Animation callbacks should change paint/transform properties. Dispose their handles on page disappearance; handler unload/disconnect also stops the root clock. Start animations after attaching nodes to their intended root.
- `StopAll()` preserves the clock's monotonic timeline. After restarting an animation on the same clock, continue supplying elapsed timestamps rather than resetting them to zero.
- Hit regions are rectangular arranged bounds, including the visually empty corners of ellipses and the area beside a line. Render transforms are inverted before testing these bounds; shape-aware hits are deferred. Disabled nodes block hits without firing, and `IsVisible="False"` collapses nodes and excludes them from paint/input.
- Backgrounds support solid brushes/colors only. Custom masks, gradients, double tap, long press, swipe, multi-touch, native overlays, and retained per-node caching are deferred. Hosted implementations must also be MAUI `Element` instances for logical ownership.
- Drawn controls are not individual native accessibility elements and do not yet provide keyboard activation. Native status/navigation controls remain available. Full drawn-tree accessibility and device frame-rate targets are not claimed.

### Phase 1 APIs

```xml
<sk:SkUiContentView Background="White">
	<sk:SkUiScrollView Padding="16">
		<sk:SkUiGrid RowDefinitions="Auto,Auto,Auto" RowSpacing="12">
			<sk:SkUiLabel Text="Observations" FontSize="24" />
			<sk:SkUiImage Grid.Row="1" Source="earth.jpg" HeightRequest="200" />
			<sk:SkUiButton Grid.Row="2" Text="Add observation" Command="{Binding AddCommand}" />
		</sk:SkUiGrid>
	</sk:SkUiScrollView>
</sk:SkUiContentView>
```

- New control-owned bindable properties delegate to fluent direct setters (`SetText`, `SetSource`, `SetContent`, `SetPadding`, etc.). **Direct setters do not write back to the bindable store or bindings.** CLR getters return applied state; `GetValue` may still return the old value. Setting a bindable value to its already-stored value may not invoke its callback, so do not mix both update paths for one property. Both paths honor `StartUpdating` / `EndUpdating`; these batch invalidation, not rollback or asynchronous loading.
- Label supports plain left-to-right text, grapheme-safe word/character wrapping, head/middle/tail truncation, font size, system family names, bold/italic, alignment, and padding. MAUI registered font aliases, rich text, bidi/complex-script shaping, selection, and native font scaling are not implemented.
- Button adds intrinsic taps, command eligibility, rounded fill/border chrome shared with its content clip (`SkUiChrome`, so glyphs never bleed past the corners; hit bounds remain rectangular), and visual states through MAUI `VisualStateManager`. `FillColor` controls its default fill; a solid `Background` overrides it. Setting both `Command` and the inherited `TappedCommand` runs only `Command` on a tap (the shared `Tapped` event still fires for both).
- Image accepts `FileImageSource` (absolute file or **Resources/Raw** package asset), `StreamImageSource`, and HTTPS `UriImageSource`. Generated `MauiImage` resource lookup and `FontImageSource` are not implemented. `AspectFit`, `AspectFill`, and `Fill` control painting; intrinsic size is decoded pixels as DIPs. No shared download cache, EXIF rotation, or animated image playback is provided.
- Image loading exposes `IsLoading`, `LoadError`, `ImageSize`, and awaitable `LoadingTask`; errors leave a blank image. Replacement cancels old work and rejects stale results. Encoded data is limited to 32 MiB and decoded size to 16 megapixels. Source streams are owned/disposed by the loader. The decode continuation is marshaled back to the thread that started loading (via a handler-independent dispatcher capture), so hosted (handlerless) images stay correct even though the actual decode runs on a thread-pool thread. Use setters and `Dispose()` on the UI thread; dispose images when permanently removing them.
- ScrollView measures its single content unconstrained on the enabled axes (`Vertical`, `Horizontal`, `Both`, `Neither`). `ContentSize` includes padding. `ScrollX`/`ScrollY` are read-only, clamped DIPs. Offset changes reposition content without remeasuring it. `ScrollTo`, `ScrollToAsync`, `AnimateScrollTo`, and `Scrolled` provide programmatic access; gesture/unload/disable interrupts animation and cancels pending async scrolls.
- Pan takes over after 10 DIPs and cancels the child's pending tap. A bounded decelerating fling uses the shared root clock, stops at bounds/rest, and is interrupted by a new press. A desktop wheel scrolls the vertical axis whenever it is enabled (`Vertical` or `Both`), and only scrolls horizontally when `Horizontal` is the sole enabled axis — `SkUiTouchEvent` carries a single `WheelDelta` with no axis indicator, so `Both` cannot yet distinguish a horizontal-wheel gesture (e.g. shift+wheel) from a vertical one. Nested-scroll arbitration, bounce, snapping, scrollbars, native overlays, and virtualization remain deferred. Native MAUI scroller nesting is compatibility-only; prefer one SkiaUi scroll surface.
- Painting conservatively rejects nodes outside the transformed canvas clip and caches stable Z-order until collection/ZIndex changes. All controls remain retained; this is paint culling, not item virtualization.

### Phase 2 APIs

```xml
<sk:SkUiGrid RowDefinitions="Auto,Auto" RowSpacing="12">
	<sk:SkUiBorder Stroke="#087F83" StrokeThickness="2" CornerRadius="10">
		<sk:SkUiLabel Text="Bordered content" />
	</sk:SkUiBorder>
	<sk:SkUiMauiContentView Grid.Row="1" HeightRequest="160">
		<Editor Placeholder="Native Editor hosted natively" />
	</sk:SkUiMauiContentView>
</sk:SkUiGrid>
```

- **`SkUiMauiContentView`** (FR-16) hosts a real MAUI `VisualElement` (e.g. `Entry`, `Editor`, `WebView`) as a native overlay instead of a Skia reimplementation. Internally, the standalone root's handler now wraps its Skia surface in a small per-platform native container (`SkUiOverlayContainer`) so overlay views can be added as absolutely positioned siblings (Android `View.Layout`, iOS/Mac Catalyst `UIView.Frame`, Windows `Canvas`). Positioning uses `ComputeRootRelativeFrame()`, which sums this node's and every ancestor's `Frame` offset plus `TranslationX`/`TranslationY` up to the standalone root. **v1 limits:** rotation/scale/opacity are not composed through ancestors, there is no snapshot-during-scroll (the overlay stays live-synced while scrolling), and `Touch` always returns `false` so SkiaUi's router never consumes hits meant for the native control. See `MauiContentViewDemoPage` in the gallery for a working Editor/WebView example, combined in one `SkUiGrid` with `SkUiLabel`/`SkUiButton` — editing the Editor's HTML live-updates the WebView.
- **`SkUiBorder`** draws a rounded-rectangle fill/border/clip around one child, sharing `SkUiChrome` geometry with Button. Fill falls back from `Background` (`SolidColorBrush`) to `BackgroundColor` like `SkUiView`. Only rounded rectangles are supported (no arbitrary `IShape` strokes like MAUI's full `Border.StrokeShape`).
- **`SkUiActivityIndicator`** is a drawn indeterminate spinner driven by the shared `AnimationClock`; animates only while `IsRunning`, and stops automatically if hidden or removed from its tree (so a detached spinner cannot keep ticking).
- **`SkUiImageButton`** extends `SkUiImage` with intrinsic taps, `Command`/`CommandParameter`/`Clicked`, and a pressed/disabled tint overlay (optionally clipped to `CornerRadius`).
- **`SkUiSwitch`**, **`SkUiCheckBox`**, **`SkUiRadioButton`** share a small `SkUiToggleControl` base (`IsChecked`, `CheckedChanged`, intrinsic tap-to-toggle). **`SkUiRadioButton` overrides this to only select, never uncheck, on a tap** (matching MAUI's `RadioButton`). `SkUiRadioButton.GroupName` is exposed for app bookkeeping only — **unlike MAUI's `RadioButton`, it does not automatically uncheck siblings**; apps must clear other radio buttons themselves (e.g. in `CheckedChanged`).
- **`SkUiVerticalStackLayout`** / **`SkUiHorizontalStackLayout`** reuse MAUI's `VerticalStackLayoutManager`/`HorizontalStackLayoutManager` (just a `Spacing` property beyond the shared `SkUiLayout` padding/Children). **`SkUiAbsoluteLayout`** reuses `AbsoluteLayoutManager` and delegates to MAUI's `AbsoluteLayout.GetLayoutBounds`/`SetLayoutBounds`/`GetLayoutFlags`/`SetLayoutFlags` attached properties (same delegation pattern `SkUiGrid` uses for `Grid.Row`/`Column`). **`SkUiFlexLayout` is not implemented** (deferred for scope).

### Verification status

- **70 automated tests pass**: the 62 Phase 0/Phase 1/review cases plus `Phase2Tests` covering `SkUiMauiContentView`'s Measure/Arrange/Touch contract, content-ownership validation, `ComputeRootRelativeFrame()` through nested hosted layouts and ancestor/own translation, `SkUiRadioButton`'s select-only tap, `SkUiBorder`'s `BackgroundColor` fallback, and `SkUiActivityIndicator` stopping its clock on detach. The remaining new Basic/Layout controls are covered via `ComponentDemoTests`' one-page-per-component contract (editors, reset, property checks, no handlers) rather than dedicated pixel goldens yet.
- **Phase 2 (2026-09-10):** implemented FR-16 native hosting (`SkUiMauiContentView` + per-platform overlay container) and nine new controls/layouts (`SkUiBorder`, `SkUiActivityIndicator`, `SkUiImageButton`, `SkUiSwitch`, `SkUiCheckBox`, `SkUiRadioButton`, `SkUiVerticalStackLayout`, `SkUiHorizontalStackLayout`, `SkUiAbsoluteLayout`), each with a gallery demo page (native side-by-side where MAUI has a direct counterpart). Android/iOS/Mac Catalyst diagnostic builds pass with 0 warnings. `SkUiFlexLayout` and per-control markdown docs (NFR-5) are not done this pass.
- A same-day Phase 2 review found and fixed a High-severity issue (`SkUiRadioButton` unchecked itself on a second tap instead of only selecting, unlike MAUI's `RadioButton`) and two Medium issues (`SkUiBorder` ignored `BackgroundColor` when `Background` wasn't an explicit brush; `SkUiActivityIndicator` could keep its animation clock running after being removed from its tree). `ComputeRootRelativeFrame()` was also extended to include `TranslationX`/`TranslationY` (previously Frame-offset only), correcting the documented v1 overlay-position limit. See `tmp/review.md` for the full findings; fixes are covered by new regression tests.
- A follow-up review on 2026-09-10 fixed a High-severity issue (SkUiImage's decode completion could mutate state off the UI thread) and three Medium issues (Button could run both `Command` and `TappedCommand` on one tap; Button text was not clipped to its rounded background, so glyphs could bleed past the corners; Grid row/column/span invalidation matched MAUI's attached-property names as literal strings). See `tmp/review.md` for the full findings; fixes are covered by new regression tests.
- A 1,000-label, 400x600-DIP headless Debug measurement improved warm CPU recording from **8.106 ms / 568,384 managed bytes per frame** to **0.635 ms / 9,488 bytes** after clip rejection and cached ordering (30 frames, same Mac). These are indicative single-run measurements, not device FPS or release performance guarantees. Native allocations and initial layout costs are not included in the per-frame allocation figure; near-zero allocation remains future work.
- **Device exit gate remains open:** the installed MAUI extension's DevFlow injection target still fails with `MSB4099`, blocking Copilot-triggered launch; the extension was not modified. Native overlay attachment/positioning (`SkUiMauiContentView`), rendered colors/contrast of the new controls, and all other on-device behavior remain unverified. No visual or GPU-performance success is inferred from compilation. Headless tests do not replace these checks.

### Demo asset

The bundled `earth.jpg` is NASA's Blue Marble western hemisphere image, downloaded from [NASA Visible Earth](https://eoimages.gsfc.nasa.gov/images/imagerecords/57000/57723/globe_west_2048.jpg). Credit: NASA / Visible Earth. It is packaged under `Resources/Raw` so the demo works offline.

### Tooling

- Git repository initialized at the solution root.
- `.gitignore` covers .NET/MAUI build outputs, IDE files, and OS artifacts.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- .NET MAUI workload (`dotnet workload install maui`)
- Platform SDKs for the targets you build (Android SDK, Xcode for iOS/Mac Catalyst, etc.)

## Build and run

```bash
# Restore and build the solution
dotnet build SkiaUi.slnx

# Run headless mechanism tests
dotnet test tests/MauiSkiaUi.Tests/MauiSkiaUi.Tests.csproj
```

In VS Code, select **.NET MAUI: Select Startup Project > MauiSkiaUiDemo**, choose an Android or Apple target, and start debugging. Apply changes with Hot Reload while debugging. The demo includes MauiDevFlow by default in **Debug**, including ordinary builds/F5: the project references the agent and defines `MAUI_DEVFLOW` to activate the existing startup registration. **Release** excludes both the agent package and registration. No extension injection is needed for ordinary Debug builds; Copilot-triggered launch remains subject to the installed extension's injection-target error described above.

## Documentation

- **README.md** (this file) — what exists today and how to build it.
- **[Requirements.md](Requirements.md)** — goals, architecture, XAML model, backlog, and local reference checkouts (MAUI, Open-Maui, Flutter, Avalonia, Uno, DrawnUi, SkiaSharp).
- **[ImplementationPlan.md](ImplementationPlan.md)** — phased delivery (PoC → initial → full gallery/docs → extensions).
- **[LayoutSystem.md](LayoutSystem.md)** — MAUI-based measure/arrange, hosted vs standalone modes, layout-manager reuse, and implementation checklist (FR-3 / FR-3a / FR-13).
- **[DrawingMechanism.md](DrawingMechanism.md)** — paint pipeline, Background/Content/Overlay layers, clip/mask, transparency-aware caching, and implementation checklist (FR-8 / FR-9 / FR-11).
- **[EventMechanism.md](EventMechanism.md)** — SkiaUi-owned gesture / event design (tap, double tap, long press, swipe), participation rules, and implementation checklist (FR-15).
- **[AnimationMechanism.md](AnimationMechanism.md)** — vsync-driven ~60 fps clock, paint / render-transform / optional layout animation tiers, and implementation checklist (FR-7).
- **[ScrollingAndCollectionViews.md](ScrollingAndCollectionViews.md)** — custom `SkUiScrollView` / virtualizing collection design (vs MAUI ScrollView/CollectionView), and implementation checklist.
- **[Testing.md](Testing.md)** — unit / mechanism / golden / device test strategy; automation and AI-assisted visual review; how peer frameworks test painting and layout.
- **Per-control docs (NFR-5):** each public `SkUi*` control/layout gets its own `.md` (how it works / how to use). For MAUI reimplementations, link to official MAUI docs and document only SkiaUi differences and extensions. (Folder layout TBD as controls land.)

## License

Not specified yet.
