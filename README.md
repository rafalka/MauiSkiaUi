# SkiaUi

.NET MAUI library of **SkiaSharp-drawn UI** with two layers you can mix in one tree: a **MAUI-compatible `SkUi*` surface** for replacing native controls, and a **lightweight Core** for building custom controls and dense UI without MAUI `View` overhead. GPU acceleration when available; **`SkUiMauiContentView`** hosts Entry, Editor, WebView, and similar as native overlays.

> **Preview.** SkiaUi is still in a prerelease state. It may contain bugs, and public APIs may change without a stable compatibility guarantee. **It is not recommended for production use** yet.

## Why SkiaUi

.NET MAUI is a strong piece of engineering. On a complex screen its cost shows up in two places.

**Speed.** Each visual update crosses from .NET into the native UI runtime. That boundary is fine for a handful of controls and becomes significant when a screen holds a large tree.

**Memory.** Every MAUI control owns a handler and a platform view, and every platform view has a native object on the other side. Leak tracking is difficult because it is often unclear whether the platform view is held by the native object, or the native object is held by the platform view.

SkiaUi draws most of the tree on one Skia surface, so those controls do not each allocate a handler and a platform view. Native views stay only where the platform must own them (text input, WebView, and similar).

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

- **Faster trees** — one shared Skia surface instead of a platform view per control; Core avoids MAUI control identity for dense composition (see [stress results](#stress-results-device) and [performance](docs/Performance.md)).
- **Drop-in replacement path** — `SkUi*` mirrors common MAUI controls/layouts with MAUI layout semantics so you can swap hot spots without reinventing measure/arrange.
- **Composition substrate** — build custom chrome and complex controls from Core primitives, then expose a thin `SkUi*` or host them via `SkUiCoreHost`.
- **XAML where you want it** — bindable properties, styles, VisualStateManager on `SkUi*`; batch updates with `StartUpdating` / `EndUpdating`.
- **GPU when it helps** — `HwAccelerated` on hosts (Metal/GL where supported); software path for leaves and constrained devices.
- **Native when Skia isn’t enough** — `SkUiMauiContentView` keeps real Entry / Editor / WebView (IME, system widgets) as overlays.
- **Shared look & palette** — control look and color scheme drive defaults for both layers without fighting MAUI `AppTheme` alone.
- **Same gesture model** — SkiaUi tap / commands on drawn nodes; overlays keep platform input.

## Install

Package on nuget.org: **[SkiaUi.Maui](https://www.nuget.org/packages/SkiaUi.Maui/)**.

```bash
dotnet add package SkiaUi.Maui
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
		<sk:SkUiEllipse WidthRequest="60" HeightRequest="60" Fill="Teal"
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
| Shapes | `SkUiBox` (BoxView), MAUI's shapes with brushes, dashes and stretch: `SkUiEllipse`, `SkUiLine`, `SkUiRectangle`, `SkUiRoundRectangle`, `SkUiPath`, `SkUiPolygon`, `SkUiPolyline`; `SkUiBorder.StrokeShape` takes any of them |
| Native overlay | `SkUiMauiContentView` (Entry, Editor, WebView, …) |

### Core (compose / custom controls)

| Area | Types |
| --- | --- |
| Bridge | `SkUiCoreHost` |
| Layouts | `SkUiCoreAbsoluteLayout`, stacks, overlay, `SkUiCoreGrid` / `SkUiCoreTable`, `SkUiCoreContentView` / `SkUiCoreBorder` |
| Controls | `SkUiCoreLabel`, `SkUiCoreButton`, toggles, image / image button, activity indicator, shapes |

Per-control guides (behavior vs MAUI, XAML samples, limits): **[docs/controls/](docs/controls/README.md)** · Core overview: **[docs/controls/SkUiCore.md](docs/controls/SkUiCore.md)**.

## Essentials

- **Coordinates** are MAUI DIPs; the handler maps to surface pixels.
- **`HwAccelerated`** is set before the handler attaches (ContentView/Layout default GPU; leaves default software). Hosted children ignore it.
- **Gestures** use SkiaUi’s own tap model (`Tapped` / `TappedCommand`), not MAUI `GestureRecognizers`.
- **Styles / VisualStateManager** work on bindable `SkUi*` properties like other MAUI views, with MAUI's states: `Normal`, `Disabled`, `PointerOver` (mouse / trackpad / pen / iPad pointer hover), buttons' `Pressed`, CheckBox `IsChecked`, Switch `On` / `Off`, RadioButton `Checked` / `Unchecked`, `Focused` / `Unfocused` from keyboard focus.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation when changing many properties.
- Fluent `Set*` setters are the property setters in chainable form: they write the bindable store, so bindings and triggers see them.
- **Accessibility:** drawn controls are read by TalkBack, VoiceOver and Narrator (MAUI's `SemanticProperties` / `AutomationProperties` apply), take keyboard focus (MAUI's `Focus()`, Tab order, Space / Enter, a focus ring), and text follows the system text size (`FontAutoScalingEnabled`). Native overlays keep their platform accessibility ([docs](docs/controls/SkUiView.md#accessibility-and-keyboard)).
- **Trimming and Native AOT:** the library is trimmable and AOT-compatible (no reflection; trim / AOT analyzers fail its build). Checked with Native AOT on iOS and Mac Catalyst and full trimming on Android. Android Native AOT (experimental in .NET 10): software surfaces (`HwAccelerated = false`) fail, because SkiaSharp's Android `SKCanvasView` needs an assembly that build doesn't include; GPU surfaces work.

## Sample app

`MauiSkiaUiDemo` in this repo is a component gallery (editors, native side-by-side comparisons, look/color playground, stress pages). It is for exploration and verification, not published with the NuGet package.

`samples/MauiSkiaUiSamples` shows **how to** build things with SkiaUi, e.g. a custom look with its own check-box transition. Each example page explains what it presents and how to achieve it, lists the things to know, and shows its own source ([samples/README.md](samples/README.md)).

## Stress results (device)

The demo's Stress page: **1,000 buttons** in a two-column grid under one scroll view, animation off; SkUi* and Core on a GPU surface. Release builds, average of 6 rounds (2 launches × 3 rounds after a warm-up round). Times in milliseconds.

| Device | Layer | Generate UI | Add to page | First frame | Overall (start → UI idle) | Scrolling |
| --- | --- | ---: | ---: | ---: | ---: | --- |
| Galaxy S9 (Android 10) | Native MAUI | 415 | 4,798 | 774 | 5,988 | — |
| | SkUi* | 241 | 28 | 214 | 484 | 59 fps |
| | Core | 55 | 7 | 165 | 228 | 59 fps |
| iPhone 8 (iOS 16) | Native MAUI | 240 | 1,649 | 927 | 2,816 | — |
| | SkUi* | 73 | 14 | 48 | 135 | 59 fps |
| | Core | 52 | 8 | 31 | 91 | 60 fps |

The drawn tree is ready **12× (SkUi\*) and 26× (Core) faster** than native MAUI on the S9, and **21× and 31× faster** on the iPhone 8, and scrolls at the display's 60 fps. What gradients, shadows, clips, shaped borders and animated content cost while scrolling, and which of them to avoid in long lists: **[docs/Performance.md](docs/Performance.md)**.

## Contributing / developing SkiaUi

Build, test, CI, architecture, and design docs: **[Development.md](Development.md)**.

## Changes

[CHANGELOG.md](https://github.com/rafalka/MauiSkiaUi/blob/master/CHANGELOG.md) — the same notes nuget.org shows for each `SkiaUi.Maui` version.

## License

[MIT](LICENSE)
