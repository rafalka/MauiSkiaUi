# SkUiScrollView

Single-surface scroller with pan, fling, wheel, and programmatic scroll APIs.

**MAUI counterpart:** [`ScrollView`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/scrollview)

## How it works

Extends [`SkUiContentView`](SkUiContentView.md). Measures content unconstrained on enabled axes. **Offset changes invalidate paint only** — content keeps a stable arranged frame and scroll is applied as a canvas/touch translation. The hosted subtree is recorded into an **`SKPicture` content cache** when content (or its arrange) changes; offset-only frames replay that picture so scrolling does not re-record every child. Pan (>10 DIP) cancels child taps. Fling uses the shared animation clock.


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** bindables call the direct setter. Direct setters **do not** write back to the bindable store (intentional FR-10 desync). Prefer one update path per property.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's own tap model (`Tapped` / `TappedCommand`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../../EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../../LayoutSystem.md).


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

[ScrollingAndCollectionViews.md](../../ScrollingAndCollectionViews.md) · Gallery: `ScrollViewDemoPage`
