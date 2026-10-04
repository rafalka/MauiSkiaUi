# SkUiContentPresenter

Shows the `Content` of the drawn control whose `ControlTemplate` contains it: the drawn counterpart of MAUI's `ContentPresenter`, for templates made of drawn views (today [`SkUiRadioButton.ControlTemplate`](SkUiRadioButton.md#controltemplate)).

**MAUI counterpart:** [`ContentPresenter`](https://learn.microsoft.com/dotnet/maui/fundamentals/controltemplate#substitute-content-into-a-contentpresenter)

## How it works

An [`SkUiContentView`](SkUiContentView.md) whose `Content` the templated control sets. When the presenter is attached (or a template is applied), it looks up its ancestors for the nearest control with a template applied, as MAUI does: crossing another presenter on the way skips one templated control, so content shown by an outer template belongs to that one.

- A **drawn view** content is shown as is (in the first presenter only: a view has one parent).
- **Text** content (a string, or any other object as its `ToString()`) is shown in a label the presenter creates, styled by the control's text properties (`TextColor`, `FontSize`, `FontFamily`, `FontAttributes`, `CharacterSpacing`, `TextTransform`), and restyled when they change; every presenter of the template gets one.

Removing the template releases the content: a view goes back to the control (beside the radio circle). A presenter that leaves the template's tree hands a view content to another presenter of it, if any.

## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10). Invalid values: `Set*` throws; XAML, bindings, styles and the property setter ignore them with a logged warning, as MAUI does.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`). Of MAUI's `GestureRecognizers`, `TapGestureRecognizer` (1 or 2 taps) runs on the arena; other recognizers are not run and are reported once as a `Trace` line. See [EventMechanism.md](../design/EventMechanism.md#maui-gesture-recognizers).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).

## How to use

```xml
<ControlTemplate x:Key="Tile">
  <sk:SkUiBorder StrokeShape="RoundRectangle 10" Padding="8">
    <sk:SkUiContentPresenter />
  </sk:SkUiBorder>
</ControlTemplate>

<sk:SkUiRadioButton ControlTemplate="{StaticResource Tile}" Content="Cat" />
```

`Padding` (from `SkUiContentView`) insets the content inside the presenter.

## Differences from MAUI ContentPresenter

| Topic | SkiaUi |
| --- | --- |
| `Content` | Set by the templated control; setting it yourself is overwritten on the next content change |
| Binding | No `RelativeSource TemplatedParent` binding (MAUI resolves those only for its own templated views); the presenter is filled directly by the control |
| Text styling | The control's text properties are always applied to the label it creates (MAUI binds those its label does not set) |

## Related

Gallery: `RadioButtonTemplateDemoPage`
