# SkUiStepper

A stepper drawn by SkiaUi: − and + buttons that change a value by an increment within a range.

**MAUI counterpart:** [`Stepper`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/stepper). Buttons only, as MAUI's (show the value with a label bound to it). Beyond MAUI: `Orientation` (stacked buttons), `DecrementText` / `IncrementText` (texts or icon-font symbols in place of − and +) with `FontFamily`, `FontSize`, `FontAttributes`, `FontAutoScalingEnabled`, `TextColor`, and the shape: `CornerRadius`, `BorderWidth`, `BorderColor`. Typed values: `SkUiStepper<T>` (`int`, `uint`, `decimal`, …). Core twins: `SkUiCoreStepper`, `SkUiCoreStepper<T>`.

## How it works

- **Value.** As MAUI's: `Value` (two-way) is rounded by the increment's significant digits and kept within `Minimum`–`Maximum`; the value the app asked for comes back when the range widens again; `Minimum` cannot exceed `Maximum` (and the reverse).
- **Buttons.** Side by side, − at the start side (`Orientation="Horizontal"`, the default), or stacked with + on top (`Orientation="Vertical"`, a spinner's shape); arranged larger than they measure, the stepper splits its room between the two. A button is disabled at its end of the range. A mouse press steps at once and repeats while held (`StepperRepeatDelay` 400 ms, then every `StepperRepeatInterval` 100 ms); a touch steps when lifted, or repeats once held past the long-press time; a drag starting on a button scrolls the page. Right to left puts + on the left (side by side only).
- **Texts.** `DecrementText` / `IncrementText` replace the look's − / + on their button: words ("Less", "More"), or a symbol of an icon font registered with MAUI's `ConfigureFonts` or `SkUiFonts.Register` and named by `FontFamily`. `null` or empty keeps the look's glyph, so one button can have a text and the other the glyph. Each text is one line on the [shared text engine](SkUiLabel.md) (shaping, fallback fonts, right to left), centered and in `TextColor`, dimmed like the glyph at its end of the range. A text wider or taller than the look's button grows both buttons to fit it (8 DIPs of padding at the sides, 4 above and below).
- **Shape.** `CornerRadius` (an `int` as MAUI Button's; default the look's `StepperCornerRadius`, 8) rounds the stepper: 0 squares it, half its height or more makes a pill. `BorderWidth` (default 0) outlines it inside its bounds in `BorderColor` (`null`: the color scheme's muted color), and the divider between the buttons then takes the outline's width and color and crosses the whole stepper, as a segmented control's. Texts keep clear of the outline (a wider outline grows a stepper with texts). The Core steppers' `CornerRadius` is a `double`, as the Core labels'.
- **Typed values (`SkUiStepper<T>`).** `Value`, `Minimum`, `Maximum` and `Increment` of any number type `T` with a minimum and a maximum (generic math: `int`, `uint`, `long`, `byte`, `short`, `decimal`, `float`, `double`, `Half`, …), so a view model's `int` or `decimal` binds without going through `double`. Integers and `decimal` step exactly; binary floating point is rounded by the increment as MAUI's stepper rounds doubles. A step stops at the range's end, also at the type's own limits: a `uint` at 3 stepping down by 5 goes to the minimum (it never wraps to 4294967295), and `int.MaxValue` stays the top. `Increment` must be above zero and the values finite: invalid values are ignored with MAUI's warning, the fluent setters throw. Same coercion as the `double` stepper (the asked value comes back when the range widens), the same buttons, texts, orientation, keys and screen readers (they see the value as a `double`). `ValueChanged` carries `SkUiValueChangedEventArgs<T>`. `SkUiStepper` stays on `double`, MAUI's `Stepper`; both derive from `SkUiStepperBase` (Core: `SkUiCoreStepperBase`).
- **XAML and Native AOT.** `x:TypeArguments` picks the typed stepper: `x:Int32`, `x:Int64`, `x:Decimal`, `x:Double`, …; types without an `x:` name through `xmlns:sys="clr-namespace:System;assembly=System.Runtime"` (`sys:UInt32`). Compiled XAML (XamlC, or MAUI's XAML source generator) creates `SkUiStepper<int>` at build time with typed literals and compiled bindings, so it is Native AOT-safe; each `T` an app uses is compiled ahead of time (no reflection in the arithmetic). Only XAML inflated at run time closes the type by reflection (`MakeGenericType`), as for any generic type; Native AOT apps compile their XAML. Bind `Value` to a property of type `T`: another type makes MAUI convert the values.
- **Keys and screen readers.** Up / Down step; Left / Right step too and Home / End go to the range's ends (the focus manager's range keys). The stepper takes keyboard focus without the platform's text input (no keyboard shows). Screen readers read an adjustable value (TalkBack's `SeekBar`, VoiceOver's adjustable, Narrator's RangeValue) and step it.
- **Look.** `SkUiLook.DrawStepper(canvas, SkUiStepperPaint)` (or `StepperPainter`): the default draws one rounded, lightly filled segment per button, divided by a line (`SkUiStepperPaint.Orientation` says which way), and leaves out a glyph the control replaces with a text (`HasDecrementText` / `HasIncrementText`; the control draws the text over the look), a − and a + (dimmed when they cannot step), a darker fill while pressed and an accent outline while focused. The control's shape reaches the look as `SkUiStepperPaint.CornerRadius`, `BorderWidth` and `BorderColor` (the default look draws the outline and the full divider when `BorderWidth` is above 0). `StepperCornerRadius` (8), `StepperButtonSize` (47 × 32: the pair is iOS's 94 × 32; stacked, 47 × 64).

