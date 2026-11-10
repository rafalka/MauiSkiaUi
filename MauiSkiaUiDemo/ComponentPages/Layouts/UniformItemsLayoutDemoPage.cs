using CommunityToolkit.Maui.Layouts;
using MauiSkiaUi;
using MauiSkiaUi.CommunityToolkit;

namespace MauiSkiaUiDemo;

/// <summary>Side-by-side property playground for <see cref="SkUiUniformItemsLayout"/> and the Community Toolkit's <see cref="UniformItemsLayout"/>.</summary>
public sealed class UniformItemsLayoutDemoPage : ComponentDemoPage
{
    private const int ItemCount = 9;
    private const int NoLimit = 0; // the editors' "no limit": int.MaxValue in the layouts

    public UniformItemsLayoutDemoPage()
        : base(nameof(SkUiUniformItemsLayout), new SkUiUniformItemsLayout(), new UniformItemsLayout(), widthRange: (80, 360, 240), heightRange: (80, 320, 200))
    {
        var skia = (SkUiUniformItemsLayout)SkiaControl;
        var native = (UniformItemsLayout)NativeControl!;
        skia.BackgroundColor = Colors.LightGray;
        native.BackgroundColor = Colors.LightGray;
        for (var index = 0; index < ItemCount; index++)
        {
            var color = (index % 3) switch { 0 => Accent, 1 => DemoColors.SampleA, _ => DemoColors.SampleB };
            var text = (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            skia.Children.Add(new SkUiLabel { Text = text, Background = color, TextColor = Colors.White, FontSize = 14, Padding = new Thickness(16, 8), Margin = new Thickness(2) });
            native.Children.Add(new Label { Text = text, Background = color, TextColor = Colors.White, FontSize = 14, Padding = new Thickness(16, 8), Margin = new Thickness(2) });
        }

        Number(nameof(SkUiUniformItemsLayout.MaxRows), NoLimit, 4, NoLimit, value => { skia.MaxRows = Limit(value); native.MaxRows = Limit(value); },
            () => Editor(skia.MaxRows), () => Editor(native.MaxRows), whole: true);
        Number(nameof(SkUiUniformItemsLayout.MaxColumns), NoLimit, 6, NoLimit, value => { skia.MaxColumns = Limit(value); native.MaxColumns = Limit(value); },
            () => Editor(skia.MaxColumns), () => Editor(native.MaxColumns), whole: true);
        Number(nameof(SkUiUniformItemsLayout.Padding), 0, 24, 0, value => { skia.Padding = value; native.Padding = value; }, () => skia.Padding.Left, () => native.Padding.Left);
        Toggle("Child1.IsVisible", true, value => { ((View)skia.Children[0]).IsVisible = value; ((View)native.Children[0]).IsVisible = value; },
            () => ((View)skia.Children[0]).IsVisible, () => ((View)native.Children[0]).IsVisible);
    }

    private static int Limit(double value) => value < 1 ? int.MaxValue : (int)value;

    private static double Editor(int limit) => limit == int.MaxValue ? NoLimit : limit;
}
