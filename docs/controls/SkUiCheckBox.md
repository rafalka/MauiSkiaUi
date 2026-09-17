# SkUiCheckBox

Square checkbox with check mark when selected.

**MAUI counterpart:** [`CheckBox`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/checkbox)

## How it works

Extends [`SkUiToggleControl`](SkUiToggleControl.md). Tap toggles `IsChecked`. Intrinsic measure 24×24 DIPs. Box/checkmark drawn via shared `SkUiChrome.DrawCheckBox` (same path as `SkUiCoreCheckBox`).


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** bindables call the direct setter. Direct setters **do not** write back to the bindable store (intentional FR-10 desync). Prefer one update path per property.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's own tap model (`Tapped` / `TappedCommand`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../../EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../../LayoutSystem.md).


## How to use

```xml
<sk:SkUiCheckBox IsChecked="True" Color="#087F83" />
```

## Key properties

`IsChecked`, `CheckedChanged`, `Color`.

## Differences from MAUI CheckBox

| Topic | SkiaUi |
| --- | --- |
| Label text | None — compose with [`SkUiLabel`](SkUiLabel.md) in a stack |
| Color model | Single `Color` for checked chrome |
| Gestures | Intrinsic SkiaUi tap |

## Related

Gallery: `CheckBoxDemoPage`
