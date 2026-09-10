using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Side-by-side property playground for <see cref="SkUiView"/>.</summary>
public sealed class ViewDemoPage : ComponentDemoPage
{
    public ViewDemoPage() : base(nameof(SkUiView), new SkUiView())
    {
        ColorEditor(nameof(Background), Accent, value => SkiaControl.Background = value, () => ((SolidColorBrush)SkiaControl.Background).Color);
        Number(nameof(Rotation), -45, 45, 0, value => SkiaControl.Rotation = value, () => SkiaControl.Rotation);
        Number(nameof(Scale), 0.25, 1.25, 1, value => SkiaControl.Scale = value, () => SkiaControl.Scale);
        var taps = 0;
        SkiaControl.Tapped += (_, _) => Feedback($"Taps: {++taps}");
        Toggle(nameof(InputTransparent), false, value => SkiaControl.InputTransparent = value, () => SkiaControl.InputTransparent);
        OnReset(() => taps = 0);
    }
}
