# SkUiHorizontalStackLayout

Horizontal stack using MAUI's `HorizontalStackLayoutManager`.

**MAUI counterpart:** [`HorizontalStackLayout`](https://learn.microsoft.com/dotnet/maui/user-interface/layouts/horizontalstacklayout)

## How it works

Same pattern as [`SkUiVerticalStackLayout`](SkUiVerticalStackLayout.md) with a horizontal manager.


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10). Invalid values: `Set*` throws; XAML, bindings, styles and the property setter ignore them with a logged warning, as MAUI does.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`). Of MAUI's `GestureRecognizers`, `TapGestureRecognizer` (1 or 2 taps) runs on the arena; other recognizers are not run and are reported once as a `Trace` line. See [EventMechanism.md](../design/EventMechanism.md#maui-gesture-recognizers).
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
