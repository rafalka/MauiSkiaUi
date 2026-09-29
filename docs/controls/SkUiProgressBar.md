# SkUiProgressBar

Bar showing a completed fraction, or ongoing activity when the amount is unknown.

**MAUI counterpart:** [`ProgressBar`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/progressbar)

## How it works

- **Determinate:** fills `Progress` (0–1) from the start; right-to-left layouts fill from the right.
- **Indeterminate:** `IsIndeterminate="True"` shows a moving segment. The motion runs on the render thread, like [`SkUiActivityIndicator`](SkUiActivityIndicator.md)'s spin: the compositor slides the recorded bar along itself, so it keeps moving while the UI thread is busy and costs no re-recording.
- **Drawing:** `SkUiLook.Current.DrawProgressBar` with a `SkUiProgressBarPaint`. Sizes and timing come from `DefaultProgressBarHeight`, `IndeterminateProgressSegment`, `IndeterminateProgressPeriod` and `GetProgressBarCornerRadius`. Same drawing as [`SkUiCoreProgressBar`](SkUiCore.md).
- **`ProgressTo`:** animates `Progress` on the UI animation clock, like MAUI's; returns `false` when a newer call replaced it.

## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** bindables call the direct setter. Direct setters **do not** write back to the bindable store (intentional FR-10 desync). Prefer one update path per property.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).


## How to use

```xml
<sk:SkUiProgressBar Progress="{Binding Completed}" ProgressColor="#087F83" />
<sk:SkUiProgressBar IsIndeterminate="{Binding IsBusy}" />
```

## Key properties

`Progress` (clamped 0–1), `IsIndeterminate`, `ProgressColor`, `TrackColor`, `ProgressTo(value, length, easing)`.

## Differences from MAUI ProgressBar

| Topic | SkiaUi |
| --- | --- |
| Indeterminate mode | `IsIndeterminate` (not in MAUI) |
| Track color | `TrackColor` (MAUI uses the platform track) |

## Related

[`SkUiSlider`](SkUiSlider.md) · Gallery: `ProgressBarDemoPage`
