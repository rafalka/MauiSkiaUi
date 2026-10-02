using System.Diagnostics;
using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

public class TextAndImageTests
{
    private static SKFont Font()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "RobotoMono-Regular.ttf");
        return new SKFont(SKTypeface.FromFile(path), 16);
    }

    [Fact]
    public void WordWrapBreaksAtSpacesAndEveryLineFits()
    {
        using var font = Font();
        const string text = "The quick brown fox jumps over the lazy dog again and again";
        var lines = SkUiTestHelpers.BreakLines(text, 100, LineBreakMode.WordWrap, font.Typeface);
        Assert.True(lines.Count > 1);
        Assert.Equal(text.Replace(" ", ""), string.Concat(lines).Replace(" ", ""));
        foreach (var line in lines)
        {
            Assert.True(font.MeasureText(line) <= 100.5f, $"'{line}' overflows");
            Assert.False(line.StartsWith(' '));
        }
    }

    [Fact]
    public void CharacterWrapNeverSplitsGraphemes()
    {
        using var font = Font();
        const string text = "ab👍🏽cd👩‍👩‍👧éfghijk";
        var lines = SkUiTestHelpers.BreakLines(text, 20, LineBreakMode.CharacterWrap, font.Typeface);
        Assert.Equal(text, string.Concat(lines));
        foreach (var line in lines)
            Assert.False(char.IsLowSurrogate(line[0]) || line[0] == '́' || line[0] == '‍', $"line starts mid-grapheme: {line}");
    }

    [Fact]
    public void LongParagraphWrapsInLinearTime()
    {
        using var font = Font();
        var text = string.Join(' ', Enumerable.Repeat("lorem ipsum dolor sit amet", 2000));
        var watch = Stopwatch.StartNew();
        var lines = SkUiTestHelpers.BreakLines(text, 300, LineBreakMode.WordWrap, font.Typeface);
        watch.Stop();
        Assert.True(lines.Count > 100);
        // The previous per-grapheme re-measure was quadratic per line; this stays well under a frame budget per 50k chars.
        Assert.True(watch.ElapsedMilliseconds < 200, $"wrap took {watch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void TailTruncationFitsWidthAndEndsWithEllipsis()
    {
        using var font = Font();
        var lines = SkUiTestHelpers.BreakLines("A fairly long single line of text", 120, LineBreakMode.TailTruncation, font.Typeface);
        var line = Assert.Single(lines);
        Assert.EndsWith("...", line);
        Assert.True(font.MeasureText(line) <= 120);
    }

    [Fact]
    public void LabelColorChangeReRecordsWithoutRewrapping()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var label = new CountingBreakLabel { Text = "Hello wrapped world", FontFamily = SkUiTestHelpers.BundledFontFamily, WidthRequest = 60 };
        var root = new SkUiContentView { Content = label };
        using var surface = new SkUiTestSurface(root, 100, 100);
        surface.Frame();
        var breaks = label.Breaks;
        label.TextColor = Colors.Red;
        surface.Frame();
        label.TextColor = Colors.Blue;
        surface.Frame();
        Assert.Equal(breaks, label.Breaks);
    }

    [Fact]
    public async Task LargeImagesDecodeDownsampledButKeepSourceLayoutSize()
    {
        var previous = SkUiImageDecoder.MaxDecodeDimension;
        SkUiImageDecoder.MaxDecodeDimension = 256;
        try
        {
            using var bitmap = new SKBitmap(1200, 600);
            using (var canvas = new SKCanvas(bitmap)) canvas.Clear(SKColors.Orange);
            using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            var bytes = encoded.ToArray();
            var image = new SkUiCoreImage().SetSourceStream(_ => Task.FromResult<Stream>(new MemoryStream(bytes)));
            await image.LoadingTask;
            Assert.Equal(new Size(1200, 600), image.ImageSize);
            var decoded = SkUiImageDecoder.Decode(bytes);
            using var frame = Assert.Single(decoded.Frames);
            Assert.Equal(new Size(1200, 600), decoded.Size);
            Assert.True(frame.Width <= 256 && frame.Height <= 256);
        }
        finally
        {
            SkUiImageDecoder.MaxDecodeDimension = previous;
        }
    }

    private sealed class CountingBreakLabel : SkUiLabel
    {
        public int Breaks { get; private set; }

        public CountingBreakLabel() => MeasureInvalidated += (_, _) => { };

        protected override Size MeasureContent(double widthConstraint, double heightConstraint)
        {
            Breaks++;
            return base.MeasureContent(widthConstraint, heightConstraint);
        }
    }
}
