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

Released with SkiaUi.Maui, with the same version. Documentation: [SkUiPicker](https://github.com/rafalka/MauiSkiaUi/blob/master/docs/controls/SkUiPicker.md).
