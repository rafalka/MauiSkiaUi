# SkiaUi control documentation (NFR-5)

Per-control guides for public `SkUi*` types. For MAUI reimplementations, baseline behavior lives in the linked Microsoft docs; each page focuses on **SkiaUi differences and extensions**.

Shared pipelines: [LayoutSystem.md](../design/LayoutSystem.md) · [DrawingMechanism.md](../design/DrawingMechanism.md) · [ControlLook.md](../design/ControlLook.md) · [ColorScheme.md](../design/ColorScheme.md) · [EventMechanism.md](../design/EventMechanism.md) · [AnimationMechanism.md](../design/AnimationMechanism.md) · [ScrollingAndCollectionViews.md](../design/ScrollingAndCollectionViews.md) · [Accessibility.md](../design/Accessibility.md) (screen readers, keyboard focus, text size: [SkUiView.md](SkUiView.md#accessibility-and-keyboard))

## Core layer (lightweight, no MAUI View)

| Type | Doc | Notes |
| --- | --- | --- |
| `SkUiCoreNode` / `ISkUiCoreNode` | [SkUiCore.md](SkUiCore.md) | Public Core tree; fluent + INPC |
| `SkUiCorePanel` / stacks / absolute / overlay / **grid** / **table** / **scroll view** | [SkUiCore.md](SkUiCore.md), [SkUiCoreGrid.md](SkUiCoreGrid.md), [SkUiCoreTable.md](SkUiCoreTable.md) | Core-only layouts; `SkUiCoreScrollView` shares the `SkUiScrollView` engine; `SkUiCoreScrollBar` is the scroll bar of both scroll views |
| `SkUiCoreWrapLayout` / `SkUiCoreHorizontalShrinkLayout` / `SkUiCoreVerticalShrinkLayout` | [SkUiWrapLayout.md](SkUiWrapLayout.md), [SkUiShrinkLayout.md](SkUiShrinkLayout.md) | Same engines as the SkUi* twins (no Core flex layout) |
| `SkUiCoreLabel` / `Button` / toggles / `Slider` / `ProgressBar` / Image / shapes / … | [SkUiCore.md](SkUiCore.md) | Core primitives & basic controls |
| `SkUiCoreHost` | [SkUiCore.md](SkUiCore.md) | `SkUiView` bridge for Core roots |
| `SkUiWeakListener<T>` / `SkUiWeakEvent` | [SkUiCore.md](SkUiCore.md#listening-to-shared-sources-own-controls) | Weak listening to shared sources and weak app-wide events, for your own controls (both layers) |

## Basic controls

| Control | Doc | MAUI counterpart |
| --- | --- | --- |
| `SkUiView` | [SkUiView.md](SkUiView.md) | — (base) |
| `SkUiLabel` | [SkUiLabel.md](SkUiLabel.md) | Label |
| `SkUiButton` | [SkUiButton.md](SkUiButton.md) | Button |
| `SkUiImage` | [SkUiImage.md](SkUiImage.md) | Image |
| `SkUiImageButton` | [SkUiImageButton.md](SkUiImageButton.md) | ImageButton |
| `SkUiActivityIndicator` | [SkUiActivityIndicator.md](SkUiActivityIndicator.md) | ActivityIndicator |
| `SkUiSwitch` | [SkUiSwitch.md](SkUiSwitch.md) | Switch |
| `SkUiCheckBox` | [SkUiCheckBox.md](SkUiCheckBox.md) | CheckBox |
| `SkUiRadioButton` | [SkUiRadioButton.md](SkUiRadioButton.md) | RadioButton (`Content`, `ControlTemplate`; MAUI's `RadioButtonGroup` works on drawn layouts) |
| `SkUiSlider` | [SkUiSlider.md](SkUiSlider.md) | Slider (plus vertical) |
| `SkUiProgressBar` | [SkUiProgressBar.md](SkUiProgressBar.md) | ProgressBar (plus indeterminate) |
| `SkUiToggleControl` | [SkUiToggleControl.md](SkUiToggleControl.md) | — (abstract) |

## Layouts

| Control | Doc | MAUI counterpart |
| --- | --- | --- |
| `SkUiContentView` | [SkUiContentView.md](SkUiContentView.md) | ContentView |
| `SkUiContentPresenter` | [SkUiContentPresenter.md](SkUiContentPresenter.md) | ContentPresenter (in drawn `ControlTemplate`s) |
| `SkUiMauiContentView` | [SkUiMauiContentView.md](SkUiMauiContentView.md) | — (native host) |
| `SkUiBorder` | [SkUiBorder.md](SkUiBorder.md) | Border |
| `SkUiLayout` | [SkUiLayout.md](SkUiLayout.md) | — (overlay) |
| `SkUiGrid` | [SkUiGrid.md](SkUiGrid.md) | Grid |
| `SkUiVerticalStackLayout` | [SkUiVerticalStackLayout.md](SkUiVerticalStackLayout.md) | VerticalStackLayout |
| `SkUiHorizontalStackLayout` | [SkUiHorizontalStackLayout.md](SkUiHorizontalStackLayout.md) | HorizontalStackLayout |
| `SkUiAbsoluteLayout` | [SkUiAbsoluteLayout.md](SkUiAbsoluteLayout.md) | AbsoluteLayout |
| `SkUiFlexLayout` | [SkUiFlexLayout.md](SkUiFlexLayout.md) | FlexLayout |
| `SkUiWrapLayout` | [SkUiWrapLayout.md](SkUiWrapLayout.md) | — (chips / tags) |
| `SkUiHorizontalShrinkLayout` / `SkUiVerticalShrinkLayout` | [SkUiShrinkLayout.md](SkUiShrinkLayout.md) | — (stacks whose children shrink to fit) |

## Graphics

| Control | Doc | MAUI counterpart |
| --- | --- | --- |
| `SkUiShape` | [SkUiShape.md](SkUiShape.md) | Shape (abstract): brushes, stroke model, `Aspect` |
| `SkUiBox` | [SkUiBox.md](SkUiBox.md) | BoxView |
| `SkUiEllipse` | [SkUiEllipse.md](SkUiEllipse.md) | Ellipse |
| `SkUiLine` | [SkUiLine.md](SkUiLine.md) | Line |
| `SkUiRectangle` | [SkUiRectangle.md](SkUiRectangle.md) | Rectangle |
| `SkUiRoundRectangle` | [SkUiRoundRectangle.md](SkUiRoundRectangle.md) | RoundRectangle |
| `SkUiPath` | [SkUiPath.md](SkUiPath.md) | Path |
| `SkUiPolygon` | [SkUiPolygon.md](SkUiPolygon.md) | Polygon |
| `SkUiPolyline` | [SkUiPolyline.md](SkUiPolyline.md) | Polyline |

## Scrolling

| Control | Doc | MAUI counterpart |
| --- | --- | --- |
| `SkUiScrollView` | [SkUiScrollView.md](SkUiScrollView.md) | ScrollView |
