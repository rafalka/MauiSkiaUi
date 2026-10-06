using System.ComponentModel;
using System.Windows.Input;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// <see cref="SkUiExpander"/>: Community Toolkit <c>Expander</c> API, header taps, lazy and template content, and the
/// expand / collapse animation (content scaled from the header's side, height following it on the UI clock).
/// </summary>
[Collection(GlobalStateCollection.Name)] // SkUiMotion.ReduceMotion
public class ExpanderTests
{
    private static long _pointer = 90_000;

    private static SkUiBox Box(double height) => new() { HeightRequest = height };

    private static void Tap(SkUiView root, Point at)
    {
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, at, TimeSpan.FromSeconds(1)));
        root.Touch(new(id, SkUiTouchAction.Released, at, TimeSpan.FromSeconds(1.02)));
    }

    /// <summary>
    /// Ticks the root's UI clock to <paramref name="milliseconds"/> and renders a frame, as the platform UI tick does:
    /// the surface lays itself out again before recording (no native layout pass).
    /// </summary>
    private static void At(SkUiTestSurface surface, double milliseconds)
    {
        surface.Root.AnimationClock.Tick(TimeSpan.FromMilliseconds(milliseconds));
        surface.Frame(milliseconds);
    }

    /// <summary>Runs <paramref name="body"/> with motion on (the system setting is not read in tests, but another test may set it).</summary>
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

    private sealed class Probe : ICommand
    {
        public List<string> Log { get; } = [];
        public bool Enabled { get; set; } = true;
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => Enabled;
        public void Execute(object? parameter) => Log.Add($"command {parameter}");
    }

    [Fact]
    public void DefaultsMatchTheToolkit()
    {
        var expander = new SkUiExpander();
        Assert.False(expander.IsExpanded);
        Assert.Equal(SkUiExpandDirection.Down, expander.Direction);
        Assert.Null(expander.Header);
        Assert.Null(expander.Content);
        Assert.Null(expander.Command);
        Assert.Null(expander.CommandParameter);
        Assert.False(expander.LazyContentExpansion);
        Assert.Equal(0u, expander.AnimationLength);
        Assert.False(expander.ClipToBounds); // a content view: shadows and press effects may overflow
        Assert.Equal(BindingMode.TwoWay, SkUiExpander.IsExpandedProperty.DefaultBindingMode);
    }

    [Fact]
    public void CollapsedMeasuresTheHeaderAndExpandedAddsTheContent()
    {
        var expander = new SkUiExpander { Header = Box(20), Content = Box(30), Padding = new Thickness(0, 2) };
        Assert.Equal(24, ((IView)expander).Measure(100, 100).Height);
        expander.IsExpanded = true;
        Assert.Equal(54, ((IView)expander).Measure(100, 100).Height);
        expander.IsExpanded = false;
        Assert.Equal(24, ((IView)expander).Measure(100, 100).Height);
    }

    [Fact]
    public void DirectionPlacesTheContentBelowOrAboveTheHeader()
    {
        SkUiBox header = Box(20), content = Box(30);
        var expander = new SkUiExpander { Header = header, Content = content, IsExpanded = true };
        SkUiTestHelpers.Arrange(new SkUiContentView { Content = new SkUiVerticalStackLayout { Children = { expander } } }, 100, 200);
        Assert.Equal(0, expander.HeaderHost.Frame.Y);
        Assert.Equal(new Rect(0, 20, 100, 30), expander.ContentHost.Frame);
        Assert.Equal(0, expander.ContentHost.AnchorY);
        Assert.Equal(new[] { expander.HeaderHost, expander.ContentHost }, expander.SkiaChildren);

        expander.Direction = SkUiExpandDirection.Up;
        SkUiTestHelpers.Arrange((IView)((Element)expander.Parent!).Parent!, 100, 200);
        Assert.Equal(new Rect(0, 0, 100, 30), expander.ContentHost.Frame);
        Assert.Equal(30, expander.HeaderHost.Frame.Y);
        Assert.Equal(1, expander.ContentHost.AnchorY); // grows from the header
        Assert.Equal(new[] { expander.ContentHost, expander.HeaderHost }, expander.SkiaChildren);

        expander.Direction = (SkUiExpandDirection)7; // rejected (MAUI ignores invalid values)
        Assert.Equal(SkUiExpandDirection.Up, expander.Direction);
        Assert.Throws<ArgumentOutOfRangeException>(() => expander.SetDirection((SkUiExpandDirection)7));
    }

    [Fact]
    public void TappingTheHeaderTogglesAndTappableHeaderViewsKeepTheirTaps()
    {
        var clicks = 0;
        var button = new SkUiButton { Text = "Go", WidthRequest = 40, HeightRequest = 20, HorizontalOptions = LayoutOptions.End };
        button.Clicked += (_, _) => clicks++;
        var header = new SkUiGrid { HeightRequest = 20, Children = { button } };
        var expander = new SkUiExpander { Header = header, Content = Box(30) };
        var root = new SkUiContentView { Content = new SkUiVerticalStackLayout { Children = { expander } } };
        SkUiTestHelpers.Arrange(root, 100, 200);

        Tap(root, new Point(10, 10));
        Assert.True(expander.IsExpanded);
        Tap(root, new Point(90, 10)); // the button
        Assert.True(expander.IsExpanded);
        Assert.Equal(1, clicks);
        SkUiTestHelpers.Arrange(root, 100, 200);
        Tap(root, new Point(10, 35)); // the content: not a toggle
        Assert.True(expander.IsExpanded);
        Tap(root, new Point(10, 10));
        Assert.False(expander.IsExpanded);
    }

    [Fact]
    public void ChangesRunTheCommandThenTheEvent()
    {
        var command = new Probe();
        var expander = new SkUiExpander { Command = command, CommandParameter = "p" };
        expander.ExpandedChanged += (_, e) => command.Log.Add($"event {e.IsExpanded}");
        expander.IsExpanded = true;
        expander.IsExpanded = true; // no change
        command.Enabled = false;
        expander.IsExpanded = false;
        Assert.Equal(["command p", "event True", "event False"], command.Log);
    }

    [Fact]
    public void ContentIsInTheTreeFromTheStartAndHiddenWhileCollapsed()
    {
        var context = new object();
        var content = Box(30);
        var expander = new SkUiExpander { Header = Box(20), Content = content, BindingContext = context };
        Assert.Same(expander.ContentHost, content.Parent);
        Assert.Same(context, content.BindingContext);
        using var surface = new SkUiTestSurface(new SkUiContentView { Content = expander }, 100, 100);
        Assert.False(content.IsShown);
        expander.IsExpanded = true;
        Assert.True(content.IsShown);
    }

    [Fact]
    public void LazyContentIsInTheTreeOnlyWhileExpanded()
    {
        var content = Box(30);
        var expander = new SkUiExpander { LazyContentExpansion = true, Header = Box(20), Content = content };
        _ = new SkUiVerticalStackLayout { Children = { expander } };
        Assert.Null(content.Parent);
        expander.IsExpanded = true;
        Assert.Same(expander.ContentHost, content.Parent);
        expander.IsExpanded = false;
        Assert.Null(content.Parent);
        Assert.Same(content, expander.Content); // kept

        expander.LazyContentExpansion = false; // collapsed: attached (hidden) at once
        Assert.Same(expander.ContentHost, content.Parent);
        expander.LazyContentExpansion = true;
        Assert.Null(content.Parent);
    }

    [Fact]
    public void TemplatesRunInATreeOrOnTheFirstExpandWhenLazy()
    {
        var created = 0;
        var template = new DataTemplate(() => { created++; return Box(30); });
        var eager = new SkUiExpander { ContentTemplate = template };
        var lazy = new SkUiExpander { ContentTemplate = template, LazyContentExpansion = true };
        Assert.Equal(0, created); // not in a tree
        _ = new SkUiVerticalStackLayout { Children = { eager, lazy } };
        Assert.Equal(1, created);
        Assert.NotNull(eager.Content);
        Assert.Null(lazy.Content);

        lazy.IsExpanded = true;
        Assert.Equal(2, created);
        var content = lazy.Content!;
        lazy.IsExpanded = false;
        Assert.Null(content.Parent);
        lazy.IsExpanded = true;
        Assert.Same(content, lazy.Content); // the instance is kept for the next expand
        Assert.Equal(2, created);

        lazy.ContentTemplate = new DataTemplate(() => Box(10)); // a new template replaces it
        Assert.NotSame(content, lazy.Content);
        Assert.Null(content.Parent);
    }

    [Fact]
    public void ASelectorPicksAgainWhenTheBindingContextChanges()
    {
        var expander = new SkUiExpander { ContentTemplate = new EvenSelector(), BindingContext = 1 };
        _ = new SkUiVerticalStackLayout { Children = { expander } };
        Assert.IsType<SkUiBox>(expander.Content);
        expander.BindingContext = 2;
        Assert.IsType<SkUiEllipse>(expander.Content);
    }

    private sealed class EvenSelector : DataTemplateSelector
    {
        private readonly DataTemplate _box = new(() => new SkUiBox());
        private readonly DataTemplate _ellipse = new(() => new SkUiEllipse());
        protected override DataTemplate OnSelectTemplate(object item, BindableObject container) => item is int number && number % 2 == 0 ? _ellipse : _box;
    }

    [Fact]
    public void WithoutAnAnimationOrBeforeItIsDrawnChangesApplyAtOnce()
    {
        WithMotion(() =>
        {
            var expander = new SkUiExpander { Header = Box(20), Content = Box(30), AnimationLength = 200, IsExpanded = true };
            Assert.False(expander.IsAnimating); // not drawn yet
            Assert.Equal(1, expander.Reveal);
            var root = new SkUiContentView { Content = new SkUiVerticalStackLayout { Children = { expander } } };
            using var surface = new SkUiTestSurface(root, 100, 100);
            surface.Frame();

            SkUiMotion.ReduceMotion = true;
            expander.IsExpanded = false;
            Assert.False(expander.IsAnimating);
            Assert.False(expander.ContentHost.IsVisible);

            SkUiMotion.ReduceMotion = false;
            expander.AnimationLength = 0;
            expander.IsExpanded = true;
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
            var expander = new SkUiExpander { Header = Box(20), Content = content, AnimationLength = 200, Direction = direction };
            var sibling = Box(10);
            var root = new SkUiContentView { Content = new SkUiVerticalStackLayout { Children = { expander, sibling } } };
            using var surface = new SkUiTestSurface(root, 100, 200);
            At(surface, 0);

            expander.IsExpanded = true;
            Assert.True(expander.IsAnimating);
            Assert.True(expander.ContentHost.IsVisible);
            At(surface, 100); // half way (CubicInOut is symmetric)
            Assert.Equal(0.5, expander.Reveal, 3);
            Assert.Equal(0.5, expander.ContentHost.ScaleY, 3);
            Assert.Equal(1, content.ScaleY); // the content's own scale is never touched
            Assert.Equal(40, expander.Height, 3);
            Assert.Equal(40, sibling.Frame.Y, 3);
            // The scaled content fills the band next to the header.
            var expectedContentY = direction == SkUiExpandDirection.Down ? 20 : -20;
            Assert.Equal(expectedContentY, expander.ContentHost.Frame.Y, 3);
            Assert.Equal(40, expander.ContentHost.Frame.Height, 3);
            Assert.Equal(direction == SkUiExpandDirection.Down ? 0 : 20, expander.HeaderHost.Frame.Y, 3);

            At(surface, 200);
            Assert.False(expander.IsAnimating);
            Assert.Equal(60, expander.Height);
            Assert.Equal(60, sibling.Frame.Y);
            Assert.Equal(1, expander.ContentHost.ScaleY);
        });
    }

    [Fact]
    public void AnimationEasingShapesTheRevealAndOvershootNeverGoesBelowNothing()
    {
        WithMotion(() =>
        {
            var expander = new SkUiExpander { Header = Box(20), Content = Box(40), AnimationLength = 200 };
            Assert.Same(Easing.CubicInOut, expander.AnimationEasing);
            var root = new SkUiContentView { Content = new SkUiVerticalStackLayout { Children = { expander } } };
            using var surface = new SkUiTestSurface(root, 100, 200);
            At(surface, 0);

            expander.AnimationEasing = Easing.Linear;
            expander.IsExpanded = true;
            At(surface, 50);
            Assert.Equal(0.25, expander.Reveal, 3);
            Assert.Equal(30, expander.Height, 3);
            At(surface, 200);

            // SpringIn dips below its start: collapsing from expanded overshoots past 1 (stretched), and expanding
            // starts below 0, which shows nothing instead of a flipped content or a height below the header's.
            expander.AnimationEasing = Easing.SpringIn;
            var reveals = new List<double>();
            expander.IsExpanded = false;
            for (var time = 210; time <= 400; time += 10)
            {
                At(surface, time);
                reveals.Add(expander.Reveal);
                Assert.True(expander.ContentHost.ScaleY >= 0);
                Assert.True(expander.Height >= 20);
            }
            Assert.Contains(reveals, reveal => reveal > 1);
            expander.IsExpanded = true;
            for (var time = 410; time <= 600; time += 10)
            {
                At(surface, time);
                Assert.True(expander.Reveal >= 0);
                Assert.True(expander.Height >= 20);
            }
            Assert.Equal(60, expander.Height);

            expander.AnimationEasing = null; // linear
            expander.IsExpanded = false;
            At(surface, 650);
            Assert.Equal(0.75, expander.Reveal, 3);
        });
    }

    [Fact]
    public void CollapsingHidesOrDetachesTheContentWhenItEndsAndReversesFromWhereItIs()
    {
        WithMotion(() =>
        {
            var content = Box(40);
            var expander = new SkUiExpander { Header = Box(20), Content = content, AnimationLength = 200, LazyContentExpansion = true, IsExpanded = true };
            var root = new SkUiContentView { Content = new SkUiVerticalStackLayout { Children = { expander } } };
            using var surface = new SkUiTestSurface(root, 100, 200);
            At(surface, 0);

            expander.IsExpanded = false;
            At(surface, 100);
            Assert.Equal(0.5, expander.Reveal, 3);
            Assert.Same(expander.ContentHost, content.Parent); // still shown while it collapses

            expander.IsExpanded = true; // reverses from half way: the remaining half of the length
            At(surface, 150);
            Assert.True(expander.Reveal is > 0.5 and < 1, $"reveal {expander.Reveal}");
            At(surface, 200);
            Assert.False(expander.IsAnimating);
            Assert.Equal(1, expander.Reveal);

            expander.IsExpanded = false;
            At(surface, 300);
            Assert.True(expander.IsAnimating);
            At(surface, 400);
            Assert.False(expander.IsAnimating);
            Assert.Null(content.Parent);
            Assert.False(expander.ContentHost.IsVisible);
            Assert.Equal(20, expander.Height);
        });
    }

    [Fact]
    public void AnimatedFramesRecordNothingAboveTheExpander()
    {
        WithMotion(() =>
        {
            var expander = new SkUiExpander { Header = Box(20), Content = Box(40), AnimationLength = 200 };
            var stack = new SkUiVerticalStackLayout
            {
                Background = Colors.White,
                Shadow = new Shadow { Brush = Colors.Black, Radius = 4 },
                Children = { expander, Box(10) }
            };
            var root = new SkUiContentView { Background = Colors.Gray, Padding = 10, Content = stack };
            using var surface = new SkUiTestSurface(root, 120, 200);
            At(surface, 0);
            var ancestorPaints = 0;
            stack.PaintInvalidated += (_, _) => ancestorPaints++;

            expander.IsExpanded = true;
            At(surface, 50);
            var recorded = surface.RecordedPictures;
            At(surface, 100);
            At(surface, 150);
            Assert.Equal(0, ancestorPaints);
            Assert.Equal(recorded + 2, surface.RecordedPictures); // the resized expander, once per frame
        });
    }

    [Fact]
    public void AnExpanderThatIsTheSurfaceLaysItselfOutInPlaceEveryFrame()
    {
        WithMotion(() =>
        {
            // The expander is the surface root: each reveal frame is laid out before it is recorded (no native layout,
            // no explicit arrange), and the expander's own picture is not recorded again (its bounds stay the same).
            var header = Box(20);
            var expander = new SkUiExpander { Header = header, Content = Box(40), AnimationLength = 200, Direction = SkUiExpandDirection.Up, Background = Colors.White };
            using var surface = new SkUiTestSurface(expander, 100, 200);
            At(surface, 0);
            var recorded = surface.RecordedPictures;
            expander.IsExpanded = true;
            At(surface, 100);
            Assert.Equal(20, expander.HeaderHost.Frame.Y, 3); // Up: the header sits below the revealed half
            Assert.Equal(0.5, expander.ContentHost.ScaleY, 3);
            var midway = surface.RecordedPictures;
            At(surface, 200);
            Assert.Equal(40, expander.HeaderHost.Frame.Y);
            Assert.Equal(new Rect(0, 0, 100, 200), expander.Frame);
            Assert.Equal(midway, surface.RecordedPictures); // a frame of the animation records nothing
            Assert.True(midway - recorded <= 2, $"{midway - recorded} pictures recorded when the content was shown");
        });
    }

    [Fact]
    public void IsAnimatingIsObservable()
    {
        WithMotion(() =>
        {
            var expander = new SkUiExpander { Header = Box(20), Content = Box(40), AnimationLength = 200 };
            var seen = new List<bool>();
            expander.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SkUiExpander.IsAnimating))
                    seen.Add(expander.IsAnimating);
            };
            using var surface = new SkUiTestSurface(new SkUiContentView { Content = new SkUiVerticalStackLayout { Children = { expander } } }, 100, 200);
            At(surface, 0);
            expander.IsExpanded = true;
            expander.IsExpanded = false; // reversed: still animating, no change to report
            At(surface, 100);
            At(surface, 300);
            Assert.Equal([true, false], seen);
        });
    }

    [Fact]
    public void ExpandedChangedIsRaisedEvenWhenTheCommandThrows()
    {
        var raised = new List<bool>();
        var expander = new SkUiExpander { Command = new Command(() => throw new InvalidOperationException("app bug")) };
        expander.ExpandedChanged += (_, e) => raised.Add(e.IsExpanded);
        Assert.Throws<InvalidOperationException>(() => expander.IsExpanded = true);
        Assert.True(expander.IsExpanded);
        Assert.Equal([true], raised);
    }

    [Fact]
    public void TheExpandedStateTextCanBeLocalized()
    {
        var original = SkUiExpander.ExpandedStateText;
        try
        {
            SkUiExpander.ExpandedStateText = expanded => expanded ? "Rozwinięte" : "Zwinięte";
            var expander = new SkUiExpander { Header = new SkUiLabel { Text = "Szczegóły", WidthRequest = 80, HeightRequest = 20 }, Content = Box(30) };
            var root = new SkUiContentView { Content = new SkUiVerticalStackLayout { Children = { expander } } };
            SkUiTestHelpers.Arrange(root, 200, 200);
            Assert.Equal("Zwinięte", SkUiSemanticsTree.Build(root).Find(expander.HeaderHost)!.Value);
            Assert.Throws<ArgumentNullException>(() => SkUiExpander.ExpandedStateText = null!);
        }
        finally
        {
            SkUiExpander.ExpandedStateText = original;
        }
    }

    [Fact]
    public void ScrollExtentsFollowTheAnimation()
    {
        WithMotion(() =>
        {
            var expander = new SkUiExpander { Header = Box(20), Content = Box(200), AnimationLength = 200 };
            var scroll = new SkUiScrollView { Content = new SkUiVerticalStackLayout { Children = { expander } } };
            using var surface = new SkUiTestSurface(new SkUiContentView { Content = scroll }, 100, 100);
            At(surface, 0);
            expander.IsExpanded = true;
            At(surface, 100);
            Assert.Equal(120, scroll.ContentSize.Height, 3);
            At(surface, 200);
            Assert.Equal(220, scroll.ContentSize.Height);
        });
    }

    [Fact]
    public void NestedExpandersMeasureThroughEachOther()
    {
        var inner = new SkUiExpander { Header = Box(10), Content = Box(15) };
        var outer = new SkUiExpander { Header = Box(20), Content = inner, Direction = SkUiExpandDirection.Up, IsExpanded = true };
        var root = new SkUiContentView { Content = new SkUiVerticalStackLayout { Children = { outer } } };
        SkUiTestHelpers.Arrange(root, 100, 200);
        Assert.Equal(30, outer.Height);
        inner.IsExpanded = true;
        SkUiTestHelpers.Arrange(root, 100, 200);
        Assert.Equal(45, outer.Height);
        Assert.Equal(25, outer.HeaderHost.Frame.Y);
    }

    [Fact]
    public void HostedNativeContentIsHiddenWhileCollapsedAndClippedWhileAnimating()
    {
        WithMotion(() =>
        {
            var native = new SkUiMauiContentView { Content = new Editor(), HeightRequest = 40 };
            var expander = new SkUiExpander { Header = Box(20), Content = native, AnimationLength = 200 };
            var root = new SkUiContentView { Content = new SkUiVerticalStackLayout { Children = { expander } } };
            using var surface = new SkUiTestSurface(root, 100, 200);
            At(surface, 0);
            Assert.True(native.IsNativeHidden);

            expander.IsExpanded = true;
            Assert.False(native.IsNativeHidden);
            At(surface, 100);
            Assert.Equal(new Rect(0, 0, 100, 40), native.ComputeRootRelativeClip()); // the expander's band, not the full 20 + 40
            At(surface, 200);
            Assert.True(native.ComputeRootRelativeClip().Height > 1000); // unclipped again
            expander.IsExpanded = false;
            At(surface, 400);
            Assert.True(native.IsNativeHidden);
        });
    }

    [Fact]
    public void TheHeaderIsAButtonWithTheExpandedState()
    {
        // A fixed size: without an installed font (Linux CI) text measures empty, and zero-size views are not elements.
        var expander = new SkUiExpander { Header = new SkUiLabel { Text = "Details", WidthRequest = 80, HeightRequest = 20 }, Content = Box(30) };
        var root = new SkUiContentView { Content = new SkUiVerticalStackLayout { Children = { expander } } };
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

/// <summary>The toolkit's <c>Expander</c> samples with only the prefix changed (and drawn views) load, bind and measure.</summary>
[Collection(RuntimeXamlCollection.Name)]
public class ExpanderXamlTests
{
    private sealed class Model : INotifyPropertyChanged
    {
        public string Name => "SkiaUi";
        public bool Open { get; set { field = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Open))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    [Fact]
    public void ToolkitSamplesLoadWithThePrefixChanged()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        const string xaml = """
            <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                         xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <sk:SkUiVerticalStackLayout Spacing="10">
                <sk:SkUiExpander x:Name="Simple">
                  <sk:SkUiExpander.Header>
                    <sk:SkUiLabel Text="Simple Expander (Tap Me)" FontSize="16" FontAttributes="Bold" HeightRequest="20" />
                  </sk:SkUiExpander.Header>
                  <sk:SkUiExpander.Content>
                    <sk:SkUiVerticalStackLayout>
                      <sk:SkUiLabel Text="Item 1" HeightRequest="20" />
                      <sk:SkUiLabel Text="Item 2" HeightRequest="20" />
                    </sk:SkUiVerticalStackLayout>
                  </sk:SkUiExpander.Content>
                </sk:SkUiExpander>
                <sk:SkUiExpander x:Name="Multi" Direction="Up">
                  <sk:SkUiExpander.Header>
                    <sk:SkUiLabel Text="Multi-Level Expander (Tap Me)" FontSize="16" FontAttributes="Bold" HeightRequest="20" />
                  </sk:SkUiExpander.Header>
                  <sk:SkUiExpander.Content>
                    <sk:SkUiExpander Direction="Down" BackgroundColor="LightGray">
                      <sk:SkUiExpander.Header>
                        <sk:SkUiLabel Text="Nested Expander (Tap Me)" FontSize="14" FontAttributes="Bold" HeightRequest="20" />
                      </sk:SkUiExpander.Header>
                      <sk:SkUiExpander.Content>
                        <sk:SkUiLabel Text="Item 1" HeightRequest="20" />
                      </sk:SkUiExpander.Content>
                    </sk:SkUiExpander>
                  </sk:SkUiExpander.Content>
                </sk:SkUiExpander>
                <sk:SkUiExpander x:Name="Bound" IsExpanded="{Binding Open}" LazyContentExpansion="True" AnimationLength="250" AnimationEasing="SpringOut">
                  <sk:SkUiExpander.Header>
                    <sk:SkUiLabel Text="{Binding Name}" HeightRequest="20" />
                  </sk:SkUiExpander.Header>
                  <sk:SkUiExpander.ContentTemplate>
                    <DataTemplate>
                      <sk:SkUiLabel Text="{Binding Name}" HeightRequest="20" />
                    </DataTemplate>
                  </sk:SkUiExpander.ContentTemplate>
                </sk:SkUiExpander>
              </sk:SkUiVerticalStackLayout>
            </ContentView>
            """;
        var model = new Model();
        var root = new ContentView { BindingContext = model };
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var stack = (SkUiVerticalStackLayout)root.Content;
        var (simple, multi, bound) = ((SkUiExpander)stack.Children[0], (SkUiExpander)stack.Children[1], (SkUiExpander)stack.Children[2]);

        Assert.Equal(20, ((IView)simple).Measure(300, 500).Height);
        simple.IsExpanded = true;
        Assert.Equal(60, ((IView)simple).Measure(300, 500).Height);

        Assert.Equal(SkUiExpandDirection.Up, multi.Direction);
        multi.IsExpanded = true;
        ((SkUiExpander)multi.Content!).IsExpanded = true;
        Assert.Equal(60, ((IView)multi).Measure(300, 500).Height);

        Assert.Equal(250u, bound.AnimationLength);
        Assert.Same(Easing.SpringOut, bound.AnimationEasing);
        Assert.Null(bound.Content); // lazy: the template waits for the first expand
        model.Open = true;
        var label = Assert.IsType<SkUiLabel>(bound.Content);
        Assert.Equal("SkiaUi", label.Text);
        Assert.True(((ISkUiAccessibleNode)bound.HeaderHost).PerformSemanticsAction(SkUiSemanticsActions.Activate)); // the header's tap
        Assert.False(model.Open); // two-way
    }
}
