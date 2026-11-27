# SkiaUi implementation plan: allocation pass and new controls

**Status:** from [a review](Review-2026-11.md) of the architecture, the implementation and the .NET 10 / 11 and C# 14 / 15 feature use (16 November 2026). The findings are agreed and on the [roadmap](Roadmap.md): track A is release 1.14 (moved from 1.13 on 17 November 2026, when the calendar took 1.13), planned step by step in [ImplementationPlan-1.14.md](../archive/ImplementationPlan-1.14.md) (which wins where the two differ), and track F is on the backlog, starting in 1.16 (moved from 1.15 on 19 November 2026, when finishing the calendar took 1.15: [ImplementationPlan-1.15.md](../archive/ImplementationPlan-1.15.md)); F2 is 1.16 (23 November 2026: [ImplementationPlan-1.16.md](../archive/ImplementationPlan-1.16.md)), F1 is 1.17, built (25 November 2026: [ImplementationPlan-1.17.md](../archive/ImplementationPlan-1.17.md)), with F5 and F6 back on the backlog after F3. [D1](#decisions) is agreed; the other decisions are still proposed, each to be agreed before its package starts.

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
- Tests are headless xUnit (`tests/MauiSkiaUi.Tests`). Text tests register the bundled font; XAML-loading classes join `RuntimeXamlCollection`. `ImplicitStyleTests` finds every public drawn control by reflection and must pass for a new one; a new control gets a leak scenario in `tests/Shared/MemoryLeaks`.
- Performance: a feature a view does not use costs nothing per frame. Run `scripts/bench.sh --baseline HEAD` (and `--quiet-gc` when one phase jumps). New bench scenarios set new options through `TrySet` / `TrySetOption`.
- The build stays at 0 warnings on `net10.0` and `net11.0`: `dotnet build MauiSkiaUi/MauiSkiaUi.csproj -f net10.0 -v q -clp:ErrorsOnly`, then the same for `net11.0`, then `dotnet test tests/MauiSkiaUi.Tests/MauiSkiaUi.Tests.csproj --logger "console;verbosity=minimal"`.
- When a feature ships: an entry under `## Unreleased` in the changelog, a page in `docs/controls/`, a gallery page in `MauiSkiaUiDemo/ComponentPages` (next to the native control where MAUI has one), the status in [Roadmap.md](Roadmap.md) and [Requirements.md](Requirements.md), and [Migration.md](../Migration.md) plus `plugins/skiaui-migration` when a MAUI or vendor conversion path changes.
- An agent reports what it measured and what it could not check (devices, screen readers). Device checks go to the roadmap's verification backlog; they are not claimed as done.

## Working rules for an agent

Written for an agent that follows steps rather than designs. They apply to every package.

