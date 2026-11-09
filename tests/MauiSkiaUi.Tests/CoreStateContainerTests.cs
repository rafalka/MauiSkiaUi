using System.ComponentModel;
using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary><see cref="SkUiCoreStateContainer"/>: the rules of <see cref="SkUiStateContainer"/> on the Core layer.</summary>
public class CoreStateContainerTests
{
    private const string Loading = "Loading";
    private const string Error = "Error";
    private static double _time;

    private static (SkUiCoreStateContainer Container, SkUiCoreBox Content, SkUiCoreBox LoadingNode, SkUiCoreBox ErrorNode) Container()
    {
        var content = new SkUiCoreBox { Color = Colors.Green };
        var loading = new SkUiCoreBox { Color = Colors.Blue };
        var error = new SkUiCoreBox { Color = Colors.Red };
        var container = new SkUiCoreStateContainer { Content = content, States = { { Loading, loading }, { Error, error } } };
        return (container, content, loading, error);
    }

    [Fact]
    public void AStateReplacesTheContentUntilItIsCleared()
    {
        var (container, content, loading, error) = Container();
        Assert.True(container.CanStateChange);
        Assert.Same(content, container.ShownChild);
        Assert.Same(container, content.Parent);
        Assert.Null(loading.Parent);

        container.SetCurrentState(Loading);
        Assert.Same(loading, container.ShownChild);
        Assert.Same(container, loading.Parent);
        Assert.Null(content.Parent);
        Assert.Equal(Loading, container.ShownState);

        container.SetCurrentState(Error);
        Assert.Same(error, container.ShownChild);
        Assert.Null(loading.Parent);

        container.SetCurrentState(null);
        Assert.Same(content, container.ShownChild);
        Assert.Null(error.Parent);

        container.SetCurrentState(Loading);
        container.SetCurrentState(string.Empty); // empty also means the content
        Assert.Null(container.CurrentState);
        Assert.Same(content, container.ShownChild);
    }

    [Fact]
    public void TheShownNodeDrawsAndTheOthersReleaseTheirResources()
    {
        var (container, content, loading, _) = Container();
        using var surface = new SkUiTestSurface(new SkUiCoreHost().SetContent(container), 100, 100);
        Assert.Equal(SKColors.Green, surface.Frame().GetPixel(50, 50));
        container.SetCurrentState(Loading);
        Assert.Equal(SKColors.Blue, surface.Frame().GetPixel(50, 50));
        Assert.False(content.RenderState.HasCommitted);
        container.SetCurrentState(null);
        Assert.Equal(SKColors.Green, surface.Frame().GetPixel(50, 50));
        Assert.False(loading.RenderState.HasCommitted);
    }

    [Fact]
    public void InvalidStatesAndNodesThrowAndLeaveTheContainerAlone()
    {
        var (container, content, loading, _) = Container();
        Assert.Throws<SkUiStateContainerException>(() => container.SetCurrentState("Missing"));
        Assert.Null(container.CurrentState);
        Assert.Same(content, container.ShownChild);

        Assert.Throws<ArgumentException>(() => container.States.Add(Loading, new SkUiCoreBox())); // a key twice
        Assert.Throws<ArgumentException>(() => container.States.Add(string.Empty, new SkUiCoreBox()));
        Assert.Throws<InvalidOperationException>(() => container.States.Add("Again", loading)); // a node in two states
        Assert.Throws<InvalidOperationException>(() => container.States.Add("Content", content));
        Assert.Throws<InvalidOperationException>(() => container.SetContent(loading));
        var parented = new SkUiCoreBox();
        new SkUiCoreContentView().SetContent(parented);
        Assert.Throws<InvalidOperationException>(() => container.States.Add("Parented", parented));
        var outer = new SkUiCoreContentView().SetContent(container);
        Assert.Throws<InvalidOperationException>(() => container.States.Add("Cycle", outer));
        Assert.Equal(2, container.States.Count);
    }

    [Fact]
    public void AFactoryBuildsItsNodeWhenTheStateIsFirstNamed()
    {
        var builds = 0;
        var container = new SkUiCoreStateContainer().AddState(Loading, () =>
        {
            builds++;
            return new SkUiCoreBox();
        });
        Assert.False(container.States.TryGetNode(Loading, out _));
        container.SetCurrentState(Loading);
        container.SetCurrentState(null);
        container.SetCurrentState(Loading);
        Assert.Equal(1, builds); // kept
        Assert.True(container.States.TryGetNode(Loading, out var node));
        Assert.Same(node, container.ShownChild);

        var failing = new SkUiCoreStateContainer().AddState(Error, () => throw new InvalidOperationException("no"));
        Assert.Throws<SkUiStateContainerException>(() => failing.SetCurrentState(Error));
        Assert.Null(failing.CurrentState);
    }

