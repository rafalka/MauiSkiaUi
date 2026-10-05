using System.ComponentModel;
using System.Runtime.CompilerServices;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary><see cref="SkUiAlternateContentView"/>: content or alternate content (or nothing), templates run on first show, animated switches.</summary>
[Collection(RuntimeXamlCollection.Name)]
public class AlternateContentViewTests
{
    private static double _time;

    private static Task Drive(SkUiTestSurface surface, Func<bool> done)
    {
        var ui = SynchronizationContext.Current as TestUiContext ?? throw new InvalidOperationException("Install a TestUiContext first.");
        for (var frame = 1; frame <= 200 && !done(); frame++)
        {
            surface.Frame(_time += 50);
            ui.RunPending(TimeSpan.FromMilliseconds(5));
        }
        Assert.True(done(), "The switch did not finish.");
        return Task.CompletedTask;
    }

    [Fact]
    public void ShowsNothingUntilShowsAlternateIsSet()
    {
        SkUiBox content = new(), alternate = new();
        var view = new SkUiAlternateContentView { Content = content, AlternateContent = alternate };
        Assert.Null(view.ShowsAlternate);
        Assert.Null(content.Parent);
        Assert.Null(alternate.Parent);
        Assert.Equal(Size.Zero, ((IView)view).Measure(100, 100));
    }

    [Fact]
    public void ShowsAlternatePicksTheSide()
    {
        SkUiBox content = new() { HeightRequest = 10 }, alternate = new() { HeightRequest = 20 };
        var view = new SkUiAlternateContentView { Content = content, AlternateContent = alternate, ShowsAlternate = false };
        Assert.Same(view, content.Parent);
        Assert.Null(alternate.Parent);

        view.ShowsAlternate = true;
        Assert.Null(content.Parent); // detached, kept
        Assert.Same(view, alternate.Parent);
        Assert.Same(content, view.Content); // the properties never change
        Assert.Equal(20, ((IView)view).Measure(100, 100).Height);

        view.ShowsAlternate = null;
        Assert.Null(alternate.Parent);
        view.ShowsAlternate = false;
        Assert.Same(view, content.Parent);
    }

    [Fact]
    public void ReplacingTheShownSideAttachesTheNewView()
    {
        var view = new SkUiAlternateContentView { AlternateContent = new SkUiBox(), ShowsAlternate = true };
        var replacement = new SkUiEllipse();
        view.AlternateContent = replacement;
        Assert.Same(view, replacement.Parent);
        var hidden = new SkUiLabel();
        view.Content = hidden; // the hidden side is not attached
        Assert.Null(hidden.Parent);
    }

    [Fact]
    public void EachTemplateRunsWhenItsSideIsFirstShown()
    {
        int contents = 0, alternates = 0;
        var view = new SkUiAlternateContentView
        {
            ContentTemplate = new DataTemplate(() => { contents++; return new SkUiBox(); }),
            AlternateContentTemplate = new DataTemplate(() => { alternates++; return new SkUiEllipse(); })
        };
        _ = new SkUiVerticalStackLayout { Children = { view } };
        Assert.Equal((0, 0), (contents, alternates)); // null: nothing created

        view.ShowsAlternate = true;
        Assert.Equal((0, 1), (contents, alternates));
        Assert.IsType<SkUiEllipse>(view.AlternateContent);
        view.ShowsAlternate = false;
        view.ShowsAlternate = true; // the created content is kept
        Assert.Equal((1, 1), (contents, alternates));
    }

    [Fact]
    public void AlternateTemplateSelectorsChooseAgainWhenTheContextChanges()
    {
        var view = new SkUiAlternateContentView { AlternateContentTemplate = new EvenSelector(), ShowsAlternate = true, BindingContext = 1 };
        _ = new SkUiVerticalStackLayout { Children = { view } };
        Assert.IsType<SkUiBox>(view.AlternateContent);
        view.BindingContext = 2;
        Assert.IsType<SkUiEllipse>(view.AlternateContent);
        Assert.Same(view, ((Element)view.AlternateContent!).Parent);
        Assert.Equal(2, ((BindableObject)view.AlternateContent).BindingContext);
    }

