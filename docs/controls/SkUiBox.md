# SkUiBox

A filled rectangle with optional rounded corners, as MAUI's `BoxView`.

**MAUI counterpart:** [`BoxView`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/boxview)

## How it works

Fills with `Color`, or, when no color is set, with `Background` (solid or gradient) / `BackgroundColor` (as MAUI's BoxView); `CornerRadius` rounds each corner independently (radii larger than the box allows are scaled down), with the look's rounded-rect geometry like [`SkUiBorder`](SkUiBorder.md). Measures 40 × 40 DIPs unless sized, as in MAUI. Hit region is the arranged rectangle. It is not a [shape](SkUiShape.md) (no stroke), as in MAUI. Core twin: `SkUiCoreBox` (`SetColor`, `SetCornerRadius`; `SetBackground` fills when no color is set). An opaque box casts its `Shadow` from its rounded shape ([SkUiView.md](SkUiView.md#backgrounds-shadows-and-clips)).

## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10). Invalid values: `Set*` throws; XAML, bindings, styles and the property setter ignore them with a logged warning, as MAUI does.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).

## How to use

```xml
<sk:SkUiBox Color="#C54150" WidthRequest="112" HeightRequest="112" />
<sk:SkUiBox Color="#087F83" CornerRadius="12,12,0,0" HeightRequest="40" />
```

## Key properties

`Color` (`null` by default), `CornerRadius` (+ `SetColor`, `SetCornerRadius`), plus the base view's layout and transform properties.

## Differences from MAUI BoxView

Drawn with Skia; passive unless `Tapped` is subscribed. Gradient backgrounds are not drawn yet (P7).

**Breaking (P6):** `SkUiBox` no longer derives from `SkUiShape`; `Color` defaults to `null` (was teal) and the default size is 40 × 40 (was 48 × 48), as in MAUI.

## Related

Gallery: `BoxDemoPage`