    [Fact]
    public void TheShownStateFollowsItsNodeWhenReplacedRemovedAndAddedAgain()
    {
        var (container, _, loading, _) = Container();
        container.SetCurrentState(Loading);
        var replacement = new SkUiCoreBox();
        container.States[Loading] = replacement;
        Assert.Same(replacement, container.ShownChild);
        Assert.Null(loading.Parent);

        Assert.True(container.States.Remove(Loading));
        Assert.Null(container.ShownChild); // nothing to show; the state is still named
        Assert.Null(replacement.Parent);
        Assert.Equal(Loading, container.CurrentState);

        var again = new SkUiCoreBox();
        container.States.Add(Loading, () => again);
        Assert.Same(again, container.ShownChild);

        container.States.Clear();
        Assert.Null(container.ShownChild);
        container.SetCurrentState(null);
        Assert.NotNull(container.ShownChild);
    }

    [Fact]
    public void ContentReplacedWhileAStateShowsIsShownWhenTheStateClears()
    {
        var (container, content, loading, _) = Container();
        container.SetCurrentState(Loading);
        var other = new SkUiCoreBox();
        container.SetContent(other);
        Assert.Same(loading, container.ShownChild);
        Assert.Null(other.Parent);
        container.SetCurrentState(null);
        Assert.Same(other, container.ShownChild);
        Assert.Null(content.Parent);
    }

    [Fact]
    public void PropertiesRaiseChanges()
    {
        var (container, _, _, _) = Container();
        var changed = new List<string?>();
        ((INotifyPropertyChanged)container).PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        container.SetCurrentState(Loading);
        Assert.Equal([nameof(SkUiCoreStateContainer.CurrentState), nameof(SkUiCoreStateContainer.ShownState)], changed);
    }

    [Fact]
    public async Task OffScreenChangesApplyAtOnce()
    {
        var (container, _, loading, _) = Container();
        var change = container.ChangeStateWithAnimation(Loading);
        Assert.True(change.IsCompletedSuccessfully);
        await change;
        Assert.Same(loading, container.ShownChild);
        Assert.Equal(Loading, container.CurrentState);

        container.SetBeforeStateChangeAnimation(SkUiViewAnimation.FadeOut());
        container.SetCurrentState(Error);
        Assert.Equal(Error, container.ShownState);
        Assert.True(container.CanStateChange);
    }

    [Fact]
    public async Task TheDefaultAnimationFadesOutAndInAndRestoresOpacity()
    {
        using var ui = TestUiContext.Install(); // continuations run between the frames this test drives
        var (container, content, loading, _) = Container();
        content.SetOpacity(0.5);
        loading.SetOpacity(0.8);
        using var surface = new SkUiTestSurface(new SkUiCoreHost().SetContent(container), 100, 100);
        surface.Frame(_time += 16);

        var change = container.ChangeStateWithAnimation(Loading);
        Assert.False(change.IsCompleted);
        Assert.False(container.CanStateChange);
        Assert.Null(container.CurrentState); // recorded at the end, as the toolkit does
        Assert.Throws<SkUiStateContainerException>(() => container.SetCurrentState(Error));
        await Assert.ThrowsAsync<SkUiStateContainerException>(() => container.ChangeStateWithAnimation(Error));

        Drive(ui, surface, () => change.IsCompleted);
        await change;
        Assert.True(container.CanStateChange);
        Assert.Equal(Loading, container.CurrentState);
        Assert.Same(loading, container.ShownChild);
        Assert.Equal(0.8, loading.Opacity, 3); // faded in to its own opacity
        Assert.Equal(0.5, content.Opacity); // the node that left gets its own back
    }

