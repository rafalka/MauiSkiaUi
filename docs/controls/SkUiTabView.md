# SkUiTabView

> **Status: built for 1.17** (steps S7: `SkUiTabView`, and S8: `SkUiCoreTabView`); custom headers built for 1.18 (phase T, [decisions](#custom-header-decisions-118-t1)). The [review's decisions](#review-decisions) are at the end.

Tabbed content inside a page: a strip of tab headers and, below (or above) it, one pane per tab, swiped sideways or chosen by a tap on its header. Settings sections, a product's "Details · Reviews · Shipping", a profile's "Posts · Media · Likes".

**MAUI counterpart:** none inside a page. MAUI's tabs are page-level: `TabbedPage` and Shell's `TabBar` / `Tab`, each tab a page with its own navigation. Apps that need tabs inside a page build them from buttons over a `CarouselView` or a `Grid` of views toggled by `IsVisible`, or use a third-party control (Syncfusion `SfTabView`, the Xamarin Community Toolkit's `TabView`, which has no MAUI port). `SkUiTabView` is that control, drawn. Keep `TabbedPage` / Shell tabs for app-level navigation; use the tab view for sections of one page.

## How it works

- **Tabs.** `Tabs` (the XAML content property) holds `SkUiTabItem`s (each once; to reorder, remove and insert): `Header` (text), `Icon` (an `ImageSource`), `BadgeText`, `IsEnabled`, and the pane: `Content` (a drawn view) or `ContentTemplate` (a `DataTemplate`, made once). Native views go inside an [`SkUiMauiContentView`](SkUiMauiContentView.md) in the pane, as anywhere on a surface. A tab item takes the tab view's binding context (or has its own), and its pane binds to the tab item's.
- **Panes are kept.** A pane is created when it first comes into view (selected, or a drag or fling reaching it), not before, and is kept after that: its scroll offset, the text typed into its fields and its own state survive going out of view and back. `ContentTemplate` panes are made once, when first shown. Created panes stay laid out at their places in the pane scroller, so a pane out of view is simply scrolled out: it is clipped away, leaves the semantics tree, and its native views hide, as in any scroll view. (The tab view does not recycle panes, unlike a carousel's item views: a pane is a section of the page, not an item.)
- **Selection.** `SelectedIndex` (two-way, default 0; kept as set while there are no tabs) is the one source of truth: a tap on a header, a swipe and a key all go through it, and setting it scrolls the panes there, animated as the scroll view's `ScrollToAsync` (at once under reduce motion or before the tab view is shown). A swipe selects the tab it comes to rest on. Values outside the tabs are coerced before they are written back (`WriteBack`), so a one-way binding is not cut. `SelectionChanged` (`SkUiSelectedIndexChangedEventArgs`: `PreviousIndex`, `CurrentIndex`) is raised for every change. When tabs are added or removed (also while the panes move), the selected tab stays selected where it moved; when it was removed, the tab in its place is selected. An index set before the tabs waits for them; one beyond the tabs there are becomes the last.
- **Swiping.** The panes scroll sideways as a strip with no looping, one tab per swipe, bouncing at the first and last (per the look's overscroll), on the render thread like the carousel (they share its layout engine). `IsSwipeEnabled="False"` leaves taps and keys only. A horizontal scroller inside a pane (a carousel, a horizontal list) gets the drags of its own axis first and hands the drag over at its ends, as nested scrollers do. A vertical scroller in a pane works as usual: the gesture arena tells the axes apart.
- **Disabled tabs.** A tab with `IsEnabled="False"` shows a dimmed header, does not take taps, and is skipped by swipes and keys (a swipe from the tab before it lands on the tab after it). The app can still select it by code.
- **Strip.** `TabStripPlacement="Top"` (default) or `"Bottom"`. `TabWidthMode="Equal"` (default) splits the strip's width equally; `"Content"` sizes each header to its content and makes the strip scroll sideways when the headers do not fit, keeping the selected header in view (scrolled to it, animated, when the selection changes). A header shows its icon at the start of its text (or the icon alone); a badge (`BadgeText`) sits at the header's top end: a number or a short text in a small pill (scaled with the system text size), an empty string a dot, `null` none. The strip is drawn by the look on the segmented control's engine: headers are not views. Text larger than the look's strip makes it taller.
- **Custom headers (1.18).** `HeaderTemplate` on the tab view (every tab; a `DataTemplateSelector` chooses by the tab item) or on a tab item (that tab, over the strip's) makes a drawn view of the app's for the header, made once per tab, in place of the look's text, icon and badge. [Below](#custom-headers).
- **Indicator.** `IndicatorStyle="Underline"` (default) draws a bar under the selected header, `"Pill"` a pill behind it, `"None"` nothing (the selected header's text color still marks it). While the panes are dragged or flung the indicator moves with them, on the render thread through a scroll link to the pane scroller: no UI-thread work per frame, also when the UI thread is busy. With content widths it also changes width between neighbors on the way. The look decides its width: the header's width, or the label's (Material 3).
- **Colors and text.** `HeaderTextColor`, `SelectedHeaderTextColor`, `IndicatorColor` and `StripBackgroundColor`, each `null` for the look's. Header font members (`FontFamily`, `FontSize`, `FontAttributes`, `FontAutoScalingEnabled`) as `ISkUiFontElement`.
- **Keyboard.** The strip is one tab stop: Left / Right select the previous / next enabled tab (mirrored right to left), Home / End the first / last; Space and Enter change nothing (the selected tab is the focused one). Tab moves on into the selected pane only; a control focused by code in another pane selects its tab, and focus inside a pane that is no longer selected goes to the strip. In a pane, the arrow keys, Home and End never move the panes (Page Up / Page Down go tab by tab when nothing inside the pane scrolls). Ctrl+Tab / Ctrl+Shift+Tab select the next / previous tab from anywhere inside the tab view (desktop convention).
- **Screen readers.** The strip is one element whose value is the selected tab, stepped by increment / decrement, and each header an element inside it, read with its text (an icon alone: the tab's `SemanticProperties.Description`), its badge, "Selected" and its place ("Reviews, 3, Selected, 2 of 3"); there is no tab role of the platforms' own yet, the headers are read as buttons. Activating a tab selects it. The panes' scroll container is named by the selected tab, and its contents are read as usual (a name on the pane itself would stand for its texts and hide them); its page actions (TalkBack's and VoiceOver's scroll gestures) go tab by tab, past disabled ones. The strip and the panes are read in their order on screen.
- **Right to left.** Headers and panes run from the right; the first tab is at the right end, and swiping right goes to the next tab.
- **Performance.** Panes are created only when reached, so tabs that are never opened cost nothing (as `ContentLoading="WhenShown"` on a [content view](SkUiContentView.md)). Swiping and the indicator run on the render thread and re-record nothing; the strip re-records only when the selection settles or a header changes (the selected header's color switches then, not while the panes move). A fling allocates nothing beyond the scroll view's own snapping.

## Custom headers

`SkUiTabView.HeaderTemplate` gives every tab a header view of the app's, `SkUiTabItem.HeaderTemplate` one tab (it wins over the strip's); a template that is a `DataTemplateSelector` chooses by the tab item (it is asked again whenever the strip changes, so it returns the same template instances, as MAUI's selectors for collection views do: a new instance makes the view again). Without either, the look draws the header as before, so a strip can mix both.

- **Binding context.** A header view's binding context is its tab item: `{Binding Header}`, `{Binding Icon}`, `{Binding BadgeText}`, and two read-only properties the tab view keeps up to date: `IsSelected` and `CurrentTextColor`, the color the look gives this tab's text now (selected, unselected or dimmed when disabled, with `HeaderTextColor` / `SelectedHeaderTextColor` over the look's). Bind a label's `TextColor` to it to match the drawn headers. The tab's view model is `{Binding BindingContext.X}` (with `x:DataType="sk:SkUiTabItem"`, cast it).
- **States.** The view's root is in the `Selected` state of `CommonStates` while its tab is selected (as a selected collection item's root), and `Disabled` while the tab is disabled (the view is disabled with it). The state changes when the selection does (a tap at once, a swipe when the panes come to rest), not while the panes move.
- **Size.** A header view fills its header's rectangle, laid out by its own `HorizontalOptions` / `VerticalOptions` (MAUI's rules: `Fill` by default, `Center` to center it). With `TabWidthMode="Content"` the header is as wide as the view measures, with no look padding added (the view brings its own); with `Equal` the headers share the strip as before, and the strip asks for the widest. The strip is as tall as the tallest header view when that is taller than the look's height, each measured at its header's width (a label that wraps in an equal share makes the strip taller). A view whose size changes (a bound text) lays the strip out again.
- **What the look keeps.** The strip's background and divider, and the indicator, which sits under the header views (a pill behind them). Where the look sizes the indicator to the label (Material 3's underline), it takes the view's measured width within the header. The look draws no text, icon or badge for a tab with a header view: a view that wants the icon or the badge binds to them. An icon a header view shows stays the tab's: bindings inside it (a glyph, a color) resolve against the tab's binding context.
- **Input and screen readers.** A header view takes no input, keyboard focus or screen reader of its own: a tap anywhere on it selects its tab (a button inside does not take it), the strip stays one tab stop, and screen readers read the tab's `Header` (an icon or a view alone: the tab's `SemanticProperties.Description`) as one element of the tab list, as before. Set `Header` even when the template shows something else.
- **Performance.** Header views are drawn views on the strip's surface: each records its own content and re-records only when it changes (a bound property, its visual state). Swiping and the indicator stay on the render thread.

```xml
<sk:SkUiTabView TabWidthMode="Content">
    <sk:SkUiTabView.HeaderTemplate>
        <DataTemplate x:DataType="sk:SkUiTabItem">
            <sk:SkUiVerticalStackLayout Padding="12,6" Spacing="2" VerticalOptions="Center">
                <sk:SkUiLabel Text="{Binding Header}" TextColor="{Binding CurrentTextColor}" FontAttributes="Bold" HorizontalTextAlignment="Center" />
                <sk:SkUiLabel Text="{Binding BadgeText, StringFormat='{0} new', TargetNullValue='none new'}" FontSize="11"
                              TextColor="{Binding CurrentTextColor}" HorizontalTextAlignment="Center" />
            </sk:SkUiVerticalStackLayout>
        </DataTemplate>
    </sk:SkUiTabView.HeaderTemplate>
    <sk:SkUiTabItem Header="Inbox" BadgeText="{Binding Unread}">…</sk:SkUiTabItem>
    <sk:SkUiTabItem Header="Archive">…</sk:SkUiTabItem>
</sk:SkUiTabView>
```

**Core.** `SkUiCoreTabItem.HeaderContent` is one tab's header node (a node without a parent); `SkUiCoreTabView.HeaderFactory` (`Func<SkUiCoreTabItem, SkUiCoreNode?>`) makes one for every other tab, called once per tab (again when the factory is replaced); returning `null` keeps the look's header for that tab. `IsSelected` and `CurrentTextColor` raise `PropertyChanged` on the tab item; Core nodes have no visual states, so a header node follows them from there. The header node is disabled with its tab.

```csharp
tabs.SetHeaderFactory(tab =>
{
    var title = new SkUiCoreLabel().SetText(tab.Header).SetFontAttributes(FontAttributes.Bold);
    if (tab.CurrentTextColor is { } now)
        title.SetTextColor(now); // a factory set later finds the color already there: no change is raised for it
    tab.PropertyChanged += (_, args) =>
    {
        if (args.PropertyName == nameof(SkUiCoreTabItem.CurrentTextColor) && tab.CurrentTextColor is { } color)
            title.SetTextColor(color);
    };
    return title;
});
```

The handler keeps the node for as long as the tab lives; when the factory is replaced or the tab view goes while the tabs stay, unsubscribe (keep the handler with the node) or use a weak subscription.

## Looks

| Look | Strip | Indicator | Height (text / icon and text) |
| --- | --- | --- | --- |
| Default | Plain, a hairline under it | Underline in the accent color, header width | 44 / 56 |
| iOS 26 | Plain | Underline, header width (`Pill` suits iOS's segmented look) | 44 / 56 |
| Material 3 | Surface, a divider under it | Underline as wide as the label, rounded ends; the icon above the text | 48 / 64 |
| Material 2 | Primary color, white headers | Full-width underline | 48 / 72 |
| Mac Catalyst | Plain | Underline, header width | 32 / 48 |
| Windows | Plain | A short accent bar under the label (Fluent) | 40 / 52 |

## How to use

```xml
<sk:SkUiTabView SelectedIndex="{Binding Section}">
    <sk:SkUiTabItem Header="Details">
        <sk:SkUiScrollView>
            <sk:SkUiLabel Text="{Binding Description}" Padding="16" />
        </sk:SkUiScrollView>
    </sk:SkUiTabItem>
    <sk:SkUiTabItem Header="Reviews" BadgeText="{Binding ReviewCount}">
        <sk:SkUiTabItem.ContentTemplate>
            <DataTemplate>
                <sk:SkUiCollectionView ItemsSource="{Binding Reviews}" />
            </DataTemplate>
        </sk:SkUiTabItem.ContentTemplate>
    </sk:SkUiTabItem>
    <sk:SkUiTabItem Header="Shipping" IsEnabled="{Binding CanShip}">
        <local:ShippingView />
    </sk:SkUiTabItem>
</sk:SkUiTabView>

<!-- Many sections: headers sized to their text in a scrolling strip, a pill indicator, the strip at the bottom. -->
<sk:SkUiTabView TabWidthMode="Content" IndicatorStyle="Pill" TabStripPlacement="Bottom">
    <sk:SkUiTabItem Header="January">…</sk:SkUiTabItem>
    <sk:SkUiTabItem Header="February">…</sk:SkUiTabItem>
</sk:SkUiTabView>
```

```csharp
var tabs = new SkUiTabView()
    .AddTab(new SkUiTabItem { Header = "Posts", Content = postsList })
    .AddTab(new SkUiTabItem { Header = "Media", ContentTemplate = new DataTemplate(() => new MediaGrid()) });
tabs.SelectionChanged += (_, args) => Analytics.Track(args.CurrentIndex);
```

## Core

`SkUiCoreTabView` is the tab view on Core nodes, on the same strip and the same layout engine, drawn the same pixel for pixel. Its tabs are `SkUiCoreTabItem`s (plain objects with change notification): `Header`, `Icon`, `BadgeText`, `IsEnabled`, `AutomationId`, and the pane as a `Content` node or a `ContentFactory` (`Func<SkUiCoreNode>`, called once, when the pane is first shown). The properties are the ones [below](#key-properties) with `Set*` methods, and selection, swiping, disabled tabs, keys and screen readers work the same.

```csharp
var tabs = new SkUiCoreTabView()
    .AddTab(new SkUiCoreTabItem("Posts", postsScroller))
    .AddTab(new SkUiCoreTabItem("Media", () => new SkUiCoreLabel().SetText("Made when first shown")).SetBadgeText("3"))
    .SetIndicatorStyle(SkUiTabIndicatorStyle.Pill);
tabs.SelectionChanged += (_, args) => Analytics.Track(args.CurrentIndex);
// A Core host that is the surface itself draws in software unless HwAccelerated is set; inside a drawn view it uses its surface.
page.Content = new SkUiCoreHost { HwAccelerated = true }.SetContent(tabs);
```

## Key properties

| Property | Default | Notes |
| --- | --- | --- |
| `Tabs` | empty | Content property; `SkUiTabItem`: `Header`, `Icon`, `BadgeText`, `Content` or `ContentTemplate`, `IsEnabled`, `HeaderTemplate`; read-only `IsSelected`, `CurrentTextColor` |
| `HeaderTemplate` | `null` | Header views for every tab without its own ([custom headers](#custom-headers)); Core: `HeaderFactory`, and `SkUiCoreTabItem.HeaderContent` |
| `SelectedIndex` | `0` | Two-way; the one source of truth |
| `TabStripPlacement` | `Top` | `Top` or `Bottom` |
| `TabWidthMode` | `Equal` | `Equal`, or `Content` (the strip scrolls) |
| `IndicatorStyle` | `Underline` | `Underline`, `Pill` or `None` |
| `IsSwipeEnabled` | `true` | Taps and keys always select |
| `HeaderTextColor`, `SelectedHeaderTextColor`, `IndicatorColor`, `StripBackgroundColor` | `null` | The look's |
| `FontFamily`, `FontSize`, `FontAttributes`, `FontAutoScalingEnabled` | | Headers; `ISkUiFontElement` |
| `SelectionChanged` | | `SkUiSelectedIndexChangedEventArgs`, every change |

## Related

[`SkUiSegmentedControl`](SkUiSegmentedControl.md) (the strip's engine) · [`SkUiCarouselView`](SkUiCarouselView.md) (the panes' layout engine) · [`SkUiContentView`](SkUiContentView.md) (`ContentLoading="WhenShown"`)

## Review decisions

Reviewed and accepted on 24 November 2026, as proposed:

1. **Members beyond the plan's table** are kept: `HeaderTextColor`, `SelectedHeaderTextColor`, `IndicatorColor`, `StripBackgroundColor`, the header font members, `AddTab`, and Ctrl+Tab / Ctrl+Shift+Tab.
2. **Tabs** are a list of `SkUiTabItem`s (bindable); no `ItemsSource` with templates in 1.17.
3. **Panes are kept** once created (D3); no option to release them.
4. **Icons** sit at the start of the header text, except on Material 3, which stacks the icon above the text.
5. **Swiping onto a disabled tab** skips it: the swipe lands on the next enabled tab.

## Custom header decisions (1.18 T1)

Decided on 27 November 2026 for the roadmap's open questions:

1. **Shape.** A `DataTemplate` (a view per tab, made once) on the tab view for the whole strip and on the tab item for one tab, the tab's winning; a `DataTemplateSelector` chooses by the tab. No view property on the tab item: a template keeps one way for both and a binding context that is always the tab item. Core: a node on the tab item and a factory on the tab view.
2. **Binding context and states.** The tab item, with read-only `IsSelected` and `CurrentTextColor` on it (both layers); the view's root in the `Selected` / `Disabled` visual states (SkUi). The state follows `SelectedIndex`, so it changes when the selection does, not while the panes move.
3. **Measuring.** Content widths: the view's measured width, no look padding. Equal widths: the engine's equal share, the widest asked for unbounded. The strip's height: at least the look's, the tallest view's when taller, each measured at its header's width once the headers are laid out (unbounded before). A selector is asked again when the strip changes and returns cached templates.
4. **What the look keeps.** The strip and the indicator; the look draws nothing else for a tab with a header view. The look's color for the tab's state is offered as `CurrentTextColor`, not applied to the view.
5. **Indicator.** Laid out on the header's rectangle as for drawn headers; the label width a look may use is the view's measured width, clamped to the header. The scroll-linked channels are unchanged (they read the indicator bounds).
6. **Semantics and input.** The header stays one element of the tab list, read from `Header` (or the tab's description); the view is excluded from the semantics tree with its subtree, takes no pointer input (taps go to the strip) and no keyboard focus.
