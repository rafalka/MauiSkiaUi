using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Side-by-side property playground for <see cref="SkUiActivityIndicator"/>.</summary>
public sealed class ActivityIndicatorDemoPage : ComponentDemoPage
{
    public ActivityIndicatorDemoPage() : base(nameof(SkUiActivityIndicator), new SkUiActivityIndicator(), new ActivityIndicator())
    {
        var skia = (SkUiActivityIndicator)SkiaControl;
        var native = (ActivityIndicator)NativeControl!;
        Toggle(nameof(SkUiActivityIndicator.IsRunning), true, value => { skia.IsRunning = value; native.IsRunning = value; }, () => skia.IsRunning, () => native.IsRunning);
        ColorEditor(nameof(SkUiActivityIndicator.Color), Accent, value => { skia.Color = value; native.Color = value; }, () => skia.Color, () => native.Color);
    }
}
