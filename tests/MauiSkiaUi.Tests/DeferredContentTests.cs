using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// <see cref="SkUiView.IsShown"/> / <see cref="SkUiView.IsShownChanged"/>, and <see cref="SkUiContentView.ContentLoading"/>:
/// content attached (and templates instantiated) only once shown.
/// </summary>
[Collection(RuntimeXamlCollection.Name)]
public class DeferredContentTests
{
    private static SkUiContentView Deferred(ISkUiView? content = null, double height = 50) => new()
    {
        ContentLoading = SkUiContentLoading.WhenShown, HeightRequest = height, Content = content
    };

    private static SkUiBox Box(double height = 50) => new() { HeightRequest = height, Color = Colors.Red };

    // A 100 × 100 surface with `deferred` in a pane that starts hidden or shown.
    private static (SkUiTestSurface Surface, SkUiVerticalStackLayout Pane) Page(SkUiContentView deferred, bool shown = true)
    {
        var pane = new SkUiVerticalStackLayout { IsVisible = shown, Children = { deferred, Box(400) } };
        var surface = new SkUiTestSurface(new SkUiContentView { Content = pane }, 100, 100);
        surface.Frame(0);
        return (surface, pane);
    }

    private static void Settle(SkUiTestSurface surface, int frames = 3)
    {
        for (var frame = 1; frame <= frames; frame++)
        {
            SkUiTestHelpers.Arrange(surface.Root, 100, 100);
            surface.Frame(frame * 16);
        }
    }

    [Fact]
    public void ContentLoadsAtOnceByDefault()
    {
        var box = Box();
        var view = new SkUiContentView { Content = box };
        Assert.True(view.IsContentLoaded);
        Assert.Same(view, box.Parent);
    }

    [Fact]
    public void DeferredContentWaitsUntilTheViewIsShown()
    {
        var box = Box();
        var view = Deferred(box);
        Assert.False(view.IsContentLoaded);
        Assert.Same(box, view.Content);
        Assert.Null(box.Parent); // set, not attached
        var loaded = 0;
        view.ContentLoaded += (_, _) => loaded++;

        var root = new SkUiContentView { Content = new SkUiVerticalStackLayout { Children = { view } } };
        Assert.False(view.IsContentLoaded); // in a tree, but no surface draws it
        using var surface = new SkUiTestSurface(root, 100, 100);
        Assert.True(view.IsContentLoaded); // the surface attached: shown
        Assert.Same(view, box.Parent);
        Assert.Equal(1, loaded);
    }

    [Fact]
    public void HiddenViewsWaitUntilShown()
    {
        var view = Deferred(Box());
        var pane = new SkUiVerticalStackLayout { IsVisible = false, Children = { view } };
        var surface = new SkUiTestSurface(new SkUiContentView { Content = pane }, 100, 100);
        using var page = surface;
        Settle(surface);
        Assert.False(view.IsContentLoaded);
        pane.IsVisible = true;
        Settle(surface);
        Assert.True(view.IsContentLoaded);
    }

    [Fact]
    public void ViewsNeverShownNeverLoad()
    {
        var view = Deferred(Box());
        _ = new SkUiVerticalStackLayout { Children = { view } };
        Assert.False(view.IsShown);
        Assert.False(view.IsContentLoaded);
    }

    [Fact]
    public void TemplatesAreInstantiatedOnlyWhenShown()
    {
        var created = 0;
        var view = new SkUiContentView
        {
            ContentTemplate = new DataTemplate(() => { created++; return new SkUiLabel { Text = "Expensive" }; }),
            ContentLoading = SkUiContentLoading.WhenShown, // after the template: nothing is created before the view is in a tree
            HeightRequest = 50,
            BindingContext = "model"
        };
        var (surface, pane) = Page(view, shown: false);
        using var page = surface;
        Settle(surface);
        Assert.Equal(0, created);
        Assert.Null(view.Content);

        pane.IsVisible = true;
        Assert.Equal(1, created);
        var label = Assert.IsType<SkUiLabel>(view.Content);
        Assert.Equal("model", label.BindingContext);
    }

