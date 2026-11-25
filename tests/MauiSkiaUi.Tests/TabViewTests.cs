using MauiSkiaUi.Rendering;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// 1.17 S7 <see cref="SkUiTabView"/>: only the first pane created, a drag towards the next creating it and both kept, a pane's
/// state surviving going out of view and back, swipes and taps selecting, swiping turned off, the strip at the top and bottom,
/// badges, disabled tabs skipped by swipes and keys, the keys and screen readers, and the indicator's scroll-linked channels
/// placing it as the UI side does (equal and content widths, right to left) with nothing re-recorded while the panes fling.
/// </summary>
public class TabViewTests
{
    private static long _pointer = 360_000;

    private static SkUiScrollView Pane(string name) =>
        new() { Content = new SkUiVerticalStackLayout { Children = { new SkUiBox { HeightRequest = 2000, Color = Colors.LightGray } } }, AutomationId = name };

    private static SkUiTabView TabView(params string[] headers)
    {
        var tabs = new SkUiTabView { FontFamily = SkUiTestHelpers.BundledFontFamily };
        foreach (var header in headers.Length == 0 ? ["Posts", "Media", "Likes"] : headers)
            tabs.AddTab(new SkUiTabItem(header, Pane(header)));
        return tabs;
    }

    private static void Run(SkUiTestSurface surface, double fromMs, double untilMs)
    {
        for (var ms = fromMs; ms <= untilMs; ms += 16)
        {
            surface.Root.AnimationClock.Tick(TimeSpan.FromMilliseconds(ms));
            surface.Frame(ms);
        }
    }

