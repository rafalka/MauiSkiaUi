using Microsoft.Maui.Layouts;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// <see cref="SkUiFlexLayout"/>: frames match MAUI's own <see cref="FlexLayout"/> (run headless with fixed-size
/// children), plus hand-computed cases for the behavior that matters most.
/// </summary>
public class FlexLayoutTests
{
    private static readonly Size[] NaturalSizes = [new(40, 20), new(70, 30), new(25, 45), new(90, 15), new(55, 25)];

    /// <summary>A handler-less MAUI child with the same measure rules as <see cref="SkUiView"/>.</summary>
    private sealed class FixedMauiView(Size natural) : View
    {
        protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
        {
            IView view = this;
            var width = Math.Max(0, widthConstraint - Margin.HorizontalThickness);
            var height = Math.Max(0, heightConstraint - Margin.VerticalThickness);
            return new Size(
                LayoutManager.ResolveConstraints(width, view.Width, natural.Width, view.MinimumWidth, view.MaximumWidth) + Margin.HorizontalThickness,
                LayoutManager.ResolveConstraints(height, view.Height, natural.Height, view.MinimumHeight, view.MaximumHeight) + Margin.VerticalThickness);
        }
    }

    private static void ApplyChildVariant(View child, int index, int variant)
    {
        if (variant == 0)
            return;
        switch (index)
        {
            case 0:
                FlexLayout.SetGrow(child, 1);
                child.Margin = new Thickness(3, 2, 5, 1);
                break;
            case 1:
                FlexLayout.SetShrink(child, 0);
                FlexLayout.SetOrder(child, 2);
                break;
            case 2:
                FlexLayout.SetAlignSelf(child, FlexAlignSelf.End);
                FlexLayout.SetGrow(child, 2);
                break;
            case 3:
                FlexLayout.SetBasis(child, new FlexBasis(0.25f, isRelative: true));
                child.WidthRequest = 30;
                break;
            case 4:
                FlexLayout.SetBasis(child, new FlexBasis(60));
                FlexLayout.SetOrder(child, -1);
                child.HeightRequest = 12;
                break;
        }
    }

    private static (SkUiFlexLayout Skia, FlexLayout Maui) CreatePair(int variant)
    {
        var skia = new SkUiFlexLayout { Padding = new Thickness(4, 6, 2, 3) };
        var maui = new FlexLayout { Padding = new Thickness(4, 6, 2, 3), Parent = new ContentView() };
        for (var i = 0; i < NaturalSizes.Length; i++)
        {
            var skChild = new FixedSkView(NaturalSizes[i]);
            var mauiChild = new FixedMauiView(NaturalSizes[i]);
            ApplyChildVariant(skChild, i, variant);
            ApplyChildVariant(mauiChild, i, variant);
            skia.Children.Add(skChild);
            maui.Children.Add(mauiChild);
        }
        return (skia, maui);
    }

    private static void AssertSameFrames(SkUiFlexLayout skia, FlexLayout maui, double width, double height, string label)
    {
        var measured = maui.CrossPlatformMeasure(width, height);
        var skMeasured = ((IView)skia).Measure(width, height);
        Assert.True(Math.Abs(measured.Width - skMeasured.Width) < 0.01 && Math.Abs(measured.Height - skMeasured.Height) < 0.01,
            $"{label}: measured {skMeasured} vs MAUI {measured}");
        var arrangeWidth = double.IsInfinity(width) ? measured.Width : width;
        var arrangeHeight = double.IsInfinity(height) ? measured.Height : height;
        ((Microsoft.Maui.ILayout)maui).CrossPlatformArrange(new Rect(0, 0, arrangeWidth, arrangeHeight));
        ((IView)skia).Arrange(new Rect(0, 0, arrangeWidth, arrangeHeight));
        for (var i = 0; i < skia.Children.Count; i++)
        {
            var expected = maui.Children[i].Frame;
            var actual = ((SkUiView)skia.Children[i]).Frame;
            Assert.True(Math.Abs(expected.X - actual.X) < 0.01 && Math.Abs(expected.Y - actual.Y) < 0.01
                && Math.Abs(expected.Width - actual.Width) < 0.01 && Math.Abs(expected.Height - actual.Height) < 0.01,
                $"{label}: child {i} frame {actual} vs MAUI {expected}");
        }
    }

