# SkUiScrollView

Single-surface scroller with pan, fling, wheel, overscroll, scroll bars, and programmatic scroll APIs.

**MAUI counterpart:** [`ScrollView`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/scrollview)

## How it works

Extends [`SkUiContentView`](SkUiContentView.md). Measures content unconstrained on enabled axes; content keeps a stable arranged frame (`max(measured extent, viewport)`). Across the scroll axis, content with an explicit size (`WidthRequest` of a vertical scroller's content, `HeightRequest` of a horizontal one's) keeps it even when it is larger than the viewport, and is clipped, not scrolled, as MAUI's ScrollView does on its platforms (same on `SkUiCoreScrollView`). With an effective right-to-left `FlowDirection`, content is mirrored and a horizontal scroller starts at the right end (`ScrollX` stays a physical offset, 0 = left).

**Sizing:** without a `HeightRequest` (`WidthRequest` for horizontal scrolling), a scroll view is as large as its content until a limit stops it: the parent's constraint or `MaximumHeightRequest` / `MaximumWidthRequest`. Below the limit it behaves as if it were not there (nothing to scroll); at the limit it stops growing and scrolls. Typical limits: a page or `ContentView` of a fixed size, a star row with `VerticalOptions="Start"` (the scroller then takes its content's height up to the row's), or a maximum size inside a stack. A vertical stack or an `Auto` grid row gives no limit along its axis (MAUI's layouts measure their children there with infinite space), so a scroller placed directly in one takes its whole content, as MAUI's ScrollView does; give it a maximum size there.

**The scroll offset is a composite-time children translation.** Scrolling never re-records content. Each child keeps its own retained picture, so an animating child re-records only itself, and off-screen children are culled by the compositor.

**Fling, `ScrollToAsync` and `AnimateScrollTo` run on the render thread** (GPU surfaces), so they stay smooth while the UI thread is busy. The render thread reports offsets back every frame, which keeps `ScrollX` / `ScrollY`, `Scrolled`, hit-testing and native overlays in sync. A new touch stops the motion at the last shown offset.

**Drags take part in the gesture arena** ([EventMechanism.md](../design/EventMechanism.md)):
- **Taps:** content taps win unless the pointer moves more than the touch slop (10 DIPs) along an enabled axis, in a direction the scroller can still move.
- **Press feedback:** a button inside shows its pressed state after `SkUiGestureSettings.PressDelay` (100 ms) unless a scroll starts first, so starting a scroll never flashes buttons.
- **Drags inside the scroller:** controls inside can still drag along the other axis (sliders, swipe rows, horizontal carousels).

**Nested scrollers:**
- Orthogonal scrollers each take their own axis.
- With the same axis, the inner scroller moves first and the rest of the drag chains to the outer one.
- A fling goes to the innermost scroller that can move that way. The wheel and trackpad scroll both axes (`SkUiTouchEvent.WheelDelta` / `WheelDeltaX`; Shift + wheel and tilt wheels on Windows): each axis goes to the innermost scroller that can move that way, and a plain mouse wheel scrolls a horizontal-only scroller.
- Inside a native MAUI `ScrollView`, the drawn scroller goes first and the native one takes over at the drawn edge.

**Overscroll** (`Overscroll`, a SkiaUi extension; `Default` follows the look, and the default look follows the platform: `Bounce` on iOS and Mac Catalyst, `Stretch` on Android, `None` elsewhere):
- What a drag cannot scroll, after the outer scrollers took what they can, pulls the dragged scroller past its edge: a rubber band with growing resistance (`Bounce`), or a stretch away from the pulled edge (`Stretch`). Dragging back takes the pull back first; releasing springs back on the render thread.
- A fling that reaches the edge runs past it with its velocity there (at most 15 % of the viewport) and settles; with `None` it stops at the edge.
- At an edge a scroller takes an outward drag only when no outer drawn scroller and no native ancestor (a MAUI `ScrollView` around the surface) can still scroll that way, so nested chaining and native hand-over are unchanged.
- `ScrollX` / `ScrollY` stay within the content (no `Scrolled` while overscrolled); native overlays follow a bounce.

