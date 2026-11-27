# SkUiSheet

A drawn sheet inside the surface: a panel that slides in from the bottom (or a side) over the page and rests on detents, or a centered dialog card, with an optional title, description and action row around its content. The user drags it between detents and away, flings it, taps the scrim, presses Back or Escape. SkUi\* only (1.18).

**MAUI counterpart:** none inside a page. MAUI's modal pages, `DisplayAlert` / `DisplayActionSheet` and the Community Toolkit's `Popup` are windows of the platform; vendor bottom sheets (The49 `BottomSheet`, `Plugin.Maui.BottomSheet`) are native views. Conversions: [Migration.md](../Migration.md#bottom-sheets-and-dialogs).

## How it works

- **Placement.** Put the sheet in XAML in a layout that fills the page (a grid, spanning its rows and columns). It fills that space, draws nothing while closed, and lets pointers through; open, it is drawn above everything in the surface opened before it (see [stacking](#modal-behaviour-and-stacking)), so its place among the layout's children does not matter. A sheet placed in a smaller layout (another sheet's content, a pane) covers only that layout's area. `SkUiSheet.ShowAsync<TResult>(anchor, content, configure)` opens one over the whole surface of `anchor` without placing it: on the surface root's overlay layer, above everything in the surface.
- **Detents.** `Detents` lists the heights it rests at: `SkUiSheetDetent.Fraction(0.5)` of the tallest it can be (the surface less the window's top inset and the look's `SheetTopMargin`), `SkUiSheetDetent.Height(320)` DIPs above the window's bottom inset, or `SkUiSheetDetent.FitContent` (the grabber, header, content and actions as tall as they ask for). In XAML: `Detents="Fit, 50%, 100%"`. The default is half and full. `SelectedDetentIndex` (two-way) is an index into the list: it opens there, setting it moves the open sheet, and a drag or fling writes the detent it comes to rest on. A sheet keeps its full length at every detent: lower detents show its top part, the rest is below the surface.
- **Motion.** Opening, closing and moving to a detent by code run on the render thread with the look's `Sheet` transition (300 ms; none under reduced motion, so the sheet jumps). The sheet's position is a scroll offset of an internal scroller, so drags, flings and the springs to detents are the scroll views' render-thread motions; a release slower than the fling speed goes to the nearest detent (or closes, when nearer closed than the lowest detent), a fling goes the way it moves and can pass detents, and a fling down past the lowest detent closes it.
- **Scrolling content.** A scroller inside the sheet scrolls first; at its top edge, a drag down hands over to the sheet (the nested-scroll hand-off), which follows the finger and then rests or closes. A drag up on the content scrolls the content; drag the header or grabber to raise the sheet (the content's own scrolling wins, unlike iOS, which raises the sheet first).
- **Scrim.** A modal sheet dims the page with `ScrimColor` (the look's `SheetScrimColor`, black at 32 % by default). Its opacity follows how much of the sheet shows, up to the lowest detent, through a render-thread link to the sheet's offset: no UI work per frame. The scrim takes every pointer over the page; a tap closes the sheet when `DismissOnScrimTap`.
- **Presentation.** `Sheet` (default) comes from its `Edge`. `Dialog` is a card centered in the surface, at most the look's `SheetDialogMaximumWidth` (560) wide inside the `SheetDialogMargin`, as tall as its content asks for: no detents, no grabber, no drag; it fades in from slightly below (the look's `SheetDialog` transition). `Auto` is a sheet on compact widths and a dialog when the surface is at least the look's `SheetDialogBreakpoint` (600) wide: the tablet form sheet.
- **Edges.** `Bottom` (default), `Start`, `End` (the left and right edges, swapped right to left). A bottom sheet is at most the look's `SheetMaximumWidth` (640) wide, centered on wider surfaces. Side sheets measure their detents along their width: at most `SideSheetMaximumWidth` (400) plus the window's inset on that edge, leaving 56 DIPs of the page; a side sheet that sets no `Detents` has one full detent.
- **Header and actions.** `Title` is drawn as a picker popup's title (bold, a level 1 heading for screen readers), `Description` below it in the muted color, then the `Content` (taking the room left), then `Actions` padded as a picker popup's button row. Each is left out when not set. The sheet's own `BackgroundColor` / `Background` colors its panel (the color scheme's background by default); `CornerRadius` (`-1`: the look's `DefaultSheetCornerRadius`) rounds the corners on the edge it opens from, every corner of a dialog.
- **Insets.** The panel keeps its content clear of the window's insets it covers (the home indicator below a bottom sheet, the notch beside a side sheet). The soft keyboard: a focused field in a scrolling content is shown above the keyboard through the scroller's viewport insets (it can move the sheet past its detent while the keyboard shows; the sheet goes back to its detent when it hides).

## Modal behaviour and stacking

- A modal sheet (`IsModal`, the default) is its surface's **modal pane** while open: keyboard focus moves in (to its first focusable node when focus was on the page or a screen reader runs) and Tab goes round inside it, the page leaves the semantics tree (screen readers read nothing behind it, live regions included), and the screen reader's focus moves to the title. When it closes, focus goes back where it was.
- **Closing by the user:** Escape (with keyboard focus in the sheet), Android's Back (the button and the gesture, through the activity's back dispatcher, ahead of MAUI's navigation), a scrim tap (`DismissOnScrimTap`), dragging or flinging it away (`IsDragEnabled`). These close it **dismissed**.
- **Stacking:** several sheets can be open, and the last opened is always on top, wherever each is placed: a sheet placed before another on the page, one placed inside another sheet's content, a `ShowAsync` dialog opened from a sheet's button and a picker's popup on `SkUiSheetPopupPresenter` all open over the sheet they came from, so nothing waits on a covered sheet. An open placed sheet is drawn, hit and read on the surface root's overlay layer, in the order sheets opened, while its layout keeps it as a child (bindings, styles, look, and where it is arranged: it covers the layout it is placed in). The topmost is the only modal pane; Back, Escape and a scrim tap close it, which hands the modal pane back to the one below. A sheet opened again while it was closing comes back on top.
- A non-modal sheet (`IsModal="False"`) has no scrim: the page stays interactive and readable around it.

## Results

`Close(result)` closes the sheet with a result; `CloseAsync()` closes it dismissed and completes when it is closed; `OpenAsync()` completes when it is open. `Closed` reports `SkUiSheetClosedEventArgs` (`Result`, `IsDismissed`). `ShowAsync<TResult>` completes with the result when it is a `TResult`, else `default` (dismissed, another type, or the cancellation token closed it):

```csharp
var delete = new SkUiButton { Text = "Delete", TextColor = Colors.Red };
var confirmed = await SkUiSheet.ShowAsync<bool>(this, new SkUiLabel { Text = "The shift goes for everyone.", Margin = new(24, 0) }, sheet =>
{
    sheet.Presentation = SkUiSheetPresentation.Dialog;
    sheet.Title = "Delete the shift?";
    sheet.Actions = delete;
    delete.Clicked += (_, _) => sheet.Close(true);
});
```

## How to use

```xml
<sk:SkUiGrid>
    <sk:SkUiVerticalStackLayout> <!-- the page --> </sk:SkUiVerticalStackLayout>

    <sk:SkUiSheet IsOpen="{Binding IsEditing}" Title="Edit the shift" Detents="50%, 100%" AutomationId="ShiftForm">
        <sk:SkUiScrollView>
            <sk:SkUiVerticalStackLayout Padding="16,0" Spacing="8">
                <sk:SkUiTimePicker Time="{Binding Start}" />
                <sk:SkUiTimePicker Time="{Binding End}" />
            </sk:SkUiVerticalStackLayout>
        </sk:SkUiScrollView>
        <sk:SkUiSheet.Actions>
            <sk:SkUiButton Text="Save" Command="{Binding SaveCommand}" HorizontalOptions="End" />
        </sk:SkUiSheet.Actions>
    </sk:SkUiSheet>
</sk:SkUiGrid>
```

Pickers' popups in a sheet, without a popup library: `SkUiSheetPopupPresenter` (app-wide `SkUiPopups.Presenter`, `UseSkiaUi(options => options.PopupPresenter = new SkUiSheetPopupPresenter())`, or per picker `PopupPresenter`) shows each popup in a sheet fitted to it, on the anchor's surface ([Popups.md](../design/Popups.md#popups-in-a-sheet)).

## Key properties

| Member | Type | Default | Notes |
| --- | --- | --- | --- |
| `Content` | `View?` | `null` | Content property; a drawn view (Core content through an `SkUiCoreHost`) |
| `Edge` | `SkUiSheetEdge` | `Bottom` | `Bottom`, `Start`, `End` |
| `Detents` | `IList<SkUiSheetDetent>` | half, full | `Fraction(d)`, `Height(dips)`, `FitContent`; XAML text `"Fit, 50%, 320"` |
| `SelectedDetentIndex` | `int` | `0` | Two-way |
| `IsOpen` | `bool` | `false` | Two-way |
| `IsModal` | `bool` | `true` | Scrim, focus trap, the page out of the semantics tree |
| `ScrimColor` | `Color?` | `null` | The look's `SheetScrimColor` |
| `DismissOnScrimTap`, `IsDragEnabled`, `HasGrabber` | `bool` | `true` | |
| `CornerRadius` | `double` | `-1` | The look's |
| `Presentation` | `SkUiSheetPresentation` | `Sheet` | `Sheet`, `Dialog`, `Auto` |
| `Title`, `Description` | `string?` | `null` | |
| `Actions` | `View?` | `null` | A drawn row below the content |
| `OpenAsync()`, `CloseAsync()`, `Close(result)` | | | Complete when the motion ends |
| `ShowAsync<TResult>(anchor, content, configure, token)` | static | | On the surface root's overlay layer |
| `Opened`, `Closed`, `DetentChanged` | events | | `SkUiSheetClosedEventArgs`, `SkUiSelectedIndexChangedEventArgs` |

## Look

`DrawSheet` (`SkUiSheetPaint`: bounds, background, resolved corners, dialog or not) and `DrawSheetGrabber` (`SkUiSheetGrabberPaint`), with `SheetPainter` / `SheetGrabberPainter` to replace them; sizes `DefaultSheetCornerRadius` (16; Material 3 28, iOS 10, Fluent 8), `SheetTopMargin` (iOS 10), `SheetMaximumWidth`, `SideSheetMaximumWidth`, `SheetDialogMaximumWidth`, `SheetDialogBreakpoint`, `SheetDialogMargin`, `SheetGrabberSize` (36 × 4; Material 3 32 × 4, iOS 36 × 5), `SheetGrabberBandHeight`; `SheetScrimColor`; `GetSheetShadow(isDialog)` (a dialog's: the window popups'); transitions `Sheet` and `SheetDialog`.

## Accessibility

The grabber is a button ("Drag handle", `SkUiStrings.SheetGrabberText`) that moves the sheet to its next detent (round from the tallest to the lowest), with increment and decrement for the next detent up or down; it is not a Tab stop. The scrim is out of the semantics tree. With an `AutomationId` on the sheet, the scrim's is the sheet's plus `SkUiSheet.ScrimAutomationIdSuffix` (`_Scrim`) and the grabber's plus `GrabberAutomationIdSuffix` (`_Grabber`), so UI tests can tap them (the scrim as an id-only element while `SkUiAccessibility.ExposeAutomationIds`). Screen readers' own dismiss gestures (VoiceOver's two-finger scrub) are not wired: Back, Escape and the content's buttons close it.

## Limits

- Drawn inside its surface: it cannot cover hosted native views (`SkUiMauiContentView`; overlay masks are on the backlog) or native chrome outside the surface (a Shell tab bar, a navigation bar). It covers a drawn bottom bar in the same surface. A surface that is not the whole page (a drawn card among native views) shows its sheets inside that card.
- No Core twin: a sheet has to be on the surface root to cover the page, and a Core tree lives inside an `SkUiCoreHost`. Core content goes into a sheet through a host.
- Lower detents translate the sheet; they do not lay its content out at the detent's height.

## Related

[Popups.md](../design/Popups.md) · [`SkUiPicker`](SkUiPicker.md) · [`SkUiSwipeView`](SkUiSwipeView.md) · Gallery: `SheetDemoPage` · Leak scenario: `SheetOpenedAndClosed`
