# SkUiUniformItemsLayout

A drawn layout of equal cells with the Community Toolkit's `UniformItemsLayout` API: every cell has the first child's size, as many columns as the width takes, at most `MaxColumns` columns and `MaxRows` rows. In the **SkiaUi.Maui.CommunityToolkit** package.

**Toolkit counterpart:** [`UniformItemsLayout`](https://learn.microsoft.com/dotnet/communitytoolkit/maui/layouts/uniformitemslayout)

## How it works

`SkUiUniformItemsLayout` implements the toolkit's `IUniformItemsLayout` and is laid out by the toolkit's public `UniformItemsLayoutManager`, so its frames are the toolkit's:

- The first visible child is measured without limits; its height is every row's, and its width decides how many columns fit (at least one, at most `MaxColumns` and the visible children).
- The width is shared equally by the columns: cells are the width divided by the columns, children arranged in them in order, row by row.
- The desired size is the columns × the first child's width by the rows × its height, plus the padding.
- Hidden children are skipped; right to left mirrors the frames like every drawn layout.

Two things differ from the toolkit's layout, which keeps its manager for its lifetime:

- **The first child's size is followed.** The toolkit's manager measures the first child once and keeps that size. The drawn layout uses a new manager each time its measure is invalid (children added or removed, a child's measure changed, an option or the constraint changed), so cells follow a first child that changes size (new text, a larger font). Children cache their measure, so nothing that has not changed is measured again.
- **Children past `MaxRows` × `MaxColumns` are hidden.** The toolkit's manager does not arrange them, so they keep an old frame. The drawn layout arranges them empty: they neither draw nor take input.

For galleries of tiles that stretch to fill the row, with an overflow tile, see [SkUiTileLayout](SkUiTileLayout.md); for many items, the collection view's grid.

## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10). Invalid values: `Set*` throws; XAML, bindings, styles and the property setter ignore them with a logged warning, as MAUI does.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`). MAUI's `GestureRecognizers` run on the arena too: tap (any `NumberOfTapsRequired`, primary or secondary button), swipe, pan, pinch and pointer recognizers; drag and drop is not run and is reported once as a `Trace` line. See [EventMechanism.md](../design/EventMechanism.md#maui-gesture-recognizers).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).

## How to use

Add the package and register it (`UseSkiaUiCommunityToolkit()`), then:

```xml
<skct:SkUiUniformItemsLayout MaxColumns="4"
    xmlns:skct="clr-namespace:MauiSkiaUi.CommunityToolkit;assembly=MauiSkiaUi.CommunityToolkit">
  <sk:SkUiImage Source="a.png" WidthRequest="64" HeightRequest="64" />
  <sk:SkUiImage Source="b.png" />
  <sk:SkUiImage Source="c.png" />
</skct:SkUiUniformItemsLayout>
```

```csharp
var cells = new SkUiUniformItemsLayout().SetMaxRows(2).SetMaxColumns(3);
foreach (var name in names)
    cells.Add(new SkUiLabel { Text = name, Padding = 8 });
```

## Key properties

| Property | Default | Notes |
| --- | --- | --- |
| `MaxRows` | `int.MaxValue` | The most rows laid out (≥ 1); children past the last row are hidden |
| `MaxColumns` | `int.MaxValue` | The most columns (≥ 1) |
| `Padding` | 0 | As every `SkUiLayout` |

`SetMaxRows` and `SetMaxColumns` are the fluent setters; they throw below 1, where the toolkit's properties throw and the drawn layout's ignore the value with a warning.

## Related

Gallery: `UniformItemsLayoutDemoPage` (side by side with the toolkit's `UniformItemsLayout`). Same package: [SkUiDockLayout](SkUiDockLayout.md).
