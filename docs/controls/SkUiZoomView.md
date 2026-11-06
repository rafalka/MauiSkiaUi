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

The view clips to its bounds. What is between a pinch's fingers follows them, also when both move the same way; zoomed in, the content's edges never come inside where they are at scale 1. A double tap zooms in at the tap, to `DoubleTapZoomScale`, and back out; these changes are not animated. Turning the wheel away from you zooms in about the content's center, and what is left at the limits passes on.

At scale 1 the view takes no drag, so it can sit in a scroll view. Set `IsWheelZoomEnabled` to `false` there, or a wheel over the view zooms before the page scrolls.

The view owns its content's `Scale`, `TranslationX` and `TranslationY`, and expects its `AnchorX` and `AnchorY` at their defaults, in the center. Replaced content gets these back at rest, and the new content starts at rest.

| Member | Meaning |
| --- | --- |
| `ZoomScale` | The current scale, read-only |
| `MinimumZoomScale` | Default 1 |
| `MaximumZoomScale` | Default 5 |
| `DoubleTapZoomScale` | Default 2.5 |
| `IsWheelZoomEnabled` | Default `true`; off, the wheel passes to what is around |
| `ZoomChanged` | The scale changed |
| `ZoomTo(scale, focus)` | Zooms keeping the point at `focus` (view coordinates) where it is |
| `ResetZoom()` | Back to scale 1 (or the nearest the limits allow), centered |

What is recorded stays at the content's own resolution, so text and vectors blur when zoomed far in. Give an image a source larger than the view if it should stay sharp.
