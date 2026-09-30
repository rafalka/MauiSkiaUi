# SkUiWrapLayout / SkUiCoreWrapLayout

Children flow left to right and wrap onto a new row when the next one does not fit: chips, tags and filter rows.

**MAUI counterpart:** none. `FlexLayout` with `Wrap="Wrap"` ([SkUiFlexLayout](SkUiFlexLayout.md)) wraps too, but has no item or row gaps.

## How it works

One engine serves both layers (`SkUiWrapEngine`), after the wrap layout in [hartez/CustomLayoutExamples](https://github.com/hartez/CustomLayoutExamples).

- Children are measured against the content width (width minus padding) with an unconstrained height.
- A child moves to a new row when it would pass the right edge. A child that starts a row never wraps, even when it is wider than the row, so there are no empty rows.
- `Spacing` separates items in a row; `RowSpacing` separates rows. A row is as tall as its tallest child.
- Each child gets a slot as wide as its desired width and as tall as its row, and aligns inside it: `VerticalOptions` (SkUi*) / `VerticalAlignment` (Core). The default `Fill` makes a row's children equally tall, as a stack's cross axis does.
- Collapsed / hidden children take no space. RTL mirrors the rows like every drawn layout.
- Arrange reuses the measured sizes when it gets the width they were measured for. Arranged at another width (measured wide, then given a narrower slot), the children are measured again against the real width first, so wrapping labels get the right rows and row heights. The Core layout allocates nothing per pass.

## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** bindables call the direct setter. Direct setters **do not** write back to the bindable store (intentional FR-10 desync). Prefer one update path per property.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).


## How to use

```xml
<sk:SkUiWrapLayout Spacing="6" RowSpacing="6" Padding="4">
  <sk:SkUiLabel Text="MAUI" CornerRadius="12" Padding="10,4" />
  <sk:SkUiLabel Text="SkiaSharp" CornerRadius="12" Padding="10,4" />
  <sk:SkUiLabel Text="Drawn UI" CornerRadius="12" Padding="10,4" />
</sk:SkUiWrapLayout>
```

```csharp
var chips = new SkUiCoreWrapLayout().SetSpacing(6).SetRowSpacing(6);
foreach (var tag in tags)
    chips.Add(new SkUiCoreLabel().SetText(tag).SetPadding(new Thickness(10, 4)).SetCornerRadius(12));
```

## Key properties

| Property | Default | Notes |
| --- | --- | --- |
| `Spacing` | 0 | Gap between items in a row (DIPs, ≥ 0) |
| `RowSpacing` | 0 | Gap between rows (DIPs, ≥ 0) |
| `Padding` | 0 | Shared layout padding |

SkUi*: bindable properties with direct setters (`SetSpacing`, `SetRowSpacing`). Core: fluent `SetSpacing`, `SetRowSpacing`, `Add`, `SetPadding`.

## Related

Gallery: `WrapLayoutDemoPage`, `CoreWrapLayoutDemoPage`.
