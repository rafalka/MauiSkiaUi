# SkUiDateTimePicker

A drawn picker of **a day and a time of it** on one popup of wheels (an appointment, a reminder, a departure): day, month and year, then hour, minute and AM / PM, ordered and named by the culture or by `WheelFormat`. MAUI has none. Its API is [SkUiDatePicker](SkUiDatePicker.md)'s with the time kept, plus the time picker's steps.

**MAUI counterpart:** none (MAUI apps pair a `DatePicker` with a `TimePicker`). Core twin: `SkUiCoreDateTimePicker`.

## How it works

- `Date` (`DateTime?`, two-way) is **`null` by default**: the field shows `Placeholder` (else `Title`) and the wheels show `DefaultHighlightedDate` (else now, on the wheels' steps). Its time is kept.
- `MinimumDate` / `MaximumDate` include the time (defaults: the start of 1 January 1900, the end of 31 December 2100): on the first day only the times from the minimum's can be chosen, on the last day those up to the maximum's. `Date` is kept between them; moving them moves it.
- `Format` (default `"g"`, the culture's short date and time) is the field's; `WheelFormat` chooses the wheels (by default the culture's date order with abbreviated month names, and its short time: six wheels in en-US). Parts without a wheel follow the date picker's rule: below the smallest wheel their least (`"d"` wheels choose midnight), above it the value's.
- `HourInterval`, `MinuteInterval`, `SecondInterval` as [SkUiTimePicker](SkUiTimePicker.md#the-wheels)'s; `BlackoutDates` and `DateEnabled` as the date picker's (the time stays when a blocked day moves to the nearest allowed one).
- Screen readers read each wheel as one adjustable element named by its part ("Month", "Hour"), as the date and time pickers' ([SkUiMultiColumnPicker](SkUiMultiColumnPicker.md)).
- `DateSelected` (MAUI's `DateChangedEventArgs`) on every change; `Opened` / `Closed` are the base picker's.
- The typed request is `SkUiDatePopupRequest` with `IncludesTime` `true` ([SkUiDatePicker](SkUiDatePicker.md#presenters-and-your-own-date-ui)).

On a narrow phone six wheels are tight: shorten them with `WheelFormat` (`"d MMM yyyy HH:mm"`) or a 24-hour clock.

## How to use

```xml
<sk:SkUiDateTimePicker Date="{Binding Appointment}" MinimumDate="{Binding Now}" MaximumDate="{Binding InTwoWeeks}"
                       MinuteInterval="15" Placeholder="When?" UpdateMode="WhenFinished" />
```

```csharp
var picker = new SkUiDateTimePicker { MinimumDate = DateTime.Now, MinuteInterval = 30 };
picker.DateEnabled = date => date.DayOfWeek != DayOfWeek.Sunday;

var core = new SkUiCoreDateTimePicker().SetMinuteInterval(15).SetWheelFormat("dd MMM HH:mm");
```

## A calendar over the time wheels

`PopupStyle = Calendar` (1.15) opens a month calendar ([SkUiCalendar](SkUiCalendar.md)) with the picker's time columns as wheels under it (the hour, minutes, seconds and AM / PM its wheel format shows), instead of all the wheels. As with the wheels, each change (a day, a wheel) applies at once and the popup stays open, or, with `UpdateMode="WhenFinished"`, the changes are a draft OK accepts; a tap on a day never closes it, since the time is still to choose.

- The calendar keeps the picker's days (the range, `BlackoutDates`, `DateEnabled`), and the time wheels the range's times on its first and last days: with `MinimumDate` at 9:30 on the 12th, choosing the 12th moves an earlier time to 9:30 and dims the earlier hours.
- `PopupStyle` is `Default` (the look's `DatePickerPopupStyle`: wheels on every built-in look), `Wheels` or `Calendar`; `CalendarStyle` styles the calendar, as on [SkUiDatePicker](SkUiDatePicker.md#a-calendar-instead-of-wheels).
- Keys: the calendar's (arrows, Page Up / Down, Home / End); Tab moves between the calendar and the wheels (arrows then turn the wheels); Enter or Space accept (or close); Escape cancels.
- The request is `SkUiDatePopupRequest` with `IncludesTime`, as with the wheels, so a presenter with its own date and time UI keeps working.

```xml
<sk:SkUiDateTimePicker PopupStyle="Calendar" UpdateMode="WhenFinished" MinuteInterval="15" Date="{Binding Appointment}" />
```

## Key properties

| Property | Default | Notes |
| --- | --- | --- |
| `Date` | `null` | `DateTime?`, two-way; with its time; in the range |
| `MinimumDate` / `MaximumDate` | 1 Jan 1900 0:00 / 31 Dec 2100 23:59:59 | Include the time |
| `Format` | `"g"` | The field's .NET format |
| `WheelFormat`, `DefaultHighlightedDate`, `BlackoutDates`, `DateEnabled` | | As the date picker's |
| `HourInterval`, `MinuteInterval`, `SecondInterval` | 1 | As the time picker's |
| `PopupStyle` / `CalendarStyle` | `Default` / `null` | `Calendar`: the calendar over the time wheels (1.15) |
| `AreColumnHeadersVisible`, `IsLooping`, `VisibleRows` | `false`, `true`, 5 | As the date picker's |
| `DateSelected` | | MAUI's `DateChangedEventArgs` |

Shared conventions: [SkUiView.md](SkUiView.md).

The picker's [field chrome](SkUiPicker.md#field-chrome) applies too: `FieldStyle`, `FillColor`, `BorderColor`, `BorderWidth`, `CornerRadii` / `CornerRadius`.
