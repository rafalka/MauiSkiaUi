using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// Changes a decoded image before it is cached and drawn (FFImageLoading's <c>ITransformation</c>): crop to a circle,
/// round corners, blur, tint, … Runs once per decode, off the UI thread for bitmaps; the result is cached under the
/// source plus every transformation's <see cref="Key"/>, so views with the same chain share it.
/// </summary>
public interface ISkUiImageTransformation
{
    /// <summary>
    /// Identifies the transformation and all its settings in the cache key: two transformations with the same key must
    /// produce the same image.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Returns the transformed image: a new one (the loader disposes <paramref name="source"/>) or <paramref name="source"/>
    /// itself. Called on a worker thread with a raster image; must not touch views.
    /// </summary>
    /// <param name="source">The decoded (and previously transformed) image.</param>
    /// <param name="pixelsPerDip">Image pixels per intrinsic DIP: multiply DIP settings (radii, border widths) by it.</param>
    SKImage Transform(SKImage source, float pixelsPerDip);
}

/// <summary>Base for the stock transformations: a raster canvas helper and invariant key formatting.</summary>
/// <remarks>
/// Settings are read when a load starts (each load works on a copy): changing a property of a transformation already
/// in use applies on the next load (<c>ReloadAsync</c>, or a new source or transformation list).
/// </remarks>
public abstract class SkUiImageTransformation : ISkUiImageTransformation
{
    /// <summary>
    /// <paramref name="transformations"/> as a load uses them: copies of the stock ones, so settings changed during the
    /// load cannot mix into its result (custom <see cref="ISkUiImageTransformation"/>s are used as they are).
    /// </summary>
    internal static IReadOnlyList<ISkUiImageTransformation>? Snapshot(IReadOnlyList<ISkUiImageTransformation>? transformations)
    {
        if (transformations is not { Count: > 0 })
            return transformations;
        var copies = new ISkUiImageTransformation[transformations.Count];
        for (var index = 0; index < copies.Length; index++)
            copies[index] = transformations[index] is SkUiImageTransformation stock ? (ISkUiImageTransformation)stock.MemberwiseClone() : transformations[index];
        return copies;
    }

    /// <inheritdoc />
    public abstract string Key { get; }

    /// <inheritdoc />
    public abstract SKImage Transform(SKImage source, float pixelsPerDip);

    /// <summary>Draws a new, transparent <paramref name="width"/>×<paramref name="height"/> raster image.</summary>
    protected static SKImage Render(int width, int height, Action<SKCanvas> draw)
    {
        ArgumentNullException.ThrowIfNull(draw);
        var info = new SKImageInfo(Math.Max(1, width), Math.Max(1, height), SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("Could not create an image surface.");
        surface.Canvas.Clear(SKColors.Transparent);
        draw(surface.Canvas);
        return surface.Snapshot();
    }

    /// <summary>Formats a key with the invariant culture.</summary>
    protected static string Invariant(FormattableString key) => FormattableString.Invariant(key);

    /// <summary>A color in keys: ARGB hex, or <c>none</c>.</summary>
    protected static string KeyOf(Color? color) => color?.ToArgbHex(includeAlpha: true) ?? "none";

    /// <summary>The linear sampling the stock transformations draw with.</summary>
    protected static SKSamplingOptions Sampling => new(SKFilterMode.Linear, SKMipmapMode.None);

    /// <summary>The centered crop of <paramref name="source"/> with <paramref name="aspectRatio"/> (width / height; 0 or less: the whole image).</summary>
    protected static SKRect CenterCrop(SKImage source, double aspectRatio, double offsetX = 0, double offsetY = 0, double zoom = 1)
    {
        ArgumentNullException.ThrowIfNull(source);
        float width = source.Width, height = source.Height;
        float cropWidth = width, cropHeight = height;
        if (aspectRatio > 0 && double.IsFinite(aspectRatio))
        {
            if (width / height > aspectRatio)
                cropWidth = (float)(height * aspectRatio);
            else
                cropHeight = (float)(width / aspectRatio);
        }
        if (zoom > 1 && double.IsFinite(zoom))
        {
            cropWidth /= (float)zoom;
            cropHeight /= (float)zoom;
        }
        var left = (width - cropWidth) / 2 * (1 + (float)Math.Clamp(offsetX, -1, 1));
        var top = (height - cropHeight) / 2 * (1 + (float)Math.Clamp(offsetY, -1, 1));
        return SKRect.Create(left, top, Math.Max(1, cropWidth), Math.Max(1, cropHeight));
    }

    /// <summary>Validates a DIP length setting (finite, not negative).</summary>
    protected static double NonNegative(double value, string name)
    {
        SkUiValidate.ThrowIfNegativeOrNotFinite(value, name);
        return value;
    }

    /// <summary>Draws a border of <paramref name="width"/> pixels inside <paramref name="shape"/>.</summary>
    private protected static void DrawBorder(SKCanvas canvas, SKRoundRect shape, float width, Color? color)
    {
        if (width <= 0 || color is null || color.Alpha <= 0)
            return;
        using var inner = new SKRoundRect(shape);
        inner.Inflate(-width / 2, -width / 2);
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = width,
            Color = new SKColor((byte)(color.Red * 255), (byte)(color.Green * 255), (byte)(color.Blue * 255), (byte)(color.Alpha * 255))
        };
        canvas.DrawRoundRect(inner, paint);
    }
}

