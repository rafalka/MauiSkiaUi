# Component page editor help

The (?) next to a component page's editor shows a description. An editor named after a property shows the property's XML
documentation (SkiaUi's, else MAUI's), embedded at build time. This file describes the editors that set up a scenario
rather than one property, and replaces a property's text where the demo needs its own.

Each `## Page.Editor` section is the description of one editor: the page's class name and the editor's title as shown.
`ComponentDemoPage.` sections are for the editors every page has. Formatting: paragraphs, `-` and `1.` lists, `**bold**`,
`*italic*`, `` `code` `` and `[links](https://example.com)`.

## ComponentDemoPage.Background brush

The `Background` of both previews: a solid color (the page's fill), or a linear or radial gradient brush. Drawn controls
paint a gradient like MAUI's native ones, clipped to their shape.

## ViewDemoPage.ToolTip

MAUI's attached tooltip (`ToolTipProperties.Text`) on the drawn view. With a mouse or trackpad (Mac Catalyst, Windows, an
iPad's pointer), resting the pointer on the preview shows it as a system tooltip; phones show none.

## GestureRecognizersDemoPage.Recognizer

The MAUI gesture recognizer both previews get, in their `GestureRecognizers`. The drawn view runs it in SkiaUi's gesture
arena; the native `BoxView` runs it through MAUI's platform code. Each side shows the last event it raised:

- `Tap`, `DoubleTap`, `TripleTap`: `TapGestureRecognizer` with 1, 2 or 3 `NumberOfTapsRequired`.
- `SecondaryTap`: `Buttons="Secondary"`, a right click (mouse, trackpad or pen).
- `Swipe`, `Pan`, `Pinch`: the status, totals, direction or scale MAUI reports.
- `Pointer`: entered, moved and exited while a mouse or trackpad hovers; pressed and released for both buttons.

## ButtonDemoPage.ToolTip

MAUI's attached tooltip (`ToolTipProperties.Text`) on both buttons. With a mouse or trackpad (Mac Catalyst, Windows, an
iPad's pointer), resting the pointer on a button shows it as a system tooltip; phones show none.

## ButtonDemoPage.ContentLayout spacing

The space between a button's image and its text (`ContentLayout.Spacing`, MAUI's `ButtonContentLayout`). Pick an image
first: without one there is nothing to space.

## ButtonDemoPage.CanExecute

Whether the buttons' `Command` can execute. When it cannot, the button is disabled as MAUI's is: dimmed, and taps are
ignored (`IsEnabled` stays as set).

## LabelDemoPage.Script

Sample texts in other scripts, written into the `Text` editor: Arabic and Hebrew (right to left, and mixed with Latin),
Devanagari, CJK and emoji. They exercise text shaping, bidirectional layout and font fallback against the native label.
**HTML** is markup for `TextType = Html`.

## ImageDemoPage.Placeholders (SkiaUi)

SkiaUi only: images shown in place of the source (`SetPlaceholders`): a spinner while it loads, and a warning glyph when it
fails. Pick a slow or broken sample to see them.

## ImageDemoPage.Core image, same MAUI source (SkUiCoreImage.SetSource)

A Core-layer image node given the same MAUI `ImageSource` as the previews: both layers share one image loader and cache.

## ImageDemoPage.Clear memory cache

Empties SkiaUi's in-memory image cache (`SkUiImageCache.ClearMemoryCache`); images on screen keep their pixels until they
load again.

## ImageDemoPage.Clear all image caches

Empties the in-memory and the on-disk image caches (`SkUiImageCache.ClearAsync`): the next load downloads again.

## IndicatorViewDemoPage.IndicatorStyle (look)

