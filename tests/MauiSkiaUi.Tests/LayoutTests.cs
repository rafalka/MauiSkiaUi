using Xunit;
using SkiaSharp;

namespace MauiSkiaUi.Tests;

public class LayoutTests
{
    [Fact]
    public void GridUsesMauiAutoStarPaddingAndAttachedProperties()
    {
        var grid = new SkUiGrid
        {
            Padding = new Thickness(10), RowSpacing = 5, ColumnSpacing = 10,
            RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)],
            ColumnDefinitions = [new ColumnDefinition(new GridLength(60)), new ColumnDefinition(GridLength.Star)]
        };
        var first = new SkUiBox { HeightRequest = 30 };
        var second = new SkUiBox();
        Grid.SetRow(second, 1);
        Grid.SetColumn(second, 1);
        grid.Children.Add(first);
        grid.Children.Add(second);
        SkUiTestHelpers.Arrange(grid, 240, 160);
        Assert.Equal(new Rect(10, 10, 60, 30), first.Frame);
        Assert.Equal(new Rect(80, 45, 150, 105), second.Frame);
        Assert.Null(first.Handler);
        grid.ColumnDefinitions[0].Width = new GridLength(80);
        SkUiTestHelpers.Arrange(grid, 240, 160);
        Assert.Equal(100, second.Frame.X);
        Grid.SetColumn(second, 0);
        Grid.SetColumnSpan(second, 2);
        SkUiTestHelpers.Arrange(grid, 240, 160);
        Assert.Equal(new Rect(10, 45, 220, 105), second.Frame);
    }

    [Fact]
    public void GridPaintsBackgroundColorInSpacingGutters()
    {
        // MAUI leaves Background as Brush.Default (empty SolidColorBrush) when only BackgroundColor is set.
        // The empty brush must not suppress BackgroundColor, or gutters between cells stay transparent.
        var grid = new SkUiGrid
        {
            BackgroundColor = Colors.LightGray,
            RowDefinitions = [new(GridLength.Star), new(GridLength.Star)],
            ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)],
            RowSpacing = 10,
            ColumnSpacing = 10,
            WidthRequest = 100,
            HeightRequest = 100
        };
        grid.Children.Add(new SkUiBox { Color = Colors.Red });
        var second = new SkUiBox { Color = Colors.Blue };
        Grid.SetColumn(second, 1);
        grid.Children.Add(second);
        Assert.True(Brush.IsNullOrEmpty(grid.Background));
        Assert.Equal(Colors.LightGray, grid.BackgroundColor);
        SkUiTestHelpers.Arrange(grid, 100, 100);
        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        grid.Paint(canvas);
        var gutter = bitmap.GetPixel(50, 50); // center of cross spacing
        Assert.Equal(SKColors.LightGray, gutter);
    }

    [Fact]
    public void CullingRespectsTransformsAndOrderCacheTracksMutations()
    {
        var layout = new SkUiLayout();
        var red = new SkUiBox { Color = Colors.Red };
        var blue = new SkUiBox { Color = Colors.Blue };
        layout.Children.Add(red);
        layout.Children.Add(blue);
        SkUiTestHelpers.Arrange(layout, 100, 100);
        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);
        layout.Paint(canvas);
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(20, 20));
        red.ZIndex = 2;
        layout.Paint(canvas);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(20, 20));
        layout.Children.Remove(red);
        layout.Paint(canvas);
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(20, 20));
        var translated = new PaintProbe { TranslationY = -120 };
        SkUiTestHelpers.Arrange(translated, 100, 100);
        translated.Paint(canvas);
        Assert.Equal(0, translated.Paints);
        translated.TranslationY = -80;
        translated.Paint(canvas);
        Assert.Equal(1, translated.Paints);
    }

    private sealed class PaintProbe : SkUiView
    {
        public int Paints { get; private set; }
        protected override void OnPaintContent(SKCanvas canvas) => Paints++;
    }

    [Fact]
    public void AbsoluteLayoutStartUpdatingDefersChildAddInvalidation()
    {
        var layout = new SkUiAbsoluteLayout();
        var host = new SkUiContentView { Content = layout };
        SkUiTestHelpers.Arrange(host, 100, 100);
        var invalidations = 0;
        host.LayoutInvalidated += (_, _) => invalidations++;

        layout.StartUpdating();
        for (var i = 0; i < 5; i++)
        {
            var box = new SkUiBox { WidthRequest = 10, HeightRequest = 10 };
            SkUiAbsoluteLayout.SetLayoutBounds(box, new Rect(i * 10, 0, 10, 10));
            layout.Children.Add(box);
        }
        Assert.Equal(0, invalidations);
        Assert.Equal(5, layout.Children.Count);
        layout.EndUpdating();
        Assert.Equal(1, invalidations);
    }

    [Fact]
    public void AbsoluteLayoutAddBatchInsertsOnceAndInvalidatesOnce()
    {
        var layout = new SkUiAbsoluteLayout();
        var host = new SkUiContentView { Content = layout };
        SkUiTestHelpers.Arrange(host, 100, 100);
        var invalidations = 0;
        host.LayoutInvalidated += (_, _) => invalidations++;

        var children = Enumerable.Range(0, 8).Select(i =>
        {
            var box = new SkUiBox { WidthRequest = 8, HeightRequest = 8 };
            SkUiAbsoluteLayout.SetLayoutBounds(box, new Rect(i * 8, 0, 8, 8));
            return (IView)box;
        }).ToArray();

        layout.Add(children);
        Assert.Equal(8, layout.Children.Count);
        Assert.Equal(1, invalidations);

        // Nested with an outer batch: Add's EndUpdating must not flush early.
        invalidations = 0;
        layout.StartUpdating();
        layout.Add([(IView)new SkUiBox { WidthRequest = 4, HeightRequest = 4 }]);
        Assert.Equal(0, invalidations);
        layout.EndUpdating();
        Assert.Equal(1, invalidations);
    }

    [Theory]
    [InlineData(typeof(SkUiView))]
    [InlineData(typeof(SkUiLayout))]
    [InlineData(typeof(SkUiContentView))]
    [InlineData(typeof(SkUiVerticalStackLayout))]
    [InlineData(typeof(SkUiHorizontalStackLayout))]
    [InlineData(typeof(SkUiAbsoluteLayout))]
    [InlineData(typeof(SkUiFlexLayout))]
    [InlineData(typeof(SkUiWrapLayout))]
    [InlineData(typeof(SkUiHorizontalShrinkLayout))]
    [InlineData(typeof(SkUiVerticalShrinkLayout))]
    [InlineData(typeof(SkUiLabel))]
    public void BackgroundColorPaintsWhenDefaultBackgroundBrushIsEmpty(Type type)
    {
        var view = (SkUiView)Activator.CreateInstance(type)!;
        view.BackgroundColor = Colors.Lime;
        view.WidthRequest = 40;
        view.HeightRequest = 40;
        SkUiTestHelpers.Arrange(view, 40, 40);
        using var bitmap = new SKBitmap(40, 40);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        view.Paint(canvas);
        Assert.Equal(SKColors.Lime, bitmap.GetPixel(20, 20));
    }

    [Fact]
    public void ExplicitBackgroundBrushWinsOverBackgroundColor()
    {
        var view = new SkUiView { Background = Colors.Blue, BackgroundColor = Colors.Red, WidthRequest = 20, HeightRequest = 20 };
        SkUiTestHelpers.Arrange(view, 20, 20);
        using var bitmap = new SKBitmap(20, 20);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        view.Paint(canvas);
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(10, 10));
    }

    [Fact]
    public void ARootArrangedWithoutAMatchingMeasureIsMeasuredAtItsArrangedSize()
    {
        // Shell's flyout on iOS and Mac Catalyst arranges its content without measuring it: before, a drawn root kept no
        // measure (or one at width 0) and its labels wrapped one character per line.
        using var _ = SkUiTestHelpers.UseBundledFont();
        SkUiLabel Caption() => new() { Text = "Performance", FontSize = 12, FontFamily = SkUiTestHelpers.BundledFontFamily };
        var expected = Caption();
        var oneLine = ((IView)expected).Measure(320, double.PositiveInfinity).Height;

        var caption = Caption();
        IView root = new SkUiScrollView { Content = new SkUiVerticalStackLayout { Children = { caption } } };
        root.Arrange(new Rect(0, 0, 320, 600));
        Assert.Equal(oneLine, caption.Height, 3);

        // Measured narrower than arranged (a first pass at width 0): measured again at the arranged width.
        var narrow = Caption();
        IView narrowRoot = new SkUiScrollView { Content = new SkUiVerticalStackLayout { Children = { narrow } } };
        narrowRoot.Measure(0, double.PositiveInfinity);
        narrowRoot.Arrange(new Rect(0, 0, 320, 600));
        Assert.Equal(oneLine, narrow.Height, 3);

        // A parent that measured with an unbounded height and arranges in a finite one keeps its measure.
        var measured = Caption();
        var stack = new SkUiVerticalStackLayout { Children = { measured } };
        IView measuredRoot = stack;
        measuredRoot.Measure(320, double.PositiveInfinity);
        var desired = measuredRoot.DesiredSize;
        measuredRoot.Arrange(new Rect(0, 0, 320, desired.Height));
        Assert.Equal(desired, measuredRoot.DesiredSize);
    }
}
