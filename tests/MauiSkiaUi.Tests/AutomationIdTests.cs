using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// M3: every <c>AutomationId</c> set on a drawn node can be found by a UI-test driver. Item containers take their item root's
/// id (always); with <see cref="SkUiAccessibility.ExposeAutomationIds"/> absorbed (a), item (b) and hidden (c) nodes become
/// id-only elements. Labels get a fixed size: without an installed font (Linux CI) text measures empty.
/// </summary>
[Collection(RuntimeXamlCollection.Name)]
public class AutomationIdTests
{
    private const string RowId = "row";
    private const string StatusId = "status";

    private static SkUiLabel Label(string text, string? automationId = null) =>
        new() { Text = text, WidthRequest = 80, HeightRequest = 20, AutomationId = automationId };

    private static SkUiVerticalStackLayout Stack(params ISkUiView[] children)
    {
        var stack = new SkUiVerticalStackLayout();
        foreach (var child in children)
            stack.Children.Add(child);
        return stack;
    }

    private static SkUiSemanticsTree Tree(SkUiView root, bool expose)
    {
        SkUiTestHelpers.Arrange(root, 300, 400);
        return SkUiSemanticsTree.Build(root, exposeAutomationIds: expose);
    }

    private static SkUiSemanticsNode? ById(SkUiSemanticsTree tree, string automationId) =>
        tree.Flatten().SingleOrDefault(node => node.AutomationId == automationId);

    [Fact]
    public void ExposingIdsIsOffByDefault() => Assert.False(SkUiAccessibility.ExposeAutomationIds);

    [Fact]
    public void AnAbsorbedLabelIsAnIdOnlyElementOfTheRowWhenExposed()
    {
        SkUiView Card()
        {
            var card = new SkUiBorder { Content = Stack(Label("Order 42"), Label("Shipped", StatusId)) };
            card.Tapped += (_, _) => { };
            return Stack(card);
        }

        var plain = Tree(Card(), expose: false);
        Assert.Null(ById(plain, StatusId));
        Assert.Equal("Order 42, Shipped", Assert.Single(plain.Flatten()).Label);

        var exposed = Tree(Card(), expose: true);
        var row = Assert.Single(exposed.Root.Children);
        Assert.Equal("Order 42, Shipped", row.Label); // the text is still read with the row
        var status = Assert.Single(row.Children);
        Assert.Equal(StatusId, status.AutomationId);
        Assert.True(status.IsIdOnly);
        Assert.Null(status.Label);
        Assert.Equal(SkUiSemanticsActions.None, status.Actions);
        Assert.False(status.IsFocusable);
        Assert.Equal((80d, 20d), (status.Bounds.Width, status.Bounds.Height)); // where the label is drawn
    }

    [Fact]
    public void DecorativeAndHiddenNodesAreIdOnlyElementsWhenExposed()
    {
        SkUiView Page()
        {
            var hidden = Label("Decoration", "hidden");
            AutomationProperties.SetIsInAccessibleTree(hidden, false);
            var excluded = Stack(Label("Gone", "excluded"));
            AutomationProperties.SetExcludedWithChildren(excluded, true);
            return Stack(
                new SkUiBox { HeightRequest = 10, AutomationId = "divider" },
                new SkUiImage { HeightRequest = 10, AutomationId = "logo" },
                Stack(new SkUiButton { Text = "Save", AutomationId = "save" }).WithAutomationId("form"),
                hidden,
                excluded,
                new SkUiCoreHost().SetContent(new SkUiCoreBox().SetHeight(10).SetAutomationId("core-box")));
        }

        var plain = Tree(Page(), expose: false);
        Assert.Equal(["save"], plain.Flatten().Select(node => node.AutomationId));

        var exposed = Tree(Page(), expose: true);
        Assert.Equal(["divider", "logo", "form", "save", "hidden", "core-box"], exposed.Flatten().Select(node => node.AutomationId));
        // An id-only container keeps its subtree's elements under it.
        var form = ById(exposed, "form")!;
        Assert.True(form.IsIdOnly);
        Assert.Equal("save", Assert.Single(form.Children).AutomationId);
        Assert.False(ById(exposed, "save")!.IsIdOnly);
        // Excluded with children stays excluded, as MAUI's native views.
        Assert.Null(ById(exposed, "excluded"));
    }

