# SkiaUi testing considerations

How we plan to verify **unit behavior** and **core mechanisms** (layout, paint, animation, gestures, hosting) — including what can run fully automated in CI, and where AI agents help.

Aligns with [Requirements.md](Requirements.md) (especially NFR-2 correctness-first → optimize), [LayoutSystem.md](LayoutSystem.md), [DrawingMechanism.md](DrawingMechanism.md), [AnimationMechanism.md](AnimationMechanism.md), and [EventMechanism.md](EventMechanism.md).

## Phase 0 implementation

Run `dotnet test tests/MauiSkiaUi.Tests/MauiSkiaUi.Tests.csproj`. The library's plain `net10.0` target shares the actual node/layout/paint/input/clock sources with the device targets; only the native handler and builder registration are platform-conditional. Tests use real MAUI views without handlers and CPU Skia bitmaps, not replacement view mocks.

The 36 cases cover primitive pixels, transparent regions, layer order, alpha/z-order composition, transform-aware hits, MAUI margins/alignment, selective measure/arrange, binding context and ownership, batching, passive/transparent/disabled hit rules, capture cancellation/removal, and animation progress/repeat/cancellation/idle behavior. Review regressions add margined/transformed intermediate-layout capture, explicit NaN maximum constraints, and clock restart on the same timeline. Clock tests never sleep. Interior pixels are exact; antialiased edge baselines and font goldens remain later work.

The native handler delegates frame work to the internal `SkUiFrameRenderer`, tested with a deterministic dispatcher queue and real Skia pictures/bitmaps. Tests cover follow-up invalidation during paint without an animation, pre-paint update coalescing, replay and pixel-to-DIP input at 1x/2x/3x density, canvas-state preservation, recovery after zero-sized layout, hosted-root rejection, and disposal stopping animations and cancelling queued work. Native event subscription teardown and GPU presentation remain device-only checks.

Device verification checklist for the Phase 0 demo:

- Launch on Android or Apple; confirm the box, ellipse, line, and software strip appear.
- Inspect `Scene` and hosted descendants: non-zero bounds; only `Scene` and `SoftwareSample` own handlers.
- Tap the box/ellipse through native input; verify `TapStatus` increments and the fill changes.
- Replay animation; verify intermediate transforms change, then `AnimationStatus` returns to `Idle` and the root clock/render loop stops.
- Inspect rendered native label/button colors and a screenshot, including a compact viewport.

On 2026-09-10 all 19 headless tests and Android/iOS/Mac Catalyst diagnostic builds passed. Automated device inspection is currently blocked by `MSB4099` in the installed MAUI extension's DevFlow injection targets; no screenshot, contrast, or GPU frame-rate result is claimed. The Phase 0 device exit gate remains open.

After review fixes on the same date, all 36 headless tests passed and Android Hot Reload succeeded. The running app still had no DevFlow agent; diagnostics also reported a missing Android broker tunnel. Native rendering/contrast verification remains open.

## Phase 1 implementation

The same test command now runs **62 cases**. Phase 1 adds MAUI Grid Auto/star/span and mutation checks; Label wrapping/direct setters; Button press, commands, rounded pixels, style/default restoration and visual states; asynchronous image decode/error/stale-result checks; scroll extent/clamping/no-remeasure, tap-to-pan takeover, deterministic fling, async completion/cancellation; full-pixel scroll-composition goldens at 1x and 2x; transformed clipping/Z-order cache mutation regressions; custom-font registry resolution/caching; and 2026-09-10 review-fix regressions (Button single-command execution on tap, rounded-clip path geometry, and documented per-orientation wheel behavior). The small scroll goldens encode expected solid-color pixels directly in the test, avoiding platform-dependent font/antialias baselines.

Reproduce the non-gating measurement:

```bash
dotnet test tests/MauiSkiaUi.Tests/MauiSkiaUi.Tests.csproj --filter FullyQualifiedName~ThousandLabel --logger 'console;verbosity=detailed'
```

It constructs 1,000 labels, measures/arranges a 400x600-DIP viewport, warms one picture recording, then records 30 frames. Before clip rejection/cached Z-order: 8.106 ms and 568,384 managed bytes/frame. After: 0.635 ms and 9,488 bytes/frame on the same Mac, Debug, .NET 10. These are indicative CPU measurements, not GPU FPS; no timing assertion is used. Remaining allocations include visible labels' font/paint resources, and native allocations are excluded. Use the device stress page's **Record** action for CPU recording of 1,000 buttons, and a native profiler for actual presentation timing.