/// <summary>
/// Crops the image to its centered square and clips it to a circle (avatars), with an optional border inside the circle
/// (FFImageLoading's <c>CircleTransformation</c>).
/// </summary>
public sealed class SkUiCircleTransformation : SkUiImageTransformation
{
    private double _borderWidth;

    /// <summary>A circle without a border.</summary>
    public SkUiCircleTransformation() { }

    /// <summary>A circle with a <paramref name="borderWidth"/> DIP border in <paramref name="borderColor"/>.</summary>
    public SkUiCircleTransformation(double borderWidth, Color? borderColor)
    {
        BorderWidth = borderWidth;
        BorderColor = borderColor;
    }

    /// <summary>Border width in image DIPs (0: none).</summary>
    public double BorderWidth { get => _borderWidth; set => _borderWidth = NonNegative(value, nameof(BorderWidth)); }

    /// <summary>Border color (<c>null</c>: none).</summary>
    public Color? BorderColor { get; set; }

    /// <inheritdoc />
    public override string Key => Invariant($"Circle;{BorderWidth};{KeyOf(BorderColor)}");

    /// <inheritdoc />
    public override SKImage Transform(SKImage source, float pixelsPerDip)
    {
        var crop = CenterCrop(source, 1);
        var size = (int)Math.Round(Math.Min(crop.Width, crop.Height));
        var border = (float)(BorderWidth * pixelsPerDip);
        var color = BorderColor;
        return Render(size, size, canvas =>
        {
            using var circle = new SKRoundRect(new SKRect(0, 0, size, size), size / 2f);
            canvas.ClipRoundRect(circle, antialias: true);
            canvas.DrawImage(source, crop, new SKRect(0, 0, size, size), Sampling);
            DrawBorder(canvas, circle, border, color);
        });
    }
}

/// <summary>
/// Rounds the corners (per corner, in image DIPs), optionally cropping to an aspect ratio first, with an optional border
/// (FFImageLoading's <c>RoundedTransformation</c> / <c>CornersTransformation</c>). Radii larger than half a side are clamped.
/// </summary>
public sealed class SkUiRoundedTransformation : SkUiImageTransformation
{
    private CornerRadius _cornerRadius = new(8);
    private double _aspectRatio;
    private double _borderWidth;

    /// <summary>8 DIP corners.</summary>
    public SkUiRoundedTransformation() { }

    /// <summary>Uniform <paramref name="radius"/> DIP corners.</summary>
    public SkUiRoundedTransformation(double radius) => CornerRadius = new CornerRadius(radius);

    /// <summary>Per-corner radii in image DIPs.</summary>
    public CornerRadius CornerRadius
    {
        get => _cornerRadius;
        set
        {
            SkUiCornerRadii.Validate(value, nameof(CornerRadius));
            _cornerRadius = value;
        }
    }

    /// <summary>Width / height to crop to first, around the center (0: keep the image's).</summary>
    public double AspectRatio { get => _aspectRatio; set => _aspectRatio = NonNegative(value, nameof(AspectRatio)); }

