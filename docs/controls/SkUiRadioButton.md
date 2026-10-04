# SkUiRadioButton

Radio circle that selects on tap (does not toggle off) and unchecks the rest of its group, with MAUI's `Content` beside it (text or a drawn view), border chrome, and `ControlTemplate` of drawn views.

**MAUI counterpart:** [`RadioButton`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/radiobutton)

## How it works

Extends [`SkUiToggleControl`](SkUiToggleControl.md) but **overrides tap** to select only (never unchecks on re-tap). Checking a radio button, by a tap or from code or a binding, unchecks the others of its group first (their `CheckedChanged` runs before its own), with MAUI's scopes:

- **No `GroupName`:** its siblings in the same parent that have no group name either.
- **A `GroupName`:** every radio button with that name on the page (before the tree is on a page: the layout that names the group, else the parent).

Unchecked radio buttons are written back to their bindable properties, so bindings see them. Intrinsic measure comes from `SkUiLook.Current.DefaultRadioButtonSize` (default 24×24 DIPs). Ring/dot drawn via `SkUiLook.Current.DrawRadioButton` (same path as `SkUiCoreRadioButton`); the default look grows the dot from the center over 160 ms ([transitions](../design/ControlLook.md#state-change-transitions-fr-26)).

**MAUI's `RadioButtonGroup`** attached properties work on any drawn layout (`SkUiLayout` and the layouts derived from it: stacks, grid, flex, wrap, …), with the same markup as in MAUI:
- `GroupName` names the layout's radio buttons that have no group name (also ones added later).
- `SelectedValue` (two-way) is the `Value` of the group's checked radio button. Setting it checks the radio button with an equal `Value`; `null` unchecks the group. A radio button added with a matching `Value` is checked on arrival.
- As in MAUI, the group goes on a layout: on `SkUiContentView`, `SkUiBorder` or `SkUiScrollView` MAUI throws `ArgumentNullException`; put it on the layout inside.

`SkUiCoreRadioButton` is simpler: it does not group itself (no `GroupName`, `Value` or group layout), so checking it leaves other radio buttons alone. Group them where your composition needs it with the `SkUiCoreRadioButtons` helpers:

```csharp
// The radio buttons exclude each other wherever they sit (e.g. each in a row with its label); the callback gets the checked one.
var group = SkUiCoreRadioButtons.Group([basic, pro, team], radio => viewModel.Plan = plans[radio]);
group.Dispose(); // only to stop grouping while the radio buttons stay; a closed screen is collected with its group

SkUiCoreRadioButtons.Uncheck(basic, pro);                       // these radio buttons
column.UncheckRadioButtons(excluded: pro, recursive: true);    // the radio buttons under a node (its children, or its whole subtree)
```

## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10). Invalid values: `Set*` throws; XAML, bindings, styles and the property setter ignore them with a logged warning, as MAUI does.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).


## Content

`Content` is shown beside the circle (the look's `DefaultRadioButtonSize`, then `DefaultRadioButtonContentSpacing`, 8 DIPs):
- **Text:** a string, or any other object as its `ToString()` (as MAUI on Android), drawn by the shared text engine with `TextColor`, `FontSize` (16), `FontFamily`, `FontAttributes`, `CharacterSpacing` and `TextTransform`, wrapped to the width left and centered vertically against the circle; dimmed while disabled.
- **A drawn view** (`ISkUiView`): hosted as the radio button's child (it inherits the binding context) and arranged in the space after the circle, with its own alignment and size. A MAUI view that is not drawn shows as its type name, as on Android: wrap it in an [`SkUiMauiContentView`](SkUiMauiContentView.md).

The whole control is the tap target; a tap on the content selects the radio button. In right-to-left layouts the circle is at the right. Without content the radio button is just the circle, measured and drawn as before.

**Chrome:** `BorderColor`, `BorderWidth` and `CornerRadius` (MAUI's `int`) outline the whole control, circle and content, with the `Background` filling the rounded shape (and casting the `Shadow` when opaque); `Padding` is the space between the border and the circle and content. MAUI's defaults are -1 (`BorderWidth`, `CornerRadius`); SkiaUi's are 0.

```xml
<sk:SkUiVerticalStackLayout RadioButtonGroup.GroupName="plan"
                            RadioButtonGroup.SelectedValue="{Binding Plan}">
  <sk:SkUiRadioButton Content="Basic" Value="Basic" />
  <sk:SkUiRadioButton Content="Pro" Value="Pro" TextColor="#087F83" FontAttributes="Bold" />
  <sk:SkUiRadioButton Value="Team" BorderColor="#087F83" BorderWidth="1" CornerRadius="8" Padding="8,4">
    <sk:SkUiHorizontalStackLayout Spacing="6">
      <sk:SkUiImage Source="team.png" HeightRequest="20" />
      <sk:SkUiLabel Text="Team" VerticalOptions="Center" />
    </sk:SkUiHorizontalStackLayout>
  </sk:SkUiRadioButton>
</sk:SkUiVerticalStackLayout>
```

`Content` is the content property, so a view can be written directly inside the radio button (MAUI needs `<RadioButton.Content>`, which works too).

```csharp
radio.CheckedChanged += (_, args) => { if (args.Value) Selected = radio.Value; };
```

## ControlTemplate

A MAUI `ControlTemplate` whose root is a drawn view replaces the circle, the content and the chrome (MAUI's [redefine RadioButton appearance](https://learn.microsoft.com/dotnet/maui/user-interface/controls/radiobutton#redefine-radiobutton-appearance)). Put an `SkUiContentPresenter` where the content goes: it shows a view content as is, and text as a label styled by the radio button's text properties. The template root gets the `Checked` / `Unchecked` visual states (as in MAUI), so setters with `TargetName` show the checked state; `Padding` surrounds the root.

```xml
<ControlTemplate x:Key="RadioButtonTemplate">
  <sk:SkUiBorder Stroke="#F3F2F1" StrokeThickness="2" StrokeShape="RoundRectangle 10" BackgroundColor="#F3F2F1"
                 HeightRequest="90" WidthRequest="90" HorizontalOptions="Start" VerticalOptions="Start">
    <VisualStateManager.VisualStateGroups>
      <VisualStateGroupList>
        <VisualStateGroup x:Name="CheckedStates">
          <VisualState x:Name="Checked">
            <VisualState.Setters>
              <Setter Property="Stroke" Value="#FF3300" />
              <Setter TargetName="check" Property="Opacity" Value="1" />
            </VisualState.Setters>
          </VisualState>
          <VisualState x:Name="Unchecked">
            <VisualState.Setters>
              <Setter Property="Stroke" Value="#F3F2F1" />
              <Setter TargetName="check" Property="Opacity" Value="0" />
            </VisualState.Setters>
          </VisualState>
        </VisualStateGroup>
      </VisualStateGroupList>
    </VisualStateManager.VisualStateGroups>
    <sk:SkUiGrid Margin="4" WidthRequest="90">
      <sk:SkUiGrid Margin="0,0,4,0" WidthRequest="18" HeightRequest="18" HorizontalOptions="End" VerticalOptions="Start">
        <sk:SkUiEllipse Stroke="Blue" Fill="White" WidthRequest="16" HeightRequest="16" HorizontalOptions="Center" VerticalOptions="Center" />
        <sk:SkUiEllipse x:Name="check" Fill="Blue" WidthRequest="8" HeightRequest="8" HorizontalOptions="Center" VerticalOptions="Center" />
      </sk:SkUiGrid>
      <sk:SkUiContentPresenter />
    </sk:SkUiGrid>
  </sk:SkUiBorder>
</ControlTemplate>

<Style TargetType="sk:SkUiRadioButton">
  <Setter Property="ControlTemplate" Value="{StaticResource RadioButtonTemplate}" />
</Style>
```

This is the template of MAUI's docs with only the prefixes changed. The template must create a drawn view (else `InvalidOperationException`). `TemplateRoot` is the created root; subclasses get `OnApplyTemplate()` and `GetTemplateChild(name)`. Setting `ControlTemplate` to `null` brings back the drawn circle and moves a view content back beside it.

**Bindings to the radio button:** MAUI resolves `{TemplateBinding X}` and `RelativeSource TemplatedParent` only for its own templated views (internal machinery), so they do not reach a drawn radio button. Bind by ancestor type instead: `{Binding Value, Source={RelativeSource AncestorType={x:Type sk:SkUiRadioButton}}}`. Unlike MAUI, the template root inherits the radio button's binding context, so `{Binding}` reaches the page's view model.

## Key properties

`Content`, `TextColor`, `FontSize`, `FontAutoScalingEnabled`, `FontFamily`, `FontAttributes`, `CharacterSpacing`, `TextTransform`, `BorderColor`, `BorderWidth`, `CornerRadius`, `Padding`, `ControlTemplate` (+ `TemplateRoot`), `IsChecked`, `CheckedChanged` (`CheckedChangedEventArgs`), `Color`, `GroupName`, `Value` (+ `SetRadioValue`). `CheckState` (Indeterminate draws a bar; taps only select) comes from [`SkUiToggleControl`](SkUiToggleControl.md). On the parent layout: MAUI's `RadioButtonGroup.GroupName`, `RadioButtonGroup.SelectedValue`.

`SkUiCoreRadioButton` stays a basic circle (no content, chrome or templates): compose it with Core labels and borders.

## Differences from MAUI RadioButton

| Topic | SkiaUi |
| --- | --- |
| Default look | The look's circle and dot (`DrawRadioButton`), not MAUI's default template; `RadioButton.DefaultTemplate` builds MAUI views, which are not drawn |
| `ControlTemplate` | Drawn views; `TemplateBinding` / `RelativeSource TemplatedParent` do not resolve (use `AncestorType`); the root inherits the binding context |
| View content without a template | Drawn beside the circle (MAUI on Android shows its type name; iOS always uses the template) |
| `BorderWidth` / `CornerRadius` defaults | 0 (MAUI: -1) |
| `FontAutoScalingEnabled` | As MAUI's: text content follows the system text size unless `False` |
| Screen readers | A radio button element named by its text content (view content and templates: the text inside them) |
| Visual states | As MAUI's RadioButton: `Checked` / `Unchecked` (also on the template root), then the common states |

## Related

Gallery: `RadioButtonDemoPage` (content, text properties, chrome), `RadioButtonTemplateDemoPage` (`ControlTemplate`)
