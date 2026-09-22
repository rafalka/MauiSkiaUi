# SkUiBorder

Single-child host with rounded-rectangle fill, stroke, and content clip. Corner radii are independent (MAUI [`CornerRadius`](https://learn.microsoft.com/dotnet/api/microsoft.maui.cornerradius)).

**MAUI counterpart:** [`Border`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/border)

## How it works

Extends [`SkUiContentView`](SkUiContentView.md). Fill paints in the Background layer; stroke paints in the Overlay layer (after content) so opaque children cannot cover the border. Fill/stroke/clip use `SkUiLook.Current.DrawRoundedBox` / `CreateRoundRectPath` with per-corner radii. Fill uses `Background` solid brush, then `BackgroundColor`. Core analogue: `SkUiCoreBorder`.


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** bindables call the direct setter. Direct setters **do not** write back to the bindable store (intentional FR-10 desync). Prefer one update path per property.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's own tap model (`Tapped` / `TappedCommand`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).


## How to use

```xml
<sk:SkUiBorder Stroke="#087F83" StrokeThickness="2" CornerRadius="10" BackgroundColor="White">
  <sk:SkUiLabel Text="Bordered content" Padding="12" />
</sk:SkUiBorder>
```

Per-corner radii (top-left, top-right, bottom-left, bottom-right):

```xml
<sk:SkUiBorder Stroke="#087F83" StrokeThickness="2" CornerRadius="16,4,4,16" BackgroundColor="White">
  <sk:SkUiLabel Text="Asymmetric corners" Padding="12" />
</sk:SkUiBorder>
```

```csharp
border.SetCornerRadius(new CornerRadius(16, 4, 4, 16));
// or uniform:
border.SetCornerRadius(10);
```

## Key properties

`Stroke`, `StrokeThickness`, `CornerRadius` (`Microsoft.Maui.CornerRadius`), plus ContentView `Content` / `Padding`.

## Differences from MAUI Border

| Topic | SkiaUi |
| --- | --- |
| Shape | Rounded rectangle only (per-corner radii) — no arbitrary `IShape` / `StrokeShape` |
| Stroke brush | Solid `Color?` only |
| Hit testing | Rectangular arranged bounds |

## Related

Gallery: `BorderDemoPage`
