# Gestures, behaviors and effects

Drawn views get input from SkiaUi's gesture arena, not from native gesture recognizers. Every press collects the gesture handlers of the views under the finger and of their ancestors; the innermost tap wins, a scroll, swipe or pan that moves past the touch slop beats taps, and a long press wins once it fires. Views without gesture handlers are transparent to touches.

## Recognizers

| MAUI | On a drawn view | Convert to |
| --- | --- | --- |
| `TapGestureRecognizer` | **Runs as is**, any `NumberOfTapsRequired` and `Buttons` primary / secondary: `Command` (if it can execute), then `Tapped` (sender = the view, `Parameter` = `CommandParameter`, `GetPosition(view)`). Single taps wait while a recognizer wants more taps | Optional: `TappedCommand` / `Tapped`, `DoubleTappedCommand` / `DoubleTapped` |
| `SwipeGestureRecognizer` | **Runs as is** (`Direction`, `Threshold`, `Command`, `Swiped`); a fast flick past the touch slop also swipes | Optional: `SwipeDirections` + `SwipedCommand` / `Swiped` |
| `PanGestureRecognizer` | **Runs as is** with `TouchPoints="1"` (the default); more touch points are not run | Optional: `PanUpdated` (also the release velocity, `PanAxis`) |
| `PinchGestureRecognizer` | **Runs as is** (`ScaleOrigin` 0–1 as MAUI) | Optional: `PinchUpdated` (also `TotalScale`, rotation, `Origin` in DIPs) |
| `PointerGestureRecognizer` | **Runs as is**: entered / moved / exited from hover, pressed / released for its `Buttons`. `GetPosition(null)` is `null` (use the view or another drawn element) | Optional: `IsPointerOver` and the `PointerOver` visual state; `SkUiPointerGestureRecognizer` in `Gestures` |
| `DragGestureRecognizer`, `DropGestureRecognizer` | Not run | Not available: keep that part native |
| Long press (toolkit, platform code) | — | `LongPressed` / `LongPressedCommand` + `LongPressedCommandParameter` |

Drawn views log ignored recognizers once (debug output line starting with `SkiaUi:`).

Recognizers on native views *around* a surface root (a page's background `TapGestureRecognizer`, a native `ScrollView`) stay native and need no change. On iOS / Mac Catalyst they yield to drawn gestures: an ancestor tap or long press fires only where no drawn control takes the touch, so a background tap that hides the keyboard still works over labels and empty areas.

### Recognizers added in code

`view.GestureRecognizers.Add(…)` keeps working on drawn views for every recognizer that runs as is, also when added or removed later (from the next press). Handlers keep MAUI's argument types (`PanUpdatedEventArgs`, `PinchGestureUpdatedEventArgs`, `SwipedEventArgs`, `PointerEventArgs`).

## Toolkit TouchBehavior (and subclasses)

`TouchBehavior` is a platform behavior: it attaches to a native view, which drawn views do not have. Check for subclasses (`class XyzBehavior : TouchBehavior`).

| TouchBehavior | Drawn view |
| --- | --- |
| `Command` / `CommandParameter` | `TappedCommand` / `TappedCommandParameter` |
| `LongPressCommand` / `LongPressCommandParameter` | `LongPressedCommand` / `LongPressedCommandParameter` |
| `TouchGestureCompleted` event | `Tapped` |
| `PressedScale`, `PressedOpacity`, `PressedBackgroundColor`, `DefaultAnimationDuration` | `ShowsPressEffect="True"` (the look's dim or ripple), or a `Pressed` visual state with `Scale` / `Opacity` / `BackgroundColor` setters |
| `IsEnabled` | `IsEnabled` on the view, or bind the command's `CanExecute` |
| `ShouldMakeChildrenInputTransparent` | Usually unnecessary (children without gesture handlers let touches through); otherwise `InputTransparent` on the children |

Extra work a subclass did (haptics, analytics) moves to the `Tapped` / `LongPressed` handler or into the command.

## Other behaviors and effects

- Plain behaviors that only use the view's events and bindable properties (for example `EventToCommandBehavior`) keep working.
- Any `PlatformBehavior` does not run.
- Effects (`Effects` collection, `RoutingEffect` / `PlatformEffect`) do not run. Rounded corners → `SkUiBorder StrokeShape` or `Clip`; shadows → `Shadow`; touch effects → see `TouchBehavior` above.

## Input blockers

MAUI code often adds an empty `TapGestureRecognizer` or pan recognizer (or a `TouchBehavior` without a command) only to stop touches reaching what is underneath. In a drawn tree:
- if the view under it is part of the same drawn tree and the blocker exists only for platform quirks, remove it;
- if blocking is the point (a backdrop under a drawn popover), keep the empty `TapGestureRecognizer` or add an empty `Tapped` handler: a view with a tap handler takes the tap whether or not its command can execute.

## Conflicts that go away

Workarounds for platform differences in nested gestures (a check box inside a tappable row firing both or neither, a tap firing after a swipe opened a row, `PassThrough` flags on one platform) are not needed in drawn trees: the arena resolves them the same way on every platform. Remove such workarounds when converting, and test the interaction.
