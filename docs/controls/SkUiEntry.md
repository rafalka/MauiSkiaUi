# SkUiEntry

A single-line text field drawn by SkiaUi: the text, caret, selection, placeholder and chrome are drawn on the surface, and the platform's keyboard and input methods (composition, autocorrect, dictation, the edit menu) reach it through the surface's text input proxy. No native view is created for it, so it clips, scrolls, transforms and fades with the drawn tree, sits under drawn popups, and works in Core trees and in recycled list rows.

**MAUI counterpart:** [`Entry`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/entry), by member name: a page ports by changing the prefix. Beyond MAUI: `CaretColor`, `SelectionColor`, `AutofillHint`. Core twin: `SkUiCoreEntry` (fluent `Set*`, `INotifyPropertyChanged`; no enabled state: use `IsReadOnly`). The fields share their base with [`SkUiEditor`](SkUiEditor.md) and [`SkUiSearchBar`](SkUiSearchBar.md) (`SkUiInputView` / `SkUiCoreInputView`, MAUI's `InputView`); [`SkUiStepper`](SkUiStepper.md) edits its value with the same engine.

## How it works

- **Engine.** One engine for every drawn field on both layers ([TextInput.md](../design/TextInput.md)): the editing model (`SkUiTextEditor`: text, selection, the input method's composition, undo / redo), its layout on the shared text engine (`SkUiTextEditLayout`: HarfBuzz shaping, bidi, fallback fonts; carets and selection follow clusters and right-to-left runs), and the field model that draws, scrolls and handles input.
- **Platform input.** While the field has keyboard focus it is attached to the surface's text input ([proxy per platform](../design/TextInput.md#proxy-decision-d0)): Android's `InputConnection` on the surface, a hidden `UITextInput` view on iOS and Mac Catalyst, a hidden `TextBox` on Windows. The soft keyboard shows on focus and hides when focus leaves the fields (moving between fields keeps it). `Keyboard`, `ReturnType`, `IsPassword`, `IsSpellCheckEnabled`, `IsTextPredictionEnabled`, `TextTransform` and `AutofillHint` choose the keyboard, its return key, corrections and autofill, as MAUI's native entries map them.
- **Focus.** A tap focuses the field (and shows the keyboard again after it was dismissed); so do `Focus()`, Tab and a screen reader's activation. Text fields take focus also when accessibility is switched off (`SkUiAccessibility.IsEnabled`) and when they are not tab stops (`IsTabStop="False"` only takes them out of Tab). The field draws its own focus highlight (the look's focused chrome), not the focus ring.
- **Return.** `Completed`, then `ReturnCommand`. Then, as MAUI's native entries: `ReturnType.Next` moves focus to the next field (Tab order); otherwise iOS gives focus up and Android hides the keyboard.
- **Typing and keys.** Typed text arrives through the input method (composition is underlined until committed). Hardware keys: arrows (by grapheme; Ctrl or ⌥ by word; Shift selects), Home / End, ⌘ + arrows on Apple platforms, Backspace / Delete (by word with Ctrl / ⌥), the clipboard and undo shortcuts (Ctrl / ⌘ + A, C, X, V, Z, Y, Shift + Z). Undo joins typing word by word and a composition into one step; setting `Text` from code clears the undo history.
- **Pointer.** A mouse click places the caret, a drag selects, a double click selects a word and a triple click the text; a right click opens the edit menu. On touch, a tap places the caret (a tap on the caret toggles the edit menu), a double tap selects a word, and a long press selects the word under it (a drag after it extends the selection by words); the selection then shows handles to drag its ends, and the platform's edit menu (cut, copy, paste, select all) opens over it. The caret moves by touch too: on Android a tap shows a handle under the caret (for a few seconds) that drags it; elsewhere the caret itself is dragged, as on iOS. The platform's magnifier (Android's magnifier, iOS's loupe) shows the text under the finger while a handle or the caret moves. A drag that does not start on a handle or the caret scrolls the page.
- **Scrolling.** Text longer than the field scrolls horizontally to keep the caret in view. In a drawn scroller, a focused field is kept above the soft keyboard ([keyboard avoidance](../design/TextInput.md#integration-d6)).
- **Caret.** A bar in `CaretColor` (else the look's), blinking by `SkUiLook.CaretBlinkInterval`: a composite-time drawable, so moving or blinking it records nothing; typing shows it solid.
- **Password.** `IsPassword` draws one bullet per character (grapheme), turns off copying, cutting and suggestions, and asks for a password keyboard (and the password autofill hint).
- **Screen readers.** An editable text field (TalkBack's `EditText`, Narrator's Edit with a settable value, VoiceOver's value): the placeholder is its name, the text (bullets for a password) its value, with the selection; TalkBack and Narrator can set the text and the selection.

## Look

The look draws the chrome behind the text: `SkUiLook.DrawTextField(canvas, SkUiTextFieldPaint)` (or `TextFieldPainter`), with the field's bounds, accent, border and text colors, how focused it is (0–1, the `FieldFocus` transition), pointer over, enabled, read-only, multiline and right to left. `DefaultSkUiLook.TextFieldStyle` picks `Underline`, `Outline` or `Filled` (`Default`: the look's `DefaultTextFieldStyle`, an outline; `AndroidSkUiLook`: Material's underline). The clear button: `DrawTextFieldClearButton` (or `TextFieldClearButtonPainter`); selection handles: `DrawTextSelectionHandle` (or `TextSelectionHandlePainter`). The caret's handle (where `ShowsTextInsertionHandle`, default on Android): `DrawTextInsertionHandle` (or `TextInsertionHandlePainter`), `GetTextInsertionHandleBounds`, `TextInsertionHandleTimeout` (4 s). Sizes and timing: `DefaultTextFieldMinimumHeight` (44), `TextFieldPadding` (12, 8), `TextFieldClearButtonWidth` (32), `CaretWidth` (2), `CaretBlinkInterval` (500 ms; zero: no blinking), `CaretColor` and `TextSelectionColor` (the accent, and the accent at 30 %), `TextSelectionHandleTouchRadius` (22).

## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior: DIP coordinates; `BindableProperty` + fluent `Set*` setters (invalid values: `Set*` throws, XAML and bindings ignore them with a warning); `StartUpdating` / `EndUpdating`; SkiaUi's gesture arena. User input writes back: typing sets `Text` (two-way by default), moving the caret sets `CursorPosition` / `SelectionLength`.

## How to use

```xml
<sk:SkUiEntry Placeholder="Email" Keyboard="Email" ReturnType="Next"
              Text="{Binding Email}" ClearButtonVisibility="WhileEditing" />
<sk:SkUiEntry Placeholder="Password" IsPassword="True" ReturnCommand="{Binding SignInCommand}" />
```

```csharp
var name = new SkUiEntry().SetPlaceholder("Name").SetMaxLength(40).SetReturnType(ReturnType.Done);
name.Completed += (_, _) => Save(name.Text);

var core = new SkUiCoreEntry().SetPlaceholder("Search").SetKeyboard(Keyboard.Plain);
```

## Key properties

| Property | Default | Notes |
| --- | --- | --- |
| `Text` | `null` | Two-way; `TextChanged` with MAUI's `TextChangedEventArgs`. Text longer than `MaxLength` is cut |
| `Placeholder`, `PlaceholderColor` | `null` | The color scheme's muted color by default |
| `TextColor`, `FontFamily`, `FontSize` (16), `FontAttributes`, `FontAutoScalingEnabled`, `CharacterSpacing` | | As labels |
| `HorizontalTextAlignment`, `VerticalTextAlignment` | Start, Center | Start follows the text's direction |
| `MaxLength` | `int.MaxValue` | Input is cut there (never inside a surrogate pair) |
| `IsReadOnly` | `false` | Selectable and copyable; no caret, no keyboard |
| `Keyboard`, `ReturnType`, `IsSpellCheckEnabled`, `IsTextPredictionEnabled` | Default, Default, true, true | The platform keyboard's options |
| `IsPassword` | `false` | Bullets; no copying or suggestions |
| `TextTransform` | Default | Upper / lower case: applied to input, to text set by the app and, when it changes, to the text already there; `Text` follows the transformed text |
| `CursorPosition`, `SelectionLength` | 0 | Follow the user; setting them selects |
| `ClearButtonVisibility` | Never | `WhileEditing`: a clear button while focused with text |
| `ReturnCommand`, `ReturnCommandParameter` | | Run after `Completed` |
| `CaretColor` (SkiaUi) | `null` | The look's `CaretColor` |
| `SelectionColor` (SkiaUi) | `null` | The look's `TextSelectionColor` |
| `AutofillHint` (SkiaUi) | `null` | `SkUiAutofillHints` (`Username`, `Password`, `NewPassword`, `OneTimeCode`, …) or a platform's own hint; a password field is a password |

## Differences from MAUI Entry

| Topic | SkiaUi |
| --- | --- |
| Native view | None: the field is drawn; the platform's input talks to it through a proxy |
| Default font size | 16 (as SkiaUi's labels), not the platform's |
| `TextTransform` | The field holds the transformed text and `Text` follows it (a two-way binding gets it), as MAUI's Android and iOS fields push back the text they show; `Default` leaves text as it is (it does not restore the original case) |
| Selection UI | Drawn handles (iOS-style bars for the selection, Android's drop under the caret) and the platform's edit menu and magnifier (Android 9+, iOS 17+) |
| Autofill | `AutofillHint` on iOS (text content type); Android autofill of drawn fields is not wired yet |
| Device verification | Typing, Gboard suggestions and the edit menu checked on an Android emulator and the iOS simulator; CJK composition, dictation and password managers still to be checked on devices ([plan](../design/ImplementationPlan.md#phase-d--drawn-text-input-fr-16-amended)) |

## Related

[`SkUiEditor`](SkUiEditor.md) · [`SkUiSearchBar`](SkUiSearchBar.md) · [`SkUiStepper`](SkUiStepper.md) · [`SkUiMauiContentView`](SkUiMauiContentView.md) (a hosted native `Entry`, for native-only needs) · [TextInput.md](../design/TextInput.md) · Gallery: `EntryDemoPage`
