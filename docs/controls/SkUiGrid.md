# SkUiGrid

Grid layout using MAUI's `GridLayoutManager`.

**MAUI counterpart:** [`Grid`](https://learn.microsoft.com/dotnet/maui/user-interface/layouts/grid)

## How it works

Extends [`SkUiLayout`](SkUiLayout.md). Uses standard `Grid.Row`, `Grid.Column`, `Grid.RowSpan`, `Grid.ColumnSpan` attached properties. Empty definitions imply one star row/column.


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10). Invalid values: `Set*` throws; XAML, bindings, styles and the property setter ignore them with a logged warning, as MAUI does.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`). Of MAUI's `GestureRecognizers`, `TapGestureRecognizer` (1 or 2 taps) runs on the arena; other recognizers are not run and are reported once as a `Trace` line. See [EventMechanism.md](../design/EventMechanism.md#maui-gesture-recognizers).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).


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
