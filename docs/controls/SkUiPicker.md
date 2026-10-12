# SkUiPicker

A drawn picker: a field that shows the selected item (or a title), and a list to choose from that opens on **your app's popup system** (CommunityToolkit's `Popup`, Syncfusion's `SfPopup`, Telerik's `RadPopup`, a navigation service of your own, or MAUI's action sheet).

**MAUI counterpart:** `Picker`. Its XAML ports by changing the prefix. Beyond MAUI: `PopupPlacement` (anchored like a combo box, in the window like a dialog or a sheet, at a rectangle), `PopupBounds`, `ItemTemplate` (drawn rows), `MaxPopupHeight`, `PopupPresenter`. Core twin: `SkUiCorePicker` (fluent setters, `ItemDisplay` as a function instead of a binding, the same field and list).

## How it works

- **Field.** Drawn by the look ([below](#look)): the selected item's text in `TextColor` and the font properties, or `Title` in `TitleColor` (default: the color scheme's muted color) while nothing is selected; a chevron at the end (the left in right-to-left layouts) that turns while the list is open. One line, truncated at the end. As tall as the look's touch target (44) unless `MinimumHeightRequest` is set; as wide as the shown text, the padding and the chevron.
- **Items and selection, as MAUI's.** `Items` (texts; add to it to fill the picker) or `ItemsSource` (any `IList`; each item shows as its `ItemDisplayBinding`, else `ToString()`; an `INotifyCollectionChanged` list is followed, weakly). `SelectedIndex` (-1: none) and `SelectedItem` stay in step, both two-way. An index set before the items applies when they come (without `SelectedIndexChanged`, as MAUI's). On collection changes the selected item is kept where it moved, or the index is clamped; removing the selected item clears the selection. While `ItemsSource` is set, `Items` holds its texts and throws when changed (as MAUI's).
- **Opening.** A tap, Space, Enter or Alt+Down, a screen reader's activation, or `IsOpen = true`. The picker builds the list (a drawn, virtualized `SkUiCollectionView` in a surface of its own, measured), works out where it goes (`PopupPlacement`) and hands both to the presenter in a request ([Popups](#popups-on-your-popup-system)). The selected item is highlighted and scrolled into view. A picker that is disabled, hidden or not on a drawn surface does not open (`IsOpen` stays `false`; MAUI's waits for its handler instead).
- **Choosing and closing.** A tap on a row (or Enter on the highlighted one) chooses it: `SelectedIndex` changes (`SelectedIndexChanged`), then the popup closes and `IsOpen` goes back to `false` (`Closed`). The popup also closes without a choice when the user dismisses it (the presenter's tap outside, Back, Escape), `IsOpen` is set to `false`, or the picker is disabled, hidden, removed or its surface goes. It closes too when the items change while it is open (it shows the items it opened with; a choice made just before is applied to the item it showed, wherever that item is now, or not at all once it has gone). A selection the app sets while it is open moves its highlight, so Enter chooses what the field shows. An anchored popup also closes when a scroller around the picker scrolls (a drawn one or a MAUI `ScrollView`) or the window resizes, as a combo box's does. A popup that fails to build (an item template that throws) leaves the picker closed, with a trace line. `Opened` and `Closed` follow `IsOpen`, as MAUI 10's.
- **Keyboard.** On the closed field, as a desktop combo box: Up / Down (and Left / Right) select the previous / next item, Home / End the first / last, typing jumps to the next item starting with what was typed (the same letter again cycles), all without opening; Alt+Down / Alt+Up, Space and Enter open. In the list (it takes keyboard focus when shown): Up / Down, Page Up / Page Down, Home / End move the highlight, typing jumps, Enter or Space chooses, Escape closes.
- **Screen readers.** A combo box (Android's spinner, WinUI's combo box with expand / collapse and the value, a button on iOS and Mac Catalyst) named by `Title` (or `SemanticProperties.Description`), with the selected text as its value and whether the list is open; activating it opens the list. The list's rows are elements; activating one chooses it.

## Popups on your popup system

SkiaUi has no popup windows. A picker asks an `ISkUiPopupPresenter` to show its list: the picker's `PopupPresenter`, else the app's `SkUiPopups.Presenter` (also set by `UseSkiaUi(options => options.PopupPresenter = …)`), else MAUI's action sheet (`SkUiActionSheetPopupPresenter`: plain item texts, centered, MAUI's own UI; one trace line says so the first time).

With CommunityToolkit.Maui, the **SkiaUi.Maui.CommunityToolkit** package is the presenter:

```csharp
// dotnet add package SkiaUi.Maui.CommunityToolkit
builder.UseMauiCommunityToolkit()
       .UseSkiaUi()
       .UseSkiaUiCommunityToolkit();   // or per picker: PopupPresenter = SkUiToolkitPopupPresenter.Instance
```

Another popup system: write a presenter (below) and register it with `UseSkiaUi(options => options.PopupPresenter = myPresenter)`.

A presenter has one method, `Task ShowAsync(SkUiPopupRequest request, CancellationToken token)`, and four rules:

1. **Show** `request.Content`, a ready MAUI view (the drawn list, measured for the popup), at `request.Bounds` (a rectangle of the window SkiaUi worked out for the placement), or aligned by `request.HorizontalOptions` / `VerticalOptions` for `Window` popups, on `request.Page` / `request.Window`.
2. **Close** it when the token is cancelled: an item was chosen in the content, or the picker closed it.
3. **Return** once it is gone. A dismissal by the user just closes and returns (no result).
4. **Keep** nothing: the request holds the picker, its surface, page and window weakly, and lets go of everything once the task completed.

What the request carries:

| Member | What |
| --- | --- |
| `Anchor` / `PlatformAnchor` | The picker (for `SkUiCorePicker`, its `SkUiCoreHost`) / the nearest view with a platform view (its surface), for popups placed relative to a view (`SfPopup.ShowRelativeToView`, Telerik's `PlacementTarget`) |
| `AnchorBounds` / `AnchorBoundsInPlatformAnchor` | The picker's rectangle in the window / in its surface, through drawn offsets, transforms and scrolling |
| `Placement` | `Anchored`, `Window` or `Absolute` (`Default` is resolved by the look) |
| `Bounds`, `OpensAbove`, `SpaceAbove` / `SpaceBelow` | Where SkiaUi puts the popup: anchored below the picker or above when it fits better, start-aligned (mirrored in right-to-left), at least the picker's width, inside the safe area less `Margin`; window popups aligned there; absolute ones at `PopupBounds` |
| `PreferredSize`, `MaximumSize` | The content's measured size; the safe area less the margin and `MaxPopupHeight` |
| `WindowSize`, `SafeAreaInsets`, `Margin`, `HorizontalOptions` / `VerticalOptions` | For presenters that place popups themselves |
| `Title`, `IsDismissible`, `OverlayColor`, `IsAnimated` | A title (dialogs, action sheets); whether a tap outside may close it; what to dim the page with (none for anchored popups); whether to animate (off with reduced motion) |
| `CloseRequested` | The token `ShowAsync` gets |
| `HasResult`, `IsCompleted` | Whether a choice was made; whether the presenter's task finished |

**Showing your own UI instead** (a platform list, a vendor picker): read the typed request, `SkUiListPopupRequest` (`Items` texts, `SelectedIndex`, `Title`), and call `request.Complete(index)` with the choice; then close and return. Date and time pickers (C7) will add their own typed requests.

### Presenter examples

- **CommunityToolkit.Maui** (`Popup`, modal pages placed by alignment and margins): `SkUiToolkitPopupPresenter` in the **SkiaUi.Maui.CommunityToolkit** package (source: `MauiSkiaUi.CommunityToolkit/`; a model for presenters of your own), used by the samples and the demo. Window popups are aligned by the request's options inside the safe area (the popup page is edge to edge, MAUI 10's default) and dimmed; anchored ones are placed at `Bounds` through their margins over a transparent page, and moved once shown by how far the popup page is from the window's origin. `CornerRadius` and `Shadow` are settable (`new SkUiToolkitPopupPresenter { CornerRadius = 4 }`).
- **MAUI's action sheet:** `SkUiActionSheetPopupPresenter` (built in; the fallback).
- **Syncfusion `SfPopup`** (a sketch, not compiled here):

  ```csharp
  public async Task ShowAsync(SkUiPopupRequest request, CancellationToken token)
  {
      var popup = new SfPopup
      {
          ContentTemplate = new DataTemplate(() => request.Content), ShowHeader = false, ShowFooter = false,
          WidthRequest = request.Bounds.Width, HeightRequest = request.Bounds.Height,
          StaysOpen = !request.IsDismissible, ShowOverlayAlways = request.OverlayColor is not null
      };
      var closed = new TaskCompletionSource();
      popup.Closed += (_, _) => closed.TrySetResult();
      using var registration = token.Register(popup.Dismiss);
      if (request.Placement == SkUiPopupPlacement.Window || request.PlatformAnchor is not { } surface)
          popup.Show(request.Bounds.X, request.Bounds.Y);
      else
          popup.ShowRelativeToView(surface, request.OpensAbove ? PopupRelativePosition.AlignTop : PopupRelativePosition.AlignBottom,
              request.AnchorBoundsInPlatformAnchor.X, request.OpensAbove ? request.AnchorBoundsInPlatformAnchor.Top : request.AnchorBoundsInPlatformAnchor.Bottom);
      await closed.Task;
  }
  ```
- **Telerik `RadPopup`**: the same shape: `PlacementTarget = request.PlatformAnchor`, `Placement = Relative` with `HorizontalOffset` / `VerticalOffset` from `AnchorBoundsInPlatformAnchor` (or `Center` for window popups), `IsModal = !request.IsDismissible`, `OutsideBackgroundColor = request.OverlayColor`; close with `IsOpen = false` on the token, return on `Closed`.
- **A navigation service with a popup stack** (open / close / bring to front): push a page or overlay with `request.Content`, pop it on the token, and complete when it was popped; keeping the request on the stack is fine (it holds nothing).

## Look

The current look ([ControlLook.md](../design/ControlLook.md)) draws every picker field and decides where popups go:

- `SkUiLook.DrawPickerField(canvas, SkUiPickerFieldPaint)` (or the `PickerFieldPainter` delegate) draws the chrome and the chevron behind the text: the field's `Bounds`, `ChevronBounds`, the accent, border and foreground colors, `Focused` (keyboard focus or an open list, animated by `SkUiTransitionKind.FieldFocus`) and `Open` (animated by `PickerOpen`), the press, pointer-over and enabled states, right to left.
- `DefaultSkUiLook.PickerFieldStyle`: `Default` (the look's `DefaultPickerFieldStyle`: an outline, the same on every platform), `Underline`, `Outline`, `Filled` or `None`. Call `SkUiLook.NotifyChanged()` after changing it.
- Platform looks mimic one platform each (`DefaultSkUiLook.ForCurrentPlatform()` picks the one the app runs on; set it with `UseSkiaUi(options => options.Look = …)` or `SkUiLook.Current`): `AndroidSkUiLook` underlines the field (Material's), `IosSkUiLook` opens window popups as a sheet at the bottom on phones; `MacSkUiLook` and `WindowsSkUiLook` match the default look so far.
- `PickerFieldPadding`, `PickerChevronWidth`, `DefaultPickerMinimumHeight` (44).
- Popups: `DefaultPopupPlacement` (`DefaultSkUiLook.PopupPlacement`, default the device's, `DevicePopupPlacement`: anchored on desktops and tablets; on phones a dialog centered in the window), `PopupHorizontalOptions` / `PopupVerticalOptions` (window popups, default centered; `Fill` / `End` makes a bottom sheet, as `IosSkUiLook` on phones), `PopupMargin` (8), `PopupAnchorGap` (4), `PopupPreferredWidth` (320, window popups), `PopupItemHeight` (44) and `PopupItemPadding` (rows without a template), `PopupHighlightColor` (the selection: the accent at 12 %), `GetPopupOverlayColor(placement)`.

## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`picker.SetTitle("Fruit").SetItemsSource(fruits)`). Getters read the bindable store, as in MAUI (FR-10).
- **Gestures** use SkiaUi's gesture arena. See [EventMechanism.md](../design/EventMechanism.md).

## How to use

MAUI's documentation samples port by changing the prefix:

```xml
<sk:SkUiPicker Title="Select a monkey" TitleColor="Red">
  <sk:SkUiPicker.ItemsSource>
    <x:Array Type="{x:Type x:String}">
      <x:String>Baboon</x:String>
      <x:String>Capuchin Monkey</x:String>
      <x:String>Blue Monkey</x:String>
    </x:Array>
  </sk:SkUiPicker.ItemsSource>
</sk:SkUiPicker>

<sk:SkUiPicker Title="Select a monkey"
               ItemsSource="{Binding Monkeys}"
               ItemDisplayBinding="{Binding Name}"
               SelectedItem="{Binding SelectedMonkey}" />
```

Anchored as a combo box, with drawn rows:

```xml
<sk:SkUiPicker Title="Paint" ItemsSource="{Binding Paints}" ItemDisplayBinding="{Binding Name}"
               PopupPlacement="Anchored" MaxPopupHeight="320">
  <sk:SkUiPicker.ItemTemplate>
    <DataTemplate x:DataType="local:Paint">
      <sk:SkUiHorizontalStackLayout Spacing="12" Padding="16,10">
        <sk:SkUiBox Color="{Binding Color}" WidthRequest="20" HeightRequest="20" CornerRadius="10" />
        <sk:SkUiLabel Text="{Binding Name}" />
      </sk:SkUiHorizontalStackLayout>
    </DataTemplate>
  </sk:SkUiPicker.ItemTemplate>
</sk:SkUiPicker>
```

Core:

```csharp
var picker = new SkUiCorePicker().SetTitle("Fruit").SetItemsSource(fruits).SetPopupPlacement(SkUiPopupPlacement.Anchored);
picker.SelectedIndexChanged += (_, _) => Apply(picker.SelectedItem);
host.SetContent(picker);
```

## Key properties

| Property | Default | Notes |
| --- | --- | --- |
| `ItemsSource` / `Items` | `null` / empty | `Items` is read-only while `ItemsSource` is set |
| `ItemDisplayBinding` | `null` | `ToString()` without it (Core: `ItemDisplay`, a function) |
| `SelectedIndex` / `SelectedItem` | `-1` / `null` | Two-way; kept in step |
| `Title` / `TitleColor` | `null` / scheme muted | Shown while nothing is selected; the title of window popups |
| `TextColor` | scheme foreground | Also the chevron and the list's rows |
| `FontFamily`, `FontSize` (16), `FontAttributes`, `FontAutoScalingEnabled`, `CharacterSpacing` | | The field and plain rows |
| `HorizontalTextAlignment` / `VerticalTextAlignment` | `Start` / `Center` | In the field |
| `IsOpen` | `false` | Two-way; `Opened` / `Closed` |
| `PopupPlacement` | `Default` | `Anchored`, `Window`, `Absolute` (SkiaUi) |
| `PopupBounds` | empty | The window rectangle of `Absolute` popups (SkiaUi) |
| `ItemTemplate` | `null` | Drawn rows (`SkUi*` views) bound to the items; a selector picks per item (SkiaUi) |
| `MaxPopupHeight` | `-1` | No limit beyond the window (SkiaUi) |
| `PopupPresenter` | `null` | The app's presenter (SkiaUi) |

Not available: MAUI's platform-specific `UpdateMode` (iOS: the selection changes when the wheel stops) and the per-platform picker UIs (wheels, dialogs): the list is SkiaUi's, on the popup system you choose, or MAUI's action sheet.
