# SkUiStepper

A stepper drawn by SkiaUi: − and + buttons that change a value by an increment within a range.

**MAUI counterpart:** [`Stepper`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/stepper). Beyond MAUI: `IsValueVisible` (the value between the buttons, editable), `ValueFormat`, `TextColor`, `FontSize`. Core twin: `SkUiCoreStepper`.

## How it works

- **Value.** As MAUI's: `Value` (two-way) is rounded by the increment's significant digits and kept within `Minimum`–`Maximum`; the value the app asked for comes back when the range widens again; `Minimum` cannot exceed `Maximum` (and the reverse).
- **Buttons.** A button disabled at its end of the range. A mouse press steps at once and repeats while held (`StepperRepeatDelay` 400 ms, then every `StepperRepeatInterval` 100 ms); a touch steps when lifted, or repeats once held past the long-press time; a drag starting on a button scrolls the page. Right to left puts + on the left.
- **Keys and screen readers.** Up / Down step (also while the value is edited); without the value, Left / Right step and Home / End go to the range's ends (the focus manager's range keys). Screen readers read an adjustable value (TalkBack's `SeekBar`, VoiceOver's adjustable, Narrator's RangeValue) and step it.
- **The value (`IsValueVisible`).** Shown between the buttons in `ValueFormat` (a .NET numeric format, current culture), edited on the [entry's engine](SkUiEntry.md) with the numeric keyboard: Return or focus leaving commits it (coerced to the range); text that is not a number goes back to the value. The stepper takes the platform's text input only while it shows its value.
- **Look.** `SkUiLook.DrawStepper(canvas, SkUiStepperPaint)` (or `StepperPainter`): the default draws one rounded, lightly filled segment per button (and the value), divided by lines, a − and a + (dimmed when they cannot step), a darker fill while pressed and an accent outline while focused. `StepperButtonSize` (47 × 32: the pair is iOS's 94 × 32), `StepperValueMinimumWidth` (56).

## How to use

```xml
<sk:SkUiStepper Minimum="0" Maximum="10" Increment="1" Value="{Binding Quantity}" />
<sk:SkUiStepper Maximum="99" IsValueVisible="True" ValueFormat="N0" Value="{Binding Count}" />
```

## Key properties

`Value` (two-way), `Minimum` (0), `Maximum` (100), `Increment` (1), `IsValueVisible` (false), `ValueFormat` ("G"), `TextColor`, `FontSize` (16). Event: `ValueChanged` (MAUI `ValueChangedEventArgs`). MAUI's `(min, max, val, increment)` constructor.

## Related

[`SkUiSlider`](SkUiSlider.md) · [`SkUiEntry`](SkUiEntry.md) · Gallery: `StepperDemoPage`