1. Do the steps in their order. One commit per step, after the step's check passes.
2. Implement the brief's API table as written: these names, types and defaults, and no other public member. A member the table does not explain is copied from the pattern control named in the brief.
3. Copy the pattern, do not invent one. Before writing a file, open the pattern file the recipe or the brief names and keep its structure (regions, order of members, how properties are declared).
4. After every step run, in this order: `dotnet build MauiSkiaUi/MauiSkiaUi.csproj -f net10.0 -v q -clp:ErrorsOnly`, the step's own tests (`dotnet test tests/MauiSkiaUi.Tests/MauiSkiaUi.Tests.csproj --filter "FullyQualifiedName~<TestClass>" --logger "console;verbosity=minimal"`), and before the last step's commit the whole suite on both lines.
5. Stop and report, without guessing, when: a symbol or file the brief names does not exist; a test outside the package still fails after two attempts to fix it; a step needs a file another running package owns ([Order and parallel work](#order-and-parallel-work)); the bench shows a phase more than 5 % slower in two runs; a step would change or remove an existing public member.
6. The final report lists: the steps done, the tests added (class names), the bench table, the files changed outside the brief's list and why, and what was not checked (devices, screen readers).

### Which agent for which package

| Agent | Packages |
| --- | --- |
| A simple agent, from the brief as written | A1, A2, A4, A5, A6, A7 steps 1 and 2, F5, F6 |
| A simple agent, after step 1 (the control page) is reviewed by a stronger agent or a person | F1, F7, F8 steps 2 and 4 to 6 |
| A stronger agent | A3 (objects crossing threads), A7 steps 3 and 4 (measuring), F2, F3 steps 2 and 4, F4 steps 2 to 5, F8 steps 1 and 3, F9, F10 |

## Control recipe

What a new drawn control `X` touches, in build order. The pattern is the stepper: `rg -l Stepper -g '*.cs' MauiSkiaUi tests MauiSkiaUiDemo` lists the same places.

| # | File | What goes there | Pattern |
| --- | --- | --- | --- |
| 1 | `docs/controls/SkUiX.md` | API table, behavior, keys, semantics | `docs/controls/SkUiStepper.md` |
| 2 | `MauiSkiaUi/Helpers/SkUiXEngine.cs` (or `…Model.cs`, `…Math.cs`) | State machine and geometry shared by both layers; no view types | `SkUiStepperModel.cs`, `SkUiStepperMath.cs` |
| 3 | `MauiSkiaUi/Look/SkUiLookPaints.cs` | The paint struct (`SkUiXPaint`): resolved colors, state, transition progress | The stepper's paint struct |
| 4 | `MauiSkiaUi/Look/SkUiLook.cs`, `DefaultSkUiLook.cs` | `MeasureX` and `DrawX`; default sizes. Platform looks (`WindowsSkUiLook.cs` and the others in `Look/`) only where the platform differs; a row in `SkUiLookComparisonPage.cs` | grep `Stepper` in each |
| 5 | `MauiSkiaUi/Controls/Basic/SkUiX.cs` | The SkUi\* control: bindable properties, fluent setters, input, `WriteBack` | `SkUiStepper.cs`, `SkUiStepperBase.cs` |
| 6 | `MauiSkiaUi/Core/SkUiCoreX.cs` | The Core twin: `SetX` validates, `SetProperty`, invalidates | `SkUiCoreStepper.cs` |
| 7 | `MauiSkiaUi/Accessibility/SkUiSemanticsOwner.cs`, `SkUiFocusManager.cs` | Role, value, actions; focus and keys | grep `Stepper` |
| 8 | `tests/MauiSkiaUi.Tests/XEngineTests.cs`, `XTests.cs` | Engine tests; control tests on both layers; a test that draws both layers and compares pixels | `StepperTests.cs` (both layers), `ControlParityTests.cs` (its `Render` helper) |
| 9 | `OneWayBindingTests.cs`, `BindableStoreTests.cs`, `ElementInterfaceTests.cs`, `PlatformLookShapeTests.cs` | One entry each for the new control | grep `Stepper` in each |
| 9a | `ImplicitStyleTests.cs` | Nothing for a control in an assembly it already scans (it finds every public `SkUiView` by reflection, so the new control must pass it); a new package adds its assembly to `DrawnViewTypes` | The Markdown assembly's line |
| 10 | `tests/Shared/MemoryLeaks/LeakScenarios.cs` | A leak scenario | The stepper's or the picker wheels' |
| 11 | `MauiSkiaUiDemo/ComponentPages/Basic/XDemoPage.cs`, `ComponentDemos.cs` | A gallery page; `ComponentDemoTests` fails without it | `StepperDemoPage.cs` |
| 12 | `benchmarks/Scenarios/Scenarios.cs` | `skui-x` and `core-x` when the control appears in lists; new options through `TrySet` | `skui-picker-wheels` |
| 13 | `docs/controls/README.md`, `CHANGELOG.md`, `Roadmap.md`, `Requirements.md`, `docs/Migration.md`, `plugins/skiaui-migration` | Index row, changelog entry, status, conversion rows for vendor controls | grep `Stepper` in the scripts |

`MauiApiParityTests` gets an entry only when MAUI has the control (none of track F's do). A control in an optional package skips rows 3 and 4 and uses a style object ([D4](#decisions)).

## Decisions

Proposed. Agree them before the first step.

- **D1 Track A ships first, as one release (1.14.0; the calendar, 1.13.0, comes before it).** It is small, it touches files the feature packages also touch (looks, gesture router), and its allocation tests then guard the new controls.
- **D2 `<LangVersion>14</LangVersion>` in `Directory.Build.props`.** Today no version is set, so net11 targets compile as C# 15 and net10 targets as C# 14 from the same source. A build with `-p:SkUiNet=11` would accept syntax the net10 build rejects. C# 15 has no feature that reduces allocations (its generated unions box value-type cases), so nothing is lost.
- **D3 No source generator for the two layers in this plan.** The duplication between `SkUiX` and `SkUiCoreX` is the main structural cost, and ten controls add to it. F1 records how many lines its two layers duplicate; decide on a generator spike from that number.
- **D4 Optional packages style themselves.** `SkUiLook` cannot gain members from another assembly. Charts and Lottie follow the Markdown package: one style object per package, defaults from `SkUiColorScheme`, settable per view.
- **D5 The sheet (F3) is a drawn container inside a surface, not a window.** It does not change "popup windows of SkiaUi's own" under not planned. It cannot cover hosted native views until overlay masks exist (backlog).
- **D6 Charts start small.** Phase 1 is four cartesian series and a sparkline. Pie, donut, gauges, pan and zoom are phase 2, taken up when an app asks.
- **D7 The data grid (F9) is SkUi\* only at first** and waits for a decision on the virtual stack's horizontal layout (backlog).
- **D8 Backdrop blur (F10) ships only if the Galaxy S9 holds 50 fps** with one blurred bar over a scrolling list. Otherwise it stays a spike with its numbers in Performance.md.
- **D9 Runtime async (A7) is enabled only if** the image-load scenarios allocate less and the package does not grow.
- **D10 Releases (proposed):** 1.13 the calendar (not part of this plan); 1.14 track A; 1.15 the calendar finished (not part of this plan; agreed 19 November 2026, which moved everything below by one release); 1.16 F1, F5, F6; 1.17 F2, F3; 1.18 F4 phase 1; then F7, F8, F9, F10 as apps ask. Changed on 23 November 2026 ([1.16 plan, D1](../archive/ImplementationPlan-1.16.md#decisions)): 1.16 F2; 1.17 F1, F5, F6; 1.18 F3; 1.19 F4 phase 1. Changed again on 24 November 2026 ([1.17 plan, D1](../archive/ImplementationPlan-1.17.md#decisions)): 1.17 F1 with the carousel's parity items; F5 and F6 after F3, their release decided when 1.18 is planned. Decided on 26 November 2026 ([1.18 plan, D1](../archive/ImplementationPlan-1.18.md#decisions)): 1.18 F3 and F5; F6 back on the backlog.

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

Every package follows the [control recipe](#control-recipe) and the [working rules](#working-rules-for-an-agent). The API tables below are the contract: an agent implements these members with these names, types and defaults, and adds no other public member. Colors typed `Color?` default to `null`, meaning the look's or the color scheme's value. "Two-way" means the control writes the value through `WriteBack` and the property's default binding mode is two-way.

Step 1 of every package is the control page (`docs/controls/SkUiX.md`) with the API table copied from here, the keys and the semantics. It is reviewed before step 2 starts. A change the review makes to the table is copied back into this plan.

### F1 Segmented control and tab view

Shipped in 1.17; planned step by step in [ImplementationPlan-1.17.md](../archive/ImplementationPlan-1.17.md), which wins where the two differ (the tab view is built on the carousel engine with panes it keeps, not on a `SkUiCarouselView`).

- **Size:** M. **Depends on:** track A. **Agent:** design step reviewed, then a simple agent.
- **Layers:** both for the segmented control; the tab view is SkUi\* only (the carousel has no Core twin).
- **Pattern to copy:** the stepper for the file layout; `SkUiToggleControl.cs`, `SkUiToggleDrawing.cs` and `SkUiTween` for the animated selection; `SkUiRadioButton.cs` for one-of-many semantics.
- **Read first:** [ControlLook.md](ControlLook.md#state-change-transitions-fr-26), `docs/controls/SkUiCarouselView.md`, `docs/controls/SkUiContentView.md` (grep "WhenShown").

`SkUiSegmentedControl` / `SkUiCoreSegmentedControl`:

| Member | Type | Default | Notes |
| --- | --- | --- | --- |
| `Segments` | `IList<SkUiSegment>` | empty | XAML content property. `SkUiSegment`: `Text`, `Icon` (`ImageSource`), `IsEnabled`, `AutomationId` |
| `ItemsSource` | `IEnumerable?` | `null` | Used when `Segments` is empty; text through `ItemDisplayBinding` (`SkUiDisplayBinding`) |
| `SelectedIndex` | `int` | `-1` | Two-way. Coerced to `-1`…count−1 before `WriteBack` |
| `SelectedItem` | `object?` | `null` | Two-way; kept in step with `SelectedIndex` |
| `SegmentWidthMode` | `SkUiSegmentWidthMode` | `Equal` | `Equal` or `Content` |
| `TextColor`, `SelectedTextColor`, `SelectionColor`, `TrackColor` | `Color?` | `null` | |
| `CornerRadius` | `double` | `-1` | `-1`: the look's |
| Font members | as `ISkUiFontElement` | | |
| `Command`, `CommandParameter` | `ICommand?`, `object?` | `null` | Run after a user selection |
| `SelectionChanged` | event | | Previous and new index |

`SkUiTabView`:

| Member | Type | Default | Notes |
| --- | --- | --- | --- |
| `Tabs` | `IList<SkUiTabItem>` | empty | Content property. `SkUiTabItem`: `Header`, `Icon`, `BadgeText`, `Content` (`View`) or `ContentTemplate`, `IsEnabled` |
| `SelectedIndex` | `int` | `0` | Two-way |
| `TabStripPlacement` | `SkUiTabStripPlacement` | `Top` | `Top` or `Bottom` |
| `TabWidthMode` | `SkUiSegmentWidthMode` | `Equal` | `Content` makes the strip scroll |
| `IndicatorStyle` | `SkUiTabIndicatorStyle` | `Underline` | `Underline`, `Pill`, `None` |
| `IsSwipeEnabled` | `bool` | `true` | |
| `SelectionChanged` | event | | |

Files to create: `Helpers/SkUiSegmentedEngine.cs`, `Controls/Basic/SkUiSegmentedControl.cs`, `Controls/Basic/SkUiSegment.cs`, `Core/SkUiCoreSegmentedControl.cs`, `Controls/Layouts/SkUiTabView.cs`, `Controls/Layouts/SkUiTabItem.cs`, `tests/…/SegmentedEngineTests.cs`, `SegmentedControlTests.cs`, `TabViewTests.cs`, the two demo pages and the two control pages.

Steps:

1. Control pages. *Check:* reviewed.
2. `SkUiSegmentedEngine`: input is the segment sizes, the available width, the width mode and the direction; output is each segment's rectangle and the selection rectangle at a progress 0…1 between two indices. No MAUI view types. *Check:* `SegmentedEngineTests` (equal and content widths, right to left, zero segments, more content than width).
3. Look: `SkUiSegmentedPaint` in `SkUiLookPaints.cs`; `MeasureSegmented` and `DrawSegmented` in `SkUiLook`, drawn by `DefaultSkUiLook` (track, selection pill, separators, pressed and disabled states). *Check:* builds with 0 warnings; `PlatformLookShapeTests` entry.
4. `SkUiSegmentedControl`: properties per the table, tap selects, the pill animates with `SkUiTween` (snaps under reduce motion). *Check:* `SegmentedControlTests` (tap, binding stays one-way after a tap, disabled segment, `ItemsSource` change).
5. `SkUiCoreSegmentedControl` on the same engine and look. *Check:* a test drawing both layers and comparing pixels.
6. Keys and semantics: Left / Right (mirrored in right to left), Home / End; one tab stop; each segment a selectable element ("selected, 2 of 4"). *Check:* key and semantics tests.
7. `SkUiTabView`: a grid of a segmented strip and a `SkUiCarouselView`; panes are created when first shown; the strip's indicator follows the carousel's offset; `SelectedIndex` is the one source of truth. *Check:* `TabViewTests` (only the first pane created, swipe changes `SelectedIndex`, strip placement, badge text).
8. Registrations, leak scenarios (`SegmentedControlShown`, `TabViewSwiped`), demo pages, bench `skui-segmented` and `core-segmented` (200 controls), docs, changelog.

**Done when:** all checks pass and the PR reports the lines duplicated between the two segmented layers ([D3](#decisions)).

### F2 Scroll-linked effects

Shipped in 1.16; planned step by step in [ImplementationPlan-1.16.md](../archive/ImplementationPlan-1.16.md), which wins where the two differ.

- **Size:** M. **Depends on:** track A. **Agent:** a stronger agent throughout (compositor and cross-thread values).
- **Read first:** `SkUiRenderLink` and `SkUiItemEffectLink` in `SkUiRenderProps.cs`, where `SkUiCompositor` evaluates `Link`, the pinned-header code in the scroll views (grep `Link =`), [ScrollingAndCollectionViews.md](ScrollingAndCollectionViews.md) (grep "pinned").

`SkUiScrollEffect` (a plain class, used by both layers; added to `SkUiView.ScrollEffects` or with `SkUiCoreNode.AddScrollEffect`):

| Member | Type | Default | Notes |
| --- | --- | --- | --- |
| `Source` | scroll view or Core scroll node | `null` | `null`: the nearest scrolling ancestor |
| `Orientation` | `ScrollOrientation` | `Vertical` | Which offset drives it |
| `InputStart`, `InputEnd` | `double` | `0`, `0` | Offset range in DIPs; may extend past the scroll range (overscroll) |
| `Property` | `SkUiScrollEffectProperty` | `TranslationY` | `TranslationX`, `TranslationY`, `Opacity`, `Scale`, `ScaleX`, `ScaleY`, `ClipHeight` |
| `From`, `To` | `double` | `0`, `0` | Output at `InputStart` and `InputEnd`; clamped outside |
| `Easing` | `Easing?` | `null` | Linear when `null`; sampled into a small table for the render thread |

Steps:

1. Control page section in `docs/controls/SkUiScrollView.md` plus recipes. *Check:* reviewed.
2. Replace the single translation link with a list of links per node, each `clamp(factor · offset + base)` onto one property. Existing users (scroll bar thumb, pinned headers) move to the list unchanged. *Check:* every scrolling and collection view test passes; bench shows no slower frame.
3. Compositor: evaluate the list each frame against the source's children offset. Nothing is recorded. *Check:* a `SkUiFrameRenderer` test: offset in, properties out, zero re-records (diagnostics counters).
4. UI side: write the same values for hit-testing and the immediate painter. *Check:* immediate painter equals compositor output.
5. Public `SkUiScrollEffect` on both layers; effects detach when the node leaves the tree. *Check:* API tests, a leak scenario (`ScrollEffectsAttached`).
6. Reduce motion: decorative effects (`Opacity`, `Scale`) hold their `To` value; layout-like ones (`ClipHeight`, translation) still run. *Check:* test.
7. Gallery page with four recipes (collapsing header, parallax image, fading toolbar, hiding button), bench `skui-scroll-effects`, docs, changelog, roadmap (the overscroll-linked effects item is taken in).

**Done when:** a 1,000-row list with a collapsing header scrolls with zero re-records and the render time per frame is within 5 % of the same list without it.

### F3 In-surface sheet

Shipped in 1.18; planned step by step in [ImplementationPlan-1.18.md](../archive/ImplementationPlan-1.18.md), which wins where the two differ (a dialog presentation, stacking, `ShowAsync<TResult>`, header and action slots).

- **Size:** M to L. **Depends on:** F2. **Agent:** a stronger agent for steps 2 and 4; a simple agent for the rest.
- **Read first:** `SkUiScrollController.cs` (fling, snap points, nested hand-off), `SkUiDraggerInput.cs`, `ISkUiPopupPresenter.cs`, [Popups.md](Popups.md), [EventMechanism.md](EventMechanism.md) (grep "nested").

`SkUiSheet` (SkUi\* only; placed as the last child of the surface root's layout):

| Member | Type | Default | Notes |
| --- | --- | --- | --- |
| `Content` | `View?` | `null` | Content property |
| `Edge` | `SkUiSheetEdge` | `Bottom` | `Bottom`, `Start`, `End` |
| `Detents` | `IList<SkUiSheetDetent>` | half, full | `SkUiSheetDetent.Fraction(d)`, `.Height(dips)`, `.FitContent` |
| `SelectedDetentIndex` | `int` | `0` | Two-way |
| `IsOpen` | `bool` | `false` | Two-way |
| `IsModal` | `bool` | `true` | Scrim, focus trap |
| `ScrimColor` | `Color?` | `null` | |
| `DismissOnScrimTap`, `IsDragEnabled`, `HasGrabber` | `bool` | `true` | |
| `CornerRadius` | `double` | `-1` | The look's |
| `OpenAsync()`, `CloseAsync()` | `Task` | | Complete when the animation ends |
| `Opened`, `Closed`, `DetentChanged` | events | | |

Steps:

1. Control page. *Check:* reviewed.
2. `Helpers/SkUiSheetEngine.cs`: detent positions from the surface size, the nearest detent for a position and a velocity, dismissal past the lowest. *Check:* `SheetEngineTests`.
3. Look members (`SkUiSheetPaint`: surface, corner radius, grabber, scrim) and `SkUiSheet` with `IsOpen` and programmatic detents as render-thread translation. *Check:* open, close and detent tests.
4. Drag: a pan recognizer on the sheet; scrolling content inside hands the drag over at its top edge (bottom sheet) through the existing nested-scroll hand-off. *Check:* tests for drag to each detent, fling, hand-off from a `SkUiScrollView` inside.
5. Modal behavior: scrim opacity linked to the sheet's position (F2), tap outside, focus trap, Escape and the back gesture close. *Check:* focus and key tests.
6. `SkUiSheetPopupPresenter : ISkUiPopupPresenter`, showing a request's content in a sheet. *Check:* a picker opens and commits through it.
7. Leak scenario `SheetOpenedAndClosed`, gallery page, docs with the limits (cannot cover hosted native views; keyboard avoidance through viewport insets), changelog; device checks added to the verification backlog.

### F4 Charts package, phase 1

- **Size:** L. **Depends on:** track A. **Agent:** a stronger agent for steps 2 to 5; a simple agent for the project setup and the rest.
- **Pattern to copy:** `MauiSkiaUi.Markdown` (project file, README, style object, both layers in a package).
- **Read first:** `MauiSkiaUi.Markdown/MauiSkiaUi.Markdown.csproj`, `SkUiMarkdownStyle.cs`, `SkUiShapePainter.cs`, the chart-model example in `SkUiWeakListener.cs`'s doc comment.

`SkUiChart` / `SkUiCoreChart`:

| Member | Type | Default | Notes |
| --- | --- | --- | --- |
| `Series` | `IList<SkUiChartSeries>` | empty | Content property |
| `XAxis`, `YAxis` | `SkUiChartAxis` | numeric, auto range | `Kind` (`Numeric`, `DateTime`, `Category`), `Minimum`, `Maximum` (`double?`, `null`: from data), `Title`, `LabelFormat`, `ShowsGridLines` |
| `ShowsLegend` | `bool` | `false` | |
| `LegendPlacement` | `SkUiChartLegendPlacement` | `Bottom` | `Top`, `Bottom`, `Start`, `End` |
| `IsTooltipEnabled` | `bool` | `true` | Press or hover |
| `SelectedPoint` | `SkUiChartPoint?` | `null` | Two-way |
| `Style` | `SkUiChartStyle?` | `null` | Colors, fonts, paddings; defaults from the color scheme |
| `PointSelected` | event | | |

`SkUiChartSeries`: `Kind` (`Line`, `Area`, `Bar`, `StackedBar`), `ItemsSource`, `XValue` and `YValue` (`Func<object, double>`, or `XBinding` / `YBinding` read as `SkUiDisplayBinding` reads), `Name`, `Color`, `StrokeWidth` (2), `ShowsMarkers` (false), `IsSmooth` (false).

`SkUiSparkline` / `SkUiCoreSparkline`: `Values` (`IReadOnlyList<double>`), `Kind` (`Line`, `Area`, `Bar`), `Color`, `StrokeWidth` (1.5), `ShowsLastPoint` (false).

Steps:

1. New project `MauiSkiaUi.Charts` copied from the Markdown project (package id `SkiaUi.Maui.Charts`), added to `SkiaUi.slnx`, the net10 solution regenerated with `scripts/net10_solution.py`, the pack and consumer-app CI lists. *Check:* builds on both lines with 0 warnings.
2. Scales and ticks (`SkUiChartScale`, "nice" numeric and date ticks, category bands). *Check:* `ChartScaleTests`, including empty, single-value and NaN data.
3. Series geometry: points to paths, bar rectangles, stacking, decimation to one min / max pair per pixel column. *Check:* `ChartGeometryTests`.
4. `SkUiCoreChart`: plot, axes and each series as separate render nodes, so a data change re-records one series. Listens to `INotifyCollectionChanged` through `SkUiWeakListener`. *Check:* a re-record count test.
5. `SkUiChart` (SkUi\* twin), legend, tooltip and selection, transitions between data sets on the UI clock. *Check:* layer pixel comparison, selection tests.
6. `SkUiSparkline` on both layers. *Check:* tests; bench `skui-sparklines` (200 in a list).
7. Semantics (a group with a summary, points as elements), right to left, leak scenarios, gallery pages, control pages, README of the package, changelog. Bench `core-chart-10k` (one line series of 10,000 points).

**Out of scope (phase 2):** pie, donut, radial gauge, pan and zoom, financial series.

### F5 Chips

Shipped in 1.18; planned in [ImplementationPlan-1.18.md](../archive/ImplementationPlan-1.18.md), which adds the group's `Padding` and a chip's `BadgeText`.

- **Size:** S to M. **Depends on:** track A. **Agent:** simple.
- **Pattern to copy:** `SkUiButton.cs` (press, command), `SkUiToggleControl.cs` (selected state), the picker's token drawing in `SkUiLook.cs` (grep "token"), `SkUiWrapEngine.cs`.

`SkUiChip` / `SkUiCoreChip`:

| Member | Type | Default | Notes |
| --- | --- | --- | --- |
| `Text` | `string?` | `null` | |
| `ImageSource` | `ImageSource?` | `null` | Leading icon or avatar |
| `Kind` | `SkUiChipKind` | `Action` | `Action`, `Filter`, `Input`, `Suggestion` |
| `IsSelected` | `bool` | `false` | Two-way; toggled by a tap when `Kind` is `Filter` |
| `ShowsRemoveButton` | `bool` | `false` | `true` by default for `Input` (a virtual default, not a property write) |
| `Command`, `CommandParameter`, `RemoveCommand` | | `null` | |
| `TextColor`, `BackgroundColor`, `SelectedColor`, `StrokeColor` | `Color?` | `null` | |
| `Clicked`, `Removed` | events | | |

`SkUiChipGroup` / `SkUiCoreChipGroup`:

| Member | Type | Default | Notes |
| --- | --- | --- | --- |
| `ItemsSource`, `ItemDisplayBinding` | | `null` | One chip per item |
| `ChipKind` | `SkUiChipKind` | `Filter` | |
| `SelectionMode` | `SkUiChipSelectionMode` | `Multiple` | `None`, `Single`, `Multiple` |
| `SelectedItem` | `object?` | `null` | Two-way |
| `SelectedItems` | `IList<object>` | empty | Two-way as the picker's multiple selection |
| `Wraps` | `bool` | `true` | `false`: one horizontally scrolling row |
| `Spacing`, `RowSpacing` | `double` | `8` | |
| `SelectionChanged`, `ChipRemoved` | events | | |

Steps: (1) control pages; (2) look: `SkUiChipPaint`, `MeasureChip`, `DrawChip` for the four kinds and states; (3) `SkUiChip`; (4) `SkUiCoreChip`, pixel comparison; (5) `SkUiChipGroup` on `SkUiWrapEngine` (or a scroll view when `Wraps` is false) and its Core twin; (6) semantics (a filter chip is a toggle, remove is a custom action, arrow keys move in the group); (7) registrations, leak scenarios, demo pages, bench `skui-chips` (500 chips), docs, changelog, and a roadmap note that the pickers' token template can build on chips. Each step's check is its test class (`ChipTests`, `ChipGroupTests`) passing.

### F6 Range slider and rating

- **Size:** S each. **Depends on:** track A. **Agent:** simple.
- **Pattern to copy:** `SkUiSlider.cs`, `SkUiSliderInput.cs`, the slider's look members and tests.

`SkUiRangeSlider` / `SkUiCoreRangeSlider`:

| Member | Type | Default | Notes |
| --- | --- | --- | --- |
| `Minimum`, `Maximum` | `double` | `0`, `1` | |
| `RangeStart`, `RangeEnd` | `double` | `0`, `1` | Two-way. Coerce before `WriteBack`: clamp to the limits, then keep `RangeEnd − RangeStart ≥ MinimumRange` by moving the thumb that is not being dragged only when it must |
| `MinimumRange` | `double` | `0` | |
| `Step` | `double` | `0` | `0`: continuous |
| Orientation, track and thumb colors | as `SkUiSlider` | | Same names as the slider's |
| `IsRangeDraggable` | `bool` | `false` | Drag the filled range to move both |
| `RangeChanged`, `DragStarted`, `DragCompleted` | events | | |

`SkUiRating` / `SkUiCoreRating`:

| Member | Type | Default | Notes |
| --- | --- | --- | --- |
| `Value` | `double` | `0` | Two-way; coerced to 0…`Maximum` and to `Precision` |
| `Maximum` | `int` | `5` | |
| `Precision` | `SkUiRatingPrecision` | `Full` | `Full`, `Half`, `Exact` |
| `IsReadOnly` | `bool` | `false` | |
| `ItemSize`, `Spacing` | `double` | `24`, `4` | |
| `Shape` | `Geometry?` | `null` | `null`: the look's star |
| `FilledColor`, `EmptyColor` | `Color?` | `null` | |
| `ValueChanged` | event | | |

Steps for each control: (1) control page; (2) engine or math in `Helpers` with tests (thumb picking: the nearest thumb, the end thumb when both sit at the minimum; rating value from a point for each precision, right to left); (3) look members; (4) SkUi\* control; (5) Core twin and pixel comparison; (6) keys (arrows, Page Up / Down, Home / End) and semantics (each thumb an adjustable element with its own label; the rating one adjustable element); (7) registrations, leak scenario, demo page, docs, changelog.

### F7 Autocomplete

- **Size:** M. **Depends on:** track A. **Agent:** design step reviewed (how it derives from the entry), then a simple agent.
- **Read first:** [CustomPickers.md](../controls/CustomPickers.md), `SkUiPopups.ShowContentAsync`, `SkUiEntry.cs`, the picker's searchable list (grep `IsSearchEnabled`).

`SkUiAutoComplete` (SkUi\* first; the Core twin is step 7). It has the entry's members (text, placeholder, fonts, field chrome) plus:

| Member | Type | Default | Notes |
| --- | --- | --- | --- |
| `ItemsSource`, `ItemDisplayBinding`, `ItemTemplate` | | `null` | |
| `FilterMode` | `SkUiAutoCompleteFilterMode` | `StartsWith` | `StartsWith`, `Contains`, `Custom`; comparisons ignore case and are culture-aware |
| `Filter` | `Func<object, string, bool>?` | `null` | For `Custom` |
| `SuggestionsProvider` | `Func<string, CancellationToken, Task<IReadOnlyList<object>>>?` | `null` | Replaces `ItemsSource` filtering; the previous call is cancelled |
| `MinimumPrefixLength` | `int` | `1` | |
| `DebounceMilliseconds` | `int` | `150` | Provider only |
| `MaxSuggestions` | `int` | `8` | |
| `HighlightsMatch` | `bool` | `true` | Matched text drawn bold through spans |
| `SelectedItem` | `object?` | `null` | Two-way |
| `AllowsFreeText` | `bool` | `true` | `false`: text reverts to the selected item on unfocus |
| `IsOpen` | `bool` | `false` | Two-way |
| `SuggestionChosen` | event | | |

Steps: (1) control page, with the decision whether it derives from `SkUiEntry` or composes one; (2) `Helpers/SkUiAutoCompleteModel.cs`: filtering, debounce, cancellation, the highlighted ranges, with tests using a fake clock; (3) the suggestion list as popup content (`SkUiPickerPopupContent` subclass) shown with the anchored placement; (4) the control: typing opens, choosing writes text and `SelectedItem`; (5) keys (Down opens and moves, Enter chooses, Escape closes) and semantics (the list announced with its count); (6) tests, leak scenario, demo page with a slow fake provider, docs, changelog; (7) the Core twin. `SkUiTextFieldModel.cs` is edited only if step 1's review says so.

### F8 Lottie package

- **Size:** S to M. **Depends on:** track A. **Agent:** step 1 and 3 by a stronger agent; the rest simple.
- **Pattern to copy:** `MauiSkiaUi.Markdown` for the project; `SkUiShimmerRendering.cs` for the drawable.

`SkUiLottieView` / `SkUiCoreLottie`:

| Member | Type | Default | Notes |
| --- | --- | --- | --- |
| `Source` | `SkUiLottieSource?` | `null` | `FromAsset(name)`, `FromStream(factory)`, `FromUri(uri)` |
| `IsPlaying` | `bool` | `true` | |
| `RepeatCount` | `int` | `-1` | `-1`: forever |
| `RepeatMode` | `SkUiLottieRepeatMode` | `Restart` | `Restart`, `Reverse` |
| `Speed` | `double` | `1` | |
| `Progress` | `double` | `0` | 0…1. Setting it seeks. Written back when playback pauses or ends, not every frame |
| `Aspect` | `Aspect` | `AspectFit` | |
| `ReducedMotionProgress` | `double` | `0` | The frame shown under reduce motion |
| `AnimationLoaded`, `AnimationFailed`, `AnimationCompleted` | events | | |

Steps:

1. Feasibility: `SkiaSharp.Skottie` at `$(SkiaSharpVersion)` exists, builds trim- and AOT-clean in a test project, and its size cost per platform is measured. *Stop and report if any fails.*
2. New project `MauiSkiaUi.Lottie` as in F4 step 1.
3. `SkUiLottieDrawable : SkUiRenderDrawable`: owns the Skottie animation, seeks from `SkUiRenderClock` and draws on the render thread; disposed by the compositor. *Check:* a `SkUiFrameRenderer` test with zero re-records across 60 frames.
4. Loading off the UI thread with limits (file size 2 MB by default, no external assets), through the image loader's download cache for URIs. *Check:* tests with a valid, an oversized and a malformed file.
5. The two controls per the table; reduce motion. *Check:* `LottieTests`, pixel comparison of the two layers at a fixed progress.
6. Leak scenario `LottiePlayed`, gallery page, bench `skui-lottie` (20 playing in a list), docs, package README, changelog.

### F9 Data grid

- **Size:** L. **Depends on:** F2, [D7](#decisions). **Agent:** a stronger agent throughout; simple agents for column types and docs.
- **Read first:** `docs/controls/SkUiCoreTable.md`, `docs/controls/SkUiVirtualVerticalStackLayout.md`, `docs/controls/SkUiGridSplitter.md`, the collection view's selection and keyboard code.

`SkUiDataGrid` (SkUi\* only):

| Member | Type | Default | Notes |
| --- | --- | --- | --- |
| `ItemsSource` | `IEnumerable?` | `null` | |
| `Columns` | `IList<SkUiDataGridColumn>` | empty | Content property. Column: `Header`, `Binding` or `ValueAccessor`, `Width` (`GridLength`), `MinimumWidth` (40), `CanSort`, `CanResize` (true), `TextAlignment`, `Format`. Types: text, numeric, date, check box, template (`CellTemplate`) |
| `FrozenColumnCount` | `int` | `0` | Leading columns that do not scroll horizontally |
| `RowHeight`, `HeaderHeight` | `double` | `-1` | The look's |
| `SelectionMode`, `SelectedItem`, `SelectedItems` | as the collection view's | | |
| `SortDescriptions` | `IList<SkUiDataGridSort>` | empty | Column and direction; the grid sorts a view, never the app's list |
| `GridLinesVisibility` | `SkUiGridLines` | `Horizontal` | `None`, `Horizontal`, `Vertical`, `Both` |
| `Sorting` (cancelable), `SelectionChanged`, `CellTapped` | events | | |

Steps: (1) control page and the [D7](#decisions) decision; (2) column width resolution (fixed, auto from header and visible rows, star) with tests; (3) rows on the virtual stack, each row one drawn node that paints text cells itself and hosts template cells; (4) horizontal scrolling with a frozen header and frozen columns as scroll links; (5) sorting through a sorted index view; (6) column resize with the splitter engine; (7) selection, keys by cell, table semantics; (8) leak scenario, gallery page, bench `skui-datagrid` (10,000 × 20), docs, changelog.

**Out of scope:** editing, grouping, filter UI, column reordering, export.

### F10 Backdrop blur

- **Size:** M. **Depends on:** F2 merged, [D8](#decisions). **Agent:** a stronger agent throughout.
- **Read first:** `SkUiRenderProps.cs`, the layer handling in `SkUiCompositor.cs` and `SkUiRenderRecorder.cs` (grep `_layerPaint`), `SkUiGpuWarmUp.cs`, [Performance.md](../Performance.md#drawing-effects).

Members on `SkUiView` and `SkUiCoreNode`: `BackdropBlurRadius` (`double`, default `0`: off) and `BackdropTint` (`Color?`, default `null`).

Steps: (1) a spike branch: one blurred bar over the effects stress list, measured on the Galaxy S9 and the iPhone 8, numbers into Performance.md. *Stop here unless [D8](#decisions) holds.* (2) the two composite-time properties; (3) the compositor draws the node through a save-layer with a backdrop blur, clipped to the node's shape; (4) fallback to the tint alone on software surfaces and under reduce transparency; (5) the immediate painter draws the fallback; (6) tests (output, fallback, no re-recording when content behind changes), gallery page, docs with "one or two per screen", changelog.

## What this plan leaves alone

The roadmap's own backlog (calendar, full SVG, MAUI parity members, off-thread recording, relayout boundaries, overlay masks) keeps its order. Tree view, shared-element transitions, staggered grids, signature pad and barcode generation were considered and left out; a snackbar can be folded into F3 if wanted.
