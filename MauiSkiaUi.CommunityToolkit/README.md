# SkiaUi.Maui.CommunityToolkit

[CommunityToolkit.Maui](https://github.com/CommunityToolkit/Maui) integration for [SkiaUi.Maui](https://www.nuget.org/packages/SkiaUi.Maui), SkiaSharp-drawn .NET MAUI controls.

SkiaUi has no popup windows of its own: drawn controls that need one (`SkUiPicker`'s list) ask the app's popup system through `ISkUiPopupPresenter`. This package is that presenter for the toolkit's `Popup`:

```csharp
builder
    .UseMauiApp<App>()
    .UseMauiCommunityToolkit()
    .UseSkiaUi()
    .UseSkiaUiCommunityToolkit(); // pickers open their lists in the toolkit's Popup
```

- Anchored lists (a combo box: desktops, tablets) sit at the field over a transparent page; window lists (a centered dialog: phones) are aligned in the window's safe area over a dimmed page.
- A tap outside or Back closes them; a choice, or the picker closing, closes them through the toolkit.
- Per control instead of app-wide: `picker.PopupPresenter = SkUiToolkitPopupPresenter.Instance`.

## Layouts

Drawn counterparts of the toolkit's layouts, laid out by the toolkit's own layout managers, so toolkit pages port by changing the prefix:

```xml
<ContentPage ...
             xmlns:skct="clr-namespace:MauiSkiaUi.CommunityToolkit;assembly=MauiSkiaUi.CommunityToolkit"
             xmlns:mct="http://schemas.microsoft.com/dotnet/2022/maui/toolkit">
  <skct:SkUiDockLayout>
    <sk:SkUiLabel Text="Header" mct:DockLayout.DockPosition="Top" />
    <sk:SkUiLabel Text="Fills the rest" />
  </skct:SkUiDockLayout>
</ContentPage>
```

- `SkUiDockLayout`: the toolkit's `DockLayout`; children keep `DockLayout.DockPosition`.
- `SkUiUniformItemsLayout`: the toolkit's `UniformItemsLayout` (`MaxRows`, `MaxColumns`).

Released with SkiaUi.Maui, with the same version. Documentation: [SkUiPicker](https://github.com/rafalka/MauiSkiaUi/blob/master/docs/controls/SkUiPicker.md), [SkUiDockLayout](https://github.com/rafalka/MauiSkiaUi/blob/master/docs/controls/SkUiDockLayout.md), [SkUiUniformItemsLayout](https://github.com/rafalka/MauiSkiaUi/blob/master/docs/controls/SkUiUniformItemsLayout.md).
