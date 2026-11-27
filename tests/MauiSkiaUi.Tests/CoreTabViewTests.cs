using MauiSkiaUi.Core;
using MauiSkiaUi.Rendering;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// 1.17 S8 <see cref="SkUiCoreTabView"/>: drawn the same as <see cref="SkUiTabView"/> (pixels compared at rest and mid-swipe, in
/// both width modes, right to left, every indicator), and what its tests check that applies: panes created when they come into
/// view and kept with their state, swipes, taps and code selecting, the tabs changing, disabled tabs, keys, screen readers,
/// the indicator's place, and nothing re-recorded or allocated while the panes fling.
/// </summary>
public class CoreTabViewTests
{
    private static long _pointer = 390_000;

    private static readonly Color[] PaneColors = [Colors.Red, Colors.Green, Colors.Blue, Colors.Yellow];

    private static SkUiCoreTabView TabView(params string[] headers)
    {
        var tabs = new SkUiCoreTabView().SetFontFamily(SkUiTestHelpers.BundledFontFamily);
        foreach (var header in headers.Length == 0 ? ["Posts", "Media", "Likes"] : headers)
        {
            var box = new SkUiCoreBox().SetColor(Colors.LightGray);
            box.SetHeight(2000);
            tabs.AddTab(new SkUiCoreTabItem(header, new SkUiCoreScrollView().SetContent(box)));
        }
        return tabs;
    }

    private static SkUiCoreTabView ColoredTabs()
    {
        var tabs = new SkUiCoreTabView().SetFontFamily(SkUiTestHelpers.BundledFontFamily);
        for (var index = 0; index < PaneColors.Length; index++)
        {
            var color = PaneColors[index];
            tabs.AddTab(new SkUiCoreTabItem($"T{index}", () => new SkUiCoreBox().SetColor(color)));
        }
        return tabs;
    }

    private static SkUiCoreHost Host(SkUiCoreNode node, bool rightToLeft = false) =>
        new SkUiCoreHost { FlowDirection = rightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight, Background = Colors.White }.SetContent(node);

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

    private static SKColor Sk(Color color) => new((byte)(color.Red * 255), (byte)(color.Green * 255), (byte)(color.Blue * 255));

    #region Both layers

    public static TheoryData<SkUiSegmentWidthMode, SkUiTabIndicatorStyle, SkUiTabStripPlacement, bool> Looks()
    {
        var data = new TheoryData<SkUiSegmentWidthMode, SkUiTabIndicatorStyle, SkUiTabStripPlacement, bool>();
        foreach (var mode in Enum.GetValues<SkUiSegmentWidthMode>())
            foreach (var style in Enum.GetValues<SkUiTabIndicatorStyle>())
                foreach (var rightToLeft in new[] { false, true })
                    data.Add(mode, style, rightToLeft ? SkUiTabStripPlacement.Bottom : SkUiTabStripPlacement.Top, rightToLeft);
        data.Add(SkUiSegmentWidthMode.Equal, SkUiTabIndicatorStyle.Underline, SkUiTabStripPlacement.Top, true);
        return data;
    }

    /// <summary>At rest, mid-drag, and at rest on the last tab: every pixel is the same, with a badge, a dot and a disabled tab.</summary>
    [Theory]
    [MemberData(nameof(Looks))]
    public void BothLayersDrawTheSame(SkUiSegmentWidthMode mode, SkUiTabIndicatorStyle style, SkUiTabStripPlacement placement, bool rightToLeft)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        string[] headers = ["A", "Much longer", "Mid", "Last header"];
        var skui = new SkUiTabView { FontFamily = SkUiTestHelpers.BundledFontFamily, TabWidthMode = mode, IndicatorStyle = style, TabStripPlacement = placement };
        var core = new SkUiCoreTabView().SetFontFamily(SkUiTestHelpers.BundledFontFamily).SetTabWidthMode(mode).SetIndicatorStyle(style).SetTabStripPlacement(placement);
        for (var index = 0; index < headers.Length; index++)
        {
            skui.AddTab(new SkUiTabItem(headers[index], new SkUiBox { Color = PaneColors[index] }));
            core.AddTab(new SkUiCoreTabItem(headers[index], new SkUiCoreBox().SetColor(PaneColors[index])));
        }
        skui.Tabs[1].BadgeText = core.Tabs[1].BadgeText = "12";
        skui.Tabs[3].BadgeText = core.Tabs[3].BadgeText = "";
        skui.Tabs[2].IsEnabled = core.Tabs[2].IsEnabled = false;
        var skuiRoot = new SkUiContentView { Content = skui, Background = Colors.White, FlowDirection = rightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight };
        var coreRoot = Host(core, rightToLeft);
        using var skuiSurface = new SkUiTestSurface(skuiRoot, 300, 160);
        using var coreSurface = new SkUiTestSurface(coreRoot, 300, 160);
        skuiSurface.Frame(0);
        coreSurface.Frame(0);
        Compare("at rest");
        Assert.True(Ink() > 800, $"only {Ink()} drawn pixels in the strip: compare a drawn control");

