namespace MauiSkiaUi;

/// <summary>
/// Finds the build-processed file of a <c>MauiImage</c> for the display density, as MAUI's image source services do,
/// and reports its scale (pixels per DIP), so it lays out at its base size:
/// <list type="bullet">
/// <item>Android: the drawable by lower-case name without extension; Android picks the density bucket (drawable-xxhdpi: 3).
/// A drawable defined in XML (a vector, shape, layer list, inset) is drawn by Android instead (<see cref="RenderXmlDrawable"/>).</item>
/// <item>iOS / Mac Catalyst: <c>name@3x.png</c> / <c>name@2x.png</c> up to the screen scale, then <c>name.png</c>, in the bundle's resources.</item>
/// <item>Windows: <c>name.scale-NNN.png</c> nearest at or above the display scale, then <c>name.png</c>, in the app folder.</item>
/// </list>
/// Resizetizer flattens folders and writes SVGs as PNGs, so <c>icons/home.svg</c> is found as <c>home.png</c>.
/// </summary>
internal static class SkUiPackageImages
{
    /// <summary>The opened MauiImage and its scale, or <c>null</c> when the app has none by that name (no platform: always).</summary>
    internal static (Stream Stream, float Scale)? TryOpen(string name, float displayScale)
    {
#if ANDROID || IOS || MACCATALYST || WINDOWS
        try
        {
            return TryOpenPlatform(name, displayScale);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return null;
        }
#else
        return null;
#endif
    }

    /// <summary>
    /// The resource id of the app's Android drawable named <paramref name="name"/> when it is defined in XML (a vector, a
    /// shape, a layer list…), which only Android can draw; 0 for a bitmap drawable, none by that name, or another platform.
    /// </summary>
    internal static int FindXmlDrawable(string name)
    {
#if ANDROID
        try
        {
            var context = Android.App.Application.Context;
            if (context.Resources is not { } resources || DrawableId(context, resources, name) is not (var id and not 0))
                return 0;
            var value = new Android.Util.TypedValue();
            resources.GetValue(id, value, resolveRefs: true);
            return value.String?.ToString() is { } file && file.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ? id : 0;
        }
        catch (Android.Content.Res.Resources.NotFoundException)
        {
            return 0;
        }
#else
        return 0;
#endif
    }

    /// <summary>
    /// Draws the XML drawable <paramref name="id"/> (<see cref="FindXmlDrawable"/>) into an image. Its intrinsic size (in
    /// DIPs) is its size; it is drawn at that size in pixels, or, being a vector, at the decode bounds
    /// (<paramref name="maxPixels"/>: fitting inside them, or covering them with <paramref name="fill"/>), so it stays sharp
    /// when the view is larger. A drawable without an intrinsic size (a shape) needs both bounds.
    /// </summary>
    internal static SkUiDecodedImage RenderXmlDrawable(int id, SkiaSharp.SKSizeI maxPixels, bool fill)
    {
#if ANDROID
        var context = Android.App.Application.Context;
        // The activity's theme, as a native image's view resolves ?attr/ colors and tints with (the application's is the
        // manifest one: in a MAUI app, the splash theme). Mutated: drawn here on the thread pool, the drawable must not share
        // its state (a vector's cached bitmap) with instances of it drawn elsewhere.
        var theme = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.Theme ?? context.Theme;
        using var loaded = context.Resources?.GetDrawable(id, theme) ?? throw new InvalidDataException("The Android drawable could not be loaded.");
        var drawable = loaded.Mutate(); // itself (the same peer), with state of its own
        var density = context.Resources?.DisplayMetrics?.Density is > 0 and var value ? value : 1f;
        var intrinsic = new SkiaSharp.SKSizeI(drawable.IntrinsicWidth, drawable.IntrinsicHeight);
        if (intrinsic.Width <= 0 || intrinsic.Height <= 0)
        {
            if (maxPixels.Width <= 0 || maxPixels.Height <= 0)
                throw new InvalidDataException("The Android drawable has no intrinsic size (a shape): set DownsampleWidth and DownsampleHeight, or DownsampleToViewSize.");
            intrinsic = maxPixels;
        }
        var target = RenderSize(intrinsic, maxPixels, fill);
        using var bitmap = Android.Graphics.Bitmap.CreateBitmap(target.Width, target.Height, Android.Graphics.Bitmap.Config.Argb8888!)
            ?? throw new InvalidOperationException("No bitmap for the Android drawable.");
        using (var canvas = new Android.Graphics.Canvas(bitmap))
        {
            drawable.SetBounds(0, 0, target.Width, target.Height);
            drawable.Draw(canvas);
        }
        var image = SkiaSharp.Views.Android.AndroidExtensions.ToSKImage(bitmap) ?? throw new InvalidOperationException("The drawn Android drawable could not be copied.");
        bitmap.Recycle();
        // A vector: larger bounds always draw it sharper.
        return new SkUiDecodedImage([image], [], new Size(intrinsic.Width / (double)density, intrinsic.Height / (double)density)) { IsReduced = true };
#else
        throw new PlatformNotSupportedException("Android drawables exist only on Android.");
#endif
    }

