# SkiaUi 1.13 implementation plan: allocation pass

**Goal:** remove the allocations that remain on three paths: input events, frames that re-record, and first frames of large trees. No new control and no public behavior change.

**Scope:** track A of the [review plan](ImplementationPlan-PerformanceAndControls.md) (16 November 2026), whose findings are agreed. Wave 1 (A1, A2, A5, A6) is planned in detail here. Wave 2 (A3, A4, A7) keeps its briefs in the review plan and gets its steps here once wave 1's numbers are in.

**Baseline:** 1.12.0 (net10 with MAUI 10.0.130 and net11 with MAUI 11.0.10, SDK 11.0.100, SkiaSharp 4.154.0).

**Rules:**

- 1.x keeps the 1.0 API. Wave 1 changes no public member: the API comparison against 1.12.0 must report nothing.
- Shared source stays C# 14.
- Every step is measured against `v1.12.0` with `scripts/bench.sh --baseline v1.12.0` (and `--quiet-gc` when one phase jumps while others drop). A step that makes a phase slower is reverted or explained.
- Each allocation fix comes with a test that fails without it, written as `PerformanceTests.CoreWrapAndShrinkRelayoutAllocatesNothing` is: a warm-up, then the fewest bytes of three batches (`GC.GetAllocatedBytesForCurrentThread`), so the runtime's own occasional work does not fail it.
- 0 warnings on `net10.0` and `net11.0`; the headless suite and the leak scenarios (`MemoryLeakTests`) pass on both.

## Decisions

- **D1 1.13.0 is the allocation pass alone** (agreed with the review). New controls start in 1.14.
- **D2 Wave 1's four steps are independent** and can be built in parallel, one branch each, merged into `1.13-implementation` in any order. None edits a file another edits.
- **D3 Gates that lock on a collection stay as they are in A1.** `SkUiWeakListener` (`_listeners`), `SkUiWeakEvent` (`_entries`), `SkUiFontScaling` (`_systemSizes`) and `DefaultSkUiLook` (`Entries`) lock on the collection they guard. Moving them to a `Lock` adds a field per instance (a weak listener exists per subscription), so they are left unless a contention trace says otherwise.
- **D4 A6 changes internal signatures only.** `SkUiImageLoader.LoadAsync` and what it calls are internal; `SkUiImageSlot` is the one caller.
- **D5 No changelog entry per step.** One entry under `### Other` for the release: fewer allocations on pointer events, text measuring and cached image loads.

## Facts that shape the plan

Checked against the code on `devel` (16 November 2026, at 1.12.0).

- **Locks.** Ten gates are `object` fields taken with `lock`: `SkUiCompositor` (`_pendingLock` 3 sites, `_renderLock` 2), `SkUiFrameRenderer._sizeLock` (4), `SkUiMetalSurface` (`_frameLock` 3, static `Gate` 2), `SkUiImageCache.Gate` (7), `SkUiImageLoader.Gate` (5), `SkUiImageDiskCache.Gate` (7), `SkUiFonts.Gate` (3), `SkUiFrameTrace._gate` (3). `SkUiStrokedOutlines.Gate` and `SkUiImageSlot._gate` are `System.Threading.Lock` already. Nothing in the library calls `Monitor.` directly, so no gate depends on `Wait`, `Pulse` or `TryEnter`.
- **Gesture snapshots.** `SkUiGestureArena.Members` is a `List<SkUiGestureRecognizer>`. `ToArray()` snapshots it at five sites in `SkUiPointerRouter.cs` (accept, cancel, move / release, press, and one more arena pass), plus `_arenas.Values.ToArray()` in `CancelAll` and `_hovered.ToArray()` on hover changes. `SkUiGestureRecognizer` snapshots its `_arenas` list (capacity 1) at four sites, `SkUiScrollView` its overlays and listeners, `SkUiMauiTaps` its recognizers. The move / release loop runs once per pointer event, so a drag allocates an array per event. Every loop re-checks membership (`Members.Contains(member)`), because handlers remove members while it runs.
- **Text.** `SkUiShaping.IsPlain` runs per measured string and calls `HasGlyph` per character. `HasGlyph` does `Coverage.GetOrAdd(typeface, …)` (a `ConcurrentDictionary`) on every call before it reads the 64 K coverage table. Its two other callers, in the font fallback, pass code points one at a time and keep the current shape.
- **Images.** `SkUiImageLoader.Load` builds the `SkUiImageLoadContext`, snapshots the transformations and builds the cache key before it asks `SkUiImageMemoryCache.TryGet`; a hit returns `Task.FromResult`. `LoadAsync` wraps it in `WithOrigin`, an async method, so a hit allocates a context, a `Task` and a state machine result. `SkUiImageSlot` (line 227) is the only caller.
- **Tests and bench.** `PerformanceTests.cs` holds the allocation-test pattern. The bench reports allocated KB per scenario (`BenchReport`), so wave 1 needs no harness change.

## Wave 1

### A1 `System.Threading.Lock` for the object gates

Size: S.