Phase 1 native acceptance checklist (still open):

- Launch Controls at compact phone and tablet/desktop sizes; inspect `ControlsHost`, `ControlsScroller`, `EarthImage`, and `AddObservation` bounds. Verify one native surface and handlerless descendants.
- Confirm offline Earth image, text wrapping, Grid columns, style colors, pressed/disabled feedback, observation count binding and reset command.
- Pan from a button: no click after threshold; fling settles; new press interrupts. Tap after scrolling must hit the translated control. Exercise Back to top and desktop wheel input.
- Navigate to Stress, scroll to the last of 1,000 buttons, tap it, run Record/Scroll/Top, and navigate back. No stale animations or extra surfaces should survive navigation.
- Query actual runtime foreground/background colors and inspect screenshots. Drawn-tree native accessibility/keyboard support is not implemented.

Android, iOS simulator, and Mac Catalyst diagnostic builds pass, including new source-generated XAML. The attempted device launch is blocked by `MSB4099` in the installed DevFlow targets, with zero registered agents. No native screenshot, visual-tree, color-contrast, GPU, or input result is claimed for Phase 1. Windows compilation and pinned-font image goldens are not verified.

## Goals

1. **Gate correctness before performance work** (NFR-2): simple implementations ship with tests; optimizations must not change observable behavior.
2. Cover **mechanisms**, not only leaf helpers: measure/arrange, paint layers/clip, animator clock, gesture delivery, hosted vs standalone.
3. Prefer tests that run **headless / in-process** on every PR; reserve device UI tests for what only a real surface can prove.
4. Make **visual regressions** detectable without relying on a human staring at the demo every change.
5. Use **AI agents** for triage and judgment of visual diffs — not as the primary CI oracle for “pixels match.”

## Short answers

| Question | Answer |
| --- | --- |
| Can painting / layout / animation be automated tests? | **Yes.** Prefer in-process harnesses that call `Measure` / `Arrange` / `Paint` (and tick animators) against an offscreen `SKSurface`, then assert geometry, events, and/or pixels. |
| Can “does it look right?” be automated? | **Yes, with golden (baseline) images** and a tolerance / SSIM comparator. This is industry standard (Flutter, Avalonia, SkiaSharp, MAUI UITest screenshots, Uno). |
| Should an AI agent be the CI pass/fail for paint? | **No as sole gate.** Use deterministic pixel/SSIM first. Use AI to **explain diffs**, suggest baseline updates vs bugs, and assist gallery review — optionally as a soft signal later. |

## Recommended test pyramid

```text
                    ┌─────────────────────┐
                    │  Device / Appium UI │  rare: overlays, IME, real GL
                    ├─────────────────────┤
                    │  Golden / visual    │  offscreen SKSurface → PNG baselines
                    ├─────────────────────┤
                    │  Mechanism tests    │  layout, paint API, anim clock, gestures
                    ├─────────────────────┤
                    │  Unit tests         │  pure helpers, easing, dirty flags, math
                    └─────────────────────┘
```

| Layer | Runs where | Typical asserts | Speed / flake risk |
| --- | --- | --- | --- |
| **Unit** | `dotnet test` (any host) | values, flags, no UI | Fast / low |
| **Mechanism** | `dotnet test` + SkiaSharp native assets | arranged rects, hit targets, paint call order, animator progress | Fast / low if clock is fakeable |
| **Golden / visual** | Same process, fixed DPI/fonts; ideally one CI OS | PNG vs baseline (± tolerance / SSIM) | Medium; pin environment |
| **Device UI** | Appium / XHarness on emulator or device | navigation, native overlay, screenshot | Slow / higher flake |

**v1 priority:** unit + mechanism + a small golden corpus. Device UI tests after `SkUiMauiContentView` and gallery exist.

---

## 1. Unit tests

Obvious and required. Examples:

- Dirty / invalidation flag transitions; `StartUpdating` / `EndUpdating` coalescing (FR-10).
- Easing and time → progress math (FR-7); no allocations in hot helpers where we claim zero-alloc.
- Clip geometry helpers; DIP ↔ pixel scale math at the root.
- Gesture classifiers in isolation (tap / double-tap / long-press / swipe thresholds) with synthetic pointer streams (FR-15).
- Layout manager edge cases that do not need a full tree (star columns, constraint clamping) when logic is extractable.

