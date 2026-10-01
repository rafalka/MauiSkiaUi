# SkUiProgressBar

Bar showing a completed fraction, or ongoing activity when the amount is unknown.

**MAUI counterpart:** [`ProgressBar`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/progressbar)

## How it works

- **Determinate:** fills `Progress` (0–1) from the start; right-to-left layouts fill from the right.
- **Indeterminate:** `IsIndeterminate="True"` shows a moving segment. The motion runs on the render thread, like [`SkUiActivityIndicator`](SkUiActivityIndicator.md)'s spin: the compositor slides the recorded bar along itself, so it keeps moving while the UI thread is busy and costs no re-recording.
- **Drawing:** `SkUiLook.Current.DrawProgressBar` with a `SkUiProgressBarPaint`. A look can smooth `Progress` changes with a `Progress` transition ([transitions](../design/ControlLook.md#state-change-transitions-fr-26); none by default, as MAUI; never on top of `ProgressTo`). Sizes and timing come from `DefaultProgressBarHeight`, `IndeterminateProgressSegment`, `IndeterminateProgressPeriod` and `GetProgressBarCornerRadius`. Same drawing as [`SkUiCoreProgressBar`](SkUiCore.md).
- **`ProgressTo`:** animates `Progress` on the UI animation clock, like MAUI's; returns `false` when a newer call replaced it or the animation was stopped (e.g. the page closed). It pauses while the bar is detached and continues once the bar is in a tree again, also when called before the bar was added.

## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): both write the bindable store, and getters read it, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10).
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).


## How to use

```xml
<sk:SkUiProgressBar Progress="{Binding Completed}" ProgressColor="#087F83" />
<sk:SkUiProgressBar IsIndeterminate="{Binding IsBusy}" />
```

## Key properties

`Progress` (clamped 0–1; NaN becomes 0), `IsIndeterminate`, `ProgressColor`, `TrackColor`, `ProgressTo(value, length, easing)`.

## Differences from MAUI ProgressBar

| Topic | SkiaUi |
| --- | --- |
| Indeterminate mode | `IsIndeterminate` (not in MAUI) |
| Track color | `TrackColor` (MAUI uses the platform track) |

## Related

[`SkUiSlider`](SkUiSlider.md) · Gallery: `ProgressBarDemoPage`
