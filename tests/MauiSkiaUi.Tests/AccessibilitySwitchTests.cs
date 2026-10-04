using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Switching drawn accessibility off (app-wide <see cref="SkUiAccessibility.IsEnabled"/>, per view
/// <see cref="SkUiView.IsAccessibilityEnabled"/>), the cost of the tree when it is on (subtrees out of view are not walked)
/// and coalesced change reports. In the global collection: the switch and the report delays are process-wide.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class AccessibilitySwitchTests : IDisposable
{
    public void Dispose()
    {
        SkUiAccessibility.IsEnabled = true;
        SkUiSemanticsOwner.ReportDelay = TimeSpan.FromMilliseconds(100);
        SkUiSemanticsOwner.MaxReportDelay = TimeSpan.FromMilliseconds(500);
    }

    private static SkUiVerticalStackLayout Stack(params ISkUiView[] children)
    {
        var stack = new SkUiVerticalStackLayout();
        foreach (var child in children)
            stack.Children.Add(child);
        SkUiTestHelpers.Arrange(stack, 300, 600);
        return stack;
    }

    [Fact]
    public void AppWideSwitchTurnsFocusOffAndBackOn()
    {
        var button = new SkUiButton { Text = "Go" };
        var root = Stack(button);
        Assert.True(button.Focus());

        SkUiAccessibility.IsEnabled = false;
        Assert.False(button.IsFocused); // cleared at once
        Assert.False(button.Focus());
        Assert.False(root.FocusManager.KeyDown(SkUiKey.Tab));
        Assert.False(root.FocusManager.HasFocusableNodes);

        SkUiAccessibility.IsEnabled = true;
        Assert.True(root.FocusManager.KeyDown(SkUiKey.Tab));
        Assert.True(button.IsFocused);
    }

    [Fact]
    public void ViewSwitchHidesItsSubtreeFromScreenReadersAndKeyboard()
    {
        var inside = new SkUiButton { Text = "Inside" };
        var outside = new SkUiButton { Text = "Outside" };
        var group = Stack(inside);
        var root = Stack(group, outside);
        inside.Focus();

        group.IsAccessibilityEnabled = false;

        Assert.False(inside.IsFocused);
        Assert.Equal(["Outside"], SkUiSemanticsTree.Build(root).Flatten().Select(node => node.Label));
        Assert.Same(outside, Assert.Single(root.FocusManager.GetTabOrder()));
        Assert.False(inside.Focus());

        // On a surface root: nothing is read or focusable.
        root.IsAccessibilityEnabled = false;
        Assert.Empty(SkUiSemanticsTree.Build(root).Flatten());
        Assert.False(root.FocusManager.HasFocusableNodes);
    }

    [Fact]
    public void SubtreesOutOfViewAreNotWalked()
    {
        var content = new SkUiVerticalStackLayout();
        var rows = new List<CountingLabel>();
        for (var index = 0; index < 100; index++)
        {
            var row = new CountingLabel { Text = $"Row {index}", HeightRequest = 40 };
            rows.Add(row);
            content.Children.Add(row);
        }
        var root = Stack(new SkUiScrollView { Content = content, HeightRequest = 200 });

        var tree = SkUiSemanticsTree.Build(root);

        Assert.Equal(5, tree.Flatten().Count(node => node.Role == SkUiSemanticsRole.Text));
        Assert.Equal(5, rows.Count(row => row.Populated > 0)); // the 95 rows below the viewport were not asked
    }

    [Fact]
    public void ChangesAreReportedOnceTheTreeIsQuiet()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var label = new SkUiLabel { Text = "A", WidthRequest = 80, HeightRequest = 20 };
        var root = Stack(label);
        var owner = new SkUiSemanticsOwner(root);
        var reports = 0;
        owner.Changed += (_, _) => reports++;

        // Still changing (a long quiet period is required, the ceiling not reached): the report waits.
        SkUiSemanticsOwner.ReportDelay = TimeSpan.FromHours(1);
        SkUiSemanticsOwner.MaxReportDelay = TimeSpan.FromHours(2);
        label.Text = "B";
        owner.Invalidate();
        TestDispatcherProvider.RunDelayed();
        Assert.Equal(0, reports);

        // The ceiling: reported even while changes keep coming.
        SkUiSemanticsOwner.MaxReportDelay = TimeSpan.Zero;
        TestDispatcherProvider.RunDelayed();
        Assert.Equal(1, reports);

        // Quiet: reported at once.
        SkUiSemanticsOwner.ReportDelay = TimeSpan.Zero;
        label.Text = "C";
        owner.Invalidate();
        TestDispatcherProvider.RunDelayed();
        Assert.Equal(2, reports);
    }

    private sealed class CountingLabel : SkUiLabel
    {
        public int Populated { get; private set; }

        protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
        {
            Populated++;
            base.OnPopulateSemantics(info);
        }
    }
}
