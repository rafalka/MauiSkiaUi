# SkUiPicker

A drawn picker: a field that shows the selected item (or a title), and a list to choose from that opens on **your app's popup system** (CommunityToolkit's `Popup`, a popup control or navigation service of your own, or MAUI's action sheet).

**MAUI counterpart:** `Picker`. Its XAML ports by changing the prefix. Beyond MAUI: several choices (`SelectionMode`, [below](#several-choices)), choices that wait for OK (`UpdateMode`, [below](#when-a-choice-takes-effect)), `PopupPlacement` (anchored like a combo box, in the window like a dialog or a sheet, at a rectangle, or [docked at the bottom](#the-bottom-pane) as iOS's picker), `PopupBounds`, `ItemTemplate` (drawn rows), `MaxPopupHeight`, `PopupPresenter`. Core twin: `SkUiCorePicker` (fluent setters, `ItemDisplay` as a function instead of a binding, `SetSelectedItems` instead of a mutable list, the same field and list). What every drawn picker shares (the field, fonts and colors, `IsOpen`, the popup's placement and presenter) is in the base class `SkUiPickerBase` (Core: `SkUiCorePickerBase`).

## How it works

- **Field.** Drawn by the look ([below](#look)): the selected item's text in `TextColor` and the font properties, or `Title` in `TitleColor` (default: the color scheme's muted color) while nothing is selected; a chevron at the end (the left in right-to-left layouts) that turns while the list is open. One line, truncated at the end. As tall as the look's touch target (44) unless `MinimumHeightRequest` is set; as wide as the shown text, the padding and the chevron.
- **Items and selection, as MAUI's.** `Items` (texts; add to it to fill the picker) or `ItemsSource` (any `IList`; each item shows as its `ItemDisplayBinding`, else `ToString()`; an `INotifyCollectionChanged` list is followed, weakly). `SelectedIndex` (-1: none) and `SelectedItem` stay in step, both two-way. An index set before the items applies when they come (without `SelectedIndexChanged`, as MAUI's). On collection changes the selected item is kept where it moved, or the index is clamped; removing the selected item clears the selection. While `ItemsSource` is set, `Items` holds its texts and throws when changed (as MAUI's).
- **Opening.** A tap, Space, Enter or Alt+Down, a screen reader's activation, or `IsOpen = true`. The picker builds the list (a drawn, virtualized `SkUiCollectionView` in a surface of its own, measured), works out where it goes (`PopupPlacement`) and hands both to the presenter in a request ([Popups](#popups-on-your-popup-system)). The selected item is highlighted and scrolled into view. A picker that is disabled, hidden or not on a drawn surface does not open (`IsOpen` stays `false`; MAUI's waits for its handler instead).
- **Choosing and closing.** A tap on a row (or Enter on the highlighted one) chooses it: `SelectedIndex` changes (`SelectedIndexChanged`), then the popup closes and `IsOpen` goes back to `false` (`Closed`). The popup also closes without a choice when the user dismisses it (the presenter's tap outside, Back, Escape), `IsOpen` is set to `false`, or the picker is disabled, hidden, removed or its surface goes. It closes too when the items change while it is open (it shows the items it opened with; a choice made just before is applied to the item it showed, wherever that item is now, or not at all once it has gone). A selection the app sets while it is open moves its highlight, so Enter chooses what the field shows. An anchored popup also closes when a scroller around the picker scrolls (a drawn one or a MAUI `ScrollView`) or the window resizes, as a combo box's does. A popup that fails to build (an item template that throws) leaves the picker closed, with a trace line. `Opened` and `Closed` follow `IsOpen`, as MAUI 10's.
- **Keyboard.** On the closed field, as a desktop combo box: Up / Down (and Left / Right) select the previous / next item, Home / End the first / last, typing jumps to the next item starting with what was typed (the same letter again cycles), all without opening; Alt+Down / Alt+Up, Space and Enter open. In the list (it takes keyboard focus when shown): Up / Down, Page Up / Page Down, Home / End move the highlight, typing jumps, Enter or Space chooses, Escape closes.
- **Screen readers.** A combo box (Android's spinner, WinUI's combo box with expand / collapse and the value, a button on iOS and Mac Catalyst) named by `Title` (or `SemanticProperties.Description`), with the selected text as its value and whether the list is open; activating it opens the list. The list's rows are elements; activating one chooses it.

## Several choices

`SelectionMode="Multiple"` (SkiaUi; default `Single`) turns the list into check boxes:

- **The choice** is `SelectedItems` (an `IList<object>` of items of `ItemsSource`, else texts of `Items`; never `null`), with `SelectedIndices` (ascending) and `SelectionChanged` (`SkUiSelectionChangedEventArgs`: the items before and after, once per change), as `SkUiCollectionView`'s multiple selection. Change the list, or bind it to a view model's (`SelectedItems="{Binding Chosen}"`; an `INotifyCollectionChanged` list is followed, weakly), to choose from code; `SelectAll()` and `ClearSelection()`. `SelectedIndex` / `SelectedItem` stay -1 / `null` (setting them does nothing). Switching the mode keeps the first choice: the selected item becomes the only one chosen, or the first chosen item becomes the selection.
- **The items change:** chosen items that leave them leave `SelectedItems`; ones that move stay chosen. Items chosen before the picker has any items wait for them.
- **The list stays open.** A tap toggles a row and applies at once (the field follows behind the popup); the list closes on a dismissal (the presenter's tap outside, Back), Enter or Escape, and keeps the choice. With `UpdateMode="WhenFinished"` the toggles are a draft: OK or Enter applies them (one `SelectionChanged`), Cancel, Escape or a dismissal discard them ([below](#when-a-choice-takes-effect)). Keys in the list: Up / Down, Page Up / Page Down, Home / End and typing move a cursor (a light row), Space toggles the row at it. The closed field opens with a tap, Space, Enter or Alt+Down; arrow keys do not change the choice there.
- **The field** shows the chosen texts in the order of the items, joined by `SelectedTextSeparator` (default `", "`), truncated at the end; or, with `SelectedCountFormat` (`"{0} selected"`), the count once two or more are chosen; the title while nothing is.
- **Tokens** (`SelectedItemsDisplay="Tokens"`; default `Text`): the field shows a token (a chip) for each chosen item, in the order of the items, drawn by the look (`SkUiLook.DrawPickerToken` / `PickerTokenPainter`, `SkUiPickerTokenPaint`). A token's × removes its item without opening the list; Backspace on the focused field removes the last one; items that cannot be chosen keep their token without a ×. The field grows by lines; with `MaxTokenLines` the tokens that do not fit become one "+N" token (`SkUiStrings.MoreTokensFormat`). A tap elsewhere on the field opens the list.
- **Rows:** a check box before the row's text or `ItemTemplate`; chosen rows show `SelectionBackground` and their template's root is in the `Selected` visual state ([below](#the-selected-rows-look)). Screen readers read the rows as selected or not.
- **Select all** (`IsSelectAllVisible`, text `SelectAllText` or `SkUiStrings.SelectAllText`): a row above the rows, a check box that is checked when every item that can be chosen is, in between when some are; a tap chooses them all (disabled items keep their state), or none when all were, as one change (a draft with `WhenFinished`).
- **Presenters** that show their own UI read `SkUiMultiListPopupRequest` (`Items`, `SelectedIndices` following the changes) and call `SetSelected(index, selected)` (or `SetSelection(indices)`) as the user toggles items (applied at once, or kept as the draft `Accept()` applies), or `Complete(indices)` with the final choice. MAUI's action sheet shows lists of one choice only: register a presenter for this mode.

```xml
<sk:SkUiPicker Title="Toppings" SelectionMode="Multiple" SelectedCountFormat="{}{0} toppings"
               ItemsSource="{Binding Toppings}" ItemDisplayBinding="{Binding Name}"
               SelectedItems="{Binding ChosenToppings}" />
```

Several columns of wheels (hours and minutes, an amount and a unit) are a control of their own: [SkUiMultiColumnPicker](SkUiMultiColumnPicker.md).

## When a choice takes effect

`UpdateMode` (on every picker, `SkUiPickerBase`; MAUI's iOS-only `Picker.UpdateMode`, here on every platform):

- **`Immediately`** (default): a tap on a row chooses it and closes the list; with several choices each toggled row, and on [wheels](SkUiMultiColumnPicker.md) each move, applies at once. Dismissing keeps what was chosen.
- **`WhenFinished`:** what is chosen in the popup is a draft. A tap only highlights a row (one choice), toggles a check box or moves a wheel, and the field and the selection properties stay as they were. **OK** (also Enter; Space on wheels) applies the draft: the selection events follow, then the popup closes (`Closed`). **Cancel**, Escape or a dismissal (the presenter's tap outside, Back) discard it. The OK and Cancel buttons sit below the list or wheels, at the end side (mirrored right to left), Cancel first; they apply from the next opening.

**The buttons' texts** are localizable in two places:

- App-wide: `SkUiStrings.Current.AcceptText` and `CancelText` (default "OK" and "Cancel"), with SkiaUi's other texts in one object you replace as a whole or derive from to read your resources ([Localization.md](../design/Localization.md)). They are read each time a popup opens, so a language switch applies to the next popup. `CancelText` is also the cancel button of MAUI's action sheet (the fallback presenter).
- Per picker: `AcceptText` / `CancelText` (bindable: `AcceptText="{x:Static res:AppResources.Choose}"` or a binding to a localization service); `null` (default) uses the app's.

**The buttons are drawn by SkiaUi on every platform** and with every presenter, so they look the same everywhere (a popup system's own footer stays off). A presenter that shows its own UI for a typed request instead of the drawn content reads `request.RequiresAccept` and the resolved `request.AcceptText` / `request.CancelText`, and calls `request.Accept()` / `request.Cancel()` from its own buttons.

```xml
<sk:SkUiPicker Title="Toppings" SelectionMode="Multiple" UpdateMode="WhenFinished"
               ItemsSource="{Binding Toppings}" SelectedItems="{Binding ChosenToppings}"
               AcceptText="{Binding Strings[Apply]}" />
```

## The app's views in the popup

`PopupHeaderTemplate` and `PopupFooterTemplate` (every picker; SkiaUi) put drawn views of the app's (`SkUi*`) above the list or wheels (under the title) and below them (above the buttons): a hint, an action, a legend. They are created for each opening and bound to the picker's binding context (Core pickers: none). With "Select all", the app's header comes first.

## The popup's width

A popup is as wide as its items want (the widest text of the first 200 items, or the first templated row), within a range:

- **At least:** `MinPopupWidth`, else an anchored popup's field width or a window popup's `SkUiLook.PopupPreferredWidth` (320). Set `MinPopupWidth` for lists of short items (single numbers), which would otherwise be as wide as the default; it replaces the field's width, so an anchored popup may be narrower than its field.
- **At most:** `MaxPopupWidth`, and the window's safe area less the margin; longer items are truncated at the end.
- **Exactly:** `PopupWidth`, which replaces both.

So a narrow field opens a wider list when its items are wider. With `SelectedItemDisplayBinding` the field shows a short text of the selected item while the list shows the whole one: a currency field shows "PLN", its anchored list "Polish Zloty (PLN)". Type-ahead and screen readers use the whole text; with several choices the joined texts and the tokens use the short one. Core: `SelectedItemDisplay`, a function.

```xml
<sk:SkUiPicker ItemsSource="{Binding Currencies}" SelectedItem="{Binding Currency}" HorizontalOptions="Start"
               ItemDisplayBinding="{Binding Name}" SelectedItemDisplayBinding="{Binding Code}"
               PopupPlacement="Anchored" MaxPopupWidth="280" />
```

## Items that cannot be chosen

`ItemEnabledBinding` (SkiaUi; Core: `ItemEnabled`, a function of the item) says per item whether it can be chosen: a `bool` of the item of `ItemsSource` (or of the text of `Items`), e.g. `ItemEnabledBinding="{Binding InStock}"`; `null` (default): every item.

- Disabled items show dimmed; a templated row's root is also in the `Disabled` visual state. A template whose root defines a `Disabled` state (`VisualStateManager.VisualStateGroups`) is not dimmed: that state is the look of disabled rows then.
- Taps on them do nothing; the keyboard (in the list and on the closed field) and type-ahead pass them by, in the key's direction.
- With several choices they keep their state: a disabled item the app chose stays chosen through toggles, `SelectAll()` and Clear; others cannot be chosen.
- The app may still select a disabled item from code.
- The states are read when the items change; after the app's data changed, `RefreshItemStates()` reads them again and an open list follows.
- Presenters with their own UI read `request.ItemsEnabled` (`null`: all); `Complete` and `SetCurrentIndex` refuse disabled items.

[Wheels](SkUiMultiColumnPicker.md) have the same per column.

## Clearing the choice

Every picker (lists, several choices, [wheels](SkUiMultiColumnPicker.md)) can be cleared: no selected item, no chosen items, no item in any column; the field shows its placeholder.

- **The field's clear button** (`IsClearButtonVisible`, default `false`): drawn by the look before the chevron while something is chosen (`SkUiLook.DrawPickerClearButton` / `PickerClearButtonPainter`, room `PickerClearButtonWidth`). A tap on it clears without opening the popup; on the focused field Delete or Backspace does the same. Screen readers and UI tests get it as a button of its own, read as `ClearText` or `SkUiStrings.ClearText`, with the id `{AutomationId}_Clear` ([Accessibility.md](../design/Accessibility.md#drawn-parts-clear-buttons-m7)).
- **The popup's Clear button** (`IsPopupClearButtonVisible`, default `false`): at the start of the popup's footer, text `ClearText` or `SkUiStrings.ClearText` ("Clear"). With `UpdateMode="Immediately"` it clears at once (a list of one choice then closes); with `WhenFinished` it clears the draft, and OK applies it. Wheels always show an item in their middle rows, so they cannot show a cleared draft: their Clear clears and closes in both modes.
- **From code:** `ClearSelection()` on every picker. A presenter's own UI calls `request.Clear()`, or completes a `SkUiListPopupRequest` with `-1`.

Clearing raises the change events (`SelectedIndexChanged`, `SelectionChanged`) and `SelectionChangedCommand` as any other change.

## The bottom pane

`PopupPlacement="BottomPane"` (SkiaUi) docks the popup to the window's bottom edge, as iOS's picker (and the keyboard):

- **Where.** As wide as the window and down to its bottom edge; the content pads itself by the safe area's sides and bottom (the home indicator, a landscape notch), so its background reaches the edges. As tall as the content (at most `MaxPopupHeight`, plus that inset), never above the safe area's top. The popup widths (`MinPopupWidth`, `MaxPopupWidth`, `PopupWidth`) do not apply. The page is not dimmed (the look's `GetPopupOverlayColor`), and the pane stays open while the page scrolls.
- **A toolbar on top** instead of the title and the footer: the title centered in the pane whatever the sides hold (moved aside only when it would overlap them; a [`SkUiHorizontalAlignStackLayout`](SkUiHorizontalAlignStackLayout.md)), a **close button** (×) at the end side, and Cancel (with `UpdateMode="WhenFinished"`) and Clear (`IsPopupClearButtonVisible`) at the start side. The close button accepts what the popup shows (iOS's Done); screen readers read it as `AcceptText`, else `SkUiStrings.Current.DoneText` ("Done"). The look draws it (`DrawPopupCloseButton`, or set `PopupCloseButtonPainter`; default a soft circle with a cross). A dismissal (a tap outside) closes without accepting.
- **A wheel on iOS.** With a look whose `PickerPaneShowsWheel` is `true` (`IosSkUiLook`, and `MacSkUiLook` that derives from it), a list to choose one item from shows as one wheel, as `UIPickerView`: the item in its middle row is chosen as the wheel turns (`SelectedIndexChanged` each time it settles on another item; with `WhenFinished`, a draft until the close button), and the close button chooses the item it shows, also when nothing was chosen yet (as MAUI's iOS picker). Disabled items are passed by, `ItemTemplate` draws the wheel's items, and Clear clears and closes (a wheel cannot show that nothing is chosen). Other looks keep the list in the pane. Lists with several choices stay lists; the multi-column, date and time pickers' wheels get the toolbar.

```xml
<sk:SkUiPicker Title="Monkey" ItemsSource="{Binding Monkeys}" SelectedItem="{Binding Monkey}"
               PopupPlacement="BottomPane" />
```

A presenter places a pane at `request.Bounds` like an anchored popup (`SkUiToolkitPopupPresenter` does, with square corners). One that shows its own UI reads the same `SkUiListPopupRequest` and may choose as the user goes with `request.SetSelectedIndex(index)` (applied at once, or the draft with `RequiresAccept`).

## Popups on your popup system

SkiaUi has no popup windows. A picker asks an `ISkUiPopupPresenter` to show its list: the picker's `PopupPresenter`, else the app's `SkUiPopups.Presenter` (also set by `UseSkiaUi(options => options.PopupPresenter = …)`), else MAUI's action sheet (`SkUiActionSheetPopupPresenter`: plain item texts, centered, MAUI's own UI; one trace line says so the first time). The action sheet has no OK button: with `UpdateMode="WhenFinished"` a tap on an item chooses and accepts it at once, and its cancel button (or a tap outside) changes nothing; lists with several choices and wheels need a presenter of their own.

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
| `Anchor` / `PlatformAnchor` | The picker (for `SkUiCorePicker`, its `SkUiCoreHost`) / the nearest view with a platform view (its surface), for popups placed relative to a view |
| `AnchorBounds` / `AnchorBoundsInPlatformAnchor` | The picker's rectangle in the window / in its surface, through drawn offsets, transforms and scrolling |
| `Placement` | `Anchored`, `Window`, `Absolute` or `BottomPane` (`Default` is resolved by the look) |
| `Bounds`, `OpensAbove`, `SpaceAbove` / `SpaceBelow` | Where SkiaUi puts the popup: anchored below the picker or above when it fits better, start-aligned (mirrored in right-to-left), at least the picker's width, inside the safe area less `Margin`; window popups aligned there; absolute ones at `PopupBounds`; a bottom pane at the window's bottom edge, its full width (the safe area inside it) |
| `PreferredSize`, `MaximumSize` | The content's measured size; the safe area less the margin and `MaxPopupHeight` |
| `WindowSize`, `SafeAreaInsets`, `Margin`, `HorizontalOptions` / `VerticalOptions` | For presenters that place popups themselves |
| `Title`, `IsDismissible`, `OverlayColor`, `IsAnimated` | A title (dialogs, action sheets); whether a tap outside may close it; what to dim the page with (none for anchored popups); whether to animate (off with reduced motion) |
| `CornerRadius`, `Shadow` | The popup's frame, from the look: `Content` is already clipped to the corners; give your container (its shape, a `Border`) the same corners and the shadow, which falls outside the content (`null`: none) |
| `CloseRequested` | The token `ShowAsync` gets |
| `HasResult`, `IsCompleted` | Whether a choice was made; whether the presenter's task finished |

**Showing your own UI instead** (a platform list, a picker of your own): read the typed request, `SkUiListPopupRequest` (`Items` texts, `SelectedIndex`, `Title`), and call `request.Complete(index)` with the choice; then close and return. Choices that wait for OK: `request.RequiresAccept`; move the highlight with `SetCurrentIndex(index)` and call `request.Accept()` / `Cancel()`, or `Complete(index)` directly. A UI that stays open while the user moves through the items (a wheel) calls `SetSelectedIndex(index)` as it goes: applied to the picker at once, or the draft `Accept()` takes. Several choices and wheels have typed requests of their own that report each change (`SkUiSelectionPopupRequest`, applied at once or kept as a draft until `Accept`: `SkUiMultiListPopupRequest`, [above](#several-choices), and `SkUiColumnsPopupRequest`, [SkUiMultiColumnPicker](SkUiMultiColumnPicker.md#presenters)). Date and time pickers have theirs ([SkUiDatePicker](SkUiDatePicker.md)).

**A picker of your own with its own UI:** derive from `SkUiPicker` (or `SkUiCorePicker`) and override `SelectItemsAsync(selection, token)` (SkiaUi). It is called when the picker opens with what to choose from, `SkUiPickerSelection`: `Items` (the `ItemsSource` objects, else the `Items` texts), `Texts` (the display texts), `SelectedItems`, `AllowsMultiple`, `IsItemEnabled(index)`, `Title`, and the `PopupRequest` underneath for placement details. Return the chosen items, taken from `Items`; none clears the choice and `null` (a dismissal) changes nothing. Close your UI when the token is cancelled (the picker closed it); what you return then is ignored. The picker applies the answer with its own rules and events, as a choice in the drawn list: one item at most without `SelectionMode="Multiple"`, items it does not have and disabled ones are ignored (disabled ones keep their state). The base shows the drawn list on the presenter and answers with what was chosen there. Your UI replaces the list in every placement, also the wheel a bottom pane shows a list as on looks with `PickerPaneShowsWheel` (iOS): to keep that wheel, call the base when `selection.PopupRequest.Placement` is `BottomPane`.

```csharp
public sealed class SheetPicker : SkUiPicker
{
    protected override async Task<IReadOnlyList<object>?> SelectItemsAsync(SkUiPickerSelection selection, CancellationToken token)
    {
        // Your UI for selection.Texts (selection.SelectedItems, selection.IsItemEnabled(i), selection.Title);
        // close it when the token is cancelled.
        var index = await MySheet.ChooseAsync(selection.Texts, token);
        return index >= 0 ? [selection.Items[index]] : null;
    }
}
```

Lower down, `ShowPopupAsync(request, token)` (on `SkUiPickerBase` and `SkUiCorePickerBase`, so on every picker) shows the popup itself, with the presenter's contract and its typed requests: override it for the multi-column, date and time pickers' wheels, or for full control over the popup; call the base for the requests you do not handle. On `SkUiPicker` it is what calls `SelectItemsAsync` for lists.

The drawn list is still built for each opening (`request.Content`), unused by such a UI.

### Presenter examples

- **CommunityToolkit.Maui** (`Popup`, modal pages placed by alignment and margins): `SkUiToolkitPopupPresenter` in the **SkiaUi.Maui.CommunityToolkit** package (source: `MauiSkiaUi.CommunityToolkit/`; a model for presenters of your own), used by the samples and the demo. Window popups are aligned by the request's options inside the safe area (the popup page is edge to edge, MAUI 10's default) and dimmed; anchored ones are placed at `Bounds` through their margins over a transparent page, and moved once shown by how far the popup page is from the window's origin. Its shape and shadow are the request's (the look's); `CornerRadius` and `Shadow` replace them for every popup (`new SkUiToolkitPopupPresenter { CornerRadius = 4 }`).
- **MAUI's action sheet:** `SkUiActionSheetPopupPresenter` (built in; the fallback).
- **A popup control of your own** (one that shows a view at a position, or relative to another view): set its content to `request.Content` and its size to `request.Bounds`; for `Placement` `Window` (or without `PlatformAnchor`) show it at `request.Bounds`, else relative to `request.PlatformAnchor`, below or above (`request.OpensAbove`) `request.AnchorBoundsInPlatformAnchor`; keep it open on outside taps unless `request.IsDismissible`, dim with `request.OverlayColor`, shape and shadow its container with `request.CornerRadius` and `request.Shadow`; close it when the token is cancelled (`token.Register`), and return once it has closed.
- **A navigation service with a popup stack** (open / close / bring to front): push a page or overlay with `request.Content`, pop it on the token, and complete when it was popped; keeping the request on the stack is fine (it holds nothing).

## Look

The current look ([ControlLook.md](../design/ControlLook.md)) draws every picker field and decides where popups go:

- `SkUiLook.DrawPickerField(canvas, SkUiPickerFieldPaint)` (or the `PickerFieldPainter` delegate) draws the chrome and the chevron behind the text: the field's `Bounds`, `ChevronBounds`, the accent, border and foreground colors, `Focused` (keyboard focus or an open list, animated by `SkUiTransitionKind.FieldFocus`) and `Open` (animated by `PickerOpen`), the press, pointer-over and enabled states, right to left.
- `DefaultSkUiLook.PickerFieldStyle`: `Default` (the look's `DefaultPickerFieldStyle`: an outline, the same on every platform), `Underline`, `Outline`, `Filled`, `PopUpButton`, `ComboBox` or `None`. Call `SkUiLook.NotifyChanged()` after changing it. A picker's own `FieldStyle` wins over it; its own chrome comes in `SkUiPickerFieldPaint.Style` and `Chrome` ([Field chrome](#field-chrome)).
- Platform looks mimic one platform each (`DefaultSkUiLook.ForCurrentPlatform()` picks the one the app runs on; set it with `UseSkiaUi(options => options.Look = …)` or `SkUiLook.Current`): `AndroidSkUiLook` underlines the field (Material's), `IosSkUiLook` opens window popups as a sheet at the bottom on phones and shows a [bottom pane](#the-bottom-pane)'s list as a wheel; `MacSkUiLook` and `WindowsSkUiLook` match the default look so far.
- The popup's footer: `PopupAcceptButtonFirst` (OK before Cancel; `WindowsSkUiLook`: `true`), `StylePopupButton(button, SkUiPopupButton)` (OK filled in the accent color, Cancel and Clear as accent text by default).
- Wheels ([SkUiMultiColumnPicker](SkUiMultiColumnPicker.md)): `PickerWheelRowHeight`, `PickerWheelEffect`, `PickerWheelBandColor`, `PickerWheelBandCornerRadius`; `PickerPaneShowsWheel` (a bottom pane's list as a wheel; default `false`, `IosSkUiLook` `true`); `DrawPopupCloseButton` / `PopupCloseButtonPainter` (a bottom pane's ×).
- `PickerFieldPadding`, `PickerChevronWidth`, `PickerClearButtonWidth` (32), `DefaultPickerMinimumHeight` (44); `DrawPickerClearButton` / `PickerClearButtonPainter` (`SkUiClearButtonPaint`) draws the field's clear button.
- Popups: `DefaultPopupPlacement` (`DefaultSkUiLook.PopupPlacement`, default the device's, `DevicePopupPlacement`: anchored on desktops and tablets; on phones a dialog centered in the window), `PopupHorizontalOptions` / `PopupVerticalOptions` (window popups, default centered; `Fill` / `End` makes a bottom sheet, as `IosSkUiLook` on phones), `PopupMargin` (8), `PopupAnchorGap` (4), `PopupPreferredWidth` (320, window popups), `PopupItemHeight` (44) and `PopupItemPadding` (rows without a template), `PopupHighlightColor` (the selection: the accent at 12 %), `GetPopupOverlayColor(placement)` (none for anchored popups and bottom panes), `GetPopupCornerRadius(placement)` and `GetPopupShadow(placement)` (the popup's frame: 10 and a soft shadow; bottom panes square; Android 4 dp, and Material 3's dialogs 28 dp; Windows 8 epx and Fluent's light flyout shadow).

## Field chrome

Every picker of both layers (this one, the date, time, date-and-time and multi-column pickers) can set its own field over the look's, as the text fields do ([SkUiEntry.md](SkUiEntry.md#field-chrome)): `FieldStyle` (`SkUiPickerFieldStyle`; `Default`: the look's `PickerFieldStyle`), `FillColor` (drawn as set, also while disabled), `BorderColor`, `BorderWidth` (0: no idle outline) and `CornerRadii` / `CornerRadius` (Core: `SetCornerRadius(double?)`), each `null` for the style's. Focus, open and disabled emphasis stay the look's, and the press overlay follows the corners.

```xml
<sk:SkUiPicker Title="Country" FieldStyle="Outline" BorderColor="{StaticResource Accent}" CornerRadius="12" />
<sk:SkUiDatePicker FieldStyle="None" FillColor="#EEF1F3" CornerRadius="22" />
```

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

### The field's look of the selected item

`SelectedItemTemplate` (SkiaUi; `SkUiPicker` only) shows the selected item in the field through drawn views of the app's, bound to `SelectedItem` (a `DataTemplateSelector` picks per item), inside the field's chrome before the chevron and the clear button; the field grows with them. The views get the field's content area, inside its padding: a field with a set height (48 is common) leaves them about the height of a line of text, so give them no padding of their own and don't reuse a list row's template with its padding (it is squeezed and clipped). They take no taps (a tap opens the list as anywhere on the field), and screen readers read the item's text. While nothing is selected the placeholder shows, and with several choices the texts or tokens.

```xml
<sk:SkUiPicker ItemsSource="{Binding Paints}" ItemDisplayBinding="{Binding Name}" SelectedItem="{Binding Paint}">
  <sk:SkUiPicker.SelectedItemTemplate>
    <DataTemplate x:DataType="local:Paint">
      <sk:SkUiHorizontalStackLayout Spacing="8">
        <sk:SkUiBox Color="{Binding Color}" WidthRequest="18" HeightRequest="18" CornerRadius="9" VerticalOptions="Center" />
        <sk:SkUiLabel Text="{Binding Name}" VerticalOptions="Center" />
      </sk:SkUiHorizontalStackLayout>
    </DataTemplate>
  </sk:SkUiPicker.SelectedItemTemplate>
</sk:SkUiPicker>
```

### The selected row's look

The list's selected row (the highlighted one: the selection when it opens, then where a tap or the keys move it; with several choices every chosen row) is drawn on `SelectionBackground`, and the root of its `ItemTemplate` is in MAUI's `Selected` visual state (`CommonStates`; `Disabled` wins, `Selected` comes before `PointerOver`), as a collection view's items. Style it as there:

- `SelectionBackground`: a brush (in XAML a color) behind selected rows; `null` (default): the look's `PopupHighlightColor` (the accent at 12 %); `Transparent`: none.
- The template root's `VisualStateGroups`: setters for the root's properties in the `Selected` state (its background, padding, opacity).

```xml
<sk:SkUiPicker Title="Paint" ItemsSource="{Binding Paints}" ItemDisplayBinding="{Binding Name}" SelectionBackground="Transparent">
  <sk:SkUiPicker.ItemTemplate>
    <DataTemplate x:DataType="local:Paint">
      <sk:SkUiHorizontalStackLayout Spacing="12" Padding="16,10">
        <VisualStateManager.VisualStateGroups>
          <VisualStateGroup Name="CommonStates">
            <VisualState Name="Normal" />
            <VisualState Name="Selected">
              <VisualState.Setters>
                <Setter Property="BackgroundColor" Value="{StaticResource Primary}" />
              </VisualState.Setters>
            </VisualState>
          </VisualStateGroup>
        </VisualStateManager.VisualStateGroups>
        <sk:SkUiBox Color="{Binding Color}" WidthRequest="20" HeightRequest="20" CornerRadius="10" />
        <sk:SkUiLabel Text="{Binding Name}" />
      </sk:SkUiHorizontalStackLayout>
    </DataTemplate>
  </sk:SkUiPicker.ItemTemplate>
</sk:SkUiPicker>
```

Rows without a template (the item texts) show the selection only through `SelectionBackground`. The wheels of [SkUiMultiColumnPicker](SkUiMultiColumnPicker.md) mark the chosen row with the look's band.

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
| `Title` / `TitleColor` | `null` / scheme muted | Shown while nothing is selected (unless `Placeholder` is set); the title of window popups |
| `Placeholder` / `PlaceholderColor` | `null` | The field's hint while nothing is chosen, when it differs from `Title`; `null`: `Title` / `TitleColor` (SkiaUi) |
| `TextColor` | scheme foreground | Also the chevron and the list's rows |
| `FontFamily`, `FontSize` (16), `FontAttributes`, `FontAutoScalingEnabled`, `CharacterSpacing` | | The field and plain rows |
| `HorizontalTextAlignment` / `VerticalTextAlignment` | `Start` / `Center` | In the field |
| `IsOpen` | `false` | Two-way; `Opened` / `Closed` |
| `PopupPlacement` | `Default` | `Anchored`, `Window`, `Absolute`, `BottomPane` ([The bottom pane](#the-bottom-pane)) (SkiaUi) |
| `PopupBounds` | empty | The window rectangle of `Absolute` popups (SkiaUi) |
| `ItemTemplate` | `null` | Drawn rows (`SkUi*` views) bound to the items; a selector picks per item; the selected row's root is in the `Selected` visual state (SkiaUi) |
| `ItemEnabledBinding` | `null` | Whether each item can be chosen ([Items that cannot be chosen](#items-that-cannot-be-chosen)); `RefreshItemStates()` (SkiaUi; Core `ItemEnabled`) |
| `SelectedItemTemplate` | `null` | The selected item in the field through drawn views ([above](#the-fields-look-of-the-selected-item)) (SkiaUi) |
| `SelectionBackground` | `null` | Behind the selected rows; `null`: the look's `PopupHighlightColor`; `Transparent`: none (SkiaUi) |
| `MaxPopupHeight` | `-1` | No limit beyond the window (SkiaUi) |
| `MinPopupWidth` / `MaxPopupWidth` | `-1` / `-1` | The popup's width range: as wide as its items within it ([The popup's width](#the-popups-width)) (SkiaUi) |
| `PopupWidth` | `-1` | The popup's exact width (replaces the range) (SkiaUi) |
| `SelectedItemDisplayBinding` | `null` | The field's short text of the selected item, the list keeps the whole one (SkiaUi; Core `SelectedItemDisplay`) |
| `SelectionChangedCommand` / `SelectionChangedCommandParameter` | `null` | Runs after each change of the choice (when it can execute), before `SelectedIndexChanged` / `SelectionChanged` (SkiaUi) |
| `IsPopupClosedOnSelection` | `false` | Several choices: the list closes after each toggled row (SkiaUi) |
| `PopupHeaderTemplate` / `PopupFooterTemplate` | `null` | The app's views above and below the list or wheels (SkiaUi) |
| `SelectedItemsDisplay` / `MaxTokenLines` | `Text` / `-1` | Several choices: the texts joined, or tokens with × ([Several choices](#several-choices)) (SkiaUi) |
| `IsSelectAllVisible` / `SelectAllText` | `false` / `null` | Several choices: a "Select all" row (SkiaUi) |
| `IsClearButtonVisible` / `IsPopupClearButtonVisible` / `ClearText` | `false` / `false` / `null` | The field's clear button and the popup's Clear button ([Clearing the choice](#clearing-the-choice)) (SkiaUi) |
| `PopupPresenter` | `null` | The app's presenter (SkiaUi) |
| `FieldStyle`, `FillColor`, `BorderColor`, `BorderWidth`, `CornerRadii` / `CornerRadius` | `Default` / `null` | The field's own chrome over the look's ([Field chrome](#field-chrome)) (SkiaUi) |
| `SelectionMode` | `Single` | `Multiple`: check boxes, the list stays open (SkiaUi) |
| `UpdateMode` | `Immediately` | `WhenFinished`: a draft until OK, with Cancel and OK buttons (MAUI's iOS `Picker.UpdateMode`, on every platform) |
| `AcceptText` / `CancelText` | `null` | The buttons' texts (`AcceptText`: also how screen readers read a bottom pane's close button); `null`: `SkUiStrings.Current`'s (SkiaUi) |
| `SelectedItems` / `SelectedIndices` | empty | The choice with `Multiple`; `SelectionChanged`, `SelectAll()`, `ClearSelection()` (SkiaUi) |
| `SelectedTextSeparator` / `SelectedCountFormat` | `", "` / `null` | The field with several choices (SkiaUi) |

MAUI's iOS-only `UpdateMode` is `UpdateMode` on every platform ([above](#when-a-choice-takes-effect)). iOS's picker is `PopupPlacement="BottomPane"` with `IosSkUiLook` (a wheel under a toolbar with a close button, drawn). Not available: the other per-platform picker UIs (Android's dialogs): the list is SkiaUi's, on the popup system you choose, or MAUI's action sheet.