The look's style for every indicator view of the app (`DefaultSkUiLook.IndicatorStyle`): `Dots`, `Pill` (Material's) or
`Pips` (Fluent's pips pager: small dots, the selected one and one under the mouse larger). `Default` is the look's own.
Pair `Pips` with `NavigationButtonVisibility` for its previous / next buttons.

## NumericEntryDemoPage.Button texts

Texts on the stepper buttons instead of the minus and plus glyphs (`DecrementText` / `IncrementText`).

## PickerDemoPage.Field shows a short text (SelectedItemDisplayBinding)

The field shows a short form of the chosen item (a currency's code) while the list shows whole names
(`SelectedItemDisplayBinding`, a binding on the item, as MAUI's `ItemDisplayBinding`).

## PickerDemoPage.Items 2 and 4 cannot be chosen (ItemEnabledBinding)

`ItemEnabledBinding`: a binding on each item that tells whether it can be chosen. Disabled items are dimmed in the list
and skipped by the keyboard and the wheels.

## PickerDemoPage.PopupHeaderTemplate / PopupFooterTemplate

Views above and below the list in the popup (`PopupHeaderTemplate`, `PopupFooterTemplate`): a hint, a search field,
a total.

## PickerMultipleDemoPage.Spider Monkey cannot be chosen (ItemEnabledBinding)

`ItemEnabledBinding`: a binding on each item that tells whether it can be chosen. A disabled item is dimmed, cannot be
checked, and is left out by *Select all*.

## MultiColumnPickerDemoPage.Columns loop

Each column's wheel wraps around from its last item to its first (the columns' `Loop`).

## MultiColumnPickerDemoPage.Column 1 Width

A fixed width for the first column (its `Width`); -1 sizes it as the other columns.

## MultiColumnPickerDemoPage.Column headers

A header text above each column's wheel (the columns' `Header`).

## DatePickerDemoPage.Range (MinimumDate / MaximumDate)

The dates that can be chosen, from `MinimumDate` to `MaximumDate`: MAUI's default range (1900 to 2100), this year, or today
and the next 90 days. The wheels and the field stay inside the range.

## DatePickerDemoPage.Days that can be chosen

Days left out inside the range: every day can be chosen, weekdays only (`DateEnabled`, a function of the date), or every
day but the 1st and 15th of each month (`BlackoutDates`, a list). Days left out are dimmed on the wheels.

## TimePickerDemoPage.Range (MinimumTime / MaximumTime)

The times that can be chosen, from `MinimumTime` to `MaximumTime`: the whole day, or opening hours (9:00 to 17:30).

## DateTimePickerDemoPage.Range (MinimumDate / MaximumDate)

The dates and times that can be chosen: the default range, or from the next quarter hour to two weeks later.

## DateTimePickerDemoPage.Weekdays only (DateEnabled)

`DateEnabled`: a function of the date that tells whether it can be chosen; here Saturdays and Sundays cannot.

## RadioButtonDemoPage.Content

What each radio button shows: a string (drawn as the button's text) or a view (drawn as its content).

## RadioButtonTemplateDemoPage.Content

What each radio button shows inside its `ControlTemplate`: a string, or a view placed by the template's
`ContentPresenter`.

## ContentViewDemoPage.HasContent

Sets the content view's `Content`, or clears it (an empty content view draws only its background and padding).

## ContentViewDemoPage.Title

The page model's title, bound to the heading inside the content view (`BindingContext` flows to the content).

## ContentViewDemoPage.WhenShown

The tab sections' `ContentLoading`: `WhenShown` creates a section's content from its `ContentTemplate` only when the
section is first shown (switch tabs to see the counts); off, every section is created at once (`Immediate`).

## ContentViewDemoPage.DelayMs

The sections' `ContentLoadingDelay`: how long a shown section waits before it creates its content, so switching tabs
quickly does not build every tab on the way.

## ContentViewDemoPage.FadeIn

The sections' `ContentLoadedAnimation`: content created when shown fades in instead of popping in.

## AlternateContentViewDemoPage.Animation

How the view switches between its content and the alternate one: at once, fading, or sliding
(`BeforeStateChangeAnimation` on the outgoing view, `AfterStateChangeAnimation` on the incoming one).

## AlternateContentViewDemoPage.Name

The bound model's name, shown by both the content and the alternate content (they share the `BindingContext`).

## ShimmerDemoPage.Switch animation

How the skeleton turns into the content when `IsLoading` goes off: at once, fading the skeleton out, or a cross-fade
(`BeforeStateChangeAnimation`, `AfterStateChangeAnimation`).

## CoreShimmerDemoPage.Switch animation

How the skeleton turns into the content when `IsLoading` goes off: at once, or fading the skeleton out
(`BeforeStateChangeAnimation`).

## ShimmerContentDemoPage.Empty texts while loading

Clears the names and messages, as a feed whose data has not arrived yet: empty labels still draw placeholder lines (one
per name, two per message), so the skeleton keeps the layout of the loaded feed.

## ExpanderNestingDemoPage.All expanders

Expands or collapses every expander on the page at once, outer and nested.

## SwipeItemViewDemoPage.Reply can execute

Whether the Reply item's `Command` can execute; when it cannot, the item is dimmed and ignores taps.

## SwipeViewListDemoPage.Delete Mode

The `Mode` of the rows' delete items: **Reveal** shows the item to tap; **Execute** deletes the row once swiped past the
threshold.

## BorderDemoPage.Stroke gradient

A linear gradient brush as the border's `Stroke` instead of a solid color.

## LayoutDemoPage.BoxOnTop

Raises the box's `ZIndex` above the ellipse: it is drawn over the ellipse and takes taps where they overlap.

## LayoutDemoPage.EllipseOpacity

The ellipse's `Opacity`: it fades, and taps still reach it while it is not transparent to input.

## LayoutDemoPage.EllipseInputTransparent

The ellipse's `InputTransparent`: taps on it go through to the box below.

## GridDemoPage.FirstColumn

The first column's `Width` (`ColumnDefinitions[0]`): `Auto` (as wide as its content), `*` (a share of the room) or a fixed
64.

## GridDemoPage.SpanLastCell

The last cell's `Grid.ColumnSpan`: across both columns, or one.

## GridDemoPage.FirstCellVisible

The first cell's `IsVisible`: a hidden child takes no room, and `Auto` rows and columns shrink to the others.

## CoreGridDemoPage.FirstColumn

The first column's `Width`: `Auto` (as wide as its content), `*` (a share of the room) or a fixed 64.

## CoreGridDemoPage.SpanLastCell

The last cell's column span: across both columns, or one.

## CoreGridDemoPage.FirstCellVisible

The first cell's `IsVisible`: a hidden child takes no room, and `Auto` rows and columns shrink to the others.

## GridSplitterDemoPage.Splitter background

Both splitters' `Background`: the page's fill, or none (the panes show through around the grip).

## GridSplitterDemoPage.Thin splitters (1 DIP)

Both splitters 1 DIP thick (`WidthRequest` / `HeightRequest`): the panes meet with no gap. The touch area keeps the
look's `DraggerTouchThickness` (or `TouchThickness`), centered on the splitter and over the panes, and the grip draws
over them; the splitters are added after the panes so they are on top.

## GridSplitterDemoPage.Left column

The left column's `Width`. Between two star columns a drag changes their star weights, so they keep sharing a resized
page; next to an `Auto` or fixed column it sets that column's width in DIPs and the star column takes the rest.

## GridSplitterDemoPage.Left column MinimumLength

`SkUiGridSplitter.MinimumLength` on the left column's definition: the narrowest a drag leaves it. MAUI's definitions
have no minimum of their own, so it limits the splitter only, not the grid's layout.

## GridSplitterDemoPage.Top row MaximumLength

`SkUiGridSplitter.MaximumLength` on the top row's definition: the tallest a drag makes it (at the slider's end, no limit).

## CoreGridSplitterDemoPage.Thin splitters (1 DIP)

The splitters 1 DIP wide, on the column separators, or the look's 16 DIPs inside the end of each header cell. A thin
splitter's touch area and grip reach over the cells on both sides.

## CoreGridSplitterDemoPage.Splitters down the whole table

Each column splitter spans every row (`RowSpan`), shows its line (`ShowsLine`) and keeps its grip in the header
(`GripAlignment` `Start`); the table's column separators are hidden, as the splitters' lines take their place. A splitter
merges no cells: the table still draws its row separators across it.

## StateContainerDemoPage.Animation

How the container switches between its content and a state view: at once, fading, or sliding (the state change
animations).

## StateContainerDemoPage.CurrentState

The attached `CurrentState` (as the Community Toolkit's StateContainer): the name of the state view to show instead of the
content (*Loading*, *Error*, *Empty*), or none for the content.

## WrapLayoutDemoPage.TallFirstChip

Makes the first chip taller (`HeightRequest = 44`): its row grows, and the other chips are placed in it by their
`VerticalOptions`.

## WrapLayoutDemoPage.ChipsVerticalOptions

The other chips' `VerticalOptions`: where they sit in a row taller than they are (turn on *TallFirstChip*).

## WrapLayoutDemoPage.Chip3.Text

The third chip's text: as it grows or shrinks, its row re-wraps and later chips move between rows.

## WrapLayoutDemoPage.Chip3.Width

The third chip's `WidthRequest`; 0 for its natural width.

## CoreWrapLayoutDemoPage.FirstChipVisible

The first chip's `IsVisible`: a hidden chip takes no room, and the others move up.

## CoreWrapLayoutDemoPage.Chip3.Text

The third chip's text: as it grows or shrinks, its row re-wraps and later chips move between rows.

## CoreWrapLayoutDemoPage.Chip3.Width

The third chip's `Width`; 0 for its natural width.

## CoreWrapLayoutDemoPage.Chip3.Height

The third chip's `Height`; 0 for its natural height.

## HorizontalShrinkLayoutDemoPage.Layout

Read only: how much of the gray preview area the layout (white) takes, and whether it lays out as a plain stack (its
children fit) or shrinks its shrinkable children.

## VerticalShrinkLayoutDemoPage.Layout

Read only: how much of the gray preview area the layout (white) takes, and whether it lays out as a plain stack (its
children fit) or shrinks its shrinkable children.

## CoreHorizontalShrinkLayoutDemoPage.Layout

Read only: how much of the gray preview area the layout (white) takes, and whether it lays out as a plain stack (its
children fit) or shrinks its shrinkable children.

## CoreVerticalShrinkLayoutDemoPage.Layout

Read only: how much of the gray preview area the layout (white) takes, and whether it lays out as a plain stack (its
children fit) or shrinks its shrinkable children.

## HorizontalShrinkLayoutDemoPage.Label2.Text

The second label's text: edit it to watch the row switch between a plain stack (it fits) and shrinking (too long).

## CoreHorizontalShrinkLayoutDemoPage.Label2.Text

The second label's text: edit it to watch the row switch between a plain stack (it fits) and shrinking (too long).

## PathDemoPage.RenderTransform rotation

Rotates both paths with a `RotateTransform` as their `RenderTransform`: the geometry turns around (60, 50) before it is
stretched and stroked.

## ScrollViewDemoPage.ContentHeight

The `HeightRequest` of the scrolled content (not of the scroll view): taller content scrolls further.

## ScrollViewDemoPage.ContentWidth

The `WidthRequest` of the scrolled content (not of the scroll view): wider content scrolls sideways with a horizontal
`Orientation`.

## ScrollViewDemoPage.Thumb color

The scroll bars' thumb color (`VerticalScrollBar.ThumbColor`, `HorizontalScrollBar.ThumbColor`): the look's, or the accent.

## ScrollViewDemoPage.ScrollToPosition

Where *Scroll to row 8* puts the row (`ScrollToAsync(element, position, …)`): `MakeVisible` scrolls only as far as
needed; `Start`, `Center` and `End` align it in the viewport.

## VirtualScrollViewDemoPage.ScrollToPosition

Where *Scroll to the middle item* puts the item: `MakeVisible` scrolls only as far as needed; `Start`, `Center` and `End`
align it in the viewport.

## VirtualScrollViewDemoPage.Variable heights

Rows of four different heights instead of one: positions past the realized rows are estimated until they are measured.

## VirtualScrollViewDemoPage.ItemExtent = 56

SkiaUi only: `ItemExtent` declares every row 56 tall, so positions are known without measuring rows (the fastest scrolling
and scrolling to an index).

## CollectionViewDemoPage.No items (empty view)

Clears the items: both lists show their `EmptyView`.

## CollectionViewDemoPage.Load more near the end

Appends a page of orders when the list nears its end (`RemainingItemsThresholdReached`); off, the list ends.

## CollectionViewLayoutsDemoPage.Layout

The `ItemsLayout`: a vertical list, a vertical grid of two or three columns (`GridItemsLayout.Span`), or a horizontal list.

## ItemsLayoutDemoPage.ItemsLayout

The one `ItemsLayout` object both lists get, as MAUI's XAML strings name them: a vertical or horizontal
`LinearItemsLayout`, or a `GridItemsLayout` of two columns (rows, horizontally). The snap point editors below set its
`SnapPointsType` and `SnapPointsAlignment`.

## ItemsLayoutDemoPage.Insert 3 at the start

Inserts three cards before the first: with `KeepItemsInView` what shows stays in place, with `KeepScrollOffset` it moves
down by three cards, with `KeepLastItemInView` both lists scroll to their end.

## ItemsLayoutDemoPage.Add one at the end

Appends a card: with `KeepLastItemInView` both lists scroll to it.

## ReorderDemoPage.CanReorderItems

Lets the user move tasks: on a phone a long press lifts one, with a mouse a press and drag. The other tasks move aside as it
passes them, and the list scrolls when it is held near an end. Both lists show the same tasks, so a move in one shows in
the other. Screen readers offer "Move up" and "Move down" on each task.

## ReorderDemoPage.ItemsLayout

A list, a grid of two columns, or a horizontal list: in a grid a lifted task moves freely and the others take their
neighbours' cells.

## ReorderDemoPage.IsGrouped

The same tasks in two groups, "Today" and "Later". Group headers do not move.

## ReorderDemoPage.CanMixGroups

In grouped lists, whether a task can move to the other group (off: it stays in its own).

## ReorderDemoPage.Reset the order

Puts the tasks back in their first order.

## CarouselViewDemoPage.Cover flow RotationAngle

How far the side items turn with the *Cover flow* `ItemEffect` (`SkUiCoverFlowEffect.RotationAngle`, degrees).

## CarouselViewDemoPage.Load more at the end (RemainingItemsThreshold = 2)

Appends items when the carousel is two items from its end (`RemainingItemsThreshold`, `RemainingItemsThresholdReached`):
an endless feed.

## RefreshViewDemoPage.Refresh takes (s)

How long the demo's refresh (the `Command`) runs before it sets `IsRefreshing` back off.

## ShimmerRowsDemoPage.Loading

`IsLoading` of every row's shimmer: the rows show their skeletons while it is on.

## VirtualVerticalStackLayoutDemoPage.Source

Where the items come from: a collection that grows a page at a time (`ItemsSource`, `RemainingItemsThreshold`), or an
endless factory that makes the item for an index (`SetItemFactory`).

## CoreTableDemoPage.HeaderRowBackground

The first row's background (`SetRowBackground(0, …)`), drawn behind its cells across the whole row.

## CoreTableDemoPage.ShowColumnTint

The first column's background (`SetColumnBackground(0, …)`); where a row background crosses it, `TrackBackgroundOrder`
decides which is on top.

## CorePressEffectDemoPage.Details can execute

Whether the Details button's command can execute. When it cannot, the button is disabled, and the card around it is not
pressed by taps on it either.

## CorePressEffectDemoPage.Fade on press

Sets `PressFeedback` of every card, chip and tile to `Fade`: instead of the look's dim or ripple, a pressed control and its
children fade to the look's `PressedContentOpacity` (0.4), composited without re-recording. The dark remove disc always
fades: a dark tint would hardly show on it.

## OverlayScrollingDemoPage.Check native placement

Compares every native view with where the drawn tree places it, also with the soft keyboard up, and reports the views
that are off.
