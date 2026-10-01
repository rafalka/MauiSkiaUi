# SkUiScrollView

Single-surface scroller with pan, fling, wheel, and programmatic scroll APIs.

**MAUI counterpart:** [`ScrollView`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/scrollview)

## How it works

Extends [`SkUiContentView`](SkUiContentView.md). Measures content unconstrained on enabled axes; content keeps a stable arranged frame (`max(measured extent, viewport)`). With an effective right-to-left `FlowDirection`, content is mirrored and a horizontal scroller starts at the right end (`ScrollX` stays a physical offset, 0 = left).

**The scroll offset is a composite-time children translation.** Scrolling never re-records content. Each child keeps its own retained picture, so an animating child re-records only itself, and off-screen children are culled by the compositor.

**Fling, `ScrollToAsync` and `AnimateScrollTo` run on the render thread** (GPU surfaces), so they stay smooth while the UI thread is busy. The render thread reports offsets back every frame, which keeps `ScrollX` / `ScrollY`, `Scrolled`, hit-testing and native overlays in sync. A new touch stops the motion at the last shown offset.

**Drags take part in the gesture arena** ([EventMechanism.md](../design/EventMechanism.md)):
- **Taps:** content taps win unless the pointer moves more than the touch slop (10 DIPs) along an enabled axis, in a direction the scroller can still move.
- **Press feedback:** a button inside shows its pressed state after `SkUiGestureSettings.PressDelay` (100 ms) unless a scroll starts first, so starting a scroll never flashes buttons.
- **Drags inside the scroller:** controls inside can still drag along the other axis (sliders, swipe rows, horizontal carousels).

**Nested scrollers:**
- Orthogonal scrollers each take their own axis.
- With the same axis, the inner scroller moves first and the rest of the drag chains to the outer one.
- A fling goes to the innermost scroller that can move that way, and the wheel scrolls the innermost scroller that can move.
- Inside a native MAUI `ScrollView`, the drawn scroller goes first and the native one takes over at the drawn edge.

Native overlays register with ancestor scrollers for O(overlays) offset sync. See [RenderingPipeline.md](../design/RenderingPipeline.md) and [ScrollingAndCollectionViews.md](../design/ScrollingAndCollectionViews.md).


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): both write the bindable store, and getters read it, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10).
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
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
| Nested scrolling | Supported (axis-aware, chaining, fling hand-off; also with Core `SkUiCoreScrollView` and native ancestors) |
| Wheel on `Both` | Vertical wheel always; horizontal only when orientation is horizontal-only |
| Overlay snapshot while scrolling | `SkUiMauiContentView.ScrollMode` (snapshot on Android / Windows, live on Apple by default) |

## Related

[ScrollingAndCollectionViews.md](../design/ScrollingAndCollectionViews.md) · Gallery: `ScrollViewDemoPage`
