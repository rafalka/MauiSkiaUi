# Shrink layouts

`SkUiHorizontalShrinkLayout`, `SkUiVerticalShrinkLayout` and their Core twins `SkUiCoreHorizontalShrinkLayout`, `SkUiCoreVerticalShrinkLayout`: stacks that fit their main axis.

**MAUI counterpart:** none (a `HorizontalStackLayout` / `VerticalStackLayout` whose children can shrink).

## How it works

One engine serves both layers and both axes (`SkUiShrinkEngine`).

1. Every visible child is measured with an unconstrained main axis (its natural size).
2. If the children and the spacing fit, the layout is a plain stack: like `HorizontalStackLayout` / `VerticalStackLayout`, it takes only the main-axis space its children need (with `Start` / `Center` / `End` alignment; `Fill` still fills its slot).
3. Otherwise the overflow is shared among the children by their **shrink factor** (`SkUiShrinkFactor`):

   | Factor | Weight (share of the overflow) |
   | --- | --- |
   | `None` (default) | 0: the child keeps its natural size |
   | `Auto` | Its natural size, but only if that is above the average of the visible children (the space left after spacing, divided by their number); otherwise 0 |
   | A number `f` > 0 | `f` × natural size (as CSS `flex-shrink`): 1 shrinks in proportion to size, 2 gives up twice as much relative to its size |

   A child whose share would take it below its minimum size (`MinimumWidthRequest` / `MinimumHeightRequest` plus margins, or 0) stops there, and the rest of the overflow is shared again among the others.
4. Shrunk children are measured again at their new size, so a label truncates or wraps.
5. Each child gets a slot of its main-axis size across the full cross axis, and aligns inside it.

With `Auto`, a longer label keeps more of the space than a shorter one, and small children (an icon, a short caption) are left alone. `Auto` and numbers can be mixed in one layout.

If the children that can shrink reach their minimum sizes, the content overflows: the layout clips its children (both layers).

The layout never hands out leftover space. For weighted growth use [SkUiFlexLayout](SkUiFlexLayout.md) `Grow` (SkUi*) or grid star columns (Core).

Arranging in a different size than measured solves again for that size.

## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** bindables call the direct setter. Direct setters **do not** write back to the bindable store (intentional FR-10 desync). Prefer one update path per property.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).


## How to use

```xml
<sk:SkUiHorizontalShrinkLayout Spacing="10">
  <sk:SkUiImage Source="avatar.png" WidthRequest="32" HeightRequest="32" />
  <sk:SkUiLabel Text="{Binding Name}" LineBreakMode="TailTruncation" sk:SkUiShrinkLayout.Shrink="Auto" />
  <sk:SkUiLabel Text="{Binding Details}" LineBreakMode="TailTruncation" sk:SkUiShrinkLayout.Shrink="2" />
  <sk:SkUiLabel Text="{Binding Status}" />
</sk:SkUiHorizontalShrinkLayout>

<sk:SkUiVerticalShrinkLayout Spacing="5">
  <sk:SkUiButton Text="Header" />
  <sk:SkUiScrollView sk:SkUiShrinkLayout.Shrink="Auto"> ... </sk:SkUiScrollView>
  <sk:SkUiButton Text="Footer" />
</sk:SkUiVerticalShrinkLayout>
```

```csharp
var row = new SkUiCoreHorizontalShrinkLayout();
row.SetSpacing(10).Add(icon).Add(title, SkUiShrinkFactor.Auto).Add(details, 2).Add(badge);
row.SetShrink(title, SkUiShrinkFactor.None);
subtitle.SetValue(SkUiCoreShrinkLayout.ShrinkProperty, SkUiShrinkFactor.Auto); // also before adding
```

## Key APIs

| API | Notes |
| --- | --- |
| `Spacing` | Gap between children (DIPs, ≥ 0) |
| `SkUiShrinkFactor` | `None`, `Auto`, or a number ≥ 0 (implicit from `double`); XAML `"None"`, `"Auto"`, `"2"` |
| Attached `SkUiShrinkLayout.Shrink` (SkUi*) | A child's factor; default `None`. One property for both axes |
| `SkUiCoreShrinkLayout.ShrinkProperty` (Core) | [Core attached property](SkUiCore.md#attached-properties), default `None`; also set by `Add(child, shrink)` and `SetShrink` (by child or index); plain `Add(child)` keeps the child's own value |

## Related

Gallery: `HorizontalShrinkLayoutDemoPage`, `VerticalShrinkLayoutDemoPage`, `CoreHorizontalShrinkLayoutDemoPage`, `CoreVerticalShrinkLayoutDemoPage`. Each shows the layout (white) inside the available space (gray): with enough space it takes only what it needs, like a stack; with less, the children with a shrink factor shrink. Pick `None`, `Auto`, `1` or `2` per child to compare the factors.
