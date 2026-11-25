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

    /// <summary>One source on two items of a control: it goes on with the other when one lets it go.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ASharedIconKeepsItsContextWhenOneItemLetsItGo(bool removed)
    {
        var icon = Icon(Colors.Lime);
        icon.SetBinding(SkUiVectorImageSource.ColorProperty, static (Model model) => model.Accent);
        var (a, b) = (new SkUiTabItem { Icon = icon }, new SkUiTabItem { Icon = icon });
        var tabs = new SkUiTabView { BindingContext = new Model(Colors.Magenta) }.AddTab(a).AddTab(b);
        Assert.Equal(Colors.Magenta, icon.Color);
        if (removed)
            tabs.Tabs.Remove(b);
        else
            b.Icon = null;
        Assert.Equal(Colors.Magenta, icon.Color);

        var other = Icon(Colors.Lime);
        other.SetBinding(SkUiVectorImageSource.ColorProperty, static (Model model) => model.Accent);
        var control = new SkUiSegmentedControl { BindingContext = new Model(Colors.Magenta) };
        control.Segments.Add(new SkUiSegment { Icon = other });
        control.Segments.Add(new SkUiSegment { Icon = other });
        if (removed)
            control.Segments.RemoveAt(1);
        else
            control.Segments[1].Icon = null;
        Assert.Equal(Colors.Magenta, other.Color);
        control.BindingContext = new Model(Colors.Orange);
        Assert.Equal(Colors.Orange, other.Color);
    }

    [Fact]
    public void AnIconDisposedWhileItsSourceTellsAChangeLoadsNothing()
    {
        var icon = Icon(Colors.Lime);
        var image = new SkUiImage { Source = icon }; // listens before the tab's icon does
        var tabs = new SkUiTabView().AddTab(new SkUiTabItem("A", new SkUiBox())).AddTab(new SkUiTabItem { Icon = icon });
        var removed = 0;
        image.LoadingStarted += (_, _) =>
        {
            if (tabs.Tabs.Count < 2)
                return;
            tabs.Tabs.RemoveAt(1); // as the change is told
            removed++;
        };
        icon.Color = Colors.Magenta; // threw: the disposed icon loaded again
        Assert.Equal((1, 1), (removed, tabs.Tabs.Count));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ASharedIconTakesTheContextOfTheItemThatShowsIt(bool tabs)
    {
        // The item that had the icon last is not in the control (it never was, or it left).
        var icon = Icon(Colors.Lime);
        icon.SetBinding(SkUiVectorImageSource.ColorProperty, static (Model model) => model.Accent);
        if (tabs)
        {
            var (a, b) = (new SkUiTabItem { Icon = icon }, new SkUiTabItem { Icon = icon });
            var view = new SkUiTabView { BindingContext = new Model(Colors.Magenta) }.AddTab(a);
            Assert.Equal(Colors.Magenta, icon.Color);
            view.AddTab(b);
            view.Tabs.Clear();
            view.AddTab(a);
            Assert.Equal(Colors.Magenta, icon.Color);
        }
        else
        {
            var (a, b) = (new SkUiSegment { Icon = icon }, new SkUiSegment { Icon = icon });
            var control = new SkUiSegmentedControl { BindingContext = new Model(Colors.Magenta) };
            control.Segments.Add(a);
            Assert.Equal(Colors.Magenta, icon.Color);
            control.Segments.Add(b);
            control.Segments.Clear();
            control.Segments.Add(a);
            Assert.Equal(Colors.Magenta, icon.Color);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RemovingAnItemThatDoesNotOwnASharedIconLeavesItAsItIs(bool tabs)
    {
        var icon = Icon(Colors.Lime);
        icon.SetBinding(SkUiVectorImageSource.ColorProperty, static (Model model) => model.Accent);
        if (tabs)
        {
            var first = new SkUiTabView { BindingContext = new Model(Colors.Orange) };
            first.AddTab(new SkUiTabItem { Icon = icon }).AddTab(new SkUiTabItem { Icon = icon });
            var second = new SkUiTabView { BindingContext = new Model(Colors.Magenta) }.AddTab(new SkUiTabItem { Icon = icon }); // owns it
            first.Tabs.RemoveAt(0);
            GC.KeepAlive(second);
        }
        else
        {
            var first = new SkUiSegmentedControl { BindingContext = new Model(Colors.Orange) };
            first.Segments.Add(new SkUiSegment { Icon = icon });
            first.Segments.Add(new SkUiSegment { Icon = icon });
            var second = new SkUiSegmentedControl { BindingContext = new Model(Colors.Magenta) };
            second.Segments.Add(new SkUiSegment { Icon = icon });
            first.Segments.RemoveAt(0);
            GC.KeepAlive(second);
        }
        Assert.Equal(Colors.Magenta, icon.Color);
    }
}
