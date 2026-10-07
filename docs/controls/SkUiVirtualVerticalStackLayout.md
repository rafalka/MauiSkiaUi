# SkUiVirtualVerticalStackLayout / SkUiVirtualScrollView / SkUiVirtualVerticalStackLayoutBase

A vertical stack whose item views are created on demand: only the items near what its scrollers show exist. Long lists, feeds and logs stay on one drawn surface without a view per item (FR-21, indexed mode).

**`SkUiVirtualVerticalStackLayout` needs a drawn scroller above it.** It does not scroll itself; it follows the `SkUiScrollView`s around it. Use `SkUiVirtualScrollView` (a scroll view with one inside) for a plain list, or put the layout inside your own `SkUiScrollView` (see [Which one](#which-one)).

**MAUI counterpart:** none directly. `SkUiVirtualScrollView` covers plain `CollectionView` lists (`ItemsSource`, `ItemTemplate`, `RemainingItemsThreshold`, `ScrollTo` by index); selection, header / footer, empty view and grouping come with `SkUiCollectionView` (Phase B2–B3, built on this layout). Design: [ScrollingAndCollectionViews.md](../design/ScrollingAndCollectionViews.md#virtual-stack-implemented).

## How it works

- **Visible window.** The layout's window is what its ancestor scrollers can show: the intersection of every ancestor `SkUiScrollView`'s viewport (and the surface), in the layout's own coordinates. So the layout works as a scroller's content, below a header or other content in a scrolled page, or inside another scrolled list, and only realizes what could be visible.
- **Indexed extent.** Every item keeps its index and its place in the content: the layout is as tall as all items together, so the scroll offset, the scroll bar and `ScrollToIndex` keep their meaning.
- **Sizes.** Each item may have a different height. Realized items are measured (against the layout's width, with an unconstrained height); their heights are kept after they are released. Items never measured are estimated: `EstimatedItemSize`, or else the average height measured so far. `ItemExtent` gives every item one height: the fast path, where positions need no measuring and nothing is estimated.
- **Scroll anchoring.** When items before the first visible one change height (measured for the first time while scrolling up, an image loaded, inserted or removed), the scroll offset moves by the same amount, so what shows stays in place. This also works during a fling or an animated scroll on the render thread: the correction reaches the running motion with the frame that brings the new layout, and the motion continues from it. While the start of the list shows, nothing is anchored (inserted items at the top push the others down).
- **Prefetch and budget.** Visible items are created at once. Items up to `PrefetchFactor` viewport lengths ahead (in the scroll direction; while a fling runs, further by the distance it is about to travel) and `PrefetchBehindFactor` behind are created before they show, within a UI-thread time budget per frame (at least one item per frame) that is chosen automatically from the measured frame rate, item cost and scroll speed; the rest continues on the next frames.
- **Release and recycling.** Items further than `ReleaseFactor` viewport lengths from the window are released. Template views go back to a pool per template (per `DataTemplateSelector` result) and are rebound to another item (`BindingContext`) instead of being created again. Scrolling never re-records or remeasures items that stay; an item that changes size by itself (its content changed) is measured again in the next layout.
- **Collection changes.** With an `INotifyCollectionChanged` source, inserts, removes, moves and replacements realize or release only the items they touch; the rest keep their views and sizes. The source is listened to weakly: a long-lived collection does not keep the layout alive.
- **Items are ordinary drawn children:** taps, buttons, nested carousels and screen readers work as anywhere else; scrolling is the ancestor scroller's.

## How to use

```xml
<sk:SkUiVirtualScrollView ItemsSource="{Binding Orders}" Spacing="4"
                          RemainingItemsThreshold="5"
                          RemainingItemsThresholdReachedCommand="{Binding LoadMoreCommand}">
  <DataTemplate x:DataType="local:Order">
    <sk:SkUiBorder Padding="12" StrokeShape="RoundRectangle 8">
      <sk:SkUiLabel Text="{Binding Title}" />
    </sk:SkUiBorder>
  </DataTemplate>
</sk:SkUiVirtualScrollView>
```

A list section in a scrolled page, below other content:

```xml
<sk:SkUiScrollView>
  <sk:SkUiVerticalStackLayout>
    <sk:SkUiLabel Text="Recent" FontSize="22" />
    <sk:SkUiScrollView Orientation="Horizontal"><!-- carousel --></sk:SkUiScrollView>
    <sk:SkUiVirtualVerticalStackLayout ItemsSource="{Binding Feed}" ItemTemplate="{StaticResource FeedCard}" />
  </sk:SkUiVerticalStackLayout>
</sk:SkUiScrollView>
```

Items from code, endless (the factory is asked until it returns `null`) or with a known count:

```csharp
var log = new SkUiVirtualScrollView()
    .SetItemFactory(index => index < entries.Count ? new SkUiLabel { Text = entries[index] } : null);
await log.ScrollToIndex(entries.Count - 1, ScrollToPosition.End, animated: false);
```

Item views that take their data from the binding context in `OnBindingContextChanged` (instead of bindings) rebind fastest when recycled.

## Key properties (`SkUiVirtualVerticalStackLayout`)

| Member | Default | Notes |
| --- | --- | --- |
| `ItemsSource` | `null` | Items; one view each from `ItemTemplate`, with the item as its binding context. `IList` sources are read by index; other sequences are copied once. Takes precedence over `ItemFactory` |
| `ItemTemplate` | `null` | Drawn views only; a `DataTemplateSelector` chooses per item (views are recycled per selected template). Without a template, items show their text in an `SkUiLabel` |
| `ItemFactory`, `ItemFactoryCount` | `null` | Items by index from code (no recycling: pool views in `ItemReleased`). Without a count, the list is endless until the factory returns `null`. `SetItemFactory(factory, count)` |
| `Spacing`, `Padding` | 0 | Gap between items; space around them |
| `ItemExtent` | 0 | When positive, the height of every item (fast path) |
| `EstimatedItemSize` | 0 | Height assumed for items not measured yet; 0: the average measured height |
| `PrefetchFactor` | 1 | Viewport lengths created ahead of the window, in the scroll direction (plus a running fling's travel) |
| `PrefetchBehindFactor` | 0.5 | Viewport lengths kept created behind the window |
| `ReleaseFactor` | 2 | Items further than this many viewport lengths are released (never closer than the prefetch distance plus half a viewport); `∞` keeps every created item |
| `PrefetchBudget` | `null` (automatic) | UI-thread time per frame for prefetch (at least one item per frame); leave it automatic |
| `RemainingItemsThreshold` | -1 | When what shows changes and the last visible item is this close to the end, `RemainingItemsThresholdReached` is raised and `RemainingItemsThresholdReachedCommand` (+ `…Parameter`) runs, once per item count |
| `ItemCount`, `FirstVisibleIndex`, `LastVisibleIndex` | | Read-only; the visible indices raise `PropertyChanged` |
| `ScrollToIndex(index, position, animated)` | | Scrolls the innermost vertical scroller around the layout (`MakeVisible`, `Start`, `Center`, `End`). The item is measured first; it keeps its place on screen while items before it are measured on the way, so it lands where asked |
| `GetRealizedView(index)`, `RemeasureItem(index)` | | The item's view while realized; measure an item again (a released item's height is forgotten and estimated) |
| `ItemRealized`, `ItemReleased` | | `SkUiVirtualItemEventArgs`: `Index`, `View`, `Item` |
| `VisibleRangeChanged` | | `SkUiVisibleRangeChangedEventArgs`: `FirstVisibleIndex`, `LastVisibleIndex` |

`SkUiVirtualScrollView` is a vertical `SkUiScrollView` whose content is the layout (`Items`; do not replace `Content`), and keeps everything a scroll view has (scroll bars, overscroll, snap points, `Scrolled`). In XAML its content is the `ItemTemplate`.

Both implement **`ISkUiVirtualList`**: the list members above except `Padding` (the scroll view has its own) and the fluent setters. The scroll view forwards each of them to `Items`, and declares its bindable properties from the layout's (same names, types, defaults and validation), so the two cannot drift apart; a test checks every member of the interface. Code that only configures or follows a list can take an `ISkUiVirtualList`.

### Which one

- The list is the whole scrolling area (the usual `CollectionView` replacement): `SkUiVirtualScrollView`.
- The list is one section of a longer scrolled page (below a header, a carousel, a form): `SkUiVirtualVerticalStackLayout` inside your own `SkUiScrollView`.
- Never `SkUiVirtualVerticalStackLayout` without a drawn scroller above it:
  - **directly in a page:** it is as tall as all its items, clipped by the surface; it creates only the first screenful and nothing scrolls;
  - **in a native MAUI `ScrollView` around the surface:** the surface becomes as tall as the list, so every item is created (no virtualization). Make the list drawn scrolling instead: an `SkUiScrollView` (or `SkUiVirtualScrollView`) inside the surface, with the surface given a height.

  Shown that way, the layout writes one `SkiaUi:` trace line (debug output) per instance: `… has no drawn scroller (SkUiScrollView) above it …`.

### `PrefetchBudget`

Creating an item view (inflating its template, binding, measuring) runs on the UI thread. Items that show are always created at once, whatever it costs: a visible gap would be worse than a slow frame. The prefetch budget limits only the items created ahead of time: per UI frame, prefetch stops once the budget is spent (always after at least one item), and continues on the next frame. Flings run on the render thread and do not stutter when the UI thread is busy; too small a budget lets prefetch fall behind a fast fling (rows show blank at the edge for a frame or two), too large a budget makes UI frames long while the list fills.

**Automatic (default, `null`).** Nothing to tune; the budget is chosen every frame from measurements:

- **Frame length:** measured from the UI frames (the surface's animation clock), so 60, 90 and 120 Hz screens, and a UI running slower than its display, are all accounted for.
- **Item cost:** each list measures what realizing its items takes (template or recycling, binding, measuring). An expensive template or a slow device raises it.
- **Idle:** a quarter of a frame (about 4 ms at 60 Hz, 2 ms at 120 Hz).
- **Scrolling:** what the items scrolling into the prefetch area per frame cost (speed × frame length ÷ item height × item cost, with a 1.5× margin), at least a quarter and at most three quarters of a frame.
- **Falling behind:** when items had to be created as they came into view during a scroll, the next budgets are doubled (up to 4×), then relax again.

**Fixed.** Setting a `TimeSpan` replaces the automatic budget: `TimeSpan.Zero` creates one prefetched item per frame (deterministic steps, tests); a large value creates everything ahead in one frame. The **SkUiVirtualVerticalStackLayout** demo page shows the item cost, the budget in use and the frame length in its status line.

## Custom lists (`SkUiVirtualVerticalStackLayoutBase`)

`SkUiVirtualVerticalStackLayout` is one subclass of an abstract engine, `SkUiVirtualVerticalStackLayoutBase`. Derive from the engine for a list control of your own that exposes only the API it needs (no `ItemsSource`, `ItemTemplate` or `ItemFactory` that would mean nothing for it): a contact list, a calendar, a log viewer, a chat. The base class keeps everything about the window, sizes, anchoring, prefetch, release, recycling, events and `ScrollToIndex`, and the public settings above (`Spacing`, `ItemExtent`, `PrefetchFactor`, …).

The subclass answers per item index, and reports the items:

| Member | Kind | What it does |
| --- | --- | --- |
| `CreateItemView(index, recycleKey)` | abstract | Creates a new view for an item (a drawn, unparented `ISkUiView`). Returns `null` only to end an endless list |
| `GetRecycleKey(index)` | virtual (`null`) | Views with the same key are interchangeable (a template, a kind of row): released views are kept per key and rebound to the next item with that key. `null`: not recycled |
| `BindItemView(index, view, recycleKey)` | virtual | Shows an item in a new or recycled view, before it is attached and measured |
| `UnbindItemView(index, view)` | virtual | A view left the layout: stop work started for it (image loads, subscriptions) |
| `GetItem(index)` | virtual (`null`) | The item reported in `ItemRealized` / `ItemReleased` |
| `ResetItems(count, hasMoreItems)` | protected | The items were replaced (also the first time): every view released, sizes forgotten |
| `InsertItems`, `RemoveItems`, `ReplaceItems`, `MoveItems` | protected | Incremental changes: only the items they touch are realized or released; what shows stays in place |
| `HasMoreItems` | protected | An endless list: while `true`, the layout asks `CreateItemView` for item `ItemCount` near the end (`null` ends it); set it again when more may follow |
| `TakeRecycledView(key)`, `ClearRecycledViews()` | protected | A recycled view for a new item of an endless list; drop the pool when keys stop creating the same views |

```csharp
public sealed class ContactList : SkUiVirtualVerticalStackLayoutBase
{
    private IReadOnlyList<Contact> _contacts = [];

    public void Show(IReadOnlyList<Contact> contacts)
    {
        _contacts = contacts;
        ResetItems(contacts.Count);
    }

    protected override object? GetRecycleKey(int index) => _contacts[index].IsGroup ? "header" : "row";

    protected override ISkUiView CreateItemView(int index, object? recycleKey) =>
        recycleKey is "header" ? new SkUiLabel { FontSize = 13 } : new ContactRow();

    protected override void BindItemView(int index, ISkUiView view, object? recycleKey)
    {
        if (view is SkUiLabel header)
            header.Text = _contacts[index].Name;
        else
            ((ContactRow)view).Show(_contacts[index]);
    }
}
```

Use it like the built-in layout, inside a drawn scroller (`<sk:SkUiScrollView><local:ContactList x:Name="Contacts" /></sk:SkUiScrollView>`). The calls come on the UI thread while scrolling and laying out: keep them cheap, and change the items (`ResetItems`, `InsertItems`, …) outside them.

## Limits

- Vertical only; horizontal lists, `InfiniteFeed` and `Loop` modes (FR-21 B / Loop) are later.
- No Core twin (`SkUiCoreVirtualVerticalStackLayout`) yet.
- A virtual layout inside a horizontal scroller realizes items by the vertical window only.
- The first layout does not know where the layout will be placed: it realizes as if at the top of its scroller's viewport (a little more than needed below a header).
- `ScrollToIndex` with an animation through many estimated items keeps the target exact by moving the offset as items are measured; the content passing by can jump slightly during such a jump.

## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form: getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10). Invalid values: `Set*` throws; XAML, bindings, styles and the property setter ignore them with a logged warning, as MAUI does.
- **Gestures** use SkiaUi's gesture arena. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).

## Related

Gallery: `VirtualScrollViewDemoPage` (next to MAUI's `CollectionView`), `VirtualVerticalStackLayoutDemoPage` (a feed below a header and a carousel; paged or endless). Tests: `VirtualVerticalStackLayoutTests`; leak scenario `VirtualListScrolled`; benchmark `virtual-fling`.
