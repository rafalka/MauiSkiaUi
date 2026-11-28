using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Clearing pointer-over state when a subtree leaves walks it only while something is pointer-over
/// (<see cref="SkUiHover.PointerOverCount"/>); Core nodes count themselves. Zeroes the process-wide count, so the class
/// runs alone.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public class HoverCountTests
{
    [Fact]
    public void ACoreNodeUnderInputTransparentViewsIsClearedWhenRemoved()
    {
        var saved = SkUiHover.PointerOverCount;
        SkUiHover.PointerOverCount = 0;
        try
        {
            var box = new VisualStateTests.CountingCoreBox();
            box.SetWidth(100).SetHeight(40).SetBackground(Colors.Red);
            var stack = new MauiSkiaUi.Core.SkUiCoreVerticalStackLayout().Add(box);
            var host = new MauiSkiaUi.Core.SkUiCoreHost { InputTransparent = true }.SetContent(stack);
            host.SetValue(SkUiView.CascadeInputTransparentShared, false); // transparent itself, its Core content not
            var root = new SkUiContentView { Content = host, InputTransparent = true, CascadeInputTransparent = false };
            SkUiTestHelpers.Arrange(root, 100, 40);

            root.Touch(new(0, SkUiTouchAction.HoverMoved, new Point(10, 10)));
            Assert.Equal((true, false, false), (box.IsPointerOver, host.IsPointerOver, root.IsPointerOver));

            stack.Remove(box); // no SkUi view is pointer-over: the Core nodes count themselves
            Assert.False(box.IsPointerOver);
            Assert.Equal([true, false], box.Changes);
            root.Touch(new(0, SkUiTouchAction.HoverExited, Point.Zero));
            Assert.Equal(0, SkUiHover.PointerOverCount);
        }
        finally
        {
            SkUiHover.PointerOverCount += saved;
        }
    }
}