    [Fact]
    public void FramesMatchMauiFlexLayoutForEveryContainerCombination()
    {
        (double Width, double Height)[] sizes = [(200, 150), (120, 300), (400, 60), (double.PositiveInfinity, 150), (200, double.PositiveInfinity)];
        var cases = 0;
        foreach (var direction in Enum.GetValues<FlexDirection>())
        foreach (var wrap in Enum.GetValues<FlexWrap>())
        foreach (var justify in Enum.GetValues<FlexJustify>())
        foreach (var alignItems in Enum.GetValues<FlexAlignItems>())
        foreach (var alignContent in new[] { FlexAlignContent.Stretch, FlexAlignContent.Start, FlexAlignContent.Center, FlexAlignContent.SpaceBetween })
        for (var variant = 0; variant < 2; variant++)
        {
            var (skia, maui) = CreatePair(variant);
            skia.Direction = maui.Direction = direction;
            skia.Wrap = maui.Wrap = wrap;
            skia.JustifyContent = maui.JustifyContent = justify;
            skia.AlignItems = maui.AlignItems = alignItems;
            skia.AlignContent = maui.AlignContent = alignContent;
            foreach (var (width, height) in sizes)
            {
                AssertSameFrames(skia, maui, width, height,
                    $"{direction}/{wrap}/{justify}/{alignItems}/{alignContent}/v{variant} @ {width}x{height}");
                cases++;
            }
        }
        Assert.True(cases > 1000);
    }

    [Fact]
    public void RowGrowSharesFreeSpaceAndFixedChildrenKeepTheirSize()
    {
        var flex = new SkUiFlexLayout { AlignItems = FlexAlignItems.Start };
        var fixedChild = new SkUiBox { WidthRequest = 40, HeightRequest = 10 };
        // Grow children have no intrinsic width and no WidthRequest (an explicit request caps the arranged width, as in MAUI).
        var grow1 = new FixedSkView(new Size(0, 10));
        var grow2 = new FixedSkView(new Size(0, 10));
        SkUiFlexLayout.SetGrow(grow1, 1);
        SkUiFlexLayout.SetGrow(grow2, 3);
        flex.Children.Add(fixedChild);
        flex.Children.Add(grow1);
        flex.Children.Add(grow2);
        SkUiTestHelpers.Arrange(flex, 200, 50);
        Assert.Equal(new Rect(0, 0, 40, 10), fixedChild.Frame);
        Assert.Equal(new Rect(40, 0, 40, 10), grow1.Frame);
        Assert.Equal(new Rect(80, 0, 120, 10), grow2.Frame);
    }

    [Fact]
    public void WrapMovesChildrenToNewLinesAndJustifySpacesThem()
    {
        var flex = new SkUiFlexLayout { Wrap = FlexWrap.Wrap, AlignContent = FlexAlignContent.Start, AlignItems = FlexAlignItems.Start };
        var boxes = Enumerable.Range(0, 3).Select(_ => new SkUiBox { WidthRequest = 60, HeightRequest = 20 }).ToArray();
        foreach (var box in boxes)
            flex.Children.Add(box);
        SkUiTestHelpers.Arrange(flex, 130, 100);
        Assert.Equal(new Rect(0, 0, 60, 20), boxes[0].Frame);
        Assert.Equal(new Rect(60, 0, 60, 20), boxes[1].Frame);
        Assert.Equal(new Rect(0, 20, 60, 20), boxes[2].Frame);

        flex.JustifyContent = FlexJustify.SpaceBetween;
        SkUiTestHelpers.Arrange(flex, 130, 100);
        Assert.Equal(70, boxes[1].Frame.X);
    }