**Stack:** xUnit or NUnit + FluentAssertions (or project convention). No MAUI app host required for pure logic.

---

## 2. Mechanism tests (automated — preferred for core)

These exercise the **contracts** of the `ISkUiView` tree without launching the demo app.

### Harness shape (target)

1. Build a small tree in code (`SkUiContentView` / `SkUiLayout` + children).
2. Drive **Measure → Arrange** with known constraints (MAUI `Size` / infinite where needed).
3. For paint: create an **`SKSurface`** (software) at a fixed pixel size and DPI scale; call the root **Paint** with DIP-correct args.
4. For animation: inject a **test clock** (or call `Tick(frameTime)`) so frames are deterministic — do not depend on real vsync in unit CI.
5. For gestures: feed a synthetic pointer stream into the same delivery path the root host uses (FR-15).

This mirrors how Avalonia headless tests and Flutter widget tests work: **full pipeline, fake platform**.

### Layout (FR-3 / FR-3a / FR-13)

| What to assert | How |
| --- | --- |
| Desired size for stacks/grids under fixed/infinite constraints | Compare `Measure` results to expected sizes |
| Child arranged bounds (rows/columns, margins, alignment) | After `Arrange`, read each child’s arranged rect |
| Selective measure/arrange | Change one child; assert siblings are **not** re-entered (spy / counter on overrides) |
| Hosted vs standalone detection | Child under Skia parent has no handler; standalone creates handler (mock or flag) |

**Automation:** fully automated; no pixels required. Prefer this over goldens for layout bugs.

### Paint / layers / clip (FR-8 / FR-9 / FR-11)

| What to assert | How |
| --- | --- |
| Layer order (Background → Content → Overlay) | Test doubles that record paint order; or sample known pixels |
| Clip: corners transparent outside round-rect | Read pixels outside clip; expect clear / background |
| Transparency compositing | Overlapping translucent fills → expected blended color (± tolerance) |
| Full-tree redraw on invalidate (v1) | Invalidate one node → root paint walk still visits tree (counters) |

**Automation:** yes. Pixel reads from `SKBitmap`/`SKPixmap` are deterministic on CPU Skia when fonts and sizes are fixed.

### Animation (FR-7)

| What to assert | How |
| --- | --- |
| Progress at t = 0, mid, end | Fake clock; assert property values after tick |
| Render-transform anim does **not** dirty measure | Counters on Measure/Arrange stay 0 across ticks |
| Layout anim **does** dirty measure (when implemented) | Counters increment |
| Render loop on only while animators active | Spy on `HasRenderLoop` / continuous-invalidate flag |
| Idle after last animator completes | Registry empty → loop off |

**Automation:** yes, if the clock is injectable. Avoid “sleep 16 ms and hope” in CI.

Optional later: golden **keyframes** (t = 0%, 50%, 100%) as PNGs for a few gallery scenes.

### Gestures / hit-test (FR-15 / FR-11)

| What to assert | How |
| --- | --- |
| Hit target is arranged bounds (even if painted round) | Point in “empty” corner still hits |
| Passive label does not consume until opted in | Event/command not raised; sibling underneath receives |
| Active button handles tap without app handler | Intrinsic handler fires |
| `InputTransparent` pass-through | Hits fall through |
| Gesture sequences | Synthetic down/move/up timelines |

**Automation:** yes; pure event assertions, occasional pixel check for press visuals.

### `SkUiMauiContentView` (FR-16)

Harder to fully automate in-process (needs real handlers / platform views).

| Approach | Role |
| --- | --- |
| Mechanism stubs | Measure/arrange placeholder bounds without live Entry |
| Device / Appium | Overlay position, focus, IME, WebView — after product exists |

Treat overlay hosting as a **thin** device suite, not the bulk of CI.

---

## 3. Golden / visual regression tests (automated)

**Golden test:** render a known scene → PNG → compare to a checked-in baseline. Fail if difference exceeds threshold; attach actual / expected / diff artifacts.

### Why this fits SkiaUi

SkiaUi’s product surface **is** pixels on an `SKCanvas`. Asserting only sizes misses regressions in fill, stroke, clip, layering, and text. Offscreen `SKSurface` goldens give **Appium-free** visual coverage of the drawn tree.

### Practical rules (from peer frameworks)

