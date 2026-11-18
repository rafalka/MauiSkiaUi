# SkiaUi 1.14 implementation plan: performance

**Goal:** remove the allocations that remain on three paths: input events, frames that re-record, and first frames of large trees. No new control and no public behavior change. Wave 3 adds four performance items from the roadmap's backlog, one of which (text shadows) changes how something is drawn.

**Moved from 1.13** on 17 November 2026, when 1.13 became the calendar release ([ImplementationPlan-1.13.md](ImplementationPlan-1.13.md)). Wave 1 had started on `1.13-implementation`: A1 (the `Lock` gates) and A2 (the gesture snapshots, without its pooled-snapshot helper yet, so it did not build) were kept in a stash, `1.14 wave 1 WIP (A1 Lock gates, A2 gesture snapshots)`, to be applied on `1.14-implementation`. The facts below were checked at 1.12.0; check them again against 1.13.0 before each step, since the calendar adds a gesture recognizer and look entry points.

**Scope:** track A of the [review plan](ImplementationPlan-PerformanceAndControls.md) (16 November 2026), whose findings are agreed. Wave 1 (A1, A2, A5, A6) is planned in detail here. Wave 2 (A3, A4, A7) keeps its briefs in the review plan and gets its steps here once wave 1's numbers are in. Wave 3 (B1 to B4) comes from the [backlog](Roadmap.md#backlog-1x), added on 16 November 2026.

**Baseline:** 1.13.0 (net10 with MAUI 10.0.130 and net11 with MAUI 11.0.10, SDK 11.0.100, SkiaSharp 4.154.0).

**Rules:**

- 1.x keeps the 1.0 API. Wave 1 changes no public member: the API comparison against 1.13.0 must report nothing.
- Shared source stays C# 14.
- Every step is measured against `v1.13.0` with `scripts/bench.sh --baseline v1.13.0` (and `--quiet-gc` when one phase jumps while others drop). A step that makes a phase slower is reverted or explained.
- Each allocation fix comes with a test that fails without it, written as `PerformanceTests.CoreWrapAndShrinkRelayoutAllocatesNothing` is: a warm-up, then the fewest bytes of three batches (`GC.GetAllocatedBytesForCurrentThread`), so the runtime's own occasional work does not fail it.
- 0 warnings on `net10.0` and `net11.0`; the headless suite and the leak scenarios (`MemoryLeakTests`) pass on both.

## Decisions

- **D1 1.14.0 is a performance release** (agreed with the review): the allocation pass plus wave 3. The calendar (1.13.0) is the one new control before it; the review's controls start in 1.15.
- **D2 Wave 1's four steps are independent** and can be built in parallel, one branch each, merged into `1.14-implementation` in any order. None edits a file another edits.
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

**Status: done** on `1.14-implementation` (17 November 2026), from the stash plus `SkUiListSnapshot`. A2's tests are in `GestureAllocationTests` (hover is measured between two Core nodes: entering and leaving a drawn `SkUi*` view allocates MAUI's `PropertyChanged` args for `IsPointerOver`, outside the router); A5 shipped step 1 only (step 2 measured no faster); A6's bound is 520 bytes per cached load through a Core image (736 at 1.13.0, `ImageAllocationTests`). Numbers: [Benchmarks.md](Benchmarks.md#114-wave-1-17-november-2026).

### A1 `System.Threading.Lock` for the object gates

Size: S.

