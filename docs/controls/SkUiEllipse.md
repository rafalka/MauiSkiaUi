# SkUiEllipse

Filled ellipse primitive.

**MAUI counterpart:** [`Ellipse`](https://learn.microsoft.com/dotnet/maui/user-interface/shapes/ellipse) shape

## How it works

Paints an oval in arranged bounds. **Hit-testing remains rectangular** (FR-11 default).


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** bindables call the direct setter. Direct setters **do not** write back to the bindable store (intentional FR-10 desync). Prefer one update path per property.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's own tap model (`Tapped` / `TappedCommand`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../../EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../../LayoutSystem.md).


## How to use

```xml
<sk:SkUiEllipse Color="#087F83" WidthRequest="100" HeightRequest="100" />
```

## Differences from MAUI Ellipse

No stroke-only / geometry path APIs. Rectangular hits include visually empty corners.

## Related

Gallery: `EllipseDemoPage`
