# SkiaUi

.NET MAUI library of **SkiaSharp-drawn UI** with two layers you can mix in one tree: a **MAUI-compatible `SkUi*` surface** for replacing native controls, and a **lightweight Core** for building custom controls and dense UI without MAUI `View` overhead. GPU acceleration when available; **`SkUiMauiContentView`** hosts Entry, Editor, WebView, and similar as native overlays.

## Two layers

| Layer | What it is | When to use it |
| --- | --- | --- |
| **`SkUi*`** (MAUI-compatible) | Drop-in style controls and layouts (`SkUiLabel`, `SkUiButton`, `SkUiGrid`, …) with XAML, bindable properties, styles, and MAUI measure/arrange | Replace slow native MAUI visual trees while keeping familiar authoring |
| **Core** (`SkUiCore*`) | Low-level fluent nodes (no `BindableObject` / MAUI `View` per cell); hosted under `SkUiCoreHost` | Author new controls or complex / high-count UI where allocation and add-to-tree cost matter |

Both share one Skia surface under a root host (`SkUiContentView` / `SkUiLayout`). Drawn children paint together; system widgets appear as overlays. The MAUI-compatible tree is fully authorable in XAML; Core is code-first (fluent + `INotifyPropertyChanged`).

```
MAUI page
└── SkUiContentView / SkUiLayout     ← one Skia surface
      ├── SkUiGrid / SkUiButton / …  ← SkUi* (replace native MAUI)
      └── SkUiCoreHost
            └── SkUiCore* tree       ← Core (compose / dense UI)
```

## Benefits

- **Faster trees** — one shared Skia surface instead of a platform view per control; Core avoids MAUI control identity for dense composition (see [stress results](#stress-results-device)).
- **Drop-in replacement path** — `SkUi*` mirrors common MAUI controls/layouts with MAUI layout semantics so you can swap hot spots without reinventing measure/arrange.
- **Composition substrate** — build custom chrome and complex controls from Core primitives, then expose a thin `SkUi*` or host them via `SkUiCoreHost`.
- **XAML where you want it** — bindable properties, styles, VisualStateManager on `SkUi*`; batch updates with `StartUpdating` / `EndUpdating`.
- **GPU when it helps** — `HwAccelerated` on hosts (Metal/GL where supported); software path for leaves and constrained devices.
- **Native when Skia isn’t enough** — `SkUiMauiContentView` keeps real Entry / Editor / WebView (IME, system widgets) as overlays.
- **Shared look & palette** — control look and color scheme drive defaults for both layers without fighting MAUI `AppTheme` alone.
- **Same gesture model** — SkiaUi tap / commands on drawn nodes; overlays keep platform input.

## Install

```bash
dotnet add package MauiSkiaUi
```

Register in `MauiProgram`:

```csharp
builder.UseSkiaUi();
```

## Quick start

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

## Controls

### SkUi* (replace native MAUI)

| Area | Types |
| --- | --- |
| Hosts & layout | `SkUiContentView`, `SkUiLayout`, `SkUiGrid`, stacks, `SkUiAbsoluteLayout`, `SkUiScrollView`, `SkUiBorder` |
| Text & chrome | `SkUiLabel`, `SkUiButton`, `SkUiImage`, `SkUiImageButton`, `SkUiActivityIndicator` |
| Toggles | `SkUiSwitch`, `SkUiCheckBox`, `SkUiRadioButton` |
| Shapes | `SkUiBox`, `SkUiEllipse`, `SkUiLine` |
| Native overlay | `SkUiMauiContentView` (Entry, Editor, WebView, …) |

### Core (compose / custom controls)

| Area | Types |
| --- | --- |
| Bridge | `SkUiCoreHost` |
| Layouts | `SkUiCoreAbsoluteLayout`, stacks, overlay, `SkUiCoreContentView` / `SkUiCoreBorder` |
| Controls | `SkUiCoreLabel`, `SkUiCoreButton`, toggles, image / image button, activity indicator, shapes |

Per-control guides (behavior vs MAUI, XAML samples, limits): **[docs/controls/](docs/controls/README.md)** · Core overview: **[docs/controls/SkUiCore.md](docs/controls/SkUiCore.md)**.

## Essentials

- **Coordinates** are MAUI DIPs; the handler maps to surface pixels.
- **`HwAccelerated`** is set before the handler attaches (ContentView/Layout default GPU; leaves default software). Hosted children ignore it.
- **Gestures** use SkiaUi’s own tap model (`Tapped` / `TappedCommand`), not MAUI `GestureRecognizers`.
- **Styles / VisualStateManager** work on bindable `SkUi*` properties like other MAUI views.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation when changing many properties.
- Direct fluent setters update applied state but **do not write back** to the bindable store — prefer one update path per property.
- Drawn nodes are not yet full accessibility / keyboard targets; native overlays keep their platform a11y.

## Sample app

`MauiSkiaUiDemo` in this repo is a component gallery (editors, native side-by-side comparisons, look/color playground, stress page). It is for exploration and verification, not published with the NuGet package.

## Stress results (device)

Average of **3 runs** each on a **Samsung Galaxy S9** (Android), stress page, **1 000 children**, animation **off**. SkUi* / Core used **HW acceleration on**. Times are milliseconds from the demo’s `[Stress]` console metrics.

| Layer | Generate UI | Add to page | UI render (layout + first frame) | Overall (start → UI idle) |
| --- | ---: | ---: | ---: | ---: |
| Native MAUI | 387.9 | 5356.2 | 825.6 | 6570.3 |
| SkUi* (GPU) | 202.3 | 47.8 | 262.2 | 517.2 |
| Core (GPU) | 36.6 | 5.7 | 84.6 | 127.5 |

On this device, overall idle time was about **13×** faster for SkUi* and **52×** faster for Core than native MAUI for the same child count.

## Contributing / developing SkiaUi

Build, test, CI, architecture, and design docs: **[Development.md](Development.md)**.

## License

[MIT](LICENSE)
