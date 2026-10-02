using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>P4 parity: MAUI image markup with only the prefix changed, plus the SkiaUi image extensions.</summary>
[Collection(RuntimeXamlCollection.Name)]
public class ImageSourceParityTests
{
    [Fact]
    public void MauiImageMarkupLoadsWithOnlyThePrefixChanged()
    {
        using var font = SkUiTestHelpers.UseBundledFont();
        const string xaml = $$"""
            <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <sk:SkUiVerticalStackLayout>
                <sk:SkUiImage Source="dotnet_bot.png" Aspect="AspectFill" IsAnimationPlaying="True" />
                <sk:SkUiImage>
                  <sk:SkUiImage.Source>
                    <FontImageSource Glyph="M" FontFamily="{{SkUiTestHelpers.BundledFontFamily}}" Size="24" Color="Red" />
                  </sk:SkUiImage.Source>
                </sk:SkUiImage>
                <sk:SkUiImage>
                  <sk:SkUiImage.Source>
                    <UriImageSource Uri="https://images.test/a.png" CachingEnabled="False" CacheValidity="10:00:00:00" />
                  </sk:SkUiImage.Source>
                </sk:SkUiImage>
                <sk:SkUiImage Source="avatar.png" DownsampleWidth="64" CacheType="Memory">
                  <sk:SkUiImage.Transformations>
                    <sk:SkUiCircleTransformation BorderWidth="2" BorderColor="White" />
                    <sk:SkUiGrayscaleTransformation />
                  </sk:SkUiImage.Transformations>
                </sk:SkUiImage>
                <sk:SkUiImageButton>
                  <sk:SkUiImageButton.Source>
                    <FontImageSource Glyph="M" FontFamily="{{SkUiTestHelpers.BundledFontFamily}}" />
                  </sk:SkUiImageButton.Source>
                </sk:SkUiImageButton>
                <sk:SkUiSlider ThumbImageSource="thumb.png" />
                <sk:SkUiImage Source="photo.jpg" LoadingPlaceholder="loading.png" ErrorPlaceholder="error.png" TransformPlaceholders="False" />
              </sk:SkUiVerticalStackLayout>
            </ContentView>
            """;
        var root = new ContentView();
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var children = ((SkUiVerticalStackLayout)root.Content).Children;

        var file = (SkUiImage)children[0];
        Assert.Equal("dotnet_bot.png", ((FileImageSource)file.Source!).File);
        Assert.True(file.IsAnimationPlaying);

        var glyph = (SkUiImage)children[1];
        Assert.Null(glyph.LoadError);
        Assert.True(glyph.ImageSize.Height >= 24); // rendered synchronously while the markup loaded

        var uri = (UriImageSource)((SkUiImage)children[2]).Source!;
        Assert.Equal((false, TimeSpan.FromDays(10)), (uri.CachingEnabled, uri.CacheValidity));

        var avatar = (SkUiImage)children[3];
        Assert.Equal((64d, SkUiImageCacheType.Memory), (avatar.DownsampleWidth, avatar.CacheType));
        Assert.Collection(avatar.Transformations,
            item => Assert.Equal(2, Assert.IsType<SkUiCircleTransformation>(item).BorderWidth),
            item => Assert.IsType<SkUiGrayscaleTransformation>(item));

        var button = (SkUiImageButton)children[4];
        Assert.Equal(new Size(18, 40), new Size(Math.Round(button.ImageSize.Width), Math.Round(button.ImageSize.Height)), new SizeTolerance(4));

        Assert.Equal("thumb.png", ((FileImageSource)((SkUiSlider)children[5]).ThumbImageSource!).File);
        var placeholders = (SkUiImage)children[6];
        Assert.Equal(("loading.png", "error.png", false), (((FileImageSource)placeholders.LoadingPlaceholder!).File,
            ((FileImageSource)placeholders.ErrorPlaceholder!).File, placeholders.TransformPlaceholders));
        foreach (var child in children.OfType<SkUiImage>())
            child.Dispose();
    }

    [Fact]
    public void EachImageHasItsOwnTransformationList()
    {
        using var a = new SkUiImage();
        using var b = new SkUiImage();
        a.Transformations.Add(new SkUiCircleTransformation());
        Assert.Empty(b.Transformations);
    }

    private sealed class SizeTolerance(double tolerance) : IEqualityComparer<Size>
    {
        public bool Equals(Size x, Size y) => Math.Abs(x.Width - y.Width) <= tolerance && Math.Abs(x.Height - y.Height) <= tolerance;
        public int GetHashCode(Size obj) => 0;
    }
}
