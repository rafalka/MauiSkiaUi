# SkUiPath

Draws any geometry: path markup or MAUI geometry objects.

**MAUI counterpart:** [`Path`](https://learn.microsoft.com/dotnet/maui/user-interface/shapes/path)

## How it works

`Data` is a MAUI `Geometry`: path markup in XAML (`Data="M 10,100 L 100,100 100,50Z"`, MAUI's own parser: lines, curves, arcs) or geometry objects (`PathGeometry` with figures and segments, `EllipseGeometry`, `RectangleGeometry`, `LineGeometry`, `RoundRectangleGeometry`, `GeometryGroup`). The fill rule is the geometry's (`PathGeometry.FillRule`, `GeometryGroup.FillRule`), even-odd otherwise, as in MAUI. `RenderTransform` (any MAUI `Transform`) moves the placed geometry when drawing, not measure.

Replacing `Data`, its figures, or a property of a geometry object (an `EllipseGeometry`'s radius, a group's children) redraws; editing the segments inside an existing figure does not (MAUI raises that change internally), so assign the figures again after such edits. `SetData(string markup)` parses markup in code.

Core twin: `SkUiCorePath` takes a MAUI Graphics `PathF` (`SetData(PathF)`) or markup (`new SkUiCorePath("M 0,0 L 20,10 Z")`, parsed by MAUI Graphics' `PathBuilder`), `SetFillRule(WindingMode)` and `SetRenderTransform(Matrix3x2)`.

## Shape model (all shapes)

The MAUI `Shape` API, from [`SkUiShape`](SkUiShape.md): `Fill` and `Stroke` brushes (solid colors and gradients), `StrokeThickness` (1 by default), `StrokeDashArray` / `StrokeDashOffset` (in multiples of the thickness), `StrokeLineCap`, `StrokeLineJoin`, `StrokeMiterLimit`, `Aspect`. A shape without `Fill` and `Stroke` draws nothing, as in MAUI.

## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10). Invalid values: `Set*` throws; XAML, bindings, styles and the property setter ignore them with a logged warning, as MAUI does.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).

## How to use

```xml
<sk:SkUiPath Data="M 10,100 L 100,100 100,50Z" Stroke="Black" Aspect="Uniform" HorizontalOptions="Start" />

<sk:SkUiPath Stroke="Black" Fill="Gray">
  <sk:SkUiPath.Data>
    <EllipseGeometry Center="50,50" RadiusX="50" RadiusY="25" />
  </sk:SkUiPath.Data>
  <sk:SkUiPath.RenderTransform>
    <RotateTransform CenterX="0" CenterY="0" Angle="45" />
  </sk:SkUiPath.RenderTransform>
</sk:SkUiPath>
```

```csharp
var check = new SkUiPath { Stroke = Colors.Green, StrokeThickness = 3, StrokeLineCap = PenLineCap.Round }
    .SetData("M 2,10 L 8,16 L 20,4");
```

## Differences from MAUI Path

| Topic | SkiaUi |
| --- | --- |
| Markup | MAUI's parser, so the same markup; like MAUI it skips an `F0` / `F1` prefix (set the geometry's `FillRule` instead) |
| Segment edits inside a figure | Not observed (MAUI observes them internally): reassign the figures |
| Hit testing | Rectangular arranged bounds |

## Related

Gallery: `PathDemoPage`
