# SkiaUi benchmarks

Measure performance **before and after a change**, for any component, in a way that an AI agent and a human run the same way. The demo app's Stress page stays a human-facing MAUI / SkUi* / Core showcase. Benchmarks live in a separate app and runner that share one scenario catalog.

## Two tiers

| Tier | Where | What it measures | Use it for |
| --- | --- | --- | --- |
| **Headless** (`benchmarks/MauiSkiaUi.Benchmarks`) | `net10.0` console app, any dev machine or CI | `generate` (build tree), `measure`, `arrange`, `record` (UI-thread picture recording of the first frame), `composite` (render-thread compositing into an offscreen raster), `update`, `updatePictures` (pictures the update's frame re-records), `allocKB` (managed bytes) | Fast inner loop (≈ 10 s), layout / text / recording regressions, allocations |
| **Device** (`benchmarks/MauiSkiaUiBench`) | Release MAUI app on Android / iOS / Mac Catalyst | `generate`, `add` (attach to the page: handler creation), `firstFrame` (attach → first composited frame: layout, recording and compositing on the real surface), `update` (change → next composited frame), `motionFps` / `motionAvgRenderMs` / `motionMaxRenderMs` (render-thread frames while animating), `motionUiFps` / `motionAvgUiMs` / `motionMaxUiMs` (UI-thread animation frames: clock tick, recording and commit), `allocKB` (what every thread allocated during the motion), `heapKB` (the managed heap after the motion and a full collection, with the scenario still shown) | Authoritative numbers: JIT/AOT, GPU surfaces, vsync pacing, real text stacks |

All values are **milliseconds** (except `motionFps`, `motionUiFps`, `updatePictures`, `allocKB` and `heapKB`). Reports show the **median** of the measured iterations; each scenario first runs `--warmup` iterations that are discarded.

On iOS and Mac Catalyst the app forces UIKit layout (`LayoutIfNeeded`) right after attaching or updating. Since iOS 18, UIKit lays out in an update cycle aligned to the display refresh. Without forcing it, `firstFrame` would include a 0–16 ms wait that depends only on where the benchmark's continuation falls in that cycle. Native views pay that wait too. The update and motion phases start after a 100 ms idle gap: a change right after a frame waits for the next vsync, which is correct pacing but not what `update` should measure.

The device app is always built **Release**: no DevFlow and no profiler. Debug numbers exaggerate managed-code cost several times and must not be compared with Release numbers. Headless numbers miss GPU, AOT and platform text costs. Treat them as a signal, and confirm important changes on a device.

## Running

```bash
# Headless, all scenarios, working tree only
scripts/bench.sh

# Uncommitted changes vs the last commit (the everyday "did I make it slower?" check)
scripts/bench.sh --baseline HEAD

# The same, with no garbage collection inside an iteration (a phase that jumped: code or collection timing?)
scripts/bench.sh --baseline HEAD --quiet-gc

# Device: a branch vs master on a connected Android phone, selected scenarios, 8 runs
scripts/bench.sh -t android -s 2299011508047ece --baseline master -S core-labels,skui-labels -n 8

# iOS simulator (UDID from `xcrun simctl list devices`); a physical iPhone UDID uses devicectl (iOS 17+), or mlaunch
# from the iOS workload for a 40-hex-digit UDID (iPhone X and older, iOS 16 at most)
scripts/bench.sh -t ios -s A7AC4C38-0DC8-471D-831B-7B3C9452BB76

# An iPhone on iOS 16 when the selected Xcode no longer has its developer image: launch through an Xcode that does
SKUI_MLAUNCH_SDKROOT=/Applications/Xcode-26.6.0.app/Contents/Developer scripts/bench.sh -t ios -s <40-hex-digit-udid>

# With the platform's look current, as an app built with DefaultSkUiLook.ForCurrentPlatform() (any SkUiLook type name)
scripts/bench.sh -t ios -s bd3d146c5a1990681dc15b4df632a85236005695 --look IosSkUiLook -S skui-stress-buttons,spinners

# Mac Catalyst
scripts/bench.sh -t maccatalyst

# Scenario list
scripts/bench.sh --list
```

`--baseline REF` checks REF out into a git worktree (`artifacts/bench/worktrees/<sha>`) and copies the working tree's `benchmarks/` folder over it. Both sides then run **identical scenarios** against their own library. The script builds both sides and runs baseline, then current. `-r N` alternates them N times and pools the samples, which reduces order and thermal bias on phones. It then prints a comparison:

```
baseline → current (android, threshold 5%)
scenario               metric              baseline   current     Δ %  verdict
core-labels            firstFrame              73.1      78.6    +7.5  ~
core-labels-update     update                  67.4      50.8   -24.6  faster
```

- A verdict is **faster** or **slower** only when all three conditions hold:
  - the medians differ by at least `--threshold` percent (default 5);
  - for timings, they also differ by at least 0.25 ms;
  - the interquartile ranges do not overlap.
- Anything else is `~` (within noise).
- **Garbage collections move between phases.** Each headless iteration starts after a full collection, but a scenario of
  1,000 controls allocates megabytes, so gen0 collections happen inside it, and each one promotes the live tree (a few ms).
  A change that allocates a little more or less moves a collection into another phase: one phase looks much slower
  (`record` +70 %) and another faster, with no code path changed. When a single phase jumps like that, compare again
  with `--quiet-gc` (a gen0 budget large enough that no collection lands inside an iteration): it compares code paths
  only. Allocations are still reported (`allocKB`); judge them on their own.
- The script exits 0 either way. Read the table, or run `scripts/bench_compare.py A.json B.json --json` for machine output, or `--markdown` for PR descriptions.

Output goes to `artifacts/bench/<timestamp>/` (git-ignored):

| File | Contents |
| --- | --- |
| `current.json`, `baseline.json` | All samples plus metadata; re-compare any two files later with `scripts/bench_compare.py` |
| `compare.txt` or `summary.txt` | The comparison table, or the medians when there is no baseline |
| `*.log` | Raw device output and build logs |

### Without the script

- **Headless:**
  ```
  dotnet run -c Release --project benchmarks/MauiSkiaUi.Benchmarks -- --runs 6 --scenarios core-labels --json out.json
  ```
  The script also sets `DOTNET_TieredCompilation=0`, so early iterations are not penalized by tiered JIT. With `SKUIBENCH_RETAINED=1` the runner also prints the managed heap each tree keeps after its first frame and after its update (`SKUIRETAINED` lines). It collects fully between phases, so its timings are not comparable, and it is exact only for scenarios that keep nothing of the previous iteration's tree (the `skel-*` ones).
- **Human on a device:** deploy `benchmarks/MauiSkiaUiBench` in Release from the IDE. Type scenario names, or leave the box empty for all, then press **Run**. The median table appears on screen.
- **Android launch extras:**
  ```
  adb shell am start -n com.rkdevel.skiauibench/skiauibench.MainActivity --ez autorun true --es scenarios core-labels,spinners --ei runs 6 --ei warmup 1 --es label mine
  ```
  Results are written to logcat.
- **Apple launch arguments:** `--autorun --exit --runs 6 --scenarios core-labels`, for example via `xcrun simctl launch --console`.

Both runners print one `SKUIBENCH {json}` line per sample, `SKUIBENCH_TABLE` lines, and `SKUIBENCH_DONE`. Problems are printed as `SKUIBENCH_WARN` or `SKUIBENCH_ERROR`; the device app warns when the previous iteration's root is still alive after it was removed and the heap collected (a leak that would make `heapKB` climb from iteration to iteration). `bench_compare.py collect` turns any such log into a results file.

## Scenarios

The catalog lives in [benchmarks/Scenarios/Scenarios.cs](../../benchmarks/Scenarios/Scenarios.cs), and both tiers link it. Scenarios marked `[device]` only run in the app: they need a real surface, motion or a native control.

| Scenario | Covers |
| --- | --- |
| `core-labels`, `skui-labels` | 1,000 plain labels in a 2-column grid (Core vs SkUi* layer) |
| `core-buttons`, `skui-buttons` | 1,000 buttons (the Stress page workload) |
| `skui-stress-buttons`, `core-stress-buttons` | The same buttons as the Stress page builds them: text color, fill, border and a command (click handler) each |
| `mixed-script-labels` | Arabic / Hebrew / Devanagari / emoji / CJK: HarfBuzz shaping, bidi, font fallback |
| `core-labels-simple` | Numeric labels with `TextRendering = Simple` (no shaping; `Auto` shapes plain Latin as one run since 1.4) |
| `core-labels-update`, `skui-labels-update` | Steady state: change every label's text (re-layout + re-record) |
| `core-labels-fit`, `skui-labels-fit` | Labels with `ShrinkToFit` and `AllowsTightening` whose texts fit, shrink a little, shrink a lot and do not fit at the minimum; update = new texts, fitted again. Libraries without the options draw them truncated, so a comparison with one is the cost of fitting (the options are set by reflection, which adds to `generate`) |
| `core-labels-fit-idle` | The same options on texts that fit: the cost of having them on |
| `core-labels-grow` | Numbers with `GrowToFill` (up to 3×) filling their cells; update = new numbers |
| `skui-icons-svg`, `-png`, `-font`, `core-icons-svg`, `-png`, `-font` | 1,000 of eight 24 dp icons, about 50 per screen, as an SVG, a PNG of the screen's pixels, or a font glyph; update = a new color on all (a theme change's work on the UI thread); motion = animated scroll |
| `icon-load-svg`, `-png`, `-font` | 200 icons loaded uncached off the UI thread. Run each alone: after a large tree, memory pressure changes the numbers |
| `icon-first-svg`, `-png`, `-font` | The first icon of the process: run alone, in a new process, with `-n 1 -w 0` |
| `skui-images`, `core-images` | 1,000 images (eight 512 px photos written as PNG files to the temp folder, `AspectFill`) in a two-column grid of each layer; update = the next photo in each. The first iteration decodes into the memory cache, later ones are cache hits. `-viewsized`: with `DownsampleToViewSize` (set by name; a no-op on older libraries), whose cache hits happen at the arrange |
| `markdown-document` | A 100 KB document in a scroll view (`SkUiMarkdownView`, building in place so a frame has its content): parse, build, measure, record; motion = an animated scroll across it. On a baseline without the package: an empty scroll view |
| `markdown-append` | The same document; update = a few words appended to the last paragraph, as a stream does. On a baseline without the package, an update replaces the empty content, so its number means nothing |
| `skui-avatars` | 1,000 `SkUiAvatarView` with initials colored by name; update = new names (re-record, no layout). Libraries without the control build a round `SkUiBorder` around an `SkUiLabel`, so a baseline from before C8 compares one drawn node against the composition |
| `skui-steppers` | 1,000 `SkUiStepper` in a two-column grid; update = toggle `IsEnabled` (every stepper re-records its look-drawn chrome) |
| `shimmer-page` | 60 placeholder rows of Core bones (180 bones) in one `SkUiCoreShimmer`, wave effect; update = the bones resized (re-layout and re-record of the bones, the effect kept); device motion = the wave (frame stats). Without the shimmer (a baseline from before C9) the same bones stand still, so the comparison is the shimmer's own cost: about +0.4 ms to composite a frame on headless raster (the group layer, the tint and the band), nothing recorded per frame |
| `shimmer-rows` | The same rows, a `SkUiShimmer` per row (a layer and a bindable view per row); compare with `shimmer-page` for one host against a shimmer per row. Galaxy S9 (2026-10-14, Release): both at 60 fps while the wave runs; render thread per frame 2.4 ms average / 7.2 ms worst for one host, 5.7 / 9.6 ms for a shimmer per row (the bones standing still: 1.9 / 2.1 ms); building 1.6 ms against 12.4 ms. Unchanged after the shimmer moved to drawables (2.5 / 7.4 ms, 5.5 / 9.8 ms) |
| `shimmer-bones` | 60 white bordered cards (chrome that must not shimmer) with five Core bones each in one `SkUiCoreShimmer` with `Mode = Bones`: the bones draw themselves with no layer; update = the bones resized; device motion = the wave (frame stats). Without bones (a baseline from before them) the same cards are boxes under a silhouette shimmer (one layer, the cards tinted too), so the comparison is bones against one layer. Headless raster: compositing 0.3 against 0.6 ms. Galaxy S9 (2026-10-14, Release, two rounds): both at 60 fps, render thread 2.6 ms average for both, 8.8 / 7.4 ms worst (within noise). A first version that clipped the band to each bone cost 2.9 ms; drawing each crossed bone as one rounded rectangle with the bone color and the band in one shader brought it to the layer's cost. Building: 3.8 against 2.6 ms, mostly the scenario creating the bones by reflection (it compiles against libraries without them) |
| `shimmer-content` | 60 white bordered cards (an avatar circle, a bold name, a two-line message, a value) as their own skeleton in one `SkUiCoreShimmer` without a skeleton of its own, `Mode = Bones` (C9b): the labels draw a bar per line and the circle its shape, the cards as they are; update = new message texts (new bars; nothing recorded for the labels); device motion = the wave (frame stats). Before C9b the same cards draw themselves, so a baseline compares entering the skeleton with drawing the content: headless raster, recording 0.9 against 0.7 ms (each control's bones made once), allocations +250 KB (the bones and their drawables), compositing the same |
| `skel-content-*`, `skel-static-*` (`skel-skui-*` on the SkUi\* layer) | The same 60 feed cards loading, with the skeleton made from the content (C9b) or by hand from bones laid out the same way (the real content is built too, as a page does). `-loaded`: the data arriving times the switch (texts set, `IsLoading = false`, the effect standing still so the update's own frame is timed); `-wave`: the wave while loading (device frame stats; `-silhouette` in a silhouette). Galaxy S9 (2026-10-14, Release): building 3.0 against 6.0 ms (Core), 44 against 111 ms (SkUi\*, a second tree of bindable views); the switch 64 against 74 ms (Core, within noise), 77 against 135 ms (SkUi\*: the content is laid out already); first frame within noise; 60 fps and 1.4–2.9 ms per frame for both. Managed heap (headless, `SKUIBENCH_RETAINED=1`): 2.2 against 3.8 MB while loading, 1.6 against 3.3 MB after (Core); 8.7 against 17.4, 7.5 against 16.6 MB (SkUi\*): a hand-made skeleton stays with the shimmer after loading |
| `skel-tax-on`, `skel-tax-off` (`skel-tax-skui-*`) | What a content skeleton costs views outside it: 1,000 labels eight layouts deep change their texts beside a one-card content skeleton loading (`on`) or loaded (`off`). The first C9b version had every node walk up to its root per recording while any shimmer showed its content: headless recording +26–28 % (`on` 2.0 against `off` 1.6 ms Core, 3.2 against 2.4 ms SkUi\*); on a Galaxy S9 within run-to-run noise (+2 to +16 ms of a 90 ms first frame over three runs). With each node's place in a skeleton set from the top, `on` and `off` are the same (headless recording 1.6 / 1.6 ms Core, 2.5 / 2.5 ms SkUi\*); Galaxy S9 (2026-10-14, Release, three rounds against the walk): every phase within noise, building included |
| `scroll-fling` `[device]` | Render-thread `AnimateScrollTo` through 400 buttons |
| `hosted-scroll-drawn` / `-auto` / `-live` | Device only: 80 buttons in a scroll view with 20 slots, filled with drawn boxes (the reference) or `SkUiMauiContentView`s hosting a native `Entry` in `ScrollMode` Auto (snapshots while scrolling on Android and Windows) or Live (placed every frame); motion = `AnimateScrollTo` to the end. Compare `allocKB` and the motion stats with the drawn reference for what the overlays cost |
| `text-shadow-scroll` / `-none` | Device only: the Effects stress page's text-shadow row, 400 cards (avatar, title, subtitle) with a shadow on each title (black, offset 1, 2, radius 3, 35 %) or none (the reference), scrolled at the page's speed (the list in 6 s), sampled for 4 s. GPU surfaces draw text shadows from the glyphs (1.14 B2); compare the motion stats with the reference |
| `collection-fling`, `virtual-stack-fling`, `carousel-swipe`, `swipe-rows`, `refresh-spinning`, `shimmer-list-scroll` | Device only, 3 s motions (1.14 B4): a collection view over 10,000 cards flung at the fastest velocity; a virtual stack layout over 10,000 cards scrolled; a carousel of 30 cards moved one card every 400 ms; 20 swipe rows opened and closed in turn every 200 ms; a refresh view refreshing over 40 cards; 120 shimmering rows scrolled. Results: [Performance.md](../Performance.md#list-controls-on-devices) |
| `virtual-fling` | `SkUiVirtualScrollView` over 10,000 buttons of three heights (recycled template views): the first frame realizes only the rows near the viewport; on a device, render-thread `AnimateScrollTo` towards the end while rows are realized, measured and anchored |
| `spinners` `[device]` | 120 activity indicators (render-thread content spin) |
| `toggle-transitions` `[device]` | 48 switches + 48 check boxes re-toggled every 120 ms: state-change transitions always running (UI clock, one re-record per control per frame) |
| `toggle-transitions-busy` `[device]` | The same while the UI thread is blocked 25 ms every 100 ms (where UI-thread transitions lose frames) |
| `native-labels` `[device]` | Reference: 1,000 native MAUI labels. `add` includes native handler creation; `firstFrame` is approximated (layout + dispatcher turns) |

### Adding a scenario

1. Subclass `BenchScenario` in `benchmarks/Scenarios/`:
   - `Build()` returns a **new** tree on every call.
   - Optional `Update` is timed until the next frame.
   - Optional `Motion` starts an animation and returns an `IDisposable` to stop it. Its duration should exceed `MotionDuration`, so the whole sampling window is in motion.
   - Set `DeviceOnly` when a headless run makes no sense.
2. Add it to `Scenarios.All` with a stable kebab-case `Name`. Renaming a scenario breaks comparisons with older result files.
3. **Baseline compatibility:** baselines compile the current `benchmarks/` folder against an older library, so scenarios must use long-stable public API.
   - Reach newer members through reflection: `TrySet(target, "Property", value)` for properties, or `GetMethod(...)` with a fallback (see `ScrollFling`).
   - A baseline that predates a member then measures the fallback.

The headless runner reaches the internal frame pipeline (`SkUiFrameRenderer`) by reflection. The device app reads `SkUiView.GetRenderStatistics()` / `ResetRenderStatistics()` the same way. Against libraries without these members, `record` / `composite` are reported empty, `firstFrame` / `update` are approximated, and motion is not measured; the app prints a `SKUIBENCH_WARN`. Comparisons against commits before the benchmarks were added (`71662f4` and earlier) are therefore only valid for `generate`, `add`, `measure` and `arrange`.

## Render statistics API

`SkUiView.GetRenderStatistics()` returns `SkUiRenderStatistics(Frames, AverageMilliseconds, MaxMilliseconds)` for a standalone surface's render thread: frames since the last `ResetRenderStatistics()`, and their cost. `UiFrames`, `UiAverageMilliseconds` and `UiMaxMilliseconds` add the UI-thread animation frames (UI-clock animations such as state-change transitions): each tick of the clock plus the recording and commit it causes.
- A frame is counted after it was flushed and submitted or presented. Its cost therefore includes GPU command submission and first-use shader compilation.
- The cost excludes GPU execution time and display latency.
- Only frames with committed content count. It is public so apps can use it for diagnostics too; it returns `default` while the view has no platform surface.

## Recorded comparisons

### 1.14 wave 1 (17 November 2026)

[ImplementationPlan-1.14.md](../archive/ImplementationPlan-1.14.md) A1, A2, A5, A6 against `v1.13.0`: headless, all scenarios, `-r 2`, macOS (Apple silicon), net10. 9 faster, 3 slower, 442 within noise.

| Scenario | Metric | 1.13.0 | Wave 1 | Δ % | Note |
| --- | --- | ---: | ---: | ---: | --- |
| core-images | allocKB | 5463.7 | 5041.9 | -7.7 | A6: a cached load allocates no task and no load context |
| core-images-viewsized | allocKB | 5808.9 | 5387.1 | -7.3 | A6 |
| core-icons-svg / -png / -font | allocKB | 5587.6 / 6456.5 / 5502.1 | 5165.8 / 6034.7 / 5080.3 | -7.5 / -6.5 / -7.7 | A6 |
| skui-images, skui-icons-* | allocKB | | | -1.7 to -2.0 | A6: the same 420 KB per 1,000 images |
| skui-images-viewsized | update | 15.1 | 11.4 | -24.2 | |
| core-icons-png | update | 4.5 | 2.5 | -45.0 | |
| skui-icons-png | arrange / record | 6.5 / 1.4 | 1.9 / 5.8 | -70.4 / +327.5 | a collection moved between phases: within noise with `--quiet-gc` |
| skui-avatars | record / update | 7.7 / 13.0 | 8.7 / 14.7 | +11.9 / +13.0 | allocations unchanged; within noise with `--quiet-gc` (`-n 10 -r 3`) |

The label and button scenarios allocate the same: their allocations are the tree and the text, not the paths wave 1 changed. Per step: A1 + A2 left the button and label scenarios within noise or faster (`-n 10 -r 3`); A2's gain is per pointer event (a drag allocated 80 bytes per move, now none: `GestureAllocationTests`), which no headless scenario drives. A5's vectorized pre-check made no difference against the single coverage lookup and was dropped. A6 with `--quiet-gc` on skui-images: `update` -11.5 %, `arrange` -9.7 %.

### 1.14 waves 1 and 2 (17 November 2026)

[ImplementationPlan-1.14.md](../archive/ImplementationPlan-1.14.md) A1 to A7 against `v1.13.0`: headless, all scenarios, `-r 2`, macOS (Apple silicon), net10. 15 faster, 6 slower, 441 within noise.

| Scenario | Metric | 1.13.0 | 1.14 | Δ % | Note |
| --- | --- | ---: | ---: | ---: | --- |
| core-images / -viewsized | allocKB | 5463.7 / 5808.9 | 4955.8 / 5301.0 | -9.3 / -8.7 | A6 cached loads, A3 batches, A4 paints |
| core-icons-svg / -png / -font | allocKB | 5587.6 / 6456.5 / 5502.1 | 5079.7 / 5886.1 / 4994.2 | -9.1 / -8.8 / -9.2 | as above |
| skui-images, skui-icons-* | allocKB | | | -2.1 to -2.5 | |
| shimmer-*, skel-* | allocKB | | | -0.6 to -4.6 | A3 (idle and props-only frames) |
| core-labels-update, core-entries-update, core-labels-fit, core-labels-grow | allocKB | | | -0.7 to -1.8 | about 86 KB each: A3 |
| skui-steppers | allocKB | 12213.3 | 11955.4 | -2.1 | A4 |
| icon-load-*, icon-first-* | allocKB | 29.2 | 29.5 | +0.9 | once per process: the per-thread paint stack and a pooled batch are created on first use |
| core-entries / core-entries-update | measure | 4.3 / 4.2 | 3.5 / 3.6 | -18.3 / -15.8 | |
| skui-stress-buttons | generate / measure / record | 12.1 / 3.3 / 3.8 | 11.4 / 3.0 / 3.5 | -5.7 / -9.9 / -7.0 | |
| core-icons-png | update | 4.9 | 2.7 | -45.9 | |
| skui-images-viewsized | update | 15.9 | 13.8 | -12.8 | |
| skui-avatars | generate / arrange / record / update | 11.4 / 2.2 / 8.2 / 14.3 | 12.6 / 2.5 / 8.8 / 16.1 | +10.4 / +16.7 / +7.2 / +13.1 | within noise with `--quiet-gc` (`-n 10 -r 3`); `generate` and `arrange` are not touched by either wave |
| skui-icons-png | arrange / record | 7.7 / 1.6 | 2.2 / 6.5 | -71.6 / +316.3 | a collection moved between phases: within noise with `--quiet-gc` |
| skel-skui-static-loaded | update | 8.3 | 8.8 | +5.4 | within noise with `--quiet-gc` (-4.6) |

What headless scenarios do not show: per-event and per-frame gains measured by the allocation tests, a drag (80 bytes per move → 0, `GestureAllocationTests`), idle and props-only frames (408 and 14,720 bytes → 0, `RenderAllocationTests`) and chrome draws (88 bytes per call → 0, `PaintAllocationTests`). Device numbers are on the verification backlog (A7 step 4, B4).

### 1.14 overlay bookkeeping on Android (17 November 2026)

[ImplementationPlan-1.14.md](../archive/ImplementationPlan-1.14.md) B3, on the Galaxy S9 (Android 10, net10 Release, `-n 6`). On devices `allocKB` is what every thread allocated during the 1.5 s motion window (about 91 frames).

| Scenario | fps | avg / max render ms | allocKB before | allocKB after |
| --- | ---: | ---: | ---: | ---: |
| hosted-scroll-drawn (reference) | 60.9 | 3.0 / 7.2 | 247.7 | 249.1 |
| hosted-scroll-auto (default: snapshots) | 58.8 | 3.4 / 9.4 | 266.2 | 267.1 |
| hosted-scroll-live | 61.4 | 2.8 / 7.6 | 874.3 | 249.8 |

The cost was in Live placement only: 6.9 KB per frame more than the drawn reference, about 340 bytes per overlay per frame, from the Android `Rect` objects each placement created (four, each with a managed peer); the default mode costs 0.2 KB per frame and no frame time. Fixed: placement keeps whole-pixel rectangles as plain integers, skips the measure and layout of an overlay whose placement did not change, and the container's layout pass no longer searches its overlays for each clip view. Frame rate and render time were not affected either way. Seen on the way: the drawn reference itself allocates about 2.7 KB per frame on the device (a render-thread scroll motion with its reports to the UI thread), which no headless scenario shows; left for a later look.

### 1.14 text-shadow spike (18 November 2026)

[ImplementationPlan-1.14.md](../archive/ImplementationPlan-1.14.md) B2 step 1. `text-shadow-scroll-*`: the Effects stress page's text-shadow row (400 cards, a shadow on each bold title: black, offset 1, 2, radius 3, 35 %), scrolled at the page's speed (the list in 6 s, about 67 cards per second), sampled for 4 s. A temporary switch in the library picked the path: `cpu` today's CPU raster per card (3 per frame at most), `gpu` the same raster on a GPU surface of the frame's context, `gpu16` that with 16 per frame, `glyph` the title's glyphs drawn again in its recording, blurred by a mask filter, in the shadow's color and at its offset (the label unclipped, so the halo is not cut at its frame). `-n 6`; the S9 on net10 Release (Android 10), the iPhone 8 on net11 Release (iOS 16, launched through Xcode 26.6's `mlaunch`; SDK 10 has no workloads on this machine).

| Variant | S9 fps | S9 avg / max render ms | S9 allocKB | iPhone 8 fps | iPhone 8 avg / max render ms | iPhone 8 allocKB |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| none (no shadow) | 59.9 | 4.1 / 13.5 | 637 | 59.5 | 4.1 / 29.5 | 774 |
| cpu (today) | 39.1 | 16.6 / 31.2 | 956 | 60.0 | 7.2 / 13.3 | 1,378 |
| gpu | 56.7 | 7.7 / 29.6 | 1,203 | 60.0 | 6.0 / 9.5 | 1,378 |
| gpu16 | 57.1 | 7.8 / 29.7 | 1,234 | 60.0 | 6.1 / 10.5 (3 samples, then the app stopped) | 1,378 |
| glyph | 59.6 | 5.1 / 18.0 | 635 | 60.2 | 4.3 / 5.8 | 779 |

Both paths pass the plan's 55 fps bar on the S9. The glyph path costs about 1 ms per frame over no shadow on the S9 and nothing measurable on the iPhone, and allocates nothing more (Skia caches the blurred glyph masks); a GPU raster still pays a blur and an offscreen surface per card as it scrolls in, with 30 ms spikes, and allocates more. Headless, the glyph shadow matches the content raster within 4 of 255 levels at radius 3 (3×) and radius 8 (1× and 3×), and within 10 at radius 3 on a 1× surface (macOS; 25 on Linux, where FreeType hints the glyphs the raster blurs); drawing the halos with `Plus` in a layer (summing overlaps, as one blur of all glyphs does) changed nothing measurable, so plain drawing is enough. Chosen: the glyph path, for labels whose content is plain text; the rest keeps the content raster.

### 1.14 runtime-async trial (17 November 2026)

[ImplementationPlan-1.14.md](../archive/ImplementationPlan-1.14.md) A7 step 3: `<Features>runtime-async=on</Features>` on the headless `net11.0` target against the same commit without it (`--net 11`, `-n 10 -r 3`; the flag took effect: async methods carry `MethodImplAttributes.Async` and no state machine). The net11 suite passed with it.

| Measure | Without | With | Δ |
| --- | ---: | ---: | ---: |
| `MauiSkiaUi.dll`, net11.0 Release | 2,493,952 B | 2,462,208 B | -1.3 % |
| allocKB: core-images, skui-images, core-icons-png, skui-icons-png, icon-load-png, icon-first-png | | | ±0.0 % each |
| timings, the same scenarios | | | all 44 rows within noise |

Not kept ([D9](ImplementationPlan-PerformanceAndControls.md#decisions): allocations must drop). After A6 a cached image load runs no async method, and the scenarios' loads are cache hits after their first iteration, so runtime async has nothing to save here. Worth a second look when the phones run .NET 11 (CoreCLR on Android and iOS), with a scenario whose loads actually wait (downloads, first decodes).

### 1.15 calendar scenarios (19 November 2026)

[ImplementationPlan-1.15.md](../archive/ImplementationPlan-1.15.md) A2: `scripts/bench.sh --baseline v1.14.0 -n 10 -S skui-calendars,core-calendars` (headless, a year of month calendars with a day style marking every day; update = each to its next month). Every phase within noise of 1.14.0; first-frame allocations 3 to 4 % lower (443 KB SkUi, 341 KB Core). Twelve calendars record in about 0.5 ms and re-record in 0.7 ms, so a calendar's first frame is cheap; what mattered was the swipe frame, which allocated 0.8 to 127 KB per frame and now allocates nothing in the calendar's drawing (`CalendarAllocationTests`; 104 bytes per frame through the pipeline, the re-recorded picture). The device scenario `calendar-slide` (a page slide every 400 ms with markers on every day) is for step A1.

### 1.15 calendar slide on devices (19 November 2026)

[ImplementationPlan-1.15.md](../archive/ImplementationPlan-1.15.md) A1: `calendar-slide` and `carousel-swipe` against v1.14.0, `-n 6`, the S9 on net10, the iPhone 8 on net11 (`mlaunch`). The first S9 run, before the slot fix, showed the slide at 36 fps (UI thread 11.2 / 31.1 ms) and a probe in the bench's copy of the library put 88 KB of each draw's 89 KB in the markers; the fixed runs and their table are in [Performance.md](../Performance.md#calendar-on-devices). A run with the S9's lock screen up measures nothing (every frame wait times out): unlock it, and keep it awake through the builds.

## Guidance for agents and reviewers

- Before and after any change that touches layout, text, recording, compositing, surfaces or animations:
  - run `scripts/bench.sh --baseline HEAD` (headless);
  - for rendering or surface changes, also run `-t android` or `-t ios` with `-S` limited to the affected scenarios.
- Paste the comparison (`--markdown`) into the PR or commit message and explain every `slower` row.
- On phones, keep the screen on and the device cool. Use `-n 6` or more, and `-r 2` for small effects. Report medians, not single runs.
- When a component gets a performance-sensitive feature, add a scenario for it in the same change.

Past investigations and their fixes (first-frame latency on Apple, shader compilation, Android vsync pacing, transitions, the iOS look's spinners): [docs/archive/Benchmark-findings.md](../archive/Benchmark-findings.md).
