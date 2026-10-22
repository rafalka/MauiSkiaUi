# SkiaUi

.NET MAUI library of **SkiaSharp-drawn UI**. Controls are drawn on one GPU surface instead of each owning a platform view, in two layers you can mix in one tree: **`SkUi*` controls** that replace MAUI's by changing the XAML prefix, and a lightweight **Core** layer for building controls of your own. WebView, maps, media and other native views stay native, as overlays in **`SkUiMauiContentView`**.

> **1.0.0 is the first stable release.** Its public API is kept by every `1.x` release; prereleases of the next version (`1.1.0-PrereleaseNN`) are published from the `devel` branch. What's next: [roadmap](docs/design/Roadmap.md).

## Who it's for

**Apps whose MAUI screens are slow.** Long forms, dashboards, lists of rich rows: on a screen with hundreds of controls, MAUI spends seconds creating handlers and platform views before the first frame. `SkUi*` controls are drop-in replacements that keep MAUI's API (the same properties, XAML, bindings, styles, VisualStateManager and layout rules), so a slow page moves to drawn controls by changing `<Label>` to `<sk:SkUiLabel>` and `<Grid>` to `<sk:SkUiGrid>`, one page or one hot spot at a time. On a 2018 Android phone a page of 1,000 buttons is ready in 0.4 s instead of 6.4 s ([stress results](#stress-results-device)). The [migration guide](docs/Migration.md) and the [migration skills](plugins/skiaui-migration/README.md) for AI coding agents cover the rest.

**Developers who build custom Skia-drawn controls.** Drawing a control on a bare `SKCanvasView` means writing its layout, text, hit-testing, gestures, accessibility and animations yourself. With Core, a custom control is composed from drawn building blocks (labels, buttons, shapes, borders, images, toggles, text fields, layouts) that are plain objects with fluent setters, no `BindableObject` and no handler, and it gets SkiaUi's text engine, gesture arena, keyboard focus, screen-reader support, render-thread animations, looks and color schemes for free. Host it in any page with `SkUiCoreHost`, or wrap it in a thin `SkUi*` view to use it from XAML ([Core guide](docs/controls/SkUiCore.md), [samples](samples/README.md)).

**Apps that need richer UI than MAUI offers.** Skeleton loading screens, chips that wrap, carousels with cover-flow effects, lists with sticky and collapsible groups, numeric and date-and-time fields, text that shrinks to fit, one consistent look on every platform: with MAUI alone these mean third-party packages, platform renderers or custom drawing. SkiaUi ships them as drawn controls that work like MAUI's, in XAML or in code, the same on Android, iOS, Mac Catalyst and Windows ([what SkiaUi adds](#beyond-maui)).

## Why SkiaUi

.NET MAUI is a strong piece of engineering. On a complex screen its cost shows up in two places.

**Speed.** Each visual update crosses from .NET into the native UI runtime. That boundary is fine for a handful of controls and becomes significant when a screen holds a large tree.

**Memory.** Every MAUI control owns a handler and a platform view, and every platform view has a native object on the other side. Leak tracking is difficult because it is often unclear whether the platform view is held by the native object, or the native object is held by the platform view.

SkiaUi draws most of the tree on one Skia surface, so those controls do not each allocate a handler and a platform view. Native views stay only where the platform must own them (WebView, maps, media, and similar); text fields are drawn, with the platform keyboard and input methods typing into them.

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

## Beyond MAUI

Parity with MAUI is the starting point. What drawn controls add on top of it:

