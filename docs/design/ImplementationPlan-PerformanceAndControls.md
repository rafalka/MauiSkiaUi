# SkiaUi implementation plan: allocation pass and new controls

**Status:** from a review of the architecture, the implementation and the .NET 10 / 11 and C# 14 / 15 feature use (16 November 2026). The findings are agreed and on the [roadmap](Roadmap.md): track A is release 1.13, planned step by step in [ImplementationPlan-1.13.md](ImplementationPlan-1.13.md) (which wins where the two differ), and track F is on the backlog. [D1](#decisions) is agreed; the other decisions are still proposed, each to be agreed before its package starts.

**Goal:**

- **Track A (performance):** remove the allocations that remain on input events, on frames that re-record and on first frames of large trees, and settle how the two .NET lines use language and runtime features.
- **Track F (features):** ten additions other UI toolkits have and SkiaUi lacks, none of them on the backlog or the "not planned" list.

**Baseline:** 1.12.0 (net10 with MAUI 10.0.130 and net11 with MAUI 11.0.10, SDK 11.0.100, SkiaSharp 4.154.0).

**How to use this plan.** Each work package below is a self-contained brief for one agent. Give the agent the "Rules for every package" section and its package, nothing else. Line numbers are from `devel` at 1.12.0 and drift: an agent finds the place by the named symbol.

## Rules for every package

