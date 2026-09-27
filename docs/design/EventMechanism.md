# SkiaUi event / gesture mechanism

Design and status of **FR-15** in [Requirements.md](Requirements.md): one shared gesture mechanism for the drawn tree, used by SkUi* views and Core nodes alike.

## Goal

- Most controls stay inert to input (e.g. `SkUiLabel`) and do not block what is underneath.
- Apps opt a control into gestures via **events** and / or **commands** (`Tapped` / `TappedCommand`, `LongPressed`, `Swiped`, …).
- Interactive controls (`SkUiButton`, toggles, Core buttons) handle taps and press feedback by default.
- Competing gestures resolve predictably:
  - taps versus scrolling;
  - a horizontal carousel inside a vertical page;
  - a slider inside a scroller;
  - pinch versus pan;
  - drawn versus native ancestors.

## Decision summary

| Topic | Choice |
| --- | --- |
| Primary API | **SkiaUi-owned** recognizers and a **gesture arena**; MAUI `GestureRecognizers` are not used for drawn trees |
| Layers | One implementation for SkUi* and Core: hit-testing over the shared render tree (`ISkUiRenderable`), recognizers on any node |
| Gestures | Tap, double tap, long press, swipe, pan, pinch / rotate, raw pointer; scrolling is a built-in recognizer of scroll views |
| Hit region | **Arranged bounds** (FR-11); clip / mask is paint-only; children of a scroll viewport are hit only inside the viewport |
| `InputTransparent` / invisible | Skipped: pointers reach what is underneath |
| Disabled (`IsEnabled = false`, or a button whose command cannot execute) | **Blocks**: hit, but neither it nor its subtree reacts. Core nodes have no `IsEnabled`; a Core button that cannot execute does not take part |
| Passive nodes | Transparent to pointers: the search continues **underneath** (z-order) at that point |
| Unhandled at the target | **Ancestors** on the hit path also compete (their recognizers are in the arena), so both rules apply: passive nodes pass through, and participating ancestors compete |
| Press feedback | **Immediate** when uncontested. After **`PressDelay` (100 ms)** when an ancestor competes (e.g. a scroll view), and never shown if the ancestor wins first |
| Thresholds | App-wide `SkUiGestureSettings` (touch slop 10 DIPs, press delay 100 ms, long press 500 ms, double tap 300 ms / 40 DIPs, swipe 100 DIPs or 800 DIPs/s, fling 40–3000 DIPs/s); per-recognizer overrides where useful |
| Multi-touch | One arena **per pointer**; independent presses (two buttons at once); pinch spans two pointers |
| Raw touch | `ISkUiView.Touch` / `SkUiCoreNode.Touch` is the **entry point** for a surface root. Custom interaction uses `SkUiPointerGestureRecognizer`, not a `Touch` override (**breaking**: `Touch` is no longer virtual) |
| Native ancestors | Coordinated at the surface: native parents are held back while a drawn continuous gesture may claim or owns the touch |
| MAUI recognizer bridge | Not planned |

## Architecture

```
Platform pointer (GL / Metal surface, MAUI SKCanvasView)
        │  DIPs + pointer id (surface root coordinates)
        ▼
SkUiPointerRouter (one per surface root)
        │  Press: hit-test once through the drawn tree (SkUi* + Core)
        │         → topmost node that participates (has recognizers) or blocks (disabled)
        │  Arena: recognizers of that node, then of each ancestor up to the root (innermost first)
        ▼
SkUiGestureArena (one per pointer)
        │  every member sees Pressed / Moved / Released
        │  Claim() → winner; the others are rejected (press states cleared, pans cancelled)
        │  last member standing wins by default; on release an undecided arena is won by the innermost member
        ▼
Recognizers raise events / commands on their owner (UI thread)
```

- **Built-in recognizers exist only while used.** A node's tap recognizer exists while it has `Tapped` handlers, an executable `TappedCommand`, an intrinsic tap (buttons, toggles) or double-tap handlers. Long press, swipe, pan and pinch recognizers exist while their events or commands are set.
  - Recognizers live in a per-node gesture set that is allocated on first use. Passive Core nodes carry one null field.
- **Validation per event:** members whose element was detached, hidden, made input-transparent or disabled are rejected. Detaching a subtree cancels its gestures, so press states never stick.
- **Coordinates:** recognizers receive surface-root DIPs (`SkUiPointer.Position`). `GetPosition(element)` maps through the current transforms and scroll offsets.

### Recognizers

