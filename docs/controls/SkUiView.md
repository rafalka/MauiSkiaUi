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
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`). Of MAUI's `GestureRecognizers`, `TapGestureRecognizer` (1 or 2 taps) runs on the arena, so tap XAML ports unchanged; other recognizers and platform behaviors such as `TouchBehavior` are not run and are reported once as a `Trace` line. See [EventMechanism.md](../design/EventMechanism.md#maui-gesture-recognizers).
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
| `Tag` / `SetTag` | Any object the app keeps with the view (an id, a model). Plain CLR property, not bindable; SkiaUi never reads it |
| `Focus()` / `Unfocus()` / `IsFocused` / `Focused` / `Unfocused` | MAUI's keyboard focus, on drawn views too (see below) |
| `IsTabStop` / `TabIndex` | Tab order within the surface (SkiaUi's own: MAUI 10 has none) |
| `SetSemanticFocus()` | Moves the screen reader to the view (MAUI's `SetSemanticFocus` needs a native view) |
| `IsAccessibilityEnabled` | `false`: the subtree is hidden from screen readers and keyboard focus (on a surface root: no platform bridge). App-wide: `SkUiAccessibility.IsEnabled` |
| `OnPopulateSemantics` / `OnSemanticsAction` | What the view reports to screen readers and what their actions do (own controls) |
| `Paint` / `Touch` | `ISkUiView` surface |

## Differences / extensions

- Not a MAUI `SKGLView` subclass; surface comes from `SkUiViewHandler` (Metal on Apple, GL thread on Android, `SKCanvasView` for software).
- Defaults: leaf controls `HwAccelerated = false`; hosts/layouts default `true`.
- Hit-testing uses **arranged bounds** (shape-aware hits deferred).
- **Visual states:** MAUI's `VisualStateManager` groups and setters work; `SkUiView` raises the states from SkiaUi's input state: `Disabled` (also while a control cannot be tapped, e.g. a command that cannot execute), else `PointerOver` while `IsPointerOver` (mouse, trackpad, pen or iPad pointer hover; see [EventMechanism.md](../design/EventMechanism.md#hover)), else `Normal`; `Focused` / `Unfocused` in a focus group, from keyboard focus (see below). Controls add their MAUI states (buttons `Pressed`, toggles their checked states). State triggers (`StateTrigger`, `CompareStateTrigger`, `AdaptiveTrigger`) work as in MAUI.
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

## Accessibility and keyboard

Drawn views are read by TalkBack, VoiceOver and Narrator, take keyboard focus, and follow the system text size (MAUI parity P10, [Accessibility.md](../design/Accessibility.md)). MAUI's accessibility XAML ports by changing the prefix:

```xml
<sk:SkUiLabel Text="Settings" SemanticProperties.HeadingLevel="Level1" />
<sk:SkUiImage Source="logo.png" SemanticProperties.Description="Company logo" />
<sk:SkUiImageButton Source="share.png" SemanticProperties.Description="Share"
                    SemanticProperties.Hint="Shares the order" />
<sk:SkUiCheckBox SemanticProperties.Description="Gift wrap" />
<sk:SkUiLabel Text="Gift wrap" AutomationProperties.IsInAccessibleTree="False" />
```

- **What is read.** Controls are elements with their role, name and state (buttons, check boxes, switches, radio buttons with their text content, sliders and progress bars with their value, labels with their text). Images and containers are read only with a `SemanticProperties.Description`, a `HeadingLevel` or a tap handler. A view with a tap handler (a card with `Tapped`, `TappedCommand`) is one button whose name is the text inside it; buttons inside it stay separate. A description replaces a view's text (on a container: the text inside is not read). `SemanticProperties.Hint` is read after the name. MAUI's `AutomationProperties.IsInAccessibleTree` (`False`: skip the view, not its children) and `ExcludedWithChildren` (skip the whole subtree) apply; `AutomationProperties.Name` / `HelpText` are read when no description / hint is set. `AutomationId` is exposed to UI tests.
- **Actions.** A screen reader's double tap (Narrator: invoke) runs the view's tap: `Tapped`, `TappedCommand`, a button's `Clicked` and command, a toggle's toggle. Sliders step by 5 % (swipe up / down, Narrator's range value), scroll views scroll by page, a long press handler gets the long-press action. Elements scrolled out of view are not read until scrolled in; a screen reader's focus scrolls a partly hidden element into view.
- **Keyboard focus.** MAUI's `Focus()`, `Unfocus()`, `IsFocused`, `Focused` / `Unfocused` and the `Focused` / `Unfocused` visual states work on drawn views. Interactive views take focus (buttons, image buttons, toggles, sliders, views with a tap handler) while visible and enabled; `Focus()` on a label returns `false`; on a surface root that is not focusable itself (a layout) it focuses the first focusable view. Tab / Shift+Tab move by `TabIndex`, then tree order (`IsTabStop="False"` leaves a view out) and on to native controls at either end; Space / Enter activate the focused view, arrows adjust a slider or scroll, Page Up / Down and Home / End scroll. The look draws a focus ring around the focused view while the keyboard is in use (`SkUiLook.DrawFocusRing`, [ControlLook.md](../design/ControlLook.md#focus-ring)); a pointer press hides it. Focusing a view scrolls it into view.
- **Text size.** Labels, buttons, radio button text, spans, HTML text and font images follow the system text size (Android font scale, iOS / Mac Catalyst Dynamic Type, Windows text scaling) unless `FontAutoScalingEnabled="False"`, as in MAUI. `SkUiLook.FontScale` is an app-wide prescale of all drawn text (also with auto scaling off): the drawn size is font size × `FontScale` × the system scale (for text that auto scales).
- **Switching it off:** `IsAccessibilityEnabled="False"` hides a view's subtree from screen readers and keyboard focus; on a surface root, the surface is read as one native view (its own `SemanticProperties` apply to it, as to any MAUI view). For decorative or self-described surfaces (charts, game canvases). `SkUiAccessibility.IsEnabled = false` does it app-wide (kiosks, games). Unused, accessibility costs next to nothing; text scaling stays either way.
- **Own controls** describe themselves by overriding `OnPopulateSemantics(SkUiSemanticsInfo)` (role, text, value, check state, range, actions; call the base first) and `OnSemanticsAction` / `OnSemanticsSetValue`; call `InvalidateSemantics()` when something they read changes without a redraw. Their keyboard focus follows their tap handler.
- **Reaching a surface with Tab** follows each platform's rule for buttons: always on Windows and Android; on Mac Catalyst only with System Settings › Keyboard › Keyboard navigation on (otherwise macOS tabs between text fields only), on iPad with a hardware keyboard. A click focuses the drawn control on Windows only (as WinUI); on Apple and Android pointer input does not move keyboard focus. Once a drawn control has focus (`Focus()`), Tab, Space / Enter and arrows work everywhere.
- Not yet: links inside HTML / span text as separate elements, custom actions, arrow-key moves within a radio group.

## Related

- [DrawingMechanism.md](../design/DrawingMechanism.md) · [EventMechanism.md](../design/EventMechanism.md) · [LayoutSystem.md](../design/LayoutSystem.md) · [Accessibility.md](../design/Accessibility.md)
- Demo: the **Accessibility** flyout page (semantics tree, keyboard focus, text scale). Gallery: `ViewDemoPage`; `BoxDemoPage` and `BorderDemoPage` compare backgrounds, shadows and clips with MAUI's own controls. Samples app: **Controls › Cards with shadows**.
