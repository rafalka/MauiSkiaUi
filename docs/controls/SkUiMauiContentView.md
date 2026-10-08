# SkUiMauiContentView

Hosts a real MAUI `VisualElement` (Entry, Editor, WebView, …) as a **native overlay** on the standalone root (FR-16).

**MAUI counterpart:** none (SkiaUi hosting pattern). Do **not** use Skia reimplementations of Entry/Editor/WebView.

## How it works

The placeholder participates in SkiaUi measure/arrange. The wrapped control's platform view is added as a sibling of the Skia surface inside `SkUiOverlayContainer`. The node never takes drawn pointers, so taps and text input go to the native control (drags are the exception, below). Position uses `ComputeRootRelativeFrame()` (Frame offsets + `TranslationX`/`TranslationY` up the hosted ancestor chain, minus scroll offsets).

**Clipping:** the overlay is clipped to the viewports of ancestor scrollers and to ancestors with `ClipToBounds`, so a scrolled control never covers drawn content around the scroller and cannot be touched outside it. While an [`SkUiExpander`](SkUiExpander.md) animates, it clips its native content too.

**Visibility:** the native view is hidden while the host or any drawn ancestor is invisible (`IsVisible="False"`, the collapsed content of an expander), and shown again when they are.

**Costs:** each host in a drawn tree watches `IsShown` while parented, so on a page with hosted native views the shown tracker walks the hosts' branches when views are reparented or change visibility, instead of returning at once. Views that move (an animating expander's siblings) walk their subtree to reposition native views only on surfaces that show any.

**Drags that start on the native control** also reach the drawn scroll view around it. Once the drag is clearly a scroll (past the touch slop along the scroller's axis), the native touch is cancelled and the drawn list scrolls. Taps, text selection and cursor placement stay native. Controls that scroll their own content (WebView, Android Editor) keep their native scrolling. Implemented on Android and iOS / Mac Catalyst. On Windows it applies to touch and pen only; mouse drags keep text selection.

**While scrolling** (`ScrollMode`):
- **Auto** (default): snapshot on Android / Windows, live on iOS / Mac Catalyst.
- **Snapshot:** while an ancestor scroller moves, the native view is hidden and a bitmap of it is drawn, so it moves exactly with the drawn content (even during render-thread flings while the UI thread is busy). It is restored `SnapshotRestoreDelay` after scrolling stops. A focused control stays live.
- **Live:** the native view is repositioned on every offset change.

Replacing `Content` while a snapshot shows drops it and captures the new control (or shows it live when it cannot be captured yet). On Windows captures are asynchronous: a capture of an earlier control that completes after a replacement, a restore or a mode change is discarded, never drawn.


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10). Invalid values: `Set*` throws; XAML, bindings, styles and the property setter ignore them with a logged warning, as MAUI does.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`). Of MAUI's `GestureRecognizers`, `TapGestureRecognizer` (1 or 2 taps) runs on the arena; other recognizers are not run and are reported once as a `Trace` line. See [EventMechanism.md](../design/EventMechanism.md#maui-gesture-recognizers).
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
| Native size changes | Measured again when the control gets its platform view; later changes of its native size (an Editor that grows with its text) are not followed: give it a size request |

## Related

[ScrollingAndCollectionViews.md](../design/ScrollingAndCollectionViews.md) · Regression suite and device checklist: [Testing.md](../design/Testing.md#hosted-controls-a6) · Gallery: `MauiContentViewDemoPage`, "Native overlays in ScrollView" (`OverlayScrollingDemoPage`)
