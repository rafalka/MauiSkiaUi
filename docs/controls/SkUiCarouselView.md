# SkUiCarouselView

A drawn carousel: a strip of cards scrolled one at a time (a photo gallery, onboarding pages, featured products), with a current item, looping past the last card back to the first, and an indicator view underneath.

**MAUI counterpart:** `CarouselView`. Most of its XAML ports by changing the prefix; the items layout becomes properties of the carousel (`ItemsLayout` → `Orientation`, `ItemSpacing`, `SnapPointsType`, `SnapPointsAlignment`), as on [`SkUiCollectionView`](SkUiCollectionView.md), and item templates create drawn views. Beyond MAUI: several cards in view (`ItemExtent`), item effects as the cards scroll (cover flow, scaling, or your own), `ScrollPosition`, `ScrollToIndex` / `ScrollToItem` returning a task. Its indicator view is [`SkUiIndicatorView`](SkUiIndicatorView.md).

## How it works

- **Layout.** By default every item fills the carousel along its axis minus `PeekAreaInsets`, so the current card is centered and its neighbors peek into the insets (`ItemSpacing` apart). With `ItemExtent` (a SkiaUi extension) every item is that long and as many show as fit. Across the axis the items are as tall as the carousel (as wide, vertically); measured without a height (in a vertical stack), the carousel is as tall as its tallest item in view. It should be bounded along its axis (a page, a grid column): measured unbounded, it is as long as one item and the insets.
- **Current item and snapping.** `SnapPointsAlignment` says where the current item lines up: `Center` (default, MAUI's), `Start` or `End`, inside the peek insets. `SnapPointsType` says how drags and flings end: `MandatorySingle` (default) one item per swipe however hard, `Mandatory` on the item nearest to where the fling would stop, `None` anywhere. A slow drag of less than half an item settles back. The wheel and trackpad settle once they pause.
- **Position.** `Position` (two-way) is the index of the item at the snap point; `CurrentItem` (two-way) is that item. They follow scrolling as another item reaches the snap point (`PositionChanged`, `CurrentItemChanged`, their commands; MAUI's argument types). Setting either scrolls there, animated with `IsScrollAnimated`; during that scroll they stay at the target. Set before the items arrive, `Position` applies once they do; beyond the last item it becomes the last. `ScrollPosition` is fractional while scrolling (2.5: halfway from the third item to the fourth), for effects of the app's own.
- **Looping** (`Loop`, default `true` as MAUI's). Past the last item the first follows, and before the first the last, without an end and without a jump: the carousel repeats its items along a strip of several copies and, once it is far from the middle copy, moves the scroll offset back by whole cycles. Nothing shows the move (also during a fling, a snap or a drag, which go on from the corrected offset). Setting the position takes the shorter way round. A single item does not loop; a loop with fewer items than fit in view shows them repeated.
- **Virtual items.** Only the items in view and one on either side have views (more with an effect that shows more); views are recycled per template (a `DataTemplateSelector` chooses per item) as the carousel scrolls, and a looping carousel shows each item in as many views as copies of it are in view. Item views take MAUI's visual states `CurrentItem`, `NextItem`, `PreviousItem` and `DefaultItem` (`SkUiCarouselView.CurrentItemVisualState`, …); when copies of an item show, only the copy at the snap point and its neighbors take the first three.
- **Item changes.** With an `INotifyCollectionChanged` source (listened to weakly) every change keeps the current item current where it shows; when it was removed, the position stays. Once every item is gone, `Position` goes back to 0 and `CurrentItem` to `null`. Items appended or inserted while the carousel flings do not stop the fling. A new `ItemsSource` or `ItemTemplate` creates the views again.
- **Loading more.** `RemainingItemsThreshold` (MAUI's): when the last item in view is that many items or fewer from the end, `RemainingItemsThresholdReached` is raised and its command runs, once per item count; append the next items and it fires again near the new end. In a looping carousel the current item counts instead of the last in view (load more while the user goes round).
- **Swiping.** Drags take part in the gesture arena as a horizontal (or vertical) scroller: taps on cards work, a carousel nested in a vertical page gets the sideways drags, and at the ends of a carousel that does not loop the drag bounces (`IsBounceEnabled`, per the look's overscroll) or chains to an outer scroller. `IsSwipeEnabled = false` turns drags and the wheel off; `Position`, the keyboard and screen readers still scroll.
- **Keyboard and screen readers.** With focus inside the carousel, the arrow keys step one item (in the reading direction, also right to left), Home and End go to the first and last items, and screen readers' scroll actions page item by item.
- **Right to left.** The strip runs from the right: the first item shows, the next one is to its left.
- **Indicator.** `IndicatorView` links an [`SkUiIndicatorView`](SkUiIndicatorView.md) (MAUI's link, usually `{x:Reference}`): its count and position follow the carousel, its selection slides with the scrolling (across the wrap of a looping carousel), and a tap on a dot scrolls the carousel there.
- **Empty view.** `EmptyView` (or `EmptyViewTemplate`) shows instead of the items while there are none.
- **Performance.** Scrolling, flings and snaps run on the render thread, as in the scroll view, and record nothing; item effects are evaluated by the compositor (below). A collection change rebinds only the views whose item changed.

Not available: MAUI's `ItemsUpdatingScrollMode` (the current item is always kept), `VisibleViews`, a grouped source, autoplay (set `Position` from a timer), several items per snap point.

## Item effects

`ItemEffect` (a SkiaUi extension, default `null`: the cards move with the scrolling as in a scroll view) transforms each item from its position relative to the current place: placement, scale, rotation, a tilt in perspective, opacity and drawing order. The compositor evaluates it for every item in every frame from the scroll offset on the render thread, so effects follow flings smoothly, re-record nothing and keep going while the UI thread is busy; hit-testing uses the same transform (nearer items are hit over farther ones).

- **`SkUiCoverFlowEffect`**: the current item faces you; the others turn towards it (`RotationAngle`, 50°), shrink (`SideItemScale`, 0.8) and stack closely on either side (`NeighborOffset`, `SideItemSpacing`, in item lengths), nearer ones over farther ones; `VisibleSideItems` (3) on either side, farther ones fade out. Give the items an `ItemExtent` of about half the width.
- **`SkUiScaleEffect`**: side items shrink (`SideItemScale`, 0.85) and fade (`SideItemOpacity`, 0.6) as they leave the current place and move closer, so the gaps stay `ItemSpacing` (`KeepsSpacing`) and more of the peeking neighbors shows.

An effect of your own derives from `SkUiCarouselEffect` and returns an `SkUiCarouselItemTransform` for a position (0 at the snap point, 1 one item after it to the right or below, fractional while scrolling; physical, also right to left). It runs on the render thread: compute only from the arguments and the effect's own settings, without allocating; call `OnChanged()` from your settings' setters. Override `GetVisibleRange` when the effect shows items the layout would not (it pulls them in), and `GetPerspective` for the tilt's camera distance (default three item sizes).

```csharp
/// <summary>Side items sink and turn a little, like cards on a table.</summary>
public sealed class TableEffect : SkUiCarouselEffect
{
    public override SkUiCarouselItemTransform GetItemTransform(double position, in SkUiCarouselItemMetrics metrics)
    {
        var near = Math.Min(Math.Abs(position), 1);
        return new SkUiCarouselItemTransform
        {
            CrossTranslation = near * metrics.ItemThickness * 0.08,
            Rotation = Math.Clamp(position, -1, 1) * 6,
            Scale = 1 - near * 0.1,
            ZIndex = -Math.Abs(position)
        };
    }
}
```

## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`carousel.SetItemsSource(photos).SetLoop(false)`). Getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10).
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).

## How to use

```xml
<sk:SkUiGrid RowDefinitions="*,Auto" RowSpacing="8">
  <sk:SkUiCarouselView x:Name="Gallery"
                       ItemsSource="{Binding Destinations}"
                       CurrentItem="{Binding Selected}"
                       PeekAreaInsets="40,0"
                       ItemSpacing="12"
                       IndicatorView="{x:Reference Dots}"
                       RemainingItemsThreshold="2"
                       RemainingItemsThresholdReachedCommand="{Binding LoadMoreCommand}">
    <DataTemplate x:DataType="local:Destination">
      <sk:SkUiBorder StrokeShape="RoundRectangle 16" StrokeThickness="0" BackgroundColor="{Binding Color}" Padding="16">
        <sk:SkUiLabel Text="{Binding Name}" TextColor="White" FontSize="22" VerticalOptions="End" />
      </sk:SkUiBorder>
    </DataTemplate>
  </sk:SkUiCarouselView>
  <sk:SkUiIndicatorView x:Name="Dots" Grid.Row="1" HorizontalOptions="Center" />
</sk:SkUiGrid>
```

Cover flow, three cards wide:

```xml
<sk:SkUiCarouselView ItemsSource="{Binding Albums}" ItemExtent="180" HeightRequest="220">
  <sk:SkUiCarouselView.ItemEffect>
    <sk:SkUiCoverFlowEffect RotationAngle="45" SideItemScale="0.75" />
  </sk:SkUiCarouselView.ItemEffect>
  …
</sk:SkUiCarouselView>
```

From MAUI: change `CarouselView` to `sk:SkUiCarouselView` and `IndicatorView` to `sk:SkUiIndicatorView`, make the template's views drawn, and move the `ItemsLayout`'s settings onto the carousel:

```xml
<!-- MAUI -->
<CarouselView.ItemsLayout>
  <LinearItemsLayout Orientation="Horizontal" ItemSpacing="12" SnapPointsType="MandatorySingle" SnapPointsAlignment="Center" />
</CarouselView.ItemsLayout>
<!-- SkiaUi -->
<sk:SkUiCarouselView ItemSpacing="12" … />  <!-- horizontal, MandatorySingle and Center are the defaults -->
```

## Key properties

| Property | Default | Notes |
| --- | --- | --- |
| `ItemsSource` / `ItemTemplate` | — | Drawn template views; a selector chooses per item; without a template, the item's text |
| `Position` / `CurrentItem` | `0` / `null` | Two-way; follow scrolling; setting them scrolls there |
| `PositionChanged` / `CurrentItemChanged` (+ commands) | — | MAUI's argument types |
| `Loop` | `true` | Endless both ways (not with one item) |
| `PeekAreaInsets` | `0` | Room at the ends of the axis where neighbors peek; shortens filling items |
| `ItemExtent` | `0` | When positive, every item's length along the axis (several in view); 0 fills (SkiaUi) |
| `ItemSpacing` | `0` | Gap between items |
| `Orientation` | `Horizontal` | `ItemsLayoutOrientation` |
| `SnapPointsType` / `SnapPointsAlignment` | `MandatorySingle` / `Center` | MAUI's `LinearItemsLayout` settings |
| `IsSwipeEnabled` / `IsBounceEnabled` / `IsScrollAnimated` | `true` | As MAUI's |
| `IsDragging` / `IsScrolling` | — | Read-only |
| `ScrollPosition` | — | Fractional item at the snap point (SkiaUi) |
| `ItemEffect` | `null` | `SkUiCoverFlowEffect`, `SkUiScaleEffect`, or your own (SkiaUi) |
| `IndicatorView` | `null` | A linked `SkUiIndicatorView` |
| `RemainingItemsThreshold` (+ event, command) | `-1` | Load more near the end |
| `EmptyView` / `EmptyViewTemplate` | — | Shown without items |
| `ScrollToIndex(index, animated)` / `ScrollToItem(item, animated)` | — | Tasks that complete when the scroll ends (SkiaUi); MAUI's `ScrollTo` overloads too |
| `Scrolled` (event) | — | MAUI's `ItemsViewScrolledEventArgs` |
