# SkUiCalendar

A drawn calendar: a month of days, the months of a year, the years of a decade or the decades of a century, with a header, day names, today, and a selection of one day, several days or a range. Use it on a page for bookings, schedules, habit trackers and date ranges, as a weekly strip above a day's agenda, or as a month picker.

**MAUI counterpart:** none (MAUI has no calendar; the API is SkiaUi's own, with the date picker's names for the day rules). Core twin: `SkUiCoreCalendar`, the same engine and the same drawing (fluent `Set*`).

## How it works

- **One drawn view.** The calendar is a single node: no view per day. The look draws each cell's chrome (`SkUiLook.DrawCalendarCell`: the selection, a range's band, today's mark, the app's background and marker dots, the keyboard focus) and the header's buttons; the calendar draws the texts through the shared text engine and keeps their layouts, so day numbers, day names and month names are laid out once.
- **Pages and display modes.** `DisplayMode` is `Month` (days), `Year` (months), `Decade` (years, with the year before and after) or `Century` (decades). `DisplayDate` is a date of the page shown.
  - The header's buttons, a swipe and `GoToPrevious()` / `GoToNext()` move a page; the page slides with the look's `SkUiTransitionKind.CalendarPage` transition (none with reduce motion). A swipe follows the finger and settles on the next page past a third of the page or after a fling, else back; past the first or last page it stretches a little.
  - A tap on the title steps up a mode; a tap on a month, year or decade steps down to it; a pinch zooms out or in. At the highest mode, the title goes back to the lowest. `MinimumDisplayMode` and `MaximumDisplayMode` limit the modes: `MaximumDisplayMode = Month` keeps the calendar on days, `MinimumDisplayMode = Year` makes a month picker whose month cells are chosen (their first day).
  - The user's changes are written back to `DisplayDate` and `DisplayMode` (two-way, handler values: an app's one-way binding keeps applying).
