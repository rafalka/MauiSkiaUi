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
        Number(nameof(SkUiImageButton.CornerRadius), 0, 30, 8, value => { skia.CornerRadius = value; native.CornerRadius = (int)Math.Round(value); }, () => skia.CornerRadius);
        Toggle(nameof(VisualElement.IsEnabled), true, value => { skia.IsEnabled = value; native.IsEnabled = value; }, () => skia.IsEnabled, () => native.IsEnabled);
        OnReset(() => { skiaClicks = nativeClicks = 0; Counts(); });
    }
}