    [Fact]
    public void AttachedPropertyChangesRelayout()
    {
        var flex = new SkUiFlexLayout { AlignItems = FlexAlignItems.Start };
        var first = new SkUiBox { WidthRequest = 20, HeightRequest = 10 };
        var second = new SkUiBox { WidthRequest = 30, HeightRequest = 10 };
        flex.Children.Add(first);
        flex.Children.Add(second);
        SkUiTestHelpers.Arrange(flex, 100, 50);
        Assert.Equal(20, second.Frame.X);

        FlexLayout.SetOrder(first, 1);
        SkUiTestHelpers.Arrange(flex, 100, 50);
        Assert.Equal(0, second.Frame.X);
        Assert.Equal(30, first.Frame.X);

        SkUiFlexLayout.SetAlignSelf(second, FlexAlignSelf.End);
        SkUiTestHelpers.Arrange(flex, 100, 50);
        Assert.Equal(40, second.Frame.Y);

        // The flex frame follows the basis; the child's own WidthRequest still caps its arranged frame (as in MAUI).
        SkUiFlexLayout.SetBasis(first, new FlexBasis(0.5f, isRelative: true));
        SkUiTestHelpers.Arrange(flex, 100, 50);
        Assert.Equal(50, flex.GetFlexFrame(first).Width);
        Assert.Equal(20, first.Frame.Width);
    }

    [Fact]
    public void RelativeAndAbsoluteBasisOfTheSameLengthDiffer()
    {
        var flex = new SkUiFlexLayout { AlignItems = FlexAlignItems.Start };
        var relative = new FixedSkView(new Size(0, 10));
        var absolute = new FixedSkView(new Size(0, 10));
        SkUiFlexLayout.SetBasis(relative, new FlexBasis(0.5f, isRelative: true));
        SkUiFlexLayout.SetBasis(absolute, new FlexBasis(0.5f));
        flex.Children.Add(relative);
        flex.Children.Add(absolute);
        SkUiTestHelpers.Arrange(flex, 200, 20);
        Assert.Equal(100, flex.GetFlexFrame(relative).Width, 3); // 50% of 200
        Assert.Equal(0.5, flex.GetFlexFrame(absolute).Width, 3); // 0.5 DIP
    }

    [Fact]
    public void ChildrenChangesRebuildTheItemTree()
    {
        var flex = new SkUiFlexLayout { AlignItems = FlexAlignItems.Start };
        var a = new SkUiBox { WidthRequest = 10, HeightRequest = 10 };
        var b = new SkUiBox { WidthRequest = 20, HeightRequest = 10 };
        var c = new SkUiBox { WidthRequest = 30, HeightRequest = 10 };
        flex.Children.Add(a);
        flex.Children.Add(b);
        SkUiTestHelpers.Arrange(flex, 100, 20);
        Assert.Equal(10, b.Frame.X);

        flex.Children.Insert(0, c);
        SkUiTestHelpers.Arrange(flex, 100, 20);
        Assert.Equal(30, a.Frame.X);
        Assert.Equal(40, b.Frame.X);

        flex.Children.Remove(a);
        SkUiTestHelpers.Arrange(flex, 100, 20);
        Assert.Equal(30, b.Frame.X);

        flex.Children[1] = a;
        SkUiTestHelpers.Arrange(flex, 100, 20);
        Assert.Equal(30, a.Frame.X);
        Assert.Equal(Rect.Zero, flex.GetFlexFrame(b));

        flex.Children.Clear();
        flex.Children.Add(b);
        SkUiTestHelpers.Arrange(flex, 100, 20);
        Assert.Equal(0, b.Frame.X);
    }

    [Fact]
    public void CollapsedChildrenTakeNoSpace()
    {
        var flex = new SkUiFlexLayout { AlignItems = FlexAlignItems.Start };
        var first = new SkUiBox { WidthRequest = 20, HeightRequest = 10, IsVisible = false };
        var second = new SkUiBox { WidthRequest = 30, HeightRequest = 10 };
        flex.Children.Add(first);
        flex.Children.Add(second);
        SkUiTestHelpers.Arrange(flex, 100, 50);
        Assert.Equal(0, second.Frame.X);

        first.IsVisible = true;
        SkUiTestHelpers.Arrange(flex, 100, 50);
        Assert.Equal(20, second.Frame.X);
    }

