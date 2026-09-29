using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Side-by-side property playground for <see cref="SkUiProgressBar"/> (plus the SkiaUi-only indeterminate mode).</summary>
public sealed class ProgressBarDemoPage : ComponentDemoPage
{
    public ProgressBarDemoPage() : base(nameof(SkUiProgressBar), new SkUiProgressBar(), new ProgressBar())
    {
        var skia = (SkUiProgressBar)SkiaControl;
        var native = (ProgressBar)NativeControl!;
        Number(nameof(SkUiProgressBar.Progress), 0, 1, 0.4, value => { skia.Progress = value; native.Progress = value; }, () => skia.Progress, () => native.Progress);
        Toggle(nameof(SkUiProgressBar.IsIndeterminate), false, value => skia.IsIndeterminate = value, () => skia.IsIndeterminate);
        ColorEditor(nameof(SkUiProgressBar.ProgressColor), Accent, value => { skia.ProgressColor = value; native.ProgressColor = value; }, () => skia.ProgressColor, () => native.ProgressColor);
        ColorEditor(nameof(SkUiProgressBar.TrackColor), SkUiColors.TrackOff, value => skia.TrackColor = value, () => skia.TrackColor);
        ActionButton("ProgressTo 1 (1 s)", () => { _ = skia.ProgressTo(1, 1000, Easing.CubicInOut); _ = native.ProgressTo(1, 1000, Easing.CubicInOut); });
        ActionButton("ProgressTo 0 (1 s)", () => { _ = skia.ProgressTo(0, 1000, Easing.CubicInOut); _ = native.ProgressTo(0, 1000, Easing.CubicInOut); });
    }
}