    [Fact]
    public void DeferredLoadingShowsTheChosenSideOnceShown()
    {
        var alternate = new SkUiBox();
        var view = new SkUiAlternateContentView
        {
            ContentLoading = SkUiContentLoading.WhenShown, AlternateContent = alternate, ShowsAlternate = true
        };
        var pane = new SkUiVerticalStackLayout { IsVisible = false, Children = { view } };
        using var surface = new SkUiTestSurface(new SkUiContentView { Content = pane }, 100, 100);
        Assert.Null(alternate.Parent);
        pane.IsVisible = true;
        Assert.Same(view, alternate.Parent);
    }

    [Fact]
    public async Task AnimatedSwitchesFollowTheNewestValue()
    {
        using var ui = TestUiContext.Install(); // continuations run between the frames this test drives
        SkUiBox content = new() { HeightRequest = 20 }, alternate = new() { HeightRequest = 20, Opacity = 0.7 };
        var view = new SkUiAlternateContentView
        {
            Content = content, AlternateContent = alternate, ShowsAlternate = false,
            BeforeStateChangeAnimation = SkUiViewAnimation.FadeOut(100), AfterStateChangeAnimation = SkUiViewAnimation.FadeIn(100)
        };
        using var surface = new SkUiTestSurface(new SkUiContentView { Content = view }, 100, 100);
        surface.Frame(_time += 16);
        var alternateParents = 0;
        alternate.ParentChanged += (_, _) => alternateParents++;

        view.ShowsAlternate = true;
        Assert.True(view.IsSwitching);
        Assert.Same(view, content.Parent); // fading out
        view.ShowsAlternate = null; // replaces the pending switch
        await Drive(surface, () => !view.IsSwitching);
        Assert.Null(content.Parent);
        Assert.Equal(0, alternateParents); // never shown
        Assert.Equal(1, content.Opacity); // the view that left got its opacity back

        view.ShowsAlternate = true;
        await Drive(surface, () => !view.IsSwitching);
        Assert.Same(view, alternate.Parent);
        Assert.Equal(0.7, alternate.Opacity); // faded in to its own opacity, exactly
    }

    [Fact]
    public void OffScreenSwitchesApplyAtOnce()
    {
        var alternate = new SkUiBox();
        var view = new SkUiAlternateContentView { AlternateContent = alternate, BeforeStateChangeAnimation = SkUiViewAnimation.FadeOut() };
        view.ShowsAlternate = true;
        Assert.False(view.IsSwitching);
        Assert.Same(view, alternate.Parent);
    }

    private const string Xaml = """
        <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                     xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
          <sk:SkUiAlternateContentView ShowsAlternate="{Binding IsEditing}"
                                       BeforeStateChangeAnimation="FadeOut 120" AfterStateChangeAnimation="FadeIn 200 CubicOut">
            <sk:SkUiLabel Text="{Binding Name}" />
            <sk:SkUiAlternateContentView.AlternateContentTemplate>
              <DataTemplate>
                <sk:SkUiBorder><sk:SkUiLabel Text="{Binding Name, StringFormat='Editing {0}'}" /></sk:SkUiBorder>
              </DataTemplate>
            </sk:SkUiAlternateContentView.AlternateContentTemplate>
          </sk:SkUiAlternateContentView>
        </ContentView>
        """;

    [Fact]
    public void AlternateContentLoadsFromXamlAndBinds()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var model = new Model { Name = "Ada", IsEditing = false };
        var root = new ContentView { BindingContext = model };
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, Xaml);
        var view = (SkUiAlternateContentView)root.Content;
        Assert.Equal("Ada", ((SkUiLabel)view.Content!).Text);
        Assert.Null(view.AlternateContent); // its template has not run

        model.IsEditing = true; // off screen: at once
        var border = Assert.IsType<SkUiBorder>(view.AlternateContent);
        Assert.Equal("Editing Ada", ((SkUiLabel)border.Content!).Text);
        Assert.Equal((120u, 200u), (view.BeforeStateChangeAnimation!.Length, view.AfterStateChangeAnimation!.Length));

        model.IsEditing = null;
        Assert.Null(((Element)border).Parent);
    }

    private sealed class Model : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public string? Name { get => field; set { field = value; Raise(); } }
        public bool? IsEditing { get => field; set { field = value; Raise(); } }
        private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private sealed class EvenSelector : DataTemplateSelector
    {
        private readonly DataTemplate _box = new(() => new SkUiBox());
        private readonly DataTemplate _ellipse = new(() => new SkUiEllipse());
        protected override DataTemplate OnSelectTemplate(object item, BindableObject container) => item is int number && number % 2 == 0 ? _ellipse : _box;
    }
}
