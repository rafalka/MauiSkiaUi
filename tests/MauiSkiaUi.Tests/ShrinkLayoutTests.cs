using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>Shrink stacks on both layers and axes (one shared engine).</summary>
public class ShrinkLayoutTests
{
    private static SkUiHorizontalShrinkLayout Row(double spacing, params (double Width, bool CanShrink)[] children)
    {
        var row = new SkUiHorizontalShrinkLayout { Spacing = spacing };
        foreach (var (width, canShrink) in children)
        {
            var child = new WrappingSkView(width, 10);
            SkUiShrinkLayout.SetShrink(child, canShrink ? SkUiShrinkFactor.Auto : SkUiShrinkFactor.None);
            row.Children.Add(child);
        }
        return row;
    }

    private static SkUiCoreHorizontalShrinkLayout CoreRow(double spacing, params (double Width, bool CanShrink)[] children)
    {
        var row = new SkUiCoreHorizontalShrinkLayout();
        row.SetSpacing(spacing);
        foreach (var (width, canShrink) in children)
            row.Add(new WrappingCoreNode(width, 10), canShrink ? SkUiShrinkFactor.Auto : SkUiShrinkFactor.None);
        return row;
    }

    private static Rect FrameOf(SkUiLayout layout, int index) => ((SkUiView)layout.Children[index]).Frame;

    [Fact]
    public void WithoutOverflowItIsAPlainStack()
    {
        var row = Row(5, (30, true), (40, false));
        var measured = ((IView)row).Measure(200, 50);
        Assert.Equal(new Size(75, 10), measured);
        ((IView)row).Arrange(new Rect(0, 0, 200, 50));
        Assert.Equal(new Rect(0, 0, 30, 50), FrameOf(row, 0));
        Assert.Equal(new Rect(35, 0, 40, 50), FrameOf(row, 1));
    }

    [Fact]
    public void OnOverflowStretchChildrenShrinkInProportionAndFixedChildrenKeepTheirSize()
    {
        // Fixed icon, two shrinkable labels (the longer gets more space), fixed label.
        var row = Row(0, (32, false), (100, true), (200, true), (50, false));
        var measured = ((IView)row).Measure(300, 50);
        Assert.Equal(300, measured.Width, 3);
        // Shrunk labels wrap: 100 → 72.67 (2 lines), 200 → 145.33 (2 lines).
        Assert.Equal(20, measured.Height);
        ((IView)row).Arrange(new Rect(0, 0, 300, 50));
        Assert.Equal(32, FrameOf(row, 0).Width);
        Assert.Equal(218 * 100 / 300.0, FrameOf(row, 1).Width, 3);
        Assert.Equal(218 * 200 / 300.0, FrameOf(row, 2).Width, 3);
        Assert.Equal(new Rect(250, 0, 50, 50), FrameOf(row, 3));
    }

    [Fact]
    public void StretchChildrenBelowTheAverageKeepTheirSize()
    {
        var row = Row(10, (20, true), (400, true));
        SkUiTestHelpers.Arrange(row, 200, 30);
        Assert.Equal(20, FrameOf(row, 0).Width);
        Assert.Equal(new Rect(30, 0, 170, 30), FrameOf(row, 1));
    }

    [Fact]
    public void FixedChildrenAloneOverflowingKeepTheirSize()
    {
        var row = Row(0, (150, false), (100, false), (30, true));
        var measured = ((IView)row).Measure(200, 30);
        Assert.Equal(200, measured.Width); // clamped by the constraint; the content overflows (clipped)
        ((IView)row).Arrange(new Rect(0, 0, 200, 30));
        Assert.Equal(new Rect(0, 0, 150, 30), FrameOf(row, 0));
        Assert.Equal(new Rect(150, 0, 100, 30), FrameOf(row, 1));
        Assert.Equal(30, FrameOf(row, 2).Width);
    }

    [Fact]
    public void CollapsedChildrenAreIgnoredAndStretchChangesRelayout()
    {
        var row = Row(0, (60, false), (300, true), (40, false));
        ((View)row.Children[0]).IsVisible = false;
        SkUiTestHelpers.Arrange(row, 200, 30);
        Assert.Equal(new Rect(0, 0, 160, 30), FrameOf(row, 1));
        Assert.Equal(160, FrameOf(row, 2).X);

        SkUiShrinkLayout.SetShrink((BindableObject)row.Children[1], SkUiShrinkFactor.None);
        SkUiTestHelpers.Arrange(row, 200, 30);
        Assert.Equal(300, FrameOf(row, 1).Width); // not shrinkable: natural size, overflowing (the layout clips)
    }

