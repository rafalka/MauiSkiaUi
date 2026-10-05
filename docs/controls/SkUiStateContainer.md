# SkUiStateContainer

Attached properties that make any drawn layout state-aware: while `CurrentState` names a state, that state's view (loading, empty, error…) replaces the layout's children; clearing it puts the children back.

**Counterpart:** the Community Toolkit's `StateContainer` / `StateView` (`CommunityToolkit.Maui.Layouts`). SkiaUi does not reference the toolkit, so the drawn version has its own types with the same API: toolkit XAML ports by changing the prefixes.

| Toolkit | SkiaUi |
| --- | --- |
| `mct:StateContainer.StateViews` | `sk:SkUiStateContainer.StateViews` |
| `mct:StateContainer.CurrentState` | `sk:SkUiStateContainer.CurrentState` |
| `mct:StateContainer.CanStateChange` | `sk:SkUiStateContainer.CanStateChange` |
| `mct:StateView.StateKey` | `sk:SkUiStateView.StateKey` |
| `StateContainer.ChangeStateWithAnimation(layout, state, token)` | Same (render-thread fade) |
| `ChangeStateWithAnimation(layout, state, Animation, Animation, token)` | `ChangeStateWithAnimation(layout, state, SkUiViewAnimation, SkUiViewAnimation, token)`: [convert each `Animation`](../Migration.md#7-animations) |
| `ChangeStateWithAnimation(layout, state, Func, Func, token)` | Same (call `AnimateAsync` inside instead of `FadeToAsync`) |
| `StateContainerException` | `SkUiStateContainerException` |
| — | `sk:SkUiStateContainer.BeforeStateChangeAnimation` / `AfterStateChangeAnimation`: animate every change without code |

## How it works

- Works on `SkUiLayout` and every layout built on it (grid, stacks, absolute, flex, wrap, shrink stacks). Other views throw `SkUiStateContainerException`, as the toolkit does for non-`Layout`s.
- `StateViews` is a list of drawn views, each with a unique `SkUiStateView.StateKey`. A state view joins the tree only while it is shown, so its bindings inherit the layout's `BindingContext` then.
- Setting `CurrentState` to a key moves the layout's children aside and shows that state's view; switching between states swaps the views; `null` or empty restores the children in their order. On a `SkUiGrid` the state view spans every row and column.
- `ChangeStateWithAnimation(layout, state)` fades: the shown views fade out, the state changes, the new views fade in to their own `Opacity` (`SkUiViewAnimation.FadeOut()` / `FadeIn()`).
- `ChangeStateWithAnimation(layout, state, before, after)` takes any `SkUiViewAnimation`s (opacity, translation, rotation, scale with optional start and end values, length, easing): `before` runs on each shown view, `after` on each new view. They run on the render thread, so they stay smooth while the UI thread builds the new state. Views that leave get their own values back and new views end at the animation's end values, exactly, also when the change is cancelled. Off screen the change applies at once.
- The function overload calls your code with the layout before and after the change; use `AnimateAsync` there for render-thread motion.
- `CanStateChange` is `false` while an animated change runs; setting `CurrentState` then throws `SkUiStateContainerException`. Bind it one way to source (its default mode) to disable commands meanwhile. A cancelled change still ends in the requested state.

## Animating every change

Set the state change animations once and every change of `CurrentState` (a binding, code, a style or a trigger) animates on the render thread, with no `ChangeStateWithAnimation` call:

```xml
<sk:SkUiGrid sk:SkUiStateContainer.CurrentState="{Binding State}"
             sk:SkUiStateContainer.BeforeStateChangeAnimation="FadeOut 150 CubicIn">
  <sk:SkUiStateContainer.AfterStateChangeAnimation>
    <sk:SkUiViewAnimation Length="250" Easing="CubicOut">
      <sk:SkUiPropertyAnimation Property="TranslationY" From="24" />
      <sk:SkUiPropertyAnimation Property="Opacity" From="0" />
    </sk:SkUiViewAnimation>
  </sk:SkUiStateContainer.AfterStateChangeAnimation>
  …
</sk:SkUiGrid>
```

- `BeforeStateChangeAnimation` runs on the shown views, `AfterStateChangeAnimation` on the new ones; either can be left out. The text form is `"FadeIn"` / `"FadeOut"`, optionally followed by a length in milliseconds and an easing. One animation (a style setter, a resource) can be shared by many layouts: it becomes read-only once used.
- `CurrentState` takes the new value at once, so a binding always reads its own value; the views follow. Changes never throw while views animate: a state set during the "before" animation replaces the pending one (the state in between is never shown), one set during the "after" animation follows when it ends.
- `CanStateChange` is `false` until the views catch up. An explicit `ChangeStateWithAnimation` meanwhile throws, as in the toolkit (check `CanStateChange` first); setting `CurrentState` does not.
- Off screen (including the first state, set before the page appears) changes apply at once.
- Difference to the explicit call: `ChangeStateWithAnimation` records `CurrentState` at the end, as the toolkit does, and throws when a change runs; automatic changes record it at the start and coalesce.

**Differences from the toolkit (fixes):**

- Invalid changes (unknown or duplicate key, a native state view, a change while one runs) throw before the value is stored. The toolkit throws from a property-changing callback, which leaves MAUI's per-property "being set" flag on, so later values of `CurrentState` on that layout are silently dropped.
- An empty `CurrentState` before any state keeps the children (the toolkit clears them).
- The fade also fades the new state view in (the toolkit fades only the returning children in), and views keep their own opacity.
- No MAUI `Animation` overload: MAUI animations tick on the UI thread and set bindable properties every frame. `SkUiViewAnimation` replaces it with a one-line conversion per animated property ([Migration.md](../Migration.md#7-animations)).

## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10). Invalid values: `Set*` throws; XAML, bindings, styles and the property setter ignore them with a logged warning, as MAUI does.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`). Of MAUI's `GestureRecognizers`, `TapGestureRecognizer` (1 or 2 taps) runs on the arena; other recognizers are not run and are reported once as a `Trace` line. See [EventMechanism.md](../design/EventMechanism.md#maui-gesture-recognizers).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).

## How to use

```xml
<sk:SkUiVerticalStackLayout sk:SkUiStateContainer.CurrentState="{Binding State}"
                            sk:SkUiStateContainer.CanStateChange="{Binding CanStateChange}">
  <sk:SkUiStateContainer.StateViews>
    <sk:SkUiHorizontalStackLayout sk:SkUiStateView.StateKey="Loading" Spacing="10">
      <sk:SkUiActivityIndicator IsRunning="True" WidthRequest="20" HeightRequest="20" />
      <sk:SkUiLabel Text="Loading…" />
    </sk:SkUiHorizontalStackLayout>
    <sk:SkUiLabel sk:SkUiStateView.StateKey="Empty" Text="Nothing here yet" />
  </sk:SkUiStateContainer.StateViews>

  <sk:SkUiLabel Text="The content" />
</sk:SkUiVerticalStackLayout>
```

```csharp
if (SkUiStateContainer.GetCanStateChange(layout))
    await SkUiStateContainer.ChangeStateWithAnimation(layout, "Loading");

// Shrink and fade out, then slide the new state in from below.
var leave = new SkUiViewAnimation([new(SkUiAnimatableProperty.Scale, to: 0.9), new(SkUiAnimatableProperty.Opacity, to: 0)], 150, Easing.CubicIn);
var enter = new SkUiViewAnimation([new(SkUiAnimatableProperty.TranslationY, from: 24), new(SkUiAnimatableProperty.Opacity, from: 0)], 250, Easing.CubicOut);
await SkUiStateContainer.ChangeStateWithAnimation(layout, null, leave, enter);
```

## Notes

- Set `StateViews` before `CurrentState` names one of them: naming a state that is not there throws, as in the toolkit.
- Edit the state views with `GetStateViews(layout)` (add, remove, clear, replace); the default list is observed (a list you set is observed when it raises collection changes). A state view replaced while shown is shown, and a state that could not be shown (its view had left the list during an animated change) is shown once its view is back. A list set with `SetStateViews` replaces the default list, which MAUI keeps with the layout.
- Children added to the layout while a state is shown are kept and restored after the original children. Do not put a `BindableLayout` on the state container itself (it addresses children by index): put it inside the content.
- An animated automatic change that fails (an animation error, a state view removed meanwhile) still ends with the views following `CurrentState`, switched without animating.
- Native views as states go inside a `SkUiMauiContentView`.

## Related

Gallery: `StateContainerDemoPage` (a grid with loading, error and empty states). Tests: `StateContainerTests`; leak scenario `StatesSwitched`.