        var id = ++_pointer;
        foreach (var root in new SkUiView[] { skuiRoot, coreRoot })
        {
            var (from, to) = rightToLeft ? (60, 170) : (240, 130);
            root.Touch(new(id, SkUiTouchAction.Pressed, new Point(from, 90), TimeSpan.Zero));
            root.Touch(new(id, SkUiTouchAction.Moved, new Point((from + to) / 2d, 90), TimeSpan.FromMilliseconds(100)));
            root.Touch(new(id, SkUiTouchAction.Moved, new Point(to, 90), TimeSpan.FromMilliseconds(200)));
        }
        skuiSurface.Frame(200);
        coreSurface.Frame(200);
        Assert.NotEqual(Sk(PaneColors[0]), coreSurface.Bitmap.GetPixel(rightToLeft ? 20 : 280, 90)); // mid-swipe: the second pane shows
        Compare("mid-swipe");
        foreach (var root in new SkUiView[] { skuiRoot, coreRoot })
            root.Touch(new(id, SkUiTouchAction.Cancelled, new Point(150, 90), TimeSpan.FromMilliseconds(400)));
        Run(skuiSurface, 400, 2000);
        Run(coreSurface, 400, 2000);

        skui.SelectedIndex = core.SelectedIndex = 3;
        Run(skuiSurface, 2100, 4000);
        Run(coreSurface, 2100, 4000);
        Assert.Equal(Sk(PaneColors[3]), coreSurface.Bitmap.GetPixel(150, 90));
        Compare("on the last tab");

        int Ink()
        {
            var strip = new Rect(0, placement == SkUiTabStripPlacement.Top ? 0 : 160 - SkUiLook.Current.TabStripMetrics.Height, 300, SkUiLook.Current.TabStripMetrics.Height);
            var background = coreSurface.Bitmap.GetPixel(1, (int)strip.Top + 1);
            var ink = 0;
            for (var y = (int)strip.Top; y < strip.Bottom; y++)
                for (var x = 0; x < 300; x++)
                    if (coreSurface.Bitmap.GetPixel(x, y) != background)
                        ink++;
            return ink;
        }

