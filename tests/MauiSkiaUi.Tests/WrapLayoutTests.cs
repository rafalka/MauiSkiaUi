using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary><see cref="SkUiWrapLayout"/> and <see cref="SkUiCoreWrapLayout"/> (one shared engine).</summary>
public class WrapLayoutTests
{
    private static readonly Size[] Sizes = [new(40, 10), new(50, 20), new(30, 15), new(60, 10)];

    private static SkUiWrapLayout SkWrap(out FixedSkView[] children)
    {
        var wrap = new SkUiWrapLayout { Spacing = 5, RowSpacing = 4, Padding = new Thickness(2) };
        children = Sizes.Select(size => new FixedSkView(size)).ToArray();
        foreach (var child in children)
            wrap.Children.Add(child);
        return wrap;
    }

    private static SkUiCoreWrapLayout CoreWrap(out FixedCoreNode[] children)
    {
        var wrap = new SkUiCoreWrapLayout().SetSpacing(5).SetRowSpacing(4).SetPadding(new Thickness(2));
        children = Sizes.Select(size => new FixedCoreNode(size)).ToArray();
        foreach (var child in children)
            wrap.Add(child);
        return wrap;
    }

    [Fact]
    public void WrapsAcrossWidthWithSpacingAndRowHeights()
    {
        var wrap = SkWrap(out var children);
        var measured = ((IView)wrap).Measure(120, 200);
        // Content 116: row 1 = 40 + 5 + 50 (the 30 would reach 130), row 2 = 30 + 5 + 60.
        Assert.Equal(new Size(99, 43), measured);
        ((IView)wrap).Arrange(new Rect(0, 0, 120, 200));
        Assert.Equal(new Rect(2, 2, 40, 20), children[0].Frame); // Fill: as tall as its row
        Assert.Equal(new Rect(47, 2, 50, 20), children[1].Frame);
        Assert.Equal(new Rect(2, 26, 30, 15), children[2].Frame);
        Assert.Equal(new Rect(37, 26, 60, 15), children[3].Frame);

        SkUiTestHelpers.Arrange(wrap, 400, 200);
        Assert.Equal(new Rect(2 + 40 + 5 + 50 + 5 + 30 + 5, 2, 60, 20), children[3].Frame);
    }

    [Fact]
    public void ChildrenAlignInsideTheirRow()
    {
        var wrap = SkWrap(out var children);
        children[0].VerticalOptions = LayoutOptions.End;
        children[2].VerticalOptions = LayoutOptions.Center;
        SkUiTestHelpers.Arrange(wrap, 120, 200);
        Assert.Equal(new Rect(2, 12, 40, 10), children[0].Frame);
        Assert.Equal(new Rect(2, 26, 30, 15), children[2].Frame);
    }

    [Fact]
    public void AWideChildStartingARowDoesNotLeaveAnEmptyRow()
    {
        var wrap = new SkUiWrapLayout { RowSpacing = 4 };
        var wide = new FixedSkView(new Size(300, 10));
        var small = new FixedSkView(new Size(10, 10));
        wrap.Children.Add(wide);
        wrap.Children.Add(small);
        SkUiTestHelpers.Arrange(wrap, 100, 100);
        Assert.Equal(new Rect(0, 0, 100, 10), wide.Frame);
        Assert.Equal(new Rect(0, 14, 10, 10), small.Frame);
    }

    [Fact]
    public void RewrapsWhenAChildSizeChangesAndSkipsCollapsedChildren()
    {
        var wrap = SkWrap(out var children);
        SkUiTestHelpers.Arrange(wrap, 120, 200);
        Assert.Equal(2, children[2].Frame.X);

        children[1].WidthRequest = 20;
        SkUiTestHelpers.Arrange(wrap, 120, 200);
        Assert.Equal(new Rect(72, 2, 30, 20), children[2].Frame);

        children[0].IsVisible = false;
        SkUiTestHelpers.Arrange(wrap, 120, 200);
        Assert.Equal(new Rect(2, 2, 20, 20), children[1].Frame);
        Assert.Equal(new Rect(27, 2, 30, 20), children[2].Frame);
        Assert.Equal(new Rect(2, 26, 60, 10), children[3].Frame);
    }

