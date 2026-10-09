# SkUiCollectionView

A drawn, virtualized list or grid with selection (single or multiple), item taps, groups (collapsible, with sticky group headers), a header and footer (scrolled or sticky), an empty view, loading more and pull-to-refresh, vertical or horizontal (FR-22, Phases B2–B3). Rows are created near the viewport and recycled per template by the FR-21 engine ([SkUiVirtualVerticalStackLayout.md](SkUiVirtualVerticalStackLayout.md)); every row may have its own size.

**MAUI counterpart:** `CollectionView` (and a `RefreshView` around it), with **SkiaUi's own API**: familiar names where they fit (`ItemsSource`, `ItemTemplate`, `SelectedItem`, `SelectedItems`, `SelectionChangedCommand`, `IsGrouped`, `GroupHeaderTemplate`, `RemainingItemsThreshold`, `EmptyView`), different ones where MAUI's do not fit the drawn, recycled model (`Header` is a view or a template, not any object; no `ItemsLayout` object but `Orientation`, `Span` and spacings; `ItemTapped` exists; `ScrollToIndex` returns a `Task`). Mapping from MAUI: [Migration.md](../Migration.md#lists-collectionview) and [collection-view.md](../../plugins/skiaui-migration/skills/skiaui-migrate/references/collection-view.md). Design: [ScrollingAndCollectionViews.md](../design/ScrollingAndCollectionViews.md#collection-view-implemented).

Swipe actions on rows: put an [`SkUiSwipeView`](SkUiSwipeView.md) in the item template. Not available: reordering items, snap points, keyboard item navigation. Plain lists without any of the features below can also use the lighter `SkUiVirtualScrollView`.

## How it works

- **Rows.** The engine lays out *rows*: an item, a grid row of up to `Span` items (as tall as its tallest item), a group's header or a group's footer. A flat single-column list has one row per item (no table); other lists keep a table of rows, and every change of the source (or of a group) becomes the smallest change of the rows, so unaffected rows keep their views and sizes. Indices in the API (`ItemCount`, `FirstVisibleIndex`, `ScrollToIndex`, `ItemTapped`'s `Index`) count items only, across the groups, collapsed groups included; `RemainingItemsThreshold` counts rows.
- **Parts.** A scroller (vertical, or horizontal with `Orientation`) whose content is the header, the load-more row at the start, the items (or the empty view), the load-more row at the end, and the footer. A sticky header or footer is drawn over the list instead (the scroller still fills the whole view): the items scroll behind it, so a translucent background, margins or rounded corners let them show through, and scrolling never re-records it. Its size along the axis (margins included) becomes the scroller's inset at that edge: at the start of the list the first item is after the header, at the end the last one before the footer, scrolling to an item places it in the uncovered band, the scroll bar runs beside that band, and the pull-to-refresh indicator comes out from below the header. A drag (or the wheel) on a sticky part scrolls the list; a tap on it never reaches the items behind (views inside it keep their taps). The list needs a bounded size along its axis (a grid row, a page); measured unbounded (inside a stack or a scroll view of the same axis) it is as long as all its rows and creates every one.
- **Items.** Each item's view (from `ItemTemplate`, or a selector) is hosted in a drawn item container: the container takes the item's taps, draws `SelectionBackground` behind a selected item, and is what the engine recycles (with its view, per template). Item views get their item as binding context.
- **Grids.** `Span` items share a row (columns; rows of a horizontal list), each as wide as the row divided by the span less `SpanSpacing`; `ItemSpacing` is the gap between rows. Grid rows are recycled as a whole; their cells are kept while their template fits the item they show next, else swapped with a pool of cells per template. In a grouped grid each group starts a new row. Moving items in a grid (`ObservableCollection.Move`) rebuilds the grid rows from the first one it touches: those rows are measured again when they show (a single-column list keeps its sizes on a move).
- **Horizontal.** With `Orientation="Horizontal"` rows follow each other from left to right (from right to left in right-to-left layouts, starting at the right edge), measured at the list's height; the header and footer (also sticky) are at the start and the end, and `ItemExtent` / `EstimatedItemSize` are widths. Horizontal lists are not pulled to refresh.
- **Groups.** With `IsGrouped`, `ItemsSource` is a list of groups, each the list of its items (MAUI's shape: a `List<T>` subclass with its own properties); changes of the groups and of each group's items are followed. `GroupHeaderTemplate` and `GroupFooterTemplate` create a row before and after each group's items (the group is their binding context; a selector chooses per group). Without a header template, groups have no header.
- **Expandable groups.** With `AllowGroupExpandCollapse` a tap on a group's header collapses or expands the group; `ExpandGroup`, `CollapseGroup`, `ExpandAll`, `CollapseAll` do it from code. A collapsed group's items (and footer) are removed from the rows — not realized, not measured — and what shows stays in place (scroll anchoring). `GroupExpanding` / `GroupCollapsing` can cancel; `GroupExpanded` / `GroupCollapsed` follow. The header's root goes to the `Expanded` or `Collapsed` visual state, and screen readers read it as a button with that state. New groups start expanded unless `AutoExpandGroups` is `false`; a group that implements `ISkUiExpandableGroup` keeps its own state: the list reads its `IsExpanded`, writes it, and follows its `PropertyChanged`. Scrolling to an item of a collapsed group expands it.
- **Sticky group headers.** With `IsStickyGroupHeader` the header of the group at the start of the uncovered band is drawn over the list (below a sticky list header) while its items scroll behind it, and pushed away by the next group's header. It is moved by a translation only (no re-record while scrolling) and rebound when the group changes; a tap on it collapses its group and brings the group's header to the start. Scrolling to an item places it after the sticky group header.
- **Selection.** `SelectionMode` `Single` or `SingleDeselect`: a tap selects its item (a cancelable `SelectionChanging` first), `SelectedItem` changes (two-way). `Multiple`: taps add or remove items in `SelectedItems` (an `ObservableCollection<object>` of the list's own, or a list of yours); changes made to that list show at once. `SelectAll` and `ClearSelection` change it in one step. Each change runs `SelectionChangedCommand` and raises `SelectionChanged` once, with the previous and current selection (`PreviousSelection`, `CurrentSelection`, plus `PreviousItem` / `CurrentItem` for a single selection). The roots of selected items' views go to the `Selected` visual state (MAUI's `CommonStates` group; `Disabled` wins, `Selected` comes before `PointerOver`); a selection change re-records only the items whose state changed; recycled views show the selection of the item they are bound to. Items removed from the source leave the selection. Items are compared with `Equals`.
- **Keeping the selection in view.** With `KeepSelectionVisible`, the list scrolls as little as needed to show the selection when it changes, when the list's size changes (a rotation, a resized window) and when it is first laid out. A single selection shows fully. A multiple selection shows the part of the list with the most selected items that fits, around a focus item: the item last selected by a tap (while it stays selected), else the first selected item in the list, so a selection set by the app or a binding shows from its start; nothing moves while what shows already holds as many. A tap only makes its item show fully (the list does not move away from the finger); after a size change the tapped item stays in view. An item shown in several groups is followed where it was tapped (its first appearance once items moved, or for a selection set by the app); the band it shows in excludes the sticky header, footer and group header. Selection changes scroll animated, size changes at once; items removed from the source do not scroll, and collapsed groups stay collapsed.
- **Taps.** `ItemTapped` and `ItemTappedCommand` run for every tap on an item, whatever the selection mode, after the selection changed (`Item`, its `Index` across the groups, its `Group`). Views inside the item that take taps themselves (buttons, views with `Tapped`) keep them: the item is not tapped then.
- **Empty view.** While there are no rows (`ItemsSource` is `null` or empty), `EmptyView` shows instead of the items, between the header and the footer, filling what the list has left. `EmptyViewTemplate` creates it the first time the list is empty.
- **Loading more.** `LoadMoreMode` `Manual` shows a load-more row (a "Load more" button by default, or `LoadMoreTemplate`) after the items (`LoadMorePosition` `End`) or before them (`Start`) while `LoadMoreCommand` can execute; `Auto` asks when that end of the rows shows (also when the items do not fill the list), `AutoOnUserScroll` only once the user has scrolled. Asking sets `IsLoadMoreActive` (the row shows a spinner), raises `LoadingMore` and runs the command; the app sets `IsLoadMoreActive` back to `false` when the items are added, and an automatic mode asks again if the end still shows once they are laid out. At the start, inserted items keep what shows in place (a chat loading older messages). `RemainingItemsThreshold` works independently (to prefetch a page before the end shows).
- **Pull to refresh.** With `IsPullToRefreshEnabled`, dragging the top of a vertical list down brings a drawn indicator down with the pull (composite-time, no re-record while dragging), also when the items do not fill the list and when overscroll is off. Releasing past 64 DIPs sets `IsRefreshing`; as MAUI's `RefreshView`, `IsRefreshing` becoming `true` (by a pull or by the app) raises `Refreshing` and runs `RefreshCommand`, and the indicator spins on the render thread until the app sets it back to `false`.
- **Accessibility.** Tappable items are actionable elements (double tap, Enter / Space); a selected item reads `SelectedStateText` ("Selected") as its value; collapsible group headers are buttons that read "Expanded" / "Collapsed" (`SkUiExpander.ExpandedStateText`).

## How to use

```xml
<sk:SkUiCollectionView ItemsSource="{Binding Orders}"
                       SelectionMode="Single" SelectedItem="{Binding Current}"
                       ItemTappedCommand="{Binding OpenOrder}"
                       IsStickyHeader="True"
                       LoadMoreMode="AutoOnUserScroll" LoadMoreCommand="{Binding LoadMore}"
                       IsLoadMoreActive="{Binding IsLoadingMore}"
                       IsPullToRefreshEnabled="True" IsRefreshing="{Binding IsBusy}"
                       RefreshCommand="{Binding Reload}">
  <sk:SkUiCollectionView.Header>
    <sk:SkUiLabel Text="Orders" FontSize="20" Padding="12" />
  </sk:SkUiCollectionView.Header>
  <sk:SkUiCollectionView.EmptyView>
    <sk:SkUiLabel Text="No orders yet" HorizontalTextAlignment="Center" VerticalTextAlignment="Center" />
  </sk:SkUiCollectionView.EmptyView>
  <DataTemplate x:DataType="local:Order">
    <sk:SkUiGrid Padding="12" ColumnDefinitions="*,Auto">
      <VisualStateManager.VisualStateGroups>
        <VisualStateGroup Name="CommonStates">
          <VisualState Name="Normal" />
          <VisualState Name="Selected">
            <VisualState.Setters>
              <Setter Property="BackgroundColor" Value="#1F087F83" />
            </VisualState.Setters>
          </VisualState>
        </VisualStateGroup>
      </VisualStateManager.VisualStateGroups>
      <sk:SkUiLabel Text="{Binding Title}" />
      <sk:SkUiButton Grid.Column="1" Text="Pay" Command="{Binding PayCommand}" /> <!-- keeps its own taps -->
    </sk:SkUiGrid>
  </DataTemplate>
</sk:SkUiCollectionView>
```

A grouped grid with sticky, collapsible group headers and multiple selection:

```xml
<sk:SkUiCollectionView ItemsSource="{Binding PhotosByDay}" IsGrouped="True"
                       Span="3" SpanSpacing="2" ItemSpacing="2"
                       IsStickyGroupHeader="True" AllowGroupExpandCollapse="True"
                       SelectionMode="Multiple" SelectedItems="{Binding Chosen}">
  <sk:SkUiCollectionView.GroupHeaderTemplate>
    <DataTemplate x:DataType="local:Day">
      <sk:SkUiLabel Text="{Binding Title}" Padding="12,8" BackgroundColor="#F2FFFFFF" />
    </DataTemplate>
  </sk:SkUiCollectionView.GroupHeaderTemplate>
  <DataTemplate x:DataType="local:Photo">
    <sk:SkUiImage Source="{Binding Thumbnail}" Aspect="AspectFill" HeightRequest="110" />
  </DataTemplate>
</sk:SkUiCollectionView>
```

From code:

```csharp
var list = new SkUiCollectionView { ItemsSource = orders, ItemTemplate = new DataTemplate(() => new OrderRow()) }
    .SetSelectionMode(SkUiSelectionMode.SingleDeselect)
    .SetHeader(new OrdersHeader(), sticky: true);
list.ItemTapped += (_, args) => Open((Order)args.Item!);
await list.ScrollToItem(orders[^1], ScrollToPosition.End);

var carousel = new SkUiCollectionView { Orientation = ItemsLayoutOrientation.Horizontal, ItemsSource = cards, HeightRequest = 180 }
    .SetSpan(2, spacing: 8);
```

## Key properties

| Member | Default | Notes |
| --- | --- | --- |
| `ItemsSource`, `ItemTemplate` | `null` | `IList` + `INotifyCollectionChanged` changes are incremental (also of each group); other sequences are copied once. Templates create drawn views, recycled per template (a `DataTemplateSelector` chooses per item). The XAML content is the `ItemTemplate` |
| `Orientation` | `Vertical` | `Horizontal`: rows along X (right to left in RTL), the header and footer at the start and the end |
| `Span`, `SpanSpacing` | 1, 0 | Items per row (a grid; rows of a horizontal list) and the gap between them. `SetSpan(span, spacing)` |
| `ItemSpacing` | 0 | Gap between rows (not around the header and footer) |
| `ItemExtent`, `EstimatedItemSize` | 0 | One size for every row along the axis (fast path); the size assumed for rows not measured yet |
| `PrefetchFactor`, `PrefetchBehindFactor`, `ReleaseFactor`, `PrefetchBudget` | 1, 0.5, 2, automatic | As the virtual layout ([SkUiVirtualVerticalStackLayout.md](SkUiVirtualVerticalStackLayout.md#key-properties-skuivirtualverticalstacklayout)): how far ahead and behind rows are created, when they are released, the UI-time budget per frame |
| `Header`, `HeaderTemplate`, `IsStickyHeader` | `null`, `null`, `false` | A drawn view before the items (a template creates it, with the list's binding context); sticky: it stays at the start, drawn over the list, and the items scroll behind it (never covered at the start). `SetHeader(view, sticky)` |
| `Footer`, `FooterTemplate`, `IsStickyFooter` | `null`, `null`, `false` | After the items (right after the last one), or sticky at the end, drawn over the list (never covering the last item at the end). `SetFooter(view, sticky)` |
| `EmptyView`, `EmptyViewTemplate` | `null` | Shown instead of the items while there are no rows; fills the space left |
| `IsGrouped` | `false` | `ItemsSource` is a list of groups, each the list of its items |
| `GroupHeaderTemplate`, `GroupFooterTemplate` | `null` | A row before / after each group's items; the group is the binding context |
| `IsStickyGroupHeader` | `false` | The current group's header stays at the start, pushed away by the next one |
| `AllowGroupExpandCollapse` | `false` | A tap on a group header collapses / expands the group |
| `AutoExpandGroups` | `true` | Whether new groups start expanded (an `ISkUiExpandableGroup` starts as it says) |
| `SelectionMode` | `None` | `None`, `Single`, `SingleDeselect` (tapping the selected item clears it), `Multiple` |
| `SelectedItem` | `null` | Two-way; the selection of `Single` / `SingleDeselect` |
| `SelectedItems` | a new `ObservableCollection<object>` | The selection of `Multiple`; set a list of yours to share it |
| `KeepSelectionVisible` | `false` | Scrolls the selection into view on selection and size changes (see above) |
| `SelectionChangedCommand` (+ `…Parameter`) | `null` | Runs when the selection changes, before `SelectionChanged` |
| `SelectionBackground` | `null` | A brush (in XAML a color) behind selected items; `null`: the accent at 12 %; `Transparent`: none (style the item with the `Selected` state) |
| `ItemTappedCommand` (+ `…Parameter`) | `null` | Runs on an item tap, with the parameter when set, else the item |
| `ShowsItemPressEffect` | `false` | Item containers show the press effect while pressed |
| `RemainingItemsThreshold`, `RemainingItemsThresholdReachedCommand` (+ `…Parameter`) | -1 | As the virtual layout, counted in rows: once per row count, when the end comes near |
| `LoadMoreMode`, `LoadMorePosition` | `None`, `End` | `Manual` (a load-more row), `Auto`, `AutoOnUserScroll`; at the `End` or the `Start` of the items |
| `LoadMoreCommand` (+ `…Parameter`), `LoadMoreTemplate`, `IsLoadMoreActive` | `null`, `null`, `false` | The command loads more (while it can execute, more can load); the row's template; two-way, set by the list when it asks, back to `false` by the app when done |
| `IsPullToRefreshEnabled` | `false` | Pulling the top down and releasing past 64 DIPs starts a refresh (vertical lists) |
| `IsRefreshing` | `false` | Two-way; `true` raises `Refreshing` and runs `RefreshCommand` (+ `…Parameter`) |
| `RefreshColor` | `null` | The indicator's arc; `null`: the accent |
| `VerticalScrollBarVisibility`, `HorizontalScrollBarVisibility`, `Overscroll` | `Default` | Of the list's scroller |
| `ItemCount`, `FirstVisibleIndex`, `LastVisibleIndex`, `ScrollX`, `ScrollY`, `IsScrolling` | | Read-only; items only (headers and footers are not items); the visible indices and `IsScrolling` raise `PropertyChanged` |
| `ScrollToIndex(index, position, animated)`, `ScrollToItem(item, …)`, `ScrollToGroup(group, …)`, `ScrollToAsync(offset, animated)` | | Return `Task`s; an item lands exactly where asked (rows between are measured on the way), after the sticky parts. `ScrollToItem` ignores an item not in the list. An item in several groups: `ScrollToItem(item)` goes to its first appearance, `ScrollToItem(item, group, …)` to the one in that group |
| `SelectAll()`, `ClearSelection()` | | One selection change each (`SelectAll`: `Multiple` only) |
| `IsGroupExpanded(group)`, `ExpandGroup(group)`, `CollapseGroup(group)`, `ExpandAll()`, `CollapseAll()` | | `ExpandGroup` / `CollapseGroup` return `false` when canceled |
| `GetRealizedView(index)`, `RemeasureItem(index)` | | The template's view of a realized item; measure an item (its row) again |
| `ISkUiItemsView` | | The members every list has, with the same names as on `SkUiVirtualScrollView` and `SkUiVirtualVerticalStackLayout` (a test checks they reach the layout inside); code that only configures or follows a list can take any of them |
| `SelectedStateText`, `LoadMoreText` (static) | "Selected", "Load more" | What screen readers read for a selected item; the default load-more button's text. Set once to localize |

## Events

| Event | Arguments | When |
| --- | --- | --- |
| `SelectionChanging` | `SkUiSelectionChangingEventArgs` (`PreviousItem`, `CurrentItem`, `PreviousSelection`, `CurrentSelection`, `Cancel`) | A tap is about to change the selection (taps only) |
| `SelectionChanged` | `SkUiSelectionChangedEventArgs` (`PreviousItem`, `CurrentItem`, `PreviousSelection`, `CurrentSelection`) | The selection changed (tap, app, `SelectAll` / `ClearSelection`, items removed) |
| `ItemTapped` | `SkUiItemTappedEventArgs` (`Item`, `Index`, `Group`) | An item was tapped outside views that take taps themselves |
| `GroupExpanding`, `GroupCollapsing` | `SkUiGroupChangingEventArgs` (`Group`, `GroupIndex`, `Cancel`) | A tap or the API is about to expand / collapse a group |
| `GroupExpanded`, `GroupCollapsed` | `SkUiGroupEventArgs` (`Group`, `GroupIndex`) | A group expanded / collapsed (also when its `ISkUiExpandableGroup.IsExpanded` changed) |
| `LoadingMore` | | The list asks for more items (before `LoadMoreCommand`) |
| `Refreshing` | | `IsRefreshing` became `true` |
| `RemainingItemsThresholdReached`, `VisibleRangeChanged`, `Scrolled` | | As on the virtual layout and the scroll view |

## Demo and tests

Demo: **Components → Scrolling → SkUiCollectionView** (next to MAUI's `CollectionView` in a `RefreshView`) and **CollectionView (groups, grid, horizontal)**. Samples: **Order list (collection view)**, **Contacts** (live grouped list, tiles, selection mode, adaptive preview). Tests: `CollectionViewTests`, `CollectionViewGroupsAndLayoutsTests`; leak scenarios `CollectionViewUsed`, `CollectionViewGrouped`.
