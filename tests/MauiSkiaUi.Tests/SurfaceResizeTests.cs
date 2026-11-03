using MauiSkiaUi.Rendering;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// A surface resized before its root's new layout is committed keeps drawing the root at the scale it was drawn at, against the
/// edge the root keeps, instead of stretching the old layout over the new size.
/// </summary>
public class SurfaceResizeTests
{
    private const int Width = 40;
    private const int Height = 100;
    private const int Band = 10;

    [Fact]
    public void ASurfaceResizedAheadOfTheLayoutKeepsTheRootsScaleAgainstTheTop()
    {
        var (root, queue) = Root();
        using var renderer = Present(root, queue);
        Draw(renderer, Width, Height).Dispose();

        using var shorter = Draw(renderer, Width, 60);
        Assert.Equal(SKColors.Red, shorter.GetPixel(20, 5));
        // Stretched, the bottom band would end the surface; kept at its scale, it is below it.
        Assert.Equal(SKColors.White, shorter.GetPixel(20, 55));
    }

    [Fact]
    public void ARootThatKeepsItsBottomIsDrawnAgainstTheSurfacesBottom()
    {
        var (root, queue) = Root();
        root.SurfaceKeepsBottom = true;
        using var renderer = Present(root, queue);
        Draw(renderer, Width, Height).Dispose();

        using var shorter = Draw(renderer, Width, 60);
        Assert.Equal(SKColors.Blue, shorter.GetPixel(20, 55));
        Assert.Equal(SKColors.White, shorter.GetPixel(20, 5));

        using var taller = Draw(renderer, Width, 140);
        Assert.Equal(SKColors.Blue, taller.GetPixel(20, 135));
        Assert.Equal(SKColors.Red, taller.GetPixel(20, 45));
    }

    [Fact]
    public void TheRootFillsTheSurfaceAgainOnceItsLayoutIsCommitted()
    {
        var (root, queue) = Root();
        using var renderer = Present(root, queue);
        Draw(renderer, Width, Height).Dispose();
        Draw(renderer, Width, 60).Dispose();

        SkUiTestHelpers.Arrange(root, Width, 60);
        renderer.RequestFrame();
        while (queue.TryDequeue(out var action))
            action();
        using var laidOut = Draw(renderer, Width, 60);
        Assert.Equal(SKColors.Red, laidOut.GetPixel(20, 5));
        Assert.Equal(SKColors.Blue, laidOut.GetPixel(20, 55));
    }

    [Fact]
    public void ANewPixelDensityScalesTheRootAtOnce()
    {
        var (root, queue) = Root();
        using var renderer = Present(root, queue);
        Draw(renderer, Width, Height).Dispose();

        using var denser = Draw(renderer, Width * 2, Height * 2);
        Assert.Equal(SKColors.Red, denser.GetPixel(40, 10));
        Assert.Equal(SKColors.Blue, denser.GetPixel(40, 190));
    }

    [Fact]
    public void ARootThatNeverFollowsTheSurfaceFillsItAfterTheHeldFrames()
    {
        var (root, queue) = Root();
        using var renderer = Present(root, queue);
        Draw(renderer, Width, Height).Dispose();

        for (var frame = 0; frame < SkUiCompositor.MaxHeldFrames; frame++)
            Draw(renderer, Width, 60).Dispose();
        using var stretched = Draw(renderer, Width, 60);
        Assert.Equal(SKColors.Blue, stretched.GetPixel(20, 57));
        using var next = Draw(renderer, Width, 60);
        Assert.Equal(SKColors.Blue, next.GetPixel(20, 57));
    }

    [Fact]
    public void ARootLaidOutBeforeItsSurfaceIsResizedFillsIt()
    {
        // A platform that resizes the view before its buffer (Android's texture view) scales the buffer to the view.
        var (root, queue) = Root();
        using var renderer = Present(root, queue);
        Draw(renderer, Width, Height).Dispose();

        SkUiTestHelpers.Arrange(root, Width, 60);
        renderer.RequestFrame();
        while (queue.TryDequeue(out var action))
            action();
        using var filled = Draw(renderer, Width, Height);
        Assert.Equal(SKColors.Red, filled.GetPixel(20, 5));
        Assert.Equal(SKColors.Blue, filled.GetPixel(20, 95));

        using var resized = Draw(renderer, Width, 60);
        Assert.Equal(SKColors.Red, resized.GetPixel(20, 5));
        Assert.Equal(SKColors.Blue, resized.GetPixel(20, 55));
    }

    /// <summary>A white root with a red band at its top and a blue one at its bottom.</summary>
    private static (SkUiBox Root, Queue<Action> Queue) Root()
    {
        var root = new SkUiBox { Color = Colors.White };
        root.SetPaintOverlay(canvas =>
        {
            using var paint = new SKPaint();
            paint.Color = SKColors.Red;
            canvas.DrawRect(0, 0, (float)root.Width, Band, paint);
            paint.Color = SKColors.Blue;
            canvas.DrawRect(0, (float)root.Height - Band, (float)root.Width, Band, paint);
        });
        SkUiTestHelpers.Arrange(root, Width, Height);
        return (root, new Queue<Action>());
    }

    private static SkUiFrameRenderer Present(SkUiBox root, Queue<Action> queue)
    {
        var renderer = new SkUiFrameRenderer(root, queue.Enqueue, () => { }, () => { });
        renderer.RequestFrame();
        queue.Dequeue()();
        return renderer;
    }

    private static SKBitmap Draw(SkUiFrameRenderer renderer, int width, int height)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        renderer.Replay(canvas, bitmap.Info);
        return bitmap;
    }
}
