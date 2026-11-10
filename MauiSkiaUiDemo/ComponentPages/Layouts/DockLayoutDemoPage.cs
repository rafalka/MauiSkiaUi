using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Layouts;
using MauiSkiaUi;
using MauiSkiaUi.CommunityToolkit;

namespace MauiSkiaUiDemo;

/// <summary>Side-by-side property playground for <see cref="SkUiDockLayout"/> and the Community Toolkit's <see cref="DockLayout"/>.</summary>
public sealed class DockLayoutDemoPage : ComponentDemoPage
{
    private static readonly (string Text, DockPosition Position)[] Parts =
    [
        ("Top", DockPosition.Top), ("Bottom", DockPosition.Bottom), ("Left", DockPosition.Left), ("Right", DockPosition.Right), ("Fill", DockPosition.None),
    ];

    public DockLayoutDemoPage()
        : base(nameof(SkUiDockLayout), new SkUiDockLayout(), new DockLayout(), widthRange: (120, 360, 280), heightRange: (80, 320, 200))
    {
        var skia = (SkUiDockLayout)SkiaControl;
        var native = (DockLayout)NativeControl!;
        skia.BackgroundColor = Colors.LightGray;
        native.BackgroundColor = Colors.LightGray;
        for (var index = 0; index < Parts.Length; index++)
        {
            var (text, position) = Parts[index];
            var color = (index % 3) switch { 0 => Accent, 1 => DemoColors.SampleA, _ => DemoColors.SampleB };
            skia.Add(new SkUiLabel { Text = text, Background = color, TextColor = Colors.White, FontSize = 14, Padding = new Thickness(8, 4) }, position);
            native.Add(new Label { Text = text, Background = color, TextColor = Colors.White, FontSize = 14, Padding = new Thickness(8, 4) }, position);
        }
        var skLeft = (BindableObject)skia.Children[2];
        var nativeLeft = (BindableObject)native.Children[2];

        Toggle(nameof(SkUiDockLayout.ShouldExpandLastChild), true, value => { skia.ShouldExpandLastChild = value; native.ShouldExpandLastChild = value; },
            () => skia.ShouldExpandLastChild, () => native.ShouldExpandLastChild);
        Number(nameof(SkUiDockLayout.HorizontalSpacing), 0, 24, 0, value => { skia.HorizontalSpacing = value; native.HorizontalSpacing = value; },
            () => skia.HorizontalSpacing, () => native.HorizontalSpacing);
        Number(nameof(SkUiDockLayout.VerticalSpacing), 0, 24, 0, value => { skia.VerticalSpacing = value; native.VerticalSpacing = value; },
            () => skia.VerticalSpacing, () => native.VerticalSpacing);
        Number(nameof(SkUiDockLayout.Padding), 0, 24, 0, value => { skia.Padding = value; native.Padding = value; }, () => skia.Padding.Left, () => native.Padding.Left);
        Choice("Child3.DockPosition", Enum.GetValues<DockPosition>(), DockPosition.Left,
            value => { SkUiDockLayout.SetDockPosition(skLeft, value); DockLayout.SetDockPosition(nativeLeft, value); },
            () => SkUiDockLayout.GetDockPosition(skLeft), () => DockLayout.GetDockPosition(nativeLeft));
        Toggle("Child1.IsVisible", true, value => { ((View)skia.Children[0]).IsVisible = value; ((View)native.Children[0]).IsVisible = value; },
            () => ((View)skia.Children[0]).IsVisible, () => ((View)native.Children[0]).IsVisible);
    }
}
