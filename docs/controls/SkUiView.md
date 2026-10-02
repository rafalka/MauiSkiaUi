# SkUiView

Base class for every Skia-drawn SkiaUi node. Implements [`ISkUiView`](../../MauiSkiaUi/ISkUiView.cs) (`IView` + `Paint` + `Touch`).

**MAUI counterpart:** none (SkiaUi infrastructure). Closest concepts: MAUI [`View`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/view) for layout properties.

## How it works

`SkUiView` owns handler-independent measure/arrange caching, Background → Content → Overlay paint (`PaintBackground` / `PaintOverlay` delegates for chrome; virtual `OnPaintContent` for structure), render transforms (translation/rotation/scale), opacity, opt-in clipping (`ClipToBounds`: on for leaves, off for layouts / content hosts, as in MAUI), tap participation, render-thread `AnimateAsync`, and access to the shared UI-thread [`SkUiAnimationClock`](../design/AnimationMechanism.md). Surfaces composite retained per-node pictures ([RenderingPipeline.md](../design/RenderingPipeline.md)). A custom MAUI handler creates a Skia surface only when the view is **standalone** in the MAUI tree.


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10). Invalid values: `Set*` throws; XAML, bindings, styles and the property setter ignore them with a logged warning, as MAUI does.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).


## How to use

Usually subclass or use a concrete control. Standalone leaf example:

```xml
<sk:SkUiBox WidthRequest="80" HeightRequest="40" Color="Teal"
            HwAccelerated="False" />
```

```csharp
var node = new SkUiLabel();
node.SetText("Hello").SetFontSize(18);
node.Tapped += (_, _) => { /* opt-in tap */ };
```

## Key APIs

| Member | Role |
| --- | --- |
| `HwAccelerated` | CLR property (not bindable). GPU vs software surface for standalone nodes. Set **before** handler creation. |
| `Tapped` / `TappedCommand` | Opt-in single tap |
| `DoubleTapped` / `LongPressed` / `Swiped` (+ commands), `PanUpdated`, `PinchUpdated` | Opt-in gestures (gesture arena) |
| `Gestures` | Custom recognizers (`SkUiPointerGestureRecognizer`, `SkUiPanGestureRecognizer`, …) |
| `IsPressed` | Shared press state for intrinsic controls |
| `ShowsPressEffect` | Draws the look's press feedback (dim or ripple) over the view and its children while pressed, clipped to its rounded shape; for cards and composite buttons with a `Tapped` / `TappedCommand` handler ([transitions](../design/ControlLook.md#state-change-transitions-fr-26)) |
| `StartUpdating` / `EndUpdating` | Coalesce invalidation |
| `InvalidatePaint` | Re-record this node's content (not its children) without remeasure. Transform / opacity / offset changes need no call: they are composite-time |
| `ClipToBounds` | Clip content, children and overlay to the arranged rect. Defaults: `true` for leaves, `false` for `SkUiLayout` / `SkUiContentView` / `SkUiCoreHost` |
| `AnimateAsync(property, to, length, easing)` | Animates `Opacity`, translation, `Rotation` or scale **on the render thread**; the bindable is updated to the final value. Setting the property meanwhile cancels it |
| `PaintBackground` / `PaintOverlay` | Chrome layer delegates (`SetPaintBackground` / `SetPaintOverlay`). Without a Background delegate the virtual `OnPaintBackground` runs; the overlay is delegate-only (no `OnPaintOverlay`). Content is virtual `OnPaintContent` only. Control chrome painters (e.g. `PaintButtonBackground`) are `protected` for subclass reuse; `PaintDefaultBackground` fills the rectangle with `ResolveBackgroundPaint()` (solid or gradient). |
| `Background` / `BackgroundColor` | MAUI's: a color, `LinearGradientBrush` or `RadialGradientBrush` (see below) |
| `Shadow` / `Clip` | MAUI's drop shadow and clip geometry, composited (see below) |
| `AnimationClock` | Shared clock of the topmost SkiaUi ancestor; local clocks are abandoned when the subtree is reparented (`OnAnimationRootChanged`) |
| `Paint` / `Touch` | `ISkUiView` surface |

## Differences / extensions

