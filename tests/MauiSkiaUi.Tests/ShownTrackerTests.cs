using System.Runtime.CompilerServices;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>1.17: a view dropped with its <c>IsShownChanged</c> handlers (a carousel that autoplays) stops counting as watched.</summary>
[Collection(GlobalStateCollection.Name)]
public class ShownTrackerTests
{
    /// <summary>Garbage of earlier tests is collected, so the app-wide count holds still.</summary>
    private static int Settled(int until = -1)
    {
        var count = -1;
        for (var round = 0; round < 30; round++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var now = SkUiShownTracker.WatchedViews;
            if (until >= 0 ? now <= until : now == count)
                break;
            count = now;
        }
        return SkUiShownTracker.WatchedViews;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ShowAndDrop(int idle)
    {
        var carousel = new SkUiCarouselView { ItemsSource = new[] { 1, 2, 3 }, ItemTemplate = new DataTemplate(static () => new SkUiBox()), AutoPlayInterval = TimeSpan.FromSeconds(5) };
        Assert.Equal(idle + 1, SkUiShownTracker.WatchedViews);
        using var surface = new SkUiTestSurface(new SkUiContentView { Content = carousel }, 300, 200);
        surface.Frame(0);
    }

    [Fact]
    public void AnAutoplayingCarouselThatIsDroppedStopsCounting()
    {
        var idle = Settled();
        ShowAndDrop(idle);
        Assert.Equal(idle, Settled(until: idle));
    }

    [Fact]
    public void HandlersAddedAndRemovedCountOnce()
    {
        var before = Settled();
        var view = new SkUiBox();
        EventHandler handler = static (_, _) => { };
        for (var round = 0; round < 3; round++)
        {
            view.IsShownChanged += handler;
            view.IsShownChanged += handler;
            Assert.Equal(before + 1, SkUiShownTracker.WatchedViews);
            view.IsShownChanged -= handler;
            view.IsShownChanged -= handler;
            Assert.Equal(before, SkUiShownTracker.WatchedViews);
        }
        GC.KeepAlive(view);
    }
}
