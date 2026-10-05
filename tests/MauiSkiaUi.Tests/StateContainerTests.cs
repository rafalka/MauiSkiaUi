using System.ComponentModel;
using System.Runtime.CompilerServices;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary><see cref="SkUiStateContainer"/> on drawn layouts, with the Community Toolkit's <c>StateContainer</c> rules.</summary>
[Collection(RuntimeXamlCollection.Name)]
public class StateContainerTests
{
    private static SkUiLabel State(string key, string? text = null)
    {
        var view = new SkUiLabel { Text = text ?? key };
        SkUiStateView.SetStateKey(view, key);
        return view;
    }

    private static (SkUiVerticalStackLayout Layout, SkUiBox First, SkUiBox Second, SkUiLabel Loading, SkUiLabel Error) Container()
    {
        SkUiBox first = new() { HeightRequest = 20 }, second = new() { HeightRequest = 20 };
        var layout = new SkUiVerticalStackLayout { Children = { first, second } };
        SkUiLabel loading = State("Loading"), error = State("Error");
        SkUiStateContainer.GetStateViews(layout).Add(loading);
        SkUiStateContainer.GetStateViews(layout).Add(error);
        return (layout, first, second, loading, error);
    }

    [Fact]
    public void StateKeyDefaultsToEmpty()
    {
        var view = new SkUiBox();
        Assert.Equal(string.Empty, SkUiStateView.GetStateKey(view));
        SkUiStateView.SetStateKey(view, "Loading");
        Assert.Equal("Loading", SkUiStateView.GetStateKey(view));
    }

    [Fact]
    public void AStateReplacesTheChildrenUntilItIsCleared()
    {
        var (layout, first, second, loading, error) = Container();
        Assert.True(SkUiStateContainer.GetCanStateChange(layout));

        SkUiStateContainer.SetCurrentState(layout, "Loading");
        Assert.Same(loading, Assert.Single(layout.Children));
        Assert.Same(layout, loading.Parent);
        Assert.Null(first.Parent);

        SkUiStateContainer.SetCurrentState(layout, "Error");
        Assert.Same(error, Assert.Single(layout.Children));
        Assert.Null(loading.Parent);

        SkUiStateContainer.SetCurrentState(layout, null);
        Assert.Equal([first, second], layout.Children);
        Assert.Same(layout, first.Parent);
        Assert.Null(error.Parent);

        SkUiStateContainer.SetCurrentState(layout, "Loading");
        SkUiStateContainer.SetCurrentState(layout, string.Empty); // empty also means the content
        Assert.Equal([first, second], layout.Children);
    }

    [Fact]
    public void AnEmptyStateBeforeAnyStateKeepsTheChildren()
    {
        var (layout, first, second, _, _) = Container();
        SkUiStateContainer.SetCurrentState(layout, string.Empty);
        Assert.Equal([first, second], layout.Children);
    }

    [Fact]
    public void OnAGridTheStateViewSpansEveryCell()
    {
        var grid = new SkUiGrid
        {
            RowDefinitions = [new RowDefinition(30), new RowDefinition(30)],
            ColumnDefinitions = [new ColumnDefinition(40), new ColumnDefinition(40), new ColumnDefinition(40)]
        };
        var cell = new SkUiBox();
        Grid.SetRow(cell, 1);
        Grid.SetColumn(cell, 2);
        grid.Children.Add(cell);
        var state = new SkUiBox();
        SkUiStateView.SetStateKey(state, "Replace");
        SkUiStateContainer.GetStateViews(grid).Add(state);

        SkUiStateContainer.SetCurrentState(grid, "Replace");
        SkUiTestHelpers.Arrange(grid, 120, 60);
        Assert.Equal(new Rect(0, 0, 120, 60), state.Frame);
    }

