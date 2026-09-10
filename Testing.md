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

## Phase 2 implementation

The same test command now runs **70 cases**, adding `Phase2Tests` for `SkUiMauiContentView`: a hosted `Editor`'s Measure/Arrange contract (documenting that a handlerless `VisualElement` measures as `Size.Zero`, a MAUI platform limitation not a SkiaUi one), rejecting content that is already parented/has a handler, `ComputeRootRelativeFrame()` summing ancestor offsets (including `TranslationX`/`TranslationY`) through nested hosted layouts (Grid inside an overlay layout inside a ContentView, and a separate case with ancestor + own translation), and confirming `Touch` always returns `false` so SkiaUi's router never consumes hits meant for the native overlay. `NotifyRootAttached`/`NotifyRootDetached` are exercised as safe no-ops without a platform root (the partial-method platform hooks are simply absent on the headless `net10.0` target). A same-day review added three more regressions: `SkUiRadioButton` only selects (never unchecks) on repeated taps; `SkUiBorder` falls back to `BackgroundColor` when `Background` isn't an explicit brush; and `SkUiActivityIndicator` stops its animation clock when removed from its tree (verified against the actual shared clock instance, not a freshly-resolved one post-detach).

`SkUiBorder`, `SkUiActivityIndicator`, `SkUiImageButton`, `SkUiSwitch`, `SkUiCheckBox`, `SkUiRadioButton`, `SkUiVerticalStackLayout`, `SkUiHorizontalStackLayout`, and `SkUiAbsoluteLayout` are covered indirectly through `ComponentDemoTests` (one demo page per concrete type, editor/reset/property-check contract, no handlers required) rather than dedicated pixel goldens — this mirrors how Phase 1's Grid/Label/Button/Image/ScrollView coverage started before adding pixel-level cases, and dedicated goldens can follow if a regression surfaces.

Phase 2 native acceptance checklist (still open, in addition to the Phase 1 checklist above):

- Confirm the native `Editor`/`WebView` overlay actually renders and receives input on `MauiContentViewDemoPage`, positioned correctly over its Skia host, including after scrolling/resizing the page (v1 does not snapshot during scroll, so any positioning drift while scrolling would be visible here first). The page now composes both overlays plus `SkUiLabel`/`SkUiButton` in one `SkUiGrid`, with the Editor's HTML text live-updating the WebView (and a Refresh button as a manual fallback) — verify both the live-typing update and the button, and that the surrounding SkUi labels/button render/position correctly alongside the two native overlays in the same grid.
- Confirm the overlay repositions correctly when nested inside other layouts (Grid/StackLayout/AbsoluteLayout), not just a single ContentView, and when an ancestor's `TranslationX`/`TranslationY` changes (e.g. during a transform animation), since `ComputeRootRelativeFrame()`'s accumulation is unverified on a real native container.
- Inspect the new Basic controls' rendered colors/contrast (Switch/CheckBox/RadioButton especially, since their default palette was chosen without a device check) and the Border's rounded-clip content.
- Verify Stack/Absolute layouts' native-vs-drawn spacing and positions visually match at a few sizes/orientations.

Android, iOS, and Mac Catalyst diagnostic builds pass (0 warnings) for the full Phase 2 surface, including the new native container/overlay plumbing. Copilot-triggered device launch is still blocked by the same pre-existing `MSB4099` DevFlow injection issue; no native overlay, contrast, or input result is claimed for Phase 2. Windows compilation is not verified (no Windows host available here).

## How to verify device rendering / live-update behavior

This walks through actually proving the things the checklists above only *ask* for — using `MauiContentViewDemoPage`'s Editor→WebView live-update as the running example, since it is the hardest case (two native overlays plus SkUi controls in one grid). The same steps apply to any other checklist item; swap the `AutomationId`s.

### CLI path (no VS Code / DevFlow MCP)

Use [`scripts/device_verify.sh`](scripts/device_verify.sh) to select a simulator, emulator, physical device, or Mac Catalyst target, build/launch `MauiSkiaUiDemo` with plain `dotnet build -t:Run` (no VS Code DevFlow injection), optionally capture a screenshot, print the Phase 0–2 checklists, and stream logs:

