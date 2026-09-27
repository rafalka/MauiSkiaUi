# SkUiScrollView

Single-surface scroller with pan, fling, wheel, and programmatic scroll APIs.

**MAUI counterpart:** [`ScrollView`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/scrollview)

## How it works

Extends [`SkUiContentView`](SkUiContentView.md). Measures content unconstrained on enabled axes; content keeps a stable arranged frame (`max(measured extent, viewport)`). With an effective right-to-left `FlowDirection`, content is mirrored and a horizontal scroller starts at the right end (`ScrollX` stays a physical offset, 0 = left).

**The scroll offset is a composite-time children translation.** Scrolling never re-records content. Each child keeps its own retained picture, so an animating child re-records only itself, and off-screen children are culled by the compositor.

**Fling, `ScrollToAsync` and `AnimateScrollTo` run on the render thread** (GPU surfaces), so they stay smooth while the UI thread is busy. The render thread reports offsets back every frame, which keeps `ScrollX` / `ScrollY`, `Scrolled`, hit-testing and native overlays in sync. A new touch stops the motion at the last shown offset.

Child press is **withheld until a tap is confirmed** (movement under 10 DIPs), so pans never press buttons. Native overlays register with ancestor scrollers for O(overlays) offset sync. See [RenderingPipeline.md](../design/RenderingPipeline.md).


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** bindables call the direct setter. Direct setters **do not** write back to the bindable store (intentional FR-10 desync). Prefer one update path per property.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's own tap model (`Tapped` / `TappedCommand`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).


## How to use

```xml
<sk:SkUiScrollView Orientation="Vertical" Padding="16">
  <sk:SkUiVerticalStackLayout Spacing="8">
    <!-- long content -->
  </sk:SkUiVerticalStackLayout>
</sk:SkUiScrollView>
```

```csharp
await scroller.ScrollToAsync(0, 400, animated: true);
```

## Key properties / APIs

`Orientation`, `ScrollX`, `ScrollY`, `ContentSize`, `Scrolled`, `ScrollTo`, `ScrollToAsync`, `AnimateScrollTo`.

## Differences from MAUI ScrollView

| Topic | SkiaUi |
| --- | --- |
| Hosting | SkiaUi content on the shared surface (prefer this over nesting MAUI ScrollView around SkiaUi) |
| Scrollbars / bounce / snap | Not implemented |
| Nested scroll arbitration | Deferred |
| Wheel on `Both` | Vertical wheel always; horizontal only when orientation is horizontal-only |
| Overlay snapshot while scrolling | Not yet (overlays live-sync) |

## Related

[ScrollingAndCollectionViews.md](../design/ScrollingAndCollectionViews.md) · Gallery: `ScrollViewDemoPage`
