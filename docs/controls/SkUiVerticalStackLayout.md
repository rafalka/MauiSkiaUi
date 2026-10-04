# SkUiVerticalStackLayout

Vertical stack using MAUI's `VerticalStackLayoutManager`.

**MAUI counterpart:** [`VerticalStackLayout`](https://learn.microsoft.com/dotnet/maui/user-interface/layouts/verticalstacklayout)

## How it works

Extends [`SkUiLayout`](SkUiLayout.md) / `IStackLayout`. Only adds `Spacing` beyond shared padding/Children.


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10). Invalid values: `Set*` throws; XAML, bindings, styles and the property setter ignore them with a logged warning, as MAUI does.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`). Of MAUI's `GestureRecognizers`, `TapGestureRecognizer` (1 or 2 taps) runs on the arena; other recognizers are not run and are reported once as a `Trace` line. See [EventMechanism.md](../design/EventMechanism.md#maui-gesture-recognizers).
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
