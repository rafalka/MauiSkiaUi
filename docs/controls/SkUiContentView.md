# SkUiContentView

Single-child Skia composition host. Typical outer bridge into a SkiaUi tree.

**MAUI counterpart:** [`ContentView`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/contentview)

## How it works

`[ContentProperty(nameof(Content))]` hosts one `ISkUiView` (`Content`, or one created from `ContentTemplate`), optionally wrapped by a drawn `ControlTemplate` ([below](#controltemplate)). Defaults `HwAccelerated = true`. Forwards measure/arrange/paint/touch to the content and owns a touch router for capture. The content can wait until the view is first shown ([below](#loading-content-when-shown)).


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

`Content`, `ContentTemplate`, `ControlTemplate` (+ `TemplateRoot`), `Padding` (+ `SetContent` / `SetControlTemplate` / `SetPadding`); `ContentLoading`, `ContentLoadingDelay`, `ContentLoadedAnimation`, `IsContentLoaded`, `ContentLoaded`, `LoadContent()`.

## ContentTemplate

`ContentTemplate` (bindable `DataTemplate` of drawn views) creates the content when `Content` is not set. A `DataTemplateSelector` chooses by the view's `BindingContext`, again whenever it changes (a different template replaces the content; the same one keeps it, rebound). The template runs once the view is in a tree (so properties set before, such as `ContentLoading`, apply first) and its result is assigned to `Content`; a new template replaces it, and explicit `Content` wins. Templates that create native views throw: put them inside an `SkUiMauiContentView`.

## ControlTemplate

`ControlTemplate` (bindable, MAUI's [`ControlTemplate`](https://learn.microsoft.com/dotnet/maui/fundamentals/controltemplate)) wraps the content in a tree of drawn views; an [`SkUiContentPresenter`](SkUiContentPresenter.md) in it shows the content. Use it for a wrapper shared through a style, around content that each instance sets:

```xml
<Style x:Key="Card" TargetType="sk:SkUiContentView">
  <Setter Property="ControlTemplate">
    <ControlTemplate>
      <sk:SkUiBorder StrokeShape="RoundRectangle 12" Padding="12">
        <sk:SkUiVerticalStackLayout Spacing="8">
          <sk:SkUiLabel Text="{Binding Title}" FontAttributes="Bold" />
          <sk:SkUiContentPresenter />
        </sk:SkUiVerticalStackLayout>
      </sk:SkUiBorder>
    </ControlTemplate>
  </Setter>
</Style>

<sk:SkUiContentView Style="{StaticResource Card}">
  <sk:SkUiImage Source="{Binding Photo}" />
</sk:SkUiContentView>
```

- `Content` / `ContentTemplate` decide *what* the content is, `ControlTemplate` *where* it is shown: the template root is the view's child and the content goes into the template's first presenter (a view has one parent). Changing or removing the template moves the same content.
- The template is created when the content loads: with `ContentLoading="WhenShown"` neither exists until the view is first shown, and `ContentLoadedAnimation` runs on the template root.
- Without a presenter in the template no content is shown and `ContentTemplate` does not run.
- The root must be a drawn view (else `InvalidOperationException`). `TemplateRoot` is the created root; subclasses get `OnApplyTemplate()` and `GetTemplateChild(name)`.
- `Padding` (and a border's stroke) surrounds the template root.

### Binding to the templated control

MAUI resolves `{TemplateBinding X}` and `RelativeSource TemplatedParent` only for its own templated views (internal machinery), so they do not reach drawn controls. Bind by ancestor type instead:

```xml
<!-- MAUI -->
<Label Text="{TemplateBinding Title}" />
<!-- SkiaUi -->
<sk:SkUiLabel Text="{Binding Title, Source={RelativeSource AncestorType={x:Type local:CardView}}}" />
```

- Compiled bindings handle it: the XAML source generator (and XamlC when an `x:DataType` is in scope) uses `AncestorType` as the binding's source type and emits a typed binding, with no reflection (trimming / NativeAOT safe). Name the control type that declares the property: a path not found on it falls back to a reflection binding instead of failing the build.
- It finds the nearest ancestor of that type, which from inside a template is the templated control. If the template itself contains another control of the same type between them, add `AncestorLevel`.
- Unlike MAUI, the template root inherits the control's binding context, so `{Binding X}` reaches the view model directly; use `AncestorType` for the control's own properties.

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
- Explicit `Content` is held (set, not attached) until then; a template is not run at all. `IsContentLoaded` (bindable, read-only) turns `true` once the content is attached and `ContentLoaded` is raised once (a template that fails leaves the view waiting and the exception surfaces; a failing load animation ends at its end values); `LoadContent()` loads at once, and setting `ContentLoading` back to `Immediate` too.
- Set `ContentLoading` before the view is first drawn: on a view already drawn it has no effect. Content stays loaded once loaded.
- Works on every content view (`SkUiBorder`, `SkUiScrollView`). These properties are plain CLR properties (not bindable): set them in XAML or code, not with bindings or styles.

## Differences from MAUI ContentView

| Topic | SkiaUi |
| --- | --- |
| Child type | Must be `ISkUiView` / `Element` (not arbitrary `View` unless it implements the contract) |
| Surface | Owns the Skia GL/SW surface when standalone |
| Nested MAUI controls | Use [`SkUiMauiContentView`](SkUiMauiContentView.md) |
| `ControlTemplate` | Drawn views; `TemplateBinding` / `RelativeSource TemplatedParent` do not resolve (use [`AncestorType`](#binding-to-the-templated-control)); the root inherits the binding context; created when the content loads |

## Related

[LayoutSystem.md](../design/LayoutSystem.md) · Gallery: `ContentViewDemoPage` (tabs of sections loaded when shown, bindings through templates) · Tests: `DeferredContentTests`; leak scenario `ContentDeferred`
