# SkUiEllipse

An ellipse or circle filling its bounds, filled, stroked or both.

**MAUI counterpart:** [`Ellipse`](https://learn.microsoft.com/dotnet/maui/user-interface/shapes/ellipse)

## How it works

Fills its arranged bounds (`Aspect` defaults to `Fill`, as in MAUI); the stroke is inset by half its thickness, so a stroke-only ellipse (`Stroke` without `Fill`) is a ring inside the bounds. Without a size it has none: give it `WidthRequest` / `HeightRequest` or a slot to fill. **Hit-testing is rectangular.** Core twin: `SkUiCoreEllipse`.

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
<sk:SkUiEllipse Fill="#087F83" WidthRequest="100" HeightRequest="100" />
<sk:SkUiEllipse Stroke="#C54150" StrokeThickness="4" StrokeDashArray="1,1" StrokeLineCap="Round" WidthRequest="60" HeightRequest="60" />
```

## Differences from MAUI Ellipse

Same API and geometry. Taps include the visually empty corners.

## Related

Gallery: `EllipseDemoPage`
