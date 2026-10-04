# SkUiLayout

Overlay layout base: every child receives the **same** padded slot; margins/alignment position within that slot. ZIndex orders paint and hit-test.

**MAUI counterpart:** none (SkiaUi overlay). Not a Stack/Grid replacement — use concrete subclasses for structured layout.

## How it works

`[ContentProperty(nameof(Children))]` owns `IList<ISkUiView>`. Defaults `HwAccelerated = true`. Base for Grid, stacks, Absolute.


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10). Invalid values: `Set*` throws; XAML, bindings, styles and the property setter ignore them with a logged warning, as MAUI does.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`). Of MAUI's `GestureRecognizers`, `TapGestureRecognizer` (1 or 2 taps) runs on the arena; other recognizers are not run and are reported once as a `Trace` line. See [EventMechanism.md](../design/EventMechanism.md#maui-gesture-recognizers).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).


## How to use

```xml
<sk:SkUiLayout>
  <sk:SkUiBox Color="Crimson" WidthRequest="80" HeightRequest="60"
              HorizontalOptions="Start" VerticalOptions="Start" Margin="16" />
  <sk:SkUiEllipse Fill="Teal" WidthRequest="60" HeightRequest="60"
                  HorizontalOptions="End" VerticalOptions="End" Margin="16" />
</sk:SkUiLayout>
```

## Key properties

`Children`, `Padding`.

## Notes

- Measure takes the max of children desired sizes.
- Prefer [`SkUiGrid`](SkUiGrid.md) / stacks / Absolute for real UI structure.

## Related

[LayoutSystem.md](../design/LayoutSystem.md) · Gallery: `LayoutDemoPage`
