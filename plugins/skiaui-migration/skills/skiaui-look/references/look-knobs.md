# Look members by control

Override these in a `DefaultSkUiLook` subclass (or a stock platform look's). Sizes are DIPs. "Base" members are on `SkUiLook`, the rest on `DefaultSkUiLook`. Values in brackets are the stock looks' (iOS / Mac Catalyst / Android Material 2 / Android Material 3).

## Text

| Member | What |
| --- | --- |
| `DefaultFontSize` (base) | Size of text that sets none: labels, buttons, radio buttons, fields, pickers, steppers [17 / 17 / 14 / 14] |
| `DefaultFontFamily` (base) | Family of text that sets none; `null` = the system font (SF, Roboto) |
| `TextIncludesFontPadding` (base) | Android's TextView font padding above the first line and below the last [false / false / true / true] |
| `AppliesFontTracking` (base) | Apply the font's `trak` table, as CoreText does [true / true / false / false] |
| `FontTrackingSizeScale` (base) | Size the tracking is looked up at, relative to the font size [1 / 0.77 / – / –] |

## Button

| Member | What |
| --- | --- |
| `DefaultButtonMinimumHeight` (base) | [36 / 35 / 36 / 36] |
| `DefaultButtonCornerRadius` (base) | Used when the button sets none [0 / 0 / 4 / 18] |
| `DefaultButtonPadding` (base) | Read when the button is created [13.5 × 7 / same / 16 × 8 / same] |
| `BasePressEffect` | `Dim` or `Ripple` |

## Switch

| Member | What |
| --- | --- |
| `DefaultSwitchSize` (base) | The control's size [63 × 28 / same / 48 × 48 / 52 × 48] |
| `SwitchStyle` | `Pill` (iOS), `ThumbOverTrack` (Material 2), `Outlined` (Material 3, Fluent) |
| `SwitchThumbWidth`, `SwitchThumbInset` | Pill: a capsule thumb's width (0 = round) and its gap to the track's edge [37, 2] |
| `SwitchTrackHeight` | Track height centered in the control; 0 = the control's [– / – / 14 / 32] |
| `SwitchTrackWidth`, `SwitchThumbDiameter`, `SwitchThumbTravel` | ThumbOverTrack geometry [23, 20, 19.5] |
| `SwitchOffThumbRadiusFraction`, `SwitchOnThumbRadiusFraction`, `SwitchOutlineWidth` | Outlined geometry |

## CheckBox and RadioButton

| Member | What |
| --- | --- |
| `DefaultCheckBoxSize` (base) | The control's size; a wider-than-tall size centers the box in it [18 / 18 / 32 / 48 × 32] |
| `CheckBoxInset` | Space around the box inside the control [0 / 0 / 7 / 7] |
| `GetCheckBoxCornerRadius(size)`, `CheckBoxBorderWidth` | Box shape; half the size draws a circle (iOS) |
| `DefaultRadioButtonSize` (base) | The circle's slot [21 / 21 / 32 / 32] |
| `RadioButtonInset` | Space around the ring inside the slot [0 / 0 / 6 / 6] |
| `RadioRingFraction`, `RadioDotRadiusFraction` | Ring width and dot radius as fractions of the ring's size |
| `DefaultRadioButtonContentSpacing` (base) | Between circle and content [6 / 6 / 0 / 0] |
| `RadioButtonPadding` (base) | Around circle and content, added to the control's Padding [8 × 7 / same / 0 / 0] |

## Slider and ProgressBar

| Member | What |
| --- | --- |
| `DefaultSliderThickness` (base) | Height of a horizontal slider [34 / 34 / 32 / 48] |
| `SliderStyle` | `Thumb` or `Handle` (Material 3's bar handle with gaps and a stop dot) |
| `SliderThumbRadius` (base), `SliderThumbWidth` | Thumb height / 2 and width (wider = capsule) [12, 37 / same / 6, 12 / –] |
| `SliderThumbTravelInset` (base) | Where the thumb's center travels from each end; input uses it too [18.5 / same / 16 / 22] |
| `SliderTrackInset`, `SliderTrackHeight` | Track ends and thickness [0, 6 / same / 16, 2 / 14, 16] |
| `SliderHandleSize`, `SliderHandleGap` | Handle style [4 × 44, 4] |
| `SliderThumbHasShadow` | |
| `DefaultProgressBarHeight` (base), `ProgressBarTrackHeight` | Control height and bar thickness centered in it [4, – / same / 16, 4 / 4, –] |
| `ProgressBarGap`, `ProgressBarHasStopIndicator` | Material 3's gap and end dot |

## ActivityIndicator

| Member | What |
| --- | --- |
| `DefaultActivityIndicatorSize` (base) | [20 / 20 / 48 / 40] |
| `ActivityIndicatorStyle` | `Spokes`, `Arc` or `Ring` |
| `ActivityIndicatorSpokeCount` | [8] |
| `ActivityIndicatorInset`, `GetActivityIndicatorStrokeWidth(diameter)` | Arc and ring geometry [– / – / 4, 4 / 4, 4] |

## Entry, Editor, Picker and Stepper

| Member | What |
| --- | --- |
| `DefaultTextFieldStyle`, `DefaultPickerFieldStyle` | `Outline`, `Underline`, `Filled`, … |
| `DefaultTextFieldMinimumHeight` (base), `TextFieldPadding` (base) | [34, 7 × 6 / same / 0, 4 10 4 11 / 0, 16 × 19.8] |
| `DefaultPickerMinimumHeight` (base), `PickerFieldPadding` (base), `PickerChevronWidth` (base) | MAUI's pickers have no arrow on iOS and Android [34, 14 × 6, 0 / same / 0, 4 10 4 11, 0] |
| `FieldCornerRadius` | [5 / 5 / 4 / 4] |
| `StepperButtonSize` (base), `DefaultStepperCornerRadius` (base) | iOS's segmented stepper matches. On Android SkiaUi keeps its segmented stepper on purpose (MAUI's is two separate gray buttons): the Stepper row differs there by design |

## Scrolling, focus, popups and motion

`BaseOverscroll`, `BaseRefreshStyle`, `ScrollBarThickness`, `ScrollBarExpandedThickness` (base), `FocusRingStyle`, `FocusRingThickness`, `PopupItemHeight`, `PopupItemPadding`, `PopupHorizontalOptions`, `PopupVerticalOptions` (base), `BaseIndicatorStyle`, and `GetTransitionCore(kind)` for durations and easings (`SkUiEasings`).

## Colors

Not the look's: derive a scheme from the stock one (`IosSkUiColorScheme`, `AndroidSkUiColorScheme`, `AndroidMaterial3SkUiColorScheme`, …, or `WithAccent(color, isDark)`) and assign it to `SkUiColorScheme.Current`. `DefaultSkUiLook.GetColorScheme(isDark)` pairs a look with its scheme.