        void Compare(string when)
        {
            for (var index = 0; index < headers.Length; index++)
                Assert.Equal(skui.HeaderBounds(index), core.HeaderBounds(index));
            var differing = 0;
            for (var y = 0; y < 160; y++)
                for (var x = 0; x < 300; x++)
                    if (skuiSurface.Bitmap.GetPixel(x, y) != coreSurface.Bitmap.GetPixel(x, y))
                        differing++;
            Assert.True(differing == 0, $"{differing} pixels differ {when}");
        }
    }

    [Fact]
    public void BothLayersMeasureTheSame()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var skui = new SkUiTabView { FontFamily = SkUiTestHelpers.BundledFontFamily };
        skui.AddTab(new SkUiTabItem("A", new SkUiBox { HeightRequest = 120 }));
        var box = new SkUiCoreBox();
        box.SetHeight(120);
        var core = new SkUiCoreTabView().SetFontFamily(SkUiTestHelpers.BundledFontFamily).AddTab(new SkUiCoreTabItem("A", box));

        var expected = ((IView)skui).Measure(300, double.PositiveInfinity);
        Assert.Equal(new Size(300, 120 + SkUiLook.Current.TabStripMetrics.Height), expected);
        Assert.Equal(expected, core.Measure(300, double.PositiveInfinity));
        expected = ((IView)skui).Measure(double.PositiveInfinity, 300);
        Assert.True(double.IsFinite(expected.Width) && expected.Width > 0, $"a width of its own: {expected}");
        Assert.Equal(expected, core.Measure(double.PositiveInfinity, 300));
        Assert.Equal(((IView)new SkUiTabView()).Measure(300, 200), new SkUiCoreTabView().Measure(300, 200)); // no tabs
    }

    #endregion

    #region Panes

    [Fact]
    public void FactoriesRunOnceWhenTheirPaneFirstComesIntoView()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var made = new int[3];
        var tabs = new SkUiCoreTabView().SetFontFamily(SkUiTestHelpers.BundledFontFamily);
        for (var index = 0; index < 3; index++)
        {
            var at = index;
            tabs.AddTab(new SkUiCoreTabItem($"T{index}", () => { made[at]++; return new SkUiCoreBox(); }));
        }
        var root = Host(tabs);
        using var surface = new SkUiTestSurface(root, 300, 400);
        surface.Frame(0);
        Assert.Equal([1, 0, 0], made);

        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, new Point(250, 200), TimeSpan.FromMilliseconds(0)));
        root.Touch(new(id, SkUiTouchAction.Moved, new Point(220, 200), TimeSpan.FromMilliseconds(100)));
        root.Touch(new(id, SkUiTouchAction.Moved, new Point(200, 200), TimeSpan.FromMilliseconds(200)));
        surface.Frame(200);
        Assert.Equal([1, 1, 0], made);
        root.Touch(new(id, SkUiTouchAction.Released, new Point(200, 200), TimeSpan.FromMilliseconds(600)));
        Run(surface, 600, 2000);
        Assert.Equal(0, tabs.SelectedIndex); // a short slow drag settles back

        tabs.SetSelectedIndex(1);
        Run(surface, 2100, 3200);
        tabs.SetSelectedIndex(0);
        Run(surface, 3300, 4400);
        Assert.Equal([1, 1, 0], made);
    }

    [Theory]
    [InlineData("code")]
    [InlineData("tap")]
    [InlineData("swipe")]
    [InlineData("jump")]
    public void ATabSelectedForTheFirstTimeDrawsItsPaneHoweverItWasSelected(string how)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var tabs = ColoredTabs();
        var root = Host(tabs);
        using var surface = new SkUiTestSurface(root, 400, 200);
        surface.Frame(0);
        Assert.Equal(Sk(Colors.Red), surface.Bitmap.GetPixel(200, 120));
        var target = how == "jump" ? 3 : 1;
        switch (how)
        {
            case "tap": Tap(root, tabs.HeaderBounds(1).Center, 0); break;
            case "swipe": Swipe(root, new Point(350, 120), new Point(50, 120), 0); break;
            default: tabs.SetSelectedIndex(target); break; // "jump": past tabs never shown
        }
        Run(surface, 50, 1500);

        Assert.Equal(target, tabs.SelectedIndex);
        Assert.Equal(Sk(PaneColors[target]), surface.Bitmap.GetPixel(200, 120));
    }

    [Fact]
    public async Task APanesStateSurvivesAndItsContentCanBeReplaced()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var tabs = TabView();
        var first = (SkUiCoreScrollView)tabs.Tabs[0].Content!;
        using var surface = new SkUiTestSurface(Host(tabs), 300, 400);
        surface.Frame(0);
        await first.ScrollToAsync(0, 250, animated: false);

        tabs.SetSelectedIndex(2);
        Run(surface, 16, 1000);
        Assert.Equal(600, tabs.PaneScrollViewForTests.ScrollX, 1);
        tabs.SetSelectedIndex(0);
        Run(surface, 1000, 2000);
        Assert.Equal(250, first.ScrollY, 1);
        Assert.Equal(0, tabs.PaneScrollViewForTests.ScrollX, 1);

        tabs.Tabs[0].SetContent(new SkUiCoreBox().SetColor(Colors.Purple));
        surface.Frame(2100);
        Assert.Equal(Sk(Colors.Purple), surface.Bitmap.GetPixel(150, 200));
        Assert.Null(first.Parent); // given back
    }

    #endregion

    #region Selection

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ASwipeSelectsItsTabWhenReleasedNotWhenThePanesComeToRest(bool rightToLeft)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var tabs = TabView();
        var changes = new List<(int, int)>();
        tabs.SelectionChanged += (_, args) => changes.Add((args.PreviousIndex, args.CurrentIndex));
        var root = Host(tabs, rightToLeft);
        using var surface = new SkUiTestSurface(root, 300, 400);
        surface.Frame(0);

        // The next tab is to the right, or to the left right to left.
        Swipe(root, new Point(rightToLeft ? 50 : 250, 200), new Point(rightToLeft ? 250 : 50, 200), 0);
        Run(surface, 30, 46);
        var controller = ((ISkUiScrollHost)tabs.PaneScrollViewForTests).Scroller;
        Assert.True(controller.IsMoving);
        Assert.Equal(1, tabs.SelectedIndex);
        Assert.True(tabs.Tabs[1].IsSelected);
        Assert.Equal([(0, 1)], changes);

        Run(surface, 62, 1500);
        Assert.False(controller.IsMoving);
        Assert.Equal(300, tabs.PaneScrollViewForTests.ScrollX, 1); // the middle tab, mirrored or not
        Assert.Equal([(0, 1)], changes);
    }

    [Fact]
    public void SwipesAndTapsSelectAndSwipingCanBeTurnedOff()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var tabs = TabView();
        var changes = new List<(int, int)>();
        var notified = 0;
        tabs.SelectionChanged += (_, args) => changes.Add((args.PreviousIndex, args.CurrentIndex));
        tabs.PropertyChanged += (_, args) => notified += args.PropertyName == nameof(SkUiCoreTabView.SelectedIndex) ? 1 : 0;
        var root = Host(tabs);
        using var surface = new SkUiTestSurface(root, 300, 400);
        surface.Frame(0);

        Swipe(root, new Point(250, 200), new Point(50, 200), 0);
        Run(surface, 30, 1500);
        Assert.Equal(1, tabs.SelectedIndex);
        Tap(root, tabs.HeaderBounds(2).Center, 1600);
        Assert.Equal(2, tabs.SelectedIndex);
        Run(surface, 1650, 2600);
        Assert.Equal(600, tabs.PaneScrollViewForTests.ScrollX, 1);
        Assert.Equal([(0, 1), (1, 2)], changes);
        Assert.Equal(2, notified);

        tabs.SetIsSwipeEnabled(false);
        Swipe(root, new Point(50, 200), new Point(250, 200), 2700);
        Run(surface, 2730, 4000);
        Assert.Equal(2, tabs.SelectedIndex);
        Assert.Equal(600, tabs.PaneScrollViewForTests.ScrollX, 1);
    }

    [Fact]
    public void ADisabledTabIsSkippedBySwipesAndKeysAndTheAppCanStillSelectIt()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var tabs = TabView();
        tabs.Tabs[1].SetIsEnabled(false);
        var root = Host(tabs);
        using var surface = new SkUiTestSurface(root, 300, 400);
        surface.Frame(0);

        Swipe(root, new Point(250, 200), new Point(50, 200), 0);
        Run(surface, 30, 1500);
        Assert.Equal(2, tabs.SelectedIndex); // past the disabled tab
        Tap(root, tabs.HeaderBounds(1).Center, 1600);
        Assert.Equal(2, tabs.SelectedIndex);

        var keys = root.FocusManager;
        Assert.True(keys.Focus(tabs.StripNode));
        Assert.True(keys.KeyDown(SkUiKey.Left));
        Assert.Equal(0, tabs.SelectedIndex);
        Assert.True(keys.KeyDown(SkUiKey.End));
        Assert.Equal(2, tabs.SelectedIndex);
        Assert.True(keys.KeyDown(SkUiKey.Tab, SkUiKeyModifiers.Control)); // around the end, past the disabled one
        Assert.Equal(0, tabs.SelectedIndex);

        tabs.SetSelectedIndex(1);
        Assert.Equal(1, tabs.SelectedIndex);
        tabs.SetSelectedIndex(9);
        Assert.Equal(2, tabs.SelectedIndex); // coerced to the last tab
    }

    [Fact]
    public void TheSelectedTabStaysSelectedWhereItMovesAndAnIndexAskedForEarlyComesBack()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var early = new SkUiCoreTabView().SetSelectedIndex(2);
        Assert.Equal(2, early.SelectedIndex); // no tabs yet
        early.AddTab(new SkUiCoreTabItem("A"));
        Assert.Equal(0, early.SelectedIndex);
        early.AddTab(new SkUiCoreTabItem("B")).AddTab(new SkUiCoreTabItem("C"));
        Assert.Equal(2, early.SelectedIndex);

        var tabs = ColoredTabs().SetSelectedIndex(1);
        using var surface = new SkUiTestSurface(Host(tabs), 400, 200);
        surface.Frame(0);
        tabs.Tabs.Insert(0, new SkUiCoreTabItem("New", new SkUiCoreBox().SetColor(Colors.Black)));
        Run(surface, 16, 600);
        Assert.Equal(2, tabs.SelectedIndex);
        Assert.Equal(Sk(Colors.Green), surface.Bitmap.GetPixel(200, 120));

        tabs.Tabs.RemoveAt(2);
        Run(surface, 700, 1300);
        Assert.Equal(2, tabs.SelectedIndex); // the tab that took its place
        Assert.Equal(Sk(Colors.Blue), surface.Bitmap.GetPixel(200, 120));
        tabs.Tabs.Clear();
        Run(surface, 1400, 1500);
        Assert.Equal(Sk(Colors.White), surface.Bitmap.GetPixel(200, 120));
    }

    [Fact]
    public void InvalidValuesThrow()
    {
        var tabs = new SkUiCoreTabView();
        Assert.Throws<ArgumentOutOfRangeException>(() => tabs.SetSelectedIndex(-2));
        Assert.Throws<ArgumentOutOfRangeException>(() => tabs.SetTabWidthMode((SkUiSegmentWidthMode)9));
        Assert.Throws<ArgumentOutOfRangeException>(() => tabs.SetIndicatorStyle((SkUiTabIndicatorStyle)9));
        Assert.Throws<ArgumentOutOfRangeException>(() => tabs.SetTabStripPlacement((SkUiTabStripPlacement)9));
        Assert.ThrowsAny<ArgumentException>(() => tabs.SetFontSize(0));
        Assert.Throws<ArgumentNullException>(() => tabs.AddTab(null!));
    }

    #endregion

    #region The strip and the indicator

    [Theory]
    [InlineData(SkUiSegmentWidthMode.Equal, false)]
    [InlineData(SkUiSegmentWidthMode.Content, false)]
    [InlineData(SkUiSegmentWidthMode.Equal, true)]
    [InlineData(SkUiSegmentWidthMode.Content, true)]
    public void TheIndicatorIsDrawnUnderTheSelectedHeader(SkUiSegmentWidthMode mode, bool rightToLeft)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var tabs = TabView("A", "Longer", "Mid").SetTabWidthMode(mode).SetIndicatorColor(Colors.Red);
        using var surface = new SkUiTestSurface(Host(tabs, rightToLeft), 300, 200);
        surface.Frame(0);
        var time = 100d;
        foreach (var selected in new[] { 0, 2, 1 })
        {
            tabs.SetSelectedIndex(selected);
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

    [Fact]
    public void WithContentWidthsTheSelectedHeaderIsInViewFromTheFirstLayout()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var tabs = TabView([.. Enumerable.Range(0, 12).Select(static index => $"Section {index}")])
            .SetTabWidthMode(SkUiSegmentWidthMode.Content).SetSelectedIndex(9);
        using var surface = new SkUiTestSurface(Host(tabs), 300, 200);
        surface.Frame(0);
        surface.Frame(16);

        var header = tabs.HeaderBounds(9);
        Assert.True(header.Right > 300, "the strip overflows");
        Assert.InRange(tabs.StripScrollViewForTests.ScrollX, header.Right - 300 - 0.5, header.Left + 0.5);
        Assert.Equal(2700, tabs.PaneScrollViewForTests.ScrollX, 1);
    }

    [Fact]
    public void NothingIsRecordedAgainWhileThePanesFling()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var tabs = TabView();
        var root = Host(tabs);
        using var surface = new SkUiTestSurface(root, 300, 400);
        surface.Frame(0);
        // Every pane created first (they are recorded once).
        tabs.SetSelectedIndex(2);
        Run(surface, 16, 1000);
        tabs.SetSelectedIndex(0);
        Run(surface, 1000, 2000);

        Swipe(root, new Point(250, 200), new Point(50, 200), 2000);
        surface.Frame(2030);
        var recorded = surface.RecordedPictures;
        var from = tabs.PaneScrollViewForTests.ScrollX;
        for (var ms = 2046d; ms <= 2200; ms += 16)
            surface.Frame(ms); // mid-fling
        Assert.NotEqual(from, tabs.PaneScrollViewForTests.ScrollX); // it moved
        Assert.Equal(1, tabs.SelectedIndex); // selected when released, before the frames above
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
        var tabs = new SkUiCoreTabView();
        for (var index = 0; index < 4; index++)
            tabs.AddTab(new SkUiCoreTabItem($"Tab {index}", new SkUiCoreBox().SetColor(Colors.Gray)));
        var row = new SkUiCoreHorizontalStackLayout();
        for (var index = 0; index < 4; index++)
        {
            var box = new SkUiCoreBox().SetColor(Colors.Gray);
            box.SetWidth(300).SetHeight(400);
            row.Add(box);
        }
        var scroll = new SkUiCoreScrollView().SetOrientation(ScrollOrientation.Horizontal);
        scroll.SetSnapPointsType(SnapPointsType.MandatorySingle);
        scroll.SetContent(row);

        Assert.Equal(FlingAllocations(Host(scroll)), FlingAllocations(Host(tabs)));
    }

    #endregion

    #region Screen readers

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

    private static List<string?> Labels(SkUiView root) =>
        SemanticNodes(root).Select(static node => node.Label).Where(static label => !string.IsNullOrEmpty(label)).ToList();

    [Fact]
    public void ScreenReadersReadTheStripThenTheSelectedPanesContentsUnderItsTabsName()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        SkUiCoreLabel Text(string text) => new SkUiCoreLabel().SetText(text).SetFontFamily(SkUiTestHelpers.BundledFontFamily);
        var stack = new SkUiCoreVerticalStackLayout();
        stack.Add(Text("First text"));
        stack.Add(new SkUiCoreButton().SetText("Press"));
        var tabs = new SkUiCoreTabView().SetFontFamily(SkUiTestHelpers.BundledFontFamily)
            .AddTab(new SkUiCoreTabItem("Posts", stack)).AddTab(new SkUiCoreTabItem("Media", Text("Second text")).SetBadgeText("3").SetAutomationId("MediaTab"));
        var root = Host(tabs);
        using var surface = new SkUiTestSurface(root, 300, 200);
        surface.Frame(0);

        Assert.Equal(["Posts", "Media, 3", "Posts", "First text", "Press"], Labels(root));
        var info = new SkUiSemanticsInfo();
        ((ISkUiAccessibleNode)tabs.StripNode).GetSemantics(info);
        Assert.Equal("Posts", info.Value);
        Assert.Equal(["Selected, 1 of 2", "2 of 2"], info.Parts!.Select(static part => part.Value));
        Assert.Equal("_MediaTab", info.Parts![1].AutomationIdSuffix);

        Assert.True(((ISkUiAccessibleNode)tabs.StripNode).PerformSemanticsPartAction(1, SkUiSemanticsActions.Activate));
        Run(surface, 50, 1500);
        Assert.Equal(["Posts", "Media, 3", "Media", "Second text"], Labels(root)); // the pane out of view is not read

        tabs.SetTabStripPlacement(SkUiTabStripPlacement.Bottom);
        surface.Frame(2000);
        Assert.Equal(["Media", "Second text", "Posts", "Media, 3"], Labels(root));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ScreenReadersPageWholeTabsAndArrowKeysInsideAPaneLeaveThePanesInPlace(bool rightToLeft)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var button = new SkUiCoreButton().SetText("Press");
        var tabs = new SkUiCoreTabView().AddTab(new SkUiCoreTabItem("A", button))
            .AddTab(new SkUiCoreTabItem("B", new SkUiCoreBox()).SetIsEnabled(false)).AddTab(new SkUiCoreTabItem("C", new SkUiCoreBox()));
        var root = Host(tabs, rightToLeft);
        using var surface = new SkUiTestSurface(root, 300, 200);
        surface.Frame(0);
        var scroll = tabs.PaneScrollViewForTests;
        var start = scroll.ScrollX;
        Assert.Equal(rightToLeft ? 600 : 0, start, 1);
        Assert.True(root.FocusManager.Focus(button));

        var time = 50d;
        foreach (var key in new[] { SkUiKey.Right, SkUiKey.Left, SkUiKey.Down, SkUiKey.Up, SkUiKey.Home, SkUiKey.End })
        {
            root.FocusManager.KeyDown(key);
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

    #endregion
}