- **The same pixels on every platform, or the platform's own look.** The default look draws each control the same on Android, iOS, Mac Catalyst and Windows. The platform looks (iOS 26, Material 2 and Material 3, Mac Catalyst, Windows) are measured against MAUI's native controls. Shapes, sizes, state transitions and colors come from one `SkUiLook` and `SkUiColorScheme` you customise once for the whole app ([control look](docs/design/ControlLook.md), [color scheme](docs/design/ColorScheme.md)).
- **Motion off the UI thread.** `AnimateAsync`, scroll flings, indeterminate progress and shimmer run on the render thread and keep going while the UI thread is busy. Moving, fading, scrolling or transforming a view with shadows and clips records and blurs nothing again ([rendering pipeline](docs/design/RenderingPipeline.md)).
- **Text MAUI doesn't have:** shrink and grow to fit, tightening, custom line breaking, HTML parsed the same on every platform with tappable links, justified text and synthetic bold / italic everywhere, rounded backgrounds and borders on labels themselves ([SkUiLabel](docs/controls/SkUiLabel.md#differences-from-maui-label)).
- **Controls MAUI doesn't have:** numeric entries with typed values, multi-column and date-and-time pickers, shimmer skeletons of your own views, wrap layouts for chips and tags, stacks whose children shrink to fit, a stack that keeps a bar's title centered, content switches with animations, virtualized stacks; the Community Toolkit's avatar, expander and state container built in, drawn.
- **More from the controls MAUI has:** a collection view with collapsible groups, sticky group headers, sticky header and footer, load more and pull-to-refresh built in; a carousel that loops without an end, shows the next cards peeking and draws item effects (cover flow, scaling, your own) on the compositor; snap points and overscroll on scroll views; vertical sliders, indeterminate progress bars, steppers with stacked buttons and typed values; pickers whose lists open on your app's popup system ([all controls](docs/controls/README.md)).
- **One gesture model.** A per-pointer gesture arena decides between nested taps, long presses, swipes and scrolls the same way on every platform; MAUI's `TapGestureRecognizer` works on drawn views ([event mechanism](docs/design/EventMechanism.md)).
- **Fewer leaks to chase.** Drawn controls have no handler and no native peer to keep them alive, long-lived sources are listened to weakly, and leak scenarios run headless with every test run and on devices.

## Install

