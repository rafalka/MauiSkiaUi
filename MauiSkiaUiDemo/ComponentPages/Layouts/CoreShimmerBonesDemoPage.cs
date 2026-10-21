using MauiSkiaUi;
using MauiSkiaUi.Core;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiCoreShimmerBone"/>: the recommended way to build a skeleton with chrome: plain Core cards with Core bones in
/// one <see cref="SkUiCoreShimmer"/> (<see cref="SkUiShimmerMode.Bones"/>), no bindings. Only the bones shimmer.
/// </summary>
public sealed class CoreShimmerBonesDemoPage : ComponentDemoPage
{
    public CoreShimmerBonesDemoPage()
        : base(nameof(SkUiCoreShimmerBone), CreateHost(out var shimmer), native: null, widthRange: (200, 400, 320), heightRange: (160, 700, 460))
    {
        DocumentTypes(typeof(SkUiCoreShimmer));
        Toggle(nameof(SkUiCoreShimmer.IsLoading), true, value => shimmer.IsLoading = value, () => shimmer.IsLoading);
        Choice(nameof(SkUiCoreShimmer.Mode), [SkUiShimmerMode.Bones, SkUiShimmerMode.Silhouette], SkUiShimmerMode.Bones,
            value => shimmer.Mode = value, () => shimmer.Mode);
        Choice(nameof(SkUiCoreShimmer.Effect), [SkUiShimmerEffect.Wave, SkUiShimmerEffect.Pulse, SkUiShimmerEffect.Color, SkUiShimmerEffect.None], SkUiShimmerEffect.Wave,
            value => shimmer.Effect = value, () => shimmer.Effect);
    }

    private static SkUiCoreHost CreateHost(out SkUiCoreShimmer shimmer)
    {
        var cards = new SkUiCoreVerticalStackLayout { Spacing = 12, Padding = new Thickness(12) };
        for (var card = 0; card < 5; card++)
            cards.Add(Card(card));
        shimmer = new SkUiCoreShimmer
        {
            Mode = SkUiShimmerMode.Bones, SkeletonContent = cards, VerticalAlignment = LayoutAlignment.Start,
            Content = new SkUiCoreLabel().SetText("Loaded").SetTextColor(DemoColors.Ink)
        };
        var host = new SkUiCoreHost { BackgroundColor = DemoColors.PageBackground };
        host.SetContent(shimmer);
        return host;
    }

    /// <summary>A leaderboard placeholder card: a rank circle, a name and a team, a value and its change.</summary>
    private static SkUiCoreBorder Card(int index)
    {
        var grid = new SkUiCoreGrid().SetColumnSpacing(12)
            .SetColumnDefinitions([new(SkUiCoreGridLength.Auto), new(SkUiCoreGridLength.Star), new(SkUiCoreGridLength.Auto)]);
        grid.Add(new SkUiCoreShimmerBone { CornerRadius = 16, Width = 32, Height = 32 }, 0, 0);
        grid.Add(Lines(LayoutAlignment.Start, 100 - index * 5, 80 - index * 4), 0, 1);
        grid.Add(Lines(LayoutAlignment.End, 60 - index * 2, 40 - index * 2), 0, 2);
        return new SkUiCoreBorder().SetBackgroundColor(Colors.White).SetStroke(DemoColors.Border).SetStrokeThickness(1).SetCornerRadius(8)
            .SetPadding(new Thickness(16)).SetContent(grid);
    }

    private static SkUiCoreVerticalStackLayout Lines(LayoutAlignment alignment, double first, double second)
    {
        var lines = new SkUiCoreVerticalStackLayout { Spacing = 4, VerticalAlignment = LayoutAlignment.Center };
        lines.Add(new SkUiCoreShimmerBone { CornerRadius = 4, Width = first, Height = 18, HorizontalAlignment = alignment });
        lines.Add(new SkUiCoreShimmerBone { CornerRadius = 4, Width = second, Height = 14, HorizontalAlignment = alignment });
        return lines;
    }
}
