using MauiSkiaUi;
using Microsoft.Maui.Layouts;

namespace MauiSkiaUiDemo;

/// <summary>Side-by-side property playground for <see cref="SkUiFlexLayout"/> and MAUI's <see cref="FlexLayout"/>.</summary>
public sealed class FlexLayoutDemoPage : ComponentDemoPage
{
    private static readonly string[] Words = ["Flex", "Wrapping row", "Grow", "Shrink me", "Basis", "Order"];
    private static readonly string[] Bases = ["Auto", "80", "40%"];

    public FlexLayoutDemoPage()
        : base(nameof(SkUiFlexLayout), new SkUiFlexLayout(), new FlexLayout(), widthRange: (120, 360, 280), heightRange: (80, 320, 180))
    {
        var skia = (SkUiFlexLayout)SkiaControl;
        var native = (FlexLayout)NativeControl!;
        skia.BackgroundColor = Colors.LightGray;
        native.BackgroundColor = Colors.LightGray;
        for (var index = 0; index < Words.Length; index++)
        {
            var color = (index % 3) switch { 0 => Accent, 1 => DemoColors.SampleA, _ => DemoColors.SampleB };
            skia.Children.Add(new SkUiLabel { Text = Words[index], Background = color, TextColor = Colors.White, FontSize = 14, Padding = new Thickness(8, 4), Margin = new Thickness(2) });
            native.Children.Add(new Label { Text = Words[index], Background = color, TextColor = Colors.White, FontSize = 14, Padding = new Thickness(8, 4), Margin = new Thickness(2) });
        }
        var skChild = (BindableObject)skia.Children[2];
        var nativeChild = (BindableObject)native.Children[2];

        Choice(nameof(SkUiFlexLayout.Direction), Enum.GetValues<FlexDirection>(), FlexDirection.Row,
            value => { skia.Direction = value; native.Direction = value; }, () => skia.Direction, () => native.Direction);
        Choice(nameof(SkUiFlexLayout.Wrap), Enum.GetValues<FlexWrap>(), FlexWrap.Wrap,
            value => { skia.Wrap = value; native.Wrap = value; }, () => skia.Wrap, () => native.Wrap);
        Choice(nameof(SkUiFlexLayout.JustifyContent), Enum.GetValues<FlexJustify>(), FlexJustify.Start,
            value => { skia.JustifyContent = value; native.JustifyContent = value; }, () => skia.JustifyContent, () => native.JustifyContent);
        Choice(nameof(SkUiFlexLayout.AlignItems), Enum.GetValues<FlexAlignItems>(), FlexAlignItems.Start,
            value => { skia.AlignItems = value; native.AlignItems = value; }, () => skia.AlignItems, () => native.AlignItems);
        Choice(nameof(SkUiFlexLayout.AlignContent), Enum.GetValues<FlexAlignContent>(), FlexAlignContent.Start,
            value => { skia.AlignContent = value; native.AlignContent = value; }, () => skia.AlignContent, () => native.AlignContent);
        Number(nameof(SkUiFlexLayout.Padding), 0, 24, 4, value => { skia.Padding = value; native.Padding = value; }, () => skia.Padding.Left, () => native.Padding.Left);

        // Per-child attached properties on the third child ("Grow").
        Number("Child3.Grow", 0, 3, 0, value => { SkUiFlexLayout.SetGrow(skChild, (float)value); FlexLayout.SetGrow(nativeChild, (float)value); },
            () => SkUiFlexLayout.GetGrow(skChild), () => FlexLayout.GetGrow(nativeChild));
        Number("Child3.Shrink", 0, 3, 1, value => { SkUiFlexLayout.SetShrink(skChild, (float)value); FlexLayout.SetShrink(nativeChild, (float)value); },
            () => SkUiFlexLayout.GetShrink(skChild), () => FlexLayout.GetShrink(nativeChild));
        Number("Child3.Order", -2, 2, 0, value => { SkUiFlexLayout.SetOrder(skChild, (int)value); FlexLayout.SetOrder(nativeChild, (int)value); },
            () => SkUiFlexLayout.GetOrder(skChild), () => FlexLayout.GetOrder(nativeChild), whole: true);
        Choice("Child3.AlignSelf", Enum.GetValues<FlexAlignSelf>(), FlexAlignSelf.Auto,
            value => { SkUiFlexLayout.SetAlignSelf(skChild, value); FlexLayout.SetAlignSelf(nativeChild, value); },
            () => SkUiFlexLayout.GetAlignSelf(skChild), () => FlexLayout.GetAlignSelf(nativeChild));
        Choice("Child3.Basis", Bases, Bases[0],
            value => { SkUiFlexLayout.SetBasis(skChild, ToBasis(value)); FlexLayout.SetBasis(nativeChild, ToBasis(value)); },
            () => FromBasis(SkUiFlexLayout.GetBasis(skChild)), () => FromBasis(FlexLayout.GetBasis(nativeChild)));
        Toggle("Child1.IsVisible", true, value => { ((View)skia.Children[0]).IsVisible = value; ((View)native.Children[0]).IsVisible = value; },
            () => ((View)skia.Children[0]).IsVisible, () => ((View)native.Children[0]).IsVisible);
    }

    private static FlexBasis ToBasis(string value) => value switch
    {
        "80" => new FlexBasis(80),
        "40%" => new FlexBasis(0.4f, isRelative: true),
        _ => FlexBasis.Auto
    };

    private static string FromBasis(FlexBasis basis) => Array.Find(Bases, value => ToBasis(value) == basis) ?? "?";
}