    [Theory]
    [InlineData(120)]
    [InlineData(90)]
    [InlineData(60)]
    [InlineData(400)]
    public void CoreFramesMatchSkUiFrames(double width)
    {
        var skia = SkWrap(out var skChildren);
        var core = CoreWrap(out var coreChildren);
        skChildren[1].VerticalOptions = LayoutOptions.Center;
        coreChildren[1].SetVerticalAlignment(LayoutAlignment.Center);
        SkUiTestHelpers.Arrange(skia, width, 200);
        var measured = core.Measure(width, 200);
        core.Arrange(new Rect(0, 0, width, 200));
        Assert.Equal(((IView)skia).Measure(width, 200), measured);
        for (var index = 0; index < Sizes.Length; index++)
            Assert.Equal(skChildren[index].Frame, coreChildren[index].Frame);
    }

    [Fact]
    public void ArrangedNarrowerThanMeasuredMeasuresTheChildrenAgain()
    {
        // Measured wide (one row, single-line labels), then arranged into a narrower slot: the labels must wrap
        // against the real width and the rows must follow.
        var wrap = new SkUiWrapLayout { RowSpacing = 4 };
        var first = new WrappingSkView(150, 10);
        var second = new WrappingSkView(150, 10);
        wrap.Children.Add(first);
        wrap.Children.Add(second);
        ((IView)wrap).Measure(400, 200);
        ((IView)wrap).Arrange(new Rect(0, 0, 100, 200));
        Assert.Equal(new Rect(0, 0, 100, 20), first.Frame);
        Assert.Equal(new Rect(0, 24, 100, 20), second.Frame);

        var core = new SkUiCoreWrapLayout().SetRowSpacing(4);
        var coreFirst = new WrappingCoreNode(150, 10);
        var coreSecond = new WrappingCoreNode(150, 10);
        core.Add(coreFirst).Add(coreSecond);
        core.Measure(400, 200);
        core.Arrange(new Rect(0, 0, 100, 200));
        Assert.Equal(first.Frame, coreFirst.Frame);
        Assert.Equal(second.Frame, coreSecond.Frame);

        // Measured unconstrained, arranged wide enough for every child: nothing to re-measure, same rows.
        var loose = new SkUiWrapLayout();
        var a = new WrappingSkView(60, 10);
        var b = new WrappingSkView(60, 10);
        loose.Children.Add(a);
        loose.Children.Add(b);
        ((IView)loose).Measure(double.PositiveInfinity, 200);
        ((IView)loose).Arrange(new Rect(0, 0, 100, 200));
        Assert.Equal(new Rect(0, 0, 60, 10), a.Frame);
        Assert.Equal(new Rect(0, 10, 60, 10), b.Frame);
    }

    [Fact]
    public void CoreHiddenChildrenTakeNoSpace()
    {
        var core = CoreWrap(out var children);
        children[0].IsVisible = false;
        core.Measure(120, 200);
        core.Arrange(new Rect(0, 0, 120, 200));
        Assert.Equal(Rect.Zero, children[0].Frame);
        Assert.Equal(2, children[1].Frame.X);
    }

    [Fact]
    public void RowsRunRightToLeftOnBothLayers()
    {
        var wrap = SkWrap(out var children);
        var root = new SkUiContentView { FlowDirection = FlowDirection.RightToLeft, Content = wrap };
        SkUiTestHelpers.Arrange(root, 120, 200);
        Assert.Equal(120 - 2 - 40, children[0].Frame.X);
        Assert.Equal(120 - 47 - 50, children[1].Frame.X);

        var core = CoreWrap(out var coreChildren);
        var host = new SkUiCoreHost { FlowDirection = FlowDirection.RightToLeft }.SetContent(core);
        SkUiTestHelpers.Arrange(host, 120, 200);
        Assert.Equal(children[0].Frame, coreChildren[0].Frame);
        Assert.Equal(children[1].Frame, coreChildren[1].Frame);
    }
}
