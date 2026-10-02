using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// Shared off-UI-thread image decoding for both layers. Large sources are decoded down to <see cref="MaxDecodeDimension"/>
/// or a view's downsample size (using the codec's native subsampling where supported, e.g. JPEG/WebP), so a 12 MP photo
/// does not cost ~48 MB of memory. Layout still uses the source size, so markup behaves the same. JPEG EXIF orientation is
/// applied, and animated GIF / WebP decode to all their frames.
/// </summary>
public static class SkUiImageDecoder
{
    private static int _maxDecodeDimension = 2048;
    private static long _maxAnimationBytes = 64L * 1024 * 1024;

    /// <summary>Longest decoded edge in pixels (default 2048). Set before images load; minimum 64.</summary>
    public static int MaxDecodeDimension
    {
        get => Volatile.Read(ref _maxDecodeDimension);
        set => Volatile.Write(ref _maxDecodeDimension, Math.Max(64, value));
    }

    /// <summary>
    /// The most memory the decoded frames of one animated image may take (default 64 MiB). A larger animation decodes
    /// to its first frame only and draws still.
    /// </summary>
    public static long MaxAnimationBytes
    {
        get => Volatile.Read(ref _maxAnimationBytes);
        set => Volatile.Write(ref _maxAnimationBytes, Math.Max(0, value));
    }

