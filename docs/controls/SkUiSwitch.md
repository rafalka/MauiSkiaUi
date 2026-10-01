# SkUiSwitch

On/off pill toggle.

**MAUI counterpart:** [`Switch`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/switch)

## How it works

Extends [`SkUiToggleControl`](SkUiToggleControl.md). Tap toggles the state. `IsToggled` / `Toggled` (MAUI's names, two-way bindable) and `IsChecked` / `CheckedChanged` are the same two-state view of `CheckState` and stay in step; `Toggled` follows `CheckedChanged`. Intrinsic measure comes from `SkUiLook.Current.DefaultSwitchSize` (default 51×31 DIPs). Track/thumb geometry is drawn via `SkUiLook.Current.DrawSwitch` (same path as `SkUiCoreSwitch`); the default look slides the thumb and blends the track color over 200 ms, and stretches the thumb while pressed ([transitions](../design/ControlLook.md#state-change-transitions-fr-26)).


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** bindables call the direct setter. Direct setters **do not** write back to the bindable store (intentional FR-10 desync). Prefer one update path per property.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).


## How to use

```xml
<sk:SkUiSwitch IsToggled="{Binding Notifications}" OnColor="#087F83" ThumbColor="White" />
```

## Key properties

`IsToggled`, `Toggled` (`ToggledEventArgs`), `OnColor`, `ThumbColor`; `IsChecked` / `CheckedChanged` as on the other toggles. `CheckState` (Indeterminate: centered thumb, half-on track) and `IsThreeState` come from [`SkUiToggleControl`](SkUiToggleControl.md).

## Differences from MAUI Switch

| Topic | SkiaUi |
| --- | --- |
| Three states | `CheckState` / `IsThreeState` (MAUI has two); `IsToggled` is `true` only for Checked |
| Off-track color | Fixed SkiaUi track-off color (no full MAUI off-color model) |
| Gestures | Intrinsic SkiaUi tap |

## Related

Gallery: `SwitchDemoPage`
