# SkUiShape

Abstract base for [`SkUiBox`](SkUiBox.md), [`SkUiEllipse`](SkUiEllipse.md), and [`SkUiLine`](SkUiLine.md).

**MAUI counterpart:** none (SkiaUi graphics helper). Related: [MAUI shapes overview](https://learn.microsoft.com/dotnet/maui/user-interface/shapes/).

## Key properties

`Color`, `StrokeWidth` (+ bindables / `Set*` via property setters on concrete types).

Default `MeasureContent` returns 48×48 DIPs.


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** bindables call the direct setter. Direct setters **do not** write back to the bindable store (intentional FR-10 desync). Prefer one update path per property.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's own tap model (`Tapped` / `TappedCommand`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).

