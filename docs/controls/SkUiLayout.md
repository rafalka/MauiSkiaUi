# SkUiLayout

Overlay layout base: every child receives the **same** padded slot; margins/alignment position within that slot. ZIndex orders paint and hit-test.

**MAUI counterpart:** none (SkiaUi overlay). Not a Stack/Grid replacement — use concrete subclasses for structured layout.

## How it works

`[ContentProperty(nameof(Children))]` owns `IList<ISkUiView>`. Defaults `HwAccelerated = true`. Base for Grid, stacks, Absolute.


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** bindables call the direct setter. Direct setters **do not** write back to the bindable store (intentional FR-10 desync). Prefer one update path per property.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's own tap model (`Tapped` / `TappedCommand`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../../EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../../LayoutSystem.md).


## How to use

```xml
<sk:SkUiLayout>
  <sk:SkUiBox Color="Crimson" WidthRequest="80" HeightRequest="60"
              HorizontalOptions="Start" VerticalOptions="Start" Margin="16" />
  <sk:SkUiEllipse Color="Teal" WidthRequest="60" HeightRequest="60"
                  HorizontalOptions="End" VerticalOptions="End" Margin="16" />
</sk:SkUiLayout>
```

## Key properties

`Children`, `Padding`.

## Notes

- Measure takes the max of children desired sizes.
- Prefer [`SkUiGrid`](SkUiGrid.md) / stacks / Absolute for real UI structure.

## Related

[LayoutSystem.md](../../LayoutSystem.md) · Gallery: `LayoutDemoPage`
