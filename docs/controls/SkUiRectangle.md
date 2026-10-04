# SkUiRectangle

A rectangle filling its bounds, optionally with rounded corners.

**MAUI counterpart:** [`Rectangle`](https://learn.microsoft.com/dotnet/maui/user-interface/shapes/rectangle)

## How it works

Fills its arranged bounds (`Aspect` defaults to `Fill`). `RadiusX` / `RadiusY` round the corners; as in MAUI the corners use the larger of the two (circular corners). As a [`SkUiBorder`](SkUiBorder.md) `StrokeShape` it draws with the look's rounded geometry. Core twin: `SkUiCoreRectangle` (`SetRadiusX`, `SetRadiusY`).

## Shape model (all shapes)

The MAUI `Shape` API, from [`SkUiShape`](SkUiShape.md): `Fill` and `Stroke` brushes (solid colors and gradients), `StrokeThickness` (1 by default), `StrokeDashArray` / `StrokeDashOffset` (in multiples of the thickness), `StrokeLineCap`, `StrokeLineJoin`, `StrokeMiterLimit`, `Aspect`. A shape without `Fill` and `Stroke` draws nothing, as in MAUI.

## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10). Invalid values: `Set*` throws; XAML, bindings, styles and the property setter ignore them with a logged warning, as MAUI does.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`). Of MAUI's `GestureRecognizers`, `TapGestureRecognizer` (1 or 2 taps) runs on the arena; other recognizers are not run and are reported once as a `Trace` line. See [EventMechanism.md](../design/EventMechanism.md#maui-gesture-recognizers).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).

## How to use

```xml
<sk:SkUiRectangle Fill="Red" Stroke="Black" StrokeThickness="3" RadiusX="50" RadiusY="10"
                  WidthRequest="200" HeightRequest="100" HorizontalOptions="Start" />
```

## Differences from MAUI Rectangle

Same API and geometry (MAUI also rounds by the larger radius).

## Related

Gallery: `RectangleDemoPage`
