# SkUiCoreTable

Core grid subclass that paints **row/column backgrounds** and **span-aware separators** in the Background layer before children. Layout is identical to [`SkUiCoreGrid`](SkUiCoreGrid.md).

**Related:** [SkUiCoreGrid.md](SkUiCoreGrid.md), [DrawingMechanism.md](../design/DrawingMechanism.md) (layers / grid lines)

## How it works

Registers a Background painter that draws:

1. Track fills in an order controlled by **`TrackBackgroundOrder`**:
   - `ColumnsOverRows` (default) — rows first, then columns (column fills cover row fills)
   - `RowsOverColumns` — columns first, then rows (row fills cover column fills)
2. **Cell fills** from `SetCellBackground` (full arranged cell, including area behind centered children with margin; supports spans)
3. Horizontal then vertical separators at inner track edges; segments are **omitted** where a visible spanned cell crosses that boundary (Excel-style merged cells)

Geometry helpers (`GetRowBackgroundRect`, `GetRowSeparatorSegments`, …) expose the same math used for painting so unit tests can assert layout without golden images.

## How to use

```csharp
var table = new SkUiCoreTable()
    .SetRowDefinitions([
        new SkUiCoreRowDefinition(new SkUiCoreGridLength(36)),
        new SkUiCoreRowDefinition(SkUiCoreGridLength.Star)
    ])
    .SetColumnDefinitions([
        new SkUiCoreColumnDefinition(new SkUiCoreGridLength(100)),
        new SkUiCoreColumnDefinition(SkUiCoreGridLength.Star)
    ]);

table.SetRowBackground(0, Colors.LightBlue);
table.SetColumnBackground(0, Colors.LightYellow);
table.SetTrackBackgroundOrder(SkUiCoreTableTrackBackgroundOrder.ColumnsOverRows);
table.SetRowSeparatorColor(Colors.Gray)
    .SetColumnSeparatorColor(Colors.Gray)
    .SetRowSeparatorThickness(1)
    .SetColumnSeparatorThickness(1);

table.Add(new SkUiCoreLabel().SetText("Header"), 0, 0, columnSpan: 2);
table.Add(new SkUiCoreLabel().SetText("A"), 1, 0);
table.Add(new SkUiCoreLabel().SetText("B"), 1, 1);

var host = new SkUiCoreHost().SetContent(table);
```

## Key properties / APIs

| API | Role |
| --- | --- |
| `SetRowBackground` / `SetColumnBackground` | Per-track fill (`null` clears) |
| `SetCellBackground` | Per-cell fill over tracks (full cell + span) |
| `TrackBackgroundOrder` | Which track fill covers the other at intersections |
| `RowSeparatorColor` / `ColumnSeparatorColor` | Line color (`null` hides) |
| `RowSeparatorThickness` / `ColumnSeparatorThickness` | Line thickness in DIPs |
| `GetRowSeparatorSegments` / `GetColumnSeparatorSegments` | Span-aware segment rects after arrange |

## Related

Gallery: `CoreTableDemoPage` · Tests: `CoreTableLayoutTests`
