using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Side-by-side property playground for <see cref="SkUiStepper"/> (plus the SkiaUi-only vertical orientation, texts in place of − and +, corner radius and outline) and MAUI's <see cref="Stepper"/>.</summary>
public sealed class StepperDemoPage : ComponentDemoPage
{
    public StepperDemoPage() : base(nameof(SkUiStepper), new SkUiStepper(), new Stepper())
    {
        var skia = (SkUiStepper)SkiaControl;
        var native = (Stepper)NativeControl!;
        skia.ValueChanged += (_, args) => Feedback($"Value {args.NewValue:G}", $"Value {native.Value:G}");
        native.ValueChanged += (_, args) => Feedback($"Value {skia.Value:G}", $"Value {args.NewValue:G}");
        Number(nameof(SkUiStepper.Maximum), 1, 100, 100, value => { skia.Maximum = value; native.Maximum = value; }, () => skia.Maximum, () => native.Maximum, whole: true);
        Number(nameof(SkUiStepper.Minimum), 0, 50, 0, value => { skia.Minimum = value; native.Minimum = value; }, () => skia.Minimum, () => native.Minimum, whole: true);
        Number(nameof(SkUiStepper.Increment), 0.5, 10, 1, value => { skia.Increment = value; native.Increment = value; }, () => skia.Increment, () => native.Increment);
        Number(nameof(SkUiStepper.Value), 0, 100, 0, value => { skia.Value = value; native.Value = value; }, () => skia.Value, () => native.Value, whole: true);
        Choice(nameof(SkUiStepper.Orientation), [StackOrientation.Horizontal, StackOrientation.Vertical], StackOrientation.Horizontal,
            value => skia.Orientation = value, () => skia.Orientation);
        ColorEditor(nameof(SkUiStepper.TextColor), Ink, value => skia.TextColor = value, () => skia.TextColor ?? Ink);
        Text(nameof(SkUiStepper.DecrementText), string.Empty, value => skia.DecrementText = value, () => skia.DecrementText ?? string.Empty);
        Text(nameof(SkUiStepper.IncrementText), string.Empty, value => skia.IncrementText = value, () => skia.IncrementText ?? string.Empty);
        Choice(nameof(SkUiStepper.FontFamily), new[] { LabelDemoPage.DefaultFontFamily }.Concat(DemoFonts.RegisteredFamilies).ToArray(), LabelDemoPage.DefaultFontFamily,
            value => skia.FontFamily = value == LabelDemoPage.DefaultFontFamily ? null : value, () => skia.FontFamily ?? LabelDemoPage.DefaultFontFamily);
        Number(nameof(SkUiStepper.FontSize), 10, 36, 16, value => skia.FontSize = value, () => skia.FontSize);
        Number(nameof(SkUiStepper.CornerRadius), 0, 32, 8, value => skia.CornerRadius = (int)Math.Round(value), () => skia.CornerRadius, whole: true);
        Number(nameof(SkUiStepper.BorderWidth), 0, 8, 0, value => skia.BorderWidth = value, () => skia.BorderWidth);
        ColorEditor(nameof(SkUiStepper.BorderColor), Ink, value => skia.BorderColor = value, () => skia.BorderColor ?? Ink);
    }
}
