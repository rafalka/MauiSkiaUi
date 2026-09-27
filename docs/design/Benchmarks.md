# SkiaUi benchmarks

Measure performance **before and after a change**, for any component, in a way that an AI agent and a human run the same way. The demo app's Stress page stays a human-facing MAUI / SkUi* / Core showcase. Benchmarks live in a separate app and runner that share one scenario catalog.

## Two tiers

| Tier | Where | What it measures | Use it for |
| --- | --- | --- | --- |
| **Headless** (`benchmarks/MauiSkiaUi.Benchmarks`) | `net10.0` console app, any dev machine or CI | `generate` (build tree), `measure`, `arrange`, `record` (UI-thread picture recording of the first frame), `composite` (render-thread compositing into an offscreen raster), `update`, `allocKB` (managed bytes) | Fast inner loop (≈ 10 s), layout / text / recording regressions, allocations |
| **Device** (`benchmarks/MauiSkiaUiBench`) | Release MAUI app on Android / iOS / Mac Catalyst | `generate`, `add` (attach to the page: handler creation), `firstFrame` (attach → first composited frame: layout, recording and compositing on the real surface), `update` (change → next composited frame), `motionFps` / `motionAvgRenderMs` / `motionMaxRenderMs` (render-thread frames while animating) | Authoritative numbers: JIT/AOT, GPU surfaces, vsync pacing, real text stacks |

All values are **milliseconds** (except `motionFps` and `allocKB`). Reports show the **median** of the measured iterations; each scenario first runs `--warmup` iterations that are discarded.

On iOS and Mac Catalyst the app forces UIKit layout (`LayoutIfNeeded`) right after attaching or updating. Since iOS 18, UIKit lays out in an update cycle aligned to the display refresh. Without forcing it, `firstFrame` would include a 0–16 ms wait that depends only on where the benchmark's continuation falls in that cycle. Native views pay that wait too. The update and motion phases start after a 100 ms idle gap: a change right after a frame waits for the next vsync, which is correct pacing but not what `update` should measure.

The device app is always built **Release**: no DevFlow and no profiler. Debug numbers exaggerate managed-code cost several times and must not be compared with Release numbers. Headless numbers miss GPU, AOT and platform text costs. Treat them as a signal, and confirm important changes on a device.

## Running

