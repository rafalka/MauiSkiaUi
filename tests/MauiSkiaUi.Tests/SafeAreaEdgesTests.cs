using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// MAUI 10's <c>SafeAreaEdges</c> on drawn containers (step S1 in docs/design/ImplementationPlan-1.2.md): a surface root pads
/// itself by its overlap with the window's insets per edge it keeps clear. Opt-in: every drawn container defaults to
/// <c>None</c> (MAUI's layouts default to <c>Container</c>); drawn views inside a surface accept the property and ignore it.
/// </summary>
[Collection(RuntimeXamlCollection.Name)]
public class SafeAreaEdgesTests
{
    // A phone in portrait: the status bar and notch above, the home indicator below.
    private static readonly Size Window = new(400, 800);
    private static readonly Thickness PhoneInsets = new(0, 47, 0, 34);

    private sealed class WindowScope : IDisposable
    {
        public WindowScope(Point origin) => SkUiView.WindowPlacementOverride = _ => (Window, origin, PhoneInsets);

        public void Dispose() => SkUiView.WindowPlacementOverride = null;
    }

    private static (SkUiView Root, SkUiBox Child) Root(SkUiView root)
    {
        var child = new SkUiBox();
        switch (root)
        {
            case SkUiLayout layout: layout.Children.Add(child); break;
            case SkUiContentView content: content.Content = child; break;
        }
        return (root, child);
    }

