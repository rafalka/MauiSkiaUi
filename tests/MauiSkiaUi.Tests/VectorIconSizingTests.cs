using System.Text;
using MauiSkiaUi.Core;
using MauiSkiaUi.Rendering;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>1.7 L2: a vector is drawn again at the size its view shows it, on both layers. Headless, one pixel per DIP.</summary>
[Collection(GlobalStateCollection.Name)]
public sealed class VectorIconSizingTests : IDisposable
{
    // The left half red: a bitmap scaled up would blur the edge in the middle.
    private const string Icon = """<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24"><rect width="12" height="24" fill="#f00"/></svg>""";
    private const string Wide = """<svg xmlns="http://www.w3.org/2000/svg" width="48" height="24"><rect width="48" height="24" fill="#00f"/></svg>""";
    private const int LongFade = 60_000;

    private readonly string _testDirectory = Path.Combine(Path.GetTempPath(), "skui-vector-size-" + Guid.NewGuid().ToString("N"));

    public VectorIconSizingTests()
    {
        SkUiImageCache.ClearMemoryCache();
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        SkUiImageCache.ClearMemoryCache();
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

    /// <summary>One image view of either layer, as these tests use it.</summary>
    private sealed record View(Func<SkUiCachedImage?> Entry, Action<double, double> Arrange, Func<Task> Loading, Action<Aspect> SetAspect)
    {
        public (int, int) Pixels => (Entry()!.Frames[0].Width, Entry()!.Frames[0].Height);

        /// <summary>Lays out as a surface does after each load, until nothing more loads.</summary>
        public async Task Settle(double width, double height)
        {
            for (var pass = 0; pass < 4; pass++)
            {
                Arrange(width, height);
                await Loading();
            }
        }
    }

    private static View Show(bool core, string path, Aspect aspect = Aspect.AspectFit)
    {
        if (core)
        {
            var node = new SkUiCoreImage().SetAspect(aspect).SetSourceFile(path);
            return new View(() => node.CachedImage, (width, height) =>
            {
                node.Measure(width, height);
                node.Arrange(new Rect(0, 0, width, height));
            }, () => node.LoadingTask, value => node.SetAspect(value));
        }
        var image = new SkUiImage { Aspect = aspect, Source = path };
        return new View(() => image.CachedImage, (width, height) => SkUiTestHelpers.Arrange(image, width, height),
            () => image.LoadingTask, value => image.Aspect = value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_view_of_the_icons_size_loads_once(bool core)
    {
        var path = File(Icon);
        var loads = SkUiImageLoader.LoadCount;
        var view = Show(core, path);
        await view.Loading();

        await view.Settle(24, 24);

        Assert.Equal((24, 24), view.Pixels);
        Assert.Equal(1, SkUiImageLoader.LoadCount - loads);
    }

    [Theory]
    [InlineData(false, 96)]
    [InlineData(false, 25.5)]
    [InlineData(false, 12)]
    [InlineData(true, 96)]
    [InlineData(true, 25.5)]
    [InlineData(true, 12)]
    public async Task It_draws_at_the_arranged_size_in_exact_pixels(bool core, double size)
    {
        var view = Show(core, File(Icon));
        await view.Loading();

        await view.Settle(size, size);

        var pixels = (int)Math.Ceiling(size);
        Assert.Equal((pixels, pixels), view.Pixels);
        Assert.True(view.Entry()!.IsVector);
        Assert.Equal(new Size(24, 24), view.Entry()!.Size); // what measure uses stays the file's
    }

    [Fact]
    public async Task The_redraw_is_sharp()
    {
        var view = Show(core: true, File(Icon));
        await view.Loading();
        await view.Settle(96, 96);

        using var pixels = SKBitmap.FromImage(view.Entry()!.Frames[0]);

        Assert.Equal(SKColors.Red, pixels.GetPixel(47, 48));
        Assert.Equal(0, pixels.GetPixel(48, 48).Alpha);
    }

    [Theory]
    [InlineData(25, 1)] // one pixel off: not worth a load
    [InlineData(26, 2)]
    public async Task A_difference_of_one_pixel_does_not_load(double size, int expected)
    {
        var path = File(Icon);
        var loads = SkUiImageLoader.LoadCount;
        var view = Show(core: true, path);
        await view.Loading();

        await view.Settle(size, size);

        Assert.Equal(expected, SkUiImageLoader.LoadCount - loads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task It_follows_a_view_that_grows_and_shrinks(bool core)
    {
        var view = Show(core, File(Icon));
        await view.Loading();

        await view.Settle(96, 96);
        Assert.Equal((96, 96), view.Pixels);
        await view.Settle(40, 40);
        Assert.Equal((40, 40), view.Pixels);
        await view.Settle(24, 24);
        Assert.Equal((24, 24), view.Pixels);
    }

    [Theory]
    [InlineData(Aspect.AspectFit, 100, 50)]
    [InlineData(Aspect.AspectFill, 200, 100)]
    [InlineData(Aspect.Fill, 200, 100)]
    public async Task The_aspect_decides_the_pixels(Aspect aspect, int width, int height)
    {
        var view = Show(core: true, File(Wide), aspect);
        await view.Loading();

        await view.Settle(100, 100);

        Assert.Equal((width, height), view.Pixels);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Changing_the_aspect_draws_it_again(bool core)
    {
        var view = Show(core, File(Wide));
        await view.Loading();
        await view.Settle(100, 100);
        Assert.Equal((100, 50), view.Pixels);

        view.SetAspect(Aspect.AspectFill);
        await view.Settle(100, 100);

        Assert.Equal((200, 100), view.Pixels);
    }

    [Fact]
    public async Task A_centered_icon_is_not_drawn_again()
    {
        var path = File(Icon);
        var loads = SkUiImageLoader.LoadCount;
        var view = Show(core: false, path, Aspect.Center);
        await view.Loading();

        await view.Settle(96, 96);

        Assert.Equal((24, 24), view.Pixels);
        Assert.Equal(1, SkUiImageLoader.LoadCount - loads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Changing_the_aspect_to_center_draws_it_at_its_own_size(bool core)
    {
        var view = Show(core, File(Icon));
        await view.Loading();
        await view.Settle(12, 12);
        Assert.Equal((12, 12), view.Pixels);

        view.SetAspect(Aspect.Center);
        await view.Settle(12, 12);
        Assert.Equal((24, 24), view.Pixels);

        view.SetAspect(Aspect.AspectFit);
        await view.Settle(12, 12);
        Assert.Equal((12, 12), view.Pixels);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_bitmap_behind_a_vector_source_is_not_left_at_the_views_size(bool core)
    {
        var path = Path.Combine(_testDirectory, Key() + ".png");
        await System.IO.File.WriteAllBytesAsync(path, ImageLoadingTests.Png(SKColors.Red, 400, 200));
        View view;
        if (core)
        {
            var node = new SkUiCoreImage();
            node.Measure(50, 50);
            node.Arrange(new Rect(0, 0, 50, 50));
            node.SetSource(SkUiImageSource.FromVector(path, Colors.Blue));
            view = new View(() => node.CachedImage, (width, height) =>
            {
                node.Measure(width, height);
                node.Arrange(new Rect(0, 0, width, height));
            }, () => node.LoadingTask, value => node.SetAspect(value));
        }
        else
        {
            var image = new SkUiImage();
            SkUiTestHelpers.Arrange(image, 50, 50);
            image.Source = new SkUiVectorImageSource { Source = path, Color = Colors.Blue };
            view = new View(() => image.CachedImage, (width, height) => SkUiTestHelpers.Arrange(image, width, height),
                () => image.LoadingTask, value => image.Aspect = value);
        }
        await view.Loading();
        Assert.Equal((50, 25), view.Pixels); // bounded: the source said it is a vector

        await view.Settle(50, 50);

        Assert.Equal((400, 200), view.Pixels); // as any bitmap without bounds
    }

    [Fact]
    public async Task SetImage_after_a_sized_vector_stays()
    {
        var node = new SkUiCoreImage();
        node.Measure(96, 96);
        node.Arrange(new Rect(0, 0, 96, 96));
        node.SetSource(SkUiImageSource.FromVector(File(Icon), Colors.Red));
        await node.LoadingTask;
        Assert.True(node.CachedImage!.IsVector);
        using var bitmap = new SKBitmap(8, 8);
        bitmap.Erase(SKColors.Red);
        using var picture = SKImage.FromBitmap(bitmap);

        node.SetImage(picture, ownsImage: false);
        node.InvalidateMeasure();
        node.Measure(96, 96);
        node.Arrange(new Rect(0, 0, 96, 96));
        await node.LoadingTask;

        Assert.NotNull(node.CachedImage);
        Assert.False(node.CachedImage!.IsVector);
    }

    [Fact]
    public async Task Changing_the_aspect_with_DownsampleToViewSize_keeps_the_icon_shown()
    {
        var bytes = Encoding.UTF8.GetBytes(Wide);
        var gate = new TaskCompletionSource();
        gate.SetResult();
        var image = new SkUiImage
        {
            DownsampleToViewSize = true,
            CacheKey = Key(),
            Source = ImageSource.FromStream(async token =>
            {
                await gate.Task.WaitAsync(token);
                return new MemoryStream(bytes);
            })
        };
        for (var pass = 0; pass < 4; pass++)
        {
            SkUiTestHelpers.Arrange(image, 100, 100);
            await image.LoadingTask;
        }
        var shown = image.CachedImage;
        Assert.True(shown!.IsVector);

        gate = new TaskCompletionSource();
        image.Aspect = Aspect.AspectFill;
        SkUiTestHelpers.Arrange(image, 100, 100);
        Assert.Same(shown, image.CachedImage); // the load waits at the gate, and the icon stays until it is done
        gate.SetResult();
        await image.LoadingTask;
        Assert.Equal((200, 100), (image.CachedImage!.Frames[0].Width, image.CachedImage.Frames[0].Height));
    }

    [Fact]
    public async Task Leaving_center_with_DownsampleToViewSize_keeps_the_icon_shown_on_both_layers()
    {
        var path = File(Icon);
        var image = new SkUiImage { DownsampleToViewSize = true, Aspect = Aspect.Center, Source = path };
        var node = new SkUiCoreImage().SetDownsampleToViewSize(true).SetAspect(Aspect.Center).SetSourceFile(path);
        await Task.WhenAll(image.LoadingTask, node.LoadingTask);
        SkUiTestHelpers.Arrange(image, 96, 96);
        node.Measure(96, 96);
        node.Arrange(new Rect(0, 0, 96, 96));
        await Task.WhenAll(image.LoadingTask, node.LoadingTask);
        Assert.True(image.CachedImage!.IsVector);
        Assert.True(node.CachedImage!.IsVector);

        image.Aspect = Aspect.AspectFit;
        node.SetAspect(Aspect.AspectFit);
        Assert.NotNull(image.CachedImage);
        Assert.NotNull(node.CachedImage);

        for (var pass = 0; pass < 3; pass++)
        {
            SkUiTestHelpers.Arrange(image, 96, 96);
            node.Measure(96, 96);
            node.Arrange(new Rect(0, 0, 96, 96));
            await Task.WhenAll(image.LoadingTask, node.LoadingTask);
        }
        Assert.Equal(96, image.CachedImage!.Frames[0].Width);
        Assert.Equal(96, node.CachedImage!.Frames[0].Width);
    }

    [Fact]
    public async Task A_fade_finishes_before_a_bitmap_behind_a_vector_source_loads_without_bounds()
    {
        var path = Path.Combine(_testDirectory, Key() + ".png");
        await System.IO.File.WriteAllBytesAsync(path, ImageLoadingTests.Png(SKColors.Red, 400, 200));
        var bounds = new SkUiImage { WidthRequest = 50, HeightRequest = 50 };
        SkUiTestHelpers.Arrange(bounds, 50, 50);
        bounds.Source = new SkUiVectorImageSource { Source = path, Color = Colors.Blue }; // puts the bounded bitmap in the cache
        await bounds.LoadingTask;
        var image = new SkUiImage { FadeAnimationEnabled = true, FadeAnimationForCachedImages = true, FadeAnimationDuration = LongFade };
        SkUiTestHelpers.Arrange(image, 50, 50);
        var frames = new Queue<Action>();
        using var renderer = new SkUiFrameRenderer(image, frames.Enqueue, () => { }, () => { });
        renderer.RequestFrame();
        Run(frames);

        image.Source = new SkUiVectorImageSource { Source = path, Color = Colors.Blue };
        Assert.True(image.IsFadingIn);
        SkUiTestHelpers.Arrange(image, 50, 50);
        Assert.Equal(50, image.CachedImage!.Frames[0].Width);

        image.AnimationClock.Tick(image.AnimationClock.FrameTime + TimeSpan.FromMilliseconds(LongFade + 1));
        SkUiTestHelpers.Arrange(image, 50, 50);
        await image.LoadingTask;

        Assert.Equal(400, image.CachedImage!.Frames[0].Width);
    }

    [Fact]
    public async Task Explicit_bounds_win()
    {
        var image = new SkUiImage { DownsampleWidth = 48, DownsampleHeight = 48, Source = File(Icon) };
        await image.LoadingTask;
        var loads = SkUiImageLoader.LoadCount;

        for (var pass = 0; pass < 3; pass++)
        {
            SkUiTestHelpers.Arrange(image, 96, 96);
            await image.LoadingTask;
        }

        Assert.Equal(48, image.CachedImage!.Frames[0].Width);
        Assert.Equal(loads, SkUiImageLoader.LoadCount);
    }

    [Fact]
    public async Task DownsampleToViewSize_ends_in_exact_pixels_not_its_steps()
    {
        var image = new SkUiImage { DownsampleToViewSize = true, Source = File(Icon) };

        for (var pass = 0; pass < 4; pass++)
        {
            SkUiTestHelpers.Arrange(image, 20, 20);
            await image.LoadingTask;
        }

        Assert.Equal(20, image.CachedImage!.Frames[0].Width); // not 32, its step
    }

    [Fact]
    public async Task Views_of_one_size_share_entries()
    {
        var path = File(Icon);
        var loads = SkUiImageLoader.LoadCount;
        var first = Show(core: false, path);
        await first.Loading();
        await first.Settle(96, 96);
        Assert.Equal(2, SkUiImageLoader.LoadCount - loads); // the file's size, then the view's

        var second = Show(core: true, path);
        await second.Settle(96, 96);

        Assert.Same(first.Entry(), second.Entry());
        Assert.Equal(2, SkUiImageLoader.LoadCount - loads);
    }

    [Fact]
    public async Task The_shown_icon_stays_while_it_is_drawn_again_and_one_redraw_runs_at_a_time()
    {
        var bytes = Encoding.UTF8.GetBytes(Icon);
        var opens = 0;
        var gate = new TaskCompletionSource();
        gate.SetResult();
        var image = new SkUiImage
        {
            CacheKey = Key(),
            Source = ImageSource.FromStream(async token =>
            {
                Interlocked.Increment(ref opens);
                await gate.Task.WaitAsync(token);
                return new MemoryStream(bytes);
            })
        };
        await image.LoadingTask;
        var small = image.CachedImage;
        Assert.Equal(1, opens);

        // A size animation, a step per frame, while the first redraw has not finished.
        gate = new TaskCompletionSource();
        for (var size = 40; size <= 200; size += 10)
        {
            SkUiTestHelpers.Arrange(image, size, size);
            Assert.Same(small, image.CachedImage);
        }
        Assert.Equal(2, opens);

        gate.SetResult();
        await image.LoadingTask;
        SkUiTestHelpers.Arrange(image, 200, 200); // the finished redraw asked for this layout
        await image.LoadingTask;

        Assert.Equal(3, opens); // the last size, not each one
        Assert.Equal(200, image.CachedImage!.Frames[0].Width);
    }

    [Fact]
    public async Task A_redraw_that_fails_keeps_the_icon_and_is_not_repeated()
    {
        var opens = 0;
        var image = new SkUiImage
        {
            CacheKey = Key(),
            Source = ImageSource.FromStream(_ =>
                ++opens == 1 ? Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(Icon))) : throw new IOException("gone"))
        };
        await image.LoadingTask;
        var shown = image.CachedImage;

        for (var pass = 0; pass < 3; pass++)
        {
            SkUiTestHelpers.Arrange(image, 96, 96);
            await image.LoadingTask;
        }

        Assert.Same(shown, image.CachedImage);
        Assert.Null(image.LoadError);
        Assert.Equal(2, opens);
    }

    [Fact]
    public async Task A_new_source_is_compared_with_its_own_pixels()
    {
        var image = new SkUiImage { Source = File(Icon) };
        await image.LoadingTask;
        for (var pass = 0; pass < 3; pass++)
        {
            SkUiTestHelpers.Arrange(image, 96, 96);
            await image.LoadingTask;
        }
        Assert.Equal(96, image.CachedImage!.Frames[0].Width);

        image.Source = File(Wide);
        await image.LoadingTask;
        for (var pass = 0; pass < 3; pass++)
        {
            SkUiTestHelpers.Arrange(image, 96, 96);
            await image.LoadingTask;
        }

        Assert.Equal((96, 48), (image.CachedImage!.Frames[0].Width, image.CachedImage.Frames[0].Height));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_image_button_draws_inside_its_padding(bool core)
    {
        var path = File(Icon);
        var loads = SkUiImageLoader.LoadCount;
        View view;
        if (core)
        {
            var node = new SkUiCoreImageButton().SetPadding(new Thickness(12));
            node.SetSourceFile(path);
            view = new View(() => node.CachedImage, (width, height) =>
            {
                node.Measure(width, height);
                node.Arrange(new Rect(0, 0, width, height));
            }, () => node.LoadingTask, _ => { });
        }
        else
        {
            var button = new SkUiImageButton { Padding = new Thickness(12), Source = path };
            view = new View(() => button.CachedImage, (width, height) => SkUiTestHelpers.Arrange(button, width, height),
                () => button.LoadingTask, _ => { });
        }
        await view.Loading();

        await view.Settle(48, 48);

        Assert.Equal((24, 24), view.Pixels);
        Assert.Equal(1, SkUiImageLoader.LoadCount - loads);
    }

    [Fact]
    public async Task A_PNG_is_not_drawn_again()
    {
        var path = Path.Combine(_testDirectory, Key() + ".png");
        await System.IO.File.WriteAllBytesAsync(path, ImageLoadingTests.Png(SKColors.Red));
        var loads = SkUiImageLoader.LoadCount;
        var view = Show(core: true, path);
        await view.Loading();

        await view.Settle(200, 200);

        Assert.Equal((40, 20), view.Pixels);
        Assert.Equal(1, SkUiImageLoader.LoadCount - loads);
    }

    [Fact]
    public async Task A_fade_finishes_before_the_icon_is_drawn_again()
    {
        var path = File(Icon);
        var cached = new SkUiCoreImage().SetSourceFile(path);
        await cached.LoadingTask;
        var image = new SkUiImage { FadeAnimationEnabled = true, FadeAnimationForCachedImages = true, FadeAnimationDuration = LongFade };
        SkUiTestHelpers.Arrange(image, 96, 96);
        var frames = new Queue<Action>();
        using var renderer = new SkUiFrameRenderer(image, frames.Enqueue, () => { }, () => { });
        renderer.RequestFrame();
        Run(frames);

        image.Source = path; // a cache hit: shown at once, and it fades
        Assert.True(image.IsFadingIn);
        SkUiTestHelpers.Arrange(image, 96, 96);
        Run(frames);
        Assert.True(image.IsFadingIn);
        Assert.Equal(24, image.CachedImage!.Frames[0].Width);

        image.AnimationClock.Tick(image.AnimationClock.FrameTime + TimeSpan.FromMilliseconds(LongFade + 1));
        Assert.False(image.IsFadingIn);
        SkUiTestHelpers.Arrange(image, 96, 96);
        await image.LoadingTask;

        Assert.Equal(96, image.CachedImage!.Frames[0].Width);
        Assert.False(image.IsFadingIn);
    }

    [Fact]
    public async Task On_a_surface_the_frames_after_each_load_do_it()
    {
        var root = new SkUiVerticalStackLayout();
        var image = new SkUiImage { WidthRequest = 96, HeightRequest = 96 };
        var hosted = new SkUiCoreImage { Width = 96, Height = 96 };
        root.Add(image);
        root.Add(new SkUiCoreHost { WidthRequest = 96, HeightRequest = 96 }.SetContent(hosted));
        SkUiTestHelpers.Arrange(root, 200, 400);
        var frames = new Queue<Action>();
        using var renderer = new SkUiFrameRenderer(root, frames.Enqueue, () => { }, () => { });
        renderer.RequestFrame();
        Run(frames);
        var path = File(Icon);

        image.Source = path;
        hosted.SetSourceFile(path);
        for (var attempt = 0; attempt < 200 && !(image.CachedImage?.Frames[0].Width == 96 && hosted.CachedImage?.Frames[0].Width == 96); attempt++)
        {
            Run(frames);
            await Task.Delay(10);
        }

        Assert.Equal(96, image.CachedImage!.Frames[0].Width);
        Assert.Equal(96, hosted.CachedImage!.Frames[0].Width);
    }

    private static void Run(Queue<Action> frames)
    {
        while (frames.TryDequeue(out var frame))
            frame();
    }
}
