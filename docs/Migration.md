# Migrating a MAUI app to SkiaUi

This guide is for developers moving existing .NET MAUI screens to SkiaUi's drawn controls. It covers where the drawn surface goes, how XAML converts, what has no drawn equivalent yet, and how gestures, styles and custom controls carry over.

> SkiaUi is a **preview**. Port one screen, measure it, and keep the rest native until you are happy with the result.

**Using an AI coding agent?** The [migration skills](../plugins/skiaui-migration/README.md) teach agents the same rules: an audit skill that finds the screens worth porting and their blockers, and a migration skill that converts a page.

## What changes and what stays

You replace a **subtree** of a page with drawn controls. Everything around it stays MAUI:

| Stays as it is | Changes |
| --- | --- |
| Pages, Shell, navigation, popups | The controls inside the drawn region: `Label` → `SkUiLabel`, `Grid` → `SkUiGrid`, … |
| View models, commands, `INotifyPropertyChanged` | Styles get a new `TargetType` (`sk:SkUiLabel`) |
| Bindings, `x:DataType`, converters, triggers, `VisualStateManager` | Gestures: MAUI's tap, swipe, pan, pinch and pointer recognizers keep working; drag and drop and `TouchBehavior` become gesture events or stay native |
| Resources, `StaticResource` / `DynamicResource`, `AppThemeBinding` | Native-only controls (WebView, maps, media, third-party controls) are wrapped in `SkUiMauiContentView` |
| Fonts from `ConfigureFonts`, `MauiImage` / `FontImageSource` images | Custom handlers, renderers and effects do not apply to drawn controls |
| `SemanticProperties`, `AutomationProperties`, `AutomationId` | |

