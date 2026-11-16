# SkUiPickerWheels

The wheels of [SkUiMultiColumnPicker](SkUiMultiColumnPicker.md)'s popup as a view of their own: **always open in the page**, with no field to tap. One snapping wheel per column, the chosen row on a band in the middle, the outer rows fading and turning away. Use it under a live readout ("Distribute **2 weeks** before **due date**"), inside a sheet's card, or anywhere a native app would show an inline wheel picker.

**MAUI counterpart:** none (MAUI shows wheels only inside the platform's date and time pickers). Core twin: none yet. The wheels are built on `SkUiCarouselView`, which has no Core twin ([1.12 plan, D1](../design/ImplementationPlan-1.12.md#decisions)). The popup of [SkUiMultiColumnPicker](SkUiMultiColumnPicker.md), the date and time pickers, and the iOS bottom pane all use the same wheels.

## How it works

- **Columns.** `Columns` holds `SkUiPickerColumn`s (in XAML, the element's content), the same columns as [SkUiMultiColumnPicker](SkUiMultiColumnPicker.md#how-it-works):
  - `ItemsSource`, `ItemDisplayBinding` / `ItemDisplay`, `SkUiPickerColumn.Range(...)`;
  - two-way `SelectedIndex` / `SelectedItem`;
  - `Loop`, `Width`, `Header`, `ItemTemplate`, `ItemEnabledBinding`.
  
  The columns bind to the wheels' binding context.
- **Choosing.** Scrolling a wheel (a drag, a fling, the pointer wheel), tapping an item, the keyboard or a screen reader moves the wheels.
  - What they show is written to the columns once **every wheel has stopped** and `CommitDelay` has passed (default 100 ms; zero: as soon as they stop).
  - Rows passed on the way are never written, so a binding to a view model sees one change per settle, not one per row.
  - A column with nothing chosen shows its first item until the wheels choose. Then every column takes what its wheel shows, so a choice is never half made.
  - Writes are handler values: an app's one-way binding to `SelectedIndex` keeps working after the user turns the wheel.
- **The app's choice** moves the wheels without animating. It wins over a choice that is still waiting to be written.
- **Items that cannot be chosen** (a column's `ItemEnabledBinding`) are dimmed. A wheel passes them, and one that stops on one rolls on to the nearest item that can be chosen.
- **Building.** Changing the columns (adding, removing, replacing), a column's items, `Loop`, `Width`, `Header` or `ItemTemplate`, or `VisibleRows`, `RowHeight`, the font or `TextColor` builds the wheels again. Properties and columns set before the view is in a tree (XAML, object initializers) build them once.
- **Size.** As wide as the columns' widest texts (or their `Width`s) within the width it gets. As tall as `VisibleRows` × `RowHeight` plus the tallest header.
- **Keyboard** (one tab stop):
  - Left / Right change the column (mirrored right to left).
  - Up / Down, Page Up / Page Down (by the visible rows less one) and Home / End move its wheel.
  - Typing jumps to the next item starting with what was typed.
  - Keys write after `CommitDelay` too, so holding a key does not write every row.
- **Screen readers.** Each wheel is one adjustable element, as a native wheel picker's column:
  - It is named by its column's `Header`, or MAUI's `SemanticProperties.Description` set on the column.
  - Its value is the visible item and its place ("weeks, 2 of 4").
  - Increment / decrement step to the next or previous item that can be chosen (VoiceOver swipe up / down, TalkBack's scroll actions, Narrator's range value).

## How to use

```xml
<sk:SkUiVerticalStackLayout Spacing="8">
    <sk:SkUiLabel Text="{Binding Readout}" />
    <sk:SkUiPickerWheels VisibleRows="5">
        <sk:SkUiPickerColumn ItemsSource="{Binding Counts}" SelectedIndex="{Binding CountIndex}"
                             SemanticProperties.Description="Amount" />
        <sk:SkUiPickerColumn ItemsSource="{Binding Units}" SelectedItem="{Binding Unit}"
                             SemanticProperties.Description="Unit" />
    </sk:SkUiPickerWheels>
</sk:SkUiVerticalStackLayout>
```

In code:

```csharp
var wheels = new SkUiPickerWheels()
    .AddColumn(SkUiPickerColumn.Range(1, 99, 1))
    .AddColumn(new SkUiPickerColumn { ItemsSource = new[] { "days", "weeks", "months", "years" } })
    .SetCommitDelay(TimeSpan.FromMilliseconds(150));
wheels.SelectionChanged += (_, args) => UpdateReadout();
```

## Key properties

| Property | Default | Notes |
| --- | --- | --- |
| `Columns` | empty | `SkUiPickerColumn`s; the XAML content |
| `VisibleRows` | 5 | Rows of each wheel (1–9); odd numbers put the chosen row in the middle |
| `RowHeight` | -1 | A row's height; -1: the look's `PickerWheelRowHeight` |
| `CommitDelay` | 100 ms | How long the wheels stay still before the columns are written |
| `TextColor`, `FontFamily`, `FontSize`, `FontAttributes`, `FontAutoScalingEnabled`, `CharacterSpacing` | the look's | The items' text |
| `SelectedIndices` / `SelectedItems` | | One per column (read-only; set the columns) |
| `SelectionChanged` | | `SkUiColumnSelectionChangedEventArgs`: `ColumnIndex`, `PreviousIndex`, `CurrentIndex` |
| `SelectionChangedCommand`, `SelectionChangedCommandParameter` | `null` | Runs before `SelectionChanged` |

The band and the wheel effect are the look's (`PickerWheelBandColor`, `PickerWheelBandCornerRadius`, `PickerWheelEffect`), as in the popup.

Demo: the component gallery's **SkUiPickerWheels** page (Basic controls), with the two schedule-editor layouts.
