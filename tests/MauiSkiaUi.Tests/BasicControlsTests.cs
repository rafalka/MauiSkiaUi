using MauiSkiaUi.Core;
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
        SkUiLook.Current.DrawSwitch(canvas, new SkUiSwitchPaint(new SKRect(0, 0, 51, 31), SkUiToggleVisual.Settled(SkUiCheckState.Checked),
            SKColors.Teal, SKColors.Gray, SKColors.White, IsEnabled: true));
        Assert.Contains(bitmap.Pixels, pixel => pixel.Alpha > 0);

        using var checkBitmap = new SKBitmap(24, 24);
        using var checkCanvas = new SKCanvas(checkBitmap);
        checkCanvas.Clear(SKColors.Transparent);
        SkUiLook.Current.DrawCheckBox(checkCanvas, new SkUiCheckBoxPaint(24, SkUiToggleVisual.Settled(SkUiCheckState.Checked),
            SKColors.Teal, SKColors.White, SKColors.Gray, IsEnabled: true));
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
    public void LabelDrawsRoundedBadgeChromeWithoutAWrappingBorder()
    {
        var plain = new SkUiLabel { BackgroundColor = Colors.Red };
        Assert.Equal(0, plain.CornerRadius);
        Assert.Equal(SkUiLook.Current.DefaultButtonCornerRadius, new SkUiButton().CornerRadius);
        var badge = new SkUiLabel { Text = "12", BackgroundColor = Colors.Red, CornerRadius = 100, BorderColor = Colors.Blue, BorderWidth = 2 };
        foreach (var (label, cornerAlpha) in new[] { (plain, 255), (badge, 0) })
        {
            SkUiTestHelpers.Arrange(label, 80, 40);
            using var bitmap = new SKBitmap(80, 40);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);
            label.Paint(canvas);
            Assert.Equal(cornerAlpha, bitmap.GetPixel(0, 0).Alpha); // a radius beyond half the height gives a pill
            Assert.Equal(SKColors.Red, bitmap.GetPixel(40, 3));
            if (label == badge)
                Assert.Equal(SKColors.Blue, bitmap.GetPixel(40, 0)); // border inside the bounds
        }

        var core = new SkUiCoreLabel().SetText("12").SetFillColor(Colors.Red).SetCornerRadius(100).SetBorderColor(Colors.Blue).SetBorderWidth(2);
        using var surface = new SkUiTestSurface(new SkUiCoreHost { Background = Colors.White }.SetContent(core), 80, 40);
        var frame = surface.Frame();
        Assert.Equal(SKColors.White, frame.GetPixel(0, 0));
        Assert.Equal(SKColors.Red, frame.GetPixel(40, 3));
        Assert.Equal(SKColors.Blue, frame.GetPixel(40, 0));
        Assert.Equal(SkUiLook.Current.DefaultButtonCornerRadius, new SkUiCoreButton().CornerRadius);
        Assert.Equal(0, new SkUiCoreLabel().CornerRadius);
    }

    [Fact]
    public void LabelAndButtonCornersAreSetSeparatelyOrAllAtOnce()
    {
        // A tab: rounded top corners only.
        var tab = new SkUiLabel { BackgroundColor = Colors.Red, CornerRadii = new CornerRadius(16, 16, 0, 0) };
        SkUiTestHelpers.Arrange(tab, 80, 40);
        using (var bitmap = new SKBitmap(80, 40))
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            tab.Paint(canvas);
            Assert.Equal(0, bitmap.GetPixel(0, 0).Alpha);
            Assert.Equal(0, bitmap.GetPixel(79, 0).Alpha);
            Assert.Equal(SKColors.Red, bitmap.GetPixel(0, 39));
            Assert.Equal(SKColors.Red, bitmap.GetPixel(79, 39));
        }

        // MAUI Button's CornerRadius sets all four corners; the last property set wins.
        var button = new SkUiButton { CornerRadius = 10 };
        Assert.Equal(new CornerRadius(10), button.CornerRadii);
        button.CornerRadii = new CornerRadius(1, 2, 3, 4);
        Assert.Equal(1, button.CornerRadius);
        button.CornerRadius = 5;
        Assert.Equal(new CornerRadius(5), button.CornerRadii);
        button.ClearValue(SkUiLabel.CornerRadiusProperty);
        Assert.Equal(new CornerRadius(SkUiLook.Current.DefaultButtonCornerRadius), button.CornerRadii);
        Assert.Throws<ArgumentOutOfRangeException>(() => button.SetCornerRadii(new CornerRadius(-1, 0, 0, 0)));

        var core = new SkUiCoreButton();
        Assert.Equal(new CornerRadius(SkUiLook.Current.DefaultButtonCornerRadius), core.CornerRadii);
        var names = new List<string?>();
        ((System.ComponentModel.INotifyPropertyChanged)core).PropertyChanged += (_, args) => names.Add(args.PropertyName);
        core.SetCornerRadii(new CornerRadius(8, 8, 0, 0));
        Assert.Equal(8, core.CornerRadius);
        Assert.Equal([nameof(SkUiCoreLabel.CornerRadii), nameof(SkUiCoreLabel.CornerRadius)], names);
        core.SetCornerRadius(3);
        Assert.Equal(new CornerRadius(3), core.CornerRadii);
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
