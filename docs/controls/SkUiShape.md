# SkUiShape

Abstract base of the drawn shapes, with MAUI's [`Shape`](https://learn.microsoft.com/dotnet/maui/user-interface/shapes/) API: [`SkUiEllipse`](SkUiEllipse.md), [`SkUiLine`](SkUiLine.md), [`SkUiRectangle`](SkUiRectangle.md), [`SkUiRoundRectangle`](SkUiRoundRectangle.md), [`SkUiPath`](SkUiPath.md), [`SkUiPolygon`](SkUiPolygon.md) and [`SkUiPolyline`](SkUiPolyline.md). Each has a Core twin (`SkUiCoreEllipse`, …) drawn by the same engine. [`SkUiBox`](SkUiBox.md) (BoxView) is not a shape, as in MAUI.

**MAUI counterpart:** [`Shape`](https://learn.microsoft.com/dotnet/api/microsoft.maui.controls.shapes.shape) ([shapes overview](https://learn.microsoft.com/dotnet/maui/user-interface/shapes/))

## Properties

| Property | Default | Notes |
| --- | --- | --- |
| `Fill` | `null` | `Brush`: `Fill="Red"`, `LinearGradientBrush`, `RadialGradientBrush`. A fill gradient spans the view's bounds |
| `Stroke` | `null` | `Brush` of the outline; a stroke gradient spans the outline's bounds (as MAUI's `ShapeDrawable`) |
| `StrokeThickness` | 1 | DIPs; part of the measured size |
| `StrokeDashArray` | empty | Dashes and gaps in multiples of `StrokeThickness` (`"4,2"`); an odd count repeats once more, as in SVG. Edits of the collection redraw |
| `StrokeDashOffset` | 0 | Where the pattern starts, in multiples of `StrokeThickness` |
| `StrokeLineCap` | `Flat` | `Flat`, `Round`, `Square`: ends of open figures and of dashes |
| `StrokeLineJoin` | `Miter` | `Miter`, `Round`, `Bevel` |
| `StrokeMiterLimit` | 10 | Miter length ÷ half the thickness before a miter is beveled |
| `Aspect` | `None` (`Fill` on rectangles and ellipses) | `Stretch`: `None`, `Fill`, `Uniform`, `UniformToFill` |

Fluent setters: `SetFill`, `SetStroke`, `SetStrokeThickness`, `SetStrokeDashArray(params double[])`, `SetStrokeDashOffset`, `SetStrokeLineCap`, `SetStrokeLineJoin`, `SetStrokeMiterLimit`, `SetAspect` (they return `SkUiShape`).

## How it works

Shapes measure, stretch and place their geometry as MAUI's `Shape` does:

- **Measure:** the geometry's bounds plus the stroke. Unstretched geometry (`Aspect="None"`: lines, paths, polygons) reaches from the origin to its far points, so a shape sizes itself without `WidthRequest` / `HeightRequest`. `Fill` takes the whole constraint; `Uniform` / `UniformToFill` scale the geometry to it. Rectangles and ellipses have no size of their own: they fill what they are given (and measure as the stroke in an unbounded direction).
- **Placement:** the drawing area is the bounds inset by half the stroke, so the stroke stays inside. Without stretch the geometry keeps its coordinates and only moves to bring an edge that sticks out over the left / top (or else the right / bottom) back in. `Uniform` centers the scaled geometry; `UniformToFill` anchors it at the top-left.
- **Drawing:** the fill, then the stroke over it, recorded once and replayed by the compositor until a property changes. Brush, dash-array, point and geometry changes redraw; shared brushes (resources) are listened to weakly and never keep a shape alive.
- Shapes are MAUI `IShape`s (`PathForBounds`), so any of them shapes a [`SkUiBorder`](SkUiBorder.md) (`StrokeShape`).

Hit testing uses the rectangular arranged bounds. Points are not mirrored in right-to-left layouts.

## Core

`SkUiCoreShape` (`SkUiCoreEllipse`, `SkUiCoreLine`, `SkUiCoreRectangle`, `SkUiCoreRoundRectangle`, `SkUiCorePath`, `SkUiCorePolygon`, `SkUiCorePolyline`) has the same properties with MAUI Graphics types: `Paint` for `Fill` / `Stroke` (`SetFill(Color)` and `SetStroke(Color)` for solid colors, or a `LinearGradientPaint` / `RadialGradientPaint`), `LineCap` / `LineJoin` (`Butt` is MAUI's `Flat`), `SkUiCoreStretch` for `Aspect`, `WindingMode` for fill rules. Same measure and the same pixels as the SkUi* shapes (checked by `ShapeTests`).

```csharp
var ring = new SkUiCoreEllipse().SetStroke(Colors.Teal).SetStrokeThickness(4).SetStrokeDashArray(2, 1);
var arrow = new SkUiCorePath("M 0,0 L 20,10 L 0,20 Z").SetFill(Colors.Teal);
```

## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10). Invalid values: `Set*` throws; XAML, bindings, styles and the property setter ignore them with a logged warning, as MAUI does.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).

## Differences from MAUI

| Topic | SkiaUi |
| --- | --- |
| `ImageBrush` | Not drawn |
| `Uniform` under one unbounded constraint | Scaled by the bounded one (MAUI scales by 0 and collapses the shape to its stroke) |
| `UniformToFill` measure | Clamped to the constraint (MAUI may report more) |
| Sized shapes | Measure to their requests (as MAUI with a platform view) |
| Hit testing | Rectangular arranged bounds |

**Breaking (P6):** `Color` and `StrokeWidth` were replaced by `Fill` / `Stroke` and `StrokeThickness` (default 1, was 2). Shapes no longer default to a teal fill, and no longer measure 48 × 48 without a size.

## Related

Demo pages: `EllipseDemoPage`, `RectangleDemoPage`, `RoundRectangleDemoPage`, `LineDemoPage`, `PolygonDemoPage`, `PolylineDemoPage`, `PathDemoPage` (side by side with MAUI's shapes). Samples app: **Controls › Shapes and borders**.
