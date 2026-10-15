using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// Property playground for <see cref="SkUiStepper{T}"/> on <see cref="decimal"/> values (a price, a quantity in tenths) next to MAUI's
/// <see cref="Stepper"/> on doubles: the drawn stepper's value, range and increment are decimals, stepped exactly.
/// </summary>
public sealed class TypedStepperDemoPage : ComponentDemoPage
{
    private static readonly decimal[] Increments = [0.01m, 0.1m, 0.25m, 1m, 5m];

    public TypedStepperDemoPage() : base("SkUiStepper<decimal>", new SkUiStepper<decimal>(), new Stepper())
    {
        var skia = (SkUiStepper<decimal>)SkiaControl;
        var native = (Stepper)NativeControl!;
        skia.ValueChanged += (_, args) => Feedback($"Value {args.NewValue}m", $"Value {native.Value:G}");
        native.ValueChanged += (_, args) => Feedback($"Value {skia.Value}m", $"Value {args.NewValue:G}");
        Number(nameof(SkUiStepper<decimal>.Maximum), 1, 100, 100, value => { skia.Maximum = (decimal)value; native.Maximum = value; },
            () => (double)skia.Maximum, () => native.Maximum, whole: true);
        Number(nameof(SkUiStepper<decimal>.Minimum), 0, 50, 0, value => { skia.Minimum = (decimal)value; native.Minimum = value; },
            () => (double)skia.Minimum, () => native.Minimum, whole: true);
        Choice(nameof(SkUiStepper<decimal>.Increment), Increments, 0.1m, value => { skia.Increment = value; native.Increment = (double)value; },
            () => skia.Increment, () => (decimal)native.Increment);
        Number(nameof(SkUiStepper<decimal>.Value), 0, 100, 0, value => { skia.Value = (decimal)value; native.Value = value; },
            () => (double)skia.Value, () => native.Value, whole: true);
        Choice(nameof(SkUiStepper<decimal>.Orientation), [StackOrientation.Horizontal, StackOrientation.Vertical], StackOrientation.Horizontal,
            value => skia.Orientation = value, () => skia.Orientation);
    }
}
