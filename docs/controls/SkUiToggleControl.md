# SkUiToggleControl

Abstract base for Switch, CheckBox, and RadioButton.

**MAUI counterpart:** none (shared SkiaUi helper).

## How it works

- **State:** `CheckState` (`SkUiCheckState`: Unchecked, Checked, Indeterminate) is the state. `IsChecked` is its MAUI-compatible two-state view: `true` only for Checked; setting it sets Checked or Unchecked (`IsChecked = false` also clears Indeterminate). A two-way `IsChecked` binding that pushes `false` while the state is Indeterminate leaves it: the binding only mirrors the state.
- **Taps:** with `IsThreeState` a tap cycles Unchecked → Checked → Indeterminate → Unchecked. Without it, taps go between Checked and Unchecked, and an Indeterminate set by the app (e.g. a "select all" box whose group is partly checked) goes to Checked. RadioButton taps only ever select.
- **Write-back:** a tap sets `CheckState` and `IsChecked` through their bindable properties (two-way by default), so bindings see user changes.
- **Events:** `CheckStateChanged` on every state change; `CheckedChanged` only when `IsChecked` changes. Both run after the bindable properties and their bindings are updated.
- **Drawing:** the look's `DrawSwitch` / `DrawCheckBox` / `DrawRadioButton` take the `SkUiCheckState` (Indeterminate: centered thumb, dash, bar).


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** bindables call the direct setter. Direct setters **do not** write back to the bindable store (intentional FR-10 desync). Prefer one update path per property.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).


## Key properties

`CheckState`, `IsChecked`, `IsThreeState` (bindable), `CheckStateChanged`, `CheckedChanged`, `SetCheckState`, `SetIsChecked`, `SetIsThreeState`.

## Related

[`SkUiSwitch`](SkUiSwitch.md) · [`SkUiCheckBox`](SkUiCheckBox.md) · [`SkUiRadioButton`](SkUiRadioButton.md)
