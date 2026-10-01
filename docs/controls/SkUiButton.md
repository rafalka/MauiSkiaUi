# SkUiButton

Drawn text button with intrinsic tap, command, rounded chrome, and visual states.

**MAUI counterpart:** [`Button`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/button)

## How it works

Extends [`SkUiLabel`](SkUiLabel.md). Background (fill, border and press feedback) is drawn by `SkUiLook.Current.DrawButton` with a `SkUiButtonPaint`; content is clipped to the same rounded path. Press feedback animates with the look's [transitions](../design/ControlLook.md#state-change-transitions-fr-26): the default look dims the fill (`PressEffect = Dim`) or spreads a ripple from the press point (`Ripple`); a quick tap still shows its full press. `HandlesTap` is true. `Command.CanExecute` gates eligibility and Disabled visual state.


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** bindables call the direct setter. Direct setters **do not** write back to the bindable store (intentional FR-10 desync). Prefer one update path per property.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).


## How to use

```xml
<sk:SkUiButton Text="Add observation" Command="{Binding AddCommand}"
               FillColor="#087F83" TextColor="White" CornerRadius="6" />
```

## Key properties

Inherits Label text APIs and its rounded chrome: per-corner `CornerRadii`, and `CornerRadius` as in MAUI (an `int` that sets all four corners; use `CornerRadii` for fractional radii), both defaulting to the look's `DefaultButtonCornerRadius` (`SkUiCoreButton` has only `CornerRadii`, plus `SetCornerRadius(double)` for all four); `BorderColor`, `BorderWidth`. Adds `Command`, `CommandParameter`, `Clicked`, `Pressed`, `Released`, `FillColor`. As in MAUI, a tap raises `Pressed`, `Released`, then `Clicked`; a cancelled press (a scroll took over, the pointer left) raises `Released` without `Clicked`.

## Differences from MAUI Button

| Topic | SkiaUi |
| --- | --- |
| Image + text content | Text only (use [`SkUiImageButton`](SkUiImageButton.md) for images) |
| Hit region | Rectangular arranged bounds (corners outside the round fill still hit) |
| `TappedCommand` vs `Command` | On tap, only `Command` runs (plus `Clicked` / `Tapped` event). Do not rely on both commands. |
| Chrome | `FillColor`; solid `Background` overrides fill |
| Visual states | MAUI's: `Normal`, `PointerOver` (hover), `Pressed`, `Disabled` (also when the command cannot execute) |

## Related

Gallery: `ButtonDemoPage`
