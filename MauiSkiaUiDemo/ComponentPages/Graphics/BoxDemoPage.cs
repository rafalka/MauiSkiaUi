using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Side-by-side property playground for <see cref="SkUiBox"/> against MAUI's BoxView.</summary>
public sealed class BoxDemoPage : ComponentDemoPage
{
    public BoxDemoPage() : base(nameof(SkUiBox), new SkUiBox(), new BoxView())
    {
        var skia = (SkUiBox)SkiaControl;
        var native = (BoxView)NativeControl!;
        // The color is the box's fill; the brush editor below clears it for a gradient Background, as MAUI's BoxView.
        var color = Accent;
        ColorEditor(nameof(SkUiBox.Color), Accent, value =>
        {
            color = value;
            if (skia.Color is null) return;
            skia.Color = value;
            native.Color = value;
        }, () => skia.Color ?? color, () => native.Color ?? color);
        Number(nameof(SkUiBox.CornerRadius), 0, 60, 0, value => { skia.CornerRadius = value; native.CornerRadius = value; },
            () => skia.CornerRadius.TopLeft, () => native.CornerRadius.TopLeft);
        Number(nameof(VisualElement.Rotation), -45, 45, 0, value => { skia.Rotation = value; native.Rotation = value; }, () => skia.Rotation, () => native.Rotation);
        // A gradient fills the box through its Background once Color is cleared, as MAUI's BoxView.
        EffectEditors(skia, native, (view, brush) =>
        {
            if (view is SkUiBox box) box.Color = brush is null ? color : null;
            if (view is BoxView boxView) boxView.Color = brush is null ? color : null;
            view.Background = brush;
        });
    }
}
