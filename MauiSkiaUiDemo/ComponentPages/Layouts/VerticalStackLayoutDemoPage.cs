using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Side-by-side property playground for <see cref="SkUiVerticalStackLayout"/>.</summary>
public sealed class VerticalStackLayoutDemoPage : ComponentDemoPage
{
    public VerticalStackLayoutDemoPage() : base(nameof(SkUiVerticalStackLayout), new SkUiVerticalStackLayout(), new VerticalStackLayout())
    {
        var skia = (SkUiVerticalStackLayout)SkiaControl;
        var native = (VerticalStackLayout)NativeControl!;
        for (var index = 0; index < 3; index++)
        {
            var color = index == 0 ? Accent : index == 1 ? DemoColors.SampleA : DemoColors.SampleB;
            skia.Children.Add(new SkUiBox { Color = color, HeightRequest = 24 });
            native.Add(new BoxView { Color = color, HeightRequest = 24 });
        }
        Number(nameof(SkUiVerticalStackLayout.Spacing), 0, 24, 6, value => { skia.Spacing = value; native.Spacing = value; }, () => skia.Spacing, () => native.Spacing);
    }
}
