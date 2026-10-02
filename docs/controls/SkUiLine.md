# SkUiLine

A straight stroke between two points, as MAUI's `Line`.

**MAUI counterpart:** [`Line`](https://learn.microsoft.com/dotnet/maui/user-interface/shapes/line)

## How it works

Draws from (`X1`, `Y1`) to (`X2`, `Y2`) in local DIPs with the `Stroke` brush. As MAUI's `Line` (no stretch by default):

- **Measure:** the intrinsic size reaches the far end points plus the stroke (`max(X1, X2) + StrokeThickness` × `max(Y1, Y2) + StrokeThickness`), so a line sizes itself.
- **Placement:** the drawing area is the bounds inset by half the stroke; the line moves only to bring an end that sticks out back in, so a line starting at 0 is not cut in half. `Aspect` stretches it like any shape.
- Points are not mirrored in right-to-left layouts. Round and square caps reach past the ends.

Hit region is the full arranged rectangle. `SkUiCoreLine` is the same on the Core layer (`SetPoints`, `SetX1` …).

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
<sk:SkUiLine X1="0" Y1="0" X2="175" Y2="47" Stroke="#263D43" StrokeThickness="5" />
<sk:SkUiLine X2="200" Stroke="#D0D7D8" />  <!-- a 1-DIP separator -->
<sk:SkUiLine X1="40" Y1="0" X2="0" Y2="120" Stroke="DarkBlue" StrokeDashArray="1,1" StrokeDashOffset="6" StrokeLineCap="Round" />
```

## Differences from MAUI Line

Same API and geometry. Hit testing uses the arranged rectangle.

## Related

Gallery: `LineDemoPage`
