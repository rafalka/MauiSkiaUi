# SkUiTileLayout / SkUiCoreTileLayout

Equal tiles in rows, as many as the width takes: photo galleries, attachment strips and blocks of cards on a page. With a row limit, the last tile can show an overflow tile ("+5") over its child.

**MAUI counterpart:** none. A MAUI page does it with a `Grid` whose columns are rebuilt in `SizeChanged`, or a `FlexLayout` with fixed child sizes. For many items (a whole photo library), use the collection view with a `GridItemsLayout` ([SkUiCollectionView](SkUiCollectionView.md)): the tile layout measures and draws every child it shows, it does not virtualize.

## How it works

One engine serves both layers (`SkUiTileEngine`), as the wrap layout's does.

- **Columns:** the fewest columns whose tiles are at most `TileSize` wide, so the tiles are as large as they may be: 300 DIPs of 100-DIP tiles is three columns of 100, 301 DIPs is four of 75.25. `ColumnSpacing` sits between columns.
- **Tile size:** the tiles stretch to fill the row, never past `TileSize`. When they would be smaller than `MinimumTileSize`, a column is dropped (down to one); the tiles then stay at `TileSize` and what is left of the row stays at its end (the left in RTL).
- **Height:** a tile is `TileAspectRatio` wide for each unit of its height (1: square; 16/9: wide). `RowSpacing` sits between rows.
- Each child is measured for its tile and arranged in it, and aligns inside it by its options (SkUi\*) / alignment (Core). The default `Fill` fills the tile.
- Collapsed / hidden children take no tile. RTL mirrors the columns as in every drawn layout.
- Unconstrained width (a horizontal stack or scroll view): one row of every tile at `TileSize`.
- The layout's desired width is the width its tiles take: with fewer children than columns, the rest of the row is not asked for.

### Row limit and overflow tile

- `MaximumRows` (-1, the default: no limit) cuts the rows. The visible children past them get no tile: they are not measured, drawn, hit or read by screen readers, and stay the layout's children (removing one is cheap). When the limit lifts, they are measured and drawn again.
- `Overflow` (an `SkUiTileOverflow`) holds `HiddenCount`, how many children did not fit, and raises `PropertyChanged` when it changes.
- With an overflow template, the last tile shows its view over the child in it, sized to the tile: the template's binding context is `Overflow`, so `Text="{Binding HiddenCount, StringFormat='+{0}'}"` over a dimming background is the usual "+5". On Core, `OverflowTemplate` is a function that makes the node once, given `Overflow`.
- A tap on the overflow tile raises `OverflowTapped` and runs `OverflowCommand` (with `OverflowCommandParameter`, by default `Overflow` on SkUi\*; `Overflow` on Core). Without a template, the rows are only cut.

## How to use

```xml
<sk:SkUiTileLayout TileSize="96" ColumnSpacing="4" RowSpacing="4" MaximumRows="2"
                   OverflowCommand="{Binding ShowAllPhotosCommand}"
                   BindableLayout.ItemsSource="{Binding Photos}">
  <BindableLayout.ItemTemplate>
    <DataTemplate>
      <sk:SkUiImage Source="{Binding Thumbnail}" Aspect="AspectFill" />
    </DataTemplate>
  </BindableLayout.ItemTemplate>
  <sk:SkUiTileLayout.OverflowTemplate>
    <DataTemplate>
      <sk:SkUiLabel Text="{Binding HiddenCount, StringFormat='+{0}'}" Background="#8C000000" TextColor="White"
                    FontSize="22" HorizontalTextAlignment="Center" VerticalTextAlignment="Center" />
    </DataTemplate>
  </sk:SkUiTileLayout.OverflowTemplate>
</sk:SkUiTileLayout>
```

```csharp
var photos = new SkUiCoreTileLayout().SetTileSize(96).SetColumnSpacing(4).SetRowSpacing(4).SetMaximumRows(2)
    .SetOverflowTemplate(overflow =>
    {
        var count = new SkUiCoreLabel().SetText($"+{overflow.HiddenCount}").SetTextColor(Colors.White);
        overflow.PropertyChanged += (_, _) => count.SetText($"+{overflow.HiddenCount}");
        return count;
    });
foreach (var photo in thumbnails)
    photos.Add(new SkUiCoreImage().SetSource(photo).SetAspect(Aspect.AspectFill));
```

## Key properties

| Property | Default | Notes |
| --- | --- | --- |
| `TileSize` | 120 | The largest tile (DIPs, > 0) |
| `MinimumTileSize` | 0 | Smaller tiles drop a column (DIPs, ≥ 0) |
| `TileAspectRatio` | 1 | Tile width per unit of height (> 0) |
| `ColumnSpacing` / `RowSpacing` | 0 | Gaps between tiles (DIPs, ≥ 0) |
| `MaximumRows` | -1 | -1: no limit; else at least 1 |
| `OverflowTemplate` | none | SkUi\*: a `DataTemplate` (or selector) of a drawn view; Core: `Func<SkUiTileOverflow, SkUiCoreNode>` |
| `OverflowCommand` | none | Run when the overflow tile is tapped; also `OverflowTapped` |
| `Overflow` | | Read only: `HiddenCount` |
| `Columns` | | Read only: the columns at the last layout |
| `Padding` | 0 | Shared layout padding |

SkUi\*: bindable properties with fluent setters (`SetTileSize`, `SetMaximumRows`, …). Core: fluent `Set*`, `Add`, `SetPadding`.

## Related

Gallery: `TileLayoutDemoPage`, `CoreTileLayoutDemoPage`. Migration: [grids rebuilt to fit equal tiles](../Migration.md#equal-tiles).
