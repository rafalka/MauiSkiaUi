# SkUiCollectionView

A drawn, virtualized list with selection, item taps, a header and footer (scrolled or sticky), an empty view, a load-more threshold and pull-to-refresh (FR-22, Phase B2). Items are created near the viewport and recycled per template by the FR-21 engine ([SkUiVirtualVerticalStackLayout.md](SkUiVirtualVerticalStackLayout.md)); every item may have its own height.

**MAUI counterpart:** `CollectionView` (and a `RefreshView` around it), with **SkiaUi's own API**: familiar names where they fit (`ItemsSource`, `ItemTemplate`, `SelectedItem`, `SelectionChangedCommand`, `RemainingItemsThreshold`, `EmptyView`), different ones where MAUI's do not fit the drawn, recycled model (`Header` is a view or a template, not any object; `ItemTapped` exists; `ScrollToIndex` returns a `Task`). Mapping from MAUI: [Migration.md](../Migration.md#lists-collectionview) and [collection-view.md](../../plugins/skiaui-migration/skills/skiaui-migrate/references/collection-view.md). Design: [ScrollingAndCollectionViews.md](../design/ScrollingAndCollectionViews.md#fr-22--skuicollectionview-requirements).

Not yet (Phase B3): grouping and expandable groups, multiple selection, grid and horizontal layouts, load-more modes with a load-more row. Plain lists without any of the features below can also use the lighter `SkUiVirtualScrollView`.

## How it works

- **Parts.** A vertical scroller whose content is the header, the items (an `SkUiVirtualVerticalStackLayout`), the empty view and the footer. A sticky header or footer is drawn over the list instead (the scroller still fills the whole view): the items scroll behind it, so a translucent background, margins or rounded corners let them show through, and scrolling never re-records it. Its height (margins included) becomes the scroller's inset at that edge: at the start of the list the first item is below the header, at the end the last one above the footer, scrolling to an item places it in the uncovered band, the scroll bar runs beside that band, and the pull-to-refresh indicator comes out from below the header. A drag (or the wheel) on a sticky part scrolls the list; a tap on it never reaches the items behind (views inside it keep their taps). The list needs a bounded height (a grid row, a page); measured with an unbounded height (inside a vertical stack or another scroll view) it is as tall as all its items and creates every one.
- **Items.** Each item's view (from `ItemTemplate`, or a selector) is hosted in a drawn item container: the container takes the item's taps, draws `SelectionBackground` behind the selected item, and is what the engine recycles (with its view, per template). Item views get their item as binding context.
- **Selection.** `SelectionMode` `Single` or `SingleDeselect`: a tap selects its item (a cancelable `SelectionChanging` first), `SelectedItem` changes (two-way), `SelectionChangedCommand` runs and `SelectionChanged` is raised. The root of the selected item's view goes to the `Selected` visual state (MAUI's `CommonStates` group; `Disabled` wins, `Selected` comes before `PointerOver`). A selection change re-records the two items whose state changed; recycled views show the selection of the item they are bound to. Removing the selected item from the source (or replacing the source with one without it) clears the selection. Items are compared with `Equals`.
- **Taps.** `ItemTapped` and `ItemTappedCommand` run for every tap on an item, whatever the selection mode, after the selection changed. Views inside the item that take taps themselves (buttons, views with `Tapped`) keep them: the item is not tapped then.
- **Empty view.** While there are no items (`ItemsSource` is `null` or empty), `EmptyView` shows instead of the items, between the header and the footer, filling what the list has left. `EmptyViewTemplate` creates it the first time the list is empty.
- **Pull to refresh.** With `IsPullToRefreshEnabled`, dragging the top of the list down brings a drawn indicator down with the pull (composite-time, no re-record while dragging), also when the items do not fill the list and when overscroll is off. Releasing past 64 DIPs sets `IsRefreshing`; as MAUI's `RefreshView`, `IsRefreshing` becoming `true` (by a pull or by the app) raises `Refreshing` and runs `RefreshCommand`, and the indicator spins on the render thread until the app sets it back to `false`.
- **Accessibility.** Tappable items are actionable elements (double tap, Enter / Space); a selected item reads `SelectedStateText` ("Selected") as its value.

## How to use

```xml
<sk:SkUiCollectionView ItemsSource="{Binding Orders}"
                       SelectionMode="Single" SelectedItem="{Binding Current}"
                       ItemTappedCommand="{Binding OpenOrder}"
                       IsStickyHeader="True"
                       RemainingItemsThreshold="5"
                       RemainingItemsThresholdReachedCommand="{Binding LoadMore}"
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

From code:

```csharp
var list = new SkUiCollectionView { ItemsSource = orders, ItemTemplate = new DataTemplate(() => new OrderRow()) }
    .SetSelectionMode(SkUiSelectionMode.SingleDeselect)
    .SetHeader(new OrdersHeader(), sticky: true);
