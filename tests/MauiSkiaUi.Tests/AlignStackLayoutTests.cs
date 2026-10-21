using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>Horizontal align stacks on both layers (one shared engine): children placed by their horizontal alignment.</summary>
public class AlignStackLayoutTests
{
    private const double Width = 300;
    private const double Height = 40;

    private static SkUiHorizontalAlignStackLayout Row(double spacing, params (double Width, LayoutAlignment Alignment)[] children)
    {
        var row = new SkUiHorizontalAlignStackLayout { Spacing = spacing };
        foreach (var (width, alignment) in children)
            row.Children.Add(new WrappingSkView(width, 20) { HorizontalOptions = new LayoutOptions(alignment, false) });
        return row;
    }

    private static SkUiCoreHorizontalAlignStackLayout CoreRow(double spacing, params (double Width, LayoutAlignment Alignment)[] children)
    {
        var row = new SkUiCoreHorizontalAlignStackLayout().SetSpacing(spacing);
        foreach (var (width, alignment) in children)
            row.Add(new WrappingCoreNode(width, 20), alignment);
        return row;
    }

    private static Rect FrameOf(SkUiLayout layout, int index) => ((SkUiView)layout.Children[index]).Frame;

    private static SkUiHorizontalAlignStackLayout Arranged(SkUiHorizontalAlignStackLayout row, double width = Width)
    {
        SkUiTestHelpers.Arrange(row, width, Height);
        return row;
    }

    [Fact]
    public void ChildrenGoByTheirAlignmentNotTheirPlaceInTheList()
    {
        // Listed centered, end, start: the centered child stays in the middle of the layout although the sides differ.
        var row = Arranged(Row(0, (50, LayoutAlignment.Center), (30, LayoutAlignment.End), (100, LayoutAlignment.Start)));
        Assert.Equal(new Rect(125, 0, 50, Height), FrameOf(row, 0));
        Assert.Equal(new Rect(270, 0, 30, Height), FrameOf(row, 1));
        Assert.Equal(new Rect(0, 0, 100, Height), FrameOf(row, 2));
    }

    [Fact]
    public void GroupsKeepListOrderAndSpacing()
    {
        var row = Arranged(Row(5,
            (20, LayoutAlignment.Start), (10, LayoutAlignment.End), (40, LayoutAlignment.Center),
            (30, LayoutAlignment.Start), (15, LayoutAlignment.End), (60, LayoutAlignment.Center)));
        Assert.Equal(0, FrameOf(row, 0).X);
        Assert.Equal(25, FrameOf(row, 3).X);
        // The last end child is at the end.
        Assert.Equal(285, FrameOf(row, 4).X);
        Assert.Equal(270, FrameOf(row, 1).X);
        // The centered row (40 + 5 + 60) is centered as one.
        Assert.Equal(97.5, FrameOf(row, 2).X, 3);
        Assert.Equal(142.5, FrameOf(row, 5).X, 3);
    }

    [Fact]
    public void FillChildrenAreCenteredAndCollapsedChildrenTakeNoSpace()
    {
        var row = Row(0, (100, LayoutAlignment.Start), (50, LayoutAlignment.Fill), (30, LayoutAlignment.End));
        Arranged(row);
        Assert.Equal(new Rect(125, 0, 50, Height), FrameOf(row, 1));

        ((View)row.Children[0]).IsVisible = false;
        row.CenterStrategy = SkUiCenterStrategy.AvailableSpace;
        Arranged(row);
        Assert.Equal((270 - 50) / 2.0, FrameOf(row, 1).X, 3);
    }

    [Fact]
    public void CenteredInTheLayoutTheyMoveAsideOnlyAsFarAsTheSidesNeed()
    {
        var row = Arranged(Row(0, (140, LayoutAlignment.Start), (50, LayoutAlignment.Center), (30, LayoutAlignment.End)));
        Assert.Equal(140, FrameOf(row, 1).X);
        // The spacing is kept between the sides and the centered children too.
        row.Spacing = 8;
        Arranged(row);
        Assert.Equal(148, FrameOf(row, 1).X);
    }

