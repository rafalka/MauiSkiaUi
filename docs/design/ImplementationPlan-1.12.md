# SkiaUi 1.12 implementation plan: inline picker wheels and pickers of your own

**Goal:** apps can use the pickers' building blocks outside the built-in pickers:

- the popup's wheels can sit inline in a page or a sheet, always open, with no field to tap ([#98](https://github.com/rafalka/MauiSkiaUi/issues/98));
- apps can write their own pickers for value types SkiaUi does not support (a color, a duration, a node of a tree, a range). They get the picker API and its engine: the drawn field, `IsOpen`, the popup's placement and presenter, OK / Cancel, keyboard, screen readers and the change events. Only the value and the popup's body are theirs.

**Scope:** two items not on the 1.x backlog until now. They come from a real app (a recurring-schedule editor, #98) and from requests for pickers of other types. Both open up internals that already exist (`SkUiColumnsPopup`, `SkUiPickerModel`'s content factory) rather than adding new engines.

**Baseline:** 1.11.0 (net10 with MAUI 10.0.130 and net11 with MAUI 11.0.10, SDK 11.0.100, SkiaSharp 4.154.0). What has shipped is in the [changelog](../../CHANGELOG.md); the rest of the backlog stays in the [roadmap](Roadmap.md#backlog-1x).

**Rules** (as in 1.10, from [Requirements.md](Requirements.md#principles)):

- 1.x keeps the 1.0 API: everything here is additive. Widening `private protected` to `protected` and `internal` to `public` is additive. The API comparison against 1.11.0 (Step 4 of the 1.11 plan) must report additions only.
- Both layers where a control has both. The exceptions are in the [decisions](#decisions) ([D1](#decisions)).
- What apps build on must be public API. The samples' custom pickers use no `InternalsVisibleTo` ([D5](#decisions)), as the integration packages do ([Popups.md](Popups.md#integration-packages)).
- A feature a view does not use costs nothing per frame (`scripts/bench.sh --baseline v1.11.0`). The built-in pickers' popups must not get slower to open after the refactors (picker scenarios, plus a new popup-open scenario if none covers it).

## Decisions

Proposed. Agree them before Step W1.

- **D1 Inline wheels are SkUi\* only in 1.12.** The wheels are built on `SkUiCarouselView`, which has no Core twin, and a Core tree cannot host a SkUi\* view (`SkUiCoreHost` only goes the other way). `SkUiCorePickerWheels` waits for a Core carousel ([backlog: CarouselView](Roadmap.md#maui-parity)). The popups of Core pickers are SkUi\* surfaces already, so custom Core pickers (phase P) are not affected.
- **D2 An inline wheel commits once it settles.** Each column's `SelectedIndex` / `SelectedItem` is written when the wheel stops (snapped, no finger down) and `CommitDelay` (default 100 ms) has passed. Rows passed on the way are not written. The popup keeps its present behavior: it chooses at once (`UpdateMode="Immediately"`) or keeps a draft until OK.
- **D3 Haptics through a small platform helper of our own,** not MAUI Essentials' `HapticFeedback`, which needs the `VIBRATE` permission on Android. Android uses `View.PerformHapticFeedback` (`SegmentFrequentTick` on API 34+, else `ClockTick`; no permission needed), iOS and Mac Catalyst use `UISelectionFeedbackGenerator`, and Windows does nothing. It is switched by `SkUiGestureSettings.IsWheelHapticFeedbackEnabled` (app-wide, default `false`) and by `IsHapticFeedbackEnabled` per wheel control (default: the app-wide setting). The popup's wheels follow the same setting. It is internal in 1.12 and can be made public if apps ask.
- **D4 No `SkUiPopups.ShowColumnsAsync` in 1.12.** #98 offers it as an alternative. A field-less popup of wheels is a page that opens `IsOpen` on a hidden picker, or a custom picker (phase P). Add it later if an app still needs it.
- **D5 Custom pickers: open the base classes and add a typed convenience class on top.**
  - `SkUiPickerBase` / `SkUiCorePickerBase` get `protected` constructors and a small protected contract (P1).
  - `SkUiValuePicker<T>` / `SkUiCoreValuePicker<T>` (P3) are written only against that contract. They are the recommended starting point and the proof that the contract is enough.
  - Apps derive closed types (`class ColorPicker : SkUiValuePicker<Color>`), so XAML needs no `x:TypeArguments`.
  - Rejected alternative: only documenting `ShowPopupAsync` overrides on the existing pickers. That covers new UI for the value types SkiaUi already has, not new value types.
- **D6 Popup bodies are drawn views.** A custom picker's popup body is a `SkUiView` that SkiaUi wraps in the popup chrome (title, the app's header and footer templates, OK / Cancel / Clear, the bottom pane's toolbar). It works with every presenter because `request.Content` stays a ready view. A custom picker that wants its own native UI instead overrides `ShowPopupAsync` and completes the typed request (P2), as the built-in pickers allow today.
- **D7 Release:** `1.12.0`, with no prerelease unless the refactor of the popups (W1, P1) turns up device-only regressions in the built-in pickers.

## Facts that shape the plan

Checked against the code on `devel` (16 November 2026).

- **The wheels are tied to the popup.** `SkUiColumnsPopup` (858 lines, internal) takes its data as text arrays with apply / current callbacks. It also builds the popup chrome (title, header, footer, buttons) and implements `ISkUiPickerPopupContent` (preferred size, request creation, release). The wheel itself is a private `WheelView : SkUiCarouselView` with `SkUiWheelEffect` (internal), the band and the fade.
- **What already exists for the wheels:** `SkUiPickerColumn` (public; bindable `ItemsSource`, `ItemDisplayBinding`, two-way `SelectedIndex` / `SelectedItem`, `Loop`, `Header`, `ItemTemplate`, `Width`), `SkUiColumnsModel` (the column bookkeeping, internal), snapping and looping, the keys (Left / Right, Up / Down, Page Up / Down, Home / End, type-ahead), screen readers (each wheel is one adjustable element: `SkUiSemanticsRole.Slider` with a range and increment / decrement), disabled items, `ISkUiColumnsRules` (the date wheels' rules). Tests: `PickerWheelPartsTests`, `PickerWheelSemanticsTests`, `DateTimeWheelsTests`.
- **The pickers already have a content seam.** `SkUiPickerModel.ContentFactory` (`Func<SkUiPickerPopupContext, ISkUiPickerPopupContent>`) is how the multi-column picker, the date and time pickers and the several-choices list put their content into the shared popup session. The session does placement, sizing, the presenter, closing on scroll / resize / disable, focus and `IsOpen`. Everything a custom picker needs is behind this seam, and all of it is internal: the factory, `SkUiPickerPopupContext`, `SkUiPickerPopupChrome`, `SkUiPopupExtras`, `ISkUiPickerPopupContent` and the request constructors.
- **Pickers can't be subclassed from outside today.** `SkUiPickerBase` and `SkUiCorePickerBase` have `private protected` constructors. Their hooks (`ClearChoice`, `ChooseItem`, `RemoveChoice`, `OnModelCreated`, `PopupItemsSource`) are `private protected`. The public extension points (`ShowPopupAsync`, `SelectItemsAsync`) only change the UI for the value types SkiaUi ships.
- **The field's text has a free-form mode:** `SkUiPickerModel.UsesValueText` / `ValueText`, used by the wheels and the date pickers. A custom picker's field text goes through it.
- **Nested scrolling:** the gesture arena already lets a drawn scroller keep a touch from a native parent (Android `RequestDisallowInterceptTouchEvent`, the iOS gate recognizer, Windows DirectManipulation hand-off; [EventMechanism.md](EventMechanism.md)). By default a drawn scroller hands over at its edge. An inline wheel must not hand over (#98: vertical drags on the wheel never scroll the sheet).
- **No haptics in the library today** (no platform code to reuse).

## Phase W: inline wheels (#98)

- **W1 Separate the wheels from the popup.** Size: M–L. **Done** on `1.12-implementation`: `SkUiWheelsView` (`Controls/Basic/SkUiWheelsView.cs`, with `SkUiWheelEffect` and `ISkUiColumnsRules`) chooses through three host callbacks (what is chosen, select, item states changed); `SkUiColumnsPopup` keeps the chrome, the request, Enter / Escape / Tab, focus and release. The existing suite passes unchanged on `net10.0` and `net11.0`; `WheelsViewTests` covers the wheels on their own. The bench has no popup-opening scenario: W2 added `skui-picker-wheels` (inline wheels, which build the same `SkUiWheelsView`), and the popup gained only one content view, so no popup scenario was added.
  - Move the wheel part of `SkUiColumnsPopup` into one internal class, `SkUiWheelsView`: the grid of `WheelView`s, entries, band, fade, keys, type-ahead, semantics, item states and rules.
  - `SkUiColumnsPopup` keeps the chrome and the `ISkUiPickerPopupContent` role, and hosts a `SkUiWheelsView`.
  - Behavior does not change. The existing wheel tests pass unchanged; add tests only where the split adds a seam (the wheels view on its own: positions, keys, semantics).
  - Check that opening the popup is no slower (bench).
- **W2 `SkUiPickerWheels`, the public inline control.** Size: M. **Done** on `1.12-implementation`:
  - `SkUiPickerWheels : SkUiView` hosts a `SkUiWheelsView` as its only child, built again when the columns or the look-affecting properties change (once, when the view reaches a tree, for XAML).
  - The column bookkeeping moved from `SkUiColumnsModel` to `SkUiColumnsTracker` (`ISkUiColumnsHost`), shared by the pickers and the inline wheels.
  - Tests: `PickerWheelsTests` (with XAML), the implicit-style and demo checks, and the leak scenario `PickerWheelsOpened`.
  - Docs ([SkUiPickerWheels.md](../controls/SkUiPickerWheels.md), the controls index, a link from the multi-column picker's page), the changelog and the gallery page (`PickerWheelsDemoPage`, the issue's two layouts) came early from W7, because the demo check requires a page for every control.
  - Bench `skui-picker-wheels` (12 wheels × 3 columns, headless): measure 6.8 ms, record 1.2 ms, about 10.5 MB allocated, mostly the carousels and their items. Column widths are measured once per build (8.6 ms before).
  - Not done: `SelectionBackground` (the band stays the look's `PickerWheelBandColor`); `IsHapticFeedbackEnabled` comes with W3's haptics.
  - `SkUiPickerWheels : SkUiView`. `Columns` (`SkUiPickerColumn`, the XAML content property) works as on `SkUiMultiColumnPicker`, with the same selection rules for each column. Columns can be shared, through `SkUiColumnsModel`.
  - Properties: `VisibleRows` (odd, 3–9, default 5), `RowHeight` (default: the look's `PickerWheelRowHeight`), `CommitDelay` (D2), `IsHapticFeedbackEnabled` (D3), `IsLoopEnabled` is per column (exists).
  - The row text style follows the pickers: font properties, `TextColor`, `SelectionBackground` (the band).
  - Read-only state and events: `SelectedIndices`, `SelectedItems`, `SelectionChanged` (`SkUiColumnSelectionChangedEventArgs`, as the multi-column picker), `SelectionChangedCommand` / `SelectionChangedCommandParameter`.
  - Writes go through `WriteBack` so the app's one-way bindings stay intact (issue #92), and follow the property pattern (`SkUiValidate`, fluent `Set*`).
  - It measures to the column widths × (`VisibleRows` × `RowHeight`).
  - It is one tab stop: Left / Right move between columns, and focus is drawn on the active column.
  - Tests: two-way bindings for each column, `CommitDelay` (no writes while scrolling, one write after it settles), columns added and removed at runtime, items changed while scrolling, right to left, implicit styles (`ImplicitStyleTests`), measuring, and a leak scenario in `tests/Shared/MemoryLeaks`.
- **W3 Settle commit and haptics.** Size: S–M. **Done** on `1.12-implementation`. The settle commit came with W2. One change from the plan: keys and screen reader steps also write after `CommitDelay` (it starts when no wheel turns), so holding a key doesn't write every row. Haptics: `SkUiHaptics` (internal), the public app-wide `SkUiGestureSettings.IsWheelHapticFeedbackEnabled` (default off) and `SkUiPickerWheels.IsHapticFeedbackEnabled` (`bool?`). The popups' wheels follow the app-wide setting. The tick is limited by real time (one per 1/60 s); tests count ticks through a hook. Device checks still to do.
  - D2 on the inline control: the wheel's `IsScrolling` going false starts a timer that `CommitDelay` cancels on renewed scrolling. Keyboard and screen reader steps commit at once.
  - D3: the platform helper `SkUiHaptics.SelectionTick(view)`, called when the row under the band changes during a drag or fling. It is limited to one tick per frame so a fast fling does not buzz. The popup's wheels use it too.
  - Tests: the timer through the frame clock used in tests (headless, no real waiting); the haptics call counted through an internal test hook.
- **W4 Keep vertical drags inside the wheel.** Size: S (Spike first: half a day). **Done headless** on `1.12-implementation`; no spike was needed. `SkUiScrollController.KeepsDrags` (internal; `SkUiCarouselView.KeepsDrags`, on for every wheel) claims drags along its axis also at the edge, which also holds off native ancestors through the existing claim. Neither the drag's rest nor its fling chains to outer drawn scrollers. Mouse wheel and trackpad scrolling still chain outwards. Device checks in a native `ScrollView` and a Community Toolkit sheet are still to do.
  - The wheel claims vertical drags without the edge hand-off, including a non-looping wheel at its first or last row. Horizontal drags still go to the parent (a sheet that swipes sideways, a carousel).
  - Implementation goes through the existing arena (a per-scroller "no edge hand-off" flag) and needs no platform code.
  - Check headless with nested `SkUiScrollView`, and on devices inside a native `ScrollView` and a bottom sheet (Community Toolkit popup) on iOS and Android.
- **W5 Accessibility.** Size: S. **Done headless** on `1.12-implementation`. What the audit found:
  - The semantics were already right. The view adds no element of its own, each wheel is one adjustable element with its value, increment / decrement go through the existing wheel code, and disabled wheels report as disabled with no actions (the bridges send `EnabledActions`).
  - Fixed: the keyboard focus ring was drawn around the whole control. It is now on the active column's wheel (rounded as the band) and follows Left / Right, taps and screen reader moves. `SkUiWheelsView.ShowFocusRing`; the popup still shows none.
  - Decided: Page Up / Page Down stay "visible rows less one", as in the popup (4 rows by default), not the issue's fixed ±5, so the two keep one behavior.
  - A wheel without a `Header` or description has no name. Documented as the app's job; no fallback.
  - Tests: `TheFocusRingIsOnTheActiveColumn`, `ScreenReadersSeeOnlyTheWheels`, `PageKeysMoveByTheVisibleRowsLessOne`. Device checks with VoiceOver, TalkBack and Narrator are still to do.
  - Each column stays one adjustable element: VoiceOver swipe up / down and TalkBack's scroll actions (increment / decrement exist), and the value text is the visible label (exists).
  - Add Page Up / Page Down as ±5 rows on the inline control if the popup's paging is a different step (check).
  - Device checks: VoiceOver (iPhone, iPad), TalkBack (tablet), Narrator.
- **W6 The popup uses the public control.** Size: S. **Done** with W1 and W2: the popup (`SkUiColumnsPopup`) and `SkUiPickerWheels` both host `SkUiWheelsView`, the option this step allowed. The popup keeps its draft / immediate choice in the request; the inline view keeps its settle timer. The multi-column, date and time and bottom-pane wheel tests pass unchanged.
  - `SkUiColumnsPopup` hosts a `SkUiPickerWheels` (or the shared `SkUiWheelsView` underneath it, whichever keeps the draft / immediate logic simpler) so there is one implementation, as #98 asks.
  - The multi-column picker, the date and time pickers and the bottom pane's single-list wheel stay green.
- **W7 Docs, demo, changelog.** Size: S. **Done**: the control page, controls index and multi-column link, the gallery page with both layouts, and the changelog came with W2–W5. W7 added the wheels to [Accessibility.md](Accessibility.md#picker-wheels), `KeepsDrags` to [EventMechanism.md](EventMechanism.md#native-coordination), FR-29 in [Requirements.md](Requirements.md), the roadmap status, and the inline mapping in the migration skill (`skiaui-migrate` controls reference). A Syncfusion line in Migration.md was dropped: the doc lists no third-party pickers.
  - Control page `docs/controls/SkUiPickerWheels.md` and the controls index.
  - The multi-column picker's page links to it ("Inline wheels").
  - A gallery page with the issue's two examples: "every [n] [unit]" with a live readout, and the 3-column "distribute [n] [unit] before [start / due date]" in a sheet.
  - Changelog under `### New features`.
  - No MAUI counterpart, so no Migration row.

**Phase W status:** done headless on `1.12-implementation` (W1–W7). Still open before 1.12.0: the device checks in [Verification](#verification) (haptics on iOS and Android, drags inside a native `ScrollView` and a Community Toolkit sheet, VoiceOver / TalkBack / Narrator on the inline wheels, the built-in pickers' popups unchanged).

## Phase P: pickers of your own

- **P1 Open the picker base classes.** Size: M. **Done** on `1.12-implementation`. Changes from the plan:
  - Clearing is `protected virtual void OnClearSelection()`, not an abstract `ClearChoice`. The built-in pickers keep their internal `ClearChoice` overrides (now a virtual whose default calls `OnClearSelection`), and simple pickers need not implement it.
  - The model asks the picker first (`ISkUiPickerOwner.CreateCustomContent`), then the derived picker's factory, then the list, so a built-in picker's own factory (several choices, wheels) cannot drop an app's content.
  - A picker with neither content nor a `ShowPopupAsync` override opens an empty list; no `Trace` line yet.
  - The test is `CustomPickerTests` (a color picker on both layers). No reflection check: the samples (P4) have no `InternalsVisibleTo` and prove the API is enough.
  - `SkUiPickerBase()` and `SkUiCorePickerBase()` become `protected`.
  - The contract derived pickers implement, on both layers:
    - `protected abstract void ClearChoice()` (exists, `private protected` today): what `ClearSelection()`, the field's clear button and the popup's Clear do.
    - `protected void SetFieldText(string? text)`: the field's text for the current value (`null`: nothing chosen, so the placeholder or title shows). It wraps `UsesValueText` / `ValueText` and the re-measure.
    - `protected virtual SkUiPickerPopupContent? CreatePopupContent(SkUiPickerPopupContext context)`: the popup's content for this opening. The default `null` means the base (the list) for the built-in pickers. A custom picker that neither returns content nor overrides `ShowPopupAsync` writes one `Trace` line and doesn't open.
    - `protected void ClosePopup()` (exists, `private protected`).
  - What stays internal: the model and the list or columns logic. The built-in pickers keep their internal overrides.
  - Tests: a test-only subclass in `tests/MauiSkiaUi.Tests` that uses only public / protected members (a reflection check that its base calls touch no internal member).
- **P2 Public popup content and request.** Size: M–L. **Done** on `1.12-implementation`, except the typed request, which comes with P3. Changes from the plan:
  - The internal record `SkUiPickerPopupContext` was renamed `SkUiPickerPopupSetup`. The public `SkUiPickerPopupContext` is a sealed read-only class built from it.
  - `SkUiPickerPopupContent` is not a view: the internal `SkUiCustomPopup` hosts its body in the chrome. Its hooks are `OnAccepted`, `OnCleared`, `OnPickerChanged` and `OnClosed`, with `Close()` / `Accept()` for the body. There is no `OnKeyDown` hook (`SkUiKeyEvent` is internal): Enter accepts and Escape cancels.
  - Content of the app's gets a non-generic `SkUiCustomPopupRequest`. `SkUiValuePopupRequest<T>` comes with `SkUiValuePicker<T>` (P3), whose request the library creates.
  - `SkUiPickerPopupContext` becomes public, read-only: `Placement`, `Title`, `IsRightToLeft`, the picker's text style (font, size, attributes, color, character spacing, selection background), `RequiresAccept`, `AcceptText`, `CancelText`, `ShowsClear`.
  - `SkUiPickerPopupContent` is a new public abstract class that SkiaUi hosts in the popup chrome (D6):
    - `protected abstract SkUiView CreateBody()` builds the content once per opening.
    - `public virtual Size PreferredSize(double minimumWidth, double maximumWidth, double maximumHeight)` defaults to measuring the body.
    - `protected void Choose()` (immediate mode: apply now) and `protected void Complete()` (apply and close).
    - In draft mode, OK applies, Cancel / Escape / a dismissal discard, and Clear clears.
    - `protected virtual void OnPickerValueChanged()` runs when the picker's value changes while the popup is open.
    - `protected virtual bool OnKeyDown(in SkUiKeyEvent key)`. Enter and Escape are handled by the chrome.
  - It's built on the internal `ISkUiPickerPopupContent` and `SkUiPopupParts`, so the chrome (title, header / footer templates, buttons, bottom-pane toolbar, safe-area padding, rounded clip) is the same as the built-in popups'.
  - The request is `SkUiValuePopupRequest<T>` (public, sealed, a `SkUiPopupRequest<T>`), for presenters that show their own UI: `Value` (as it opened), `SetValue(T)` (live, or a draft until `Accept()`), `Complete(T)`, `Clear()`. The popup session must apply a result for this request type (today `Ended` only reads `SkUiListPopupRequest`). Add a typed result hook to the session, not one more `is` branch per type.
  - Tests: content measured and placed in each placement, draft and immediate modes, Clear, Escape, focus back to the field, a presenter completing the typed request without showing the content, the picker closing the popup (`IsOpen = false`, disabled, removed) while a custom body is open, the content released after close (leak scenario).
- **P3 `SkUiValuePicker<T>` / `SkUiCoreValuePicker<T>`.** Size: M. **Done** on `1.12-implementation`. Changes from the plan:
  - Both classes are abstract: the app must supply the popup body (`CreateValueContent`).
  - "None" is `null`: a value type that can be empty is picked as its nullable (`SkUiValuePicker<TimeSpan?>`), instead of a separate `DefaultValue`.
  - The field's text comes from `ValueFormat`, else `FormatValue`, else `ToString()`. There is no `ValueDisplayBinding` (a binding path is not trimming-safe for an open `T`, and `FormatValue` covers it). No separate `ValueChangedCommand`: the picker's `SelectionChangedCommand` runs before `ValueChanged`.
  - `T` carries `[DynamicallyAccessedMembers(PublicParameterlessConstructor)]`, as `BindableProperty.Create` asks under trimming.
  - The draft lives in the typed request (`SkUiValuePopupRequest<T>`), so the content (`SkUiValuePopupContent<T>`) and a presenter's own UI share one path. The base gained `SetFieldText(text, description)` for `DescribeValue`.
  - Tests: `ValuePickerTests`, with XAML of a closed type and the Core twin.
  - Built only on P1 and P2 (D5).
  - SkUi\*: bindable `Value` (`T?`, two-way by default, written back with `WriteBack`; the `BindableProperty` is a static on the generic type, so one for each closed type), `ValueChanged` (old / new), `ValueChangedCommand`, `DefaultValue`.
  - The field's text comes from `ValueDisplayBinding` or `ValueFormat` (`string.Format`), else `protected virtual string? FormatValue(T value)`, else `ToString()`.
  - `protected abstract SkUiPickerPopupContent CreatePopupContent(...)` returns a content whose body reads and writes `Picker.Value` through a draft that the chrome applies.
  - Core: the same with `SetValue` / `INotifyPropertyChanged`.
  - Everything else (placeholder, title, `UpdateMode`, placement, presenter, `LeadingView`, field chrome, keyboard, semantics) comes from the base.
  - Screen readers: the field reads the formatted value. `protected virtual string? DescribeValue(T value)` gives a spoken text that differs from the shown one (a color's name for a swatch).
  - Tests: the generic statics for each closed type (two closed types side by side), the binding round trip, the change events, the text sources in order, XAML for a closed subclass (`RuntimeXamlCollection`), implicit styles.
- **P4 Samples built on public API only.** Size: S–M. **Done** on `1.12-implementation`: the samples app's **Pickers of your own** page (`CustomPickersSample`, with `ColorPicker`, `ActionSheetColorPicker`, `DurationPicker` and `CoreRatingPicker` in their own files). The samples project has no `InternalsVisibleTo`, and it compiles: the API is enough. Test: `PickerSamplesTests.ThePickersOfYourOwnPickAColorADurationAndARating`. There is no demo gallery page: the value pickers are abstract, so the demo check doesn't ask for one, and the samples page shows them better.
  - In `samples/MauiSkiaUiSamples`:
    - `ColorPicker : SkUiValuePicker<Color>`, a swatch grid body;
    - `DurationPicker : SkUiValuePicker<TimeSpan>`, whose body is a `SkUiPickerWheels` (hours | minutes), showing that phase W's control is also a popup body;
    - a Core variant of one of them.
  - Plus one `ShowPopupAsync` override that answers `SkUiValuePopupRequest<Color>` with the app's own UI (the presenter path).
  - The samples project has no `InternalsVisibleTo`, so this proves D5. Gallery pages in the demo too.
- **P5 Docs.** Size: S. **Done**: the [CustomPickers.md](../controls/CustomPickers.md) guide, linked from the controls index, [SkUiPicker.md](../controls/SkUiPicker.md), [Architecture.md](Architecture.md#popups), [Popups.md](Popups.md#pickers-of-your-own), the roadmap, FR-29 and the migration skill; the changelog entries came with P1–P3.
  - A new guide `docs/controls/CustomPickers.md` ("Pickers of your own"):
    - when to override `ShowPopupAsync` / `SelectItemsAsync` (new UI, existing types);
    - when to derive `SkUiValuePicker<T>` (a new value type);
    - when to derive `SkUiPickerBase` (several values, your own field text rules).
  - It includes the contract, draft vs immediate, presenters, screen readers, and a full color-picker walk-through.
  - Link it from [SkUiPicker.md](../controls/SkUiPicker.md#popups-on-your-popup-system), [Popups.md](Popups.md), the controls index and [Architecture.md](Architecture.md#popups).
  - Changelog under `### New features`.
  - The migration skills: a MAUI app's own picker controls (custom `Picker` handlers) can be pointed at the guide (a note in `skiaui-migrate`, no checker rule).

**Phase P status:** done headless on `1.12-implementation` (P1–P5). Device checks of the samples' pickers in each placement are still open ([Verification](#verification)).

## Order

W1 → W2 → W3 → W4 (spike early, it may change W2's gesture setup) → W5 → W6 → W7, then P1 → P2 → P3 → P4 → P5. P1 can start in parallel with W2 (different files), but P4's duration sample needs W2. The 1.12 changelog section and the roadmap entry are updated as each step lands.

## Not in 1.12

- `SkUiCorePickerWheels` (D1).
- Inline date and time wheels (`SkUiDateTimeWheels` stays internal). Apps can build them from `SkUiPickerWheels` columns, but without the date rules. Make them public once an app asks; the rules are `ISkUiColumnsRules`, which would have to become public too.
- `SkUiPopups.ShowColumnsAsync` (D4).
- Public haptics API (D3).
- Custom field painting: the field stays the look's (`SkUiLook.DrawPickerField`, `LeadingView`, `SelectedItemTemplate` on the list picker). A custom picker that needs another field restyles it through the look.

## Verification

- Headless suite green on `net10.0` and `net11.0`. Build with 0 warnings, including trim / AOT analyzers on the generic value picker (statics on generic types and `ValueDisplayBinding` must be trimming-safe, as `ItemDisplayBinding` is).
- API comparison against 1.11.0: additions only.
- `scripts/bench.sh --baseline v1.11.0`: no regressions in the picker scenarios or in popup opening.
- Devices (`scripts/device_verify.sh`), iPhone and iPad simulators (iOS 26), an Android tablet emulator and a phone, Windows, Mac Catalyst:
  - the #98 sheet: wheels inside a scrolling sheet don't scroll it (W4), commit after settling (W3), haptics on iOS and Android with the setting on and none with it off;
  - VoiceOver / TalkBack / Narrator on inline wheels (W5);
  - the built-in multi-column, date and time pickers and the iOS bottom pane wheel unchanged after W1 and W6;
  - the sample color and duration pickers in each placement (anchored, window, bottom pane), draft and immediate, with the Community Toolkit presenter and the action sheet fallback (the fallback cannot show a custom body: it writes the `Trace` line and doesn't open; document it).
- Memory-leak scenarios for the inline wheels and a custom picker's popup, headless and on devices (`scripts/device_tests.sh`).

## Release

`1.12.0` ([Releasing.md](../Releasing.md)): changelog `## Unreleased` → `## 1.12.0`, roadmap row and section to shipped, close #98 with a link to the docs.
