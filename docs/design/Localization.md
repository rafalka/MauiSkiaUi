# Localization: SkiaUi's own texts

SkiaUi shows or reads out only a few texts of its own; everything else (labels, titles, items) comes from the app. Those few are in one object, `SkUiStrings`, so an app translates them in one place and switches them at once, as it switches the look ([ControlLook.md](ControlLook.md)).

| Text | Default | Where |
| --- | --- | --- |
| `AcceptText` | "OK" | The OK button of pickers with `UpdateMode="WhenFinished"` ([SkUiPicker](../controls/SkUiPicker.md#when-a-choice-takes-effect)), unless the picker sets its own `AcceptText` |
| `CancelText` | "Cancel" | Their Cancel button (unless the picker sets its own `CancelText`), and the cancel button of MAUI's action sheet (the fallback presenter) |
| `ClearText` | "Clear" | The Clear button of picker popups (`IsPopupClearButtonVisible`), unless the picker sets its own `ClearText` |
| `SelectAllText` | "Select all" | The "Select all" row of picker lists with several choices (`IsSelectAllVisible`), unless the picker sets its own `SelectAllText` |
| `MoreTokensFormat` | "+{0}" | The token that stands for the chosen items that do not fit in a picker's field (`MaxTokenLines`), with their count as `{0}` |
| `LoadMoreText` | "Load more" | The default load-more button of `SkUiCollectionView` (`LoadMoreMode="Manual"`) |
| `SelectedStateText` | "Selected" | What screen readers read as the value of a selected collection view item (also a chosen row of a picker with several choices) |
| `ExpandedStateText` / `CollapsedStateText` | "Expanded" / "Collapsed" | What screen readers read after the header of an `SkUiExpander` or a collapsible collection view group |

## How to translate

**Set the texts** of an instance and make it current, at startup or on a language switch:

```csharp
builder.UseSkiaUi(options => options.Strings = new SkUiStrings
{
    AcceptText = "Übernehmen", CancelText = "Abbrechen", LoadMoreText = "Mehr laden",
    SelectedStateText = "Ausgewählt", ExpandedStateText = "Erweitert", CollapsedStateText = "Reduziert"
});

// Later, another language: replace it as a whole.
SkUiStrings.Current = germanStrings;
```

**Or derive** and read the app's resources in the getters. They are read each time a text is needed (a popup opens, a screen reader reads an element), so they follow `CultureInfo.CurrentUICulture`:

```csharp
public sealed class AppSkUiStrings : SkUiStrings
{
    public override string AcceptText => AppResources.Ok;
    public override string CancelText => AppResources.Cancel;
    public override string LoadMoreText => AppResources.LoadMore;
    public override string SelectedStateText => AppResources.Selected;
    public override string ExpandedStateText => AppResources.Expanded;
    public override string CollapsedStateText => AppResources.Collapsed;
}

builder.UseSkiaUi(options => options.Strings = new AppSkUiStrings());

// After switching the culture:
SkUiStrings.NotifyChanged();
```

## What follows a change

Replacing `SkUiStrings.Current`, setting a text on the current instance, or `SkUiStrings.NotifyChanged()` raises `SkUiStrings.CurrentChanged` (a weak event: subscribers are not kept alive):

- Live surfaces rebuild what screen readers read.
- Drawn texts SkiaUi created take the new text (the load-more button).
- Popups read the texts when they open; an open popup keeps the texts it opened with.

Change the texts on the UI thread.

Texts a control takes from the app win over these: a picker's `AcceptText` / `CancelText` (bindable, so they can bind to a localization service), and a presenter that shows its own UI for a typed request instead of the drawn content (its own buttons, its own texts).

New texts SkiaUi shows go into `SkUiStrings` (a virtual property with an English default), never into a control.
