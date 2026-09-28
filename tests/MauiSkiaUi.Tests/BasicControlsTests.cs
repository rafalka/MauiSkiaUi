using Xunit;
using SkiaSharp;

namespace MauiSkiaUi.Tests;

public class BasicControlsTests
{
    [Fact]
    public void RegisteredFontResolvesAheadOfSystemLookupAndCachesTypeface()
    {
        var fontPath = Path.Combine(AppContext.BaseDirectory, "Assets", "RobotoMono-Regular.ttf");
        var opens = 0;
        SkUiFonts.Register("Test-RobotoMono-Cache", () => { opens++; return File.OpenRead(fontPath); });
        try
        {
            var label = new SkUiLabel { Text = "Monospace", FontFamily = "Test-RobotoMono-Cache" };
            SkUiTestHelpers.Arrange(label, 200, 40);
            using var bitmap = new SKBitmap(200, 40);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);
            label.Paint(canvas);
            Assert.Contains(bitmap.Pixels, pixel => pixel.Alpha > 0);
            label.Paint(canvas);
            Assert.Equal(1, opens);
        }
        finally { SkUiFonts.Unregister("Test-RobotoMono-Cache"); }
        Assert.Null(SkUiFonts.TryResolve("Test-RobotoMono-Cache"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ButtonWithEmptyBackgroundBrushFallsBackToFill(bool pressed)
    {
        var button = new SkUiButton { Background = new SolidColorBrush(), FillColor = Colors.Red };
        SkUiTestHelpers.Arrange(button, 100, 50);
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
        SkUiTestHelpers.Arrange(button, 100, 50);
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

    [Fact]
    public void SharedChromeSwitchAndCheckBoxPaintersProduceOpaquePixels()
    {
        using var bitmap = new SKBitmap(51, 31);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        SkUiChrome.DrawSwitch(canvas, new SKRect(0, 0, 51, 31), isChecked: true, SKColors.Teal, SKColors.White);
        Assert.Contains(bitmap.Pixels, pixel => pixel.Alpha > 0);

        using var checkBitmap = new SKBitmap(24, 24);
        using var checkCanvas = new SKCanvas(checkBitmap);
        checkCanvas.Clear(SKColors.Transparent);
        SkUiChrome.DrawCheckBox(checkCanvas, 24, isChecked: true, SKColors.Teal, SKColors.Teal);
        Assert.Contains(checkBitmap.Pixels, pixel => pixel.Alpha > 0);
    }

    [Theory]
    [InlineData(Aspect.AspectFit, 100, 50)]
    [InlineData(Aspect.AspectFill, 200, 100)]
    [InlineData(Aspect.Fill, 100, 100)]
    public void SharedChromeImageDestinationRespectsAspect(Aspect aspect, float expectedWidth, float expectedHeight)
    {
        var dest = SkUiChrome.ComputeImageDestination(100, 100, 200, 100, aspect);
        Assert.Equal(expectedWidth, dest.Width, precision: 2);
        Assert.Equal(expectedHeight, dest.Height, precision: 2);
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
        SkUiTestHelpers.Arrange(button, 100, 50);
        button.Touch(new(1, SkUiTouchAction.Pressed, new Point(20, 20)));
        Assert.Equal(Colors.Red, button.FillColor);
        button.Touch(new(1, SkUiTouchAction.Cancelled, new Point(20, 20)));
        Assert.Equal(SkUiColors.Accent, button.FillColor);
    }

    [Fact]
    public async Task ImageLoadsStreamsFitsAndReportsErrors()
    {
        using var image = new SkUiImage { Source = ImageSource.FromStream(() => new MemoryStream(ImageBytes(SKColors.Red))) };
        await image.LoadingTask;
        Assert.Null(image.LoadError);
        Assert.False(image.IsLoading);
        Assert.Equal(new Size(40, 20), image.ImageSize);
        SkUiTestHelpers.Arrange(image, 100, 100);
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
        SkUiTestHelpers.Arrange(image, 40, 20);
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
    public void LabelWrapsAndDirectSettersDoNotWriteBack()
    {
        using var font = SkUiTestHelpers.UseBundledFont();
        var label = new SkUiLabel
        {
            Text = "alpha beta gamma delta",
            FontSize = 20,
            FontFamily = SkUiTestHelpers.BundledFontFamily,
        };
        var wide = ((IView)label).Measure(400, 300);
        var narrow = ((IView)label).Measure(70, 300);
        Assert.True(narrow.Height > wide.Height);
        label.SetText("direct").SetTextColor(Colors.Red);
        Assert.Equal("direct", label.Text);
        Assert.Equal("alpha beta gamma delta", label.GetValue(SkUiLabel.TextProperty));
        label.Text = "bound";
        Assert.Equal("bound", label.Text);
        SkUiTestHelpers.Arrange(label, 160, 60);
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
        SkUiTestHelpers.Arrange(button, 120, 50);
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
    public void RadioButtonOnlySelectsAndNeverUnchecksOnRetap()
    {
        var radio = new SkUiRadioButton();
        SkUiTestHelpers.Arrange(radio, 24, 24);
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
        SkUiTestHelpers.Arrange(border, 40, 40);
        using var bitmap = new SKBitmap(40, 40);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        border.Paint(canvas);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(1, 1));
    }

    [Fact]
    public void BorderAcceptsPerCornerRadii()
    {
        var border = new SkUiBorder
        {
            BackgroundColor = Colors.Red,
            CornerRadius = new CornerRadius(16, 0, 0, 16),
            Stroke = Colors.Black,
            StrokeThickness = 2,
        };
        Assert.Equal(16, border.CornerRadius.TopLeft);
        Assert.Equal(0, border.CornerRadius.TopRight);
        SkUiTestHelpers.Arrange(border, 60, 40);
        using var bitmap = new SKBitmap(60, 40);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        border.Paint(canvas);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(30, 20));
    }

    [Fact]
    public void BorderStrokeIsPaintedAboveOpaqueContent()
    {
        var border = new SkUiBorder
        {
            BackgroundColor = Colors.White,
            Stroke = Colors.Red,
            StrokeThickness = 4,
            CornerRadius = 0,
            Content = new SkUiBox { Color = Colors.White },
        };
        SkUiTestHelpers.Arrange(border, 40, 40);
        using var bitmap = new SKBitmap(40, 40);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        border.Paint(canvas);
        var edge = bitmap.GetPixel(1, 20);
        Assert.True(edge.Red > 200 && edge.Green < 80 && edge.Blue < 80, $"Expected red stroke at edge, got {edge}");
    }

    [Fact]
    public void ActivityIndicatorSpinsOnRenderThreadAndStopsWhenRemovedFromTree()
    {
        var layout = new SkUiLayout();
        var indicator = new SkUiActivityIndicator { WidthRequest = 20, HeightRequest = 20 };
        layout.Children.Add(indicator);
        using var surface = new SkUiTestSurface(layout, 40, 40);
        indicator.IsRunning = true;
        surface.Frame(0);
        Assert.True(surface.NeedsFrame);
        var recorded = surface.RecordedPictures;
        surface.Frame(300);
        Assert.Equal(recorded, surface.RecordedPictures);

        layout.Children.Remove(indicator);
        surface.Frame(600);
        // Intent stays true; the detached node is no longer composited, so frames stop.
        Assert.True(indicator.IsRunning);
        Assert.False(surface.NeedsFrame);
    }

    [Fact]
    public void ActivityIndicatorRunningBeforeParentingSpinsOnceAttached()
    {
        var indicator = new SkUiActivityIndicator { IsRunning = true, WidthRequest = 20, HeightRequest = 20 };
        var layout = new SkUiLayout();
        layout.Children.Add(indicator);
        var root = new SkUiContentView { Content = layout };
        using var surface = new SkUiTestSurface(root, 40, 40);
        surface.Frame(0);
        Assert.True(indicator.IsRunning);
        Assert.True(surface.NeedsFrame);
    }

    [Fact]
    public void ActivityIndicatorStopsWhenAncestorLayoutDetachedAndResumesWhenRehosted()
    {
        var indicator = new SkUiActivityIndicator { WidthRequest = 20, HeightRequest = 20 };
        var inner = new SkUiLayout();
        inner.Children.Add(indicator);
        var host1 = new SkUiContentView { Content = inner };
        using var surface1 = new SkUiTestSurface(host1, 40, 40);
        indicator.IsRunning = true;
        surface1.Frame(0);
        Assert.True(surface1.NeedsFrame);

        host1.Content = null;
        surface1.Frame(100);
        Assert.True(indicator.IsRunning);
        Assert.False(surface1.NeedsFrame);

        var host2 = new SkUiContentView { HwAccelerated = false, Content = inner };
        using var surface2 = new SkUiTestSurface(host2, 40, 40);
        surface2.Frame(0);
        Assert.True(surface2.NeedsFrame);
    }

    [Fact]
    public void ActivityIndicatorSpinIsCulledWhenHidden()
    {
        var indicator = new SkUiActivityIndicator { IsRunning = true, WidthRequest = 20, HeightRequest = 20 };
        var root = new SkUiContentView { Content = indicator };
        using var surface = new SkUiTestSurface(root, 40, 40);
        surface.Frame(0);
        Assert.True(surface.NeedsFrame);
        indicator.IsVisible = false;
        SkUiTestHelpers.Arrange(root, 40, 40);
        surface.Frame(100);
        Assert.False(surface.NeedsFrame);
    }
}
