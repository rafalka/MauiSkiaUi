# MAUI `CollectionView` → `SkUiCollectionView`

`SkUiCollectionView` is **not** a MAUI API clone. It is a SkUi-first virtualized list on the drawn surface (FR-22). Convert lists member by member with this reference; update it whenever the control gains members (together with [Migration.md](../../../../../docs/Migration.md#lists-collectionview)). Control guide: [SkUiCollectionView.md](../../../../../docs/controls/SkUiCollectionView.md).

## Status

Shipped (Phase B2): vertical linear lists with `ItemsSource` + templates, single selection, item taps, header / footer (scrolled or sticky), empty view, the remaining-items threshold, pull-to-refresh, scrolling to an index or item.

Not yet (Phase B3): **grouping** (`IsGrouped`, group templates), **multiple selection** (`SelectedItems`), **horizontal** and **grid** `ItemsLayout`. Keep those lists as a native `CollectionView` outside the drawn region (see [gaps.md](gaps.md)).

A plain list (items and a template only) can also use the lighter **`SkUiVirtualScrollView`** (a vertical scroll view of an `SkUiVirtualVerticalStackLayout`, [SkUiVirtualVerticalStackLayout.md](../../../../../docs/controls/SkUiVirtualVerticalStackLayout.md)): the same item members, no item containers.

## Mapping

| MAUI `CollectionView` | `SkUiCollectionView` | Notes |
| --- | --- | --- |
| `ItemsSource` | `ItemsSource` | `IList` + `INotifyCollectionChanged` changes are incremental; other sequences are copied once |
| `ItemTemplate` (incl. a `DataTemplateSelector`) | `ItemTemplate` (the element's XAML content) | Templates must create drawn views; views are recycled per template (rebound by `BindingContext`) |
| `ItemTemplateSelector` property (custom code) | `ItemTemplate="{StaticResource Selector}"` | A selector is set as the template |
| `ItemSizingStrategy="MeasureAllItems"` (default) | default | Every item may have its own height; unmeasured items are estimated (`EstimatedItemSize`, else the average) |
| `ItemSizingStrategy="MeasureFirstItem"` | `ItemExtent="<height>"` | One height for every item, set explicitly |
| `ItemsLayout="VerticalList"` / `LinearItemsLayout` `ItemSpacing` | default / `ItemSpacing` | Vertical only |
| `ItemsLayout` horizontal, `GridItemsLayout`, `SnapPointsType` | — | B3: keep the list native |
| `SelectionMode="None"` / `"Single"` | `SelectionMode="None"` / `"Single"` | `SkUiSelectionMode`; plus `SingleDeselect` (tapping the selected item clears it) |
| `SelectionMode="Multiple"`, `SelectedItems` | — | B3: keep the list native |
| `SelectedItem` (two-way) | `SelectedItem` (two-way) | Compared with `Equals`; removing the item from the source clears it |
| `SelectionChanged` (`SelectionChangedEventArgs.PreviousSelection` / `CurrentSelection` lists) | `SelectionChanged` (`SkUiSelectionChangedEventArgs.PreviousItem` / `CurrentItem`) | Raised for taps, code, and removed items |
| — | `SelectionChanging` (`Cancel`) | Taps only: veto a change (unsaved edits, …) |
| `SelectionChangedCommand` (+ `Parameter`) | same names | Runs before `SelectionChanged` |
| `Selected` visual state on the item root | same | `CommonStates`; `Disabled` wins, `Selected` comes before `PointerOver` |
| Selected-row background from the platform / a `Selected` state setter | `SelectionBackground` (brush; default accent at 12 %) | `SelectionBackground="Transparent"` when the template styles its own `Selected` state |
| `TapGestureRecognizer` on the item root (open the item) | `ItemTapped` / `ItemTappedCommand` (+ `Parameter`, default the item) | Fires for every tap on the item, whatever the selection mode, after the selection changed; buttons inside the item keep their own taps |
| `Header` / `Footer` (object or view) + `HeaderTemplate` / `FooterTemplate` | `Header` / `Footer` (drawn views) + templates | A string becomes `<sk:SkUiCollectionView.Header><sk:SkUiLabel Text="..." /></sk:SkUiCollectionView.Header>`; templates bind to the list's context |
| — | `IsStickyHeader`, `IsStickyFooter` | Keep them in place while the items scroll |
| `EmptyView` (object or view) + `EmptyViewTemplate` | `EmptyView` (drawn view) + `EmptyViewTemplate` | Shown while `ItemsSource` is null or empty; fills the space between header and footer |
| `RemainingItemsThreshold`, `RemainingItemsThresholdReached`, `RemainingItemsThresholdReachedCommand` (+ `Parameter`) | same names | Fires once per item count, when what shows changes |
| `ScrollTo(index, position: …, animate: …)` | `ScrollToIndex(index, position, animated)` | Returns a `Task`; lands exactly on items of any height |
| `ScrollTo(item, …)` | `ScrollToItem(item, position, animated)` | Ignores an item not in the list |
| `Scrolled` (`ItemsViewScrolledEventArgs.FirstVisibleItemIndex` / `LastVisibleItemIndex`, `VerticalOffset`) | `Scrolled` (`ScrollY`) and `VisibleRangeChanged` / `FirstVisibleIndex` / `LastVisibleIndex` | |
| `ItemsUpdatingScrollMode` | — | Items inserted or resized above the first visible item keep what shows in place; at the very top, new items show |
| `VerticalScrollBarVisibility` | same | Plus `Overscroll` (bounce / stretch) |
| `IsGrouped`, `GroupHeaderTemplate`, `GroupFooterTemplate` | — | B3: keep the list native |
| `CanReorderItems`, `SwipeView` rows, `ContextMenu` | — | Not yet (later / Phase C) |
| `RefreshView` around it: `IsRefreshing`, `Command` (+ `Parameter`), `RefreshColor`, `Refreshing`, `IsEnabled` / `IsRefreshEnabled` | `IsPullToRefreshEnabled="True"`, `IsRefreshing` (two-way), `RefreshCommand` (+ `Parameter`), `RefreshColor`, `Refreshing` | Drop the `RefreshView`. As MAUI: `IsRefreshing = true` (pull or code) raises `Refreshing` and runs the command; the app sets it back to `false` |

## Intentional differences

| Topic | MAUI `CollectionView` | `SkUiCollectionView` |
| --- | --- | --- |
| Rendering | Platform recycler + one MAUI handler per cell | One Skia surface; items created near the viewport, recycled per template |
| Header / footer / empty view | Any object (strings become labels) | Drawn views or templates only |
| Selection events | Lists of previous / current items | One previous / current item (single selection), plus cancelable `SelectionChanging` |
| Item taps | No event (gesture recognizers on the template) | `ItemTapped` + command, independent of selection |
| Sticky header / footer | Not available | `IsStickyHeader` / `IsStickyFooter` |
| Pull to refresh | A separate `RefreshView` | Built in; works also on short lists and without overscroll |
| Height | Fills its slot; in a `StackLayout` / `ScrollView` it may create every cell | Same rule: give it a bounded height (grid row, page) |

## Agent workflow

1. Do not convert by prefix swap alone: walk the table above for every attribute and property element of the list.
2. Lists using grouping, multiple selection, or a horizontal / grid `ItemsLayout`: keep native (outside the drawn region) and report them.
3. Convert the item template, header, footer and empty view to drawn views like the rest of the region; replace `TapGestureRecognizer`s on the item root with `ItemTappedCommand`.
4. Move a surrounding `RefreshView` onto the list (`IsPullToRefreshEnabled`, `IsRefreshing`, `RefreshCommand`).
5. Run `check_xaml.py` on the page: it reports native `CollectionView`s inside drawn trees and MAUI-only members left on `sk:SkUiCollectionView`.
