# MAUI `CollectionView` → `SkUiCollectionView`

`SkUiCollectionView` is **not** a MAUI API clone. It is a SkUi-first virtualized list on the drawn surface (FR-22). Convert lists member by member with this reference; update it whenever the control gains members (together with [Migration.md](../../../../../docs/Migration.md#lists-collectionview)). Control guide: [SkUiCollectionView.md](../../../../../docs/controls/SkUiCollectionView.md).

## Status

Supported: vertical and horizontal lists and grids with `ItemsSource` + templates, single and multiple selection, item taps, groups (headers, footers, collapsible, sticky group headers), header / footer (scrolled or sticky), empty view, loading more (manual and automatic, at the end or the start), the remaining-items threshold, pull-to-refresh, scrolling to an index, item or group, reordering (`CanReorderItems`, `CanMixGroups`, `ReorderCompleted`). Swipe actions port: wrap the item template in `sk:SkUiSwipeView`.

A plain list (items and a template only) can also use the lighter **`SkUiVirtualScrollView`** (a vertical scroll view of an `SkUiVirtualVerticalStackLayout`, [SkUiVirtualVerticalStackLayout.md](../../../../../docs/controls/SkUiVirtualVerticalStackLayout.md)): the same item members, no item containers.

## Mapping

| MAUI `CollectionView` | `SkUiCollectionView` | Notes |
| --- | --- | --- |
| `ItemsSource` | `ItemsSource` | `IList` + `INotifyCollectionChanged` changes are incremental; other sequences are copied once |
| `ItemTemplate` (incl. a `DataTemplateSelector`) | `ItemTemplate` (the element's XAML content) | Templates must create drawn views; views are recycled per template (rebound by `BindingContext`) |
| `ItemTemplateSelector` property (custom code) | `ItemTemplate="{StaticResource Selector}"` | A selector is set as the template |
| `ItemSizingStrategy="MeasureAllItems"` (default) | default | Every item may have its own height; unmeasured items are estimated (`EstimatedItemSize`, else the average) |
| `ItemSizingStrategy="MeasureFirstItem"` | same, or `ItemExtent="<height>"` | The first row measured sizes every row (a grouped list measures every row); `ItemExtent` sets the size explicitly and wins |
| `ItemsLayout="VerticalList"` / `"HorizontalList"` / `"VerticalGrid, 3"`, `LinearItemsLayout` / `GridItemsLayout` elements | same | Decides the orientation, span and spacings; without it the list's `Orientation`, `Span`, `ItemSpacing` (between rows) and `SpanSpacing` (within a row) give them: use one or the other (setting both is reported, and the layout wins). Horizontal: rows along X (right to left in RTL), no pull-to-refresh. In a grouped grid each group starts a new row |
| `SnapPointsType` / `SnapPointsAlignment` on the items layout | same | Drags, flings and the wheel end on a row lined up with the viewport's start, center or end, or at either end of the list (a header stays reachable); lists and grids, both orientations |
| `SelectionMode="None"` / `"Single"` / `"Multiple"` | same names | `SkUiSelectionMode`; plus `SingleDeselect` (tapping the selected item clears it). C#: `SelectionMode.X` → `SkUiSelectionMode.X`; never cast between the enums (their values differ); a bound view-model property becomes `SkUiSelectionMode` |
| Scrolling to the selected item by hand (`ScrollTo(SelectedItem)` after selection or rotation) | `KeepSelectionVisible="True"` | Also after size changes; multiple selections show as many selected items as fit |
| `SelectedItems` | `SelectedItems` | An `ObservableCollection<object>` by default, or a list of yours (`IList<object>`); changes made to an observable list show; `SelectAll()` / `ClearSelection()` |
| `SelectedItem` (two-way) | `SelectedItem` (two-way) | Compared with `Equals`; removing the item from the source clears it |
| `SelectionChanged` (`SelectionChangedEventArgs.PreviousSelection` / `CurrentSelection` lists) | `SelectionChanged` (`SkUiSelectionChangedEventArgs.PreviousSelection` / `CurrentSelection`, plus `PreviousItem` / `CurrentItem`) | Raised once per change: taps, code, `SelectAll` / `ClearSelection`, removed items. Change the handler's parameter type to `SkUiSelectionChangedEventArgs` (also for handlers wired in XAML) |
| — | `SelectionChanging` (`Cancel`) | Taps only: veto a change (unsaved edits, …) |
| `SelectionChangedCommand` (+ `Parameter`) | same names | Runs before `SelectionChanged` |
| `Selected` visual state on the item root | same | `CommonStates`; `Disabled` wins, `Selected` comes before `PointerOver` |
| Selected-row background from the platform / a `Selected` state setter | `SelectionBackground` (brush; default accent at 12 %) | `SelectionBackground="Transparent"` when the template styles its own `Selected` state |
| `TapGestureRecognizer` on the item root (open the item) | `ItemTapped` / `ItemTappedCommand` (+ `Parameter`, default the item) | Fires for every tap on the item, whatever the selection mode, after the selection changed; buttons inside the item keep their own taps |
| `Header` / `Footer` (string or view) + `HeaderTemplate` / `FooterTemplate` | same | A drawn view, or text in XAML (`Header="Contacts"`: a label). The template binds to the list's context |
| `Header="{Binding Info}"` (data) + `HeaderTemplate` binding `{Binding Title}` | drop `Header`; `HeaderTemplate` binding `{Binding Info.Title}` | The template's binding context is the list's, not the data (the checker reports a bound `Header` / `Footer` / `EmptyView`; `--fix` rewrites an inline template). Same for `Footer` and `EmptyView` |
| — | `IsStickyHeader`, `IsStickyFooter` | Keep them in place, drawn over the list: the items scroll behind them (use a translucent or inset header to show them) and are never covered at the start or the end |
| `EmptyView` (string or view) + `EmptyViewTemplate` | same | Shown while `ItemsSource` is null or empty; fills the space between header and footer. Text in XAML shows as a centered label |
| `RemainingItemsThreshold`, `RemainingItemsThresholdReached`, `RemainingItemsThresholdReachedCommand` (+ `Parameter`) | same names | Fires once per row count, when what shows changes (rows: items of a plain list; grid rows, group headers and footers otherwise) |
| — (a "Load more" footer or busy indicator built by hand) | `LoadMoreMode` (`Manual`, `Auto`, `AutoOnUserScroll`), `LoadMorePosition` (`End`, `Start`), `LoadMoreCommand`, `LoadMoreTemplate`, `IsLoadMoreActive`, `LoadingMore` | A load-more row (button, spinner); `Start` keeps what shows while older items are inserted (chats) |
| `ScrollTo(index, groupIndex, position, animate)`, `ScrollToRequested` | same, or `ScrollToIndex(index, position, animated)` | The awaitable form returns a `Task`; both land exactly on items of any height |
| `ScrollTo(item, …)` | same, or `ScrollToItem(item, position, animated)` | Ignores an item not in the list |
| `Scrolled` (`ItemsViewScrolledEventArgs.FirstVisibleItemIndex` / `LastVisibleItemIndex`, `VerticalOffset`) | `Scrolled` (`ScrolledEventArgs.ScrollY` / `ScrollX`) and `VisibleRangeChanged` / `FirstVisibleIndex` / `LastVisibleIndex` | Change the handler's parameter type to `ScrolledEventArgs`. Deltas: the difference from the previous event's offset; `CenterItemIndex`: from the visible range |
| `ItemsUpdatingScrollMode` (`KeepItemsInView`, `KeepScrollOffset`, `KeepLastItemInView`) | same | `KeepItemsInView` (default): what shows stays in place when items are inserted before it (at the very top, new items show); `KeepScrollOffset`: the offset stays and the items move; `KeepLastItemInView`: scrolls to the end after every change |
| `VerticalScrollBarVisibility` | same | Plus `Overscroll` (bounce / stretch) |
| `IsGrouped`, `GroupHeaderTemplate`, `GroupFooterTemplate` | same names | A group is the list of its items (MAUI's shape); group and item changes are followed; the group is the header's and footer's binding context |
| — | `IsStickyGroupHeader` | The current group's header stays at the start, pushed away by the next one |
| — | `AllowGroupExpandCollapse`, `AutoExpandGroups`, `ExpandGroup` / `CollapseGroup` / `ExpandAll` / `CollapseAll`, `GroupExpanding` / `GroupExpanded` / `GroupCollapsing` / `GroupCollapsed`, `ISkUiExpandableGroup` | Collapsed groups' items are not realized; the header's root goes to `Expanded` / `Collapsed` |
| `ScrollTo(item, group, …)` | same, or `ScrollToItem(item, group, …)`, `ScrollToGroup(group, …)` | An item of a collapsed group expands it |
| `SwipeView` rows | `sk:SkUiSwipeView` in the item template | Prefix swap ([controls.md](controls.md)); the swipe takes horizontal drags and leaves vertical ones to the list, a scroll closes an open row, a recycled row shows closed |
| `CanReorderItems`, `CanMixGroups`, `ReorderCompleted` | same | As MAUI: the item moves in its `IList` (`RemoveAt`, then `Insert`) and `ReorderCompleted` follows. A long press lifts an item (touch), a press and drag (mouse); screen readers get "Move up" / "Move down". The source (or the group) must be an `IList` that is not read-only or fixed-size, as on MAUI |
| `ContextMenu` | — | Context menus are not planned |
| `RefreshView` around it: `IsRefreshing`, `Command` (+ `Parameter`), `RefreshColor`, `Refreshing`, `IsEnabled` / `IsRefreshEnabled` | `IsPullToRefreshEnabled="True"`, `IsRefreshing` (two-way), `RefreshCommand` (+ `Parameter`), `RefreshColor`, `Refreshing` | Drop the `RefreshView`. As MAUI: `IsRefreshing = true` (pull or code) raises `Refreshing` and runs the command; the app sets it back to `false`. Keeping it as `sk:SkUiRefreshView` around the list also works (leave `IsPullToRefreshEnabled` off then) |

## Intentional differences

| Topic | MAUI `CollectionView` | `SkUiCollectionView` |
| --- | --- | --- |
| Rendering | Platform recycler + one MAUI handler per cell | One Skia surface; items created near the viewport, recycled per template |
| Header / footer / empty view | Any object (strings become labels) | Drawn views, or text in XAML (a label); data goes through the template bound to the list's context |
| Selection events | Lists of previous / current items | One previous / current item (single selection), plus cancelable `SelectionChanging` |
| Item taps | No event (gesture recognizers on the template) | `ItemTapped` + command, independent of selection |
| Sticky header / footer | Not available | `IsStickyHeader` / `IsStickyFooter`, and sticky group headers |
| Layout | An `ItemsLayout` object | The same object (it wins when set), or `Orientation`, `Span`, `ItemSpacing`, `SpanSpacing` on the list |
| Expandable groups | Not available | Built in |
| Pull to refresh | A separate `RefreshView` | Built in; works also on short lists and without overscroll; the look's indicator (overlay badge or inline, the platform's); mouse drags pull only with `IsMousePullEnabled`; `RefreshCompletion="Automatic"` ends it when an async command is done |
| Height | Fills its slot; in a `StackLayout` / `ScrollView` it may create every cell | Same rule: give it a bounded height (grid row, page) |

## Agent workflow

1. Do not convert by prefix swap alone: walk the table above for every attribute and property element of the list.
2. Convert the item template, header, footer and empty view to drawn views like the rest of the region; replace `TapGestureRecognizer`s on the item root with `ItemTappedCommand`.
3. Move a surrounding `RefreshView` onto the list (`IsPullToRefreshEnabled`, `IsRefreshing`, `RefreshCommand`), or keep it as `sk:SkUiRefreshView` (not both).
5. Run `check_xaml.py` on the page: it reports native `CollectionView`s inside drawn trees and MAUI-only members left on `sk:SkUiCollectionView`; `check_xaml.py --fix` rewrites data headers with an inline template.
