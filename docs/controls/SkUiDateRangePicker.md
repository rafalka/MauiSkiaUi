# SkUiDateRangePicker

A drawn picker of a run of days: the field shows the range ("11/3/2026 – 11/5/2026"), and the popup is a month calendar in range selection. The first tap starts the range, the second ends it and the popup closes. Use it for a stay, a leave request, a report's period or a filter on dates.

**MAUI counterpart:** none (SkiaUi's own control). Core twin: `SkUiCoreDateRangePicker` (fluent `Set*`).

## How it works

- **A value picker of `SkUiDateRange?`.** It is an [`SkUiValuePicker<SkUiDateRange?>`](CustomPickers.md): `Value` (two-way, handler values for the user's choices) is the range, `null` for none; `ValueChanged` reports each change. Clearing (the clear buttons, `ClearSelection()`) sets it to `null`.
- **The field.** `DisplayFormat` formats the range: a `string.Format` composite format in the current culture with the first day as `{0}`, the last day as `{1}` and the number of days as `{2}`. Default `"{0:d} – {1:d}"` (the culture's short dates); `"{0:MMM d} – {1:MMM d}"`, `"{0:d} ({2} days)"`. An open range the app sets (only its first day) formats its last day and count as empty, without trailing spaces; a format that cannot be used falls back to the default. The inherited `ValueFormat` is not used. Without a range the field shows `Placeholder`, else `Title`.
- **The popup.** A [SkUiCalendar](SkUiCalendar.md) in `Range` selection, opened on the range's month (else today's):
  - Without OK to wait for (`UpdateMode="Immediately"`, the default), the first tap starts a range (the picker's value does not change yet) and the second ends it: the popup closes with the range.
  - With `UpdateMode="WhenFinished"`, the range is a draft that OK applies: one day while only its start is chosen (with `MinimumRangeDays` above 1, the last whole range stays the draft until the range ends), then the whole range. Cancel, Escape and a tap outside drop it. The popup's Clear button empties the draft and the calendar and keeps the popup open for another range; OK then applies none (without OK to wait for, Clear clears the picker and closes).
  - Keys go to the calendar (arrows, Page Up / Down, Home / End, Enter / Space choose); waiting for OK, Enter on a day of the whole draft accepts it. Screen readers read each day as an element of its own.
- **Which days.** The date picker's rules: `MinimumDate`, `MaximumDate` (also where the pages stop), `BlackoutDates` and `DateEnabled`. The calendar's range rules: `MinimumRangeDays`, `MaximumRangeDays` (both ends counted) and `AllowsDisabledDaysInRange` (`false`: a range may not run over a day that cannot be chosen). While the range waits for its end, the days that would break a rule are dimmed. The rules are read when the popup opens; the app's own `Value` is not checked against them.
- **Styling the calendar.** `CalendarStyle` is a `Style` with `TargetType="skui:SkUiCalendar"`: colors, fonts, header, `FirstDayOfWeek`, `SelectionShape`, week numbers. The picker sets the selection mode, the range and the rules itself; without a style the calendar takes the picker's font family.
- **Presenters.** The request is `SkUiValuePopupRequest<SkUiDateRange?>`: a presenter with its own UI reads `Value`, calls `SetValue(range)` as the user goes or `Complete(range)` at the end; `Clear()` chooses none.
- Everything else is the picker's ([SkUiPicker](SkUiPicker.md)): `Title`, `Placeholder`, the popup's placement, width and presenter, the app's popup header and footer, the clear buttons, the field chrome, `IsOpen` with `Opened` / `Closed`, keyboard and screen readers.

## How to use

```xml
<skui:SkUiDateRangePicker Title="Stay" Placeholder="Pick your dates"
                          Value="{Binding Stay}" MinimumDate="{Binding Today}"
                          MinimumRangeDays="2" MaximumRangeDays="14"
                          BlackoutDates="{Binding BookedNights}" AllowsDisabledDaysInRange="False"
                          DisplayFormat="{}{0:MMM d} → {1:MMM d} ({2} nights)" />
```

```csharp
var picker = new SkUiDateRangePicker { Title = "Leave", UpdateMode = SkUiPickerUpdateMode.WhenFinished };
picker.DateEnabled = day => day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
picker.ValueChanged += (_, e) => Console.WriteLine(e.NewValue is { } range ? $"{range.Days} days" : "none");

// Core
var core = new SkUiCoreDateRangePicker().SetDisplayFormat("{0:yyyy-MM-dd} / {1:yyyy-MM-dd}").SetMaximumRangeDays(31);
```

## Key properties

| Property | Default | Notes |
| --- | --- | --- |
| `Value` | `null` | `SkUiDateRange?`, two-way |
| `DisplayFormat` | `"{0:d} – {1:d}"` | The field: `{0}` first day, `{1}` last day, `{2}` number of days |
| `MinimumDate` / `MaximumDate` | `null` | Days outside cannot be chosen |
| `BlackoutDates` / `DateEnabled` | empty / `null` | Days that cannot be chosen |
| `MinimumRangeDays` / `MaximumRangeDays` | 0 / 0 | The range's length, both ends counted (0: any) |
| `AllowsDisabledDaysInRange` | `true` | `false`: a range may not run over a day that cannot be chosen |
| `CalendarStyle` | `null` | A `Style` for the popup's calendar |
| `CalendarSystem` | `null` | The popup calendar's calendar system (1.15, [SkUiCalendar](SkUiCalendar.md#how-it-works)); the field's text follows `DisplayFormat` and the culture |
| `Title`, `Placeholder`, `UpdateMode`, `PopupPlacement`, `PopupPresenter`, `IsClearButtonVisible`, `IsPopupClearButtonVisible`, … | | As `SkUiPicker`'s (`SkUiPickerBase`) |

## Limits

- One range. Several ranges are the calendar's (`SelectionMode = MultipleRanges`) on a page.
- The rules apply to the user's taps in the popup; a `Value` the app sets is shown as it is.
