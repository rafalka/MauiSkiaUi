# SkUiActivityIndicator

Indeterminate spinner driven by the shared animation clock.

**MAUI counterpart:** [`ActivityIndicator`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/activityindicator)

## How it works

While `IsRunning` is true, a repeating clock animation updates the sweep angle. The root handler issues **one** paint invalidation per tick (spinners do not each bubble `InvalidatePaint`). Hiding (`IsVisible=false`) or removing the control—or an **ancestor** layout—from its parent stops the clock so detached subtrees cannot keep ticking. Setting `IsRunning` before the control joins its surface-owning ancestor still works: the spin callback rebinds onto the shared root clock when parenting changes. Stroke paint is cached and released on detach. Intrinsic measure comes from `SkUiLook.Current.DefaultActivityIndicatorSize` (default 36×36 DIPs). Arc geometry uses `SkUiLook.Current.DrawActivityIndicator` (same path as `SkUiCoreActivityIndicator`).


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** bindables call the direct setter. Direct setters **do not** write back to the bindable store (intentional FR-10 desync). Prefer one update path per property.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's own tap model (`Tapped` / `TappedCommand`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../../EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../../LayoutSystem.md).


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
| Lifecycle | Auto-stops on hide/detach |
| Size | Fixed intrinsic size unless constrained by layout |

## Related

Gallery: `ActivityIndicatorDemoPage`
