# SkiaUi event / gesture mechanism

Design notes and implementation checklist for **FR-15** in [Requirements.md](Requirements.md).

## Goal

Provide one **easy, shared** gesture API that every `SkUi*` control inherits from `SkUiView`, without copying gesture state machines per control.

Apps should be able to:

- Leave most controls inert to input (e.g. `SkUiLabel`).
- Opt a control into gestures via **events** and/or **commands** (e.g. `Tapped` / `TappedCommand`).
- Rely on interactive controls (e.g. `SkUiButton`) to **handle tap by default**, including press visuals / visual states, even when no app handler is registered.

## Decision summary

| Topic | Choice |
| --- | --- |
| Primary API | **SkiaUi-owned** gestures on `SkUiView` |
| Not primary | MAUI `View.GestureRecognizers` / `GesturePlatformManager` |
| Why | Hosted children have no platform handler (FR-13); long press is not a MAUI recognizer; one path for standalone + hosted |
| Gesture set (v1) | Single tap, double tap, long press, swipe |
| Shared implementation | Classifier + delivery on the root host; participation + raise API on `SkUiView` |
| Hit-test region | **Arranged bounds** by default (FR-11); clip/mask is paint-only unless a future opt-in |
| `InputTransparent` | Honored (`IView.InputTransparent`) — skip hit-test / delivery |

Optional later: bridge classified gestures into MAUI recognizers for compatibility. **Out of scope for v1.**

## Architecture

```
Platform pointer/touch
        │
        ▼
Standalone SkUi root (handler / surface)     ← only place that maps platform → DIPs
        │
        ▼
Hit-test ISkUiView tree (z-order, arranged bounds, InputTransparent, IsEnabled)
        │
        ▼
Gesture classifier (tap / double-tap / long-press / swipe)
        │
        ▼
SkUiView participation check → raise events / execute commands / intrinsic control logic
```

Raw `ISkUiView` touch (down/move/up) remains available for controls that need continuous tracking (scroll, custom drag). The classifier builds **high-level gestures** on top of that stream; controls should prefer the shared gesture API for tap/swipe/etc. instead of reimplementing timers and thresholds.

## Participation model

A control **participates** in a gesture when at least one of the following is true:

1. **App opt-in** — an event has subscribers and/or a related command is set (and `CanExecute` is true when applicable).
2. **Intrinsic handler** — the control type opts in by default (e.g. `SkUiButton` for tap / press).

Otherwise the control is **transparent to that gesture**: hit-testing continues to views underneath (subject to siblings/z-order). A plain `SkUiLabel` with no `Tapped` handlers and no `TappedCommand` must not consume taps.

### Examples

| Control | Default | App can |
| --- | --- | --- |
| `SkUiLabel` | Passive — no gesture consumption | Subscribe to `Tapped` / set `TappedCommand` (and other gestures as exposed) |
| `SkUiButton` | Active — handles tap (and press feedback) even with no app handlers | Also use `Tapped` / `Command` for app logic; disabling / `InputTransparent` still applies |
| `SkUiLayout` / `SkUiContentView` | Passive unless opted in | Same opt-in surface as other `SkUiView`s; layouts still forward hit-test to children first |

Intrinsic vs opt-in must be implemented as a **virtual hook** on `SkUiView` (e.g. “does this type intrinsically handle tap?”), not duplicated gesture detectors in each control class.

## Public surface (target)

Exact names TBD during implementation; intent:

- **Events** on `SkUiView`: e.g. `Tapped`, `DoubleTapped`, `LongPressed`, `Swiped` (with event-args carrying position, direction for swipe, etc.).
- **Commands** (bindable): e.g. `TappedCommand` / `TappedCommandParameter`, and analogs for double-tap, long-press, swipe where useful for MVVM.
- **Fluent direct setters** for commands if FR-10 pattern applies.
- Controls like `SkUiButton` may expose a primary `Command` that aliases or shares the tap path (document clearly).

XAML / code should feel familiar to MAUI authors without requiring `GestureRecognizers` collections.

## Input rules

### `IView.InputTransparent`

When `InputTransparent == true`:

- The view is **excluded** from hit-testing.
- It does **not** receive gestures or raw touch.
- Pointers pass through to views below (or parent continues the search).

Applies equally to passive and intrinsic controls (a transparent button is not pressable).

### `IsEnabled`

When `IsEnabled == false`:

- Do **not** raise gesture events or execute gesture commands.
- **Proposed default (confirm in implementation):** disabled views still **hit-test and block** pointers (like typical MAUI buttons), unless `InputTransparent` is also true. Document the final rule in public XML docs.

### Clip / mask (FR-11)

