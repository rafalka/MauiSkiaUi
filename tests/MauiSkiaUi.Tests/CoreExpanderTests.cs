using System.ComponentModel;
using System.Windows.Input;
using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary><see cref="SkUiCoreExpander"/>: the rules of <see cref="SkUiExpander"/> on the Core layer.</summary>
[Collection(GlobalStateCollection.Name)] // SkUiMotion.ReduceMotion
public class CoreExpanderTests
{
    private static long _pointer = 190_000;

    private static SkUiCoreBox Box(double height)
    {
        var box = new SkUiCoreBox();
        box.SetHeight(height);
        return box;
    }

    private static void Tap(SkUiView root, Point at)
    {
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, at, TimeSpan.FromSeconds(1)));
        root.Touch(new(id, SkUiTouchAction.Released, at, TimeSpan.FromSeconds(1.02)));
    }

    /// <summary>Ticks the root's UI clock to <paramref name="milliseconds"/> and renders a frame (the surface lays itself out first).</summary>
    private static void At(SkUiTestSurface surface, double milliseconds)
    {
        surface.Root.AnimationClock.Tick(TimeSpan.FromMilliseconds(milliseconds));
        surface.Frame(milliseconds);
    }

    private static void WithMotion(Action body)
    {
        var reduce = SkUiMotion.ReduceMotion;
        try
        {
            SkUiMotion.ReduceMotion = false;
            body();
        }
        finally
        {
            SkUiMotion.ReduceMotion = reduce;
        }
    }

    private static (SkUiCoreVerticalStackLayout Stack, SkUiTestSurface Surface) Hosted(SkUiCoreExpander expander, SkUiCoreNode? sibling = null, int height = 200)
    {
        var stack = new SkUiCoreVerticalStackLayout();
        stack.Add(expander);
        if (sibling is not null)
            stack.Add(sibling);
        return (stack, new SkUiTestSurface(new SkUiCoreHost().SetContent(stack), 100, height));
    }

    private sealed class Probe : ICommand
    {
        public List<string> Log { get; } = [];
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => Log.Add($"command {parameter}");
    }

    [Fact]
    public void DefaultsMatchTheSkUiExpander()
    {
        var expander = new SkUiCoreExpander();
        Assert.False(expander.IsExpanded);
        Assert.Equal(SkUiExpandDirection.Down, expander.Direction);
        Assert.Null(expander.Header);
        Assert.Null(expander.Content);
        Assert.False(expander.LazyContentExpansion);
        Assert.Equal(0u, expander.AnimationLength);
        Assert.Same(Easing.CubicInOut, expander.AnimationEasing);
        Assert.False(expander.ClipToBounds);
    }

    [Fact]
    public void CollapsedMeasuresTheHeaderAndExpandedAddsTheContent()
    {
        var expander = new SkUiCoreExpander { Header = Box(20), Content = Box(30), Padding = new Thickness(0, 2) };
        Assert.Equal(24, expander.Measure(100, 100).Height);
        expander.SetIsExpanded(true);
        Assert.Equal(54, expander.Measure(100, 100).Height);
    }

    [Fact]
    public void DirectionPlacesTheContentBelowOrAboveTheHeader()
    {
        var expander = new SkUiCoreExpander { Header = Box(20), Content = Box(30), IsExpanded = true };
        var (_, surface) = Hosted(expander);
        using var _ = surface;
        surface.Frame();
        Assert.Equal(0, expander.HeaderHost.Frame.Y);
        Assert.Equal(20, expander.ContentHost.Frame.Y);

        expander.SetDirection(SkUiExpandDirection.Up);
        surface.Frame();
        Assert.Equal(0, expander.ContentHost.Frame.Y);
        Assert.Equal(30, expander.HeaderHost.Frame.Y);
        Assert.Equal(1, expander.ContentHost.AnchorY); // grows from the header's side
        Assert.Throws<ArgumentOutOfRangeException>(() => expander.SetDirection((SkUiExpandDirection)7));
    }

    [Fact]
    public void TappingTheHeaderTogglesAndTappableHeaderNodesKeepTheirTaps()
    {
        var clicks = 0;
        var button = new SkUiCoreButton();
        button.SetText("x").SetWidth(20).SetHeight(20).SetHorizontalAlignment(LayoutAlignment.End);
        button.Clicked += (_, _) => clicks++;
        var header = new SkUiCoreAbsoluteLayout();
        header.SetHeight(20);
        header.Add(button, new Rect(80, 0, 20, 20));
        var expander = new SkUiCoreExpander { Header = header, Content = Box(30) };
        var (_, surface) = Hosted(expander);
        using var _ = surface;
        surface.Frame();

        Tap(surface.Root, new Point(10, 10));
        Assert.True(expander.IsExpanded);
        Tap(surface.Root, new Point(90, 10)); // the button
        Assert.Equal(1, clicks);
        Assert.True(expander.IsExpanded);
        Tap(surface.Root, new Point(10, 10));
        Assert.False(expander.IsExpanded);
    }

    [Fact]
    public void ChangesRunTheCommandThenTheEventAlsoWhenTheCommandThrows()
    {
        var probe = new Probe();
        var expander = new SkUiCoreExpander { Command = probe, CommandParameter = 7 };
        expander.ExpandedChanged += (_, args) => probe.Log.Add($"event {args.IsExpanded}");
        expander.SetIsExpanded(true).SetIsExpanded(true).SetIsExpanded(false);
        Assert.Equal(["command 7", "event True", "command 7", "event False"], probe.Log);

        var raised = false;
        var throwing = new SkUiCoreExpander { Command = new Command(() => throw new InvalidOperationException()) };
        throwing.ExpandedChanged += (_, _) => raised = true;
        Assert.Throws<InvalidOperationException>(() => throwing.SetIsExpanded(true));
        Assert.True(raised);
        Assert.True(throwing.IsExpanded);
    }

    [Fact]
    public void ContentIsInTheTreeFromTheStartAndHiddenWhileCollapsedOrOnlyWhileExpandedWhenLazy()
    {
        var content = Box(30);
        var expander = new SkUiCoreExpander { Content = content };
        Assert.Same(expander.ContentHost, content.Parent);
        Assert.False(expander.ContentHost.IsVisible);
        expander.SetIsExpanded(true);
        Assert.True(expander.ContentHost.IsVisible);

        expander.SetLazyContentExpansion(true).SetIsExpanded(false); // no animation: removed at once
        Assert.Null(content.Parent);
        expander.SetIsExpanded(true);
        Assert.Same(expander.ContentHost, content.Parent);
        expander.SetIsExpanded(false);
        var replacement = Box(10);
        expander.SetContent(replacement); // collapsed and lazy: not attached
        Assert.Null(replacement.Parent);
        Assert.Throws<InvalidOperationException>(() => expander.SetHeader(replacement));
    }

    [Fact]
    public void WithoutAnAnimationOrBeforeItIsDrawnChangesApplyAtOnce()
    {
        WithMotion(() =>
        {
            var expander = new SkUiCoreExpander { Header = Box(20), Content = Box(30), AnimationLength = 200, IsExpanded = true };
            Assert.False(expander.IsAnimating); // not drawn yet
            Assert.Equal(1, expander.Reveal);
            var (_, surface) = Hosted(expander);
            using var _ = surface;
            surface.Frame();

            SkUiMotion.ReduceMotion = true;
            expander.SetIsExpanded(false);
            Assert.False(expander.IsAnimating);
            Assert.False(expander.ContentHost.IsVisible);

            SkUiMotion.ReduceMotion = false;
            expander.SetAnimationLength(0).SetIsExpanded(true);
            Assert.False(expander.IsAnimating);
            Assert.Equal(1, expander.ContentHost.ScaleY);
        });
    }

    [Theory]
    [InlineData(SkUiExpandDirection.Down)]
    [InlineData(SkUiExpandDirection.Up)]
    public void ExpandingScalesTheContentFromTheHeaderAndTheHeightFollows(SkUiExpandDirection direction)
    {
        WithMotion(() =>
        {
            var content = Box(40);
            var sibling = Box(10);
            var expander = new SkUiCoreExpander { Header = Box(20), Content = content, AnimationLength = 200, Direction = direction };
            var (_, surface) = Hosted(expander, sibling);
            using var _ = surface;
            At(surface, 0);

            expander.SetIsExpanded(true);
            Assert.True(expander.IsAnimating);
            Assert.True(expander.ContentHost.IsVisible);
            At(surface, 100); // half way (CubicInOut is symmetric)
            Assert.Equal(0.5, expander.Reveal, 3);
            Assert.Equal(0.5, expander.ContentHost.ScaleY, 3);
            Assert.Equal(1, content.ScaleY); // the content's own scale is never touched
            Assert.Equal(40, expander.Frame.Height, 3);
            Assert.Equal(40, sibling.Frame.Y, 3);
            Assert.Equal(direction == SkUiExpandDirection.Down ? 20 : -20, expander.ContentHost.Frame.Y, 3);
            Assert.Equal(40, expander.ContentHost.Frame.Height, 3);
            Assert.Equal(direction == SkUiExpandDirection.Down ? 0 : 20, expander.HeaderHost.Frame.Y, 3);

            At(surface, 200);
            Assert.False(expander.IsAnimating);
            Assert.Equal(60, expander.Frame.Height);
            Assert.Equal(60, sibling.Frame.Y);
            Assert.Equal(1, expander.ContentHost.ScaleY);
        });
    }

    [Fact]
    public void CollapsingHidesOrDetachesTheContentWhenItEndsAndReversesFromWhereItIs()
    {
        WithMotion(() =>
        {
            var content = Box(40);
            var expander = new SkUiCoreExpander { Header = Box(20), Content = content, AnimationLength = 200, LazyContentExpansion = true, IsExpanded = true };
            var (_, surface) = Hosted(expander);
            using var _ = surface;
            At(surface, 0);

            expander.SetIsExpanded(false);
            At(surface, 100);
            Assert.Equal(0.5, expander.Reveal, 3);
            Assert.Same(expander.ContentHost, content.Parent); // still shown while it collapses

            expander.SetIsExpanded(true); // reverses from half way: the remaining half of the length
            At(surface, 150);
            Assert.True(expander.Reveal is > 0.5 and < 1, $"reveal {expander.Reveal}");
            At(surface, 200);
            Assert.False(expander.IsAnimating);
            Assert.Equal(1, expander.Reveal);

            expander.SetIsExpanded(false);
            At(surface, 300);
            Assert.True(expander.IsAnimating);
            At(surface, 400);
            Assert.False(expander.IsAnimating);
            Assert.Null(content.Parent);
            Assert.False(expander.ContentHost.IsVisible);
            Assert.Equal(20, expander.Frame.Height);
        });
    }

    [Fact]
    public void AnimatedFramesRecordOnlyTheResizedExpander()
    {
        WithMotion(() =>
        {
            var expander = new SkUiCoreExpander { Header = Box(20), Content = Box(40), AnimationLength = 200 };
            var stack = new SkUiCoreVerticalStackLayout();
            stack.SetBackground(Colors.White);
            stack.SetShadow(new Shadow { Brush = Colors.Black, Radius = 4 });
            stack.Add(expander);
            stack.Add(Box(10));
            var border = new SkUiCoreBorder().SetContent(stack);
            border.SetBackground(Colors.Gray);
            border.SetPadding(new Thickness(10));
            using var surface = new SkUiTestSurface(new SkUiCoreHost().SetContent(border), 120, 200);
            At(surface, 0);
            var ancestorPaints = 0;
            stack.PaintInvalidated += (_, _) => ancestorPaints++;
            border.PaintInvalidated += (_, _) => ancestorPaints++;

            expander.SetIsExpanded(true);
            At(surface, 50);
            var recorded = surface.RecordedPictures;
            At(surface, 100);
            At(surface, 150);
            Assert.Equal(0, ancestorPaints); // the stack and the border are not recorded again (R2)
            Assert.True(surface.RecordedPictures - recorded <= 2, $"{surface.RecordedPictures - recorded} pictures in two frames"); // at most the expander, once per frame
        });
    }

    [Fact]
    public void IsAnimatingIsObservable()
    {
        WithMotion(() =>
        {
            var expander = new SkUiCoreExpander { Header = Box(20), Content = Box(40), AnimationLength = 200 };
            var (_, surface) = Hosted(expander);
            using var _ = surface;
            At(surface, 0);
            var changes = new List<bool>();
            ((INotifyPropertyChanged)expander).PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(SkUiCoreExpander.IsAnimating))
                    changes.Add(expander.IsAnimating);
            };
            expander.SetIsExpanded(true);
            At(surface, 100);
            At(surface, 250);
            Assert.Equal([true, false], changes);
        });
    }

    [Fact]
    public void TheHeaderIsAButtonWithTheExpandedState()
    {
        // A fixed size: without an installed font (Linux CI) text measures empty, and zero-size nodes are not elements.
        var label = new SkUiCoreLabel().SetText("Details");
        label.SetWidth(80).SetHeight(20);
        var expander = new SkUiCoreExpander { Header = label, Content = Box(30) };
        var root = new SkUiContentView { Content = new SkUiCoreHost().SetContent(expander) };
        SkUiTestHelpers.Arrange(root, 200, 200);
        var owner = new SkUiSemanticsOwner(root);
        var header = owner.Tree.Find(expander.HeaderHost)!;
        Assert.Equal(SkUiSemanticsRole.Button, header.Role);
        Assert.Equal("Details", header.Label);
        Assert.Equal("Collapsed", header.Value);

        Assert.True(owner.Perform(header.Id, SkUiSemanticsActions.Activate));
        Assert.True(expander.IsExpanded);
        Assert.Equal("Expanded", SkUiSemanticsTree.Build(root).Find(expander.HeaderHost)!.Value);
    }
}
