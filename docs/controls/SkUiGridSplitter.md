# SkUiGridSplitter / SkUiCoreGridSplitter

A [`SkUiDragger`](SkUiDragger.md) that resizes the rows or columns of the grid it is in: drag it, or focus it and use the arrow keys. It works in tables too: resizable columns with grips in the header.

**MAUI counterpart:** none (MAUI has no splitter). The model is the `GridSplitter` of WPF, Avalonia and WinUI.

## How it works

One engine serves both layers (`SkUiGridSplitterEngine`, `SkUiGridSplitterMath`); the drag, keyboard, line and grip are the dragger's.

- **Which tracks (`ResizeBehavior`, `SkUiGridResizeBehavior`):**
  - `BasedOnAlignment` (default), from the splitter's alignment along the resize direction (`HorizontalOptions` for columns): `End` is `CurrentAndNext`, `Start` is `PreviousAndCurrent`, `Fill` / `Center` is `PreviousAndNext`.
  - `PreviousAndNext`: the splitter has a row (or column) of its own, usually an `Auto` one, between the two it resizes.
  - `CurrentAndNext`: the splitter sits at the end of a cell (a table header's column boundary) and resizes its own track and the next one. `PreviousAndCurrent` is the same at a cell's start.
  - `CurrentOnly`: only the splitter's track; it becomes absolute and the grid grows or shrinks with it (columns of a table in a horizontal scroller).
  - The tracks need definitions of their own; a splitter without them does nothing, and pointers and arrow keys go to others.
- **Direction:** `ResizeDirection` is `Rows`, `Columns` or `Auto` (default). `Auto` resizes columns when the splitter is aligned horizontally (`HorizontalOptions` not `Fill`) and fills vertically, rows the other way round; otherwise rows when it spans every column of a grid with several rows, columns when it spans every row of a grid with several columns, otherwise by its shape (taller than wide: columns). The splitter follows its resize direction; `DragAxis` does not apply.
- **What a drag writes:** the first track (above the boundary, or at its start) takes the pointer's travel since it went down, and the second gives it up; the two keep their total size.
  - Two star tracks get new star weights in the new proportion (their weights keep their sum), so they keep sharing a resized page.
  - Next to a star track, only the other track changes, to an absolute size; the star track takes what is left. With other star tracks in the grid, they share the change too.
  - `Auto` and absolute tracks become absolute.
  - `CurrentOnly`: the track becomes absolute; nothing else changes.
- **Limits:**
  - SkUi\*: MAUI's `RowDefinition` / `ColumnDefinition` have no minimum or maximum, so the splitter has attached properties for them: `SkUiGridSplitter.MinimumLength` and `MaximumLength` on a definition. They limit splitter drags only, not the grid's layout.
  - Core: the definitions' `MinHeight` / `MaxHeight` (`MinWidth` / `MaxWidth`), which limit the layout as well.
  - A track never goes below 0, nor past the two tracks' total.
- **Where it works:** in a `SkUiGrid`, and, as a drawn surface of its own, in MAUI's `Grid` (the grid can mix native and drawn children). The Core splitter works in a `SkUiCoreGrid` and a [`SkUiCoreTable`](SkUiCoreTable.md#resizable-columns).
- **Track sizes on the SkUi layer:** MAUI's grid does not expose the sizes it arranged, so the splitter works them out when a drag starts: absolute tracks at their length, `Auto` tracks at their largest child in that track alone, star tracks sharing the rest by weight. A track sized by children spanning several tracks, or star tracks in a grid that sizes to its content, may come out a little different; the drag then starts with a small jump. The Core grid exposes its sizes, so the Core splitter is exact.
- **Following the pointer:** the splitter moves under the pointer while the grid lays out again; the dragger measures the travel in the grid's coordinates, so the boundary stays under the finger.
- **Keyboard and accessibility:** a splitter takes keyboard focus. Up / Down move a row splitter, Left / Right a column splitter (mirrored right to left), by `KeyboardIncrement` DIPs. Screen readers see a slider whose value is the size of the first track (Home / End: its smallest and largest size; a track resized alone with no maximum reads as a range up to twice its size); increment and decrement move it by `KeyboardIncrement`. Give it a `SemanticProperties.Description` that says what it resizes.
- **Look:** the dragger's: a grip (`GripAlignment`, `GripLength`, `GripColor`), an optional line along it (`ShowsLine`, `LineColor`), `SkUiLook.DrawDragger`. The splitter stretches along its track as a grid child. It takes 16 DIPs across by default; a 1 DIP splitter (`HeightRequest="1"` on a row splitter, with `ShowsLine` as the divider) leaves no gap between the tracks and keeps a 24 DIP touch area over them (`TouchThickness`, see [thin draggers](SkUiDragger.md#how-it-works)); add it after the tracks' content so it is on top.

## Resizable table columns

A [`SkUiCoreTable`](SkUiCoreTable.md#resizable-columns) is a Core grid, so the Core splitter resizes its columns. Put one splitter in each header cell but the last, aligned to the cell's end (`SetHorizontalAlignment(LayoutAlignment.End)`): it resizes its column and the next one (or, with `CurrentOnly`, its column alone), and its grip is in the header. To show a line down the whole table, span it over every row (`SetRowSpan`), with `ShowsLine` and `GripAlignment` `Start` to keep the grip in the header; a splitter merges no cells, so the table still draws its row separators across it. Make the splitters 1 DIP wide (`SetWidth(1)`): each then lies on its column separator and takes no room from the cell, while its touch area and grip reach over the cells on both sides. Add them after the header cells so they are on top. A thicker splitter lies inside the end of its cell: its line runs half its thickness inside the column boundary (show either it or the table's separators), and the cell's content should leave it room.

## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form: getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10). Invalid values: `Set*` throws; XAML, bindings, styles and the property setter ignore them with a logged warning, as MAUI does.
- **Gestures** use SkiaUi's gesture arena. A row splitter claims vertical drags, a column splitter horizontal ones; a drag across it goes to scrollers around it. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).

## How to use

A list beside a detail pane, and a detail pane split into two rows:

```xml
<sk:SkUiGrid ColumnDefinitions="*,Auto,2*">
  <sk:SkUiCollectionView ItemsSource="{Binding Items}" />
  <sk:SkUiGridSplitter Grid.Column="1" SemanticProperties.Description="List width" />
  <sk:SkUiGrid Grid.Column="2">
    <sk:SkUiGrid.RowDefinitions>
      <RowDefinition Height="*" sk:SkUiGridSplitter.MinimumLength="80" />
      <RowDefinition Height="Auto" />
      <RowDefinition Height="200" sk:SkUiGridSplitter.MinimumLength="48" />
    </sk:SkUiGrid.RowDefinitions>
    <sk:SkUiLabel Text="{Binding Selected.Title}" />
    <sk:SkUiGridSplitter Grid.Row="1" />
    <sk:SkUiEditor Grid.Row="2" Text="{Binding Selected.Notes}" />
  </sk:SkUiGrid>
</sk:SkUiGrid>
```

In a MAUI `Grid` (here it resizes a native area above a drawn one):

```xml
<Grid RowDefinitions="300,Auto,*">
  <WebView Source="https://dotnet.microsoft.com" />
  <sk:SkUiGridSplitter Grid.Row="1" HeightRequest="24" />
  <sk:SkUiScrollView Grid.Row="2" />
</Grid>
```

```csharp
var splitter = new SkUiCoreGridSplitter().SetKeyboardIncrement(20);
var grid = new SkUiCoreGrid()
    .SetRowDefinitions([new(SkUiCoreGridLength.Star) { MinHeight = 60 }, new(SkUiCoreGridLength.Auto), new(SkUiCoreGridLength.Star)])
    .Add(top, 0, 0)
    .Add(splitter, 1, 0)
    .Add(bottom, 2, 0);
```

A table header (Core):

```csharp
var table = new SkUiCoreTable()
    .SetRowDefinitions([new(new SkUiCoreGridLength(36)), new(SkUiCoreGridLength.Auto), new(SkUiCoreGridLength.Auto)])
    .SetColumnDefinitions([new(new SkUiCoreGridLength(120)) { MinWidth = 40 }, new(new SkUiCoreGridLength(120)), new(SkUiCoreGridLength.Star)])
    .SetColumnSeparatorColor(Colors.LightGray);
// … header and data cells …
for (var column = 0; column < 2; column++)
{
    // 1 DIP on the column separator, after the header cells: its touch area and grip are on top of them.
    var splitter = new SkUiCoreGridSplitter();
    splitter.SetWidth(1).SetHorizontalAlignment(LayoutAlignment.End);
    table.Add(splitter, 0, column);
}
```

Keep a size the user chose: read the definition's `Height` (or `Width`) on `DragCompleted` and set it again when the page is built.

## Key properties

| Property | Default | Notes |
| --- | --- | --- |
| `ResizeDirection` | `Auto` | `Auto`, `Columns` or `Rows` (`SkUiGridResizeDirection`) |
| `ResizeBehavior` | `BasedOnAlignment` | `BasedOnAlignment`, `PreviousAndNext`, `CurrentAndNext`, `PreviousAndCurrent`, `CurrentOnly` (`SkUiGridResizeBehavior`) |
| `MinimumLength` / `MaximumLength` | 0 / ∞ | SkUi\* only, attached to a `RowDefinition` / `ColumnDefinition`: the track's limits for splitter drags |
| Dragger's | | `ShowsGrip`, `GripColor`, `GripAlignment`, `GripLength`, `ShowsLine`, `LineColor`, `KeyboardIncrement`, `TouchThickness`, `IsDragging` ([SkUiDragger.md](SkUiDragger.md#key-properties)) |

Events: the dragger's `DragStarted`, `DragDelta`, `DragCompleted`. SkUi\*: bindable properties with fluent setters (`SetResizeDirection`, `SetResizeBehavior`). Core: the same fluent setters.

## Related

Gallery: `GridSplitterDemoPage`, `CoreGridSplitterDemoPage` (resizable table columns); every component page resizes its preview area with one. [SkUiGrid.md](SkUiGrid.md), [SkUiCoreGrid.md](SkUiCoreGrid.md).
