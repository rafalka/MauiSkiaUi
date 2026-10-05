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
| Bindings, `x:DataType`, converters, triggers, `VisualStateManager` | Gestures: `TapGestureRecognizer` keeps working; other recognizers and `TouchBehavior` become gesture events |
| Resources, `StaticResource` / `DynamicResource`, `AppThemeBinding` | Native-only controls (Entry, Editor, pickers, WebView) are wrapped in `SkUiMauiContentView` |
| Fonts from `ConfigureFonts`, `MauiImage` / `FontImageSource` images | Custom handlers, renderers and effects do not apply to drawn controls |
| `SemanticProperties`, `AutomationProperties`, `AutomationId` | |

The property names are MAUI's: the parity work ([ImplementationPlan.md](design/ImplementationPlan.md#maui-parity-at-a-glance)) adds the MAUI API to each drawn control, so most XAML converts by changing the element prefix.

## Which screens to port first

Drawn controls pay off where a screen holds **many views**. Every native MAUI view has a handler and a platform view; a drawn tree has one surface. On the stress page, 1,000 buttons are ready 12× (SkUi\*) to 26× (Core) faster than native MAUI on a mid-range Android phone ([README](../README.md#stress-results-device)).

Good first candidates:
- dashboards, cards, detail pages and forms with deep `Grid` / `StackLayout` nesting;
- screens that are slow to open, or where adding views to the page shows up in traces;
- repeated rows inside a `ScrollView` (not a `CollectionView`: see [Not available yet](#not-available-yet)).

Leave for later: screens built around a `CollectionView` with many items, `SwipeView` rows, or third-party controls (charts, calendars, data grids) that would have to sit on top of the drawn surface.

## Setup

```bash
dotnet add package SkiaUi.Maui
```

```csharp
// MauiProgram.cs
builder.UseMauiApp<App>().UseSkiaUi();
```

```xml
xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi"
```

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
    │               └── Entry                          real native Entry
    └── Button                   ← MAUI, outside the surface
```

Rules:
- **Only drawn views go inside a drawn tree.** A plain `Label` or `Entry` inside `SkUiGrid` throws "Only SkiaUi children are supported". Wrap native controls in `SkUiMauiContentView`.
- **One surface per screen region.** Prefer one root for the page body (usually around the scroll view) over many small roots: each root is a platform view with its own GPU surface.
- **Drawn scrolling beats native scrolling.** Put the `SkUiScrollView` inside the root, not the root inside a MAUI `ScrollView`. Drawn surfaces inside a native `ScrollView` work (gestures are coordinated), but the drawn scroller is what keeps scrolling smooth.
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
                <sk:SkUiMauiContentView>
                    <Entry Text="{Binding Note}" Placeholder="Note" />
                </sk:SkUiMauiContentView>
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
| `ContentView` | `SkUiContentView` | Also the base class for drawn custom controls |
| `Grid` | `SkUiGrid` | |
| `VerticalStackLayout` / `HorizontalStackLayout` | `SkUiVerticalStackLayout` / `SkUiHorizontalStackLayout` | |
| `StackLayout` | `SkUiVerticalStackLayout` / `SkUiHorizontalStackLayout` | Pick by `Orientation`; keep `Spacing` |
| `AbsoluteLayout` | `SkUiAbsoluteLayout` | |
| `FlexLayout` | `SkUiFlexLayout` | Also `SkUiWrapLayout` for chips and tags |
| Toolkit `StateContainer` / `StateView` (attached properties) | `SkUiStateContainer` / `SkUiStateView` | Same properties and `ChangeStateWithAnimation` overloads, on any drawn layout ([SkUiStateContainer.md](controls/SkUiStateContainer.md)) |
| Toolkit `Expander` | `SkUiExpander` | Same properties and `ExpandedChanged`; `Header` / `Content` must be drawn views. `ExpandDirection` → `SkUiExpandDirection`, `ExpandedChangedEventArgs` → `SkUiExpandedChangedEventArgs`; drop `HandleHeaderTapped` (a native list view workaround). `IsExpanded` is two-way by default. Plus `ContentTemplate`, `LazyContentExpansion`, `AnimationLength` / `AnimationEasing` ([SkUiExpander.md](controls/SkUiExpander.md)) |
| `ScrollView` | `SkUiScrollView` | Plus snap points and overscroll |
| `Border` | `SkUiBorder` | `StrokeShape`, brush strokes, dashes |
| `Frame` (obsolete) | `SkUiBorder` | `CornerRadius` → `StrokeShape="RoundRectangle N"`, `HasShadow` → `Shadow`, `BorderColor` → `Stroke` |
| `Label` | `SkUiLabel` | Spans, span taps, `TextType="Html"` |
| `Button` | `SkUiButton` | `ImageSource`, `ContentLayout` |
| `Image` | `SkUiImage` | Shared memory and disk cache, transformations, placeholders |
| `ImageButton` | `SkUiImageButton` | |
| `BoxView` | `SkUiBox` | |
| `Ellipse`, `Line`, `Rectangle`, `RoundRectangle`, `Path`, `Polygon`, `Polyline` | `SkUiEllipse`, `SkUiLine`, … | |
| `CheckBox`, `Switch`, `RadioButton` | `SkUiCheckBox`, `SkUiSwitch`, `SkUiRadioButton` | Optional third state; radio `Content` and `ControlTemplate` |
| `Slider`, `ProgressBar`, `ActivityIndicator` | `SkUiSlider`, `SkUiProgressBar`, `SkUiActivityIndicator` | Vertical slider, indeterminate progress |
| `GraphicsView`, `SKCanvasView` | A `SkUiView` subclass | Override `MeasureContent` and `OnPaintContent(SKCanvas)` |
| `Entry`, `Editor`, `SearchBar`, `Picker`, `DatePicker`, `TimePicker`, `WebView`, maps, media | Wrapped in `SkUiMauiContentView` | Native by design |
| `Layout.IsClippedToBounds` | `ClipToBounds` | Leaves clip by default, layouts do not |

Per-control differences: [docs/controls/](controls/README.md).

### 3. Wrap native-only controls

```xml
<sk:SkUiMauiContentView>
    <Picker ItemsSource="{Binding Stores}" SelectedItem="{Binding Store}" />
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
- Colors, brushes, `AppThemeBinding`, `OnPlatform`, `OnIdiom`, converters and resource dictionaries are MAUI's and need no change.
- **Look.** Drawn controls are drawn by a look (`SkUiLook`) with platform-inspired defaults, not by the native widgets. Buttons, switches and check boxes therefore look close to, but not exactly like, the platform's. Set the colors you care about explicitly, or customise the look ([ControlLook.md](design/ControlLook.md), samples app: Customisation).
- **Fonts** registered with `ConfigureFonts` work by alias. Text follows the OS text size (`FontAutoScalingEnabled`), as MAUI's does.

### 5. Convert gestures and touch behaviors

`TapGestureRecognizer` works on drawn views as it does on MAUI (single and double taps, `Command` / `CommandParameter`, `Tapped`, `GetPosition`), so tap XAML needs no change. The shorter drawn form is also available:

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

Other gesture input needs converting. A drawn view that has it writes one `Trace` line on its first press, naming what is ignored and what to use.

| MAUI / toolkit | SkiaUi |
| --- | --- |
| `TapGestureRecognizer` | Works as is, or `Tapped` / `TappedCommand` + `TappedCommandParameter` |
| `TapGestureRecognizer NumberOfTapsRequired="2"` | Works as is, or `DoubleTapped` / `DoubleTappedCommand` |
| `TapGestureRecognizer Buttons="Secondary"` | Not available (drawn taps are primary) |
| `SwipeGestureRecognizer Direction="Left"` | `SwipeDirections="Left"` + `Swiped` / `SwipedCommand` (the parameter defaults to the direction). Different commands per direction: one handler that switches on `e.Direction`, or several `SkUiSwipeGestureRecognizer`s in `Gestures` (they also take `Threshold`) |
| `PanGestureRecognizer` | `PanUpdated`: MAUI's `GestureStatus` and `TotalX` / `TotalY`, so handlers port almost unchanged; plus the release velocity on `Completed`. `PanAxis` limits the axis |
| `PinchGestureRecognizer` | `PinchUpdated`: `Status` and `Scale` (change since the last update) as on MAUI, plus `TotalScale` and rotation. `Origin` is the pointers' midpoint in DIPs relative to the view, where MAUI's `ScaleOrigin` is a 0–1 fraction of its size |
| `PointerGestureRecognizer` | `IsPointerOver` and the `PointerOver` visual state for hover; `SkUiPointerGestureRecognizer` in `Gestures` for raw pointers |
| Toolkit `TouchBehavior` `Command` / `LongPressCommand` | `TappedCommand` / `LongPressedCommand` (+ parameters) |
| Toolkit `TouchBehavior` press scale / opacity / color | `ShowsPressEffect="True"` (the look's dim or ripple), or a `Pressed` visual state with your own setters |
| A tap or pan recognizer that only swallows touches | Often not needed: drawn layouts, labels and images without gestures are transparent to touches, and what is under them gets the touch. Where blocking is the point (a backdrop under a drawn popover), keep an empty `TapGestureRecognizer` or give the view an empty `Tapped` handler |
| `InputTransparent`, `CascadeInputTransparent` | Same meaning |

How drawn gestures resolve: every press runs a **gesture arena** over the views under the finger and their ancestors. The innermost tap wins, a scroll or swipe that moves past the touch slop wins over taps, and a long press wins once it fires. A tappable row with a check box inside therefore behaves the same on every platform, without per-platform workarounds. See [EventMechanism.md](design/EventMechanism.md).

Platform behaviors (any `PlatformBehavior`, such as `TouchBehavior`) and effects attach to a native view, which drawn views do not have. Plain behaviors that only use the view's events and properties (for example `EventToCommandBehavior`) keep working.

### 6. Port custom controls

| What the custom control is | Port to |
| --- | --- |
| A `ContentView` composing other views in XAML or code | A `SkUiContentView` subclass composing drawn views (`x:Class` XAML or C#). Bindable properties carry over |
| A control drawn with `GraphicsView` / `SKCanvasView` | A `SkUiView` subclass: `MeasureContent` for its size, `OnPaintContent(SKCanvas)` for drawing, gesture events for input |
| A control with a custom handler or renderer, an `Effect`, or platform code | Redraw it as above, or keep it native inside `SkUiMauiContentView`. Common effects have drawn equivalents: rounded corners → `SkUiBorder StrokeShape` or `Clip`, shadows → `Shadow` |
| A templated control (`ControlTemplate`) | Only `SkUiRadioButton` takes a drawn `ControlTemplate` today; compose the parts in a `SkUiContentView` subclass instead. `TemplateBinding` does not reach drawn controls: bind with `RelativeSource AncestorType` |
| Many small repeated parts (cells, tiles, chips) where allocation matters | Core nodes (`SkUiCore*`) under a `SkUiCoreHost`: no `BindableObject` per part ([SkUiCore.md](controls/SkUiCore.md)) |

Custom handlers registered for MAUI types (`Label`, `Button`, …) do not affect the drawn controls. Check what each one did (for example removing Android's button padding or the iOS text field border) and set the equivalent properties on the drawn control.

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
- **UI tests.** `AutomationId` is exposed to UI test frameworks through the accessibility tree. In unit tests, `SkUiDiagnostics.SimulateTap(element)` and `HitTest` drive drawn input without a device.
- **Defer what is not shown.** Other tabs and hidden or collapsed panes can wait: wrap them in `SkUiContentView ContentLoading="WhenShown"` with a `ContentTemplate` ([SkUiContentView.md](controls/SkUiContentView.md#loading-content-when-shown)).
- **Measure.** Time the page from navigation to first frame before and after; [Performance.md](Performance.md) lists what costs most while scrolling (shadows, gradients and clips in long lists).

## Not available yet

| MAUI | Status | Meanwhile |
| --- | --- | --- |
| `CollectionView` | Planned (Phase B: virtualized `SkUiCollectionView`) | Keep the MAUI `CollectionView` with native item templates, or, for up to a few hundred items, a drawn stack inside `SkUiScrollView` |
| `SwipeView` | Planned (C1) | `Swiped` / `PanUpdated` on the row for simple cases, or keep the list native |
| `RefreshView` | Planned (C2) | A MAUI `RefreshView` around the surface root: the drawn scroller hands the drag to native parents at its top edge, as inside a native `ScrollView` (this combination is not covered by tests yet) |
| `CarouselView`, `IndicatorView` | Planned (D1) | `SkUiScrollView Orientation="Horizontal"` with `SnapPointsType="MandatorySingle"` |
| `Stepper` | Planned (D2) | Two `SkUiButton`s |
| `ControlTemplate` on content views | `SkUiRadioButton` only | Compose a `SkUiContentView` subclass |
| Drag and drop, tooltips, context flyouts | Not planned | Keep native |
| `ListView`, `TableView`, cells, `Frame` | Obsolete in MAUI; not planned | `CollectionView` (when available), `SkUiBorder` |
| Shell, pages, navigation | Out of scope | Stay MAUI |
| Third-party controls (charts, calendars, data grids, signature pads) | Native | Host in `SkUiMauiContentView`, or keep them outside the surface |

Swipe, pan and pinch *recognizers* may later run on drawn views like taps do (P11b); until then use the gesture events above.

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