```bash
./scripts/device_verify.sh -l                          # list targets
./scripts/device_verify.sh -p android                   # prompt / pick Android
./scripts/device_verify.sh -p ios "iPhone 16"           # iOS Simulator by name
./scripts/device_verify.sh -p maccatalyst --phase overlay
./scripts/device_verify.sh --checklist-only --phase 2   # print checklist only
./scripts/device_verify.sh -p android --screenshot      # launch + save PNG under tmp/screenshots
```

Device selection mirrors `runsim.sh`-style UX (numbered list, substring match on name/serial/UDID/AVD). After launch, work through the printed checklist by hand — this path does **not** connect a DevFlow agent. Prefer it whenever VS Code DevFlow is blocked (`MSB4099`, empty agents, missing `adb reverse`, etc.); see step 6 below.

### 1. Get a connected DevFlow agent (VS Code)

1. Select a startup project/device (**.NET MAUI: Select Startup Project**, or the `{ }` status bar item) if none is selected.
2. Launch via the MAUI debug tools, not a plain terminal `dotnet run`: `dotnet_maui_debugProject`. If a session is already active, save your files and use `dotnet_maui_runHotReload` instead — do not rebuild from the terminal while debugging.
3. Confirm an agent is actually reachable — a successful launch/Hot Reload does **not** by itself prove this: call `mcp_maui_maui_list_agents`. If it returns an empty list, do **not** assume the app is broken; see "Known DevFlow agent issues" below before retrying.
4. Once at least one agent is listed, `mcp_maui_maui_wait` (blocks until the agent is ready) then `mcp_maui_maui_capabilities` to see what the connected agent supports.

### 2. Navigate to the page under test

```
mcp_maui_maui_navigate  route: "demo-SkUiMauiContentView"
```

If that fails ("route may not exist"), the app is probably still on the gallery's home route — use `mcp_maui_maui_query` with `automationId: "OpenSkUiMauiContentView"` to get a fresh element id, then `mcp_maui_maui_tap` with that id. Element ids from a previous `maui_tree`/`maui_query` call are not stable across navigations — always re-query right before tapping.

### 3. Prove the Editor and WebView actually rendered as native overlays

```
mcp_maui_maui_tree  depth: 15
```

Look for `Editor` and `WebView` (or their platform types, e.g. `UIKit.UITextView`, `Android.Webkit.WebView`) with **non-zero bounds** positioned inside the `SkUiGrid` preview area, not stacked at `(0,0)` — that would indicate `ComputeRootRelativeFrame()` positioned them incorrectly. Then:

```
mcp_maui_maui_screenshot
```

Visually confirm both overlays are visible, correctly sized, and not overlapping the surrounding `SkUiLabel`/`SkUiButton` text.

### 4. Prove the live HTML update actually works

```
mcp_maui_maui_fill   automationId or elementId of the Editor, text: "<h1 style='color:red'>Changed</h1>"
```

(If `maui_fill` isn't supported for a native `Editor` on the connected agent's platform, use `maui_focus` + `maui_key` to type instead — check `maui_capabilities` first.) Then re-run `mcp_maui_maui_screenshot` and confirm the WebView now shows the red "Changed" heading **without** tapping "Refresh preview" — this proves the `TextChanged` live-update path, not just the manual button. Then tap the "Refresh preview" `SkUiButton` (`mcp_maui_maui_query` for `automationId: "RefreshPreview"`, then `mcp_maui_maui_tap`) and confirm the WebView is unchanged (it was already up to date) — this proves the manual path doesn't regress the automatic one.

### 5. Check contrast/colors for real, not from source

Use `mcp_maui_maui_get_property` on the `SkUiLabel`s and native controls (`TextColor`, `BackgroundColor`) — never infer contrast from XAML/C# color literals, since platform-native controls can override them. Follow up with the screenshot from step 3 if a value looks off.

### 6. If no agent ever connects

