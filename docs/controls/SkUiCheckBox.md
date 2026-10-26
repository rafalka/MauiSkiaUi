# SkUiCheckBox

Square checkbox with check mark when selected.

**MAUI counterpart:** [`CheckBox`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/checkbox)

## How it works

Extends [`SkUiToggleControl`](SkUiToggleControl.md). Tap toggles; with `IsThreeState` it also reaches Indeterminate (drawn as a dash), which apps also set to show a partly checked group. Intrinsic measure comes from `SkUiLook.Current.DefaultCheckBoxSize` (default 24×24 DIPs), at least `DefaultCheckBoxMinimumWidth` wide (`WindowsSkUiLook`: 120, WinUI's `MinWidth`, with the box at the start) unless the check box sets a `MinimumWidthRequest` (Core: `MinimumWidth`), which replaces it as it replaces WinUI's natively. Box/checkmark drawn via `SkUiLook.Current.DrawCheckBox` (same path as `SkUiCoreCheckBox`); the default look fades the fill in and draws the check mark (or grows the dash) over 160 ms ([transitions](../design/ControlLook.md#state-change-transitions-fr-26)).


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10). Invalid values: `Set*` throws; XAML, bindings, styles and the property setter ignore them with a logged warning, as MAUI does.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`). Of MAUI's `GestureRecognizers`, `TapGestureRecognizer` (1 or 2 taps) runs on the arena; other recognizers are not run and are reported once as a `Trace` line. See [EventMechanism.md](../design/EventMechanism.md#maui-gesture-recognizers).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).


## How to use

```xml
<sk:SkUiCheckBox IsChecked="True" Color="#087F83" />
<sk:SkUiCheckBox CheckState="{Binding AllSelected}" IsThreeState="True" />
```

## Key properties

`CheckState`, `IsChecked`, `IsThreeState`, `CheckStateChanged`, `CheckedChanged` (`CheckedChangedEventArgs`, as in MAUI), `Color`, and MAUI 10's `Command` / `CommandParameter`. The command runs whenever `IsChecked` changes, from a tap or from code, after `CheckedChanged`, and only if it can execute at that moment. While it cannot execute, the check box looks and acts disabled. Core: `SkUiCoreCheckBox.SetCommand` / `SetCommandParameter`.

## Differences from MAUI CheckBox

| Topic | SkiaUi |
| --- | --- |
| Label text | None — compose with [`SkUiLabel`](SkUiLabel.md) in a stack |
| Color model | Single `Color` for checked chrome |
| Three states | `CheckState` / `IsThreeState` (MAUI has two) |
| A command that cannot execute | Disables the check box as in MAUI, but `IsEnabled` keeps its value (MAUI's reads `false`), as on drawn buttons |
| Gestures | Intrinsic SkiaUi tap |
| Visual states | As MAUI's CheckBox: while checked, `IsChecked` when the `CommonStates` group defines it, else `Normal`; otherwise `Normal` / `PointerOver` / `Disabled`. Indeterminate counts as unchecked |

## Related

Gallery: `CheckBoxDemoPage`