    [Fact]
    public void VerticalStretchShortensStretchChildrenToFitTheHeight()
    {
        var column = new SkUiVerticalShrinkLayout { Spacing = 5 };
        var top = new FixedSkView(new Size(50, 30));
        var content = new FixedSkView(new Size(50, 200));
        var bottom = new FixedSkView(new Size(50, 30));
        SkUiShrinkLayout.SetShrink(content, SkUiShrinkFactor.Auto);
        column.Children.Add(top);
        column.Children.Add(content);
        column.Children.Add(bottom);
        SkUiTestHelpers.Arrange(column, 80, 120);
        Assert.Equal(new Rect(0, 0, 80, 30), top.Frame);
        Assert.Equal(new Rect(0, 35, 80, 50), content.Frame);
        Assert.Equal(new Rect(0, 90, 80, 30), bottom.Frame);

        SkUiTestHelpers.Arrange(column, 80, 400);
        Assert.Equal(200, content.Frame.Height);
    }

    [Fact]
    public void ArrangedInLessSpaceThanMeasuredSolvesAgain()
    {
        var row = Row(0, (100, true), (100, true));
        ((IView)row).Measure(double.PositiveInfinity, 50);
        ((IView)row).Arrange(new Rect(0, 0, 100, 50));
        Assert.Equal(50, FrameOf(row, 0).Width, 3);
        Assert.Equal(new Rect(50, 0, 50, 50), FrameOf(row, 1));
    }

    [Theory]
    [InlineData(300)]
    [InlineData(200)]
    [InlineData(120)]
    [InlineData(60)]
    public void CoreFramesMatchSkUiFrames(double width)
    {
        (double, bool)[] children = [(32, false), (100, true), (200, true), (50, false)];
        var skia = Row(4, children);
        var core = CoreRow(4, children);
        SkUiTestHelpers.Arrange(skia, width, 40);
        var measured = core.Measure(width, 40);
        core.Arrange(new Rect(0, 0, width, 40));
        Assert.Equal(((IView)skia).Measure(width, 40), measured);
        for (var index = 0; index < children.Length; index++)
            Assert.Equal(FrameOf(skia, index), core.Children[index].Frame);
    }

    [Fact]
    public void CoreVerticalStretchAndStretchFlags()
    {
        var column = new SkUiCoreVerticalShrinkLayout();
        var top = new FixedCoreNode(new Size(50, 30));
        var content = new FixedCoreNode(new Size(50, 200));
        column.Add(top).Add(content, SkUiShrinkFactor.Auto);
        Assert.Equal(SkUiShrinkFactor.Auto, column.GetShrink(content));
        column.Measure(80, 100);
        column.Arrange(new Rect(0, 0, 80, 100));
        Assert.Equal(new Rect(0, 30, 80, 70), content.Frame);

        column.SetShrink(content, SkUiShrinkFactor.None);
        column.Measure(80, 100);
        column.Arrange(new Rect(0, 0, 80, 100));
        Assert.Equal(200, content.Frame.Height);

        column.SetShrink(1, SkUiShrinkFactor.Auto);
        Assert.Equal(SkUiShrinkFactor.Auto, column.GetShrink(1));
        column.Measure(80, 100);
        column.Arrange(new Rect(0, 0, 80, 100));
        Assert.Equal(70, content.Frame.Height);

        // The flag is an attached value: it stays with the child, which can carry it into another layout.
        column.Remove(content);
        Assert.Throws<ArgumentException>(() => column.SetShrink(content, SkUiShrinkFactor.Auto));
        var other = new SkUiCoreVerticalShrinkLayout();
        other.Add(new FixedCoreNode(new Size(50, 30))).Add(content);
        other.Measure(80, 100);
        other.Arrange(new Rect(0, 0, 80, 100));
        Assert.Equal(70, content.Frame.Height);

        // Set before adding, and changes re-measure the parent.
        var early = new FixedCoreNode(new Size(50, 200));
        early.SetValue(SkUiCoreShrinkLayout.ShrinkProperty, SkUiShrinkFactor.Auto);
        var third = new SkUiCoreVerticalShrinkLayout();
        third.Add(early);
        third.Measure(80, 100);
        third.Arrange(new Rect(0, 0, 80, 100));
        Assert.Equal(100, early.Frame.Height);
        early.SetValue(SkUiCoreShrinkLayout.ShrinkProperty, SkUiShrinkFactor.None);
        third.Measure(80, 100);
        third.Arrange(new Rect(0, 0, 80, 100));
        Assert.Equal(200, early.Frame.Height);
    }

