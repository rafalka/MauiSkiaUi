# SkiaUi testing considerations

How we plan to verify **unit behavior** and **core mechanisms** (layout, paint, animation, gestures, hosting) — including what can run fully automated in CI, and where AI agents help.

Aligns with [Requirements.md](Requirements.md) (especially NFR-2 correctness-first → optimize), [LayoutSystem.md](LayoutSystem.md), [DrawingMechanism.md](DrawingMechanism.md), [AnimationMechanism.md](AnimationMechanism.md), and [EventMechanism.md](EventMechanism.md).

## Automated tests (by area)

Run `dotnet test tests/MauiSkiaUi.Tests/MauiSkiaUi.Tests.csproj`. The library's plain `net10.0` target shares the actual node/layout/paint/input/clock sources with the device targets; only the native handler and builder registration are platform-conditional. Tests use real MAUI views without handlers and CPU Skia bitmaps, not replacement view mocks.

| Test class | Coverage |
| --- | --- |
| `PipelineTests` | Frame renderer, paint layers, measure/arrange, hit-testing, animation clock, ownership, Box/Ellipse/Line |
| `BasicControlsTests` | Label, Button, Image, Border, ActivityIndicator, toggles/chrome |
| `LayoutTests` | Grid, AbsoluteLayout batching, layout culling/Z-order, BackgroundColor semantics |
| `ScrollViewTests` | Offsets, pan, render-thread fling / animated scroll, wheel, no re-record on offset, scroll goldens |
| `RenderingTests` | Retained compositor: render-thread animation with a stalled UI, selective re-record, one root signal per frame, clip defaults, reparenting, immediate vs composited parity |
| `TextAndImageTests` | Linear wrap, grapheme-safe breaks, truncation, paint-only label changes, downsampled decode |
| `MauiContentViewTests` | Native overlay measure/arrange/touch contract, root-relative frames |
| `PerformanceTests` | 1,000-label warm recording measurement (no timing assertion) |
| `CoreLayerTests` | `MauiSkiaUi.Core` smoke/regression |
| `LookAndColorSchemeTests` | FR-18 / FR-19 look and color scheme |
| `ToggleStateTests`, `SliderTests`, `ProgressBarTests` | Three-state toggles and binding write-back, slider range / input / scroll competition, progress fill / indeterminate slide / `ProgressTo` |
| `TransitionTests` | FR-26 state-change transitions on a deterministic clock: switch slide and mid-way reversal (one re-record per frame), no animation before the first frame or with reduced motion, stopped clock jumps to the end, quick-tap press and ripple origin, ripple pixels, slider glide, progress smoothing, Core parity |
| `AnimationClockTests` | Callbacks that dispose animations or stop the clock mid-tick; the internal stopped callback |
| `ContentAndTemplateTests` | P9: button image measure and placement per `ContentLayout` (RTL, scaled-down images, Core pixels, MAUI markup); radio button text / view content, chrome; MAUI's RadioButton `ControlTemplate` sample with drawn views and `SkUiContentPresenter` |
| `AccessibilityTests`, `KeyboardFocusTests`, `FontScalingTests`, `AccessibilitySwitchTests` | P10: semantics trees of both layers (MAUI semantic properties, merged tappable groups, visible bounds through scrolling and clips, actions per control, ids and change reports, hit-testing); MAUI `Focus()` / `Unfocus()` and visual states on drawn views, Core focus, tab order, keys, focus ring pixels, native focus hooks; `FontAutoScalingEnabled` on labels, buttons, radio text, spans, HTML, font images (global: `SkUiFontScaling.Factor`, focus-visible state) |
| `TextEditingTests`, `EntryTests`, `EditorTests`, `SearchBarTests`, `StepperTests`, `TextFieldAutofillTests` | Phase D: the editing engine (graphemes, words, bidi carets, composition, undo coalescing, incremental layout), fields of both layers (keys, mouse and touch selection, handles and the edit menu, clipboard, password, `MaxLength`, return behavior, focus without accessibility, keyboard avoidance, the proxy following the field, screen-reader text fields), the editor's wrapping and scrolling, the search bar, the stepper's coercion, buttons, repeats and value field, what autofill gets (the form, value changes, fills, fields leaving); a recording proxy stands in for the platform |
| `ComponentDemoTests` | One demo page per concrete control (editors/reset contract) |
| `MemoryLeakTests` | Leak scenarios (below): nothing a scenario built survives its surfaces; the detector self-test; long-lived commands drop listeners |

Clock tests never sleep. Interior pixels are exact; antialiased edge baselines and font goldens remain later work where not already covered by scroll solid-color goldens.

Reproduce the non-gating measurement:

```bash
dotnet test tests/MauiSkiaUi.Tests/MauiSkiaUi.Tests.csproj --filter FullyQualifiedName~ThousandLabel --logger 'console;verbosity=detailed'
```

It constructs 1,000 labels, measures/arranges a 400x600-DIP viewport, warms one picture recording, then records 30 frames. Before clip rejection/cached Z-order: 8.106 ms and 568,384 managed bytes/frame. After: 0.635 ms and 9,488 bytes/frame on the same Mac, Debug, .NET 10. These are indicative CPU measurements, not GPU FPS; no timing assertion is used. Remaining allocations include visible labels' font/paint resources, and native allocations are excluded. Use the device stress page's **Record** action for CPU recording of 1,000 buttons, and a native profiler for actual presentation timing.

## Memory leak tests

One scenario catalog, [`tests/Shared/MemoryLeaks`](../../tests/Shared/MemoryLeaks/LeakScenarios.cs), runs in two places:

- **Headless** (`MemoryLeakTests`, part of `dotnet test` and CI). Each scenario is hosted on test surfaces, exercised, and its surfaces are disposed like a handler disconnect.
- **On a device** ([`tests/MauiSkiaUi.DeviceTests`](../../tests/MauiSkiaUi.DeviceTests), an app for Android, iOS and Mac Catalyst). Each scenario's page is pushed on a Shell stack, exercised with real handlers and platform views, then popped; Shell disconnects its handlers, as in apps.

**What a scenario does.** Leaks usually come from state that only exists after interaction, so every scenario exercises its UI first:

