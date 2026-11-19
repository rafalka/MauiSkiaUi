# Pickers of your own

SkiaUi's pickers are built on one engine: the drawn field (text, placeholder, chevron, clear button, states), `IsOpen` with `Opened` / `Closed`, the popup's placement and size, your app's popup presenter, OK / Cancel and Clear, the bottom pane, keyboard and screen readers. Since 1.12 an app can put that engine to work for values SkiaUi has no picker for: a color, a duration, a rating, a node of a tree, a range.

**MAUI counterpart:** none. A MAUI app writes its own control or a custom `Picker` handler for this; with SkiaUi it derives a picker class and draws only the popup's body.

## Which way

| You want | Do this |
| --- | --- |
| Your own UI for a type SkiaUi already picks (a platform list, a calendar for a date) | Override `ShowPopupAsync` (or `SelectItemsAsync` on `SkUiPicker`) and answer the typed request: [SkUiPicker](SkUiPicker.md#popups-on-your-popup-system), [SkUiDatePicker](SkUiDatePicker.md) |
| One value of a type of your own | Derive [`SkUiValuePicker<T>`](#a-value-picker) (Core: `SkUiCoreValuePicker<T>`) |
| Several values, or field text rules of your own | Derive [`SkUiPickerBase`](#on-the-picker-base) (Core: `SkUiCorePickerBase`) |
| The picker's wheels without a field | [SkUiPickerWheels](SkUiPickerWheels.md) |

The samples app's **Pickers of your own** page has four complete pickers, written against SkiaUi's public API only (`samples/MauiSkiaUiSamples/Samples/Controls/Pickers/`):
- `ColorPicker`: swatches;
- `DurationPicker`: inline wheels with OK;
- `ActionSheetColorPicker`: the app's own UI;
- `CoreRatingPicker`: Core.

## A value picker

`SkUiValuePicker<T>` holds `Value` and leaves two things to you: the field's text and the popup's body.

```csharp
public class ColorPicker : SkUiValuePicker<Color?>
{
    protected override string? FormatValue(Color? value) => NameOf(value) ?? value?.ToHex();

    protected override SkUiValuePopupContent<Color?> CreateValueContent(SkUiPickerPopupContext context) => new SwatchGrid();

    private sealed class SwatchGrid : SkUiValuePopupContent<Color?>
    {
        protected override SkUiView CreateBody()
        {
            var grid = new SkUiGrid { Padding = new Thickness(16), RowSpacing = 12, ColumnSpacing = 12 };
            // ... a button per swatch:
            button.Clicked += (_, _) =>
            {
                if (Context.RequiresAccept)
                    SetValue(color);   // a draft: OK applies it
                else
                    Complete(color);   // chosen: applied, and the popup closes
            };
            return grid;
        }

        protected override void OnValueChanged() => ShowChoice(); // ring the swatch of Value
    }
}
```

```xml
<local:ColorPicker Title="Label color" Placeholder="Choose a color" Value="{Binding LabelColor}" IsClearButtonVisible="True" />
```

- **Closed types.** Derive one per value type. XAML then needs no type arguments, and the class is the place for the popup's body. Both value pickers are abstract.
- **None.** `null` means nothing is chosen: the field shows `Placeholder` (else `Title`) and no clear button. A value type that can be empty is picked as its nullable (`SkUiValuePicker<TimeSpan?>`). Override `HasValue` when another value means none.
- **Value.** `Value` binds two-way. The user's choices are written as handler values, so an app's one-way binding keeps working after the user picks. Clearing (the field's clear button, Delete, the popup's Clear, `ClearSelection()`) sets it to `default`. `SelectionChangedCommand` runs, then `ValueChanged` (`SkUiValueChangedEventArgs<T>`).
- **Field text.** The field shows `ValueFormat` (`string.Format` with the value as `{0}`, in the current culture: `"{0:hh\:mm}"`), else `FormatValue(value)`, else the value's `ToString()`. `DescribeValue(value)` is what screen readers read when it should differ (a color's name for a hex code). Call `RefreshFieldText()` when what they read changes (a culture switch).
- **Generic subclasses.** `T` is annotated `[DynamicallyAccessedMembers(PublicParameterlessConstructor)]` (what `BindableProperty.Create` asks under trimming), so a generic subclass of yours repeats the annotation; closed types need nothing.

### The popup's body

`SkUiValuePopupContent<T>` is created per opening (`CreateValueContent`), and SkiaUi shows its body in the popup chrome:
- the title of window popups;
- the app's `PopupHeaderTemplate` / `PopupFooterTemplate`;
- OK / Cancel with `UpdateMode="WhenFinished"`;
- Clear with `IsPopupClearButtonVisible`;
- the bottom pane's toolbar.

| Member | |
| --- | --- |
| `CreateBody()` | Builds the drawn body (SkUi\* views) once, when the popup is built |
| `Context` | `SkUiPickerPopupContext`: `Placement`, `Title`, `IsRightToLeft`, the picker's font, `TextColor`, `SelectionBackground`, `RequiresAccept`, the button texts, `IsClearButtonVisible` |
| `Value` | What the popup shows: the picker's value when it opened, then the user's changes (a draft while the popup waits for OK) |
| `SetValue(value)` | The user changed it: applied at once, or kept for OK. The popup stays open |
| `Complete(value)` | Chosen: applied (also when the popup waits for OK, as if OK was pressed) and the popup closes |
| `OnValueChanged()` | `Value` changed (the user, the Clear button, the app changing the picker's value while open): the body follows |
| `PreferredSize(min, max, maxHeight)` | The body's size (by default it is measured); SkiaUi adds the chrome |
| `MinimumHeight` | How short an anchored popup may make the body beside the field before it covers the field instead. `NaN` (default): a few rows, for a body that scrolls. `double.PositiveInfinity`: the whole body, for a form that cannot shrink (a calendar with buttons). Negative throws |
| `Request`, `IsOpen`, `Body` | The popup's request while open, and the body built |

**Immediately or on OK.** Without `UpdateMode="WhenFinished"`, each `SetValue` changes the picker at once. A body for one tap (swatches) calls `Complete` instead, and one that is turned or dragged (wheels, a slider) calls `SetValue` and stays open until a dismissal. With it, `SetValue` keeps a draft that OK (or Enter) applies, and Cancel, Escape and a dismissal drop it. The popup is placed, sized, dimmed and closed like every picker's: the picker disabled, hidden, removed or `IsOpen = false`, an anchored popup's scroller scrolled, the window resized.

**Anchored forms stay whole.** By default an anchored popup is cut down to the room beside its field, and the body scrolls or is clipped. A body that cannot usefully shrink sets `MinimumHeight = double.PositiveInfinity` in its constructor or `CreateBody`. The popup then keeps its whole height: placed beside the field when it fits on a side, otherwise moved over the field, always inside the window's safe area less the look's `PopupMargin` (and limited to that area when it is taller). A number in between lets it shrink down to that height first. The built-in calendar and wheel popups always behave this way.

**Wheels as a body.** [SkUiPickerWheels](SkUiPickerWheels.md) makes a good body. Set its `CommitDelay` to zero (the value picker's draft already waits for OK), turn the columns' `SelectionChanged` into `SetValue`, and set the columns from `Value` in `OnValueChanged`, as the samples' `DurationPicker` does.

### Your own UI instead

A presenter, or the picker's `ShowPopupAsync` override, can show its own UI instead of `request.Content` (a platform color dialog). It answers the typed request, `SkUiValuePopupRequest<T>`:
- `Value` (as it opened);
- `SetValue(value)` as the user changes it (at once, or a draft with `RequiresAccept`, then `Accept()`);
- `Complete(value)` to choose and close;
- `Clear()` for none, or `Cancel()`.

Close your UI when the token is cancelled, and return once it is gone; the picker applies the answer and raises its events as with the drawn body.

```csharp
protected override async Task ShowPopupAsync(SkUiPopupRequest request, CancellationToken token)
{
    if (request is not SkUiValuePopupRequest<Color?> colors || request.Page is not { } page)
    {
        await base.ShowPopupAsync(request, token);
        return;
    }
    var chosen = await page.DisplayActionSheetAsync(Title, request.CancelText, null, names);
    if (!token.IsCancellationRequested && chosen is not null && chosen != request.CancelText)
        colors.Complete(ColorNamed(chosen));
}
```

## On the picker base

For a picker that holds something other than one value (a range of two dates, several tags with rules of your own), derive `SkUiPickerBase` (or `SkUiCorePickerBase`) and keep the state yourself:

- `SetFieldText(text)` / `SetFieldText(text, description)`: what the field shows for the current choice (`null`: nothing chosen), and what screen readers read when it differs. Call it whenever the choice changes; an open popup's content hears of it.
- `OnClearSelection()`: the choice is cleared (the clear buttons, Delete on the field, `ClearSelection()`): set it to none and call `SetFieldText(null)`.
- `CreatePopupContent(context)`: return an `SkUiPickerPopupContent`, the non-generic base of the value content:
  - `CreateBody()`;
  - `OnAccepted()`: OK or Enter, apply your draft;
  - `OnCleared()`, `OnPickerChanged()` and `OnClosed()`;
  - `Close()` and `Accept()` for the body to call.
  
  Apply choices to your picker yourself. The content may hold a reference to it: the popup's request lets go of the content once the popup has gone.
- `ClosePopup()` closes the popup from the picker's side.
- A presenter sees an `SkUiCustomPopupRequest`: `Accept()` runs `OnAccepted` and closes, `Clear()` clears the picker and closes.

`CreatePopupContent` returning `null` (the default) keeps the picker's own content, which is how the built-in pickers keep their lists and wheels.

## Things to know

- **Presenters.** The popup goes to the picker's `PopupPresenter`, else `SkUiPopups.Presenter`. MAUI's action sheet, the fallback when none is registered, shows lists only: a picker of your own's popup does not open there (one `Trace` line). Register a presenter (the CommunityToolkit one in `SkiaUi.Maui.CommunityToolkit`, or your app's), or answer the request yourself as above.
- **Bodies are drawn views.** The body is a tree of SkUi\* views in the popup's own drawn surface. Native views go in through `SkUiMauiContentView`, as anywhere in SkiaUi.
- **Keys.** The popup takes keyboard focus when shown: Enter accepts and Escape cancels. A view of the body that takes focus (an entry) handles its own keys.
- **Everything else is the picker's:** `Title`, `Placeholder`, `TextColor` and the font, field chrome and `LeadingView`, `PopupPlacement`, `PopupWidth` / `MinPopupWidth` / `MaxPopupWidth`, `MaxPopupHeight`, `UpdateMode`, `AcceptText` / `CancelText`, the clear buttons and `ClearText`, the popup header and footer templates. See [SkUiPicker](SkUiPicker.md).
