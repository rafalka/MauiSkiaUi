# Core layer (`MauiSkiaUi.Core`)

Lightweight Skia nodes **without** MAUI `View` / `BindableObject` identity. Use them to compose complex controls or dense trees; place the tree in the MAUI-compatible surface via **`SkUiCoreHost`**.

**Contract:** [CoreRequirements.md](../design/CoreRequirements.md) (goals, FR-C*, fluent/INPC/MVVM Toolkit, Core-as-delegate sharing).

MAUI-compatible controls keep the existing names (`SkUiLabel`, `SkUiButton`, `SkUiAbsoluteLayout`, …).

## When to use which

| Layer | Types | Use for |
| --- | --- | --- |
| **MAUI-compatible** | `SkUiLabel`, `SkUiGrid`, … | XAML, styles, bindings, drop-in pages |
| **Core** | `SkUiCoreLabel`, `SkUiCoreAbsoluteLayout`, … | Building blocks inside custom controls; stress/dense lists |

## Types

| Type | Role |
| --- | --- |
| `ISkUiCoreNode` / `SkUiCoreNode` | Measure / arrange / paint / touch; fluent `Set*`; `INotifyPropertyChanged`; `PaintBackground`/`PaintOverlay` delegates + virtual `OnPaintContent`; `StartUpdating` / `EndUpdating`; `AnimationClock`; composite-time `Opacity` / `TranslationX/Y` / `Rotation` / `Scale` / `ClipToBounds` (transform-aware hit testing) and render-thread `AnimateAsync` |
| `SkUiCorePanel` | Multi-child base (attach, padding, paint, hit-test) |
| `SkUiCoreAbsoluteLayout` | Absolute (+ optional proportional) layout; MAUI-compatible proportional X/Y and child alignment |
| `SkUiCoreAbsoluteLayoutFlags` | Same idea as MAUI `AbsoluteLayoutFlags` |
| `SkUiCoreVerticalStackLayout` / `SkUiCoreHorizontalStackLayout` | Stack layouts (owned algorithms; no MAUI managers) |
| `SkUiCoreOverlayLayout` | Children share one slot (like `SkUiLayout`) |
| `SkUiCoreGrid` | Auto / absolute / star grid + per-track min/max; see [SkUiCoreGrid.md](SkUiCoreGrid.md) |
| `SkUiCoreTable` | Grid + row/column/cell backgrounds and span-aware separators; see [SkUiCoreTable.md](SkUiCoreTable.md) |
| `SkUiCoreContentView` / `SkUiCoreBorder` | Single-child host; border adds rounded chrome with per-corner `CornerRadius` |
| `SkUiCoreLabel` / `SkUiCoreButton` | Text (wrap/truncate via `LineBreakMode` or custom `LineBreaker`) and rounded tap button (`ICommand`) |
| `SkUiCoreTextLineBreaker` / `SkUiCoreTextLineBreakers` | Line-break delegate + stock MAUI-mode breakers for Core labels |
| `SkUiCoreToggleControl` / `CheckBox` / `RadioButton` / `Switch` | Boolean toggles |
| `SkUiCoreShape` / `Box` / `Ellipse` / `Line` | Drawing primitives |
| `SkUiCoreImage` / `SkUiCoreImageButton` | Decoded image (+ tap/tint); no MAUI `ImageSource` |
| `SkUiCoreActivityIndicator` | Indeterminate spinner on the host animation clock |
| `SkUiCoreHost` | `SkUiView` bridge that hosts one Core root |

**Not in Core yet:** `ScrollView` (use MAUI-compatible wrappers + `SkUiCoreHost` for scrolled Core trees).

Core types intentionally do **not** implement `IView` and are **not** accepted by `SkUiLayout.Children`. Mixing requires `SkUiCoreHost`.

## Example

