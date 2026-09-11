using Xunit;
using SkiaSharp;
using System.Diagnostics;
using Xunit.Abstractions;

namespace MauiSkiaUi.Tests;

public class Phase1Tests(ITestOutputHelper output)
{
    [Fact]
    public void RegisteredFontResolvesAheadOfSystemLookupAndCachesTypeface()
    {
        var fontPath = Path.Combine(AppContext.BaseDirectory, "Assets", "RobotoMono-Regular.ttf");
        var opens = 0;
        SkUiFonts.Register("Test-RobotoMono", () => { opens++; return File.OpenRead(fontPath); });
        try
        {
            var label = new SkUiLabel { Text = "Monospace", FontFamily = "Test-RobotoMono" };
            Arrange(label, 200, 40);
            using var bitmap = new SKBitmap(200, 40);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);
            label.Paint(canvas);
            Assert.Contains(bitmap.Pixels, pixel => pixel.Alpha > 0);
            label.Paint(canvas);
            Assert.Equal(1, opens);
        }
        finally { SkUiFonts.Unregister("Test-RobotoMono"); }
        Assert.Null(SkUiFonts.TryResolve("Test-RobotoMono"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ButtonWithEmptyBackgroundBrushFallsBackToFill(bool pressed)
    {
        var button = new SkUiButton { Background = new SolidColorBrush(), FillColor = Colors.Red };
        Arrange(button, 100, 50);
        if (pressed) button.Touch(new(1, SkUiTouchAction.Pressed, new Point(20, 20)));
        using var bitmap = new SKBitmap(100, 50);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        button.Paint(canvas);
        var pixel = bitmap.GetPixel(20, 20);
        Assert.Equal(255, pixel.Red);
        Assert.Equal(pressed ? 191 : 255, pixel.Alpha);
    }

    [Fact]
    public void ButtonRunsOnlyItsOwnCommandNotInheritedTappedCommand()
    {
        var buttonClicks = 0;
        var tappedCommandRuns = 0;
        var tappedEvents = 0;
        var button = new SkUiButton { Command = new Command(() => buttonClicks++) };
        button.SetTappedCommand(new Command(() => tappedCommandRuns++));
        button.Tapped += (_, _) => tappedEvents++;
        Arrange(button, 100, 50);
        button.Touch(new(1, SkUiTouchAction.Pressed, new Point(20, 20)));
        button.Touch(new(1, SkUiTouchAction.Released, new Point(20, 20)));
        Assert.Equal(1, buttonClicks);
        Assert.Equal(0, tappedCommandRuns);
        Assert.Equal(1, tappedEvents);
    }

    [Fact]
    public void ButtonRoundedClipPathExcludesCornerWedgeButIncludesInterior()
    {
        using var path = SkUiChrome.CreateRoundRectPath(new SKRect(0, 0, 100, 60), 20);
        Assert.False(path.Contains(2, 2));
        Assert.True(path.Contains(50, 30));
        Assert.True(path.Contains(20, 2));
    }

    [Theory]
    [InlineData(ScrollOrientation.Vertical, 0, 40)]
    [InlineData(ScrollOrientation.Horizontal, 40, 0)]
    [InlineData(ScrollOrientation.Both, 0, 40)]
    public void WheelScrollsDocumentedAxisPerOrientation(ScrollOrientation orientation, double expectedX, double expectedY)
    {
        var scroll = new SkUiScrollView { Content = new SkUiBox { WidthRequest = 400, HeightRequest = 400 }, Orientation = orientation };
        Arrange(scroll, 100, 100);
        scroll.Touch(new(1, SkUiTouchAction.Wheel, new Point(50, 50), WheelDelta: -40));
        Assert.Equal(expectedX, scroll.ScrollX);
        Assert.Equal(expectedY, scroll.ScrollY);
    }

    [Fact]
    public void ButtonDefaultsAllowStylesAndVisualStateRestoration()
    {
        var style = new Style(typeof(SkUiButton));
        style.Setters.Add(new Setter { Property = SkUiLabel.TextColorProperty, Value = Colors.Black });
        style.Setters.Add(new Setter { Property = SkUiLabel.PaddingProperty, Value = default(Thickness) });
        var button = new SkUiButton { Style = style };
        Assert.Equal(Colors.Black, button.TextColor);
        Assert.Equal(default, button.Padding);
        button.Style = null;
        Assert.Equal(Colors.White, button.TextColor);
        Assert.Equal(new Thickness(18, 12), button.Padding);
        var normal = new VisualState { Name = "Normal" };
        var pressed = new VisualState { Name = "Pressed" };
        pressed.Setters.Add(new Setter { Property = SkUiButton.FillColorProperty, Value = Colors.Red });
        var group = new VisualStateGroup { Name = "CommonStates", States = { normal, pressed } };
        VisualStateManager.SetVisualStateGroups(button, new VisualStateGroupList { group });
        Arrange(button, 100, 50);
        button.Touch(new(1, SkUiTouchAction.Pressed, new Point(20, 20)));
        Assert.Equal(Colors.Red, button.FillColor);
        button.Touch(new(1, SkUiTouchAction.Cancelled, new Point(20, 20)));
        Assert.Equal(SkUiColors.Accent, button.FillColor);
    }

    [Fact]
    public async Task AsyncScrollCompletesAndDisablingCancelsMotion()
    {
        var scroll = new SkUiScrollView { Content = new SkUiBox { HeightRequest = 500 } };
        Arrange(scroll, 100, 100);
        var completed = scroll.ScrollToAsync(0, 200);
        scroll.AnimationClock.Tick(TimeSpan.FromSeconds(1));
        await completed;
        Assert.Equal(200, scroll.ScrollY);
        var cancelled = scroll.ScrollToAsync(0, 300);
        scroll.IsEnabled = false;
        Assert.False(scroll.AnimationClock.IsRunning);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
    }

    [Theory]
    [InlineData(double.NaN, 0, "horizontalOffset")]
    [InlineData(0, double.PositiveInfinity, "verticalOffset")]
    [InlineData(double.NegativeInfinity, double.NaN, "horizontalOffset")]
    public void ScrollOffsetValidationNamesTheInvalidArgument(double x, double y, string paramName)
    {
        var scroll = new SkUiScrollView { Content = new SkUiBox { WidthRequest = 400, HeightRequest = 400 } };
        Arrange(scroll, 100, 100);
        var sync = Assert.Throws<ArgumentOutOfRangeException>(() => scroll.ScrollTo(x, y));
        Assert.Equal(paramName, sync.ParamName);
        var async = Assert.Throws<ArgumentOutOfRangeException>(() => { _ = scroll.ScrollToAsync(x, y); });
        Assert.Equal(paramName, async.ParamName);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void ScrolledCompositionMatchesFullPixelGolden(int density)
    {
        var grid = new SkUiGrid { RowDefinitions = [new(new GridLength(8)), new(new GridLength(8))] };
        grid.Children.Add(new SkUiBox { Color = Colors.Red });
        var blue = new SkUiBox { Color = Colors.Blue };
        Grid.SetRow(blue, 1);
        grid.Children.Add(blue);
        var scroll = new SkUiScrollView { Content = grid };
        Arrange(scroll, 8, 8);
        scroll.ScrollTo(0, 4);
        using var bitmap = new SKBitmap(8 * density, 8 * density);
        using var canvas = new SKCanvas(bitmap);
        canvas.Scale(density);
        scroll.Paint(canvas);
        for (var row = 0; row < bitmap.Height; row++)
            for (var column = 0; column < bitmap.Width; column++)
                Assert.Equal(row < 4 * density ? SKColors.Red : SKColors.Blue, bitmap.GetPixel(column, row));
    }

    [Fact]
    public void ScrollLivePaintsWhileContentAnimationsRunWithoutRebuildingPicture()
    {
        var indicator = new SkUiActivityIndicator { IsRunning = true, WidthRequest = 36, HeightRequest = 36 };
        var scroll = new SkUiScrollView { Content = indicator };
        Arrange(scroll, 40, 40);
        using var bitmap = new SKBitmap(40, 40);
        using var canvas = new SKCanvas(bitmap);

        scroll.Paint(canvas);
        Assert.Equal(0, scroll.ContentPictureRebuilds);
        Assert.False(scroll.HasContentPicture);

        scroll.AnimationClock.Tick(TimeSpan.FromMilliseconds(250));
        scroll.Paint(canvas);
        Assert.Equal(0, scroll.ContentPictureRebuilds);

        indicator.IsRunning = false;
        scroll.Paint(canvas);
        Assert.Equal(1, scroll.ContentPictureRebuilds);
        Assert.True(scroll.HasContentPicture);
    }

    [Fact]
    public void ScrollContentPictureCacheSurvivesOffsetOnlyInvalidation()
    {
        var grid = new SkUiGrid { RowDefinitions = [new(new GridLength(8)), new(new GridLength(8))] };
        var red = new SkUiBox { Color = Colors.Red };
        var blue = new SkUiBox { Color = Colors.Blue };
        Grid.SetRow(blue, 1);
        grid.Children.Add(red);
        grid.Children.Add(blue);
        var scroll = new SkUiScrollView { Content = grid };
        Arrange(scroll, 8, 8);
        using var bitmap = new SKBitmap(8, 8);
        using var canvas = new SKCanvas(bitmap);

        scroll.Paint(canvas);
        Assert.Equal(1, scroll.ContentPictureRebuilds);
        Assert.True(scroll.HasContentPicture);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(4, 2));

        scroll.ScrollTo(0, 4);
        scroll.Paint(canvas);
        Assert.Equal(1, scroll.ContentPictureRebuilds);
        Assert.True(scroll.HasContentPicture);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(4, 2));
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(4, 6));

        red.Color = Colors.Lime;
        scroll.Paint(canvas);
        Assert.Equal(2, scroll.ContentPictureRebuilds);
        scroll.ScrollTo(0, 0);
        scroll.Paint(canvas);
        Assert.Equal(2, scroll.ContentPictureRebuilds);
        Assert.Equal(SKColors.Lime, bitmap.GetPixel(4, 2));
    }

