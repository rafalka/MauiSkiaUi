using System.ComponentModel;
using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// MAUI's <c>CascadeInputTransparent</c> and <c>IsClippedToBounds</c> on drawn containers (step R2 in
/// docs/archive/ImplementationPlan-1.2.md), on both layers: with the default, an input-transparent container lets pointers
/// through its whole subtree; without cascading, only the container itself lets them through, and its children still take
/// them.
/// </summary>
public class CascadeInputTransparentTests
{
    private const double Size = 200;
    private static readonly Point OnChild = new(25, 25);
    private static readonly Point OnContainer = new(150, 150);

    private sealed class Taps
    {
        public int Under;
        public int Container;
        public int Child;

        public (int Under, int Container, int Child) Counts => (Under, Container, Child);
    }

    private static void Tap(SkUiView root, Point position)
    {
        root.Touch(new(1, SkUiTouchAction.Pressed, position, TimeSpan.Zero));
        root.Touch(new(1, SkUiTouchAction.Released, position, TimeSpan.FromMilliseconds(50)));
    }

    /// <summary>A box under a full-size container whose 50 × 50 child sits at the top left, each counting its taps.</summary>
    private static (SkUiView Root, SkUiContentView Container, Taps Taps) DrawnTree()
    {
        var taps = new Taps();
        var under = new SkUiBox();
        under.Tapped += (_, _) => taps.Under++;
        var child = new SkUiBox { WidthRequest = 50, HeightRequest = 50, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
        child.Tapped += (_, _) => taps.Child++;
        var container = new SkUiContentView { Content = child };
        container.Tapped += (_, _) => taps.Container++;
        var root = new SkUiGrid { Children = { under, container } };
        SkUiTestHelpers.Arrange(root, Size, Size);
        return (root, container, taps);
    }

    private static (SkUiView Root, SkUiCoreContentView Container, Taps Taps) CoreTree()
    {
        var taps = new Taps();
        var under = new SkUiCoreBox();
        under.Tapped += (_, _) => taps.Under++;
        var child = new SkUiCoreBox();
        child.SetWidth(50).SetHeight(50).SetHorizontalAlignment(LayoutAlignment.Start).SetVerticalAlignment(LayoutAlignment.Start);
        child.Tapped += (_, _) => taps.Child++;
        var container = new SkUiCoreContentView().SetContent(child);
        container.Tapped += (_, _) => taps.Container++;
        var grid = new SkUiCoreGrid();
        grid.Add(under);
        grid.Add(container);
        var host = new SkUiCoreHost().SetContent(grid);
        SkUiTestHelpers.Arrange(host, Size, Size);
        return (host, container, taps);
    }

    [Fact]
    public void ACascadingTransparentContainerLetsTapsThroughItsSubtree()
    {
        var (root, container, taps) = DrawnTree();
        var (coreRoot, coreContainer, coreTaps) = CoreTree();
        Assert.True(container.CascadeInputTransparent);
        Assert.True(coreContainer.CascadeInputTransparent);

        container.InputTransparent = true;
        coreContainer.SetInputTransparent(true);
        foreach (var position in new[] { OnChild, OnContainer })
        {
            Tap(root, position);
            Tap(coreRoot, position);
        }

        Assert.Equal((2, 0, 0), taps.Counts);
        Assert.Equal((2, 0, 0), coreTaps.Counts);
    }

    [Fact]
    public void WithoutCascadingOnlyTheContainerLetsTapsThrough()
    {
        var (root, container, taps) = DrawnTree();
        var (coreRoot, coreContainer, coreTaps) = CoreTree();
        container.CascadeInputTransparent = false;
        coreContainer.SetCascadeInputTransparent(false);

        // Not transparent: the flag changes nothing.
        Tap(root, OnContainer);
        Tap(coreRoot, OnContainer);
        Assert.Equal((0, 1, 0), taps.Counts);
        Assert.Equal((0, 1, 0), coreTaps.Counts);

        container.InputTransparent = true;
        coreContainer.SetInputTransparent(true);
        Tap(root, OnChild);
        Tap(coreRoot, OnChild);
        Assert.Equal((0, 1, 1), taps.Counts);
        Assert.Equal((0, 1, 1), coreTaps.Counts);

        // The container's own tap recognizer is out: the tap reaches what is underneath.
        Tap(root, OnContainer);
        Tap(coreRoot, OnContainer);
        Assert.Equal((1, 1, 1), taps.Counts);
        Assert.Equal((1, 1, 1), coreTaps.Counts);

        // Cascading again covers the child.
        container.CascadeInputTransparent = true;
        coreContainer.SetCascadeInputTransparent(true);
        Tap(root, OnChild);
        Tap(coreRoot, OnChild);
        Assert.Equal((2, 1, 1), taps.Counts);
        Assert.Equal((2, 1, 1), coreTaps.Counts);
    }

    [Fact]
    public void ANonCascadingTransparentScrollViewDoesNotScrollButItsContentTakesTaps()
    {
        var taps = 0;
        var row = new SkUiBox { HeightRequest = 1000 };
        row.Tapped += (_, _) => taps++;
        var scroll = new SkUiScrollView { Content = row, InputTransparent = true, CascadeInputTransparent = false };
        SkUiTestHelpers.Arrange(scroll, Size, Size);

        scroll.Touch(new(1, SkUiTouchAction.Pressed, new Point(100, 150), TimeSpan.Zero));
        for (var step = 1; step <= 10; step++)
            scroll.Touch(new(1, SkUiTouchAction.Moved, new Point(100, 150 - step * 10), TimeSpan.FromMilliseconds(step * 10)));
        scroll.Touch(new(1, SkUiTouchAction.Released, new Point(100, 50), TimeSpan.FromMilliseconds(600)));
        Assert.Equal(0, scroll.ScrollY);

        Tap(scroll, new Point(100, 100));
        Assert.Equal(1, taps);
    }

    [Fact]
    public void IsClippedToBoundsIsClipToBoundsOnBothLayers()
    {
        var grid = new SkUiGrid();
        var changed = new List<string?>();
        grid.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        Assert.False(grid.IsClippedToBounds);
        Assert.Same(SkUiView.ClipToBoundsProperty, SkUiGrid.IsClippedToBoundsProperty);

        grid.SetValue(SkUiGrid.IsClippedToBoundsProperty, true);
        Assert.True(grid.ClipToBounds);
        Assert.True(grid.IsClippedToBounds);
        Assert.Contains("IsClippedToBounds", changed);

        var core = new SkUiCoreGrid();
        var coreChanged = new List<string?>();
        ((INotifyPropertyChanged)core).PropertyChanged += (_, args) => coreChanged.Add(args.PropertyName);
        Assert.False(core.IsClippedToBounds);
        core.IsClippedToBounds = true;
        Assert.True(core.ClipToBounds);
        Assert.Contains("IsClippedToBounds", coreChanged);
    }
}
