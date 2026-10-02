# Performance

What SkiaUi costs on real devices, measured with the demo app's stress pages: how fast large trees build and scroll compared with native MAUI, and what drawing effects (gradients, borders, shadows, clips, translucency, animated content) cost while scrolling. Use it to decide which effects are safe in long scrolling lists.

## How it was measured

- **Devices:** Samsung Galaxy S9 (Android 10, Exynos 9810, OpenGL ES; 60 Hz) and iPhone 8 (iOS 16.7, A11, Metal; 60 Hz), measured in October 2026 with the P7 code (gradients, shadows, clips), on devices that had not been under load before each batch. Back-to-back sessions on a warm device ran up to 30 % slower, so compare numbers from one session.
- **Builds:** Release builds of `MauiSkiaUiDemo` (iOS: full AOT), GPU surfaces (`HwAccelerated = true`).
- **Runs:** scripted, with no one touching the device (see [Reproducing](#reproducing)).
  - Stress page: 2 launches of 4 rounds per layer; the first round of each launch warms up and is not counted, so each number is the average of 6 rounds.
  - Effects stress page: 3 launches per layer, each running the full matrix (an unreported warm-up pass, then every configuration); each number is the average of the 3 launches.
- **Times** are milliseconds:
  - *Generate*: building the tree.
  - *Add*: attaching it to the page.
  - *First frame*: layout and the first frame.
  - *Overall*: from the start until the UI thread is idle.
  - *Render*: what the GPU surface's render thread spends per composited frame, from the start of compositing until the frame is flushed and submitted. GPU execution time is not included, so a frame can fit this budget and still miss the display when the GPU is the bottleneck.
  - *fps*: frames composited per second during an animated scroll across the whole list.

## Large trees

The Stress page: **1,000 buttons** in a two-column grid under one scroll view, animation off. The SkUi* and Core layers use one drawn surface; native MAUI uses a MAUI `Grid` of `Button`s in a `ScrollView`.

**Galaxy S9**

| Layer | Generate | Add | First frame | Overall | Scroll |
| --- | ---: | ---: | ---: | ---: | --- |
| Native MAUI | 415 | 4,798 | 774 | 5,988 | — |
| SkUi* | 241 | 28 | 214 | 484 | 59.0 fps, 5.9 ms per frame |
| Core | 55 | 7 | 165 | 228 | 58.7 fps, 6.0 ms per frame |

**iPhone 8**

| Layer | Generate | Add | First frame | Overall | Scroll |
| --- | ---: | ---: | ---: | ---: | --- |
| Native MAUI | 240 | 1,649 | 927 | 2,816 | — |
| SkUi* | 73 | 14 | 48 | 135 | 59.1 fps, 6.5 ms per frame |
| Core | 52 | 8 | 31 | 91 | 60.1 fps, 6.5 ms per frame |

The drawn layers are ready **12× (SkUi*) and 26× (Core) faster** than native MAUI on the S9, and **21× and 31× faster** on the iPhone 8. Most of the native time is attaching the tree: every MAUI control creates a handler and a platform view, while a drawn tree creates none. Scrolling the drawn list holds the display's 60 fps on both devices; native MAUI scrolls through the platform's own scroll view, which these runs time only as a whole (no frame statistics).

The first round after launch is slower on every layer (S9 SkUi* 907 ms, Core 577 ms overall): it also pays for creating the GPU surface and for code that runs for the first time.

## Drawing effects

The Effects stress page: **400 cards** (avatar, title, subtitle) in a scroll view, with one effect switched on at a time, then all together:

| Effect | What each card gets |
| --- | --- |
| baseline | A white rectangle with a 1 DIP border |
| gradient | A linear-gradient `Background` |
| rounded | `RoundRectangle 12` corners |
| shaped border | A ticket-shaped `StrokeShape` (a path with notches) with a dashed gradient stroke |
| card shadow | A soft `Shadow` on the card (opaque card: cast from its outline) |
| text shadow | A `Shadow` on the title (cast from the glyphs, rasterized once per card) |
| clip | The avatar clipped to a circle (`Clip`) |
| translucent | The card at 90 % opacity (an opacity layer) |
| spinner | A running `ActivityIndicator` on the card |

Render time per frame while scrolling, and frames per second. The change from the baseline is in brackets.

| Effect | iPhone 8 SkUi* | iPhone 8 Core | Galaxy S9 SkUi* | Galaxy S9 Core |
| --- | --- | --- | --- | --- |
| baseline | 4.8 ms · 60 fps | 4.7 ms · 60 fps | 4.6 ms · 58 fps | 4.6 ms · 59 fps |
| gradient | 5.0 ms (+5 %) · 60 fps | 5.0 ms (+6 %) · 60 fps | 4.6 ms (+1 %) · 58 fps | 4.8 ms (+5 %) · 58 fps |
| rounded | 5.2 ms (+8 %) · 60 fps | 5.2 ms (+9 %) · 60 fps | 4.7 ms (+2 %) · 59 fps | 5.0 ms (+9 %) · 59 fps |
| clip | 5.6 ms (+17 %) · 60 fps | 5.6 ms (+18 %) · 59 fps | 5.4 ms (+19 %) · 59 fps | 5.5 ms (+21 %) · 58 fps |
| spinner | 5.2 ms (+8 %) · 60 fps | 5.1 ms (+9 %) · 60 fps | 4.5 ms (−2 %) · 60 fps | 4.6 ms (0 %) · 60 fps |
| translucent | 6.3 ms (+33 %) · 60 fps | 6.0 ms (+28 %) · 60 fps | 6.7 ms (+48 %) · 58 fps | 6.7 ms (+46 %) · 57 fps |
| card shadow | 6.4 ms (+33 %) · 60 fps | 6.1 ms (+30 %) · 60 fps | 7.6 ms (+67 %) · **51 fps** | 6.2 ms (+37 %) · 58 fps |
| text shadow | 7.2 ms (+52 %) · 59 fps | 7.2 ms (+53 %) · 60 fps | 13.6 ms (+199 %) · **39 fps** | 14.3 ms (+213 %) · **38 fps** |
| shaped border | 7.8 ms (+63 %) · 59 fps | 7.7 ms (+64 %) · 60 fps | 21.2 ms (+366 %) · **30 fps** | 23.0 ms (+404 %) · **29 fps** |
| **all** | 9.6 ms (+101 %) · **53 fps** | 9.5 ms (+101 %) · **53 fps** | 46.6 ms (+924 %) · **19 fps** | 45.1 ms (+889 %) · **20 fps** |

First frame of the 400-card list (ms):

| Effect | iPhone 8 SkUi* | iPhone 8 Core | Galaxy S9 SkUi* | Galaxy S9 Core |
| --- | ---: | ---: | ---: | ---: |
| baseline | 124 | 32 | 600 | 184 |
| shaped border | 1,027 | 904 | 1,893 | 1,605 |
| all | 1,726 | 1,589 | 2,992 | 2,634 |
| any other single effect | 96–113 | 38–44 | 297–691 | 127–200 |

### What the numbers mean

- **Nothing re-records while scrolling.** No configuration recorded a picture during the scroll: gradients, shadows, clips and opacity are applied when compositing, and moving content only changes offsets. The cost of an effect is compositing (and GPU) work per frame.
- **Free in practice:** gradient backgrounds, rounded corners, clips and running activity indicators cost 0–20 % render time and keep 58–60 fps on both devices.
- **Moderate:** translucent cards (an opacity layer each) and card shadows from opaque fills cost 30–70 %. The iPhone 8 keeps 60 fps. On the S9, SkUi* card shadows dropped to 51 fps although the render thread stayed at 7.6 ms per frame. The blur work is on the GPU, and these numbers don't include GPU time.
- **Expensive on the S9:**
  - **Shaped borders** (a path with a dashed gradient stroke, 21–23 ms per frame) and **text shadows** (14 ms per frame) take the S9 to 30–40 fps. The iPhone 8 holds 59–60 fps with both.
  - The dashed outline of a complex path is costly on the S9's GL backend.
  - A text shadow is cast from the glyphs, so each card's shadow is rasterized on the CPU once, when the card first scrolls into view. That is about 50 per second in this scroll, capped at 3 per frame. The iPhone's faster CPU absorbs it; the S9's doesn't.
- **All effects together** stay usable on the iPhone 8 (53 fps) but not on the S9 (20 fps).
- **Animated content keeps the surface drawing.** With spinners, the surface composites about 60 frames per second even while nothing scrolls, where a static list composites none. That is little work per frame, but it never stops, which costs battery. With every effect on, even the idle S9 only manages 30 fps.
- **First frames:**
  - A shaped border costs about 1–2 s for the first frame of 400 cards, because each card computes its outline and its content clip (a path operation) when it is first laid out.
  - With the other effects, the S9's SkUi* first frame varies between 300 and 700 ms.
  - The first configuration of each run (the baseline) also pays one-time costs.
- **Native MAUI** builds the same card lists in 2.7–6.4 s to first frame on the iPhone 8 and 3.7–12.5 s on the S9, depending on the effects. Later configurations in a run are slower, as earlier native trees are still being released; only the first configuration compares cleanly.

### Recommendations for long lists

- Prefer **opaque fills** for shadowed cards: an opaque card, border, button, box or shape casts its shadow from its outline, which is cheap. Shadows on text, images or translucent shapes are rasterized from content.
- Avoid **text shadows** on many list items on mid-range Android devices; put the shadow on an opaque card behind the text instead.
- Avoid **dashed strokes on complex paths** on many list items; a rounded rectangle, solid stroke or simpler path is much cheaper.
- Use **running spinners** only while something loads: they keep the surface drawing every frame.
- Check on the slowest device you support. The iPhone 8 absorbs every single effect; the S9 shows where the limits are.

## Reproducing

Both stress pages are in the demo app (`MauiSkiaUiDemo`): **Stress** (the 1,000-button grid) and **Effects stress** (the card list, with **Matrix** in the toolbar for every effect). Run a Release build. Scripted runs start a page and print one `data` line per round (key=value, ms) to the console:

```bash
# Android (adb): intent extras become environment variables
adb shell am start -n com.rkdevel.mauiskiauidemo/crc64ae2d351589396129.MainActivity \
    -e SKUI_DEMO_ROUTE effects-stress -e SKUI_EFFECTS_STRESS matrix -e SKUI_EFFECTS_STRESS_LAYER skui
adb logcat -s DOTNET | grep '\[EffectsStress\] data'

# iOS 16 and older (mlaunch); iOS 17+: xcrun devicectl device process launch --environment-variables
mlaunch --launchdev MauiSkiaUiDemo.app --devname <udid> --wait-for-exit:true --setenv=SKUI_EXIT_WHEN_DONE=1 \
    --setenv=SKUI_DEMO_ROUTE=stress --setenv=SKUI_STRESS=run --setenv=SKUI_STRESS_LAYER=core \
    --setenv=SKUI_STRESS_ROUNDS=4 --setenv=SKUI_STRESS_SCROLL=1
```

| Variable | Values |
| --- | --- |
| `SKUI_DEMO_ROUTE` | `stress`, `effects-stress` |
| `SKUI_STRESS=run` | Stress page: `SKUI_STRESS_LAYER` (`skui`, `core`, `native`), `SKUI_STRESS_ROUNDS` (3), `SKUI_STRESS_COUNT` (1000), `SKUI_STRESS_ANIMATE=1`, `SKUI_STRESS_HW=0`, `SKUI_STRESS_SCROLL=1` |
| `SKUI_EFFECTS_STRESS=matrix` / `repeat` | Effects stress page: `SKUI_EFFECTS_STRESS_LAYER`, `SKUI_EFFECTS_STRESS_COUNT` (400), `SKUI_EFFECTS_STRESS_HW=0`, `SKUI_EFFECTS_STRESS_SCROLL=ui`; `repeat` scrolls `SKUI_EFFECTS_STRESS_EFFECTS` (e.g. `Spinner,Clip`) `SKUI_EFFECTS_STRESS_REPEAT` times |
| `SKUI_EXIT_WHEN_DONE=1` | Quit after the results (for launchers that wait for the app to exit) |

Debug builds also report per-frame phases (apply, draw, flush, present), garbage collections and shadow rasterizations for the slowest frames ([RenderingPipeline.md](design/RenderingPipeline.md#frame-trace-diagnostics-builds)). For before / after comparisons of a change, use the benchmark runner ([Benchmarks.md](design/Benchmarks.md)).