    private static void Swipe(SkUiView root, Point from, Point to, double startMs)
    {
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, from, TimeSpan.FromMilliseconds(startMs)));
        root.Touch(new(id, SkUiTouchAction.Moved, new Point((from.X * 2 + to.X) / 3, from.Y), TimeSpan.FromMilliseconds(startMs + 10)));
        root.Touch(new(id, SkUiTouchAction.Moved, to, TimeSpan.FromMilliseconds(startMs + 20)));
        root.Touch(new(id, SkUiTouchAction.Released, to, TimeSpan.FromMilliseconds(startMs + 25)));
    }

    private static void Tap(SkUiView root, Point at, double ms)
    {
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, at, TimeSpan.FromMilliseconds(ms)));
        root.Touch(new(id, SkUiTouchAction.Released, at, TimeSpan.FromMilliseconds(ms + 40)));
    }

    [Fact]
    public void OnlyTheFirstPaneIsCreatedAndADragTowardsTheNextCreatesItAndKeepsBoth()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var tabs = TabView();
        using var surface = new SkUiTestSurface(tabs, 300, 400);
        surface.Frame(0);
        Assert.True(tabs.IsPaneCreated(0));
        Assert.False(tabs.IsPaneCreated(1));

        var id = ++_pointer;
        tabs.Touch(new(id, SkUiTouchAction.Pressed, new Point(250, 200), TimeSpan.FromMilliseconds(0)));
        tabs.Touch(new(id, SkUiTouchAction.Moved, new Point(220, 200), TimeSpan.FromMilliseconds(100)));
        tabs.Touch(new(id, SkUiTouchAction.Moved, new Point(200, 200), TimeSpan.FromMilliseconds(200)));
        surface.Frame(200);
        Assert.True(tabs.IsPaneCreated(1)); // it came into view
        Assert.False(tabs.IsPaneCreated(2));
        tabs.Touch(new(id, SkUiTouchAction.Released, new Point(200, 200), TimeSpan.FromMilliseconds(600)));
        Run(surface, 600, 2000);

        Assert.Equal(0, tabs.SelectedIndex); // a short slow drag settles back
        Assert.True(tabs.IsPaneCreated(0));
        Assert.True(tabs.IsPaneCreated(1));
    }

    [Fact]
    public void APanesStateSurvivesGoingOutOfViewAndBack()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var tabs = TabView();
        var first = (SkUiScrollView)tabs.Tabs[0].Content!;
        using var surface = new SkUiTestSurface(tabs, 300, 400);
        surface.Frame(0);
        first.ScrollToAsync(0, 250, animated: false).Wait();

        tabs.SelectedIndex = 2;
        Run(surface, 16, 1000);
        Assert.Equal(600, tabs.PaneScrollViewForTests.ScrollX, 1);
        tabs.SelectedIndex = 0;
        Run(surface, 1000, 2000);

        Assert.Same(first, tabs.Tabs[0].Content);
        Assert.Equal(250, first.ScrollY, 1);
        Assert.Equal(0, tabs.PaneScrollViewForTests.ScrollX, 1);
    }

    [Fact]
    public void SwipesAndTapsSelectAndSwipingCanBeTurnedOff()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var tabs = TabView();
        var changes = new List<(int, int)>();
        tabs.SelectionChanged += (_, args) => changes.Add((args.PreviousIndex, args.CurrentIndex));
        using var surface = new SkUiTestSurface(tabs, 300, 400);
        surface.Frame(0);

        Swipe(tabs, new Point(250, 200), new Point(50, 200), 0);
        Run(surface, 30, 1500);
        Assert.Equal(1, tabs.SelectedIndex);
        Assert.Equal([(0, 1)], changes);

        Tap(tabs, tabs.HeaderBounds(2).Center, 1600);
        Assert.Equal(2, tabs.SelectedIndex);
        Run(surface, 1650, 2600);
        Assert.Equal(600, tabs.PaneScrollViewForTests.ScrollX, 1);

        tabs.IsSwipeEnabled = false;
        Swipe(tabs, new Point(50, 200), new Point(250, 200), 2700);
        Run(surface, 2730, 4000);
        Assert.Equal(2, tabs.SelectedIndex);
        Assert.Equal(600, tabs.PaneScrollViewForTests.ScrollX, 1);
    }

    [Fact]
    public void TheStripSitsAboveOrBelowThePanes()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var tabs = TabView();
        using var surface = new SkUiTestSurface(tabs, 300, 400);
        surface.Frame(0);
        var height = SkUiLook.Current.TabStripMetrics.Height;
        Assert.Equal(new Rect(0, 0, 300, height), tabs.StripScrollViewForTests.Bounds);
        Assert.Equal(new Rect(0, height, 300, 400 - height), tabs.PaneScrollViewForTests.Bounds);

        tabs.TabStripPlacement = SkUiTabStripPlacement.Bottom;
        surface.Frame(16);
        Assert.Equal(new Rect(0, 400 - height, 300, height), tabs.StripScrollViewForTests.Bounds);
        Assert.Equal(new Rect(0, 0, 300, 400 - height), tabs.PaneScrollViewForTests.Bounds);
    }

    [Fact]
    public void ADisabledTabIsSkippedBySwipesAndKeysAndTheAppCanStillSelectIt()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var tabs = TabView();
        tabs.Tabs[1].IsEnabled = false;
        using var surface = new SkUiTestSurface(tabs, 300, 400);
        surface.Frame(0);

        Swipe(tabs, new Point(250, 200), new Point(50, 200), 0);
        Run(surface, 30, 1500);
        Assert.Equal(2, tabs.SelectedIndex); // past the disabled tab
        Tap(tabs, tabs.HeaderBounds(1).Center, 1600);
        Assert.Equal(2, tabs.SelectedIndex);

        var keys = tabs.FocusManager;
        Assert.True(keys.Focus((ISkUiAccessibleNode)tabs.StripView));
        Assert.True(keys.KeyDown(SkUiKey.Left));
        Assert.Equal(0, tabs.SelectedIndex);
        Assert.True(keys.KeyDown(SkUiKey.End));
        Assert.Equal(2, tabs.SelectedIndex);
        Assert.True(keys.KeyDown(SkUiKey.Tab, SkUiKeyModifiers.Control)); // around the end, past the disabled one
        Assert.Equal(0, tabs.SelectedIndex);

        tabs.SelectedIndex = 1;
        Assert.Equal(1, tabs.SelectedIndex);
        tabs.SelectedIndex = 9;
        Assert.Equal(2, tabs.SelectedIndex); // coerced to the last tab
    }

    [Fact]
    public void TheStripIsATabListWithBadges()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var tabs = TabView();
        tabs.Tabs[1].BadgeText = "3";
        tabs.Tabs[2].AutomationId = "LikesTab";
        using var surface = new SkUiTestSurface(new SkUiContentView { Content = tabs, Background = Colors.White }, 300, 400);
        surface.Frame(0);

        var info = new SkUiSemanticsInfo();
        ((ISkUiAccessibleNode)tabs.StripView).GetSemantics(info);
        Assert.Equal("Posts", info.Value);
        Assert.Equal(["Posts", "Media, 3", "Likes"], info.Parts!.Select(static part => part.Label));
        Assert.Equal("Selected, 1 of 3", info.Parts![0].Value);
        Assert.Equal("_LikesTab", info.Parts![2].AutomationIdSuffix);
        Assert.True(((ISkUiAccessibleNode)tabs.StripView).PerformSemanticsPartAction(2, SkUiSemanticsActions.Activate));
        Assert.Equal(2, tabs.SelectedIndex);

        // The badge: a pill in the error color at the header's top end.
        var media = tabs.HeaderBounds(1);
        var badge = surface.Bitmap.GetPixel((int)media.Right - 10, (int)media.Top + 10);
        var error = SkUiColors.Error;
        Assert.Equal(new SKColor((byte)(error.Red * 255), (byte)(error.Green * 255), (byte)(error.Blue * 255)), badge.WithAlpha(255));
    }

    /// <summary>The indicator's links evaluated at <paramref name="offset"/>: its translation and clip width.</summary>
    private static (float X, float Width) IndicatorAt(SkUiTabView tabs, float offset)
    {
        var props = SkUiRenderProps.Default;
        ((ISkUiRenderable)tabs.IndicatorView).GetRenderProps(ref props);
        var values = SkUiRenderLinkValues.Evaluate(props.Links!, offset, 0);
        return (values.TranslationX, float.IsPositiveInfinity(values.ClipWidth) ? (float)tabs.IndicatorView.Width : values.ClipWidth);
    }

    [Theory]
    [InlineData(SkUiSegmentWidthMode.Equal, false)]
    [InlineData(SkUiSegmentWidthMode.Content, false)]
    [InlineData(SkUiSegmentWidthMode.Equal, true)]
    [InlineData(SkUiSegmentWidthMode.Content, true)]
    public void TheIndicatorsLinksPlaceItAsTheUiSideDoesAtThreeOffsets(SkUiSegmentWidthMode mode, bool rightToLeft)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var tabs = TabView("A", "Much longer", "Mid");
        tabs.TabWidthMode = mode;
        tabs.FlowDirection = rightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        using var surface = new SkUiTestSurface(tabs, 300, 400);
        surface.Frame(0);
        var look = SkUiLook.Current;
        Rect Expected(int index) => look.GetTabIndicatorBounds(tabs.HeaderBounds(index), 0, SkUiTabIndicatorStyle.Underline, false);
        var paneOf = rightToLeft ? (Func<int, float>)(index => 600 - 300 * index) : index => 300 * index;

        var (x0, w0) = IndicatorAt(tabs, paneOf(0));
        Assert.Equal(Expected(0).X, x0, 2);
        Assert.Equal(Expected(0).Width, w0, 2);
        var (x1, w1) = IndicatorAt(tabs, paneOf(1));
        Assert.Equal(Expected(1).X, x1, 2);
        Assert.Equal(Expected(1).Width, w1, 2);
        var (xMid, wMid) = IndicatorAt(tabs, (paneOf(0) + paneOf(1)) / 2);
        Assert.Equal((Expected(0).X + Expected(1).X) / 2, xMid, 2);
        Assert.Equal((Expected(0).Width + Expected(1).Width) / 2, wMid, 2);

        // The UI side's own placement at its current offset (the selected pane) is the same channel evaluated there.
        var props = SkUiRenderProps.Default;
        ((ISkUiRenderable)tabs.IndicatorView).GetRenderProps(ref props);
        Assert.Equal(Expected(0).X, props.EffectiveTranslationX, 2);
    }

    [Fact]
    public void NothingIsRecordedAgainWhileThePanesFling()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var tabs = TabView();
        using var surface = new SkUiTestSurface(tabs, 300, 400);
        surface.Frame(0);
        // Every pane created first (they are recorded once).
        tabs.SelectedIndex = 2;
        Run(surface, 16, 1000);
        tabs.SelectedIndex = 0;
        Run(surface, 1000, 2000);

        Swipe(tabs, new Point(250, 200), new Point(50, 200), 2000);
        surface.Frame(2030);
        var recorded = surface.RecordedPictures;
        for (var ms = 2046d; ms <= 2200; ms += 16)
            surface.Frame(ms); // mid-fling: the selection has not settled
        Assert.True(tabs.PaneScrollViewForTests.IsScrolling, "still flinging");
        Assert.Equal(recorded, surface.RecordedPictures);
    }

    /// <summary>The bytes allocated over the middle frames of a fling of <paramref name="root"/> (after two warm-up swipes).</summary>
    private static long FlingAllocations(SkUiView root)
    {
        using var surface = new SkUiTestSurface(root, 300, 400);
        surface.Frame(0);
        for (var round = 0; round < 2; round++)
        {
            Swipe(root, new Point(250, 200), new Point(50, 200), 1000 * round);
            for (var ms = 1000d * round + 30; ms < 1000 * round + 900; ms += 16)
                surface.Frame(ms);
        }
        Swipe(root, new Point(250, 200), new Point(50, 200), 3000);
        surface.Frame(3030);
        surface.Frame(3046);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var ms = 3062d; ms <= 3200; ms += 16)
            surface.Frame(ms);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [Fact]
    public void AFlingAllocatesNothingBeyondThePlainSnappingScrollViews()
    {
        var tabs = new SkUiTabView();
        for (var index = 0; index < 4; index++)
            tabs.AddTab(new SkUiTabItem($"Tab {index}", new SkUiBox { Color = Colors.Gray }));
        var row = new SkUiHorizontalStackLayout();
        for (var index = 0; index < 4; index++)
            row.Children.Add(new SkUiBox { WidthRequest = 300, HeightRequest = 400, Color = Colors.Gray });
        var scroll = new SkUiScrollView { Orientation = ScrollOrientation.Horizontal, SnapPointsType = SnapPointsType.MandatorySingle, Content = row };

        // The snap spring itself is the scroller's; the panes, the strip and the indicator add nothing per frame.
        Assert.Equal(FlingAllocations(scroll), FlingAllocations(tabs));
    }

    private static readonly Color[] PaneColors = [Colors.Red, Colors.Green, Colors.Blue, Colors.Yellow];

    private static SkUiTabView ColoredTabs()
    {
        var tabs = new SkUiTabView { FontFamily = SkUiTestHelpers.BundledFontFamily };
        for (var index = 0; index < PaneColors.Length; index++)
            tabs.AddTab(new SkUiTabItem($"T{index}", new SkUiBox { Color = PaneColors[index] }));
        return tabs;
    }

    private static SKColor Sk(Color color) => new((byte)(color.Red * 255), (byte)(color.Green * 255), (byte)(color.Blue * 255));

    [Theory]
    [InlineData("code")]
    [InlineData("tap")]
    [InlineData("swipe")]
    [InlineData("jump")]
    public void ATabSelectedForTheFirstTimeDrawsItsPaneHoweverItWasSelected(string how)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var tabs = ColoredTabs();
        using var surface = new SkUiTestSurface(tabs, 400, 200);
        surface.Frame(0);
        Assert.Equal(Sk(Colors.Red), surface.Bitmap.GetPixel(200, 120));
        var target = how == "jump" ? 3 : 1;
        switch (how)
        {
            case "tap": Tap(tabs, tabs.HeaderBounds(1).Center, 0); break;
            case "swipe": Swipe(tabs, new Point(350, 120), new Point(50, 120), 0); break;
            default: tabs.SelectedIndex = target; break; // "jump": past tabs never shown
        }
        Run(surface, 50, 1500);

        Assert.Equal(target, tabs.SelectedIndex);
        Assert.Equal(Sk(PaneColors[target]), surface.Bitmap.GetPixel(200, 120));
    }

    [Theory]
    [InlineData(SkUiSegmentWidthMode.Equal, false)]
    [InlineData(SkUiSegmentWidthMode.Content, false)]
    [InlineData(SkUiSegmentWidthMode.Equal, true)]
    [InlineData(SkUiSegmentWidthMode.Content, true)]
    public void TheIndicatorIsDrawnUnderTheSelectedHeader(SkUiSegmentWidthMode mode, bool rightToLeft)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var tabs = TabView("A", "Longer", "Mid");
        tabs.TabWidthMode = mode;
        tabs.IndicatorColor = Colors.Red;
        tabs.FlowDirection = rightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        using var surface = new SkUiTestSurface(new SkUiContentView { Content = tabs, Background = Colors.White }, 300, 200);
        surface.Frame(0);
        var time = 100d;
        foreach (var selected in new[] { 0, 2, 1 })
        {
            tabs.SelectedIndex = selected;
            Run(surface, time, time + 1400);
            time += 2000;
            var header = tabs.HeaderBounds(selected);
            var (first, last) = (-1, -1);
            for (var x = 0; x < 300; x++)
                if (surface.Bitmap.GetPixel(x, (int)header.Bottom - 1) == SKColors.Red)
                    (first, last) = (first < 0 ? x : first, x);
            // Device pixels: fractional edges are blended.
            Assert.InRange(first, header.Left - 1, header.Left + 1);
            Assert.InRange(last + 1, header.Right - 1, header.Right + 1);
        }
    }

    private static List<SkUiSemanticsNode> SemanticNodes(SkUiView root)
    {
        var nodes = new List<SkUiSemanticsNode>();
        void Walk(SkUiSemanticsNode node)
        {
            nodes.Add(node);
            foreach (var child in node.Children)
                Walk(child);
        }
        Walk(SkUiSemanticsTree.Build(root).Root);
        return nodes;
    }

    [Fact]
    public void ScreenReadersReadTheStripThenTheSelectedPanesContentsUnderItsTabsName()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        SkUiLabel Text(string text) => new() { Text = text, FontFamily = SkUiTestHelpers.BundledFontFamily };
        var tabs = new SkUiTabView { FontFamily = SkUiTestHelpers.BundledFontFamily };
        tabs.AddTab(new SkUiTabItem("Posts", new SkUiVerticalStackLayout { Children = { Text("First text"), new SkUiButton { Text = "Press" } } }));
        tabs.AddTab(new SkUiTabItem("Media", Text("Second text")));
        using var surface = new SkUiTestSurface(tabs, 300, 200);
        surface.Frame(0);

        var nodes = SemanticNodes(tabs);
        var labels = nodes.Select(static node => node.Label).Where(static label => !string.IsNullOrEmpty(label)).ToList();
        Assert.Equal(["Posts", "Media", "Posts", "First text", "Press"], labels); // the two headers, the panes' container, its contents
        Assert.Equal(SkUiSemanticsRole.ScrollView, nodes.Single(static node => node is { Label: "Posts", PartIndex: < 0 }).Role);

        tabs.SelectedIndex = 1;
        Run(surface, 50, 1500);
        labels = SemanticNodes(tabs).Select(static node => node.Label).Where(static label => !string.IsNullOrEmpty(label)).ToList()!;
        Assert.Equal(["Posts", "Media", "Media", "Second text"], labels); // the pane out of view is not read

        tabs.TabStripPlacement = SkUiTabStripPlacement.Bottom;
        surface.Frame(2000);
        labels = SemanticNodes(tabs).Select(static node => node.Label).Where(static label => !string.IsNullOrEmpty(label)).ToList()!;
        Assert.Equal(["Media", "Second text", "Posts", "Media"], labels);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ScreenReadersPageWholeTabsAndArrowKeysInsideAPaneLeaveThePanesInPlace(bool rightToLeft)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var tabs = new SkUiTabView { FlowDirection = rightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight };
        var button = new SkUiButton { Text = "Press" };
        tabs.AddTab(new SkUiTabItem("A", button));
        tabs.AddTab(new SkUiTabItem("B", new SkUiBox()) { IsEnabled = false });
        tabs.AddTab(new SkUiTabItem("C", new SkUiBox()));
        using var surface = new SkUiTestSurface(tabs, 300, 200);
        surface.Frame(0);
        var scroll = tabs.PaneScrollViewForTests;
        var start = scroll.ScrollX;
        Assert.True(tabs.FocusManager.Focus(button));

        var time = 50d;
        foreach (var key in new[] { SkUiKey.Right, SkUiKey.Left, SkUiKey.Down, SkUiKey.Up, SkUiKey.Home, SkUiKey.End })
        {
            tabs.FocusManager.KeyDown(key);
            Run(surface, time, time + 600);
            time += 700;
        }
        Assert.Equal((0, start), (tabs.SelectedIndex, scroll.ScrollX));

        var next = rightToLeft ? SkUiSemanticsActions.ScrollBackward : SkUiSemanticsActions.ScrollForward; // towards smaller offsets
        var info = new SkUiSemanticsInfo();
        ((ISkUiAccessibleNode)scroll).GetSemantics(info);
        Assert.Equal(next, info.Actions & (SkUiSemanticsActions.ScrollForward | SkUiSemanticsActions.ScrollBackward)); // the first tab: one way only
        Assert.True(((ISkUiAccessibleNode)scroll).PerformSemanticsAction(next));
        Run(surface, time, time + 1500);
        Assert.Equal(2, tabs.SelectedIndex); // past the disabled tab
        Assert.Equal(rightToLeft ? 0 : 600, scroll.ScrollX, 1);
    }

    [Fact]
    public void WithContentWidthsTheSelectedHeaderIsInViewFromTheFirstLayout()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var tabs = TabView([.. Enumerable.Range(0, 12).Select(static index => $"Section {index}")]);
        tabs.TabWidthMode = SkUiSegmentWidthMode.Content;
        tabs.SelectedIndex = 9;
        using var surface = new SkUiTestSurface(tabs, 300, 200);
        surface.Frame(0);
        surface.Frame(16);

        var header = tabs.HeaderBounds(9);
        Assert.True(header.Right > 300, "the strip overflows");
        Assert.InRange(tabs.StripScrollViewForTests.ScrollX, header.Right - 300 - 0.5, header.Left + 0.5);
        Assert.Equal(2700, tabs.PaneScrollViewForTests.ScrollX, 1);
    }
}

/// <summary>1.17 S7: native views in a tab view's panes (with the other tests that create overlays).</summary>
[Collection(nameof(OverlayScrollTests))]
public class TabViewNativeViewTests
{
    [Fact]
    public void ANativeViewInAPaneOutOfViewIsClippedAway()
    {
        var native = new SkUiMauiContentView { Content = new BoxView() };
        var tabs = new SkUiTabView();
        tabs.AddTab(new SkUiTabItem("Native", native));
        tabs.AddTab(new SkUiTabItem("Other", new SkUiBox()));
        using var surface = new SkUiTestSurface(tabs, 400, 200);
        surface.Frame(0);
        Rect Visible() => native.ComputeRootRelativeFrame().Intersect(native.ComputeRootRelativeClip());
        Assert.Equal(400, Visible().Width);

        tabs.SelectedIndex = 1;
        for (var ms = 50d; ms < 1500; ms += 16)
            surface.Frame(ms);
        Assert.Equal(0, Visible().Width);
    }
}
