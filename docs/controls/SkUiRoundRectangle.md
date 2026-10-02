# SkUiRoundRectangle

A rectangle filling its bounds with per-corner radii.

**MAUI counterpart:** [`RoundRectangle`](https://learn.microsoft.com/dotnet/maui/user-interface/shapes/roundrectangle)

## How it works

Fills its arranged bounds (`Aspect` defaults to `Fill`); `CornerRadius` sets each corner (top-left, top-right, bottom-left, bottom-right; `"40"` or `"40,0,0,40"` in XAML). Radii larger than the shape allows are clamped. As a [`SkUiBorder`](SkUiBorder.md) `StrokeShape` it draws with the look's rounded geometry. Core twin: `SkUiCoreRoundRectangle` (`SetCornerRadius`).

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
<sk:SkUiRoundRectangle CornerRadius="40" Fill="Blue" WidthRequest="200" HeightRequest="200" />

<sk:SkUiBorder Stroke="Teal" StrokeThickness="2">
  <sk:SkUiBorder.StrokeShape>
    <sk:SkUiRoundRectangle CornerRadius="16,4,4,16" />
  </sk:SkUiBorder.StrokeShape>
  <sk:SkUiLabel Text="Asymmetric corners" />
</sk:SkUiBorder>
```

## Differences from MAUI RoundRectangle

Same API and geometry.

## Related

Gallery: `RoundRectangleDemoPage`
