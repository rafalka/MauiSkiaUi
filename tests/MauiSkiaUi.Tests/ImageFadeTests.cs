using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// I3: a loaded image fades in on the render thread over the placeholder it replaces (<c>FadeAnimationEnabled</c>), on
/// both layers, and ends on the UI clock; cache hits, images loaded before the view was drawn and reduced motion do not
/// fade. Runs with the image cache tests: it fills and clears the process-wide cache.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class ImageFadeTests : IDisposable
{
    private const int Width = 40;
    private const int Height = 20;
    private const int LongFade = 10_000; // the frames the test draws stay at its start
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "skui-fade-" + Guid.NewGuid().ToString("N"));

    public ImageFadeTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        SkUiImageCache.ClearMemoryCache();
        try { Directory.Delete(_directory, recursive: true); }
        catch (IOException) { }
    }

    private async Task<string> CachedFile(SKColor color)
    {
        var path = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".png");
        await File.WriteAllBytesAsync(path, ImageLoadingTests.Png(color, Width, Height));
        var warm = new SkUiCoreImage().SetSourceFile(path);
        await warm.LoadingTask; // in the memory cache from now on
        return path;
    }

    /// <summary>Draws the committed frames and returns the pixel in the middle of the image.</summary>
    private static SKColor Draw(SkUiFrameRenderer renderer, Queue<Action> frames)
    {
        while (frames.TryDequeue(out var frame))
            frame();
        using var bitmap = new SKBitmap(Width, Height);
        using var canvas = new SKCanvas(bitmap);
        renderer.Replay(canvas, bitmap.Info);
        return bitmap.GetPixel(Width / 2, Height / 2);
    }

    private static (SkUiFrameRenderer Renderer, Queue<Action> Frames) Show(SkUiView root)
    {
        SkUiTestHelpers.Arrange(root, Width, Height);
        var frames = new Queue<Action>();
        var renderer = new SkUiFrameRenderer(root, frames.Enqueue, () => { }, () => { });
        renderer.RequestFrame();
        Draw(renderer, frames); // drawn once: the view's state changes animate from now on
        return (renderer, frames);
    }

    [Fact]
    public async Task ACachedImageFadesInWhenAskedAndShowsWhenTheFadeEnds()
    {
        var red = await CachedFile(SKColors.Red);
        var image = new SkUiImage { FadeAnimationEnabled = true, FadeAnimationForCachedImages = true, FadeAnimationDuration = LongFade };
        var (renderer, frames) = Show(image);
        using (renderer)
        {
            image.Source = ImageSource.FromFile(red);
            Assert.True(image.IsFadingIn);
            Assert.True(Draw(renderer, frames).Alpha < 64); // the compositor draws it nearly transparent at first

            image.AnimationClock.Tick(image.AnimationClock.FrameTime + TimeSpan.FromMilliseconds(LongFade + 1));
            Assert.False(image.IsFadingIn);
            Assert.Equal(SKColors.Red, Draw(renderer, frames)); // recorded in the content again, no drawable
        }
    }

    [Fact]
    public async Task AnImageFadesInOverItsPlaceholderWhichGoesWhenTheFadeEnds()
    {
        using var _ = SkUiTestHelpers.UseTestDispatcher(); // the load completes on the dispatcher, as in an app
        var green = await CachedFile(SKColors.Lime);
        var gate = new TaskCompletionSource();
        var bytes = ImageLoadingTests.Png(SKColors.Red, Width, Height);
        var image = new SkUiImage { FadeAnimationEnabled = true, FadeAnimationDuration = LongFade, LoadingPlaceholder = ImageSource.FromFile(green) };
        var (renderer, frames) = Show(image);
        using (renderer)
        {
            image.Source = ImageSource.FromStream(async token => { await gate.Task.WaitAsync(token); return new MemoryStream(bytes); });
            Assert.Equal(SKColors.Lime, Draw(renderer, frames));

            gate.SetResult();
            await image.LoadingTask;
            Assert.True(image.IsFadingIn);
            Assert.False(image.IsShowingPlaceholder);
            var crossFade = Draw(renderer, frames);
            Assert.True(crossFade.Green > 192 && crossFade.Red < 64, crossFade.ToString()); // the placeholder, under the image fading in

            image.AnimationClock.StopAll(); // the page closed: the fade ends at once
            Assert.False(image.IsFadingIn);
            Assert.Equal(SKColors.Red, Draw(renderer, frames));
        }
    }

    [Fact]
    public async Task CacheHitsImagesLoadedBeforeTheFirstFrameAndReducedMotionShowAtOnce()
    {
        var red = await CachedFile(SKColors.Red);

        var cached = new SkUiImage { FadeAnimationEnabled = true }; // cached images do not fade by default
        var (renderer, frames) = Show(cached);
        using (renderer)
        {
            cached.Source = ImageSource.FromFile(red);
            Assert.False(cached.IsFadingIn);
            Assert.Equal(SKColors.Red, Draw(renderer, frames));
        }

        var notDrawn = new SkUiImage { FadeAnimationEnabled = true, FadeAnimationForCachedImages = true, Source = ImageSource.FromFile(red) };
        Assert.False(notDrawn.IsFadingIn); // set before the page appears: shown at once

        var reduced = new SkUiImage { FadeAnimationEnabled = true, FadeAnimationForCachedImages = true };
        (renderer, frames) = Show(reduced);
        var wasReduced = SkUiMotion.ReduceMotion;
        SkUiMotion.ReduceMotion = true;
        try
        {
            using (renderer)
            {
                reduced.Source = ImageSource.FromFile(red);
                Assert.False(reduced.IsFadingIn);
            }
        }
        finally
        {
            SkUiMotion.ReduceMotion = wasReduced;
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => new SkUiImage().SetFadeAnimation(true, -1));
        var invalid = new SkUiImage { FadeAnimationDuration = -5 }; // ignored, as MAUI ignores invalid values
        Assert.Null(invalid.FadeAnimationDuration);
    }

    [Fact]
    public async Task ACoreImageFadesInAndAnImageReplacingAShownOneDoesNot()
    {
        var red = await CachedFile(SKColors.Red);
        var blue = await CachedFile(SKColors.Blue);
        var core = new SkUiCoreImage().SetFadeAnimation(true, LongFade).SetFadeAnimationForCachedImages(true);
        var host = new SkUiCoreHost().SetContent(core);
        var (renderer, frames) = Show(host);
        using (renderer)
        {
            core.SetSourceFile(red);
            Assert.True(core.IsFadingIn);
            Assert.True(Draw(renderer, frames).Alpha < 64);

            core.SetSourceFile(blue); // replaces the shown image: no fade, and the old one's ends
            Assert.False(core.IsFadingIn);
            Assert.Equal(SKColors.Blue, Draw(renderer, frames));

            core.Clear();
            core.SetSourceFile(red); // nothing shown: fades in again
            Assert.True(core.IsFadingIn);
            core.Clear();
            Assert.False(core.IsFadingIn);
            Assert.Equal(0, Draw(renderer, frames).Alpha);
        }
    }
}
