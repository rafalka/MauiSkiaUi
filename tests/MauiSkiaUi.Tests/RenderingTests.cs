using System.Runtime.CompilerServices;
using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>Retained compositor, selective recording, and render-thread animation guarantees.</summary>
public class RenderingTests
{
    [Fact]
    public async Task RenderThreadAnimationAdvancesWhileUiThreadIsStalled()
    {
        var box = new SkUiBox { Color = Colors.Red, WidthRequest = 20, HeightRequest = 20, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
        var root = new SkUiContentView { Background = Colors.White, Content = box };
        using var surface = new SkUiTestSurface(root, 60, 20);
        using var bitmap = new SKBitmap(60, 20);
        using var canvas = new SKCanvas(bitmap);
        surface.Frame(0);

        var moved = box.AnimateAsync(SkUiAnimatableProperty.TranslationX, 40, 100);
        surface.Renderer.PresentFrame();
        // From here on the UI queue is never pumped: only the render thread advances.
        surface.Renderer.Render(canvas, bitmap.Info, TimeSpan.FromMilliseconds(1000));
        Assert.Equal(SKColors.Red, bitmap.GetPixel(10, 10));
        surface.Renderer.Render(canvas, bitmap.Info, TimeSpan.FromMilliseconds(1200));
        Assert.Equal(SKColors.White, bitmap.GetPixel(10, 10));
        Assert.Equal(SKColors.Red, bitmap.GetPixel(50, 10));
        Assert.Equal(0, box.TranslationX);

        surface.PumpUi();
        Assert.True(await moved.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(40, box.TranslationX);
        // The acknowledged final value does not snap back or re-record on the next frame.
        var recorded = surface.RecordedPictures;
        Assert.Equal(SKColors.Red, surface.Frame(1300).GetPixel(50, 10));
        Assert.Equal(recorded, surface.RecordedPictures);
    }

    [Fact]
    public async Task FadeInFromZeroOpacityShowsContentWhileAnimating()
    {
        var box = new SkUiBox { Color = Colors.Red, WidthRequest = 20, HeightRequest = 20, Opacity = 0 };
        var root = new SkUiContentView { Content = box, Background = Colors.White };
        using var surface = new SkUiTestSurface(root, 40, 40);
        using var bitmap = new SKBitmap(40, 40);
        using var canvas = new SKCanvas(bitmap);
        Assert.Equal(SKColors.White, surface.Frame(0).GetPixel(10, 10));

        var fade = box.AnimateAsync(SkUiAnimatableProperty.Opacity, 1, 100);
        surface.Renderer.PresentFrame();
        // Render thread only: the content must already be recorded although the node was invisible.
        surface.Renderer.Render(canvas, bitmap.Info, TimeSpan.FromMilliseconds(1000));
        surface.Renderer.Render(canvas, bitmap.Info, TimeSpan.FromMilliseconds(1050));
        var mid = bitmap.GetPixel(10, 10);
        Assert.True(mid.Red == 255 && mid.Green is > 0 and < 255, $"half-faded red expected, got {mid}");
        surface.Renderer.Render(canvas, bitmap.Info, TimeSpan.FromMilliseconds(1200));
        Assert.Equal(SKColors.Red, bitmap.GetPixel(10, 10));
        surface.PumpUi();
        Assert.True(await fade.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Theory]
    [InlineData(false)] // queued on the node, never committed
    [InlineData(true)]  // committed, not yet applied by the render thread
    public async Task AnimateAsyncCompletesWhenTheSurfaceIsDisposedFirst(bool committed)
    {
        var box = new SkUiBox { Color = Colors.Red, WidthRequest = 20, HeightRequest = 20 };
        var root = new SkUiContentView { Content = box };
        var surface = new SkUiTestSurface(root, 40, 40);
        surface.Frame(0);
        var fade = box.AnimateAsync(SkUiAnimatableProperty.Opacity, 0, 100);
        if (committed)
            surface.Renderer.PresentFrame();
        surface.Dispose();
        surface.PumpUi();
        Assert.False(await fade.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, box.Opacity);
    }

    [Fact]
    public async Task AnimateAsyncCompletesWhenTheNodeIsDetachedBeforeCommit()
    {
        var box = new SkUiBox { Color = Colors.Red, WidthRequest = 20, HeightRequest = 20 };
        var root = new SkUiContentView { Content = box };
        using var surface = new SkUiTestSurface(root, 40, 40);
        surface.Frame(0);
        var move = box.AnimateAsync(SkUiAnimatableProperty.TranslationX, 10, 100);
        root.Content = null;
        surface.Frame(10);
        Assert.False(await move.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task SettingAnimatedPropertyCancelsRenderAnimation()
    {
        var box = new SkUiBox { Color = Colors.Red, WidthRequest = 20, HeightRequest = 20 };
        var root = new SkUiContentView { Content = box };
        using var surface = new SkUiTestSurface(root, 40, 40);
        surface.Frame(0);
        var fade = box.AnimateAsync(SkUiAnimatableProperty.Opacity, 0, 1000);
        surface.Frame(10);
        surface.Frame(500);
        box.Opacity = 0.8;
        surface.Frame(600);
        surface.Frame(700);
        Assert.False(await fade.WaitAsync(TimeSpan.FromSeconds(5)));
        // The UI's explicit value wins over the cancelled animation's last frame.
        Assert.Equal(0.8, box.Opacity);
        Assert.False(surface.NeedsFrame);
    }

    [Fact]
    public async Task ACancelledTokenStopsTheRenderAnimationWhereItIsOnBothLayers()
    {
        var box = new SkUiBox { Color = Colors.Red, WidthRequest = 20, HeightRequest = 20 };
        var node = new SkUiCoreBox().SetColor(Colors.Red);
        node.SetWidth(20).SetHeight(20);
        var root = new SkUiVerticalStackLayout { Children = { box, new SkUiCoreHost().SetContent(node) } };
        using var surface = new SkUiTestSurface(root, 40, 60);
        surface.Frame(0);

        using var cancellation = new CancellationTokenSource();
        var move = box.AnimateAsync(SkUiAnimatableProperty.TranslationX, 100, 1000, Easing.Linear, cancellation.Token);
        var coreMove = node.AnimateAsync(SkUiAnimatableProperty.TranslationX, 100, 1000, Easing.Linear, cancellation.Token);
        surface.Frame(10);
        surface.Frame(510);
        // The token holds the animations weakly: a collection while they run must not lose them.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        cancellation.Cancel();
        surface.Frame(600);
        surface.Frame(700);
        Assert.False(await move.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.False(await coreMove.WaitAsync(TimeSpan.FromSeconds(5)));
        // The last shown value, about half way, not the target.
        Assert.InRange(box.TranslationX, 30, 70);
        Assert.InRange(node.TranslationX, 30, 70);
        Assert.False(surface.NeedsFrame);

        // A token cancelled already starts nothing.
        Assert.False(await box.AnimateAsync(SkUiAnimatableProperty.Opacity, 0, 100, null, cancellation.Token));
        Assert.False(await node.AnimateAsync(SkUiAnimatableProperty.Opacity, 0, 100, null, cancellation.Token));
        Assert.Equal((1d, 1d), (box.Opacity, node.Opacity));
    }

    [Fact]
    public void ALongLivedTokenDoesNotKeepAViewThatNeverShowsAlive()
    {
        using var cancellation = new CancellationTokenSource();
        var (box, node) = AnimateUnshown(cancellation.Token);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(box.TryGetTarget(out _));
        Assert.False(node.TryGetTarget(out _));
        cancellation.Cancel();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference<SkUiBox>, WeakReference<SkUiCoreBox>) AnimateUnshown(CancellationToken token)
    {
        // Never attached to a surface: the animations stay queued and never finish.
        var box = new SkUiBox { Color = Colors.Red };
        var node = new SkUiCoreBox().SetColor(Colors.Red);
        _ = box.AnimateAsync(SkUiAnimatableProperty.Opacity, 0, 1000, null, token);
        _ = node.AnimateAsync(SkUiAnimatableProperty.Opacity, 0, 1000, null, token);
        return (new WeakReference<SkUiBox>(box), new WeakReference<SkUiCoreBox>(node));
    }

    [Fact]
    public async Task ACancelledTokenStopsAViewAnimationWithoutApplyingItsEnd()
    {
        var box = new SkUiBox { Color = Colors.Red, WidthRequest = 20, HeightRequest = 20 };
        var root = new SkUiContentView { Content = box };
        using var surface = new SkUiTestSurface(root, 40, 40);
        surface.Frame(0);

        using var cancellation = new CancellationTokenSource();
        var fade = SkUiViewAnimation.FadeOut(1000, Easing.Linear).RunAsync(box, cancellation.Token);
        surface.Frame(10);
        surface.Frame(510);
        cancellation.Cancel();
        surface.Frame(600);
        surface.Frame(700);
        Assert.False(await fade.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.InRange(box.Opacity, 0.3, 0.7);
    }

    [Fact]
    public void ChangingOneOfManyChildrenReRecordsOnlyThatChild()
    {
        var stack = new SkUiVerticalStackLayout();
        var boxes = Enumerable.Range(0, 100).Select(_ => new SkUiBox { Color = Colors.Red, HeightRequest = 2 }).ToArray();
        foreach (var box in boxes)
            stack.Children.Add(box);
        using var surface = new SkUiTestSurface(stack, 20, 200);
        surface.Frame();
        var recorded = surface.RecordedPictures;
        Assert.True(recorded >= 100);

        boxes[42].Color = Colors.Blue;
        var bitmap = surface.Frame();
        Assert.Equal(recorded + 1, surface.RecordedPictures);
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(10, 85));
    }

    [Fact]
    public void ManyInvalidationsInOneFrameReachTheRootOnce()
    {
        var stack = new SkUiVerticalStackLayout();
        var boxes = Enumerable.Range(0, 50).Select(_ => new SkUiBox { HeightRequest = 2 }).ToArray();
        foreach (var box in boxes)
            stack.Children.Add(box);
        using var surface = new SkUiTestSurface(stack, 20, 100);
        surface.Frame();
        var rootSignals = 0;
        stack.PaintInvalidated += (_, _) => rootSignals++;
        foreach (var box in boxes)
            box.Color = Colors.Green;
        Assert.Equal(1, rootSignals);
        surface.Frame();
        boxes[0].Color = Colors.Red;
        Assert.Equal(2, rootSignals);
    }

    [Fact]
    public void LeavesClipByDefaultButLayoutsLetChildrenOverflow()
    {
        var overflowing = new SkUiBox
        {
            Color = Colors.Red, WidthRequest = 10, HeightRequest = 10, TranslationX = 20,
            HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start
        };
        var inner = new SkUiLayout { WidthRequest = 10, HeightRequest = 10, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
        inner.Children.Add(overflowing);
        var root = new SkUiContentView { Content = inner };
        using var surface = new SkUiTestSurface(root, 40, 20);
        Assert.Equal(SKColors.Red, surface.Frame().GetPixel(25, 5));

        inner.ClipToBounds = true;
        Assert.NotEqual(SKColors.Red, surface.Frame().GetPixel(25, 5));
    }

    [Fact]
    public void MovingAChildBetweenParentsReRecordsItUnderTheNewParent()
    {
        var box = new SkUiBox { Color = Colors.Red };
        var left = new SkUiContentView { WidthRequest = 20, HorizontalOptions = LayoutOptions.Start, Content = box };
        var right = new SkUiContentView { WidthRequest = 20, HorizontalOptions = LayoutOptions.End };
        var root = new SkUiLayout();
        root.Children.Add(left);
        root.Children.Add(right);
        using var surface = new SkUiTestSurface(root, 40, 10);
        Assert.Equal(SKColors.Red, surface.Frame().GetPixel(5, 5));

        left.Content = null;
        right.Content = box;
        SkUiTestHelpers.Arrange(root, 40, 10);
        var bitmap = surface.Frame();
        Assert.NotEqual(SKColors.Red, bitmap.GetPixel(5, 5));
        Assert.Equal(SKColors.Red, bitmap.GetPixel(35, 5));
    }

    [Fact]
    public void ImmediatePaintMatchesCompositedFrame()
    {
        var border = new SkUiBorder { BackgroundColor = Colors.Blue, CornerRadius = 6, Stroke = Colors.Black, StrokeThickness = 2, Opacity = 0.5 };
        border.Content = new SkUiBox { Color = Colors.Yellow, Rotation = 30, Margin = new Thickness(6) };
        var root = new SkUiContentView { Background = Colors.White, Content = border };
        using var surface = new SkUiTestSurface(root, 30, 30);
        var composited = surface.Frame();
        using var immediate = new SKBitmap(30, 30);
        using (var canvas = new SKCanvas(immediate))
        {
            canvas.Clear(SKColors.White);
            root.Paint(canvas);
        }
        for (var y = 0; y < 30; y++)
            for (var x = 0; x < 30; x++)
            {
                var a = composited.GetPixel(x, y);
                var b = immediate.GetPixel(x, y);
                Assert.True(Math.Abs(a.Red - b.Red) <= 2 && Math.Abs(a.Green - b.Green) <= 2 && Math.Abs(a.Blue - b.Blue) <= 2,
                    $"({x},{y}) composited {a} vs immediate {b}");
            }
    }
}
