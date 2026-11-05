# Performance

What SkiaUi costs on real devices, measured with the demo app's stress pages: how fast large trees build and scroll compared with native MAUI, and what drawing effects (gradients, borders, shadows, clips, translucency, animated content) cost while scrolling. Use it to decide which effects are safe in long scrolling lists.

## How it was measured

- **Devices:** Samsung Galaxy S9 (Android 10, Exynos 9810, OpenGL ES; 60 Hz) and iPhone 8 (iOS 16.7, A11, Metal; 60 Hz). Both were chosen to represent old, slow devices: what holds up on them holds up on the phones most apps support. Devices had not been under load before each batch. Back-to-back sessions on a warm device ran up to 30 % slower, so compare numbers from one session.
- **Code and toolchain:** measured on 21 October 2026 at commit `f9ca53c`, on .NET 10 (SDK 10.0.400, MAUI 10.0.110, SkiaSharp 4.153.1). The iPhone 8's drawing-effects results were measured again on 22 October at commit `8e5bfda`, after the iOS look's activity indicator was drawn as one image (the full matrix, 3 launches per layer).
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
| Native MAUI | 388 | 5,235 | 805 | 6,428 | — |
| SkUi* | 208 | 36 | 167 | 413 | 59.1 fps, 6.3 ms per frame |
| Core | 76 | 11 | 169 | 257 | 58.9 fps, 6.3 ms per frame |

**iPhone 8**

| Layer | Generate | Add | First frame | Overall | Scroll |
| --- | ---: | ---: | ---: | ---: | --- |
| Native MAUI | 237 | 1,683 | 965 | 2,885 | — |
| SkUi* | 114 | 14 | 37 | 164 | 60.1 fps, 6.5 ms per frame |
| Core | 50 | 4 | 35 | 89 | 60.0 fps, 6.5 ms per frame |

The drawn layers are ready **16× (SkUi*) and 25× (Core) faster** than native MAUI on the S9, and **18× and 32× faster** on the iPhone 8. Most of the native time is attaching the tree: every MAUI control creates a handler and a platform view, while a drawn tree creates none. Scrolling the drawn list holds the display's 60 fps on both devices; native MAUI scrolls through the platform's own scroll view, which these runs time only as a whole (no frame statistics).

The first round after launch is slower on every layer (S9 SkUi* 570 ms, Core 303 ms overall): it also pays for creating the GPU surface and for code that runs for the first time.

On the iPhone 8 the garbage collector takes a large share of SkUi*'s *Generate*: the 1,000 buttons are about 8 MB of objects that all stay alive, and the collections that run while they are built (minor and major ones) took about 33 ms per round in a profile of the demo. Whether a costly one lands inside a round moves *Generate* between about 60 and 110 ms, so compare averages of several rounds from one session. The earlier 73 ms (2 October, commit `5b5b28b`) is not a different cost of the code: that commit's own demo, run on the same device on 22 October, built in 110 ms (one round in eight in about 60 ms), as the current one does. The benchmark app builds the same buttons in about 50 ms, where collections cost about half as much.

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
| baseline | 5.2 ms · 60 fps | 5.1 ms · 60 fps | 5.1 ms · 59 fps | 5.1 ms · 59 fps |
| gradient | 5.4 ms (+5 %) · 60 fps | 5.2 ms (+3 %) · 60 fps | 6.0 ms (+18 %) · 57 fps | 5.1 ms (0 %) · 59 fps |
| rounded | 5.5 ms (+7 %) · 60 fps | 5.4 ms (+6 %) · 60 fps | 5.6 ms (+10 %) · 58 fps | 5.4 ms (+6 %) · 59 fps |
| clip | 6.0 ms (+16 %) · 60 fps | 5.8 ms (+15 %) · 60 fps | 6.6 ms (+31 %) · 57 fps | 5.9 ms (+16 %) · 59 fps |
| spinner | 5.9 ms (+15 %) · 60 fps | 5.6 ms (+11 %) · 60 fps | 4.9 ms (−3 %) · 60 fps | 5.3 ms (+4 %) · 59 fps |
| translucent | 6.5 ms (+26 %) · 60 fps | 6.5 ms (+28 %) · 60 fps | 6.8 ms (+35 %) · 58 fps | 6.9 ms (+35 %) · 59 fps |
| card shadow | 6.3 ms (+21 %) · 60 fps | 6.4 ms (+26 %) · 60 fps | 7.3 ms (+45 %) · 55 fps | 6.5 ms (+28 %) · 58 fps |
| text shadow | 7.1 ms (+38 %) · 60 fps | 7.2 ms (+43 %) · 60 fps | 14.2 ms (+180 %) · **45 fps** | 13.6 ms (+168 %) · **45 fps** |
| shaped border | 7.5 ms (+46 %) · 60 fps | 7.6 ms (+49 %) · 60 fps | 19.8 ms (+292 %) · **33 fps** | 19.4 ms (+282 %) · **34 fps** |
| **all** | 10.2 ms (+97 %) · **52 fps** | 10.1 ms (+99 %) · **53 fps** | 46.8 ms (+825 %) · **20 fps** | 46.1 ms (+807 %) · **20 fps** |