list.ItemTapped += (_, args) => Open((Order)args.Item!);
await list.ScrollToItem(orders[^1], ScrollToPosition.End);
```

## Key properties

| Member | Default | Notes |
| --- | --- | --- |
| `ItemsSource`, `ItemTemplate` | `null` | As `SkUiVirtualVerticalStackLayout`: `IList` + `INotifyCollectionChanged` changes are incremental; templates create drawn views, recycled per template (a `DataTemplateSelector` chooses per item). The XAML content is the `ItemTemplate` |
| `ItemSpacing` | 0 | Gap between items (not around the header and footer) |
| `ItemExtent`, `EstimatedItemSize` | 0 | One height for every item (fast path); the height assumed for items not measured yet |
| `PrefetchFactor`, `PrefetchBehindFactor`, `ReleaseFactor`, `PrefetchBudget` | 1, 0.5, 2, automatic | As the virtual layout ([SkUiVirtualVerticalStackLayout.md](SkUiVirtualVerticalStackLayout.md#key-properties-skuivirtualverticalstacklayout)): how far ahead and behind items are created, when they are released, the UI-time budget per frame |
| `Header`, `HeaderTemplate`, `IsStickyHeader` | `null`, `null`, `false` | A drawn view above the items (a template creates it, with the list's binding context); sticky: it stays at the top, drawn over the list, and the items scroll behind it (never covered at the start). `SetHeader(view, sticky)` |
| `Footer`, `FooterTemplate`, `IsStickyFooter` | `null`, `null`, `false` | Below the items (right after the last one), or sticky at the bottom, drawn over the list (never covering the last item at the end). `SetFooter(view, sticky)` |
| `EmptyView`, `EmptyViewTemplate` | `null` | Shown instead of the items while there are none; fills the space left |
| `SelectionMode` | `None` | `None`, `Single`, `SingleDeselect` (tapping the selected item clears it). `None` clears `SelectedItem` |
| `SelectedItem` | `null` | Two-way; shown while the mode is not `None` |
| `SelectionChangedCommand` (+ `…Parameter`) | `null` | Runs when `SelectedItem` changes, before `SelectionChanged` |
| `SelectionBackground` | `null` | A brush (in XAML a color) behind the selected item; `null`: the accent at 12 %; `Transparent`: none (style the item with the `Selected` state) |
| `ItemTappedCommand` (+ `…Parameter`) | `null` | Runs on an item tap, with the parameter when set, else the item |
| `ShowsItemPressEffect` | `false` | Item containers show the press effect while pressed |
| `RemainingItemsThreshold`, `RemainingItemsThresholdReachedCommand` (+ `…Parameter`) | -1 | As the virtual layout: once per item count, when the end comes near |
| `IsPullToRefreshEnabled` | `false` | Pulling the top down and releasing past 64 DIPs starts a refresh |
| `IsRefreshing` | `false` | Two-way; `true` raises `Refreshing` and runs `RefreshCommand` (+ `…Parameter`) |
| `RefreshColor` | `null` | The indicator's arc; `null`: the accent |
| `VerticalScrollBarVisibility`, `Overscroll` | `Default` | Of the list's scroller |
| `ItemCount`, `FirstVisibleIndex`, `LastVisibleIndex`, `ScrollY`, `IsScrolling` | | Read-only; the visible indices and `IsScrolling` raise `PropertyChanged` |
| `ScrollToIndex(index, position, animated)`, `ScrollToItem(item, …)`, `ScrollToAsync(offset, animated)` | | Return `Task`s; an item lands exactly where asked (items between are measured on the way). `ScrollToItem` ignores an item not in the list |
| `GetRealizedView(index)`, `RemeasureItem(index)` | | The template's view of a realized item; measure an item again |
| `ISkUiItemsView` | | The members every list has, with the same names as on `SkUiVirtualScrollView` and `SkUiVirtualVerticalStackLayout` (a test checks they reach the layout inside); code that only configures or follows a list can take any of them |
| `SelectedStateText` (static) | "Selected" | What screen readers read for a selected item; set once to localize |

## Events

| Event | Arguments | When |
| --- | --- | --- |
| `SelectionChanging` | `SkUiSelectionChangingEventArgs` (`PreviousItem`, `CurrentItem`, `Cancel`) | A tap is about to change the selection (taps only) |
| `SelectionChanged` | `SkUiSelectionChangedEventArgs` (`PreviousItem`, `CurrentItem`) | `SelectedItem` changed (tap, app, item removed) |
| `ItemTapped` | `SkUiItemTappedEventArgs` (`Item`, `Index`) | An item was tapped outside views that take taps themselves |
| `Refreshing` | | `IsRefreshing` became `true` |
| `RemainingItemsThresholdReached`, `VisibleRangeChanged`, `Scrolled` | | As on the virtual layout and the scroll view |

## Demo and tests

Demo: **Components → Scrolling → SkUiCollectionView**, next to MAUI's `CollectionView` in a `RefreshView`. Tests: `CollectionViewTests`.