    [Theory]
    [InlineData(SkUiCenterStrategy.Parent, 100, 125)]
    [InlineData(SkUiCenterStrategy.Parent, 140, 140)]
    [InlineData(SkUiCenterStrategy.AvailableSpace, 100, 160)]
    [InlineData(SkUiCenterStrategy.AvailableSpace, 140, 180)]
    [InlineData(SkUiCenterStrategy.ParentOrAvailableSpace, 100, 125)]
    [InlineData(SkUiCenterStrategy.ParentOrAvailableSpace, 140, 180)]
    [InlineData(SkUiCenterStrategy.AvailableSpaceWhenBothSides, 100, 160)]
    public void StrategiesPlaceTheCenteredChildren(SkUiCenterStrategy strategy, double start, double expected)
    {
        var row = Row(0, (start, LayoutAlignment.Start), (50, LayoutAlignment.Center), (30, LayoutAlignment.End));
        row.CenterStrategy = strategy;
        Arranged(row);
        Assert.Equal(expected, FrameOf(row, 1).X, 3);
    }

    [Fact]
    public void WithOneSideOnlyAvailableSpaceWhenBothSidesCentersInTheLayout()
    {
        var row = Row(0, (100, LayoutAlignment.Start), (50, LayoutAlignment.Center), (30, LayoutAlignment.End));
        row.CenterStrategy = SkUiCenterStrategy.AvailableSpaceWhenBothSides;
        ((View)row.Children[2]).IsVisible = false;
        Arranged(row);
        Assert.Equal(125, FrameOf(row, 1).X);
        // Unless it does not fit there.
        ((View)row.Children[0]).WidthRequest = 140;
        Arranged(row);
        Assert.Equal((140 + 300 - 50) / 2.0, FrameOf(row, 1).X, 3);
    }

    [Fact]
    public void CenteredChildrenGetTheWidthTheSidesLeave()
    {
        // 300 − 2 × 10 spacing − 100 − 30: the long centered child wraps in 150.
        var row = Arranged(Row(10, (100, LayoutAlignment.Start), (300, LayoutAlignment.Center), (30, LayoutAlignment.End)));
        Assert.Equal(new Rect(110, 0, 150, Height), FrameOf(row, 1));
        Assert.Equal(40, ((IView)row.Children[1]).DesiredSize.Height);
        Assert.Equal(new Rect(270, 0, 30, Height), FrameOf(row, 2));
    }

    [Fact]
    public void MeasuresRoomToCenterInTheLayout()
    {
        var row = Row(0, (100, LayoutAlignment.Start), (50, LayoutAlignment.Center), (30, LayoutAlignment.End));
        // Centered in the layout: the wider side on both sides of the centered child.
        Assert.Equal(new Size(250, 20), ((IView)row).Measure(double.PositiveInfinity, double.PositiveInfinity));
        // As far as the width allows.
        Assert.Equal(new Size(200, 20), ((IView)row).Measure(200, double.PositiveInfinity));
        row.CenterStrategy = SkUiCenterStrategy.AvailableSpace;
        Assert.Equal(new Size(180, 20), ((IView)row).Measure(double.PositiveInfinity, double.PositiveInfinity));
        row.Padding = new Thickness(4, 2);
        Assert.Equal(new Size(188, 24), ((IView)row).Measure(double.PositiveInfinity, double.PositiveInfinity));
    }

    [Fact]
    public void ArrangedNarrowerThanMeasuredItMeasuresAgain()
    {
        var row = Row(0, (100, LayoutAlignment.Start), (200, LayoutAlignment.Center), (30, LayoutAlignment.End));
        ((IView)row).Measure(double.PositiveInfinity, Height);
        ((IView)row).Arrange(new Rect(0, 0, Width, Height));
        Assert.Equal(new Rect(100, 0, 170, Height), FrameOf(row, 1));
    }

