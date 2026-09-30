using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// Registers Skia typefaces for font family names that <see cref="SKTypeface.FromFamilyName(string, SKFontStyleWeight, SKFontStyleWidth, SKFontStyleSlant)"/>
/// cannot resolve as an installed system font. Fonts registered with MAUI <c>ConfigureFonts</c> (<c>MauiFont</c> items or
/// embedded resources) are also picked up automatically, on every platform.
/// <see cref="SkUiLabel"/> and its subclasses consult this registry before falling back to system font lookup.
/// </summary>
public static class SkUiFonts
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, Func<Stream>> Factories = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, SKTypeface> Cache = new(StringComparer.Ordinal);
    private static readonly HashSet<string> Misses = new(StringComparer.Ordinal);

    /// <summary>
    /// Registers a typeface loader for a font family name, e.g. the same alias passed to <c>fonts.AddFont(file, alias)</c>.
    /// The stream is opened lazily and only once; call this from app startup before the family name is first painted.
    /// </summary>
    public static void Register(string familyName, Func<Stream> openFont)
    {
        ArgumentException.ThrowIfNullOrEmpty(familyName);
        ArgumentNullException.ThrowIfNull(openFont);
        lock (Gate)
        {
            Factories[familyName] = openFont;
            Misses.Remove(familyName);
            // Not disposed: retained pictures on the render thread may still reference the old typeface.
            Cache.Remove(familyName);
        }
    }

    /// <summary>Removes a registration and forgets its cached typeface (left to the GC: retained pictures may still use it).</summary>
    public static void Unregister(string familyName)
    {
        lock (Gate)
        {
            Factories.Remove(familyName);
            Misses.Remove(familyName);
            Cache.Remove(familyName);
        }
    }

    /// <summary>Resolves a registered family name to a cached, registry-owned typeface, or null if not registered/loadable.</summary>
    internal static SKTypeface? TryResolve(string familyName)
    {
        lock (Gate)
        {
            if (Cache.TryGetValue(familyName, out var typeface)) return typeface;
            if (Misses.Contains(familyName)) return null;
            var definitive = false;
            if (Factories.TryGetValue(familyName, out var open))
            {
                using var stream = open();
                typeface = SKTypeface.FromStream(stream);
            }
            else
            {
                typeface = ResolveMauiFont(familyName, out definitive);
            }
            if (typeface is not null) Cache[familyName] = typeface;
            // Remember a miss only when the MAUI registrar answered: before the app's services exist, retry later.
            else if (definitive) Misses.Add(familyName);
            return typeface;
        }
    }

    /// <summary>
    /// Falls back to fonts registered with MAUI <c>ConfigureFonts</c> (via <see cref="Microsoft.Maui.IFontRegistrar"/>),
    /// so app-embedded fonts work without an explicit <see cref="Register"/> call. What the registrar returns differs
    /// per platform (see <see cref="FromMauiFont"/>).
    /// </summary>
    /// <param name="alias">Family name / alias to look up.</param>
    /// <param name="definitive"><c>false</c> when the registrar could not be asked (app not started yet, lookup failed).</param>
    private static SKTypeface? ResolveMauiFont(string alias, out bool definitive)
    {
#if ANDROID || IOS || MACCATALYST || WINDOWS
        definitive = false;
        try
        {
            if (IPlatformApplication.Current?.Services.GetService(typeof(Microsoft.Maui.IFontRegistrar)) is not Microsoft.Maui.IFontRegistrar registrar)
                return null;
            var font = registrar.GetFont(alias);
            definitive = true;
            return font is { Length: > 0 } ? FromMauiFont(font) : null;
        }
        catch (Exception)
        {
            return null;
        }
#else
        definitive = true;
        return null;
#endif
    }

#if ANDROID || IOS || MACCATALYST || WINDOWS
    /// <summary>
    /// A typeface for what MAUI's font registrar returned (MAUI 10):
    /// <list type="bullet">
    /// <item>a file path: embedded-resource fonts copied to a cache folder (Android, Windows unpackaged);</item>
    /// <item>Android, <c>MauiFont</c> items: the font's file name in the APK's assets;</item>
    /// <item>iOS / Mac Catalyst: the font's PostScript name, after MAUI registered it with CoreText;</item>
    /// <item>Windows: an <c>ms-appx:</c> / <c>ms-appdata:</c> URI of the font file.</item>
    /// </list>
    /// </summary>
    private static SKTypeface? FromMauiFont(string font)
    {
        if (File.Exists(font))
            return SKTypeface.FromFile(font);
#if ANDROID
        return FromAndroidAsset(font);
#elif IOS || MACCATALYST
        return FromPostScriptName(font);
#else
        return WindowsFontPath(font) is { } path && File.Exists(path) ? SKTypeface.FromFile(path) : null;
#endif
    }
#endif

#if ANDROID
    private static SKTypeface? FromAndroidAsset(string fileName)
    {
        try
        {
            using var asset = Android.App.Application.Context.Assets?.Open(fileName);
            if (asset is null)
                return null;
            using var memory = new MemoryStream();
            asset.CopyTo(memory);
            return SKTypeface.FromData(SKData.CreateCopy(memory.ToArray()));
        }
        catch (Java.IO.IOException)
        {
            return null; // not an asset
        }
    }
#endif

#if IOS || MACCATALYST
    /// <summary>
    /// The face with <paramref name="postScriptName"/>: CoreText knows the family of a font MAUI registered, and Skia's
    /// CoreText font manager lists that family's faces (Skia matches families, not PostScript names).
    /// </summary>
    private static SKTypeface? FromPostScriptName(string postScriptName)
    {
        using var coreTextFont = new CoreText.CTFont(postScriptName, 12);
        // CoreText substitutes a default font for unknown names.
        if (coreTextFont.PostScriptName != postScriptName)
            return null;
        using var faces = SKFontManager.Default.GetFontStyles(coreTextFont.FamilyName);
        for (var index = 0; index < faces.Count; index++)
        {
            var face = faces.CreateTypeface(index);
            if (face?.PostScriptName == postScriptName)
                return face;
            face?.Dispose();
        }
        return null;
    }
#endif

#if WINDOWS
    /// <summary>
    /// The file behind MAUI's Windows font URIs: <c>ms-appx:///Fonts/x.ttf</c> (the app folder),
    /// <c>ms-appx://c/Users/…/x.ttf</c> (an absolute path written as a URI, unpackaged embedded fonts) or
    /// <c>ms-appdata:///temp/Fonts/x.ttf</c> (a packaged app's temporary folder); an optional <c>#Family</c> suffix.
    /// </summary>
    private static string? WindowsFontPath(string font)
    {
        var hash = font.IndexOf('#');
        if (hash >= 0)
            font = font[..hash];
        if (!Uri.TryCreate(font, UriKind.Absolute, out var uri))
            return null;
        var path = Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        switch (uri.Scheme)
        {
            case "ms-appx" when uri.Host.Length == 1:
                return $"{uri.Host}:{Path.DirectorySeparatorChar}{path}";
            case "ms-appx":
                return Path.Combine(AppContext.BaseDirectory, path);
            case "ms-appdata" when path.StartsWith("temp" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase):
                try
                {
                    return Path.Combine(Windows.Storage.ApplicationData.Current.TemporaryFolder.Path, path["temp".Length..].TrimStart(Path.DirectorySeparatorChar));
                }
                catch (InvalidOperationException)
                {
                    return null; // not a packaged app
                }
            default:
                return null;
        }
    }
#endif
}
