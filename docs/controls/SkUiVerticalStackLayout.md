# SkUiVerticalStackLayout

Vertical stack using MAUI's `VerticalStackLayoutManager`.

**MAUI counterpart:** [`VerticalStackLayout`](https://learn.microsoft.com/dotnet/maui/user-interface/layouts/verticalstacklayout)

## How it works

Extends [`SkUiLayout`](SkUiLayout.md) / `IStackLayout`. Only adds `Spacing` beyond shared padding/Children.


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** bindables call the direct setter. Direct setters **do not** write back to the bindable store (intentional FR-10 desync). Prefer one update path per property.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's own tap model (`Tapped` / `TappedCommand`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).


## How to use

```xml
<sk:SkUiVerticalStackLayout Spacing="12">
  <sk:SkUiLabel Text="One" />
  <sk:SkUiLabel Text="Two" />
</sk:SkUiVerticalStackLayout>
```

## Key properties

`Spacing`, `Children`, `Padding`.

## Differences from MAUI VerticalStackLayout

Children must be `ISkUiView`. Layout semantics follow the MAUI manager.

## Related

Gallery: `VerticalStackLayoutDemoPage`
