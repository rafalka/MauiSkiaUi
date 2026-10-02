using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Side-by-side property playground for <see cref="SkUiBox"/> against MAUI's BoxView.</summary>
public sealed class BoxDemoPage : ComponentDemoPage
{
    public BoxDemoPage() : base(nameof(SkUiBox), new SkUiBox(), new BoxView())
    {
        var skia = (SkUiBox)SkiaControl;
        var native = (BoxView)NativeControl!;
        ColorEditor(nameof(SkUiBox.Color), Accent, value => { skia.Color = value; native.Color = value; }, () => skia.Color!, () => native.Color);
        Number(nameof(SkUiBox.CornerRadius), 0, 60, 0, value => { skia.CornerRadius = value; native.CornerRadius = value; },
            () => skia.CornerRadius.TopLeft, () => native.CornerRadius.TopLeft);
        Number(nameof(VisualElement.Rotation), -45, 45, 0, value => { skia.Rotation = value; native.Rotation = value; }, () => skia.Rotation, () => native.Rotation);
    }
}
