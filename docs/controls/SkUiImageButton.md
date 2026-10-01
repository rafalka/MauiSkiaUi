# SkUiImageButton

Tappable image with command support, pressed/disabled tint, rounded corners, border and padding.

**MAUI counterpart:** [`ImageButton`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/imagebutton)

## How it works

Extends [`SkUiImage`](SkUiImage.md). Intrinsic tap; `Command.CanExecute` controls eligibility. `Pressed` / `Released` come before `Clicked`, as in MAUI; a cancelled press (e.g. a scroll took over) also raises `Released`. The image is drawn inside `Padding` (which adds to the intrinsic size) and clipped to the per-corner `CornerRadii`; `CornerRadius` (an `int`, as in MAUI) sets all four and reads the top-left one, whichever of the two is set last wins. `SkUiCoreImageButton` has only `CornerRadii` (plus `SetCornerRadius(double)`). `BorderColor` / `BorderWidth` draw a rounded border inside the bounds, over the image and the tint. Press / disabled feedback is drawn over the image by `SkUiLook.Current.DrawPressOverlay` with a `SkUiPressOverlayPaint` and animates with the look's [transitions](../design/ControlLook.md#state-change-transitions-fr-26): a dark tint fades in (`PressEffect = Dim`) or a ripple spreads from the press point (`Ripple`).


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** bindables call the direct setter. Direct setters **do not** write back to the bindable store (intentional FR-10 desync). Prefer one update path per property.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).


## How to use

```xml
<sk:SkUiImageButton Source="earth.jpg" Command="{Binding OpenCommand}" CornerRadius="8"
                    Padding="4" BorderColor="#087F83" BorderWidth="2" />
```

## Key properties

All Image APIs plus `Command`, `CommandParameter`, `Clicked`, `Pressed`, `Released`, `CornerRadii`, `CornerRadius`, `BorderColor`, `BorderWidth`, `Padding`.

## Differences from MAUI ImageButton

| Topic | SkiaUi |
| --- | --- |
| Aspect / source limits | Same as [`SkUiImage`](SkUiImage.md) |
| Gestures | SkiaUi tap model only |

## Related

Gallery: `ImageButtonDemoPage`
