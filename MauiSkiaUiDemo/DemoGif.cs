using SkiaSharp;

namespace MauiSkiaUiDemo;

/// <summary>
/// Builds a small looping animated GIF in code (Skia decodes GIFs but cannot encode them), so the image demos can show
/// animation without bundling a file. Uncompressed LZW: a clear code every 200 pixels keeps every code 9 bits.
/// </summary>
public static class DemoGif
{
    private static readonly SKColor[] Palette = [SKColors.White, new(0x25, 0x63, 0xEB), new(0xCB, 0xD5, 0xE1)];

    /// <summary>A spinner: eight dots round a circle, one highlighted per frame, 100 ms each.</summary>
    public static byte[] Spinner(int size = 48, int frames = 8)
    {
        var gif = new List<byte>();
        gif.AddRange("GIF89a"u8.ToArray());
        gif.AddRange([(byte)size, (byte)(size >> 8), (byte)size, (byte)(size >> 8), 0xF7, 0, 0]); // 256-color global table
        for (var index = 0; index < 256; index++)
        {
            var color = index < Palette.Length ? Palette[index] : SKColors.Black;
            gif.AddRange([color.Red, color.Green, color.Blue]);
        }
        gif.AddRange([0x21, 0xFF, 0x0B, .. "NETSCAPE2.0"u8.ToArray(), 0x03, 0x01, 0x00, 0x00, 0x00]); // loop forever
        for (var frame = 0; frame < frames; frame++)
        {
            gif.AddRange([0x21, 0xF9, 0x04, 0x04, 0x0A, 0x00, 0x00, 0x00]); // keep the frame, 10 cs
            gif.AddRange([0x2C, 0, 0, 0, 0, (byte)size, (byte)(size >> 8), (byte)size, (byte)(size >> 8), 0x00]);
            gif.Add(8); // LZW minimum code size
            AddSubBlocks(gif, Lzw(FramePixels(size, frames, frame)));
        }
        gif.Add(0x3B);
        return [.. gif];
    }

    private static byte[] FramePixels(int size, int frames, int frame)
    {
        using var bitmap = new SKBitmap(size, size);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(Palette[0]);
            using var paint = new SKPaint { IsAntialias = false };
            var center = size / 2f;
            for (var dot = 0; dot < frames; dot++)
            {
                var angle = dot * 2 * Math.PI / frames - Math.PI / 2;
                paint.Color = dot == frame ? Palette[1] : Palette[2];
                canvas.DrawCircle(center + (float)Math.Cos(angle) * size * 0.34f, center + (float)Math.Sin(angle) * size * 0.34f, size * 0.09f, paint);
            }
        }
        var pixels = new byte[size * size];
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                pixels[y * size + x] = (byte)Math.Max(0, Array.IndexOf(Palette, bitmap.GetPixel(x, y)));
        return pixels;
    }

    private static byte[] Lzw(byte[] pixels)
    {
        const int Clear = 256, End = 257, Width = 9;
        var output = new List<byte>();
        int buffer = 0, bits = 0;
        void Emit(int code)
        {
            buffer |= code << bits;
            bits += Width;
            while (bits >= 8)
            {
                output.Add((byte)buffer);
                buffer >>= 8;
                bits -= 8;
            }
        }
        for (var index = 0; index < pixels.Length; index++)
        {
            if (index % 200 == 0)
                Emit(Clear);
            Emit(pixels[index]);
        }
        Emit(End);
        if (bits > 0)
            output.Add((byte)buffer);
        return [.. output];
    }

    private static void AddSubBlocks(List<byte> gif, byte[] data)
    {
        for (var offset = 0; offset < data.Length; offset += 255)
        {
            var length = Math.Min(255, data.Length - offset);
            gif.Add((byte)length);
            gif.AddRange(data.AsSpan(offset, length).ToArray());
        }
        gif.Add(0);
    }
}
