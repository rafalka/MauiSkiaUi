using System.Net;
using System.Net.Http.Headers;
using System.Text;
using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// 1.7 L1: SVG bytes through the one image loader, from every source that reads bytes. The package lookup needs a platform
/// (<c>FileSystem</c>, drawables, bundle files), so it is checked on devices.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class VectorImageLoadingTests : IDisposable
{
    // The left half red: a bitmap scaled up would blur the edge in the middle.
    private const string Icon = """<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24"><rect width="12" height="24" fill="#f00"/></svg>""";
    private const string Wide = """<svg xmlns="http://www.w3.org/2000/svg" width="48" height="24"><rect width="48" height="24" fill="#00f"/></svg>""";
    private const string Tall = """<svg xmlns="http://www.w3.org/2000/svg" width="14.41pt" height="20.77pt" viewBox="0 0 14.41 20.77"><rect width="14.41" height="20.77"/></svg>""";
    private const string Plain = """<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24"><path d="M0 0h24v24H0z"/></svg>""";
    private const string Gradient = """<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24"><linearGradient id="a"/></svg>""";
    private const string SvgMediaType = "image/svg+xml";

    private readonly long _memoryBudget = SkUiImageCache.MemoryCacheMaxBytes;
    private readonly HttpClient _httpClient = SkUiImageCache.HttpClient;
    private readonly string _diskDirectory = SkUiImageCache.DiskCacheDirectory;
    private readonly string _testDirectory = Path.Combine(Path.GetTempPath(), "skui-vector-" + Guid.NewGuid().ToString("N"));

    public VectorImageLoadingTests()
    {
        SkUiImageCache.MemoryCacheMaxBytes = 64L * 1024 * 1024;
        SkUiImageCache.ClearMemoryCache();
        SkUiImageCache.DiskCacheDirectory = Path.Combine(_testDirectory, "cache");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        SkUiImageCache.ClearMemoryCache();
        SkUiImageCache.MemoryCacheMaxBytes = _memoryBudget;
        SkUiImageCache.HttpClient = _httpClient;
        SkUiImageCache.DiskCacheDirectory = _diskDirectory;
        try { Directory.Delete(_testDirectory, recursive: true); }
        catch (IOException) { }
    }

    private static byte[] Bytes(string svg) => Encoding.UTF8.GetBytes(svg);

    private static string Key() => Guid.NewGuid().ToString("N");

    private static SKBitmap Pixels(SKImage image) => SKBitmap.FromImage(image);

    private string WriteFile(string svg)
    {
        var path = Path.Combine(_testDirectory, Key() + ".svg");
        File.WriteAllText(path, svg);
        return path;
    }

    [Theory]
    [InlineData(1f, 24, 24)]
    [InlineData(2.625f, 63, 63)]
    [InlineData(3f, 72, 72)]
    public void Without_bounds_it_draws_at_the_display_scale(float displayScale, int width, int height)
    {
        var decoded = SkUiImageDecoder.Decode(Bytes(Icon), displayScale: displayScale);

        Assert.Equal(new SKSizeI(width, height), new SKSizeI(decoded.Frames[0].Width, decoded.Frames[0].Height));
        Assert.Equal(new Size(24, 24), decoded.Size);
        Assert.True(decoded.IsReduced); // larger bounds give more pixels, unlike a full-size PNG
        Assert.Empty(decoded.Durations);
    }

    [Theory]
    [InlineData(240, 240, false, 240, 240)] // larger than the file: a vector scales up
    [InlineData(10, 10, false, 10, 10)]
    [InlineData(240, 0, false, 240, 240)]
    [InlineData(0, 120, false, 120, 120)]
    public void With_bounds_it_draws_at_them(int boundWidth, int boundHeight, bool fill, int width, int height)
    {
        var decoded = SkUiImageDecoder.Decode(Bytes(Icon), maxPixels: new SKSizeI(boundWidth, boundHeight), fill: fill, displayScale: 3);

        Assert.Equal(new SKSizeI(width, height), new SKSizeI(decoded.Frames[0].Width, decoded.Frames[0].Height));
        Assert.Equal(new Size(24, 24), decoded.Size);
    }

    [Theory]
    [InlineData(false, 100, 50)]
    [InlineData(true, 200, 100)]
    public void Bounds_keep_the_aspect(bool fill, int width, int height)
    {
        var decoded = SkUiImageDecoder.Decode(Bytes(Wide), maxPixels: new SKSizeI(100, 100), fill: fill);

        Assert.Equal(new SKSizeI(width, height), new SKSizeI(decoded.Frames[0].Width, decoded.Frames[0].Height));
    }

    [Fact]
    public void A_fractional_size_keeps_its_aspect_at_bounds()
    {
        var decoded = SkUiImageDecoder.Decode(Bytes(Tall), maxPixels: new SKSizeI(0, 1000));

        Assert.Equal(694, decoded.Frames[0].Width); // not 667, from rounding the size to 14 × 21 first
        Assert.Equal(1000, decoded.Frames[0].Height);
    }

    [Fact]
    public void The_decode_limit_holds_with_the_aspect_kept()
    {
        var limit = SkUiImageDecoder.MaxDecodeDimension;

        var bounded = SkUiImageDecoder.Decode(Bytes(Wide), maxPixels: new SKSizeI(limit * 4, 0));
        Assert.Equal(new SKSizeI(limit, limit / 2), new SKSizeI(bounded.Frames[0].Width, bounded.Frames[0].Height));

        var large = SkUiImageDecoder.Decode(Bytes("""<svg xmlns="http://www.w3.org/2000/svg" width="100000" height="50000"/>"""));
        Assert.Equal(new SKSizeI(limit, limit / 2), new SKSizeI(large.Frames[0].Width, large.Frames[0].Height));
    }

    [Fact]
    public void It_is_drawn_at_the_bounds_not_scaled_up()
    {
        var decoded = SkUiImageDecoder.Decode(Bytes(Icon), maxPixels: new SKSizeI(240, 240));

        using var pixels = Pixels(decoded.Frames[0]);
        Assert.Equal(SKColors.Red, pixels.GetPixel(119, 120));
        Assert.Equal(0, pixels.GetPixel(120, 120).Alpha);
    }

    [Fact]
    public void CurrentColor_is_black_as_SVG_says()
    {
        var decoded = SkUiImageDecoder.Decode(Bytes("""<svg xmlns="http://www.w3.org/2000/svg" width="4" height="4"><rect width="4" height="4" fill="currentColor"/></svg>"""));

        using var pixels = Pixels(decoded.Frames[0]);
        Assert.Equal(SKColors.Black, pixels.GetPixel(2, 2));
    }

    [Fact]
    public void Other_bytes_still_fail_as_a_corrupt_image()
    {
        var error = Assert.Throws<InvalidDataException>(() => SkUiImageDecoder.Decode(Bytes("not an image")));

        Assert.Equal("Unsupported or corrupt image.", error.Message);
    }

    [Fact]
    public async Task A_file_loads_on_both_layers_and_shares_one_entry()
    {
        var path = WriteFile(Icon);
        var loads = SkUiImageLoader.LoadCount;

        var image = new SkUiImage { Source = path };
        await image.LoadingTask;
        Assert.Null(image.LoadError);
        Assert.Equal(new Size(24, 24), image.ImageSize);

        var core = new SkUiCoreImage().SetSourceFile(path);
        await core.LoadingTask;
        Assert.Same(image.CachedImage, core.CachedImage);
        Assert.Equal(1, SkUiImageLoader.LoadCount - loads);
        Assert.True(core.CachedImage!.IsReduced);
    }

    [Fact]
    public async Task A_stream_loads_and_a_key_caches_it()
    {
        var opens = 0;
        Func<CancellationToken, Task<Stream>> open = _ =>
        {
            opens++;
            return Task.FromResult<Stream>(new MemoryStream(Bytes(Icon)));
        };
        var key = Key();

        var first = new SkUiCoreImage().SetSourceStream(open, key);
        await first.LoadingTask;
        Assert.Null(first.LoadError);
        var second = new SkUiCoreImage().SetSourceStream(open, key);
        await second.LoadingTask;

        Assert.Same(first.CachedImage, second.CachedImage);
        Assert.Equal(1, opens);
    }

    [Fact]
    public async Task A_download_reports_progress_and_is_kept_on_disk()
    {
        using var _ = SkUiTestHelpers.UseTestDispatcher(); // dispatched reports write the bindable store
        var handler = new Handler(Bytes(Icon));
        SkUiImageCache.HttpClient = new HttpClient(handler);
        var uri = new Uri($"https://images.test/{Key()}.svg");

        var first = new SkUiImage { Source = ImageSource.FromUri(uri) };
        await first.LoadingTask;
        Assert.Null(first.LoadError);
        Assert.Equal(new Size(24, 24), first.ImageSize);
        Assert.Equal(1, first.LoadingProgress);
        await WaitForSingleFile();

        SkUiImageCache.ClearMemoryCache();
        var origin = default(SkUiImageOrigin?);
        var second = new SkUiImage();
        second.LoadingFinished += (_, args) => origin = args.Origin;
        second.Source = ImageSource.FromUri(uri);
        await second.LoadingTask;

        Assert.Equal(1, handler.Requests);
        Assert.Equal(SkUiImageOrigin.DiskCache, origin);
    }

    [Fact]
    public async Task A_corrupt_cached_file_is_downloaded_again()
    {
        var handler = new Handler(Bytes(Icon));
        SkUiImageCache.HttpClient = new HttpClient(handler);
        var uri = new Uri($"https://images.test/{Key()}.svg");

        var first = new SkUiCoreImage().SetSourceUri(uri);
        await first.LoadingTask;
        var file = await WaitForSingleFile();
        first.Clear();
        await File.WriteAllTextAsync(file, "<svg xmlns=");
        SkUiImageCache.ClearMemoryCache();

        var corrupt = new SkUiCoreImage().SetSourceUri(uri);
        await corrupt.LoadingTask;
        Assert.IsType<InvalidDataException>(corrupt.LoadError);
        Assert.Equal(1, handler.Requests);
        await WaitUntil(() => !File.Exists(file));

        var again = new SkUiCoreImage().SetSourceUri(uri);
        await again.LoadingTask;
        Assert.Null(again.LoadError);
        Assert.Equal(2, handler.Requests);
    }

    [Fact]
    public async Task A_file_that_cannot_be_drawn_names_what_stopped_it_and_is_not_kept()
    {
        var handler = new Handler(Bytes(Gradient));
        SkUiImageCache.HttpClient = new HttpClient(handler);

        var image = new SkUiImage { Source = ImageSource.FromUri(new Uri($"https://images.test/{Key()}.svg")) };
        await image.LoadingTask;

        Assert.IsType<InvalidDataException>(image.LoadError);
        Assert.Contains("linearGradient", image.LoadError!.Message);
        Assert.Null(image.CachedImage);
        await Task.Delay(50); // a disk write is not awaited: give a wrong one the time to show
        Assert.False(Directory.Exists(SkUiImageCache.DiskCacheDirectory) && Directory.GetFiles(SkUiImageCache.DiskCacheDirectory, "*.img").Length > 0);
    }

    [Fact]
    public async Task Keys_differ_by_bounds_and_equal_bounds_share()
    {
        var path = WriteFile(Icon);
        var loads = SkUiImageLoader.LoadCount;

        var small = new SkUiCoreImage().SetDownsample(10, 10).SetSourceFile(path);
        await small.LoadingTask;
        var large = new SkUiCoreImage().SetDownsample(100, 100).SetSourceFile(path);
        await large.LoadingTask;
        var sameLarge = new SkUiCoreImage().SetDownsample(100, 100).SetSourceFile(path);
        await sameLarge.LoadingTask;

        Assert.NotSame(small.CachedImage, large.CachedImage);
        Assert.Same(large.CachedImage, sameLarge.CachedImage);
        Assert.Equal(2, SkUiImageLoader.LoadCount - loads);
        Assert.True(large.CachedImage!.Bytes > small.CachedImage!.Bytes);
    }

    [Fact]
    public async Task A_transformation_applies_to_it()
    {
        var core = new SkUiCoreImage().SetTransformations(new SkUiTintTransformation(Colors.Blue)).SetSourceFile(WriteFile(Plain));
        await core.LoadingTask;

        Assert.Null(core.LoadError);
        using var pixels = Pixels(core.CachedImage!.Frames[0]);
        Assert.Equal(SKColors.Blue, pixels.GetPixel(12, 12));
    }

    [Fact]
    public async Task A_PNG_with_the_svg_name_still_decodes_as_a_PNG()
    {
        var path = Path.Combine(_testDirectory, "misnamed.svg");
        await File.WriteAllBytesAsync(path, ImageLoadingTests.Png(SKColors.Red));

        var core = new SkUiCoreImage().SetSourceFile(path);
        await core.LoadingTask;

        Assert.Null(core.LoadError);
        Assert.Equal(new Size(40, 20), core.ImageSize);
        Assert.False(core.CachedImage!.IsReduced);
    }

    private static async Task<string> WaitForSingleFile()
    {
        var directory = SkUiImageCache.DiskCacheDirectory;
        await WaitUntil(() => Directory.Exists(directory) && Directory.GetFiles(directory, "*.img").Length == 1);
        return Directory.GetFiles(directory, "*.img")[0];
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 200 && !condition(); attempt++)
            await Task.Delay(25);
        Assert.True(condition());
    }

    private sealed class Handler(byte[] body) : HttpMessageHandler
    {
        private int _requests;

        public int Requests => Volatile.Read(ref _requests);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requests);
            var content = new ByteArrayContent(body);
            content.Headers.ContentType = new MediaTypeHeaderValue(SvgMediaType);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
}