    /// <summary>Border width in image DIPs (0: none).</summary>
    public double BorderWidth { get => _borderWidth; set => _borderWidth = NonNegative(value, nameof(BorderWidth)); }

    /// <summary>Border color (<c>null</c>: none).</summary>
    public Color? BorderColor { get; set; }

    /// <inheritdoc />
    public override string Key => Invariant(
        $"Rounded;{CornerRadius.TopLeft},{CornerRadius.TopRight},{CornerRadius.BottomRight},{CornerRadius.BottomLeft};{AspectRatio};{BorderWidth};{KeyOf(BorderColor)}");

    /// <inheritdoc />
    public override SKImage Transform(SKImage source, float pixelsPerDip)
    {
        var crop = CenterCrop(source, AspectRatio);
        var width = (int)Math.Round(crop.Width);
        var height = (int)Math.Round(crop.Height);
        var radii = CornerRadius;
        var border = (float)(BorderWidth * pixelsPerDip);
        var color = BorderColor;
        return Render(width, height, canvas =>
        {
            var bounds = new SKRect(0, 0, width, height);
            using var shape = new SKRoundRect();
            shape.SetRectRadii(bounds,
            [
                Radius(radii.TopLeft), Radius(radii.TopRight), Radius(radii.BottomRight), Radius(radii.BottomLeft)
            ]);
            canvas.ClipRoundRect(shape, antialias: true);
            canvas.DrawImage(source, crop, bounds, Sampling);
            DrawBorder(canvas, shape, border, color);
        });

        SKPoint Radius(double dips)
        {
            var radius = (float)Math.Min(dips * pixelsPerDip, Math.Min(width, height) / 2d);
            return new SKPoint(radius, radius);
        }
    }
}

/// <summary>
/// Crops to an aspect ratio and / or zooms in, around the center moved by an offset (FFImageLoading's
/// <c>CropTransformation</c>).
/// </summary>
public sealed class SkUiCropTransformation : SkUiImageTransformation
{
    private double _aspectRatio;
    private double _zoomFactor = 1;
    private double _offsetX;
    private double _offsetY;

    /// <summary>Width / height of the crop (0: the image's).</summary>
    public double AspectRatio { get => _aspectRatio; set => _aspectRatio = NonNegative(value, nameof(AspectRatio)); }

    /// <summary>Zoom in by this factor (1: none; the crop gets this much smaller).</summary>
    public double ZoomFactor
    {
        get => _zoomFactor;
        set
        {
            if (!double.IsFinite(value) || value < 1)
                throw new ArgumentOutOfRangeException(nameof(ZoomFactor), value, "The zoom factor must be finite and at least 1.");
            _zoomFactor = value;
        }
    }

    /// <summary>Where the crop sits horizontally in the free space: -1 left edge, 0 centered, 1 right edge.</summary>
    public double OffsetX { get => _offsetX; set => _offsetX = Unit(value, nameof(OffsetX)); }

    /// <summary>Where the crop sits vertically in the free space: -1 top edge, 0 centered, 1 bottom edge.</summary>
    public double OffsetY { get => _offsetY; set => _offsetY = Unit(value, nameof(OffsetY)); }

    /// <inheritdoc />
    public override string Key => Invariant($"Crop;{AspectRatio};{ZoomFactor};{OffsetX};{OffsetY}");

    /// <inheritdoc />
    public override SKImage Transform(SKImage source, float pixelsPerDip)
    {
        var crop = CenterCrop(source, AspectRatio, OffsetX, OffsetY, ZoomFactor);
        var width = (int)Math.Round(crop.Width);
        var height = (int)Math.Round(crop.Height);
        if (width == source.Width && height == source.Height)
            return source;
        return Render(width, height, canvas => canvas.DrawImage(source, crop, new SKRect(0, 0, width, height), Sampling));
    }

    private static double Unit(double value, string name)
    {
        if (!double.IsFinite(value) || value < -1 || value > 1)
            throw new ArgumentOutOfRangeException(name, value, "The offset must be between -1 and 1.");
        return value;
    }
}

/// <summary>Mirrors the image horizontally and / or vertically (FFImageLoading's <c>FlipTransformation</c>).</summary>
public sealed class SkUiFlipTransformation : SkUiImageTransformation
{
    /// <summary>Mirror left to right (default).</summary>
    public bool Horizontal { get; set; } = true;