1. Change the ten fields listed above from `object` to `Lock` (`private readonly Lock _renderLock = new();`, `private static readonly Lock Gate = new();`). The `lock` statements stay as written: on a `Lock` the compiler emits `EnterScope`.
2. Check each gate is used only in `lock` statements (`rg -n '\b_pendingLock\b'` and so on). A gate passed as `object` or used any other way stays an `object`, with a comment saying why.
3. Leave the four collection gates of [D3](#decisions).
4. Build both lines, run the suite, then `scripts/bench.sh --baseline v1.12.0 -S core-buttons,skui-buttons,core-labels,skui-labels`.

**Done when:** no `readonly object` field is left under a `lock` in the ten places, the suite is green on `net10.0` and `net11.0`, and no bench phase is slower.

### A2 Gesture arena without an array per pointer event

Size: S.

1. Add one internal helper in `MauiSkiaUi/Gestures`: a `ref struct` snapshot that rents from `ArrayPool<T>.Shared`, copies a `List<T>` through `CollectionsMarshal.AsSpan`, exposes the span, and on `Dispose` clears the rented range and returns it. One rent per loop keeps re-entrancy safe: a handler that opens another arena rents its own buffer.
2. Use it at the `ToArray()` sites of `SkUiPointerRouter`, `SkUiGestureRecognizer`, `SkUiMauiTaps` and `SkUiScrollView` (`using var snapshot = …; foreach (var member in snapshot.Span)`). Keep every existing membership re-check.
3. `_arenas.Values.ToArray()` in `CancelAll` runs when a surface detaches, not per event: leave it.
4. Tests in `PerformanceTests`: after a warm-up, 100 moves of one pressed pointer over a tree with a tap and a pan recognizer allocate 0 bytes; the same for hover moves over two nodes. Add a re-entrancy test: a recognizer that cancels its arena from `OnPointerMoved` leaves the router consistent.

**Done when:** the allocation tests pass, every gesture, scroll and swipe test passes unchanged, and the leak scenarios pass (a snapshot must not keep recognizers alive: the buffer is cleared on return).

### A5 Text plain-path check

Size: S.

1. In `SkUiShaping.IsPlain`, fetch the coverage table once (`Coverage.GetOrAdd`) and test each character against it in the loop, with the lazy fill for unknown entries moved into a small local function. `HasGlyph(typeface, cp)` stays for the fallback callers and uses the same fill.
2. Before the loop, reject text with a character outside `IsSimpleChar`'s ranges in one vectorized pass: a `static readonly SearchValues<char>` built from those ranges and `text.AsSpan().ContainsAnyExcept(…)`. The ranges stay defined in one place (build the set from the same constants `IsSimpleChar` uses).
3. Tests: the shaping and label tests pass unchanged; new cases at the range borders (U+007E, U+007F, U+00A0, U+017F, U+0180, U+2010, U+2027, U+20C0, a surrogate pair) take the same path as before the change.
4. Bench: `scripts/bench.sh --baseline v1.12.0 -n 10 -r 3 -S core-labels,skui-labels`; report the measure phase.

**Done when:** the paths chosen are unchanged (the border tests), and the measure phase of the label scenarios is no slower. If it is not measurably faster either, keep step 1 (it removes a dictionary lookup per character) and drop step 2.

### A6 Image memory-cache hits

Size: S.

1. In `SkUiImageLoader.Load`, compute the source key and the cache key, ask `SkUiImageMemoryCache.TryGet`, and only then build `SkUiImageLoadContext` for a miss. The transformations snapshot is part of the key, so it stays before the lookup.
2. Return `ValueTask<SkUiImageLoad>` from `LoadAsync`: a hit completes synchronously with the origin already set, so `WithOrigin`'s state machine runs for misses only. Adjust `SkUiImageSlot` (the one caller) to take the synchronous result without awaiting when `IsCompletedSuccessfully`.
3. Tests: with an image already in the memory cache, 100 loads through a slot allocate a fixed number of bytes per load; measure before the change and assert the new bound. The image tests (`ImageLoadingTests`, `ImageSourceParityTests`) and the leak scenarios pass unchanged; a lease is still taken and released per view.
4. Bench: the image and icon scenarios (`scripts/bench.sh --list`, then the ones that bind cached images), allocated KB.

**Done when:** a memory-cache hit allocates no `Task` and no load context, and the lease counts in the existing cache tests are unchanged.

## Wave 2 (outline)

Briefs: [A3](ImplementationPlan-PerformanceAndControls.md#a3-pooled-render-batches-and-updates), [A4](ImplementationPlan-PerformanceAndControls.md#a4-shared-scratch-paints), [A7](ImplementationPlan-PerformanceAndControls.md#a7-toolchain-and-api).

- **A3** pooled render batches and updates, after A1 (same file).
- **A4** shared scratch paints in looks and painters, alone on `SkUiLook.cs`.
- **A7** `LangVersion` 14, `params ReadOnlySpan<T>` overloads (the release's only API additions), the runtime-async trial on net11, and Performance.md measured again on .NET 11 devices.

## Verification and release

- Headless: the suite and the leak scenarios on both lines after each step and after the merge.
- Bench: the full `scripts/bench.sh --baseline v1.12.0` table after wave 1 and again after wave 2, recorded in [Benchmarks.md](Benchmarks.md).
- Devices: `scripts/device_tests.sh` on the Galaxy S9 and the iPhone 8 before release (leaks and a drag over a long list). Nothing in wave 1 changes what is drawn, so no look checks are added to the verification backlog.
- Release `1.13.0` when both waves are in; if wave 2 slips, wave 1 can ship by itself.
