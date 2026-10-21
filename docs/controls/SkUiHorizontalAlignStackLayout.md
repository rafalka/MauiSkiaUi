# Align stack layout

`SkUiHorizontalAlignStackLayout` and its Core twin `SkUiCoreHorizontalAlignStackLayout`: a horizontal stack that places its children by their horizontal alignment, not by their place in the list. Its main use is a bar whose title stays centered whatever sits at its sides: a dialog title next to a close button (×), a page bar with a back button only.

**MAUI counterpart:** none (a `Grid` with `Auto,*,Auto` columns centers the title between the sides, not in the bar).

## How it works

One engine serves both layers (`SkUiAlignStackEngine`).

1. Children are grouped by `HorizontalOptions` (SkUi*) / `HorizontalAlignment` (Core): `Start` children are stacked from the start side, `End` children from the end side (in list order, the last one at the end), and `Center` and `Fill` children in one row in the middle. `Fill` counts as `Center`, and children are not stretched.
2. The start and end children are measured first, then the centered ones in the width left (a title truncates or wraps rather than overlap the sides).
3. The centered row is placed by `CenterStrategy`:

   | `SkUiCenterStrategy` | Centered row |
   | --- | --- |
   | `Parent` (default) | Centered in the layout's content box (inside `Padding`), whatever the sides hold; moved aside only as far as needed to clear them |
   | `AvailableSpace` | Centered in the space between the start and end children |
   | `ParentOrAvailableSpace` | Centered in the layout when it fits there; otherwise centered in the space between the sides |
   | `AvailableSpaceWhenBothSides` | `AvailableSpace` when both sides have children, else `ParentOrAvailableSpace` (a page bar: a back button alone keeps the title centered on the page) |

4. `Spacing` is the gap between children, also kept between the sides and the centered row.
5. Every child gets the full content height and aligns itself in it by its vertical alignment.

Measured size: the children plus spacing; for the strategies that center in the layout, also room for the wider side on both sides of the centered row, as far as the width allows. Arranged in a different width than measured, the children are measured again for it. Collapsed (Core: invisible) children take no space. Mirrored right to left (start is the right side).

The picker's bottom pane toolbar (Cancel and Clear at the start, the title, the close button at the end) uses it.

## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10). Invalid values: `Set*` throws; XAML, bindings, styles and the property setter ignore them with a logged warning, as MAUI does.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`). Of MAUI's `GestureRecognizers`, `TapGestureRecognizer` (1 or 2 taps) runs on the arena; other recognizers are not run and are reported once as a `Trace` line. See [EventMechanism.md](../design/EventMechanism.md#maui-gesture-recognizers).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).

## How to use

```xml
<sk:SkUiHorizontalAlignStackLayout Spacing="8" Padding="8,0" HeightRequest="44">
  <sk:SkUiLabel Text="Select a monkey" FontAttributes="Bold" MaxLines="1" LineBreakMode="TailTruncation"
                HorizontalOptions="Center" VerticalOptions="Center" />
  <sk:SkUiButton Text="Cancel" HorizontalOptions="Start" VerticalOptions="Center" />
  <sk:SkUiButton Text="×" HorizontalOptions="End" VerticalOptions="Center" />
</sk:SkUiHorizontalAlignStackLayout>
```

```csharp
var bar = new SkUiCoreHorizontalAlignStackLayout()
    .SetSpacing(8)
    .SetCenterStrategy(SkUiCenterStrategy.AvailableSpaceWhenBothSides)
    .Add(back, LayoutAlignment.Start)
    .Add(title, LayoutAlignment.Center)
    .Add(menu, LayoutAlignment.End);
```

## Key APIs

| API | Notes |
| --- | --- |
| `Spacing` | Gap between children and between the sides and the centered row (DIPs, ≥ 0) |
| `CenterStrategy` | `SkUiCenterStrategy`: `Parent` (default), `AvailableSpace`, `ParentOrAvailableSpace`, `AvailableSpaceWhenBothSides` |
| `Add(child, alignment)` (Core) | Appends a child and sets its `HorizontalAlignment`; plain `Add(child)` keeps the child's own alignment (default `Fill`: centered) |

## Related

Gallery: `HorizontalAlignStackLayoutDemoPage`, `CoreHorizontalAlignStackLayoutDemoPage`. Each shows a bar with a centered title over a "Layout center" marker, side buttons whose texts you can edit (to make a side wider or narrower) or hide, and a picker for the strategy: the title keeps to the center while it fits and moves just clear of a side that grows into it.
