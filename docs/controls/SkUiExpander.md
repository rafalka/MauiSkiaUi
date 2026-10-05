# SkUiExpander

A header that shows or hides its content when tapped. FAQ answers, settings groups, details rows, nested menus.

**MAUI counterpart:** Community Toolkit `Expander` (`mct:Expander`). Its XAML ports by changing the prefix, with drawn views as `Header` and `Content`. Beyond the toolkit: `ContentTemplate`, `LazyContentExpansion` and an expand / collapse animation (`AnimationLength`).

## How it works

- **Header and content.** `Header` is always shown. `Content` shows while `IsExpanded` is `true`, below the header (`Direction="Down"`, the default) or above it (`Up`). Both are drawn views; put native views (Entry, WebView) in an [`SkUiMauiContentView`](SkUiMauiContentView.md).
- **Tapping the header toggles `IsExpanded`.** Tappable views inside the header (a button) keep their own taps. For screen readers the header is a button whose value is "Expanded" or "Collapsed", and it activates with Space / Enter from the keyboard. `IsExpanded` binds two-way by default, so a bound model sees header taps. (The toolkit's default is one-way, which a header tap would break.)
- **Change notifications.** Each change of `IsExpanded` runs `Command` with `CommandParameter` (if it can execute), then raises `ExpandedChanged` (`SkUiExpandedChangedEventArgs.IsExpanded`), at the change and not when an animation ends. This is the toolkit's order.
- **Layout.** As the toolkit's two `Auto` grid rows: the header and the content are measured with an unbounded height, the width is the wider of the two, and extra height goes to the end. `Padding` insets both. The expander does not clip its children (`ClipToBounds` is `false`, as for content views), so shadows and press effects may overflow.
- **Content templates.** `ContentTemplate` (a `DataTemplate` or `DataTemplateSelector` of drawn views) creates `Content` when it is not set. It runs once the expander is in a tree, or with `LazyContentExpansion` when the expander first expands. A selector picks again when the binding context changes, and a new template replaces the content.
- **`LazyContentExpansion`.**
  - `false` (default): the content is in the tree from the start (bindings resolve, the template runs) and only hidden while collapsed. Hidden content is not measured, drawn or hit-tested, its views report `IsShown = false`, and hosted native views are hidden.
  - `true`: the content is in the tree only while expanded. It is added when the expander expands, a template running the first time, and removed when it has collapsed, so collapsed content binds, loads and draws nothing. Template content is kept for the next expand. Turning it on while collapsed removes the content at once.

### Animation

- `AnimationLength` (milliseconds, default `0` = none) animates every change of `IsExpanded`.
- **The content grows from the header's side.** It is scaled vertically, anchored at its top for `Down` and its bottom for `Up`, while the expander's height follows it. The views around the expander (the rest of a stack, a scroll view's extent) move along. The easing is fixed (`CubicInOut`).
- **`IsExpanded` changes at once and the content follows.** A change while it animates reverses from where it is, taking the remaining part of the length. `IsAnimating` is `true` meanwhile. When a collapse ends, the content is hidden (or removed when lazy).
- **Where it runs.** The height is laid out again on every frame on the UI thread, while the scale is composite-time. Each frame re-records only the expander, and only because its size changed: its content, its ancestors and their shadows are not recorded again.
- **When it does not animate:** before the expander is first drawn, while the system reduces motion ([`SkUiMotion`](../design/AnimationMechanism.md)), or with a length of 0. The change then applies at once.
- **Native content.** Hosted native views cannot be scaled: while the expander animates they are clipped to it, so they are revealed rather than grown.
- The content's own `ScaleY` / `AnchorY` are never touched; the scale is applied to an internal host around it.

## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`expander.SetHeader(h).SetIsExpanded(true)`). Getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10). Invalid values: `Set*` throws; XAML, bindings, styles and the property setter ignore them with a logged warning, as MAUI does.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).

## How to use

```xml
<sk:SkUiExpander IsExpanded="{Binding ShowDetails}" AnimationLength="250" LazyContentExpansion="True">
  <sk:SkUiExpander.Header>
    <sk:SkUiLabel Text="Details" FontAttributes="Bold" />
  </sk:SkUiExpander.Header>
  <sk:SkUiExpander.ContentTemplate>
    <DataTemplate>
      <local:DetailsCard />
    </DataTemplate>
  </sk:SkUiExpander.ContentTemplate>
</sk:SkUiExpander>
```

From the toolkit, change `mct:Expander` to `sk:SkUiExpander` and the header and content views to drawn ones. `ExpandDirection` becomes `SkUiExpandDirection` and `ExpandedChangedEventArgs` becomes `SkUiExpandedChangedEventArgs`; XAML `Direction="Up"` stays as written. The toolkit's `HandleHeaderTapped` (a workaround for resizing rows of native list views) is not needed and does not exist.

## Key properties

| Property | Default | Notes |
| --- | --- | --- |
| `Header` | — | Drawn view; tapping it toggles `IsExpanded` |
| `Content` / `ContentTemplate` | — | Drawn view, or a template that creates it |
| `IsExpanded` | `false` | Two-way by default |
| `Direction` | `Down` | `Down`: content below the header; `Up`: above |
| `Command` / `CommandParameter` | — | Run on every change of `IsExpanded` |
| `LazyContentExpansion` | `false` | Content in the tree only while expanded |
| `AnimationLength` | `0` | Milliseconds; `0` shows and hides at once |
| `Padding` | `0` | Inset around header and content |
| `IsAnimating` | `false` | Expanding or collapsing (read-only) |
| `ExpandedChanged` (event) | — | After `Command`, at each change |

## Related

[SkUiAlternateContentView](SkUiAlternateContentView.md) (one of two contents) · [SkUiContentView](SkUiContentView.md) (content loaded when shown) · Gallery: `ExpanderDemoPage` · Tests: `ExpanderTests`, `ExpanderXamlTests`; leak scenario `ExpanderToggled`