    /// <summary>Mirror top to bottom.</summary>
    public bool Vertical { get; set; }

    /// <inheritdoc />
    public override string Key => Invariant($"Flip;{Horizontal};{Vertical}");

    /// <inheritdoc />
    public override SKImage Transform(SKImage source, float pixelsPerDip)
    {
        if (!Horizontal && !Vertical)
            return source;
        var (horizontal, vertical) = (Horizontal, Vertical);
        return Render(source.Width, source.Height, canvas =>
        {
            canvas.Scale(horizontal ? -1 : 1, vertical ? -1 : 1, source.Width / 2f, source.Height / 2f);
            canvas.DrawImage(source, 0, 0, Sampling);
        });
    }
}

/// <summary>
/// Rotates the image clockwise by <see cref="Degrees"/>; the result grows to hold the rotated image (quarter turns
/// swap width and height), so the intrinsic size changes with it (FFImageLoading's <c>RotateTransformation</c>).
/// </summary>
public sealed class SkUiRotateTransformation : SkUiImageTransformation
{
    private double _degrees = 90;

    /// <summary>A quarter turn clockwise.</summary>
    public SkUiRotateTransformation() { }

    /// <summary>Rotates by <paramref name="degrees"/> clockwise.</summary>
    public SkUiRotateTransformation(double degrees) => Degrees = degrees;

    /// <summary>Clockwise angle in degrees (negative: counter-clockwise).</summary>
    public double Degrees
    {
        get => _degrees;
        set
        {
            if (!double.IsFinite(value))
                throw new ArgumentOutOfRangeException(nameof(Degrees), value, "The angle must be finite.");
            _degrees = value;
        }
    }

    /// <inheritdoc />
    public override string Key => Invariant($"Rotate;{Degrees}");

    /// <inheritdoc />
    public override SKImage Transform(SKImage source, float pixelsPerDip)
    {
        var degrees = Degrees % 360;
        if (degrees == 0)
            return source;
        var radians = degrees * Math.PI / 180;
        var cos = Math.Abs(Math.Cos(radians));
        var sin = Math.Abs(Math.Sin(radians));
        // Quarter turns come out exact (no anti-aliased fringe from rounding).
        if (cos < 1e-9) cos = 0;
        if (sin < 1e-9) sin = 0;
        var width = (int)Math.Round(source.Width * cos + source.Height * sin);
        var height = (int)Math.Round(source.Width * sin + source.Height * cos);
        return Render(width, height, canvas =>
        {
            canvas.Translate(width / 2f, height / 2f);
            canvas.RotateDegrees((float)degrees);
            canvas.DrawImage(source, -source.Width / 2f, -source.Height / 2f, Sampling);
        });
    }
}

/// <summary>Gaussian blur, edges clamped so they do not fade (FFImageLoading's <c>BlurredTransformation</c>).</summary>
public sealed class SkUiBlurTransformation : SkUiImageTransformation
{
    private double _radius = 8;

    /// <summary>An 8 DIP blur.</summary>
    public SkUiBlurTransformation() { }

    /// <summary>A <paramref name="radius"/> DIP blur.</summary>
    public SkUiBlurTransformation(double radius) => Radius = radius;

    /// <summary>Blur strength (Gaussian sigma) in image DIPs.</summary>
    public double Radius { get => _radius; set => _radius = NonNegative(value, nameof(Radius)); }

    /// <inheritdoc />
    public override string Key => Invariant($"Blur;{Radius}");

    /// <inheritdoc />
    public override SKImage Transform(SKImage source, float pixelsPerDip)
    {
        var sigma = (float)(Radius * pixelsPerDip);
        if (sigma <= 0)
            return source;
        return Render(source.Width, source.Height, canvas =>
        {
            using var filter = SKImageFilter.CreateBlur(sigma, sigma, SKShaderTileMode.Clamp);
            using var paint = new SKPaint { ImageFilter = filter };
            canvas.DrawImage(source, 0, 0, Sampling, paint);
        });
    }
}

