# SkUiContentView

Single-child Skia composition host. Typical outer bridge into a SkiaUi tree.

**MAUI counterpart:** [`ContentView`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/contentview)

## How it works

`[ContentProperty(nameof(Content))]` hosts one `ISkUiView` (`Content`, or one created from `ContentTemplate`). Defaults `HwAccelerated = true`. Forwards measure/arrange/paint/touch to the content and owns a touch router for capture. The content can wait until the view is first shown ([below](#loading-content-when-shown)).


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10). Invalid values: `Set*` throws; XAML, bindings, styles and the property setter ignore them with a logged warning, as MAUI does.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`). Of MAUI's `GestureRecognizers`, `TapGestureRecognizer` (1 or 2 taps) runs on the arena; other recognizers are not run and are reported once as a `Trace` line. See [EventMechanism.md](../design/EventMechanism.md#maui-gesture-recognizers).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).


## How to use

```xml
<sk:SkUiContentView Background="White" HeightRequest="280">
  <sk:SkUiGrid>...</sk:SkUiGrid>
</sk:SkUiContentView>
```

## Key properties

`Content`, `ContentTemplate`, `Padding` (+ `SetContent` / `SetPadding`); `ContentLoading`, `ContentLoadingDelay`, `ContentLoadedAnimation`, `IsContentLoaded`, `ContentLoaded`, `LoadContent()`.

## ContentTemplate

`ContentTemplate` (bindable `DataTemplate` of drawn views) creates the content when `Content` is not set. A `DataTemplateSelector` chooses by the view's `BindingContext`, again whenever it changes (a different template replaces the content; the same one keeps it, rebound). The template runs once the view is in a tree (so properties set before, such as `ContentLoading`, apply first) and its result is assigned to `Content`; a new template replaces it, and explicit `Content` wins. Templates that create native views throw: put them inside an `SkUiMauiContentView`.

## Loading content when shown

`ContentLoading="WhenShown"` attaches the content, and runs `ContentTemplate`, only once the view is first shown, so sections that are never shown (other tabs, hidden panes, collapsed areas) cost nothing: no views created from the template, no measure, no recording, no bindings resolved.

```xml
<sk:SkUiContentView IsVisible="{Binding IsChartTab}" ContentLoading="WhenShown" ContentLoadingDelay="0:0:0.15"
                    ContentLoadedAnimation="FadeIn 200">
  <sk:SkUiContentView.ContentTemplate>
    <DataTemplate>
      <local:SalesChart />
    </DataTemplate>
  </sk:SkUiContentView.ContentTemplate>
</sk:SkUiContentView>
```

- **Shown** is [`SkUiView.IsShown`](SkUiView.md#isshown): the view and its ancestors are `IsVisible` and its tree is on a live surface. Position does not count: a view scrolled out of a scroll view is shown (loading when a view scrolls into the viewport is left to a dedicated control).
- **Before it loads** the view measures as its size requests (`HeightRequest`, `MinimumHeightRequest`).
- `ContentLoadingDelay`: the view must stay shown that long first, so tabs that are only passed through are never created. `ContentLoadedAnimation`: a render-thread `SkUiViewAnimation` run on the new content (`"FadeIn 200"`), so it does not pop in; content shown from a page's first frame loads before anything is drawn and does not animate.
- Explicit `Content` is held (set, not attached) until then; a template is not run at all. `IsContentLoaded` (bindable, read-only) turns `true` and `ContentLoaded` is raised once; `LoadContent()` loads at once, and setting `ContentLoading` back to `Immediate` too.
- Set `ContentLoading` before the view is first drawn: on a view already drawn it has no effect. Content stays loaded once loaded.
- Works on every content view (`SkUiBorder`, `SkUiScrollView`). These properties are plain CLR properties (not bindable): set them in XAML or code, not with bindings or styles.

## Differences from MAUI ContentView

| Topic | SkiaUi |
| --- | --- |
| Child type | Must be `ISkUiView` / `Element` (not arbitrary `View` unless it implements the contract) |
| Surface | Owns the Skia GL/SW surface when standalone |
| Nested MAUI controls | Use [`SkUiMauiContentView`](SkUiMauiContentView.md) |

## Related

[LayoutSystem.md](../design/LayoutSystem.md) · Gallery: `ContentViewDemoPage` (tabs of sections loaded when shown, bindings through templates) · Tests: `DeferredContentTests`; leak scenario `ContentDeferred`
