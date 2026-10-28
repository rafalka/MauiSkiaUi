# SkUiDragger / SkUiCoreDragger

A drawn handle the user drags. It reports the drag and does nothing else itself: your handler resizes or moves whatever the handle is for. [`SkUiGridSplitter`](SkUiGridSplitter.md) is a dragger that resizes grid rows and columns.

**MAUI counterpart:** none. MAUI's `PanGestureRecognizer` reports a pan too, but its totals are relative to the view, so a handle that moves with what it resizes reads its own movement back.

## How it works

One engine serves both layers (`SkUiDraggerModel`).

- **Events:** `DragStarted` (totals 0), `DragDelta` as the pointer moves, `DragCompleted` on release or when the drag is canceled (`IsCanceled`: another recognizer took the pointer, or it was lost). `SkUiDragEventArgs` carries `TotalX` / `TotalY` (travel since the drag started) and `DeltaX` / `DeltaY` (since the previous event).
- **Travel in the parent's coordinates:** the totals are measured in the dragger's parent, so when your handler moves or resizes the dragger (a handle under a panel it resizes), the totals still follow the pointer. Take the size on `DragStarted` and set it to that size plus the total on each `DragDelta`; do not add up deltas.
- **Subclasses** override `OnDragStarted`, `OnDragDelta` and `OnDragCompleted` (call the base to raise the events), `EffectiveDragAxis` (the direction it follows now) and `CanDrag` (whether a drag can start; otherwise pointers and keys go to others).
- **Direction:** `DragAxis` is `Vertical`, `Horizontal` or `Both` (default, `SkUiPanAxis`). A one-axis dragger reports no travel across its axis, and a drag across it goes to scrollers around it. The drag starts past the touch slop and measures from where the pointer went down, so the handle does not lag behind the pointer.
- **Keyboard:** a dragger takes keyboard focus. An arrow key along its axis is a drag of `KeyboardIncrement` DIPs (default 10): one `DragStarted`, `DragDelta` and `DragCompleted` with `IsKeyboard` set. Right and Down are positive.
- **Accessibility:** a plain dragger has no value of its own; give it a `SemanticProperties.Description`, and override `OnPopulateSemantics` in a subclass that has one (the grid splitter is a slider over a track's size).
- **Thin draggers and the touch area:** a dragger takes the room its size gives it in a layout (`DefaultDraggerThickness`, 16 DIPs, across), and its touch area is at least `TouchThickness` across (default: the look's `DraggerTouchThickness`, 24 DIPs), centered on it. So a 1 DIP dragger (`HeightRequest="1"`, with `ShowsLine` it is the divider) takes no room and is still easy to grab: pointers up to 12 DIPs on either side go to it, and its grip draws over its neighbors (the node does not clip a dragger thinner than its touch area). For that, it must be on top of them: add it after them (or give it a higher `ZIndex`); a parent that clips to its bounds still cuts it off at its edge. Taps in the touch area go to the dragger, not to the content under it (a header cell's tap right at the column boundary). A disabled dragger, or a splitter with nothing to resize, takes no pointers outside its bounds.
- **Look:** the look draws an optional line along the whole dragger (`ShowsLine`, `LineColor`, `SkUiLook.DraggerLineThickness`) and a grip (`ShowsGrip`, `GripColor`, `GripLength`, the look's `DraggerGripLength` by default), twice as thick while dragged, centered on a thinner dragger. `GripAlignment` places the grip: `Start` (top, or the leading side of a horizontal bar), `Center`, `End`, or `Fill` (along all of it), `DraggerGripInset` from the edge. The line and grip run across a one-axis dragger's direction (a `Vertical` dragger is a horizontal bar) and along the longer side of a two-axis one. `Background` fills the rest. Size: `DefaultDraggerThickness` (16 DIPs) across, the grip's length along; in a layout it stretches along its row or column like any view.

## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form: getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10). Invalid values: `Set*` throws; XAML, bindings, styles and the property setter ignore them with a logged warning, as MAUI does.
- **Gestures** use SkiaUi's gesture arena. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. A dragger directly in MAUI content is a surface of its own. See [LayoutSystem.md](../design/LayoutSystem.md).

## How to use

A panel the user makes taller with a handle under it:

```xml
<sk:SkUiVerticalStackLayout>
  <sk:SkUiBorder x:Name="Notes" HeightRequest="120" />
  <sk:SkUiDragger DragAxis="Vertical" ShowsLine="True" DragStarted="OnDragStarted" DragDelta="OnDragDelta"
                  SemanticProperties.Description="Notes height" />
</sk:SkUiVerticalStackLayout>
```

```csharp
double _startHeight;

void OnDragStarted(object? sender, SkUiDragEventArgs e) => _startHeight = Notes.HeightRequest;

void OnDragDelta(object? sender, SkUiDragEventArgs e) => Notes.HeightRequest = Math.Max(48, _startHeight + e.TotalY);
```

A subclass that does its own handling:

```csharp
public class WidthHandle : SkUiDragger
{
    private double _start;

    public WidthHandle() => DragAxis = SkUiPanAxis.Horizontal;

    public required View Target { get; init; }

    protected override void OnDragStarted(SkUiDragEventArgs args)
    {
        _start = Target.Width;
        base.OnDragStarted(args);
    }

    protected override void OnDragDelta(SkUiDragEventArgs args)
    {
        Target.WidthRequest = Math.Max(80, _start + args.TotalX);
        base.OnDragDelta(args);
    }
}
```

A divider between two panes that takes no room (the splitter is last, so it is on top of the panes):

```xml
<sk:SkUiGrid RowDefinitions="*,Auto,*">
  <sk:SkUiBorder />
  <sk:SkUiBorder Grid.Row="2" />
  <sk:SkUiGridSplitter Grid.Row="1" HeightRequest="1" ShowsLine="True" />
</sk:SkUiGrid>
```

Core: the same events and fluent setters (`new SkUiCoreDragger().SetDragAxis(SkUiPanAxis.Both).SetGripAlignment(LayoutAlignment.End)`).

## Key properties

| Property | Default | Notes |
| --- | --- | --- |
| `DragAxis` | `Both` | `SkUiPanAxis`: `Both`, `Horizontal`, `Vertical` |
| `ShowsGrip` / `GripColor` | `true` / muted | The grip the look draws |
| `GripAlignment` | `Center` | `Start`, `Center`, `End`, `Fill` along the dragger |
| `GripLength` | -1 | DIPs; below 0, the look's `DraggerGripLength` (36) |
| `ShowsLine` / `LineColor` | `false` / muted | A line along the whole dragger |
| `KeyboardIncrement` | 10 | DIPs an arrow key moves it (> 0) |
| `TouchThickness` | -1 | Least touch area across the dragger, in DIPs, centered on it; below 0, the look's `DraggerTouchThickness` (24) |
| `IsDragging` | — | Read-only |

Events: `DragStarted`, `DragDelta`, `DragCompleted` (`SkUiDragEventArgs`).

## Related

Gallery: `DraggerDemoPage`, `CoreDraggerDemoPage`. [SkUiGridSplitter.md](SkUiGridSplitter.md).
