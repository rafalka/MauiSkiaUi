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
