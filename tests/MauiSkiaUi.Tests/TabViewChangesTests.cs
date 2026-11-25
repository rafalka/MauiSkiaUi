using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// 1.17: both tab views while things change under them: tabs added and removed as the panes move, the direction changing,
/// unbounded parents (the layout settles), the selection set again inside its own event, the selected tab removed, keyboard
/// focus inside panes, Space and Enter on the strip, larger text, and the tab items' binding contexts and templates.
/// </summary>
public class TabViewChangesTests
{
    private static long _pointer = 450_000;

    /// <summary>A tab view on either layer, with what the tests need of it.</summary>
    private sealed record Tabs(SkUiView Root, Func<int> Selected, Action<int> Select, Func<double> Offset, Action<int, string> Insert, Action<int> Remove,
        Action<Action<int, int>> OnChanged, SkUiView? Drawn, SkUiCoreTabView? Core);

    private static Tabs Make(bool core, int count = 4, bool rightToLeft = false, Func<int, SkUiView>? drawnPane = null, Func<int, SkUiCoreNode>? corePane = null)
    {
        var direction = rightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        if (core)
        {
            var tabs = new SkUiCoreTabView().SetFontFamily(SkUiTestHelpers.BundledFontFamily);
            for (var index = 0; index < count; index++)
                tabs.AddTab(new SkUiCoreTabItem($"T{index}", corePane?.Invoke(index) ?? new SkUiCoreBox()));
            return new Tabs(new SkUiCoreHost { FlowDirection = direction }.SetContent(tabs), () => tabs.SelectedIndex, value => tabs.SetSelectedIndex(value),
                () => tabs.PaneScrollViewForTests.ScrollX, (index, header) => tabs.Tabs.Insert(index, new SkUiCoreTabItem(header, new SkUiCoreBox())),
                tabs.Tabs.RemoveAt, handler => tabs.SelectionChanged += (_, args) => handler(args.PreviousIndex, args.CurrentIndex), null, tabs);
        }
        var drawn = new SkUiTabView { FontFamily = SkUiTestHelpers.BundledFontFamily };
        for (var index = 0; index < count; index++)
            drawn.AddTab(new SkUiTabItem($"T{index}", drawnPane?.Invoke(index) ?? new SkUiBox()));
        return new Tabs(new SkUiContentView { Content = drawn, FlowDirection = direction }, () => drawn.SelectedIndex, value => drawn.SelectedIndex = value,
            () => drawn.PaneScrollViewForTests.ScrollX, (index, header) => drawn.Tabs.Insert(index, new SkUiTabItem(header, new SkUiBox())),
            drawn.Tabs.RemoveAt, handler => drawn.SelectionChanged += (_, args) => handler(args.PreviousIndex, args.CurrentIndex), drawn, null);
    }

    private static void Run(SkUiTestSurface surface, double from, double until)
    {
        for (var ms = from; ms <= until; ms += 16)
        {
            surface.Root.AnimationClock.Tick(TimeSpan.FromMilliseconds(ms));
            surface.Frame(ms);
        }
    }

