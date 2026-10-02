using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Side-by-side property playground for <see cref="SkUiView"/>.</summary>
public sealed class ViewDemoPage : ComponentDemoPage
{
    public ViewDemoPage() : base(nameof(SkUiView), new SkUiView())
    {
        // The color is the solid fill; the brush editor swaps it for a gradient.
        var solid = Accent;
        ColorEditor(nameof(Background), Accent, value =>
        {
            solid = value;
            if (SkiaControl.Background is not GradientBrush) SkiaControl.Background = value;
        }, () => (SkiaControl.Background as SolidColorBrush)?.Color ?? solid);
        EffectEditors(SkiaControl, null, (view, brush) => view.Background = brush ?? solid);
        Number(nameof(Rotation), -45, 45, 0, value => SkiaControl.Rotation = value, () => SkiaControl.Rotation);
        Number(nameof(Scale), 0.25, 1.25, 1, value => SkiaControl.Scale = value, () => SkiaControl.Scale);
        var taps = 0;
        SkiaControl.Tapped += (_, _) => Feedback($"Taps: {++taps}");
        Toggle(nameof(InputTransparent), false, value => SkiaControl.InputTransparent = value, () => SkiaControl.InputTransparent);
        // Press feedback from the look (Dim / Ripple and speed: Look & colors page).
        Toggle(nameof(SkUiView.ShowsPressEffect), true, value => SkiaControl.ShowsPressEffect = value, () => SkiaControl.ShowsPressEffect);
        OnReset(() => taps = 0);
    }
}
