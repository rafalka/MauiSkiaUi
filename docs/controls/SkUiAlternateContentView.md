# SkUiAlternateContentView

A content view with a second content: `ShowsAlternate` shows the `Content`, the `AlternateContent`, or nothing. Read and edit modes, loaded content and its skeleton, signed in and out.

**MAUI counterpart:** none (MAUI switches with two views and `IsVisible`, or a `DataTrigger` replacing `Content`). Built on [`SkUiContentView`](SkUiContentView.md), so `Padding`, `ContentTemplate` and loading when shown work as there.

## How it works

- `ShowsAlternate` (bindable `bool?`): `false` shows `Content` / `ContentTemplate`, `true` shows `AlternateContent` / `AlternateContentTemplate`, `null` (the default) shows nothing. Only the shown side is attached (measured, drawn, bound); the other is detached and kept, so switching back reuses it. The content properties never change when switching.
- **Templates run on first show:** `ContentTemplate` and `AlternateContentTemplate` create their side only the first time it is shown, so a side that is never shown costs nothing. A `DataTemplateSelector` chooses by the binding context, again when it changes.
- **Animated switches:** with `BeforeStateChangeAnimation` and / or `AfterStateChangeAnimation` (render-thread [`SkUiViewAnimation`](SkUiView.md#key-apis)s; XAML `"FadeOut 120"`, `"FadeIn 200 CubicOut"`, or an element listing the properties), every change of `ShowsAlternate` animates: the shown content runs the first, the new one the second. `ShowsAlternate` takes the new value at once and the content follows; a value set while it animates wins (one set during the "before" animation replaces the pending switch, one set during the "after" animation follows it). `IsSwitching` is `true` meanwhile. The same rules as [`SkUiStateContainer`](SkUiStateContainer.md#animating-every-change), which shares the transition code. Before the view is first drawn, switches apply at once.
- **With `ContentLoading="WhenShown"`** nothing is attached (and no template runs) until the view is first shown; then the side `ShowsAlternate` names.

## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10). Invalid values: `Set*` throws; XAML, bindings, styles and the property setter ignore them with a logged warning, as MAUI does.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`). Of MAUI's `GestureRecognizers`, `TapGestureRecognizer` (1 or 2 taps) runs on the arena; other recognizers are not run and are reported once as a `Trace` line. See [EventMechanism.md](../design/EventMechanism.md#maui-gesture-recognizers).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).

## How to use

```xml
<sk:SkUiAlternateContentView ShowsAlternate="{Binding IsEditing}"
                             BeforeStateChangeAnimation="FadeOut 120" AfterStateChangeAnimation="FadeIn 200 CubicOut">
  <local:ProfileCard />
  <sk:SkUiAlternateContentView.AlternateContentTemplate>
    <DataTemplate>
      <local:ProfileEditor />
    </DataTemplate>
  </sk:SkUiAlternateContentView.AlternateContentTemplate>
</sk:SkUiAlternateContentView>
```

## Key properties

| Property | Default | Notes |
| --- | --- | --- |
| `ShowsAlternate` | `null` | `false`: `Content`; `true`: `AlternateContent`; `null`: nothing |
| `Content` / `ContentTemplate` | — | Inherited from `SkUiContentView` (the `false` side) |
| `AlternateContent` / `AlternateContentTemplate` | — | The `true` side |
| `BeforeStateChangeAnimation` / `AfterStateChangeAnimation` | `null` | Render-thread switch animations |
| `IsSwitching` | `false` | An animated switch is running (read-only) |

## Related

[SkUiContentView](SkUiContentView.md) · [SkUiStateContainer](SkUiStateContainer.md) (more than two states, on any layout) · Gallery: `AlternateContentViewDemoPage` · Tests: `AlternateContentViewTests`; leak scenario `AlternateSwitched`
