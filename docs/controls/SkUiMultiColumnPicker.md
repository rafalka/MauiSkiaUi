# SkUiMultiColumnPicker

A drawn picker with several columns: a field that shows the chosen item of each column, and a popup of **wheels**, one per column, side by side, on **your app's popup system** (the same presenters as [SkUiPicker](SkUiPicker.md#popups-on-your-popup-system)). Use it for any choice made of parts: hours and minutes, an amount and a unit, a month and a year. The date and time pickers ([SkUiDatePicker](SkUiDatePicker.md), [SkUiTimePicker](SkUiTimePicker.md), [SkUiDateTimePicker](SkUiDateTimePicker.md)) use its wheels; for a date or a time, use them.

**MAUI counterpart:** none (MAUI's `DatePicker` / `TimePicker` show platform wheels: [SkUiDatePicker](SkUiDatePicker.md) and [SkUiTimePicker](SkUiTimePicker.md) replace them). Core twin: `SkUiCoreMultiColumnPicker` (fluent setters, the same columns, field and wheels).

## How it works

- **Columns.** `Columns` holds `SkUiPickerColumn`s (in XAML, the element's content). A column has `ItemsSource` (any `IList`; an `INotifyCollectionChanged` one is followed, weakly), `ItemDisplayBinding` (else `ToString()`), `SelectedIndex` / `SelectedItem` (both two-way, -1 / `null`: nothing chosen), `Loop` and `Width`. Columns are bindable objects: in XAML they bind to the picker's binding context. A column can be shared by pickers, and keeps none of them alive.
- **A column's selection** follows `SkUiPicker`'s rules where they fit: an index past the items becomes the last item, an index or an item set before the items applies when they come, a chosen item that moves in a changing source stays chosen, and one that leaves it leaves its index to the next item.
- **Field.** Drawn by the look as the picker's ([Look](SkUiPicker.md#look)): the chosen texts joined by `Separator` (default a space; columns with nothing chosen add nothing), or `Format` (`string.Format` with each column's text as `{0}`, `{1}`, …, an empty text for a column with nothing chosen), or `Title` in `TitleColor` while no column has a choice. One line, truncated at the end.
- **Headers and item templates.** A column's `Header` ("Hour", "Minute") shows above its wheel in the look's muted color. A column's `ItemTemplate` (drawn `SkUi*` views, a selector per item) shows its items instead of the texts, bound to the column's items; the root of the item in the middle row is in the `Selected` visual state while it is there (as the list picker's selected row), items that cannot be chosen are dimmed and their roots are in `Disabled` (not dimmed when the root defines a `Disabled` state: that is their look then).
- **Numbers in steps.** `SkUiPickerColumn.Range(start, end, step, format)` makes a column of whole numbers (`Range(0, 55, 5, "00")`: minutes every five), shown with a .NET number format in the current culture; its items are `int`s, so `SelectedItem = 30` works. `ItemDisplay` (a function of the item) sets the texts of columns built in code without a binding.
- **Wheels.** Each column is a vertical wheel: a snapping, virtualized list (an `SkUiCarouselView`) with the chosen item in the middle row, behind a band in the look's `PopupHighlightColor`. Items fade, shrink a little and turn away from the middle as on a drum, drawn by the compositor (scrolling records nothing). A wheel with `Loop` repeats its items without an end (hours); one without stops at its first and last item, which can still reach the middle row. `VisibleRows` (default 5, 1–9) rows show, each the look's `PickerWheelRowHeight` tall (default `PopupItemHeight`); the look sets the band (`PickerWheelBandColor`, `PickerWheelBandCornerRadius`) and the items' effect (`PickerWheelEffect`). Columns share the popup's width in proportion to their widths (`Width`, else the widest text, measured from the first 200 items).
- **Items that cannot be chosen** (a column's `ItemEnabledBinding`, like `SkUiPicker`'s): dimmed; a wheel passes them while it scrolls without choosing them and, when it stops on one, rolls on to the nearest item that can be chosen; taps on them do nothing; the keyboard and type-ahead pass them by. After the app's data changed, `RefreshItemStates()` on the column reads them again and an open wheel follows, moving off its item when that one became disabled (to the nearest earlier one: the 31st of a month that turned into February becomes the 28th). Presenters read `request.ItemsEnabled` (per column).
- **Choosing.** Scrolling a wheel (a drag, a fling, the pointer wheel), tapping an item, the keyboard, or a presenter's own UI changes that column's `SelectedIndex` at once, as its middle row changes: the field follows while the popup is open, and `SelectionChanged` names the column with its previous and current index. The popup stays open; it closes on a dismissal (the presenter's tap outside, Back), Enter, Space or Escape, keeping what was chosen. With `UpdateMode="WhenFinished"` the wheels' moves are a draft: the columns change once **OK** (or Enter, Space) is pressed, and Cancel, Escape or a dismissal discard them ([SkUiPicker](SkUiPicker.md#when-a-choice-takes-effect): the buttons and their localized texts). A column with nothing chosen shows its first item in the middle row; once the user chooses in any wheel (scrolls, taps, also the item already in the middle, or uses a key), the columns with nothing chosen take the item their wheel shows, so the field never shows half a choice ("01:"). With `WhenFinished`, OK likewise chooses what the wheels show. The popup's Clear chooses nothing in every column and closes, also with `WhenFinished` (the wheels cannot show a cleared draft).
- **Closing.** As the picker's popup: `IsOpen = false`, disabled, hidden, removed, the surface torn down, an anchored popup's scroller scrolled or the window resized. Changing the columns (adding, removing, replacing one, a column's items, `Loop` or `Width`) closes an open popup too: it shows the columns it opened with. A selection the app sets while it is open moves the wheels.
- **Keyboard.** In the popup (it takes focus when shown): Left / Right change the column (mirrored right to left), Up / Down, Page Up / Page Down (by the visible rows less one), Home / End move its wheel (around the end of a looping one), typing jumps to the next item starting with what was typed; Enter, Space or Escape close. On the closed field: a tap, Space, Enter or Alt+Down open; arrow keys do nothing (a choice of parts has no single next item).
- **Screen readers.** The field is a combo box named by `Title`, with the field's text as its value. The wheels are not yet elements of their own (planned).

## Presenters

The wheels open on the presenter of the picker's list ([SkUiPicker](SkUiPicker.md#popups-on-your-popup-system)): `PopupPresenter`, else `SkUiPopups.Presenter`, else MAUI's action sheet, which shows lists of one choice only: without a presenter the wheels close at once, with a trace line. A presenter shows `request.Content` as for a list.

**Showing your own UI instead** (a wheel picker of your own): read the typed request, `SkUiColumnsPopupRequest`: `Columns` (each column's texts), `Loops`, `SelectedIndices` (one per column, -1: none, following the changes). Call `SetSelectedIndex(column, index)` (or `SetSelection(indices)`) as the user changes a column: the picker applies it at once, or, when `request.RequiresAccept`, keeps it as the draft `request.Accept()` applies. Or call `Complete(indices)` with the final choice, which also closes the popup. Both return `false` for a choice the request does not offer, or once the popup is closing.

## How to use

```xml
<sk:SkUiMultiColumnPicker Title="Pick a time" Format="{}{0}:{1} {2}">
  <sk:SkUiPickerColumn ItemsSource="{Binding Hours}" SelectedIndex="{Binding Hour}" Loop="True" />
  <sk:SkUiPickerColumn ItemsSource="{Binding Minutes}" SelectedItem="{Binding Minute}" Loop="True" />
  <sk:SkUiPickerColumn ItemsSource="{Binding DayParts}" SelectedItem="{Binding DayPart}" />
</sk:SkUiMultiColumnPicker>
```

Objects with a display binding, and a fixed column width:

```xml
<sk:SkUiMultiColumnPicker Title="Amount" Separator=" ">
  <sk:SkUiPickerColumn ItemsSource="{Binding Amounts}" SelectedItem="{Binding Amount}" Width="80" />
  <sk:SkUiPickerColumn ItemsSource="{Binding Units}" ItemDisplayBinding="{Binding Symbol}" SelectedItem="{Binding Unit}" />
</sk:SkUiMultiColumnPicker>
```

Code and Core:

```csharp
var picker = new SkUiMultiColumnPicker { Title = "Time", Separator = ":" }
    .AddColumn(new SkUiPickerColumn { ItemsSource = hours, Loop = true })
    .AddColumn(new SkUiPickerColumn { ItemsSource = minutes, Loop = true });
picker.SelectionChanged += (_, args) => Apply(args.ColumnIndex, args.CurrentIndex);

var core = new SkUiCoreMultiColumnPicker().SetTitle("Time").SetSeparator(":")
    .AddColumn(new SkUiPickerColumn { ItemsSource = hours, Loop = true })
    .AddColumn(new SkUiPickerColumn { ItemsSource = minutes, Loop = true });
host.SetContent(core);
```

## Key properties

| Property | Default | Notes |
| --- | --- | --- |
| `Columns` | empty | `SkUiPickerColumn`s; the XAML content |
| `Separator` | `" "` | Between the chosen texts; ignored with `Format` |
| `Format` | `null` | `string.Format` of the field, `{0}`… per column |
| `VisibleRows` | 5 | Rows of each wheel (1–9) |
| `PopupHeaderTemplate`, `PopupFooterTemplate` | `null` | The app's views above and below the wheels ([SkUiPicker](SkUiPicker.md#the-apps-views-in-the-popup)) |
| `IsClearButtonVisible`, `IsPopupClearButtonVisible`, `ClearText` | `false`, `false`, `null` | Clear every column ([SkUiPicker](SkUiPicker.md#clearing-the-choice)); `ClearSelection()` |
| `UpdateMode`, `AcceptText`, `CancelText` | `Immediately`, `null` | `WhenFinished`: the wheels are a draft until OK ([SkUiPicker](SkUiPicker.md#when-a-choice-takes-effect)) |
| `SelectedIndices` / `SelectedItems` | | One per column (read-only; set the columns) |
| `SelectionChanged` | | `SkUiColumnSelectionChangedEventArgs`: `ColumnIndex`, `PreviousIndex`, `CurrentIndex` |
| `Title`, `TitleColor`, `Placeholder`, `PlaceholderColor`, `TextColor`, font properties, alignments, `IsOpen` / `Opened` / `Closed`, `PopupPlacement`, `PopupBounds`, `PopupWidth`, `MinPopupWidth`, `MaxPopupWidth`, `MaxPopupHeight`, `PopupPresenter`, `SelectionChangedCommand` (+ parameter) | | As `SkUiPicker`'s (`SkUiPickerBase`); the font is also the wheels' |

| `SkUiPickerColumn` | Default | Notes |
| --- | --- | --- |
| `ItemsSource` | `null` | Any `IList`, followed weakly when it notifies changes |
| `ItemDisplayBinding` | `null` | The text of each item; `ToString()` without it |
| `SelectedIndex` / `SelectedItem` | -1 / `null` | Two-way; kept in step |
| `Loop` | `false` | The wheel repeats its items without an end |
| `Header` | `null` | Shown above the wheel |
| `ItemTemplate` | `null` | Drawn items bound to the column's items; the middle one's root `Selected` |
| `ItemDisplay` | `null` | The texts as a function of the item (code), when there is no `ItemDisplayBinding` |
| `ItemEnabledBinding` | `null` | Whether each item can be chosen; `RefreshItemStates()` after the data changed |
| `Width` | -1 | The wheel's width (relative to the other columns'); -1: the widest text |

Shared conventions (DIPs, bindable properties with fluent `Set*` setters, the gesture arena) as for every control: [SkUiView.md](SkUiView.md).