    [Fact]
    public void RunsRightToLeftOnBothLayers()
    {
        var row = Row(0, (40, false), (300, true));
        var root = new SkUiContentView { FlowDirection = FlowDirection.RightToLeft, Content = row };
        SkUiTestHelpers.Arrange(root, 200, 20);
        Assert.Equal(160, FrameOf(row, 0).X);
        Assert.Equal(new Rect(0, 0, 160, 20), FrameOf(row, 1));

        var core = CoreRow(0, (40, false), (300, true));
        var host = new SkUiCoreHost { FlowDirection = FlowDirection.RightToLeft }.SetContent(core);
        SkUiTestHelpers.Arrange(host, 200, 20);
        Assert.Equal(160, core.Children[0].Frame.X);
        Assert.Equal(new Rect(0, 0, 160, 20), core.Children[1].Frame);
    }

    private static SkUiHorizontalShrinkLayout FactorRow(params (double Width, SkUiShrinkFactor Shrink)[] children)
    {
        var row = new SkUiHorizontalShrinkLayout();
        foreach (var (width, shrink) in children)
        {
            var child = new WrappingSkView(width, 10);
            SkUiShrinkLayout.SetShrink(child, shrink);
            row.Children.Add(child);
        }
        return row;
    }

    [Fact]
    public void ExplicitFactorsShareTheOverflowByFactorTimesNaturalSize()
    {
        // Factor 1 on both: the overflow (50) goes 1:2, like the natural sizes; unlike Auto, the smaller one shrinks too.
        var row = FactorRow((100, 1), (200, 1), (50, SkUiShrinkFactor.None));
        SkUiTestHelpers.Arrange(row, 300, 20);
        Assert.Equal(100 - 50 / 3.0, FrameOf(row, 0).Width, 3);
        Assert.Equal(200 - 100 / 3.0, FrameOf(row, 1).Width, 3);
        Assert.Equal(50, FrameOf(row, 2).Width);

        var auto = FactorRow((100, SkUiShrinkFactor.Auto), (200, SkUiShrinkFactor.Auto), (50, SkUiShrinkFactor.None));
        SkUiTestHelpers.Arrange(auto, 300, 20);
        Assert.Equal(100, FrameOf(auto, 0).Width); // Auto: at the average (100), so it keeps its size
        Assert.Equal(150, FrameOf(auto, 1).Width);

        // Equal sizes, factors 2 and 1: the overflow (30) goes 2:1.
        var weighted = FactorRow((100, 2), (100, 1));
        SkUiTestHelpers.Arrange(weighted, 170, 20);
        Assert.Equal(80, FrameOf(weighted, 0).Width, 3);
        Assert.Equal(90, FrameOf(weighted, 1).Width, 3);
    }

    [Fact]
    public void AutoAndExplicitFactorsMix()
    {
        // Average 66.7: the small Auto child keeps its size; the big Auto child (weight 300) and the factor-1 child
        // (weight 60) share the overflow of 180 as 150 : 30.
        var row = FactorRow((20, SkUiShrinkFactor.Auto), (300, SkUiShrinkFactor.Auto), (60, 1));
        SkUiTestHelpers.Arrange(row, 200, 20);
        Assert.Equal(20, FrameOf(row, 0).Width, 3);
        Assert.Equal(150, FrameOf(row, 1).Width, 3);
        Assert.Equal(30, FrameOf(row, 2).Width, 3);
    }

