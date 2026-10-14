using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiShimmerBone"/>: a loading list of cards whose bones shimmer while the cards themselves, and a Cancel
/// button, draw as they are (<see cref="SkUiShimmerMode.Bones"/>). Switch to <see cref="SkUiShimmerMode.Silhouette"/> to see
/// the difference: the whole skeleton becomes the bone color.
/// </summary>
public sealed class ShimmerBonesDemoPage : ComponentDemoPage
{
    public ShimmerBonesDemoPage()
        : base(nameof(SkUiShimmerBone), Create(), widthRange: (200, 400, 320), heightRange: (120, 600, 300))
    {
        var shimmer = (SkUiShimmer)SkiaControl;
        Toggle(nameof(SkUiShimmer.IsLoading), true, value => shimmer.IsLoading = value, () => shimmer.IsLoading);
        Choice(nameof(SkUiShimmer.Mode), [SkUiShimmerMode.Bones, SkUiShimmerMode.Silhouette], SkUiShimmerMode.Bones,
            value => shimmer.Mode = value, () => shimmer.Mode);
        Choice(nameof(SkUiShimmer.Effect), [SkUiShimmerEffect.Wave, SkUiShimmerEffect.Pulse, SkUiShimmerEffect.Color, SkUiShimmerEffect.None], SkUiShimmerEffect.Wave,
            value => shimmer.Effect = value, () => shimmer.Effect);
        Choice(nameof(SkUiShimmer.WaveRegion), [SkUiShimmerWaveRegion.Shimmer, SkUiShimmerWaveRegion.Surface], SkUiShimmerWaveRegion.Shimmer,
            value => shimmer.WaveRegion = value, () => shimmer.WaveRegion);
        Number(nameof(SkUiShimmer.Duration), 200, 3000, 1000, value => shimmer.Duration = value, () => shimmer.Duration, whole: true);
        Feedback("Bones draw themselves on the render thread: no layer, nothing recorded per frame");
    }

    private static SkUiShimmer Create()
    {
        var cards = new SkUiVerticalStackLayout { Spacing = 12, Padding = new Thickness(12), BackgroundColor = DemoColors.PageBackground };
        for (var card = 0; card < 3; card++)
            cards.Children.Add(Card(card));
        cards.Children.Add(new SkUiButton { Text = "Cancel", HorizontalOptions = LayoutOptions.Center });
        return new SkUiShimmer
        {
            Mode = SkUiShimmerMode.Bones,
            SkeletonContent = cards,
            Content = new SkUiLabel { Text = "Loaded", Padding = new Thickness(12), TextColor = DemoColors.Ink }
        };
    }

    /// <summary>A white bordered card with a round bone and two bars.</summary>
    private static SkUiBorder Card(int index)
    {
        var lines = new SkUiVerticalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center };
        lines.Children.Add(new SkUiShimmerBone { WidthRequest = 120 - index * 10, HeightRequest = 14, CornerRadius = 4, HorizontalOptions = LayoutOptions.Start });
        lines.Children.Add(new SkUiShimmerBone { WidthRequest = 80 + index * 10, HeightRequest = 10, CornerRadius = 4, HorizontalOptions = LayoutOptions.Start });
        var row = new SkUiHorizontalStackLayout { Spacing = 12 };
        row.Children.Add(new SkUiShimmerBone { WidthRequest = 36, HeightRequest = 36, CornerRadius = 18 });
        row.Children.Add(lines);
        return new SkUiBorder
        {
            Stroke = DemoColors.Border, StrokeThickness = 1, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            BackgroundColor = Colors.White, Padding = new Thickness(12), Content = row
        };
    }
}