- **Weeks.** Six weeks by default, so a month page always has the same height. `VisibleWeeks` (1 to 5) shows that many weeks and pages by weeks: a weekly strip. `FirstDayOfWeek` (default: the culture's) sets the first column. `AreAdjacentDaysVisible` shows the neighbor months' days, dimmed (a tap on one chooses it and slides its month in); a selection or a range on them is drawn as on the month's own days, so a range that crosses the month's end runs on into them. `AreWeekNumbersVisible` adds a column of week numbers (ISO 8601 for Monday weeks under the "first four-day week" rule, else the culture's rule); a tap on one raises `WeekNumberTapped` with the week's first and last days, its number, the first and last days that can be chosen and ready-made ranges (`Range`, `EnabledRange`), so the app can select the whole week.
- **Day names.** `DayNameFormat` is `Shortest` ("Tu"), `Abbreviated` ("Tue") or `FirstLetter` ("T"). The names come from `SkUiStrings.Current`: its `ShortestDayNames`, `AbbreviatedDayNames` and `FirstLetterDayNames` (seven names from Sunday) when set, else the current culture's; override `SkUiStrings.GetDayName` to read the app's resources. A change of the strings redraws the calendars. While the page shows today, today's day name is drawn in `TodayDayNameTextColor` (default: `TodayColor`, the accent); `IsTodayDayNameHighlighted = false` turns it off.
- **Selection.** `SelectionMode`:
  - `Single` (default): `SelectedDate`. A tap on the selected day keeps it, unless `AllowsDeselection`.
  - `Multiple`: `SelectedDates`, in date order; a tap adds a day or removes it. The user's changes write a new list, so a binding sees each change; a list the app sets is read when set, not followed.
  - `Range`: `SelectedRange` (`SkUiDateRange`: `Start`, `End`, `IsComplete`, `LastDay`, `Days`, `Contains`). The first tap starts an open range (its `End` is `null`), the second ends it (in either order), a third starts a new one.
  - **Which end a tap sets** (`RangeSelectionTarget`, two-way): `Auto` (default) as above; `Start` sets the first day (a last day before it is cleared) and then moves the target to `End`; `End` sets the last day (a day before the first becomes the first, the old first the last) and stays. Bind it to a "Start" / "End" pair of fields above the calendar, so the user picks which end the next tap changes.
  - `MultipleRanges`: `SelectedRanges` (two-way, in date order). A tap outside the chosen ranges starts a range, the next tap ends it, and ranges that overlap or touch join into one; a tap on a chosen range removes it. `SelectedRange` is the range changed last; a list the app sets is ordered and joined.
  - **Range rules** (range and multiple ranges, the user's taps only): `MinimumRangeDays` and `MaximumRangeDays` (both ends counted: a stay of at least two nights is 3; 0 is no limit) and `AllowsDisabledDaysInRange` (default `true`; `false`: a range may not run over a day that cannot be chosen, as a booking over a booked night). While a range waits for its end, the days that would break a rule are dimmed and cannot be tapped; a tap on the range's own first day keeps it open. The app's own ranges are not checked.
  - `None`: taps only raise `DateTapped`.
  - `SelectedDate` is also the last day chosen in the other modes (a range's first day). `SelectionChanged` is raised for the user's changes and the app's, with `AddedDates` / `RemovedDates`, or `OldRange` / `NewRange`. `ClearSelection()` clears every mode.
- **Day rules** (the date picker's): `MinimumDate` and `MaximumDate` (also where the pages stop), `BlackoutDates` (an empty observable list by default; changes to an observable list are followed) and `DateEnabled` (a function). Days they rule out are dimmed and cannot be chosen; a tap on one raises `DateTapped` with `IsEnabled = false`, so the app can explain why.
- **Header.** `HeaderLayout` is `TitleFirst` (the title at the start, both buttons at the end: compact, suits a phone) or `Centered` (the buttons around a centered title). The title is the month and year (`TitleFormat`, else the culture's year-month pattern), the year, or the first and last years. `IsHeaderVisible = false` hides it; build a header of your own with `Title`, `CanGoToPrevious`, `CanGoToNext`, `GoToPrevious()`, `GoToNext()` and `DisplayMode`.
- **Header and buttons.** The title has its own `HeaderFontFamily`, `HeaderFontSize`, `HeaderFontAttributes` and `HeaderTextColor`; the header its `HeaderHeight` (the look's 48) and `HeaderPadding` (8 at the sides). The buttons take `NavigationButtonSize` (the look's 36), `NavigationButtonSpacing` (4), `NavigationButtonColor`, `NavigationButtonBackgroundColor`, `NavigationButtonCornerRadius` (a circle by default), `NavigationButtonBorderColor` and `NavigationButtonBorderWidth`. Their picture is the look's chevron, or:
  - an icon font glyph: `PreviousButtonGlyph` / `NextButtonGlyph` in `NavigationButtonFontFamily`, colored by `NavigationButtonColor`;
  - an image: `PreviousButtonIcon` / `NextButtonIcon` (a PNG, an SVG, a URL: any image source, through the shared loader and cache; tint an SVG with `SkUiImageSource.FromVector(source, color)`), drawn at `NavigationButtonGlyphSize` (half the button by default);
  - both are mirrored right to left and dimmed while pressed or disabled.
  - **Drawn by a subclass:** override `OnDrawNavigationButton(canvas, button, isNext)` or `OnDrawTitle(canvas, bounds, title, color, isPressed)` and return `true` to draw them your way (`DrawCalendarText` draws text through the calendar); taps keep working. App-wide, the look's `CalendarNavigationButtonPainter` replaces every calendar's buttons. A header made of views of your own: `IsHeaderVisible = false` with `Title`, `CanGoToPrevious`, `CanGoToNext` and the methods.
- **Look.** Every color is `null` by default and takes the color scheme's color when drawn (accent, foreground, muted, disabled), so the calendar follows a dark-mode switch on both layers. One set of font properties (`FontFamily`, `FontSize`, `FontAttributes`, `FontAutoScalingEnabled`) covers every cell kind; the header, the day names and today have their own size, weight and color. Today is a ring, a fill, a colored number or not marked (`TodayStyle`); selected days are circles, rounded squares or squares (`SelectionShape`); a range is a band with rounded ends between its first and last days.
- **Events under the days.** `DayStyle` is a function that returns an `SkUiCalendarDayStyle` for a day, or `null`: `TextColor`, `BackgroundColor`, `FontAttributes`, `IsStrikethrough` and `Markers`. It is asked once per day while drawing and remembered; call `RefreshDayStyles()` when the data behind it changes.
  - **Markers** sit in an area under the number (`DayMarkerAreaHeight`; by default the look's 14 DIPs as soon as `DayStyle` or `DayPainter` is set, and rows grow by it): `SkUiCalendarMarker.Dot(color)`, `Bar(color)` (as wide as half the number's circle), `Glyph(glyph, fontFamily, color)` (an icon of an icon font, as tall as the area) and `Caption(text, color)` (the look's 10-DIP text, shortened to fit). A day's markers sit in one centered row in the order given; what does not fit is left out. Shifts, time off and vacation as colored bars, birthdays as a glyph, a holiday as a caption.
  - **A day of your own.** `DayPainter` (`SkUiCalendarDayPainter`) draws each day the app's way after the look's chrome: it gets an `SkUiCalendarDayContext` with the day, its `Bounds`, `NumberBounds` and `MarkerBounds`, its state (selected, range part, today, enabled, adjacent, focused), the number's `TextColor` and the day's `Style`, and `DrawText(...)` to draw text through the calendar's text engine (shaped, laid out once and kept). Return `true` to draw the day's contents yourself (the calendar then draws neither the number nor the markers), `false` to have them drawn over yours. Use it for avatars, prices, progress rings or icons from images.
  - Why not a view per day: a month page and its two neighbors hold over a hundred days, and each would be a measured, recorded view on every swipe. The painter gives the same freedom on both layers with nothing to create.
- **Size.** As wide as it is given (seven 44-DIP columns by itself), as tall as the header (48), the day names (28) and the weeks (`DayRowHeight`, default the look's 44). Given more height, the rows stretch. Year, decade and century pages use the same area in 4 rows of 3.
- **Keyboard** (one tab stop): arrows move a focus by day and week (by cell on the other pages; mirrored right to left), Page Up / Down by page (with Shift by a year), Home / End to the week's ends, Enter / Space choose (or open a month or year). The focus moves to the next page when it leaves the page, and the focused cell has a ring while the keyboard drives the calendar.
- **Screen readers.** The calendar is an adjustable element: its name is the title, its value the focused day with its state ("Tuesday, November 10, 2026, Selected"); increment / decrement move a day, activate chooses it. Its children are elements of their own: the header's title (activating it changes the display mode) and buttons (`SkUiStrings.PreviousText`, `NextText`), and every cell of the page with its full date ("Tuesday, November 10, 2026") and its state ("Selected, Today"); a day that cannot be chosen is read dimmed. Activating a day chooses it. A cell's element keeps its id while the page changes, and its automation id is the calendar's with the date (`MyCalendar_2026-11-10`; `_Title`, `_Previous`, `_Next` for the header).
- **Right to left,** the columns, the header and the swipe are mirrored.

## How to use

```xml
<skui:SkUiCalendar SelectedDate="{Binding Date}"
                   MinimumDate="{Binding Today}"
                   FirstDayOfWeek="Monday" />
```

A booking range with blocked days:

```csharp
var calendar = new SkUiCalendar
{
    SelectionMode = SkUiCalendarSelectionMode.Range,
    MinimumDate = DateTime.Today,
    MaximumDate = DateTime.Today.AddMonths(6),
    HeaderLayout = SkUiCalendarHeaderLayout.Centered,
};
calendar.BlackoutDates!.Add(new DateTime(2026, 12, 25));
calendar.SelectionChanged += (_, e) => Book(e.NewRange);
```

A compact, styled calendar (title first, round gray buttons, semibold gray day names, today as a ring with a bold number):

```xml
<skui:SkUiCalendar HeaderLayout="TitleFirst" HeaderFontSize="18" HeaderPadding="0"
                   NavigationButtonSize="32" NavigationButtonSpacing="16"
                   PreviousButtonIcon="caret_left.svg" NextButtonIcon="caret_right.svg"
                   NavigationButtonColor="#4B5563" NavigationButtonBackgroundColor="#F3F4F6"
                   DayNameFormat="Abbreviated" DayNameTextColor="#6B7280" DayNameFontAttributes="Bold"
                   FontSize="15" TextColor="#111827" TodayStyle="Ring" TodayTextColor="#111827" />
```

Events under the days (bars for shifts and time off, a glyph for birthdays, a caption for holidays), on the Core layer:

```csharp
var calendar = new SkUiCoreCalendar()
    .SetFirstDayOfWeek(DayOfWeek.Monday)
    .SetDayStyle(day => schedule.TryGetValue(day, out var entry)
        ? new SkUiCalendarDayStyle
        {
            Markers = entry.Kind switch
            {
                EntryKind.Shift => [SkUiCalendarMarker.Bar(shiftColor)],
                EntryKind.Birthday => [SkUiCalendarMarker.Glyph(Icons.Cake, "AppIcons", birthdayColor)],
                _ => [SkUiCalendarMarker.Caption(entry.Title, holidayColor)]
            },
            IsStrikethrough = entry.IsCancelled
        }
        : null);
```

A day painted by the app (a price under each day, the number drawn by the calendar):

```csharp
calendar.DayPainter = (SKCanvas canvas, in SkUiCalendarDayContext day) =>
{
    if (prices.TryGetValue(day.Date, out var price))
        day.DrawText(canvas, price, day.MarkerBounds, day.TextColor, fontSize: 10);
    return false;
};
```

A range chosen from "Start" and "End" fields above the calendar:

```xml
<skui:SkUiCalendar SelectionMode="Range"
                   SelectedRange="{Binding Range}"
                   RangeSelectionTarget="{Binding Target}" /> <!-- Start / End: the field the user picked -->
```

A whole week chosen by its number:

```csharp
calendar.AreWeekNumbersVisible = true;
calendar.SelectionMode = SkUiCalendarSelectionMode.Range;
calendar.WeekNumberTapped += (_, e) => calendar.SelectedRange = e.EnabledRange; // the week's days that can be chosen
```

A full example over the app's data (appointments, out-of-office days, public holidays, a day's agenda): the samples app's "Team calendar" (`samples/MauiSkiaUiSamples/Samples/Controls/Calendar/TeamCalendarSample.cs`).

A weekly strip above an agenda: `VisibleWeeks="1" MaximumDisplayMode="Month" AreDayNamesVisible="True"`. A month picker (card expiry): `MinimumDisplayMode="Year" MaximumDisplayMode="Decade"`; the chosen month is `SelectedDate`'s month.

## Key properties

| Property | Default | Notes |
| --- | --- | --- |
| `DisplayDate` | today | A date of the page shown; kept in the range. Two-way. |
| `DisplayMode` | `Month` | `Month`, `Year`, `Decade`, `Century`; kept within `MinimumDisplayMode` / `MaximumDisplayMode`. Two-way. |
| `MinimumDisplayMode` / `MaximumDisplayMode` | `Month` / `Century` | Limit the modes. |
| `VisibleWeeks` | 6 | 1–6; fewer pages by weeks. |
| `FirstDayOfWeek` | `null` | The culture's. |
| `SelectionMode` | `Single` | `None`, `Single`, `Multiple`, `Range`, `MultipleRanges`. |
| `SelectedDate` / `SelectedDates` / `SelectedRange` / `SelectedRanges` | none | Two-way; see **Selection**. |
| `MinimumRangeDays` / `MaximumRangeDays` | 0 / 0 | The length of a range the user chooses (0: any). |
| `AllowsDisabledDaysInRange` | `true` | `false`: a range may not run over a day that cannot be chosen. |
| `AllowsDeselection` | `false` | A tap on the selected day clears it (single). |
| `MinimumDate` / `MaximumDate` | `null` | Days outside cannot be chosen or shown. |
| `BlackoutDates` / `DateEnabled` | empty / `null` | Days that cannot be chosen. |
| `RangeSelectionTarget` | `Auto` | Which end of the range a tap sets; two-way. |
| `DayStyle` | `null` | Per-day colors, weight, strike-through, markers. |
| `DayMarkerAreaHeight` | -1 | The area under the number for markers (the look's 14 with `DayStyle` or `DayPainter`, else none). |
| `DayPainter` | `null` | Draws each day the app's way. |
| `NavigationOrientation` | `Horizontal` | The swipe's direction; vertical also turns the header buttons' chevrons up and down (`SkUiCalendarNavigationButtonPaint.Direction`). |
| `IsSwipeEnabled` / `IsPinchEnabled` | `true` | |
| `IsHeaderVisible` / `HeaderLayout` / `AreNavigationButtonsVisible` | `true` / `TitleFirst` / `true` | |
| `TitleFormat` | `null` | The month title's .NET format. |
| `HeaderTextColor` / `HeaderFontSize` / `HeaderFontAttributes` | text color / look's 17 / bold | |
| `HeaderFontFamily` / `HeaderHeight` / `HeaderPadding` | `FontFamily` / look's 48 / 8 at the sides | |
| `NavigationButtonColor` / `NavigationButtonBackgroundColor` | accent / none | |
| `NavigationButtonSize` / `NavigationButtonSpacing` / `NavigationButtonCornerRadius` | look's 36 / 4 / circle | |
| `NavigationButtonBorderColor` / `NavigationButtonBorderWidth` | none / 0 | |
| `PreviousButtonGlyph` / `NextButtonGlyph` / `NavigationButtonFontFamily` | none | Icon font glyphs instead of the chevron. |
| `PreviousButtonIcon` / `NextButtonIcon` / `NavigationButtonGlyphSize` | none / half the button | Images instead of the chevron. |
| `AreDayNamesVisible` / `DayNameFormat` | `true` / `Shortest` | `Shortest` ("Tu"), `Abbreviated` ("Tue"), `FirstLetter` ("T"); the names come from `SkUiStrings`. |
| `IsTodayDayNameHighlighted` / `TodayDayNameTextColor` | `true` / today's color | Today's day name in the accent while the page shows today. |
| `DayNameTextColor` / `DayNameFontSize` / `DayNameFontAttributes` | muted / look's 12 / none | Also the week numbers. |
| `AreAdjacentDaysVisible` / `AreWeekNumbersVisible` | `true` / `false` | |
| `TextColor`, `FontFamily`, `FontSize`, `FontAttributes`, `FontAutoScalingEnabled` | scheme / look's / look's 16 / none / `true` | Days, months, years. |
| `DayRowHeight` | -1 | A row's height when the calendar sizes itself (the look's 44). |
| `TodayStyle` / `TodayColor` / `TodayTextColor` / `TodayFontAttributes` | `Ring` / accent / text / bold | |
| `SelectionShape` / `SelectionColor` / `SelectedTextColor` | `Circle` / accent / contrast | |
| `RangeColor` / `RangeTextColor` | light selection color / text | |
| `AdjacentDayTextColor` / `DisabledDayTextColor` / `WeekendTextColor` | muted / disabled / text | |
| `Title`, `CanGoToPrevious`, `CanGoToNext` | read-only | For a header of your own. |

Methods: `GoToPrevious(animated)`, `GoToNext(animated)`, `GoToDate(date, animated)`, `GoToToday(animated)`, `ClearSelection()`, `RefreshDayStyles()`. Events: `SelectionChanged`, `DateTapped`, `WeekNumberTapped`.

## Look

- `SkUiLook.DrawCalendarCell(canvas, SkUiCalendarCellPaint)` and `DrawCalendarNavigationButton(canvas, SkUiCalendarNavigationButtonPaint)`, each with a painter delegate (`CalendarCellPainter`, `CalendarNavigationButtonPainter`) and a `…Core` override. The cell paint carries the cell's bounds and kind (day or period), the selection shape, whether it is selected, its range part and whether the band runs on to its left or right, today, focus, press, enabled, and the resolved colors.
- The look draws the cell's chrome in the cell above its marker area; the calendar draws the number, the markers and the day painter's drawing.
- Sizes: `CalendarHeaderHeight` (48), `CalendarMarkerAreaHeight` (14), `CalendarMarkerFontSize` (10), `CalendarDayNamesHeight` (28), `CalendarRowHeight` (44), `CalendarWeekNumberWidth` (32), `CalendarNavigationButtonSize` (36), `CalendarFontSize` (16), `CalendarHeaderFontSize` (17), `CalendarDayNameFontSize` (12).
- Platform looks draw the default calendar in 1.13.

## Limits

- Gregorian dates; day and month names and the first day come from the current culture. Other calendar systems are not supported yet.
- Markers take icon glyphs, not image sources (a day painter can draw images).
- The calendar in a popup: the date pickers' `PopupStyle = Calendar` ([SkUiDatePicker](SkUiDatePicker.md)) and [SkUiDateRangePicker](SkUiDateRangePicker.md). `SkUiDateTimePicker` keeps its wheels.
