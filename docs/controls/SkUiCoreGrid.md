# SkUiCoreGrid

Core grid layout with Auto, absolute, and star tracks, optional **per-track min/max**, spans, and spacing. Owned algorithm — does **not** call MAUI `GridLayoutManager`.

**Related:** [SkUiGrid.md](SkUiGrid.md) (MAUI-compatible wrapper), [SkUiCore.md](SkUiCore.md), [SkUiCoreTable.md](SkUiCoreTable.md)

## How it works

Extends [`SkUiCorePanel`](SkUiCore.md). Child placement is a dictionary on the panel (`SetRow` / `SetColumn` / spans or `Add(child, row, col, …)`), not MAUI attached properties.

Empty `RowDefinitions` / `ColumnDefinitions` imply one star track each (MAUI parity).

Row/column definitions support Avalonia/WinUI-style clamps:

- `SkUiCoreRowDefinition.MinHeight` / `MaxHeight`
- `SkUiCoreColumnDefinition.MinWidth` / `MaxWidth`

Min and max clamp the resolved track size. For a star track, the share of leftover space is calculated first; min applies only when that share is smaller, and max only when it is larger. Leftover after a clamp is redistributed to the other stars.

Layout math lives in internal `SkUiCoreGridStructure` so subclasses (e.g. table) can reuse track metrics after arrange. **No cell/track chrome** — use [`SkUiCoreTable`](SkUiCoreTable.md) for backgrounds and separators.

## How to use

```csharp
var grid = new SkUiCoreGrid()
    .SetPadding(new Thickness(8))
    .SetRowSpacing(6)
    .SetColumnSpacing(6)
    .SetRowDefinitions([
        new SkUiCoreRowDefinition(SkUiCoreGridLength.Auto),
        new SkUiCoreRowDefinition(SkUiCoreGridLength.Star).SetMinHeight(40)
    ])
    .SetColumnDefinitions([
        new SkUiCoreColumnDefinition(new SkUiCoreGridLength(80)).SetMaxWidth(120),
        new SkUiCoreColumnDefinition(SkUiCoreGridLength.Star)
    ]);

grid.Add(new SkUiCoreLabel().SetText("Title"), 0, 0);
grid.Add(new SkUiCoreButton().SetText("OK"), 0, 1);
grid.Add(new SkUiCoreBox(), 1, 0, columnSpan: 2);

var host = new SkUiCoreHost().SetContent(grid);
```

## Key properties / APIs

| API | Role |
| --- | --- |
| `RowDefinitions` / `ColumnDefinitions` | Track sizes (`SkUiCoreGridLength` Auto / absolute / star) |
| `RowSpacing` / `ColumnSpacing` / `Padding` | Gaps and inset |
| `Add` / `SetPlacement` / `SetRow` / … | Cell indices and spans |
| `GetRowOffset` / `GetColumnOffset` / `GetRowHeight` / `GetColumnWidth` / `GetCellBounds` | Metrics after measure/arrange (for subclasses / tests) |

## Differences from MAUI / SkUiGrid

| Topic | Core |
| --- | --- |
| Children | `ISkUiCoreNode` only |
| Placement | Fluent / dictionary APIs (no `BindableObject` attached props) |
| Def min/max | Supported on Core definitions |
| Hosting | Via `SkUiCoreHost` |

## Related

Gallery: `CoreGridDemoPage` (layout / min-max editors) · Tests: `CoreGridLayoutTests` · Chrome: [`SkUiCoreTable`](SkUiCoreTable.md)