```csharp
var root = new SkUiCoreVerticalStackLayout()
    .SetSpacing(8)
    .SetPadding(new Thickness(12));
root.Add(new SkUiCoreLabel().SetText("Title").SetFontSize(18));
root.Add(new SkUiCoreLabel()
    .SetText("Long cell copy that wraps or truncates.")
    .SetLineBreakMode(LineBreakMode.TailTruncation));
root.Add(new SkUiCoreButton().SetText("OK").SetClicked(() => { }));
root.Add(new SkUiCoreSwitch().SetIsChecked(true));

var host = new SkUiCoreHost().SetContent(root);
scroller.SetContent(host);
```

### Core label line breaking

`SkUiCoreLabel` measures and paints through `LineBreaker` (`SkUiCoreTextLineBreaker`).

- `SetLineBreakMode(LineBreakMode)` installs a stock breaker from `SkUiCoreTextLineBreakers` (same modes as `SkUiLabel`).
- `SetLineBreaker(...)` installs a custom policy and sets `LineBreakMode` to `null`.
- Default is `WordWrap`.
- Stock modes break on HarfBuzz-shaped widths; a custom breaker decides the logical lines and each line is then shaped.
- `FlowDirection` / `SetFlowDirection` on any Core node sets the layout direction (`MatchParent` inherits from the Core parent, then from `SkUiCoreHost.FlowDirection`); RTL mirrors child frames, and labels in `Auto` follow it.
- `TextRendering` / `SetTextRendering(SkUiTextRendering)`: `Auto` (fast path for plain Latin text, HarfBuzz otherwise), `Shaped`, `Simple` (never shapes, for dense plain text / numbers) — see [SkUiLabel.md](SkUiLabel.md).
- `TextDirection` / `SetTextDirection(SkUiTextDirection)` sets the paragraph direction (`Auto` = first strong character, default). Shaping, bidi and font fallback are the same as on [`SkUiLabel`](SkUiLabel.md).

## Diagnostics and automation

- **Visual tree:** Core nodes implement `IVisualTreeElement`, so MAUI's visual tree continues from `SkUiCoreHost` into the Core tree (Live Visual Tree, `GetVisualTreeDescendants()`, automation agents).
- **Notifications:** adds and removes are reported to `VisualDiagnostics` only when MAUI diagnostics are enabled (Debug). Release builds pay nothing.
- **`AutomationId` / `SetAutomationId`:** identify a node for automation, like MAUI's `AutomationId`.
- **`SkUiDiagnostics`:** locates drawn elements, which have no platform view:
  - `GetRootBounds` / `GetWindowBounds`: bounds after transforms and scroll offsets;
  - `HitTest` / `HitTestWindow`: the deepest element at a point;
  - `SimulateTap`: press and release through the surface, as a real touch.

## Stress comparison

Demo **Stress test** page: toggle **Core layer** to build the same two-column grid of buttons with Core nodes vs MAUI-compatible `SkUiGrid` + `SkUiButton`. **Animate** puts running `SkUiCoreActivityIndicator` / `SkUiActivityIndicator` cells in the second column on either layer. Compare Generate / Add / Render timings (see measured gap in [CoreRequirements.md](../design/CoreRequirements.md)).

## Roadmap (see CoreRequirements)

- Separate `MauiSkiaUi.Core` assembly without `Microsoft.Maui.Controls`
- Hand-rolled INPC on `SkUiCoreNode`; button/image-button commands are `ICommand` (`SkUiCoreCommand` helper)
- Fluent `Set*` as single apply path; CLR setters call `Set*`
- Shared painters via public **`SkUiLook`** / **`DefaultSkUiLook`** (`SkUiChrome` is a thin façade); **FR-18**
- Shared palette via public **`SkUiColorScheme`** (light/dark) and **`SkUiColors`** accessors; **FR-19**
- Neither look nor scheme is MAUI Style/VSM (FR-12)
- `SkUiLabel` / `SkUiButton` eventually delegate measure & paint to Core (still separate types; chrome already shared)
- Core **ScrollView** (deferred; Grid/Table shipped)
