# SkUiRadioButton

Radio circle that selects on tap (does not toggle off) and unchecks the rest of its group.

**MAUI counterpart:** [`RadioButton`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/radiobutton)

## How it works

Extends [`SkUiToggleControl`](SkUiToggleControl.md) but **overrides tap** to select only (never unchecks on re-tap). Checking a radio button, by a tap or from code or a binding, unchecks the others of its group first (their `CheckedChanged` runs before its own), with MAUI's scopes:

- **No `GroupName`:** its siblings in the same parent that have no group name either.
- **A `GroupName`:** every radio button with that name on the page (before the tree is on a page: the layout that names the group, else the parent).

Unchecked radio buttons are written back to their bindable properties, so bindings see them. Intrinsic measure comes from `SkUiLook.Current.DefaultRadioButtonSize` (default 24×24 DIPs). Ring/dot drawn via `SkUiLook.Current.DrawRadioButton` (same path as `SkUiCoreRadioButton`); the default look grows the dot from the center over 160 ms ([transitions](../design/ControlLook.md#state-change-transitions-fr-26)).

**MAUI's `RadioButtonGroup`** attached properties work on any drawn layout (`SkUiLayout` and the layouts derived from it: stacks, grid, flex, wrap, …), with the same markup as in MAUI:
- `GroupName` names the layout's radio buttons that have no group name (also ones added later).
- `SelectedValue` (two-way) is the `Value` of the group's checked radio button. Setting it checks the radio button with an equal `Value`; `null` unchecks the group. A radio button added with a matching `Value` is checked on arrival.
- As in MAUI, the group goes on a layout: on `SkUiContentView`, `SkUiBorder` or `SkUiScrollView` MAUI throws `ArgumentNullException`; put it on the layout inside.

`SkUiCoreRadioButton` is simpler: the radio buttons in the same Core parent form a group (no `GroupName`, `Value` or group layout).

## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): both write the bindable store, and getters read it, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10).
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).


## How to use

```xml
<sk:SkUiVerticalStackLayout RadioButtonGroup.GroupName="plan"
                            RadioButtonGroup.SelectedValue="{Binding Plan}">
  <sk:SkUiHorizontalStackLayout Spacing="8">
    <sk:SkUiRadioButton Value="Basic" />
    <sk:SkUiLabel Text="Basic" VerticalOptions="Center" />
  </sk:SkUiHorizontalStackLayout>
  <sk:SkUiHorizontalStackLayout Spacing="8">
    <sk:SkUiRadioButton Value="Pro" />
    <sk:SkUiLabel Text="Pro" VerticalOptions="Center" />
  </sk:SkUiHorizontalStackLayout>
</sk:SkUiVerticalStackLayout>
```

The radio buttons sit in different parents, so the group needs a name (here from the layout); radio buttons that share a parent exclude each other without one.

```csharp
radio.CheckedChanged += (_, args) => { if (args.Value) Selected = radio.Value; };
```

## Key properties

`IsChecked`, `CheckedChanged` (`CheckedChangedEventArgs`), `Color`, `GroupName`, `Value` (+ `SetRadioValue`). `CheckState` (Indeterminate draws a bar; taps only select) comes from [`SkUiToggleControl`](SkUiToggleControl.md). On the parent layout: MAUI's `RadioButtonGroup.GroupName`, `RadioButtonGroup.SelectedValue`.

## Differences from MAUI RadioButton

| Topic | SkiaUi |
| --- | --- |
| `Content` / label | Not drawn — compose a label beside the control (P8 in [ImplementationPlan.md](../design/ImplementationPlan.md)) |
| `ControlTemplate` | Not supported |
| Visual | Circle + dot only |
| Visual states | As MAUI's RadioButton: `Checked` / `Unchecked`, then the common states |

## Related

Gallery: `RadioButtonDemoPage`
