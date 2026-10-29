# Look members by control

Override these in a `DefaultSkUiLook` subclass (or a stock platform look's). Sizes are DIPs. "Base" members are on `SkUiLook`, the rest on `DefaultSkUiLook`. Values in brackets are the stock looks' (iOS / Mac Catalyst / Android Material 2 / Android Material 3 / Windows).

## Text

| Member | What |
| --- | --- |
| `DefaultFontSize` (base) | Size of text that sets none: labels, buttons, radio buttons, fields, pickers, steppers [17 / 17 / 14 / 14 / 14] |
| `DefaultFontFamily` (base) | Family of text that sets none; `null` = the system font (SF, Roboto) [– / – / – / – / Segoe UI Variable] |
| `TextIncludesFontPadding` (base) | Android's TextView font padding above the first line and below the last [false / false / true / true / false] |
| `AppliesFontTracking` (base) | Apply the font's `trak` table, as CoreText does [true / true / false / false / false] |
| `FontTrackingSizeScale` (base) | Size the tracking is looked up at, relative to the font size [1 / 0.77 / – / – / –] |

## Button

| Member | What |
| --- | --- |
| `DefaultButtonMinimumHeight` (base) | [36 / 35 / 36 / 36 / 32] |
| `DefaultButtonCornerRadius` (base) | Used when the button sets none [0 / 0 / 4 / 18 / 4] |
| `DefaultButtonPadding` (base) | Read when the button is created [13.5 × 7 / same / 16 × 8 / same / 12, 6, 12, 7 (Fluent's padding and its 1 epx border)] |
| `DefaultButtonBorderWidth` (base) | Drawn in the scheme's `ButtonBorder`; does not change the size [0 / 0 / 0 / 0 / 1] |
| `ButtonHasElevationBorder` | The border's bottom edge darker (Fluent) [false / … / true] |
| `BasePressEffect` | `Dim` or `Ripple` |

## Switch

| Member | What |
| --- | --- |
| `DefaultSwitchSize` (base) | The control's size [63 × 28 / same / 48 × 48 / 52 × 48 / 40 × 40] |
| `SwitchStyle` | `Pill` (iOS), `ThumbOverTrack` (Material 2), `Outlined` (Material 3, Fluent) |
| `SwitchThumbWidth`, `SwitchThumbInset` | Pill: a capsule thumb's width (0 = round) and its gap to the track's edge [37, 2] |
| `SwitchTrackHeight` | Track height centered in the control; 0 = the control's [– / – / 14 / 32 / 20] |
| `SwitchTrackWidth`, `SwitchThumbDiameter`, `SwitchThumbTravel` | ThumbOverTrack geometry [23, 20, 19.5] |
| `SwitchOffThumbRadiusFraction`, `SwitchOnThumbRadiusFraction`, `SwitchOutlineWidth` | Outlined geometry [Material 3: 0.25, 0.375, 2 / Windows: 0.3, 0.3, 1] |

## CheckBox and RadioButton

| Member | What |
| --- | --- |
| `DefaultCheckBoxSize` (base) | The control's size; a wider-than-tall size centers the box in it [18 / 18 / 32 / 48 × 32 / 32] |
| `DefaultCheckBoxMinimumWidth` (base) | WinUI's MinWidth: the box stays at the start; a `MinimumWidthRequest` replaces it, as natively [0 / 0 / 0 / 0 / 120] |
| `CheckBoxInset` | Space around the box inside the control [0 / 0 / 7 / 7 / 6] |
| `GetCheckBoxCornerRadius(size)`, `CheckBoxBorderWidth` | Box shape; half the size draws a circle (iOS) |
| `CheckMarkStrokeFraction`, `CheckMarkScale` | The check mark's width (fraction of the box) and size [0.12, 1 / … / Windows: 0.065, 0.8] |
| `DefaultRadioButtonSize` (base) | The circle's slot [21 / 21 / 32 / 32 / 20] |
| `RadioButtonInset` | Space around the ring inside the slot [0 / 0 / 6 / 6 / 0] |
| `RadioButtonStyle` | `Dot` (a dot of the ring's color) or `Filled` (Fluent: the ring fills around a white dot) |
| `RadioRingFraction`, `RadioDotRadiusFraction` | Ring width and dot radius as fractions of the ring's size [Windows: 0.05, 0.3] |
| `DefaultRadioButtonContentSpacing` (base) | Between circle and content [6 / 6 / 0 / 0 / 8] |
| `RadioButtonPadding` (base) | Around circle and content, added to the control's Padding [8 × 7 / same / 0 / 0 / 0 × 6] |
| `DefaultRadioButtonMinimumWidth` (base) | WinUI's MinWidth; a `MinimumWidthRequest` replaces it, as natively [0 / 0 / 0 / 0 / 120] |

## Slider and ProgressBar

| Member | What |
| --- | --- |
| `DefaultSliderThickness` (base) | Height of a horizontal slider [34 / 34 / 32 / 48 / 32] |
| `SliderStyle` | `Thumb` or `Handle` (Material 3's bar handle with gaps and a stop dot) |
| `SliderThumbRadius` (base), `SliderThumbWidth` | Thumb height / 2 and width (wider = capsule) [12, 37 / same / 6, 12 / – / 10, 20] |
| `SliderThumbInnerRadius` | A white thumb around a dot of the thumb color this size; 0 = a filled thumb [0 / 0 / 0 / – / 5] |
| `SliderThumbTravelInset` (base) | Where the thumb's center travels from each end; input uses it too [18.5 / same / 16 / 22 / 10] |
| `SliderTrackInset`, `SliderTrackHeight` | Track ends and thickness [0, 6 / same / 16, 2 / 14, 16 / 0, 4] |
| `SliderHandleSize`, `SliderHandleGap` | Handle style [4 × 44, 4] |
| `SliderThumbHasShadow` | |
| `DefaultProgressBarHeight` (base), `ProgressBarTrackHeight` | Control height and bar thickness centered in it [4, – / same / 16, 4 / 4, – / 3, –] |
| `ProgressBarTrackLineHeight` | A thinner track line under the fill [– / – / – / – / 1] |
| `ProgressBarGap`, `ProgressBarHasStopIndicator` | Material 3's gap and end dot |
| `GetProgressBarCornerRadius(height)` | Rounding of the bar's ends [height / 2 / same / 0 (square) / height / 2 / height / 2] |

## ActivityIndicator

| Member | What |
| --- | --- |
| `DefaultActivityIndicatorSize` (base) | [20 / 20 / 48 / 40 / 32] |
| `ActivityIndicatorStyle` | `Spokes`, `Arc` or `Ring` (Fluent's short arc without a track) |
| `ActivityIndicatorSpokeCount` | [8] |
| `ActivityIndicatorInset`, `GetActivityIndicatorStrokeWidth(diameter)` | Arc and ring geometry [– / – / 4, 4 / 4, 4 / 0, a tenth of the diameter] |

## Entry, Editor, Picker and Stepper

| Member | What |
| --- | --- |
| `DefaultTextFieldStyle`, `DefaultPickerFieldStyle` | `Outline`, `Underline`, `Filled`, Fluent's `TextBox` (text fields) and `ComboBox` (pickers), … (one field that differs: its own `FieldStyle` and chrome, not the look) |
| `DefaultTextFieldMinimumHeight` (base), `TextFieldPadding` (base) | [34, 7 × 6 / same / 0, 4 10 4 11 / 0, 16 × 19.8 / 32, 11 6 7 7] |
| `DefaultPickerMinimumHeight` (base), `PickerFieldPadding` (base), `PickerChevronWidth` (base) | MAUI's pickers have no arrow on iOS and Android [34, 14 × 6, 0 / same / 0, 4 10 4 11, 0 / same / 32, 12 5 0 7, 38] |
| `FieldCornerRadius` | [5 / 5 / 4 / 4 / 4] |
| `StepperButtonSize` (base), `DefaultStepperCornerRadius` (base) | iOS's segmented stepper matches. On Android and Windows SkiaUi keeps its segmented stepper on purpose (MAUI's is two separate buttons; Windows: 32 × 32 each, so the size matches): the Stepper row's shape differs there by design |

## Scrolling, focus, popups and motion

`BaseOverscroll`, `BaseRefreshStyle`, `ScrollBarThickness`, `ScrollBarExpandedThickness` (base), `FocusRingStyle`, `FocusRingThickness`, `PopupItemHeight`, `PopupItemPadding`, `PopupHorizontalOptions`, `PopupVerticalOptions` (base), `BaseIndicatorStyle`, and `GetTransitionCore(kind)` for durations and easings (`SkUiEasings`).

## Colors

Not the look's: derive a scheme from the stock one (`IosSkUiColorScheme`, `AndroidSkUiColorScheme`, `AndroidMaterial3SkUiColorScheme`, `WindowsSkUiColorScheme`, …, or `WithAccent(color, isDark)`) and assign it to `SkUiColorScheme.Current`. `DefaultSkUiLook.GetColorScheme(isDark)` pairs a look with its scheme. Control tokens: `ButtonBackground`, `ButtonForeground`, `ButtonBorder` and `ActivityIndicator` (iOS / Mac: a transparent button with accent text and a gray spinner; Android: an accent button and spinner; Windows: Fluent's light button with a faint border and an accent ring). Until set they follow another token (`ButtonBackground` the accent), so `WithAccent` carries into them. Read when a control is created; set them before creating controls or in the app's `SkUi*` styles.
