# SkiaUi 1.13 implementation plan: calendar

**Goal:** a drawn calendar on both layers, `SkUiCalendar` and `SkUiCoreCalendar` ([FR-33](Requirements.md#functional-requirements)): a control on a page for bookings, schedules and date ranges, and later the month grid the date pickers can open instead of wheels. The performance work planned for 1.13 moved to [1.14](ImplementationPlan-1.14.md) on 17 November 2026.

**Baseline:** 1.12.0 (net10 with MAUI 10.0.130 and net11 with MAUI 11.0.10, SDK 11.0.100, SkiaSharp 4.154.0).

**Rules:**

- 1.x keeps the 1.0 API: additions only. The API comparison against 1.12.0 lists the calendar's new types, the look's new members and one new `SkUiTransitionKind` member (appended last), nothing else.
- MAUI has no calendar, so the API is SkiaUi's own. Where the date picker already has a name it is reused (`MinimumDate`, `MaximumDate`, `BlackoutDates`, `DateEnabled`), so the picker and the calendar agree about which days can be chosen. Other calendar controls were studied for what apps need; their property names are not copied.
- Both layers on one engine, as the indicator view: the controls copy their settings into a shared model, which measures, draws, hit-tests and handles keys.
- 0 warnings on `net10.0` and `net11.0`; the headless suite and the leak scenarios pass on both.

## Decisions

- **D1 A calendar is one drawn node.** Day cells are laid out, hit-tested and drawn by the engine, as the indicator view draws its dots: no view per day. Per-day styling is a callback the recording reads (`DayStyle`), not a template.
- **D1a Days are customized in two steps, both drawn.** Events go in a marker area under the number (`DayMarkerAreaHeight`): dots, bars, icon glyphs and captions from `DayStyle.Markers`, laid out in one row by the engine. A day of the app's own is a painter (`DayPainter`) called after the look's chrome, with the day's parts, state and colors and the calendar's text drawing; it can take over the number and markers. A view per day was weighed and left out: a page and its two neighbors hold over a hundred days, each a measured, recorded view per swipe, and the Core layer has no templates.
- **D2 Pages slide on the UI thread for now.** A swipe or a previous / next step re-records the calendar's one node per frame while the page moves. The texts are laid out once and kept (day numbers 1 to 31, day names, month names), so a frame draws cached text blobs and a few circles. Moving the pages to the render thread (two recorded pages and an animated offset, as the archived phase F planned on the carousel's engine) is taken up only if the device check in [step 5](#step-5-verification-and-release) shows dropped frames on the Galaxy S9.
- **D3 Four display modes:** month (days), year (months), decade (years), century (decades). The title steps up, a cell steps down, a pinch zooms out (up) or in (down), and `MinimumDisplayMode` / `MaximumDisplayMode` limit them (a month-and-year picker for card expiry: `MinimumDisplayMode = Year`). At the highest mode a tap on the title goes back to the lowest, so the title is always a way back to the days.
- **D4 Selection modes:** none, single, multiple, range. Each mode has its own property: `SelectedDate` (single; also the last date chosen in the other modes), `SelectedDates` (multiple, a new read-only list per change so a binding sees it), `SelectedRange` (`SkUiDateRange`, whose end is `null` while it is open: the first tap starts it, the second ends it, a third starts a new one). `RangeSelectionTarget` (`Auto`, `Start`, `End`, two-way) lets an app with "Start" and "End" fields say which end the next tap sets; after a start the calendar moves the target to the end. The user's changes are written back (`WriteBack` on the SkUi layer). `SelectionChanged` fires for the user's changes and the app's.
- **D5 Colors are resolved when drawn.** Every color property is nullable: `null` takes the color scheme's color at paint time (accent, foreground, muted, disabled), so a Core calendar follows a dark-mode switch too (the backlog's Core scheme item does not apply to it).
- **D6 One set of font properties for every cell kind**, plus separate size, weight and color for the header, the day names and today. An app restyling the calendar sets a handful of properties, not one text style per cell state.
- **D7 The header is drawn by the calendar** (its buttons styled by size, shape, border, colors and an icon glyph or image; a subclass can draw the buttons and the title itself through protected hooks) in two layouts: the title at the start with both buttons at the end (a compact header that suits a phone), or the buttons around a centered title. A tap on the title changes the display mode (D3). An app header of its own uses `IsHeaderVisible = false` with `GoToPrevious()`, `GoToNext()`, `DisplayMode`, `CanGoToPrevious`, `CanGoToNext` and the `Title` property, which is the text the header would show.
- **D8 Gregorian dates.** Dates are `DateTime` values without time (the time part is dropped). Day and month names and the first day of the week come from the current culture; other calendar systems (Hijri, Persian, Thai) stay on the backlog, as for the date picker's wheels.
- **D9 Screen readers get one adjustable element in 1.13:** the title as its name, the focused (or selected) day with its state as its value, increment / decrement moving a day and activate choosing it. An element per day needs virtual children in the semantics tree, which no control has yet: wave 2.

## Wave 1: the calendar

### Step 1 Engine

`MauiSkiaUi/Helpers/SkUiCalendarGrid.cs` (date math) and `SkUiCalendarModel.cs` (state, layout, drawing, input), shared by both layers and holding no control.

- Month pages by culture: the first day of the week (set, or the culture's), six weeks, or one to five weeks paged by weeks (`VisibleWeeks`); the neighbor months' days (shown or blank); week numbers by the culture's week rule.
- Year, decade and century pages: 4 × 3 cells, the neighbor decade's or century's first and last cells dimmed.
- Day states: today, selected, in a range (start, middle, end), disabled (outside `MinimumDate` / `MaximumDate`, a blackout date, `DateEnabled` false), weekend, a neighbor month's day, focused by the keyboard; the app's `DayStyle` (text and background colors, font attributes, a strike-through, markers) and `DayPainter`.
- Layout: header, day-name row, grid; rows stretch to the height given, else the look's row height. Hit-testing against the same layout (title, buttons, cells), mirrored right-to-left.
- Navigation: a page slide and a zoom between display modes on one tween with the look's new `SkUiTransitionKind.CalendarPage` (reduce motion: none); a swipe drags the page, and on release it settles on the neighbor page past a third of the page or a fling, else back. Navigation stops at the range's months.
- Keys: arrows by day and week (cells in the other modes), Page Up / Down by page, with Shift by year, Home / End to the week's ends, Enter / Space to choose; the focus moves to the next page when it leaves the page. The focused cell has a ring while the keyboard drives the calendar.
- Text through the shared engine, a layout kept per text and style (bounded), released with `ReleaseDrawingResources`.

### Step 2 Look

`SkUiLook.DrawCalendarCell` (`SkUiCalendarCellPaint`: the cell's bounds above the marker area, state, colors, range part, selection shape) and `DrawCalendarNavigationButton` (`SkUiCalendarNavigationButtonPaint`), each with a painter delegate and a `…Core` override; sizes (`CalendarHeaderHeight`, `CalendarDayNamesHeight`, `CalendarRowHeight`, `CalendarNavigationButtonSize`) `CalendarMarkerAreaHeight`, `CalendarMarkerFontSize`) and the header and day-name font sizes. `DefaultSkUiLook` draws the circle or rounded square, the band of a range with its rounded ends, the ring or fill of today and chevrons; the engine draws the markers. Platform looks inherit it in 1.13; Material's, Fluent's and UIKit's calendars as platform looks are a later step.

### Step 3 Controls

`SkUiCalendar` (bindable properties with `SkUiValidate`, fluent `Set*`, `WriteBack` of the user's changes) and `SkUiCoreCalendar` (`Set*` with `SetProperty`). Gestures: one recognizer for taps and swipes (it gives up a drag across its axis, so a calendar in a scroll view scrolls the page) and the pinch recognizer. Events: `SelectionChanged`, `DateTapped`. Methods: `GoToNext`, `GoToPrevious`, `GoToDate`, `GoToToday`, `ClearSelection`, `RefreshDayStyles`.

### Step 4 Tests, demo, docs

- Headless tests (`CalendarGridTests`, `CalendarTests`): grids by culture and first day, week paging, year / decade / century cells, rules, selection modes on both layers, the range target, write-back to a one-way binding, keys, display-mode steps, swipe settle and page commit, the marker area and the day painter, drawing both layers to the same pixels, implicit styles (`ImplicitStyleTests` finds the new view).
- Leak scenario: a calendar swiped and its selection changed on a closed page.
- Demo: a gallery page with the properties to play with, a styled calendar (a compact header with round buttons, today as a ring), a range booking and a month of events (bars, a glyph, a caption, dots).
- Docs: [SkUiCalendar](../controls/SkUiCalendar.md), the controls index, the changelog, [Requirements](Requirements.md) (FR-33), [Migration.md](../Migration.md) (from other calendar controls) and the migration skills.

### Step 5 Verification and release

- Headless: the suite and the leak scenarios on both lines.
- Devices (before release, on the Galaxy S9 and the iPhone 8): swipe feel and frame time while a page slides (D2), the pinch, VoiceOver and TalkBack on the calendar element, the keyboard on Mac Catalyst and Windows.
- Release `1.13.0` with wave 1; wave 2 can follow as 1.13.x or move on.

## Wave 2 (outline)

- **Date picker popup:** a `PopupStyle` on the date pickers of both layers and `SkUiDateTimePicker` (`Wheels`, `Calendar`, `Default` = the look's: a calendar on Android and Windows, wheels on iOS), following `UpdateMode` (a draft until OK), the range, blackout days and the day rule. `SkUiDatePopupRequest` is unchanged, so presenters with their own UI keep working.
- **Date range picker:** `SkUiDateRangePicker` and its Core twin on the range selection, with a typed popup request.
- **Semantics children:** an element per day with its full date and state (D9), the header's title and buttons as their own elements.
- **More selection rules:** a longest range, a range that may not cross a disabled day, several ranges.