    [Theory]
    [InlineData(SkUiCenterStrategy.Parent, 300)]
    [InlineData(SkUiCenterStrategy.Parent, 160)]
    [InlineData(SkUiCenterStrategy.AvailableSpace, 300)]
    [InlineData(SkUiCenterStrategy.ParentOrAvailableSpace, 240)]
    [InlineData(SkUiCenterStrategy.AvailableSpaceWhenBothSides, 200)]
    public void CoreFramesMatchSkUiFrames(SkUiCenterStrategy strategy, double width)
    {
        (double, LayoutAlignment)[] children =
            [(60, LayoutAlignment.Start), (90, LayoutAlignment.Center), (20, LayoutAlignment.End), (40, LayoutAlignment.Start), (30, LayoutAlignment.Fill)];
        var skia = Row(6, children);
        skia.CenterStrategy = strategy;
        var core = CoreRow(6, children).SetCenterStrategy(strategy);
        SkUiTestHelpers.Arrange(skia, width, Height);
        var measured = core.Measure(width, Height);
        core.Arrange(new Rect(0, 0, width, Height));
        Assert.Equal(((IView)skia).Measure(width, Height), measured);
        for (var index = 0; index < children.Length; index++)
            Assert.Equal(FrameOf(skia, index), core.Children[index].Frame);
    }

    [Fact]
    public void RunsRightToLeftOnBothLayers()
    {
        var row = Row(0, (100, LayoutAlignment.Start), (50, LayoutAlignment.Center), (30, LayoutAlignment.End));
        SkUiTestHelpers.Arrange(new SkUiContentView { FlowDirection = FlowDirection.RightToLeft, Content = row }, Width, Height);
        Assert.Equal(200, FrameOf(row, 0).X);
        Assert.Equal(125, FrameOf(row, 1).X);
        Assert.Equal(0, FrameOf(row, 2).X);

        var core = CoreRow(0, (100, LayoutAlignment.Start), (50, LayoutAlignment.Center), (30, LayoutAlignment.End));
        SkUiTestHelpers.Arrange(new SkUiCoreHost { FlowDirection = FlowDirection.RightToLeft }.SetContent(core), Width, Height);
        Assert.Equal(200, core.Children[0].Frame.X);
        Assert.Equal(125, core.Children[1].Frame.X);
        Assert.Equal(0, core.Children[2].Frame.X);
    }

    [Fact]
    public void ChangesRelayoutAndInvalidValuesAreRefused()
    {
        var row = Arranged(Row(0, (100, LayoutAlignment.Start), (50, LayoutAlignment.Center), (30, LayoutAlignment.End)));
        ((View)row.Children[1]).HorizontalOptions = LayoutOptions.End;
        Arranged(row);
        Assert.Equal(220, FrameOf(row, 1).X);

        row.CenterStrategy = (SkUiCenterStrategy)42;
        Assert.Equal(SkUiCenterStrategy.Parent, row.CenterStrategy);
        Assert.Throws<ArgumentOutOfRangeException>(() => row.SetCenterStrategy((SkUiCenterStrategy)42));
        Assert.Throws<ArgumentOutOfRangeException>(() => row.SetSpacing(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkUiCoreHorizontalAlignStackLayout().SetCenterStrategy((SkUiCenterStrategy)42));

        var core = CoreRow(0, (100, LayoutAlignment.Start), (50, LayoutAlignment.Center));
        var host = new SkUiCoreHost().SetContent(core);
        SkUiTestHelpers.Arrange(host, Width, Height);
        Assert.Equal(125, core.Children[1].Frame.X);
        core.CenterStrategy = SkUiCenterStrategy.AvailableSpace;
        SkUiTestHelpers.Arrange(host, Width, Height);
        Assert.Equal(175, core.Children[1].Frame.X);
    }
}