    /// <summary>
    /// Decodes <paramref name="bytes"/>. The intrinsic size (DIPs) is the oriented source size divided by
    /// <paramref name="sourceScale"/> (pixels per DIP: 3 for an Android xxhdpi drawable or an iOS <c>@3x</c> file).
    /// <paramref name="maxPixels"/> bounds the decoded size per axis (0: unbounded), on top of <see cref="MaxDecodeDimension"/>;
    /// images are never upscaled.
    /// </summary>
    internal static SkUiDecodedImage Decode(byte[] bytes, float sourceScale = 1, SKSizeI maxPixels = default)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data) ?? throw new InvalidDataException("Unsupported or corrupt image.");
        var origin = codec.EncodedOrigin;
        var swaps = SwapsAxes(origin);
        var encoded = new SKSizeI(codec.Info.Width, codec.Info.Height);
        var oriented = swaps ? new SKSizeI(encoded.Height, encoded.Width) : encoded;
        var target = Target(oriented, maxPixels);
        var targetEncoded = swaps ? new SKSizeI(target.Height, target.Width) : target;
        var size = new Size(oriented.Width / (double)sourceScale, oriented.Height / (double)sourceScale);

        if (codec.FrameCount > 1 && (long)target.Width * target.Height * 4 * codec.FrameCount <= MaxAnimationBytes)
            return DecodeFrames(codec, targetEncoded, origin, size);

        using var decoded = DecodeStill(codec, encoded, targetEncoded);
        using var upright = Orient(decoded, origin);
        return new SkUiDecodedImage([ToImage(upright ?? decoded)], [], size);
    }

    /// <summary>An image sharing the bitmap's pixels (an immutable bitmap is not copied); the bitmap may be disposed after.</summary>
    private static SKImage ToImage(SKBitmap bitmap)
    {
        bitmap.SetImmutable();
        return SKImage.FromBitmap(bitmap) ?? throw new InvalidDataException("Image creation failed.");
    }

    /// <summary>The decoded (oriented) size: the source fitted inside the bounds, aspect kept, never upscaled.</summary>
    private static SKSizeI Target(SKSizeI source, SKSizeI maxPixels)
    {
        var max = MaxDecodeDimension;
        var scale = Math.Min(1f, max / (float)Math.Max(source.Width, source.Height));
        if (maxPixels.Width > 0)
            scale = Math.Min(scale, maxPixels.Width / (float)source.Width);
        if (maxPixels.Height > 0)
            scale = Math.Min(scale, maxPixels.Height / (float)source.Height);
        return scale >= 1 ? source
            : new SKSizeI(Math.Max(1, (int)Math.Round(source.Width * scale)), Math.Max(1, (int)Math.Round(source.Height * scale)));
    }

    private static SKBitmap DecodeStill(SKCodec codec, SKSizeI encoded, SKSizeI target)
    {
        if (target == encoded)
            return SKBitmap.Decode(codec) ?? throw new InvalidDataException("Image decoding failed.");
        // Native subsampled decode (cheap) to the nearest supported size ≥ target, then resample the rest.
        var native = codec.GetScaledDimensions(target.Width / (float)encoded.Width);
        var decodeInfo = codec.Info.WithSize(native.Width, native.Height).WithAlphaType(SKAlphaType.Premul);
        var decoded = SKBitmap.Decode(codec, decodeInfo) ?? SKBitmap.Decode(codec) ?? throw new InvalidDataException("Image decoding failed.");
        return Fit(decoded, target);
    }

    /// <summary>Resizes <paramref name="bitmap"/> down to <paramref name="target"/> (disposing it), or returns it when it fits.</summary>
    private static SKBitmap Fit(SKBitmap bitmap, SKSizeI target)
    {
        if (bitmap.Width <= target.Width && bitmap.Height <= target.Height)
            return bitmap;
        using (bitmap)
            return bitmap.Resize(bitmap.Info.WithSize(target.Width, target.Height), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None))
                ?? throw new InvalidDataException("Image resize failed.");
    }

    /// <summary>
    /// All frames of an animation, each composed over the frame it depends on (<c>RequiredFrame</c>), at the full
    /// encoded size and then resized. Durations follow browsers: 10 ms or less shows for 100 ms.
    /// </summary>
    private static SkUiDecodedImage DecodeFrames(SKCodec codec, SKSizeI target, SKEncodedOrigin origin, Size size)
    {
        var infos = codec.FrameInfo;
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        var full = new SKBitmap?[infos.Length];
        var frames = new SKImage[infos.Length];
        var durations = new int[infos.Length];
        try
        {
            for (var index = 0; index < infos.Length; index++)
            {
                var required = infos[index].RequiredFrame;
                var bitmap = required >= 0 && required < index && full[required] is { } prior ? prior.Copy() : new SKBitmap(info);
                if (required < 0)
                    bitmap.Erase(SKColors.Transparent);
                var result = codec.GetPixels(info, bitmap.GetPixels(), new SKCodecOptions(index, required < index ? required : -1));
                if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
                {
                    bitmap.Dispose();
                    throw new InvalidDataException($"Animation frame {index} decoding failed ({result}).");
                }
                full[index] = bitmap;
                var fitted = bitmap.Width <= target.Width && bitmap.Height <= target.Height ? bitmap : Fit(bitmap.Copy(), target);
                using (var upright = Orient(fitted, origin))
                    frames[index] = ToImage(upright ?? fitted); // a shared, immutable frame: later frames copy it
                if (!ReferenceEquals(fitted, bitmap))
                    fitted.Dispose();
                durations[index] = infos[index].Duration <= 10 ? 100 : infos[index].Duration;
            }
            return new SkUiDecodedImage(frames, durations, size);
        }
        catch
        {
            foreach (var frame in frames)
                frame?.Dispose();
            throw;
        }
        finally
        {
            foreach (var bitmap in full)
                bitmap?.Dispose();
        }
    }

    private static bool SwapsAxes(SKEncodedOrigin origin) =>
        origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;

    /// <summary>
    /// The bitmap turned upright for its EXIF <paramref name="origin"/> (Skia's <c>SkEncodedOriginToMatrix</c>), or
    /// <c>null</c> when it already is.
    /// </summary>
    internal static SKBitmap? Orient(SKBitmap bitmap, SKEncodedOrigin origin)
    {
        if (origin is SKEncodedOrigin.TopLeft)
            return null;
        float w = bitmap.Width, h = bitmap.Height;
        var matrix = origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
            _ => SKMatrix.Identity
        };
        if (matrix.IsIdentity)
            return null;
        var info = SwapsAxes(origin) ? bitmap.Info.WithSize(bitmap.Height, bitmap.Width) : bitmap.Info;
        var result = new SKBitmap(info.WithAlphaType(SKAlphaType.Premul));
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);
        canvas.SetMatrix(matrix);
        using var image = SKImage.FromBitmap(bitmap);
        canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
        return result;
    }
}

/// <summary>A decoded image: one frame for stills, several with durations (ms) for animations; its size in DIPs.</summary>
internal sealed record SkUiDecodedImage(SKImage[] Frames, int[] Durations, Size Size)
{
    /// <summary>Decoded pixel memory of all frames.</summary>
    public long Bytes
    {
        get
        {
            long total = 0;
            foreach (var frame in Frames)
                total += (long)frame.Width * frame.Height * 4;
            return total;
        }
    }

    /// <summary>
    /// Transformed <paramref name="frames"/> in place of the decoded ones, sized at the same DIPs per pixel
    /// (<paramref name="dipsPerPixel"/>, measured before the transformation replaced the originals).
    /// </summary>
    public SkUiDecodedImage WithFrames(SKImage[] frames, SKSize dipsPerPixel) =>
        new(frames, Durations, new Size(frames[0].Width * dipsPerPixel.Width, frames[0].Height * dipsPerPixel.Height));
}
