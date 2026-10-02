using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// Shared off-UI-thread image decoding for <see cref="SkUiImage"/> and <see cref="Core.SkUiCoreImage"/>.
/// Large sources are decoded down to <see cref="MaxDecodeDimension"/> (using the codec's native subsampling
/// where supported, e.g. JPEG/WebP) so a 12 MP photo does not cost ~48 MB of memory. Layout still uses the
/// original source size (one source pixel = one intrinsic DIP), so markup behaves the same.
/// </summary>
public static class SkUiImageDecoder
{
    private static int _maxDecodeDimension = 2048;

    /// <summary>Longest decoded edge in pixels (default 2048). Set before images load; minimum 64.</summary>
    public static int MaxDecodeDimension
    {
        get => Volatile.Read(ref _maxDecodeDimension);
        set => Volatile.Write(ref _maxDecodeDimension, Math.Max(64, value));
    }

    /// <summary>Decodes <paramref name="bytes"/>; returns the (possibly reduced) image and the original source size.</summary>
    internal static (SKImage Image, SKSizeI SourceSize) Decode(byte[] bytes)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data) ?? throw new InvalidDataException("Unsupported or corrupt image.");
        var source = new SKSizeI(codec.Info.Width, codec.Info.Height);
        var max = MaxDecodeDimension;
        var longest = Math.Max(source.Width, source.Height);
        if (longest <= max)
        {
            using var full = SKBitmap.Decode(codec) ?? throw new InvalidDataException("Image decoding failed.");
            return (SKImage.FromBitmap(full), source);
        }

        var scale = max / (float)longest;
        var target = new SKSizeI(Math.Max(1, (int)(source.Width * scale)), Math.Max(1, (int)(source.Height * scale)));
        // Native subsampled decode (cheap) to the nearest supported size ≥ target, then resample the rest.
        var native = codec.GetScaledDimensions(scale);
        var decodeInfo = codec.Info.WithSize(native.Width, native.Height).WithAlphaType(SKAlphaType.Premul);
        using var decoded = SKBitmap.Decode(codec, decodeInfo) ?? SKBitmap.Decode(codec) ?? throw new InvalidDataException("Image decoding failed.");
        if (decoded.Width <= target.Width && decoded.Height <= target.Height)
            return (SKImage.FromBitmap(decoded), source);
        using var resized = decoded.Resize(decoded.Info.WithSize(target.Width, target.Height),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None)) ?? throw new InvalidDataException("Image resize failed.");
        return (SKImage.FromBitmap(resized), source);
    }
}
