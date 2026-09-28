using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>Right-to-left layout mirroring (MAUI FlowDirection) for drawn trees.</summary>
public class RtlLayoutTests
{
    private static SkUiBox Box(Color color, double width = 20) =>
        new() { Color = color, WidthRequest = width, HeightRequest = 10, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };

    [Fact]
    public void HorizontalStackRunsRightToLeft()
    {
        var first = Box(Colors.Red);
        var second = Box(Colors.Blue);
        var stack = new SkUiHorizontalStackLayout { Spacing = 5 };
        stack.Children.Add(first);
        stack.Children.Add(second);
        var root = new SkUiContentView { FlowDirection = FlowDirection.RightToLeft, Content = stack };
        SkUiTestHelpers.Arrange(root, 100, 10);
        Assert.Equal(80, first.Frame.X);
        Assert.Equal(55, second.Frame.X);

        root.FlowDirection = FlowDirection.LeftToRight;
        SkUiTestHelpers.Arrange(root, 100, 10);
        Assert.Equal(0, first.Frame.X);
        Assert.Equal(25, second.Frame.X);
    }

    [Fact]
    public void GridColumnZeroIsOnTheRightAndMarginsMirror()
    {
        var grid = new SkUiGrid { ColumnDefinitions = [new(new GridLength(30)), new(GridLength.Star)] };
        var start = Box(Colors.Red, 10);
        start.Margin = new Thickness(4, 0, 0, 0);
        grid.Children.Add(start);
        var second = Box(Colors.Blue, 10);
        Grid.SetColumn(second, 1);
        grid.Children.Add(second);
        var root = new SkUiContentView { FlowDirection = FlowDirection.RightToLeft, Content = grid };
        SkUiTestHelpers.Arrange(root, 100, 10);
        // Column 0 spans x 70..100 when mirrored; Start alignment + leading margin put the box at the right edge.
        Assert.Equal(new Rect(86, 0, 10, 10), start.Frame);
        Assert.Equal(60, second.Frame.X);
    }

    [Fact]
    public void ExplicitLeftToRightSubtreeInsideRtlRoot()
    {
        var inner = new SkUiHorizontalStackLayout { FlowDirection = FlowDirection.LeftToRight, WidthRequest = 50, HorizontalOptions = LayoutOptions.Start };
        var a = Box(Colors.Red, 10);
        inner.Children.Add(a);
        var root = new SkUiContentView { FlowDirection = FlowDirection.RightToLeft, Content = inner };
        SkUiTestHelpers.Arrange(root, 100, 10);
        Assert.Equal(50, inner.Frame.X); // inner itself is mirrored by its RTL parent
        Assert.Equal(0, a.Frame.X);      // its children are laid out LTR
    }

    [Fact]
    public void InheritedDirectionReachesLateChildrenAndHitTesting()
    {
        var stack = new SkUiHorizontalStackLayout();
        var root = new SkUiContentView { FlowDirection = FlowDirection.RightToLeft, Content = stack };
        var tapped = 0;
        var button = new SkUiButton { WidthRequest = 30, HeightRequest = 10, HorizontalOptions = LayoutOptions.Start };
        button.Clicked += (_, _) => tapped++;
        stack.Children.Add(button);
        SkUiTestHelpers.Arrange(root, 100, 10);
        Assert.Equal(70, button.Frame.X);
        root.Touch(new(1, SkUiTouchAction.Pressed, new Point(85, 5)));
        root.Touch(new(1, SkUiTouchAction.Released, new Point(85, 5)));
        Assert.Equal(1, tapped);
    }

    [Fact]
    public void RtlCompositeFramePaintsMirrored()
    {
        var stack = new SkUiHorizontalStackLayout();
        stack.Children.Add(Box(Colors.Red));
        var root = new SkUiContentView { Background = Colors.White, FlowDirection = FlowDirection.RightToLeft, Content = stack };
        using var surface = new SkUiTestSurface(root, 100, 10);
        var bitmap = surface.Frame();
        Assert.Equal(SKColors.Red, bitmap.GetPixel(90, 5));
        Assert.Equal(SKColors.White, bitmap.GetPixel(10, 5));
    }

    [Fact]
    public void HorizontalScrollStartsAtLogicalStartInRtl()
    {
        var content = new SkUiHorizontalStackLayout();
        content.Children.Add(Box(Colors.Red, 300));
        var scroll = new SkUiScrollView { Orientation = ScrollOrientation.Horizontal, FlowDirection = FlowDirection.RightToLeft, Content = content };
        SkUiTestHelpers.Arrange(scroll, 100, 20);
        Assert.Equal(200, scroll.ScrollX);
    }

