# SkUiSwitch

On/off pill toggle.

**MAUI counterpart:** [`Switch`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/switch)

## How it works

Extends [`SkUiToggleControl`](SkUiToggleControl.md). Tap toggles `IsChecked`. Intrinsic measure comes from `SkUiLook.Current.DefaultSwitchSize` (default 51×31 DIPs). Track/thumb geometry is drawn via `SkUiLook.Current.DrawSwitch` (same path as `SkUiCoreSwitch`).


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** bindables call the direct setter. Direct setters **do not** write back to the bindable store (intentional FR-10 desync). Prefer one update path per property.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's own tap model (`Tapped` / `TappedCommand`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../../EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../../LayoutSystem.md).


## How to use

```xml
<sk:SkUiSwitch IsChecked="True" OnColor="#087F83" ThumbColor="White" />
```

## Key properties

`IsChecked`, `CheckedChanged`, `OnColor`, `ThumbColor`.

## Differences from MAUI Switch

| Topic | SkiaUi |
| --- | --- |
| State API | `IsChecked` / `CheckedChanged` (not `IsToggled` / `Toggled`) |
| Off-track color | Fixed SkiaUi track-off color (no full MAUI off-color model) |
| Gestures | Intrinsic SkiaUi tap |

## Related

Gallery: `SwitchDemoPage`
