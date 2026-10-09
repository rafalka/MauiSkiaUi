using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// Rasterizes <see cref="SkUiFontImageSource"/> glyphs through the shared text engine (font registry, shaping and
/// fallback), at the display scale so they are sharp. Like MAUI's font images, the image is the text's advance wide
/// and its line high, the glyph centered.
/// </summary>
internal static class SkUiFontImages
{
    internal static SkUiDecodedImage Render(SkUiFontImageSource source, float displayScale)
    {
        var typeface = SkUiTypefaces.Resolve(source.FontFamily, source.FontAttributes);
        var style = new SkUiTextStyle(typeface, source.DrawnSize, LineBreakMode.NoWrap, FontAttributes: source.FontAttributes);
        var layout = new SkUiTextLayout();
        var size = layout.Measure(source.Glyph, style, default, double.PositiveInfinity);
        if (size.Width <= 0 || size.Height <= 0)
            throw new InvalidDataException("The glyph has no size.");
        var scale = Math.Max(1, displayScale);
        var width = Math.Max(1, (int)Math.Ceiling(size.Width * scale));
        var height = Math.Max(1, (int)Math.Ceiling(size.Height * scale));
        var info = new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("Could not create a glyph surface.");
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.Scale(scale);
        var color = source.Color ?? Colors.White;
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor((byte)(color.Red * 255), (byte)(color.Green * 255), (byte)(color.Blue * 255), (byte)(color.Alpha * 255))
        };
        layout.Draw(canvas, source.Glyph, style, default, width / scale, height / scale, TextAlignment.Center, TextAlignment.Center, paint);
        layout.Release(); // the glyph's blob, now drawn
        return new SkUiDecodedImage([surface.Snapshot()], [], new Size(width / (double)scale, height / (double)scale));
    }
}
