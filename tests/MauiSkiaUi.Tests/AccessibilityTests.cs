using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Semantics trees of drawn surfaces (P10 in ImplementationPlan.md): which drawn nodes of either layer screen readers
/// read, what they read, where they are, and the actions they perform.
/// </summary>
public class AccessibilityTests
{
    private static SkUiVerticalStackLayout Stack(params ISkUiView[] children)
    {
        var stack = new SkUiVerticalStackLayout();
        foreach (var child in children)
            stack.Children.Add(child);
        return stack;
    }

    private static SkUiHorizontalStackLayout Row(params ISkUiView[] children)
    {
        var row = new SkUiHorizontalStackLayout();
        foreach (var child in children)
            row.Children.Add(child);
        return row;
    }

    private static SkUiSemanticsTree Tree(SkUiView root, double width = 300, double height = 400)
    {
        SkUiTestHelpers.Arrange(root, width, height);
        return SkUiSemanticsTree.Build(root);
    }

    private static string Describe(SkUiSemanticsTree tree) =>
        string.Join(" | ", tree.Flatten().Select(node => $"{node.Role}:{node.Label}"));

    [Fact]
    public void ControlsAreElementsWithTheirRolesAndText()
    {
        var root = Stack(
            new SkUiLabel { Text = "Title" },
            new SkUiButton { Text = "Save" },
            new SkUiCheckBox { IsChecked = true },
            new SkUiSwitch(),
            new SkUiRadioButton { Content = "Monthly" },
            new SkUiSlider { Minimum = 0, Maximum = 10, Value = 5 },
            new SkUiProgressBar { Progress = 0.25 },
            new SkUiBox { HeightRequest = 10 },
            new SkUiImage { HeightRequest = 10 });
        var tree = Tree(root);

        Assert.Equal("Text:Title | Button:Save | CheckBox: | Switch: | RadioButton:Monthly | Slider: | ProgressBar:", Describe(tree));
        var nodes = tree.Flatten().ToList();
        Assert.Equal(SkUiCheckState.Checked, nodes[2].CheckState);
        Assert.Equal(SkUiCheckState.Unchecked, nodes[3].CheckState);
        Assert.Equal(new SkUiSemanticsRange(0, 10, 5), nodes[5].Range);
        Assert.Equal(SkUiSemanticsActions.Increment | SkUiSemanticsActions.Decrement, nodes[5].Actions);
        Assert.Equal(new SkUiSemanticsRange(0, 1, 0.25), nodes[6].Range);
        Assert.Equal(SkUiSemanticsActions.Activate, nodes[1].Actions);
    }

    [Fact]
    public void SemanticPropertiesApply()
    {
        var image = new SkUiImage { HeightRequest = 20 };
        SemanticProperties.SetDescription(image, "Company logo");
        var heading = new SkUiLabel { Text = "Settings" };
        SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level1);
        var button = new SkUiButton { Text = "→" };
        SemanticProperties.SetDescription(button, "Next page");
        SemanticProperties.SetHint(button, "Shows the next results");
        var hidden = new SkUiLabel { Text = "Decoration" };
        AutomationProperties.SetIsInAccessibleTree(hidden, false);
        var excluded = Stack(new SkUiButton { Text = "Hidden" });
        AutomationProperties.SetExcludedWithChildren(excluded, true);
        var tree = Tree(Stack(image, heading, button, hidden, excluded));

