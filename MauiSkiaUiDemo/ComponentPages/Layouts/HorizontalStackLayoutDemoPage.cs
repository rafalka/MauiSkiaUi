using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Side-by-side property playground for <see cref="SkUiHorizontalStackLayout"/>.</summary>
public sealed class HorizontalStackLayoutDemoPage : ComponentDemoPage
{
    public HorizontalStackLayoutDemoPage() : base(nameof(SkUiHorizontalStackLayout), new SkUiHorizontalStackLayout(), new HorizontalStackLayout())
    {
        var skia = (SkUiHorizontalStackLayout)SkiaControl;
        var native = (HorizontalStackLayout)NativeControl!;
        for (var index = 0; index < 3; index++)
        {
            var color = index == 0 ? Accent : index == 1 ? DemoColors.SampleA : DemoColors.SampleB;
            skia.Children.Add(new SkUiBox { Color = color, WidthRequest = 24 });
            native.Add(new BoxView { Color = color, WidthRequest = 24 });
        }
        Number(nameof(SkUiHorizontalStackLayout.Spacing), 0, 24, 6, value => { skia.Spacing = value; native.Spacing = value; }, () => skia.Spacing, () => native.Spacing);
    }
}