    [Fact]
    public void ChildrenFreezeAtTheirMinimumAndTheRestIsSharedAgain()
    {
        // Equal shares of 25 would take the first child below its minimum (90): it stops there, the other gives up the rest.
        var row = FactorRow((100, 1), (100, 1));
        ((SkUiView)row.Children[0]).MinimumWidthRequest = 90;
        SkUiTestHelpers.Arrange(row, 150, 20);
        Assert.Equal(90, FrameOf(row, 0).Width, 3);
        Assert.Equal(60, FrameOf(row, 1).Width, 3);

        // A large factor on a small child: its share (25) is more than its size, so it stops at 0.
        var small = FactorRow((10, 10), (100, 1));
        SkUiTestHelpers.Arrange(small, 60, 20);
        Assert.Equal(0, FrameOf(small, 0).Width, 3);
        Assert.Equal(60, FrameOf(small, 1).Width, 3);

        // Everything at its minimum: the content overflows (the layout clips).
        var stuck = FactorRow((100, 1), (100, 1));
        ((SkUiView)stuck.Children[0]).MinimumWidthRequest = 100;
        ((SkUiView)stuck.Children[1]).MinimumWidthRequest = 80;
        SkUiTestHelpers.Arrange(stuck, 150, 20);
        Assert.Equal(100, FrameOf(stuck, 0).Width, 3);
        Assert.Equal(80, FrameOf(stuck, 1).Width, 3);
    }

    [Theory]
    [InlineData(300)]
    [InlineData(150)]
    [InlineData(60)]
    public void CoreFramesMatchSkUiFramesWithFactorsAndMinimums(double width)
    {
        (double Width, SkUiShrinkFactor Shrink)[] children = [(40, SkUiShrinkFactor.None), (120, SkUiShrinkFactor.Auto), (80, 2), (60, 0.5)];
        var skia = FactorRow(children);
        ((SkUiView)skia.Children[2]).MinimumWidthRequest = 30;
        var core = new SkUiCoreHorizontalShrinkLayout();
        foreach (var (natural, shrink) in children)
            core.Add(new WrappingCoreNode(natural, 10), shrink);
        ((SkUiCoreNode)core.Children[2]).SetMinimumWidth(30);
        SkUiTestHelpers.Arrange(skia, width, 40);
        var measured = core.Measure(width, 40);
        core.Arrange(new Rect(0, 0, width, 40));
        Assert.Equal(((IView)skia).Measure(width, 40), measured);
        for (var index = 0; index < children.Length; index++)
            Assert.Equal(FrameOf(skia, index), core.Children[index].Frame);
    }

    [Fact]
    public void ShrinkLayoutsClipTheirChildrenOnBothLayers()
    {
        // Children stuck at their minimum size can still overflow; unlike other layouts, shrink layouts clip.
        Assert.True(new SkUiHorizontalShrinkLayout().ClipToBounds);
        Assert.True(new SkUiVerticalShrinkLayout().ClipToBounds);
        Assert.False(new SkUiHorizontalStackLayout().ClipToBounds);
        Assert.True(new SkUiCoreHorizontalShrinkLayout().ClipToBounds);
        Assert.True(new SkUiCoreVerticalShrinkLayout().ClipToBounds);
    }

    [Fact]
    public void ShrinkFactorParsesFormatsAndValidates()
    {
        Assert.Equal(SkUiShrinkFactor.Auto, SkUiShrinkFactor.Parse(" auto "));
        Assert.Equal(SkUiShrinkFactor.None, SkUiShrinkFactor.Parse("None"));
        Assert.Equal(SkUiShrinkFactor.None, SkUiShrinkFactor.Parse("0"));
        Assert.Equal(new SkUiShrinkFactor(1.5), SkUiShrinkFactor.Parse("1.5"));
        Assert.Throws<FormatException>(() => SkUiShrinkFactor.Parse("lots"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkUiShrinkFactor(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkUiShrinkFactor(double.NaN));
        Assert.Equal(["None", "Auto", "2"], new SkUiShrinkFactor[] { SkUiShrinkFactor.None, SkUiShrinkFactor.Auto, 2 }.Select(factor => factor.ToString()));
        Assert.True(SkUiShrinkFactor.None.IsNone);
        Assert.True(SkUiShrinkFactor.Auto.IsAuto);
        Assert.NotEqual(SkUiShrinkFactor.Auto, new SkUiShrinkFactor(1)); // Auto also applies the average rule
        var converter = new SkUiShrinkFactorTypeConverter();
        Assert.Equal(new SkUiShrinkFactor(3), converter.ConvertFromInvariantString("3"));
        Assert.Equal("Auto", converter.ConvertToInvariantString(SkUiShrinkFactor.Auto));
    }
}