        Assert.Equal("Image:Company logo | Text:Settings | Button:Next page", Describe(tree));
        var nodes = tree.Flatten().ToList();
        Assert.Equal(SemanticHeadingLevel.Level1, nodes[1].HeadingLevel);
        Assert.Equal("Shows the next results", nodes[2].Hint);
    }

    [Fact]
    public void TappableContainerReadsItsTextAsOneElement()
    {
        var card = new SkUiBorder
        {
            Content = Stack(
                new SkUiLabel { Text = "Order 42" },
                new SkUiLabel { Text = "Shipped" },
                new SkUiButton { Text = "Track" })
        };
        card.Tapped += (_, _) => { };
        var tree = Tree(Stack(card));

        var element = Assert.Single(tree.Root.Children);
        Assert.Equal(SkUiSemanticsRole.None, element.Role);
        Assert.Equal("Order 42, Shipped", element.Label);
        Assert.Equal(SkUiSemanticsActions.Activate, element.Actions);
        // The button inside stays an element of its own, a child of the card.
        var button = Assert.Single(element.Children);
        Assert.Equal("Track", button.Label);
    }

    [Fact]
    public void DescribedContainerReplacesItsText()
    {
        var group = Row(new SkUiLabel { Text = "4.5" }, new SkUiLabel { Text = "★★★★☆" });
        SemanticProperties.SetDescription(group, "Rated 4.5 of 5");
        var tree = Tree(Stack(group));

        Assert.Equal("None:Rated 4.5 of 5", Describe(tree));
    }

    [Fact]
    public void RadioButtonReadsItsViewContent()
    {
        var radio = new SkUiRadioButton { Content = new SkUiLabel { Text = "Yearly plan" } };
        var tree = Tree(Stack(radio));

        Assert.Equal("RadioButton:Yearly plan", Describe(tree));
    }

    [Fact]
    public void CoreNodesReportTheirSemantics()
    {
        var button = new SkUiCoreButton { Text = "Core action" };
        var described = new SkUiCoreImage().SetSemanticDescription("Avatar").SetHeight(20);
        var stack = new SkUiCoreVerticalStackLayout();
        stack.Add(new SkUiCoreLabel().SetText("Core text"));
        stack.Add(button);
        stack.Add(described);
        stack.Add(new SkUiCoreSwitch().SetIsChecked(true));
        stack.Add(new SkUiCoreSlider().SetValue(0.5));
        var tree = Tree(new SkUiCoreHost().SetContent(stack));

        Assert.Equal("Text:Core text | Button:Core action | Image:Avatar | Switch: | Slider:", Describe(tree));
        Assert.Equal(SkUiCheckState.Checked, tree.Flatten().ElementAt(3).CheckState);
    }

    [Fact]
    public void BoundsAreSurfaceCoordinatesOfTheVisiblePart()
    {
        var label = new SkUiLabel { Text = "Inner", HeightRequest = 30, WidthRequest = 100, Margin = new Thickness(10, 20, 0, 0), HorizontalOptions = LayoutOptions.Start };
        var content = Stack(new SkUiBox { HeightRequest = 50 }, label);
        var scroll = new SkUiScrollView { Content = content, HeightRequest = 100, Margin = new Thickness(5) };
        var root = Stack(scroll);
        var tree = Tree(root);

        var node = tree.Flatten().Single(n => n.Label == "Inner");
        Assert.Equal(new Rect(15, 75, 100, 30), node.Bounds);

        // Scrolled by 80: the label (at 70 in the content) moves up and its top 10 DIPs are cut by the viewport.
        content.Children.Add(new SkUiBox { HeightRequest = 400 });
        SkUiTestHelpers.Arrange(root, 300, 400);
        scroll.ScrollToAsync(0, 80, animated: false);
        tree = SkUiSemanticsTree.Build(root);
        node = tree.Flatten().Single(n => n.Label == "Inner");
        Assert.Equal(new Rect(15, 5, 100, 20), node.Bounds);

        // Scrolled past it: no longer an element.
        scroll.ScrollToAsync(0, 200, animated: false);
        Assert.DoesNotContain(SkUiSemanticsTree.Build(root).Flatten(), n => n.Label == "Inner");
    }

    [Fact]
    public void ScrollViewIsAContainerWithPageActions()
    {
        var content = new SkUiVerticalStackLayout();
        for (var index = 0; index < 20; index++)
            content.Children.Add(new SkUiLabel { Text = $"Row {index}", HeightRequest = 40 });
        var scroll = new SkUiScrollView { Content = content, HeightRequest = 200 };
        var root = Stack(scroll);
        var tree = Tree(root);

        var scroller = Assert.Single(tree.Root.Children);
        Assert.Equal(SkUiSemanticsRole.ScrollView, scroller.Role);
        Assert.Equal(SkUiSemanticsActions.ScrollForward, scroller.Actions);
        Assert.Equal(5, scroller.Children.Count); // only the visible rows

        using var surface = new SkUiTestSurface(root, 300, 400);
        surface.Frame();
        var owner = new SkUiSemanticsOwner(root);
        Assert.True(owner.Perform(scroller.Id, SkUiSemanticsActions.ScrollForward));
        surface.Frame(0);
        surface.Frame(1000);
        Assert.Equal(160, scroll.ScrollY, 0.5); // a page: 80 % of the viewport
        Assert.Equal(SkUiSemanticsActions.ScrollForward | SkUiSemanticsActions.ScrollBackward, owner.Tree.Find(scroller.Id)!.Actions);
    }

    [Fact]
    public void ActivateRunsTheTapOfEachControl()
    {
        var clicks = 0;
        var button = new SkUiButton { Text = "Go" };
        button.Clicked += (_, _) => clicks++;
        var check = new SkUiCheckBox();
        var radio = new SkUiRadioButton { Content = "A" };
        var slider = new SkUiSlider { Maximum = 100, Value = 50 };
        var coreToggled = 0;
        var coreSwitch = new SkUiCoreSwitch();
        coreSwitch.CheckedChanged += (_, _) => coreToggled++;
        var root = Stack(button, check, radio, slider, new SkUiCoreHost().SetContent(coreSwitch));
        SkUiTestHelpers.Arrange(root, 300, 400);
        var owner = new SkUiSemanticsOwner(root);
        int Id(object node) => owner.Tree.Find((ISkUiAccessibleNode)node)!.Id;

        Assert.True(owner.Perform(Id(button), SkUiSemanticsActions.Activate));
        Assert.True(owner.Perform(Id(check), SkUiSemanticsActions.Activate));
        Assert.True(owner.Perform(Id(radio), SkUiSemanticsActions.Activate));
        Assert.True(owner.Perform(Id(slider), SkUiSemanticsActions.Increment));
        Assert.True(owner.Perform(Id(coreSwitch), SkUiSemanticsActions.Activate));
        Assert.True(owner.SetValue(Id(slider), 20));

        Assert.Equal(1, clicks);
        Assert.True(check.IsChecked);
        Assert.True(radio.IsChecked);
        Assert.Equal(20, slider.Value);
        Assert.Equal(1, coreToggled);
        // Actions a node does not have, or a disabled node's, do nothing.
        Assert.False(owner.Perform(Id(button), SkUiSemanticsActions.Increment));
        button.IsEnabled = false;
        Assert.False(owner.Perform(Id(button), SkUiSemanticsActions.Activate));
        Assert.Equal(1, clicks);
    }

    [Fact]
    public void DisabledCommandReadsAsDisabledButton()
    {
        var button = new SkUiButton { Text = "Send", Command = new Command(() => { }, () => false) };
        var core = new SkUiCoreButton { Text = "Core" }.SetCommand(new Command(() => { }, () => false));
        var tree = Tree(Stack(button, new SkUiCoreHost().SetContent(core)));

        Assert.All(tree.Flatten(), node =>
        {
            Assert.Equal(SkUiSemanticsRole.Button, node.Role);
            Assert.False(node.IsEnabled);
            Assert.Equal(SkUiSemanticsActions.None, node.EnabledActions);
        });
    }

    [Fact]
    public void DisabledAncestorsDisableTheirSubtree()
    {
        var check = new SkUiCoreCheckBox();
        var host = new SkUiCoreHost().SetContent(check);
        var button = new SkUiButton { Text = "Inner" };
        var group = Stack(button);
        var root = Stack(host, group);
        SkUiTestHelpers.Arrange(root, 300, 400);
        var owner = new SkUiSemanticsOwner(root);
        host.IsEnabled = false;
        group.IsEnabled = false;

        Assert.All(owner.Tree.Flatten(), node => Assert.False(node.IsEnabled));
        Assert.False(owner.Perform(owner.Tree.Find(check)!.Id, SkUiSemanticsActions.Activate));
        Assert.False(check.IsChecked);
    }

    [Fact]
    public void TwoAxisScrollerAdvertisesOnlyPagesItCanScroll()
    {
        var content = new SkUiBox { WidthRequest = 600, HeightRequest = 300 };
        var scroll = new SkUiScrollView { Content = content, Orientation = ScrollOrientation.Both, HeightRequest = 200, WidthRequest = 200, HorizontalOptions = LayoutOptions.Start };
        var root = Stack(scroll);
        SkUiTestHelpers.Arrange(root, 300, 400);
        scroll.ScrollToAsync(0, 100, animated: false); // at the vertical end, room left horizontally

        var node = Assert.Single(SkUiSemanticsTree.Build(root).Root.Children);
        Assert.Equal(SkUiSemanticsActions.ScrollBackward, node.Actions);
    }

    [Fact]
    public void HostedNativeViewsAreLeftToThePlatform()
    {
        var tree = Tree(Stack(new SkUiMauiContentView { Content = new Entry(), HeightRequest = 40 }));

        var node = Assert.Single(tree.Root.Children);
        Assert.True(node.IsNative);
        Assert.Same(node, tree.HitTest(new Point(5, 5)));
    }

    [Fact]
    public void HitTestFindsTheFrontmostInnermostElement()
    {
        var button = new SkUiButton { Text = "Inner", HeightRequest = 40 };
        var card = new SkUiBorder { Content = Stack(new SkUiLabel { Text = "Card", HeightRequest = 40 }, button) };
        card.Tapped += (_, _) => { };
        var tree = Tree(Stack(card));

        Assert.Equal("Inner", tree.HitTest(new Point(10, 60))?.Label);
        Assert.Equal("Card", tree.HitTest(new Point(10, 10))?.Label);
        Assert.Null(tree.HitTest(new Point(10, 300)));
    }

    [Fact]
    public void IdsStayAcrossRebuildsAndChangesAreReported()
    {
        var label = new SkUiLabel { Text = "One" };
        var root = Stack(label, new SkUiButton { Text = "Two" });
        SkUiTestHelpers.Arrange(root, 300, 400);
        var owner = new SkUiSemanticsOwner(root);
        var first = owner.Tree;
        var reports = new List<(bool Structure, IReadOnlyList<int> Ids)>();
        owner.Changed += (structure, ids) => reports.Add((structure, ids));

        label.Text = "Uno";
        owner.Invalidate();
        owner.Flush();
        Assert.Equal(first.Find(label)!.Id, owner.Tree.Find(label)!.Id);
        var report = Assert.Single(reports);
        Assert.False(report.Structure);
        Assert.Equal([first.Find(label)!.Id], report.Ids);

        root.Children.Add(new SkUiLabel { Text = "Three" });
        SkUiTestHelpers.Arrange(root, 300, 400);
        owner.Invalidate();
        owner.Flush();
        Assert.True(reports[^1].Structure);
    }

    [Fact]
    public void SemanticPropertyChangesInvalidateTheSurface()
    {
        var button = new SkUiButton { Text = "Old" };
        var root = Stack(button);
        SkUiTestHelpers.Arrange(root, 300, 400);
        var owner = new SkUiSemanticsOwner(root);
        _ = owner.Tree;

        SemanticProperties.SetDescription(button, "New");
        Assert.Equal("New", owner.Tree.Find(button)!.Label);
    }

    [Fact]
    public void SetSemanticFocusReachesTheBridge()
    {
        var button = new SkUiButton { Text = "Target" };
        var root = Stack(button);
        SkUiTestHelpers.Arrange(root, 300, 400);
        var owner = new SkUiSemanticsOwner(root);
        int? requested = null;
        root.SemanticFocusRequested = id => requested = id;

        button.SetSemanticFocus();

        Assert.Equal(owner.Tree.Find(button)!.Id, requested);
    }

    [Fact]
    public void TagIsKeptAndIgnored()
    {
        var model = new object();
        var view = new SkUiButton { Tag = model };
        var node = new SkUiCoreLabel().SetTag(model);

        Assert.Same(model, view.Tag);
        Assert.Same(model, node.Tag);
        Assert.Same(view, view.SetTag(null));
        Assert.Null(view.Tag);
    }
}