    [Fact]
    public void MeasuresUnderAnInfiniteWidthInsideAHorizontalScrollView()
    {
        var flex = new SkUiFlexLayout();
        for (var i = 0; i < 4; i++)
            flex.Children.Add(new SkUiBox { WidthRequest = 50, HeightRequest = 20 });
        var scroll = new SkUiScrollView { Orientation = ScrollOrientation.Horizontal, Content = flex };
        SkUiTestHelpers.Arrange(scroll, 120, 40);
        Assert.Equal(200, flex.Frame.Width);
        Assert.Equal(150, ((SkUiView)flex.Children[3]).Frame.X);
    }

    [Fact]
    public void RowRunsRightToLeft()
    {
        var flex = new SkUiFlexLayout { AlignItems = FlexAlignItems.Start };
        var first = new SkUiBox { WidthRequest = 20, HeightRequest = 10 };
        var second = new SkUiBox { WidthRequest = 30, HeightRequest = 10 };
        flex.Children.Add(first);
        flex.Children.Add(second);
        var root = new SkUiContentView { FlowDirection = FlowDirection.RightToLeft, Content = flex };
        SkUiTestHelpers.Arrange(root, 100, 10);
        Assert.Equal(80, first.Frame.X);
        Assert.Equal(50, second.Frame.X);
    }
}

/// <summary>XAML attached-property syntax for the flex and stretch layouts (runtime XAML inflation).</summary>
[Collection(RuntimeXamlCollection.Name)]
public class LayoutXamlTests
{
    [Fact]
    public void FlexAndStretchAttachedPropertiesParseFromXaml()
    {
        const string xaml = """
            <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <sk:SkUiVerticalStackLayout>
                <sk:SkUiFlexLayout Wrap="Wrap" JustifyContent="SpaceBetween">
                  <sk:SkUiBox sk:SkUiFlexLayout.Grow="1" sk:SkUiFlexLayout.Basis="25%" sk:SkUiFlexLayout.AlignSelf="End" />
                  <sk:SkUiBox FlexLayout.Order="-1" FlexLayout.Shrink="0" />
                </sk:SkUiFlexLayout>
                <sk:SkUiHorizontalShrinkLayout Spacing="4">
                  <sk:SkUiLabel Text="Stretch" sk:SkUiShrinkLayout.Shrink="Auto" />
                  <sk:SkUiLabel Text="Twice" sk:SkUiShrinkLayout.Shrink="2" />
                </sk:SkUiHorizontalShrinkLayout>
                <sk:SkUiWrapLayout Spacing="6" RowSpacing="3" />
              </sk:SkUiVerticalStackLayout>
            </ContentView>
            """;
        var root = new ContentView();
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var stack = (SkUiVerticalStackLayout)root.Content;
        var flex = (SkUiFlexLayout)stack.Children[0];
        Assert.Equal(FlexWrap.Wrap, flex.Wrap);
        Assert.Equal(FlexJustify.SpaceBetween, flex.JustifyContent);
        var first = (BindableObject)flex.Children[0];
        Assert.Equal(1f, SkUiFlexLayout.GetGrow(first));
        Assert.Equal(new FlexBasis(0.25f, isRelative: true), SkUiFlexLayout.GetBasis(first));
        Assert.Equal(FlexAlignSelf.End, SkUiFlexLayout.GetAlignSelf(first));
        var second = (BindableObject)flex.Children[1];
        Assert.Equal(-1, SkUiFlexLayout.GetOrder(second));
        Assert.Equal(0f, SkUiFlexLayout.GetShrink(second));
        var stretch = (SkUiHorizontalShrinkLayout)stack.Children[1];
        Assert.Equal(SkUiShrinkFactor.Auto, SkUiShrinkLayout.GetShrink((BindableObject)stretch.Children[0]));
        Assert.Equal(new SkUiShrinkFactor(2), SkUiShrinkLayout.GetShrink((BindableObject)stretch.Children[1]));
        var wrap = (SkUiWrapLayout)stack.Children[2];
        Assert.Equal((6d, 3d), (wrap.Spacing, wrap.RowSpacing));
    }
}
