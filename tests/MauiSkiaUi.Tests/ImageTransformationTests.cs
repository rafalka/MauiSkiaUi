using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>P4: the stock image transformations, on images directly (no loader or cache).</summary>
public class ImageTransformationTests
{
    /// <summary>A 40×20 image: red left half, blue right half.</summary>
    private static SKImage Halves()
    {
        using var bitmap = new SKBitmap(40, 20);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Blue);
            using var red = new SKPaint { Color = SKColors.Red };
            canvas.DrawRect(0, 0, 20, 20, red);
        }
        return SKImage.FromBitmap(bitmap);
    }

    private static SKBitmap Apply(ISkUiImageTransformation transformation, float pixelsPerDip = 1)
    {
        using var source = Halves();
        var result = transformation.Transform(source, pixelsPerDip);
        try
        {
            return SKBitmap.FromImage(result);
        }
        finally
        {
            if (!ReferenceEquals(result, source))
                result.Dispose();
        }
    }

    [Fact]
    public void CircleCropsTheCenterSquareAndDrawsTheBorderInside()
    {
        using var bitmap = Apply(new SkUiCircleTransformation(1, Colors.Lime), pixelsPerDip: 2);
        Assert.Equal((20, 20), (bitmap.Width, bitmap.Height));
        Assert.Equal(0, bitmap.GetPixel(1, 1).Alpha); // outside the circle
        Assert.Equal(SKColors.Red, bitmap.GetPixel(5, 10)); // the center square spans both halves
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(15, 10));
        Assert.True(bitmap.GetPixel(10, 0).Green > 200); // 1 DIP = 2 px border
        Assert.True(bitmap.GetPixel(10, 1).Green > 200);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(9, 3));
    }

    [Fact]
    public void RoundedRoundsEachCornerAndCropsToTheAspectRatio()
    {
        using var rounded = Apply(new SkUiRoundedTransformation { CornerRadius = new CornerRadius(8, 0, 0, 8) });
        Assert.Equal((40, 20), (rounded.Width, rounded.Height));
        Assert.Equal(0, rounded.GetPixel(0, 0).Alpha); // rounded top-left
        Assert.Equal(SKColors.Blue, rounded.GetPixel(39, 0)); // square top-right
        using var square = Apply(new SkUiRoundedTransformation(4) { AspectRatio = 1 });
        Assert.Equal((20, 20), (square.Width, square.Height));
    }

    [Fact]
    public void CropMovesWithTheOffsetAndZooms()
    {
        using var left = Apply(new SkUiCropTransformation { AspectRatio = 1, OffsetX = -1 });
        Assert.Equal((20, 20), (left.Width, left.Height));
        Assert.Equal(SKColors.Red, left.GetPixel(19, 10));
        using var right = Apply(new SkUiCropTransformation { AspectRatio = 1, OffsetX = 1 });
        Assert.Equal(SKColors.Blue, right.GetPixel(0, 10));
        using var zoomed = Apply(new SkUiCropTransformation { ZoomFactor = 2 });
        Assert.Equal((20, 10), (zoomed.Width, zoomed.Height));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkUiCropTransformation { ZoomFactor = 0.5 });
    }

    [Fact]
    public void FlipAndRotateMoveThePixels()
    {
        using var flipped = Apply(new SkUiFlipTransformation());
        Assert.Equal(SKColors.Blue, flipped.GetPixel(5, 10));
        using var rotated = Apply(new SkUiRotateTransformation(90));
        Assert.Equal((20, 40), (rotated.Width, rotated.Height));
        Assert.Equal(SKColors.Red, rotated.GetPixel(10, 5)); // clockwise: the left half goes on top
        Assert.Equal(SKColors.Blue, rotated.GetPixel(10, 35));
        using var back = Apply(new SkUiRotateTransformation(-270));
        Assert.Equal(SKColors.Red, back.GetPixel(10, 5));
    }

    [Fact]
    public void ColorTransformationsRecolor()
    {
        using var gray = Apply(new SkUiGrayscaleTransformation());
        var pixel = gray.GetPixel(5, 10);
        Assert.True(pixel.Red == pixel.Green && pixel.Green == pixel.Blue && pixel.Red is > 40 and < 80);
        using var sepia = Apply(new SkUiSepiaTransformation());
        Assert.True(sepia.GetPixel(30, 10) is { Red: > 0 } tone && tone.Red >= tone.Green && tone.Green >= tone.Blue);
        using var tinted = Apply(new SkUiTintTransformation(Colors.Lime));
        Assert.Equal(SKColors.Lime, tinted.GetPixel(5, 10));
        Assert.Equal(SKColors.Lime, tinted.GetPixel(30, 10));
        using var inverted = Apply(new SkUiColorMatrixTransformation([-1, 0, 0, 0, 1, 0, -1, 0, 0, 1, 0, 0, -1, 0, 1, 0, 0, 0, 1, 0]));
        Assert.Equal(new SKColor(0, 255, 255), inverted.GetPixel(5, 10));
    }

    [Fact]
    public void BlurMixesAcrossEdgesButKeepsTheBorderOpaque()
    {
        using var blurred = Apply(new SkUiBlurTransformation(3));
        Assert.Equal(255, blurred.GetPixel(0, 0).Alpha); // clamped edges do not fade
        var middle = blurred.GetPixel(20, 10);
        Assert.True(middle.Red > 60 && middle.Blue > 60);
        Assert.Equal(SKColors.Red, Apply(new SkUiBlurTransformation(0)).GetPixel(20 - 1, 10));
    }

    [Fact]
    public void KeysCoverEverySettingAndUseTheInvariantCulture()
    {
        var culture = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("pl-PL");
        try
        {
            Assert.Equal("Circle;1.5;#FFFF0000", new SkUiCircleTransformation(1.5, Colors.Red).Key);
            Assert.NotEqual(new SkUiRoundedTransformation(4).Key, new SkUiRoundedTransformation(4) { BorderWidth = 1 }.Key);
            Assert.NotEqual(new SkUiTintTransformation(Colors.Red).Key, new SkUiTintTransformation(Colors.Red) { BlendMode = SKBlendMode.Multiply }.Key);
            Assert.DoesNotContain(",5", new SkUiBlurTransformation(2.5).Key);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = culture;
        }
    }
}