    [Fact]
    public void TemplatesCreateContentAtOnceWithoutDeferral()
    {
        var view = new SkUiContentView { ContentTemplate = new DataTemplate(() => new SkUiBox()) };
        Assert.Null(view.Content); // not in a tree yet
        _ = new SkUiVerticalStackLayout { Children = { view } };
        Assert.IsType<SkUiBox>(view.Content);

        var replaced = view.Content;
        view.ContentTemplate = new DataTemplate(() => new SkUiLabel());
        Assert.IsType<SkUiLabel>(view.Content);
        Assert.Null(((Element)replaced!).Parent);

        var own = new SkUiEllipse();
        view.Content = own; // explicit content wins and stays
        view.ContentTemplate = new DataTemplate(() => new SkUiBox());
        Assert.Same(own, view.Content);

        var selector = new SkUiContentView { ContentTemplate = new KindSelector(), BindingContext = 2 };
        _ = new SkUiVerticalStackLayout { Children = { selector } };
        Assert.IsType<SkUiEllipse>(selector.Content);
    }

    [Fact]
    public void NativeTemplatesAreRejected()
    {
        var view = new SkUiContentView { ContentTemplate = new DataTemplate(() => new Label()) };
        var error = Assert.Throws<InvalidOperationException>(() => new SkUiVerticalStackLayout { Children = { view } });
        Assert.Contains("SkUiMauiContentView", error.Message);
    }

    [Fact]
    public void TheDelayRequiresStayingShown()
    {
        using var clock = new ManualGestureClock();
        var view = Deferred(Box());
        view.ContentLoadingDelay = TimeSpan.FromMilliseconds(300);
        var (surface, pane) = Page(view, shown: false);
        using var page = surface;
        Settle(surface);

        pane.IsVisible = true;
        clock.Advance(TimeSpan.FromMilliseconds(200));
        pane.IsVisible = false; // passed through before the delay
        clock.Advance(TimeSpan.FromMilliseconds(200));
        Assert.False(view.IsContentLoaded);

        pane.IsVisible = true;
        clock.Advance(TimeSpan.FromMilliseconds(299));
        Assert.False(view.IsContentLoaded);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.True(view.IsContentLoaded);
    }

    [Fact]
    public void ContentShownFromTheFirstFrameDoesNotAnimate()
    {
        var box = Box();
        var view = Deferred(box);
        view.ContentLoadedAnimation = SkUiViewAnimation.FadeIn(100);
        var (surface, _) = Page(view);
        using var page = surface;
        Assert.True(view.IsContentLoaded);
        Assert.Equal(1, box.Opacity); // nothing drawn yet: no fade on the page's first frame
    }

    [Fact]
    public void LoadContentForcesLoadingAndImmediateLoadsToo()
    {
        var first = Deferred(Box());
        first.LoadContent();
        Assert.True(first.IsContentLoaded);
        Assert.NotNull(((Element)first.Content!).Parent);

        var second = Deferred(Box());
        second.ContentLoading = SkUiContentLoading.Immediate;
        Assert.True(second.IsContentLoaded);
    }

    [Fact]
    public void DeferringAfterSettingContentTakesItBackOut()
    {
        var box = Box();
        var view = new SkUiContentView { Content = box, ContentLoading = SkUiContentLoading.WhenShown };
        Assert.False(view.IsContentLoaded);
        Assert.Null(box.Parent);
        Assert.Same(box, view.Content);
    }

    [Fact]
    public void DeferringADrawnViewHasNoEffect()
    {
        var view = new SkUiContentView { Content = Box(), HeightRequest = 50 };
        using var surface = new SkUiTestSurface(new SkUiContentView { Content = view }, 100, 100);
        surface.Frame(0);
        view.ContentLoading = SkUiContentLoading.WhenShown;
        Assert.True(view.IsContentLoaded);
    }

    [Fact]
    public void TheLoadAnimationRunsOnTheNewContent()
    {
        using var ui = TestUiContext.Install(); // continuations run between the frames this test drives
        var box = Box();
        var view = Deferred(box);
        view.ContentLoadedAnimation = SkUiViewAnimation.FadeIn(100);
        var (surface, pane) = Page(view, shown: false);
        using var page = surface;
        Settle(surface);
        pane.IsVisible = true;
        Assert.True(view.IsContentLoaded);
        Assert.Equal(0, box.Opacity); // the fade starts transparent
        for (var frame = 0; frame < 40 && box.Opacity < 1; frame++)
        {
            surface.Frame(100 + frame * 50);
            ui.RunPending();
        }
        Assert.Equal(1, box.Opacity);
        Assert.True(view.ContentLoadedAnimation.IsFrozen);
    }

