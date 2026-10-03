# Core layer (`MauiSkiaUi.Core`)

Lightweight Skia nodes **without** MAUI `View` / `BindableObject` identity. Use them to compose complex controls or dense trees; place the tree in the MAUI-compatible surface via **`SkUiCoreHost`**.

**Contract:** [CoreRequirements.md](../design/CoreRequirements.md) (goals, FR-C*, fluent/INPC/MVVM Toolkit, Core-as-delegate sharing).

MAUI-compatible controls keep the existing names (`SkUiLabel`, `SkUiButton`, `SkUiAbsoluteLayout`, …).

## When to use which

| Layer | Types | Use for |
| --- | --- | --- |
| **MAUI-compatible** | `SkUiLabel`, `SkUiGrid`, … | XAML, styles, bindings, drop-in pages |
| **Core** | `SkUiCoreLabel`, `SkUiCoreAbsoluteLayout`, … | Building blocks inside custom controls; stress/dense lists |

## Types

| Type | Role |
| --- | --- |
| `ISkUiCoreNode` / `SkUiCoreNode` | Measure / arrange / paint / touch; fluent `Set*`; `INotifyPropertyChanged`; `PaintBackground`/`PaintOverlay` delegates + virtual `OnPaintContent`; `StartUpdating` / `EndUpdating`; `AnimationClock`; composite-time `Opacity` / `TranslationX/Y` / `Rotation` / `Scale` / `ClipToBounds` (transform-aware hit testing) and render-thread `AnimateAsync`; `Background` (`Paint`: solid or gradient), `Shadow` (`IShadow`) and `Clip` (`IShape`), see [Backgrounds, shadows and clips](#backgrounds-shadows-and-clips) |
| `SkUiCoreShadow` | Immutable drop shadow for `SetShadow`: `Paint` (or a `Color`), `Offset`, `Radius` (10), `Opacity` (1) |
| `SkUiCorePanel` | Multi-child base (attach, padding, paint, hit-test) |
| `SkUiCoreAttachedProperty<T>` | Typed per-child value a layout reads (grid row, absolute bounds, shrink factor); `node.GetValue` / `SetValue` / `ClearValue` / `IsSet`. See [Attached properties](#attached-properties) |
| `SkUiCoreAbsoluteLayout` | Absolute (+ optional proportional) layout; MAUI-compatible proportional X/Y and child alignment |
| `SkUiCoreAbsoluteLayoutFlags` | Same idea as MAUI `AbsoluteLayoutFlags` |
| `SkUiCoreVerticalStackLayout` / `SkUiCoreHorizontalStackLayout` | Stack layouts (owned algorithms; no MAUI managers); children align on the cross axis by `HorizontalAlignment` / `VerticalAlignment`, as MAUI's stacks (every Core container places nodes as MAUI's `ComputeFrame`, so Core and SkUi* frames match) |
| `SkUiCoreOverlayLayout` | Children share one slot (like `SkUiLayout`) |
| `SkUiCoreWrapLayout` | Children wrap onto new rows (`Spacing`, `RowSpacing`); same engine as `SkUiWrapLayout`, see [SkUiWrapLayout.md](SkUiWrapLayout.md) |
| `SkUiCoreHorizontalShrinkLayout` / `SkUiCoreVerticalShrinkLayout` | Stacks whose children with a shrink factor (`ShrinkProperty`: `None`, `Auto` or a number; e.g. `Add(child, SkUiShrinkFactor.Auto)`) shrink to fit; see [SkUiShrinkLayout.md](SkUiShrinkLayout.md) |
| `SkUiCoreGrid` | Auto / absolute / star grid + per-track min/max; see [SkUiCoreGrid.md](SkUiCoreGrid.md) |
| `SkUiCoreTable` | Grid + row/column/cell backgrounds and span-aware separators; see [SkUiCoreTable.md](SkUiCoreTable.md) |
| `SkUiCoreContentView` / `SkUiCoreBorder` | Single-child host; the border draws any `StrokeShape` (a Core shape or any MAUI Graphics `IShape`; without one a rectangle rounded by `CornerRadius`) with a `Paint` stroke, dashes, caps and joins, insets the content by the stroke and clips it to the shape ([SkUiBorder.md](SkUiBorder.md)) |
| `SkUiCoreLabel` / `SkUiCoreButton` | Text with the text properties of `SkUiLabel` (`FontAttributes`, `MaxLines`, `LineHeight`, `CharacterSpacing`, `TextDecorations`, `TextTransform`; wrap / truncate via `LineBreakMode` or a custom `LineBreaker`; spans: `SkUiCoreSpan` via `SetSpans`) with optional rounded chrome (`FillColor`, or a `Background` paint which replaces it; per-corner `CornerRadii`, `SetCornerRadius(double)` to set all four, `BorderColor`, `BorderWidth`: badges without a wrapping border), and the rounded tap button (`ICommand`) built on it |
| `SkUiTextLineBreaker` / `SkUiTextLineBreakers` | Custom line breaking for labels on both layers, and the stock breakers ([SkUiLabel.md](SkUiLabel.md#custom-line-breaking)) |
| `SkUiCoreToggleControl` / `CheckBox` / `RadioButton` / `Switch` | Toggles: `CheckState` (Unchecked / Checked / Indeterminate), `IsChecked` view, `IsThreeState`; Switch `IsToggled` / `Toggled`; radio buttons in the same parent exclude each other |
| `SkUiCoreSlider` | Horizontal or vertical slider (`Minimum` / `Maximum` / `Value`, drag events, `SetThumbImageSource`) |
| `SkUiCoreProgressBar` | Determinate or indeterminate (render-thread) progress bar, `ProgressTo` |
| `SkUiCoreShape` / `Ellipse` / `Line` / `Rectangle` / `RoundRectangle` / `Path` / `Polygon` / `Polyline` | MAUI's shape model with MAUI Graphics types: `Fill` / `Stroke` paints (`SetFill(Color)`, gradients), `StrokeThickness`, dashes, `LineCap` / `LineJoin`, `SkUiCoreStretch` aspect; paths from `PathF` or markup ([SkUiShape.md](SkUiShape.md)) |
| `SkUiCoreBox` | BoxView twin: `Color` (else `Background`), per-corner `CornerRadius`, 40 × 40 unless sized |
| `SkUiCoreImage` / `SkUiCoreImageButton` | Image from an `SkUiImageSource` (`SetSource`, or `SetSourceFile` (`MauiImage` / raw asset / path), `SetSourceUri`, `SetSourceStream(open, cacheKey)`, `SetSourceFont`) or a decoded `SKImage` (`SetImage`), through the cache shared with SkUi* images; `SetTransformations`, `SetDownsample`, `SetCacheType`, `SetIsAnimationPlaying`, `SetLoadingPlaceholder` / `SetErrorPlaceholder`, `LoadingStarted` / `LoadingFinished` ([SkUiImage.md](SkUiImage.md)). The button adds tap/tint, `Pressed` / `Released`, `Padding`, `CornerRadii` clip (`SetCornerRadius(double)`), border. No MAUI `ImageSource` |
| `SkUiCoreActivityIndicator` | Indeterminate spinner, rotated by the compositor on the render thread |
| `SkUiCoreScrollView` | Scroller on the shared scroll engine: offsets, render-thread fling / animated scroll, wheel (both axes), nesting with Core and SkUi* scrollers; scroll bars (`SetHorizontalScrollBarVisibility`, `SetVerticalScrollBarVisibility`), overscroll (`SetOverscroll`), snap points (`SetSnapPointsType`, `SetSnapPointsAlignment`), `ScrollToAsync(SkUiCoreNode, ScrollToPosition, bool)` / `GetScrollPositionForNode` ([SkUiScrollView.md](SkUiScrollView.md)) |
| `SkUiCoreScrollBar` | A scroll bar of a drawn scroller ([below](#scroll-bars)) |
| `SkUiCoreHost` | `SkUiView` bridge that hosts one Core root |

Core types intentionally do **not** implement `IView` and are **not** accepted by `SkUiLayout.Children`. Mixing requires `SkUiCoreHost`.

## Layout: alignment

Every Core container places a node in its slot by `HorizontalAlignment` / `VerticalAlignment` as MAUI's `ComputeFrame` places a view, so Core and SkUi* layouts give the same frames: stacks align their children on the cross axis, content views, borders, overlays and the host on both axes, grids and absolute layouts within their cells.

- `Fill` (default) takes the slot, up to `MaximumWidth` / `MaximumHeight`.
- With an explicit `Width` / `Height`, or a finite maximum, `Fill` **centers** the node in a larger slot, as MAUI does.
- `Start` / `Center` / `End` place the node's desired size.
- One difference from MAUI: a frame never grows past its slot (MAUI lets a view larger than its slot overflow it).

**Migrating from earlier versions:** before P7 most Core containers ignored alignment (a `Center`ed node in a stack stretched or sat at the start) and anchored explicitly sized `Fill` nodes at the top-left. Set `Start` alignments where a layout relied on that.

## Attached properties

Core has no bindable properties, but layouts need per-child data (a grid cell, absolute bounds, a shrink factor). That data is stored on the child as a **Core attached property**: the Core counterpart of a MAUI attached property, without bindings or styles.

- A layout declares a static `SkUiCoreAttachedProperty<T>` (name, owner type, default value, optional validation). Stock ones: `SkUiCoreGrid.RowProperty` / `ColumnProperty` / `RowSpanProperty` / `ColumnSpanProperty`, `SkUiCoreAbsoluteLayout.LayoutBoundsProperty` / `LayoutFlagsProperty`, `SkUiCoreShrinkLayout.ShrinkProperty`.
- Any node reads and writes them: `GetValue` (the default when never set), `SetValue` (validated), `ClearValue`, `IsSet`. Writing the default value is the same as `ClearValue`: `IsSet` then returns `false` (unlike MAUI, where writing the default still counts as set).
- A change raises `PropertyChanged` with the property's name and, for layout properties (`affectsParentMeasure`, the default), re-measures the node's parent. Layouts do not subscribe to their children.
- The value lives on the child: it can be set **before** the child is added, and it **stays** with the child when it is removed or moved to another layout (as in MAUI). Layouts keep no per-child tables, so removing a child needs no cleanup.
- The layout helpers (`grid.Add(child, row, column)`, `grid.SetRow`, `absolute.Add(child, bounds)`, `shrink.Add(child, SkUiShrinkFactor.Auto)`, …) write the same values. Layout-scoped setters (`SetPlacement`, `SetLayoutBounds`, `SetShrink`) still require the child to be in that layout; `SetValue` does not.
- Storage is a small array per node, created on the first value; later writes reuse each value's box, so only the first write of a property allocates and reads never do.

```csharp
var cell = new SkUiCoreLabel().SetText("B2");
cell.SetValue(SkUiCoreGrid.RowProperty, 1).SetValue(SkUiCoreGrid.ColumnProperty, 1);
grid.Add(cell); // placed by its own values
```

## Example

```csharp
var root = new SkUiCoreVerticalStackLayout()
    .SetSpacing(8)
    .SetPadding(new Thickness(12));
root.Add(new SkUiCoreLabel().SetText("Title").SetFontSize(18));
root.Add(new SkUiCoreLabel()
    .SetText("Long cell copy that wraps or truncates.")
    .SetLineBreakMode(LineBreakMode.TailTruncation));
root.Add(new SkUiCoreButton().SetText("OK").SetClicked(() => { }));
root.Add(new SkUiCoreSwitch().SetIsChecked(true));

var host = new SkUiCoreHost().SetContent(root);
scroller.SetContent(host);
```

### Core label line breaking

`SkUiCoreLabel` shares the text engine of `SkUiLabel`, with the same properties as CLR properties and `Set*` setters ([SkUiLabel.md](SkUiLabel.md)).

- `LineBreakMode` / `SetLineBreakMode(LineBreakMode)`: stock wrapping and truncation (default `WordWrap`), on HarfBuzz-shaped widths.
- `MaxLines` / `SetMaxLines(int)`: the most lines drawn (-1, the default, or 0: no limit); with `TailTruncation` the text wraps and the last line ends with the ellipsis.
- `LineBreaker` / `SetLineBreaker(SkUiTextLineBreaker?)`: a custom breaker that replaces `LineBreakMode` and can still apply it (`context.Break()`); `null` (default) uses `LineBreakMode`. Each returned line is shaped. Call `InvalidateTextLayout()` when the breaker's own inputs change.
- `LineHeight`, `CharacterSpacing`, `TextDecorations`, `TextTransform`, `FontAttributes` (+ `Set*`): as on `SkUiLabel`.
- `FlowDirection` / `SetFlowDirection` on any Core node sets the layout direction (`MatchParent` inherits from the Core parent, then from `SkUiCoreHost.FlowDirection`); RTL mirrors child frames, and labels in `Auto` follow it.
- `TextRendering` / `SetTextRendering(SkUiTextRendering)`: `Auto` (fast path for plain Latin text, HarfBuzz otherwise), `Shaped`, `Simple` (never shapes, for dense plain text / numbers) — see [SkUiLabel.md](SkUiLabel.md).
- `TextDirection` / `SetTextDirection(SkUiTextDirection)` sets the paragraph direction (`Auto` = first strong character, default). Shaping, bidi and font fallback are the same as on [`SkUiLabel`](SkUiLabel.md).

### Text and spans

`SetSpans(...)` / `AddSpan(span)` show `SkUiCoreSpan`s instead of `Text` (MAUI's `FormattedText`, [SkUiLabel.md](SkUiLabel.md#formatted-text-spans)): one paragraph through the same engine, with the label's `LineBreakMode`, `MaxLines`, alignment, padding, direction and chrome. `SetText` clears the spans and `SetSpans` clears `Text`; `SetSpans(null)` goes back to `Text`.

- **Span values** are nullable: `null` is the label's (`TextColor`, `BackgroundColor` (none), `FontFamily`, `FontSize`, `FontAttributes`, `CharacterSpacing`, `LineHeight`, `TextDecorations`); `TextTransform.Default` is the label's transform. Fluent `Set*` setters and `PropertyChanged`; a color, background or decoration change only repaints.
- **Taps:** `span.Tapped += …` makes a span tappable (the sender is the span, the position in the label's coordinates). A press on it takes the tap from the label and its ancestors; a press beside it does not. `label.SpanAt(point)` returns the span drawn at a point.
- **HTML:** `SetTextType(TextType.Html)` draws `Text` as HTML ([SkUiLabel.md](SkUiLabel.md#html-text)); `LinkTapped` reports tapped links, `LinkAt(point)` the href at a point. `SkUiHtml.ToCoreSpans(html)` gives the spans to adjust them.
- **Ownership:** a span belongs to one label at a time (`Owner`); showing it in a second label, or twice, throws. `SetSpans(null)` frees the spans.

```csharp
var link = new SkUiCoreSpan("terms").SetTextColor(Colors.Blue).SetTextDecorations(TextDecorations.Underline);
link.Tapped += (_, _) => OpenTerms();
var consent = new SkUiCoreLabel()
    .SetSpans(new SkUiCoreSpan("I accept the "), link, new SkUiCoreSpan(".").SetFontAttributes(FontAttributes.Bold))
    .SetFontSize(15);
```

## Backgrounds, shadows and clips

Every Core node has MAUI's `Background`, `Shadow` and `Clip` with MAUI Graphics types, drawn by the same engine as the SkUi* views ([SkUiView.md](SkUiView.md#backgrounds-shadows-and-clips)):

```csharp
var card = new SkUiCoreBorder().SetCornerRadius(new CornerRadius(14)).SetStrokeThickness(0).SetContent(title);
card.SetBackground(new LinearGradientPaint(
        [new PaintGradientStop(0, Colors.SkyBlue), new PaintGradientStop(1, Colors.SlateBlue)], new Point(0, 0), new Point(1, 0)))
    .SetShadow(new SkUiCoreShadow(Colors.Black, new Point(0, 4), radius: 12, opacity: 0.25f));
avatar.SetClip(new SkUiCoreEllipse());
```

- **`SetBackground(Paint)`** / **`SetBackground(Color)`**: a solid color or a linear or radial gradient mapped onto the node's bounds, drawn by the virtual `OnPaintBackground` unless a `PaintBackground` painter is set (painters can call `PaintDefaultBackground`). Filled controls fill their own shape with it instead of their color: a label's or button's rounded chrome (over `FillColor`), a border's outline (over `BackgroundColor`), a box (when `Color` is null), an image button's rounded bounds, a table under its track fills.
- **`SetShadow(IShadow)`**: a `SkUiCoreShadow` value, or any `IShadow` (MAUI's `Shadow` too, whose changes redraw). Same silhouette rules as the SkUi* layer: an opaque fill casts it from its shape, any other node from what it and its children draw.
- **`SetClip(IShape)`**: a Core shape is placed in the node's bounds (as when it shapes a border: `new SkUiCoreEllipse()` is a circle that follows the size); a MAUI geometry (`EllipseGeometry`, `RectangleGeometry`, …) is in the node's coordinates. Changes of the shape's properties re-clip. Input keeps the rectangular bounds.
- All three are composited: transforms, `AnimateAsync`, scrolling and shadow / clip edits record nothing.

## Listening to shared sources (own controls)

A custom node often follows something that outlives it: a view model, a shared `Paint` or command, an app-wide service. A plain `+=` on such a source keeps the node, and the screen it is part of, alive until it unsubscribes. The drawn controls never do that; use the same two public helpers (namespace `MauiSkiaUi`, both layers):

- **`SkUiWeakListener<TTarget>`** for sources you listen to: `INotifyPropertyChanged` objects, `INotifyCollectionChanged` collections, `ICommand.CanExecuteChanged`, and MAUI's gradient brushes and geometry groups. One subscription per source however many nodes listen, held weakly. The callback gets the target and an `SkUiChange` (`Kind`: `Property`, `Collection`, `CanExecute`, `Invalidated`; `PropertyName`; `Sender`), so it can be `static`.
- **`SkUiWeakEvent`** / **`SkUiWeakEvent<TArgs>`** for long-lived events you publish (a static service, an app setting): subscribers are not kept alive. Handlers on an object (method groups, lambdas that use only `this`) live as long as that object; static handlers and lambdas that capture locals are kept strongly, as by a plain event (a weak closure would stop firing at the next collection). `SkUiLook.CurrentChanged` and `SkUiColorScheme.CurrentChanged` work this way.

```csharp
public sealed class LegendNode : SkUiCoreNode
{
    // Store the listener: the source holds it only weakly, so one nobody keeps stops reporting.
    private readonly SkUiWeakListener<LegendNode> _modelListener;

    public LegendNode() => _modelListener = new(this, static (node, change) =>
    {
        if (change.PropertyName is nameof(ChartModel.Series)) node.InvalidateMeasure();
    });

    public LegendNode SetModel(ChartModel? model) { _modelListener.Listen(model); InvalidateMeasure(); return this; }
}

public static class Units
{
    private static readonly SkUiWeakEvent _changed = new();

    public static event EventHandler? Changed { add => _changed.Add(value); remove => _changed.Remove(value); }

    public static void Notify() => _changed.Raise(null, EventArgs.Empty);
}
```

**When not to:** what a node owns (its children, its own sub-objects) shares its lifetime; subscribe to it plainly and unsubscribe when it is replaced. A weak reference there only hides a missing cleanup. Weak listening does not replace detach cleanup either: a removed node that is still referenced is alive and keeps listening.

**Threads:** callbacks run on the thread that raised the change: a view model set from a background task, a command raising `CanExecuteChanged` from any thread. Nodes and views must be changed on the UI thread, so marshal first when the source may change elsewhere: `MainThread.BeginInvokeOnMainThread(() => node.InvalidateMeasure())`.

## Gestures

Core nodes take part in the same gesture arena as SkUi* views:
- **Events:** `Tapped`, `DoubleTapped`, `LongPressed`, `Swiped` (`SetSwipeDirections`), `PanUpdated` (`SetPanAxis`), `PinchUpdated`.
- **Custom recognizers:** `AddGestureRecognizer` (e.g. `SkUiPointerGestureRecognizer` for raw pointer handling).
- **Buttons and toggles** handle taps intrinsically.
- **Cost:** recognizers exist only while used, so passive nodes carry a single null field.
- **Entry point:** `Touch` is a dispatch entry point (usually called by `SkUiCoreHost`'s surface), not an override point.

## Diagnostics and automation

- **Visual tree:** Core nodes implement `IVisualTreeElement`, so MAUI's visual tree continues from `SkUiCoreHost` into the Core tree (Live Visual Tree, `GetVisualTreeDescendants()`, automation agents).
- **Notifications:** adds and removes are reported to `VisualDiagnostics` only when MAUI diagnostics are enabled (Debug). Release builds pay nothing.
- **`AutomationId` / `SetAutomationId`:** identify a node for automation, like MAUI's `AutomationId`.
- **`SkUiDiagnostics`:** locates drawn elements, which have no platform view:
  - `GetRootBounds` / `GetWindowBounds`: bounds after transforms and scroll offsets;
  - `HitTest` / `HitTestWindow`: the deepest element at a point;
  - `SimulateTap`: press and release through the surface, as a real touch.

## Stress comparison

Demo **Stress test** page: toggle **Core layer** to build the same two-column grid of buttons with Core nodes vs MAUI-compatible `SkUiGrid` + `SkUiButton`. **Animate** puts running `SkUiCoreActivityIndicator` / `SkUiActivityIndicator` cells in the second column on either layer. Compare Generate / Add / Render timings (see measured gap in [CoreRequirements.md](../design/CoreRequirements.md)).

## Roadmap (see CoreRequirements)

- Separate `MauiSkiaUi.Core` assembly without `Microsoft.Maui.Controls`
- `SkUiLabel` / `SkUiButton` delegate measure & paint to Core instances (today they are separate types that share the internal text engine and the `SkUiLook` chrome)

Already shipped: hand-rolled INPC on `SkUiCoreNode` with `ICommand` commands (`SkUiCoreCommand` helper); fluent `Set*` as the single apply path, with CLR setters calling `Set*`; shared painters, default sizes and state-change transitions via **`SkUiLook`** (FR-18, FR-26: Core toggles, buttons, sliders and progress bars animate like their SkUi* counterparts, through the same internal animators; `ShowsPressEffect` on any node gives a composite button, e.g. a `SkUiCoreBorder` holding an icon and labels, the look's press feedback: see the "Composite buttons" demo) and the shared palette via **`SkUiColorScheme`** / **`SkUiColors`** (FR-19) — neither is MAUI Style/VSM (FR-12); Grid, Table and ScrollView layouts.

## Scroll bars

`SkUiCoreScrollBar` is the scroll bar of `SkUiScrollView` and `SkUiCoreScrollView`: both expose theirs as `VerticalScrollBar` / `HorizontalScrollBar` (shown by `VerticalScrollBarVisibility` / `HorizontalScrollBarVisibility`). The bar is the track, a Core node pinned to the viewport edge; its thumb is a child placed by a scroll link, so the compositor moves it from the scroll offset during render-thread flings and fades it, with nothing recorded.

| Member | Meaning |
| --- | --- |
| `SkUiCoreScrollBar(SkUiScrollView, ScrollOrientation)`, `SkUiCoreScrollBar(SkUiCoreScrollView, ScrollOrientation)` | A bar placed by the app that follows the scroller from anywhere in the same surface (held weakly by the scroller) |
| `Orientation` | `Vertical` or `Horizontal` |
| `Visibility` / `SetVisibility` | `Default` (shows while scrolling, fades out), `Always`, `Never`; a scroller's own bar follows the scroller's property (setting it throws) |
| `ThumbColor` / `SetThumbColor` | `null`: the scheme's foreground at 40 % |
| `IsInteractive` / `SetIsInteractive` | A hovering pointer expands the bar and can drag the thumb or page (default `true`) |
| `IsExpanded`, `IsDragging` | Hovered or dragged (thicker, with its track); the thumb is being dragged |

```csharp
var list = new SkUiCoreScrollView().SetVerticalScrollBarVisibility(ScrollBarVisibility.Never);
var bar = new SkUiCoreScrollBar(list, ScrollOrientation.Vertical).SetVisibility(ScrollBarVisibility.Always);
var row = new SkUiCoreHorizontalStackLayout().Add(list).Add(bar); // the bar beside the list
```
