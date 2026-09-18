# SkUiBox

Filled rectangle primitive.

**MAUI counterpart:** [`BoxView`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/boxview)

## How it works

[`SkUiShape`](SkUiShape.md) subclass. Default intrinsic size 48×48. Hit region is the arranged rectangle.


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** bindables call the direct setter. Direct setters **do not** write back to the bindable store (intentional FR-10 desync). Prefer one update path per property.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's own tap model (`Tapped` / `TappedCommand`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).


## How to use

```xml
<sk:SkUiBox Color="#C54150" WidthRequest="112" HeightRequest="112" />
```

## Key properties

`Color`, `StrokeWidth` (ignored for filled box), plus base view layout/transform props.

## Differences from MAUI BoxView

Drawn with Skia; corner radius not supported (use [`SkUiBorder`](SkUiBorder.md)). Passive unless `Tapped` subscribed.

## Related

Gallery: `BoxDemoPage`
