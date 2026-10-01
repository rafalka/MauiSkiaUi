# SkUiContentView

Single-child Skia composition host. Typical outer bridge into a SkiaUi tree.

**MAUI counterpart:** [`ContentView`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/contentview)

## How it works

`[ContentProperty(nameof(Content))]` hosts one `ISkUiView`. Defaults `HwAccelerated = true`. Forwards measure/arrange/paint/touch to `Content` and owns a touch router for capture.


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): both write the bindable store, and getters read it, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10).
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).


## How to use

```xml
<sk:SkUiContentView Background="White" HeightRequest="280">
  <sk:SkUiGrid>...</sk:SkUiGrid>
</sk:SkUiContentView>
```

## Key properties

`Content`, `Padding` (+ `SetContent` / `SetPadding`).

## Differences from MAUI ContentView

| Topic | SkiaUi |
| --- | --- |
| Child type | Must be `ISkUiView` / `Element` (not arbitrary `View` unless it implements the contract) |
| Surface | Owns the Skia GL/SW surface when standalone |
| Nested MAUI controls | Use [`SkUiMauiContentView`](SkUiMauiContentView.md) |

## Related

[LayoutSystem.md](../design/LayoutSystem.md) · Gallery: `ContentViewDemoPage`
