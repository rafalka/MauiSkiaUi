# SkUiTimePicker

A drawn time picker with **MAUI's `TimePicker` API**: change the XAML prefix of a `TimePicker` and it becomes one. The popup holds wheels for the hour and the minute (and the second), and AM / PM on a 12-hour clock, as the culture says or `WheelFormat` chooses, on your app's popup system. Beyond MAUI, from Syncfusion's `SfTimePicker` and Telerik's `RadTimePicker`: a range (`MinimumTime`, `MaximumTime`), steps (`HourInterval`, `MinuteInterval`, `SecondInterval`), the time the wheels show while there is none (`DefaultHighlightedTime`), headers, OK / Cancel, clearing, and the app's own time UI through the presenter (`SkUiTimePopupRequest`).

**MAUI counterpart:** `TimePicker`. Core twin: `SkUiCoreTimePicker`. The wheels work as the date picker's: [SkUiDatePicker](SkUiDatePicker.md#the-wheels).

## MAUI's rules

- `Time` (`TimeSpan?`, two-way) is **midnight** by default, a time of a day (0 to less than 24 hours; other values are ignored with a warning, `SetTime` throws), and `null` for none (the field shows `Placeholder`, else `Title`).
- `Format` (default `"t"`, the culture's short time) is the field's .NET time format, applied to the time of a day as MAUI's (`"T"`, `"HH:mm"`, `"h:mm tt"`).
- `TimeSelected` (MAUI's `TimeChangedEventArgs`: `OldTime`, `NewTime`) on every change; `IsOpen` with `Opened` / `Closed` of MAUI's types (`TimePickerOpenedEventArgs`, `TimePickerClosedEventArgs`).
- SkiaUi keeps `Time` between `MinimumTime` (default 0:00) and `MaximumTime` (default 23:59:59), as the date picker keeps its date. A time between the wheels' steps stays as the app set it; the wheels show the step before it.

## The wheels

- **The clock.** By default the culture's short time: en-US hours 12, 1, … 11 with an AM / PM wheel; pl-PL hours 00–23. `WheelFormat` chooses: `H` / `HH` a 24-hour clock, `h` / `hh` a 12-hour one (an AM / PM wheel is added when the format has no `tt`), `mm` the minutes, `ss` the seconds (`"T"`: the culture's long time, with seconds).
- **Steps.** `HourInterval` (1–23), `MinuteInterval` and `SecondInterval` (1–59): the wheels show 0, the step, twice the step, … (`MinuteInterval="15"`: 00, 15, 30, 45). The range is taken onto the steps: with quarter hours and a minimum of 9:10, 9:15 is the first time.
- **The range dims what is outside it**: the hours, minutes and AM or PM that cannot be chosen; a choice outside it moves to its end (17:45 with a maximum of 17:30 becomes 17:30).
- The hour, minute and second wheels loop (`IsLooping`, default `true`); AM / PM does not. `AreColumnHeadersVisible` shows "Hour", "Minute", "Second" (`SkUiStrings.HourText`, `MinuteText`, `SecondText`).

## Your own time UI

The typed request is **`SkUiTimePopupRequest`** (a `SkUiColumnsPopupRequest`): `Time`, `DefaultHighlightedTime`, `MinimumTime`, `MaximumTime` (on the steps), `Is24Hour`, `IncludesSeconds`, `HourInterval`, `MinuteInterval`, `SecondInterval`, `CurrentTime` (the draft with `RequiresAccept`); `SetTime(time)` and `CompleteTime(time)` (rounded down to the steps; `false` outside the range), `Clear()`, `Cancel()`, `Accept()`.

## How to use

```xml
<!-- MAUI's TimePicker, with the prefix changed -->
<sk:SkUiTimePicker Time="{Binding Alarm}" Format="HH:mm" />

<!-- Opening hours in quarter hours, chosen with OK, empty with the wheels on 9:00 -->
<sk:SkUiTimePicker Time="{Binding Start}" MinimumTime="08:00" MaximumTime="17:30" MinuteInterval="15"
                   DefaultHighlightedTime="09:00" Placeholder="Choose a time" UpdateMode="WhenFinished" />
```

```csharp
var picker = new SkUiTimePicker().SetMinuteInterval(5).SetWheelFormat("HH:mm");
picker.TimeSelected += (_, args) => Schedule(args.NewTime);

var core = new SkUiCoreTimePicker().SetMinimumTime(TimeSpan.FromHours(8)).SetMinuteInterval(15);
```

## Key properties

| Property | Default | Notes |
| --- | --- | --- |
| `Time` | 0:00 | `TimeSpan?`, two-way; a time of a day, in the range |
| `MinimumTime` / `MaximumTime` | 0:00 / 23:59:59 | The range |
| `Format` | `"t"` | The field's .NET time format |
| `WheelFormat` | `null` | Which wheels show; `null`: the culture's short time |
| `HourInterval`, `MinuteInterval`, `SecondInterval` | 1 | The wheels' steps |
| `DefaultHighlightedTime` | `null` | What the wheels show without a time (`null`: now) |
| `AreColumnHeadersVisible`, `IsLooping`, `VisibleRows` | `false`, `true`, 5 | As the date picker's |
| `TimeSelected` | | MAUI's `TimeChangedEventArgs` |
| `Title`, `Placeholder`, `TextColor`, font properties, `IsOpen` / `Opened` / `Closed`, popup settings, `UpdateMode`, clearing, the app's popup views, `SelectionChangedCommand` | | As `SkUiPicker`'s; `ClearSelection()` sets `Time` to `null` |

Shared conventions: [SkUiView.md](SkUiView.md).
