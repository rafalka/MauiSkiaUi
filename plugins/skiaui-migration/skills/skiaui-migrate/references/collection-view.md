# MAUI `CollectionView` → `SkUiCollectionView`

`SkUiCollectionView` is **not** a MAUI API clone. It is a SkUi-first virtualized list on the drawn surface (FR-22). Convert lists member by member with this reference; update it whenever the control gains members (together with [Migration.md](../../../../../docs/Migration.md#lists-collectionview)). Control guide: [SkUiCollectionView.md](../../../../../docs/controls/SkUiCollectionView.md).

## Status

Shipped (Phases B2–B3): vertical and horizontal lists and grids with `ItemsSource` + templates, single and multiple selection, item taps, groups (headers, footers, collapsible, sticky group headers), header / footer (scrolled or sticky), empty view, loading more (manual and automatic, at the end or the start), the remaining-items threshold, pull-to-refresh, scrolling to an index, item or group.

Not available: **reordering** (`CanReorderItems`), **snap points** on the items layout, swipe actions (Phase C). Keep those lists as a native `CollectionView` outside the drawn region (see [gaps.md](gaps.md)).

A plain list (items and a template only) can also use the lighter **`SkUiVirtualScrollView`** (a vertical scroll view of an `SkUiVirtualVerticalStackLayout`, [SkUiVirtualVerticalStackLayout.md](../../../../../docs/controls/SkUiVirtualVerticalStackLayout.md)): the same item members, no item containers.

## Mapping

| MAUI `CollectionView` | `SkUiCollectionView` | Notes |
| --- | --- | --- |
| `ItemsSource` | `ItemsSource` | `IList` + `INotifyCollectionChanged` changes are incremental; other sequences are copied once |
| `ItemTemplate` (incl. a `DataTemplateSelector`) | `ItemTemplate` (the element's XAML content) | Templates must create drawn views; views are recycled per template (rebound by `BindingContext`) |
| `ItemTemplateSelector` property (custom code) | `ItemTemplate="{StaticResource Selector}"` | A selector is set as the template |
| `ItemSizingStrategy="MeasureAllItems"` (default) | default | Every item may have its own height; unmeasured items are estimated (`EstimatedItemSize`, else the average) |
| `ItemSizingStrategy="MeasureFirstItem"` | `ItemExtent="<height>"` | One height for every item, set explicitly |
| `ItemsLayout="VerticalList"` / `LinearItemsLayout` `ItemSpacing` | default / `ItemSpacing` | |
| `ItemsLayout="HorizontalList"` / `LinearItemsLayout Orientation="Horizontal"` | `Orientation="Horizontal"` | Rows along X (right to left in RTL); no pull-to-refresh |
| `ItemsLayout="VerticalGrid, 3"` / `GridItemsLayout Span="3"` (`VerticalItemSpacing`, `HorizontalItemSpacing`) | `Span="3"` (`ItemSpacing` between rows, `SpanSpacing` within a row) | A horizontal grid: `Orientation="Horizontal"` with `Span` rows. In a grouped grid each group starts a new row |
| `SnapPointsType` / `SnapPointsAlignment` on the items layout | — | Not available: keep the list native, or use an `SkUiScrollView` with snap points |
| `SelectionMode="None"` / `"Single"` / `"Multiple"` | same names | `SkUiSelectionMode`; plus `SingleDeselect` (tapping the selected item clears it) |
| Scrolling to the selected item by hand (`ScrollTo(SelectedItem)` after selection or rotation) | `KeepSelectionVisible="True"` | Also after size changes; multiple selections show as many selected items as fit |
| `SelectedItems` | `SelectedItems` | An `ObservableCollection<object>` by default, or a list of yours (`IList<object>`); changes made to an observable list show; `SelectAll()` / `ClearSelection()` |
| `SelectedItem` (two-way) | `SelectedItem` (two-way) | Compared with `Equals`; removing the item from the source clears it |
| `SelectionChanged` (`SelectionChangedEventArgs.PreviousSelection` / `CurrentSelection` lists) | `SelectionChanged` (`SkUiSelectionChangedEventArgs.PreviousSelection` / `CurrentSelection`, plus `PreviousItem` / `CurrentItem`) | Raised once per change: taps, code, `SelectAll` / `ClearSelection`, removed items |
| — | `SelectionChanging` (`Cancel`) | Taps only: veto a change (unsaved edits, …) |
| `SelectionChangedCommand` (+ `Parameter`) | same names | Runs before `SelectionChanged` |
| `Selected` visual state on the item root | same | `CommonStates`; `Disabled` wins, `Selected` comes before `PointerOver` |
| Selected-row background from the platform / a `Selected` state setter | `SelectionBackground` (brush; default accent at 12 %) | `SelectionBackground="Transparent"` when the template styles its own `Selected` state |
| `TapGestureRecognizer` on the item root (open the item) | `ItemTapped` / `ItemTappedCommand` (+ `Parameter`, default the item) | Fires for every tap on the item, whatever the selection mode, after the selection changed; buttons inside the item keep their own taps |
| `Header` / `Footer` (object or view) + `HeaderTemplate` / `FooterTemplate` | `Header` / `Footer` (drawn views) + templates | A string becomes `<sk:SkUiCollectionView.Header><sk:SkUiLabel Text="..." /></sk:SkUiCollectionView.Header>`; templates bind to the list's context |
| — | `IsStickyHeader`, `IsStickyFooter` | Keep them in place, drawn over the list: the items scroll behind them (use a translucent or inset header to show them) and are never covered at the start or the end |
| `EmptyView` (object or view) + `EmptyViewTemplate` | `EmptyView` (drawn view) + `EmptyViewTemplate` | Shown while `ItemsSource` is null or empty; fills the space between header and footer |
| `RemainingItemsThreshold`, `RemainingItemsThresholdReached`, `RemainingItemsThresholdReachedCommand` (+ `Parameter`) | same names | Fires once per row count, when what shows changes (rows: items of a plain list; grid rows, group headers and footers otherwise) |
| — (a "Load more" footer or busy indicator built by hand) | `LoadMoreMode` (`Manual`, `Auto`, `AutoOnUserScroll`), `LoadMorePosition` (`End`, `Start`), `LoadMoreCommand`, `LoadMoreTemplate`, `IsLoadMoreActive`, `LoadingMore` | A load-more row (button, spinner); `Start` keeps what shows while older items are inserted (chats) |
| `ScrollTo(index, position: …, animate: …)` | `ScrollToIndex(index, position, animated)` | Returns a `Task`; lands exactly on items of any height |
| `ScrollTo(item, …)` | `ScrollToItem(item, position, animated)` | Ignores an item not in the list |
| `Scrolled` (`ItemsViewScrolledEventArgs.FirstVisibleItemIndex` / `LastVisibleItemIndex`, `VerticalOffset`) | `Scrolled` (`ScrollY`) and `VisibleRangeChanged` / `FirstVisibleIndex` / `LastVisibleIndex` | |
| `ItemsUpdatingScrollMode` | — | Items inserted or resized above the first visible item keep what shows in place; at the very top, new items show |
| `VerticalScrollBarVisibility` | same | Plus `Overscroll` (bounce / stretch) |
| `IsGrouped`, `GroupHeaderTemplate`, `GroupFooterTemplate` | same names | A group is the list of its items (MAUI's shape); group and item changes are followed; the group is the header's and footer's binding context |
| — | `IsStickyGroupHeader` | The current group's header stays at the start, pushed away by the next one |
| — | `AllowGroupExpandCollapse`, `AutoExpandGroups`, `ExpandGroup` / `CollapseGroup` / `ExpandAll` / `CollapseAll`, `GroupExpanding` / `GroupExpanded` / `GroupCollapsing` / `GroupCollapsed`, `ISkUiExpandableGroup` | Collapsed groups' items are not realized; the header's root goes to `Expanded` / `Collapsed` |
| `ScrollTo(item, group, …)` | `ScrollToItem(item, …)`, `ScrollToGroup(group, …)` | An item of a collapsed group expands it |
| `CanReorderItems`, `SwipeView` rows, `ContextMenu` | — | Not yet (later / Phase C) |
| `RefreshView` around it: `IsRefreshing`, `Command` (+ `Parameter`), `RefreshColor`, `Refreshing`, `IsEnabled` / `IsRefreshEnabled` | `IsPullToRefreshEnabled="True"`, `IsRefreshing` (two-way), `RefreshCommand` (+ `Parameter`), `RefreshColor`, `Refreshing` | Drop the `RefreshView`. As MAUI: `IsRefreshing = true` (pull or code) raises `Refreshing` and runs the command; the app sets it back to `false` |

## Intentional differences

| Topic | MAUI `CollectionView` | `SkUiCollectionView` |
| --- | --- | --- |
| Rendering | Platform recycler + one MAUI handler per cell | One Skia surface; items created near the viewport, recycled per template |
| Header / footer / empty view | Any object (strings become labels) | Drawn views or templates only |
| Selection events | Lists of previous / current items | One previous / current item (single selection), plus cancelable `SelectionChanging` |
| Item taps | No event (gesture recognizers on the template) | `ItemTapped` + command, independent of selection |
| Sticky header / footer | Not available | `IsStickyHeader` / `IsStickyFooter`, and sticky group headers |
| Layout | An `ItemsLayout` object | `Orientation`, `Span`, `ItemSpacing`, `SpanSpacing` on the list |
| Expandable groups | Not available | Built in |
| Pull to refresh | A separate `RefreshView` | Built in; works also on short lists and without overscroll |
| Height | Fills its slot; in a `StackLayout` / `ScrollView` it may create every cell | Same rule: give it a bounded height (grid row, page) |

## Agent workflow

1. Do not convert by prefix swap alone: walk the table above for every attribute and property element of the list.
2. Reorderable lists (`CanReorderItems`) and items layouts with snap points: keep native (outside the drawn region) and report them.
3. Convert the item template, header, footer and empty view to drawn views like the rest of the region; replace `TapGestureRecognizer`s on the item root with `ItemTappedCommand`.
4. Move a surrounding `RefreshView` onto the list (`IsPullToRefreshEnabled`, `IsRefreshing`, `RefreshCommand`).
5. Run `check_xaml.py` on the page: it reports native `CollectionView`s inside drawn trees and MAUI-only members left on `sk:SkUiCollectionView`.