/// <summary>1.17 S8: both tab views after the look and scheme change, and after their tree is drawn by another surface, against a fresh one.</summary>
[Collection(GlobalStateCollection.Name)]
public class TabViewLookChangeTests
{
    private static SkUiView Build(bool core, SkUiTabIndicatorStyle style)
    {
        if (core)
        {
            var tabs = new SkUiCoreTabView().SetFontFamily(SkUiTestHelpers.BundledFontFamily).SetIndicatorStyle(style)
                .AddTab(new SkUiCoreTabItem("A longer one", new SkUiCoreBox())).AddTab(new SkUiCoreTabItem("Media", new SkUiCoreBox()).SetBadgeText("3"))
                .SetSelectedIndex(1);
            return new SkUiCoreHost().SetContent(tabs);
        }
        var drawn = new SkUiTabView { FontFamily = SkUiTestHelpers.BundledFontFamily, IndicatorStyle = style };
        drawn.AddTab(new SkUiTabItem("A longer one", new SkUiBox())).AddTab(new SkUiTabItem("Media", new SkUiBox()) { BadgeText = "3" });
        drawn.SelectedIndex = 1;
        return new SkUiContentView { Content = drawn };
    }

    private static string Pixels(SkUiTestSurface surface)
    {
        for (var ms = 1000; ms < 1200; ms += 16)
            surface.Frame(ms);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(surface.Bitmap.Bytes));
    }

    [Theory]
    [InlineData(false, SkUiTabIndicatorStyle.Underline)]
    [InlineData(false, SkUiTabIndicatorStyle.Pill)]
    [InlineData(true, SkUiTabIndicatorStyle.Underline)]
    [InlineData(true, SkUiTabIndicatorStyle.Pill)]
    public void ALookAndSchemeChangeRedrawsTheTabViewAsAFreshOne(bool core, SkUiTabIndicatorStyle style)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (look, scheme) = (SkUiLook.Current, SkUiColorScheme.Current);
        try
        {
            var root = Build(core, style);
            using var surface = new SkUiTestSurface(root, 300, 160);
            surface.Frame(0);
            var before = Pixels(surface);
            SkUiLook.Current = new AndroidSkUiLook(); // another strip height, colors and indicator
            SkUiColorScheme.Current = new DarkSkUiColorScheme();
            SkUiRenderInvalidation.MarkLookChanged(root);
            SkUiTestHelpers.Arrange(root, 300, 160);
            var after = Pixels(surface);

            using var fresh = new SkUiTestSurface(Build(core, style), 300, 160);
            fresh.Frame(0);
            Assert.NotEqual(before, after);
            Assert.Equal(Pixels(fresh), after);
        }
        finally
        {
            SkUiLook.Current = look;
            SkUiColorScheme.Current = scheme;
        }
    }

    public static TheoryData<bool, string> Changes()
    {
        var data = new TheoryData<bool, string>();
        foreach (var core in new[] { false, true })
            foreach (var change in new[] { "bottom", "content", "pill", "none", "text", "selected text", "indicator", "background", "size", "italic", "header", "badge", "disabled", "off" })
                data.Add(core, change);
        return data;
    }

    private static void Change(SkUiView root, string change)
    {
        if (root is SkUiCoreHost { Content: SkUiCoreTabView core })
            switch (change)
            {
                case "bottom": core.SetTabStripPlacement(SkUiTabStripPlacement.Bottom); break;
                case "content": core.SetTabWidthMode(SkUiSegmentWidthMode.Content); break;
                case "pill": core.SetIndicatorStyle(SkUiTabIndicatorStyle.Pill); break;
                case "none": core.SetIndicatorStyle(SkUiTabIndicatorStyle.None); break;
                case "text": core.SetHeaderTextColor(Colors.Green); break;
                case "selected text": core.SetSelectedHeaderTextColor(Colors.Green); break;
                case "indicator": core.SetIndicatorColor(Colors.Green); break;
                case "background": core.SetStripBackgroundColor(Colors.Yellow); break;
                case "size": core.SetFontSize(22); break;
                case "italic": core.SetFontAttributes(FontAttributes.Italic); break;
                case "header": core.Tabs[0].SetHeader("Renamed"); break;
                case "badge": core.Tabs[0].SetBadgeText(""); break;
                case "disabled": core.Tabs[0].SetIsEnabled(false); break;
                default: core.SetIsEnabled(false); break;
            }
        else if (root is SkUiContentView { Content: SkUiTabView drawn })
            switch (change)
            {
                case "bottom": drawn.TabStripPlacement = SkUiTabStripPlacement.Bottom; break;
                case "content": drawn.TabWidthMode = SkUiSegmentWidthMode.Content; break;
                case "pill": drawn.IndicatorStyle = SkUiTabIndicatorStyle.Pill; break;
                case "none": drawn.IndicatorStyle = SkUiTabIndicatorStyle.None; break;
                case "text": drawn.HeaderTextColor = Colors.Green; break;
                case "selected text": drawn.SelectedHeaderTextColor = Colors.Green; break;
                case "indicator": drawn.IndicatorColor = Colors.Green; break;
                case "background": drawn.StripBackgroundColor = Colors.Yellow; break;
                case "size": drawn.FontSize = 22; break;
                case "italic": drawn.FontAttributes = FontAttributes.Italic; break;
                case "header": drawn.Tabs[0].Header = "Renamed"; break;
                case "badge": drawn.Tabs[0].BadgeText = ""; break;
                case "disabled": drawn.Tabs[0].IsEnabled = false; break;
                default: drawn.IsEnabled = false; break;
            }
        else
            Assert.Fail("not a tab view's root");
    }

    /// <summary>A property changed on a shown tab view draws as one built with it, and back again.</summary>
    [Theory]
    [MemberData(nameof(Changes))]
    public void AChangeOfAShownTabViewDrawsAsOneBuiltWithIt(bool core, string change)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var root = Build(core, SkUiTabIndicatorStyle.Underline);
        using var surface = new SkUiTestSurface(root, 300, 160);
        surface.Frame(0);
        var before = Pixels(surface);
        Change(root, change);
        var after = Pixels(surface);

        var built = Build(core, SkUiTabIndicatorStyle.Underline);
        Change(built, change);
        using var fresh = new SkUiTestSurface(built, 300, 160);
        fresh.Frame(0);
        Assert.NotEqual(before, after);
        Assert.Equal(Pixels(fresh), after);
    }

    public static TheoryData<bool, string> TreeLooks()
    {
        var data = new TheoryData<bool, string>();
        foreach (var core in new[] { false, true })
            foreach (var name in new[] { nameof(AndroidSkUiLook), nameof(AndroidMaterial3SkUiLook), nameof(MacSkUiLook), nameof(WindowsSkUiLook) })
                data.Add(core, name);
        return data;
    }

    /// <summary>A subtree's own look draws and measures the tab view as the same look app-wide does: strip height, indicator, fonts.</summary>
    [Theory]
    [MemberData(nameof(TreeLooks))]
    public void ASubtreesLookDrawsAsTheSameLookAppWide(bool core, string name)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        DefaultSkUiLook look = name switch
        {
            nameof(AndroidSkUiLook) => new AndroidSkUiLook(),
            nameof(AndroidMaterial3SkUiLook) => new AndroidMaterial3SkUiLook(),
            nameof(MacSkUiLook) => new MacSkUiLook(),
            _ => new WindowsSkUiLook()
        };
        var root = Build(core, SkUiTabIndicatorStyle.Underline);
        root.Look = look;
        using var surface = new SkUiTestSurface(root, 300, 160);
        surface.Frame(0);
        var subtree = Pixels(surface);

        var app = SkUiLook.Current;
        try
        {
            SkUiLook.Current = look;
            using var appWide = new SkUiTestSurface(Build(core, SkUiTabIndicatorStyle.Underline), 300, 160);
            appWide.Frame(0);
            Assert.Equal(Pixels(appWide), subtree);
        }
        finally
        {
            SkUiLook.Current = app;
        }
    }

    [Theory]
    [InlineData(false, SkUiTabIndicatorStyle.Underline)]
    [InlineData(false, SkUiTabIndicatorStyle.Pill)]
    [InlineData(true, SkUiTabIndicatorStyle.Underline)]
    [InlineData(true, SkUiTabIndicatorStyle.Pill)]
    public void ATreeDrawnByAnotherSurfaceLooksTheSame(bool core, SkUiTabIndicatorStyle style)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var root = Build(core, style);
        string before;
        using (var surface = new SkUiTestSurface(root, 300, 160))
        {
            surface.Frame(0);
            before = Pixels(surface);
        }
        SkUiRenderInvalidation.ResetSubtree(root);
        using var other = new SkUiTestSurface(root, 300, 160);
        other.Frame(0);
        Assert.Equal(before, Pixels(other));
    }
}