The property names are MAUI's: each drawn control has its MAUI counterpart's API ([MAUI parity at a glance](#maui-parity-at-a-glance)), so most XAML converts by changing the element prefix.

## MAUI parity at a glance

Checked against .NET MAUI 10.0.120 (`MauiApiParityTests` compares every drawn control's public API with MAUI's). Controls marked SkUi\* + Core exist on both layers. The [control pages](controls/README.md) list each control's differences; [Not available yet](#not-available-yet) has the gaps.

| MAUI | SkiaUi | Status |
| --- | --- | --- |
| ActivityIndicator, ProgressBar | SkUi* + Core | Done (ProgressBar plus indeterminate) |
| CheckBox | SkUi* + Core | Done, plus three states |
| Slider | SkUi* + Core | Done, plus vertical; `ThumbImageSource` |
| Switch | SkUi* + Core | Done, plus three states (`On` / `Off` visual states) |
| RadioButton | SkUi* + Core | Done: `Content` (text or a drawn view), text properties, border chrome, `ControlTemplate` with `SkUiContentPresenter` (SkUi* only: the Core radio stays a circle to compose; `TemplateBinding` → `AncestorType`); group exclusion, `Value`, `RadioButtonGroup` |
| Label | SkUi* + Core | Done: text properties, spans and span taps, `TextType="Html"`, plus custom line breaking and tappable HTML links |
| Button | SkUi* + Core | Done: `ImageSource` + `ContentLayout`, text properties |
| Image, ImageButton | SkUi* + Core | Done, plus shared memory / disk cache, transformations, downsampling; runtime SVG files: planned |
| BoxView | SkUi* + Core | Done, gradient `Background` |
| Ellipse, Line, Rectangle, RoundRectangle, Path, Polygon, Polyline | SkUi* + Core | Done: brushes, stroke model, `Aspect`, geometries and path markup |
| Border | SkUi* + Core | Done: `StrokeShape`, brush stroke, dashes, stroke inset and shape clip; gradient `Background` |
| ScrollView | SkUi* + Core | Done: scroll bars (draggable on desktop, `SkUiCoreScrollBar`), overscroll (bounce, stretch), scroll to element, both wheel axes, plus snap points |
| Grid, VerticalStackLayout, HorizontalStackLayout, AbsoluteLayout, ContentView | SkUi* + Core | Done |
| FlexLayout | `SkUiFlexLayout` (SkUi* only) | Done |
| Toolkit Expander | `SkUiExpander` (SkUi* only) | Done, plus `ContentTemplate`, lazy content, animation |
| Toolkit StateContainer | `SkUiStateContainer` / `SkUiStateView` attached properties on every drawn layout (SkUi* only) | Done, plus animations before and after each state change |
| Toolkit AvatarView | `SkUiAvatarView` + Core | Done, plus initials of the text, colors by the text, a status badge |
| StackLayout | Stacks | Map |
| BindableLayout | Every `SkUiLayout` (grid, stacks, absolute, flex, wrap, shrink) | Done: MAUI's controller over `IBindableLayout`; templates and empty views must be drawn (a string `EmptyView` throws); no virtualization (use the collection view) |
| Every view: visual states | MAUI's states per control, `PointerOver` from hover, `Focused` from keyboard focus, state triggers | Done; `Selected` on collection items |
| Every view: `Shadow`, gradient `Background`, `Clip` | SkUi* + Core | Done (MAUI's `ImageBrush` is internal: an `SkUiImage` behind the content) |
| Every view: `SemanticProperties`, focus, font scaling | SkUi* + Core | Done: TalkBack, VoiceOver, Narrator; keyboard focus and ring; `FontAutoScalingEnabled`; plus `IsTabStop` / `TabIndex` (gone from MAUI) |
| Every view: `GestureRecognizers` | Arena gestures (`Tapped`, …) | Done: tap (any count, primary or secondary button), swipe, pan (one pointer), pinch and pointer recognizers run on drawn views; drag and drop is reported, not run |
| CollectionView | `SkUiCollectionView`; `SkUiVirtualScrollView` for plain lists | Done: MAUI's `ItemsLayout` (snap points included), `ItemSizingStrategy`, `ScrollTo`, `ItemsUpdatingScrollMode`, reordering (`CanReorderItems`, `CanMixGroups`, `ReorderCompleted`), templates and selectors, variable sizes, single and multiple selection, `Selected` state, item taps, header / footer (also sticky), empty view, groups (also collapsible, sticky headers), grids, horizontal, load more, `RemainingItemsThreshold`, scroll to index / item / group, pull-to-refresh |
| SwipeView | `SkUiSwipeView`, MAUI's `SwipeItems` / `SwipeItem`; `SkUiSwipeItemView` for `SwipeItemView` | Done; custom accessibility actions open |
| RefreshView | `SkUiRefreshView`; pull-to-refresh also built into `SkUiCollectionView` | Done; screen-reader refresh action open |
| CarouselView, IndicatorView | `SkUiCarouselView`; `SkUiIndicatorView` + Core | Done: `ItemsLayout` (or the items layout as carousel properties), `ItemsUpdatingScrollMode`, scroll bars, `IndicatorTemplate` (SkUi* only); plus several items in view and item effects; `VisibleViews` open |
| Picker | `SkUiPicker` + Core | Done: drawn field, drawn list on the app's popup system (`ISkUiPopupPresenter`); plus placement, drawn rows, keyboard and type-ahead everywhere; `UpdateMode` on every platform |
| DatePicker, TimePicker | `SkUiDatePicker`, `SkUiTimePicker` + Core; `SkUiDateTimePicker` | Done: drawn wheels on the app's popup system; plus wheel formats, ranges of times, steps, blackout days; a month calendar planned |
| Stepper | `SkUiStepper` + Core | Done, plus stacked buttons (`Orientation`), texts in place of − / +, corner radius and outline, and typed values (`SkUiStepper<T>`) |
| Entry, Editor, SearchBar | `SkUiEntry`, `SkUiEditor`, `SkUiSearchBar` + Core | Done: drawn, the platform keyboard and IME through a proxy per surface; plus `CaretColor`, `SelectionColor`, `AutofillHint`; device checks of composition, dictation and password managers open; hosted `Entry` (`SkUiMauiContentView`) for native-only needs |
| WebView, Map, media | Hosted (`SkUiMauiContentView`) | By design |
| Pages, Shell, navigation | MAUI | Out of scope |
| Frame, ListView, TableView, cells (`TextCell`, `ImageCell`, `SwitchCell`, `EntryCell`, `ViewCell`), Compatibility layouts | — | Out of scope: obsolete in MAUI (Frame since .NET 9, the rest in .NET 10); use Border and CollectionView |

## Which screens to port first

Drawn controls pay off where a screen holds **many views**. Every native MAUI view has a handler and a platform view; a drawn tree has one surface. On the stress page, 1,000 buttons are ready 16× (SkUi\*) to 25× (Core) faster than native MAUI on an old Android phone (Galaxy S9) ([README](../README.md#stress-results-device)).

Good first candidates:
- dashboards, cards, detail pages and forms with deep `Grid` / `StackLayout` nesting;
- screens that are slow to open, or where adding views to the page shows up in traces;
- repeated rows inside a `ScrollView`, and `CollectionView` lists (also grouped, grids, horizontal, with multiple selection): `SkUiCollectionView`, or `SkUiVirtualScrollView` for plain lists (see [Lists](#lists-collectionview)).

Leave for later: screens built around third-party controls (charts, calendars, data grids) that would have to sit on top of the drawn surface.

## Setup

```bash
dotnet add package SkiaUi.Maui
```

```csharp
// MauiProgram.cs
builder.UseMauiApp<App>().UseSkiaUi();
// Pickers with CommunityToolkit.Maui: lists in the toolkit's Popup (package SkiaUi.Maui.CommunityToolkit; see Pickers below)
builder.UseMauiApp<App>().UseMauiCommunityToolkit().UseSkiaUi().UseSkiaUiCommunityToolkit();
```

```xml
xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi"
```

### iOS and Mac Catalyst: scene lifecycle

SkiaUi 1.1.0 builds with Xcode 26.6 (workload set 10.0.401) or Xcode 27.1 (workload set 10.0.402.1); the installed workload set picks the Xcode. iOS 27 closes apps built with Xcode 27 that don't use the UIKit scene lifecycle. This is a MAUI and Xcode requirement, not a SkiaUi one, but apps that move to Xcode 27 need it. In each app's `Platforms/iOS` and `Platforms/MacCatalyst`:

```csharp
// SceneDelegate.cs
using Foundation;

[Register("SceneDelegate")]
public class SceneDelegate : MauiUISceneDelegate
{
}
```

```xml
<!-- Info.plist -->
<key>UIApplicationSceneManifest</key>
<dict>
	<key>UIApplicationSupportsMultipleScenes</key>
	<false/>
	<key>UISceneConfigurations</key>
	<dict>
		<key>UIWindowSceneSessionRoleApplication</key>
		<array>
			<dict>
				<key>UISceneConfigurationName</key>
				<string>__MAUI_DEFAULT_SCENE_CONFIGURATION__</string>
				<key>UISceneDelegateClassName</key>
				<string>SceneDelegate</string>
			</dict>
		</array>
	</dict>
</dict>
```

Also raise the app's Mac Catalyst `SupportedOSPlatformVersion` to `17.0` or higher. The demo and samples apps show the full setup (`MauiSkiaUiDemo/Platforms/iOS`, `MauiSkiaUiDemo/Platforms/MacCatalyst`). Microsoft's guide covers it in detail: [migrate .NET MAUI apps to the UIKit scene lifecycle](https://devblogs.microsoft.com/dotnet/migrate-dotnet-maui-apps-uikit-scene-lifecycle/).

## Where the drawn surface goes

A drawn tree starts at a **surface root**: a `SkUiContentView` (one child) or a drawn layout (`SkUiGrid`, stacks, `SkUiLayout`) placed in ordinary MAUI content. The root owns the Skia surface (GPU by default, `HwAccelerated`); every drawn view below it paints into that surface and has no platform view of its own.

```
ContentPage                      ← MAUI (navigation, toolbar, Shell)
└── Grid                         ← MAUI, optional
    ├── sk:SkUiContentView       ← surface root: one Skia surface
    │   └── sk:SkUiScrollView
    │       └── sk:SkUiVerticalStackLayout
    │           ├── sk:SkUiLabel, sk:SkUiButton, …     drawn
    │           └── sk:SkUiMauiContentView             native island
    │               └── WebView                        real native WebView
    └── Button                   ← MAUI, outside the surface
```

Rules:
- **Only drawn views go inside a drawn tree.** A plain `Label` or `Entry` inside `SkUiGrid` throws "Only SkiaUi children are supported". Wrap native controls in `SkUiMauiContentView`.
- **One surface per screen region.** Prefer one root for the page body (usually around the scroll view) over many small roots: each root is a platform view with its own GPU surface.
- **Drawn scrolling beats native scrolling.** Put the `SkUiScrollView` inside the root, not the root inside a MAUI `ScrollView`. Drawn surfaces inside a native `ScrollView` work (gestures are coordinated), but the drawn scroller is what keeps scrolling smooth.
- **The safe area is opt-in on the root** (MAUI 10's `SafeAreaEdges`). Every drawn root goes edge to edge by default (`None`), unlike MAUI's layouts, which default to `Container`. A page whose MAUI `Grid` kept its content out of the status bar and notch keeps doing so once drawn with `SafeAreaEdges="Container"` on the drawn layout root: it pads its children by how far the window's bars, notch and home indicator reach into it. `SoftInput` keeps the bottom clear of the keyboard too, `All` both. Drawn views inside the surface accept `SafeAreaEdges` and ignore it: pad them yourself, or make the region a root of its own.
- **Native islands sit on top.** `SkUiMauiContentView` places the real control above the surface, so drawn content (a drawn dropdown or popover) cannot cover it. While its scroller moves, it is drawn from a snapshot on Android and Windows ([SkUiMauiContentView.md](controls/SkUiMauiContentView.md)).

## Converting a page, step by step

### 1. Wrap the region

Pick the subtree and put it in a surface root. Keep the outer page, toolbar and anything native-only outside.

```xml
<!-- Before -->
<ContentPage ...>
    <ScrollView>
        <VerticalStackLayout Padding="16" Spacing="12">
            <Label Text="{Binding Title}" FontSize="24" />
            <Frame CornerRadius="8" HasShadow="True">
                <Grid ColumnDefinitions="*,Auto">
                    <Label Text="{Binding Summary}" />
                    <Image Grid.Column="1" Source="chevron.png" />
                </Grid>
            </Frame>
            <Entry Text="{Binding Note}" Placeholder="Note" />
            <Button Text="Save" Command="{Binding SaveCommand}" />
        </VerticalStackLayout>
    </ScrollView>
</ContentPage>

<!-- After -->
<ContentPage ... xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
    <sk:SkUiContentView>
        <sk:SkUiScrollView>
            <sk:SkUiVerticalStackLayout Padding="16" Spacing="12">
                <sk:SkUiLabel Text="{Binding Title}" FontSize="24" />
                <sk:SkUiBorder StrokeShape="RoundRectangle 8" Padding="20">
                    <sk:SkUiBorder.Shadow>
                        <Shadow Radius="8" Opacity="0.3" />
                    </sk:SkUiBorder.Shadow>
                    <sk:SkUiGrid ColumnDefinitions="*,Auto">
                        <sk:SkUiLabel Text="{Binding Summary}" />
                        <sk:SkUiImage Grid.Column="1" Source="chevron.png" />
                    </sk:SkUiGrid>
                </sk:SkUiBorder>
                <sk:SkUiEntry Text="{Binding Note}" Placeholder="Note" />
                <sk:SkUiButton Text="Save" Command="{Binding SaveCommand}" />
            </sk:SkUiVerticalStackLayout>
        </sk:SkUiScrollView>
    </sk:SkUiContentView>
</ContentPage>
```

Attached properties stay MAUI's (`Grid.Row`, `AbsoluteLayout.LayoutBounds`, `FlexLayout.Grow`, `SemanticProperties.Description`, `RadioButtonGroup.GroupName`). `BindableLayout` (`ItemsSource`, `ItemTemplate`, `ItemTemplateSelector`, `EmptyView`, `EmptyViewTemplate`) works on every drawn layout; the templates and the empty view must create drawn views, and a string `EmptyView` is not supported ([SkUiLayout.md](controls/SkUiLayout.md#bindablelayout)).

### 2. Rename the controls

| MAUI | SkiaUi | Notes |
| --- | --- | --- |
| `ContentView` | `SkUiContentView` | Also the base class for drawn custom controls; `ControlTemplate` of drawn views ([templated controls](#templated-controls)) |
| `Grid` | `SkUiGrid` | Prefix swap, also in code: `grid.Add(view, column, row)`, `AddWithSpan`, `SetRow` / `SetColumn` / spans for a view and `AddRowDefinition` / `AddColumnDefinition` work as on MAUI's `Grid`; children must be drawn views. `SkUiGridSplitter` lets users resize its rows or columns (also in a MAUI `Grid`) |
| `VerticalStackLayout` / `HorizontalStackLayout` | `SkUiVerticalStackLayout` / `SkUiHorizontalStackLayout` | Prefix swap, also in code: every drawn layout has MAUI `Layout`'s `Add` / `Insert` / `Remove` / `Clear` and collection initializers; `AddRange` adds many children with one re-measure |
| `StackLayout` | `SkUiVerticalStackLayout` / `SkUiHorizontalStackLayout` | Pick by `Orientation`; keep `Spacing` |
| `AbsoluteLayout` | `SkUiAbsoluteLayout` | |
| `FlexLayout` | `SkUiFlexLayout` | Also `SkUiWrapLayout` for chips and tags |
| Toolkit `StateContainer` / `StateView` (attached properties) | `SkUiStateContainer` / `SkUiStateView` | Same properties and `ChangeStateWithAnimation` overloads, on any drawn layout ([SkUiStateContainer.md](controls/SkUiStateContainer.md)) |
| Toolkit `Expander` | `SkUiExpander` | Same properties and `ExpandedChanged`; `Header` / `Content` must be drawn views. `ExpandDirection` → `SkUiExpandDirection`, `ExpandedChangedEventArgs` → `SkUiExpandedChangedEventArgs`; drop `HandleHeaderTapped` (a native list view workaround). `IsExpanded` is two-way by default. Plus `ContentTemplate`, `LazyContentExpansion`, `AnimationLength` / `AnimationEasing` ([SkUiExpander.md](controls/SkUiExpander.md)) |
| Hand-made skeleton views (shapes with animations) | `SkUiShimmer` / `SkUiCoreShimmer` | Your skeleton as `SkeletonContent`, one shimmer around it: see [Shimmer](#shimmer) |
| Toolkit `AvatarView` | `SkUiAvatarView` | Prefix swap; in SkiaUi.Maui itself. Defaults that differ: see [Avatar view](#avatar-view) |
| `SwipeView` | `SkUiSwipeView` | Prefix swap: `SwipeItems` / `SwipeItem` stay MAUI's, `SwipeItemView` → `SkUiSwipeItemView` with drawn content; same events, `Threshold`, modes and `Open` / `Close`. Plus `IsOpen`; scrolling around it and a new binding context close it ([SkUiSwipeView.md](controls/SkUiSwipeView.md)) |
| `ScrollView` | `SkUiScrollView` | Plus snap points, overscroll and its events (`Overscrolled`, `PullReleased`, `PullEdges`) |
| `RefreshView` (around other content) | `SkUiRefreshView` | Prefix swap with drawn content: same `IsRefreshing`, `Command`, `RefreshColor`, `IsRefreshEnabled`, `Refreshing`. Pulls through the content's vertical scrollers at their top, or the view itself over content that does not scroll ([SkUiRefreshView.md](controls/SkUiRefreshView.md)). Mouse drags no longer pull unless `IsMousePullEnabled`. Plus `RefreshStyle` (overlay badge or inline, the platform's by default), `RefreshTriggerDistance`, and `RefreshCompletion="Automatic"`: the view ends the refresh when an async command (busy while it runs) or the deferrals taken in `Refreshing` are done, instead of the view model setting `IsRefreshing` back |
| `CollectionView` (+ `RefreshView`) | `SkUiCollectionView` | SkiaUi's own API, not a prefix swap: see [Lists](#lists-collectionview) |
| `CarouselView`, `IndicatorView` | `SkUiCarouselView`, `SkUiIndicatorView` | Prefix swap, with the items layout as properties of the carousel: see [Carousels](#carousels-carouselview-indicatorview) |
| `Border` | `SkUiBorder` | `StrokeShape`, brush strokes, dashes |
| `Frame` (obsolete) | `SkUiBorder` | `CornerRadius` → `StrokeShape="RoundRectangle N"`, `HasShadow` → `Shadow`, `BorderColor` → `Stroke` |
| `Label` | `SkUiLabel` | Spans, span taps, `TextType="Html"`. Plus `ShrinkToFit` / `MinimumFontScale` and `AllowsTightening` (text that does not fit gets tighter and smaller), `GrowToFill` / `MaximumFontScale` (text that fills its label) and a custom `LineBreaker` |
| `Button` | `SkUiButton` | `ImageSource`, `ContentLayout` |
| `Image` | `SkUiImage` | Shared memory and disk cache, transformations, placeholders |
| `ImageButton` | `SkUiImageButton` | |
| `BoxView` | `SkUiBox` | |
| `Ellipse`, `Line`, `Rectangle`, `RoundRectangle`, `Path`, `Polygon`, `Polyline` | `SkUiEllipse`, `SkUiLine`, … | |
| `CheckBox`, `Switch`, `RadioButton` | `SkUiCheckBox`, `SkUiSwitch`, `SkUiRadioButton` | Optional third state; radio `Content` and `ControlTemplate` |
| `Slider`, `ProgressBar`, `ActivityIndicator` | `SkUiSlider`, `SkUiProgressBar`, `SkUiActivityIndicator` | Vertical slider, indeterminate progress |
| `GraphicsView`, `SKCanvasView` | A `SkUiView` subclass | Override `MeasureContent` and `OnPaintContent(SKCanvas)` |
| `Entry`, `Editor`, `SearchBar` | `SkUiEntry`, `SkUiEditor`, `SkUiSearchBar` | Prefix swap; drawn, typed through the platform keyboard: see [Text input](#text-input-entry-editor-searchbar) |
| `Stepper` | `SkUiStepper` | Prefix swap; plus `Orientation` (stacked buttons), `DecrementText` / `IncrementText` + font (custom − / +), `CornerRadius` / `BorderWidth` / `BorderColor`; `SkUiStepper<T>` for `int` / `decimal` / … values |
| `Entry` with `Keyboard="Numeric"` + a converter; a component library's numeric entry or up-down | `SkUiNumericEntry`, `SkUiNumericEntry<T>` | SkiaUi's own API: see [Numbers](#numbers-numeric-entries) |
| `Picker` | `SkUiPicker` | Prefix swap; its list opens on your popup system: see [Pickers](#pickers-picker) |
| `DatePicker`, `TimePicker` | `SkUiDatePicker`, `SkUiTimePicker` | Prefix swap; wheels on your popup system like the picker's list: see [Pickers](#pickers-picker) |
| `WebView`, maps, media | Wrapped in `SkUiMauiContentView` | Native by design |
| `IsClippedToBounds` (layouts, content views, the scroll, swipe, refresh and indicator views, the radio button) | Same name on the drawn containers | The same property as `ClipToBounds`, which every drawn view has: leaves clip by default, layouts do not |

Per-control differences: [docs/controls/](controls/README.md).

### 3. Wrap native-only controls

```xml
<sk:SkUiMauiContentView>
    <WebView Source="{Binding HelpUrl}" />
</sk:SkUiMauiContentView>
```

The wrapper takes part in drawn layout; the native control keeps its own input, keyboard and accessibility. A vertical drag that starts on it still scrolls the drawn list around it.

### 4. Move styles and resources

Styles target a type, and drawn controls are different types:

```xml
<!-- Before -->
<Style TargetType="Label">
    <Setter Property="TextColor" Value="{AppThemeBinding Light={StaticResource Gray900}, Dark={StaticResource White}}" />
</Style>

<!-- After: keep the MAUI style for the native labels that remain, add one for drawn labels -->
<Style TargetType="sk:SkUiLabel">
    <Setter Property="TextColor" Value="{AppThemeBinding Light={StaticResource Gray900}, Dark={StaticResource White}}" />
</Style>
```

- Implicit styles of MAUI types (`TargetType="Label"`) do not reach drawn controls; copy them with the `sk:` type. `BasedOn` works between drawn styles.
- `VisualStateManager` groups and setters work, with MAUI's state names (`Normal`, `Disabled`, `Pressed`, `PointerOver`, `Focused`, `Checked`, `On` / `Off`).
- **Disabled buttons** draw the fill the app set, as MAUI's: a `Disabled` state's `Background` setter ports as it is (or as `FillColor`); only the scheme's default fill turns grey (`SkUiColorScheme.Disabled`), and a button without a fill keeps none and dims its text. A `Disabled` state that only changes the background → `DisabledFillColor` on the button or in its style (image buttons too). A text button whose `Disabled` state sets its own `TextColor` → also `DisabledFillColor="Transparent"`, so the text is drawn in that color instead of dimmed ([SkUiButton.md](controls/SkUiButton.md#disabled-buttons)).
- Colors, brushes, `AppThemeBinding`, `OnPlatform`, `OnIdiom`, converters and resource dictionaries are MAUI's and need no change.
- **Look.** Drawn controls are drawn by a look (`SkUiLook`), not by the native widgets. The default look (`DefaultSkUiLook`) draws the same on every platform, so a migrated page no longer changes between Android and iOS (a picker field is outlined everywhere, its list a centered dialog on phones). For the platform's appearance, set the platform look: `builder.UseSkiaUi(options => options.Look = DefaultSkUiLook.ForCurrentPlatform())` (`IosSkUiLook` for iOS 26, `MacSkUiLook` for Mac Catalyst in the iPad idiom, `AndroidSkUiLook` for MAUI's Material 2 Android controls and `AndroidMaterial3SkUiLook` with `<UseMaterial3>true</UseMaterial3>`, which `ForCurrentPlatform` detects; `WindowsSkUiLook`). The platform looks are measured against MAUI's native controls, so unstyled drawn controls take the native ones' sizes; the app's MAUI styles still need `SkUi*` styles. Check them in your app with `SkUiLookComparisonPage` (native and drawn controls side by side with their sizes; push it from a debug menu) or the `skiaui-look` agent skill, and customise the look where yours differs ([ControlLook.md](design/ControlLook.md#matching-mauis-native-controls), samples app: Customisation). Colors come from the color scheme: set the ones you care about explicitly or pick the look's (`GetColorScheme`, set before the page's controls are created); the platform schemes also give buttons and activity indicators the native colors (an iOS system button has no fill and tinted text, Windows' standard button is light with a border).
- **Fonts** registered with `ConfigureFonts` work by alias, and on iOS / Mac Catalyst by PostScript name, as on MAUI. Text follows the OS text size (`FontAutoScalingEnabled`), as MAUI's does.

### 5. Convert gestures and touch behaviors

MAUI's tap, swipe, pan, pinch and pointer recognizers work on drawn views as on MAUI: same events and argument types, commands and parameters, the view as the sender. Gesture XAML and code-behind handlers need no change. They run in the gesture arena, so a pan or swipe inside a drawn scroller competes with it as the drawn gestures do (the innermost wins once it moves past the touch slop). For taps, the shorter drawn form is also available:

```xml
<!-- MAUI form: still works -->
<sk:SkUiGrid>
    <sk:SkUiGrid.GestureRecognizers>
        <TapGestureRecognizer Command="{Binding OpenCommand}" CommandParameter="{Binding .}" />
    </sk:SkUiGrid.GestureRecognizers>
</sk:SkUiGrid>

<!-- Drawn form -->
<sk:SkUiGrid TappedCommand="{Binding OpenCommand}" TappedCommandParameter="{Binding .}" ShowsPressEffect="True" />
```

MAUI's tooltips (`ToolTipProperties.Text`) also need no change: the surface shows them for the drawn view under a mouse or trackpad pointer (Mac Catalyst, iPad with a pointer, Windows; none on phones, as MAUI). A Core node takes `SetToolTip(text)`.

The rest needs converting. A drawn view that has it writes one `Trace` line on its first press, naming what is ignored and what to use. The drawn gesture events remain an alternative to the recognizers.

| MAUI / toolkit | SkiaUi |
| --- | --- |
| `TapGestureRecognizer` | Works as is (any `NumberOfTapsRequired`; `Buttons` primary, secondary or both), or `Tapped` / `TappedCommand` + `TappedCommandParameter`, `DoubleTapped` / `DoubleTappedCommand` |
| `SwipeGestureRecognizer` | Works as is (`Direction`, `Threshold`, `Command`). A fast flick past the touch slop also swipes, as on iOS. Alternative: `SwipeDirections` + `Swiped` / `SwipedCommand` |
| `PanGestureRecognizer` | Works as is with `TouchPoints="1"` (the default); more touch points are not run. Alternative: `PanUpdated` (also the release velocity, and `PanAxis`) |
| `PinchGestureRecognizer` | Works as is (`ScaleOrigin` a 0–1 fraction, as MAUI's). Alternative: `PinchUpdated` (also `TotalScale`, rotation, `Origin` in DIPs) |
| `PointerGestureRecognizer` | Works as is: entered / moved / exited from hover, pressed / released from presses of its `Buttons`. `GetPosition` takes the view or any element drawn on the same surface; `GetPosition(null)` (the window) is `null`. A press another gesture takes (a scroller's drag) still ends with `PointerReleased`. Alternatives: `IsPointerOver` and the `PointerOver` visual state; `SkUiPointerGestureRecognizer` in `Gestures` |
| `DragGestureRecognizer`, `DropGestureRecognizer` | Not run: keep that part native (in an `SkUiMauiContentView`) |
| Toolkit `TouchBehavior` `Command` / `LongPressCommand` | `TappedCommand` / `LongPressedCommand` (+ parameters) |
| Toolkit `TouchBehavior` press scale / opacity / color | `ShowsPressEffect="True"` (the look's dim or ripple; with `PressFeedback="Fade"` the view fades to the look's `PressedContentOpacity`, 0.4, composited), or a `Pressed` visual state with your own setters |
| A tap or pan recognizer that only swallows touches | Often not needed: drawn layouts, labels and images without gestures are transparent to touches, and what is under them gets the touch. Where blocking is the point (a backdrop under a drawn popover), keep an empty `TapGestureRecognizer` or give the view an empty `Tapped` handler |
| `InputTransparent`, `CascadeInputTransparent` | Same meaning on both layers: with `CascadeInputTransparent="False"`, only the container lets touches through and its children still take them. A cascading container does not set its children's own `InputTransparent` (MAUI does); what they receive is the same |

How drawn gestures resolve: every press runs a **gesture arena** over the views under the finger and their ancestors. The innermost tap wins, a scroll or swipe that moves past the touch slop wins over taps, and a long press wins once it fires. A tappable row with a check box inside therefore behaves the same on every platform, without per-platform workarounds. See [EventMechanism.md](design/EventMechanism.md).

Recognizers on **native views around a surface** (a page's content `Grid` with a background `TapGestureRecognizer`, a `ScrollView`) stay native and keep working. Drawn gestures go first: on iOS and Mac Catalyst a native ancestor's tap or long-press recognizer fires only for touches no drawn control takes (a tap on a label or an empty area still reaches it), and a native pan or swipe waits while a drawn scroller or swipe may take the drag.

Platform behaviors (any `PlatformBehavior`, such as `TouchBehavior`) and effects attach to a native view, which drawn views do not have. Plain behaviors that only use the view's events and properties (for example `EventToCommandBehavior`) keep working.

### 6. Port custom controls

| What the custom control is | Port to |
| --- | --- |
| A `ContentView` composing other views in XAML or code | A `SkUiContentView` subclass composing drawn views (`x:Class` XAML or C#). Bindable properties carry over |
| A control drawn with `GraphicsView` / `SKCanvasView` | A `SkUiView` subclass: `MeasureContent` for its size, `OnPaintContent(SKCanvas)` for drawing, gesture events for input, `AddSkeletonBones` for its bones in a [skeleton from the content](controls/SkUiShimmer.md#a-skeleton-from-the-content) (else one bone of its rectangle) |
| A control with a custom handler or renderer, an `Effect`, or platform code | Redraw it as above, or keep it native inside `SkUiMauiContentView`. Common effects have drawn equivalents: rounded corners → `SkUiBorder StrokeShape` or `Clip`, shadows → `Shadow` |
| A templated control (`ControlTemplate`) | A `SkUiContentView` subclass (or a style on `SkUiContentView`) with a `ControlTemplate` of drawn views and an `SkUiContentPresenter`; `TemplateBinding` becomes `RelativeSource AncestorType` ([below](#templated-controls)) |
| Many small repeated parts (cells, tiles, chips) where allocation matters | Core nodes (`SkUiCore*`) under a `SkUiCoreHost`: no `BindableObject` per part ([SkUiCore.md](controls/SkUiCore.md)). They disable and take commands as views do: `SetIsEnabled` (cascading, also from the host), `SetInputTransparent`, `SetTappedCommand` / `SetTappedCommandParameter`. Image setters take a MAUI `ImageSource` or a string directly (`SetSource(viewModel.Photo)`, `SetImageSource("save.png")`); set it again when a bound source changes ([Images from MAUI sources](controls/SkUiCore.md#images-from-maui-sources)) |

Custom handlers registered for MAUI types (`Label`, `Button`, …) do not affect the drawn controls. Check what each one did (for example removing Android's button padding or the iOS text field border) and set the equivalent properties on the drawn control.

#### Templated controls

`SkUiContentView` (and the content views built on it, such as `SkUiBorder`) and `SkUiRadioButton` take a `ControlTemplate` of drawn views; `ContentPresenter` becomes `SkUiContentPresenter`. MAUI resolves `{TemplateBinding}` and `RelativeSource TemplatedParent` only for its own templated views, so they do not reach drawn controls: bind to the control by ancestor type.

```xml
<!-- Before -->
<ControlTemplate x:Key="CardTemplate">
    <Border Padding="12">
        <VerticalStackLayout>
            <Label Text="{TemplateBinding Title}" />
            <ContentPresenter />
        </VerticalStackLayout>
    </Border>
</ControlTemplate>

<!-- After -->
<ControlTemplate x:Key="CardTemplate">
    <sk:SkUiBorder Padding="12">
        <sk:SkUiVerticalStackLayout>
            <sk:SkUiLabel Text="{Binding Title, Source={RelativeSource AncestorType={x:Type local:CardView}}}" />
            <sk:SkUiContentPresenter />
        </sk:SkUiVerticalStackLayout>
    </sk:SkUiBorder>
</ControlTemplate>
```

- `{TemplateBinding X}` and `{Binding X, Source={RelativeSource TemplatedParent}}` both become `{Binding X, Source={RelativeSource AncestorType={x:Type local:CardView}}}`, naming the templated control's type (`local:CardView` here, which now derives from `SkUiContentView`). Keep `Mode`, `Converter` and `StringFormat`. `check_xaml.py --fix` rewrites them when it finds the type (the template's `TargetType`, its style's, or the control whose `ControlTemplate` it is).
- This is a compiled binding (XAML source generator, or XamlC with `x:DataType` in scope): no reflection, safe for trimming and NativeAOT. A custom markup extension imitating `TemplateBinding` could not be compiled.
- The template root inherits the control's binding context (MAUI's does not), so `{Binding X}` inside the template reaches the view model directly.
- The template is created when the content loads: with `ContentLoading="WhenShown"`, only once shown. Details: [SkUiContentView.md](controls/SkUiContentView.md#controltemplate).

### 7. Animations

`SkUiView.AnimateAsync(SkUiAnimatableProperty.Opacity, 0, 250, Easing.CubicOut)` animates opacity, translation, rotation and scale on the render thread, so it stays smooth while the UI thread works. Prefer it to MAUI's `FadeTo` / `TranslateTo`, which change the same bindable properties from the UI thread every frame. State changes of the built-in controls (press, check, toggle) already animate through the look.

APIs that take a reusable animation take a `SkUiViewAnimation` (render thread) instead of a MAUI `Animation` (UI-thread callbacks). Convert each child animation whose callback sets a composite property: its start and end become `From` / `To`, its easing and length move to the description.

| MAUI | SkiaUi |
| --- | --- |
| `element.FadeToAsync(0, 250, Easing.CubicIn)` | `view.AnimateAsync(SkUiAnimatableProperty.Opacity, 0, 250, Easing.CubicIn)` |
| `element.TranslateToAsync(0, 40, 300)` | `view.AnimateAsync(SkUiAnimatableProperty.TranslationY, 40, 300)` (one call per axis) |
| `element.ScaleToAsync(0.9)` / `RotateToAsync(90)` | `view.AnimateAsync(SkUiAnimatableProperty.Scale, 0.9)` / `(…Rotation, 90)` |
| `new Animation(v => view.Opacity = v, 1, 0, Easing.CubicIn)` | `new SkUiViewAnimation([new(SkUiAnimatableProperty.Opacity, from: 1, to: 0)], 250, Easing.CubicIn)` |
| Parent `Animation` with children over the same span | One `SkUiViewAnimation` listing every property (they run together) |
| Toolkit `StateContainer.ChangeStateWithAnimation(layout, state, before, after)` with `Animation`s | `SkUiStateContainer.ChangeStateWithAnimation(layout, state, before, after)` with `SkUiViewAnimation`s; `SkUiViewAnimation.FadeOut()` / `FadeIn()` cover the common case, no `to` ends at the view's own value. Or set `sk:SkUiStateContainer.BeforeStateChangeAnimation="FadeOut"` / `AfterStateChangeAnimation="FadeIn"` once and drop the call: every `CurrentState` change animates |

Animations of layout properties (`WidthRequest`, `HeightRequest`, `Margin`) and child animations with staggered spans have no render-thread form: keep them as MAUI or `SkUiAnimationClock` callbacks (they re-lay out every frame), or redesign them as transforms.

### 8. Verify

- **Build and run the screen.** Compare it with the native version side by side; check light and dark themes and a large OS text size.
- **Read the debug output.** Search for `SkiaUi:` lines: they name MAUI gesture input that drawn views ignore.
- **Tap everything** that had a gesture recognizer or behavior; test taps inside scrollers and drags that start on buttons.
- **Accessibility.** Drawn controls are read by TalkBack, VoiceOver and Narrator from MAUI's `SemanticProperties`, and take keyboard focus. Check that tappable containers have a description or readable text inside.
- **UI tests.** `AutomationId` is exposed to UI test frameworks through the accessibility tree (Appium and Maestro find drawn views by id as they find MAUI's; Maestro's bare `id:` matches Android's `package:id/…` form). A screen reader reads a tappable row as one element, so ids inside it (a status label), on plain layouts and on decorative views are found only when the UI-test build turns on `UseSkiaUi(o => o.ExposeAutomationIds = true)` (under the app's own symbol); read their text from the row's name. A collection item's root id is on its item when the list has taps or selection. In unit tests, `SkUiDiagnostics.SimulateTap(element)` and `HitTest` drive drawn input without a device.
- **Defer what is not shown.** Other tabs and hidden or collapsed panes can wait: wrap them in `SkUiContentView ContentLoading="WhenShown"` with a `ContentTemplate` ([SkUiContentView.md](controls/SkUiContentView.md#loading-content-when-shown)).
- **Measure.** Time the page from navigation to first frame before and after; [Performance.md](Performance.md) lists what costs most while scrolling (shadows, gradients and clips in long lists).

## Lists (`CollectionView`)

`SkUiCollectionView` ([SkUiCollectionView.md](controls/SkUiCollectionView.md)) replaces a `CollectionView` (and a `RefreshView` around it) on the drawn surface: items are created near the viewport and recycled, with drawn item templates. Most of MAUI's list XAML ports by changing the prefix; the rest of its API is SkiaUi's own and is converted member by member. The full mapping is in [collection-view.md](../plugins/skiaui-migration/skills/skiaui-migrate/references/collection-view.md). The usual changes:

- `ItemsSource`, `ItemTemplate` (or a selector), `SelectedItem`, `SelectedItems`, `SelectionChangedCommand` (+ parameter), `EmptyView`, `IsGrouped`, `GroupHeaderTemplate`, `GroupFooterTemplate`, `RemainingItemsThreshold` (+ event, command): same names. The templates and the empty view create drawn views; a grouped source has MAUI's shape (each group the list of its items).
- `SelectionMode="Single"` / `"Multiple"`: same; `SkUiSelectionMode.SingleDeselect` clears the selection when the selected item is tapped again. `SelectionChanged` gets `SkUiSelectionChangedEventArgs` (`PreviousSelection` / `CurrentSelection`, plus `PreviousItem` / `CurrentItem`), and `SelectionChanging` can cancel a tap's change; `SelectAll()` / `ClearSelection()`.
- The `Selected` visual state on the item's root works as in MAUI; by default the selected item also gets a light accent background (`SelectionBackground="Transparent"` turns it off).
- `Header` / `Footer` / `EmptyView`: a drawn view, or text in XAML as on MAUI (`Header="Contacts"` shows a label; the empty view's is centered). Data with a template converts: drop `Header="{Binding Info}"` and bind the template through the list's context (`{Binding Info.Title}` instead of `{Binding Title}`; `check_xaml.py --fix` rewrites an inline template), since `HeaderTemplate` / `FooterTemplate` / `EmptyViewTemplate` take the list's binding context. `IsStickyHeader` / `IsStickyFooter` keep them in place over the list (the items scroll behind them, uncovered at the start and the end).
- `ItemsLayout` as on MAUI (`"VerticalGrid, 2"`, `LinearItemsLayout` / `GridItemsLayout` elements): it decides the orientation, span and spacings. Without it, the list's own `Orientation`, `Span`, `ItemSpacing` (between rows) and `SpanSpacing` (within a row) give them; use one or the other. Its `SnapPointsType` / `SnapPointsAlignment` work as on MAUI: drags, flings and the wheel end on a row lined up with the start, center or end (or at either end of the list, so a header stays reachable). `ItemSizingStrategy="MeasureFirstItem"` as on MAUI (or a fixed `ItemExtent`).
- Groups can collapse (`AllowGroupExpandCollapse`, `ExpandGroup` / `CollapseGroup`) and keep their header at the start (`IsStickyGroupHeader`): SkiaUi additions.
- A "Load more" footer built by hand → `LoadMoreMode` (`Manual`, `Auto`, `AutoOnUserScroll`), `LoadMoreCommand`, `IsLoadMoreActive`, at the end or the start (`LoadMorePosition`).
- `ItemsUpdatingScrollMode` as on MAUI: `KeepItemsInView` (default; the items that show stay in place when items are added before them), `KeepScrollOffset`, `KeepLastItemInView` (a chat or a log: the list scrolls to its end after each change).
- MAUI's `ScrollTo(index, groupIndex, position, animate)` / `ScrollTo(item, group, position, animate)` and `ScrollToRequested` as on MAUI; the awaitable `ScrollToIndex` / `ScrollToItem` / `ScrollToGroup` complete when the scroll ends. `Scrolled` reports `ScrollX` / `ScrollY`, and `FirstVisibleIndex` / `LastVisibleIndex` / `VisibleRangeChanged` give the visible items.
- **C# that names MAUI's types** needs these changes. XAML does not, but a handler wired in XAML has to take SkiaUi's argument type:
  - `Scrolled` passes MAUI's `ScrolledEventArgs`, not `ItemsViewScrolledEventArgs`: `VerticalOffset` / `HorizontalOffset` → `ScrollY` / `ScrollX`; `FirstVisibleItemIndex` / `LastVisibleItemIndex` → the list's `FirstVisibleIndex` / `LastVisibleIndex`, or `VisibleRangeChanged`, which is raised only when they change; `VerticalDelta` / `HorizontalDelta` → the difference from the offset of the previous event; `CenterItemIndex` → from the visible range.
  - `SelectionChanged` handlers take `SkUiSelectionChangedEventArgs` instead of `SelectionChangedEventArgs`; `PreviousSelection` / `CurrentSelection` stay. `SelectionChangedCommand` needs no change.
  - `SelectionMode.Single` → `SkUiSelectionMode.Single`, and the same for `None` and `Multiple`. Never cast one enum to the other: their values differ (`SkUiSelectionMode` puts `SingleDeselect` where MAUI has `Multiple`). A view-model property bound to `SelectionMode` becomes an `SkUiSelectionMode`.

  These stay as they are through 1.x; 2.0 is to take MAUI's types ([roadmap](design/Roadmap.md#held-for-20)).
- A `TapGestureRecognizer` on the item root that opens the item → `ItemTappedCommand` (the parameter defaults to the item). Buttons inside an item keep their own taps.
- `RefreshView` around it → `IsPullToRefreshEnabled="True"` with `IsRefreshing` and `RefreshCommand` (`Command` on `RefreshView`); setting `IsRefreshing` runs the command, as in MAUI. Keeping the `RefreshView` as `SkUiRefreshView` around the list works too ([SkUiRefreshView.md](controls/SkUiRefreshView.md)); then leave `IsPullToRefreshEnabled` off.
- `SwipeView` rows → `SkUiSwipeView` in the item template ([SkUiSwipeView.md](controls/SkUiSwipeView.md)): rows swipe horizontally while the list scrolls vertically, a scroll closes an open row, and a recycled row shows closed.
- The list needs a bounded size along its axis: in a stack layout or a `ScrollView` of the same axis it would create every item (as a native `CollectionView` there would).

- `CanReorderItems`, `CanMixGroups` and `ReorderCompleted`: same, from 1.3. As on MAUI, the item leaves its list (`RemoveAt`) and goes to its new place (`Insert`), so the source (or the group) must be an `IList` that can change; `ReorderCompleted` follows. A touch lifts the item with a long press, the mouse with a press and drag, and screen readers (TalkBack, VoiceOver) offer "Move up" / "Move down" on each item. A long press handled inside the item template fires first and keeps the touch.

Plain lists can also use the lighter `SkUiVirtualScrollView` ([SkUiVirtualVerticalStackLayout.md](controls/SkUiVirtualVerticalStackLayout.md)).

## Carousels (`CarouselView`, `IndicatorView`)

`SkUiCarouselView` ([SkUiCarouselView.md](controls/SkUiCarouselView.md)) and `SkUiIndicatorView` ([SkUiIndicatorView.md](controls/SkUiIndicatorView.md)) keep MAUI's names; the usual changes:

- `CarouselView` → `sk:SkUiCarouselView`, `IndicatorView` → `sk:SkUiIndicatorView`; `IndicatorView="{x:Reference …}"` links them as in MAUI. The item template creates drawn views (`EmptyView` too).
- `ItemsLayout` as on MAUI (`"HorizontalList"`, `"VerticalList"`, a `LinearItemsLayout` element): it decides the orientation, spacing and snap points. Without it, the carousel's own `Orientation`, `ItemSpacing`, `SnapPointsType` and `SnapPointsAlignment` give them (the defaults are MAUI's carousel defaults: horizontal, `MandatorySingle`, `Center`); use one or the other. A text `EmptyView` shows as on MAUI; data converts as on the collection view.
- Same: `ItemsSource`, `Position` and `CurrentItem` (two-way, with their events, commands and MAUI's argument types), `Loop` (default `true`), `PeekAreaInsets`, `IsSwipeEnabled`, `IsBounceEnabled`, `IsScrollAnimated`, `IsDragging`, `RemainingItemsThreshold` (+ event, command), `Scrolled`, `ScrollTo`, the `CurrentItem` / `NextItem` / `PreviousItem` / `DefaultItem` visual states.
- `ScrollTo(index, …)` and `ScrollToRequested` as on MAUI; also awaitable as `ScrollToIndex(index, animated)` / `ScrollToItem(item, animated)`.
- `ItemsUpdatingScrollMode`: `KeepItemsInView` (default) keeps the current item current wherever it moves (MAUI's handlers go back to the first item instead), `KeepScrollOffset` keeps `Position`, `KeepLastItemInView` moves to the last item. `HorizontalScrollBarVisibility` / `VerticalScrollBarVisibility` as on MAUI; `Default` shows none, and a looping carousel never does.
- `IndicatorTemplate` as on MAUI: a drawn view per indicator (the template must create drawn views), bound to its item, with the indicator colors as backgrounds and the `Selected` visual state on the current one. Drawing the indicators through the look (`IndicatorPainter`, `DefaultSkUiLook.IndicatorStyle`) stays the cheaper path, and its selection follows the scrolling.
- Not available: `VisibleViews` (use `GetRealizedView(index)` or `Scrolled`).
- New: several cards in view (`ItemExtent`), item effects drawn by the compositor (`ItemEffect`: `SkUiCoverFlowEffect`, `SkUiScaleEffect`), `ScrollPosition`; on the indicator `IndicatorSpacing` and `Orientation`.

## Pickers (`Picker`)

`SkUiPicker` ([SkUiPicker.md](controls/SkUiPicker.md)) keeps MAUI's `Picker` API: change the prefix. Same `ItemsSource` / `Items`, `ItemDisplayBinding`, `SelectedIndex` / `SelectedItem` (two-way, MAUI's selection rules), `SelectedIndexChanged`, `Title` / `TitleColor`, `TextColor`, the font properties, `CharacterSpacing`, the text alignments, `IsOpen` / `Opened` / `Closed`.

What differs is where the list shows. SkiaUi has no popup windows: the picker draws its list and hands it to an `ISkUiPopupPresenter` of your app, built on what you already use for popups:

- **CommunityToolkit.Maui:** add the **SkiaUi.Maui.CommunityToolkit** package and call `UseSkiaUiCommunityToolkit()` after `UseSkiaUi()`.
- **A popup control or navigation service of your own:** one method to write, `ShowAsync(request, token)`: show `request.Content` at `request.Bounds` (or relative to `request.PlatformAnchor` with `request.AnchorBoundsInPlatformAnchor`), close it when the token is cancelled, return when it is gone. Sketches in [SkUiPicker.md](controls/SkUiPicker.md#presenter-examples).
- **A picker UI of your own** (a wheel, a searchable list): the presenter reads `SkUiListPopupRequest` (`Items`, `SelectedIndex`, `Title`) and calls `request.Complete(index)` (or `SetSelectedIndex(index)` as the user goes, for a UI that stays open). For one picker type rather than the whole app, derive the picker and override `SelectItemsAsync(selection, token)`: the items in, the chosen items out (or `ShowPopupAsync(request, token)` for wheels and dates).
- **Nothing registered:** the list opens in MAUI's action sheet (plain item texts, MAUI's UI), with one trace line.

Platform differences go: the list is the same drawn list everywhere, anchored to the field as a combo box on desktops and tablets and in the window on phones (`PopupPlacement` and the look decide). To keep iOS's wheel picker, set `PopupPlacement="BottomPane"`: docked to the window's bottom edge, drawn as a wheel with `IosSkUiLook` (a list in the pane with other looks; [The bottom pane](controls/SkUiPicker.md#the-bottom-pane)). `ios:Picker.UpdateMode="WhenFinished"` becomes `UpdateMode="WhenFinished"` on the picker itself, on every platform: the choice waits for an OK button (texts translated with SkiaUi's other texts in `SkUiStrings`, [Localization.md](design/Localization.md), or per picker). New: `ItemTemplate` (drawn rows), `MaxPopupHeight`, keyboard navigation and type-ahead on every platform, several choices (`SelectionMode="Multiple"`, `SelectedItems`: a replacement for a multi-select combo box) and, as a control of its own, wheels of several columns ([SkUiMultiColumnPicker](controls/SkUiMultiColumnPicker.md): a replacement for a multi-column wheel picker).

**`DatePicker` and `TimePicker`** become `SkUiDatePicker` and `SkUiTimePicker` by the prefix ([SkUiDatePicker.md](controls/SkUiDatePicker.md), [SkUiTimePicker.md](controls/SkUiTimePicker.md)): the same `Date` / `MinimumDate` / `MaximumDate` / `Format` / `DateSelected`, `Time` / `Format` / `TimeSelected`, `IsOpen` / `Opened` / `Closed` (MAUI's event types), the text and font properties. Instead of the platform's dialog or wheels, drawn wheels open on the same presenter as the picker's list, ordered and named by the culture; a presenter can still show a platform dialog by handling `SkUiDatePopupRequest` / `SkUiTimePopupRequest`. SkiaUi keeps a time between `MinimumTime` and `MaximumTime` as MAUI keeps a date in its range. New beyond MAUI: `WheelFormat` (which wheels: a month and year picker), `BlackoutDates` / `DateEnabled`, `MinimumTime` / `MaximumTime`, `HourInterval` / `MinuteInterval` / `SecondInterval`, `DefaultHighlightedDate` / `DefaultHighlightedTime`, headers, `UpdateMode`, clearing, and `SkUiDateTimePicker` for a day and a time on one popup.

Coming from other date and time pickers: a format that lists the columns → `WheelFormat` (a .NET format, `"dd MM yyyy"`); the display format → `Format`; minute and hour steps → `HourInterval` / `MinuteInterval` / `SecondInterval`; blocked days → `BlackoutDates` (or `DateEnabled`); the value shown while there is none → `DefaultHighlightedDate` / `DefaultHighlightedTime`; column headers → `AreColumnHeadersVisible` with `SkUiStrings`' texts; looping wheels → `IsLooping`; a dialog mode → `PopupPlacement="Window"`; an OK / Cancel footer → `UpdateMode="WhenFinished"`; a clear button and a placeholder → `IsClearButtonVisible`, `Placeholder`. Not available: a drawn calendar popup (planned), durations longer than a day (use `SkUiMultiColumnPicker` with `SkUiPickerColumn.Range` columns).

## Text input (`Entry`, `Editor`, `SearchBar`)

`SkUiEntry`, `SkUiEditor` and `SkUiSearchBar` ([SkUiEntry.md](controls/SkUiEntry.md), [SkUiEditor.md](controls/SkUiEditor.md), [SkUiSearchBar.md](controls/SkUiSearchBar.md)) keep MAUI's API: change the prefix. `Text` (two-way), `TextChanged`, `Placeholder`, `Keyboard`, `ReturnType`, `ReturnCommand` / `Completed`, `IsPassword`, `MaxLength`, `IsReadOnly`, `CursorPosition` / `SelectionLength`, `ClearButtonVisibility`, the editor's `AutoSize`, the search bar's `SearchCommand` / `SearchButtonPressed` stay. The text is drawn; the platform's keyboard, input methods, autocorrect, dictation, clipboard and edit menu still do the typing. A field moved out of `SkUiMauiContentView` loses the overlay limits: it clips and scrolls with the page (no snapshots), drawn popups cover it, and it works in collection view rows and Core trees.

What changes:

- **Platform customizations.** Handler mapper tweaks on `Entry` (removing the underline, the caret color, `ios:Entry.CursorColor`, padding) do not apply: use `CaretColor`, `SelectionColor` and the field's own chrome: `FieldStyle` (`None` removes the underline or border; `Underline`, `Outline`, `Filled`, `TextBox`), `TextPadding`, `FillColor`, `BorderColor`, `BorderWidth`, `CornerRadius` / `CornerRadii` ([SkUiEntry.md](controls/SkUiEntry.md#field-chrome); pickers take the same but `TextPadding`). For every field of an app or a screen, set the look's (`DefaultSkUiLook.TextFieldStyle`, `TextFieldPadding`) or a subtree's look; `TextFieldPainter` draws chrome of your own.
- **A `Border` drawn around an `Entry` or `Picker`** (rounded, filled, with the entry's own underline hidden by a handler) becomes the field itself: `FillColor` for the border's `Background`, `BorderColor` / `BorderWidth` for its `Stroke` / `StrokeThickness`, `CornerRadius` for its `RoundRectangle`, `TextPadding` for its `Padding`; drop the wrapper and the handler. The field then draws the look's focus and error states on that chrome. Keep the `Border` (with `FieldStyle="None"`) only for a shape the field does not draw (a dashed stroke, a shadow, a gradient).
- **Autofill.** `AutofillHint` (`SkUiAutofillHints.Username`, `Password`, `OneTimeCode`, …) replaces platform-specific content types; password managers fill drawn fields on Android (the autofill service) and iOS. Call `SkUiAutofill.Commit()` when a sign-in or sign-up succeeds (`Cancel()` when it fails), so Android offers to save at once. On Android, Shell hides flyout and tab root pages from autofill (MAUI's `Entry` there gets nothing either): keep a sign-in form on a pushed page.
- **Keyboard avoidance.** A focused field scrolls above the soft keyboard inside drawn scrollers (`SkUiScrollView`, `SkUiCoreScrollView`, nested ones too). Custom code that moves the page for the keyboard (resizing a grid row, `WindowSoftInputModeAdjust` tricks) can usually go; keep the app's soft input mode.
- **Default font size** is 16 (SkiaUi's labels), not the platform's; set `FontSize` where it mattered.
- **Input filtering and masks.** The Community Toolkit's `MaskedBehavior` attaches to MAUI's `InputView` only: use `InputFormatters` with `SkUiMaskFormatter` (the toolkit's `X` placeholder is `&`, or `0` for digits only: `Mask="(XXX) XXX-XXXX"` → `Mask="(000) 000-0000"`; add `FormatsAssignedText="True"` to mask bound text too). `TextChanged` handlers that strip characters or cut the text (setting `Text` back, which resets the caret and adds a second change) become `SkUiFilteringFormatter` / `SkUiLengthLimitingFormatter` or `SkUiTextInputFormatter.Create`, which act before the edit lands, keep the caret and keep undo working.
- **Icons and buttons in a field.** MAUI's `Entry` has none, so pages put an `Image` or `ImageButton` beside the entry in a `Grid` or `HorizontalStackLayout` inside a `Border` that draws the field (and hide the entry's own underline with a handler). Move them into `LeadingView` / `TrailingView` and drop the wrapper: the field's chrome, focus highlight and touch target then cover them, the text never runs under them, and a button there does not take focus from the field. A component library's input layout with leading / trailing views inside the field, and Avalonia's `InnerLeftContent` / `InnerRightContent`, map to the same properties.
- **Validation messages.** Pages that show an error with a `Label` under the entry (`IsVisible` bound to the error) and recolor the field through a handler, a `Border` or a Toolkit validation behavior's `InvalidStyle` can set `HasError` and `ErrorText` instead (a hint that stays while typing: `HelperText`): the outline turns to the color scheme's `Error` (or the field's `ErrorColor`), the message sits under the field, and screen readers read it with the field. Bind `HasError` to the view model's error (or to a Toolkit validation behavior's `IsNotValid` with `Source={x:Reference}`); the validation itself stays the app's. A component library's input layout with `HasError` / `ErrorText` / `HelperText`, or `Invalid` / `InvalidFocused` visual states, map to the same members.
- **`TextTransform`** transforms the text itself (typed, pasted, set by the app, and what is already there when it changes); `Text` and a two-way binding get the transformed text.

`Stepper` becomes `SkUiStepper` by the prefix ([SkUiStepper.md](controls/SkUiStepper.md)), with MAUI's coercion; buttons only, as MAUI's (keep the label bound to its value). `Orientation="Vertical"` stacks the buttons, + on top; `DecrementText` / `IncrementText` with `FontFamily` / `FontSize` put words or icon-font symbols on them; `CornerRadius`, `BorderWidth` and `BorderColor` shape and outline it (a stepper styled through a platform handler or effect moves to these). A stepper bound to an `int` or `decimal` through a converter (or a `double` property kept only for it) can become typed: `<sk:SkUiStepper x:TypeArguments="x:Int32" Value="{Binding Quantity}" />` binds the `int` directly; its `ValueChanged` carries `SkUiValueChangedEventArgs<int>` instead of MAUI's `ValueChangedEventArgs`.

### Numbers (numeric entries)

MAUI has no numeric entry: pages use an `Entry` with `Keyboard="Numeric"`, a converter from text to the number (or a `string` property parsed in the view model), and often a `TextChanged` handler that strips what is not a digit, sometimes a `Stepper` beside it. `SkUiNumericEntry` (`double?`) and `SkUiNumericEntry<T>` (`int`, `decimal`, … through `x:TypeArguments`) replace the whole group ([SkUiNumericEntry.md](controls/SkUiNumericEntry.md)): bind `Value` to the number and drop the converter and the handler; the entry takes numbers only, clamps into `Minimum` / `Maximum` when the user is done (not while typing), shows the value in `Format` when not edited, and picks the keyboard from the type and range (on an iPhone, a minus sign is typeable only through this: `Keyboard.Numeric` is a pad without one). A `Stepper` beside it becomes `ButtonPlacement="End"` (or `Split`), the label showing its value goes. A `string` property kept only for the entry can become a `T?` (or `T` with `AllowNull="False"`).

| From | To |
| --- | --- |
| `<Entry Keyboard="Numeric" Text="{Binding Amount, Converter={StaticResource DecimalConverter}}" />` | `<sk:SkUiNumericEntry x:TypeArguments="x:Decimal" Value="{Binding Amount}" />` |
| A component library's numeric entry (`Value`, `Minimum`, `Maximum`, `CustomFormat`, `Culture`, `AllowNull`, `ValueChangeMode` `OnLostFocus` / `OnKeyFocus`, `MaximumNumberDecimalDigits`, `IsEditable`, `ShowClearButton`) | `SkUiNumericEntry`: `Value`, `Minimum`, `Maximum`, `Format`, `Culture`, `AllowNull`, `ValueChangeMode` `OnCompleted` / `OnKeystroke`, `MaximumDecimalDigits`, `IsEditable`, `ClearButtonVisibility="WhileEditing"`. Its `ValueChanged` runs synchronously here; losing focus does not round `Value` to the shown digits (`MaximumDecimalDigits` does). `PercentDisplayMode` has no counterpart (format with `P`) |
| A component library's numeric up-down (`SmallChange` / `LargeChange`, `UpDownPlacementMode` `Inline` / `InlineVertical`, `UpDownButtonAlignment` `Left` / `Right` / `Both`, `UpDownButtonColor`, `AutoReverse`) | `SkUiNumericEntry`: `Increment` / `LargeIncrement`, `ButtonOrientation` (`Inline` → `Horizontal`, `InlineVertical` → `Vertical`) with `ButtonPlacement` `Start` / `End` / `Split`, `ButtonTextColor`, `AutoReverse`. Button templates → `DecrementText` / `IncrementText` (words or icon-font symbols) or the look's `TextFieldStepButtonsPainter` |
| A numeric input laid out − [ value ] + | `SkUiNumericEntry` with `ButtonPlacement="Split"`: `Value`, `Minimum`, `Maximum`, `Increment`, `Format`; a read-only mode in which the buttons still step is `IsEditable="False"` (`IsReadOnly` stops the buttons too). Its increment / decrement commands have no counterpart yet: handle `ValueChanged` |

## Avatar view

`SkUiAvatarView` ([SkUiAvatarView.md](controls/SkUiAvatarView.md)) is in the main package and does not need the Community Toolkit.

**From the Community Toolkit's `AvatarView`:** change the prefix. `Text`, `ImageSource`, `TextColor`, `TextTransform`, the font properties, `CharacterSpacing`, `BorderColor` / `BorderWidth`, `CornerRadius`, `Padding` and the `Border` members stay. Three defaults differ:

- **Corners:** fully rounded at any size. The toolkit's are 24 at any size. Where a larger avatar relied on that, add `CornerRadius="24"`.
- **Background and text color:** from the look until set. The toolkit's background is transparent; add `BackgroundColor="Transparent"` to keep it.
- **Text size:** sized to the avatar while `FontSize` is not set.

Styles now apply to everything the toolkit's constructor set (padding, stroke, sizes). Drop workarounds that set these in code after construction. The toolkit's `Font` property is not there: set the font properties.

**From other avatar controls:** a name shown as initials → `Text` with `Initials` (`FirstWord`, `FirstAndLastWord`; one word gives one letter); a background picked per avatar → `AvatarColorMode` (by the text, so it is stable in recycled rows); an initials color → `TextColor`; inner padding → `Padding`; circle or square shapes → the default or `CornerRadius="4"`; size presets → `WidthRequest` / `HeightRequest` (the avatar sizes its text by itself); a status badge view around the avatar → `BadgeColor` and `BadgePosition` (a dot), or a count, an icon or a shape of your own through `BadgePainter`. Not available: group avatars (compose drawn avatars).

## Shimmer

`SkUiShimmer` ([SkUiShimmer.md](controls/SkUiShimmer.md)) shows `SkeletonContent` while `IsLoading`, then `Content`; the effect runs on the render thread and every shimmer is in phase. Build loading screens from Core bones (`SkUiCoreBox` in an `SkUiCoreHost`, or a whole `SkUiCoreShimmer`) and bind only `IsLoading`. A shimmer control with built-in layouts ports by building the skeleton from boxes (`SkUiBox` / `SkUiCoreBox` with `CornerRadius`) as `SkeletonContent`. A skeleton with parts that must not shimmer (cards around the bones, a Cancel button) uses `Mode="Bones"` with `SkUiShimmerBone` / `SkUiCoreShimmerBone` as the bones; it can also be laid over a page that stays in MAUI ([Bones](controls/SkUiShimmer.md#bones-a-skeleton-with-chrome)). A skeleton that copies the page's layout (the same cards, rows of the same sizes) need not be kept at all once the page is drawn: leave `SkeletonContent` out and the shimmer shows its content as the skeleton, each control drawing its bones ([a skeleton from the content](controls/SkUiShimmer.md#a-skeleton-from-the-content)); mark static parts `sk:SkUiShimmer.Skeleton="Keep"`.

**From a hand-made skeleton view** (MAUI shapes, one `Animation` per box, `IsLoading` passed down): replace the boxes with bones (`SkUiCoreBox` / `SkUiBox`, any color) inside one shimmer, and the animation with settings:

| Before | After |
| --- | --- |
| Opacity pulse `0.3 ↔ 1` with a cosine over 1.5 s | `Effect="Pulse" PulseOpacity="0.3" Duration="750"` |
| Color from a start to an end color linearly in 750 ms, restarting | `Effect="Color" BoneColor="{start}" HighlightColor="{end}" Duration="750" RepeatMode="Restart" Easing="Linear"` |
| Fade out of the skeleton when loading ends | `BeforeStateChangeAnimation="FadeOut 350 CubicInOut"` |
| `IsLoading` on every box | `IsLoading` on the shimmer |
| Boxes inside the real layout's cards, beside a Cancel button | `Mode="Bones"`: `SkUiShimmerBone`s in their places, the cards and the button as drawn views |
| A skeleton view that repeats the page's own layout (the same cards and rows, boxes where the texts go) | No skeleton: the drawn page in a shimmer without `SkeletonContent`, `Mode="Bones"`; texts become bars, cards keep their chrome; static texts `Skeleton="Keep"`; empty bound texts draw `SkeletonLines` placeholder lines; an empty collection view shows placeholder rows of its own template (no fake items in the view model). The page takes no input while loading |
| A skeleton view swapped with the MAUI page (a state container) | The page stays; one `SkUiShimmer` with only `SkeletonContent` over it, hidden once faded (`IsVisible` bound to `IsShowingSkeleton`) |
| Pausing on `Unloaded`, hardware layers, per-box animation names | Drop: a shimmer not drawn requests no frames; the bones are one recorded picture in one layer |

## Not available yet

| MAUI | Status | Meanwhile |
| --- | --- | --- |
| Drag and drop, context flyouts | Not planned | Keep native |
| `ListView`, `TableView`, cells, `Frame` | Obsolete in MAUI; not planned | `SkUiCollectionView` (or `SkUiVirtualScrollView` for plain lists), `SkUiBorder` |
| Shell, pages, navigation | Out of scope | Stay MAUI |
| Third-party controls (charts, calendars, data grids, signature pads) | Native | Host in `SkUiMauiContentView`, or keep them outside the surface |

## Platform-specifics

Platform-specifics (`ios:`, `android:`, `windows:` attached properties) compile on drawn controls and are ignored. The ones with a drawn member convert, and `check_xaml.py --fix` rewrites them inside `OnPlatform`, so they still apply on that platform only:

| Platform-specific | Drawn member |
| --- | --- |
| iOS `Entry.CursorColor` | `CaretColor` (every text field) |
| Android `Entry.ImeOptions` (`Done`, `Go`, `Next`, `Search`, `Send`) | `ReturnType` |
| iOS `Picker.UpdateMode`, `DatePicker.UpdateMode`, `TimePicker.UpdateMode` | `UpdateMode` (every picker, every platform) |
| iOS `Entry.AdjustsFontSizeToFitWidth` | None: the drawn entry does not shrink its text |

The checker reports the others as ignored. Drawn meanings of some of them (a tap on a slider's track, swipe transition modes) are in the [roadmap's backlog](design/Roadmap.md#backlog-1x).

## Checklist

- [ ] `UseSkiaUi()` registered; `xmlns:sk` added to the page.
- [ ] One surface root per page region; `SkUiScrollView` inside it.
- [ ] Every control inside the root is a `SkUi*` view; native ones are wrapped in `SkUiMauiContentView`.
- [ ] Styles copied with `sk:` target types; visual states checked.
- [ ] Gesture recognizers other than taps, `TouchBehavior`s and effects converted; no `SkiaUi:` lines left in the debug output.
- [ ] Custom controls ported or hosted; custom handler tweaks replaced by properties.
- [ ] `BindableLayout` templates and empty views on drawn layouts create drawn views.
- [ ] Light / dark, large text, screen reader and keyboard checked.
- [ ] Page-open time and scrolling measured against the native version.
