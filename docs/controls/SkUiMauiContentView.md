# SkUiMauiContentView

Hosts a real MAUI `VisualElement` (Entry, Editor, WebView, …) as a **native overlay** on the standalone root (FR-16).

**MAUI counterpart:** none (SkiaUi hosting pattern). Do **not** use Skia reimplementations of Entry/Editor/WebView.

## How it works

The placeholder participates in SkiaUi measure/arrange. The wrapped control's platform view is added as a sibling of the Skia surface inside `SkUiOverlayContainer`. `Touch` always returns `false` so SkiaUi never steals native input. Position uses `ComputeRootRelativeFrame()` (Frame offsets + `TranslationX`/`TranslationY` up the hosted ancestor chain).


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** bindables call the direct setter. Direct setters **do not** write back to the bindable store (intentional FR-10 desync). Prefer one update path per property.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's own tap model (`Tapped` / `TappedCommand`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).


## How to use

```xml
<sk:SkUiMauiContentView HeightRequest="160">
  <Editor Placeholder="Native Editor" />
</sk:SkUiMauiContentView>
```

## Key properties

`Content` (`VisualElement`), `SetContent`. Content must be unparented and handlerless when assigned.

## Differences / v1 limits

| Topic | Behavior |
| --- | --- |
| Paint | Does not draw the live control into Skia |
| Transforms | No rotation/scale/opacity composition through ancestors |
| Scroll | Live sync only — **no** snapshot-during-scroll yet |
| Platforms | Overlay hooks are no-ops on headless `net10.0` tests |
| Measure without handler | Hosted Editor may measure `Size.Zero` until a platform handler exists |

## Related

[ScrollingAndCollectionViews.md](../design/ScrollingAndCollectionViews.md) · Gallery: `MauiContentViewDemoPage`
