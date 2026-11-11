# SkUiDockLayout

A drawn dock layout with the Community Toolkit's `DockLayout` API: children docked in order to the top, bottom, left or right of the space the earlier ones left, and the last one filling the rest. In the **SkiaUi.Maui.CommunityToolkit** package.

**Toolkit counterpart:** [`DockLayout`](https://learn.microsoft.com/dotnet/communitytoolkit/maui/layouts/docklayout)

## How it works

`SkUiDockLayout` implements the toolkit's `IDockLayout` and is laid out by the toolkit's public `DockLayoutManager`, as the drawn grid, stacks and flex layout run MAUI's managers, so its frames are the toolkit's.

- Children use the toolkit's attached `DockLayout.DockPosition` (`SkUiDockLayout.DockPositionProperty` is the same object): `Top`, `Bottom`, `Left`, `Right`, or `None` (docked as `Top`). A change re-lays the layout out.
- Each docked child takes its desired height (top, bottom) or width (left, right) and the whole other axis of the space left; `HorizontalSpacing` / `VerticalSpacing` follow each child docked left or right / top or bottom.
- With `ShouldExpandLastChild` (default), the last child fills what remains, whatever its position.
- Hidden children are skipped; `Padding` (and the safe area on a surface root) insets everything; right to left mirrors the frames like every drawn layout.

## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10). Invalid values: `Set*` throws; XAML, bindings, styles and the property setter ignore them with a logged warning, as MAUI does.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`). MAUI's `GestureRecognizers` run on the arena too: tap (any `NumberOfTapsRequired`, primary or secondary button), swipe, pan, pinch and pointer recognizers; drag and drop is not run and is reported once as a `Trace` line. See [EventMechanism.md](../design/EventMechanism.md#maui-gesture-recognizers).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).

## How to use

Add the package and register it (`UseSkiaUiCommunityToolkit()`), then:

```xml
<ContentPage ...
             xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi"
             xmlns:skct="clr-namespace:MauiSkiaUi.CommunityToolkit;assembly=MauiSkiaUi.CommunityToolkit"
             xmlns:mct="http://schemas.microsoft.com/dotnet/2022/maui/toolkit">
  <skct:SkUiDockLayout VerticalSpacing="8">
    <sk:SkUiLabel Text="Header" mct:DockLayout.DockPosition="Top" />
    <sk:SkUiLabel Text="Status" mct:DockLayout.DockPosition="Bottom" />
    <sk:SkUiVerticalStackLayout mct:DockLayout.DockPosition="Left" WidthRequest="120" />
    <sk:SkUiLabel Text="Content fills the rest" />
  </skct:SkUiDockLayout>
</ContentPage>
```

```csharp
var dock = new SkUiDockLayout().SetHorizontalSpacing(4);
dock.Add(header, DockPosition.Top);
dock.Add(menu, DockPosition.Left);
dock.Add(content); // the last child fills the rest
SkUiDockLayout.SetDockPosition(menu, DockPosition.Right); // later: lays the dock out again
```

## Key properties

| Property | Default | Notes |
| --- | --- | --- |
| `ShouldExpandLastChild` | `true` | The last child fills the space the others left |
| `HorizontalSpacing` | 0 | Gap after a child docked left or right (≥ 0) |
| `VerticalSpacing` | 0 | Gap after a child docked top or bottom (≥ 0) |
| `Padding` | 0 | As every `SkUiLayout` |
| Attached `DockPosition` | `None` | The toolkit's `DockLayout.DockPositionProperty`, also `SkUiDockLayout.GetDockPosition` / `SetDockPosition` |

`SetShouldExpandLastChild`, `SetHorizontalSpacing`, `SetVerticalSpacing` are the fluent setters; `Add(view, position)` adds a child docked at a position, as on the toolkit's layout.

## Differences from the toolkit's DockLayout

- Children must be drawn views (`ISkUiView`); native ones go in an [`SkUiMauiContentView`](SkUiMauiContentView.md).
- Invalid spacings are ignored with a warning (the fluent setters throw), as for every drawn control.

## Related

Gallery: `DockLayoutDemoPage` (side by side with the toolkit's `DockLayout`). Same package: [SkUiUniformItemsLayout](SkUiUniformItemsLayout.md). Layout grids with sized rows and columns: [SkUiGrid](SkUiGrid.md).
