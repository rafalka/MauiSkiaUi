using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;
using ImagePosition = Microsoft.Maui.Controls.Button.ButtonContentLayout.ImagePosition;

namespace MauiSkiaUi.Tests;

/// <summary>
/// MAUI parity P9: Button <c>ImageSource</c> + <c>ContentLayout</c> (both layers), RadioButton <c>Content</c> (text or a drawn
/// view), its text properties and border chrome, and <c>ControlTemplate</c> with <see cref="SkUiContentPresenter"/>.
/// </summary>
[Collection(RuntimeXamlCollection.Name)]
public class ContentAndTemplateTests
{
    private const double Spacing = 10;

    private static SKBitmap Render(Action<SKCanvas> paint, int width, int height)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        paint(canvas);
        return bitmap;
    }

    private static byte[] ImageBytes(SKColor color, int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    private static ImageSource RedSquare(int size = 20) => ImageSource.FromStream(() => new MemoryStream(ImageBytes(SKColors.Red, size, size)));

    /// <summary>A transparent button (no fill, no minimum height, no padding) so the pixels show only the image and the text.</summary>
    private static async Task<SkUiButton> ImageButton(string text, ImagePosition position, ImageSource? image = null)
    {
        var button = new SkUiButton
        {
            Text = text, FontFamily = SkUiTestHelpers.BundledFontFamily, TextColor = Colors.Blue, FillColor = Colors.Transparent,
            Padding = new Thickness(0), MinimumHeightRequest = 0, ImageSource = image ?? RedSquare(),
            ContentLayout = new Button.ButtonContentLayout(position, Spacing),
        };
        await button.ImageLoadingTask;
        return button;
    }

    private static Size TextSize(string text)
    {
        var label = new SkUiLabel { Text = text, FontFamily = SkUiTestHelpers.BundledFontFamily };
        return ((IView)label).Measure(double.PositiveInfinity, double.PositiveInfinity);
    }

    [Fact]
    public async Task ButtonImageAndTextMeasureAsOneGroupPerPosition()
    {
        using var font = SkUiTestHelpers.UseBundledFont();
        var text = TextSize("Settings");
        foreach (var position in new[] { ImagePosition.Left, ImagePosition.Right })
        {
            var button = await ImageButton("Settings", position);
            Assert.Equal(new Size(20 + Spacing + text.Width, Math.Max(20, text.Height)), ((IView)button).Measure(double.PositiveInfinity, double.PositiveInfinity));
        }
        foreach (var position in new[] { ImagePosition.Top, ImagePosition.Bottom })
        {
            var button = await ImageButton("Settings", position);
            Assert.Equal(new Size(Math.Max(20, text.Width), 20 + Spacing + text.Height), ((IView)button).Measure(double.PositiveInfinity, double.PositiveInfinity));
        }
        // Padding surrounds the group; without text there is no spacing.
        var padded = await ImageButton("", ImagePosition.Left);
        padded.Padding = new Thickness(4, 6);
        Assert.Equal(new Size(28, 32), ((IView)padded).Measure(double.PositiveInfinity, double.PositiveInfinity));
        // Without an image the button measures as before.
        var plain = new SkUiButton { Text = "Settings", FontFamily = SkUiTestHelpers.BundledFontFamily, Padding = new Thickness(0), MinimumHeightRequest = 0 };
        Assert.Equal(text, ((IView)plain).Measure(double.PositiveInfinity, double.PositiveInfinity));
    }

    [Fact]
    public async Task ButtonPlacesTheImageBesideTheCenteredText()
    {
        using var font = SkUiTestHelpers.UseBundledFont();
        var text = TextSize("Go");
        var groupWidth = 20 + Spacing + text.Width;
        const int width = 160, height = 60;
        var left = (width - groupWidth) / 2;

        var button = await ImageButton("Go", ImagePosition.Left);
        SkUiTestHelpers.Arrange(button, width, height);
        using (var bitmap = Render(button.Paint, width, height))
        {
            Assert.Equal(SKColors.Red, bitmap.GetPixel((int)(left + 10), height / 2)); // the image starts the centered group
            Assert.Equal(0, bitmap.GetPixel((int)left - 2, height / 2).Alpha);
            Assert.True(HasText(bitmap, (int)(left + 20 + Spacing), (int)(left + groupWidth))); // the text after it
            Assert.False(HasText(bitmap, 0, (int)(left + 20)));
        }

        // Right: the text first, the image at the end of the group.
        button.ContentLayout = new Button.ButtonContentLayout(ImagePosition.Right, Spacing);
        SkUiTestHelpers.Arrange(button, width, height);
        using (var bitmap = Render(button.Paint, width, height))
            Assert.Equal(SKColors.Red, bitmap.GetPixel((int)(left + groupWidth - 10), height / 2));

        // Right-to-left: Left is the start side (the right).
        button.ContentLayout = new Button.ButtonContentLayout(ImagePosition.Left, Spacing);
        button.FlowDirection = FlowDirection.RightToLeft;
        SkUiTestHelpers.Arrange(button, width, height);
        using (var bitmap = Render(button.Paint, width, height))
        {
            Assert.Equal(SKColors.Red, bitmap.GetPixel((int)(width - left - 10), height / 2));
            Assert.False(HasText(bitmap, (int)(width - left - 20), width));
        }

        // Top: the image above the text, both centered.
        var top = await ImageButton("Go", ImagePosition.Top);
        var groupHeight = 20 + Spacing + text.Height;
        SkUiTestHelpers.Arrange(top, width, 100);
        using (var bitmap = Render(top.Paint, width, 100))
        {
            var imageTop = (100 - groupHeight) / 2;
            Assert.Equal(SKColors.Red, bitmap.GetPixel(width / 2, (int)(imageTop + 10)));
            Assert.Equal(0, bitmap.GetPixel(width / 2, (int)imageTop - 2).Alpha);
        }
    }

    /// <summary>Whether blue text (antialiased) is drawn between <paramref name="fromX"/> and <paramref name="toX"/>.</summary>
    private static bool HasText(SKBitmap bitmap, int fromX, int toX)
    {
        for (var x = Math.Max(0, fromX); x < Math.Min(bitmap.Width, toX); x++)
            for (var y = 0; y < bitmap.Height; y++)
                if (bitmap.GetPixel(x, y) is { Alpha: > 128, Blue: > 128, Red: < 100 })
                    return true;
        return false;
    }

    [Fact]
    public void ButtonsDoNotWrapByDefaultAsMaui()
    {
        Assert.Equal(new Button().LineBreakMode, new SkUiButton().LineBreakMode);
        Assert.Equal(LineBreakMode.NoWrap, new SkUiButton().LineBreakMode);
        Assert.Equal(LineBreakMode.NoWrap, new SkUiCoreButton().LineBreakMode);
        Assert.Equal(LineBreakMode.WordWrap, new SkUiLabel().LineBreakMode); // labels still wrap
        var button = new SkUiButton { LineBreakMode = LineBreakMode.WordWrap };
        button.ClearValue(SkUiLabel.LineBreakModeProperty);
        Assert.Equal(LineBreakMode.NoWrap, button.LineBreakMode);
    }

    [Fact]
    public async Task TextThatDoesNotFitStartsAfterTheImageAndStaysInItsSlot()
    {
        using var font = SkUiTestHelpers.UseBundledFont();
        var button = await ImageButton("A long button text", ImagePosition.Left);
        button.Padding = new Thickness(6, 0);
        const int width = 120, height = 40;
        SkUiTestHelpers.Arrange(button, width, height);
        Assert.Equal(height, button.Height); // one line: no wrap
        using var bitmap = Render(button.Paint, width, height);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(6 + 10, height / 2)); // the image at the start of the padding
        var textStart = 6 + 20 + (int)Spacing;
        Assert.False(HasText(bitmap, 0, textStart - 1)); // nothing over the image or the spacing…
        Assert.True(HasText(bitmap, textStart, textStart + 12)); // …the beginning of the text right after them
        Assert.False(HasText(bitmap, width - 5, width)); // and nothing in the right padding
    }

    [Fact]
    public void CenteredLineWiderThanTheLabelShowsItsBeginning()
    {
        using var font = SkUiTestHelpers.UseBundledFont();
        var label = new SkUiLabel
        {
            Text = "Beginning of a long line", FontFamily = SkUiTestHelpers.BundledFontFamily, TextColor = Colors.Blue,
            LineBreakMode = LineBreakMode.NoWrap, HorizontalTextAlignment = TextAlignment.Center,
        };
        SkUiTestHelpers.Arrange(label, 60, 30);
        using var centered = Render(label.Paint, 60, 30);
        label.HorizontalTextAlignment = TextAlignment.Start;
        using var start = Render(label.Paint, 60, 30);
        Assert.True(HasText(centered, 0, 60));
        Assert.Equal(start.Pixels, centered.Pixels); // placed at the start, not cut on both sides
    }

    [Fact]
    public async Task ButtonImageIsScaledDownToFitButNeverUp()
    {
        using var font = SkUiTestHelpers.UseBundledFont();
        var button = await ImageButton("", ImagePosition.Left, RedSquare(40));
        Assert.Equal(new Size(40, 40), ((IView)button).Measure(double.PositiveInfinity, double.PositiveInfinity));
        Assert.Equal(new Size(30, 30), ((IView)button).Measure(double.PositiveInfinity, 30));
        button.Padding = new Thickness(5);
        Assert.Equal(new Size(30, 30), ((IView)button).Measure(double.PositiveInfinity, 30)); // 20 DIPs of image inside the padding

        SkUiTestHelpers.Arrange(button, 100, 30);
        using var bitmap = Render(button.Paint, 100, 30);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(50, 15));
        Assert.Equal(0, bitmap.GetPixel(50, 3).Alpha); // the padding
        Assert.Equal(0, bitmap.GetPixel(38, 15).Alpha); // a 20-DIP square, centered
    }

    [Fact]
    public async Task TopAndBottomImagesLeaveRoomForTheText()
    {
        using var font = SkUiTestHelpers.UseBundledFont();
        var text = TextSize("Go");
        var button = await ImageButton("Go", ImagePosition.Top, RedSquare(40));
        // Height for the text, the spacing and a 20-DIP image: the image shrinks, the text stays.
        var height = text.Height + Spacing + 20;
        Assert.Equal(new Size(Math.Max(20, text.Width), height), ((IView)button).Measure(double.PositiveInfinity, height));
        SkUiTestHelpers.Arrange(button, 100, height);
        using var bitmap = Render(button.Paint, 100, (int)Math.Ceiling(height));
        Assert.Equal(SKColors.Red, bitmap.GetPixel(50, 10));
        Assert.Equal(0, bitmap.GetPixel(50 + 12, 10).Alpha); // 20 DIPs wide, not 40
        Assert.True(HasText(bitmap, 0, 100)); // the text is still drawn below
    }

    [Fact]
    public async Task DisposingAButtonReleasesItsImage()
    {
        using var font = SkUiTestHelpers.UseBundledFont();
        var button = await ImageButton("Go", ImagePosition.Left);
        var entry = button.CachedImage!;
        Assert.False(entry.IsReleased);
        button.Dispose();
        Assert.True(entry.IsReleased); // a stream image is not cached: the button's lease was the last reference
        Assert.Equal(TextSize("Go"), ((IView)button).Measure(double.PositiveInfinity, double.PositiveInfinity)); // a text button now
        button.ImageSource = RedSquare();
        await button.ImageLoadingTask;
        Assert.Null(button.CachedImage); // not loaded after disposal
        button.Dispose(); // twice: nothing

        var cleared = await ImageButton("Go", ImagePosition.Left);
        var clearedEntry = cleared.CachedImage!;
        cleared.ImageSource = null;
        Assert.True(clearedEntry.IsReleased);

        var core = new SkUiCoreButton();
        core.SetImageSource(SkUiImageSource.FromStream(_ => Task.FromResult<Stream>(new MemoryStream(ImageBytes(SKColors.Red, 20, 20)))));
        await core.ImageLoadingTask;
        var coreEntry = core.CachedImage!;
        core.Dispose();
        Assert.True(coreEntry.IsReleased);
    }

    [Fact]
    public async Task CoreButtonDrawsTheImageAsTheSkUiButton()
    {
        using var font = SkUiTestHelpers.UseBundledFont();
        foreach (var position in new[] { ImagePosition.Left, ImagePosition.Top, ImagePosition.Right, ImagePosition.Bottom })
        {
            var button = await ImageButton("Go", position);
            var core = new SkUiCoreButton().SetFillColor(Colors.Transparent);
            core.SetTextColor(Colors.Blue).SetFontFamily(SkUiTestHelpers.BundledFontFamily).SetText("Go").SetPadding(new Thickness(0));
            core.SetMinimumHeight(0);
            core.SetContentLayout(new Button.ButtonContentLayout(position, Spacing));
            core.SetImageSource(SkUiImageSource.FromStream(_ => Task.FromResult<Stream>(new MemoryStream(ImageBytes(SKColors.Red, 20, 20)))));
            await core.ImageLoadingTask;
            Assert.Equal(((IView)button).Measure(double.PositiveInfinity, double.PositiveInfinity), core.Measure(double.PositiveInfinity, double.PositiveInfinity));

            SkUiTestHelpers.Arrange(button, 120, 80);
            core.Measure(120, 80);
            core.Arrange(new Rect(0, 0, 120, 80));
            using var expected = Render(button.Paint, 120, 80);
            using var actual = Render(core.Paint, 120, 80);
            Assert.Equal(expected.Pixels, actual.Pixels);
        }
    }

    [Fact]
    public void MauiButtonImageMarkupLoadsWithOnlyThePrefixChanged()
    {
        // MAUI's ButtonPage gallery (Controls.Sample), prefix changed.
        const string xaml = """
            <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <sk:SkUiVerticalStackLayout>
                <sk:SkUiButton ContentLayout="Top" TextColor="White" Background="Black" ImageSource="settings.png" />
                <sk:SkUiButton ContentLayout="Bottom" ImageSource="settings.png" Text="settings" TextColor="White" Background="Black" />
                <sk:SkUiButton ContentLayout="Left" ImageSource="settings.png" Text="settings" />
                <sk:SkUiButton ContentLayout="Right, 20" ImageSource="coffee.png" Text="Lorem ipsum dolor sit amet" />
                <sk:SkUiButton ContentLayout="5" Text="spacing only" />
              </sk:SkUiVerticalStackLayout>
            </ContentView>
            """;
        var root = new ContentView();
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var buttons = ((SkUiVerticalStackLayout)root.Content).Children.Cast<SkUiButton>().ToArray();
        Assert.Equal([ImagePosition.Top, ImagePosition.Bottom, ImagePosition.Left, ImagePosition.Right, ImagePosition.Left],
            buttons.Select(button => button.ContentLayout.Position));
        Assert.Equal([10d, 10, 10, 20, 5], buttons.Select(button => button.ContentLayout.Spacing));
        Assert.Equal("settings.png", ((FileImageSource)buttons[0].ImageSource!).File);
        Assert.Equal(new Button().ContentLayout.ToString(), new SkUiButton().ContentLayout.ToString()); // MAUI's default
    }

    [Fact]
    public void RadioButtonDrawsStringContentBesideTheCircle()
    {
        using var font = SkUiTestHelpers.UseBundledFont();
        var circle = SkUiLook.Current.DefaultRadioButtonSize;
        var spacing = SkUiLook.Current.DefaultRadioButtonContentSpacing;
        var radio = new SkUiRadioButton { Content = "Cat", FontFamily = SkUiTestHelpers.BundledFontFamily, TextColor = Colors.Blue };
        var text = TextSize("Cat");
        Assert.Equal(new Size(circle.Width + spacing + text.Width, Math.Max(circle.Height, text.Height)),
            ((IView)radio).Measure(double.PositiveInfinity, double.PositiveInfinity));

        // The text transform and font size change the measure; other objects show as their ToString().
        radio.TextTransform = TextTransform.Uppercase;
        Assert.Equal(circle.Width + spacing + TextSize("CAT").Width, ((IView)radio).Measure(double.PositiveInfinity, double.PositiveInfinity).Width);
        radio.Content = 42;
        Assert.Equal(circle.Width + spacing + TextSize("42").Width, ((IView)radio).Measure(double.PositiveInfinity, double.PositiveInfinity).Width);
        radio.Content = null;
        Assert.Equal(circle, ((IView)radio).Measure(double.PositiveInfinity, double.PositiveInfinity));

        radio.Content = "Cat";
        radio.TextTransform = TextTransform.Default;
        SkUiTestHelpers.Arrange(radio, 120, 30);
        using (var bitmap = Render(radio.Paint, 120, 30))
        {
            Assert.NotEqual(0, bitmap.GetPixel(1, 15).Alpha); // the ring at the start
            Assert.True(HasText(bitmap, (int)(circle.Width + spacing), 120));
            Assert.False(HasText(bitmap, 0, (int)circle.Width));
        }
        // Right-to-left: the circle at the right, the text before it.
        radio.FlowDirection = FlowDirection.RightToLeft;
        SkUiTestHelpers.Arrange(radio, 120, 30);
        using (var bitmap = Render(radio.Paint, 120, 30))
        {
            Assert.NotEqual(0, bitmap.GetPixel(118, 15).Alpha);
            Assert.Equal(0, bitmap.GetPixel(1, 15).Alpha);
            Assert.True(HasText(bitmap, 0, (int)(120 - circle.Width - spacing)));
        }
    }

    [Fact]
    public void RadioButtonHostsAViewContentThatTapsSelect()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var circle = SkUiLook.Current.DefaultRadioButtonSize;
        var spacing = SkUiLook.Current.DefaultRadioButtonContentSpacing;
        var content = new SkUiBox { WidthRequest = 60, HeightRequest = 40, Color = Colors.Green };
        var radio = new SkUiRadioButton { Content = content, BindingContext = "model", HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
        Assert.Same(radio, content.Parent);
        Assert.Equal("model", content.BindingContext);
        Assert.Equal(new Size(circle.Width + spacing + 60, 40), ((IView)radio).Measure(double.PositiveInfinity, double.PositiveInfinity));

        var host = new SkUiContentView { Content = radio };
        SkUiTestHelpers.Arrange(host, 200, 100);
        Assert.Equal(new Rect(circle.Width + spacing, 0, 60, 40), content.Frame);
        Assert.Same(content, SkUiDiagnostics.SimulateTap(content)); // the content is hit…
        Assert.True(radio.IsChecked); // …and the tap selects the radio button

        // A view cannot be shown twice; replacing the content releases it.
        Assert.Throws<InvalidOperationException>(() => new SkUiRadioButton().SetContent(content));
        radio.Content = "Text";
        Assert.Null(content.Parent);
        Assert.Empty(((IVisualTreeElement)radio).GetVisualChildren());
    }

    [Fact]
    public void RadioButtonContentFollowsACircleArrangedSmallerThanMeasured()
    {
        var spacing = SkUiLook.Current.DefaultRadioButtonContentSpacing;
        var content = new SkUiBox { WidthRequest = 30, HorizontalOptions = LayoutOptions.Start };
        var radio = new SkUiRadioButton { Content = content };
        radio.Measure(200, 12);
        radio.Arrange(new Rect(0, 0, 200, 12)); // 12 DIPs high: a 12-DIP circle instead of the look's 24
        Assert.Equal(12 + spacing, content.Frame.X);
    }

    [Fact]
    public void RadioButtonBorderAndPaddingSurroundTheContent()
    {
        var circle = SkUiLook.Current.DefaultRadioButtonSize;
        var radio = new SkUiRadioButton { Padding = new Thickness(4), BorderWidth = 2, BorderColor = Colors.Blue, CornerRadius = 8, BackgroundColor = Colors.Yellow };
        var size = ((IView)radio).Measure(double.PositiveInfinity, double.PositiveInfinity);
        Assert.Equal(new Size(circle.Width + 12, circle.Height + 12), size);
        SkUiTestHelpers.Arrange(radio, size.Width, size.Height);
        using var bitmap = Render(radio.Paint, (int)size.Width, (int)size.Height);
        Assert.Equal(SKColors.Blue, bitmap.GetPixel((int)size.Width / 2, 0)); // the border
        Assert.Equal(SKColors.Yellow, bitmap.GetPixel((int)size.Width / 2, 3)); // the background inside it
        Assert.Equal(0, bitmap.GetPixel(0, 0).Alpha); // a rounded corner
    }

    private const string TemplateXaml = """
        <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                     xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                     xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
          <ContentView.Resources>
            <ControlTemplate x:Key="RadioButtonTemplate">
              <sk:SkUiBorder Stroke="#F3F2F1" StrokeThickness="2" StrokeShape="RoundRectangle 10" BackgroundColor="#F3F2F1"
                             HeightRequest="90" WidthRequest="90" HorizontalOptions="Start" VerticalOptions="Start">
                <VisualStateManager.VisualStateGroups>
                  <VisualStateGroupList>
                    <VisualStateGroup x:Name="CheckedStates">
                      <VisualState x:Name="Checked">
                        <VisualState.Setters>
                          <Setter Property="Stroke" Value="#FF3300" />
                          <Setter TargetName="check" Property="Opacity" Value="1" />
                        </VisualState.Setters>
                      </VisualState>
                      <VisualState x:Name="Unchecked">
                        <VisualState.Setters>
                          <Setter Property="BackgroundColor" Value="#F3F2F1" />
                          <Setter Property="Stroke" Value="#F3F2F1" />
                          <Setter TargetName="check" Property="Opacity" Value="0" />
                        </VisualState.Setters>
                      </VisualState>
                    </VisualStateGroup>
                  </VisualStateGroupList>
                </VisualStateManager.VisualStateGroups>
                <sk:SkUiGrid Margin="4" WidthRequest="90">
                  <sk:SkUiGrid Margin="0,0,4,0" WidthRequest="18" HeightRequest="18" HorizontalOptions="End" VerticalOptions="Start">
                    <sk:SkUiEllipse Stroke="Blue" Fill="White" WidthRequest="16" HeightRequest="16" HorizontalOptions="Center" VerticalOptions="Center" />
                    <sk:SkUiEllipse x:Name="check" Fill="Blue" WidthRequest="8" HeightRequest="8" HorizontalOptions="Center" VerticalOptions="Center" />
                  </sk:SkUiGrid>
                  <sk:SkUiContentPresenter />
                </sk:SkUiGrid>
              </sk:SkUiBorder>
            </ControlTemplate>
            <Style TargetType="sk:SkUiRadioButton">
              <Setter Property="ControlTemplate" Value="{StaticResource RadioButtonTemplate}" />
            </Style>
          </ContentView.Resources>
          <sk:SkUiHorizontalStackLayout RadioButtonGroup.GroupName="animals" RadioButtonGroup.SelectedValue="{Binding Value}">
            <sk:SkUiRadioButton Value="Cat" TextColor="Purple">
              <sk:SkUiRadioButton.Content>
                <sk:SkUiVerticalStackLayout>
                  <sk:SkUiBox Color="Orange" WidthRequest="20" HeightRequest="20" HorizontalOptions="Center" VerticalOptions="Center" />
                  <sk:SkUiLabel Text="{Binding Value, Source={RelativeSource AncestorType={x:Type sk:SkUiRadioButton}}}"
                                HorizontalOptions="Center" VerticalOptions="End" />
                </sk:SkUiVerticalStackLayout>
              </sk:SkUiRadioButton.Content>
            </sk:SkUiRadioButton>
            <sk:SkUiRadioButton Value="Dog" Content="Dog" TextColor="Purple" />
          </sk:SkUiHorizontalStackLayout>
        </ContentView>
        """;

    [Fact]
    public void MauiRadioButtonTemplateMarkupWorksWithDrawnViews()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var model = new Selection { Value = "Dog" };
        var root = new ContentView { BindingContext = model };
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, TemplateXaml);
        var radios = ((SkUiHorizontalStackLayout)root.Content).Children.Cast<SkUiRadioButton>().ToArray();
        var (cat, dog) = (radios[0], radios[1]);
        var catRoot = Assert.IsType<SkUiBorder>(cat.TemplateRoot);
        Assert.Same(cat, ((Element)catRoot).Parent);

        // The presenter shows the view content; bindings in it reach the radio button by ancestor type.
        var presenter = Descendants(catRoot).OfType<SkUiContentPresenter>().Single();
        var content = Assert.IsType<SkUiVerticalStackLayout>(presenter.Content);
        Assert.Same(cat.Content, content);
        Assert.Equal("Cat", ((SkUiLabel)content.Children[1]).Text);
        // String content: a label styled by the radio button's text properties.
        var dogLabel = Assert.IsType<SkUiLabel>(Descendants((SkUiView)dog.TemplateRoot!).OfType<SkUiContentPresenter>().Single().Content);
        Assert.Equal(("Dog", Colors.Purple), (dogLabel.Text, dogLabel.TextColor));
        dog.TextColor = Colors.Green;
        Assert.Equal(Colors.Green, dogLabel.TextColor);

        // Checked / Unchecked reach the template root's states.
        var check = (SkUiEllipse)((Element)catRoot).FindByName("check");
        Assert.Equal(0, check.Opacity);
        Assert.Equal(1, ((SkUiEllipse)((Element)dog.TemplateRoot!).FindByName("check")).Opacity);

        var stack = (SkUiView)root.Content;
        root.Content = null;
        var host = new SkUiContentView { Content = stack, BindingContext = model };
        SkUiTestHelpers.Arrange(host, 300, 120);
        Assert.Equal(new Rect(0, 0, 90, 90), catRoot.Frame); // the root's own size and alignment, inside the radio button
        Assert.NotNull(SkUiDiagnostics.SimulateTap(cat));
        Assert.True(cat.IsChecked);
        Assert.False(dog.IsChecked);
        Assert.Equal("Cat", model.Value);
        Assert.Equal(1, check.Opacity);
        Assert.Equal(Color.FromArgb("#FF3300"), ((SolidColorBrush)catRoot.Stroke!).Color);

        // Without the template, the content is shown beside the drawn circle again.
        cat.ControlTemplate = null;
        Assert.Null(cat.TemplateRoot);
        Assert.Null(presenter.Content);
        Assert.Same(cat, content.Parent);
        cat.ControlTemplate = (ControlTemplate)root.Resources["RadioButtonTemplate"];
        Assert.IsType<SkUiContentPresenter>(content.Parent);
    }

    [Fact]
    public void TemplatesMustCreateDrawnViews()
    {
        var radio = new SkUiRadioButton();
        Assert.Throws<InvalidOperationException>(() => radio.ControlTemplate = new ControlTemplate(() => new Label()));
    }

    [Fact]
    public void ContentChangesMoveBetweenPresenterAndText()
    {
        var radio = new SkUiRadioButton
        {
            ControlTemplate = new ControlTemplate(() => new SkUiVerticalStackLayout { Children = { new SkUiContentPresenter(), new SkUiContentPresenter() } }),
            Content = "Both",
        };
        var presenters = ((SkUiVerticalStackLayout)radio.TemplateRoot!).Children.Cast<SkUiContentPresenter>().ToArray();
        Assert.All(presenters, presenter => Assert.Equal("Both", ((SkUiLabel)presenter.Content!).Text)); // text in each one
        var view = new SkUiBox();
        radio.Content = view;
        Assert.Same(view, presenters[0].Content); // a view in the first one only
        Assert.Null(presenters[1].Content);
        radio.Content = null;
        Assert.All(presenters, presenter => Assert.Null(presenter.Content));
        Assert.Null(view.Parent);
    }

    private static IEnumerable<SkUiView> Descendants(SkUiView view)
    {
        yield return view;
        foreach (var child in view.SkiaChildren.OfType<SkUiView>())
            foreach (var descendant in Descendants(child))
                yield return descendant;
    }

    private sealed class Selection : System.ComponentModel.INotifyPropertyChanged
    {
        private object? _value;

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public object? Value
        {
            get => _value;
            set
            {
                if (Equals(_value, value)) return;
                _value = value;
                PropertyChanged?.Invoke(this, new(nameof(Value)));
            }
        }
    }
}
