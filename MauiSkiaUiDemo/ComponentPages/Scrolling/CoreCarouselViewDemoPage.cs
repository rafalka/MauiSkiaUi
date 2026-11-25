using MauiSkiaUi;
using MauiSkiaUi.Core;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiCoreCarouselView"/> (1.17 C5) with a linked <see cref="SkUiCoreIndicatorView"/>: cards made by an item
/// factory and recycled as the strip scrolls; editors for looping, peek insets, spacing, the item extent, several items per
/// snap point, the item effect and autoplay. The feedback line reports the position, the scroll position and how many card
/// nodes the factory made.
/// </summary>
public sealed class CoreCarouselViewDemoPage : ComponentDemoPage
{
    private const string NoEffect = "None";
    private const string CoverFlow = "Cover flow";
    private const string ScaleEffect = "Scale";

    private static readonly Color[] Palette =
        [Color.FromArgb("#2563EB"), Color.FromArgb("#7C3AED"), Color.FromArgb("#DB2777"), Color.FromArgb("#EA580C"), Color.FromArgb("#16A34A"), Color.FromArgb("#0891B2")];

    private readonly SkUiCoreCarouselView _carousel;
    private readonly SkUiCoverFlowEffect _coverFlow = new();
    private readonly SkUiScaleEffect _scale = new();
    private int _created;

    public CoreCarouselViewDemoPage()
        : base(nameof(SkUiCoreCarouselView), CreateHost(out var carousel, out var indicator), native: null,
            widthRange: (200, 420, 340), heightRange: (160, 420, 260))
    {
        _carousel = carousel;
        carousel.SetItemFactory(_ =>
        {
            _created++;
            return Card();
        }, static (node, item) => Bind(node, (int)item!));
        void Report() => Feedback($"Position {carousel.Position} · scroll {carousel.ScrollPosition:F2} · {_created} card nodes made");
        carousel.PositionChanged += (_, _) => Report();
        carousel.Scrolled += (_, _) => Report();

        Toggle(nameof(SkUiCoreCarouselView.Loop), true, value => carousel.SetLoop(value), () => carousel.Loop);
        Number("PeekAreaInsets (left, right)", 0, 80, 0, value => carousel.SetPeekAreaInsets(new Thickness(value, 0)), () => carousel.PeekAreaInsets.Left, whole: true);
        Number(nameof(SkUiCoreCarouselView.ItemSpacing), 0, 32, 0, value => carousel.SetItemSpacing(value), () => carousel.ItemSpacing, whole: true);
        Number(nameof(SkUiCoreCarouselView.ItemExtent), 0, 300, 0, value => carousel.SetItemExtent(value), () => carousel.ItemExtent, whole: true);
        Number(nameof(SkUiCoreCarouselView.ItemsPerSnapPoint), 1, 4, 1, value => carousel.SetItemsPerSnapPoint((int)value), () => carousel.ItemsPerSnapPoint, whole: true);
        Choice(nameof(SkUiCoreCarouselView.SnapPointsAlignment), Enum.GetValues<SnapPointsAlignment>(), SnapPointsAlignment.Center,
            value => carousel.SetSnapPointsAlignment(value), () => carousel.SnapPointsAlignment);
        Choice(nameof(SkUiCoreCarouselView.ItemEffect), [NoEffect, CoverFlow, ScaleEffect], NoEffect, value => carousel.SetItemEffect(value switch
        {
            CoverFlow => _coverFlow,
            ScaleEffect => _scale,
            _ => null
        }), () => carousel.ItemEffect switch
        {
            SkUiCoverFlowEffect => CoverFlow,
            SkUiScaleEffect => ScaleEffect,
            _ => NoEffect
        });
        Number("AutoPlayInterval (seconds, 0: off)", 0, 8, 0, value => carousel.SetAutoPlayInterval(TimeSpan.FromSeconds(value)),
            () => carousel.AutoPlayInterval.TotalSeconds, whole: true);
        Toggle(nameof(SkUiCoreCarouselView.IsAutoPlayPaused), false, value => carousel.SetIsAutoPlayPaused(value), () => carousel.IsAutoPlayPaused);
        ActionButton("Previous", () => Step(-1));
        ActionButton("Next", () => Step(1));
        OnReset(() => carousel.SetPosition(0));
        _ = indicator;
    }

    private void Step(int delta)
    {
        var count = _carousel.ItemCount;
        var perPage = _carousel.ItemsPerSnapPoint;
        var last = (count - 1) / perPage * perPage;
        var next = _carousel.Position + delta * perPage;
        _ = _carousel.ScrollToIndex(_carousel.Loop ? next > last ? 0 : next < 0 ? last : next : Math.Clamp(next, 0, last));
    }

    /// <summary>The gallery's sample: the carousel with its cards and indicator.</summary>
    internal static SkUiCoreHost CreateSample()
    {
        var host = CreateHost(out var carousel, out _);
        carousel.SetPeekAreaInsets(new Thickness(24, 0)).SetItemFactory(static _ => Card(), static (node, item) => Bind(node, (int)item!));
        return host;
    }

    private static SkUiCoreHost CreateHost(out SkUiCoreCarouselView carousel, out SkUiCoreIndicatorView indicator)
    {
        carousel = new SkUiCoreCarouselView().SetItemsSource(Enumerable.Range(0, 12).ToList());
        carousel.SetHeight(200);
        indicator = new SkUiCoreIndicatorView().SetCarousel(carousel);
        indicator.SetHeight(24);
        var stack = new SkUiCoreVerticalStackLayout().SetSpacing(8);
        stack.Add(carousel);
        stack.Add(indicator);
        return new SkUiCoreHost().SetContent(stack);
    }

    private static SkUiCoreNode Card()
    {
        var card = new SkUiCoreBorder().SetCornerRadius(16);
        card.SetMargin(new Thickness(6, 4));
        card.SetContent(new SkUiCoreLabel().SetFontSize(22).SetTextColor(Colors.White)
            .SetHorizontalTextAlignment(TextAlignment.Center).SetVerticalTextAlignment(TextAlignment.Center));
        return card;
    }

    private static void Bind(SkUiCoreNode node, int item)
    {
        var card = (SkUiCoreBorder)node;
        card.SetBackgroundColor(Palette[item % Palette.Length]);
        ((SkUiCoreLabel)card.Content!).SetText($"Card {item + 1}");
    }
}