    [Fact]
    public void ScrollViewsDeferTheirContentToo()
    {
        var box = Box(400);
        var scroller = new SkUiScrollView { ContentLoading = SkUiContentLoading.WhenShown, Content = box, HeightRequest = 80 };
        var pane = new SkUiVerticalStackLayout { IsVisible = false, Children = { scroller } };
        var surface = new SkUiTestSurface(new SkUiContentView { Content = pane }, 100, 100);
        using var page = surface;
        Settle(surface);
        Assert.Null(box.Parent);
        pane.IsVisible = true;
        Settle(surface);
        Assert.Same(scroller, box.Parent);
    }

    [Fact]
    public void AWaitingViewMovedIntoAShownTreeLoads()
    {
        var view = Deferred(Box());
        var waitingRoom = new SkUiVerticalStackLayout { Children = { view } };
        var shownPane = new SkUiVerticalStackLayout();
        using var surface = new SkUiTestSurface(new SkUiContentView { Content = shownPane }, 100, 100);
        waitingRoom.Children.Remove(view);
        Assert.False(view.IsContentLoaded);
        shownPane.Children.Add(view);
        Assert.True(view.IsContentLoaded);
    }

    [Fact]
    public void IsShownFollowsVisibilityParentsAndTheSurface()
    {
        var view = new SkUiBox();
        var changes = new List<bool>();
        view.IsShownChanged += (_, _) => changes.Add(view.IsShown);
        var pane = new SkUiVerticalStackLayout { Children = { view } };
        var root = new SkUiContentView { Content = pane };
        Assert.False(view.IsShown);

        var surface = new SkUiTestSurface(root, 100, 100);
        pane.IsVisible = false;
        pane.IsVisible = true;
        view.IsVisible = false;
        view.IsVisible = true;
        pane.Children.Remove(view);
        pane.Children.Add(view);
        surface.Dispose();
        Assert.Equal([true, false, true, false, true, false, true, false], changes);
    }

    [Fact]
    public void IsShownIsComputedWithoutHandlersAndWatchersAreCountedUpTheTree()
    {
        var view = new SkUiBox();
        var pane = new SkUiVerticalStackLayout { Children = { view } };
        var root = new SkUiContentView { Content = pane };
        using var surface = new SkUiTestSurface(root, 100, 100);
        Assert.True(view.IsShown); // no handler: computed on demand
        pane.IsVisible = false;
        Assert.False(view.IsShown);
        Assert.False(SkUiShownTracker.HasState(view) || SkUiShownTracker.HasState(pane) || SkUiShownTracker.HasState(root)); // no handlers: no state

        var detached = new SkUiVerticalStackLayout();
        var watched = new SkUiBox();
        detached.Children.Add(watched);
        EventHandler handler = (_, _) => { };
        watched.IsShownChanged += handler;
        Assert.Equal(1, SkUiShownTracker.Watchers(detached));
        root.Content = detached; // the watcher moves with its subtree
        Assert.Equal((0, 1, 1), (SkUiShownTracker.Watchers(pane), SkUiShownTracker.Watchers(detached), SkUiShownTracker.Watchers(root)));
        Assert.True(watched.IsShown);
        watched.IsShownChanged -= handler;
        Assert.False(SkUiShownTracker.HasState(watched) || SkUiShownTracker.HasState(detached) || SkUiShownTracker.HasState(root)); // state goes with the handlers
    }


    private const string Xaml = """
        <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                     xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
          <sk:SkUiContentView ContentLoading="WhenShown" ContentLoadingDelay="0:0:0.2"
                              ContentLoadedAnimation="FadeIn 200" HeightRequest="120">
            <sk:SkUiContentView.ContentTemplate>
              <DataTemplate>
                <sk:SkUiLabel Text="{Binding}" />
              </DataTemplate>
            </sk:SkUiContentView.ContentTemplate>
          </sk:SkUiContentView>
        </ContentView>
        """;

    [Fact]
    public void DeferredContentLoadsFromXaml()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var root = new ContentView { BindingContext = "Hello" };
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, Xaml);
        var view = (SkUiContentView)root.Content;
        Assert.Equal((SkUiContentLoading.WhenShown, TimeSpan.FromMilliseconds(200)), (view.ContentLoading, view.ContentLoadingDelay));
        Assert.Equal(200u, view.ContentLoadedAnimation!.Length);
        Assert.False(view.IsContentLoaded);
        Assert.Null(view.Content);
        view.LoadContent();
        Assert.Equal("Hello", Assert.IsType<SkUiLabel>(view.Content).Text);
    }

    private sealed class KindSelector : DataTemplateSelector
    {
        protected override DataTemplate OnSelectTemplate(object item, BindableObject container) =>
            item is 2 ? new DataTemplate(() => new SkUiEllipse()) : new DataTemplate(() => new SkUiBox());
    }
}
