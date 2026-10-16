# SkUiNumericEntry

An entry for numbers only, drawn by SkiaUi, with optional − / + buttons inside its chrome: the value is shown formatted, edited as a plain number, and stepped by buttons, keys and the wheel.

**MAUI counterpart:** none. MAUI apps use an [`Entry`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/entry) with `Keyboard="Numeric"` and a converter, or a vendor control: Syncfusion's `SfNumericEntry` / `SfNumericUpDown`, Telerik's `RadNumericInput`. `SkUiNumericEntry` is on `double?` as both vendors' are; `SkUiNumericEntry<T>` takes any number type (`int`, `long`, `decimal`, `float`, …). Core twins: `SkUiCoreNumericEntry`, `SkUiCoreNumericEntry<T>`. It is a [`SkUiEntry`](SkUiEntry.md) (the same field engine, keyboard proxy, `LeadingView` / `TrailingView`, validation chrome), not a new editor.

## How it works

- **Value.** `Value` (two-way) is `T?`: `null` while the field is empty (`AllowNull`, the default; the placeholder shows), else within `Minimum`–`Maximum` (default the type's whole range) and rounded to `MaximumDecimalDigits` (half away from zero). With `AllowNull="False"`, empty text commits 0 clamped into the range. A value the app sets is clamped at once, and the value it asked for comes back when the range widens (as the steppers'). `ValueChanged` (synchronous) carries the old and new value (`SkUiValueChangedEventArgs<T?>`).
- **Shown and edited.** While not edited, the field shows `Value` in `Format` (a .NET numeric format: `"N2"`, `"C0"`, `"P1"`, `"0.## kg"`) and `Culture` (default the current culture). Focused, it shows the plain number (the culture's decimal separator, no group separators, `-` for negatives) with everything selected; a tap that focuses it keeps the selection, so typing replaces the value. The format never rounds `Value`: `MaximumDecimalDigits` is the only rounding. `Text` is the shown text (read-only; text the app sets on `TextProperty` is committed as typed text).
- **Typing.** A numeric input rule runs before `InputFormatters` ([D9](../design/TextInput.md#input-formatters-d9)): digits (also other scripts', as ASCII), the decimal separator once (`.` and `,` both type the culture's; typing it again moves the caret after the one there; a separator first gets a 0; refused for integer types and with `MaximumDecimalDigits="0"`), and a minus sign only when `Minimum` is negative: it toggles the sign wherever the caret is (`+` removes it), and nothing goes before it. No more decimal digits than `MaximumDecimalDigits`; an integer type refuses what would overflow it (a `byte` entry refuses 256). Pasted text is cleaned of spaces, group separators, currency and percent signs (`1 234,50 €` → `1234,50` in German); text that is still not a number is refused. The range is not applied while typing: with `Minimum="10"`, the 1 of 15 can be typed.
- **Committing.** `ValueChangeMode="OnCompleted"` (default): the text becomes the value on Return, when focus leaves, and before a step; it is clamped then, leading zeros go, and text that is no number goes back to the value. `OnKeystroke`: also every edit that makes a number within the range. Escape puts the last value back (everything selected). Return then raises `Completed` and runs `ReturnCommand`.
- **Buttons.** `ButtonPlacement`: `None` (default), `End` (both at the end side), `Start`, `Split` (− at the start side, + at the end); `ButtonOrientation`: side by side (− then +) or stacked: + above − in one column (a spinner), or with `Split`, + in a bar across the top of the field and − in one across the bottom. They follow right-to-left layouts. They are parts of the field's row ([D11](../design/TextInput.md#leading-and-trailing-views-d11)): outermost, then the padding, the leading / trailing view and the clear button, so the text never runs under them. A button never focuses the field: on an unfocused field it steps the value without showing the keyboard; on a focused one the typed text is committed first, and focus and the keyboard stay. A mouse press steps at once and repeats while held (the look's `StepperRepeatDelay`, `StepperRepeatInterval`); a touch steps when lifted, or repeats once held past the long-press time, and a drag starting on a button scrolls the page. A button is dimmed at its end of the range. `DecrementText` / `IncrementText` replace the look's − / + with words or an icon font's symbols (`ButtonFontFamily`, at the field's font size); a larger text widens both buttons. `ButtonTextColor` colors them (default the text color).
- **Steps.** `Increment` (default 1) for buttons, Up / Down and the wheel; `LargeIncrement` (default 10 × `Increment`) for Page Up / Page Down. From an empty value, a step starts at 0 (within the range). A step stops at the range's end, also at the type's limits; `AutoReverse` goes on from the other end instead. Binary floating point is rounded by the increment (0.1 + 0.2 shows 0.3), as the steppers round.
- **Wheel.** Steps while the field is focused (desktop): one step per 20 DIPs of wheel or trackpad; unfocused, the wheel scrolls the page.
- **Editable, read-only.** `IsEditable="False"`: the value cannot be typed (no keyboard shows) but the buttons, keys and wheel step it (Telerik's `IsReadOnly`). `IsReadOnly` (MAUI's) stops every change, the buttons included.
- **Keyboard.** Picked from the type and range instead of MAUI's one `Keyboard.Numeric`: digits for a non-negative integer (Android `ClassNumber`, iOS `NumberPad`), with a decimal separator for decimals (`NumberFlagDecimal`, `DecimalPad`), with a minus sign for a negative `Minimum` (`NumberFlagSigned`; on iOS `NumbersAndPunctuation`, since neither number pad has a minus key); Windows `Number`. No autocorrection or suggestions. `Keyboard` is ignored.
- **An entry.** `SkUiNumericEntry` derives from `SkUiEntry` (Core: `SkUiCoreEntry`), and keeps that base in 1.x: styles that apply to derived entries reach it, and `ReturnCommand`, `Completed`, `ClearButtonVisibility`, `LeadingView` / `TrailingView` and the validation chrome work as on an entry. Members meant for free text are inherited but not for numbers: `Keyboard` is ignored (above); leave `IsPassword`, `TextTransform`, `IsSpellCheckEnabled`, `IsTextPredictionEnabled` and `AutofillHint` at their defaults.
- **Screen readers.** An editable text field (its value the shown text) with increment and decrement actions while the value can move.
- **Look.** `SkUiLook.DrawTextFieldStepButtons(canvas, SkUiTextFieldStepButtonsPaint)` (or `TextFieldStepButtonsPainter`): the default draws a light rounded tint while pressed, short muted dividers towards the text and between the buttons, and a − and a + (dimmed when they cannot step), leaving out a glyph the entry replaces with a text. `TextFieldStepButtonSize` (36 × 28: a button's width beside the text, a bar's height above or below it).
- **Typed values (`SkUiNumericEntry<T>`).** `Value` (`T?`), `Minimum`, `Maximum`, `Increment` and `LargeIncrement` of any number type with a minimum and a maximum (generic math). Integers and `decimal` are exact. In XAML: `x:TypeArguments="x:Int32"` (compiled XAML and Native AOT, as the typed stepper's). `SkUiNumericEntry` is `SkUiNumericEntry<double>`; both derive from `SkUiNumericEntryBase` (Core: `SkUiCoreNumericEntryBase`).

## How to use

```xml
<!-- A quantity: an int, never empty, − / + split around it. -->
<sk:SkUiNumericEntry x:TypeArguments="x:Int32" Minimum="1" Maximum="99" AllowNull="False"
                     ButtonPlacement="Split" HorizontalTextAlignment="Center" Value="{Binding Quantity}" />
<!-- A price: decimal, currency format while not edited, two decimals typed at most. -->
<sk:SkUiNumericEntry x:TypeArguments="x:Decimal" Minimum="0" Format="C2" MaximumDecimalDigits="2"
                     Placeholder="Price" Value="{Binding Price}" />
<!-- A weight in kg with a spinner at the end, stepping by 0.5. -->
<sk:SkUiNumericEntry Minimum="0" Maximum="500" Increment="0.5" Format="0.0 kg"
                     ButtonPlacement="End" ButtonOrientation="Vertical" Value="{Binding Weight}" />
```

```csharp
var temperature = new SkUiNumericEntry<int>()
    .SetRange(-30, 50)
    .SetButtonPlacement(SkUiStepButtonPlacement.End)
    .SetFormat("0 °C");
temperature.ValueChanged += (_, args) => Console.WriteLine($"{args.OldValue} → {args.NewValue}");
var core = new SkUiCoreNumericEntry<decimal>().SetIncrement(0.25m).SetMaximumDecimalDigits(2).SetValue(1.5m);
```

## Key properties

`Value` (two-way, `T?`), `Minimum` / `Maximum` (the type's range), `Increment` (1), `LargeIncrement` (`null`: 10 × `Increment`), `AllowNull` (true), `Format` (`null`), `Culture` (`null`: current), `ValueChangeMode` (`OnCompleted`), `MaximumDecimalDigits` (`null`; integers 0), `IsEditable` (true), `AutoReverse` (false), `ButtonPlacement` (`None`), `ButtonOrientation` (`Horizontal`), `DecrementText` / `IncrementText`, `ButtonFontFamily`, `ButtonTextColor`; the entry's (`Placeholder`, fonts, alignments, `ReturnType`, `ReturnCommand`, `Completed`, `ClearButtonVisibility`, `IsReadOnly`, `LeadingView` / `TrailingView`, `HasError` / `ErrorText` / `HelperText`, …). Event: `ValueChanged` (`SkUiValueChangedEventArgs<T?>`).

Left open: holding a button does not accelerate; device checks of the number keyboards (iOS `NumbersAndPunctuation`, Samsung Keyboard's separators) and of screen readers' increment / decrement on a text field; Windows' RangeValue pattern.

## Related

[`SkUiEntry`](SkUiEntry.md) · [`SkUiStepper`](SkUiStepper.md) · Gallery: `NumericEntryDemoPage` (next to an `Entry` with `Keyboard.Numeric`), `TypedNumericEntryDemoPage` (`SkUiNumericEntry<decimal>`) · Design: [TextInput.md](../design/TextInput.md#numeric-entry-d16)
