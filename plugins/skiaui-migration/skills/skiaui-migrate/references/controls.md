# MAUI → SkiaUi controls

Namespace: `xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi"`. Drawn controls use MAUI's property names; the table lists only what differs.

## Hosts and layouts

| MAUI | SkiaUi | Notes |
| --- | --- | --- |
| `ContentView` | `sk:SkUiContentView` | Surface root (one child) or a hosted container; base class for drawn custom controls. Also `ContentTemplate`, `ControlTemplate` of drawn views (`TemplateBinding` → `RelativeSource AncestorType`, see [custom-controls.md](custom-controls.md#templated-control)), and `ContentLoading="WhenShown"` (+ `ContentLoadingDelay`, `ContentLoadedAnimation`) to create a pane only when first shown: use it for heavy tabs and hidden or collapsed panes |
| `Grid` | `sk:SkUiGrid` | `RowDefinitions`, `ColumnDefinitions`, spacing, `Grid.Row` / `Grid.Column` / spans as in MAUI |
| `VerticalStackLayout` | `sk:SkUiVerticalStackLayout` | |
| `HorizontalStackLayout` | `sk:SkUiHorizontalStackLayout` | |
| `StackLayout` | `sk:SkUiVerticalStackLayout` / `sk:SkUiHorizontalStackLayout` | Choose by `Orientation` (default vertical); drop `Orientation`; keep `Spacing` |
| `AbsoluteLayout` | `sk:SkUiAbsoluteLayout` | MAUI's `AbsoluteLayout.LayoutBounds` / `LayoutFlags` |
| `FlexLayout` | `sk:SkUiFlexLayout` | MAUI's attached `FlexLayout.*` properties |
| — | `sk:SkUiWrapLayout` | Chips and tags (wrapping rows) |
| Two views toggled by `IsVisible`, or a `DataTrigger` swapping `Content` | `sk:SkUiAlternateContentView` | `ShowsAlternate="{Binding …}"` shows `Content` (`false`), `AlternateContent` (`true`) or nothing (`null`); templates run on first show; optional switch animations |
| Toolkit `mct:StateContainer.*` / `mct:StateView.StateKey` | `sk:SkUiStateContainer.*` / `sk:SkUiStateView.StateKey` | Attached properties on any drawn layout: `StateViews` (drawn views), `CurrentState`, `CanStateChange`; `SkUiStateContainer.ChangeStateWithAnimation` in code (MAUI `Animation` arguments → `SkUiViewAnimation`, see below), or `sk:SkUiStateContainer.BeforeStateChangeAnimation="FadeOut"` / `AfterStateChangeAnimation="FadeIn"` in XAML to animate every bound change without code; `StateContainerException` → `SkUiStateContainerException` |
| `RefreshView` | `sk:SkUiRefreshView` | Prefix swap with drawn content: same `IsRefreshing` (two-way), `Command` / `CommandParameter`, `RefreshColor`, `IsRefreshEnabled`, `Refreshing` (handlers taking `EventArgs` still attach). Pulls through the content's vertical scrollers at their top, or the view itself over content that does not scroll. Mouse drags pull only with `IsMousePullEnabled="True"` (keep MAUI's desktop behavior by setting it). Optional: `RefreshCompletion="Automatic"` replaces `IsRefreshing = false` at the end of an async command; `RefreshStyle`, `RefreshTriggerDistance`. Around a `CollectionView`: see [collection-view.md](collection-view.md) |
| `SwipeView` | `sk:SkUiSwipeView` | Prefix swap: `SwipeItems` / `SwipeItem` stay as written (MAUI's types, drawn by the view); `SwipeItemView` → `sk:SkUiSwipeItemView` with drawn content; content drawn. Same `Threshold`, `Mode`, `SwipeBehaviorOnInvoked`, `SwipeStarted` / `SwipeChanging` / `SwipeEnded`, `Open` / `Close`. Drop `OpenRequested` / `CloseRequested` handlers (handler plumbing) |
| `CarouselView` | `sk:SkUiCarouselView` | Prefix swap; drawn item template and `EmptyView`. `ItemsLayout` → carousel properties: `Orientation`, `ItemSpacing`, `SnapPointsType`, `SnapPointsAlignment` (defaults: horizontal, `MandatorySingle`, `Center`, MAUI's carousel defaults; drop a `LinearItemsLayout` that only restates them). Same `ItemsSource`, `Position` / `CurrentItem` (two-way) and their events and commands, `Loop` (default `true`), `PeekAreaInsets`, `IsSwipeEnabled`, `IsBounceEnabled`, `IsScrollAnimated`, `RemainingItemsThreshold` (+ event, command), `Scrolled`, `ScrollTo`, the item visual states. Drop `ItemsUpdatingScrollMode` (the current item is always kept) and `VisibleViews` uses |
| `IndicatorView` | `sk:SkUiIndicatorView` | Prefix swap; link with the carousel's `IndicatorView="{x:Reference …}"` as in MAUI. Same `Count`, `Position`, `ItemsSource`, `IndicatorColor`, `SelectedIndicatorColor`, `IndicatorSize`, `IndicatorsShape`, `MaximumVisible`, `HideSingle`. `IndicatorTemplate` is not supported: plain dots port as they are; for other shapes set `DefaultSkUiLook.IndicatorStyle` (`Pill`) or an `IndicatorPainter` on the look and report it |
| Toolkit `mct:Expander` | `sk:SkUiExpander` | Same `Header`, `Content`, `IsExpanded` (two-way by default), `Direction`, `Command` / `CommandParameter`, `ExpandedChanged`; header and content must be drawn views (native ones in `sk:SkUiMauiContentView`). `ExpandDirection` → `SkUiExpandDirection`, `ExpandedChangedEventArgs` → `SkUiExpandedChangedEventArgs`; drop `HandleHeaderTapped`. Optional: `ContentTemplate`, `LazyContentExpansion="True"` (content in the tree only while expanded), `AnimationLength="250"` with `AnimationEasing="CubicOut"` |
| — | `sk:SkUiLayout` | Overlay: children share one slot |
| `ScrollView` | `sk:SkUiScrollView` | Same API (`Orientation`, `ScrollToAsync`, `Scrolled`, scroll bar visibility); plus `SnapPointsType`, `Overscroll`, overscroll events (`Overscrolled`, `PullReleased`, `PullEdges`) |
| `Border` | `sk:SkUiBorder` | `StrokeShape` (string `RoundRectangle 8` or a shape element), `Stroke`, `StrokeThickness`, dashes |
| `Frame` | `sk:SkUiBorder` | `CornerRadius="8"` → `StrokeShape="RoundRectangle 8"`; `BorderColor` → `Stroke`; `HasShadow="True"` → `<sk:SkUiBorder.Shadow><Shadow .../></sk:SkUiBorder.Shadow>`; Frame's default `Padding` is 20 |
| `ContentPresenter` | `sk:SkUiContentPresenter` | Inside a drawn `ControlTemplate` (of a `SkUiContentView`, a content view built on it, or a `SkUiRadioButton`) |

`IsClippedToBounds` → `ClipToBounds` (leaf controls clip by default, layouts do not). Attached properties stay MAUI's (`Grid.Row`, `AbsoluteLayout.LayoutBounds`, `FlexLayout.Grow`, `SemanticProperties.*`, `AutomationProperties.*`, `RadioButtonGroup.GroupName`, `VisualStateManager.VisualStateGroups`).

`BindableLayout` works on every drawn layout (`ItemsSource`, `ItemTemplate`, `ItemTemplateSelector`, `EmptyView`, `EmptyViewTemplate`): convert the template content and the empty view to drawn controls like the rest of the region (native ones go inside `sk:SkUiMauiContentView`). A string `EmptyView` throws: replace it with `<BindableLayout.EmptyView><sk:SkUiLabel Text="..." /></BindableLayout.EmptyView>`. No virtualization: fine for tens to a few hundred items; longer lists: `sk:SkUiCollectionView` (selection, header, empty view; [collection-view.md](collection-view.md)) or `sk:SkUiVirtualScrollView` (or `sk:SkUiVirtualVerticalStackLayout` inside a drawn scroller) with the same template.

## Controls

| MAUI | SkiaUi | Notes |
| --- | --- | --- |
| `Label` | `sk:SkUiLabel` | Text properties, `MaxLines`, `LineBreakMode`, `FormattedText` / `Span` (span `TapGestureRecognizer`s work), `TextType="Html"`. Code that shrank (or grew) the font size until the text fit becomes `ShrinkToFit="True"` with `MinimumFontScale` (or `GrowToFill="True"` with `MaximumFontScale`); native tightening (iOS `AllowsDefaultTighteningForTruncation`) becomes `AllowsTightening="True"` |
| `Button` | `sk:SkUiButton` | `Clicked`, `Command`, `Pressed` / `Released`, `ImageSource`, `ContentLayout`, `CornerRadius`, `BorderColor` / `BorderWidth`, `Padding`. Text does not wrap by default (as MAUI) |
| `Image` | `sk:SkUiImage` | `Source` (files, `MauiImage`, `FontImageSource`, URIs, streams), `Aspect`, `IsAnimationPlaying`; plus `Transformations`, `DownsampleWidth` / `DownsampleHeight`, `LoadingPlaceholder`, `ErrorPlaceholder` (FFImageLoading-style) |
| `ImageButton` | `sk:SkUiImageButton` | |
| `BoxView` | `sk:SkUiBox` | `Color`, `CornerRadius` |
| `Ellipse`, `Line`, `Rectangle`, `RoundRectangle`, `Path`, `Polygon`, `Polyline` | `sk:SkUiEllipse`, `sk:SkUiLine`, `sk:SkUiRectangle`, `sk:SkUiRoundRectangle`, `sk:SkUiPath`, `sk:SkUiPolygon`, `sk:SkUiPolyline` | Brushes, stroke model, `Aspect`, path markup |
| `CheckBox` | `sk:SkUiCheckBox` | `IsChecked`, `CheckedChanged`, `Color` |
| `Switch` | `sk:SkUiSwitch` | `IsToggled`, `Toggled`, `OnColor`, `ThumbColor` |
| `RadioButton` | `sk:SkUiRadioButton` | `Content` (text or a drawn view), `GroupName`, `Value`, `ControlTemplate` of drawn views |
| `Slider` | `sk:SkUiSlider` | `ThumbImageSource`; plus vertical |
| `ProgressBar` | `sk:SkUiProgressBar` | Plus indeterminate |
| `ActivityIndicator` | `sk:SkUiActivityIndicator` | |
| `GraphicsView`, `SKCanvasView` | A `SkUiView` subclass | See [custom-controls.md](custom-controls.md) |

`FFImageLoading`'s `CachedImage` → `sk:SkUiImage` (its circle / rounded / blur transformations have drawn equivalents in `Transformations`).

## Native islands

`Entry`, `Editor`, `SearchBar`, `Picker`, `DatePicker`, `TimePicker`, `WebView`, maps, media and third-party controls stay native inside the drawn tree (drawn `Entry`, `Editor` and `SearchBar` equivalents are planned; until they ship, wrap these too):

```xml
<sk:SkUiMauiContentView>
    <Entry Text="{Binding Name}" Placeholder="Name" />
</sk:SkUiMauiContentView>
```

The wrapper is measured and arranged like a drawn view; the native control sits above the surface. Drawn content cannot cover it (a drawn dropdown over an Entry is hidden behind it). One control per wrapper.

## Every drawn view

- Bindable properties, bindings (`x:DataType` compiled bindings too), styles, triggers, `VisualStateManager` (states: `Normal`, `Disabled`, `PointerOver`, `Focused`, buttons `Pressed`, check box `IsChecked`, switch `On` / `Off`, radio `Checked` / `Unchecked`).
- `Shadow`, gradient `Background`, `Clip`, `Opacity`, transforms, `InputTransparent`, `IsVisible`, `IsEnabled`, `Margin`, `HorizontalOptions` / `VerticalOptions`, `WidthRequest` / `HeightRequest`, `FlowDirection`.
- `SemanticProperties` / `AutomationProperties` / `AutomationId` (screen readers, UI tests), `Focus()` / `Unfocus()`, `IsTabStop` / `TabIndex`.
- Fonts registered with `ConfigureFonts` by alias; `FontAutoScalingEnabled` follows the OS text size.
- Not drawn: `ImageBrush`.
- Drawn controls are drawn by the look (`SkUiLook`): close to, not identical with, native widgets. Set colors explicitly where the design depends on them.

## Animations (code-behind)

Drawn views animate opacity, translation, rotation and scale on the render thread. Convert MAUI's UI-thread animations; each is one line:

| MAUI | SkiaUi |
| --- | --- |
| `view.FadeToAsync(0, 250, easing)` | `view.AnimateAsync(SkUiAnimatableProperty.Opacity, 0, 250, easing)` |
| `view.TranslateToAsync(x, y, 250)` | `AnimateAsync(SkUiAnimatableProperty.TranslationX, x, 250)` and `(…TranslationY, y, 250)`, awaited together |
| `view.ScaleToAsync(s)` / `RotateToAsync(d)` | `AnimateAsync(SkUiAnimatableProperty.Scale, s)` / `(…Rotation, d)` |
| `new Animation(v => view.Opacity = v, start, end, easing)` passed to an API | `new SkUiViewAnimation([new(SkUiAnimatableProperty.Opacity, from: start, to: end)], length, easing)` |
| Parent `Animation` with children over the same span | One `SkUiViewAnimation` listing every property |

Leave as MAUI animations (and say so in the report): animations of `WidthRequest` / `HeightRequest` / `Margin` and staggered child spans; they re-lay out every frame on drawn views too.
