using System.Net;
using System.Net.Http.Headers;
using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// P4: the shared image loader and caches (memory, disk, shared in-flight loads), downsampling, EXIF orientation,
/// animated GIFs, font glyph images and the slider thumb image, on both layers. Runs alone: it changes and measures
/// the process-wide caches.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class ImageLoadingTests : IDisposable
{
    private readonly long _memoryBudget = SkUiImageCache.MemoryCacheMaxBytes;
    private readonly HttpClient _httpClient = SkUiImageCache.HttpClient;
    private readonly string _diskDirectory = SkUiImageCache.DiskCacheDirectory;
    private readonly string _testDirectory = Path.Combine(Path.GetTempPath(), "skui-images-" + Guid.NewGuid().ToString("N"));

    public ImageLoadingTests()
    {
        SkUiImageCache.MemoryCacheMaxBytes = 64L * 1024 * 1024;
        SkUiImageCache.ClearMemoryCache();
        SkUiImageCache.DiskCacheDirectory = Path.Combine(_testDirectory, "cache");
    }

    public void Dispose()
    {
        SkUiImageCache.ClearMemoryCache();
        SkUiImageCache.MemoryCacheMaxBytes = _memoryBudget;
        SkUiImageCache.HttpClient = _httpClient;
        SkUiImageCache.DiskCacheDirectory = _diskDirectory;
        SkUiImageCache.CacheKeyFactory = null;
        try { Directory.Delete(_testDirectory, recursive: true); }
        catch (IOException) { }
    }

    internal static byte[] Png(SKColor color, int width = 40, int height = 20)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static string Key() => Guid.NewGuid().ToString("N");

    private static Func<CancellationToken, Task<Stream>> Open(byte[] bytes, Action? opened = null) => _ =>
    {
        opened?.Invoke();
        return Task.FromResult<Stream>(new MemoryStream(bytes));
    };

    [Fact]
    public async Task ASecondViewOfTheSameSourceDecodesNothingAndShowsAtOnce()
    {
        var path = Path.Combine(_testDirectory, "red.png");
        Directory.CreateDirectory(_testDirectory);
        await File.WriteAllBytesAsync(path, Png(SKColors.Red));
        var loads = SkUiImageLoader.LoadCount;

        using var first = new SkUiImage { Source = ImageSource.FromFile(path) };
        await first.LoadingTask;
        Assert.Equal(new Size(40, 20), first.ImageSize);
        Assert.Equal(loads + 1, SkUiImageLoader.LoadCount);

        using var second = new SkUiImage { Source = ImageSource.FromFile(path) };
        Assert.True(second.LoadingTask.IsCompleted); // a memory hit applies synchronously: no empty frame
        Assert.False(second.IsLoading);
        Assert.Equal(new Size(40, 20), second.ImageSize);
        Assert.Same(first.CachedImage, second.CachedImage);

        using var core = new SkUiCoreImage().SetSourceFile(path); // Core shares the cache
        Assert.Same(first.CachedImage, core.CachedImage);
        Assert.Equal(loads + 1, SkUiImageLoader.LoadCount);
    }

    [Fact]
    public async Task ConcurrentLoadsOfOneKeyShareOneDecodeAndSurviveOneWaiterCancelling()
    {
        var gate = new TaskCompletionSource();
        var opens = 0;
        var bytes = Png(SKColors.Green);
        var source = SkUiImageSource.FromStream(async token =>
        {
            Interlocked.Increment(ref opens);
            await gate.Task.WaitAsync(token);
            return new MemoryStream(bytes);
        }, Key());

        using var leaving = new SkUiCoreImage().SetSource(source);
        using var staying = new SkUiCoreImage().SetSource(source);
        Assert.True(leaving.IsLoading && staying.IsLoading);
        leaving.Clear(); // one waiter leaves: the shared load goes on for the other
        gate.SetResult();
        await staying.LoadingTask;

        Assert.Equal(1, opens);
        Assert.Equal(new Size(40, 20), staying.ImageSize);
        Assert.Null(leaving.CachedImage);
        Assert.Null(leaving.LoadError);
    }

    [Fact]
    public async Task TheSharedLoadIsCancelledWhenEveryWaiterLeaves()
    {
        var token = new TaskCompletionSource<CancellationToken>();
        var source = SkUiImageSource.FromStream(async cancellation =>
        {
            token.SetResult(cancellation);
            await Task.Delay(Timeout.Infinite, cancellation);
            throw new InvalidOperationException("not reached");
        }, Key());

        using var image = new SkUiCoreImage().SetSource(source);
        var loadToken = await token.Task;
        image.Clear();
        await WaitUntil(() => loadToken.IsCancellationRequested); // the last waiter leaving cancels the shared work
        Assert.False(image.IsLoading);
        Assert.Null(image.LoadError);
    }

    [Fact]
    public async Task EntriesLiveWhileShownAndAreDisposedAfterEvictionAndRelease()
    {
        using var shown = new SkUiCoreImage().SetSourceStream(Open(Png(SKColors.Red)), Key());
        await shown.LoadingTask;
        var entry = shown.CachedImage!;
        Assert.Equal(1, SkUiImageCache.MemoryCacheCount);
        Assert.Equal(40 * 20 * 4, SkUiImageCache.MemoryCacheBytes);

        SkUiImageCache.ClearMemoryCache();
        Assert.Equal(0, SkUiImageCache.MemoryCacheCount);
        Assert.False(entry.IsReleased); // the view still shows it
        shown.Dispose();
        Assert.True(entry.IsReleased);

        var cached = new SkUiCoreImage().SetSourceStream(Open(Png(SKColors.Blue)), Key());
        await cached.LoadingTask;
        var kept = cached.CachedImage!;
        cached.Dispose();
        Assert.False(kept.IsReleased); // the cache keeps it for the next view
        SkUiImageCache.ClearMemoryCache();
        Assert.True(kept.IsReleased);
    }

    [Fact]
    public async Task TheMemoryBudgetEvictsTheLeastRecentlyUsed()
    {
        SkUiImageCache.MemoryCacheMaxBytes = 40 * 20 * 4 * 2; // two images
        var keys = new[] { Key(), Key(), Key() };
        var images = new List<SkUiCoreImage>();
        foreach (var key in keys)
        {
            var image = new SkUiCoreImage().SetSourceStream(Open(Png(SKColors.Red)), key);
            await image.LoadingTask;
            images.Add(image);
            if (key == keys[1])
                new SkUiCoreImage().SetSourceStream(Open(Png(SKColors.Red)), keys[0]).Dispose(); // touch the first
        }
        Assert.Equal(2, SkUiImageCache.MemoryCacheCount);
        var loads = SkUiImageLoader.LoadCount;
        using var first = new SkUiCoreImage().SetSourceStream(Open(Png(SKColors.Red)), keys[0]);
        Assert.Equal(loads, SkUiImageLoader.LoadCount); // recently used: still cached
        using var second = new SkUiCoreImage().SetSourceStream(Open(Png(SKColors.Red)), keys[1]);
        await second.LoadingTask;
        Assert.Equal(loads + 1, SkUiImageLoader.LoadCount); // the least recently used went
        images.ForEach(image => image.Dispose());
    }

    [Fact]
    public async Task StreamsWithoutAKeyAndCacheTypeNoneAreNotCached()
    {
        var opens = 0;
        var bytes = Png(SKColors.Red);
        using var a = new SkUiCoreImage().SetSourceStream(Open(bytes, () => opens++));
        using var b = new SkUiCoreImage().SetSourceStream(Open(bytes, () => opens++));
        await Task.WhenAll(a.LoadingTask, b.LoadingTask);
        Assert.Equal(2, opens);
        Assert.NotSame(a.CachedImage, b.CachedImage);

        var key = Key();
        using var c = new SkUiCoreImage().SetCacheType(SkUiImageCacheType.None).SetSourceStream(Open(bytes, () => opens++), key);
        using var d = new SkUiCoreImage().SetCacheType(SkUiImageCacheType.Disk).SetSourceStream(Open(bytes, () => opens++), key);
        await Task.WhenAll(c.LoadingTask, d.LoadingTask);
        Assert.Equal(4, opens);
        Assert.Equal(0, SkUiImageCache.MemoryCacheCount);
    }

    [Fact]
    public async Task DownsamplingDecodesSmallerKeepsTheLayoutSizeAndIsCachedSeparately()
    {
        var key = Key();
        var bytes = Png(SKColors.Red, 400, 200);
        using var full = new SkUiCoreImage().SetSourceStream(Open(bytes), key);
        using var thumb = new SkUiCoreImage().SetDownsample(100, 0).SetSourceStream(Open(bytes), key);
        await Task.WhenAll(full.LoadingTask, thumb.LoadingTask);
        Assert.Equal(400, full.CachedImage!.Frames[0].Width);
        Assert.Equal((100, 50), (thumb.CachedImage!.Frames[0].Width, thumb.CachedImage.Frames[0].Height));
        Assert.Equal(new Size(400, 200), thumb.ImageSize);
        Assert.Equal(2, SkUiImageCache.MemoryCacheCount);

        using var bounded = new SkUiImage { DownsampleHeight = 20, Source = ImageSource.FromStream(() => new MemoryStream(bytes)) };
        await bounded.LoadingTask;
        Assert.Equal((40, 20), (bounded.CachedImage!.Frames[0].Width, bounded.CachedImage.Frames[0].Height));
        Assert.Equal(new Size(400, 200), bounded.ImageSize);
    }

    [Fact]
    public async Task TransformationsAreCachedPerChainAndCanChangeTheIntrinsicSize()
    {
        var key = Key();
        var bytes = Png(SKColors.Red);
        using var plain = new SkUiCoreImage().SetSourceStream(Open(bytes), key);
        using var circle = new SkUiCoreImage().SetTransformations(new SkUiCircleTransformation(2, Colors.Blue)).SetSourceStream(Open(bytes), key);
        await Task.WhenAll(plain.LoadingTask, circle.LoadingTask);
        Assert.Equal(new Size(40, 20), plain.ImageSize);
        Assert.Equal(new Size(20, 20), circle.ImageSize); // cropped to the centered square
        Assert.Equal(2, SkUiImageCache.MemoryCacheCount);

        using var bitmap = SKBitmap.FromImage(circle.CachedImage!.Frames[0]);
        Assert.Equal(0, bitmap.GetPixel(0, 0).Alpha);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(10, 10));
        Assert.True(bitmap.GetPixel(10, 0).Blue > 200); // border inside the circle

        // The SkUi* list: adding an item reloads with it.
        using var image = new SkUiImage { Source = ImageSource.FromStream(() => new MemoryStream(bytes)) };
        await image.LoadingTask;
        image.Transformations.Add(new SkUiRotateTransformation(90));
        await image.LoadingTask;
        Assert.Equal(new Size(20, 40), image.ImageSize);
        image.Transformations.Clear();
        await image.LoadingTask;
        Assert.Equal(new Size(40, 20), image.ImageSize);
    }

    [Fact]
    public async Task ExifOrientationIsAppliedToPixelsAndSize()
    {
        using var bitmap = new SKBitmap(40, 20);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Blue);
            canvas.DrawRect(0, 0, 20, 20, new SKPaint { Color = SKColors.Red });
        }
        using var image = new SkUiCoreImage().SetSourceStream(Open(JpegWithOrientation(bitmap, orientation: 6)));
        await image.LoadingTask;
        Assert.Null(image.LoadError);
        Assert.Equal(new Size(20, 40), image.ImageSize); // turned a quarter clockwise: the left (red) half is on top
        using var upright = SKBitmap.FromImage(image.CachedImage!.Frames[0]);
        Assert.True(upright.GetPixel(10, 5).Red > 200 && upright.GetPixel(10, 5).Blue < 60);
        Assert.True(upright.GetPixel(10, 35).Blue > 200 && upright.GetPixel(10, 35).Red < 60);
    }

    /// <summary>A JPEG of <paramref name="bitmap"/> with an EXIF APP1 segment holding only the orientation tag.</summary>
    private static byte[] JpegWithOrientation(SKBitmap bitmap, ushort orientation)
    {
        using var data = bitmap.Encode(SKEncodedImageFormat.Jpeg, 95);
        var jpeg = data.ToArray();
        byte[] app1 =
        [
            0xFF, 0xE1, 0x00, 0x22, // APP1, length 34
            (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0,
            (byte)'M', (byte)'M', 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08, // big-endian TIFF, IFD at 8
            0x00, 0x01, // one entry
            0x01, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01, (byte)(orientation >> 8), (byte)orientation, 0x00, 0x00, // Orientation, SHORT
            0x00, 0x00, 0x00, 0x00 // no next IFD
        ];
        return [.. jpeg[..2], .. app1, .. jpeg[2..]];
    }

    [Fact]
    public async Task AnimatedGifsPlayOnTheUiClockWhenAnimationPlays()
    {
        var gif = TwoFrameGif();
        var decoded = SkUiImageDecoder.Decode(gif);
        Assert.Equal(2, decoded.Frames.Length);
        Assert.Equal([100, 100], decoded.Durations);
        foreach (var frame in decoded.Frames) frame.Dispose();

        using var image = new SkUiImage { Aspect = Aspect.Fill, Source = ImageSource.FromStream(() => new MemoryStream(gif)) };
        await image.LoadingTask;
        using var surface = new SkUiTestSurface(image, 10, 10);
        Assert.Equal(SKColors.Red, surface.Frame().GetPixel(5, 5)); // not playing: the first frame
        image.AnimationClock.Tick(TimeSpan.FromMilliseconds(150));
        Assert.Equal(SKColors.Red, surface.Frame(150).GetPixel(5, 5));

        image.IsAnimationPlaying = true;
        surface.Frame(160); // the paint starts playback on the view's clock
        image.AnimationClock.Tick(TimeSpan.FromMilliseconds(270));
        Assert.Equal(SKColors.Blue, surface.Frame(270).GetPixel(5, 5));
        image.AnimationClock.Tick(TimeSpan.FromMilliseconds(370));
        Assert.Equal(SKColors.Red, surface.Frame(370).GetPixel(5, 5)); // loops

        image.IsAnimationPlaying = false;
        image.AnimationClock.Tick(TimeSpan.FromMilliseconds(470));
        Assert.Equal(SKColors.Red, surface.Frame(470).GetPixel(5, 5)); // paused on the current frame
        Assert.False(image.AnimationClock.IsRunning);
    }

    [Fact]
    public void TheDemoSpinnerGifDecodesToAllItsFrames()
    {
        var decoded = SkUiImageDecoder.Decode(MauiSkiaUiDemo.DemoGif.Spinner(48));
        Assert.Equal(8, decoded.Frames.Length);
        Assert.All(decoded.Durations, duration => Assert.Equal(100, duration));
        using var first = SKBitmap.FromImage(decoded.Frames[0]);
        Assert.Equal(SKColors.White, first.GetPixel(0, 0));
        Assert.Equal(new SKColor(0x25, 0x63, 0xEB), first.GetPixel(24, 24 - (int)(48 * 0.34f))); // the top dot is lit first
        foreach (var frame in decoded.Frames) frame.Dispose();
    }

    /// <summary>A looping 1×1 GIF: a red frame, then a blue one, 100 ms each.</summary>
    internal static byte[] TwoFrameGif()
    {
        static byte[] Frame(byte lzw) =>
        [
            0x21, 0xF9, 0x04, 0x00, 0x0A, 0x00, 0x00, 0x00, // graphic control: no disposal, 10 cs delay
            0x2C, 0, 0, 0, 0, 0x01, 0x00, 0x01, 0x00, 0x00, // image descriptor 1×1, no local table
            0x02, 0x02, lzw, 0x01, 0x00 // LZW min code size 2: clear, index, end
        ];
        return
        [
            .. "GIF89a"u8.ToArray(),
            0x01, 0x00, 0x01, 0x00, 0x80, 0x00, 0x00, // 1×1, global table of 2 colors
            0xFF, 0x00, 0x00, 0x00, 0x00, 0xFF, // red, blue
            0x21, 0xFF, 0x0B, .. "NETSCAPE2.0"u8.ToArray(), 0x03, 0x01, 0x00, 0x00, 0x00, // loop forever
            .. Frame(0x44), // index 0
            .. Frame(0x4C), // index 1
            0x3B
        ];
    }

    [Fact]
    public void FontGlyphImagesRenderSynchronouslyThroughTheFontPipeline()
    {
        using var font = SkUiTestHelpers.UseBundledFont();
        using var image = new SkUiImage
        {
            Source = new FontImageSource { Glyph = "M", FontFamily = SkUiTestHelpers.BundledFontFamily, Size = 20, Color = Colors.Red }
        };
        Assert.True(image.LoadingTask.IsCompleted);
        Assert.Null(image.LoadError);
        Assert.InRange(image.ImageSize.Height, 20, 32); // the font's line height
        Assert.InRange(image.ImageSize.Width, 8, 16); // one monospaced advance
        using var bitmap = SKBitmap.FromImage(image.CachedImage!.Frames[0]);
        var red = false;
        for (var y = 0; y < bitmap.Height && !red; y++)
            for (var x = 0; x < bitmap.Width && !red; x++)
                red = bitmap.GetPixel(x, y) is { Alpha: > 200, Red: > 200, Blue: < 60 };
        Assert.True(red);

        using var core = new SkUiCoreImage().SetSourceFont("M", SkUiTestHelpers.BundledFontFamily, 20, Colors.Red);
        Assert.Same(image.CachedImage, core.CachedImage);

        using var white = new SkUiCoreImage().SetSourceFont("M", SkUiTestHelpers.BundledFontFamily, 40);
        Assert.InRange(white.ImageSize.Height, 40, 64); // MAUI's default color is white
        Assert.NotSame(image.CachedImage, white.CachedImage);
    }

    [Fact]
    public async Task DownloadsAreKeptOnDiskForTheirValidity()
    {
        var handler = new StubHandler(Png(SKColors.Red));
        SkUiImageCache.HttpClient = new HttpClient(handler);
        var uri = new Uri($"https://images.test/{Key()}.png");
        var directory = SkUiImageCache.DiskCacheDirectory;

        using (var first = new SkUiCoreImage().SetSourceUri(uri))
        {
            await first.LoadingTask;
            Assert.Null(first.LoadError);
            Assert.Equal(new Size(40, 20), first.ImageSize);
        }
        Assert.Equal(1, handler.Requests);
        var file = await WaitForSingleFile(directory);

        SkUiImageCache.ClearMemoryCache();
        using (var fromDisk = new SkUiCoreImage().SetSourceUri(uri))
        {
            await fromDisk.LoadingTask;
            Assert.Equal(new Size(40, 20), fromDisk.ImageSize);
        }
        Assert.Equal(1, handler.Requests);

        SkUiImageCache.ClearMemoryCache();
        using (var uncached = new SkUiImage { Source = new UriImageSource { Uri = uri, CachingEnabled = false } })
            await uncached.LoadingTask;
        Assert.Equal(2, handler.Requests);

        SkUiImageCache.ClearMemoryCache();
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow - TimeSpan.FromDays(2)); // older than the default day
        using (var expired = new SkUiCoreImage().SetSourceUri(uri))
            await expired.LoadingTask;
        Assert.Equal(3, handler.Requests);
        await WaitUntil(() => File.Exists(file) && DateTime.UtcNow - File.GetLastWriteTimeUtc(file) < TimeSpan.FromHours(1));

        await SkUiImageCache.RemoveAsync(uri);
        Assert.False(File.Exists(file));
        Assert.Equal(0, SkUiImageCache.MemoryCacheCount);
    }

    [Fact]
    public async Task ACustomCacheKeyLetsTokenizedUrlsShareMemoryAndDiskEntries()
    {
        var handler = new StubHandler(Png(SKColors.Red));
        SkUiImageCache.HttpClient = new HttpClient(handler);
        SkUiImageCache.CacheKeyFactory = SkUiImageCacheKeys.IgnoreQueryParameters("token");
        var path = $"https://images.test/{Key()}.png";

        using var first = new SkUiImage { Source = ImageSource.FromUri(new Uri(path + "?size=s&token=aaa")) };
        await first.LoadingTask;
        using var second = new SkUiCoreImage().SetSourceUri(new Uri(path + "?token=bbb&size=s"));
        await second.LoadingTask;
        Assert.Equal(1, handler.Requests);
        Assert.Same(first.CachedImage, second.CachedImage);
        var file = await WaitForSingleFile(SkUiImageCache.DiskCacheDirectory);

        SkUiImageCache.ClearMemoryCache();
        using (var fromDisk = new SkUiCoreImage().SetSourceUri(new Uri(path + "?token=ccc&size=s")))
            await fromDisk.LoadingTask;
        Assert.Equal(1, handler.Requests); // a new token, the same download on disk

        using (var otherSize = new SkUiCoreImage().SetSourceUri(new Uri(path + "?size=l&token=aaa")))
            await otherSize.LoadingTask;
        Assert.Equal(2, handler.Requests); // parameters that are not ignored still count

        await SkUiImageCache.RemoveAsync(new Uri(path + "?size=s&token=zzz"));
        Assert.False(File.Exists(file));
    }

    [Fact]
    public async Task TheCacheKeyFactoryFallsBackToDefaultsCachesStreamsAndReportsItsErrors()
    {
        var bytes = Png(SKColors.Red);
        var opens = 0;
        SkUiImageCache.CacheKeyFactory = source => source is SkUiStreamImageSource ? "avatar-7" : null;
        using var a = new SkUiImage { Source = ImageSource.FromStream(() => { opens++; return new MemoryStream(bytes); }) };
        await a.LoadingTask;
        using var b = new SkUiImage { Source = ImageSource.FromStream(() => { opens++; return new MemoryStream(bytes); }) };
        Assert.Equal(1, opens); // keyed by the factory: the second stream view is a cache hit
        Assert.Same(a.CachedImage, b.CachedImage);

        using var font = SkUiTestHelpers.UseBundledFont();
        using var glyph = new SkUiCoreImage().SetSourceFont("M", SkUiTestHelpers.BundledFontFamily, 20); // null: default key
        using var same = new SkUiCoreImage().SetSourceFont("M", SkUiTestHelpers.BundledFontFamily, 20);
        Assert.Same(glyph.CachedImage, same.CachedImage);

        SkUiImageCache.CacheKeyFactory = _ => throw new FormatException("bad key");
        using var failed = new SkUiCoreImage().SetSourceStream(Open(bytes));
        await failed.LoadingTask;
        Assert.IsType<FormatException>(Assert.IsType<InvalidOperationException>(failed.LoadError).InnerException);
    }

    [Fact]
    public void WithoutQueryParametersKeepsTheRestInOrder()
    {
        var uri = new Uri("https://cdn.test/a/b.png?w=40&Token=x%20y&h=20&sig=abc#frag");
        Assert.Equal("https://cdn.test/a/b.png?w=40&h=20", SkUiImageCacheKeys.WithoutQueryParameters(uri, "token", "SIG"));
        Assert.Equal("https://cdn.test/a/b.png", SkUiImageCacheKeys.WithoutQueryParameters(uri));
        Assert.Equal("https://cdn.test/a/b.png", SkUiImageCacheKeys.WithoutQueryParameters(new Uri("https://cdn.test/a/b.png?token=1"), "token"));
        var factory = SkUiImageCacheKeys.IgnoreQueryParameters("token");
        Assert.Null(factory(SkUiImageSource.FromFile("a.png")));
        Assert.Equal("https://cdn.test/a/b.png?w=1", factory(SkUiImageSource.FromUri(new Uri("https://cdn.test/a/b.png?token=2&w=1"))));
    }

    [Fact]
    public async Task ClearAsyncEmptiesTheChosenCachesAndTheDiskSizeIsReported()
    {
        var bytes = Png(SKColors.Red);
        SkUiImageCache.HttpClient = new HttpClient(new StubHandler(bytes));
        Assert.Equal(0, await SkUiImageCache.GetDiskCacheBytesAsync());
        using (var image = new SkUiCoreImage().SetSourceUri(new Uri($"https://images.test/{Key()}.png")))
            await image.LoadingTask;
        await WaitForSingleFile(SkUiImageCache.DiskCacheDirectory);
        Assert.Equal(bytes.Length, await SkUiImageCache.GetDiskCacheBytesAsync());
        Assert.Equal(1, SkUiImageCache.MemoryCacheCount);

        await SkUiImageCache.ClearAsync(SkUiImageCacheType.Memory);
        Assert.Equal(0, SkUiImageCache.MemoryCacheCount);
        Assert.Equal(bytes.Length, await SkUiImageCache.GetDiskCacheBytesAsync());

        using (var image = new SkUiCoreImage().SetSourceStream(Open(bytes), Key()))
            await image.LoadingTask;
        await SkUiImageCache.ClearAsync(); // both
        Assert.Equal(0, SkUiImageCache.MemoryCacheCount);
        Assert.Equal(0, await SkUiImageCache.GetDiskCacheBytesAsync());
    }

    private static SKColor PixelAt(SkUiCoreImage image, int x, int y)
    {
        image.Measure(double.PositiveInfinity, double.PositiveInfinity);
        image.Arrange(new Rect(0, 0, 40, 40));
        using var bitmap = new SKBitmap(40, 40);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            image.Paint(canvas);
        }
        return bitmap.GetPixel(x, y);
    }

    [Fact]
    public async Task TheLoadingPlaceholderShowsAndSizesTheViewUntilTheImageArrives()
    {
        var gate = new TaskCompletionSource();
        var bytes = Png(SKColors.Red, 40, 40);
        using var image = new SkUiCoreImage()
            .SetLoadingPlaceholder(SkUiImageSource.FromStream(Open(Png(SKColors.Blue, 10, 10)), Key()))
            .SetErrorPlaceholder(SkUiImageSource.FromStream(Open(Png(SKColors.Black, 30, 30)), Key()));
        image.SetSourceStream(async token => { await gate.Task.WaitAsync(token); return new MemoryStream(bytes); });
        await WaitUntil(() => image.IsShowingPlaceholder); // the keyed placeholder stream decodes off the UI thread
        Assert.True(image.IsLoading);
        Assert.Equal(Size.Zero, image.ImageSize); // the image's own size
        Assert.Equal(new Size(10, 10), image.Measure(double.PositiveInfinity, double.PositiveInfinity)); // measured as the placeholder
        Assert.Equal(SKColors.Blue, PixelAt(image, 20, 20));

        gate.SetResult();
        await image.LoadingTask;
        Assert.False(image.IsShowingPlaceholder);
        Assert.Equal(new Size(40, 40), image.Measure(double.PositiveInfinity, double.PositiveInfinity));
        Assert.Equal(SKColors.Red, PixelAt(image, 20, 20));
    }

    [Fact]
    public async Task TheErrorPlaceholderShowsAfterAFailureAndGoesWithTheNextSource()
    {
        var error = Png(SKColors.Black, 30, 30);
        using var image = new SkUiImage
        {
            ErrorPlaceholder = ImageSource.FromStream(() => new MemoryStream(error)),
            Source = ImageSource.FromStream(() => new MemoryStream([1, 2, 3]))
        };
        await image.LoadingTask;
        Assert.NotNull(image.LoadError);
        await WaitUntil(() => image.IsShowingPlaceholder);
        Assert.Equal(new Size(30, 30), ((IView)image).Measure(double.PositiveInfinity, double.PositiveInfinity));

        image.Source = ImageSource.FromStream(() => new MemoryStream(Png(SKColors.Red)));
        await image.LoadingTask;
        Assert.False(image.IsShowingPlaceholder);
        Assert.Null(image.LoadError);
        Assert.Equal(new Size(40, 20), image.ImageSize);
    }

    [Fact]
    public async Task PlaceholdersTakeTheImagesTransformationsUnlessTurnedOff()
    {
        var key = Key();
        var placeholder = SkUiImageSource.FromStream(Open(Png(SKColors.Blue)), key); // 40 × 20
        using var image = new SkUiCoreImage().SetTransformations(new SkUiCircleTransformation()).SetErrorPlaceholder(placeholder);
        await image.SetSourceStream(Open([1, 2, 3])).LoadingTask;
        await WaitUntil(() => image.IsShowingPlaceholder);
        Assert.Equal(new Size(20, 20), image.Measure(double.PositiveInfinity, double.PositiveInfinity)); // circle-cropped too
        image.SetTransformPlaceholders(false);
        await WaitUntil(() => image.IsShowingPlaceholder && image.Measure(double.PositiveInfinity, double.PositiveInfinity) == new Size(40, 20));
    }

    [Fact]
    public async Task EveryLoadStartsAndFinishesOnceWithItsOutcomeAndOrigin()
    {
        var events = new List<string>();
        void Track(SkUiCoreImage image)
        {
            image.LoadingStarted += (_, args) => events.Add($"start loading={image.IsLoading}");
            image.LoadingFinished += (_, args) => events.Add($"{args.Status} {args.Origin} {args.Error?.GetType().Name} {args.ImageSize.Width}x{args.ImageSize.Height} loading={image.IsLoading}");
        }
        var key = Key();
        var bytes = Png(SKColors.Red);
        using var first = new SkUiCoreImage();
        Track(first);
        await first.SetSourceStream(Open(bytes), key).LoadingTask;
        Assert.Equal(["start loading=True", "Succeeded Stream  40x20 loading=False"], events);

        events.Clear();
        using var second = new SkUiCoreImage();
        Track(second);
        second.SetSourceStream(Open(bytes), key); // memory hit: both events at once
        Assert.Equal(["start loading=False", "Succeeded MemoryCache  40x20 loading=False"], events);

        events.Clear();
        var gate = new TaskCompletionSource();
        second.SetSourceStream(async token => { await gate.Task.WaitAsync(token); return new MemoryStream(bytes); });
        second.SetSourceStream(Open([1, 2, 3])); // replaces the pending load
        await second.LoadingTask;
        Assert.Equal(["start loading=True", "Cancelled   0x0 loading=True", "start loading=True", "Failed  InvalidDataException 0x0 loading=False"], events);

        events.Clear();
        var never = new SkUiCoreImage();
        Track(never);
        never.SetSourceStream(async token => { await Task.Delay(Timeout.Infinite, token); return Stream.Null; });
        never.Dispose();
        Assert.Equal(["start loading=True", "Cancelled   0x0 loading=True"], events);
    }

    [Fact]
    public async Task OriginsReportFilesFontsNetworkAndTheDiskCache()
    {
        var origins = new List<SkUiImageOrigin?>();
        void Track(SkUiImage image) => image.LoadingFinished += (_, args) => origins.Add(args.Origin);

        var path = Path.Combine(_testDirectory, "origin.png");
        Directory.CreateDirectory(_testDirectory);
        await File.WriteAllBytesAsync(path, Png(SKColors.Red));
        using var file = new SkUiImage();
        Track(file);
        file.Source = ImageSource.FromFile(path);
        await file.LoadingTask;

        using var font = SkUiTestHelpers.UseBundledFont();
        using var glyph = new SkUiImage();
        Track(glyph);
        glyph.Source = new FontImageSource { Glyph = "Q", FontFamily = SkUiTestHelpers.BundledFontFamily, Size = 17 };

        SkUiImageCache.HttpClient = new HttpClient(new StubHandler(Png(SKColors.Red)));
        var uri = new Uri($"https://images.test/{Key()}.png");
        using var web = new SkUiImage();
        Track(web);
        web.Source = ImageSource.FromUri(uri);
        await web.LoadingTask;
        await WaitForSingleFile(SkUiImageCache.DiskCacheDirectory);
        SkUiImageCache.ClearMemoryCache();
        web.Source = ImageSource.FromUri(uri);
        await web.LoadingTask;

        Assert.Equal([SkUiImageOrigin.File, SkUiImageOrigin.Font, SkUiImageOrigin.Network, SkUiImageOrigin.DiskCache], origins);
    }

    [Fact]
    public async Task NonImageResponsesAndHttpErrorsFail()
    {
        SkUiImageCache.HttpClient = new HttpClient(new StubHandler("<html></html>"u8.ToArray(), "text/html"));
        using var html = new SkUiCoreImage().SetSourceUri(new Uri($"https://images.test/{Key()}.png"));
        await html.LoadingTask;
        Assert.IsType<InvalidDataException>(html.LoadError);

        SkUiImageCache.HttpClient = new HttpClient(new StubHandler([], status: HttpStatusCode.NotFound));
        using var missing = new SkUiImage { Source = ImageSource.FromUri(new Uri($"https://images.test/{Key()}.png")) };
        await missing.LoadingTask;
        Assert.IsType<HttpRequestException>(missing.LoadError);
        Assert.False(missing.IsLoading);
    }

    [Fact]
    public async Task SliderThumbImagesReplaceTheLooksThumbUpright()
    {
        var bytes = Png(SKColors.Red, 12, 12);
        var slider = new SkUiSlider
        {
            Value = 0.5,
            ThumbColor = Colors.Blue,
            MinimumTrackColor = Colors.Transparent,
            MaximumTrackColor = Colors.Transparent,
            ThumbImageSource = ImageSource.FromStream(() => new MemoryStream(bytes))
        };
        await WaitUntil(() => RenderSlider(slider).GetPixel(50, 16) == SKColors.Red);
        using var bitmap = RenderSlider(slider);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(50, 16)); // centered on the thumb
        Assert.Equal(0, bitmap.GetPixel(50, 5).Alpha); // the 12 DIP image, not the look's 20 DIP blue thumb
        Assert.Equal(0, bitmap.GetPixel(41, 16).Alpha);

        var core = new SkUiCoreSlider().SetValue(0.5).SetThumbColor(Colors.Blue)
            .SetMinimumTrackColor(Colors.Transparent).SetMaximumTrackColor(Colors.Transparent)
            .SetThumbImageSource(SkUiImageSource.FromStream(Open(bytes), Key()));
        await WaitUntil(() => Render(canvas => { core.Arrange(new Rect(0, 0, 100, 32)); core.Paint(canvas); }).GetPixel(50, 16) == SKColors.Red);
    }

    private static SKBitmap RenderSlider(SkUiSlider slider)
    {
        SkUiTestHelpers.Arrange(slider, 100, 32);
        return Render(slider.Paint);
    }

    private static SKBitmap Render(Action<SKCanvas> paint)
    {
        var bitmap = new SKBitmap(100, 32);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        paint(canvas);
        return bitmap;
    }

    private static async Task<string> WaitForSingleFile(string directory)
    {
        await WaitUntil(() => Directory.Exists(directory) && Directory.GetFiles(directory, "*.img").Length == 1);
        return Directory.GetFiles(directory, "*.img")[0];
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 200 && !condition(); attempt++)
            await Task.Delay(25);
        Assert.True(condition());
    }

    private sealed class StubHandler(byte[] body, string mediaType = "image/png", HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        private int _requests;

        public int Requests => Volatile.Read(ref _requests);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requests);
            var content = new ByteArrayContent(body);
            content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
            return Task.FromResult(new HttpResponseMessage(status) { Content = content });
        }
    }
}
