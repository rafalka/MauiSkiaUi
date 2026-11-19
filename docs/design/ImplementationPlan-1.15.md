# SkiaUi 1.15 implementation plan: the calendar, finished

**Goal:** close what 1.13 left open on `SkUiCalendar` / `SkUiCoreCalendar` and the pickers built on it, so the calendar's page has no "later" left that an app is likely to hit: the device checks 1.13 shipped without, a measured page slide (and the render-thread slide if the measurement asks for it), a date-and-time popup, platform looks' calendars, image markers, a month view with event lines and calendars other than the Gregorian one.

**Status:** proposed 19 November 2026; [D1](#decisions) agreed the same day. Built on `1.15-implementation` (19 November 2026): A2, A3, A4, B2, B3, B4, B5 and wave C (taken into the release on 19 November 2026: D8's cut line does not apply). A1's frame times measured on both phones (19 November 2026), which made B1 unnecessary; A1's checks by hand stay on the verification backlog. The review's controls planned for 1.15 (F1 segmented control and tab view, F5 chips, F6 range slider and rating) moved to 1.16 ([D10 of the review plan](ImplementationPlan-PerformanceAndControls.md#decisions) and the roadmap are updated).

**Baseline:** 1.14.0 (net10 with MAUI 10.0.130 and net11 with MAUI 11.0.10, SDK 11.0.100, SkiaSharp 4.154.0).

**Rules:**

- 1.x keeps the 1.0 API: additions only. The API comparison against 1.14.0 lists the new members of this plan and nothing else. No default changes: a calendar, a date picker or a date-and-time picker an app has today looks and behaves as it does on the built-in looks, except where a platform look gains its own calendar drawing ([D4](#decisions)).
- Both layers on one engine (`SkUiCalendarModel`, `SkUiCalendarGrid`); every new property exists on both, and a test keeps them at parity ([step A4](#a4-layer-parity-test)).
- NFR-2: a frame of a page slide and a frame of a swipe allocate nothing after a warm-up ([step A2](#a2-bench-scenarios-and-allocation-tests)). Perf-sensitive steps are checked with `scripts/bench.sh --baseline v1.14.0`.
- 0 warnings on `net10.0` and `net11.0`; the headless suite and the leak scenarios (`MemoryLeakTests`, with `CalendarSwiped` and `CalendarPopupsOpened`) pass on both.

## Decisions

- **D1 1.15.0 finishes the calendar; the review's controls start in 1.16.** 1.13 shipped with its device checks pending ([1.13 step 5](ImplementationPlan-1.13.md#step-5-verification-and-release)) and the decision on render-thread page slides (1.13 D2) waiting for them, and its page lists five gaps ([Limits](../controls/SkUiCalendar.md#limits), [Roadmap](Roadmap.md#new-controls-and-packages)). Finishing them now, while the engine is fresh, is cheaper than after three new controls have touched the looks and the gesture code. The review plan's release order shifts by one: 1.16 F1, F5, F6; 1.17 F2, F3; 1.18 F4 phase 1.
- **D2 Measure before moving the slide.** Wave A measures a page slide on the Galaxy S9 and the iPhone 8. The render-thread slide ([step B1](#b1-render-thread-page-slides-conditional)) is built only if the S9 misses frames (more than 2 % of frames over 16.7 ms across ten slides, or any over 33 ms), as 1.13 D2 said. If it does not, B1 is dropped and the measurement goes in [Performance.md](../Performance.md) as the reason.
- **D3 The date-and-time popup is the calendar over the time wheels,** in one popup content, not two popups in a row: the calendar on top, the time columns of `SkUiDateTimeWheels` below as the inline wheels (`SkUiPickerWheels`' engine) draw them, OK / Cancel as today. `SkUiDateTimePicker.PopupStyle` (`Wheels`, `Calendar`, `Default` = the look's) reuses `SkUiDatePickerPopupStyle`; the request stays `SkUiDatePopupRequest` (with `IncludesTime`, as the date and time wheels' request already is), so presenters with their own UI keep working. A tap on a day never closes this popup (the time is still to choose). *Changed while building:* it follows `UpdateMode` as the wheel popup does (each change applies at once and the popup stays open, or a draft OK accepts) instead of always waiting for OK: the same picker then behaves the same whichever popup it shows, and an app that wants OK already says so with `WhenFinished`. The time wheels are `SkUiWheelsView` over a second `SkUiDateTimeWheels` with only the time columns, built again for each chosen day, so the range's first and last days offer only its times.
- **D4 Platform looks draw their platform's calendar; popups keep their wheels.** `AndroidMaterial3SkUiLook`, `IosSkUiLook`, `MacSkUiLook` and `WindowsSkUiLook` override `DrawCalendarCellCore`, `DrawCalendarNavigationButtonCore` and the calendar sizes so a calendar on a page looks like the platform's (Material 3's date picker grid, UIKit's `UICalendarView`, AppKit's graphical date picker, WinUI's `CalendarView`). That changes how an existing calendar under a platform look is drawn, which is the point of a platform look and was announced in 1.13's page ("platform looks inherit the default in 1.13"). `DatePickerPopupStyle` stays `Wheels` on every built-in look: switching the pickers' popup to a calendar under a platform look is a behavior change and goes to [Held for 2.0](Roadmap.md#held-for-20). An app opts in with `PopupStyle="Calendar"` or a look that returns it.
- **D5 Image markers go through the shared image loader.** `SkUiCalendarMarker.Image(source, size)` is drawn from an `SkUiImageSlot` per distinct source held by the model (not per day): a month of birthday avatars with one source each loads each once, and the LRU cache shares them with the rest of the app. A marker whose image has not loaded yet is left out (its room kept); the load invalidates the calendar's content. The slots are released with `ReleaseDrawingResources` and when `DayStyle` stops returning their source (on `RefreshDayStyles`).
- **D6 A month view with events is markers stacked as lines, not a scheduler.** `DayMarkerLayout` (`Row`, the 1.13 behavior and default; `Lines`) lays a day's markers out one per line under the number when the rows are tall enough (a calendar given the height of a page). A `Caption` becomes an event line (a colored chip with its text, shortened to fit), a `Bar` a thin line, dots and glyphs gather on the last line; what does not fit becomes a "+N" line (`SkUiStrings.MoreEventsFormat`). Events that run across several days drawn as one bar over the cells, time slots, drag to move and a week or day timeline are a scheduler and stay [not planned](Roadmap.md#not-planned).
- **D7 Other calendar systems come through `System.Globalization.Calendar`.** A `CalendarSystem` property (`Calendar?`, `null`: the culture's own calendar when it is one the grid supports, else Gregorian, so 1.13 apps are unchanged under Gregorian cultures) sets how the pages are cut into months and years and how titles, day numbers and month names are written. Dates stay `DateTime` everywhere (selection, rules, events), so the app's data does not change with the calendar shown. Supported: `GregorianCalendar` (every type), `ThaiBuddhistCalendar`, `JapaneseCalendar`, `TaiwanCalendar`, `KoreanCalendar` (Gregorian months, other year numbers and eras: titles only), and the lunar and solar ones with their own months: `UmAlQuraCalendar`, `HijriCalendar`, `PersianCalendar`, `HebrewCalendar` (13 months in a leap year). Calendars outside that list fall back to Gregorian with a warning, as invalid values do. The date pickers' wheels stay Gregorian in 1.15; their calendar popup follows the picker's `CalendarStyle`, so an app can show a Hijri calendar popup over Gregorian rules. *Changed while building:* `null` stays Gregorian (1.x keeps defaults: a Thai or Saudi app's calendar would otherwise change under it); `CalendarSystem = CultureInfo.CurrentCulture.Calendar` follows the culture. Texts come from the calendar's own culture when the current one cannot write it (.NET formats a calendar only in cultures that offer it).
- **D8 Wave C is the release's cut line.** Waves A and B are the release. Wave C (D7) is the largest step and touches the grid's date math everywhere: it lands in 1.15 if it is done and verified when wave B is, else it moves to 1.16 and the release goes out without it. Nothing in A or B depends on it.

## Facts that shape the plan

Checked against `devel` at 1.14.0 (19 November 2026).

- **Engine.** `MauiSkiaUi/Helpers/SkUiCalendarModel.cs` (2,167 lines) holds state, layout, drawing and input; `SkUiCalendarGrid.cs` (241) the date math, Gregorian only, with `SkUiCalendarGrid.Gregorian(culture)` giving a Thai or Hijri culture a Gregorian copy so texts match the pages. `SkUiCalendar.cs` (1,530) and `SkUiCoreCalendar.cs` (1,078) copy settings into the model; types are in `SkUiCalendarTypes.cs`.
- **Slides re-record.** A page slide or a swipe re-records the calendar's one node every frame (1.13 D2). There is no calendar scenario in `benchmarks/Scenarios/Scenarios.cs`, no calendar entry in [Performance.md](../Performance.md) or [Benchmarks.md](Benchmarks.md), and no allocation test for a slide frame.
- **Device checks.** None of 1.13's device checks has been run (they were missing from the [verification backlog](Roadmap.md#verification-backlog) until 19 November 2026). The 1.12 inline wheels' device checks are pending too.
- **Popups.** `SkUiCalendarPopup` is internal and built from `SkUiDateTimeWheels` (the picker's rules and value). `SkUiDateTimePicker` has no `PopupStyle`. The look's `DatePickerPopupStyle` returns `Wheels` on every built-in look.
- **Looks.** No platform look overrides a calendar member; `SkUiLookComparisonPage` has no calendar row.
- **Markers.** `SkUiCalendarMarker` has `Dot`, `Bar`, `Glyph`, `Caption`; markers sit in one centered row in `DayMarkerAreaHeight`.
- **Tests and leaks.** `CalendarTests` (1,193 lines), `DatePickerTests`, `DateRangePickerTests`; leak scenarios `CalendarSwiped` and `CalendarPopupsOpened`.
- **Roadmap.** The release table and the 1.12 to 1.14 sections were brought up to date on 19 November 2026 (all three shipped), and 1.13's device checks were added to the verification backlog.

## Wave A: verify and harden what shipped

Independent steps; A1 needs the devices, the rest run headless.

### A1 Device checks of 1.13

**Status: frame times done; the checks by hand are on the [verification backlog](Roadmap.md#verification-backlog).** By an agent with both phones connected (19 November 2026), the bench scenario `calendar-slide` (a 420-tall month with markers on every day and week numbers, the next month every 400 ms, animated) against v1.14.0. The first S9 run found the slide at 36 fps with the UI thread at 11 / 31 ms per frame (average / worst): the calendar's draw allocated about 90 KB per frame, almost all in the markers. A caption that shrinks to fit (the S9's font made "Off" overflow its cell) was fitted again in every cell, because cells of a grid differ in width by float rounding and the fit cache compares widths exactly; text slots are now snapped to 1/16 DIP (`AShrunkCaptionIsFittedOnceNotPerCell`). After the fix the S9 draws every frame of a slide inside the 60 Hz budget (UI 5.0 / 10.2 ms, render 4.2 / 7.4 ms, 44 fps, the most the scenario can show: a slide fills 280 ms of each 400), allocating 0.64 MB over the motion instead of 26 MB at 1.14. Numbers for both phones: [Performance.md](../Performance.md#calendar-on-devices). Items 2 to 5 (the feel, TalkBack and VoiceOver, keyboards, the popups on each presenter) need hands on the phones.

Size: M (a device session). On the Galaxy S9 (Android 10) and the iPhone 8 (iOS 16.7), the net11 build, with the demo's calendar, date picker and range picker gallery pages and the samples' "Team calendar":

1. Frame times of ten page slides (buttons) and ten swipes, with markers on every day and with `DayPainter`; the same on a weekly strip. Record them in [Performance.md](../Performance.md) under a calendar heading. This is [D2](#decisions)'s input.
2. The feel: a swipe following the finger, the third-of-a-page and fling settle, the stretch past the last page, a pinch between modes, a calendar inside a scroll view (a vertical drag scrolls the page).
3. TalkBack and VoiceOver: the adjustable element (increment / decrement by day), each day's element (date and state, dimmed when unavailable), the header's title and buttons, the automation ids.
4. The keyboard on Mac Catalyst and Windows: arrows, Page Up / Down with and without Shift, Home / End, Enter, the focus ring; in the date picker's calendar popup too.
5. The pickers' calendar popup and the range picker on each presenter (anchored, window, bottom pane), with `UpdateMode` `WhenFinished`, on both phones and on Windows.

These checks and the 1.12 inline wheels' (same session) are on the [verification backlog](Roadmap.md#verification-backlog); what is run comes off it, what cannot be run stays there by name.

**Done when:** the numbers are in Performance.md, each item is checked or listed in the backlog, and anything found is fixed (with a test) or filed.

### A2 Bench scenarios and allocation tests

Size: S.

1. Scenarios `skui-calendar` and `core-calendar` (first frame of a month page with a `DayStyle` marking every day) and `calendar-slide` (a page slide driven frame by frame through `SkUiFrameRenderer`, reporting per-frame record and composite times). New options set through `TrySet` so `--baseline v1.14.0` still builds.
2. In `PerformanceTests`: after a warm-up, the frames of a page slide and of a swipe drag allocate 0 bytes (fewest of three batches, as `CoreWrapAndShrinkRelayoutAllocatesNothing`), on both layers, with markers and with a `DayPainter` that draws text.
3. Fix what they find: day style lookups, marker layout, per-frame text layouts and the gesture path of the calendar's recognizer (1.14's gesture work was checked against 1.12 code; the calendar's recognizer came after).

**Done when:** both tests pass, the scenarios are in `--list`, and Benchmarks.md has the first numbers.

**Status: done.** A swipe frame's drawing allocated 0.8 KB on a plain calendar and up to 127 KB with markers; it allocates nothing now (`CalendarAllocationTests`), and a whole frame through the pipeline 104 bytes (the re-recorded picture). Causes: the zoom's closure allocated on every `Draw`, marker captions measured and drawn on one layout (laid out twice per frame), per-frame rect jitter laying out shortened captions again (pages now move by a canvas translation), MAUI colors made per frame by the palette, and `HasFlag` boxing in Debug. Numbers: [Benchmarks.md](Benchmarks.md#115-calendar-scenarios-19-november-2026).

### A3 Edge cases sweep

Size: S. Tests first, fixes where they fail, on both layers:

- `MinimumDate` / `MaximumDate` at `DateTime.MinValue` / `MaxValue` and at the ends of a decade and century page; `GoToDate` beyond them.
- A culture change while a page is shown (texts, first day, week rule), and `SkUiStrings` changes during a slide.
- Right to left during a swipe; `FlowDirection` changed mid-slide.
- A range with `RangeSelectionTarget = End` across a blackout day with `AllowsDisabledDaysInRange = false`; `MultipleRanges` joining across a month the pages skip.
- `VisibleWeeks` changed while a week page is mid-slide; `DisplayMode` limits changed to exclude the current mode.

**Status: done.** `CalendarEdgeCaseTests` (11 tests): all passed against 1.14's code, so the sweep found nothing to fix. The day style that throws was dropped: no app callback of the calendar or the pickers (`DateEnabled`, `DayPainter`) is caught today, and swallowing one would hide the app's bug; it fails fast as they do.

### A4 Layer parity test

**Status: done** (`CalendarParityTests`): the only difference found is the layers' image convention (`ImageSource` on SkUi, `SkUiImageSource` on Core), which the test knows.

Size: S. A reflection test in `CalendarTests`: every public bindable property of `SkUiCalendar` has a CLR property and a fluent `Set*` of the same name and type on `SkUiCoreCalendar`, and every public method and event exists on both (an explicit list of intended differences, empty today). The same for `SkUiDateRangePicker` / `SkUiCoreDateRangePicker`. New properties in waves B and C are then kept at parity by the test.

## Wave B: close the gaps

B1 waits for A1; B2 to B5 are independent of each other and of A, and can be built in parallel, one branch each (they touch different files except `SkUiCalendarTypes.cs`, where each adds its own types: merge in any order).

### B1 Render-thread page slides (conditional)

**Status: not needed** ([D2](#decisions)): after A1's fix no frame of a slide on the Galaxy S9 is over 16.7 ms (UI 10.2 ms at worst, render 7.4 ms), so the slide stays on the UI thread, as 1.13 D2 decided. The risks weighed for it (pages as internal render children, taps against render-thread offsets, the semantics tree after a slide) are not taken on.

Size: L. Only if [D2](#decisions)'s threshold is crossed.

1. The calendar's node records each visible page (the current one and, during a slide, its neighbor) as a child render node of its own, re-recorded only when the page's content changes; the header and day names stay in the parent.
2. A slide or a settle animates the children's offset on the render thread (as the carousel and flings do), with the look's `CalendarPage` transition; a swipe moves them through composite-time props, so a drag frame records nothing.
3. Zooms between display modes stay a UI-thread tween (rare and short).
4. `ReleaseDrawingResources` releases each page's recording; semantics and hit-testing read the settled page.

**Done when:** a slide frame records nothing (a test on `SkUiFrameRenderer`'s recorded count), A2's tests still pass, and the S9 numbers are taken again.

### B2 The date-and-time popup

**Status: done** (`SkUiDateTimeCalendarPopup`, `DateTimeCalendarPopupTests`, the `CalendarPopupsOpened` leak scenario); see D3 for what changed.

Size: M. [D3](#decisions).

1. `PopupStyle` on `SkUiDateTimePicker` and its Core twin, validated, `Default` reading the look's `DatePickerPopupStyle`.
2. `SkUiDateTimeCalendarPopup`: the calendar over the time columns, both following the picker's rules (min / max, blackout days, `DateEnabled`, minute steps; a minimum time on the minimum day). Keys: Tab moves between the calendar and the wheels; the calendar's keys as in the date popup; Enter accepts.
3. Sizing on each presenter: the bottom pane and the window stack them; an anchored popup too narrow for both shows them stacked with the calendar's `VisibleWeeks` unchanged (no scrolling popup).
4. Tests in `DatePickerTests` (draft, accept, cancel, rules at the day boundary), the leak scenario `CalendarPopupsOpened` extended with a date-and-time picker, the gallery page's `PopupStyle` switch.

### B3 Platform looks' calendars

**Status: done** with option (a), chosen 19 November 2026: the look's `CalendarTodayStyle`, `CalendarSelectionShape`, `CalendarSelectionColor(accent)` and `CalendarSelectedTextColor(selection, baseColor, text)` (Skia colors: drawing allocates nothing) answer what the app leaves unset; on the SkUi layer "unset" is `IsSet` false (read as drawn, so `ClearValue` of an unchanged value counts), on Core never set; the getters keep reading the 1.13 defaults. Material 3, iOS, Mac and Windows set them and their sizes; Windows draws the selection as a ring. Tests: `CalendarLookDefaultsTests`. The comparison page's row was left out: MAUI has no calendar to put beside it; the looks were checked on a rendered sheet of all five.


Size: M. [D4](#decisions). For each look: the cell chrome (selection, today, range band, focus, press), the navigation buttons, and the sizes, from the platform's own control:

- **Material 3:** 40-DIP circles on 48-DIP rows, today an outlined circle, the range band in the secondary container color running to the cell edges, the title with a drop-down arrow before the buttons (`HeaderLayout` `TitleFirst`).
- **iOS:** `UICalendarView`: today's number in the accent with no ring, the selected day a light accent circle with an accent bold number, the title with a chevron, buttons as plain accent chevrons.
- **Mac:** the graphical date picker's compact cells, a rounded rectangle selection, small chevrons.
- **Windows:** `CalendarView`: today an accent-filled circle, the selection a ring, square hover cells, up / down buttons (`NavigationOrientation` stays the app's: the look only draws).

Add a calendar row to `SkUiLookComparisonPage`. Tests: each look draws a month page (selection, range, today, focus) without throwing, and its sizes are what it says; pixel checks stay manual, on the comparison page, before release.

### B4 Image markers

**Status: done** (`CalendarImageMarkerTests`, the `CalendarSwiped` leak scenario). Sources no day drew since the last `RefreshDayStyles` are let go at the next one.

Size: S. [D5](#decisions). `SkUiCalendarMarker.Image(ImageSource source, double size = 0)` (`0`: the marker area's height) and `SkUiCalendarMarkerKind.Image` (appended last). Tests with PNG bytes (`ImageLoadingTests.Png`): a marker draws once loaded, a source used on many days loads once, `RefreshDayStyles` releasing a source no day uses, `ReleaseDrawingResources`; the leak scenario `CalendarSwiped` gets image markers. The samples' "Team calendar" shows avatars for birthdays.

### B5 Event lines

Size: M. [D6](#decisions).

1. `DayMarkerLayout` (`Row` default, `Lines`) on both layers; `DayMarkerLineHeight` (the look's new `CalendarMarkerLineHeight`, 16). In `Lines`, the marker area is what is left of the row under the number; a row given no extra height shows the "+N" line only.
2. `SkUiStrings.MoreEventsFormat` ("+{0}") and the screen reader's day value listing the captions ("Tuesday, November 10, 2026, Selected, 3 events: Standup, …"; `SkUiStrings.EventsFormat`).
3. `DayTapped` already carries the date. (`MarkerLines` on `SkUiCalendarDayContext` was dropped: `MarkerBounds` is the lines' area, which is what a painter needs.)

**Status: done** (`CalendarEventLinesTests`, a `Lines` case in `CalendarAllocationTests`). The day's captions are read in both layouts, joined after the state; no `EventsFormat` string was needed.
4. Tests: lines fitted to the row height, the overflow count, right to left, both layers drawing the same pixels. A samples page: a month view of a team's events filling the screen, a day's list under it on a phone.

## Wave C: other calendar systems (cut line, D8)

Size: L. [D7](#decisions).

**Status: done** (`SkUiCalendarSystem`, the grid's optional system parameter, `CalendarSystemTests`). The engine keeps the page start, steps and month boundaries per page: .NET's Hebrew calendar allocates per date it splits, and a swipe frame now asks the calendar nothing per cell (0 bytes). Japanese decades and centuries count Gregorian years (its era years restart); day numbers stay digits.

1. **Grid.** `SkUiCalendarGrid` takes a `Calendar`: a month page is the calendar's month (`GetDaysInMonth`, `GetMonthsInYear`, `AddMonths` on the calendar), weeks still by `FirstDayOfWeek`; a year page lists the calendar's months (12 or 13 cells: 13 in a Hebrew leap year, laid out 4 × 4 with the last cells empty, or 5 × 3 decided on the look's sizes); decade and century pages count the calendar's years. Week numbers stay the culture's rule on Gregorian dates (documented).
2. **Texts.** Day numbers, month names, titles and screen reader dates formatted by a culture copy whose `DateTimeFormat.Calendar` is the chosen calendar (replacing `SkUiCalendarGrid.Gregorian` where a supported calendar is set); digits stay the culture's (no native digit substitution, as .NET's formatting).
3. **Limits.** Each calendar's `MinSupportedDateTime` / `MaxSupportedDateTime` joins `MinimumDate` / `MaximumDate` as where pages stop.
4. **API.** `CalendarSystem` on both calendars and the range picker (passed to its popup), and on `CalendarStyle` for the date pickers' popups.
5. Tests per supported calendar: page boundaries against .NET's own calendar for 30 years, the Hebrew leap year's year page, Umm al-Qura's supported range ends, titles, selection and range rules unchanged in `DateTime` terms, right to left with Arabic and Hebrew cultures.

## Docs, demo and release

- **Docs:** [SkUiCalendar](../controls/SkUiCalendar.md) (new properties; the Limits section emptied of what this release closes; the Look section's platform looks), [SkUiDateTimePicker](../controls/SkUiDateTimePicker.md) (`PopupStyle`), [SkUiDatePicker](../controls/SkUiDatePicker.md) and [SkUiDateRangePicker](../controls/SkUiDateRangePicker.md) (calendar systems in the popup), [ControlLook](ControlLook.md) (platform calendars, `CalendarMarkerLineHeight`), [Localization](Localization.md) (calendar systems, the new strings), [Migration.md](../Migration.md) and the migration skills (drop "Not available" items), [Requirements](Requirements.md) (FR-33 complete), [Performance.md](../Performance.md) (A1's numbers).
- **Roadmap:** a 1.15.0 section; the calendar's "Later" list reduced to what stays out (the scheduler); the 1.15 row and section marked shipped; what A1 checks taken off the verification backlog.
- **Changelog:** one `### New features` entry per wave B / C item, `### Other` for the bench scenarios and the platform looks' drawing change, `### Fixes` for what A1 and A3 find.
- **Demo:** the calendar gallery page gets `DayMarkerLayout`, image markers and `CalendarSystem`; the date-and-time picker's page gets `PopupStyle`; the look comparison page its calendar row.
- **Verification before release:** the headless suite and leak scenarios on both lines; the API comparison against 1.14.0; `scripts/bench.sh --baseline v1.14.0 -S skui-calendar,core-calendar,calendar-slide`; on the S9 and the iPhone 8: B1's slide (if built), the date-and-time popup on each presenter, event lines and image markers scrolling through months, the platform looks on their own platforms, VoiceOver and TalkBack on event lines and on a Hijri calendar (if C lands).
- Release `1.15.0`.

## Order and sizes

| Step | Size | Depends on | Parallel with |
| --- | --- | --- | --- |
| A1 Device checks | M | – | A2–A4, B2–B5 |
| A2 Bench and allocation tests | S | – | everything |
| A3 Edge cases | S | – | everything |
| A4 Parity test | S | – | everything (land early: guards B and C) |
| B1 Render-thread slides | L | A1 (D2) | B2–B5 |
| B2 Date-and-time popup | M | – | B3–B5 |
| B3 Platform looks | M | – | B2, B4, B5 |
| B4 Image markers | S | – | B2, B3, B5 |
| B5 Event lines | M | – (after B4 if both change marker layout in the same week) | B2, B3 |
| C Calendar systems | L | A4 | after B lands, or alongside on its own branch |
