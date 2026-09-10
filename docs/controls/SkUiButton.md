# SkUiButton

Drawn text button with intrinsic tap, command, rounded chrome, and visual states.

**MAUI counterpart:** [`Button`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/button)

## How it works

Extends [`SkUiLabel`](SkUiLabel.md). Background uses shared `SkUiChrome` rounded rect; content is clipped to the same path. `HandlesTap` is true. `Command.CanExecute` gates eligibility and Disabled visual state.


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** bindables call the direct setter. Direct setters **do not** write back to the bindable store (intentional FR-10 desync). Prefer one update path per property.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's own tap model (`Tapped` / `TappedCommand`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../../EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../../LayoutSystem.md).


## How to use

```xml
<sk:SkUiButton Text="Add observation" Command="{Binding AddCommand}"
               FillColor="#087F83" TextColor="White" CornerRadius="6" />
```

## Key properties

Inherits Label text APIs. Adds `Command`, `CommandParameter`, `Clicked`, `CornerRadius`, `FillColor`, `BorderColor`, `BorderWidth`.

## Differences from MAUI Button

| Topic | SkiaUi |
| --- | --- |
| Image + text content | Text only (use [`SkUiImageButton`](SkUiImageButton.md) for images) |
| Hit region | Rectangular arranged bounds (corners outside the round fill still hit) |
| `TappedCommand` vs `Command` | On tap, only `Command` runs (plus `Clicked` / `Tapped` event). Do not rely on both commands. |
| Chrome | `FillColor`; solid `Background` overrides fill |
| Visual states | Normal / Pressed / Disabled via MAUI `VisualStateManager` |

## Related

Gallery: `ButtonDemoPage`
