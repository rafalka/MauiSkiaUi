# SkUiBorder

Single-child host with rounded-rectangle fill, stroke, and content clip.

**MAUI counterpart:** [`Border`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/border)

## How it works

Extends [`SkUiContentView`](SkUiContentView.md). Shares `SkUiChrome` geometry with Button. Fill uses `Background` solid brush, then `BackgroundColor`.


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** bindables call the direct setter. Direct setters **do not** write back to the bindable store (intentional FR-10 desync). Prefer one update path per property.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's own tap model (`Tapped` / `TappedCommand`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../../EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../../LayoutSystem.md).


## How to use

```xml
<sk:SkUiBorder Stroke="#087F83" StrokeThickness="2" CornerRadius="10" BackgroundColor="White">
  <sk:SkUiLabel Text="Bordered content" Padding="12" />
</sk:SkUiBorder>
```

## Key properties

`Stroke`, `StrokeThickness`, `CornerRadius`, plus ContentView `Content` / `Padding`.

## Differences from MAUI Border

| Topic | SkiaUi |
| --- | --- |
| Shape | Rounded rectangle only — no arbitrary `IShape` / `StrokeShape` |
| Stroke brush | Solid `Color?` only |
| Hit testing | Rectangular arranged bounds |

## Related

Gallery: `BorderDemoPage`
