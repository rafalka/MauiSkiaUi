# SkiaUi 1.19 implementation plan: value inputs, colors that follow the system, lists and the sheet finished

**Goal:** the backlog items picked for 1.19 on 28 November 2026, everything suggested for it except the charts package (F4, still on the [backlog](Roadmap.md#new-controls-and-packages)):

- **New controls** (review [F6 and F7](ImplementationPlan-PerformanceAndControls.md#track-f-features)): range slider and rating; autocomplete; the pickers' tokens drawn as chips.
- **Parity and guards:** the MAUI 11 members still missing; validation of every enum property; a test that keeps the Core layer to [FR-C1](Requirements.md#core-layer-fr-c).
- **Colors:**
  - Scheme defaults resolved when painting, on both layers, so trees made before a dark-mode switch follow it.
  - Opt-in tracking of the OS theme; the platform accent and Android dynamic color; color schemes per tree.
- **Lists:** the virtual stack's public horizontal layout, prefetch in DIPs, item sizes from the app, load-more at the engine, `VirtualScrollMode`, and a Core twin.
- **The sheet finished:** a platform pane title, overlay masks so drawn popups can cover hosted native views, and a Core sheet.
- **Verification:** a device pass over what 1.16 to 1.18 left open, before the new features pile on.

**Status:** proposed 28 November 2026. The decisions below are to be agreed before their phase starts. Current state of each area: surveyed in the code on 28 November 2026 (sizes and touch points under each phase).

## Decisions

- **D1 1.19 scope and cut lines.**
  - **Scope:** the phases below.
  - **Size:** a larger release than 1.17 or 1.18, so three parts carry a cut line and move to 1.20 if earlier phases run long:
    - L4, `VirtualScrollMode` (`Loop` with items of any size is new work);
    - L5, the Core virtual stack;
    - S3, the Core sheet.
  - **Ships together:** the rest.
  - **Release:** `1.19.0`.
- **D2 Range slider and rating follow the F6 brief.**
  - **Rating shape:** `SkUiRating.Shape` is MAUI's `Geometry?` and `SkUiCoreRating.Shape` a `PathF?`: the layers' convention, as `SkUiPath.Data` / `SkUiCorePath.Data` are.
  - **Precision:** `Exact` reads a fractional value from the point, and is drawn as a clipped partial fill.
- **D3 Autocomplete derives from the entry.**
  - **Hierarchy:** `SkUiAutoComplete : SkUiEntry` and `SkUiCoreAutoComplete : SkUiCoreEntry`. Text, placeholder, fonts, field chrome, keyboard, IME and autofill come with them. `SkUiTextFieldModel` gains no members unless step U1 finds it must.
  - **Suggestions:** shown through the popup presenters (anchored by default; the sheet presenter on phones when the app sets it), as F7 says.
  - **Core twin:** in this release, not a later step. Since 1.18 the Core side reuses the same presenters through the host, so it costs little.
  - **Brief otherwise unchanged:** filter modes, provider with debounce and cancellation, `HighlightsMatch` through spans, `AllowsFreeText`.
- **D4 The pickers' tokens are drawn as input chips.**
  - **Drawing:** the shared picker model keeps its token layout ("+N", Backspace removal, hit boxes) and draws each token with `SkUiLook.DrawChip` / `MeasureChip` (`SkUiChipKind.Input`), so tokens and chips look the same in every look.
  - **Compatibility:** `PickerTokenPainter`, `DrawPickerTokenCore` and `SkUiPickerTokenPaint` keep working: a look that overrides them still draws its tokens. They are marked obsolete for 2.0.
  - **Token template (cut line inside phase T):** a `TokenTemplate` on `SkUiPicker`, hosted the way `SelectedItemTemplate` hosts views (SkUi\* only, like that template).
- **D5 MAUI 11 members.** Each item ships on both target frameworks where SkiaUi owns the API, and is net11-only where it bridges a MAUI 11 type:
  - **`SkUiBox.Fill`** (`Brush?`) and **`SkUiCoreBox.Fill`** (`Paint?`), on both frameworks. `Fill` wins over `Color` and `Background`, as in MAUI 11.
  - **`ReturnCommand` and `ReturnCommandParameter` on `SkUiEditor` and `SkUiCoreEditor`**, both frameworks, copying `SkUiEntry`'s. When it runs follows MAUI 11's `Editor`; step A1 reads MAUI's source first, since Return in an editor inserts a line.
  - **MAUI's `LongPressGestureRecognizer` on drawn views,** net11 only (`#if NET11_0_OR_GREATER` in `SkUiMauiGestures`, the library's first net11 condition):
    - Mapped onto `SkUiLongPressGestureRecognizer`: `MinimumPressDuration`, `AllowableMovement`, `NumberOfTouchesRequired` 1.
    - Raises `LongPressing` statuses and `LongPressed`. If MAUI keeps `SendLongPressed` internal, it is reached through `[UnsafeAccessor]`, as the pointer events already are.
  - **`CancellationToken` overloads** of SkiaUi's own `AnimateAsync` (both layers) and `SkUiViewAnimation.RunAsync`. MAUI's `FadeToAsync(…, token)` and the other extensions already work on drawn views (they set bindable properties), so parity here means SkiaUi's render-thread animations.
  - **`InvalidateStyle` / `InvalidateVisualStates`:** inherited already; tests that state copied into fields follows a style taken off and put back.
  - **Parity test:** the `Maui11Backlog` entries of `MauiApiParityTests` go away.
- **D6 Every enum property validates.**
  - **Helpers:** generic `SkUiValidate.Defined<TEnum>` and `SkUiValidate.Flags<TEnum>` (a mask of the defined flags).
  - **Scope:** the 46 enum bindable properties without a `validateValue` get one. Their fluent `SetX` on both layers throws on undefined values; the Core setters do the same.
  - **Not breaking:** an undefined enum value is a bug in the caller, so this is a behaviour change under `### Other`. MAUI's own controls take such values silently; ours now ignore them with a warning, as for every other invalid value.
  - **Guard:** the twin parity test already probes undefined values alike on both layers. A new reflection test fails for any enum bindable property without validation.
- **D7 The FR-C1 test.** It goes over the public types of `MauiSkiaUi.Core` and checks two things:
  - Nothing but `SkUiCoreHost` (the bridge) derives from `BindableObject` or `Element`.
  - Every `Microsoft.Maui.Controls` type in a public or protected signature is on an allow-list with a reason.
    - **Values, enums and event args MAUI defines:** `FontAttributes`, `DataTemplate`, `StackOrientation`, `LayoutAlignment`, `SelectionMode`, the scroll and snap enums, `PenLineCap`, `FillRule`, `ValueChangedEventArgs`, … (allowed by FR-C1).
    - **Classes that stretch FR-C1:** `ImageSource` (segment and tab icons), `Brush` (`SkUiCorePicker.SelectionBackground`), `Style` (`CalendarStyle` on the Core date pickers) and `Button.ButtonContentLayout`; the test found two more SkUi\* types that derive from Controls types: `SkUiPickerColumn` (`SkUiCoreMultiColumnPicker`'s columns, an `Element`) and `SkUiScrollView` (an `SkUiCoreScrollBar` constructor). They are allowed in 1.x; replacements go to [Held for 2.0](Roadmap.md#held-for-20), as changing them breaks callers.
- **D8 Scheme defaults are resolved when painting, on both layers.**
  - **What changes:** a color whose default is a scheme token (21 copies in 13 Core files; 17 field initializers, 18 `defaultValueCreator`s and 3 `Default*Color` overrides on the SkUi\* layer) is stored as "not set" and read through `?? SkUiColors.X` where it is used. The refresh indicator and the scroll bar already do this.
  - **What callers see:**
    - **Getters:** they keep their non-null types and return the current scheme's color while the property is not set.
    - **`GetValue` on an unset SkUi\* color:** it now reports `null` instead of a snapshot. This is noted under `### Other`; the migration and color-scheme docs say so.
  - **Repaint triggers:**
    - A change to `SkUiColorScheme.Current` already invalidates every surface. The scheme's instance `Changed` event (a token changed in place) now does the same.
    - `ColorScheme.md`'s "snapshot at creation" note is replaced.
- **D9 Colors from the system** (built on D8; each part is opt-in):
  - **Following the OS theme:** `SkUiColorScheme.FollowSystemTheme = true` sets `Current` from the current look's `GetColorScheme(isDark)` whenever MAUI's `RequestedThemeChanged` fires, and once when set. Default `false`: today's apps decide themselves, and `ColorScheme.md`'s non-goal becomes this opt-in.
  - **Platform accent:** `SkUiPlatformColors.Accent` (nullable) reads Android 12+'s dynamic `system_accent1_*`, the Windows accent from `UISettings` and macOS' `controlAccentColor`; iOS has no system accent. It raises `Changed` when the system's changes. `SkUiColorScheme.UseSystemAccent = true` feeds it into `WithAccent` for the followed scheme. Platform schemes keep their own accents otherwise.
  - **Per-tree schemes (cut line inside phase C):** `SkUiView.ColorScheme` (bindable, inherited down the drawn tree) and `SkUiCoreNode.SetColorScheme`. They are scoped while painting with the machinery looks use per tree (`SkUiLookScope`, a thread-static scoped value, tree versions). `SkUiColors.X` reads the scoped scheme.
- **D10 The virtual stack.**
  - **Horizontal:** a public `SkUiVirtualHorizontalStackLayout` (MAUI's naming), and `Orientation` on `SkUiVirtualScrollView`. The engine already lays out horizontally for the collection view (`IsHorizontal`, internal). The vertical base class keeps its name; a rename waits for 2.0.
  - **`PrefetchDistance` (DIPs):** used instead of `PrefetchFactor` when set (`-1` default: the factor).
  - **`QueryItemSize`:** a `Func<int, object?, double>` returning a size along the stack, or `NaN` to measure. It feeds `SkUiVirtualItemSizes`, so variable-size lists scroll to an index exactly.
  - **Load-more:** the collection view's load-more (mode, position, command, placeholder template, `IsLoadMoreActive`) moves into the engine, so the virtual stack and the virtual scroll view get it. The collection view's API is unchanged.
  - **`VirtualScrollMode` (cut line):**
    - `Normal` (default).
    - `InfiniteFeed`: load-more at the end automatically, and items far behind are released and re-estimated, so memory stays flat.
    - `Loop`: the list wraps around. The carousel's copies-and-recentre approach, generalized to items of any size.
  - **Core twin (cut line):** the realization, prefetch and size logic moves into a view-agnostic `SkUiVirtualStackEngine`, as the carousel's did. `SkUiCoreVirtualStackLayout` runs on it inside `SkUiCoreScrollView`, with an item factory and recycling keys. The SkUi\* stack keeps its behaviour on the same engine, and its 1,198-line test class guards the move. `TwinParityTests`' "unpaired" list loses the stack.
- **D11 The sheet finished.**
  - **Pane title:**
    - **Semantics:** the sheet's panel becomes a container semantics node with a pane title (its `Title`) and a modal flag.
    - **Android:** `paneTitle` and a pane-changed event.
    - **Apple:** an `AccessibilityViewIsModal` container and `accessibilityPerformEscape` (VoiceOver's escape gesture closes the topmost sheet).
    - **Windows:** a dialog landmark on the peer.
  - **Overlay masks, first form:**
    - **Freezing:** while a drawn overlay (a sheet, a dialog, a popup on the surface overlay or a modal pane) covers a hosted native view, the native view is frozen. It is captured, hidden and drawn as its snapshot under the overlay, with the snapshot machinery scrolling and animating ancestors already use. It comes back live when nothing covers it.
    - **Input:** a covered native view does not take input; a focused native field that a sheet covers loses focus. True masks (non-rectangular native clips per platform) stay on the backlog.
  - **Core sheet (cut line):** `SkUiCoreSheet`, `ShowAsync`-first.
    - **Hosting:** it opens on the anchor's surface overlay inside an internal `SkUiCoreHost`, so the overlay layer needs no change. Its parts are Core nodes (a `SkUiCoreScrollView` track, panel, scrim, grabber) over the same `SkUiSheetEngine`.
    - **Back handling:** `SkUiSheetBackHandler` takes an interface instead of `SkUiSheet`.
    - **Not in 1.19:** Core sheets placed in a Core tree (hoisting).
- **D12 A device pass first.**
  - **Hardware:** the open 1.16–1.18 checks on the [verification backlog](Roadmap.md#verification-backlog), run with DevFlow's dev.skiaui extension on the Galaxy S9, the iPhone 8, Mac Catalyst and Windows.
  - **What is checked:** the sheet's drag and Back; live regions, chips, tab headers and segments with TalkBack, VoiceOver and Narrator; press scale and hit slop under a finger; scroll-linked recipes; font features.
  - **Timing:** it runs as phase 0, alongside phase A. Bugs it finds are fixed first, each with a headless repro where one is possible. Checks that pass leave the backlog.

## Phase 0: device pass (D12, M)

- **01** Android (Galaxy S9): the sheet (drag, fling, list hand-off, Back, keyboard), chips and tab headers by touch, press scale and hit slop, live regions with TalkBack, scroll-linked recipes.
- **02** iPhone 8: the same with VoiceOver.
- **03** Mac Catalyst and Windows: keyboards (Ctrl+Tab, arrows in chip groups and segments), Narrator on the 1.18 controls.
- **04** Fixes, and the backlog updated: each passed item leaves it, with the date and device.

## Phase A: parity and guards (parallel, in worktrees)

- **A1 MAUI 11 members (D5, M).**
  - `Fill` on both boxes.
  - Editor `ReturnCommand` on both layers.
  - The long-press bridge (net11), with tests in `MauiGestureRecognizerTests` (a press held past `MinimumPressDuration`, moved past `AllowableMovement`, `LongPressing` statuses).
  - Token overloads of `AnimateAsync` / `RunAsync`, and the `InvalidateStyle` tests.
  - *Check:* `MauiApiParityTests` without `Maui11Backlog` entries; both frameworks build; the package validation baseline holds.
- **A2 Enum validation (D6, S–M).**
  - `Defined<TEnum>` / `Flags<TEnum>`; the 46 properties; the setters.
  - The reflection guard; `ImplicitStyleTests` still pass (styles set only defined values).
- **A3 FR-C1 test (D7, S).** `CoreLayerTests` (or a new `CoreLayerContractTests`), with the allow-list and reasons; Requirements.md's FR-C1 status loses "a test that guards it is open".

## Phase C: colors (D8, D9)

- **C1 Paint-time defaults (M):**
  - Both layers, control by control, in one PR so the twins stay at parity.
  - The scheme's instance `Changed` invalidates surfaces.
  - *Check:*
    - A Core tree and an SkUi\* tree made before `SkUiColorScheme.Current` changes draw the new scheme's pixels after it, and keep set colors.
    - `TwinParityTests` (defaults read the same on both layers).
    - Benches `core-labels`, `skui-labels`, `skui-chips` within noise (a `??` per paint).
- **C2 Follow the OS theme (S):** `FollowSystemTheme`, through `Application.RequestedThemeChanged`; a headless test with a fake theme source.
- **C3 Platform accent (S–M):** `SkUiPlatformColors.Accent` on Android, Windows and Mac Catalyst (listeners for the system's changes), `UseSystemAccent`. Headless tests cover the plumbing; the colors themselves are a device check.
- **C4 Per-tree schemes (M, cut line):** `ColorScheme` on views and nodes, scoped like looks; a gallery page with a light card in a dark page.

## Phase R: range slider and rating (F6, D2, S each)

The brief's seven steps for each control: page, math in `Helpers` with tests, look members, SkUi\* control, Core twin with pixel comparison, keys and semantics, registrations, leak scenario, gallery page, docs. *Check:* `RangeSliderTests`, `RatingTests` (thumb picking, `MinimumRange`, steps, right to left, each precision); `TwinParityTests` picks the pairs up.

## Phase U: autocomplete (F7, D3, M)

- **U1** Control page; check that deriving from the entry needs nothing from `SkUiTextFieldModel`.
- **U2** `SkUiAutoCompleteModel` (filtering, debounce, cancellation, highlighted ranges) with a fake clock.
- **U3** The suggestion list as `SkUiPickerPopupContent`.
- **U4** `SkUiAutoComplete`.
- **U5** Keys (Down, Enter, Escape) and semantics (the list's count announced).
- **U6** `SkUiCoreAutoComplete`.
- **U7** Leak scenario, gallery page with a slow fake provider, docs and migration notes (how vendor and toolkit autocompletes map onto it).

## Phase T: picker tokens as chips (D4, M)

- **T1** The model draws and measures tokens with the chip painter (Input kind, the remove ×, pressed state, right to left, "+N" as a chip without ×). The token painter members keep working and are marked obsolete.
- **T2** `TokenTemplate` on `SkUiPicker` (cut line inside the phase).
- *Check:* the picker token tests (layout, Backspace, removal hit boxes) and pixel goldens per look.

## Phase L: the virtual stack (D10)

- **L1** `SkUiVirtualHorizontalStackLayout`, `SkUiVirtualScrollView.Orientation`, docs (S).
- **L2** `PrefetchDistance`, `QueryItemSize` (S–M).
- **L3** Load-more in the engine, shared with the collection view (M).
- **L4** `VirtualScrollMode` `InfiniteFeed` and `Loop` (L, cut line).
- **L5** `SkUiVirtualStackEngine` and `SkUiCoreVirtualStackLayout` (L, cut line).
- *Check:*
  - `VirtualVerticalStackLayoutTests` unchanged and green through L5's move, plus a horizontal class.
  - Benches `virtual-fling`, `virtual-stack-fling` and `collection-*` within noise.
  - Leak scenarios for the horizontal stack and the Core twin.
  - Frame times of a 10,000-item horizontal list on the S9.

## Phase S: the sheet finished (D11)

- **S1 Pane title (S–M):** the semantics node, the three bridges, the escape gesture. *Check:* headless semantics tests (pane node, modal flag, title); screen readers on the device pass list.
- **S2 Overlay masks, first form (M):** freeze covered natives while an overlay shows. *Check:*
  - Headless tests with fake natives (covered then uncovered, focus leaves a covered field).
  - Device checks: a WebView and a map under a sheet on Android, iOS and Windows.
- **S3 Core sheet (M+, cut line):** `SkUiCoreSheet` with `ShowAsync`, Core parts, the back handler interface; leak scenario `CoreSheetOpenedAndClosed`; gallery page.

## Docs, demo and release

Each phase updates:

- **Its control pages:** new pages for the range slider, rating, autocomplete and horizontal stack.
- **Design docs:** [ColorScheme.md](ColorScheme.md) (D8, D9), [ScrollingAndCollectionViews.md](ScrollingAndCollectionViews.md) (its "Not yet" line), [Accessibility.md](Accessibility.md) (pane title leaves "not done"), [Popups.md](Popups.md) (masks), [Requirements.md](Requirements.md) (FR-C1).
- **Release notes:** the changelog under `## Unreleased`.
- **Migration:** [Migration.md](../Migration.md) and the migration skills (`LongPressGestureRecognizer`, `BoxView.Fill`, `Editor.ReturnCommand`, the toolkit and vendor range sliders, ratings and autocompletes).
- **Demo:** gallery pages next to the MAUI controls.
- **Status pages:** [Roadmap.md](Roadmap.md).

## Order and sizes

| Order | Work | Size | Parallel |
| --- | --- | --- | --- |
| 1 | Phase 0 (device pass), A1, A2, A3 | M, M, S–M, S | Yes: the device pass by hand, A in worktrees |
| 2 | C1, R, L1–L2 | M, S + S, S–M | Yes: they share only the look and the gallery |
| 3 | C2, C3, U, T1, S1, L3 | S, S–M, M, M, S–M, M | Yes, except U and T1 (both in the picker popup code: one after the other) |
| 4 | S2, C4, T2 | M, M, M | Yes |
| 5 | L4, L5, S3 | L, L, M+ | Cut line: what is not done by the release goes to 1.20 |

`SkUiVirtualVerticalStackLayoutBase.cs`, `SkUiTextFieldModel.cs` and `SkUiViewHandler.cs` are large coordinating classes: never two running packages on the same one (L3 / L4 / L5 one after the other; S2 alone in the handler).

## Not in 1.19

- The charts package (F4), Lottie (F8), the data grid (F9, waits for L1 and the virtual stack's Core twin), backdrop blur (F10), full SVG.
- True overlay masks (non-rectangular native clips); Core sheets placed in a Core tree.
- A source generator for the twins: declined after 1.18 ([D3](ImplementationPlan-PerformanceAndControls.md#decisions) of the review plan).
- Renaming the vertical virtual stack's base class, and replacing `ImageSource`, `Brush`, `Style` and `ButtonContentLayout` in the Core API: 2.0.

## Verification

- **Tests and build:** headless suite green on net10.0 and net11.0. The library at 0 warnings (trim and AOT analyzers included), with the net11-only code (A1) built for both lines.
- **Benches:** within noise where a phase touches a hot path (paint-time color defaults, the virtual stack, chip-drawn tokens).
- **Device checks:** phase 0 first. The new items (accent colors, masks over WebView and maps, screen readers on the pane title and the new controls) go on the verification backlog until run.