| Scenario | Exercise |
| --- | --- |
| `ButtonsClicked` | Buttons bound to a long-lived command, clicked twice each, disabled and re-enabled, a cancelled press; an image button |
| `TogglesTapped` | Switch, check boxes (one three-state), radio group, tapped several times |
| `SlidersAndProgress` | Horizontal, vertical and Core sliders dragged and tapped; progress bars animating and indeterminate at close |
| `LabelsReshaped` | Wrapped, truncated, RTL, Arabic, emoji, Simple / Shaped labels; text and width changed repeatedly |
| `ContentTemplated` | Buttons with images (a long-lived shared icon edited while shown); radio buttons with text and view content and a long-lived shared `ControlTemplate`: tapped, content swapped, template removed and applied again; Core radio buttons grouped by `SkUiCoreRadioButtons.Group` with a long-lived callback |
| `ImagesReloaded` | Stream images: sources swapped, reloaded, aspect changed |
| `AvatarsShown` | Avatar rows in a collection view (initials from bound names, badges) flung so rows recycle; avatars with a cached photo and a shared icon enlarged and their photos swapped and cleared; a Core avatar |
| `LayoutsRelayout` | Grid, stacks, absolute layout, border: resized; children added, removed, reordered, hidden; definitions changed |
| `BindableLayoutItems` | MAUI `BindableLayout` on a wrap layout bound to a long-lived collection and on a stack with a template selector and an empty view: items added, inserted, replaced, moved, removed; cleared to the empty view and refilled; items source swapped |
| `ShimmersShown` | A shimmer of Core bones (repeated, surface-wide wave) switched to its content and back with a shared long-lived fade, its effect changed while shown; a shimmer of SkUi and Core bones in a card switched between bones and silhouette and to its content; a Core shimmer switched with a fade; a shimmer and a Core shimmer whose content is the skeleton (labels with and without text, a button with a long-lived command, a kept title, a card, an empty collection view showing placeholder rows) switched between bones and silhouette, a view's skeleton value changed, loaded and loading again; shimmer rows bound to a long-lived source in a collection view, flung so rows recycle; closed while shimmering |
| `StatesSwitched` | `SkUiStateContainer` on a grid and a stack: loading (spinner running), error (retry button, long-lived command) and empty states, switched directly and with the fade; a change rejected while one runs; hidden state views removed; automatic state change animations (a shared long-lived animation) retargeted while running; closed while one runs |
| `ContentDeferred` | Three tabs of `SkUiContentView` sections that load when shown (explicit content and templates with a long-lived command; delay, fade-in): tabs switched so some load, a waiting section removed, closed with sections waiting and delay timers pending |
| `ExpanderToggled` | `SkUiExpander` sections (explicit, template and lazy content, a hosted Entry, a long-lived command, both directions): expanded and collapsed by header taps, animated and not, reversed mid-animation, content replaced while collapsed; closed mid-collapse |
| `SwipeViewsSwiped` | `SkUiSwipeView` rows in a `SkUiCollectionView` over a long-lived collection, sharing long-lived swipe items (declared with `LeakScenarioContext.LongLived`, so the tree walk does not track them) and a long-lived command: rows swiped open and closed, items tapped (a drawn item view, a swipe item that removes its row), opened by code, closed by flinging the list, an execute-mode swipe, a row recycled while open; closed mid-swipe |
| `AlternateSwitched` | `SkUiAlternateContentView` cards (explicit content and templates with a long-lived command, shared long-lived animations): switched directly and animated, retargeted while switching, an alternate replaced while hidden; closed mid-switch |
| `ScrollFling` | Vertical list with a nested carousel: drags, flings, animated scroll; closed mid-fling |
| `VirtualListScrolled` | `SkUiVirtualScrollView` bound to a long-lived collection (items of different heights, recycled template views): dragged, flung, scrolled to an index, items inserted and removed while shown; closed mid-fling |
| `CollectionViewUsed` | `SkUiCollectionView` bound to a long-lived collection, with long-lived selection, item-tap and refresh commands, a sticky header and an empty view: items tapped to select and deselect, flung, scrolled to an item, pulled to refresh, the selected item removed, the collection emptied and refilled; closed mid-fling |
| `RefreshPulled` | `SkUiRefreshView` (inline style, automatic completion) with a long-lived command around a header and a drawn scroller: pulled through the scroller and by the header (content held down while refreshing), each refresh ended by a deferral taken in `Refreshing`, a short pull released before the trigger (the indicator going back), the scroller replaced while the view still links to it (the old one must go); closed mid-pull |
| `CollectionViewGrouped` | `SkUiCollectionView` over long-lived groups (a grid of two columns, sticky collapsible group headers), with a long-lived selected-items list and load-more command: headers tapped to collapse and expand, items selected, flung, scrolled to a group, groups and items added and removed, the template replaced; closed mid-fling |
| `GesturesMixed` | Tap, double tap, long press, swipe, pan, pinch (drawn and Core); closed with a finger down |
| `TextFieldsEdited` | Drawn entries (a password, a search bar with a long-lived command), an editor and a stepper showing its value, with their Core twins: focused (text input attached), typed in, a word long-pressed (handles and the edit menu), tabbed through, the editor scrolled, the stepper stepped and its value typed, text set by code; closed with a field focused |
| `AccessibleFocused` | Drawn and Core controls with semantic properties, read through a semantics tree that reports changes; focused by `Focus()` and Tab, activated by keys and screen-reader actions; a focused row removed; buttons tagged with a long-lived object |
| `AnimationsRunning` | Render-thread animations and a spinner running at close; nodes detached mid-animation |
| `SurfaceReplaced` | A page replaces its GPU surface with a software one and back (device only) |
| `MovedBetweenSurfaces` | A subtree with gestures, a scroller and a native Entry moves between two surfaces |
| `CoreControls` | Core buttons, toggles, table and scroll view: clicked, scrolled, rows removed |
| `NativeOverlays` | Entry / Editor overlays in a drawn scroller (snapshot mode): scrolled, focused, content replaced |
| `NativeNesting` | GPU and software surfaces inside a native `ScrollView`, each dragged |

**What is checked.**

- **Interaction took effect.** Each run checks its own results (clicks counted, every gesture raised, offsets moved). A leak check over an interaction that silently did nothing would prove nothing.
- **Removed while open.** Objects a scenario removes (children, replaced overlay content and its native view, discarded surfaces) must be collectable while the rest of the page is still alive. This catches leaks that grow for as long as a long-lived page lives.
- **After close.** Everything else must be collectable: the page, every MAUI view, drawn view and Core node, each handler and platform view, and what surface handlers own (renderer, compositor, GPU / software surface, overlay container). Survivors are listed by type and text, e.g. `SkUiViewHandler of SkUiCoreHost`.
- **The detector itself.** A scenario that deliberately retains its root must be reported (`DetectorReportsADeliberateLeak`; the device app runs it first).

**Text focus.** Android's `InputMethodManager` and MAUI's Apple keyboard helper keep the last focused text field until another one gains focus; a plain MAUI page does the same. After a scenario focused a field, the device runner therefore focuses the test page's own field, as the next screen of an app would.

**Run on a device:**

```bash
scripts/device_tests.sh -t maccatalyst
scripts/device_tests.sh -t ios                          # booted simulator; -s UDID for a device
scripts/device_tests.sh -t android -s 2299011508047ece
scripts/device_tests.sh -t android -S ButtonsClicked,NativeOverlays
```

The script builds the app in Release (no debugger or Hot Reload keeping instances alive), launches it with `--autorun --exit` and collects its console lines:

- `SKUILEAK_START`;
- `SKUILEAK_RENDER {json}` for the render check;
- `SKUILEAK_HOSTED {json}` for the hosted-control check;
- `SKUILEAK_DETECTOR {json}` for the self-test;
- one `SKUILEAK {json}` per scenario;
- `SKUILEAK_DONE`.

It prints a table and exits non-zero on any failure. Logs go to `artifacts/device-tests/<timestamp>/`.

- **`--aot`:** builds the app with Native AOT (which implies full trimming).
- **`--trim`:** builds it fully trimmed, without AOT.
- Either way, the script fails when the app build reports a trim or AOT warning for SkiaUi code.
- **Render check:** before the scenarios, the app draws a label, a button and an app-defined view with an overlay layer offscreen, and checks their pixels (`SKUILEAK_RENDER`). Leak scenarios alone would not notice a build where drawing silently breaks. It also loads a `MauiImage` (an SVG item, by its generated PNG name) and checks it lays out at its 48 × 48 base size from the display-density file, and that a `FontImageSource` glyph renders synchronously in a `ConfigureFonts` font: the platform-specific image lookups of P4. Last, it checks that `SkUiWeakEvent` still tells closures from objects in that build (a lambda capturing a local keeps firing after a collection, an object's handler does not keep it alive): it relies on compiler-generated type names, which trimming or Native AOT could change.
- **Hosted-control check (A6):** `HostedControlsCheck` opens a page with an Entry, an Editor, a WebView, an Entry in a nested horizontal scroller, one in a collapsed expander and one that gets replaced, all hosted in a drawn scroller. After each step it reads every native view back from the platform (`SkUiMauiContentView.GetNativePlacement`: the clip wrapper's and the view's laid-out geometry, whether it shows) and compares it with the drawn tree's frame, visible part and hidden state, within 1 DIP (`SkUiMauiContentView.FindNativePlacementMismatch`, also behind the demo's **Check native placement**). The steps are: first layout, instant scrolls of both scrollers, an animated scroll (snapshots on Android / Windows, live placement on Apple; the motion must be observed, else the step fails) and the restore after it, a focused Entry that keeps focus and stays live while scrolling, the same tree on a new page, expand and collapse, and a replaced control (`SKUILEAK_HOSTED`). `--placement-tolerance -1` must make it fail (`scripts/device_tests.sh -t maccatalyst -S NativeOverlays --placement-tolerance -1`): proof that it compares real native geometry.
- **CI** runs the Mac Catalyst device tests with `--aot`. iOS devices below iOS 17 (not supported by `devicectl`) are launched through `mlaunch` (`dotnet build -t:Run`).

Launched normally, the app shows the test page: **Run all**, **Rerun failed**, **Self-checks** (detector and render check), and **Run** per scenario, with results and survivors inline.

## Device profiling (dotnet-trace)