    [Fact]
    public void TheSurfaceRootIsNotRepeated()
    {
        // The root's id is on the surface's native view (MAUI maps it there).
        var root = Stack(Label("Title"));
        root.AutomationId = "page";
        Assert.Null(ById(Tree(root, expose: true), "page"));
    }

    [Fact]
    public void IdOnlyElementsAreNotHitAndNotFoundBySource()
    {
        var button = new SkUiButton { Text = "Save", HeightRequest = 40 };
        var box = new SkUiBox { HeightRequest = 40, AutomationId = "spacer" };
        var tree = Tree(Stack(Stack(button).WithAutomationId("group"), box), expose: true);

        Assert.Equal("Save", tree.HitTest(new Point(10, 10))?.Label); // inside the id-only group
        Assert.Null(tree.HitTest(new Point(10, 60))); // the id-only box: the surface behind it
        Assert.NotNull(ById(tree, "spacer"));
        Assert.Null(tree.Find(box)); // not an element screen readers can focus
    }

    [Fact]
    public void ACollectionItemContainerTakesItsItemRootsId()
    {
        SkUiCollectionView List(Func<SkUiView> template, SkUiSelectionMode mode)
        {
            var list = new SkUiCollectionView
            {
                ItemsSource = new[] { "A", "B" },
                ItemTemplate = new DataTemplate(template),
                SelectionMode = mode,
                VerticalScrollBarVisibility = ScrollBarVisibility.Never
            };
            return list;
        }

        // A selectable item absorbs its root (and its text): the container carries the root's id, without the option.
        var selectable = Tree(List(() => Stack(Label("Item"), Label("Detail", StatusId)).WithAutomationId(RowId), SkUiSelectionMode.Single), expose: false);
        var items = selectable.Flatten().Where(node => node.AutomationId == RowId).ToList();
        Assert.Equal(2, items.Count);
        Assert.All(items, item => Assert.Equal("Item, Detail", item.Label));
        Assert.All(items, item => Assert.False(item.IsIdOnly));
        // Inner ids are absorbed: id-only children with the option.
        Assert.DoesNotContain(selectable.Flatten(), node => node.AutomationId == StatusId);
        var exposed = Tree(List(() => Stack(Label("Item"), Label("Detail", StatusId)).WithAutomationId(RowId), SkUiSelectionMode.Single), expose: true);
        Assert.Equal(2, exposed.Flatten().Count(node => node is { AutomationId: StatusId, IsIdOnly: true }));
        Assert.Equal(2, exposed.Flatten().Count(node => node is { AutomationId: RowId, IsIdOnly: false })); // not repeated

        // An item root that is an element of its own (a button) keeps its id; the container does not take it.
        var buttons = Tree(List(() => new SkUiButton { Text = "Open", AutomationId = RowId }, SkUiSelectionMode.Single), expose: false);
        Assert.All(buttons.Flatten().Where(node => node.AutomationId == RowId), node => Assert.Equal(SkUiSemanticsRole.Button, node.Role));
        Assert.Equal(2, buttons.Flatten().Count(node => node.AutomationId == RowId));
    }

    [Fact]
    public void AnExpanderHeaderTakesItsHeadersId()
    {
        var expander = new SkUiExpander { Header = Label("Details", "details-header"), Content = new SkUiBox { HeightRequest = 30 } };
        var tree = Tree(Stack(expander), expose: false);
        var header = tree.Find(expander.HeaderHost)!;
        Assert.Equal(("details-header", "Details"), (header.AutomationId, header.Label));
    }
}

internal static class AutomationIdTestExtensions
{
    public static T WithAutomationId<T>(this T view, string automationId) where T : SkUiView
    {
        view.AutomationId = automationId;
        return view;
    }
}
