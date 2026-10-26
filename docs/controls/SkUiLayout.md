# SkUiLayout

Overlay layout base: every child receives the **same** padded slot; margins/alignment position within that slot. ZIndex orders paint and hit-test.

**MAUI counterpart:** none (SkiaUi overlay). Not a Stack/Grid replacement — use concrete subclasses for structured layout.

## How it works

`[ContentProperty(nameof(Children))]` owns `IList<ISkUiView>`. Defaults `HwAccelerated = true`. Base for Grid, stacks, Absolute.


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10). Invalid values: `Set*` throws; XAML, bindings, styles and the property setter ignore them with a logged warning, as MAUI does.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`). MAUI's `GestureRecognizers` run on the arena too: tap (any `NumberOfTapsRequired`, primary or secondary button), swipe, pan, pinch and pointer recognizers; drag and drop is not run and is reported once as a `Trace` line. See [EventMechanism.md](../design/EventMechanism.md#maui-gesture-recognizers).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).


## How to use

```xml
<sk:SkUiLayout>
  <sk:SkUiBox Color="Crimson" WidthRequest="80" HeightRequest="60"
              HorizontalOptions="Start" VerticalOptions="Start" Margin="16" />
  <sk:SkUiEllipse Fill="Teal" WidthRequest="60" HeightRequest="60"
                  HorizontalOptions="End" VerticalOptions="End" Margin="16" />
</sk:SkUiLayout>
```

## Key properties

`Children`, `Padding`.

## Children in code

Every drawn layout has MAUI `Layout`'s child members, so code-behind ports by renaming the type: `Add`, `Insert`, `Remove`, `RemoveAt`, `Clear`, `Contains`, `IndexOf`, `Count` and the indexer take `IView` (as MAUI's) and require drawn views (an `ArgumentException` names a native one; host it in an [`SkUiMauiContentView`](SkUiMauiContentView.md)). As with MAUI, `Add(null)` / `Insert(i, null)` and `RemoveAt` past the end do nothing. `AddRange` adds many children with one re-measure (`StartUpdating` / `EndUpdating`). Collection initializers work:

```csharp
var card = new SkUiVerticalStackLayout { Spacing = 4, Padding = 12 };
card.Add(new SkUiLabel { Text = "Title" });
card.Insert(0, icon);
var row = new SkUiHorizontalStackLayout { avatar, card };
```

## BindableLayout

MAUI's `BindableLayout` works on `SkUiLayout` and every layout built on it (`SkUiGrid`, the stacks, `SkUiAbsoluteLayout`, `SkUiFlexLayout`, `SkUiWrapLayout`, the shrink stacks): `ItemsSource` (collection changes add, insert, replace, move and remove children), `ItemTemplate`, `ItemTemplateSelector`, `EmptyView`, `EmptyViewTemplate`. MAUI XAML ports by changing the prefix of the layout and the template content:

```xml
<sk:SkUiWrapLayout Spacing="4" BindableLayout.ItemsSource="{Binding Tags}">
  <BindableLayout.ItemTemplate>
    <DataTemplate>
      <sk:SkUiLabel Text="{Binding}" Padding="8,4" />
    </DataTemplate>
  </BindableLayout.ItemTemplate>
  <BindableLayout.EmptyView>
    <sk:SkUiLabel Text="No tags" />
  </BindableLayout.EmptyView>
</sk:SkUiWrapLayout>
```

- Templates must create drawn views (put native ones inside an [`SkUiMauiContentView`](SkUiMauiContentView.md)); a native view throws an `ArgumentException` that says so.
- Without a template, items show as centered `SkUiLabel`s with the item's text, like MAUI's default template (also briefly when `ItemsSource` is set before `ItemTemplate`). As in MAUI, the text is the item's `ToString()` when the item is shown or replaced; changes inside the item are not tracked (MAUI's `{Binding .}` does not observe them either): use an `ItemTemplate` that binds the item's properties for live items.
- Writes through `IBindableLayout.Children` go through the same checks as writes through `ILayout`.
- `EmptyView` must be a drawn view, or come from `EmptyViewTemplate`: a string `EmptyView` throws, because MAUI turns it into its own `Label`.
- Templates can set attached properties (`Grid.Row`, `FlexLayout.Grow`, …); they re-lay out the layout like any child's.
- Every item gets a view, as in MAUI: no virtualization. For long lists use [`SkUiCollectionView`](SkUiCollectionView.md) or [`SkUiVirtualScrollView`](SkUiVirtualVerticalStackLayout.md), which create items near the viewport only.

## Notes

- Measure takes the max of children desired sizes.
- Prefer [`SkUiGrid`](SkUiGrid.md) / stacks / Absolute for real UI structure.

## Related

[LayoutSystem.md](../design/LayoutSystem.md) · Gallery: `LayoutDemoPage`
