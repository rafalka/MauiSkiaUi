# SkUiSlider

Horizontal or vertical slider for a value in a range.

**MAUI counterpart:** [`Slider`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/slider)

## How it works

- **Input:** a drag along the slider moves the value once it passes the touch slop, so a drag across a horizontal slider still scrolls the page around it. A tap moves the value to the tapped position. The thumb's center follows the touch.
- **Vertical:** `Orientation="Vertical"` puts the minimum at the bottom. Right-to-left layouts put a horizontal slider's minimum at the right.
- **Drawing:** `SkUiLook.Current.DrawSlider` with a `SkUiSliderPaint` (drawn fraction, colors, press amount, enabled). After a tap the thumb glides to the new value (`SliderThumb` transition, 150 ms by default; the value changes at once); drags and code move it at once. The press halo fades in and out with dragging ([transitions](../design/ControlLook.md#state-change-transitions-fr-26)). It is always in horizontal left-to-right coordinates: the control rotates the canvas for vertical sliders. Sizes come from `DefaultSliderThickness`, `DefaultSliderLength` and `SliderThumbRadius`. Same drawing as [`SkUiCoreSlider`](SkUiCore.md).

## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10). Invalid values: `Set*` throws; XAML, bindings, styles and the property setter ignore them with a logged warning, as MAUI does.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).
- **User input writes back:** a tap (toggles) or a drag / tap (sliders) sets the bindable property, so two-way bindings see the change, as with MAUI's controls.


## How to use

```xml
<sk:SkUiSlider Minimum="0" Maximum="100" Value="{Binding Volume}" />
<sk:SkUiSlider Orientation="Vertical" HeightRequest="200" Value="{Binding Level}" />
```

## Key properties

`Minimum`, `Maximum`, `Value` (two-way; clamped to the range, also when the range changes; as in MAUI 10 the requested value is kept and comes back when the range widens, so XAML property order doesn't matter; an empty range gives `Minimum`), `Orientation`, `MinimumTrackColor`, `MaximumTrackColor`, `ThumbColor`, `IsDragging`, `DragStartedCommand`, `DragCompletedCommand`, `ThumbImageSource`. Events: `ValueChanged` (MAUI `ValueChangedEventArgs`), `DragStarted`, `DragCompleted`. Fluent: `SetSliderValue` (not `SetValue`, which is `BindableObject`'s), `SetMinimum`, `SetMaximum`, `SetOrientation`, color setters, `SetThumbImageSource`.

`ThumbImageSource` (MAUI's) replaces the look's thumb with an image at its intrinsic size, loaded like [`SkUiImage.Source`](SkUiImage.md#sources) through the shared cache: the look draws the track only (`SkUiSliderPaint.HasThumbImage`), and the control draws the image upright, centered where the thumb would be, also on vertical and right-to-left sliders. The thumb's travel and touch mapping stay the look's (`SliderThumbRadius`). Disabled sliders draw it at half opacity. `SkUiCoreSlider.SetThumbImageSource` takes an `SkUiImageSource`.

## Differences from MAUI Slider

| Topic | SkiaUi |
| --- | --- |
| Orientation | `Horizontal` (default) or `Vertical` |
| `ThumbImageSource` | Drawn upright at its intrinsic size; the travel stays the look's thumb inset |
| Tap on the track | Moves the value there (no drag events) |

## Related

[`SkUiProgressBar`](SkUiProgressBar.md) · Gallery: `SliderDemoPage`
