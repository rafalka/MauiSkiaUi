using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// 1.17: with an item effect the snap point lines up the current item, also with several items per snap point, and a centered
/// strip that does not loop has room at its ends, so every page's first card is drawn centered and facing, on both carousels,
/// right to left and vertical, and when the effect is switched on while the carousel rests. Without an effect nothing changes.
/// </summary>
public class CarouselEffectCenteringTests
{
    private const int Width = 340;
    private const int Height = 200;
    private const int Items = 7; // pages of 2 and 3 end with a short one

    private static readonly SKColor[] Cards = [.. Enumerable.Range(0, Items).Select(static index => new SKColor((byte)(30 + 30 * index), 40, (byte)(250 - 30 * index)))];

    private static Color CardColor(object? item) => item is int index ? Color.FromRgb(Cards[index].Red, Cards[index].Green, Cards[index].Blue) : Colors.White;

    /// <summary>A carousel on either layer, with what the tests need of it.</summary>
    private sealed record Carousel(SkUiView Root, Action<SkUiCarouselEffect?> SetEffect, Func<int, Task> GoTo, Func<int> Position, Func<double> ScrollPosition);

    private static Carousel Make(bool core, int perPage, double extent, SkUiCarouselEffect? effect, bool loop, bool rightToLeft = false, bool vertical = false)
    {
        var items = Enumerable.Range(0, Items).ToList();
        var direction = rightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        if (core)
        {
            var carousel = new SkUiCoreCarouselView().SetLoop(loop).SetItemsSource(items).SetItemsPerSnapPoint(perPage).SetItemExtent(extent).SetItemSpacing(6)
                .SetItemEffect(effect).SetItemFactory(static _ => new SkUiCoreBox(), static (node, item) => ((SkUiCoreBox)node).SetColor(CardColor(item)));
            if (vertical)
                carousel.SetOrientation(ItemsLayoutOrientation.Vertical);
            return new Carousel(new SkUiCoreHost { Background = Colors.White, FlowDirection = direction }.SetContent(carousel), value => carousel.SetItemEffect(value),
                index => carousel.ScrollToIndex(index, animated: false), () => carousel.Position, () => carousel.ScrollPosition);
        }
        var skui = new SkUiCarouselView
        {
            Loop = loop, ItemsSource = items, ItemsPerSnapPoint = perPage, ItemExtent = extent, ItemSpacing = 6, ItemEffect = effect,
            ItemTemplate = new DataTemplate(static () =>
            {
                var box = new SkUiBox();
                box.BindingContextChanged += static (sender, _) => ((SkUiBox)sender!).Color = CardColor(((SkUiBox)sender).BindingContext);
                return box;
            })
        };
        if (vertical)
            skui.Orientation = ItemsLayoutOrientation.Vertical;
        return new Carousel(new SkUiContentView { Content = skui, Background = Colors.White, FlowDirection = direction }, value => skui.ItemEffect = value,
            index => skui.ScrollToIndex(index, animated: false), () => skui.Position, () => skui.ScrollPosition);
    }

    /// <summary>Where the card of item <paramref name="index"/> is drawn along the axis through the middle: its start and its end.</summary>
    private static (int Start, int End) Drawn(SkUiTestSurface surface, int index, bool vertical)
    {
        var (start, end) = (-1, -1);
        for (var along = 0; along < (vertical ? Height : Width); along++)
        {
            if ((vertical ? surface.Bitmap.GetPixel(Width / 2, along) : surface.Bitmap.GetPixel(along, Height / 2)) != Cards[index])
                continue;
            start = start < 0 ? along : start;
            end = along + 1;
        }
        return (start, end);
    }

    private static void AssertFacingInTheMiddle(SkUiTestSurface surface, int index, double extent, bool vertical, string when)
    {
        var (start, end) = Drawn(surface, index, vertical);
        var middle = (vertical ? Height : Width) / 2d;
        Assert.True(start >= 0 && Math.Abs((start + end) / 2d - middle) <= 1, $"{when}: card {index} drawn {start}..{end}, its middle not at {middle}");
        Assert.True(Math.Abs(end - start - extent) <= 2, $"{when}: card {index} is {end - start} wide, not {extent}: it does not face");
    }

    private static double Extent(int perPage, double extent, bool vertical) =>
        extent > 0 ? extent : ((vertical ? Height : Width) - 6 * (perPage - 1)) / (double)perPage;

