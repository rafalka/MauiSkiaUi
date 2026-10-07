# MAUI `CollectionView` → `SkUiCollectionView`

`SkUiCollectionView` is **not** a MAUI API clone. It is a SkUi-first virtualized list on the drawn surface (FR-22). Use this reference when porting screens; update it whenever the control ships or gains members (together with [Migration.md](../../../../../docs/Migration.md)).

## Status

`SkUiCollectionView` is **not available yet** (Phase B2–B3). Its engine is: **`SkUiVirtualScrollView`** (a vertical scroll view of an `SkUiVirtualVerticalStackLayout`, [SkUiVirtualVerticalStackLayout.md](../../../../../docs/controls/SkUiVirtualVerticalStackLayout.md)) ports **plain lists** today. Lists that need selection, a header / footer, an empty view, grouping, or a horizontal / grid layout stay a native `CollectionView` outside the drawn region until `SkUiCollectionView` ships (see [gaps.md](gaps.md)).

## Plain lists today: `SkUiVirtualScrollView`

| MAUI `CollectionView` | `SkUiVirtualScrollView` | Notes |
| --- | --- | --- |
| `ItemsSource` | `ItemsSource` | `IList` + `INotifyCollectionChanged` changes are incremental; other sequences are copied once |
| `ItemTemplate` (incl. a `DataTemplateSelector`) | `ItemTemplate` (the element's XAML content) | Templates must create drawn views; views are recycled per template (rebound by `BindingContext`) |
| `ItemSizingStrategy="MeasureAllItems"` (default) | default | Every item may have its own height; unmeasured items are estimated (`EstimatedItemSize`, else the average) |
| `ItemSizingStrategy="MeasureFirstItem"` | `ItemExtent="<height>"` | One height for every item, set explicitly |
| `ItemsLayout` `LinearItemsLayout.ItemSpacing` | `Spacing` | Vertical only |
| `RemainingItemsThreshold`, `RemainingItemsThresholdReached`, `RemainingItemsThresholdReachedCommand` | same names | Fires once per item count, when what shows changes |
| `ScrollTo(index, position: …, animate: …)` | `ScrollToIndex(index, position, animated)` | Returns a `Task`; lands exactly on items of any height |
| `ScrollTo(item, …)` | `ScrollToIndex(list.IndexOf(item), …)` | |
| `Scrolled` (`ItemsViewScrolledEventArgs.FirstVisibleItemIndex` / `LastVisibleItemIndex`) | `Scrolled` (`ScrollX` / `ScrollY`) and `VisibleRangeChanged` / `FirstVisibleIndex` / `LastVisibleIndex` | |
| `ItemsUpdatingScrollMode` | — | Items inserted or resized before the first visible item keep what shows in place; at the very top, new items at the top show |
| `SelectionMode`, `SelectedItem(s)`, `SelectionChanged` | — | Not yet: `TappedCommand` on the item template, or keep the native list |
| `Header`, `Footer`, `EmptyView` | — | Not yet: put an `SkUiVirtualVerticalStackLayout` in an `SkUiScrollView` with drawn views above / below it; show an empty state with `SkUiStateContainer` |
| `IsGrouped`, group templates | — | Not yet |
| Horizontal `ItemsLayout`, `GridItemsLayout`, snap points | — | Not yet |
| `RefreshView` around it | — | A MAUI `RefreshView` around the surface root |

## Design intent

| Topic | MAUI `CollectionView` | `SkUiCollectionView` |
| --- | --- | --- |
| API goal | Platform-native recycler + MAUI surface | One Skia surface, FR-21 indexed virtualization + recycling |
| Parity | Full MAUI member set | **SkUi-first**; only patterns that fit performance and the single-surface model |
| Grouping | Flat groups | Groups + **expandable** groups (`IsExpanded`; collapsed items not realized) |
| Migration | Rename prefix in XAML | Read this doc + [ScrollingAndCollectionViews.md](../../../../../docs/design/ScrollingAndCollectionViews.md#fr-22--skuicollectionview-requirements) |

## Mapping (fill in at MVP)

Document here, per MAUI member or app pattern:

- **Supported** — SkUi property / event name and any semantic difference.
- **Different** — SkUi alternative (e.g. scroll / selection / grouping model).
- **Unsupported** — keep native list, hybrid page, or workaround.

Expected sections when B2 lands:

- Data: `ItemsSource`, templates, empty view, collection changes.
- Layout: linear / grid (not necessarily MAUI's `ItemsLayout` type names).
- Header / footer / sticky behavior.
- Grouping and **expand / collapse**.
- Selection and item tap.
- Scroll, load-more threshold, pull to refresh.
- Intentional gaps (e.g. MAUI reorder APIs, `ItemsUpdatingScrollMode` equivalents if any).

## Agent workflow

1. Do not assume MAUI `CollectionView` XAML converts by prefix swap alone.
2. Compare the app's features against the tables above once this file is populated.
3. After migration, run `check_xaml.py` on the page; update [gaps.md](gaps.md) when the control is no longer listed as blocked.