First frame of the 400-card list (ms):

| Effect | iPhone 8 SkUi* | iPhone 8 Core | Galaxy S9 SkUi* | Galaxy S9 Core |
| --- | ---: | ---: | ---: | ---: |
| baseline | 131 | 38 | 552 | 139 |
| shaped border | 999 | 907 | 1,886 | 1,454 |
| all | 1,765 | 1,582 | 2,795 | 2,482 |
| any other single effect | 100–117 | 42–48 | 286–336 | 117–195 |

### What the numbers mean

- **Nothing re-records while scrolling.** No configuration recorded a picture during the scroll: gradients, shadows, clips and opacity are applied when compositing, and moving content only changes offsets. The cost of an effect is compositing (and GPU) work per frame.
- **Free in practice:** gradient backgrounds, rounded corners, clips and running activity indicators cost 0–30 % render time and keep 57–60 fps on both devices. (The iOS look's spinner drew its 8 spokes one by one until 22 October, which cost 30–45 % on the iPhone 8 and took the SkUi* list to 57 fps; drawn as one image, it costs 11–15 % at 60 fps.)
- **Moderate:**
  - Translucent cards (an opacity layer each) and card shadows from opaque fills cost 20–45 %. The iPhone 8 keeps 60 fps; the S9 keeps 55–59 fps. The blur work is on the GPU, and these numbers don't include GPU time.
- **Expensive on the S9:**
  - **Shaped borders** (a path with a dashed gradient stroke, about 20 ms per frame) and **text shadows** (14 ms per frame) take the S9 to 33–45 fps. The iPhone 8 holds 60 fps with both.
  - The dashed outline of a complex path is costly on the S9's GL backend.
  - A text shadow is cast from the glyphs, so each card's shadow is rasterized on the CPU once, when the card first scrolls into view. That is about 50 per second in this scroll, capped at 3 per frame. The iPhone's faster CPU absorbs it; the S9's doesn't.
- **All effects together** stay usable on the iPhone 8 (52–53 fps) but not on the S9 (20 fps).
- **Animated content keeps the surface drawing.** With spinners, the surface composites about 60 frames per second even while nothing scrolls, where a static list composites none. That is little work per frame, but it never stops, which costs battery. With every effect on, even the idle S9 only manages 30 fps.
- **First frames:**
  - A shaped border costs about 1–2 s for the first frame of 400 cards, because each card computes its outline and its content clip (a path operation) when it is first laid out.
  - With the other effects, the S9's SkUi* first frame takes 290–340 ms.
  - The first configuration of each run (the baseline) also pays one-time costs.
- **Native MAUI** builds the same card lists in 2.7–6.4 s to first frame on the iPhone 8 and 3.1–11.7 s on the S9, depending on the effects. Later configurations in a run are slower, as earlier native trees are still being released; only the first configuration compares cleanly.

### Recommendations for long lists

- Prefer **opaque fills** for shadowed cards: an opaque card, border, button, box or shape casts its shadow from its outline, which is cheap. Shadows on text, images or translucent shapes are rasterized from content.
- Avoid **text shadows** on many list items on mid-range Android devices; put the shadow on an opaque card behind the text instead.
- Avoid **dashed strokes on complex paths** on many list items; a rounded rectangle, solid stroke or simpler path is much cheaper.
- Use **running spinners** only while something loads: they keep the surface drawing every frame.
- **Defer expensive panes** (other tabs, hidden or collapsed sections): `SkUiContentView ContentLoading="WhenShown"` with a `ContentTemplate` creates a pane only when it is first shown (optionally after a delay, so tabs passed through are not created) ([SkUiContentView.md](controls/SkUiContentView.md#loading-content-when-shown)).
- Check on the slowest device you support. The iPhone 8 absorbs every single effect; the S9 shows where the limits are.

## Vector icons

**Eight 24 dp gear icons** (45 to 73 path verbs each), as `.svg` files drawn by the [reader](controls/SkUiImage.md), as PNGs of the screen's pixels (what a `MauiImage` is) recolored with a tint, and as font glyphs. Measured on 5 November 2026 with `scripts/bench.sh` on the same two phones, Release builds, SDK 10.0.402, MAUI 10.0.120, SkiaSharp 4.154.0.

**Loading**, off the UI thread and not cached:

| | S9 SVG | S9 PNG | S9 font | iPhone 8 SVG | iPhone 8 PNG | iPhone 8 font |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| First icon in the process (ms, median of 3 launches) | 259 | 160 | 263 | 26 | 19 | 46 |
| Each further icon (ms, 200 loads, each kind in its own process) | 0.93 | 0.52 | 0.21 | 0.19 | 0.16 | 0.05 |

The bench writes its files before it starts timing, so the first row is the load alone. On the S9, a decode was about 20 µs for the codec to refuse the bytes, 120 µs to parse and 240 µs to draw at 72 px; the rest is the file read and the load's own work. In a process that had just built the 1,000-icon trees below, the S9 took 3.4 ms per SVG and 0.9 ms per PNG. The cause was not investigated: it runs off the UI thread, and memory pressure is the likely reason.

**1,000 icons once loaded**, about 50 per screen, in ms. *Update* is a new color on all of them (a theme change's work on the UI thread; the eight loads a color needs the first time run off it); *Scroll* is an animated scroll.

**Galaxy S9**

| Layer, icon | Generate | Add | First frame | Update | Scroll |
| --- | ---: | ---: | ---: | ---: | --- |
| SkUi*, SVG | 241 | 25 | 127 | 126 | 58.4 fps, 5.7 ms per frame |
| SkUi*, PNG | 213 | 24 | 112 | 181 | 60.5 fps, 5.4 ms per frame |
| SkUi*, font | 158 | 18 | 121 | 176 | 56.6 fps, 7.2 ms per frame |
| Core, SVG | 36 | 16 | 50 | 141 | 57.6 fps, 6.6 ms per frame |
| Core, PNG | 52 | 4 | 51 | 139 | 57.8 fps, 6.5 ms per frame |
| Core, font | 39 | 16 | 50 | 147 | 56.2 fps, 6.8 ms per frame |

**iPhone 8**

| Layer, icon | Generate | Add | First frame | Update | Scroll |
| --- | ---: | ---: | ---: | ---: | --- |
| SkUi*, SVG | 120 | 15 | 35 | 47 | 59.9 fps, 6.3 ms per frame |
| SkUi*, PNG | 80 | 11 | 27 | 42 | 59.9 fps, 6.2 ms per frame |
| SkUi*, font | 158 | 17 | 42 | 54 | 59.9 fps, 6.2 ms per frame |
| Core, SVG | 38 | 2 | 14 | 45 | 59.2 fps, 6.3 ms per frame |
| Core, PNG | 41 | 2 | 13 | 42 | 59.7 fps, 6.2 ms per frame |
| Core, font | 40 | 2 | 14 | 42 | 59.2 fps, 6.2 ms per frame |

The scenarios create the SkUi\* layer's SVG sources by name, so a baseline without them still builds: that part of its *Generate* is the bench's.

- **Once loaded, an SVG icon costs what a PNG does:** it is a bitmap of the same size, and scrolling is within noise on both phones.
- **An uncached SVG** costs about what a PNG does on the iPhone 8 and up to twice as much on the S9, off the UI thread. A font glyph is faster to load, but it renders on the UI thread.
- **The first icon needs no warm-up.** The S9 shows it about 100 ms later than a PNG, once per process and without blocking the UI thread, about what a font glyph takes; the iPhone 8 shows it a few ms later.
- **A theme change** on a screen of 50 icons from 20 files should take under 10 ms on the S9's UI thread and 20 loads off it (about 19 ms of work). This is worked out from the numbers above, not measured on such a screen.

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

The vector icon numbers come from the bench app: `scripts/bench.sh -t android -S skui-icons-svg,skui-icons-png,skui-icons-font`, and `core-icons-*`, `icon-load-*`; `icon-first-*` with `-n 1 -w 0`, alone, once per launch. An iPhone on iOS 16 may need `SKUI_MLAUNCH_SDKROOT` ([Benchmarks.md](design/Benchmarks.md#running)).

| Variable | Values |
| --- | --- |
| `SKUI_DEMO_ROUTE` | `stress`, `effects-stress` |
| `SKUI_STRESS=run` | Stress page: `SKUI_STRESS_LAYER` (`skui`, `core`, `native`), `SKUI_STRESS_ROUNDS` (3), `SKUI_STRESS_COUNT` (1000), `SKUI_STRESS_ANIMATE=1`, `SKUI_STRESS_HW=0`, `SKUI_STRESS_SCROLL=1` |
| `SKUI_EFFECTS_STRESS=matrix` / `repeat` | Effects stress page: `SKUI_EFFECTS_STRESS_LAYER`, `SKUI_EFFECTS_STRESS_COUNT` (400), `SKUI_EFFECTS_STRESS_HW=0`, `SKUI_EFFECTS_STRESS_SCROLL=ui`; `repeat` scrolls `SKUI_EFFECTS_STRESS_EFFECTS` (e.g. `Spinner,Clip`) `SKUI_EFFECTS_STRESS_REPEAT` times |
| `SKUI_EXIT_WHEN_DONE=1` | Quit after the results (for launchers that wait for the app to exit) |

Debug builds also report per-frame phases (apply, draw, flush, present), garbage collections and shadow rasterizations for the slowest frames ([RenderingPipeline.md](design/RenderingPipeline.md#frame-trace-diagnostics-builds)). For before / after comparisons of a change, use the benchmark runner ([Benchmarks.md](design/Benchmarks.md)).
