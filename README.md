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
- `SkUiBox`, `SkUiEllipse`, and `SkUiLine` expose bindable colors and sizing and paint with SkiaSharp 4.151.1.
- Hosted children have logical MAUI parents and inherited binding contexts, but no handlers or native surfaces. Duplicate ownership and tree cycles are rejected.
- Handler-independent measure/arrange uses MAUI constraint and frame helpers, including margins, requests, alignment, and cached unchanged passes.
- Paint walks Background / Content / Overlay phases, with local rectangular clipping, opacity, translation, rotation, scale, and stable ZIndex ordering.
- `Tapped` / `TappedCommand` use child-first hit-testing, inverse transforms, single-pointer capture, a 10-DIP movement threshold, and cancellation. Buttons participate intrinsically with `Clicked`, `Command`, `CommandParameter`, `IsPressed`, and Normal/Pressed/Disabled visual states. Passive and input-transparent nodes pass through; disabled nodes block without firing.
- `SkUiAnimationClock` accepts deterministic frame times and stops rendering when the last animation finishes. Transform animations do not invalidate measure.
- `StartUpdating` / `EndUpdating` batch layout and paint notifications. Native resources and subscriptions are released when the handler disconnects.
- `UseSkiaUi()` registers SkiaSharp and the custom standalone handler. Public library APIs include XML documentation.

### Demo (`MauiSkiaUiDemo`)

- The first screen is a XAML control sample with Grid, wrapping Label, an offline NASA image, command-bound Buttons, MAUI styles/visual states, and scrollable content.
- **Stress test** opens 1,000 hosted buttons under one scroll surface. **Scroll** animates through the content, **Top** resets it, and **Record** reports CPU picture-recording time and managed allocations (not GPU FPS).
- **Primitives** opens the original Phase 0 page:
- XAML-authored composition with a box, ellipse, and line under one GPU-default `SkUiContentView`.
- Four-second transform animation starts on load and can be replayed; a native status label reports animation/idle state.
- Tapping a filled primitive changes its color and increments a native tap counter.
- A standalone software-rendered box demonstrates the leaf-control default.
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
- Button adds intrinsic taps, command eligibility, rounded fill/border chrome, and visual states through MAUI `VisualStateManager`. `FillColor` controls its default fill; a solid `Background` overrides it. Hit bounds remain rectangular.
- Image accepts `FileImageSource` (absolute file or **Resources/Raw** package asset), `StreamImageSource`, and HTTPS `UriImageSource`. Generated `MauiImage` resource lookup and `FontImageSource` are not implemented. `AspectFit`, `AspectFill`, and `Fill` control painting; intrinsic size is decoded pixels as DIPs. No shared download cache, EXIF rotation, or animated image playback is provided.
- Image loading exposes `IsLoading`, `LoadError`, `ImageSize`, and awaitable `LoadingTask`; errors leave a blank image. Replacement cancels old work and rejects stale results. Encoded data is limited to 32 MiB and decoded size to 16 megapixels. Source streams are owned/disposed by the loader. Use setters and `Dispose()` on the UI thread; dispose images when permanently removing them.
- ScrollView measures its single content unconstrained on the enabled axes (`Vertical`, `Horizontal`, `Both`, `Neither`). `ContentSize` includes padding. `ScrollX`/`ScrollY` are read-only, clamped DIPs. Offset changes reposition content without remeasuring it. `ScrollTo`, `ScrollToAsync`, `AnimateScrollTo`, and `Scrolled` provide programmatic access; gesture/unload/disable interrupts animation and cancels pending async scrolls.
- Pan takes over after 10 DIPs and cancels the child's pending tap. A bounded decelerating fling uses the shared root clock, stops at bounds/rest, and is interrupted by a new press. Desktop wheel events use the vertical axis, or horizontal axis for horizontal-only scroll. Nested-scroll arbitration, bounce, snapping, scrollbars, native overlays, and virtualization remain deferred. Native MAUI scroller nesting is compatibility-only; prefer one SkiaUi scroll surface.
- Painting conservatively rejects nodes outside the transformed canvas clip and caches stable Z-order until collection/ZIndex changes. All controls remain retained; this is paint culling, not item virtualization.

### Verification status

- **49 automated tests pass**, including the 36 Phase 0/review cases, Phase 1 controls, styles/visual-state restoration, image replacement/errors, scroll pan/fling/cancellation, full-pixel composition goldens at 1x/2x, and culling/order-cache regressions.
- Phase 1 diagnostic builds, including source-generated XAML, succeeded for Android, iOS simulator, and Mac Catalyst on 2026-09-10. Windows has not been built here.
- A 1,000-label, 400x600-DIP headless Debug measurement improved warm CPU recording from **8.106 ms / 568,384 managed bytes per frame** to **0.635 ms / 9,488 bytes** after clip rejection and cached ordering (30 frames, same Mac). These are indicative single-run measurements, not device FPS or release performance guarantees. Native allocations and initial layout costs are not included in the per-frame allocation figure; near-zero allocation remains future work.
- **Device exit gate remains open:** manual Mac launch was reported, but automated visual-tree, screenshot, native input, and contrast checks were blocked by the installed MAUI extension's DevFlow injection target (`MSB4099` in `MauiDevFlow.targets`). No visual or GPU-performance success is inferred from compilation. Re-run device checks after fixing/updating that extension.
- Latest Phase 1 attempt: no active debug session; Copilot launch again failed on the installed injection target and diagnostics reported zero agents. The extension was not modified. Functional navigation, image presentation, native pan/wheel/fling, compact layouts, GPU behavior, and rendered color contrast remain unverified. Headless tests do not replace these checks.

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
