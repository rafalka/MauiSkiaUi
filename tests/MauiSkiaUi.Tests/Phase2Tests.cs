using Xunit;
using SkiaSharp;

namespace MauiSkiaUi.Tests;

public class Phase2Tests
{
    private static void Arrange(IView view, double width, double height)
    {
        view.Measure(width, height);
        view.Arrange(new Rect(0, 0, width, height));
    }

    [Fact]
    public void RadioButtonOnlySelectsAndNeverUnchecksOnRetap()
    {
        var radio = new SkUiRadioButton();
        Arrange(radio, 24, 24);
        radio.Touch(new(1, SkUiTouchAction.Pressed, new Point(12, 12)));
        radio.Touch(new(1, SkUiTouchAction.Released, new Point(12, 12)));
        Assert.True(radio.IsChecked);
        radio.Touch(new(1, SkUiTouchAction.Pressed, new Point(12, 12)));
        radio.Touch(new(1, SkUiTouchAction.Released, new Point(12, 12)));
        Assert.True(radio.IsChecked);
    }

    [Fact]
    public void BorderPaintsBackgroundColorWhenBackgroundBrushIsUnset()
    {
        var border = new SkUiBorder { BackgroundColor = Colors.Red, CornerRadius = 0, Content = new SkUiBox { Color = Colors.Red } };
        Arrange(border, 40, 40);
        using var bitmap = new SKBitmap(40, 40);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        border.Paint(canvas);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(1, 1));
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
        Arrange(grid, 100, 100);
        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        grid.Paint(canvas);
        var gutter = bitmap.GetPixel(50, 50); // center of cross spacing
        Assert.Equal(SKColors.LightGray, gutter);
    }

    [Theory]
    [InlineData(typeof(SkUiView))]
    [InlineData(typeof(SkUiLayout))]
    [InlineData(typeof(SkUiContentView))]
    [InlineData(typeof(SkUiVerticalStackLayout))]
    [InlineData(typeof(SkUiHorizontalStackLayout))]
    [InlineData(typeof(SkUiAbsoluteLayout))]
    [InlineData(typeof(SkUiLabel))]
    public void BackgroundColorPaintsWhenDefaultBackgroundBrushIsEmpty(Type type)
    {
        var view = (SkUiView)Activator.CreateInstance(type)!;
        view.BackgroundColor = Colors.Lime;
        view.WidthRequest = 40;
        view.HeightRequest = 40;
        Arrange(view, 40, 40);
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
        Arrange(view, 20, 20);
        using var bitmap = new SKBitmap(20, 20);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        view.Paint(canvas);
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(10, 10));
    }

    [Fact]
    public void ActivityIndicatorStopsClockWhenRemovedFromTree()
    {
        var layout = new SkUiLayout();
        var indicator = new SkUiActivityIndicator();
        layout.Children.Add(indicator);
        indicator.IsRunning = true;
        var clock = layout.AnimationClock;
        Assert.True(clock.IsRunning);
        layout.Children.Remove(indicator);
        Assert.False(indicator.IsRunning);
        Assert.False(clock.IsRunning);
    }

    [Fact]
    public void MauiContentViewRootRelativeFrameIncludesAncestorAndOwnTranslation()
    {
        var overlay = new SkUiMauiContentView
        {
            Content = new Editor(), WidthRequest = 20, HeightRequest = 20, TranslationX = 5, TranslationY = 3,
            HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start
        };
        var layout = new SkUiLayout { TranslationX = 100, TranslationY = 50 };
        layout.Children.Add(overlay);
        var root = new SkUiContentView { Content = layout };
        Arrange(root, 200, 200);
        // root(0,0) -> layout Frame(0,0) + Translation(100,50) -> overlay Frame(0,0) + Translation(5,3).
        Assert.Equal(new Rect(105, 53, 20, 20), overlay.ComputeRootRelativeFrame());
    }

    [Fact]
    public void MauiContentViewMeasuresArrangesContentAndNeverConsumesTouch()
    {
        // A plain MAUI VisualElement without a platform handler cannot report a real measured size in
        // headless tests (that requires native text measurement); this is an FR-16 test limitation, not a
        // defect. Arrange still delegates the given bounds to Content regardless of the measured size.
        var editor = new Editor { Text = "hello" };
        var host = new SkUiMauiContentView { Content = editor };
        Assert.Equal(Size.Zero, ((IView)host).Measure(200, 200));
        ((IView)host).Arrange(new Rect(0, 0, 120, 40));
        Assert.Equal(new Rect(0, 0, 120, 40), editor.Frame);
        Assert.False(host.Touch(new(1, SkUiTouchAction.Pressed, new Point(10, 10))));
    }

    [Fact]
    public void MauiContentViewRejectsAlreadyOwnedContent()
    {
        var editor = new Editor();
        _ = new SkUiMauiContentView { Content = editor };
        Assert.Throws<InvalidOperationException>(() => new SkUiMauiContentView().SetContent(editor));
    }

    [Fact]
    public void MauiContentViewComputesRootRelativeFrameThroughNestedHostedLayouts()
    {
        var overlay = new SkUiMauiContentView
        {
            Content = new Editor(), WidthRequest = 50, HeightRequest = 20,
            HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start
        };
        var grid = new SkUiGrid { Padding = new Thickness(10) };
        Grid.SetRow(overlay, 0);
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.Children.Add(overlay);
        var layout = new SkUiLayout();
        layout.Children.Add(grid);
        var root = new SkUiContentView { Content = layout };
        ((IView)root).Measure(300, 300);
        ((IView)root).Arrange(new Rect(0, 0, 300, 300));
        // root(0,0) -> layout(0,0, no padding) -> grid(Padding 10) -> overlay: expect (10,10,50,20).
        Assert.Equal(new Rect(10, 10, 50, 20), overlay.ComputeRootRelativeFrame());
    }

    [Fact]
    public void MauiContentViewNotifyHooksAreSafeNoOpsWithoutAPlatformRoot()
    {
        var host = new SkUiMauiContentView { Content = new Editor() };
        host.NotifyRootAttached();
        host.NotifyRootDetached();
    }
}
