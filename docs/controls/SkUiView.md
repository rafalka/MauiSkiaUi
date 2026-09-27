# SkUiView

Base class for every Skia-drawn SkiaUi node. Implements [`ISkUiView`](../../MauiSkiaUi/ISkUiView.cs) (`IView` + `Paint` + `Touch`).

**MAUI counterpart:** none (SkiaUi infrastructure). Closest concepts: MAUI [`View`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/view) for layout properties.

## How it works

`SkUiView` owns handler-independent measure/arrange caching, Background → Content → Overlay paint (`PaintBackground` / `PaintOverlay` delegates for chrome; virtual `OnPaintContent` for structure), render transforms (translation/rotation/scale), opacity, opt-in clipping (`ClipToBounds`: on for leaves, off for layouts / content hosts, as in MAUI), tap participation, render-thread `AnimateAsync`, and access to the shared UI-thread [`SkUiAnimationClock`](../design/AnimationMechanism.md). Surfaces composite retained per-node pictures ([RenderingPipeline.md](../design/RenderingPipeline.md)). A custom MAUI handler creates a Skia surface only when the view is **standalone** in the MAUI tree.


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** bindables call the direct setter. Direct setters **do not** write back to the bindable store (intentional FR-10 desync). Prefer one update path per property.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).


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
| `DoubleTapped` / `LongPressed` / `Swiped` (+ commands), `PanUpdated`, `PinchUpdated` | Opt-in gestures (gesture arena) |
| `Gestures` | Custom recognizers (`SkUiPointerGestureRecognizer`, `SkUiPanGestureRecognizer`, …) |
| `IsPressed` | Shared press state for intrinsic controls |
| `StartUpdating` / `EndUpdating` | Coalesce invalidation |
| `InvalidatePaint` | Re-record this node's content (not its children) without remeasure. Transform / opacity / offset changes need no call: they are composite-time |
| `ClipToBounds` | Clip content, children and overlay to the arranged rect. Defaults: `true` for leaves, `false` for `SkUiLayout` / `SkUiContentView` / `SkUiCoreHost` |
| `AnimateAsync(property, to, length, easing)` | Animates `Opacity`, translation, `Rotation` or scale **on the render thread**; the bindable is updated to the final value. Setting the property meanwhile cancels it |
| `PaintBackground` / `PaintOverlay` | Chrome layer delegates (`SetPaintBackground` / `SetPaintOverlay`). Content is virtual `OnPaintContent` only. Control chrome painters (e.g. `PaintButtonBackground`) are `protected` for subclass reuse; `PaintDefaultBackground` is the solid MAUI fill fallback. |
| `AnimationClock` | Shared clock of the topmost SkiaUi ancestor; local clocks are abandoned when the subtree is reparented (`OnAnimationRootChanged`) |
| `Paint` / `Touch` | `ISkUiView` surface |

## Differences / extensions

- Not a MAUI `SKGLView` subclass; surface comes from `SkUiViewHandler` (Metal on Apple, GL thread on Android, `SKCanvasView` for software).
- Defaults: leaf controls `HwAccelerated = false`; hosts/layouts default `true`.
- Hit-testing uses **arranged bounds** (shape-aware hits deferred).
- Solid `Background` / `BackgroundColor` only in v1. Prefer either path; empty MAUI default brushes do not block `BackgroundColor`.

## Related

- [DrawingMechanism.md](../design/DrawingMechanism.md) · [EventMechanism.md](../design/EventMechanism.md) · [LayoutSystem.md](../design/LayoutSystem.md)
- Gallery: `ViewDemoPage`
