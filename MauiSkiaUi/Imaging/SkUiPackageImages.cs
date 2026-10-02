namespace MauiSkiaUi;

/// <summary>
/// Finds the build-processed file of a <c>MauiImage</c> for the display density, as MAUI's image source services do,
/// and reports its scale (pixels per DIP), so it lays out at its base size:
/// <list type="bullet">
/// <item>Android: the drawable by lower-case name without extension; Android picks the density bucket (drawable-xxhdpi: 3).</item>
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
        var resourceName = Path.GetFileNameWithoutExtension(name).ToLowerInvariant();
        if (resourceName.Length == 0)
            return null;
#pragma warning disable CA1422 // GetIdentifier is discouraged, not removed: MAUI resolves MauiImage drawables the same way
        var id = resources.GetIdentifier(resourceName, "drawable", context.PackageName);
#pragma warning restore CA1422
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
