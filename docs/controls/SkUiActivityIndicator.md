# SkUiActivityIndicator

Indeterminate spinner that spins on the render thread.

**MAUI counterpart:** [`ActivityIndicator`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/activityindicator)

## How it works

While `IsRunning` is true, the arc is recorded once and the compositor rotates it about the slot center on the render thread (one revolution per second). No per-frame re-recording and no UI-thread work, so it keeps spinning while the UI thread is busy. Spinning only requests frames while the indicator is actually drawn: hidden, culled or detached indicators cost nothing. Hiding (`IsVisible=false`) clears `IsRunning`. Detaching keeps `IsRunning`, so a rehost resumes automatically. Intrinsic measure comes from `SkUiLook.Current.DefaultActivityIndicatorSize` (default 36×36 DIPs). The arc comes from `SkUiLook.Current.DrawActivityIndicator`, drawn at `sweepStart = 0`, so custom painters must draw centered, rotation-symmetric geometry. `SkUiCoreActivityIndicator` uses the same path.


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10). Invalid values: `Set*` throws; XAML, bindings, styles and the property setter ignore them with a logged warning, as MAUI does.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).


## How to use

```xml
<sk:SkUiActivityIndicator IsRunning="True" Color="#087F83" />
```

## Key properties

`IsRunning`, `Color` (+ `Set*`). Intrinsic measure defaults to 36×36 DIPs.

## Differences from MAUI ActivityIndicator

| Topic | SkiaUi |
| --- | --- |
| Appearance | Drawn arc on Skia (not platform spinner) |
| Lifecycle | Unbinds on hide/detach; `IsRunning` resumes after rehost |
| Size | Fixed intrinsic size unless constrained by layout |

## Related

Gallery: `ActivityIndicatorDemoPage`
