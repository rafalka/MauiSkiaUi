# SkUiLine

Straight stroke between two points, like MAUI's `Line`.

**MAUI counterpart:** [`Line`](https://learn.microsoft.com/dotnet/maui/user-interface/shapes/line) shape

## How it works

Draws from (`X1`, `Y1`) to (`X2`, `Y2`) in local DIPs with `StrokeWidth` and `Color` (butt caps). As MAUI's `Line` (no stretch):

- **Measure:** the intrinsic size reaches the far end points plus the stroke (`max(X1, X2) + StrokeWidth` × `max(Y1, Y2) + StrokeWidth`), so a line sizes itself without `WidthRequest` / `HeightRequest`.
- **Placement:** the drawing area is the bounds inset by half the stroke; the line moves only to bring an end that sticks out over the left / top (or else the right / bottom) edge back in, so a line starting at 0 is not cut in half.
- Points are not mirrored in right-to-left layouts. With no points set (all 0) nothing is drawn.

Hit region is the full arranged rectangle. `SkUiCoreLine` is the same on the Core layer (`SetPoints`, `SetX1` …).


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): both write the bindable store, and getters read it, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10).
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).


## How to use

```xml
<sk:SkUiLine X1="0" Y1="0" X2="175" Y2="47" Color="#263D43" StrokeWidth="5" />
<sk:SkUiLine X2="200" Color="#D0D7D8" StrokeWidth="1" />  <!-- a separator -->
```

## Differences from MAUI Line

| Topic | SkiaUi |
| --- | --- |
| Stroke | `Color` + `StrokeWidth` (MAUI: `Stroke` brush + `StrokeThickness`, default 1; SkiaUi default 2) |
| `Aspect` (stretch), dashes, caps | Not supported yet (P6 in [ImplementationPlan.md](../design/ImplementationPlan.md)) |
| Hit testing | Rectangular arranged bounds |

## Related

Gallery: `LineDemoPage`
