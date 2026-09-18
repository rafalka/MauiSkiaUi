# Core layer (`MauiSkiaUi.Core`)

Lightweight Skia nodes **without** MAUI `View` / `BindableObject` identity. Use them to compose complex controls or dense trees; place the tree in the MAUI-compatible surface via **`SkUiCoreHost`**.

**Contract:** [CoreRequirements.md](../../CoreRequirements.md) (goals, FR-C*, fluent/INPC/MVVM Toolkit, Core-as-delegate sharing).

MAUI-compatible controls keep the existing names (`SkUiLabel`, `SkUiButton`, `SkUiAbsoluteLayout`, …).

## When to use which

| Layer | Types | Use for |
| --- | --- | --- |
| **MAUI-compatible** | `SkUiLabel`, `SkUiGrid`, … | XAML, styles, bindings, drop-in pages |
| **Core** | `SkUiCoreLabel`, `SkUiCoreAbsoluteLayout`, … | Building blocks inside custom controls; stress/dense lists |

## Types

| Type | Role |
| --- | --- |
| `ISkUiCoreNode` / `SkUiCoreNode` | Measure / arrange / paint / touch; fluent `Set*`; `INotifyPropertyChanged`; `PaintBackground`/`PaintOverlay` delegates + virtual `OnPaintContent`; `StartUpdating` / `EndUpdating`; `AnimationClock` |
| `SkUiCorePanel` | Multi-child base (attach, padding, paint, hit-test) |
| `SkUiCoreAbsoluteLayout` | Absolute (+ optional proportional) layout; MAUI-compatible proportional X/Y and child alignment |
| `SkUiCoreAbsoluteLayoutFlags` | Same idea as MAUI `AbsoluteLayoutFlags` |
| `SkUiCoreVerticalStackLayout` / `SkUiCoreHorizontalStackLayout` | Stack layouts (owned algorithms; no MAUI managers) |
| `SkUiCoreOverlayLayout` | Children share one slot (like `SkUiLayout`) |
| `SkUiCoreContentView` / `SkUiCoreBorder` | Single-child host; border adds rounded chrome |
| `SkUiCoreLabel` / `SkUiCoreButton` | Text and rounded tap button (`ICommand`) |
| `SkUiCoreToggleControl` / `CheckBox` / `RadioButton` / `Switch` | Boolean toggles |
| `SkUiCoreShape` / `Box` / `Ellipse` / `Line` | Drawing primitives |
| `SkUiCoreImage` / `SkUiCoreImageButton` | Decoded image (+ tap/tint); no MAUI `ImageSource` |
| `SkUiCoreActivityIndicator` | Indeterminate spinner on the host animation clock |
| `SkUiCoreHost` | `SkUiView` bridge that hosts one Core root |

**Not in Core yet:** `Grid`, `ScrollView` (use MAUI-compatible wrappers + `SkUiCoreHost` for scrolled Core trees).

Core types intentionally do **not** implement `IView` and are **not** accepted by `SkUiLayout.Children`. Mixing requires `SkUiCoreHost`.

## Example

```csharp
var root = new SkUiCoreVerticalStackLayout()
    .SetSpacing(8)
    .SetPadding(new Thickness(12));
root.Add(new SkUiCoreLabel().SetText("Title").SetFontSize(18));
root.Add(new SkUiCoreButton().SetText("OK").SetClicked(() => { }));
root.Add(new SkUiCoreSwitch().SetIsChecked(true));

var host = new SkUiCoreHost().SetContent(root);
scroller.SetContent(host);
```

## Stress comparison

Demo **Stress test** page: toggle **Core layer** to build the same two-column absolute button list with Core nodes vs MAUI-compatible `SkUiAbsoluteLayout` + `SkUiButton`. **Animate** puts running `SkUiCoreActivityIndicator` / `SkUiActivityIndicator` cells in the second column on either layer. Compare Generate / Add / Render timings (see measured gap in [CoreRequirements.md](../../CoreRequirements.md)).

## Roadmap (see CoreRequirements)

- Separate `MauiSkiaUi.Core` assembly without `Microsoft.Maui.Controls`
- Hand-rolled INPC on `SkUiCoreNode`; button/image-button commands are `ICommand` (`SkUiCoreCommand` helper)
- Fluent `Set*` as single apply path; CLR setters call `Set*`
- Shared painters via public **`SkUiLook`** / **`DefaultSkUiLook`** (`SkUiChrome` is a thin façade); **FR-18**
- Shared palette via public **`SkUiColorScheme`** (light/dark) and **`SkUiColors`** accessors; **FR-19**
- Neither look nor scheme is MAUI Style/VSM (FR-12)
- `SkUiLabel` / `SkUiButton` eventually delegate measure & paint to Core (still separate types; chrome already shared)
- Core **Grid** / **ScrollView** (deferred)