    private static void Swipe(SkUiView root, double fromX, double toX, double startMs)
    {
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, new Point(fromX, 100), TimeSpan.FromMilliseconds(startMs)));
        root.Touch(new(id, SkUiTouchAction.Moved, new Point((fromX * 2 + toX) / 3, 100), TimeSpan.FromMilliseconds(startMs + 10)));
        root.Touch(new(id, SkUiTouchAction.Moved, new Point(toX, 100), TimeSpan.FromMilliseconds(startMs + 20)));
        root.Touch(new(id, SkUiTouchAction.Released, new Point(toX, 100), TimeSpan.FromMilliseconds(startMs + 25)));
    }

    private static void AssertRestsOnTheSelectedTab(Tabs tabs, int count, bool rightToLeft, string when)
    {
        var expected = rightToLeft ? (count - 1 - tabs.Selected()) * 300 : tabs.Selected() * 300;
        Assert.True(Math.Abs(expected - tabs.Offset()) < 0.5, $"{when}: tab {tabs.Selected()} is selected and the panes rest at {tabs.Offset()}, not {expected}");
    }

    #region Tabs changing while the panes move

    public static TheoryData<bool, bool, bool, string> Changes()
    {
        var data = new TheoryData<bool, bool, bool, string>();
        foreach (var core in new[] { false, true })
            foreach (var rightToLeft in new[] { false, true })
                foreach (var swipe in new[] { false, true })
                    foreach (var change in new[] { "append", "insert 0", "insert 1", "insert 2", "remove 0", "remove 1", "remove 2", "remove 3" })
                        data.Add(core, rightToLeft, swipe, change);
        return data;
    }

    [Theory]
    [MemberData(nameof(Changes))]
    public void TabsAddedOrRemovedWhileThePanesMoveLeaveThemRestingOnTheSelectedTab(bool core, bool rightToLeft, bool swipe, string change)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var tabs = Make(core, rightToLeft: rightToLeft);
        using var surface = new SkUiTestSurface(tabs.Root, 300, 200);
        surface.Frame(0);
        if (swipe)
            Swipe(tabs.Root, rightToLeft ? 50 : 250, rightToLeft ? 250 : 50, 0);
        else
            tabs.Select(1);
        Run(surface, 30, 110); // on the way
        var (kind, at) = change == "append" ? ("insert", 4) : (change.Split(' ')[0], int.Parse(change.Split(' ')[1]));
        if (kind == "insert")
            tabs.Insert(at, "New");
        else
            tabs.Remove(at);
        Run(surface, 126, 2500);

        AssertRestsOnTheSelectedTab(tabs, kind == "insert" ? 5 : 3, rightToLeft, change);
        if (!swipe && change != "remove 1")
            Assert.Equal(kind == "insert" && at <= 1 ? 2 : kind == "remove" && at == 0 ? 0 : 1, tabs.Selected()); // the tab asked for, where it is now
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AfterTheSelectedLastTabIsRemovedAnAddedOneIsNotSelected(bool core)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var tabs = Make(core, count: 3);
        using var surface = new SkUiTestSurface(tabs.Root, 300, 200);
        surface.Frame(0);
        tabs.Select(2);
        Run(surface, 16, 800);
        tabs.Remove(2);
        surface.Frame(900);
        Assert.Equal(1, tabs.Selected());

        tabs.Insert(2, "New");
        Run(surface, 1000, 1200);
        Assert.Equal(1, tabs.Selected());
        tabs.Insert(0, "First"); // still followed
        Run(surface, 1300, 1600);
        Assert.Equal(2, tabs.Selected());
        AssertRestsOnTheSelectedTab(tabs, 4, false, "after the inserts");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnIndexBeyondTheTabsBecomesTheLastForGood(bool core)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var tabs = Make(core, count: 3);
        tabs.Select(10);
        Assert.Equal(2, tabs.Selected());
        tabs.Insert(3, "New");
        Assert.Equal(2, tabs.Selected());
    }

    #endregion

    #region Direction

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 3)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 3)]
    public void ChangingTheDirectionKeepsTheSelectedPaneShown(bool core, int selected)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var tabs = Make(core);
        using var surface = new SkUiTestSurface(tabs.Root, 300, 200);
        surface.Frame(0);
        tabs.Select(selected);
        Run(surface, 16, 800);
        var time = 900;
        foreach (var direction in new[] { FlowDirection.RightToLeft, FlowDirection.LeftToRight, FlowDirection.RightToLeft })
        {
            tabs.Root.FlowDirection = direction;
            Run(surface, time, time + 100);
            time += 200;
            Assert.Equal(selected, tabs.Selected());
            AssertRestsOnTheSelectedTab(tabs, 4, direction == FlowDirection.RightToLeft, $"{direction}");
        }
    }

    #endregion

    #region Unbounded parents

    public static TheoryData<bool, string> Unbounded()
    {
        var data = new TheoryData<bool, string>();
        foreach (var core in new[] { false, true })
            foreach (var parent in new[] { "horizontal stack", "horizontal scroll", "auto column", "vertical stack" })
                data.Add(core, parent);
        return data;
    }

    [Theory]
    [MemberData(nameof(Unbounded))]
    public void TheLayoutSettlesWhereNothingBoundsTheTabView(bool core, string parent)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var tabs = Make(core, drawnPane: static _ => new SkUiBox { WidthRequest = 260, HeightRequest = 90 }, corePane: static _ => new SkUiCoreBox().SetWidth(260).SetHeight(90));
        var root = LayoutSettling.Place((View)tabs.Root, parent);
        using var surface = new SkUiTestSurface(root, 300, 200);
        LayoutSettling.AssertSettles(surface, parent);

        var size = ((IView)tabs.Root).DesiredSize;
        if (parent != "vertical stack")
            Assert.Equal(260, size.Width, 0.5); // as wide as its first pane
        Assert.Equal(90 + SkUiLook.Current.TabStripMetrics.Height, size.Height, 0.5);
    }

    #endregion

    #region The second look: what the fixes must not break

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnUnboundedTabViewFollowsItsPanesSize(bool core)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var drawn = new SkUiBox { WidthRequest = 260, HeightRequest = 90 };
        var node = new SkUiCoreBox();
        node.SetWidth(260).SetHeight(90);
        var tabs = Make(core, drawnPane: index => index == 0 ? drawn : new SkUiBox(), corePane: index => index == 0 ? node : new SkUiCoreBox());
        using var surface = new SkUiTestSurface(LayoutSettling.Place(tabs.Root, "horizontal stack"), 600, 300);
        LayoutSettling.AssertSettles(surface, "at first");
        Assert.Equal(260, tabs.Root.Width, 0.5);
        if (core)
            node.SetWidth(400).SetHeight(140);
        else
            (drawn.WidthRequest, drawn.HeightRequest) = (400, 140);
        LayoutSettling.AssertSettles(surface, "larger", 2000);
        Assert.Equal(400, tabs.Root.Width, 0.5);
        Assert.Equal(140 + SkUiLook.Current.TabStripMetrics.Height, ((IView)tabs.Root).DesiredSize.Height, 0.5);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    public void SeveralTabsAddedInOneTurnDoNotMakeThePanesJump(bool core, int added)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var tabs = Make(core);
        using var surface = new SkUiTestSurface(tabs.Root, 300, 200);
        surface.Frame(0);
        tabs.Select(2);
        Run(surface, 16, 130);
        var before = tabs.Offset();
        Assert.InRange(before, 60, 560);
        for (var index = 0; index < added; index++)
            tabs.Insert(4, "New");
        surface.Frame(146);
        Assert.InRange(tabs.Offset(), before, before + 120); // one frame on
        var highest = 0d;
        for (var ms = 162; ms < 1500; ms += 16)
        {
            surface.Frame(ms);
            highest = Math.Max(highest, tabs.Offset());
        }
        Assert.InRange(highest, 599.5, 600.5); // never past the tab asked for
        Assert.Equal(2, tabs.Selected());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FocusInAnotherPaneLeavesAScrollViewAroundTheTabViewInPlace(bool core)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (tabs, buttons, _) = WithButtons(core);
        tabs.Root.WidthRequest = 300;
        tabs.Root.HeightRequest = 200;
        var outer = new SkUiScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            Content = new SkUiHorizontalStackLayout { Children = { (ISkUiView)tabs.Root, new SkUiBox { WidthRequest = 900, HeightRequest = 200 } } }
        };
        using var surface = new SkUiTestSurface(outer, 300, 200);
        surface.Frame(0);
        foreach (var index in new[] { 2, 0 })
        {
            tabs.Select(index);
            Run(surface, index == 2 ? 16 : 1000, index == 2 ? 900 : 1900);
        }
        Assert.True(outer.FocusManager.Focus(buttons[2]));
        Run(surface, 2000, 3000);
        Assert.Equal((2, 600, 0), (tabs.Selected(), (int)Math.Round(tabs.Offset()), (int)Math.Round(outer.ScrollX)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MovingFocusToAWideStripKeepsItsSelectedHeaderInView(bool core)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var buttons = new List<ISkUiAccessibleNode>();
        var tabs = Make(core, count: 12, drawnPane: _ => { var button = new SkUiButton { Text = "B" }; buttons.Add(button); return button; },
            corePane: _ => { var button = new SkUiCoreButton().SetText("B"); buttons.Add(button); return button; });
        for (var index = 0; index < 12; index++)
            if (core)
                tabs.Core!.Tabs[index].SetHeader($"Section {index}");
            else
                ((SkUiTabView)tabs.Drawn!).Tabs[index].Header = $"Section {index}";
        if (core)
            tabs.Core!.SetTabWidthMode(SkUiSegmentWidthMode.Content);
        else
            ((SkUiTabView)tabs.Drawn!).TabWidthMode = SkUiSegmentWidthMode.Content;
        using var surface = new SkUiTestSurface(tabs.Root, 300, 200);
        surface.Frame(0);
        tabs.Select(7);
        Run(surface, 16, 1000);
        Assert.True(tabs.Root.FocusManager.Focus(buttons[7]));
        Assert.True(tabs.Root.FocusManager.KeyDown(SkUiKey.Tab, SkUiKeyModifiers.Control));
        Run(surface, 1100, 2200);
        Assert.Equal(8, tabs.Selected());
        var header = core ? tabs.Core!.HeaderBounds(8) : ((SkUiTabView)tabs.Drawn!).HeaderBounds(8);
        var scroll = core ? tabs.Core!.StripScrollViewForTests.ScrollX : ((SkUiTabView)tabs.Drawn!).StripScrollViewForTests.ScrollX;
        Assert.InRange(scroll, header.Right - 300 - 0.5, header.Left + 0.5);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AFingerThatCatchesThePanesHoldsThem(bool core)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var tabs = Make(core);
        using var surface = new SkUiTestSurface(tabs.Root, 300, 200);
        surface.Frame(0);
        tabs.Select(2);
        Run(surface, 16, 130);
        tabs.Insert(4, "New");
        Run(surface, 146, 162);
        tabs.Root.Touch(new(480_001, SkUiTouchAction.Pressed, new Point(150, 120), TimeSpan.FromMilliseconds(170)));
        surface.Frame(178);
        var held = tabs.Offset();
        Run(surface, 194, 900);
        Assert.Equal(held, tabs.Offset(), 0.5);
        tabs.Root.Touch(new(480_001, SkUiTouchAction.Released, new Point(150, 120), TimeSpan.FromMilliseconds(910)));
        Run(surface, 920, 2500);
        AssertRestsOnTheSelectedTab(tabs, 5, false, "after the release");
    }

    #endregion

    #region The selection set again inside its event

    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public void ATabTheAppGivesBackInTheEventMovesThePanesBackToo(bool core, bool swipe)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var tabs = Make(core);
        tabs.OnChanged((_, current) =>
        {
            if (current == 1)
                tabs.Select(0); // a locked tab
        });
        using var surface = new SkUiTestSurface(tabs.Root, 300, 200);
        surface.Frame(0);
        if (swipe)
            Swipe(tabs.Root, 250, 50, 0);
        else
            tabs.Select(1);
        Run(surface, 30, 2500);
        Assert.Equal(0, tabs.Selected());
        Assert.Equal(0, tabs.Offset(), 0.5);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ASelectionMadeInThePropertysNotificationIsTheOneEverythingShows(bool core)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var tabs = Make(core);
        var events = new List<int>();
        tabs.OnChanged((_, current) => events.Add(current));
        System.ComponentModel.INotifyPropertyChanged notifier = core ? tabs.Core! : tabs.Drawn!;
        notifier.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SkUiTabView.SelectedIndex) && tabs.Selected() == 1)
                tabs.Select(2);
        };
        using var surface = new SkUiTestSurface(tabs.Root, 300, 200);
        surface.Frame(0);
        tabs.Select(1);
        Run(surface, 16, 1500);
        Assert.Equal(2, tabs.Selected());
        Assert.Equal(2, events[^1]);
        AssertRestsOnTheSelectedTab(tabs, 4, false, "after the notification");
    }

    [Fact]
    public void ASelectionMadeInTheEventOfARemovalIsWhatWasAskedForLast()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var tabs = Make(core: false, count: 3);
        tabs.Select(2);
        tabs.OnChanged((_, current) =>
        {
            if (current == 1)
                tabs.Select(0);
        });
        tabs.Remove(2);
        Assert.Equal(0, tabs.Selected());
        tabs.Insert(2, "New");
        Assert.Equal(0, tabs.Selected());
    }

    #endregion

    #region Keyboard

    private static (Tabs Tabs, ISkUiAccessibleNode[] Buttons, ISkUiAccessibleNode Strip) WithButtons(bool core)
    {
        var buttons = new ISkUiAccessibleNode[3];
        var tabs = Make(core, count: 3,
            drawnPane: index => (SkUiView)(buttons[index] = new SkUiButton { Text = "B", WidthRequest = 80, HorizontalOptions = LayoutOptions.Start }),
            corePane: index =>
            {
                var button = new SkUiCoreButton().SetText("B");
                button.SetWidth(80).SetHorizontalAlignment(LayoutAlignment.Start);
                buttons[index] = button;
                return button;
            });
        return (tabs, buttons, core ? tabs.Core!.StripNode : (ISkUiAccessibleNode)((SkUiTabView)tabs.Drawn!).StripView);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TabGoesOnIntoTheSelectedPaneOnly(bool core)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (tabs, buttons, strip) = WithButtons(core);
        using var surface = new SkUiTestSurface(tabs.Root, 300, 200);
        surface.Frame(0);
        tabs.Select(1);
        Run(surface, 16, 800);
        tabs.Select(0);
        Run(surface, 900, 1700);

        Assert.Equal([strip, buttons[0]], tabs.Root.FocusManager.GetTabOrder());
        tabs.Select(1);
        Run(surface, 1800, 2600);
        Assert.Equal([strip, buttons[1]], tabs.Root.FocusManager.GetTabOrder());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FocusingAControlInAnotherPaneSelectsItsTab(bool core)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (tabs, buttons, _) = WithButtons(core);
        using var surface = new SkUiTestSurface(tabs.Root, 300, 200);
        surface.Frame(0);
        tabs.Select(1);
        Run(surface, 16, 800);
        tabs.Select(0);
        Run(surface, 900, 1700);

        Assert.True(tabs.Root.FocusManager.Focus(buttons[1]));
        Run(surface, 1800, 2800);
        Assert.Equal(1, tabs.Selected());
        AssertRestsOnTheSelectedTab(tabs, 3, false, "after the focus");
        Assert.Same(buttons[1], tabs.Root.FocusManager.Focused);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FocusLeavesAPaneThatIsNoLongerSelectedForTheStrip(bool core)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (tabs, buttons, strip) = WithButtons(core);
        using var surface = new SkUiTestSurface(tabs.Root, 300, 200);
        surface.Frame(0);
        Assert.True(tabs.Root.FocusManager.Focus(buttons[0]));
        Assert.True(tabs.Root.FocusManager.KeyDown(SkUiKey.Tab, SkUiKeyModifiers.Control));
        Run(surface, 16, 900);
        Assert.Equal(1, tabs.Selected());
        Assert.Same(strip, tabs.Root.FocusManager.Focused);
        Assert.True(tabs.Root.FocusManager.KeyDown(SkUiKey.Tab, SkUiKeyModifiers.Control)); // goes on
        Assert.Equal(2, tabs.Selected());
    }

    [Theory]
    [InlineData(false, "Space")]
    [InlineData(false, "Enter")]
    [InlineData(true, "Space")]
    [InlineData(true, "Enter")]
    public void SpaceAndEnterOnTheStripSelectNothingElse(bool core, string key)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (tabs, _, strip) = WithButtons(core);
        using var surface = new SkUiTestSurface(tabs.Root, 300, 200);
        surface.Frame(0);
        Assert.True(tabs.Root.FocusManager.Focus(strip));
        Assert.True(tabs.Root.FocusManager.KeyDown(Enum.Parse<SkUiKey>(key)));
        Assert.Equal(0, tabs.Selected());
        var info = new SkUiSemanticsInfo();
        strip.GetSemantics(info);
        Assert.Equal(SkUiSemanticsActions.None, info.Actions & SkUiSemanticsActions.Activate);
        strip.PerformSemanticsAction(SkUiSemanticsActions.Activate);
        Assert.Equal(0, tabs.Selected());
    }

    #endregion

    #region The strip and the tab items

    [Fact]
    public void ATabIsInTheTabViewOnce()
    {
        var tab = new SkUiTabItem("A", new SkUiBox());
        var tabs = new SkUiTabView().AddTab(tab).AddTab(new SkUiTabItem("B", new SkUiBox()));
        Assert.Throws<ArgumentException>(() => tabs.AddTab(tab));
        Assert.Throws<ArgumentException>(() => tabs.Tabs.Insert(0, tab));
        Assert.Throws<ArgumentException>(() => tabs.Tabs[1] = tab);
        Assert.Throws<ArgumentNullException>(() => tabs.Tabs.Add(null!));
        Assert.Equal(2, tabs.Tabs.Count);
        tabs.Tabs[0] = tab; // the same place
        tabs.Tabs.Remove(tab);
        tabs.AddTab(tab); // it left, so it can come back
        ((System.Collections.ObjectModel.ObservableCollection<SkUiTabItem>)tabs.Tabs).Move(1, 0);
        Assert.Equal(["A", "B"], tabs.Tabs.Select(static item => item.Header));

        var coreTab = new SkUiCoreTabItem("A", new SkUiCoreBox());
        var core = new SkUiCoreTabView().AddTab(coreTab);
        Assert.Throws<ArgumentException>(() => core.AddTab(coreTab));
        Assert.Single(core.Tabs);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LargerTextMakesTheStripTaller(bool core)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var tabs = Make(core);
        using var surface = new SkUiTestSurface(tabs.Root, 300, 300);
        surface.Frame(0);
        double Height() => core ? tabs.Core!.StripScrollViewForTests.Frame.Height : ((SkUiTabView)tabs.Drawn!).StripScrollViewForTests.Height;
        Assert.Equal(SkUiLook.Current.TabStripMetrics.Height, Height());
        if (core)
            tabs.Core!.SetFontSize(48);
        else
            ((SkUiTabView)tabs.Drawn!).FontSize = 48;
        surface.Frame(16);
        Assert.True(Height() >= 48, $"the strip is {Height()} tall with text of 48");
    }

    [Fact]
    public void APaneBindsToItsTabsContext()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var label = new SkUiLabel();
        label.SetBinding(SkUiLabel.TextProperty, static (string text) => text);
        var tab = new SkUiTabItem("A", label);
        var tabs = new SkUiTabView { BindingContext = "the tab view's" };
        tabs.AddTab(tab);
        using var surface = new SkUiTestSurface(tabs, 300, 200);
        surface.Frame(0);
        Assert.Equal("the tab view's", label.Text);
        tab.BindingContext = "the tab's";
        Assert.Equal("the tab's", label.Text);
        tabs.BindingContext = "another";
        Assert.Equal("the tab's", label.Text);
    }

    private sealed class PaneSelector : DataTemplateSelector
    {
        protected override DataTemplate OnSelectTemplate(object item, BindableObject container) =>
            new(() => new SkUiLabel { Text = $"Pane of {item}" });
    }

    [Fact]
    public void APanesTemplateCanBeASelector()
    {
        var tab = new SkUiTabItem { Header = "A", ContentTemplate = new PaneSelector(), BindingContext = "vm" };
        var tabs = new SkUiTabView();
        tabs.AddTab(tab);
        using var surface = new SkUiTestSurface(tabs, 300, 200);
        surface.Frame(0);
        var labels = new List<string?>();
        void Walk(SkUiSemanticsNode node) { labels.Add(node.Label); foreach (var child in node.Children) Walk(child); }
        Walk(SkUiSemanticsTree.Build(tabs).Root);
        Assert.Contains("Pane of vm", labels);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnIconTabIsReadByItsDescription(bool core)
    {
        var tabs = Make(core, count: 2);
        if (core)
            tabs.Core!.Tabs[0].SetHeader(null).SetSemanticDescription("Home");
        else
        {
            var tab = ((SkUiTabView)tabs.Drawn!).Tabs[0];
            tab.Header = null;
            SemanticProperties.SetDescription(tab, "Home");
        }
        using var surface = new SkUiTestSurface(tabs.Root, 300, 200);
        surface.Frame(0);
        var info = new SkUiSemanticsInfo();
        (core ? tabs.Core!.StripNode : (ISkUiAccessibleNode)((SkUiTabView)tabs.Drawn!).StripView).GetSemantics(info);
        Assert.Equal("Home", info.Parts![0].Label);
        Assert.Equal("Home", info.Value);
    }

    #endregion
}

/// <summary>Whether a layout settles: a frame after the first asks for no other.</summary>
internal static class LayoutSettling
{
    public static SkUiView Place(View view, string parent)
    {
        switch (parent)
        {
            case "horizontal stack":
                return new SkUiHorizontalStackLayout { Children = { (ISkUiView)view } };
            case "horizontal scroll":
                return new SkUiScrollView { Orientation = ScrollOrientation.Horizontal, Content = (ISkUiView)view };
            case "auto column":
            {
                var grid = new SkUiGrid { ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) } };
                grid.Children.Add((ISkUiView)view);
                return grid;
            }
            default:
                return new SkUiVerticalStackLayout { Children = { (ISkUiView)view } };
        }
    }

    public static void AssertSettles(SkUiTestSurface surface, string when, double startMs = 0)
    {
        for (var frame = 0; frame < 10; frame++)
            surface.Frame(startMs + frame * 16);
        var before = surface.RenderRequests;
        for (var frame = 10; frame < 40; frame++)
            surface.Frame(startMs + frame * 16);
        Assert.True(surface.RenderRequests == before, $"{when}: {surface.RenderRequests - before} frames asked for in 30 idle ones");
    }
}