    [Fact]
    public async Task ACancelledChangeStillEndsInTheNewState()
    {
        using var ui = TestUiContext.Install();
        var (container, _, loading, _) = Container();
        using var surface = new SkUiTestSurface(new SkUiCoreHost().SetContent(container), 100, 100);
        surface.Frame(_time += 16);
        using var cancel = new CancellationTokenSource();
        var change = container.ChangeStateWithAnimation(Loading, cancel.Token);
        cancel.Cancel();
        Drive(ui, surface, () => change.IsCompleted);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => change);
        Assert.Equal(Loading, container.CurrentState);
        Assert.Same(loading, container.ShownChild);
        Assert.True(container.CanStateChange);
    }

    [Fact]
    public async Task AnimatedChangesValidateTheirArguments()
    {
        var (container, _, _, _) = Container();
        await Assert.ThrowsAsync<ArgumentException>(() => container.ChangeStateWithAnimation(Loading, null, null));
        await Assert.ThrowsAsync<SkUiStateContainerException>(() => container.ChangeStateWithAnimation("Missing"));
        Assert.True(container.CanStateChange);
    }

    [Fact]
    public async Task WithStateChangeAnimationsEveryChangeAnimatesAndTheNewestStateWins()
    {
        using var ui = TestUiContext.Install();
        var (container, content, loading, error) = Container();
        container.SetBeforeStateChangeAnimation(SkUiViewAnimation.FadeOut(100)).SetAfterStateChangeAnimation(SkUiViewAnimation.FadeIn(100));
        using var surface = new SkUiTestSurface(new SkUiCoreHost().SetContent(container), 100, 100);
        surface.Frame(_time += 16);
        var loadingParents = 0;
        ((INotifyPropertyChanged)loading).PropertyChanged += (_, args) => loadingParents += args.PropertyName == nameof(SkUiCoreNode.Parent) ? 1 : 0;

        container.SetCurrentState(Loading);
        Assert.Equal(Loading, container.CurrentState); // the value changes at once
        Assert.False(container.CanStateChange);
        Assert.Same(content, container.ShownChild); // the nodes follow
        container.SetCurrentState(Error); // no exception while it animates

        Drive(ui, surface, () => container.CanStateChange);
        Assert.Same(error, container.ShownChild);
        Assert.Equal(0, loadingParents); // replaced before it was shown
        Assert.Equal((1d, 1d), (error.Opacity, content.Opacity));

        // A state set while the new node animates in follows it.
        container.SetCurrentState(null);
        Drive(ui, surface, () => ReferenceEquals(container.ShownChild, content));
        container.SetCurrentState(Loading);
        Drive(ui, surface, () => container.CanStateChange);
        Assert.Same(loading, container.ShownChild);
        Assert.Equal((1d, 1d, 1d), (error.Opacity, content.Opacity, loading.Opacity));
        await Task.CompletedTask;
    }

    [Fact]
    public void AnimationsScaleOneAxisAndFreezeWhenUsed()
    {
        var (container, _, loading, _) = Container();
        var grow = new SkUiViewAnimation([new(SkUiAnimatableProperty.ScaleY, from: 0)]);
        container.SetAfterStateChangeAnimation(grow);
        Assert.True(grow.IsFrozen);
        container.SetCurrentState(Loading); // off screen: no animation
        Assert.Equal((1d, 1d), (loading.ScaleX, loading.ScaleY));
    }

    [Fact]
    public void AShimmerStateDrawsItsSkeleton()
    {
        var bone = new SkUiCoreBox { Color = Colors.Gray };
        var shimmer = new SkUiCoreShimmer { SkeletonContent = bone, Effect = SkUiShimmerEffect.None, BoneColor = Colors.Transparent };
        var container = new SkUiCoreStateContainer { Content = new SkUiCoreBox { Color = Colors.Green }, States = { { Loading, shimmer } } };
        using var surface = new SkUiTestSurface(new SkUiCoreHost().SetContent(container), 100, 100);
        surface.Frame();
        container.SetCurrentState(Loading);
        Assert.Equal(SKColors.Gray, surface.Frame().GetPixel(50, 50));
        Assert.True(bone.RenderState.HasCommitted);
        container.SetCurrentState(null);
        Assert.Equal(SKColors.Green, surface.Frame().GetPixel(50, 50));
        Assert.False(bone.RenderState.HasCommitted);
    }

    [Fact]
    public void PaddingInsetsTheShownNode()
    {
        var (container, content, _, _) = Container();
        container.SetPadding(new Thickness(10));
        using var surface = new SkUiTestSurface(new SkUiCoreHost().SetContent(container), 100, 100);
        var frame = surface.Frame();
        Assert.Equal(new Rect(10, 10, 80, 80), content.Frame);
        Assert.NotEqual(SKColors.Green, frame.GetPixel(5, 5));
    }

    // Frames until done: each frame advances the animations; their continuations run between frames.
    private static void Drive(TestUiContext ui, SkUiTestSurface surface, Func<bool> done)
    {
        for (var frame = 1; frame <= 200 && !done(); frame++)
        {
            surface.Frame(_time += 50);
            ui.RunPending();
        }
        Assert.True(done(), "The animated state change did not finish.");
    }
}
