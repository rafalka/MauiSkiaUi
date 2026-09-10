using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Side-by-side property playground for <see cref="SkUiContentView"/>.</summary>
public sealed class ContentViewDemoPage : ComponentDemoPage
{
    public ContentViewDemoPage() : base(nameof(SkUiContentView), new SkUiContentView(), new ContentView())
    {
        var skia = (SkUiContentView)SkiaControl;
        var native = (ContentView)NativeControl!;
        var drawn = new SkUiLabel { Background = Accent, TextColor = Colors.White, FontSize = 16 };
        var standard = new Label { Background = Accent, TextColor = Colors.White, FontSize = 16 };
        skia.Content = drawn;
        native.Content = standard;
        Text("ContentText", "Hosted content", value => { drawn.Text = value; standard.Text = value; }, () => drawn.Text, () => standard.Text);
        Number(nameof(SkUiContentView.Padding), 0, 28, 12, value => { skia.Padding = value; native.Padding = value; }, () => skia.Padding.Left, () => native.Padding.Left);
        ColorEditor(nameof(VisualElement.Background), DemoColors.SoftSurface, value => { skia.Background = value; native.Background = value; },
            () => ((SolidColorBrush)skia.Background).Color, () => ((SolidColorBrush)native.Background).Color);
        Toggle("HasContent", true, value => { skia.Content = value ? drawn : null; native.Content = value ? standard : null; }, () => skia.Content is not null, () => native.Content is not null);
    }
}