```bash
# Headless, all scenarios, working tree only
scripts/bench.sh

# Uncommitted changes vs the last commit (the everyday "did I make it slower?" check)
scripts/bench.sh --baseline HEAD

# Device: a branch vs master on a connected Android phone, selected scenarios, 8 runs
scripts/bench.sh -t android -s 2299011508047ece --baseline master -S core-labels,skui-labels -n 8

# iOS simulator (UDID from `xcrun simctl list devices`); a physical iPhone UDID uses devicectl
scripts/bench.sh -t ios -s A7AC4C38-0DC8-471D-831B-7B3C9452BB76

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
  The script also sets `DOTNET_TieredCompilation=0`, so early iterations are not penalized by tiered JIT.
- **Human on a device:** deploy `benchmarks/MauiSkiaUiBench` in Release from the IDE. Type scenario names, or leave the box empty for all, then press **Run**. The median table appears on screen.
- **Android launch extras:**
  ```
  adb shell am start -n com.rkdevel.skiauibench/skiauibench.MainActivity --ez autorun true --es scenarios core-labels,spinners --ei runs 6 --ei warmup 1 --es label mine
  ```
  Results are written to logcat.
- **Apple launch arguments:** `--autorun --exit --runs 6 --scenarios core-labels`, for example via `xcrun simctl launch --console`.

Both runners print one `SKUIBENCH {json}` line per sample, `SKUIBENCH_TABLE` lines, and `SKUIBENCH_DONE`. Problems are printed as `SKUIBENCH_WARN` or `SKUIBENCH_ERROR`. `bench_compare.py collect` turns any such log into a results file.

## Scenarios

The catalog lives in [benchmarks/Scenarios/Scenarios.cs](../../benchmarks/Scenarios/Scenarios.cs), and both tiers link it. Scenarios marked `[device]` only run in the app: they need a real surface, motion or a native control.

| Scenario | Covers |
| --- | --- |
| `core-labels`, `skui-labels` | 1,000 plain labels in a 2-column grid (Core vs SkUi* layer) |
| `core-buttons`, `skui-buttons` | 1,000 buttons (the Stress page workload) |
| `mixed-script-labels` | Arabic / Hebrew / Devanagari / emoji / CJK: HarfBuzz shaping, bidi, font fallback |
| `core-labels-simple` | Numeric labels with `TextRendering = Simple` |
| `core-labels-update`, `skui-labels-update` | Steady state: change every label's text (re-layout + re-record) |
| `scroll-fling` `[device]` | Render-thread `AnimateScrollTo` through 400 buttons |
| `spinners` `[device]` | 120 activity indicators (render-thread content spin) |
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

`SkUiView.GetRenderStatistics()` returns `SkUiRenderStatistics(Frames, AverageMilliseconds, MaxMilliseconds)` for a standalone surface's render thread: frames since the last `ResetRenderStatistics()`, and their cost.
- A frame is counted after it was flushed and submitted or presented. Its cost therefore includes GPU command submission and first-use shader compilation.
- The cost excludes GPU execution time and display latency.
- Only frames with committed content count. It is public so apps can use it for diagnostics too; it returns `default` while the view has no platform surface.

## Guidance for agents and reviewers

- Before and after any change that touches layout, text, recording, compositing, surfaces or animations:
  - run `scripts/bench.sh --baseline HEAD` (headless);
  - for rendering or surface changes, also run `-t android` or `-t ios` with `-S` limited to the affected scenarios.
- Paste the comparison (`--markdown`) into the PR or commit message and explain every `slower` row.
- On phones, keep the screen on and the device cool. Use `-n 6` or more, and `-r 2` for small effects. Report medians, not single runs.
- When a component gets a performance-sensitive feature, add a scenario for it in the same change.

## Findings so far

- **iOS / Catalyst first frame (Core vs SkUi\*):** early simulator runs showed Core slower than SkUi\* (23.6 vs 12.3 ms). Tracing showed the measured work was only about half of each first frame (Core 8–9 ms, SkUi\* 11–13 ms). The rest was waiting:
  - **Display-link wait:** after a commit, the Metal loop waited for the next `CADisplayLink` tick, 0–16 ms.
  - **Empty frame:** it also presented an empty frame before the first commit, which delayed the next render.
  - **UIKit layout wait:** UIKit's display-aligned layout added another 0–16 ms before measure, alternating between iterations.
  - **Fixes and results:**
    - Commits that arrive while idle now render immediately, and surfaces skip frames until content exists. Commit-to-rendered latency went from about 5 ms median (up to 16 ms at p90) to about 1 ms.
    - The bench forces the UIKit layout.
    - Results are now stable to about ±0.5 ms, with Core ahead on the simulator (labels 8.1 vs 11.0 ms, buttons 9.8 vs 12.3, update 9.7 vs 13.3) and on Catalyst.
- **First-use shader compilation (Apple):**
  - **Problem:** after a fresh install, the first frame that uses a new kind of drawing (text, rounded rects) spends 250–500 ms in the GPU flush while Skia compiles Metal pipelines. Metal caches them across launches. It happens on the render thread, so the UI thread stays responsive, but the first screen of a freshly installed app appears late.
  - **Measurement fix:** statistics used to stop before the flush, so this time spilled into the next measured iteration. They now include it.
  - **Candidate fix (not implemented):** a startup warm-up frame that draws common primitives offscreen.

- **Android vsync pacing** (found by `spinners` / `scroll-fling`):
  - **Problem:** `eglSwapBuffers` on a `TextureView` does not block on vsync, so continuous render-thread animations rendered 285–497 frames per second on the Galaxy S9 and discarded most of them.
  - **Fix:** continuous frames are now paced by a `Choreographer` on a dedicated looper thread (`SkUiVsync`). Pacing is independent of the UI thread, so animations still run while it is busy.
  - **Result:** 59.8–60 fps, at an average render cost of 1.7–3.2 ms per frame.