- **Pin the environment:** one CI OS (e.g. Linux) generates and verifies goldens; local macOS failures may be font/AA drift, not product bugs.
- **Fix DPI, size, theme, fonts** in the harness (embed a test font for text goldens).
- **Exact match** for simple geometry; **tolerance or SSIM** for AA / text (Avalonia uses an allowed error; Verify.Avalonia can use SSIM; SkiaSharp goldens use per-renderer tolerance).
- **Update workflow:** intentional UI change → regenerate baselines → commit with the PR (Flutter `--update-goldens`; MAUI snapshot folders; SkiaSharp harvest scripts).
- **Exclude inherently non-deterministic scenes** (live clocks, random, continuous animation) or freeze time / pause animators before capture.

### Suggested corpus (start small)

- Solid / rounded button Background + Content + Overlay.
- Translucent overlapping labels (FR-8).
- Clipped round rect with content that would paint outside (FR-11).
- Simple `SkUiGrid` / stack arrangement with colored children (layout → pixels).
- One animation keyframe set (optional).

Store under e.g. `tests/MauiSkiaUi.Tests/Goldens/` with platform or renderer subfolders only if SW vs GL diverge.

---

## 4. Device / UI automation (selective)

Use when in-process tests cannot:

- Real **GL** present path vs software surface differences.
- **`SkUiMauiContentView`** overlay sync (Entry / Editor / WebView).
- End-to-end gallery smoke on Android / iOS / Mac Catalyst.

