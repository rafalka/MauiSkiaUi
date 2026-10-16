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
| Toolkit `mct:AvatarView` | `sk:SkUiAvatarView` | Prefix swap (in SkiaUi.Maui itself, no toolkit package): same `Text`, `ImageSource`, `TextColor`, `TextTransform`, font properties, `CharacterSpacing`, `BorderColor` / `BorderWidth`, `CornerRadius`, `Padding`, `StrokeShape`. Defaults that differ: corners fully rounded at any size until `CornerRadius` is set (add `CornerRadius="24"` where a larger avatar relied on the toolkit's 24), a background and text color from the look until set (add `BackgroundColor="Transparent"` to keep the toolkit's), text sized to the avatar while `FontSize` is not set. Optional: `Initials="FirstAndLastWord"` (a name in `Text` shown as initials) with `AvatarColorMode="LightBackground"`, `BadgeColor` for a status dot |
| Other avatar controls | `sk:SkUiAvatarView` | A name shown as initials → `Text` with `Initials` (`FirstWord` / `FirstAndLastWord`; one word gives one letter); a background picked per avatar → `AvatarColorMode`; an initials color → `TextColor`; inner padding → `Padding`; circle / square → the default / `CornerRadius="4"`; size presets → `WidthRequest` / `HeightRequest`; a status badge view around the avatar → `BadgeColor` / `BadgePosition` (or `BadgePainter`). Not available: group avatars |
| A shimmer control with built-in skeleton layouts | `sk:SkUiShimmer` (Core: `SkUiCoreShimmer`) | Build the skeleton from boxes with `CornerRadius` as `SkeletonContent` (prefer Core bones `SkUiCoreBox` in an `SkUiCoreHost`); the loaded content → `Content`; the loading flag → `IsLoading`; the bones' color → `BoneColor` (replaces their colors); the wave's color, width and direction → `HighlightColor`, `WaveWidth`, `WaveDirection`; one pass → `Duration` (ms); a skeleton repeated as list rows → the row added several times to a stack (a loop, or `BindableLayout`). One shimmer around the whole skeleton (one group per frame) |
| Hand-made skeleton view (MAUI shapes, one `Animation` per box, `IsLoading` passed down) | `sk:SkUiShimmer` around bones | One shimmer, bones as `SkUiBox` / `SkUiCoreBox`. Opacity pulse 0.3 ↔ 1 over 1.5 s → `Effect="Pulse" PulseOpacity="0.3" Duration="750"`; linear color animation of 750 ms restarting → `Effect="Color" BoneColor="{start}" HighlightColor="{end}" Duration="750" RepeatMode="Restart" Easing="Linear"`; fade out at the end → `BeforeStateChangeAnimation="FadeOut 350 CubicInOut"`; `IsLoading` on every box → `IsLoading` on the shimmer. Boxes inside cards or beside buttons that must not shimmer → `Mode="Bones"` with `sk:SkUiShimmerBone` (`CornerRadius`, optional `Color`) as the boxes. A skeleton that repeats the drawn page's own layout → drop it: the page as `Content` of a shimmer without `SkeletonContent` (`Mode="Bones"`), static texts `sk:SkUiShimmer.Skeleton="Keep"`, empty bound texts' placeholder lines `sk:SkUiShimmer.SkeletonLines` (default 1); an empty collection view shows rows of its template (`sk:SkUiShimmer.SkeletonItemCount`, default: fill it), so drop fake placeholder items; the content takes no input while loading. A skeleton swapped with a MAUI page → keep the page, lay the shimmer (only `SkeletonContent`, page background) over it in the same `Grid` cell, `IsVisible="{Binding IsShowingSkeleton, Source={RelativeSource Self}, x:DataType=sk:SkUiShimmer}"`. Drop `Unloaded` pausing and Android hardware layers |
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
| `Picker` | `sk:SkUiPicker` | Prefix swap: same `ItemsSource` / `Items`, `ItemDisplayBinding`, `SelectedIndex` / `SelectedItem` (two-way), `SelectedIndexChanged`, `Title` / `TitleColor`, text and font properties, alignments, `IsOpen` / `Opened` / `Closed`. Its list opens on the app's popup system: register a presenter once (with CommunityToolkit.Maui: the `SkiaUi.Maui.CommunityToolkit` package and `UseSkiaUiCommunityToolkit()`; see the note below), else MAUI's action sheet shows the items. `ios:Picker.UpdateMode="WhenFinished"` → `UpdateMode="WhenFinished"` on `sk:SkUiPicker` (every platform; Cancel / OK buttons, texts from `SkUiStrings.Current` (app-wide, all of SkiaUi's texts; derive it to read the app's resources) or `AcceptText` / `CancelText` on the picker: wire them to the app's localization); drop `ios:Picker.UpdateMode="Immediately"` (the default). Optional: `PopupPlacement` (`Anchored` combo box, `Window` dialog or sheet), `ItemTemplate` of drawn rows, `MaxPopupHeight`, `SelectionMode="Multiple"` with `SelectedItems` (several choices) |
| `DatePicker` | `sk:SkUiDatePicker` | Prefix swap: same `Date` (`DateTime?`, two-way, today by default), `MinimumDate` / `MaximumDate`, `Format`, `DateSelected`, `IsOpen` / `Opened` / `Closed` (MAUI's event args), text and font properties. Its wheels open on the app's popup system (the presenter registered for `SkUiPicker`). Optional: `WheelFormat` (`"MMMM yyyy"` a month picker), `BlackoutDates`, `DateEnabled` (code), `DefaultHighlightedDate`, `AreColumnHeadersVisible`, `UpdateMode="WhenFinished"`, `IsClearButtonVisible`, `Placeholder`; `sk:SkUiDateTimePicker` for a day and a time together |
| `TimePicker` | `sk:SkUiTimePicker` | Prefix swap: same `Time` (`TimeSpan?`, two-way), `Format`, `TimeSelected`, `IsOpen` / `Opened` / `Closed`, text and font properties; wheels on the app's popup system. Optional: `MinimumTime` / `MaximumTime`, `MinuteInterval` (and `HourInterval`, `SecondInterval`), `WheelFormat` (`"HH:mm"`), `DefaultHighlightedTime` |
| `Entry` | `sk:SkUiEntry` | Prefix swap, drawn (the platform keyboard and input methods type into it): same `Text` (two-way), `TextChanged`, `Placeholder` / `PlaceholderColor`, `Keyboard`, `ReturnType`, `ReturnCommand` / `Completed`, `IsPassword`, `MaxLength`, `IsReadOnly`, `CursorPosition` / `SelectionLength`, `ClearButtonVisibility`, `IsSpellCheckEnabled`, `IsTextPredictionEnabled`, text and font properties. Default `FontSize` 16. Handler / mapper tweaks (no underline, caret color, `ios:Entry.CursorColor`) → `CaretColor`, `SelectionColor`, the look's `DefaultSkUiLook.TextFieldStyle` (`Underline`, `Outline`, `Filled`); platform content types → `AutofillHint` (`SkUiAutofillHints`). Keyboard-avoidance code for fields inside a drawn `SkUiScrollView` can go (the focused field scrolls above the keyboard). Toolkit `MaskedBehavior` (attaches to MAUI's `InputView` only) → `<sk:SkUiEntry.InputFormatters><sk:SkUiMaskFormatter Mask="(000) 000-0000" /></sk:SkUiEntry.InputFormatters>` (`X` → `&`, or `0` for digits; `FormatsAssignedText="True"` to mask bound text); `TextChanged` handlers that filter or cut the text → `SkUiFilteringFormatter` / `SkUiLengthLimitingFormatter` / `SkUiTextInputFormatter.Create`. Keep a hosted `Entry` only for an Android login form that needs the autofill service (not wired for drawn fields yet) |
| `Editor` | `sk:SkUiEditor` | As `Entry`, multiline: `AutoSize="TextChanges"` grows with the text; scrolls inside |
| `SearchBar` | `sk:SkUiSearchBar` | As `Entry`: `SearchCommand` / `SearchCommandParameter`, `SearchButtonPressed`, `CancelButtonColor`; plus `SearchIconColor` |
| `Stepper` | `sk:SkUiStepper` | Prefix swap: same `Value` (two-way), `Minimum`, `Maximum`, `Increment`, `ValueChanged`, MAUI's coercion; buttons only, so a label showing the value stays. `Orientation="Vertical"` stacks the buttons (+ on top); `DecrementText` / `IncrementText` with `FontFamily` / `FontSize` replace − / + (words, icon-font symbols); `CornerRadius` / `BorderWidth` / `BorderColor` replace handler or effect styling of its outline. A `Value` bound to an `int` / `decimal` through a converter → `x:TypeArguments="x:Int32"` (`SkUiStepper<T>`), drop the converter; `ValueChanged` handlers then take `SkUiValueChangedEventArgs<T>` |
| `GraphicsView`, `SKCanvasView` | A `SkUiView` subclass | See [custom-controls.md](custom-controls.md) |

`FFImageLoading`'s `CachedImage` → `sk:SkUiImage` (its circle / rounded / blur transformations have drawn equivalents in `Transformations`).

## Native islands

`WebView`, maps, media and third-party controls stay native inside the drawn tree:

```xml
<sk:SkUiMauiContentView>
    <WebView Source="{Binding HelpUrl}" />
</sk:SkUiMauiContentView>
```

The wrapper is measured and arranged like a drawn view; the native control sits above the surface. Drawn content cannot cover it (a drawn dropdown over a hosted view is hidden behind it). One control per wrapper.

## Every drawn view

- Bindable properties, bindings (`x:DataType` compiled bindings too), styles, triggers, `VisualStateManager` (states: `Normal`, `Disabled`, `PointerOver`, `Focused`, buttons `Pressed`, check box `IsChecked`, switch `On` / `Off`, radio `Checked` / `Unchecked`).
- `Shadow`, gradient `Background`, `Clip`, `Opacity`, transforms, `InputTransparent`, `IsVisible`, `IsEnabled`, `Margin`, `HorizontalOptions` / `VerticalOptions`, `WidthRequest` / `HeightRequest`, `FlowDirection`.
- `SemanticProperties` / `AutomationProperties` / `AutomationId` (screen readers, UI tests), `Focus()` / `Unfocus()`, `IsTabStop` / `TabIndex`.
- Fonts registered with `ConfigureFonts` by alias; `FontAutoScalingEnabled` follows the OS text size.
- Not drawn: `ImageBrush`.
- Drawn controls are drawn by the look (`SkUiLook`): close to, not identical with, native widgets. The default look is the same on every platform (an outlined picker field on Android too); if the app should keep each platform's appearance, set `builder.UseSkiaUi(options => options.Look = DefaultSkUiLook.ForCurrentPlatform())` and tell the user. Set colors explicitly where the design depends on them.

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

## Pickers and the app's popups

`sk:SkUiPicker` draws its field and its list but has no popup window of its own: an `ISkUiPopupPresenter` shows the list on whatever the app uses for popups. Before converting pickers, check what the app uses:

- **CommunityToolkit.Maui (`Popup`):** add the `SkiaUi.Maui.CommunityToolkit` package (same version as SkiaUi.Maui) and register it: `builder.UseMauiCommunityToolkit().UseSkiaUi().UseSkiaUiCommunityToolkit()`.
- **Another popup library, a popup navigation service of the app's:** write a presenter on it (one method: show `request.Content` at `request.Bounds`, close it when the token is cancelled, return when it is gone; a sketch in `SkUiPicker.md`).
- **Nothing:** pickers open MAUI's action sheet (plain item texts); tell the user and suggest a presenter.

A presenter that shows a picker UI of its own instead reads `SkUiListPopupRequest` (`Items`, `SelectedIndex`) and calls `request.Complete(index)`; for several choices and wheels it reads `SkUiMultiListPopupRequest` / `SkUiColumnsPopupRequest` and calls `SetSelection(indices)` as the user changes them.

Third-party pickers with no MAUI counterpart can become drawn ones: a multi-select combo box → `sk:SkUiPicker SelectionMode="Multiple"` with `SelectedItems`; a wheel picker of several columns → `sk:SkUiMultiColumnPicker` with `sk:SkUiPickerColumn`s (`ItemsSource`, `SelectedIndex` / `SelectedItem`, `Loop`). Their options map: `IsClearButtonVisible` → `IsClearButtonVisible`; `Placeholder` → `Placeholder`; `DropDownWidth` → `PopupWidth` (or `MinPopupWidth` / `MaxPopupWidth`); a separate member for the field's text → `SelectedItemDisplayBinding`; `DropDownMaxHeight` → `MaxPopupHeight`; `SelectionChangedCommand` → `SelectionChangedCommand`; `IsDropDownClosedOnSelection` → `IsPopupClosedOnSelection`; tokens → `SelectedItemsDisplay="Tokens"`; `SelectedItemTemplate` → `SelectedItemTemplate`; dropdown header / footer → `PopupHeaderTemplate` / `PopupFooterTemplate`; a selection confirmed by OK → `UpdateMode="WhenFinished"`; column headers → `SkUiPickerColumn.Header`; `EnableLooping` → `Loop`; blackout dates → `ItemEnabledBinding`. Ask before converting a combo box that is editable or filters as the user types: search waits for SkiaUi's drawn entry.
