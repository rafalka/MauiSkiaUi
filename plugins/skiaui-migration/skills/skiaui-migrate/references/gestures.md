# Gestures, behaviors and effects

Drawn views get input from SkiaUi's gesture arena, not from native gesture recognizers. Every press collects the gesture handlers of the views under the finger and of their ancestors; the innermost tap wins, a scroll, swipe or pan that moves past the touch slop beats taps, and a long press wins once it fires. Views without gesture handlers are transparent to touches.

## Recognizers

| MAUI | On a drawn view | Convert to |
| --- | --- | --- |
| `TapGestureRecognizer` (1 tap, primary button) | **Runs as is**: `Command` (if it can execute), then `Tapped` (sender = the view, `Parameter` = `CommandParameter`, `GetPosition(view)`) | Optional: `TappedCommand="…" TappedCommandParameter="…"` or `Tapped="Handler"` |
| `TapGestureRecognizer NumberOfTapsRequired="2"` | **Runs as is** (single taps wait for the double-tap timeout) | Optional: `DoubleTappedCommand` / `DoubleTapped` |
| `TapGestureRecognizer` with 3+ taps or `Buttons="Secondary"` | Not run | No drawn equivalent: rethink the interaction |
| `SwipeGestureRecognizer` | Not run | `SwipeDirections="Left,Right"` + `SwipedCommand` (parameter defaults to the `SwipeDirection`) or `Swiped` (`e.Direction`) |
| `PanGestureRecognizer` | Not run | `PanUpdated` (`e.Status`: MAUI's `GestureStatus`; `e.TotalX`, `e.TotalY`; `e.VelocityX`, `e.VelocityY` on `Completed`), `PanAxis` |
| `PinchGestureRecognizer` | Not run | `PinchUpdated` (`e.Status`, `e.Scale` per update as MAUI, `e.TotalScale`, `e.Rotation`, `e.Origin` in DIPs relative to the view; MAUI's `ScaleOrigin` was 0–1) |
| `PointerGestureRecognizer` | Not run | Hover: `IsPointerOver` and the `PointerOver` visual state. Raw input: `SkUiPointerGestureRecognizer` in `Gestures` |
| `DragGestureRecognizer`, `DropGestureRecognizer` | Not run | Not available: keep that part native |
| Long press (toolkit, platform code) | — | `LongPressed` / `LongPressedCommand` + `LongPressedCommandParameter` |

Drawn views log ignored recognizers once (debug output line starting with `SkiaUi:`).

### Swipe with a command per direction

```xml
<!-- MAUI -->
<Grid.GestureRecognizers>
    <SwipeGestureRecognizer Direction="Left" Command="{Binding NextCommand}" />
    <SwipeGestureRecognizer Direction="Right" Command="{Binding PreviousCommand}" />
</Grid.GestureRecognizers>
```

Either one command that takes the direction (`SwipeDirections="Left,Right" SwipedCommand="{Binding PageCommand}"`, with the view model switching on `SwipeDirection`), or code-behind:

```csharp
grid.SwipeDirections = SwipeDirection.Left | SwipeDirection.Right;
grid.Swiped += (_, e) =>
{
    var command = e.Direction == SwipeDirection.Left ? vm.NextCommand : vm.PreviousCommand;
    if (command.CanExecute(null)) command.Execute(null);
};
```

A `Threshold` other than the default: add `new SkUiSwipeGestureRecognizer { Direction = …, Threshold = 50 }` to the view's `Gestures` collection and handle its event, instead of `SwipeDirections`.

### Pan handlers

MAUI pan handlers port almost unchanged: subscribe to `PanUpdated` on the drawn view instead of the recognizer's `PanUpdated`, and change the argument type to `SkUiPanUpdatedEventArgs`. Animating `TranslationX` / `TranslationY` from a pan works; for the settle animation after `Completed`, prefer `view.AnimateAsync(SkUiAnimatableProperty.TranslationY, target, 250, Easing.CubicOut)` (render thread).

### Recognizers added in code

`view.GestureRecognizers.Add(new TapGestureRecognizer { … })` keeps working on drawn views, also when added or removed later. Other recognizer types added in code must become the drawn events above. Handlers removed when a page closes: unsubscribe the drawn events the same way.

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