    private static T Load<T>(string xaml) where T : View
    {
        var root = new ContentView();
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, $"""
            <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              {xaml}
            </ContentView>
            """);
        var view = (T)root.Content;
        root.Content = null;
        return view;
    }

    [Fact]
    public void EdgeToEdgeUntilSet()
    {
        Assert.Equal(SafeAreaEdges.None, new SkUiGrid().SafeAreaEdges);
        Assert.Equal(SafeAreaEdges.None, new SkUiVerticalStackLayout().SafeAreaEdges);
        Assert.Equal(SafeAreaEdges.None, new SkUiContentView().SafeAreaEdges);
        Assert.Equal(SafeAreaEdges.None, new SkUiBorder().SafeAreaEdges);
        Assert.Equal(SafeAreaEdges.Default, new SkUiScrollView().SafeAreaEdges);
        Assert.Equal(SafeAreaEdges.None, new SkUiSwipeView().SafeAreaEdges);
        Assert.Equal(SafeAreaEdges.None, new SkUiRefreshView().SafeAreaEdges);
    }

    [Fact]
    public void ALayoutThatIsASurfaceKeepsItsChildrenOutOfTheBarsAndTheNotch()
    {
        using var _ = new WindowScope(Point.Zero);
        var (edgeToEdge, first) = Root(new SkUiGrid());
        SkUiTestHelpers.Arrange(edgeToEdge, Window.Width, Window.Height);
        Assert.Equal(new Rect(0, 0, 400, 800), first.Frame); // the default, as in 1.x

        var container = Load<SkUiGrid>("""<sk:SkUiGrid SafeAreaEdges="Container" />""");
        var (root, child) = Root(container);
        SkUiTestHelpers.Arrange(root, Window.Width, Window.Height);
        Assert.Equal(PhoneInsets, root.SafeAreaInsetInUse);
        Assert.Equal(new Rect(0, 47, 400, 800 - 47 - 34), child.Frame);
    }

    [Fact]
    public void ContentViewsGoEdgeToEdgeUnlessTheyKeepEdgesClear()
    {
        using var _ = new WindowScope(Point.Zero);
        var (root, child) = Root(new SkUiContentView());
        SkUiTestHelpers.Arrange(root, Window.Width, Window.Height);
        Assert.Equal(new Rect(0, 0, 400, 800), child.Frame);

        var bottomOnly = Load<SkUiContentView>("""<sk:SkUiContentView SafeAreaEdges="None, None, None, Container" />""");
        var (root2, child2) = Root(bottomOnly);
        SkUiTestHelpers.Arrange(root2, Window.Width, Window.Height);
        Assert.Equal(new Rect(0, 0, 400, 800 - 34), child2.Frame);
    }

    [Fact]
    public void OnlyTheOverlapWithTheInsetsIsPadded()
    {
        // The surface starts below the status bar (a native header above it) and ends above the home indicator's area.
        using var _ = new WindowScope(new Point(0, 100));
        var (root, child) = Root(new SkUiVerticalStackLayout { SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container) });
        SkUiTestHelpers.Arrange(root, Window.Width, 600);
        Assert.Equal(default, root.SafeAreaInsetInUse);
        Assert.Equal(0, child.Frame.Y);
    }

    [Fact]
    public void NestedDrawnViewsIgnoreIt()
    {
        using var _ = new WindowScope(Point.Zero);
        var inner = new SkUiGrid { SafeAreaEdges = SafeAreaEdges.All };
        var (innerRoot, child) = Root(inner);
        var root = new SkUiContentView { Content = innerRoot };
        SkUiTestHelpers.Arrange(root, Window.Width, Window.Height);
        Assert.Equal(default, inner.SafeAreaInsetInUse);
        Assert.Equal(new Rect(0, 0, 400, 800), child.Frame);
    }

    [Fact]
    public void AScrollViewArrangesItsContentInsideThePadding()
    {
        using var _ = new WindowScope(Point.Zero);
        var (root, child) = Root(new SkUiScrollView { SafeAreaEdges = SafeAreaEdges.All });
        child.HeightRequest = 2000;
        SkUiTestHelpers.Arrange(root, Window.Width, Window.Height);
        Assert.Equal(new Rect(0, 47, 400, 2000), child.Frame);
    }

    [Fact]
    public void SettingItLaysTheRootOutAgain()
    {
        using var _ = new WindowScope(Point.Zero);
        var (root, child) = Root(new SkUiGrid { SafeAreaEdges = SafeAreaEdges.All });
        SkUiTestHelpers.Arrange(root, Window.Width, Window.Height);
        Assert.Equal(47, child.Frame.Y);
        ((SkUiGrid)root).SafeAreaEdges = SafeAreaEdges.None;
        SkUiTestHelpers.Arrange(root, Window.Width, Window.Height);
        Assert.Equal(new Rect(0, 0, 400, 800), child.Frame);
    }

    [Fact]
    public void SoftInputKeepsTheBottomClearOfTheKeyboard()
    {
        var regions = (SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.SoftInput);
        var placed = new Rect(0, 0, 400, 800);
        Assert.Equal(new Thickness(0, 0, 0, 300), SkUiView.SafeAreaPadding(regions, placed, Window, PhoneInsets, keyboardTop: 500));
        Assert.Equal(default, SkUiView.SafeAreaPadding(regions, placed, Window, PhoneInsets, double.PositiveInfinity));
        // All: the bars, the notch and the keyboard; the larger of the home indicator and the keyboard at the bottom.
        var all = (SafeAreaRegions.All, SafeAreaRegions.All, SafeAreaRegions.All, SafeAreaRegions.All);
        Assert.Equal(new Thickness(0, 47, 0, 300), SkUiView.SafeAreaPadding(all, placed, Window, PhoneInsets, keyboardTop: 500));
        Assert.Equal(new Thickness(0, 47, 0, 34), SkUiView.SafeAreaPadding(all, placed, Window, PhoneInsets, keyboardTop: 790));
        // Without a known window size, only the start edges.
        Assert.Equal(new Thickness(0, 47, 0, 0), SkUiView.SafeAreaPadding(all, placed, Size.Zero, PhoneInsets, double.PositiveInfinity));
    }

    [Fact]
    public void TheKeyboardsReportPadsARootThatKeepsClearOfIt()
    {
        using var _ = new WindowScope(Point.Zero);
        var (root, child) = Root(new SkUiGrid { SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container, SafeAreaRegions.Container, SafeAreaRegions.Container, SafeAreaRegions.All) });
        SkUiTestHelpers.Arrange(root, Window.Width, Window.Height);
        var focus = new SkUiFocusManager(root);
        focus.SetKeyboardTop(500);
        SkUiTestHelpers.Arrange(root, Window.Width, Window.Height);
        Assert.Equal(new Rect(0, 47, 400, 500 - 47), child.Frame);
        focus.SetKeyboardTop(double.PositiveInfinity);
        SkUiTestHelpers.Arrange(root, Window.Width, Window.Height);
        Assert.Equal(new Rect(0, 47, 400, 800 - 47 - 34), child.Frame);
    }

    [Fact]
    public void TheKeyboardIsKnownWhileTheBottomIgnoresIt()
    {
        using var _ = new WindowScope(Point.Zero);
        var softInput = new SafeAreaEdges(SafeAreaRegions.Container, SafeAreaRegions.Container, SafeAreaRegions.Container, SafeAreaRegions.All);
        var (root, child) = Root(new SkUiGrid { SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container) });
        SkUiTestHelpers.Arrange(root, Window.Width, Window.Height);
        var focus = new SkUiFocusManager(root);
        focus.SetKeyboardTop(500);
        ((SkUiGrid)root).SafeAreaEdges = softInput;
        SkUiTestHelpers.Arrange(root, Window.Width, Window.Height);
        Assert.Equal(new Rect(0, 47, 400, 500 - 47), child.Frame);
        // The keyboard closes while the bottom ignores it: turned on again, it pads for no keyboard.
        ((SkUiGrid)root).SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container);
        focus.SetKeyboardTop(double.PositiveInfinity);
        ((SkUiGrid)root).SafeAreaEdges = softInput;
        SkUiTestHelpers.Arrange(root, Window.Width, Window.Height);
        Assert.Equal(new Rect(0, 47, 400, 800 - 47 - 34), child.Frame);
    }
}
