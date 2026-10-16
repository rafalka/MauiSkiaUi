# SkUiEntry

A single-line text field drawn by SkiaUi: the text, caret, selection, placeholder and chrome are drawn on the surface, and the platform's keyboard and input methods (composition, autocorrect, dictation, the edit menu) reach it through the surface's text input proxy. No native view is created for it, so it clips, scrolls, transforms and fades with the drawn tree, sits under drawn popups, and works in Core trees and in recycled list rows.

**MAUI counterpart:** [`Entry`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/entry), by member name: a page ports by changing the prefix. Beyond MAUI: `CaretColor`, `SelectionColor`, `AutofillHint`, `InputFormatters` / `FormatsAssignedText`, `LeadingView` / `TrailingView`, `HasError` / `ErrorText` / `HelperText` / `ErrorColor`. Core twin: `SkUiCoreEntry` (fluent `Set*`, `INotifyPropertyChanged`; no enabled state: use `IsReadOnly`). The fields share their base with [`SkUiEditor`](SkUiEditor.md) and [`SkUiSearchBar`](SkUiSearchBar.md) (`SkUiInputView` / `SkUiCoreInputView`, MAUI's `InputView`); [`SkUiStepper`](SkUiStepper.md) edits its value with the same engine.

## How it works

- **Engine.** One engine for every drawn field on both layers ([TextInput.md](../design/TextInput.md)): the editing model (`SkUiTextEditor`: text, selection, the input method's composition, undo / redo), its layout on the shared text engine (`SkUiTextEditLayout`: HarfBuzz shaping, bidi, fallback fonts; carets and selection follow clusters and right-to-left runs), and the field model that draws, scrolls and handles input.
- **Platform input.** While the field has keyboard focus it is attached to the surface's text input ([proxy per platform](../design/TextInput.md#proxy-decision-d0)): Android's `InputConnection` on the surface, a hidden `UITextInput` view on iOS and Mac Catalyst, a hidden `TextBox` on Windows. The soft keyboard shows on focus and hides when focus leaves the fields (moving between fields keeps it). `Keyboard`, `ReturnType`, `IsPassword`, `IsSpellCheckEnabled`, `IsTextPredictionEnabled`, `TextTransform` and `AutofillHint` choose the keyboard, its return key, corrections and autofill, as MAUI's native entries map them.
- **Focus.** A tap focuses the field (and shows the keyboard again after it was dismissed); so do `Focus()`, Tab and a screen reader's activation. Text fields take focus also when accessibility is switched off (`SkUiAccessibility.IsEnabled`) and when they are not tab stops (`IsTabStop="False"` only takes them out of Tab). The field draws its own focus highlight (the look's focused chrome), not the focus ring.
- **Return.** `Completed`, then `ReturnCommand`. Then, as MAUI's native entries: `ReturnType.Next` moves focus to the next field (Tab order); otherwise iOS gives focus up and Android hides the keyboard.
- **Typing and keys.** Typed text arrives through the input method (composition is underlined until committed). Hardware keys: arrows (by grapheme; Ctrl or ⌥ by word; Shift selects), Home / End, ⌘ + arrows on Apple platforms, Backspace / Delete (by word with Ctrl / ⌥), the clipboard and undo shortcuts (Ctrl / ⌘ + A, C, X, V, Z, Y, Shift + Z). Undo joins typing word by word and a composition into one step; setting `Text` from code clears the undo history.
- **Pointer.** A mouse click places the caret, a drag selects, a double click selects a word and a triple click the text; a right click opens the edit menu. On touch, a tap places the caret (a tap on the caret toggles the edit menu), a double tap selects a word, and a long press selects the word under it (a drag after it extends the selection by words); the selection then shows handles to drag its ends, and the platform's edit menu (cut, copy, paste, select all) opens over it. The caret moves by touch too: on Android a tap shows a handle under the caret (for a few seconds) that drags it; elsewhere the caret itself is dragged, as on iOS. The platform's magnifier (Android's magnifier, iOS's loupe) shows the text under the finger while a handle or the caret moves. A drag that does not start on a handle or the caret scrolls the page.
- **Scrolling.** Text longer than the field scrolls horizontally to keep the caret in view. In drawn scrollers (`SkUiScrollView`, `SkUiCoreScrollView`, nested ones too), a focused field is kept above the soft keyboard ([keyboard avoidance](../design/TextInput.md#integration-d6)).
- **Caret.** A bar in `CaretColor` (else the look's), blinking by `SkUiLook.CaretBlinkInterval`: a composite-time drawable, so moving or blinking it records nothing; typing shows it solid.
- **Input formatters.** `InputFormatters` rewrite or refuse the user's edits, in order (Flutter's `TextInputFormatter`): typing, an input method's committed text (a composition is formatted when committed, not while composed), paste, cut, Backspace / Delete and a screen reader's set-text. Each gets the value before the edit and the value after it (`SkUiTextEditValue`: text and selection) and returns the value the field takes, so a formatter can filter, cap, re-mask the whole text and place the caret. What the last returns is one edit and one undo step; undo / redo are not formatted, and text the app sets is formatted only with `FormatsAssignedText` (as if pasted into an empty field; `Text` follows the result). Built in: `SkUiFilteringFormatter` (allow or deny by a regular expression or a character predicate, `DigitsOnly`), `SkUiLengthLimitingFormatter` (graphemes, digits, or letters and digits) and `SkUiMaskFormatter` (`0` digit, `L` letter, `A` letter or digit, `&` any, `\` escapes; literals typed in as the user reaches them; `Unmask`, `IsComplete`). `SkUiTextInputFormatter.Create` wraps a function; a subclass overrides `Format`. `MaxLength` still holds after them.
- **Leading and trailing views.** `LeadingView` and `TrailingView` put any drawn view inside the field's chrome, before and after the text (Material's leading / trailing icons and prefix / suffix text; Avalonia's `InnerLeftContent` / `InnerRightContent`): an `SkUiImage` icon, an `SkUiLabel` unit, an `SkUiImageButton`. Each is measured at its own size (a taller one makes the field taller), placed inside the padding with `TextFieldAdornmentSpacing` (8) to the text, and aligned with the text (`VerticalTextAlignment`: centered in an entry, at the top of an editor); the text is inset so they never overlap, and they mirror in right-to-left. A search bar's icon comes before the leading view; the clear button sits between the text and the trailing view. A press on a view that handles pointers itself (a button, a view with a tap gesture) is the view's: it does not focus the field, move its caret or (on Windows) take focus from it, so a button can act on a focused field with its keyboard up. A press on an icon or a label focuses the field. The views are the field's children: they inherit its binding context, a hidden one (`IsVisible="False"`) takes no room, and screen readers read a label's text with the field's name and a button as its own element. A view is in one slot of one field.
- **Validation chrome.** `HasError` marks the input invalid (Material 3's error state; Telerik's `Invalid`): the look draws the outline or underline in the error color (`ErrorColor`, else the color scheme's `Error`), thicker while focused. `HelperText` is a message under the field (Material's supporting text: a hint that stays while the user types, unlike `Placeholder`) in the muted color; while `HasError` is set, `ErrorText` takes its place in that color (an empty `ErrorText` keeps the helper text, in the error color). The message wraps at the field's width and makes the field taller (an unconstrained field is at least as wide as its message); in a field of fixed height it takes its room from the field while the field keeps its own height, and hangs below the field otherwise (over what follows: leave room, or let the field size itself). A press on it does not focus the field. The field does not validate: the app sets `HasError` (bound to a view model's error, a Community Toolkit validation behavior's `IsNotValid`, or set in `TextChanged`), so it complements whatever validation the app uses. Visual states while enabled: `Invalid`, `InvalidFocused` (focused, when defined; else `Invalid`) and `Valid`, in a group of their own (`ValidityStates`) or as `Invalid` / `InvalidFocused` in `CommonStates`, where they take the place of the field's state. Screen readers read the message after the field; TalkBack also hears the error as an `EditText`'s error.
- **Password.** `IsPassword` draws one bullet per character (grapheme), turns off copying, cutting and suggestions, and asks for a password keyboard (and the password autofill hint).
- **Screen readers.** An editable text field (TalkBack's `EditText`, Narrator's Edit with a settable value, VoiceOver's value): the placeholder is its name, the text (bullets for a password) its value, with the selection; TalkBack and Narrator can set the text and the selection. The helper or error text is read after it (TalkBack: the hint, or the `EditText`'s error and invalid state).

## Look

The look draws the chrome behind the text: `SkUiLook.DrawTextField(canvas, SkUiTextFieldPaint)` (or `TextFieldPainter`), with the field's bounds (above its message), accent, border and text colors, how focused it is (0–1, the `FieldFocus` transition), pointer over, enabled, read-only, multiline, right to left, and an error (`HasError`, and `Error`: the field's `ErrorColor`, else the scheme's). `DefaultSkUiLook.TextFieldStyle` picks `Underline`, `Outline` or `Filled` (`Default`: the look's `DefaultTextFieldStyle`, an outline; `AndroidSkUiLook`: Material's underline). The clear button: `DrawTextFieldClearButton` (or `TextFieldClearButtonPainter`); selection handles: `DrawTextSelectionHandle` (or `TextSelectionHandlePainter`). The caret's handle (where `ShowsTextInsertionHandle`, default on Android): `DrawTextInsertionHandle` (or `TextInsertionHandlePainter`), `GetTextInsertionHandleBounds`, `TextInsertionHandleTimeout` (4 s). Sizes and timing: `DefaultTextFieldMinimumHeight` (44), `TextFieldPadding` (12, 8), `TextFieldClearButtonWidth` (32), `TextFieldAdornmentSpacing` (8, between a leading or trailing view and the text), `TextFieldMessageSpacing` (4, between the field and its message) and `TextFieldMessageFontSize` (12), `CaretWidth` (2), `CaretBlinkInterval` (500 ms; zero: no blinking), `CaretColor` and `TextSelectionColor` (the accent, and the accent at 30 %), `TextSelectionHandleTouchRadius` (22).

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

Input formatters (mask-as-you-type, filters, caps):

```xml
<sk:SkUiEntry Placeholder="Phone" Keyboard="Telephone" Text="{Binding Phone}" FormatsAssignedText="True">
    <sk:SkUiEntry.InputFormatters>
        <sk:SkUiMaskFormatter Mask="(000) 000-0000" />
    </sk:SkUiEntry.InputFormatters>
</sk:SkUiEntry>
<sk:SkUiEntry Placeholder="Code" Keyboard="Numeric">
    <sk:SkUiEntry.InputFormatters>
        <sk:SkUiFilteringFormatter Pattern="[0-9]" />
        <sk:SkUiLengthLimitingFormatter MaxLength="6" />
    </sk:SkUiEntry.InputFormatters>
</sk:SkUiEntry>
```

Validation chrome (a helper text, an error the view model reports):

```xml
<sk:SkUiEntry Placeholder="Email" Keyboard="Email" Text="{Binding Email}"
              HelperText="Your work address"
              HasError="{Binding HasEmailError}" ErrorText="{Binding EmailError}">
    <VisualStateManager.VisualStateGroups>
        <VisualStateGroup x:Name="ValidityStates">
            <VisualState x:Name="Valid" />
            <VisualState x:Name="Invalid">
                <VisualState.Setters>
                    <Setter Property="TextColor" Value="DarkRed" />
                </VisualState.Setters>
            </VisualState>
        </VisualStateGroup>
    </VisualStateManager.VisualStateGroups>
</sk:SkUiEntry>
```

```csharp
var age = new SkUiEntry().SetPlaceholder("Age").SetKeyboard(Keyboard.Numeric).SetErrorText("Enter a whole number");
age.TextChanged += (_, e) => age.HasError = !int.TryParse(e.NewTextValue, out _);
var core = new SkUiCoreEntry().SetPlaceholder("Code").SetHelperText("6 digits");
```

Leading and trailing views (icons, units, buttons):

```xml
<sk:SkUiEntry Placeholder="Weight" Keyboard="Numeric" Text="{Binding Weight}">
    <sk:SkUiEntry.LeadingView>
        <sk:SkUiImage Source="scale.png" WidthRequest="20" HeightRequest="20" />
    </sk:SkUiEntry.LeadingView>
    <sk:SkUiEntry.TrailingView>
        <sk:SkUiLabel Text="kg" TextColor="Gray" />
    </sk:SkUiEntry.TrailingView>
</sk:SkUiEntry>
```

```csharp
var code = new SkUiEntry().SetPlaceholder("Coupon")
    .SetTrailingView(new SkUiImageButton { Source = "qr.png", WidthRequest = 24, HeightRequest = 24, Command = ScanCommand });
var core = new SkUiCoreEntry().SetPlaceholder("Amount").SetLeadingView(new SkUiCoreLabel().SetText("$"));
```

```csharp
var tag = new SkUiEntry().SetInputFormatters(
    new SkUiFilteringFormatter(@"\s", SkUiFilterMode.Deny, "-"),
    SkUiTextInputFormatter.Create((old, value) => value with { Text = value.Text.ToLowerInvariant() }));
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
| `InputFormatters` (SkiaUi) | empty | `SkUiTextInputFormatter`s run in order on the user's edits (see above); changing them formats the text already in the field, and `Text` follows |
| `FormatsAssignedText` (SkiaUi) | `false` | Text the app sets goes through `InputFormatters` too, and `Text` follows; turning it on formats the text already there |
| `LeadingView`, `TrailingView` (SkiaUi) | `null` | Drawn views inside the field before and after the text (Core: `SkUiCoreNode`s); see above |
| `HasError` (SkiaUi) | `false` | Error chrome: the outline and message in the color scheme's `Error`; visual states `Invalid` / `InvalidFocused` / `Valid` |
| `ErrorText` (SkiaUi) | `null` | The message under the field while `HasError`, in place of `HelperText` |
| `HelperText` (SkiaUi) | `null` | A message under the field (muted); wraps and makes the field taller |
| `ErrorColor` (SkiaUi) | `null` | The error chrome's and message's color; the color scheme's `Error` by default |
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
