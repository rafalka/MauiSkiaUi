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
- `ISkUiView : IView`, `SkUiView`, `SkUiContentView`, and a minimal overlay `SkUiLayout` implement the Phase 0 pipeline.
- `SkUiBox`, `SkUiEllipse`, and `SkUiLine` expose bindable colors and sizing and paint with SkiaSharp 4.151.1.
- Hosted children have logical MAUI parents and inherited binding contexts, but no handlers or native surfaces. Duplicate ownership and tree cycles are rejected.
- Handler-independent measure/arrange uses MAUI constraint and frame helpers, including margins, requests, alignment, and cached unchanged passes.
- Paint walks Background / Content / Overlay phases, with local rectangular clipping, opacity, translation, rotation, scale, and stable ZIndex ordering.
- `Tapped` uses child-first hit-testing, inverse transforms, single-pointer capture, a 10-DIP movement threshold, and cancellation. Passive and input-transparent nodes pass through; disabled nodes block without firing.
- `SkUiAnimationClock` accepts deterministic frame times and stops rendering when the last animation finishes. Transform animations do not invalidate measure.
- `StartUpdating` / `EndUpdating` batch layout and paint notifications. Native resources and subscriptions are released when the handler disconnects.
- `UseSkiaUi()` registers SkiaSharp and the custom standalone handler. Public library APIs include XML documentation.

### Demo (`MauiSkiaUiDemo`)

- XAML-authored composition with a box, ellipse, and line under one GPU-default `SkUiContentView`.
- Four-second transform animation starts on load and can be replayed; a native status label reports animation/idle state.
- Tapping a filled primitive changes its color and increments a native tap counter.
- A standalone software-rendered box demonstrates the leaf-control default.
- Conditional MauiDevFlow initialization and Mac Catalyst server entitlement are wired for runtime inspection.

### Phase 0 usage and limits

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

- `SkUiLayout` is deliberately an **overlay**, not Grid/Stack parity: every child receives the same local slot; margins and alignment position it. Grid/Stack managers remain later-phase work.
- `HwAccelerated` is a CLR property set **before handler creation**; changing it after attachment throws. Content hosts/layouts default to GPU; leaves default to software. Hosted values are ignored. The SkiaSharp GPU backend is platform-dependent (Metal on Mac Catalyst). There is no automatic GPU recovery: set `HwAccelerated="False"` before attachment on unsupported devices.
- All tree geometry, paint, and input use DIPs. Only the handler scales to surface pixels. `Paint` receives a local canvas; parents translate to child frames, and input routers undo the same render transform.
- Tree mutations and animation callbacks run on the UI thread. Each requested frame records the full tree into a scoped `SKPicture`; the surface replays the snapshot under a lock. This bridges Android's GL render thread safely, not a retained per-node cache. GPU `HasRenderLoop` drives animation; software schedules the next invalidation after paint. No fixed-rate timer is used.
- Animation callbacks should change paint/transform properties. Dispose their handles on page disappearance; handler unload/disconnect also stops the root clock. Start animations after attaching nodes to their intended root.
- Backgrounds support solid brushes/colors only. Custom masks, gradients, commands, double tap, long press, swipe, multi-touch, native overlays, and retained per-node caching are deferred. Hosted implementations must also be MAUI `Element` instances for logical ownership.
- Skia primitives are not individual native accessibility elements yet; native status/buttons remain available. Full drawn-tree accessibility and measured frame-rate targets are not claimed in Phase 0.

### Verification status

- 19 automated tests cover paint pixels/layers/alpha, layout/cache behavior, ownership, hit-testing/capture, invalidation, and deterministic animation.
- Diagnostic builds succeeded for Android, iOS simulator, and Mac Catalyst on 2026-09-10.
- **Device exit gate remains open:** manual Mac launch was reported, but automated visual-tree, screenshot, native input, and contrast checks were blocked by the installed MAUI extension's DevFlow injection target (`MSB4099` in `MauiDevFlow.targets`). No visual or GPU-performance success is inferred from compilation. Re-run device checks after fixing/updating that extension.

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

In VS Code, select **.NET MAUI: Select Startup Project > MauiSkiaUiDemo**, choose an Android or Apple target, and start debugging. Apply changes with Hot Reload while debugging. Copilot-triggered launches inject MauiDevFlow; ordinary builds/F5 may not.

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
