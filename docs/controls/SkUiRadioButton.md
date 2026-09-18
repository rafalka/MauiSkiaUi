# SkUiRadioButton

Radio circle that selects on tap (does not toggle off).

**MAUI counterpart:** [`RadioButton`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/radiobutton)

## How it works

Extends [`SkUiToggleControl`](SkUiToggleControl.md) but **overrides tap** to set `IsChecked = true` only (never unchecks on re-tap). `GroupName` is bookkeeping for apps — **siblings are not auto-unchecked**. Intrinsic measure comes from `SkUiLook.Current.DefaultRadioButtonSize` (default 24×24 DIPs). Ring/dot drawn via `SkUiLook.Current.DrawRadioButton` (same path as `SkUiCoreRadioButton`).


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** bindables call the direct setter. Direct setters **do not** write back to the bindable store (intentional FR-10 desync). Prefer one update path per property.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's own tap model (`Tapped` / `TappedCommand`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).


## How to use

```xml
<sk:SkUiHorizontalStackLayout Spacing="8">
  <sk:SkUiRadioButton x:Name="A" GroupName="plan" IsChecked="True" />
  <sk:SkUiLabel Text="Option A" VerticalOptions="Center" />
</sk:SkUiHorizontalStackLayout>
```

```csharp
radio.CheckedChanged += (_, isChecked) =>
{
    if (!isChecked) return;
    foreach (var other in group) if (!ReferenceEquals(other, radio)) other.IsChecked = false;
};
```

## Key properties

`IsChecked`, `CheckedChanged`, `Color`, `GroupName`.

## Differences from MAUI RadioButton

| Topic | SkiaUi |
| --- | --- |
| `Content` / label | Not drawn — compose Label beside the control |
| Group exclusion | **Not automatic** — clear siblings in app code |
| Re-tap | Stays checked (select-only) |
| Visual | Circle + dot only |

## Related

Gallery: `RadioButtonDemoPage`
