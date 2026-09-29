using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Side-by-side property playground for <see cref="SkUiSlider"/> (plus the SkiaUi-only vertical orientation).</summary>
public sealed class SliderDemoPage : ComponentDemoPage
{
    public SliderDemoPage() : base(nameof(SkUiSlider), new SkUiSlider(), new Slider())
    {
        var skia = (SkUiSlider)SkiaControl;
        var native = (Slider)NativeControl!;
        skia.ValueChanged += (_, args) => Feedback($"Value {args.NewValue:F2}", $"Value {native.Value:F2}");
        native.ValueChanged += (_, args) => Feedback($"Value {skia.Value:F2}", $"Value {args.NewValue:F2}");
        skia.DragStarted += (_, _) => Feedback("Drag started");
        skia.DragCompleted += (_, _) => Feedback($"Drag completed at {skia.Value:F2}", $"Value {native.Value:F2}");
        Number(nameof(SkUiSlider.Maximum), 1, 100, 1, value => { skia.Maximum = value; native.Maximum = value; }, () => skia.Maximum, () => native.Maximum);
        Number(nameof(SkUiSlider.Minimum), 0, 50, 0, value => { skia.Minimum = value; native.Minimum = value; }, () => skia.Minimum, () => native.Minimum);
        Number(nameof(SkUiSlider.Value), 0, 100, 0, value => { skia.Value = value; native.Value = value; }, () => skia.Value, () => native.Value);
        Choice(nameof(SkUiSlider.Orientation), [StackOrientation.Horizontal, StackOrientation.Vertical], StackOrientation.Horizontal,
            value => skia.Orientation = value, () => skia.Orientation);
        ColorEditor(nameof(SkUiSlider.MinimumTrackColor), Accent, value => { skia.MinimumTrackColor = value; native.MinimumTrackColor = value; }, () => skia.MinimumTrackColor, () => native.MinimumTrackColor);
        ColorEditor(nameof(SkUiSlider.MaximumTrackColor), SkUiColors.TrackOff, value => { skia.MaximumTrackColor = value; native.MaximumTrackColor = value; }, () => skia.MaximumTrackColor, () => native.MaximumTrackColor);
        ColorEditor(nameof(SkUiSlider.ThumbColor), Accent, value => { skia.ThumbColor = value; native.ThumbColor = value; }, () => skia.ThumbColor, () => native.ThumbColor);
    }
}
