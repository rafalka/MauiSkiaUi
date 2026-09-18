# SkUiHorizontalStackLayout

Horizontal stack using MAUI's `HorizontalStackLayoutManager`.

**MAUI counterpart:** [`HorizontalStackLayout`](https://learn.microsoft.com/dotnet/maui/user-interface/layouts/horizontalstacklayout)

## How it works

Same pattern as [`SkUiVerticalStackLayout`](SkUiVerticalStackLayout.md) with a horizontal manager.


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** bindables call the direct setter. Direct setters **do not** write back to the bindable store (intentional FR-10 desync). Prefer one update path per property.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's own tap model (`Tapped` / `TappedCommand`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).


## How to use

```xml
<sk:SkUiHorizontalStackLayout Spacing="8">
  <sk:SkUiButton Text="A" />
  <sk:SkUiButton Text="B" />
</sk:SkUiHorizontalStackLayout>
```

## Key properties

`Spacing`, `Children`, `Padding`.

## Related

Gallery: `HorizontalStackLayoutDemoPage`
