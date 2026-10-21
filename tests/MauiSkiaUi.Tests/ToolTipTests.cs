using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Tooltips on drawn views (U2 in ImplementationPlan.md): the drawn view under the pointer with
/// <c>ToolTipProperties.Text</c> (a Core node's <c>ToolTip</c>), or its nearest ancestor with one, and its bounds, which
/// the platforms show (Apple asks by position, Windows follows hover).
/// </summary>
public class ToolTipTests
{
    private readonly SkUiAbsoluteLayout _root = new();
    private readonly SkUiBox _box = new();
    private readonly SkUiContentView _container = new() { Content = new SkUiBox() };
    private readonly SkUiCoreBox _coreBox = new();

    public ToolTipTests()
    {
        Place(_box, new Rect(10, 10, 40, 20), "box");
        Place(_container, new Rect(60, 10, 60, 40), "container");
        Place(new SkUiCoreHost().SetContent(_coreBox.SetToolTip("core")), new Rect(130, 10, 50, 30), null);
        ToolTipProperties.SetText(_root, "root");
        SkUiTestHelpers.Arrange(_root, 200, 100);
    }

    private void Place(SkUiView view, Rect bounds, string? toolTip)
    {
        SkUiAbsoluteLayout.SetLayoutBounds(view, bounds);
        if (toolTip is not null)
            ToolTipProperties.SetText(view, toolTip);
        _root.Add(view);
    }

    private SkUiToolTipTarget? At(double x, double y) => _root.Router.ToolTipAt(new Point(x, y));

    [Fact]
    public void ShowsTheViewsUnderThePointerOrTheNearestAncestors()
    {
        Assert.Equal(new SkUiToolTipTarget("box", new Rect(10, 10, 40, 20)), At(20, 15));
        // The container's child has none: the container's, over the container's bounds.
        Assert.Equal(new SkUiToolTipTarget("container", new Rect(60, 10, 60, 40)), At(70, 20));
        Assert.Equal(new SkUiToolTipTarget("core", new Rect(130, 10, 50, 30)), At(140, 20));
        Assert.Equal(new SkUiToolTipTarget("root", new Rect(0, 0, 200, 100)), At(5, 90));

        _root.ClearValue(ToolTipProperties.TextProperty);
        Assert.Null(At(5, 90));
        _coreBox.SetToolTip("");
        Assert.Null(At(140, 20));
    }

    [Fact]
    public void FollowsTransformsInputTransparencyAndTheEnabledState()
    {
        _box.TranslationX = 5;
        Assert.Equal(new SkUiToolTipTarget("box", new Rect(15, 10, 40, 20)), At(20, 15));
        Assert.Equal("root", At(12, 15)?.Text);

        // A disabled view is still the one under the pointer (as for a press); an input-transparent one is not.
        _box.IsEnabled = false;
        Assert.Equal("box", At(20, 15)?.Text);
        _box.InputTransparent = true;
        Assert.Equal("root", At(20, 15)?.Text);
    }

    [Fact]
    public void HoverReportsChangesOfTheShownTooltip()
    {
        var changes = 0;
        _root.Router.HoveredToolTipChanged += () => changes++;

        Hover(20, 15);
        Assert.Equal(1, changes);
        Assert.Equal(new SkUiToolTipTarget("box", new Rect(10, 10, 40, 20)), _root.Router.HoveredToolTip);
        Hover(30, 20); // the same view: the platform's delay keeps running
        Assert.Equal(1, changes);
        Hover(70, 20);
        Assert.Equal(2, changes);
        Assert.Equal("container", _root.Router.HoveredToolTip?.Text);
        _root.Touch(new(0, SkUiTouchAction.HoverExited, Point.Zero));
        Assert.Equal(3, changes);
        Assert.Null(_root.Router.HoveredToolTip);
    }

    [Fact]
    public void SurfaceMapsThroughTheRootsTransform()
    {
        _root.TranslationX = 20;
        using var renderer = new SkUiFrameRenderer(_root, _ => { }, () => { }, () => { });
        Assert.Equal(new SkUiToolTipTarget("box", new Rect(30, 10, 40, 20)), renderer.ToolTipAtDips(new Point(35, 15)));

        _root.Touch(new(0, SkUiTouchAction.HoverMoved, new Point(70, 20)));
        Assert.Equal(new SkUiToolTipTarget("container", new Rect(80, 10, 60, 40)), renderer.HoveredToolTip);
    }

    private void Hover(double x, double y) => _root.Touch(new(0, SkUiTouchAction.HoverMoved, new Point(x, y)));

    [Fact]
    public void HoverIsTrackedWithoutSubscribers()
    {
        Hover(20, 15);
        Assert.Equal("box", _root.Router.HoveredToolTip?.Text);
    }

    [Fact]
    public void ATextChangedUnderARestingPointerIsReported()
    {
        var changes = 0;
        void Changed() => changes++;
        _root.Router.HoveredToolTipChanged += Changed;
        Hover(20, 15);
        ToolTipProperties.SetText(_box, "renamed");
        Assert.Equal(2, changes);
        Assert.Equal("renamed", _root.Router.HoveredToolTip?.Text);
        // A Core node's too.
        Hover(140, 20);
        _coreBox.SetToolTip("core renamed");
        Assert.Equal(4, changes);
        Assert.Equal("core renamed", _root.Router.HoveredToolTip?.Text);
        _root.Router.HoveredToolTipChanged -= Changed;
    }

    [Fact]
    public void AfterAPressTheSameViewsTooltipIsReportedAgainOnTheNextHover()
    {
        var changes = 0;
        void Changed() => changes++;
        _root.Router.HoveredToolTipChanged += Changed;
        Hover(20, 15);
        _root.Router.ResetHoveredToolTip();
        Hover(21, 15);
        Assert.Equal(2, changes);
        Assert.Equal("box", _root.Router.HoveredToolTip?.Text);
        _root.Router.HoveredToolTipChanged -= Changed;
    }

    [Fact]
    public void AViewThatLeftTheTreeUnderThePointerShowsNoTooltip()
    {
        Hover(20, 15);
        _root.Remove(_box);
        Assert.NotEqual("box", _root.Router.HoveredToolTip?.Text);
    }

    [Fact]
    public void BoundsMapThroughNestedParentsAndNotForANodeOutsideTheRoot()
    {
        _container.TranslationY = 4;
        Assert.Equal(new Rect(130, 10, 50, 30), _root.Router.MapToRoot(_coreBox));
        Assert.Equal(new Rect(60, 14, 60, 40), _root.Router.MapToRoot((SkUiView)_container.Content!));
        Assert.Equal(new Rect(0, 0, 200, 100), _root.Router.MapToRoot(_root));

        _root.Remove(_box);
        Assert.Null(_root.Router.MapToRoot(_box));
        Assert.Null(_root.Router.MapToRoot(new SkUiBox()));
    }

    [Fact]
    public void CoreToolTipRaisesPropertyChanged()
    {
        var node = new SkUiCoreBox();
        var changed = new List<string?>();
        node.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        node.ToolTip = "Delete";
        node.SetToolTip("Delete");
        Assert.Equal("Delete", node.ToolTip);
        Assert.Equal([nameof(SkUiCoreNode.ToolTip)], changed);
    }
}