Always profile **Release** builds. The demo enables emulator/simulator diagnostic defaults from the [MAUI profiling guide](https://learn.microsoft.com/dotnet/maui/fundamentals/profiling) unless you pass `-p:EnableProfiling=false`: Android emulator `10.0.2.2` + `connect`, iOS simulator `127.0.0.1` + `listen`, port `9000`, `DiagnosticSuspend=false`.

```bash
# Android emulator (runtime profiling)
dotnet build MauiSkiaUiDemo/MauiSkiaUiDemo.csproj -t:Run -c Release -f net10.0-android

# iOS simulator
dotnet build MauiSkiaUiDemo/MauiSkiaUiDemo.csproj -t:Run -c Release -f net10.0-ios

# Startup profiling: -p:DiagnosticSuspend=true
# Physical Android device: -p:DiagnosticAddress=127.0.0.1
# Store/CI packages without diagnostic endpoints: -p:EnableProfiling=false
```

Collect with `dotnet-trace collect --dsrouter android-emu --format speedscope` (or `ios-sim` / `android` / `ios`). Do not ship packages built with profiling enabled.

## Device acceptance checklists (still open)

Automated device inspection is blocked by `MSB4099` in the installed MAUI extension's DevFlow injection targets on some machines; use [`scripts/device_verify.sh`](../../scripts/device_verify.sh) for plain CLI launches. No screenshot, contrast, or GPU frame-rate result is claimed until checked on-device.

### Primitives

- Launch on Android or Apple; confirm the box, ellipse, line, and software strip appear.
- Inspect `Scene` and hosted descendants: non-zero bounds; only `Scene` and `SoftwareSample` own handlers.
- Tap the box/ellipse through native input; verify `TapStatus` increments and the fill changes.
- Replay animation; verify intermediate transforms change, then `AnimationStatus` returns to `Idle` and the root clock/render loop stops.
- Inspect rendered native label/button colors and a screenshot, including a compact viewport.

### Composition + Stress

- Launch Controls at compact phone and tablet/desktop sizes; inspect `ControlsHost`, `ControlsScroller`, `EarthImage`, and `AddObservation` bounds. Verify one native surface and handlerless descendants.
- Confirm offline Earth image, text wrapping, Grid columns, style colors, pressed/disabled feedback, observation count binding and reset command.
- Pan from a button: no click after threshold; fling settles; new press interrupts. Tap after scrolling must hit the translated control. Exercise Back to top and desktop wheel input.
- Navigate to Stress, confirm config-only UI, Run test (default 1,000 children / HW on), note timings, scroll to the last button, tap it, run Record/Scroll/Top, and navigate back. No stale animations or extra surfaces should survive navigation.
- Query actual runtime foreground/background colors and inspect screenshots. Accessibility and keyboard: see [below](#accessibility-and-keyboard-p10).

### Native overlay (`SkUiMauiContentView`)

- Confirm the native `Editor`/`WebView` overlay actually renders and receives input on `MauiContentViewDemoPage`, positioned correctly over its Skia host, including after scrolling/resizing the page (v1 does not snapshot during scroll, so any positioning drift while scrolling would be visible here first). The page uses a responsive 2-column `SkUiGrid` (tablet: labels above Editor|WebView; phone: label|control per row) with a full-width Refresh button, and the Editor's HTML text live-updates the WebView — verify both the live-typing update and the button, and that labels/button stay aligned with the two native overlays when rotating or resizing across the 720 DIP breakpoint.
- Confirm the overlay repositions correctly when nested inside other layouts (Grid/StackLayout/AbsoluteLayout), not just a single ContentView, and when an ancestor's `TranslationX`/`TranslationY` changes (e.g. during a transform animation), since `ComputeRootRelativeFrame()`'s accumulation is unverified on a real native container.
- Inspect Basic controls' rendered colors/contrast (Switch/CheckBox/RadioButton especially) and the Border's rounded-clip content.
- Verify Stack/Absolute layouts' native-vs-drawn spacing and positions visually match at a few sizes/orientations.

### Hosted controls (A6)

Automated: headless `HostedControlsTests`, `OverlayScrollTests`, `MauiContentViewTests` (also the hosted cases in `ExpanderTests`, `RtlLayoutTests`, `AccessibilityTests`) and the `NativeOverlays` leak scenario; on devices, the hosted-control check of `scripts/device_tests.sh` (above). The checks below need a person (soft keyboard, IME, native touch handling).

Page: **Native overlays in ScrollView** in the demo (Entry ×3, Editor, Entry in a nested carousel, Entry in an expander, a WebView that scrolls inside, a last Entry at the end of the list). **Check native placement** compares every native view with the drawn tree and reports in the status line; use it after each step, also with the keyboard up.

1. **Focus and soft keyboard:** tap Entry 1, type; tap the last Entry (end of the list) with the list scrolled so it sits in the lower half: the keyboard must not cover it (the page or the list moves it into view) and the caret shows. Check native placement with the keyboard up, then after it closes. Rotate with the keyboard up.
2. **IME composition:** type with a composing keyboard (Japanese / Chinese pinyin / Korean; Gboard or the iOS keyboard): the marked text and candidates show at the field; autocorrect and dictation insert into it. Select text: handles and the edit menu (copy, paste) work, and the loupe on iOS.
3. **Scroll while focused:** with Entry 1 focused (keyboard up), drag and fling the list: the field stays live (no red snapshot outline) and moves with the list; typing goes on after the fling.
4. **Snapshots (Android, Windows):** fling the list: every overlay shows a red outline (HighlightSnapshots) and moves exactly with the drawn rows, also with **Stall 2s** pressed during the fling; when the list stops, the native views come back in place without a jump or flash. Repeat on the carousel (horizontal).
5. **Live mode (iOS, Mac Catalyst; ScrollMode Live elsewhere):** drag slowly and fling: the native views follow the rows with at most a frame of lag and never cover the drawn header / footer.
6. **Drags that start on a native control:** start a vertical drag on an Entry: the list scrolls (the Entry does not select text); a tap places the caret; a horizontal drag on an Entry selects text. Windows: the same with touch and pen; a mouse drag selects text.
7. **WebView scroll nesting:** drag inside the WebView: its page scrolls first; at its end the gesture does not leak into odd states (the drawn list scrolls on the next drag). Pinch-zoom inside the WebView stays native.
8. **Expander:** expand: the Entry appears with the content (clipped while the content grows), stays aligned when the list scrolls; collapse: it hides with the content.
9. **Navigation:** focus an Entry, navigate back with the keyboard up, open the page again: no stale native view on the other page, the keyboard closed.

Record what was observed per platform here (date, device, result). **Run 2026-10-06:** hosted-control check passed on Mac Catalyst (macOS 27), an iPad simulator (iOS 26.5) and a Galaxy S9 (Android 10), after it found that a focused Entry scrolled out of view lost focus on Android (fixed); the manual steps above are not run yet. Windows not run.

### Drawn text input (Phase D)

Pages: **Entry**, **Editor**, **SearchBar** and **Stepper** in the demo's component gallery (each next to MAUI's control), and the fields in a drawn scroller. Proxies: [TextInput.md](TextInput.md#platform-proxies-d2). Checked so far: an Android emulator with Gboard (typing, suggestions, backspace, long-press selection, handles, the floating toolbar, copy / paste, horizontal scrolling) and the iOS simulator (typing, autocapitalization). Windows 11 (2026-10-15, hardware keyboard and mouse, Polish layout): typing, Enter / Completed, arrows, Ctrl + arrows, Home / End, Shift selection both ways, Backspace / Delete, Ctrl + A / C / V / Z / Y with word-level undo, non-Latin letters and an emoji, password masking, the editor's lines and Up / Down, the search bar's search, the stepper's typed value, clicks, double click, the right-click menu (and closing it every way), focus moving to and from native fields by click and Tab. Reported 2026-10-16: physical iOS and Android devices, Mac Catalyst, the Windows touch keyboard and IMEs. Android autofill (D14) on a Galaxy S9 (Android 10, Samsung Pass, 2026-10-16): the service parsed the drawn user name and password by their hints and the code field by its placeholder, offered its credentials on the drawn user name field, and offered to save the typed pair on **Sign in** (`SkUiAutofill.Commit`). Run 2 (2026-10-16, samples app): with the code field in the sign-in form, Samsung Pass saved the code as the user name, for the native form as much as the drawn one (the code moved to its own step); the save prompt after **Sign in** on the drawn form showed, then closed by itself about 2.5 s later as the code page appeared, after Android focus had moved to a hosted `EditText` on another page (open: why focus lands there when a focused drawn field's page is replaced); the native form showed no prompt in that run. Keys injected with `adb shell input text` right after a field takes focus are partly lost while the service's popup opens, on the native `Entry` as much as on the drawn field: type by hand. Still open: the steps below for composition, dictation and the other password managers.

1. **Multiline keys:** in the editor, Enter after a word that is still composing keeps the word and starts a line; Backspace at a line's start joins it to the previous line (Samsung Keyboard sends both as key events, Gboard as text). Checked on a Galaxy S9 with Samsung Keyboard (2026-10-15).
2. **Keyboard and focus:** tap a field: the keyboard shows and the caret blinks; tap another field: the keyboard stays; tap outside or press Done: it hides (Android hides on Return, iOS gives focus up). `Keyboard="Numeric"`, `Email`, `Telephone`, `Url` and `ReturnType` change the keyboard and its return key; a password field gets the password keyboard and no suggestions.
3. **Composition:** Japanese (romaji → kana → kanji), Chinese pinyin and Korean (Hangul syllables built and broken by Backspace) on Gboard, Samsung Keyboard and the iOS / macOS / Windows IMEs: marked text underlined, the candidate window at the field, committing and cancelling. Reconversion of committed text where the IME offers it.
4. **Autocorrect, suggestions, dictation:** accept a suggestion (it replaces the word), undo an autocorrection, dictate a sentence (the dictation placeholder at the caret on iOS), swipe typing.
5. **Autofill and password managers:** a username and password pair (`AutofillHint`) offers saved passwords and one-time codes and saves new ones: iOS through the Passwords app, Android through the autofill service (D14); Windows: nothing expected. Steps [below](#password-managers-and-autofill).
6. **Selection:** long press, double tap (at a normal finger's pace, the field focused or not: the word stays selected with the menu over it), handles dragged across lines and out of a scrolled field, the edit menu's cut, copy, paste and select all; mouse and trackpad selection, right click, Shift + arrows, ⌘ / Ctrl shortcuts and undo on Mac Catalyst and Windows.
7. **Moving the caret (D15):** Android: a tap shows the drop under the caret (hidden after 4 s, when typing); dragging it moves the caret with the system magnifier above the finger, also when the handle hangs below the field. iOS: dragging the caret moves it with the loupe (iOS 17+); a double tap on the word under the caret still selects it. Checked on a Galaxy S9 (Android 10) and the iOS 26.5 simulator (2026-10-15).
8. **Keyboard avoidance:** fields at the bottom of a drawn scroller come above the keyboard when focused and when the keyboard changes height (suggestions bar, emoji keyboard, rotation); scrolling the page with the keyboard up keeps the candidate window at the field.
9. **Screen readers:** TalkBack, VoiceOver and Narrator read the placeholder and the text (bullets for a password), set the text, move the cursor; VoiceOver's rotor by characters and words.
10. **Lists and popups:** fields in collection view rows keep their text when rows recycle; a drawn picker popup opened over a focused field draws on top.

Every step below compares the drawn field with MAUI's native one on the same page (the demo's gallery pages show both; the samples app's **Sign-in form** has a drawn form and a native one). What the native field does is the expected result; a difference is a bug unless the step says otherwise. The **Entry** page's feedback line shows `Text`, `CursorPosition` and `SelectionLength` after each change: check it after each commit.

#### Input method composition (CJK, Korean)

Set up the input methods once:

| Platform | Where | Add |
| --- | --- | --- |
| Android (Gboard) | Settings › System › Languages & input › On-screen keyboard › Gboard › Languages › Add keyboard | Japanese (Kana *and* QWERTY), Chinese (Simplified) › Pinyin – QWERTY, Korean › 2-Bulsik. Switch with the globe key or a long press on Space. Gboard on an emulator commits without composing: use a device |
| Android (Samsung) | Settings › General management › Samsung Keyboard settings › Languages and types › Manage input languages | 日本語, 中文(简体), 한국어 (keyboard type Qwerty) |
| iOS / iPadOS | Settings › General › Keyboard › Keyboards › Add New Keyboard | Japanese › Romaji *and* Kana, Chinese, Simplified › Pinyin – QWERTY, Korean › Standard. Switch with the globe key |
| Mac Catalyst | System Settings › Keyboard › Text Input › Input Sources › Edit… › + | Japanese – Romaji, Chinese, Simplified › Pinyin – Simplified, Korean › 2-Set Korean. Switch with Ctrl + Space or the Globe key |
| Windows 11 | Settings › Time & language › Language & region › Add a language | Japanese (Microsoft IME), Chinese (Simplified, China; Microsoft Pinyin), Korean (Microsoft IME). Switch with Win + Space; Japanese and Korean toggle native input with Alt + \` / Right Alt (한/영) |

Run each case in the **Entry** page's field, then the **Editor**'s (cases 1, 4, 6 and 9 also matter there), on each platform:

1. **Japanese, romaji to kanji.** Empty field, type `nihongo`. Expected: `にほんご` underlined (the composition), the candidate bar or window at the caret, not at a screen corner or the previous field; `Text` does not change while composing on iOS / Mac (it changes on Android, as a native `EditText`: compare). Press Space (or tap 変換): `日本語` offered; pick it, press Return / tap 確定: `日本語` committed, no underline, caret after it, `Text` = `日本語`. Return committing a composition must not complete an entry (`Completed` not raised) or start a line in the editor.
2. **Segments.** Type `watashihagakuseidesu`, press Space: the text splits into segments, the active one marked thicker. Shift + ← / → (iOS: tap a segment; Android: the candidate strip's arrows) resizes / moves between segments; ← / → move *inside the composition*, not the field's caret. Commit: the whole sentence lands once, nothing doubled.
3. **Kana keyboard (flick).** Japanese Kana on iOS / Gboard 12-key: flick-type か → き, then the ゛ key: き becomes ぎ in place (the composition is replaced, not appended).
4. **Chinese pinyin.** Type `zhongwen`: the pinyin shows in the field underlined (iOS: in the field; Windows Pinyin: in its own window at the caret), the candidates `中文` …; pick by tap or the digit key: `中文` committed. Type `nihao` and Space: the first candidate commits. Type `xian`, then pick 西安 (two characters) and 先 (one): each replaces the whole pinyin.
5. **Korean syllables.** 2-set: type ㅎ ㅏ ㄴ: one syllable `한` composed (underlined or highlighted). Type ㄱ ㅡ ㄹ: `한글`. Then the reflow case: empty field, type ㅎ ㅏ ㄴ ㅏ: `하나` (the ㄴ moves to the second syllable; the field must show `하` then `하나`, never `한ㅏ`). Backspace breaks the last syllable jamo by jamo (`한` → `하` → `ㅎ` → empty), one Backspace per jamo, not one per syllable. Space commits.
6. **Leaving a composition.** Start a composition in each language (`にほ`, `zhong`, `한`), then each of: tap elsewhere in the same field; tap another drawn field; tap the native field; Tab (hardware keyboard); scroll the page; rotate the device; press the clear button. Expected: the composition commits (or, where the native field drops it, is dropped the same way), never doubled, never left underlined in an unfocused field. **Korean last syllable:** type `안녕` and immediately tap the next field: `Text` must be `안녕`, not `안녀` (the classic lost-syllable bug).
7. **In existing text.** With `abc def` in the field, put the caret between `abc` and ` def` and compose `にほん`, commit: `abcにほん def`. Select `abc` and compose: the composition replaces the selection.
8. **Limits and rules.** Set `MaxLength` to 5 and compose `nihongonobenkyou`: while composing it may show more (as native), after commit at most 5 characters stay, no crash, caret at the end. With `InputFormatters` = **No spaces, lower case**: compose Korean or Japanese with spaces between words: formatted when committed, not while composing. `IsPassword` on: the IMEs fall back to Latin input (no composition), as native.
9. **Undo.** Hardware keyboard (Mac, Windows, iPad, Android with a keyboard): commit `日本語`, Cmd / Ctrl + Z removes the whole committed text in one step; Cmd / Ctrl + Shift + Z (Ctrl + Y) brings it back.
10. **Reconversion.** Windows: select committed `日本語` and press the 変換 key (or the IME's Reconvert); macOS: select it and press Ctrl + Shift + R or the Kana key twice. iOS / Android: tap a committed word with Japanese active. Expected: candidates for it, and picking one replaces only that word.
11. **Geometry.** In the **Editor** with three wrapped lines, compose on the last line: the candidate window follows the caret to that line; scroll the editor or page while composing: the window stays at the caret (Android after the next frame).

Record failures with the platform, keyboard, field, case number and what the field showed (a screen recording helps for case 5 and 6).

#### Dictation

Turn it on once: iOS Settings › General › Keyboard › Enable Dictation; Android: Gboard's microphone key (Settings › Voice typing on); macOS System Settings › Keyboard › Dictation (shortcut: press 🎤 / Fn twice); Windows Settings › Time & language › Speech › Voice typing (Win + H).

1. **A sentence.** Empty **Editor**, start dictation, say *"Hello world comma this is a test period new line second line"*, stop. Expected: `Hello world, this is a test.` then a line break and `Second line` (punctuation and *new line* as native), caret at the end, `Text` updated. iOS shows a placeholder (a waveform or dots) at the caret while it recognizes: it must sit at the caret, not at the start of the field, and go away when the text arrives.
2. **Into an entry.** The same in the **Entry**: *new line* inserts nothing (or a space, as native), Return-free.
3. **Replace a selection.** Select a word and dictate a word: it replaces the selection.
4. **In the middle.** Put the caret between two words and dictate: the text lands at the caret with spaces as native.
5. **Typing while dictating** (iOS 16+, Gboard): start dictation, type a letter on the keyboard: dictation goes on or stops as with the native field; nothing doubled.
6. **Edit afterwards.** Undo (shake on iOS, Cmd / Ctrl + Z with a keyboard) removes the dictated phrase in one step; tap a dictated word: iOS offers alternatives (blue underline on the native field; the drawn field may not show the underline: note it).
7. **Leaving.** Dictate and tap another field before the text arrives: the text lands in the field it was dictated into, or is dropped, as native, never in the other field.

#### Password managers and autofill

Page: **Sign-in form** in the samples app (`samples/MauiSkiaUiSamples`, Controls section; a pushed page, as it must be: on Android, Shell's flyout and tab root pages sit in a `ViewPager2` that hides them from autofill, native fields included): a drawn form (user name with `AutofillHint` `Username`, password, a new-password pair with `NewPassword` on the **Create account** side) and the same form with MAUI's native `Entry` below it. **Sign in** commits the autofill context (`SkUiAutofill.Commit`) and opens a **Verify** page with a one-time code field (`OneTimeCode`, drawn and native); **Verify** cancels the autofill context (`SkUiAutofill.Cancel`: the code is never offered for saving) and opens a "Signed in" page; its **Sign out** goes back to the form. The code has a step of its own because a code field in the sign-in form is saved with it as the user name (Samsung Pass, native fields too). Use a throwaway account (`skiaui-test@example.com` with a made-up password), never a real one.

Setup:

- **iOS:** Settings › General › AutoFill & Passwords › AutoFill Passwords and Passkeys on, Passwords on. In the Passwords app, add an entry: website `example.com`, the test user name and password. The demo has no associated domain, so iOS offers saved passwords through the key button in the QuickType bar (pick from all passwords) rather than as a direct suggestion, and does not offer to save or suggest strong passwords: that needs `webcredentials:` associated domains (an app setting, not SkiaUi). To test saving and strong passwords, build with an associated domain you control, or skip those steps.
- **Android:** Settings › Passwords & accounts (or System › Languages & input › Advanced) › Autofill service: Google. Repeat the run with a second service (Bitwarden or 1Password: install it, sign in, set it as the autofill service), since services read the form differently.

Steps (drawn form first, then the native form for comparison):

1. **Offer.** Tap the drawn user name field. iOS: the QuickType bar shows a key (or the saved account); Android: an autofill dropdown under the field or inline suggestions in Gboard's strip (Android 11+), with the saved account once one is saved (before that, the service may offer nothing: do step 3 first).
2. **Fill.** Pick the account. Expected: the user name *and* the password filled (password as bullets), `Text` of both updated, the caret at the end of the focused field, the keyboard still up. iOS fills from one hidden text view: if only the focused field fills, note it (that is a gap to fix, not expected behavior).
3. **Save.** Clear both fields, type the test user name and a new password, tap **Sign in**. Android: the service asks to save (*Save password to Google?*), with the typed user name; accept, sign out, tap the user name field: the account is offered (step 1). iOS (with an associated domain): *Save Password?* after the page changes.
4. **Update.** Sign in again with the same user name and a changed password: Android offers to update the saved password.
5. **Leaving without commit.** Turn **Commit autofill on sign in** off, type a new pair and tap **Sign in**, or go back (the back arrow) instead: Android may offer to save (the fields left the screen) depending on the service; record what the native form does in the same case.
6. **New password.** **Create account** side, tap the new-password field: Android (Google) offers a generated strong password; iOS with an associated domain offers *Use Strong Password*. Picking it fills the field (and on iOS the confirm field if one is marked `NewPassword` too).
7. **One-time code.** On the **Verify** page, focus the code field and have another phone send an SMS to the device: *Your code is 123456*. iOS: *From Messages 123456* in the QuickType bar; Android: Gboard's strip offers the code (Google autofill with SMS permission). Picking it fills the field.
8. **Sensitive data.** With TalkBack or VoiceOver on, the filled password is read as bullets; Android's autofill service never shows the password in its dropdown.
9. **Leak check.** Sign in and out ten times, then run the `TextFieldsEdited` scenario (`scripts/device_tests.sh -S TextFieldsEdited`): no growth.

### Accessibility and keyboard (P10)

Page: **Accessibility** in the demo flyout (`SKUI_DEMO_ROUTE=accessibility` opens it at launch). Its right column (below on phones) shows the semantics tree the platform gets, refreshed every second.

- **Screen readers:** with TalkBack (Android), VoiceOver (iOS / Mac Catalyst) and Narrator (Windows): swipe / move through the form; every control is read with its role, name, state and hint (the check box and switch by their descriptions, the decorative labels beside them skipped), headings are headings, the card is one button "Order 42, Shipped yesterday · tap for details", the Entry is read natively in its place, the Core section too. Double tap (Narrator: Enter / Space on the item) activates: the status line shows the button's `Tag`. Swipe up / down on the slider steps it by 5 %. Move past the third time slot: TalkBack scrolls the drawn list; VoiceOver: three-finger swipe up scrolls it by a page. "Read Continue" moves the screen reader to the Continue button.
- **Keyboard:** "Focus Continue" focuses it (no ring after a click); Tab / Shift+Tab walk all interactive drawn controls of both layers with the ring; Space / Enter activate; arrows step the slider; Tab past "Share order" moves to the next native control (Android, Windows); Page Down over the time slots scrolls them. Text scale: the picker grows all drawn text at once, also on other pages.
- **Keyboard into the surface:** Mac Catalyst needs System Settings › Keyboard › Keyboard navigation on (else Tab only moves between text fields, as on macOS; restart the app after switching it, UIKit reads it at launch); iPad: a hardware keyboard (Full Keyboard Access for everything). Tab from the page's buttons reaches the first drawn control; Tab past "Share order" goes on to the text-scale picker (last on the page: MAUI's Catalyst picker opens its sheet on focus and keeps Tab there). Windows: a click on a drawn button focuses it (no ring); Tab continues from it.
- **Checked on Mac Catalyst (2026-10-03)** through the macOS accessibility API, which is what VoiceOver reads (`AXUIElementCreateApplication(pid)`: roles, descriptions, values, hints; `AXPress`, `AXIncrement`, `AXScrollDownByPage`), and with key events posted to the app (Tab, Shift+Tab, Space, Enter; the ring in a window screenshot). Not yet run: TalkBack, VoiceOver on iOS, Narrator, Android and Windows keyboards.

Android, iOS simulator, and Mac Catalyst diagnostic builds pass for the completed surface. The Windows library also compiles on macOS against the WinAppSDK references (no PRI / MRT tooling): `dotnet build MauiSkiaUi/MauiSkiaUi.csproj -f net10.0-windows10.0.19041.0 -p:TargetFrameworks=net10.0-windows10.0.19041.0 -p:EnableWindowsTargeting=true -p:AppxGeneratePriEnabled=false -p:EnableCoreMrtTooling=false -p:EnableMsixTooling=false`; running it needs a Windows host.

## How to verify device rendering / live-update behavior

This walks through actually proving the things the checklists above only *ask* for — using `MauiContentViewDemoPage`'s Editor→WebView live-update as the running example, since it is the hardest case (two native overlays plus SkUi controls in one grid). The same steps apply to any other checklist item; swap the `AutomationId`s.

### CLI path (no VS Code / DevFlow MCP)

Use [`scripts/device_verify.sh`](../../scripts/device_verify.sh) to select a simulator, emulator, physical device, or Mac Catalyst target, build/launch `MauiSkiaUiDemo` with plain `dotnet build -t:Run` (no VS Code DevFlow injection), optionally capture a screenshot, print the acceptance checklists, and stream logs:

```bash
./scripts/device_verify.sh -l                          # list targets
./scripts/device_verify.sh -p android                   # prompt / pick Android
./scripts/device_verify.sh -p ios "iPhone 16"           # iOS Simulator by name
./scripts/device_verify.sh -p maccatalyst --checklist overlay
./scripts/device_verify.sh --checklist-only --checklist composition
./scripts/device_verify.sh -p android --screenshot      # launch + save PNG under tmp/screenshots
```

Device selection mirrors `runsim.sh`-style UX (numbered list, substring match on name/serial/UDID/AVD). After launch, work through the printed checklist by hand — this path does **not** connect a DevFlow agent. Prefer it whenever VS Code DevFlow is blocked (`MSB4099`, empty agents, missing `adb reverse`, etc.); see step 6 below.

### 1. Get a connected DevFlow agent (VS Code)

1. Select a startup project/device (**.NET MAUI: Select Startup Project**, or the `{ }` status bar item) if none is selected.
2. Launch via the MAUI debug tools, not a plain terminal `dotnet run`: `dotnet_maui_debugProject`. If a session is already active, save your files and use `dotnet_maui_runHotReload` instead — do not rebuild from the terminal while debugging.
3. Confirm an agent is actually reachable — a successful launch/Hot Reload does **not** by itself prove this: call `mcp_maui_maui_list_agents`. If it returns an empty list, do **not** assume the app is broken; see "Known DevFlow agent issues" below before retrying.
4. Once at least one agent is listed, `mcp_maui_maui_wait` (blocks until the agent is ready) then `mcp_maui_maui_capabilities` to see what the connected agent supports.

### 2. Navigate to the page under test

```
mcp_maui_maui_navigate  route: "demo-SkUiMauiContentView"
```

If that fails ("route may not exist"), the app is probably still on the gallery's home route — use `mcp_maui_maui_query` with `automationId: "OpenSkUiMauiContentView"` to get a fresh element id, then `mcp_maui_maui_tap` with that id. Element ids from a previous `maui_tree`/`maui_query` call are not stable across navigations — always re-query right before tapping.

### 3. Prove the Editor and WebView actually rendered as native overlays

```
mcp_maui_maui_tree  depth: 15
```

Look for `Editor` and `WebView` (or their platform types, e.g. `UIKit.UITextView`, `Android.Webkit.WebView`) with **non-zero bounds** positioned inside the `SkUiGrid` preview area, not stacked at `(0,0)` — that would indicate `ComputeRootRelativeFrame()` positioned them incorrectly. Then:

```
mcp_maui_maui_screenshot
```

Visually confirm both overlays are visible, correctly sized, and not overlapping the surrounding `SkUiLabel`/`SkUiButton` text.

### 4. Prove the live HTML update actually works

```
mcp_maui_maui_fill   automationId or elementId of the Editor, text: "<h1 style='color:red'>Changed</h1>"
```

(If `maui_fill` isn't supported for a native `Editor` on the connected agent's platform, use `maui_focus` + `maui_key` to type instead — check `maui_capabilities` first.) Then re-run `mcp_maui_maui_screenshot` and confirm the WebView now shows the red "Changed" heading **without** tapping "Refresh preview" — this proves the `TextChanged` live-update path, not just the manual button. Then tap the "Refresh preview" `SkUiButton` (`mcp_maui_maui_query` for `automationId: "RefreshPreview"`, then `mcp_maui_maui_tap`) and confirm the WebView is unchanged (it was already up to date) — this proves the manual path doesn't regress the automatic one.

### 5. Check contrast/colors for real, not from source

Use `mcp_maui_maui_get_property` on the `SkUiLabel`s and native controls (`TextColor`, `BackgroundColor`) — never infer contrast from XAML/C# color literals, since platform-native controls can override them. Follow up with the screenshot from step 3 if a value looks off.

### 6. If no agent ever connects

Fall back to manual verification via [`scripts/device_verify.sh`](../../scripts/device_verify.sh) (preferred) or an equivalent plain `dotnet build -t:Run` / IDE Play button, then visually check the same things by hand (type in the Editor, watch the WebView, eyeball contrast). Record what you actually observed — do not report a checklist item as verified without either an agent-based check or an explicit manual one.

### Drawn elements (`dev.skiaui` extension)

DevFlow's own tree treats drawn elements differently from native views:
- **SkUi\* views:** it lists them, but with parent-relative frames.
- **Core nodes:** it lists them (they are visual tree elements), without bounds or text.
- **Taps:** `ui tap` fails for both, because neither has a platform view.

The demo registers a Debug-only agent extension ([SkiaUiDevFlowExtension.cs](../../MauiSkiaUiDemo/Diagnostics/SkiaUiDevFlowExtension.cs)) built on `SkUiDiagnostics`:

```bash
maui devflow extensions call dev.skiaui tree                                    # drawn elements: type, automationId, text, window bounds
maui devflow extensions call dev.skiaui tap '{"automationId":"AddObservation"}' # press + release through the drawn tree
maui devflow extensions call dev.skiaui tap '{"type":"SkUiCoreLabel","text":"Cell 1"}'
maui devflow extensions call dev.skiaui hit '{"x":201,"y":709}'                 # deepest drawn element at a window point
```

- **Coordinates:** window DIPs, the same as native elements in `ui tree`.
- **Scope:** all three tools look only at the page on screen (the top modal page, else the Shell's current page). Pages lower in the navigation stack or in other Shell sections keep their surfaces, but they are ignored.
- **Tap result:** `tap` returns both the matched element and the element actually hit. If something covers the element's center, the two differ. An element outside every viewport returns **422** ("not on screen"); **409** is always DevFlow's mutation lease.
- **Lease:** `tap` is a mutation, so it waits for DevFlow's mutation lease. A lease taken by an earlier CLI call (e.g. `ui navigate`) expires after about 10 s.
- **Tests:** in headless tests, use `SkUiDiagnostics.GetRootBounds` / `HitTest` / `SimulateTap` directly.

## Known DevFlow agent issues (as of 2026-09-10)

These affect every device-verification attempt in this repo so far. Check `dotnet_maui_diagnoseDevFlow` first before repeating any step below more than once — it works independently of the MCP connection and reports which of these you're hitting.

1. **Copilot-triggered launch fails to build (`MSB4099`).** The installed VS Code MAUI extension injects `Microsoft.Maui.DevFlow.Agent` via a `MauiDevFlow.targets` file added through `CustomAfterMicrosoftCommonTargets`. That file's top-level `PropertyGroup` condition references an item-list function (`@(PackageReference->WithMetadataValue(...))`), which MSBuild disallows outside a target, producing `MSB4099`. This blocks `dotnet_maui_debugProject`/Copilot-triggered launches entirely; it is **not** caused by anything in this repo, and no extension files have been modified to work around it. Plain `dotnet build`/`dotnet run` and [`scripts/device_verify.sh`](../../scripts/device_verify.sh) (no DevFlow injection) are unaffected — see the CLI path and "no agent ever connects" fallback above.
2. **CLI/agent version mismatch.** `dotnet_maui_diagnoseDevFlow` has reported the extension's bundled CLI (`0.1.0-preview.12.26368.2`) not matching the demo's referenced agent package (now `0.1.0-preview.13.26512.5`, the version of the `maui` CLI tool and of the project's DevFlow skills in `.claude/skills/maui-devflow-*`; pinned in `MauiSkiaUiDemo.csproj` for Debug builds; update the three together: `maui devflow skills check` — see the "enable DevFlow" request in project history; **do not** change this pin as a generic fix for #1). When `mismatchedAgentVersions` is non-empty, expect "agent not responding" style failures even when registration succeeds. Recovery: stop the debug session, `dotnet nuget locals http-cache --clear`, rebuild.
3. **Android needs a manual `adb reverse` tunnel.** `broker_reverse_present: false` in the diagnostics above means the in-app agent cannot reach the host broker. Run the exact command the diagnostics suggest, e.g. `adb -s emulator-5554 reverse tcp:19223 tcp:19223`, then re-check with `dotnet_maui_diagnoseDevFlow`. A missing `agent_forwards` entry additionally means the host can't reach the in-app HTTP agent — that needs `adb forward tcp:<agent-port> tcp:<agent-port>` using the port the agent actually bound (from `appDebugOutput`, e.g. `9223`), not an assumed fixed port.
4. **Agent registers, then becomes unreachable.** Observed on iOS after a successful Hot Reload: `maui_list_agents` briefly showed a connected agent, but `maui_capabilities`/`maui_tree` failed afterward, and a later diagnostics call showed `agentCount: 0`. Treat a one-time successful registration as a snapshot, not a guarantee the agent stays reachable for the rest of the session — re-check with `maui_list_agents` before every device-verification attempt, not just once at the start.
5. **Android bundled DevFlow assemblies sometimes fail to load, but the agent starts anyway.** `appDebugOutput` has shown `open_from_bundles: failed to load bundled assembly Microsoft.Maui.DevFlow.Agent[.Core/.Abstractions].dll` immediately followed by `Agent started on port 9223` / `HTTP server started on port 9223`. The exact effect on functionality is unclear (not reproduced against a reachable agent yet); if Android verification behaves oddly even after fixing #3, check `appDebugOutput` for these lines and treat them as a possible contributing factor, not the settled cause.
6. **Reads work, changes fail with "The DevFlow broker lease authority is temporarily unavailable".** The broker is not running (`maui devflow diagnose` shows `broker_running: false`): `ui tree`, `ui query` and `ui screenshot` reach the agent directly on port 9223, but `ui navigate`, `ui tap` and `ui set-property` need the broker's lease. Start it with `maui devflow broker start` (port 19223); the agent may take a while to re-register (`maui devflow list`), and the first changes can still be refused for a few seconds.

**Bottom line:** exhaust `dotnet_maui_diagnoseDevFlow` and the fixes above once each per issue; do not loop the same MCP call expecting a different result. If still blocked, report the exact diagnostic evidence and fall back to manual verification (step 6 above) rather than claiming device behavior that was never actually observed.

## Goals

1. **Gate correctness before performance work** (NFR-2): simple implementations ship with tests; optimizations must not change observable behavior.
2. Cover **mechanisms**, not only leaf helpers: measure/arrange, paint layers/clip, animator clock, gesture delivery, hosted vs standalone.
3. Prefer tests that run **headless / in-process** on every PR; reserve device UI tests for what only a real surface can prove.
4. Make **visual regressions** detectable without relying on a human staring at the demo every change.
5. Use **AI agents** for triage and judgment of visual diffs — not as the primary CI oracle for “pixels match.”

## Short answers

| Question | Answer |
| --- | --- |
| Can painting / layout / animation be automated tests? | **Yes.** Prefer in-process harnesses that call `Measure` / `Arrange` / `Paint` (and tick animators) against an offscreen `SKSurface`, then assert geometry, events, and/or pixels. |
| Can “does it look right?” be automated? | **Yes, with golden (baseline) images** and a tolerance / SSIM comparator. This is industry standard (Flutter, Avalonia, SkiaSharp, MAUI UITest screenshots, Uno). |
| Should an AI agent be the CI pass/fail for paint? | **No as sole gate.** Use deterministic pixel/SSIM first. Use AI to **explain diffs**, suggest baseline updates vs bugs, and assist gallery review — optionally as a soft signal later. |

## Recommended test pyramid

```text
                    ┌─────────────────────┐
                    │  Device / Appium UI │  rare: overlays, IME, real GL
                    ├─────────────────────┤
                    │  Golden / visual    │  offscreen SKSurface → PNG baselines
                    ├─────────────────────┤
                    │  Mechanism tests    │  layout, paint API, anim clock, gestures
                    ├─────────────────────┤
                    │  Unit tests         │  pure helpers, easing, dirty flags, math
                    └─────────────────────┘
```

| Layer | Runs where | Typical asserts | Speed / flake risk |
| --- | --- | --- | --- |
| **Unit** | `dotnet test` (any host) | values, flags, no UI | Fast / low |
| **Mechanism** | `dotnet test` + SkiaSharp native assets | arranged rects, hit targets, paint call order, animator progress | Fast / low if clock is fakeable |
| **Golden / visual** | Same process, fixed DPI/fonts; ideally one CI OS | PNG vs baseline (± tolerance / SSIM) | Medium; pin environment |
| **Device UI** | Appium / XHarness on emulator or device | navigation, native overlay, screenshot | Slow / higher flake |

**v1 priority:** unit + mechanism + a small golden corpus. Device UI tests after `SkUiMauiContentView` and gallery exist.

---

## 1. Unit tests

Obvious and required. Examples:

- Dirty / invalidation flag transitions; `StartUpdating` / `EndUpdating` coalescing (FR-10).
- Easing and time → progress math (FR-7); no allocations in hot helpers where we claim zero-alloc.
- Clip geometry helpers; DIP ↔ pixel scale math at the root.
- Gesture classifiers in isolation (tap / double-tap / long-press / swipe thresholds) with synthetic pointer streams (FR-15).
- Layout manager edge cases that do not need a full tree (star columns, constraint clamping) when logic is extractable.

**Stack:** xUnit or NUnit + FluentAssertions (or project convention). No MAUI app host required for pure logic.

---

## 2. Mechanism tests (automated — preferred for core)

These exercise the **contracts** of the `ISkUiView` tree without launching the demo app.

### Harness shape (target)

1. Build a small tree in code (`SkUiContentView` / `SkUiLayout` + children).
2. Drive **Measure → Arrange** with known constraints (MAUI `Size` / infinite where needed).
3. For paint: create an **`SKSurface`** (software) at a fixed pixel size and DPI scale; call the root **Paint** with DIP-correct args.
4. For animation: inject a **test clock** (or call `Tick(frameTime)`) so frames are deterministic — do not depend on real vsync in unit CI.
5. For gestures: feed a synthetic pointer stream into the same delivery path the root host uses (FR-15).

This mirrors how Avalonia headless tests and Flutter widget tests work: **full pipeline, fake platform**.

### Layout (FR-3 / FR-3a / FR-13)

| What to assert | How |
| --- | --- |
| Desired size for stacks/grids under fixed/infinite constraints | Compare `Measure` results to expected sizes |
| Child arranged bounds (rows/columns, margins, alignment) | After `Arrange`, read each child’s arranged rect |
| Selective measure/arrange | Change one child; assert siblings are **not** re-entered (spy / counter on overrides) |
| Hosted vs standalone detection | Child under Skia parent has no handler; standalone creates handler (mock or flag) |

**Automation:** fully automated; no pixels required. Prefer this over goldens for layout bugs.

### Paint / layers / clip (FR-8 / FR-9 / FR-11)

| What to assert | How |
| --- | --- |
| Layer order (Background → Content → Overlay) | Test doubles that record paint order; or sample known pixels |
| Clip: corners transparent outside round-rect | Read pixels outside clip; expect clear / background |
| Transparency compositing | Overlapping translucent fills → expected blended color (± tolerance) |
| Selective re-record | Change one node → `RecordedPictures` grows by one; offset/transform changes record nothing |

**Automation:** yes. Pixel reads from `SKBitmap`/`SKPixmap` are deterministic on CPU Skia when fonts and sizes are fixed.

### Animation (FR-7)

| What to assert | How |
| --- | --- |
| Progress at t = 0, mid, end | Fake clock; assert property values after tick |
| Render-transform anim does **not** dirty measure | Counters on Measure/Arrange stay 0 across ticks |
| Layout anim **does** dirty measure (when implemented) | Counters increment |
| Render loop on only while animators active | `SkUiCompositor.NeedsFrame` via `SkUiTestSurface` |
| Idle after last animator completes | Registry empty → loop off |

**Automation:** yes, if the clock is injectable. Avoid “sleep 16 ms and hope” in CI.

Optional later: golden **keyframes** (t = 0%, 50%, 100%) as PNGs for a few gallery scenes.

### Gestures / hit-test (FR-15 / FR-11)

| What to assert | How |
| --- | --- |
| Hit target is arranged bounds (even if painted round) | Point in “empty” corner still hits |
| Passive label does not consume until opted in | Event/command not raised; sibling underneath receives |
| Active button handles tap without app handler | Intrinsic handler fires |
| `InputTransparent` pass-through | Hits fall through |
| Gesture sequences | Synthetic down/move/up timelines |

**Automation:** yes; pure event assertions, occasional pixel check for press visuals.

### `SkUiMauiContentView` (FR-16)

Harder to fully automate in-process (needs real handlers / platform views).

| Approach | Role |
| --- | --- |
| Mechanism stubs | Measure/arrange placeholder bounds without live Entry |
| Device / Appium | Overlay position, focus, IME, WebView — after product exists |

Treat overlay hosting as a **thin** device suite, not the bulk of CI.

---

## 3. Golden / visual regression tests (automated)

**Golden test:** render a known scene → PNG → compare to a checked-in baseline. Fail if difference exceeds threshold; attach actual / expected / diff artifacts.

### Why this fits SkiaUi

SkiaUi’s product surface **is** pixels on an `SKCanvas`. Asserting only sizes misses regressions in fill, stroke, clip, layering, and text. Offscreen `SKSurface` goldens give **Appium-free** visual coverage of the drawn tree.

### Practical rules (from peer frameworks)

- **Pin the environment:** one CI OS (e.g. Linux) generates and verifies goldens; local macOS failures may be font/AA drift, not product bugs.
- **Fix DPI, size, theme, fonts** in the harness (embed a test font for text goldens).
- **Exact match** for simple geometry; **tolerance or SSIM** for AA / text (Avalonia uses an allowed error; Verify.Avalonia can use SSIM; SkiaSharp goldens use per-renderer tolerance).
- **Update workflow:** intentional UI change → regenerate baselines → commit with the PR (Flutter `--update-goldens`; MAUI snapshot folders; SkiaSharp harvest scripts).
- **Exclude inherently non-deterministic scenes** (live clocks, random, continuous animation) or freeze time / pause animators before capture.

### Suggested corpus (start small)

- Solid / rounded button Background + Content + Overlay.
- Translucent overlapping labels (FR-8).
- Clipped round rect with content that would paint outside (FR-11).
- Simple `SkUiGrid` / stack arrangement with colored children (layout → pixels).
- One animation keyframe set (optional).

Store under e.g. `tests/MauiSkiaUi.Tests/Goldens/` with platform or renderer subfolders only if SW vs GL diverge.

---

## 4. Device / UI automation (selective)

Use when in-process tests cannot:

- Real **GL** present path vs software surface differences.
- **`SkUiMauiContentView`** overlay sync (Entry / Editor / WebView).
- End-to-end gallery smoke on Android / iOS / Mac Catalyst.

**.NET MAUI** pattern: Appium + shared NUnit tests + `VerifyScreenshot()` against per-platform snapshot folders ([MAUI UITests](https://github.com/dotnet/maui/wiki/UITests), [docs](https://learn.microsoft.com/dotnet/maui/deployment/ui-testing)).

Keep this layer **small**; Skia tree correctness should mostly be proven offscreen.

---

## 5. How other frameworks implement this

| Framework | Mechanism / headless | Visual / golden | Notes for SkiaUi |
| --- | --- | --- | --- |
| **Flutter** | Widget tests: `pumpWidget`, fake async, `pump` / `pumpAndSettle` | `matchesGoldenFile` — pixel compare + `--update-goldens`; CI on one OS; custom comparator for tolerance | Closest mental model for **tree + paint** tests without a device |
| **Avalonia** | `Avalonia.Headless` — layout/input without a real window | Enable Skia (`UseHeadlessDrawing = false`), `CaptureRenderedFrame`, compare to `.expected.png`; `Avalonia.RenderTests`; Verify.Avalonia + SSIM | Strong model for **headless Skia** + threshold |
| **SkiaSharp** | In-process scenes across backends | Golden PNG matrix per renderer; tolerance; harvest unseeded goldens from CI | Same graphics stack we paint with — reuse comparison ideas |
| **.NET MAUI** | Unit tests for non-UI; limited in-proc UI | Appium UITests + `VerifyScreenshot` baselines per platform | Use for **host app / overlays**, not primary Skia tree tests |
| **Uno Platform** | RuntimeTests in-app; Uno.UITest out-of-proc | `TakeScreenshot` + `ImageAssert`; CI sample snapshot comparer (XOR diffs) | Gallery screenshot matrix is a good **semi-automated** pattern |
| **DrawnUi** | Demo / manual gallery heavy | No first-class public golden harness analogous to Flutter/Avalonia | SkiaUi should **not** rely on demo-only verification |

**Takeaway:** frameworks that own a retained or immediate Skia/compositor tree almost always add **offscreen render + baseline images**. Frameworks that own platform views lean on **Appium screenshots**. SkiaUi should do **both**, with weight on the former.

---

## 6. AI agents and visual comparison

### What automation already does well

- Exact or SSIM pixel compare is **fast, cheap, deterministic**, and CI-friendly.
- Diff PNGs (actual / expected / mask) are enough for most regressions.

### Where AI helps

| Use | Automated? | Role |
| --- | --- | --- |
| Summarize a failing golden (“label shifted 4px; corner AA changed”) | Semi | Agent reads diff artifacts + optional baseline/actual images (vision) |
| Classify **bug vs intentional** after a known API change | Semi | Agent proposes approve-baseline vs fix-code; human or policy confirms |
| Compare paint to a **design brief** (“rounded 8dp primary button”) without a PNG baseline | Experimental | Weak oracle alone; useful for scaffolding new goldens |
| Walk the gallery and flag “looks broken” | Semi / nightly | Complements, does not replace, golden CI |
| Write new mechanism tests from FR checklists | Dev aid | Agent authors tests; CI still runs deterministic asserts |

Commercial **Visual AI** (e.g. Applitools Eyes) and agent-oriented tools (e.g. RegressionBot-style APIs) encode “ignore AA noise / judge layout intent.” For a library like SkiaUi, start with **local SSIM/tolerance**; add AI triage when golden volume or noise becomes painful.

### Recommended policy

1. **CI gate:** deterministic golden + mechanism tests only.
2. **PR assist:** agent (or human) reviews attached diffs when goldens fail; may suggest updating baselines when the PR description claims intentional visual change.
3. **Do not** block merge solely on an LLM “looks good” without a baseline or geometric assert.
4. Optional later: agent job that opens a draft comment with a structured diff summary (region, severity, likely cause).

---

## 7. Proposed solution structure (when we add tests)

| Project | Contents |
| --- | --- |
| `MauiSkiaUi.Tests` | Unit + mechanism + golden tests; references `MauiSkiaUi` + SkiaSharp native assets for the CI RID |
| `MauiSkiaUiDemo.UITests` (later) | Appium shared tests; sparse screenshots for host/overlay |

Shared helpers (suggested):

- `TestSurface` — create SW `SKSurface`, run measure/arrange/paint, export PNG / sample pixels.
- `FakeFrameClock` — advance animation time without vsync.
- `GoldenAssert` — compare bitmaps with configurable max ΔE / SSIM; write `*.actual.png` / `*.diff.png` on failure.
- Pointer stream builder for FR-15.

Document how to regenerate goldens (e.g. `dotnet test --filter Golden -- UpdateGoldens=true` or an env var), matching Flutter/Avalonia ergonomics.

---

## 8. Adoption roadmap

| Step | Deliverable |
| --- | --- |
| **1** | Test project; unit tests for any pure logic as it lands; correctness-before-optimize discipline (NFR-2) |
| **2** | Mechanism harness: Measure/Arrange/Paint on SW surface; layout + dirty-tracking + gesture tests |
| **3** | Golden corpus (~5–15 scenes); CI on one pinned OS; diff artifacts on failure |
| **4** | Animation tests with fake clock + optional keyframe goldens |
| **5** | Thin Appium suite for `SkUiMauiContentView` + gallery smoke |
| **6** | Optional AI triage on golden failures / nightly gallery review |

---

## 9. Open decisions

- [ ] Test framework: xUnit vs NUnit (MAUI UITest samples often use NUnit).
- [ ] Golden compare: exact bytes vs ImageSharp Δ vs SSIM (lean SSIM/tolerance once text enters corpus).
- [ ] Whether SW goldens alone are enough for v1, or we also need a Mesa/GL lane (SkiaSharp-style) later.
- [ ] Where baselines live (repo vs generated-only on CI with promotion script).
- [ ] How aggressively to automate AI review (comment-only vs future soft check).

---

## References (external)

- Flutter: [`matchesGoldenFile`](https://api.flutter.dev/flutter/flutter_test/matchesGoldenFile.html), golden comparator docs
- Avalonia: [Headless platform / visual regression](https://docs.avaloniaui.net/docs/testing/setting-up-the-headless-platform), `Avalonia.RenderTests`
- SkiaSharp: golden-image harness / visual tests in-tree (`documentation/dev/golden-image-tests.md`)
- .NET MAUI: [UI testing](https://learn.microsoft.com/dotnet/maui/deployment/ui-testing), [UITests wiki](https://github.com/dotnet/maui/wiki/UITests) (`VerifyScreenshot`)
- Uno: [Creating automated UI tests](https://platform.uno/docs/articles/uno-development/creating-ui-tests.html), snapshot comparer in CI
- Snapshot helpers: e.g. Meziantou SkiaSharp snapshot testing (PNG + optional SSIM)
- Visual AI / agent triage: Applitools Eyes; agent-oriented regression APIs (evaluate if golden noise warrants it)

Local design refs: [Requirements.md](Requirements.md), [LayoutSystem.md](LayoutSystem.md), [DrawingMechanism.md](DrawingMechanism.md), [AnimationMechanism.md](AnimationMechanism.md), [EventMechanism.md](EventMechanism.md).