Package on nuget.org: **[SkiaUi.Maui](https://www.nuget.org/packages/SkiaUi.Maui/)**.

**Requirements:** .NET 10 with .NET MAUI 10.0.120 or a later 10.x (SDK 10.0.402 or newer); Android 5.0 (API 21), iOS 15, Mac Catalyst 17 and Windows 10 1809 or newer. iOS and Mac Catalyst builds need Xcode 27 (on macOS 26.4 or newer), and apps built with it use the UIKit scene lifecycle ([migration guide](docs/Migration.md#ios-and-mac-catalyst-scene-lifecycle)). SkiaUi 1.0.x builds with MAUI 10.0.110, Mac Catalyst 15 and Xcode 26.

```bash
dotnet add package SkiaUi.Maui
```

Register in `MauiProgram`:

```csharp
builder.UseSkiaUi();
```

Using [CommunityToolkit.Maui](https://github.com/CommunityToolkit/Maui)? **[SkiaUi.Maui.CommunityToolkit](https://www.nuget.org/packages/SkiaUi.Maui.CommunityToolkit/)** opens drawn pickers' lists in the toolkit's `Popup` (without it, they open in MAUI's action sheet, or on any popup system through an `ISkUiPopupPresenter` of yours):

```bash
dotnet add package SkiaUi.Maui.CommunityToolkit
```

```csharp
builder.UseMauiCommunityToolkit().UseSkiaUi().UseSkiaUiCommunityToolkit();
```

## Quick start

```xml
xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi"
```

A drawn page, with MAUI's controls under the `sk:` prefix:

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

A list that shows a shimmering skeleton while it loads:

```xml
<sk:SkUiShimmer IsLoading="{Binding IsLoading}" AfterStateChangeAnimation="FadeIn 250">
	<sk:SkUiCollectionView ItemsSource="{Binding Contacts}" SelectionMode="Single">
		<DataTemplate x:DataType="local:Contact">
			<sk:SkUiGrid ColumnDefinitions="Auto,*" ColumnSpacing="12" Padding="16,8">
				<sk:SkUiAvatarView Text="{Binding Name}" WidthRequest="40" HeightRequest="40" CornerRadius="20" />
				<sk:SkUiVerticalStackLayout Grid.Column="1" Spacing="2" VerticalOptions="Center">
					<sk:SkUiLabel Text="{Binding Name}" FontAttributes="Bold" />
					<sk:SkUiLabel Text="{Binding Email}" FontSize="13" TextColor="Gray" />
				</sk:SkUiVerticalStackLayout>
			</sk:SkUiGrid>
		</DataTemplate>
	</sk:SkUiCollectionView>
</sk:SkUiShimmer>
```

No second layout for the skeleton: while `IsLoading` is true, the still-empty list fills itself with placeholder rows made from its own item template, and each control in them draws as bones (the avatar a circle, each label a bar). The bones shimmer on the render thread, and when the contacts arrive and `IsLoading` turns false, the rows fade in ([SkUiShimmer](docs/controls/SkUiShimmer.md), [SkUiCollectionView](docs/controls/SkUiCollectionView.md)).

## Migrating a MAUI app

**[docs/Migration.md](docs/Migration.md)** walks through moving existing MAUI screens to drawn controls: where the drawn surface goes, the control mapping, styles, gestures, custom controls and what has no drawn equivalent yet. For AI coding agents, the **[SkiaUi migration skills](plugins/skiaui-migration/README.md)** audit an app and convert pages by the same rules (Claude Code: `/plugin marketplace add rafalka/MauiSkiaUi`, then `/plugin install skiaui-migration@skiaui`).

## Controls

### SkUi* (replace native MAUI)

| Area | Types |
| --- | --- |
| Hosts & layout | `SkUiContentView`, `SkUiLayout`, `SkUiGrid`, stacks, `SkUiAbsoluteLayout`, `SkUiFlexLayout`, `SkUiBorder`; `SkUiWrapLayout`, shrink stacks, `SkUiHorizontalAlignStackLayout` |
| Scrolling & lists | `SkUiScrollView`, `SkUiRefreshView`, `SkUiCollectionView`, `SkUiCarouselView`, `SkUiIndicatorView`, `SkUiSwipeView`, virtual stacks |
| Text & chrome | `SkUiLabel`, `SkUiButton`, `SkUiImage`, `SkUiImageButton`, `SkUiAvatarView`, `SkUiActivityIndicator`, `SkUiProgressBar` |
| Toggles & values | `SkUiSwitch`, `SkUiCheckBox`, `SkUiRadioButton`, `SkUiSlider`, `SkUiStepper` |
| Text input | `SkUiEntry`, `SkUiEditor`, `SkUiSearchBar`, `SkUiNumericEntry` / `SkUiNumericEntry<T>` |
| Pickers | `SkUiPicker`, `SkUiMultiColumnPicker`, `SkUiDatePicker`, `SkUiTimePicker`, `SkUiDateTimePicker` |
| Content & loading | `SkUiContentPresenter`, `SkUiAlternateContentView`, `SkUiExpander`, `SkUiStateContainer`, `SkUiShimmer` |
| Shapes | `SkUiBox` (BoxView), MAUI's shapes with brushes, dashes and stretch: `SkUiEllipse`, `SkUiLine`, `SkUiRectangle`, `SkUiRoundRectangle`, `SkUiPath`, `SkUiPolygon`, `SkUiPolyline`; `SkUiBorder.StrokeShape` takes any of them |
| Native overlay | `SkUiMauiContentView` (WebView, maps, …) |

### Core (compose / custom controls)

| Area | Types |
| --- | --- |
| Bridge | `SkUiCoreHost` |
| Layouts | `SkUiCoreAbsoluteLayout`, stacks, overlay, `SkUiCoreGrid` / `SkUiCoreTable`, wrap and shrink layouts, `SkUiCoreContentView` / `SkUiCoreBorder`, `SkUiCoreScrollView` |
| Controls | `SkUiCoreLabel`, `SkUiCoreButton`, toggles, `SkUiCoreSlider`, `SkUiCoreProgressBar`, image / image button, `SkUiCoreAvatar`, activity indicator, shapes, `SkUiCoreShimmer`, `SkUiCoreIndicatorView` |
| Text input & pickers | `SkUiCoreEntry` / `SkUiCoreEditor` / `SkUiCoreSearchBar` / `SkUiCoreNumericEntry` / `SkUiCoreStepper`; `SkUiCorePicker`, `SkUiCoreMultiColumnPicker`, `SkUiCoreDatePicker`, `SkUiCoreTimePicker`, `SkUiCoreDateTimePicker` |

Per-control guides (behavior vs MAUI, XAML samples, limits): **[docs/controls/](docs/controls/README.md)** · Core overview: **[docs/controls/SkUiCore.md](docs/controls/SkUiCore.md)**.

## Essentials

- **Coordinates** are MAUI DIPs; the handler maps to surface pixels.
- **`HwAccelerated`** is set before the handler attaches (ContentView/Layout default GPU; leaves default software). Hosted children ignore it.
- **Gestures** use SkiaUi’s gesture arena (`Tapped` / `TappedCommand`, `LongPressed`, `Swiped`, `PanUpdated`, …). MAUI's `TapGestureRecognizer` works on drawn views; other recognizers and platform behaviors such as `TouchBehavior` are converted to those events ([migration guide](docs/Migration.md#5-convert-gestures-and-touch-behaviors)).
- **Styles / VisualStateManager** work on bindable `SkUi*` properties like other MAUI views, with MAUI's states: `Normal`, `Disabled`, `PointerOver` (mouse / trackpad / pen / iPad pointer hover), buttons' `Pressed`, CheckBox `IsChecked`, Switch `On` / `Off`, RadioButton `Checked` / `Unchecked`, `Focused` / `Unfocused` from keyboard focus.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation when changing many properties.
- Fluent `Set*` setters are the property setters in chainable form: they write the bindable store, so bindings and triggers see them.
- **Accessibility:** drawn controls are read by TalkBack, VoiceOver and Narrator (MAUI's `SemanticProperties` / `AutomationProperties` apply), take keyboard focus (MAUI's `Focus()`, Tab order, Space / Enter, a focus ring), and text follows the system text size (`FontAutoScalingEnabled`). Native overlays keep their platform accessibility ([docs](docs/controls/SkUiView.md#accessibility-and-keyboard)).
- **Trimming and Native AOT:** the library is trimmable and AOT-compatible (no reflection; trim / AOT analyzers fail its build). Checked with Native AOT on iOS and Mac Catalyst and full trimming on Android. Android Native AOT (experimental in .NET 10): software surfaces (`HwAccelerated = false`) fail, because SkiaSharp's Android `SKCanvasView` needs an assembly that build doesn't include; GPU surfaces work.

## Sample app

`MauiSkiaUiDemo` in this repo is a component gallery (editors, native side-by-side comparisons, look/color playground, stress pages). It is for exploration and verification, not published with the NuGet package.

**Prebuilt apps:** every [GitHub release](https://github.com/rafalka/MauiSkiaUi/releases) has the demo and samples apps attached: an Android APK (arm64, signed with a debug key: allow installing from unknown sources) and a Mac Catalyst app in a zip (not signed: right-click it and choose **Open** the first time).

`samples/MauiSkiaUiSamples` shows **how to** build things with SkiaUi, e.g. a custom look with its own check-box transition. Each example page explains what it presents and how to achieve it, lists the things to know, and shows its own source ([samples/README.md](samples/README.md)).

## Stress results (device)

The demo's Stress page: **1,000 buttons** in a two-column grid under one scroll view, animation off; SkUi* and Core on a GPU surface. Release builds on **.NET 10** (MAUI 10.0.110), measured in October 2026; average of 6 rounds (2 launches × 3 rounds after a warm-up round). Times in milliseconds.

The Galaxy S9 (2018) and iPhone 8 (2017) were chosen to represent old, slow devices: a UI that is fast on them is fast on the phones most apps support.

| Device | Layer | Generate UI | Add to page | First frame | Overall (start → UI idle) | Scrolling |
| --- | --- | ---: | ---: | ---: | ---: | --- |
| Galaxy S9 (Android 10) | Native MAUI | 388 | 5,235 | 805 | 6,428 | — |
| | SkUi* | 208 | 36 | 167 | 413 | 59 fps |
| | Core | 76 | 11 | 169 | 257 | 59 fps |
| iPhone 8 (iOS 16) | Native MAUI | 237 | 1,683 | 965 | 2,885 | — |
| | SkUi* | 114 | 14 | 37 | 164 | 60 fps |
| | Core | 50 | 4 | 35 | 89 | 60 fps |

The drawn tree is ready **16× (SkUi\*) and 25× (Core) faster** than native MAUI on the S9, and **18× and 32× faster** on the iPhone 8, and scrolls at the display's 60 fps. What gradients, shadows, clips, shaped borders and animated content cost while scrolling, and which of them to avoid in long lists: **[docs/Performance.md](docs/Performance.md)**.

## Contributing / developing SkiaUi

How to report bugs, request features and open pull requests: **[CONTRIBUTING.md](https://github.com/rafalka/MauiSkiaUi/blob/master/CONTRIBUTING.md)**. Questions go to [Discussions](https://github.com/rafalka/MauiSkiaUi/discussions) ([SUPPORT.md](https://github.com/rafalka/MauiSkiaUi/blob/master/SUPPORT.md)); security problems are reported privately ([SECURITY.md](https://github.com/rafalka/MauiSkiaUi/blob/master/SECURITY.md)).

Build, test and CI: **[Development.md](https://github.com/rafalka/MauiSkiaUi/blob/master/Development.md)**. Architecture, requirements and the roadmap: **[docs](https://github.com/rafalka/MauiSkiaUi/blob/master/docs/README.md)**.

## Changes

[CHANGELOG.md](https://github.com/rafalka/MauiSkiaUi/blob/master/CHANGELOG.md) — the same notes nuget.org shows for each `SkiaUi.Maui` version.

## License

[MIT](LICENSE)