**.NET MAUI** pattern: Appium + shared NUnit tests + `VerifyScreenshot()` against per-platform snapshot folders ([MAUI UITests](https://github.com/dotnet/maui/wiki/UITests), [docs](https://learn.microsoft.com/dotnet/maui/deployment/ui-testing)).

Keep this layer **small**; Skia tree correctness should mostly be proven offscreen.

---

## 5. How other frameworks implement this

| Framework | Mechanism / headless | Visual / golden | Notes for SkiaUi |
| --- | --- | --- | --- |
| **Flutter** | Widget tests: `pumpWidget`, fake async, `pump` / `pumpAndSettle` | `matchesGoldenFile` — pixel compare + `--update-goldens`; CI on one OS; custom comparator for tolerance | Closest mental model for **tree + paint** tests without a device |
| **Avalonia** | `Avalonia.Headless` — layout/input without a real window | Enable Skia (`UseHeadlessDrawing = false`), `CaptureRenderedFrame`, compare to `.expected.png`; `Avalonia.RenderTests`; Verify.Avalonia + SSIM | Strong model for **headless Skia** + threshold |
| **SkiaSharp** | In-process scenes across backends | Golden PNG matrix per renderer; tolerance; harvest unseeded goldens from CI | Same graphics stack we paint with — reuse comparison ideas |
| **.NET MAUI** | Unit tests for non-UI; limited in-proc UI | Appium UITests + `VerifyScreenshot` baselines per platform | Use for **host app / overlays**, not primary Skia tree tests |
| **Uno Platform** | RuntimeTests in-app; Uno.UITest out-of-proc | `TakeScreenshot` + `ImageAssert`; CI sample snapshot comparer (XOR diffs) | Gallery screenshot matrix is a good **semi-automated** pattern |
| **DrawnUi** | Demo / manual gallery heavy | No first-class public golden harness analogous to Flutter/Avalonia (as of our reference checkout) | SkiaUi should **not** rely on demo-only verification |

**Takeaway:** frameworks that own a retained or immediate Skia/compositor tree almost always add **offscreen render + baseline images**. Frameworks that own platform views lean on **Appium screenshots**. SkiaUi should do **both**, with weight on the former.

---

## 6. AI agents and visual comparison

### What automation already does well

- Exact or SSIM pixel compare is **fast, cheap, deterministic**, and CI-friendly.
- Diff PNGs (actual / expected / mask) are enough for most regressions.

### Where AI helps

| Use | Automated? | Role |
| --- | --- | --- |
| Summarize a failing golden (“label shifted 4px; corner AA changed”) | Semi | Agent reads diff artifacts + optional baseline/actual images (vision) |
| Classify **bug vs intentional** after a known API change | Semi | Agent proposes approve-baseline vs fix-code; human or policy confirms |
| Compare paint to a **design brief** (“rounded 8dp primary button”) without a PNG baseline | Experimental | Weak oracle alone; useful for scaffolding new goldens |
| Walk the gallery and flag “looks broken” | Semi / nightly | Complements, does not replace, golden CI |
| Write new mechanism tests from FR checklists | Dev aid | Agent authors tests; CI still runs deterministic asserts |

Commercial **Visual AI** (e.g. Applitools Eyes) and agent-oriented tools (e.g. RegressionBot-style APIs) encode “ignore AA noise / judge layout intent.” For a library like SkiaUi, start with **local SSIM/tolerance**; add AI triage when golden volume or noise becomes painful.

### Recommended policy

1. **CI gate:** deterministic golden + mechanism tests only.
2. **PR assist:** agent (or human) reviews attached diffs when goldens fail; may suggest updating baselines when the PR description claims intentional visual change.
3. **Do not** block merge solely on an LLM “looks good” without a baseline or geometric assert.
4. Optional later: agent job that opens a draft comment with a structured diff summary (region, severity, likely cause).

---

## 7. Proposed solution structure (when we add tests)

| Project | Contents |
| --- | --- |
| `MauiSkiaUi.Tests` | Unit + mechanism + golden tests; references `MauiSkiaUi` + SkiaSharp native assets for the CI RID |
| `MauiSkiaUiDemo.UITests` (later) | Appium shared tests; sparse screenshots for host/overlay |

Shared helpers (suggested):

- `TestSurface` — create SW `SKSurface`, run measure/arrange/paint, export PNG / sample pixels.
- `FakeFrameClock` — advance animation time without vsync.
- `GoldenAssert` — compare bitmaps with configurable max ΔE / SSIM; write `*.actual.png` / `*.diff.png` on failure.
- Pointer stream builder for FR-15.

Document how to regenerate goldens (e.g. `dotnet test --filter Golden -- UpdateGoldens=true` or an env var), matching Flutter/Avalonia ergonomics.

---

## 8. Phased adoption

| Phase | Deliverable |
| --- | --- |
| **P0** | Test project; unit tests for any pure logic as it lands; correctness-before-optimize discipline (NFR-2) |
| **P1** | Mechanism harness: Measure/Arrange/Paint on SW surface; layout + dirty-tracking + gesture tests |
| **P2** | Golden corpus (~5–15 scenes); CI on one pinned OS; diff artifacts on failure |
| **P3** | Animation tests with fake clock + optional keyframe goldens |
| **P4** | Thin Appium suite for `SkUiMauiContentView` + gallery smoke |
| **P5** | Optional AI triage on golden failures / nightly gallery review |

---

## 9. Open decisions

- [ ] Test framework: xUnit vs NUnit (MAUI UITest samples often use NUnit).
- [ ] Golden compare: exact bytes vs ImageSharp Δ vs SSIM (lean SSIM/tolerance once text enters corpus).
- [ ] Whether SW goldens alone are enough for v1, or we also need a Mesa/GL lane (SkiaSharp-style) later.
- [ ] Where baselines live (repo vs generated-only on CI with promotion script).
- [ ] How aggressively to automate AI review (comment-only vs future soft check).

---

## References (external)

- Flutter: [`matchesGoldenFile`](https://api.flutter.dev/flutter/flutter_test/matchesGoldenFile.html), golden comparator docs
- Avalonia: [Headless platform / visual regression](https://docs.avaloniaui.net/docs/testing/setting-up-the-headless-platform), `Avalonia.RenderTests`
- SkiaSharp: golden-image harness / visual tests in-tree (`documentation/dev/golden-image-tests.md`)
- .NET MAUI: [UI testing](https://learn.microsoft.com/dotnet/maui/deployment/ui-testing), [UITests wiki](https://github.com/dotnet/maui/wiki/UITests) (`VerifyScreenshot`)
- Uno: [Creating automated UI tests](https://platform.uno/docs/articles/uno-development/creating-ui-tests.html), snapshot comparer in CI
- Snapshot helpers: e.g. Meziantou SkiaSharp snapshot testing (PNG + optional SSIM)
- Visual AI / agent triage: Applitools Eyes; agent-oriented regression APIs (evaluate if golden noise warrants it)

Local design refs: [Requirements.md](Requirements.md), [LayoutSystem.md](LayoutSystem.md), [DrawingMechanism.md](DrawingMechanism.md), [AnimationMechanism.md](AnimationMechanism.md), [EventMechanism.md](EventMechanism.md).
