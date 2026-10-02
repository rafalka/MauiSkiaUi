using MauiSkiaUi.LeakTests;
using SkiaSharp;

namespace MauiSkiaUi.DeviceTests;

/// <summary>
/// Draws a few controls offscreen through the real frame pipeline (recorder, compositor) and checks their pixels.
/// Leak scenarios prove lifetime and input, not painting; this catches builds (trimmed, Native AOT) where drawing
/// silently breaks (an app-defined overlay painter, text, a button fill), and checks that drawn text finds a font the
/// app registered only with MAUI's <c>ConfigureFonts</c>. It also loads a <c>MauiImage</c> (an SVG Resizetizer turned into
/// density PNGs) and a font glyph image, which resolve differently on each platform.
/// </summary>
internal static class RenderCheck
{
    private const int Size = 200;

    /// <summary>Alias of Roboto Mono, registered with <c>ConfigureFonts</c> only (MauiProgram).</summary>
    public const string MauiFontAlias = "SkUiDeviceTestMono";

    /// <summary>The <c>MauiImage</c> item (Resources/Images/skui_badge.svg, 48 × 48), referenced by its generated PNG.</summary>
    public const string MauiImageName = "skui_badge.png";

    public static async Task<LeakResult> RunAsync()
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
            // Drawn text in the ConfigureFonts font (label → text engine → SkUiFonts → MAUI's registrar). Roboto Mono is
            // monospaced: ten i's are as wide as ten M's; in the default (proportional) font they are far narrower.
            double Width(string text)
            {
                var sample = new SkUiLabel { Text = text, FontFamily = MauiFontAlias, FontSize = 20 };
                return sample.Measure(double.PositiveInfinity, double.PositiveInfinity).Width;
            }
            var narrow = Width("iiiiiiiiii");
            var wide = Width("MMMMMMMMMM");
            if (SkUiFonts.TryResolve(MauiFontAlias) is null || wide <= 0 || Math.Abs(narrow - wide) > wide * 0.02)
                problems.Add($"drawn text in ConfigureFonts alias '{MauiFontAlias}' is not monospaced (i×10 {narrow:F1} vs M×10 {wide:F1}): the font did not resolve");
            var images = await CheckImagesAsync(problems);
            return new LeakResult("RenderCheck", problems.Count == 0 ? LeakStatus.Pass : LeakStatus.Fail,
                problems.Count == 0 ? $"overlay layer, label and button drawn; ConfigureFonts font resolved; {images}" : string.Join(" · ", problems));
        }
        catch (Exception exception)
        {
            return new LeakResult("RenderCheck", LeakStatus.Fail, $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    /// <summary>
    /// The MauiImage lays out at its 48 × 48 base size from the file for the display density (more pixels than DIPs on
    /// a high-density screen); a font glyph renders synchronously in the ConfigureFonts font.
    /// </summary>
    private static async Task<string> CheckImagesAsync(List<string> problems)
    {
        using var image = new SkUiImage { Source = ImageSource.FromFile(MauiImageName) };
        await Task.WhenAny(image.LoadingTask, Task.Delay(TimeSpan.FromSeconds(10)));
        var density = DeviceDisplay.Current.MainDisplayInfo.Density;
        var pixels = image.CachedImage?.Frames[0].Width ?? 0;
        if (image.LoadError is { } error)
            problems.Add($"MauiImage '{MauiImageName}' failed: {error.GetType().Name}: {error.Message}");
        else if (image.ImageSize != new Size(48, 48))
            problems.Add($"MauiImage '{MauiImageName}' is {image.ImageSize.Width:F1} × {image.ImageSize.Height:F1} DIPs, not its 48 × 48 base size");
        else if (density >= 1.5 && pixels <= 48)
            problems.Add($"MauiImage '{MauiImageName}' decoded at {pixels} px on a {density:F2}× display: not the density file");

        using var glyph = new SkUiImage { Source = new FontImageSource { Glyph = "M", FontFamily = MauiFontAlias, Size = 20, Color = Colors.Black } };
        if (glyph.LoadError is not null || !glyph.LoadingTask.IsCompleted)
            problems.Add($"font glyph image failed: {glyph.LoadError?.Message ?? "not synchronous"}");
        else if (Math.Abs(glyph.ImageSize.Width - 12) > 1.5)
            problems.Add($"font glyph image is {glyph.ImageSize.Width:F1} DIPs wide, not one Roboto Mono advance (12)");
        return $"MauiImage 48 × 48 from {pixels} px ({density:F2}×), font glyph {glyph.ImageSize.Width:F0} × {glyph.ImageSize.Height:F0}";
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
