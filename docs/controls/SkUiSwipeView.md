# SkUiSwipeView

Content with actions revealed by a swipe: list rows that archive, delete or flag, cards with a reply action, a panel pulled down for a quick answer.

**MAUI counterpart:** `SwipeView`. Its XAML ports by changing the prefix: `SwipeItems` and `SwipeItem` stay MAUI's own types (the drawn view draws them), the content becomes drawn views, and `SwipeItemView` becomes [`SkUiSwipeItemView`](#skuiswipeitemview) (MAUI's hosts native views). Beyond MAUI: `IsOpen`, `Padding`, and the content of an open view does not take taps.

## How it works

- **Sides.** Swiping the content to the right reveals `LeftItems`, to the left `RightItems`, down `TopItems`, up `BottomItems`. A side without visible items is not revealed. Left and right are the physical sides, also in right-to-left layouts.
- **Items.** Each side is a MAUI `SwipeItems` collection of `SwipeItem`s (drawn as buttons: `BackgroundColor`, `IconImageSource` above `Text`, the text white or black by the background's luminosity, as MAUI's handlers do; `IsEnabled`, `IsVisible`, `Command` / `CommandParameter`, `Invoked` and `Clicked` work as on MAUI) or `SkUiSwipeItemView`s. A MAUI `SwipeItemView` or another item type throws `NotSupportedException` when added. Items follow their changes while shown (text, colors, visibility, the collection), also items shared through resources: a shared collection or item keeps no swipe view alive.
- **Sizes**, as MAUI's handlers: beside the content, a `SwipeItem` is 100 DIPs wide (`Reveal`) or the items share the content's width (`Execute`), an item view is as wide as it measures (100 DIPs when it measures nothing), and all are as tall as the content. Above or below it, the items share the content's width and are as tall as the content, or as the tallest item view. Items are revealed beside the content inside `Padding`.
- **Opening (`SwipeMode.Reveal`, default).** The content follows the finger up to the items' length, and the items slide in with its edge. A release past `Threshold` (default 60 % of the items' length; a larger value means the full length) opens them; a fast swipe opens or closes whatever the distance; otherwise the content springs back. A drag that passes the closed position reveals the other side of the same axis when it has items.
- **Executing (`SwipeMode.Execute`).** A release past the threshold (of 80 % of the content's length, or the item views' length) invokes the first visible item when it is enabled (a `SwipeItem`'s `IsEnabled`, which follows its command; an item view that is enabled and whose command can execute), as MAUI's handlers do; nothing opens, and a disabled first item is not replaced by the next one. A `SwipeItem`'s icon and text sit at its inner edge, next to the content (MAUI centers them), so a short swipe already shows what a full one will do; in `Reveal` they are centered.
- **Invoking.** A tap on an item invokes it: `Command` (when it can execute), then the item's events. The view then closes, unless the side's `SwipeBehaviorOnInvoked` is `RemainOpen` (MAUI's handlers close on `Auto` and `Close` in both modes).
- **Closing.** A tap on the content of an open view closes it, and the content does not get the tap. A drag back closes it too. A scroll of a drawn scroller around the view (the list it is a row of) closes it, as does a new binding context: a recycled list row shows closed. `Close()` closes it from code.
- **`Open(OpenSwipeItem, animated)` / `Close(animated)`.** Open or close from code, animated by default (200 ms, as MAUI's handlers). `Open` on a side without visible items does nothing; before the view is laid out, it opens once it is. `IsOpen` is `true` while items are open or opening (bindable, read-only). The swipe events are raised only for swipes, not for `Open` / `Close`.
- **Events.** `SwipeStarted` when a swipe starts moving the content (`SwipeDirection` of the side it reveals), `SwipeChanging` on every move (`Offset`: the content's offset in DIPs, negative to the left and up), `SwipeEnded` on release (`IsOpen`). These are MAUI's event argument types.
- **Gestures.** The swipe is an arena member ([EventMechanism.md](../design/EventMechanism.md)). It claims a drag that passes the touch slop towards a side with items. A drag along the other axis is left to scrollers, so a vertical list of swipe rows scrolls, and a horizontal drag on a row swipes it. Taps on the content, and taps the list handles (`ItemTapped`, selection), work as without the swipe view; tappable views inside the content keep their taps; views in the content with their own drags (a slider) win over the swipe.
- **Drawing.** The content and the items move at composite time: a swipe, an animation or an open view re-records nothing. A side's items are created the first time that side is revealed. The view clips to its bounds (`ClipToBounds` is `true`, as MAUI's platform views), which also clips hosted native views in the content while it moves.
- **Accessibility.** Open items are buttons for screen readers and the keyboard; closed items are not in the semantics tree, and there are no custom actions for them yet (open the view from your own control, e.g. a menu button, for users who cannot swipe).
- **Disabled** (`IsEnabled = false`): no swipes.

## SkUiSwipeItemView

A swipe item of drawn views: MAUI's `SwipeItemView` (a `ContentView` with `Command`, `CommandParameter` and `Invoked`) on the drawn surface. A tap invokes it: `Command` runs (when it can execute), then `Invoked`. While `Command` cannot execute it is disabled (MAUI disables its `SwipeItemView` the same way). It is an [`SkUiContentView`](SkUiContentView.md): `Content`, `ContentTemplate`, `Padding`, `Background`. Views inside it keep their own input (a switch toggles). Until its side is first revealed it is the logical child of its `SwipeItems`, as on MAUI.

## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`swipe.SetContent(row).SetThreshold(80)`). Getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10). Invalid values: `Set*` throws; XAML, bindings, styles and the property setter ignore them with a logged warning, as MAUI does.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).

## How to use

```xml
<sk:SkUiCollectionView ItemsSource="{Binding Messages}">
  <sk:SkUiCollectionView.ItemTemplate>
    <DataTemplate x:DataType="local:Message">
      <sk:SkUiSwipeView>
        <sk:SkUiSwipeView.LeftItems>
          <SwipeItems>
            <SwipeItem Text="Flag" BackgroundColor="Orange"
                       Command="{Binding FlagCommand, Source={RelativeSource AncestorType={x:Type local:InboxViewModel}}}"
                       CommandParameter="{Binding .}" />
          </SwipeItems>
        </sk:SkUiSwipeView.LeftItems>
        <sk:SkUiSwipeView.RightItems>
          <SwipeItems Mode="Execute">
            <SwipeItem Text="Delete" IconImageSource="delete.png" BackgroundColor="Crimson"
                       Command="{Binding DeleteCommand, Source={RelativeSource AncestorType={x:Type local:InboxViewModel}}}"
                       CommandParameter="{Binding .}" />
          </SwipeItems>
        </sk:SkUiSwipeView.RightItems>
        <sk:SkUiVerticalStackLayout Padding="16,10" BackgroundColor="White">
          <sk:SkUiLabel Text="{Binding From}" FontAttributes="Bold" />
          <sk:SkUiLabel Text="{Binding Subject}" />
        </sk:SkUiVerticalStackLayout>
      </sk:SkUiSwipeView>
    </DataTemplate>
  </sk:SkUiCollectionView.ItemTemplate>
</sk:SkUiCollectionView>
```

From MAUI, change `SwipeView` to `sk:SkUiSwipeView`, its content to drawn views, and each `SwipeItemView` to `sk:SkUiSwipeItemView` with drawn content. `SwipeItems`, `SwipeItem`, the modes, the events and `Open` / `Close` stay as written. `OpenRequested` / `CloseRequested` (for handlers) do not exist.

## Key properties

| Property | Default | Notes |
| --- | --- | --- |
| `Content` | — | Drawn view that is swiped |
| `LeftItems` / `RightItems` / `TopItems` / `BottomItems` | empty `SwipeItems` | MAUI's collection: `Mode` (`Reveal` / `Execute`), `SwipeBehaviorOnInvoked` |
| `Threshold` | `0` | DIPs a release must pass; `0`: 60 % of the open distance |
| `Padding` | `0` | Inset around the content (items stay beside the content) |
| `IsOpen` | `false` | Items open or opening (read-only) |
| `SwipeStarted` / `SwipeChanging` / `SwipeEnded` (events) | — | MAUI's argument types |
| `Open(OpenSwipeItem, bool)` / `Close(bool)` | — | Animated by default |

## Related

[SkUiCollectionView](SkUiCollectionView.md) (swipe views as rows) · [SkUiExpander](SkUiExpander.md) · Gallery: `SwipeViewDemoPage` (next to MAUI's `SwipeView`: both sides, top item views, modes, invoke behavior, threshold), `SwipeItemViewDemoPage` (drawn item views: a reply button, a switch), `SwipeViewListDemoPage` (mail rows in a collection view next to MAUI's) · Tests: `SwipeViewTests`, `SwipeViewXamlTests`; leak scenario `SwipeViewsSwiped`