**Paint and hit-test are decoupled by default** (same idea as UIKit `cornerRadius` / Android `clipToOutline` / MAUI buttons):

- Clip, rounded corners, and masks constrain **what is drawn**.
- Hit-testing uses the control’s **arranged layout rectangle**.
- A tap in a visually empty rounded corner that is still inside the layout rect **hits that control** (does not fall through to views underneath).

Shape-aware hit-testing (path / mask contains-point) is **out of scope for v1** as a default; if needed later, expose an opt-in override rather than coupling every clip to input.

### Platform / density

Gesture positions use the same **DIP** coordinate system as Measure / Arrange / Paint / Touch (Requirements — Decided).

## Delivery rules (v1 intent)

1. Root host converts platform events → DIP + pointer id.
2. On press: hit-test top-most eligible view (respect z-order, arranged bounds, `InputTransparent`).
3. Track capture for that press so move/up go to the press target (needed for swipe / long-press cancel if pointer leaves — define cancel thresholds).
4. Classifier emits at most the configured gestures for that interaction.
5. For each emitted gesture, ask the target (then optionally parents — see open questions) whether it participates; if yes, raise/execute and mark consumed as appropriate.
6. If the leaf does not participate, continue to the next view under the point (or bubble — see open questions).

Layouts paint and hit-test children in z-order; the gesture system must not require each layout to reimplement classification.

## What we need to implement

### Core (shared)

- [ ] Pointer/touch ingress on the **standalone root** host (handler / Skia surface), including enable-touch wiring.
- [ ] DIP mapping and multi-pointer id plumbing (v1 may document single-pointer-only if multi-touch is deferred).
- [ ] Tree **hit-test** API used by both raw touch and gestures (**arranged bounds** + `InputTransparent`; not clip/mask by default — FR-11).
- [ ] **Gesture classifier**: thresholds/timeouts for single tap, double tap, long press, swipe (direction + distance); cancel rules when movement exceeds tap slop.
- [ ] **`SkUiView` participation API**: detect event subscribers / commands; virtual `HasIntrinsic*Gesture` (or equivalent) for button-like controls.
- [ ] Raise events + execute commands on the UI thread as required by MAUI conventions.
- [ ] Consume / pass-through semantics so passive labels do not block siblings underneath.
- [ ] Honor `IsEnabled` per the documented blocking rule.
- [ ] XML docs on all public gesture members; note that MAUI `GestureRecognizers` are not used.

### Control integration

- [ ] `SkUiLabel` (and similar chrome-less text): default passive; works when user sets command or event.
- [ ] `SkUiButton`: intrinsic tap / press; visual state `Pressed` / `Disabled` via FR-12 where applicable; works with zero app handlers.
- [ ] Layouts / content host: child-first hit-test; no accidental full-rect gesture steal unless opted in.
- [ ] Avoid per-control copies of timers, swipe math, or double-tap tracking.

### Verification

- [ ] Demo: label with `TappedCommand` / `Tapped` handler.
- [ ] Demo: button without app handlers still shows press feedback and can host a `Command`.
- [ ] Demo: `InputTransparent="True"` overlay does not receive taps; view below does.
- [ ] Demo: rounded / clipped control still receives taps in layout-rect corners (paint-only clip — FR-11).
- [ ] Standalone leaf and hosted-under-`SkUiContentView` behave the same for the public gesture API.

## Open questions

Record answers here when decided; keep Requirements “Open decisions” in sync for cross-cutting items.

1. **Bubble vs sibling search:** If the top-most view under the pointer does not participate, do we walk **down the z-stack** at that point, or **bubble to parents** only, or both?
2. **Public API names:** final event/command property names; whether swipe is one event with direction vs per-direction events.
3. **Thresholds:** platform-specific vs fixed DIP timeouts/distances; configurability on `SkUiView` or app-wide defaults only.
4. **Multi-touch:** v1 single pointer only, or simultaneous independent pointers?
5. **Raw touch vs gestures:** can a control both handle raw touch and still receive classified taps? Precedence when both consume.
6. **Disabled hit-testing:** confirm “disabled still blocks” vs “disabled is pass-through.”
7. **MAUI recognizer bridge:** defer entirely, or schedule a thin optional compatibility layer later?

## References

- [Requirements.md](Requirements.md) — FR-11, FR-13, FR-15, coordinate system, dual-mode hosting
- [DrawingMechanism.md](DrawingMechanism.md) — clip/mask is paint-only by default; same arranged bounds for hit-test
- .NET MAUI — layout/`IView` only for input *properties* (`InputTransparent`, `IsEnabled`); not `GesturePlatformManager` for SkiaUi trees
- DrawnUi — useful reference for drawn-tree gesture participation / attached commands; do not copy wholesale
