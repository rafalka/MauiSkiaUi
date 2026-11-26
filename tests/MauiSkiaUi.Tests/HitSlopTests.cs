using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// 1.18 A4 (issue #134): <c>HitSlop</c> and <c>MinimumTouchTargetSize</c> on views and Core nodes. A node is pressed past its
/// rectangle where no other node is under the finger, or only a container of it; an exact hit on another node wins; slops
/// are cut by the clips of the node's containers; the nearest of two slops wins; the semantics frame grows the same way.
/// </summary>
public sealed class HitSlopTests
{
    private static long _pointer = 160_000;

    private static void Tap(SkUiView root, Point at)
    {
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, at, TimeSpan.FromSeconds(1)));
        root.Touch(new(id, SkUiTouchAction.Released, at, TimeSpan.FromSeconds(1.02)));
    }

    /// <summary>A tappable 20 × 20 box at (<paramref name="x"/>, <paramref name="y"/>) counting its taps.</summary>
    private static SkUiBox Box(double x, double y, List<string> taps, string name)
    {
        var box = new SkUiBox
        {
            WidthRequest = 20, HeightRequest = 20, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(x, y, 0, 0), Color = Colors.Black
        };
        box.Tapped += (_, _) => taps.Add(name);
        return box;
    }

    private static SkUiGrid Root(params ISkUiView[] children)
    {
        var root = new SkUiGrid();
        foreach (var child in children)
            root.Children.Add(child);
        SkUiTestHelpers.Arrange(root, 200, 200);
        return root;
    }

    [Fact]
    public void AViewIsPressedInsideItsSlopAndNotPastIt()
    {
        var taps = new List<string>();
        var box = Box(40, 40, taps, "box");
        var root = Root(box);
        Tap(root, new Point(35, 50));
        Assert.Empty(taps);

        box.HitSlop = new Thickness(12);
        Tap(root, new Point(35, 50));
        Tap(root, new Point(50, 71));
        Assert.Equal(["box", "box"], taps);
        Tap(root, new Point(27, 50)); // 13 past its left edge
        Assert.Equal(2, taps.Count);

        // A side of its own.
        box.HitSlop = new Thickness(0, 0, 30, 0);
        Tap(root, new Point(35, 50));
        Tap(root, new Point(85, 50));
        Assert.Equal(3, taps.Count);
    }

    [Fact]
    public void AMinimumTouchTargetGrowsSmallViewsEvenly()
    {
        var taps = new List<string>();
        var box = Box(40, 40, taps, "box");
        box.MinimumTouchTargetSize = 44; // 12 on each side of 20
        var root = Root(box);
        Tap(root, new Point(29, 50));
        Tap(root, new Point(50, 71));
        Assert.Equal(2, taps.Count);
        Tap(root, new Point(27, 50));
        Assert.Equal(2, taps.Count);
        Assert.Equal(new Thickness(12), ((ISkUiInputNode)box).HitSlop);

        // A larger slop side wins; a view as large as the minimum gets none from it.
        box.HitSlop = new Thickness(20, 0, 0, 0);
        Assert.Equal(new Thickness(20, 12, 12, 12), ((ISkUiInputNode)box).HitSlop);
        box.HitSlop = default;
        box.WidthRequest = box.HeightRequest = 50;
        SkUiTestHelpers.Arrange(root, 200, 200);
        Assert.Equal(default, ((ISkUiInputNode)box).HitSlop);
    }

    [Fact]
    public void AnExactHitOnAnotherViewWinsAndTheNearestSlopWinsBetweenSlops()
    {
        var taps = new List<string>();
        var left = Box(40, 40, taps, "left");
        var right = Box(70, 40, taps, "right"); // 10 DIPs apart
        left.HitSlop = new Thickness(20);
        var root = Root(left, right);
        Tap(root, new Point(72, 50)); // on the right box, inside the left one's slop
        Assert.Equal(["right"], taps);
        Tap(root, new Point(68, 50)); // the gap: only the left one's slop
        Assert.Equal(["right", "left"], taps);

        right.HitSlop = new Thickness(20);
        Tap(root, new Point(63, 50)); // 3 past the left box, 7 before the right one
        Tap(root, new Point(67, 50)); // 7 past the left box, 3 before the right one
        Assert.Equal(["right", "left", "left", "right"], taps);
    }

    [Fact]
    public void ASlopReachesPastNonClippingContainersAndBeatsATappableContainer()
    {
        var taps = new List<string>();
        var box = Box(0, 0, taps, "box");
        box.HitSlop = new Thickness(15);
        var row = new SkUiGrid
        {
            WidthRequest = 20, HeightRequest = 20, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(60, 60, 0, 0), Children = { box }
        };
        var root = Root(row);
        Tap(root, new Point(50, 70)); // outside the row and the box, inside the slop
        Assert.Equal(["box"], taps);

        // A tappable container under the finger is not an exact hit on another view: the box inside it wins.
        var card = new SkUiBorder { Padding = new Thickness(30), StrokeThickness = 0, Content = Box(0, 0, taps, "inner") };
        ((SkUiBox)card.Content!).HitSlop = new Thickness(15);
        card.Tapped += (_, _) => taps.Add("card");
        var cardRoot = Root(card);
        Tap(cardRoot, new Point(25, 40)); // the inner box is at (30, 30)
        Assert.Equal("inner", taps[^1]);
        Tap(cardRoot, new Point(5, 5));
        Assert.Equal("card", taps[^1]);
    }

    [Fact]
    public void ASlopIsCutByAScrollersViewportAndBlockedByADisabledView()
    {
        var taps = new List<string>();
        var box = Box(0, 0, taps, "box");
        box.HitSlop = new Thickness(15);
        var scroll = new SkUiScrollView
        {
            HeightRequest = 100, VerticalOptions = LayoutOptions.Start, Margin = new Thickness(40, 50, 0, 0), Content = new SkUiVerticalStackLayout { Children = { box, new SkUiBox { HeightRequest = 500 } } }
        };
        var root = Root(scroll);
        Tap(root, new Point(50, 45)); // above the viewport, inside the slop
        Assert.Empty(taps);
        Tap(root, new Point(50, 72)); // below the box, inside the viewport and the slop
        Assert.Equal(["box"], taps);

        var disabled = Box(40, 75, taps, "disabled");
        disabled.IsEnabled = false;
        var other = Box(40, 40, taps, "other");
        other.HitSlop = new Thickness(15);
        var blockedRoot = Root(other, disabled);
        Tap(blockedRoot, new Point(50, 78)); // on the disabled box, inside the other's slop
        Assert.Equal(["box"], taps);
    }

    [Fact]
    public void CoreNodesHaveSlopsToo()
    {
        var taps = 0;
        var node = new SkUiCoreBox().SetColor(Colors.Black);
        node.SetWidth(20).SetHeight(20).SetHorizontalAlignment(LayoutAlignment.Center).SetVerticalAlignment(LayoutAlignment.Center);
        node.Tapped += (_, _) => taps++;
        var host = new SkUiCoreHost();
        host.SetContent(node);
        SkUiTestHelpers.Arrange(host, 100, 100);
        Tap(host, new Point(35, 50));
        Assert.Equal(0, taps);
        node.SetMinimumTouchTargetSize(44);
        Tap(host, new Point(35, 50));
        Assert.Equal(1, taps);
        node.SetMinimumTouchTargetSize(0).SetHitSlop(new Thickness(0, 0, 0, 20));
        Tap(host, new Point(35, 50));
        Tap(host, new Point(50, 75));
        Assert.Equal(2, taps);
        Assert.Throws<ArgumentOutOfRangeException>(() => node.SetHitSlop(new Thickness(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => node.SetMinimumTouchTargetSize(-1));
    }

    [Fact]
    public void TheSemanticsFrameGrowsWithTheSlop()
    {
        var taps = new List<string>();
        var box = Box(40, 40, taps, "box");
        SemanticProperties.SetDescription(box, "Next");
        var root = Root(box);
        SKRectAssert(new Rect(40, 40, 20, 20), Frame(root));
        box.HitSlop = new Thickness(12);
        SKRectAssert(new Rect(28, 28, 44, 44), Frame(root));

        static Rect Frame(SkUiView root)
        {
            var node = SkUiSemanticsTree.Build(root).Flatten().Single(n => n.Label == "Next");
            return node.Bounds;
        }

        static void SKRectAssert(Rect expected, Rect actual)
        {
            Assert.Equal(expected.X, actual.X, 0.01);
            Assert.Equal(expected.Y, actual.Y, 0.01);
            Assert.Equal(expected.Width, actual.Width, 0.01);
            Assert.Equal(expected.Height, actual.Height, 0.01);
        }
    }

    [Fact]
    public void InvalidValuesAreIgnoredByPropertiesAndRejectedBySetters()
    {
        var view = new SkUiBox { HitSlop = new Thickness(4), MinimumTouchTargetSize = 44 };
        view.HitSlop = new Thickness(-1);
        view.MinimumTouchTargetSize = double.NaN;
        Assert.Equal(new Thickness(4), view.HitSlop);
        Assert.Equal(44, view.MinimumTouchTargetSize);
        Assert.Throws<ArgumentOutOfRangeException>(() => view.SetHitSlop(new Thickness(double.PositiveInfinity)));
        Assert.Throws<ArgumentOutOfRangeException>(() => view.SetMinimumTouchTargetSize(-2));
    }
}
