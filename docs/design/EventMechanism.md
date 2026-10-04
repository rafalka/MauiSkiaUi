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
| Hover | Outside the arena: `HoverMoved` / `HoverExited` samples set `SkUiView.IsPointerOver` on the hovered view and its ancestors (MAUI's `PointerOver` visual state); see **Hover** |
| MAUI `GestureRecognizers` | `TapGestureRecognizer` (1 or 2 taps, primary button) runs on drawn taps of drawn views, so tap XAML ports unchanged; other recognizers and platform behaviors are reported, not run. See **MAUI gesture recognizers** |

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

## MAUI gesture recognizers

MAUI connects `View.GestureRecognizers` only to views with a native view. Drawn views inside a surface have none, so SkiaUi runs the common part itself, on the arena:

- **Taps (P11a):** a `TapGestureRecognizer` in a drawn view's `GestureRecognizers` with `NumberOfTapsRequired` 1 or 2 and the primary button runs on that view's drawn taps, as MAUI raises it: its `Command` (when it can execute), then `Tapped` with the view as sender, `CommandParameter` as `Parameter` and `GetPosition(view)`. Single taps run after the view's own `Tapped` and before `TappedCommand`; a double-tap recognizer makes single taps wait, as `DoubleTapped` does. The same path serves span taps (`Span.GestureRecognizers`), screen-reader activation and Space / Enter.
- **Arena rules apply:** a view with a tap recognizer takes part like one with `Tapped`, whether or not its command can execute (as on MAUI, an empty recognizer keeps taps from the views underneath), and the innermost one wins. Recognizers added or removed at any time apply from the next press.
- **Not run (reported):** pan, swipe, pinch, pointer, drag and drop recognizers, taps with other counts or buttons, and platform behaviors (`PlatformBehavior<,>`, e.g. the toolkit's `TouchBehavior`), which attach to a native view. The first press on such a view writes one `Trace` line naming it (`AutomationId` when set) and the gesture event to use instead. A **surface root** has a native view: MAUI runs its recognizers natively, outside the arena, so taps that drawn controls handle reach them too; they are reported as well.

Drawn views keep their own API for everything else (`LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, `Gestures`). A bridge for the other recognizer types is possible (MAUI exposes `IPanGestureController`, `ISwipeGestureController`, `IPinchGestureController`) and stays open (P11b).

## Hover

A pointer moving over the surface without contact (mouse, trackpad, pen hover, iPad pointer) arrives as `SkUiTouchAction.HoverMoved`; `HoverExited` when it leaves the surface or starts touching it.

- **Sources:** Windows reports hover through SkiaSharp's touch events (`Moved` / `Entered` without contact, `Exited`). Android surfaces (GL and software) use the platform view's `Hover` event (`HoverEnter` / `HoverMove` / `HoverExit`); Apple surfaces a `UIHoverGestureRecognizer` (Mac Catalyst, iPadOS with a pointer). Both are removed when the handler disconnects. Touch-only devices never hover.
- **Routing:** `SkUiPointerRouter` hit-tests the position like a press, but passive nodes count: the topmost hit-test-visible node there and every ancestor up to the root are pointer-over, as with native hover (a parent stays hovered while the pointer is over a child). Nodes that leave the chain are told before nodes that enter it, outermost first. A disabled node ends the search, as for a press.
- **Effect:** `SkUiView.IsPointerOver` (with a property change) and `ChangeVisualState`, so `PointerOver` visual states apply. Core nodes ignore hover. Hover does not touch gesture arenas: a press while hovered runs as usual, and on release the view is still pointer-over.
- **Limits:** content that scrolls under a still pointer updates on the next pointer move; a hovered view removed from the tree is cleared on the next move; the surface disconnecting clears all.

## Native coordination

The drawn arena runs inside one platform view. Native ancestors (a MAUI `ScrollView`, Shell swipe-back, …) are coordinated at the surface boundary through `SkUiPointerRouter.NativeState`:

| State | Meaning | Android (GL / MAUI surface) | iOS / Mac Catalyst (Metal) |
| --- | --- | --- | --- |
| `Pending` | A drawn continuous gesture (pan, scroll, swipe, pinch, blocking pointer recognizer) may still claim; within the slop | `RequestDisallowInterceptTouchEvent(true)` | Gate recognizer stays *Possible*; ancestor pan / pinch / swipe recognizers wait for it |
| `Claimed` | A drawn continuous gesture owns the touch | keep disallowing | Gate *Began* → ancestors fail |
| `None` | Nothing drawn wants a continuous gesture (taps only, or past the slop without a claim, e.g. a drawn scroller at its edge) | disallow released → the native parent may intercept | Gate *Failed* → ancestors proceed immediately |

This gives native "nested scrolling" too: a drawn scroller inside a native one scrolls first, and the native one takes over at the drawn edge.

**Windows** (touch / pen; mouse drags never pan a WinUI `ScrollViewer`): SkiaSharp captures every pointer and sets `ManipulationMode = All`, which keeps DirectManipulation off. When the state becomes `None` past the slop, the handler hands the contact to an ancestor `ScrollViewer` that can scroll: it releases the capture, sets `ManipulationMode = System` and calls `TryStartDirectManipulation` (one attempt per contact). If that fails, SkiaSharp's capture and mode are restored. A surface contact that loses capture while still down (DirectManipulation, another capture, a system gesture) is cancelled in the drawn tree, so no drawn gesture keeps tracking a lost pointer.

### Drags that start on a native overlay

A native control hosted by `SkUiMauiContentView` sits above the drawn surface, so its touches never reach the drawn tree by themselves. Each overlay's clip wrapper therefore also offers them to the drawn tree (`SkUiPointerRouter.DispatchFromOverlay`).
- **Who competes:** only the **continuous** recognizers (scroll, pan, swipe, pinch) of the overlay's drawn ancestors. Taps, text selection and cursor placement stay native.
- **Handover:** once a drawn gesture claims the pointer (e.g. a vertical drag in a drawn scroller), the native control's touch is cancelled and the rest of the drag goes to the drawn tree.
  - **Android:** `SkUiOverlayClip.OnInterceptTouchEvent`, like a native scrolling parent.
  - **iOS / Mac Catalyst:** `SkUiOverlayDragRecognizer` on the clip wrapper. It stays *Possible* while nothing drawn has claimed, so the native control works as usual; it begins when a drawn gesture claims, which cancels the native touches; it fails as soon as nothing drawn can claim.
- **Native precedence:** controls that scroll their own content keep it, as they do inside a native MAUI `ScrollView`:
  - a WebView, and MAUI's Android `Editor`, which blocks its parents from intercepting;
  - on iOS, a scrollable `UITextView` / `WKWebView`, whose own pan begins first.
- **Windows:** `OverlayDragWatcher` in `SkUiOverlayContainer`, touch and pen only (mouse drags stay native: text selection).
  - `handledEventsToo` pointer handlers on the clip canvas see the native control's events; once the contact is down, its moves, release and cancel are also followed on the overlay container and the window's root content, so a finger lifted outside the overlay still ends the drawn gesture.
  - On a claim the overlay container captures the pointer, so the native control loses it. If the native control's own manipulation (DirectManipulation) takes the contact first, the drawn side gets a cancel.
  - The overlay a drag started on stays live (not replaced by its snapshot) until the drag ends: WinUI drops a contact whose element is hidden.
  - A wheel (touchpad two-finger scroll) the native control does not use is forwarded to the drawn tree. WebView2 takes wheel input itself, so the list does not scroll under a WebView.
  - Verified on a Windows touch laptop (2026-09-28; `WindowsValidation-results.md`).
- **Verified on a Galaxy S9:** a slow vertical drag starting on an Entry scrolls the drawn list without focusing the Entry; a tap on the Entry still focuses it (keyboard shown); a drag on the Editor stays native.

**The iOS gate resolves on its own touches too.**
- **Why:** a touch the drawn view never receives (e.g. the tap that stops a decelerating native `ScrollView` is consumed by the scroll view) would otherwise leave the gate *Possible*, with every ancestor pan waiting for it. The page would freeze until the next touch that reaches the drawn tree.
- **Rules:**
  - at touch-down, the gate fails at once when an ancestor `UIScrollView` is still decelerating or dragging (that touch belongs to the native scroll view);
  - it fails at touch end if still undecided;
  - as a last resort, it fails when the pointer moved past the slop and the drawn tree still has not seen the touch 300 ms after it began. A normal touch reaches content views up to ~150 ms late (`delaysContentTouches`), so failing earlier would give quick drags on drawn scrollers to the native page.
- **Both surface types:** the gate is attached to the Metal view and to software (`SKCanvasView`) surfaces.
- **Software surfaces on iOS / Mac Catalyst:** they don't use SkiaSharp's touch recognizer.
  - A delivering recognizer (`SkUiTouchDeliverer`) never changes state and cannot be prevented. It feeds touches to the drawn tree, then updates the gate, in that order (two independent recognizers on one view give no ordering guarantee).
  - When a native ancestor scroll view starts dragging, it cancels the drawn gesture itself, which the scroll view does for content views but not for recognizers.

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
  - **Galaxy S9, drawn surfaces inside a native MAUI `ScrollView`** (demo page "Native nesting"), GPU and software surfaces, real swipes:
    - a drawn same-axis list scrolls first, and a new drag at its end scrolls the page;
    - a drawn carousel takes horizontal drags, and vertical drags on it scroll the page;
    - a drawn row with no scroller takes swipes and taps, and vertical drags on it scroll the page;
    - a Core scroll view behaves like the drawn list.
  - **iPhone:** GPU and software surfaces both verified: nested scrolling works, and the page no longer freezes after a tap-to-stop (2026-09-27).
  - **Windows 11, mouse** (2026-09-27, GPU and software surfaces; [WindowsValidation-results.md](WindowsValidation-results.md)): press feedback and clicks, drawn scroller drag / fling / wheel, a drag that starts on a button scrolls without clicking, Core carousel, same-axis panel hand-off to the page, row swipe, long press, double tap.
  - **Windows 11, "Native nesting", mouse** (2026-09-28, GPU and software): drawn and Core lists take drags without moving the page; the wheel scrolls a list to its end and then the page; wheel over native filler scrolls the page; carousel drag, row swipe and tap work. Mouse drags never pan the native page (WinUI `ScrollViewer` pans only for touch / pen). A vertical wheel over a horizontal-only drawn scroller scrolls it horizontally.
  - **Windows touch laptop** (2026-09-28, touchscreen and touchpad): drawn drags and flicks; drags that start on Entry fields scroll the drawn list (snapshot mode, the live overlay stays aligned); taps still focus them; "Native nesting" hand-over to the native page; touchpad wheel over an Entry / Editor scrolls the list; lost contacts no longer leave scrolling stuck; pinch.
  - **By design:** once a drawn scroller has claimed a drag, the rest of that drag stays drawn. It chains only to drawn outer scrollers; Android cannot hand a gesture back to a native parent mid-drag. The native page takes over on the next drag.

## Still open

- A dedicated SkUi* gesture demo page. Gestures and nested scrollers are shown today on the Core "ScrollView + gestures" page, `ViewDemoPage` (`InputTransparent`) and "Native nesting".
- Hover / pointer-over events and an axis-aware wheel (desktop).
- Keyboard focus and accessibility actions shipped with P10 ([Accessibility.md](Accessibility.md)): keys go to the focused drawn node through the surface's focus manager, not through the gesture arena; screen-reader actions call the nodes' tap paths. Still open: public key events for app controls.
- Shape-aware hit-testing (opt-in), if needed.

## References

- [Requirements.md](Requirements.md): FR-11, FR-13, FR-15, FR-16, FR-17.
- [ScrollingAndCollectionViews.md](ScrollingAndCollectionViews.md): scrolling rules and nesting.
- Local **Flutter**: `gestures/arena.dart`, `recognizer.dart` (arena model: accept / reject, sweep on pointer up).
- Local **Android** / **UIKit** docs: `requestDisallowInterceptTouchEvent`, `gestureRecognizer:shouldBeRequiredToFailByGestureRecognizer:`.
