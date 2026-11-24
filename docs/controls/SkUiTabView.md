# SkUiTabView

> **Status: design, reviewed 24 November 2026 (1.17 step S1).** Nothing on this page ships yet. It is the contract for steps S7 and S8 of [ImplementationPlan-1.17.md](../design/ImplementationPlan-1.17.md#s7-tab-view) (decisions D3 and D8 there); the [review's decisions](#review-decisions) are at the end.

Tabbed content inside a page: a strip of tab headers and, below (or above) it, one pane per tab, swiped sideways or chosen by a tap on its header. Settings sections, a product's "Details · Reviews · Shipping", a profile's "Posts · Media · Likes".

**MAUI counterpart:** none inside a page. MAUI's tabs are page-level: `TabbedPage` and Shell's `TabBar` / `Tab`, each tab a page with its own navigation. Apps that need tabs inside a page build them from buttons over a `CarouselView` or a `Grid` of views toggled by `IsVisible`, or use a third-party control (Syncfusion `SfTabView`, the Xamarin Community Toolkit's `TabView`, which has no MAUI port). `SkUiTabView` is that control, drawn. Keep `TabbedPage` / Shell tabs for app-level navigation; use the tab view for sections of one page.

## How it works

- **Tabs.** `Tabs` (the XAML content property) holds `SkUiTabItem`s: `Header` (text), `Icon` (an `ImageSource`), `BadgeText`, `IsEnabled`, and the pane: `Content` (a drawn view) or `ContentTemplate` (a `DataTemplate`, made once). Native views go inside an [`SkUiMauiContentView`](SkUiMauiContentView.md) in the pane, as anywhere on a surface. A tab item takes the tab view's binding context, so headers and panes can bind.
- **Panes are kept.** A pane is created when it first comes into view (selected, or a drag or fling reaching it), not before, and is kept after that: its scroll offset, the text typed into its fields and its own state survive going out of view and back. `ContentTemplate` panes are made once, when first shown. Panes out of view are neither measured, arranged nor drawn, and leave the semantics tree. (The tab view does not recycle panes, unlike a carousel's item views: a pane is a section of the page, not an item.)
- **Selection.** `SelectedIndex` (two-way, default 0; -1 only without tabs) is the one source of truth: a tap on a header, a swipe and a key all go through it, and setting it scrolls the panes there with the look's transition (at once under reduce motion). Values outside the tabs are coerced before they are written back (`WriteBack`), so a one-way binding is not cut. `SelectionChanged` (`SkUiSelectedIndexChangedEventArgs`: `PreviousIndex`, `CurrentIndex`) is raised for every change. When tabs are added or removed, the selected tab stays selected where it moved; when it was removed, the index stays within the tabs.
- **Swiping.** The panes scroll sideways as a strip with no looping, one tab per swipe, bouncing at the first and last (per the look's overscroll), on the render thread like the carousel (they share its layout engine). `IsSwipeEnabled="False"` leaves taps and keys only. A horizontal scroller inside a pane (a carousel, a horizontal list) gets the drags of its own axis first and hands the drag over at its ends, as nested scrollers do. A vertical scroller in a pane works as usual: the gesture arena tells the axes apart.
- **Disabled tabs.** A tab with `IsEnabled="False"` shows a dimmed header, does not take taps, and is skipped by swipes and keys (a swipe from the tab before it lands on the tab after it). The app can still select it by code.
- **Strip.** `TabStripPlacement="Top"` (default) or `"Bottom"`. `TabWidthMode="Equal"` (default) splits the strip's width equally; `"Content"` sizes each header to its content and makes the strip scroll sideways when the headers do not fit, keeping the selected header in view (scrolled to it, animated, when the selection changes). A header shows its icon at the start of its text (or the icon alone); a badge (`BadgeText`) sits at the header's top end: a number or a short text in a small pill, an empty string a dot, `null` none. The strip is drawn by the look on the segmented control's engine: headers are not views.
- **Indicator.** `IndicatorStyle="Underline"` (default) draws a bar under the selected header, `"Pill"` a pill behind it, `"None"` nothing (the selected header's text color still marks it). While the panes are dragged or flung the indicator moves with them, on the render thread through a scroll link to the pane scroller: no UI-thread work per frame, also when the UI thread is busy. With content widths it also changes width between neighbors on the way. The look decides its width: the header's width, or the label's (Material 3).
- **Colors and text.** `HeaderTextColor`, `SelectedHeaderTextColor`, `IndicatorColor` and `StripBackgroundColor`, each `null` for the look's. Header font members (`FontFamily`, `FontSize`, `FontAttributes`, `FontAutoScalingEnabled`) as `ISkUiFontElement`.
- **Keyboard.** The strip is one tab stop: Left / Right select the previous / next enabled tab (mirrored right to left), Home / End the first / last; Tab moves on into the selected pane. Ctrl+Tab / Ctrl+Shift+Tab select the next / previous tab from anywhere inside the tab view (desktop convention).
- **Screen readers.** The strip is a tab list and each header a tab ("Reviews, tab, selected, 2 of 3", with the badge read after the header); the platforms' `TabList` / `TabItem` roles where they have them. Each pane is a container named by its tab's header. Activating a tab selects it.
- **Right to left.** Headers and panes run from the right; the first tab is at the right end, and swiping right goes to the next tab.
- **Performance.** Panes are created only when reached, so tabs that are never opened cost nothing (as `ContentLoading="WhenShown"` on a [content view](SkUiContentView.md)). Swiping and the indicator run on the render thread and re-record nothing; the strip re-records only when the selection settles or a header changes.

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
<sk:SkUiTabView TabWidthMode="Content" IndicatorStyle="Pill" TabStripPlacement="Bottom" Tabs="{Binding Sections}" />
```

```csharp
var tabs = new SkUiTabView()
    .AddTab(new SkUiTabItem { Header = "Posts", Content = postsList })
    .AddTab(new SkUiTabItem { Header = "Media", ContentTemplate = new DataTemplate(() => new MediaGrid()) });
tabs.SelectionChanged += (_, args) => Analytics.Track(args.CurrentIndex);
```

## Core

`SkUiCoreTabView` (1.17's cut line: it ships if the rest of 1.17 is done, otherwise it goes to the backlog) is the tab view on Core nodes, on the same engine and strip: `SkUiCoreTabItem` takes a `Content` node or a `ContentFactory` (`Func<SkUiCoreNode>`, called once, when the pane is first shown), with the same selection, swiping, keys and semantics.

## Key properties

| Property | Default | Notes |
| --- | --- | --- |
| `Tabs` | empty | Content property; `SkUiTabItem`: `Header`, `Icon`, `BadgeText`, `Content` or `ContentTemplate`, `IsEnabled` |
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
