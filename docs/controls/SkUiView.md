# SkUiView

Base class for every Skia-drawn SkiaUi node. Implements [`ISkUiView`](../../MauiSkiaUi/ISkUiView.cs) (`IView` + `Paint` + `Touch`).

**MAUI counterpart:** none (SkiaUi infrastructure). Closest concepts: MAUI [`View`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/view) for layout properties.

## How it works

`SkUiView` owns handler-independent measure/arrange caching, Background → Content → Overlay paint phases, render transforms (translation/rotation/scale), opacity, rectangular clipping, tap participation, and access to the shared [`SkUiAnimationClock`](../../AnimationMechanism.md). A custom MAUI handler creates a Skia surface only when the view is **standalone** in the MAUI tree.


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** bindables call the direct setter. Direct setters **do not** write back to the bindable store (intentional FR-10 desync). Prefer one update path per property.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's own tap model (`Tapped` / `TappedCommand`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../../EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../../LayoutSystem.md).


## How to use

Usually subclass or use a concrete control. Standalone leaf example:

```xml
<sk:SkUiBox WidthRequest="80" HeightRequest="40" Color="Teal"
            HwAccelerated="False" />
```

```csharp
var node = new SkUiLabel();
node.SetText("Hello").SetFontSize(18);
node.Tapped += (_, _) => { /* opt-in tap */ };
```

## Key APIs

| Member | Role |
| --- | --- |
| `HwAccelerated` | CLR property (not bindable). GPU vs software surface for standalone nodes. Set **before** handler creation. |
| `Tapped` / `TappedCommand` | Opt-in single tap |
| `IsPressed` | Shared press state for intrinsic controls |
| `StartUpdating` / `EndUpdating` | Coalesce invalidation |
| `InvalidatePaint` | Redraw without remeasure |
| `AnimationClock` | Shared root clock |
| `Paint` / `Touch` | `ISkUiView` surface |

## Differences / extensions

- Not a MAUI `SKGLView` subclass; surface comes from `SkUiViewHandler`.
- Defaults: leaf controls `HwAccelerated = false`; hosts/layouts default `true`.
- Hit-testing uses **arranged bounds** (shape-aware hits deferred).
- Solid `Background` / `BackgroundColor` only in v1. Prefer either path; empty MAUI default brushes do not block `BackgroundColor`.

## Related

- [DrawingMechanism.md](../../DrawingMechanism.md) · [EventMechanism.md](../../EventMechanism.md) · [LayoutSystem.md](../../LayoutSystem.md)
- Gallery: `ViewDemoPage`