    public static TheoryData<bool, int, string, bool, bool, bool> Cases()
    {
        var data = new TheoryData<bool, int, string, bool, bool, bool>();
        foreach (var core in new[] { false, true })
            foreach (var perPage in new[] { 1, 2, 3 })
                foreach (var effect in new[] { "cover", "scale" })
                    foreach (var loop in new[] { true, false })
                    {
                        data.Add(core, perPage, effect, loop, false, false);
                        if (effect == "cover")
                        {
                            data.Add(core, perPage, effect, loop, true, false);
                            data.Add(core, perPage, effect, loop, false, true);
                        }
                    }
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task EveryPagesFirstCardIsDrawnInTheMiddleFacing(bool core, int perPage, string effectName, bool loop, bool rightToLeft, bool vertical)
    {
        SkUiCarouselEffect effect = effectName == "cover" ? new SkUiCoverFlowEffect() : new SkUiScaleEffect();
        var itemExtent = perPage == 1 ? 90 : 0; // several per snap point: a page fills the carousel
        var carousel = Make(core, perPage, itemExtent, effect, loop, rightToLeft, vertical);
        using var surface = new SkUiTestSurface(carousel.Root, Width, Height);
        surface.Frame(0);
        surface.Frame(16);
        var extent = Extent(perPage, itemExtent, vertical);
        AssertFacingInTheMiddle(surface, 0, extent, vertical, "at first");

        for (var page = 0; page < Items; page += perPage)
        {
            await carousel.GoTo(page);
            surface.Frame(100 + page * 100);
            surface.Frame(150 + page * 100);
            Assert.Equal(page, carousel.Position());
            AssertFacingInTheMiddle(surface, page, extent, vertical, $"on the page from {page}");
        }
    }

    // 328 / 3: three cards fill the carousel, so at the ends of a strip that does not loop another item than the current one is at the snap point.
    [Theory]
    [InlineData(false, 1, 90, true)]
    [InlineData(false, 1, 90, false)]
    [InlineData(false, 1, 328d / 3, true)]
    [InlineData(false, 1, 328d / 3, false)]
    [InlineData(false, 2, 0, true)]
    [InlineData(false, 2, 0, false)]
    [InlineData(true, 1, 90, true)]
    [InlineData(true, 1, 90, false)]
    [InlineData(true, 1, 328d / 3, true)]
    [InlineData(true, 1, 328d / 3, false)]
    [InlineData(true, 2, 0, true)]
    [InlineData(true, 2, 0, false)]
    public async Task SwitchingTheEffectOnAndOffKeepsTheCurrentItem(bool core, int perPage, double itemExtent, bool loop)
    {
        var carousel = Make(core, perPage, itemExtent, null, loop);
        using var surface = new SkUiTestSurface(carousel.Root, Width, Height);
        surface.Frame(0);
        var time = 100;
        foreach (var page in new[] { 0, 2 * perPage, (Items - 1) / perPage * perPage })
        {
            await carousel.GoTo(page);
            surface.Frame(time += 50);
            var before = (carousel.Position(), Drawn(surface, page, vertical: false));
            Assert.Equal(page, carousel.Position());

            carousel.SetEffect(new SkUiCoverFlowEffect());
            surface.Frame(time += 50);
            surface.Frame(time += 50);
            Assert.Equal((page, (double)page), (carousel.Position(), Math.Round(carousel.ScrollPosition(), 3)));
            AssertFacingInTheMiddle(surface, page, Extent(perPage, itemExtent, vertical: false), vertical: false, $"page {page}, effect on");

            carousel.SetEffect(null);
            surface.Frame(time += 50);
            surface.Frame(time += 50);
            Assert.Equal(before, (carousel.Position(), Drawn(surface, page, vertical: false)));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AFlingToTheEndOfAStripThatDoesNotLoopRestsOnTheLastPageCentered(bool core)
    {
        var carousel = Make(core, 3, 0, new SkUiCoverFlowEffect(), loop: false);
        using var surface = new SkUiTestSurface(carousel.Root, Width, Height);
        surface.Frame(0);
        var time = 0d;
        for (var swipe = 0; swipe < 4; swipe++)
        {
            var id = 420_000 + swipe;
            carousel.Root.Touch(new(id, SkUiTouchAction.Pressed, new Point(300, 100), TimeSpan.FromMilliseconds(time)));
            carousel.Root.Touch(new(id, SkUiTouchAction.Moved, new Point(220, 100), TimeSpan.FromMilliseconds(time + 10)));
            carousel.Root.Touch(new(id, SkUiTouchAction.Moved, new Point(60, 100), TimeSpan.FromMilliseconds(time + 20)));
            carousel.Root.Touch(new(id, SkUiTouchAction.Released, new Point(60, 100), TimeSpan.FromMilliseconds(time + 25)));
            for (var ms = time + 30; ms < time + 1900; ms += 16)
                surface.Frame(ms);
            time += 2000;
        }
        Assert.Equal(6, carousel.Position()); // pages 0, 3 and 6
        AssertFacingInTheMiddle(surface, 6, Extent(3, 0, vertical: false), vertical: false, "after the fling");
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public void WithoutAnEffectAPageFillsTheCarousel(bool core, bool loop)
    {
        var carousel = Make(core, 2, 0, null, loop);
        using var surface = new SkUiTestSurface(carousel.Root, Width, Height);
        surface.Frame(0);
        Assert.Equal((0, 167), Drawn(surface, 0, vertical: false));
        Assert.Equal((173, 340), Drawn(surface, 1, vertical: false));
    }
}
