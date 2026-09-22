# Changelog

Release notes for the NuGet package **SkiaUi.Maui**.

Pack and publish workflows copy the body under `## <version>` into the package `PackageReleaseNotes` field. nuget.org shows that text on the package page. The heading must match `Version` in [Directory.Build.props](Directory.Build.props) exactly (newest section first). A link back to this file is appended when the notes are extracted.

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
