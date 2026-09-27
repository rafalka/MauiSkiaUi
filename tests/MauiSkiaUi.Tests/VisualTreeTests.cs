using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>Drawn elements in MAUI's visual tree (Live Visual Tree / automation) and SkUiDiagnostics locating.</summary>
public class VisualTreeTests
{
    private const string DiagnosticsSwitch = "Microsoft.Maui.RuntimeFeature.EnableMauiDiagnostics";

    private static SkUiBox Box(double width = 20, double height = 10) =>
        new() { Color = Colors.Red, WidthRequest = width, HeightRequest = height, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };

    private static SkUiCoreBox CoreBox(double width = 20, double height = 10)
    {
        var box = new SkUiCoreBox();
        box.SetWidth(width).SetHeight(height).SetHorizontalAlignment(LayoutAlignment.Start).SetVerticalAlignment(LayoutAlignment.Start);
        return box;
    }

    [Fact]
    public void SkUiChildrenAreVisualChildren()
    {
        var first = Box();
        var stack = new SkUiVerticalStackLayout();
        stack.Children.Add(first);
        var root = new SkUiContentView { Content = stack };

        Assert.Same(stack, Assert.Single(((IVisualTreeElement)root).GetVisualChildren()));
        Assert.Same(first, Assert.Single(((IVisualTreeElement)stack).GetVisualChildren()));
        Assert.Same(stack, ((IVisualTreeElement)first).GetVisualParent());
    }

    [Fact]
    public void CoreTreeIsReachableThroughTheHost()
    {
        var leaf = CoreBox();
        var border = new SkUiCoreBorder().SetContent(leaf);
        var stack = new SkUiCoreVerticalStackLayout().Add(border).Add(CoreBox());
        var host = new SkUiCoreHost().SetContent(stack);
        var root = new SkUiContentView { Content = host };

        Assert.Same(stack, Assert.Single(((IVisualTreeElement)host).GetVisualChildren()));
        Assert.Equal(2, ((IVisualTreeElement)stack).GetVisualChildren().Count);
        Assert.Same(leaf, Assert.Single(((IVisualTreeElement)border).GetVisualChildren()));
        Assert.Contains(leaf, root.GetVisualTreeDescendants());

        var chain = new List<IVisualTreeElement>();
        for (var up = ((IVisualTreeElement)leaf).GetVisualParent(); up is not null; up = up.GetVisualParent())
            chain.Add(up);
        Assert.Equal(new IVisualTreeElement[] { border, stack, host, root }, chain);
    }

    [Fact]
    public void CoreChangesRaiseVisualTreeChangedWhenDiagnosticsAreEnabled()
    {
        var events = new List<(object? Parent, object Child, int Index, VisualTreeChangeType Type)>();
        var stack = new SkUiCoreVerticalStackLayout();
        var host = new SkUiCoreHost();
        // The switch and event are global: tests running in parallel raise events too (on other threads).
        void OnChanged(object? sender, VisualTreeChangeEventArgs args)
        {
            if (!ReferenceEquals(args.Parent, stack) && !ReferenceEquals(args.Parent, host))
                return;
            lock (events)
                events.Add((args.Parent, args.Child, args.ChildIndex, args.ChangeType));
        }
        AppContext.TryGetSwitch(DiagnosticsSwitch, out var previous);
        AppContext.SetSwitch(DiagnosticsSwitch, true);
        var previousCore = SkUiDiagnostics.TreeNotifications;
        SkUiDiagnostics.TreeNotifications = true;
        VisualDiagnostics.VisualTreeChanged += OnChanged;
        try
        {
            var a = CoreBox();
            var b = CoreBox();
            stack.Add(a).Add(b);
            host.SetContent(stack);
            stack.Remove(a);
            host.SetContent(null);

            Assert.Equal(
                new (object?, object, int, VisualTreeChangeType)[]
                {
                    (stack, a, 0, VisualTreeChangeType.Add),
                    (stack, b, 1, VisualTreeChangeType.Add),
                    (host, stack, 0, VisualTreeChangeType.Add),
                    (stack, a, 0, VisualTreeChangeType.Remove),
                    (host, stack, 0, VisualTreeChangeType.Remove),
                },
                events);
        }
        finally
        {
            VisualDiagnostics.VisualTreeChanged -= OnChanged;
            AppContext.SetSwitch(DiagnosticsSwitch, previous);
            SkUiDiagnostics.TreeNotifications = previousCore;
        }
    }