/// <summary>
/// Tints the image with a color (FFImageLoading's <c>TintTransformation</c>). The default <see cref="SKBlendMode.SrcIn"/>
/// paints every visible pixel in <see cref="Color"/>, keeping its alpha: recolors monochrome icons.
/// </summary>
public sealed class SkUiTintTransformation : SkUiImageTransformation
{
    private Color _color = Colors.Black;

    /// <summary>A black tint.</summary>
    public SkUiTintTransformation() { }

    /// <summary>A <paramref name="color"/> tint.</summary>
    public SkUiTintTransformation(Color color) => Color = color;

    /// <summary>Tint color.</summary>
    public Color Color
    {
        get => _color;
        set => _color = value ?? throw new ArgumentNullException(nameof(Color));
    }

    /// <summary>How the tint combines with the image (the tint is the source, the image the destination).</summary>
    public SKBlendMode BlendMode { get; set; } = SKBlendMode.SrcIn;

    /// <inheritdoc />
    public override string Key => Invariant($"Tint;{KeyOf(Color)};{BlendMode}");

    /// <inheritdoc />
    public override SKImage Transform(SKImage source, float pixelsPerDip)
    {
        var color = new SKColor((byte)(Color.Red * 255), (byte)(Color.Green * 255), (byte)(Color.Blue * 255), (byte)(Color.Alpha * 255));
        var mode = BlendMode;
        return Render(source.Width, source.Height, canvas =>
        {
            canvas.DrawImage(source, 0, 0, Sampling);
            canvas.DrawColor(color, mode);
        });
    }
}

/// <summary>
/// Applies a 4×5 color matrix (FFImageLoading's <c>ColorSpaceTransformation</c>): row-major R, G, B, A rows of
/// <c>[r, g, b, a, offset]</c>, offsets normalized (0–1), as <see cref="SKColorFilter.CreateColorMatrix(float[])"/>.
/// </summary>
public class SkUiColorMatrixTransformation : SkUiImageTransformation
{
    private float[] _matrix = (float[])Identity.Clone();

    private static readonly float[] Identity =
    [
        1, 0, 0, 0, 0,
        0, 1, 0, 0, 0,
        0, 0, 1, 0, 0,
        0, 0, 0, 1, 0
    ];

    /// <summary>The identity matrix (no change).</summary>
    public SkUiColorMatrixTransformation() { }

    /// <summary>Applies <paramref name="matrix"/> (20 values).</summary>
    public SkUiColorMatrixTransformation(float[] matrix) => Matrix = matrix;

    /// <summary>The 20 matrix values (a copy is kept).</summary>
    public float[] Matrix
    {
        get => (float[])_matrix.Clone();
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Length != 20)
                throw new ArgumentException("A color matrix has 20 values.", nameof(Matrix));
            _matrix = (float[])value.Clone();
        }
    }

    /// <inheritdoc />
    public override string Key => "ColorMatrix;" + string.Join(',', Array.ConvertAll(_matrix, value => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));

    /// <inheritdoc />
    public override SKImage Transform(SKImage source, float pixelsPerDip)
    {
        var matrix = _matrix;
        return Render(source.Width, source.Height, canvas =>
        {
            using var filter = SKColorFilter.CreateColorMatrix(matrix);
            using var paint = new SKPaint { ColorFilter = filter };
            canvas.DrawImage(source, 0, 0, Sampling, paint);
        });
    }
}

/// <summary>Converts the image to grayscale (Rec. 709 luma; FFImageLoading's <c>GrayscaleTransformation</c>).</summary>
public sealed class SkUiGrayscaleTransformation() : SkUiColorMatrixTransformation(
[
    0.2126f, 0.7152f, 0.0722f, 0, 0,
    0.2126f, 0.7152f, 0.0722f, 0, 0,
    0.2126f, 0.7152f, 0.0722f, 0, 0,
    0, 0, 0, 1, 0
]);

/// <summary>Gives the image a sepia tone (FFImageLoading's <c>SepiaTransformation</c>).</summary>
public sealed class SkUiSepiaTransformation() : SkUiColorMatrixTransformation(
[
    0.393f, 0.769f, 0.189f, 0, 0,
    0.349f, 0.686f, 0.168f, 0, 0,
    0.272f, 0.534f, 0.131f, 0, 0,
    0, 0, 0, 1, 0
]);