    /// <summary>
    /// Pixels to draw a vector of <paramref name="intrinsic"/> pixels at: the bounds when there are any (scaled up too), the
    /// intrinsic size otherwise; at most <see cref="SkUiImageDecoder.MaxDecodeDimension"/> on the longer side.
    /// </summary>
    internal static SkiaSharp.SKSizeI RenderSize(SkiaSharp.SKSizeI intrinsic, SkiaSharp.SKSizeI maxPixels, bool fill)
    {
        var width = maxPixels.Width / (double)intrinsic.Width;
        var height = maxPixels.Height / (double)intrinsic.Height;
        var scale = (maxPixels.Width > 0, maxPixels.Height > 0) switch
        {
            (true, true) => fill ? Math.Max(width, height) : Math.Min(width, height),
            (true, false) => width,
            (false, true) => height,
            _ => 1
        };
        scale = Math.Min(scale, SkUiImageDecoder.MaxDecodeDimension / (double)Math.Max(intrinsic.Width, intrinsic.Height));
        return new SkiaSharp.SKSizeI(Math.Max(1, (int)Math.Round(intrinsic.Width * scale)), Math.Max(1, (int)Math.Round(intrinsic.Height * scale)));
    }

    /// <summary>File names to try for <paramref name="name"/> with a density <paramref name="suffix"/> (before the extension).</summary>
    private static IEnumerable<string> Candidates(string name, string suffix)
    {
        var file = Path.GetFileName(name);
        var baseName = Path.GetFileNameWithoutExtension(file);
        var extension = Path.GetExtension(file);
        if (extension.Length > 0 && !extension.Equals(".svg", StringComparison.OrdinalIgnoreCase))
            yield return baseName + suffix + extension;
        if (!extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
            yield return baseName + suffix + ".png";
    }

#if ANDROID
    private static (Stream, float)? TryOpenPlatform(string name, float displayScale)
    {
        var context = Android.App.Application.Context;
        var resources = context.Resources;
        if (resources is null || context.PackageName is null)
            return null;
        var id = DrawableId(context, resources, name);
        if (id == 0)
            return null;
        var value = new Android.Util.TypedValue();
        var stream = resources.OpenRawResource(id, value);
        if (stream is null)
            return null;
        // The density of the bucket Android picked: 0 is the default (mdpi), 0xFFFF "none" (nodpi).
        var density = value.Density is 0 or 0xFFFF ? 160 : value.Density;
        return (stream, density / 160f);
    }

    /// <summary>The app's drawable by lower-case name without extension, as MAUI finds a MauiImage; 0 without one.</summary>
    private static int DrawableId(Android.Content.Context context, Android.Content.Res.Resources resources, string name)
    {
        var resourceName = Path.GetFileNameWithoutExtension(name).ToLowerInvariant();
        if (resourceName.Length == 0 || context.PackageName is null)
            return 0;
#pragma warning disable CA1422 // GetIdentifier is discouraged, not removed: MAUI resolves MauiImage drawables the same way
        return resources.GetIdentifier(resourceName, "drawable", context.PackageName);
#pragma warning restore CA1422
    }
#elif IOS || MACCATALYST
    private static (Stream, float)? TryOpenPlatform(string name, float displayScale)
    {
        var folder = Foundation.NSBundle.MainBundle.ResourcePath;
        if (string.IsNullOrEmpty(folder))
            return null;
        var maxScale = Math.Clamp((int)Math.Ceiling(displayScale), 1, 3);
        for (var scale = maxScale; scale >= 1; scale--)
        {
            foreach (var file in Candidates(name, scale == 1 ? "" : $"@{scale}x"))
            {
                var path = Path.Combine(folder, file);
                if (File.Exists(path))
                    return (File.OpenRead(path), scale);
            }
        }
        // Asset catalogs (images added by other tooling): let UIKit find it, at its own scale.
        using var image = UIKit.UIImage.FromBundle(Path.GetFileNameWithoutExtension(name));
        if (image?.AsPNG() is not { } png)
            return null;
        return (png.AsStream(), (float)image.CurrentScale);
    }
#elif WINDOWS
    private static readonly int[] WindowsScales = [100, 125, 150, 200, 400];

    private static (Stream, float)? TryOpenPlatform(string name, float displayScale)
    {
        var folder = AppContext.BaseDirectory;
        var wanted = (int)Math.Round(displayScale * 100);
        // The nearest scale at or above the display's, then smaller ones, then the unqualified file.
        var order = WindowsScales.Where(scale => scale >= wanted).Concat(WindowsScales.Where(scale => scale < wanted).Reverse());
        foreach (var scale in order)
        {
            foreach (var file in Candidates(name, $".scale-{scale}"))
            {
                var path = Path.Combine(folder, file);
                if (File.Exists(path))
                    return (File.OpenRead(path), scale / 100f);
            }
        }
        foreach (var file in Candidates(name, ""))
        {
            var path = Path.Combine(folder, file);
            if (File.Exists(path))
                return (File.OpenRead(path), 1f);
        }
        return null;
    }
#endif
}
