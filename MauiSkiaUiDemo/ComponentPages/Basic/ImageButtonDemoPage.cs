using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Side-by-side property playground for <see cref="SkUiImageButton"/>.</summary>
public sealed class ImageButtonDemoPage : ComponentDemoPage
{
    public ImageButtonDemoPage() : base(nameof(SkUiImageButton), new SkUiImageButton(), new ImageButton())
    {
        var skia = (SkUiImageButton)SkiaControl;
        var native = (ImageButton)NativeControl!;
        var skiaClicks = 0;
        var nativeClicks = 0;
        void Source()
        {
            skia.Source = ImageSource.FromFile(DemoAssets.EarthImage);
            native.Source = ImageSource.FromFile(DemoAssets.EarthImage);
        }
        Source();
        native.BackgroundColor = Colors.Transparent;
        void Counts() => Feedback($"Clicks: {skiaClicks}", $"Clicks: {nativeClicks}");
        skia.Clicked += (_, _) => { skiaClicks++; Counts(); };
        native.Clicked += (_, _) => { nativeClicks++; Counts(); };
        Number(nameof(SkUiImageButton.CornerRadius), 0, 30, 8, value => { skia.CornerRadius = (int)Math.Round(value); native.CornerRadius = (int)Math.Round(value); }, () => skia.CornerRadius, () => native.CornerRadius, whole: true);
        Number(nameof(SkUiImageButton.Padding), 0, 24, 0, value => { skia.Padding = new Thickness(value); native.Padding = new Thickness(value); }, () => skia.Padding.Left, () => native.Padding.Left);
        Number(nameof(SkUiImageButton.BorderWidth), 0, 8, 0, value => { skia.BorderWidth = value; native.BorderWidth = value; }, () => skia.BorderWidth, () => native.BorderWidth);
        ColorEditor(nameof(SkUiImageButton.BorderColor), Ink, value => { skia.BorderColor = value; native.BorderColor = value; }, () => skia.BorderColor, () => native.BorderColor);
        Choice(nameof(SkUiImageButton.Aspect), [Aspect.AspectFit, Aspect.AspectFill, Aspect.Fill, Aspect.Center], Aspect.AspectFit,
            value => { skia.Aspect = value; native.Aspect = value; }, () => skia.Aspect, () => native.Aspect);
        Toggle(nameof(VisualElement.IsEnabled), true, value => { skia.IsEnabled = value; native.IsEnabled = value; }, () => skia.IsEnabled, () => native.IsEnabled);
        OnReset(() => { skiaClicks = nativeClicks = 0; Counts(); });
    }
}
