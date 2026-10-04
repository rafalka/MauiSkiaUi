# SkUiBorder

A single-child host with a drawn outline of any shape, as MAUI's `Border`: background, stroke (brush, dashes, caps, joins), the content inset by the padding and the stroke, and clipped to the stroke's inner edge.

**MAUI counterpart:** [`Border`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/border)

## How it works

Extends [`SkUiContentView`](SkUiContentView.md). The background (`Background`, solid or gradient, then `BackgroundColor`) fills the outline in the Background layer; the stroke paints in the Overlay layer (after content), so opaque children cannot cover it.

- **`StrokeShape`:** any shape of either drawn layer ([`SkUiRoundRectangle`](SkUiRoundRectangle.md), [`SkUiEllipse`](SkUiEllipse.md), [`SkUiPath`](SkUiPath.md), …), MAUI's own (`RoundRectangle`, `Ellipse`, `Path`, …) or any `IShape`. XAML takes MAUI's markup: `StrokeShape="RoundRectangle 10"`, `"RoundRectangle 40,0,0,40"`, `"Ellipse"`, `"Path M 0,0 …"`, `"Polygon …"`. Without a shape the outline is a rectangle rounded by the SkiaUi shorthand `CornerRadius` (0 by default: MAUI's default rectangle). Rectangles and rounded rectangles draw with the look's rounded geometry (`SkUiLook.CreateRoundRectPath`); changing a property of the shape redraws.
- **Stroke geometry:** as MAUI on Android and Windows, the stroke is centered on the shape fitted into the bounds inset by half the stroke, so it stays inside the bounds; dashes start at the top-left (below the corner when rounded) and run clockwise, as MAUI's paths.
- **Content:** inside `Padding` plus `StrokeThickness` (MAUI's measure), clipped to the stroke's inner edge: for rounded rectangles concentric with the stroke (the radii less half the stroke), for other shapes exactly (the outline minus the stroke). The clip is applied by the compositor and cached.
- **Press effect** (`ShowsPressEffect`) follows rounded-rectangle shapes.

Core analogue: `SkUiCoreBorder` (`SetStrokeShape` with a Core shape or any `IShape`, `SetStroke(Paint)` / `SetStroke(Color)`, `SetStrokeDashArray`, …), drawn by the same geometry (same pixels, checked by `BorderShapeTests`).

## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10). Invalid values: `Set*` throws; XAML, bindings, styles and the property setter ignore them with a logged warning, as MAUI does.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`). Of MAUI's `GestureRecognizers`, `TapGestureRecognizer` (1 or 2 taps) runs on the arena; other recognizers are not run and are reported once as a `Trace` line. See [EventMechanism.md](../design/EventMechanism.md#maui-gesture-recognizers).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).

## How to use

From the MAUI docs, with only the prefix changed:

```xml
<sk:SkUiBorder Stroke="#C49B33" StrokeThickness="4" StrokeShape="RoundRectangle 40,0,0,40"
               Background="#2B0B98" Padding="16,8" HorizontalOptions="Center">
  <sk:SkUiLabel Text=".NET MAUI" TextColor="White" FontSize="18" FontAttributes="Bold" />
</sk:SkUiBorder>
```

A gradient stroke and a SkiaUi shape:

```xml
<sk:SkUiBorder StrokeThickness="3" Padding="12" StrokeDashArray="4,2">
  <sk:SkUiBorder.Stroke>
    <LinearGradientBrush EndPoint="1,0">
      <GradientStop Color="Orange" Offset="0" />
      <GradientStop Color="Brown" Offset="1" />
    </LinearGradientBrush>
  </sk:SkUiBorder.Stroke>
  <sk:SkUiBorder.StrokeShape>
    <sk:SkUiEllipse />
  </sk:SkUiBorder.StrokeShape>
  <sk:SkUiLabel Text="Ring" />
</sk:SkUiBorder>
```

The shorthand, and code:

```xml
<sk:SkUiBorder Stroke="#087F83" StrokeThickness="2" CornerRadius="16,4,4,16" BackgroundColor="White">
  <sk:SkUiLabel Text="Asymmetric corners" Padding="12" />
</sk:SkUiBorder>
```

```csharp
border.SetStrokeShape(new SkUiRoundRectangle { CornerRadius = 12 }).SetStroke(Colors.Teal).SetStrokeDashArray(4, 2);
core.SetStrokeShape(new SkUiCoreEllipse()).SetStroke(Colors.Teal).SetStrokeThickness(3);
```

## Key properties

`StrokeShape`, `Stroke` (`Brush`), `StrokeThickness` (1), `StrokeDashArray`, `StrokeDashOffset`, `StrokeLineCap`, `StrokeLineJoin`, `StrokeMiterLimit`, `CornerRadius` (shorthand, without a `StrokeShape`), plus ContentView `Content` / `Padding`.

## Differences from MAUI Border

| Topic | SkiaUi |
| --- | --- |
| `CornerRadius` | SkiaUi shorthand for `StrokeShape="RoundRectangle …"` |
| `StrokeShape` default | `null` (a rectangle rounded by `CornerRadius`); MAUI returns a `Rectangle` instance |
| Stroke position | Inside the bounds, centered on the inset shape (MAUI's Android and Windows; iOS strokes the full-size shape from inside) |
| Gradient backgrounds | Fill the outline (P7) |
| `Shadow` | With an opaque background (and an opaque or no stroke), cast from the outline's outer edge, as MAUI on Android; otherwise from what the border draws ([SkUiView.md](SkUiView.md#backgrounds-shadows-and-clips)) |
| Hit testing | Rectangular arranged bounds |

**Breaking (P6):** `Stroke` is a `Brush` (was `Color?`; `Stroke = Colors.Red` still compiles), `CornerRadius` defaults to 0 (was 6), and the content is inset by `StrokeThickness` as well as `Padding`. Core: `SkUiCoreBorder.Stroke` is a MAUI Graphics `Paint` (`SetStroke(Color)` still works).

## Related

Gallery: `BorderDemoPage`. Samples app: **Controls › Shapes and borders**.
