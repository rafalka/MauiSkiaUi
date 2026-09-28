# SkUiMauiContentView

Hosts a real MAUI `VisualElement` (Entry, Editor, WebView, …) as a **native overlay** on the standalone root (FR-16).

**MAUI counterpart:** none (SkiaUi hosting pattern). Do **not** use Skia reimplementations of Entry/Editor/WebView.

## How it works

The placeholder participates in SkiaUi measure/arrange. The wrapped control's platform view is added as a sibling of the Skia surface inside `SkUiOverlayContainer`. `Touch` always returns `false` so SkiaUi never steals native input. Position uses `ComputeRootRelativeFrame()` (Frame offsets + `TranslationX`/`TranslationY` up the hosted ancestor chain, minus scroll offsets).

**Clipping:** the overlay is clipped to the viewports of ancestor scrollers and to ancestors with `ClipToBounds`, so a scrolled control never covers drawn content around the scroller and cannot be touched outside it.

**Drags that start on the native control** also reach the drawn scroll view around it. Once the drag is clearly a scroll (past the touch slop along the scroller's axis), the native touch is cancelled and the drawn list scrolls. Taps, text selection and cursor placement stay native. Controls that scroll their own content (WebView, Android Editor) keep their native scrolling. Implemented on Android and iOS / Mac Catalyst. On Windows it applies to touch and pen only; mouse drags keep text selection.

**While scrolling** (`ScrollMode`):
- **Auto** (default): snapshot on Android / Windows, live on iOS / Mac Catalyst.
- **Snapshot:** while an ancestor scroller moves, the native view is hidden and a bitmap of it is drawn, so it moves exactly with the drawn content (even during render-thread flings while the UI thread is busy). It is restored `SnapshotRestoreDelay` after scrolling stops. A focused control stays live.
- **Live:** the native view is repositioned on every offset change.


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** bindables call the direct setter. Direct setters **do not** write back to the bindable store (intentional FR-10 desync). Prefer one update path per property.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).


## How to use

```xml
<sk:SkUiMauiContentView HeightRequest="160">
  <Editor Placeholder="Native Editor" />
</sk:SkUiMauiContentView>
```

## Key properties

`Content` (`VisualElement`), `SetContent`. Content must be unparented and handlerless when assigned.
`ScrollMode` (`SkUiOverlayScrollMode`), `IsShowingSnapshot`, `UsesSnapshotWhileScrolling`; static `SnapshotRestoreDelay` and `HighlightSnapshots` (diagnostics).

## Differences / v1 limits

| Topic | Behavior |
| --- | --- |
| Paint | Does not draw the live control into Skia |
| Transforms | No rotation/scale/opacity composition through ancestors |
| Scroll | Clipped to scroll viewports; snapshot while scrolling on Android / Windows (configurable) |
| Drawn content over overlays | Not masked yet: drawn popups cannot cover a native overlay |
| Platforms | Overlay hooks are no-ops on headless `net10.0` tests |
| Measure without handler | Hosted Editor may measure `Size.Zero` until a platform handler exists |

## Related

[ScrollingAndCollectionViews.md](../design/ScrollingAndCollectionViews.md) · Gallery: `MauiContentViewDemoPage`, "Native overlays in ScrollView" (`OverlayScrollingDemoPage`)
