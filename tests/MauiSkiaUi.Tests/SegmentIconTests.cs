using System.Diagnostics;
using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// 1.17: the icons of segments and tab headers follow their own sources: a color changed on the source (as a theme binding or a
/// shared resource does) draws, on both layers, also when the source's color is bound to the item's context.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class SegmentIconTests : IDisposable
{
    private const string Square = """<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24"><rect width="24" height="24" fill="currentColor"/></svg>""";

    private readonly long _memoryBudget = SkUiImageCache.MemoryCacheMaxBytes;
    private readonly string _directory = Directory.CreateTempSubdirectory("skui-segment-icon").FullName;

    public SegmentIconTests() => SkUiImageCache.MemoryCacheMaxBytes = 64L * 1024 * 1024;

    public void Dispose()
    {
        SkUiImageCache.ClearMemoryCache();
        SkUiImageCache.MemoryCacheMaxBytes = _memoryBudget;
        Directory.Delete(_directory, recursive: true);
    }

    private SkUiVectorImageSource Icon(Color color)
    {
        var path = Path.Combine(_directory, "icon.svg");
        File.WriteAllText(path, Square);
        return new SkUiVectorImageSource { Source = path, Color = color };
    }

    private static SkUiView Build(string control, ImageSource icon, object? context = null)
    {
        switch (control)
        {
            case "segmented":
            {
                var segmented = new SkUiSegmentedControl { BindingContext = context };
                segmented.Segments.Add(new SkUiSegment { Icon = icon });
                return new SkUiContentView { Content = segmented, Background = Colors.White };
            }
            case "core segmented":
            {
                var segmented = new SkUiCoreSegmentedControl();
                segmented.Segments.Add(new SkUiCoreSegment { Icon = icon });
                return new SkUiCoreHost { Background = Colors.White }.SetContent(segmented);
            }
            case "tabs":
                return new SkUiContentView
                {
                    Content = new SkUiTabView { BindingContext = context }.AddTab(new SkUiTabItem { Icon = icon, Content = new SkUiBox() }), Background = Colors.White
                };
            default:
                return new SkUiCoreHost { Background = Colors.White }.SetContent(new SkUiCoreTabView().AddTab(new SkUiCoreTabItem(null, new SkUiCoreBox()).SetIcon(icon)));
        }
    }

    /// <summary>Frames until a pixel of <paramref name="color"/> is drawn (icons load on another thread).</summary>
    private static bool Draws(SkUiTestSurface surface, SKColor color, ref double time)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < TimeSpan.FromSeconds(10))
        {
            surface.Frame(time += 16);
            for (var x = 0; x < surface.Bitmap.Width; x++)
                for (var y = 0; y < 60; y++)
                    if (surface.Bitmap.GetPixel(x, y) == color)
                        return true;
            Thread.Sleep(10);
        }
        return false;
    }

    [Theory]
    [InlineData("segmented")]
    [InlineData("core segmented")]
    [InlineData("tabs")]
    [InlineData("core tabs")]
    public void AnIconWhoseSourceChangesIsDrawnAgain(string control)
    {
        var icon = Icon(Colors.Lime);
        using var surface = new SkUiTestSurface(Build(control, icon), 120, 60);
        var time = 0d;
        Assert.True(Draws(surface, SKColors.Lime, ref time), "the icon");
        icon.Color = Colors.Magenta; // as a theme binding sets it
        Assert.True(Draws(surface, SKColors.Magenta, ref time), "the icon in its new color");
    }

    internal sealed record Model(Color Accent);

    [Theory]
    [InlineData("segmented")]
    [InlineData("tabs")]
    public void AnIconsColorBindsToItsItemsContext(string control)
    {
        var icon = Icon(Colors.Lime);
        icon.SetBinding(SkUiVectorImageSource.ColorProperty, static (Model model) => model.Accent);
        var root = Build(control, icon, new Model(Colors.Magenta));
        using var surface = new SkUiTestSurface(root, 120, 60);
        var time = 0d;
        Assert.True(Draws(surface, SKColors.Magenta, ref time), "the icon in its bound color");
    }
}

/// <summary>1.17: a view dropped with its <c>IsShownChanged</c> handlers (a carousel that autoplays) stops counting as watched.</summary>
[Collection(GlobalStateCollection.Name)]
public class ShownTrackerCollectedTests
{
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void ShowAndDrop()
    {
        var carousel = new SkUiCarouselView { ItemsSource = new[] { 1, 2, 3 }, ItemTemplate = new DataTemplate(static () => new SkUiBox()), AutoPlayInterval = TimeSpan.FromSeconds(5) };
        var root = new SkUiContentView { Content = carousel };
        var before = SkUiShownTracker.WatchedViews;
        Assert.True(before > 0);
        using var surface = new SkUiTestSurface(root, 300, 200);
        surface.Frame(0);
    }

    [Fact]
    public void AnAutoplayingCarouselThatIsDroppedStopsCounting()
    {
        Settle(int.MaxValue);
        var idle = SkUiShownTracker.WatchedViews;
        ShowAndDrop();
        Settle(idle);
        Assert.True(SkUiShownTracker.WatchedViews <= idle, $"{SkUiShownTracker.WatchedViews} watched views, {idle} before");

        static void Settle(int until)
        {
            for (var round = 0; round < 20 && (round == 0 || SkUiShownTracker.WatchedViews > until); round++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
    }

    [Fact]
    public void HandlersAddedAndRemovedCountOnce()
    {
        var view = new SkUiBox();
        EventHandler handler = static (_, _) => { };
        var before = SkUiShownTracker.WatchedViews;
        for (var round = 0; round < 3; round++)
        {
            view.IsShownChanged += handler;
            view.IsShownChanged += handler;
            Assert.Equal(before + 1, SkUiShownTracker.WatchedViews);
            view.IsShownChanged -= handler;
            view.IsShownChanged -= handler;
            Assert.Equal(before, SkUiShownTracker.WatchedViews);
        }
    }
}
