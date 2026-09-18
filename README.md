# SkiaUi

.NET MAUI library of **base controls and layouts drawn with SkiaSharp**, with GPU acceleration when available and **native MAUI hosting** (`SkUiMauiContentView`) for Entry, Editor, WebView, and similar.

Compose an `ISkUiView` tree under a root host (`SkUiContentView` / `SkUiLayout`). Drawn children share one Skia surface; system-backed controls appear as native overlays. The tree is fully authorable in XAML.

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

| Area | Types |
| --- | --- |
| Hosts & layout | `SkUiContentView`, `SkUiLayout`, `SkUiGrid`, stacks, `SkUiAbsoluteLayout`, `SkUiScrollView`, `SkUiBorder` |
| Text & chrome | `SkUiLabel`, `SkUiButton`, `SkUiImage`, `SkUiImageButton`, `SkUiActivityIndicator` |
| Toggles | `SkUiSwitch`, `SkUiCheckBox`, `SkUiRadioButton` |
| Shapes | `SkUiBox`, `SkUiEllipse`, `SkUiLine` |
| Native overlay | `SkUiMauiContentView` (Entry, Editor, WebView, …) |

Per-control guides (behavior vs MAUI, XAML samples, limits): **[docs/controls/](docs/controls/README.md)**.

## Essentials

- **Coordinates** are MAUI DIPs; the handler maps to surface pixels.
- **`HwAccelerated`** is set before the handler attaches (ContentView/Layout default GPU; leaves default software). Hosted children ignore it.
- **Gestures** use SkiaUi’s own tap model (`Tapped` / `TappedCommand`), not MAUI `GestureRecognizers`.
- **Styles / VisualStateManager** work on bindable properties like other MAUI views.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation when changing many properties.
- Direct fluent setters update applied state but **do not write back** to the bindable store — prefer one update path per property.
- Drawn nodes are not yet full accessibility / keyboard targets; native overlays keep their platform a11y.

## Sample app

`MauiSkiaUiDemo` in this repo is a component gallery (editors, native side-by-side comparisons, look/color playground, stress page). It is for exploration and verification, not published with the NuGet package.

## Contributing / developing SkiaUi

Build, test, CI, architecture, and design docs: **[Development.md](Development.md)**.

## License

[MIT](LICENSE)
