using MauiSkiaUi.LeakTests;
using SkiaSharp;

namespace MauiSkiaUi.DeviceTests;

/// <summary>
/// Draws a few controls offscreen through the real frame pipeline (recorder, compositor) and checks their pixels.
/// Leak scenarios prove lifetime and input, not painting; this catches builds (trimmed, Native AOT) where drawing
/// silently breaks (an app-defined overlay painter, text, a button fill).
/// </summary>
internal static class RenderCheck
{
    private const int Size = 200;

    public static LeakResult Run()
    {
        try
        {
            var probe = new OverlayProbe { Color = Colors.Blue, WidthRequest = 40, HeightRequest = 40, HorizontalOptions = LayoutOptions.Start };
            var label = new SkUiLabel { Text = "Render check", FontSize = 20, TextColor = Colors.Black };
            var button = new SkUiButton { Text = "OK", FillColor = Colors.Green, TextColor = Colors.White, HeightRequest = 40 };
            var root = new SkUiContentView
            {
                Background = Colors.White,
                Content = new SkUiVerticalStackLayout { Spacing = 10, Padding = new Thickness(10), Children = { probe, label, button } }
            };
            using var bitmap = Render(root);

            var problems = new List<string>();
            var center = SkUiDiagnostics.GetRootBounds(probe)!.Value.Center;
            if (bitmap.GetPixel((int)center.X, (int)center.Y) != SKColors.Red)
                problems.Add($"overlay layer of an app-defined view not drawn (center {bitmap.GetPixel((int)center.X, (int)center.Y)})");
            if (!Any(bitmap, SkUiDiagnostics.GetRootBounds(label)!.Value, color => color.Red < 90 && color.Green < 90 && color.Blue < 90))
                problems.Add("label text not drawn");
            if (!Any(bitmap, SkUiDiagnostics.GetRootBounds(button)!.Value, color => color.Green > 100 && color.Red < 60 && color.Blue < 60))
                problems.Add("button fill not drawn");
            return new LeakResult("RenderCheck", problems.Count == 0 ? LeakStatus.Pass : LeakStatus.Fail,
                problems.Count == 0 ? "overlay layer, label and button drawn" : string.Join(" · ", problems));
        }
        catch (Exception exception)
        {
            return new LeakResult("RenderCheck", LeakStatus.Fail, $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    /// <summary>One frame through the retained pipeline, as a headless surface does.</summary>
    private static SKBitmap Render(SkUiView root)
    {
        root.Measure(Size, Size);
        root.Arrange(new Rect(0, 0, Size, Size));
        var queue = new Queue<Action>();
        var renderer = new SkUiFrameRenderer(root, queue.Enqueue, static () => { }, static () => { });
        try
        {
            void Pump()
            {
                while (queue.Count > 0)
                    queue.Dequeue()();
            }
            renderer.RequestFrame();
            Pump();
            renderer.PresentFrame();
            var bitmap = new SKBitmap(Size, Size);
            using (var canvas = new SKCanvas(bitmap))
                renderer.Render(canvas, bitmap.Info, TimeSpan.Zero);
            Pump();
            return bitmap;
        }
        finally
        {
            renderer.Dispose();
        }
    }

    private static bool Any(SKBitmap bitmap, Rect bounds, Func<SKColor, bool> match)
    {
        for (var y = (int)bounds.Top; y < (int)bounds.Bottom && y < bitmap.Height; y++)
            for (var x = (int)bounds.Left; x < (int)bounds.Right && x < bitmap.Width; x++)
                if (match(bitmap.GetPixel(x, y)))
                    return true;
        return false;
    }

    /// <summary>An app-defined view that paints an overlay layer through <see cref="SkUiView.PaintOverlay"/>.</summary>
    private sealed class OverlayProbe : SkUiBox
    {
        public OverlayProbe() => SetPaintOverlay(PaintMarker);

        private void PaintMarker(SKCanvas canvas)
        {
            using var paint = new SKPaint { Color = SKColors.Red };
            canvas.DrawRect(10, 10, (float)Width - 20, (float)Height - 20, paint);
        }
    }
}
