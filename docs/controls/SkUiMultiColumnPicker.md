# SkUiMultiColumnPicker

A drawn picker with several columns: a field that shows the chosen item of each column, and a popup of **wheels**, one per column, side by side, on **your app's popup system** (the same presenters as [SkUiPicker](SkUiPicker.md#popups-on-your-popup-system)). Use it for any choice made of parts: hours and minutes, an amount and a unit, a month and a year. The date and time pickers (C7) are built on it.

**MAUI counterpart:** none (MAUI's `DatePicker` / `TimePicker` show platform wheels; SkiaUi's own date and time pickers come with C7). Core twin: `SkUiCoreMultiColumnPicker` (fluent setters, the same columns, field and wheels).

## How it works

- **Columns.** `Columns` holds `SkUiPickerColumn`s (in XAML, the element's content). A column has `ItemsSource` (any `IList`; an `INotifyCollectionChanged` one is followed, weakly), `ItemDisplayBinding` (else `ToString()`), `SelectedIndex` / `SelectedItem` (both two-way, -1 / `null`: nothing chosen), `Loop` and `Width`. Columns are bindable objects: in XAML they bind to the picker's binding context. A column can be shared by pickers, and keeps none of them alive.
- **A column's selection** follows `SkUiPicker`'s rules where they fit: an index past the items becomes the last item, an index or an item set before the items applies when they come, a chosen item that moves in a changing source stays chosen, and one that leaves it leaves its index to the next item.
- **Field.** Drawn by the look as the picker's ([Look](SkUiPicker.md#look)): the chosen texts joined by `Separator` (default a space; columns with nothing chosen add nothing), or `Format` (`string.Format` with each column's text as `{0}`, `{1}`, …, an empty text for a column with nothing chosen), or `Title` in `TitleColor` while no column has a choice. One line, truncated at the end.
- **Wheels.** Each column is a vertical wheel: a snapping, virtualized list (an `SkUiCarouselView`) with the chosen item in the middle row, behind a band in the look's `PopupHighlightColor`. Items fade, shrink a little and turn away from the middle as on a drum, drawn by the compositor (scrolling records nothing). A wheel with `Loop` repeats its items without an end (hours); one without stops at its first and last item, which can still reach the middle row. `VisibleRows` (default 5, 1–9) rows show, each `PopupItemHeight` tall. Columns share the popup's width in proportion to their widths (`Width`, else the widest text, measured from the first 200 items).
- **Items that cannot be chosen** (a column's `ItemEnabledBinding`, like `SkUiPicker`'s): dimmed; a wheel passes them while it scrolls without choosing them and, when it stops on one, rolls on to the nearest item that can be chosen; taps on them do nothing; the keyboard and type-ahead pass them by. After the app's data changed, `RefreshItemStates()` on the column reads them again and an open wheel follows, moving off its item when that one became disabled (to the nearest earlier one: the 31st of a month that turned into February becomes the 28th). Presenters read `request.ItemsEnabled` (per column).
- **Choosing.** Scrolling a wheel (a drag, a fling, the pointer wheel), tapping an item, the keyboard, or a presenter's own UI changes that column's `SelectedIndex` at once, as its middle row changes: the field follows while the popup is open, and `SelectionChanged` names the column with its previous and current index. The popup stays open; it closes on a dismissal (the presenter's tap outside, Back), Enter, Space or Escape, keeping what was chosen. With `UpdateMode="WhenFinished"` the wheels' moves are a draft: the columns change once **OK** (or Enter, Space) is pressed, and Cancel, Escape or a dismissal discard them ([SkUiPicker](SkUiPicker.md#when-a-choice-takes-effect): the buttons and their localized texts). A column with nothing chosen shows its first item in the middle row until the user scrolls, taps (also the item already in the middle) or uses a key.
- **Closing.** As the picker's popup: `IsOpen = false`, disabled, hidden, removed, the surface torn down, an anchored popup's scroller scrolled or the window resized. Changing the columns (adding, removing, replacing one, a column's items, `Loop` or `Width`) closes an open popup too: it shows the columns it opened with. A selection the app sets while it is open moves the wheels.
- **Keyboard.** In the popup (it takes focus when shown): Left / Right change the column (mirrored right to left), Up / Down, Page Up / Page Down (by the visible rows less one), Home / End move its wheel (around the end of a looping one), typing jumps to the next item starting with what was typed; Enter, Space or Escape close. On the closed field: a tap, Space, Enter or Alt+Down open; arrow keys do nothing (a choice of parts has no single next item).
- **Screen readers.** The field is a combo box named by `Title`, with the field's text as its value. The wheels are not yet elements of their own (planned with C7).

## Presenters

The wheels open on the presenter of the picker's list ([SkUiPicker](SkUiPicker.md#popups-on-your-popup-system)): `PopupPresenter`, else `SkUiPopups.Presenter`, else MAUI's action sheet, which shows lists of one choice only: without a presenter the wheels close at once, with a trace line. A presenter shows `request.Content` as for a list.

**Showing your own UI instead** (a vendor's wheel picker): read the typed request, `SkUiColumnsPopupRequest`: `Columns` (each column's texts), `Loops`, `SelectedIndices` (one per column, -1: none, following the changes). Call `SetSelectedIndex(column, index)` (or `SetSelection(indices)`) as the user changes a column: the picker applies it at once, or, when `request.RequiresAccept`, keeps it as the draft `request.Accept()` applies. Or call `Complete(indices)` with the final choice, which also closes the popup. Both return `false` for a choice the request does not offer, or once the popup is closing.

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
| `IsClearButtonVisible`, `IsPopupClearButtonVisible`, `ClearText` | `false`, `false`, `null` | Clear every column ([SkUiPicker](SkUiPicker.md#clearing-the-choice)); `ClearSelection()` |
| `UpdateMode`, `AcceptText`, `CancelText` | `Immediately`, `null` | `WhenFinished`: the wheels are a draft until OK ([SkUiPicker](SkUiPicker.md#when-a-choice-takes-effect)) |
| `SelectedIndices` / `SelectedItems` | | One per column (read-only; set the columns) |
| `SelectionChanged` | | `SkUiColumnSelectionChangedEventArgs`: `ColumnIndex`, `PreviousIndex`, `CurrentIndex` |
| `Title`, `TitleColor`, `Placeholder`, `PlaceholderColor`, `TextColor`, font properties, alignments, `IsOpen` / `Opened` / `Closed`, `PopupPlacement`, `PopupBounds`, `PopupWidth`, `MaxPopupHeight`, `PopupPresenter`, `SelectionChangedCommand` (+ parameter) | | As `SkUiPicker`'s (`SkUiPickerBase`); the font is also the wheels' |

| `SkUiPickerColumn` | Default | Notes |
| --- | --- | --- |
| `ItemsSource` | `null` | Any `IList`, followed weakly when it notifies changes |
| `ItemDisplayBinding` | `null` | The text of each item; `ToString()` without it |
| `SelectedIndex` / `SelectedItem` | -1 / `null` | Two-way; kept in step |
| `Loop` | `false` | The wheel repeats its items without an end |
| `ItemEnabledBinding` | `null` | Whether each item can be chosen; `RefreshItemStates()` after the data changed |
| `Width` | -1 | The wheel's width (relative to the other columns'); -1: the widest text |

Shared conventions (DIPs, bindable properties with fluent `Set*` setters, the gesture arena) as for every control: [SkUiView.md](SkUiView.md).
