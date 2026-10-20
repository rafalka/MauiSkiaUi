# Why a row differs

Compare with "App styles" off first: that row compares the look with the platform's defaults. Then turn it on: what changes there comes from the app's styles.

| Symptom | Likely cause | Fix |
| --- | --- | --- |
| Every text row is taller natively on Android (about 2.3 dp at 14 sp) | Android's font padding not applied | `TextIncludesFontPadding => true` (the Android looks have it) |
| Text a few percent wider than native on iOS | Font tracking not applied | `AppliesFontTracking => true` (the Apple looks have it) |
| Text wider natively on Mac Catalyst than on iOS | CoreText tracks at the drawn size (77%) | `FontTrackingSizeScale => 0.77` (the Mac look) |
| Text in a different font | The app's styles set `FontFamily` on native controls only | Add the setter to the `SkUi*` style, or `DefaultFontFamily` for every control |
| Same height, different width for a button | Padding | `DefaultButtonPadding` (read when the button is created) |
| A control's size matches but its part is offset (a check box, a radio ring) | The native part sits inside a larger touch area | `CheckBoxInset`, `RadioButtonInset`, a wider `DefaultCheckBoxSize` |
| Slider thumb at a different place for the same value | Travel inset | `SliderThumbTravelInset` (input follows it), `SliderTrackInset` for the track ends |
| A native control is 44 high and the drawn one is not | The app's style sets `MinimumHeightRequest` on the native control | Same setter on the `SkUi*` style |
| Picker wider by an arrow | MAUI's iOS and Android pickers have no arrow | `PickerChevronWidth => 0` |
| A shape SkiaUi has no member for | Not drawn yet | Report it; do not approximate it with sizes |

## In SkiaUi itself

When a stock look is wrong on its own platform, fix the stock look: the value in `MauiSkiaUi/Look/<Platform>SkUiLook.cs`, its remark (what the platform does), `PlatformLookShapeTests` / `PlatformLookTextTests`, the look's section in `docs/design/ControlLook.md`, and this skill's [look-knobs.md](look-knobs.md) values.
