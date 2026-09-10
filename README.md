# SkiaUi

.NET MAUI library of **base controls and layouts drawn with SkiaSharp**, using GPU acceleration via `SKGLView` when available, with **native MAUI control hosting** (`SkUiMauiContentView`) for Entry, Editor, WebView, and similar.

MAUI hosts a single accelerated surface; SkiaUi visuals live in an `ISkUiView` tree (`SkUiView` / `SkUiContentView` / `SkUiLayout`) that receives measure, arrange, paint, and touch from the root host. System-backed controls (Entry, Editor, WebView) are hosted as native overlays via `SkUiMauiContentView` (see FR-16 in Requirements). The SkiaUi tree is intended to be fully authorable in XAML (e.g. `SkUiContentView` → `SkUiGrid` → `SkUiLabel` / `SkUiMauiContentView`). See [Requirements.md](Requirements.md) for the full architecture.


## Solution structure

| Project | Type | Description |
| --- | --- | --- |
| `MauiSkiaUi` | .NET MAUI class library (`net10.0-*`) | SkiaSharp-based UI controls (`SkUi*` types); intended for **NuGet** publish |
| `MauiSkiaUiDemo` | .NET MAUI application (`net10.0-*`) | Sample host used to develop and verify controls; **in-repo only** (not published) |

Solution file: `SkiaUi.slnx`

## Current implementation

### Library (`MauiSkiaUi`)

- Targets Android, iOS, and Mac Catalyst (Windows TFM included when building on Windows).
- References `Microsoft.Maui.Controls` and `SkiaSharp.Views.Maui.Controls`.
- Placeholder type `Class1` is present from the project template; no custom controls are implemented yet.

### Demo (`MauiSkiaUiDemo`)

- Standard .NET 10 MAUI single-project app template.
- Project reference to `MauiSkiaUi`.
- Default template UI (`MainPage` counter sample); does not yet host SkiaSharp controls.
- SkiaSharp handlers are not registered in `MauiProgram` yet (required before using `SKGLView` / custom Skia controls).

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

# Run the demo (example: Mac Catalyst)
dotnet build MauiSkiaUiDemo/MauiSkiaUiDemo.csproj -t:Run -f net10.0-maccatalyst

# Run the demo (example: Android)
dotnet build MauiSkiaUiDemo/MauiSkiaUiDemo.csproj -t:Run -f net10.0-android
```

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
