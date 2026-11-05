using MauiSkiaUi.Rendering;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// While a root's layout and its surface's size disagree (either can change first), the root is drawn at the scale it was last drawn
/// at, against the edge it keeps, instead of being stretched over the surface.
/// </summary>
public class SurfaceResizeTests
{
    private const int Width = 40;
    private const int Height = 100;
    private const int Band = 10;

    [Theory]
    [InlineData(double.NaN, 100, 292)] // first layout
    [InlineData(0, 100, 292)]
    [InlineData(292, 150, 292)] // within the slack: the swap chain stays
    [InlineData(292, 196, 292)]
    [InlineData(292, 197, 581)] // running out: changed once, with more slack
    [InlineData(1000, 232, 1000)]
    [InlineData(1000, 231, 615)] // far too long: shorter, still longer than the slot
    [InlineData(292, 0, 0)]
    public void AWindowsPanelKeepsSlackAroundItsSlot(double current, double slot, double expected) =>
        Assert.Equal(expected, SkUiSurfaceSlack.PanelLength(current, slot));

    [Fact]
    public void AWindowsPanelSettlesDuringADragBackAndForth()
    {
        var panel = SkUiSurfaceSlack.PanelLength(double.NaN, 150);
        var changes = 0;
        for (var sweep = 0; sweep < 20; sweep++)
            for (var step = 0; step <= 44; step++)
            {
                // 150 to 370 and back, 5 DIPs a step.
                var slot = 150 + 5 * (sweep % 2 == 0 ? step : 44 - step);
                var next = SkUiSurfaceSlack.PanelLength(panel, slot);
                if (Math.Abs(next - panel) > 0.5)
                    changes++;
                panel = next;
            }
        Assert.Equal(1, changes);
    }

    [Fact]
    public void TheClearColorFillsACanvasLargerThanTheFrame()
    {
        var (root, queue) = Root();
        root.BackgroundColor = Colors.Lime;
        using var renderer = Present(root, queue);
        using var surface = SKSurface.Create(new SKImageInfo(Width + 20, Height + 20, SKColorType.Rgba8888, SKAlphaType.Premul));
        renderer.Render(surface.Canvas, new SKImageInfo(Width, Height));
        surface.Canvas.Flush();
        using var image = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        Assert.Equal(SKColors.Lime, bitmap.GetPixel(Width + 10, Height + 10));
    }

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

        Layout(root, renderer, queue, 60);
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
    public void ARootLaidOutBeforeItsSurfaceIsResizedKeepsItsScale()
    {
        var (root, queue) = Root();
        using var renderer = Present(root, queue);
        Draw(renderer, Width, Height).Dispose();

        Layout(root, renderer, queue, 60);
        using var held = Draw(renderer, Width, Height);
        Assert.Equal(SKColors.Red, held.GetPixel(20, 5));
        // Stretched, the bottom band would end the surface; kept at its scale, it ends the root's new height.
        Assert.Equal(SKColors.Blue, held.GetPixel(20, 55));
        Assert.Equal(0, held.GetPixel(20, 95).Alpha);

        using var resized = Draw(renderer, Width, 60);
        Assert.Equal(SKColors.Red, resized.GetPixel(20, 5));
        Assert.Equal(SKColors.Blue, resized.GetPixel(20, 55));
    }

    [Fact]
    public void ARootThatKeepsItsBottomLaidOutBeforeItsSurfaceIsResizedIsDrawnAgainstTheBottom()
    {
        var (root, queue) = Root();
        root.SurfaceKeepsBottom = true;
        using var renderer = Present(root, queue);
        Draw(renderer, Width, Height).Dispose();

        Layout(root, renderer, queue, 60);
        using var held = Draw(renderer, Width, Height);
        Assert.Equal(0, held.GetPixel(20, 5).Alpha);
        Assert.Equal(SKColors.Red, held.GetPixel(20, 45));
        Assert.Equal(SKColors.Blue, held.GetPixel(20, 95));
    }

    [Fact]
    public void ARootLaidOutBeforeASurfaceThatScalesFramesToTheViewFillsIt()
    {
        // Android's texture view: the view already has the root's size and scales the old buffer to it.
        var (root, queue) = Root();
        using var renderer = Present(root, queue);
        renderer.Compositor.ScalesFrameToView = true;
        Draw(renderer, Width, Height).Dispose();

        Layout(root, renderer, queue, 60);
        using var filled = Draw(renderer, Width, Height);
        Assert.Equal(SKColors.Red, filled.GetPixel(20, 5));
        Assert.Equal(SKColors.Blue, filled.GetPixel(20, 95));

        using var resized = Draw(renderer, Width, 60);
        Assert.Equal(SKColors.Red, resized.GetPixel(20, 5));
        Assert.Equal(SKColors.Blue, resized.GetPixel(20, 55));
    }

    [Fact]
    public void TouchesFollowTheRootHeldAgainstTheTop()
    {
        var (root, queue) = Root();
        using var renderer = Present(root, queue);
        Draw(renderer, Width, Height).Dispose();
        Draw(renderer, Width, 60).Dispose();

        // Drawn at its scale, not stretched: a pixel near the surface's bottom is the root's white middle, not its bottom band.
        AssertPoint(new Point(20, 55), renderer.PixelsToRoot(new Point(20, 55)));
        AssertPoint(new Point(20, 55), renderer.DipsToRoot(new Point(20, 55)));
    }

    [Fact]
    public void TouchesFollowTheRootHeldAgainstTheBottom()
    {
        var (root, queue) = Root();
        root.SurfaceKeepsBottom = true;
        using var renderer = Present(root, queue);
        Draw(renderer, Width, Height).Dispose();

        // Laid out shorter before the surface is resized: drawn 40 pixels down, its bottom band at the surface's bottom.
        Layout(root, renderer, queue, 60);
        Draw(renderer, Width, Height).Dispose();
        AssertPoint(new Point(20, 55), renderer.PixelsToRoot(new Point(20, 95)));
        AssertPoint(new Point(20, 55), renderer.DipsToRoot(new Point(20, 95)));

        // Resized: surface and root agree again.
        Draw(renderer, Width, 60).Dispose();
        AssertPoint(new Point(20, 55), renderer.PixelsToRoot(new Point(20, 55)));
        AssertPoint(new Point(20, 55), renderer.DipsToRoot(new Point(20, 55)));
    }

    [Fact]
    public void TouchesOnASurfaceThatScalesFramesToTheViewFollowTheView()
    {
        var (root, queue) = Root();
        using var renderer = Present(root, queue);
        renderer.Compositor.ScalesFrameToView = true;
        Draw(renderer, Width, Height).Dispose();

        // Filled: the buffer's pixels stretch over the root, and the view already has the root's size.
        Layout(root, renderer, queue, 60);
        Draw(renderer, Width, Height).Dispose();
        AssertPoint(new Point(20, 57), renderer.PixelsToRoot(new Point(20, 95)));
        AssertPoint(new Point(20, 57), renderer.DipsToRoot(new Point(20, 57)));
    }

    private static void AssertPoint(Point expected, Point? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.X, actual.Value.X, PointTolerance);
        Assert.Equal(expected.Y, actual.Value.Y, PointTolerance);
    }

    private const double PointTolerance = 0.01;

    /// <summary>Lays the root out to <paramref name="height"/> and commits the frame.</summary>
    private static void Layout(SkUiBox root, SkUiFrameRenderer renderer, Queue<Action> queue, int height)
    {
        SkUiTestHelpers.Arrange(root, Width, height);
        renderer.RequestFrame();
        while (queue.TryDequeue(out var action))
            action();
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