- Read [CLAUDE.md](../../CLAUDE.md) first and follow its token hygiene: never read `artifacts/`, `CHANGELOG.md` or the large design docs whole (`grep -n` for the section, then `sed -n 'a,bp'`).
- 1.x keeps the 1.0 API. Everything is additive; the API comparison against the newest 1.x release must report additions only.
- Shared source is C# 14. No C# 15 syntax (`with(...)` in collection expressions, `union`, `closed`, labeled `break`), because SDK 10 builds the net10 line by itself.
- Both layers where a control has both (`SkUiX` and `SkUiCoreX`) on one shared engine in `MauiSkiaUi/Helpers`. The engine holds the state machine and the geometry; the two controls hold properties only. Say so in the PR when a package ships one layer, and why.
- SkUi\* properties follow the property pattern: `validateValue` from `SkUiValidate`, a fluent `SetX`, a change callback that copies into a field, `WriteBack` for values the control writes itself. Change callbacks may run before the constructor body.
- Chrome is drawn by the look from a paint struct passed with `in`. A control in the core package adds look members to `SkUiLook` with defaults in `DefaultSkUiLook`; a control in an optional package uses a style object over `SkUiColorScheme`, as `SkUiMarkdownStyle` does ([D4](#decisions)).
- Motion honors `SkUiMotion` (reduce motion). What moves every frame is a composite-time property or an `SkUiRenderDrawable`, never a re-recorded picture.
- Accessibility is part of "done": a semantics role, a name, actions, keyboard focus and keys ([Accessibility.md](Accessibility.md): grep for the role you need).
- Tests are headless xUnit (`tests/MauiSkiaUi.Tests`). Text tests register the bundled font; XAML-loading classes join `RuntimeXamlCollection`. A new drawn control is added to `ImplicitStyleTests` and gets a leak scenario in `tests/Shared/MemoryLeaks`.
- Performance: a feature a view does not use costs nothing per frame. Run `scripts/bench.sh --baseline HEAD` (and `--quiet-gc` when one phase jumps). New bench scenarios set new options through `TrySet` / `TrySetOption`.
- The build stays at 0 warnings on `net10.0` and `net11.0`: `dotnet build MauiSkiaUi/MauiSkiaUi.csproj -f net10.0 -v q -clp:ErrorsOnly`, then the same for `net11.0`, then `dotnet test tests/MauiSkiaUi.Tests/MauiSkiaUi.Tests.csproj --logger "console;verbosity=minimal"`.
- When a feature ships: an entry under `## Unreleased` in the changelog, a page in `docs/controls/`, a gallery page in `MauiSkiaUiDemo/ComponentPages` (next to the native control where MAUI has one), the status in [Roadmap.md](Roadmap.md) and [Requirements.md](Requirements.md), and [Migration.md](../Migration.md) plus `plugins/skiaui-migration` when a MAUI or vendor conversion path changes.
- An agent reports what it measured and what it could not check (devices, screen readers). Device checks go to the roadmap's verification backlog; they are not claimed as done.

## Decisions

Proposed. Agree them before the first step.

- **D1 Track A ships first, as one release (1.13.0).** It is small, it touches files the feature packages also touch (looks, gesture router), and its allocation tests then guard the new controls.
- **D2 `<LangVersion>14</LangVersion>` in `Directory.Build.props`.** Today no version is set, so net11 targets compile as C# 15 and net10 targets as C# 14 from the same source. A build with `-p:SkUiNet=11` would accept syntax the net10 build rejects. C# 15 has no feature that reduces allocations (its generated unions box value-type cases), so nothing is lost.
- **D3 No source generator for the two layers in this plan.** The duplication between `SkUiX` and `SkUiCoreX` is the main structural cost, and ten controls add to it. F1 records how many lines its two layers duplicate; decide on a generator spike from that number.
- **D4 Optional packages style themselves.** `SkUiLook` cannot gain members from another assembly. Charts and Lottie follow the Markdown package: one style object per package, defaults from `SkUiColorScheme`, settable per view.
- **D5 The sheet (F3) is a drawn container inside a surface, not a window.** It does not change "popup windows of SkiaUi's own" under not planned. It cannot cover hosted native views until overlay masks exist (backlog).
- **D6 Charts start small.** Phase 1 is four cartesian series and a sparkline. Pie, donut, gauges, pan and zoom are phase 2, taken up when an app asks.
- **D7 The data grid (F9) is SkUi\* only at first** and waits for a decision on the virtual stack's horizontal layout (backlog).
- **D8 Backdrop blur (F10) ships only if the Galaxy S9 holds 50 fps** with one blurred bar over a scrolling list. Otherwise it stays a spike with its numbers in Performance.md.
- **D9 Runtime async (A7) is enabled only if** the image-load scenarios allocate less and the package does not grow.
- **D10 Releases (proposed):** 1.13 track A; 1.14 F1, F5, F6; 1.15 F2, F3; 1.16 F4 phase 1; then F7, F8, F9, F10 as apps ask.

## Order and parallel work

| Wave | Packages | Can run in parallel | Shared files to watch |
| --- | --- | --- | --- |
| 1 | A1, A2, A5, A6 | Yes, all four | None |
| 2 | A3, A4, A7 | A3 with A4; A7 after both | `SkUiCompositor.cs` (A1 before A3), `SkUiLook.cs` (A4 alone) |
| 3 | F1, F5, F6 | Yes, in worktrees | `SkUiLook.cs`, `DefaultSkUiLook.cs`, `ImplicitStyleTests`, `ComponentDemos.cs`: each agent appends, merge in order |
| 4 | F2, then F3 | No: F3 uses F2's links for its scrim and content | `SkUiRenderProps.cs`, `SkUiCompositor.cs`, `SkUiScrollController.cs` |
| 5 | F4, F8 | Yes: separate new projects | `SkiaUi.slnx`, `Directory.Build.props`, `scripts/net10_solution.py` output |
| 6 | F7, F9, F10 | F7 with F9; F10 alone | F10: `SkUiCompositor.cs`, `SkUiRenderProps.cs` |

One package per agent, each in its own worktree and branch. `SkUiViewHandler.cs`, `SkUiTextFieldModel.cs` and `SkUiVirtualVerticalStackLayoutBase.cs` are large coordinating classes: never give two running agents a package that edits the same one.

## Track A: allocation pass

### A1 `System.Threading.Lock` for the remaining gates

- **Size:** S. **Depends on:** nothing.
- **Find:** `rg -n 'readonly object' -g '*.cs' MauiSkiaUi` and keep the fields used with `lock`: `SkUiCompositor` (`_pendingLock`, `_renderLock`), `SkUiFrameRenderer` (`_sizeLock`), `SkUiMetalSurface` (`_frameLock`, `Gate`), `SkUiImageCache`, `SkUiImageLoader`, `SkUiImageDiskCache`, `SkUiFonts` (`Gate`), `SkUiFrameTrace` (`_gate`). The item-key objects in the collection view and the carousel are not locks: leave them.
- **Do:** change the field type to `Lock` (as `SkUiStrokedOutlines.Gate`). Check first that no gate is used with `Monitor.Wait`, `Monitor.Pulse`, `Monitor.TryEnter` or passed as `object`; such a gate stays as it is, with a comment.
- **Done when:** the suite is green on both lines and `scripts/bench.sh --baseline HEAD` shows no phase slower. No changelog entry is needed beyond one line under `### Other`.

### A2 Gesture arena without an array per pointer event

- **Size:** S. **Depends on:** nothing.
- **Find:** `rg -n 'ToArray\(\)' MauiSkiaUi/Gestures MauiSkiaUi/Controls/Scrolling`. The hot one is the move / release loop over `arena.Members` in `SkUiPointerRouter`; the others are press, accept, cancel, hover and the scroll view's listeners.
- **Do:** replace each snapshot with a reusable buffer: rent from `ArrayPool<T>.Shared`, copy, iterate, clear and return in `finally`. Keep the existing "is it still a member" check, since members leave during the loop. Re-entrancy is real (a handler can open a popup or cancel an arena), so no shared static list.
- **Test:** an allocation test beside `PerformanceTests.CoreWrapAndShrinkRelayoutAllocatesNothing`: after a warm-up, 100 moves of one pointer over a tree with a tap and a pan recognizer allocate 0 bytes (`GC.GetAllocatedBytesForCurrentThread`).
- **Done when:** that test passes and every gesture test passes unchanged.

### A3 Pooled render batches and updates

- **Size:** M. **Depends on:** A1 (same file).
- **Read first:** [RenderingPipeline.md](RenderingPipeline.md) (grep "batch" and "ownership"), `SkUiRenderRecorder.cs`, and `SkUiRenderBatch` / `SkUiRenderUpdate` at the top of `SkUiCompositor.cs`.
- **Do:**
  1. In the recorder's children pass, replace `_scratch.GetRange(start, count).ToArray()` with one copy from `CollectionsMarshal.AsSpan(_scratch).Slice(start, count)`.
  2. Pool `SkUiRenderBatch` and `SkUiRenderUpdate`: after `ApplyPending` the compositor clears each object (every reference, so nothing committed is kept alive or disposed twice) and pushes it on a lock-free stack the recorder pops from. A batch discarded on dispose is not pooled. Cap the pool.
  3. Keep the ownership rule: objects committed in `SkUiRenderProps` belong to the compositor. Pooling the envelope must not change who disposes its contents.
- **Test:** an allocation test: 60 frames of a UI-clock transition on one switch allocate no `SkUiRenderBatch` or `SkUiRenderUpdate` after warm-up (measure bytes per frame before and after, assert a bound). Run the leak scenarios (`MemoryLeakTests`).
- **Bench:** add a scenario with 50 running UI-clock transitions if none exists; report allocated KB per frame before and after with `--quiet-gc`.
- **Out of scope:** changing what a batch carries.

### A4 Shared scratch paints

- **Size:** S to M. **Depends on:** nothing; edits `SkUiLook.cs` alone in its wave.
- **Find:** `rg -n 'using var [a-zA-Z]+ = new SKPaint' -g '*.cs' MauiSkiaUi`. Skip `SkUiGpuWarmUp`, `SkUiImageTransformations`, `SkUiFontImages` and `SkUiVectorPainter`'s layer paints, which run once per image, not per control.
- **Do:** follow the pattern already in `DefaultSkUiLook` (`_paint ??= new SKPaint()`) and `SkUiShapePainter`: one scratch paint per painter family, reset before use (`paint.Reset()`, then set what the site needs). Recording runs on the UI thread, but looks are shared objects, so the scratch is `[ThreadStatic]` and a site that calls into another painter while holding it uses its own.
- **Risk:** a paint that leaks state (shader, path effect, blend mode) to the next site. `Reset()` at every use is the rule; the existing parity tests (`ControlParityTests` and the per-control ones) must pass unchanged.
- **Bench:** `core-stress-buttons`, `skui-stress-buttons`, and a scenario with sliders, steppers and tokens; report record time and allocated KB.

### A5 Text plain-path check

- **Size:** S. **Depends on:** nothing.
- **Where:** `SkUiShaping.IsPlain` and `HasGlyph`.
- **Do:** fetch the typeface's coverage array once per string (one `Coverage.GetOrAdd`), then test characters against it in the loop; keep the lazy fill for unknown entries. Before the loop, reject text with a character outside the simple ranges with one vectorized pass (`ContainsAnyExceptInRange` for the ASCII range as the fast accept, or a `SearchValues<char>` built from `IsSimpleChar`'s ranges).
- **Test:** the shaping tests pass unchanged; add cases at the range borders (U+007E, U+00A0, U+017F, U+2010, U+20C0, a surrogate pair).
- **Bench:** `core-labels`, `skui-labels` with `-n 10 -r 3`.

### A6 Image memory-cache hits

- **Size:** S. **Depends on:** nothing.
- **Where:** `SkUiImageLoader.Load` and its callers (`LoadAsync`, `WithOrigin`).
- **Do:** look up the memory cache before creating `SkUiImageLoadContext`; return `ValueTask<SkUiCachedImage>` (or a synchronous `TryGetCached` the views call first) so a hit allocates no `Task`. Internal API only: public signatures stay.
- **Test:** binding 100 recycled rows to images already in the memory cache allocates a bounded number of bytes per row; assert the bound after measuring. Image and leak tests pass unchanged.

### A7 Toolchain and API

- **Size:** M. **Depends on:** A1 to A6 merged, so the numbers describe the result.
- **Do:**
  1. `<LangVersion>14</LangVersion>` in `Directory.Build.props` ([D2](#decisions)), with a comment on why.
  2. `params ReadOnlySpan<T>` overloads beside the public `params T[]` setters (`SetStrokeDashArray`, `SetPoints`, `SetTransformations` and their Core twins, the poly-shape constructors). Additive: the array overloads stay. `MauiApiParityTests` and the API comparison must pass.
  3. Runtime async trial ([D9](#decisions)): `<Features>runtime-async=on</Features>` for net11 targets only, in a branch. Compare image-load scenarios and the packed package size with and without. Keep it only if it wins; record the numbers either way in [Benchmarks.md](Benchmarks.md).
  4. Measure [Performance.md](../Performance.md) again on .NET 11 (CoreCLR on Android and iOS, cached interface dispatch on iOS). This needs the two test phones: an agent prepares the scripts and the tables, a person runs them.
- **Out of scope:** `[SkipLocalsInit]`, inline arrays and SIMD: no profile points at them.

## Track F: features

Each package starts with a short design note in its control page (`docs/controls/SkUiX.md`: API table, behavior, keys, semantics) before code, so the reviewer can check the API against MAUI's naming.

### F1 Segmented control and tab view

- **Size:** M. **Depends on:** track A. **Layers:** both for the segmented control; the tab view is SkUi\* only (the carousel has no Core twin).
- **Read first:** `SkUiToggleControl.cs`, `SkUiToggleDrawing.cs`, `SkUiRadioButton.cs` (group semantics), [ControlLook.md](ControlLook.md#state-change-transitions-fr-26), `docs/controls/SkUiCarouselView.md`, `docs/controls/SkUiContentView.md` (grep "WhenShown").
- **Build:**
  1. `SkUiSegmentedEngine` in `Helpers`: segment measuring (equal or by content), hit-testing, the selection's animated position (`SkUiTween`), keys (Left / Right, Home / End).
  2. `SkUiSegmentedControl` / `SkUiCoreSegmentedControl`: `ItemsSource` or inline segments (text, icon, both), `SelectedIndex` / `SelectedItem` (two-way through `WriteBack`), `SelectionChanged`, `Command`, disabled segments. Look members: track, selection pill, separators, per platform look.
  3. `SkUiTabView`: a tab strip (fixed or scrolling, top or bottom, an underline or pill indicator that follows the swipe through a render link) over a carousel of panes created when first shown. `SelectedIndex`, `TabItems` with header text, icon and badge text, `IsSwipeEnabled`.
- **Accessibility:** the strip is one tab stop with a current item (as the collection view's keyboard model); role tab / tab list; "selected, 2 of 4".
- **Tests:** engine geometry, selection by tap and keys, two-way binding kept by `WriteBack`, right-to-left mirroring, implicit styles, XAML, a leak scenario per control.
- **Bench:** 200 segmented controls in a list; a tab view of 5 heavy panes must create only the first.
- **Docs:** control pages, gallery pages, Migration.md rows for the vendor tab views and segmented controls.
- **Report:** the number of lines duplicated between the two segmented layers ([D3](#decisions)).

### F2 Scroll-linked effects

- **Size:** M. **Depends on:** track A.
- **Read first:** `SkUiRenderLink` and `SkUiItemEffectLink` in `SkUiRenderProps.cs`, how `SkUiCompositor` evaluates `Link`, and the pinned-header code in the scroll views (grep `Link =`). [ScrollingAndCollectionViews.md](ScrollingAndCollectionViews.md): grep "pinned" and "sticky".
- **Build:**
  1. Generalize the internal link: per property (translation X / Y, opacity, scale, clip height), `clamp(factor · offset + base, min, max)` with an optional easing, evaluated by the compositor against the source scroller's offset each frame.
  2. Public API on both layers: `SkUiScrollEffect` (source scroller, input range in DIPs, output property and range), attached to any drawn view or Core node inside or outside the scroller. The UI side writes the same values for hit-testing, as the link does today.
  3. Ready-made recipes in the docs and the gallery: collapsing header with a title that shrinks, parallax image, a toolbar that fades in, a floating button that hides on scroll.
- **Take in from the backlog:** "render-thread effects linked to overscroll" uses the same mechanism: an effect's input range may extend past the scroll range.
- **Tests:** compositor tests through `SkUiFrameRenderer` (offset in, props out, no recording: assert zero re-records with the diagnostics counters), immediate-painter parity, reduce motion (effects snap to their end state where they are decorative).
- **Bench:** a list with a collapsing header: render time per frame against the same list without it.

### F3 In-surface sheet

- **Size:** M to L. **Depends on:** F2.
- **Read first:** `SkUiScrollController.cs` (fling, snap points, nested hand-off), `SkUiDraggerInput.cs`, `ISkUiPopupPresenter.cs`, [Popups.md](Popups.md), [EventMechanism.md](EventMechanism.md) (grep "nested").
- **Build:**
  1. `SkUiSheetEngine`: detents (fractions, DIPs, fit content), the drag with velocity to the nearest detent, dismissal past the lowest, all as render-thread translation.
  2. `SkUiSheet` (SkUi\*), placed as the last child of the surface root's layout: `Edge` (bottom, start, end), `Detents`, `SelectedDetent` (two-way), `IsOpen` (two-way), `IsModal` (scrim, tap outside dismisses), `HasGrabber`, `Content`. Scrolling content hands the drag to the sheet at its top edge.
  3. `SkUiSheetPopupPresenter`: an `ISkUiPopupPresenter` that shows picker popups in a sheet, for apps without a popup system.
- **Accessibility:** modal sheets trap focus and are announced; Escape and the back gesture close; the grabber has "expand" / "collapse" actions.
- **Limits to document:** it cannot cover hosted native views ([D5](#decisions)); keyboard avoidance uses the viewport insets from 1.6.
- **Tests:** detent math, drag and fling to detents, nested scroll hand-off, modal focus, `IsOpen` binding, a leak scenario (`SheetOpenedAndClosed`).
- **Device checks (verification backlog):** feel on the S9 and the iPhone 8, the Android back gesture, the keyboard over a sheet.

### F4 Charts package, phase 1

- **Size:** L. **Depends on:** track A. **New project:** `MauiSkiaUi.Charts` (`SkiaUi.Maui.Charts`), set up like `MauiSkiaUi.Markdown` (csproj, README, both lines, in `SkiaUi.slnx` and the generated net10 solution, pack and consumer-app CI).
- **Read first:** `MauiSkiaUi.Markdown/MauiSkiaUi.Markdown.csproj` and `SkUiMarkdownStyle.cs` (the package and style pattern), `SkUiShapePainter.cs`, `SkUiTextLayout` usage in `SkUiCoreLabel.cs`, the `SkUiWeakListener` example in its doc comment (a chart model).
- **Build:**
  1. Model: `SkUiChartSeries` (line, area, bar, stacked bar) over `ItemsSource` with X / Y bindings read through compiled accessors (AOT-safe, as `SkUiDisplayBinding`), numeric, date and category axes with "nice" ticks, a legend, `SkUiChartStyle`.
  2. `SkUiCoreChart` draws plot, axes and series as separate render nodes, so a data change re-records the series only; `SkUiChart` is the SkUi\* twin. `SkUiSparkline` / `SkUiCoreSparkline`: one series, no axes, sized for list rows.
  3. Interaction: a tooltip and crosshair on press or hover; selection of a point.
  4. Motion: series animate in and between data sets on the UI clock, off under reduce motion.
- **Accessibility:** the chart is a group with a summary; points are reachable elements ("March, 42 units").
- **Tests:** tick generation, scaling, hit-testing, decimation of long series (min / max per pixel column), empty and NaN data, right-to-left.
- **Bench:** 200 sparklines in a list (record and scroll), one chart of 10,000 points (first frame under 16 ms headless is the target to measure against).
- **Out of scope (phase 2):** pie, donut, radial gauge, pan and zoom, financial series, trackball across charts.

### F5 Chips

- **Size:** S to M. **Depends on:** track A. **Layers:** both.
- **Read first:** `SkUiButton.cs`, `SkUiToggleControl.cs`, the picker's token drawing in `SkUiLook.cs` (grep "token"), `SkUiWrapEngine.cs`.
- **Build:** `SkUiChip` (text, leading icon or avatar, optional remove button, `IsSelected` for filter chips, `Command`) and `SkUiChipGroup` (wrap or one scrolling row, `ItemsSource`, `SelectionMode` none / single / multiple, `SelectedItems`, `ChipRemoved`). Look members for the four kinds: action, filter, input, suggestion.
- **Accessibility:** a filter chip is a toggle; remove is a custom action; arrow keys move within the group.
- **Then:** note in the roadmap that the pickers' token template can build on it.

### F6 Range slider and rating

- **Size:** S each. **Depends on:** track A. **Layers:** both.
- **Read first:** `SkUiSlider.cs`, `SkUiSliderInput.cs`, the slider's look members.
- **Build:**
  1. `SkUiRangeSlider`: `RangeStart`, `RangeEnd` (two-way, clamped and coerced before `WriteBack`), `MinimumRange`, `Step`, both orientations, the thumb nearest the press moves, drag of the range itself (optional).
  2. `SkUiRating`: `Value`, `Maximum`, `Precision` (full, half, exact), `IsReadOnly`, a shape from a path or the look's star, drawn with a clip for partial fill.
- **Accessibility:** each thumb is an adjustable element with its own label; the rating is one adjustable element.

### F7 Autocomplete

- **Size:** M. **Depends on:** track A; reads F5 for tokens later.
- **Read first:** [CustomPickers.md](../controls/CustomPickers.md), `SkUiPopups.ShowContentAsync`, `SkUiEntry.cs`, the picker's searchable list (grep `IsSearchEnabled`).
- **Build:** `SkUiAutoComplete` (SkUi\*, then Core): a drawn entry whose suggestions open in an anchored drawn list through the popup presenter. `ItemsSource` with a filter mode (starts with, contains, custom), or `SuggestionsProvider` (`Func<string, CancellationToken, Task<IReadOnlyList<object>>>`, debounced, the previous request cancelled), `ItemDisplayBinding`, `ItemTemplate`, matched text highlighted with spans, `SelectedItem`, free text allowed or not.
- **Accessibility and keys:** Down opens and moves, Enter chooses, Escape closes; the list is announced with its count.
- **Edits `SkUiTextFieldModel.cs` only if needed,** and then alone in its wave.

### F8 Lottie package

- **Size:** S to M. **Depends on:** track A. **New project:** `MauiSkiaUi.Lottie` (`SkiaUi.Maui.Lottie`) on `SkiaSharp.Skottie`.
- **First step:** check that `SkiaSharp.Skottie` has a version matching `$(SkiaSharpVersion)`, is trim and AOT clean, and what it adds to an app's size on Android and iOS. Stop and report if any of the three fails.
- **Read first:** `SkUiRenderDrawable.cs` and `SkUiShimmerRendering.cs` (the one drawable in the library), [RenderingPipeline.md](RenderingPipeline.md) (grep "drawable").
- **Build:** `SkUiLottieView` / `SkUiCoreLottie`: `Source` (asset, stream, URI through the image loader's download cache), `IsPlaying`, `RepeatCount`, `Speed`, `Progress` (two-way), `AnimationCompleted`. The animation is an `SkUiRenderDrawable` the compositor seeks and draws from `SkUiRenderClock`; the UI thread records nothing per frame. Under reduce motion it shows the first or a chosen frame.
- **Untrusted files:** size and layer limits, no external assets unless allowed, as the SVG reader's rules.
- **Bench:** 20 playing animations in a scrolling list: render time per frame, zero re-records.

### F9 Data grid

- **Size:** L. **Depends on:** F2 (frozen columns are links), [D7](#decisions).
- **Read first:** `docs/controls/SkUiCoreTable.md`, `docs/controls/SkUiVirtualVerticalStackLayout.md`, `docs/controls/SkUiGridSplitter.md`, the collection view's selection and keyboard code.
- **Build:** `SkUiDataGrid` (SkUi\*): text, numeric, date, check box and template columns; rows virtualized on the virtual stack, each row one drawn node whose cells are laid out by shared column widths; horizontal scrolling with a frozen header row and frozen leading columns; sort by header tap (the grid sorts a view, never the app's list); column resize with the splitter engine; row selection (the collection view's modes); `ItemsSource` change animations off by default.
- **Accessibility:** table, row and cell roles; arrow keys move by cell.
- **Bench:** 10,000 rows × 20 columns: first frame, fling, allocated KB per recycled row.
- **Out of scope:** editing, grouping, filtering UI, column reordering, export.

### F10 Backdrop blur

- **Size:** M. **Depends on:** F2 merged (same files), [D8](#decisions).
- **Read first:** `SkUiRenderProps.cs`, the layer handling in `SkUiCompositor.cs` and `SkUiRenderRecorder.cs` (grep `_layerPaint`), `SkUiGpuWarmUp.cs` (blur warm-up), [Performance.md](../Performance.md#drawing-effects).
- **Build:** a composite-time property (`BackdropBlurRadius`, `BackdropTint`) on both layers: the compositor draws the node through a save-layer with a backdrop blur filter, clipped to the node's shape. Software surfaces and reduce transparency fall back to the tint alone.
- **Measure first:** a spike on the S9 and the iPhone 8 with one blurred bar over the effects stress list. Ship only on [D8](#decisions)'s terms; put the numbers and the "one or two per screen" advice in Performance.md.
- **Tests:** compositor output against the immediate painter; the fallback path; no re-recording when the content behind changes.

## What this plan leaves alone

The roadmap's own backlog (calendar, full SVG, MAUI parity members, off-thread recording, relayout boundaries, overlay masks) keeps its order. Tree view, shared-element transitions, staggered grids, signature pad and barcode generation were considered and left out; a snackbar can be folded into F3 if wanted.
