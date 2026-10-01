# SkUiFlexLayout

A drawn flex layout with MAUI's `FlexLayout` API: direction, wrapping, justification and alignment, and per-child grow, shrink, basis, order and self-alignment.

**MAUI counterpart:** [`FlexLayout`](https://learn.microsoft.com/dotnet/maui/user-interface/layouts/flexlayout)

## How it works

`SkUiFlexLayout` implements MAUI's `IFlexLayout` and is laid out by MAUI's public `FlexLayoutManager`, like the other SkUi layouts reuse MAUI's managers. MAUI's flex engine behind the manager (`Microsoft.Maui.Layouts.Flex.Item`) is internal, so SkiaUi carries a port of it (from dotnet/maui 10.0.101, MIT). The port keeps the algorithm unchanged and removes its per-pass allocations.

- Each child has one engine item. Items are rebuilt in child order when children change, and refreshed from the attached properties on every pass.
- Children are measured only in the measure pass (the engine's self-sizing callback). The arrange pass reuses their desired sizes, as in MAUI. The callback includes MAUI's fix for items measured wider than they are arranged (dotnet/maui#27520).
- Under an infinite constraint (inside a scroll view), shrinking and stretching are skipped on that axis, as in MAUI.
- RTL mirrors the frames like every drawn layout.

A headless test compares the frames with MAUI's own `FlexLayout` for every combination of `Direction`, `Wrap`, `JustifyContent`, `AlignItems` and `AlignContent`, with and without per-child properties, finite and infinite constraints.

## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10). Invalid values: `Set*` throws; XAML, bindings, styles and the property setter ignore them with a logged warning, as MAUI does.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).


## How to use

```xml
<sk:SkUiFlexLayout Wrap="Wrap" JustifyContent="SpaceBetween" AlignItems="Center">
  <sk:SkUiLabel Text="Grows" sk:SkUiFlexLayout.Grow="1" />
  <sk:SkUiLabel Text="Fixed" sk:SkUiFlexLayout.Shrink="0" />
  <sk:SkUiLabel Text="Quarter" sk:SkUiFlexLayout.Basis="25%" />
  <sk:SkUiLabel Text="First" sk:SkUiFlexLayout.Order="-1" />
</sk:SkUiFlexLayout>
```

MAUI's own attached syntax (`FlexLayout.Grow="1"`) works too: the properties are the same objects.

```csharp
var flex = new SkUiFlexLayout { Direction = FlexDirection.Column, AlignItems = FlexAlignItems.Start };
SkUiFlexLayout.SetGrow(child, 1);
SkUiFlexLayout.SetAlignSelf(child, FlexAlignSelf.End);
```

## Key properties

| Property | Default | Notes |
| --- | --- | --- |
| `Direction` | `Row` | `Row`, `RowReverse`, `Column`, `ColumnReverse` |
| `Wrap` | `NoWrap` | `Wrap`, `Reverse` |
| `JustifyContent` | `Start` | Main-axis free space: `Center`, `End`, `SpaceBetween`, `SpaceAround`, `SpaceEvenly` |
| `AlignItems` | `Stretch` | Cross-axis alignment in a line |
| `AlignContent` | `Stretch` | Distribution of wrapped lines |
| `Position` | `Relative` | MAUI API parity; no effect on the layout (as in MAUI) |
| Attached `Order`, `Grow`, `Shrink`, `AlignSelf`, `Basis` | 0, 0, 1, `Auto`, `Auto` | MAUI's `FlexLayout.*Property` objects, exposed on `SkUiFlexLayout` too |

Each container property has a direct setter (`SetDirection`, `SetWrap`, ...). `GetFlexFrame(child)` returns a child's engine frame.

## Differences from MAUI FlexLayout

- Children must be `ISkUiView`.
- MAUI shares a nested `FlexLayout`'s engine item with its parent. A nested `SkUiFlexLayout` is an ordinary child that measures itself.
- MAUI applies a 0.1 DIP wrap tolerance only on Android and Windows. Drawn layouts apply it on every platform, so a row that fits on one platform fits on all.
- An explicit `WidthRequest` / `HeightRequest` on a child still caps its arranged frame, even when grow or basis give it more space (as in MAUI).

## Related

Gallery: `FlexLayoutDemoPage` (side by side with MAUI's `FlexLayout`). For simple chip rows with fixed gaps see [SkUiWrapLayout](SkUiWrapLayout.md).
