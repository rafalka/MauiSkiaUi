# SkUiBox

Filled rectangle primitive.

**MAUI counterpart:** [`BoxView`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/boxview)

## How it works

[`SkUiShape`](SkUiShape.md) subclass. Default intrinsic size 48×48. `CornerRadius` rounds each corner independently (MAUI's `CornerRadius`; radii larger than the box allows are scaled down), with the look's rounded-rect geometry like [`SkUiBorder`](SkUiBorder.md). Hit region is the arranged rectangle.


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): both write the bindable store, and getters read it, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10).
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).


## How to use

```xml
<sk:SkUiBox Color="#C54150" WidthRequest="112" HeightRequest="112" />
<sk:SkUiBox Color="#087F83" CornerRadius="12,12,0,0" HeightRequest="40" />
```

## Key properties

`Color`, `CornerRadius` (+ `SetCornerRadius`), `StrokeWidth` (ignored for filled box), plus base view layout/transform props.

## Differences from MAUI BoxView

Drawn with Skia. Default intrinsic size is 48×48 (MAUI: 40×40). Passive unless `Tapped` subscribed.

## Related

Gallery: `BoxDemoPage`
