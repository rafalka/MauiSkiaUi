# SkUiDatePicker

A drawn date picker with **MAUI's `DatePicker` API**: change the XAML prefix of a `DatePicker` and it becomes one. The field is drawn as the other pickers' ([SkUiPicker](SkUiPicker.md#look)), and the popup holds **wheels** for the day, the month and the year, ordered, named and formatted by the current culture, on **your app's popup system** (the same presenters as [SkUiPicker](SkUiPicker.md#popups-on-your-popup-system)). Beyond MAUI it takes the best of Syncfusion's `SfDatePicker` and Telerik's `RadDatePicker`: days that cannot be chosen (`BlackoutDates`, `DateEnabled`), the date the wheels show while there is none (`DefaultHighlightedDate`), a wheel format of its own (a month and year picker, `"MM/yy"`), headers, OK / Cancel, a clear button and a placeholder, and the app's own date UI through the presenter (`SkUiDatePopupRequest`).

**MAUI counterpart:** `DatePicker`. Core twin: `SkUiCoreDatePicker` (fluent setters, the same field and wheels). A day and a time of it: [SkUiDateTimePicker](SkUiDateTimePicker.md); a time: [SkUiTimePicker](SkUiTimePicker.md). The three share `SkUiDateTimePickerBase` (Core: `SkUiCoreDateTimePickerBase`) and one engine.

## MAUI's rules

- `Date` (`DateTime?`, two-way) is **today** by default, a day without its time (a time set with it is dropped), and `null` for none: the field then shows `Placeholder` (else `Title`).
- It is kept between `MinimumDate` (default 1 January 1900) and `MaximumDate` (default 31 December 2100); moving them moves it. A minimum after the maximum (or the reverse) is ignored with a warning (`SetMinimumDate` / `SetMaximumDate` throw). `null` means no limit (the year wheel then shows the 200 years before and after the date, else today).
- `Format` (default `"d"`, the culture's short date) is the field's .NET date format, in the current culture (a format it cannot use falls back to `"d"`).
- `DateSelected` (MAUI's `DateChangedEventArgs`: `OldDate`, `NewDate`) is raised on every change, by the user or the app; `SelectionChangedCommand` runs before it.
- `IsOpen` (two-way) with `Opened` / `Closed` of MAUI's types (`DatePickerOpenedEventArgs`, `DatePickerClosedEventArgs`); the base picker's `Opened` / `Closed` are raised too.

## The wheels

- **Which wheels, in which order.** By default the culture's short date order (en-US: month, day, year; pl-PL: day, month, year; ja-JP: year, month, day), with the months' names and four-digit years. `WheelFormat` (a .NET date format, Telerik's `SpinnerFormat`) chooses them: its parts name the wheels, `d` / `dd` the day, `M` / `MM` the month's number, `MMM` / `MMMM` its name, `yy` / `yyyy` the year; separators, quoted text and a weekday (`ddd`) are ignored. Standard formats are the culture's patterns (`"D"`, `"Y"` a month and a year). `"MMMM yyyy"` is a month picker, `"MM/yy"` a card's expiry, `"yyyy"` a year picker.
- **Parts without a wheel.** Below the smallest wheel they are their least: a month and year picker chooses the 1st. Above it they keep the date's (else the highlighted one's): a picker without a year wheel stays in the year it shows.
- **The wheels depend on each other.** Items that cannot be chosen with the other wheels' choice are dimmed: the days a month does not have, the months, days and years outside the range, `BlackoutDates` and the days `DateEnabled` refuses (these two need a day wheel: a month and year picker allows every month). A wheel passes them while it turns without choosing them; one that stops on such an item rolls to the nearest one that can be chosen. A choice that cannot be made moves to the nearest one that can: the 31st of a month turned into February becomes its last day, a date before the minimum the minimum, a blocked day the nearest allowed day (the earlier one first). A wheel still turning is left alone and follows once it stops.
- **Nothing chosen.** With `Date` `null` the wheels show `DefaultHighlightedDate` (else today), kept in the range; turning one wheel (or OK) chooses what all of them show.
- **Choosing**, **keyboard** and **closing** are the wheels' of [SkUiMultiColumnPicker](SkUiMultiColumnPicker.md): a turn, a tap or a key changes the date at once (`UpdateMode="Immediately"`, the default) or as a draft that OK applies (`WhenFinished`); Left / Right change the wheel, Up / Down, Page Up / Down, Home / End and typing move it; the popup's Clear chooses no date and closes. Changing the range, the blackout list, `DateEnabled` or a wheel setting while the popup is open closes it (the next one has the change).
- `AreColumnHeadersVisible` shows "Day", "Month", "Year" above the wheels (from [SkUiStrings](../design/Localization.md): `DayText`, `MonthText`, `YearText`); `IsLooping` (default `true`) lets the day and month wheels loop (years never do); `VisibleRows` (default 5).
- The wheels use the Gregorian calendar; the field's text follows the culture's calendar.

## Presenters and your own date UI

The wheels open on the picker's presenter (`PopupPresenter`, else `SkUiPopups.Presenter`); a presenter shows `request.Content`. To show your own date UI instead (a platform date dialog, a calendar of your own), handle the typed request, **`SkUiDatePopupRequest`** (a `SkUiColumnsPopupRequest`):

| Member | Meaning |
| --- | --- |
| `Date` | The date when the popup opened (`null`: none) |
| `DefaultHighlightedDate` | What to show while `Date` is `null` |
| `MinimumDate`, `MaximumDate` | The range, on the wheels' grid |
| `IsDateEnabled(date)` | In the range, not a blackout date, allowed by `DateEnabled` |
| `IncludesTime`, `Is24Hour`, `HourInterval`, `MinuteInterval`, `SecondInterval` | For a date and time picker |
| `CurrentDate` | The popup's date now (the draft with `RequiresAccept`) |
| `SetDate(date)` | Changes the date (at once, or the draft OK applies); `false` for a date that cannot be chosen |
| `CompleteDate(date)` | Chooses it and closes; `null` chooses none |
| `Clear()`, `Cancel()`, `Accept()` | As every request |

Dates are rounded down to the wheels' grid (a time between two steps to the earlier); a date whose parts without a wheel differ from the kept ones is refused.

## How to use

```xml
<!-- MAUI's DatePicker, with the prefix changed -->
<sk:SkUiDatePicker Date="{Binding Due}" MinimumDate="{Binding Today}" MaximumDate="2026-12-31" Format="D" />

<!-- Holidays and headers, OK / Cancel, empty with the wheels on 1990 -->
<sk:SkUiDatePicker Date="{Binding Birthday}" DefaultHighlightedDate="1990-01-01" Placeholder="Date of birth"
                   BlackoutDates="{Binding Holidays}" AreColumnHeadersVisible="True" UpdateMode="WhenFinished" />

<!-- A card's expiry: month and year -->
<sk:SkUiDatePicker Date="{Binding Expiry}" WheelFormat="MM/yy" Format="MM/yy" />
```

Code and Core:

```csharp
var picker = new SkUiDatePicker { MinimumDate = DateTime.Today, Format = "dd MMM yyyy" };
picker.DateEnabled = date => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
picker.DateSelected += (_, args) => Book(args.NewDate);

var core = new SkUiCoreDatePicker().SetMinimumDate(DateTime.Today).SetWheelFormat("MMMM yyyy");
host.SetContent(core);
```

## Key properties

| Property | Default | Notes |
| --- | --- | --- |
| `Date` | today | `DateTime?`, two-way; the day only; in the range |
| `MinimumDate` / `MaximumDate` | 1 Jan 1900 / 31 Dec 2100 | `null`: no limit |
| `Format` | `"d"` | The field's .NET date format |
| `WheelFormat` | `null` | Which wheels show (a .NET date format); `null`: the culture's |
| `DefaultHighlightedDate` | `null` | What the wheels show without a date (`null`: today) |
| `BlackoutDates` | empty list | Days that cannot be chosen; read when the popup opens |
| `DateEnabled` | `null` | `Func<DateTime, bool>`: the app's rule (not bindable) |
| `AreColumnHeadersVisible` | `false` | "Day", "Month", "Year" above the wheels |
| `IsLooping` | `true` | Day and month wheels loop |
| `VisibleRows` | 5 | Rows of each wheel (1–9) |
| `DateSelected` | | MAUI's `DateChangedEventArgs` |
| `Title`, `Placeholder`, `TextColor`, font properties, alignments, `IsOpen` / `Opened` / `Closed`, `PopupPlacement`, popup sizes, `PopupPresenter`, `UpdateMode`, `AcceptText`, `CancelText`, `IsClearButtonVisible`, `IsPopupClearButtonVisible`, `ClearText`, `PopupHeaderTemplate`, `PopupFooterTemplate`, `SelectionChangedCommand` | | As `SkUiPicker`'s (`SkUiPickerBase`); `ClearSelection()` sets `Date` to `null` |

Not yet: a drawn month calendar as the popup (with `SkUiCalendar`, [Phase F](../design/ImplementationPlan.md#phase-f--calendar-skuicalendar)); screen-reader elements for the wheels.

Shared conventions (DIPs, bindable properties with fluent `Set*` setters, the gesture arena) as for every control: [SkUiView.md](SkUiView.md).