| Recognizer | Claims when | Events |
| --- | --- | --- |
| `SkUiTapGestureRecognizer` | Wins by default (innermost on release); resigns past the touch slop | `Tapped`, `DoubleTapped` (single taps wait `DoubleTapTimeout` only when double taps are handled), `PressedChanged` |
| `SkUiLongPressGestureRecognizer` | After `Duration` within the slop | `LongPressed` |
| `SkUiPanGestureRecognizer` (`Axis`) | Past the slop along its axis (that axis dominating) | `PanUpdated` (Started / Running / Completed with velocity / Canceled) |
| `SkUiSwipeGestureRecognizer` (`Direction`, `Threshold`) | Past the slop in an allowed dominant direction (resigns otherwise) | `Swiped` on release (distance ≥ threshold, or a fast flick) |
| `SkUiPinchGestureRecognizer` | Two pointers, when the distance changes past the slop or rotation > 5° | `PinchUpdated` (Scale, TotalScale, Rotation, TotalRotation, Origin) |
| `SkUiPointerGestureRecognizer` | On press (`ClaimOnPress`, default) | `Pointer` (Pressed / Moved / Released / Cancelled) |
| Scroll (internal, scroll views) | Past the slop along an enabled axis in a direction it can move; immediately when pressed during a fling | Scrolls; chains the remainder / fling outward ([ScrollingAndCollectionViews.md](ScrollingAndCollectionViews.md#gestures-and-nesting-rules-implemented)) |

### Public surface

- **Events on `SkUiView`:**
  - `Tapped`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`.
  - Bindable `TappedCommand`, `DoubleTappedCommand`, `LongPressedCommand` and `SwipedCommand` (+ parameters; the swipe parameter defaults to the direction).
  - `SwipeDirections`, `PanAxis`, `IsPressed`.
  - The `Gestures` collection for custom recognizers.
- **Core nodes (`SkUiCoreNode`):** the same events, `SetSwipeDirections`, `SetPanAxis`, `AddGestureRecognizer` / `RemoveGestureRecognizer`, and `AutomationId`.
- **Controls:** `SkUiButton.Clicked` / `Command`, toggles and Core buttons use the intrinsic tap.
- **`SkUiGestureSettings`:** app-wide thresholds.
- **`SkUiDiagnostics.SimulateTap` / `HitTest`:** for tests and automation.

## Native coordination

The drawn arena runs inside one platform view. Native ancestors (a MAUI `ScrollView`, Shell swipe-back, …) are coordinated at the surface boundary through `SkUiPointerRouter.NativeState`:

| State | Meaning | Android (GL / MAUI surface) | iOS / Mac Catalyst (Metal) |
| --- | --- | --- | --- |
| `Pending` | A drawn continuous gesture (pan, scroll, swipe, pinch, blocking pointer recognizer) may still claim; within the slop | `RequestDisallowInterceptTouchEvent(true)` | Gate recognizer stays *Possible*; ancestor pan / pinch / swipe recognizers wait for it |
| `Claimed` | A drawn continuous gesture owns the touch | keep disallowing | Gate *Began* → ancestors fail |
| `None` | Nothing drawn wants a continuous gesture (taps only, or past the slop without a claim, e.g. a drawn scroller at its edge) | disallow released → the native parent may intercept | Gate *Failed* → ancestors proceed immediately |

This gives native "nested scrolling" too: a drawn scroller inside a native one scrolls first, and the native one takes over at the drawn edge.

## Verification

- Headless tests (`GestureTests`, `PipelineTests`, `ScrollViewTests`, `CoreLayerTests`) cover:
  - tap rules and pass-through;
  - press delay;
  - orthogonal and same-axis nested scrolling, fling hand-off, wheel routing;
  - drags and swipes inside scrollers;
  - double tap / long press with a deterministic clock;
  - pinch, multi-touch, pointer recognizers;
  - native state;
  - Core scrolling nested in SkUi* scrolling.
- Devices:
  - **Galaxy S9:** real `adb` swipes and taps. The drawn scroller scrolls with a fling, a drawn button inside it clicks, nested Core carousels and same-axis panels hand over at their edges, and row swipes work inside a vertical scroller.
  - **Physical iOS device:** long press, double tap, pinch and swipes inside drawn scroll views (checked manually, 2026-09-27).
  - **Not yet verified:** drawn scrollers inside a native MAUI `ScrollView` (the Android disallow-intercept path and the iOS gate recognizer), and Windows.

## Still open

- A demo page for gestures and nested scrollers (FR-15 checklist).
- Hover / pointer-over events and an axis-aware wheel (desktop).
- Keyboard and accessibility actions (activation of focused elements) — later.
- Shape-aware hit-testing (opt-in), if needed.

## References

- [Requirements.md](Requirements.md): FR-11, FR-13, FR-15, FR-16, FR-17.
- [ScrollingAndCollectionViews.md](ScrollingAndCollectionViews.md): scrolling rules and nesting.
- Local **Flutter**: `gestures/arena.dart`, `recognizer.dart` (arena model: accept / reject, sweep on pointer up).
- Local **Android** / **UIKit** docs: `requestDisallowInterceptTouchEvent`, `gestureRecognizer:shouldBeRequiredToFailByGestureRecognizer:`.