**Scroll bars** (MAUI's `VerticalScrollBarVisibility` / `HorizontalScrollBarVisibility`): `Default` shows a bar while the offset changes and fades it out after `SkUiLook.ScrollBarFadeDelay` (500 ms) over `ScrollBarFadeDuration` (250 ms); `Always` shows it while the content overflows; `Never` hides it. Drawn by `SkUiLook.DrawScrollBar` (`ScrollBarThickness`, `ScrollBarMargin`, `ScrollBarMinimumThumbLength`; color: the scheme's foreground at 40 %, or the bar's `ThumbColor`), on the left in right-to-left layouts; both bars leave the corner free.
- **Overlay or gutter:** a `Default` bar draws over the content. An `Always` bar reserves a gutter beside it (`SkUiLook.ScrollBarReservedThickness`, 12 DIPs: the expanded bar and its margins), on the right (left in right-to-left layouts) or at the bottom: the content is measured, arranged and clipped in the rest (the scrollport), and the bar sits in the gutter. The gutter is reserved whenever the axis scrolls, also while the content fits (the bar then hides), as CSS `scrollbar-gutter: stable`, so the layout never flips with its own result. Offsets, `ScrollToAsync` positions and native overlays use the scrollport.
- **Core components:** each bar is a [`SkUiCoreScrollBar`](SkUiCore.md#scroll-bars) (`VerticalScrollBar`, `HorizontalScrollBar`): a Core node pinned to the viewport whose thumb is placed by a scroll link, so the compositor moves it during flings and fades it with no recording and no UI-thread work. Style it with `ThumbColor` and `IsInteractive`; read `IsExpanded` / `IsDragging`.
- **Desktop:** while a mouse, trackpad or pen hovers the 16 DIP strip along the edge (`ScrollBarHitThickness`), the bar shows, widens to `ScrollBarExpandedThickness` (8) with its track (`DrawScrollBarTrack`) and takes presses: drag the thumb to scroll, press the track to page one viewport towards the press. Touch never hovers, so touches on the strip scroll and tap the content as usual.
- **Placed bars:** `new SkUiCoreScrollBar(scrollView, ScrollOrientation.Vertical)` follows a scroller from anywhere in the same surface (in a `SkUiCoreHost`, a Core layout beside a card); set its `Visibility` and the scroller's own bar to `Never`.

**Snap points** (`SnapPointsType`, `SnapPointsAlignment`: MAUI's CollectionView enums; a SkiaUi extension, MAUI's ScrollView has none): the children of the content are the snap targets, lined up with the viewport's start, center or end. With `Mandatory`, drags, flings and wheel / trackpad scrolling (after a 150 ms pause) end on the snap point nearest to where the motion would stop; with `MandatorySingle` a swipe moves one snap point from where the drag started, and a slow drag settles on the nearest (a carousel). The settle is a render-thread spring that starts at the release velocity and never passes its target. Programmatic scrolls do not snap.

**Scrolling to an element** (MAUI's API): `ScrollToAsync(Element, ScrollToPosition, bool)` scrolls so that a drawn descendant, a Core node under a `SkUiCoreHost` (`ScrollToAsync(SkUiCoreNode, …)`), or a MAUI view inside a `SkUiMauiContentView` is at the `Start`, `Center` or `End` of the viewport; `MakeVisible` scrolls only when it is not fully visible, aligning its end when it begins after the viewport's start and its start otherwise (MAUI's rule: an element larger than the viewport moves even when part of it shows). Positions come from the layout (offsets of nested scrollers included, transforms ignored, as MAUI) and are clamped; before the first layout the request waits for it. `GetScrollPositionForElement` returns the offset without scrolling. Both `ScrollToAsync` overloads raise MAUI's `ScrollToRequested` with MAUI's `ScrollToRequestedEventArgs`; with `Orientation="Neither"` they do nothing.

Native overlays register with ancestor scrollers for O(overlays) offset sync. See [RenderingPipeline.md](../design/RenderingPipeline.md) and [ScrollingAndCollectionViews.md](../design/ScrollingAndCollectionViews.md).


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10). Invalid values: `Set*` throws; XAML, bindings, styles and the property setter ignore them with a logged warning, as MAUI does.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`). Of MAUI's `GestureRecognizers`, `TapGestureRecognizer` (1 or 2 taps) runs on the arena; other recognizers are not run and are reported once as a `Trace` line. See [EventMechanism.md](../design/EventMechanism.md#maui-gesture-recognizers).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).


## How to use

```xml
<sk:SkUiScrollView Orientation="Vertical" Padding="16">
  <sk:SkUiVerticalStackLayout Spacing="8">
    <!-- long content -->
  </sk:SkUiVerticalStackLayout>
</sk:SkUiScrollView>
```

```xml
<sk:SkUiScrollView Orientation="Both" VerticalScrollBarVisibility="Always" HorizontalScrollBarVisibility="Never"
                   Overscroll="Bounce">
  …
</sk:SkUiScrollView>
```

```csharp
await scroller.ScrollToAsync(0, 400, animated: true);
await scroller.ScrollToAsync(finalLabel, ScrollToPosition.Center, animated: true);
```

## Key properties / APIs

`Orientation`, `ScrollX`, `ScrollY`, `ContentSize`, `HorizontalScrollBarVisibility`, `VerticalScrollBarVisibility`, `VerticalScrollBar`, `HorizontalScrollBar`, `Overscroll`, `SnapPointsType`, `SnapPointsAlignment`, `IsScrolling`, `Scrolled`, `ScrollToRequested`, `ScrollTo`, `ScrollToAsync` (offset, element, Core node), `GetScrollPositionForElement`, `AnimateScrollTo`. Core: [`SkUiCoreScrollView`](SkUiCore.md) has the same features (`Set*` setters, `ScrollToAsync(SkUiCoreNode, …)`, `GetScrollPositionForNode`).

## Differences from MAUI ScrollView

| Topic | SkiaUi |
| --- | --- |
| Hosting | SkiaUi content on the shared surface (prefer this over nesting MAUI ScrollView around SkiaUi) |
| Scroll bars | Drawn by the look, the same on every platform; `Default` fades as on mobile, and a hovering pointer expands the bar for dragging and paging (desktop); bars can be styled and placed by the app |
| Bounce / overscroll | Per look or per scroller (`Overscroll`), not only the platform's |
| Snap points | `SnapPointsType` / `SnapPointsAlignment` on the content's children (MAUI has them on CollectionView only) |
| Nested scrolling | Supported (axis-aware, chaining, fling hand-off; also with Core `SkUiCoreScrollView` and native ancestors) |
| `ScrollToAsync` tasks | Cancelled (not completed) when a newer scroll, a drag or unloading supersedes them |
| Overlay snapshot while scrolling | `SkUiMauiContentView.ScrollMode` (snapshot on Android / Windows, live on Apple by default) |

## Related

[ScrollingAndCollectionViews.md](../design/ScrollingAndCollectionViews.md) · Gallery: `ScrollViewDemoPage`
