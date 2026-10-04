# SkUiPolygon

A closed figure through a list of points.

**MAUI counterpart:** [`Polygon`](https://learn.microsoft.com/dotnet/maui/user-interface/shapes/polygon)

## How it works

Draws the figure through `Points` (a MAUI `PointCollection`: `"40,10 70,80 10,50"` in XAML) and closes it. `FillRule` (`EvenOdd` by default, or `Nonzero`) decides the inside where the figure crosses itself. Adding or removing points in the collection re-measures and redraws. Measures and places as any unstretched shape (`Aspect="None"`). See [`SkUiPolyline`](SkUiPolyline.md) for the open variant. Core twin: `SkUiCorePolygon(params Point[])` (`SetPoints`, `SetFillRule(WindingMode)`).

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
<sk:SkUiPolygon Points="40,10 70,80 10,50" Fill="AliceBlue" Stroke="Green" StrokeThickness="5" StrokeLineJoin="Round" />
<sk:SkUiPolygon Points="0 48, 0 144, 96 150, 100 0, 192 0, 192 96, 50 96, 48 192, 150 200 144 48"
                Fill="Blue" Stroke="Red" StrokeThickness="3" FillRule="Nonzero" />
```

## Differences from MAUI Polygon

Same API and geometry.

## Related

Gallery: `PolygonDemoPage`
