using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// M2: MAUI image sources on the Core layer: <see cref="SkUiImageSource.FromMaui"/> (the conversion the SkUi* controls use),
/// <see cref="SkUiImageSource.FromString"/> (MAUI's <c>ImageSourceConverter</c> rule) and the implicit conversions that let
/// every Core image property take either.
/// </summary>
public class CoreMauiImageSourceTests
{
    [Fact]
    public void FromMauiMapsEverySupportedKind()
    {
        Func<CancellationToken, Task<Stream>> open = _ => Task.FromResult<Stream>(new MemoryStream());

        Assert.Equal(new SkUiFileImageSource("icon.png"), SkUiImageSource.FromMaui(ImageSource.FromFile("icon.png")));
        var uri = Assert.IsType<SkUiUriImageSource>(SkUiImageSource.FromMaui(new UriImageSource
        {
            Uri = new Uri("https://images.test/a.png"), CachingEnabled = false, CacheValidity = TimeSpan.FromDays(3)
        }));
        Assert.Equal((new Uri("https://images.test/a.png"), false, TimeSpan.FromDays(3)), (uri.Uri, uri.CachingEnabled, uri.CacheValidity));
        Assert.Same(open, Assert.IsType<SkUiStreamImageSource>(SkUiImageSource.FromMaui(new StreamImageSource { Stream = open })).Open);
        var font = Assert.IsType<SkUiFontImageSource>(SkUiImageSource.FromMaui(new FontImageSource
        {
            Glyph = "A", FontFamily = "Icons", Size = 20, Color = Colors.Red, FontAutoScalingEnabled = false
        }));
        Assert.Equal(("A", "Icons", 20d, Colors.Red, false), (font.Glyph, font.FontFamily, font.Size, font.Color, font.FontAutoScalingEnabled));
    }

    [Fact]
    public void FromMauiReturnsNullForNoSourceAndFailingSourcesForInvalidOnes()
    {
        Assert.Null(SkUiImageSource.FromMaui(null));
        Assert.Null(SkUiImageSource.FromMaui(new FileImageSource()));
        // An unsupported URI scheme fails when loaded (LoadError), as on the SkUi* controls; it does not throw here.
        Assert.IsType<SkUiFailedImageSource>(SkUiImageSource.FromMaui(new UriImageSource { Uri = new Uri("ftp://images.test/a.png") }));
    }

    [Fact]
    public void FromStringFollowsMauisImageSourceConverter()
    {
        Assert.Null(SkUiImageSource.FromString(null));
        Assert.Null(SkUiImageSource.FromString(" "));
        Assert.Equal(new SkUiFileImageSource("icon.png"), SkUiImageSource.FromString("icon.png"));
        Assert.Equal(SkUiImageSource.FromUri(new Uri("https://images.test/a.png")), SkUiImageSource.FromString("https://images.test/a.png"));
        Assert.Equal(SkUiImageSource.FromUri(new Uri("http://images.test/a.png")), SkUiImageSource.FromString("http://images.test/a.png"));
        var path = Path.Combine(Path.GetTempPath(), "photo.png");
        Assert.Equal(new SkUiFileImageSource(path), SkUiImageSource.FromString(path));
        Assert.Equal(new SkUiFileImageSource(path), SkUiImageSource.FromString(new Uri(path).AbsoluteUri));
    }

    [Fact]
    public void CoreImagePropertiesTakeMauiSourcesAndStrings()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var image = new SkUiCoreImage().SetSource(new FontImageSource { Glyph = "M", FontFamily = SkUiTestHelpers.BundledFontFamily, Size = 24 });
        Assert.IsType<SkUiFontImageSource>(image.Source);
        Assert.Null(image.LoadError);
        Assert.True(image.ImageSize.Height >= 24); // font glyphs render synchronously

        image.SetSource("photo.jpg").SetLoadingPlaceholder("loading.png").SetErrorPlaceholder(ImageSource.FromFile("error.png"));
        Assert.Equal(new SkUiFileImageSource("photo.jpg"), image.Source);
        Assert.Equal(new SkUiFileImageSource("loading.png"), image.LoadingPlaceholder);
        Assert.Equal(new SkUiFileImageSource("error.png"), image.ErrorPlaceholder);
        image.SetSource(null);
        Assert.Null(image.Source);

        var button = new SkUiCoreButton().SetImageSource("icon.png");
        Assert.Equal(new SkUiFileImageSource("icon.png"), button.ImageSource);
        ImageSource maui = "https://images.test/thumb.png";
        var slider = new SkUiCoreSlider().SetThumbImageSource(maui);
        Assert.IsType<SkUiUriImageSource>(slider.ThumbImageSource);
        var avatar = new SkUiCoreAvatar { ImageSource = "avatar.png" };
        Assert.Equal(new SkUiFileImageSource("avatar.png"), avatar.ImageSource);
    }
}