    [Fact]
    public void RootBoundsFollowLayoutTranslationAndScrollOffset()
    {
        var target = Box(20, 10);
        target.TranslationX = 3;
        var stack = new SkUiVerticalStackLayout { Padding = new Thickness(10, 5, 0, 0) };
        stack.Children.Add(Box(20, 100));
        stack.Children.Add(target);
        var scroll = new SkUiScrollView { Content = stack };
        SkUiTestHelpers.Arrange(scroll, 100, 50);

        Assert.Equal(new Rect(13, 105, 20, 10), SkUiDiagnostics.GetRootBounds(target));
        scroll.ScrollTo(0, 60); // content is 115 tall, viewport 50: max offset 65
        Assert.Equal(new Rect(13, 45, 20, 10), SkUiDiagnostics.GetRootBounds(target));
        Assert.Same(scroll, SkUiDiagnostics.GetSurfaceRoot(target));
        Assert.Equal(new Rect(0, 0, 100, 50), SkUiDiagnostics.GetRootBounds(scroll));
    }

    [Fact]
    public void RootBoundsOfCoreNodesIncludeTransformsAndRtl()
    {
        var scaled = CoreBox(20, 10);
        scaled.SetScale(2);
        var stack = new SkUiCoreHorizontalStackLayout().Add(CoreBox(30, 10)).Add(scaled);
        var host = new SkUiCoreHost().SetContent(stack);
        var root = new SkUiContentView { Content = host };
        SkUiTestHelpers.Arrange(root, 100, 40);

        // Scale 2 about the center of (30, 0, 20, 10).
        Assert.Equal(new Rect(20, -5, 40, 20), SkUiDiagnostics.GetRootBounds(scaled));

        root.FlowDirection = FlowDirection.RightToLeft;
        SkUiTestHelpers.Arrange(root, 100, 40);
        Assert.Equal(new Rect(40, -5, 40, 20), SkUiDiagnostics.GetRootBounds(scaled));
    }

    [Fact]
    public void HitTestFindsDeepestElementHonoringScrollAndClip()
    {
        var top = Box(40, 20);
        var lower = new SkUiCoreBox();
        lower.SetHeight(20).SetVerticalAlignment(LayoutAlignment.Start);
        var host = new SkUiCoreHost { HeightRequest = 200 }.SetContent(new SkUiCoreVerticalStackLayout().Add(CoreBox(40, 100)).Add(lower));
        var stack = new SkUiVerticalStackLayout();
        stack.Children.Add(top);
        stack.Children.Add(host);
        var scroll = new SkUiScrollView { Content = stack };
        SkUiTestHelpers.Arrange(scroll, 100, 50);

        Assert.Same(top, SkUiDiagnostics.HitTest(scroll, new Point(5, 5)));
        Assert.Null(SkUiDiagnostics.HitTest(scroll, new Point(150, 5)));

        // `lower` spans content y 120..140: below the viewport until scrolled.
        Assert.NotSame(lower, SkUiDiagnostics.HitTest(scroll, new Point(5, 45)));
        scroll.ScrollTo(0, 100);
        Assert.Same(lower, SkUiDiagnostics.HitTest(scroll, new Point(5, 25)));
        Assert.Equal(new Rect(0, 20, 100, 20), SkUiDiagnostics.GetRootBounds(lower));
    }

    [Fact]
    public void SimulateTapClicksDrawnAndCoreButtons()
    {
        var clicks = 0;
        var button = new SkUiButton { Text = "Go", WidthRequest = 60, HeightRequest = 30, HorizontalOptions = LayoutOptions.Start };
        button.Clicked += (_, _) => clicks++;
        var coreClicks = 0;
        var coreButton = new SkUiCoreButton();
        coreButton.SetText("Core").SetWidth(60).SetHeight(30).SetHorizontalAlignment(LayoutAlignment.Start).SetVerticalAlignment(LayoutAlignment.Start);
        coreButton.Clicked += (_, _) => coreClicks++;
        var stack = new SkUiVerticalStackLayout { Spacing = 10 };
        stack.Children.Add(button);
        stack.Children.Add(new SkUiCoreHost { HeightRequest = 40 }.SetContent(coreButton));
        var scroll = new SkUiScrollView { Content = stack };
        SkUiTestHelpers.Arrange(scroll, 200, 60);

        Assert.Same(button, SkUiDiagnostics.SimulateTap(button));
        Assert.Equal(1, clicks);

        // Partly below the viewport (content y 40..70, viewport 0..60): the center (y 55) is still visible.
        Assert.Same(coreButton, SkUiDiagnostics.SimulateTap(coreButton));
        Assert.Equal(1, coreClicks);
    }

    [Fact]
    public void DetachedAndUnhostedNodesHaveNoBounds()
    {
        Assert.Null(SkUiDiagnostics.GetRootBounds(CoreBox()));
        Assert.Null(SkUiDiagnostics.GetRootBounds(new object()));
        Assert.Null(SkUiDiagnostics.GetWindowBounds(Box()));
    }
}
