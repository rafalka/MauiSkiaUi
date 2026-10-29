using System.ComponentModel;
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

        var first = new SkUiImage { Source = ImageSource.FromFile(path) };
        await first.LoadingTask;
        Assert.Equal(new Size(40, 20), first.ImageSize);
        Assert.Equal(loads + 1, SkUiImageLoader.LoadCount);

        var second = new SkUiImage { Source = ImageSource.FromFile(path) };
        Assert.True(second.LoadingTask.IsCompleted); // a memory hit applies synchronously: no empty frame
        Assert.False(second.IsLoading);
        Assert.Equal(new Size(40, 20), second.ImageSize);
        Assert.Same(first.CachedImage, second.CachedImage);

        var core = new SkUiCoreImage().SetSourceFile(path); // Core shares the cache
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

        var leaving = new SkUiCoreImage().SetSource(source);
        var staying = new SkUiCoreImage().SetSource(source);
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

        var image = new SkUiCoreImage().SetSource(source);
        var loadToken = await token.Task;
        image.Clear();
        await WaitUntil(() => loadToken.IsCancellationRequested); // the last waiter leaving cancels the shared work
        Assert.False(image.IsLoading);
        Assert.Null(image.LoadError);
    }

    [Fact]
    public async Task EntriesLiveWhileShownAndAreDisposedAfterEvictionAndRelease()
    {
        var shown = new SkUiCoreImage().SetSourceStream(Open(Png(SKColors.Red)), Key());
        await shown.LoadingTask;
        var entry = shown.CachedImage!;
        Assert.Equal(1, SkUiImageCache.MemoryCacheCount);
        Assert.Equal(40 * 20 * 4, SkUiImageCache.MemoryCacheBytes);

        SkUiImageCache.ClearMemoryCache();
        Assert.Equal(0, SkUiImageCache.MemoryCacheCount);
        Assert.False(entry.IsReleased); // the view still shows it
        shown.Clear(); // releases its reference
        Assert.True(entry.IsReleased);

        var cached = new SkUiCoreImage().SetSourceStream(Open(Png(SKColors.Blue)), Key());
        await cached.LoadingTask;
        var kept = cached.CachedImage!;
        cached.Clear();
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
                new SkUiCoreImage().SetSourceStream(Open(Png(SKColors.Red)), keys[0]).Clear(); // touch the first
        }
        Assert.Equal(2, SkUiImageCache.MemoryCacheCount);
        var loads = SkUiImageLoader.LoadCount;
        var first = new SkUiCoreImage().SetSourceStream(Open(Png(SKColors.Red)), keys[0]);
        Assert.Equal(loads, SkUiImageLoader.LoadCount); // recently used: still cached
        var second = new SkUiCoreImage().SetSourceStream(Open(Png(SKColors.Red)), keys[1]);
        await second.LoadingTask;
        Assert.Equal(loads + 1, SkUiImageLoader.LoadCount); // the least recently used went
        images.ForEach(image => image.Clear());
    }

    [Fact]
    public async Task StreamsWithoutAKeyAndCacheTypeNoneAreNotCached()
    {
        var opens = 0;
        var bytes = Png(SKColors.Red);
        var a = new SkUiCoreImage().SetSourceStream(Open(bytes, () => opens++));
        var b = new SkUiCoreImage().SetSourceStream(Open(bytes, () => opens++));
        await Task.WhenAll(a.LoadingTask, b.LoadingTask);
        Assert.Equal(2, opens);
        Assert.NotSame(a.CachedImage, b.CachedImage);

        var key = Key();
        var c = new SkUiCoreImage().SetCacheType(SkUiImageCacheType.None).SetSourceStream(Open(bytes, () => opens++), key);
        var d = new SkUiCoreImage().SetCacheType(SkUiImageCacheType.Disk).SetSourceStream(Open(bytes, () => opens++), key);
        await Task.WhenAll(c.LoadingTask, d.LoadingTask);
        Assert.Equal(4, opens);
        Assert.Equal(0, SkUiImageCache.MemoryCacheCount);
    }

    [Fact]
    public async Task DownsamplingDecodesSmallerKeepsTheLayoutSizeAndIsCachedSeparately()
    {
        var key = Key();
        var bytes = Png(SKColors.Red, 400, 200);
        var full = new SkUiCoreImage().SetSourceStream(Open(bytes), key);
        var thumb = new SkUiCoreImage().SetDownsample(100, 0).SetSourceStream(Open(bytes), key);
        await Task.WhenAll(full.LoadingTask, thumb.LoadingTask);
        Assert.Equal(400, full.CachedImage!.Frames[0].Width);
        Assert.Equal((100, 50), (thumb.CachedImage!.Frames[0].Width, thumb.CachedImage.Frames[0].Height));
        Assert.Equal(new Size(400, 200), thumb.ImageSize);
        Assert.Equal(2, SkUiImageCache.MemoryCacheCount);

        var bounded = new SkUiImage { DownsampleHeight = 20, Source = ImageSource.FromStream(() => new MemoryStream(bytes)) };
        await bounded.LoadingTask;
        Assert.Equal((40, 20), (bounded.CachedImage!.Frames[0].Width, bounded.CachedImage.Frames[0].Height));
        Assert.Equal(new Size(400, 200), bounded.ImageSize);
    }

    [Fact]
    public async Task TransformationsAreCachedPerChainAndCanChangeTheIntrinsicSize()
    {
        var key = Key();
        var bytes = Png(SKColors.Red);
        var plain = new SkUiCoreImage().SetSourceStream(Open(bytes), key);
        var circle = new SkUiCoreImage().SetTransformations(new SkUiCircleTransformation(2, Colors.Blue)).SetSourceStream(Open(bytes), key);
        await Task.WhenAll(plain.LoadingTask, circle.LoadingTask);
        Assert.Equal(new Size(40, 20), plain.ImageSize);
        Assert.Equal(new Size(20, 20), circle.ImageSize); // cropped to the centered square
        Assert.Equal(2, SkUiImageCache.MemoryCacheCount);

        using var bitmap = SKBitmap.FromImage(circle.CachedImage!.Frames[0]);
        Assert.Equal(0, bitmap.GetPixel(0, 0).Alpha);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(10, 10));
        Assert.True(bitmap.GetPixel(10, 0).Blue > 200); // border inside the circle

        // The SkUi* list: adding an item reloads with it.
        var image = new SkUiImage { Source = ImageSource.FromStream(() => new MemoryStream(bytes)) };
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
        var image = new SkUiCoreImage().SetSourceStream(Open(JpegWithOrientation(bitmap, orientation: 6)));
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

        var image = new SkUiImage { Aspect = Aspect.Fill, Source = ImageSource.FromStream(() => new MemoryStream(gif)) };
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

    [Fact]
    public void TheAnimationBudgetCountsTheFullSizeFramesKeptWhileDecoding()
    {
        // Shown as a thumbnail, the frames are small, but each is composed at the full 48×48 size first.
        using var data = SKData.CreateCopy(MauiSkiaUiDemo.DemoGif.Spinner(48));
        using var codec = SKCodec.Create(data);
        var thumbnails = 8L * 8 * 4 * codec.FrameCount;
        var bytes = SkUiImageDecoder.AnimationBytes(codec, new SKSizeI(8, 8));
        Assert.InRange(bytes, thumbnails + 48 * 48 * 4, thumbnails + 2L * 48 * 48 * 4 * codec.FrameCount);
        // Frames that later frames are not composed over are not kept: fewer than all of them at full size.
        Assert.True(bytes < thumbnails + 48L * 48 * 4 * codec.FrameCount);
        var decoded = SkUiImageDecoder.Decode(MauiSkiaUiDemo.DemoGif.Spinner(48), maxPixels: new SKSizeI(8, 8));
        Assert.Equal(codec.FrameCount, decoded.Frames.Length);
        foreach (var frame in decoded.Frames) frame.Dispose();
    }

    [Fact]
    public async Task ATransformationsSettingsAreReadWhenTheLoadStarts()
    {
        using var source = new SKBitmap(2, 1);
        source.SetPixel(0, 0, SKColors.Red);
        source.SetPixel(1, 0, SKColors.Blue);
        using var encoded = source.Encode(SKEncodedImageFormat.Png, 100);
        var bytes = encoded.ToArray();
        var flip = new SkUiFlipTransformation { Horizontal = true };
        var gate = new TaskCompletionSource();
        var image = new SkUiCoreImage().SetTransformations([flip]);
        var loading = image.SetSourceStream(async token => { await gate.Task.WaitAsync(token); return new MemoryStream(bytes); }, Key()).LoadingTask;
        flip.Horizontal = false; // during the load: applies on the next one
        gate.SetResult();
        await loading;
        image.Measure(double.PositiveInfinity, double.PositiveInfinity);
        image.Arrange(new Rect(0, 0, 2, 1));
        using var bitmap = new SKBitmap(2, 1);
        using (var canvas = new SKCanvas(bitmap))
            image.Paint(canvas);
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(0, 0)); // flipped, as the key the result is cached under says

        var copies = SkUiImageTransformation.Snapshot([flip])!;
        Assert.NotSame(flip, copies[0]);
        Assert.Equal(flip.Key, copies[0].Key);
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
        var image = new SkUiImage
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

        var core = new SkUiCoreImage().SetSourceFont("M", SkUiTestHelpers.BundledFontFamily, 20, Colors.Red);
        Assert.Same(image.CachedImage, core.CachedImage);

        var white = new SkUiCoreImage().SetSourceFont("M", SkUiTestHelpers.BundledFontFamily, 40);
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

        {
            var first = new SkUiCoreImage().SetSourceUri(uri);
            await first.LoadingTask;
            Assert.Null(first.LoadError);
            Assert.Equal(new Size(40, 20), first.ImageSize);
            first.Clear();
        }
        Assert.Equal(1, handler.Requests);
        var file = await WaitForSingleFile(directory);

        SkUiImageCache.ClearMemoryCache();
        {
            var fromDisk = new SkUiCoreImage().SetSourceUri(uri);
            await fromDisk.LoadingTask;
            Assert.Equal(new Size(40, 20), fromDisk.ImageSize);
            fromDisk.Clear();
        }
        Assert.Equal(1, handler.Requests);

        SkUiImageCache.ClearMemoryCache();
        {
            var uncached = new SkUiImage { Source = new UriImageSource { Uri = uri, CachingEnabled = false } };
            await uncached.LoadingTask;
            uncached.Source = null;
        }
        Assert.Equal(2, handler.Requests);

        SkUiImageCache.ClearMemoryCache();
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow - TimeSpan.FromDays(2)); // older than the default day
        {
            var expired = new SkUiCoreImage().SetSourceUri(uri);
            await expired.LoadingTask;
            expired.Clear();
        }
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

        var first = new SkUiImage { Source = ImageSource.FromUri(new Uri(path + "?size=s&token=aaa")) };
        await first.LoadingTask;
        var second = new SkUiCoreImage().SetSourceUri(new Uri(path + "?token=bbb&size=s"));
        await second.LoadingTask;
        Assert.Equal(1, handler.Requests);
        Assert.Same(first.CachedImage, second.CachedImage);
        var file = await WaitForSingleFile(SkUiImageCache.DiskCacheDirectory);

        SkUiImageCache.ClearMemoryCache();
        {
            var fromDisk = new SkUiCoreImage().SetSourceUri(new Uri(path + "?token=ccc&size=s"));
            await fromDisk.LoadingTask;
            fromDisk.Clear();
        }
        Assert.Equal(1, handler.Requests); // a new token, the same download on disk

        {
            var otherSize = new SkUiCoreImage().SetSourceUri(new Uri(path + "?size=l&token=aaa"));
            await otherSize.LoadingTask;
            otherSize.Clear();
        }
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
        var a = new SkUiImage { Source = ImageSource.FromStream(() => { opens++; return new MemoryStream(bytes); }) };
        await a.LoadingTask;
        var b = new SkUiImage { Source = ImageSource.FromStream(() => { opens++; return new MemoryStream(bytes); }) };
        Assert.Equal(1, opens); // keyed by the factory: the second stream view is a cache hit
        Assert.Same(a.CachedImage, b.CachedImage);

        using var font = SkUiTestHelpers.UseBundledFont();
        var glyph = new SkUiCoreImage().SetSourceFont("M", SkUiTestHelpers.BundledFontFamily, 20); // null: default key
        var same = new SkUiCoreImage().SetSourceFont("M", SkUiTestHelpers.BundledFontFamily, 20);
        Assert.Same(glyph.CachedImage, same.CachedImage);

        SkUiImageCache.CacheKeyFactory = _ => throw new FormatException("bad key");
        var failed = new SkUiCoreImage().SetSourceStream(Open(bytes));
        await failed.LoadingTask;
        Assert.IsType<FormatException>(Assert.IsType<InvalidOperationException>(failed.LoadError).InnerException);
    }

    [Fact]
    public async Task AViewsCacheKeySharesOneEntryAcrossSourcesAndIsRemovedByKey()
    {
        var handler = new StubHandler(Png(SKColors.Red));
        SkUiImageCache.HttpClient = new HttpClient(handler);
        SkUiImageCache.CacheKeyFactory = _ => throw new FormatException("not consulted while a view has a key");
        var key = Key();

        var first = new SkUiImage { CacheKey = key, Source = ImageSource.FromUri(new Uri($"https://images.test/{Key()}.png")) };
        await first.LoadingTask;
        Assert.Null(first.LoadError);
        var second = new SkUiCoreImage().SetCacheKey(key).SetSourceUri(new Uri($"https://images.test/{Key()}.png"));
        Assert.Same(first.CachedImage, second.CachedImage); // another URL, the same key: a memory hit
        var opens = 0;
        var stream = new SkUiCoreImage().SetCacheKey(key).SetSourceStream(Open(Png(SKColors.Blue), () => opens++));
        Assert.Same(first.CachedImage, stream.CachedImage); // a stream source is cacheable under a key
        Assert.Equal(0, opens);
        Assert.Equal(1, handler.Requests);
        var file = await WaitForSingleFile(SkUiImageCache.DiskCacheDirectory);

        SkUiImageCache.ClearMemoryCache();
        {
            var fromDisk = new SkUiCoreImage().SetCacheKey(key).SetSourceUri(new Uri($"https://images.test/{Key()}.png"));
            await fromDisk.LoadingTask;
            fromDisk.Clear();
        }
        Assert.Equal(1, handler.Requests); // the download is shared on disk too

        await SkUiImageCache.RemoveByKeyAsync(key);
        Assert.False(File.Exists(file));
        Assert.Equal(0, SkUiImageCache.MemoryCacheCount);
        await Assert.ThrowsAsync<ArgumentException>(() => SkUiImageCache.RemoveByKeyAsync(""));
    }

    [Fact]
    public async Task ChangingTheCacheKeyReloadsAndPlaceholdersKeepTheirOwnKeys()
    {
        var path = Path.Combine(_testDirectory, "placeholder.png");
        Directory.CreateDirectory(_testDirectory);
        await File.WriteAllBytesAsync(path, Png(SKColors.Green));
        var bytes = Png(SKColors.Red);
        var opens = 0;
        var image = new SkUiImage
        {
            Source = ImageSource.FromStream(() => { opens++; return new MemoryStream(bytes); }),
            CacheKey = "a"
        };
        await image.LoadingTask;
        var a = image.CachedImage;
        Assert.NotNull(a);

        image.CacheKey = "b";
        await image.LoadingTask;
        Assert.NotSame(a, image.CachedImage);
        image.SetCacheKey("a");
        Assert.Same(a, image.CachedImage); // back to "a": a memory hit
        var openedBefore = opens;
        image.CacheKey = "";
        await image.LoadingTask;
        Assert.Null(image.CachedImage!.Key); // no key: an unkeyed stream is decoded but not cached
        Assert.Equal(openedBefore + 1, opens);

        var pending = new SkUiImage
        {
            LoadingPlaceholder = ImageSource.FromFile(path),
            CacheKey = Key(),
            Source = ImageSource.FromStream(async token => { await Task.Delay(Timeout.Infinite, token); return Stream.Null; })
        };
        await WaitUntil(() => pending.IsShowingPlaceholder); // decoded and cached under its own key, not the view's
        var loads = SkUiImageLoader.LoadCount;
        var placeholderAsSource = new SkUiImage { Source = ImageSource.FromFile(path) };
        Assert.True(placeholderAsSource.LoadingTask.IsCompleted);
        Assert.Equal(loads, SkUiImageLoader.LoadCount);
        pending.Source = null;
    }

    private static ImageSource WideStream() =>
        new StreamImageSource { Stream = _ => Task.FromResult<Stream>(new MemoryStream(Png(SKColors.Red, 400, 200))) };

    private static SkUiImage ViewSized(Aspect aspect = Aspect.AspectFit) =>
        new() { WidthRequest = 100, HeightRequest = 100, Aspect = aspect, DownsampleToViewSize = true, CacheKey = Key() };

    [Fact]
    public async Task DownsampleToViewSizeWaitsForTheArrangeAndDecodesForIt()
    {
        var image = ViewSized();
        image.Source = WideStream();
        Assert.Null(image.CachedImage); // waits for the arrange
        Assert.False(image.IsLoading);

        SkUiTestHelpers.Arrange(image, 100, 100); // bounds 112 × 112 (steps of 16)
        await image.LoadingTask;
        var fitted = image.CachedImage!.Frames[0];
        Assert.Equal((112, 56), (fitted.Width, fitted.Height)); // fits inside the bounds
        Assert.Equal(new Size(400, 200), image.ImageSize); // the intrinsic size stays the source's

        var covering = ViewSized(Aspect.AspectFill);
        covering.Source = WideStream();
        SkUiTestHelpers.Arrange(covering, 100, 100);
        await covering.LoadingTask;
        Assert.Equal((224, 112), (covering.CachedImage!.Frames[0].Width, covering.CachedImage.Frames[0].Height));
    }

    [Fact]
    public async Task DownsampleToViewSizeDecodesAgainOnlyWhenTheViewGrows()
    {
        var image = ViewSized();
        image.Source = WideStream();
        SkUiTestHelpers.Arrange(image, 100, 100);
        await image.LoadingTask;
        var small = image.CachedImage!;

        image.WidthRequest = image.HeightRequest = 200;
        SkUiTestHelpers.Arrange(image, 200, 200);
        Assert.Same(small, image.CachedImage); // the smaller image shows until the larger one has loaded
        await image.LoadingTask;
        Assert.Equal(208, image.CachedImage!.Frames[0].Width);

        var loads = SkUiImageLoader.LoadCount;
        image.WidthRequest = image.HeightRequest = 50;
        SkUiTestHelpers.Arrange(image, 50, 50);
        SkUiTestHelpers.Arrange(image, 201, 201); // within the step
        Assert.Equal(loads, SkUiImageLoader.LoadCount);

        // A row of the same size with the same source: a memory hit at its arrange.
        var row = new SkUiImage { WidthRequest = 200, HeightRequest = 200, DownsampleToViewSize = true, CacheKey = image.CacheKey };
        row.Source = WideStream();
        SkUiTestHelpers.Arrange(row, 200, 200);
        Assert.Same(image.CachedImage, row.CachedImage);
        Assert.Equal(loads, SkUiImageLoader.LoadCount);
    }

    [Fact]
    public async Task DownsampleToViewSizeLeavesSizesFromTheImageAndExplicitBoundsAlone()
    {
        // Sized by its image: arranged at 0 × 0 while empty, so not bounded; arranged at its image size afterwards.
        var auto = new SkUiImage { DownsampleToViewSize = true, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start, CacheKey = Key() };
        auto.Source = WideStream();
        SkUiTestHelpers.Arrange(auto, 1000, 1000);
        await auto.LoadingTask;
        Assert.Equal(400, auto.CachedImage!.Frames[0].Width);
        var loads = SkUiImageLoader.LoadCount;
        SkUiTestHelpers.Arrange(auto, 1000, 1000);
        Assert.Equal(new Size(400, 200), auto.Frame.Size);
        Assert.Equal(loads, SkUiImageLoader.LoadCount);

        // Fixed width, height from the image: only the width is bounded.
        var stack = new SkUiVerticalStackLayout();
        var inStack = new SkUiImage { DownsampleToViewSize = true, CacheKey = Key() };
        stack.Add(inStack);
        inStack.Source = WideStream();
        SkUiTestHelpers.Arrange(stack, 150, 1000);
        await inStack.LoadingTask;
        Assert.Equal((160, 80), (inStack.CachedImage!.Frames[0].Width, inStack.CachedImage.Frames[0].Height));

        // Explicit bounds win: the load starts at once.
        var explicitBounds = new SkUiImage { DownsampleToViewSize = true, DownsampleWidth = 50, CacheKey = Key() };
        explicitBounds.Source = WideStream();
        await explicitBounds.LoadingTask;
        Assert.Equal(50, explicitBounds.CachedImage!.Frames[0].Width);

        // Centered images are drawn unscaled: not downsampled to the view.
        var centered = ViewSized(Aspect.Center);
        centered.Source = WideStream();
        await centered.LoadingTask;
        Assert.Equal(400, centered.CachedImage!.Frames[0].Width);
    }

    [Fact]
    public async Task DownsampleToViewSizeStartsAtTheSurfacesLayoutAndOnCoreNodes()
    {
        var root = new SkUiVerticalStackLayout();
        var image = new SkUiImage { HeightRequest = 60, DownsampleToViewSize = true, CacheKey = Key() };
        root.Add(image);
        SkUiTestHelpers.Arrange(root, 120, 400);
        var frames = new Queue<Action>();
        using (var renderer = new SkUiFrameRenderer(root, frames.Enqueue, () => { }, () => { }))
        {
            renderer.RequestFrame();
            while (frames.TryDequeue(out var frame))
                frame();
            image.Source = WideStream();
            Assert.Null(image.CachedImage);
            Assert.NotEmpty(frames); // emptying the image asked for a frame, which lays the surface out again
            while (frames.TryDequeue(out var frame))
                frame();
            await image.LoadingTask;
        }
        Assert.Equal((128, 64), (image.CachedImage!.Frames[0].Width, image.CachedImage.Frames[0].Height));

        var core = new SkUiCoreImage().SetDownsampleToViewSize(true).SetCacheKey(Key());
        core.SetSourceStream(Open(Png(SKColors.Red, 400, 200)));
        Assert.Null(core.CachedImage);
        core.Measure(100, 100);
        core.Arrange(new Rect(0, 0, 100, 100));
        await core.LoadingTask;
        Assert.Equal((112, 56), (core.CachedImage!.Frames[0].Width, core.CachedImage.Frames[0].Height));
        core.SetAspect(Aspect.AspectFill); // covering needs other bounds: loads again at the same size
        await core.LoadingTask;
        Assert.Equal((224, 112), (core.CachedImage!.Frames[0].Width, core.CachedImage.Frames[0].Height));
    }

    [Fact]
    public async Task DownloadsReportProgressToEveryViewWaitingForThem()
    {
        using var _ = SkUiTestHelpers.UseTestDispatcher(); // dispatched reports write the bindable store
        var bytes = Png(SKColors.Red, 200, 200);
        var handler = new ChunkedHandler(bytes, chunk: bytes.Length / 4 + 1, sendLength: true);
        SkUiImageCache.HttpClient = new HttpClient(handler);
        var uri = new Uri($"https://images.test/{Key()}.png");
        var image = new SkUiImage();
        var core = new SkUiCoreImage();
        var reports = new List<SkUiImageDownloadProgressEventArgs>();
        var coreReports = 0;
        image.DownloadProgress += (_, args) => { lock (reports) reports.Add(args); };
        core.DownloadProgress += (_, _) => Interlocked.Increment(ref coreReports);
        image.Source = ImageSource.FromUri(uri);
        core.SetSourceUri(uri); // joins the same download
        Assert.Equal(0, image.LoadingProgress);

        handler.Release(); // headers and the first chunk
        await WaitUntil(() => image.LoadingProgress > 0);
        Assert.InRange(image.LoadingProgress, 0.2, 0.3);
        Assert.Equal(image.LoadingProgress, (double)image.GetValue(SkUiImage.LoadingProgressProperty));
        Assert.Equal(image.LoadingProgress, core.LoadingProgress);
        Assert.True(SkUiImage.LoadingProgressProperty.IsReadOnly);

        handler.Release(3);
        await image.LoadingTask;
        await core.LoadingTask;
        Assert.Equal(1, handler.Requests);
        Assert.Equal(1, image.LoadingProgress);
        Assert.Equal(1d, image.GetValue(SkUiImage.LoadingProgressProperty));
        Assert.Equal(1, core.LoadingProgress);
        SkUiImageDownloadProgressEventArgs[] seen;
        lock (reports) seen = [.. reports];
        Assert.Equal(bytes.Length, seen[^1].BytesReceived);
        Assert.Equal(bytes.Length, seen[^1].TotalBytes);
        Assert.Equal(1, seen[^1].Progress);
        Assert.Equal(seen.Select(report => report.BytesReceived).Order(), seen.Select(report => report.BytesReceived));
        Assert.Equal(seen.Length, Volatile.Read(ref coreReports));

        // A cache hit does not report, and shows as loaded.
        reports.Clear();
        var hit = new SkUiImage();
        hit.DownloadProgress += (_, args) => reports.Add(args);
        hit.Source = ImageSource.FromUri(uri);
        Assert.Equal(1, hit.LoadingProgress);
        Assert.Empty(reports);
    }

    [Fact]
    public async Task ADownloadOfUnknownSizeReportsMinusOneAndAReplacedOneStopsReporting()
    {
        var bytes = Png(SKColors.Red, 200, 200);
        var handler = new ChunkedHandler(bytes, chunk: bytes.Length / 4 + 1, sendLength: false);
        SkUiImageCache.HttpClient = new HttpClient(handler);
        var image = new SkUiCoreImage();
        var reports = new List<SkUiImageDownloadProgressEventArgs>();
        image.DownloadProgress += (_, args) => { lock (reports) reports.Add(args); };
        image.SetSourceUri(new Uri($"https://images.test/{Key()}.png"));

        handler.Release();
        await WaitUntil(() => { lock (reports) return reports.Count > 0 && reports[^1].BytesReceived > 0; });
        Assert.Equal(-1, image.LoadingProgress);
        lock (reports)
        {
            Assert.Null(reports[^1].TotalBytes);
            Assert.Null(reports[^1].Progress);
        }

        int before;
        lock (reports) before = reports.Count;
        image.Clear(); // cancels: later chunks are not reported
        Assert.Equal(0, image.LoadingProgress);
        handler.Release(3);
        await Task.Delay(100);
        lock (reports) Assert.Equal(before, reports.Count);
    }

    [Fact]
    public void ACoreImageSetDirectlyReportsItsProgressAsLoaded()
    {
        var image = new SkUiCoreImage();
        using var bitmap = new SKBitmap(4, 4);
        bitmap.Erase(SKColors.Red);
        image.SetImage(SKImage.FromBitmap(bitmap));
        Assert.Equal(1, image.LoadingProgress);
        image.SetImage(null);
        Assert.Equal(0, image.LoadingProgress);
    }

    /// <summary>A PNG response whose body arrives one chunk per <see cref="Release"/>, with or without Content-Length.</summary>
    private sealed class ChunkedHandler(byte[] body, int chunk, bool sendLength) : HttpMessageHandler
    {
        private readonly SemaphoreSlim _chunks = new(0);
        private int _requests;

        public int Requests => Volatile.Read(ref _requests);

        public void Release(int chunks = 1) => _chunks.Release(chunks);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requests);
            await _chunks.WaitAsync(cancellationToken); // the headers come with the first chunk
            _chunks.Release();
            var content = new StreamContent(new ChunkedStream(body, chunk, _chunks));
            content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Headers.ContentLength = sendLength ? body.Length : null;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }

    private sealed class ChunkedStream(byte[] body, int chunk, SemaphoreSlim chunks) : Stream
    {
        private int _position;

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_position >= body.Length)
                return 0;
            await chunks.WaitAsync(cancellationToken);
            var count = Math.Min(Math.Min(chunk, buffer.Length), body.Length - _position);
            body.AsMemory(_position, count).CopyTo(buffer);
            _position += count;
            return count;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    [Fact]
    public void XmlDrawablesRenderAtTheirBoundsUpToTheDecodeLimitAndNotElsewhere()
    {
        var intrinsic = new SKSizeI(144, 72); // 48 × 24 dp at 3x
        Assert.Equal(intrinsic, SkUiPackageImages.RenderSize(intrinsic, default, fill: false));
        Assert.Equal(new SKSizeI(600, 300), SkUiPackageImages.RenderSize(intrinsic, new SKSizeI(600, 600), fill: false)); // a vector scales up
        Assert.Equal(new SKSizeI(1200, 600), SkUiPackageImages.RenderSize(intrinsic, new SKSizeI(600, 600), fill: true));
        Assert.Equal(new SKSizeI(72, 36), SkUiPackageImages.RenderSize(intrinsic, new SKSizeI(0, 36), fill: false));
        var limit = SkUiImageDecoder.MaxDecodeDimension;
        Assert.Equal(limit, SkUiPackageImages.RenderSize(intrinsic, new SKSizeI(limit * 4, 0), fill: false).Width);
        Assert.Equal(0, SkUiPackageImages.FindXmlDrawable("skui_vector_star")); // only Android has drawables
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
        {
            var image = new SkUiCoreImage().SetSourceUri(new Uri($"https://images.test/{Key()}.png"));
            await image.LoadingTask;
            image.Clear();
        }
        await WaitForSingleFile(SkUiImageCache.DiskCacheDirectory);
        Assert.Equal(bytes.Length, await SkUiImageCache.GetDiskCacheBytesAsync());
        Assert.Equal(1, SkUiImageCache.MemoryCacheCount);

        await SkUiImageCache.ClearAsync(SkUiImageCacheType.Memory);
        Assert.Equal(0, SkUiImageCache.MemoryCacheCount);
        Assert.Equal(bytes.Length, await SkUiImageCache.GetDiskCacheBytesAsync());

        {
            var image = new SkUiCoreImage().SetSourceStream(Open(bytes), Key());
            await image.LoadingTask;
            image.Clear();
        }
        var unrelated = Path.Combine(SkUiImageCache.DiskCacheDirectory, "notes.txt");
        await File.WriteAllTextAsync(unrelated, "the folder is the app's choice");
        await SkUiImageCache.ClearAsync(); // both
        Assert.Equal(0, SkUiImageCache.MemoryCacheCount);
        Assert.Equal(0, await SkUiImageCache.GetDiskCacheBytesAsync());
        Assert.Empty(Directory.GetFiles(SkUiImageCache.DiskCacheDirectory, "*.img"));
        Assert.True(File.Exists(unrelated)); // only the cache's own files are deleted
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
    public async Task IsLoadingIsAReadOnlyBindablePropertyForTriggersAndBindings()
    {
        using var _ = SkUiTestHelpers.UseTestDispatcher(); // as in an app: the load completes on the dispatcher
        var gate = new TaskCompletionSource();
        var bytes = Png(SKColors.Red);
        var image = new SkUiImage();
        var notified = new List<bool>();
        image.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SkUiImage.IsLoading)) notified.Add(image.IsLoading);
        };
        image.Source = ImageSource.FromStream(async token => { await gate.Task.WaitAsync(token); return new MemoryStream(bytes); });
        Assert.True((bool)image.GetValue(SkUiImage.IsLoadingProperty));
        Assert.True(SkUiImage.IsLoadingProperty.IsReadOnly);

        gate.SetResult();
        await image.LoadingTask;
        Assert.False((bool)image.GetValue(SkUiImage.IsLoadingProperty));
        Assert.Equal([true, false], notified); // once per change
    }

    [Fact]
    public async Task ALoadWithoutADispatcherLeavesTheBindableStoreToTheViewsThread()
    {
        // Finishing on the thread pool, the load raises IsLoading but does not write the store (MAUI's is not
        // thread-safe), which follows on the view's next change.
        var gate = new TaskCompletionSource();
        var bytes = Png(SKColors.Red);
        var image = new SkUiImage();
        var notified = new List<bool>();
        image.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SkUiImage.IsLoading)) notified.Add(image.IsLoading);
        };
        image.Source = ImageSource.FromStream(async token => { await gate.Task.WaitAsync(token); return new MemoryStream(bytes); });
        Assert.True((bool)image.GetValue(SkUiImage.IsLoadingProperty));

        gate.SetResult();
        await image.LoadingTask;
        Assert.False(image.IsLoading);
        Assert.Equal([true, false], notified);
        Assert.True((bool)image.GetValue(SkUiImage.IsLoadingProperty));

        // Any change on the view's thread (here its aspect; in an app, its layout) brings the store up to date.
        image.Aspect = Aspect.Fill;
        Assert.False((bool)image.GetValue(SkUiImage.IsLoadingProperty));
        Assert.Equal([true, false, false], notified);

        image.Source = null;
        Assert.False((bool)image.GetValue(SkUiImage.IsLoadingProperty));
    }

    [Fact]
    public async Task TheLoadingPlaceholderShowsAndSizesTheViewUntilTheImageArrives()
    {
        var gate = new TaskCompletionSource();
        var bytes = Png(SKColors.Red, 40, 40);
        var image = new SkUiCoreImage()
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
        var image = new SkUiImage
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
        var image = new SkUiCoreImage().SetTransformations(new SkUiCircleTransformation()).SetErrorPlaceholder(placeholder);
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
        var first = new SkUiCoreImage();
        Track(first);
        await first.SetSourceStream(Open(bytes), key).LoadingTask;
        Assert.Equal(["start loading=True", "Succeeded Stream  40x20 loading=False"], events);

        events.Clear();
        var second = new SkUiCoreImage();
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
        never.Clear(); // cancels the load
        Assert.Equal(["start loading=True", "Cancelled   0x0 loading=True"], events);
    }

    [Fact]
    public async Task ALoadFinishingOnAnotherThreadWaitsUntilTheImageIsDone()
    {
        // Without a dispatcher (as here) a load finishes on the thread pool: it must not change the image's state while the
        // UI thread is inside it, here raising the state change of a placeholder that shows at once (a font glyph).
        using var _ = SkUiTestHelpers.UseBundledFont();
        var bytes = Png(SKColors.Red);

        var coreOpened = new TaskCompletionSource<Stream>();
        var core = new SkUiCoreImage().SetSourceStream(_ => coreOpened.Task);
        using var coreLoaded = new ManualResetEventSlim();
        core.LoadingFinished += (_, args) => { if (args.Status == SkUiImageLoadStatus.Succeeded) coreLoaded.Set(); };
        Assert.False(FinishesDuringStateChange(core, coreOpened, bytes, coreLoaded,
            () => core.SetLoadingPlaceholder(SkUiImageSource.FromFont("A", SkUiTestHelpers.BundledFontFamily))));
        await core.LoadingTask;
        Assert.Equal(new Size(40, 20), core.ImageSize);

        var viewOpened = new TaskCompletionSource<Stream>();
        var view = new SkUiImage { Source = ImageSource.FromStream(_ => viewOpened.Task) };
        using var viewLoaded = new ManualResetEventSlim();
        view.LoadingFinished += (_, args) => { if (args.Status == SkUiImageLoadStatus.Succeeded) viewLoaded.Set(); };
        Assert.False(FinishesDuringStateChange(view, viewOpened, bytes, viewLoaded,
            () => view.LoadingPlaceholder = new FontImageSource { Glyph = "A", FontFamily = SkUiTestHelpers.BundledFontFamily }));
        await view.LoadingTask;
        Assert.Equal(new Size(40, 20), view.ImageSize);
    }

    /// <summary>
    /// Whether the load waiting for <paramref name="opened"/> finished (<paramref name="loaded"/>) while
    /// <paramref name="image"/> was still raising the placeholder state change of <paramref name="enter"/>: the stream is
    /// opened on another thread from inside that change.
    /// </summary>
    private static bool FinishesDuringStateChange(INotifyPropertyChanged image, TaskCompletionSource<Stream> opened, byte[] bytes,
        ManualResetEventSlim loaded, Action enter)
    {
        var entered = false;
        var finishedInside = false;
        image.PropertyChanged += OnChanged;
        enter();
        image.PropertyChanged -= OnChanged;
        Assert.True(entered);
        return finishedInside;

        void OnChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName != nameof(SkUiImage.IsShowingPlaceholder) || entered)
                return;
            entered = true;
            _ = Task.Run(() => opened.SetResult(new MemoryStream(bytes)));
            // Many times what the tiny image takes to decode; the load waits for the image, so this times out.
            finishedInside = loaded.Wait(TimeSpan.FromMilliseconds(300));
        }
    }

    [Fact]
    public async Task ReplacingLoadsAsTheyFinishOnThePoolStartsAndFinishesEachOnce()
    {
        // Without a dispatcher a finishing load disposed its cancellation source while the UI thread's next load cancelled
        // it (ObjectDisposedException), and could finish a load the next one had already cancelled.
        var bytes = Png(SKColors.Red, 4, 4);
        var core = new SkUiCoreImage();
        var view = new SkUiImage();
        var started = 0;
        var finished = 0;
        core.LoadingStarted += (_, _) => Interlocked.Increment(ref started);
        core.LoadingFinished += (_, _) => Interlocked.Increment(ref finished);
        view.LoadingStarted += (_, _) => Interlocked.Increment(ref started);
        view.LoadingFinished += (_, _) => Interlocked.Increment(ref finished);
        for (var i = 0; i < 200; i++)
        {
            var data = bytes; // a new delegate, so a new (unequal) source each time
            core.SetSourceStream(_ => Task.FromResult<Stream>(new MemoryStream(data)));
            view.Source = ImageSource.FromStream(_ => Task.FromResult<Stream>(new MemoryStream(data)));
            Thread.SpinWait(Random.Shared.Next(20_000)); // lands the next load at varying points of the last one's completion
        }
        await core.LoadingTask;
        await view.LoadingTask;
        Assert.Equal(400, Volatile.Read(ref started));
        Assert.Equal(400, Volatile.Read(ref finished));
        Assert.Equal(new Size(4, 4), core.ImageSize);
        Assert.Equal(new Size(4, 4), view.ImageSize);
    }

    [Fact]
    public async Task OriginsReportFilesFontsNetworkAndTheDiskCache()
    {
        var origins = new List<SkUiImageOrigin?>();
        void Track(SkUiImage image) => image.LoadingFinished += (_, args) => origins.Add(args.Origin);

        var path = Path.Combine(_testDirectory, "origin.png");
        Directory.CreateDirectory(_testDirectory);
        await File.WriteAllBytesAsync(path, Png(SKColors.Red));
        var file = new SkUiImage();
        Track(file);
        file.Source = ImageSource.FromFile(path);
        await file.LoadingTask;

        using var font = SkUiTestHelpers.UseBundledFont();
        var glyph = new SkUiImage();
        Track(glyph);
        glyph.Source = new FontImageSource { Glyph = "Q", FontFamily = SkUiTestHelpers.BundledFontFamily, Size = 17 };

        SkUiImageCache.HttpClient = new HttpClient(new StubHandler(Png(SKColors.Red)));
        var uri = new Uri($"https://images.test/{Key()}.png");
        var web = new SkUiImage();
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
        var handler = new StubHandler("<html></html>"u8.ToArray(), "text/html");
        SkUiImageCache.HttpClient = new HttpClient(handler);
        var page = new Uri($"https://images.test/{Key()}.png");
        var html = new SkUiCoreImage().SetSourceUri(page);
        await html.LoadingTask;
        Assert.IsType<InvalidDataException>(html.LoadError);
        // Bytes that do not decode are not kept: the next load asks the server again.
        await Task.Delay(100);
        Assert.False(Directory.Exists(SkUiImageCache.DiskCacheDirectory) && Directory.GetFiles(SkUiImageCache.DiskCacheDirectory, "*.img").Length > 0);
        await html.ReloadAsync();
        Assert.Equal(2, handler.Requests);

        SkUiImageCache.HttpClient = new HttpClient(new StubHandler([], status: HttpStatusCode.NotFound));
        var missing = new SkUiImage { Source = ImageSource.FromUri(new Uri($"https://images.test/{Key()}.png")) };
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
