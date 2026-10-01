# SkUiCheckBox

Square checkbox with check mark when selected.

**MAUI counterpart:** [`CheckBox`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/checkbox)

## How it works

Extends [`SkUiToggleControl`](SkUiToggleControl.md). Tap toggles; with `IsThreeState` it also reaches Indeterminate (drawn as a dash), which apps also set to show a partly checked group. Intrinsic measure comes from `SkUiLook.Current.DefaultCheckBoxSize` (default 24×24 DIPs). Box/checkmark drawn via `SkUiLook.Current.DrawCheckBox` (same path as `SkUiCoreCheckBox`); the default look fades the fill in and draws the check mark (or grows the dash) over 160 ms ([transitions](../design/ControlLook.md#state-change-transitions-fr-26)).


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** bindables call the direct setter. Direct setters **do not** write back to the bindable store (intentional FR-10 desync). Prefer one update path per property.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).


## How to use

```xml
<sk:SkUiCheckBox IsChecked="True" Color="#087F83" />
<sk:SkUiCheckBox CheckState="{Binding AllSelected}" IsThreeState="True" />
```

## Key properties

`CheckState`, `IsChecked`, `IsThreeState`, `CheckStateChanged`, `CheckedChanged` (`CheckedChangedEventArgs`, as in MAUI), `Color`.

## Differences from MAUI CheckBox

| Topic | SkiaUi |
| --- | --- |
| Label text | None — compose with [`SkUiLabel`](SkUiLabel.md) in a stack |
| Color model | Single `Color` for checked chrome |
| Three states | `CheckState` / `IsThreeState` (MAUI has two) |
| Gestures | Intrinsic SkiaUi tap |
| Visual states | As MAUI's CheckBox: while checked, `IsChecked` when the `CommonStates` group defines it, else `Normal`; otherwise `Normal` / `PointerOver` / `Disabled`. Indeterminate counts as unchecked |

## Related

Gallery: `CheckBoxDemoPage`
