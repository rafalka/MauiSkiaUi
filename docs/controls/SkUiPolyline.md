# SkUiPolyline

Connected straight lines through a list of points (an open figure).

**MAUI counterpart:** [`Polyline`](https://learn.microsoft.com/dotnet/maui/user-interface/shapes/polyline)

## How it works

As [`SkUiPolygon`](SkUiPolygon.md), without closing the figure: `Points`, `FillRule`; a `Fill` fills the area the open figure encloses, as in MAUI. Caps apply at both ends, joins at every vertex. Core twin: `SkUiCorePolyline`.

## Shape model (all shapes)

The MAUI `Shape` API, from [`SkUiShape`](SkUiShape.md): `Fill` and `Stroke` brushes (solid colors and gradients), `StrokeThickness` (1 by default), `StrokeDashArray` / `StrokeDashOffset` (in multiples of the thickness), `StrokeLineCap`, `StrokeLineJoin`, `StrokeMiterLimit`, `Aspect`. A shape without `Fill` and `Stroke` draws nothing, as in MAUI.

## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10). Invalid values: `Set*` throws; XAML, bindings, styles and the property setter ignore them with a logged warning, as MAUI does.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).

## How to use

```xml
<sk:SkUiPolyline Points="0,0 10,30 15,0 18,60 23,30 35,30 40,0 43,60 48,30 100,30" Stroke="Red" />
<sk:SkUiPolyline Points="0 48, 0 144, 96 150, 100 0, 192 0" Stroke="DarkBlue" StrokeThickness="20"
                 StrokeLineJoin="Round" StrokeLineCap="Round" />
```

## Differences from MAUI Polyline

Same API and geometry.

## Related

Gallery: `PolylineDemoPage`