    [Fact]
    public void InvalidStatesThrowAndLeaveTheLayoutAlone()
    {
        var (layout, first, second, loading, _) = Container();
        var missing = Assert.Throws<SkUiStateContainerException>(() => SkUiStateContainer.SetCurrentState(layout, "Unknown"));
        Assert.Equal("SkUiStateView for Unknown not defined.", missing.Message);
        Assert.Null(SkUiStateContainer.GetCurrentState(layout));
        Assert.Equal([first, second], layout.Children);

        SkUiStateContainer.GetStateViews(layout).Add(State("Error", "again"));
        var duplicate = Assert.Throws<SkUiStateContainerException>(() => SkUiStateContainer.SetCurrentState(layout, "Error"));
        Assert.Contains("unique StateKey", duplicate.Message);

        var native = new Label();
        SkUiStateView.SetStateKey(native, "Native");
        SkUiStateContainer.GetStateViews(layout).Add(native);
        var notDrawn = Assert.Throws<SkUiStateContainerException>(() => SkUiStateContainer.SetCurrentState(layout, "Native"));
        Assert.Contains("SkUiMauiContentView", notDrawn.Message);
        Assert.Equal([first, second], layout.Children);

        // Rejected values are refused before MAUI starts setting them, so later values still apply.
        SkUiStateContainer.SetCurrentState(layout, "Loading");
        Assert.Same(loading, Assert.Single(layout.Children));
    }

    [Fact]
    public void OnlyDrawnLayoutsCanHoldStates()
    {
        var error = Assert.Throws<SkUiStateContainerException>(() => SkUiStateContainer.SetCurrentState(new SkUiContentView(), "Loading"));
        Assert.Contains(nameof(SkUiLayout), error.Message);
    }

    [Fact]
    public void StateViewsInheritTheLayoutsBindingContextWhileShown()
    {
        var (layout, _, _, loading, _) = Container();
        layout.BindingContext = "model";
        SkUiStateContainer.SetCurrentState(layout, "Loading");
        Assert.Equal("model", loading.BindingContext);
    }

