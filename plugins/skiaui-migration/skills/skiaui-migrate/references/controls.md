# MAUI → SkiaUi controls

Namespace: `xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi"`. Drawn controls use MAUI's property names; the table lists only what differs.

## Hosts and layouts

| MAUI | SkiaUi | Notes |
| --- | --- | --- |
| `ContentView` | `sk:SkUiContentView` | Surface root (one child) or a hosted container; base class for drawn custom controls |
| `Grid` | `sk:SkUiGrid` | `RowDefinitions`, `ColumnDefinitions`, spacing, `Grid.Row` / `Grid.Column` / spans as in MAUI |
| `VerticalStackLayout` | `sk:SkUiVerticalStackLayout` | |
| `HorizontalStackLayout` | `sk:SkUiHorizontalStackLayout` | |
| `StackLayout` | `sk:SkUiVerticalStackLayout` / `sk:SkUiHorizontalStackLayout` | Choose by `Orientation` (default vertical); drop `Orientation`; keep `Spacing` |
| `AbsoluteLayout` | `sk:SkUiAbsoluteLayout` | MAUI's `AbsoluteLayout.LayoutBounds` / `LayoutFlags` |
| `FlexLayout` | `sk:SkUiFlexLayout` | MAUI's attached `FlexLayout.*` properties |
| — | `sk:SkUiWrapLayout` | Chips and tags (wrapping rows) |
| — | `sk:SkUiLayout` | Overlay: children share one slot |
| `ScrollView` | `sk:SkUiScrollView` | Same API (`Orientation`, `ScrollToAsync`, `Scrolled`, scroll bar visibility); plus `SnapPointsType`, `Overscroll` |
| `Border` | `sk:SkUiBorder` | `StrokeShape` (string `RoundRectangle 8` or a shape element), `Stroke`, `StrokeThickness`, dashes |
| `Frame` | `sk:SkUiBorder` | `CornerRadius="8"` → `StrokeShape="RoundRectangle 8"`; `BorderColor` → `Stroke`; `HasShadow="True"` → `<sk:SkUiBorder.Shadow><Shadow .../></sk:SkUiBorder.Shadow>`; Frame's default `Padding` is 20 |
| `ContentPresenter` | `sk:SkUiContentPresenter` | Only inside a `SkUiRadioButton` `ControlTemplate` |

`IsClippedToBounds` → `ClipToBounds` (leaf controls clip by default, layouts do not). Attached properties stay MAUI's (`Grid.Row`, `AbsoluteLayout.LayoutBounds`, `FlexLayout.Grow`, `SemanticProperties.*`, `AutomationProperties.*`, `RadioButtonGroup.GroupName`, `VisualStateManager.VisualStateGroups`).

## Controls

| MAUI | SkiaUi | Notes |
| --- | --- | --- |
| `Label` | `sk:SkUiLabel` | Text properties, `MaxLines`, `LineBreakMode`, `FormattedText` / `Span` (span `TapGestureRecognizer`s work), `TextType="Html"` |
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

`Entry`, `Editor`, `SearchBar`, `Picker`, `DatePicker`, `TimePicker`, `WebView`, maps, media and third-party controls stay native inside the drawn tree:

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
