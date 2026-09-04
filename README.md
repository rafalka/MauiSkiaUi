# SkiaUi

.NET MAUI library of **base controls and layouts drawn entirely with SkiaSharp**, using GPU acceleration via `SKGLView` when available.

MAUI hosts a single bridge control (`SkUiContentView`); all SkiaUi UI lives in an `ISkUiView` tree that receives measure, layout, paint, and touch from that bridge. The SkiaUi tree is intended to be fully authorable in XAML (e.g. `SkUiContentView` → `SkUiGrid` → `SkUiLabel`). See [Requirements.md](Requirements.md) for the full architecture.

## Solution structure

| Project | Type | Description |
| --- | --- | --- |
| `MauiSkiaUi` | .NET MAUI class library (`net10.0-*`) | SkiaSharp-based UI controls for consumption by MAUI apps |
| `MauiSkiaUiDemo` | .NET MAUI application (`net10.0-*`) | Sample host used to develop and verify controls |

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
- **[Requirements.md](Requirements.md)** — goals, architecture, XAML model, backlog, and local reference checkouts (MAUI, Open-Maui, Flutter, Avalonia, Uno, DrawnUi).

## License

Not specified yet.
