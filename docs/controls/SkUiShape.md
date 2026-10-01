# SkUiShape

Abstract base for [`SkUiBox`](SkUiBox.md), [`SkUiEllipse`](SkUiEllipse.md), and [`SkUiLine`](SkUiLine.md).

**MAUI counterpart:** none (SkiaUi graphics helper). Related: [MAUI shapes overview](https://learn.microsoft.com/dotnet/maui/user-interface/shapes/).

## Key properties

`Color`, `StrokeWidth` (+ bindables / `Set*` via property setters on concrete types).

Default `MeasureContent` returns 48×48 DIPs.


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10). Invalid values: `Set*` throws; XAML, bindings, styles and the property setter ignore them with a logged warning, as MAUI does.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).

