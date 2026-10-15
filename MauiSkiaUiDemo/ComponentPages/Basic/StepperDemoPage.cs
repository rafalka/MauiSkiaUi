using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Side-by-side property playground for <see cref="SkUiStepper"/> (plus the SkiaUi-only editable value) and MAUI's <see cref="Stepper"/>.</summary>
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
        Toggle(nameof(SkUiStepper.IsValueVisible), false, value => skia.IsValueVisible = value, () => skia.IsValueVisible);
        ColorEditor(nameof(SkUiStepper.TextColor), Ink, value => skia.TextColor = value, () => skia.TextColor ?? Ink);
    }
}
