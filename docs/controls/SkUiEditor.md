# SkUiEditor

A multiline text field drawn by SkiaUi: wrapped lines, Return inserts a line break, and the text scrolls inside the field. Everything else (the engine, platform input, focus, keys, pointer selection, the caret, screen readers, the look) is the entry's: see [`SkUiEntry`](SkUiEntry.md).

**MAUI counterpart:** [`Editor`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/editor), by member name. Core twin: `SkUiCoreEditor`.

## How it works

- **Lines.** Text wraps at the field's width (words, then characters); paragraphs are laid out separately and an edit re-shapes only the paragraph it changed. Up / Down and Page Up / Down move by lines keeping the caret's x; Home / End go to the visual line's ends (End stays on a wrapped line).
- **Scrolling.** The text scrolls vertically to keep the caret in view while typing. A wheel or trackpad scrolls the text and hands what it cannot use to the scrollers around it; a vertical touch drag scrolls the text while it can scroll that way (otherwise the page scrolls).
- **`AutoSize`.** `TextChanges` measures the editor again as its text changes (it grows and shrinks with its lines); `Disabled` (default) keeps the size it was measured at, and typing does not measure it again.
- **Line breaks** are `\n`: text set with `\r\n` or `\r` (MAUI's `Editor` on Windows gives `\r`) shows its lines, and `Text` follows with `\n`, as it follows a cut to `MaxLength`. An entry shows the app's line breaks as spaces and keeps its text.
- **`Completed`** is raised when the editor loses focus, as MAUI's.
- **Return** inserts a line break (the soft keyboard shows a line-break key).
- **`VerticalTextAlignment`** is Start by default (the entry's is Center).

## How to use

```xml
<sk:SkUiEditor Placeholder="Notes" AutoSize="TextChanges" Text="{Binding Notes}" MaxLength="500" />
<sk:SkUiEditor Placeholder="Description" HeightRequest="160" />
```

## Key properties

`Text` (two-way), `Placeholder`, `PlaceholderColor`, `TextColor`, font properties, `CharacterSpacing`, `HorizontalTextAlignment`, `VerticalTextAlignment` (Start), `MaxLength`, `IsReadOnly`, `Keyboard`, `IsSpellCheckEnabled`, `IsTextPredictionEnabled`, `TextTransform`, `CursorPosition`, `SelectionLength`, `AutoSize`; SkiaUi: `CaretColor`, `SelectionColor`, `AutofillHint`, `InputFormatters`, `FormatsAssignedText` ([input formatters](SkUiEntry.md#how-it-works)), `LeadingView` / `TrailingView` ([leading and trailing views](SkUiEntry.md#how-it-works); at the top of the text by default, as `VerticalTextAlignment`), `HasError` / `ErrorText` / `HelperText` / `ErrorColor` ([validation chrome](SkUiEntry.md#how-it-works); with a fixed height the message takes its room from the editor, down to the editor's own height, then hangs below it). The [field chrome](SkUiEntry.md#field-chrome): `FieldStyle` (`None` inside a border of the app's), `TextPadding`, `FillColor`, `BorderColor`, `BorderWidth`, `CornerRadii` / `CornerRadius`. Events: `TextChanged`, `Completed`. See [`SkUiEntry`](SkUiEntry.md#key-properties).

## Related

[`SkUiEntry`](SkUiEntry.md) · [TextInput.md](../design/TextInput.md) · Gallery: `EditorDemoPage`