    [Fact]
    public void ScrollContentPicturePaintsFullViewportWhenExtentNarrowerThanViewport()
    {
        // Measure with a narrow constraint (extent stays narrow), then arrange into a wider viewport.
        // Arrange expands content via max(extent, viewport); the picture must use that same space.
        var grid = new SkUiGrid
        {
            ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)],
            RowDefinitions = [new(new GridLength(40))],
        };
        var left = new SkUiBox { Color = Colors.Red };
        var right = new SkUiBox { Color = Colors.Blue };
        Grid.SetColumn(right, 1);
        grid.Children.Add(left);
        grid.Children.Add(right);
        var scroll = new SkUiScrollView { Content = grid };
        ((IView)scroll).Measure(40, 40);
        ((IView)scroll).Arrange(new Rect(0, 0, 100, 40));
        Assert.True(scroll.ContentSize.Width <= 40);

        using var bitmap = new SKBitmap(100, 40);
        using var canvas = new SKCanvas(bitmap);
        scroll.Paint(canvas);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(20, 20));
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(80, 20));
    }

    [Fact]
    public void ScrollContentPictureSurvivesLongOffsetScroll()
    {
        // Full content-space picture: scrolling far must not rebuild (only translate).
        var column = new SkUiVerticalStackLayout();
        for (var i = 0; i < 8; i++)
            column.Children.Add(new SkUiBox
            {
                Color = i % 2 == 0 ? Colors.Red : Colors.Blue,
                HeightRequest = 50,
                WidthRequest = 40,
            });
        var scroll = new SkUiScrollView { Content = column };
        Arrange(scroll, 40, 50);
        using var bitmap = new SKBitmap(40, 50);
        using var canvas = new SKCanvas(bitmap);

        scroll.Paint(canvas);
        Assert.Equal(1, scroll.ContentPictureRebuilds);

        scroll.ScrollTo(0, 40);
        scroll.Paint(canvas);
        Assert.Equal(1, scroll.ContentPictureRebuilds);

        scroll.ScrollTo(0, 200);
        scroll.Paint(canvas);
        Assert.Equal(1, scroll.ContentPictureRebuilds);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(20, 25));
    }

    [Fact]
    public void ScrollTapDoesNotRebuildContentPicture()
    {
        var button = new SkUiButton { Text = "Go", WidthRequest = 80, HeightRequest = 40 };
        var clicks = 0;
        button.Clicked += (_, _) => clicks++;
        var scroll = new SkUiScrollView { Content = button };
        Arrange(scroll, 100, 80);
        using var bitmap = new SKBitmap(100, 80);
        using var canvas = new SKCanvas(bitmap);

        scroll.Paint(canvas);
        Assert.Equal(1, scroll.ContentPictureRebuilds);

        Assert.True(scroll.Touch(new(1, SkUiTouchAction.Pressed, new Point(20, 20), TimeSpan.Zero)));
        Assert.True(scroll.Touch(new(1, SkUiTouchAction.Released, new Point(20, 20), TimeSpan.FromMilliseconds(20))));
        scroll.Paint(canvas);
        Assert.Equal(1, clicks);
        Assert.Equal(1, scroll.ContentPictureRebuilds);
    }

    [Fact]
    public void ScrollContentPictureRebuildDeferredDuringPointerGesture()
    {
        var button = new SkUiButton { Text = "Go", WidthRequest = 80, HeightRequest = 200 };
        var scroll = new SkUiScrollView { Content = button };
        Arrange(scroll, 100, 80);
        using var bitmap = new SKBitmap(100, 80);
        using var canvas = new SKCanvas(bitmap);

        scroll.Paint(canvas);
        Assert.Equal(1, scroll.ContentPictureRebuilds);

        Assert.True(scroll.Touch(new(1, SkUiTouchAction.Pressed, new Point(20, 20), TimeSpan.Zero)));
        scroll.Paint(canvas);
        Assert.Equal(1, scroll.ContentPictureRebuilds);
        Assert.True(scroll.HasContentPicture);

        Assert.True(scroll.Touch(new(1, SkUiTouchAction.Moved, new Point(20, -40), TimeSpan.FromMilliseconds(50))));
        scroll.Paint(canvas);
        Assert.Equal(1, scroll.ContentPictureRebuilds);

        // Pan never pressed the child, so finger-up must not rebuild the content picture.
        Assert.True(scroll.Touch(new(1, SkUiTouchAction.Released, new Point(20, -40), TimeSpan.FromMilliseconds(200))));
        scroll.Paint(canvas);
        Assert.Equal(1, scroll.ContentPictureRebuilds);
    }

    [Fact]
    public void CullingRespectsTransformsAndOrderCacheTracksMutations()
    {
        var layout = new SkUiLayout();
        var red = new SkUiBox { Color = Colors.Red };
        var blue = new SkUiBox { Color = Colors.Blue };
        layout.Children.Add(red);
        layout.Children.Add(blue);
        Arrange(layout, 100, 100);
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
        Arrange(translated, 100, 100);
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
    public void ThousandLabelRecordingMeasurement()
    {
        var grid = new SkUiGrid();
        for (var index = 0; index < 1000; index++)
        {
            grid.RowDefinitions.Add(new RowDefinition(new GridLength(36)));
            var label = new SkUiLabel { Text = $"Sample {index:0000}", FontSize = 16 };
            Grid.SetRow(label, index);
            grid.Children.Add(label);
        }
        var scroll = new SkUiScrollView { Content = grid };
        var timer = Stopwatch.StartNew();
        Arrange(scroll, 400, 600);
        output.WriteLine($"Initial layout: {timer.Elapsed.TotalMilliseconds:F2} ms");
        using var recorder = new SKPictureRecorder();
        void Record()
        {
            var canvas = recorder.BeginRecording(new SKRect(0, 0, 400, 600));
            scroll.Paint(canvas);
            using var picture = recorder.EndRecording();
        }
        Record();
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        timer.Restart();
        for (var frame = 0; frame < 30; frame++) Record();
        output.WriteLine($"Warm recording: {timer.Elapsed.TotalMilliseconds / 30:F3} ms/frame; {(GC.GetAllocatedBytesForCurrentThread() - allocated) / 30} managed bytes/frame");
        Assert.Equal(1000, grid.Children.Count);
        Assert.All(grid.Children, child => Assert.Null(child.Handler));
    }

    [Fact]
    public async Task ImageLoadsStreamsFitsAndReportsErrors()
    {
        using var image = new SkUiImage { Source = ImageSource.FromStream(() => new MemoryStream(ImageBytes(SKColors.Red))) };
        await image.LoadingTask;
        Assert.Null(image.LoadError);
        Assert.False(image.IsLoading);
        Assert.Equal(new Size(40, 20), image.ImageSize);
        Arrange(image, 100, 100);
        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        image.Paint(canvas);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(50, 50));
        Assert.Equal(0, bitmap.GetPixel(50, 10).Alpha);
        image.Aspect = Aspect.AspectFill;
        image.Paint(canvas);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(50, 10));
        image.Source = ImageSource.FromStream(() => new MemoryStream([1, 2, 3]));
        await image.LoadingTask;
        Assert.NotNull(image.LoadError);
        Assert.Equal(Size.Zero, image.ImageSize);
    }

    [Fact]
    public async Task SupersededImageLoadCannotReplaceNewSource()
    {
        var completion = new TaskCompletionSource<Stream>();
        using var image = new SkUiImage { Source = new StreamImageSource { Stream = _ => completion.Task } };
        var previousLoad = image.LoadingTask;
        image.Source = ImageSource.FromStream(() => new MemoryStream(ImageBytes(SKColors.Blue)));
        await image.LoadingTask;
        completion.SetResult(new MemoryStream(ImageBytes(SKColors.Red)));
        await previousLoad;
        Arrange(image, 40, 20);
        using var bitmap = new SKBitmap(40, 20);
        using var canvas = new SKCanvas(bitmap);
        image.Paint(canvas);
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(20, 10));
    }

    private static byte[] ImageBytes(SKColor color)
    {
        using var bitmap = new SKBitmap(40, 20);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    [Fact]
    public void ScrollClampsOffsetsAndDoesNotRemeasureContent()
    {
        var child = new MeasureProbe { WidthRequest = 400, HeightRequest = 600 };
        var scroll = new SkUiScrollView { Content = child, Orientation = ScrollOrientation.Both };
        Arrange(scroll, 100, 150);
        var count = child.Measures;
        scroll.ScrollTo(900, 900);
        Assert.Equal(300, scroll.ScrollX);
        Assert.Equal(450, scroll.ScrollY);
        // Offset is applied in paint/touch only; arranged frame stays at the content origin.
        Assert.Equal(new Rect(0, 0, 400, 600), child.Frame);
        Assert.Equal(count, child.Measures);
        scroll.ScrollTo(-1, -1);
        Assert.Equal(0, scroll.ScrollY);
        child.HeightRequest = 100;
        Arrange(scroll, 100, 150);
        Assert.Equal(0, scroll.ScrollY);
    }

    [Fact]
    public void ScrollPanCancelsButtonAndFlingUsesClock()
    {
        var button = new SkUiButton { HeightRequest = 500 };
        var scroll = new SkUiScrollView { Content = button };
        Arrange(scroll, 150, 100);
        var clicks = 0;
        button.Clicked += (_, _) => clicks++;
        scroll.Touch(new(1, SkUiTouchAction.Pressed, new Point(30, 80), TimeSpan.Zero));
        Assert.False(button.IsPressed);
        scroll.Touch(new(1, SkUiTouchAction.Released, new Point(30, 80), TimeSpan.FromMilliseconds(20)));
        Assert.Equal(1, clicks);
        Assert.False(button.IsPressed);
        scroll.Touch(new(2, SkUiTouchAction.Pressed, new Point(30, 80), TimeSpan.FromMilliseconds(100)));
        Assert.False(button.IsPressed);
        scroll.Touch(new(2, SkUiTouchAction.Moved, new Point(30, 20), TimeSpan.FromMilliseconds(150)));
        Assert.False(button.IsPressed);
        Assert.Equal(60, scroll.ScrollY);
        scroll.Touch(new(2, SkUiTouchAction.Released, new Point(30, 20), TimeSpan.FromMilliseconds(160)));
        Assert.True(scroll.AnimationClock.IsRunning);
        scroll.AnimationClock.Tick(TimeSpan.FromMilliseconds(200));
        Assert.True(scroll.ScrollY > 60);
        scroll.AnimationClock.Tick(TimeSpan.FromSeconds(2));
        Assert.False(scroll.AnimationClock.IsRunning);
        Assert.Equal(1, clicks);
    }

    private sealed class MeasureProbe : SkUiBox
    {
        public int Measures { get; private set; }
        protected override Size MeasureContent(double widthConstraint, double heightConstraint)
        {
            Measures++;
            return base.MeasureContent(widthConstraint, heightConstraint);
        }
    }

    [Fact]
    public void LabelWrapsAndDirectSettersDoNotWriteBack()
    {
        var label = new SkUiLabel { Text = "alpha beta gamma delta", FontSize = 20 };
        var wide = ((IView)label).Measure(400, 300);
        var narrow = ((IView)label).Measure(70, 300);
        Assert.True(narrow.Height > wide.Height);
        label.SetText("direct").SetTextColor(Colors.Red);
        Assert.Equal("direct", label.Text);
        Assert.Equal("alpha beta gamma delta", label.GetValue(SkUiLabel.TextProperty));
        label.Text = "bound";
        Assert.Equal("bound", label.Text);
        Arrange(label, 160, 60);
        using var bitmap = new SKBitmap(160, 60);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        label.Paint(canvas);
        Assert.Contains(bitmap.Pixels, pixel => pixel.Red > 0 && pixel.Alpha > 0);
        Assert.False(label.Touch(new(1, SkUiTouchAction.Pressed, new Point(10, 10))));
    }

    [Fact]
    public void ButtonPressCommandCancellationAndRoundedPixels()
    {
        var clicks = 0;
        var enabled = true;
        var button = new SkUiButton { Text = "Run", Command = new Command(() => clicks++, () => enabled) };
        Arrange(button, 120, 50);
        Assert.True(button.Touch(new(1, SkUiTouchAction.Pressed, new Point(20, 20))));
        Assert.True(button.IsPressed);
        button.Touch(new(1, SkUiTouchAction.Released, new Point(20, 20)));
        Assert.False(button.IsPressed);
        Assert.Equal(1, clicks);
        button.Touch(new(1, SkUiTouchAction.Pressed, new Point(20, 20)));
        button.Touch(new(1, SkUiTouchAction.Cancelled, new Point(20, 20)));
        Assert.Equal(1, clicks);
        enabled = false;
        button.Touch(new(1, SkUiTouchAction.Pressed, new Point(20, 20)));
        button.Touch(new(1, SkUiTouchAction.Released, new Point(20, 20)));
        Assert.Equal(1, clicks);
        using var bitmap = new SKBitmap(120, 50);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        button.Paint(canvas);
        Assert.Equal(0, bitmap.GetPixel(0, 0).Alpha);
        Assert.Equal(255, bitmap.GetPixel(10, 10).Alpha);
    }

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
        Arrange(grid, 240, 160);
        Assert.Equal(new Rect(10, 10, 60, 30), first.Frame);
        Assert.Equal(new Rect(80, 45, 150, 105), second.Frame);
        Assert.Null(first.Handler);
        grid.ColumnDefinitions[0].Width = new GridLength(80);
        Arrange(grid, 240, 160);
        Assert.Equal(100, second.Frame.X);
        Grid.SetColumn(second, 0);
        Grid.SetColumnSpan(second, 2);
        Arrange(grid, 240, 160);
        Assert.Equal(new Rect(10, 45, 220, 105), second.Frame);
    }

    private static void Arrange(IView view, double width, double height)
    {
        view.Measure(width, height);
        view.Arrange(new Rect(0, 0, width, height));
    }
}