- Not a MAUI `SKGLView` subclass; surface comes from `SkUiViewHandler` (Metal on Apple, GL thread on Android, `SKCanvasView` for software).
- Defaults: leaf controls `HwAccelerated = false`; hosts/layouts default `true`.
- Hit-testing uses **arranged bounds** (shape-aware hits deferred).
- **Visual states:** MAUI's `VisualStateManager` groups and setters work; `SkUiView` raises the states from SkiaUi's input state: `Disabled` (also while a control cannot be tapped, e.g. a command that cannot execute), else `PointerOver` while `IsPointerOver` (mouse, trackpad, pen or iPad pointer hover; see [EventMechanism.md](../design/EventMechanism.md#hover)), else `Normal`; `Focused` / `Unfocused` in a focus group (no keyboard focus on drawn views yet). Controls add their MAUI states (buttons `Pressed`, toggles their checked states). State triggers (`StateTrigger`, `CompareStateTrigger`, `AdaptiveTrigger`) work as in MAUI.
- `ImageBrush` backgrounds are not drawn. Empty MAUI brushes (the default brush, a gradient without stops) do not hide `BackgroundColor`.

## Backgrounds, shadows and clips

MAUI's `Background`, `Shadow` and `Clip` work on every drawn view, so MAUI XAML that uses them ports by changing the prefix (MAUI parity P7):

```xml
<sk:SkUiBorder StrokeShape="RoundRectangle 16" StrokeThickness="0" Padding="16"
               Shadow="0 6 16 Black 0.3">
  <sk:SkUiBorder.Background>
    <LinearGradientBrush EndPoint="1,1">
      <GradientStop Color="#4F46E5" Offset="0" />
      <GradientStop Color="#0D9488" Offset="1" />
    </LinearGradientBrush>
  </sk:SkUiBorder.Background>
  <sk:SkUiLabel Text="Gradient card" TextColor="White" />
</sk:SkUiBorder>

<sk:SkUiImage Source="avatar.png" WidthRequest="56" HeightRequest="56">
  <sk:SkUiImage.Clip>
    <EllipseGeometry Center="28,28" RadiusX="28" RadiusY="28" />
  </sk:SkUiImage.Clip>
</sk:SkUiImage>
```

- **Gradient backgrounds** (`LinearGradientBrush`, `RadialGradientBrush`, mapped onto the view's bounds) fill wherever a solid background is drawn: the default rectangle, a label's or button's rounded chrome (through the look: `SkUiLook.DrawRoundedBox(…, Paint fill, …)`, `SkUiButtonPaint.FillPaint`), a border's shape, a box's corners, an image button's rounded bounds. Brush and gradient-stop changes redraw; shared brushes never keep a view alive.
- **`Shadow`** (MAUI's `Shadow`: `Brush`, solid or gradient, `Offset`, `Radius`, `Opacity`; also MAUI's `"offsetX offsetY radius color opacity"` markup). As MAUI on Android: a view with an **opaque fill** (a background, a border, a button, a box, a shape's opaque fill and undashed stroke) casts its shadow from the fill's shape, which is blurred directly and is cheap; **any other view** casts it from everything it and its children draw (text, images, transparent shapes), rasterized once on the render thread and reused while that subtree does not change. The blur radius converts to a Gaussian sigma as Android and Skia do (`0.57735 × radius + 0.5`). The shadow draws before the view, outside its own `ClipToBounds`, and changes neither layout nor hit-testing; a parent with `ClipToBounds` (or a scroll viewport, a border's shape) clips its children's shadows. The view's `Opacity` fades its shadow too. With a `Clip`, the shadow follows the clip.
- **`Clip`** (any MAUI `Geometry`, in the view's coordinates; path and group geometries keep their `FillRule`) clips the view's content, children and overlay. Drawing only: taps still hit the rectangular bounds (FR-11). Geometry edits re-clip.
- **Composite-time:** shadows and clips are render-node properties, applied when compositing. Moving, scrolling, fading or transforming a shadowed or clipped view (`TranslationX`, `AnimateAsync`, a scroll fling) records nothing and blurs nothing again; editing the shadow or the clip redraws it without recording the view's content.
- Core nodes have the same three: `SetBackground(Paint)`, `SetShadow(IShadow)` (e.g. `SkUiCoreShadow`), `SetClip(IShape)` ([SkUiCore.md](SkUiCore.md#backgrounds-shadows-and-clips)).
- Not drawn: `ImageBrush`; shape-aware hit-testing stays opt-in for later.

## Related

- [DrawingMechanism.md](../design/DrawingMechanism.md) · [EventMechanism.md](../design/EventMechanism.md) · [LayoutSystem.md](../design/LayoutSystem.md)
- Gallery: `ViewDemoPage`; `BoxDemoPage` and `BorderDemoPage` compare backgrounds, shadows and clips with MAUI's own controls. Samples app: **Controls › Cards with shadows**.
