# SkUiLine

Diagonal stroke from top-left to bottom-right of the arranged bounds.

**MAUI counterpart:** [`Line`](https://learn.microsoft.com/dotnet/maui/user-interface/shapes/line) shape

## How it works

Uses `StrokeWidth` and `Color`. Hit region is still the full arranged rectangle.


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** bindables call the direct setter. Direct setters **do not** write back to the bindable store (intentional FR-10 desync). Prefer one update path per property.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's own tap model (`Tapped` / `TappedCommand`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../../EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../../LayoutSystem.md).


## How to use

```xml
<sk:SkUiLine Color="#263D43" StrokeWidth="5" WidthRequest="180" HeightRequest="52" />
```

## Differences from MAUI Line

Fixed diagonal geometry (not arbitrary X1/Y1/X2/Y2). Rectangular hit bounds.

## Related

Gallery: `LineDemoPage`
