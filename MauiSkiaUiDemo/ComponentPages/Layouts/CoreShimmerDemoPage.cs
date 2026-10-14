using MauiSkiaUi;
using MauiSkiaUi.Core;

namespace MauiSkiaUiDemo;

/// <summary>
/// A feed loading, the recommended way: one <see cref="SkUiCoreShimmer"/> around a skeleton of plain Core bones (three posts'
/// avatars, lines and pictures), then the loaded posts. One group and one effect for every bone, recorded once; "Reload" shows the skeleton for two seconds and fades to the posts.
/// </summary>
public sealed class CoreShimmerDemoPage : ComponentDemoPage
{
    private const string NoFade = "None";
    private const string FadeOut = "FadeOut 350 CubicInOut";
    private static readonly string[] Fades = [NoFade, FadeOut];
    private static readonly string[] Authors = ["Ada Lovelace", "Grace Hopper", "Alan Turing", "Barbara Liskov"];
    private IDispatcherTimer? _reload;

    public CoreShimmerDemoPage()
        : base(nameof(SkUiCoreShimmer), CreateHost(out var shimmer), native: null, widthRange: (200, 400, 320), heightRange: (160, 600, 420))
    {
        var fade = FadeOut;
        shimmer.SetBeforeStateChangeAnimation(SkUiViewAnimation.FadeOut(350, Easing.CubicInOut));
        Toggle(nameof(SkUiCoreShimmer.IsLoading), true, value => shimmer.IsLoading = value, () => shimmer.IsLoading);
        ActionButton("Reload (2 s)", () =>
        {
            shimmer.IsLoading = true;
            _reload?.Stop();
            _reload = Dispatcher.CreateTimer();
            _reload.Interval = TimeSpan.FromSeconds(2);
            _reload.IsRepeating = false;
            _reload.Tick += (_, _) => shimmer.IsLoading = false;
            _reload.Start();
        });
        Choice(nameof(SkUiCoreShimmer.Effect), [SkUiShimmerEffect.Wave, SkUiShimmerEffect.Pulse, SkUiShimmerEffect.Color, SkUiShimmerEffect.None], SkUiShimmerEffect.Wave,
            value => shimmer.Effect = value, () => shimmer.Effect);
        Choice(nameof(SkUiCoreShimmer.WaveDirection),
            [SkUiShimmerWaveDirection.Default, SkUiShimmerWaveDirection.LeftToRight, SkUiShimmerWaveDirection.TopToBottom],
            SkUiShimmerWaveDirection.Default, value => shimmer.WaveDirection = value, () => shimmer.WaveDirection);
        Number(nameof(SkUiCoreShimmer.Duration), 200, 3000, 1000, value => shimmer.Duration = value, () => shimmer.Duration, whole: true);
        Choice("Switch animation", Fades, FadeOut, value =>
        {
            fade = value;
            shimmer.BeforeStateChangeAnimation = value == FadeOut ? SkUiViewAnimation.FadeOut(350, Easing.CubicInOut) : null;
        }, () => fade);
    }

    /// <inheritdoc />
    protected override void OnDisappearing()
    {
        _reload?.Stop();
        base.OnDisappearing();
    }

    private static SkUiCoreHost CreateHost(out SkUiCoreShimmer shimmer)
    {
        shimmer = new SkUiCoreShimmer { SkeletonContent = Skeleton(), Content = Posts(), VerticalAlignment = LayoutAlignment.Start };
        var host = new SkUiCoreHost { BackgroundColor = Colors.White };
        host.SetContent(shimmer);
        return host;
    }

    /// <summary>Three posts' bones.</summary>
    private static SkUiCoreNode Skeleton()
    {
        var feed = new SkUiCoreVerticalStackLayout();
        for (var post = 0; post < 3; post++)
            feed.Add(PostSkeleton());
        return feed;
    }

    /// <summary>One post's bones: an avatar, a name, a line and a picture. Their color does not matter: the shimmer fills them.</summary>
    private static SkUiCoreNode PostSkeleton()
    {
        var header = new SkUiCoreHorizontalStackLayout { Spacing = 10 };
        header.Add(new SkUiCoreBox { Color = Colors.Black, CornerRadius = 18, Width = 36, Height = 36 });
        var lines = new SkUiCoreVerticalStackLayout { Spacing = 6, VerticalAlignment = LayoutAlignment.Center };
        lines.Add(new SkUiCoreBox { Color = Colors.Black, CornerRadius = 4, Width = 120, Height = 12 });
        lines.Add(new SkUiCoreBox { Color = Colors.Black, CornerRadius = 4, Width = 80, Height = 10 });
        header.Add(lines);
        var post = new SkUiCoreVerticalStackLayout { Padding = new Thickness(12), Spacing = 10 };
        post.Add(header);
        post.Add(new SkUiCoreBox { Color = Colors.Black, CornerRadius = 4, Height = 12 });
        post.Add(new SkUiCoreBox { Color = Colors.Black, CornerRadius = 8, Height = 64 });
        return post;
    }

    private static SkUiCoreNode Posts()
    {
        var feed = new SkUiCoreVerticalStackLayout();
        foreach (var author in Authors)
        {
            var post = new SkUiCoreVerticalStackLayout { Padding = new Thickness(12), Spacing = 6 };
            post.Add(new SkUiCoreLabel().SetText(author).SetFontSize(15).SetFontAttributes(FontAttributes.Bold).SetTextColor(DemoColors.Ink));
            post.Add(new SkUiCoreLabel().SetText("Loaded: the skeleton faded out and the posts came in.").SetFontSize(13).SetTextColor(DemoColors.Caption));
            post.Add(new SkUiCoreBox { Color = DemoColors.SoftSurface, CornerRadius = 8, Height = 64 });
            feed.Add(post);
        }
        return feed;
    }
}
