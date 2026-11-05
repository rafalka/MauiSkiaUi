using System.Net;
using System.Net.Http.Headers;
using System.Text;
using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>1.7 L3: <see cref="SkUiVectorImageSource"/> and <see cref="SkUiImageSource.FromVector"/>: an icon's color and size.</summary>
[Collection(GlobalStateCollection.Name)]
public sealed class VectorImageSourceTests : IDisposable
{
    // currentColor in the left half, a written color in the right.
    private const string Icon = """<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24"><rect width="12" height="24" fill="currentColor"/><rect x="12" width="12" height="24" fill="#0f0"/></svg>""";
    private const string Wide = """<svg xmlns="http://www.w3.org/2000/svg" width="48" height="24"><rect width="48" height="24" fill="currentColor"/></svg>""";
    private const string Gradient = """<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24"><linearGradient id="a"/></svg>""";

    private readonly long _memoryBudget = SkUiImageCache.MemoryCacheMaxBytes;
    private readonly HttpClient _httpClient = SkUiImageCache.HttpClient;
    private readonly string _diskDirectory = SkUiImageCache.DiskCacheDirectory;
    private readonly string _testDirectory = Path.Combine(Path.GetTempPath(), "skui-vector-source-" + Guid.NewGuid().ToString("N"));

    public VectorImageSourceTests()
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
        SkUiImageCache.CacheKeyFactory = null;
        try { Directory.Delete(_testDirectory, recursive: true); }
        catch (IOException) { }
    }

    private static string Key() => Guid.NewGuid().ToString("N");

    private string File(string svg)
    {
        var path = Path.Combine(_testDirectory, Key() + ".svg");
        System.IO.File.WriteAllText(path, svg);
        return path;
    }

    private static SKColor Pixel(SkUiCachedImage image, int x, int y)
    {
        using var pixels = SKBitmap.FromImage(image.Frames[0]);
        return pixels.GetPixel(x, y);
    }

    [Fact]
    public async Task Color_is_the_value_of_currentColor_and_written_colors_stay()
    {
        var core = new SkUiCoreImage().SetSource(SkUiImageSource.FromVector(File(Icon), Colors.Red));
        await core.LoadingTask;

        Assert.Null(core.LoadError);
        Assert.Equal(SKColors.Red, Pixel(core.CachedImage!, 6, 12));
        Assert.Equal(SKColors.Lime, Pixel(core.CachedImage!, 18, 12));
    }

    [Fact]
    public async Task Without_a_color_it_draws_black()
    {
        var core = new SkUiCoreImage().SetSource(SkUiImageSource.FromVector(File(Icon)));
        await core.LoadingTask;

        Assert.Equal(SKColors.Black, Pixel(core.CachedImage!, 6, 12));
    }

    [Theory]
    [InlineData(0, 48, 24)]
    [InlineData(96, 96, 48)]
    [InlineData(12, 12, 6)]
    public async Task Size_is_the_longest_edge(double size, double width, double height)
    {
        var core = new SkUiCoreImage().SetSource(SkUiImageSource.FromVector(File(Wide), size: size));
        await core.LoadingTask;

        Assert.Equal(new Size(width, height), core.ImageSize);
        Assert.Equal(((int)width, (int)height), (core.CachedImage!.Frames[0].Width, core.CachedImage.Frames[0].Height));
    }

    [Fact]
    public async Task Text_is_a_source()
    {
        var first = new SkUiCoreImage().SetSource(SkUiImageSource.FromVectorText(Icon, Colors.Blue, 48));
        await first.LoadingTask;
        Assert.Null(first.LoadError);
        Assert.Equal(new Size(48, 48), first.ImageSize);
        Assert.Equal(SKColors.Blue, Pixel(first.CachedImage!, 12, 24));

        var second = new SkUiCoreImage().SetSource(SkUiImageSource.FromVectorText(Icon, Colors.Blue, 48));
        Assert.Same(first.CachedImage, second.CachedImage);
    }

    [Fact]
    public async Task Color_and_size_join_the_key_and_equal_ones_share()
    {
        var path = File(Icon);
        var loads = SkUiImageLoader.LoadCount;

        var red = new SkUiCoreImage().SetSource(SkUiImageSource.FromVector(path, Colors.Red));
        await red.LoadingTask;
        var sameRed = new SkUiCoreImage().SetSource(SkUiImageSource.FromVector(path, Colors.Red));
        var blue = new SkUiCoreImage().SetSource(SkUiImageSource.FromVector(path, Colors.Blue));
        await blue.LoadingTask;
        var larger = new SkUiCoreImage().SetSource(SkUiImageSource.FromVector(path, Colors.Red, 48));
        await larger.LoadingTask;

        Assert.Same(red.CachedImage, sameRed.CachedImage);
        Assert.NotSame(red.CachedImage, blue.CachedImage);
        Assert.NotSame(red.CachedImage, larger.CachedImage);
        Assert.Equal(3, SkUiImageLoader.LoadCount - loads);
        Assert.Equal(SkUiImageSource.FromVector(path, Colors.Red, 48), SkUiImageSource.FromVector(path, Colors.Red, 48));
    }

    [Fact]
    public async Task A_download_is_shared_by_its_colors_and_the_apps_key_factory_sees_the_download()
    {
        var handler = new Handler(Encoding.UTF8.GetBytes(Icon));
        SkUiImageCache.HttpClient = new HttpClient(handler);
        var seen = new List<SkUiImageSource>();
        SkUiImageCache.CacheKeyFactory = source =>
        {
            lock (seen) seen.Add(source);
            return null;
        };
        var uri = new Uri($"https://images.test/{Key()}.svg");

        var red = new SkUiCoreImage().SetSource(SkUiImageSource.FromVector(SkUiImageSource.FromUri(uri), Colors.Red));
        await red.LoadingTask;
        Assert.Null(red.LoadError);
        var directory = SkUiImageCache.DiskCacheDirectory;
        for (var attempt = 0; attempt < 200 && !(Directory.Exists(directory) && Directory.GetFiles(directory, "*.img").Length == 1); attempt++)
            await Task.Delay(25);
        var blue = new SkUiCoreImage().SetSource(SkUiImageSource.FromVector(SkUiImageSource.FromUri(uri), Colors.Blue));
        await blue.LoadingTask;

        Assert.Equal(1, handler.Requests);
        Assert.Equal(SKColors.Blue, Pixel(blue.CachedImage!, 6, 12));
        Assert.Single(Directory.GetFiles(directory, "*.img"));
        lock (seen)
            Assert.All(seen, source => Assert.IsType<SkUiUriImageSource>(source));
    }

    [Fact]
    public async Task Removing_a_source_removes_every_color()
    {
        var path = File(Icon);
        var red = new SkUiCoreImage().SetSource(SkUiImageSource.FromVector(path, Colors.Red));
        await red.LoadingTask;
        var blue = new SkUiCoreImage().SetSource(SkUiImageSource.FromVector(path, Colors.Blue));
        await blue.LoadingTask;
        Assert.Equal(2, SkUiImageCache.MemoryCacheCount);

        await SkUiImageCache.RemoveAsync(SkUiImageSource.FromVector(path));

        Assert.Equal(0, SkUiImageCache.MemoryCacheCount);
    }

    [Fact]
    public async Task Removing_a_download_through_a_vector_source_removes_its_file()
    {
        var handler = new Handler(Encoding.UTF8.GetBytes(Icon));
        SkUiImageCache.HttpClient = new HttpClient(handler);
        var uri = new Uri($"https://images.test/{Key()}.svg");
        var source = SkUiImageSource.FromVector(SkUiImageSource.FromUri(uri), Colors.Red);
        var image = new SkUiCoreImage().SetSource(source);
        await image.LoadingTask;
        var directory = SkUiImageCache.DiskCacheDirectory;
        for (var attempt = 0; attempt < 200 && !(Directory.Exists(directory) && Directory.GetFiles(directory, "*.img").Length == 1); attempt++)
            await Task.Delay(25);
        Assert.Single(Directory.GetFiles(directory, "*.img"));

        await SkUiImageCache.RemoveAsync(source);

        Assert.Empty(Directory.GetFiles(directory, "*.img"));
        Assert.Equal(0, SkUiImageCache.MemoryCacheCount);
    }

    [Fact]
    public async Task A_download_through_a_vector_source_reports_progress()
    {
        using var _ = SkUiTestHelpers.UseTestDispatcher(); // dispatched reports write the bindable store
        SkUiImageCache.HttpClient = new HttpClient(new Handler(Encoding.UTF8.GetBytes(Icon)));
        var reports = 0;
        var image = new SkUiImage();
        image.DownloadProgress += (_, _) => Interlocked.Increment(ref reports);

        image.Source = new SkUiVectorImageSource { Source = $"https://images.test/{Key()}.svg", Color = Colors.Red };
        await image.LoadingTask;

        Assert.Null(image.LoadError);
        Assert.Equal(1, image.LoadingProgress);
        for (var attempt = 0; attempt < 200 && Volatile.Read(ref reports) == 0; attempt++)
            await Task.Delay(10);
        Assert.True(Volatile.Read(ref reports) > 0);
    }

    [Fact]
    public async Task Reloading_the_same_vector_source_reports_a_failure()
    {
        var opens = 0;
        var core = new SkUiCoreImage().SetSource(SkUiImageSource.FromVector(SkUiImageSource.FromStream(_ =>
            ++opens == 1 ? Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(Icon))) : throw new IOException("gone"), Key()), Colors.Red));
        await core.LoadingTask;
        Assert.Null(core.LoadError);
        SkUiImageCache.ClearMemoryCache();

        await core.ReloadAsync();

        Assert.IsType<IOException>(core.LoadError);
    }

    [Theory]
    [InlineData("""<?xml version="1.0" encoding="utf-16"?>""")]
    [InlineData("""<?xml version="1.0" encoding="utf-8"?>""")]
    [InlineData("  \n\uFEFF")]
    public async Task Text_ignores_the_encoding_it_declares(string prefix)
    {
        var core = new SkUiCoreImage().SetSource(SkUiImageSource.FromVectorText(prefix + Icon, Colors.Blue));
        await core.LoadingTask;

        Assert.Null(core.LoadError);
        Assert.Equal(SKColors.Blue, Pixel(core.CachedImage!, 6, 12));
    }

    [Fact]
    public async Task A_new_color_keeps_the_icon_shown_until_it_is_drawn()
    {
        var bytes = Encoding.UTF8.GetBytes(Icon);
        var gate = new TaskCompletionSource();
        gate.SetResult();
        var key = Key();
        Func<CancellationToken, Task<Stream>> open = async token =>
        {
            await gate.Task.WaitAsync(token);
            return new MemoryStream(bytes);
        };
        var core = new SkUiCoreImage().SetSource(SkUiImageSource.FromVector(SkUiImageSource.FromStream(open, key), Colors.Red));
        await core.LoadingTask;
        var red = core.CachedImage;

        gate = new TaskCompletionSource();
        core.SetSource(SkUiImageSource.FromVector(SkUiImageSource.FromStream(open, key), Colors.Blue));
        Assert.Same(red, core.CachedImage);
        gate.SetResult();
        await core.LoadingTask;

        Assert.Equal(SKColors.Blue, Pixel(core.CachedImage!, 6, 12));
    }

    [Fact]
    public async Task Another_file_does_not_keep_the_old_icon()
    {
        var bytes = Encoding.UTF8.GetBytes(Icon);
        var gate = new TaskCompletionSource();
        var core = new SkUiCoreImage().SetSource(SkUiImageSource.FromVector(File(Wide), Colors.Red));
        await core.LoadingTask;
        Assert.NotNull(core.CachedImage);

        core.SetSource(SkUiImageSource.FromVector(SkUiImageSource.FromStream(async token =>
        {
            await gate.Task.WaitAsync(token);
            return new MemoryStream(bytes);
        }, Key()), Colors.Red));

        Assert.Null(core.CachedImage);
        gate.SetResult();
        await core.LoadingTask;
        Assert.NotNull(core.CachedImage);
    }

    [Fact]
    public async Task A_sized_view_draws_a_vector_source_for_its_size_at_once()
    {
        var path = File(Icon);
        var source = new SkUiVectorImageSource { Source = path, Color = Colors.Red };
        var image = new SkUiImage();
        SkUiTestHelpers.Arrange(image, 96, 96);
        var loads = SkUiImageLoader.LoadCount;

        image.Source = source;
        await image.LoadingTask;
        Assert.Equal(96, image.CachedImage!.Frames[0].Width);
        Assert.Equal(1, SkUiImageLoader.LoadCount - loads);

        source.Color = Colors.Blue; // a theme change: one load, no step through the file's size
        await image.LoadingTask;
        Assert.Equal(96, image.CachedImage!.Frames[0].Width);
        Assert.Equal(SKColors.Blue, Pixel(image.CachedImage, 6, 48));
        Assert.Equal(2, SkUiImageLoader.LoadCount - loads);

        SkUiTestHelpers.Arrange(image, 96, 96);
        await image.LoadingTask;
        Assert.Equal(2, SkUiImageLoader.LoadCount - loads);
    }

    [Fact]
    public async Task A_core_node_does_too()
    {
        var path = File(Icon);
        var core = new SkUiCoreImage();
        core.Measure(96, 96);
        core.Arrange(new Rect(0, 0, 96, 96));
        var loads = SkUiImageLoader.LoadCount;

        core.SetSource(SkUiImageSource.FromVector(path, Colors.Red));
        await core.LoadingTask;

        Assert.Equal(96, core.CachedImage!.Frames[0].Width);
        Assert.Equal(1, SkUiImageLoader.LoadCount - loads);
    }

    [Fact]
    public async Task Each_control_that_tracks_a_source_reloads_for_a_new_color()
    {
        var source = new SkUiVectorImageSource { Source = File(Icon), Color = Colors.Red, Size = 24 };
        var image = new SkUiImage { Source = source };
        var imageButton = new SkUiImageButton { Source = source };
        var button = new SkUiButton { ImageSource = source };
        var avatar = new SkUiAvatarView { ImageSource = source };
        await Task.WhenAll(image.LoadingTask, imageButton.LoadingTask, button.ImageLoadingTask, avatar.ImageLoadingTask);
        Assert.Equal(SKColors.Red, Pixel(image.CachedImage!, 6, 12));
        Assert.Equal(SKColors.Red, Pixel(button.CachedImage!, 6, 12));

        source.Color = Colors.Blue;
        await Task.WhenAll(image.LoadingTask, imageButton.LoadingTask, button.ImageLoadingTask, avatar.ImageLoadingTask);

        Assert.Equal(SKColors.Blue, Pixel(image.CachedImage!, 6, 12));
        Assert.Equal(SKColors.Blue, Pixel(imageButton.CachedImage!, 6, 12));
        Assert.Equal(SKColors.Blue, Pixel(button.CachedImage!, 6, 12));
        Assert.Equal(SKColors.Blue, Pixel(avatar.CachedImage!, avatar.CachedImage!.Frames[0].Width / 4, avatar.CachedImage.Frames[0].Height / 2));

        source.Source = File(Wide);
        await image.LoadingTask;
        Assert.Equal(new Size(24, 12), image.ImageSize);
    }

    [Fact]
    public async Task A_slider_thumb_reloads_for_a_new_color()
    {
        var source = new SkUiVectorImageSource { Source = File(Wide), Color = Colors.Red, Size = 24 };
        var slider = new SkUiSlider { Value = 0.5, MinimumTrackColor = Colors.Transparent, MaximumTrackColor = Colors.Transparent, ThumbImageSource = source };
        await WaitForThumb(slider, SKColors.Red);

        source.Color = Colors.Blue;

        await WaitForThumb(slider, SKColors.Blue);
    }

    private static async Task WaitForThumb(SkUiSlider slider, SKColor color)
    {
        SKColor Center()
        {
            SkUiTestHelpers.Arrange(slider, 100, 32);
            using var bitmap = new SKBitmap(100, 32);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);
            slider.Paint(canvas);
            return bitmap.GetPixel(50, 16);
        }
        for (var attempt = 0; attempt < 200 && Center() != color; attempt++)
            await Task.Delay(10);
        Assert.Equal(color, Center());
    }

    [Fact]
    public async Task A_theme_change_recolors_it()
    {
        var previous = Application.Current;
        var app = new Application { UserAppTheme = AppTheme.Light };
        try
        {
            var source = new SkUiVectorImageSource { Source = File(Icon) };
            source.SetAppThemeColor(SkUiVectorImageSource.ColorProperty, Colors.Red, Colors.Blue);
            var image = new SkUiImage { Source = source };
            var page = new ContentPage { Content = image, Parent = app }; // the theme comes down the element tree
            await image.LoadingTask;
            Assert.Equal(SKColors.Red, Pixel(image.CachedImage!, 6, 12));

            app.UserAppTheme = AppTheme.Dark;
            await image.LoadingTask;

            Assert.Equal(SKColors.Blue, Pixel(image.CachedImage!, 6, 12));
            Assert.Same(app, page.Parent);
        }
        finally
        {
            Application.Current = previous;
        }
    }

    [Fact]
    public async Task A_dynamic_resource_on_Color_follows_the_view()
    {
        var source = new SkUiVectorImageSource { Source = File(Icon) };
        source.SetDynamicResource(SkUiVectorImageSource.ColorProperty, "IconColor");
        var image = new SkUiImage();
        image.Resources["IconColor"] = Colors.Red;
        image.Source = source;
        await image.LoadingTask;
        Assert.Equal(SKColors.Red, Pixel(image.CachedImage!, 6, 12));

        image.Resources["IconColor"] = Colors.Blue;
        await image.LoadingTask;

        Assert.Equal(SKColors.Blue, Pixel(image.CachedImage!, 6, 12));
    }

    [Fact]
    public async Task A_binding_on_Color_resolves_against_the_view()
    {
        var source = new SkUiVectorImageSource { Source = File(Icon) };
        source.SetBinding(SkUiVectorImageSource.ColorProperty, static (ColorHolder holder) => holder.Tint);
        var image = new SkUiImage { BindingContext = new ColorHolder(Colors.Red), Source = source };
        await image.LoadingTask;

        Assert.Equal(SKColors.Red, Pixel(image.CachedImage!, 6, 12));
    }

    internal sealed record ColorHolder(Color Tint);

    [Fact]
    public async Task A_PNG_through_a_vector_source_shows_as_it_is()
    {
        var path = Path.Combine(_testDirectory, Key() + ".png");
        await System.IO.File.WriteAllBytesAsync(path, ImageLoadingTests.Png(SKColors.Red));

        var core = new SkUiCoreImage().SetSource(SkUiImageSource.FromVector(path, Colors.Blue, 96));
        await core.LoadingTask;

        Assert.Null(core.LoadError);
        Assert.Equal(new Size(40, 20), core.ImageSize);
        Assert.Equal(SKColors.Red, Pixel(core.CachedImage!, 20, 10));
    }

    [Fact]
    public async Task What_cannot_be_drawn_is_a_load_error()
    {
        var image = new SkUiImage { Source = new SkUiVectorImageSource { Source = File(Gradient), Color = Colors.Red } };
        await image.LoadingTask;

        Assert.Contains("linearGradient", image.LoadError!.Message);
    }

    [Fact]
    public void FromMaui_maps_it()
    {
        var mapped = SkUiImageSource.FromMaui(new SkUiVectorImageSource { Source = "check.svg", Color = Colors.Red, Size = 24 });

        Assert.Equal(SkUiImageSource.FromVector("check.svg", Colors.Red, 24), mapped);
        Assert.Null(SkUiImageSource.FromMaui(new SkUiVectorImageSource()));
        Assert.Null(SkUiImageSource.FromMaui(new SkUiVectorImageSource { Source = " " }));
        Assert.Equal(SkUiImageSource.FromVector(SkUiImageSource.FromUri(new Uri("https://images.test/a.svg"))),
            SkUiImageSource.FromMaui(new SkUiVectorImageSource { Source = "https://images.test/a.svg" }));
        Assert.True(SkUiMauiImageSources.AffectsImage(nameof(SkUiVectorImageSource.Source)));
        Assert.True(SkUiMauiImageSources.AffectsImage(nameof(SkUiVectorImageSource.Color)));
        Assert.True(SkUiMauiImageSources.AffectsImage(nameof(SkUiVectorImageSource.Size)));
    }

    [Fact]
    public void A_vector_of_a_vector_is_one_and_the_last_settings_win()
    {
        var inner = SkUiImageSource.FromVector("check.svg", Colors.Red, 24);

        Assert.Equal(SkUiImageSource.FromVector("check.svg", Colors.Blue, 48), SkUiImageSource.FromVector(inner, Colors.Blue, 48));
    }

    [Fact]
    public void Invalid_arguments_throw_or_are_ignored()
    {
        Assert.Throws<ArgumentNullException>(() => SkUiImageSource.FromVector((SkUiImageSource)null!));
        Assert.Throws<ArgumentNullException>(() => SkUiImageSource.FromVector((string)null!));
        Assert.Throws<ArgumentException>(() => SkUiImageSource.FromVector(" "));
        Assert.Throws<ArgumentException>(() => SkUiImageSource.FromVector(SkUiImageSource.FromFont("A")));
        Assert.Throws<ArgumentOutOfRangeException>(() => SkUiImageSource.FromVector("check.svg", size: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SkUiImageSource.FromVector("check.svg", size: double.NaN));
        Assert.Throws<ArgumentException>(() => SkUiImageSource.FromVectorText(" "));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkUiVectorImageSource().SetSize(-1));
        var source = new SkUiVectorImageSource { Size = 24 };
        source.Size = -1; // ignored with a warning, as MAUI does
        Assert.Equal(24, source.Size);
    }

    private sealed class Handler(byte[] body) : HttpMessageHandler
    {
        private int _requests;

        public int Requests => Volatile.Read(ref _requests);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requests);
            await Task.Delay(20, cancellationToken); // not inline: a load reports progress once it has started
            var content = new ByteArrayContent(body);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/svg+xml");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }
}
