# SkUiToggleControl

Abstract base for Switch, CheckBox, and RadioButton.

**MAUI counterpart:** none (shared SkiaUi helper).

## How it works

Owns `IsChecked`, `CheckedChanged`, and default tap-to-toggle. RadioButton overrides tap for select-only behavior.


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** bindables call the direct setter. Direct setters **do not** write back to the bindable store (intentional FR-10 desync). Prefer one update path per property.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's own tap model (`Tapped` / `TappedCommand`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../../EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../../LayoutSystem.md).


## Key properties

`IsChecked`, `IsCheckedProperty`, `CheckedChanged`, `SetIsChecked`.

## Related

[`SkUiSwitch`](SkUiSwitch.md) · [`SkUiCheckBox`](SkUiCheckBox.md) · [`SkUiRadioButton`](SkUiRadioButton.md)