## How to use

```xml
<sk:SkUiStepper Minimum="0" Maximum="10" Increment="1" Value="{Binding Quantity}" />
<HorizontalStackLayout Spacing="8">
    <sk:SkUiLabel Text="{Binding Count}" VerticalOptions="Center" />
    <sk:SkUiStepper Maximum="99" Orientation="Vertical" Value="{Binding Count}" />
</HorizontalStackLayout>
<!-- Icon-font chevrons; the font is registered with ConfigureFonts as "Icons". -->
<sk:SkUiStepper Orientation="Vertical" FontFamily="Icons" FontSize="20" DecrementText="&#xE5CF;" IncrementText="&#xE5CE;" />
<!-- A pill with a 1 DIP outline in the accent color. -->
<sk:SkUiStepper CornerRadius="16" BorderWidth="1" BorderColor="{StaticResource Primary}" />
<!-- Typed values: an int quantity and a decimal amount in quarters. -->
<sk:SkUiStepper x:TypeArguments="x:Int32" Minimum="1" Maximum="20" Value="{Binding Quantity}" />
<sk:SkUiStepper x:TypeArguments="x:Decimal" Increment="0.25" Maximum="10" Value="{Binding Amount}" />
```

```csharp
var quantity = new SkUiStepper<int>().SetMinimum(1).SetMaximum(20).SetOrientation(StackOrientation.Vertical);
quantity.ValueChanged += (_, args) => Console.WriteLine($"{args.OldValue} → {args.NewValue}");
var core = new SkUiCoreStepper<uint>().SetIncrement(5).SetValue(10);
```

## Key properties

`Value` (two-way), `Minimum` (0), `Maximum` (100), `Increment` (1), `Orientation` (`Horizontal`), `DecrementText` / `IncrementText` (`null`: the look's − / +), `FontFamily`, `FontSize` (16), `FontAttributes`, `FontAutoScalingEnabled` (true), `TextColor` (the − and + or their texts), `CornerRadius` (the look's, 8), `BorderWidth` (0), `BorderColor` (`null`: muted). Event: `ValueChanged` (MAUI `ValueChangedEventArgs`). MAUI's `(min, max, val, increment)` constructor. `SkUiStepper<T>`: the same with `T` values (`Increment` above zero), `ValueChanged` with `SkUiValueChangedEventArgs<T>`.

## Related

[`SkUiSlider`](SkUiSlider.md) · Gallery: `StepperDemoPage`, `TypedStepperDemoPage` (`SkUiStepper<decimal>`) · Sample: Pizza order (the quantity)