1. Change the ten fields listed above from `object` to `Lock` (`private readonly Lock _renderLock = new();`, `private static readonly Lock Gate = new();`). The `lock` statements stay as written: on a `Lock` the compiler emits `EnterScope`.
2. Check each gate is used only in `lock` statements (`rg -n '\b_pendingLock\b'` and so on). A gate passed as `object` or used any other way stays an `object`, with a comment saying why.
3. Leave the four collection gates of [D3](#decisions).
4. Build both lines, run the suite, then `scripts/bench.sh --baseline v1.13.0 -S core-buttons,skui-buttons,core-labels,skui-labels`.

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
4. Bench: `scripts/bench.sh --baseline v1.13.0 -n 10 -r 3 -S core-labels,skui-labels`; report the measure phase.

**Done when:** the paths chosen are unchanged (the border tests), and the measure phase of the label scenarios is no slower. If it is not measurably faster either, keep step 1 (it removes a dictionary lookup per character) and drop step 2.

### A6 Image memory-cache hits

Size: S.

1. In `SkUiImageLoader.Load`, compute the source key and the cache key, ask `SkUiImageMemoryCache.TryGet`, and only then build `SkUiImageLoadContext` for a miss. The transformations snapshot is part of the key, so it stays before the lookup.
2. Return `ValueTask<SkUiImageLoad>` from `LoadAsync`: a hit completes synchronously with the origin already set, so `WithOrigin`'s state machine runs for misses only. Adjust `SkUiImageSlot` (the one caller) to take the synchronous result without awaiting when `IsCompletedSuccessfully`.
3. Tests: with an image already in the memory cache, 100 loads through a slot allocate a fixed number of bytes per load; measure before the change and assert the new bound. The image tests (`ImageLoadingTests`, `ImageSourceParityTests`) and the leak scenarios pass unchanged; a lease is still taken and released per view.
4. Bench: the image and icon scenarios (`scripts/bench.sh --list`, then the ones that bind cached images), allocated KB.

**Done when:** a memory-cache hit allocates no `Task` and no load context, and the lease counts in the existing cache tests are unchanged.

## Wave 2

Briefs: [A3](ImplementationPlan-PerformanceAndControls.md#a3-pooled-render-batches-and-updates), [A4](ImplementationPlan-PerformanceAndControls.md#a4-shared-scratch-paints), [A7](ImplementationPlan-PerformanceAndControls.md#a7-toolchain-and-api). Steps written on 17 November 2026, after wave 1's numbers; the facts below were checked against `1.14-implementation` then.

Order: A3, then A4 (independent files, done one after the other on one branch), then A7, whose numbers describe the result. Each step is measured against wave 1's last commit with `scripts/bench.sh --baseline <commit>`, and the release table against `v1.13.0`.

### Facts that shape wave 2

- **Batches.** `SkUiRenderRecorder.Sync` allocates an `SkUiRenderBatch` (with two `List`s) on every frame it runs, including frames where nothing changed: it returns `null` then and the batch is garbage. Each changed node gets a new `SkUiRenderUpdate` (`Node` is a get-only constructor property). `SkUiFrameRenderer` is the only caller of `Sync` and the only one that commits; the compositor reads a batch only inside `Apply` (`ApplyPending` swaps `_pending` and `_applying`, applies, clears). Batches that are never applied go through `DiscardWithAnimations` (commit after dispose, `Dispose`).
- **Children pass.** `state.CommittedSources = _scratch.GetRange(start, count).ToArray()` allocates a `List` and an array; the array is kept, the list is not. The `SkUiRenderNode[]` sent with a children pass becomes `node.Children` on the render thread and stays: it cannot be pooled.
- **Transitions.** `toggle-transitions` (48 switches and 48 check boxes) is a device-only scenario; headless frames are driven with `SkUiTestHelpers`' frame helper (`Renderer.RequestFrame`, `Frame(time)`).
- **Paints.** 33 `using var … = new SKPaint` sites; out of the brief's skip list 23 remain: 12 in `SkUiLook.cs`, 2 each in `DefaultSkUiLook.cs`, `SkUiCoreTable.cs` and `SkUiMauiContentView.cs`, 1 each in `SkUiBorderGeometry`, `SkUiButtonFill`, `SkUiShapePainter`, `SkUiSliderInput` (a dimming paint, only when disabled) and `SkUiStrokedOutlines`. `DefaultSkUiLook.Paint(color)` (`[ThreadStatic]`, `Reset()` per use) and `SkUiShapePainter._paint` are the patterns.
- **API.** No `LangVersion` is set. The public `params T[]` members: `SetStrokeDashArray` (`SkUiShape`, `SkUiBorder`, `SkUiCoreShape`, `SkUiCoreBorder`), `SetPoints` (`SkUiPolyShape`, `SkUiCorePolyShape`), `SetTransformations` (`SkUiImage`, `SkUiCoreImage`), the `SkUiCorePolygon` / `SkUiCorePolyline` constructors and `SkUiImageCacheKeys.IgnoreQueryParameters`. The API check is `scripts/api_compat.sh` (`-p:SkUiApiBaseline=1.13.0`).

### A3 Pooled render batches and updates

Size: M. Edits `SkUiRenderRecorder.cs`, `SkUiCompositor.cs`, `SkUiFrameRenderer.cs`.

1. **Measure first.** An allocation test in `RenderAllocationTests`: a Core tree of 50 boxes whose `TranslationX` changes every frame (props only, nothing re-recorded), 60 frames through the frame helper after a warm-up; and the same tree with no change (idle frames). Record bytes per frame for both before any change.
2. **Children pass:** copy `CollectionsMarshal.AsSpan(_scratch).Slice(start, count).ToArray()`.
3. **Pool:** the compositor owns a small pool of batches (a `Lock` and a fixed array of at most 4; a lock-free stack would allocate a node per push, and the pool is touched twice per frame). After `Apply` it clears each applied batch and returns it; `Sync` takes a batch from the pool and gives it back itself when nothing changed, so an idle frame allocates nothing. A batch keeps its cleared `SkUiRenderUpdate`s in a spare list (at most 256) that the recorder refills before allocating, so updates travel with their batch and the pool is entered once per frame on each side. `SkUiRenderUpdate.Node` becomes settable (internal type).
4. **Clearing** sets every reference to `null` (node, pictures, children, props' effects by assigning `default`) without disposing anything: after `Apply` the compositor owns what was committed, and the `Disposed`-node branch of `Apply` keeps disposing the pictures it disposes today. Discarded batches (`DiscardWithAnimations`) are not pooled.
5. **Tests:** the allocation test asserts the new bounds (idle: 0 bytes; moving: the per-frame floor, with what remains named in a comment); `PipelineTests`, the rendering, transition and animation tests and `MemoryLeakTests` pass unchanged; a test that a pooled batch keeps no picture, node or effect alive (a weak reference to a replaced picture's owner is collected).
6. **Bench:** the `*-update` scenarios and `skel-*-wave` against wave 1, with `--quiet-gc`.

**Done when:** idle frames allocate nothing, a props-only frame allocates no batch or update, and nothing in the suite or the leak scenarios changes.

**Status: done** (`6d81c16`). Idle frames 408 → 0 bytes, moving 50 nodes 14,720 → 0 bytes per frame (`RenderAllocationTests`). Two allocations outside the brief turned up while measuring and are fixed with it: a new `PresentFrame` delegate per frame request, and `RenderCore`'s reports closure, allocated on every frame because its lambda captured a local. Bench against wave 1 with `--quiet-gc`: all rows within noise.

### A4 Shared scratch paints

Size: S to M. Edits the files listed in the facts; alone on `SkUiLook.cs`.

1. One `[ThreadStatic]` scratch per painter family, reset (`Reset()`) and set at every use: `SkUiLook` (its own private helper, as `DefaultSkUiLook.Paint`), `DefaultSkUiLook` (the existing `Paint`), and one each for the helpers that need it. A site that holds the scratch while it calls into another method of the same family (a nested `Draw…`) takes a second scratch (`Paint2`), never the first.
2. Debug and once-per-geometry sites stay as they are: `SkUiMauiContentView`'s placeholder drawing and `SkUiStrokedOutlines` (cached outlines). Say so in a comment only where the reason is not obvious.
3. Tests: `ControlParityTests`, the look, toggle, slider and table tests pass unchanged (pixel output is the test: a leaked shader, path effect or blend mode shows there). Add an allocation test: re-recording a switch, a slider and a token chip 100 times allocates no `SKPaint` (bytes per recording before and after, a bound).
4. Bench: `core-stress-buttons`, `skui-stress-buttons`, and the scenario that draws sliders, steppers and tokens (add one through `TrySet` if none exists): record time and allocated KB.

**Done when:** no per-call `new SKPaint` is left in the 20 sites outside step 2, and no pixel test changes.

**Status: done.** What changed from the steps: one shared helper, `SkUiScratchPaint`, instead of a scratch per family and a `Paint2` rule. Several of these paints are passed to protected virtual methods (`DrawActivityIndicator`, `DrawPasswordRevealGlyph`), and a subclass's override can call back into the look while one is held, which a fixed second scratch does not cover. The helper is a per-thread stack of 8 paints rented with `using` and reset on return, so every nested rental gets its own paint. `SkUiMauiContentView`'s site draws a native view's snapshot on every recording while one is shown (not debug drawing), so it was converted; `SkUiStrokedOutlines.Compute` (a cache miss) keeps its own paint. 22 sites converted. `PaintAllocationTests`: seven look and painter draws 88 → 0 bytes per call, plus the helper's nesting, reset and depth. New bench scenario `skui-steppers`: 172 KB less per iteration; with `--quiet-gc`, `record` -6.9 %, `update` -4.7 % (within noise); the stress-button and avatar scenarios unchanged.

### A7 Toolchain and API

Size: M. After A3 and A4.

1. `<LangVersion>14</LangVersion>` in `Directory.Build.props`, with the reason (both lines compile the same language; no C# 15 syntax can reach the net11 build alone). Build both lines and the demo.
2. `params ReadOnlySpan<T>` overloads beside each `params T[]` member in the facts (the array overloads stay; a call with an array still binds to them). `MauiApiParityTests` passes and `scripts/api_compat.sh` against 1.13.0 reports additions only. Tests: each overload sets the same value as the array overload. Docs: the controls' pages list the overload.
3. Runtime-async trial on net11 targets only (`<Features>runtime-async=on</Features>`): the image and icon scenarios with `--net 11` (allocated KB and timings) and the packed package size, with and without. Kept only if allocations drop and the package does not grow ([D9](ImplementationPlan-PerformanceAndControls.md#decisions)); the numbers go to Benchmarks.md either way.
4. Performance.md on .NET 11 devices: the scripts and empty tables are prepared with B4; a person runs them.

**Done when:** steps 1 to 3 are in with their numbers, and step 4 is on the verification backlog or done by a person.

**Status: steps 1 to 3 done; step 4 on the verification backlog.** C# 14 for every project (library, tests, benchmarks, demo and samples build on net11 with 0 warnings). Span overloads for the eleven members (`SpanParamsOverloadTests`); `scripts/api_compat.sh --baseline 1.13.0` reports no differences. Runtime async: the assembly 1.3 % smaller, allocations and timings unchanged, so not kept (numbers in [Benchmarks.md](Benchmarks.md#114-runtime-async-trial-17-november-2026)).

## Wave 3: from the backlog

Independent of waves 1 and 2 except where noted. B2 is the only step in 1.14 that changes pixels.

### B1 Re-shaping an edited paragraph without new arrays

Size: S to M. After A5 (same shaping code).

Facts: `SkUiTextEditLayout` keeps paragraphs by content, so an edit re-shapes one paragraph per keystroke. That paragraph gets a new `ParagraphLayout`, new line arrays, a new `SkUiShaping.Paragraph` (with its `Advances` array) and a new text blob ([TextInput.md](TextInput.md): grep "allocation-free").

1. Measure first: an allocation test typing 100 characters into a `SkUiCoreEntry` with one paragraph of 80 characters; record bytes per keystroke and what they are (a memory profile or by elimination).
2. Reuse the edited paragraph's buffers: keep the replaced `ParagraphLayout`'s arrays (advances, caret edges, line ranges) and refill them when the new text fits, growing by doubling when it does not. Buffers belong to the layout instance, never to a shared static.
3. The text blob is rebuilt (its glyphs changed); the string is the app's. State the floor that remains in the test's comment.
4. Tests: the allocation test with the new bound; `TextEditLayout` tests and the entry and editor tests unchanged; bidi, surrogate pairs and a paragraph that grows past its buffers.
5. Bench: `core-entries-update` and `skui-entries` against `v1.13.0`.

**Done when:** bytes per keystroke drop to the floor named in step 3 and no text-input test changes.

**Status: done.** Reused: the paragraph and line objects and the caret-edge arrays (the edit layout's own). One spare paragraph, kept in a field, its lines refilled in place; reused arrays grow 16 entries past need. A first version kept two stacks per layout and doubled the arrays: the entry scenarios, which set each text once, then allocated 78 to 187 bytes more per entry, so it was reworked to cost nothing when nobody types (+8 bytes per entry, the field). Per edit (`TextEditAllocationTests`): plain 2,580 → 1,596, shaped 3,228 → 2,244, simple 1,480 → 496 bytes; typing 100 characters on 3,816 → 2,523 bytes per keystroke. The floor is the shaping engine's paragraph and runs (glyph, position and cluster arrays sized by their length, read through `.Length` by every text path), the shaped line and its blob, and the paragraph's string when the text has several: lowering it means runs that carry their length apart from their arrays, a change to the shared engine left for later. A reused layout gives the same lines, edges and carets as a fresh one through growth, wrapping, bidi, surrogate pairs and paragraph edits. Bench against wave 2 (`core-entries`, `core-entries-update`, `skui-entries`): within noise.

### B2 Text shadows cast on the GPU

Size: M. A stronger agent; alone on `SkUiRenderShadow.cs` and the compositor's shadow code, so after A3.

Facts: a shadow has two paths ([RenderingPipeline.md](RenderingPipeline.md): grep "Content |"). An opaque fill casts from its outline: blurred directly, cheap. Anything else (text, images, translucent shapes) casts from content: the compositor rasterizes the blurred alpha of the node's body into a per-node raster once the subtree is stable, at most `MaxShadowRasterizationsPerFrame` (3) per frame, on the CPU. On the Galaxy S9 a scrolling list with a text shadow per card runs at 44 fps (14 ms per frame), because about 50 cards per second need a new raster ([Performance.md](../Performance.md#drawing-effects)).

1. Spike, on the S9 and the iPhone 8 with the effects stress page's "text shadow" row: (a) rasterize the content shadow into a GPU-backed surface of the frame's context instead of a CPU raster; (b) for a node whose content is text only, draw the text blob itself with the shadow's blur as a mask filter and the shadow's color, which Skia caches per glyph run. Record render time and fps for both. *Decide from the numbers; stop and report if neither reaches 55 fps on the S9.*
2. Implement the chosen path behind the existing `SkUiShadowPainter` seam. Path (b) needs the label to say its content is one text run (a flag in the committed shadow), and falls back to the content path for spans with inline objects, images and mixed content.
3. Software surfaces and the immediate painter keep today's path and must give the same pixels as before.
4. Resource rules: a GPU raster belongs to the compositor and its context; it is dropped on context loss and surface teardown (`ReleaseDrawingResources`), and counted in the shadow statistics (`ShadowRasterizations`, `LiveShadows`) so the existing tests still describe it.
5. Tests: shadow tests and the immediate-painter comparison pass; a pixel comparison of old and new output within a tolerance for a blurred text shadow at two radii and two densities; leak scenarios.
6. Docs: the shadow table in RenderingPipeline.md, the text-shadow row and recommendation in Performance.md with the new numbers, the roadmap item closed.

**Done when:** the S9 holds 55 fps or more on the text-shadow row, the iPhone 8 is no slower, and software output is unchanged. Device look checks (blur softness against the old output, on both phones) are done before release, not left on the verification backlog.

**Status: steps 1 to 6 done; the device look checks are left for before the release.** Spike, with a new device scenario (`text-shadow-scroll-*`, the stress page's row scrolled at its speed) and a temporary switch: (a) a GPU raster reached 56.7 fps on the S9 with 30 ms spikes; (b) the glyphs drawn with a blur mask filter reached 59.6 fps, about 1 ms per frame over no shadow, and allocated nothing more; the iPhone 8 held 60 fps with both (numbers: [Benchmarks.md](Benchmarks.md#114-text-shadow-spike-18-november-2026)). Chosen: (b). What changed from the steps: the glyphs are recorded on the UI thread into a picture committed with the shadow (`SkUiRenderShadow.Glyphs`, from `ISkUiShadowCaster.CreateTextShadow`), instead of a flag and the label's blob on the render thread: a picture holds its blobs and mask filter, so its lifetime follows the shadow's (the compositor disposes it as it does the outline), and the key that keeps a shadow (`PaintVersion`, bumped by every re-record, now also by skeleton changes) renews it with the text. Glyphs: a plain `SkUiLabel` or `SkUiCoreLabel` (not a subclass, which may draw more) with plain text and no background, chrome, background painter or clip shape, not shown as skeleton bones; everything else, and every raster canvas (software surfaces, Windows, the immediate painter), keeps the content raster and its pixels, so step 4's GPU raster bookkeeping was not needed (a `GlyphShadows` counter instead). Drawn unclipped by the node, the glyph shadow matches the raster within 4 of 255 levels, 10 for radius 3 at 1× (`TextShadowTests`: both layers, two radii, two densities; which labels qualify; the replaced picture released). Against B3 on the devices (`--baseline HEAD`): S9 43.2 → 59.7 fps, 15.5 → 4.6 ms per frame, 991 → 636 KB per window; iPhone 8 60 fps, 7.2 → 4.4 ms, 1,378 → 778 KB; the list without shadows unchanged. The leak scenarios pass headless and, for labels, effects, shimmers and surfaces, on the S9.

### B3 Overlay bookkeeping on Android

Size: S, after a measurement. Edits `SkUiViewHandler.cs`, so alone on that file.

Facts: the roadmap names "cheaper overlay bookkeeping on Android" without saying what is costly, and the review did not read this code. The overlay container and its placement live in `SkUiViewHandler.cs` and `SkUiMauiContentView.cs` ([SkUiMauiContentView.md](../controls/SkUiMauiContentView.md)).

1. Measure: a page with 20 hosted native views in a drawn scroll view; a frame trace during a scroll and an allocation count per frame on the Android path. Name the cost (per-frame layout passes, snapshot refreshes, list scans, allocations).
2. *Stop and report if nothing above 0.5 ms or 1 KB per frame is found:* the item is then closed in the roadmap with the numbers.
3. Otherwise fix the named cost only (cache what does not change between frames, skip overlays whose clip and frame are unchanged), with a headless test where the logic is platform-neutral and a device check where it is not.

**Done when:** the measurement is in [Benchmarks.md](Benchmarks.md) and the roadmap item is either closed or fixed.

**Status: done.** Measured with three new device scenarios (`hosted-scroll-drawn` / `-auto` / `-live`) and device `allocKB` (all threads, the motion window). The default mode (snapshots while scrolling) costs 0.2 KB per frame and no frame time, under the threshold; Live placement cost 6.9 KB per frame (four Android `Rect` objects per overlay per placement). Fixed in `SkUiViewHandler.cs`: whole-pixel rectangles as plain integers (`SkUiPixelRect`) on the clip view, no measure and layout for an unchanged placement, no search per clip view in the container's layout pass. After: Live allocates as much as the drawn reference. On the S9 the device tests' hosted-control check and 48 of 49 leak scenarios pass; `TextFieldsEdited` fails (the input method's `InsertText("!")` has no effect), the same with this change reverted and at v1.13.0, so it predates 1.14 on that device. Numbers: [Benchmarks.md](Benchmarks.md#114-overlay-bookkeeping-on-android-17-november-2026).

### B4 Device frame-time checks

Size: S. Needs a person with the Galaxy S9 and the iPhone 8, in the same session as A7's .NET 11 measurements.

From the [verification backlog](Roadmap.md#verification-backlog), "Controls: feel and frame time on devices": the collection view (fling at 10,000 items, memory), the virtual stack, the carousel, the swipe view, the refresh view, and a scrolling list of shimmering rows.

1. An agent prepares one scripted page per check in the demo's stress pages (or reuses the gallery page where it can be driven by `SKUI_TOUR`), each reporting fps, render time per frame and allocated KB.
2. A person runs them on both phones on .NET 11 and records the table in Performance.md.
3. The verification backlog's "Controls" item is cut down to what was not covered (presenters on Windows, screen readers, tooltips).

## Verification and release

- Headless: the suite and the leak scenarios on both lines after each step and after the merge.
- Bench: the full `scripts/bench.sh --baseline v1.13.0` table after wave 1 and again after wave 2, recorded in [Benchmarks.md](Benchmarks.md) (both done, 17 November 2026: [after wave 2](Benchmarks.md#114-waves-1-and-2-17-november-2026)).
- Devices: `scripts/device_tests.sh` on the Galaxy S9 and the iPhone 8 before release (leaks and a drag over a long list). Waves 1 and 2 change nothing that is drawn. B2 does: its look checks are part of the release.
- Release `1.14.0` when the three waves are in. B2 may delay it (accepted on 16 November 2026); if B2's spike fails, 1.14.0 ships without it and the roadmap item stays open with the spike's numbers.
