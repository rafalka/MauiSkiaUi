using System.ComponentModel;
using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// MAUI API parity of shipped controls (Phase P1 in ImplementationPlan.md): Switch <c>IsToggled</c>, button press events,
/// BoxView corner radius, Line points, Image <c>Aspect.Center</c> / http, ImageButton chrome, on both layers.
/// </summary>
[Collection(RuntimeXamlCollection.Name)]
public class ControlParityTests
{
    private static readonly Point Inside = new(10, 10);

    private static SKBitmap Render(Action<SKCanvas> paint, int width, int height)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        paint(canvas);
        return bitmap;
    }

    private static byte[] ImageBytes(SKColor color, int width = 40, int height = 20)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    [Fact]
    public void SwitchIsToggledIsTheTwoStateViewAndBindsBothWays()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var model = new ToggleModel();
        var toggle = new SkUiSwitch { BindingContext = model };
        toggle.SetBinding(SkUiSwitch.IsToggledProperty, nameof(ToggleModel.IsOn));
        var events = new List<string>();
        toggle.CheckedChanged += (_, args) => events.Add($"checked {args.Value}");
        toggle.Toggled += (_, args) => events.Add($"toggled {args.Value}: model {model.IsOn}");

        model.IsOn = true;
        Assert.Equal(SkUiCheckState.Checked, toggle.CheckState);
        Assert.True((bool)toggle.GetValue(SkUiToggleControl.IsCheckedProperty));

        _ = new SkUiContentView { Content = toggle };
        SkUiTestHelpers.Arrange(SkUiDiagnostics.GetSurfaceRoot(toggle)!, 100, 40);
        Assert.NotNull(SkUiDiagnostics.SimulateTap(toggle));
        Assert.False(model.IsOn);
        Assert.Equal(["checked True", "toggled True: model True", "checked False", "toggled False: model False"], events);

        toggle.CheckState = SkUiCheckState.Indeterminate;
        Assert.False(toggle.IsToggled);
        toggle.CheckState = SkUiCheckState.Checked;
        toggle.IsToggled = false;
        Assert.Equal(SkUiCheckState.Unchecked, toggle.CheckState);
        toggle.CheckState = SkUiCheckState.Indeterminate;
        toggle.IsToggled = false; // also clears Indeterminate
        Assert.Equal(SkUiCheckState.Unchecked, toggle.CheckState);
    }

    [Fact]
    public void CoreSwitchIsToggledNotifiesAndRaisesToggled()
    {
        var toggle = new SkUiCoreSwitch();
        var events = new List<string>();
        ((INotifyPropertyChanged)toggle).PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SkUiCoreSwitch.IsToggled)) events.Add("notify");
        };
        toggle.Toggled += (_, args) => events.Add($"toggled {args.Value}");
        toggle.IsToggled = true;
        Assert.True(toggle.IsChecked);
        toggle.CheckState = SkUiCheckState.Indeterminate;
        Assert.Equal(["notify", "toggled True", "notify", "toggled False"], events);
    }

    [Fact]
    public void ButtonsRaisePressedReleasedThenClickedAndReleaseOnCancel()
    {
        var button = new SkUiButton { Text = "" };
        var image = new SkUiImageButton();
        foreach (var (view, events) in new (SkUiView, List<string>)[] { (button, []), (image, []) })
        {
            switch (view)
            {
                case SkUiButton b:
                    b.Pressed += (_, _) => events.Add("pressed");
                    b.Released += (_, _) => events.Add("released");
                    b.Clicked += (_, _) => events.Add("clicked");
                    break;
                case SkUiImageButton i:
                    i.Pressed += (_, _) => events.Add("pressed");
                    i.Released += (_, _) => events.Add("released");
                    i.Clicked += (_, _) => events.Add("clicked");
                    break;
            }
            SkUiTestHelpers.Arrange(view, 100, 50);
            view.Touch(new(1, SkUiTouchAction.Pressed, Inside));
            view.Touch(new(1, SkUiTouchAction.Released, Inside));
            view.Touch(new(2, SkUiTouchAction.Pressed, Inside));
            view.Touch(new(2, SkUiTouchAction.Cancelled, Inside));
            Assert.Equal(["pressed", "released", "clicked", "pressed", "released"], events);
        }
    }

    [Fact]
    public void CoreButtonsRaisePressedReleasedThenClicked()
    {
        var button = new SkUiCoreButton();
        var image = new SkUiCoreImageButton();
        var buttonEvents = new List<string>();
        var imageEvents = new List<string>();
        button.Pressed += (_, _) => buttonEvents.Add("pressed");
        button.Released += (_, _) => buttonEvents.Add("released");
        button.Clicked += (_, _) => buttonEvents.Add("clicked");
        image.Pressed += (_, _) => imageEvents.Add("pressed");
        image.Released += (_, _) => imageEvents.Add("released");
        image.Clicked += (_, _) => imageEvents.Add("clicked");
        foreach (var node in new SkUiCoreNode[] { button, image })
        {
            node.Measure(100, 50);
            node.Arrange(new Rect(0, 0, 100, 50));
            node.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Pressed, Inside));
            node.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Released, Inside));
        }
        Assert.Equal(["pressed", "released", "clicked"], buttonEvents);
        Assert.Equal(["pressed", "released", "clicked"], imageEvents);
    }

    [Fact]
    public void BoxViewCornerRadiusRoundsTheFillOnBothLayers()
    {
        var box = new SkUiBox { Color = Colors.Red, CornerRadius = new CornerRadius(20, 20, 0, 0) };
        SkUiTestHelpers.Arrange(box, 40, 40);
        using (var bitmap = Render(box.Paint, 40, 40))
        {
            Assert.Equal(0, bitmap.GetPixel(1, 1).Alpha);
            Assert.Equal(SKColors.Red, bitmap.GetPixel(1, 38)); // square bottom corners
            Assert.Equal(SKColors.Red, bitmap.GetPixel(20, 20));
        }

        var core = new SkUiCoreBox().SetCornerRadius(20);
        core.SetColor(Colors.Red);
        core.Measure(40, 40);
        core.Arrange(new Rect(0, 0, 40, 40));
        using (var bitmap = Render(core.Paint, 40, 40))
        {
            Assert.Equal(0, bitmap.GetPixel(1, 1).Alpha);
            Assert.Equal(0, bitmap.GetPixel(1, 38).Alpha);
            Assert.Equal(SKColors.Red, bitmap.GetPixel(20, 20));
        }
    }

    [Fact]
    public void LineMeasuresAndDrawsItsPointsLikeMaui()
    {
        var line = new SkUiLine(10, 5, 50, 25) { StrokeThickness = 2 };
        Assert.Equal(new Size(52, 27), ((IView)line).Measure(double.PositiveInfinity, double.PositiveInfinity));
        // MAUI's default stroke is 1 DIP.
        Assert.Equal(new Size(1, 1), ((IView)new SkUiLine()).Measure(double.PositiveInfinity, double.PositiveInfinity));

        var horizontal = new SkUiLine(0, 10, 40, 10) { Stroke = Colors.Red, StrokeThickness = 4 };
        SkUiTestHelpers.Arrange(horizontal, 44, 20);
        using (var bitmap = Render(horizontal.Paint, 44, 20))
        {
            // Placed as MAUI places an unstretched shape: shifted right by half the stroke, so the left end is not cut.
            Assert.Equal(SKColors.Red, bitmap.GetPixel(3, 10));
            Assert.Equal(SKColors.Red, bitmap.GetPixel(41, 10));
            Assert.Equal(0, bitmap.GetPixel(20, 3).Alpha);
        }

        var core = new SkUiCoreLine(10, 5, 50, 25);
        core.SetStrokeThickness(2);
        Assert.Equal(new Size(52, 27), core.Measure(double.PositiveInfinity, double.PositiveInfinity));
        core.SetY2(45);
        Assert.Equal(new Size(52, 47), core.Measure(double.PositiveInfinity, double.PositiveInfinity));
    }

    [Fact]
    public async Task ImageAspectCenterDrawsAtSourceSize()
    {
        var image = new SkUiImage { Source = ImageSource.FromStream(() => new MemoryStream(ImageBytes(SKColors.Red))), Aspect = Aspect.Center };
        await image.LoadingTask;
        SkUiTestHelpers.Arrange(image, 100, 100);
        using var bitmap = Render(image.Paint, 100, 100);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(50, 50));
        Assert.Equal(SKColors.Red, bitmap.GetPixel(31, 41));
        Assert.Equal(0, bitmap.GetPixel(28, 50).Alpha); // 40×20 centered: x 30…70, y 40…60
        Assert.Equal(0, bitmap.GetPixel(50, 38).Alpha);
    }

    [Fact]
    public void ImageAspectCenterUsesTheSourceSizeOfADownsampledDecode()
    {
        using var decoded = SKImage.FromBitmap(SolidBitmap(SKColors.Blue, 20, 10)); // a 40×20 source decoded at half size
        using var bitmap = Render(canvas => SkUiImageDrawing.Draw(canvas, decoded, new Size(40, 20), new SKRect(0, 0, 100, 100), Aspect.Center), 100, 100);
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(31, 41));
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(68, 58));
        Assert.Equal(0, bitmap.GetPixel(28, 50).Alpha);
    }

    private static SKBitmap SolidBitmap(SKColor color, int width, int height)
    {
        var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);
        return bitmap;
    }

    [Fact]
    public async Task HttpImageSourcesAreLoadedNotRejected()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        // Nothing listens on the discard port: the load fails in the HTTP client, not as an unsupported source.
        var image = new SkUiImage { Source = ImageSource.FromUri(new Uri("http://127.0.0.1:9/image.png")) };
        await image.LoadingTask;
        Assert.IsNotType<NotSupportedException>(image.LoadError);
        var core = new SkUiCoreImage().SetSourceUri(new Uri("http://127.0.0.1:9/image.png"));
        await core.LoadingTask;
        Assert.IsNotType<NotSupportedException>(core.LoadError);
    }

    [Fact]
    public async Task ImageButtonPadsClipsAndBordersTheImageOnBothLayers()
    {
        var button = new SkUiImageButton
        {
            Source = ImageSource.FromStream(() => new MemoryStream(ImageBytes(SKColors.Red))),
            Aspect = Aspect.Fill,
            Padding = new Thickness(5),
            CornerRadius = 30,
            BorderColor = Colors.Blue,
            BorderWidth = 2,
        };
        await button.LoadingTask;
        Assert.Equal(new Size(50, 30), ((IView)button).Measure(double.PositiveInfinity, double.PositiveInfinity));
        SkUiTestHelpers.Arrange(button, 100, 60);
        using (var bitmap = Render(button.Paint, 100, 60))
            AssertImageButtonPixels(bitmap);

        var core = new SkUiCoreImageButton()
            .SetCornerRadius(30).SetBorderColor(Colors.Blue).SetBorderWidth(2).SetPadding(new Thickness(5));
        core.SetAspect(Aspect.Fill);
        core.SetSourceStream(_ => Task.FromResult<Stream>(new MemoryStream(ImageBytes(SKColors.Red))));
        await core.LoadingTask;
        Assert.Equal(new Size(50, 30), core.Measure(double.PositiveInfinity, double.PositiveInfinity));
        core.Arrange(new Rect(0, 0, 100, 60));
        using (var bitmap = Render(core.Paint, 100, 60))
            AssertImageButtonPixels(bitmap);
    }

    private static void AssertImageButtonPixels(SKBitmap bitmap)
    {
        Assert.Equal(SKColors.Red, bitmap.GetPixel(50, 30)); // the image
        Assert.Equal(0, bitmap.GetPixel(50, 3).Alpha); // padding
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(50, 0)); // border, inside the bounds
        Assert.Equal(0, bitmap.GetPixel(1, 1).Alpha); // rounded corner: neither image nor border
        Assert.Equal(0, bitmap.GetPixel(7, 7).Alpha); // inside the padding, but outside the 30-DIP corner arc: the image is clipped too
    }

    [Fact]
    public void RoundedChromeIsDrawnTheSameOnBothLayers()
    {
        // Labels, buttons and image buttons of both layers share one chrome state and its drawing (SkUiChromeState).
        var radii = new CornerRadius(14, 4, 0, 9);
        var label = new SkUiLabel { BackgroundColor = Colors.Yellow, CornerRadii = radii, BorderColor = Colors.Blue, BorderWidth = 3 };
        var coreLabel = new SkUiCoreLabel().SetFillColor(Colors.Yellow).SetCornerRadii(radii).SetBorderColor(Colors.Blue).SetBorderWidth(3);
        AssertSamePixels(label, coreLabel);

        // Buttons: the look's default radius until one is set, then the explicit radii.
        var button = new SkUiButton { FillColor = Colors.Green, BorderColor = Colors.Blue, BorderWidth = 2 };
        var coreButton = new SkUiCoreButton().SetFillColor(Colors.Green).SetBorderColor(Colors.Blue).SetBorderWidth(2);
        AssertSamePixels(button, coreButton);
        button.CornerRadii = radii;
        coreButton.SetCornerRadii(radii);
        AssertSamePixels(button, coreButton);

        var imageButton = new SkUiImageButton { CornerRadii = radii, BorderColor = Colors.Red, BorderWidth = 4, Padding = new Thickness(2) };
        var coreImageButton = new SkUiCoreImageButton().SetCornerRadii(radii).SetBorderColor(Colors.Red).SetBorderWidth(4).SetPadding(new Thickness(2));
        AssertSamePixels(imageButton, coreImageButton);

        static void AssertSamePixels(SkUiView view, SkUiCoreNode node)
        {
            SkUiTestHelpers.Arrange(view, 60, 34);
            node.Measure(60, 34);
            node.Arrange(new Rect(0, 0, 60, 34));
            using var expected = Render(view.Paint, 60, 34);
            using var actual = Render(node.Paint, 60, 34);
            Assert.NotEqual(0, expected.GetPixel(30, 0).Alpha); // something was drawn
            Assert.Equal(expected.Pixels, actual.Pixels);
        }
    }

    [Fact]
    public void MauiMarkupForTheNewPropertiesParses()
    {
        const string xaml = """
            <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <sk:SkUiVerticalStackLayout>
                <sk:SkUiSwitch IsToggled="True" />
                <sk:SkUiBox Color="Red" CornerRadius="10,10,0,0" />
                <sk:SkUiLine X1="0" Y1="0" X2="100" Y2="20" />
                <sk:SkUiImageButton CornerRadius="8" BorderColor="Black" BorderWidth="2" Padding="4" Aspect="Center" />
                <sk:SkUiRadioButton GroupName="plan" Value="Basic" />
              </sk:SkUiVerticalStackLayout>
            </ContentView>
            """;
        var root = new ContentView();
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var children = ((SkUiVerticalStackLayout)root.Content).Children;
        Assert.True(((SkUiSwitch)children[0]).IsToggled);
        Assert.Equal(new CornerRadius(10, 10, 0, 0), ((SkUiBox)children[1]).CornerRadius);
        Assert.Equal((100d, 20d), (((SkUiLine)children[2]).X2, ((SkUiLine)children[2]).Y2));
        var imageButton = (SkUiImageButton)children[3];
        Assert.Equal((8, 2d, new Thickness(4), Aspect.Center), (imageButton.CornerRadius, imageButton.BorderWidth, imageButton.Padding, imageButton.Aspect));
        Assert.Equal("Basic", ((SkUiRadioButton)children[4]).Value);
    }

    private sealed class ToggleModel : INotifyPropertyChanged
    {
        private bool _isOn;

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool IsOn
        {
            get => _isOn;
            set
            {
                if (_isOn == value) return;
                _isOn = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsOn)));
            }
        }
    }
}
