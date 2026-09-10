# SkiaUi control documentation (NFR-5)

Per-control guides for public `SkUi*` types. For MAUI reimplementations, baseline behavior lives in the linked Microsoft docs; each page focuses on **SkiaUi differences and extensions**.

Shared pipelines: [LayoutSystem.md](../LayoutSystem.md) · [DrawingMechanism.md](../DrawingMechanism.md) · [EventMechanism.md](../EventMechanism.md) · [AnimationMechanism.md](../AnimationMechanism.md) · [ScrollingAndCollectionViews.md](../ScrollingAndCollectionViews.md)

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
| `SkUiRadioButton` | [SkUiRadioButton.md](SkUiRadioButton.md) | RadioButton |
| `SkUiToggleControl` | [SkUiToggleControl.md](SkUiToggleControl.md) | — (abstract) |

## Layouts

| Control | Doc | MAUI counterpart |
| --- | --- | --- |
| `SkUiContentView` | [SkUiContentView.md](SkUiContentView.md) | ContentView |
| `SkUiMauiContentView` | [SkUiMauiContentView.md](SkUiMauiContentView.md) | — (native host) |
| `SkUiBorder` | [SkUiBorder.md](SkUiBorder.md) | Border |
| `SkUiLayout` | [SkUiLayout.md](SkUiLayout.md) | — (overlay) |
| `SkUiGrid` | [SkUiGrid.md](SkUiGrid.md) | Grid |
| `SkUiVerticalStackLayout` | [SkUiVerticalStackLayout.md](SkUiVerticalStackLayout.md) | VerticalStackLayout |
| `SkUiHorizontalStackLayout` | [SkUiHorizontalStackLayout.md](SkUiHorizontalStackLayout.md) | HorizontalStackLayout |
| `SkUiAbsoluteLayout` | [SkUiAbsoluteLayout.md](SkUiAbsoluteLayout.md) | AbsoluteLayout |

`SkUiFlexLayout` is not implemented yet.

## Graphics

| Control | Doc | MAUI counterpart |
| --- | --- | --- |
| `SkUiShape` | [SkUiShape.md](SkUiShape.md) | — (abstract) |
| `SkUiBox` | [SkUiBox.md](SkUiBox.md) | BoxView |
| `SkUiEllipse` | [SkUiEllipse.md](SkUiEllipse.md) | Ellipse |
| `SkUiLine` | [SkUiLine.md](SkUiLine.md) | Line |

## Scrolling

| Control | Doc | MAUI counterpart |
| --- | --- | --- |
| `SkUiScrollView` | [SkUiScrollView.md](SkUiScrollView.md) | ScrollView |
