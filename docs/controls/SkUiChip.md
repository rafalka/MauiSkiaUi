# SkUiChip and SkUiChipGroup

Chips: compact elements with a text, an optional leading image, a count badge and a remove button, for actions, filters, entered values and suggestions; a group lays them out from items, wrapping or in one scrolling row. Core twins: `SkUiCoreChip`, `SkUiCoreChipGroup`.

**MAUI counterpart:** none (Material's `Chip` / `ChipGroup` on Android; .NET MAUI and the Community Toolkit have none).

## How it works

- **A chip is a button underneath** (`SkUiChip : SkUiButton`, `SkUiCoreChip : SkUiCoreButton`): `Text`, the font properties, `ImageSource` (a leading icon or avatar), `Command` / `CommandParameter`, `Clicked`, press feedback, keyboard focus and `HitSlop` work as on a button. The look draws its chrome instead of the button's (`SkUiLook.DrawChip` with a `SkUiChipPaint`; `ChipPainter` replaces it): the shape (outlined while not selected, filled while selected), the press dim, the check, the badge's pill and the ×.
- **Kinds** (`Kind`, `SkUiChipKind`): `Action` clicks, `Filter` toggles `IsSelected` on a tap (two-way; before `Clicked` runs) and shows a check while selected (no check when it has an image), `Input` shows its × by default, `Suggestion` clicks. All are drawn alike; a look can tell them apart through `SkUiChipPaint.Kind`.
- **Removing:** `ShowsRemoveButton` (default: shown for input chips, until set; clearing it goes back to that). A tap on the × (its column at the end, the full height), a screen reader's "Remove" action (`SkUiStrings.RemoveText`) or Delete / Backspace on the focused chip raises `Removed`, runs `RemoveCommand` with `CommandParameter` and the group's `ChipRemoved`; the app takes the chip or its item away. `Remove()` does the same from code.
- **Badge:** `BadgeText` (a count) in a pill at the end, before the ×, as wide as its text needs (at least a circle); screen readers read it as the value. `BadgeColor` and `BadgeTextColor` (default the scheme's button background and foreground), `BadgeFontSize` (`-1`: the look's `ChipBadgeFontSize`, 11; scaled with the system text size when the chip's text is), `BadgeFontFamily` and `BadgeFontAttributes` (default bold) style it.
- **Colors:** `SelectedColor` (default the accent, light), `StrokeColor` (default a muted line while not selected), `TextColor`, and `BackgroundColor` / `Background` for an unselected chip (a gradient's averaged color). Disabled chips draw at half opacity.
- **Selected look:** while selected and enabled, a chip is in the `Selected` state of `CommonStates`, as MAUI's selected collection items (the order is `Disabled`, `Pressed`, `Selected`, `PointerOver`, `Normal`). A style's `Selected` state sets whatever the selected chip needs, without a `Selected*` property for each: `TextColor`, `FontAttributes`, `SelectedColor`, `StrokeColor`, `BadgeColor`, the image. A group without a `Pressed` state keeps the selected look while pressed (MAUI's visual state manager stays in a state when the next one is missing). Core chips have no visual states: handle `IsSelected` in `PropertyChanged`.
- **Sizes:** the look's `DefaultChipHeight` (32, the least height), `DefaultChipPadding`, `DefaultChipCornerRadius` (8), `ChipIconSize` (18: the check, the × and the badge's least height), `ChipIconSpacing` (6), `ChipStrokeWidth` (1). The check, badge and × take room inside the padding: a filter chip grows by the check's room when selected.
- **Right to left:** the check moves to the right, the badge and × to the left.

### Groups

- `ItemsSource` gives one chip per item (followed when it raises collection changes); `ItemDisplayBinding` gives the texts (Core: `ItemDisplay`, a function). Each chip's binding context and `CommandParameter` are its item, so an implicit `SkUiChip` style can bind to it; `Chips` lists them.
- `ChipKind` (default `Filter`) sets every chip's kind. `SelectionMode` (`SkUiChipSelectionMode`: `None`, `Single`, `Multiple`, default `Multiple`): `Single` keeps one selected in `SelectedItem` (two-way; tapping the selected chip clears it), `Multiple` adds and removes items in `SelectedItems` (a list of the group's, or a view model's through a binding; a read-only list is replaced). `SelectionChanged` (`SkUiSelectionChangedEventArgs`) follows taps and the app's changes; setting a chip's `IsSelected` also moves the group's selection. With a selecting mode, every chip is read as a check box.
- `Wraps` (default `true`): rows like `SkUiWrapLayout`, `Spacing` and `RowSpacing` (8). `false`: one row in a horizontal scroller without a scroll bar; `Padding` is inside the scrolled row, so the first chip lines up with the page's margin and the row still scrolls to the screen's edge.
- Keys: Left / Right (mirrored right to left), Home / End move the keyboard focus between the chips; Tab leaves the group.

## How to use

```xml
<sk:SkUiChipGroup ItemsSource="{Binding Statuses}" SelectedItems="{Binding ChosenStatuses}" />

<sk:SkUiChipGroup ItemsSource="{Binding Periods}" SelectionMode="Single" SelectedItem="{Binding Period}"
                  Wraps="False" Padding="16,0" />

<sk:SkUiChipGroup ItemsSource="{Binding Recipients}" ChipKind="Input" SelectionMode="None"
                  ChipRemoved="OnRecipientRemoved" />

<sk:SkUiChip Text="Requests" BadgeText="{Binding PendingCount}" Command="{Binding OpenRequests}" />
```

```csharp
var filters = new SkUiCoreChipGroup().SetItemsSource(statuses).SetSelectionMode(SkUiChipSelectionMode.Multiple);
filters.SelectionChanged += (_, args) => Apply(args.CurrentSelection);
```

A selected look of the app's own, for every chip:

```xml
<Style TargetType="sk:SkUiChip" ApplyToDerivedTypes="True">
    <Setter Property="VisualStateManager.VisualStateGroups">
        <VisualStateGroupList>
            <VisualStateGroup x:Name="CommonStates">
                <VisualState x:Name="Normal" />
                <VisualState x:Name="Selected">
                    <VisualState.Setters>
                        <Setter Property="TextColor" Value="White" />
                        <Setter Property="FontAttributes" Value="Bold" />
                        <Setter Property="SelectedColor" Value="{StaticResource Primary}" />
                    </VisualState.Setters>
                </VisualState>
                <VisualState x:Name="Disabled" />
            </VisualStateGroup>
        </VisualStateGroupList>
    </Setter>
</Style>
```

## Key properties

Chip: `Kind`, `IsSelected`, `ShowsRemoveButton`, `BadgeText`, `BadgeColor`, `BadgeTextColor`, `BadgeFontSize`, `BadgeFontFamily`, `BadgeFontAttributes`, `SelectedColor`, `StrokeColor`, `RemoveCommand`, `Removed`, `Remove()`, plus the button's. Group: `ItemsSource`, `ItemDisplayBinding` / `ItemDisplay`, `ChipKind`, `SelectionMode`, `SelectedItem`, `SelectedItems`, `Wraps`, `Spacing`, `RowSpacing`, `Padding`, `Chips`, `SelectionChanged`, `ChipRemoved`.

## Not included

A chip template for groups (style the chips with an implicit style instead), chips declared as children of a group, and an animated width change when the check appears. The pickers' token display (`SelectedItemsDisplay="Tokens"`) still draws its own tokens; it can build on chips later.

## Related

[`SkUiSegmentedControl`](SkUiSegmentedControl.md) · [`SkUiButton`](SkUiButton.md) · [`SkUiWrapLayout`](SkUiWrapLayout.md) · Gallery: `ChipsDemoPage`, `ChipGroupDemoPage`, `CoreChipsDemoPage`
