# Benchmark findings (archived)

> Archived from `docs/design/Benchmarks.md`: performance investigations and their fixes, up to October 2026. How to run the benchmarks: [docs/design/Benchmarks.md](../design/Benchmarks.md); device results for users: [docs/Performance.md](../Performance.md).

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

- **State-change transitions (UI thread)** (found by `toggle-transitions`):
  - **First run:** re-toggling every 250 ms reported about 50 frames per second even on Mac Catalyst at 0.4 ms per frame. The transitions (160–200 ms) had ended before each re-toggle, so no frames were needed in between. The scenarios now re-toggle every 120 ms.
  - **Result:** with 96 controls always animating, the Galaxy S9 runs 58.4 UI animation frames per second at 4.7 ms each (about 0.05 ms per control); Mac Catalyst runs 59.9 at 0.6 ms. With the UI thread blocked 25% of the time, it drops to 51.5 / 49.9. Details and the design decision: [ArchitectureReview.md](ArchitectureReview-2026-09.md#state-change-animations).

- **iOS look spinners and Stress page Generate on the iPhone 8** (found by `spinners`, `skui-stress-buttons` with `--look IosSkUiLook`):
  - **Spinners:** the iOS look's activity indicator drew its 8 spokes one by one, so each running spinner was 8 draws on every frame the render thread turned it (an arc is one). 120 spinners rendered in 7.3 ms per frame against 4.1 ms with the arc, and the Effects stress page's spinner cards cost +33 % render time while scrolling. The spokes are now rasterized once per size and color and drawn as one image: 6.0 ms for 120 spinners, +9 % on the Effects page.
  - **Generate:** the Stress page's SkUi\* *Generate* went from 73 ms (2 October) to 114 ms. In the benchmark app the same buttons built in 48 ms at the old commit and 52 ms now, with the iOS look or without it, and the old commit's demo, run on the same day as the new one, built in 110 ms as well. A Time Profiler trace of the demo put about 33 ms per round in Mono's collector (minor and major collections while 8 MB of live buttons are built), against 16 ms in the benchmark app. Whether a costly collection lands inside a round moves the demo between about 60 and 110 ms.