    [Fact]
    public void LabelInheritsRtlFromAncestor()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var label = new SkUiLabel { Text = "Hi", TextColor = Colors.Black, FontFamily = SkUiTestHelpers.BundledFontFamily };
        var root = new SkUiContentView { Background = Colors.White, Content = label };
        using var surface = new SkUiTestSurface(root, 200, 30);
        Assert.True(InkCenter(surface.Frame()) < 100);
        root.FlowDirection = FlowDirection.RightToLeft;
        SkUiTestHelpers.Arrange(root, 200, 30);
        Assert.True(InkCenter(surface.Frame()) > 100);
    }

    [Fact]
    public void SwitchThumbAndCheckBoxMirror()
    {
        var toggle = new SkUiSwitch { IsChecked = true, WidthRequest = 60, HeightRequest = 30 };
        var host = new SkUiContentView { Background = Colors.White, Content = toggle };
        using var surface = new SkUiTestSurface(host, 60, 30);
        var ltrThumb = BrightestColumn(surface.Frame());
        host.FlowDirection = FlowDirection.RightToLeft;
        SkUiTestHelpers.Arrange(host, 60, 30);
        var rtlThumb = BrightestColumn(surface.Frame());
        Assert.True(ltrThumb > 30 && rtlThumb < 30, $"thumb ltr={ltrThumb} rtl={rtlThumb}");

        var check = new SkUiCheckBox { IsChecked = true, WidthRequest = 80, HeightRequest = 20, FlowDirection = FlowDirection.RightToLeft };
        var checkHost = new SkUiContentView { Background = Colors.White, Content = check };
        using var checkSurface = new SkUiTestSurface(checkHost, 80, 20);
        var bitmap = checkSurface.Frame();
        Assert.NotEqual(SKColors.White, bitmap.GetPixel(70, 10));
        Assert.Equal(SKColors.White, bitmap.GetPixel(10, 10));
    }

    [Fact]
    public void CorePanelsMirrorFromHostAndExplicitCoreDirection()
    {
        var first = new SkUiCoreBox();
        first.SetWidth(20).SetHeight(10).SetHorizontalAlignment(LayoutAlignment.Start);
        var second = new SkUiCoreBox();
        second.SetWidth(20).SetHeight(10).SetHorizontalAlignment(LayoutAlignment.Start);
        var stack = new SkUiCoreHorizontalStackLayout().Add(first).Add(second);
        var host = new SkUiCoreHost { FlowDirection = FlowDirection.RightToLeft }.SetContent(stack);
        SkUiTestHelpers.Arrange(host, 100, 10);
        Assert.Equal(80, first.Frame.X);
        Assert.Equal(60, second.Frame.X);

        stack.SetFlowDirection(FlowDirection.LeftToRight);
        SkUiTestHelpers.Arrange(host, 100, 10);
        Assert.Equal(0, first.Frame.X);
        Assert.Equal(20, second.Frame.X);
    }

    [Fact]
    public void CoreTableChromeFollowsMirroredColumns()
    {
        var table = new SkUiCoreTable()
            .SetColumnDefinitions([new SkUiCoreColumnDefinition(new SkUiCoreGridLength(30)), new SkUiCoreColumnDefinition(SkUiCoreGridLength.Star)])
            .SetRowDefinitions([new SkUiCoreRowDefinition(new SkUiCoreGridLength(10))])
            .SetColumnBackground(0, Colors.Red);
        var host = new SkUiCoreHost { Background = Colors.White, FlowDirection = FlowDirection.RightToLeft }.SetContent(table);
        using var surface = new SkUiTestSurface(host, 100, 10);
        var bitmap = surface.Frame();
        Assert.Equal(SKColors.Red, bitmap.GetPixel(90, 5));
        Assert.Equal(SKColors.White, bitmap.GetPixel(10, 5));
    }

    [Fact]
    public void NativeOverlayFrameIsMirrored()
    {
        var overlay = new SkUiMauiContentView { WidthRequest = 30, HeightRequest = 10, HorizontalOptions = LayoutOptions.Start };
        var stack = new SkUiHorizontalStackLayout();
        stack.Children.Add(overlay);
        var root = new SkUiContentView { FlowDirection = FlowDirection.RightToLeft, Content = stack };
        SkUiTestHelpers.Arrange(root, 100, 10);
        Assert.Equal(70, overlay.ComputeRootRelativeFrame().X);
    }

    private static double InkCenter(SKBitmap bitmap)
    {
        double sum = 0, count = 0;
        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
                if (bitmap.GetPixel(x, y).Red < 128) { sum += x; count++; }
        Assert.True(count > 0);
        return sum / count;
    }

    /// <summary>Column with the most near-white pixels on the middle row (the switch thumb).</summary>
    private static int BrightestColumn(SKBitmap bitmap)
    {
        var y = bitmap.Height / 2;
        var best = 0;
        var bestScore = -1;
        for (var x = 2; x < bitmap.Width - 2; x++)
        {
            var c = bitmap.GetPixel(x, y);
            var score = c.Red + c.Green + c.Blue;
            // Ignore the white page background outside the track: only count pixels inside the track band.
            var above = bitmap.GetPixel(x, 2);
            if (above.Red + above.Green + above.Blue > 740 && score > 740) continue;
            if (score > bestScore) { bestScore = score; best = x; }
        }
        return best;
    }
}
