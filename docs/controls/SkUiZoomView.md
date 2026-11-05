# SkUiZoomView

Pinch, pan, double tap and wheel zoom of its content, on both layers (`SkUiZoomView`, `SkUiCoreZoomView`). The content's composite-time scale and offset change, so nothing is recorded again while it moves.

**MAUI counterpart:** none.

```xml
<sk:SkUiZoomView MaximumZoomScale="4" HeightRequest="300">
    <sk:SkUiImage Source="plan.png" Aspect="AspectFit" />
</sk:SkUiZoomView>
```

```csharp
var zoom = new SkUiCoreZoomView().SetMaximumZoomScale(4);
zoom.SetContent(new SkUiCoreImage().SetSourceFile("plan.png"));
```

The content fills the view at scale 1, and it clips to the view. A pinch keeps the point under the fingers where it is; the content's edges never come inside the view's. At scale 1 it takes no drag, so it can sit in a scroll view. A double tap zooms in at the tap, to `DoubleTapZoomScale`, and back out; these changes are not animated. The wheel zooms about the center, and passes on what is left at the limits.

| Member | Meaning |
| --- | --- |
| `ZoomScale` | The current scale, read-only |
| `MinimumZoomScale` | Default 1 |
| `MaximumZoomScale` | Default 5 |
| `DoubleTapZoomScale` | Default 2.5 |
| `ZoomChanged` | The scale changed |
| `ZoomTo(scale, focus)` | Zooms keeping the point at `focus` (view coordinates) where it is |
| `ResetZoom()` | Back to scale 1, centered |

What is recorded stays at the content's own resolution, so text and vectors blur when zoomed far in. Give an image a source larger than the view if it should stay sharp.
