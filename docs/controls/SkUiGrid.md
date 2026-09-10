# SkUiGrid

Grid layout using MAUI's `GridLayoutManager`.

**MAUI counterpart:** [`Grid`](https://learn.microsoft.com/dotnet/maui/user-interface/layouts/grid)

## How it works

Extends [`SkUiLayout`](SkUiLayout.md). Uses standard `Grid.Row`, `Grid.Column`, `Grid.RowSpan`, `Grid.ColumnSpan` attached properties. Empty definitions imply one star row/column.


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** bindables call the direct setter. Direct setters **do not** write back to the bindable store (intentional FR-10 desync). Prefer one update path per property.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's own tap model (`Tapped` / `TappedCommand`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../../EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../../LayoutSystem.md).


## How to use

```xml
<sk:SkUiGrid RowDefinitions="Auto,*,Auto" ColumnDefinitions="*,*" ColumnSpacing="12" RowSpacing="8">
  <sk:SkUiLabel Text="Title" />
  <sk:SkUiLabel Grid.Column="1" Text="Meta" />
  <sk:SkUiBox Grid.Row="1" Grid.ColumnSpan="2" Color="#F1F5F5" />
</sk:SkUiGrid>
```

## Key properties

`RowDefinitions`, `ColumnDefinitions`, `RowSpacing`, `ColumnSpacing` (+ `Set*`).

## Differences from MAUI Grid

| Topic | SkiaUi |
| --- | --- |
| Children | `ISkUiView` only |
| Attached props | Same MAUI `Grid.*` APIs; parent invalidation is SkiaUi-owned |
| Paint | Single shared surface (hosted children have no handlers) |

## Related

Gallery: `GridDemoPage`