    [Fact]
    public void CanStateChangeBindsOneWayToSource()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var model = new Model { CanChange = false };
        var (layout, _, _, _, _) = Container();
        layout.BindingContext = model;
        layout.SetBinding(SkUiStateContainer.CanStateChangeProperty, new Binding(nameof(Model.CanChange)));
        Assert.True(model.CanChange);
        Assert.True(SkUiStateContainer.GetCanStateChange(layout));
    }

    [Fact]
    public async Task OffScreenAnAnimatedChangeAppliesAtOnce()
    {
        var (layout, _, _, loading, _) = Container();
        var change = SkUiStateContainer.ChangeStateWithAnimation(layout, "Loading");
        Assert.True(change.IsCompletedSuccessfully);
        await change;
        Assert.Same(loading, Assert.Single(layout.Children));
        Assert.Equal("Loading", SkUiStateContainer.GetCurrentState(layout));
    }

    [Fact]
    public async Task TheDefaultAnimationFadesOutAndInAndRestoresOpacity()
    {
        var (layout, first, second, loading, _) = Container();
        second.Opacity = 0.5;
        loading.Opacity = 0.8;
        var root = new SkUiContentView { Content = layout };
        using var surface = new SkUiTestSurface(root, 100, 100);
        surface.Frame(0);

        var change = SkUiStateContainer.ChangeStateWithAnimation(layout, "Loading");
        Assert.False(change.IsCompleted);
        Assert.False(SkUiStateContainer.GetCanStateChange(layout));
        Assert.Null(SkUiStateContainer.GetCurrentState(layout));
        Assert.Throws<SkUiStateContainerException>(() => SkUiStateContainer.SetCurrentState(layout, "Error"));
        await Assert.ThrowsAsync<SkUiStateContainerException>(() => SkUiStateContainer.ChangeStateWithAnimation(layout, "Error"));

        await Drive(surface, change);
        await change;
        Assert.True(SkUiStateContainer.GetCanStateChange(layout));
        Assert.Equal("Loading", SkUiStateContainer.GetCurrentState(layout));
        Assert.Same(loading, Assert.Single(layout.Children));
        Assert.Equal(0.8, loading.Opacity, 3); // faded in to its own opacity
        Assert.Equal(1, first.Opacity); // the views that left get theirs back
        Assert.Equal(0.5, second.Opacity);

        var back = SkUiStateContainer.ChangeStateWithAnimation(layout, null);
        await Drive(surface, back);
        await back;
        Assert.Equal([first, second], layout.Children);
        Assert.Equal(1, first.Opacity, 3);
        Assert.Equal(0.5, second.Opacity, 3);
        Assert.Equal(0.8, loading.Opacity);
    }

    [Fact]
    public async Task ViewAnimationsRunOnTheLeavingAndTheNewViewsAndEndExactly()
    {
        var (layout, first, second, loading, _) = Container();
        first.TranslationX = 5;
        var root = new SkUiContentView { Content = layout };
        using var surface = new SkUiTestSurface(root, 100, 100);
        surface.Frame(0);
        var shrink = new SkUiViewAnimation([new(SkUiAnimatableProperty.Scale, to: 0.8), new(SkUiAnimatableProperty.Opacity, to: 0)], 100, Easing.CubicIn);
        var slideIn = new SkUiViewAnimation([new(SkUiAnimatableProperty.TranslationY, from: 24), new(SkUiAnimatableProperty.Opacity, from: 0)], 100, Easing.CubicOut);

        var change = SkUiStateContainer.ChangeStateWithAnimation(layout, "Loading", shrink, slideIn);
        surface.Frame(10);
        await Drive(surface, change);
        await change;
        Assert.Same(loading, Assert.Single(layout.Children));
        Assert.Equal((0d, 1d, 1d), (loading.TranslationY, loading.Opacity, loading.Scale)); // ends at its own values, exactly
        Assert.Equal((1d, 1d, 5d), (first.Scale, first.Opacity, first.TranslationX)); // the leaving views get theirs back
        Assert.Equal(1, second.Scale);
    }

    [Fact]
    public async Task AViewAnimationOffScreenJumpsToItsEndValues()
    {
        var box = new SkUiBox { Opacity = 0.5 };
        var animation = new SkUiViewAnimation([new(SkUiAnimatableProperty.Rotation, to: 90), new(SkUiAnimatableProperty.Opacity, from: 0)]);
        Assert.True(await animation.RunAsync(box));
        Assert.Equal((90d, 0.5), (box.Rotation, box.Opacity));
    }

    [Fact]
    public async Task AViewAnimationOnScreenEndsAtItsExactValues()
    {
        var box = new SkUiBox { WidthRequest = 20, HeightRequest = 20 };
        using var surface = new SkUiTestSurface(new SkUiContentView { Content = box }, 40, 40);
        surface.Frame(0);
        var run = new SkUiViewAnimation([new(SkUiAnimatableProperty.Opacity, to: 0.3), new(SkUiAnimatableProperty.TranslationX, 10, 20)], 100).RunAsync(box);
        Assert.Equal(10, box.TranslationX); // From applies at the start
        await Drive(surface, run);
        Assert.True(await run);
        Assert.Equal((0.3, 20d), (box.Opacity, box.TranslationX));
    }

    [Fact]
    public async Task FunctionAnimationsReceiveTheLayout()
    {
        var (layout, _, _, loading, _) = Container();
        var calls = new List<string>();
        Task Before(VisualElement element, CancellationToken token)
        {
            calls.Add($"before {element.GetType().Name} {SkUiStateContainer.GetCanStateChange(layout)}");
            return Task.CompletedTask;
        }
        Task After(VisualElement element, CancellationToken token)
        {
            calls.Add($"after {((SkUiLayout)element).Children.Count}");
            return Task.CompletedTask;
        }

        await SkUiStateContainer.ChangeStateWithAnimation(layout, "Loading", Before, After);
        Assert.Equal(["before SkUiVerticalStackLayout False", "after 1"], calls);
        Assert.Same(loading, Assert.Single(layout.Children));
        Assert.True(SkUiStateContainer.GetCanStateChange(layout));
        Assert.Equal("Loading", SkUiStateContainer.GetCurrentState(layout));
    }

    [Fact]
    public async Task AnimatedChangesValidateTheirArguments()
    {
        var (layout, first, second, _, _) = Container();
        await Assert.ThrowsAsync<ArgumentException>(() => SkUiStateContainer.ChangeStateWithAnimation(layout, "Loading", (SkUiViewAnimation?)null, null));
        await Assert.ThrowsAsync<ArgumentException>(() => SkUiStateContainer.ChangeStateWithAnimation(layout, "Loading", null, (Func<VisualElement, CancellationToken, Task>?)null));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SkUiStateContainer.ChangeStateWithAnimation(layout, "Loading", cancelled.Token));
        Assert.True(SkUiStateContainer.GetCanStateChange(layout));
        Assert.Equal([first, second], layout.Children);
    }

    [Fact]
    public async Task ACancelledChangeStillEndsInTheNewState()
    {
        var (layout, first, _, loading, _) = Container();
        using var cancellation = new CancellationTokenSource();
        var change = SkUiStateContainer.ChangeStateWithAnimation(layout, "Loading",
            async (_, token) => await Task.Delay(Timeout.Infinite, token), null, cancellation.Token);
        Assert.False(SkUiStateContainer.GetCanStateChange(layout));
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => change);
        Assert.True(SkUiStateContainer.GetCanStateChange(layout));
        Assert.Equal("Loading", SkUiStateContainer.GetCurrentState(layout));
        Assert.Same(loading, Assert.Single(layout.Children));
        Assert.Equal(1, first.Opacity);
    }

    private const string ToolkitXaml = """
        <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                     xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
          <sk:SkUiVerticalStackLayout Padding="24,0"
                                      sk:SkUiStateContainer.CurrentState="{Binding CurrentState}"
                                      sk:SkUiStateContainer.CanStateChange="{Binding CanChange}">
            <sk:SkUiStateContainer.StateViews>
              <sk:SkUiBorder sk:SkUiStateView.StateKey="Loading" Stroke="CornflowerBlue">
                <sk:SkUiHorizontalStackLayout Spacing="10">
                  <sk:SkUiActivityIndicator HeightRequest="20" IsRunning="True" WidthRequest="20" Color="CornflowerBlue" />
                  <sk:SkUiLabel Text="Pretending to load things..." VerticalTextAlignment="Center" />
                </sk:SkUiHorizontalStackLayout>
              </sk:SkUiBorder>
              <sk:SkUiLabel Padding="20" sk:SkUiStateView.StateKey="StateKey can be anything!" Text="Hi, I'm a simple Label state!" />
            </sk:SkUiStateContainer.StateViews>
            <sk:SkUiBorder>
              <sk:SkUiLabel Text="This is the default content." VerticalTextAlignment="Center" />
            </sk:SkUiBorder>
          </sk:SkUiVerticalStackLayout>
        </ContentView>
        """;

    [Fact]
    public void ToolkitMarkupWorksWithOnlyThePrefixChanged()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var model = new Model();
        var root = new ContentView { BindingContext = model };
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, ToolkitXaml);
        var layout = (SkUiVerticalStackLayout)root.Content;
        Assert.IsType<SkUiBorder>(Assert.Single(layout.Children));
        Assert.True(model.CanChange);

        model.CurrentState = "Loading";
        Assert.Equal("Loading", SkUiStateView.GetStateKey((BindableObject)Assert.Single(layout.Children)));
        model.CurrentState = "StateKey can be anything!";
        Assert.IsType<SkUiLabel>(Assert.Single(layout.Children));
        model.CurrentState = null;
        Assert.IsType<SkUiBorder>(Assert.Single(layout.Children));
    }

    // Frames until the change completes: each frame advances the fades; its continuation runs between frames.
    private static Task Drive(SkUiTestSurface surface, Task change) => Drive(surface, () => change.IsCompleted);

    private static async Task Drive(SkUiTestSurface surface, Func<bool> done)
    {
        for (var frame = 1; frame <= 200 && !done(); frame++)
        {
            surface.Frame(_time += 50);
            await Task.Delay(5);
        }
        Assert.True(done(), "The animated state change did not finish.");
    }

    private static double _time;

    private static (SkUiVerticalStackLayout Layout, SkUiBox First, SkUiBox Second, SkUiLabel Loading, SkUiLabel Error, SkUiTestSurface Surface) Animated()
    {
        var (layout, first, second, loading, error) = Container();
        SkUiStateContainer.SetBeforeStateChangeAnimation(layout, SkUiViewAnimation.FadeOut(100));
        SkUiStateContainer.SetAfterStateChangeAnimation(layout, SkUiViewAnimation.FadeIn(100));
        var surface = new SkUiTestSurface(new SkUiContentView { Content = layout }, 100, 100);
        surface.Frame(_time += 16);
        return (layout, first, second, loading, error, surface);
    }

    [Fact]
    public async Task WithStateChangeAnimationsEveryChangeAnimatesAndTheNewestStateWins()
    {
        var (layout, first, second, loading, error, surface) = Animated();
        using var _ = surface;
        var loadingParents = 0;
        loading.ParentChanged += (_, _) => loadingParents++;

        SkUiStateContainer.SetCurrentState(layout, "Loading");
        Assert.Equal("Loading", SkUiStateContainer.GetCurrentState(layout)); // the value changes at once
        Assert.False(SkUiStateContainer.GetCanStateChange(layout));
        Assert.Equal([first, second], layout.Children); // the views follow
        SkUiStateContainer.SetCurrentState(layout, "Error"); // no exception while it animates

        await Drive(surface, () => SkUiStateContainer.GetCanStateChange(layout));
        Assert.Same(error, Assert.Single(layout.Children));
        Assert.Equal(0, loadingParents); // replaced before it was shown
        Assert.Equal((1d, 1d, 1d), (error.Opacity, first.Opacity, second.Opacity));
    }

    [Fact]
    public async Task AStateSetWhileTheNewViewsAnimateInFollows()
    {
        var (layout, first, second, _, error, surface) = Animated();
        using var _ = surface;
        SkUiStateContainer.SetCurrentState(layout, "Error");
        await Drive(surface, () => layout.Children.Contains(error));
        SkUiStateContainer.SetCurrentState(layout, null);
        await Drive(surface, () => SkUiStateContainer.GetCanStateChange(layout));
        Assert.Equal([first, second], layout.Children);
        Assert.Equal((1d, 1d, 1d), (error.Opacity, first.Opacity, second.Opacity));
        Assert.Null(SkUiStateContainer.GetCurrentState(layout));
    }

    [Fact]
    public async Task ExplicitChangesWaitForAutomaticOnes()
    {
        var (layout, _, _, _, _, surface) = Animated();
        using var _ = surface;
        SkUiStateContainer.SetCurrentState(layout, "Loading");
        await Assert.ThrowsAsync<SkUiStateContainerException>(() => SkUiStateContainer.ChangeStateWithAnimation(layout, "Error"));
        await Drive(surface, () => SkUiStateContainer.GetCanStateChange(layout));
        var change = SkUiStateContainer.ChangeStateWithAnimation(layout, "Error");
        await Drive(surface, change);
        await change;
        Assert.Equal("Error", SkUiStateContainer.GetCurrentState(layout));
    }

    [Fact]
    public void OffScreenAutomaticChangesApplyAtOnce()
    {
        var (layout, _, _, loading, _) = Container();
        SkUiStateContainer.SetBeforeStateChangeAnimation(layout, SkUiViewAnimation.FadeOut());
        SkUiStateContainer.SetCurrentState(layout, "Loading");
        Assert.Same(loading, Assert.Single(layout.Children));
        Assert.True(SkUiStateContainer.GetCanStateChange(layout));
    }

    [Fact]
    public async Task ABoundStateChangingQuicklyNeverThrows()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var (layout, first, second, _, _, surface) = Animated();
        using var _ = surface;
        var model = new Model();
        layout.BindingContext = model;
        layout.SetBinding(SkUiStateContainer.CurrentStateProperty, new Binding(nameof(Model.CurrentState)));
        foreach (var state in new[] { "Loading", "Error", "Loading", null })
            model.CurrentState = state;
        await Drive(surface, () => SkUiStateContainer.GetCanStateChange(layout));
        Assert.Equal([first, second], layout.Children);
    }

    [Fact]
    public void AnimationsParseFromTextAndFreezeWhenUsed()
    {
        var fade = SkUiViewAnimation.Parse("FadeOut 150 CubicIn");
        Assert.Equal((150u, Easing.CubicIn), (fade.Length, fade.Easing));
        Assert.Equal(0, Assert.Single(fade.Properties).To);
        Assert.Equal(250u, SkUiViewAnimation.Parse("fadein").Length);
        Assert.Throws<FormatException>(() => SkUiViewAnimation.Parse("Spin 100"));

        var shared = new SkUiViewAnimation { Length = 100, Properties = { new SkUiPropertyAnimation { Property = SkUiAnimatableProperty.Scale, From = 0.8 } } };
        SkUiStateContainer.SetAfterStateChangeAnimation(new SkUiGrid(), shared);
        SkUiStateContainer.SetAfterStateChangeAnimation(new SkUiGrid(), shared); // one instance, many layouts
        Assert.True(shared.IsFrozen);
        Assert.Throws<InvalidOperationException>(() => shared.Length = 200);
        Assert.Throws<InvalidOperationException>(() => shared.Properties.Clear());
        Assert.Throws<InvalidOperationException>(() => shared.Properties[0].To = 1);
    }

    private const string AnimationXaml = """
        <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                     xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
          <sk:SkUiGrid sk:SkUiStateContainer.CurrentState="{Binding CurrentState}"
                       sk:SkUiStateContainer.BeforeStateChangeAnimation="FadeOut 150 CubicIn">
            <sk:SkUiStateContainer.AfterStateChangeAnimation>
              <sk:SkUiViewAnimation Length="300" Easing="CubicOut">
                <sk:SkUiPropertyAnimation Property="TranslationY" From="24" />
                <sk:SkUiPropertyAnimation Property="Opacity" From="0" />
              </sk:SkUiViewAnimation>
            </sk:SkUiStateContainer.AfterStateChangeAnimation>
            <sk:SkUiStateContainer.StateViews>
              <sk:SkUiLabel sk:SkUiStateView.StateKey="Loading" Text="Loading…" />
            </sk:SkUiStateContainer.StateViews>
            <sk:SkUiLabel Text="Content" />
          </sk:SkUiGrid>
        </ContentView>
        """;

    [Fact]
    public void StateChangeAnimationsLoadFromXaml()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var model = new Model();
        var root = new ContentView { BindingContext = model };
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, AnimationXaml);
        var grid = (SkUiGrid)root.Content;
        var before = SkUiStateContainer.GetBeforeStateChangeAnimation(grid)!;
        var after = SkUiStateContainer.GetAfterStateChangeAnimation(grid)!;
        Assert.Equal((150u, Easing.CubicIn), (before.Length, before.Easing));
        Assert.Equal((300u, Easing.CubicOut), (after.Length, after.Easing));
        Assert.Equal([(SkUiAnimatableProperty.TranslationY, (double?)24), (SkUiAnimatableProperty.Opacity, 0)], after.Properties.Select(property => (property.Property, property.From)));
        model.CurrentState = "Loading"; // off screen: at once
        Assert.Equal("Loading", SkUiStateView.GetStateKey((BindableObject)Assert.Single(grid.Children)));
    }

    private sealed class Model : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public string? CurrentState { get => field; set { field = value; Raise(); } }

        public bool CanChange { get => field; set { field = value; Raise(); } }

        private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