Fall back to manual verification via [`scripts/device_verify.sh`](scripts/device_verify.sh) (preferred) or an equivalent plain `dotnet build -t:Run` / IDE Play button, then visually check the same things by hand (type in the Editor, watch the WebView, eyeball contrast). Record what you actually observed — do not report a checklist item as verified without either an agent-based check or an explicit manual one.

## Known DevFlow agent issues (as of 2026-09-10)

These affect every device-verification attempt in this repo so far, on every phase. Check `dotnet_maui_diagnoseDevFlow` first before repeating any step below more than once — it works independently of the MCP connection and reports which of these you're hitting.

1. **Copilot-triggered launch fails to build (`MSB4099`).** The installed VS Code MAUI extension injects `Microsoft.Maui.DevFlow.Agent` via a `MauiDevFlow.targets` file added through `CustomAfterMicrosoftCommonTargets`. That file's top-level `PropertyGroup` condition references an item-list function (`@(PackageReference->WithMetadataValue(...))`), which MSBuild disallows outside a target, producing `MSB4099`. This blocks `dotnet_maui_debugProject`/Copilot-triggered launches entirely; it is **not** caused by anything in this repo, and no extension files have been modified to work around it. Plain `dotnet build`/`dotnet run` and [`scripts/device_verify.sh`](scripts/device_verify.sh) (no DevFlow injection) are unaffected — see the CLI path and "no agent ever connects" fallback above.
2. **CLI/agent version mismatch.** `dotnet_maui_diagnoseDevFlow` has reported the extension's bundled CLI (`0.1.0-preview.12.26368.2`) not matching the demo's referenced agent package (`0.1.0-preview.12.26421.1`, pinned in `MauiSkiaUiDemo.csproj` for Debug builds — see the "enable DevFlow" request in project history; **do not** change this pin as a generic fix for #1). When `mismatchedAgentVersions` is non-empty, expect "agent not responding" style failures even when registration succeeds. Recovery: stop the debug session, `dotnet nuget locals http-cache --clear`, rebuild.
3. **Android needs a manual `adb reverse` tunnel.** `broker_reverse_present: false` in the diagnostics above means the in-app agent cannot reach the host broker. Run the exact command the diagnostics suggest, e.g. `adb -s emulator-5554 reverse tcp:19223 tcp:19223`, then re-check with `dotnet_maui_diagnoseDevFlow`. A missing `agent_forwards` entry additionally means the host can't reach the in-app HTTP agent — that needs `adb forward tcp:<agent-port> tcp:<agent-port>` using the port the agent actually bound (from `appDebugOutput`, e.g. `9223`), not an assumed fixed port.
4. **Agent registers, then becomes unreachable.** Observed on iOS after a successful Hot Reload: `maui_list_agents` briefly showed a connected agent, but `maui_capabilities`/`maui_tree` failed afterward, and a later diagnostics call showed `agentCount: 0`. Treat a one-time successful registration as a snapshot, not a guarantee the agent stays reachable for the rest of the session — re-check with `maui_list_agents` before every device-verification attempt, not just once at the start.
5. **Android bundled DevFlow assemblies sometimes fail to load, but the agent starts anyway.** `appDebugOutput` has shown `open_from_bundles: failed to load bundled assembly Microsoft.Maui.DevFlow.Agent[.Core/.Abstractions].dll` immediately followed by `Agent started on port 9223` / `HTTP server started on port 9223`. The exact effect on functionality is unclear (not reproduced against a reachable agent yet); if Android verification behaves oddly even after fixing #3, check `appDebugOutput` for these lines and treat them as a possible contributing factor, not the settled cause.

**Bottom line:** exhaust `dotnet_maui_diagnoseDevFlow` and the fixes above once each per issue; do not loop the same MCP call expecting a different result. If still blocked, report the exact diagnostic evidence and fall back to manual verification (step 6 above) rather than claiming device behavior that was never actually observed.

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
| **DrawnUi** | Demo / manual gallery heavy | No first-class public golden harness analogous to Flutter/Avalonia | SkiaUi should **not** rely on demo-only verification |

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
