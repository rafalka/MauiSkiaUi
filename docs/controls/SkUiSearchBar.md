# SkUiSearchBar

A search field drawn by SkiaUi: an entry with the look's search field (a search icon at the start), a clear button whenever it has text, and the keyboard's search key. A composition of the entry's engine, not a new editor: see [`SkUiEntry`](SkUiEntry.md) for focus, keys, selection, platform input and screen readers.

**MAUI counterpart:** [`SearchBar`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/searchbar), by member name. Core twin: `SkUiCoreSearchBar`.

## How it works

- **Searching.** Return (the soft keyboard's search key; `ReturnType` is `Search` by default) raises `SearchButtonPressed`, then runs `SearchCommand` with `SearchCommandParameter`. On phones the search bar then gives focus up (the keyboard goes), as MAUI's search bars on iOS and Android.
- **Clearing.** The clear button shows whenever there is text (also unfocused, as MAUI's), in `CancelButtonColor`.
- **Leading and trailing views.** As the entry's ([`LeadingView` / `TrailingView`](SkUiEntry.md#how-it-works)): the leading view comes after the search icon, the trailing view after the clear button (a voice-search or filter button).
- **Look.** `SkUiLook.DrawSearchField(canvas, SkUiSearchFieldPaint)` (or `SearchFieldPainter`): a lightly filled rounded field with a magnifying glass in `SearchIconColor` in `SearchIconWidth` (28) at the start side; an accent outline while focused.

## How to use

```xml
<sk:SkUiSearchBar Placeholder="Search" Text="{Binding Query}"
                  SearchCommand="{Binding SearchCommand}" SearchCommandParameter="{Binding Query}" />
```

## Key properties

The entry's text properties (`Text`, `Placeholder`, `PlaceholderColor`, `TextColor`, fonts, alignment, `MaxLength`, `IsReadOnly`, `Keyboard`, `CursorPosition`, `SelectionLength`, `InputFormatters`, `FormatsAssignedText`, `LeadingView`, `TrailingView`, …), plus `SearchCommand`, `SearchCommandParameter`, `CancelButtonColor`, `SearchIconColor`, `ReturnType` (Search). Events: `SearchButtonPressed`, `TextChanged`.

## Related

[`SkUiEntry`](SkUiEntry.md) · Gallery: `SearchBarDemoPage`
